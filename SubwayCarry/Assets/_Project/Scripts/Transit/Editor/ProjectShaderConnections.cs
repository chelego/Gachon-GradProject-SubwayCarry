#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SubwayCarry.Transit.Editor
{
    /// <summary>Checks saved shader references and actual renderer usage without rebuilding scenes.</summary>
    public static class ProjectShaderConnections
    {
        private const string Art = "Assets/_Project/Art/";
        private const string Sprites = Art + "Sprites/";
        public const string ReportPath = "Library/ShaderConnectionAudit.txt";

        public static Shader RequireShader(string path)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (ShaderBroken(shader))
            {
                string messages = shader ? string.Join("\n", ShaderUtil.GetShaderMessages(shader)
                    .Select(message => message.severity + ": " + message.message)) : "Shader asset is missing.";
                throw new InvalidOperationException("Shader unavailable: " + path + "\n" + messages);
            }
            return shader;
        }

        private static string[] MaterialPaths()
        {
            return AssetDatabase.FindAssets("t:Material", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().ToArray();
        }

        private static string ExpectedShaderPath(string materialPath)
        {
            if (materialPath.StartsWith(Art + "PrototypeMaterials/", StringComparison.Ordinal))
                return "Packages/com.unity.render-pipelines.universal/Shaders/Unlit.shader";
            if (materialPath.StartsWith(Sprites + "Materials/", StringComparison.Ordinal))
                return "Packages/com.unity.render-pipelines.universal/Shaders/2D/Sprite-Lit-Default.shader";
            if (materialPath == Sprites + "SubwayTopDown/InteriorUnlit.mat"
                || materialPath == Sprites + "StationExpansion/StationUnlit.mat")
                return "Packages/com.unity.render-pipelines.universal/Shaders/2D/Sprite-Unlit-Default.shader";
            if (materialPath == Sprites + "StationExpansion/StationGeometry.mat")
                return Sprites + "StationExpansion/StationVertexColor.shader";
            if (new[] { "QuarterSolid", "QuarterFloor", "QuarterDoor" }.Any(
                name => materialPath == Sprites + "SubwayQuarterView/" + name + ".mat"))
                return Sprites + "SubwayQuarterView/SubwayQuarterSurface.shader";

            // For other materials, use their saved GUID rather than guessing a shader.
            if (!materialPath.EndsWith(".mat", StringComparison.Ordinal) || !File.Exists(materialPath))
                return null;
            var match = Regex.Match(File.ReadAllText(materialPath), @"m_Shader:\s*\{[^}]*guid:\s*([a-f0-9]+)");
            return match.Success ? AssetDatabase.GUIDToAssetPath(match.Groups[1].Value) : null;
        }

        private static bool ShaderBroken(Shader shader)
        {
            return shader == null || shader.name == "Hidden/InternalErrorShader"
                || !shader.isSupported || ShaderUtil.ShaderHasError(shader);
        }

        private static string ShaderStatus(Shader shader)
        {
            if (!shader) return "MISSING SHADER";
            if (shader.name == "Hidden/InternalErrorShader") return "INTERNAL ERROR SHADER";
            return "supported=" + shader.isSupported + "; compiler errors=" + ShaderUtil.ShaderHasError(shader);
        }

        private static string CompileCustomPasses(Material material)
        {
            if (!material.shader || !AssetDatabase.GetAssetPath(material.shader)
                .StartsWith("Assets/", StringComparison.Ordinal)) return null;

            // Shader variants compile on demand. Check the actual variant used by this material.
            var passes = new List<string>();
            for (int pass = 0; pass < material.passCount; pass++)
            {
                ShaderUtil.CompilePass(material, pass, true);
                passes.Add(pass + "=" + ShaderUtil.IsPassCompiled(material, pass));
            }
            return string.Join(", ", passes);
        }

        [MenuItem("SubwayCarry/Diagnostics/Audit Project Shader Connections")]
        public static void AuditProject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before auditing project shader connections.");

            var report = new StringBuilder();
            report.AppendLine("Shader connection audit: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("Unity: " + Application.unityVersion);
            report.AppendLine("Graphics API: " + SystemInfo.graphicsDeviceType);
            report.AppendLine("Default pipeline: " + GraphicsSettings.defaultRenderPipeline);
            report.AppendLine("Active pipeline: " + GraphicsSettings.currentRenderPipeline);
            int materials = 0, assets = 0, slots = 0, issues = 0;
            var paths = MaterialPaths().Concat(AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)
                    && (p.EndsWith(".ttf") || p.EndsWith(".otf")))).Distinct();
            foreach (string path in paths)
            {
                foreach (var material in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    materials++;
                    string compiledPasses = CompileCustomPasses(material);
                    if (ShaderBroken(material.shader)) issues++;
                    report.AppendLine("MATERIAL " + path + " [" + material.name + "] -> "
                        + (material.shader ? material.shader.name : "MISSING")
                        + "; shader asset=" + AssetDatabase.GetAssetPath(material.shader)
                        + "; status=" + ShaderStatus(material.shader));
                    if (compiledPasses != null) report.AppendLine("  Compiled custom shader passes: " + compiledPasses);
                    if (File.Exists(path) && path.EndsWith(".mat", StringComparison.Ordinal))
                    {
                        var stored = Regex.Match(File.ReadAllText(path), @"m_Shader:\s*\{[^}]*guid:\s*([a-f0-9]+)");
                        if (stored.Success)
                            report.AppendLine("  Saved GUID: " + stored.Groups[1].Value
                                + "; resolved=" + AssetDatabase.GUIDToAssetPath(stored.Groups[1].Value));
                    }
                    if (material.shader)
                        foreach (var message in ShaderUtil.GetShaderMessages(material.shader))
                            report.AppendLine("  " + message.severity + ": " + message.message);
                }
            }

            foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p =>
                p.StartsWith("Assets/", StringComparison.Ordinal)
                && (p.EndsWith(".unity") || p.EndsWith(".prefab"))).OrderBy(p => p))
            {
                UnityEngine.SceneManagement.Scene preview = default;
                try
                {
                    GameObject[] roots;
                    if (path.EndsWith(".unity"))
                    {
                        preview = EditorSceneManager.OpenPreviewScene(path);
                        roots = preview.GetRootGameObjects();
                    }
                    else
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (!prefab) continue;
                        roots = new[] { prefab };
                    }
                    assets++;
                    int localSlots = 0, localIssues = 0;
                    foreach (var renderer in roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)))
                        foreach (var material in renderer.sharedMaterials)
                        {
                            slots++;
                            localSlots++;
                            if (!material || ShaderBroken(material.shader))
                            {
                                issues++;
                                localIssues++;
                                report.AppendLine("  INVALID " + path + " / " + renderer.name
                                    + " / " + (material ? material.name + "; " + ShaderStatus(material.shader) : "MISSING MATERIAL"));
                            }
                        }
                    report.AppendLine("ASSET " + path + ": slots=" + localSlots + "; invalid=" + localIssues);
                }
                finally
                {
                    if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                }
            }
            string summary = "Materials=" + materials + "; scenes/prefabs=" + assets
                + "; renderer material slots=" + slots + "; invalid checks=" + issues;
            report.AppendLine(summary);
            Directory.CreateDirectory("Library");
            File.WriteAllText(ReportPath, report.ToString(), new UTF8Encoding(false));
            Debug.Log(summary + ". Full report: " + Path.GetFullPath(ReportPath));
        }

        [MenuItem("SubwayCarry/Diagnostics/Repair Project Shader Connections")]
        public static void RepairProject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before repairing project shader connections.");

            var paths = MaterialPaths().Where(p => p.EndsWith(".mat", StringComparison.Ordinal)).ToArray();
            var shaders = new Dictionary<string, Shader>();
            foreach (string shaderPath in paths.Select(ExpectedShaderPath).Where(p => !string.IsNullOrEmpty(p)).Distinct())
            {
                AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                if (ShaderBroken(shader))
                {
                    Debug.LogWarning("Cannot repair using shader: " + shaderPath + ". Check the shader compile messages.");
                    continue;
                }
                shaders.Add(shaderPath, shader);
            }
            int repaired = 0;
            foreach (string path in paths)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material || !ShaderBroken(material.shader)) continue;
                string expected = ExpectedShaderPath(path);
                if (string.IsNullOrEmpty(expected) || !shaders.TryGetValue(expected, out Shader shader))
                {
                    Debug.LogWarning("Shader connection remains unresolved: " + path);
                    continue;
                }
                Undo.RecordObject(material, "Repair project shader connection");
                string[] keywords = material.shaderKeywords;
                int renderQueue = material.rawRenderQueue;
                material.shader = shader;
                material.shaderKeywords = keywords;
                material.renderQueue = renderQueue;
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                repaired++;
            }
            SceneView.RepaintAll();
            Debug.Log("Repaired project shader connections: " + repaired);
            AuditProject();
        }
    }
}
#endif

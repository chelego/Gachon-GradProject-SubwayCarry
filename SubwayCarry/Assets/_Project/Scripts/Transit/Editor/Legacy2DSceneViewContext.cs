#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SubwayCarry.Transit.Editor
{
    /// <summary>Keeps archived sprite scenes in their original 2D editor context.</summary>
    [InitializeOnLoad]
    internal static class Legacy2DSceneViewContext
    {
        static Legacy2DSceneViewContext()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.delayCall += RestoreActiveContext;
        }

        private static bool IsLegacy2D(Scene scene)
        {
            if (!scene.path.StartsWith("Assets/_Project/Scenes/Legacy/")) return false;
            switch (scene.name)
            {
                case "GachonUniv":
                case "GachonUniv_2D_Backup":
                case "Wangsimni":
                case "Wangsimni_2D_Backup":
                case "Subway_QuarterView":
                case "Subway_QuarterView_2D_Backup":
                case "Subway_TopDown":
                    return true;
                default:
                    return false;
            }
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (mode == OpenSceneMode.Single) RestoreActiveContext();
        }

        private static void RestoreActiveContext()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;
            var view = SceneView.lastActiveSceneView;
            if (IsLegacy2D(scene))
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
                if (pipeline) QualitySettings.renderPipeline = pipeline;
                QualitySettings.antiAliasing = 0;
                if (!view) return;
                view.in2DMode = true;
                view.orthographic = true;
                bool hasBounds = false;
                Bounds bounds = default;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                    {
                        if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                        if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                        else bounds.Encapsulate(renderer.bounds);
                    }
                }
                if (hasBounds) view.Frame(bounds, true);
                view.Repaint();
                return;
            }
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.activeInHierarchy || !root.GetComponent<SubwaySceneRenderPipeline3D>()) continue;
                if (view && Camera.main)
                {
                    var camera = Camera.main.transform;
                    view.in2DMode = false;
                    view.LookAt(camera.position + camera.forward * 8f, camera.rotation, 4f, false, true);
                    view.Repaint();
                }
                break;
            }
        }
    }
}
#endif

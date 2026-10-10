#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SubwayCarry.Core.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SubwayCarry.Transit.Editor
{
    /// <summary>Reproducible, native-Unity authoring for the Bokjeong and Suseo station environments.</summary>
    public static class StationExpansionBuilder
    {
        private const string Root = "Assets/_Project";
        private const string Art = Root + "/Art/Sprites/StationExpansion";
        private const string Sprites = Root + "/Art/Sprites";
        private static readonly Color Ink = Hex("26343C");
        private static readonly Color Yellow = Hex("F4BD24");
        private static readonly Color Pink = Hex("CF3978");
        private static readonly Color Orange = Hex("EF8131");
        private static readonly Color Violet = Hex("905690");
        private static readonly Color Blue = Hex("38749A");
        private static Sprite solid;
        private static Font font;
        private static Material spriteMaterial;
        private static Material geometryMaterial;
        private static string meshPrefix;
        private static int meshIndex;
        private static readonly List<Transform> boarding = new List<Transform>();
        private static readonly List<Transform> exits = new List<Transform>();

        [MenuItem("SubwayCarry/Stations/Create Bokjeong and Suseo (missing scenes only)")]
        public static void BuildMissing()
        {
            PrepareAssets();
            BuildStation(false, false);
            BuildStation(true, false);
            AssetDatabase.SaveAssets();
        }

        // Explicit entry point for authoring iterations; never touches reference scenes.
        public static void RebuildGenerated()
        {
            PrepareAssets();
            BuildStation(false, true);
            BuildStation(true, true);
            AssetDatabase.SaveAssets();
        }

        private static Shader RequireGeometryShader()
        {
            const string path = Art + "/StationVertexColor.shader";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
                throw new InvalidOperationException("Missing station shader. Restore " + path + " and its .meta from Git.");
            if (ShaderUtil.ShaderHasError(shader) || !shader.isSupported)
                throw new InvalidOperationException("Station shader cannot compile or is unsupported. Select " + path + " and check the Console.");
            return shader;
        }

        [MenuItem("SubwayCarry/Stations/Repair Bokjeong and Suseo Material Shader")]
        public static void RepairMaterialShader()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before repairing station materials.");

            const string shaderPath = Art + "/StationVertexColor.shader";
            AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var shader = RequireGeometryShader();
            const string materialPath = Art + "/StationGeometry.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
                throw new InvalidOperationException("Missing station material: " + materialPath);
            if (material.shader != shader)
            {
                Undo.RecordObject(material, "Repair station surface shader");
                material.shader = shader;
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
            }
            AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            SceneView.RepaintAll();
            Debug.Log("Bokjeong and Suseo surface material verified: StationGeometry uses " + shader.name + ".");
        }

        private static void PrepareAssets()
        {
            Directory.CreateDirectory(Art);
            Directory.CreateDirectory(Art + "/Geometry");
            Directory.CreateDirectory(Root + "/Data/Stations");
            Directory.CreateDirectory(Root + "/Prefabs/Stations");
            AssetDatabase.Refresh();
            string whitePath = Sprites + "/StationSolid.png";
            if (!File.Exists(whitePath))
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                texture.Apply();
                File.WriteAllBytes(whitePath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(whitePath);
            }
            foreach (string path in new[] { whitePath, Sprites + "/StationElevator.png", Sprites + "/BokjeongRoundColumn.png" })
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing required station art: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            solid = SpriteAt("StationSolid");
            font = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Art/Fonts/NanumGothic-Regular.ttf");
            if (font == null) throw new InvalidOperationException("Station Korean font is missing.");
            var spriteShader = ProjectShaderConnections.RequireShader(
                "Packages/com.unity.render-pipelines.universal/Shaders/2D/Sprite-Unlit-Default.shader");
            string materialPath = Art + "/StationUnlit.mat";
            spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (spriteMaterial == null)
            {
                spriteMaterial = new Material(spriteShader);
                AssetDatabase.CreateAsset(spriteMaterial, materialPath);
            }
            else if (spriteMaterial.shader != spriteShader)
            {
                spriteMaterial.shader = spriteShader;
                EditorUtility.SetDirty(spriteMaterial);
            }
            var geometryShader = RequireGeometryShader();
            string geometryPath = Art + "/StationGeometry.mat";
            geometryMaterial = AssetDatabase.LoadAssetAtPath<Material>(geometryPath);
            if (geometryMaterial == null)
            {
                geometryMaterial = new Material(geometryShader);
                AssetDatabase.CreateAsset(geometryMaterial, geometryPath);
            }
            else if (geometryMaterial.shader != geometryShader)
            {
                geometryMaterial.shader = geometryShader;
                EditorUtility.SetDirty(geometryMaterial);
            }
            string prefabPath = Root + "/Prefabs/Stations/AccessibleStationElevator.prefab";
            if (!File.Exists(prefabPath))
            {
                var prefabRoot = new GameObject("Accessible Station Elevator");
                Sprite(prefabRoot.transform, "StationElevator", "Elevator Sprite", Vector2.up * 2.15f, 0.49f, Color.white, 1800);
                var footprint = prefabRoot.AddComponent<PolygonCollider2D>();
                footprint.points = new[] { new Vector2(-1, 0), new Vector2(0, -0.625f), new Vector2(1, 0), new Vector2(0, 0.625f) };
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                UnityEngine.Object.DestroyImmediate(prefabRoot);
            }
        }

        private static void BuildStation(bool suseo, bool replace)
        {
            string id = suseo ? "Suseo" : "Bokjeong";
            meshPrefix = id;
            meshIndex = 0;
            string scenePath = Root + "/Scenes/" + id + ".unity";
            if (File.Exists(scenePath) && !replace) return;
            if (SceneManager.GetSceneByPath(scenePath).isLoaded)
                throw new InvalidOperationException("Close " + id + " before rebuilding; open scene edits are preserved.");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            boarding.Clear();
            exits.Clear();
            GameObject root = new GameObject(id + " Station");
            var layout = root.AddComponent<StationSceneLayout>();
            Camera camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.backgroundColor = Hex("141F29");
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.allowDynamicResolution = false;
            camera.transparencySortMode = TransparencySortMode.CustomAxis;
            camera.transparencySortAxis = Vector3.up;
            camera.gameObject.AddComponent<AudioListener>();
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;

            Transform main = Group(root.transform, suseo ? "B4 · 수인분당선 상대식 승강장" : "B3 · 수인분당선 섬식 승강장");
            Transform upper = Group(root.transform, suseo ? "환승 중층 · 3호선 / GTX-A / SRT" : "B2 · 8호선 섬식 승강장");
            int length = suseo ? 14 : 12;
            Platform(main, length, !suseo, suseo, Yellow, suseo ? "수서" : "복정", suseo ? "Suseo" : "Bokjeong",
                suseo ? "K221" : "K222", suseo ? "대모산입구  ←     →  복정" : "수서  ←     →  가천대", "SB");
            Vector2 stairs = suseo ? new Vector2(10.4f, 1.5f) : new Vector2(7.0f, 1.6f);
            Vector2 lift = suseo ? new Vector2(12.1f, 2.1f) : new Vector2(10.1f, 1.7f);
            FacilityArt(main, stairs, lift, suseo ? "3호선 · GTX-A · SRT" : "8호선 갈아타는 곳", suseo ? Orange : Pink);
            Transform mainArrival = Point(main, "Arrival from transfer level", P(stairs.x - 1.45f, stairs.y));
            Transform mainLiftArrival = Point(main, "Accessible arrival", P(lift.x - 0.95f, lift.y - 0.65f));
            Transform upperArrival;
            Transform upperLiftArrival;
            if (suseo)
            {
                TransferHall(upper);
                upperArrival = Point(upper, "Arrival from B4", P(1.2f, 1.7f));
                upperLiftArrival = Point(upper, "Accessible arrival", P(1.0f, 3.3f));
            }
            else
            {
                Platform(upper, 12, true, false, Pink, "복정", "Bokjeong", "820", "장지  ←     →  남위례", "L8");
                FacilityArt(upper, stairs, lift, "수인분당선  B3", Yellow);
                upperArrival = Point(upper, "Arrival from B3", P(stairs.x - 1.45f, stairs.y));
                upperLiftArrival = Point(upper, "Accessible arrival", P(lift.x - 0.95f, lift.y - 0.65f));
                exits.Add(Point(upper, "Exit B1 concourse · exits 1–4", P(11f, 1.0f)));
                Sign(upper, "나가는 곳  ↑  1 · 2 · 3 · 4", "B1  CONCOURSE", P(10.8f, 1.2f) + Vector2.up * 4.1f, 4.5f, Yellow, 2400);
            }

            var levels = new[]
            {
                new StationSceneLayout.Level { label = suseo ? "B4 수인분당선" : "B3 수인분당선", geometry = main.gameObject,
                    arrival = Point(main, "Player Spawn", P(1.0f, 1.3f)), cameraTarget = Point(root.transform, "Platform Camera Target", new Vector2(suseo ? 12f : 9.8f, suseo ? 11.6f : 10.5f)), cameraSize = suseo ? 16f : 14.8f },
                new StationSceneLayout.Level { label = suseo ? "환승 중층" : "B2 8호선", geometry = upper.gameObject,
                    arrival = upperArrival, cameraTarget = Point(root.transform, "Transfer Camera Target", new Vector2(suseo ? 4.9f : 9.8f, suseo ? 9.2f : 10.5f)), cameraSize = suseo ? 12.8f : 14.8f }
            };
            layout.Configure(CreateData(suseo, stairs, lift), camera, levels, boarding.ToArray(), exits.ToArray());
            Link(main, "Stairs to transfer level", P(stairs.x - 1.2f, stairs.y - 0.05f), layout, 1, upperArrival);
            Link(main, "Elevator to transfer level", P(lift.x - 0.85f, lift.y - 0.65f), layout, 1, upperLiftArrival);
            Link(upper, "Stairs to Suin-Bundang", upperArrival.localPosition, layout, 0, mainArrival);
            Link(upper, "Elevator to Suin-Bundang", upperLiftArrival.localPosition, layout, 0, mainLiftArrival);
            var section = root.AddComponent<StationSection>();
            var so = new SerializedObject(section);
            so.FindProperty("stationData").objectReferenceValue = layout.Data;
            so.FindProperty("sectionLabel").stringValue = suseo ? "수서" : "복정";
            so.FindProperty("spawnPoint").objectReferenceValue = levels[0].arrival;
            so.FindProperty("cameraTarget").objectReferenceValue = levels[0].cameraTarget;
            so.FindProperty("cameraOrthoSize").floatValue = levels[0].cameraSize;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                EditorSceneManager.CloseScene(scene, true);
                throw new IOException("Could not save station scene: " + scenePath);
            }
            Debug.Log("[Station Expansion] Saved " + scenePath + ", levels=" + levels.Length + ", boarding anchors=" + boarding.Count);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void Platform(Transform parent, int length, bool island, bool suseo, Color line,
            string korean, string english, string code, string neighbours, string platformPrefix)
        {
            Transform floor = Group(parent, "01 · Floor and tactile wayfinding");
            Floor(floor, length, 4, suseo ? Hex("C7CBC9") : Hex("CBCAC3"));
            FloorPattern(floor, length, suseo);
            Transform screens = Group(parent, "02 · Platform screen doors");
            int count = length == 14 ? 9 : 8;
            DoorRow(screens, length, count, 3.5f, false, line, platformPrefix + "_NORTH");
            if (island) DoorRow(screens, length, count, -0.5f, true, line, platformPrefix + "_SOUTH");
            else
            {
                CeramicWall(parent, length);
            }
            Transform furniture = Group(parent, "03 · Furniture and clear circulation");
            foreach (float u in suseo ? new[] { 2.4f, 5.3f } : new[] { 2.5f, 4.7f })
            {
                if (suseo)
                {
                    Prop(furniture, "bench_2", "Wall-side waiting bench", P(u, 0.2f), 1.35f, 0.8f, Color.white, new Vector2(2.8f, 0.75f));
                    CeramicColumn(furniture, P(u + 0.7f, 0.6f));
                }
                else
                    Prop(furniture, "BokjeongRoundColumn", "Red round column with circular wooden bench", P(u, 1.45f), 0.30f, 1.42f,
                        Color.white, new Vector2(2.3f, 1.15f));
            }
            Prop(furniture, "vending", "Drink vending machine", P(0.65f, 2.15f), 0.56f, 1.15f, Color.white, new Vector2(1.25f, 0.8f));
            Sign(parent, korean + "   " + code, english + "  |  " + (line == Pink ? "8호선" : "수인분당선") + "\n" + neighbours,
                P(3.55f, 2.05f) + Vector2.up * 4.0f, 6f, line, 2200);
            Sign(parent, line == Pink ? "←  잠실 · 별내" : "←  왕십리 · 청량리", line == Pink ? "8호선  |  LINE 8" : "수인분당선  |  SUIN-BUNDANG",
                P(length - 3.6f, 3.5f) + Vector2.up * 4.3f, 5f, line, 2200);
            // Wayfinding is carried by signs and tactile paving, not invented line-colour floor stripes.
            Boundary(parent, "Platform perimeter", new[] { P(-0.5f, -0.5f), P(length - 0.5f, -0.5f), P(length - 0.5f, 3.5f), P(-0.5f, 3.5f), P(-0.5f, -0.5f) });
        }

        private static void Floor(Transform parent, int length, int width, Color tint)
        {
            // One flat shared coordinate system: no per-tile side faces, alpha margins or sprite pivots.
            var surface = new Surface();
            surface.Tile(-0.5f, -0.5f, length - 0.5f, width - 0.5f, Hex("9EA29E"));
            for (int u = 0; u < length * 2; u++)
                for (int v = 0; v < width * 2; v++)
                {
                    float x = -0.5f + u * 0.5f, y = -0.5f + v * 0.5f;
                    float tone = 0.985f + ((u * 7 + v * 11) % 5) * 0.0075f;
                    surface.Tile(x + 0.006f, y + 0.006f, x + 0.494f, y + 0.494f, tint * new Color(tone, tone, tone, 1));
                }
            surface.Save(parent, "Seamless stone floor", -8000);
            var edge = new Surface();
            Vector2 a = P(-0.5f, -0.5f), b = P(length - 0.5f, -0.5f), c = P(-0.5f, width - 0.5f);
            edge.Quad(a, b, b + Vector2.down * 0.16f, a + Vector2.down * 0.16f, Hex("777E80"));
            edge.Quad(c, a, a + Vector2.down * 0.16f, c + Vector2.down * 0.16f, Hex("59656B"));
            edge.Save(parent, "Platform outer slab edge", -7999);
        }

        private static void DoorRow(Transform parent, int length, int count, float v, bool foreground, Color line, string platformId)
        {
            float span = length * 2.45f / count;
            float scale = span / 3.78f;
            for (int i = 0; i < count; i++)
            {
                float u = -0.5f + length * (i + 0.5f) / count;
                Vector2 anchor = P(u, v);
                Transform module = Group(parent, platformId + " · Door " + (i + 1));
                module.localPosition = anchor;
                Color glass = new Color(1, 1, 1, foreground ? 0.55f : 1f);
                int order = Order(anchor);
                var screenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Screen_door.prefab");
                if (screenPrefab == null) throw new InvalidOperationException("Screen_door.prefab is missing.");
                var screen = (GameObject)PrefabUtility.InstantiatePrefab(screenPrefab, module);
                screen.name = "Screen_door";
                var frame = screen.GetComponentsInChildren<SpriteRenderer>(true)
                    .First(r => r.sprite != null && r.sprite.name.StartsWith("screen_door_fixed"));
                Vector3 frameOffset = screen.transform.InverseTransformPoint(frame.transform.position);
                screen.transform.localScale = Vector3.one * scale;
                screen.transform.localPosition = new Vector3(0, 1.51f * scale, 0) - frameOffset * scale;
                foreach (var renderer in screen.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    renderer.color = glass;
                    renderer.sortingOrder = order + (renderer == frame ? 0 : 1);
                    renderer.sharedMaterial = spriteMaterial;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(screen.transform);
                Rect(module, "Line identity", new Vector2(0, 3.02f * scale), new Vector2(span * 1.08f, 0.09f), line, 26.565f, order + 2);
                Text(module, (i / 4 + 1) + "–" + (i % 4 + 1), new Vector2(0, 2.73f * scale), 0.22f, Ink, 26.565f, order + 3);
                float inside = foreground ? v + 0.5f : v - 0.5f;
                Transform point = Point(parent, platformId + "_Boarding_" + (i + 1), P(u, inside));
                boarding.Add(point);
                // Short queue brackets leave the central alighting lane clear.
                foreach (float side in new[] { -0.42f, 0.42f })
                    Rect(parent, "Queue bracket", P(u + side, inside), new Vector2(0.7f, 0.055f), Ink, -26.565f, -4700);
            }
            TactileStrip(parent, length, foreground ? v + 0.24f : v - 0.24f);
        }

        private static void FacilityArt(Transform parent, Vector2 stairs, Vector2 lift, string label, Color line)
        {
            Prop(parent, "escalator", "Transfer escalator", P(stairs.x, stairs.y), 0.66f, 1.40f, Color.white, new Vector2(3.0f, 1.4f));
            Prop(parent, "StationElevator", "Accessible lift", P(lift.x, lift.y), 0.49f, 2.15f, Color.white, new Vector2(2.0f, 1.25f));
            Sign(parent, label + "  ↑", "TRANSFER  /  ELEVATOR", P(stairs.x + 0.45f, stairs.y) + Vector2.up * 4.7f, 5.9f, line, 2450);
            Text(parent, "ELEVATOR", P(lift.x, lift.y) + Vector2.up * 4.7f, 0.22f, Color.white, 26.565f, 2450);
        }

        private static void TransferHall(Transform parent)
        {
            Transform floor = Group(parent, "01 · Transfer concourse");
            Floor(floor, 9, 5, Hex("C7CBC9"));
            Boundary(parent, "Concourse perimeter", new[] { P(-0.5f, -0.5f), P(8.5f, -0.5f), P(8.5f, 4.5f), P(-0.5f, 4.5f), P(-0.5f, -0.5f) });
            Prop(parent, "escalator", "Stairs down to B4", P(2.2f, 1.7f), 0.65f, 1.4f, Color.white, new Vector2(2.8f, 1.3f));
            Prop(parent, "StationElevator", "Accessible lift B4", P(1.5f, 3.7f), 0.48f, 2.1f, Color.white, new Vector2(2.0f, 1.2f));
            Sign(parent, "수서  Suseo", "갈아타는 곳  |  TRANSFER", P(3.6f, 3.3f) + Vector2.up * 4f, 6.3f, Blue, 2400);
            Sign(parent, "↓ 수인분당선", "B4  SUIN-BUNDANG", P(1.0f, 1.0f) + Vector2.up * 4.5f, 4.7f, Yellow, 2400);
            string[] names = { "3호선", "GTX-A", "SRT · 나가는 곳" };
            string[] subtitles = { "일원 · 가락시장", "수도권광역급행철도", "고속철도 연결통로" };
            Color[] colors = { Orange, Violet, Hex("70445C") };
            for (int i = 0; i < 3; i++)
            {
                float v = 0.5f + i * 1.45f;
                Vector2 position = P(7.7f, v);
                Prop(parent, "pillar", "Portal pier " + names[i], position + new Vector2(-1.0f, 0.5f), 0.67f, 1.3f, colors[i] * 0.45f + Color.white * 0.55f, new Vector2(0.65f, 0.6f));
                Sign(parent, names[i] + "  ↑", subtitles[i], position + Vector2.up * 3.4f, 4f, colors[i], 2500 + i * 10);
                var route = new Surface();
                route.Tile(2.6f, v - 0.035f, 7.0f, v + 0.035f, colors[i]);
                route.Save(floor, names[i] + " floor route", -4900);
                Text(floor, ">     >     >", P(4.8f, v), 0.32f, Color.white, 26.565f, -4800);
                exits.Add(Point(parent, names[i] + " · Integration boundary", P(7.2f, v)));
            }
            Prop(parent, "bench_2", "Concourse waiting bench", P(4.5f, 4.0f), 1.3f, 0.8f, Color.white, new Vector2(2.6f, 0.8f));
            Prop(parent, "vending", "Concourse vending", P(0.1f, 0.0f), 0.57f, 1.2f, Color.white, new Vector2(1.2f, 0.8f));
        }

        private static void FloorPattern(Transform parent, int length, bool suseo)
        {
            var pattern = new Surface();
            if (suseo)
            {
                // Broad double chevrons seen on the Bundang platform, not GTX platform markings.
                foreach (float u in new[] { 3.6f, 7.4f })
                    foreach (float shift in new[] { 0f, 0.62f })
                    {
                        Vector2 a = new Vector2(u + shift - 0.7f, -0.38f);
                        Vector2 b = new Vector2(u + shift + 0.45f, 1.42f);
                        Vector2 c = new Vector2(u + shift - 0.7f, 3.12f);
                        pattern.GroundChevron(a, b, c, 0.20f, Hex("60656A"));
                    }
            }
            else
                for (int u = 1; u < length; u += 2)
                    foreach (float v in new[] { 0f, 2.5f })
                        pattern.Tile(u - 0.22f, v - 0.2f, u + 0.22f, v + 0.2f, Hex("838584"));
            pattern.Save(parent, "Station-specific stone inlays", -7900);
        }

        private static void TactileStrip(Transform parent, int length, float v)
        {
            var strip = new Surface();
            strip.Tile(-0.5f, v - 0.14f, length - 0.5f, v + 0.14f, Hex("D9A820"));
            for (int i = 0; i < length * 4; i++)
            {
                float u = -0.5f + i * 0.25f;
                strip.Tile(u + 0.006f, v - 0.135f, u + 0.244f, v + 0.135f, Hex("E9BE3D"));
                for (int x = 0; x < 3; x++)
                    for (int y = 0; y < 3; y++)
                    {
                        float a = u + 0.047f + x * 0.078f, b = v - 0.085f + y * 0.085f;
                        strip.Tile(a - 0.018f, b - 0.018f, a + 0.018f, b + 0.018f, Hex("B08B22"));
                        strip.Tile(a - 0.014f, b - 0.007f, a + 0.010f, b + 0.017f, Hex("FFE185"));
                    }
            }
            strip.Save(parent, "Continuous flush tactile strip", -5100);
        }

        private static void CeramicWall(Transform parent, int length)
        {
            // Foreground wall is cut down in the centre so the player remains visible.
            Transform wall = Group(parent, "Suseo white-blue ceramic wall · central cutaway");
            for (int i = 0; i < length * 2; i++)
            {
                float u = -0.5f + i * 0.5f;
                float h = i < 3 || i >= length * 2 - 3 ? 3.4f : 1.1f;
                var panel = new Surface();
                Vector2 a = P(u, -0.5f), b = P(u + 0.5f, -0.5f);
                panel.Quad(a, b, b + Vector2.up * h, a + Vector2.up * h, Hex("777E82"));
                for (float z = 0.06f; z < h - 0.08f; z += 0.24f)
                {
                    Color color = z < 0.22f ? Hex("293F59") : z < 0.50f ? Hex("3E86A4") : z < 0.74f ? Hex("C1C5BD") : z < 1.0f ? Hex("72ADBE") : Hex("D6D8CE");
                    for (int c = 0; c < 3; c++)
                    {
                        Vector2 l = Vector2.Lerp(a, b, c / 3f + 0.01f) + Vector2.up * z;
                        Vector2 r = Vector2.Lerp(a, b, (c + 1) / 3f - 0.01f) + Vector2.up * z;
                        float dh = Mathf.Min(0.222f, h - 0.04f - z);
                        panel.Quad(l, r, r + Vector2.up * dh, l + Vector2.up * dh, color);
                    }
                }
                panel.Save(wall, "Ceramic wall panel " + i, Order((a + b) * 0.5f));
            }
        }

        private static void CeramicColumn(Transform parent, Vector2 foot)
        {
            var column = Group(parent, "Suseo round ceramic column"); column.localPosition = foot;
            var shape = new Surface();
            const float radius = 0.67f, height = 3.4f;
            // Faceted ellipse silhouette and vertical grout preserve the existing crisp sprite style.
            for (int i = 0; i < 16; i++)
            {
                float t0 = Mathf.PI + i * Mathf.PI / 16, t1 = Mathf.PI + (i + 1) * Mathf.PI / 16;
                Vector2 a = new Vector2(Mathf.Cos(t0) * radius, Mathf.Sin(t0) * radius * 0.5f);
                Vector2 b = new Vector2(Mathf.Cos(t1) * radius, Mathf.Sin(t1) * radius * 0.5f);
                shape.Quad(a, b, b + Vector2.up * height, a + Vector2.up * height, Hex("434D56"));
                for (float z = 0.05f; z < height - 0.04f; z += 0.25f)
                {
                    Color color = z < 0.55f ? Hex("286C99") : z < 1.05f ? Hex("66A9C0") : Hex("D9DAD2");
                    float shade = 0.79f + 0.21f * Mathf.Sin((i + 1) * Mathf.PI / 20);
                    color *= new Color(shade, shade, shade, 1);
                    shape.Quad(Vector2.Lerp(a,b,.07f) + Vector2.up*z, Vector2.Lerp(a,b,.93f)+Vector2.up*z,
                        Vector2.Lerp(a,b,.93f)+Vector2.up*(z+.232f), Vector2.Lerp(a,b,.07f)+Vector2.up*(z+.232f), color);
                }
            }
            shape.Ellipse(Vector2.up * height, new Vector2(radius, radius * .5f), Hex("B9C2BF"));
            shape.Save(column, "Round ceramic shaft", Order(foot));
            var collider = column.gameObject.AddComponent<PolygonCollider2D>();
            collider.points = new[] { new Vector2(-radius, 0), new Vector2(0, -radius * .5f), new Vector2(radius, 0), new Vector2(0, radius * .5f) };
        }

        // Authoring-only native geometry. Assets are persisted so scenes render without this editor script.
        private sealed class Surface
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();
            private readonly List<Color> colors = new List<Color>();
            public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
            {
                int n = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                // SpriteRenderer converts display colours for a linear project; mesh vertex colours need the same conversion.
                Color vertexColor = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
                for (int i = 0; i < 4; i++) colors.Add(vertexColor);
                triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
            }
            public void Tile(float u0, float v0, float u1, float v1, Color color) => Quad(P(u0,v0),P(u1,v0),P(u1,v1),P(u0,v1),color);
            public void GroundChevron(Vector2 a, Vector2 b, Vector2 c, float width, Color color)
            {
                Vector2 d0=(b-a).normalized, d1=(c-b).normalized;
                Vector2 n0=new Vector2(-d0.y,d0.x), n1=new Vector2(-d1.y,d1.x);
                Vector2 m=(n0+n1).normalized;
                Vector2 join=m*(width*.5f/Vector2.Dot(m,n0));
                Vector2 s0=n0*width*.5f,s1=n1*width*.5f;
                Vector2 q0=a-s0,q1=b-join,q2=b+join,q3=a+s0;
                Quad(P(q0.x,q0.y),P(q1.x,q1.y),P(q2.x,q2.y),P(q3.x,q3.y),color);
                q0=b-join;q1=c-s1;q2=c+s1;q3=b+join;
                Quad(P(q0.x,q0.y),P(q1.x,q1.y),P(q2.x,q2.y),P(q3.x,q3.y),color);
            }
            public void Ellipse(Vector2 center, Vector2 radius, Color color)
            {
                for(int i=0;i<32;i++)
                {
                    float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;
                    Quad(center,center+new Vector2(Mathf.Cos(a)*radius.x,Mathf.Sin(a)*radius.y),
                        center+new Vector2(Mathf.Cos(b)*radius.x,Mathf.Sin(b)*radius.y),center,color);
                }
            }
            public void Save(Transform parent, string name, int order)
            {
                Mesh mesh = new Mesh { name = name };
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.SetColors(colors);
                mesh.SetUVs(0, vertices.Select(_ => new Vector2(.5f,.5f)).ToList());
                mesh.RecalculateBounds();
                string path = Art + "/Geometry/" + meshPrefix + "_" + (meshIndex++).ToString("D3") + ".asset";
                mesh.name = Path.GetFileNameWithoutExtension(path);
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing != null) { EditorUtility.CopySerialized(mesh,existing); UnityEngine.Object.DestroyImmediate(mesh); mesh=existing; EditorUtility.SetDirty(mesh); }
                else AssetDatabase.CreateAsset(mesh,path);
                Transform t=Group(parent,name);
                t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=t.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=geometryMaterial; renderer.sortingOrder=order;
            }
        }

        private static StationData CreateData(bool suseo, Vector2 stairs, Vector2 lift)
        {
            string id = suseo ? "Suseo" : "Bokjeong";
            string path = Root + "/Data/Stations/StationData_" + id + ".asset";
            StationData data = AssetDatabase.LoadAssetAtPath<StationData>(path);
            if (data == null) { data = ScriptableObject.CreateInstance<StationData>(); AssetDatabase.CreateAsset(data, path); }
            var so = new SerializedObject(data);
            so.FindProperty("stationId").stringValue = id;
            so.FindProperty("stationName").stringValue = suseo ? "수서" : "복정";
            so.FindProperty("isTransferStation").boolValue = true;
            string[] lines = suseo ? new[] { "수인분당선", "3호선", "GTX-A", "SRT" } : new[] { "수인분당선", "8호선" };
            var lineArray = so.FindProperty("connectedLines"); lineArray.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++) lineArray.GetArrayElementAtIndex(i).stringValue = lines[i];
            var platforms = so.FindProperty("platforms"); platforms.arraySize = suseo ? 2 : 4;
            for (int i = 0; i < platforms.arraySize; i++)
            {
                var p = platforms.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("platformId").stringValue = (i < 2 ? "SB" : "L8") + (i % 2 == 0 ? "_NORTH" : "_SOUTH");
                p.FindPropertyRelative("lineName").stringValue = i < 2 ? "수인분당선" : "8호선";
                p.FindPropertyRelative("boundFor").stringValue = i < 2 ? (i % 2 == 0 ? "왕십리 · 청량리" : "모란 · 죽전 · 인천") : (i % 2 == 0 ? "잠실 · 별내" : "남위례 · 모란");
                p.FindPropertyRelative("doorOpeningSide").enumValueIndex = (int)(suseo || i >= 2 ? DoorOpeningSide.Left : DoorOpeningSide.Right);
                var facilities = p.FindPropertyRelative("facilities"); facilities.arraySize = 3;
                string[] labels = { "TransferEscalator", "AccessibleElevator", "TransferPassage" };
                StationFacilityType[] types = { StationFacilityType.Escalator, StationFacilityType.Elevator, StationFacilityType.TransferPassage };
                Vector2[] positions = { P(stairs.x, stairs.y), P(lift.x, lift.y), P(stairs.x - 1.2f, stairs.y) };
                for (int f = 0; f < 3; f++)
                {
                    var item = facilities.GetArrayElementAtIndex(f);
                    item.FindPropertyRelative("facilityId").stringValue = labels[f];
                    item.FindPropertyRelative("facilityType").enumValueIndex = (int)types[f];
                    item.FindPropertyRelative("localPosition").vector2Value = positions[f];
                }
                // Crowd balance is owned by the AI/content team; no invented operational counts.
                p.FindPropertyRelative("timeProfiles").arraySize = 0;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void Link(Transform parent, string name, Vector2 position, StationSceneLayout layout, int target, Transform arrival)
        {
            Transform anchor = Point(parent, name, position);
            var trigger = anchor.gameObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true; trigger.radius = 1.25f;
            anchor.gameObject.AddComponent<StationLevelLink>().Configure(layout, target, arrival);
        }

        private static void Prop(Transform parent, string art, string name, Vector2 foot, float scale, float height, Color tint, Vector2 footprint)
        {
            if (art == "StationElevator")
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Stations/AccessibleStationElevator.prefab");
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
                instance.name = name; instance.transform.localPosition = foot;
                SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>();
                renderer.transform.localPosition = Vector2.up * height;
                renderer.transform.localScale = Vector3.one * scale;
                renderer.sortingOrder = Order(foot);
                return;
            }
            Transform group = Group(parent, name); group.localPosition = foot;
            Sprite(group, art, name + " Sprite", Vector2.up * height, scale, tint, Order(foot));
            var collider = group.gameObject.AddComponent<PolygonCollider2D>();
            collider.points = new[] { new Vector2(-footprint.x / 2, 0), new Vector2(0, -footprint.y / 2), new Vector2(footprint.x / 2, 0), new Vector2(0, footprint.y / 2) };
        }

        private static void Sign(Transform parent, string title, string subtitle, Vector2 position, float width, Color accent, int order)
        {
            Transform group = Group(parent, "Sign · " + title); group.localPosition = position;
            bool multiline = subtitle.Contains("\n");
            float h = multiline ? 1.72f : 1.38f;
            Rect(group, "Dark frame", Vector2.zero, new Vector2(width + 0.12f, h), Ink, 26.565f, order);
            Rect(group, "Ivory face", new Vector2(0, 0.04f), new Vector2(width, h - 0.13f), Hex("F4F0DF"), 26.565f, order + 1);
            float stripY = -h * 0.41f;
            Rect(group, "Route color", new Vector2(-stripY * 0.5f, stripY), new Vector2(width, 0.14f), accent, 26.565f, order + 2);
            float titleHeight = Mathf.Min(0.55f, (width - 0.45f) / Mathf.Max(1f, title.Length * 0.65f));
            Text(group, title, new Vector2(-0.14f, 0.29f), titleHeight, Ink, 26.565f, order + 3);
            Text(group, subtitle, new Vector2(0.14f, -0.29f), 0.25f, Ink, 26.565f, order + 3);
        }

        private static SpriteRenderer Sprite(Transform parent, string asset, string name, Vector2 position, float scale, Color color, int order)
        {
            Transform t = Group(parent, name); t.localPosition = position; t.localScale = Vector3.one * scale;
            var renderer = t.gameObject.AddComponent<SpriteRenderer>(); renderer.sprite = SpriteAt(asset);
            renderer.color = color; renderer.sortingOrder = order; renderer.sharedMaterial = spriteMaterial;
            return renderer;
        }

        private static void Rect(Transform parent, string name, Vector2 p, Vector2 size, Color color, float angle, int order)
        {
            Transform t = Group(parent, name); t.localPosition = p; t.localRotation = Quaternion.Euler(0, 0, angle);
            t.localScale = new Vector3(size.x / solid.bounds.size.x, size.y / solid.bounds.size.y, 1);
            var r = t.gameObject.AddComponent<SpriteRenderer>(); r.sprite = solid; r.color = color; r.sortingOrder = order; r.sharedMaterial = spriteMaterial;
        }

        private static void Text(Transform parent, string value, Vector2 p, float height, Color color, float angle, int order)
        {
            Transform t = Group(parent, value); t.localPosition = new Vector3(p.x, p.y, -0.2f); t.localRotation = Quaternion.Euler(0, 0, angle);
            var text = t.gameObject.AddComponent<TextMesh>(); text.font = font; text.fontSize = 64; text.characterSize = height / 6.4f;
            text.text = value; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = color.linear;
            text.fontStyle = FontStyle.Bold;
            var renderer = t.GetComponent<MeshRenderer>(); renderer.sharedMaterial = font.material; renderer.sortingOrder = order;
        }

        private static void Boundary(Transform parent, string name, Vector2[] points)
        {
            var edge = Group(parent, name).gameObject.AddComponent<EdgeCollider2D>(); edge.points = points; edge.edgeRadius = 0.06f;
        }
        private static Transform Group(Transform parent, string name) { var g = new GameObject(name); g.transform.SetParent(parent, false); return g.transform; }
        private static Transform Point(Transform parent, string name, Vector2 position) { Transform t = Group(parent, name); t.localPosition = position; return t; }
        private static Vector2 P(float u, float v) => new Vector2((u - v) * 2.45f, (u + v) * 1.225f);
        private static int Order(Vector2 foot) => 1800 - Mathf.RoundToInt(foot.y * 30);
        private static Sprite SpriteAt(string name) => AssetDatabase.LoadAllAssetsAtPath(Sprites + "/" + name + ".png").OfType<Sprite>().First();
        private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out Color color); return color; }
    }
}
#endif

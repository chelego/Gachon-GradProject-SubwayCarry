#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SubwayCarry.AI
{
    public static class AiPrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Prototype_AI.unity";
        private const string MaterialFolder = "Assets/_Project/Art/PrototypeMaterials";

        [MenuItem("SubwayCarry/Prototype/Build AI Pathfinding Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolder("Assets/_Project/Art", "PrototypeMaterials");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("AI Prototype");

            CreateCamera(root.transform);
            CreateLight(root.transform);
            CreateBlock("Floor", Vector2.zero, new Vector2(18f, 8f),
                new Color(0.08f, 0.12f, 0.18f), root.transform, false, 1f);

            Color wallColor = new Color(0.22f, 0.3f, 0.4f);
            CreateBlock("Wall Top", new Vector2(0f, 4.25f), new Vector2(18.5f, 0.5f), wallColor, root.transform, true);
            CreateBlock("Wall Bottom", new Vector2(0f, -4.25f), new Vector2(18.5f, 0.5f), wallColor, root.transform, true);
            CreateBlock("Wall Left", new Vector2(-9.25f, 0f), new Vector2(0.5f, 8.5f), wallColor, root.transform, true);
            CreateBlock("Wall Right", new Vector2(9.25f, 0f), new Vector2(0.5f, 8.5f), wallColor, root.transform, true);

            Color seatColor = new Color(0.1f, 0.45f, 0.62f);
            CreateBlock("Seats Top Left", new Vector2(-4.5f, 2.8f), new Vector2(4.5f, 1.2f), seatColor, root.transform, true);
            CreateBlock("Seats Top Right", new Vector2(4.5f, 2.8f), new Vector2(4.5f, 1.2f), seatColor, root.transform, true);
            CreateBlock("Seats Bottom Left", new Vector2(-4.5f, -2.8f), new Vector2(4.5f, 1.2f), seatColor, root.transform, true);
            CreateBlock("Seats Bottom Right", new Vector2(4.5f, -2.8f), new Vector2(4.5f, 1.2f), seatColor, root.transform, true);
            CreateBlock("Center Obstacle", Vector2.zero, new Vector2(2f, 2f),
                new Color(0.55f, 0.25f, 0.25f), root.transform, true);

            GameObject navigationObject = new GameObject("Grid Navigation");
            navigationObject.transform.SetParent(root.transform);
            GridNavigation2D navigation = navigationObject.AddComponent<GridNavigation2D>();

            GameObject start = CreateMarker("Start", new Vector2(-7f, -1.5f),
                new Color(0.25f, 0.85f, 0.4f), root.transform);
            GameObject goalA = CreateMarker("Goal A", new Vector2(7f, 1.5f),
                new Color(0.95f, 0.35f, 0.3f), root.transform);
            GameObject goalB = CreateMarker("Goal B", new Vector2(-7f, 1.5f),
                new Color(0.95f, 0.75f, 0.2f), root.transform);

            GameObject agent = CreateBlock("Passenger Agent", start.transform.position,
                new Vector2(0.65f, 0.85f), new Color(1f, 0.85f, 0.15f), root.transform, false, -0.2f);
            var body = agent.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            agent.AddComponent<CapsuleCollider2D>();

            var line = agent.AddComponent<LineRenderer>();
            line.sharedMaterial = GetOrCreateMaterial("Path", new Color(1f, 0.85f, 0.2f));
            line.widthMultiplier = 0.08f;
            line.useWorldSpace = true;
            line.sortingOrder = 10;

            var passenger = agent.AddComponent<PassengerPrototypeAgent>();
            passenger.Configure(navigation, new[]
            {
                goalA.transform,
                goalB.transform,
                start.transform
            }, line);

            CreateLabel(root.transform);
            EditorSceneManager.SaveScene(scene, ScenePath);
            ValidatePathfinder();
            Selection.activeGameObject = agent;

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log("[AI Prototype] Scene created: " + ScenePath);
        }

        [MenuItem("SubwayCarry/Prototype/Validate A* Pathfinding")]
        public static void ValidatePathfinder()
        {
            var blocked = new HashSet<Vector2Int>
            {
                new Vector2Int(3, 1),
                new Vector2Int(3, 2),
                new Vector2Int(3, 3)
            };

            List<Vector2Int> path = AStarGrid.FindPath(
                new Vector2Int(1, 2),
                new Vector2Int(6, 2),
                8,
                5,
                cell => !blocked.Contains(cell));

            if (path.Count == 0)
            {
                throw new InvalidOperationException("A* validation failed: no path returned.");
            }

            foreach (Vector2Int cell in path)
            {
                if (blocked.Contains(cell))
                {
                    throw new InvalidOperationException("A* validation failed: path crosses an obstacle.");
                }
            }

            Debug.Log("[AI Prototype] A* validation passed. Path nodes: " + path.Count);
        }

        private static GameObject CreateBlock(
            string name,
            Vector2 position,
            Vector2 size,
            Color color,
            Transform parent,
            bool obstacle,
            float z = 0f)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Quad);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.position = new Vector3(position.x, position.y, z);
            block.transform.localScale = new Vector3(size.x, size.y, 1f);

            MeshCollider meshCollider = block.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(meshCollider);
            }

            block.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial(name, color);

            if (obstacle)
            {
                block.AddComponent<BoxCollider2D>();
                block.AddComponent<NavigationObstacle>();
            }

            return block;
        }

        private static GameObject CreateMarker(string name, Vector2 position, Color color, Transform parent)
        {
            return CreateBlock(name, position, new Vector2(0.5f, 0.5f), color, parent, false, -0.1f);
        }

        private static void CreateCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
        }

        private static void CreateLight(Transform parent)
        {
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(parent);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
        }

        private static void CreateLabel(Transform parent)
        {
            GameObject labelObject = new GameObject("Prototype Label");
            labelObject.transform.SetParent(parent);
            labelObject.transform.position = new Vector3(0f, 4.7f, -0.2f);

            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = "AI PROTOTYPE - A* PATHFINDING";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = 0.08f;
            label.color = Color.white;
        }

        private static Material GetOrCreateMaterial(string sourceName, Color color)
        {
            string safeName = sourceName.Replace(" ", string.Empty).Replace("/", string.Empty);
            string path = MaterialFolder + "/" + safeName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string fullPath = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
#endif

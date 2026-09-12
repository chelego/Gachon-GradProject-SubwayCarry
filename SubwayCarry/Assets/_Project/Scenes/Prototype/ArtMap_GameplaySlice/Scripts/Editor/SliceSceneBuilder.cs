using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;
using SubwayCarry.AI.V2;
using SubwayCarry.Gameplay;

namespace SubwayCarry.Prototype.ArtMapSlice.Editor
{
    public static class SliceSceneBuilder
    {
        public const string Root = "Assets/_Project/Scenes/Prototype/ArtMap_GameplaySlice";
        public const string ScenePath = Root + "/ArtMap_01_Gachon_Train_Wangsimni_Playable.unity";
        const string StationSource = "Assets/_Project/Scenes/";
        const string PlayerSource = "Assets/_Project/Prefabs/Gameplay/";

        [MenuItem("SubwayCarry/Art Map Slice/Create Connected Maps (new scene only)")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode first; this builder never stops your play session.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) throw new InvalidOperationException("Scene already exists. Preserve manual edits; do not overwrite.");
            var previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var root = new GameObject("ART_MAP_SLICE__Connected_Stations_And_Train");
                var controller = root.AddComponent<SliceGameController>();
                var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 7;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.035f, 0.045f, 0.06f);
                camera.nearClipPlane = 0.1f; camera.farClipPlane = 100; camera.transform.position = new Vector3(0, 0, -30);
                cameraObject.AddComponent<AudioListener>(); cameraObject.AddComponent<UniversalAdditionalCameraData>();
                var light = new GameObject("Global 2D Light").AddComponent<Light2D>(); light.lightType = Light2D.LightType.Global; light.intensity = 1;
                controller.gameplayCamera = camera;
                controller.maps = new[] {
                    ImportMap(scene, "GachonUniv.unity", "GACHON UNIVERSITY", 1f),
                    ImportMap(scene, "Subway.unity", "TRAIN INTERIOR", 0.2f),
                    ImportMap(scene, "Wangsimni.unity", "WANGSIMNI", 1f)
                };
                for (int i = 0; i < controller.maps.Length; i++) BuildNavigationAndAnchors(controller.maps[i], i);
                ConnectMaps(controller.maps);
                controller.baselineProfile = AssetDatabase.LoadAssetAtPath<PassengerAiV2PersonalityProfile>("Assets/_Project/Scenes/Ai_v2/Profiles/AI_V2_Profile_BasePassenger.asset");
                if (controller.baselineProfile == null) throw new InvalidOperationException("Base passenger profile is missing.");
                Shader outlineShader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Scripts/PlayerOutline.shader");
                if (outlineShader == null) throw new InvalidOperationException("Outline shader is missing.");
                var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Player_CyanOutline.mat");
                if (material == null) { material = new Material(outlineShader); AssetDatabase.CreateAsset(material, Root + "/Player_CyanOutline.mat"); }
                controller.playerOutline = material;
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerSource + "Player.prefab");
                var player = UnityEngine.Object.Instantiate(source);
                try
                {
                    player.name = "Player_Slice";
                    var input = new SerializedObject(player.GetComponent<PlayerController>()); input.FindProperty("moveSpeed").floatValue = 2.2f; input.ApplyModifiedPropertiesWithoutUndo();
                    player.AddComponent<SlicePlayerBoundary>().moveSpeed = 2.2f;
                    var motion = player.AddComponent<DebugTrainMotionProvider>();
                    var balance = new SerializedObject(player.GetComponent<PlayerBalance>()); balance.FindProperty("trainMotionProviderSource").objectReferenceValue = motion; balance.ApplyModifiedPropertiesWithoutUndo();
                    var animator = new SerializedObject(player.GetComponent<PlayerSpriteAnimator>());
                    controller.idleSprites = ReadSprites(animator.FindProperty("idleSprites")); controller.walkSprites = ReadSprites(animator.FindProperty("walkSprites"));
                    controller.playerPrefab = PrefabUtility.SaveAsPrefabAsset(player, Root + "/Player_Slice.prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(player); }
                controller.packagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerSource + "CakePackage.prefab");
                SliceBehaviorSetup.Configure(controller);
                SlicePresentationRepair.Apply(controller);
                foreach (var map in controller.maps) map.gameObject.SetActive(false);
                // Authoring view shows the starting art; runtime activates only the selected map.
                controller.maps[0].gameObject.SetActive(true);
                Vector2 entry = controller.maps[0].entry; camera.transform.position = new Vector3(entry.x, entry.y + 1.2f, -30);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Scene save failed.");
                AssetDatabase.SaveAssets();
                Debug.Log("Art map slice created: " + ScenePath + ". No Play Mode or visual validation was performed.");
            }
            finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        }

        static SliceMap ImportMap(Scene target, string file, string label, float scale)
        {
            var root = new GameObject("Map_" + label.Replace(' ', '_')); SceneManager.MoveGameObjectToScene(root, target);
            var map = root.AddComponent<SliceMap>(); map.displayName = label;
            Scene source = EditorSceneManager.OpenPreviewScene(StationSource + file);
            try
            {
                foreach (GameObject sourceRoot in source.GetRootGameObjects())
                {
                    if (sourceRoot.GetComponent<Camera>() != null || sourceRoot.GetComponent<Light2D>() != null) continue;
                    var clone = UnityEngine.Object.Instantiate(sourceRoot); clone.name = sourceRoot.name;
                    SceneManager.MoveGameObjectToScene(clone, target); clone.transform.SetParent(root.transform, false);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(source); }
            root.transform.localScale = Vector3.one * scale;
            map.floorTiles = root.GetComponentInChildren<Tilemap>(true);
            Bounds bounds;
            if (map.floorTiles != null)
            {
                var local = map.floorTiles.localBounds;
                Vector3 min = map.floorTiles.transform.TransformPoint(local.min), max = map.floorTiles.transform.TransformPoint(local.max);
                bounds = new Bounds((min + max) * 0.5f, max - min);
                map.floorTiles.GetComponent<TilemapRenderer>().sortingOrder = -30000;
                var diamonds = new List<SliceFloorDiamond>();
                var grid = map.floorTiles.GetComponentInParent<Grid>();
                foreach (var cell in map.floorTiles.cellBounds.allPositionsWithin)
                {
                    var sprite = map.floorTiles.GetSprite(cell); if (sprite == null) continue;
                    Vector3 localCenter = grid.CellToLocalInterpolated((Vector3)cell + map.floorTiles.tileAnchor);
                    Vector2 center = map.floorTiles.transform.TransformPoint(localCenter + sprite.bounds.center);
                    Vector2 half = Vector2.Scale(sprite.bounds.extents, map.floorTiles.transform.lossyScale);
                    // These art tiles occupy about five grid cells, not one occupied Tilemap cell.
                    half += new Vector2(0.25f, 0.125f) * scale;
                    half.y = Mathf.Min(half.y, half.x * 0.5f);
                    diamonds.Add(new SliceFloorDiamond { center = center, halfSize = half });
                    bounds.Encapsulate((Vector3)(center - half)); bounds.Encapsulate((Vector3)(center + half));
                }
                map.floorDiamonds = diamonds.ToArray();
            }
            else
            {
                var floor = root.transform.Find("Floor").GetComponent<SpriteRenderer>(); bounds = floor.bounds; floor.sortingOrder = -30000;
            }
            map.floorBounds = Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
            map.cameraSize = map.floorTiles != null ? 7.5f : 5.7f;
            return map;
        }

        static void BuildNavigationAndAnchors(SliceMap map, int index)
        {
            var obstacleList = new List<Rect>(); var interests = new List<SliceInterest>();
            var doors = new List<SpriteRenderer>();
            var renderers = map.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var r in renderers)
            {
                string name = r.name.ToLowerInvariant(); if (name == "floor") continue;
                Bounds b = r.bounds;
                float feetY = b.min.y + b.size.y * 0.12f;
                r.sortingOrder = Mathf.Clamp(Mathf.RoundToInt(-feetY * 80), -25000, 25000);
                bool pillar = name.Contains("pillar") || name.Contains("vending");
                bool seat = name.Contains("bench") || name.StartsWith("subway_seat_");
                if (name.Contains("door_fixed")) doors.Add(r);
                if (pillar || seat)
                {
                    Vector2 size = new Vector2(Mathf.Clamp(b.size.x * (pillar ? 0.42f : 0.62f), 0.3f, 2.5f), Mathf.Clamp(b.size.y * 0.14f, 0.25f, 0.65f));
                    Vector2 center = new Vector2(b.center.x, feetY + size.y * 0.25f);
                    if (index == 1) { center.x = b.center.x; center.y = b.center.y - b.extents.y * 0.25f; size.x = Mathf.Min(size.x, 0.6f); size.y = Mathf.Min(size.y, 0.4f); }
                    var rect = new Rect(center - size * 0.5f, size); obstacleList.Add(rect);
                    var colliderObject = new GameObject("Footprint_" + r.name); colliderObject.transform.SetParent(map.transform, false); colliderObject.transform.position = center;
                    var box = colliderObject.AddComponent<BoxCollider2D>(); box.size = size / map.transform.lossyScale.x;
                    colliderObject.AddComponent<SliceStaticImpactSource>();
                    if (seat) interests.Add(new SliceInterest { position = center + (index == 1 ? new Vector2(Mathf.Sign(map.floorBounds.center.x - center.x) * 0.85f, 0) : new Vector2(0, -0.9f)), kind = PassengerAiV2InteriorSpotKind.Seat, comfort = 1 });
                    else interests.Add(new SliceInterest { position = center + new Vector2(0.7f, -0.3f), kind = PassengerAiV2InteriorSpotKind.Lean, comfort = 0.7f });
                }
            }
            map.obstacles = obstacleList.ToArray(); map.EnsureGrid();
            if (map.Grid.WalkableCellCount < 20) throw new InvalidOperationException("Map has no usable floor: " + map.displayName);
            map.entry = map.Grid.Nearest(map.floorBounds.center);
            Vector2 axis = index == 1 ? Vector2.up : new Vector2(1, 0.5f).normalized;
            map.exits = new[] { MakeExit(map, axis), MakeExit(map, -axis) };
            if (index == 0) map.entry = map.Grid.Nearest(new Vector2(-12.02f, -18.46f));
            if (index == 2) map.entry = map.Grid.Nearest(new Vector2(11.72f, -4.43f));
            var validInterests = new List<SliceInterest>();
            foreach (var item in interests)
            {
                var itemCopy = item; itemCopy.position = map.Grid.Nearest(item.position);
                bool duplicate = false;
                foreach (var other in validInterests) if ((itemCopy.position - other.position).sqrMagnitude < 0.8f) { duplicate = true; break; }
                if (!duplicate) validInterests.Add(itemCopy);
            }
            for (int i = 0; i < 12; i++)
            {
                var samples = map.Grid.SamplePoints; Vector2 p = map.Grid.Nearest(samples[(i * 131 + 19) % samples.Count]);
                bool tooClose = false;
                foreach (var existing in validInterests) if ((p - existing.position).sqrMagnitude < 0.8f) { tooClose = true; break; }
                if (tooClose) continue;
                validInterests.Add(new SliceInterest { position = p, kind = PassengerAiV2InteriorSpotKind.Stand, comfort = 0.25f });
            }
            map.interests = validInterests.ToArray();
            var portals = new List<SlicePortal>();
            foreach (var door in doors)
            {
                Vector2 target;
                if (index == 1) target = new Vector2(door.bounds.center.x < map.floorBounds.center.x ? map.floorBounds.xMin + 0.6f : map.floorBounds.xMax - 0.6f, door.bounds.center.y);
                else target = new Vector2(door.bounds.center.x, door.bounds.min.y - 0.45f);
                Vector2 position = map.Grid.Nearest(target);
                portals.Add(new SlicePortal { label = index == 1 ? (target.x < map.floorBounds.center.x ? "Gachon University" : "Wangsimni") : "Train interior", position = position, targetMap = index == 1 ? (target.x < map.floorBounds.center.x ? 0 : 2) : 1 });
            }
            if (portals.Count == 0) throw new InvalidOperationException("No source door geometry found in " + map.displayName);
            map.portals = portals.ToArray();
        }

        static SliceExit MakeExit(SliceMap map, Vector2 direction)
        {
            Vector2 p = map.floorBounds.center + direction * (map.floorBounds.width + map.floorBounds.height);
            for (int i = 0; i < 1000; i++) { p -= direction * 0.2f; if (map.Grid.Fits(p, 0.3f)) break; }
            Vector2 inside = map.Grid.Nearest(p), outside = inside;
            for (int i = 0; i < 100; i++) { outside += direction * 0.2f; if (!map.ContainsFloor(outside)) { outside += direction * 1.5f; break; } }
            return new SliceExit { inside = inside, outside = outside };
        }

        static void ConnectMaps(SliceMap[] maps)
        {
            for (int m = 0; m < maps.Length; m++) for (int i = 0; i < maps[m].portals.Length; i++)
            {
                var p = maps[m].portals[i]; SliceMap destination = maps[p.targetMap];
                Vector2 arrival = destination.entry;
                for (int j = 0; j < destination.portals.Length; j++) if (destination.portals[j].targetMap == m) { arrival = destination.Grid.Nearest(Vector2.Lerp(destination.portals[j].position, destination.floorBounds.center, 0.12f)); break; }
                p.arrival = arrival; maps[m].portals[i] = p;
                var marker = new GameObject("Portal_" + i + "_To_" + p.label); marker.transform.SetParent(maps[m].transform, false); marker.transform.position = p.position;
                var line = marker.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.loop = true; line.positionCount = 24;
                line.startWidth = line.endWidth = 0.05f / maps[m].transform.lossyScale.x;
                line.startColor = line.endColor = new Color(0.25f, 0.9f, 1, 0.75f); line.sortingOrder = -29000;
                var material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat"); line.sharedMaterial = material;
                for (int n = 0; n < 24; n++) { float a = n * Mathf.PI * 2 / 24; line.SetPosition(n, new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.25f, 0) / maps[m].transform.lossyScale.x); }
            }
        }
        static Sprite[] ReadSprites(SerializedProperty p) { var result = new Sprite[p.arraySize]; for (int i = 0; i < result.Length; i++) result[i] = (Sprite)p.GetArrayElementAtIndex(i).objectReferenceValue; return result; }
    }
}

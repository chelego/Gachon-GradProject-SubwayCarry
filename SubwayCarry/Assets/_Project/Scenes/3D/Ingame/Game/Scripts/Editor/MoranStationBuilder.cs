using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SubwayCarry.Gameplay3D.Editor
{
    internal static partial class MoranStationBuilder
    {
        const string Game = Metro3DBuilder.Root;
        const string MaterialFolder = Game + "/Art/Materials";
        const string FinishFolder = MaterialFolder + "/Moran";
        internal const string PrefabPath = Game + "/Prefabs/StationLayout_OffsetStairs.prefab";
        internal const string ScenePath = "Assets/_Project/Scenes/3D/MoranStation_Preview.unity";
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        [MenuItem("SubwayCarry/3D/Open Moran Station Preview")]
        static void OpenOrCreate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab)
            {
                var model = BuildModel();
                prefab = PrefabUtility.SaveAsPrefabAsset(model, PrefabPath);
                UnityEngine.Object.DestroyImmediate(model);
                if (!prefab) throw new InvalidOperationException("Could not save " + PrefabPath);
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            SetEnvironment();
            CreateVisitor(instance.transform);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Could not save " + ScenePath);
            AssetDatabase.SaveAssets();
            FrameEntrance();
        }

        // Only called when the prefab is first created. Later artist edits live in the prefab.
        internal static GameObject BuildModel()
        {
            if (!AssetDatabase.IsValidFolder(FinishFolder))
                AssetDatabase.CreateFolder(MaterialFolder, "Moran");
            var root = new GameObject("StationLayout_OffsetStairs");
            try
            {
                BuildArchitecture(root.transform);
                BuildLifts(root.transform);
                BuildInteriors(root.transform);
                BuildRoomReflections(root.transform);
                BindPassengerLayout(root.transform);
                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        static Transform Group(Transform parent, string name, Vector3 local = default)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = local;
            return group;
        }

        static GameObject Box(Transform parent, string name, Vector3 local, Vector3 size, Material mat, bool solid = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = local;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!solid) UnityEngine.Object.DestroyImmediate(box.GetComponent<BoxCollider>());
            return box;
        }

        static Material Mat(string name)
        {
            if (materials.TryGetValue(name, out var material) && material) return material;
            string folder = name.StartsWith("Metro_", StringComparison.Ordinal) ? "/Metro/" :
                name.StartsWith("Circulation_", StringComparison.Ordinal) ? "/Circulation/" : "/";
            material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + folder + name + ".mat");
            if (!material) throw new InvalidOperationException("Missing station material: " + name);
            return materials[name] = material;
        }

        static Material Finish(string name)
        {
            if (materials.TryGetValue(name, out var material) && material) return material;
            var path = FinishFolder + "/" + name + ".mat";
            material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                string hex;
                switch (name)
                {
                    case "Teal": hex = "#195957"; break;
                    case "Seafoam": hex = "#91C3AC"; break;
                    case "Brass": hex = "#C8A264"; break;
                    case "Parchment": hex = "#E9E1CE"; break;
                    case "Ink": hex = "#24383A"; break;
                    case "Terra": hex = "#B26B55"; break;
                    default: throw new ArgumentException("Unknown Moran finish " + name);
                }
                ColorUtility.TryParseHtmlString(hex, out var color);
                material = new Material(Mat("Metro_CeramicWall").shader) { name = name, enableInstancing = true };
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Metallic", name == "Brass" ? .58f : .06f);
                material.SetFloat("_Smoothness", name == "Parchment" ? .28f : .42f);
                AssetDatabase.CreateAsset(material, path);
            }
            return materials[name] = material;
        }

        static void SetEnvironment()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.34f, .36f, .38f);
            RenderSettings.ambientEquatorColor = new Color(.23f, .24f, .25f);
            RenderSettings.ambientGroundColor = new Color(.17f, .18f, .18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.075f, .078f, .08f);
            RenderSettings.fogStartDistance = 50;
            RenderSettings.fogEndDistance = 160;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(Game + "/Art/Textures/Metro/IndoorReflection.hdr");
            RenderSettings.reflectionIntensity = 1f;
            var volume = new GameObject("Indoor presentation").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Game + "/Data/IndoorPost.asset");
        }

        static void BuildRoomReflections(Transform station)
        {
            var group = Group(station, "Station reflections");
            void Room(string zone, Vector3 eye, Vector3 min, Vector3 max)
            {
                var probe = Group(group, zone, eye).gameObject.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Custom;
                probe.renderDynamicObjects = true;
                probe.resolution = 128;
                probe.hdr = true;
                probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
                probe.backgroundColor = new Color(.025f, .027f, .027f);
                probe.nearClipPlane = .06f;
                probe.farClipPlane = 150;
                probe.boxProjection = true;
                probe.center = (min + max) * .5f - eye;
                probe.size = max - min;
                probe.blendDistance = .8f;
                probe.intensity = .85f;
                probe.importance = 1;
                probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(
                    Game + "/Art/Textures/Moran/" + zone + ".exr");
            }
            Room("Concourse", new Vector3(0, 5.35f, 31.4f),
                new Vector3(WestWall, HallFloor, 10.8f), new Vector3(EastWall, 7.70f, 72));
            Room("WestPlatform", new Vector3(-6.7f, 1.65f, 53.8f),
                new Vector3(WestWall, 0, PlatformFirst), new Vector3(-2.35f, 3.32f, PlatformLast));
            Room("EastPlatform", new Vector3(6.7f, 1.65f, 63.4f),
                new Vector3(2.35f, 0, PlatformFirst), new Vector3(EastWall, 3.32f, PlatformLast));
        }

        static void ConfigureCamera(Camera camera)
        {
            camera.nearClipPlane = .035f;
            camera.farClipPlane = 260f;
            camera.fieldOfView = 72f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .027f, .027f);
            var data = camera.GetUniversalAdditionalCameraData();
            data.SetRenderer(0);
            data.renderPostProcessing = true;
        }

        static void BindPassengerLayout(Transform root)
        {
            var layout = root.gameObject.AddComponent<Metro3DStationLayout>();
            layout.entrance = Group(root, "Public entrance arrival", new Vector3(-1.10f, HallFloor, 13.6f));
            layout.exit = Group(root, "Public exit beyond turn", new Vector3(2.72f, HallFloor, 12.85f));
            layout.unpaidHall = Group(root, "Ticket hall arrival", new Vector3(0, HallFloor, 21.5f));
            layout.entryWaitingBounds = Rect.MinMaxRect(WestWall, 10.8f, EastWall, 30);
            layout.entryWaitingLimit = 8;
            layout.preferDirectEntranceWalk = true;
            layout.useLocalFareGateMovement = true;
            layout.hallBounds = Rect.MinMaxRect(WestWall + .3f, 10.95f, EastWall - .3f, 71.7f);
            layout.platformBounds = Rect.MinMaxRect(WestWall + .3f, PlatformFirst + .3f, EastWall - .3f, PlatformLast - .3f);
            layout.tunnelStart = PlatformFirst - 10;
            layout.tunnelEnd = PlatformLast + 10;
            var architecture = root.Find("Architecture");
            var west = architecture.Find("West stair");
            var east = architecture.Find("East stair");
            layout.westTop = west.Find("B1 Landing");
            layout.westBottom = west.Find("B2 Landing");
            layout.eastTop = east.Find("B1 Landing");
            layout.eastBottom = east.Find("B2 Landing");
            var ends = new List<GameObject>();
            foreach (Transform part in architecture.GetComponentsInChildren<Transform>())
            {
                if (part.name == "Distant tunnel shadow") ends.Add(part.gameObject);
                if (part.name != "Platform screen doors") continue;
                var bank = part.gameObject.AddComponent<Metro3DPlatformDoors>();
                bank.authoredAtServiceWidth = true;
                for (int i = 0; i < Metro3DTrain.DoorZ.Length; i++)
                {
                    var pair = part.Find("Door " + Metro3DTrain.DoorNumber(i));
                    bank.trainSide = pair.localPosition.x < 0 ? 1 : -1;
                    foreach (Transform child in pair)
                        if (child.name == "PlatformDoor") bank.panels.Add(child);
                    bank.blockers.Add(pair.Find("Closed screen door").GetComponent<BoxCollider>());
                    pair.Find("DoorStatusLight").gameObject.AddComponent<Metro3DDoorStatus>();
                }
            }
            layout.previewTunnelEnds = ends.ToArray();
        }

        static void CreateVisitor(Transform station)
        {
            var visitor = new GameObject("Walkthrough - Fare hall entrance");
            visitor.transform.position = new Vector3(0, 3.65f, 23.5f);
            var capsule = visitor.AddComponent<CharacterController>();
            capsule.height = 1.74f;
            capsule.radius = .255f;
            capsule.center = Vector3.up * .87f;
            capsule.stepOffset = .28f;
            capsule.skinWidth = .025f;
            capsule.minMoveDistance = 0;
            var eye = Group(visitor.transform, "Main Camera", Vector3.up * 1.61f);
            eye.tag = "MainCamera";
            var camera = eye.gameObject.AddComponent<Camera>();
            ConfigureCamera(camera);
            eye.gameObject.AddComponent<AudioListener>();
            var scope = visitor.AddComponent<Metro3DRenderScope>();
            scope.pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(Game + "/Data/Pipeline3D.asset");
            var guide = visitor.AddComponent<MoranStationWalkthrough>();
            guide.view = camera;
            guide.gates = station.GetComponentsInChildren<Metro3DFareDoor>();
            guide.guideFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/Fonts/NanumGothic/NanumGothic.ttf");
        }

        internal static void FrameEntrance()
        {
            if (SceneView.lastActiveSceneView is SceneView view)
            {
                view.in2DMode = false;
                view.orthographic = false;
                view.LookAtDirect(new Vector3(0, 5.05f, 29.5f), Quaternion.identity, 4.2f);
                view.Repaint();
            }
        }
    }
}

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using SubwayCarry.AI.V2;
using SubwayCarry.Gameplay3D;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SubwayCarry.EditorTools.AI.V2
{
    public static class PassengerAiV23DSceneBuilder
    {
        internal const string Root = "Assets/_Project/Scenes/3D/Test/AI/Assets";
        internal const string SceneRoot = "Assets/_Project/Scenes/3D/Test/AI";
        internal const string Model = "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/AnimationLibrary_Unity_Standard.fbx";
        public const string ScenePath = SceneRoot + "/AI_V2_01_SocialCorridor_TwoWayAndFlow3D_Test.unity";

        [MenuItem("SubwayCarry/Passenger AI V2/3D/Build 01 - Corridor + Animated Mannequin")]
        public static void BuildFirstField()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("Existing 3D field preserved; not regenerated: " + ScenePath);
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Data");
            EnsureFolder(Root + "/Prefabs");

            ModelImporter importer = AssetImporter.GetAtPath(Model) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("CC0 mannequin model missing.");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.motionNodeName = "root";
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            var selectedClips = new List<ModelImporterClipAnimation>();
            foreach (var clip in importer.defaultClipAnimations)
            {
                if (!Ends(clip.name, "Idle_Loop") && !Ends(clip.name, "Walk_Loop") && !Ends(clip.name, "Jog_Fwd_Loop")) continue;
                clip.loopTime = true;
                clip.loopPose = true;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                selectedClips.Add(clip);
            }
            if (selectedClips.Count != 3) throw new InvalidOperationException("Expected Idle / Walk / Jog clips, got " + selectedClips.Count);
            importer.clipAnimations = selectedClips.ToArray();
            importer.SaveAndReimport();

            AnimationClip idle = FindClip("Idle_Loop"), walk = FindClip("Walk_Loop"), jog = FindClip("Jog_Fwd_Loop");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "/Data/TestMannequin.controller");
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(Root + "/Data/TestMannequin.controller");
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                AnimatorState state = controller.layers[0].stateMachine.AddState("Locomotion");
                var tree = new BlendTree { name = "Idle_Walk_Jog", blendParameter = "Speed", useAutomaticThresholds = false };
                AssetDatabase.AddObjectToAsset(tree, controller);
                tree.AddChild(idle, 0f);
                tree.AddChild(walk, 1.35f);
                tree.AddChild(jog, 2.8f);
                state.motion = tree;
                controller.layers[0].stateMachine.defaultState = state;
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssetIfDirty(controller);
            }

            Material shell = NewMaterial("Mannequin_TanShell", new Color(.72f, .49f, .31f), .32f);
            Material coolShell = NewMaterial("Mannequin_TealShell", new Color(.20f, .52f, .57f), .32f);
            Material joints = NewMaterial("Mannequin_DarkJoints", new Color(.045f, .055f, .065f), .28f);
            Material floor = NewMaterial("TestFloor", new Color(.24f, .28f, .31f), .36f);
            Material wall = NewMaterial("BoundaryWall", new Color(.60f, .64f, .66f), .3f);
            Material line = NewMaterial("CentreGuide", new Color(.68f, .73f, .75f), .2f);
            Material cyan = NewMaterial("LeftSpawn", new Color(.12f, .39f, .44f), .28f);
            Material orange = NewMaterial("RightSpawn", new Color(.58f, .31f, .15f), .28f);
            var previous = SceneManager.GetActiveScene();
            bool emptyBatchScene = Application.isBatchMode && string.IsNullOrEmpty(previous.path) && previous.rootCount == 0;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                emptyBatchScene ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                GameObject prefab = BuildMannequin(controller, idle, shell, joints);
                GameObject test = new GameObject("TEST_01_SocialCorridor_3D_TwoWayAndFlow");
                var manager = new GameObject("CrowdManager_SpatialHash").AddComponent<PassengerAiV2CrowdManager>();
                manager.transform.SetParent(test.transform, false);
                var agents = new PassengerAiV2Agent[16];
                float[] lanes = { -2.35f, -.8f, .8f, 2.35f };
                for (int i = 0; i < agents.Length; i++)
                {
                    bool left = i < 8;
                    int index = i % 8;
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, test.transform);
                    instance.name = "Passenger_" + (left ? "LeftToRight_" : "RightToLeft_") + index.ToString("00");
                    instance.transform.position = new Vector3((left ? 1 : -1) * (-16.65f + (index / 4) * 2.1f), 0f, lanes[index % 4]);
                    if (left) ReplaceShell(instance, coolShell, joints);
                    instance.transform.Find("WorldBody3D/Heading").rotation = Quaternion.LookRotation(left ? Vector3.right : Vector3.left);
                    agents[i] = instance.GetComponent<PassengerAiV2Agent>();
                }
                var scenario = test.AddComponent<PassengerAiV2Corridor3DScenario>();
                scenario.Configure(AssetDatabase.LoadAssetAtPath<PassengerAiV2PersonalityProfile>(PassengerAiV2SceneBuilder.BaselineProfilePath), manager, agents);

                var geometry = new GameObject("Blender_TestField_36m_x_7m");
                geometry.transform.SetParent(test.transform, false);
                Mesh floorMesh = FindMesh("FloorPanel"), wallMesh = FindMesh("WallPanel"), lineMesh = FindMesh("GuideMark");
                for (int x = -17; x <= 17; x += 2)
                {
                    for (int z = -3; z <= 3; z += 2)
                        Piece("Floor_" + x + "_" + z, floorMesh, x < -15 ? cyan : x > 15 ? orange : floor,
                            new Vector3(x, 0f, z), Vector3.one, geometry.transform, true);
                    foreach (float z in new[] { -3.7f, 3.7f })
                        Piece("BoundaryWall", wallMesh, wall, new Vector3(x, 0f, z), new Vector3(1, .44f, 1), geometry.transform, true);
                }
                for (int x = -16; x <= 16; x += 2)
                    Piece("CentreGuide", lineMesh, line, new Vector3(x, .002f, 0f), Vector3.one, geometry.transform, false);

                Camera camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.09f, .12f, .15f);
                camera.fieldOfView = 54f;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 150f;
                Quaternion view = Quaternion.Euler(43, -25, 0);
                camera.transform.SetPositionAndRotation(new Vector3(0, .7f, 0) - view * Vector3.forward * 30, view);
                camera.gameObject.AddComponent<AudioListener>();
                camera.gameObject.AddComponent<PassengerAiV23DTestCamera>();
                var scope = new GameObject("SceneOnly_3D_RenderScope").AddComponent<Metro3DRenderScope>();
                scope.pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/_Project/Scenes/3D/Ingame/Game/Data/Pipeline3D.asset");
                if (scope.pipeline == null) throw new InvalidOperationException("Existing scene-local 3D pipeline missing.");
                AddLight("Key Light", Quaternion.Euler(48, -35, 0), 1.1f, new Color(1f, .95f, .88f), true);
                AddLight("Fill Light", Quaternion.Euler(35, 135, 0), .35f, new Color(.77f, .87f, 1f), false);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.3f, .33f, .36f);
                RenderSettings.fog = false;
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Scene save failed.");
                Debug.Log("AI3D_FIELD_READY: " + ScenePath + " | 16 imported mannequins | 3D capsules | shared V2 decisions | no original scenes replaced");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (scene.isLoaded && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static GameObject BuildMannequin(RuntimeAnimatorController controller, AnimationClip idle, Material shell, Material joints)
        {
            GameObject template = new GameObject("TestMannequin3D");
            try
            {
                var agent = template.AddComponent<PassengerAiV2Agent>();
                template.GetComponent<Rigidbody2D>().simulated = false;
                template.GetComponent<SpriteRenderer>().enabled = false;
                template.GetComponent<CircleCollider2D>().enabled = false;
                GameObject physical = new GameObject("WorldBody3D");
                physical.transform.SetParent(template.transform, false);
                physical.layer = 2; // Built-in Ignore Raycast: world sweeps must not include other NPC bodies.
                var rb = physical.AddComponent<Rigidbody>();
                rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
                var capsule = physical.AddComponent<CapsuleCollider>();
                capsule.height = 1.78f; capsule.radius = .28f; capsule.center = Vector3.up * .89f;
                physical.AddComponent<PassengerAiV23DMotor>();
                GameObject heading = new GameObject("Heading");
                heading.transform.SetParent(physical.transform, false);
                GameObject axis = new GameObject("AuthoredAxisCorrection");
                axis.transform.SetParent(heading.transform, false);
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model), axis.transform);
                model.name = "Quaternius_TestMannequin";
                model.transform.localScale = Vector3.one * (1.78f / 1.82918f);
                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                ReplaceShell(model, shell, joints);
                idle.SampleAnimation(model, 0f);
                Vector3 footForward = BoneForward(model.transform, "L") + BoneForward(model.transform, "R");
                footForward.y = 0f;
                if (footForward.sqrMagnitude < .01f) throw new InvalidOperationException("Cannot resolve mannequin forward axis.");
                axis.transform.localRotation = Quaternion.FromToRotation(footForward.normalized, Vector3.forward);
                template.AddComponent<PassengerAiV23DVisual>().Configure(agent, heading.transform, animator);
                var result = PrefabUtility.SaveAsPrefabAsset(template, Root + "/Prefabs/TestMannequin3D.prefab");
                if (result == null) throw new InvalidOperationException("Mannequin prefab save failed.");
                return result;
            }
            finally { UnityEngine.Object.DestroyImmediate(template); }
        }

        internal static void ReplaceShell(GameObject root, Material shell, Material joints)
        {
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = materials[i] != null && materials[i].name.Contains("Joint") ? joints : shell;
                renderer.sharedMaterials = materials;
                renderer.updateWhenOffscreen = true;
            }
        }
        internal static void AddLight(string name, Quaternion rotation, float intensity, Color color, bool shadows)
        {
            Light light = new GameObject(name).AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = intensity; light.color = color;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.transform.rotation = rotation;
        }
        internal static GameObject Piece(string name, Mesh mesh, Material material, Vector3 position, Vector3 scale, Transform parent, bool collision)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collision) obj.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.BatchingStatic);
            return obj;
        }
        internal static Material NewMaterial(string name, Color color, float smoothness)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing; // Resume a partial build without changing existing values.
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
        internal static AnimationClip FindClip(string suffix)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(Model))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__") && Ends(clip.name, suffix)) return clip;
            throw new InvalidOperationException("Clip missing: " + suffix);
        }
        private static bool Ends(string name, string suffix) { return name == suffix || name.EndsWith("|" + suffix); }
        private static Vector3 BoneForward(Transform model, string side)
        {
            Transform foot = null, toe = null;
            foreach (Transform bone in model.GetComponentsInChildren<Transform>())
            {
                if (bone.name == "DEF-foot." + side) foot = bone;
                if (bone.name == "DEF-toe." + side) toe = bone;
            }
            if (foot == null || toe == null) throw new InvalidOperationException("Mannequin foot bones missing.");
            return toe.position - foot.position;
        }
        internal static Mesh FindMesh(string name)
        {
            foreach (string file in new[] {"TestFieldKit.fbx", "AiFacilities.fbx"})
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(Root + "/Models/" + file))
                    if (asset is Mesh mesh && mesh.name == name) return mesh;
            throw new InvalidOperationException("Blender mesh missing: " + name);
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif

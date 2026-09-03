#if UNITY_EDITOR
using System.Collections.Generic;
using SubwayCarry.AI.V2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SubwayCarry.EditorTools.AI.V2
{
    public static class PassengerAiV2SceneBuilder
    {
        public const string SocialCorridorScenePath =
            "Assets/_Project/Scenes/Ai_v2/AI_V2_01_SocialCorridor_TwoWayAndFlow_Test.unity";
        public const string StairMergeScenePath =
            "Assets/_Project/Scenes/Ai_v2/AI_V2_02_StairMerge_NarrowEntrance_Test.unity";
        public const string FareGateScenePath =
            "Assets/_Project/Scenes/Ai_v2/AI_V2_03_FareGate_QueueAndDirection_Test.unity";
        public const string TrainDoorScenePath =
            "Assets/_Project/Scenes/Ai_v2/AI_V2_04_TrainDoor_AlightBeforeBoard_Test.unity";
        public const string TrainInteriorScenePath =
            "Assets/_Project/Scenes/Ai_v2/AI_V2_05_TrainInterior_SeatStandLean_Test.unity";
        public const string FullJourneyScenePath =
            "Assets/_Project/Scenes/Ai_v2/AI_V2_06_FullJourney_StationToStation_Test.unity";
        private const string PrototypeAiScenePath =
            "Assets/_Project/Scenes/Prototype_AI.unity";
        public const string BaselineProfilePath =
            "Assets/_Project/Scenes/Ai_v2/Profiles/AI_V2_Profile_BasePassenger.asset";

        [MenuItem("SubwayCarry/Passenger AI V2/Build 01 - Social Corridor (Two Way + Flow)")]
        public static void BuildSocialCorridorScene()
        {
            PassengerAiV2PersonalityProfile baselineProfile = EnsureBaselineProfile();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 11.5f;
            camera.backgroundColor = new Color(0.035f, 0.055f, 0.075f, 1f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject scenarioObject = new GameObject("TEST_01_SocialCorridor_TwoWayAndFlow");
            PassengerAiV2SocialCorridorScenario scenario =
                scenarioObject.AddComponent<PassengerAiV2SocialCorridorScenario>();
            scenario.ConfigureBaselineProfile(baselineProfile);

            GameObject purposeObject = new GameObject(
                "PURPOSE__TwoWayPassing__SameDirectionFlow__DeadlockCheck");
            purposeObject.transform.SetParent(scenarioObject.transform, false);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, SocialCorridorScenePath))
            {
                Debug.LogError($"[PassengerAI V2] Scene save failed: {SocialCorridorScenePath}");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PassengerAI V2] Built: {SocialCorridorScenePath}");
        }

        [MenuItem("SubwayCarry/Passenger AI V2/Build 02 - Stair Merge (Narrow Entrance)")]
        public static bool BuildStairMergeScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(StairMergeScenePath) != null)
            {
                Debug.LogError($"[PassengerAI V2] 02 scene already exists and was not overwritten: {StairMergeScenePath}");
                return false;
            }

            PassengerAiV2PersonalityProfile baselineProfile = EnsureBaselineProfile();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 11.5f;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.055f, 1f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject scenarioObject = new GameObject("TEST_02_StairMerge_NarrowEntrance");
            PassengerAiV2StairMergeScenario scenario =
                scenarioObject.AddComponent<PassengerAiV2StairMergeScenario>();
            scenario.ConfigureBaselineProfile(baselineProfile);

            GameObject purposeObject = new GameObject(
                "PURPOSE__SmartObjectReservation__DirectionalYield__NoWaitingInsideStair");
            purposeObject.transform.SetParent(scenarioObject.transform, false);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, StairMergeScenePath))
            {
                Debug.LogError($"[PassengerAI V2] Scene save failed: {StairMergeScenePath}");
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PassengerAI V2] Built: {StairMergeScenePath}");
            return true;
        }

        [MenuItem("SubwayCarry/Passenger AI V2/Build 03 - Fare Gate (Queue + Direction)")]
        public static bool BuildFareGateScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FareGateScenePath) != null)
            {
                Debug.LogError($"[PassengerAI V2] 03 scene already exists and was not overwritten: {FareGateScenePath}");
                return false;
            }

            PassengerAiV2PersonalityProfile baselineProfile = EnsureBaselineProfile();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 11.5f;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.055f, 1f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject scenarioObject = new GameObject("TEST_03_FareGate_QueueAndDirection");
            PassengerAiV2FareGateScenario scenario =
                scenarioObject.AddComponent<PassengerAiV2FareGateScenario>();
            scenario.ConfigureBaselineProfile(baselineProfile);

            GameObject purposeObject = new GameObject(
                "PURPOSE__Queue__CardTagBeforeOpen__DirectionLockedSameGatePassage");
            purposeObject.transform.SetParent(scenarioObject.transform, false);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, FareGateScenePath))
            {
                Debug.LogError($"[PassengerAI V2] Scene save failed: {FareGateScenePath}");
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PassengerAI V2] Built: {FareGateScenePath}");
            return true;
        }

        [MenuItem("SubwayCarry/Passenger AI V2/Build 04 - Train Door (Alight Before Board)")]
        public static bool BuildTrainDoorScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TrainDoorScenePath) != null)
            {
                Debug.LogError($"[PassengerAI V2] 04 scene already exists and was not overwritten: {TrainDoorScenePath}");
                return false;
            }

            PassengerAiV2PersonalityProfile baselineProfile = EnsureBaselineProfile();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 11.5f;
            camera.backgroundColor = new Color(0.018f, 0.03f, 0.045f, 1f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject scenarioObject = new GameObject("TEST_04_TrainDoor_AlightBeforeBoard");
            PassengerAiV2TrainDoorScenario scenario =
                scenarioObject.AddComponent<PassengerAiV2TrainDoorScenario>();
            scenario.ConfigureBaselineProfile(baselineProfile);

            GameObject purposeObject = new GameObject(
                "PURPOSE__AlightFirst__ClearDoorway__Board__ClosingWarning");
            purposeObject.transform.SetParent(scenarioObject.transform, false);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, TrainDoorScenePath))
            {
                Debug.LogError($"[PassengerAI V2] Scene save failed: {TrainDoorScenePath}");
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PassengerAI V2] Built: {TrainDoorScenePath}");
            return true;
        }

        [MenuItem("SubwayCarry/Passenger AI V2/Build 05 - Train Interior (Seat + Stand + Lean)")]
        public static bool BuildTrainInteriorScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            PassengerAiV2PersonalityProfile baselineProfile = EnsureBaselineProfile();
            Scene scene = EditorSceneManager.OpenScene(
                PrototypeAiScenePath,
                OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, TrainInteriorScenePath))
            {
                Debug.LogError($"[PassengerAI V2] Prototype_AI copy failed: {TrainInteriorScenePath}");
                return false;
            }

            GameObject prototypeRoot = FindRoot(scene, "AI Prototype");
            Transform firstCar = prototypeRoot != null
                ? prototypeRoot.transform.Find("Train Car 1")
                : null;
            if (prototypeRoot == null || firstCar == null)
            {
                Debug.LogError("[PassengerAI V2] Prototype_AI root or Train Car 1 was not found.");
                return false;
            }

            StripPrototypeRuntimeActors(prototypeRoot);

            Camera camera = prototypeRoot.GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                camera.orthographic = true;
                camera.orthographicSize = 7.5f;
                camera.transform.position = new Vector3(
                    firstCar.position.x,
                    firstCar.position.y,
                    -10f);
            }

            if (Object.FindFirstObjectByType<Light>() == null)
            {
                GameObject lightObject = new GameObject("Directional Light");
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1f;
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            GameObject scenarioObject = new GameObject("TEST_05_TrainInterior_SeatStandLean");
            PassengerAiV2TrainInteriorScenario scenario =
                scenarioObject.AddComponent<PassengerAiV2TrainInteriorScenario>();
            scenario.ConfigurePrototypeMap(
                baselineProfile,
                prototypeRoot.transform,
                firstCar,
                60);

            GameObject purposeObject = new GameObject(
                "PURPOSE__UtilityActivityChoice__UniqueReservation__PrepareToAlight");
            purposeObject.transform.SetParent(scenarioObject.transform, false);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, TrainInteriorScenePath))
            {
                Debug.LogError($"[PassengerAI V2] Scene save failed: {TrainInteriorScenePath}");
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PassengerAI V2] Built: {TrainInteriorScenePath}");
            return true;
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == rootName)
                {
                    return roots[i];
                }
            }

            return null;
        }

        private static void StripPrototypeRuntimeActors(GameObject prototypeRoot)
        {
            MonoBehaviour[] behaviours =
                prototypeRoot.GetComponentsInChildren<MonoBehaviour>(true);
            var actorObjects = new HashSet<GameObject>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                string typeName = behaviour.GetType().FullName;
                if (typeName == "SubwayCarry.AI.GeneralPassengerPrototype"
                    || typeName == "SubwayCarry.AI.TrainDoorCyclePrototype"
                    || typeName == "SubwayCarry.AI.PlayerBoardingCyclePrototype")
                {
                    actorObjects.Add(behaviour.gameObject);
                }
                else if (typeName == "SubwayCarry.AI.TrainCarCameraController")
                {
                    Object.DestroyImmediate(behaviour);
                }
            }

            foreach (GameObject actorObject in actorObjects)
            {
                if (actorObject != null)
                {
                    Object.DestroyImmediate(actorObject);
                }
            }
        }

        [MenuItem("SubwayCarry/Passenger AI V2/Build 06 - Full Journey (Outside To Destination Exit)")]
        public static bool BuildFullJourneyScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            PassengerAiV2PersonalityProfile baselineProfile = EnsureBaselineProfile();
            Scene scene = EditorSceneManager.OpenScene(
                TrainInteriorScenePath,
                OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, FullJourneyScenePath))
            {
                Debug.LogError($"[PassengerAI V2] 05 copy failed: {FullJourneyScenePath}");
                return false;
            }

            PassengerAiV2TrainInteriorScenario previousScenario =
                Object.FindFirstObjectByType<PassengerAiV2TrainInteriorScenario>();
            if (previousScenario != null)
            {
                Object.DestroyImmediate(previousScenario.gameObject);
            }

            GameObject prototypeRoot = FindRoot(scene, "AI Prototype");
            Transform firstCar = prototypeRoot != null
                ? prototypeRoot.transform.Find("Train Car 1")
                : null;
            if (prototypeRoot == null || firstCar == null)
            {
                Debug.LogError("[PassengerAI V2] 06 requires AI Prototype/Train Car 1 from 05.");
                return false;
            }

            Camera camera = prototypeRoot.GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                camera.transform.SetParent(null, true);
                camera.orthographic = true;
                camera.orthographicSize = 7.5f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
            }

            GameObject scenarioObject = new GameObject("TEST_06_FullJourney_StationToStation");
            PassengerAiV2FullJourneyScenario scenario =
                scenarioObject.AddComponent<PassengerAiV2FullJourneyScenario>();
            scenario.Configure(baselineProfile, prototypeRoot.transform, firstCar);
            GameObject purposeObject = new GameObject(
                "PURPOSE__OutsideToGateToPlatformToTrainToDestinationExit__OnePersistentPassenger");
            purposeObject.transform.SetParent(scenarioObject.transform, false);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, FullJourneyScenePath))
            {
                Debug.LogError($"[PassengerAI V2] Scene save failed: {FullJourneyScenePath}");
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PassengerAI V2] Built: {FullJourneyScenePath}");
            return true;
        }

        [MenuItem("SubwayCarry/Passenger AI V2/Apply Base Profile To Open 01 Scene")]
        public static bool ApplyBaselineProfileToOpenScene()
        {
            PassengerAiV2SocialCorridorScenario scenario =
                Object.FindFirstObjectByType<PassengerAiV2SocialCorridorScenario>();
            if (scenario == null)
            {
                Debug.LogError("[PassengerAI V2] Open the 01 Social Corridor scene first.");
                return false;
            }

            PassengerAiV2PersonalityProfile baselineProfile = EnsureBaselineProfile();
            Undo.RecordObject(scenario, "Assign Passenger AI V2 Base Profile");
            scenario.ConfigureBaselineProfile(baselineProfile);
            EditorUtility.SetDirty(scenario);
            Scene openScene = scenario.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(openScene);
            bool saved = EditorSceneManager.SaveScene(openScene);
            AssetDatabase.SaveAssets();
            if (saved)
            {
                Debug.Log($"[PassengerAI V2] Base profile linked without rebuilding scene: {openScene.path}");
            }

            return saved;
        }

        public static PassengerAiV2PersonalityProfile EnsureBaselineProfile()
        {
            const string profileFolder = "Assets/_Project/Scenes/Ai_v2/Profiles";
            if (!AssetDatabase.IsValidFolder(profileFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Scenes/Ai_v2", "Profiles");
            }

            PassengerAiV2PersonalityProfile profile =
                AssetDatabase.LoadAssetAtPath<PassengerAiV2PersonalityProfile>(BaselineProfilePath);
            if (profile != null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<PassengerAiV2PersonalityProfile>();
            profile.name = "AI_V2_Profile_BasePassenger";
            profile.ConfigurePrototypeDefaults(
                "base_passenger",
                "Base Passenger",
                new Color(0.78f, 0.86f, 1f, 1f),
                0.62f,
                0.52f,
                0.5f,
                0.25f,
                0.55f,
                0.65f,
                0.55f,
                0.5f,
                1.35f,
                0.22f,
                0.65f,
                0.85f,
                0.5f,
                0.45f);
            AssetDatabase.CreateAsset(profile, BaselineProfilePath);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
#endif

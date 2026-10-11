#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SubwayCarry.Transit.Editor
{
    /// <summary>
    /// Editor utility that merges the separate GachonUniv.unity and
    /// Wangsimni.unity station scenes into a single unified scene at
    /// <c>Assets/_Project/Scenes/Empty.unity</c>.
    ///
    /// Each station's content is placed under a parent GameObject with a
    /// <see cref="StationSection"/> component. A shared
    /// <see cref="StationGameplayController"/> manages which station is
    /// visible at runtime.
    ///
    /// Run from the menu: <b>SubwayCarry / Transit / Build Station Gameplay Scene</b>.
    ///
    /// This follows the same Editor-driven build pattern as
    /// <c>AiPrototypeSceneBuilder</c> and <c>GameplayFlowPrototypeBuilder</c>.
    /// </summary>
    public static class StationGameplaySceneBuilder
    {
        private const string OutputScenePath =
            "Assets/_Project/Scenes/Empty.unity";

        private const string GachonUnivScenePath =
            "Assets/_Project/Scenes/GachonUniv.unity";

        private const string WangsimniScenePath =
            "Assets/_Project/Scenes/Wangsimni.unity";

        /// <summary>
        /// Horizontal offset (world units) between station sections.
        /// Chosen large enough so stations never overlap visually.
        /// GachonUniv stays at the original position; Wangsimni is offset
        /// by this value on the X axis.
        /// </summary>
        private const float StationOffsetX = 80f;

        [MenuItem("SubwayCarry/Transit/Build Station Gameplay Scene")]
        public static void Build()
        {
            Build(true);
        }

        public static void Build(bool prompt)
        {
            if (prompt && !EditorUtility.DisplayDialog(
                "Build Station Gameplay Scene",
                "This will overwrite the current Empty.unity with a combined " +
                "scene containing both GachonUniv and Wangsimni stations.\n\n" +
                "The original station scene files will NOT be modified.\n\n" +
                "Continue?",
                "Build",
                "Cancel"))
            {
                return;
            }

            // ----------------------------------------------------------
            // 1. Create a fresh empty scene as the build target.
            // ----------------------------------------------------------
            Scene targetScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            // ----------------------------------------------------------
            // 2. Create the root hierarchy and shared infrastructure.
            // ----------------------------------------------------------
            GameObject sceneRoot = new GameObject("Station Gameplay Root");

            // Shared camera
            Camera mainCamera = CreateMainCamera(sceneRoot.transform);

            // Controller
            GameObject controllerObject = new GameObject("Station Gameplay Controller");
            controllerObject.transform.SetParent(sceneRoot.transform);
            StationGameplayController controller =
                controllerObject.AddComponent<StationGameplayController>();

            // ----------------------------------------------------------
            // 3. Merge station scenes into section containers.
            // ----------------------------------------------------------
            StationSection gachonSection = MergeStationScene(
                GachonUnivScenePath,
                "Station_GachonUniv",
                "가천대역 (GachonUniv)",
                Vector3.zero,
                targetScene,
                sceneRoot.transform);

            StationSection wangsimniSection = MergeStationScene(
                WangsimniScenePath,
                "Station_Wangsimni",
                "왕십리역 (Wangsimni)",
                new Vector3(StationOffsetX, 0f, 0f),
                targetScene,
                sceneRoot.transform);

            // ----------------------------------------------------------
            // 4. Wire up the controller.
            // ----------------------------------------------------------
            SerializedObject controllerSO = new SerializedObject(controller);
            SerializedProperty stationsProperty =
                controllerSO.FindProperty("stations");
            stationsProperty.arraySize = 2;
            stationsProperty.GetArrayElementAtIndex(0).objectReferenceValue =
                gachonSection;
            stationsProperty.GetArrayElementAtIndex(1).objectReferenceValue =
                wangsimniSection;

            SerializedProperty cameraProp =
                controllerSO.FindProperty("mainCamera");
            cameraProp.objectReferenceValue = mainCamera;
            controllerSO.ApplyModifiedPropertiesWithoutUndo();

            // ----------------------------------------------------------
            // 5. Save and validate.
            // ----------------------------------------------------------
            EditorSceneManager.SaveScene(targetScene, OutputScenePath);
            ValidateScene(targetScene);

            Selection.activeGameObject = controllerObject;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log(
                "[Station Gameplay] Scene built successfully: " +
                OutputScenePath +
                "\n  GachonUniv section at origin." +
                "\n  Wangsimni section offset by X=" + StationOffsetX +
                "\n  Use the controller to switch between stations.");
        }

        // ==============================================================
        //  Internal helpers
        // ==============================================================

        /// <summary>
        /// Opens a source station scene additively, moves all of its root
        /// objects into a container in the target scene, then closes the
        /// source scene.
        /// </summary>
        private static StationSection MergeStationScene(
            string scenePath,
            string containerName,
            string label,
            Vector3 offset,
            Scene targetScene,
            Transform parentInTarget)
        {
            if (!System.IO.File.Exists(
                System.IO.Path.Combine(
                    Application.dataPath,
                    "..",
                    scenePath)))
            {
                Debug.LogError(
                    "[Station Gameplay] Source scene not found: " + scenePath);
                return null;
            }

            // Open the source scene additively.
            Scene sourceScene = EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Additive);

            // Create the section container in the target scene.
            GameObject container = new GameObject(containerName);
            SceneManager.MoveGameObjectToScene(container, targetScene);
            container.transform.SetParent(parentInTarget);
            container.transform.position = offset;

            // Create spawn and camera target points.
            Transform spawnPoint = CreatePoint(
                "Spawn Point",
                Vector3.zero,
                container.transform);
            Transform cameraTarget = CreatePoint(
                "Camera Target",
                Vector3.zero,
                container.transform);

            // Gather all root objects from the source scene.
            List<GameObject> sourceRoots =
                new List<GameObject>(sourceScene.GetRootGameObjects());

            // Determine camera position from the source scene's camera
            // and apply it to our spawn/camera targets.
            Vector3 sourceCameraPosition = Vector3.zero;
            float sourceCameraOrtho = 15f;
            foreach (GameObject root in sourceRoots)
            {
                Camera cam = root.GetComponent<Camera>();
                if (cam != null)
                {
                    sourceCameraPosition = root.transform.position;
                    sourceCameraOrtho = cam.orthographicSize;
                    break;
                }
            }

            // Set spawn and camera target to the source camera position
            // (relative to the container offset).
            spawnPoint.localPosition = new Vector3(
                sourceCameraPosition.x,
                sourceCameraPosition.y,
                0f);
            cameraTarget.localPosition = new Vector3(
                sourceCameraPosition.x,
                sourceCameraPosition.y,
                0f);

            // Move source root objects into the container, skipping cameras
            // (we use the shared camera instead).
            foreach (GameObject root in sourceRoots)
            {
                // Skip camera objects from source scenes.
                Camera cam = root.GetComponent<Camera>();
                if (cam != null)
                {
                    continue;
                }

                Vector3 originalLocalPos = root.transform.localPosition;
                Quaternion originalLocalRot = root.transform.localRotation;
                Vector3 originalLocalScale = root.transform.localScale;

                // Move to target scene first (required before reparenting
                // to an object in a different scene).
                SceneManager.MoveGameObjectToScene(root, targetScene);
                root.transform.SetParent(container.transform, false);
                root.transform.localPosition = originalLocalPos;
                root.transform.localRotation = originalLocalRot;
                root.transform.localScale = originalLocalScale;
            }

            // Close the (now empty) source scene without saving.
            EditorSceneManager.CloseScene(sourceScene, true);

            // Add StationSection component.
            StationSection section = container.AddComponent<StationSection>();
            SerializedObject sectionSO = new SerializedObject(section);
            sectionSO.FindProperty("sectionLabel").stringValue = label;
            sectionSO.FindProperty("spawnPoint").objectReferenceValue =
                spawnPoint;
            sectionSO.FindProperty("cameraTarget").objectReferenceValue =
                cameraTarget;
            sectionSO.FindProperty("cameraOrthoSize").floatValue =
                sourceCameraOrtho;
            sectionSO.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log(
                $"[Station Gameplay] Merged '{scenePath}' as '{containerName}' " +
                $"({sourceRoots.Count} root objects, offset={offset})");

            return section;
        }

        private static Camera CreateMainCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 15f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;

            cameraObject.AddComponent<AudioListener>();

            // Add URP camera data if available.
            System.Type urpCameraType = System.Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, " +
                "Unity.RenderPipelines.Universal.Runtime");
            if (urpCameraType != null)
            {
                cameraObject.AddComponent(urpCameraType);
            }

            return camera;
        }

        private static Transform CreatePoint(
            string name,
            Vector3 localPosition,
            Transform parent)
        {
            GameObject point = new GameObject(name);
            point.transform.SetParent(parent);
            point.transform.localPosition = localPosition;
            return point.transform;
        }

        private static void ValidateScene(Scene scene)
        {
            int sectionCount = 0;
            int controllerCount = 0;
            int cameraCount = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                sectionCount +=
                    root.GetComponentsInChildren<StationSection>(true).Length;
                controllerCount +=
                    root.GetComponentsInChildren<StationGameplayController>(true)
                        .Length;
                cameraCount +=
                    root.GetComponentsInChildren<Camera>(true).Length;
            }

            if (sectionCount < 2)
            {
                Debug.LogWarning(
                    "[Station Gameplay] Expected at least 2 StationSection " +
                    "components but found " + sectionCount);
            }

            if (controllerCount != 1)
            {
                Debug.LogWarning(
                    "[Station Gameplay] Expected exactly 1 " +
                    "StationGameplayController but found " + controllerCount);
            }

            if (cameraCount != 1)
            {
                Debug.LogWarning(
                    "[Station Gameplay] Expected exactly 1 Camera but found " +
                    cameraCount);
            }

            Debug.Log(
                $"[Station Gameplay] Validation: {sectionCount} sections, " +
                $"{controllerCount} controller(s), {cameraCount} camera(s)");
        }
    }
}
#endif

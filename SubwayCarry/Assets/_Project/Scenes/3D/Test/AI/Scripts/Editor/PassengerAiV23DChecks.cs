#if UNITY_EDITOR
using System;
using SubwayCarry.AI.V2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SubwayCarry.EditorTools.AI.V2
{
    [InitializeOnLoad]
    public static class PassengerAiV23DChecks
    {
        private const string Pending = "SubwayCarry.AI3D.CorridorCheckPending";
        private const string FailureKey = "SubwayCarry.AI3D.CorridorCheckFailure";
        private static PassengerAiV2Agent[] agents;
        private static PassengerAiV2CrowdManager crowd;
        private static Vector2[] previous;
        private static float[] travelled;
        private static double started;
        private static int maximumDeadlocked;
        private static string failure;
        static PassengerAiV23DChecks()
        {
            if (SessionState.GetBool(Pending, false)) Attach();
        }
        private static void Attach()
        {
            EditorApplication.playModeStateChanged -= OnState;
            EditorApplication.playModeStateChanged += OnState;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
        public static void ValidateFirstField()
        {
            Require(Application.isBatchMode, "This verification entry point is batch-only; it must not replace an editing scene.");
            Scene scene = EditorSceneManager.OpenScene(PassengerAiV23DSceneBuilder.ScenePath, OpenSceneMode.Single);
            Mesh floorMesh = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Scenes/3D/Test/AI/Assets/Models/TestFieldKit.fbx"))
                if (asset is Mesh mesh && mesh.name == "FloorPanel") floorMesh = mesh;
            Require(floorMesh != null && floorMesh.bounds.size.y < .2f && floorMesh.bounds.size.z > 1.9f,
                "Blender floor axes were not imported as a horizontal XZ floor.");
            var found = UnityEngine.Object.FindObjectsByType<PassengerAiV2Agent>(FindObjectsSortMode.None);
            Require(found.Length == 16, "Expected sixteen imported mannequins.");
            foreach (var agent in found)
            {
                Require(agent.GetComponentInChildren<PassengerAiV23DMotor>() != null, "3D motor missing.");
                var animation = agent.GetComponentInChildren<Animator>();
                Require(animation != null && animation.runtimeAnimatorController != null, "Animation controller missing.");
                Require(animation.avatar != null && animation.avatar.isValid, "Generic animation avatar missing/invalid.");
                Require(!animation.applyRootMotion, "Code-owned 3D motion must not add animation root motion.");
                var renderer = agent.GetComponentInChildren<SkinnedMeshRenderer>();
                string meshPath = AssetDatabase.GetAssetPath(renderer.sharedMesh);
                Require(meshPath.Contains("UniversalAnimationLibrary"), "Old human model unexpectedly used.");
                Require(renderer.bones.Length >= 45, "Imported articulated humanoid rig incomplete.");
                Require(agent.GetComponentInChildren<CapsuleCollider>().height == 1.78f, "Unexpected human capsule height.");
            }
            CheckMotorContract();
            Debug.Log("AI3D_ASSET_CHECK_PASS: 16 mannequins; imported skeleton, generic animation, 3D capsule and opt-in 2D compatibility confirmed.");
        }
        private static void CheckMotorContract()
        {
            var root = new GameObject("Temporary_OptInMotorContractCheck");
            var managerObject = new GameObject("Temporary_TestCrowd");
            try
            {
                var manager = managerObject.AddComponent<PassengerAiV2CrowdManager>();
                var agent = root.AddComponent<PassengerAiV2Agent>();
                agent.Initialize(manager, new Vector2(2, 1), new Vector2(8, 1),
                    PassengerAiV2RuntimePersonality.CreateFallback(1), 0, -5, 5, null, Color.white);
                Require(agent.Position == new Vector2(2, 1), "Original 2D position contract changed.");
                agent.TeleportAndSetTarget(new Vector2(3, 2), new Vector2(8, 1));
                Require(agent.GetComponent<Rigidbody2D>().position == new Vector2(3, 2), "Original 2D teleport changed.");
                var motor = new ProbeMotor();
                agent.UsePlanarMotor(motor);
                Require(!agent.GetComponent<Rigidbody2D>().simulated, "2D physics remains active in 3D mode.");
                agent.TeleportAndSetTarget(new Vector2(7, 4), new Vector2(9, 4));
                Require(motor.Position == new Vector2(7, 4) && agent.Position == motor.Position,
                    "Navigation did not use the opt-in motor.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }
        private sealed class ProbeMotor : IPassengerAiV2PlanarMotor
        {
            public Vector2 Position { get; private set; }
            public void Teleport(Vector2 position) { Position = position; }
            public Vector2 Move(Vector2 position) { Position = position; return position; }
        }
        public static void RunCorridorPlayCheck()
        {
            ValidateFirstField();
            SessionState.EraseString(FailureKey);
            SessionState.SetBool(Pending, true);
            Attach();
            EditorApplication.isPlaying = true;
        }
        private static void OnState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                agents = UnityEngine.Object.FindObjectsByType<PassengerAiV2Agent>(FindObjectsSortMode.None);
                crowd = UnityEngine.Object.FindFirstObjectByType<PassengerAiV2CrowdManager>();
                previous = new Vector2[agents.Length]; travelled = new float[agents.Length];
                for (int i = 0; i < agents.Length; i++) previous[i] = agents[i].Position;
                started = EditorApplication.timeSinceStartup;
                maximumDeadlocked = 0; failure = null;
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending, false))
            {
                failure = SessionState.GetString(FailureKey, "");
                SessionState.EraseString(FailureKey);
                SessionState.EraseBool(Pending);
                EditorApplication.update -= Tick;
                EditorApplication.playModeStateChanged -= OnState;
                if (string.IsNullOrEmpty(failure)) Debug.Log("AI3D_PLAY_CHECK_PASS: 30s corridor simulation; 16 real 3D bodies; no 3-second deadlocks; each passenger travelled > 5m. Visual/Profiler approval not performed.");
                else Debug.LogError("AI3D_PLAY_CHECK_FAIL: " + failure);
                if (Application.isBatchMode) EditorApplication.Exit(string.IsNullOrEmpty(failure) ? 0 : 2);
            }
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || agents == null || crowd == null) return;
            maximumDeadlocked = Mathf.Max(maximumDeadlocked, crowd.DeadlockedCount);
            for (int i = 0; i < agents.Length; i++)
            {
                Vector2 now = agents[i].Position;
                float delta = Vector2.Distance(previous[i], now);
                if (delta < 2f) travelled[i] += delta; // Do not count the original loop teleport as travel.
                previous[i] = now;
                var motor = agents[i].GetComponentInChildren<PassengerAiV23DMotor>();
                if (agents[i].GetComponent<Rigidbody2D>().simulated || Mathf.Abs(motor.transform.position.y) > .03f)
                    failure = "Unexpected active 2D physics or 3D ground height.";
            }
            if (EditorApplication.timeSinceStartup - started < 30) return;
            if (agents.Length != 16 || maximumDeadlocked != 0) failure = "Agent count/deadlock count: " + agents.Length + "/" + maximumDeadlocked;
            for (int i = 0; i < travelled.Length; i++) if (travelled[i] < 5) failure = "Passenger " + i + " moved only " + travelled[i].ToString("0.00") + "m.";
            Debug.Log("AI3D_CORRIDOR_METRICS: deadlockMax=" + maximumDeadlocked + " firstDistance=" + travelled[0].ToString("0.00"));
            SessionState.SetString(FailureKey, failure ?? "");
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif

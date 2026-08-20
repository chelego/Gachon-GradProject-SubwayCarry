#if UNITY_EDITOR
using System;
using System.Reflection;
using SubwayCarry.Delivery;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SubwayCarry.Prototype.Editor
{
    [InitializeOnLoad]
    public static class GameplayFlowPrototypePlayModeVerifier
    {
        private const string RequestedKey =
            "SubwayCarry.GameplayFlowVerifier.Requested";
        private const string FinishedKey =
            "SubwayCarry.GameplayFlowVerifier.Finished";
        private const string SucceededKey =
            "SubwayCarry.GameplayFlowVerifier.Succeeded";

        private static int phase;
        private static double phaseStartedAt;

        static GameplayFlowPrototypePlayModeVerifier()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            SessionState.SetBool(RequestedKey, true);
            SessionState.SetBool(FinishedKey, false);
            SessionState.SetBool(SucceededKey, false);
            phase = 0;

            EditorSceneManager.OpenScene(
                GameplayFlowPrototypeBuilder.ScenePath,
                OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void HandlePlayModeStateChanged(
            PlayModeStateChange state)
        {
            if (!SessionState.GetBool(RequestedKey, false))
            {
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                phase = 0;
                phaseStartedAt = EditorApplication.timeSinceStartup;
                return;
            }

            if (state != PlayModeStateChange.EnteredEditMode ||
                !SessionState.GetBool(FinishedKey, false))
            {
                return;
            }

            bool succeeded = SessionState.GetBool(SucceededKey, false);
            SessionState.EraseBool(RequestedKey);
            SessionState.EraseBool(FinishedKey);
            SessionState.EraseBool(SucceededKey);

            if (succeeded)
            {
                Debug.Log(
                    "[Gameplay Flow Prototype] Play Mode cycle passed: " +
                    "tutorial -> selection -> gate -> train -> destination -> " +
                    "settlement -> return.");
                EditorApplication.Exit(0);
            }
            else
            {
                EditorApplication.Exit(1);
            }
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(RequestedKey, false) ||
                !EditorApplication.isPlaying)
            {
                return;
            }

            try
            {
                RunPhase();
            }
            catch (Exception exception)
            {
                Fail(exception.ToString());
            }
        }

        private static void RunPhase()
        {
            PrototypeGameFlowController flow =
                UnityEngine.Object.FindFirstObjectByType<PrototypeGameFlowController>();
            EconomyService economy =
                UnityEngine.Object.FindFirstObjectByType<EconomyService>();
            if (flow == null || economy == null)
            {
                if (Elapsed < 5f)
                {
                    return;
                }

                throw new InvalidOperationException(
                    "Flow controller or economy service did not start.");
            }

            switch (phase)
            {
                case 0:
                    if (Elapsed < 0.25f)
                    {
                        return;
                    }

                    InvokePrivate(flow, "FinishTutorial");
                    RequireStage(flow, PrototypeFlowStage.GachonHub);
                    NextPhase();
                    break;

                case 1:
                    InvokePrivate(flow, "SelectFirstDelivery");
                    RequireStage(flow, PrototypeFlowStage.DeliverySelected);
                    if (!flow.TryPerform(
                            PrototypeInteractionAction.TapDepartureGate))
                    {
                        throw new InvalidOperationException(
                            "Departure gate rejected the selected delivery.");
                    }

                    RequireStage(flow, PrototypeFlowStage.DeparturePlatform);
                    SetPrivateField(flow, "rideDuration", 0.15f);
                    NextPhase();
                    break;

                case 2:
                    if (Elapsed < 1.5f)
                    {
                        return;
                    }

                    flow.NotifyBoardedTrain();
                    RequireStage(flow, PrototypeFlowStage.TrainRide);
                    NextPhase();
                    break;

                case 3:
                    if (flow.CurrentStage !=
                        PrototypeFlowStage.DestinationStation)
                    {
                        if (Elapsed > 5f)
                        {
                            throw new TimeoutException(
                                "The train did not arrive at the destination.");
                        }

                        return;
                    }

                    flow.NotifyLeftTrainAtDestination();
                    if (!flow.CanPerform(
                            PrototypeInteractionAction.CompleteAtDestinationGate))
                    {
                        if (Elapsed > 5f)
                        {
                            throw new TimeoutException(
                                "The destination fade did not finish.");
                        }

                        return;
                    }

                    if (!flow.TryPerform(
                            PrototypeInteractionAction.CompleteAtDestinationGate))
                    {
                        throw new InvalidOperationException(
                            "Destination gate did not complete the delivery.");
                    }

                    RequireStage(flow, PrototypeFlowStage.Settlement);
                    int cash = economy.CurrentEconomyState.CurrentCash;
                    if (cash != 16500)
                    {
                        throw new InvalidOperationException(
                            "Unexpected settlement cash: " + cash);
                    }

                    InvokePrivate(flow, "ReturnToHub");
                    NextPhase();
                    break;

                case 4:
                    if (flow.CurrentStage == PrototypeFlowStage.GachonHub)
                    {
                        Succeed();
                        return;
                    }

                    if (Elapsed > 5f)
                    {
                        throw new TimeoutException(
                            "The flow did not return to Gachon hub.");
                    }

                    break;
            }
        }

        private static float Elapsed =>
            (float)(EditorApplication.timeSinceStartup - phaseStartedAt);

        private static void NextPhase()
        {
            phase++;
            phaseStartedAt = EditorApplication.timeSinceStartup;
        }

        private static void RequireStage(
            PrototypeGameFlowController flow,
            PrototypeFlowStage expected)
        {
            if (flow.CurrentStage != expected)
            {
                throw new InvalidOperationException(
                    "Expected stage " + expected +
                    " but was " + flow.CurrentStage + ".");
            }
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(
                    target.GetType().Name,
                    methodName);
            }

            method.Invoke(target, null);
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(
                    target.GetType().Name,
                    fieldName);
            }

            field.SetValue(target, value);
        }

        private static void Succeed()
        {
            SessionState.SetBool(SucceededKey, true);
            SessionState.SetBool(FinishedKey, true);
            EditorApplication.isPlaying = false;
        }

        private static void Fail(string reason)
        {
            Debug.LogError(
                "[Gameplay Flow Prototype] Play Mode cycle failed: " + reason);
            SessionState.SetBool(SucceededKey, false);
            SessionState.SetBool(FinishedKey, true);
            EditorApplication.isPlaying = false;
        }
    }
}
#endif

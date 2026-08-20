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
                    "tutorial -> locked departure gate -> escalator -> platform -> " +
                    "train arrival -> boarding -> ride -> destination platform -> " +
                    "escalator -> locked exit gate -> settlement -> return.");
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
            PrototypeTrainArrival trainArrival =
                UnityEngine.Object.FindFirstObjectByType<PrototypeTrainArrival>();
            if (flow == null || economy == null || trainArrival == null)
            {
                if (Elapsed < 5f)
                {
                    return;
                }

                throw new InvalidOperationException(
                    "Flow controller, economy service, or train arrival did not start.");
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
                    GameObject departureBlocker =
                        GameObject.Find("Departure Long Fare Gate Blocker");
                    if (departureBlocker == null || !departureBlocker.activeSelf)
                    {
                        throw new InvalidOperationException(
                            "Departure fare gate was not physically locked before card tap.");
                    }

                    if (!flow.TryPerform(
                            PrototypeInteractionAction.TapDepartureGate))
                    {
                        throw new InvalidOperationException(
                            "Departure gate rejected the selected delivery.");
                    }
                    if (departureBlocker.activeSelf)
                    {
                        throw new InvalidOperationException(
                            "Departure fare gate blocker stayed active after card tap.");
                    }

                    RequireStage(flow, PrototypeFlowStage.DepartureConcourse);
                    SetPrivateField(flow, "platformTrainWaitDuration", 0.05f);
                    SetPrivateField(flow, "boardingOpenGraceDuration", 0.05f);
                    SetPrivateField(flow, "rideDuration", 0.15f);
                    SetPrivateField(trainArrival, "arrivalDuration", 0.1f);
                    NextPhase();
                    break;

                case 2:
                    flow.NotifyEnteredDepartureEscalator();
                    RequireStage(flow, PrototypeFlowStage.DepartureEscalator);
                    NextPhase();
                    break;

                case 3:
                    if (GetPrivateField<bool>(flow, "transitioning"))
                    {
                        if (Elapsed > 5f)
                        {
                            throw new TimeoutException(
                                "The departure escalator fade did not finish.");
                        }

                        return;
                    }

                    flow.NotifyEnteredDeparturePlatform();
                    RequireStage(flow, PrototypeFlowStage.DeparturePlatform);
                    NextPhase();
                    break;

                case 4:
                    if (GetPrivateField<bool>(flow, "transitioning") ||
                        !GetPrivateField<bool>(flow, "departureTrainReady"))
                    {
                        if (Elapsed > 6f)
                        {
                            throw new TimeoutException(
                                "The departure train did not arrive and open its doors.");
                        }

                        return;
                    }

                    flow.NotifyBoardedTrain();
                    RequireStage(flow, PrototypeFlowStage.TrainBoarding);
                    NextPhase();
                    break;

                case 5:
                    if (flow.CurrentStage == PrototypeFlowStage.TrainRide)
                    {
                        NextPhase();
                        return;
                    }

                    if (Elapsed > 6f)
                    {
                        throw new TimeoutException(
                            "The train did not leave the departure platform.");
                    }

                    break;

                case 6:
                    if (flow.CurrentStage != PrototypeFlowStage.DestinationPlatform ||
                        GetPrivateField<bool>(flow, "transitioning"))
                    {
                        if (Elapsed > 6f)
                        {
                            throw new TimeoutException(
                                "The train did not arrive at the destination.");
                        }

                        return;
                    }

                    flow.NotifyLeftTrainAtDestination();
                    flow.NotifyEnteredDestinationEscalator();
                    RequireStage(flow, PrototypeFlowStage.DestinationEscalator);
                    NextPhase();
                    break;

                case 7:
                    if (GetPrivateField<bool>(flow, "transitioning"))
                    {
                        if (Elapsed > 5f)
                        {
                            throw new TimeoutException(
                                "The destination escalator fade did not finish.");
                        }

                        return;
                    }

                    flow.NotifyEnteredDestinationConcourse();
                    RequireStage(flow, PrototypeFlowStage.DestinationConcourse);
                    NextPhase();
                    break;

                case 8:
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

                    GameObject destinationBlocker =
                        GameObject.Find("Destination Long Fare Gate Blocker");
                    if (destinationBlocker == null || !destinationBlocker.activeSelf)
                    {
                        throw new InvalidOperationException(
                            "Destination fare gate was not physically locked before card tap.");
                    }

                    if (!flow.TryPerform(
                            PrototypeInteractionAction.CompleteAtDestinationGate))
                    {
                        throw new InvalidOperationException(
                            "Destination gate did not accept the transit card.");
                    }
                    if (destinationBlocker.activeSelf)
                    {
                        throw new InvalidOperationException(
                            "Destination fare gate blocker stayed active after card tap.");
                    }

                    RequireStage(flow, PrototypeFlowStage.DestinationConcourse);
                    flow.NotifyExitedDestinationGate();
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

                case 9:
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

        private static T GetPrivateField<T>(object target, string fieldName)
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

            return (T)field.GetValue(target);
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

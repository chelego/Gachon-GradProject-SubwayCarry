#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SubwayCarry.AI.Tests
{
    public sealed class PassengerJourneyPrototypeTests
    {
        [UnityTest]
        public IEnumerator PassengersCompleteStationBoardingTransferAndExitRoutes()
        {
            AiPrototypeSceneBuilder.BuildJourney();
            EditorSceneManager.OpenScene(
                AiPrototypeSceneBuilder.JourneyScenePath,
                OpenSceneMode.Single);

            yield return new EnterPlayMode();

            PassengerStationRoutePrototype route = null;
            float startupDeadline = Time.realtimeSinceStartup + 5f;
            while (route == null && Time.realtimeSinceStartup < startupDeadline)
            {
                route = Object.FindFirstObjectByType<PassengerStationRoutePrototype>();
                yield return null;
            }

            bool routeStarted = route != null;
            bool completed = false;
            int boardingCount = 0;
            int transferCount = 0;
            int exitCount = 0;
            int entryGateCount = 0;
            int exitGateCount = 0;
            int remainingPassengerCount = -1;
            float deadline = Time.realtimeSinceStartup + 120f;
            while (route != null && Time.realtimeSinceStartup < deadline)
            {
                boardingCount = route.BoardingCompletionCount;
                transferCount = route.TransferCompletionCount;
                exitCount = route.ExitCompletionCount;
                if (boardingCount == 4 && transferCount == 2 && exitCount == 4)
                {
                    remainingPassengerCount =
                        Object.FindObjectsByType<GeneralPassengerPrototype>(
                            FindObjectsSortMode.None).Length;
                    if (remainingPassengerCount == 0)
                    {
                        PassengerJourneyWaypoint[] waypoints =
                            Object.FindObjectsByType<PassengerJourneyWaypoint>(
                                FindObjectsSortMode.None);
                        entryGateCount = CountCompletions(
                            waypoints,
                            PassengerJourneyWaypointAction.TapEntryGate);
                        exitGateCount = CountCompletions(
                            waypoints,
                            PassengerJourneyWaypointAction.TapExitGate);
                        completed = true;
                        break;
                    }
                }

                yield return null;
            }

            yield return new ExitPlayMode();

            Assert.That(routeStarted, Is.True, "Passenger station route did not start.");
            Assert.That(
                completed,
                Is.True,
                "Journey timed out. boarding=" + boardingCount +
                ", transfer=" + transferCount +
                ", exit=" + exitCount +
                ", remaining=" + remainingPassengerCount + ".");
            Assert.That(entryGateCount, Is.EqualTo(4));
            Assert.That(exitGateCount, Is.EqualTo(4));
        }

        private static int CountCompletions(
            PassengerJourneyWaypoint[] waypoints,
            PassengerJourneyWaypointAction action)
        {
            int count = 0;
            foreach (PassengerJourneyWaypoint waypoint in waypoints)
            {
                if (waypoint != null && waypoint.Action == action)
                {
                    count += waypoint.CompletionCount;
                }
            }

            return count;
        }
    }
}
#endif

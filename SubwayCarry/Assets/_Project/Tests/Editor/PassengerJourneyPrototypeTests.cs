#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using SubwayCarry.Core.Contracts;
using SubwayCarry.Gameplay;
using SubwayCarry.Prototype;
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

        [UnityTest]
        public IEnumerator PlayerCanChooseBothDirectionalPlatformsWithoutPassengerAi()
        {
            AiPrototypeSceneBuilder.BuildJourneySketch();
            EditorSceneManager.OpenScene(
                AiPrototypeSceneBuilder.JourneySketchScenePath,
                OpenSceneMode.Single);

            yield return new EnterPlayMode();

            PlayerController player = null;
            StationMapScreenSwitcherPrototype screenSwitcher = null;
            float startupDeadline = Time.realtimeSinceStartup + 5f;
            while ((player == null || screenSwitcher == null) &&
                   Time.realtimeSinceStartup < startupDeadline)
            {
                player = Object.FindFirstObjectByType<PlayerController>();
                screenSwitcher =
                    Object.FindFirstObjectByType<StationMapScreenSwitcherPrototype>();
                yield return null;
            }

            Assert.That(player, Is.Not.Null, "Controllable player did not start.");
            Assert.That(screenSwitcher, Is.Not.Null, "Station map switcher did not start.");

            StationMapPortalPrototype[] portals =
                Object.FindObjectsByType<StationMapPortalPrototype>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            StationFareGatePrototype fareGate =
                Object.FindFirstObjectByType<StationFareGatePrototype>(
                    FindObjectsInactive.Include);
            GeneralPassengerPrototype[] passengers =
                Object.FindObjectsByType<GeneralPassengerPrototype>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            PassengerStationJourneyPrototype[] journeys =
                Object.FindObjectsByType<PassengerStationJourneyPrototype>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            Assert.That(portals.Length, Is.EqualTo(8));
            Assert.That(passengers.Length, Is.Zero, "Passenger AI exists in player scene.");
            Assert.That(journeys.Length, Is.Zero, "Station journey AI exists in player scene.");
            Assert.That(fareGate, Is.Not.Null);
            Assert.That(fareGate.IsOpen, Is.False);

            InteractionResult gateResult = fareGate.TryInteract(
                new InteractionContext(
                    player.gameObject,
                    player.transform.position,
                    player.FacingDirection));
            Assert.That(gateResult.Succeeded, Is.True);
            Assert.That(fareGate.IsOpen, Is.True);

            AssertPortalTransfer(portals, "Concourse To Upbound Stairs", player, screenSwitcher, "MAP 2A - Upbound Stairs");
            AssertPortalTransfer(portals, "Upbound Stairs To Platform", player, screenSwitcher, "MAP 3 - Platform");
            AssertPortalTransfer(portals, "Upbound Platform To Stairs", player, screenSwitcher, "MAP 2A - Upbound Stairs");
            AssertPortalTransfer(portals, "Upbound Stairs To Concourse", player, screenSwitcher, "MAP 1 - Concourse");

            AssertPortalTransfer(portals, "Concourse To Downbound Stairs", player, screenSwitcher, "MAP 2B - Downbound Stairs");
            AssertPortalTransfer(portals, "Downbound Stairs To Platform", player, screenSwitcher, "MAP 3 - Platform");
            AssertPortalTransfer(portals, "Downbound Platform To Stairs", player, screenSwitcher, "MAP 2B - Downbound Stairs");
            AssertPortalTransfer(portals, "Downbound Stairs To Concourse", player, screenSwitcher, "MAP 1 - Concourse");

            yield return new ExitPlayMode();
        }

        private static void AssertPortalTransfer(
            StationMapPortalPrototype[] portals,
            string portalName,
            PlayerController player,
            StationMapScreenSwitcherPrototype screenSwitcher,
            string expectedScreenName)
        {
            StationMapPortalPrototype portal = null;
            foreach (StationMapPortalPrototype candidate in portals)
            {
                if (candidate != null && candidate.gameObject.name == portalName)
                {
                    portal = candidate;
                    break;
                }
            }

            Assert.That(portal, Is.Not.Null, "Missing portal: " + portalName);
            Assert.That(portal.Transfer(player), Is.True, "Portal failed: " + portalName);
            Assert.That(screenSwitcher.CurrentScreen, Is.Not.Null);
            Assert.That(screenSwitcher.CurrentScreen.name, Is.EqualTo(expectedScreenName));
            Assert.That(
                Vector2.Distance(player.transform.position, portal.ArrivalPoint.position),
                Is.LessThan(0.01f),
                "Player did not arrive at the configured destination for " + portalName + ".");
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

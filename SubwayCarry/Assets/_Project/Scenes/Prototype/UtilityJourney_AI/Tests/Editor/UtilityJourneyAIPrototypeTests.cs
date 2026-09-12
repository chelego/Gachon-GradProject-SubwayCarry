#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using SubwayCarry.AI.UtilityJourney.Editor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SubwayCarry.AI.UtilityJourney.Tests
{
    public sealed class UtilityJourneyAIPrototypeTests
    {
        [Test]
        public void BeforeTapInEntryGateOutscoresAndInvalidatesPlatformStair()
        {
            UtilityPassengerFacts facts = CreateFacts(
                UtilityJourneyArea.OriginConcourse);
            UtilityCandidateScore gate = Evaluate(
                facts,
                "gate",
                UtilityJourneyFacilityKind.EntryGate,
                UtilityJourneyDirection.Any,
                6f);
            UtilityCandidateScore stair = Evaluate(
                facts,
                "stair",
                UtilityJourneyFacilityKind.PlatformDownStair,
                UtilityJourneyDirection.Downbound,
                2f);

            Assert.That(gate.IsValid, Is.True);
            Assert.That(gate.Action, Is.EqualTo(UtilityJourneyAction.TapIn));
            Assert.That(stair.IsValid, Is.False);
        }

        [Test]
        public void PaidPassengerChoosesDirectionCompatiblePlatformStair()
        {
            UtilityPassengerFacts facts = CreateFacts(
                UtilityJourneyArea.OriginConcourse);
            facts.FarePaid = true;
            facts.RequiredDirection = UtilityJourneyDirection.Downbound;
            UtilityCandidateScore correct = Evaluate(
                facts,
                "down",
                UtilityJourneyFacilityKind.PlatformDownStair,
                UtilityJourneyDirection.Downbound,
                8f);
            UtilityCandidateScore wrong = Evaluate(
                facts,
                "up",
                UtilityJourneyFacilityKind.PlatformDownStair,
                UtilityJourneyDirection.Upbound,
                2f);

            Assert.That(correct.IsValid, Is.True);
            Assert.That(correct.Action,
                Is.EqualTo(UtilityJourneyAction.DescendToPlatform));
            Assert.That(wrong.IsValid, Is.False);
        }

        [Test]
        public void PlatformWaitIsReplacedByBoardingWhenTrainDoorOpens()
        {
            UtilityPassengerFacts facts = CreateFacts(
                UtilityJourneyArea.OriginPlatform);
            UtilityCandidateScore waiting = Evaluate(
                facts,
                "wait",
                UtilityJourneyFacilityKind.PlatformWaitingArea,
                UtilityJourneyDirection.Downbound,
                1f);
            UtilityCandidateScore closedDoor = Evaluate(
                facts,
                "door",
                UtilityJourneyFacilityKind.TrainDoor,
                UtilityJourneyDirection.Downbound,
                2f);
            facts.TrainAtPlatform = true;
            facts.TrainDoorOpen = true;
            UtilityCandidateScore openDoor = Evaluate(
                facts,
                "door",
                UtilityJourneyFacilityKind.TrainDoor,
                UtilityJourneyDirection.Downbound,
                2f);

            Assert.That(waiting.IsValid, Is.True);
            Assert.That(waiting.Action,
                Is.EqualTo(UtilityJourneyAction.WaitForTrain));
            Assert.That(closedDoor.IsValid, Is.False);
            Assert.That(openDoor.IsValid, Is.True);
            Assert.That(openDoor.Action,
                Is.EqualTo(UtilityJourneyAction.BoardTrain));
            Assert.That(openDoor.Score, Is.GreaterThan(waiting.Score));
        }

        [Test]
        public void DestinationDoorOpenInvalidatesRideSpotAndForcesAlighting()
        {
            UtilityPassengerFacts facts = CreateFacts(
                UtilityJourneyArea.TrainInterior);
            facts.TrainAtPlatform = true;
            facts.TrainDoorOpen = true;
            facts.IsOnTrain = true;
            facts.IsSettledInsideTrain = true;
            facts.DestinationReady = true;

            UtilityCandidateScore rideSpot = Evaluate(
                facts,
                "ride-spot",
                UtilityJourneyFacilityKind.TrainRideSpot,
                UtilityJourneyDirection.Downbound,
                0.2f,
                UtilityJourneyWaitingStyle.BenchSeat,
                1f);
            UtilityCandidateScore alightDoor = Evaluate(
                facts,
                "alight-door",
                UtilityJourneyFacilityKind.TrainDoor,
                UtilityJourneyDirection.Downbound,
                5f);

            Assert.That(rideSpot.IsValid, Is.False);
            Assert.That(alightDoor.IsValid, Is.True);
            Assert.That(alightDoor.Action,
                Is.EqualTo(UtilityJourneyAction.AlightTrain));
        }

        [Test]
        public void RelaxedPassengerCanPreferBenchWhileTrainIsFarAway()
        {
            UtilityPassengerFacts facts = CreateFacts(
                UtilityJourneyArea.OriginPlatform);
            facts.SecondsUntilTrainArrival = 9f;
            UtilityCandidateScore bench = Evaluate(
                facts,
                "bench",
                UtilityJourneyFacilityKind.PlatformWaitingArea,
                UtilityJourneyDirection.Downbound,
                5f,
                UtilityJourneyWaitingStyle.BenchSeat,
                0.95f);
            UtilityCandidateScore queue = Evaluate(
                facts,
                "queue",
                UtilityJourneyFacilityKind.PlatformWaitingArea,
                UtilityJourneyDirection.Downbound,
                5f,
                UtilityJourneyWaitingStyle.DoorQueue,
                0.15f);

            Assert.That(bench.Score, Is.GreaterThan(queue.Score));
        }

        [Test]
        public void ImminentTrainPullsPassengerFromBenchToDoorQueue()
        {
            UtilityPassengerFacts facts = CreateFacts(
                UtilityJourneyArea.OriginPlatform);
            facts.SecondsUntilTrainArrival = 1.5f;
            UtilityCandidateScore bench = Evaluate(
                facts,
                "bench",
                UtilityJourneyFacilityKind.PlatformWaitingArea,
                UtilityJourneyDirection.Downbound,
                5f,
                UtilityJourneyWaitingStyle.BenchSeat,
                0.95f);
            UtilityCandidateScore queue = Evaluate(
                facts,
                "queue",
                UtilityJourneyFacilityKind.PlatformWaitingArea,
                UtilityJourneyDirection.Downbound,
                5f,
                UtilityJourneyWaitingStyle.DoorQueue,
                0.45f);

            Assert.That(queue.Score, Is.GreaterThan(bench.Score));
        }

        [Test]
        public void PreferredRandomStreetExitCanOverrideShorterExit()
        {
            UtilityPassengerFacts facts = CreateFacts(
                UtilityJourneyArea.DestinationConcourse);
            facts.PreferredExitId = "far-exit";
            UtilityCandidateScore near = Evaluate(
                facts,
                "near-exit",
                UtilityJourneyFacilityKind.StreetExit,
                UtilityJourneyDirection.Any,
                2f);
            UtilityCandidateScore preferred = Evaluate(
                facts,
                "far-exit",
                UtilityJourneyFacilityKind.StreetExit,
                UtilityJourneyDirection.Any,
                20f);

            Assert.That(preferred.IsValid, Is.True);
            Assert.That(preferred.Score, Is.GreaterThan(near.Score));
        }

        [UnityTest]
        public IEnumerator OnePassengerCompletesBothMapsAndTrainWithoutAuthoredWaypoints()
        {
            UtilityJourneyAIPrototypeBuilder.Build();
            EditorSceneManager.OpenScene(
                UtilityJourneyAIPrototypeBuilder.ScenePath,
                OpenSceneMode.Single);

            UtilityJourneyWorldPrototype editWorld =
                Object.FindFirstObjectByType<UtilityJourneyWorldPrototype>(
                    FindObjectsInactive.Include);
            UtilityPassengerBrainPrototype editBrain =
                Object.FindFirstObjectByType<UtilityPassengerBrainPrototype>(
                    FindObjectsInactive.Include);
            Assert.That(editWorld, Is.Not.Null);
            Assert.That(editBrain, Is.Not.Null);
            editWorld.ConfigurePrototypeTimings(0.05f, 0.12f, 0.8f, 0.01f);
            editBrain.ConfigurePrototypeMotion(18f, 0.03f, 0.05f);

            yield return new EnterPlayMode();

            UtilityJourneyWorldPrototype world = null;
            UtilityPassengerBrainPrototype brain = null;
            float deadline = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < deadline)
            {
                world ??= Object.FindFirstObjectByType<UtilityJourneyWorldPrototype>(
                    FindObjectsInactive.Include);
                brain ??= Object.FindFirstObjectByType<UtilityPassengerBrainPrototype>(
                    FindObjectsInactive.Include);
                if (world != null && world.IsComplete)
                {
                    break;
                }
                yield return null;
            }

            int waypointCount = Object.FindObjectsByType<PassengerJourneyWaypoint>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;
            bool completed = world != null && world.IsComplete;
            int entryTaps = world != null ? world.EntryGateTapCount : -1;
            int exitTaps = world != null ? world.ExitGateTapCount : -1;
            int transitions = world != null ? world.TransitionCount : -1;
            int decisions = brain != null ? brain.DecisionCount : -1;
            int visitedAreas = brain != null ? brain.VisitedAreaCount : -1;
            bool authoredRoute = brain != null && brain.UsesAuthoredWaypointRoute;

            yield return new ExitPlayMode();

            Assert.That(completed, Is.True, "Utility journey timed out.");
            Assert.That(entryTaps, Is.EqualTo(1));
            Assert.That(exitTaps, Is.EqualTo(1));
            Assert.That(transitions, Is.EqualTo(4));
            Assert.That(decisions, Is.GreaterThan(8));
            Assert.That(visitedAreas, Is.GreaterThanOrEqualTo(5));
            Assert.That(authoredRoute, Is.False);
            Assert.That(waypointCount, Is.Zero);
        }

        private static UtilityPassengerFacts CreateFacts(UtilityJourneyArea area)
        {
            return new UtilityPassengerFacts
            {
                Area = area,
                RequiredDirection = UtilityJourneyDirection.Downbound,
                FarePaid = false,
                TrainAtPlatform = false,
                TrainDoorOpen = false,
                IsOnTrain = area == UtilityJourneyArea.TrainInterior,
                IsSettledInsideTrain = false,
                DestinationReady = false,
                IsTransitioning = false,
                IsComplete = false,
                PreferredExitId = string.Empty,
                SecondsUntilTrainArrival = float.PositiveInfinity
            };
        }

        private static UtilityCandidateScore Evaluate(
            UtilityPassengerFacts facts,
            string id,
            UtilityJourneyFacilityKind kind,
            UtilityJourneyDirection direction,
            float distance,
            UtilityJourneyWaitingStyle waitingStyle =
                UtilityJourneyWaitingStyle.None,
            float personalPreference = 0.5f)
        {
            var facility = new UtilityFacilityObservation(
                id,
                kind,
                facts.Area,
                direction,
                true,
                true,
                distance,
                0f,
                0f,
                personalPreference,
                false,
                waitingStyle,
                string.Empty);
            return UtilityJourneyDecisionModel.Evaluate(facts, facility);
        }
    }
}
#endif

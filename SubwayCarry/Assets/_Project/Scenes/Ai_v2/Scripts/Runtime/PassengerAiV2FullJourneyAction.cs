using UnityEngine;

namespace SubwayCarry.AI.V2
{
    /// <summary>
    /// Journey Planner가 고른 현재 단계의 Smart Object 접근, 예약, 사용과 완료를 실행한다.
    /// </summary>
    [RequireComponent(typeof(PassengerAiV2Agent))]
    public sealed class PassengerAiV2FullJourneyAction : MonoBehaviour
    {
        [SerializeField, Range(4f, 20f)] private float actionUpdateHz = 10f;
        [SerializeField, Min(5f)] private float recoverableTimeout = 45f;

        private PassengerAiV2Agent agent;
        private PassengerAiV2FullJourneyScenario scenario;
        private PassengerAiV2FullJourneyPlanner planner;
        private PassengerAiV2JourneyBlackboard blackboard;
        private PassengerAiV2RuntimePersonality personality;
        private PassengerAiV2FullJourneyStage observedStage;
        private PassengerAiV2TrainInteriorAction interiorAction;
        private int substep;
        private int transitionSerialAtRequest;
        private float nextUpdateTime;
        private float interactionReadyAt;

        public int CompletedSmartObjectActions { get; private set; }
        public int UtilityDecisionCount { get; private set; }

        public void Initialize(
            PassengerAiV2Agent passenger,
            PassengerAiV2FullJourneyScenario journeyScenario,
            PassengerAiV2FullJourneyPlanner journeyPlanner,
            PassengerAiV2JourneyBlackboard journeyBlackboard,
            PassengerAiV2RuntimePersonality runtimePersonality)
        {
            agent = passenger;
            scenario = journeyScenario;
            planner = journeyPlanner;
            blackboard = journeyBlackboard;
            personality = runtimePersonality;
            observedStage = blackboard.CurrentStage;
            substep = 0;
            nextUpdateTime = Time.time;
        }

        private void Update()
        {
            if (agent == null
                || scenario == null
                || planner == null
                || blackboard == null
                || scenario.IsTransitioning
                || Time.time < nextUpdateTime)
            {
                return;
            }

            nextUpdateTime = Time.time + 1f / Mathf.Max(4f, actionUpdateHz);
            if (observedStage != blackboard.CurrentStage)
            {
                observedStage = blackboard.CurrentStage;
                substep = 0;
                interactionReadyAt = 0f;
            }

            if (observedStage != PassengerAiV2FullJourneyStage.Completed
                && Time.time - blackboard.StageStartedAt >= recoverableTimeout)
            {
                RecoverCurrentStage();
            }

            switch (observedStage)
            {
                case PassengerAiV2FullJourneyStage.EnterOriginStation:
                    if (TickStair(
                            scenario.OriginStreetStair,
                            PassengerAiV2StairDirection.LowerToUpper))
                    {
                        CompleteStage();
                    }
                    break;

                case PassengerAiV2FullJourneyStage.PassOriginEntryGate:
                    if (TickGate(
                            scenario.OriginEntryGate,
                            PassengerAiV2FareGateDirection.LowerToUpper))
                    {
                        blackboard.MarkEntryCardTagged();
                        CompleteStage();
                    }
                    break;

                case PassengerAiV2FullJourneyStage.ReachOriginPlatform:
                    TickOriginPlatformTransition();
                    break;

                case PassengerAiV2FullJourneyStage.WaitForOriginTrain:
                    TickWaitForOriginTrain();
                    break;

                case PassengerAiV2FullJourneyStage.BoardOriginTrain:
                    TickBoardAndEnterTrain();
                    break;

                case PassengerAiV2FullJourneyStage.ChooseTrainActivity:
                    TickChooseTrainActivity();
                    break;

                case PassengerAiV2FullJourneyStage.RideAndPrepareToAlight:
                    TickRideAndPrepare();
                    break;

                case PassengerAiV2FullJourneyStage.AlightAtDestination:
                    if (TickTrainDoor(
                            scenario.DestinationTrainDoor,
                            PassengerAiV2TrainDoorFlow.Alight))
                    {
                        blackboard.MarkAlightedTrain();
                        CompleteStage();
                    }
                    break;

                case PassengerAiV2FullJourneyStage.ReachDestinationConcourse:
                    TickDestinationConcourseTransition();
                    break;

                case PassengerAiV2FullJourneyStage.PassDestinationExitGate:
                    if (TickGate(
                            scenario.DestinationExitGate,
                            PassengerAiV2FareGateDirection.UpperToLower))
                    {
                        blackboard.MarkExitCardTagged();
                        CompleteStage();
                    }
                    break;

                case PassengerAiV2FullJourneyStage.LeaveDestinationStation:
                    TickLeaveDestinationStation();
                    break;

                case PassengerAiV2FullJourneyStage.Completed:
                    agent.SetHoldPosition(true);
                    break;
            }
        }

        private void TickOriginPlatformTransition()
        {
            if (substep < 10)
            {
                if (!TickStair(
                        scenario.OriginPlatformStair,
                        PassengerAiV2StairDirection.LowerToUpper))
                {
                    return;
                }

                transitionSerialAtRequest = scenario.TransitionSerial;
                if (scenario.RequestMapTransition(
                        PassengerAiV2JourneyMap.OriginPlatform,
                        scenario.OriginPlatformSpawn,
                        scenario.StationBounds))
                {
                    substep = 10;
                }
                return;
            }

            if (!scenario.IsTransitioning
                && scenario.ActiveMap == PassengerAiV2JourneyMap.OriginPlatform
                && scenario.TransitionSerial > transitionSerialAtRequest)
            {
                CompleteStage();
            }
        }

        private void TickWaitForOriginTrain()
        {
            MoveTo(scenario.OriginPlatformWaitPoint);
            if (!Reached(scenario.OriginPlatformWaitPoint, 0.55f))
            {
                return;
            }

            agent.SetHoldPosition(true);
            PassengerAiV2TrainDoorPhase phase = scenario.OriginTrainDoor.Phase;
            if (phase == PassengerAiV2TrainDoorPhase.Opening
                || phase == PassengerAiV2TrainDoorPhase.Alighting
                || phase == PassengerAiV2TrainDoorPhase.Boarding)
            {
                CompleteStage();
            }
        }

        private void TickBoardAndEnterTrain()
        {
            if (substep < 10)
            {
                if (!TickTrainDoor(
                        scenario.OriginTrainDoor,
                        PassengerAiV2TrainDoorFlow.Board))
                {
                    return;
                }

                blackboard.MarkBoardedTrain();
                transitionSerialAtRequest = scenario.TransitionSerial;
                if (scenario.RequestMapTransition(
                        PassengerAiV2JourneyMap.TrainInterior,
                        scenario.TrainInteriorSpawn,
                        scenario.TrainInteriorBounds))
                {
                    substep = 10;
                }
                return;
            }

            if (!scenario.IsTransitioning
                && scenario.ActiveMap == PassengerAiV2JourneyMap.TrainInterior
                && scenario.TransitionSerial > transitionSerialAtRequest)
            {
                CompleteStage();
            }
        }

        private void TickChooseTrainActivity()
        {
            if (substep == 0)
            {
                UtilityDecisionCount++;
                interiorAction = scenario.StartTrainInteriorActivity(agent, personality);
                substep = 1;
                return;
            }

            if (interiorAction != null && interiorAction.IsUsingActivity)
            {
                blackboard.MarkTrainActivityUsed();
                CompleteStage();
            }
        }

        private void TickRideAndPrepare()
        {
            if (substep >= 10)
            {
                if (!scenario.IsTransitioning
                    && scenario.ActiveMap == PassengerAiV2JourneyMap.DestinationPlatform
                    && scenario.TransitionSerial > transitionSerialAtRequest)
                {
                    CompleteStage();
                }
                return;
            }

            if (interiorAction == null)
            {
                interiorAction = scenario.StartTrainInteriorActivity(agent, personality);
            }

            if (!interiorAction.IsPrepared)
            {
                return;
            }

            blackboard.MarkPreparedToAlight();
            scenario.StopTrainInteriorActivity();
            interiorAction = null;
            transitionSerialAtRequest = scenario.TransitionSerial;
            if (scenario.RequestMapTransition(
                    PassengerAiV2JourneyMap.DestinationPlatform,
                    scenario.DestinationTrainSpawn,
                    scenario.StationBounds))
            {
                substep = 10;
            }
        }

        private void TickDestinationConcourseTransition()
        {
            if (substep < 10)
            {
                if (!TickStair(
                        scenario.DestinationPlatformStair,
                        PassengerAiV2StairDirection.UpperToLower))
                {
                    return;
                }

                transitionSerialAtRequest = scenario.TransitionSerial;
                if (scenario.RequestMapTransition(
                        PassengerAiV2JourneyMap.DestinationConcourse,
                        scenario.DestinationConcourseSpawn,
                        scenario.StationBounds))
                {
                    substep = 10;
                }
                return;
            }

            if (!scenario.IsTransitioning
                && scenario.ActiveMap == PassengerAiV2JourneyMap.DestinationConcourse
                && scenario.TransitionSerial > transitionSerialAtRequest)
            {
                CompleteStage();
            }
        }

        private void TickLeaveDestinationStation()
        {
            if (substep < 10)
            {
                if (TickStair(
                        scenario.DestinationStreetStair,
                        PassengerAiV2StairDirection.UpperToLower))
                {
                    substep = 10;
                }
                return;
            }

            MoveTo(scenario.DestinationOutsidePoint);
            if (Reached(scenario.DestinationOutsidePoint, 0.55f))
            {
                blackboard.MarkJourneyCompleted();
                CompleteStage();
            }
        }

        private bool TickGate(
            PassengerAiV2FareGateSmartObject gate,
            PassengerAiV2FareGateDirection direction)
        {
            if (gate == null)
            {
                return false;
            }

            switch (substep)
            {
                case 0:
                    gate.RequestUse(
                        agent,
                        (int)direction,
                        Vector2.Distance(agent.Position, gate.GetApproachPoint()));
                    MoveTo(gate.HasReservation(agent)
                        ? gate.GetReaderPoint()
                        : gate.GetQueuePosition(agent));
                    if (gate.HasReservation(agent))
                    {
                        substep = 1;
                    }
                    break;

                case 1:
                    MoveTo(gate.GetReaderPoint());
                    if (Reached(gate.GetReaderPoint(), 0.48f))
                    {
                        agent.SetHoldPosition(true);
                        interactionReadyAt = Time.time + 0.28f;
                        substep = 2;
                    }
                    break;

                case 2:
                    if (Time.time >= interactionReadyAt && gate.TagCard(agent))
                    {
                        MoveTo(gate.GetPassPoint());
                        substep = 3;
                    }
                    break;

                case 3:
                    MoveTo(gate.GetPassPoint());
                    if (Reached(gate.GetPassPoint(), 0.48f) && gate.MarkPassed(agent))
                    {
                        MoveTo(gate.GetReleasePoint());
                        substep = 4;
                    }
                    break;

                case 4:
                    MoveTo(gate.GetReleasePoint());
                    if (Reached(gate.GetReleasePoint(), 0.52f))
                    {
                        gate.Release(agent);
                        CompletedSmartObjectActions++;
                        return true;
                    }
                    break;
            }

            return false;
        }

        private bool TickStair(
            PassengerAiV2StairSmartObject stair,
            PassengerAiV2StairDirection direction)
        {
            if (stair == null)
            {
                return false;
            }

            switch (substep)
            {
                case 0:
                    stair.RequestUse(
                        agent,
                        (int)direction,
                        Vector2.Distance(agent.Position, stair.GetApproachPoint(direction)));
                    MoveTo(stair.HasReservation(agent)
                        ? stair.GetApproachPoint(direction)
                        : stair.GetQueuePosition(agent, direction));
                    if (stair.HasReservation(agent)
                        && Reached(stair.GetApproachPoint(direction), 0.55f))
                    {
                        MoveTo(stair.GetEntryPoint(direction));
                        substep = 1;
                    }
                    break;

                case 1:
                    MoveTo(stair.GetEntryPoint(direction));
                    if (Reached(stair.GetEntryPoint(direction), 0.5f))
                    {
                        stair.MarkEntered(agent);
                        MoveTo(stair.GetExitPoint(direction));
                        substep = 2;
                    }
                    break;

                case 2:
                    MoveTo(stair.GetExitPoint(direction));
                    if (Reached(stair.GetExitPoint(direction), 0.52f))
                    {
                        MoveTo(stair.GetReleasePoint(direction));
                        substep = 3;
                    }
                    break;

                case 3:
                    MoveTo(stair.GetReleasePoint(direction));
                    if (Reached(stair.GetReleasePoint(direction), 0.55f))
                    {
                        stair.Release(agent);
                        CompletedSmartObjectActions++;
                        return true;
                    }
                    break;
            }

            return false;
        }

        private bool TickTrainDoor(
            PassengerAiV2TrainDoorSmartObject door,
            PassengerAiV2TrainDoorFlow flow)
        {
            if (door == null)
            {
                return false;
            }

            int direction = flow == PassengerAiV2TrainDoorFlow.Alight ? 1 : -1;
            switch (substep)
            {
                case 0:
                    door.RequestUse(agent, direction,
                        Vector2.Distance(agent.Position, door.GetQueuePosition(agent, flow)));
                    MoveTo(door.HasReservation(agent)
                        ? door.GetApproachPoint(agent)
                        : door.IsFlowReleased(flow)
                            ? door.GetConvoyPosition(agent, flow)
                            : door.GetQueuePosition(agent, flow));
                    if (door.HasReservation(agent) && door.CanPass(agent))
                    {
                        MoveTo(door.GetApproachPoint(agent));
                        substep = 1;
                    }
                    break;

                case 1:
                    MoveTo(door.GetApproachPoint(agent));
                    if (Reached(door.GetApproachPoint(agent), 0.5f))
                    {
                        MoveTo(door.GetPassPoint(agent));
                        substep = 2;
                    }
                    break;

                case 2:
                    MoveTo(door.GetPassPoint(agent));
                    if (Reached(door.GetPassPoint(agent), 0.5f) && door.MarkPassed(agent))
                    {
                        MoveTo(door.GetReleasePoint(agent));
                        substep = 3;
                    }
                    break;

                case 3:
                    MoveTo(door.GetReleasePoint(agent));
                    if (Reached(door.GetReleasePoint(agent), 0.55f))
                    {
                        door.Release(agent);
                        CompletedSmartObjectActions++;
                        return true;
                    }
                    break;
            }

            return false;
        }

        private void CompleteStage()
        {
            planner.CompleteCurrentStage(observedStage);
        }

        private void MoveTo(Vector2 target)
        {
            agent.SetHoldPosition(false);
            if ((agent.MovementTarget - target).sqrMagnitude > 0.01f)
            {
                agent.SetMovementTarget(target);
            }
        }

        private bool Reached(Vector2 point, float radius)
        {
            return (agent.Position - point).sqrMagnitude <= radius * radius;
        }

        private void RecoverCurrentStage()
        {
            switch (observedStage)
            {
                case PassengerAiV2FullJourneyStage.EnterOriginStation:
                    scenario.OriginStreetStair?.Cancel(agent);
                    break;
                case PassengerAiV2FullJourneyStage.PassOriginEntryGate:
                    scenario.OriginEntryGate?.Cancel(agent);
                    break;
                case PassengerAiV2FullJourneyStage.ReachOriginPlatform:
                    scenario.OriginPlatformStair?.Cancel(agent);
                    break;
                case PassengerAiV2FullJourneyStage.BoardOriginTrain:
                    scenario.OriginTrainDoor?.Cancel(agent);
                    break;
                case PassengerAiV2FullJourneyStage.AlightAtDestination:
                    scenario.DestinationTrainDoor?.Cancel(agent);
                    break;
                case PassengerAiV2FullJourneyStage.ReachDestinationConcourse:
                    scenario.DestinationPlatformStair?.Cancel(agent);
                    break;
                case PassengerAiV2FullJourneyStage.PassDestinationExitGate:
                    scenario.DestinationExitGate?.Cancel(agent);
                    break;
                case PassengerAiV2FullJourneyStage.LeaveDestinationStation:
                    scenario.DestinationStreetStair?.Cancel(agent);
                    break;
            }

            planner.ReportRecoverableFailure("Stage timeout");
            substep = 0;
            interactionReadyAt = 0f;
            agent.SetHoldPosition(false);
        }
    }
}

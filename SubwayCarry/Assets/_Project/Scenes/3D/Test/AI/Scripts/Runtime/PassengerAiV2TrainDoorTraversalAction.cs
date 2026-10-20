using UnityEngine;

namespace SubwayCarry.AI.V2
{
    /// <summary>
    /// 문 흐름에 맞춰 대기, 예약 Lane 접근, 문 통과, 안전지점 이탈과 다음 주기 대기를 수행한다.
    /// 탑승 승객은 가까운 쪽 줄의 순서를 유지하고, 하차 말미부터 맨 앞 승객 순으로 진입한다.
    /// </summary>
    [RequireComponent(typeof(PassengerAiV2Agent))]
    public sealed class PassengerAiV2TrainDoorTraversalAction : MonoBehaviour
    {
        private enum DoorActionPhase
        {
            Queueing,
            MovingToDoor,
            PassingDoor,
            LeavingDoor,
            ReachingDestination,
            WaitingNextCycle
        }

        [SerializeField, Range(2f, 20f)] private float actionUpdateHz = 12f;
        [SerializeField, Range(1f, 1.6f)] private float alightApproachSpeedScale = 1.1f;
        [SerializeField, Range(1f, 1.8f)] private float alightPassageSpeedScale = 1.26f;
        [SerializeField, Range(1f, 1.5f)] private float boardPassageSpeedScale = 1.1f;

        private PassengerAiV2Agent agent;
        private PassengerAiV2TrainDoorSmartObject door;
        private PassengerAiV2TrainDoorFlow flow;
        private Vector2 journeyStart;
        private Vector2 journeyDestination;
        private Rect worldBounds;
        private DoorActionPhase phase;
        private int activeCycle = -1;
        private int completedCycle = -1;
        private float nextActionUpdateTime;

        public bool IsWaiting => phase == DoorActionPhase.Queueing
            && door != null
            && !door.HasReservation(agent);
        public bool IsPassing => phase == DoorActionPhase.PassingDoor
            || phase == DoorActionPhase.LeavingDoor;

        public void Initialize(
            PassengerAiV2Agent passenger,
            PassengerAiV2TrainDoorSmartObject trainDoor,
            PassengerAiV2TrainDoorFlow requestedFlow,
            Vector2 start,
            Vector2 destination,
            Rect allowedWorldBounds,
            float stagger01)
        {
            agent = passenger;
            door = trainDoor;
            flow = requestedFlow;
            journeyStart = start;
            journeyDestination = destination;
            worldBounds = allowedWorldBounds;
            nextActionUpdateTime = Time.time
                + Mathf.Clamp01(stagger01) / Mathf.Max(2f, actionUpdateHz);

            agent.SetAutoLoop(false);
            agent.SetMovementBounds(worldBounds);
            BeginCycle();
        }

        private void OnDestroy()
        {
            if (door != null && agent != null)
            {
                door.Cancel(agent);
                agent.SetMovementSpeedScale(1f);
            }
        }

        private void Update()
        {
            if (agent == null || door == null || Time.time < nextActionUpdateTime)
            {
                return;
            }

            nextActionUpdateTime = Time.time + 1f / Mathf.Max(2f, actionUpdateHz);
            TickAction();
        }

        private void BeginCycle()
        {
            activeCycle = door.CycleIndex;
            agent.SetMovementBounds(worldBounds);
            agent.SetMovementSpeedScale(
                flow == PassengerAiV2TrainDoorFlow.Alight ? alightApproachSpeedScale : 1f);
            agent.TeleportAndSetTarget(journeyStart, journeyStart);
            door.RequestUse(agent, (int)flow, float.PositiveInfinity);
            Vector2 queuePosition = door.GetQueuePosition(agent, flow);
            agent.SetMovementTarget(queuePosition);
            phase = DoorActionPhase.Queueing;
        }

        private void TickAction()
        {
            switch (phase)
            {
                case DoorActionPhase.Queueing:
                    bool flowReleased = door.IsFlowReleased(flow);
                    Vector2 queuePosition = flowReleased
                        ? door.GetConvoyPosition(agent, flow)
                        : door.GetQueuePosition(agent, flow);
                    door.RequestUse(
                        agent,
                        (int)flow,
                        Vector2.Distance(agent.Position, queuePosition));
                    activeCycle = door.CycleIndex;
                    if (door.HasReservation(agent))
                    {
                        agent.SetMovementSpeedScale(
                            flow == PassengerAiV2TrainDoorFlow.Alight
                                ? alightPassageSpeedScale
                                : boardPassageSpeedScale);
                        agent.SetMovementTarget(door.GetApproachPoint(agent));
                        phase = DoorActionPhase.MovingToDoor;
                    }
                    else
                    {
                        SetTargetIfChanged(queuePosition, flowReleased);
                    }
                    break;

                case DoorActionPhase.MovingToDoor:
                    if (agent.HasReachedTarget && door.CanPass(agent))
                    {
                        agent.SetMovementTarget(door.GetPassPoint(agent));
                        phase = DoorActionPhase.PassingDoor;
                    }
                    break;

                case DoorActionPhase.PassingDoor:
                    if (agent.HasReachedTarget && door.MarkPassed(agent))
                    {
                        agent.SetMovementTarget(door.GetReleasePoint(agent));
                        phase = DoorActionPhase.LeavingDoor;
                    }
                    break;

                case DoorActionPhase.LeavingDoor:
                    if (agent.HasReachedTarget)
                    {
                        door.Release(agent);
                        agent.SetMovementSpeedScale(
                            flow == PassengerAiV2TrainDoorFlow.Alight ? 1.15f : 1f);
                        agent.SetMovementTarget(journeyDestination);
                        phase = DoorActionPhase.ReachingDestination;
                    }
                    break;

                case DoorActionPhase.ReachingDestination:
                    if (agent.HasReachedTarget)
                    {
                        completedCycle = activeCycle;
                        agent.SetMovementSpeedScale(1f);
                        agent.SetHoldPosition(true);
                        phase = DoorActionPhase.WaitingNextCycle;
                    }
                    break;

                case DoorActionPhase.WaitingNextCycle:
                    if (door.CycleIndex > completedCycle && CanJoinCurrentCycle())
                    {
                        BeginCycle();
                    }
                    break;
            }
        }

        private bool CanJoinCurrentCycle()
        {
            if (door.Phase == PassengerAiV2TrainDoorPhase.Opening)
            {
                return true;
            }

            if (flow == PassengerAiV2TrainDoorFlow.Alight)
            {
                return door.Phase == PassengerAiV2TrainDoorPhase.Alighting;
            }

            return door.Phase == PassengerAiV2TrainDoorPhase.Alighting
                || door.Phase == PassengerAiV2TrainDoorPhase.Boarding;
        }

        private void SetTargetIfChanged(Vector2 target, bool followingMovingQueue)
        {
            if ((agent.MovementTarget - target).sqrMagnitude > 0.01f)
            {
                if (followingMovingQueue)
                {
                    agent.SetFollowingTarget(target);
                }
                else
                {
                    agent.SetMovementTarget(target);
                }
            }
        }
    }
}

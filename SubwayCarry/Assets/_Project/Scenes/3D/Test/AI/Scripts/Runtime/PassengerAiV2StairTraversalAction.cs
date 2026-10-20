using UnityEngine;

namespace SubwayCarry.AI.V2
{
    /// <summary>
    /// 계단 사용을 접근, 혼잡 진입 조절, 진입, 통과, 이탈의 완료 조건으로 실행한다.
    /// 평상시에는 양방향 승객이 현재 형성된 군중 흐름을 따라 연속 진입한다.
    /// </summary>
    [RequireComponent(typeof(PassengerAiV2Agent))]
    public sealed class PassengerAiV2StairTraversalAction : MonoBehaviour
    {
        private enum TraversalPhase
        {
            Approach,
            WaitingForReservation,
            Entering,
            Traversing,
            Leaving,
            ReachingDestination,
            Cooldown
        }

        [SerializeField, Range(2f, 20f)] private float actionUpdateHz = 10f;
        [SerializeField, Min(0.1f)] private float requestDistance = 1.35f;
        [SerializeField, Min(0f)] private float restartDelay = 1.2f;

        private PassengerAiV2Agent agent;
        private PassengerAiV2StairSmartObject stair;
        private PassengerAiV2StairDirection direction;
        private Vector2 journeyStart;
        private Vector2 journeyDestination;
        private Rect worldBounds;
        private TraversalPhase phase;
        private float nextActionUpdateTime;
        private float cooldownUntil;

        public bool IsWaiting => phase == TraversalPhase.WaitingForReservation;
        public bool IsInsideStair => phase == TraversalPhase.Traversing || phase == TraversalPhase.Leaving;

        public void Initialize(
            PassengerAiV2Agent passenger,
            PassengerAiV2StairSmartObject stairObject,
            PassengerAiV2StairDirection travelDirection,
            Vector2 start,
            Vector2 destination,
            Rect allowedWorldBounds,
            float stagger01)
        {
            agent = passenger;
            stair = stairObject;
            direction = travelDirection;
            journeyStart = start;
            journeyDestination = destination;
            worldBounds = allowedWorldBounds;
            nextActionUpdateTime = Time.time + Mathf.Clamp01(stagger01) / Mathf.Max(2f, actionUpdateHz);

            agent.SetAutoLoop(false);
            agent.SetMovementBounds(worldBounds);
            agent.SetEmergentCorridorFlow(stair.FlowInfluenceBounds, Vector2.up);
            agent.TeleportAndSetTarget(journeyStart, stair.GetApproachPoint(direction));
            phase = TraversalPhase.Approach;
        }

        private void OnDestroy()
        {
            if (stair != null && agent != null)
            {
                stair.Cancel(agent);
            }
        }

        private void Update()
        {
            if (agent == null || stair == null || Time.time < nextActionUpdateTime)
            {
                return;
            }

            nextActionUpdateTime = Time.time + 1f / Mathf.Max(2f, actionUpdateHz);
            TickAction();
        }

        private void TickAction()
        {
            switch (phase)
            {
                case TraversalPhase.Approach:
                    if (agent.DistanceToTarget <= requestDistance)
                    {
                        stair.RequestUse(agent, (int)direction, agent.DistanceToTarget);
                        SetTargetIfChanged(stair.GetQueuePosition(agent, direction));
                        phase = TraversalPhase.WaitingForReservation;
                    }
                    break;

                case TraversalPhase.WaitingForReservation:
                    stair.RequestUse(agent, (int)direction, agent.DistanceToTarget);
                    if (stair.HasReservation(agent))
                    {
                        agent.SetMovementTarget(stair.GetEntryPoint(direction));
                        phase = TraversalPhase.Entering;
                    }
                    else
                    {
                        SetTargetIfChanged(stair.GetQueuePosition(agent, direction));
                    }
                    break;

                case TraversalPhase.Entering:
                    if (agent.HasReachedTarget)
                    {
                        stair.MarkEntered(agent);
                        agent.SetMovementBounds(stair.TraversalBounds);
                        agent.SetMovementTarget(stair.GetExitPoint(direction));
                        phase = TraversalPhase.Traversing;
                    }
                    break;

                case TraversalPhase.Traversing:
                    if (agent.HasReachedTarget)
                    {
                        agent.SetMovementBounds(worldBounds);
                        agent.SetMovementTarget(stair.GetReleasePoint(direction));
                        phase = TraversalPhase.Leaving;
                    }
                    break;

                case TraversalPhase.Leaving:
                    if (agent.HasReachedTarget)
                    {
                        stair.Release(agent);
                        agent.ClearEmergentCorridorFlow();
                        agent.SetMovementBounds(worldBounds);
                        agent.SetMovementTarget(journeyDestination);
                        phase = TraversalPhase.ReachingDestination;
                    }
                    break;

                case TraversalPhase.ReachingDestination:
                    if (agent.HasReachedTarget)
                    {
                        agent.SetHoldPosition(true);
                        cooldownUntil = Time.time + restartDelay;
                        phase = TraversalPhase.Cooldown;
                    }
                    break;

                case TraversalPhase.Cooldown:
                    if (Time.time >= cooldownUntil)
                    {
                        stair.Cancel(agent);
                        agent.SetEmergentCorridorFlow(stair.FlowInfluenceBounds, Vector2.up);
                        agent.SetMovementBounds(worldBounds);
                        agent.TeleportAndSetTarget(journeyStart, stair.GetApproachPoint(direction));
                        phase = TraversalPhase.Approach;
                    }
                    break;
            }
        }

        private void SetTargetIfChanged(Vector2 target)
        {
            if ((agent.MovementTarget - target).sqrMagnitude > 0.01f)
            {
                agent.SetMovementTarget(target);
            }
        }
    }
}

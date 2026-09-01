using UnityEngine;

namespace SubwayCarry.AI.V2
{
    /// <summary>
    /// 방향이 맞는 개찰구를 거리와 줄 길이로 선택하고,
    /// 줄서기, 카드 태그, 열린 동일 통로 통과와 해제를 순서대로 수행한다.
    /// </summary>
    [RequireComponent(typeof(PassengerAiV2Agent))]
    public sealed class PassengerAiV2FareGateTraversalAction : MonoBehaviour
    {
        private enum FareGatePhase
        {
            Queueing,
            MovingToReader,
            TaggingCard,
            Passing,
            Leaving,
            ReachingDestination,
            Cooldown
        }

        [SerializeField, Range(2f, 20f)] private float actionUpdateHz = 12f;
        [SerializeField, Min(0.1f)] private float cardTagDuration = 0.65f;
        [SerializeField, Min(0f)] private float restartDelay = 1.2f;
        [SerializeField, Min(0f)] private float queueLoadCost = 1.35f;

        private PassengerAiV2Agent agent;
        private PassengerAiV2FareGateSmartObject[] gates;
        private PassengerAiV2FareGateSmartObject selectedGate;
        private PassengerAiV2FareGateDirection direction;
        private Vector2 journeyStart;
        private Vector2 journeyDestination;
        private Rect worldBounds;
        private FareGatePhase phase;
        private float nextActionUpdateTime;
        private float phaseCompleteTime;

        public bool IsWaiting => phase == FareGatePhase.Queueing
            && selectedGate != null
            && !selectedGate.HasReservation(agent);
        public bool IsTaggingCard => phase == FareGatePhase.TaggingCard;
        public bool IsPassing => phase == FareGatePhase.Passing || phase == FareGatePhase.Leaving;

        public void Initialize(
            PassengerAiV2Agent passenger,
            PassengerAiV2FareGateSmartObject[] availableGates,
            PassengerAiV2FareGateDirection travelDirection,
            Vector2 start,
            Vector2 destination,
            Rect allowedWorldBounds,
            float stagger01)
        {
            agent = passenger;
            gates = availableGates;
            direction = travelDirection;
            journeyStart = start;
            journeyDestination = destination;
            worldBounds = allowedWorldBounds;
            nextActionUpdateTime = Time.time
                + Mathf.Clamp01(stagger01) / Mathf.Max(2f, actionUpdateHz);

            agent.SetAutoLoop(false);
            agent.SetMovementBounds(worldBounds);
            BeginJourney();
        }

        private void OnDestroy()
        {
            if (selectedGate != null && agent != null)
            {
                selectedGate.Cancel(agent);
            }
        }

        private void Update()
        {
            if (agent == null || Time.time < nextActionUpdateTime)
            {
                return;
            }

            nextActionUpdateTime = Time.time + 1f / Mathf.Max(2f, actionUpdateHz);
            TickAction();
        }

        private void BeginJourney()
        {
            selectedGate = SelectGate();
            if (selectedGate == null)
            {
                agent.SetHoldPosition(true);
                return;
            }

            agent.TeleportAndSetTarget(journeyStart, selectedGate.GetApproachPoint());
            selectedGate.RequestUse(
                agent,
                (int)direction,
                Vector2.Distance(agent.Position, selectedGate.GetApproachPoint()));
            SetTargetIfChanged(selectedGate.GetQueuePosition(agent));
            phase = FareGatePhase.Queueing;
        }

        private void TickAction()
        {
            if (selectedGate == null)
            {
                BeginJourney();
                return;
            }

            switch (phase)
            {
                case FareGatePhase.Queueing:
                    selectedGate.RequestUse(
                        agent,
                        (int)direction,
                        Vector2.Distance(agent.Position, selectedGate.GetApproachPoint()));
                    if (selectedGate.HasReservation(agent))
                    {
                        agent.SetMovementTarget(selectedGate.GetReaderPoint());
                        phase = FareGatePhase.MovingToReader;
                    }
                    else
                    {
                        SetTargetIfChanged(selectedGate.GetQueuePosition(agent));
                    }
                    break;

                case FareGatePhase.MovingToReader:
                    if (agent.HasReachedTarget)
                    {
                        agent.SetHoldPosition(true);
                        phaseCompleteTime = Time.time + cardTagDuration;
                        phase = FareGatePhase.TaggingCard;
                    }
                    break;

                case FareGatePhase.TaggingCard:
                    if (Time.time >= phaseCompleteTime && selectedGate.TagCard(agent))
                    {
                        agent.SetHoldPosition(false);
                        agent.SetMovementTarget(selectedGate.GetPassPoint());
                        phase = FareGatePhase.Passing;
                    }
                    break;

                case FareGatePhase.Passing:
                    if (agent.HasReachedTarget && selectedGate.MarkPassed(agent))
                    {
                        // 승객의 몸 전체가 차단봉을 지난 시점에 통로를 바로 닫고
                        // 다음 대기자에게 예약을 넘긴다. 이탈점까지 걷는 시간은 점유하지 않는다.
                        selectedGate.Release(agent);
                        agent.SetMovementTarget(selectedGate.GetReleasePoint());
                        phase = FareGatePhase.Leaving;
                    }
                    break;

                case FareGatePhase.Leaving:
                    if (agent.HasReachedTarget)
                    {
                        agent.SetMovementTarget(journeyDestination);
                        phase = FareGatePhase.ReachingDestination;
                    }
                    break;

                case FareGatePhase.ReachingDestination:
                    if (agent.HasReachedTarget)
                    {
                        agent.SetHoldPosition(true);
                        phaseCompleteTime = Time.time + restartDelay;
                        phase = FareGatePhase.Cooldown;
                    }
                    break;

                case FareGatePhase.Cooldown:
                    if (Time.time >= phaseCompleteTime)
                    {
                        selectedGate.Cancel(agent);
                        BeginJourney();
                    }
                    break;
            }
        }

        private PassengerAiV2FareGateSmartObject SelectGate()
        {
            PassengerAiV2FareGateSmartObject best = null;
            float bestScore = float.PositiveInfinity;
            if (gates == null)
            {
                return null;
            }

            for (int i = 0; i < gates.Length; i++)
            {
                PassengerAiV2FareGateSmartObject gate = gates[i];
                if (gate == null || !gate.IsCompatible(direction))
                {
                    continue;
                }

                float distance = Vector2.Distance(journeyStart, gate.GetApproachPoint());
                float score = distance + gate.QueueLoad * queueLoadCost;
                if (score < bestScore)
                {
                    best = gate;
                    bestScore = score;
                }
            }

            return best;
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

using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI.V2
{
    /// <summary>
    /// 현재 여정 단계 안에서 좌석, 입석과 기대기 점수를 비교하고,
    /// 하차 준비 사건이 오면 기존 자리를 반납한 뒤 문 준비 위치를 예약한다.
    /// </summary>
    [RequireComponent(typeof(PassengerAiV2Agent))]
    public sealed class PassengerAiV2TrainInteriorAction : MonoBehaviour
    {
        private enum InteriorActionPhase
        {
            ChoosingActivity,
            MovingToActivity,
            UsingActivity,
            ChoosingDoorPosition,
            MovingToDoorPosition,
            Prepared,
            WaitingReset
        }

        [SerializeField, Range(2f, 8f)] private float utilityUpdateHz = 4f;
        [SerializeField, Min(0.1f)] private float retryDelay = 0.45f;

        private PassengerAiV2Agent agent;
        private PassengerAiV2TrainInteriorEnvironment environment;
        private PassengerAiV2RuntimePersonality personality;
        private PassengerAiV2InteriorSpotSmartObject selectedSpot;
        private Vector2 journeyStart;
        private Rect worldBounds;
        private Vector3 defaultScale;
        private InteriorActionPhase phase;
        private int activeCycle;
        private float nextActionUpdateTime;

        public bool IsPrepared => phase == InteriorActionPhase.Prepared;
        public bool IsUsingActivity => phase == InteriorActionPhase.UsingActivity;
        public float LastUtilityScore { get; private set; }
        public PassengerAiV2InteriorSpotKind SelectedKind => selectedSpot != null
            ? selectedSpot.Kind
            : PassengerAiV2InteriorSpotKind.Stand;

        public void Initialize(
            PassengerAiV2Agent passenger,
            PassengerAiV2TrainInteriorEnvironment trainEnvironment,
            PassengerAiV2RuntimePersonality runtimePersonality,
            Vector2 start,
            Rect allowedBounds,
            float stagger01)
        {
            agent = passenger;
            environment = trainEnvironment;
            personality = runtimePersonality;
            journeyStart = start;
            worldBounds = allowedBounds;
            defaultScale = agent.transform.localScale;
            nextActionUpdateTime = Time.time
                + Mathf.Clamp01(stagger01) / Mathf.Max(2f, utilityUpdateHz);

            agent.SetAutoLoop(false);
            agent.SetMovementBounds(worldBounds);
            BeginCycle();
        }

        private void OnDestroy()
        {
            ReleaseSelectedSpot();
        }

        private void Update()
        {
            if (agent == null || environment == null || Time.time < nextActionUpdateTime)
            {
                return;
            }

            nextActionUpdateTime = Time.time + 1f / Mathf.Max(2f, utilityUpdateHz);
            if (environment.CycleIndex != activeCycle)
            {
                BeginCycle();
            }

            if (environment.Phase == PassengerAiV2TrainInteriorPhase.Resetting)
            {
                EnterResetWait();
                return;
            }

            if (environment.ShouldPrepareToAlight
                && phase != InteriorActionPhase.ChoosingDoorPosition
                && phase != InteriorActionPhase.MovingToDoorPosition
                && phase != InteriorActionPhase.Prepared)
            {
                BeginPrepareToAlight();
            }

            TickAction();
        }

        private void BeginCycle()
        {
            ReleaseSelectedSpot();
            activeCycle = environment.CycleIndex;
            ResetPosture();
            agent.SetMovementBounds(worldBounds);
            agent.SetMovementSpeedScale(1f);
            agent.TeleportAndSetTarget(journeyStart, journeyStart);
            phase = InteriorActionPhase.ChoosingActivity;
            nextActionUpdateTime = Time.time;
        }

        private void TickAction()
        {
            switch (phase)
            {
                case InteriorActionPhase.ChoosingActivity:
                    TryChooseActivity();
                    break;

                case InteriorActionPhase.MovingToActivity:
                    if (selectedSpot == null || !selectedSpot.HasReservation(agent))
                    {
                        RetryActivityDecision();
                    }
                    else if (HasReachedReservedSpot())
                    {
                        SnapToReservedSpot();
                        if (selectedSpot.MarkOccupied(agent))
                        {
                            ApplyPosture(selectedSpot.Kind);
                            agent.SetHoldPosition(true);
                            phase = InteriorActionPhase.UsingActivity;
                        }
                        else
                        {
                            RetryActivityDecision();
                        }
                    }
                    break;

                case InteriorActionPhase.UsingActivity:
                    break;

                case InteriorActionPhase.ChoosingDoorPosition:
                    TryChooseDoorPosition();
                    break;

                case InteriorActionPhase.MovingToDoorPosition:
                    if (selectedSpot == null || !selectedSpot.HasReservation(agent))
                    {
                        RetryDoorDecision();
                    }
                    else if (HasReachedReservedSpot())
                    {
                        SnapToReservedSpot();
                        if (selectedSpot.MarkOccupied(agent))
                        {
                            agent.SetHoldPosition(true);
                            environment.ReportPrepared(agent);
                            phase = InteriorActionPhase.Prepared;
                        }
                        else
                        {
                            RetryDoorDecision();
                        }
                    }
                    break;

                case InteriorActionPhase.Prepared:
                case InteriorActionPhase.WaitingReset:
                    break;
            }
        }

        private bool HasReachedReservedSpot()
        {
            return selectedSpot != null
                && (agent.HasReachedTarget
                    || (agent.Position - selectedSpot.UsePosition).sqrMagnitude <= 0.36f);
        }

        private void SnapToReservedSpot()
        {
            if (selectedSpot != null)
            {
                agent.TeleportAndSetTarget(selectedSpot.UsePosition, selectedSpot.UsePosition);
            }
        }

        private void TryChooseActivity()
        {
            PassengerAiV2InteriorSpotSmartObject bestSpot = null;
            float bestScore = float.NegativeInfinity;
            IReadOnlyList<PassengerAiV2InteriorSpotSmartObject> spots = environment.ActivitySpots;
            for (int i = 0; i < spots.Count; i++)
            {
                PassengerAiV2InteriorSpotSmartObject candidate = spots[i];
                if (candidate == null || !candidate.IsAvailableFor(agent))
                {
                    continue;
                }

                float score = ScoreActivity(candidate);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSpot = candidate;
                }
            }

            environment.ReportDecision();
            if (bestSpot == null
                || !bestSpot.RequestUse(
                    agent,
                    0,
                    Vector2.Distance(agent.Position, bestSpot.UsePosition)))
            {
                RetryActivityDecision();
                return;
            }

            selectedSpot = bestSpot;
            LastUtilityScore = bestScore;
            agent.SetMovementTarget(selectedSpot.UsePosition);
            phase = InteriorActionPhase.MovingToActivity;
        }

        private float ScoreActivity(PassengerAiV2InteriorSpotSmartObject spot)
        {
            float score;
            switch (spot.Kind)
            {
                case PassengerAiV2InteriorSpotKind.Seat:
                    score = 0.45f
                        + personality.SeatPreference * 0.5f
                        + personality.FatigueSensitivity * 0.08f;
                    break;

                case PassengerAiV2InteriorSpotKind.Lean:
                    score = 0.42f
                        + (1f - personality.SeatPreference) * 0.22f
                        + personality.DoorProximityPreference * 0.08f;
                    break;

                default:
                    score = 0.38f
                        + personality.DoorProximityPreference * 0.18f
                        + personality.Mobility * 0.06f;
                    break;
            }

            score += spot.Comfort * 0.18f;
            score -= Vector2.Distance(agent.Position, spot.UsePosition) * 0.03f;
            score -= spot.ExitPreparationCost
                * personality.DoorProximityPreference
                * 0.12f;
            return score;
        }

        private void BeginPrepareToAlight()
        {
            ReleaseSelectedSpot();
            ResetPosture();
            agent.SetMovementSpeedScale(1.08f);
            phase = InteriorActionPhase.ChoosingDoorPosition;
            nextActionUpdateTime = Time.time;
        }

        private void TryChooseDoorPosition()
        {
            PassengerAiV2InteriorSpotSmartObject bestSpot = null;
            float bestScore = float.NegativeInfinity;
            IReadOnlyList<PassengerAiV2InteriorSpotSmartObject> spots = environment.DoorPrepareSpots;
            for (int i = 0; i < spots.Count; i++)
            {
                PassengerAiV2InteriorSpotSmartObject candidate = spots[i];
                if (candidate == null || !candidate.IsAvailableFor(agent))
                {
                    continue;
                }

                float score = -Vector2.Distance(agent.Position, candidate.UsePosition)
                    + candidate.Comfort * 0.25f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSpot = candidate;
                }
            }

            environment.ReportDecision();
            if (bestSpot == null
                || !bestSpot.RequestUse(
                    agent,
                    0,
                    Vector2.Distance(agent.Position, bestSpot.UsePosition)))
            {
                RetryDoorDecision();
                return;
            }

            selectedSpot = bestSpot;
            LastUtilityScore = bestScore;
            agent.SetMovementTarget(selectedSpot.UsePosition);
            phase = InteriorActionPhase.MovingToDoorPosition;
        }

        private void RetryActivityDecision()
        {
            ReleaseSelectedSpot();
            environment.ReportReplan();
            phase = InteriorActionPhase.ChoosingActivity;
            nextActionUpdateTime = Time.time + retryDelay;
        }

        private void RetryDoorDecision()
        {
            ReleaseSelectedSpot();
            environment.ReportReplan();
            phase = InteriorActionPhase.ChoosingDoorPosition;
            nextActionUpdateTime = Time.time + retryDelay;
        }

        private void EnterResetWait()
        {
            if (phase == InteriorActionPhase.WaitingReset)
            {
                return;
            }

            ReleaseSelectedSpot();
            ResetPosture();
            agent.SetMovementSpeedScale(1f);
            agent.SetHoldPosition(true);
            phase = InteriorActionPhase.WaitingReset;
        }

        private void ReleaseSelectedSpot()
        {
            if (selectedSpot != null && agent != null)
            {
                selectedSpot.Release(agent);
            }

            selectedSpot = null;
        }

        private void ApplyPosture(PassengerAiV2InteriorSpotKind kind)
        {
            ResetPosture();
            if (kind == PassengerAiV2InteriorSpotKind.Seat)
            {
                agent.transform.localScale = new Vector3(
                    defaultScale.x,
                    defaultScale.y * 0.72f,
                    defaultScale.z);
            }
            else if (kind == PassengerAiV2InteriorSpotKind.Lean)
            {
                float angle = agent.Position.x < 0f ? -9f : 9f;
                agent.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private void ResetPosture()
        {
            if (agent == null)
            {
                return;
            }

            agent.transform.localScale = defaultScale;
            agent.transform.localRotation = Quaternion.identity;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI.V2
{
    public enum PassengerAiV2TrainInteriorPhase
    {
        Settling,
        Riding,
        PrepareToAlight,
        DoorOpen,
        Resetting
    }

    /// <summary>
    /// 객차 내부 Smart Object 목록과 운행 주기만 소유한다.
    /// 개별 승객 대신 낮은 주기로 점유와 검증 수치를 집계한다.
    /// </summary>
    public sealed class PassengerAiV2TrainInteriorEnvironment : MonoBehaviour
    {
        [SerializeField, Range(2f, 20f)] private float updateHz = 10f;
        [SerializeField, Min(1f)] private float settlingDuration = 4f;
        [SerializeField, Min(1f)] private float ridingDuration = 9f;
        [SerializeField, Min(1f)] private float prepareDuration = 10f;
        [SerializeField, Min(1f)] private float doorOpenDuration = 3f;
        [SerializeField, Min(0.2f)] private float resetDuration = 1f;

        private readonly List<PassengerAiV2InteriorSpotSmartObject> activitySpots =
            new List<PassengerAiV2InteriorSpotSmartObject>(16);
        private readonly List<PassengerAiV2InteriorSpotSmartObject> doorPrepareSpots =
            new List<PassengerAiV2InteriorSpotSmartObject>(12);
        private readonly HashSet<int> preparedAgents = new HashSet<int>();
        private float phaseStartedAt;
        private float nextUpdateTime;
        private int currentPeakActivityCount;
        private bool currentUsedAllActivityKinds;

        public PassengerAiV2TrainInteriorPhase Phase { get; private set; }
        public int CycleIndex { get; private set; } = 1;
        public IReadOnlyList<PassengerAiV2InteriorSpotSmartObject> ActivitySpots => activitySpots;
        public IReadOnlyList<PassengerAiV2InteriorSpotSmartObject> DoorPrepareSpots => doorPrepareSpots;
        public int SeatOccupiedCount { get; private set; }
        public int StandOccupiedCount { get; private set; }
        public int LeanOccupiedCount { get; private set; }
        public int PreparedCount => preparedAgents.Count;
        public int DecisionCount { get; private set; }
        public int ReplanCount { get; private set; }
        public int InvalidReservationCount { get; private set; }
        public int CompletedCycleCount { get; private set; }
        public int LastCyclePreparedCount { get; private set; }
        public int LastCyclePeakActivityCount { get; private set; }
        public bool LastCycleUsedAllActivityKinds { get; private set; }
        public bool ShouldPrepareToAlight =>
            Phase == PassengerAiV2TrainInteriorPhase.PrepareToAlight
            || Phase == PassengerAiV2TrainInteriorPhase.DoorOpen;

        private void Awake()
        {
            Phase = PassengerAiV2TrainInteriorPhase.Settling;
            phaseStartedAt = Time.time;
        }

        private void Update()
        {
            if (Time.time < nextUpdateTime)
            {
                return;
            }

            nextUpdateTime = Time.time + 1f / Mathf.Max(2f, updateHz);
            RefreshMetrics();
            TickCycle();
        }

        public void RegisterSpot(PassengerAiV2InteriorSpotSmartObject spot)
        {
            if (spot == null)
            {
                return;
            }

            if (spot.Kind == PassengerAiV2InteriorSpotKind.DoorPrepare)
            {
                doorPrepareSpots.Add(spot);
            }
            else
            {
                activitySpots.Add(spot);
            }
        }

        public void ConfigurePhaseDurations(
            float settlingSeconds,
            float ridingSeconds,
            float prepareSeconds,
            float doorOpenSeconds,
            float resetSeconds)
        {
            settlingDuration = Mathf.Max(1f, settlingSeconds);
            ridingDuration = Mathf.Max(1f, ridingSeconds);
            prepareDuration = Mathf.Max(1f, prepareSeconds);
            doorOpenDuration = Mathf.Max(1f, doorOpenSeconds);
            resetDuration = Mathf.Max(0.2f, resetSeconds);
        }

        public void ReportDecision()
        {
            DecisionCount++;
        }

        public void ReportReplan()
        {
            ReplanCount++;
        }

        public void ReportPrepared(PassengerAiV2Agent agent)
        {
            if (agent != null)
            {
                preparedAgents.Add(agent.GetInstanceID());
            }
        }

        private void TickCycle()
        {
            float elapsed = Time.time - phaseStartedAt;
            switch (Phase)
            {
                case PassengerAiV2TrainInteriorPhase.Settling:
                    if (elapsed >= settlingDuration)
                    {
                        SetPhase(PassengerAiV2TrainInteriorPhase.Riding);
                    }
                    break;

                case PassengerAiV2TrainInteriorPhase.Riding:
                    if (elapsed >= ridingDuration)
                    {
                        SetPhase(PassengerAiV2TrainInteriorPhase.PrepareToAlight);
                    }
                    break;

                case PassengerAiV2TrainInteriorPhase.PrepareToAlight:
                    if (elapsed >= prepareDuration)
                    {
                        SetPhase(PassengerAiV2TrainInteriorPhase.DoorOpen);
                    }
                    break;

                case PassengerAiV2TrainInteriorPhase.DoorOpen:
                    if (elapsed >= doorOpenDuration)
                    {
                        SetPhase(PassengerAiV2TrainInteriorPhase.Resetting);
                    }
                    break;

                case PassengerAiV2TrainInteriorPhase.Resetting:
                    if (elapsed >= resetDuration)
                    {
                        ResetCycle();
                    }
                    break;
            }
        }

        private void ResetCycle()
        {
            LastCyclePreparedCount = preparedAgents.Count;
            LastCyclePeakActivityCount = currentPeakActivityCount;
            LastCycleUsedAllActivityKinds = currentUsedAllActivityKinds;
            CompletedCycleCount++;
            currentPeakActivityCount = 0;
            currentUsedAllActivityKinds = false;

            for (int i = 0; i < activitySpots.Count; i++)
            {
                activitySpots[i].ForceReset();
            }

            for (int i = 0; i < doorPrepareSpots.Count; i++)
            {
                doorPrepareSpots[i].ForceReset();
            }

            preparedAgents.Clear();
            CycleIndex++;
            SetPhase(PassengerAiV2TrainInteriorPhase.Settling);
        }

        private void RefreshMetrics()
        {
            SeatOccupiedCount = 0;
            StandOccupiedCount = 0;
            LeanOccupiedCount = 0;
            InvalidReservationCount = 0;
            for (int i = 0; i < activitySpots.Count; i++)
            {
                PassengerAiV2InteriorSpotSmartObject spot = activitySpots[i];
                InvalidReservationCount += spot.InvalidReservationCount;
                if (!spot.IsOccupied)
                {
                    continue;
                }

                switch (spot.Kind)
                {
                    case PassengerAiV2InteriorSpotKind.Seat:
                        SeatOccupiedCount++;
                        break;
                    case PassengerAiV2InteriorSpotKind.Stand:
                        StandOccupiedCount++;
                        break;
                    case PassengerAiV2InteriorSpotKind.Lean:
                        LeanOccupiedCount++;
                        break;
                }
            }

            for (int i = 0; i < doorPrepareSpots.Count; i++)
            {
                InvalidReservationCount += doorPrepareSpots[i].InvalidReservationCount;
            }

            int occupiedActivityCount =
                SeatOccupiedCount + StandOccupiedCount + LeanOccupiedCount;
            currentPeakActivityCount = Mathf.Max(currentPeakActivityCount, occupiedActivityCount);
            currentUsedAllActivityKinds |= SeatOccupiedCount > 0
                && StandOccupiedCount > 0
                && LeanOccupiedCount > 0;
        }

        private void SetPhase(PassengerAiV2TrainInteriorPhase nextPhase)
        {
            Phase = nextPhase;
            phaseStartedAt = Time.time;
        }
    }
}

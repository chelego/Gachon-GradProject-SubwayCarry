using UnityEngine;

namespace SubwayCarry.AI.V2
{
    /// <summary>
    /// 목적지역 출구 도달 목표를 고정된 HTN 단계로 한 번 분해한다.
    /// Utility는 각 단계 안의 시설과 행동 선택에만 사용한다.
    /// </summary>
    public sealed class PassengerAiV2FullJourneyPlanner : MonoBehaviour
    {
        private static readonly PassengerAiV2FullJourneyStage[] JourneyPlan =
        {
            PassengerAiV2FullJourneyStage.EnterOriginStation,
            PassengerAiV2FullJourneyStage.PassOriginEntryGate,
            PassengerAiV2FullJourneyStage.ReachOriginPlatform,
            PassengerAiV2FullJourneyStage.WaitForOriginTrain,
            PassengerAiV2FullJourneyStage.BoardOriginTrain,
            PassengerAiV2FullJourneyStage.ChooseTrainActivity,
            PassengerAiV2FullJourneyStage.RideAndPrepareToAlight,
            PassengerAiV2FullJourneyStage.AlightAtDestination,
            PassengerAiV2FullJourneyStage.ReachDestinationConcourse,
            PassengerAiV2FullJourneyStage.PassDestinationExitGate,
            PassengerAiV2FullJourneyStage.LeaveDestinationStation,
            PassengerAiV2FullJourneyStage.Completed
        };

        private PassengerAiV2JourneyBlackboard blackboard;
        private int planIndex;

        public int PlanRevision { get; private set; }
        public int CurrentPlanIndex => planIndex;
        public int StageCount => JourneyPlan.Length;

        public void Initialize(PassengerAiV2JourneyBlackboard journeyBlackboard)
        {
            blackboard = journeyBlackboard;
            planIndex = 0;
            PlanRevision++;
            blackboard.BeginJourney();
        }

        public bool CompleteCurrentStage(PassengerAiV2FullJourneyStage completedStage)
        {
            if (blackboard == null
                || planIndex >= JourneyPlan.Length
                || JourneyPlan[planIndex] != completedStage)
            {
                return false;
            }

            if (planIndex < JourneyPlan.Length - 1)
            {
                planIndex++;
                blackboard.SetStage(JourneyPlan[planIndex]);
            }

            return true;
        }

        public void ReportRecoverableFailure(string reason)
        {
            if (blackboard != null)
            {
                blackboard.RecordRecovery(reason);
            }
        }
    }
}

using UnityEngine;

namespace SubwayCarry.AI.V2
{
    public enum PassengerAiV2FullJourneyStage
    {
        EnterOriginStation,
        PassOriginEntryGate,
        ReachOriginPlatform,
        WaitForOriginTrain,
        BoardOriginTrain,
        ChooseTrainActivity,
        RideAndPrepareToAlight,
        AlightAtDestination,
        ReachDestinationConcourse,
        PassDestinationExitGate,
        LeaveDestinationStation,
        Completed
    }

    /// <summary>
    /// Scene과 공간이 바뀌어도 한 승객에게 계속 남아 있는 전체 여정 상태다.
    /// </summary>
    public sealed class PassengerAiV2JourneyBlackboard : MonoBehaviour
    {
        public PassengerAiV2FullJourneyStage CurrentStage { get; private set; }
        public bool EntryCardTagged { get; private set; }
        public bool BoardedTrain { get; private set; }
        public bool UsedTrainActivity { get; private set; }
        public bool PreparedToAlight { get; private set; }
        public bool AlightedTrain { get; private set; }
        public bool ExitCardTagged { get; private set; }
        public bool ReachedDestinationExit { get; private set; }
        public int StageTransitionCount { get; private set; }
        public int RecoveryCount { get; private set; }
        public int CompletedJourneyCount { get; private set; }
        public float StageStartedAt { get; private set; }
        public string LastRecoveryReason { get; private set; } = string.Empty;

        public void BeginJourney()
        {
            EntryCardTagged = false;
            BoardedTrain = false;
            UsedTrainActivity = false;
            PreparedToAlight = false;
            AlightedTrain = false;
            ExitCardTagged = false;
            ReachedDestinationExit = false;
            StageTransitionCount = 0;
            RecoveryCount = 0;
            LastRecoveryReason = string.Empty;
            SetStage(PassengerAiV2FullJourneyStage.EnterOriginStation);
        }

        public void SetStage(PassengerAiV2FullJourneyStage stage)
        {
            CurrentStage = stage;
            StageStartedAt = Time.time;
            StageTransitionCount++;
        }

        public void MarkEntryCardTagged()
        {
            EntryCardTagged = true;
        }

        public void MarkBoardedTrain()
        {
            BoardedTrain = true;
        }

        public void MarkTrainActivityUsed()
        {
            UsedTrainActivity = true;
        }

        public void MarkPreparedToAlight()
        {
            PreparedToAlight = true;
        }

        public void MarkAlightedTrain()
        {
            AlightedTrain = true;
        }

        public void MarkExitCardTagged()
        {
            ExitCardTagged = true;
        }

        public void MarkJourneyCompleted()
        {
            ReachedDestinationExit = true;
            CompletedJourneyCount++;
        }

        public void RecordRecovery(string reason)
        {
            RecoveryCount++;
            LastRecoveryReason = reason ?? string.Empty;
            StageStartedAt = Time.time;
        }
    }
}

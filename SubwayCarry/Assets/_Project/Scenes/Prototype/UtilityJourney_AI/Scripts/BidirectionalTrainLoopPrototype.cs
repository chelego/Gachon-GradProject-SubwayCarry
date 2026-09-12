using System.Collections;
using SubwayCarry.AI;
using UnityEngine;
using UnityEngine.UI;

namespace SubwayCarry.AI.UtilityJourney
{
    [DisallowMultipleComponent]
    public sealed class BidirectionalTrainLoopPrototype : MonoBehaviour
    {
        [SerializeField] private Transform downboundTrain;
        [SerializeField] private Transform upboundTrain;
        [SerializeField] private TrainDoorController[] downboundPlatformDoors;
        [SerializeField] private TrainDoorController[] upboundPlatformDoors;
        [SerializeField] private Text statusText;
        [SerializeField, Min(0f)] private float doorOpeningDelaySeconds = 2f;
        [SerializeField, Min(0.1f)] private float doorOpenSeconds = 10f;
        [SerializeField, Min(0f)] private float postCloseDelaySeconds = 2f;
        [SerializeField, Min(1f)] private float interstationTravelSeconds = 30f;
        [SerializeField, Min(0.5f)] private float offscreenApproachSeconds = 6f;
        [SerializeField, Min(0f)] private float offscreenHiddenSeconds = 1f;
        [SerializeField] private Vector3 stationALower;
        [SerializeField] private Vector3 stationBLower;
        [SerializeField] private Vector3 stationAUpper;
        [SerializeField] private Vector3 stationBUpper;
        [SerializeField] private float offscreenX = 90f;
        [SerializeField, Min(0.5f)] private float maximumPassengerTransferHoldSeconds = 8f;

        private readonly ServiceState downboundState = new ServiceState();
        private readonly ServiceState upboundState = new ServiceState();
        private string passengerJourneyStatus = "Passenger: entering Station A";
        private int downboundPassengerTransferHolds;
        private int upboundPassengerTransferHolds;
        private float nextUiUpdateTime;

        public float InterstationTravelSeconds => interstationTravelSeconds;
        public Transform DownboundTrain => downboundTrain;
        public TrainDoorController[] DownboundPlatformDoors => downboundPlatformDoors;
        public bool IsDownboundAtStationA => IsTrainAt(downboundTrain, stationALower);
        public bool IsDownboundAtStationB => IsTrainAt(downboundTrain, stationBLower);
        public bool AreDownboundDoorsOpen =>
            AreDoorsInState(downboundPlatformDoors, true);
        public Transform UpboundTrain => upboundTrain;
        public TrainDoorController[] UpboundPlatformDoors => upboundPlatformDoors;
        public bool IsUpboundAtStationA => IsTrainAt(upboundTrain, stationAUpper);
        public bool IsUpboundAtStationB => IsTrainAt(upboundTrain, stationBUpper);
        public bool AreUpboundDoorsOpen =>
            AreDoorsInState(upboundPlatformDoors, true);

        public void SetPassengerJourneyStatus(string status)
        {
            passengerJourneyStatus = status ?? string.Empty;
        }

        public void BeginDownboundPassengerTransfer()
        {
            downboundPassengerTransferHolds++;
        }

        public void EndDownboundPassengerTransfer()
        {
            downboundPassengerTransferHolds = Mathf.Max(
                0,
                downboundPassengerTransferHolds - 1);
        }

        public void BeginPassengerTransfer(UtilityJourneyDirection direction)
        {
            if (direction == UtilityJourneyDirection.Upbound)
            {
                upboundPassengerTransferHolds++;
            }
            else
            {
                downboundPassengerTransferHolds++;
            }
        }

        public void EndPassengerTransfer(UtilityJourneyDirection direction)
        {
            if (direction == UtilityJourneyDirection.Upbound)
            {
                upboundPassengerTransferHolds = Mathf.Max(
                    0,
                    upboundPassengerTransferHolds - 1);
            }
            else
            {
                downboundPassengerTransferHolds = Mathf.Max(
                    0,
                    downboundPassengerTransferHolds - 1);
            }
        }

        public float GetSecondsUntilDoorClose(UtilityJourneyDirection direction)
        {
            bool upbound = direction == UtilityJourneyDirection.Upbound;
            TrainDoorController[] doors = upbound
                ? upboundPlatformDoors
                : downboundPlatformDoors;
            if (!AreDoorsInState(doors, true))
            {
                return float.PositiveInfinity;
            }

            ServiceState state = upbound ? upboundState : downboundState;
            return state.RemainingSeconds > 0f
                ? state.RemainingSeconds
                : doorOpenSeconds;
        }

        public void Configure(
            Transform downbound,
            TrainDoorController[] downboundDoors,
            Transform upbound,
            TrainDoorController[] upboundDoors,
            Text display,
            Vector3 aLower,
            Vector3 bLower,
            Vector3 aUpper,
            Vector3 bUpper,
            float travelSeconds,
            float approachSeconds,
            float outsideX,
            float openingDelaySeconds,
            float openSeconds,
            float closeDelaySeconds,
            float hiddenSeconds)
        {
            downboundTrain = downbound;
            downboundPlatformDoors = downboundDoors;
            upboundTrain = upbound;
            upboundPlatformDoors = upboundDoors;
            statusText = display;
            stationALower = aLower;
            stationBLower = bLower;
            stationAUpper = aUpper;
            stationBUpper = bUpper;
            interstationTravelSeconds = Mathf.Max(1f, travelSeconds);
            offscreenApproachSeconds = Mathf.Max(0.5f, approachSeconds);
            offscreenX = Mathf.Abs(outsideX);
            doorOpeningDelaySeconds = Mathf.Max(0f, openingDelaySeconds);
            doorOpenSeconds = Mathf.Max(0.1f, openSeconds);
            postCloseDelaySeconds = Mathf.Max(0f, closeDelaySeconds);
            offscreenHiddenSeconds = Mathf.Max(0f, hiddenSeconds);
        }

        private void Start()
        {
            if (downboundTrain == null || upboundTrain == null)
            {
                Debug.LogError(
                    "[BidirectionalTrainLoop] Both train roots must be assigned.",
                    this);
                enabled = false;
                return;
            }

            downboundTrain.position = stationALower;
            upboundTrain.position = stationBUpper;
            SetDoorsOpen(downboundPlatformDoors, false);
            SetDoorsOpen(upboundPlatformDoors, false);
            StartCoroutine(RunService(
                downboundTrain,
                downboundPlatformDoors,
                stationALower,
                stationBLower,
                new Vector3(-offscreenX, stationALower.y, stationALower.z),
                new Vector3(offscreenX, stationBLower.y, stationBLower.z),
                "DOWNBOUND A > B",
                downboundState));
            StartCoroutine(RunService(
                upboundTrain,
                upboundPlatformDoors,
                stationBUpper,
                stationAUpper,
                new Vector3(offscreenX, stationBUpper.y, stationBUpper.z),
                new Vector3(-offscreenX, stationAUpper.y, stationAUpper.z),
                "UPBOUND B > A",
                upboundState));
        }

        private void Update()
        {
            if (statusText == null)
            {
                return;
            }
            if (Time.unscaledTime < nextUiUpdateTime)
            {
                return;
            }
            nextUiUpdateTime = Time.unscaledTime + 0.2f;

            statusText.text =
                "UTILITY NPCS / FULL A <> B JOURNEYS\n" +
                passengerJourneyStatus + "\n" +
                FormatState(downboundState) + "\n" +
                FormatState(upboundState) + "\n" +
                $"DOORS 2s > OPEN 10s > 2s / A-B TRAVEL {interstationTravelSeconds:0}s";
        }

        private static bool IsTrainAt(Transform train, Vector3 position)
        {
            return train != null &&
                   train.gameObject.activeInHierarchy &&
                   Vector2.Distance(train.position, position) <= 0.08f;
        }

        private IEnumerator RunService(
            Transform train,
            TrainDoorController[] platformDoors,
            Vector3 origin,
            Vector3 destination,
            Vector3 offscreenEntry,
            Vector3 offscreenExit,
            string label,
            ServiceState state)
        {
            state.Label = label;
            train.gameObject.SetActive(true);
            train.position = origin;

            while (true)
            {
                yield return ServiceStop(
                    train,
                    origin,
                    platformDoors,
                    "ORIGIN",
                    state);
                yield return Move(
                    train,
                    origin,
                    destination,
                    interstationTravelSeconds,
                    "BETWEEN STATIONS",
                    state);
                yield return ServiceStop(
                    train,
                    destination,
                    platformDoors,
                    "DESTINATION",
                    state);
                yield return Move(
                    train,
                    destination,
                    offscreenExit,
                    offscreenApproachSeconds,
                    "LEAVING MAP",
                    state);

                train.gameObject.SetActive(false);
                train.position = offscreenEntry;
                yield return WaitPhase(
                    offscreenHiddenSeconds,
                    "FULLY OUTSIDE MAP",
                    state);
                train.gameObject.SetActive(true);

                yield return Move(
                    train,
                    offscreenEntry,
                    origin,
                    offscreenApproachSeconds,
                    "ENTERING ORIGIN",
                    state);
            }
        }

        private IEnumerator ServiceStop(
            Transform train,
            Vector3 position,
            TrainDoorController[] doors,
            string stationPhase,
            ServiceState state)
        {
            train.position = position;
            SetDoorsOpen(doors, false);
            yield return WaitForDoors(
                doors,
                false,
                stationPhase + " / DOORS CLOSING",
                state);
            yield return Hold(
                train,
                position,
                doorOpeningDelaySeconds,
                stationPhase + " / DOORS OPEN IN",
                state);

            SetDoorsOpen(doors, true);
            yield return WaitForDoors(
                doors,
                true,
                stationPhase + " / DOORS OPENING",
                state);
            yield return Hold(
                train,
                position,
                doorOpenSeconds,
                stationPhase + " / DOORS OPEN",
                state);
            if (ReferenceEquals(doors, downboundPlatformDoors) ||
                ReferenceEquals(doors, upboundPlatformDoors))
            {
                yield return HoldForPassengerTransfer(
                    train,
                    position,
                    stationPhase,
                    state,
                    ReferenceEquals(doors, upboundPlatformDoors));
            }

            SetDoorsOpen(doors, false);
            yield return WaitForDoors(
                doors,
                false,
                stationPhase + " / DOORS CLOSING",
                state);
            yield return Hold(
                train,
                position,
                postCloseDelaySeconds,
                stationPhase + " / DEPARTURE IN",
                state);
        }

        private static IEnumerator WaitForDoors(
            TrainDoorController[] doors,
            bool open,
            string phase,
            ServiceState state)
        {
            state.Phase = phase;
            state.RemainingSeconds = 0f;
            while (!AreDoorsInState(doors, open))
            {
                yield return null;
            }
        }

        private static IEnumerator WaitPhase(
            float seconds,
            string phase,
            ServiceState state)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                state.Phase = phase;
                state.RemainingSeconds = Mathf.Max(0f, seconds - elapsed);
                yield return null;
            }
        }

        private static void SetDoorsOpen(
            TrainDoorController[] doors,
            bool open)
        {
            if (doors == null)
            {
                return;
            }

            foreach (TrainDoorController door in doors)
            {
                door?.SetOpen(open);
            }
        }

        private static bool AreDoorsInState(
            TrainDoorController[] doors,
            bool open)
        {
            if (doors == null || doors.Length == 0)
            {
                return true;
            }

            foreach (TrainDoorController door in doors)
            {
                if (door == null)
                {
                    continue;
                }

                if ((open && !door.IsOpen) || (!open && !door.IsClosed))
                {
                    return false;
                }
            }

            return true;
        }

        private static IEnumerator Hold(
            Transform train,
            Vector3 position,
            float seconds,
            string phase,
            ServiceState state)
        {
            train.position = position;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                state.Phase = phase;
                state.RemainingSeconds = Mathf.Max(0f, seconds - elapsed);
                yield return null;
            }
        }

        private IEnumerator HoldForPassengerTransfer(
            Transform train,
            Vector3 position,
            string stationPhase,
            ServiceState state,
            bool upbound)
        {
            float elapsed = 0f;
            float transferWarningSeconds = Mathf.Max(
                18f,
                maximumPassengerTransferHoldSeconds);
            bool overrunReported = false;
            while ((upbound
                       ? upboundPassengerTransferHolds
                       : downboundPassengerTransferHolds) > 0)
            {
                elapsed += Time.deltaTime;
                train.position = position;
                state.Phase = elapsed < transferWarningSeconds
                    ? stationPhase + " / PASSENGER TRANSFER"
                    : stationPhase + " / WAITING FOR PASSENGERS";
                state.RemainingSeconds = Mathf.Max(
                    0f,
                    transferWarningSeconds - elapsed);
                if (!overrunReported && elapsed >= transferWarningSeconds)
                {
                    overrunReported = true;
                    Debug.LogWarning(
                        "[BidirectionalTrainLoop] Departure remains held until all " +
                        "passengers finish boarding or alighting.",
                        this);
                }
                yield return null;
            }
        }

        private static IEnumerator Move(
            Transform train,
            Vector3 from,
            Vector3 to,
            float seconds,
            string phase,
            ServiceState state)
        {
            float elapsed = 0f;
            train.position = from;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                float linear = Mathf.Clamp01(elapsed / seconds);
                float eased = Mathf.SmoothStep(0f, 1f, linear);
                train.position = Vector3.LerpUnclamped(from, to, eased);
                state.Phase = phase;
                state.RemainingSeconds = Mathf.Max(0f, seconds - elapsed);
                yield return null;
            }

            train.position = to;
        }

        private static string FormatState(ServiceState state)
        {
            string label = string.IsNullOrEmpty(state.Label) ? "INITIALIZING" : state.Label;
            string phase = string.IsNullOrEmpty(state.Phase) ? "READY" : state.Phase;
            return $"{label}: {phase} ({state.RemainingSeconds:0.0}s)";
        }

        private sealed class ServiceState
        {
            public string Label;
            public string Phase;
            public float RemainingSeconds;
        }
    }
}

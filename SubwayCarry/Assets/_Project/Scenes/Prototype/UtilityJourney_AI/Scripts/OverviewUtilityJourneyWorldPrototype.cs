using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI.UtilityJourney
{
    /// <summary>
    /// Per-passenger adapter for the whole A-B overview. Every passenger owns
    /// independent trip facts, while trains and narrow traversal locks are shared.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OverviewUtilityJourneyWorldPrototype :
        MonoBehaviour,
        IUtilityJourneyWorld
    {
        private static readonly Dictionary<int, OverviewUtilityJourneyWorldPrototype>
            TraversalOwners =
                new Dictionary<int, OverviewUtilityJourneyWorldPrototype>();

        [SerializeField] private UtilityPassengerBrainPrototype passenger;
        [SerializeField] private BidirectionalTrainLoopPrototype trainService;
        [SerializeField] private Transform activeTrain;
        [SerializeField] private Transform[] originEntranceOutsidePoints;
        [SerializeField] private Transform[] originEntranceSpawns;
        [SerializeField] private UtilityJourneyFacilityPrototype[] destinationExits;
        [SerializeField] private UtilityJourneyDirection requiredDirection =
            UtilityJourneyDirection.Downbound;
        [SerializeField] private string passengerLabel = "Passenger";
        [SerializeField, Min(0f)] private float startupDelaySeconds;
        [SerializeField] private bool repeatJourney = true;
        [SerializeField, Min(0.1f)] private float controlledWalkSpeed = 1.15f;
        [SerializeField, Min(0f)] private float stairTransferPauseSeconds = 0.22f;
        [SerializeField, Min(0f)] private float gatePauseSeconds = 0.16f;
        [SerializeField, Min(0.1f)] private float repeatDelaySeconds = 4f;

        private UtilityJourneyArea currentArea = UtilityJourneyArea.OriginConcourse;
        private bool farePaid;
        private bool onTrain;
        private bool settledInsideTrain;
        private bool transitioning;
        private bool complete;
        private bool trainTransferHeld;
        private bool arrivalTransferHeld;
        private string preferredExitId = string.Empty;
        private Vector3 lastTrainPosition;
        private int activeTraversalKey;
        private float nextStatusUpdateTime;
        private bool hasReportedStatus;
        private bool lastReportedComplete;
        private UtilityJourneyArea lastReportedArea;
        private UtilityJourneyAction lastReportedAction;
        private UtilityPassengerSocialState lastReportedSocialState;
        private string lastReportedProfile = string.Empty;

        public UtilityJourneyArea CurrentArea => currentArea;
        public string CurrentSocialSpaceId
        {
            get
            {
                bool upbound = requiredDirection == UtilityJourneyDirection.Upbound;
                switch (currentArea)
                {
                    case UtilityJourneyArea.OriginConcourse:
                        return upbound ? "B-CONCOURSE" : "A-CONCOURSE";
                    case UtilityJourneyArea.OriginPlatform:
                        return upbound ? "B-UP-PLATFORM" : "A-DOWN-PLATFORM";
                    case UtilityJourneyArea.TrainInterior:
                        return upbound ? "UPBOUND-TRAIN" : "DOWNBOUND-TRAIN";
                    case UtilityJourneyArea.DestinationPlatform:
                        return upbound ? "A-UP-PLATFORM" : "B-DOWN-PLATFORM";
                    case UtilityJourneyArea.DestinationConcourse:
                        return upbound ? "A-CONCOURSE" : "B-CONCOURSE";
                    default:
                        return passengerLabel + "-OUTSIDE";
                }
            }
        }
        public bool IsTransitioning => transitioning;
        public bool IsComplete => complete;
        public bool CompletedFullCycle { get; private set; }
        public int EntryGateTapCount { get; private set; }
        public int ExitGateTapCount { get; private set; }
        public int BoardingCount { get; private set; }
        public int AlightingCount { get; private set; }
        public int CompletedCycleCount { get; private set; }
        public string StartSpawnName { get; private set; } = string.Empty;
        public string PreferredExitId => preferredExitId;

        public void Configure(
            UtilityPassengerBrainPrototype controlledPassenger,
            BidirectionalTrainLoopPrototype service,
            Transform train,
            Transform[] entranceOutsidePoints,
            Transform[] entranceSpawns,
            UtilityJourneyFacilityPrototype[] exits,
            UtilityJourneyDirection direction,
            string label = "Passenger",
            float startupDelay = 0f,
            bool shouldRepeat = true)
        {
            passenger = controlledPassenger;
            trainService = service;
            activeTrain = train;
            originEntranceOutsidePoints = entranceOutsidePoints;
            originEntranceSpawns = entranceSpawns;
            destinationExits = exits;
            requiredDirection = direction;
            passengerLabel = string.IsNullOrWhiteSpace(label) ? "Passenger" : label;
            startupDelaySeconds = Mathf.Max(0f, startupDelay);
            repeatJourney = shouldRepeat;
        }

        public UtilityPassengerFacts GetFacts()
        {
            bool atOrigin;
            bool atDestination;
            bool doorsOpen;
            if (requiredDirection == UtilityJourneyDirection.Upbound)
            {
                atOrigin = trainService != null && trainService.IsUpboundAtStationB;
                atDestination = trainService != null && trainService.IsUpboundAtStationA;
                doorsOpen = trainService != null && trainService.AreUpboundDoorsOpen;
            }
            else
            {
                atOrigin = trainService != null && trainService.IsDownboundAtStationA;
                atDestination = trainService != null && trainService.IsDownboundAtStationB;
                doorsOpen = trainService != null && trainService.AreDownboundDoorsOpen;
            }

            bool trainAtRelevantPlatform = currentArea == UtilityJourneyArea.OriginPlatform
                ? atOrigin
                : currentArea == UtilityJourneyArea.TrainInterior && atDestination;
            return new UtilityPassengerFacts
            {
                Area = currentArea,
                RequiredDirection = requiredDirection,
                FarePaid = farePaid,
                TrainAtPlatform = trainAtRelevantPlatform,
                TrainDoorOpen = trainAtRelevantPlatform && doorsOpen,
                IsOnTrain = onTrain,
                IsSettledInsideTrain = settledInsideTrain,
                DestinationReady = onTrain && atDestination,
                IsTransitioning = transitioning,
                IsComplete = complete,
                PreferredExitId = preferredExitId,
                SecondsUntilTrainArrival = atOrigin ? 0f : 30f,
                SecondsUntilDoorClose = trainAtRelevantPlatform && doorsOpen
                    ? trainService.GetSecondsUntilDoorClose(requiredDirection)
                    : float.PositiveInfinity
            };
        }

        public void ResolveAction(
            UtilityJourneyAction action,
            UtilityJourneyFacilityPrototype facility)
        {
            if (transitioning || complete || facility == null)
            {
                return;
            }

            if (RequiresExclusiveTraversal(action) && !TryAcquireTraversal(facility))
            {
                passenger?.NotifyWorldChanged();
                return;
            }

            facility.RecordUse();
            switch (action)
            {
                case UtilityJourneyAction.TapIn:
                    StartCoroutine(TraverseFareGate(facility, true));
                    break;
                case UtilityJourneyAction.DescendToPlatform:
                    StartCoroutine(TransferStair(facility, UtilityJourneyArea.OriginPlatform));
                    break;
                case UtilityJourneyAction.WaitForTrain:
                    passenger?.NotifyWorldChanged(false);
                    break;
                case UtilityJourneyAction.BoardTrain:
                    UtilityPassengerFacts boardingFacts = GetFacts();
                    if (boardingFacts.TrainAtPlatform && boardingFacts.TrainDoorOpen)
                    {
                        StartCoroutine(BoardTrain(facility));
                    }
                    else
                    {
                        ReleaseTraversal();
                    }
                    break;
                case UtilityJourneyAction.SettleInsideTrain:
                    settledInsideTrain = true;
                    passenger?.NotifyWorldChanged();
                    break;
                case UtilityJourneyAction.RideTrain:
                    passenger?.NotifyWorldChanged(false);
                    break;
                case UtilityJourneyAction.AlightTrain:
                    UtilityPassengerFacts alightFacts = GetFacts();
                    if (alightFacts.DestinationReady && alightFacts.TrainDoorOpen)
                    {
                        StartCoroutine(AlightTrain(facility));
                    }
                    else
                    {
                        ReleaseTraversal();
                    }
                    break;
                case UtilityJourneyAction.AscendToConcourse:
                    StartCoroutine(TransferStair(
                        facility,
                        UtilityJourneyArea.DestinationConcourse));
                    break;
                case UtilityJourneyAction.TapOut:
                    StartCoroutine(TraverseFareGate(facility, false));
                    break;
                case UtilityJourneyAction.LeaveStation:
                    StartCoroutine(LeaveStation(facility));
                    break;
            }
        }

        private void Awake()
        {
            if (passenger != null && startupDelaySeconds > 0f)
            {
                passenger.gameObject.SetActive(false);
            }
        }

        private IEnumerator Start()
        {
            if (passenger == null || trainService == null || activeTrain == null)
            {
                enabled = false;
                Debug.LogError("[BidirectionalTrainLoop] Passenger world is not configured.", this);
                yield break;
            }

            if (startupDelaySeconds > 0f)
            {
                passenger.gameObject.SetActive(false);
                yield return new WaitForSeconds(startupDelaySeconds);
                passenger.gameObject.SetActive(true);
            }

            yield return BeginJourneyFromStreet();
        }

        private void Update()
        {
            if (trainService == null || passenger == null)
            {
                return;
            }

            MaintainArrivalTransferHold();

            if (Time.unscaledTime < nextStatusUpdateTime)
            {
                return;
            }
            nextStatusUpdateTime = Time.unscaledTime + 0.35f;

            UtilityJourneyAction action = passenger.CurrentAction;
            UtilityPassengerSocialState socialState = passenger.SocialState;
            string profileName = passenger.ActiveProfileName;
            if (hasReportedStatus && lastReportedComplete == complete &&
                lastReportedArea == currentArea &&
                lastReportedAction == action &&
                lastReportedSocialState == socialState &&
                string.Equals(lastReportedProfile, profileName,
                    System.StringComparison.Ordinal))
            {
                return;
            }

            hasReportedStatus = true;
            lastReportedComplete = complete;
            lastReportedArea = currentArea;
            lastReportedAction = action;
            lastReportedSocialState = socialState;
            lastReportedProfile = profileName;
            string state = complete ? "COMPLETE" : currentArea + " / " + passenger.CurrentAction;
            trainService.SetPassengerJourneyStatus(
                passengerLabel + " " + requiredDirection + ": " + state +
                " / " + socialState + " / " + profileName);
        }

        private void LateUpdate()
        {
            if (activeTrain == null)
            {
                return;
            }

            Vector3 currentTrainPosition = activeTrain.position;
            if (onTrain && passenger != null)
            {
                Vector3 trainDelta = currentTrainPosition - lastTrainPosition;
                if (trainDelta.sqrMagnitude > 0.0000001f)
                {
                    passenger.TranslateWithCarrier(trainDelta);
                }
            }

            lastTrainPosition = currentTrainPosition;
        }

        private IEnumerator BeginJourneyFromStreet()
        {
            currentArea = UtilityJourneyArea.OriginConcourse;
            farePaid = false;
            onTrain = false;
            settledInsideTrain = false;
            transitioning = true;
            complete = false;
            passenger.SetCollisionEnabled(true);
            arrivalTransferHeld = false;
            ChooseTripEndpoints();
            passenger.SetMovementSuspended(true);
            yield return null;

            if (originEntranceSpawns != null && originEntranceSpawns.Length > 0)
            {
                int index = Random.Range(0, originEntranceSpawns.Length);
                Transform spawn = originEntranceSpawns[index];
                Transform outside = originEntranceOutsidePoints != null &&
                                    index < originEntranceOutsidePoints.Length
                    ? originEntranceOutsidePoints[index]
                    : null;
                if (spawn != null)
                {
                    StartSpawnName = spawn.name;
                    passenger.Teleport(outside != null ? outside.position : spawn.position);
                    if (outside != null)
                    {
                        yield return MoveControlledPath(new[] { outside.position, spawn.position });
                    }
                }
            }

            lastTrainPosition = activeTrain.position;
            ResumeDecisionMaking();
        }

        private void ChooseTripEndpoints()
        {
            if (destinationExits == null || destinationExits.Length == 0)
            {
                preferredExitId = string.Empty;
                return;
            }

            UtilityJourneyFacilityPrototype exit =
                destinationExits[Random.Range(0, destinationExits.Length)];
            preferredExitId = exit != null ? exit.FacilityId : string.Empty;
        }

        private IEnumerator TraverseFareGate(
            UtilityJourneyFacilityPrototype facility,
            bool entering)
        {
            transitioning = true;
            passenger.SetMovementSuspended(true);
            UtilityJourneyPassagePrototype passage = facility.Passage;
            if (passage != null)
            {
                passage.SetOpen(true);
                yield return new WaitForSeconds(passage.AnimationSeconds + gatePauseSeconds);
            }

            yield return MoveControlledPath(BuildSourceTraversalPoints(facility));
            if (passage != null)
            {
                passage.SetOpen(false);
                yield return new WaitForSeconds(passage.AnimationSeconds);
            }

            farePaid = entering;
            if (entering)
            {
                EntryGateTapCount++;
            }
            else
            {
                ExitGateTapCount++;
            }

            ReleaseTraversal();
            ResumeDecisionMaking();
        }

        private IEnumerator TransferStair(
            UtilityJourneyFacilityPrototype facility,
            UtilityJourneyArea destinationArea)
        {
            if (!facility.HasTransition)
            {
                ReleaseTraversal();
                Debug.LogError("[BidirectionalTrainLoop] Stair has no destination: " +
                               facility.FacilityId, facility);
                yield break;
            }

            transitioning = true;
            passenger.SetMovementSuspended(true);
            yield return MoveControlledPath(BuildSourceTraversalPoints(facility));
            passenger.gameObject.SetActive(false);
            yield return new WaitForSeconds(stairTransferPauseSeconds);
            currentArea = destinationArea;
            passenger.Teleport(facility.DestinationSpawn.position);
            passenger.gameObject.SetActive(true);

            Vector3[] release = BuildDestinationReleasePoints(facility);
            if (release.Length > 1)
            {
                yield return MoveControlledPath(release);
            }

            ReleaseTraversal();
            ResumeDecisionMaking();
        }

        private IEnumerator BoardTrain(UtilityJourneyFacilityPrototype facility)
        {
            if (!facility.HasTransition)
            {
                ReleaseTraversal();
                yield break;
            }

            transitioning = true;
            passenger.SetMovementSuspended(true);
            BeginTrainTransfer();
            passenger.SetCollisionEnabled(false);
            lastTrainPosition = activeTrain.position;
            onTrain = true;
            yield return MoveControlledPath(BuildSourceTraversalPoints(facility));
            passenger.Teleport(facility.DestinationSpawn.position);
            lastTrainPosition = activeTrain.position;
            currentArea = UtilityJourneyArea.TrainInterior;
            settledInsideTrain = false;
            BoardingCount++;
            yield return new WaitForFixedUpdate();
            EndTrainTransfer();
            ReleaseTraversal();
            ResumeDecisionMaking();
        }

        private IEnumerator AlightTrain(UtilityJourneyFacilityPrototype facility)
        {
            if (!facility.HasTransition)
            {
                ReleaseTraversal();
                yield break;
            }

            transitioning = true;
            passenger.SetMovementSuspended(true);
            BeginTrainTransfer();
            passenger.SetCollisionEnabled(false);
            yield return MoveControlledPath(BuildAlightPath(facility));
            currentArea = UtilityJourneyArea.DestinationPlatform;
            onTrain = false;
            settledInsideTrain = false;
            AlightingCount++;
            yield return new WaitForFixedUpdate();
            passenger.SetCollisionEnabled(true);
            EndTrainTransfer();
            ReleaseTraversal();
            ResumeDecisionMaking();
        }

        private IEnumerator LeaveStation(UtilityJourneyFacilityPrototype facility)
        {
            transitioning = true;
            passenger.SetMovementSuspended(true);
            if (facility.HasTraversal)
            {
                yield return MoveControlledPath(BuildSourceTraversalPoints(facility));
            }

            ReleaseTraversal();
            complete = true;
            CompletedFullCycle = true;
            CompletedCycleCount++;
            currentArea = UtilityJourneyArea.Completed;
            transitioning = false;
            passenger.NotifyWorldChanged();
            Debug.Log("[BidirectionalTrainLoop] " + passengerLabel + " completed " +
                      requiredDirection + " cycle.", this);

            if (repeatJourney)
            {
                yield return new WaitForSeconds(repeatDelaySeconds + Random.Range(0f, 2f));
                passenger.gameObject.SetActive(false);
                yield return null;
                passenger.gameObject.SetActive(true);
                yield return BeginJourneyFromStreet();
            }
        }

        private void BeginTrainTransfer()
        {
            if (trainTransferHeld)
            {
                return;
            }

            trainTransferHeld = true;
            trainService.BeginPassengerTransfer(requiredDirection);
        }

        private void MaintainArrivalTransferHold()
        {
            if (!onTrain || currentArea != UtilityJourneyArea.TrainInterior)
            {
                if (arrivalTransferHeld && !transitioning)
                {
                    EndTrainTransfer();
                }
                return;
            }

            UtilityPassengerFacts facts = GetFacts();
            if (facts.DestinationReady && facts.TrainDoorOpen)
            {
                if (!arrivalTransferHeld)
                {
                    BeginTrainTransfer();
                    arrivalTransferHeld = trainTransferHeld;
                    passenger.NotifyWorldChanged();
                }
            }
            else if (arrivalTransferHeld && !transitioning)
            {
                EndTrainTransfer();
            }
        }

        private void EndTrainTransfer()
        {
            if (!trainTransferHeld)
            {
                arrivalTransferHeld = false;
                return;
            }

            trainTransferHeld = false;
            arrivalTransferHeld = false;
            trainService.EndPassengerTransfer(requiredDirection);
        }

        private bool TryAcquireTraversal(UtilityJourneyFacilityPrototype facility)
        {
            int key = facility.Passage != null
                ? facility.Passage.GetInstanceID()
                : facility.TraversalEntry != null
                    ? facility.TraversalEntry.GetInstanceID()
                    : facility.GetInstanceID();
            if (TraversalOwners.TryGetValue(key, out OverviewUtilityJourneyWorldPrototype owner) &&
                owner != null && owner != this && owner.isActiveAndEnabled)
            {
                return false;
            }

            TraversalOwners[key] = this;
            activeTraversalKey = key;
            return true;
        }

        private void ReleaseTraversal()
        {
            if (activeTraversalKey == 0)
            {
                return;
            }

            if (TraversalOwners.TryGetValue(
                    activeTraversalKey,
                    out OverviewUtilityJourneyWorldPrototype owner) && owner == this)
            {
                TraversalOwners.Remove(activeTraversalKey);
            }

            activeTraversalKey = 0;
        }

        private static bool RequiresExclusiveTraversal(UtilityJourneyAction action)
        {
            return action == UtilityJourneyAction.TapIn ||
                   action == UtilityJourneyAction.BoardTrain ||
                   action == UtilityJourneyAction.AlightTrain ||
                   action == UtilityJourneyAction.TapOut ||
                   action == UtilityJourneyAction.LeaveStation;
        }

        private void ResumeDecisionMaking()
        {
            transitioning = false;
            passenger.SetMovementSuspended(false);
            passenger.NotifyWorldChanged();
        }

        private Vector3[] BuildSourceTraversalPoints(
            UtilityJourneyFacilityPrototype facility)
        {
            var points = new List<Vector3> { passenger.transform.position };
            if (facility.TraversalEntry != null)
            {
                points.Add(facility.TraversalEntry.position);
            }
            if (facility.TraversalDestination != null)
            {
                points.Add(facility.TraversalDestination.position);
            }
            return points.ToArray();
        }

        private static Vector3[] BuildDestinationReleasePoints(
            UtilityJourneyFacilityPrototype facility)
        {
            var points = new List<Vector3>();
            if (facility.DestinationSpawn != null)
            {
                points.Add(facility.DestinationSpawn.position);
            }
            if (facility.DestinationEntry != null)
            {
                points.Add(facility.DestinationEntry.position);
            }
            if (facility.DestinationRelease != null)
            {
                points.Add(facility.DestinationRelease.position);
            }
            return points.ToArray();
        }

        private Vector3[] BuildAlightPath(UtilityJourneyFacilityPrototype facility)
        {
            var points = new List<Vector3> { passenger.transform.position };
            if (facility.TraversalEntry != null)
            {
                points.Add(facility.TraversalEntry.position);
            }
            if (facility.TraversalDestination != null)
            {
                points.Add(facility.TraversalDestination.position);
            }
            foreach (Vector3 point in BuildDestinationReleasePoints(facility))
            {
                points.Add(point);
            }
            return points.ToArray();
        }

        private IEnumerator MoveControlledPath(Vector3[] points)
        {
            if (points == null || points.Length < 2)
            {
                yield break;
            }

            for (int index = 1; index < points.Length; index++)
            {
                Vector3 start = passenger.transform.position;
                Vector3 end = points[index];
                float distance = Vector2.Distance(start, end);
                float duration = distance / Mathf.Max(0.1f, controlledWalkSpeed);
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float ratio = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                    passenger.SetControlledPosition(Vector3.Lerp(start, end, ratio));
                    passenger.FaceControlledDirection(end - start);
                    yield return null;
                }

                passenger.SetControlledPosition(end);
            }
        }

        private void OnDisable()
        {
            ReleaseTraversal();
            EndTrainTransfer();
            if (passenger != null)
            {
                passenger.SetCollisionEnabled(true);
            }
            onTrain = false;
        }
    }
}

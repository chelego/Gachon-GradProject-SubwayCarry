using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace SubwayCarry.AI.UtilityJourney
{
    [Serializable]
    public struct UtilityJourneyAreaScreen
    {
        public UtilityJourneyArea Area;
        public GameObject Root;

        public UtilityJourneyAreaScreen(UtilityJourneyArea area, GameObject root)
        {
            Area = area;
            Root = root;
        }
    }

    [DisallowMultipleComponent]
    public sealed class UtilityJourneyWorldPrototype : MonoBehaviour, IUtilityJourneyWorld
    {
        [SerializeField] private UtilityPassengerBrainPrototype passenger;
        [SerializeField] private UtilityJourneyAreaScreen[] screens;
        [SerializeField] private Transform[] originEntranceSpawns;
        [SerializeField] private UtilityJourneyFacilityPrototype[] destinationExits;
        [SerializeField] private CanvasGroup fadeOverlay;
        [SerializeField] private Text worldStatusText;
        [SerializeField] private Transform originPlatformTrainVisual;
        [SerializeField] private Transform destinationPlatformTrainVisual;
        [SerializeField] private Transform trainInteriorVisual;
        [SerializeField] private UtilityJourneyPassagePrototype[] originTrainDoors;
        [SerializeField] private UtilityJourneyPassagePrototype[] destinationTrainDoors;
        [SerializeField] private UtilityJourneyDirection requiredDirection =
            UtilityJourneyDirection.Downbound;
        [SerializeField, Min(0f)] private float trainArrivalDelay = 4f;
        [SerializeField, Min(0.2f)] private float trainArrivalDuration = 4.5f;
        [SerializeField, Min(1f)] private float rideDurationSeconds = 60f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.35f;
        [SerializeField] private Vector2 trainEntryOffset = new Vector2(-45f, 0f);
        [SerializeField] private Vector2 trainExitOffset = new Vector2(45f, 0f);
        [SerializeField, Min(0.2f)] private float trainDepartureDuration = 3.8f;
        [SerializeField, Min(0.05f)] private float passagePauseSeconds = 0.18f;
        [SerializeField, Min(0f)] private float shakePosition = 0.055f;
        [SerializeField, Min(0f)] private float shakeAngle = 0.16f;

        private UtilityJourneyArea currentArea;
        private Vector3 originTrainBasePosition;
        private Vector3 destinationTrainBasePosition;
        private Vector3 interiorBasePosition;
        private float platformEnteredAt = -1f;
        private float rideStartedAt = -1f;
        private bool trainAtPlatform;
        private bool trainDoorOpen;
        private bool farePaid;
        private bool onTrain;
        private bool settledInsideTrain;
        private bool destinationReady;
        private bool transitioning;
        private bool complete;
        private string preferredExitId;
        private Transform passengerOriginalParent;

        public UtilityJourneyArea CurrentArea => currentArea;
        public string CurrentSocialSpaceId => currentArea.ToString();
        public bool IsTransitioning => transitioning;
        public bool IsComplete => complete;
        public bool IsTrainAtPlatform => trainAtPlatform;
        public bool IsTrainDoorOpen => trainDoorOpen;
        public bool IsDestinationReady => destinationReady;
        public bool IsRideInProgress => onTrain && !destinationReady;
        public float RideElapsedSeconds => rideStartedAt < 0f
            ? 0f
            : Mathf.Max(0f, Time.time - rideStartedAt);
        public float RideRemainingSeconds => Mathf.Max(
            0f,
            rideDurationSeconds - RideElapsedSeconds);
        public string PreferredExitId => preferredExitId;
        public string StartSpawnName { get; private set; }
        public int EntryGateTapCount { get; private set; }
        public int ExitGateTapCount { get; private set; }
        public int TransitionCount { get; private set; }
        public bool CompletedSixtySecondRide { get; private set; }

        public void Configure(
            UtilityPassengerBrainPrototype controlledPassenger,
            UtilityJourneyAreaScreen[] areaScreens,
            Transform[] entranceSpawns,
            UtilityJourneyFacilityPrototype[] exits,
            CanvasGroup overlay,
            Text statusText,
            Transform originTrain,
            Transform destinationTrain,
            Transform interiorTrain,
            UtilityJourneyPassagePrototype[] originDoorVisuals,
            UtilityJourneyPassagePrototype[] destinationDoorVisuals,
            UtilityJourneyDirection direction)
        {
            passenger = controlledPassenger;
            screens = areaScreens;
            originEntranceSpawns = entranceSpawns;
            destinationExits = exits;
            fadeOverlay = overlay;
            worldStatusText = statusText;
            originPlatformTrainVisual = originTrain;
            destinationPlatformTrainVisual = destinationTrain;
            trainInteriorVisual = interiorTrain;
            originTrainDoors = originDoorVisuals;
            destinationTrainDoors = destinationDoorVisuals;
            requiredDirection = direction;
            passengerOriginalParent = passenger != null
                ? passenger.transform.parent
                : null;

            originTrainBasePosition = originPlatformTrainVisual != null
                ? originPlatformTrainVisual.localPosition
                : Vector3.zero;
            destinationTrainBasePosition = destinationPlatformTrainVisual != null
                ? destinationPlatformTrainVisual.localPosition
                : Vector3.zero;
            interiorBasePosition = trainInteriorVisual != null
                ? trainInteriorVisual.localPosition
                : Vector3.zero;
        }

        public void ConfigurePrototypeTimings(
            float arrivalDelay,
            float arrivalDuration,
            float rideSeconds,
            float transitionFadeSeconds)
        {
            trainArrivalDelay = Mathf.Max(0f, arrivalDelay);
            trainArrivalDuration = Mathf.Max(0.05f, arrivalDuration);
            rideDurationSeconds = Mathf.Max(0.1f, rideSeconds);
            fadeDuration = Mathf.Max(0.01f, transitionFadeSeconds);
        }

        public UtilityPassengerFacts GetFacts()
        {
            return new UtilityPassengerFacts
            {
                Area = currentArea,
                RequiredDirection = requiredDirection,
                FarePaid = farePaid,
                TrainAtPlatform = trainAtPlatform,
                TrainDoorOpen = trainDoorOpen,
                IsOnTrain = onTrain,
                IsSettledInsideTrain = settledInsideTrain,
                DestinationReady = destinationReady,
                IsTransitioning = transitioning,
                IsComplete = complete,
                PreferredExitId = preferredExitId,
                SecondsUntilTrainArrival = GetSecondsUntilTrainArrival(),
                SecondsUntilDoorClose = trainDoorOpen ? 3f : float.PositiveInfinity
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

            facility.RecordUse();
            switch (action)
            {
                case UtilityJourneyAction.TapIn:
                    StartCoroutine(TraverseFareGateRoutine(facility, true));
                    break;

                case UtilityJourneyAction.DescendToPlatform:
                    StartTransition(facility);
                    break;

                case UtilityJourneyAction.WaitForTrain:
                    passenger?.NotifyWorldChanged(false);
                    break;

                case UtilityJourneyAction.BoardTrain:
                    if (trainAtPlatform && trainDoorOpen)
                    {
                        StartCoroutine(BoardAndDepartRoutine(facility));
                    }
                    break;

                case UtilityJourneyAction.SettleInsideTrain:
                    settledInsideTrain = true;
                    passenger?.NotifyWorldChanged();
                    break;

                case UtilityJourneyAction.AlightTrain:
                    if (destinationReady && trainDoorOpen)
                    {
                        StartCoroutine(ArriveAndAlightRoutine(facility));
                    }
                    break;

                case UtilityJourneyAction.AscendToConcourse:
                    StartTransition(facility);
                    break;

                case UtilityJourneyAction.TapOut:
                    StartCoroutine(TraverseFareGateRoutine(facility, false));
                    break;

                case UtilityJourneyAction.LeaveStation:
                    complete = true;
                    currentArea = UtilityJourneyArea.Completed;
                    ActivateOnly(UtilityJourneyArea.Completed);
                    passenger?.SetMovementSuspended(true);
                    passenger?.NotifyWorldChanged();
                    Debug.Log(
                        "[UtilityJourneyAI] Passenger completed the full trip through exit " +
                        facility.FacilityId + ".",
                        this);
                    break;
            }
        }

        private IEnumerator Start()
        {
            currentArea = UtilityJourneyArea.OriginConcourse;
            ActivateOnly(currentArea);
            ChooseRandomTripEndpoints();
            ResetTrainVisuals();

            if (fadeOverlay != null)
            {
                fadeOverlay.alpha = 1f;
                fadeOverlay.blocksRaycasts = true;
            }

            passenger?.SetMovementSuspended(true);
            yield return null;

            if (originEntranceSpawns != null && originEntranceSpawns.Length > 0)
            {
                Transform spawn = originEntranceSpawns[UnityEngine.Random.Range(
                    0,
                    originEntranceSpawns.Length)];
                if (spawn != null)
                {
                    StartSpawnName = spawn.name;
                    passenger?.Teleport(spawn.position);
                }
            }

            yield return Fade(1f, 0f);
            passenger?.SetMovementSuspended(false);
            passenger?.NotifyWorldChanged();
        }

        private void Update()
        {
            UpdateOriginTrainArrival();
            UpdateTrainRide();
            UpdateStatusText();
        }

        private void UpdateOriginTrainArrival()
        {
            if (currentArea != UtilityJourneyArea.OriginPlatform ||
                trainAtPlatform ||
                platformEnteredAt < 0f)
            {
                return;
            }

            float elapsed = Time.time - platformEnteredAt;
            if (elapsed < trainArrivalDelay)
            {
                return;
            }

            float ratio = Mathf.Clamp01(
                (elapsed - trainArrivalDelay) / trainArrivalDuration);
            ratio = ratio * ratio * (3f - 2f * ratio);
            if (originPlatformTrainVisual != null)
            {
                originPlatformTrainVisual.localPosition =
                    originTrainBasePosition +
                    (Vector3)Vector2.Lerp(trainEntryOffset, Vector2.zero, ratio);
            }

            if (ratio >= 1f)
            {
                trainAtPlatform = true;
                trainDoorOpen = true;
                SetDoorsOpen(originTrainDoors, true);
                passenger?.NotifyWorldChanged();
            }
        }

        private void UpdateTrainRide()
        {
            if (!onTrain || rideStartedAt < 0f)
            {
                RestoreInteriorTransform();
                return;
            }

            if (!destinationReady)
            {
                float t = Time.time - rideStartedAt;
                if (trainInteriorVisual != null)
                {
                    float x = Mathf.Sin(t * 16.5f) * shakePosition;
                    float y = Mathf.Sin(t * 11.2f + 0.7f) * shakePosition * 0.55f;
                    float angle = Mathf.Sin(t * 8.4f) * shakeAngle;
                    trainInteriorVisual.localPosition =
                        interiorBasePosition + new Vector3(x, y, 0f);
                    trainInteriorVisual.localRotation = Quaternion.Euler(0f, 0f, angle);
                }

                if (Time.time - rideStartedAt >= rideDurationSeconds)
                {
                    destinationReady = true;
                    trainDoorOpen = true;
                    CompletedSixtySecondRide = rideDurationSeconds >= 59.9f;
                    RestoreInteriorTransform();
                    passenger?.NotifyWorldChanged();
                }
            }
        }

        private void StartTransition(UtilityJourneyFacilityPrototype facility)
        {
            if (!facility.HasTransition)
            {
                Debug.LogError(
                    "[UtilityJourneyAI] Transition facility has no destination: " +
                    facility.FacilityId,
                    facility);
                return;
            }

            StartCoroutine(TransitionRoutine(facility));
        }

        private IEnumerator TransitionRoutine(UtilityJourneyFacilityPrototype facility)
        {
            transitioning = true;
            passenger?.SetMovementSuspended(true);
            if (facility.HasTraversal)
            {
                yield return MoveControlledPath(
                    BuildSourceTraversalPoints(facility),
                    true,
                    false);
            }
            else
            {
                yield return Fade(0f, 1f);
            }

            currentArea = facility.DestinationArea;
            ActivateOnly(currentArea);
            passenger?.Teleport(facility.DestinationSpawn.position);
            TransitionCount++;

            if (currentArea == UtilityJourneyArea.OriginPlatform &&
                platformEnteredAt < 0f)
            {
                platformEnteredAt = Time.time;
            }
            else if (currentArea == UtilityJourneyArea.DestinationPlatform)
            {
                trainAtPlatform = true;
                trainDoorOpen = true;
                if (destinationPlatformTrainVisual != null)
                {
                    destinationPlatformTrainVisual.localPosition =
                        destinationTrainBasePosition;
                }
                SetDoorsOpen(destinationTrainDoors, true);
            }

            yield return null;
            Vector3[] releasePoints = BuildDestinationReleasePoints(facility);
            if (releasePoints.Length > 1)
            {
                yield return MoveControlledPath(releasePoints, false, true);
            }
            else
            {
                yield return Fade(1f, 0f);
            }
            transitioning = false;
            passenger?.SetMovementSuspended(false);
            passenger?.NotifyWorldChanged();
        }

        private IEnumerator TraverseFareGateRoutine(
            UtilityJourneyFacilityPrototype facility,
            bool entering)
        {
            if (transitioning)
            {
                yield break;
            }

            transitioning = true;
            passenger?.SetMovementSuspended(true);
            UtilityJourneyPassagePrototype passage = facility.Passage;
            if (passage != null)
            {
                passage.SetOpen(true);
                yield return new WaitForSeconds(
                    passage.AnimationSeconds + passagePauseSeconds);
            }

            if (facility.HasTraversal)
            {
                yield return MoveControlledPath(
                    BuildSourceTraversalPoints(facility),
                    false,
                    false);
            }

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

            transitioning = false;
            passenger?.SetMovementSuspended(false);
            passenger?.NotifyWorldChanged();
        }

        private IEnumerator BoardAndDepartRoutine(
            UtilityJourneyFacilityPrototype facility)
        {
            if (transitioning)
            {
                yield break;
            }

            transitioning = true;
            passenger?.SetMovementSuspended(true);
            if (facility.HasTraversal)
            {
                yield return MoveControlledPath(
                    BuildSourceTraversalPoints(facility),
                    false,
                    false);
            }

            onTrain = true;
            settledInsideTrain = false;
            trainDoorOpen = false;
            SetDoorsOpen(originTrainDoors, false);
            yield return new WaitForSeconds(0.45f);

            if (passenger != null && originPlatformTrainVisual != null)
            {
                passenger.transform.SetParent(originPlatformTrainVisual, true);
            }

            Vector3 start = originTrainBasePosition;
            Vector3 end = originTrainBasePosition + (Vector3)trainExitOffset;
            float elapsed = 0f;
            SetFadeImmediate(0f);
            while (elapsed < trainDepartureDuration)
            {
                elapsed += Time.deltaTime;
                float ratio = Mathf.Clamp01(elapsed / trainDepartureDuration);
                float eased = ratio * ratio * (3f - 2f * ratio);
                if (originPlatformTrainVisual != null)
                {
                    originPlatformTrainVisual.localPosition =
                        Vector3.Lerp(start, end, eased);
                }

                if (ratio >= 0.8f)
                {
                    SetFadeImmediate(Mathf.InverseLerp(0.8f, 1f, ratio));
                }
                yield return null;
            }

            if (passenger != null)
            {
                passenger.transform.SetParent(passengerOriginalParent, true);
            }

            currentArea = facility.DestinationArea;
            ActivateOnly(currentArea);
            passenger?.Teleport(facility.DestinationSpawn.position);
            TransitionCount++;
            rideStartedAt = Time.time;
            yield return Fade(1f, 0f);
            transitioning = false;
            passenger?.SetMovementSuspended(false);
            passenger?.NotifyWorldChanged();
        }

        private IEnumerator ArriveAndAlightRoutine(
            UtilityJourneyFacilityPrototype facility)
        {
            if (transitioning || !facility.HasTransition)
            {
                yield break;
            }

            transitioning = true;
            passenger?.SetMovementSuspended(true);
            trainDoorOpen = false;
            SetDoorsOpen(destinationTrainDoors, false);

            yield return Fade(0f, 1f);

            currentArea = facility.DestinationArea;
            ActivateOnly(currentArea);
            passenger?.Teleport(facility.DestinationSpawn.position);
            TransitionCount++;
            trainAtPlatform = true;
            if (destinationPlatformTrainVisual != null)
            {
                destinationPlatformTrainVisual.localPosition =
                    destinationTrainBasePosition;
            }

            yield return null;
            yield return Fade(1f, 0f);
            yield return new WaitForSeconds(passagePauseSeconds);

            SetDoorsOpen(destinationTrainDoors, true);
            trainDoorOpen = true;
            float doorAnimationSeconds = destinationTrainDoors == null
                ? 0f
                : destinationTrainDoors
                    .Where(door => door != null)
                    .Select(door => door.AnimationSeconds)
                    .DefaultIfEmpty(0f)
                    .Max();
            yield return new WaitForSeconds(
                doorAnimationSeconds + passagePauseSeconds);

            Vector3[] releasePoints = BuildDestinationReleasePoints(facility);
            if (releasePoints.Length > 1)
            {
                yield return MoveControlledPath(
                    releasePoints,
                    false,
                    false);
            }

            onTrain = false;
            settledInsideTrain = false;
            transitioning = false;
            passenger?.SetMovementSuspended(false);
            passenger?.NotifyWorldChanged();
        }

        private Vector3[] BuildSourceTraversalPoints(
            UtilityJourneyFacilityPrototype facility)
        {
            var points = new System.Collections.Generic.List<Vector3>();
            points.Add(passenger != null
                ? passenger.transform.position
                : facility.transform.position);
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

        private Vector3[] BuildDestinationReleasePoints(
            UtilityJourneyFacilityPrototype facility)
        {
            var points = new System.Collections.Generic.List<Vector3>();
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

        private IEnumerator MoveControlledPath(
            Vector3[] points,
            bool fadeOutAtLastFifth,
            bool fadeInAcrossPath)
        {
            if (passenger == null || points == null || points.Length < 2)
            {
                yield break;
            }

            float totalDistance = 0f;
            for (int index = 1; index < points.Length; index++)
            {
                totalDistance += Vector2.Distance(points[index - 1], points[index]);
            }
            totalDistance = Mathf.Max(0.01f, totalDistance);
            float completedDistance = 0f;
            if (fadeOutAtLastFifth)
            {
                SetFadeImmediate(0f);
            }
            else if (fadeInAcrossPath)
            {
                SetFadeImmediate(1f);
            }

            for (int index = 1; index < points.Length; index++)
            {
                Vector3 start = points[index - 1];
                Vector3 end = points[index];
                float segmentDistance = Vector2.Distance(start, end);
                float duration = segmentDistance /
                                 Mathf.Max(0.1f, passenger.WalkSpeed);
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float ratio = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                    Vector3 position = Vector3.Lerp(start, end, ratio);
                    passenger.SetControlledPosition(position);
                    passenger.FaceControlledDirection(end - start);
                    float pathRatio = (completedDistance + segmentDistance * ratio) /
                                      totalDistance;
                    if (fadeOutAtLastFifth && pathRatio >= 0.8f)
                    {
                        SetFadeImmediate(Mathf.InverseLerp(0.8f, 1f, pathRatio));
                    }
                    else if (fadeInAcrossPath)
                    {
                        SetFadeImmediate(1f - pathRatio);
                    }
                    yield return null;
                }

                passenger.SetControlledPosition(end);
                completedDistance += segmentDistance;
            }

            if (fadeOutAtLastFifth)
            {
                SetFadeImmediate(1f);
            }
            else if (fadeInAcrossPath)
            {
                SetFadeImmediate(0f);
            }
        }

        private IEnumerator Fade(float from, float to)
        {
            if (fadeOverlay == null)
            {
                yield break;
            }

            fadeOverlay.alpha = from;
            fadeOverlay.blocksRaycasts = true;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeOverlay.alpha = Mathf.Lerp(
                    from,
                    to,
                    Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }

            fadeOverlay.alpha = to;
            fadeOverlay.blocksRaycasts = to > 0.001f;
        }

        private void SetFadeImmediate(float alpha)
        {
            if (fadeOverlay == null)
            {
                return;
            }

            fadeOverlay.alpha = Mathf.Clamp01(alpha);
            fadeOverlay.blocksRaycasts = fadeOverlay.alpha > 0.001f;
        }

        private static void SetDoorsOpen(
            UtilityJourneyPassagePrototype[] doors,
            bool open)
        {
            if (doors == null)
            {
                return;
            }

            foreach (UtilityJourneyPassagePrototype door in doors)
            {
                door?.SetOpen(open);
            }
        }

        private float GetSecondsUntilTrainArrival()
        {
            if (trainAtPlatform)
            {
                return 0f;
            }
            if (currentArea != UtilityJourneyArea.OriginPlatform ||
                platformEnteredAt < 0f)
            {
                return float.PositiveInfinity;
            }

            return Mathf.Max(
                0f,
                trainArrivalDelay + trainArrivalDuration -
                (Time.time - platformEnteredAt));
        }

        private void ChooseRandomTripEndpoints()
        {
            preferredExitId = string.Empty;
            if (destinationExits == null || destinationExits.Length == 0)
            {
                return;
            }

            UtilityJourneyFacilityPrototype exit = destinationExits[
                UnityEngine.Random.Range(0, destinationExits.Length)];
            if (exit != null)
            {
                preferredExitId = exit.FacilityId;
            }
        }

        private void ActivateOnly(UtilityJourneyArea area)
        {
            if (screens == null)
            {
                return;
            }

            foreach (UtilityJourneyAreaScreen screen in screens)
            {
                if (screen.Root != null)
                {
                    screen.Root.SetActive(screen.Area == area);
                }
            }
        }

        private void ResetTrainVisuals()
        {
            if (originPlatformTrainVisual != null)
            {
                originPlatformTrainVisual.localPosition =
                    originTrainBasePosition + (Vector3)trainEntryOffset;
            }

            if (destinationPlatformTrainVisual != null)
            {
                destinationPlatformTrainVisual.localPosition =
                    destinationTrainBasePosition;
            }

            SetDoorsOpen(originTrainDoors, false);
            SetDoorsOpen(destinationTrainDoors, false);

            RestoreInteriorTransform();
        }

        private void RestoreInteriorTransform()
        {
            if (trainInteriorVisual == null)
            {
                return;
            }

            trainInteriorVisual.localPosition = interiorBasePosition;
            trainInteriorVisual.localRotation = Quaternion.identity;
        }

        private void UpdateStatusText()
        {
            if (worldStatusText == null)
            {
                return;
            }

            string trainStatus = currentArea == UtilityJourneyArea.Completed
                ? "JOURNEY COMPLETE"
                : currentArea == UtilityJourneyArea.TrainInterior
                ? destinationReady
                    ? "DESTINATION ARRIVED / DOORS OPEN"
                    : "TRAIN RIDE  " + RideRemainingSeconds.ToString("0.0") + "s"
                : trainAtPlatform
                    ? "TRAIN AT PLATFORM / DOORS " +
                      (trainDoorOpen ? "OPEN" : "CLOSED")
                    : currentArea == UtilityJourneyArea.OriginPlatform
                        ? "TRAIN APPROACHING"
                        : string.Empty;
            worldStatusText.text =
                "UTILITY PASSENGER JOURNEY  |  " + currentArea +
                "\n" + trainStatus +
                "\nStart: " + (StartSpawnName ?? "choosing") +
                "  Preferred exit: " + (preferredExitId ?? "choosing");
        }
    }
}

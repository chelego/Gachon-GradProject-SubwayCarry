using System;
using System.Collections.Generic;
using SubwayCarry.AI;
using UnityEngine;
using UnityEngine.UI;

namespace SubwayCarry.AI.UtilityJourney
{
    [Serializable]
    public struct UtilityJourneyNavigationBinding
    {
        public UtilityJourneyArea Area;
        public GridNavigation2D Navigation;

        public UtilityJourneyNavigationBinding(
            UtilityJourneyArea area,
            GridNavigation2D navigation)
        {
            Area = area;
            Navigation = navigation;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class UtilityPassengerBrainPrototype : MonoBehaviour
    {
        private static readonly HashSet<UtilityPassengerBrainPrototype> ActivePassengers =
            new HashSet<UtilityPassengerBrainPrototype>();

        private readonly struct ScoredFacility
        {
            public readonly UtilityJourneyFacilityPrototype Facility;
            public readonly UtilityCandidateScore Candidate;

            public ScoredFacility(
                UtilityJourneyFacilityPrototype facility,
                UtilityCandidateScore candidate)
            {
                Facility = facility;
                Candidate = candidate;
            }
        }

        private readonly struct SocialSteeringResult
        {
            public readonly Vector2 Direction;
            public readonly float SpeedFactor;
            public readonly UtilityPassengerSocialState State;

            public SocialSteeringResult(
                Vector2 direction,
                float speedFactor,
                UtilityPassengerSocialState state)
            {
                Direction = direction;
                SpeedFactor = Mathf.Clamp(speedFactor, 0f, 1.12f);
                State = state;
            }
        }

        [SerializeField] private MonoBehaviour world;
        [SerializeField] private UtilityJourneyFacilityPrototype[] facilities;
        [SerializeField] private UtilityJourneyNavigationBinding[] navigationBindings;
        [SerializeField] private Text thoughtText;
        [SerializeField] private UtilityPassengerTuningProfile[] tuningProfiles;
        [SerializeField, Min(0.1f)] private float walkSpeed = 1.15f;
        [SerializeField, Min(0.1f)] private float runSpeed = 2.45f;
        [SerializeField, Min(0.1f)] private float acceleration = 4.5f;
        [SerializeField, Min(0.05f)] private float decisionInterval = 0.32f;
        [SerializeField, Min(0.05f)] private float repathInterval = 0.42f;
        [SerializeField, Min(0.01f)] private float pathPointAcceptance = 0.09f;
        [SerializeField, Range(0f, 1f)] private float crowdFlowInfluence = 0.46f;
        [SerializeField, Min(0.2f)] private float collisionPredictionSeconds = 1.35f;
        [SerializeField, Range(0.5f, 1f)] private float predictiveYieldSpeed = 0.72f;
        [SerializeField, Range(1f, 1.2f)] private float flowCatchupMultiplier = 1.08f;
        [SerializeField, Min(0.2f)] private float minimumTrainSettleDistance = 0.85f;
        [SerializeField, Min(0)] private int personalitySeed;

        private readonly List<Vector3> activePath = new List<Vector3>();
        private readonly List<ScoredFacility> scoredFacilities =
            new List<ScoredFacility>();
        private readonly HashSet<UtilityJourneyArea> visitedAreas =
            new HashSet<UtilityJourneyArea>();

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private UtilityJourneyFacilityPrototype currentFacility;
        private UtilityJourneyAction currentAction;
        private UtilityCandidateScore currentCandidate;
        private UtilityJourneyArea lastArea;
        private int pathIndex;
        private float nextDecisionTime;
        private float nextRepathTime;
        private float nextIdleReconsiderTime;
        private float interactionStartedAt = -1f;
        private bool movementSuspended;
        private bool isRunning;
        private float currentSpeed;
        private string topCandidateSummary = string.Empty;
        private UtilityPassengerArchetype archetype;
        private UtilityPassengerTuningProfile activeProfile;
        private UtilityPassengerSocialTuning socialTuning =
            UtilityPassengerSocialTuning.Default;
        private UtilityPassengerSocialState socialState =
            UtilityPassengerSocialState.FreeWalking;
        private Vector2 desiredMovementDirection;
        private Vector3 standingScale;

        public UtilityJourneyAction CurrentAction => currentAction;
        public string CurrentFacilityId => currentFacility != null
            ? currentFacility.FacilityId
            : string.Empty;
        public string CurrentReason => currentCandidate.Reason;
        public int DecisionCount { get; private set; }
        public int CompletedActionCount { get; private set; }
        public int PathSearchCount { get; private set; }
        public int VisitedAreaCount => visitedAreas.Count;
        public bool UsesAuthoredWaypointRoute => false;
        public bool IsRunning => isRunning;
        public float CurrentSpeed => currentSpeed;
        public float WalkSpeed => walkSpeed;
        public UtilityPassengerArchetype Archetype => archetype;
        public UtilityPassengerSocialState SocialState => socialState;
        public string ActiveProfileName => activeProfile != null
            ? activeProfile.DisplayName
            : archetype.ToString();

        public void Configure(
            MonoBehaviour journeyWorld,
            UtilityJourneyFacilityPrototype[] perceivedFacilities,
            UtilityJourneyNavigationBinding[] navigations,
            Text debugThoughtText,
            int seed = 0,
            UtilityPassengerTuningProfile[] availableProfiles = null)
        {
            world = journeyWorld;
            facilities = perceivedFacilities;
            navigationBindings = navigations;
            thoughtText = debugThoughtText;
            personalitySeed = seed;
            tuningProfiles = availableProfiles;
        }

        private IUtilityJourneyWorld JourneyWorld => world as IUtilityJourneyWorld;

        public void ConfigurePrototypeMotion(
            float movementSpeed,
            float decisionsEverySeconds,
            float repathEverySeconds)
        {
            walkSpeed = Mathf.Max(0.1f, movementSpeed);
            runSpeed = Mathf.Max(walkSpeed, walkSpeed * 1.55f);
            acceleration = Mathf.Max(1f, runSpeed * 4f);
            decisionInterval = Mathf.Max(0.02f, decisionsEverySeconds);
            repathInterval = Mathf.Max(0.02f, repathEverySeconds);
        }

        public void Teleport(Vector3 position)
        {
            EnsureBody();
            body.position = position;
            transform.position = position;
            body.linearVelocity = Vector2.zero;
            ClearDecision();
        }

        public void SetControlledPosition(Vector3 position)
        {
            EnsureBody();
            body.position = position;
            transform.position = position;
            body.linearVelocity = Vector2.zero;
        }

        public void TranslateWithCarrier(Vector3 delta)
        {
            if (delta.sqrMagnitude <= 0.0000001f)
            {
                return;
            }

            EnsureBody();
            Vector3 position = transform.position + delta;
            body.position = position;
            transform.position = position;
            body.linearVelocity = Vector2.zero;
            for (int index = 0; index < activePath.Count; index++)
            {
                activePath[index] += delta;
            }
        }

        public void SetCollisionEnabled(bool enabled)
        {
            EnsureBody();
            if (bodyCollider != null)
            {
                bodyCollider.enabled = enabled;
            }
        }

        public void FaceControlledDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.up = direction.normalized;
        }

        public void SetMovementSuspended(bool suspended)
        {
            movementSuspended = suspended;
            EnsureBody();
            if (suspended)
            {
                body.linearVelocity = Vector2.zero;
                currentSpeed = 0f;
                isRunning = false;
            }
        }

        public void NotifyWorldChanged(bool clearCurrentChoice = true)
        {
            nextDecisionTime = 0f;
            nextRepathTime = 0f;
            if (clearCurrentChoice)
            {
                ClearDecision();
            }
        }

        private void Awake()
        {
            EnsureBody();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.linearDamping = 12f;
            currentSpeed = 0f;
            standingScale = transform.localScale;
        }

        private void OnEnable()
        {
            ActivePassengers.Add(this);
        }

        private void OnDisable()
        {
            ActivePassengers.Remove(this);
            desiredMovementDirection = Vector2.zero;
        }

        private void Start()
        {
            if (JourneyWorld == null)
            {
                enabled = false;
                Debug.LogError(
                    "[UtilityJourneyAI] Passenger world must implement IUtilityJourneyWorld.",
                    this);
                return;
            }

            if (personalitySeed <= 0)
            {
                personalitySeed = UnityEngine.Random.Range(1, int.MaxValue);
            }

            SelectAndApplyTuningProfile();

            lastArea = JourneyWorld.CurrentArea;
            visitedAreas.Add(lastArea);
            Decide(true);
        }

        private void FixedUpdate()
        {
            if (JourneyWorld == null)
            {
                return;
            }

            UtilityJourneyArea area = JourneyWorld.CurrentArea;
            if (area != lastArea)
            {
                lastArea = area;
                visitedAreas.Add(area);
                ClearDecision();
                nextDecisionTime = 0f;
                RestoreStandingPose();
            }

            if (movementSuspended || JourneyWorld.IsTransitioning || JourneyWorld.IsComplete)
            {
                body.linearVelocity = Vector2.zero;
                UpdateThoughtText();
                return;
            }

            bool atCurrentFacility = IsAtCurrentFacility();
            if (currentAction == UtilityJourneyAction.RideTrain && atCurrentFacility)
            {
                UtilityPassengerFacts rideFacts = JourneyWorld.GetFacts();
                if (!rideFacts.DestinationReady || !rideFacts.TrainDoorOpen)
                {
                    StopAtCurrentFacility();
                    return;
                }

                ClearDecision();
            }

            if (currentAction == UtilityJourneyAction.WaitForTrain && atCurrentFacility)
            {
                UtilityPassengerFacts waitFacts = JourneyWorld.GetFacts();
                if (waitFacts.TrainAtPlatform && waitFacts.TrainDoorOpen)
                {
                    ClearDecision();
                }
                else if (Time.time < nextIdleReconsiderTime)
                {
                    StopAtCurrentFacility();
                    return;
                }
                else
                {
                    nextIdleReconsiderTime = Time.time + Mathf.Max(
                        1.1f,
                        decisionInterval * 4f);
                    Decide(true);
                    if (currentAction == UtilityJourneyAction.WaitForTrain &&
                        IsAtCurrentFacility())
                    {
                        StopAtCurrentFacility();
                        return;
                    }
                }
            }

            if (Time.time >= nextDecisionTime || currentFacility == null)
            {
                Decide(currentFacility == null);
            }

            if (currentFacility == null)
            {
                UpdateThoughtText();
                return;
            }

            if (!IsAtCurrentFacility())
            {
                interactionStartedAt = -1f;
                RestoreStandingPose();
                MoveTowardCurrentFacility();
                UpdateThoughtText();
                return;
            }

            body.linearVelocity = Vector2.zero;
            if (interactionStartedAt < 0f)
            {
                interactionStartedAt = Time.time;
            }

            float requiredInteractionSeconds = IsImmediateTraversal(currentFacility)
                ? 0f
                : currentFacility.InteractionSeconds;
            if (Time.time - interactionStartedAt < requiredInteractionSeconds)
            {
                UpdateThoughtText();
                return;
            }

            UtilityJourneyFacilityPrototype resolvedFacility = currentFacility;
            UtilityJourneyAction resolvedAction = currentAction;
            CompletedActionCount++;
            ClearDecision();
            JourneyWorld.ResolveAction(resolvedAction, resolvedFacility);
            nextDecisionTime = 0f;
            UpdateThoughtText();
        }

        private void Decide(bool force)
        {
            if (!force && Time.time < nextDecisionTime)
            {
                return;
            }

            nextDecisionTime = Time.time + decisionInterval;
            DecisionCount++;
            UtilityPassengerFacts facts = JourneyWorld.GetFacts();
            scoredFacilities.Clear();

            if (facilities != null)
            {
                foreach (UtilityJourneyFacilityPrototype facility in facilities)
                {
                    if (facility == null || facility.Area != facts.Area)
                    {
                        continue;
                    }

                    if (!IsContextuallyUsable(facility, facts))
                    {
                        continue;
                    }

                    float pathDistance = Vector2.Distance(
                        body.position,
                        facility.transform.position);
                    bool reachable = IsPotentiallyReachable(
                        facts.Area,
                        body.position,
                        facility.transform.position,
                        GetEffectiveAcceptanceRadius(facility));
                    float preference = GetStablePreference(facility);
                    float liveQueueSeconds;
                    float liveCrowdCost = GetLiveFacilityCrowd(
                        facility,
                        out liveQueueSeconds);
                    if (IsExclusiveComfortSpotOccupied(facility))
                    {
                        continue;
                    }
                    var observation = new UtilityFacilityObservation(
                        facility.FacilityId,
                        facility.Kind,
                        facility.Area,
                        facility.Direction,
                        facility.IsAvailable,
                        reachable,
                        pathDistance,
                        Mathf.Max(facility.CrowdCost, liveCrowdCost),
                        facility.EstimatedQueueSeconds + liveQueueSeconds,
                        preference,
                        facility == currentFacility,
                        facility.WaitingStyle,
                        facility.RouteGroup,
                        facility.ComfortValue,
                        facility.TransferConvenience);
                    UtilityCandidateScore candidate =
                        UtilityJourneyDecisionModel.Evaluate(
                            facts,
                            observation,
                            activeProfile != null
                                ? activeProfile.DecisionWeights
                                : UtilityPassengerDecisionWeights.Default,
                            thoughtText != null);
                    if (!candidate.IsValid)
                    {
                        continue;
                    }

                    scoredFacilities.Add(new ScoredFacility(facility, candidate));
                }
            }

            scoredFacilities.Sort((left, right) =>
                right.Candidate.Score.CompareTo(left.Candidate.Score));
            if (thoughtText != null)
            {
                BuildTopCandidateSummary();
            }
            if (scoredFacilities.Count == 0)
            {
                currentFacility = null;
                currentAction = UtilityJourneyAction.None;
                activePath.Clear();
                pathIndex = 0;
                return;
            }

            ScoredFacility best = default;
            List<Vector3> bestPath = null;
            bool foundBest = false;
            foreach (ScoredFacility scored in scoredFacilities)
            {
                if (scored.Facility == currentFacility &&
                    scored.Candidate.Action == currentAction &&
                    activePath.Count > 0 && pathIndex < activePath.Count)
                {
                    best = scored;
                    foundBest = true;
                    break;
                }

                if (TryBuildPath(
                        facts.Area,
                        body.position,
                        scored.Facility.transform.position,
                        GetEffectiveAcceptanceRadius(scored.Facility),
                        out List<Vector3> resolvedPath,
                        out _))
                {
                    best = scored;
                    bestPath = resolvedPath;
                    foundBest = true;
                    break;
                }
            }

            if (!foundBest)
            {
                currentFacility = null;
                currentAction = UtilityJourneyAction.None;
                activePath.Clear();
                pathIndex = 0;
                return;
            }

            bool changed = best.Facility != currentFacility ||
                           best.Candidate.Action != currentAction;
            bool replacePath = changed || activePath.Count == 0 ||
                               pathIndex >= activePath.Count;
            currentFacility = best.Facility;
            currentAction = best.Candidate.Action;
            currentCandidate = best.Candidate;
            if (replacePath && bestPath != null)
            {
                activePath.Clear();
                activePath.AddRange(bestPath);
                pathIndex = activePath.Count > 1 ? 1 : 0;
                nextRepathTime = Time.time + repathInterval;
            }
            if (changed)
            {
                interactionStartedAt = -1f;
            }
        }

        private void MoveTowardCurrentFacility()
        {
            if (currentFacility == null)
            {
                return;
            }

            bool missingPath = activePath.Count == 0;
            bool exhaustedPath = pathIndex >= activePath.Count &&
                                 Time.time >= nextRepathTime;
            if (missingPath || exhaustedPath)
            {
                if (TryBuildPath(
                        JourneyWorld.CurrentArea,
                        body.position,
                        currentFacility.transform.position,
                        GetEffectiveAcceptanceRadius(currentFacility),
                        out List<Vector3> path,
                        out _))
                {
                    activePath.Clear();
                    activePath.AddRange(path);
                    pathIndex = activePath.Count > 1 ? 1 : 0;
                }

                nextRepathTime = Time.time + Mathf.Max(1f, repathInterval * 4f);
            }

            Vector2 target = currentFacility.transform.position;
            while (pathIndex < activePath.Count)
            {
                target = activePath[pathIndex];
                if (Vector2.Distance(body.position, target) > pathPointAcceptance)
                {
                    break;
                }

                pathIndex++;
            }

            if (pathIndex >= activePath.Count)
            {
                target = currentFacility.transform.position;
            }

            Vector2 offset = target - body.position;
            if (offset.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 direction = offset.normalized;
            desiredMovementDirection = direction;
            isRunning = ShouldRunForCurrentIntention(offset.magnitude);
            float targetSpeed = isRunning ? runSpeed : walkSpeed;
            SocialSteeringResult social = EvaluateSocialSteering(
                direction,
                target,
                targetSpeed);
            direction = social.Direction.sqrMagnitude > 0.0001f
                ? social.Direction.normalized
                : direction;
            targetSpeed *= social.SpeedFactor;
            socialState = isRunning && social.State == UtilityPassengerSocialState.FreeWalking
                ? UtilityPassengerSocialState.RushingForDoor
                : social.State;
            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                targetSpeed,
                acceleration * Time.fixedDeltaTime);
            float step = Mathf.Min(
                currentSpeed * Time.fixedDeltaTime,
                offset.magnitude);
            body.MovePosition(body.position + direction * step);
            transform.up = Vector2.Lerp(
                transform.up,
                direction,
                Mathf.Clamp01(10f * Time.fixedDeltaTime));
        }

        private bool ShouldRunForCurrentIntention(float remainingDistance)
        {
            if (world == null || remainingDistance < 1.5f)
            {
                return false;
            }

            UtilityPassengerFacts facts = JourneyWorld.GetFacts();
            if (currentAction == UtilityJourneyAction.BoardTrain)
            {
                if (!facts.TrainAtPlatform || !facts.TrainDoorOpen)
                {
                    return false;
                }

                float walkSeconds = remainingDistance / Mathf.Max(0.1f, walkSpeed);
                return facts.SecondsUntilDoorClose <=
                           socialTuning.DoorRushThresholdSeconds ||
                       walkSeconds + 0.7f >= facts.SecondsUntilDoorClose;
            }

            if (currentAction == UtilityJourneyAction.AlightTrain)
            {
                return facts.DestinationReady && facts.TrainDoorOpen;
            }

            return false;
        }

        private void StopAtCurrentFacility()
        {
            body.linearVelocity = Vector2.zero;
            currentSpeed = 0f;
            isRunning = false;
            desiredMovementDirection = Vector2.zero;
            socialState = currentFacility != null &&
                          currentFacility.WaitingStyle ==
                          UtilityJourneyWaitingStyle.DoorQueue
                ? UtilityPassengerSocialState.QueueFollowing
                : UtilityPassengerSocialState.FreeWalking;
            ApplyWaitingPose();
            UpdateThoughtText();
        }

        private bool IsPotentiallyReachable(
            UtilityJourneyArea area,
            Vector2 start,
            Vector2 destination,
            float acceptanceRadius)
        {
            if (Vector2.Distance(start, destination) <= acceptanceRadius)
            {
                return true;
            }

            return FindNavigation(area, start, destination) != null;
        }

        private SocialSteeringResult EvaluateSocialSteering(
            Vector2 desiredDirection,
            Vector2 target,
            float targetSpeed)
        {
            string socialSpace = JourneyWorld != null
                ? JourneyWorld.CurrentSocialSpaceId
                : string.Empty;
            Vector2 separation = Vector2.zero;
            Vector2 sideStep = Vector2.zero;
            float speedFactor = 1f;
            int nearbyCount = 0;
            bool followingQueue = false;
            bool yielding = false;
            bool waitingForAlighting = false;
            bool forcedContact = false;
            Vector2 crowdFlowDirection = Vector2.zero;
            float crowdFlowSpeed = 0f;
            float crowdFlowWeight = 0f;

            foreach (UtilityPassengerBrainPrototype peer in ActivePassengers)
            {
                if (peer == null || peer == this || !peer.isActiveAndEnabled ||
                    peer.JourneyWorld == null ||
                    !string.Equals(
                        peer.JourneyWorld.CurrentSocialSpaceId,
                        socialSpace,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                Vector2 toPeer = (Vector2)peer.transform.position - body.position;
                float distance = toPeer.magnitude;
                if (distance <= 0.0001f || distance > socialTuning.AwarenessRadius)
                {
                    continue;
                }

                nearbyCount++;
                Vector2 peerDirection = peer.desiredMovementDirection.sqrMagnitude > 0.01f
                    ? peer.desiredMovementDirection.normalized
                    : Vector2.zero;
                float directionAlignment = peerDirection.sqrMagnitude > 0.01f
                    ? Vector2.Dot(peerDirection, desiredDirection)
                    : 0f;
                bool broadlySameFlow = directionAlignment > 0.55f;
                if (directionAlignment > 0.2f)
                {
                    float flowWeight =
                        (1f - distance / socialTuning.AwarenessRadius) *
                        Mathf.Lerp(0.35f, 1f, directionAlignment);
                    crowdFlowDirection += peerDirection * flowWeight;
                    crowdFlowSpeed += peer.currentSpeed * flowWeight;
                    crowdFlowWeight += flowWeight;
                }

                float crowdCompression = Mathf.Clamp01(
                    (nearbyCount - 2f) / 5f) *
                    (1f - socialTuning.CrowdTolerance);
                float desiredSpace = Mathf.Lerp(
                    socialTuning.PersonalSpaceRadius,
                    socialTuning.MinimumCrowdedRadius,
                    crowdCompression);
                if (distance < desiredSpace)
                {
                    float strength = 1f - distance / Mathf.Max(0.05f, desiredSpace);
                    if (broadlySameFlow)
                    {
                        float longitudinal = Vector2.Dot(toPeer, desiredDirection);
                        Vector2 lateralOffset =
                            toPeer - desiredDirection * longitudinal;
                        if (lateralOffset.sqrMagnitude > 0.0025f)
                        {
                            separation -= lateralOffset.normalized * strength * 0.55f;
                        }

                        if (longitudinal > 0.05f)
                        {
                            float peerPace = Mathf.Clamp(
                                peer.currentSpeed / Mathf.Max(0.1f, targetSpeed),
                                0.35f,
                                1f);
                            float gapBlend = Mathf.InverseLerp(
                                socialTuning.MinimumCrowdedRadius,
                                desiredSpace,
                                distance);
                            speedFactor = Mathf.Min(
                                speedFactor,
                                Mathf.Lerp(0.35f, peerPace, gapBlend));
                        }
                    }
                    else
                    {
                        separation -= toPeer.normalized * strength;
                    }

                    if (distance <= socialTuning.MinimumCrowdedRadius)
                    {
                        forcedContact = true;
                    }
                }

                bool sameTarget = currentFacility != null && peer.currentFacility != null &&
                                  Vector2.Distance(
                                      currentFacility.transform.position,
                                      peer.currentFacility.transform.position) <= 0.5f;
                bool peerAhead = Vector2.Dot(toPeer.normalized, desiredDirection) > 0.28f;
                bool sameFlow = peerDirection.sqrMagnitude <= 0.01f ||
                                broadlySameFlow;
                if (sameTarget && sameFlow && peerAhead &&
                    peer.DistanceToCurrentFacility() <= DistanceToCurrentFacility() + 0.15f)
                {
                    followingQueue = true;
                    float queueStopDistance = desiredSpace + 0.14f;
                    if (distance < queueStopDistance)
                    {
                        float peerPace = Mathf.Clamp(
                            peer.currentSpeed / Mathf.Max(0.1f, targetSpeed),
                            0.35f,
                            1f);
                        float gapBlend = Mathf.InverseLerp(
                            socialTuning.MinimumCrowdedRadius,
                            queueStopDistance,
                            distance);
                        float flowFollowingSpeed = Mathf.Lerp(
                            0.35f,
                            Mathf.Max(0.65f, peerPace),
                            gapBlend);
                        float disciplinedFollowing = Mathf.Lerp(
                            0.85f,
                            flowFollowingSpeed,
                            socialTuning.QueueDiscipline);
                        speedFactor = Mathf.Min(
                            speedFactor,
                            disciplinedFollowing);
                    }
                }

                if (currentAction == UtilityJourneyAction.BoardTrain &&
                    peer.currentAction == UtilityJourneyAction.AlightTrain &&
                    distance < 2.2f)
                {
                    waitingForAlighting = true;
                    speedFactor = 0f;
                    sideStep += new Vector2(-desiredDirection.y, desiredDirection.x) *
                                GetPassingSideSign() * 0.9f;
                    continue;
                }

                bool oppositeFlow = directionAlignment < -0.3f;
                if (oppositeFlow && peerAhead && distance < 1.65f)
                {
                    sideStep += new Vector2(-desiredDirection.y, desiredDirection.x) *
                                1f *
                                Mathf.Lerp(0.35f, 0.9f, 1f - distance / 1.65f);
                }

                Vector2 relativeVelocity =
                    peerDirection * peer.currentSpeed - desiredDirection * targetSpeed;
                float relativeSpeedSquared = relativeVelocity.sqrMagnitude;
                if (peerDirection.sqrMagnitude > 0.01f &&
                    directionAlignment < 0.78f &&
                    relativeSpeedSquared > 0.01f)
                {
                    float timeToClosest = Mathf.Clamp(
                        -Vector2.Dot(toPeer, relativeVelocity) /
                        relativeSpeedSquared,
                        0f,
                        collisionPredictionSeconds);
                    Vector2 closestOffset = toPeer + relativeVelocity * timeToClosest;
                    float predictedSpace = desiredSpace + 0.16f;
                    if (timeToClosest > 0.05f &&
                        closestOffset.sqrMagnitude < predictedSpace * predictedSpace)
                    {
                        bool hasPriority = personalitySeed != peer.personalitySeed
                            ? personalitySeed < peer.personalitySeed
                            : GetInstanceID() < peer.GetInstanceID();
                        float avoidanceSide = oppositeFlow
                            ? 1f
                            : hasPriority ? 1f : -1f;
                        float urgency = 1f - timeToClosest /
                            Mathf.Max(0.1f, collisionPredictionSeconds);
                        sideStep += new Vector2(-desiredDirection.y, desiredDirection.x) *
                                    avoidanceSide * Mathf.Lerp(0.25f, 0.85f, urgency);
                        if (hasPriority && speedFactor >= 0.99f)
                        {
                            speedFactor = Mathf.Min(
                                flowCatchupMultiplier,
                                1f + urgency * 0.08f);
                        }
                        else
                        {
                            speedFactor = Mathf.Min(
                                speedFactor,
                                Mathf.Lerp(0.9f, predictiveYieldSpeed, urgency));
                            yielding = true;
                        }
                    }
                }

                Vector2 closingVelocity =
                    desiredDirection * targetSpeed - peerDirection * peer.currentSpeed;
                if (peerAhead && distance < 1.15f &&
                    Vector2.Dot(closingVelocity, toPeer.normalized) > 0.15f)
                {
                    float courtesyYield = Mathf.Lerp(
                        0.72f,
                        0.18f,
                        socialTuning.Courtesy);
                    float assertiveRecovery = Mathf.Lerp(
                        0.65f,
                        1f,
                        socialTuning.Assertiveness);
                    speedFactor = Mathf.Min(
                        speedFactor,
                        courtesyYield * assertiveRecovery);
                    yielding = true;
                }
            }

            if (crowdFlowWeight > 0.01f)
            {
                Vector2 averageFlowDirection = crowdFlowDirection.sqrMagnitude > 0.0001f
                    ? crowdFlowDirection.normalized
                    : desiredDirection;
                float flowBlend = crowdFlowInfluence *
                                  Mathf.Clamp01(crowdFlowWeight / 2.2f);
                desiredDirection = Vector2.Lerp(
                    desiredDirection,
                    averageFlowDirection,
                    flowBlend).normalized;

                float averageFlowSpeed = crowdFlowSpeed / crowdFlowWeight;
                float flowSpeedFactor = Mathf.Clamp(
                    averageFlowSpeed / Mathf.Max(0.1f, targetSpeed),
                    0.55f,
                    flowCatchupMultiplier);
                if (flowSpeedFactor < 1f)
                {
                    speedFactor = Mathf.Min(speedFactor, flowSpeedFactor);
                }
                else if (!yielding && !waitingForAlighting && speedFactor >= 0.99f)
                {
                    speedFactor = Mathf.Max(speedFactor, flowSpeedFactor);
                }
            }

            Vector2 combined = desiredDirection + separation * 1.2f + sideStep;
            if (combined.sqrMagnitude <= 0.0001f)
            {
                combined = desiredDirection;
            }

            UtilityPassengerSocialState state = UtilityPassengerSocialState.FreeWalking;
            if (waitingForAlighting)
            {
                state = UtilityPassengerSocialState.WaitingForAlighting;
            }
            else if (forcedContact)
            {
                state = UtilityPassengerSocialState.CrowdedContact;
            }
            else if (sideStep.sqrMagnitude > 0.01f)
            {
                state = UtilityPassengerSocialState.SideStepping;
            }
            else if (followingQueue)
            {
                state = UtilityPassengerSocialState.QueueFollowing;
            }
            else if (yielding)
            {
                state = UtilityPassengerSocialState.Yielding;
            }

            return new SocialSteeringResult(combined.normalized, speedFactor, state);
        }

        private float GetLiveFacilityCrowd(
            UtilityJourneyFacilityPrototype facility,
            out float liveQueueSeconds)
        {
            liveQueueSeconds = 0f;
            if (facility == null || JourneyWorld == null)
            {
                return 0f;
            }

            int nearby = 0;
            int sameTarget = 0;
            string socialSpace = JourneyWorld.CurrentSocialSpaceId;
            foreach (UtilityPassengerBrainPrototype peer in ActivePassengers)
            {
                if (peer == null || peer == this || !peer.isActiveAndEnabled ||
                    peer.JourneyWorld == null ||
                    !string.Equals(
                        peer.JourneyWorld.CurrentSocialSpaceId,
                        socialSpace,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (Vector2.Distance(peer.transform.position, facility.transform.position) <= 1.6f)
                {
                    nearby++;
                }
                if (peer.currentFacility != null &&
                    Vector2.Distance(
                        peer.currentFacility.transform.position,
                        facility.transform.position) <= 0.5f)
                {
                    sameTarget++;
                }
            }

            liveQueueSeconds = sameTarget * 1.15f;
            return Mathf.Clamp01(nearby / 5f + sameTarget / 4f);
        }

        private bool IsExclusiveComfortSpotOccupied(
            UtilityJourneyFacilityPrototype facility)
        {
            if (facility == null || JourneyWorld == null ||
                (facility.WaitingStyle != UtilityJourneyWaitingStyle.BenchSeat &&
                 facility.Kind != UtilityJourneyFacilityKind.TrainRideSpot))
            {
                return false;
            }

            string socialSpace = JourneyWorld.CurrentSocialSpaceId;
            foreach (UtilityPassengerBrainPrototype peer in ActivePassengers)
            {
                if (peer == null || peer == this || !peer.isActiveAndEnabled ||
                    peer.JourneyWorld == null ||
                    !string.Equals(
                        peer.JourneyWorld.CurrentSocialSpaceId,
                        socialSpace,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (Vector2.Distance(peer.transform.position, facility.transform.position) <= 0.38f)
                {
                    return true;
                }
            }

            return false;
        }

        private float DistanceToCurrentFacility()
        {
            return currentFacility == null
                ? float.PositiveInfinity
                : Vector2.Distance(body.position, currentFacility.transform.position);
        }

        private float GetPassingSideSign()
        {
            if (Mathf.Abs(socialTuning.PassingSidePreference) > 0.05f)
            {
                return Mathf.Sign(socialTuning.PassingSidePreference);
            }

            return (personalitySeed & 1) == 0 ? 1f : -1f;
        }

        private bool TryBuildPath(
            UtilityJourneyArea area,
            Vector2 start,
            Vector2 destination,
            float acceptanceRadius,
            out List<Vector3> path,
            out float pathDistance)
        {
            path = new List<Vector3>();
            pathDistance = Vector2.Distance(start, destination);
            if (pathDistance <= acceptanceRadius)
            {
                path.Add(start);
                path.Add(destination);
                return true;
            }

            GridNavigation2D navigation = FindNavigation(area, start, destination);
            if (navigation == null)
            {
                return false;
            }

            if (area == UtilityJourneyArea.TrainInterior)
            {
                float aisleY = navigation.WorldBounds.center.y;
                Vector3 aisleFrom = new Vector3(start.x, aisleY, transform.position.z);
                Vector3 aisleTo = new Vector3(destination.x, aisleY, transform.position.z);
                path.Add(start);
                if (Vector2.Distance(path[path.Count - 1], aisleFrom) > 0.05f)
                {
                    path.Add(aisleFrom);
                }
                if (Vector2.Distance(path[path.Count - 1], aisleTo) > 0.05f)
                {
                    path.Add(aisleTo);
                }
                if (Vector2.Distance(path[path.Count - 1], destination) > 0.05f)
                {
                    path.Add(destination);
                }

                pathDistance = 0f;
                for (int index = 1; index < path.Count; index++)
                {
                    pathDistance += Vector2.Distance(path[index - 1], path[index]);
                }
                return path.Count > 1;
            }

            PathSearchCount++;
            path.AddRange(navigation.FindWorldPath(start, destination));
            if (path.Count == 0)
            {
                return false;
            }

            pathDistance = 0f;
            Vector2 previous = start;
            foreach (Vector3 point in path)
            {
                pathDistance += Vector2.Distance(previous, point);
                previous = point;
            }

            return true;
        }

        private GridNavigation2D FindNavigation(
            UtilityJourneyArea area,
            Vector2 start,
            Vector2 destination)
        {
            if (navigationBindings == null)
            {
                return null;
            }

            foreach (UtilityJourneyNavigationBinding binding in navigationBindings)
            {
                GridNavigation2D navigation = binding.Navigation;
                if (binding.Area == area &&
                    navigation != null &&
                    navigation.ContainsWorldPosition(start) &&
                    navigation.ContainsWorldPosition(destination))
                {
                    return navigation;
                }
            }

            return null;
        }

        private bool IsAtCurrentFacility()
        {
            return currentFacility != null &&
                   Vector2.Distance(
                       body.position,
                       currentFacility.transform.position) <=
                   GetEffectiveAcceptanceRadius(currentFacility);
        }

        private static float GetEffectiveAcceptanceRadius(
            UtilityJourneyFacilityPrototype facility)
        {
            if (facility == null)
            {
                return 0f;
            }

            switch (facility.Kind)
            {
                case UtilityJourneyFacilityKind.PlatformDownStair:
                case UtilityJourneyFacilityKind.PlatformUpStair:
                    return Mathf.Max(0.78f, facility.AcceptanceRadius);
                case UtilityJourneyFacilityKind.StreetExit:
                    return Mathf.Max(0.68f, facility.AcceptanceRadius);
                case UtilityJourneyFacilityKind.EntryGate:
                case UtilityJourneyFacilityKind.ExitGate:
                case UtilityJourneyFacilityKind.TrainDoor:
                    return Mathf.Max(0.48f, facility.AcceptanceRadius);
                default:
                    return facility.AcceptanceRadius;
            }
        }

        private bool IsContextuallyUsable(
            UtilityJourneyFacilityPrototype facility,
            UtilityPassengerFacts facts)
        {
            if (facility.Kind == UtilityJourneyFacilityKind.PlatformWaitingArea &&
                facility.WaitingStyle == UtilityJourneyWaitingStyle.WallRest)
            {
                GridNavigation2D platformNavigation = FindNavigation(
                    facts.Area,
                    facility.transform.position,
                    facility.transform.position);
                if (platformNavigation != null)
                {
                    float localX = Mathf.Abs(
                        facility.transform.position.x -
                        platformNavigation.WorldBounds.center.x);
                    if (localX < 6.25f)
                    {
                        return false;
                    }
                }
            }

            if (facts.Area == UtilityJourneyArea.TrainInterior &&
                !facts.IsSettledInsideTrain &&
                facility.Kind == UtilityJourneyFacilityKind.TrainRideSpot &&
                Vector2.Distance(body.position, facility.transform.position) <
                minimumTrainSettleDistance)
            {
                return false;
            }

            string group = facility.RouteGroup;
            if (facility.Kind == UtilityJourneyFacilityKind.ExitGate &&
                !string.IsNullOrEmpty(group) &&
                !string.IsNullOrEmpty(facts.PreferredExitId))
            {
                bool leftExit = facts.PreferredExitId == "exit-1" ||
                                facts.PreferredExitId == "exit-2";
                return string.Equals(
                    group,
                    leftExit ? "left" : "right",
                    StringComparison.Ordinal);
            }

            return true;
        }

        private static bool IsImmediateTraversal(
            UtilityJourneyFacilityPrototype facility)
        {
            return facility != null &&
                   (facility.Kind == UtilityJourneyFacilityKind.PlatformDownStair ||
                    facility.Kind == UtilityJourneyFacilityKind.PlatformUpStair);
        }

        private float GetStablePreference(UtilityJourneyFacilityPrototype facility)
        {
            float jitter = HashPreference(facility != null
                ? facility.FacilityId
                : string.Empty);
            if (facility == null)
            {
                return jitter;
            }

            if (facility.WaitingStyle != UtilityJourneyWaitingStyle.None)
            {
                if (activeProfile != null)
                {
                    float configured = activeProfile.GetStylePreference(
                        facility.WaitingStyle);
                    return Mathf.Clamp01(configured * 0.86f + jitter * 0.14f);
                }

                UtilityJourneyWaitingStyle preferredStyle = GetPreferredWaitingStyle();
                if (archetype == UtilityPassengerArchetype.Balanced)
                {
                    return 0.5f + jitter * 0.5f;
                }

                return facility.WaitingStyle == preferredStyle
                    ? 0.82f + jitter * 0.18f
                    : 0.08f + jitter * 0.34f;
            }

            if ((facility.Kind == UtilityJourneyFacilityKind.PlatformDownStair ||
                 facility.Kind == UtilityJourneyFacilityKind.PlatformUpStair) &&
                !string.IsNullOrEmpty(facility.RouteGroup))
            {
                string preferredSide = (personalitySeed & 1) == 0
                    ? "left"
                    : "right";
                return facility.RouteGroup == preferredSide
                    ? 0.78f + jitter * 0.22f
                    : 0.12f + jitter * 0.3f;
            }

            return jitter;
        }

        private float HashPreference(string facilityId)
        {
            unchecked
            {
                uint hash = (uint)personalitySeed;
                string value = facilityId ?? string.Empty;
                for (int index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 16777619u;
                }

                return (hash % 1000u) / 999f;
            }
        }

        private UtilityJourneyWaitingStyle GetPreferredWaitingStyle()
        {
            switch (archetype)
            {
                case UtilityPassengerArchetype.QueuePlanner:
                    return UtilityJourneyWaitingStyle.DoorQueue;
                case UtilityPassengerArchetype.SeatSeeker:
                    return UtilityJourneyWaitingStyle.BenchSeat;
                case UtilityPassengerArchetype.WallRelaxed:
                    return UtilityJourneyWaitingStyle.WallRest;
                case UtilityPassengerArchetype.QuickTransfer:
                    return UtilityJourneyWaitingStyle.DoorQueue;
                default:
                    return UtilityJourneyWaitingStyle.None;
            }
        }

        private void ApplyWaitingPose()
        {
            if (currentFacility == null)
            {
                RestoreStandingPose();
                return;
            }

            switch (currentFacility.WaitingStyle)
            {
                case UtilityJourneyWaitingStyle.BenchSeat:
                    transform.localScale = new Vector3(
                        standingScale.x,
                        standingScale.y * 0.68f,
                        standingScale.z);
                    transform.up = Vector2.up;
                    break;
                case UtilityJourneyWaitingStyle.WallRest:
                    transform.localScale = standingScale;
                    transform.up = Vector2.right;
                    break;
                default:
                    RestoreStandingPose();
                    break;
            }
        }

        private void RestoreStandingPose()
        {
            if (standingScale.sqrMagnitude > 0.0001f)
            {
                transform.localScale = standingScale;
            }
        }

        private void BuildTopCandidateSummary()
        {
            if (scoredFacilities.Count == 0)
            {
                topCandidateSummary = "No useful reachable facility";
                return;
            }

            int count = Mathf.Min(3, scoredFacilities.Count);
            var parts = new string[count];
            for (int index = 0; index < count; index++)
            {
                ScoredFacility item = scoredFacilities[index];
                parts[index] = item.Facility.FacilityId + " " +
                               item.Candidate.Score.ToString("0.0");
            }

            topCandidateSummary = string.Join("  >  ", parts);
        }

        private void UpdateThoughtText()
        {
            if (thoughtText == null)
            {
                return;
            }

            string thought = currentFacility == null
                ? "THINKING: looking for a useful reachable facility"
                : "THINKING: " + currentAction + " via " +
                  currentFacility.FacilityId +
                  "  score " + currentCandidate.Score.ToString("0.0") +
                  "\n" + currentCandidate.Reason;
            string motion = movementSuspended || currentFacility == null ||
                            IsAtCurrentFacility()
                ? "WAITING"
                : isRunning
                    ? "RUNNING"
                    : "WALKING";
            thoughtText.text = thought +
                               "\nPersonality: " + ActiveProfileName +
                               (currentFacility != null &&
                                currentFacility.WaitingStyle != UtilityJourneyWaitingStyle.None
                                   ? " / " + currentFacility.WaitingStyle
                                   : string.Empty) +
                               "\nMotion: " + motion +
                               "  speed " + currentSpeed.ToString("0.00") +
                               "\nCandidates: " + topCandidateSummary +
                               "\nDecisions: " + DecisionCount +
                               "  A* searches: " + PathSearchCount +
                               "  Completed actions: " + CompletedActionCount;
        }

        private void SelectAndApplyTuningProfile()
        {
            UtilityPassengerTuningProfile[] validProfiles = tuningProfiles == null
                ? Array.Empty<UtilityPassengerTuningProfile>()
                : Array.FindAll(tuningProfiles, item => item != null);
            if (validProfiles.Length == 0)
            {
                archetype = (UtilityPassengerArchetype)(personalitySeed % 4);
                activeProfile = null;
                socialTuning = UtilityPassengerSocialTuning.Default;
                return;
            }

            int index = personalitySeed % validProfiles.Length;
            activeProfile = validProfiles[index];
            archetype = activeProfile.Archetype;
            walkSpeed = activeProfile.WalkSpeed;
            runSpeed = Mathf.Max(walkSpeed, activeProfile.RunSpeed);
            acceleration = activeProfile.Acceleration;
            decisionInterval = activeProfile.DecisionInterval;
            repathInterval = activeProfile.RepathInterval;
            socialTuning = activeProfile.SocialTuning;
        }

        private void ClearDecision()
        {
            currentFacility = null;
            currentAction = UtilityJourneyAction.None;
            currentCandidate = default;
            activePath.Clear();
            pathIndex = 0;
            interactionStartedAt = -1f;
            isRunning = false;
            desiredMovementDirection = Vector2.zero;
            socialState = UtilityPassengerSocialState.FreeWalking;
        }

        private void EnsureBody()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }
        }
    }
}

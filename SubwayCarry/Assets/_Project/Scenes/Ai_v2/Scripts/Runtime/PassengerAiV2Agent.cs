using System.Collections.Generic;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.AI.V2
{
    /// <summary>
    /// 첫 V2 테스트용 승객이다. 전역 목표 방향과 근거리 Social Navigation,
    /// 물리 이동을 분리하고 인식은 승객별로 분산된 저주기로 실행한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
    public sealed class PassengerAiV2Agent : MonoBehaviour, IPackageImpactSource
    {
        [Header("Perception")]
        [SerializeField, Range(2f, 30f)] private float perceptionHz = 12f;
        [SerializeField, Min(1f)] private float neighborRadius = 4f;
        [SerializeField, Min(0.1f)] private float collisionHorizon = 2.2f;

        [Header("Movement")]
        [SerializeField, Min(0.2f)] private float preferredSpeed = 1.5f;
        [SerializeField, Min(0.1f)] private float acceleration = 4.5f;
        [SerializeField, Min(0.1f)] private float bodyRadius = 0.42f;
        [SerializeField, Min(0f)] private float personalSpace = 0.22f;
        [SerializeField, Range(0f, 1f)] private float courtesy = 0.65f;
        [SerializeField, Range(0f, 1f)] private float assertiveness = 0.5f;
        [SerializeField, Range(0f, 1f)] private float crowdTolerance = 0.5f;

        [Header("Local Avoidance")]
        [SerializeField, Range(0f, 0.5f)] private float rightHandPreference = 0.14f;
        [SerializeField, Range(0.25f, 1.2f)] private float candidateLateralStrength = 0.68f;
        [SerializeField, Range(0.2f, 2f)] private float avoidanceCommitmentTime = 0.9f;
        [SerializeField, Range(0f, 2f)] private float avoidanceSwitchAdvantage = 0.65f;

        [Header("Emergent Corridor Flow")]
        [SerializeField, Range(0f, 2f)] private float sameFlowCohesionWeight = 0.72f;
        [SerializeField, Range(0f, 1.5f)] private float leaderFollowWeight = 0.52f;
        [SerializeField, Range(0f, 5f)] private float opposingFlowChannelWeight = 2.35f;
        [SerializeField, Range(0.5f, 2f)] private float flowChannelWidth = 1.15f;
        [SerializeField, Range(0.2f, 3f)] private float flowMemoryDuration = 1.65f;

        private readonly List<PassengerAiV2Agent> neighbors = new List<PassengerAiV2Agent>(16);
        private PassengerAiV2CrowdManager crowdManager;
        private Rigidbody2D body;
        private SpriteRenderer bodyRenderer;
        private Transform facingMarker;
        private Vector2 loopStartPosition;
        private Vector2 loopGoalPosition;
        private Vector2 movementTarget;
        private Vector2 lastTravelDirection = Vector2.right;
        private Vector2 velocity;
        private Vector2 desiredVelocity;
        private float nextPerceptionTime;
        private float stuckDuration;
        private float avoidanceCommitmentUntil;
        private float rememberedFlowLateral;
        private float flowMemoryConfidence;
        private float flowMemoryUntil;
        private float movementSpeedScale = 1f;
        private Rect movementBounds;
        private Rect emergentFlowBounds;
        private Vector2 emergentFlowAxis = Vector2.up;
        private int committedAvoidanceSide;
        private int leftAvoidanceSelectionCount;
        private int straightAvoidanceSelectionCount;
        private int rightAvoidanceSelectionCount;
        private bool autoLoop = true;
        private bool holdPosition;
        private bool emergentCorridorFlowEnabled;
        // Opt-in integration hooks. Existing 01-06 scenarios keep their original motor.
        private System.Func<Vector2, Vector2, Vector2> steeringTargetResolver;
        private System.Func<Vector2, Vector2, float, Vector2> movementConstraint;
        private Transform externalMotionSource;
        private Vector2 previousExternalPosition;

        public Vector2 Position => body != null ? body.position : (Vector2)transform.position;
        public Vector2 Velocity => velocity;
        public Vector2 ImpactVelocity => velocity;
        public Vector2 TravelDirection
        {
            get
            {
                if (externalMotionSource != null)
                    return velocity.sqrMagnitude > 0.001f ? velocity.normalized : lastTravelDirection;
                Vector2 direction = movementTarget - Position;
                return direction.sqrMagnitude > 0.001f ? direction.normalized : lastTravelDirection;
            }
        }
        public float Radius => bodyRadius;
        public bool IsDeadlocked => stuckDuration >= 3f;
        public string PersonalityId { get; private set; }
        public int AvoidanceSide => committedAvoidanceSide;
        public int LeftAvoidanceSelectionCount => leftAvoidanceSelectionCount;
        public int StraightAvoidanceSelectionCount => straightAvoidanceSelectionCount;
        public int RightAvoidanceSelectionCount => rightAvoidanceSelectionCount;
        public Vector2 MovementTarget => movementTarget;
        public float DistanceToTarget => Vector2.Distance(Position, movementTarget);
        public bool HasReachedTarget => (Position - movementTarget).sqrMagnitude <= 0.2f;

        public void Initialize(
            PassengerAiV2CrowdManager manager,
            Vector2 start,
            Vector2 goal,
            PassengerAiV2RuntimePersonality personality,
            float stagger01,
            float minY,
            float maxY,
            Sprite sharedSprite,
            Color color)
        {
            crowdManager = manager;
            loopStartPosition = start;
            loopGoalPosition = goal;
            movementTarget = goal;
            lastTravelDirection = (goal - start).sqrMagnitude > 0.001f
                ? (goal - start).normalized
                : Vector2.right;
            PersonalityId = personality.ProfileId;
            preferredSpeed = Mathf.Max(0.2f, personality.WalkingSpeed);
            personalSpace = Mathf.Max(0.05f, personality.PersonalSpace);
            courtesy = Mathf.Clamp01(personality.Courtesy);
            assertiveness = Mathf.Clamp01(personality.Assertiveness);
            crowdTolerance = Mathf.Clamp01(personality.CrowdTolerance);
            perceptionHz = Mathf.Clamp(1f / Mathf.Max(0.05f, personality.ReactionDelay), 4f, 20f);
            movementBounds = Rect.MinMaxRect(-1000f, minY, 1000f, maxY);

            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.position = start;

            CircleCollider2D circle = GetComponent<CircleCollider2D>();
            circle.radius = bodyRadius;
            circle.isTrigger = true;

            bodyRenderer = GetComponent<SpriteRenderer>();
            bodyRenderer.sprite = sharedSprite;
            bodyRenderer.color = color;
            bodyRenderer.sortingOrder = 10;
            transform.localScale = new Vector3(bodyRadius * 1.65f, bodyRadius * 1.65f, 1f);

            GameObject marker = new GameObject("FacingDirection");
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = new Vector3(0.58f, 0f, 0f);
            marker.transform.localScale = new Vector3(0.46f, 0.18f, 1f);
            SpriteRenderer markerRenderer = marker.AddComponent<SpriteRenderer>();
            markerRenderer.sprite = sharedSprite;
            markerRenderer.color = personality.DebugColor;
            markerRenderer.sortingOrder = 11;
            facingMarker = marker.transform;

            float interval = 1f / Mathf.Max(2f, perceptionHz);
            nextPerceptionTime = Time.time + interval * Mathf.Clamp01(stagger01);
            desiredVelocity = TravelDirection * preferredSpeed * movementSpeedScale;
            velocity = desiredVelocity;
            crowdManager.Register(this);
        }

        public void SetAutoLoop(bool enabled)
        {
            autoLoop = enabled;
        }

        public void ConfigureMovementSpace(
            System.Func<Vector2, Vector2, Vector2> resolveSteeringTarget,
            System.Func<Vector2, Vector2, float, Vector2> constrainMove,
            float worldRadius)
        {
            steeringTargetResolver = resolveSteeringTarget;
            movementConstraint = constrainMove;
            bodyRadius = Mathf.Max(0.1f, worldRadius);
        }

        // A non-rendered participant lets the existing spatial hash perceive a human-controlled player.
        public void FollowExternalMotion(Transform source, float worldRadius)
        {
            externalMotionSource = source;
            bodyRadius = Mathf.Max(0.1f, worldRadius);
            previousExternalPosition = source.position;
            body.position = previousExternalPosition;
            velocity = desiredVelocity = Vector2.zero;
            bodyRenderer.enabled = false;
            GetComponent<CircleCollider2D>().enabled = false;
            if (facingMarker != null) facingMarker.gameObject.SetActive(false);
            autoLoop = false;
        }

        public void SetMovementBounds(Rect bounds)
        {
            movementBounds = bounds;
        }

        public void SetMovementSpeedScale(float scale)
        {
            movementSpeedScale = Mathf.Clamp(scale, 0.35f, 2f);
        }

        public void SetEmergentCorridorFlow(Rect bounds, Vector2 corridorAxis)
        {
            emergentCorridorFlowEnabled = true;
            emergentFlowBounds = bounds;
            emergentFlowAxis = corridorAxis.sqrMagnitude > 0.001f
                ? corridorAxis.normalized
                : Vector2.up;
            flowMemoryConfidence = 0f;
            flowMemoryUntil = 0f;
        }

        public void ClearEmergentCorridorFlow()
        {
            emergentCorridorFlowEnabled = false;
            flowMemoryConfidence = 0f;
            flowMemoryUntil = 0f;
        }

        public void SetMovementTarget(Vector2 target)
        {
            Vector2 direction = target - Position;
            if (direction.sqrMagnitude > 0.001f)
            {
                lastTravelDirection = direction.normalized;
            }

            movementTarget = target;
            committedAvoidanceSide = 0;
            avoidanceCommitmentUntil = 0f;
            holdPosition = false;
            stuckDuration = 0f;
        }

        public void SetFollowingTarget(Vector2 target)
        {
            Vector2 direction = target - Position;
            if (direction.sqrMagnitude > 0.001f)
            {
                lastTravelDirection = direction.normalized;
            }

            movementTarget = target;
            holdPosition = false;
        }

        public void SetHoldPosition(bool shouldHold)
        {
            holdPosition = shouldHold;
            if (shouldHold)
            {
                movementTarget = Position;
                desiredVelocity = Vector2.zero;
            }
        }

        public void TeleportAndSetTarget(Vector2 position, Vector2 target)
        {
            body.position = position;
            movementTarget = target;
            Vector2 direction = target - position;
            lastTravelDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : lastTravelDirection;
            velocity = lastTravelDirection * preferredSpeed * movementSpeedScale * 0.65f;
            desiredVelocity = velocity;
            committedAvoidanceSide = 0;
            avoidanceCommitmentUntil = 0f;
            holdPosition = false;
            stuckDuration = 0f;
        }

        private void OnDestroy()
        {
            if (crowdManager != null)
            {
                crowdManager.Unregister(this);
            }
        }

        private void Update()
        {
            if (externalMotionSource != null || crowdManager == null || holdPosition || Time.time < nextPerceptionTime)
            {
                return;
            }

            float interval = 1f / Mathf.Max(2f, perceptionHz);
            nextPerceptionTime = Time.time + interval;
            EvaluateSocialVelocity();
        }

        private void FixedUpdate()
        {
            if (body == null)
            {
                return;
            }

            if (externalMotionSource != null)
            {
                Vector2 position = externalMotionSource.position;
                Vector2 measured = (position - previousExternalPosition) / Mathf.Max(0.001f, Time.fixedDeltaTime);
                velocity = measured.sqrMagnitude > 100f ? Vector2.zero : measured;
                previousExternalPosition = position;
                body.position = position;
                movementTarget = position + velocity;
                if (velocity.sqrMagnitude > 0.001f) lastTravelDirection = velocity.normalized;
                return;
            }

            velocity = Vector2.MoveTowards(velocity, desiredVelocity, acceleration * Time.fixedDeltaTime);
            Vector2 next = body.position + velocity * Time.fixedDeltaTime;
            next.x = Mathf.Clamp(next.x, movementBounds.xMin + bodyRadius, movementBounds.xMax - bodyRadius);
            next.y = Mathf.Clamp(next.y, movementBounds.yMin + bodyRadius, movementBounds.yMax - bodyRadius);
            if (movementConstraint != null)
            {
                next = movementConstraint(body.position, next, bodyRadius);
                velocity = (next - body.position) / Mathf.Max(0.001f, Time.fixedDeltaTime);
            }
            body.MovePosition(next);

            if (facingMarker != null && velocity.sqrMagnitude > 0.01f)
            {
                facingMarker.localPosition = transform.InverseTransformVector(velocity.normalized) * 0.58f;
            }

            float remaining = Vector2.Distance(movementTarget, body.position);
            bool movingMeaningfully = velocity.magnitude
                > preferredSpeed * movementSpeedScale * 0.18f;
            stuckDuration = remaining > 1f && !movingMeaningfully
                ? stuckDuration + Time.fixedDeltaTime
                : Mathf.Max(0f, stuckDuration - Time.fixedDeltaTime * 2f);

            if (holdPosition)
            {
                desiredVelocity = Vector2.zero;
                return;
            }

            if (remaining <= 0.45f)
            {
                if (autoLoop)
                {
                    body.position = loopStartPosition;
                    movementTarget = loopGoalPosition;
                    lastTravelDirection = (loopGoalPosition - loopStartPosition).normalized;
                    velocity = lastTravelDirection * preferredSpeed * movementSpeedScale * 0.65f;
                    desiredVelocity = velocity;
                    committedAvoidanceSide = 0;
                    avoidanceCommitmentUntil = 0f;
                    stuckDuration = 0f;
                }
                else
                {
                    desiredVelocity = Vector2.zero;
                }
            }
        }

        private void EvaluateSocialVelocity()
        {
            Vector2 position = body.position;
            Vector2 steeringTarget = steeringTargetResolver != null
                ? steeringTargetResolver(position, movementTarget) : movementTarget;
            Vector2 toTarget = steeringTarget - position;
            if (toTarget.sqrMagnitude <= 0.001f)
            {
                desiredVelocity = Vector2.zero;
                return;
            }

            Vector2 forward = toTarget.normalized;
            lastTravelDirection = forward;
            float activePreferredSpeed = preferredSpeed * movementSpeedScale;
            float selectedSpeed = activePreferredSpeed;
            Vector2 avoidance = Vector2.zero;
            float strongestAvoidanceUrgency = 0f;
            float strongestHeadOnFactor = 0f;
            bool avoidanceNeeded = false;

            crowdManager.QueryNearby(position, neighborRadius, this, neighbors);
            bool useEmergentFlow = IsEmergentFlowActive(position);
            UpdateEmergentFlowMemory(position, forward, useEmergentFlow);
            float density01 = Mathf.Clamp01(neighbors.Count / 10f);
            float adaptivePersonalSpace = personalSpace * Mathf.Lerp(1f, 0.4f, density01 * crowdTolerance);
            for (int i = 0; i < neighbors.Count; i++)
            {
                PassengerAiV2Agent other = neighbors[i];
                Vector2 relativePosition = other.Position - position;
                float distanceSquared = relativePosition.sqrMagnitude;
                if (distanceSquared < 0.0001f)
                {
                    continue;
                }

                float distance = Mathf.Sqrt(distanceSquared);
                Vector2 otherDirection = other.TravelDirection;
                float directionAgreement = Vector2.Dot(forward, otherDirection);
                float ahead = Vector2.Dot(relativePosition, forward);
                float lateralDistance = Mathf.Abs(
                    forward.x * relativePosition.y - forward.y * relativePosition.x);
                float sameFlowWidth = bodyRadius
                    + other.Radius
                    + adaptivePersonalSpace * 0.65f;

                // 옆 Lane의 승객을 내 앞사람으로 오인해 두 줄 전체가 연쇄 감속하지 않도록 한다.
                if (directionAgreement > 0.65f
                    && ahead > 0f
                    && lateralDistance < sameFlowWidth)
                {
                    float safeGap = bodyRadius + other.Radius + adaptivePersonalSpace + 0.45f;
                    if (distance < safeGap * 2f)
                    {
                        float flowSpeed = Mathf.Max(0.35f, other.Velocity.magnitude);
                        float gapRecovery = Mathf.Max(0f, distance - safeGap) * 0.7f;
                        selectedSpeed = Mathf.Min(selectedSpeed, flowSpeed + gapRecovery);
                    }
                }

                Vector2 relativeVelocity = other.Velocity - forward * selectedSpeed;
                float relativeSpeedSquared = relativeVelocity.sqrMagnitude;
                if (relativeSpeedSquared > 0.001f)
                {
                    float timeToClosest = -Vector2.Dot(relativePosition, relativeVelocity) / relativeSpeedSquared;
                    if (timeToClosest > 0f && timeToClosest < collisionHorizon)
                    {
                        Vector2 closestOffset = relativePosition + relativeVelocity * timeToClosest;
                        float comfortRadius = bodyRadius + other.Radius + adaptivePersonalSpace;
                        if (closestOffset.sqrMagnitude < comfortRadius * comfortRadius)
                        {
                            float urgency = 1f - timeToClosest / collisionHorizon;
                            float headOnFactor = Mathf.InverseLerp(0.4f, -1f, directionAgreement);
                            avoidanceNeeded = true;
                            strongestHeadOnFactor = Mathf.Max(strongestHeadOnFactor, headOnFactor);
                            strongestAvoidanceUrgency = Mathf.Max(
                                strongestAvoidanceUrgency,
                                urgency * Mathf.Lerp(0.55f, 1f, headOnFactor));
                            float yieldSpeed = Mathf.Lerp(0.7f, 0.9f, assertiveness);
                            selectedSpeed *= Mathf.Lerp(0.96f, yieldSpeed, urgency);
                        }
                    }
                }

                float overlapDistance = bodyRadius + other.Radius;
                if (distance < overlapDistance)
                {
                    float penetration = (overlapDistance - distance) / Mathf.Max(0.01f, overlapDistance);
                    avoidance -= relativePosition / distance * penetration * 0.65f;
                }
            }

            if (avoidanceNeeded)
            {
                int selectedSide = SelectAvoidanceSide(
                    position,
                    forward,
                    selectedSpeed,
                    adaptivePersonalSpace,
                    strongestHeadOnFactor > 0.35f);
                if (selectedSide == 0)
                {
                    selectedSpeed *= Mathf.Lerp(0.9f, 0.62f, strongestAvoidanceUrgency);
                }
                else
                {
                    Vector2 rightHandSide = new Vector2(forward.y, -forward.x);
                    float strength = Mathf.Lerp(0.24f, 1.05f, strongestAvoidanceUrgency);
                    avoidance += rightHandSide
                        * selectedSide
                        * strength
                        * Mathf.Lerp(0.75f, 1.1f, courtesy);
                }
            }
            else if (Time.time >= avoidanceCommitmentUntil)
            {
                committedAvoidanceSide = 0;
            }

            if (useEmergentFlow && flowMemoryConfidence > 0.01f)
            {
                Vector2 lateralAxis = new Vector2(emergentFlowAxis.y, -emergentFlowAxis.x);
                float currentLateral = Vector2.Dot(position, lateralAxis);
                float flowError = rememberedFlowLateral - currentLateral;
                avoidance += lateralAxis
                    * Mathf.Clamp(flowError / Mathf.Max(0.1f, flowChannelWidth), -1f, 1f)
                    * 0.44f
                    * flowMemoryConfidence;
            }

            Vector2 combinedDirection = forward + avoidance;
            if (combinedDirection.sqrMagnitude < 0.01f)
            {
                combinedDirection = forward;
            }

            float normalMinimum = activePreferredSpeed * Mathf.Lerp(0.16f, 0.34f, assertiveness);
            float minimumFlowSpeed = IsDeadlocked ? activePreferredSpeed * 0.55f : normalMinimum;
            selectedSpeed = Mathf.Clamp(selectedSpeed, minimumFlowSpeed, activePreferredSpeed);
            desiredVelocity = combinedDirection.normalized * selectedSpeed;
        }

        private int SelectAvoidanceSide(
            Vector2 position,
            Vector2 forward,
            float speed,
            float adaptivePersonalSpace,
            bool requireLateralAvoidance)
        {
            float leftScore = ScoreAvoidanceCandidate(
                position,
                forward,
                speed,
                adaptivePersonalSpace,
                -1);
            float straightScore = ScoreAvoidanceCandidate(
                position,
                forward,
                speed,
                adaptivePersonalSpace,
                0);
            float rightScore = ScoreAvoidanceCandidate(
                position,
                forward,
                speed,
                adaptivePersonalSpace,
                1);
            if (requireLateralAvoidance)
            {
                straightScore += 6f;
            }

            int bestSide = 0;
            float bestScore = straightScore;
            if (leftScore < bestScore)
            {
                bestSide = -1;
                bestScore = leftScore;
            }

            if (rightScore < bestScore)
            {
                bestSide = 1;
                bestScore = rightScore;
            }

            if (committedAvoidanceSide != 0 && Time.time < avoidanceCommitmentUntil)
            {
                float committedScore = committedAvoidanceSide < 0 ? leftScore : rightScore;
                if (bestScore + avoidanceSwitchAdvantage >= committedScore)
                {
                    bestSide = committedAvoidanceSide;
                }
            }

            if (bestSide != committedAvoidanceSide || Time.time >= avoidanceCommitmentUntil)
            {
                committedAvoidanceSide = bestSide;
                avoidanceCommitmentUntil = Time.time + avoidanceCommitmentTime;
            }

            if (bestSide < 0)
            {
                leftAvoidanceSelectionCount++;
            }
            else if (bestSide > 0)
            {
                rightAvoidanceSelectionCount++;
            }
            else
            {
                straightAvoidanceSelectionCount++;
            }

            return bestSide;
        }

        private float ScoreAvoidanceCandidate(
            Vector2 position,
            Vector2 forward,
            float speed,
            float adaptivePersonalSpace,
            int side)
        {
            Vector2 right = new Vector2(forward.y, -forward.x);
            Vector2 candidateDirection =
                (forward + right * side * candidateLateralStrength).normalized;
            Vector2 candidateVelocity = candidateDirection * speed;
            float score = 0f;

            if (velocity.sqrMagnitude > 0.01f)
            {
                score += (1f - Vector2.Dot(velocity.normalized, candidateDirection)) * 0.7f;
            }

            Vector2 projectedPosition = position + candidateVelocity * 1.15f;
            score += Vector2.Distance(projectedPosition, movementTarget) * 0.035f;
            score += ScoreBounds(projectedPosition);
            bool useEmergentFlow = IsEmergentFlowActive(position);
            if (!useEmergentFlow && side > 0)
            {
                score -= rightHandPreference;
            }

            Vector2 flowLateralAxis = Vector2.zero;
            Vector2 flowSelfFuture = Vector2.zero;
            if (useEmergentFlow)
            {
                flowLateralAxis = new Vector2(emergentFlowAxis.y, -emergentFlowAxis.x);
                flowSelfFuture = position + candidateVelocity * 1.05f;
                if (flowMemoryConfidence > 0.01f)
                {
                    float candidateLateral = Vector2.Dot(projectedPosition, flowLateralAxis);
                    score += Mathf.Abs(candidateLateral - rememberedFlowLateral)
                        * sameFlowCohesionWeight
                        * flowMemoryConfidence;
                }
            }

            for (int i = 0; i < neighbors.Count; i++)
            {
                PassengerAiV2Agent other = neighbors[i];
                Vector2 relativePosition = other.Position - position;
                float forwardDistance = Vector2.Dot(relativePosition, forward);
                float sideDistance = Vector2.Dot(relativePosition, right) * side;
                if (side != 0
                    && forwardDistance > -0.25f
                    && forwardDistance < neighborRadius
                    && sideDistance > -0.2f)
                {
                    float distanceWeight =
                        1f - Mathf.Clamp01(forwardDistance / neighborRadius);
                    float laneWeight =
                        1f - Mathf.Clamp01(Mathf.Abs(sideDistance - 0.85f) / 2f);
                    score += distanceWeight * laneWeight * 0.7f;
                }

                score += ScorePredictedSeparation(
                    position,
                    candidateVelocity,
                    other,
                    adaptivePersonalSpace,
                    0.45f,
                    3.2f);
                score += ScorePredictedSeparation(
                    position,
                    candidateVelocity,
                    other,
                    adaptivePersonalSpace,
                    1.1f,
                    1.7f);
                score += ScorePredictedSeparation(
                    position,
                    candidateVelocity,
                    other,
                    adaptivePersonalSpace,
                    2f,
                    0.8f);

                if (useEmergentFlow)
                {
                    float distance = relativePosition.magnitude;
                    if (distance < neighborRadius)
                    {
                        float proximityWeight = 1f - distance / Mathf.Max(0.01f, neighborRadius);
                        float directionAgreement = Vector2.Dot(forward, other.TravelDirection);
                        if (directionAgreement < -0.35f)
                        {
                            Vector2 otherFuture = other.Position + other.Velocity * 1.05f;
                            Vector2 futureOffset = otherFuture - flowSelfFuture;
                            float lateralSeparation =
                                Mathf.Abs(Vector2.Dot(futureOffset, flowLateralAxis));
                            float sameChannel = 1f - Mathf.Clamp01(
                                lateralSeparation / Mathf.Max(0.1f, flowChannelWidth));
                            score += sameChannel * proximityWeight * opposingFlowChannelWeight;
                        }
                        else if (directionAgreement > 0.55f
                            && forwardDistance > -0.25f
                            && other.Velocity.sqrMagnitude > 0.01f)
                        {
                            float alignment = Vector2.Dot(candidateDirection, other.Velocity.normalized);
                            float leaderWeight = Mathf.Lerp(
                                0.45f,
                                1f,
                                Mathf.Clamp01(forwardDistance / Mathf.Max(0.1f, neighborRadius)));
                            score += (1f - alignment)
                                * proximityWeight
                                * leaderWeight
                                * leaderFollowWeight;
                        }
                    }
                }
            }

            return score;
        }

        private bool IsEmergentFlowActive(Vector2 position)
        {
            return emergentCorridorFlowEnabled && emergentFlowBounds.Contains(position);
        }

        private void UpdateEmergentFlowMemory(Vector2 position, Vector2 forward, bool useEmergentFlow)
        {
            if (!useEmergentFlow)
            {
                if (Time.time >= flowMemoryUntil)
                {
                    flowMemoryConfidence = 0f;
                }

                return;
            }

            Vector2 lateralAxis = new Vector2(emergentFlowAxis.y, -emergentFlowAxis.x);
            float weightedLateral = 0f;
            float totalWeight = 0f;
            for (int i = 0; i < neighbors.Count; i++)
            {
                PassengerAiV2Agent other = neighbors[i];
                float directionAgreement = Vector2.Dot(forward, other.TravelDirection);
                if (directionAgreement < 0.55f)
                {
                    continue;
                }

                Vector2 offset = other.Position - position;
                float distance = offset.magnitude;
                if (distance >= neighborRadius)
                {
                    continue;
                }

                float ahead = Vector2.Dot(offset, forward);
                if (ahead < -0.45f)
                {
                    continue;
                }

                float lateralDistance = Mathf.Abs(Vector2.Dot(offset, lateralAxis));
                if (lateralDistance > flowChannelWidth * 1.8f)
                {
                    continue;
                }

                float proximityWeight = 1f - distance / Mathf.Max(0.01f, neighborRadius);
                float directionWeight = Mathf.InverseLerp(0.55f, 1f, directionAgreement);
                float aheadWeight = Mathf.Lerp(
                    0.28f,
                    1f,
                    Mathf.Clamp01((ahead + 0.45f) / Mathf.Max(0.1f, neighborRadius)));
                float weight = proximityWeight
                    * Mathf.Lerp(0.55f, 1f, directionWeight)
                    * aheadWeight;
                Vector2 predictedOther = other.Position + other.Velocity * 0.8f;
                weightedLateral += Vector2.Dot(predictedOther, lateralAxis) * weight;
                totalWeight += weight;
            }

            if (totalWeight > 0.05f)
            {
                float observedLateral = weightedLateral / totalWeight;
                if (flowMemoryConfidence <= 0.01f)
                {
                    rememberedFlowLateral = observedLateral;
                }
                else
                {
                    rememberedFlowLateral = Mathf.Lerp(rememberedFlowLateral, observedLateral, 0.34f);
                }

                flowMemoryConfidence = Mathf.Clamp01(totalWeight / 1.4f);
                flowMemoryUntil = Time.time + flowMemoryDuration;
            }
            else if (Time.time >= flowMemoryUntil)
            {
                flowMemoryConfidence = 0f;
            }
        }

        private float ScorePredictedSeparation(
            Vector2 position,
            Vector2 candidateVelocity,
            PassengerAiV2Agent other,
            float adaptivePersonalSpace,
            float predictionTime,
            float weight)
        {
            Vector2 selfFuture = position + candidateVelocity * predictionTime;
            Vector2 otherFuture = other.Position + other.Velocity * predictionTime;
            float separation = Vector2.Distance(selfFuture, otherFuture);
            float comfortDistance = bodyRadius + other.Radius + adaptivePersonalSpace;
            float evaluationDistance = comfortDistance * 2f;
            if (separation >= evaluationDistance)
            {
                return 0f;
            }

            float danger = 1f - separation / Mathf.Max(0.01f, evaluationDistance);
            float opposingFlow =
                Vector2.Dot(candidateVelocity.normalized, other.TravelDirection) < 0f
                    ? 1.3f
                    : 1f;
            return danger * danger * weight * opposingFlow;
        }

        private float ScoreBounds(Vector2 projectedPosition)
        {
            float minX = movementBounds.xMin + bodyRadius;
            float maxX = movementBounds.xMax - bodyRadius;
            float minY = movementBounds.yMin + bodyRadius;
            float maxY = movementBounds.yMax - bodyRadius;
            float outside = 0f;
            if (projectedPosition.x < minX)
            {
                outside += minX - projectedPosition.x;
            }
            else if (projectedPosition.x > maxX)
            {
                outside += projectedPosition.x - maxX;
            }

            if (projectedPosition.y < minY)
            {
                outside += minY - projectedPosition.y;
            }
            else if (projectedPosition.y > maxY)
            {
                outside += projectedPosition.y - maxY;
            }

            return outside * 12f;
        }
    }
}

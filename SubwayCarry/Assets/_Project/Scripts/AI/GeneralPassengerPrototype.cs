using System.Collections.Generic;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class GeneralPassengerPrototype : MonoBehaviour, IPackageImpactSource
    {
        private static Mesh passengerBodyMesh;
        private static Mesh directVisionMesh;
        private static Mesh peripheralVisionMesh;
        private static Material directVisionMaterial;
        private static Material peripheralVisionMaterial;

        private enum PassengerState
        {
            ApproachingPlatform,
            WaitingOutside,
            Boarding,
            ChoosingBehavior,
            MovingToActivity,
            Observing,
            Yielding,
            WaitingToStand,
            PreparingToExit,
            WaitingAtExitDoor,
            Exiting,
            LeavingPlatform,
            LeavingStation,
            Transferring
        }

        private enum PassengerBehavior
        {
            None,
            Seated,
            Handhold,
            Leaning,
            DoorStanding,
            AisleStanding,
            Exiting
        }

        private struct ObservedSeat
        {
            public PassengerSeatPrototype Seat;
            public Transform SittingPoint;

            public ObservedSeat(PassengerSeatPrototype seat, Transform sittingPoint)
            {
                Seat = seat;
                SittingPoint = sittingPoint;
            }
        }

        [SerializeField] private Transform mapRoot;
        [SerializeField] private PassengerDoorway boardingDoorway;
        [SerializeField] private bool randomizeBoardingDoorway = true;
        [SerializeField] private PassengerDoorway[] doorways;
        [SerializeField] private TrainDoorCyclePrototype doorCycle;
        [SerializeField] private PassengerStationJourneyPrototype stationJourney;
        [SerializeField] private GridNavigation2D[] navigationAreas;
        [SerializeField] private PassengerSeatPrototype[] seats;
        [SerializeField] private PassengerActivityPoint[] activityPoints;
        [SerializeField] private TextMesh stateLabel;
        [SerializeField, Range(30f, 160f)] private float directVisionAngle = 120f;
        [SerializeField, Range(120f, 240f)] private float peripheralVisionAngle = 180f;
        [SerializeField, Min(0.5f)] private float directVisionDistance = 4.8f;
        [SerializeField, Min(0.5f)] private float peripheralVisionDistance = 3.2f;
        [SerializeField, Min(0.05f)] private float openDoorVisionInterval = 0.28f;
        [SerializeField, Min(0.1f)] private float closedDoorVisionInterval = 0.85f;
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.8f;
        [SerializeField, Min(1f)] private float minimumDecisionInterval = 6f;
        [SerializeField, Min(1f)] private float maximumDecisionInterval = 10f;
        [SerializeField, Min(1f)] private float exitPreparationLeadTime = 7f;
        [SerializeField, Min(1)] private int minimumRideStops = 1;
        [SerializeField, Min(1)] private int maximumRideStops = 3;
        [SerializeField, Range(0f, 1f)] private float seatRecheckChance = 0.08f;
        [SerializeField, Range(0f, 1f)] private float initialSeatPursuitChance = 0.86f;
        [SerializeField, Min(0.1f)] private float seatApproachCrowdRadius = 1.35f;
        [SerializeField, Min(0.1f)] private float seatApproachCrowdLimit = 3.4f;
        [SerializeField, Min(0.1f)] private float seatCrowdRecheckInterval = 0.4f;
        [SerializeField, Min(0.1f)] private float avoidanceLookAhead = 0.8f;
        [SerializeField, Range(0.1f, 0.9f)] private float avoidanceLateralStrength = 0.45f;
        [SerializeField, Min(0.05f)] private float avoidanceSideHoldDuration = 0.35f;
        [SerializeField, Min(0.05f)] private float movementIntentMemory = 0.3f;
        [SerializeField, Range(0f, 1f)] private float leisurelyPassengerChance = 0.25f;
        [SerializeField, Range(0f, 1f)] private float hurriedPassengerChance = 0.2f;
        [SerializeField, Min(0.1f)] private float exitWaitingAcceptanceRadius = 0.4f;
        [SerializeField, Min(0.1f)] private float waitingPersonalSpaceRadius = 0.56f;
        [SerializeField, Min(0.1f)] private float waitingSeparationSpeed = 0.35f;
        [SerializeField, Min(0.1f)] private float maximumWaitingDrift = 0.24f;
        [SerializeField, Min(0.1f)] private float blockedActivityTimeout = 2.2f;
        [SerializeField, Min(0.05f)] private float blockedWaitDuration = 0.35f;
        [SerializeField, Min(0.1f)] private float blockedSqueezeMinimumDuration = 1f;
        [SerializeField, Min(0.1f)] private float blockedSqueezeMaximumDuration = 1.5f;
        [SerializeField, Range(0f, 1f)] private float squeezeThroughChance = 0.25f;
        [SerializeField, Range(0.1f, 0.8f)] private float squeezeSpeedMultiplier = 0.4f;
        [SerializeField, Min(0.1f)] private float softOverlapRadius = 0.65f;
        [SerializeField, Min(0.1f)] private float stationaryRouteClearance = 0.7f;
        [SerializeField, Min(0.1f)] private float courtesyStepDistance = 0.65f;
        [SerializeField, Min(0.01f)] private float courtesyRouteClearanceGain = 0.12f;
        [SerializeField, Min(0.1f)] private float exitIntentCorridorWidth = 0.78f;
        [SerializeField, Min(0.1f)] private float standingPersonalSpaceRadius = 0.62f;
        [SerializeField, Min(0.1f)] private float standingYieldDetectionRadius = 1.45f;
        [SerializeField, Min(0.1f)] private float obstructionYieldThreshold = 0.9f;
        [SerializeField, Min(0.01f)] private float obstructionScoreDecay = 0.35f;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.08f;
        [SerializeField, Min(0.01f)] private float activityClaimLeadTime = 0.1f;
        [SerializeField, Min(0.1f)] private float activityClaimRadius = 0.42f;
        [SerializeField, Min(0.1f)] private float crowdEscapeThreshold = 7.5f;
        [SerializeField, Min(0.1f)] private float crowdEscapeMinimumImprovement = 1.2f;
        [SerializeField, Min(0.1f)] private float crowdEscapeCheckInterval = 2f;
        [SerializeField, Min(0.1f)] private float crowdEscapeCooldown = 12f;
        [SerializeField, Range(0f, 1f)] private float crowdEscapeChance = 0.08f;
        [SerializeField, Min(0.1f)] private float wallLeanReleaseDistance = 0.72f;
        [SerializeField, Min(0.1f)] private float nearbyHandholdDistance = 0.75f;
        [SerializeField, Min(0f)] private float settledComfortGap = 0.22f;
        [SerializeField, Min(0.1f)] private float crowdPressureRadius = 1.25f;
        [SerializeField, Min(0.01f)] private float crowdPressureSpeed = 0.34f;
        [SerializeField, Min(0.1f)] private float minimumBodySeparation = 0.46f;
        [SerializeField, Min(0.1f)] private float comfortableBodySeparation = 0.66f;
        [SerializeField, Min(0.1f)] private float extremeCrowdScore = 7.5f;
        [SerializeField, Min(0.1f)] private float routePassingClearance = 0.72f;
        [SerializeField, Min(0.1f)] private float seatCompetitionRadius = 1.25f;
        [SerializeField, Min(0f)] private float seatCompetitionEtaMargin = 0.25f;
        [SerializeField, Min(0.1f)] private float settledSeatCheckMinimum = 1f;
        [SerializeField, Min(0.1f)] private float settledSeatCheckMaximum = 3f;
        [SerializeField, Range(0f, 1f)] private float lowOccupancySeatRatio = 0.58f;
        [SerializeField, Range(0f, 1f)] private float seatedRelocationChance = 0.45f;
        [SerializeField, Min(0.1f)] private float seatedRelocationMinimumDelay = 3f;
        [SerializeField, Min(0.1f)] private float seatedRelocationMaximumDelay = 6f;
        [SerializeField, Range(0f, 0.2f)] private float lateExitPreparationChance = 0.05f;
        [SerializeField, Min(0.1f)] private float passIntentDelay = 0.22f;
        [SerializeField, Min(0.1f)] private float passIntentInterval = 0.35f;
        [SerializeField, Min(0.05f)] private float courtesyYieldReevaluationInterval = 0.18f;
        [SerializeField, Min(0.01f)] private float courtesyYieldRetargetMinimumGain = 0.08f;
        [SerializeField, Min(0.5f)] private float courtesyYieldMaximumDuration = 2.5f;
        [SerializeField, Min(0.1f)] private float exitDoorReviewInterval = 0.65f;
        [SerializeField, Min(0.1f)] private float exitDoorSwitchMinimumGain = 0.65f;
        [SerializeField, Min(0.1f)] private float exitMovementStallDuration = 2f;
        [SerializeField, Min(0f)] private float wallLeanSurfaceGap = 0.025f;
        [SerializeField, Min(0.05f)] private float standIntentInterval = 0.18f;
        [SerializeField, Min(0.1f)] private float standClearanceRadius = 0.58f;
        [SerializeField, Range(0.5f, 1f)] private float noCrossingSeparationRatio = 0.78f;
        [SerializeField, Range(0.5f, 1f)] private float squeezeBodySeparationMultiplier = 0.74f;
        [SerializeField, Min(30f)] private float bodyTurnSpeed = 420f;
        [SerializeField] private PassengerState state;
        [SerializeField] private PassengerBehavior behavior;

        private readonly List<Vector3> path = new List<Vector3>();
        private readonly List<string> decisionHistory = new List<string>();
        private readonly List<ObservedSeat> observedSeats = new List<ObservedSeat>();
        private readonly List<GeneralPassengerPrototype> nearbyPassengerBuffer =
            new List<GeneralPassengerPrototype>();
        private readonly List<GeneralPassengerPrototype> environmentQueryBuffer =
            new List<GeneralPassengerPrototype>();
        private readonly List<GeneralPassengerPrototype> visionPassengerBuffer =
            new List<GeneralPassengerPrototype>();
        private readonly HashSet<int> perceivedPassengerIds = new HashSet<int>();
        private readonly HashSet<Transform> perceivedSeatPoints = new HashSet<Transform>();
        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private MeshRenderer bodyRenderer;
        private MaterialPropertyBlock bodyColorProperties;
        private TextMesh targetSeatNumberLabel;
        private Transform directVisionVisual;
        private Transform peripheralVisionVisual;
        private PassengerSeatPrototype plannedSeat;
        private Transform plannedSittingPoint;
        private PassengerSeatPrototype reservedSeat;
        private Transform reservedSittingPoint;
        private PassengerActivityPoint reservedActivityPoint;
        private PassengerDoorway exitDoorway;
        private PassengerDoorway exitProgressDoorway;
        private PassengerDoorway courtesyYieldDoorway;
        private PassengerIntentCoordinator intentCoordinator;
        private GeneralPassengerPrototype[] passengerPeers;
        private PlayerBoardingCyclePrototype playerPeer;
        private Collider2D[] wallColliders;
        private Vector2 activityTarget;
        private Vector2 exitAisleTarget;
        private Vector2 exitWaitingTarget;
        private Vector2 exitCrossingTarget;
        private Vector2 exitOutsideTarget;
        private Vector2 pathTarget;
        private int pathIndex;
        private int boardingStopNumber;
        private int plannedExitStopNumber;
        private int boardingOrder = int.MaxValue;
        private float nextDecisionTime;
        private float nextRepathTime;
        private float nextVisionScanTime;
        private float headLookHoldUntil;
        private float avoidanceSide;
        private float avoidanceSideUntil;
        private float movementIntentUntil;
        private float blockedByPassengerSince = -1f;
        private float blockedSqueezeUntil;
        private float blockedGiveUpAt;
        private float courtesy;
        private float courtesyWaitUntil;
        private float courtesyWaitCooldownUntil;
        private float nextIntentBroadcastTime;
        private float nextPassIntentTime;
        private float nextExitAisleRerouteTime;
        private float nextExitDoorReviewTime;
        private float nextCourtesyYieldReevaluationTime;
        private float nextStandIntentTime;
        private float nextSeatCrowdCheckTime;
        private float nextSettledSeatCheckTime;
        private float nextSeatedRelocationTime;
        private float nextStandingYieldCheckTime;
        private float standingYieldCooldownUntil;
        private float courtesyYieldCanReturnAt;
        private float courtesyYieldUntil;
        private float courtesyYieldStartedAt;
        private float personalPaceMultiplier = 1f;
        private float exitPaceMultiplier = 1f;
        private float obstructionScore;
        private float lastObstructionReportTime;
        private float obstructionYieldCooldownUntil;
        private float avoidAbandonedTargetUntil;
        private float crowdTolerance = 1f;
        private float nextCrowdEscapeCheckTime;
        private float crowdEscapeCooldownUntil;
        private float lastActivityProgressTime;
        private float lastActivityProgressDistance = float.MaxValue;
        private float nextBoardingPlanReviewTime;
        private float boardingInsideSince = -1f;
        private float softContactUntil;
        private float bodySqueezeUntil;
        private float bodySqueezeAngle;
        private float jellyCompression;
        private float lastExitProgressTime;
        private float lastExitProgressDistance = float.MaxValue;
        private Vector2 movementIntent;
        private Vector2 viewDirection = Vector2.up;
        private Vector3 baseBodyScale;
        private Vector3 stateLabelBaseScale;
        private Vector2 courtesyYieldOrigin;
        private Vector2 courtesyYieldTarget;
        private Vector2 platformExitTarget;
        private Vector2 blockedDetourTarget;
        private Vector2 obstructionDirection;
        private Vector2 abandonedActivityTarget;
        private Vector2 activityProgressTarget;
        private GeneralPassengerPrototype courtesyYieldSource;
        private GeneralPassengerPrototype courtesyWaitPassenger;
        private GeneralPassengerPrototype blockingPassenger;
        private GeneralPassengerPrototype obstructionSource;
        private PassengerExitIntent courtesyYieldIntent;
        private bool hasPathTarget;
        private bool pathBlockedByPassengers;
        private bool hasBoardingReservation;
        private bool reachedBoardingDoorCenter;
        private bool reachedBoardingInside;
        private bool clearingBoardingDoor;
        private bool boardingCollisionBypassActive;
        private bool hasBoardingActivityPlan;
        private bool initialPlacement;
        private bool hasExitReservation;
        private bool waitsUntilDoorForExit;
        private bool squeezeThroughPassengers;
        private bool reachedCourtesyYieldTarget;
        private bool courtesyYieldSawDoorOpen;
        private bool hasCourtesyYieldIntent;
        private bool reachedExitAisle;
        private bool reachedExitCrossing;
        private bool blockedDetourAttempted;
        private bool hasBlockedDetourTarget;
        private int avoidanceAdjustmentCount;
        private int blockedPushCount;
        private int abandonedActivityCount;
        private int squeezeStepCount;
        private int blockedDetourCount;
        private int obstructionYieldCount;
        private int seatCrowdSwitchCount;
        private int boardingSeatPlanSwitchCount;
        private int visionScanCount;
        private int preferredStandingRetryCount;

        public string CurrentState =>
            stationJourney != null && stationJourney.IsControlling
                ? stationJourney.CurrentStageName
                : state.ToString();
        public string CurrentBehavior => behavior.ToString();
        public string DecisionHistory => string.Join(" > ", decisionHistory);
        public PassengerDoorway BoardingDoorway => boardingDoorway;
        public PassengerDoorway ExitDoorway => exitDoorway;
        public int AvoidanceAdjustmentCount => avoidanceAdjustmentCount;
        public int BlockedPushCount => blockedPushCount;
        public int AbandonedActivityCount => abandonedActivityCount;
        public int SqueezeStepCount => squeezeStepCount;
        public int BlockedDetourCount => blockedDetourCount;
        public int ObstructionYieldCount => obstructionYieldCount;
        public int SeatCrowdSwitchCount => seatCrowdSwitchCount;
        public float ObstructionScore => obstructionScore;
        public float CurrentMoveSpeed => GetCurrentMoveSpeed();
        public float Courtesy => courtesy;
        public Vector2 ViewDirection => viewDirection;
        public int ObservedSeatCount => observedSeats.Count;
        public int VisionScanCount => visionScanCount;
        public int PerceivedPassengerCount => perceivedPassengerIds.Count;
        public int PerceivedSeatCount => perceivedSeatPoints.Count;
        public Vector2 SimulationPosition =>
            body != null ? body.position : (Vector2)transform.position;
        public Vector2 ImpactVelocity => GetMovementIntent() * GetCurrentMoveSpeed();
        public bool IsSeekingSeat =>
            behavior == PassengerBehavior.Seated &&
            (state == PassengerState.MovingToActivity ||
             (state == PassengerState.Boarding && hasBoardingActivityPlan));

        public void Configure(
            PassengerDoorway initialBoardingDoorway,
            TrainDoorCyclePrototype cycle,
            Transform passengerMapRoot,
            bool useRandomBoardingDoorway,
            TextMesh label)
        {
            boardingDoorway = initialBoardingDoorway;
            doorCycle = cycle;
            mapRoot = passengerMapRoot;
            randomizeBoardingDoorway = useRandomBoardingDoorway;
            stateLabel = label;
        }

        public void ConfigureStationJourney(
            PassengerStationJourneyPrototype journey)
        {
            stationJourney = journey;
        }

        public void ConfigureRideStops(int minimumStops, int maximumStops)
        {
            minimumRideStops = Mathf.Max(1, minimumStops);
            maximumRideStops = Mathf.Max(minimumRideStops, maximumStops);
        }

        public void SetStationJourneyStage(
            PassengerJourneyLeg leg,
            PassengerJourneyWaypointAction waypointAction)
        {
            PassengerState nextState;
            switch (leg)
            {
                case PassengerJourneyLeg.BoardingRoute:
                    nextState = PassengerState.ApproachingPlatform;
                    break;
                case PassengerJourneyLeg.TransferRoute:
                    nextState = PassengerState.Transferring;
                    break;
                case PassengerJourneyLeg.ExitRoute:
                    nextState = PassengerState.LeavingStation;
                    break;
                default:
                    return;
            }

            if (state != nextState)
            {
                SetState(nextState, PassengerBehavior.None);
                RecordDecision("Station route -> " + waypointAction);
            }

            UpdateLabel();
        }

        public void CompleteStationApproach()
        {
            if (boardingDoorway == null)
            {
                return;
            }

            SetState(PassengerState.WaitingOutside, PassengerBehavior.None);
        }

        public void BeginTransferBoarding(
            PassengerDoorway transferDoorway,
            TrainDoorCyclePrototype transferCycle,
            Transform transferRoot)
        {
            if (transferDoorway == null)
            {
                Destroy(gameObject);
                return;
            }

            ReleaseCurrentActivity();
            ReleaseBoardingReservation();
            ReleaseExitReservation();
            intentCoordinator?.Unregister(this);
            intentCoordinator = null;

            boardingDoorway = transferDoorway;
            doorCycle = transferCycle != null ? transferCycle : doorCycle;
            mapRoot = transferRoot != null ? transferRoot : transferDoorway.transform.root;
            randomizeBoardingDoorway = false;
            exitDoorway = null;
            exitProgressDoorway = null;
            reachedBoardingDoorCenter = false;
            reachedBoardingInside = false;
            boardingInsideSince = -1f;
            reachedExitAisle = false;
            reachedExitCrossing = false;
            boardingOrder = int.MaxValue;
            initialPlacement = false;
            hasBoardingActivityPlan = false;
            ClearPath();
            ResetPassengerBlock();

            if (bodyCollider != null)
            {
                bodyCollider.enabled = true;
            }
            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Dynamic;
                body.linearVelocity = Vector2.zero;
            }

            RefreshMapReferences();
            RegisterWithIntentCoordinator();
            SetState(PassengerState.WaitingOutside, PassengerBehavior.None);
        }

        private void Start()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            body.constraints &= ~RigidbodyConstraints2D.FreezeRotation;
            ConfigureRoundPassengerBody();
            baseBodyScale = transform.localScale;
            stateLabelBaseScale = stateLabel != null
                ? stateLabel.transform.localScale
                : Vector3.one;
            ConfigureDebugVisuals();
            body.linearDamping = 8f;
            squeezeThroughPassengers = Random.value < squeezeThroughChance;
            courtesy = Random.Range(0.15f, 0.95f);
            crowdTolerance = Random.Range(0.82f, 1.25f);
            AssignMovementPace();
            IgnoreHardPassengerCollisions();
            RefreshMapReferences();
            PassengerSeatPrototype.EnsureDebugNumbering(seats);
            RegisterWithIntentCoordinator();
            if (stationJourney == null)
            {
                stationJourney = GetComponent<PassengerStationJourneyPrototype>();
            }

            if (boardingDoorway == null || !boardingDoorway.IsUsable)
            {
                enabled = false;
                Debug.LogError("[Passenger AI] No usable PassengerDoorway was found.", this);
                return;
            }

            Vector2 initialLookDirection =
                (Vector2)boardingDoorway.InsidePoint.position -
                (Vector2)boardingDoorway.OutsidePoint.position;
            if (initialLookDirection.sqrMagnitude > 0.001f)
            {
                viewDirection = initialLookDirection.normalized;
            }

            nextVisionScanTime =
                Time.time + Mathf.Abs(GetInstanceID() % 12) * 0.025f;
            if (stationJourney != null &&
                stationJourney.TryBeginPreBoarding(this))
            {
                PerformVisionScan(true);
                return;
            }

            SetPosition(boardingDoorway.GetBoardingQueuePosition(gameObject));
            PerformVisionScan(true);
            SetState(PassengerState.WaitingOutside, PassengerBehavior.None);
        }

        private void ConfigureRoundPassengerBody()
        {
            transform.localScale = new Vector3(0.62f, 0.62f, 1f);

            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshFilter.sharedMesh = GetPassengerBodyMesh();
            }

            CapsuleCollider2D capsuleCollider = GetComponent<CapsuleCollider2D>();
            if (capsuleCollider == null)
            {
                capsuleCollider = gameObject.AddComponent<CapsuleCollider2D>();
            }

            capsuleCollider.direction = CapsuleDirection2D.Horizontal;
            capsuleCollider.size = new Vector2(1.12f, 0.78f);
            capsuleCollider.offset = Vector2.zero;
            capsuleCollider.isTrigger = false;
            capsuleCollider.enabled = true;

            Collider2D[] colliders = GetComponents<Collider2D>();
            foreach (Collider2D collider in colliders)
            {
                if (collider != capsuleCollider)
                {
                    collider.enabled = false;
                }
            }

            bodyCollider = capsuleCollider;
        }

        private static Mesh GetPassengerBodyMesh()
        {
            if (passengerBodyMesh != null)
            {
                return passengerBodyMesh;
            }

            const int segmentCount = 32;
            var vertices = new Vector3[segmentCount + 1];
            var triangles = new int[segmentCount * 3];
            vertices[0] = Vector3.zero;

            for (int i = 0; i < segmentCount; i++)
            {
                float angle = i / (float)segmentCount * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(angle) * 0.58f,
                    Mathf.Sin(angle) * 0.42f,
                    0f);

                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i == segmentCount - 1 ? 1 : i + 2;
                triangles[triangleIndex + 2] = i + 1;
            }

            passengerBodyMesh = new Mesh { name = "Passenger Shoulder Body Mesh" };
            passengerBodyMesh.vertices = vertices;
            passengerBodyMesh.triangles = triangles;
            passengerBodyMesh.RecalculateNormals();
            passengerBodyMesh.RecalculateBounds();
            return passengerBodyMesh;
        }

        private void ConfigureDebugVisuals()
        {
            bodyRenderer = GetComponent<MeshRenderer>();
            bodyColorProperties = new MaterialPropertyBlock();

            GameObject seatNumberObject = new GameObject("Target Seat Number");
            seatNumberObject.transform.SetParent(transform);
            seatNumberObject.transform.localPosition = new Vector3(0f, 0f, -0.38f);
            targetSeatNumberLabel = seatNumberObject.AddComponent<TextMesh>();
            targetSeatNumberLabel.anchor = TextAnchor.MiddleCenter;
            targetSeatNumberLabel.alignment = TextAlignment.Center;
            targetSeatNumberLabel.characterSize = 0.11f;
            targetSeatNumberLabel.fontSize = 52;
            targetSeatNumberLabel.fontStyle = FontStyle.Bold;
            targetSeatNumberLabel.color = Color.white;

            peripheralVisionVisual = CreateVisionVisual(
                "Peripheral Vision 180",
                GetPeripheralVisionMesh(),
                GetVisionMaterial(false),
                -12);
            directVisionVisual = CreateVisionVisual(
                "Direct Vision 120",
                GetDirectVisionMesh(),
                GetVisionMaterial(true),
                -11);
            UpdateDebugAppearance();
        }

        private Transform CreateVisionVisual(
            string visualName,
            Mesh mesh,
            Material material,
            int sortingOrder)
        {
            GameObject visual = new GameObject(visualName);
            visual.transform.SetParent(transform);
            visual.transform.localPosition = new Vector3(0f, 0f, 0.18f);
            visual.transform.localScale = Vector3.one;

            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            return visual.transform;
        }

        private static Mesh GetDirectVisionMesh()
        {
            if (directVisionMesh == null)
            {
                directVisionMesh = CreateVisionSectorMesh(
                    "Direct Vision Sector",
                    120f,
                    4.8f / 0.62f);
            }

            return directVisionMesh;
        }

        private static Mesh GetPeripheralVisionMesh()
        {
            if (peripheralVisionMesh == null)
            {
                peripheralVisionMesh = CreateVisionSectorMesh(
                    "Peripheral Vision Sector",
                    180f,
                    3.2f / 0.62f);
            }

            return peripheralVisionMesh;
        }

        private static Mesh CreateVisionSectorMesh(
            string meshName,
            float angle,
            float radius)
        {
            const int segmentCount = 32;
            var vertices = new Vector3[segmentCount + 2];
            var triangles = new int[segmentCount * 3];
            vertices[0] = Vector3.zero;

            float halfAngle = angle * 0.5f;
            for (int i = 0; i <= segmentCount; i++)
            {
                float degrees = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segmentCount);
                float radians = degrees * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(radians) * radius,
                    Mathf.Sin(radians) * radius,
                    0f);

                if (i >= segmentCount)
                {
                    continue;
                }

                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i + 2;
            }

            var mesh = new Mesh { name = meshName };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material GetVisionMaterial(bool direct)
        {
            Material cached = direct ? directVisionMaterial : peripheralVisionMaterial;
            if (cached != null)
            {
                return cached;
            }

            Shader shader = Shader.Find("Sprites/Default");
            cached = new Material(shader)
            {
                name = direct
                    ? "Passenger Direct Vision Material"
                    : "Passenger Peripheral Vision Material",
                hideFlags = HideFlags.HideAndDontSave,
                color = direct
                    ? new Color(0.15f, 0.85f, 1f, 0.035f)
                    : new Color(0.2f, 0.65f, 1f, 0.012f)
            };

            if (direct)
            {
                directVisionMaterial = cached;
            }
            else
            {
                peripheralVisionMaterial = cached;
            }

            return cached;
        }

        private void LateUpdate()
        {
            if (state == PassengerState.Observing &&
                behavior == PassengerBehavior.Leaning)
            {
                FaceAwayFromNearestWall();
            }

            Vector2 lookDirection = viewDirection.sqrMagnitude > 0.001f
                ? viewDirection.normalized
                : Vector2.up;
            Vector3 currentScale = transform.localScale;
            Vector3 childScaleCompensation = new Vector3(
                currentScale.x > 0.001f ? baseBodyScale.x / currentScale.x : 1f,
                currentScale.y > 0.001f ? baseBodyScale.y / currentScale.y : 1f,
                1f);
            float lookAngle = Mathf.Atan2(lookDirection.y, lookDirection.x) *
                              Mathf.Rad2Deg;
            float localLookAngle = lookAngle - transform.eulerAngles.z;
            if (directVisionVisual != null)
            {
                directVisionVisual.localRotation =
                    Quaternion.Euler(0f, 0f, localLookAngle);
                directVisionVisual.localScale = childScaleCompensation;
            }

            if (peripheralVisionVisual != null)
            {
                peripheralVisionVisual.localRotation =
                    Quaternion.Euler(0f, 0f, localLookAngle);
                peripheralVisionVisual.localScale = childScaleCompensation;
            }

            if (stateLabel != null)
            {
                stateLabel.transform.rotation = Quaternion.identity;
                if (stateLabel.transform.parent == transform)
                {
                    stateLabel.transform.localScale = Vector3.Scale(
                        stateLabelBaseScale,
                        childScaleCompensation);
                }
            }

            if (targetSeatNumberLabel != null)
            {
                targetSeatNumberLabel.transform.rotation = Quaternion.identity;
                targetSeatNumberLabel.transform.localScale =
                    childScaleCompensation;
            }
        }

        private void FixedUpdate()
        {
            if (stationJourney != null && stationJourney.IsControlling)
            {
                return;
            }

            StopResidualMotion();
            UpdateBodySqueezeRotation();
            UpdateObstructionPressure();
            clearingBoardingDoor = false;

            if (boardingDoorway == null || doorCycle == null)
            {
                return;
            }

            UpdateVisionPerception();

            switch (state)
            {
                case PassengerState.WaitingOutside:
                    Vector2 queuePosition = boardingDoorway.GetBoardingQueuePosition(gameObject);
                    bool isAtQueuePosition = MoveWithoutAvoidance(queuePosition);
                    if (isAtQueuePosition && boardingDoorway.TryBeginBoarding(gameObject))
                    {
                        boardingOrder = boardingDoorway.GetBoardingOrder(gameObject);
                        boardingStopNumber = doorCycle.StopNumber;
                        int maximumStops = Mathf.Max(
                            minimumRideStops,
                            maximumRideStops);
                        plannedExitStopNumber = boardingStopNumber + Random.Range(
                            Mathf.Max(1, minimumRideStops),
                            maximumStops + 1);
                        waitsUntilDoorForExit =
                            Random.value < lateExitPreparationChance;
                        reachedBoardingDoorCenter = false;
                        reachedBoardingInside = false;
                        boardingInsideSince = -1f;
                        hasBoardingActivityPlan = false;
                        boardingSeatPlanSwitchCount = 0;
                        initialPlacement = false;
                        SetState(PassengerState.Boarding, PassengerBehavior.None);
                        PrepareBoardingActivityPlan();
                    }
                    break;

                case PassengerState.Boarding:
                    ReviewBoardingActivityPlan();
                    if (!reachedBoardingDoorCenter)
                    {
                        reachedBoardingDoorCenter = MoveWithoutAvoidance(
                            boardingDoorway.GetBoardingEntryPosition(gameObject));
                    }
                    else if (!reachedBoardingInside)
                    {
                        SetBoardingCollisionBypass(true);
                        if (!boardingDoorway.HasEnteredTrain(body.position) &&
                            TryRealignWithBoardingDoor())
                        {
                            break;
                        }

                        bool canPeelOffToOpeningSideSeat =
                            hasBoardingActivityPlan &&
                            behavior == PassengerBehavior.Seated &&
                            boardingDoorway.HasEnteredTrain(body.position) &&
                            boardingDoorway.IsDestinationOnBoardingSide(activityTarget);
                        if (canPeelOffToOpeningSideSeat)
                        {
                            BeginPreplannedActivityAfterDoorCrossing();
                            ContinueActivityMovement();
                            break;
                        }

                        bool hasEnteredTrain =
                            boardingDoorway.HasEnteredTrain(body.position);
                        if (hasEnteredTrain && boardingInsideSince < 0f)
                        {
                            boardingInsideSince = Time.time;
                        }

                        bool completeCrowdedIngress =
                            hasEnteredTrain &&
                            boardingInsideSince >= 0f &&
                            Time.time - boardingInsideSince >= 1.1f;

                        Vector2 ingressTarget = hasBoardingActivityPlan
                            ? boardingDoorway.GetBoardingIngressPosition(
                                gameObject,
                                activityTarget)
                            : boardingDoorway.GetBoardingInsidePosition(gameObject);
                        reachedBoardingInside = completeCrowdedIngress ||
                                                MoveWithoutAvoidance(
                                                    ingressTarget,
                                                    1.08f);
                        if (completeCrowdedIngress)
                        {
                            RecordDecision("Inside crowd -> Boarding complete");
                        }
                        if (reachedBoardingInside && hasBoardingActivityPlan)
                        {
                            BeginPreplannedActivityAfterDoorCrossing();
                            ContinueActivityMovement();
                        }
                        else if (reachedBoardingInside)
                        {
                            SetBoardingCollisionBypass(false);
                            ReleaseBoardingReservation();
                            initialPlacement = true;
                            ClearPath();
                            PerformVisionScan(true);
                            ChooseKnownSeat();
                        }
                    }
                    break;

                case PassengerState.ChoosingBehavior:
                    if (ShouldPrepareToExit())
                    {
                        BeginExitPreparation();
                    }
                    else if (!initialPlacement && TryBeginCrowdEscape())
                    {
                        break;
                    }
                    else
                    {
                        ChooseKnownSeat();
                    }
                    break;

                case PassengerState.MovingToActivity:
                    if (ShouldPrepareToExit())
                    {
                        BeginExitPreparation();
                    }
                    else if (TryClearBoardingDoorForFollowingPassengers())
                    {
                        break;
                    }
                    else if (!initialPlacement && TryBeginCrowdEscape())
                    {
                        break;
                    }
                    else if (TryRerouteFromCrowdedSeat())
                    {
                        break;
                    }
                    else if (!initialPlacement && ShouldGiveUpContestedActivity())
                    {
                        ReconsiderContestedActivity();
                    }
                    else
                    {
                        ContinueActivityMovement();
                    }
                    break;

                case PassengerState.Observing:
                    if (ShouldPrepareToExit())
                    {
                        BeginExitPreparation();
                    }
                    else if (TryClearBoardingDoorForFollowingPassengers())
                    {
                        break;
                    }
                    else if (TryLeaveActiveBoardingFlow())
                    {
                        break;
                    }
                    else if (TryRelocateToComfortableSeat())
                    {
                        break;
                    }
                    else if (TryTakeVisibleOpenSeat())
                    {
                        break;
                    }
                    else if (ShouldHoldWallPositionDuringBoarding())
                    {
                        break;
                    }
                    else if (TryApplyCrowdPressure())
                    {
                        break;
                    }
                    else if (TryBeginCrowdEscape())
                    {
                        break;
                    }
                    else if (TryYieldFromObstructionPressure())
                    {
                        break;
                    }
                    else if (TryYieldForPassingTraffic())
                    {
                        break;
                    }
                    else if (Time.time >= nextDecisionTime)
                    {
                        EvaluateReasonedBehaviorChange();
                    }
                    break;

                case PassengerState.Yielding:
                    UpdateCourtesyYield();
                    break;

                case PassengerState.WaitingToStand:
                    UpdateWaitingToStand();
                    break;

                case PassengerState.PreparingToExit:
                    BroadcastExitIntent();
                    if (TryImproveExitDoorwayChoice())
                    {
                        break;
                    }

                    if (!reachedExitAisle)
                    {
                        if (blockedByPassengerSince >= 0f &&
                            Time.time - blockedByPassengerSince >= 1.1f &&
                            Time.time >= nextExitAisleRerouteTime)
                        {
                            TryRerouteExitAisleEntry();
                        }

                        reachedExitAisle = Vector2.Distance(body.position, exitAisleTarget) <= 0.18f ||
                                           MoveUsingPath(exitAisleTarget);
                        if (reachedExitAisle)
                        {
                            ClearPath();
                            ResetPassengerBlock();
                        }

                        break;
                    }

                    if (exitDoorway != null &&
                        (IsInsideExitWaitingArea() || MoveUsingPath(exitWaitingTarget)))
                    {
                        SetState(PassengerState.WaitingAtExitDoor, PassengerBehavior.Exiting);
                    }
                    break;

                case PassengerState.WaitingAtExitDoor:
                    MaintainPersonalSpaceNearExit();
                    if (exitDoorway != null &&
                        exitDoorway.Door.IsOpen &&
                        doorCycle.StopNumber > boardingStopNumber)
                    {
                        ResetExitProgressTracking();
                        reachedExitCrossing = false;
                        SetState(PassengerState.Exiting, PassengerBehavior.Exiting);
                    }
                    break;

                case PassengerState.Exiting:
                    if (exitDoorway != null)
                    {
                        if (HasExitMovementStalled())
                        {
                            if (TryForceExitThresholdTraversal())
                            {
                                break;
                            }

                            if (!TrySwitchToAlternativeExitDoorway())
                            {
                                hasBlockedDetourTarget = false;
                                blockedDetourAttempted = false;
                                ClearPath();
                                ResetPassengerBlock();
                                ResetExitProgressTracking();
                            }

                            break;
                        }

                        if (!reachedExitCrossing)
                        {
                            reachedExitCrossing = MoveDirectly(exitCrossingTarget);
                            if (reachedExitCrossing)
                            {
                                SetBoardingCollisionBypass(true);
                                ResetExitProgressTracking();
                            }

                            break;
                        }

                        bool reachedOutside = MoveThroughExit(exitOutsideTarget);
                        if (reachedOutside || exitDoorway.HasLeftTrain(body.position))
                        {
                            ReleaseExitReservation();
                            if (stationJourney == null ||
                                !stationJourney.TryBeginPostAlighting(this))
                            {
                                Destroy(gameObject);
                            }
                        }
                    }
                    break;

                case PassengerState.LeavingPlatform:
                    if (MoveWithoutAvoidance(platformExitTarget))
                    {
                        Destroy(gameObject);
                    }
                    break;
            }

            ResolveImmediatePassengerOverlap();
        }

        private bool TryRealignWithBoardingDoor()
        {
            if (boardingDoorway == null ||
                boardingDoorway.OutsidePoint == null ||
                boardingDoorway.InsidePoint == null)
            {
                return false;
            }

            Vector2 outside = boardingDoorway.OutsidePoint.position;
            Vector2 inside = boardingDoorway.InsidePoint.position;
            Vector2 inward = inside - outside;
            if (inward.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            inward.Normalize();
            Vector2 lateral = new Vector2(-inward.y, inward.x);
            float currentOffset = Vector2.Dot(
                body.position - outside,
                lateral);
            if (Mathf.Abs(currentOffset) <= 0.56f)
            {
                return false;
            }

            Vector2 entry =
                boardingDoorway.GetBoardingEntryPosition(gameObject);
            float entryOffset = Vector2.Dot(entry - outside, lateral);
            Vector2 alignmentTarget =
                body.position +
                lateral * (entryOffset - currentOffset);
            MoveWithoutAvoidance(alignmentTarget);
            return true;
        }

        private void RefreshMapReferences()
        {
            if (mapRoot == null)
            {
                mapRoot = transform.parent;
            }

            doorways = FindMapComponents<PassengerDoorway>();
            navigationAreas = FindMapComponents<GridNavigation2D>();
            seats = FindMapComponents<PassengerSeatPrototype>();
            activityPoints = FindMapComponents<PassengerActivityPoint>();
            passengerPeers = FindMapComponents<GeneralPassengerPrototype>();
            PlayerBoardingCyclePrototype[] players =
                FindSceneComponents<PlayerBoardingCyclePrototype>();
            playerPeer = players.Length > 0 ? players[0] : null;

            var walls = new List<Collider2D>();
            foreach (NavigationObstacle obstacle in FindMapComponents<NavigationObstacle>())
            {
                if (obstacle == null || !obstacle.gameObject.name.Contains("Wall"))
                {
                    continue;
                }

                Collider2D wallCollider = obstacle.GetComponent<Collider2D>();
                if (wallCollider != null)
                {
                    walls.Add(wallCollider);
                }
            }

            wallColliders = walls.ToArray();

            if (doorCycle == null)
            {
                TrainDoorCyclePrototype[] cycles = FindSceneComponents<TrainDoorCyclePrototype>();
                if (cycles.Length > 0)
                {
                    doorCycle = cycles[0];
                }
            }

            bool hasConfiguredDoorway = boardingDoorway != null &&
                                        boardingDoorway.IsUsable &&
                                        ContainsDoorway(boardingDoorway);

            if (!randomizeBoardingDoorway &&
                hasConfiguredDoorway &&
                boardingDoorway.TryReserveBoarding(gameObject))
            {
                hasBoardingReservation = true;
                return;
            }

            boardingDoorway = ReserveRandomBoardingDoorway();
        }

        private void RegisterWithIntentCoordinator()
        {
            if (mapRoot == null)
            {
                return;
            }

            intentCoordinator = mapRoot.GetComponent<PassengerIntentCoordinator>();
            if (intentCoordinator == null)
            {
                intentCoordinator = mapRoot.gameObject.AddComponent<PassengerIntentCoordinator>();
            }

            intentCoordinator.Register(this);
        }

        private PassengerDoorway ReserveRandomBoardingDoorway()
        {
            var candidates = new List<PassengerDoorway>();
            int smallestQueue = int.MaxValue;

            if (doorways != null)
            {
                foreach (PassengerDoorway doorway in doorways)
                {
                    if (doorway != null && doorway.IsUsable)
                    {
                        int queueCount = doorway.BoardingReservationCount;
                        if (queueCount < smallestQueue)
                        {
                            candidates.Clear();
                            smallestQueue = queueCount;
                        }

                        if (queueCount == smallestQueue)
                        {
                            candidates.Add(doorway);
                        }
                    }
                }
            }

            while (candidates.Count > 0)
            {
                int index = Random.Range(0, candidates.Count);
                PassengerDoorway candidate = candidates[index];
                candidates.RemoveAt(index);

                if (!candidate.TryReserveBoarding(gameObject))
                {
                    continue;
                }

                hasBoardingReservation = true;
                return candidate;
            }

            hasBoardingReservation = false;
            return FindNearestUsableDoorway(transform.position);
        }

        private void ReleaseBoardingReservation()
        {
            if (!hasBoardingReservation || boardingDoorway == null)
            {
                return;
            }

            boardingDoorway.ReleaseBoarding(gameObject);
            hasBoardingReservation = false;
        }

        private void OnDestroy()
        {
            ReleaseBoardingReservation();
            ReleaseExitReservation();
            intentCoordinator?.Unregister(this);
        }

        private bool ContainsDoorway(PassengerDoorway target)
        {
            if (doorways == null)
            {
                return false;
            }

            foreach (PassengerDoorway doorway in doorways)
            {
                if (doorway == target)
                {
                    return true;
                }
            }

            return false;
        }

        private T[] FindMapComponents<T>() where T : Component
        {
            if (mapRoot != null)
            {
                return mapRoot.GetComponentsInChildren<T>(true);
            }

            return FindSceneComponents<T>();
        }

        private T[] FindSceneComponents<T>() where T : Component
        {
            T[] found = FindObjectsByType<T>(FindObjectsSortMode.None);
            var sameScene = new List<T>(found.Length);

            foreach (T component in found)
            {
                if (component != null && component.gameObject.scene == gameObject.scene)
                {
                    sameScene.Add(component);
                }
            }

            return sameScene.ToArray();
        }

        private void CollectNearbyPassengers(
            Vector2 position,
            float radius,
            List<GeneralPassengerPrototype> results)
        {
            if (intentCoordinator != null)
            {
                intentCoordinator.CollectNearbyPassengers(
                    position,
                    radius,
                    this,
                    results);
                return;
            }

            results.Clear();
            if (passengerPeers == null)
            {
                return;
            }

            float radiusSquared = radius * radius;
            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null ||
                    passenger == this ||
                    passenger.body == null ||
                    Vector2.SqrMagnitude(passenger.body.position - position) >
                    radiusSquared)
                {
                    continue;
                }

                results.Add(passenger);
            }
        }

        private void UpdateVisionPerception()
        {
            if (Time.time < nextVisionScanTime)
            {
                return;
            }

            bool hasOpenDoor = HasOpenDoor();
            float interval = hasOpenDoor
                ? openDoorVisionInterval
                : closedDoorVisionInterval;
            float stagger =
                Mathf.Abs(GetInstanceID() % 7) * interval * 0.025f;
            nextVisionScanTime = Time.time + interval + stagger;
            PerformVisionScan(false);
            headLookHoldUntil = Time.time + interval * 0.8f;
        }

        private void PerformVisionScan(bool force, bool keepForwardLook = false)
        {
            if (!force && body == null)
            {
                return;
            }

            Vector2 baseDirection = GetMovementIntent();
            if (baseDirection.sqrMagnitude <= 0.001f)
            {
                baseDirection = viewDirection.sqrMagnitude > 0.001f
                    ? viewDirection
                    : Vector2.up;
            }

            if (!keepForwardLook &&
                (state == PassengerState.Boarding ||
                 state == PassengerState.ChoosingBehavior ||
                 initialPlacement))
            {
                float[] scanAngles = { 0f, -38f, 38f, -68f, 68f };
                float scanAngle = scanAngles[visionScanCount % scanAngles.Length];
                viewDirection = RotateDirection(
                    baseDirection.normalized,
                    scanAngle);
            }

            perceivedPassengerIds.Clear();
            perceivedSeatPoints.Clear();
            float maximumDistance = Mathf.Max(
                directVisionDistance,
                peripheralVisionDistance) * GetVisionRangeMultiplier();
            CollectNearbyPassengers(
                body.position,
                maximumDistance,
                visionPassengerBuffer);
            foreach (GeneralPassengerPrototype passenger in visionPassengerBuffer)
            {
                if (passenger != null &&
                    passenger.body != null &&
                    IsWorldPointVisible(passenger.body.position))
                {
                    perceivedPassengerIds.Add(passenger.GetInstanceID());
                }
            }

            if (seats != null)
            {
                foreach (PassengerSeatPrototype seat in seats)
                {
                    if (seat == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < seat.Capacity; i++)
                    {
                        Transform sittingPoint = seat.GetSittingPoint(i);
                        if (sittingPoint != null &&
                            IsWorldPointVisible(sittingPoint.position))
                        {
                            perceivedSeatPoints.Add(sittingPoint);
                        }
                    }
                }
            }

            visionScanCount++;
        }

        private bool IsWorldPointVisible(Vector2 worldPosition)
        {
            Vector2 offset = worldPosition - body.position;
            float distance = offset.magnitude;
            if (distance <= 0.001f)
            {
                return true;
            }

            Vector2 lookDirection = viewDirection.sqrMagnitude > 0.001f
                ? viewDirection.normalized
                : Vector2.up;
            float rangeMultiplier = GetVisionRangeMultiplier();
            float alignment = Vector2.Dot(lookDirection, offset / distance);
            float directThreshold = Mathf.Cos(
                directVisionAngle * 0.5f * Mathf.Deg2Rad);
            if (distance <= directVisionDistance * rangeMultiplier &&
                alignment >= directThreshold)
            {
                return true;
            }

            float peripheralThreshold = Mathf.Cos(
                peripheralVisionAngle * 0.5f * Mathf.Deg2Rad);
            return distance <= peripheralVisionDistance * rangeMultiplier &&
                   alignment >= peripheralThreshold;
        }

        private float GetVisionRangeMultiplier()
        {
            return state == PassengerState.WaitingOutside ||
                   state == PassengerState.Boarding
                ? 1.75f
                : 1f;
        }

        private bool HasOpenDoor()
        {
            if (doorways == null)
            {
                return false;
            }

            foreach (PassengerDoorway doorway in doorways)
            {
                if (doorway != null &&
                    doorway.Door != null &&
                    doorway.Door.IsOpen)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector2 RotateDirection(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(
                direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine);
        }

        private PassengerDoorway FindNearestUsableDoorway(Vector2 position)
        {
            PassengerDoorway nearest = null;
            float nearestDistance = float.MaxValue;

            if (doorways == null)
            {
                return null;
            }

            foreach (PassengerDoorway doorway in doorways)
            {
                if (doorway == null || !doorway.IsUsable)
                {
                    continue;
                }

                float distance = Vector2.SqrMagnitude(
                    (Vector2)doorway.OutsidePoint.position - position);
                if (distance < nearestDistance)
                {
                    nearest = doorway;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private void PrepareBoardingActivityPlan()
        {
            initialPlacement = true;
            Vector2 inward =
                (Vector2)boardingDoorway.InsidePoint.position -
                (Vector2)boardingDoorway.OutsidePoint.position;
            if (inward.sqrMagnitude > 0.001f)
            {
                viewDirection = inward.normalized;
            }

            PerformVisionScan(true, true);
            observedSeats.Clear();
            CollectAvailableSeats();
            bool pursuesSeat =
                observedSeats.Count > 0 &&
                Random.value <= GetInitialSeatPursuitChance();
            if (pursuesSeat && TryPlanObservedSeat())
            {
                behavior = PassengerBehavior.Seated;
                hasBoardingActivityPlan = true;
                nextBoardingPlanReviewTime = Time.time + 0.4f;
                RecordDecision("Platform view -> Plan seat");
                UpdateLabel();
                return;
            }

            observedSeats.Clear();
            if (TryChooseInitialStandingBehavior())
            {
                hasBoardingActivityPlan = true;
                nextBoardingPlanReviewTime = Time.time + 0.4f;
                RecordDecision("Platform view -> Plan " + behavior);
                UpdateLabel();
                return;
            }

            behavior = PassengerBehavior.AisleStanding;
            activityTarget = boardingDoorway.GetBoardingInsidePosition(gameObject);
            hasBoardingActivityPlan = true;
            nextBoardingPlanReviewTime = Time.time + 0.4f;
            RecordDecision("Platform view -> Plan open standing");
            UpdateLabel();
        }

        private void ReviewBoardingActivityPlan()
        {
            if (Time.time < nextBoardingPlanReviewTime)
            {
                return;
            }

            nextBoardingPlanReviewTime = Time.time + 0.4f;
            PerformVisionScan(true);
            if (!hasBoardingActivityPlan)
            {
                PrepareBoardingActivityPlan();
                return;
            }

            if (behavior != PassengerBehavior.Seated ||
                plannedSeat == null ||
                plannedSittingPoint == null)
            {
                return;
            }

            if (IsInsideDirectBoardingLane(body.position) &&
                !boardingDoorway.HasEnteredTrain(body.position))
            {
                return;
            }

            float targetDistance = Vector2.Distance(body.position, activityTarget);
            CalculateSeatCompetitionPenalty(
                activityTarget,
                targetDistance,
                out bool likelyToLoseSeat);
            if (!likelyToLoseSeat || boardingSeatPlanSwitchCount >= 3)
            {
                return;
            }

            boardingSeatPlanSwitchCount++;
            abandonedActivityTarget = activityTarget;
            avoidAbandonedTargetUntil = Time.time + 1.2f;
            ClearPlannedSeat();
            observedSeats.Clear();
            CollectAvailableSeats();
            if (observedSeats.Count > 0 && TryPlanObservedSeat())
            {
                behavior = PassengerBehavior.Seated;
                RecordDecision("Boarding view -> Other seat");
                UpdateLabel();
                return;
            }

            observedSeats.Clear();
            if (TryChooseInitialStandingBehavior())
            {
                RecordDecision("Boarding view -> No reachable seat");
                UpdateLabel();
            }
        }

        private void BeginPreplannedActivityAfterDoorCrossing()
        {
            ReleaseBoardingReservation();
            reachedBoardingInside = true;
            hasBoardingActivityPlan = false;
            ClearPath();
            ResetPassengerBlock();
            ResetActivityProgressTracking();
            state = PassengerState.MovingToActivity;
            RecordDecision("Door crossed -> " + behavior);
            UpdateLabel();
        }

        private void ContinueActivityMovement()
        {
            if (initialPlacement)
            {
                SetBoardingCollisionBypass(true);
                if (TryFinishInitialSeatMoveNearApproach())
                {
                    return;
                }

                if (MoveUsingPath(activityTarget, true))
                {
                    FinishActivityMove();
                }
            }
            else if (HasActivityMoveStalled())
            {
                AbandonBlockedActivity();
            }
            else if (MoveUsingPath(activityTarget))
            {
                FinishActivityMove();
            }
            else if (!initialPlacement && ShouldAbandonBlockedActivity())
            {
                AbandonBlockedActivity();
            }
        }

        private bool TryFinishInitialSeatMoveNearApproach()
        {
            if (behavior != PassengerBehavior.Seated ||
                plannedSeat == null ||
                plannedSittingPoint == null)
            {
                return false;
            }

            float crowdPressure =
                CalculateSeatApproachCrowdPressure(activityTarget);
            float sitDistance = crowdPressure >= seatApproachCrowdLimit * 0.5f
                ? 1.05f
                : 0.58f;
            if (Vector2.Distance(body.position, activityTarget) > sitDistance)
            {
                return false;
            }

            FinishActivityMove();
            return true;
        }

        private void SetBoardingCollisionBypass(bool active)
        {
            boardingCollisionBypassActive = active;
            if (bodyCollider != null && bodyCollider.enabled)
            {
                bodyCollider.isTrigger = active;
            }
        }

        private void ChooseKnownSeat()
        {
            if (ShouldPrepareToExit())
            {
                observedSeats.Clear();
                BeginExitPreparation();
                return;
            }

            observedSeats.Clear();
            CollectAvailableSeats();
            bool sawAvailableSeat = observedSeats.Count > 0;
            bool pursuesSeat = !initialPlacement ||
                                Random.value <= GetInitialSeatPursuitChance();
            if (pursuesSeat &&
                observedSeats.Count > 0 &&
                TryPlanObservedSeat())
            {
                if (reservedActivityPoint != null)
                {
                    reservedActivityPoint.Release(gameObject);
                    reservedActivityPoint = null;
                }

                behavior = PassengerBehavior.Seated;
                RecordDecision("Nearest known seat -> Seated");
                ClearPath();
                state = PassengerState.MovingToActivity;
                UpdateLabel();
                return;
            }

            observedSeats.Clear();
            if (sawAvailableSeat && !pursuesSeat)
            {
                RecordDecision("Seat available -> Prefer open space");
            }
            if (behavior != PassengerBehavior.None &&
                behavior != PassengerBehavior.Exiting)
            {
                RecordDecision("No seat -> Stay " + behavior);
                ScheduleNextDecision();
                state = PassengerState.Observing;
                UpdateLabel();
                return;
            }

            ChooseBehavior();
        }

        private void CollectAvailableSeats()
        {
            if (seats == null)
            {
                return;
            }

            bool limitToCurrentVision = initialPlacement;
            foreach (PassengerSeatPrototype seat in seats)
            {
                if (seat == null)
                {
                    continue;
                }

                for (int i = 0; i < seat.Capacity; i++)
                {
                    Transform sittingPoint = seat.GetSittingPoint(i);
                    if (sittingPoint == null ||
                        !seat.IsAvailable(sittingPoint) ||
                        (limitToCurrentVision &&
                         !perceivedSeatPoints.Contains(sittingPoint)) ||
                        HasObservedSeat(sittingPoint))
                    {
                        continue;
                    }

                    observedSeats.Add(new ObservedSeat(seat, sittingPoint));
                }
            }
        }

        private float GetInitialSeatPursuitChance()
        {
            float chance = initialSeatPursuitChance;
            if (boardingOrder <= 7)
            {
                chance += 0.18f;
            }

            float nearbyCrowdPenalty = Mathf.Clamp01(
                CalculateCrowdScore(body.position) / 12f) * 0.16f;
            return Mathf.Clamp01(chance - nearbyCrowdPenalty);
        }

        private bool HasObservedSeat(Transform sittingPoint)
        {
            foreach (ObservedSeat observedSeat in observedSeats)
            {
                if (observedSeat.SittingPoint == sittingPoint)
                {
                    return true;
                }
            }

            return false;
        }

        private void ChooseBehavior()
        {
            ReleaseCurrentActivity();

            if (initialPlacement && TryChooseInitialStandingBehavior())
            {
                ClearPath();
                state = PassengerState.MovingToActivity;
                RecordDecision("Initial standing choice -> " + behavior);
                UpdateLabel();
                return;
            }

            if (!TryChooseLeastCrowdedStandingPosition(out PassengerBehavior nextBehavior))
            {
                behavior = PassengerBehavior.AisleStanding;
                activityTarget = body.position;
                initialPlacement = false;
                RecordDecision("Stay in current open space");
                ScheduleNextDecision();
                SetState(PassengerState.Observing, behavior);
                return;
            }

            behavior = nextBehavior;
            RecordDecision(nextBehavior.ToString());
            ClearPath();
            state = PassengerState.MovingToActivity;
            UpdateLabel();
        }

        private bool TryChooseInitialStandingBehavior()
        {
            bool earlyStandingPassenger = boardingOrder <= 7;
            if (TryChooseBoardingPerimeterStandingPosition())
            {
                return true;
            }

            if (TryChooseOppositeClosedDoorLeanPosition())
            {
                return true;
            }

            if (TryChooseEndWallLeanPosition())
            {
                return true;
            }

            if (TryReserveOppositeSideHandhold())
            {
                return true;
            }

            if (TryChooseWallLeanPosition(earlyStandingPassenger))
            {
                return true;
            }

            if (earlyStandingPassenger && TryChooseWallLeanPosition(false))
            {
                return true;
            }

            return TryReserveActivityPoint(PassengerBehavior.Handhold);
        }

        private bool TryChooseBoardingPerimeterStandingPosition()
        {
            if (!initialPlacement || boardingDoorway == null)
            {
                return false;
            }

            GridNavigation2D navigation = boardingDoorway.Navigation;
            if (navigation == null)
            {
                navigation = FindCurrentNavigationArea();
            }

            if (navigation == null)
            {
                return false;
            }

            Rect bounds = navigation.WorldBounds;
            float bodyMargin = Mathf.Max(0.72f, GetBodyRadius() + 0.2f);
            float perimeterOffset = Mathf.Clamp(
                bounds.height * 0.18f,
                0.72f,
                0.9f);
            float horizontalMargin = Mathf.Max(0.75f, bounds.width * 0.04f);
            int placementTier = GetBoardingPlacementTier();
            Vector2 inside = boardingDoorway.InsidePoint.position;
            float openingSide = Mathf.Sign(inside.y - bounds.center.y);
            if (Mathf.Approximately(openingSide, 0f))
            {
                openingSide = -1f;
            }

            Vector2 selectedPosition = body.position;
            float bestScore = float.MaxValue;
            bool found = false;

            for (int attempt = 0; attempt < 48; attempt++)
            {
                bool useOpeningSide = Random.value < 0.48f;
                float side = useOpeningSide ? openingSide : -openingSide;
                Vector2 candidate = new Vector2(
                    Random.Range(
                        bounds.xMin + horizontalMargin,
                        bounds.xMax - horizontalMargin),
                    bounds.center.y +
                    side * perimeterOffset +
                    Random.Range(-0.08f, 0.08f));
                candidate.x = Mathf.Clamp(candidate.x,
                    bounds.xMin + bodyMargin,
                    bounds.xMax - bodyMargin);

                if (!navigation.IsWorldWalkable(candidate) ||
                    !IsStandingPositionAvailable(candidate) ||
                    IsInsideDirectBoardingLane(candidate) ||
                    IsRecentlyAbandonedTarget(candidate, 0.85f))
                {
                    continue;
                }

                float score = CalculateCrowdScore(candidate) * 4f -
                              Vector2.Distance(inside, candidate) *
                              (placementTier == 0 ? 0.18f : 0.06f) +
                              Random.Range(0f, 0.12f);
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                selectedPosition = candidate;
                found = true;
            }

            if (!found)
            {
                return false;
            }

            behavior = PassengerBehavior.AisleStanding;
            activityTarget = selectedPosition;
            return true;
        }

        private bool HasActivityMoveStalled()
        {
            float distance = Vector2.Distance(body.position, activityTarget);
            bool newTarget = Vector2.Distance(activityProgressTarget, activityTarget) > 0.05f;
            if (newTarget || lastActivityProgressDistance == float.MaxValue)
            {
                activityProgressTarget = activityTarget;
                lastActivityProgressDistance = distance;
                lastActivityProgressTime = Time.time;
                return false;
            }

            if (distance < lastActivityProgressDistance - 0.12f)
            {
                lastActivityProgressDistance = distance;
                lastActivityProgressTime = Time.time;
                return false;
            }

            float stallTimeout = initialPlacement ? 4.5f : 3.5f;
            return Time.time - lastActivityProgressTime >= stallTimeout;
        }

        private void ResetActivityProgressTracking()
        {
            activityProgressTarget = Vector2.zero;
            lastActivityProgressDistance = float.MaxValue;
            lastActivityProgressTime = Time.time;
        }

        private void ResetExitProgressTracking()
        {
            exitProgressDoorway = exitDoorway;
            lastExitProgressDistance = float.MaxValue;
            lastExitProgressTime = Time.time;
        }

        private bool HasExitMovementStalled()
        {
            if (exitDoorway == null)
            {
                return false;
            }

            float distance = Vector2.Distance(
                body.position,
                exitDoorway.OutsidePoint.position);
            if (exitProgressDoorway != exitDoorway ||
                lastExitProgressDistance == float.MaxValue)
            {
                exitProgressDoorway = exitDoorway;
                lastExitProgressDistance = distance;
                lastExitProgressTime = Time.time;
                return false;
            }

            if (distance < lastExitProgressDistance - 0.18f)
            {
                lastExitProgressDistance = distance;
                lastExitProgressTime = Time.time;
                return false;
            }

            return Time.time - lastExitProgressTime >= exitMovementStallDuration;
        }

        private bool TryForceExitThresholdTraversal()
        {
            if (exitDoorway == null ||
                exitDoorway.Door == null ||
                !exitDoorway.Door.IsOpen ||
                exitDoorway.InsidePoint == null ||
                Vector2.Distance(
                    body.position,
                    exitDoorway.InsidePoint.position) > 1.45f)
            {
                return false;
            }

            reachedExitCrossing = true;
            hasBlockedDetourTarget = false;
            blockedDetourAttempted = false;
            ClearPath();
            ResetPassengerBlock();
            SetBoardingCollisionBypass(true);
            ResetExitProgressTracking();
            RecordDecision("Door threshold stalled -> Continue outside");
            return true;
        }

        private bool TrySwitchToAlternativeExitDoorway()
        {
            PassengerDoorway alternative = null;
            float bestScore = float.MaxValue;
            if (doorways != null)
            {
                foreach (PassengerDoorway doorway in doorways)
                {
                    if (doorway == null ||
                        doorway == exitDoorway ||
                        !doorway.IsUsable ||
                        !doorway.Door.IsOpen)
                    {
                        continue;
                    }

                    float score = CalculateExitDoorwayScore(doorway);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        alternative = doorway;
                    }
                }
            }

            if (alternative == null)
            {
                return false;
            }

            ReleaseExitReservation();
            exitDoorway = alternative;
            ReserveExitDoorway(exitDoorway);
            ConfigureExitAisleTarget();
            hasBlockedDetourTarget = false;
            blockedDetourAttempted = false;
            ClearPath();
            ResetPassengerBlock();
            ResetExitProgressTracking();
            SetState(PassengerState.PreparingToExit, PassengerBehavior.Exiting);
            BroadcastExitIntent(true);
            RecordDecision("Exit blocked -> Other door");
            return true;
        }

        private bool TryImproveExitDoorwayChoice()
        {
            if (exitDoorway == null ||
                doorways == null ||
                Time.time < nextExitDoorReviewTime)
            {
                return false;
            }

            nextExitDoorReviewTime = Time.time + exitDoorReviewInterval;
            float currentDistance = Vector2.Distance(
                body.position,
                exitDoorway.InsidePoint.position);
            bool routePressure = blockedByPassengerSince >= 0f &&
                                 Time.time - blockedByPassengerSince >= 0.65f;
            if (currentDistance < 2.2f && !routePressure)
            {
                return false;
            }

            PassengerDoorway alternative = null;
            float currentScore = CalculateExitDoorwayScore(exitDoorway);
            float bestScore = currentScore;
            foreach (PassengerDoorway doorway in doorways)
            {
                if (doorway == null || doorway == exitDoorway || !doorway.IsUsable)
                {
                    continue;
                }

                float score = CalculateExitDoorwayScore(doorway);
                if (score + exitDoorSwitchMinimumGain < bestScore)
                {
                    bestScore = score;
                    alternative = doorway;
                }
            }

            if (alternative == null)
            {
                return false;
            }

            ReleaseExitReservation();
            exitDoorway = alternative;
            ReserveExitDoorway(exitDoorway);
            ConfigureExitAisleTarget();
            hasBlockedDetourTarget = false;
            blockedDetourAttempted = false;
            ClearPath();
            ResetPassengerBlock();
            ResetExitProgressTracking();
            BroadcastExitIntent(true);
            RecordDecision("Exit route crowded -> Quieter door");
            return true;
        }

        private float CalculateExitDoorwayScore(PassengerDoorway doorway)
        {
            if (doorway == null || doorway.InsidePoint == null)
            {
                return float.MaxValue;
            }

            Vector2 doorwayPosition = doorway.InsidePoint.position;
            float distance = Vector2.Distance(body.position, doorwayPosition);
            int reservations = doorway.ExitReservationCount;
            if (hasExitReservation && doorway == exitDoorway)
            {
                reservations = Mathf.Max(0, reservations - 1);
            }

            float reservationPressure = reservations * 0.82f;
            float localCrowdPressure = CalculateCrowdScore(doorwayPosition) * 0.32f;
            return distance + reservationPressure + localCrowdPressure;
        }

        private bool TryBeginCrowdEscape()
        {
            if (behavior == PassengerBehavior.Seated ||
                behavior == PassengerBehavior.Leaning ||
                behavior == PassengerBehavior.Handhold ||
                behavior == PassengerBehavior.Exiting ||
                Time.time < nextCrowdEscapeCheckTime ||
                Time.time < crowdEscapeCooldownUntil)
            {
                return false;
            }

            nextCrowdEscapeCheckTime = Time.time + crowdEscapeCheckInterval;
            if (Random.value > crowdEscapeChance)
            {
                return false;
            }

            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null)
            {
                return false;
            }

            float currentCrowdScore = CalculateCrowdScore(body.position);
            if (currentCrowdScore < crowdEscapeThreshold * crowdTolerance ||
                !TryFindQuietStandingPosition(
                    navigation,
                    out Vector2 quietPosition,
                    out float quietCrowdScore) ||
                currentCrowdScore - quietCrowdScore < crowdEscapeMinimumImprovement)
            {
                return false;
            }

            ReleaseCurrentActivity();
            ClearPath();
            ResetPassengerBlock();

            behavior = PassengerBehavior.AisleStanding;
            activityTarget = quietPosition;
            RecordDecision("Crowded -> Quiet open space");

            crowdEscapeCooldownUntil = Time.time + crowdEscapeCooldown;
            state = PassengerState.MovingToActivity;
            UpdateLabel();
            return true;
        }

        private bool TryClearBoardingDoorForFollowingPassengers()
        {
            if (boardingDoorway == null ||
                behavior == PassengerBehavior.Seated ||
                behavior == PassengerBehavior.Exiting ||
                !boardingDoorway.HasEnteredTrain(body.position) ||
                !boardingDoorway.IsInsideActiveBoardingFlow(body.position) ||
                !boardingDoorway.HasIncomingPassengerBehind(
                    gameObject,
                    body.position))
            {
                return false;
            }

            Vector2 clearanceTarget =
                boardingDoorway.GetBoardingClearancePosition(gameObject);
            clearingBoardingDoor = true;
            if (state == PassengerState.Observing)
            {
                ReleaseCurrentActivity();
                behavior = PassengerBehavior.AisleStanding;
                activityTarget = clearanceTarget;
                ClearPath();
                ResetPassengerBlock();
                ResetActivityProgressTracking();
                state = PassengerState.MovingToActivity;
                RecordDecision("Following passenger -> Clear doorway");
                UpdateLabel();
            }

            MoveWithoutAvoidance(clearanceTarget, 1.15f);
            return true;
        }

        private bool TryLeaveActiveBoardingFlow()
        {
            if (!IsInsideActiveBoardingFlow(body.position) ||
                behavior == PassengerBehavior.Seated ||
                behavior == PassengerBehavior.Exiting)
            {
                return false;
            }

            if (behavior == PassengerBehavior.Leaning &&
                IsNearWall(body.position, wallLeanReleaseDistance) &&
                !IsInsideDirectBoardingLane(body.position))
            {
                return false;
            }

            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null ||
                !TryFindQuietStandingPosition(
                    navigation,
                    out Vector2 clearPosition,
                    out _))
            {
                return false;
            }

            ReleaseCurrentActivity();
            ClearPath();
            ResetPassengerBlock();
            behavior = PassengerBehavior.AisleStanding;
            activityTarget = clearPosition;
            state = PassengerState.MovingToActivity;
            RecordDecision("Incoming passengers -> Clear doorway");
            UpdateLabel();
            return true;
        }

        private bool ShouldHoldWallPositionDuringBoarding()
        {
            return behavior == PassengerBehavior.Leaning &&
                   IsNearWall(body.position, wallLeanReleaseDistance) &&
                   IsInsideActiveBoardingFlow(body.position) &&
                   !IsInsideDirectBoardingLane(body.position);
        }

        private bool IsInsideDirectBoardingLane(Vector2 position)
        {
            if (doorways == null)
            {
                return false;
            }

            foreach (PassengerDoorway doorway in doorways)
            {
                if (doorway != null &&
                    doorway.IsUsable &&
                    doorway.IsInsideDirectBoardingLane(position))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryFindQuietStandingPosition(
            GridNavigation2D navigation,
            out Vector2 selectedPosition,
            out float selectedCrowdScore)
        {
            const int maximumAttempts = 48;
            selectedPosition = body.position;
            selectedCrowdScore = float.MaxValue;
            float bestSelectionScore = float.MaxValue;
            bool found = false;

            for (int attempt = 0; attempt < maximumAttempts; attempt++)
            {
                Vector2 candidate = GetRandomOpenStandingPosition(navigation);
                if (!navigation.IsWorldWalkable(candidate) ||
                    !IsStandingPositionAvailable(candidate))
                {
                    continue;
                }

                float crowdScore = CalculateCrowdScore(candidate);
                float aisleCenterDistance = Mathf.Abs(
                    candidate.y - navigation.WorldBounds.center.y);
                float aisleCenterPenalty = Mathf.Max(
                    0f,
                    1.15f - aisleCenterDistance) * 3.2f;
                float selectionScore = crowdScore * 4f +
                                       aisleCenterPenalty +
                                       Vector2.Distance(body.position, candidate) * 0.06f +
                                       Random.Range(0f, 0.1f);
                if (selectionScore >= bestSelectionScore)
                {
                    continue;
                }

                bestSelectionScore = selectionScore;
                selectedPosition = candidate;
                selectedCrowdScore = crowdScore;
                found = true;
            }

            return found;
        }

        private Vector2 GetRandomOpenStandingPosition(GridNavigation2D navigation)
        {
            if (!CanUseAisleStandingPosition())
            {
                return GetRandomBoardingPerimeterPosition(navigation);
            }

            Rect bounds = navigation.WorldBounds;
            float horizontalMargin = bounds.width * 0.07f;
            float openAreaHalfWidth = Mathf.Min(2.75f, bounds.height * 0.34f);
            return new Vector2(
                Random.Range(bounds.xMin + horizontalMargin, bounds.xMax - horizontalMargin),
                Random.Range(
                    bounds.center.y - openAreaHalfWidth,
                    bounds.center.y + openAreaHalfWidth));
        }

        private bool TryAdoptNearbySupport()
        {
            if (!IsSettledPositionComfortable(body.position))
            {
                return false;
            }

            if (TryGetNearbyWall(body.position, out _) &&
                TrySnapToWallForLeaning())
            {
                behavior = PassengerBehavior.Leaning;
                FaceAwayFromNearestWall();
                RecordDecision("Stand near wall -> Lean");
                return true;
            }

            return TryGrabNearbyHandhold();
        }

        private bool TryGetNearbyWall(Vector2 position, out Collider2D selectedWall)
        {
            selectedWall = null;
            float nearestDistance = Mathf.Min(
                wallLeanReleaseDistance,
                GetBodyRadius() + wallLeanSurfaceGap + 0.06f);
            if (wallColliders == null)
            {
                return false;
            }

            foreach (Collider2D wallCollider in wallColliders)
            {
                if (wallCollider == null)
                {
                    continue;
                }

                if (!TryGetWallLeanPosition(
                        wallCollider,
                        position,
                        out Vector2 leanPosition) ||
                    IsSeatFrontLeanPosition(leanPosition))
                {
                    continue;
                }

                float distance = Vector2.Distance(position, wallCollider.ClosestPoint(position));
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    selectedWall = wallCollider;
                }
            }

            return selectedWall != null;
        }

        private bool TrySnapToWallForLeaning()
        {
            if (!TryGetNearbyWall(body.position, out Collider2D wall))
            {
                return false;
            }

            if (!TryGetWallLeanPosition(
                    wall,
                    body.position,
                    out Vector2 snappedPosition) ||
                IsSeatFrontLeanPosition(snappedPosition))
            {
                return false;
            }
            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null ||
                !navigation.IsWorldWalkable(snappedPosition) ||
                IsBlockedByNavigationObstacle(snappedPosition))
            {
                return false;
            }

            body.MovePosition(snappedPosition);
            activityTarget = snappedPosition;
            return true;
        }

        private bool TryGetWallLeanPosition(
            Collider2D wall,
            Vector2 position,
            out Vector2 leanPosition)
        {
            leanPosition = position;
            if (wall == null)
            {
                return false;
            }

            Vector2 closestPoint = wall.ClosestPoint(position);
            Vector2 awayFromWall = position - closestPoint;
            if (awayFromWall.sqrMagnitude <= 0.001f)
            {
                awayFromWall = position - (Vector2)wall.bounds.center;
            }

            if (awayFromWall.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            leanPosition = closestPoint +
                           awayFromWall.normalized *
                           (GetBodyRadius() + wallLeanSurfaceGap);
            return true;
        }

        private bool IsSeatFrontLeanPosition(Vector2 position)
        {
            if (seats == null)
            {
                return false;
            }

            float aisleY = GetAisleCenterY();
            foreach (PassengerSeatPrototype seat in seats)
            {
                if (seat == null)
                {
                    continue;
                }

                Collider2D seatCollider = seat.GetComponent<Collider2D>();
                if (seatCollider == null)
                {
                    continue;
                }

                Bounds bounds = seatCollider.bounds;
                float seatSide = Mathf.Sign(bounds.center.y - aisleY);
                float positionSide = Mathf.Sign(position.y - aisleY);
                bool insideSeatWidth =
                    position.x > bounds.min.x + 0.04f &&
                    position.x < bounds.max.x - 0.04f;
                bool closeToSeatFace =
                    Mathf.Abs(position.y - bounds.center.y) <=
                    bounds.extents.y + GetBodyRadius() + 0.16f;
                if (seatSide == positionSide &&
                    insideSeatWidth &&
                    closeToSeatFace)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGrabNearbyHandhold()
        {
            PassengerActivityPoint selected = null;
            float nearestDistance = nearbyHandholdDistance;
            if (activityPoints == null)
            {
                return false;
            }

            foreach (PassengerActivityPoint point in activityPoints)
            {
                if (point == null ||
                    point.ActivityType != PassengerActivityType.Handhold ||
                    !point.IsAvailableFor(gameObject))
                {
                    continue;
                }

                float distance = Vector2.Distance(body.position, point.transform.position);
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    selected = point;
                }
            }

            if (selected == null || !selected.TryReserve(gameObject))
            {
                return false;
            }

            reservedActivityPoint = selected;
            behavior = PassengerBehavior.Handhold;
            RecordDecision("Stand near handhold -> Hold");
            return true;
        }

        private bool TryApplyCrowdPressure()
        {
            if (behavior == PassengerBehavior.Leaning &&
                IsNearWall(body.position, wallLeanReleaseDistance))
            {
                return false;
            }

            bool canBePushed = behavior == PassengerBehavior.Leaning ||
                               IsFreeStandingBehavior(behavior);
            if (!canBePushed || passengerPeers == null)
            {
                return false;
            }

            Vector2 pressureDirection = Vector2.zero;
            int pressureSources = 0;
            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null ||
                    passenger == this ||
                    passenger.body == null ||
                    (passenger.state != PassengerState.Boarding &&
                     passenger.state != PassengerState.MovingToActivity))
                {
                    continue;
                }

                Vector2 offset = body.position - passenger.body.position;
                float distance = offset.magnitude;
                Vector2 passengerIntent = passenger.GetMovementIntent();
                if (distance <= 0.001f ||
                    distance > crowdPressureRadius ||
                    passengerIntent.sqrMagnitude <= 0.001f ||
                    Vector2.Dot(passengerIntent.normalized, offset.normalized) < 0.25f)
                {
                    continue;
                }

                float weight = 1f - distance / crowdPressureRadius;
                pressureDirection += passengerIntent.normalized * weight;
                pressureSources++;
            }

            if (pressureSources < 4 || pressureDirection.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null)
            {
                return false;
            }

            Vector2 direction = pressureDirection.normalized;
            float pressureMultiplier = 1f + Mathf.Min(pressureSources - 2, 3) * 0.2f;
            Vector2 nextPosition = body.position +
                                   direction * crowdPressureSpeed *
                                   pressureMultiplier * Time.fixedDeltaTime;
            if (!navigation.IsWorldWalkable(nextPosition) ||
                IsBlockedByNavigationObstacle(nextPosition))
            {
                return false;
            }

            body.MovePosition(nextPosition);
            RememberMovementIntent(direction);
            if (behavior == PassengerBehavior.Leaning &&
                !IsNearWall(nextPosition, wallLeanReleaseDistance))
            {
                behavior = PassengerBehavior.AisleStanding;
                activityTarget = nextPosition;
                RecordDecision("Crowd pressure -> Leave wall");
                UpdateLabel();
            }
            else if (IsFreeStandingBehavior(behavior))
            {
                activityTarget = nextPosition;
            }

            return true;
        }

        private bool IsNearWall(Vector2 position, float maximumDistance)
        {
            if (wallColliders == null)
            {
                return false;
            }

            foreach (Collider2D wallCollider in wallColliders)
            {
                if (wallCollider != null &&
                    Vector2.Distance(position, wallCollider.ClosestPoint(position)) <= maximumDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private float CalculateSeatApproachCrowdPressure(Vector2 position)
        {
            if (passengerPeers == null)
            {
                return 0f;
            }

            float pressure = 0f;
            float targetRadius = seatApproachCrowdRadius * 1.2f;
            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null || passenger == this || passenger.body == null)
                {
                    continue;
                }

                if (!CanPerceivePassengerForDecision(passenger))
                {
                    continue;
                }

                if (passenger.bodyCollider != null &&
                    passenger.bodyCollider.enabled &&
                    passenger.behavior != PassengerBehavior.Seated)
                {
                    float bodyDistance = Vector2.Distance(position, passenger.body.position);
                    if (bodyDistance < seatApproachCrowdRadius)
                    {
                        pressure += (1f - bodyDistance / seatApproachCrowdRadius) * 1.25f;
                    }
                }

                if (passenger.IsSeekingSeat)
                {
                    float targetDistance = Vector2.Distance(
                        position,
                        passenger.activityTarget);
                    if (targetDistance < targetRadius)
                    {
                        pressure += (1f - targetDistance / targetRadius) * 1.8f;
                    }
                }
            }

            if (playerPeer != null)
            {
                float playerDistance = Vector2.Distance(position, playerPeer.transform.position);
                if (playerDistance < seatApproachCrowdRadius)
                {
                    pressure += (1f - playerDistance / seatApproachCrowdRadius) * 1.25f;
                }
            }

            return pressure;
        }

        private bool TryRerouteFromCrowdedSeat()
        {
            if (!IsSeekingSeat ||
                Time.time < nextSeatCrowdCheckTime ||
                seatCrowdSwitchCount >= 3)
            {
                return false;
            }

            nextSeatCrowdCheckTime = Time.time + seatCrowdRecheckInterval;
            float pressure = CalculateSeatApproachCrowdPressure(activityTarget);
            float targetDistance = Vector2.Distance(body.position, activityTarget);
            CalculateSeatCompetitionPenalty(
                activityTarget,
                targetDistance,
                out bool likelyToLoseSeat);
            bool routeBlocked =
                blockedByPassengerSince >= 0f &&
                Time.time - blockedByPassengerSince >= 0.3f;
            bool hasNearbyAlternative =
                HasNearbyVisibleSeatAlternative(targetDistance);
            if (pressure < seatApproachCrowdLimit &&
                !likelyToLoseSeat &&
                !routeBlocked &&
                !hasNearbyAlternative)
            {
                return false;
            }

            if (!hasNearbyAlternative &&
                !likelyToLoseSeat &&
                pressure < seatApproachCrowdLimit)
            {
                return false;
            }

            abandonedActivityTarget = activityTarget;
            avoidAbandonedTargetUntil = Time.time + 2.5f;
            seatCrowdSwitchCount++;
            nextSeatCrowdCheckTime = Time.time + 0.9f;
            ReleaseCurrentActivity();
            ClearPath();
            ResetPassengerBlock();
            ResetActivityProgressTracking();

            observedSeats.Clear();
            CollectAvailableSeats();
            if (observedSeats.Count > 0 && TryPlanObservedSeat())
            {
                behavior = PassengerBehavior.Seated;
                RecordDecision("Crowded seat route -> Other seat");
                state = PassengerState.MovingToActivity;
                UpdateLabel();
                return true;
            }

            observedSeats.Clear();
            initialPlacement = false;
            if (TryChooseLeastCrowdedStandingPosition(
                    out PassengerBehavior standingBehavior))
            {
                behavior = standingBehavior;
                RecordDecision("Crowded seat route -> Stand elsewhere");
                state = PassengerState.MovingToActivity;
                UpdateLabel();
                return true;
            }

            behavior = PassengerBehavior.AisleStanding;
            activityTarget = body.position;
            RecordDecision("Crowded seat route -> Stay nearby");
            ScheduleNextDecision();
            SetState(PassengerState.Observing, behavior);
            return true;
        }

        private bool HasNearbyVisibleSeatAlternative(float currentDistance)
        {
            observedSeats.Clear();
            CollectAvailableSeats();
            foreach (ObservedSeat observedSeat in observedSeats)
            {
                if (observedSeat.Seat == null ||
                    observedSeat.SittingPoint == null ||
                    (observedSeat.Seat == plannedSeat &&
                     observedSeat.SittingPoint == plannedSittingPoint))
                {
                    continue;
                }

                Vector2 approachPosition = observedSeat.Seat.GetApproachPosition(
                    observedSeat.SittingPoint);
                float alternativeDistance = Vector2.Distance(
                    body.position,
                    approachPosition);
                if (alternativeDistance + 0.18f < currentDistance &&
                    alternativeDistance <= 2.2f &&
                    CalculateSeatApproachCrowdPressure(approachPosition) <
                    seatApproachCrowdLimit)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryPlanObservedSeat()
        {
            var candidates = new List<ObservedSeat>(observedSeats);

            while (candidates.Count > 0)
            {
                int selectedIndex = -1;
                float bestScore = float.MaxValue;
                int fallbackIndex = -1;
                float bestFallbackScore = float.MaxValue;

                for (int i = 0; i < candidates.Count; i++)
                {
                    if (candidates[i].Seat == null ||
                        candidates[i].SittingPoint == null)
                    {
                        continue;
                    }

                    Vector2 approachPosition = candidates[i].Seat.GetApproachPosition(
                        candidates[i].SittingPoint);
                    if (initialPlacement &&
                        IsInsideDirectBoardingLane(approachPosition))
                    {
                        continue;
                    }

                    float distance = Vector2.Distance(
                        body.position,
                        approachPosition);
                    float sidePriorityPenalty =
                        GetBoardingSeatSidePriority(approachPosition) * 0.45f;
                    float seatPreferencePenalty =
                        GetSeatPreferencePenalty(
                            candidates[i].Seat,
                            candidates[i].SittingPoint);
                    float crowdPressure =
                        CalculateSeatApproachCrowdPressure(approachPosition);
                    float competitionPenalty =
                        CalculateSeatCompetitionPenalty(
                            approachPosition,
                            distance,
                            out bool likelyToLoseSeat);
                    float fallbackScore = distance +
                                          crowdPressure * 0.45f +
                                          competitionPenalty * 0.35f +
                                          sidePriorityPenalty +
                                          seatPreferencePenalty;
                    if (fallbackScore < bestFallbackScore)
                    {
                        bestFallbackScore = fallbackScore;
                        fallbackIndex = i;
                    }

                    if (likelyToLoseSeat ||
                        crowdPressure >= seatApproachCrowdLimit)
                    {
                        continue;
                    }

                    float score = distance +
                                  crowdPressure * 0.8f +
                                  competitionPenalty +
                                  sidePriorityPenalty +
                                  seatPreferencePenalty;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        selectedIndex = i;
                    }
                }

                if (selectedIndex < 0)
                {
                    selectedIndex = fallbackIndex;
                }

                if (selectedIndex < 0)
                {
                    break;
                }

                ObservedSeat observedSeat = candidates[selectedIndex];
                candidates.RemoveAt(selectedIndex);
                if (observedSeat.Seat == null ||
                    observedSeat.SittingPoint == null ||
                    !observedSeat.Seat.IsAvailable(observedSeat.SittingPoint) ||
                    IsRecentlyAbandonedTarget(
                        observedSeat.SittingPoint.position,
                        1.4f))
                {
                    continue;
                }

                plannedSeat = observedSeat.Seat;
                plannedSittingPoint = observedSeat.SittingPoint;
                activityTarget = observedSeat.Seat.GetApproachPosition(
                    observedSeat.SittingPoint);
                observedSeats.Clear();
                return true;
            }

            return false;
        }

        private float GetSeatPreferencePenalty(
            PassengerSeatPrototype seat,
            Transform sittingPoint)
        {
            if (seat == null || sittingPoint == null || seat.Capacity <= 0)
            {
                return 0f;
            }

            int index = seat.GetSittingPointIndex(sittingPoint);
            if (index < 0)
            {
                return 0f;
            }

            int edgeDistance = Mathf.Min(
                index,
                seat.Capacity - 1 - index);
            float edgePenalty;
            if (edgeDistance == 0)
            {
                edgePenalty = 0f;
            }
            else if (edgeDistance == 1)
            {
                edgePenalty = 0.18f;
            }
            else
            {
                edgePenalty = 0.34f + (edgeDistance - 2) * 0.08f;
            }

            bool lowOccupancy = IsCurrentCarLowOccupancy();
            if (!lowOccupancy)
            {
                return edgePenalty * 0.35f;
            }

            int nearestOccupiedDistance =
                seat.GetNearestOccupiedSlotDistance(
                    sittingPoint,
                    gameObject);
            float spacingPenalty = 0f;
            if (nearestOccupiedDistance == 1)
            {
                spacingPenalty = 4.5f;
            }
            else if (nearestOccupiedDistance == 2)
            {
                spacingPenalty = 0.85f;
            }
            else if (nearestOccupiedDistance == 3)
            {
                spacingPenalty = 0.18f;
            }

            return edgePenalty + spacingPenalty;
        }

        private bool IsCurrentCarLowOccupancy()
        {
            if (seats == null || passengerPeers == null)
            {
                return false;
            }

            int seatCapacity = 0;
            foreach (PassengerSeatPrototype seat in seats)
            {
                if (seat != null &&
                    (mapRoot == null || seat.transform.IsChildOf(mapRoot)))
                {
                    seatCapacity += seat.Capacity;
                }
            }

            if (seatCapacity <= 0)
            {
                return false;
            }

            int passengerCount = 0;
            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null ||
                    passenger.mapRoot != mapRoot ||
                    passenger.state == PassengerState.LeavingPlatform)
                {
                    continue;
                }

                passengerCount++;
            }

            return passengerCount / (float)seatCapacity <=
                   lowOccupancySeatRatio;
        }

        private int GetBoardingSeatSidePriority(Vector2 seatPosition)
        {
            if (!initialPlacement ||
                boardingDoorway == null ||
                boardingDoorway.Navigation == null)
            {
                return 0;
            }

            float centerY = boardingDoorway.Navigation.WorldBounds.center.y;
            float boardingSide = Mathf.Sign(
                boardingDoorway.InsidePoint.position.y - centerY);
            float seatSide = Mathf.Sign(seatPosition.y - centerY);
            return Mathf.Approximately(boardingSide, seatSide) ? 0 : 1;
        }

        private float CalculateSeatCompetitionPenalty(
            Vector2 seatApproach,
            float ownDistance,
            out bool likelyToLoseSeat)
        {
            likelyToLoseSeat = false;
            if (passengerPeers == null)
            {
                return 0f;
            }

            float ownArrivalTime = ownDistance /
                                   Mathf.Max(0.1f, GetCurrentMoveSpeed());
            float penalty = 0f;

            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null ||
                    passenger == this ||
                    passenger.body == null ||
                    !CanPerceivePassengerForDecision(passenger) ||
                    !passenger.IsActivelyMoving())
                {
                    continue;
                }

                Vector2 otherToSeat =
                    seatApproach - passenger.body.position;
                float otherDistance = otherToSeat.magnitude;
                Vector2 otherMovement = passenger.GetMovementIntent();
                if (otherDistance > seatCompetitionRadius * 3f ||
                    otherDistance <= 0.001f ||
                    otherMovement.sqrMagnitude <= 0.001f ||
                    Vector2.Dot(
                        otherMovement.normalized,
                        otherToSeat.normalized) < 0.58f)
                {
                    continue;
                }

                float otherArrivalTime =
                    otherDistance /
                    Mathf.Max(0.1f, passenger.GetCurrentMoveSpeed());
                if (otherArrivalTime + seatCompetitionEtaMargin < ownArrivalTime)
                {
                    likelyToLoseSeat = true;
                    penalty += 4f +
                               Mathf.Clamp(
                                   ownArrivalTime - otherArrivalTime,
                                   0f,
                                   2f);
                }
                else
                {
                    penalty += 0.8f;
                }
            }

            return penalty;
        }

        private bool CanPerceivePassengerForDecision(
            GeneralPassengerPrototype passenger)
        {
            return passenger != null &&
                   passenger.body != null &&
                   (perceivedPassengerIds.Contains(passenger.GetInstanceID()) ||
                    Vector2.Distance(body.position, passenger.body.position) <= 0.9f);
        }

        private bool TryReserveActivityPoint(PassengerBehavior selectedBehavior)
        {
            PassengerActivityType pointType = ToActivityType(selectedBehavior);
            var candidates = new List<PassengerActivityPoint>();

            if (activityPoints != null)
            {
                foreach (PassengerActivityPoint point in activityPoints)
                {
                    if (point != null && point.ActivityType == pointType && point.IsAvailableFor(gameObject))
                    {
                        candidates.Add(point);
                    }
                }
            }

            while (candidates.Count > 0)
            {
                PassengerActivityPoint selectedPoint = SelectActivityPoint(candidates);
                candidates.Remove(selectedPoint);
                Vector2 usePosition = selectedPoint.GetUsePosition();
                if ((initialPlacement &&
                     IsInsideDirectBoardingLane(usePosition)) ||
                    !IsStandingPositionAvailable(usePosition) ||
                    IsRecentlyAbandonedTarget(usePosition, 0.8f) ||
                    !selectedPoint.TryReserve(gameObject))
                {
                    continue;
                }

                reservedActivityPoint = selectedPoint;
                activityTarget = selectedPoint.GetUsePosition();
                behavior = selectedBehavior;
                return true;
            }

            return false;
        }

        private bool TryChooseOppositeClosedDoorLeanPosition()
        {
            if (boardingDoorway == null || doorways == null)
            {
                return false;
            }

            Vector2 boardingInward =
                ((Vector2)boardingDoorway.InsidePoint.position -
                 (Vector2)boardingDoorway.OutsidePoint.position).normalized;
            Vector2 selectedPosition = body.position;
            float bestScore = float.MaxValue;
            bool found = false;
            float wallClearance = GetBodyRadius() + wallLeanSurfaceGap;
            float[] lateralOffsets = { -0.48f, 0.48f };

            foreach (PassengerDoorway doorway in doorways)
            {
                if (doorway == null ||
                    doorway == boardingDoorway ||
                    doorway.Door == null ||
                    doorway.InsidePoint == null ||
                    doorway.OutsidePoint == null ||
                    doorway.Navigation == null ||
                    doorway.Door.IsOpen ||
                    doorway.Navigation != boardingDoorway.Navigation)
                {
                    continue;
                }

                Vector2 toDoor =
                    (Vector2)doorway.InsidePoint.position -
                    (Vector2)boardingDoorway.InsidePoint.position;
                if (toDoor.sqrMagnitude <= 0.001f ||
                    Vector2.Dot(boardingInward, toDoor.normalized) < 0.65f)
                {
                    continue;
                }

                Vector2 inward =
                    ((Vector2)doorway.InsidePoint.position -
                     (Vector2)doorway.OutsidePoint.position).normalized;
                Vector2 lateral = new Vector2(-inward.y, inward.x);
                foreach (float lateralOffset in lateralOffsets)
                {
                    Vector2 candidate =
                        (Vector2)doorway.Door.transform.position +
                        inward * wallClearance +
                        lateral * lateralOffset;
                    GridNavigation2D navigation = doorway.Navigation;
                    if (navigation == null ||
                        !navigation.IsWorldWalkable(candidate) ||
                        !IsStandingPositionAvailable(candidate) ||
                        IsSeatFrontLeanPosition(candidate) ||
                        IsInsideDirectBoardingLane(candidate) ||
                        IsRecentlyAbandonedTarget(candidate, 0.85f))
                    {
                        continue;
                    }

                    float score = CalculateCrowdScore(candidate) * 4f +
                                  Vector2.Distance(body.position, candidate) * 0.16f +
                                  Random.Range(0f, 0.08f);
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    selectedPosition = candidate;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            activityTarget = selectedPosition;
            behavior = PassengerBehavior.Leaning;
            return true;
        }

        private bool TryChooseEndWallLeanPosition()
        {
            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null || wallColliders == null)
            {
                return false;
            }

            Rect navigationBounds = navigation.WorldBounds;
            Vector2 selectedPosition = body.position;
            float bestScore = float.MaxValue;
            bool found = false;
            float bodyClearance = GetBodyRadius() + wallLeanSurfaceGap;
            float sampleSpacing = Mathf.Max(
                standingPersonalSpaceRadius + 0.08f,
                GetBodyRadius() * 2f + settledComfortGap);

            foreach (Collider2D wall in wallColliders)
            {
                if (wall == null)
                {
                    continue;
                }

                Bounds bounds = wall.bounds;
                bool verticalEndWall =
                    bounds.size.y > bounds.size.x &&
                    (Mathf.Abs(bounds.center.x - navigationBounds.xMin) < 0.55f ||
                     Mathf.Abs(bounds.center.x - navigationBounds.xMax) < 0.55f);
                if (!verticalEndWall)
                {
                    continue;
                }

                float inward = bounds.center.x < navigationBounds.center.x ? 1f : -1f;
                float candidateX =
                    bounds.center.x + inward * (bounds.extents.x + bodyClearance);
                float minimumY = Mathf.Max(
                    bounds.min.y + bodyClearance,
                    navigationBounds.yMin + bodyClearance);
                float maximumY = Mathf.Min(
                    bounds.max.y - bodyClearance,
                    navigationBounds.yMax - bodyClearance);

                for (float candidateY = minimumY;
                     candidateY <= maximumY + 0.01f;
                     candidateY += sampleSpacing)
                {
                    Vector2 candidate = new Vector2(candidateX, candidateY);
                    if (!navigation.IsWorldWalkable(candidate) ||
                        !IsStandingPositionAvailable(candidate) ||
                        IsSeatFrontLeanPosition(candidate) ||
                        IsInsideDirectBoardingLane(candidate) ||
                        IsRecentlyAbandonedTarget(candidate, 0.85f))
                    {
                        continue;
                    }

                    float score = CalculateCrowdScore(candidate) * 4f +
                                  Vector2.Distance(body.position, candidate) * 0.16f +
                                  Random.Range(0f, 0.08f);
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    selectedPosition = candidate;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            activityTarget = selectedPosition;
            behavior = PassengerBehavior.Leaning;
            return true;
        }

        private bool TryReserveOppositeSideHandhold()
        {
            if (boardingDoorway == null ||
                boardingDoorway.InsidePoint == null ||
                boardingDoorway.OutsidePoint == null ||
                activityPoints == null)
            {
                return false;
            }

            Vector2 boardingInward =
                ((Vector2)boardingDoorway.InsidePoint.position -
                 (Vector2)boardingDoorway.OutsidePoint.position).normalized;
            Vector2 boardingInside = boardingDoorway.InsidePoint.position;
            var candidates = new List<PassengerActivityPoint>();

            foreach (PassengerActivityPoint point in activityPoints)
            {
                if (point == null ||
                    point.ActivityType != PassengerActivityType.Handhold ||
                    !point.IsAvailableFor(gameObject))
                {
                    continue;
                }

                Vector2 usePosition = point.GetUsePosition();
                float oppositeSideDepth = Vector2.Dot(
                    usePosition - boardingInside,
                    boardingInward);
                if (oppositeSideDepth <= 1.2f)
                {
                    continue;
                }

                candidates.Add(point);
            }

            while (candidates.Count > 0)
            {
                PassengerActivityPoint selectedPoint = SelectActivityPoint(candidates);
                candidates.Remove(selectedPoint);
                Vector2 usePosition = selectedPoint.GetUsePosition();
                if (!IsStandingPositionAvailable(usePosition) ||
                    IsInsideDirectBoardingLane(usePosition) ||
                    IsRecentlyAbandonedTarget(usePosition, 0.8f) ||
                    !selectedPoint.TryReserve(gameObject))
                {
                    continue;
                }

                reservedActivityPoint = selectedPoint;
                activityTarget = usePosition;
                behavior = PassengerBehavior.Handhold;
                return true;
            }

            return false;
        }

        private bool TryChooseWallLeanPosition(bool preferDeepInterior)
        {
            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null || wallColliders == null)
            {
                return false;
            }

            Rect navigationBounds = navigation.WorldBounds;
            Vector2 selectedPosition = body.position;
            float bestScore = float.MaxValue;
            bool found = false;
            float bodyClearance = GetBodyRadius() + wallLeanSurfaceGap;

            foreach (Collider2D wall in wallColliders)
            {
                if (wall == null)
                {
                    continue;
                }

                Bounds bounds = wall.bounds;
                bool verticalWall = bounds.size.y > bounds.size.x;

                foreach (float wallSide in new[] { -1f, 1f })
                {
                    for (int sample = 0; sample < 4; sample++)
                    {
                        Vector2 candidate;
                        if (verticalWall)
                        {
                            candidate = new Vector2(
                                bounds.center.x +
                                wallSide * (bounds.extents.x + bodyClearance),
                                Random.Range(
                                    bounds.min.y + bodyClearance,
                                    bounds.max.y - bodyClearance));
                        }
                        else
                        {
                            candidate = new Vector2(
                                Random.Range(
                                    bounds.min.x + bodyClearance,
                                    bounds.max.x - bodyClearance),
                                bounds.center.y +
                                wallSide * (bounds.extents.y + bodyClearance));
                        }

                        if (!navigation.IsWorldWalkable(candidate) ||
                            !IsStandingPositionAvailable(candidate) ||
                            IsSeatFrontLeanPosition(candidate) ||
                            (initialPlacement &&
                             IsInsideDirectBoardingLane(candidate)) ||
                            IsRecentlyAbandonedTarget(candidate, 0.85f))
                        {
                            continue;
                        }

                        float distanceFromDoor = boardingDoorway != null
                            ? Vector2.Distance(
                                boardingDoorway.InsidePoint.position,
                                candidate)
                            : 0f;
                        float inwardDepth = 0f;
                        if (boardingDoorway != null)
                        {
                            Vector2 inwardDirection =
                                ((Vector2)boardingDoorway.InsidePoint.position -
                                 (Vector2)boardingDoorway.OutsidePoint.position).normalized;
                            inwardDepth = Vector2.Dot(
                                candidate -
                                (Vector2)boardingDoorway.InsidePoint.position,
                                inwardDirection);
                        }

                        float score = CalculateCrowdScore(candidate) * 4f +
                                      Vector2.Distance(body.position, candidate) *
                                      (preferDeepInterior ? 0.12f : 0.72f) -
                                      distanceFromDoor *
                                      (preferDeepInterior ? 0.2f : 0.03f) -
                                      inwardDepth *
                                      (preferDeepInterior ? 1.1f : 0.15f) +
                                      Random.Range(0f, 0.08f);
                        if (score >= bestScore)
                        {
                            continue;
                        }

                        bestScore = score;
                        selectedPosition = candidate;
                        found = true;
                    }
                }
            }

            if (!found)
            {
                return false;
            }

            activityTarget = selectedPosition;
            behavior = PassengerBehavior.Leaning;
            return true;
        }

        private bool ShouldGiveUpContestedActivity()
        {
            if (!IsFixedStandingBehavior(behavior) || reservedActivityPoint == null)
            {
                return false;
            }

            if (!reservedActivityPoint.IsReservedBy(gameObject))
            {
                return true;
            }

            if (passengerPeers == null)
            {
                return false;
            }

            Vector2 ownTarget = reservedActivityPoint.transform.position;
            float ownArrivalTime = Vector2.Distance(body.position, ownTarget) /
                                   Mathf.Max(0.1f, GetCurrentMoveSpeed());

            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null ||
                    passenger == this ||
                    passenger.body == null ||
                    !IsFixedStandingBehavior(passenger.behavior) ||
                    passenger.reservedActivityPoint == null)
                {
                    continue;
                }

                Vector2 otherTarget = passenger.reservedActivityPoint.transform.position;
                if (Vector2.Distance(ownTarget, otherTarget) > activityClaimRadius)
                {
                    continue;
                }

                bool alreadyUsingTarget = passenger.state == PassengerState.Observing ||
                                          passenger.state == PassengerState.Yielding;
                if (alreadyUsingTarget)
                {
                    return true;
                }

                if (passenger.state != PassengerState.MovingToActivity)
                {
                    continue;
                }

                float otherArrivalTime = Vector2.Distance(passenger.body.position, otherTarget) /
                                         Mathf.Max(0.1f, passenger.GetCurrentMoveSpeed());
                if (otherArrivalTime + activityClaimLeadTime < ownArrivalTime)
                {
                    return true;
                }

                if (Mathf.Abs(otherArrivalTime - ownArrivalTime) <= activityClaimLeadTime &&
                    passenger.GetInstanceID() < GetInstanceID())
                {
                    return true;
                }
            }

            return false;
        }

        private void ReconsiderContestedActivity()
        {
            PassengerBehavior previousBehavior = behavior;
            abandonedActivityTarget = activityTarget;
            avoidAbandonedTargetUntil = Time.time + 1.2f;
            ReleaseCurrentActivity();
            ClearPath();
            ResetPassengerBlock();

            if (TryChooseBehaviorTarget(previousBehavior))
            {
                RecordDecision("Taken spot -> Other " + previousBehavior);
                state = PassengerState.MovingToActivity;
                UpdateLabel();
                return;
            }

            if (previousBehavior == PassengerBehavior.Leaning &&
                TryChooseBehaviorTarget(PassengerBehavior.Handhold))
            {
                RecordDecision("Taken lean spot -> Handhold");
                state = PassengerState.MovingToActivity;
                UpdateLabel();
                return;
            }

            if (TryChooseLeastCrowdedStandingPosition(out PassengerBehavior standingBehavior))
            {
                behavior = standingBehavior;
                RecordDecision("Taken spot -> Stand nearby");
                state = PassengerState.MovingToActivity;
                UpdateLabel();
                return;
            }

            behavior = PassengerBehavior.AisleStanding;
            activityTarget = body.position;
            RecordDecision("Taken spot -> Stay nearby");
            ScheduleNextDecision();
            SetState(PassengerState.Observing, behavior);
        }

        private bool TryChooseBehaviorTarget(PassengerBehavior selectedBehavior)
        {
            if (selectedBehavior == PassengerBehavior.Seated)
            {
                return TryPlanObservedSeat();
            }

            if (selectedBehavior == PassengerBehavior.Leaning ||
                selectedBehavior == PassengerBehavior.Handhold)
            {
                if (selectedBehavior == PassengerBehavior.Leaning)
                {
                    return TryChooseOppositeClosedDoorLeanPosition() ||
                           TryChooseEndWallLeanPosition() ||
                           TryChooseWallLeanPosition(false);
                }

                return TryReserveOppositeSideHandhold() ||
                       TryReserveActivityPoint(PassengerBehavior.Handhold);
            }

            if (IsFreeStandingBehavior(selectedBehavior))
            {
                return TryChooseFreeStandingPosition(selectedBehavior);
            }

            return TryReserveActivityPoint(selectedBehavior);
        }

        private bool TryChooseFreeStandingPosition(PassengerBehavior selectedBehavior)
        {
            const int maximumAttempts = 28;
            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null)
            {
                return false;
            }

            for (int attempt = 0; attempt < maximumAttempts; attempt++)
            {
                Vector2 candidate;
                if (!CanUseAisleStandingPosition())
                {
                    candidate = GetRandomBoardingPerimeterPosition(navigation);
                    selectedBehavior = PassengerBehavior.AisleStanding;
                }
                else
                {
                    candidate = selectedBehavior == PassengerBehavior.DoorStanding
                        ? GetRandomDoorStandingPosition()
                        : GetRandomAisleStandingPosition(navigation);
                }
                if (!navigation.IsWorldWalkable(candidate) ||
                    !IsStandingPositionAvailable(candidate) ||
                    (initialPlacement &&
                     IsInsideDirectBoardingLane(candidate)) ||
                    IsRecentlyAbandonedTarget(candidate, 0.85f))
                {
                    continue;
                }

                activityTarget = candidate;
                behavior = selectedBehavior;
                return true;
            }

            return false;
        }

        private bool TryChooseLeastCrowdedStandingPosition(
            out PassengerBehavior selectedBehavior)
        {
            const int maximumAttempts = 36;
            selectedBehavior = PassengerBehavior.AisleStanding;
            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null)
            {
                return false;
            }

            Vector2 selectedPosition = body.position;
            float bestScore = float.MaxValue;
            bool found = false;
            int placementTier = GetBoardingPlacementTier();

            for (int attempt = 0; attempt < maximumAttempts; attempt++)
            {
                PassengerBehavior candidateBehavior = PassengerBehavior.AisleStanding;
                Vector2 candidate = initialPlacement && placementTier > 0 && attempt < 26
                    ? GetRandomNearbyStandingPosition(
                        navigation,
                        placementTier == 1 ? 3f : 2.1f)
                    : GetRandomOpenStandingPosition(navigation);
                if (!navigation.IsWorldWalkable(candidate) ||
                    !IsStandingPositionAvailable(candidate) ||
                    (initialPlacement &&
                     IsInsideDirectBoardingLane(candidate)) ||
                    IsRecentlyAbandonedTarget(candidate, 0.85f))
                {
                    continue;
                }

                float crowdScore = CalculateCrowdScore(candidate);
                float distanceFromCurrent = Vector2.Distance(body.position, candidate);
                float distanceFromDoor = boardingDoorway != null
                    ? Vector2.Distance(boardingDoorway.InsidePoint.position, candidate)
                    : 0f;
                float aisleCenterDistance = Mathf.Abs(
                    candidate.y - navigation.WorldBounds.center.y);
                float aisleCenterPenalty = Mathf.Max(
                    0f,
                    1.15f - aisleCenterDistance) * 3.4f;
                float score;
                if (placementTier == 0)
                {
                    score = crowdScore * 3.5f -
                            distanceFromDoor * 0.6f +
                            aisleCenterPenalty +
                            Random.Range(0f, 0.12f);
                }
                else if (placementTier == 1)
                {
                    score = crowdScore * 3.8f +
                            distanceFromCurrent * 0.85f +
                            DistanceToNearestAvailableHandhold(candidate) * 0.5f +
                            aisleCenterPenalty +
                            Random.Range(0f, 0.12f);
                }
                else
                {
                    score = crowdScore * 3.2f +
                            distanceFromCurrent * 1.4f +
                            aisleCenterPenalty +
                            Random.Range(0f, 0.12f);
                }
                if (score < bestScore)
                {
                    bestScore = score;
                    selectedPosition = candidate;
                    selectedBehavior = candidateBehavior;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            activityTarget = selectedPosition;
            behavior = selectedBehavior;
            return true;
        }

        private Vector2 GetRandomNearbyStandingPosition(
            GridNavigation2D navigation,
            float radius)
        {
            Rect bounds = navigation.WorldBounds;
            Vector2 candidate = body.position + Random.insideUnitCircle * radius;
            float horizontalMargin = Mathf.Max(0.45f, GetBodyRadius() + 0.12f);
            float verticalMargin = Mathf.Max(1.1f, GetBodyRadius() + 0.2f);
            candidate.x = Mathf.Clamp(
                candidate.x,
                bounds.xMin + horizontalMargin,
                bounds.xMax - horizontalMargin);
            candidate.y = Mathf.Clamp(
                candidate.y,
                bounds.yMin + verticalMargin,
                bounds.yMax - verticalMargin);
            return candidate;
        }

        private int GetBoardingPlacementTier()
        {
            if (boardingOrder <= 3)
            {
                return 0;
            }

            return boardingOrder <= 7 ? 1 : 2;
        }

        private float DistanceToNearestAvailableHandhold(Vector2 position)
        {
            float nearestDistance = 3f;
            if (activityPoints == null)
            {
                return nearestDistance;
            }

            foreach (PassengerActivityPoint point in activityPoints)
            {
                if (point == null ||
                    point.ActivityType != PassengerActivityType.Handhold ||
                    !point.IsAvailableFor(gameObject))
                {
                    continue;
                }

                nearestDistance = Mathf.Min(
                    nearestDistance,
                    Vector2.Distance(position, point.transform.position));
            }

            return nearestDistance;
        }

        private bool IsRecentlyAbandonedTarget(Vector2 candidate, float clearance)
        {
            return Time.time < avoidAbandonedTargetUntil &&
                   Vector2.Distance(candidate, abandonedActivityTarget) < clearance;
        }

        private Vector2 GetRandomAisleStandingPosition(GridNavigation2D navigation)
        {
            if (!CanUseAisleStandingPosition())
            {
                return GetRandomBoardingPerimeterPosition(navigation);
            }

            Rect bounds = navigation.WorldBounds;
            float horizontalMargin = bounds.width * 0.08f;
            float aisleHalfWidth = Mathf.Min(0.95f, bounds.height * 0.16f);
            return new Vector2(
                Random.Range(bounds.xMin + horizontalMargin, bounds.xMax - horizontalMargin),
                Random.Range(bounds.center.y - aisleHalfWidth, bounds.center.y + aisleHalfWidth));
        }

        private bool CanUseAisleStandingPosition()
        {
            return doorCycle != null && doorCycle.IsTravelling;
        }

        private Vector2 GetRandomBoardingPerimeterPosition(
            GridNavigation2D navigation)
        {
            Rect bounds = navigation.WorldBounds;
            float horizontalMargin = Mathf.Max(0.75f, bounds.width * 0.04f);
            float perimeterOffset = Mathf.Clamp(
                bounds.height * 0.18f,
                0.72f,
                0.9f);
            float side = Random.value < 0.5f ? -1f : 1f;
            return new Vector2(
                Random.Range(
                    bounds.xMin + horizontalMargin,
                    bounds.xMax - horizontalMargin),
                bounds.center.y +
                side * perimeterOffset +
                Random.Range(-0.08f, 0.08f));
        }

        private Vector2 GetRandomDoorStandingPosition()
        {
            var candidates = new List<PassengerDoorway>();
            if (doorways != null)
            {
                foreach (PassengerDoorway doorway in doorways)
                {
                    if (doorway != null && doorway.IsUsable)
                    {
                        candidates.Add(doorway);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return body.position;
            }

            PassengerDoorway selected = candidates[Random.Range(0, candidates.Count)];
            Vector2 inside = selected.InsidePoint.position;
            Vector2 outside = selected.OutsidePoint.position;
            Vector2 inward = (inside - outside).normalized;
            Vector2 lateral = new Vector2(-inward.y, inward.x);
            float side = Random.value < 0.5f ? -1f : 1f;
            return inside +
                   inward * Random.Range(0.25f, 0.65f) +
                   lateral * side * Random.Range(0.95f, 1.25f);
        }

        private GridNavigation2D FindCurrentNavigationArea()
        {
            if (navigationAreas != null)
            {
                foreach (GridNavigation2D navigation in navigationAreas)
                {
                    if (navigation != null && navigation.ContainsWorldPosition(body.position))
                    {
                        return navigation;
                    }
                }
            }

            return boardingDoorway != null ? boardingDoorway.Navigation : null;
        }

        private bool IsStandingPositionAvailable(Vector2 position)
        {
            if (IsBlockedByNavigationObstacle(position) ||
                IsInsideActiveBoardingFlow(position))
            {
                return false;
            }

            return IsSettledPositionComfortable(position);
        }

        private bool IsInsideActiveBoardingFlow(Vector2 position)
        {
            if (doorways == null)
            {
                return false;
            }

            foreach (PassengerDoorway doorway in doorways)
            {
                if (doorway != null &&
                    doorway.IsUsable &&
                    doorway.IsInsideActiveBoardingFlow(position))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSettledPositionComfortable(Vector2 position)
        {
            if (passengerPeers == null)
            {
                return true;
            }

            float ownRadius = GetBodyRadius();
            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null || passenger == this || passenger.body == null)
                {
                    continue;
                }

                float requiredDistance = Mathf.Max(
                    standingPersonalSpaceRadius,
                    ownRadius + passenger.GetBodyRadius() + settledComfortGap);
                bool settledPassenger = passenger.state == PassengerState.Observing ||
                                        passenger.state == PassengerState.Yielding ||
                                        passenger.state == PassengerState.WaitingAtExitDoor;
                bool occupiesStandingSpace = settledPassenger &&
                                               passenger.bodyCollider != null &&
                                               passenger.bodyCollider.enabled &&
                                               passenger.behavior != PassengerBehavior.Seated;
                if (occupiesStandingSpace &&
                    Vector2.Distance(position, passenger.body.position) < requiredDistance)
                {
                    return false;
                }

                bool hasSpatialStandingTarget = UsesSpatialStandingTarget(passenger) &&
                                                (passenger.state == PassengerState.MovingToActivity ||
                                                 (passenger.state == PassengerState.Boarding &&
                                                  passenger.hasBoardingActivityPlan) ||
                                                 passenger.state == PassengerState.Observing ||
                                                 passenger.state == PassengerState.Yielding);
                if (hasSpatialStandingTarget &&
                    Vector2.Distance(position, passenger.activityTarget) < requiredDistance)
                {
                    return false;
                }
            }

            if (playerPeer != null &&
                Vector2.Distance(position, playerPeer.transform.position) <
                standingPersonalSpaceRadius + 0.46f + settledComfortGap)
            {
                return false;
            }

            return true;
        }

        private PassengerActivityPoint SelectActivityPoint(List<PassengerActivityPoint> candidates)
        {
            PassengerActivityPoint selected = candidates[0];
            float bestScore = float.MaxValue;
            foreach (PassengerActivityPoint candidate in candidates)
            {
                Vector2 usePosition = candidate.GetUsePosition();
                float score = CalculateCrowdScore(usePosition) * 1.6f +
                              Vector2.Distance(body.position, usePosition) * 0.55f +
                              Random.Range(0f, 0.12f);
                if (score < bestScore)
                {
                    bestScore = score;
                    selected = candidate;
                }
            }

            return selected;
        }

        private float CalculateCrowdScore(Vector2 position)
        {
            float score = 0f;
            CollectNearbyPassengers(position, 3f, environmentQueryBuffer);
            foreach (GeneralPassengerPrototype passenger in environmentQueryBuffer)
            {
                if (passenger == null || passenger == this || passenger.body == null)
                {
                    continue;
                }

                bool isPerceived =
                    perceivedPassengerIds.Contains(passenger.GetInstanceID()) ||
                    Vector2.Distance(body.position, passenger.body.position) <= 0.9f;
                if (!isPerceived)
                {
                    continue;
                }

                float distance = Vector2.Distance(position, passenger.body.position);
                if (distance < 3f)
                {
                    score += 1f - distance / 3f;
                }
            }

            if (passengerPeers != null)
            {
                foreach (GeneralPassengerPrototype passenger in passengerPeers)
                {
                    if (passenger == null ||
                        passenger == this ||
                        passenger.body == null)
                    {
                        continue;
                    }

                    bool hasStandingTarget = UsesSpatialStandingTarget(passenger) &&
                                             (passenger.state == PassengerState.MovingToActivity ||
                                              passenger.state == PassengerState.Observing);
                    if (hasStandingTarget)
                    {
                        float targetDistance = Vector2.Distance(
                            position,
                            passenger.activityTarget);
                        if (targetDistance < 1.6f)
                        {
                            score += (1f - targetDistance / 1.6f) * 0.8f;
                        }
                    }
                }
            }

            if (playerPeer != null)
            {
                float playerDistance = Vector2.Distance(position, playerPeer.transform.position);
                bool seesPlayer =
                    IsWorldPointVisible(playerPeer.transform.position) ||
                    Vector2.Distance(body.position, playerPeer.transform.position) <= 0.9f;
                if (seesPlayer && playerDistance < 3f)
                {
                    score += 1f - playerDistance / 3f;
                }
            }

            return score;
        }

        private bool HasAvailablePoint(PassengerBehavior candidate)
        {
            if (candidate == PassengerBehavior.Leaning)
            {
                return wallColliders != null && wallColliders.Length > 0;
            }

            if (IsFreeStandingBehavior(candidate))
            {
                return FindCurrentNavigationArea() != null;
            }

            PassengerActivityType pointType = ToActivityType(candidate);
            if (activityPoints == null)
            {
                return false;
            }

            foreach (PassengerActivityPoint point in activityPoints)
            {
                if (point != null && point.ActivityType == pointType && point.IsAvailableFor(gameObject))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFreeStandingBehavior(PassengerBehavior candidate)
        {
            return candidate == PassengerBehavior.DoorStanding ||
                   candidate == PassengerBehavior.AisleStanding;
        }

        private static bool UsesSpatialStandingTarget(GeneralPassengerPrototype passenger)
        {
            return passenger != null &&
                   (IsFreeStandingBehavior(passenger.behavior) ||
                    passenger.behavior == PassengerBehavior.Handhold ||
                    passenger.behavior == PassengerBehavior.Leaning);
        }

        private static bool IsFixedStandingBehavior(PassengerBehavior candidate)
        {
            return candidate == PassengerBehavior.Handhold ||
                   candidate == PassengerBehavior.Leaning;
        }

        private static bool IsStationaryStandingBehavior(
            PassengerBehavior candidate)
        {
            return candidate == PassengerBehavior.Handhold ||
                   candidate == PassengerBehavior.Leaning ||
                   candidate == PassengerBehavior.DoorStanding ||
                   candidate == PassengerBehavior.AisleStanding;
        }

        private static PassengerActivityType ToActivityType(PassengerBehavior selectedBehavior)
        {
            switch (selectedBehavior)
            {
                case PassengerBehavior.Handhold:
                    return PassengerActivityType.Handhold;
                case PassengerBehavior.Leaning:
                    return PassengerActivityType.Lean;
                case PassengerBehavior.DoorStanding:
                    return PassengerActivityType.DoorStanding;
                default:
                    return PassengerActivityType.AisleStanding;
            }
        }

        private void FinishActivityMove()
        {
            ResetActivityProgressTracking();
            initialPlacement = false;
            SetBoardingCollisionBypass(false);
            if (behavior == PassengerBehavior.Seated)
            {
                if (!TryClaimPlannedSeat())
                {
                    HandleLostSeatAtArrival();
                    return;
                }

                SitDown();
            }
            else if (behavior == PassengerBehavior.Leaning)
            {
                if (!TrySnapToWallForLeaning())
                {
                    behavior = PassengerBehavior.AisleStanding;
                    activityTarget = body.position;
                }
            }
            else
            {
                activityTarget = body.position;
                TryAdoptNearbySupport();
            }

            ScheduleNextDecision();
            SetState(PassengerState.Observing, behavior);
        }

        private bool TryClaimPlannedSeat()
        {
            if (plannedSeat == null || plannedSittingPoint == null)
            {
                return false;
            }

            PassengerSeatPrototype seatToClaim = plannedSeat;
            Transform pointToClaim = plannedSittingPoint;
            if (!seatToClaim.TryReserveClosestAvailable(
                    gameObject,
                    pointToClaim,
                    2,
                    out Transform claimedPoint))
            {
                return false;
            }

            reservedSeat = seatToClaim;
            reservedSittingPoint = claimedPoint;
            intentCoordinator?.PublishSeatOccupied(
                this,
                reservedSeat,
                reservedSittingPoint);
            ClearPlannedSeat();
            return true;
        }

        public void ReceiveSeatOccupied(
            GeneralPassengerPrototype source,
            PassengerSeatPrototype occupiedSeat,
            Transform occupiedPoint)
        {
            if (source == null ||
                source == this ||
                occupiedSeat == null ||
                occupiedPoint == null ||
                plannedSeat != occupiedSeat ||
                plannedSittingPoint != occupiedPoint ||
                !IsSeekingSeat)
            {
                return;
            }

            abandonedActivityTarget = activityTarget;
            avoidAbandonedTargetUntil = Time.time + 1.5f;
            ClearPlannedSeat();
            ClearPath();
            ResetPassengerBlock();
            ResetActivityProgressTracking();

            observedSeats.Clear();
            CollectAvailableSeats();
            if (observedSeats.Count > 0 && TryPlanObservedSeat())
            {
                behavior = PassengerBehavior.Seated;
                if (state == PassengerState.Boarding)
                {
                    hasBoardingActivityPlan = true;
                    boardingSeatPlanSwitchCount++;
                }
                else
                {
                    seatCrowdSwitchCount++;
                    state = PassengerState.MovingToActivity;
                }

                nextSeatCrowdCheckTime = Time.time + 0.7f;
                RecordDecision("Seat occupied -> Other seat");
                UpdateLabel();
                return;
            }

            observedSeats.Clear();
            if (state == PassengerState.Boarding &&
                TryChooseInitialStandingBehavior())
            {
                hasBoardingActivityPlan = true;
                RecordDecision("Seat occupied -> Stand");
                UpdateLabel();
                return;
            }

            behavior = PassengerBehavior.None;
            initialPlacement = true;
            RecordDecision("Seat occupied -> Stand");
            ChooseBehavior();
        }

        private void HandleLostSeatAtArrival()
        {
            abandonedActivityTarget = activityTarget;
            avoidAbandonedTargetUntil = Time.time + 1.5f;
            ClearPlannedSeat();
            ClearPath();
            ResetPassengerBlock();
            PerformVisionScan(true);
            observedSeats.Clear();
            CollectAvailableSeats();
            if (observedSeats.Count > 0 && TryPlanObservedSeat())
            {
                behavior = PassengerBehavior.Seated;
                state = PassengerState.MovingToActivity;
                RecordDecision("Seat taken first -> Other visible seat");
                UpdateLabel();
                return;
            }

            observedSeats.Clear();
            behavior = PassengerBehavior.None;
            initialPlacement = true;
            RecordDecision("Seat taken first -> Stand");
            ChooseBehavior();
        }

        private void FaceAwayFromNearestWall()
        {
            if (wallColliders == null || wallColliders.Length == 0)
            {
                Vector2 towardAisle = new Vector2(
                    0f,
                    GetAisleCenterY() - body.position.y);
                if (towardAisle.sqrMagnitude > 0.001f)
                {
                    viewDirection = towardAisle.normalized;
                }

                return;
            }

            Vector2 position = body.position;
            float nearestDistance = float.MaxValue;
            foreach (Collider2D wall in wallColliders)
            {
                if (wall == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(
                    position,
                    wall.ClosestPoint(position));
                nearestDistance = Mathf.Min(nearestDistance, distance);
            }

            Vector2 combinedAwayDirection = Vector2.zero;
            float cornerTolerance = 0.18f;
            foreach (Collider2D wall in wallColliders)
            {
                if (wall == null)
                {
                    continue;
                }

                Vector2 closestPoint = wall.ClosestPoint(position);
                float distance = Vector2.Distance(position, closestPoint);
                if (distance > nearestDistance + cornerTolerance ||
                    distance > wallLeanReleaseDistance + cornerTolerance)
                {
                    continue;
                }

                Vector2 awayFromWall = position - closestPoint;
                if (awayFromWall.sqrMagnitude <= 0.001f)
                {
                    awayFromWall = position - (Vector2)wall.bounds.center;
                }

                if (awayFromWall.sqrMagnitude <= 0.001f)
                {
                    continue;
                }

                float weight = 1f / Mathf.Max(0.08f, distance);
                combinedAwayDirection += awayFromWall.normalized * weight;
            }

            if (combinedAwayDirection.sqrMagnitude > 0.001f)
            {
                viewDirection = combinedAwayDirection.normalized;
                return;
            }

            Vector2 fallbackTowardAisle = new Vector2(
                0f,
                GetAisleCenterY() - position.y);
            if (fallbackTowardAisle.sqrMagnitude > 0.001f)
            {
                viewDirection = fallbackTowardAisle.normalized;
            }
        }

        private bool TryRelocateToComfortableSeat()
        {
            if (state != PassengerState.Observing ||
                behavior != PassengerBehavior.Seated ||
                reservedSeat == null ||
                reservedSittingPoint == null ||
                !IsCurrentCarLowOccupancy() ||
                Time.time < nextSeatedRelocationTime)
            {
                return false;
            }

            ScheduleSeatedRelocationCheck();
            if (Random.value > seatedRelocationChance ||
                reservedSeat.GetNearestOccupiedSlotDistance(
                    reservedSittingPoint,
                    gameObject) != 1)
            {
                return false;
            }

            int currentIndex =
                reservedSeat.GetSittingPointIndex(reservedSittingPoint);
            if (currentIndex < 0)
            {
                return false;
            }

            Transform selectedPoint = null;
            float currentPenalty =
                GetSeatPreferencePenalty(
                    reservedSeat,
                    reservedSittingPoint);
            float bestPenalty = currentPenalty;
            for (int offset = -1; offset <= 1; offset += 2)
            {
                Transform candidate =
                    reservedSeat.GetSittingPoint(currentIndex + offset);
                if (candidate == null ||
                    !reservedSeat.IsAvailable(candidate))
                {
                    continue;
                }

                int nearestOccupied =
                    reservedSeat.GetNearestOccupiedSlotDistance(
                        candidate,
                        gameObject);
                if (nearestOccupied <= 1)
                {
                    continue;
                }

                float candidatePenalty =
                    GetSeatPreferencePenalty(
                        reservedSeat,
                        candidate);
                if (candidatePenalty + 0.45f >= bestPenalty)
                {
                    continue;
                }

                selectedPoint = candidate;
                bestPenalty = candidatePenalty;
            }

            if (selectedPoint == null)
            {
                return false;
            }

            PassengerSeatPrototype targetSeat = reservedSeat;
            ReleaseCurrentActivity();
            plannedSeat = targetSeat;
            plannedSittingPoint = selectedPoint;
            activityTarget = targetSeat.GetApproachPosition(selectedPoint);
            behavior = PassengerBehavior.Seated;
            initialPlacement = false;
            ClearPath();
            ResetPassengerBlock();
            ResetActivityProgressTracking();
            RecordDecision("Seat spacing -> Move one slot");
            state = PassengerState.MovingToActivity;
            UpdateLabel();
            return true;
        }

        private bool TryTakeVisibleOpenSeat()
        {
            if (behavior == PassengerBehavior.Seated ||
                behavior == PassengerBehavior.Exiting ||
                state != PassengerState.Observing)
            {
                return false;
            }

            if (nextSettledSeatCheckTime <= 0f)
            {
                ScheduleSettledSeatCheck();
                return false;
            }

            if (Time.time < nextSettledSeatCheckTime)
            {
                return false;
            }

            ScheduleSettledSeatCheck();
            PerformVisionScan(true);
            PassengerSeatPrototype selectedSeat = null;
            Transform selectedPoint = null;
            float bestScore = float.MaxValue;
            foreach (PassengerSeatPrototype seat in seats)
            {
                if (seat == null)
                {
                    continue;
                }

                for (int index = 0; index < seat.Capacity; index++)
                {
                    Transform sittingPoint = seat.GetSittingPoint(index);
                    if (sittingPoint == null ||
                        !perceivedSeatPoints.Contains(sittingPoint) ||
                        !seat.IsAvailable(sittingPoint))
                    {
                        continue;
                    }

                    Vector2 approachPosition =
                        seat.GetApproachPosition(sittingPoint);
                    float distance = Vector2.Distance(
                        body.position,
                        approachPosition);
                    CalculateSeatCompetitionPenalty(
                        approachPosition,
                        distance,
                        out bool likelyToLoseSeat);
                    float score = distance +
                                  GetSeatPreferencePenalty(
                                      seat,
                                      sittingPoint);
                    if (likelyToLoseSeat ||
                        CalculateSeatApproachCrowdPressure(approachPosition) >=
                        seatApproachCrowdLimit ||
                        score >= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    selectedSeat = seat;
                    selectedPoint = sittingPoint;
                }
            }

            if (selectedSeat == null || selectedPoint == null)
            {
                return false;
            }

            ReleaseCurrentActivity();
            plannedSeat = selectedSeat;
            plannedSittingPoint = selectedPoint;
            activityTarget = selectedSeat.GetApproachPosition(selectedPoint);
            behavior = PassengerBehavior.Seated;
            initialPlacement = false;
            nextSeatCrowdCheckTime = Time.time + 0.7f;
            ClearPath();
            ResetPassengerBlock();
            ResetActivityProgressTracking();
            RecordDecision("Open seat visible -> Seated");
            state = PassengerState.MovingToActivity;
            UpdateLabel();
            return true;
        }

        private void ScheduleSettledSeatCheck()
        {
            nextSettledSeatCheckTime = Time.time + Random.Range(
                settledSeatCheckMinimum,
                Mathf.Max(
                    settledSeatCheckMinimum,
                    settledSeatCheckMaximum));
        }

        private void ScheduleSeatedRelocationCheck()
        {
            nextSeatedRelocationTime = Time.time + Random.Range(
                seatedRelocationMinimumDelay,
                Mathf.Max(
                    seatedRelocationMinimumDelay,
                    seatedRelocationMaximumDelay));
        }

        private void EvaluateReasonedBehaviorChange()
        {
            if (IsFreeStandingBehavior(behavior) && TryAdoptNearbySupport())
            {
                ScheduleNextDecision();
                UpdateLabel();
                return;
            }

            if (behavior != PassengerBehavior.Seated &&
                Random.value < Mathf.Clamp01(seatRecheckChance))
            {
                ChooseKnownSeat();
                return;
            }

            RecordDecision("Stay " + behavior);
            ScheduleNextDecision();
            UpdateLabel();
        }

        private void BeginExitPreparation()
        {
            if (TryWaitForStandUpSpace())
            {
                return;
            }

            ContinueExitPreparation();
        }

        private bool TryWaitForStandUpSpace()
        {
            if (behavior != PassengerBehavior.Seated ||
                reservedSeat == null ||
                reservedSittingPoint == null ||
                bodyCollider == null ||
                bodyCollider.enabled)
            {
                return false;
            }

            Vector2 standPosition =
                reservedSeat.GetApproachPosition(reservedSittingPoint);
            if (IsStandUpSpaceClear(standPosition))
            {
                return false;
            }

            state = PassengerState.WaitingToStand;
            nextStandIntentTime = 0f;
            RecordDecision("Stand requested -> Wait for space");
            BroadcastStandIntent(true);
            UpdateLabel();
            return true;
        }

        private void UpdateWaitingToStand()
        {
            if (reservedSeat == null ||
                reservedSittingPoint == null ||
                bodyCollider == null ||
                bodyCollider.enabled)
            {
                ContinueExitPreparation();
                return;
            }

            Vector2 standPosition =
                reservedSeat.GetApproachPosition(reservedSittingPoint);
            BroadcastStandIntent();
            if (!IsStandUpSpaceClear(standPosition))
            {
                RememberMovementIntent(Vector2.zero);
                return;
            }

            RecordDecision("Stand space clear -> Get up");
            ReleaseCurrentActivity();
            ContinueExitPreparation();
        }

        private bool IsStandUpSpaceClear(Vector2 standPosition)
        {
            CollectNearbyPassengers(
                standPosition,
                standClearanceRadius + 0.35f,
                nearbyPassengerBuffer);
            Vector2 seatPosition = reservedSittingPoint != null
                ? (Vector2)reservedSittingPoint.position
                : body.position;
            foreach (GeneralPassengerPrototype passenger in nearbyPassengerBuffer)
            {
                if (passenger == null ||
                    passenger.body == null ||
                    passenger.bodyCollider == null ||
                    !passenger.bodyCollider.enabled)
                {
                    continue;
                }

                float standDistance = Vector2.Distance(
                    passenger.body.position,
                    standPosition);
                float routeDistance = DistancePointToSegment(
                    passenger.body.position,
                    seatPosition,
                    standPosition);
                if (standDistance < standClearanceRadius ||
                    routeDistance < minimumBodySeparation * 0.85f)
                {
                    return false;
                }
            }

            return playerPeer == null ||
                   Vector2.Distance(
                       playerPeer.transform.position,
                       standPosition) >= standClearanceRadius;
        }

        private void BroadcastStandIntent(bool force = false)
        {
            if (intentCoordinator == null ||
                reservedSeat == null ||
                reservedSittingPoint == null ||
                (!force && Time.time < nextStandIntentTime))
            {
                return;
            }

            Vector2 sourcePosition = reservedSittingPoint.position;
            Vector2 standPosition =
                reservedSeat.GetApproachPosition(reservedSittingPoint);
            Vector2 direction = standPosition - sourcePosition;
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            intentCoordinator.PublishStandIntent(
                new PassengerStandIntent(
                    this,
                    sourcePosition,
                    standPosition,
                    direction.normalized,
                    Time.time + standIntentInterval * 2f));
            nextStandIntentTime = Time.time + standIntentInterval;
        }

        public void ReceiveStandIntent(PassengerStandIntent intent)
        {
            if (intent.Source == null ||
                intent.ExpiresAt < Time.time ||
                intent.Direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            bool updatesActiveYield =
                state == PassengerState.Yielding &&
                courtesyYieldSource == intent.Source;
            if (!updatesActiveYield &&
                (state != PassengerState.Observing ||
                 !IsStationaryStandingBehavior(behavior)))
            {
                return;
            }

            float routeDistance = DistancePointToSegment(
                body.position,
                intent.SourcePosition,
                intent.StandPosition);
            if (routeDistance > routePassingClearance ||
                Vector2.Distance(body.position, intent.StandPosition) >
                standingYieldDetectionRadius)
            {
                return;
            }

            if (!TryFindRouteYieldTarget(
                    intent.SourcePosition,
                    intent.Direction.normalized,
                    out Vector2 yieldTarget))
            {
                return;
            }

            if (updatesActiveYield)
            {
                courtesyYieldUntil = Mathf.Max(
                    courtesyYieldUntil,
                    Time.time + 0.8f);
                SetCourtesyYieldTarget(yieldTarget);
                return;
            }

            obstructionYieldCount++;
            RecordDecision("Standing passenger -> Yield");
            BeginCourtesyYield(intent.Source, yieldTarget);
        }

        private void ContinueExitPreparation()
        {
            ReleaseCurrentActivity();
            ClearPath();
            exitDoorway = SelectExitDoorway();

            if (exitDoorway == null)
            {
                exitDoorway = boardingDoorway;
            }

            ReserveExitDoorway(exitDoorway);
            ConfigureExitAisleTarget();
            nextExitDoorReviewTime = Time.time + Random.Range(0.25f, 0.45f);

            SetState(PassengerState.PreparingToExit, PassengerBehavior.Exiting);
            BroadcastExitIntent(true);
        }

        private void ConfigureExitAisleTarget()
        {
            float aisleY = GetAisleCenterY();
            float seatSide = Mathf.Sign(body.position.y - aisleY);
            if (Mathf.Approximately(seatSide, 0f))
            {
                seatSide = GetInstanceID() % 2 == 0 ? -1f : 1f;
            }

            int lateralSlot = Mathf.Abs(GetInstanceID()) % 3 - 1;
            exitAisleTarget = new Vector2(
                body.position.x + lateralSlot * 0.32f,
                aisleY + seatSide * 0.12f);

            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation != null)
            {
                Rect bounds = navigation.WorldBounds;
                exitAisleTarget.x = Mathf.Clamp(
                    exitAisleTarget.x,
                    bounds.xMin + 0.55f,
                    bounds.xMax - 0.55f);
                exitAisleTarget.y = Mathf.Clamp(
                    exitAisleTarget.y,
                    bounds.yMin + 0.55f,
                    bounds.yMax - 0.55f);
            }

            reachedExitAisle = Vector2.Distance(body.position, exitAisleTarget) <= 0.18f;
            nextExitAisleRerouteTime = Time.time + 0.8f;
            ClearPath();
            ResetPassengerBlock();
        }

        private bool TryRerouteExitAisleEntry()
        {
            nextExitAisleRerouteTime = Time.time + 1.1f;
            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null)
            {
                return false;
            }

            float aisleY = GetAisleCenterY();
            float aisleSide = Mathf.Sign(exitAisleTarget.y - aisleY);
            if (Mathf.Approximately(aisleSide, 0f))
            {
                aisleSide = 1f;
            }

            Vector2[] candidates =
            {
                new Vector2(body.position.x - 0.72f, aisleY + aisleSide * 0.12f),
                new Vector2(body.position.x + 0.72f, aisleY + aisleSide * 0.12f),
                new Vector2(body.position.x - 0.95f, aisleY - aisleSide * 0.08f),
                new Vector2(body.position.x + 0.95f, aisleY - aisleSide * 0.08f)
            };

            Vector2 selected = exitAisleTarget;
            float bestScore = float.MaxValue;
            foreach (Vector2 candidate in candidates)
            {
                if (!navigation.IsWorldWalkable(candidate) ||
                    !IsEmergencyYieldPositionAvailable(candidate))
                {
                    continue;
                }

                float score = CalculateCrowdScore(candidate) * 1.4f +
                              Vector2.Distance(body.position, candidate) * 0.25f;
                if (score < bestScore)
                {
                    bestScore = score;
                    selected = candidate;
                }
            }

            if (bestScore == float.MaxValue)
            {
                return false;
            }

            exitAisleTarget = selected;
            ClearPath();
            ResetPassengerBlock();
            RecordDecision("Exit aisle blocked -> Side step");
            return true;
        }

        private void BroadcastExitIntent(bool force = false)
        {
            if (intentCoordinator == null || exitDoorway == null ||
                (!force && Time.time < nextIntentBroadcastTime))
            {
                return;
            }

            float aisleY = GetAisleCenterY();
            Vector2 sourcePosition = body.position;
            Vector2 destination = exitWaitingTarget;
            var intent = new PassengerExitIntent(
                this,
                exitDoorway,
                sourcePosition,
                new Vector2(sourcePosition.x, aisleY),
                new Vector2(destination.x, aisleY),
                destination,
                Time.time + 0.8f);
            intentCoordinator.PublishExitIntent(intent);
            nextIntentBroadcastTime = Time.time + 0.35f;
        }

        public void ReceiveExitIntent(PassengerExitIntent intent)
        {
            if (intent.Source == null ||
                intent.ExpiresAt < Time.time)
            {
                return;
            }

            bool updatesActiveYield = state == PassengerState.Yielding &&
                                      courtesyYieldSource == intent.Source;
            if (updatesActiveYield)
            {
                courtesyYieldIntent = intent;
                hasCourtesyYieldIntent = true;
                courtesyYieldDoorway = intent.Doorway;
                courtesyYieldUntil = Mathf.Max(
                    courtesyYieldUntil,
                    Time.time + 0.65f);
                if (IsBlockingExitIntent(intent) &&
                    TryFindCourtesyYieldTarget(intent, out Vector2 updatedTarget))
                {
                    TryApplyCourtesyYieldRetarget(intent, updatedTarget, true);
                }

                return;
            }

            if (state != PassengerState.Observing ||
                (behavior != PassengerBehavior.Handhold &&
                 behavior != PassengerBehavior.DoorStanding &&
                 behavior != PassengerBehavior.Leaning &&
                 behavior != PassengerBehavior.AisleStanding) ||
                !IsBlockingExitIntent(intent))
            {
                return;
            }

            Vector2 routeDirection = GetNearestIntentSegmentDirection(
                body.position,
                intent);
            if (!ShouldYieldForPassingRoute(intent.Source, routeDirection))
            {
                return;
            }

            if (!TryFindCourtesyYieldTarget(intent, out Vector2 yieldTarget))
            {
                return;
            }

            BeginCourtesyYield(intent.Source, yieldTarget, intent.Doorway);
            courtesyYieldIntent = intent;
            hasCourtesyYieldIntent = true;
        }

        private void BroadcastPassIntent(Vector2 desiredDirection)
        {
            if (intentCoordinator == null ||
                blockingPassenger == null ||
                desiredDirection.sqrMagnitude <= 0.001f ||
                blockedByPassengerSince < 0f ||
                Time.time - blockedByPassengerSince < passIntentDelay ||
                Time.time < nextPassIntentTime)
            {
                return;
            }

            var intent = new PassengerPassIntent(
                this,
                blockingPassenger,
                body.position,
                desiredDirection.normalized,
                activityTarget,
                Time.time + 0.65f);
            intentCoordinator.PublishPassIntent(intent);
            nextPassIntentTime = Time.time + passIntentInterval;
        }

        public void ReceivePassIntent(PassengerPassIntent intent)
        {
            if (intent.Source == null ||
                intent.Recipient != this ||
                intent.ExpiresAt < Time.time ||
                state != PassengerState.Observing ||
                !IsStationaryStandingBehavior(behavior) ||
                intent.Direction.sqrMagnitude <= 0.001f ||
                Vector2.Distance(body.position, intent.SourcePosition) >
                standingYieldDetectionRadius * 1.45f ||
                Vector2.Dot(
                    (body.position - intent.SourcePosition).normalized,
                    intent.Direction.normalized) < 0.05f ||
                !IsDirectlyBlockingPassingRoute(
                    intent.SourcePosition,
                    intent.Direction.normalized) ||
                !ShouldYieldForPassingRoute(
                    intent.Source,
                    intent.Direction.normalized))
            {
                return;
            }

            obstructionSource = intent.Source;
            obstructionDirection = intent.Direction.normalized;
            obstructionScore = obstructionYieldThreshold;
            lastObstructionReportTime = Time.time;
            obstructionYieldCooldownUntil = 0f;
            if (!TryFindRouteYieldTarget(
                    intent.SourcePosition,
                    obstructionDirection,
                    out Vector2 yieldTarget))
            {
                return;
            }

            obstructionScore = 0f;
            obstructionSource = null;
            obstructionYieldCooldownUntil = Time.time + Random.Range(0.8f, 1.2f);
            obstructionYieldCount++;
            RecordDecision("Pass request -> Yield");
            BeginCourtesyYield(intent.Source, yieldTarget);
        }

        private void BeginCourtesyYield(
            GeneralPassengerPrototype source,
            Vector2 yieldTarget,
            PassengerDoorway exitFlowDoorway = null)
        {
            if (Vector2.Distance(body.position, yieldTarget) <= 0.08f)
            {
                standingYieldCooldownUntil = Time.time + Random.Range(0.8f, 1.2f);
                return;
            }

            bool releasedHandhold = behavior == PassengerBehavior.Handhold;

            bool urgentExit = source != null && source.HasUrgentExitPriority();
            courtesyYieldSource = source;
            courtesyYieldDoorway = exitFlowDoorway;
            hasCourtesyYieldIntent = false;
            courtesyYieldSawDoorOpen = false;
            courtesyYieldOrigin = body.position;
            courtesyYieldTarget = yieldTarget;
            courtesyYieldStartedAt = Time.time;
            nextCourtesyYieldReevaluationTime =
                Time.time + courtesyYieldReevaluationInterval;
            courtesyYieldCanReturnAt = Time.time +
                                       (urgentExit
                                           ? Random.Range(0.25f, 0.4f)
                                           : Random.Range(0.4f, 0.65f));
            courtesyYieldUntil = Time.time +
                                 (urgentExit
                                     ? Random.Range(1.3f, 1.8f)
                                     : Random.Range(0.9f, 1.3f));
            reachedCourtesyYieldTarget = false;
            ClearPath();
            RecordDecision(
                releasedHandhold
                    ? "Release handhold -> Yield"
                    : "Step back -> Yield");
            state = PassengerState.Yielding;
            UpdateLabel();
        }

        private bool IsBlockingExitIntent(PassengerExitIntent intent)
        {
            Vector2 position = body.position;
            return DistancePointToSegment(position, intent.SourcePosition, intent.AisleEntry) <=
                   exitIntentCorridorWidth ||
                   DistancePointToSegment(position, intent.AisleEntry, intent.AisleExit) <=
                   exitIntentCorridorWidth ||
                   DistancePointToSegment(position, intent.AisleExit, intent.Destination) <=
                   exitIntentCorridorWidth;
        }

        private bool TryFindCourtesyYieldTarget(
            PassengerExitIntent intent,
            out Vector2 yieldTarget)
        {
            Vector2 position = body.position;
            Vector2 routeDirection = GetNearestIntentSegmentDirection(position, intent);
            if (routeDirection.sqrMagnitude <= 0.001f)
            {
                routeDirection = (intent.Destination - intent.SourcePosition).normalized;
            }

            Vector2 perpendicular = new Vector2(-routeDirection.y, routeDirection.x);
            Vector2[] candidates =
            {
                position + perpendicular * courtesyStepDistance,
                position - perpendicular * courtesyStepDistance,
                position + perpendicular * courtesyStepDistance * 1.45f,
                position - perpendicular * courtesyStepDistance * 1.45f,
                position - routeDirection * courtesyStepDistance * 0.75f,
                position - routeDirection * courtesyStepDistance * 1.25f,
                GetWallTuckPosition(position)
            };

            float bestScore = float.MinValue;
            yieldTarget = position;
            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                Vector2 candidate = candidates[candidateIndex];
                if (!IsEmergencyYieldPositionAvailable(candidate))
                {
                    continue;
                }

                bool wallTuckCandidate = candidateIndex == candidates.Length - 1 &&
                                         Vector2.Distance(candidate, position) > 0.05f;
                float score = DistanceToExitIntentRoute(candidate, intent) +
                              (wallTuckCandidate ? 1.25f : 0f) -
                              Vector2.Distance(position, candidate) * 0.08f;
                if (score > bestScore)
                {
                    bestScore = score;
                    yieldTarget = candidate;
                }
            }

            return bestScore > float.MinValue;
        }

        private Vector2 GetWallTuckPosition(Vector2 position)
        {
            if (!TryGetNearbyWall(position, out Collider2D wall))
            {
                return position;
            }

            Vector2 closestPoint = wall.ClosestPoint(position);
            Vector2 awayFromWall = position - closestPoint;
            if (awayFromWall.sqrMagnitude <= 0.001f)
            {
                awayFromWall = new Vector2(
                    GetAisleCenterY() - position.y,
                    0f);
            }

            return closestPoint + awayFromWall.normalized * (GetBodyRadius() + 0.08f);
        }

        private void UpdateCourtesyYield()
        {
            if (ShouldPrepareToExit())
            {
                state = PassengerState.Observing;
                BeginExitPreparation();
                return;
            }

            ReevaluateCourtesyYieldTarget();

            float maximumYieldDuration = hasCourtesyYieldIntent
                ? courtesyYieldMaximumDuration * 2f
                : courtesyYieldMaximumDuration;
            bool exceededMaximumDuration =
                Time.time - courtesyYieldStartedAt >= maximumYieldDuration;

            if (!reachedCourtesyYieldTarget)
            {
                reachedCourtesyYieldTarget = MoveDirectly(courtesyYieldTarget);
                if (!reachedCourtesyYieldTarget &&
                    (exceededMaximumDuration ||
                     (Time.time - courtesyYieldStartedAt >= 0.55f &&
                      !IsCourtesyYieldSourceStillMoving())))
                {
                    FinishCourtesyYieldInPlace();
                }

                return;
            }

            bool sourcePassed = courtesyYieldSource == null ||
                                courtesyYieldSource.body == null ||
                                 Vector2.Distance(
                                     body.position,
                                     courtesyYieldSource.body.position) >
                                 standingYieldDetectionRadius * 1.15f ||
                                courtesyYieldSource.state == PassengerState.LeavingPlatform ||
                                !IsCourtesyYieldSourceStillMoving();
            if (courtesyYieldDoorway != null &&
                courtesyYieldDoorway.Door != null &&
                courtesyYieldDoorway.Door.IsOpen)
            {
                courtesyYieldSawDoorOpen = true;
            }

            bool exitFlowActive = courtesyYieldDoorway != null &&
                                  courtesyYieldDoorway.ExitReservationCount >= 3 &&
                                  (!courtesyYieldSawDoorOpen ||
                                   (courtesyYieldDoorway.Door != null &&
                                    courtesyYieldDoorway.Door.IsOpen));
            if ((!exceededMaximumDuration && exitFlowActive) ||
                Time.time < courtesyYieldCanReturnAt ||
                (!sourcePassed && Time.time < courtesyYieldUntil))
            {
                RememberMovementIntent(Vector2.zero);
                return;
            }

            if (exceededMaximumDuration)
            {
                FinishCourtesyYieldInPlace();
                return;
            }

            if (courtesyYieldDoorway == null && IsFreeStandingBehavior(behavior))
            {
                activityTarget = body.position;
                CompleteCourtesyYield();
                standingYieldCooldownUntil = Time.time + Random.Range(1.2f, 2f);
                return;
            }

            if (!IsCourtesyPositionAvailable(courtesyYieldOrigin))
            {
                FinishCourtesyYieldInPlace();
                return;
            }

            if (!MoveDirectly(courtesyYieldOrigin))
            {
                return;
            }

            CompleteCourtesyYield();
        }

        private void ReevaluateCourtesyYieldTarget()
        {
            if (courtesyYieldSource == null ||
                courtesyYieldSource.body == null ||
                Time.time < nextCourtesyYieldReevaluationTime)
            {
                return;
            }

            nextCourtesyYieldReevaluationTime =
                Time.time + courtesyYieldReevaluationInterval;
            if (hasCourtesyYieldIntent)
            {
                PassengerExitIntent currentIntent = new PassengerExitIntent(
                    courtesyYieldSource,
                    courtesyYieldDoorway,
                    courtesyYieldSource.body.position,
                    courtesyYieldIntent.AisleEntry,
                    courtesyYieldIntent.AisleExit,
                    courtesyYieldIntent.Destination,
                    Time.time + courtesyYieldReevaluationInterval * 2f);
                courtesyYieldIntent = currentIntent;
                if (IsBlockingExitIntent(currentIntent) &&
                    TryFindCourtesyYieldTarget(currentIntent, out Vector2 exitYieldTarget))
                {
                    TryApplyCourtesyYieldRetarget(
                        currentIntent,
                        exitYieldTarget,
                        false);
                }

                return;
            }

            Vector2 moverDirection = courtesyYieldSource.GetMovementIntent();
            if (moverDirection.sqrMagnitude <= 0.001f ||
                !TryFindRouteYieldTarget(
                    courtesyYieldSource.body.position,
                    moverDirection.normalized,
                    out Vector2 routeYieldTarget))
            {
                return;
            }

            Vector2 routeEnd = courtesyYieldSource.body.position +
                               moverDirection.normalized * standingYieldDetectionRadius;
            float currentClearance = DistancePointToSegment(
                courtesyYieldTarget,
                courtesyYieldSource.body.position,
                routeEnd);
            float candidateClearance = DistancePointToSegment(
                routeYieldTarget,
                courtesyYieldSource.body.position,
                routeEnd);
            bool currentUnavailable =
                !IsEmergencyYieldPositionAvailable(courtesyYieldTarget);
            if (currentUnavailable ||
                candidateClearance >= currentClearance +
                courtesyYieldRetargetMinimumGain)
            {
                SetCourtesyYieldTarget(routeYieldTarget);
            }
        }

        private bool TryApplyCourtesyYieldRetarget(
            PassengerExitIntent intent,
            Vector2 candidate,
            bool routeWasUpdated)
        {
            float currentClearance = DistanceToExitIntentRoute(
                courtesyYieldTarget,
                intent);
            float candidateClearance = DistanceToExitIntentRoute(candidate, intent);
            float requiredGain = routeWasUpdated
                ? courtesyYieldRetargetMinimumGain * 0.5f
                : courtesyYieldRetargetMinimumGain;
            bool currentUnavailable =
                !IsEmergencyYieldPositionAvailable(courtesyYieldTarget);
            bool currentStillBlocksRoute =
                currentClearance <= exitIntentCorridorWidth + 0.05f;
            if (!currentUnavailable &&
                !currentStillBlocksRoute &&
                candidateClearance < currentClearance + requiredGain)
            {
                return false;
            }

            SetCourtesyYieldTarget(candidate);
            return true;
        }

        private void SetCourtesyYieldTarget(Vector2 target)
        {
            if (Vector2.Distance(courtesyYieldTarget, target) <= 0.06f)
            {
                return;
            }

            courtesyYieldTarget = target;
            reachedCourtesyYieldTarget =
                Vector2.Distance(body.position, courtesyYieldTarget) <= arrivalDistance;
            ClearPath();
            ResetPassengerBlock();
            RecordDecision("Yield route changed -> Better side");
        }

        private void CompleteCourtesyYield()
        {
            courtesyYieldSource = null;
            courtesyYieldDoorway = null;
            hasCourtesyYieldIntent = false;
            courtesyYieldSawDoorOpen = false;
            reachedCourtesyYieldTarget = false;
            courtesyYieldStartedAt = 0f;
            courtesyYieldCanReturnAt = 0f;
            courtesyYieldUntil = 0f;
            obstructionScore = 0f;
            obstructionSource = null;
            ScheduleNextDecision();
            state = PassengerState.Observing;
            RecordDecision("Yield complete");
            UpdateLabel();
        }

        private void FinishCourtesyYieldInPlace()
        {
            ReleaseCurrentActivity();
            behavior = PassengerBehavior.AisleStanding;
            activityTarget = body.position;
            standingYieldCooldownUntil = Time.time + Random.Range(1.2f, 2f);
            CompleteCourtesyYield();
        }

        private bool IsCourtesyYieldSourceStillMoving()
        {
            if (courtesyYieldSource == null || courtesyYieldSource.body == null)
            {
                return false;
            }

            return courtesyYieldSource.IsActivelyMoving() ||
                   courtesyYieldSource.HasUrgentExitPriority();
        }

        private bool TryYieldForPassingTraffic()
        {
            if (!IsStationaryStandingBehavior(behavior) ||
                Time.time < nextStandingYieldCheckTime ||
                Time.time < standingYieldCooldownUntil)
            {
                return false;
            }

            nextStandingYieldCheckTime = Time.time + Random.Range(0.18f, 0.3f);
            GeneralPassengerPrototype nearestMover = null;
            Vector2 nearestIntent = Vector2.zero;
            float nearestDistance = float.MaxValue;
            CollectNearbyPassengers(
                body.position,
                standingYieldDetectionRadius,
                nearbyPassengerBuffer);
            IReadOnlyList<GeneralPassengerPrototype> passengers =
                nearbyPassengerBuffer;

            foreach (GeneralPassengerPrototype passenger in passengers)
            {
                if (passenger == null ||
                    passenger == this ||
                    passenger.body == null ||
                    !passenger.IsActivelyMoving() ||
                    passenger.state == PassengerState.Yielding)
                {
                    continue;
                }

                Vector2 intent = passenger.GetMovementIntent();
                float distance = Vector2.Distance(body.position, passenger.body.position);
                if (intent.sqrMagnitude <= 0.01f ||
                    distance > standingYieldDetectionRadius ||
                    DistancePointToSegment(
                        body.position,
                        passenger.body.position,
                        passenger.body.position + intent.normalized * standingYieldDetectionRadius) >
                    exitIntentCorridorWidth ||
                    !ShouldYieldForPassingRoute(passenger, intent.normalized) ||
                    distance >= nearestDistance)
                {
                    continue;
                }

                nearestMover = passenger;
                nearestIntent = intent.normalized;
                nearestDistance = distance;
            }

            if (nearestMover != null &&
                TryFindRouteYieldTarget(nearestMover.body.position, nearestIntent, out Vector2 moverYieldTarget))
            {
                BeginCourtesyYield(nearestMover, moverYieldTarget);
                return true;
            }

            return false;
        }

        private bool ShouldYieldForPassingRoute(
            GeneralPassengerPrototype mover,
            Vector2 moverDirection)
        {
            if (mover == null ||
                mover.body == null ||
                moverDirection.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            if (!mover.HasUrgentExitPriority() && IsSettledAtProtectedEdge())
            {
                return false;
            }

            if (!IsDirectlyBlockingPassingRoute(
                    mover.body.position,
                    moverDirection))
            {
                return false;
            }

            Vector2 offset = body.position - mover.body.position;
            if (Vector2.Dot(offset, moverDirection) <= 0f)
            {
                return false;
            }

            Vector2 perpendicular = new Vector2(
                -moverDirection.y,
                moverDirection.x);
            Vector2 leftPassingPoint =
                body.position + perpendicular * routePassingClearance;
            Vector2 rightPassingPoint =
                body.position - perpendicular * routePassingClearance;

            return !IsPassingLaneOpen(leftPassingPoint, mover) &&
                   !IsPassingLaneOpen(rightPassingPoint, mover);
        }

        private bool IsDirectlyBlockingPassingRoute(
            Vector2 moverPosition,
            Vector2 moverDirection)
        {
            if (moverDirection.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            Vector2 direction = moverDirection.normalized;
            Vector2 offset = body.position - moverPosition;
            float forwardDistance = Vector2.Dot(offset, direction);
            if (forwardDistance <= 0.04f ||
                forwardDistance > standingYieldDetectionRadius * 1.15f)
            {
                return false;
            }

            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float lateralDistance = Mathf.Abs(Vector2.Dot(offset, perpendicular));
            float blockingHalfWidth = Mathf.Max(
                GetBodyRadius() * 1.15f,
                minimumBodySeparation * 0.82f);
            return lateralDistance <= blockingHalfWidth;
        }

        private bool IsSettledAtProtectedEdge()
        {
            if (state != PassengerState.Observing ||
                !IsStationaryStandingBehavior(behavior) ||
                IsInsideDirectBoardingLane(body.position))
            {
                return false;
            }

            return behavior == PassengerBehavior.Leaning ||
                   IsNearWall(
                       body.position,
                       Mathf.Max(
                           wallLeanReleaseDistance,
                           GetBodyRadius() + 0.16f));
        }

        private bool IsPassingLaneOpen(
            Vector2 position,
            GeneralPassengerPrototype mover)
        {
            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null ||
                !navigation.IsWorldWalkable(position) ||
                IsBlockedByNavigationObstacle(position))
            {
                return false;
            }

            CollectNearbyPassengers(
                position,
                comfortableBodySeparation,
                environmentQueryBuffer);
            IReadOnlyList<GeneralPassengerPrototype> nearby =
                environmentQueryBuffer;
            foreach (GeneralPassengerPrototype passenger in nearby)
            {
                if (passenger == null ||
                    passenger == this ||
                    passenger == mover ||
                    passenger.bodyCollider == null ||
                    !passenger.bodyCollider.enabled)
                {
                    continue;
                }

                return false;
            }

            return playerPeer == null ||
                   Vector2.Distance(
                       position,
                       playerPeer.transform.position) >=
                   comfortableBodySeparation;
        }

        private bool TryFindRouteYieldTarget(
            Vector2 moverPosition,
            Vector2 moverDirection,
            out Vector2 yieldTarget)
        {
            Vector2 perpendicular = new Vector2(-moverDirection.y, moverDirection.x);
            Vector2[] candidates =
            {
                body.position + perpendicular * courtesyStepDistance * 0.65f,
                body.position - perpendicular * courtesyStepDistance * 0.65f,
                body.position + perpendicular * courtesyStepDistance,
                body.position - perpendicular * courtesyStepDistance,
                body.position + perpendicular * courtesyStepDistance * 1.35f,
                body.position - perpendicular * courtesyStepDistance * 1.35f,
                body.position - moverDirection * courtesyStepDistance * 0.7f,
                body.position - moverDirection * courtesyStepDistance +
                perpendicular * courtesyStepDistance * 0.65f,
                body.position - moverDirection * courtesyStepDistance -
                perpendicular * courtesyStepDistance * 0.65f,
                GetWallTuckPosition(body.position)
            };

            yieldTarget = body.position;
            Vector2 routeEnd = moverPosition + moverDirection * standingYieldDetectionRadius;
            float currentRouteDistance = DistancePointToSegment(
                body.position,
                moverPosition,
                routeEnd);
            float minimumClearanceGain = Mathf.Min(
                courtesyRouteClearanceGain,
                courtesyStepDistance * 0.1f);
            float bestScore = float.MinValue;
            foreach (Vector2 candidate in candidates)
            {
                GridNavigation2D navigation = FindCurrentNavigationArea();
                if (navigation == null ||
                    !navigation.IsWorldWalkable(candidate) ||
                    !IsEmergencyYieldPositionAvailable(candidate))
                {
                    continue;
                }

                float routeDistance = DistancePointToSegment(
                    candidate,
                    moverPosition,
                    routeEnd);
                if (routeDistance < currentRouteDistance + minimumClearanceGain)
                {
                    continue;
                }

                float score =
                    routeDistance * 4f -
                    CalculateCrowdScore(candidate) * 0.45f -
                    Vector2.Distance(body.position, candidate) * 0.2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    yieldTarget = candidate;
                }
            }

            return bestScore > float.MinValue;
        }

        private void ReportObstruction(
            GeneralPassengerPrototype source,
            Vector2 sourceDirection)
        {
            if (source == null ||
                source == this ||
                !CanYieldForStationaryTraffic(source) ||
                (Time.time < obstructionYieldCooldownUntil &&
                 !source.HasUrgentExitPriority()) ||
                source.state == PassengerState.Yielding)
            {
                return;
            }

            Vector2 offset = body.position - source.body.position;
            if (offset.magnitude > standingYieldDetectionRadius ||
                sourceDirection.sqrMagnitude <= 0.001f ||
                Vector2.Dot(offset.normalized, sourceDirection.normalized) < 0.25f ||
                !IsDirectlyBlockingPassingRoute(
                    source.body.position,
                    sourceDirection.normalized) ||
                !ShouldYieldForPassingRoute(source, sourceDirection.normalized))
            {
                return;
            }

            obstructionSource = source;
            obstructionDirection = sourceDirection.normalized;
            lastObstructionReportTime = Time.time;
            if (source.HasUrgentExitPriority())
            {
                obstructionScore = obstructionYieldThreshold;
                obstructionYieldCooldownUntil = 0f;
                return;
            }

            float sourceUrgency = Mathf.Clamp01(source.GetMovementPriority() / 3f);
            obstructionScore = Mathf.Min(
                obstructionYieldThreshold * 1.5f,
                obstructionScore + Time.fixedDeltaTime * Mathf.Lerp(0.75f, 1.3f, sourceUrgency));
        }

        private void UpdateObstructionPressure()
        {
            if (Time.time - lastObstructionReportTime > 0.25f)
            {
                obstructionScore = Mathf.MoveTowards(
                    obstructionScore,
                    0f,
                    obstructionScoreDecay * Time.fixedDeltaTime);
            }

            if (obstructionSource == null || obstructionScore <= 0f)
            {
                obstructionSource = null;
            }
        }

        private bool TryYieldFromObstructionPressure()
        {
            if (obstructionScore < obstructionYieldThreshold ||
                obstructionSource == null ||
                !CanYieldForStationaryTraffic(obstructionSource) ||
                obstructionSource.state == PassengerState.Yielding ||
                Time.time < obstructionYieldCooldownUntil ||
                !TryFindRouteYieldTarget(
                    obstructionSource.body.position,
                    obstructionDirection,
                    out Vector2 yieldTarget))
            {
                return false;
            }

            GeneralPassengerPrototype source = obstructionSource;
            obstructionScore = 0f;
            obstructionSource = null;
            obstructionYieldCooldownUntil = Time.time + Random.Range(2f, 3f);
            obstructionYieldCount++;
            RecordDecision("Blocking route -> Yield");
            BeginCourtesyYield(source, yieldTarget);
            return true;
        }

        private bool CanYieldForStationaryTraffic(GeneralPassengerPrototype source)
        {
            if (source == null || state != PassengerState.Observing)
            {
                return false;
            }

            return IsStationaryStandingBehavior(behavior) &&
                   source.IsActivelyMoving() &&
                   (source.HasUrgentExitPriority() ||
                    !IsSettledAtProtectedEdge());
        }

        private bool IsEmergencyYieldPositionAvailable(Vector2 position)
        {
            if (IsBlockedByNavigationObstacle(position))
            {
                return false;
            }

            if (passengerPeers != null)
            {
                foreach (GeneralPassengerPrototype passenger in passengerPeers)
                {
                    if (passenger == null ||
                        passenger == this ||
                        passenger.state != PassengerState.Yielding ||
                        Vector2.Distance(
                            position,
                            passenger.courtesyYieldTarget) >=
                        minimumBodySeparation)
                    {
                        continue;
                    }

                    return false;
                }
            }

            Collider2D[] overlaps = Physics2D.OverlapCircleAll(position, 0.34f);
            foreach (Collider2D overlap in overlaps)
            {
                GeneralPassengerPrototype passenger =
                    overlap.GetComponentInParent<GeneralPassengerPrototype>();
                if (passenger != null && passenger != this)
                {
                    return false;
                }

                if (overlap.GetComponentInParent<PlayerBoardingCyclePrototype>() != null)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsCourtesyPositionAvailable(Vector2 position)
        {
            if (IsBlockedByNavigationObstacle(position))
            {
                return false;
            }

            Collider2D[] overlaps = Physics2D.OverlapCircleAll(position, 0.48f);
            foreach (Collider2D overlap in overlaps)
            {
                GeneralPassengerPrototype passenger =
                    overlap.GetComponentInParent<GeneralPassengerPrototype>();
                if (passenger != null && passenger != this)
                {
                    return false;
                }

                if (overlap.GetComponentInParent<PlayerBoardingCyclePrototype>() != null)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector2 GetNearestIntentSegmentDirection(
            Vector2 position,
            PassengerExitIntent intent)
        {
            Vector2[] starts = { intent.SourcePosition, intent.AisleEntry, intent.AisleExit };
            Vector2[] ends = { intent.AisleEntry, intent.AisleExit, intent.Destination };
            float nearestDistance = float.MaxValue;
            Vector2 nearestDirection = Vector2.zero;

            for (int i = 0; i < starts.Length; i++)
            {
                float distance = DistancePointToSegment(position, starts[i], ends[i]);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestDirection = (ends[i] - starts[i]).normalized;
                }
            }

            return nearestDirection;
        }

        private static float DistanceToExitIntentRoute(
            Vector2 position,
            PassengerExitIntent intent)
        {
            return Mathf.Min(
                DistancePointToSegment(position, intent.SourcePosition, intent.AisleEntry),
                DistancePointToSegment(position, intent.AisleEntry, intent.AisleExit),
                DistancePointToSegment(position, intent.AisleExit, intent.Destination));
        }

        private static float DistancePointToSegment(
            Vector2 point,
            Vector2 segmentStart,
            Vector2 segmentEnd)
        {
            Vector2 segment = segmentEnd - segmentStart;
            if (segment.sqrMagnitude <= 0.0001f)
            {
                return Vector2.Distance(point, segmentStart);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - segmentStart, segment) /
                                    segment.sqrMagnitude);
            return Vector2.Distance(point, segmentStart + segment * t);
        }

        private PassengerDoorway SelectExitDoorway()
        {
            PassengerDoorway selected = null;
            float bestScore = float.MaxValue;

            if (doorways == null)
            {
                return boardingDoorway;
            }

            foreach (PassengerDoorway doorway in doorways)
            {
                if (doorway == null || !doorway.IsUsable)
                {
                    continue;
                }

                float score = CalculateExitDoorwayScore(doorway);
                if (score < bestScore)
                {
                    selected = doorway;
                    bestScore = score;
                }
            }

            return selected ?? boardingDoorway;
        }

        private void ReserveExitDoorway(PassengerDoorway doorway)
        {
            if (doorway == null)
            {
                return;
            }

            doorway.TryReserveExit(gameObject);
            hasExitReservation = true;
            exitWaitingTarget = doorway.GetExitWaitingPosition(gameObject);
            exitCrossingTarget = doorway.GetExitCrossingPosition(gameObject);
            exitOutsideTarget = doorway.GetExitOutsidePosition(gameObject);
        }

        private void ReleaseExitReservation()
        {
            if (!hasExitReservation || exitDoorway == null)
            {
                return;
            }

            exitDoorway.ReleaseExit(gameObject);
            hasExitReservation = false;
        }

        private void ReleaseCurrentActivity()
        {
            ClearPlannedSeat();
            if (reservedSeat != null)
            {
                if (bodyCollider != null && !bodyCollider.enabled)
                {
                    StandUp();
                }

                reservedSeat.Release(gameObject);
                reservedSeat = null;
                reservedSittingPoint = null;
            }

            if (reservedActivityPoint != null)
            {
                reservedActivityPoint.Release(gameObject);
                reservedActivityPoint = null;
            }
        }

        private void ClearPlannedSeat()
        {
            plannedSeat = null;
            plannedSittingPoint = null;
        }

        private void SitDown()
        {
            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            body.bodyType = RigidbodyType2D.Kinematic;
            SetPosition(reservedSittingPoint.position);
            Vector2 aisleDirection = activityTarget - (Vector2)reservedSittingPoint.position;
            if (aisleDirection.sqrMagnitude > 0.001f)
            {
                viewDirection = aisleDirection.normalized;
            }

            ScheduleSeatedRelocationCheck();
        }

        private void StandUp()
        {
            SetPosition(reservedSeat.GetApproachPosition(reservedSittingPoint));
            body.bodyType = RigidbodyType2D.Dynamic;

            if (bodyCollider != null)
            {
                bodyCollider.enabled = true;
            }
        }

        private bool ShouldPrepareToExit()
        {
            if (waitsUntilDoorForExit)
            {
                return !doorCycle.IsTravelling &&
                       plannedExitStopNumber == doorCycle.StopNumber;
            }

            return doorCycle.IsTravelling &&
                   plannedExitStopNumber == doorCycle.StopNumber + 1 &&
                   doorCycle.TravelTimeRemaining <= exitPreparationLeadTime;
        }

        private bool MoveUsingPath(
            Vector2 destination,
            bool ignorePassengerAvoidance = false)
        {
            if (Vector2.Distance(body.position, destination) <= arrivalDistance)
            {
                return true;
            }

            if (!ignorePassengerAvoidance && hasBlockedDetourTarget)
            {
                if (!MoveDirectly(blockedDetourTarget))
                {
                    return false;
                }

                hasBlockedDetourTarget = false;
                blockedDetourAttempted = false;
                RecordDecision("Detour clear -> Resume");
                ResetPassengerBlock();
                ClearPath();
            }

            if (!hasPathTarget ||
                Vector2.Distance(pathTarget, destination) > 0.05f ||
                Time.time >= nextRepathTime)
            {
                RebuildPath(destination, ignorePassengerAvoidance);
            }

            if (path.Count == 0 || pathIndex >= path.Count)
            {
                if (!ignorePassengerAvoidance &&
                    pathBlockedByPassengers &&
                    !HasClearForwardMovementArc(destination - body.position))
                {
                    RememberMovementIntent(Vector2.zero);
                    return false;
                }

                return ignorePassengerAvoidance
                    ? MoveWithoutAvoidance(destination)
                    : MoveDirectly(destination);
            }

            Vector2 waypoint = path[pathIndex];
            if (ignorePassengerAvoidance)
            {
                MoveWithoutAvoidance(waypoint);
            }
            else
            {
                MoveBodyTowards(waypoint);
            }

            if (Vector2.Distance(body.position, waypoint) <= arrivalDistance + 0.04f)
            {
                pathIndex++;
            }

            return Vector2.Distance(body.position, destination) <= arrivalDistance;
        }

        private void RebuildPath(
            Vector2 destination,
            bool ignorePassengerAvoidance)
        {
            path.Clear();
            pathBlockedByPassengers = false;
            Vector2 start = body.position;
            List<Vector2> stationaryPassengers = ignorePassengerAvoidance
                ? null
                : CollectStationaryPassengerPositions(destination);

            if (!ignorePassengerAvoidance &&
                !initialPlacement &&
                ShouldUseAisleRoute(destination))
            {
                float aisleY = GetAisleCenterY() + GetPreferredAisleOffset();
                float horizontalDirection = Mathf.Sign(destination.x - start.x);
                float mergeDistance = Mathf.Min(
                    1.8f,
                    Mathf.Abs(destination.x - start.x) * 0.35f);
                Vector2 aisleEntry = new Vector2(
                    start.x + horizontalDirection * mergeDistance,
                    aisleY);
                float approachLead = Mathf.Abs(destination.x - start.x) > 1.4f ? 1.1f : 0f;
                Vector2 aisleApproach = new Vector2(
                    destination.x - horizontalDirection * approachLead,
                    aisleY);

                AppendPathSegment(start, aisleEntry, stationaryPassengers);
                if (pathBlockedByPassengers)
                {
                    return;
                }

                AppendPathSegment(aisleEntry, aisleApproach, stationaryPassengers);
                if (pathBlockedByPassengers)
                {
                    return;
                }

                AppendPathSegment(aisleApproach, destination, stationaryPassengers);
            }
            else
            {
                AppendPathSegment(start, destination, stationaryPassengers);
            }

            while (path.Count > 0 &&
                   Vector2.Distance(body.position, path[0]) <= arrivalDistance + 0.12f)
            {
                path.RemoveAt(0);
            }

            pathIndex = 0;
            pathTarget = destination;
            hasPathTarget = true;
            nextRepathTime = Time.time + 1.5f;
        }

        private void AppendPathSegment(
            Vector2 start,
            Vector2 destination,
            List<Vector2> stationaryPassengers)
        {
            if (Vector2.Distance(start, destination) <= 0.1f)
            {
                return;
            }

            List<Vector3> shortestPath = FindShortestPath(
                start,
                destination,
                stationaryPassengers);
            bool canKeepMovingThroughCrowd =
                HasUrgentExitPriority() ||
                HasClearForwardMovementArc(destination - start);
            if ((shortestPath == null || shortestPath.Count == 0) &&
                stationaryPassengers != null &&
                stationaryPassengers.Count > 0 &&
                canKeepMovingThroughCrowd)
            {
                shortestPath = FindShortestPath(start, destination, null);
            }

            if (shortestPath == null || shortestPath.Count == 0)
            {
                if (stationaryPassengers != null &&
                    stationaryPassengers.Count > 0 &&
                    !canKeepMovingThroughCrowd)
                {
                    pathBlockedByPassengers = true;
                    return;
                }

                if (path.Count == 0 || Vector2.Distance(path[path.Count - 1], destination) > 0.1f)
                {
                    path.Add(destination);
                }

                return;
            }

            foreach (Vector3 point in shortestPath)
            {
                if (path.Count == 0 || Vector2.Distance(path[path.Count - 1], point) > 0.08f)
                {
                    path.Add(point);
                }
            }
        }

        private List<Vector3> FindShortestPath(
            Vector2 start,
            Vector2 destination,
            List<Vector2> stationaryPassengers)
        {
            List<Vector3> shortestPath = null;
            if (navigationAreas == null)
            {
                return null;
            }

            foreach (GridNavigation2D navigation in navigationAreas)
            {
                if (navigation == null)
                {
                    continue;
                }

                List<Vector3> candidatePath = stationaryPassengers == null
                    ? navigation.FindWorldPath(start, destination)
                    : navigation.FindWorldPath(
                        start,
                        destination,
                        point => IsWalkableAroundStationaryPassengers(
                            point,
                            start,
                            destination,
                            stationaryPassengers));
                if (candidatePath.Count == 0 ||
                    (shortestPath != null && candidatePath.Count >= shortestPath.Count))
                {
                    continue;
                }

                shortestPath = candidatePath;
            }

            return shortestPath;
        }

        private List<Vector2> CollectStationaryPassengerPositions(
            Vector2 destination)
        {
            var positions = new List<Vector2>();
            if (passengerPeers == null)
            {
                return positions;
            }

            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null ||
                    passenger == this ||
                    passenger.body == null ||
                    passenger.bodyCollider == null ||
                    !passenger.bodyCollider.enabled ||
                    (passenger.state != PassengerState.Observing &&
                     passenger.state != PassengerState.Yielding &&
                     passenger.state != PassengerState.WaitingAtExitDoor) ||
                    Vector2.Distance(passenger.body.position, destination) < 0.45f)
                {
                    continue;
                }

                positions.Add(passenger.body.position);
            }

            if (playerPeer != null &&
                Vector2.Distance(playerPeer.transform.position, destination) >= 0.45f)
            {
                positions.Add(playerPeer.transform.position);
            }

            return positions;
        }

        private bool IsWalkableAroundStationaryPassengers(
            Vector2 point,
            Vector2 start,
            Vector2 destination,
            List<Vector2> stationaryPassengers)
        {
            if (Vector2.Distance(point, start) <= stationaryRouteClearance ||
                Vector2.Distance(point, destination) <= 0.3f)
            {
                return true;
            }

            foreach (Vector2 passengerPosition in stationaryPassengers)
            {
                if (Vector2.Distance(point, passengerPosition) <
                    stationaryRouteClearance)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ShouldUseAisleRoute(Vector2 destination)
        {
            if (passengerPeers == null)
            {
                return false;
            }

            bool meaningfulTravel = Vector2.Distance(body.position, destination) > 1.2f;
            bool horizontalTravel = Mathf.Abs(destination.x - body.position.x) > 1.5f;
            if (!meaningfulTravel || !horizontalTravel)
            {
                return false;
            }

            float aisleY = GetAisleCenterY();
            float minimumX = Mathf.Min(body.position.x, destination.x) - 0.8f;
            float maximumX = Mathf.Max(body.position.x, destination.x) + 0.8f;
            bool upperSideBlocked = false;
            bool lowerSideBlocked = false;

            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null ||
                    passenger == this ||
                    passenger.body == null ||
                    passenger.behavior != PassengerBehavior.Handhold ||
                    passenger.state != PassengerState.Observing)
                {
                    continue;
                }

                Vector2 passengerPosition = passenger.body.position;
                if (passengerPosition.x < minimumX || passengerPosition.x > maximumX)
                {
                    continue;
                }

                upperSideBlocked |= passengerPosition.y > aisleY + 0.45f;
                lowerSideBlocked |= passengerPosition.y < aisleY - 0.45f;
            }

            return upperSideBlocked && lowerSideBlocked;
        }

        private float GetAisleCenterY()
        {
            return mapRoot != null ? mapRoot.position.y : 0f;
        }

        private float GetPreferredAisleOffset()
        {
            int lane = Mathf.Abs(GetInstanceID()) % 5;
            return (lane - 2) * 0.16f;
        }

        private bool MoveDirectly(Vector2 destination)
        {
            bool detouringAroundPassenger = hasBlockedDetourTarget &&
                                             Vector2.Distance(destination, blockedDetourTarget) > 0.05f;
            if (detouringAroundPassenger)
            {
                if (Time.time >= blockedGiveUpAt)
                {
                    hasBlockedDetourTarget = false;
                    blockedDetourAttempted = false;
                    ResetPassengerBlock();
                }
                else
                {
                    MoveBodyTowards(blockedDetourTarget);
                    if (Vector2.Distance(body.position, blockedDetourTarget) <=
                        arrivalDistance + 0.08f)
                    {
                        hasBlockedDetourTarget = false;
                        blockedDetourAttempted = false;
                        ResetPassengerBlock();
                    }

                    return false;
                }
            }

            MoveBodyTowards(destination);
            return Vector2.Distance(body.position, destination) <= arrivalDistance;
        }

        private bool MoveWithoutAvoidance(
            Vector2 destination,
            float speedMultiplier = 1f)
        {
            Vector2 toDestination = destination - body.position;
            if (toDestination.sqrMagnitude <= arrivalDistance * arrivalDistance)
            {
                return true;
            }

            float step = Mathf.Min(
                GetCurrentMoveSpeed() * Mathf.Max(0.1f, speedMultiplier) *
                Time.fixedDeltaTime,
                toDestination.magnitude);
            Vector2 direction = toDestination.normalized;
            if (Time.time >= headLookHoldUntil)
            {
                viewDirection = Vector2.Lerp(
                    viewDirection,
                    direction,
                    Mathf.Clamp01(8f * Time.fixedDeltaTime)).normalized;
            }

            RememberMovementIntent(direction);
            Vector2 proposedPosition =
                body.position + direction * step;
            Vector2 nextPosition = initialPlacement
                ? proposedPosition
                : KeepMinimumBodySeparation(
                    proposedPosition,
                    direction);
            if ((state == PassengerState.Boarding ||
                 clearingBoardingDoor ||
                 initialPlacement) &&
                Vector2.Distance(nextPosition, body.position) <
                step * 0.25f)
            {
                BeginSoftSqueeze(direction);
                nextPosition = KeepMinimumBodySeparation(
                    proposedPosition,
                    direction);
                if (Vector2.Distance(nextPosition, body.position) <
                    step * 0.25f &&
                    IsInsideActiveBoardingFlow(body.position) &&
                    !IsBlockedByNavigationObstacle(proposedPosition))
                {
                    nextPosition = proposedPosition;
                }
            }

            nextPosition = PreventPassengerCrossing(
                nextPosition,
                state == PassengerState.Boarding ||
                clearingBoardingDoor ||
                initialPlacement ||
                IsExtremelyCrowded(body.position));
            body.MovePosition(nextPosition);
            return Vector2.Distance(body.position, destination) <= arrivalDistance;
        }

        private bool MoveThroughExit(Vector2 destination)
        {
            Vector2 toDestination = destination - body.position;
            if (toDestination.sqrMagnitude <= arrivalDistance * arrivalDistance)
            {
                return true;
            }

            Vector2 direction = toDestination.normalized;
            Vector2 separation = CalculateSeparationVector(
                minimumBodySeparation * 1.35f);
            if (separation.sqrMagnitude > 0.01f)
            {
                Vector2 separatedDirection =
                    (direction * 0.82f + separation.normalized * 0.18f).normalized;
                if (Vector2.Dot(separatedDirection, direction) >= 0.65f)
                {
                    direction = separatedDirection;
                }
            }

            float step = Mathf.Min(
                GetCurrentMoveSpeed() * Time.fixedDeltaTime,
                toDestination.magnitude);
            if (Time.time >= headLookHoldUntil)
            {
                viewDirection = Vector2.Lerp(
                    viewDirection,
                    direction,
                    Mathf.Clamp01(8f * Time.fixedDeltaTime)).normalized;
            }

            RememberMovementIntent(direction);
            Vector2 nextPosition = PreventPassengerCrossing(
                body.position + direction * step,
                true);
            body.MovePosition(nextPosition);
            return Vector2.Distance(body.position, destination) <= arrivalDistance;
        }

        private void BeginLeavingPlatform()
        {
            Vector2 inside = exitDoorway.InsidePoint.position;
            Vector2 outside = exitDoorway.OutsidePoint.position;
            Vector2 outward = (outside - inside).normalized;
            Vector2 lateral = new Vector2(-outward.y, outward.x);
            platformExitTarget = outside +
                                 outward * 2.8f +
                                 lateral * Random.Range(-1.1f, 1.1f);

            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            body.bodyType = RigidbodyType2D.Kinematic;
            ClearPath();
        }

        private void MoveBodyTowards(Vector2 destination)
        {
            Vector2 toDestination = destination - body.position;
            if (toDestination.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float step = Mathf.Min(
                GetCurrentMoveSpeed() * Time.fixedDeltaTime,
                toDestination.magnitude);
            Vector2 desiredDirection = toDestination.normalized;
            Vector2 movementDirection = GetLocallyAvoidedDirection(desiredDirection);
            RememberMovementIntent(
                movementDirection.sqrMagnitude > 0.0001f
                    ? movementDirection
                    : desiredDirection * 0.15f);
            if (movementDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            if (Time.time >= headLookHoldUntil)
            {
                viewDirection = Vector2.Lerp(
                    viewDirection,
                    movementDirection.normalized,
                    Mathf.Clamp01(8f * Time.fixedDeltaTime)).normalized;
            }

            Vector2 nextPosition = body.position + movementDirection * step;
            float desiredBodySeparation =
                GetDesiredBodySeparation(body.position);
            float activeMinimumSeparation =
                GetActiveMinimumBodySeparation();
            bool hasContinuousMovementPriority =
                HasUrgentExitPriority() ||
                HasClearForwardMovementArc(movementDirection);
            float nextPositionClearance = hasContinuousMovementPriority
                ? activeMinimumSeparation
                : desiredBodySeparation;
            GeneralPassengerPrototype passengerAtNextPosition =
                FindPassengerTooCloseTo(nextPosition, nextPositionClearance);
            if (passengerAtNextPosition != null &&
                !IsPriorityMovementSeparating(passengerAtNextPosition, nextPosition))
            {
                blockingPassenger = passengerAtNextPosition;
                Vector2 recoveryDirection = GetBlockedMovementDirection(desiredDirection);
                if (recoveryDirection.sqrMagnitude <= 0.0001f)
                {
                    RememberMovementIntent(Vector2.zero);
                    return;
                }

                nextPosition = body.position + recoveryDirection * step;
                activeMinimumSeparation =
                    GetActiveMinimumBodySeparation();
                float recoveryClearance = hasContinuousMovementPriority
                    ? activeMinimumSeparation
                    : Mathf.Max(
                        activeMinimumSeparation,
                        desiredBodySeparation * 0.92f);
                GeneralPassengerPrototype recoveryBlocker =
                    FindPassengerTooCloseTo(nextPosition, recoveryClearance);
                if (recoveryBlocker != null &&
                    !IsPriorityMovementSeparating(recoveryBlocker, nextPosition))
                {
                    RememberMovementIntent(Vector2.zero);
                    return;
                }
            }

            nextPosition = KeepMinimumBodySeparation(nextPosition, movementDirection);
            nextPosition = PreventPassengerCrossing(
                nextPosition,
                Time.time < softContactUntil ||
                IsExtremelyCrowded(body.position));
            body.MovePosition(nextPosition);
        }

        private void ResolveImmediatePassengerOverlap()
        {
            if (body == null ||
                bodyCollider == null ||
                !bodyCollider.enabled ||
                boardingCollisionBypassActive ||
                body.bodyType != RigidbodyType2D.Dynamic)
            {
                return;
            }

            float activeMinimumSeparation =
                GetActiveMinimumBodySeparation();
            CollectNearbyPassengers(
                body.position,
                activeMinimumSeparation,
                environmentQueryBuffer);
            Vector2 correction = Vector2.zero;
            int correctionCount = 0;
            float ownPriority = GetMovementPriority();

            foreach (GeneralPassengerPrototype passenger in environmentQueryBuffer)
            {
                if (passenger == null ||
                    passenger.body == null ||
                    passenger.bodyCollider == null ||
                    !passenger.bodyCollider.enabled)
                {
                    continue;
                }

                Vector2 offset = body.position - passenger.body.position;
                float distance = offset.magnitude;
                if (distance >= activeMinimumSeparation)
                {
                    continue;
                }

                bool selfIsSettled = state == PassengerState.Observing &&
                                     IsStationaryStandingBehavior(behavior);
                bool otherIsSettled = passenger.state == PassengerState.Observing &&
                                      IsStationaryStandingBehavior(passenger.behavior);
                float otherPriority = passenger.GetMovementPriority();
                bool shouldStepAway;
                if (selfIsSettled != otherIsSettled)
                {
                    shouldStepAway = !selfIsSettled;
                }
                else
                {
                    shouldStepAway = ownPriority < otherPriority - 0.05f ||
                                     (Mathf.Abs(ownPriority - otherPriority) <= 0.05f &&
                                      GetInstanceID() > passenger.GetInstanceID());
                }
                if (!shouldStepAway)
                {
                    continue;
                }

                if (distance <= 0.001f)
                {
                    Vector2 otherIntent = passenger.GetMovementIntent();
                    Vector2 baseDirection = otherIntent.sqrMagnitude > 0.001f
                        ? otherIntent.normalized
                        : Vector2.up;
                    float side = GetInstanceID() > passenger.GetInstanceID()
                        ? 1f
                        : -1f;
                    offset = new Vector2(
                        -baseDirection.y,
                        baseDirection.x) * side;
                    distance = 0f;
                }

                correction += offset.normalized *
                              (activeMinimumSeparation - distance + 0.02f);
                correctionCount++;
            }

            if (correctionCount == 0 || correction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 candidate = body.position +
                                Vector2.ClampMagnitude(correction, 0.22f);
            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation != null &&
                navigation.IsWorldWalkable(candidate) &&
                !IsBlockedByNavigationObstacle(candidate))
            {
                body.MovePosition(candidate);
            }
        }

        private Vector2 KeepMinimumBodySeparation(
            Vector2 proposedPosition,
            Vector2 movementDirection)
        {
            if (passengerPeers == null)
            {
                return proposedPosition;
            }

            Vector2 adjustedPosition = proposedPosition;
            float requiredSeparation =
                GetDesiredBodySeparation(proposedPosition);
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (GeneralPassengerPrototype passenger in passengerPeers)
                {
                    if (passenger == null ||
                    passenger == this ||
                    passenger.body == null ||
                    passenger.bodyCollider == null ||
                    !passenger.bodyCollider.enabled)
                    {
                        continue;
                    }

                    Vector2 offset = adjustedPosition - passenger.body.position;
                    float distance = offset.magnitude;
                    if (distance >= requiredSeparation)
                    {
                        continue;
                    }

                    if (distance <= 0.001f)
                    {
                        Vector2 baseDirection = movementDirection.sqrMagnitude > 0.001f
                            ? movementDirection.normalized
                            : Vector2.up;
                        float side = GetInstanceID() < passenger.GetInstanceID() ? -1f : 1f;
                        offset = new Vector2(-baseDirection.y, baseDirection.x) * side;
                        distance = 0f;
                    }

                    adjustedPosition += offset.normalized *
                                        (requiredSeparation - distance);
                }
            }

            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation != null &&
                navigation.ContainsWorldPosition(body.position) &&
                (!navigation.IsWorldWalkable(adjustedPosition) ||
                 IsBlockedByNavigationObstacle(adjustedPosition)))
            {
                return body.position;
            }

            return adjustedPosition;
        }

        private Vector2 PreventPassengerCrossing(
            Vector2 proposedPosition,
            bool allowTightCrowd)
        {
            if (passengerPeers == null)
            {
                return proposedPosition;
            }

            Vector2 start = body.position;
            Vector2 movement = proposedPosition - start;
            if (movement.sqrMagnitude <= 0.000001f)
            {
                return proposedPosition;
            }

            float noCrossingDistance = minimumBodySeparation *
                                       noCrossingSeparationRatio *
                                       (allowTightCrowd ? 0.92f : 1f);
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (GeneralPassengerPrototype passenger in passengerPeers)
                {
                    if (passenger == null ||
                        passenger == this ||
                        passenger.body == null ||
                        passenger.bodyCollider == null ||
                        !passenger.bodyCollider.enabled)
                    {
                        continue;
                    }

                    Vector2 otherPosition = passenger.body.position;
                    Vector2 startOffset = start - otherPosition;
                    float startDistance = startOffset.magnitude;
                    Vector2 endOffset = start + movement - otherPosition;
                    float endDistance = endOffset.magnitude;
                    if (startDistance < noCrossingDistance &&
                        endDistance > startDistance + 0.001f)
                    {
                        continue;
                    }

                    float movementLengthSquared = movement.sqrMagnitude;
                    if (movementLengthSquared <= 0.000001f)
                    {
                        break;
                    }

                    float closestTime = Mathf.Clamp01(
                        Vector2.Dot(otherPosition - start, movement) /
                        movementLengthSquared);
                    Vector2 closestOffset =
                        start + movement * closestTime - otherPosition;
                    if (closestOffset.sqrMagnitude >=
                        noCrossingDistance * noCrossingDistance)
                    {
                        continue;
                    }

                    Vector2 separationNormal = startOffset.sqrMagnitude > 0.001f
                        ? startOffset.normalized
                        : closestOffset.sqrMagnitude > 0.001f
                            ? closestOffset.normalized
                            : (GetInstanceID() < passenger.GetInstanceID()
                                ? Vector2.left
                                : Vector2.right);
                    float inwardMovement = Vector2.Dot(
                        movement,
                        separationNormal);
                    if (inwardMovement < 0f)
                    {
                        movement -= separationNormal * inwardMovement;
                    }
                }
            }

            return start + movement;
        }

        private bool IsPriorityMovementSeparating(
            GeneralPassengerPrototype passenger,
            Vector2 nextPosition)
        {
            Vector2 movement = nextPosition - body.position;
            if ((!HasUrgentExitPriority() &&
                 !HasClearForwardMovementArc(movement)) ||
                passenger == null ||
                passenger.body == null)
            {
                return false;
            }

            float currentDistance = Vector2.Distance(
                body.position,
                passenger.body.position);
            float nextDistance = Vector2.Distance(
                nextPosition,
                passenger.body.position);
            return nextDistance > currentDistance + 0.002f;
        }

        private Vector2 GetLocallyAvoidedDirection(Vector2 desiredDirection)
        {
            float radius = GetAvoidanceRadius();
            GeneralPassengerPrototype passengerAhead =
                FindPassengerDirectlyAhead(desiredDirection, radius);
            if (passengerAhead != null)
            {
                blockingPassenger = passengerAhead;
            }

            if (ShouldWaitForPassenger(passengerAhead, desiredDirection))
            {
                RememberMovementIntent(Vector2.zero);
                return Vector2.zero;
            }

            if (!HasActorAhead(desiredDirection, radius))
            {
                if (!IsStillCloseToBlockingPassenger())
                {
                    ResetPassengerBlock();
                }

                return desiredDirection;
            }

            Vector2 perpendicular = new Vector2(-desiredDirection.y, desiredDirection.x);
            float selectedSide = avoidanceSide;
            bool headOnApproach = IsHeadOnApproach(
                passengerAhead,
                desiredDirection);

            if (Time.time >= avoidanceSideUntil ||
                Mathf.Approximately(selectedSide, 0f))
            {
                Vector2 leftDirection = BuildAvoidanceDirection(
                    desiredDirection,
                    perpendicular,
                    1f);
                Vector2 rightDirection = BuildAvoidanceDirection(
                    desiredDirection,
                    perpendicular,
                    -1f);
                float leftClearance = MeasureClearance(leftDirection, radius);
                float rightClearance = MeasureClearance(rightDirection, radius);

                if (Mathf.Max(leftClearance, rightClearance) <= radius * 0.55f)
                {
                    return GetBlockedMovementDirection(desiredDirection);
                }

                if (passengerAhead != null)
                {
                    selectedSide = SelectPassingSide(
                        passengerAhead,
                        desiredDirection);

                    float selectedClearance = selectedSide > 0f
                        ? leftClearance
                        : rightClearance;
                    float alternateClearance = selectedSide > 0f
                        ? rightClearance
                        : leftClearance;
                    if (alternateClearance > selectedClearance + 0.08f)
                    {
                        selectedSide *= -1f;
                    }
                }
                else if (Mathf.Abs(leftClearance - rightClearance) <= 0.05f)
                {
                    selectedSide = GetInstanceID() % 2 == 0 ? 1f : -1f;
                }
                else
                {
                    selectedSide = leftClearance > rightClearance ? 1f : -1f;
                }

                avoidanceSide = selectedSide;
                float sideHoldDuration = headOnApproach
                    ? Mathf.Max(avoidanceSideHoldDuration, 0.8f)
                    : avoidanceSideHoldDuration;
                avoidanceSideUntil = Time.time + sideHoldDuration;
            }

            Vector2 avoidedDirection = BuildAvoidanceDirection(
                desiredDirection,
                perpendicular,
                selectedSide);

            if (MeasureClearance(avoidedDirection, radius) <= radius * 0.35f)
            {
                return GetBlockedMovementDirection(desiredDirection);
            }

            if (!IsStillCloseToBlockingPassenger())
            {
                ResetPassengerBlock();
            }

            avoidanceAdjustmentCount++;
            return avoidedDirection;
        }

        private bool IsStillCloseToBlockingPassenger()
        {
            return blockingPassenger != null &&
                   blockingPassenger.body != null &&
                   Vector2.Distance(body.position, blockingPassenger.body.position) <
                   softOverlapRadius * 1.25f;
        }

        private GeneralPassengerPrototype FindPassengerTooCloseTo(
            Vector2 position,
            float minimumDistance)
        {
            GeneralPassengerPrototype[] passengers = passengerPeers;
            GeneralPassengerPrototype nearest = null;
            float nearestDistance = minimumDistance;
            foreach (GeneralPassengerPrototype passenger in passengers)
            {
                if (passenger == null ||
                    passenger == this ||
                    passenger.body == null ||
                    passenger.bodyCollider == null ||
                    !passenger.bodyCollider.enabled)
                {
                    continue;
                }

                float distance = Vector2.Distance(position, passenger.body.position);
                if (distance < nearestDistance)
                {
                    nearest = passenger;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private GeneralPassengerPrototype FindPassengerDirectlyAhead(
            Vector2 desiredDirection,
            float radius)
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll(
                body.position,
                radius,
                desiredDirection,
                avoidanceLookAhead * 0.85f);
            GeneralPassengerPrototype nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (RaycastHit2D hit in hits)
            {
                GeneralPassengerPrototype passenger = hit.collider == null
                    ? null
                    : hit.collider.GetComponentInParent<GeneralPassengerPrototype>();
                if (passenger == null || passenger == this || hit.distance >= nearestDistance)
                {
                    continue;
                }

                Vector2 offset = passenger.body.position - body.position;
                if (offset.sqrMagnitude <= 0.001f ||
                    Vector2.Dot(offset.normalized, desiredDirection) < 0.55f)
                {
                    continue;
                }

                nearest = passenger;
                nearestDistance = hit.distance;
            }

            return nearest;
        }

        private bool ShouldWaitForPassenger(
            GeneralPassengerPrototype passenger,
            Vector2 desiredDirection)
        {
            if (HasUrgentExitPriority() ||
                HasClearForwardMovementArc(desiredDirection))
            {
                courtesyWaitPassenger = null;
                return false;
            }

            if (passenger == null)
            {
                return false;
            }

            Vector2 otherIntent = passenger.GetMovementIntent();
            if (otherIntent.sqrMagnitude <= 0.01f)
            {
                return false;
            }

            float directionAlignment = Vector2.Dot(
                desiredDirection,
                otherIntent.normalized);
            if (directionAlignment > 0.55f)
            {
                courtesyWaitPassenger = null;
                return false;
            }

            if (directionAlignment < -0.25f)
            {
                Vector2 meetingPoint =
                    (body.position + passenger.body.position) * 0.5f;
                bool narrowCrowd =
                    CountNearbyMovingPassengers(meetingPoint, 1.15f) >= 3;
                bool yieldsFirst = GetInstanceID() > passenger.GetInstanceID();
                return narrowCrowd && yieldsFirst &&
                       ShouldBrieflyWaitForPassenger(passenger, 0.45f, 0.9f);
            }

            if (courtesyWaitPassenger != null && Time.time < courtesyWaitUntil)
            {
                return courtesyWaitPassenger == passenger;
            }

            if (courtesyWaitPassenger != null)
            {
                courtesyWaitPassenger = null;
                courtesyWaitCooldownUntil = Time.time + Random.Range(0.35f, 0.65f);
            }

            if (Time.time < courtesyWaitCooldownUntil)
            {
                return false;
            }

            float ownPriority = GetMovementPriority();
            float otherPriority = passenger.GetMovementPriority();
            bool shouldYield = ownPriority < otherPriority - 0.05f;
            if (Mathf.Abs(ownPriority - otherPriority) <= 0.05f)
            {
                shouldYield = GetInstanceID() > passenger.GetInstanceID();
            }

            if (!shouldYield)
            {
                return false;
            }

            courtesyWaitPassenger = passenger;
            courtesyWaitUntil = Time.time + Mathf.Lerp(0.3f, 0.85f, courtesy);
            RecordDecision("Wait for " + passenger.name);
            return true;
        }

        private bool ShouldBrieflyWaitForPassenger(
            GeneralPassengerPrototype passenger,
            float minimumDuration,
            float maximumDuration)
        {
            if (courtesyWaitPassenger == passenger)
            {
                if (Time.time < courtesyWaitUntil)
                {
                    return true;
                }

                courtesyWaitPassenger = null;
                courtesyWaitCooldownUntil = Time.time + Random.Range(0.65f, 1f);
                return false;
            }

            if (courtesyWaitPassenger != null ||
                Time.time < courtesyWaitCooldownUntil)
            {
                return false;
            }

            courtesyWaitPassenger = passenger;
            courtesyWaitUntil = Time.time + Random.Range(
                minimumDuration,
                maximumDuration);
            return true;
        }

        private int CountNearbyMovingPassengers(Vector2 position, float radius)
        {
            int count = 0;
            if (passengerPeers == null)
            {
                return count;
            }

            foreach (GeneralPassengerPrototype passenger in passengerPeers)
            {
                if (passenger == null ||
                    passenger.body == null ||
                    passenger.bodyCollider == null ||
                    !passenger.bodyCollider.enabled ||
                    !passenger.IsActivelyMoving())
                {
                    continue;
                }

                if (Vector2.Distance(position, passenger.body.position) <= radius)
                {
                    count++;
                }
            }

            return count;
        }

        private float GetMovementPriority()
        {
            if (state == PassengerState.Exiting ||
                state == PassengerState.PreparingToExit ||
                state == PassengerState.WaitingAtExitDoor)
            {
                return 4.5f;
            }

            if (state == PassengerState.Boarding)
            {
                return 2.6f;
            }

            if (IsSeekingSeat)
            {
                return 2.1f;
            }

            return 1f + (1f - courtesy) * 0.45f;
        }

        private float SelectPassingSide(
            GeneralPassengerPrototype passenger,
            Vector2 desiredDirection)
        {
            if (IsHeadOnApproach(passenger, desiredDirection))
            {
                return -1f;
            }

            return GetInstanceID() % 2 == 0 ? 1f : -1f;
        }

        private static bool IsHeadOnApproach(
            GeneralPassengerPrototype passenger,
            Vector2 desiredDirection)
        {
            if (passenger == null || desiredDirection.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            Vector2 otherIntent = passenger.GetMovementIntent();
            return otherIntent.sqrMagnitude > 0.001f &&
                   Vector2.Dot(desiredDirection, otherIntent.normalized) < -0.25f;
        }

        private bool IsActivelyMoving()
        {
            return state == PassengerState.Boarding ||
                   state == PassengerState.MovingToActivity ||
                   state == PassengerState.PreparingToExit ||
                   state == PassengerState.Exiting;
        }

        private bool HasUrgentExitPriority()
        {
            return state == PassengerState.WaitingToStand ||
                   state == PassengerState.PreparingToExit ||
                   state == PassengerState.WaitingAtExitDoor ||
                   state == PassengerState.Exiting;
        }

        private bool HasClearForwardMovementArc(Vector2 desiredDirection)
        {
            if (desiredDirection.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            Vector2 forward = desiredDirection.normalized;
            float scanRadius = Mathf.Max(
                comfortableBodySeparation * 1.4f,
                avoidanceLookAhead * 0.75f);
            CollectNearbyPassengers(
                body.position,
                scanRadius,
                nearbyPassengerBuffer);
            foreach (GeneralPassengerPrototype passenger in nearbyPassengerBuffer)
            {
                if (passenger == null || passenger.body == null)
                {
                    continue;
                }

                Vector2 offset = passenger.body.position - body.position;
                if (offset.sqrMagnitude > 0.001f &&
                    Vector2.Dot(forward, offset.normalized) >= 0f)
                {
                    return false;
                }
            }

            if (playerPeer != null)
            {
                Vector2 playerOffset =
                    (Vector2)playerPeer.transform.position - body.position;
                if (playerOffset.sqrMagnitude <= scanRadius * scanRadius &&
                    playerOffset.sqrMagnitude > 0.001f &&
                    Vector2.Dot(forward, playerOffset.normalized) >= 0f)
                {
                    return false;
                }
            }

            Vector2 forwardCheckPosition =
                body.position + forward * Mathf.Min(0.55f, scanRadius);
            GridNavigation2D navigation = FindCurrentNavigationArea();
            return (navigation == null ||
                    navigation.IsWorldWalkable(forwardCheckPosition)) &&
                   !IsBlockedByNavigationObstacle(forwardCheckPosition);
        }

        private void RememberMovementIntent(Vector2 direction)
        {
            movementIntent = direction;
            movementIntentUntil = Time.time + movementIntentMemory;
        }

        private Vector2 GetMovementIntent()
        {
            return Time.time <= movementIntentUntil
                ? movementIntent
                : Vector2.zero;
        }

        private void AssignMovementPace()
        {
            float paceRoll = Random.value;
            if (paceRoll < leisurelyPassengerChance)
            {
                personalPaceMultiplier = Random.Range(0.45f, 0.62f);
            }
            else if (paceRoll > 1f - hurriedPassengerChance)
            {
                personalPaceMultiplier = Random.Range(0.85f, 1f);
            }
            else
            {
                personalPaceMultiplier = Random.Range(0.63f, 0.82f);
            }

            float exitRoll = Random.value;
            if (exitRoll < 0.2f)
            {
                exitPaceMultiplier = Random.Range(0.72f, 0.84f);
            }
            else if (exitRoll > 0.75f)
            {
                exitPaceMultiplier = Random.Range(0.95f, 1.05f);
            }
            else
            {
                exitPaceMultiplier = Random.Range(0.84f, 0.96f);
            }
        }

        private float GetCurrentMoveSpeed()
        {
            float baseMoveSpeed = Mathf.Min(moveSpeed, 1.8f);
            float speed = baseMoveSpeed * personalPaceMultiplier;
            if (initialPlacement && boardingOrder <= 7)
            {
                speed = Mathf.Max(speed, baseMoveSpeed * 0.82f);
            }

            if (IsSeekingSeat)
            {
                speed *= 1.05f;
            }

            if (state == PassengerState.PreparingToExit ||
                state == PassengerState.Exiting)
            {
                speed *= exitPaceMultiplier;
            }

            return Mathf.Clamp(speed, 0.75f, baseMoveSpeed * 1.1f);
        }

        private Vector2 GetBlockedMovementDirection(Vector2 desiredDirection)
        {
            blockedPushCount++;
            RegisterPassengerBlock();
            if (HasUrgentExitPriority())
            {
                if (ShouldRequestPassageFromBlockingPassenger(desiredDirection))
                {
                    blockingPassenger.ReportObstruction(this, desiredDirection);
                    BroadcastPassIntent(desiredDirection);
                }

                float urgentBlockedDuration = Time.time - blockedByPassengerSince;
                if (!hasBlockedDetourTarget &&
                    urgentBlockedDuration >= 0.55f &&
                    TryBeginUrgentExitDetour(desiredDirection))
                {
                    return Vector2.zero;
                }

                bool tightlyOverlapping = blockingPassenger != null &&
                                          blockingPassenger.body != null &&
                                          Vector2.Distance(
                                              body.position,
                                              blockingPassenger.body.position) <
                                          softOverlapRadius * 0.55f;
                if (tightlyOverlapping)
                {
                    Vector2 awayFromPassenger =
                        body.position - blockingPassenger.body.position;
                    if (awayFromPassenger.sqrMagnitude <= 0.001f)
                    {
                        awayFromPassenger = GetInstanceID() <
                                            blockingPassenger.GetInstanceID()
                            ? Vector2.up
                            : Vector2.down;
                    }

                    return awayFromPassenger.normalized * 0.35f;
                }

                return Vector2.zero;
            }

            float blockedDuration = Time.time - blockedByPassengerSince;
            bool canUseCrowdSqueeze =
                state == PassengerState.Boarding &&
                IsExtremelyCrowded(body.position);

            if (blockedDuration < blockedWaitDuration)
            {
                return Vector2.zero;
            }

            if (!blockedDetourAttempted)
            {
                TryBeginBlockedDetour(desiredDirection);
                if (hasBlockedDetourTarget)
                {
                    return Vector2.zero;
                }
            }

            if (blockedDuration >= Mathf.Max(passIntentDelay, 0.55f) &&
                ShouldRequestPassageFromBlockingPassenger(desiredDirection))
            {
                blockingPassenger.ReportObstruction(this, desiredDirection);
                BroadcastPassIntent(desiredDirection);
            }

            if (blockingPassenger != null &&
                IsHeadOnApproach(blockingPassenger, desiredDirection) &&
                canUseCrowdSqueeze)
            {
                Vector2 perpendicular = new Vector2(
                    -desiredDirection.y,
                    desiredDirection.x);
                float side = Mathf.Approximately(avoidanceSide, 0f)
                    ? -1f
                    : avoidanceSide;
                squeezeStepCount++;
                Vector2 passingDirection =
                    (desiredDirection * 0.42f +
                     perpendicular * side * 0.72f).normalized;
                BeginSoftSqueeze(passingDirection);
                return passingDirection * 0.7f;
            }

            if (Time.time < blockedSqueezeUntil)
            {
                if (!canUseCrowdSqueeze)
                {
                    return Vector2.zero;
                }

                Vector2 perpendicular = new Vector2(-desiredDirection.y, desiredDirection.x);
                float side = Mathf.Approximately(avoidanceSide, 0f)
                    ? (GetInstanceID() % 2 == 0 ? 1f : -1f)
                    : avoidanceSide;
                Vector2 squeezedDirection = (desiredDirection * 0.45f +
                                              perpendicular * side * 0.55f).normalized;
                squeezeStepCount++;
                float willingness = squeezeThroughPassengers ? 1f : 0.65f;
                BeginSoftSqueeze(squeezedDirection);
                return squeezedDirection * squeezeSpeedMultiplier * willingness;
            }

            return Vector2.zero;
        }

        private bool ShouldRequestPassageFromBlockingPassenger(
            Vector2 desiredDirection)
        {
            return blockingPassenger != null &&
                   blockingPassenger.body != null &&
                   blockingPassenger.CanYieldForStationaryTraffic(this) &&
                   blockingPassenger.IsDirectlyBlockingPassingRoute(
                       body.position,
                       desiredDirection) &&
                   blockingPassenger.ShouldYieldForPassingRoute(
                       this,
                       desiredDirection);
        }

        private bool TryBeginUrgentExitDetour(Vector2 desiredDirection)
        {
            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null || desiredDirection.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            Vector2 perpendicular = new Vector2(-desiredDirection.y, desiredDirection.x);
            float blockerSide = 0f;
            if (blockingPassenger != null && blockingPassenger.body != null)
            {
                blockerSide = Mathf.Sign(Vector2.Dot(
                    blockingPassenger.body.position - body.position,
                    perpendicular));
            }

            float preferredSide = Mathf.Approximately(blockerSide, 0f)
                ? (Mathf.Approximately(avoidanceSide, 0f) ? -1f : avoidanceSide)
                : -blockerSide;
            Vector2[] candidates =
            {
                body.position + perpendicular * preferredSide * 0.9f - desiredDirection * 0.3f,
                body.position - perpendicular * preferredSide * 0.9f - desiredDirection * 0.3f,
                body.position + perpendicular * preferredSide * 1.2f - desiredDirection * 0.5f,
                body.position - perpendicular * preferredSide * 1.2f - desiredDirection * 0.5f,
                body.position + perpendicular * preferredSide * 0.85f + desiredDirection * 0.35f,
                body.position - perpendicular * preferredSide * 0.85f + desiredDirection * 0.35f
            };

            foreach (Vector2 candidate in candidates)
            {
                if (!navigation.IsWorldWalkable(candidate) ||
                    !IsEmergencyYieldPositionAvailable(candidate))
                {
                    continue;
                }

                blockedDetourTarget = candidate;
                hasBlockedDetourTarget = true;
                blockedDetourAttempted = true;
                blockedGiveUpAt = Time.time + 1.6f;
                avoidanceSide = Mathf.Sign(Vector2.Dot(
                    candidate - body.position,
                    perpendicular));
                blockedDetourCount++;
                ClearPath();
                RecordDecision("Exit blocked -> Go around");
                return true;
            }

            return false;
        }

        private void RegisterPassengerBlock()
        {
            if (blockedByPassengerSince >= 0f)
            {
                return;
            }

            blockedByPassengerSince = Time.time;
            blockedSqueezeUntil = Time.time + Random.Range(
                blockedSqueezeMinimumDuration,
                Mathf.Max(blockedSqueezeMinimumDuration, blockedSqueezeMaximumDuration));
            if (!blockedDetourAttempted)
            {
                blockedGiveUpAt = blockedSqueezeUntil + blockedActivityTimeout;
            }
        }

        private void TryBeginBlockedDetour(Vector2 desiredDirection)
        {
            blockedDetourAttempted = true;
            Vector2 perpendicular = new Vector2(-desiredDirection.y, desiredDirection.x);
            float previousSide = Mathf.Approximately(avoidanceSide, 0f)
                ? (GetInstanceID() % 2 == 0 ? 1f : -1f)
                : avoidanceSide;
            float oppositeSide = -previousSide;
            Vector2[] candidates =
            {
                body.position + perpendicular * oppositeSide * 0.95f - desiredDirection * 0.18f,
                body.position + perpendicular * previousSide * 0.95f - desiredDirection * 0.18f,
                body.position - desiredDirection * 0.72f + perpendicular * oppositeSide * 0.45f
            };

            GridNavigation2D navigation = FindCurrentNavigationArea();
            foreach (Vector2 candidate in candidates)
            {
                if (navigation == null ||
                    !navigation.IsWorldWalkable(candidate) ||
                    !IsCourtesyPositionAvailable(candidate))
                {
                    continue;
                }

                blockedDetourTarget = candidate;
                hasBlockedDetourTarget = true;
                blockedGiveUpAt = Time.time + blockedActivityTimeout;
                blockedDetourCount++;
                ClearPath();
                RecordDecision("Blocked -> Opposite detour");
                return;
            }

            blockedGiveUpAt = Time.time + 0.35f;
        }

        private bool ShouldAbandonBlockedActivity()
        {
            return state == PassengerState.MovingToActivity &&
                   blockedDetourAttempted &&
                   Time.time >= blockedGiveUpAt;
        }

        private void AbandonBlockedActivity()
        {
            bool wasInitialPlacement = initialPlacement;
            abandonedActivityCount++;
            RecordDecision("Blocked -> Reconsider");
            abandonedActivityTarget = activityTarget;
            avoidAbandonedTargetUntil = Time.time + Random.Range(4f, 6f);
            ReleaseCurrentActivity();
            hasBlockedDetourTarget = false;
            blockedDetourAttempted = false;
            ClearPath();
            ResetPassengerBlock();
            ResetActivityProgressTracking();

            if (wasInitialPlacement && preferredStandingRetryCount < 3)
            {
                preferredStandingRetryCount++;
                initialPlacement = true;
                if (TryChooseInitialStandingBehavior())
                {
                    RecordDecision(
                        "Blocked -> Retry preferred zone " +
                        preferredStandingRetryCount);
                    state = PassengerState.MovingToActivity;
                    UpdateLabel();
                    return;
                }
            }

            initialPlacement = false;
            if (CanSettleAtCurrentPosition())
            {
                behavior = PassengerBehavior.AisleStanding;
                activityTarget = body.position;
                RecordDecision("Blocked -> Stand before crowd");
                ScheduleNextDecision();
                SetState(PassengerState.Observing, behavior);
                return;
            }

            if (TryChooseNearbyStandingPosition())
            {
                RecordDecision("Blocked -> Stand nearby");
                SetState(PassengerState.MovingToActivity, PassengerBehavior.AisleStanding);
                return;
            }

            behavior = PassengerBehavior.AisleStanding;
            activityTarget = body.position;
            ScheduleNextDecision();
            SetState(PassengerState.Observing, behavior);
        }

        private bool CanSettleAtCurrentPosition()
        {
            if (IsInsideDirectBoardingLane(body.position) ||
                IsBlockedByNavigationObstacle(body.position))
            {
                return false;
            }

            CollectNearbyPassengers(
                body.position,
                comfortableBodySeparation,
                environmentQueryBuffer);
            foreach (GeneralPassengerPrototype passenger in environmentQueryBuffer)
            {
                if (passenger == null ||
                    passenger.body == null ||
                    passenger.bodyCollider == null ||
                    !passenger.bodyCollider.enabled)
                {
                    continue;
                }

                return false;
            }

            return playerPeer == null ||
                   Vector2.Distance(
                       body.position,
                       playerPeer.transform.position) >=
                   comfortableBodySeparation;
        }

        private bool TryChooseNearbyStandingPosition()
        {
            GridNavigation2D navigation = FindCurrentNavigationArea();
            if (navigation == null)
            {
                return false;
            }

            Vector2 selected = body.position;
            float bestScore = float.MaxValue;
            bool found = false;
            for (int attempt = 0; attempt < 28; attempt++)
            {
                Vector2 candidate = GetRandomNearbyStandingPosition(navigation, 1.8f);
                if (!navigation.IsWorldWalkable(candidate) ||
                    !IsStandingPositionAvailable(candidate))
                {
                    continue;
                }

                float score = CalculateCrowdScore(candidate) * 3f +
                              Vector2.Distance(body.position, candidate) * 1.4f;
                if (score < bestScore)
                {
                    bestScore = score;
                    selected = candidate;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            behavior = PassengerBehavior.AisleStanding;
            activityTarget = selected;
            return true;
        }

        private void ResetPassengerBlock()
        {
            blockedByPassengerSince = -1f;
            blockingPassenger = null;
            if (!hasBlockedDetourTarget)
            {
                blockedSqueezeUntil = 0f;
                blockedGiveUpAt = 0f;
                blockedDetourAttempted = false;
            }
        }

        private Vector2 BuildAvoidanceDirection(
            Vector2 desiredDirection,
            Vector2 perpendicular,
            float side)
        {
            return (desiredDirection +
                    perpendicular * side * avoidanceLateralStrength).normalized;
        }

        private bool HasActorAhead(Vector2 direction, float radius)
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll(
                body.position,
                radius,
                direction,
                avoidanceLookAhead);

            foreach (RaycastHit2D hit in hits)
            {
                if (IsAvoidableActor(hit.collider))
                {
                    return true;
                }
            }

            return false;
        }

        private float MeasureClearance(Vector2 direction, float radius)
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll(
                body.position,
                radius,
                direction,
                avoidanceLookAhead);
            float nearestDistance = avoidanceLookAhead;

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null ||
                    hit.collider == bodyCollider ||
                    hit.collider.isTrigger)
                {
                    continue;
                }

                nearestDistance = Mathf.Min(nearestDistance, hit.distance);
            }

            return nearestDistance;
        }

        private bool IsAvoidableActor(Collider2D candidate)
        {
            if (candidate == null ||
                candidate == bodyCollider ||
                candidate.isTrigger)
            {
                return false;
            }

            GeneralPassengerPrototype passenger =
                candidate.GetComponentInParent<GeneralPassengerPrototype>();
            if (passenger != null)
            {
                return passenger != this;
            }

            return candidate.GetComponentInParent<PlayerBoardingCyclePrototype>() != null;
        }

        private bool IsInsideExitWaitingArea()
        {
            return Vector2.Distance(body.position, exitWaitingTarget) <=
                   exitWaitingAcceptanceRadius;
        }

        private void MaintainPersonalSpaceNearExit()
        {
            Vector2 toAssignedPosition = exitWaitingTarget - body.position;
            if (toAssignedPosition.sqrMagnitude <= 0.01f)
            {
                RememberMovementIntent(Vector2.zero);
                return;
            }

            Vector2 correction = toAssignedPosition.normalized;
            Vector2 separation = CalculateSeparationVector(
                Mathf.Min(waitingPersonalSpaceRadius, minimumBodySeparation * 0.85f));
            if (separation.sqrMagnitude > 0.01f)
            {
                correction =
                    (correction * 0.82f + separation.normalized * 0.18f).normalized;
            }

            Vector2 nextPosition = body.position +
                                   Vector2.ClampMagnitude(correction, 1f) *
                                   waitingSeparationSpeed *
                                   Time.fixedDeltaTime;
            if (!IsBlockedByNavigationObstacle(nextPosition))
            {
                RememberMovementIntent(correction);
                body.MovePosition(nextPosition);
            }
        }

        private Vector2 CalculateSeparationVector(float radius)
        {
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(body.position, radius);
            var actors = new HashSet<int>();
            Vector2 separation = Vector2.zero;

            foreach (Collider2D overlap in overlaps)
            {
                Transform actor = GetAvoidableActor(overlap);
                if (actor == null || !actors.Add(actor.GetInstanceID()))
                {
                    continue;
                }

                Vector2 away = body.position - (Vector2)actor.position;
                float distance = away.magnitude;
                if (distance <= 0.001f)
                {
                    float side = GetInstanceID() < actor.GetInstanceID() ? -1f : 1f;
                    away = Vector2.right * side;
                    distance = 0.001f;
                }

                float strength = Mathf.Clamp01((radius - distance) / radius);
                separation += away.normalized * strength;
            }

            return separation;
        }

        private Transform GetAvoidableActor(Collider2D candidate)
        {
            if (candidate == null ||
                candidate == bodyCollider ||
                candidate.isTrigger)
            {
                return null;
            }

            GeneralPassengerPrototype passenger =
                candidate.GetComponentInParent<GeneralPassengerPrototype>();
            if (passenger != null && passenger != this)
            {
                return passenger.transform;
            }

            PlayerBoardingCyclePrototype player =
                candidate.GetComponentInParent<PlayerBoardingCyclePrototype>();
            return player != null ? player.transform : null;
        }

        private void IgnoreHardPassengerCollisions()
        {
            GeneralPassengerPrototype[] passengers =
                FindObjectsByType<GeneralPassengerPrototype>(FindObjectsSortMode.None);

            foreach (GeneralPassengerPrototype passenger in passengers)
            {
                if (passenger == null || passenger == this)
                {
                    continue;
                }

                Collider2D[] otherColliders = passenger.GetComponents<Collider2D>();
                foreach (Collider2D otherCollider in otherColliders)
                {
                    if (otherCollider != null)
                    {
                        Physics2D.IgnoreCollision(bodyCollider, otherCollider, true);
                    }
                }
            }
        }

        private void StopResidualMotion()
        {
            if (body == null || body.bodyType != RigidbodyType2D.Dynamic)
            {
                return;
            }

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        private bool IsBlockedByNavigationObstacle(Vector2 position)
        {
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(
                position,
                GetAvoidanceRadius());

            foreach (Collider2D overlap in overlaps)
            {
                if (overlap.GetComponentInParent<NavigationObstacle>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private float GetAvoidanceRadius()
        {
            if (bodyCollider == null)
            {
                return 0.2f;
            }

            Vector2 extents = bodyCollider.bounds.extents;
            return Mathf.Max(0.12f, Mathf.Min(extents.x, extents.y) * 0.65f);
        }

        private float GetDesiredBodySeparation(Vector2 position)
        {
            float activeMinimumSeparation =
                GetActiveMinimumBodySeparation();
            return IsExtremelyCrowded(position)
                ? activeMinimumSeparation
                : Mathf.Max(activeMinimumSeparation, comfortableBodySeparation);
        }

        private float GetActiveMinimumBodySeparation()
        {
            float compressedMultiplier = Mathf.Lerp(
                squeezeBodySeparationMultiplier,
                0.56f,
                jellyCompression);
            return Time.time < softContactUntil
                ? minimumBodySeparation * compressedMultiplier
                : minimumBodySeparation;
        }

        private void BeginSoftSqueeze(Vector2 movementDirection)
        {
            if (movementDirection.sqrMagnitude <= 0.001f)
            {
                return;
            }

            softContactUntil = Mathf.Max(softContactUntil, Time.time + 0.42f);
            bodySqueezeUntil = Mathf.Max(bodySqueezeUntil, Time.time + 0.46f);
            bodySqueezeAngle =
                Mathf.Atan2(movementDirection.y, movementDirection.x) *
                Mathf.Rad2Deg;
        }

        private void UpdateBodySqueezeRotation()
        {
            if (body == null)
            {
                return;
            }

            bool squeezing = Time.time < bodySqueezeUntil;
            float targetCompression = squeezing ? 1f : 0f;
            jellyCompression = Mathf.MoveTowards(
                jellyCompression,
                targetCompression,
                (squeezing ? 5.5f : 3.5f) * Time.fixedDeltaTime);
            Vector3 compressedScale = new Vector3(
                baseBodyScale.x * 1.12f,
                baseBodyScale.y * 0.72f,
                baseBodyScale.z);
            transform.localScale = Vector3.Lerp(
                baseBodyScale,
                compressedScale,
                jellyCompression);

            float targetAngle = 0f;
            if (squeezing)
            {
                targetAngle = bodySqueezeAngle;
            }
            else if (state == PassengerState.Observing &&
                     behavior == PassengerBehavior.Leaning &&
                     viewDirection.sqrMagnitude > 0.001f)
            {
                targetAngle =
                    Mathf.Atan2(viewDirection.y, viewDirection.x) *
                    Mathf.Rad2Deg +
                    90f;
            }

            float nextAngle = Mathf.MoveTowardsAngle(
                body.rotation,
                targetAngle,
                bodyTurnSpeed * Time.fixedDeltaTime);
            body.MoveRotation(Mathf.DeltaAngle(0f, nextAngle));
        }

        private bool IsExtremelyCrowded(Vector2 position)
        {
            return CalculateCrowdScore(position) >= extremeCrowdScore;
        }

        private float GetBodyRadius()
        {
            if (bodyCollider == null)
            {
                return 0.28f;
            }

            Vector2 extents = bodyCollider.bounds.extents;
            return Mathf.Max(0.2f, Mathf.Max(extents.x, extents.y));
        }

        private void ClearPath()
        {
            path.Clear();
            pathIndex = 0;
            hasPathTarget = false;
            pathBlockedByPassengers = false;
        }

        private void ScheduleNextDecision()
        {
            nextDecisionTime = Time.time + Random.Range(
                minimumDecisionInterval,
                Mathf.Max(minimumDecisionInterval, maximumDecisionInterval));
        }

        private void SetPosition(Vector3 position)
        {
            body.position = position;
        }

        private void SetState(PassengerState nextState, PassengerBehavior nextBehavior)
        {
            if (nextState != PassengerState.Boarding &&
                boardingCollisionBypassActive)
            {
                SetBoardingCollisionBypass(false);
            }

            state = nextState;
            behavior = nextBehavior;
            if (nextState == PassengerState.Observing &&
                nextBehavior != PassengerBehavior.Seated &&
                nextBehavior != PassengerBehavior.Exiting)
            {
                ScheduleSettledSeatCheck();
            }

            UpdateLabel();
        }

        private void RecordDecision(string decision)
        {
            decisionHistory.Add(decision);
            if (decisionHistory.Count > 20)
            {
                decisionHistory.RemoveAt(0);
            }
        }

        private void UpdateDebugAppearance()
        {
            if (bodyRenderer != null && bodyColorProperties != null)
            {
                Color stateColor = GetDebugStateColor();
                bodyRenderer.GetPropertyBlock(bodyColorProperties);
                bodyColorProperties.SetColor("_BaseColor", stateColor);
                bodyColorProperties.SetColor("_Color", stateColor);
                bodyRenderer.SetPropertyBlock(bodyColorProperties);
            }

            if (targetSeatNumberLabel != null)
            {
                int seatNumber = behavior == PassengerBehavior.Seated
                    ? GetReservedSeatNumber()
                    : 0;
                targetSeatNumberLabel.text =
                    seatNumber > 0 ? seatNumber.ToString() : string.Empty;
            }
        }

        private Color GetDebugStateColor()
        {
            if (state == PassengerState.Yielding)
            {
                return new Color(1f, 0.84f, 0.12f);
            }

            switch (behavior)
            {
                case PassengerBehavior.AisleStanding:
                case PassengerBehavior.DoorStanding:
                    return new Color(0.18f, 0.52f, 1f);
                case PassengerBehavior.Seated:
                    return new Color(0.95f, 0.2f, 0.18f);
                case PassengerBehavior.Leaning:
                    return new Color(0.18f, 0.8f, 0.34f);
                default:
                    return new Color(0.95f, 0.55f, 0.18f);
            }
        }

        private int GetReservedSeatNumber()
        {
            if (reservedSeat != null && reservedSittingPoint != null)
            {
                return reservedSeat.GetDebugSeatNumber(reservedSittingPoint);
            }

            return plannedSeat != null && plannedSittingPoint != null
                ? plannedSeat.GetDebugSeatNumber(plannedSittingPoint)
                : 0;
        }

        private void UpdateLabel()
        {
            UpdateDebugAppearance();
            if (stateLabel == null)
            {
                return;
            }

            string action;
            if (state == PassengerState.WaitingToStand)
            {
                action = "WaitingToStand";
            }
            else if (state == PassengerState.Yielding)
            {
                action = "Yielding";
            }
            else if (behavior == PassengerBehavior.AisleStanding ||
                     behavior == PassengerBehavior.DoorStanding)
            {
                action = "Standing";
            }
            else
            {
                action = behavior == PassengerBehavior.None
                    ? CurrentState
                    : behavior.ToString();
            }

            int seatNumber = GetReservedSeatNumber();
            if (behavior == PassengerBehavior.Seated && seatNumber > 0)
            {
                action += " -> Seat " + seatNumber;
            }

            stateLabel.text = gameObject.name + "\n" + action;
        }
    }
}

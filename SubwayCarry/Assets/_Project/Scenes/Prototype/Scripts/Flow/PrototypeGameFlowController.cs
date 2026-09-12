using System;
using System.Collections;
using SubwayCarry.AI;
using SubwayCarry.Core.Contracts;
using SubwayCarry.Prototype.Delivery;
using SubwayCarry.Prototype.Delivery.Mocks;
using SubwayCarry.Prototype.Gameplay;
using SubwayCarry.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using RuntimePlayerBalance = SubwayCarry.Gameplay.PlayerBalance;

namespace SubwayCarry.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeGameFlowController : MonoBehaviour
    {
        private static readonly string[] TutorialPages =
        {
            "방학 동안 다음 학기 학비를 마련해야 한다.\n가천대학교 배달 앱의 첫 배송 튜토리얼.",
            "WASD로 이동하고 마우스 방향을 바라본다.\n가까운 시설은 E키로 상호작용한다.",
            "배송을 선택한 뒤 긴 개찰구에서 E키로 교통카드를 찍는다.\n카드를 찍기 전에는 개찰구 통로를 지나갈 수 없다.",
            "개찰구 뒤의 2열 에스컬레이터와 전용 통로를 지나 승강장으로 간다.\n열차가 들어와 정차하고 문이 열리면 직접 탑승한다.",
            "열차가 흔들리면 화면의 중심잡기 안내를 따른다.\n이때 WASD는 이동 대신 반대 방향 버티기나 중심 조절에 사용된다."
        };

        [Header("Player")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerPosture playerPosture;
        [SerializeField] private PackageDurability packageDurability;
        [SerializeField] private PrototypeCameraFollow cameraFollow;

        private PrototypePlayerSpriteAnimator playerSpriteAnimator;

        [Header("Services")]
        [SerializeField] private DeliveryService deliveryService;
        [SerializeField] private EconomyService economyService;
        [SerializeField] private MockTransitProgressProvider transitProgress;

        [Header("Flow Points")]
        [SerializeField] private Transform hubSpawn;
        [SerializeField] private Transform departureConcourseReturnSpawn;
        [SerializeField] private Transform departureEscalatorSpawn;
        [SerializeField] private Transform departureEscalatorReturnSpawn;
        [SerializeField] private Transform departurePlatformSpawn;
        [SerializeField] private Transform travelTrainSpawn;
        [SerializeField] private Transform destinationTrainSpawn;
        [SerializeField] private Transform destinationPlatformReturnSpawn;
        [SerializeField] private Transform destinationEscalatorSpawn;
        [SerializeField] private Transform destinationEscalatorReturnSpawn;
        [SerializeField] private Transform destinationConcourseSpawn;
        [SerializeField] private TrainDoorController[] departureDoors;
        [SerializeField] private TrainDoorController[] destinationDoors;
        [SerializeField] private GameObject departureGateBlocker;
        [SerializeField] private GameObject destinationGateBlocker;
        [SerializeField] private PrototypeTrainArrival departureTrainArrival;
        [SerializeField, Min(0f)] private float platformTrainWaitDuration = 1.5f;
        [SerializeField, Min(0f)] private float boardingOpenGraceDuration = 6f;
        [SerializeField, Min(1f)] private float rideDuration = 8f;
        [SerializeField] private bool enableBalanceChallenges = true;

        private PrototypeFlowStage stage;
        private PrototypeInteractable nearbyInteraction;
        private DeliverySettlementSnapshot lastSettlement;
        private bool hasSettlement;
        private bool mapOpen;
        private bool destinationTrainExited;
        private bool departureTrainReady;
        private bool departureTrainArrivalStarted;
        private bool destinationGateOpen;
        private bool transitioning;
        private string pendingDeliveryId;
        private string toast;
        private float toastUntil;
        private float boardingGraceRemaining;
        private float rideRemaining;
        private float fadeAlpha;
        private int tutorialPage;
        private bool midRideBalanceEmitted;

        private RuntimePlayerBalance playerBalance;
        private PrototypeTrainMotionProvider trainMotionProvider;

        private Font uiFont;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;
        private GUIStyle hudTitleStyle;
        private GUIStyle hudSmallStyle;

        public PrototypeFlowStage CurrentStage => stage;

        public void SetBalanceChallengesEnabled(bool enabled)
        {
            enableBalanceChallenges = enabled;
            if (!enabled)
            {
                playerBalance?.CancelCurrentChallenge();
            }
        }

        public void Configure(
            PlayerController controller,
            PlayerPosture posture,
            PackageDurability durability,
            PrototypeCameraFollow followCamera,
            DeliveryService deliveries,
            EconomyService economy,
            MockTransitProgressProvider transit,
            Transform hub,
            Transform departureConcourseReturn,
            Transform departureEscalator,
            Transform departureEscalatorReturn,
            Transform departurePlatform,
            Transform travelTrain,
            Transform destinationTrain,
            Transform destinationPlatformReturn,
            Transform destinationEscalator,
            Transform destinationEscalatorReturn,
            Transform destinationConcourse,
            TrainDoorController[] controlledDepartureDoors,
            TrainDoorController[] controlledDestinationDoors,
            GameObject controlledDepartureGateBlocker,
            GameObject controlledDestinationGateBlocker,
            PrototypeTrainArrival controlledDepartureTrainArrival)
        {
            playerController = controller;
            playerPosture = posture;
            packageDurability = durability;
            playerSpriteAnimator = controller != null
                ? controller.GetComponent<PrototypePlayerSpriteAnimator>()
                : null;
            playerSpriteAnimator?.SetPackageDurabilityProvider(durability);
            cameraFollow = followCamera;
            deliveryService = deliveries;
            economyService = economy;
            transitProgress = transit;
            hubSpawn = hub;
            departureConcourseReturnSpawn = departureConcourseReturn;
            departureEscalatorSpawn = departureEscalator;
            departureEscalatorReturnSpawn = departureEscalatorReturn;
            departurePlatformSpawn = departurePlatform;
            travelTrainSpawn = travelTrain;
            destinationTrainSpawn = destinationTrain;
            destinationPlatformReturnSpawn = destinationPlatformReturn;
            destinationEscalatorSpawn = destinationEscalator;
            destinationEscalatorReturnSpawn = destinationEscalatorReturn;
            destinationConcourseSpawn = destinationConcourse;
            departureDoors = controlledDepartureDoors;
            destinationDoors = controlledDestinationDoors;
            departureGateBlocker = controlledDepartureGateBlocker;
            destinationGateBlocker = controlledDestinationGateBlocker;
            departureTrainArrival = controlledDepartureTrainArrival;
        }

        public bool CanPerform(PrototypeInteractionAction action)
        {
            if (transitioning || mapOpen || stage == PrototypeFlowStage.Tutorial ||
                stage == PrototypeFlowStage.Settlement)
            {
                return false;
            }

            switch (action)
            {
                case PrototypeInteractionAction.OpenDeliveryMap:
                    return stage == PrototypeFlowStage.GachonHub ||
                           stage == PrototypeFlowStage.DeliverySelected;

                case PrototypeInteractionAction.TapDepartureGate:
                    return stage == PrototypeFlowStage.DeliverySelected &&
                           !string.IsNullOrEmpty(pendingDeliveryId);

                case PrototypeInteractionAction.CompleteAtDestinationGate:
                    return stage == PrototypeFlowStage.DestinationConcourse &&
                           destinationTrainExited &&
                           !destinationGateOpen;

                default:
                    return false;
            }
        }

        public bool TryPerform(PrototypeInteractionAction action)
        {
            if (!CanPerform(action))
            {
                ShowToast("지금은 사용할 수 없다.");
                return false;
            }

            switch (action)
            {
                case PrototypeInteractionAction.OpenDeliveryMap:
                    mapOpen = true;
                    RefreshPlayerControl();
                    return true;

                case PrototypeInteractionAction.TapDepartureGate:
                    return BeginSelectedDelivery();

                case PrototypeInteractionAction.CompleteAtDestinationGate:
                    OpenDestinationGate();
                    return true;

                default:
                    return false;
            }
        }

        public void SetNearbyInteraction(
            PrototypeInteractable interactable,
            bool isNearby)
        {
            if (isNearby)
            {
                nearbyInteraction = interactable;
            }
            else if (nearbyInteraction == interactable)
            {
                nearbyInteraction = null;
            }
        }

        public void NotifyEnteredDepartureEscalator()
        {
            if (stage != PrototypeFlowStage.DepartureConcourse || transitioning)
            {
                return;
            }

            stage = PrototypeFlowStage.DepartureEscalator;
            StartCoroutine(FadeTeleport(
                departureEscalatorSpawn,
                null));
        }

        public void NotifyReturnedToDepartureConcourse()
        {
            if (stage != PrototypeFlowStage.DepartureEscalator || transitioning)
            {
                return;
            }

            stage = PrototypeFlowStage.DepartureConcourse;
            SetBlockerActive(departureGateBlocker, true);
            StartCoroutine(FadeTeleport(
                departureConcourseReturnSpawn,
                () => ShowToast(
                    "가천대역 개찰구 안쪽",
                    2f)));
        }

        public void NotifyEnteredDeparturePlatform()
        {
            if (stage != PrototypeFlowStage.DepartureEscalator || transitioning)
            {
                return;
            }

            stage = PrototypeFlowStage.DeparturePlatform;
            StartCoroutine(FadeTeleport(
                departurePlatformSpawn,
                HandleEnteredDeparturePlatform));
        }

        public void NotifyReturnedToDepartureEscalator()
        {
            if (stage != PrototypeFlowStage.DeparturePlatform || transitioning)
            {
                return;
            }

            stage = PrototypeFlowStage.DepartureEscalator;
            StartCoroutine(FadeTeleport(
                departureEscalatorReturnSpawn,
                null));
        }

        public void NotifyBoardedTrain()
        {
            if (stage != PrototypeFlowStage.DeparturePlatform ||
                transitioning ||
                !departureTrainReady)
            {
                return;
            }

            stage = PrototypeFlowStage.TrainBoarding;
            StartCoroutine(DepartAfterBoarding());
        }

        public void NotifyLeftTrainAtDestination()
        {
            if (stage != PrototypeFlowStage.DestinationPlatform ||
                destinationTrainExited)
            {
                return;
            }

            destinationTrainExited = true;
        }

        public void NotifyEnteredDestinationEscalator()
        {
            if (stage != PrototypeFlowStage.DestinationPlatform ||
                !destinationTrainExited ||
                transitioning)
            {
                return;
            }

            stage = PrototypeFlowStage.DestinationEscalator;
            StartCoroutine(FadeTeleport(
                destinationEscalatorSpawn,
                null));
        }

        public void NotifyReturnedToDestinationPlatform()
        {
            if (stage != PrototypeFlowStage.DestinationEscalator || transitioning)
            {
                return;
            }

            stage = PrototypeFlowStage.DestinationPlatform;
            StartCoroutine(FadeTeleport(
                destinationPlatformReturnSpawn,
                null));
        }

        public void NotifyEnteredDestinationConcourse()
        {
            if (stage != PrototypeFlowStage.DestinationEscalator || transitioning)
            {
                return;
            }

            stage = PrototypeFlowStage.DestinationConcourse;
            StartCoroutine(FadeTeleport(
                destinationConcourseSpawn,
                null));
        }

        public void NotifyReturnedToDestinationEscalator()
        {
            if (stage != PrototypeFlowStage.DestinationConcourse || transitioning)
            {
                return;
            }

            stage = PrototypeFlowStage.DestinationEscalator;
            StartCoroutine(FadeTeleport(
                destinationEscalatorReturnSpawn,
                null));
        }

        public void NotifyExitedDestinationGate()
        {
            if (stage != PrototypeFlowStage.DestinationConcourse ||
                !destinationGateOpen ||
                transitioning)
            {
                return;
            }

            CompleteDeliveryAtGate();
        }

        private void Awake()
        {
            ResolvePlayerSpriteAnimator();
            playerSpriteAnimator?.SetPackageDurabilityProvider(packageDurability);
            EnsureBalanceSystem();
            uiFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Arial" },
                24);
        }

        private void Start()
        {
            if (deliveryService != null)
            {
                deliveryService.DeliverySettled += HandleDeliverySettled;
            }

            stage = PrototypeFlowStage.Tutorial;
            tutorialPage = 0;
            mapOpen = false;
            hasSettlement = false;
            destinationTrainExited = false;
            departureTrainReady = false;
            departureTrainArrivalStarted = false;
            destinationGateOpen = false;
            boardingGraceRemaining = 0f;
            midRideBalanceEmitted = false;
            SetDoorsOpen(departureDoors, false);
            SetDoorsOpen(destinationDoors, false);
            SetBlockerActive(departureGateBlocker, true);
            SetBlockerActive(destinationGateBlocker, true);
            departureTrainArrival?.PrepareOffscreen();

            SetPackageCarrying(false);
            TeleportTo(hubSpawn);
            RefreshPlayerControl();
        }

        private void OnDestroy()
        {
            if (deliveryService != null)
            {
                deliveryService.DeliverySettled -= HandleDeliverySettled;
            }
        }

        private void Update()
        {
            if (stage != PrototypeFlowStage.TrainRide || transitioning)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (enableBalanceChallenges && playerBalance != null &&
                !playerBalance.IsActive && Keyboard.current != null &&
                Keyboard.current.digit9Key.wasPressedThisFrame)
            {
                trainMotionProvider?.Emit(
                    TrainMotionPhase.EmergencyBraking,
                    Vector2.up,
                    1.5f);
            }
#endif

            rideRemaining = Mathf.Max(0f, rideRemaining - Time.deltaTime);
            if (enableBalanceChallenges && !midRideBalanceEmitted &&
                rideRemaining <= rideDuration * 0.5f &&
                playerBalance != null && !playerBalance.IsActive &&
                playerPosture != null &&
                (playerPosture.CurrentState == PostureState.Standing ||
                 playerPosture.CurrentState == PostureState.OverheadCarry))
            {
                midRideBalanceEmitted = true;
                trainMotionProvider?.Emit(
                    TrainMotionPhase.SpeedChanging,
                    Vector2.right,
                    1f);
            }

            if (rideRemaining <= 0f &&
                (playerBalance == null || !playerBalance.IsActive))
            {
                StartCoroutine(ArriveAtDestination());
            }
        }

        private bool BeginSelectedDelivery()
        {
            if (deliveryService == null ||
                !deliveryService.TryStartDelivery(pendingDeliveryId))
            {
                ShowToast("교통비가 부족하거나 배송을 시작할 수 없다.", 3f);
                return false;
            }

            nearbyInteraction = null;
            destinationTrainExited = false;
            departureTrainReady = false;
            departureTrainArrivalStarted = false;
            destinationGateOpen = false;
            midRideBalanceEmitted = false;
            playerBalance?.CancelCurrentChallenge();
            SetDoorsOpen(destinationDoors, false);
            SetDoorsOpen(departureDoors, false);
            SetBlockerActive(departureGateBlocker, false);
            stage = PrototypeFlowStage.DepartureConcourse;
            ShowToast("교통카드 확인", 2f);
            return true;
        }

        private IEnumerator BringDepartureTrainIn()
        {
            ShowToast("열차 도착 대기 중", 2f);
            if (platformTrainWaitDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(platformTrainWaitDuration);
            }

            ShowToast("열차가 승강장으로 들어오고 있다.", 3f);
            if (departureTrainArrival != null)
            {
                yield return departureTrainArrival.Arrive();
            }

            yield return new WaitForSecondsRealtime(0.35f);
            SetDoorsOpen(departureDoors, true);
            yield return WaitForDoors(departureDoors, true, 3f);
            departureTrainArrival?.OpenPlatformEdge();
            departureTrainReady = true;
            ShowToast("열차 도착 / 출입문 열림", 3f);
        }

        private void HandleEnteredDeparturePlatform()
        {
            if (!departureTrainArrivalStarted)
            {
                departureTrainArrivalStarted = true;
                StartCoroutine(BringDepartureTrainIn());
                return;
            }

            ShowToast(
                departureTrainReady
                    ? "열차 대기 중 / 출입문 열림"
                    : "열차 진입 중",
                3f);
        }

        private IEnumerator DepartAfterBoarding()
        {
            boardingGraceRemaining = boardingOpenGraceDuration;
            ShowToast("탑승 완료", Mathf.Max(3f, boardingOpenGraceDuration));

            while (boardingGraceRemaining > 0f &&
                   stage == PrototypeFlowStage.TrainBoarding)
            {
                boardingGraceRemaining = Mathf.Max(
                    0f,
                    boardingGraceRemaining - Time.unscaledDeltaTime);
                yield return null;
            }

            if (stage != PrototypeFlowStage.TrainBoarding)
            {
                yield break;
            }

            transitioning = true;
            nearbyInteraction = null;
            RefreshPlayerControl();
            SetDoorsOpen(departureDoors, false);
            yield return WaitForDoors(departureDoors, false, 3f);
            yield return FadeTo(1f, 0.65f);

            TeleportTo(travelTrainSpawn);
            yield return new WaitForSecondsRealtime(0.25f);
            stage = PrototypeFlowStage.TrainRide;
            rideRemaining = rideDuration;
            midRideBalanceEmitted = false;
            yield return FadeTo(0f, 0.65f);

            transitioning = false;
            RefreshPlayerControl();
            ShowToast("열차 출발", 2f);
            if (enableBalanceChallenges)
            {
                trainMotionProvider?.Emit(
                    TrainMotionPhase.Departing,
                    Vector2.left,
                    1f);
            }
        }

        private IEnumerator ArriveAtDestination()
        {
            playerBalance?.CancelCurrentChallenge();
            trainMotionProvider?.Emit(
                TrainMotionPhase.Stopped,
                Vector2.zero,
                0f);
            transitioning = true;
            RefreshPlayerControl();
            yield return FadeTo(1f, 0.65f);

            stage = PrototypeFlowStage.DestinationPlatform;
            destinationTrainExited = false;
            TeleportTo(destinationTrainSpawn);
            SetDoorsOpen(destinationDoors, true);
            yield return WaitForDoors(destinationDoors, true, 3f);
            yield return new WaitForSecondsRealtime(0.25f);
            yield return FadeTo(0f, 0.65f);

            transitioning = false;
            RefreshPlayerControl();
            ShowToast("정자역 도착", 2f);
        }

        private void CompleteDeliveryAtGate()
        {
            hasSettlement = false;
            transitProgress?.ArriveAt("jeongja", true);
            if (!hasSettlement)
            {
                ShowToast("정산 정보를 만들지 못했다.", 3f);
                return;
            }

            nearbyInteraction = null;
            stage = PrototypeFlowStage.Settlement;
            RefreshPlayerControl();
        }

        private void OpenDestinationGate()
        {
            destinationGateOpen = true;
            nearbyInteraction = null;
            SetBlockerActive(destinationGateBlocker, false);
            ShowToast("교통카드 확인", 2f);
        }

        private void HandleDeliverySettled(DeliverySettlementSnapshot settlement)
        {
            lastSettlement = settlement;
            hasSettlement = true;
        }

        private void ReturnToHub()
        {
            if (transitioning)
            {
                return;
            }

            StartCoroutine(FadeTeleport(hubSpawn, () =>
            {
                transitProgress?.ArriveAt("gachon", false);
                stage = PrototypeFlowStage.GachonHub;
                pendingDeliveryId = null;
                destinationTrainExited = false;
                departureTrainReady = false;
                departureTrainArrivalStarted = false;
                destinationGateOpen = false;
                hasSettlement = false;
                mapOpen = false;
                boardingGraceRemaining = 0f;
                midRideBalanceEmitted = false;
                playerBalance?.CancelCurrentChallenge();
                trainMotionProvider?.Emit(
                    TrainMotionPhase.Stopped,
                    Vector2.zero,
                    0f);
                SetPackageCarrying(false);
                SetDoorsOpen(departureDoors, false);
                SetDoorsOpen(destinationDoors, false);
                SetBlockerActive(departureGateBlocker, true);
                SetBlockerActive(destinationGateBlocker, true);
                departureTrainArrival?.PrepareOffscreen();
                ShowToast("가천대역 복귀", 2f);
            }));
        }

        private void SetPackageCarrying(bool carrying)
        {
            ResolvePlayerSpriteAnimator();
            playerSpriteAnimator?.SetCarryingPackage(carrying);

            PlayerCollisionImpact collisionImpact = playerController != null
                ? playerController.GetComponent<PlayerCollisionImpact>()
                : null;
            if (collisionImpact != null)
            {
                collisionImpact.enabled = carrying;
            }
        }

        private bool IsPackageCarrying()
        {
            ResolvePlayerSpriteAnimator();
            return playerSpriteAnimator != null &&
                   playerSpriteAnimator.IsCarryingPackage;
        }

        private void ResolvePlayerSpriteAnimator()
        {
            if (playerSpriteAnimator == null && playerController != null)
            {
                playerSpriteAnimator =
                    playerController.GetComponent<PrototypePlayerSpriteAnimator>();
            }
        }

        private void EnsureBalanceSystem()
        {
            if (playerController == null || playerPosture == null)
            {
                return;
            }

            trainMotionProvider = GetComponent<PrototypeTrainMotionProvider>();
            if (trainMotionProvider == null)
            {
                trainMotionProvider =
                    gameObject.AddComponent<PrototypeTrainMotionProvider>();
            }

            playerBalance =
                playerController.GetComponent<RuntimePlayerBalance>();
            if (playerBalance == null)
            {
                playerBalance =
                    playerController.gameObject.AddComponent<RuntimePlayerBalance>();
            }

            playerBalance.Configure(
                trainMotionProvider,
                playerPosture,
                packageDurability);
            playerController.SetBalanceController(playerBalance);

            BalanceHudPresenter balanceHud =
                GetComponent<BalanceHudPresenter>();
            if (balanceHud == null)
            {
                balanceHud = gameObject.AddComponent<BalanceHudPresenter>();
            }
            balanceHud.Configure(playerBalance);
        }

        private static void SetBlockerActive(GameObject blocker, bool active)
        {
            if (blocker != null)
            {
                blocker.SetActive(active);
            }
        }

        private IEnumerator FadeTeleport(Transform target, Action completed)
        {
            transitioning = true;
            nearbyInteraction = null;
            RefreshPlayerControl();
            yield return FadeTo(1f, 0.55f);
            TeleportTo(target);
            yield return new WaitForSecondsRealtime(0.2f);
            yield return FadeTo(0f, 0.55f);
            transitioning = false;
            completed?.Invoke();
            RefreshPlayerControl();
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

        private static IEnumerator WaitForDoors(
            TrainDoorController[] doors,
            bool open,
            float timeout)
        {
            float elapsed = 0f;
            while (!AreDoorsInState(doors, open) && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
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

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            float startAlpha = fadeAlpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeAlpha = Mathf.Lerp(
                    startAlpha,
                    targetAlpha,
                    Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            fadeAlpha = targetAlpha;
        }

        private void TeleportTo(Transform target)
        {
            if (target == null || playerController == null)
            {
                return;
            }

            Rigidbody2D body = playerController.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = target.position;
                body.linearVelocity = Vector2.zero;
            }
            else
            {
                playerController.transform.position = target.position;
            }

            PrototypeCameraZone zone =
                target.GetComponentInParent<PrototypeCameraZone>();
            if (zone != null)
            {
                cameraFollow?.EnterZone(zone);
            }
            else
            {
                cameraFollow?.SnapToTarget();
            }
        }

        private void RefreshPlayerControl()
        {
            if (playerController == null)
            {
                return;
            }

            playerController.enabled =
                !transitioning &&
                !mapOpen &&
                stage != PrototypeFlowStage.Tutorial &&
                stage != PrototypeFlowStage.Settlement;
        }

        private void FinishTutorial()
        {
            tutorialPage = TutorialPages.Length - 1;
            stage = PrototypeFlowStage.GachonHub;
            RefreshPlayerControl();
        }

        private void SelectFirstDelivery()
        {
            bool alreadyAccepted =
                stage == PrototypeFlowStage.DeliverySelected &&
                pendingDeliveryId == "1-1";
            pendingDeliveryId = "1-1";
            if (!alreadyAccepted)
            {
                packageDurability?.ResetToFull();
            }
            SetPackageCarrying(true);
            stage = PrototypeFlowStage.DeliverySelected;
            mapOpen = false;
            RefreshPlayerControl();
            ShowToast(
                alreadyAccepted
                    ? "정자역 배송은 이미 수락했다."
                    : "정자역 배송 수락 · 케이크 수령",
                2f);
        }

        private void ShowToast(string message, float seconds = 2f)
        {
            toast = message;
            toastUntil = Time.unscaledTime + seconds;
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawHud();

            if (stage == PrototypeFlowStage.Tutorial)
            {
                DrawTutorial();
            }
            else if (mapOpen)
            {
                DrawDeliveryMap();
            }
            else if (stage == PrototypeFlowStage.Settlement)
            {
                DrawSettlement();
            }

            DrawInteractionPrompt();
            DrawToast();
            DrawFade();
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(22, 22, 18, 18)
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = Color.white;
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 20,
                wordWrap = true
            };
            bodyStyle.normal.textColor = Color.white;
            smallStyle = new GUIStyle(bodyStyle)
            {
                fontSize = 16
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = uiFont,
                fontSize = 19,
                fixedHeight = 48f
            };
            hudTitleStyle = new GUIStyle(titleStyle)
            {
                fontSize = 14
            };
            hudSmallStyle = new GUIStyle(smallStyle)
            {
                fontSize = 8
            };
        }

        private void DrawHud()
        {
            Rect hud = new Rect(
                12f,
                12f,
                Mathf.Min(390f, Screen.width - 24f),
                76f);
            GUI.Box(hud, GUIContent.none, panelStyle);

            int cash = economyService != null
                ? economyService.CurrentEconomyState.CurrentCash
                : 0;
            PackageDurabilitySnapshot durability = packageDurability != null
                ? packageDurability.CurrentDurability
                : new PackageDurabilitySnapshot(100f, 100f, false);
            bool isCarryingPackage = IsPackageCarrying();

            GUI.Label(new Rect(hud.x + 12f, hud.y + 5f, hud.width - 24f, 20f),
                $"{GetStageLabel()}   보유 현금 {cash:N0}원", hudTitleStyle);
            GUI.Label(new Rect(hud.x + 12f, hud.y + 27f, hud.width - 24f, 16f),
                isCarryingPackage
                    ? $"상자 {durability.BoxDurability:0}%   케이크 {durability.CakeDurability:0}%"
                    : "운반 물품 없음",
                hudSmallStyle);
            GUI.Label(new Rect(hud.x + 12f, hud.y + 44f, hud.width - 24f, 27f),
                GetObjective(), hudSmallStyle);

            if (stage == PrototypeFlowStage.GachonHub ||
                stage == PrototypeFlowStage.DeliverySelected)
            {
                float right = Screen.width - 168f;
                if (GUI.Button(new Rect(right, 20f, 150f, 42f), "저장 (준비 중)", buttonStyle))
                {
                    ShowToast("저장 기능은 아직 연결하지 않았다.", 2.5f);
                }

                if (GUI.Button(new Rect(right, 70f, 150f, 42f), "게임 나가기", buttonStyle))
                {
                    QuitPrototype();
                }
            }
        }

        private void DrawTutorial()
        {
            Rect modal = CenteredRect(720f, 390f);
            GUI.Box(modal, GUIContent.none, panelStyle);
            GUI.Label(new Rect(modal.x + 24f, modal.y + 20f, modal.width - 48f, 42f),
                $"가천대 배송 안내  {tutorialPage + 1}/{TutorialPages.Length}",
                titleStyle);
            GUI.Label(new Rect(modal.x + 24f, modal.y + 82f, modal.width - 48f, 190f),
                TutorialPages[tutorialPage], bodyStyle);

            if (tutorialPage > 0 &&
                GUI.Button(new Rect(modal.x + 24f, modal.yMax - 70f, 150f, 46f),
                    "이전", buttonStyle))
            {
                tutorialPage--;
            }

            string nextLabel = tutorialPage >= TutorialPages.Length - 1
                ? "배송 시작 준비"
                : "다음";
            if (GUI.Button(new Rect(modal.xMax - 194f, modal.yMax - 70f, 170f, 46f),
                    nextLabel, buttonStyle))
            {
                if (tutorialPage >= TutorialPages.Length - 1)
                {
                    FinishTutorial();
                }
                else
                {
                    tutorialPage++;
                }
            }
        }

        private void DrawDeliveryMap()
        {
            Rect modal = CenteredRect(780f, 500f);
            GUI.Box(modal, GUIContent.none, panelStyle);
            GUI.Label(new Rect(modal.x + 24f, modal.y + 18f, modal.width - 48f, 42f),
                "가천대학교 배달 앱", titleStyle);
            GUI.Label(new Rect(modal.x + 24f, modal.y + 68f, modal.width - 48f, 48f),
                "배송 1-1   가천대역 → 정자역   ★", bodyStyle);
            GUI.Label(new Rect(modal.x + 24f, modal.y + 120f, modal.width - 48f, 160f),
                "노선: 가천대 → 모란 → 야탑 → 서현 → 정자\n" +
                "시간대: 오후 1시 / 환승 없음 / 혼잡도 여유\n" +
                "물품: 케이크 / 물품 가치: 25,000원\n" +
                "교통비: 1,500원 / 배달 수수료: 3,000원",
                bodyStyle);
            GUI.Label(new Rect(modal.x + 24f, modal.y + 300f, modal.width - 48f, 70f),
                "배송을 수락하면 케이크를 수령한다. 이후 개찰구에서 교통카드를 찍으면 교통비가 결제된다.",
                smallStyle);

            if (GUI.Button(new Rect(modal.x + 24f, modal.yMax - 72f, 150f, 48f),
                    "닫기", buttonStyle))
            {
                mapOpen = false;
                RefreshPlayerControl();
            }

            bool alreadyAccepted =
                stage == PrototypeFlowStage.DeliverySelected &&
                pendingDeliveryId == "1-1";
            if (GUI.Button(new Rect(modal.xMax - 224f, modal.yMax - 72f, 200f, 48f),
                    alreadyAccepted ? "수락 완료" : "배송 수락", buttonStyle))
            {
                SelectFirstDelivery();
            }
        }

        private void DrawSettlement()
        {
            Rect modal = CenteredRect(720f, 520f);
            GUI.Box(modal, GUIContent.none, panelStyle);
            GUI.Label(new Rect(modal.x + 24f, modal.y + 18f, modal.width - 48f, 42f),
                "배송 완료 정산", titleStyle);

            int currentCash = economyService != null
                ? economyService.CurrentEconomyState.CurrentCash
                : 0;
            string result =
                $"배달 수수료                 +{lastSettlement.DeliveryFee:N0}원\n" +
                $"물품 배상액                  -{lastSettlement.Compensation:N0}원\n" +
                $"출발 교통비                  -{lastSettlement.OutboundFare:N0}원\n" +
                $"복귀 교통비                  -{lastSettlement.ReturnFare:N0}원\n\n" +
                $"최종 손익                     {lastSettlement.NetIncome:+#,0;-#,0;0}원\n" +
                $"현재 보유 현금                {currentCash:N0}원";
            GUI.Label(new Rect(modal.x + 30f, modal.y + 88f, modal.width - 60f, 285f),
                result, bodyStyle);

            if (GUI.Button(new Rect(modal.x + 30f, modal.yMax - 78f, 180f, 50f),
                    "저장 (준비 중)", buttonStyle))
            {
                ShowToast("저장 기능은 아직 연결하지 않았다.", 2.5f);
            }

            if (GUI.Button(new Rect(modal.xMax - 250f, modal.yMax - 78f, 220f, 50f),
                    "가천대역으로 복귀", buttonStyle))
            {
                ReturnToHub();
            }
        }

        private void DrawInteractionPrompt()
        {
            if (nearbyInteraction == null ||
                !CanPerform(nearbyInteraction.Action))
            {
                return;
            }

            Rect promptRect = new Rect(
                Screen.width * 0.5f - 210f,
                Screen.height - 88f,
                420f,
                52f);
            GUI.Box(promptRect, nearbyInteraction.Prompt, buttonStyle);
        }

        private void DrawToast()
        {
            if (string.IsNullOrEmpty(toast) || Time.unscaledTime > toastUntil)
            {
                return;
            }

            Rect toastRect = new Rect(
                Screen.width * 0.5f - 300f,
                Screen.height - 150f,
                600f,
                52f);
            GUI.Box(toastRect, toast, bodyStyle);
        }

        private void DrawFade()
        {
            if (fadeAlpha <= 0.001f)
            {
                return;
            }

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private string GetObjective()
        {
            switch (stage)
            {
                case PrototypeFlowStage.Tutorial:
                    return "튜토리얼을 확인한다.";
                case PrototypeFlowStage.GachonHub:
                    return "개찰구 앞 배송 단말기에서 배송을 선택한다.";
                case PrototypeFlowStage.DeliverySelected:
                    return "개찰구에 교통카드를 찍는다.";
                case PrototypeFlowStage.DepartureConcourse:
                    return "2열 에스컬레이터로 내려간다. 뒤 개찰구로는 나갈 수 없다.";
                case PrototypeFlowStage.DepartureEscalator:
                    return "오른쪽 끝은 승강장, 왼쪽 끝은 개찰구 대합실이다.";
                case PrototypeFlowStage.DeparturePlatform:
                    return departureTrainReady
                        ? "열린 문을 지나 열차 안으로 직접 들어간다."
                        : "안전선 안쪽에서 열차가 들어오기를 기다린다.";
                case PrototypeFlowStage.TrainBoarding:
                    return $"열차 문이 닫히기까지 {Mathf.CeilToInt(boardingGraceRemaining)}초";
                case PrototypeFlowStage.TrainRide:
                    return $"정자역까지 이동 중... {Mathf.CeilToInt(rideRemaining)}초";
                case PrototypeFlowStage.DestinationPlatform:
                    return destinationTrainExited
                        ? "2열 에스컬레이터 입구로 이동한다."
                        : "열차에서 직접 내려 승강장으로 이동한다.";
                case PrototypeFlowStage.DestinationEscalator:
                    return "오른쪽 끝은 대합실, 왼쪽 끝은 승강장이다.";
                case PrototypeFlowStage.DestinationConcourse:
                    return destinationGateOpen
                        ? "열린 개찰구를 직접 통과한다."
                        : "E키로 개찰구를 열거나 왼쪽 에스컬레이터로 돌아간다.";
                case PrototypeFlowStage.Settlement:
                    return "정산 결과를 확인한다.";
                default:
                    return string.Empty;
            }
        }

        private string GetStageLabel()
        {
            switch (stage)
            {
                case PrototypeFlowStage.Tutorial:
                    return "가천대역 안내";
                case PrototypeFlowStage.GachonHub:
                case PrototypeFlowStage.DeliverySelected:
                    return "가천대역";
                case PrototypeFlowStage.DepartureConcourse:
                    return "가천대역 대합실";
                case PrototypeFlowStage.DepartureEscalator:
                    return "가천대역 에스컬레이터";
                case PrototypeFlowStage.DeparturePlatform:
                    return "가천대역 승강장";
                case PrototypeFlowStage.TrainBoarding:
                case PrototypeFlowStage.TrainRide:
                    return "수인분당선 열차";
                case PrototypeFlowStage.DestinationPlatform:
                    return "정자역 승강장";
                case PrototypeFlowStage.DestinationEscalator:
                    return "정자역 에스컬레이터";
                case PrototypeFlowStage.DestinationConcourse:
                    return "정자역 대합실";
                case PrototypeFlowStage.Settlement:
                    return "배송 정산";
                default:
                    return string.Empty;
            }
        }

        private static Rect CenteredRect(float width, float height)
        {
            width = Mathf.Min(width, Screen.width - 40f);
            height = Mathf.Min(height, Screen.height - 40f);
            return new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
        }

        private static void QuitPrototype()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

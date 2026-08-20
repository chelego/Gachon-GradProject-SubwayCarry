using System;
using System.Collections;
using SubwayCarry.Core.Contracts;
using SubwayCarry.Delivery;
using SubwayCarry.Delivery.Mocks;
using SubwayCarry.Gameplay;
using UnityEngine;

namespace SubwayCarry.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeGameFlowController : MonoBehaviour
    {
        private static readonly string[] TutorialPages =
        {
            "방학 동안 다음 학기 학비를 마련해야 한다.\n가천대학교 배달 앱에서 첫 배송을 시작해보자.",
            "WASD로 이동하고 마우스 방향을 바라본다.\n가까운 시설은 E키로 상호작용한다.",
            "개찰구 앞 배송 단말기에서 목적지를 선택한 뒤\n교통카드를 찍으면 출발 승강장으로 이동한다.",
            "이번 통합 프로토타입은 승객 없이 전체 진행만 확인한다.\n열차 탑승 후 목적지역 개찰구까지 이동해보자."
        };

        [Header("Player")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerPosture playerPosture;
        [SerializeField] private PackageDurability packageDurability;
        [SerializeField] private PrototypeCameraFollow cameraFollow;

        [Header("Services")]
        [SerializeField] private DeliveryService deliveryService;
        [SerializeField] private EconomyService economyService;
        [SerializeField] private MockTransitProgressProvider transitProgress;

        [Header("Flow Points")]
        [SerializeField] private Transform hubSpawn;
        [SerializeField] private Transform departureSpawn;
        [SerializeField] private Transform destinationTrainSpawn;
        [SerializeField] private GameObject departureDoorBlocker;
        [SerializeField, Min(1f)] private float rideDuration = 8f;

        private PrototypeFlowStage stage;
        private PrototypeInteractable nearbyInteraction;
        private DeliverySettlementSnapshot lastSettlement;
        private bool hasSettlement;
        private bool mapOpen;
        private bool destinationTrainExited;
        private bool transitioning;
        private string pendingDeliveryId;
        private string toast;
        private float toastUntil;
        private float rideRemaining;
        private float fadeAlpha;
        private int tutorialPage;

        private Font uiFont;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;

        public PrototypeFlowStage CurrentStage => stage;

        public void Configure(
            PlayerController controller,
            PlayerPosture posture,
            PackageDurability durability,
            PrototypeCameraFollow followCamera,
            DeliveryService deliveries,
            EconomyService economy,
            MockTransitProgressProvider transit,
            Transform hub,
            Transform departure,
            Transform destinationTrain,
            GameObject doorBlocker)
        {
            playerController = controller;
            playerPosture = posture;
            packageDurability = durability;
            cameraFollow = followCamera;
            deliveryService = deliveries;
            economyService = economy;
            transitProgress = transit;
            hubSpawn = hub;
            departureSpawn = departure;
            destinationTrainSpawn = destinationTrain;
            departureDoorBlocker = doorBlocker;
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
                    return stage == PrototypeFlowStage.DestinationStation &&
                           destinationTrainExited;

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
                    CompleteDeliveryAtGate();
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

        public void NotifyBoardedTrain()
        {
            if (stage != PrototypeFlowStage.DeparturePlatform || transitioning)
            {
                return;
            }

            stage = PrototypeFlowStage.TrainRide;
            rideRemaining = rideDuration;
            if (departureDoorBlocker != null)
            {
                departureDoorBlocker.SetActive(true);
            }

            ShowToast("탑승 완료. 열차가 출발한다.", 2.5f);
        }

        public void NotifyLeftTrainAtDestination()
        {
            if (stage != PrototypeFlowStage.DestinationStation ||
                destinationTrainExited)
            {
                return;
            }

            destinationTrainExited = true;
            ShowToast("정자역에 도착했다. 개찰구로 나가자.", 3f);
        }

        private void Awake()
        {
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
            if (departureDoorBlocker != null)
            {
                departureDoorBlocker.SetActive(false);
            }

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

            rideRemaining = Mathf.Max(0f, rideRemaining - Time.deltaTime);
            if (rideRemaining <= 0f)
            {
                StartCoroutine(ArriveAtDestination());
            }
        }

        private bool BeginSelectedDelivery()
        {
            packageDurability?.ResetToFull();
            if (deliveryService == null ||
                !deliveryService.TryStartDelivery(pendingDeliveryId))
            {
                ShowToast("교통비가 부족하거나 배송을 시작할 수 없다.", 3f);
                return false;
            }

            nearbyInteraction = null;
            stage = PrototypeFlowStage.DeparturePlatform;
            StartCoroutine(FadeTeleport(
                departureSpawn,
                () => ShowToast("출발 승강장이다. 열린 문으로 직접 탑승하자.", 3f)));
            return true;
        }

        private IEnumerator ArriveAtDestination()
        {
            transitioning = true;
            RefreshPlayerControl();
            yield return FadeTo(1f, 0.65f);

            if (departureDoorBlocker != null)
            {
                departureDoorBlocker.SetActive(false);
            }

            stage = PrototypeFlowStage.DestinationStation;
            destinationTrainExited = false;
            TeleportTo(destinationTrainSpawn);
            yield return new WaitForSecondsRealtime(0.25f);
            yield return FadeTo(0f, 0.65f);

            transitioning = false;
            RefreshPlayerControl();
            ShowToast("정자역 도착. 열차에서 내려 개찰구로 이동하자.", 3f);
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
                hasSettlement = false;
                mapOpen = false;
                ShowToast("가천대역으로 복귀했다. 다음 배송을 선택할 수 있다.", 3f);
            }));
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

            cameraFollow?.SnapToTarget();
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
            ShowToast("개찰구 앞 배송 단말기로 이동하자.", 3f);
        }

        private void SelectFirstDelivery()
        {
            pendingDeliveryId = "1-1";
            stage = PrototypeFlowStage.DeliverySelected;
            mapOpen = false;
            RefreshPlayerControl();
            ShowToast("정자역 배송을 선택했다. 개찰구에서 교통카드를 찍자.", 3f);
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
        }

        private void DrawHud()
        {
            Rect hud = new Rect(18f, 18f, Mathf.Min(520f, Screen.width - 36f), 128f);
            GUI.Box(hud, GUIContent.none, panelStyle);

            int cash = economyService != null
                ? economyService.CurrentEconomyState.CurrentCash
                : 0;
            PackageDurabilitySnapshot durability = packageDurability != null
                ? packageDurability.CurrentDurability
                : new PackageDurabilitySnapshot(100f, 100f, false);

            GUI.Label(new Rect(hud.x + 18f, hud.y + 10f, hud.width - 36f, 32f),
                $"{GetStageLabel()}   보유 현금 {cash:N0}원", titleStyle);
            GUI.Label(new Rect(hud.x + 18f, hud.y + 47f, hud.width - 36f, 30f),
                $"상자 {durability.BoxDurability:0}%   케이크 {durability.CakeDurability:0}%",
                smallStyle);
            GUI.Label(new Rect(hud.x + 18f, hud.y + 77f, hud.width - 36f, 42f),
                GetObjective(), smallStyle);

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
                "선택 후 개찰구에서 교통카드를 찍으면 교통비가 결제된다.",
                smallStyle);

            if (GUI.Button(new Rect(modal.x + 24f, modal.yMax - 72f, 150f, 48f),
                    "닫기", buttonStyle))
            {
                mapOpen = false;
                RefreshPlayerControl();
            }

            if (GUI.Button(new Rect(modal.xMax - 224f, modal.yMax - 72f, 200f, 48f),
                    "이 배송 선택", buttonStyle))
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
                case PrototypeFlowStage.DeparturePlatform:
                    return "열린 문을 지나 열차 안으로 직접 들어간다.";
                case PrototypeFlowStage.TrainRide:
                    return $"정자역까지 이동 중... {Mathf.CeilToInt(rideRemaining)}초";
                case PrototypeFlowStage.DestinationStation:
                    return destinationTrainExited
                        ? "정자역 개찰구로 이동해 배송을 완료한다."
                        : "열차에서 직접 내려 승강장으로 이동한다.";
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
                case PrototypeFlowStage.DeparturePlatform:
                    return "가천대역 승강장";
                case PrototypeFlowStage.TrainRide:
                    return "수인분당선 열차";
                case PrototypeFlowStage.DestinationStation:
                    return "정자역";
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

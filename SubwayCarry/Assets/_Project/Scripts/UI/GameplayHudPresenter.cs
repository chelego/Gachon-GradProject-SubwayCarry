using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.UI
{
    [DisallowMultipleComponent]
    public sealed class GameplayHudPresenter : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour staminaProviderSource;
        [SerializeField] private MonoBehaviour packageDurabilityProviderSource;
        [SerializeField] private MonoBehaviour packageOwnerSource;
        [SerializeField] private MonoBehaviour deliveryStateProviderSource;
        [SerializeField] private MonoBehaviour economyStateProviderSource;
        [SerializeField] private MonoBehaviour settlementProviderSource;

        private IStaminaStateProvider staminaProvider;
        private IPackageDurabilityProvider packageDurabilityProvider;
        private IPackageAvailabilityController packageAvailability;
        private IDeliveryStateProvider deliveryStateProvider;
        private IEconomyStateProvider economyStateProvider;
        private IDeliverySettlementProvider settlementProvider;

        private StaminaStateSnapshot staminaState;
        private PackageDurabilitySnapshot durabilityState;
        private DeliveryStateSnapshot deliveryState;
        private EconomyStateSnapshot economyState;
        private DeliverySettlementSnapshot settlementState;
        private bool hasSettlement;
        private bool subscribed;

        private Texture2D whiteTexture;
        private Font font;
        private GUIStyle panelStyle;
        private GUIStyle headerStyle;
        private GUIStyle labelStyle;
        private GUIStyle valueStyle;
        private GUIStyle smallStyle;

        public StaminaStateSnapshot CurrentStaminaState => staminaState;
        public PackageDurabilitySnapshot CurrentDurabilityState => durabilityState;
        public DeliveryStateSnapshot CurrentDeliveryState => deliveryState;
        public EconomyStateSnapshot CurrentEconomyState => economyState;
        public DeliverySettlementSnapshot CurrentSettlement => settlementState;
        public bool HasSettlement => hasSettlement;

        public void Configure(
            MonoBehaviour staminaSource,
            MonoBehaviour packageSource,
            MonoBehaviour packageOwner,
            MonoBehaviour deliverySource,
            MonoBehaviour economySource,
            MonoBehaviour settlementSource)
        {
            Unsubscribe();
            staminaProviderSource = staminaSource;
            packageDurabilityProviderSource = packageSource;
            packageOwnerSource = packageOwner;
            deliveryStateProviderSource = deliverySource;
            economyStateProviderSource = economySource;
            settlementProviderSource = settlementSource;
            ResolveSources();
            ReadCurrentStates();
            Subscribe();
        }

        private void Awake()
        {
            ResolveSources();
            ReadCurrentStates();

            whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Arial" },
                20);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            ReadCurrentStates();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (whiteTexture != null)
            {
                Destroy(whiteTexture);
            }
        }

        private void ResolveSources()
        {
            staminaProvider = staminaProviderSource as IStaminaStateProvider;
            packageDurabilityProvider =
                packageDurabilityProviderSource as IPackageDurabilityProvider;
            packageAvailability = packageOwnerSource as IPackageAvailabilityController;
            deliveryStateProvider = deliveryStateProviderSource as IDeliveryStateProvider;
            economyStateProvider = economyStateProviderSource as IEconomyStateProvider;
            settlementProvider = settlementProviderSource as IDeliverySettlementProvider;

            if (packageDurabilityProvider != null || packageOwnerSource == null)
            {
                return;
            }

            MonoBehaviour[] packageBehaviours =
                packageOwnerSource.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour packageBehaviour in packageBehaviours)
            {
                if (packageBehaviour is IPackageDurabilityProvider provider)
                {
                    packageDurabilityProviderSource = packageBehaviour;
                    packageDurabilityProvider = provider;
                    break;
                }
            }
        }

        private void ReadCurrentStates()
        {
            if (staminaProvider != null)
            {
                staminaState = staminaProvider.CurrentStaminaState;
            }

            if (packageDurabilityProvider != null)
            {
                durabilityState = packageDurabilityProvider.CurrentDurability;
            }

            if (deliveryStateProvider != null)
            {
                deliveryState = deliveryStateProvider.CurrentDeliveryState;
            }

            if (economyStateProvider != null)
            {
                economyState = economyStateProvider.CurrentEconomyState;
            }
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || subscribed)
            {
                return;
            }

            if (staminaProvider != null)
            {
                staminaProvider.StaminaStateChanged += HandleStaminaStateChanged;
            }

            if (packageDurabilityProvider != null)
            {
                packageDurabilityProvider.DurabilityChanged += HandleDurabilityChanged;
            }

            if (deliveryStateProvider != null)
            {
                deliveryStateProvider.DeliveryStateChanged += HandleDeliveryStateChanged;
            }

            if (economyStateProvider != null)
            {
                economyStateProvider.EconomyStateChanged += HandleEconomyStateChanged;
            }

            if (settlementProvider != null)
            {
                settlementProvider.DeliverySettled += HandleDeliverySettled;
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (staminaProvider != null)
            {
                staminaProvider.StaminaStateChanged -= HandleStaminaStateChanged;
            }

            if (packageDurabilityProvider != null)
            {
                packageDurabilityProvider.DurabilityChanged -= HandleDurabilityChanged;
            }

            if (deliveryStateProvider != null)
            {
                deliveryStateProvider.DeliveryStateChanged -= HandleDeliveryStateChanged;
            }

            if (economyStateProvider != null)
            {
                economyStateProvider.EconomyStateChanged -= HandleEconomyStateChanged;
            }

            if (settlementProvider != null)
            {
                settlementProvider.DeliverySettled -= HandleDeliverySettled;
            }

            subscribed = false;
        }

        private void HandleStaminaStateChanged(StaminaStateSnapshot snapshot)
        {
            staminaState = snapshot;
        }

        private void HandleDurabilityChanged(PackageDurabilitySnapshot snapshot)
        {
            durabilityState = snapshot;
        }

        private void HandleDeliveryStateChanged(DeliveryStateSnapshot snapshot)
        {
            deliveryState = snapshot;
            if (snapshot.Phase == DeliveryPhase.InTransit)
            {
                hasSettlement = false;
            }
        }

        private void HandleEconomyStateChanged(EconomyStateSnapshot snapshot)
        {
            economyState = snapshot;
        }

        private void HandleDeliverySettled(DeliverySettlementSnapshot snapshot)
        {
            settlementState = snapshot;
            hasSettlement = true;
        }

        private void OnGUI()
        {
            EnsureStyles();

            bool showPackage = packageAvailability != null
                ? packageAvailability.HasPackage
                : deliveryState.Phase == DeliveryPhase.InTransit;
            float panelHeight = showPackage ? 270f : 188f;
            if (hasSettlement)
            {
                panelHeight += 54f;
            }

            float width = Mathf.Min(370f, Mathf.Max(250f, Screen.width - 32f));
            Rect panel = new Rect(16f, 16f, width, panelHeight);
            GUI.Box(panel, GUIContent.none, panelStyle);

            float x = panel.x + 18f;
            float contentWidth = panel.width - 36f;
            float y = panel.y + 12f;

            GUI.Label(new Rect(x, y, contentWidth, 28f), "배송 상태", headerStyle);
            y += 34f;
            DrawKeyValue(
                x,
                ref y,
                contentWidth,
                "현금",
                FormatCash(economyState.CurrentCash));
            DrawKeyValue(
                x,
                ref y,
                contentWidth,
                "배송",
                GetDeliveryLabel(deliveryState));

            y += 5f;
            DrawBar(
                new Rect(x, y, contentWidth, 22f),
                staminaState.Ratio,
                new Color(0.18f, 0.72f, 0.9f, 1f),
                "스테미너  " + staminaState.Current.ToString("0.0") +
                " / " + staminaState.Maximum.ToString("0.0"));
            y += 31f;

            if (showPackage)
            {
                DrawBar(
                    new Rect(x, y, contentWidth, 22f),
                    durabilityState.BoxDurability / 100f,
                    new Color(0.82f, 0.62f, 0.25f, 1f),
                    "상자 내구도  " +
                    Mathf.RoundToInt(durabilityState.BoxDurability) + "%");
                y += 31f;
                DrawBar(
                    new Rect(x, y, contentWidth, 22f),
                    durabilityState.CakeDurability / 100f,
                    new Color(0.95f, 0.44f, 0.62f, 1f),
                    "케이크 내구도  " +
                    Mathf.RoundToInt(durabilityState.CakeDurability) + "%");
                y += 31f;
            }

            if (hasSettlement)
            {
                y += 4f;
                GUI.Label(
                    new Rect(x, y, contentWidth, 46f),
                    "정산  수수료 " + FormatCash(settlementState.DeliveryFee) +
                    "  ·  배상 " + FormatCash(settlementState.Compensation) +
                    "\n최종 손익  " + FormatSignedCash(settlementState.NetIncome),
                    smallStyle);
            }
        }

        private void DrawKeyValue(
            float x,
            ref float y,
            float width,
            string key,
            string value)
        {
            GUI.Label(new Rect(x, y, width * 0.34f, 24f), key, labelStyle);
            GUI.Label(
                new Rect(x + width * 0.32f, y, width * 0.68f, 24f),
                value,
                valueStyle);
            y += 27f;
        }

        private void DrawBar(Rect rect, float ratio, Color fillColor, string label)
        {
            DrawSolid(rect, new Color(0.08f, 0.1f, 0.14f, 0.95f));
            Rect fill = rect;
            fill.width *= Mathf.Clamp01(ratio);
            DrawSolid(fill, fillColor);
            GUI.Label(rect, label, smallStyle);
        }

        private void DrawSolid(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, whiteTexture);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box);
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            headerStyle.normal.textColor = Color.white;
            labelStyle = new GUIStyle(headerStyle)
            {
                fontSize = 16,
                fontStyle = FontStyle.Normal
            };
            labelStyle.normal.textColor = new Color(0.76f, 0.82f, 0.9f);
            valueStyle = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleRight
            };
            valueStyle.normal.textColor = Color.white;
            smallStyle = new GUIStyle(labelStyle)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            smallStyle.normal.textColor = Color.white;
        }

        private static string GetDeliveryLabel(in DeliveryStateSnapshot state)
        {
            if (state.Phase == DeliveryPhase.None)
            {
                return "대기 중";
            }

            string deliveryId = string.IsNullOrEmpty(state.DeliveryId)
                ? "-"
                : state.DeliveryId;
            return deliveryId + " · " + GetPhaseLabel(state.Phase);
        }

        private static string GetPhaseLabel(DeliveryPhase phase)
        {
            switch (phase)
            {
                case DeliveryPhase.Selected:
                    return "선택됨";
                case DeliveryPhase.TravellingToDeparture:
                    return "출발역 이동";
                case DeliveryPhase.InTransit:
                    return "운송 중";
                case DeliveryPhase.Transferring:
                    return "환승 중";
                case DeliveryPhase.Arrived:
                    return "도착";
                case DeliveryPhase.Completed:
                    return "완료";
                case DeliveryPhase.Failed:
                    return "실패";
                default:
                    return "대기 중";
            }
        }

        private static string FormatCash(int amount)
        {
            return amount.ToString("N0") + "원";
        }

        private static string FormatSignedCash(int amount)
        {
            return (amount > 0 ? "+" : string.Empty) + FormatCash(amount);
        }
    }
}

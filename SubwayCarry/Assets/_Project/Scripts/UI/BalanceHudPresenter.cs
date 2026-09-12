using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.UI
{
    [DisallowMultipleComponent]
    public sealed class BalanceHudPresenter : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour balanceProviderSource;
        [SerializeField, Min(0f)] private float resultDisplayDuration = 0.65f;

        private IBalanceStateProvider balanceProvider;
        private BalanceStateSnapshot state;
        private bool subscribed;
        private float resultVisibleUntil;
        private Texture2D whiteTexture;
        private Font font;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle instructionStyle;
        private GUIStyle detailStyle;

        public void Configure(MonoBehaviour providerSource)
        {
            Unsubscribe();
            balanceProviderSource = providerSource;
            balanceProvider = balanceProviderSource as IBalanceStateProvider;
            if (balanceProvider != null)
            {
                state = balanceProvider.CurrentBalanceState;
            }
            Subscribe();
        }

        private void Awake()
        {
            balanceProvider = balanceProviderSource as IBalanceStateProvider;
            if (balanceProvider != null)
            {
                state = balanceProvider.CurrentBalanceState;
            }

            whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Arial" },
                24);
        }

        private void OnEnable()
        {
            Subscribe();
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

        private void Subscribe()
        {
            if (!isActiveAndEnabled || subscribed || balanceProvider == null)
            {
                return;
            }

            balanceProvider.BalanceStateChanged += HandleBalanceStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || balanceProvider == null)
            {
                subscribed = false;
                return;
            }

            balanceProvider.BalanceStateChanged -= HandleBalanceStateChanged;
            subscribed = false;
        }

        private void HandleBalanceStateChanged(BalanceStateSnapshot snapshot)
        {
            state = snapshot;
            if (snapshot.Phase == BalanceChallengePhase.Succeeded ||
                snapshot.Phase == BalanceChallengePhase.Failed)
            {
                resultVisibleUntil = Time.unscaledTime + resultDisplayDuration;
            }
            else if (snapshot.Phase == BalanceChallengePhase.Cancelled)
            {
                resultVisibleUntil = 0f;
            }
        }

        private void OnGUI()
        {
            bool showResult =
                (state.Phase == BalanceChallengePhase.Succeeded ||
                 state.Phase == BalanceChallengePhase.Failed) &&
                Time.unscaledTime <= resultVisibleUntil;
            if (!state.IsActive && !showResult)
            {
                return;
            }

            EnsureStyles();
            float width = Mathf.Min(500f, Screen.width - 32f);
            float height = 176f;
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                Mathf.Max(22f, Screen.height * 0.16f),
                width,
                height);
            GUI.Box(panel, GUIContent.none, panelStyle);

            if (showResult)
            {
                DrawResult(panel);
                return;
            }

            GUI.Label(
                new Rect(panel.x + 18f, panel.y + 10f, panel.width - 36f, 30f),
                state.Phase == BalanceChallengePhase.Warning
                    ? "열차가 흔들립니다"
                    : GetPatternTitle(state.Pattern),
                titleStyle);
            GUI.Label(
                new Rect(panel.x + 18f, panel.y + 41f, panel.width - 36f, 34f),
                GetInstruction(state),
                instructionStyle);

            Rect gauge = new Rect(
                panel.x + 30f,
                panel.y + 86f,
                panel.width - 60f,
                28f);
            DrawPatternGauge(gauge);

            GUI.Label(
                new Rect(panel.x + 18f, panel.y + 124f, panel.width - 36f, 28f),
                GetDetail(state),
                detailStyle);
        }

        private void DrawResult(Rect panel)
        {
            bool success = state.Phase == BalanceChallengePhase.Succeeded;
            Color previous = GUI.color;
            GUI.color = success
                ? new Color(0.2f, 0.8f, 0.45f, 0.3f)
                : new Color(0.95f, 0.2f, 0.2f, 0.32f);
            GUI.DrawTexture(panel, whiteTexture);
            GUI.color = previous;
            GUI.Label(
                new Rect(panel.x + 18f, panel.y + 54f, panel.width - 36f, 58f),
                success ? "중심을 잡았습니다" : "중심을 잃었습니다",
                titleStyle);
        }

        private void DrawPatternGauge(Rect gauge)
        {
            DrawSolid(gauge, new Color(0.08f, 0.1f, 0.14f, 0.95f));

            if (state.Pattern == BalanceChallengePattern.CounterTap)
            {
                Rect fill = gauge;
                fill.width *= state.Progress;
                DrawSolid(fill, new Color(0.2f, 0.78f, 0.9f, 0.95f));
                return;
            }

            float targetCenter = Mathf.Lerp(
                gauge.x,
                gauge.xMax,
                Mathf.InverseLerp(-1f, 1f, state.TargetValue));
            float zoneHalfWidth = gauge.width * state.SafeZoneHalfWidth * 0.5f;
            Rect safeZone = new Rect(
                targetCenter - zoneHalfWidth,
                gauge.y + 3f,
                zoneHalfWidth * 2f,
                gauge.height - 6f);
            DrawSolid(safeZone, new Color(0.18f, 0.7f, 0.38f, 0.8f));

            float pointerX = Mathf.Lerp(
                gauge.x,
                gauge.xMax,
                Mathf.InverseLerp(-1f, 1f, state.IndicatorValue));
            Rect pointer = new Rect(
                pointerX - 3f,
                gauge.y - 5f,
                6f,
                gauge.height + 10f);
            DrawSolid(pointer, new Color(1f, 0.86f, 0.25f, 1f));
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
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = Color.white;
            instructionStyle = new GUIStyle(titleStyle)
            {
                fontSize = 21
            };
            detailStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter
            };
            detailStyle.normal.textColor = new Color(0.86f, 0.9f, 0.96f);
        }

        private static string GetPatternTitle(BalanceChallengePattern pattern)
        {
            switch (pattern)
            {
                case BalanceChallengePattern.CounterTap:
                    return "반대 방향으로 버티기";
                case BalanceChallengePattern.CenterGauge:
                    return "중앙에 중심 유지";
                case BalanceChallengePattern.ImpactTiming:
                    return "충격에 맞춰 버티기";
                default:
                    return "중심잡기";
            }
        }

        private static string GetInstruction(BalanceStateSnapshot snapshot)
        {
            string key = GetKeyLabel(snapshot.RequiredInput);
            switch (snapshot.Pattern)
            {
                case BalanceChallengePattern.CounterTap:
                    return key + " 연타!";
                case BalanceChallengePattern.CenterGauge:
                    return snapshot.InputAxis == BalanceInputAxis.Horizontal
                        ? "A / D로 표시를 중앙에 유지"
                        : "W / S로 표시를 중앙에 유지";
                case BalanceChallengePattern.ImpactTiming:
                    return key + " · 노란 표시가 초록 구간에 들어오면 입력";
                default:
                    return key;
            }
        }

        private static string GetDetail(BalanceStateSnapshot snapshot)
        {
            if (snapshot.Phase == BalanceChallengePhase.Warning)
            {
                return "준비  " + snapshot.RemainingSeconds.ToString("0.0") + "초";
            }

            if (snapshot.Pattern == BalanceChallengePattern.CounterTap)
            {
                return "게이지 " + Mathf.RoundToInt(snapshot.Progress * 100f) +
                       "%   ·   남은 시간 " +
                       snapshot.RemainingSeconds.ToString("0.0") + "초";
            }

            return "남은 시간 " +
                   snapshot.RemainingSeconds.ToString("0.0") + "초";
        }

        private static string GetKeyLabel(BalanceInputDirection direction)
        {
            switch (direction)
            {
                case BalanceInputDirection.Up:
                    return "W";
                case BalanceInputDirection.Left:
                    return "A";
                case BalanceInputDirection.Down:
                    return "S";
                case BalanceInputDirection.Right:
                    return "D";
                default:
                    return "";
            }
        }
    }
}

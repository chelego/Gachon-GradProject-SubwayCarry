using SubwayCarry.Core.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // Original modern card HUD and portrait phone; no reference-game artwork redistributed.
    public sealed class SlicePixelHud : MonoBehaviour
    {
        SliceJourneyController flow;
        SliceDamageFeedback feedback;
        GUIStyle text, small, title, big, center;
        readonly SliceMetroMap metro = new SliceMetroMap();
        int preview;
        bool mapTab = true, help, services;
        bool wasMapOpen;
        Vector2 scroll;
        string notice;
        float noticeUntil;
        static readonly Color Ink = new Color32(18, 29, 38, 255), Card = new Color32(30, 46, 56, 246);
        static readonly Color Paper = new Color32(241, 246, 242, 255), Muted = new Color32(151, 175, 180, 255);
        static readonly Color Mint = new Color32(96, 226, 188, 255), Gold = new Color32(246, 194, 91, 255), Red = new Color32(246, 123, 112, 255);
        public void Initialize(SliceJourneyController journey) { flow = journey; feedback = journey.GetComponent<SliceDamageFeedback>(); }
        void OnDisable() { metro.ReleaseRoute(); }
        void Update()
        {
            if (flow == null) return;
            if (flow.MapOpen && !wasMapOpen)
            {
                if (flow.SelectedIndex >= 0) preview = flow.SelectedIndex;
                scroll.y = Mathf.Max(0, (flow.Catalog.orders.Length - flow.UnlockedCount) * 70 - 140);
            }
            if (wasMapOpen && !flow.MapOpen) metro.ReleaseRoute();
            wasMapOpen = flow.MapOpen;
            if (notice != flow.Notice) { notice = flow.Notice; noticeUntil = Time.unscaledTime + 3.5f; }
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) help = !help;
        }
        void Styles()
        {
            if (text != null) return;
            text = new GUIStyle(GUI.skin.label) { font = flow.Catalog.uiFont, fontSize = 19, wordWrap = true, normal = { textColor = Paper } };
            small = new GUIStyle(text) { fontSize = 15, normal = { textColor = Muted } };
            title = new GUIStyle(text) { fontSize = 24, fontStyle = FontStyle.Bold };
            big = new GUIStyle(title) { fontSize = 30 };
            center = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
        }
        static void Fill(Rect r, Color c) => GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, c, 0, 0);
        static void Panel(Rect r, Color c, float radius = 10) => GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, c, 0, radius);
        void Label(float x, float y, float w, string value, GUIStyle style = null, float h = 34) => GUI.Label(new Rect(x, y, w, h), value, style ?? text);
        bool Button(Rect r, string value, bool active = false, bool enabled = true)
        {
            bool hover = r.Contains(Event.current.mousePosition) && enabled && GUI.enabled;
            Panel(r, !enabled ? Ink : active ? Mint : hover ? new Color32(56, 81, 90, 255) : Card, 8);
            Color old = GUI.color; GUI.color = !enabled ? Muted : active ? Ink : Paper;
            GUI.Label(r, value, center); GUI.color = old;
            bool was = GUI.enabled; GUI.enabled &= enabled;
            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none); GUI.enabled = was; return clicked;
        }
        void OnGUI()
        {
            if (flow == null) return;
            Styles(); var old = GUI.matrix; int depth = GUI.depth;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            GUI.depth = -20;
            bool previousEnabled = GUI.enabled;
            GUI.enabled &= !flow.Paused && (feedback == null || !feedback.CutInVisible);
            if (!flow.MapOpen && !flow.IsResult) DrawHud();
            if (flow.MapOpen) DrawPhone();
            if (flow.IsResult) DrawResult();
            GUI.enabled = previousEnabled;
            if (flow.Paused) DrawPause();
            if (help && !flow.Paused) DrawHelp();
            if (feedback != null && feedback.CutInVisible) DrawDamage();
            GUI.depth = depth; GUI.matrix = old;
        }
        string Goal()
        {
            if (flow.Phase == DeliveryPhase.None) return "첫 배송을 골라보자";
            if (flow.Phase == DeliveryPhase.Selected) return "교통카드 찍기";
            if (flow.Phase == DeliveryPhase.Arrived) return "출구로 나가기";
            if (flow.Phase == DeliveryPhase.Transferring) return "다음 노선으로 환승";
            if (flow.OnTrain) return flow.IsRequiredStop ? "이번 역에서 내리기" : "케이크를 안전하게 운반하기";
            if (flow.World.CurrentMap.hasFareGate) return "승강장으로 이동";
            return flow.World.DoorsOpen ? "열차에 탑승하기" : "열차 기다리기";
        }
        void DrawHud()
        {
            Panel(new Rect(24, 24, 318, 91), Card); Fill(new Rect(24, 40, 3, 56), Mint);
            Label(44, 36, 278, flow.Order == null ? "가천대역" : flow.Order.stops[flow.Order.stops.Length - 1].label + " 배송", title);
            Label(44, 75, 278, Goal(), small);
            Panel(new Rect(1104, 24, 152, 44), Card);
            Label(1115, 29, 130, "₩ " + flow.Economy.CurrentEconomyState.CurrentCash.ToString("N0"), center);
            if (flow.Phase != DeliveryPhase.None)
            {
                var d = flow.World.Package.CurrentDurability;
                Panel(new Rect(1010, 80, 246, 97), Card);
                DrawItem(new Rect(1024, 92, 58, 50), false, SliceDamageFeedback.DamageStage(d.CakeDurability));
                Label(1087, 94, 150, "케이크 " + d.CakeDurability.ToString("0") + "%");
                Bar(new Rect(1088, 125, 145, 5), d.CakeDurability / 100, d.CakeDurability >= 40 ? Mint : Red);
                Label(1026, 148, 205, "상자 보호 " + d.BoxDurability.ToString("0") + "%", small);
            }
            float stamina = flow.World.Posture.StaminaRatio;
            if (stamina < .995f)
            { Panel(new Rect(1040, 191, 216, 39), Card); Label(1055, 196, 60, "체력", small); Bar(new Rect(1106, 209, 132, 5), stamina, Gold); }
            if (Button(new Rect(24, 652, 128, 44), "배달 앱  TAB")) flow.ToggleMap();
            if (Button(new Rect(166, 652, 70, 44), "?")) help = !help;
            if (noticeUntil > Time.unscaledTime && !string.IsNullOrEmpty(notice))
            {
                Panel(new Rect(355, 634, 570, 50), Card);
                Label(373, 644, 534, notice.Length > 53 ? notice.Substring(0, 52) + "…" : notice, center);
            }
            DrawInteraction(); DrawBalance();
            if (flow.Phase == DeliveryPhase.Transferring && flow.World.CurrentMapIndex == 5)
            {
                if (Button(new Rect(1030, 538, 226, 44), "능력 강화")) services = !services;
                if (services)
                {
                    if (Button(new Rect(1000, 356, 256, 46), "체력 +  ₩" + flow.UpgradePrice(flow.StaminaLevel))) flow.BuyUpgrade(0);
                    if (Button(new Rect(1000, 412, 256, 46), "균형 +  ₩" + flow.UpgradePrice(flow.BalanceLevel))) flow.BuyUpgrade(1);
                    if (Button(new Rect(1000, 468, 256, 46), "민첩 +  ₩" + flow.UpgradePrice(flow.AgilityLevel))) flow.BuyUpgrade(2);
                }
            }
        }
        void DrawInteraction()
        {
            if (!flow.TryGetInteractionHint(out Vector2 position, out string caption, out string key)) return;
            Vector3 s = flow.World.gameplayCamera.WorldToScreenPoint(position + Vector2.up * .8f);
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            float x = Mathf.Clamp((s.x - (Screen.width - 1280 * scale) / 2) / scale - 92, 20, 1060);
            float y = Mathf.Clamp((Screen.height - s.y - (Screen.height - 720 * scale) / 2) / scale - 40, 130, 565);
            Panel(new Rect(x, y, 200, 42), Card); Panel(new Rect(x + 7, y + 7, 30, 28), Mint, 5);
            Color old = GUI.color; GUI.color = Ink; Label(x + 7, y + 7, 30, key, center, 28); GUI.color = old;
            Label(x + 45, y + 8, 148, caption);
        }
        void DrawBalance()
        {
            var b = flow.World.Balance.CurrentBalanceState; if (!b.IsActive) return;
            Panel(new Rect(431, 516, 418, 94), Card);
            string key = flow.World.Balance.CurrentPromptKey.ToString();
            string hint = b.Pattern == BalanceChallengePattern.CenterGauge ? (b.InputAxis == BalanceInputAxis.Horizontal ? "A · D  중심 잡기" : "W · S  중심 잡기") : b.Pattern == BalanceChallengePattern.ImpactTiming ? key + "  타이밍 맞추기" : key + "  연타해서 버티기";
            Label(451, 528, 378, hint, center);
            var r = new Rect(452, 575, 376, 12); Panel(r, Ink, 4);
            if (b.Pattern == BalanceChallengePattern.CounterTap) Bar(r, b.Progress, Gold);
            else
            {
                float lo = Mathf.Clamp01((b.TargetValue - b.SafeZoneHalfWidth + 1) / 2), hi = Mathf.Clamp01((b.TargetValue + b.SafeZoneHalfWidth + 1) / 2);
                Fill(new Rect(r.x + r.width * lo, r.y, r.width * (hi - lo), r.height), Mint);
                Fill(new Rect(r.x + r.width * Mathf.Clamp01((b.IndicatorValue + 1) / 2) - 2, r.y - 4, 4, r.height + 8), Paper);
            }
        }
        static void Bar(Rect r, float value, Color c) { Panel(r, Ink, 3); if (value > 0) Panel(new Rect(r.x, r.y, r.width * Mathf.Clamp01(value), r.height), c, 3); }
        void DrawPhone()
        {
            Fill(new Rect(0, 0, 1280, 720), new Color(.025f, .05f, .07f, .64f));
            Panel(new Rect(380, 18, 520, 686), new Color(0, 0, 0, .4f), 30);
            Panel(new Rect(372, 10, 520, 688), new Color32(9, 15, 23, 255), 28);
            Panel(new Rect(382, 20, 500, 668), new Color32(24, 39, 49, 255), 20);
            Label(405, 29, 100, "13:00", small);
            Panel(new Rect(587, 27, 90, 17), new Color32(9, 15, 23, 255), 9);
            Label(796, 30, 60, "● ▰", small);
            Label(407, 62, 355, "캠퍼스 딜리버리", title);
            if (Button(new Rect(816, 58, 42, 35), "×")) flow.ToggleMap();
            if (Button(new Rect(405, 110, 219, 39), "노선도", mapTab)) { mapTab = true; services = false; }
            if (Button(new Rect(638, 110, 219, 39), "배송 목록", !mapTab)) { mapTab = false; services = false; }
            preview = Mathf.Clamp(preview, 0, flow.Catalog.orders.Length - 1);
            var order = PreviewOrder();
            var content = new Rect(400, 164, 464, 333); Panel(content, Ink);
            if (services) DrawServices();
            else if (mapTab)
            {
                SliceRouteHighlight highlight = null;
                if (flow.Catalog.routeHighlights != null)
                    foreach (var candidate in flow.Catalog.routeHighlights)
                        if (candidate != null && candidate.orderId == order.id) { highlight = candidate; break; }
                metro.Draw(new Rect(405, 169, 454, 323), flow.Catalog.metroMapImage, highlight, small);
            }
            else
            {
                scroll = GUI.BeginScrollView(content, scroll, new Rect(0, 0, 440, flow.Catalog.orders.Length * 70), false, false);
                for (int row = 0; row < flow.Catalog.orders.Length; row++)
                {
                    int i = flow.Catalog.orders.Length - 1 - row; var item = flow.Catalog.orders[i]; bool unlocked = i < flow.UnlockedCount;
                    var r = new Rect(6, row * 70 + 5, 427, 62);
                    if (Button(r, "", i == preview)) { preview = i; mapTab = true; }
                    Color old = GUI.color; GUI.color = i == preview ? Ink : unlocked ? Paper : Muted;
                    Label(20, r.y + 7, 275, item.stops[item.stops.Length - 1].label);
                    Label(20, r.y + 36, 295, unlocked ? "보수 ₩" + item.fee.ToString("N0") : "이전 배송 완료 시 열림", small, 23);
                    Label(313, r.y + 20, 110, new string('★', item.stars), small); GUI.color = old;
                }
                GUI.EndScrollView();
            }
            order = PreviewOrder();
            Label(409, 508, 445, "가천대 → " + order.stops[order.stops.Length - 1].label, text, 36);
            Label(409, 552, 445, "보수 ₩" + order.fee.ToString("N0") + "     교통비 ₩" + order.outboundFare.ToString("N0"));
            Label(409, 585, 445, new string('★', order.stars) + "  ·  " + order.hour + "시  ·  케이크 ₩" + order.value.ToString("N0"), small);
            if (Button(new Rect(405, 622, 296, 44), flow.Phase == DeliveryPhase.None ? "이 배송 수락" : "배송 중", true, preview < flow.UnlockedCount && flow.Phase == DeliveryPhase.None)) flow.Select(preview);
            if (Button(new Rect(714, 622, 143, 44), services ? "지도" : "학교 지원")) { services = !services; mapTab = true; }
            Panel(new Rect(582, 677, 100, 4), Muted, 2);
        }
        SliceDeliveryOrder PreviewOrder() => flow.Order != null && preview == flow.SelectedIndex ? flow.Order : flow.Catalog.orders[preview];
        void DrawServices()
        {
            Label(426, 193, 395, "학교 지원", title);
            Label(426, 235, 395, "보험: 손상 배상금 지원\n교통지원: 교통비 1회 지원", small, 58);
            if (Button(new Rect(426, 310, 398, 55), "보험 구매  ₩" + flow.Catalog.insurancePrice)) flow.BuyService(true);
            if (Button(new Rect(426, 383, 398, 55), "교통지원 구매  ₩" + flow.Catalog.fareSupportPrice)) flow.BuyService(false);
            Label(426, 454, 398, "보유  보험 " + flow.Insurance + "  /  교통지원 " + flow.FareSupport, small);
        }
        void DrawResult()
        {
            Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, .65f)); Panel(new Rect(405, 105, 470, 510), Card, 18);
            Label(435, 135, 410, flow.Phase == DeliveryPhase.Completed ? "배송 완료!" : "배송 실패", big, 46);
            var s = flow.LastSettlement;
            Label(435, 213, 410, "보수                 +" + s.DeliveryFee.ToString("N0") + "원\n배상금             -" + s.Compensation.ToString("N0") + "원\n교통비             -" + (s.OutboundFare + s.ReturnFare).ToString("N0") + "원", text, 156);
            Label(435, 368, 410, "합계  " + s.NetIncome.ToString("+#,0;-#,0;0") + "원", big, 44);
            Label(435, 430, 410, flow.ServiceReceipt + (flow.Failure == DeliveryFailureReason.Bankrupt ? "파산 · 진행이 초기화됐어요." : flow.ScholarshipAwarded ? "전액 장학금 달성!" : ""), small, 80);
            if (Button(new Rect(435, 529, 410, 54), "가천대역으로", true)) flow.ReturnToHub();
        }
        void DrawPause()
        {
            Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, .55f)); Panel(new Rect(460, 223, 360, 264), Card);
            Label(490, 250, 300, "잠시 쉬어가기", title);
            if (Button(new Rect(490, 314, 300, 48), "저장")) flow.SaveProgress();
            if (Button(new Rect(490, 382, 300, 48), "계속하기", true)) flow.TogglePause();
        }
        void DrawHelp()
        {
            Panel(new Rect(28, 305, 306, 324), Card); Label(49, 322, 264, "조작법", title);
            Label(49, 374, 264, "WASD  이동\n마우스  바라보기\nE  이용하기\n1 서기    2 기대기\n3 앉기    4 잡기\n5 머리 위로 들기\nTAB 배달 앱  ·  ESC 쉬기", text, 240);
        }
        void DrawDamage()
        {
            float elapsed = feedback.CutInProgress * Mathf.Max(2.5f, flow.Catalog.damageCutInSeconds);
            float remaining = (1 - feedback.CutInProgress) * Mathf.Max(2.5f, flow.Catalog.damageCutInSeconds);
            float alpha = Mathf.Clamp01(remaining / .3f);
            float t = Mathf.Clamp01(elapsed / .26f) - 1;
            float pop = 1 + 2.4f * t * t * t + 1.4f * t * t;
            Fill(new Rect(0, 0, 1280, 720), new Color(.025f, .035f, .045f, .38f * alpha));
            Matrix4x4 savedMatrix = GUI.matrix; Color savedColor = GUI.color;
            GUI.matrix *= Matrix4x4.TRS(new Vector3(640, 355, 0), Quaternion.identity, Vector3.one * Mathf.Max(.01f, pop))
                * Matrix4x4.Translate(new Vector3(-640, -355, 0));
            GUI.color = new Color(1, 1, 1, alpha);
            if (flow.Catalog.damageBurst != null)
                GUI.DrawTexture(new Rect(260, 90, 760, 540), flow.Catalog.damageBurst, ScaleMode.ScaleToFit, true);
            var d = feedback.DisplayDurability;
            // The illustration IS the message. No sentence, caption or dismiss button obscures it.
            bool showCake = feedback.CakeLoss > 0;
            DrawItem(new Rect(455, 174, 370, 370), !showCake,
                SliceDamageFeedback.DamageStage(showCake ? d.CakeDurability : d.BoxDurability));
            GUI.color = savedColor; GUI.matrix = savedMatrix;
        }
        void DrawItem(Rect rect, bool box, int stage)
        {
            if (flow.Catalog.damageAtlas != null)
            {
                float size = Mathf.Min(rect.width, rect.height);
                rect = new Rect(rect.center.x - size * .5f, rect.center.y - size * .5f, size, size);
                GUI.DrawTextureWithTexCoords(rect, flow.Catalog.damageAtlas, new Rect(Mathf.Clamp(stage, 0, 3) * .25f, box ? .5f : 0, .25f, .5f), true);
            }
        }
    }
}

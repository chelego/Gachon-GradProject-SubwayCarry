using SubwayCarry.Core.Contracts;
using SubwayCarry.Gameplay;
using UnityEngine;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // Presentation only: the existing package receiver remains the sole authority for damage.
    public sealed class SliceDamageFeedback : MonoBehaviour
    {
        SliceJourneyController flow;
        PackageDurabilitySnapshot previous;
        float endsAt, startedAt, nextCutIn, lastRecoil;
        readonly Collider2D[] contacts = new Collider2D[12];
        public PackageDurabilitySnapshot DisplayDurability { get; private set; }
        public float CakeLoss { get; private set; }
        public bool CutInVisible => flow != null && endsAt > Time.unscaledTime;
        public float CutInProgress => Mathf.Clamp01((Time.unscaledTime - startedAt) / Mathf.Max(.1f, endsAt - startedAt));
        public static int DamageStage(float health) => SlicePresentationRules.DamageStage(health);
        public static bool ShouldCutIn(float oldBox, float oldCake, float box, float cake, float threshold)
            => SlicePresentationRules.ShouldCutIn(oldBox, oldCake, box, cake, threshold);
        public void Initialize(SliceJourneyController journey)
        {
            flow = journey; previous = flow.World.Package.CurrentDurability;
            flow.World.Package.DurabilityChanged += Changed;
            var reaction = flow.World.Player.AddComponent<SlicePlayerPresentation>(); reaction.Initialize(flow);
        }
        void Changed(PackageDurabilitySnapshot value)
        {
            var before = previous; previous = value;
            float loss = Mathf.Max(0, before.BoxDurability - value.BoxDurability) + Mathf.Max(0, before.CakeDurability - value.CakeDurability);
            if (loss <= 0 || flow.Phase == DeliveryPhase.None || flow.World.IsChangingMap) return;
            var pose = flow.World.Player.GetComponent<SlicePlayerPresentation>();
            if (pose != null) pose.React(Mathf.Clamp01(loss / 35));
            if (Time.time - lastRecoil > .18f && flow.World.Posture.CanMove)
            {
                lastRecoil = Time.time;
                Vector2 player = flow.World.PlayerPosition, away = -flow.World.Player.GetComponent<PlayerController>().FacingDirection;
                var filter = new ContactFilter2D { useTriggers = false, useLayerMask = false };
                int count = Physics2D.OverlapCircle(player, 1.1f, filter, contacts);
                float best = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    var c = contacts[i];
                    if (c == null || c.transform.IsChildOf(flow.World.Player.transform) || c.attachedRigidbody == null) continue;
                    float d = ((Vector2)c.bounds.center - player).sqrMagnitude;
                    if (d < best && d > .001f) { best = d; away = (player - (Vector2)c.bounds.center).normalized; }
                }
                flow.World.Player.GetComponent<SlicePlayerBoundary>().AddImpulse(away * Mathf.Clamp(loss * .075f, .25f, 2.3f) * flow.Catalog.recoilScale);
            }
            bool milestone = DamageStage(value.BoxDurability) > DamageStage(before.BoxDurability) || DamageStage(value.CakeDurability) > DamageStage(before.CakeDurability);
            if (!ShouldCutIn(before.BoxDurability, before.CakeDurability, value.BoxDurability, value.CakeDurability, flow.Catalog.damageCutInThreshold) || (!milestone && Time.unscaledTime < nextCutIn)) return;
            DisplayDurability = value; CakeLoss = Mathf.Max(0, before.CakeDurability - value.CakeDurability);
            startedAt = Time.unscaledTime; endsAt = startedAt + Mathf.Max(2.5f, flow.Catalog.damageCutInSeconds);
            nextCutIn = endsAt + flow.Catalog.damageCutInCooldown;
            flow.SetPresentationPaused(true);
        }
        void Update() { if (flow != null && flow.PresentationPaused && !CutInVisible) flow.SetPresentationPaused(false); }
        void OnDestroy() { if (flow != null && flow.World != null && flow.World.Package != null) flow.World.Package.DurabilityChanged -= Changed; }
    }
    [DefaultExecutionOrder(550)]
    public sealed class SlicePlayerPresentation : MonoBehaviour
    {
        SliceJourneyController flow;
        Transform body, package, legs;
        Vector3 bodyBase, packageBase;
        float reaction, flash, turn;
        SpriteRenderer bodyRenderer;
        Color baseColor;
        public void Initialize(SliceJourneyController journey)
        {
            flow = journey; body = transform.Find("BodyVisual"); package = transform.Find("PackageVisual");
            bodyBase = body.localPosition; if (package != null) packageBase = package.localPosition;
            bodyRenderer = body.GetComponent<SpriteRenderer>(); baseColor = bodyRenderer.color;
        }
        public void React(float strength) { reaction = Mathf.Max(reaction, strength); flash = .16f; }
        void OnCollisionEnter2D(Collision2D collision)
        {
            if (flow == null || flow.InputBlocked || flow.World.IsChangingMap || collision.rigidbody == null) return;
            float speed = collision.relativeVelocity.magnitude; if (speed < .3f) return;
            Vector2 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector2.zero;
            GetComponent<SlicePlayerBoundary>().AddImpulse(normal * Mathf.Min(2.4f, speed * .45f) * flow.Catalog.recoilScale);
            React(Mathf.Clamp01(speed / 4));
        }
        void LateUpdate()
        {
            if (flow == null || body == null) return;
            if (package == null) { package = transform.Find("PackageVisual"); if (package != null) packageBase = package.localPosition; }
            if (legs == null) legs = transform.Find("Locomotion_Legs");
            var posture = flow.World.Posture.CurrentState;
            float targetTurn = posture == CarryPosture.Leaning ? -7 : posture == CarryPosture.HoldingSupport ? 3 : 0;
            turn = Mathf.Lerp(turn, targetTurn, 1 - Mathf.Exp(-12 * Time.deltaTime));
            reaction = Mathf.MoveTowards(reaction, 0, Time.deltaTime * 2.8f); flash -= Time.deltaTime;
            float kick = Mathf.Sin(reaction * Mathf.PI) * .08f;
            Vector3 offset = new Vector3(-kick, kick * .4f, 0);
            body.localPosition = bodyBase + offset;
            body.localRotation = Quaternion.Euler(0, 0, posture == CarryPosture.Fallen ? 0 : turn + reaction * 7);
            bodyRenderer.color = flash > 0 ? Color.Lerp(baseColor, new Color(1, .55f, .45f), .4f) : baseColor;
            if (package != null)
            {
                float lift = posture == CarryPosture.OverheadCarry ? .65f : 0;
                Vector3 target = packageBase + offset + Vector3.up * lift;
                package.localPosition = Vector3.Lerp(package.localPosition, target, 1 - Mathf.Exp(-14 * Time.deltaTime));
                package.localRotation = body.localRotation;
            }
            if (legs != null) { legs.localPosition = body.localPosition; legs.localRotation = body.localRotation; }
        }
    }
}

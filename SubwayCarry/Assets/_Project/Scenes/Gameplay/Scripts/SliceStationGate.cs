using UnityEngine;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    public sealed class SliceStationGate : MonoBehaviour
    {
        SliceMap map;
        SliceJourneyController journey;
        Sprite pixel;
        Transform leaf;
        public void Initialize(SliceMap source)
        {
            map = source;
            var art = SliceStationArt.Get(transform.parent);
            pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f, Texture2D.whiteTexture.width);
            for (int v = 0; v <= 6; v += 2)
            {
                var p = SliceTrainLayout.Project(6, v);
                art.Draw(transform, 2, "Fare reader pedestal", p - Vector2.up * .24f, 1.15f, map.GroundOrder(p));
            }
            leaf = Part("Retracting fare flap", SliceTrainLayout.Project(6, 3) + Vector2.up * .28f, new Vector2(1.85f, .48f), new Color32(231, 148, 51, 225)).transform;
            leaf.localRotation = Quaternion.Euler(0, 0, -26.565f);
        }
        public void Bind(SliceJourneyController source) { journey = source; }
        SpriteRenderer Part(string name, Vector2 p, Vector2 size, Color color)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false); go.transform.position = p; go.transform.localScale = size;
            var r = go.AddComponent<SpriteRenderer>(); r.sprite = pixel; r.color = color; r.sortingOrder = map.GroundOrder(p - Vector2.up * .4f); return r;
        }
        void Update()
        {
            if (journey == null || leaf == null) return;
            float target = journey.GateOpen ? .02f : 1.85f;
            Vector3 scale = leaf.localScale; scale.x = Mathf.MoveTowards(scale.x, target, Time.deltaTime * 7); leaf.localScale = scale;
        }
        void OnDestroy() { if (pixel != null) Destroy(pixel); }
    }
}

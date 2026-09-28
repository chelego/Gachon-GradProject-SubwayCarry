using UnityEngine;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // One shared atlas slice cache per gameplay root; never read pixels during a frame update.
    public sealed class SliceStationArt : MonoBehaviour
    {
        readonly Sprite[] pieces = new Sprite[8];
        public static SliceStationArt Get(Transform root)
        {
            var art = root.GetComponent<SliceStationArt>();
            if (art != null) return art;
            art = root.gameObject.AddComponent<SliceStationArt>();
            var world = root.GetComponent<SliceGameController>();
            if (world != null && world.deliveryCatalog != null) art.Slice(world.deliveryCatalog.stationArtAtlas);
            return art;
        }
        void Slice(Texture2D atlas)
        {
            if (atlas == null) return;
            var pixels = atlas.GetPixels32();
            for (int i = 0; i < pieces.Length; i++)
            {
                int x0 = atlas.width * (i % 4) / 4, x1 = atlas.width * (i % 4 + 1) / 4;
                int y0 = atlas.height * (1 - i / 4) / 2, y1 = atlas.height * (2 - i / 4) / 2;
                int left = x1, right = x0, bottom = y1, top = y0;
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
                    if (pixels[y * atlas.width + x].a > 40)
                    { left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); }
                if (right <= left || top <= bottom) continue;
                pieces[i] = Sprite.Create(atlas, new Rect(left, bottom, right - left + 1, top - bottom + 1), new Vector2(.5f, 0), 100, 0, SpriteMeshType.FullRect);
                pieces[i].name = "Concourse part " + i;
            }
        }
        public SpriteRenderer Draw(Transform parent, int piece, string name, Vector2 ground, float width, int order, bool flip = false)
        {
            if (piece < 0 || piece >= pieces.Length || pieces[piece] == null) return null;
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = ground;
            go.transform.localScale = Vector3.one * (width / pieces[piece].bounds.size.x);
            var r = go.AddComponent<SpriteRenderer>(); r.sprite = pieces[piece]; r.flipX = flip; r.sortingOrder = order;
            return r;
        }
        public SpriteRenderer GroundWall(Transform parent, int piece, Vector2 left, Vector2 right, int order)
        {
            var r = Draw(parent, piece, "Tiled station wall", Vector2.zero, right.x - left.x, order);
            if (r == null) return null;
            // Measured ground corners of the two atlas wall views, not the lowest pixel of the whole image.
            Vector2 a = piece == 0 ? new Vector2(.06f, .025f) : new Vector2(.015f, .425f);
            Vector2 b = piece == 0 ? new Vector2(.985f, .44f) : new Vector2(.92f, .02f);
            Vector2 sourceA = Point(r, a), sourceB = Point(r, b);
            Vector2 source = sourceB - sourceA, target = right - left;
            r.transform.localScale *= target.magnitude / source.magnitude;
            r.transform.rotation = Quaternion.Euler(0, 0, Vector2.SignedAngle(source, target));
            r.transform.position += (Vector3)(left - Point(r, a));
            return r;
        }
        public static Vector2 Point(SpriteRenderer r, Vector2 normalized)
        {
            Bounds b = r.sprite.bounds;
            Vector2 p = new Vector2(Mathf.Lerp(b.min.x, b.max.x, normalized.x), Mathf.Lerp(b.min.y, b.max.y, normalized.y));
            if (r.flipX) p.x = -p.x;
            if (r.flipY) p.y = -p.y;
            return r.transform.TransformPoint(p);
        }
        void OnDestroy() { foreach (var sprite in pieces) if (sprite != null) Destroy(sprite); }
    }
}

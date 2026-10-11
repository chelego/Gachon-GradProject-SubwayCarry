using UnityEngine;

namespace SubwayCarry.Transit
{
    /// <summary>
    /// Paired sliding leaves for P(u, v, h) = (u + .75v, .5u - .375v + h).
    /// The door root uses the same unrotated XY coordinate system as the car.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class SubwayQuarterDoor : MonoBehaviour
    {
        [SerializeField] private Transform leftLeaf;
        [SerializeField] private Transform rightLeaf;
        [SerializeField, Min(0.01f)] private float slideLength = 1.27f;
        [SerializeField, Min(0.01f)] private float transitionDuration = 0.65f;

        private Rigidbody2D leftBody;
        private Rigidbody2D rightBody;
        private Vector3 leftClosedPosition;
        private Vector3 rightClosedPosition;
        private float openness;
        private float targetOpenness;
        private bool initialized;
        private MeshRenderer[] clippedRenderers;
        private MaterialPropertyBlock clipping;

        public float Openness => openness;
        public bool IsOpen => openness >= 0.999f;
        public bool IsClosed => openness <= 0.001f;

        public void Configure(Transform left, Transform right, float length = 1.27f)
        {
            leftLeaf = left;
            rightLeaf = right;
            slideLength = Mathf.Max(0.01f, length);
            PrepareLeaf(leftLeaf);
            PrepareLeaf(rightLeaf);
            CacheClosedPositions();
            CacheRenderers();
            ApplyClipping();
        }

        public void SetOpen(bool open)
        {
            targetOpenness = open ? 1f : 0f;
        }

        private void Awake()
        {
            if (Application.IsPlaying(gameObject)) CacheClosedPositions();
        }

        private void OnEnable()
        {
            CacheRenderers();
            ApplyClipping();
        }

        private void CacheRenderers()
        {
            var renderers = new System.Collections.Generic.List<MeshRenderer>();
            if (leftLeaf != null) renderers.AddRange(leftLeaf.GetComponentsInChildren<MeshRenderer>(true));
            if (rightLeaf != null) renderers.AddRange(rightLeaf.GetComponentsInChildren<MeshRenderer>(true));
            clippedRenderers = renderers.ToArray();
        }

        private void LateUpdate() => ApplyClipping();

        private void ApplyClipping()
        {
            if (clippedRenderers == null) CacheRenderers();
            if (clipping == null) clipping = new MaterialPropertyBlock();
            foreach (var renderer in clippedRenderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(clipping);
                clipping.SetFloat("_ClipEnabled", 1f);
                clipping.SetVector("_ClipX", new Vector4(transform.position.x - slideLength,
                    transform.position.x + slideLength, 0, 0));
                renderer.SetPropertyBlock(clipping);
            }
        }

        private void CacheClosedPositions()
        {
            leftBody = leftLeaf != null ? leftLeaf.GetComponent<Rigidbody2D>() : null;
            rightBody = rightLeaf != null ? rightLeaf.GetComponent<Rigidbody2D>() : null;
            if (leftLeaf != null) leftClosedPosition = leftLeaf.localPosition;
            if (rightLeaf != null) rightClosedPosition = rightLeaf.localPosition;
            openness = 0f;
            targetOpenness = 0f;
            initialized = true;
        }

        private static Rigidbody2D PrepareLeaf(Transform leaf)
        {
            if (leaf == null) return null;
            Rigidbody2D body = leaf.GetComponent<Rigidbody2D>();
            if (body == null) body = leaf.gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            return body;
        }

        private void FixedUpdate()
        {
            if (!Application.IsPlaying(gameObject)) return;
            if (!initialized) CacheClosedPositions();
            if (leftLeaf == null || rightLeaf == null) return;

            openness = Mathf.MoveTowards(openness, targetOpenness,
                Time.fixedDeltaTime / Mathf.Max(0.01f, transitionDuration));

            // Do not normalize: one car-space unit projects to (1, .5),
            // so normalizing would shorten the authored opening.
            Vector3 slide = new Vector3(slideLength, slideLength * 0.5f, 0f) * openness;
            MoveLeaf(leftLeaf, leftBody, leftClosedPosition - slide);
            MoveLeaf(rightLeaf, rightBody, rightClosedPosition + slide);
        }

        private static void MoveLeaf(Transform leaf, Rigidbody2D body, Vector3 localPosition)
        {
            if (body == null)
            {
                leaf.localPosition = localPosition;
                return;
            }

            Vector3 worldPosition = leaf.parent != null
                ? leaf.parent.TransformPoint(localPosition)
                : localPosition;
            body.MovePosition(new Vector2(worldPosition.x, worldPosition.y));
        }
    }
}

using UnityEngine;

namespace SubwayCarry.Prototype.QuarterView
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class QuarterViewTestCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 deadZone = new Vector2(3.6f, 1.65f);
        [SerializeField, Min(0.01f)] private float smoothTime = 0.16f;
        [SerializeField] private Vector2 minimum = new Vector2(-7.2f, -3.1f);
        [SerializeField] private Vector2 maximum = new Vector2(7.2f, 3.15f);

        private Vector2 velocity;

        public void Configure(Transform followTarget, Vector2 min, Vector2 max)
        {
            target = followTarget;
            minimum = min;
            maximum = max;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector2 current = transform.position;
            Vector2 desired = current;
            Vector2 delta = (Vector2)target.position - current;

            if (Mathf.Abs(delta.x) > deadZone.x)
            {
                desired.x = target.position.x - Mathf.Sign(delta.x) * deadZone.x;
            }

            if (Mathf.Abs(delta.y) > deadZone.y)
            {
                desired.y = target.position.y - Mathf.Sign(delta.y) * deadZone.y;
            }

            desired.x = Mathf.Clamp(desired.x, minimum.x, maximum.x);
            desired.y = Mathf.Clamp(desired.y, minimum.y, maximum.y);
            Vector2 smoothed = Vector2.SmoothDamp(current, desired, ref velocity, smoothTime);
            transform.position = new Vector3(smoothed.x, smoothed.y, transform.position.z);
        }
    }
}

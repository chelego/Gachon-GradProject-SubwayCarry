using UnityEngine;

namespace SubwayCarry.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float followSpeed = 12f;
        [SerializeField] private float cameraZ = -10f;

        public void Configure(Transform followTarget)
        {
            target = followTarget;
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            Vector3 position = target.position;
            transform.position = new Vector3(position.x, position.y, cameraZ);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 targetPosition = new Vector3(
                target.position.x,
                target.position.y,
                cameraZ);
            float t = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);
        }
    }
}

using UnityEngine;

namespace SubwayCarry.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private PrototypeCameraZone currentZone;
        [SerializeField, Min(0f)] private float followSpeed = 12f;
        [SerializeField] private float cameraZ = -10f;

        private void Awake()
        {
            if (controlledCamera == null)
            {
                controlledCamera = GetComponent<Camera>();
            }
        }

        public void Configure(Transform followTarget)
        {
            target = followTarget;
            SnapToTarget();
        }

        public void EnterZone(PrototypeCameraZone zone)
        {
            currentZone = zone;
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            Vector2 desired = GetDesiredPosition();
            transform.position = new Vector3(desired.x, desired.y, cameraZ);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector2 desired = GetDesiredPosition();
            Vector3 targetPosition = new Vector3(desired.x, desired.y, cameraZ);
            float t = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);
        }

        private Vector2 GetDesiredPosition()
        {
            if (currentZone == null)
            {
                return target != null ? (Vector2)target.position : Vector2.zero;
            }

            float halfWidth = GetViewportHalfWidth();
            float zoneHalfWidth = currentZone.WorldSize.x * 0.5f;
            float minCameraX = currentZone.WorldCenter.x - zoneHalfWidth + halfWidth;
            float maxCameraX = currentZone.WorldCenter.x + zoneHalfWidth - halfWidth;
            float cameraX = minCameraX <= maxCameraX
                ? Mathf.Clamp(target.position.x, minCameraX, maxCameraX)
                : currentZone.WorldCenter.x;
            return new Vector2(cameraX, currentZone.WorldCenter.y);
        }

        private float GetViewportHalfWidth()
        {
            if (controlledCamera == null)
            {
                controlledCamera = GetComponent<Camera>();
            }

            return controlledCamera != null
                ? controlledCamera.orthographicSize * controlledCamera.aspect
                : 12f;
        }
    }
}

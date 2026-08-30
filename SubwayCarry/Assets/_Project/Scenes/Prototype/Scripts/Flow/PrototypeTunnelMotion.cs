using UnityEngine;

namespace SubwayCarry.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeTunnelMotion : MonoBehaviour
    {
        [SerializeField] private Transform[] movingStrips;
        [SerializeField, Min(1f)] private float wrapHalfWidth = 16f;
        [SerializeField, Min(0f)] private float speed = 7f;

        public void Configure(
            Transform[] strips,
            float horizontalHalfWidth,
            float movementSpeed)
        {
            movingStrips = strips;
            wrapHalfWidth = Mathf.Max(1f, horizontalHalfWidth);
            speed = Mathf.Max(0f, movementSpeed);
        }

        private void Update()
        {
            if (movingStrips == null || movingStrips.Length == 0 || speed <= 0f)
            {
                return;
            }

            float wrapWidth = wrapHalfWidth * 2f;
            float movement = speed * Time.deltaTime;
            foreach (Transform strip in movingStrips)
            {
                if (strip == null)
                {
                    continue;
                }

                Vector3 position = strip.localPosition;
                position.x -= movement;
                while (position.x < -wrapHalfWidth)
                {
                    position.x += wrapWidth;
                }

                strip.localPosition = position;
            }
        }
    }
}

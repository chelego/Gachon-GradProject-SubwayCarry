using System.Collections;
using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    public sealed class StationTrainArrivalPrototype : MonoBehaviour
    {
        [SerializeField] private Vector2 platformPosition;
        [SerializeField] private float entryOffsetX = -34f;
        [SerializeField, Min(0f)] private float arrivalDelay = 0.25f;
        [SerializeField, Min(0.1f)] private float arrivalDuration = 2.8f;

        public bool HasArrived { get; private set; }

        public void Configure(
            Vector2 destination,
            float horizontalEntryOffset,
            float duration,
            float delay = 0.25f)
        {
            platformPosition = destination;
            entryOffsetX = horizontalEntryOffset;
            arrivalDuration = Mathf.Max(0.1f, duration);
            arrivalDelay = Mathf.Max(0f, delay);
        }

        private void OnEnable()
        {
            if (HasArrived)
            {
                transform.position = platformPosition;
                return;
            }

            StartCoroutine(ArriveAtPlatform());
        }

        private IEnumerator ArriveAtPlatform()
        {
            Vector2 start = platformPosition + Vector2.right * entryOffsetX;
            transform.position = start;
            HasArrived = false;

            if (arrivalDelay > 0f)
            {
                yield return new WaitForSeconds(arrivalDelay);
            }

            float elapsed = 0f;
            while (elapsed < arrivalDuration)
            {
                elapsed += Time.deltaTime;
                float ratio = Mathf.Clamp01(elapsed / arrivalDuration);
                ratio = ratio * ratio * (3f - 2f * ratio);
                transform.position = Vector2.Lerp(start, platformPosition, ratio);
                yield return null;
            }

            transform.position = platformPosition;
            HasArrived = true;
        }
    }
}

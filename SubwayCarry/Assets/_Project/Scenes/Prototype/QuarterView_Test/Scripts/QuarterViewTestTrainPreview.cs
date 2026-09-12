using System.Collections;
using UnityEngine;

namespace SubwayCarry.Prototype.QuarterView
{
    [DisallowMultipleComponent]
    public sealed class QuarterViewTestTrainPreview : MonoBehaviour
    {
        [SerializeField] private Vector2 entryOffset = new Vector2(-20f, -10f);
        [SerializeField, Min(0f)] private float arrivalDelay = 1.2f;
        [SerializeField, Min(0.2f)] private float travelDuration = 4.8f;
        [SerializeField, Min(0f)] private float dwellDuration = 7f;
        [SerializeField] private bool loop = true;

        private Coroutine routine;

        public void Configure(Vector2 offset, float duration)
        {
            entryOffset = offset;
            travelDuration = Mathf.Max(0.2f, duration);
        }

        private void OnEnable()
        {
            transform.localPosition = new Vector3(entryOffset.x, entryOffset.y, 0f);
            routine = StartCoroutine(ArrivalRoutine());
        }

        private void OnDisable()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
        }

        private IEnumerator ArrivalRoutine()
        {
            do
            {
                transform.localPosition = new Vector3(entryOffset.x, entryOffset.y, 0f);
                if (arrivalDelay > 0f)
                {
                    yield return new WaitForSeconds(arrivalDelay);
                }

                float elapsed = 0f;
                while (elapsed < travelDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / travelDuration);
                    t = 1f - Mathf.Pow(1f - t, 3f);
                    Vector2 offset = Vector2.LerpUnclamped(entryOffset, Vector2.zero, t);
                    transform.localPosition = new Vector3(offset.x, offset.y, 0f);
                    yield return null;
                }

                transform.localPosition = Vector3.zero;
                if (dwellDuration > 0f)
                {
                    yield return new WaitForSeconds(dwellDuration);
                }
            }
            while (loop);

            routine = null;
        }
    }
}

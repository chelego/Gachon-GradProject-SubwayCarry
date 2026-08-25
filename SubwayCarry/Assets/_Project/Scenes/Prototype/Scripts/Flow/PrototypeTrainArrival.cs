using System.Collections;
using UnityEngine;

namespace SubwayCarry.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeTrainArrival : MonoBehaviour
    {
        [SerializeField] private Transform trainRoot;
        [SerializeField] private GameObject platformEdgeBlocker;
        [SerializeField] private Vector3 parkedWorldPosition;
        [SerializeField] private Vector3 entryOffset = new Vector3(30f, 0f, 0f);
        [SerializeField, Min(0.1f)] private float arrivalDuration = 3.2f;

        public bool IsArrived { get; private set; }

        public void Configure(
            Transform controlledTrain,
            GameObject edgeBlocker,
            Vector3 parkedPosition,
            Vector3 offscreenOffset,
            float duration)
        {
            trainRoot = controlledTrain;
            platformEdgeBlocker = edgeBlocker;
            parkedWorldPosition = parkedPosition;
            entryOffset = offscreenOffset;
            arrivalDuration = Mathf.Max(0.1f, duration);
            PrepareOffscreen();
        }

        public void PrepareOffscreen()
        {
            IsArrived = false;
            if (trainRoot != null)
            {
                trainRoot.position = parkedWorldPosition + entryOffset;
            }

            if (platformEdgeBlocker != null)
            {
                platformEdgeBlocker.SetActive(true);
            }
        }

        public IEnumerator Arrive()
        {
            if (trainRoot == null)
            {
                IsArrived = true;
                yield break;
            }

            Vector3 start = parkedWorldPosition + entryOffset;
            trainRoot.position = start;
            float elapsed = 0f;
            while (elapsed < arrivalDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / arrivalDuration);
                float eased = t * t * (3f - 2f * t);
                trainRoot.position = Vector3.LerpUnclamped(
                    start,
                    parkedWorldPosition,
                    eased);
                yield return null;
            }

            trainRoot.position = parkedWorldPosition;
            IsArrived = true;
        }

        public void OpenPlatformEdge()
        {
            if (platformEdgeBlocker != null)
            {
                platformEdgeBlocker.SetActive(false);
            }
        }
    }
}

using UnityEngine;

namespace SubwayCarry.AI.UtilityJourney
{
    [DisallowMultipleComponent]
    public sealed class UtilityJourneyPassagePrototype : MonoBehaviour
    {
        [SerializeField] private Transform[] movingPanels;
        [SerializeField] private Vector3[] closedLocalPositions;
        [SerializeField] private Vector3[] openLocalOffsets;
        [SerializeField, Min(0.05f)] private float animationSeconds = 0.28f;
        [SerializeField] private bool targetOpen;
        [SerializeField, Range(0f, 1f)] private float openAmount;

        public bool IsOpen => openAmount >= 0.98f;
        public float AnimationSeconds => animationSeconds;

        public void Configure(
            Transform[] panels,
            Vector3[] offsets,
            float duration = 0.28f)
        {
            movingPanels = panels ?? new Transform[0];
            openLocalOffsets = offsets ?? new Vector3[0];
            closedLocalPositions = new Vector3[movingPanels.Length];
            animationSeconds = Mathf.Max(0.05f, duration);
            for (int index = 0; index < movingPanels.Length; index++)
            {
                if (movingPanels[index] != null)
                {
                    closedLocalPositions[index] = movingPanels[index].localPosition;
                }
            }

            SetImmediate(false);
        }

        public void SetOpen(bool value)
        {
            targetOpen = value;
        }

        public void SetImmediate(bool value)
        {
            targetOpen = value;
            openAmount = value ? 1f : 0f;
            ApplyPanelPositions();
        }

        private void Update()
        {
            float target = targetOpen ? 1f : 0f;
            if (Mathf.Approximately(openAmount, target))
            {
                return;
            }

            openAmount = Mathf.MoveTowards(
                openAmount,
                target,
                Time.deltaTime / animationSeconds);
            ApplyPanelPositions();
        }

        private void ApplyPanelPositions()
        {
            if (movingPanels == null || closedLocalPositions == null)
            {
                return;
            }

            int count = Mathf.Min(movingPanels.Length, closedLocalPositions.Length);
            for (int index = 0; index < count; index++)
            {
                Transform panel = movingPanels[index];
                if (panel == null)
                {
                    continue;
                }

                Vector3 offset = openLocalOffsets != null &&
                                 index < openLocalOffsets.Length
                    ? openLocalOffsets[index]
                    : Vector3.zero;
                panel.localPosition = Vector3.Lerp(
                    closedLocalPositions[index],
                    closedLocalPositions[index] + offset,
                    openAmount);
            }
        }
    }
}

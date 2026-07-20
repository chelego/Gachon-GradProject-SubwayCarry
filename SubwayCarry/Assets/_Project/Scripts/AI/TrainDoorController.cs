using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    public sealed class TrainDoorController : MonoBehaviour
    {
        [SerializeField] private Transform leftPanel;
        [SerializeField] private Transform rightPanel;
        [SerializeField, Min(0.1f)] private float slideDistance = 0.55f;
        [SerializeField, Min(0.1f)] private float transitionDuration = 0.6f;

        private Vector3 leftClosedPosition;
        private Vector3 rightClosedPosition;
        private float openness;
        private float targetOpenness;

        public bool IsOpen => openness >= 0.95f;
        public bool IsClosed => openness <= 0.05f;

        public void Configure(Transform leftDoorPanel, Transform rightDoorPanel)
        {
            leftPanel = leftDoorPanel;
            rightPanel = rightDoorPanel;
        }

        public void SetOpen(bool open)
        {
            targetOpenness = open ? 1f : 0f;
        }

        private void Awake()
        {
            if (leftPanel != null)
            {
                leftClosedPosition = leftPanel.localPosition;
            }

            if (rightPanel != null)
            {
                rightClosedPosition = rightPanel.localPosition;
            }
        }

        private void Update()
        {
            if (leftPanel == null || rightPanel == null)
            {
                return;
            }

            float speed = 1f / transitionDuration;
            openness = Mathf.MoveTowards(openness, targetOpenness, speed * Time.deltaTime);
            leftPanel.localPosition = leftClosedPosition + Vector3.left * slideDistance * openness;
            rightPanel.localPosition = rightClosedPosition + Vector3.right * slideDistance * openness;
        }
    }
}

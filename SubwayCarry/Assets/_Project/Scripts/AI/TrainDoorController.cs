using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    public sealed class TrainDoorController : MonoBehaviour
    {
        [SerializeField] private Transform leftPanel;
        [SerializeField] private Transform rightPanel;
        [SerializeField, Min(0.1f)] private float slideDistance = 0.9f;
        [SerializeField, Min(0.1f)] private float transitionDuration = 0.6f;

        private Vector3 leftClosedPosition;
        private Vector3 rightClosedPosition;
        private Rigidbody2D leftBody;
        private Rigidbody2D rightBody;
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
                leftBody = leftPanel.GetComponent<Rigidbody2D>();
            }

            if (rightPanel != null)
            {
                rightClosedPosition = rightPanel.localPosition;
                rightBody = rightPanel.GetComponent<Rigidbody2D>();
            }
        }

        private void FixedUpdate()
        {
            if (leftPanel == null || rightPanel == null)
            {
                return;
            }

            float speed = 1f / transitionDuration;
            openness = Mathf.MoveTowards(openness, targetOpenness, speed * Time.fixedDeltaTime);
            MovePanel(leftPanel, leftBody, leftClosedPosition + Vector3.left * slideDistance * openness);
            MovePanel(rightPanel, rightBody, rightClosedPosition + Vector3.right * slideDistance * openness);
        }

        private static void MovePanel(Transform panel, Rigidbody2D body, Vector3 localPosition)
        {
            if (body == null)
            {
                panel.localPosition = localPosition;
                return;
            }

            body.MovePosition(panel.parent.TransformPoint(localPosition));
        }
    }
}

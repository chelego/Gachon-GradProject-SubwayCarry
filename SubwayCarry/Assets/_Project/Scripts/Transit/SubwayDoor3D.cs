using UnityEngine;

namespace SubwayCarry.Transit
{
    [DisallowMultipleComponent]
    public sealed class SubwayDoor3D : MonoBehaviour
    {
        public Transform negativeLeaf, positiveLeaf;
        public float slideDistance = .64f;
        public float transitionDuration = .9f;
        private Vector3 negativeClosed, positiveClosed;
        private float openness;
        private readonly Collider[] obstruction = new Collider[16];
        public bool RequestedOpen { get; private set; }
        public float Openness => openness;
        private void Awake()
        {
            if (negativeLeaf) negativeClosed = negativeLeaf.localPosition;
            if (positiveLeaf) positiveClosed = positiveLeaf.localPosition;
        }
        public void SetOpen(bool open) => RequestedOpen = open;
        private void Update()
        {
            if (!RequestedOpen && openness > 0f)
            {
                int count = Physics.OverlapBoxNonAlloc(transform.TransformPoint(Vector3.up),
                    new Vector3(.35f, 1f, .68f), obstruction, transform.rotation, ~0,
                    QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                    if (obstruction[i].GetComponent<CharacterController>()) return;
            }
            openness = Mathf.MoveTowards(openness, RequestedOpen ? 1 : 0,
                Time.deltaTime / Mathf.Max(.01f, transitionDuration));
            float offset = Mathf.SmoothStep(0, 1, openness) * slideDistance;
            if (negativeLeaf) negativeLeaf.localPosition = negativeClosed - Vector3.forward * offset;
            if (positiveLeaf) positiveLeaf.localPosition = positiveClosed + Vector3.forward * offset;
        }
    }
}


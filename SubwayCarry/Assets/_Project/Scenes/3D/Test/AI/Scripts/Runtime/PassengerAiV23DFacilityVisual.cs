using UnityEngine;

namespace SubwayCarry.AI.V2
{
    /// <summary>Physical 3D presentation listens to existing Smart Object state.</summary>
    public sealed class PassengerAiV23DFacilityVisual : MonoBehaviour
    {
        [SerializeField] private PassengerAiV2FareGateSmartObject gate;
        [SerializeField] private PassengerAiV2TrainDoorSmartObject door;
        [SerializeField] private Transform left, right;
        private Vector3 leftClosed, rightClosed;
        private Collider leftCollider,rightCollider;
        private float open;
        public void Configure(PassengerAiV2FareGateSmartObject fareGate, PassengerAiV2TrainDoorSmartObject trainDoor, Transform a, Transform b)
        { gate=fareGate; door=trainDoor; left=a; right=b; }
        private void Awake()
        {
            if(left!=null) { leftClosed=left.localPosition; leftCollider=left.GetComponent<Collider>(); }
            if(right!=null) { rightClosed=right.localPosition; rightCollider=right.GetComponent<Collider>(); }
        }
        private void Update()
        {
            bool isOpen=gate!=null ? gate.IsOpen : door!=null && door.IsDoorOpen;
            open=Mathf.MoveTowards(open,isOpen?1:0,Time.deltaTime*4);
            float travel=gate!=null ? .86f : 1.12f;
            if(left!=null) left.localPosition=leftClosed+Vector3.left*open*travel;
            if(right!=null) right.localPosition=rightClosed+Vector3.right*open*travel;
            // Admission is controlled by reservations; no invisible closed collider.
            if(leftCollider!=null) leftCollider.enabled=!isOpen;
            if(rightCollider!=null) rightCollider.enabled=!isOpen;
        }
    }
}

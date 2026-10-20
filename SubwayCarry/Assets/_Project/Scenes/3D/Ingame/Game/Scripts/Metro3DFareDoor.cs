using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public sealed class Metro3DFareDoor : MonoBehaviour
    {
        public Transform left,right;
        public Collider blocker;
        float amount,target,passengerUntil;
        public bool Passable => amount>=.85f;
        public void HoldForPassenger() {passengerUntil=Time.time+.8f;}
        public void Open() {target=1;}
        public void Close() {target=0;}
        void Update()
        {
            amount=Mathf.MoveTowards(amount,Time.time<passengerUntil?1:target,Time.deltaTime*3);
            left.localRotation=Quaternion.Euler(0,82*amount,0);
            right.localRotation=Quaternion.Euler(0,-82*amount,0);
            blocker.enabled=amount<.85f;
        }
    }
}

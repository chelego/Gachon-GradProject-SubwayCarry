using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public enum Metro3DFacilityKind { Seat, Lean, Hold, FareGate, ExitGate, ElevatorCall, ElevatorRide }

    public sealed class Metro3DFacility : MonoBehaviour
    {
        public Metro3DFacilityKind kind;
        public Transform anchor, approach;
        public Collider blocker;
        public string caption;
        public bool trainFacility;
        public Metro3DLift lift;
        public int liftFloor;
        public Metro3DFareDoor gateDoor;
        public Object Occupant { get; private set; }
        public bool Available => Occupant == null;
        public bool Claim(Object actor)
        {
            if (Occupant != null && Occupant != actor) return false;
            Occupant = actor; return true;
        }
        public void Release(Object actor) { if (Occupant == actor) Occupant = null; }
    }
}

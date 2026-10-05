using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // Persistent journey intent; local navigation and gait do not choose destinations.
    public sealed class Metro3DPassenger : MonoBehaviour
    {
        public Metro3DWorld world;
        public int index, stationIndex;
        public bool inTrain;
        public Vector3 Velocity { get; private set; }
        public bool IsAlighting => state==Activity.Alight || state==Activity.StandUp;
        public bool WantsToAlight => inTrain&&riding!=null&&riding.DoorsOpen&&riding.CurrentStop>0&&index%3==riding.CurrentStop%3;
        enum Activity { Walk, Wait, SitDown, Sit, StandUp, Board, Alight, Exit }
        Activity state;
        Metro3DFacility seat;
        Metro3DTrain riding;
        Animator animator;
        CharacterController motor;
        Metro3DCrowd crowd;
        readonly Vector3[] path=new Vector3[384];
        int pathCount, pathIndex, waypoint, door;
        bool pendingPath, triedSeat, exiting;
        Vector3 target, transitionFrom, transitionTo, velocity, steering;
        float nextDecision, nextSteering, verticalSpeed, transitionTime, blockedFor, retryAt;
        static readonly int Speed=Animator.StringToHash("Speed"), Seated=Animator.StringToHash("Seated"), Gait=Animator.StringToHash("Gait");
        void Start()
        {
            animator=GetComponentInChildren<Animator>(); motor=GetComponent<CharacterController>();
            crowd=world.GetComponent<Metro3DCrowd>();
            if(crowd==null) crowd=world.gameObject.AddComponent<Metro3DCrowd>();
            crowd.world=world; crowd.Register(this);
            riding=inTrain?GetComponentInParent<Metro3DTrain>():null;
            target=transform.position; state=Activity.Wait;
            nextDecision=Time.time+(index%13)*.025f; nextSteering=Time.time+(index%7)*.013f;
        }
        void Update()
        {
            if(world.session.Blocked) { Velocity=Vector3.zero; Animate(0); return; }
            // Door events still wake a distant passenger; do not freeze the journey at 80m.
            if(Time.time>=nextDecision) { nextDecision=Time.time+.3f; Decide(); }
            if(state==Activity.SitDown||state==Activity.StandUp) { UpdateTransition(); return; }
            if(state==Activity.Sit) { Velocity=Vector3.zero; Animate(0); return; }
            Animate(Velocity.magnitude);
        }
        void FixedUpdate()
        {
            if(crowd==null||motor==null||!motor.enabled||world.session.Blocked) return;
            bool walking=state==Activity.Walk||state==Activity.Board||state==Activity.Alight||state==Activity.Exit;
            Vector3 goal=target;
            if(pathIndex<pathCount) {
                goal=transform.parent.TransformPoint(path[pathIndex]);
                if(FlatDistance(transform.position,goal)<.2f) { pathIndex++; goal=pathIndex<pathCount?transform.parent.TransformPoint(path[pathIndex]):target; }
            }
            Vector3 delta=goal-transform.position; delta.y=0;
            Vector3 desired=walking&&!pendingPath&&delta.magnitude>.12f ? delta.normalized*Mathf.Min(world.settings.npcSpeed,delta.magnitude*3.2f):Vector3.zero;
            if(Time.time>=nextSteering) { nextSteering=Time.time+.1f; steering=desired.sqrMagnitude>.001f?crowd.SafeVelocity(this,desired):Vector3.zero; }
            if(!walking||pendingPath||delta.magnitude<=.12f) steering=Vector3.zero;
            velocity=Vector3.MoveTowards(velocity,steering,Time.deltaTime*(steering.sqrMagnitude<velocity.sqrMagnitude?3.8f:2.3f));
            if(velocity.sqrMagnitude>.008f) {
                transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(velocity),Time.deltaTime*210);
                velocity*=Mathf.Lerp(.35f,1,Mathf.Clamp01(Vector3.Dot(transform.forward,velocity.normalized)));
            }
            verticalSpeed=motor.isGrounded?-1.5f:verticalSpeed-20*Time.deltaTime;
            Vector3 before=transform.position; motor.Move((velocity+Vector3.up*verticalSpeed)*Time.deltaTime);
            Velocity=(transform.position-before)/Mathf.Max(.001f,Time.deltaTime); Velocity=new Vector3(Velocity.x,0,Velocity.z);
            blockedFor=walking&&!pendingPath&&delta.magnitude>.4f&&Velocity.magnitude<.045f?blockedFor+Time.deltaTime:0;
            if(blockedFor>2.5f&&Time.time>=retryAt) { blockedFor=0; retryAt=Time.time+3; RequestPath(); }
        }
        void Animate(float speed)
        {
            if(animator==null) return;
            animator.SetFloat(Speed,speed,.12f,Time.deltaTime); animator.SetFloat(Gait,Mathf.Clamp(speed/1.25f,.65f,1.35f));
            animator.SetBool(Seated,state==Activity.Sit||state==Activity.SitDown);
        }
        void Decide()
        {
            var active=world.train;
            if(state==Activity.Alight&&riding!=null&&!riding.DoorsOpen) {state=Activity.Wait; target=transform.position; pathCount=0; pendingPath=false; triedSeat=false; velocity=steering=Vector3.zero;}
            if(inTrain&&riding!=null&&riding.DoorsOpen&&riding.CurrentStop>0&&index%3==riding.CurrentStop%3) {
                if(state==Activity.SitDown||state==Activity.StandUp) return;
                if(state==Activity.Sit) { BeginStand(); return; }
                if(state!=Activity.Alight) { ReleaseSeat(); door=riding.NearestDoor(transform.position); waypoint=0; state=Activity.Alight; SetTarget(riding.DoorPoint(door,false),true); }
                AdvanceDoor(false); return;
            }
            if(state==Activity.Alight) { AdvanceDoor(false); return; }
            if(state==Activity.Exit) { if(FlatDistance(transform.position,target)<.3f) Destroy(gameObject); return; }
            if(!inTrain&&state==Activity.Sit&&active.DoorsOpen&&active.CurrentStop==stationIndex&&world.session.CanPassengersBoard&&index%4!=0) { BeginStand(); return; }
            if(state==Activity.Sit||state==Activity.SitDown||state==Activity.StandUp) return;
            if(!inTrain&&!exiting&&active.DoorsOpen&&active.CurrentStop==stationIndex&&world.session.CanPassengersBoard&&index%4!=0) {
                if(state!=Activity.Board) { ReleaseSeat(); door=active.NearestDoor(transform.position); waypoint=0; state=Activity.Board; SetTarget(active.DoorPoint(door,true),true); }
                AdvanceDoor(true); return;
            }
            if(state==Activity.Board&&!active.DoorsOpen) { state=Activity.Wait; velocity=steering=Vector3.zero; pendingPath=false; pathCount=0; target=transform.position; return; }
            if(state==Activity.Walk&&FlatDistance(transform.position,target)<.18f) {
                if(seat!=null) { BeginSit(); return; }
                state=Activity.Wait; velocity=steering=Vector3.zero;
            }
            if(state==Activity.Wait&&!triedSeat) { triedSeat=true; TrySeat(); }
            // No idle timeout that sends people backwards to a different waiting point.
        }
        void AdvanceDoor(bool boarding)
        {
            Metro3DTrain t=boarding?world.train:riding;
            if(t==null||!t.DoorsOpen) return;
            if(waypoint==0&&FlatDistance(transform.position,target)<.24f) {
                if(boarding&&crowd.AlightersAt(t,door)) return;
                waypoint=1; SetTarget(t.DoorPoint(door,!boarding),false);
            } else if(waypoint==1&&FlatDistance(transform.position,target)<.24f) {
                pathCount=pathIndex=0; pendingPath=false; velocity=steering=Vector3.zero;
                inTrain=boarding; riding=boarding?t:null; stationIndex=t.CurrentStop;
                transform.SetParent(boarding?t.transform:world.stations[stationIndex],true); triedSeat=false;
                if(boarding) { state=Activity.Wait; target=transform.position; TrySeat(); triedSeat=true; }
                else {
                    exiting=true; state=Activity.Exit; float z=transform.localPosition.z;
                    SetTarget(transform.parent.TransformPoint(new Vector3(-t.side*7.76f,0,z<Metro3DWorld.PlatformLength*.5f?12:Metro3DWorld.FarStairBottom-.7f)),true);
                }
            }
        }
        bool TrySeat()
        {
            Metro3DFacility best=null; float score=float.NegativeInfinity;
            foreach(var f in world.facilities) {
                if(f==null||f.kind!=Metro3DFacilityKind.Seat||!f.Available||f.trainFacility!=inTrain||f.approach==null||!f.transform.IsChildOf(transform.parent)) continue;
                float d=FlatDistance(transform.position,f.approach.position); if(d>8) continue;
                float s=8-d; if(s>score) {best=f;score=s;}
            }
            if(best==null||!best.Claim(this)) return false;
            seat=best; state=Activity.Walk; SetTarget(best.approach.position,true); return true;
        }
        void SetTarget(Vector3 p,bool navigate) { target=p; pathCount=pathIndex=0; pendingPath=false; if(navigate) RequestPath(); }
        void RequestPath() { if(crowd==null) return; pendingPath=true; crowd.Request(this); }
        public void SolvePath(Metro3DNavigation navigation)
        {
            if(!pendingPath) return;
            pathCount=navigation.Find(transform.parent,inTrain,transform.position,target,path); pathIndex=0; pendingPath=false;
            if(pathCount==0) {retryAt=Time.time+4; velocity=steering=Vector3.zero; if(state==Activity.Walk) { ReleaseSeat(); state=Activity.Wait; triedSeat=true; target=transform.position; }}
        }
        void BeginSit()
        {
            state=Activity.SitDown; transitionTime=0; transitionFrom=transform.position; transitionTo=seat.anchor.position;
            motor.enabled=false; velocity=steering=Velocity=Vector3.zero; animator.CrossFadeInFixedTime("SitDown",.12f); Animate(0);
        }
        void BeginStand()
        {
            state=Activity.StandUp; transitionTime=0; transitionFrom=transform.position; transitionTo=seat.approach.position;
            animator.CrossFadeInFixedTime("StandUp",.12f); Animate(0);
        }
        void UpdateTransition()
        {
            transitionTime+=Time.deltaTime; float p=Mathf.Clamp01(transitionTime/.9f);
            transform.position=Vector3.Lerp(transitionFrom,transitionTo,p*p*(3-2*p));
            transform.rotation=Quaternion.RotateTowards(transform.rotation,seat.anchor.rotation,Time.deltaTime*240); Velocity=Vector3.zero;
            if(p<1) return;
            if(state==Activity.SitDown) {state=Activity.Sit; animator.CrossFadeInFixedTime("Seated",.08f);}
            else { ReleaseSeat(); motor.enabled=true; state=Activity.Wait; triedSeat=true; nextDecision=0; animator.CrossFadeInFixedTime("Locomotion",.12f); }
        }
        void ReleaseSeat() { if(seat!=null) { seat.Release(this); seat=null; } }
        public void RelocateToPlatform()
        {
            ReleaseSeat(); if(motor!=null) motor.enabled=false;
            transform.position=world.stations[stationIndex].TransformPoint(world.PlatformWaitingPoint(index));
            if(motor!=null) motor.enabled=true;
            state=Activity.Wait; target=transform.position; triedSeat=false; pendingPath=false; pathCount=0;
        }
        static float FlatDistance(Vector3 a,Vector3 b) {a.y=b.y; return Vector3.Distance(a,b);}
        void OnDestroy() { ReleaseSeat(); if(crowd!=null) crowd.Unregister(this); if(world!=null) world.passengers.Remove(this); }
    }
}

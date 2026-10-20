using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // Locomotion/activity executor. Journey intent lives in the existing V2 planner adapter.
    public sealed class Metro3DPassenger : MonoBehaviour
    {
        public Metro3DWorld world;
        public int index,stationIndex;
        public bool inTrain;
        public Vector3 Velocity {get;private set;}
        public float SteeringCost {get;private set;}
        public int StaticRejects {get;private set;}
        public Metro3DPassengerJourney Journey {get;private set;}
        public Metro3DTrain Riding => riding;
        public bool DoorCrossing => (state==Activity.Board||state==Activity.Alight)&&(waypoint==1||waypoint==2);
        public float SteeringDistance {get;private set;}
        public bool PortalCommitted => DoorCrossing||(Journey!=null&&Journey.CrossingGate);
        public Metro3DPassenger QueueLeader {get;private set;}
        public Vector3 QueueForward {get;private set;}
        public float QueueGap {get;private set;}
        public bool QueueActive => Time.time<queueUntil;
        public bool QueueCommitted {
            get {
                if(!QueueActive||FlatDistance(transform.position,CurrentGoal)>=1.6f) return false;
                // Joining the tail may require walking back along the queue. Do
                // not apply forward-following constraints before that join ends.
                Vector3 toGoal=CurrentGoal-transform.position;toGoal.y=0;
                float along=Vector3.Dot(toGoal,QueueForward);
                // Approaching from the side is not yet following in the lane.
                // Otherwise a queued alighter cannot leave the central aisle.
                if(along<-.08f||(toGoal-QueueForward*along).sqrMagnitude>.32f*.32f) return false;
                return QueueLeader==null||Vector3.Dot(QueueLeader.transform.position-transform.position,QueueForward)>0;
            }
        }
        public int PassingSide {get;private set;}
        public float PassingUntil {get;private set;}
        public bool RecoveryActive => Time.time<backoffUntil;
        public float QueueReactionDelay => Mathf.Clamp(Journey.Personality.ReactionDelay/Journey.SpeedScale,.1f,.5f);
        bool ExpectedQueueWait {
            get {
                if(!QueueActive||PortalCommitted)return false;
                if(state==Activity.Alight&&riding!=null&&!riding.DoorsOpen)return true;
                if(!QueueCommitted)return false;
                if(QueueLeader!=null&&QueueLeader.isActiveAndEnabled) {
                    Vector3 gap=QueueLeader.transform.position-transform.position;gap.y=0;
                    return gap.magnitude<QueueGap+.4f;
                }
                return queuePermissionWait&&FlatDistance(transform.position,target)<.4f;
            }
        }
        public bool WaitingForQueue => ExpectedQueueWait;
        float queueUntil,nextQueuePath,yieldUntil,backoffUntil;
        bool queuePermissionWait;
        int recoveryStage;
        Vector3 recoveryVelocity,backoffFrom;
        public void SetQueueContext(Metro3DPassenger leader,Vector3 direction,float gap,bool permissionWait)
        {
            QueueLeader=leader;QueueForward=direction.normalized;QueueGap=gap;
            queuePermissionWait=permissionWait;queueUntil=Time.time+.8f;
        }
        public void ClearQueueContext() {queueUntil=0;QueueLeader=null;queuePermissionWait=false;}
        public void CommitPassingSide(int side) {PassingSide=side;PassingUntil=Time.time+.85f;}
        public void FollowQueueGoal(Vector3 point)
        {
            // Queue following is locomotion inside a facility action, not a
            // transition out of Board/Alight. Losing that action resets its
            // alignment waypoint on the next decision tick.
            if(state!=Activity.Board&&state!=Activity.Alight) state=Activity.Walk;
            if(crowd.CanApproachDirect(this,point)) SetTarget(point,false);
            else if(Time.time>=nextQueuePath||(!pendingPath&&pathIndex>=pathCount)) {
                nextQueuePath=Time.time+.65f;SetTarget(point,true);
            }
        }
        public bool IsAlighting => state==Activity.Alight;
        public int AlightingDoor => state==Activity.Alight?door:-1;
        public bool IsSeated => state==Activity.Sit||state==Activity.SitDown;
        public bool IsResting => state==Activity.Wait;
        public Metro3DFacility Seat => seat;
        public Vector3 CurrentGoal => target;
        public bool CanAssist => !IsSeated&&state!=Activity.StandUp&&state!=Activity.Board&&state!=Activity.Alight&&seat==null
            &&Journey!=null&&(inTrain||Journey.Waiting)&&!WantsToAlight
            &&!(inTrain&&riding!=null&&Journey.AtDestination(riding.CurrentStop))
            &&!(!inTrain&&Journey.CanBoard&&world.train!=null&&world.train.DoorsOpen&&!world.train.Moving&&world.train.CurrentStop==stationIndex);
        public float SittingWeight => state==Activity.Sit?1:state==Activity.SitDown?Mathf.SmoothStep(0,1,transitionTime/.9f):state==Activity.StandUp?1-Mathf.SmoothStep(0,1,transitionTime/.9f):0;
        public bool WantsToAlight => inTrain&&riding!=null&&riding.DoorsOpen&&!riding.Moving
            &&(Journey.AtDestination(riding.CurrentStop)||(riding==world.train&&world.session.TransferAtCurrentStop));
        enum Activity { Walk,Wait,SitDown,Sit,StandUp,Board,Alight }
        Activity state;
        Metro3DFacility seat;
        Metro3DTrain riding;
        Metro3DTrain egressTrain;
        int egressDoor;
        Vector3 egressPoint;
        public bool ClearingDoor(Metro3DTrain train,int number)
            => train!=null&&!inTrain&&egressTrain==train&&number==egressDoor&&(transform.localPosition-egressPoint).sqrMagnitude<1.25f*1.25f;
        Animator animator;
        CharacterController motor;
        Metro3DCrowd crowd;
        readonly Vector3[] path=new Vector3[384];
        int pathCount,pathIndex,waypoint,door;
        bool pendingPath,triedSeat;
        Vector3 target,targetLocal,transitionFrom,transitionTo,velocity,steering;
        float nextDecision,nextSteering,verticalSpeed,transitionTime,blockedFor,retryAt;
        float helperUntil,clearYieldUntil;
        bool helperTaskActive;
        bool transitionPaused;
        float transitionAnimatorSpeed=1,transitionBlockedSince;
        public float TransitionBlockedFor => transitionPaused?Time.time-transitionBlockedSince:0;
        static readonly int Speed=Animator.StringToHash("Speed"),Seated=Animator.StringToHash("Seated"),Gait=Animator.StringToHash("Gait");
        public void Initialize(Metro3DWorld owner,int id,int stop,bool inside,bool existing,bool far)
        {
            world=owner;index=id;stationIndex=stop;inTrain=inside;helperUntil=clearYieldUntil=0;helperTaskActive=false;
            queueUntil=nextQueuePath=yieldUntil=backoffUntil=0;recoveryStage=PassingSide=0;PassingUntil=0;QueueLeader=null;
            animator=GetComponentInChildren<Animator>(true);motor=GetComponent<CharacterController>();crowd=world.GetComponent<Metro3DCrowd>();
            ResumeTransitionAnimation();egressTrain=null;
            riding=inside?world.train:null;seat=null;triedSeat=pendingPath=false;pathCount=pathIndex=waypoint=0;
            velocity=steering=Velocity=Vector3.zero;verticalSpeed=blockedFor=0;state=Activity.Wait;target=transform.position;targetLocal=transform.localPosition;
            nextDecision=Time.time+(id%13)*.025f;nextSteering=Time.time+(id%7)*.013f;motor.enabled=true;
            motor.radius=.24f;motor.skinWidth=.025f;
            var contacts=GetComponent<Metro3DPassengerContacts>();if(contacts==null) contacts=gameObject.AddComponent<Metro3DPassengerContacts>();contacts.Initialize(this);
            Journey=GetComponent<Metro3DPassengerJourney>();if(Journey==null) Journey=gameObject.AddComponent<Metro3DPassengerJourney>();
            Journey.Initialize(this,stop,inside,existing,far);
            var colors=new[]{Color.white,new Color(.82f,.88f,1),new Color(1,.85f,.7f),new Color(.85f,.85f,.9f),new Color(.75f,.9f,.9f),new Color(.9f,.8f,1)};
            var tint=new MaterialPropertyBlock();tint.SetColor("_BaseColor",colors[id%colors.Length]);
            foreach(var renderer in GetComponentsInChildren<SkinnedMeshRenderer>()) {
                for(int slot=0;slot<renderer.sharedMaterials.Length;slot++)
                    if(renderer.sharedMaterials[slot]!=null&&renderer.sharedMaterials[slot].name=="CommuterCoat") renderer.SetPropertyBlock(tint,slot);
            }
            if(animator!=null) {animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;}
        }
        void OnEnable() {
            if(crowd!=null) crowd.Register(this);
            if(animator!=null) {animator.Rebind();animator.Play("Locomotion",0,(index*.173f)%1);}
        }
        void Update()
        {
            if(world==null||world.session.SimulationPaused) {Velocity=Vector3.zero;Animate(0);return;}
            if(state==Activity.SitDown||state==Activity.StandUp) {UpdateTransition();return;}
            if(Time.time>=nextDecision) {nextDecision=Time.time+.25f;Decide();}Animate(Velocity.magnitude);
        }
        void FixedUpdate()
        {
            if(crowd==null||motor==null||!motor.enabled||world.session.SimulationPaused) return;
            bool walking=state==Activity.Walk||state==Activity.Board||state==Activity.Alight;
            if(Journey.OnStairs) {pendingPath=false;pathCount=pathIndex=0;}
            target=transform.parent.TransformPoint(targetLocal);Vector3 goal=target;
            if(pathIndex<pathCount) {
                goal=transform.parent.TransformPoint(path[pathIndex]);
                if(FlatDistance(transform.position,goal)<(inTrain?.09f:.2f)) {pathIndex++;goal=pathIndex<pathCount?transform.parent.TransformPoint(path[pathIndex]):target;}
            }
            Vector3 delta=goal-transform.position;delta.y=0;
            SteeringDistance=delta.magnitude;
            float speed=world.settings.npcSpeed*Journey.SpeedScale;
            Vector3 desired=walking&&!pendingPath&&delta.magnitude>.07f?delta.normalized*Mathf.Min(speed,delta.magnitude*3.2f):Vector3.zero;
            if(Time.time>=nextSteering) {nextSteering=Time.time+.1f;steering=desired.sqrMagnitude>.001f?crowd.SafeVelocity(this,desired):Vector3.zero;SteeringCost=crowd.LastSteeringCost;StaticRejects=crowd.LastStaticRejects;}
            if(!walking||pendingPath||delta.magnitude<=.07f) steering=Vector3.zero;
            if(Time.time<yieldUntil) steering=Vector3.zero;
            else if(Time.time<backoffUntil) {
                if((transform.localPosition-backoffFrom).sqrMagnitude<.28f*.28f) steering=recoveryVelocity;
                else backoffUntil=0;
            }
            velocity=Vector3.MoveTowards(velocity,steering,Time.fixedDeltaTime*(steering.sqrMagnitude<velocity.sqrMagnitude?3.8f:2.3f));
            float facing=1;
            if(velocity.sqrMagnitude>.008f) {
                transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(velocity),Time.fixedDeltaTime*240);
                facing=Mathf.Clamp01(Vector3.Dot(transform.forward,velocity.normalized));
            }
            // Do not repeatedly multiply the stored velocity by facing: that caused stop/start jitter.
            verticalSpeed=motor.isGrounded?-2f:verticalSpeed-20*Time.fixedDeltaTime;
            Vector3 before=transform.position;motor.Move((velocity*facing+Vector3.up*verticalSpeed)*Time.fixedDeltaTime);
            Vector3 actual=(transform.position-before)/Time.fixedDeltaTime;Velocity=new Vector3(actual.x,0,actual.z);
            bool progressing=Velocity.magnitude>=.045f;
            bool arrived=pathIndex>=pathCount&&FlatDistance(transform.position,target)<.23f;
            if(ExpectedQueueWait||!walking||pendingPath||arrived||progressing) {
                blockedFor=0;recoveryStage=0;
            } else blockedFor+=Time.fixedDeltaTime;
            if(!PortalCommitted&&blockedFor>.9f&&recoveryStage==0) {crowd.RequestPassage(this);yieldUntil=Time.time+.25f;recoveryStage=1;}
            if(!PortalCommitted&&blockedFor>1.6f&&recoveryStage==1) {
                if(crowd.TryBackoff(this,out recoveryVelocity)) {backoffUntil=Time.time+1.4f;backoffFrom=transform.localPosition;}
                recoveryStage=2;
            }
            if(blockedFor>3&&Time.time>=retryAt&&!PortalCommitted&&!ExpectedQueueWait) {
                blockedFor=0;retryAt=Time.time+4;Journey.Planner.ReportRecoverableFailure("Local path blocked after yield/backoff; destination retained");
                if(!helperTaskActive&&state==Activity.Walk&&seat==null&&(inTrain||Journey.CanBoard)) {
                    if(!inTrain&&motor.isGrounded&&world.Traffic.TryPlatformRest(this)) {WaitJourney();return;}
                    Vector3 failed=target;
                    SetTarget(inTrain?world.Traffic.TrainStandingPoint(this,riding,failed):world.Traffic.PlatformWaitingPoint(this,failed),true);
                }
                // A committed stair flight is a 3D facility traversal, not a
                // planar station grid. Replan on its landing, not mid-slope.
                else if(!Journey.OnStairs)RequestPath();
            }
        }
        void Animate(float speed)
        {
            if(animator==null) return;
            // Preserve the project's existing walk-speed reference. Use actual
            // displacement, while allowing the idle clip to run at normal speed.
            animator.SetFloat(Speed,speed,.12f,Time.deltaTime);animator.SetFloat(Gait,speed<.06f?1:Mathf.Clamp(speed/Mathf.Max(.1f,world.settings.npcSpeed),.3f,1.65f));
            animator.SetBool(Seated,state==Activity.Sit||state==Activity.SitDown);
        }
        void Decide()
        {
            if(helperTaskActive&&Time.time>=helperUntil) {
                helperTaskActive=false;
                if(CanAssist) {
                    state=Activity.Walk;triedSeat=true;
                    SetTarget(inTrain?world.Traffic.TrainStandingPoint(this,riding):world.Traffic.PlatformWaitingPoint(this),true);
                }
            }
            if(Time.time<helperUntil&&CanAssist)return;
            if(!inTrain&&Journey.Tick()) return;
            var active=world.train;
            if(inTrain&&riding!=null) {
                bool prepare=WantsToAlight||(riding.Moving&&Journey.AtDestination(riding.CurrentStop));
                if(prepare) {
                    Journey.PreparingToAlight();
                    world.Traffic.ReleaseStanding(this);
                    if(state==Activity.Sit) {BeginStand();return;}
                    if(state!=Activity.Alight) {ReleaseSeat();door=riding.NearestDoor(transform.position);waypoint=0;state=Activity.Alight;SetTarget(riding.DoorPoint(door,false),true);}
                    AdvanceDoor(false);return;
                }
            }
            if(!inTrain&&Journey.CanBoard&&active!=null&&active.DoorsOpen&&!active.Moving&&active.CurrentStop==stationIndex&&world.session.CanPassengersBoard) {
                if(state==Activity.Sit) {BeginStand();return;}
                if(state!=Activity.Board) {ReleaseSeat();world.Traffic.ReleaseStanding(this);door=active.NearestDoor(transform.position);waypoint=0;state=Activity.Board;Journey.BeginBoard();}
                AdvanceDoor(true);return;
            }
            if(state==Activity.Board&&(active==null||!active.DoorsOpen||!world.session.CanPassengersBoard)&&waypoint==0) {
                world.Traffic.ReleaseDoor(this);Journey.CancelBoard();SetWaitingPoint();return;
            }
            if(state==Activity.Board) {AdvanceDoor(true);return;}
            if(state==Activity.Sit||state==Activity.SitDown||state==Activity.StandUp) return;
            if(state==Activity.Walk&&FlatDistance(transform.position,target)<.23f) {if(seat!=null) {BeginSit();return;}WaitJourney();}
            if(state==Activity.Wait&&inTrain) {
                var support=world.Traffic.SupportFor(this);
                if(support!=null) {
                    Vector3 facing=support.lean?riding.transform.TransformDirection(support.normal):riding.transform.forward*(index%2==0?1:-1);
                    transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(facing),60*.25f);
                }
            }
            if(state==Activity.Wait&&!triedSeat) {
                triedSeat=true;
                if(inTrain&&!(Journey.SeatPreference>.48f&&TrySeat())) {state=Activity.Walk;SetTarget(world.Traffic.TrainStandingPoint(this,riding),true);}
                Journey.ActivityChosen();
            }
        }
        void AdvanceDoor(bool boarding)
        {
            Metro3DTrain t=boarding?world.train:riding;if(t==null) return;
            if(!boarding)crowd.ClearLanding(t,door,this);
            if(waypoint==0) {
                Vector3 approach=world.Traffic.DoorApproach(this,t,door,boarding);
                if((target-approach).sqrMagnitude>.0025f) FollowQueueGoal(approach);
                bool alreadyAtDoor=!boarding&&FlatDistance(transform.position,t.DoorPoint(door,false))<.25f;
                if(!t.DoorsOpen||(!alreadyAtDoor&&FlatDistance(transform.position,approach)>.28f)||!world.Traffic.TryDoor(this,t,door,boarding)) return;
                // Queue beside the door, align in front of its opening, then
                // cross straight through. A diagonal from the side queue hits
                // the fixed glazing even when the opening itself is clear.
                waypoint=1;ClearQueueContext();SetTarget(t.DoorPoint(door,boarding),false);
            }
            if(waypoint==1) {
                if(FlatDistance(transform.position,t.DoorPoint(door,boarding))>.15f) return;
                waypoint=2;SetTarget(t.DoorPoint(door,!boarding),false);
            }
            if(waypoint!=2) return;
            Vector3 local=t.transform.InverseTransformPoint(transform.position);
            // Completion is the feet crossing the physical boundary, not hitting one occupied point exactly.
            bool crossed=boarding?t.Contains(transform.position)&&Mathf.Abs(local.x)<1.1f
                :(-t.side*world.stations[t.CurrentStop].InverseTransformPoint(transform.position).x)>Metro3DWorld.ScreenDoorX+.24f;
            if(!crossed) return;
            world.Traffic.ReleaseDoor(this);var old=transform.parent;
            inTrain=boarding;stationIndex=t.CurrentStop;transform.SetParent(boarding?t.transform:world.stations[stationIndex],true);
            egressTrain=boarding?null:t;egressDoor=door;
            if(!boarding)egressPoint=transform.parent.InverseTransformPoint(t.DoorPoint(door,true));
            world.Traffic.ChangedParent(old,transform.parent);riding=boarding?t:null;triedSeat=false;WaitJourney();
            if(boarding) {
                Journey.OnBoard();triedSeat=true;
                if(!(Journey.SeatPreference>.48f&&TrySeat())) {state=Activity.Walk;SetTarget(world.Traffic.TrainStandingPoint(this,riding),true);}
                Journey.ActivityChosen();
            }
            else Journey.OnAlight(stationIndex);
        }
        public void WalkJourney(Vector3 p,bool navigate) {state=Activity.Walk;SetTarget(p,navigate);}
        // A short, cancellable local task. Destination, gate and train decisions stay with Journey.
        public bool TryHelperTarget(Vector3 point)
        {
            if(!CanAssist)return false;
            if(!helperTaskActive)world.Traffic.ReleaseStanding(this);
            helperTaskActive=true;
            helperUntil=Time.time+.45f;
            if((point-target).sqrMagnitude>.16f||state!=Activity.Walk){state=Activity.Walk;SetTarget(point,true);}
            return true;
        }
        public void WaitJourney() {state=Activity.Wait;target=transform.position;targetLocal=transform.localPosition;pendingPath=false;pathCount=pathIndex=0;steering=Vector3.zero;}
        public void SetWaitingPoint()
        {
            state=Activity.Walk;triedSeat=true;
            Vector3 point=world.Traffic.PlatformWaitingPoint(this);
            SetTarget(point,true);
        }
        bool TrySeat()
        {
            // A personal-space-oriented, less fatigued rider may prefer a real
            // nearby wall rather than consume a seat. Destination stays fixed.
            if(inTrain&&Journey.SeatPreference<.68f&&Journey.Personality.PersonalSpace>.45f&&world.Traffic.TryLeanPoint(this,riding,out var leanPoint)) {
                state=Activity.Walk;SetTarget(leanPoint,true);return true;
            }
            Metro3DFacility best=null;float score=float.NegativeInfinity;
            foreach(var f in world.facilities) {
                if(f==null||f.kind!=Metro3DFacilityKind.Seat||!f.Available||f.trainFacility!=inTrain||f.approach==null||!f.transform.IsChildOf(transform.parent)) continue;
                float d=FlatDistance(transform.position,f.approach.position);if(d>6) continue;
                float s=6-d;if(s>score) {best=f;score=s;}
            }
            if(best==null||!best.Claim(this)) return false;
            world.Traffic.ReleaseStanding(this);
            seat=best;state=Activity.Walk;SetTarget(best.approach.position,true);return true;
        }
        void SetTarget(Vector3 p,bool navigate) {
            Vector3 local=transform.parent.InverseTransformPoint(p);
            if((local-targetLocal).sqrMagnitude<.0025f) {
                target=p;
                if(navigate&&!pendingPath&&pathIndex>=pathCount&&FlatDistance(transform.position,p)>.4f&&Time.time>=retryAt) RequestPath();
                return;
            }
            target=p;targetLocal=local;pathCount=pathIndex=0;pendingPath=false;if(navigate) RequestPath();
        }
        void RequestPath() {
            if(crowd==null)return;
            if(Journey!=null&&Journey.OnStairs) {pendingPath=false;pathCount=pathIndex=0;return;}
            pendingPath=true;crowd.Request(this);
        }
        public void SolvePath(Metro3DNavigation navigation)
        {
            if(!pendingPath||!gameObject.activeInHierarchy) return;
            pathCount=navigation.Find(transform.parent,inTrain,transform.position,target,path,this);pathIndex=0;pendingPath=false;
            if(pathCount==0) {
                retryAt=Time.time+4;velocity=steering=Vector3.zero;
                if(seat!=null) {
                    ReleaseSeat();WaitJourney();triedSeat=true;
                    if(inTrain) {state=Activity.Walk;SetTarget(world.Traffic.TrainStandingPoint(this,riding),true);}
                }
                Journey.Planner.ReportRecoverableFailure("No collision-clear local path; destination retained");
            }
        }
        public void ClearDoorLanding()
            => ClearDoorLanding(transform.position);
        public void ClearDoorLanding(Vector3 point)
        {
            if(state==Activity.Sit||state==Activity.SitDown||state==Activity.StandUp||state==Activity.Alight||seat!=null) return;
            if(!inTrain&&!Journey.CanBoard)return;
            if(Time.time<clearYieldUntil||state==Activity.Walk&&Velocity.magnitude>.08f) return;
            // A stationary rider blocking an alighter/stand-up landing yields
            // to a genuinely free spot away from that landing, then resumes the
            // same journey. Repeated requests do not retarget moving people.
            clearYieldUntil=Time.time+1.5f;
            if(state==Activity.Board) {
                if(waypoint==0&&world.train!=null)FollowQueueGoal(world.Traffic.DoorApproach(this,world.train,door,true));
                return;
            }
            state=Activity.Walk;triedSeat=true;
            SetTarget(inTrain?world.Traffic.TrainStandingPoint(this,riding,point):world.Traffic.PlatformWaitingPoint(this,point),true);
        }
        void BeginSit()
        {
            state=Activity.SitDown;transitionTime=0;transitionFrom=transform.localPosition;transitionTo=transform.parent.InverseTransformPoint(seat.anchor.position);
            motor.enabled=false;velocity=steering=Velocity=Vector3.zero;animator.CrossFadeInFixedTime("SitDown",.12f);Animate(0);
        }
        void BeginStand()
        {
            // Do not animate a disabled capsule into an occupied aisle and then
            // enable it inside another passenger. Reserve and clear the landing.
            if(seat==null||!crowd.PrepareStandSpace(this,seat.approach.position)) return;
            state=Activity.StandUp;transitionTime=0;transitionFrom=transform.localPosition;transitionTo=transform.parent.InverseTransformPoint(seat.approach.position);
            transitionAnimatorSpeed=animator.speed;
            animator.CrossFadeInFixedTime("StandUp",.12f);Animate(0);
        }
        void UpdateTransition()
        {
            float nextTime=transitionTime+Time.deltaTime,p=Mathf.Clamp01(nextTime/.9f);
            Vector3 next=Vector3.Lerp(transitionFrom,transitionTo,p*p*(3-2*p));
            if(state==Activity.StandUp&&!crowd.SpaceFree(transform.parent.TransformPoint(next),this,.49f)) {
                if(!transitionPaused) {transitionPaused=true;transitionBlockedSince=Time.time;}
                animator.speed=0;Velocity=Vector3.zero;return;
            }
            ResumeTransitionAnimation();transitionTime=nextTime;
            // A seat/stand transition stays in carriage space during arrival,
            // braking and departure. World-space endpoints left riders behind.
            transform.localPosition=next;
            transform.rotation=Quaternion.RotateTowards(transform.rotation,seat.anchor.rotation,Time.deltaTime*240);Velocity=Vector3.zero;
            if(p<1) return;
            if(state==Activity.SitDown) {state=Activity.Sit;animator.CrossFadeInFixedTime("Seated",.08f);}
            else {ReleaseSeat();motor.enabled=true;crowd.ReleaseStandSpace(this);WaitJourney();triedSeat=true;nextDecision=0;animator.CrossFadeInFixedTime("Locomotion",.12f);}
        }
        void ResumeTransitionAnimation() {if(transitionPaused&&animator!=null)animator.speed=transitionAnimatorSpeed;transitionPaused=false;}
        void ReleaseSeat() {if(seat!=null) {seat.Release(this);seat=null;}}
        public void ResetForPool() {ResumeTransitionAnimation();ClearQueueContext();ReleaseSeat();crowd.Unregister(this);motor.enabled=false;pendingPath=false;pathCount=0;Velocity=velocity=steering=Vector3.zero;}
        public void RelocateToPlatform() {ReleaseSeat();motor.enabled=false;transform.position=world.stations[stationIndex].TransformPoint(world.PlatformWaitingPoint(index%world.settings.passengerCount));motor.enabled=true;WaitJourney();triedSeat=false;}
        static float FlatDistance(Vector3 a,Vector3 b) {a.y=b.y;return Vector3.Distance(a,b);}
        void OnDisable() {if(crowd!=null) crowd.Unregister(this);}
        void OnDestroy() {ReleaseSeat();if(crowd!=null) crowd.Unregister(this);if(world!=null) world.passengers.Remove(this);}
    }
}

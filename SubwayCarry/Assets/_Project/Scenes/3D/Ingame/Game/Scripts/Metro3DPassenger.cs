using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public enum Metro3DWalkPhase { Still, Starting, Walking, Curving, Braking, Pivoting, FacilityAlignment, QueueAdvance, QueueWait, Blocked }
    public enum Metro3DStopReason { None, Arrived, PathPending, Queue, Tagging, Door, Turning, StaticObstacle, ReservedPassage, Surface, Passenger, Yield, Recovery, Activity, SimulationPaused, Avoidance, MotorContact }
    // Locomotion/activity executor. Journey intent lives in the existing V2 planner adapter.
    public sealed class Metro3DPassenger : MonoBehaviour
    {
        public Metro3DWorld world;
        public int index,stationIndex;
        public bool inTrain;
        // Explicit Walk-field opt-in only. Serialized ingame defaults remain unchanged.
        public bool WalkReviewCandidate {get;set;}
        public bool WalkReviewControlled {get;set;}
        public bool WalkReviewFrozen {get;set;}
        public bool WalkReviewAnimationCandidate {get;set;}
        float reviewAnimationSpeed;
        public float ReviewMeasuredWalkSpeed {get;set;}=1;
        public int CopyReviewPath(Vector3[] output){int n=Mathf.Min(output.Length,pathCount);System.Array.Copy(path,output,n);return n;}
        public void SetReviewPath(Vector3[] points,int count){if(!WalkReviewControlled)return;pathCount=Mathf.Min(count,path.Length);System.Array.Copy(points,path,pathCount);pathIndex=0;pendingPath=false;nextReviewLook=0;}
        public Vector3 CommandVelocity=>velocity;
        public bool FinalWalkTarget {get;private set;}
        internal bool CanWalkDirect(Vector3 point)=>crowd.CanShortenWalkingPath(this,point);
        Vector3 reviewLookLocal;
        float nextReviewLook;
        internal float MotionYawRate=>curvedTurnRate;
        internal bool MotionPivot=>pivoting;
        internal int CopyMotionPath(Vector3[] output)
        {
            if(pendingPath||output.Length==0)return 0;
            int count=0;output[count++]=TravelLookahead;
            for(int i=pathIndex+1;i<pathCount&&count<output.Length-1;i++)output[count++]=transform.parent.TransformPoint(path[i]);
            Vector3 destination=transform.parent.TransformPoint(targetLocal);
            if(count<output.Length&&(output[count-1]-destination).sqrMagnitude>.0025f)output[count++]=destination;
            return count;
        }
        public System.Guid PersistentId {get;private set;}
        public Metro3DMotionTrial MotionTrial {get;internal set;}
        public bool MotionMatchingLocomotion => state!=Activity.Sit&&state!=Activity.SitDown&&state!=Activity.StandUp&&Journey!=null&&!Journey.OnStairs&&Journey.GateAction!=Metro3DPassengerJourney.GatePhase.Tag;
        public Vector3 MotionPathPoint(float distance)
        {
            Vector3 from=transform.position;
            if(pendingPath)return from;
            for(int i=pathIndex;i<pathCount;i++) {
                Vector3 next=transform.parent.TransformPoint(path[i]);
                float length=Vector3.Distance(from,next);
                if(length>distance)return Vector3.Lerp(from,next,distance/length);
                distance-=length;from=next;
            }
            return Vector3.MoveTowards(from,transform.parent.TransformPoint(targetLocal),distance);
        }
        public Metro3DWalkPhase WalkPhase {get;private set;}
        public Metro3DStopReason StopReason {get;private set;}
        public Vector3 DesiredVelocity {get;private set;}
        public Vector3 AvoidanceVelocity => steering;
        public Vector3 PathWaypoint {get;private set;}
        public Vector3 TravelLookahead {get;private set;}
        public float RotationDegreesPerSecond {get;private set;}
        public float RotationTravel {get;private set;}
        public float WalkingAlignmentScale {get;private set;}
        public int PathPointIndex => pathIndex;
        public int PathPointCount => pathCount;
        public float BlockedSeconds => blockedFor;
        public float ComfortableTurnRate => Mathf.Lerp(105,145,Journey!=null?Mathf.Clamp01(Journey.Personality.Mobility):.5f);
        public float GateClearanceDistance(float halfLength) => halfLength+.24f+Mathf.Clamp(world.settings.npcSpeed*Journey.SpeedScale/(ComfortableTurnRate*Mathf.Deg2Rad),.35f,.7f)+.12f;
        bool pivoting;
        float curvedTurnRate;
        public int InitialDirection {get;set;}
        public string ActivityLabel => state==Activity.Board?"승차":state==Activity.Alight?"하차":state==Activity.Sit?"착석":state==Activity.SitDown?"앉는 중":state==Activity.StandUp?"일어서는 중":WaitingForQueue?"줄 대기":state==Activity.Walk?"이동":"대기";
        public Vector3 MovementIntent => state==Activity.Walk||state==Activity.Board||state==Activity.Alight?transform.parent.TransformPoint(targetLocal)-transform.position:Vector3.zero;
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
        public void ClearQueueContext() {queueUntil=0;QueueLeader=null;queuePermissionWait=false;FaceTravel();}
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
            FaceQueue();
        }
        void FaceQueue() {if(QueueCommitted)FaceJourney(QueueForward);else FaceTravel();}
        // Actions supply an orientation intent, never a second root-rotation writer.
        // Store it in the pedestrian frame so a moving/rotating carriage preserves its meaning.
        Vector3 facingLocal;
        bool hasFacingIntent;
        float facingTurnSpeed;
        public Vector3 BodyFacingIntent => hasFacingIntent?transform.parent.TransformDirection(facingLocal):Vector3.zero;
        public void FaceJourney(Vector3 direction,float degreesPerSecond=180)
        {
            direction.y=0;
            if(direction.sqrMagnitude<.0001f) {FaceTravel();return;}
            facingLocal=transform.parent.InverseTransformDirection(direction.normalized);facingTurnSpeed=Mathf.Clamp(degreesPerSecond,30,360);hasFacingIntent=true;
        }
        public void FaceTravel() {hasFacingIntent=false;facingLocal=Vector3.zero;}
        float ForwardWalkScale(Vector3 motion)
        {
            float alignment=motion.sqrMagnitude>.0001f?Vector3.Dot(transform.forward,motion.normalized):1;
            return Mathf.InverseLerp(.9659258f,.9902681f,alignment); // 15 -> 8 degrees
        }
        float ApplyBodyFacing(Vector3 motion,float deltaTime)
        {
            Vector3 direction=BodyFacingIntent;bool actionFacing=direction.sqrMagnitude>.0001f;
            // A waiting/interaction orientation is not a strafing animation.
            // Only a committed passage keeps its action axis while moving.
            if(motion.sqrMagnitude>.008f&&!PortalCommitted)actionFacing=false;
            if(!actionFacing)direction=motion;
            direction.y=0;
            if(direction.sqrMagnitude<.008f)return 1;
            direction.Normalize();
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),deltaTime*(actionFacing?facingTurnSpeed:240));
            return Mathf.Clamp01(Vector3.Dot(transform.forward,direction));
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
        Metro3DTrain boardingTrain;
        Metro3DTrain egressTrain;
        int egressDoor;
        Vector3 egressPoint;
        public bool ClearingDoor(Metro3DTrain train,int number)
            => train!=null&&!inTrain&&egressTrain==train&&number==egressDoor&&(transform.localPosition-egressPoint).sqrMagnitude<1.25f*1.25f;
        Animator animator;
        CharacterController motor;
        Metro3DCrowd crowd;
        readonly Vector3[] path=new Vector3[384];
        Vector3[] continuationPath;
        Vector3 continuationFrom,continuationGoal;
        int continuationCount;
        bool continuationPending,continuationReady;
        public bool ContinuationReady => continuationReady;
        public float AnimationSpeed => animator!=null?animator.GetFloat(Speed):0;
        public int AnimationState => animator!=null?animator.GetCurrentAnimatorStateInfo(0).shortNameHash:0;
        public bool AnimationTransition => animator!=null&&animator.IsInTransition(0);
        int pathCount,pathIndex,waypoint,door;
        bool pendingPath,triedSeat;
        Vector3 target,targetLocal,transitionFrom,transitionTo,velocity,steering;
        float nextDecision,nextSteering,verticalSpeed,transitionTime,blockedFor,retryAt;
        // Native clip sampling: 1.333s cycle, median planted-foot travel
        // 0.996m/s at the preserved mannequin's 0.9731136 visual scale.
        const float WalkReferenceSpeed=1f,WalkBlendSpeed=.12f;
        float walkReferenceSpeed=WalkReferenceSpeed;
        float helperUntil,clearYieldUntil;
        bool helperTaskActive;
        bool transitionPaused;
        float transitionAnimatorSpeed=1,transitionBlockedSince;
        public float TransitionBlockedFor => transitionPaused?Time.time-transitionBlockedSince:0;
        static readonly int Speed=Animator.StringToHash("Speed"),Seated=Animator.StringToHash("Seated"),Gait=Animator.StringToHash("Gait");
        public void Initialize(Metro3DWorld owner,int id,int stop,bool inside,bool existing,bool far)
        {
            WalkReviewCandidate=WalkReviewControlled=WalkReviewFrozen=WalkReviewAnimationCandidate=false;nextReviewLook=reviewAnimationSpeed=0;
            if(MotionTrial!=null)MotionTrial.Release();
            if(crowd!=null)crowd.Unregister(this);
            PersistentId=System.Guid.NewGuid();pivoting=false;curvedTurnRate=RotationTravel=0;StopReason=Metro3DStopReason.None;WalkPhase=Metro3DWalkPhase.Still;
            DesiredVelocity=PathWaypoint=TravelLookahead=Vector3.zero;RotationDegreesPerSecond=0;WalkingAlignmentScale=1;
            world=owner;index=id;stationIndex=stop;inTrain=inside;helperUntil=clearYieldUntil=0;helperTaskActive=false;
            queueUntil=nextQueuePath=yieldUntil=backoffUntil=0;recoveryStage=PassingSide=0;PassingUntil=0;QueueLeader=null;
            animator=GetComponentInChildren<Animator>(true);motor=GetComponent<CharacterController>();crowd=world.GetComponent<Metro3DCrowd>();
            ResumeTransitionAnimation();egressTrain=null;
            riding=inside?GetComponentInParent<Metro3DTrain>():null;boardingTrain=null;seat=null;triedSeat=pendingPath=false;pathCount=pathIndex=waypoint=0;
            velocity=steering=Velocity=Vector3.zero;verticalSpeed=blockedFor=0;state=Activity.Wait;target=transform.position;targetLocal=transform.localPosition;
            FaceTravel();
            CancelJourneyContinuation();
            nextDecision=Time.time+(id%13)*.025f;nextSteering=Time.time+(id%7)*.013f;motor.enabled=true;
            motor.radius=.24f;motor.skinWidth=.025f;
            // Acceleration starts below the prefab's old 1mm movement cutoff.
            // Discarding those steps pins measured-velocity feedback at zero.
            motor.minMoveDistance=0;
            var contacts=GetComponent<Metro3DPassengerContacts>();if(contacts==null) contacts=gameObject.AddComponent<Metro3DPassengerContacts>();contacts.Initialize(this);
            Journey=GetComponent<Metro3DPassengerJourney>();if(Journey==null) Journey=gameObject.AddComponent<Metro3DPassengerJourney>();
            Journey.Initialize(this,stop,inside,existing,far);
            var colors=new[]{Color.white,new Color(.82f,.88f,1),new Color(1,.85f,.7f),new Color(.85f,.85f,.9f),new Color(.75f,.9f,.9f),new Color(.9f,.8f,1)};
            var tint=new MaterialPropertyBlock();tint.SetColor("_BaseColor",colors[id%colors.Length]);
            foreach(var renderer in GetComponentsInChildren<SkinnedMeshRenderer>()) {
                for(int slot=0;slot<renderer.sharedMaterials.Length;slot++)
                    if(renderer.sharedMaterials[slot]!=null&&renderer.sharedMaterials[slot].name=="CommuterCoat") renderer.SetPropertyBlock(tint,slot);
            }
            if(animator!=null) {
                animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
                walkReferenceSpeed=WalkReferenceSpeed*Mathf.Abs(animator.transform.lossyScale.z)/.9731136f;
            }
            if(isActiveAndEnabled)crowd.Register(this);
        }
        void OnEnable() {
            if(crowd!=null) crowd.Register(this);
            if(animator!=null) {animator.Rebind();animator.Play("Locomotion",0,(index*.173f)%1);}
        }
        void Update()
        {
            if(WalkReviewFrozen)return;
            if(world==null||world.session.SimulationPaused) {Velocity=Vector3.zero;Animate(0);return;}
            if(state==Activity.SitDown||state==Activity.StandUp) {UpdateTransition();return;}
            if(!WalkReviewControlled&&Time.time>=nextDecision) {nextDecision=Time.time+.25f;Decide();}Animate(Velocity.magnitude);
        }
        void FixedUpdate()
        {
            if(crowd==null||motor==null||!motor.enabled||world.session.SimulationPaused||WalkReviewFrozen) return;
            Quaternion beforeRotation=transform.rotation;
            if(WalkReviewControlled||WalkReviewCandidate&&Journey.ReviewStairActive)Journey.TickReviewStair();
            bool walking=state==Activity.Walk||state==Activity.Board||state==Activity.Alight;
            if(Journey.OnStairs) {pendingPath=false;pathCount=pathIndex=0;}
            target=transform.parent.TransformPoint(targetLocal);Vector3 goal=target;
            int previousPoint=pathIndex;
            if(pathIndex<pathCount) {
                goal=transform.parent.TransformPoint(path[pathIndex]);
                while(pathIndex<pathCount&&FlatDistance(transform.position,goal)<(inTrain?.09f:.2f)) {pathIndex++;goal=pathIndex<pathCount?transform.parent.TransformPoint(path[pathIndex]):target;}
                if(!WalkReviewCandidate&&walking&&!PortalCommitted&&!QueueCommitted&&pathIndex+1<pathCount) {
                    Vector3 next=transform.parent.TransformPoint(path[pathIndex+1]),segment=next-goal,offset=transform.position-goal;segment.y=offset.y=0;
                    float along=Vector3.Dot(offset,segment.normalized);
                    Vector3 projected=goal+segment.normalized*Mathf.Clamp(along,0,segment.magnitude);
                    Vector3 ahead=Vector3.MoveTowards(projected,next,.65f);
                    // A rounded corner need not touch its mathematical vertex.
                    // Consume it only after entering the next visible corridor.
                    if(along>.02f&&FlatDistance(transform.position,goal)<1.1f&&crowd.CanFollowWalkingSegment(this,ahead)) {pathIndex++;goal=next;}
                }
                // Consume small grid zigzags only when the next segment is
                // physically visible. Keep real corners/queue alignment intact.
                if(!WalkReviewCandidate&&walking&&!PortalCommitted&&!QueueCommitted&&Time.time>=nextSteering)
                    while(pathIndex+1<pathCount&&FlatDistance(transform.position,goal)<Mathf.Clamp(.2f+Velocity.magnitude*.22f,.25f,.5f)&&
                        crowd.CanApproachDirect(this,transform.parent.TransformPoint(path[pathIndex+1]))) {
                        pathIndex++;goal=transform.parent.TransformPoint(path[pathIndex]);
                    }
            }
            if(pathIndex!=previousPoint)nextSteering=0;
            PathWaypoint=goal;
            // Look through a visible bend, not through its wall or a reserved
            // passage. The original polyline and its completion points remain.
            if(!WalkReviewCandidate&&walking&&!PortalCommitted&&!QueueCommitted&&pathIndex+1<pathCount) {
                Vector3 next=transform.parent.TransformPoint(path[pathIndex+1]);
                Vector3 incoming=goal-transform.position,outgoingSegment=next-goal;incoming.y=outgoingSegment.y=0;
                float look=Mathf.Clamp(world.settings.npcSpeed*Journey.SpeedScale*.65f,.5f,1.1f);
                if(incoming.magnitude<look&&outgoingSegment.sqrMagnitude>.01f&&Vector3.Dot(incoming.normalized,outgoingSegment.normalized)>-.5f) {
                    Vector3 ahead=Vector3.MoveTowards(goal,next,look-incoming.magnitude);
                    if(crowd.CanFollowWalkingSegment(this,ahead))goal=ahead;
                }
            }
            if(WalkReviewCandidate&&walking&&!PortalCommitted&&!QueueCommitted&&!Journey.OnStairs)goal=ReviewPathTarget(goal);
            TravelLookahead=goal;
            Vector3 delta=goal-transform.position;delta.y=0;
            SteeringDistance=delta.magnitude;
            float speed=world.settings.npcSpeed*Journey.SpeedScale;
            Vector3 outgoing=Vector3.zero;
            if(pathIndex+1<pathCount) {outgoing=transform.parent.TransformDirection(path[pathIndex+1]-path[pathIndex]);outgoing.y=0;}
            FinalWalkTarget=pathIndex>=pathCount||pathIndex==pathCount-1&&FlatDistance(goal,target)<.18f;
            float requested=WalkReviewCandidate&&!PortalCommitted&&!QueueCommitted
                ?(FinalWalkTarget&&!Journey.StairApproaching?Mathf.Min(speed,Metro3DWalkMotion.ArrivalSpeed(FlatDistance(transform.position,target))):speed)
                :Mathf.Min(speed,delta.magnitude*3.2f);
            if(outgoing.sqrMagnitude>.001f&&delta.sqrMagnitude>.001f) {
                // Brake before a bend, including the steering sample's latency.
                // Otherwise inertia carries the walker past a short turn node,
                // then its valid path points backwards into following traffic.
                float cornerSpeed=speed*Mathf.Lerp(.55f,1,Mathf.Max(0,Vector3.Dot(delta.normalized,outgoing.normalized)));
                float remaining=Mathf.Max(0,delta.magnitude-(inTrain?.09f:.2f)-speed*.1f);
                requested=Mathf.Min(requested,Mathf.Sqrt(cornerSpeed*cornerSpeed+2*3.8f*remaining));
            }
            Vector3 desired=walking&&!pendingPath&&delta.magnitude>.07f?delta.normalized*requested:Vector3.zero;
            DesiredVelocity=desired;
            if(Time.time>=nextSteering) {nextSteering=Time.time+.1f;steering=desired.sqrMagnitude>.001f?crowd.SafeVelocity(this,desired):Vector3.zero;SteeringCost=crowd.LastSteeringCost;StaticRejects=crowd.LastStaticRejects;}
            if(!walking||pendingPath||delta.magnitude<=.07f) steering=Vector3.zero;
            if(Time.time<yieldUntil) steering=Vector3.zero;
            else if(Time.time<backoffUntil) {
                if((transform.localPosition-backoffFrom).sqrMagnitude<.28f*.28f) steering=recoveryVelocity;
                else backoffUntil=0;
            }
            velocity=WalkingStep(steering,walking,Time.fixedDeltaTime);
            RotationDegreesPerSecond=Quaternion.Angle(beforeRotation,transform.rotation)/Time.fixedDeltaTime;
            RotationTravel+=RotationDegreesPerSecond*Time.fixedDeltaTime;
            verticalSpeed=motor.isGrounded?-2f:verticalSpeed-20*Time.fixedDeltaTime;
            Vector3 before=transform.position;motor.Move((velocity+Vector3.up*verticalSpeed)*Time.fixedDeltaTime);
            Vector3 actual=(transform.position-before)/Time.fixedDeltaTime;Velocity=new Vector3(actual.x,0,actual.z);
            Journey.FinishGatePassage();
            UpdateStopReason(walking);
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
        Vector3 WalkingStep(Vector3 preferred,bool walking,float dt)
        {
            if(WalkReviewCandidate&&!PortalCommitted&&!QueueCommitted) {
                Vector3 wanted=walking?preferred:Vector3.zero;
                // Social intent is cached at 10Hz. Braking to a final goal is
                // a Motor constraint and must use this physics step's distance.
                // Otherwise a stale speed overruns the goal and requests a pivot.
                if(FinalWalkTarget&&!Journey.StairApproaching)wanted=Vector3.ClampMagnitude(wanted,Metro3DWalkMotion.ArrivalSpeed(FlatDistance(transform.position,target)));
                Vector3 arc=Metro3DWalkMotion.Step(transform.forward,Velocity,wanted,ComfortableTurnRate,dt,ref curvedTurnRate,ref pivoting,out var facing);
                ApplyBodyFacing(facing*Mathf.Max(.1f,arc.magnitude),dt);
                WalkingAlignmentScale=1;
                arc=crowd.ValidateWalkingVelocity(this,arc);
                WalkPhase=pivoting?Metro3DWalkPhase.Pivoting:arc.sqrMagnitude<.0001f&&wanted.sqrMagnitude>.0025f?Metro3DWalkPhase.Blocked:
                    wanted.sqrMagnitude<.0025f?Velocity.magnitude>.05f?Metro3DWalkPhase.Braking:Metro3DWalkPhase.Still:
                    Velocity.magnitude<.07f?Metro3DWalkPhase.Starting:Mathf.Abs(curvedTurnRate)>8?Metro3DWalkPhase.Curving:Metro3DWalkPhase.Walking;
                return arc;
            }
            float actualSpeed=Velocity.magnitude;
            bool movingIntent=walking&&preferred.sqrMagnitude>.0025f;
            float angle=movingIntent?Vector3.Angle(transform.forward,preferred):0;
            if(!PortalCommitted&&movingIntent&&angle>110)pivoting=true;
            if(!movingIntent||PortalCommitted||angle<10)pivoting=false;
            Vector3 candidate;
            if(PortalCommitted) {
                curvedTurnRate=0;WalkPhase=Metro3DWalkPhase.FacilityAlignment;
                ApplyBodyFacing(preferred.sqrMagnitude>.008f?preferred:Velocity,dt);
                candidate=Vector3.MoveTowards(Velocity,preferred,dt*(preferred.magnitude<actualSpeed?3.8f:2.3f));
            } else if(pivoting) {
                curvedTurnRate=0;WalkPhase=Metro3DWalkPhase.Pivoting;
                candidate=Vector3.MoveTowards(Velocity,Vector3.zero,dt*3.8f);
                ApplyBodyFacing(actualSpeed>.1f?Velocity:preferred,dt);
            } else if(movingIntent) {
                Vector3 from=actualSpeed>.07f?Velocity.normalized:transform.forward;
                float change=Vector3.SignedAngle(from,preferred,Vector3.up);
                float turn=Mathf.Sign(change)*Mathf.Min(ComfortableTurnRate,Mathf.Sqrt(2*540*Mathf.Abs(change)));
                curvedTurnRate=Mathf.MoveTowards(curvedTurnRate,turn,dt*540);
                if(Mathf.Abs(curvedTurnRate*dt)>Mathf.Abs(change))curvedTurnRate=change/dt;
                Vector3 direction=Quaternion.AngleAxis(curvedTurnRate*dt,Vector3.up)*from;
                float nextSpeed=Mathf.MoveTowards(actualSpeed,preferred.magnitude,dt*(preferred.magnitude<actualSpeed?3.8f:2.3f));
                candidate=direction*nextSpeed;
                // Motor follows the feasible arc; the body follows that same
                // motion. The social-navigation vector remains a separate intent.
                ApplyBodyFacing(direction*Mathf.Max(.1f,nextSpeed),dt);
                WalkPhase=actualSpeed<.07f?Metro3DWalkPhase.Starting:Mathf.Abs(curvedTurnRate)>8?Metro3DWalkPhase.Curving:QueueCommitted?Metro3DWalkPhase.QueueAdvance:Metro3DWalkPhase.Walking;
            } else {
                curvedTurnRate=Mathf.MoveTowards(curvedTurnRate,0,dt*540);
                candidate=Vector3.MoveTowards(Velocity,Vector3.zero,dt*3.8f);
                ApplyBodyFacing(candidate,dt);WalkPhase=actualSpeed>.05f?Metro3DWalkPhase.Braking:Metro3DWalkPhase.Still;
            }
            WalkingAlignmentScale=ForwardWalkScale(candidate);
            candidate=crowd.ValidateWalkingVelocity(this,candidate*WalkingAlignmentScale);
            if(candidate.sqrMagnitude<.0001f&&movingIntent&&!pivoting&&crowd.LastWalkingRejection!=Metro3DStopReason.None)WalkPhase=Metro3DWalkPhase.Blocked;
            return candidate;
        }
        Vector3 ReviewPathTarget(Vector3 fallback)
        {
            // Refresh with the existing 10Hz steering budget, not on every Motor tick.
            if(Time.time<nextReviewLook)return transform.parent.TransformPoint(reviewLookLocal);
            nextReviewLook=Time.time+.1f;
            Vector3 selected=fallback;
            if(pathIndex<pathCount) {
                // Clearance-aware string pulling over the existing grid polyline.
                // Far target first removes arbitrarily dense open-space nodes in one query.
                int at=pathCount-1,budget=8;
                while(at>=pathIndex&&budget-->0) {
                    Vector3 point=transform.parent.TransformPoint(path[at]);
                    if(crowd.CanShortenWalkingPath(this,point)) {pathIndex=at;selected=point;break;}
                    if(at==pathIndex)break;
                    at=pathIndex+(at-pathIndex)/2;
                }
                if(pathIndex==pathCount-1&&crowd.CanShortenWalkingPath(this,target))selected=target;
                // Preview the next tangent only where the complete capsule corridor is safe.
                if(pathIndex+1<pathCount) {
                    Vector3 next=transform.parent.TransformPoint(path[pathIndex+1]);
                    float look=Mathf.Clamp(.45f+Velocity.magnitude*.65f,.55f,1.35f);
                    float distance=FlatDistance(transform.position,selected);
                    if(distance<look) {
                        Vector3 ahead=Vector3.MoveTowards(selected,next,look-distance);
                        if(crowd.CanShortenWalkingPath(this,ahead))selected=ahead;
                    }
                }
            } else if(crowd.CanShortenWalkingPath(this,target))selected=target;
            reviewLookLocal=transform.parent.InverseTransformPoint(selected);return selected;
        }
        void UpdateStopReason(bool walking)
        {
            StopReason=Metro3DStopReason.None;
            if(Velocity.magnitude>.08f)return;
            // A slow, correctly executed start/brake is not a capsule contact.
            if(WalkReviewCandidate&&Velocity.sqrMagnitude>.0001f&&(Velocity-velocity).sqrMagnitude<.0004f)return;
            if(pendingPath)StopReason=Metro3DStopReason.PathPending;
            else if(Journey.GateAction==Metro3DPassengerJourney.GatePhase.Tag)StopReason=Metro3DStopReason.Tagging;
            else if(Journey.GateAction==Metro3DPassengerJourney.GatePhase.DoorWait)StopReason=Metro3DStopReason.Door;
            else if(ExpectedQueueWait){StopReason=Metro3DStopReason.Queue;WalkPhase=Metro3DWalkPhase.QueueWait;}
            else if(pivoting||WalkingAlignmentScale<.1f&&steering.sqrMagnitude>.01f)StopReason=Metro3DStopReason.Turning;
            else if(Time.time<yieldUntil)StopReason=Metro3DStopReason.Yield;
            else if(Time.time<backoffUntil)StopReason=Metro3DStopReason.Recovery;
            else if(crowd.LastWalkingRejection!=Metro3DStopReason.None&&steering.sqrMagnitude>.001f)StopReason=crowd.LastWalkingRejection;
            else if(walking&&velocity.sqrMagnitude>.001f)StopReason=Metro3DStopReason.MotorContact;
            else if(walking&&DesiredVelocity.sqrMagnitude>.01f&&steering.sqrMagnitude<.001f)StopReason=Metro3DStopReason.Avoidance;
            else StopReason=walking?Metro3DStopReason.Arrived:Metro3DStopReason.Activity;
        }
        void Animate(float speed)
        {
            if(animator==null) return;
            if(WalkReviewAnimationCandidate){reviewAnimationSpeed=Mathf.MoveTowards(reviewAnimationSpeed,speed,(speed<.025f?12:4)*Time.deltaTime);if(speed<.025f)reviewAnimationSpeed=0;speed=reviewAnimationSpeed;}
            // Velocity measures only this capsule's own Move, excluding the
            // carriage transform's translation/rotation before that step.
            if(speed<.025f) {animator.SetFloat(Speed,0);animator.SetFloat(Gait,1);}
            else {
                animator.SetFloat(Speed,speed,.06f,Time.deltaTime);
                float walkWeight=Mathf.Clamp01(animator.GetFloat(Speed)/WalkBlendSpeed);
                float reference=WalkReviewControlled&&WalkReviewAnimationCandidate?ReviewMeasuredWalkSpeed:walkReferenceSpeed;
                animator.SetFloat(Gait,Mathf.Clamp(speed/(Mathf.Max(.1f,reference)*Mathf.Max(.05f,walkWeight)),.02f,2.5f));
            }
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
            var active=boardingTrain!=null?boardingTrain:world.Transit.BoardingTrain(this);
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
            if(!inTrain&&Journey.CanBoard&&active!=null&&active.CanBoard&&active.CurrentStop==stationIndex) {
                if(state==Activity.Sit) {BeginStand();return;}
                if(state!=Activity.Board) {ReleaseSeat();world.Traffic.ReleaseStanding(this);boardingTrain=active;door=active.NearestDoor(transform.position);waypoint=0;state=Activity.Board;Journey.BeginBoard();}
                AdvanceDoor(true);return;
            }
            if(state==Activity.Board&&(active==null||!active.CanBoard)&&waypoint==0) {
                world.Traffic.ReleaseDoor(this);boardingTrain=null;Journey.CancelBoard();SetWaitingPoint();return;
            }
            if(state==Activity.Board) {AdvanceDoor(true);return;}
            if(state==Activity.Sit||state==Activity.SitDown||state==Activity.StandUp) return;
            if(state==Activity.Walk&&FlatDistance(transform.position,target)<.23f) {if(seat!=null) {BeginSit();return;}WaitJourney();}
            if(state==Activity.Wait)SetWaitingFacing();
            if(state==Activity.Wait&&!triedSeat) {
                triedSeat=true;
                if(inTrain&&!(Journey.SeatPreference>.48f&&TrySeat())) {state=Activity.Walk;SetTarget(world.Traffic.TrainStandingPoint(this,riding),true);}
                Journey.ActivityChosen();
            }
        }
        void SetWaitingFacing()
        {
            if(!inTrain) {
                if(Journey.Waiting)FaceJourney(world.stations[stationIndex].TransformDirection(Vector3.right*Journey.TrainSide));
                return;
            }
            var support=world.Traffic.SupportFor(this);
            if(support!=null)FaceJourney(support.lean?riding.transform.TransformDirection(support.normal):riding.transform.forward*(index%2==0?1:-1),60);
        }
        void AdvanceDoor(bool boarding)
        {
            Metro3DTrain t=boarding?boardingTrain:riding;if(t==null) return;
            if(!boarding)crowd.ClearLanding(t,door,this);
            if(waypoint==0) {
                Vector3 approach=world.Traffic.DoorApproach(this,t,door,boarding);
                if((target-approach).sqrMagnitude>.0025f) FollowQueueGoal(approach);
                else FaceQueue();
                // A branch head watches its actual opening; followers watch the queue flow.
                if(QueueLeader==null&&FlatDistance(transform.position,approach)<.28f)
                    FaceJourney(t.DoorPoint(door,!boarding)-transform.position);
                bool alreadyAtDoor=!boarding&&FlatDistance(transform.position,t.DoorPoint(door,false))<.25f;
                if(!t.DoorsOpen||(!alreadyAtDoor&&FlatDistance(transform.position,approach)>.28f)||!world.Traffic.TryDoor(this,t,door,boarding)) return;
                // Queue beside the door, align in front of its opening, then
                // cross straight through. A diagonal from the side queue hits
                // the fixed glazing even when the opening itself is clear.
                waypoint=1;ClearQueueContext();SetTarget(t.DoorPoint(door,boarding),false);
            }
            if(waypoint==1) {
                if(FlatDistance(transform.position,t.DoorPoint(door,boarding))<.35f)FaceJourney(t.DoorPoint(door,!boarding)-t.DoorPoint(door,boarding));
                if(FlatDistance(transform.position,t.DoorPoint(door,boarding))>.15f) return;
                waypoint=2;SetTarget(t.DoorPoint(door,!boarding),false);
            }
            if(waypoint!=2) return;
            FaceJourney(t.DoorPoint(door,!boarding)-t.DoorPoint(door,boarding));
            Vector3 local=t.transform.InverseTransformPoint(transform.position);
            // Completion is the feet crossing the physical boundary, not hitting one occupied point exactly.
            bool crossed=boarding?t.Contains(transform.position)&&Mathf.Abs(local.x)<1.1f
                :(-t.side*world.stations[t.CurrentStop].InverseTransformPoint(transform.position).x)>Metro3DWorld.ScreenDoorX+.24f;
            if(!crossed) return;
            world.Traffic.ReleaseDoor(this);var old=transform.parent;
            inTrain=boarding;stationIndex=t.CurrentStop;transform.SetParent(boarding?t.transform:world.stations[stationIndex],true);
            egressTrain=boarding?null:t;egressDoor=door;
            if(!boarding)egressPoint=transform.parent.InverseTransformPoint(t.DoorPoint(door,true));
            world.Traffic.ChangedParent(old,transform.parent);riding=boarding?t:null;boardingTrain=null;triedSeat=false;WaitJourney();
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
        public void WaitJourney() {state=Activity.Wait;target=transform.position;targetLocal=transform.localPosition;pendingPath=false;pathCount=pathIndex=0;steering=Vector3.zero;FaceTravel();}
        public void HoldJourney() {state=Activity.Wait;pendingPath=false;pathCount=pathIndex=0;steering=Vector3.zero;}
        internal void InitializeWalkingReview(Metro3DWorld owner,int id)
        {
            Initialize(owner,id,0,false,false,false);WalkReviewControlled=true;
            GetComponent<Metro3DPassengerContacts>().enabled=false;
        }
        public void PrepareJourneyContinuation(Vector3 from,Vector3 goal)
        {
            if(continuationPending||continuationReady)return;
            if(continuationPath==null)continuationPath=new Vector3[path.Length];
            continuationFrom=from;continuationGoal=goal;continuationPending=true;crowd.Request(this);
        }
        public void CancelJourneyContinuation() {continuationPending=continuationReady=false;continuationCount=0;}
        public bool ContinueJourney(Vector3 goal)
        {
            bool ready=continuationReady&&continuationCount>0&&(goal-continuationGoal).sqrMagnitude<.01f;
            FaceTravel();
            state=Activity.Walk;target=goal;targetLocal=transform.parent.InverseTransformPoint(goal);pathIndex=0;pendingPath=false;nextSteering=0;
            if(ready) {pathCount=continuationCount;System.Array.Copy(continuationPath,path,pathCount);}
            else {pathCount=0;RequestPath();}
            CancelJourneyContinuation();return ready;
        }
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
            FaceTravel();target=p;targetLocal=local;pathCount=pathIndex=0;pendingPath=false;nextReviewLook=0;if(navigate) RequestPath();
        }
        void RequestPath() {
            if(crowd==null)return;
            if(Journey!=null&&Journey.OnStairs) {pendingPath=false;pathCount=pathIndex=0;return;}
            pendingPath=true;crowd.Request(this);
        }
        public void SolvePath(Metro3DNavigation navigation)
        {
            if(!gameObject.activeInHierarchy)return;
            if(!pendingPath) {
                if(continuationPending) {
                    continuationCount=navigation.Find(transform.parent,false,continuationFrom,continuationGoal,continuationPath,this);
                    continuationPending=false;continuationReady=continuationCount>0;
                }
                return;
            }
            pathCount=navigation.Find(transform.parent,inTrain,transform.position,target,path,this);pathIndex=0;pendingPath=false;
            if(continuationPending)crowd.Request(this);
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
                if(waypoint==0&&boardingTrain!=null)FollowQueueGoal(world.Traffic.DoorApproach(this,boardingTrain,door,true));
                return;
            }
            state=Activity.Walk;triedSeat=true;
            SetTarget(inTrain?world.Traffic.TrainStandingPoint(this,riding,point):world.Traffic.PlatformWaitingPoint(this,point),true);
        }
        void BeginSit()
        {
            state=Activity.SitDown;transitionTime=0;transitionFrom=transform.localPosition;transitionTo=transform.parent.InverseTransformPoint(seat.anchor.position);
            motor.enabled=false;velocity=steering=Velocity=Vector3.zero;animator.CrossFadeInFixedTime("SitDown",.12f);MotionTrial?.CrossFadeLegacy("SitDown",.12f);Animate(0);
        }
        void BeginStand()
        {
            // Do not animate a disabled capsule into an occupied aisle and then
            // enable it inside another passenger. Reserve and clear the landing.
            if(seat==null||!crowd.PrepareStandSpace(this,seat.approach.position)) return;
            state=Activity.StandUp;transitionTime=0;transitionFrom=transform.localPosition;transitionTo=transform.parent.InverseTransformPoint(seat.approach.position);
            transitionAnimatorSpeed=animator.speed;
            animator.CrossFadeInFixedTime("StandUp",.12f);MotionTrial?.CrossFadeLegacy("StandUp",.12f);Animate(0);
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
            if(state==Activity.SitDown) {state=Activity.Sit;animator.CrossFadeInFixedTime("Seated",.08f);MotionTrial?.CrossFadeLegacy("Seated",.08f);}
            else {ReleaseSeat();motor.enabled=true;crowd.ReleaseStandSpace(this);WaitJourney();triedSeat=true;nextDecision=0;animator.CrossFadeInFixedTime("Locomotion",.12f);MotionTrial?.CrossFadeLegacy("Locomotion",.12f);}
        }
        void ResumeTransitionAnimation() {if(transitionPaused&&animator!=null)animator.speed=transitionAnimatorSpeed;transitionPaused=false;}
        void ReleaseSeat() {if(seat!=null) {seat.Release(this);seat=null;}}
        public void ResetForPool() {if(MotionTrial!=null)MotionTrial.Release();ResumeTransitionAnimation();Journey.CancelGate();CancelJourneyContinuation();ClearQueueContext();ReleaseSeat();crowd.Unregister(this);motor.enabled=false;pendingPath=false;pathCount=0;Velocity=velocity=steering=Vector3.zero;}
        public void RelocateToPlatform() {ReleaseSeat();motor.enabled=false;transform.position=world.stations[stationIndex].TransformPoint(world.PlatformWaitingPoint(index%world.settings.passengerCount));motor.enabled=true;WaitJourney();triedSeat=false;}
        static float FlatDistance(Vector3 a,Vector3 b) {a.y=b.y;return Vector3.Distance(a,b);}
        void OnDisable() {FaceTravel();if(crowd!=null) crowd.Unregister(this);}
        void OnDestroy() {ReleaseSeat();if(crowd!=null) crowd.Unregister(this);if(world!=null) world.passengers.Remove(this);}
    }
}

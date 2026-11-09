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
        public float GateClearanceDistance(float halfLength) => halfLength+WalkingRadius+.14f;
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
        internal bool LearnedSteeringActive {get;set;}
        internal string ActiveSteeringPolicy => LearnedSteeringActive&&crowd!=null?crowd.SteeringPolicy:"규칙";
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
        internal const float StandingRadius=.24f, NarrowRadius=.19f;
        internal Metro3DPassenger YieldingFor {get;private set;}
        internal Vector3 YieldSpot {get;private set;}
        internal Vector3 YieldOrigin {get;private set;}
        internal Vector3 YieldFlow {get;private set;}
        internal float YieldLimit {get;private set;}
        internal bool FlowYieldActive=>YieldLimit>0;
        internal bool RejoiningFlow {get;private set;}
        internal bool FollowingNarrowFlow {get;set;}
        float flowApproachUntil;
        internal bool ContinueNarrowFlow(Vector3 origin,Vector3 direction)
        {
            float progress=Vector3.Dot(origin-YieldOrigin,YieldFlow);
            return Time.time<flowApproachUntil&&progress>-.4f&&progress<2.85f&&Vector3.Dot(direction,YieldFlow)>.72f;
        }
        internal void FinishFlowRejoin()
        {
            ClearFlowYield();
            flowApproachUntil=Time.time+8f;
        }
        internal void RejoinFlow() {RejoiningFlow=true;YieldLimit=Time.time+4;FaceTravel();}
        System.Guid yieldedRider;
        internal bool HasYieldingRider => YieldingFor!=null&&YieldingFor.isActiveAndEnabled&&YieldingFor.PersistentId==yieldedRider;
        internal bool CanYieldToFlow => state==Activity.Walk&&!inTrain&&!WalkReviewCandidate&&!PortalCommitted&&!QueueActive&&
            Journey.ReservedGate==null&&Journey.StairForward.sqrMagnitude<.5f;
        internal void YieldToFlow(Metro3DPassenger other,Vector3 point,Vector3 returnPoint,Vector3 forward)
        {
            YieldingFor=other;yieldedRider=other.PersistentId;YieldSpot=point;
            YieldOrigin=returnPoint;YieldFlow=forward;YieldLimit=Time.time+12f;flowApproachUntil=0;
        }
        internal void ClearFlowYield()
        {
            if(!FlowYieldActive)return;
            YieldingFor=null;YieldLimit=0;RejoiningFlow=false;FaceTravel();
        }
        internal float WalkingRadius {get;private set;}=StandingRadius;
        void SetWalkingRadius(float radius)
        {
            motor.radius=radius;
            WalkingRadius=radius;
        }
        internal float NarrowPoseWeight {get;private set;}
        internal int NarrowPoseSide {get;private set;}
        internal bool NarrowWalking => narrowUntil>Time.time||WalkingRadius<StandingRadius-.01f;
        float narrowUntil,stairPassUntil,stairPassOffset;
        internal void RememberStairPass()=>stairPassUntil=Time.time+.62f;
        internal float UpdateStairPassOffset()
        {
            stairPassOffset=Mathf.MoveTowards(stairPassOffset,Time.time<stairPassUntil?.15f:0,.045f);
            return stairPassOffset;
        }
        int recoveryStage;
        Vector3 recoveryVelocity,backoffFrom;
        public void SetQueueContext(Metro3DPassenger leader,Vector3 direction,float gap,bool permissionWait)
        {
            QueueLeader=leader;QueueForward=direction.normalized;QueueGap=gap;
            queuePermissionWait=permissionWait;queueUntil=Time.time+.8f;
        }
        public void ClearQueueContext() {queueUntil=0;QueueLeader=null;queuePermissionWait=false;FaceTravel();}
        public void CommitPassingSide(int side) {PassingSide=side;PassingUntil=Time.time+.85f;}
        public void FollowQueueGoal(Vector3 point,bool preferDirect=false)
        {
            // Queue following is locomotion inside a facility action, not a
            // transition out of Board/Alight. Losing that action resets its
            // alignment waypoint on the next decision tick.
            if(state!=Activity.Board&&state!=Activity.Alight) state=Activity.Walk;
            bool direct=preferDirect?crowd.CanFollowWalkingSegment(this,point):crowd.CanApproachDirect(this,point);
            if(direct) {
                if(preferDirect&&(pendingPath||pathIndex<pathCount)) {
                    // A queued path can keep an obsolete near point even though its destination
                    // is now reachable directly. Switch the active path, not just its target.
                    pendingPath=false;pathCount=pathIndex=0;nextSteering=0;
                }
                SetTarget(point,false);
            }
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
        Metro3DSeatedPastime seatedPastime;
        Metro3DPassengerContacts contacts;
        public string PhoneActivity => contacts?.PhoneLabel ?? "";
        enum TransitNotice { None, Board, PrepareAlight, Alight }
        TransitNotice noticedTransit;
        Metro3DTrain noticedTrain;
        float transitResponseAt, transitLookUntil;
        internal bool PhoneTransitLook => DoorCrossing||Time.time<transitLookUntil;
        internal bool SettledStanding => motor!=null&&motor.enabled&&motor.isGrounded&&seat==null&&!DoorCrossing&&
            !NarrowWalking&&!Journey.OnStairs&&Velocity.sqrMagnitude<.055f*.055f&&
            (state==Activity.Wait||ExpectedQueueWait);

        bool NoticeTransit(TransitNotice cue,Metro3DTrain train)
        {
            if(cue!=noticedTransit||train!=noticedTrain) {
                noticedTransit=cue;noticedTrain=train;
                float response=contacts?.HasPhoneInUse==true?QueueReactionDelay:0;
                transitResponseAt=Time.time+response;
                transitLookUntil=cue==TransitNotice.None?0:transitResponseAt+.95f;
            }
            return Time.time>=transitResponseAt;
        }

        internal bool PhoneSituation(bool continuing,out bool walking)
        {
            bool doorActivity=state==Activity.Board||state==Activity.Alight;
            walking=state==Activity.Walk||doorActivity;
            if(seat!=null||MotionTrial!=null||world.walkingReviewOnly||world.Transit==null||
                WalkReviewCandidate||WalkReviewControlled||helperTaskActive||NarrowWalking||NarrowPoseWeight>.001f||
                Journey.OnStairs||Journey.StairApproaching||!continuing&&(FlowYieldActive||RecoveryActive))return false;
            if(doorActivity)return continuing;
            if(QueueActive||PortalCommitted)return false;
            if(!walking)return state==Activity.Wait&&(inTrain||Journey.Waiting);
            var gate=Journey.ReservedGate;
            return Journey.GateAction==Metro3DPassengerJourney.GatePhase.None||
                Journey.GateAction==Metro3DPassengerJourney.GatePhase.Approach&&
                (gate==null||FlatDistance(transform.position,gate.transform.position)>2.2f);
        }
        public string SeatedPastimeLabel => seatedPastime?.Label ?? "";
        public string SeatedPastimeStage => seatedPastime?.Stage ?? "";
        internal void ApplySeatedPastimePose() => seatedPastime?.ApplyPose();
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
        const float AlightingLeadSeconds = 12;
        enum Activity { Walk,Wait,SitDown,Sit,StandUp,Board,Alight }
        Activity state;
        Metro3DFacility seat;
        float nextPlatformRestCheck,nextIdleGroundProbe;
        bool checkedMovingSupport;
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
        int pathCount,pathIndex,waypoint,door,alightingDoorsTried;
        float blockedAlightingDoorSince;
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
            seatedPastime?.Clear();seatedPastime=null;
            if(crowd!=null)crowd.Unregister(this);
            noticedTransit=TransitNotice.None;noticedTrain=null;transitResponseAt=transitLookUntil=0;
            PersistentId=System.Guid.NewGuid();pivoting=false;curvedTurnRate=RotationTravel=0;StopReason=Metro3DStopReason.None;WalkPhase=Metro3DWalkPhase.Still;
            DesiredVelocity=PathWaypoint=TravelLookahead=Vector3.zero;RotationDegreesPerSecond=0;WalkingAlignmentScale=1;
            world=owner;index=id;stationIndex=stop;inTrain=inside;helperUntil=clearYieldUntil=0;helperTaskActive=false;
            queueUntil=nextQueuePath=yieldUntil=backoffUntil=0;recoveryStage=PassingSide=0;PassingUntil=0;QueueLeader=null;
            YieldingFor=null;YieldLimit=flowApproachUntil=0;RejoiningFlow=FollowingNarrowFlow=false;
            animator=GetComponentInChildren<Animator>(true);motor=GetComponent<CharacterController>();crowd=world.GetComponent<Metro3DCrowd>();
            ResumeTransitionAnimation();egressTrain=null;
            riding=inside?GetComponentInParent<Metro3DTrain>():null;boardingTrain=null;seat=null;triedSeat=pendingPath=false;pathCount=pathIndex=waypoint=0;
            velocity=steering=Velocity=Vector3.zero;verticalSpeed=blockedFor=0;state=Activity.Wait;target=transform.position;targetLocal=transform.localPosition;
            FaceTravel();
            CancelJourneyContinuation();
            nextPlatformRestCheck=Time.time+2+(id%9)*.21f;checkedMovingSupport=false;nextIdleGroundProbe=0;
            nextDecision=Time.time+(id%13)*.025f;nextSteering=Time.time+(id%7)*.013f;motor.enabled=true;
            SetWalkingRadius(StandingRadius);motor.skinWidth=.025f;
            NarrowPoseWeight=narrowUntil=stairPassUntil=stairPassOffset=0;NarrowPoseSide=0;
            // Acceleration starts below the prefab's old 1mm movement cutoff.
            // Discarding those steps pins measured-velocity feedback at zero.
            motor.minMoveDistance=0;
            contacts=GetComponent<Metro3DPassengerContacts>();if(contacts==null) contacts=gameObject.AddComponent<Metro3DPassengerContacts>();contacts.Initialize(this);
            Journey=GetComponent<Metro3DPassengerJourney>();if(Journey==null) Journey=gameObject.AddComponent<Metro3DPassengerJourney>();
            Journey.Initialize(this,stop,inside,existing,far);
            var colors=new[]{Color.white,new Color(.82f,.88f,1),new Color(1,.85f,.7f),new Color(.85f,.85f,.9f),new Color(.75f,.9f,.9f),new Color(.9f,.8f,1)};
            var tint=new MaterialPropertyBlock();tint.SetColor("_BaseColor",colors[id%colors.Length]);
            foreach(var renderer in GetComponentsInChildren<SkinnedMeshRenderer>()) {
                for(int slot=0;slot<renderer.sharedMaterials.Length;slot++)
                    if(renderer.sharedMaterials[slot]!=null&&renderer.sharedMaterials[slot].name=="CommuterCoat") renderer.SetPropertyBlock(tint,slot);
            }
            if(animator!=null) {
                animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.CullCompletely;
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
            if(MotionTrial!=null)seatedPastime?.Clear();
            else seatedPastime?.Tick(Time.deltaTime);
            if(!WalkReviewControlled&&Time.time>=nextDecision) {nextDecision=Time.time+.25f;Decide();}Animate(Velocity.magnitude);
        }
        void FixedUpdate()
        {
            if(crowd==null||motor==null||!motor.enabled||world.session.SimulationPaused||WalkReviewFrozen) return;
            Quaternion beforeRotation=transform.rotation;
            if(WalkReviewControlled||WalkReviewCandidate&&Journey.ReviewStairActive)Journey.TickReviewStair();
            bool walking=state==Activity.Walk||state==Activity.Board||state==Activity.Alight;
            // A settled platform passenger still blocks people and can turn
            // or decide to leave. Refresh the ground contact periodically,
            // instead of pushing the same capsule into the floor at 50 Hz.
            if(state==Activity.Wait&&!inTrain&&!WalkReviewControlled&&Journey.Waiting&&Journey.ReservedGate==null&&
                !QueueActive&&!NarrowWalking&&!FlowYieldActive&&Velocity.sqrMagnitude<.000001f&&motor.isGrounded) {
                float now=Time.fixedTime;
                if(now<nextIdleGroundProbe) {
                    float dt=Time.fixedDeltaTime;
                    ApplyBodyFacing(Vector3.zero,dt);
                    float rotation=Quaternion.Angle(beforeRotation,transform.rotation);
                    RotationDegreesPerSecond=rotation/dt;RotationTravel+=rotation;
                    DesiredVelocity=Velocity=velocity=steering=Vector3.zero;LearnedSteeringActive=false;
                    WalkingAlignmentScale=1;WalkPhase=Metro3DWalkPhase.Still;UpdateStopReason(false);
                    return;
                }
                nextIdleGroundProbe=now+.12f+(index&3)*.02f;
            }
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
            Vector3 flight=Journey.StairForward;
            bool stairFlow=flight.sqrMagnitude>.5f;
            Vector3 walkingDirection=delta;
            if(stairFlow) {
                // Return to our lane within a few steps, rather than drifting
                // toward its center over the entire length of the flight.
                float forward=Vector3.Dot(delta,flight);
                walkingDirection=delta-flight*forward+flight*Mathf.Clamp(forward,0,1.05f);
            }
            Vector3 desired=walking&&!pendingPath&&delta.magnitude>.07f?walkingDirection.normalized*requested:Vector3.zero;
            DesiredVelocity=desired;
            bool steerNow=Time.time>=nextSteering;
            if(steerNow&&state==Activity.Walk&&!inTrain&&!WalkReviewCandidate&&!PortalCommitted&&!QueueActive&&Journey.ReservedGate==null&&
                (stairFlow||blockedFor>.28f||NarrowWalking)&&desired.sqrMagnitude>.035f&&
                crowd.TryNarrowPassage(this,desired,out int shoulder)) {
                if(!NarrowWalking&&NarrowPoseWeight<.05f)NarrowPoseSide=shoulder;
                narrowUntil=Time.time+.36f;
            }
            if(DoorCrossing&&world.Traffic.IsSharedDoorCrossing(boardingTrain,door,doorLane)) {
                if(!NarrowWalking&&NarrowPoseWeight<.05f)NarrowPoseSide=doorLane*boardingTrain.side;
                narrowUntil=Time.time+.30f;
            }
            if(NarrowWalking&&Time.time>=narrowUntil&&steerNow&&crowd.CanRestoreWalkingRadius(this))
                SetWalkingRadius(StandingRadius);
            NarrowPoseWeight=Mathf.MoveTowards(NarrowPoseWeight,NarrowWalking?1:0,Time.fixedDeltaTime*4.2f);
            if(NarrowPoseWeight>=.75f&&Time.time<narrowUntil&&WalkingRadius>NarrowRadius)
                SetWalkingRadius(NarrowRadius);
            if(steerNow) {
                nextSteering=Time.time+.1f;
                LearnedSteeringActive=false;
                Vector3 limited=NarrowWalking?Vector3.ClampMagnitude(desired,.75f):desired;
                FollowingNarrowFlow=false;
                if(!CanYieldToFlow||limited.sqrMagnitude<=.001f)ClearFlowYield();
                steering=limited.sqrMagnitude>.001f?crowd.SafeVelocity(this,limited):Vector3.zero;
                SteeringCost=crowd.LastSteeringCost;StaticRejects=crowd.LastStaticRejects;
            }
            if(stairFlow||NarrowWalking||FlowYieldActive||FollowingNarrowFlow)yieldUntil=backoffUntil=0;
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
            if(stairFlow||NarrowWalking||FlowYieldActive||FollowingNarrowFlow||ExpectedQueueWait||!walking||pendingPath||arrived||progressing) {
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
                float requestedSpeed=preferred.magnitude;
                // Tight final approaches need a smaller radius than ordinary
                // corridor turns. Ease off before the arc overshoots the goal.
                if(FinalWalkTarget&&SteeringDistance<1&&Mathf.Abs(change)>45)
                    requestedSpeed*=Mathf.Lerp(1,.22f,Mathf.InverseLerp(45,105,Mathf.Abs(change)));
                float nextSpeed=Mathf.MoveTowards(actualSpeed,requestedSpeed,dt*(requestedSpeed<actualSpeed?3.8f:2.3f));
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
                bool prepare=WantsToAlight||riding.Moving&&riding.MovementRemaining<=AlightingLeadSeconds
                    &&Journey.AtDestination(riding.CurrentStop);
                if(prepare) {
                    var cue=WantsToAlight?TransitNotice.Alight:TransitNotice.PrepareAlight;
                    if(!NoticeTransit(cue,riding))return;
                    Journey.PreparingToAlight();
                    world.Traffic.ReleaseStanding(this);
                    if(state==Activity.Sit) {BeginStand();return;}
                    if(state!=Activity.Alight) {
                        ReleaseSeat();door=riding.NearestDoor(transform.position);waypoint=0;state=Activity.Alight;
                        alightingDoorsTried=1<<door;blockedAlightingDoorSince=0;
                        SetTarget(riding.DoorPoint(door,false),true);
                    }
                    AdvanceDoor(false);return;
                }
            }
            if(!inTrain&&Journey.CanBoard&&active!=null&&active.CanBoard&&active.CurrentStop==stationIndex) {
                if(!NoticeTransit(TransitNotice.Board,active))return;
                if(state==Activity.Sit) {BeginStand();return;}
                if(state!=Activity.Board) {ReleaseSeat();world.Traffic.ReleaseStanding(this);boardingTrain=active;door=active.NearestDoor(transform.position);waypoint=0;state=Activity.Board;Journey.BeginBoard();}
                AdvanceDoor(true);return;
            }
            if(state==Activity.Board&&(active==null||!active.CanBoard)&&waypoint==0) {
                world.Traffic.ReleaseDoor(this);boardingTrain=null;Journey.CancelBoard();SetWaitingPoint();return;
            }
            if(state==Activity.Board) {AdvanceDoor(true);return;}
            if(state!=Activity.Alight&&Time.time>=transitLookUntil)NoticeTransit(TransitNotice.None,null);
            if(!inTrain&&Journey.CanBoard&&UpdatePlatformRest())return;
            if(state==Activity.Sit) {ChooseSeatedPastime();return;}
            if(state==Activity.SitDown||state==Activity.StandUp) return;
            if(state==Activity.Walk&&FlatDistance(transform.position,target)<(world.Traffic.SupportFor(this)!=null?.105f:.23f)) {
                if(seat!=null) {BeginSit();return;}
                WaitJourney();
            }
            if(state==Activity.Wait&&inTrain&&riding!=null&&riding.MovementRemaining>16&&!checkedMovingSupport) {
                checkedMovingSupport=true;
                if(Journey.Personality.PersonalSpace>.23f&&index%3!=0&&world.Traffic.SupportFor(this)?.lean!=true&&
                    world.Traffic.TryLeanPoint(this,riding,out var lean)) {
                    WalkJourney(lean,false);return;
                }
            }
            if(state==Activity.Wait)SetWaitingFacing();
            if(state==Activity.Wait&&!triedSeat) {
                triedSeat=true;
                if(inTrain&&!(Journey.SeatPreference>.48f&&TrySeat())) {state=Activity.Walk;SetTarget(world.Traffic.TrainStandingPoint(this,riding),true);}
                Journey.ActivityChosen();
            }
        }
        void ChooseSeatedPastime()
        {
            if(world.walkingReviewOnly||MotionTrial!=null||seat==null||world.settings.seatedActions==null||
                world.settings.seatedActions.Length==0)return;
            if(seatedPastime?.Active==true) {
                if(seatedPastime.NeedsLegRoom&&!crowd.SpaceFree(seat.anchor.position,this,.57f))seatedPastime.Release();
                return;
            }
            float available;
            if(inTrain) {
                if(riding==null||!riding.Moving||riding.CurrentStop==stationIndex)return;
                float lead=Journey.AtDestination(riding.CurrentStop)?AlightingLeadSeconds:0;
                available=riding.MovementRemaining-lead;
            } else {
                if(!Journey.CanBoard||world.Transit==null)return;
                available=world.Transit.TimeToBoarding(this)-6;
            }
            if(available<6)return;
            seatedPastime??=new Metro3DSeatedPastime(this,world.SeatedActions);
            seatedPastime.Choose(Mathf.Min(available,40),crowd);
        }

        bool UpdatePlatformRest()
        {
            if(Time.time<nextPlatformRestCheck||state!=Activity.Wait&&state!=Activity.Walk&&seat==null)return false;
            nextPlatformRestCheck=Time.time+(seat!=null?1.25f:4+(index%5)*.2f);
            float wait=world.Transit.TimeToBoarding(this);
            if(seat!=null&&wait<6) {
                if(state==Activity.Sit)BeginStand();
                else if(state==Activity.Walk) {ReleaseSeat();SetWaitingPoint();}
                return true;
            }
            if(seat!=null||wait<12||world.Traffic.SupportFor(this)!=null||Journey.OnStairs)return false;
            Vector3 local=transform.localPosition;
            if(Mathf.Abs(local.y)>.18f||local.x*Journey.TrainSide>=-Metro3DWorld.ScreenDoorX)return false;
            float density=crowd.LocalDensity(transform.position,transform.parent);
            float preference=Journey.SeatPreference+(1-Mathf.Clamp01(Journey.Personality.Mobility))*.14f-density*.10f;
            float timePreference=Mathf.InverseLerp(16,40,wait);
            if(preference>Mathf.Lerp(.54f,.38f,timePreference)&&TrySeat(Mathf.Lerp(5,10,timePreference)))return true;
            float leanInterest=Mathf.Repeat(index*.618034f,1);
            if(leanInterest>Mathf.Lerp(.25f,.57f,Journey.Personality.Patience)||
                !world.Traffic.TryPlatformLeanPoint(this,out var point))return false;
            WalkJourney(point,!crowd.CanFollowWalkingSegment(this,point));return true;
        }

        void SetWaitingFacing()
        {
            var support=world.Traffic.SupportFor(this);
            if(support!=null&&support.frame==transform.parent) {
                Vector3 facing=support.lean?support.frame.TransformDirection(support.LeanFacing):
                    support.frame.forward*(index%2==0?1:-1);
                FaceJourney(facing,100);
            } else if(!inTrain&&Journey.Waiting)
                FaceJourney(world.stations[stationIndex].TransformDirection(Vector3.right*Journey.TrainSide));
        }
        int doorLane;
        void AdvanceDoor(bool boarding)
        {
            Metro3DTrain t=boarding?boardingTrain:riding;if(t==null) return;
            if(!boarding&&waypoint!=0)crowd.ClearLanding(t,door,this);
            if(waypoint==0) {
                Vector3 approach=world.Traffic.DoorApproach(this,t,door,boarding);
                Vector3 entry=t.DoorPoint(door,boarding);
                float distance=FlatDistance(transform.position,entry);
                float queueDistance=FlatDistance(transform.position,approach);
                bool direct=boarding?!QueueActive&&distance<.86f:distance<1.15f;
                if(!boarding)crowd.ClearLanding(t,door,this);
                if(t.DoorsOpen&&(direct||queueDistance<.28f)&&world.Traffic.TryDoor(this,t,door,boarding,out doorLane)) {
                    waypoint=1;ClearQueueContext();SetTarget(t.DoorPoint(door,boarding,doorLane),false);
                } else {
                    if((target-approach).sqrMagnitude>.0025f)FollowQueueGoal(approach);
                    else FaceQueue();
                    if(QueueLeader==null&&queueDistance<.28f)FaceJourney(entry-transform.position);
                    if(!boarding&&t.DoorsOpen&&(direct||queueDistance<.28f))RetryBlockedAlightingDoor(t);
                    else blockedAlightingDoorSince=0;
                    return;
                }
            }
            if(waypoint==1) {
                // Finish the diagonal approach before facing across the threshold.
                if(FlatDistance(transform.position,t.DoorPoint(door,boarding,doorLane))>.15f) return;
                waypoint=2;SetTarget(t.DoorPoint(door,!boarding,doorLane),false);
            }
            if(waypoint!=2) return;
            FaceJourney(t.DoorPoint(door,!boarding,doorLane)-t.DoorPoint(door,boarding,doorLane));
            Vector3 local=t.transform.InverseTransformPoint(transform.position);
            // Completion is the feet crossing the physical boundary, not hitting one occupied point exactly.
            bool crossed=boarding?t.Contains(transform.position)&&Mathf.Abs(local.x)<(doorLane==0?1.1f:.85f)
                :(-t.side*world.stations[t.CurrentStop].InverseTransformPoint(transform.position).x)>Metro3DWorld.ScreenDoorX+.24f;
            if(!crossed) return;
            world.Traffic.ReleaseDoor(this);var old=transform.parent;
            inTrain=boarding;stationIndex=t.CurrentStop;transform.SetParent(boarding?t.transform:world.stations[stationIndex],true);
            egressTrain=boarding?null:t;egressDoor=door;
            if(!boarding)egressPoint=transform.parent.InverseTransformPoint(t.DoorPoint(door,true));
            world.Traffic.ChangedParent(old,transform.parent);riding=boarding?t:null;boardingTrain=null;triedSeat=checkedMovingSupport=false;WaitJourney();
            if(boarding) {
                Journey.OnBoard();triedSeat=true;
                if(!(Journey.SeatPreference>.48f&&TrySeat())) {state=Activity.Walk;SetTarget(world.Traffic.TrainStandingPoint(this,riding),true);}
                Journey.ActivityChosen();
            }
            else Journey.OnAlight(stationIndex);
        }
        public void WalkJourney(Vector3 p,bool navigate) {state=Activity.Walk;SetTarget(p,navigate);}
        internal bool TryWalkStairApproach(Vector3 point)
        {
            // The landing is already identified by the stair action. Check a
            // short segment on it, then leave each step to the normal Motor.
            Vector3 ahead=Vector3.MoveTowards(transform.position,point,1.15f);
            if(!crowd.CanFollowWalkingSegment(this,ahead))return false;
            WalkJourney(point,false);
            pendingPath=false;pathCount=pathIndex=0;nextSteering=0;
            return true;
        }
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
            if(continuationPath==null||continuationPath.Length==0)continuationPath=new Vector3[path.Length];
            continuationFrom=from;continuationGoal=goal;continuationPending=true;crowd.Request(this);
        }
        public void CancelJourneyContinuation() {continuationPending=continuationReady=false;continuationCount=0;}
        public bool ContinueJourney(Vector3 goal,Vector3 gateForward=default)
        {
            bool ready=continuationReady&&continuationCount>0&&(goal-continuationGoal).sqrMagnitude<.01f;
            FaceTravel();
            state=Activity.Walk;target=goal;targetLocal=transform.parent.InverseTransformPoint(goal);
            pathIndex=pathCount=0;pendingPath=false;nextSteering=0;
            if(gateForward!=Vector3.zero&&crowd.CanShortenWalkingPath(this,goal)) {
                CancelJourneyContinuation();
                return true;
            }
            if(ready&&gateForward!=Vector3.zero) {
                // The precomputed path starts from an estimated position. Join a
                // reachable point in front of the actual feet after the crossing.
                ready=false;
                for(int i=Mathf.Min(continuationCount-1,3);i>=0;i--) {
                    Vector3 point=transform.parent.TransformPoint(continuationPath[i]);
                    Vector3 offset=point-transform.position;offset.y=0;
                    if(Vector3.Dot(offset,gateForward)<-.04f||offset.sqrMagnitude<.18f*.18f||
                        !crowd.CanShortenWalkingPath(this,point))continue;
                    pathIndex=i;ready=true;break;
                }
            }
            if(ready) {pathCount=continuationCount;System.Array.Copy(continuationPath,path,pathCount);}
            else RequestPath();
            CancelJourneyContinuation();return ready;
        }
        void RetryBlockedAlightingDoor(Metro3DTrain train)
        {
            if(!world.Traffic.IsAlightingHead(train,door,this)) {blockedAlightingDoorSince=0;return;}
            if(blockedAlightingDoorSince==0)blockedAlightingDoorSince=Time.time;
            if(Time.time-blockedAlightingDoorSince<6)return;
            blockedAlightingDoorSince=Time.time-4;
            if(!world.Traffic.TryAlternateAlightingDoor(this,train,door,alightingDoorsTried,out int alternate))return;
            world.Traffic.ReleaseDoor(this);
            door=alternate;alightingDoorsTried|=1<<door;blockedAlightingDoorSince=0;
            FollowQueueGoal(world.Traffic.DoorApproach(this,train,door,false));
            Journey.Planner.ReportRecoverableFailure("Alighting lane occupied; using another door in the carriage");
        }
        internal void ContinueToPlatformWait(Vector3 point,Vector3 landingForward)
        {
            triedSeat=true;
            ContinueJourney(point,landingForward);
        }
        public void SetWaitingPoint()
        {
            state=Activity.Walk;triedSeat=true;
            Vector3 point=world.Traffic.PlatformWaitingPoint(this);
            SetTarget(point,true);
        }
        bool TrySeat(float maxDistance=6)
        {
            // A personal-space-oriented, less fatigued rider may prefer a real
            // nearby wall rather than consume a seat. Destination stays fixed.
            if(inTrain&&Journey.SeatPreference<.68f&&Journey.Personality.PersonalSpace>.45f&&world.Traffic.TryLeanPoint(this,riding,out var leanPoint)) {
                state=Activity.Walk;SetTarget(leanPoint,true);return true;
            }
            Metro3DFacility best=null;float score=float.NegativeInfinity;
            foreach(var f in world.facilities) {
                if(f==null||f.kind!=Metro3DFacilityKind.Seat||!f.Available||f.trainFacility!=inTrain||f.approach==null||!f.transform.IsChildOf(transform.parent)) continue;
                if(!inTrain) {
                    var local=transform.parent.InverseTransformPoint(f.approach.position);
                    if(local.x*Journey.TrainSide>=0||Mathf.Abs(f.approach.position.y-transform.position.y)>.4f)continue;
                }
                float d=FlatDistance(transform.position,f.approach.position);
                if(d>maxDistance||!inTrain&&!crowd.SpaceFree(f.approach.position,this,.47f))continue;
                float s=maxDistance-d;
                if(s<=score||f.anchor==null)continue;
                s-=world.Traffic.OccupiedSeatNeighbors(f)*4.5f+crowd.LocalDensity(f.approach.position,transform.parent)*2.2f;
                if(s>score) {best=f;score=s;}
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
                var support=world.Traffic.SupportFor(this);
                if(!inTrain&&support!=null&&support.lean) {
                    Vector3 unavailable=target;
                    WalkJourney(world.Traffic.PlatformWaitingPoint(this,unavailable),true);
                }
                Journey.Planner.ReportRecoverableFailure("No collision-clear local path; destination retained");
            }
        }
        public void ClearDoorLanding()
            => ClearDoorLanding(transform.position);
        public void ClearDoorLanding(Vector3 point) => ClearDoorLanding(point,false);
        internal void ClearDoorLanding(Vector3 point,bool alightingPriority)
        {
            if(state==Activity.Sit||state==Activity.SitDown||state==Activity.StandUp||state==Activity.Alight) return;
            if(!inTrain&&!Journey.CanBoard||PortalCommitted||helperTaskActive)return;
            if(Time.time<clearYieldUntil||!alightingPriority&&(seat!=null||state==Activity.Walk&&Velocity.magnitude>.08f)) return;
            // A confirmed obstruction of the alighting lane takes precedence
            // over an unfinished walk to a seat or standing position.
            ReleaseSeat();
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
            if(contacts?.PutPhoneAwayForSeat()==true)return;
            state=Activity.SitDown;transitionTime=0;transitionFrom=transform.localPosition;transitionTo=transform.parent.InverseTransformPoint(seat.anchor.position);
            motor.enabled=false;velocity=steering=Velocity=Vector3.zero;animator.CrossFadeInFixedTime("SitDown",.12f);MotionTrial?.CrossFadeLegacy("SitDown",.12f);Animate(0);
        }
        void BeginStand()
        {
            if(seatedPastime?.Release()==true)return;
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
        void ReleaseSeat() {seatedPastime?.Clear();if(seat!=null) {seat.Release(this);seat=null;}}
        internal void RestoreBackgroundJourney(Metro3DStationTraffic.RiderSnapshot rider)
        {
            PersistentId=rider.id;
            Journey.RestoreRider(rider);
        }
        public void ResetForPool() {if(MotionTrial!=null)MotionTrial.Release();ResumeTransitionAnimation();Journey.CancelGate();CancelJourneyContinuation();ClearQueueContext();ReleaseSeat();crowd.Unregister(this);motor.enabled=false;pendingPath=false;pathCount=0;Velocity=velocity=steering=Vector3.zero;}
        public void RelocateToPlatform() {ReleaseSeat();motor.enabled=false;transform.position=world.stations[stationIndex].TransformPoint(world.PlatformWaitingPoint(index%world.settings.passengerCount));motor.enabled=true;WaitJourney();triedSeat=false;}
        static float FlatDistance(Vector3 a,Vector3 b) {a.y=b.y;return Vector3.Distance(a,b);}
        void OnDisable() {seatedPastime?.Clear();FaceTravel();if(crowd!=null) crowd.Unregister(this);}
        void OnDestroy() {ReleaseSeat();if(crowd!=null) crowd.Unregister(this);if(world!=null) world.passengers.Remove(this);}
    }
}

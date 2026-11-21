using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public enum Metro3DWalkPhase { Still, Starting, Walking, Curving, Braking, Pivoting, FacilityAlignment, QueueAdvance, QueueWait, Blocked }
    public enum Metro3DStopReason { None, Arrived, PathPending, Queue, Tagging, Door, Turning, StaticObstacle, ReservedPassage, Surface, Passenger, Yield, Recovery, Activity, SimulationPaused, Avoidance, MotorContact }
    // Locomotion/activity executor. Journey intent lives in the existing V2 planner adapter.
    public sealed partial class Metro3DPassenger : MonoBehaviour
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
        internal bool CanClearLanding(Vector3 start,Vector3 end,bool pastStair=false)
            =>crowd.CanShortenWalkingPath(this,start,end,pastStair);
        internal bool HasAscendingAtStair(Vector3 top,Vector3 bottom,Vector3 descent)
            =>crowd.HasAscendingAtStair(transform.parent,top,bottom,descent);
        internal bool CanWaitOffStair(Vector3 point)=>crowd.CanWaitOffStair(this,point);
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
        internal bool SteppingTurn => WalkPhase==Metro3DWalkPhase.Pivoting||WalkPhase==Metro3DWalkPhase.Blocked&&
            RotationDegreesPerSecond>15&&Velocity.sqrMagnitude<.02f;
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
        internal bool FollowingQueueContinuously {get;private set;}
        internal bool ContinuousQueueAllowed => Journey.ReservedGate==null;
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
        System.Guid queueLeaderId;

        bool TryContinuousQueue(Metro3DPassenger leader,float freeSpeed,out Vector3 intent)
        {
            intent=Vector3.zero;
            if(pendingPath||pathIndex<pathCount||!TryQueueLink(leader,out float ahead))
                return false;

            float leadSpeed=Mathf.Max(0,Vector3.Dot(leader.Velocity,QueueForward));
            float ownSpeed=Mathf.Max(0,Vector3.Dot(Velocity,QueueForward));
            bool closingAtDoor=state==Activity.Board&&waypoint==0&&boardingTrain!=null&&boardingTrain.CanBoard&&
                (boardingTrain.DoorPoint(door,true)-transform.position).sqrMagnitude<2.3f*2.3f&&
                !crowd.ObserveDoor(boardingTrain,door).incoming;
            float reservedGap=WalkingRadius+leader.WalkingRadius+(closingAtDoor?.012f:.065f);
            float pressure=closingAtDoor?Mathf.Max(BoardingUrgency,BoardInitiative):0;
            float preferredGap=Mathf.Lerp(QueueGap,reservedGap+.016f,pressure);
            float comfortableGap=Mathf.Max(reservedGap,preferredGap)+ownSpeed*QueueReactionDelay*
                (closingAtDoor?.08f:.32f);
            float openGap=Mathf.Max(0,ahead-reservedGap);
            float brakeCap=Mathf.Sqrt(leadSpeed*leadSpeed+2*3.8f*openGap);
            // Follow measured lead speed immediately. Small spacing errors
            // change speed gradually and never request a backwards turn.
            float error=ahead-comfortableGap;
            float correction=Metro3DQueueMotionPolicy.Correction(error,leadSpeed-ownSpeed,leadSpeed);
            float follow=leadSpeed+error*1.55f+correction*Mathf.InverseLerp(0,.30f,leadSpeed);
            float speed=Mathf.Min(freeSpeed,brakeCap,Mathf.Max(0,follow));
            // The leader sets pace; the authored lane target sets lateral
            // position. Copying the leader's lateral avoidance amplifies it.
            Vector3 slotOffset=Vector3.ProjectOnPlane(CurrentGoal-transform.position,Vector3.up);
            Vector3 laneError=slotOffset-QueueForward*Vector3.Dot(slotOffset,QueueForward);
            intent=(QueueForward+Vector3.ClampMagnitude(laneError*.42f,.13f)).normalized*speed;
            return true;
        }
        bool TryQueueLink(Metro3DPassenger leader,out float ahead)
        {
            ahead=0;
            if(!QueueActive||!ContinuousQueueAllowed||PortalCommitted||leader==null||leader.PersistentId!=queueLeaderId||
                !leader.isActiveAndEnabled||leader.transform.parent!=transform.parent)return false;
            Vector3 delta=leader.transform.position-transform.position;
            ahead=Vector3.Dot(delta,QueueForward);
            Vector3 sideways=Vector3.ProjectOnPlane(delta,Vector3.up)-QueueForward*ahead;
            return Mathf.Abs(delta.y)<.4f&&ahead>0&&ahead<2.5f&&sideways.sqrMagnitude<.38f*.38f;
        }
        bool queuePermissionWait;
        internal const float StandingRadius=.24f, NarrowRadius=.19f;
        internal Metro3DRiderLook RiderLook {get;private set;}
        internal float BodyRadius {get;private set;}=StandingRadius;
        internal float HeightRatio {get;private set;}=1;
        internal float CompactRadius=>NarrowRadius*(BodyRadius/StandingRadius);
        internal bool WardrobeApplied {get;private set;}
        internal Metro3DPassenger Companion {get;private set;}
        System.Guid companionId;
        internal System.Guid CompanionId=>companionId;
        float companionWaitUntil,lookAtSignUntil;
        int lastArrivalReaction=-1;
        float courtesyUntil,courtesyNext;
        Metro3DPassenger courtesyPeer;
        System.Guid courtesyPeerId;
        internal void AcknowledgePassing(Metro3DPassenger other)
        {
            if(!world.settings.ambientEventsEnabled||index%7!=1||Time.time<courtesyNext||other==null||other.PortalCommitted||other.QueueActive)return;
            courtesyPeer=other;courtesyPeerId=other.PersistentId;courtesyUntil=Time.time+1.15f;courtesyNext=Time.time+45;
        }
        internal bool WaitForCompanion()
        {
            if(Time.time<companionRetryAt)return true;
            if(!HasCompanion)return false;
            if(CanFinishGroupBoarding(CompanionService))return false;
            if(Companion.inTrain)return Companion.Riding!=CompanionService;
            return Companion.stationIndex!=stationIndex||!Companion.Journey.CanBoard||
                (Companion.transform.position-transform.position).sqrMagnitude>2.6f*2.6f;
        }
        internal bool HasCompanion => Companion!=null&&Companion.isActiveAndEnabled&&Companion.PersistentId==companionId;
        internal void PairWith(Metro3DPassenger companion) {Companion=companion;companionId=companion.PersistentId;}
        readonly System.Collections.Generic.Dictionary<string,Transform> wardrobeBones=new();
        Transform[] wardrobeMappedBones,baseBones;
        Mesh baseSkin;
        Material[] baseSkinMaterials;
        Transform wardrobeRoot,head,spine;
        Vector3 baseWardrobeScale;
        SkinnedMeshRenderer wardrobeRenderer;
        Bounds baseSkinBounds;
        Metro3DRocketboxCast.MountedAvatar externalAvatar;
        internal SkinnedMeshRenderer ExternalPassengerSkin=>externalAvatar?.Skin;

        internal void RetargetPassengerVisual(float leftWristWeight, float rightWristWeight)
        {
            if(externalAvatar==null)return;
            float sitting=SeatContactWeight;
            bool alignHands=sitting>.01f||contacts!=null&&(contacts.Holding||contacts.Leaning||contacts.HasPhoneInUse)
                ||Journey.GateAction==Metro3DPassengerJourney.GatePhase.Tag;
            Transform phone=seatedPastime?.HeldPhone??contacts?.VisiblePhone;
            float relaxedHands=contacts?.RelaxedHandsWeight??0;
            float relaxedRight=contacts?.HasPhoneInUse==true?0:relaxedHands;
            float lapWeight=CanCorrectVisualWrists&&seat!=null?sitting*(seatedPastime?.LapWeight??1):0;
            externalAvatar.Apply(sitting,alignHands,phone!=null,relaxedHands,relaxedRight,leftWristWeight,rightWristWeight,lapWeight:lapWeight);
            if(phone!=null)externalAvatar.MatchPhonePalm(phone);
        }
        internal void SetRiderLook(Metro3DRiderWardrobe wardrobe,Metro3DRiderLook look)
        {
            RiderLook=look;
            if(wardrobeRenderer==null) {
                var renderer=GetComponentInChildren<SkinnedMeshRenderer>(true);
                var model=transform.Find("HumanModel");
                if(renderer==null||model==null)return;
                wardrobeRenderer=renderer;wardrobeRoot=model;
                baseWardrobeScale=wardrobeRoot.localScale;
                baseSkin=wardrobeRenderer.sharedMesh;baseBones=wardrobeRenderer.bones;
                baseSkinMaterials=wardrobeRenderer.sharedMaterials;baseSkinBounds=wardrobeRenderer.localBounds;
            }
            float ratio=look==Metro3DRiderLook.Child?.73f:look==Metro3DRiderLook.Senior?.94f:1;
            float width=look==Metro3DRiderLook.Child?.77f:look==Metro3DRiderLook.Senior?.99f:1;
            wardrobeRoot.localScale=Vector3.Scale(baseWardrobeScale,new Vector3(width,ratio,width));
            bool publisher=wardrobe.PublisherCast.TryMount(this,wardrobeRenderer,baseSkin,baseBones,look,index,ref externalAvatar);
            if(publisher){
                var lod=wardrobeRenderer.GetComponent<Metro3DWardrobeLod>();
                if(lod!=null)lod.Clear();
                wardrobeRenderer.enabled=false;
                wardrobeRenderer.sharedMesh=null;
                if(wardrobeBones.Count==0)
                    foreach(var bone in wardrobeRoot.GetComponentsInChildren<Transform>(true))wardrobeBones.TryAdd(bone.name,bone);
            }else {
                externalAvatar?.Dispose();externalAvatar=null;
                wardrobeRenderer.enabled=true;
            }
            WardrobeApplied=publisher||wardrobe.Apply(wardrobeRenderer,wardrobeRoot,look,wardrobeBones,ref wardrobeMappedBones,index);
            if(!WardrobeApplied) {
                wardrobeRoot.localScale=baseWardrobeScale;
                wardrobeRenderer.sharedMesh=baseSkin;wardrobeRenderer.bones=baseBones;
                wardrobeRenderer.sharedMaterials=baseSkinMaterials;wardrobeRenderer.localBounds=baseSkinBounds;
                return;
            }
            wardrobeBones.TryGetValue("DEF-head",out head);wardrobeBones.TryGetValue("DEF-spine.002",out spine);
            HeightRatio=ratio;
            BodyRadius=StandingRadius*width;
            motor.height=1.8f*HeightRatio;motor.center=new Vector3(0,motor.height*.5f,0);
            motor.stepOffset=Mathf.Min(.30f,motor.height*.18f);
            SetWalkingRadius(BodyRadius);
            walkReferenceSpeed*=look==Metro3DRiderLook.Child?.82f:look==Metro3DRiderLook.Senior?.93f:1;
        }
        internal void ApplyRiderPosture(float sitting,System.Action<Transform> remember)
        {
            if(!WardrobeApplied||head==null||spine==null||NarrowPoseWeight>.02f)return;
            if(sitting>.99f&&Time.time<lookAtSignUntil&&seatedPastime?.Active!=true) {
                float elapsed=1.8f-(lookAtSignUntil-Time.time);
                float weight=Mathf.SmoothStep(0,1,elapsed/.25f)*Mathf.Clamp01((lookAtSignUntil-Time.time)/.5f);
                remember(head);head.rotation=Quaternion.AngleAxis(-13*weight,transform.right)*head.rotation;
            }
            if(sitting>.01f)return;
            if(yieldingToPackage&&!PortalCommitted&&!Journey.OnStairs){
                Vector3 to=perceivedCarryPoint-head.position;
                float angle=Mathf.Clamp(Vector3.SignedAngle(transform.forward,Vector3.ProjectOnPlane(to,Vector3.up),Vector3.up),-18,18);
                remember(head);head.rotation=Quaternion.AngleAxis(angle*.4f,transform.up)*head.rotation;
            }
            if(Time.time<courtesyUntil&&courtesyPeer!=null&&courtesyPeer.isActiveAndEnabled&&courtesyPeer.PersistentId==courtesyPeerId&&
                !PortalCommitted&&!QueueActive&&!Journey.OnStairs&&seat==null) {
                Vector3 toward=courtesyPeer.transform.position-transform.position;
                if(toward.sqrMagnitude<2.4f*2.4f) {
                    float angle=Mathf.Clamp(Vector3.SignedAngle(transform.forward,Vector3.ProjectOnPlane(toward,Vector3.up),Vector3.up),-26,26);
                    float phase=Mathf.Clamp01((courtesyUntil-Time.time)/1.15f);
                    float weight=Mathf.Sin(phase*Mathf.PI);
                    remember(head);
                    head.rotation=Quaternion.AngleAxis(angle*weight,Vector3.up)*Quaternion.AngleAxis(6*weight,transform.right)*head.rotation;
                }
            }
            if(RiderLook==Metro3DRiderLook.Senior&&!PortalCommitted) {
                remember(spine);spine.rotation=Quaternion.AngleAxis(Mathf.Lerp(1.5f,3.5f,1-Journey.Personality.Mobility),transform.right)*spine.rotation;
                remember(head);head.rotation=Quaternion.AngleAxis(-2.3f,transform.right)*head.rotation;
            }
            if(RiderLook==Metro3DRiderLook.Child&&HasCompanion&&!PortalCommitted) {
                Vector3 direction=Companion.transform.position-transform.position;
                if(direction.sqrMagnitude>2.4f*2.4f||direction.sqrMagnitude<.04f)return;
                float yaw=Mathf.Clamp(Vector3.SignedAngle(transform.forward,Vector3.ProjectOnPlane(direction,transform.up),transform.up),-23,23);
                remember(head);head.rotation=Quaternion.AngleAxis(yaw*.45f,transform.up)*head.rotation;
            }
        }
        internal void RestoreIdleWrists() => externalAvatar?.RestoreIdleWrists();
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
        const float DoorCapsuleInset=.018f;
        bool compressedDoorCapsule;
        float doorCapsuleUntil,nextDoorCapsuleRestore;
        void SetWalkingRadius(float radius)
        {
            WalkingRadius=radius;
            motor.radius=radius-(compressedDoorCapsule?DoorCapsuleInset:0);
        }
        internal float NarrowPoseWeight {get;private set;}
        internal int NarrowPoseSide {get;private set;}
        internal bool NarrowWalking => narrowUntil>Time.time||WalkingRadius<BodyRadius-.01f;
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
            QueueLeader=leader;queueLeaderId=leader==null?System.Guid.Empty:leader.PersistentId;
            QueueForward=direction.normalized;QueueGap=gap;
            queuePermissionWait=permissionWait;queueUntil=Time.time+.8f;
        }
        public void ClearQueueContext() {queueUntil=0;QueueLeader=null;queueLeaderId=System.Guid.Empty;FollowingQueueContinuously=false;queuePermissionWait=false;FaceTravel();}
        public void CommitPassingSide(int side) {PassingSide=side;PassingUntil=Time.time+.85f;}
        public void FollowQueueGoal(Vector3 point,bool preferDirect=false)
            =>FollowQueueGoal(point,preferDirect,false);

        internal void ApproachExitGate(Vector3 point)
        {
            // A route to the far side of the hall can be pending for several
            // decision ticks. Take a clear stretch toward the gate in the meantime.
            // Keep a usable full route, and let the gate own its FIFO near the reader.
            if(pathIndex>=pathCount&&FlatDistance(transform.position,point)>2.2f) {
                Vector3 step=Vector3.MoveTowards(transform.position,point,1.7f);
                if(crowd.CanShortenWalkingPath(this,step)) {
                    WalkJourney(step,false);
                    return;
                }
            }
            FollowQueueGoal(point,preferDirect:true);
        }

        void FollowQueueGoal(Vector3 point,bool preferDirect,bool allowLocalAdvance)
        {
            // Queue following is locomotion inside a facility action, not a
            // transition out of Board/Alight. Losing that action resets its
            // alignment waypoint on the next decision tick.
            if(state!=Activity.Board&&state!=Activity.Alight) state=Activity.Walk;
            if(TryQueueLink(QueueLeader,out _)&&Mathf.Abs(point.y-transform.position.y)<.28f) {
                Vector3 delta=Vector3.ProjectOnPlane(point-transform.position,Vector3.up);
                float travel=Vector3.Dot(delta,QueueForward);
                Vector3 lateral=delta-QueueForward*travel;
                // While already in the lane, a newly shortened spacing slot
                // is a place to wait. It is never a reason to turn back.
                if(travel<0&&lateral.sqrMagnitude<.18f*.18f) {
                    point-=QueueForward*travel;
                }
            }
            bool direct=preferDirect?crowd.CanFollowWalkingSegment(this,point):crowd.CanApproachDirect(this,point);
            if(!direct&&allowLocalAdvance) {
                Vector3 feet=transform.position;
                Vector3 desired=point-feet;
                Vector3 forward=QueueActive&&IsAlighting?QueueForward:
                    boardingTrain!=null?boardingTrain.transform.right*boardingTrain.side:Vector3.zero;
                if(desired.sqrMagnitude>2.15f*2.15f&&Vector3.Dot(desired,forward)>.6f) {
                    Vector3 step=Vector3.MoveTowards(feet,point,1.10f);
                    if(crowd.CanFollowWalkingSegment(this,step)) {
                        // Advance through the safe next stretch of the aisle
                        // without waiting for a route to the entire queue.
                        point=step;direct=preferDirect=true;
                    }
                }
            }
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
        internal Vector3 CabinContactForce {get;private set;}
        internal Vector3 CabinPushVelocity {get;private set;}
        internal Vector3 ActualContactStep {get;private set;}
        internal float BoardingPushEffort {get;private set;}
        Vector3 contactForceTarget;
        Metro3DPassenger contactForcePeer;
        System.Guid contactForcePeerId;
        // Both controllers share the existing .025m skin. Their
        // resting shoulder envelope must survive temporary capsule compression.
        internal float DoorContactRadius=>WalkingRadius+motor.skinWidth*.5f;
        float pushEffortTarget,doorContactLoad;
        internal float DoorContactPressure=>Mathf.Clamp01(doorContactLoad/Metro3DCrowd.DoorContactLoadLimit);
        internal bool HasDoorShoulderContact=>doorContactLoad>8f||BoardingPushEffort>.10f;
        Metro3DTrain nearbyDoorContact;
        float nearbyDoorContactUntil;
        Metro3DTrain ContactVehicle=>ClearingOwnDoor?egressTrain:state==Activity.Board?boardingTrain:
            inTrain?riding:Time.time<nearbyDoorContactUntil?nearbyDoorContact:null;

        internal void NoteDoorContact(Metro3DTrain train)
        {
            nearbyDoorContact=train;nearbyDoorContactUntil=Time.time+.52f;
        }

        internal bool AtDoorContact(Metro3DTrain train,int number)
            =>ContactVehicle==train&&(DoorCrossing?door==number:
                ClearingOwnDoor?egressDoor==number:train.NearestDoor(transform.position)==number);

        internal bool DoorContactContext(out Metro3DTrain train,out int number)
        {
            train=ContactVehicle;number=-1;
            if(train==null||!train.DoorsOpen||train.Moving||seat!=null)return false;
            number=DoorCrossing?door:ClearingOwnDoor?egressDoor:
                state==Activity.Board&&boardingTrain==train?door:train.NearestDoor(transform.position);
            return (train.DoorPoint(number,!inTrain)-transform.position).sqrMagnitude<2.3f*2.3f;
        }

        internal Vector3 DoorDrive(Metro3DTrain train)
        {
            Vector3 outward=-train.transform.right*train.side;
            if(ClearingRetreatPath(train))return DesiredVelocity;
            if(ClearingOwnDoor||TravelsOutward&&DoorCrossing)
                return Vector3.Dot(DesiredVelocity,outward)>.2f
                    ?DesiredVelocity*(DoorStallPressure?1.23f:1):outward*.67f;
            if(state==Activity.Board&&(DoorCrossing||IntendsDoorAdvance))
                return Vector3.Dot(DesiredVelocity,-outward)>.2f
                    ?DesiredVelocity*(1+.32f*BoardingUrgency+(DoorStallPressure?(CrowdRushActive?.48f:.18f):0))
                    :-outward*Mathf.Lerp(.27f,.53f,BoardingUrgency);
            if(state==Activity.Board&&train.CanBoard&&BoardingUrgency>.2f&&QueueLeader!=null&&
                QueueLeader.PersistentId==queueLeaderId&&QueueLeader.boardingTrain==train&&!QueueLeader.TravelsOutward&&
                Vector3.Dot(DesiredVelocity,-outward)>.10f)
                return DesiredVelocity*(BoardingUrgency*Mathf.Lerp(.35f,.76f,BoardInitiative));
            if(inTrain&&Time.time<boardedContactUntil)return DesiredVelocity;
            return Vector3.zero;
        }
        float boardedContactUntil;
        int cabinEntryDoor=-1;
        float cabinEntryUntil,nextCabinEntryPlan;
        void RefreshDoorContact()
        {
            contactForceTarget=Vector3.zero;pushEffortTarget=doorContactLoad=0;contactForcePeer=null;
            if(!motor.isGrounded||!DoorContactContext(out var train,out int number))return;
            contactForceTarget=crowd.DoorContactForce(this,train,number,out pushEffortTarget,out contactForcePeer,out doorContactLoad);
            contactForcePeerId=contactForcePeer!=null?contactForcePeer.PersistentId:System.Guid.Empty;
        }
        void UpdateDoorCapsule(bool steppingAside)
        {
            var player=world.player;
            bool roomForPlayer=player==null||(player.transform.position-transform.position).sqrMagnitude>.75f*.75f;
            // Opposing contacts can cancel movement while still
            // squeezing the body. Use contact load for capsule compliance.
            bool compress=roomForPlayer&&doorContactLoad>(steppingAside?8f:20f);
            float now=Time.time;
            if(compress) {
                doorCapsuleUntil=now+.38f;
                if(compressedDoorCapsule)return;
            } else {
                if(!compressedDoorCapsule||now<nextDoorCapsuleRestore||roomForPlayer&&now<doorCapsuleUntil)return;
                nextDoorCapsuleRestore=now+.24f;
                // Wait until the full radius fits. Rebuilding the capsule for
                // every oscillation of the contact force breaks the stance.
                if(!crowd.CanRestoreWalkingRadius(this))return;
            }
            compressedDoorCapsule=compress;
            SetWalkingRadius(WalkingRadius);
        }
        Metro3DTrain observedBoardTrain;
        int observedBoardDoor;
        float boardingOpportunityAt,boardingConfidence;
        float doorStallTime, crowdRushUntil;
        Metro3DTrain crowdRushTrain;
        MaterialPropertyBlock rushTint, previousRushTint;
        SkinnedMeshRenderer rushTintSkin;
        static readonly int RushColor = Shader.PropertyToID("_BaseColor");
        internal bool CrowdRushActive => crowdRushTrain!=null&&Time.time<crowdRushUntil&&
            !crowdRushTrain.Moving&&crowdRushTrain.DoorsOpen;
        internal bool DoorStallReady => doorStallTime>=.099f;
        internal bool DoorStallPressure {get;set;}

        void WatchDoorStall(float dt)
        {
            bool followingStoppedHead=ApproachingOpenDoor&&TryQueueLink(QueueLeader,out float gap)&&
                gap<.88f&&QueueLeader.Velocity.sqrMagnitude<.14f*.14f;
            bool canAdvance=DoorCrossing&&!RetreatingDoor||ApproachingOpenDoor&&IntendsDoorAdvance&&!ExpectedQueueWait;
            bool stalled=!DoorCourtesyStep&&Velocity.sqrMagnitude<.14f*.14f&&
                (canAdvance&&DesiredVelocity.sqrMagnitude>.16f||followingStoppedHead);
            doorStallTime=stalled?Mathf.Min(.35f,doorStallTime+dt):0;
            if(!stalled)DoorStallPressure=false;
        }

        void TryStartCrowdRush(Metro3DTrain train,float remaining)
        {
            if(!world.settings.ambientEventsEnabled||world.walkingReviewOnly||HasCompanion||
                RiderLook==Metro3DRiderLook.Child||RiderLook==Metro3DRiderLook.Senior||externalAvatar?.IsLocalScan==true||
                BoardInitiative<.40f)return;
            float window=remaining>0?remaining:Mathf.Max(0,world.settings.dwellSeconds-7f);
            if(window<4f)return;
            float until=Time.time+Mathf.Min(window-.2f,9f);
            if(!world.TryScheduleCrowdRush(stationIndex,train,transform.position,until))return;
            crowdRushTrain=train;
            crowdRushUntil=until;
        }

        void UpdateCrowdRushTint()
        {
            if(!CrowdRushActive) {
                if(rushTintSkin!=null)ClearCrowdRushTint();
                return;
            }
            var skin=ExternalPassengerSkin!=null?ExternalPassengerSkin:wardrobeRenderer;
            if(skin==null||skin==rushTintSkin)return;
            ClearCrowdRushTint();
            rushTint??=new MaterialPropertyBlock();
            previousRushTint??=new MaterialPropertyBlock();
            skin.GetPropertyBlock(previousRushTint);
            skin.GetPropertyBlock(rushTint);
            rushTint.SetColor(RushColor,new Color(1.12f,.49f,.40f,1));
            skin.SetPropertyBlock(rushTint);
            rushTintSkin=skin;
        }

        void ClearCrowdRushTint()
        {
            if(rushTintSkin!=null)
                rushTintSkin.SetPropertyBlock(previousRushTint.isEmpty?null:previousRushTint);
            rushTintSkin=null;
        }

        internal float BoardingUrgency{get;private set;}
        internal bool IntendsDoorAdvance{get;private set;}
        float BoardInitiative {
            get {
                var temperament=Journey.Personality;
                return temperament.Assertiveness*.56f+temperament.SelfInterest*.24f+(1-temperament.Patience)*.20f;
            }
        }
        static float BoardWindowUrgency(float remaining)=>Mathf.InverseLerp(14f,3f,remaining);

        internal bool ReadBoardingOpportunity(Metro3DTrain train,int doorIndex,bool available,float opposingPressure,float room)
        {
            float now=Time.time;
            if(train!=observedBoardTrain||doorIndex!=observedBoardDoor) {
                observedBoardTrain=train;observedBoardDoor=doorIndex;
                boardingOpportunityAt=now-.18f;boardingConfidence=.34f;IntendsDoorAdvance=false;
            }
            float duration=Mathf.Clamp(now-boardingOpportunityAt,0,.32f);
            boardingOpportunityAt=now;
            float remaining=world.Transit.BoardingSeconds(train);
            bool admitted=train.CanBoard||CanFinishGroupBoarding(train);
            BoardingUrgency=admitted?BoardWindowUrgency(remaining):0;
            if(admitted&&!CrowdRushActive)TryStartCrowdRush(train,remaining);
            float initiative=CrowdRushActive?1:BoardInitiative;
            float utility=.60f+.25f*initiative+.29f*BoardingUrgency+.08f*room-
                opposingPressure*Mathf.Lerp(1.14f,.46f,initiative);
            float confidence=available&&admitted?Mathf.Clamp01(utility):0;
            float response=Mathf.Lerp(1.9f,7.8f,Mathf.Max(initiative,BoardingUrgency));
            if(room>.28f&&opposingPressure<.15f)response+=1.8f+initiative;
            boardingConfidence=Mathf.MoveTowards(boardingConfidence,confidence,duration*response);
            IntendsDoorAdvance=available&&(IntendsDoorAdvance?boardingConfidence>.48f:boardingConfidence>=.57f);
            return IntendsDoorAdvance;
        }

        internal bool BoardingAttention(out Vector3 direction)
        {
            direction=Vector3.zero;
            if(state!=Activity.Board||waypoint!=0||boardingTrain==null)return false;
            direction=boardingTrain.DoorPoint(door,false)-transform.position;
            direction.y=0;
            float reach=QueueActive?7.5f:4.0f;
            return direction.sqrMagnitude<reach*reach&&direction.sqrMagnitude>.04f;
        }

        internal bool BoardingDoorLook(out Vector3 direction)
        {
            direction=Vector3.zero;
            if(state!=Activity.Board||waypoint!=0||boardingTrain==null)return false;
            direction=boardingTrain.DoorPoint(door,false)-transform.position;
            direction.y=0;
            return direction.sqrMagnitude<14f&&direction.sqrMagnitude>.04f;
        }

        public int AlightingDoor => state==Activity.Alight?door:-1;
        Metro3DSeatedPastime seatedPastime;
        Metro3DPassengerContacts contacts;
        public string PhoneActivity => contacts?.PhoneLabel ?? "";
        enum TransitNotice { None, Board, PrepareAlight, Alight }
        TransitNotice noticedTransit;
        Metro3DTrain noticedTrain;
        float transitResponseAt, transitLookUntil;
        float platformAttentionUntil, landingTurnUntil;
        Vector3 landingForwardLocal;
        internal bool PhoneTransitLook => DoorCrossing||Time.time<transitLookUntil||BoardingDoorLook(out _)||
            state==Activity.Wait&&Time.time<platformAttentionUntil;
        internal bool SettledStanding => motor!=null&&motor.enabled&&motor.isGrounded&&seat==null&&!DoorCrossing&&
            !NarrowWalking&&!Journey.OnStairs&&Velocity.sqrMagnitude<.055f*.055f&&
            (state==Activity.Wait||ExpectedQueueWait);

        internal bool CanCorrectVisualWrists =>
            MotionTrial == null && !WalkReviewCandidate && !WalkReviewControlled &&
            animator != null && animator.isActiveAndEnabled;

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
            if(doorActivity)return continuing&&!(state==Activity.Board&&BoardingDoorLook(out _));
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
        float TransitionDuration=>RiderLook==Metro3DRiderLook.Senior?1.12f:RiderLook==Metro3DRiderLook.Child?1.0f:.9f;
        float transitionBaseAnimatorSpeed=1;
        internal bool RisingFromSeat => state==Activity.StandUp;
        internal bool LoweringToSeat => state==Activity.SitDown;
        internal float SeatTransitionPhase => Mathf.Clamp01(transitionTime/TransitionDuration);
        internal Vector3 StandLanding => transform.parent.TransformPoint(transitionTo);
        public float SittingWeight => state==Activity.Sit?1:state==Activity.SitDown?Mathf.SmoothStep(0,1,SeatTransitionPhase):state==Activity.StandUp?1-Mathf.SmoothStep(0,1,SeatTransitionPhase):0;
        internal float SeatContactWeight => state switch {
            Activity.Sit => 1,
            Activity.SitDown => Mathf.SmoothStep(0,1,Mathf.InverseLerp(.14f,.86f,SeatTransitionPhase)),
            Activity.StandUp => 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.29f,.87f,SeatTransitionPhase)),
            _ => 0
        };
        internal float SeatedPortraitWeight => index%3!=0?seatedPastime?.PortraitWeight??0:0;
        internal AnimationLab.AnimationLabSeatedPhoneReview SeatedPhoneSource => world.SeatedActions.phone;
        internal void ApplySeatedPastimeProp() => seatedPastime?.ApplyCurrentProp();
        public bool WantsToAlight => inTrain&&riding!=null&&riding.DoorsOpen&&!riding.Moving
            &&(companionReturning||Journey.AtDestination(riding.CurrentStop)||world.Transit.EndsLine(riding)||(riding==world.train&&world.session.TransferAtCurrentStop));
        const float AlightingLeadSeconds = 12;
        enum Activity { Walk,Wait,SitDown,Sit,StandUp,Board,Alight }
        Activity state;
        Metro3DFacility seat;
        float nextPlatformRestCheck,nextIdleGroundProbe;
        bool checkedMovingSupport;
        Metro3DStationTraffic.SupportSpot aisleGrip;
        float gripYieldUntil;
        const float GripYieldDistance=.16f;
        internal float GripYieldWeight {get;private set;}
        Metro3DTrain riding;
        Metro3DTrain boardingTrain;
        Metro3DTrain egressTrain;
        int egressDoor;
        Vector3 egressPoint;
        float nextExitDirection;
        bool exitQueueActive,exitWaypointLeavesQueue;
        Metro3DTrain missedService;
        float nextQueueRejoin;
        int missedDoor;
        int cabinArrivalDoor=-1,cabinSettlementDoor=-1,cabinSettlementRetries;
        float cabinProgressSince;
        Vector3 cabinProgressFeet;
        Vector3 platformFlankDirection;
        float platformFlankUntil;

        internal bool ExitingPlatformQueue => exitQueueActive;
        internal bool DischargingThrough(Metro3DTrain train,int number)
            =>exitQueueActive&&egressTrain==train&&egressDoor==number;
        internal bool QueuedAtPlatformDoor(Metro3DTrain train,int number) =>
            !inTrain&&!DoorCrossing&&(state==Activity.Board&&boardingTrain==train&&door==number||
                missedService==train&&missedDoor==number||
                Journey.CanBoard&&world.Traffic.StagedPlatformDoor(this,out int staged,out _)&&staged==number);
        internal void HoldPlatformFlank(Vector3 away) {
            platformFlankDirection=transform.parent.InverseTransformDirection(away);
            platformFlankUntil=Time.time+.50f;
        }
        bool HoldingPlatformFlank => QueueActive&&!DoorCrossing&&Time.time<platformFlankUntil;
        internal Vector3 PlatformFlankDirection =>
            (HoldingPlatformFlank&&!DoorCourtesyStep&&Velocity.sqrMagnitude<.14f*.14f)
                ?transform.parent.TransformDirection(platformFlankDirection):Vector3.zero;
        internal bool ClearingOwnDoor=>egressTrain!=null&&ClearingDoor(egressTrain,egressDoor);
        internal bool JustLeftDoor(Metro3DTrain train,int door)
            =>egressTrain==train&&egressDoor==door&&
                (transform.localPosition-egressPoint).sqrMagnitude<2.1f*2.1f;
        public bool ClearingDoor(Metro3DTrain train,int number)
        {
            if(train==null||inTrain||egressTrain!=train||number!=egressDoor)return false;
            Vector3 delta=transform.localPosition-egressPoint;
            float along=-train.side*delta.x;
            // A lane offset is not the edge of the door. Keep the exit
            // action while our body can still overlap the central landing.
            float across=Mathf.Abs(delta.z+doorLane*Metro3DTrain.DoorLaneOffset);
            float edge=Metro3DTrain.DoorWidth*.5f+WalkingRadius;
            return Mathf.Abs(delta.y)<.40f&&along>-.70f&&along<Metro3DCrowd.ExitLandingDepth&&
                (along<.42f||across<edge);
        }
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
        float blockedAlightingDoorSince,alightingProgressDistance=float.PositiveInfinity;
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
        float animationPace,animationPaceRate,animationCadence,animationCadenceRate;
        static readonly int Speed=Animator.StringToHash("Speed"),Seated=Animator.StringToHash("Seated"),Gait=Animator.StringToHash("Gait");
        public void Initialize(Metro3DWorld owner,int id,int stop,bool inside,bool existing,bool far)
        {
            WalkReviewCandidate=WalkReviewControlled=WalkReviewFrozen=WalkReviewAnimationCandidate=false;
            nextReviewLook=reviewAnimationSpeed=platformAttentionUntil=landingTurnUntil=0;
            landingForwardLocal=Vector3.zero;
            if(MotionTrial!=null)MotionTrial.Release();
            seatedPastime?.Clear();seatedPastime=null;
            if(crowd!=null)crowd.Unregister(this);
            noticedTransit=TransitNotice.None;noticedTrain=null;transitResponseAt=transitLookUntil=0;
            PersistentId=System.Guid.NewGuid();pivoting=false;curvedTurnRate=RotationTravel=0;StopReason=Metro3DStopReason.None;WalkPhase=Metro3DWalkPhase.Still;
            DesiredVelocity=PathWaypoint=TravelLookahead=Vector3.zero;RotationDegreesPerSecond=0;WalkingAlignmentScale=1;
            world=owner;index=id;stationIndex=stop;inTrain=inside;helperUntil=clearYieldUntil=0;helperTaskActive=false;
            queueUntil=nextQueuePath=yieldUntil=backoffUntil=0;recoveryStage=PassingSide=0;PassingUntil=0;
            QueueLeader=null;queueLeaderId=System.Guid.Empty;FollowingQueueContinuously=false;
            YieldingFor=null;YieldLimit=flowApproachUntil=0;RejoiningFlow=FollowingNarrowFlow=false;
            animator=GetComponentInChildren<Animator>(true);motor=GetComponent<CharacterController>();crowd=world.GetComponent<Metro3DCrowd>();
            animationPace=animationPaceRate=animationCadence=animationCadenceRate=0;
            ResumeTransitionAnimation();if(animator!=null)animator.speed=1;transitionBaseAnimatorSpeed=1;egressTrain=null;
            exitQueueActive=exitWaypointLeavesQueue=false;missedService=null;platformFlankUntil=0;cabinArrivalDoor=cabinSettlementDoor=-1;
            CabinContactForce=CabinPushVelocity=ActualContactStep=contactForceTarget=Vector3.zero;boardedContactUntil=0;cabinEntryDoor=-1;
            contactForcePeer=null;contactForcePeerId=System.Guid.Empty;
            retreatingBoarder=false;boardingProgress=boardingProgressAt=boardAlignmentUntil=0;
            nearbyDoorContact=null;nearbyDoorContactUntil=retreatClearStepUntil=nextDoorYieldSample=nextDoorRelayAt=0;
            canRelayDoorYield=doorYieldBlocked=false;doorYieldStartedAt=0;
            ClearCrowdRushTint();
            crowdRushTrain=null;crowdRushUntil=doorStallTime=0;DoorStallPressure=false;
            observedBoardTrain=null;BoardingUrgency=boardingConfidence=BoardingPushEffort=pushEffortTarget=doorContactLoad=0;
            IntendsDoorAdvance=StagedBoarding=false;NextQueueSideStep=QueueSideStepUntil=0;
            riding=inside?GetComponentInParent<Metro3DTrain>():null;boardingTrain=null;seat=null;triedSeat=pendingPath=false;pathCount=pathIndex=waypoint=0;
            velocity=steering=Velocity=Vector3.zero;verticalSpeed=blockedFor=0;state=Activity.Wait;target=transform.position;targetLocal=transform.localPosition;
            FaceTravel();
            CancelJourneyContinuation();
            nextPlatformRestCheck=Time.time+2+(id%9)*.21f;checkedMovingSupport=false;nextIdleGroundProbe=0;
            aisleGrip=null;gripYieldUntil=GripYieldWeight=0;
            nextDecision=Time.time+(id%13)*.025f;nextSteering=Time.time+(id%7)*.013f;motor.enabled=true;
            ClearCompanionBoarding();ResetDecisions();companionRetryAt=nextRegroup=0;
            WardrobeApplied=false;Companion=null;companionId=System.Guid.Empty;HeightRatio=1;BodyRadius=StandingRadius;
            lastArrivalReaction=-1;companionWaitUntil=lookAtSignUntil=courtesyUntil=courtesyNext=0;courtesyPeer=null;
            if(wardrobeRoot!=null)wardrobeRoot.localScale=baseWardrobeScale;
            motor.height=1.8f;motor.center=new Vector3(0,.9f,0);motor.stepOffset=.30f;
            compressedDoorCapsule=false;doorCapsuleUntil=nextDoorCapsuleRestore=0;
            SetWalkingRadius(StandingRadius);motor.skinWidth=.025f;
            NarrowPoseWeight=narrowUntil=stairPassUntil=stairPassOffset=0;NarrowPoseSide=0;
            // Acceleration starts below the prefab's old 1mm movement cutoff.
            // Discarding those steps pins measured-velocity feedback at zero.
            motor.minMoveDistance=0;
            contacts=GetComponent<Metro3DPassengerContacts>();if(contacts==null) contacts=gameObject.AddComponent<Metro3DPassengerContacts>();contacts.Initialize(this);
            Journey=GetComponent<Metro3DPassengerJourney>();if(Journey==null) Journey=gameObject.AddComponent<Metro3DPassengerJourney>();
            Journey.Initialize(this,stop,inside,existing,far);
            if(animator!=null) {
                animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.CullCompletely;
                walkReferenceSpeed=WalkReferenceSpeed*Mathf.Abs(animator.transform.lossyScale.z)/.9731136f;
            }
            if(isActiveAndEnabled)crowd.Register(this);
        }
        void OnEnable() {
            if(crowd!=null) crowd.Register(this);
            if(animator!=null) {
                animator.Rebind();
                animator.Play("Locomotion",0,(index*.173f)%1);
                if(externalAvatar!=null&&animator.isActiveAndEnabled&&animator.runtimeAnimatorController!=null) {
                    var culling=animator.cullingMode;
                    animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    animator.SetFloat(Speed,0);animator.SetFloat(Gait,0);animator.SetBool(Seated,false);
                    animator.Update(0);
                    externalAvatar.Apply(0,false,false,0,0,0,0,initialPose:true);
                    animator.cullingMode=culling;
                }
            }
        }
        void Update()
        {
            if(WalkReviewFrozen)return;
            UpdateCrowdRushTint();
            if(externalAvatar!=null&&animator!=null){
                var skin=externalAvatar.Skin;
                var cull=skin.isVisible||skin.updateWhenOffscreen?AnimatorCullingMode.AlwaysAnimate:AnimatorCullingMode.CullCompletely;
                if(animator.cullingMode!=cull)animator.cullingMode=cull;
            }
            if(world==null||world.session.SimulationPaused) {Velocity=Vector3.zero;Animate(0);return;}
            if(state==Activity.SitDown||state==Activity.StandUp) {UpdateTransition();return;}
            if(MotionTrial!=null)seatedPastime?.Clear();
            else seatedPastime?.Tick(Time.deltaTime);
            if(cabinEntryDoor>=0&&ContinueIntoCabin()) {Animate(Velocity.magnitude);return;}
            if(!WalkReviewControlled&&Time.time>=nextDecision) {
                nextDecision=Time.time+.25f;
                if(!AdvancePlatformExit()&&!RecoverCabinSettlement())Decide();
            }
            Animate(Velocity.magnitude);
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
                !DoorCourtesyStep&&Time.time>=nearbyDoorContactUntil&&CabinPushVelocity.sqrMagnitude<.0004f&&
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
            if(walking&&Time.time>=nextSteering) {
                if(DoorCrossing&&waypoint==1&&!RetreatingDoor)goal=DoorApproachLookahead(goal);
                else if(Time.time<landingTurnUntil&&!PortalCommitted&&!QueueActive&&!pendingPath)
                    goal=LandingLookahead(goal);
            }
            TravelLookahead=goal;
            Vector3 delta=goal-transform.position;delta.y=0;
            SteeringDistance=delta.magnitude;
            float speed=world.settings.npcSpeed*Journey.SpeedScale;
            if(DoorCrossing&&!RetreatingDoor)
                speed*=IsAlighting?1.20f:1.16f+Mathf.Max(BoardingUrgency,BoardInitiative)*.27f+
                    (CrowdRushActive?.12f:0);
            else if(exitQueueActive)speed*=1.18f;
            else if(cabinEntryDoor>=0||inTrain&&Time.time<boardedContactUntil&&state==Activity.Walk)
                speed*=Mathf.Lerp(1.13f,1.28f,BoardInitiative);
            else if(state==Activity.Board&&IntendsDoorAdvance)
                speed*=1+Mathf.Max(BoardingUrgency,BoardInitiative)*.24f;
            Vector3 outgoing=Vector3.zero;
            if(pathIndex+1<pathCount) {outgoing=transform.parent.TransformDirection(path[pathIndex+1]-path[pathIndex]);outgoing.y=0;}
            FinalWalkTarget=pathIndex>=pathCount||pathIndex==pathCount-1&&FlatDistance(goal,target)<.18f;
            float requested=WalkReviewCandidate&&!PortalCommitted&&!QueueCommitted
                ?(FinalWalkTarget&&!Journey.StairApproaching?Mathf.Min(speed,Metro3DWalkMotion.ArrivalSpeed(FlatDistance(transform.position,target))):speed)
                :Mathf.Min(speed,delta.magnitude*3.2f);
            if(!FinalWalkTarget&&!PortalCommitted&&!QueueCommitted&&!Journey.OnStairs)
                requested=speed;
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
            // A boarding head can close the last few centimeters of
            // an available approach using its own motor before any push.
            float arrivalTolerance=state==Activity.Board&&waypoint==0&&IntendsDoorAdvance&&QueueLeader==null?.025f:.07f;
            Vector3 queueIntent=Vector3.zero;
            FollowingQueueContinuously=walking&&TryContinuousQueue(QueueLeader,speed,out queueIntent);
            Vector3 desired=FollowingQueueContinuously?queueIntent:
                walking&&!pendingPath&&delta.magnitude>arrivalTolerance?walkingDirection.normalized*requested:Vector3.zero;
            DesiredVelocity=desired;
            WatchDoorStall(Time.fixedDeltaTime);
            bool steerNow=Time.time>=nextSteering;
            bool movingDoorPassage=DoorCrossing&&!RetreatingDoor;
            bool searchShoulder=movingDoorPassage||
                state==Activity.Walk&&!WalkReviewCandidate&&!PortalCommitted&&!QueueActive&&Journey.ReservedGate==null&&
                (cabinEntryDoor>=0||exitQueueActive||stairFlow||blockedFor>.28f||StaticRejects>0||NarrowWalking);
            if(steerNow&&searchShoulder&&desired.sqrMagnitude>.035f&&crowd.TryNarrowPassage(this,desired,out int shoulder)) {
                if(!NarrowWalking&&NarrowPoseWeight<.05f)NarrowPoseSide=shoulder;
                narrowUntil=Time.time+.36f;
            }
            if(DoorCrossing&&world.Traffic.IsSharedDoorCrossing(CrossingTrain,door,doorLane)) {
                if(!NarrowWalking&&NarrowPoseWeight<.05f)
                    NarrowPoseSide=doorLane*CrossingTrain.side*(TravelsOutward?-1:1);
                narrowUntil=Time.time+.30f;
            }
            if(NarrowWalking&&Time.time>=narrowUntil&&steerNow&&crowd.CanRestoreWalkingRadius(this))
                SetWalkingRadius(BodyRadius);
            NarrowPoseWeight=Mathf.MoveTowards(NarrowPoseWeight,NarrowWalking?1:0,Time.fixedDeltaTime*4.2f);
            if(NarrowPoseWeight>=.75f&&Time.time<narrowUntil&&WalkingRadius>CompactRadius)
                SetWalkingRadius(CompactRadius);
            if(steerNow) {
                nextSteering=Time.time+.1f;
                LearnedSteeringActive=false;
                // Keep ordinary stepping speed through a valid shared gap.
                // The selected corridor and final Motor checks still set the pace.
                Vector3 limited=NarrowWalking?Vector3.ClampMagnitude(desired,movingDoorPassage?1.16f:.75f):desired;
                FollowingNarrowFlow=false;
                if(!CanYieldToFlow||limited.sqrMagnitude<=.001f)ClearFlowYield();
                steering=limited.sqrMagnitude>.001f?crowd.SafeVelocity(this,limited):Vector3.zero;
                SteeringCost=crowd.LastSteeringCost;StaticRejects=crowd.LastStaticRejects;
            }
            if(stairFlow||NarrowWalking||FlowYieldActive||FollowingNarrowFlow)yieldUntil=backoffUntil=0;
            if(!walking||pendingPath||!FollowingQueueContinuously&&delta.magnitude<=arrivalTolerance) steering=Vector3.zero;
            if(Time.time<yieldUntil) steering=Vector3.zero;
            else if(Time.time<backoffUntil) {
                if((transform.localPosition-backoffFrom).sqrMagnitude<.28f*.28f) steering=recoveryVelocity;
                else backoffUntil=0;
            }
            bool shortStep=DoorYieldVelocity(out var shift,out var facing);
            if(shortStep||RetreatingDoor) {
                float dt=Time.fixedDeltaTime;
                Vector3 intent=shortStep?shift:Vector3.ClampMagnitude(steering,.72f);
                if(!shortStep)facing=boardingTrain.transform.right*boardingTrain.side;
                FaceJourney(facing,shortStep?100:155);ApplyBodyFacing(Vector3.zero,dt);
                velocity=crowd.ValidateWalkingVelocity(this,Vector3.MoveTowards(Velocity,intent,dt*4.8f),true);
                doorYieldBlocked=shortStep&&crowd.LastWalkingRejection==Metro3DStopReason.ReservedPassage;
                if(shortStep)DesiredVelocity=intent;
                WalkPhase=velocity.sqrMagnitude>.003f?Metro3DWalkPhase.QueueAdvance:Metro3DWalkPhase.QueueWait;
                WalkingAlignmentScale=1;pivoting=false;curvedTurnRate=0;
            } else if(TryGripAisleStep(steerNow,out var gripStep)) {
                float dt=Time.fixedDeltaTime;
                ApplyBodyFacing(Vector3.zero,dt);
                velocity=crowd.ValidateWalkingVelocity(this,Vector3.MoveTowards(Velocity,gripStep,dt*3.4f),true);
                WalkPhase=velocity.sqrMagnitude>.004f?Metro3DWalkPhase.QueueAdvance:Metro3DWalkPhase.Still;
                WalkingAlignmentScale=1;pivoting=false;curvedTurnRate=0;
            } else velocity=WalkingStep(steering,walking,Time.fixedDeltaTime);
            Vector3 walkVelocity=velocity;
            float contactDt=Time.fixedDeltaTime;
            if(steerNow) {
                RefreshDoorContact();
                UpdateDoorCapsule(shortStep);
            }
            // A force sample cannot keep moving the actor once the
            // closest contributing shoulder has left contact range.
            if(contactForcePeer==null||contactForcePeer.PersistentId!=contactForcePeerId||
                !crowd.InDoorBodyContact(this,contactForcePeer)) {
                contactForceTarget=CabinContactForce=CabinPushVelocity=Vector3.zero;
                doorContactLoad=0;contactForcePeer=null;
            }
            CabinContactForce=Vector3.MoveTowards(CabinContactForce,contactForceTarget,470f*contactDt);
            if(BoardingPushEffort>0||pushEffortTarget>0)
                BoardingPushEffort=Mathf.MoveTowards(BoardingPushEffort,pushEffortTarget,5.5f*contactDt);
            if(CabinContactForce.sqrMagnitude>.8f||CabinPushVelocity.sqrMagnitude>.0004f) {
                float mass=70*HeightRatio*HeightRatio;
                CabinPushVelocity=Vector3.ClampMagnitude((CabinPushVelocity+CabinContactForce*(contactDt/mass))*.923f,.37f);
                Vector3 combined=Vector3.ClampMagnitude(velocity+CabinPushVelocity,Mathf.Max(.37f,velocity.magnitude+.15f));
                Vector3 validated=crowd.ValidateWalkingVelocity(this,combined,true);
                if(validated.sqrMagnitude>.0004f)velocity=validated;
            } else CabinPushVelocity=Vector3.zero;
            RotationDegreesPerSecond=Quaternion.Angle(beforeRotation,transform.rotation)/Time.fixedDeltaTime;
            RotationTravel+=RotationDegreesPerSecond*Time.fixedDeltaTime;
            verticalSpeed=motor.isGrounded?-2f:verticalSpeed-20*Time.fixedDeltaTime;
            Vector3 before=transform.position;motor.Move((velocity+Vector3.up*verticalSpeed)*Time.fixedDeltaTime);
            Vector3 actual=(transform.position-before)/Time.fixedDeltaTime;Velocity=new Vector3(actual.x,0,actual.z);
            if(aisleGrip!=null)GripYieldWeight=Mathf.Clamp01(
                Vector3.Dot(transform.localPosition-aisleGrip.feet,aisleGrip.Facing)/GripYieldDistance);
            ActualContactStep=Vector3.zero;
            if(CabinPushVelocity.sqrMagnitude>.004f&&(velocity-walkVelocity).sqrMagnitude>.0004f) {
                Vector3 direction=CabinPushVelocity.normalized;
                float pushed=Mathf.Min(Vector3.Dot(velocity-walkVelocity,direction),
                    Vector3.Dot(Velocity-walkVelocity,direction));
                if(pushed>0)ActualContactStep=direction*(pushed*contactDt);
            }
            if(!shortStep&&!inTrain&&CanReceiveDoorYield&&!HasCompanion&&contactForcePeer!=null&&
                ActualContactStep.sqrMagnitude>.0014f*.0014f&&DoorContactContext(out var pressedTrain,out int pressedDoor)&&
                (contactForcePeer.TravelsOutward||contactForcePeer.ClearingDoor(pressedTrain,pressedDoor))) {
                Vector3 outward=-pressedTrain.transform.right*pressedTrain.side;
                if(Vector3.Dot(ActualContactStep,outward)>.0012f&&
                    !RecentlyYieldedAtDoor(pressedTrain,pressedDoor))
                    TryYieldDoorLanding(pressedTrain,pressedDoor,contactForcePeer.transform.position,relay:true);
            }
            Journey.FinishGatePassage();
            UpdateStopReason(walking);
            bool progressing=Velocity.magnitude>=.045f;
            bool arrived=pathIndex>=pathCount&&FlatDistance(transform.position,target)<.23f;
            if(shortStep||RetreatingDoor||stairFlow||NarrowWalking||FlowYieldActive||FollowingNarrowFlow||
                ExpectedQueueWait||!walking||pendingPath||arrived||progressing) {
                blockedFor=0;recoveryStage=0;
            } else blockedFor+=Time.fixedDeltaTime;
            // These handoffs already have a local exit goal. Generic backoff
            // would put the passenger back in the way of the following flow.
            if(cabinEntryDoor>=0||exitQueueActive||Journey.ClearingStairLanding) {
                yieldUntil=backoffUntil=blockedFor=0;recoveryStage=0;
            }
            bool holdingDoorQueue=waypoint==0&&QueueActive&&
                (IsAlighting&&QueueLeader!=null||state==Activity.Board&&boardingTrain!=null&&
                    (QueueLeader!=null||StagedBoarding||FlatDistance(transform.position,target)<1.15f));
            if(holdingDoorQueue) {
                // A temporary pause in a committed branch belongs to the
                // queue; generic passage requests turn the rider upstream.
                yieldUntil=backoffUntil=blockedFor=0;recoveryStage=0;
            }
            if(!PortalCommitted&&!holdingDoorQueue&&blockedFor>.9f&&recoveryStage==0) {crowd.RequestPassage(this);yieldUntil=Time.time+.25f;recoveryStage=1;}
            if(!PortalCommitted&&!holdingDoorQueue&&blockedFor>1.6f&&recoveryStage==1) {
                if(crowd.TryBackoff(this,out recoveryVelocity)) {backoffUntil=Time.time+1.4f;backoffFrom=transform.localPosition;}
                recoveryStage=2;
            }
            if(blockedFor>3&&Time.time>=retryAt&&!PortalCommitted&&!ExpectedQueueWait) {
                blockedFor=0;retryAt=Time.time+4;Journey.Planner.ReportRecoverableFailure("Local path blocked after yield/backoff; destination retained");
                if(inTrain&&seat!=null&&state==Activity.Walk) {
                    Vector3 unreachable=target;
                    ReleaseSeat();SettleInsideCarriage(riding.NearestDoor(transform.position),unreachable);
                }
                else if(!helperTaskActive&&state==Activity.Walk&&seat==null&&(inTrain||Journey.CanBoard)) {
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
            bool gateCrossing=Journey.CrossingGate;
            // A nearby wall support does not need the turning
            // radius of a passenger walking down the corridor at full pace.
            bool closeApproach=state==Activity.Walk&&FinalWalkTarget&&SteeringDistance<1.12f&&
                world.Traffic.SupportFor(this)?.lean==true;
            if(!gateCrossing&&movingIntent&&angle>(closeApproach?80:110))pivoting=true;
            if(!movingIntent||gateCrossing||angle<(closeApproach?42:72))pivoting=false;
            Vector3 candidate;
            if(gateCrossing) {
                curvedTurnRate=0;WalkPhase=Metro3DWalkPhase.FacilityAlignment;
                ApplyBodyFacing(preferred.sqrMagnitude>.008f?preferred:Velocity,dt);
                candidate=Vector3.MoveTowards(Velocity,preferred,dt*(preferred.magnitude<actualSpeed?3.8f:2.3f));
            } else if(pivoting) {
                curvedTurnRate=0;WalkPhase=Metro3DWalkPhase.Pivoting;
                candidate=Vector3.MoveTowards(Velocity,Vector3.zero,dt*3.8f);
                ApplyBodyFacing(actualSpeed>.1f?Velocity:preferred,dt);
            } else if(movingIntent) {
                // Advance along the turn's midpoint instead of facing the
                // next waypoint first and taking a separate straight step.
                Vector3 from=transform.forward;
                float change=Vector3.SignedAngle(from,preferred,Vector3.up);
                float turn=Mathf.Sign(change)*Mathf.Min(ComfortableTurnRate,Mathf.Sqrt(2*540*Mathf.Abs(change)));
                curvedTurnRate=Mathf.MoveTowards(curvedTurnRate,turn,dt*540);
                if(Mathf.Abs(curvedTurnRate*dt)>Mathf.Abs(change))curvedTurnRate=change/dt;
                float yaw=curvedTurnRate*dt;
                Vector3 facing=Quaternion.AngleAxis(yaw,Vector3.up)*from;
                Vector3 direction=Quaternion.AngleAxis(yaw*.5f,Vector3.up)*from;
                float requestedSpeed=preferred.magnitude;
                // Tight final approaches need a smaller radius than ordinary
                // corridor turns. Ease off before the arc overshoots the goal.
                if(closeApproach&&angle>34) {
                    // The chord to a nearby target bounds the radius of the
                    // arc. A shallow corner retains the normal walk speed.
                    float radius=SteeringDistance/(2*Mathf.Sin(angle*Mathf.Deg2Rad));
                    requestedSpeed=Mathf.Min(requestedSpeed,Mathf.Max(.09f,radius*ComfortableTurnRate*Mathf.Deg2Rad));
                } else if(FinalWalkTarget&&!DoorCrossing&&SteeringDistance<1&&angle>45)
                    requestedSpeed*=Mathf.Lerp(1,.52f,Mathf.InverseLerp(45,105,angle));
                float nextSpeed=Mathf.MoveTowards(actualSpeed,requestedSpeed,dt*(requestedSpeed<actualSpeed?3.8f:2.3f));
                candidate=direction*nextSpeed;
                // Motor follows the feasible arc; the body follows that same
                // motion. The social-navigation vector remains a separate intent.
                ApplyBodyFacing(facing*Mathf.Max(.1f,nextSpeed),dt);
                WalkPhase=actualSpeed<.07f?Metro3DWalkPhase.Starting:Mathf.Abs(curvedTurnRate)>8?Metro3DWalkPhase.Curving:QueueCommitted?Metro3DWalkPhase.QueueAdvance:Metro3DWalkPhase.Walking;
            } else {
                curvedTurnRate=Mathf.MoveTowards(curvedTurnRate,0,dt*540);
                candidate=Vector3.MoveTowards(Velocity,Vector3.zero,dt*3.8f);
                ApplyBodyFacing(candidate,dt);WalkPhase=actualSpeed>.05f?Metro3DWalkPhase.Braking:Metro3DWalkPhase.Still;
            }
            WalkingAlignmentScale=ForwardWalkScale(candidate);
            Vector3 step=candidate*WalkingAlignmentScale;
            candidate=crowd.ValidateWalkingVelocity(this,step);
            if(candidate.sqrMagnitude<.0001f&&DoorCrossing&&!RetreatingDoor&&actualSpeed>.18f&&
                crowd.LastWalkingRejection==Metro3DStopReason.Passenger) {
                // When the shoulder ahead is still moving, try a shorter
                // step along the same arc with the complete Motor checks.
                candidate=crowd.ValidateWalkingVelocity(this,step*.58f);
            }
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
            float dt=Time.deltaTime;
            float measured=speed>.012f?speed:0;
            float reference=WalkReviewControlled&&WalkReviewAnimationCandidate?ReviewMeasuredWalkSpeed:walkReferenceSpeed;
            animationPace=Mathf.SmoothDamp(animationPace,measured,ref animationPaceRate,
                measured<animationPace?.112f:.072f,6f,dt);
            float weight=Mathf.Clamp01(animationPace/WalkBlendSpeed);
            float cadence=measured>0?Mathf.Clamp(measured/(Mathf.Max(.1f,reference)*Mathf.Max(.24f,weight)),0,2.1f):0;
            animationCadence=Mathf.SmoothDamp(animationCadence,cadence,ref animationCadenceRate,.082f,6f,dt);
            // A motor sample can be zero for one frame at contact. Keep the
            // blend and gait phase continuous through that sample.
            animator.SetFloat(Speed,animationPace<.001f?0:animationPace);
            animator.SetFloat(Gait,animationCadence<.001f?0:animationCadence);
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
            if(UpdateCompanionBoarding())return;
            if(!inTrain&&Journey.Tick()) return;
            if(RegroupOnPlatform())return;
            if(state==Activity.Wait)SetWaitingFacing();
            var active=boardingTrain!=null?boardingTrain:world.Transit.BoardingTrain(this);
            if(inTrain&&riding!=null) {
                bool prepare=WantsToAlight||ApproachingAlightStop();
                if(prepare) {
                    var cue=WantsToAlight?TransitNotice.Alight:TransitNotice.PrepareAlight;
                    if(!NoticeTransit(cue,riding))return;
                    RecordDecision(MetroDecision.Prepare,MetroDecisionReason.DestinationNear,margin:riding.MovementRemaining);
                    Journey.PreparingToAlight();
                    world.Traffic.ReleaseStanding(this);cabinArrivalDoor=cabinSettlementDoor=-1;
                    if(state==Activity.Sit) {BeginStand();return;}
                    if(state!=Activity.Alight) {
                        ReleaseSeat();door=riding.NearestDoor(transform.position);waypoint=0;state=Activity.Alight;
                        alightingDoorsTried=1<<door;blockedAlightingDoorSince=0;alightingProgressDistance=float.PositiveInfinity;
                        SetTarget(riding.DoorPoint(door,false),true);
                    }
                    AdvanceDoor(false);return;
                }
            }
            if(!inTrain&&Journey.CanBoard&&active!=null&&(active.CanBoard||CanFinishGroupBoarding(active))&&active.CurrentStop==stationIndex) {
                if(!ChooseBoardingDoor(active,out int selectedDoor))return;
                if(!NoticeTransit(TransitNotice.Board,active))return;
                if(state==Activity.Sit) {BeginStand();return;}
                if(state!=Activity.Board) {
                    if(!BeginCompanionBoarding(active,selectedDoor))return;
                    door=CompanionService==active?companionDoor:selectedDoor;
                    StagedBoarding=world.Traffic.PreserveStagedDoorSide(this,door);
                    ReleaseSeat();world.Traffic.ReleaseStanding(this);
                    missedService=null;platformFlankUntil=0;
                    boardingTrain=active;waypoint=0;state=Activity.Board;Journey.BeginBoard();
                }
                AdvanceDoor(true);return;
            }
            if(state==Activity.Board&&(active==null||!active.CanBoard&&!CanFinishGroupBoarding(active))&&waypoint==0) {
                CancelDoorBoarding();return;
            }
            if(state==Activity.Board) {AdvanceDoor(true);return;}
            if(missedService!=null&&ContinuePlatformRequeue())return;
            if(state!=Activity.Alight&&Time.time>=transitLookUntil)NoticeTransit(TransitNotice.None,null);
            if(!inTrain&&Journey.CanBoard&&UpdatePlatformRest())return;
            if(state==Activity.Sit) {
                if(world.settings.ambientEventsEnabled&&inTrain&&riding!=null&&!riding.Moving&&riding.DoorsOpen&&lastArrivalReaction!=riding.CurrentStop&&
                    !Journey.AtDestination(riding.CurrentStop)&&seatedPastime?.Dozing==true&&(index+riding.CurrentStop)%4==0) {
                    lastArrivalReaction=riding.CurrentStop;lookAtSignUntil=Time.time+1.8f;seatedPastime.Release();
                }
                ChooseSeatedPastime();return;
            }
            if(state==Activity.SitDown||state==Activity.StandUp) return;
            if(state==Activity.Walk&&FlatDistance(transform.position,target)<(world.Traffic.SupportFor(this)!=null?.105f:.23f)) {
                if(seat!=null) {BeginSit();return;}
                WaitJourney();
            }
            if(state==Activity.Wait&&inTrain&&riding!=null&&riding.MovementRemaining>16&&!checkedMovingSupport) {
                checkedMovingSupport=true;
                if(Journey.Personality.PersonalSpace>.23f&&index%3!=0&&world.Traffic.SupportFor(this)?.lean!=true&&
                    world.Traffic.TryLeanPoint(this,riding,out var lean)) {
                    WalkToLeanSupport(lean);return;
                }
            }
            if(state==Activity.Wait&&TryGivePlayerRoom())return;
            if(state==Activity.Wait&&inTrain&&riding!=null&&Time.time>=nextSeatLook){
                nextSeatLook=Time.time+5+(index%9)*.27f;
                float margin=riding.Moving?riding.MovementRemaining:world.Transit.BoardingSeconds(riding)+world.settings.travelSeconds;
                // A committed support/life action already owns this waiting
                // position. Periodic seat discovery must not restart its path.
                if(world.Traffic.SupportFor(this)==null&&contacts?.LifeActive!=true&&
                    margin>AlightingLeadSeconds+14&&Journey.SeatPreference>.48f) {
                    if(cabinArrivalDoor<0) {
                        if(TrySeat())return;
                    } else if(TrySeat(2.35f,cabinArrivalDoor,directOnly:true)) {
                        TrackCabinSettlement(cabinArrivalDoor);return;
                    }
                }
            }
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
                if(seatedPastime.NeedsLegRoom&&!Metro3DSeatedPastime.HasLegRoom(this,crowd))seatedPastime.Release();
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
            if(state==Activity.Wait&&seat==null&&world.Traffic.StandingOnPlatformStrip(this)&&
                world.Traffic.TryGivePlatformRoom(this,out var clearPoint)) {
                WalkJourney(clearPoint,false);
                return true;
            }
            float wait=world.Transit.TimeToBoarding(this);
            if(seat!=null&&wait<6) {
                RecordDecision(MetroDecision.Prepare,MetroDecisionReason.TimeMargin,margin:wait);
                if(state==Activity.Sit)BeginStand();
                else if(state==Activity.Walk) {ReleaseSeat();SetWaitingPoint();}
                return true;
            }
            if(seat!=null||wait<12||world.Traffic.SupportFor(this)!=null||Journey.OnStairs)return false;
            if(wait<28&&world.Traffic.StagedPlatformDoor(this,out _,out _))return false;
            Vector3 local=transform.localPosition;
            if(Mathf.Abs(local.y)>.18f||local.x*Journey.TrainSide>=-Metro3DWorld.ScreenDoorX)return false;
            float density=crowd.LocalDensity(transform.position,transform.parent);
            float preference=Journey.SeatPreference+(1-Mathf.Clamp01(Journey.Personality.Mobility))*.14f-density*.10f;
            float timePreference=Mathf.InverseLerp(16,40,wait);
            float threshold=Mathf.Lerp(.54f,.38f,timePreference);
            if(preference>threshold&&TrySeat(Mathf.Lerp(5,10,timePreference))){
                RecordDecision(MetroDecision.Rest,MetroDecisionReason.TimeMargin,preference,threshold,wait,next:nextPlatformRestCheck);return true;
            }
            float leanInterest=Mathf.Repeat(index*.618034f,1);
            if(leanInterest>Mathf.Lerp(.25f,.57f,Journey.Personality.Patience)||
                !world.Traffic.TryPlatformLeanPoint(this,out var point)){
                RecordDecision(MetroDecision.Wait,MetroDecisionReason.TimeMargin,preference,threshold,wait,next:nextPlatformRestCheck);return false;
            }
            RecordDecision(MetroDecision.Rest,MetroDecisionReason.TimeMargin,1-leanInterest,1-Mathf.Lerp(.25f,.57f,Journey.Personality.Patience),wait,selected:1,next:nextPlatformRestCheck);
            WalkToLeanSupport(point);return true;
        }

        void WalkToLeanSupport(Vector3 point)
        {
            // This support has already been chosen. Arrival must not start
            // the one-time cabin search and replace the reservation.
            triedSeat=true;
            WalkJourney(point,!crowd.CanFollowWalkingSegment(this,point));
        }

        bool TryGripAisleStep(bool perceive,out Vector3 step)
        {
            step=Vector3.zero;
            var support=state==Activity.Wait&&inTrain&&motor.isGrounded?world.Traffic.SupportFor(this):null;
            if(support==null||support.lean||support.train!=riding||support.frame!=transform.parent||
                (transform.localPosition-support.feet).sqrMagnitude>.30f*.30f) {
                aisleGrip=null;gripYieldUntil=GripYieldWeight=0;
                return false;
            }
            if(aisleGrip!=support) {aisleGrip=support;gripYieldUntil=0;}
            Vector3 facing=support.frame.TransformDirection(support.Facing);
            if(Vector3.Dot(transform.forward,facing)<.96f)return false;
            if(perceive&&crowd.NeedsGripClearance(this,support))gripYieldUntil=Time.time+.42f;
            float goal=Time.time<gripYieldUntil?GripYieldDistance:0;
            float offset=Vector3.Dot(transform.localPosition-support.feet,support.Facing);
            float error=goal-offset;
            // A small, acceleration-limited step within the same support reservation.
            if(Mathf.Abs(error)<.012f)return false;
            step=facing*Mathf.Clamp(error*4.6f,-.26f,.43f);
            return true;
        }

        void SetWaitingFacing()
        {
            var support=world.Traffic.SupportFor(this);
            if(support!=null&&support.frame==transform.parent) {
                FaceJourney(support.frame.TransformDirection(support.Facing),100);
            } else if(!inTrain&&(Journey.Waiting||missedService!=null||world.Traffic.StagedPlatformDoor(this,out _,out _)))
            {
                var station=world.stations[stationIndex];
                Vector3 towardTrack=station.TransformDirection(Vector3.right*Journey.TrainSide);
                if(Mathf.Abs(transform.localPosition.y)>.28f||transform.localPosition.x*Journey.TrainSide>=0)return;
                Vector3 focus=towardTrack-station.forward*(Journey.TravelDirection*.23f);
                bool staged=world.Traffic.StagedPlatformDoor(this,out int doorIndex,out _);
                if(staged) {
                    float along=Metro3DTrain.DoorZ[doorIndex]-transform.localPosition.z;
                    focus=towardTrack+station.forward*Mathf.Clamp(along*.30f,-.50f,.50f);
                }
                foreach(var service in world.Trains) {
                    if(service==null||service.CurrentStop!=stationIndex||service.side!=Journey.TrainSide||
                        service.TravelDirection!=Journey.TravelDirection||service.ServiceLine!=Journey.NextLine)continue;
                    if(service.Moving&&service.MovementRemaining>5.5f)continue;
                    if(service.Moving)focus=towardTrack-station.forward*(service.TravelDirection*.74f);
                    else {
                        Vector3 towardDoor=service.DoorPoint(staged?doorIndex:service.NearestDoor(transform.position),true)-transform.position;
                        focus=towardTrack+station.forward*Mathf.Clamp(Vector3.Dot(towardDoor,station.forward)*.36f,-.62f,.62f);
                    }
                    if(service.Moving?service.MovementRemaining<2.1f:service.CanBoard)platformAttentionUntil=Time.time+.32f;
                    break;
                }
                float angle=(Mathf.Repeat(index*.618034f,1)-.5f)*17f;
                FaceJourney(Quaternion.AngleAxis(angle,station.up)*focus,100);
            }
        }
        int doorLane;
        internal bool StagedBoarding {get;private set;}
        internal float NextQueueSideStep {get;set;}
        internal float QueueSideStepUntil {get;set;}
        internal Metro3DTrain CrossingTrain=>DoorCrossing?(IsAlighting?riding:boardingTrain):null;
        internal int CrossingDoor=>door;
        internal bool ApproachingOpenDoor=>state==Activity.Board&&waypoint==0&&boardingTrain!=null&&boardingTrain.CanBoard;
        internal bool ApproachingBoardingDoor(Metro3DTrain train,int number)
            =>state==Activity.Board&&waypoint==0&&boardingTrain==train&&door==number&&QueueActive&&
                train.DoorsOpen&&train.CanBoard&&!ClearingRetreatPath(train,number);
        internal int CrossingLane=>doorLane;
        bool retreatingBoarder;
        float boardingProgress,boardingProgressAt,boardAlignmentUntil;
        float alightingClaimProgress,alightingClaimProgressAt;
        bool restoringAlightingAlignment;
        internal bool RetreatingDoor=>state==Activity.Board&&retreatingBoarder;
        internal bool TravelsOutward=>IsAlighting||RetreatingDoor;

        bool BodyInsideCarriage(Metro3DTrain train)
        {
            Vector3 feet=transform.position;
            Vector3 trail=feet-train.transform.right*train.side*(WalkingRadius+.012f);
            return train.Contains(feet)&&train.Contains(trail);
        }

        bool ReturnBlockedBoarder(Metro3DTrain train)
        {
            float progress=Vector3.Dot(transform.position-train.DoorPoint(door,true,doorLane),train.transform.right*train.side);
            if(progress>boardingProgress+.045f) {
                boardingProgress=progress;boardingProgressAt=Time.time;
            }
            if(!retreatingBoarder) {
                if(train.CanBoard||BodyInsideCarriage(train)||Time.time-boardingProgressAt<1.65f)return false;
                // Allow this rider to leave the opening under the same motor
                // and door reservation before the train closes its doors.
                retreatingBoarder=true;
                SetTarget(train.DoorPoint(door,true,doorLane)-train.transform.right*train.side*.39f,false);
                nextSteering=0;
            }
            crowd.ClearLanding(train,door,this,doorLane);
            float outside=-train.side*world.stations[train.CurrentStop].InverseTransformPoint(transform.position).x;
            if(outside<Metro3DWorld.ScreenDoorX+WalkingRadius+.12f)return true;
            CancelDoorBoarding();
            return true;
        }

        void CancelDoorBoarding()
        {
            missedService=boardingTrain;missedDoor=door;
            world.Traffic.ReleaseDoor(this);
            boardingTrain=null;retreatingBoarder=false;waypoint=0;
            Journey.CancelBoard();
            nextQueueRejoin=Time.time+Mathf.Lerp(.58f,1.55f,Journey.Personality.Patience);
            WaitJourney();SetWaitingFacing();
        }

        bool ContinuePlatformRequeue()
        {
            if(inTrain||!Journey.CanBoard) {missedService=null;return false;}
            if(Time.time<nextQueueRejoin)return true;
            if(!missedService.Moving&&missedService.CurrentStop==stationIndex&&missedService.DoorsOpen&&
                crowd.ObserveDoor(missedService,missedDoor).incoming)
                return true;
            if(world.Traffic.TryRejoinPlatformLine(this,missedService,missedDoor,out Vector3 point)) {
                missedService=null;triedSeat=true;
                if(FlatDistance(transform.position,point)>.09f)
                    WalkJourney(point,!crowd.CanFollowWalkingSegment(this,point));
                return true;
            }
            nextQueueRejoin=Time.time+Mathf.Lerp(.68f,1.55f,Journey.Personality.Patience);
            return true;
        }

        void AdvanceDoor(bool boarding)
        {
            Metro3DTrain t=boarding?boardingTrain:riding;if(t==null) return;
            if(boarding) {
                if(waypoint!=0&&ReturnBlockedBoarder(t))return;
                if(waypoint==0&&t.DoorsOpen&&!t.CanBoard&&!CanFinishGroupBoarding(t)) {
                    CancelDoorBoarding();return;
                }
            }
            if(waypoint!=0)crowd.ClearLanding(t,door,this,doorLane);
            if(waypoint==0) {
                Vector3 approach=world.Traffic.DoorApproach(this,t,door,boarding);
                Vector3 entry=t.DoorPoint(door,boarding);
                float distance=FlatDistance(transform.position,entry);
                float queueDistance=FlatDistance(transform.position,approach);
                bool direct=boarding?distance<1.22f:distance<1.15f;
                if(boarding&&ClearingRetreatPath(t,door))return;
                if(!boarding)crowd.ClearLanding(t,door,this);
                if(t.DoorsOpen&&(!boarding||IntendsDoorAdvance)&&(direct||queueDistance<.28f)&&world.Traffic.TryDoor(this,t,door,boarding,out doorLane)) {
                    waypoint=1;ClearQueueContext();
                    if(boarding) {
                        retreatingBoarder=false;boardingProgressAt=Time.time;boardAlignmentUntil=0;
                        boardingProgress=Vector3.Dot(transform.position-t.DoorPoint(door,true,doorLane),t.transform.right*t.side);
                    }
                    else {
                        restoringAlightingAlignment=false;
                        alightingClaimProgress=-FlatDistance(transform.position,t.DoorPoint(door,false,doorLane));
                        alightingClaimProgressAt=Time.time;
                    }
                    SetTarget(t.DoorPoint(door,boarding,doorLane),false);
                    nextSteering=0;
                } else {
                    if((target-approach).sqrMagnitude>.0025f)
                        FollowQueueGoal(approach,preferDirect:!boarding,allowLocalAdvance:t.DoorsOpen&&
                            (!boarding||t.CanBoard&&!crowd.ObserveDoor(t,door).incoming));
                    else FaceQueue();
                    if(boarding&&StagedBoarding&&queuePermissionWait&&queueDistance<.22f) {
                        yieldUntil=backoffUntil=0;blockedFor=0;recoveryStage=0;
                    }
                    if(boarding&&!HoldingPlatformFlank&&Velocity.sqrMagnitude<.19f*.19f&&BoardingDoorLook(out var look))
                        FaceJourney(look,Mathf.Lerp(105,165,BoardingUrgency));
                    else if(QueueLeader==null&&queueDistance<.28f)FaceJourney(entry-transform.position);
                    if(!boarding&&t.DoorsOpen)RetryBlockedAlightingDoor(t);
                    else blockedAlightingDoorSince=0;
                    return;
                }
            }
            if(!boarding&&RecoverAlightingAlignment(t))return;
            if(waypoint==1) {
                Vector3 crossingEntry=t.DoorPoint(door,boarding,doorLane);
                Vector3 crossingExit=t.DoorPoint(door,!boarding,doorLane);
                float gap=FlatDistance(transform.position,crossingEntry);
                if(gap>.15f&&((!boarding&&restoringAlightingAlignment)||!CanCrossDoorDiagonally(crossingEntry,crossingExit)))return;
                if(!boarding) {
                    restoringAlightingAlignment=false;
                    alightingClaimProgress=Vector3.Dot(transform.position,-t.transform.right*t.side);
                    alightingClaimProgressAt=Time.time;
                }
                waypoint=2;SetTarget(crossingExit,false);
                nextSteering=0;
            }
            if(waypoint!=2) return;
            if(boarding&&RecoverBoardingAlignment(t))return;
            FaceTravel();
            Vector3 feet=transform.position;
            // Complete a boarding only once the back of the body is inside
            // the carriage. The rest of the walk belongs to cabin movement.
            bool crossed=boarding?BodyInsideCarriage(t)
                :(-t.side*world.stations[t.CurrentStop].InverseTransformPoint(feet).x)>Metro3DWorld.ScreenDoorX+.24f;
            if(!crossed) return;
            world.Traffic.ReleaseDoor(this);var old=transform.parent;
            inTrain=boarding;stationIndex=t.CurrentStop;transform.SetParent(boarding?t.transform:world.stations[stationIndex],true);
            egressTrain=boarding?null:t;egressDoor=door;
            if(!boarding)egressPoint=transform.parent.InverseTransformPoint(t.DoorPoint(door,true,doorLane));
            world.Traffic.ChangedParent(old,transform.parent);riding=boarding?t:null;boardingTrain=null;triedSeat=checkedMovingSupport=false;
            if(boarding) {
                boardedContactUntil=Time.time+3.8f;
                Journey.OnBoard();
                // Preserve the moving handoff through the doorway. Reserve
                // a seat or standing spot once the vestibule is clear.
                cabinEntryDoor=door;cabinEntryUntil=Time.time+4.5f;nextCabinEntryPlan=0;
                state=Activity.Walk;targetLocal=transform.localPosition;
                ContinueIntoCabin();
            }
            else {
                Journey.OnAlight(stationIndex,t.side,t.ServiceLine);
                exitQueueActive=true;exitWaypointLeavesQueue=false;
                yieldUntil=backoffUntil=0;
                PlanPlatformExit();
            }
        }
        void PlanPlatformExit()
        {
            Vector3 entry=transform.parent.TransformPoint(egressPoint-Vector3.forward*(doorLane*Metro3DTrain.DoorLaneOffset));
            Vector3 point=crowd.DoorDepartureTarget(this,egressTrain,egressDoor,entry,out exitWaypointLeavesQueue);
            nextExitDirection=Time.time+.32f;
            WalkJourney(point,false);nextSteering=0;
        }

        bool AdvancePlatformExit()
        {
            if(!exitQueueActive)return false;
            float gap=FlatDistance(transform.position,target);
            if(gap<.17f&&exitWaypointLeavesQueue) {
                exitQueueActive=false;
                if(Journey.CanBoard)SetWaitingPoint();
                return false;
            }
            if(egressTrain.CurrentStop==stationIndex&&egressTrain.DoorsOpen&&!egressTrain.Moving)
                crowd.ClearLanding(egressTrain,egressDoor,this);
            if(Time.time>=nextExitDirection&&(gap<.17f||Velocity.sqrMagnitude<.13f*.13f))PlanPlatformExit();
            return true;
        }

        bool RecoverBoardingAlignment(Metro3DTrain train)
        {
            Vector3 feet=transform.position;
            if(boardAlignmentUntil>0) {
                if(Time.time<boardAlignmentUntil&&FlatDistance(feet,target)>.10f)return true;
                boardAlignmentUntil=0;
                boardingProgressAt=Time.time;
                SetTarget(train.DoorPoint(door,false,doorLane),false);
                nextSteering=0;
                return false;
            }
            if(!train.CanBoard||Time.time-boardingProgressAt<.70f||Velocity.sqrMagnitude>.10f*.10f)
                return false;
            Vector3 entry=train.DoorPoint(door,true,doorLane);
            Vector3 inward=train.transform.right*train.side,lateral=train.transform.forward;
            float depth=Vector3.Dot(feet-entry,inward);
            float offset=Vector3.Dot(feet-entry,lateral);
            float room=Metro3DTrain.DoorWidth*.5f-WalkingRadius-.04f;
            if(depth<-.30f||depth>.79f||Mathf.Abs(offset+doorLane*Metro3DTrain.DoorLaneOffset)<room)
                return false;

            // An admitted diagonal can later drift outside the jamb when
            // bodies press from the side. Rejoin our lane without backing
            // into the queue or treating other bodies as vacant space.
            Vector3 step=feet-lateral*Mathf.Clamp(offset,-.42f,.42f)+inward*.075f;
            if(crowd.ObserveDoor(train,door).incoming||!crowd.CanFollowWalkingSegment(this,step)||
                !crowd.DoorPassageFree(this,train,door,feet,step))
                return false;
            boardAlignmentUntil=Time.time+.95f;
            SetTarget(step,false);
            nextSteering=0;
            return true;
        }

        bool RecoverAlightingAlignment(Metro3DTrain train)
        {
            Vector3 feet=transform.position,outward=-train.transform.right*train.side;
            Vector3 entry=train.DoorPoint(door,false,doorLane);
            float progress=waypoint==1?-FlatDistance(feet,entry):Vector3.Dot(feet,outward);
            if(progress>alightingClaimProgress+.065f) {
                alightingClaimProgress=progress;alightingClaimProgressAt=Time.time;
            }
            float stalled=Time.time-alightingClaimProgressAt;
            if(stalled<1.10f||!BodyInsideCarriage(train))return false;
            if(!restoringAlightingAlignment&&waypoint==2&&
                Mathf.Abs(Vector3.Dot(feet-entry,train.transform.forward))>.32f&&
                crowd.CanFollowWalkingSegment(this,entry)) {
                // Steering can take a rider outside its accepted diagonal.
                // Realign inside the carriage under the same door reservation.
                restoringAlightingAlignment=true;alightingClaimProgressAt=Time.time;
                alightingClaimProgress=-FlatDistance(feet,entry);
                waypoint=1;SetTarget(entry,false);nextSteering=0;
                return true;
            }
            if(stalled<2.8f||!world.Traffic.TryAlternateAlightingDoor(this,train,door,alightingDoorsTried,out int alternate))
                return false;
            SwitchAlightingDoor(train,alternate);return true;
        }
        bool ContinueIntoCabin()
        {
            if(!inTrain||riding==null||WantsToAlight) {cabinEntryDoor=-1;return false;}
            float depth=transform.localPosition.x*riding.side;
            float sideGap=Mathf.Abs(transform.localPosition.z-Metro3DTrain.DoorZ[cabinEntryDoor]);
            bool pastDoor=depth>=-.08f||depth>=-.55f&&sideGap>Metro3DTrain.DoorWidth*.5f+.19f;
            if(pastDoor||Time.time>=cabinEntryUntil) {
                int entrance=cabinEntryDoor;
                cabinEntryDoor=-1;triedSeat=true;
                if(HasCompanion&&!Companion.inTrain)WalkJourney(world.Traffic.TrainStandingPoint(this,riding),true);
                else {
                    if(!TrySeat(Mathf.Lerp(5.25f,7f,Journey.SeatPreference),entrance))
                        SettleInsideCarriage(entrance);
                    Journey.ActivityChosen();TrackCabinSettlement(entrance);
                }
                return false;
            }
            // A visible seat can be claimed before reaching the aisle center.
            // A clear forward diagonal preserves motion off the threshold.
            if(depth>=-.68f&&nextCabinEntryPlan<=Time.time&&
                !crowd.ObserveDoor(riding,cabinEntryDoor).incoming&&
                TrySeat(Mathf.Lerp(2.8f,5.2f,Journey.SeatPreference),cabinEntryDoor,directOnly:true)) {
                TrackCabinSettlement(cabinEntryDoor);
                cabinEntryDoor=-1;triedSeat=true;Journey.ActivityChosen();return false;
            }
            Vector3 goalDelta=riding.transform.TransformPoint(targetLocal)-transform.position;
            goalDelta.y=0;
            // Yielding or door pressure can move us past the last cabin
            // target. Retire it before rechecking the space farther inside.
            if(Vector3.Dot(goalDelta,riding.transform.right*riding.side)<-.075f) {
                target=transform.position;targetLocal=transform.localPosition;
                steering=goalDelta=Vector3.zero;
            }
            if(Time.time>=nextCabinEntryPlan&&(nextCabinEntryPlan==0||
                goalDelta.sqrMagnitude<.18f*.18f||Vector3.Dot(Velocity,goalDelta)<.024f)) {
                nextCabinEntryPlan=Time.time+.30f;
                bool advance=crowd.TryCabinEntryGoal(this,riding,cabinEntryDoor,out var point);
                if(!advance&&goalDelta.sqrMagnitude<.18f*.18f) {
                    // An occupied aisle can still leave room for a short
                    // forward-diagonal step out of the doorway corridor.
                    Vector3 inside=riding.DoorPoint(cabinEntryDoor,false)+riding.transform.right*(riding.side*.86f);
                    advance=crowd.TryClearCabinApproach(this,riding,cabinEntryDoor,inside,out point);
                    if(!advance)nextCabinEntryPlan=Time.time+.24f;
                }
                if(advance) {
                    WalkJourney(point,false);nextSteering=0;
                    if((point-transform.position).sqrMagnitude<.65f*.65f)
                        nextCabinEntryPlan=Time.time+.18f;
                }
            }
            return true;
        }
        Vector3 LandingLookahead(Vector3 targetPoint)
        {
            Vector3 feet=transform.position;
            Vector3 heading=transform.parent.TransformDirection(landingForwardLocal);
            Vector3 route=Vector3.ProjectOnPlane(targetPoint-feet,transform.up);
            if(route.sqrMagnitude<.46f*.46f)return targetPoint;
            float alignment=Vector3.Dot(heading,route.normalized);
            if(alignment<-.27f||alignment>.91f)return targetPoint;
            float turn=Mathf.Lerp(.53f,.94f,1-(landingTurnUntil-Time.time)/.48f);
            Vector3 tangent=Vector3.Slerp(heading,route.normalized,turn);
            Vector3 lead=feet+tangent*Mathf.Clamp(.35f+Velocity.magnitude*.30f,.45f,.74f);
            return crowd.CanFollowWalkingSegment(this,lead)?lead:targetPoint;
        }

        Vector3 DoorApproachLookahead(Vector3 entry)
        {
            var train=CrossingTrain;
            if(train==null||restoringAlightingAlignment||boardAlignmentUntil>0)return entry;
            Vector3 feet=transform.position;
            float gap=FlatDistance(feet,entry);
            if(gap<.09f||gap>.70f)return entry;
            Vector3 exit=train.DoorPoint(door,IsAlighting,doorLane);
            Vector3 axis=(exit-entry).normalized;
            if(Vector3.Dot(feet-entry,axis)>.11f)return entry;
            float drift=Vector3.ProjectOnPlane(feet-entry,axis).magnitude;
            if(drift>.24f)return entry;
            // Preview the exit only once the same lane and clearance
            // checks used to advance the action would allow this crossing.
            if(!CanCrossDoorDiagonally(entry,exit))return entry;
            Vector3 lead=entry+axis*Mathf.Lerp(.14f,.29f,Mathf.InverseLerp(.70f,.22f,gap));
            return crowd.CanFollowWalkingSegment(this,lead)&&
                crowd.DoorPassageFree(this,train,door,feet,lead,leaving:IsAlighting)?lead:entry;
        }

        bool CanCrossDoorDiagonally(Vector3 entry,Vector3 exit)
        {
            Vector3 axis=Vector3.ProjectOnPlane(exit-entry,Vector3.up);
            float depth=axis.magnitude;
            if(depth<.1f)return false;
            axis/=depth;
            Vector3 from=Vector3.ProjectOnPlane(transform.position-entry,Vector3.up);
            Vector3 to=Vector3.ProjectOnPlane(exit-transform.position,Vector3.up);
            float progress=Vector3.Dot(to,axis);
            if(progress<.15f||from.sqrMagnitude>1.25f*1.25f)return false;
            // Check the path at both thresholds, including the room required
            // by the other lane if the door has granted two reservations.
            float outer=Metro3DTrain.DoorWidth*.5f-WalkingRadius-.045f;
            float minimum=doorLane==0?-outer+.095f:-.19f;
            float maximum=doorLane==0?outer-.095f:outer-Metro3DTrain.DoorLaneOffset;
            float atNear=Mathf.Clamp01((depth*.29f-Vector3.Dot(from,axis))/progress);
            float atFar=Mathf.Clamp01((depth*.72f-Vector3.Dot(from,axis))/progress);
            // Keep the inner half clear for the next shoulder, but use
            // the real width between our lane and the jamb on the outer side.
            Vector3 lateral=doorLane==0?Vector3.Cross(Vector3.up,axis):CrossingTrain.transform.forward*doorLane;
            float nearSide=Vector3.Dot(from+to*atNear,lateral);
            float farSide=Vector3.Dot(from+to*atFar,lateral);
            if(nearSide<minimum||nearSide>maximum||farSide<minimum||farSide>maximum||
                !crowd.CanFollowWalkingSegment(this,exit))return false;
            // A moving leader owns the space farther ahead. Require clear
            // space for the first step and let the motor follow its pace.
            bool follows=world.Traffic.HasMovingDoorLeader(this,CrossingTrain,door,doorLane,axis);
            Vector3 clearTo=follows?Vector3.MoveTowards(transform.position,exit,.34f):exit;
            return crowd.DoorPassageFree(this,CrossingTrain,door,transform.position,clearTo);
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
            if(!world.Traffic.IsAlightingHead(train,door,this)) {blockedAlightingDoorSince=0;alightingProgressDistance=float.PositiveInfinity;return;}
            float distance=FlatDistance(transform.position,train.DoorPoint(door,false));
            // Side-to-side avoidance is motion, but not progress toward the exit.
            // Preserve the original patience window while evaluating useful progress.
            if(blockedAlightingDoorSince==0||distance<alightingProgressDistance-.15f){
                blockedAlightingDoorSince=Time.time;alightingProgressDistance=distance;
            }
            if(Time.time-blockedAlightingDoorSince<6)return;
            blockedAlightingDoorSince=Time.time-4;
            if(!world.Traffic.TryAlternateAlightingDoor(this,train,door,alightingDoorsTried,out int alternate))return;
            SwitchAlightingDoor(train,alternate);
        }
        void SwitchAlightingDoor(Metro3DTrain train,int alternate)
        {
            world.Traffic.ReleaseDoor(this);
            waypoint=0;restoringAlightingAlignment=false;
            door=alternate;alightingDoorsTried|=1<<door;blockedAlightingDoorSince=0;alightingProgressDistance=float.PositiveInfinity;
            FollowQueueGoal(world.Traffic.DoorApproach(this,train,door,false));
            Journey.Planner.ReportRecoverableFailure("Alighting lane occupied; using another door in the carriage");
        }
        internal void ContinueToPlatformWait(Vector3 point,Vector3 landingForward)
        {
            triedSeat=true;
            ContinueJourney(point,landingForward);
            landingTurnUntil=Time.time+.48f;
            landingForwardLocal=transform.parent.InverseTransformDirection(landingForward);
            // The landing is a new walking surface. If the reserved wait spot
            // is directly ahead or diagonal, preserve the moving handoff.
            if(pendingPath&&Vector3.Dot(point-transform.position,landingForward)>-.04f&&
                crowd.CanFollowWalkingSegment(this,point))pendingPath=false;
        }
        public void SetWaitingPoint()
        {
            state=Activity.Walk;triedSeat=true;
            Vector3 point=world.Traffic.PlatformWaitingPoint(this);
            bool direct=world.Traffic.StagedPlatformDoor(this,out _,out _)&&
                crowd.CanFollowWalkingSegment(this,point);
            SetTarget(point,!direct);
        }
        void TrackCabinSettlement(int entrance)
        {
            cabinArrivalDoor=cabinSettlementDoor=entrance;cabinSettlementRetries=0;
            cabinProgressFeet=transform.localPosition;cabinProgressSince=Time.time;
        }

        bool RecoverCabinSettlement()
        {
            if(cabinSettlementDoor<0)return false;
            if(!inTrain||riding==null||state!=Activity.Walk||WantsToAlight||
                ApproachingAlightStop()||FlatDistance(transform.position,target)<.19f) {
                cabinSettlementDoor=-1;
                return false;
            }
            Vector3 local=transform.localPosition;
            if(FlatDistance(local,cabinProgressFeet)>.14f) {
                cabinProgressFeet=local;cabinProgressSince=Time.time;
                return false;
            }
            if(pendingPath||Time.time-cabinProgressSince<1.35f)return false;
            Vector3 previous=target;
            // A different short route can remain open while the first seat's
            // approach is blocked. Commit only after the replacement is found.
            if(cabinSettlementRetries++==0&&TrySeat(2.35f,cabinSettlementDoor,directOnly:true,avoid:previous)) {
                cabinProgressSince=Time.time;nextSteering=0;
                return true;
            }
            Vector3 nearby=world.Traffic.TrainStandingPoint(this,riding,previous,cabinSettlementDoor,reachableOnly:true);
            ReleaseSeat();yieldUntil=backoffUntil=blockedFor=0;recoveryStage=0;
            if(FlatDistance(transform.position,nearby)>.11f&&cabinSettlementRetries<3) {
                WalkJourney(nearby,false);nextSteering=0;cabinProgressSince=Time.time;
            } else {
                world.Traffic.ReleaseStanding(this);
                cabinSettlementDoor=-1;WaitJourney();
                nextSeatLook=Time.time+12f+Journey.Personality.Patience*4f;
            }
            return true;
        }

        bool ApproachingAlightStop() => riding.Moving&&riding.MovementRemaining<=AlightingLeadSeconds&&
            (Journey.AtDestination(riding.CurrentStop)||world.Transit.EndsLine(riding));

        void SettleInsideCarriage(int entrance,Vector3? avoid=null)
        {
            Vector3 point=world.Traffic.TrainStandingPoint(this,riding,avoid,entrance);
            WalkJourney(point,!crowd.CanFollowWalkingSegment(this,point));
        }

        bool TrySeat(float maxDistance=6,int entrance=-1,bool directOnly=false,Vector3? avoid=null)
        {
            // A personal-space-oriented, less fatigued rider may prefer a real
            // nearby wall rather than consume a seat. Destination stays fixed.
            if(entrance<0&&inTrain&&Journey.SeatPreference<.68f&&Journey.Personality.PersonalSpace>.45f&&world.Traffic.TryLeanPoint(this,riding,out var leanPoint)) {
                WalkToLeanSupport(leanPoint);return true;
            }
            Metro3DFacility best=null;float score=float.NegativeInfinity;
            foreach(var f in world.facilities) {
                if(f==null||f.kind!=Metro3DFacilityKind.Seat||!f.Available||Time.time-f.AvailableSince<QueueReactionDelay||f.trainFacility!=inTrain||f.approach==null||!f.transform.IsChildOf(transform.parent)) continue;
                if(!inTrain) {
                    var local=transform.parent.InverseTransformPoint(f.approach.position);
                    if(local.x*Journey.TrainSide>=0||Mathf.Abs(f.approach.position.y-transform.position.y)>.4f)continue;
                }
                if(avoid.HasValue&&FlatDistance(avoid.Value,f.approach.position)<.49f)continue;
                float d=FlatDistance(transform.position,f.approach.position);
                if(d>maxDistance||!inTrain&&!crowd.SpaceFree(f.approach.position,this,.47f))continue;
                float s=maxDistance-d;
                if(s<=score||f.anchor==null)continue;
                if(entrance>=0) {
                    if(riding.NearestDoor(f.approach.position)/Metro3DTrain.DoorsPerCar!=entrance/Metro3DTrain.DoorsPerCar||
                        !crowd.TryCabinDestination(this,riding,entrance,f.approach.position,directOnly,out float traffic))continue;
                    s-=traffic*.72f;
                }
                s-=world.Traffic.OccupiedSeatNeighbors(f)*4.5f+crowd.LocalDensity(f.approach.position,transform.parent)*2.2f;
                if(RiderLook==Metro3DRiderLook.Child&&HasCompanion&&Companion.transform.parent==transform.parent)
                    s-=FlatDistance(f.approach.position,Companion.CurrentGoal)*1.4f;
                if(s>score) {best=f;score=s;}
            }
            if(best==null||!best.Claim(this)) return false;
            world.Traffic.ReleaseStanding(this);
            ReleaseSeat();seat=best;state=Activity.Walk;
            SetTarget(best.approach.position,!directOnly&&(entrance<0||!crowd.CanFollowWalkingSegment(this,best.approach.position)));
            return true;
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
            // A queued walking request can outlive arrival at its seat. Its
            // failure must not release a seat or change a transition's owner.
            if(state==Activity.SitDown||state==Activity.Sit||state==Activity.StandUp){pendingPath=false;return;}
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
        float retreatClearStepUntil;
        int retreatClearDoor;
        internal bool ClearingRetreatPath(Metro3DTrain train,int door=-1)
            =>train!=null&&train==nearbyDoorContact&&Time.time<retreatClearStepUntil&&
                (door<0||retreatClearDoor==door);

        Vector3 doorYieldGoal,doorYieldHeading,doorYieldStart,doorYieldApproach;
        float nextDoorYieldSample,nextDoorRelayAt;
        bool canRelayDoorYield,doorYieldBlocked;
        float doorYieldStartedAt;
        internal bool CanReceiveDoorYield=>!PortalCommitted&&seat==null&&!helperTaskActive&&!IsAlighting&&
            state!=Activity.Sit&&state!=Activity.StandUp&&state!=Activity.SitDown;
        internal bool DoorCourtesyStep => ClearingRetreatPath(nearbyDoorContact)&&
            nearbyDoorContact.DoorsOpen&&!nearbyDoorContact.Moving;

        internal bool TryGetDoorYieldGoal(Metro3DTrain train,int door,out Vector3 goal)
        {
            goal=default;
            if(!DoorCourtesyStep||!ClearingRetreatPath(train,door)||PortalCommitted||seat!=null)return false;
            goal=train.transform.TransformPoint(doorYieldGoal);
            return true;
        }

        internal bool RecentlyYieldedAtDoor(Metro3DTrain train,int door)
            =>nearbyDoorContact==train&&retreatClearDoor==door&&retreatClearStepUntil>0&&
                Time.time<retreatClearStepUntil+2.8f;

        bool DoorYieldVelocity(out Vector3 speed,out Vector3 facing)
        {
            speed=facing=Vector3.zero;
            if(!DoorCourtesyStep||PortalCommitted||seat!=null)return false;
            var train=nearbyDoorContact;
            if(doorYieldBlocked&&Time.time>=nextDoorYieldSample) {
                nextDoorYieldSample=Time.time+.14f;
                Vector3 beginning=train.transform.TransformPoint(doorYieldStart);
                if(crowd.TryCloseDoorYield(this,train,retreatClearDoor,train.transform.TransformPoint(doorYieldApproach),
                    out var adjusted,relay:true)&&(adjusted-beginning).sqrMagnitude<.44f*.44f) {
                    doorYieldGoal=train.transform.InverseTransformPoint(adjusted);
                    retreatClearStepUntil=Mathf.Min(doorYieldStartedAt+1.5f,Mathf.Max(retreatClearStepUntil,Time.time+.45f));
                }
            }
            Vector3 delta=train.transform.TransformPoint(doorYieldGoal)-transform.position;delta.y=0;
            float length=delta.magnitude;
            if(length>.026f)speed=delta*(Mathf.Min(.74f,Mathf.Sqrt(8f*(length-.022f)))/length);
            facing=train.transform.TransformDirection(doorYieldHeading);
            if(canRelayDoorYield&&Time.time>=nextDoorRelayAt) {
                Vector3 outward=-train.transform.right*train.side;
                Vector3 moved=transform.position-train.transform.TransformPoint(doorYieldStart);
                nextDoorRelayAt=Time.time+.10f;
                bool beganStep=Vector3.Dot(moved,outward)>.009f||
                    Vector3.Dot(Velocity,outward)>.10f;
                bool pressingBack=Vector3.Dot(speed,outward)>.12f;
                if((beganStep||pressingBack)&&
                    crowd.TryRelayDoorYield(this,train,retreatClearDoor,outward,contactOnly:!beganStep))
                    canRelayDoorYield=false;
            }
            return length>.026f||Velocity.sqrMagnitude>.07f*.07f;
        }

        internal bool TryYieldDoorLanding(Metro3DTrain train,int door,Vector3 approachingFrom,bool relay=false)
        {
            if(!CanReceiveDoorYield)return false;
            // A continuing clearance request keeps the doorway context alive,
            // including the short recovery interval after a completed step.
            NoteDoorContact(train);
            if(ClearingRetreatPath(train,door)||relay&&RecentlyYieldedAtDoor(train,door))return true;
            if(Time.time<nextDoorYieldSample)return true;
            nextDoorYieldSample=Time.time+.13f;
            if(!crowd.TryCloseDoorYield(this,train,door,approachingFrom,out var step,relay))return true;
            BeginDoorYield(train,door,step,relay,approachingFrom);
            return true;
        }

        internal bool TryFollowDoorYield(Metro3DTrain train,int door,Vector3 step)
        {
            if(inTrain||!CanReceiveDoorYield||RecentlyYieldedAtDoor(train,door)||
                !crowd.CanApproachDirect(this,step))return false;
            NoteDoorContact(train);
            Vector3 entry=train.DoorPoint(door,false);
            Vector3 approach=entry+train.transform.forward*Vector3.Dot(transform.position-entry,train.transform.forward);
            BeginDoorYield(train,door,step,true,approach);
            return true;
        }

        void BeginDoorYield(Metro3DTrain train,int door,Vector3 step,bool relay,Vector3 approachingFrom)
        {
            retreatClearDoor=door;
            doorYieldApproach=train.transform.InverseTransformPoint(approachingFrom);
            doorYieldGoal=train.transform.InverseTransformPoint(step);
            doorYieldStart=train.transform.InverseTransformPoint(transform.position);
            canRelayDoorYield=relay;doorYieldBlocked=false;doorYieldStartedAt=Time.time;
            nextDoorRelayAt=Time.time+Mathf.Lerp(.07f,.16f,Journey.Personality.ReactionDelay);
            Vector3 direction=Vector3.ProjectOnPlane(step-transform.position,Vector3.up).normalized;
            Vector3 facing=transform.forward;
            // A backward clearance step keeps the chest forward. For a lateral
            // step the body turns slightly into the step as the lead foot opens.
            float dot=Vector3.Dot(facing,direction);
            if(inTrain&&dot>-.48f)facing=Vector3.RotateTowards(facing,direction,Mathf.Lerp(.18f,.48f,Mathf.InverseLerp(-.48f,.75f,dot)),0);
            doorYieldHeading=train.transform.InverseTransformDirection(facing);
            float distance=Vector3.Distance(step,transform.position);
            retreatClearStepUntil=Time.time+(relay?
                Mathf.Clamp(.29f+distance/.53f,.62f,.98f):Mathf.Clamp(.19f+distance/.61f,.48f,.93f));
            world.Traffic.ReleaseStanding(this);
        }

        public void ClearDoorLanding()
            => ClearDoorLanding(transform.position);
        public void ClearDoorLanding(Vector3 point) => ClearDoorLanding(point,false);
        internal void ClearDoorLanding(Vector3 point,bool alightingPriority,int clearingDoor=-1,Vector3 crossingFrom=default)
        {
            if(state==Activity.Sit||state==Activity.SitDown||state==Activity.StandUp||state==Activity.Alight||cabinEntryDoor>=0) return;
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
            if(inTrain&&riding!=null&&clearingDoor>=0&&crowd.TryClearCabinApproach(this,riding,clearingDoor,crossingFrom,out var step)) {
                world.Traffic.ReleaseStanding(this);
                SetTarget(step,false);nextSteering=0;return;
            }
            SetTarget(inTrain?world.Traffic.TrainStandingPoint(this,riding,point):world.Traffic.PlatformWaitingPoint(this,point),true);
        }
        void BeginSit()
        {
            if(contacts?.PutPhoneAwayForSeat()==true)return;
            pendingPath=false;pathCount=pathIndex=0;
            transitionBaseAnimatorSpeed=animator.speed;animator.speed*=.9f/TransitionDuration;
            state=Activity.SitDown;transitionTime=0;transitionFrom=transform.localPosition;transitionTo=transform.parent.InverseTransformPoint(seat.anchor.position);
            motor.enabled=false;velocity=steering=Velocity=Vector3.zero;animator.CrossFadeInFixedTime("SitDown",.12f);MotionTrial?.CrossFadeLegacy("SitDown",.12f);Animate(0);
        }
        void BeginStand()
        {
            if(seatedPastime?.Release()==true)return;
            // Do not animate a disabled capsule into an occupied aisle and then
            // enable it inside another passenger. Reserve and clear the landing.
            if(seat==null)return;
            Vector3 landing=seat.approach.position;
            if(!crowd.PrepareStandSpace(this,landing)) {
                // Stand a little closer to the seat when a passenger blocks
                // its usual aisle mark. The capsule still needs real clearance.
                landing-=seat.anchor.forward*.19f;
                if(!crowd.PrepareStandSpace(this,landing))return;
            }
            pendingPath=false;pathIndex=pathCount=0;
            transitionBaseAnimatorSpeed=animator.speed;animator.speed*=.9f/TransitionDuration;
            state=Activity.StandUp;transitionTime=0;transitionFrom=transform.localPosition;transitionTo=transform.parent.InverseTransformPoint(landing);
            transitionAnimatorSpeed=animator.speed;
            animator.CrossFadeInFixedTime("StandUp",.12f);MotionTrial?.CrossFadeLegacy("StandUp",.12f);Animate(0);
        }
        void UpdateTransition()
        {
            float nextTime=transitionTime+Time.deltaTime,p=Mathf.Clamp01(nextTime/TransitionDuration);
            float rootTravel=state==Activity.StandUp?Mathf.InverseLerp(.20f,.92f,p):Mathf.InverseLerp(.12f,.88f,p);
            Vector3 next=Vector3.Lerp(transitionFrom,transitionTo,Mathf.SmoothStep(0,1,rootTravel));
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
            animator.speed=transitionBaseAnimatorSpeed;
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
        public void ResetForPool() {ClearCompanionBoarding();if(MotionTrial!=null)MotionTrial.Release();ResumeTransitionAnimation();Journey.CancelGate();CancelJourneyContinuation();ClearQueueContext();ReleaseSeat();crowd.Unregister(this);motor.enabled=false;pendingPath=false;pathCount=0;Velocity=velocity=steering=Vector3.zero;}
        public void RelocateToPlatform() {ReleaseSeat();motor.enabled=false;transform.position=world.stations[stationIndex].TransformPoint(world.PlatformWaitingPoint(index%world.settings.passengerCount));motor.enabled=true;WaitJourney();triedSeat=false;}
        static float FlatDistance(Vector3 a,Vector3 b) {a.y=b.y;return Vector3.Distance(a,b);}
        void OnDisable() {ClearCrowdRushTint();seatedPastime?.Clear();FaceTravel();if(crowd!=null) crowd.Unregister(this);}
        void OnDestroy() {ReleaseSeat();if(crowd!=null) crowd.Unregister(this);if(world!=null) world.passengers.Remove(this);}
    }
}

using System.Collections.Generic;
using UnityEngine;
using SubwayCarry.AI.V2;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // A moving stimulus and a perception-driven response share no cue/schedule input.
    public sealed class AnimationLabReactionReview : MonoBehaviour
    {
        public enum Stage {Off,Preparing,Walking,Reacting,Holding,Resuming,Done}
        AnimationLabPhoneCase owner;PassengerAiV2CrowdManager crowd;AnimationLabReactionBody body;AnimationLabReactionStimulus stimulus;
        readonly List<PassengerAiV2Agent> nearby=new List<PassengerAiV2Agent>(8);readonly RaycastHit[] hits=new RaycastHit[32];
        Vector2 start,flow,goal,released;float nextSense,resumeAt;int scenario;float cuePhase=-1;bool resumed;
        public bool DisableAnimation,ShowObstacle=true;
        public Stage State {get;private set;}
        public int PendingMode=-1;
        public int Reactions {get;private set;}
        public float DetectedAt {get;private set;}=-1;
        public float AppearedAt=>stimulus!=null?stimulus.AppearedAt:-1;
        public float ClosestTime {get;private set;}
        public float ClosestOffset {get;private set;}
        public Vector2 TravelFlow=>flow;
        public AnimationLabPhoneMotor Other=>stimulus!=null?stimulus.Peer:null;
        public AnimationLabReactionBody Body=>body;
        public bool CanLeave=>body==null||!body.ControlsMovement;
        public string Status=>State==Stage.Preparing?"보행 중 폰 정리":State==Stage.Walking?"보행 · 이동 상대 관측":State==Stage.Reacting?body.Entry.authored.clipName:State==Stage.Holding?"우회 공간 없음 · 안전 제동":State==Stage.Done?"원래 목적지 보행 복귀":"목적지 보행 회복";
        public void Initialize(AnimationLabPhoneCase value){owner=value;crowd=value.GetComponent<PassengerAiV2CrowdManager>();}
        public void Begin(int kind,float phase=-1)
        {
            if(!CanLeave)return;End();scenario=Mathf.Clamp(kind-8,0,6);cuePhase=phase;goal=owner.Agent.MovementTarget;start=owner.Passenger.Position;if(owner.Agent.HasReachedTarget&&UnityEngine.Vector2.Distance(goal,start)<.2f)goal=UnityEngine.Vector2.Distance(start,owner.Data.destination)>UnityEngine.Vector2.Distance(start,owner.Data.start)?owner.Data.destination:owner.Data.start;flow=owner.Passenger.ActualVelocity.sqrMagnitude>.04f?new Vector2(owner.Passenger.ActualVelocity.x,owner.Passenger.ActualVelocity.z).normalized:(goal-start).normalized;if(flow.sqrMagnitude<.1f)flow=Vector2.up;
            owner.Passenger.PhoneLayer.RequestedMode=AnimationLabPhoneMode.Stored;owner.Agent.SetHoldPosition(false);owner.Agent.SetMovementTarget(goal);owner.Agent.SetMovementSpeedScale(1);State=Stage.Preparing;DetectedAt=-1;ClosestTime=ClosestOffset=0;Reactions=0;PendingMode=-1;resumed=false;
        }
        public void Tick()
        {
            if(State==Stage.Off)return;owner.Passenger.Goal=owner.Agent.MovementTarget;
            if(State==Stage.Preparing){if(owner.Passenger.PhoneLayer.IsTransitioning||owner.Passenger.PhoneLayer.PhoneInHand)return;start=owner.Passenger.Position;stimulus=gameObject.AddComponent<AnimationLabReactionStimulus>();stimulus.Initialize(owner,scenario,start,flow,cuePhase,ShowObstacle);State=Stage.Walking;}
            if(Time.time<nextSense)return;nextSense=Time.time+.055f;bool danger=Observe(out var opponent,out var kind);
            if(State==Stage.Walking&&danger&&!DisableAnimation)
            {
                if(body==null){body=owner.Passenger.gameObject.AddComponent<AnimationLabReactionBody>();body.Initialize(owner);}AnimationLabPhoneMotion.ReactionClip clip;
                if(kind==AnimationLabReactionKind.FrontLeft||kind==AnimationLabReactionKind.FrontRight){var l=body.Choose(AnimationLabReactionKind.FrontLeft,out float lc);var r=body.Choose(AnimationLabReactionKind.FrontRight,out float rc);bool lf=l!=null&&Fits(l,opponent),rf=r!=null&&Fits(r,opponent);clip=lf&&(!rf||lc<=rc+.001f)?l:rf?r:null;if(clip!=null)kind=clip.kind;}
                else{clip=body.Choose(kind);if(clip!=null&&!Fits(clip,opponent))clip=null;}
                DetectedAt=Time.time;if(clip==null){owner.Agent.SetHoldPosition(true);State=Stage.Holding;return;}
                body.Begin(clip);var side=new Vector2(flow.y,-flow.x);float sign=kind==AnimationLabReactionKind.FrontLeft||kind==AnimationLabReactionKind.PassRight?-1:1;owner.Agent.SetMovementTarget(owner.Passenger.Position+flow*4+side*(sign*.85f));owner.Passenger.Goal=owner.Agent.MovementTarget;State=Stage.Reacting;Reactions++;
            }
            else if(State==Stage.Reacting&&body.Complete){body.Release();Resume();}
            else if(State==Stage.Holding&&!danger){owner.Agent.SetHoldPosition(false);Resume();}
            if(DisableAnimation&&State==Stage.Walking&&stimulus!=null&&stimulus.AppearedAt>=0&&Time.time-stimulus.AppearedAt>4)Resume();
            if(State==Stage.Resuming&&resumed&&Time.time>=resumeAt&&Vector2.Distance(released,owner.Passenger.Position)>.40f&&(body==null||!body.Active)){State=Stage.Done;if(PendingMode>=0){int mode=PendingMode;PendingMode=-1;owner.SetReviewMode(mode);}}
        }
        void Resume(){owner.Agent.SetMovementTarget(goal);owner.Passenger.Goal=goal;owner.Agent.SetMovementSpeedScale(1);released=owner.Passenger.Position;resumeAt=Time.time+.8f;resumed=true;State=Stage.Resuming;}
        bool Observe(out PassengerAiV2Agent selected,out AnimationLabReactionKind kind)
        {
            selected=null;kind=AnimationLabReactionKind.FrontLeft;float best=2;var position=owner.Passenger.Position;var self=new Vector2(owner.Passenger.ActualVelocity.x,owner.Passenger.ActualVelocity.z);if(State==Stage.Walking&&self.sqrMagnitude>.04f)flow=self.normalized;var right=new Vector2(flow.y,-flow.x);crowd.QueryNearby(position,3.2f,owner.Agent,nearby);
            foreach(var other in nearby){var relative=other.Position-position;var velocity=other.Velocity-self;float vv=velocity.sqrMagnitude;if(State==Stage.Holding&&Vector2.Dot(relative,flow)>-.1f&&Mathf.Abs(Vector2.Dot(relative,right))<owner.Agent.Radius+other.Radius+.18f&&(relative.magnitude<owner.Agent.Radius+other.Radius+.25f||(Vector2.Dot(relative,flow)<3.0f&&Vector2.Dot(other.Velocity,flow)<-.05f))){selected=other;continue;}if(vv<.01f||Vector2.Dot(relative,velocity)>=-.02f||Vector2.Dot(relative,flow)<-.1f)continue;float t=-Vector2.Dot(relative,velocity)/vv;if(t<0||t>1.55f||t>best)continue;var closest=relative+velocity*t;float safety=owner.Agent.Radius+other.Radius+.18f;if(closest.magnitude>safety)continue;var eye=owner.Passenger.transform.position+Vector3.up;var ray=new Vector3(relative.x,0,relative.y);bool occluded=false;int visibleHits=Physics.RaycastNonAlloc(eye,ray.normalized,hits,Mathf.Max(0,ray.magnitude-other.Radius*1.4f),~0,QueryTriggerInteraction.Ignore);for(int h=0;h<visibleHits;h++)if(hits[h].collider!=null&&!hits[h].collider.transform.IsChildOf(owner.Passenger.transform)&&hits[h].collider.bounds.max.y>eye.y*.7f){occluded=true;break;}if(occluded)continue;selected=other;best=t;ClosestTime=t;ClosestOffset=Vector2.Dot(closest,right);float lateral=Vector2.Dot(relative,right);bool headOn=Mathf.Abs(ClosestOffset)<.16f&&Mathf.Abs(lateral)<.5f&&Vector2.Dot(other.Velocity,flow)<-.2f;kind=headOn?AnimationLabReactionKind.FrontLeft:(Mathf.Abs(ClosestOffset)>.06f?ClosestOffset:lateral)>0?AnimationLabReactionKind.PassRight:AnimationLabReactionKind.PassLeft;}
            return selected!=null;
        }
        bool Fits(AnimationLabPhoneMotion.ReactionClip clip,PassengerAiV2Agent opponent)
        {
            var basis=Quaternion.LookRotation(new Vector3(flow.x,0,flow.y));var origin=owner.Passenger.transform.position;var previous=origin;float rate=Mathf.Clamp(owner.Passenger.ActualVelocity.magnitude/clip.referenceSpeed,.7f,1.2f);
            for(int i=3;i<clip.root.Length;i+=3){var target=origin+basis*(clip.root[i]-clip.root[0]);var delta=target-previous;if(!owner.Data.bounds.Contains(new Vector2(target.x,target.z))||!owner.Navigation.IsClear(new Vector2(previous.x,previous.z),new Vector2(target.x,target.z)))return false;if(delta.sqrMagnitude>.000001f){var offset=previous-origin;int n=Physics.CapsuleCastNonAlloc(owner.Passenger.CapsuleBottom+offset,owner.Passenger.CapsuleTop+offset,owner.Passenger.BodyRadius,delta.normalized,hits,delta.magnitude+.025f,~0,QueryTriggerInteraction.Ignore);for(int h=0;h<n;h++)if(hits[h].collider!=null&&!hits[h].collider.transform.IsChildOf(owner.Passenger.transform)&&hits[h].normal.y<.7f&&hits[h].collider.bounds.max.y>origin.y+.035f){if(opponent!=null&&Other!=null&&hits[h].collider.transform.IsChildOf(Other.transform))continue;return false;}}
                foreach(var other in nearby){float time=i/(clip.authored.sampleRate*rate);var future=other.Position+other.Velocity*time;if(Vector2.Distance(new Vector2(target.x,target.z),future)<owner.Passenger.BodyRadius+other.Radius+.035f)return false;}previous=target;}return true;
        }
        public void End(){if(body!=null)body.Cancel();if(State!=Stage.Off&&owner!=null&&owner.Agent!=null){owner.Agent.SetHoldPosition(false);owner.Agent.SetMovementTarget(goal);}State=Stage.Off;if(stimulus!=null){stimulus.Cleanup();Destroy(stimulus);stimulus=null;}}
        void OnDestroy(){End();}
    }
    [DefaultExecutionOrder(-100)] public sealed class AnimationLabReactionStimulus : MonoBehaviour
    {
        AnimationLabPhoneCase owner;PassengerAiV2Agent agent;Transform proxy;GameObject leftWall,rightWall;Vector2 start,flow,velocity,peerGoal;float phase;int scenario;bool allowed;
        public AnimationLabPhoneMotor Peer {get;private set;}
        public float AppearedAt {get;private set;}=-1;
        public void Initialize(AnimationLabPhoneCase value,int scene,Vector2 origin,Vector2 direction,float cue,bool show){owner=value;scenario=scene;start=origin;flow=direction;phase=cue;allowed=show;}
        void FixedUpdate()
        {
            if(owner==null)return;if(Peer==null){float gait=Mathf.Repeat(owner.Passenger.WalkTime/owner.Data.walk.length,1);if(!allowed||Vector2.Dot(owner.Passenger.Position-start,flow)<1||(phase>=0&&Mathf.Abs(Mathf.DeltaAngle(gait*360,phase*360))>12))return;var right=new Vector2(flow.y,-flow.x);float lateral=scenario==2?.28f:scenario==3?-.28f:scenario==4?1.05f:0;var p=owner.Passenger.Position+flow*2.35f+right*lateral;var direction=scenario==6?flow:-flow;var end=p+direction*7;end=new Vector2(Mathf.Clamp(end.x,owner.Data.bounds.xMin+.5f,owner.Data.bounds.xMax-.5f),Mathf.Clamp(end.y,owner.Data.bounds.yMin+.5f,owner.Data.bounds.yMax-.5f));peerGoal=end;Peer=owner.CreatePassenger("Moving encounter pedestrian",p,end,out agent);proxy=new GameObject("Observed planar peer position").transform;proxy.SetParent(transform);proxy.position=new Vector3(p.x,p.y,0);agent.FollowExternalMotion(proxy,.24f);velocity=direction*(scenario==6?1.1f:.68f);Peer.SeedWalkingVelocity(velocity);Peer.transform.rotation=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));AppearedAt=Time.time;
                if(scenario==1||scenario==5)leftWall=Wall(-.82f);if(scenario==5)rightWall=Wall(.82f);Physics.SyncTransforms();}
            var next=Vector2.Distance(Peer.Position,peerGoal)>.12f?Peer.Position+velocity*Time.fixedDeltaTime:Peer.Position;Peer.Move(next);var position=Peer.Position;proxy.position=new Vector3(position.x,position.y,0);
        }
        GameObject Wall(float x){var right=new Vector2(flow.y,-flow.x);var p=owner.Passenger.Position+flow*.9f+right*x;var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Blocked avoidance space";go.transform.SetParent(transform);go.transform.SetPositionAndRotation(new Vector3(p.x,.9f,p.y),Quaternion.LookRotation(new Vector3(flow.x,0,flow.y)));go.transform.localScale=new Vector3(.28f,1.8f,3.0f);return go;}
        public void Cleanup(){if(agent!=null){var manager=owner!=null?owner.GetComponent<PassengerAiV2CrowdManager>():null;if(manager!=null)manager.Unregister(agent);Destroy(agent.gameObject);}if(Peer!=null)Destroy(Peer.gameObject);if(proxy!=null)Destroy(proxy.gameObject);if(leftWall!=null)Destroy(leftWall);if(rightWall!=null)Destroy(rightWall);Peer=null;}
        void OnDestroy(){Cleanup();}
    }
}

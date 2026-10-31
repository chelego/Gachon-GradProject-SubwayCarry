using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using SubwayCarry.AI.V2;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // One review case, not an alternate journey planner. Agent targets survive every phone/attention transition.
    public sealed class AnimationLabPhoneCase : MonoBehaviour
    {
        public enum Attention {Phone,CheckAhead,Arrived}
        public static AnimationLabPhoneCase Active {get;private set;}
        public AnimationLabPhoneMotion Data {get;private set;}
        public AnimationLabPhoneMotor Passenger {get;private set;}
        public AnimationLabPhoneMotor Peer {get;private set;}
        public PassengerAiV2Agent Agent {get;private set;}
        public PassengerAiV2Agent PeerAgent {get;private set;}
        public Attention State {get;private set;}
        public int AttentionChanges {get;private set;}
        public bool SawPhone,SawRisk,ResumedPhone;
        public float MinimumSeparation=float.PositiveInfinity;
        public PassengerAiV23DNavigation Navigation {get;private set;}
        public AnimationLabSqueezeReview Squeeze {get;private set;}
        public AnimationLabReactionReview Reaction {get;private set;}
        public AnimationLabReviewMenu Menu {get;private set;}
        readonly List<PassengerAiV2Agent> nearby=new List<PassengerAiV2Agent>(8);
        readonly List<GameObject> parkedActors=new List<GameObject>();
        AnimationLabPhoneLayer layer;PassengerAiV2CrowdManager crowd;float nextSense,clearAfter,nextText;string caption="";
        public string PassengerId {get;private set;}
        public int ReviewMode {get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string scene=SceneManager.GetActiveScene().name;if(scene!="Normal"&&scene!="Situations")return;
            var data=Resources.Load<AnimationLabPhoneMotion>(AnimationLabPhoneMotion.ResourceName);
            if(data==null||!data.Valid||!(scene=="Normal"?data.normalEnabled:data.situationsEnabled))return;
            var go=new GameObject("Passenger phone review · V2 navigation");go.AddComponent<AnimationLabPhoneCase>().Initialize(data,scene=="Normal");
        }
        public void Initialize(AnimationLabPhoneMotion config,bool clipReview)
        {
            Active=this;Data=config;PassengerId=System.Guid.NewGuid().ToString("N");
            // Park the old comparison only in this Play session. No editor scene/prefab is saved or changed.
            foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var behavior in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(behavior==null||behavior==this)continue;string type=behavior.GetType().FullName;
                if(type=="SubwayCarry.Gameplay3D.AnimationLab.AnimationLabCharacter"&&behavior.gameObject.activeSelf){parkedActors.Add(behavior.gameObject);behavior.gameObject.SetActive(false);}
                else if(type=="SubwayCarry.Gameplay3D.AnimationLab.AnimationLabCamera"||type=="SubwayCarry.Gameplay3D.AnimationLab.AnimationLabWalkReview")behavior.enabled=false;
            }
            foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var label in root.GetComponentsInChildren<TextMesh>())label.GetComponent<Renderer>().enabled=false;
            crowd=gameObject.AddComponent<PassengerAiV2CrowdManager>();Navigation=gameObject.AddComponent<PassengerAiV23DNavigation>();
            var solids=new List<Rect>();foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var collider in root.GetComponentsInChildren<Collider>())
            {var b=collider.bounds;if(collider.enabled&&!collider.isTrigger&&b.size.y>.5f&&!(collider is CharacterController)&&config.bounds.Overlaps(new Rect(b.min.x,b.min.z,b.size.x,b.size.z)))solids.Add(new Rect(b.min.x,b.min.z,b.size.x,b.size.z));}
            if(!clipReview&&config.pillar.width>.01f&&config.pillar.height>.01f){var pillar=GameObject.CreatePrimitive(PrimitiveType.Cube);pillar.name="Route obstacle";pillar.transform.SetParent(transform);pillar.transform.position=new Vector3(config.pillar.center.x,.75f,config.pillar.center.y);pillar.transform.localScale=new Vector3(config.pillar.width,1.5f,config.pillar.height);solids.Add(config.pillar);}
            Navigation.Configure(config.bounds,solids.ToArray());Passenger=CreatePassenger("Purposeful passenger",config.start,config.destination,out var agent);Agent=agent;
            layer=Passenger.gameObject.AddComponent<AnimationLabPhoneLayer>();layer.Initialize(config);Passenger.PhoneLayer=layer;
            PassengerAiV2Agent peerAgent=null;if(!clipReview)Peer=CreatePassenger("Other pedestrian",config.peerStart,config.peerDestination,out peerAgent);else Peer=null;
            if(Peer!=null)PeerAgent=Peer.GetComponentInChildren<AnimationLabPhoneMotor>()!=null?peerAgent:null;
            var camera=Camera.main;if(camera!=null){var middle=(config.start+config.destination)*.5f;camera.transform.SetPositionAndRotation(new Vector3(middle.x+8.7f,3.2f,middle.y-3.2f),Quaternion.LookRotation(new Vector3(middle.x,1,middle.y)-new Vector3(middle.x+8.7f,3.2f,middle.y-3.2f)));camera.fieldOfView=57;}
            if(!Application.isBatchMode&&camera!=null){var orbit=camera.GetComponent<AnimationLabCamera>();if(orbit!=null){orbit.reviewTarget=Passenger.transform;orbit.inspectOnly=true;orbit.yaw=155;orbit.pitch=12;orbit.distance=2.7f;orbit.lookHeight=1.14f;orbit.enabled=true;}}
            Squeeze=gameObject.AddComponent<AnimationLabSqueezeReview>();Squeeze.Initialize(this);Reaction=gameObject.AddComponent<AnimationLabReactionReview>();Reaction.Initialize(this);Menu=gameObject.AddComponent<AnimationLabReviewMenu>();Menu.Initialize(this);Physics.SyncTransforms();Sense();
        }
        internal AnimationLabPhoneMotor CreatePassenger(string label,Vector2 start,Vector2 goal,out PassengerAiV2Agent agent)
        {
            var visual=Instantiate(Data.rig,transform);visual.name=label;visual.layer=8;
            var motor=visual.AddComponent<AnimationLabPhoneMotor>();motor.Initialize(Data,start,goal);
            var decision=new GameObject(label+" · decision");decision.transform.SetParent(transform);agent=decision.AddComponent<PassengerAiV2Agent>();
            var personality=PassengerAiV2RuntimePersonality.CreateFallback(0);personality.WalkingSpeed=Data.referenceSpeed;personality.PersonalSpace=.22f;personality.ReactionDelay=.12f;
            agent.Initialize(crowd,start,goal,personality,.25f,Data.bounds.yMin,Data.bounds.yMax,null,Color.white);agent.SetAutoLoop(false);agent.UsePlanarMotor(motor);agent.SetMovementBounds(Data.bounds);agent.ConfigureArrivalTolerance(.08f);Navigation.Bind(agent);return motor;
        }
        void Update()
        {
            if(Agent==null)return;Passenger.Goal=Agent.MovementTarget;if(!Application.isBatchMode&&Keyboard.current!=null){var k=Keyboard.current;if(k.digit1Key.wasPressedThisFrame)SetReviewMode(0);if(k.digit2Key.wasPressedThisFrame)SetReviewMode(1);if(k.digit3Key.wasPressedThisFrame)SetReviewMode(2);if(k.digit4Key.wasPressedThisFrame)SetReviewMode(3);if(k.digit5Key.wasPressedThisFrame)SetReviewMode(4);if(k.digit6Key.wasPressedThisFrame)SetReviewMode(5);}if(ReviewMode>=14){Reaction.Tick();return;}if(ReviewMode==5){Squeeze.Tick();return;}if(ReviewMode>0&&Agent.HasReachedTarget)Agent.SetMovementTarget(Vector2.Distance(Passenger.Position,Data.start)>Vector2.Distance(Passenger.Position,Data.destination)?Data.start:Data.destination);if(Time.time>=nextSense){nextSense=Time.time+.16f;Sense();}
            if(Peer!=null)MinimumSeparation=Mathf.Min(MinimumSeparation,Vector2.Distance(Passenger.Position,Peer.Position));
            if(layer.Weight>.85f){if(SawRisk)ResumedPhone=true;else SawPhone=true;}
            if(Time.time>=nextText){nextText=Time.time+.2f;caption=dataCaption();}
        }
        void Sense()
        {
            var forward=Passenger.ActualVelocity.sqrMagnitude>.04f?new Vector2(Passenger.ActualVelocity.x,Passenger.ActualVelocity.z).normalized:Agent.TravelDirection;
            bool risk=false;float urgency=0;Vector3 look=Passenger.transform.position+new Vector3(forward.x,0,forward.y)*5+Vector3.up*1.55f;
            crowd.QueryNearby(Agent.Position,4,Agent,nearby);
            foreach(var other in nearby)
            {
                var relative=other.Position-Agent.Position;var velocity=other.Velocity-Agent.Velocity;float vv=velocity.sqrMagnitude;
                float t=vv>.0001f?-Vector2.Dot(relative,velocity)/vv:0;float clearance=Agent.Radius+other.Radius+.38f;
                // Social Navigation may already have prevented a collision. Its committed avoidance is still a reason to look up.
                if(Agent.AvoidanceSide!=0&&Vector2.Dot(relative,forward)>-.3f&&relative.sqrMagnitude<3.6f*3.6f){risk=true;urgency=Mathf.Max(urgency,.35f);look=new Vector3(other.Position.x,1.55f,other.Position.y);}
                if(Vector2.Dot(relative,forward)>-.2f&&t>=0&&t<2.6f&&(relative+velocity*t).sqrMagnitude<clearance*clearance)
                {risk=true;urgency=Mathf.Max(urgency,1-t/2.6f);look=new Vector3(other.Position.x,1.55f,other.Position.y);}
            }
            float turn=Passenger.RequestedVelocity.sqrMagnitude>.04f?Vector3.Angle(Passenger.transform.forward,Passenger.RequestedVelocity):0;
            bool wall=Physics.SphereCast(Passenger.transform.position+Vector3.up,.23f,new Vector3(forward.x,0,forward.y),out _,Mathf.Clamp(Agent.Velocity.magnitude*1.4f,.6f,1.8f),1,QueryTriggerInteraction.Ignore);
            risk|=turn>28||wall;
            if(risk){clearAfter=Time.time+.85f;SawRisk|=SawPhone;}
            var state=Agent.DistanceToTarget<.13f&&Agent.Velocity.magnitude<.1f?Attention.Arrived:risk||Time.time<clearAfter?Attention.CheckAhead:Attention.Phone;
            if(state!=State){State=state;AttentionChanges++;}layer.WantsPhone=State==Attention.Phone&&Agent.DistanceToTarget>.7f;layer.LookTarget=look;layer.RequestedMode=ReviewMode==1?AnimationLabPhoneMode.Stored:ReviewMode==2?AnimationLabPhoneMode.Read:ReviewMode==3?AnimationLabPhoneMode.Landscape:ReviewMode==4?AnimationLabPhoneMode.Carry:State==Attention.Arrived?AnimationLabPhoneMode.Stored:State==Attention.CheckAhead?AnimationLabPhoneMode.Carry:AnimationLabPhoneMode.Read;
            Agent.SetMovementSpeedScale(State==Attention.CheckAhead&&urgency>.3f?.72f:State==Attention.Phone?.93f:1);
        }
        public void SetReviewMode(int mode)
        {
            mode=Mathf.Clamp(mode,0,17);if(mode>=6&&mode<14)return;if(mode>=14&&gameObject.scene.name!="Situations")return;if(ReviewMode>=14&&!Reaction.CanLeave){Reaction.PendingMode=mode;return;}if(ReviewMode>=14)Reaction.End();if(ReviewMode==5&&mode!=5&&!Squeeze.CanLeave){Squeeze.PendingMode=mode;return;}if(ReviewMode==5&&mode!=5)Squeeze.End();ReviewMode=mode;Agent.SetAutoLoop(false);
            if(ReviewMode==0)Agent.SetMovementTarget(Data.destination);
            if(PeerAgent!=null){bool show=ReviewMode==0;if(show){Peer.gameObject.SetActive(true);PeerAgent.gameObject.SetActive(true);crowd.Register(PeerAgent);}else{crowd.Unregister(PeerAgent);PeerAgent.gameObject.SetActive(false);Peer.gameObject.SetActive(false);}}
            if(ReviewMode>=14)Reaction.Begin(ReviewMode-6);else if(ReviewMode==5)Squeeze.Begin();else Sense();
        }
        public string Caption=>ReviewMode>=14?Reaction.Status:ReviewMode==5?Squeeze.Status:caption;
        string dataCaption()=>layer.HasActionSet?layer.CurrentActionName+(layer.AttachmentFault.Length>0?" · "+layer.AttachmentFault:""):Data.actions.Length>0?"동작 데이터 누락 · 기존 폰 자세":Data.displayName;
        void OnGUI(){Menu?.Draw();}
        void OnDestroy(){if(Active==this)Active=null;foreach(var actor in parkedActors)if(actor!=null)actor.SetActive(true);}
    }
}

using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Unity.Profiling;
using SubwayCarry.AI.V2;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Opt-in adapter: V2 still owns the goal/social velocity; this CC is the only root writer.
    public sealed class AnimationLabPhoneMotor : MonoBehaviour,IPassengerAiV2PlanarMotor
    {
        static readonly ProfilerMarker MotorMarker=new ProfilerMarker("SubwayCarry.PhoneCase.Motor");
        static readonly ProfilerMarker PoseMarker=new ProfilerMarker("SubwayCarry.PhoneCase.Pose");
        CharacterController controller;Animator animator;AnimationLabPhoneMotion data;
        PlayableGraph graph;AnimationMixerPlayable mixer;AnimationClipPlayable idle,walk;
        float yawRate,fall,walkWeight,resumeBlend;bool pivot;
        public AnimationLabReactionBody Reaction {get;set;}
        public Vector3 CapsuleBottom=>transform.position+controller.center-Vector3.up*(controller.height*.5f-controller.radius);
        public Vector3 CapsuleTop=>transform.position+controller.center+Vector3.up*(controller.height*.5f-controller.radius);
        public AnimationLabSqueezeBody SqueezeBody {get;set;}
        public float BodyRadius=>controller!=null?controller.radius:.24f;
        public Vector2 Goal {get;set;}
        public Vector2 Position=>new Vector2(transform.position.x,transform.position.z);
        public Vector3 ActualVelocity {get;private set;}
        public Vector3 RequestedVelocity {get;private set;}
        public AnimationLabPhoneLayer PhoneLayer {get;set;}
        public float WalkWeight=>walkWeight;
        public float WalkTime=>walk.IsValid()?(float)walk.GetTime():0;
        public Transform Model=>animator!=null?animator.transform:null;
        public void Initialize(AnimationLabPhoneMotion motion,Vector2 start,Vector2 goal)
        {
            data=motion;Goal=goal;animator=transform.Find(data.animatorPath).GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            controller=gameObject.AddComponent<CharacterController>();controller.height=1.78f;controller.radius=.24f;controller.center=Vector3.up*.89f;controller.skinWidth=.015f;controller.minMoveDistance=0;controller.stepOffset=.2f;
            graph=PlayableGraph.Create("Phone trial · shared Generic Idle/Walk");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);mixer=AnimationMixerPlayable.Create(graph,2);
            idle=AnimationClipPlayable.Create(graph,data.idle);walk=AnimationClipPlayable.Create(graph,data.walk);idle.SetApplyFootIK(false);walk.SetApplyFootIK(false);idle.SetApplyPlayableIK(false);walk.SetApplyPlayableIK(false);
            graph.Connect(idle,0,mixer,0);graph.Connect(walk,0,mixer,1);mixer.SetInputWeight(0,1);AnimationPlayableOutput.Create(graph,"Single base pose owner",animator).SetSourcePlayable(mixer);graph.Play();graph.Evaluate(0);
            // One spawn placement, measured from the actual mesh. Never re-scale or translate the pelvis each frame.
            float bottom=float.PositiveInfinity;var mesh=new Mesh();foreach(var skin in GetComponentsInChildren<SkinnedMeshRenderer>()){skin.BakeMesh(mesh);foreach(var v in mesh.vertices)bottom=Mathf.Min(bottom,skin.transform.TransformPoint(v).y);}Destroy(mesh);
            float localBottom=float.IsFinite(bottom)?bottom-transform.position.y:0;controller.center=Vector3.up*(controller.height*.5f+localBottom+controller.skinWidth);float placement=-localBottom+.003f;controller.enabled=false;transform.position=new Vector3(start.x,placement,start.y);controller.enabled=true;
            var direction=goal-start;if(direction.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));
        }
        public void Teleport(Vector2 position)
        {controller.enabled=false;transform.position=new Vector3(position.x,transform.position.y,position.y);controller.enabled=true;ActualVelocity=RequestedVelocity=Vector3.zero;yawRate=fall=0;pivot=false;}
        public Vector2 Move(Vector2 position)
        {
            using var marker=MotorMarker.Auto();float dt=Time.fixedDeltaTime;
            var wanted=new Vector3(position.x-Position.x,0,position.y-Position.y)/dt;
            wanted=Vector3.ClampMagnitude(wanted,Metro3DWalkMotion.ArrivalSpeed(Vector2.Distance(Position,Goal)));RequestedVelocity=wanted;
            Vector3 command,facing;
            if(Reaction!=null&&Reaction.ControlsMovement)command=Reaction.Constrain(wanted,dt,out facing);
            else if(SqueezeBody!=null&&SqueezeBody.Active)command=SqueezeBody.Constrain(wanted,dt,out facing);
            else command=Metro3DWalkMotion.Step(transform.forward,ActualVelocity,wanted,125,dt,ref yawRate,ref pivot,out facing);
            // Keep the shared forward arc. CharacterController performs final wall/person collision resolution.
            transform.rotation=Quaternion.LookRotation(facing);fall=controller.isGrounded?-2:Mathf.Max(-12,fall-9.81f*dt);
            var before=transform.position;controller.Move((command+Vector3.up*fall)*dt);ActualVelocity=Vector3.ProjectOnPlane((transform.position-before)/dt,Vector3.up);if(Reaction!=null&&Reaction.ControlsMovement)Reaction.AfterMove(ActualVelocity,dt);return Position;
        }
        void LateUpdate()
        {
            if(!graph.IsValid())return;using var marker=PoseMarker.Auto();Reaction?.RestoreBase();SqueezeBody?.RestoreBase();PhoneLayer?.RestoreBase();
            float speed=ActualVelocity.magnitude;float desired=speed<.025f?0:Mathf.Clamp01(speed/(data.referenceSpeed*.8f));
            if(resumeBlend>0){resumeBlend=Mathf.Max(0,resumeBlend-Time.deltaTime);desired=1;}
            walkWeight=Mathf.MoveTowards(walkWeight,desired,Time.deltaTime*(desired<walkWeight?7:4));
            float rate=walkWeight>.001f?Mathf.Clamp(speed/(data.referenceSpeed*walkWeight),0,1.3f):0;walk.SetSpeed(rate);idle.SetSpeed(1);
            mixer.SetInputWeight(0,1-walkWeight);mixer.SetInputWeight(1,walkWeight);graph.Evaluate(Time.deltaTime);if(PhoneLayer!=null){PhoneLayer.LocomotionTime=WalkTime;PhoneLayer.Apply(Time.deltaTime);}SqueezeBody?.Apply(Time.deltaTime);Reaction?.Apply(Time.deltaTime);
        }
        public void SeedWalkingVelocity(Vector2 velocity){ActualVelocity=RequestedVelocity=new Vector3(velocity.x,0,velocity.y);walkWeight=Mathf.Clamp01(velocity.magnitude/(data.referenceSpeed*.8f));}
        public void SyncWalkPhase(float phase){if(walk.IsValid()){walk.SetTime(Mathf.Repeat(phase,1)*data.walk.length);walkWeight=1;resumeBlend=.18f;}}
        public void SetIdlePhase(float seconds){if(idle.IsValid())idle.SetTime(seconds);}
        public void SetRadius(float radius){controller.radius=Mathf.Clamp(radius,.16f,.24f);}
        // Pool/menu hiding must not destroy a graph which still belongs to this passenger.
        void OnDisable(){PhoneLayer?.RestoreBase();}
        void OnDestroy(){if(graph.IsValid())graph.Destroy();}
    }
}

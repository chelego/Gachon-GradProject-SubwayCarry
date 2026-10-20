using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using Unity.InferenceEngine;
using SubwayCarry.Gameplay3D;

namespace SubwayCarry.AI.HelpNPC
{
    // High-level learned intent only. CharacterController, A*, reservations and
    // full-journey planning remain the existing gameplay implementation.
    public sealed class Metro3DHelperAgent : Agent
    {
        Metro3DPassenger passenger;
        Metro3DCrowd crowd;
        Renderer[] renderers;
        MaterialPropertyBlock tint;
        static ModelAsset model;
        static bool modelChecked;
        float nextDecision,committedUntil,helpStarted,cooldownUntil;
        HelperAction current;
        Vector3 right,forward,safePoint;
        HelperSituation snapshot;
        bool painted,wasHelping;
        public HelperAction CurrentAction=>current;
        public int InferenceCount {get;private set;}
        public bool IsHelping=>painted&&wasHelping;
        public HelperSituation LastSituation=>snapshot;
        public static void Configure(Metro3DPassenger actor)
        {
            var old=actor.GetComponent<Metro3DHelperAgent>();
            if(!modelChecked){model=Resources.Load<ModelAsset>("HelpfulCitizen");modelChecked=true;}
            bool selected=actor.index%12==0&&model!=null;
            if(!selected){if(old!=null){old.Paint(false);old.enabled=false;}return;}
            if(old==null){
                var bp=actor.GetComponent<BehaviorParameters>();if(bp==null)bp=actor.gameObject.AddComponent<BehaviorParameters>();
                bp.BehaviorName="HelpfulCitizen";bp.BrainParameters.VectorObservationSize=HelperSituation.ObservationCount;
                bp.BrainParameters.NumStackedVectorObservations=1;bp.BrainParameters.ActionSpec=ActionSpec.MakeDiscrete(4);
                bp.Model=model;bp.BehaviorType=BehaviorType.InferenceOnly;bp.InferenceDevice=InferenceDevice.Burst;bp.DeterministicInference=true;
                old=actor.gameObject.AddComponent<Metro3DHelperAgent>();
            }
            old.passenger=actor;old.crowd=actor.world.GetComponent<Metro3DCrowd>();
            old.renderers=actor.GetComponentsInChildren<Renderer>();old.current=HelperAction.ContinueJourney;old.painted=false;
            old.nextDecision=Time.time+(actor.index%7)*.035f;old.committedUntil=old.helpStarted=old.cooldownUntil=0;old.enabled=true;old.Paint(false);
        }
        bool Available(out Metro3DPlayer player)
        {
            player=passenger!=null&&passenger.world!=null?passenger.world.player:null;
            return player!=null&&!passenger.world.session.SimulationPaused&&passenger.CanAssist
                &&(passenger.inTrain?player.Riding==passenger.Riding:player.Riding==null)
                &&Mathf.Abs(player.transform.position.y-transform.position.y)<.7f
                &&(player.transform.position-transform.position).sqrMagnitude<30.25f;
        }
        void Update()
        {
            if(!Available(out var player)){current=HelperAction.ContinueJourney;Paint(false);return;}
            if(current!=HelperAction.ContinueJourney&&Time.time-helpStarted>6){current=HelperAction.ContinueJourney;cooldownUntil=Time.time+8;Paint(false);}
            if(Time.time<cooldownUntil)return;
            if(Time.time<nextDecision)return;
            nextDecision=Time.time+.3f;Snapshot(player);RequestDecision();
        }
        Vector2 Flat(Vector3 value)=>new Vector2(Vector3.Dot(value,right),Vector3.Dot(value,forward));
        void Snapshot(Metro3DPlayer player)
        {
            forward=Vector3.ProjectOnPlane(player.transform.forward,Vector3.up).normalized;right=Vector3.Cross(Vector3.up,forward);
            Vector3 relative=transform.position-player.transform.position;
            Vector2 offset=Flat(relative);
            float blocking=offset.y>.15f&&offset.y<1.4f?Mathf.Clamp01(1-Mathf.Abs(offset.x)/.65f):0;
            safePoint=player.transform.position+right*(offset.x>=0?1.15f:-1.15f)+forward*.7f;
            bool safe=crowd!=null&&crowd.StaticSpaceFree(safePoint,transform.parent)&&crowd.SpaceFree(safePoint,passenger,.48f);
            if(!safe)safePoint=player.transform.position-right*(offset.x>=0?1.15f:-1.15f)+forward*.7f;
            safe=safe||(crowd!=null&&crowd.StaticSpaceFree(safePoint,transform.parent)&&crowd.SpaceFree(safePoint,passenger,.48f));
            if(!safe&&crowd!=null){
                foreach(float sign in new[]{1f,-1f}){
                    Vector3 point=player.transform.position+forward*sign*1.05f;
                    if(crowd.StaticSpaceFree(point,transform.parent)&&crowd.SpaceFree(point,passenger,.48f)){safePoint=point;safe=true;break;}
                }
            }
            bool balance=player.BalanceActive||(player.UsesPersonalBalance&&player.PersonalBalance.ConsumesMovementInput);
            float motion=player.UsesPersonalBalance?player.PersonalBalance.CurrentTrainMotion.Intensity:player.Riding!=null&&player.Riding.Moving?.65f:0;
            snapshot=new HelperSituation {
                playerOffset=Flat(player.transform.position-transform.position),goalOffset=Flat(passenger.CurrentGoal-transform.position),
                playerVelocity=Flat(player.Velocity),helperVelocity=Flat(passenger.Velocity),balanceRisk=balance?.8f:motion*.25f,
                fatigue=1-Mathf.Clamp01(player.Stamina/100),density=crowd!=null?crowd.LocalDensity(player.transform.position,transform.parent):0,
                blocking=blocking,trainMotion=motion,cargo=passenger.world.session.HasPackage?1:0,
                safeSpace=safe||(relative.sqrMagnitude<1.35f*1.35f&&passenger.world.session.HasPackage)?1:0,timeMargin=1,previousAction=current };
        }
        public override void CollectObservations(VectorSensor sensor){snapshot.Observe(sensor);}
        public override void OnActionReceived(ActionBuffers actions)
        {
            InferenceCount++;
            if(!Available(out var player))return;
            var chosen=(HelperAction)Mathf.Clamp(actions.DiscreteActions[0],0,3);
            if(Time.time<committedUntil&&chosen!=current)chosen=current;
            else if(chosen!=current){committedUntil=Time.time+1.1f;if(current==HelperAction.ContinueJourney)helpStarted=Time.time;}
            current=chosen;
            float distance=Vector3.Distance(transform.position,player.transform.position);
            Vector3 destination=transform.position;bool accepted=false;
            if(current==HelperAction.YieldPassage)destination=player.transform.position+right*(Vector3.Dot(transform.position-player.transform.position,right)>=0?1.1f:-1.1f);
            else if(current==HelperAction.SteadyLoad)destination=player.transform.position+right*(Vector3.Dot(transform.position-player.transform.position,right)>=0?.8f:-.8f);
            else if(current==HelperAction.GuideToSpace)destination=safePoint;
            if(current!=HelperAction.ContinueJourney&&snapshot.safeSpace>.5f&&crowd.StaticSpaceFree(destination,transform.parent)&&crowd.SpaceFree(destination,passenger,.45f))accepted=passenger.TryHelperTarget(destination);
            // A citizen already beside the player can provide support in place;
            // requiring an empty lateral slot rejected every occupied-seat aisle.
            if(current==HelperAction.SteadyLoad&&distance<1.35f&&snapshot.cargo>.5f)accepted=true;
            if(accepted&&current==HelperAction.SteadyLoad&&distance<1.35f)player.ReceiveNpcSupport(.4f);
            if(accepted&&current==HelperAction.GuideToSpace&&distance<1.85f)player.ReceiveNpcGuidance(.3f);
            Paint(accepted);
        }
        void Paint(bool helping)
        {
            if(renderers==null||(painted&&wasHelping==helping))return;painted=true;wasHelping=helping;if(tint==null)tint=new MaterialPropertyBlock();
            Color color=helping?new Color(.12f,.76f,.30f):new Color(.90f,.42f,.12f);
            tint.SetColor("_BaseColor",color);tint.SetColor("_Color",color);
            foreach(var r in renderers)if(r!=null)for(int i=0;i<r.sharedMaterials.Length;i++)if(r.sharedMaterials[i]!=null&&!r.sharedMaterials[i].name.Contains("Joint"))r.SetPropertyBlock(tint,i);
        }
        public override void Heuristic(in ActionBuffers actionsOut){var actions=actionsOut.DiscreteActions;actions[0]=0;}
    }
}

using System.Collections.Generic;
using UnityEngine;
using SubwayCarry.AI.V2;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // One existing crowd review. Goals/social navigation are retained; no authoring-time scene edits.
    public sealed class AnimationLabSqueezeReview : MonoBehaviour
    {
        public enum Stage {Off,Approach,Waiting,Entering,Passing,Leaving,Done}
        AnimationLabPhoneCase owner;AnimationLabSqueezeBody body;PassengerAiV2CrowdManager crowd;
        readonly List<PassengerAiV2Agent> agents=new List<PassengerAiV2Agent>();readonly List<AnimationLabPhoneMotor> people=new List<AnimationLabPhoneMotor>();
        readonly List<PassengerAiV2Agent> nearby=new List<PassengerAiV2Agent>();
        Vector2 flow,cross,center,approach,goal,journeyGoal,releasePosition;float nextSense,originalRadius,resumeAt;bool wide,preparing,resumeSet,hasJourney;int side=1;
        float pendingWidth,pendingRotation;int pendingLead;int passageMode;
        public int PassageMode=>passageMode;
        public Vector2 TravelFlow=>flow;
        public Stage State {get;private set;}public int PendingMode=-1;
        public float Gap {get;private set;}public float MinimumDistance {get;private set;}=10;public int Completed {get;private set;}
        public AnimationLabSqueezeBody Body=>body;public IReadOnlyList<AnimationLabPhoneMotor> People=>people;
        public bool CanLeave=>State==Stage.Off||State==Stage.Approach||State==Stage.Waiting||State==Stage.Done;
        public float RequiredGap
        {
            get {var e=owner.Data.FindSqueeze(1,side,passageMode);return Mathf.Max(.48f,e!=null?e.clearanceWidth:.55f)+.045f;}
        }
        public string Status=>!owner.Data.HasSqueezeSet?"통과 동작 데이터 없음":preparing?"보행 중 폰 정리":State==Stage.Waiting?"사람 사이 간격 대기":State==Stage.Done?"통과 완료":State==Stage.Approach?"틈 확인 · 접근":body!=null&&body.Active?body.CurrentClip:"보행 복귀";
        public void Initialize(AnimationLabPhoneCase value){owner=value;crowd=owner.GetComponent<PassengerAiV2CrowdManager>();}
        public void Begin(float width=1.12f,float rotation=0,int lead=1)
        {
            if(!owner.Data.HasSqueezeSet||!CanLeave)return;
            var intendedGoal=owner.Agent.MovementTarget;End();journeyGoal=intendedGoal;hasJourney=true;resumeSet=false;PendingMode=-1;side=lead<0?-1:1;passageMode=width<=1.16f&&owner.Data.HasSideStepSet(side)?1:0;
            if(owner.Passenger.PhoneLayer.IsTransitioning||owner.Passenger.PhoneLayer.PhoneInHand)
            {
                // Finish the existing phone action while walking before staging the crowd ahead.
                preparing=true;pendingWidth=width;pendingRotation=rotation;pendingLead=lead;State=Stage.Approach;
                owner.Passenger.PhoneLayer.RequestedMode=AnimationLabPhoneMode.Stored;return;
            }
            if(body==null){body=owner.Passenger.gameObject.AddComponent<AnimationLabSqueezeBody>();body.Initialize(owner.Passenger,owner.Data);}
            var pos=owner.Passenger.Position;flow=(owner.Data.bounds.center-pos).normalized;if(flow.sqrMagnitude<.1f)flow=Vector2.up;
            flow=Rotate(flow,rotation);cross=new Vector2(flow.y,-flow.x);center=pos+flow*2.35f;approach=center-flow*1.45f;goal=center+flow*3.60f;
            Vector2[] layout={new Vector2(-width*.5f,-.04f),new Vector2(width*.5f,.12f),new Vector2(-1.10f,-.58f),new Vector2(-.96f,.61f),new Vector2(1.13f,-.39f),new Vector2(1.02f,.70f)};
            float[] yaw={28,-145,-42,132,-120,63};
            for(int i=0;i<layout.Length;i++)
            {
                var p=center+cross*layout[i].x+flow*layout[i].y;
                var visual=owner.CreatePassenger("Crowd gap · person "+i,p,p,out var agent);visual.transform.rotation=Quaternion.LookRotation(new Vector3(flow.x,0,flow.y))*Quaternion.Euler(0,yaw[i],0);
                agent.SetAutoLoop(false);agent.SetBodyClearance(.24f,.1f);agent.ConfigureArrivalTolerance(.05f);agent.SetHoldPosition(true);visual.SetIdlePhase(i*.37f);people.Add(visual);agents.Add(agent);
            }
            originalRadius=owner.Agent.Radius;owner.Agent.SetBodyClearance(.24f,.04f);owner.Agent.SetAutoLoop(false);
            owner.Agent.SetMovementTarget(goal);owner.Passenger.Goal=goal;owner.Agent.SetMovementSpeedScale(.90f);
            owner.Passenger.PhoneLayer.RequestedMode=AnimationLabPhoneMode.Stored;wide=false;MinimumDistance=10;State=Stage.Approach;
            Physics.SyncTransforms();SenseGap();
        }
        static Vector2 Rotate(Vector2 v,float angle){float a=angle*Mathf.Deg2Rad;return new Vector2(v.x*Mathf.Cos(a)+v.y*Mathf.Sin(a),-v.x*Mathf.Sin(a)+v.y*Mathf.Cos(a));}
        void SenseGap()
        {
            crowd.QueryNearby(center,1.65f,owner.Agent,nearby);PassengerAiV2Agent left=null,right=null;float l=-10,r=10;
            foreach(var a in nearby){float x=Vector2.Dot(a.Position-center,cross);if(Mathf.Abs(Vector2.Dot(a.Position-center,flow))>.4f)continue;if(x<0&&x>l){left=a;l=x;}if(x>0&&x<r){right=a;r=x;}}
            Gap=left!=null&&right!=null?r-l-left.Radius-right.Radius:0;wide=Gap>RequiredGap+.20f;
        }
        public void Tick()
        {
            if(State==Stage.Off)return;
            if(preparing){if(!owner.Passenger.PhoneLayer.IsTransitioning&&!owner.Passenger.PhoneLayer.PhoneInHand){preparing=false;Begin(pendingWidth,pendingRotation,pendingLead);}return;}
            var p=owner.Passenger.Position;owner.Passenger.Goal=owner.Agent.MovementTarget;
            foreach(var person in people)MinimumDistance=Mathf.Min(MinimumDistance,Vector2.Distance(p,person.Position));
            if(Time.time>=nextSense){nextSense=Time.time+.13f;SenseGap();}
            float along=Vector2.Dot(p-center,flow);
            if(State==Stage.Approach||State==Stage.Waiting)
            {
                if(Gap<RequiredGap)
                {
                    owner.Agent.SetMovementTarget(approach);if(owner.Agent.DistanceToTarget<.18f)State=Stage.Waiting;return;
                }
                owner.Agent.SetMovementTarget(goal);
                if(along< -1.45f)return;
                if(wide){State=Stage.Leaving;return;}
                body.Begin(flow,side,passageMode);owner.Agent.SetMovementSpeedScale(.90f);State=Stage.Entering;
            }
            else if(State==Stage.Entering&&body.PhaseComplete){body.ChangePhase(1);State=Stage.Passing;}
            else if(State==Stage.Passing&&along>1.12f&&body.Cycle<.025f){body.ChangePhase(2);State=Stage.Leaving;}
            else if(State==Stage.Leaving)
            {
                if(body.Active&&body.PhaseComplete){body.End();ResumeJourney();}
                if(!body.Active&&!resumeSet&&owner.Agent.DistanceToTarget<.8f)ResumeJourney();
                if(!body.Active&&resumeSet&&Time.time>=resumeAt&&(owner.Agent.DistanceToTarget<.18f||Vector2.Distance(releasePosition,p)>.12f)){State=Stage.Done;Completed++;if(PendingMode>=0){int m=PendingMode;PendingMode=-1;owner.SetReviewMode(m);}}
            }
        }
        void ResumeJourney(){owner.Agent.SetBodyClearance(originalRadius);owner.Agent.SetMovementSpeedScale(.90f);owner.Agent.SetMovementTarget(journeyGoal);owner.Passenger.Goal=journeyGoal;releasePosition=owner.Passenger.Position;resumeAt=Time.time+.45f;resumeSet=true;}
        public void End()
        {
            body?.End();if(State!=Stage.Off&&owner.Agent!=null&&!preparing)owner.Agent.SetBodyClearance(originalRadius);if(hasJourney&&owner.Agent!=null)owner.Agent.SetMovementTarget(journeyGoal);hasJourney=false;State=Stage.Off;preparing=false;
            for(int i=0;i<agents.Count;i++){if(agents[i]!=null)Destroy(agents[i].gameObject);if(people[i]!=null)Destroy(people[i].gameObject);}agents.Clear();people.Clear();
        }
        void OnDestroy(){End();}
    }
}


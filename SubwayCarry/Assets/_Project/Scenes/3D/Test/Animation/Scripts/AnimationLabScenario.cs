using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Profiling;
using Use=SubwayCarry.Gameplay3D.AnimationLab.AnimationLabMotionSet.Use;
using Intent=SubwayCarry.Gameplay3D.AnimationLab.AnimationLabMotionSearch.Intent;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Review-only adapter. CharacterController owns all root movement; the existing graph owns pose.
    [DefaultExecutionOrder(250)]
    public sealed class AnimationLabScenario : MonoBehaviour
    {
        public const string Revision="NORMAL_POSTURE_04";
        public static readonly string[] NormalCases={"출발 · 직선 · 정지","완만한 좌회전","완만한 우회전","왼쪽 코너","오른쪽 코너","정지 후 180°","출발 취소 · 재입력","걷는 중 반전","직접 조작"};
        public static readonly string[] SituationCases={"다가오는 보행자","좁은 통로","상체만 양보 — 원본 미확보","옆걸음","안내물 확인","가벼운 접촉 — 원본 미확보"};
        static readonly ProfilerMarker SearchProfile=new ProfilerMarker("SubwayCarry.AnimationReview.FrameSearch");
        static readonly ProfilerMarker PoseProfile=new ProfilerMarker("SubwayCarry.AnimationReview.Pose");
        public AnimationLabCharacter Actor {get;private set;}
        public AnimationLabMotionSet Data {get;private set;}
        public readonly AnimationLabMotionSearch Search=new AnimationLabMotionSearch();
        public bool Situations {get;private set;}
        public bool Automated {get;set;}
        public int Case {get;private set;}
        public string Reason {get;private set;}="";
        public string Missing=>Situations&&(Case==2||Case==5)?"전용 동작 데이터 미확보 · 대체 모션 없음":!Situations&&Search.postureFamily==1&&Mathf.Abs(ActualYawRate)>15?"동일 자세군 선회 원본 미확보 · 기존 피벗/제한 보정":"";
        public AnimationLabMotionSet.Entry Current=>index>=0&&Data!=null?Data.entries[index]:null;
        public float TimeInClip=>time;
        public int EntryIndex=>index;
        public int PreviousIndex=>oldIndex;
        public float PreviousTime=>oldTime;
        public int DecisionStatus {get;private set;}
        public float QueryTime {get;private set;}
        public float Cost {get;private set;}
        public int Switches {get;private set;}
        public float LeftSupport=>!Situations&&poseSampled?observedLeftSupport:Current==null?0:Current.At(SampleTime(Current,time)).leftSupport;
        public float RightSupport=>!Situations&&poseSampled?observedRightSupport:Current==null?0:Current.At(SampleTime(Current,time)).rightSupport;
        public Vector3 Desired {get;private set;}
        public float ActualYawRate {get;private set;}
        public float Blend=>fade<=0?1:Mathf.Clamp01(blendTime/fade);
        public long PoseTicks,PoseFrames,PoseBytes;
        public Vector3 LastCommand {get;private set;}
        public Vector3 LastRootDelta {get;private set;}
        public float Elapsed=>elapsed;
        AnimationLabFreeWalkData free;
        public AnimationLabPoseProfile NormalPose {get;private set;}
        public AnimationLabPoseProfile PoseDraft {get;set;}
        readonly Quaternion[] normalBase=new Quaternion[AnimationLabPoseProfile.Bones.Length];
        public Transform PoseJoint(int i)=>i>=0&&i<profileBones.Length?profileBones[i]:null;
        public Quaternion BaseJointRotation(int i)=>normalBase[i];
        public void RefreshPose()=>Present(0);
        public float LegacyIdleContribution=>legacyIdleWeight;
        public float NormalIdleContribution=>normalIdleWeight;
        int index=-1,oldIndex=-1;
        float time,oldTime,blendTime,fade,searchAfter,held,elapsed,sideHold,chosenSide=1,lookHold;
        float startHandoff;
        float normalSpeed,normalYawRate;
        bool normalCruise;
        Vector3 previousGoal;
        float normalIdleWeight,normalIdleVelocity,normalIdleFrom,normalIdleFromVelocity;
        float legacyIdleWeight,legacyIdleVelocity,legacyIdleFrom,legacyIdleFromVelocity;
        float lastYaw,nextText,leg;
        Intent previousIntent;
        Vector3 spawn,manualInput;
        Vector3 previousLeft,previousRight,leftVelocity,rightVelocity;
        Vector3 previousWorldLeft,previousWorldRight;
        float observedLeftSupport,observedRightSupport;
        Transform leftToe,rightToe;
        bool poseSampled;
        Quaternion spawnRotation;
        bool ready,manualOverride;
        Transform hips,lf,rf,lk,rk,ll,rl,sign;
        GameObject fixtures,peer;
        CharacterController peerMotor;
        AnimationLabFreeWalkPlayer peerPlayer;
        float peerTime;
        readonly Transform[] profileBones=new Transform[AnimationLabPoseProfile.Bones.Length];
        string caption="";
        GUIStyle text,button;
        public bool Initialize(AnimationLabCharacter actor,bool situations)
        {
            Actor=actor;Situations=situations;Data=Resources.Load<AnimationLabMotionSet>("ReviewMotions");free=Resources.Load<AnimationLabFreeWalkData>("FreeWalkReview");
            NormalPose=situations?null:Resources.Load<AnimationLabPoseProfile>("NormalPoseProfile");
            if(Data==null||Data.entries.Length==0||free==null||!Actor.EnableFreeWalk(free)){Reason="검수 데이터/Avatar 누락 · 기존 재생 유지";Debug.LogWarning(Reason,Actor);enabled=false;return false;}
            Actor.Scenario=this;Actor.FreePoseReview=false;Actor.FreeWalkAuto=false;
            Actor.FreeWalk.InertialTransitions=!situations;
            if(!situations&&!Array.Exists(Data.entries,row=>row.usable&&row.postureFamily==1&&(row.uses&Use.Forward)!=0))
            {Reason="Normal 호환 보행 데이터 누락 · 혼합 자세군 검색 중단";Debug.LogWarning(Reason,this);enabled=false;return false;}
            RestoreSearchMode();
            if(!situations)Debug.Log(Revision+" | posture family="+Search.postureFamily+" | render motor + composite pose continuation",this);
            hips=Bone(HumanBodyBones.Hips);lf=Bone(HumanBodyBones.LeftFoot);rf=Bone(HumanBodyBones.RightFoot);
            leftToe=Bone(HumanBodyBones.LeftToes);rightToe=Bone(HumanBodyBones.RightToes);
            lk=Bone(HumanBodyBones.LeftLowerLeg);rk=Bone(HumanBodyBones.RightLowerLeg);ll=Bone(HumanBodyBones.LeftUpperLeg);rl=Bone(HumanBodyBones.RightUpperLeg);
            leg=Vector3.Distance(ll.position,lk.position)+Vector3.Distance(lk.position,lf.position);
            for(int b=0;b<profileBones.Length;b++)profileBones[b]=Bone(AnimationLabPoseProfile.Bones[b]);
            spawn=new Vector3(0,actor.transform.position.y,-5);spawnRotation=Quaternion.identity;
            ready=true;SelectCase(0);return true;
        }
        Transform Bone(HumanBodyBones b)=>Actor.animator.GetBoneTransform(b);
        public void SelectCase(int value)
        {
            if(!ready)return;Case=Mathf.Clamp(value,0,(Situations?SituationCases:NormalCases).Length-1);
            ClearFixtures();Actor.SetFrozen(false);
            var placement=spawn;
            if(!Situations&&Physics.Raycast(spawn+Vector3.up*.5f,Vector3.down,out var ground,.8f,Actor.environment,QueryTriggerInteraction.Ignore))
                placement.y=ground.point.y-Actor.motor.center.y+Actor.motor.height*.5f+Actor.motor.skinWidth;
            Actor.ResetReviewMotor(placement,spawnRotation);elapsed=held=searchAfter=sideHold=lookHold=0;
            manualOverride=false;manualInput=Vector3.zero;index=oldIndex=-1;time=oldTime=blendTime=0;Switches=0;Reason="같은 조건에서 시작";
            normalSpeed=normalYawRate=0;normalCruise=false;previousGoal=Vector3.zero;
            normalIdleWeight=normalIdleVelocity=legacyIdleWeight=legacyIdleVelocity=0;
            poseSampled=false;leftVelocity=rightVelocity=Vector3.zero;
            for(int i=0;i<Data.entries.Length;i++)if(Data.entries[i].usable&&(Data.entries[i].uses&Use.Idle)!=0&&(Search.postureFamily==0||Data.entries[i].postureFamily==Search.postureFamily)){SwitchTo(i,0,0,"검수 시작");break;}
            if(index<0)throw new InvalidOperationException("Review idle is missing");
            if(Situations)BuildFixtures();lastYaw=Actor.transform.eulerAngles.y;Physics.SyncTransforms();Present(0);
        }
        public void SetManualInput(Vector3 input){manualOverride=true;manualInput=Vector3.ClampMagnitude(input,1);}
        void Update()
        {
            if(!ready||Actor==null)return;
            if(!Situations)Actor.orbit.CaptureInput();
            if(!Automated&&Keyboard.current!=null)
            {
                var k=Keyboard.current;if(k.f6Key.wasPressedThisFrame)Actor.SetFrozen(!Actor.Frozen);
                if(k.rKey.wasPressedThisFrame)SelectCase(Case);
                Vector2 move=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
                if(move.sqrMagnitude>0){manualOverride=true;var forward=Actor.orbit.FlatForward;manualInput=forward*move.y+Vector3.Cross(Vector3.up,forward)*move.x;}
                else manualInput=Vector3.zero;
            }
            if(!Situations)Actor.StepReviewFrame(Time.deltaTime);
            if(Time.unscaledTime>=nextText)
            {
                nextText=Time.unscaledTime+.2f;
                caption=$"{(Situations?"Situations":"Normal P04 · 자세군 "+Search.postureFamily)} · {(Situations?SituationCases:NormalCases)[Case]}\n{Current?.label}  {SampleTime(Current,time):F2}s · 실제 {Actor.ActualVelocity.magnitude:F2}m/s · 회전 {ActualYawRate:F0}°/s\n{Reason}{(Missing.Length>0?" · "+Missing:"")}";
            }
        }
        Vector3 CaseGoal()
        {
            if(manualOverride||!Situations&&Case==8)return Vector3.ClampMagnitude(manualInput,1)*1.55f;
            if(Situations)
            {
                if(Case==2||Case==5||Case==4)return Vector3.zero;
                if(Case==3)return elapsed>1&&elapsed<3.5f?Vector3.right*.65f:Vector3.zero;
                if(elapsed<1)return Vector3.zero;
                var target=new Vector3(0,spawn.y,3);var delta=target-Actor.transform.position;delta.y=0;
                if(delta.magnitude<1.2f)return Vector3.zero;
                if(Case==0&&peer!=null)
                {
                    var separation=peer.transform.position-Actor.transform.position;separation.y=0;
                    float ahead=Vector3.Dot(separation,Actor.transform.forward);
                    if(ahead>-.4f&&ahead<2.7f&&Mathf.Abs(Vector3.Dot(separation,Actor.transform.right))<.85f)
                    {if(sideHold<=0){chosenSide=SideClear(1)?1:SideClear(-1)?-1:0;sideHold=.8f;}return chosenSide!=0?Actor.transform.right*(chosenSide*.65f):Vector3.zero;}
                }
                return delta.normalized*1.55f;
            }
            if(elapsed<1)return Vector3.zero;
            if(Case==0&&elapsed>=3.8f)return Vector3.zero;
            if(Case==6)return elapsed<1.35f||elapsed>2&&elapsed<5?Vector3.forward*1.55f:Vector3.zero;
            if(Case==7)return elapsed<3?Vector3.forward*1.55f:elapsed<7?Vector3.back*1.55f:Vector3.zero;
            if(Case==5)return elapsed<3?Vector3.zero:elapsed<6?Vector3.back*1.55f:Vector3.zero;
            // Cases provide geometry-independent intent curves. Clip names/times are never scheduled.
            if(elapsed>7)return Vector3.zero;
            float turn=Case==1?-35:Case==2?35:Case==3?-90:Case==4?90:0;
            float yaw=turn*Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-2.6f)/1.5f));
            return Quaternion.Euler(0,yaw,0)*Vector3.forward*1.55f;
        }
        bool SideClear(float side)
        {
            return !Blocked(Actor.transform.position,Actor.transform.right*side*.7f);
        }
        public bool Blocked(Vector3 from,Vector3 delta)
        {
            float distance=Vector3.ProjectOnPlane(delta,Vector3.up).magnitude;if(distance<.0001f)return false;
            var center=from+Actor.motor.center;float radius=Actor.motor.radius;
            float extent=Mathf.Max(0,Actor.motor.height*.5f-radius);
            return Physics.CapsuleCast(center+Vector3.up*extent,center-Vector3.up*extent,Mathf.Max(.01f,radius-.015f),delta.normalized,distance+.02f,Actor.environment,QueryTriggerInteraction.Ignore);
        }
        public void Step(float dt,out Vector3 velocity,out float yaw)
        {
            velocity=Vector3.zero;yaw=0;if(!ready||Current==null)return;
            elapsed+=dt;held+=dt;searchAfter-=dt;sideHold=Mathf.Max(0,sideHold-dt);TickPeer(dt);
            Desired=CaseGoal();Vector3 local=Actor.transform.InverseTransformDirection(Desired);
            float angle=Desired.sqrMagnitude>.001f?Vector3.SignedAngle(Actor.transform.forward,Desired,Vector3.up):0;
            var intent=Desired.sqrMagnitude<.01f?(Actor.ActualVelocity.magnitude>.12f?Intent.Brake:Intent.Rest):Intent.Travel;
            if(Mathf.Abs(angle)>110)intent=Actor.ActualVelocity.magnitude>.16f?Intent.Brake:Intent.Pivot;
            bool samePivotDirection=Mathf.Sign(angle)==Mathf.Sign(Current.netYaw);
            if((Current.uses&Use.Pivot)!=0&&Desired.sqrMagnitude>.01f&&Mathf.Abs(angle)>(Situations?10:45)&&samePivotDirection)intent=Intent.Pivot;
            if(Situations&&(Current.uses&Use.Stop)!=0&&Desired.sqrMagnitude<.01f&&time<Current.duration-.5f)intent=Intent.Brake;
            if(!Situations)
            {
                if(intent==Intent.Rest)normalCruise=false;
                if((Current.uses&Use.Start)!=0&&time>=startHandoff)normalCruise=true;
                Search.cruise=normalCruise;
                if(Desired.sqrMagnitude>.01f&&previousGoal.sqrMagnitude>.01f&&Vector3.Angle(Desired,previousGoal)>4)searchAfter=0;
                previousGoal=Desired;
            }
            if(Situations&&(Case==3||Case==0&&sideHold>0)&&Desired.sqrMagnitude>.01f)intent=Intent.SideStep;
            if(Situations&&Case==4&&sign!=null&&elapsed>1&&elapsed<5.5f&&!Physics.Linecast(Actor.transform.position+Vector3.up*1.55f,sign.position,Actor.environment,QueryTriggerInteraction.Ignore))intent=Intent.Look;
            if(Desired.sqrMagnitude>.01f&&Blocked(Actor.transform.position,Desired.normalized*Mathf.Max(.15f,Actor.ActualVelocity.magnitude*.45f)))
            {intent=Actor.ActualVelocity.magnitude>.12f?Intent.Brake:Intent.Rest;Reason="장애물 · 실제 캡슐 여유 부족";}
            PrepareQuery(intent,angle,dt);
            float endTime=Current.loop?float.PositiveInfinity:Current.duration;
            bool ended=time>=endTime-.06f;
            DecisionStatus=0;
            if(searchAfter<=0||ended)
            {
                searchAfter=.08f;
                using(SearchProfile.Auto())
                {
                    var found=Search.Find(Data);float continuation=Search.Allowed(Current)?Search.Cost(Current,SampleTime(Current,time)):float.PositiveInfinity;
                    bool changed=intent!=previousIntent;
                    bool committedPivot=Situations&&(Current.uses&Use.Pivot)!=0&&intent==Intent.Pivot&&!ended;
                    // Keep the first source support exchange while accelerating straight ahead.
                    // Cancellation/turn intent may still interrupt; this is not a fixed wait timer.
                    bool firstStep=!Situations&&(Current.uses&Use.Start)!=0&&intent==Intent.Travel&&Mathf.Abs(angle)<12&&time<startHandoff;
                    bool redirected=!Situations&&Mathf.Abs(angle)>4&&held>.12f;
                    bool need=!committedPivot&&!firstStep&&(ended||changed||redirected||held>(Situations?.45f:.18f)&&found.valid&&found.cost+.1f<continuation);
                    QueryTime=elapsed;DecisionStatus=committedPivot?1:!found.valid?2:!need?3:Situations&&Blend<.999f?4:5;
                    if(found.valid&&need&&(Blend>=.999f||!Situations))
                    {
                        float phaseDistance=Mathf.Abs(found.time-SampleTime(Current,time));
                        if(Current.loop)phaseDistance=Mathf.Min(phaseDistance,Mathf.Abs(Current.duration-phaseDistance));
                        bool same=found.entry==index&&phaseDistance<.18f;
                        if(!same&&CanEnter(Data.entries[found.entry],found.time))
                        {
                            float poseGap=Vector3.Distance(Search.pose.leftFoot,Data.entries[found.entry].At(found.time).leftFoot)+Vector3.Distance(Search.pose.rightFoot,Data.entries[found.entry].At(found.time).rightFoot);
                            float bodyGap=Mathf.Abs(Mathf.DeltaAngle(Search.pose.bodyYaw,Data.entries[found.entry].At(found.time).bodyYaw));
                            float duration=Mathf.Clamp(.08f+poseGap*.12f+bodyGap*.0015f,.1f,.28f);
                            SwitchTo(found.entry,found.time,duration,ended?"동작 끝 · 자세/궤적 검색":changed?"의도 변경 · 자세/지지발 검색":"더 적합한 궤적 · 전환");
                            Cost=found.cost;
                            DecisionStatus=6;
                        }
                    }
                    previousIntent=intent;
                }
            }
            var current=Current;float rate=1;
            if(intent==Intent.Travel&&(current.uses&Use.Start)==0&&current.meanSpeed>.4f)
                rate=Situations?Mathf.Clamp(Desired.magnitude/current.meanSpeed,.85f,1.15f):Mathf.Clamp(Mathf.Max(.5f,normalSpeed)/Mathf.Max(.5f,current.At(SampleTime(current,time)).speed),.55f,1.4f);
            float next=current.loop?time+dt*rate:Mathf.Min(current.duration,time+dt*rate);
            current.Delta(time,next,out var delta,out float rotation);var frame=current.At(SampleTime(current,time));
            delta=Quaternion.Euler(0,-frame.yaw,0)*delta;delta.y=0;
            blendTime+=dt;float weight=Mathf.SmoothStep(0,1,Blend);
            if(oldIndex>=0&&weight<1)
            {
                var previous=Data.entries[oldIndex];float oldNext=previous.loop?oldTime+dt:Mathf.Min(previous.duration,oldTime+dt);
                previous.Delta(oldTime,oldNext,out var oldDelta,out float oldYaw);oldDelta=Quaternion.Euler(0,-previous.At(SampleTime(previous,oldTime)).yaw,0)*oldDelta;oldDelta.y=0;
                delta=Vector3.Lerp(oldDelta,delta,weight);rotation=Mathf.Lerp(oldYaw,rotation,weight);oldTime=oldNext;
            }
            if(!Situations)
            {
                float nativeRotation=rotation;
                // Animation supplies stride/support data. Input owns the executable walk arc;
                // a slow/end frame of a turn clip must not become a motor brake command.
                float target=0;
                if(intent==Intent.Travel)
                {
                    target=Desired.magnitude;
                    if((current.uses&Use.Start)!=0&&!normalCruise&&time<startHandoff)target=Mathf.Min(target,frame.speed);
                }
                normalSpeed=Mathf.MoveTowards(normalSpeed,target,dt*(target<normalSpeed?2.8f:2));
                if(intent==Intent.Pivot)
                {
                    normalYawRate=0; // The source pivot supplies its planted stepping rotation.
                    rotation=Mathf.Sign(rotation)==Mathf.Sign(angle)?Mathf.Sign(angle)*Mathf.Min(Mathf.Abs(rotation),Mathf.Abs(angle)):0;
                    delta=Vector3.ClampMagnitude(delta,.45f*dt);
                }
                else
                {
                    float wanted=Desired.sqrMagnitude>.01f?Mathf.Clamp(angle/.22f,-90,90):0;
                    normalYawRate=Mathf.MoveTowards(normalYawRate,wanted,480*dt);
                    rotation=normalYawRate*dt;
                    if(Desired.sqrMagnitude>.01f&&Mathf.Sign(rotation)==Mathf.Sign(angle)&&Mathf.Abs(rotation)>Mathf.Abs(angle)){rotation=angle;normalYawRate=0;}
                    delta=Quaternion.Euler(0,rotation*.5f,0)*Vector3.forward*(normalSpeed*dt);
                    // Bounded contact-aware root arc adjustment, not IK or a second root writer.
                    // Only compensate extra steering, never translate a foot or override a collision.
                    if(normalSpeed>.2f&&Mathf.Abs(normalYawRate)>10)
                    {
                        Transform support=LeftSupport>.8f&&RightSupport<.2f?lf:RightSupport>.8f&&LeftSupport<.2f?rf:null;
                        if(support!=null)
                        {
                            var anchor=Actor.transform.InverseTransformPoint(support.position);anchor.y=0;
                            var correction=anchor-Quaternion.Euler(0,rotation-nativeRotation,0)*anchor;
                            delta+=Vector3.ClampMagnitude(correction,normalSpeed*dt*.15f);
                            delta=delta.normalized*(normalSpeed*dt);
                        }
                    }
                }
            }
            else if(intent==Intent.Travel&&Mathf.Abs(angle)<12&&(current.uses&Use.MovingTurn)==0)rotation+=Mathf.Clamp(angle,-6*dt,6*dt);
            var world=Actor.transform.TransformDirection(delta);LastRootDelta=world;
            if(Blocked(Actor.transform.position,world)){world=Vector3.zero;Reason="충돌 검사로 이동 차단 · 보행 중단 요청";}
            velocity=world/dt;yaw=rotation;LastCommand=velocity;time=next;
        }
        void PrepareQuery(Intent intent,float angle,float dt)
        {
            // Search is a readonly, non-serialized working object. A script reload recreates
            // it while this component's journey/clip fields survive. Derive the mode from
            // its owner at every query; never let that reset reopen incompatible clips.
            RestoreSearchMode();Search.continuation=Situations?null:Current;
            Search.intent=intent;Search.desiredTurn=angle;Search.side=Mathf.Sign(Vector3.Dot(Desired,Actor.transform.right));
            Search.velocity=Actor.transform.InverseTransformDirection(Actor.ActualVelocity);
            var body=Quaternion.LookRotation(Vector3.Cross(Vector3.ProjectOnPlane(rl.position-ll.position,Vector3.up),Vector3.up));var inverse=Quaternion.Inverse(body);
            Search.pose=new AnimationLabMotionSet.Frame{leftFoot=inverse*(lf.position-hips.position)/leg,rightFoot=inverse*(rf.position-hips.position)/leg,leftKnee=inverse*(lk.position-hips.position)/leg,rightKnee=inverse*(rk.position-hips.position)/leg,hips=inverse*(hips.position-Actor.animator.transform.position)/leg,leftSupport=LeftSupport,rightSupport=RightSupport,leftVelocity=leftVelocity,rightVelocity=rightVelocity,bodyYaw=Mathf.DeltaAngle(Actor.transform.eulerAngles.y,body.eulerAngles.y)};
            if(sign!=null)Search.lookYaw=Mathf.Clamp(Vector3.SignedAngle(Actor.transform.forward,sign.position-Actor.transform.position,Vector3.up),-65,65);
            Vector3 p=Vector3.zero,direction=Vector3.forward;float speed=Actor.ActualVelocity.magnitude,heading=0,integrated=0,predictedYawRate=normalYawRate;
            for(int h=0;h<3;h++)
            {
                while(integrated<AnimationLabMotionSearch.Horizons[h]-.001f)
                {
                    float step=Mathf.Min(.05f,AnimationLabMotionSearch.Horizons[h]-integrated);
                    float target=(intent==Intent.Rest||intent==Intent.Brake||intent==Intent.Pivot||intent==Intent.Look)?0:Desired.magnitude;
                    speed=Mathf.MoveTowards(speed,target,step*(target<speed?2.8f:1.8f));
                    if(intent==Intent.SideStep)direction=new Vector3(Search.side,0,0);
                    else if(intent==Intent.Travel||intent==Intent.Pivot)
                    {
                        if(!Situations&&intent==Intent.Travel){predictedYawRate=Mathf.MoveTowards(predictedYawRate,Mathf.Clamp((angle-heading)/.22f,-90,90),480*step);heading+=predictedYawRate*step;}
                        else heading=Mathf.MoveTowardsAngle(heading,angle,(intent==Intent.Pivot?150:60)*step);
                        direction=Quaternion.Euler(0,heading,0)*Vector3.forward;
                    }
                    p+=direction*(speed*step);integrated+=step;
                }
                Search.future[h]=p;Search.futureYaw[h]=intent==Intent.SideStep?0:heading;
            }
        }
        bool CanEnter(AnimationLabMotionSet.Entry entry,float at)
        {
            if(!Situations)return Search.intent==Intent.Rest||Search.intent==Intent.Brake||!Blocked(Actor.transform.position,Actor.transform.TransformDirection(Search.future[0]));
            entry.Delta(at,at+.3f,out var delta,out _);delta=Quaternion.Euler(0,-entry.At(at).yaw,0)*delta;delta.y=0;
            return !Blocked(Actor.transform.position,Actor.transform.TransformDirection(delta));
        }
        void SwitchTo(int entry,float at,float duration,string reason)
        {
            normalIdleFrom=normalIdleWeight;normalIdleFromVelocity=normalIdleVelocity;
            legacyIdleFrom=legacyIdleWeight;legacyIdleFromVelocity=legacyIdleVelocity;
            oldIndex=index;oldTime=time;index=entry;time=at;held=blendTime=0;fade=duration;Reason=reason;Switches++;
            startHandoff=(Current.uses&Use.Start)!=0?FirstPlantSettled(Current,at):at;
            Actor.FreeWalk.SelectExternal(Current.clip,SampleTime(Current,at),duration);
        }
        float FirstPlantSettled(AnimationLabMotionSet.Entry entry,float at)
        {
            int initial=0;bool exchanged=false;
            for(int i=Mathf.CeilToInt(at*entry.sampleRate);i<entry.frames.Length-1;i++)
            {
                var f=entry.frames[i];var next=entry.frames[i+1];
                int side=f.leftSupport>.8f&&f.rightSupport<.2f&&next.leftSupport>.8f?1:f.rightSupport>.8f&&f.leftSupport<.2f&&next.rightSupport>.8f?-1:0;
                if(side==0)continue;
                if(initial==0){initial=side;continue;}
                if(side!=initial)exchanged=true;
                if(exchanged&&i>0)
                {
                    float Height(int j){var p=entry.frames[j];return p.root.y+(p.hips.y+(side>0?p.leftFoot.y:p.rightFoot.y))*leg;}
                    if(Height(i)<=Height(i-1)&&Height(i+1)>=Height(i))return i/entry.sampleRate;
                }
            }
            return at; // No inferred exchange metadata: never invent a hold or freeze the actor.
        }
        public static float SampleTime(AnimationLabMotionSet.Entry entry,float seconds)=>entry==null?0:entry.loop?Mathf.Repeat(seconds,entry.duration):Mathf.Clamp(seconds,0,entry.duration);
        public void Present(float dt)
        {
            if(!ready||Actor==null||Actor.FreeWalk==null)return;
            using(PoseProfile.Auto())
            {
                long began=System.Diagnostics.Stopwatch.GetTimestamp(),bytes=GC.GetAllocatedBytesForCurrentThread();
                Actor.FreeWalk.EvaluateExternal(SampleTime(Current,time),oldIndex<0?0:SampleTime(Data.entries[oldIndex],oldTime),Blend,Actor.transform,dt);
                // Pose caching is deliberately before FK. Carry the displayed correction weights
                // and their velocities separately, so a mid-transition search cannot drop/double FK.
                float previousNormal=normalIdleWeight,previousLegacy=legacyIdleWeight;
                normalIdleWeight=CorrectionWeight(normalIdleFrom,normalIdleFromVelocity,(Current.uses&Use.Idle)!=0?1:0,Blend,fade);
                legacyIdleWeight=Situations?(Current.clip==free.idle&&Blend>=1?1:0):CorrectionWeight(legacyIdleFrom,legacyIdleFromVelocity,Current.clip==free.idle?1:0,Blend,fade);
                if(dt>0){normalIdleVelocity=(normalIdleWeight-previousNormal)/dt;legacyIdleVelocity=(legacyIdleWeight-previousLegacy)/dt;}
                // The saved legacy correction still belongs only to its original Idle; its
                // outgoing contribution decays instead of disappearing on the Start switch.
                if(legacyIdleWeight>0&&free.poseProfile!=null)
                {
                    var row=free.poseProfile.For(AnimationLabPoseProfile.Clip.Idle);
                    if(row.enabled)for(int i=0;i<profileBones.Length;i++)if(profileBones[i]!=null)profileBones[i].localRotation*=Quaternion.SlerpUnclamped(Quaternion.identity,row.Get(i),row.strength*legacyIdleWeight);
                }
                // Separate target-specific profile, applied once to a freshly evaluated pose.
                // It is not the legacy Kubold profile and never modifies imported motion/Avatar.
                var correction=PoseDraft!=null?PoseDraft:NormalPose;
                for(int i=0;i<profileBones.Length;i++)if(profileBones[i]!=null)
                {
                    normalBase[i]=profileBones[i].localRotation;
                    if(correction==null)continue;
                    var a=correction.idle;var b=correction.walk;
                    var idle=Quaternion.SlerpUnclamped(Quaternion.identity,a.Get(i),a.enabled?a.strength:0);
                    var walk=Quaternion.SlerpUnclamped(Quaternion.identity,b.Get(i),b.enabled?b.strength:0);
                    profileBones[i].localRotation*=Quaternion.Slerp(walk,idle,normalIdleWeight);
                }
                ActualYawRate=dt>0?Mathf.DeltaAngle(lastYaw,Actor.transform.eulerAngles.y)/dt:0;lastYaw=Actor.transform.eulerAngles.y;
                var inverse=Quaternion.Inverse(Quaternion.LookRotation(Vector3.Cross(Vector3.ProjectOnPlane(rl.position-ll.position,Vector3.up),Vector3.up)));
                var left=inverse*(lf.position-hips.position)/leg;var right=inverse*(rf.position-hips.position)/leg;
                if(poseSampled&&dt>0&&!Actor.Frozen){leftVelocity=(left-previousLeft)/dt;rightVelocity=(right-previousRight)/dt;}
                if(!Situations&&Physics.Raycast(Actor.transform.position+Vector3.up*.2f,Vector3.down,out var ground,.6f,Actor.environment,QueryTriggerInteraction.Ignore))
                {
                    float Contact(Transform foot,Transform toe,Vector3 previous)
                    {
                        float speed=poseSampled&&dt>0?Vector3.ProjectOnPlane((foot.position-previous)/dt,Vector3.up).magnitude:0;
                        float height=(toe!=null?Mathf.Min(toe.position.y,foot.position.y):foot.position.y)-ground.point.y;
                        return (1-Mathf.InverseLerp(.2f,.6f,speed))*(1-Mathf.InverseLerp(.025f+Actor.motor.skinWidth,.065f+Actor.motor.skinWidth,height));
                    }
                    if(dt>0||!poseSampled){observedLeftSupport=Contact(lf,leftToe,previousWorldLeft);observedRightSupport=Contact(rf,rightToe,previousWorldRight);}
                }
                previousWorldLeft=lf.position;previousWorldRight=rf.position;
                previousLeft=left;previousRight=right;poseSampled=true;
                PoseTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-began;PoseBytes+=GC.GetAllocatedBytesForCurrentThread()-bytes;PoseFrames++;
            }
        }
        void LateUpdate(){if(Situations)Present(Time.deltaTime);}
        static float CorrectionWeight(float from,float velocity,float target,float t,float duration)
        {
            if(duration<=0)return target;
            float t2=t*t,t3=t2*t;
            return Mathf.Clamp01((2*t3-3*t2+1)*from+(t3-2*t2+t)*duration*velocity+(-2*t3+3*t2)*target);
        }
        void BuildFixtures()
        {
            fixtures=new GameObject("Situations · current case");
            if(Case==0||Case==5)
            {
                peer=new GameObject("Approaching participant");peer.transform.SetParent(fixtures.transform);peer.transform.SetPositionAndRotation(new Vector3(0,0,1),Quaternion.Euler(0,180,0));
                peerMotor=peer.AddComponent<CharacterController>();peerMotor.radius=.26f;peerMotor.height=1.75f;peerMotor.center=Vector3.up*.875f;
                var model=Instantiate(Actor.animator.gameObject,peer.transform);model.transform.localPosition=Actor.animator.transform.localPosition;model.transform.localRotation=Actor.animator.transform.localRotation;
                foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
                var animator=model.GetComponent<Animator>();peerPlayer=new AnimationLabFreeWalkPlayer(animator,free);
                foreach(var entry in Data.entries)if(entry.usable&&entry.loop&&(entry.uses&Use.Forward)!=0){peerPlayer.SelectExternal(entry.clip,0,0);break;}
                peerTime=0;
            }
            if(Case==1||Case==2)
            {
                float width=Case==1?1.15f:.68f;
                Box("Left passage",new Vector3(-width*.5f-.1f,.8f,-1.5f),new Vector3(.2f,1.6f,5));
                Box("Right passage",new Vector3(width*.5f+.1f,.8f,-1.5f),new Vector3(.2f,1.6f,5));
            }
            if(Case==4){sign=new GameObject("안내물 주시 대상").transform;sign.SetParent(fixtures.transform);sign.position=new Vector3(2.6f,1.6f,-2);var board=Box("안내판",sign.position,new Vector3(.6f,.4f,.06f));Destroy(board.GetComponent<Collider>());}
        }
        GameObject Box(string name,Vector3 p,Vector3 scale)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(fixtures.transform);go.transform.position=p;go.transform.localScale=scale;
            var renderer=go.GetComponent<Renderer>();foreach(var r in Actor.gameObject.scene.GetRootGameObjects()){var source=r.GetComponentInChildren<MeshRenderer>();if(source!=null&&source.sharedMaterial!=null){renderer.sharedMaterial=source.sharedMaterial;break;}}
            return go;
        }
        void TickPeer(float dt)
        {
            if(peer==null||peerMotor==null)return;
            float speed=Case==5?.55f:.9f;var separation=Actor.transform.position-peer.transform.position;separation.y=0;
            if(Vector3.Dot(separation,peer.transform.forward)>0&&separation.magnitude<1.15f)speed=0;
            if(elapsed>1)peerMotor.Move((peer.transform.forward*speed+Vector3.down*2)*dt);
            if(speed>.01f)peerTime+=dt*.65f;
            peerPlayer?.EvaluateExternal(Mathf.Repeat(peerTime,peerPlayer.Clip.length),0,1,peer.transform);
        }
        void ClearFixtures(){peerPlayer?.Dispose();peerPlayer=null;if(fixtures!=null){fixtures.SetActive(false);Destroy(fixtures);}peer=null;peerMotor=null;sign=null;}
        void OnGUI()
        {
            if(!ready||Automated)return;if(text==null){text=new GUIStyle(GUI.skin.label){font=Actor.library.font,fontSize=14,wordWrap=true};button=new GUIStyle(GUI.skin.button){font=Actor.library.font,fontSize=13};}
            float width=Mathf.Min(Screen.width-24,750);GUI.Box(new Rect(12,12,width,111),"");GUI.Label(new Rect(24,19,width-24,75),caption,text);
            GUI.Label(new Rect(24,94,width-24,22),"WASD 직접 조작 · 마우스/휠 시점 · R 현재 조건 재시작 · F6 정지 · Esc 커서",text);
            var cases=Situations?SituationCases:NormalCases;float x=12,y=Screen.height-42;
            for(int i=0;i<cases.Length;i++){float w=Situations?184:145;if(x+w>Screen.width-12){x=12;y-=31;}if(GUI.Button(new Rect(x,y,w,27),(i==Case?"● ":"")+cases[i],button))SelectCase(i);x+=w+5;}
        }
        void OnDestroy(){ClearFixtures();if(Actor!=null&&Actor.Scenario==this)Actor.Scenario=null;}
        void OnEnable()
        {
            RestoreSearchMode();
            if(!ready||Actor==null||Actor.FreeWalk==null||Current==null)return;
            // This readonly cache is also recreated by a script reload. Rebind references,
            // preserving the authored profile and applying its rotations only in Present.
            for(int b=0;b<profileBones.Length;b++)profileBones[b]=Bone(AnimationLabPoseProfile.Bones[b]);
            Actor.FreeWalk.InertialTransitions=!Situations;
            Actor.FreeWalk.SelectExternal(Current.clip,SampleTime(Current,time),0);
            oldIndex=-1;fade=blendTime=0;poseSampled=false;
            Present(0);
        }
        void RestoreSearchMode(){Search.refinePhase=!Situations;Search.postureFamily=Situations?0:1;}
    }
}

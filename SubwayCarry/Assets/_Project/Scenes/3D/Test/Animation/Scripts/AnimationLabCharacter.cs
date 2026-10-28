using System;
using MxM;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Manual animation harness, NOT another Journey/Navigation implementation.
    // Its CharacterController is the only root writer in these isolated fields.
    [DefaultExecutionOrder(100)]
    public sealed class AnimationLabCharacter : MonoBehaviour,IMxMTrajectory
    {
        public enum Pipeline { Original,ConvertedClip,Matching }
        public enum Action { Free,Approach,Enter,Seated,Lean,Exit }
        public AnimationLabLibrary library;
        public Animator animator;
        public CharacterController motor;
        public AnimationLabCamera orbit;
        public AnimationLabContacts contacts;
        public AnimationLabAnchor[] anchors;
        public LayerMask environment=1;
        public string field="Walk";
        public bool Automated {get;set;}
        public bool Frozen {get;private set;}
        public bool Ready {get;private set;}
        public Vector3 ActualVelocity {get;private set;}
        public Vector3 DesiredVelocity {get;private set;}
        public Vector3 AvoidanceVelocity=>DesiredVelocity; // no crowd in a manual field
        public Action State {get;private set;}
        public Pipeline Mode {get;private set;}
        public AnimationLabAnchor Anchor {get;private set;}
        public float ContactWeight {get;private set;}
        public string Output {get;private set;}="초기화";
        public string Reason {get;private set;}="";
        public float PoseCost {get;private set;}
        public float ClipTime {get;private set;}
        public string ClipName {get;private set;}="";
        public int ConvertedIndex {get;private set;}
        public Metro3DMotionPlayer MatchingPlayer=>matching;
        public bool ExternalReview {get;set;}
        public AnimationLabFreeWalkPlayer FreeWalk {get;private set;}
        public AnimationLabScenario Scenario {get;set;}
        AnimationLabFreeWalkData freeWalkData;
        bool freeContactEnabled,freeContactCorrection;
        public bool FreeWalkAuto {get;set;}
        public bool FreePoseReview {get;set;}
        public float WalkSpeed=>FreeWalk!=null?FreeWalk.ReferenceSpeed:library.walkSpeed;
        public bool EnableFreeWalk(AnimationLabFreeWalkData data)
        {
            if(field!="Walk"||data==null||!data.Valid)return false;
            freeWalkData=data;freeContactEnabled=contacts.enabled;freeContactCorrection=contacts.correction;
            contacts.Clear();contacts.correction=false;contacts.enabled=false;
            SetPipeline(Pipeline.Original);return FreeWalk!=null&&FreeWalk.Ready;
        }
        public void DisableFreeWalk()
        {
            if(freeWalkData==null)return;
            freeWalkData=null;FreeWalkAuto=false;FreePoseReview=false;
            contacts.Clear();contacts.enabled=freeContactEnabled;contacts.correction=freeContactCorrection;
            SetPipeline(Pipeline.Original);
        }
        public int LegacyStateHash=>legacy.IsValid()?legacy.GetCurrentAnimatorStateInfo(0).shortNameHash:0;
        PlayableGraph graph;
        AnimatorControllerPlayable legacy;
        AnimationClipPlayable single;
        AnimationMixerPlayable mixer;
        Metro3DMotionPlayer matching;
        bool graphReady,pending,detail=true,wantsRun,queuedExit;
        Pipeline requested;
        Vector3 input,velocity,spawn;
        Quaternion spawnRotation;
        float fall,phase,clock,walkRate=1,modeWeight,nextText,poseTime=-1;
        string cachedText="",message="";
        GUIStyle labelStyle;
        Transform[] joints;
        Transform[] frozenBones;
        Vector3[] frozenPositions;
        Quaternion[] frozenRotations;
        Vector3[] previousJoints,jointVelocities;
        TrajectoryPoint[] goal;
        float[] times;
        readonly Vector3[] history=new Vector3[96];
        readonly float[] historyYaw=new float[96],historyTime=new float[96];
        int head,count,poseFrame=-1,graphCreatedFrame=-1;
        static readonly int SpeedParameter=Animator.StringToHash("Speed"),GaitParameter=Animator.StringToHash("Gait");
        public Vector3 PredictionEnd=>goal!=null&&goal.Length>0?animator.transform.position+goal[goal.Length-1].Position:transform.position;
        public float CurrentTime=>clock;
        void Awake()
        {
            if(library==null||animator==null||motor==null){enabled=false;return;}
            spawn=transform.position;spawnRotation=transform.rotation;
            animator.runtimeAnimatorController=library.controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            frozenBones=animator.GetComponentsInChildren<Transform>(true);frozenPositions=new Vector3[frozenBones.Length];frozenRotations=new Quaternion[frozenBones.Length];
            motor.minMoveDistance=0;SetPipeline(Pipeline.Original);
        }
        public void SetInput(Vector3 direction,bool run=false){input=Vector3.ClampMagnitude(direction,1);wantsRun=run;}
        public void SetFrozen(bool value){if(value&&!Frozen)for(int i=0;i<frozenBones.Length;i++){frozenPositions[i]=frozenBones[i].localPosition;frozenRotations[i]=frozenBones[i].localRotation;}Frozen=value;animator.speed=FreePoseReview?1:value?0:1;FreeWalk?.Pause(value);if(graph.IsValid())graph.SetTimeUpdateMode(value?DirectorUpdateMode.Manual:DirectorUpdateMode.GameTime);if(matching!=null){matching.SetGraphPaused(value);if(value)matching.Pause();else matching.UnPause();matching.Legacy.SetSpeed(value?0:1);}}
        void LateUpdate(){if(Frozen&&!FreePoseReview&&(Scenario==null||!Scenario.enabled||Scenario.Situations))for(int i=0;i<frozenBones.Length;i++){frozenBones[i].localPosition=frozenPositions[i];frozenBones[i].localRotation=frozenRotations[i];}}
        public void SetPipeline(Pipeline mode)
        {
            if(field=="Walk"&&mode!=Pipeline.Original)return;
            contacts?.Clear();requested=mode;pending=true;Ready=false;ShutdownGraph();
            if(GetComponent<Metro3DMotionPlayer>()==null)CreateGraph();
        }
        void CreateGraph()
        {
            pending=false;Mode=requested;poseTime=-1;count=head=0;poseFrame=-1;modeWeight=0;graphCreatedFrame=Time.frameCount;
            if(field=="Walk"&&freeWalkData!=null)
            {
                try {FreeWalk=new AnimationLabFreeWalkPlayer(animator,freeWalkData);Ready=true;return;}
                catch(Exception e)
                {
                    freeWalkData=null;FreeWalk=null;
                    contacts.correction=freeContactCorrection;contacts.enabled=freeContactEnabled;
                    Reason="무료 후보 초기화 실패: "+e.Message;
                    Debug.LogWarning(Reason,this); // Keep the existing Generic playback usable.
                }
            }
            animator.Rebind();animator.Update(0);
            if(Mode==Pipeline.Matching&&library.matchingData!=null){
                var data=library.matchingData;
                joints=new Transform[data.MatchBonesGeneric.Length];previousJoints=new Vector3[joints.Length];jointVelocities=new Vector3[joints.Length];
                var bones=animator.GetComponentsInChildren<Transform>(true);
                for(int i=0;i<joints.Length;i++){foreach(var t in bones)if(t.name==data.MatchBonesGeneric[i]){joints[i]=t;break;}if(joints[i]==null){Mode=Pipeline.Original;Reason="Generic 필수 뼈 없음";break;}}
                if(Mode==Pipeline.Matching){matching=gameObject.AddComponent<Metro3DMotionPlayer>();matching.Boot(animator,data);legacy=matching.Legacy;matching.PreparePose();RestoreActionState();matching.PreparePose();Ready=true;return;}
            } else if(Mode==Pipeline.Matching){Mode=Pipeline.Original;Reason="검색 데이터 없음";}
            graph=PlayableGraph.Create("Animation field comparison");graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            legacy=AnimatorControllerPlayable.Create(graph,library.controller);
            mixer=AnimationMixerPlayable.Create(graph,2);graph.Connect(legacy,0,mixer,0);mixer.SetInputWeight(0,1);
            if(Mode==Pipeline.ConvertedClip&&library.converted!=null&&library.converted.Length>0){single=AnimationClipPlayable.Create(graph,library.converted[ConvertedIndex%library.converted.Length]);single.SetApplyFootIK(false);single.SetApplyPlayableIK(false);graph.Connect(single,0,mixer,1);}
            var output=AnimationPlayableOutput.Create(graph,"Retained Generic rig",animator);output.SetSourcePlayable(mixer);graph.Play();graph.Evaluate(0);RestoreActionState();graph.Evaluate(0);graphReady=Ready=true;
        }
        void RestoreActionState(){if(State==Action.Seated)legacy.Play("Seated",0,0);else if(State==Action.Enter)legacy.Play("SitDown",0,Mathf.Clamp01(phase/library.enterSeconds));else if(State==Action.Exit&&Anchor!=null&&Anchor.kind==AnimationLabAnchor.Kind.Seat)legacy.Play("StandUp",0,Mathf.Clamp01(phase/library.exitSeconds));else legacy.Play("Locomotion",0,0);}
        public void SelectConverted(int offset){if(library.converted==null||library.converted.Length==0)return;ConvertedIndex=(ConvertedIndex+offset+library.converted.Length)%library.converted.Length;SetPipeline(Pipeline.ConvertedClip);}
        void Update()
        {
            if(ExternalReview)return;
            if(Scenario!=null&&Scenario.enabled)
            {
                if(!Scenario.Situations)return;
                Scenario.Step(Time.fixedDeltaTime,out velocity,out float yaw);DesiredVelocity=Scenario.Desired;
                transform.rotation=Quaternion.AngleAxis(yaw,Vector3.up)*transform.rotation;
                MoveReviewMotor(velocity,Time.fixedDeltaTime);return;
            }
            if(pending){if(GetComponent<Metro3DMotionPlayer>()==null)CreateGraph();else return;}
            if(!Ready||Time.frameCount==graphCreatedFrame)return;
            if(!Automated&&!ExternalReview&&Keyboard.current!=null){
                var k=Keyboard.current;Vector2 move=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
                var forward=orbit!=null?orbit.FlatForward:Vector3.forward;if(move.sqrMagnitude>0&&FreeWalk!=null)FreeWalkAuto=false;if(!FreeWalkAuto)SetInput(forward*move.y+Vector3.Cross(Vector3.up,forward)*move.x,FreeWalk==null&&(k.leftShiftKey.isPressed||k.rightShiftKey.isPressed));
                if(k.eKey.wasPressedThisFrame)Interact();if(k.f6Key.wasPressedThisFrame)SetFrozen(!Frozen);
                if(Frozen){if(k.hKey.wasPressedThisFrame)detail=!detail;return;}
                if(k.digit1Key.wasPressedThisFrame)SetPipeline(Pipeline.Original);if(field!="Walk"){if(k.digit2Key.wasPressedThisFrame)SetPipeline(Pipeline.ConvertedClip);if(k.digit3Key.wasPressedThisFrame)SetPipeline(Pipeline.Matching);
                if(k.leftBracketKey.wasPressedThisFrame)SelectConverted(-1);if(k.rightBracketKey.wasPressedThisFrame)SelectConverted(1);}
                if(field=="Walk"&&k.bKey.wasPressedThisFrame){WalkCandidate=!WalkCandidate;reviewYaw=0;reviewPivot=false;}
                if(field=="Walk"&&k.vKey.wasPressedThisFrame)WalkAnimationCandidate=!WalkAnimationCandidate;
                if(k.iKey.wasPressedThisFrame&&FreeWalk==null){contacts.correction=!contacts.correction;contacts.Clear();}if(k.hKey.wasPressedThisFrame)detail=!detail;
                if(k.rKey.wasPressedThisFrame)ResetActor();
            }
            if(!Ready||pending||Frozen||Time.frameCount==graphCreatedFrame||!animator.isInitialized)return;
            clock+=Time.deltaTime;
            if(FreeWalk!=null){if(!FreePoseReview)FreeWalk.Tick(ActualVelocity.magnitude,DesiredVelocity.magnitude,Time.deltaTime);Output="무료 Humanoid 후보 · IK OFF · MxM OFF";ClipName=FreeWalk.Clip.name;ClipTime=FreeWalk.Time;return;}
            if(State==Action.Enter||State==Action.Exit){phase+=Time.deltaTime;float duration=State==Action.Enter?library.enterSeconds:Anchor!=null&&Anchor.kind==AnimationLabAnchor.Kind.Wall?.35f:library.exitSeconds;float t=Mathf.Clamp01(phase/duration);ContactWeight=State==Action.Enter?Mathf.SmoothStep(0,1,t):1-Mathf.SmoothStep(0,1,t);if(t>=1){if(State==Action.Enter){State=Action.Seated;legacy.CrossFadeInFixedTime("Seated",.12f);if(queuedExit){queuedExit=false;CancelInteraction("");}}else{State=Action.Free;Anchor=null;legacy.CrossFadeInFixedTime("Locomotion",.12f);contacts.Clear();}}}
            if(State==Action.Lean)ContactWeight=Mathf.MoveTowards(ContactWeight,1,Time.deltaTime*3);
            if(Anchor!=null&&!Anchor.Contact(out _,out _)){CancelInteraction("접촉 표면 소실");}
            float speed=new Vector2(ActualVelocity.x,ActualVelocity.z).magnitude;
            bool activity=State!=Action.Free&&State!=Action.Approach;
            bool run=speed>1.5f;
            float nominal=run?library.runSpeed:library.walkSpeed;
            walkRate=speed>.06f?Mathf.Clamp(speed/Mathf.Max(.1f,nominal),.6f,1.25f):1;
            if(field=="Walk"&&WalkAnimationCandidate&&!activity&&!run){
                filteredWalk=Mathf.MoveTowards(filteredWalk,speed,(speed<.025f?12:4)*Time.deltaTime);
                if(speed<.025f)filteredWalk=0;
                float weight=Mathf.Clamp01(filteredWalk/(library.walkSpeed*.65f));
                walkRate=weight>.001f?Mathf.Clamp(filteredWalk/(library.walkSpeed*weight),.65f,1.25f):1;
                legacy.SetFloat(SpeedParameter,weight*library.walkSpeed);legacy.SetFloat(GaitParameter,walkRate);
            }else{legacy.SetFloat(SpeedParameter,speed);legacy.SetFloat(GaitParameter,walkRate);}
            bool supported=!activity&&!run&&field!="Stairs";
            modeWeight=Mathf.MoveTowards(modeWeight,Mode!=Pipeline.Original&&supported?1:0,Time.deltaTime/.18f);
            if(matching!=null){matching.SetWeight(modeWeight);legacy.SetSpeed(1);if(modeWeight<.001f&&!matching.IsPaused)matching.Pause();else if(supported&&matching.IsPaused)matching.UnPause();if(supported){int id=Mathf.Clamp(matching.CurrentInterpolatedPose.PoseId,0,library.matchingData.Poses.Length-1);float recorded=library.matchingData.Poses[id].LocalVelocity.magnitude*Mathf.Abs(animator.transform.lossyScale.z);matching.SetPlaybackSpeed(recorded>.2f&&speed>.08f?Mathf.Clamp(speed/recorded,.85f,1.15f):1,true);}}
            else if(graphReady){mixer.SetInputWeight(0,1-modeWeight);mixer.SetInputWeight(1,modeWeight);if(single.IsValid()){var clip=library.converted[ConvertedIndex%library.converted.Length];if(single.GetTime()>=clip.length)single.SetTime(0);single.SetSpeed(1);}}
            if(Mode==Pipeline.Original||modeWeight<.01f){Output="CC0 원본 · "+(activity?State.ToString():run?"달리기":speed>.06f?"보행":"대기");Reason=Mode==Pipeline.Original?"":activity?"시설 활동은 원본 클립":run?"달리기 검색 데이터 없음":field=="Stairs"?"계단 전용 데이터 없음":"전환 중";ClipName=activity?(State==Action.Enter?library.sitEnter.name:State==Action.Exit&&Anchor!=null&&Anchor.kind==AnimationLabAnchor.Kind.Seat?library.sitExit.name:State==Action.Seated?library.sitIdle.name:library.idle.name):run?library.run.name:speed>.06f?library.walk.name:library.idle.name;ClipTime=legacy.GetCurrentAnimatorStateInfo(0).normalizedTime;}
            else if(matching!=null){Output="M 검색 출력 (시각 불합격 비교본)";Reason="";var clip=matching.GetChosenClip();ClipName=clip!=null?clip.name:"없음";ClipTime=matching.ChosenAnimationTime;
#if UNITY_EDITOR
                PoseCost=matching.LastChosenCost;
#endif
            } else {Output="변환 클립 단독 · 검색 OFF";Reason="원본/변환 분리 검수";ClipName=library.converted[ConvertedIndex%library.converted.Length].name;ClipTime=(float)single.GetTime();}
            RecordHistory();
            if(clock>=nextText){nextText=clock+.2f;cachedText=$"{field}   {Output}\n{ClipName}  {(modeWeight<.01f?"상태 위상":"재생(s)")} {ClipTime:F2}   실제 {speed:F2}m/s   재생 {walkRate:F2}   접점 {(contacts.correction?"ON":"OFF")}\n{(Anchor!=null?Anchor.label:"")} {Reason} {message}";}
        }
        void FixedUpdate()
        {
            if(!Ready||Frozen||ExternalReview||FreePoseReview||!animator.isInitialized)return;
            if(Scenario!=null&&Scenario.enabled)return;
            Vector3 direction=input;float targetSpeed=wantsRun?library.runSpeed:WalkSpeed;
            if(State==Action.Approach){var delta=Anchor.approach.position-transform.position;delta.y=0;direction=delta.sqrMagnitude>.0025f?delta.normalized:Vector3.zero;targetSpeed=Mathf.Min(.85f,Mathf.Sqrt(2*2.8f*delta.magnitude));if(delta.magnitude<.08f){direction=Vector3.zero;Vector3 facing=Anchor.contact.forward;transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(facing),180*Time.fixedDeltaTime);if(Vector3.Angle(transform.forward,facing)<3){phase=0;ContactWeight=0;State=Anchor.kind==AnimationLabAnchor.Kind.Seat?Action.Enter:Action.Lean;legacy.CrossFadeInFixedTime(State==Action.Enter?"SitDown":"Locomotion",.12f);}}}
            else if(State!=Action.Free){direction=Vector3.zero;targetSpeed=0;}
            DesiredVelocity=direction*targetSpeed;
            if(field=="Walk"&&WalkCandidate&&State==Action.Free){
                velocity=Metro3DWalkMotion.Step(transform.forward,ActualVelocity,DesiredVelocity,125,Time.fixedDeltaTime,ref reviewYaw,ref reviewPivot,out var facing);
                transform.rotation=Quaternion.LookRotation(facing);
                // Native capsule sweep remains the final collision owner; never enable root motion.
                if(velocity.sqrMagnitude>.0001f&&Physics.CapsuleCast(transform.position+Vector3.up*.33f,transform.position+Vector3.up*1.45f,.24f,velocity.normalized,velocity.magnitude*Time.fixedDeltaTime+.025f,environment,QueryTriggerInteraction.Ignore))velocity=Vector3.zero;
            }else{
            if(direction.sqrMagnitude>.001f){float angle=Vector3.Angle(transform.forward,direction);float turnSpeed=180;transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),turnSpeed*Time.fixedDeltaTime);targetSpeed*=Mathf.InverseLerp(100,20,angle);}
            Vector3 desired=direction.sqrMagnitude>.001f?transform.forward*targetSpeed*direction.magnitude:Vector3.zero;
            velocity=transform.forward*Mathf.MoveTowards(velocity.magnitude,desired.magnitude,(desired.magnitude<velocity.magnitude?3.8f:2.3f)*Time.fixedDeltaTime);
            }
            MoveReviewMotor(velocity,Time.fixedDeltaTime);
            if(State==Action.Approach&&clock-phase>10&&ActualVelocity.sqrMagnitude<.0001f){CancelInteraction("접근 막힘");}
        }
        void MoveReviewMotor(Vector3 command,float dt)
        {
            if(motor.isGrounded&&fall<0)fall=-2;else fall=Mathf.Max(-12,fall-9.81f*dt);
            Vector3 before=transform.position;motor.Move((command+Vector3.up*fall)*dt);
            ActualVelocity=Vector3.ProjectOnPlane((transform.position-before)/dt,Vector3.up);
        }
        // Only the isolated Scenario uses this render clock. Legacy movement keeps FixedUpdate.
        public void StepReviewFrame(float dt)
        {
            if(!Ready||Scenario==null||!Scenario.enabled||ExternalReview||FreePoseReview||!animator.isInitialized)return;
            if(Frozen){Scenario.Present(0);return;}
            int steps=Mathf.Max(1,Mathf.CeilToInt(dt/(1f/60)));
            float step=dt/steps;
            for(int i=0;i<steps;i++)
            {
                Scenario.Step(step,out velocity,out float yaw);DesiredVelocity=Scenario.Desired;
                transform.rotation=Quaternion.AngleAxis(yaw,Vector3.up)*transform.rotation;
                MoveReviewMotor(velocity,step);Scenario.Present(step);
            }
        }
        public void ResetReviewMotor(Vector3 position,Quaternion rotation)
        {
            State=Action.Free;Anchor=null;ContactWeight=0;queuedExit=false;contacts.Clear();
            motor.enabled=false;transform.SetPositionAndRotation(position,rotation);motor.enabled=true;
            velocity=ActualVelocity=DesiredVelocity=Vector3.zero;fall=reviewYaw=filteredWalk=0;reviewPivot=false;
        }
        public bool WalkCandidate=true,WalkAnimationCandidate=true;
        float reviewYaw,filteredWalk;
        bool reviewPivot;
        public bool BeginInteraction(AnimationLabAnchor anchor)
        {
            if(State!=Action.Free||anchor==null||!anchor.Contact(out _,out _))return false;
            if(Vector3.Distance(transform.position,anchor.approach.position)>2)return false;
            Anchor=anchor;State=Action.Approach;phase=clock;message="";queuedExit=false;return true;
        }
        public void Interact()
        {
            if(FreeWalk!=null)return; // Walk-only opt-in; no unverified seated/leaning retargeting.
            if(Frozen)return;
            if(State!=Action.Free){CancelInteraction("");return;}
            AnimationLabAnchor best=null;float distance=2;
            foreach(var anchor in anchors){if(anchor==null||!anchor.SurfaceValid)continue;float d=Vector3.Distance(transform.position,anchor.approach.position);if(d<distance){distance=d;best=anchor;}}
            if(!BeginInteraction(best))message="좌석/벽의 접근 표시 가까이에서 E";
        }
        public void CancelInteraction(string why)
        {
            if(FreeWalk!=null){State=Action.Free;Anchor=null;ContactWeight=0;contacts.Clear();return;}
            message=why;
            if(State==Action.Exit)return;
            if(State==Action.Enter){queuedExit=true;return;}
            if(State==Action.Seated){State=Action.Exit;phase=(1-ContactWeight)*library.exitSeconds;legacy.CrossFadeInFixedTime("StandUp",.12f);}
            else if(State==Action.Lean){State=Action.Exit;phase=(1-ContactWeight)*.35f;}
            else {State=Action.Free;Anchor=null;ContactWeight=0;contacts.Clear();legacy.CrossFadeInFixedTime("Locomotion",.12f);}
        }
        public void ResetActor(){CancelInteraction("");State=Action.Free;Anchor=null;ContactWeight=0;queuedExit=false;motor.enabled=false;transform.SetPositionAndRotation(spawn,spawnRotation);motor.enabled=true;velocity=ActualVelocity=DesiredVelocity=Vector3.zero;fall=reviewYaw=filteredWalk=0;reviewPivot=false;contacts.Clear();SetPipeline(Pipeline.Original);}
        public float Support(int side)
        {
            if(FreeWalk!=null)return 0; // No invented contact labels and no IK on the first candidate.
            if(modeWeight>.05f)return 0; // no fabricated CMU support labels
            float speed=ActualVelocity.magnitude;if(speed<.06f)return 1;
            var state=legacy.GetCurrentAnimatorStateInfo(0);float t=state.normalizedTime%1;
            var curve=speed>1.5f?(side==0?library.runLeftSupport:library.runRightSupport):(side==0?library.leftSupport:library.rightSupport);
            return curve!=null?curve.Evaluate(t):0;
        }
        void RecordHistory(){if(count>0&&clock-historyTime[(head+95)%96]<.02f)return;history[head]=animator.transform.position;historyYaw[head]=animator.transform.eulerAngles.y;historyTime[head]=clock;head=(head+1)%96;count=Mathf.Min(count+1,96);}
        public TrajectoryPoint[] GetCurrentGoal()
        {
            if(poseFrame!=Time.frameCount){float dt=poseTime<0?0:clock-poseTime;poseTime=clock;poseFrame=Time.frameCount;for(int i=0;i<joints.Length;i++){var p=joints[i].position;jointVelocities[i]=dt>.0001f?animator.transform.InverseTransformVector((p-previousJoints[i])/dt-ActualVelocity):Vector3.zero;previousJoints[i]=p;}}
            ref var pose=ref matching.CurrentInterpolatedPose;pose.LocalVelocity=animator.transform.InverseTransformVector(ActualVelocity);
            for(int i=0;i<joints.Length;i++)pose.JointsData[i]=new JointData(animator.transform.InverseTransformPoint(joints[i].position),jointVelocities[i]);
            var origin=animator.transform.position;
            for(int i=0;i<times.Length;i++){
                float t=times[i];Vector3 p=origin;float yaw=animator.transform.eulerAngles.y;
                if(t<0){for(int n=0;n<count;n++){int at=(head-1-n+96)%96;p=history[at];yaw=historyYaw[at];if(historyTime[at]<=clock+t)break;}}
                else {Vector3 forward=transform.forward;float speed=ActualVelocity.magnitude;float elapsed=0;while(elapsed<t-.0001f){float dt=Mathf.Min(.1f,t-elapsed);var wanted=DesiredVelocity.sqrMagnitude>.001f?DesiredVelocity.normalized:forward;forward=Vector3.RotateTowards(forward,wanted,Mathf.PI*dt,0);speed=Mathf.MoveTowards(speed,DesiredVelocity.magnitude,dt*(DesiredVelocity.magnitude<speed?3.8f:2.3f));float step=speed*dt;var capsule=transform.position+Vector3.up*.9f+(p-origin);if(Physics.CapsuleCast(capsule+Vector3.up*.55f,capsule-Vector3.up*.55f,.23f,forward,step,environment,QueryTriggerInteraction.Ignore))speed=0;else p+=forward*step;elapsed+=dt;}yaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg+Mathf.DeltaAngle(transform.eulerAngles.y,animator.transform.eulerAngles.y);}
                goal[i]=new TrajectoryPoint(p-origin,yaw);
            }
            return goal;
        }
        public Transform GetTransform()=>animator.transform;
        public void SetGoalRequirements(float[] value){times=value;goal=new TrajectoryPoint[value.Length];}
        public void SetGoal(TrajectoryPoint[] value){Array.Copy(value,goal,Mathf.Min(value.Length,goal.Length));}
        public void CopyGoalFromPose(ref PoseData pose) { }
        public void Pause() { }
        public void UnPause() { }
        public void ResetMotion(float rotation=0){count=head=0;poseTime=-1;poseFrame=-1;}
        public bool HasMovementInput()=>DesiredVelocity.sqrMagnitude>.0025f;
        public bool IsEnabled()=>enabled&&!Frozen;
        void OnGUI()
        {
            if(!detail||Automated||ExternalReview||FreeWalk!=null)return;
            if(labelStyle==null)labelStyle=new GUIStyle(GUI.skin.label){font=library.font,fontSize=14};
            GUI.Box(new Rect(12,Screen.height-137,Mathf.Min(850,Screen.width-24),125),"");
            GUI.Label(new Rect(24,Screen.height-130,820,70),cachedText,labelStyle);
            GUI.Label(new Rect(24,Screen.height-65,820,46),field=="Walk"?$"WASD · 마우스/휠 · B 이동 {(WalkCandidate?"후보":"기존")} · V 애니 연결 {(WalkAnimationCandidate?"후보":"기존")} · I 접지 · F6 정지\nN 수동/NPC · J 재현 조건 · R 재시작 · H 표기 · Esc 커서 (2·3 불합격 출력 비활성)":"WASD 이동 · Shift 달리기 · 마우스 시점/휠 거리 · PgUp/Dn 높이 · E 앉기/기대기/해제\n1 원본 · 2 변환 클립 · [ ] 클립 변경 · 3 M 비교본 · I 접점 보정 · F6 정지 · R 초기화 · H 표기 · Esc 커서",labelStyle);
        }
        void ShutdownGraph(){if(FreeWalk!=null){FreeWalk.Dispose();FreeWalk=null;}if(matching!=null){matching.Shutdown();Destroy(matching);matching=null;}if(graph.IsValid())graph.Destroy();graphReady=false;}
        void OnDisable(){ShutdownGraph();Ready=false;}
        void OnEnable(){if(!Ready&&frozenBones!=null){velocity=ActualVelocity=DesiredVelocity=Vector3.zero;fall=0;SetPipeline(Pipeline.Original);}}
    }
}

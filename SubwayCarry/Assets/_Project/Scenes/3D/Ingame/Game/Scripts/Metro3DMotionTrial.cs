using System;
using System.Text;
using MxM;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SubwayCarry.Gameplay3D
{
    // Opt-in animation experiment. This class never moves/rotates an actor.
    // One shared database, one explicitly selected identity, one playback graph.
    [DefaultExecutionOrder(100)]
    public sealed class Metro3DMotionTrial : MonoBehaviour, IMxMTrajectory
    {
        static Metro3DMotionTrial selected;
        static MxMAnimData shared;
        static Metro3DWalkSampleData sharedSample;
        MxMAnimData data;
        Metro3DWalkSampleData sample;
        bool walkSample,reviewFrozen,restoreMotor,previousMotor;
        int directClip=-1;
        readonly Vector3[] predictionPath=new Vector3[384];
        readonly Vector3[] predictedActual=new Vector3[128];
        readonly float[] predictedAt=new float[128];
        int predictionHead,predictionCount;
        public bool WalkSample=>walkSample;
        public Metro3DWalkSampleData SampleData=>sample;
        public static Metro3DWalkSampleData LoadWalkData()=>Resources.Load<Metro3DWalkSampleData>("PassengerWalkDataInfo")??Resources.Load<Metro3DWalkSampleData>("PassengerWalkSampleInfo");
        public float FallbackSeconds {get;private set;}
        public float MatchingSeconds {get;private set;}
        public int FallbackEntries {get;private set;}
        int eligiblePoses,excludedPoses;
        static readonly System.Collections.Generic.HashSet<MxMAnimData> checkedData=new System.Collections.Generic.HashSet<MxMAnimData>();
        public int DirectClipIndex=>directClip;
        public float PredictionError {get;private set;}
        public int PredictionMeasurements {get;private set;}
        public float SampleLoopSpeed=>sample!=null?sample.loopSpeed*Mathf.Abs(animator.transform.lossyScale.z):0;
        float MinimumPace=>sample.minimumPace*Mathf.Abs(animator.transform.lossyScale.z);
        float MaximumPace=>sample.maximumPace*Mathf.Abs(animator.transform.lossyScale.z);
        public System.Action BeforePoseQuery;
        public string Context {get;private set;}="";
        public float Contact(int foot)=>sample!=null&&player!=null?sample.Contact(directClip>=0?sample.clips[directClip].animation:player.GetChosenClip(),directClip>=0?player.DirectTime:player.ChosenAnimationTime,foot):0;
        public Metro3DPassenger Actor {get;private set;}
        public Guid Identity {get;private set;}
        public bool Matching => player!=null&&player.Ready&&wasSupported&&weight>=.999f&&directClip<0;
        public string FallbackReason {get;private set;}="초기화 대기";
        public int PoseChanges {get;private set;}
        public int GoalQueries {get;private set;}
        public float QueryMilliseconds {get;private set;}
        public long QueryAllocatedBytes {get;private set;}
        public double InitializeMilliseconds {get;private set;}
        public long InitializeAllocatedBytes {get;private set;}
        public string TransitionReason {get;private set;}="초기화";
        public float MatchingWeight=>weight;
        public Metro3DMotionPlayer Player=>player;
        public Vector3 PredictedEndpoint=>goal!=null&&goal.Length>0?animator.transform.position+goal[goal.Length-1].Position:transform.position;
        Metro3DMotionPlayer player;
        Animator animator;
        AnimatorCullingMode previousCulling;
        bool changedCulling;
        Transform frame;
        Metro3DCrowd crowd;
        Transform[] joints;
        Vector3[] previousJoints,jointVelocities;
        Vector3 previousRoot;
        float[] predictionTimes;
        TrajectoryPoint[] goal;
        Vector3[] localGoal;
        float[] localGoalAngles;
        float lastPoseTime=-1,lastPrediction=-1,weight;
        int poseFrame=-1;
        readonly Vector3[] historyPositions=new Vector3[96];
        readonly float[] historyAngles=new float[96],historyTimes=new float[96];
        int historyHead,historyCount;
        bool paused,shuttingDown,pendingInitialize,wasSupported;
        int previousClip=-1;
        public static Metro3DMotionTrial Toggle(Metro3DPassenger actor)
        {
            if(actor==null||actor.PersistentId==Guid.Empty)return null;
            bool same=selected!=null&&selected.Actor==actor&&selected.Identity==actor.PersistentId;
            if(selected!=null)selected.Release();
            if(same)return null;
            return StartWalkSample(actor);
        }
        public static Metro3DMotionTrial StartWalkSample(Metro3DPassenger actor,bool exclusive=true)
        {
            if(actor==null||actor.PersistentId==Guid.Empty)return null;
            if(exclusive&&selected!=null)selected.Release();
            if(actor.MotionTrial!=null)actor.MotionTrial.Release();
            var trial=actor.gameObject.AddComponent<Metro3DMotionTrial>();if(exclusive)selected=trial;
            trial.walkSample=true;trial.Actor=actor;trial.Identity=actor.PersistentId;actor.MotionTrial=trial;
            if(!actor.WalkReviewControlled){trial.restoreMotor=true;trial.previousMotor=actor.WalkReviewCandidate;}
            trial.InitializeTrial();return trial;
        }
        public void SetDirect(int index)
        {
            if(!walkSample||sample==null||player==null)return;
            directClip=index<0?-1:(index+sample.clips.Length)%sample.clips.Length;
            player.SetDirectClip(directClip<0?null:sample.clips[directClip].animation);lastPrediction=-1;
        }
        public void FreezeReview(bool value)
        {
            if(player==null)return;reviewFrozen=value;player.SetGraphPaused(value);
            if(value)player.Pause();else{player.UnPause();ResetMotion();}
        }
        void InitializeTrial()
        {
            long initializeStarted=System.Diagnostics.Stopwatch.GetTimestamp(),initializeAllocated=GC.GetAllocatedBytesForCurrentThread();
            try{
            // Unity's DisallowMultipleComponent remains in effect until deferred
            // destruction finishes. A same-frame reselect waits instead of
            // reusing another passenger's graph or adding a duplicate player.
            if(GetComponent<Metro3DMotionPlayer>()!=null){pendingInitialize=true;FallbackReason="이전 재생기 정리 대기";return;}
            pendingInitialize=false;
            animator=Actor.GetComponentInChildren<Animator>(true);
            if(walkSample){if(sharedSample==null)sharedSample=LoadWalkData();sample=sharedSample;data=sample!=null?sample.database:null;}
            else{if(shared==null)shared=Resources.Load<MxMAnimData>("PassengerMotionTrial");data=shared;}
            if(data==null||data.Poses==null||data.Poses.Length<2){FallbackReason=walkSample?"Walk MM 데이터 없음 (출처·설치 확인)":"동작 데이터 없음";return;}
            eligiblePoses=excludedPoses=0;
            foreach(var pose in data.Poses){if((pose.Tags&ETags.DoNotUse)!=0){excludedPoses++;continue;}if(pose.AnimType!=EMxMAnimtype.IdleSet)eligiblePoses++;}
            if(eligiblePoses==0){FallbackReason="유효한 이동 후보 없음";return;}
            if(!checkedData.Contains(data)){
                foreach(var pose in data.Poses){if((pose.Tags&ETags.DoNotUse)!=0)continue;
                    if(!float.IsFinite(pose.LocalVelocity.sqrMagnitude)) {FallbackReason="데이터 속도 NaN/Infinity";return;}
                    foreach(var point in pose.Trajectory)if(!float.IsFinite(point.Position.sqrMagnitude)||!float.IsFinite(point.FacingAngle)){FallbackReason="데이터 궤적 NaN/Infinity";return;}
                    foreach(var joint in pose.JointsData)if(!float.IsFinite(joint.Position.sqrMagnitude)||!float.IsFinite(joint.Velocity.sqrMagnitude)){FallbackReason="데이터 자세 NaN/Infinity";return;}
                }checkedData.Add(data);
            }
            if(animator==null||animator.avatar==null||animator.isHuman){FallbackReason="검증한 Generic 골격 아님";return;}
            joints=new Transform[data.MatchBonesGeneric.Length];previousJoints=new Vector3[joints.Length];jointVelocities=new Vector3[joints.Length];
            var bones=animator.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<joints.Length;i++) {
                for(int n=0;n<bones.Length;n++)if(bones[n].name==data.MatchBonesGeneric[i]){joints[i]=bones[n];break;}
                if(joints[i]==null){FallbackReason="필수 골격 없음: "+data.MatchBonesGeneric[i];return;}
            }
            frame=Actor.transform.parent;crowd=Actor.world.GetComponent<Metro3DCrowd>();
            try {
                previousCulling=animator.cullingMode;changedCulling=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                player=gameObject.AddComponent<Metro3DMotionPlayer>();
                player.Boot(animator,data);
                player.OnPoseChange.AddListener(PoseChanged);
                if(restoreMotor)Actor.WalkReviewCandidate=true;
                FallbackReason="";
            } catch(Exception e){FallbackReason="초기화 실패: "+e.GetType().Name;DisposePlayer();Debug.LogException(e,this);}
            }finally{InitializeMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-initializeStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency;InitializeAllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-initializeAllocated;}
        }
        void PoseChanged(MxMAnimator.PoseChangeData change)
        {
            PoseChanges++;
            int clip=player.ChosenPose.PrimaryClipId;
            TransitionReason=clip==previousClip?"동일 클립의 다른 자세로 전환":"자세·궤적 검색 클립 전환";
            previousClip=clip;
        }
        void Update()
        {
            if(Actor==null||Actor.PersistentId!=Identity){Release();return;}
            if(pendingInitialize){if(GetComponent<Metro3DMotionPlayer>()==null)InitializeTrial();else return;}
            if(player==null||!player.Ready){FallbackSeconds+=Time.deltaTime;return;}
            if(reviewFrozen)return;
            // Rebase history when entering another pedestrian frame. No stale
            // station coordinates or carriage motion become locomotion features.
            if(frame!=transform.parent){frame=transform.parent;ResetMotion();}
            RecordHistory();
            bool supported=Actor.MotionMatchingLocomotion;
            Context=supported?"Locomotion":"ActionClip";
            string missing="";
            if(walkSample){
                float speed=Actor.Velocity.magnitude,cruise=SampleLoopSpeed;
                if(!Actor.WalkReviewCandidate){supported=false;Context="UnvalidatedMotor";missing="기존 Motor 비교: 새 예측 연결은 개선 Motor 전용";}
                else if(!supported)missing=Actor.Journey.OnStairs?"미확보: 계단 원본 / 기존 실행":Actor.Journey.GateAction==Metro3DPassengerJourney.GatePhase.Tag?"미확보: 태그 원본 / 기존 실행":"의도된 ActionClip: 착석·기립";
                else if(Actor.PortalCommitted||Actor.QueueCommitted){supported=false;Context="FacilityAlignment";missing="정밀 시설 정렬·줄 이동: 기존 실행 / 전용 데이터 미확보";}
                else if(Actor.WalkPhase==Metro3DWalkPhase.Pivoting||Mathf.Abs(Actor.RotationDegreesPerSecond)>(sample.supportsTurns?sample.maximumTurnRate:22)){supported=false;Context="UnsupportedTurn";missing=sample.supportsTurns?"범위 밖: 전용 제자리 피벗/기록 회전 속도 초과":"미확보: 선회·피벗 원본";}
                else if((Actor.DesiredVelocity.magnitude>.25f&&!Actor.FinalWalkTarget&&Actor.DesiredVelocity.magnitude<(sample.supportsTurns?MinimumPace:cruise*.85f))||(speed>.25f&&(Actor.WalkPhase==Metro3DWalkPhase.Walking||Actor.WalkPhase==Metro3DWalkPhase.Curving)&&Mathf.Abs(speed-Actor.DesiredVelocity.magnitude)<.08f&&speed<(sample.supportsTurns?MinimumPace:cruise*.85f))){supported=false;Context="UnsupportedPace";missing="범위 밖: 현재 속도의 보행 원본";}
                else if(speed>(sample.supportsTurns?MaximumPace:cruise*1.15f)){supported=false;Context="UnsupportedPace";missing="범위 밖: 빠른 보행/달리기 원본";}
            }
            if(!supported&&wasSupported)FallbackEntries++;
            if(supported&&!wasSupported){historyCount=historyHead=0;poseFrame=-1;lastPoseTime=lastPrediction=-1;}
            wasSupported=supported;
            FallbackReason=supported?"":walkSample?missing:"기존 활동: 착석·기립·계단·시설 제스처";
            weight=Mathf.MoveTowards(weight,supported?1:0,Time.deltaTime/.2f);
            player.SetWeight(weight);
            if(directClip<0){if(supported&&weight>=.999f)MatchingSeconds+=Time.deltaTime;else if(!supported)FallbackSeconds+=Time.deltaTime;}
            if(directClip>=0)player.SetDirectWeight(true);else player.SetDirectWeight(false);
            player.SyncLegacy(animator.GetFloat("Speed"),animator.GetFloat("Gait"),Actor.IsSeated);
            bool pauseSearch=weight<.001f||directClip>=0;
            if(pauseSearch&&!player.IsPaused)player.Pause();
            else if(!pauseSearch&&supported&&player.IsPaused)player.UnPause();
            // Playback rate correction is bounded; absent motion is not invented
            // through root rotation/translation warping.
            int poseId=Mathf.Clamp(player.CurrentInterpolatedPose.PoseId,0,player.CurrentAnimData.Poses.Length-1);
            float recorded=player.CurrentAnimData.Poses[poseId].LocalVelocity.magnitude*Mathf.Abs(animator.transform.lossyScale.z);
            float actual=Actor.Velocity.magnitude;
            float rate=recorded>.2f&&actual>.08f?Mathf.Clamp(actual/recorded,.85f,1.15f):1;
            player.SetPlaybackSpeed(rate,true);
            if(directClip>=0)player.AdvanceDirect(Time.deltaTime);
            if(walkSample){float l=Contact(0),r=Contact(1);player.SetFavourTags((ETags)(l>.65f&&r<.35f?1:r>.65f&&l<.35f?2:0),.9f);}
        }
        void RecordHistory()
        {
            float now=Time.time;
            if(historyCount>0&&now-historyTimes[(historyHead+95)%96]<.02f)return;
            historyPositions[historyHead]=frame.InverseTransformPoint(animator.transform.position);
            historyAngles[historyHead]=Mathf.DeltaAngle(frame.eulerAngles.y,animator.transform.eulerAngles.y);
            historyTimes[historyHead]=now;historyHead=(historyHead+1)%96;historyCount=Mathf.Min(96,historyCount+1);
        }
        void MeasurePose()
        {
            if(poseFrame==Time.frameCount)return;
            poseFrame=Time.frameCount;
            float dt=lastPoseTime<0?0:Time.time-lastPoseTime;
            Vector3 root=frame.InverseTransformPoint(animator.transform.position);
            // FixedUpdate may run zero or multiple times before this rendered
            // pose sample. Subtract root displacement measured over THESE same
            // two samples, not the last fixed-step velocity over another dt.
            Vector3 measuredRoot=dt>.0001f?frame.TransformVector((root-previousRoot)/dt):Vector3.zero;
            for(int i=0;i<joints.Length;i++) {
                var local=frame.InverseTransformPoint(joints[i].position);
                jointVelocities[i]=dt>.0001f?animator.transform.InverseTransformVector(frame.TransformVector((local-previousJoints[i])/dt)-measuredRoot):Vector3.zero;
                previousJoints[i]=local;
            }
            previousRoot=root;lastPoseTime=Time.time;
        }
        public TrajectoryPoint[] GetCurrentGoal()
        {
            long ticks=System.Diagnostics.Stopwatch.GetTimestamp(),allocated=GC.GetAllocatedBytesForCurrentThread();
            BeforePoseQuery?.Invoke();
            MeasurePose();
            // Called by MxM after its interpolated-pose update and before its
            // search jobs. Replace query features with the actually played pose.
            ref PoseData pose=ref player.CurrentInterpolatedPose;
            pose.LocalVelocity=animator.transform.InverseTransformVector(Actor.Velocity);
            for(int i=0;i<joints.Length;i++)pose.JointsData[i]=new JointData(animator.transform.InverseTransformPoint(joints[i].position),jointVelocities[i]);
            if(Time.time-lastPrediction>=.05f||lastPrediction<0){lastPrediction=Time.time;Predict();for(int i=0;i<goal.Length;i++){localGoal[i]=frame.InverseTransformPoint(goal[i].Position);localGoalAngles[i]=Mathf.DeltaAngle(frame.eulerAngles.y,goal[i].FacingAngle);}}
            // MxM's IMxMTrajectory contract is a world-oriented displacement,
            // NOT an absolute world position (Goal.SetRelativeTo uses a vector).
            for(int i=0;i<goal.Length;i++)goal[i]=new TrajectoryPoint(frame.TransformPoint(localGoal[i])-animator.transform.position,frame.eulerAngles.y+localGoalAngles[i]);
            GoalQueries++;
            QueryMilliseconds+=(float)((System.Diagnostics.Stopwatch.GetTimestamp()-ticks)*1000.0/System.Diagnostics.Stopwatch.Frequency);
            QueryAllocatedBytes+=GC.GetAllocatedBytesForCurrentThread()-allocated;
            return goal;
        }
        void Predict()
        {
            if(walkSample&&Actor.WalkReviewCandidate){PredictWalk();return;}
            Vector3 origin=animator.transform.position,position=Actor.transform.position,velocity=Actor.Velocity;
            Vector3 forward=Actor.transform.forward;float elapsed=0,travel=0;
            float frameYaw=frame.eulerAngles.y,axisOffset=Mathf.DeltaAngle(Actor.transform.eulerAngles.y,animator.transform.eulerAngles.y);
            for(int i=0;i<predictionTimes.Length;i++) {
                float time=predictionTimes[i];
                if(time<0) {
                    Vector3 historical=frame.InverseTransformPoint(origin);float angle=Mathf.DeltaAngle(frameYaw,animator.transform.eulerAngles.y);
                    for(int n=0;n<historyCount;n++){int at=(historyHead-1-n+96)%96;historical=historyPositions[at];angle=historyAngles[at];if(historyTimes[at]<=Time.time+time)break;}
                    goal[i]=new TrajectoryPoint(frame.TransformPoint(historical),frameYaw+angle);continue;
                }
                while(elapsed<time-.0001f) {
                    float dt=Mathf.Min(.05f,time-elapsed);
                    Vector3 point=Actor.MotionPathPoint(travel+.45f),toward=point-position;toward.y=0;
                    bool wants=Actor.MovementIntent.sqrMagnitude>.01f&&Actor.DesiredVelocity.sqrMagnitude>.0025f;
                    float speed=wants?Actor.AvoidanceVelocity.magnitude:0;
                    Vector3 direction=toward.sqrMagnitude>.0025f?toward.normalized:forward;
                    if((Actor.PortalCommitted||!wants)&&Actor.BodyFacingIntent.sqrMagnitude>.01f)direction=Actor.BodyFacingIntent.normalized;
                    forward=Vector3.RotateTowards(forward,direction,Actor.ComfortableTurnRate*Mathf.Deg2Rad*dt,0);
                    float nextSpeed=Mathf.MoveTowards(velocity.magnitude,speed,dt*(speed<velocity.magnitude?3.8f:2.3f));
                    Vector3 next=position+forward*nextSpeed*dt;
                    if(!crowd.CanFollowWalkingSegment(Actor,next)){next=position;nextSpeed=0;}
                    travel+=Vector3.Distance(next,position);position=next;velocity=forward*nextSpeed;elapsed+=dt;
                }
                goal[i]=new TrajectoryPoint(origin+(position-Actor.transform.position),Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg+axisOffset);
            }
        }
        void PredictWalk()
        {
            Vector3 origin=animator.transform.position,position=Actor.transform.position,velocity=Actor.Velocity,forward=Actor.transform.forward;
            float elapsed=0,yawRate=Actor.MotionYawRate,axisOffset=Mathf.DeltaAngle(Actor.transform.eulerAngles.y,animator.transform.eulerAngles.y);bool pivot=Actor.MotionPivot;
            int count=Actor.CopyMotionPath(predictionPath),at=0;
            for(int i=0;i<predictionTimes.Length;i++){
                float time=predictionTimes[i];
                if(time<0){Vector3 p=frame.InverseTransformPoint(origin);float angle=Mathf.DeltaAngle(frame.eulerAngles.y,animator.transform.eulerAngles.y);
                    for(int n=0;n<historyCount;n++){int h=(historyHead-1-n+96)%96;p=historyPositions[h];angle=historyAngles[h];if(historyTimes[h]<=Time.time+time)break;}
                    goal[i]=new TrajectoryPoint(frame.TransformPoint(p),frame.eulerAngles.y+angle);continue;}
                while(elapsed<time-.0001f){
                    float dt=Mathf.Min(Time.fixedDeltaTime,time-elapsed);
                    while(at<count-1&&Vector3.ProjectOnPlane(predictionPath[at]-position,Vector3.up).magnitude<.25f)at++;
                    Vector3 toward=count>0?predictionPath[at]-position:Vector3.zero;toward.y=0;
                    float speed=Actor.AvoidanceVelocity.magnitude;
                    if(count==0||Actor.DesiredVelocity.sqrMagnitude<.0025f)speed=0;
                    if(at==count-1)speed=Mathf.Min(speed,Metro3DWalkMotion.ArrivalSpeed(toward.magnitude));
                    // Keep measured avoidance's lateral correction separate from
                    // the path tangent. It is never a forced forward translation.
                    var correction=Actor.AvoidanceVelocity-Actor.DesiredVelocity;correction.y=0;
                    Vector3 wanted=toward.sqrMagnitude>.0001f?toward.normalized*speed+correction:Vector3.zero;
                    if(wanted.magnitude>speed&&speed>.01f)wanted=wanted.normalized*speed;
                    var step=Metro3DWalkMotion.Step(forward,velocity,wanted,Actor.ComfortableTurnRate,dt,ref yawRate,ref pivot,out var face);
                    Vector3 next=position+step*dt;
                    if(step.sqrMagnitude>.0001f&&!crowd.CanShortenWalkingPath(Actor,position,next)){next=position;step=Vector3.zero;}
                    position=next;velocity=step;forward=face;elapsed+=dt;
                }
                goal[i]=new TrajectoryPoint(origin+position-Actor.transform.position,Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg+axisOffset);
            }
            // Compare the .2s prediction with a later measured location in the
            // same station/carriage frame. Parent rebase clears the queue.
            while(predictionCount>0){int first=(predictionHead-predictionCount+128)%128;if(predictedAt[first]>Time.time)break;
                if(Time.time-predictedAt[first]<.05f){PredictionError+=Vector3.Distance(frame.InverseTransformPoint(Actor.transform.position),predictedActual[first]);PredictionMeasurements++;}predictionCount--;}
            float horizon=sample!=null&&sample.supportsTurns?.35f:.2f;int future=-1;
            for(int i=0;i<predictionTimes.Length;i++)if(Mathf.Abs(predictionTimes[i]-horizon)<.001f){future=i;break;}
            if(future>=0){predictedActual[predictionHead]=frame.InverseTransformPoint(goal[future].Position-origin+Actor.transform.position);predictedAt[predictionHead]=Time.time+predictionTimes[future];predictionHead=(predictionHead+1)%128;predictionCount=Mathf.Min(128,predictionCount+1);}
        }
        public void CrossFadeLegacy(string state,float seconds){if(player!=null&&player.Ready)player.Legacy.CrossFadeInFixedTime(state,seconds);}
        public Transform GetTransform()=>animator!=null?animator.transform:transform;
        public void SetGoalRequirements(float[] times){predictionTimes=times;goal=new TrajectoryPoint[times.Length];localGoal=new Vector3[times.Length];localGoalAngles=new float[times.Length];for(int i=0;i<goal.Length;i++)goal[i]=new TrajectoryPoint(Vector3.zero,animator.transform.eulerAngles.y);}
        public void SetGoal(TrajectoryPoint[] value){if(goal!=null)Array.Copy(value,goal,Mathf.Min(value.Length,goal.Length));}
        public void CopyGoalFromPose(ref PoseData pose) {lastPrediction=-1;}
        public void Pause(){paused=true;}
        public void UnPause(){paused=false;lastPrediction=-1;}
        public void ResetMotion(float rotation=0){historyCount=historyHead=predictionCount=predictionHead=0;lastPoseTime=lastPrediction=-1;poseFrame=-1;}
        public bool HasMovementInput()=>Actor!=null&&(Actor.DesiredVelocity.sqrMagnitude>.0025f||Actor.WalkPhase==Metro3DWalkPhase.Pivoting||Actor.BodyFacingIntent.sqrMagnitude>.01f&&Vector3.Angle(Actor.transform.forward,Actor.BodyFacingIntent)>5);
        public bool IsEnabled()=>!shuttingDown&&!paused&&isActiveAndEnabled;
        public int CopyPrediction(Vector3[] output){if(goal==null||lastPrediction<0||frame==null||output.Length==0)return 0;int n=0;output[n++]=animator.transform.position;for(int i=0;i<predictionTimes.Length&&n<output.Length;i++)if(predictionTimes[i]>0)output[n++]=frame.TransformPoint(localGoal[i]);return n;}
        public int CopyHistory(Vector3[] output){if(frame==null)return 0;int n=Mathf.Min(historyCount,output.Length);for(int i=0;i<n;i++)output[i]=frame.TransformPoint(historyPositions[(historyHead-n+i+96)%96]);return n;}
        public void AppendDetails(StringBuilder text)
        {
            text.Append("\nWalk 모션 매칭 (사용자 검수 전): ").Append(sample==null?"데이터 미로드":!string.IsNullOrEmpty(sample.sourceName)?sample.sourceName:"Kubold LITE 직선 샘플").Append(' ').Append(directClip>=0?"단독 출력":Matching?"검색 활성":"기존 방식/전환").Append(' ').Append(FallbackReason);
            text.Append("\nMM 출력 ").Append(MatchingSeconds.ToString("F1")).Append("s / 폴백 ").Append(FallbackSeconds.ToString("F1")).Append("s · 진입 ").Append(FallbackEntries);
            if(player==null||!player.Ready)return;
            text.Append("\n후보(데이터 기준) ").Append(eligiblePoses).Append(" / 경계·태그 제외 ").Append(excludedPoses).Append(" / 검색 간격 ").Append(player.UpdateInterval.ToString("F2")).Append("s");
            if(sample!=null&&!string.IsNullOrEmpty(sample.buildSignature))text.Append(" / 빌드 ").Append(sample.buildSignature,0,Mathf.Min(8,sample.buildSignature.Length));
            if(directClip>=0){text.Append("\n리타기팅 단독 재생 / 검색 OFF: ").Append(sample.clips[directClip].animation.name).Append(" @ ").Append(player.DirectTime.ToString("F2"));return;}
            if(weight<.001f){text.Append("\n실제 출력: Legacy / ").Append(Actor.ActivityLabel).Append(walkSample?" (신규 샘플 미지원 범위) / "+Context:" (이전 CMU 후보는 재생 중 아님)");return;}
            if(weight<.999f)text.Append("\n실제 출력: Legacy + M 블렌드 / M ").Append((weight*100).ToString("F0")).Append('%');
            else text.Append("\n실제 출력: M 검색");
            var clip=player.GetChosenClip();text.Append("\n클립 ").Append(clip!=null?clip.name:"없음").Append(" @ ").Append(player.ChosenAnimationTime.ToString("F2")).Append("s / 비용 ");
#if UNITY_EDITOR
            text.Append(player.LastChosenCost.ToString("F3"));
#else
            text.Append("에디터에서 측정");
#endif
            text.Append(" / ").Append(player.TimeSinceMotionChosen>.2f?"이어 재생 (현재 자세 우대)":TransitionReason).Append("\n예측 끝 ").Append(PredictedEndpoint.ToString("F2")).Append(" / 재생 ").Append(player.PlaybackSpeed.ToString("F2"));
#if UNITY_EDITOR
            if(walkSample)text.Append(" / 자세 ").Append(player.LastSearchPoseCost.ToString("F2")).Append(" · 궤적 ").Append(player.LastSearchTrajectoryCost.ToString("F2")).Append(" (최근 검색)");
#endif
            if(walkSample)text.Append("\nDB ").Append(data.name).Append(" / 자세 ").Append(player.CurrentInterpolatedPose.PoseId).Append(" / ").Append(Context).Append("\n").Append(sample.supportsTurns?sample.coverage:"Idle/직선/출발/정지 L·R · 선회/느린 걷기 미확보").Append("\n지지 추정 L/R ").Append(Contact(0).ToString("F2")).Append('/').Append(Contact(1).ToString("F2")).Append(" · ").Append(sample.supportsTurns?".35s":".2s").Append(" 예측 오차 ").Append((PredictionMeasurements>0?PredictionError/PredictionMeasurements:0).ToString("F3")).Append("m / MM ").Append(MatchingSeconds.ToString("F1")).Append("s · 폴백 ").Append(FallbackSeconds.ToString("F1")).Append("s (").Append(FallbackEntries).Append(") / 질의 ").Append(GoalQueries).Append(" · ").Append(QueryMilliseconds.ToString("F2")).Append("ms");
            else text.Append("\n데이터: CMU 보행·선회·피벗 시험 / 양 지지발 출발·정지 분류·발 접지 보정 미완료");
        }
        public void Release()
        {
            if(shuttingDown)return;shuttingDown=true;enabled=false;
            if(selected==this)selected=null;
            if(Actor!=null&&Actor.MotionTrial==this)Actor.MotionTrial=null;
            if(restoreMotor&&Actor!=null&&Actor.PersistentId==Identity)Actor.WalkReviewCandidate=previousMotor;
            DisposePlayer();Destroy(this);
        }
        void DisposePlayer()
        {
            AnimatorStateInfo legacy=default;bool restore=false;
            bool touched=changedCulling||player!=null;
            float speed=animator!=null?animator.GetFloat("Speed"):0,gait=animator!=null?animator.GetFloat("Gait"):1;
            if(player!=null){if(player.Ready){legacy=player.Legacy.IsInTransition(0)?player.Legacy.GetNextAnimatorStateInfo(0):player.Legacy.GetCurrentAnimatorStateInfo(0);restore=legacy.fullPathHash!=0;}player.Shutdown();Destroy(player);player=null;}
            if(animator!=null&&touched){animator.applyRootMotion=false;if(changedCulling){animator.cullingMode=previousCulling;changedCulling=false;}if(animator.isActiveAndEnabled){animator.Rebind();animator.SetFloat("Speed",speed);animator.SetFloat("Gait",gait);animator.SetBool("Seated",Actor!=null&&Actor.IsSeated);if(restore)animator.Play(legacy.fullPathHash,0,legacy.normalizedTime);else animator.Play(Actor!=null&&Actor.IsSeated?"Seated":"Locomotion");}}
        }
        void OnDisable(){if(!shuttingDown)Release();}
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using System.Text;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Walk-only fixture adapter. Production grid, path queue, Crowd, Motor and
    // Animator execute unchanged or explicitly opt into the review candidate.
    [DefaultExecutionOrder(200)]
    public sealed class AnimationLabWalkReview : MonoBehaviour
    {
        public AnimationLabCharacter manual;
        public Metro3DSettings sourceSettings;
        public Transform geometry;
        public Transform stairEntry,stairExit,stairAfter;
        public GameObject cornerWall;
        public bool Automated {get;set;}
        public Metro3DPassenger Passenger {get;private set;}
        public bool NpcMode {get;private set;}
        public int Case {get;private set;}
        public Metro3DCrowd Crowd=>crowd;
        public System.Action<Metro3DPassenger,float> MotorSample;
        public static readonly string[] Cases={"긴 직선","왼쪽 대각선","오른쪽 대각선","촘촘한 열린 경로","왼쪽 코너","오른쪽 코너","계단 대각선 진입","계단 하행 연결","큰 반전","30도 선회","45도 선회","회전된 배치","회전된 계단 진입","회전된 계단 하행","소규모 교행","후행 승객"};
        Metro3DPassenger companion;
        Metro3DWorld world;
        Metro3DCrowd crowd;
        Transform station;
        Animator animation;
        SkinnedMeshRenderer[] manualSkins;
        Vector3[] raw=new Vector3[384];
        readonly Vector3[] motionLine=new Vector3[96];
        int rawCount;
        float nextText;
        bool candidate=true,animationCandidate=true,detail=true;
        bool sampleEnabled,samplePace;
        bool frozen;
        int direct=-1;
        bool showSource;
        bool sourceRawRoot;
        public bool freeCandidateEnabled=true;
        public bool freePoseReview=true;
        AnimationLabFreeWalkData freeData;
        Animator sourcePoseAnimator;
        Vector3 sourcePosePosition;
        Quaternion sourcePoseRotation;
        float sourcePoseSeconds;
        Transform comparisonFocus;
        Material sourcePoseMaterial;
        Material sourceSkinMaterial;
        Renderer[] sourceRenderers;
        Transform[] sourceBaseBones;
        Vector3[] sourceBasePositions,sourceBaseScales;
        Quaternion[] sourceBaseRotations;
        bool sourceVisible=true,editPreviousFrozen;
        AnimationLabPoseProfile editProfile;
        readonly Transform[] poseJoints=new Transform[AnimationLabPoseProfile.Bones.Length];
        readonly Quaternion[] baseJointRotations=new Quaternion[AnimationLabPoseProfile.Bones.Length];
        Avatar cachedPoseAvatar;
        float editSeconds;
        public bool PoseEditing {get;private set;}
        public AnimationLabPoseProfile.Clip SelectedPoseClip {get;private set;}
        public float SelectedNormalizedTime=>freeData==null?0:editSeconds/(SelectedPoseClip==AnimationLabPoseProfile.Clip.Idle?freeData.idle.length:freeData.walk.length);
        public bool SourceVisible=>sourceVisible;
        public int SourceSkinCount=>sourceView==null?0:sourceView.GetComponentsInChildren<SkinnedMeshRenderer>().Length;
        float poseReviewTime;
        public bool PoseReviewActive=>manual!=null&&manual.isActiveAndEnabled&&manual.FreePoseReview&&sourceView!=null&&freeData!=null&&freeData.PoseValid;
        public Animator PoseSourceAnimator=>sourcePoseAnimator;
        public Transform PoseSourceRoot=>sourceView!=null?sourceView.transform:null;
        public float PoseReviewTime=>poseReviewTime;
        public double PoseSourceTime=>sourcePoseSeconds;
        public int SourceBoneSegments
        {
            get{int count=0;if(sourceLines!=null)foreach(var line in sourceLines)if(line!=null&&line.enabled&&line.sharedMaterial!=null&&line.positionCount==2&&Vector3.Distance(line.GetPosition(0),line.GetPosition(1))>.001f)count++;return count;}
        }
        Vector3 freeOrigin;
        int freePoint;
        float freeWait=-1,freeTextAt;
        string freeText="";
        readonly Vector3[] freeRoute={new Vector3(0,0,3),new Vector3(1.5f,0,3.8f),new Vector3(2.5f,0,1.8f),new Vector3(-2,0,-1.5f),new Vector3(0,0,-4),Vector3.zero};
        public int FreePoint=>freePoint;
        GameObject sourceView;
        Metro3DWalkSampleData sourceData;
        Transform sourceRoot;
        Vector3 sourceScale;
        Quaternion sourceRotation;
        int sourceIndex=-1;
        Transform[] sourceBones;
        LineRenderer[] sourceLines;
        public void SetDirect(int index){direct=index;if(Passenger!=null){Passenger.WalkReviewFrozen=frozen||index>=0;Passenger.MotionTrial?.SetDirect(index);Passenger.MotionTrial?.FreezeReview(frozen);}}
        public void SetSourcePreview(bool value){showSource=value;if(value&&direct<0)SetDirect(0);RefreshSourcePreview();}
        public void SetSourceRootMotion(bool value){sourceRawRoot=value;}
        void ClearSourcePreview(){if(sourceView!=null){Destroy(sourceView);if(Passenger!=null)manual.orbit.reviewTarget=Passenger.transform;}sourceView=null;sourceData=null;sourceRoot=null;sourceBones=null;sourceLines=null;sourceIndex=-1;if(Passenger!=null)foreach(var skin in Passenger.GetComponentsInChildren<SkinnedMeshRenderer>())skin.enabled=true;}
        void RefreshSourcePreview()
        {
            ClearSourcePreview();if(!showSource||!sampleEnabled||Passenger==null||direct<0)return;
            var data=Passenger.MotionTrial?.SampleData;if(data==null||direct>=data.clips.Length)return;
            var clip=data.clips[direct];var prefab=clip.sourcePreview!=null?clip.sourcePreview:data.sourcePreview;if(prefab==null)return;
            foreach(var skin in Passenger.GetComponentsInChildren<SkinnedMeshRenderer>())skin.enabled=false;
            sourceView=Instantiate(prefab,Passenger.transform);sourceView.transform.localPosition=Vector3.zero;
            sourceView.transform.localRotation=Quaternion.Inverse(Passenger.transform.rotation)*animation.transform.rotation*(clip.sourcePreview!=null?clip.sourceAxis:data.sourceAxis);
            sourceView.transform.localScale=Vector3.one*(Mathf.Abs(animation.transform.lossyScale.x/Passenger.transform.lossyScale.x)*(clip.sourcePreview!=null?clip.sourceScale:data.sourceScale));
            foreach(var legacy in sourceView.GetComponentsInChildren<Animation>(true)){legacy.Stop();legacy.enabled=false;}
            sourceData=data;sourceRoot=sourceView.transform.Find(clip.sourcePreview!=null?clip.sourceRootPath:data.sourceRootPath);sourceRotation=sourceView.transform.localRotation;sourceScale=sourceView.transform.localScale;sourceIndex=direct;
            // HOLD contains a source skeleton, not a licensed character mesh.
            // Display that skeleton honestly instead of claiming a source skin.
            if(sourceView.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length==0){Lines();sourceBones=sourceView.GetComponentsInChildren<Transform>(true);sourceLines=new LineRenderer[sourceBones.Length];for(int i=0;i<sourceBones.Length;i++){var bone=sourceBones[i];if(bone.parent==null||bone.parent==sourceView.transform||bone.name.Contains("Estimated"))continue;var go=new GameObject("Source bone");go.transform.SetParent(sourceView.transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=lineMaterials[0];line.startWidth=line.endWidth=.018f;line.startColor=line.endColor=Color.yellow;line.positionCount=2;sourceLines[i]=line;}}
        }
        void LateUpdate(){if(!showSource||!sampleEnabled||Passenger==null||Passenger.MotionTrial?.Player==null)return;if(sourceIndex!=direct)RefreshSourcePreview();if(sourceView!=null&&sourceRoot!=null){var clip=sourceData.clips[direct];float time=Passenger.MotionTrial.Player.DirectTime+clip.sourceTimeOffset;if(clip.original.isLooping)time=Mathf.Repeat(time,clip.original.length);clip.original.SampleAnimation(sourceView,Mathf.Min(time,clip.original.length));sourceView.transform.localPosition=Vector3.zero;sourceView.transform.localRotation=sourceRotation;sourceView.transform.localScale=sourceScale;if(!sourceRawRoot){sourceRoot.localPosition=clip.sourceRootPosition;sourceRoot.localRotation=clip.sourceRootRotation;}manual.orbit.reviewTarget=sourceRawRoot?sourceRoot:Passenger.transform;if(sourceLines!=null)for(int i=0;i<sourceLines.Length;i++)if(sourceLines[i]!=null){sourceLines[i].SetPosition(0,sourceBones[i].parent.position);sourceLines[i].SetPosition(1,sourceBones[i].position);}}}
        readonly StringBuilder motionText=new StringBuilder(1200);
        public bool SampleEnabled=>sampleEnabled;
        public void SetSample(bool value){ClearSourcePreview();showSource=false;sampleEnabled=value;direct=-1;if(Passenger==null)return;Passenger.WalkReviewFrozen=frozen;if(value)ConnectSample();else{manual.contacts.Clear();manual.contacts.enabled=false;manual.contacts.BindReview(null);Passenger.MotionTrial?.Release();}}
        public void SetSamplePace(bool value){samplePace=value;if(NpcMode)Restart(Case);}
        void ConnectSample(){var trial=Metro3DMotionTrial.StartWalkSample(Passenger);manual.contacts.BindReview(Passenger);manual.contacts.enabled=true;Passenger.WalkReviewFrozen=frozen||direct>=0;if(trial!=null){trial.BeforePoseQuery=manual.contacts.RestoreForSearch;if(direct>=0)trial.SetDirect(direct);if(frozen)trial.FreezeReview(true);}}
        string text="";
        GUIStyle style;
        LineRenderer[] lines;
        Material[] lineMaterials;
        void Lines()
        {
            if(lines!=null)return;lines=new LineRenderer[8];lineMaterials=new Material[8];
            Color[] colors={Color.gray,Color.cyan,Color.yellow,new Color(1,.45f,.1f),Color.green,Color.blue,Color.magenta,Color.white};
            for(int i=0;i<lines.Length;i++){var go=new GameObject("Selected path/velocity "+i);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();lineMaterials[i]=new Material(Shader.Find("Universal Render Pipeline/Unlit"));lineMaterials[i].SetColor("_BaseColor",colors[i]);line.sharedMaterial=lineMaterials[i];line.useWorldSpace=true;line.startWidth=line.endWidth=i==0?.018f:.035f;line.startColor=line.endColor=colors[i];line.positionCount=0;lines[i]=line;}
        }
        void Start()
        {
            manualSkins=manual.GetComponentsInChildren<SkinnedMeshRenderer>();
            bool permittedBatch=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-freeWalkReview")>=0;
            string scene=gameObject.scene.name;
            if((scene=="Normal"||scene=="Situations")&&!permittedBatch)
            {
                var scenario=manual.gameObject.AddComponent<AnimationLabScenario>();
                if(!scenario.Initialize(manual,scene=="Situations"))Destroy(scenario);
                return;
            }
            if(!freeCandidateEnabled||Application.isBatchMode&&!permittedBatch)return;
            var data=Resources.Load<AnimationLabFreeWalkData>("FreeWalkReview");
            if(data!=null&&data.Valid&&(!freePoseReview||data.PoseValid)&&manual.EnableFreeWalk(data))
            {
                freeData=data;freeOrigin=manual.transform.position;
                if(freePoseReview)BeginPoseReview();else manual.FreeWalkAuto=true;
            }
        }
        public void EnterLegacyPoseReview()
        {
            if(manual==null||manual.Scenario==null||manual.FreePoseReview)return;
            manual.Scenario.enabled=false;manual.SetFrozen(false);
            freeData=Resources.Load<AnimationLabFreeWalkData>("FreeWalkReview");freeOrigin=manual.transform.position;
            BeginPoseReview();
        }
        public void LeaveLegacyPoseReview()
        {
            if(manual==null||manual.Scenario==null)return;
            EndPoseEditing();if(sourceView!=null)Destroy(sourceView);sourceView=null;
            if(sourcePoseMaterial!=null)Destroy(sourcePoseMaterial);sourcePoseMaterial=null;
            if(sourceSkinMaterial!=null)Destroy(sourceSkinMaterial);sourceSkinMaterial=null;
            if(comparisonFocus!=null)Destroy(comparisonFocus.gameObject);comparisonFocus=null;
            manual.orbit.reviewTarget=null;manual.FreePoseReview=false;manual.Scenario.enabled=true;manual.Scenario.SelectCase(0);
        }
        static Vector3 BodyForward(Animator animator)
        {
            var side=animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position-animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
            return Vector3.Cross(Vector3.ProjectOnPlane(side,Vector3.up),Vector3.up).normalized;
        }
        void BeginPoseReview()
        {
            manual.FreePoseReview=true;manual.FreeWalkAuto=false;manual.SetInput(Vector3.zero);
            sourceView=Instantiate(freeData.sourceSkeleton,transform);
            sourceView.name="Original motion skeleton and raw curves A0 - no Humanoid evaluation";
            sourcePoseAnimator=sourceView.GetComponentInChildren<Animator>();
            if(sourcePoseAnimator!=null)throw new System.InvalidOperationException("Raw A0 review must not contain an Animator/Avatar evaluator");
            foreach(var legacy in sourceView.GetComponentsInChildren<Animation>(true))legacy.enabled=false;
            sourceView.transform.position=manual.transform.position-manual.transform.right*1.4f;
            sourceBaseBones=sourceView.GetComponentsInChildren<Transform>();
            sourceBasePositions=new Vector3[sourceBaseBones.Length];sourceBaseScales=new Vector3[sourceBaseBones.Length];sourceBaseRotations=new Quaternion[sourceBaseBones.Length];
            for(int i=0;i<sourceBaseBones.Length;i++){sourceBasePositions[i]=sourceBaseBones[i].localPosition;sourceBaseScales[i]=sourceBaseBones[i].localScale;sourceBaseRotations[i]=sourceBaseBones[i].localRotation;}
            // Align the comparison display by measured anatomical facing, not an arbitrary model fix.
            manual.FreeWalk.SamplePose(freeData.idle,0);
            freeData.sourceIdle.SampleAnimation(sourceView,0);
            var sourceAll=sourceView.GetComponentsInChildren<Transform>();
            Transform left=null,right=null;foreach(var bone in sourceAll){if(bone.name=="LeftUpLeg")left=bone;if(bone.name=="RightUpLeg")right=bone;}
            float angle=Vector3.SignedAngle(Vector3.Cross(Vector3.ProjectOnPlane(right.position-left.position,Vector3.up),Vector3.up).normalized,BodyForward(manual.animator),Vector3.up);
            sourceView.transform.rotation=Quaternion.AngleAxis(angle,Vector3.up)*sourceView.transform.rotation;
            sourcePosePosition=sourceView.transform.position;sourcePoseRotation=sourceView.transform.rotation;
            SetSourcePose(freeData.sourceIdle,0,freeData.sourceIdleOrigin,freeData.sourceIdleVelocity);
            // This FBX also contains its original skin with a legacy default material.
            // Only the local reference instance gets a neutral material; no source asset is changed.
            var skins=sourceView.GetComponentsInChildren<SkinnedMeshRenderer>();
            if(skins.Length>0&&manualSkins.Length>0&&manualSkins[0].sharedMaterial!=null)
            {
                sourceSkinMaterial=new Material(manualSkins[0].sharedMaterial.shader);
                sourceSkinMaterial.name="Walk reference - local URP material";
                sourceSkinMaterial.SetColor("_BaseColor",new Color(.35f,.65f,.7f));
                if(sourceSkinMaterial.HasProperty("_Smoothness"))sourceSkinMaterial.SetFloat("_Smoothness",.3f);
                foreach(var skin in skins){var materials=skin.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=sourceSkinMaterial;skin.sharedMaterials=materials;skin.updateWhenOffscreen=true;}
            }
            var shader=Shader.Find("Universal Render Pipeline/Unlit");
            if(shader!=null)
            {
                sourcePoseMaterial=new Material(shader);sourcePoseMaterial.SetColor("_BaseColor",new Color(.2f,.85f,.95f));
                sourceBones=sourceAll;sourceLines=new LineRenderer[sourceBones.Length];
                for(int i=0;i<sourceBones.Length;i++)
                {
                    var bone=sourceBones[i];if(bone.parent==null||bone.parent==sourceView.transform)continue;
                    var go=new GameObject("Raw source bone");go.transform.SetParent(sourceView.transform,false);
                    var line=go.AddComponent<LineRenderer>();line.sharedMaterial=sourcePoseMaterial;line.positionCount=2;
                    line.startWidth=line.endWidth=bone.name.Contains("Hand")?.008f:.025f;line.useWorldSpace=true;sourceLines[i]=line;
                }
            }
            comparisonFocus=new GameObject("Single pose comparison focus").transform;comparisonFocus.SetParent(transform);
            comparisonFocus.position=(manual.transform.position+sourceView.transform.position)*.5f;
            manual.orbit.reviewTarget=comparisonFocus;
            sourceRenderers=sourceView.GetComponentsInChildren<Renderer>();
        }
        public Transform PoseJoint(int index)
        {
            if(index<0||index>=poseJoints.Length||manual==null||manual.animator==null||!manual.animator.isHuman)return null;
            if(cachedPoseAvatar!=manual.animator.avatar)
            {
                cachedPoseAvatar=manual.animator.avatar;
                for(int i=0;i<poseJoints.Length;i++)poseJoints[i]=manual.animator.GetBoneTransform(AnimationLabPoseProfile.Bones[i]);
            }
            return poseJoints[index];
        }
        public Quaternion BaseJointRotation(int index)=>index>=0&&index<baseJointRotations.Length?baseJointRotations[index]:Quaternion.identity;
        public void ShowPoseSource(bool value){sourceVisible=value;if(sourceRenderers!=null)foreach(var renderer in sourceRenderers)if(renderer!=null)renderer.enabled=value;}
        public void BeginPoseEditing(AnimationLabPoseProfile profile,AnimationLabPoseProfile.Clip clip,float normalized,bool paused)
        {
            if(!PoseReviewActive||profile==null)return;
            if(!PoseEditing)editPreviousFrozen=manual.Frozen;
            PoseEditing=true;editProfile=profile;SeekPose(clip,normalized);manual.SetFrozen(paused);EvaluatePoseNow();
        }
        public void EndPoseEditing()
        {
            if(!PoseEditing)return;
            poseReviewTime=SelectedPoseClip==AnimationLabPoseProfile.Clip.Idle?editSeconds:freeData.idle.length+editSeconds;
            PoseEditing=false;editProfile=null;if(manual!=null)manual.SetFrozen(editPreviousFrozen);ShowPoseSource(true);EvaluatePoseNow();
        }
        public void SeekPose(AnimationLabPoseProfile.Clip clip,float normalized)
        {
            if(freeData==null)return;
            SelectedPoseClip=clip;editSeconds=Mathf.Clamp01(normalized)*(clip==AnimationLabPoseProfile.Clip.Idle?freeData.idle.length:freeData.walk.length);
            manual.SetFrozen(true);EvaluatePoseNow();
        }
        public bool EvaluatePoseNow()
        {
            if(!PoseReviewActive||manual.FreeWalk==null||!manual.FreeWalk.Ready)return false;
            float idleDuration=freeData.idle.length;
            float section=Mathf.Repeat(poseReviewTime,idleDuration+6);
            bool idle=PoseEditing?SelectedPoseClip==AnimationLabPoseProfile.Clip.Idle:section<idleDuration;
            var clip=idle?freeData.idle:freeData.walk;
            float time=PoseEditing?Mathf.Clamp(editSeconds,0,clip.length):Mathf.Repeat(idle?section:section-idleDuration,clip.length);
            manual.FreeWalk.SamplePose(clip,time); // Sole fresh base-pose evaluator, including F6/slider holds.
            var profile=PoseEditing?editProfile:freeData.poseProfile;
            var pose=profile==null?null:profile.For(idle?AnimationLabPoseProfile.Clip.Idle:AnimationLabPoseProfile.Clip.Walk);
            for(int i=0;i<poseJoints.Length;i++)
            {
                var joint=PoseJoint(i);if(joint==null)continue;
                baseJointRotations[i]=joint.localRotation;
                if(pose!=null&&pose.enabled&&pose.strength>0)joint.localRotation=baseJointRotations[i]*Quaternion.Slerp(Quaternion.identity,pose.Get(i),Mathf.Clamp01(pose.strength));
            }
            SetSourcePose(idle?freeData.sourceIdle:freeData.sourceWalk,time,idle?freeData.sourceIdleOrigin:freeData.sourceWalkOrigin,idle?freeData.sourceIdleVelocity:freeData.sourceWalkVelocity);
            return true;
        }
        void SetSourcePose(AnimationClip clip,float time,Vector3 origin,Vector3 velocity)
        {
            // Raw Generic sampling only writes keyed channels. Reset unkeyed channels too,
            // so returning from Walk to Idle cannot retain a different clip's source pose.
            for(int i=0;i<sourceBaseBones.Length;i++){sourceBaseBones[i].localPosition=sourceBasePositions[i];sourceBaseBones[i].localRotation=sourceBaseRotations[i];sourceBaseBones[i].localScale=sourceBaseScales[i];}
            clip.SampleAnimation(sourceView,time);sourcePoseSeconds=time;
            // Review display only: subtract the recorded constant XZ travel, not instantaneous hip motion.
            // This leaves the source pelvis bob and lateral oscillation intact and never moves the NPC/Motor.
            sourceView.transform.SetPositionAndRotation(sourcePosePosition-sourcePoseRotation*(origin+velocity*time),sourcePoseRotation);
            if(sourceLines!=null)for(int i=0;i<sourceLines.Length;i++)if(sourceLines[i]!=null){sourceLines[i].SetPosition(0,sourceBones[i].parent.position);sourceLines[i].SetPosition(1,sourceBones[i].position);}
        }
        void PoseSequence()
        {
            if(Keyboard.current!=null&&Keyboard.current.rKey.wasPressedThisFrame){if(PoseEditing)editSeconds=0;else poseReviewTime=0;}
            if(!manual.Frozen){if(PoseEditing){float length=SelectedPoseClip==AnimationLabPoseProfile.Clip.Idle?freeData.idle.length:freeData.walk.length;editSeconds=Mathf.Repeat(editSeconds+Time.deltaTime,length);}else poseReviewTime+=Time.deltaTime;}
            float idleDuration=freeData.idle.length;
            float section=Mathf.Repeat(poseReviewTime,idleDuration+6);bool idle=section<idleDuration;
            EvaluatePoseNow();
            if(Time.unscaledTime>=freeTextAt)
            {
                freeTextAt=Time.unscaledTime+.2f;
                freeText=PoseEditing?"Walk 자세 조절 · Scene 뷰의 관절 점/회전 손잡이 · 보정은 프로필에 별도 저장":$"왼쪽: 원본 A0 · 오른쪽: 대상 더미 A2 · {(idle?"서기":"걷기")} · 같은 재생 시각\n이동·전환·IK OFF · 마우스 시점 · F6 정지/재개 · R 처음부터";
            }
        }
        void FreeSequence()
        {
            if(Keyboard.current!=null&&Keyboard.current.rKey.wasPressedThisFrame){freePoint=0;freeWait=-1;manual.FreeWalkAuto=true;}
            if(manual.FreeWalkAuto&&!manual.Frozen)
            {
                Vector3 delta=freeOrigin+freeRoute[freePoint]-manual.transform.position;delta.y=0;
                bool stopping=freePoint==0||freePoint==4||freePoint==freeRoute.Length-1;
                if(freeWait>=0)
                {
                    manual.SetInput(Vector3.zero);
                    freeWait-=Time.deltaTime;
                    if(freeWait<=0) {freeWait=-1;freePoint=(freePoint+1)%freeRoute.Length;}
                }
                else if(delta.magnitude<(stopping?.09f:.65f))
                {
                    if(stopping){freeWait=2;manual.SetInput(Vector3.zero);}
                    else freePoint=(freePoint+1)%freeRoute.Length;
                }
                else
                {
                    Vector3 heading=delta.normalized;
                    if(!stopping&&delta.magnitude<1.3f)
                    {
                        Vector3 next=freeOrigin+freeRoute[(freePoint+1)%freeRoute.Length]-manual.transform.position;next.y=0;
                        heading=Vector3.Slerp(heading,next.normalized,(1-delta.magnitude/1.3f)*.6f).normalized;
                    }
                    float speed=stopping?Mathf.Min(manual.WalkSpeed,Metro3DWalkMotion.ArrivalSpeed(delta.magnitude)):manual.WalkSpeed;
                    manual.SetInput(heading*(speed/manual.WalkSpeed));
                }
            }
            if(Time.unscaledTime>=freeTextAt)
            {
                freeTextAt=Time.unscaledTime+.2f;
                freeText=$"무료 Walk · {(manual.FreeWalkAuto?"자동 확인 경로":"직접 조작")} · {manual.FreeWalk.State}\nWASD 직접 걷기 · R 자동 경로 처음부터 · 마우스 시점 · F6 정지/재개 · IK/MxM OFF";
            }
        }
        void EnsureWorld()
        {
            if(world!=null)return;
            var go=new GameObject("Walk production services (no delivery or train simulation)");go.SetActive(false);
            world=go.AddComponent<Metro3DWorld>();world.walkingReviewOnly=true;
            world.settings=Instantiate(sourceSettings);world.settings.npcSpeed=manual.library.walkSpeed;
            world.session=go.AddComponent<Metro3DSession>();go.AddComponent<Metro3DStationTraffic>();crowd=go.AddComponent<Metro3DCrowd>();
            station=new GameObject("Walk spatial frame").transform;station.position=Vector3.down*3.6f;
            geometry.SetParent(station,true);world.stations.Add(station);go.SetActive(true);
        }
        public void SetNpcMode(bool value)
        {
            if(NpcMode==value)return;
            EnsureWorld();if(manualSkins==null)manualSkins=manual.GetComponentsInChildren<SkinnedMeshRenderer>();NpcMode=value;manual.ExternalReview=value;manual.SetFrozen(value);manual.motor.enabled=!value;manual.contacts.Clear();manual.contacts.enabled=!value;
            foreach(var skin in manualSkins)skin.enabled=!value;
            if(value)Restart(Case);else{ClearSourcePreview();manual.contacts.BindReview(null);if(Passenger!=null)Passenger.gameObject.SetActive(false);if(companion!=null)companion.gameObject.SetActive(false);if(cornerWall!=null)cornerWall.SetActive(false);if(lines!=null)foreach(var line in lines)line.enabled=false;station.rotation=Quaternion.identity;manual.orbit.reviewTarget=null;manual.SetFrozen(false);manual.ResetActor();}
        }
        public void SetCandidate(bool value){candidate=value;manual.WalkCandidate=value;if(Passenger!=null)Restart(Case);}
        public void Restart(int scenario)
        {
            EnsureWorld();Case=(scenario+Cases.Length)%Cases.Length;frozen=false;
            ClearSourcePreview();
            manual.contacts.Clear();manual.contacts.enabled=false;manual.contacts.BindReview(null);world.settings.npcSpeed=samplePace?1.6f:manual.library.walkSpeed;
            if(Passenger!=null){Passenger.ResetForPool();Destroy(Passenger.gameObject);Passenger=null;}
            if(companion!=null){companion.ResetForPool();Destroy(companion.gameObject);companion=null;}
            // Rotate the entire fixture, including colliders and the pedestrian frame.
            station.rotation=Quaternion.Euler(0,Case==11||Case==12?67:Case==13?-43:0,0);crowd.ForgetSurface(station);Physics.SyncTransforms();
            if(cornerWall!=null){cornerWall.SetActive(Case==4||Case==5);cornerWall.transform.localPosition=new Vector3(Case==4?-4.5f:4.5f,.8f,0);Physics.SyncTransforms();}
            var go=Instantiate(sourceSettings.passengerPrefab,station);go.SetActive(false);go.name="Selected WALK passenger";
            Passenger=go.GetComponent<Metro3DPassenger>();if(Passenger==null)Passenger=go.AddComponent<Metro3DPassenger>();
            Vector3 start=new Vector3(0,3.6f,-5),goal=new Vector3(0,3.6f,5);
            if(Case==1)goal=new Vector3(-3,3.6f,1);if(Case==2)goal=new Vector3(3,3.6f,1);
            if(Case==4){start=new Vector3(-6,3.6f,-3);goal=new Vector3(-1,3.6f,5);}
            if(Case==5){start=new Vector3(6,3.6f,-3);goal=new Vector3(1,3.6f,5);}
            if(Case==6||Case==12)start=new Vector3(-3,3.6f,1);
            if(Case==7||Case==13){start=station.InverseTransformPoint(stairAfter.position);goal=new Vector3(-3,3.6f,1);}
            if(Case==8)goal=new Vector3(0,3.6f,-8);
            if(Case==9||Case==10)goal=start+Quaternion.Euler(0,Case==9?30:45,0)*Vector3.forward*7;
            go.transform.localPosition=start;go.transform.localRotation=Quaternion.identity;
            Passenger.InitialDirection=1;Passenger.InitializeWalkingReview(world,2300);Passenger.WalkReviewCandidate=candidate;
            Passenger.ReviewMeasuredWalkSpeed=manual.library.walkSpeed;
            Passenger.WalkReviewAnimationCandidate=animationCandidate;world.passengers.Clear();world.passengers.Add(Passenger);
            go.SetActive(true);animation=go.GetComponentInChildren<Animator>();animation.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animation.Play("Locomotion",0,.17f);foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>())skin.updateWhenOffscreen=true;
            Passenger.WalkJourney(station.TransformPoint(goal),true);
            if(Case==3){for(int i=0;i<30;i++)raw[i]=Vector3.Lerp(start,goal,(i+1)/30f)+Vector3.right*(i%2==0?.1f:-.1f);raw[29]=goal;Passenger.SetReviewPath(raw,30);}
            if(Case==6||Case==7||Case==12||Case==13){bool up=Case==6||Case==12;Vector3 entry=station.InverseTransformPoint(up?stairEntry.position:stairExit.position),exit=station.InverseTransformPoint(up?stairExit.position:stairEntry.position),after=up?station.InverseTransformPoint(stairAfter.position):goal;Passenger.Journey.SetReviewStair(entry,exit,after);}
            if(Case>=14){var other=Instantiate(sourceSettings.passengerPrefab,station);other.SetActive(false);companion=other.GetComponent<Metro3DPassenger>();if(companion==null)companion=other.AddComponent<Metro3DPassenger>();other.transform.localPosition=Case==14?new Vector3(.35f,3.6f,4):new Vector3(0,3.6f,-6.2f);other.transform.localRotation=Quaternion.Euler(0,Case==14?180:0,0);companion.InitializeWalkingReview(world,2400);other.SetActive(true);world.passengers.Add(companion);companion.WalkJourney(station.TransformPoint(Case==14?new Vector3(.35f,3.6f,-6):goal),true);}
            rawCount=0;manual.orbit.reviewTarget=Passenger.transform;manual.orbit.yaw=station.eulerAngles.y;
            Physics.SyncTransforms();
            if(sampleEnabled){ConnectSample();RefreshSourcePreview();}
        }
        void Update()
        {
            if(manual==null)return;
            if(manual.Scenario!=null&&manual.Scenario.enabled)return;
            if(manual.FreeWalk!=null){if(manual.FreePoseReview)PoseSequence();else FreeSequence();return;}
            if(!Automated&&Keyboard.current!=null){var k=Keyboard.current;
                if(k.nKey.wasPressedThisFrame)SetNpcMode(!NpcMode);
                if(NpcMode){if(k.bKey.wasPressedThisFrame)SetCandidate(!candidate);if(k.jKey.wasPressedThisFrame)Restart(Case+(k.leftShiftKey.isPressed?-1:1));if(k.rKey.wasPressedThisFrame)Restart(Case);
                    if(k.kKey.wasPressedThisFrame)SetSample(!sampleEnabled);
                    if(k.tKey.wasPressedThisFrame)SetSamplePace(!samplePace);
                    if(k.lKey.wasPressedThisFrame&&sampleEnabled){showSource=false;ClearSourcePreview();SetDirect(direct<0?0:-1);if(direct<0)Restart(Case);}
                    if(k.oKey.wasPressedThisFrame&&sampleEnabled)SetSourcePreview(!showSource);
                    if(k.uKey.wasPressedThisFrame&&showSource)sourceRawRoot=!sourceRawRoot;
                    if(sampleEnabled&&direct>=0&&(k.rightBracketKey.wasPressedThisFrame||k.leftBracketKey.wasPressedThisFrame)){int count=Passenger.MotionTrial?.SampleData?.clips.Length??0;if(count>0){direct=(direct+(k.rightBracketKey.wasPressedThisFrame?1:count-1))%count;Passenger.MotionTrial?.SetDirect(direct);}}
                    if(sampleEnabled&&direct>=0&&(k.commaKey.wasPressedThisFrame||k.periodKey.wasPressedThisFrame)){var player=Passenger.MotionTrial?.Player;if(player!=null)player.SeekDirect(player.DirectTime+(k.periodKey.wasPressedThisFrame?1:-1));}
                    if(k.iKey.wasPressedThisFrame&&sampleEnabled){manual.contacts.correction=!manual.contacts.correction;manual.contacts.Clear();}
                    if(k.vKey.wasPressedThisFrame){animationCandidate=!animationCandidate;Passenger.WalkReviewAnimationCandidate=animationCandidate;}
                    if(k.f6Key.wasPressedThisFrame){frozen=!frozen;Passenger.WalkReviewFrozen=frozen||direct>=0;animation.speed=frozen?0:1;Passenger.MotionTrial?.FreezeReview(frozen);if(companion!=null){companion.WalkReviewFrozen=frozen;companion.GetComponentInChildren<Animator>().speed=animation.speed;}}
                    if(k.hKey.wasPressedThisFrame)detail=!detail;
                }
            }
            if(!NpcMode||Passenger==null)return;
            Lines();if(rawCount==0&&Passenger.PathPointCount>0)rawCount=Passenger.CopyReviewPath(raw);
            if(Time.unscaledTime>=nextText){nextText=Time.unscaledTime+.2f;text=$"NPC 경로 · {Cases[Case]} · 이동 {(candidate?"후보":"기존")} / 애니 연결 {(animationCandidate?"후보":"기존")}\n희망 {Passenger.DesiredVelocity.magnitude:F2} / 회피 {Passenger.AvoidanceVelocity.magnitude:F2} / 최종 {Passenger.CommandVelocity.magnitude:F2} / 실제 {Passenger.Velocity.magnitude:F2} m/s   {Passenger.WalkPhase} · {Passenger.StopReason}\n{Passenger.Journey.ReviewStairPhase} · {(Passenger.FinalWalkTarget?"최종 도착":"중간 통과")} · 경로 {Passenger.PathPointIndex}/{Passenger.PathPointCount} · yaw {Passenger.RotationDegreesPerSecond:F1}°/s\nN 수동 · J 조건 · B 이동 비교 · V 연결 비교 · F6 정지 · R 재시작 · H 표기\nK 신규 MM · T 공통 속도 {(samplePace?"1.6m/s":"기존")} (모든 비교 공통) · L 단독/검색 · [ ] 클립 · I 접지 {(manual.contacts.correction?"ON":"OFF")}";
                if(sampleEnabled&&Passenger.MotionTrial!=null){motionText.Clear();Passenger.MotionTrial.AppendDetails(motionText);text+=motionText.ToString();if(direct>=0)text+="\n단독 자세 · 이동/접지 보정 OFF · , . ±1초 · L 검색 복귀(재시작)";else text+=$"\n보라: {(Passenger.MotionTrial.SampleData?.supportsTurns==true?1:.4f):F1}s 예측 / 흰색: 실제 이력 · IK 잠금 L/R {manual.contacts.LeftPlanted}/{manual.contacts.RightPlanted} · 골반 보정 {manual.contacts.PelvisCorrection:F3}m";if(showSource)text+=sourceRawRoot?"\nO 원본 골격 · U 기록된 루트 이동/회전 (실제 NPC 이동 아님)":"\nO 원본 골격 · U 루트 제거 비교 (리타기팅·검색·접지 OFF)";else text+="\nO 원본 골격 비교";}}
            if(detail){for(int i=1;i<rawCount;i++)Debug.DrawLine(station.TransformPoint(raw[i-1])+Vector3.up*.04f,station.TransformPoint(raw[i])+Vector3.up*.04f,Color.gray);
                Vector3 p=Passenger.transform.position+Vector3.up*.08f;Debug.DrawLine(p,Passenger.TravelLookahead+Vector3.up*.08f,Color.cyan);Debug.DrawRay(p,Passenger.DesiredVelocity,Color.yellow);Debug.DrawRay(p,Passenger.CommandVelocity,Color.green);Debug.DrawRay(p,Passenger.transform.forward,Color.blue);}
            for(int i=0;i<lines.Length;i++)lines[i].enabled=detail;
            if(detail){lines[0].positionCount=rawCount;for(int i=0;i<rawCount;i++)lines[0].SetPosition(i,station.TransformPoint(raw[i])+Vector3.up*.04f);
                Vector3 p=Passenger.transform.position+Vector3.up*.08f;for(int i=1;i<6;i++){lines[i].positionCount=2;lines[i].SetPosition(0,p);lines[i].SetPosition(1,i==1?Passenger.TravelLookahead+Vector3.up*.08f:p+(i==2?Passenger.DesiredVelocity:i==3?Passenger.AvoidanceVelocity:i==4?Passenger.CommandVelocity:Passenger.transform.forward));}}
            for(int i=6;i<8;i++){int count=0;if(detail&&sampleEnabled&&Passenger.MotionTrial!=null)count=i==6?(Passenger.MotionTrial.Matching?Passenger.MotionTrial.CopyPrediction(motionLine):0):Passenger.MotionTrial.CopyHistory(motionLine);lines[i].positionCount=count;for(int k=0;k<count;k++)lines[i].SetPosition(k,motionLine[k]+Vector3.up*.12f);}
        }
        void FixedUpdate(){if(NpcMode&&Passenger!=null&&!Passenger.WalkReviewFrozen)MotorSample?.Invoke(Passenger,Time.fixedDeltaTime);}
        void OnGUI(){if(manual==null)return;if(manual.Scenario!=null&&manual.Scenario.enabled)return;if(manual.Scenario!=null&&GUI.Button(new Rect(12,12,230,30),"Normal 데이터 동작 검수로 복귀"))LeaveLegacyPoseReview();if(manual.FreeWalk!=null){if(Automated)return;if(style==null)style=new GUIStyle(GUI.skin.label){font=manual.library.font,fontSize=14};GUI.Box(new Rect(12,Screen.height-72,Mathf.Min(900,Screen.width-24),60),"");GUI.Label(new Rect(24,Screen.height-68,870,54),freeText,style);return;}if(!NpcMode||!detail||Automated)return;if(style==null)style=new GUIStyle(GUI.skin.label){font=manual.library.font,fontSize=14};float height=sampleEnabled?Mathf.Min(Screen.height-24,430):142;GUI.Box(new Rect(12,Screen.height-height-12,Mathf.Min(1040,Screen.width-24),height),"");GUI.Label(new Rect(24,Screen.height-height-8,1000,height-4),text,style);}
        void OnDestroy(){if(sourcePoseMaterial!=null)Destroy(sourcePoseMaterial);if(sourceSkinMaterial!=null)Destroy(sourceSkinMaterial);if(comparisonFocus!=null)Destroy(comparisonFocus.gameObject);ClearSourcePreview();if(lineMaterials!=null)foreach(var material in lineMaterials)Destroy(material);if(world!=null){Destroy(world.settings);Destroy(world.gameObject);}}
    }
}

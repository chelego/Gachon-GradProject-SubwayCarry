#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Real render-time manual input regression, plus a separately fixed-clock comparison film.
    [InitializeOnLoad]
    public static class AnimationLabFirstStepChecks
    {
        const string Key="SubwayCarry.FirstStep.Check";
        static readonly int[] Rates={30,60,0};
        static readonly int[] Delays={8,16,11,29,6,18,35,9};
        static AnimationLabScenario scenario;
        static AnimationLabFirstStepInput input;
        static Result result;
        static Case row;
        static int test,rate,lastFrame=-1;
        static float begin,previousTime;
        static bool video;
        static bool Response=>Environment.GetCommandLineArgs().Contains("-normalResponse");
        static bool CorrectionOff=>Environment.GetCommandLineArgs().Contains("-normalCorrectionOff");
        static bool Posture=>Environment.GetCommandLineArgs().Contains("-postureReview");
        static bool Mixed=>Environment.GetCommandLineArgs().Contains("-postureMixed");
        static bool queryReset;
        static bool PostureAblation=>Environment.GetCommandLineArgs().Contains("-postureAblation");
        static bool PostureAuto=>Environment.GetCommandLineArgs().Contains("-postureAuto");
        static string output,variant;
        static RenderTexture target;
        static Texture2D pixels;
        static int filmFrame;
        static int lastSleep;
        [Serializable] public class Frame
        {
            public float t,dt,clipTime,oldTime,blend,yaw,lSupport,rSupport;
            public int entry,oldEntry,switches;
            public string reason;
            public Vector3 input,root,command,actual,left,right,leftToe,rightToe,hips,leftHand,rightHand,leftElbow,rightElbow;
            public Vector3 chest,shoulderL,shoulderR;
            public Vector3 head,neck,collarL,collarR,hipL,hipR,kneeL,kneeR,rootScale,visualScale,visualPosition;
        }
        [Serializable] public class Case {public int test,fps;public long poseTicks,poseBytes,poseFrames,searchTicks,searchBytes,searches;public List<Frame> frames=new List<Frame>();}
        [Serializable] public class Pose {public string rig,clip;public float t;public Vector3 shoulderL,shoulderR,elbowL,elbowR,handL,handR,footL,footR,hips;}
        [Serializable] public class Result {public string error="",scope="Isolated native Unity; manual input; unapproved visual candidate";public string[] clips;public List<Case> cases=new List<Case>();public List<Pose> poses=new List<Pose>();}
        static AnimationLabFirstStepChecks(){EditorApplication.update+=Tick;}
        public static void Run()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Isolated QA only");
            variant=Environment.GetCommandLineArgs().Contains("-firstStepAfter")?"after":"before";
            video=Environment.GetCommandLineArgs().Contains("-firstStepVideo");
            output=Path.Combine(Directory.GetParent(Application.dataPath).Parent.Parent.Parent.FullName,"MotionMatching");
            result=new Result();Audit();SessionState.SetString(Key+".Variant",variant);SessionState.SetBool(Key+".Video",video);
            File.WriteAllText(Path.Combine(output,"first-step-pose-"+variant+".json"),JsonUtility.ToJson(result));
            SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(AnimationLabBuilder.Root+"/Normal.unity");EditorApplication.EnterPlaymode();
        }
        static void Audit()
        {
            var data=AssetDatabase.LoadAssetAtPath<AnimationLabMotionSet>(AnimationLabBuilder.Root+"/Data/Resources/ReviewMotions.asset");
            result.clips=data.entries.Select(e=>e.label).ToArray();
            var free=AssetDatabase.LoadAssetAtPath<AnimationLabFreeWalkData>(AnimationLabBuilder.Root+"/FreeWalk/Resources/FreeWalkReview.asset");
            foreach(var entry in data.entries.Where(e=>e.usable&&(e.uses&(AnimationLabMotionSet.Use.Forward|AnimationLabMotionSet.Use.MovingTurn|AnimationLabMotionSet.Use.Idle|AnimationLabMotionSet.Use.Pivot))!=0&&!e.mirrored))
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AnimationLabBuilder.Root+"/Prefabs/Character.prefab");
                var root=UnityEngine.Object.Instantiate(prefab);var animator=root.GetComponentInChildren<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                foreach(var c in root.GetComponentsInChildren<MonoBehaviour>())c.enabled=false;
                using(var player=new AnimationLabFreeWalkPlayer(animator,free))
                {
                    player.SelectExternal(entry.clip,0,0);
                    var correction=Resources.Load<AnimationLabPoseProfile>("NormalPoseProfile");
                    for(int i=0;i<=120;i++)
                    {
                        float at=entry.duration*i/120;player.EvaluateExternal(at,0,1,root.transform);result.poses.Add(ReadPose(animator,"target",entry.label,at));
                        if(correction==null)continue;
                        var pose=(entry.uses&AnimationLabMotionSet.Use.Idle)!=0?correction.idle:correction.walk;
                        if(pose.enabled)for(int b=0;b<AnimationLabPoseProfile.Bones.Length;b++){var bone=animator.GetBoneTransform(AnimationLabPoseProfile.Bones[b]);if(bone!=null)bone.localRotation*=Quaternion.SlerpUnclamped(Quaternion.identity,pose.Get(b),pose.strength);}
                        result.poses.Add(ReadPose(animator,"target-corrected",entry.label,at));
                    }
                }
                UnityEngine.Object.DestroyImmediate(root);
                string path=AssetDatabase.GetAssetPath(entry.clip);var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(model==null)continue;
                root=UnityEngine.Object.Instantiate(model);animator=root.GetComponent<Animator>();
                if(animator==null)animator=root.GetComponentInChildren<Animator>();
                if(animator==null||!animator.isHuman){UnityEngine.Object.DestroyImmediate(root);continue;}
                animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
                var graph=PlayableGraph.Create("First step source audit");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var clip=AnimationClipPlayable.Create(graph,entry.clip);clip.SetApplyFootIK(false);clip.SetApplyPlayableIK(false);AnimationPlayableOutput.Create(graph,"Source",animator).SetSourcePlayable(clip);graph.Play();
                for(int i=0;i<=120;i++){float at=entry.duration*i/120;clip.SetTime(at);graph.Evaluate(0);result.poses.Add(ReadPose(animator,"source",entry.label,at));}
                graph.Destroy();UnityEngine.Object.DestroyImmediate(root);
            }
        }
        public static void MakeNormalCorrection()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Isolated QA only");
            string path=AnimationLabBuilder.Root+"/Data/Resources/NormalPoseProfile.asset";
            if(AssetDatabase.LoadAssetAtPath<AnimationLabPoseProfile>(path)!=null)throw new InvalidOperationException("Preserve existing authored correction");
            var profile=ScriptableObject.CreateInstance<AnimationLabPoseProfile>();
            var free=AssetDatabase.LoadAssetAtPath<AnimationLabFreeWalkData>(AnimationLabBuilder.Root+"/FreeWalk/Resources/FreeWalkReview.asset");
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AnimationLabBuilder.Root+"/Prefabs/Character.prefab"));
            var animator=root.GetComponentInChildren<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var set=Resources.Load<AnimationLabMotionSet>("ReviewMotions");
            using(var player=new AnimationLabFreeWalkPlayer(animator,free))
            {
                for(int row=0;row<2;row++)
                {
                    var clip=row==0?free.idle:set.entries.First(e=>e.label=="Walking").clip;
                    player.SelectExternal(clip,clip.length*.3f,0);player.EvaluateExternal(clip.length*.3f,0,1,root.transform);
                    var left=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);var right=animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                    var lateral=Vector3.ProjectOnPlane(right.position-left.position,Vector3.up).normalized;
                    var pose=row==0?profile.idle:profile.walk;
                    foreach(int i in new[]{3,4,7,8})
                    {
                        var bone=animator.GetBoneTransform(AnimationLabPoseProfile.Bones[i]);if(bone==null)continue;
                        // Axis measured on this approved target, then stored as an editable local FK delta.
                        var axis=i==3||i==7?Vector3.up:lateral;
                        float angle=i==3?-2.5f:i==7?2.5f:4.5f;
                        pose.Set(i,Quaternion.AngleAxis(angle,bone.InverseTransformDirection(axis)));
                    }
                }
            }
            UnityEngine.Object.DestroyImmediate(root);AssetDatabase.CreateAsset(profile,path);AssetDatabase.SaveAssets();
            Debug.Log("NORMAL_CORRECTION_CREATED target-local; clavicle 2.5 deg, upper arm 4.5 deg; no chest/hand/leg adjustment");EditorApplication.Exit(0);
        }
        static Pose ReadPose(Animator a,string rig,string clip,float time)
        {
            var h=a.GetBoneTransform(HumanBodyBones.Hips);var l=a.GetBoneTransform(HumanBodyBones.LeftUpperLeg);var r=a.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            var body=Quaternion.LookRotation(Vector3.Cross(Vector3.ProjectOnPlane(r.position-l.position,Vector3.up),Vector3.up));
            var inv=Quaternion.Inverse(body);Vector3 Position(HumanBodyBones b){var t=a.GetBoneTransform(b);return t==null?Vector3.zero:inv*(t.position-h.position);}
            return new Pose{rig=rig,clip=clip,t=time,shoulderL=Position(HumanBodyBones.LeftUpperArm),shoulderR=Position(HumanBodyBones.RightUpperArm),elbowL=Position(HumanBodyBones.LeftLowerArm),elbowR=Position(HumanBodyBones.RightLowerArm),handL=Position(HumanBodyBones.LeftHand),handR=Position(HumanBodyBones.RightHand),footL=Position(HumanBodyBones.LeftFoot),footR=Position(HumanBodyBones.RightFoot),hips=h.position};
        }
        static Vector3 Bone(HumanBodyBones b)=>scenario.Actor.animator.GetBoneTransform(b).position;
        public static void Feed()
        {
            if(scenario==null||row==null)return;
            float t=(video?Time.time:Time.unscaledTime)-begin;Vector3 command=t>=.5f&&t<3.7f?Vector3.forward:Vector3.zero;
            if(Posture){command=t>=1&&t<(Mixed?6:3.8f)?Vector3.forward:Vector3.zero;if(Mixed&&!queryReset&&t>=2){scenario.Search.postureFamily=0;queryReset=true;Debug.Log("QUERY_STATE_RESET at="+t);}if(!PostureAuto)scenario.SetManualInput(command);input.value=command;return;}
            if(Response)
            {
                command=t>=.5f&&t<8?Vector3.forward:Vector3.zero;
                if(t>=3&&t<4)command=new Vector3(.5f,0,1).normalized;
                if(t>=4.8f&&t<5.8f)command=new Vector3(-.5f,0,1).normalized;
                if(t>=6.4f&&t<7.2f)command=new Vector3(1,0,1).normalized;
                scenario.SetManualInput(command);input.value=command;return;
            }
            if(test==1&&t>=2.1f&&t<2.8f)command=new Vector3(.5f,0,1).normalized;
            if(test==4&&t>=2.1f&&t<2.8f)command=new Vector3(-.5f,0,1).normalized;
            if(test==2)command=t>=.5f&&t<3.7f?Vector3.back:Vector3.zero;
            if(test==3&&t>=1.35f&&t<1.65f)command=Vector3.zero;
            scenario.SetManualInput(command);input.value=command;
        }
        static void StartCase()
        {
            queryReset=false;
            scenario.SelectCase(PostureAuto?0:8);scenario.Automated=true;scenario.Actor.Automated=true;
            if(PostureAblation)scenario.Actor.FreeWalk.InertialTransitions=false;
            if(CorrectionOff&&scenario.NormalPose!=null){scenario.PoseDraft=UnityEngine.Object.Instantiate(scenario.NormalPose);scenario.PoseDraft.idle.enabled=scenario.PoseDraft.walk.enabled=false;}
            begin=previousTime=video?Time.time:Time.unscaledTime;row=new Case{test=test,fps=video?60:Rates[rate]};result.cases.Add(row);
            row.poseTicks=-scenario.PoseTicks;row.poseBytes=-scenario.PoseBytes;row.poseFrames=-scenario.PoseFrames;
            row.searchTicks=-scenario.Search.Ticks;row.searchBytes=-scenario.Search.Bytes;row.searches=-scenario.Search.Searches;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||Time.frameCount==lastFrame||Time.frameCount<8)return;
            lastFrame=Time.frameCount;
            try
            {
                if(scenario==null)
                {
                    variant=SessionState.GetString(Key+".Variant","before");video=SessionState.GetBool(Key+".Video",false);
                    output=Path.Combine(Directory.GetParent(Application.dataPath).Parent.Parent.Parent.FullName,"MotionMatching");
                    result=JsonUtility.FromJson<Result>(File.ReadAllText(Path.Combine(output,"first-step-pose-"+variant+".json")));
                    scenario=UnityEngine.Object.FindFirstObjectByType<AnimationLabScenario>();
                    Application.logMessageReceived+=Log;
                    input=scenario.gameObject.AddComponent<AnimationLabFirstStepInput>();
                    if(Environment.GetCommandLineArgs().Contains("-firstStepPoseControls")){CheckPoseControls();Finish();return;}
                    Time.captureDeltaTime=video?1f/60:0;StartCase();return;
                }
                float now=video?Time.time:Time.unscaledTime;
                float t=now-begin,dt=now-previousTime;if(dt<=0)return;
                previousTime=now;
                row.frames.Add(new Frame{t=t,dt=dt,clipTime=scenario.TimeInClip,oldTime=scenario.PreviousTime,blend=scenario.Blend,yaw=scenario.transform.eulerAngles.y,entry=scenario.EntryIndex,oldEntry=scenario.PreviousIndex,switches=scenario.Switches,reason=scenario.Reason,input=input.value,root=scenario.transform.position,command=scenario.LastCommand,actual=scenario.Actor.ActualVelocity,lSupport=scenario.LeftSupport,rSupport=scenario.RightSupport,left=Bone(HumanBodyBones.LeftFoot),right=Bone(HumanBodyBones.RightFoot),leftToe=Bone(HumanBodyBones.LeftToes),rightToe=Bone(HumanBodyBones.RightToes),hips=Bone(HumanBodyBones.Hips),leftHand=Bone(HumanBodyBones.LeftHand),rightHand=Bone(HumanBodyBones.RightHand),leftElbow=Bone(HumanBodyBones.LeftLowerArm),rightElbow=Bone(HumanBodyBones.RightLowerArm)});
                var captured=row.frames[row.frames.Count-1];captured.chest=Bone(HumanBodyBones.Chest);captured.shoulderL=Bone(HumanBodyBones.LeftUpperArm);captured.shoulderR=Bone(HumanBodyBones.RightUpperArm);
                captured.head=Bone(HumanBodyBones.Head);captured.neck=Bone(HumanBodyBones.Neck);captured.collarL=Bone(HumanBodyBones.LeftShoulder);captured.collarR=Bone(HumanBodyBones.RightShoulder);
                captured.hipL=Bone(HumanBodyBones.LeftUpperLeg);captured.hipR=Bone(HumanBodyBones.RightUpperLeg);captured.kneeL=Bone(HumanBodyBones.LeftLowerLeg);captured.kneeR=Bone(HumanBodyBones.RightLowerLeg);
                captured.rootScale=scenario.transform.lossyScale;captured.visualScale=scenario.Actor.animator.transform.lossyScale;captured.visualPosition=scenario.Actor.animator.transform.position;
                if(video&&Time.frameCount%2==0)Capture();
                if(t>=(Posture?Mixed?7.2f:5.2f:Response?9:4.8f))
                {
                    row.poseTicks+=scenario.PoseTicks;row.poseBytes+=scenario.PoseBytes;row.poseFrames+=scenario.PoseFrames;
                    row.searchTicks+=scenario.Search.Ticks;row.searchBytes+=scenario.Search.Bytes;row.searches+=scenario.Search.Searches;
                    if(!row.frames.Any(f=>f.actual.sqrMagnitude>.01f))throw new InvalidOperationException("Manual motor did not run; never accept an idle-only test");
                    if(Mixed&&variant=="after"&&row.frames.Any(f=>scenario.Data.entries[f.entry].postureFamily!=1))throw new InvalidOperationException("Normal selected an incompatible posture family after query-state loss");
                    if(Mixed&&variant=="before"&&!row.frames.Any(f=>scenario.Data.entries[f.entry].postureFamily==0))throw new InvalidOperationException("Mixed-family symptom was not reproduced; do not substitute a different defect");
                    Debug.Log("FIRST_STEP_CASE "+variant+" "+test+" "+Rates[rate]+" frames="+row.frames.Count);
                    if(Response||Posture||video||++test>=5){test=0;if(PostureAuto||PostureAblation||video||++rate>=Rates.Length){Finish();return;}}
                    StartCase();
                }
                if(!video)
                {
                    int delay=Rates[rate]>0?Mathf.Max(0,Mathf.RoundToInt(1000f/Rates[rate]-(dt*1000-lastSleep))):Delays[Time.frameCount%Delays.Length];
                    lastSleep=delay;Thread.Sleep(delay); // Compensate measured editor overhead, without changing Unity time.
                }
            }
            catch(Exception e){result.error=e.ToString();Debug.LogException(e);Finish();}
        }
        static void Capture()
        {
            if(Mixed)
            {
                if(target==null){target=new RenderTexture(960,540,24);pixels=new Texture2D(960,540,TextureFormat.RGB24,false);Directory.CreateDirectory(Path.Combine(output,"mixed-"+variant));}
                var c=Camera.main;var prior=RenderTexture.active;c.targetTexture=target;c.aspect=16f/9;c.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,960,540),0,0,false);pixels.Apply();RenderTexture.active=prior;c.targetTexture=null;
                File.WriteAllBytes(Path.Combine(output,"mixed-"+variant,filmFrame++.ToString("D4")+".png"),pixels.EncodeToPNG());return;
            }
            string prefix=Posture?"posture-":Response?"normal-response-":"first-step-";
            if(target==null){target=new RenderTexture(640,640,24);pixels=new Texture2D(1280,640,TextureFormat.RGB24,false);Directory.CreateDirectory(Path.Combine(output,prefix+variant));}
            var camera=Camera.main;var focus=scenario.transform.position+Vector3.up*.95f;
            if(Posture){focus.y=.95f;camera.orthographic=true;camera.orthographicSize=1.03f;}
            camera.targetTexture=target;camera.fieldOfView=36;
            var saved=RenderTexture.active;
            for(int view=0;view<2;view++)
            {
                camera.transform.position=focus+(view==0?new Vector3(3.5f,.3f,0):new Vector3(2.6f,.4f,Posture?-2.6f:2.6f));camera.transform.LookAt(focus);camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,640,640),view*640,0,false);
            }
            pixels.Apply();RenderTexture.active=saved;camera.targetTexture=null;
            File.WriteAllBytes(Path.Combine(output,prefix+variant,filmFrame++.ToString("D4")+".png"),pixels.EncodeToPNG());
        }
        static void CheckPoseControls()
        {
            AnimationLabPoseWindow.OpenNormal();var window=EditorWindow.GetWindow<AnimationLabPoseWindow>();window.EditorTick();
            if(window.Draft==null||scenario.PoseDraft!=window.Draft)throw new InvalidOperationException("Normal draft is not connected to live Scenario");
            scenario.SelectCase(8);scenario.SetManualInput(Vector3.zero);scenario.Actor.SetFrozen(true);
            var original=window.Draft.idle.Get(4);var bone=scenario.PoseJoint(4);scenario.RefreshPose();var before=bone.localRotation;
            window.SetRotation(4,original*Quaternion.AngleAxis(1,Vector3.right));scenario.RefreshPose();var changed=bone.localRotation;
            if(Quaternion.Angle(before,changed)<.8f)throw new InvalidOperationException("Normal frozen joint edit was overwritten");
            scenario.RefreshPose();if(Quaternion.Angle(changed,bone.localRotation)>.002f)throw new InvalidOperationException("Correction accumulated on repeated evaluation");
            Undo.PerformUndo();scenario.RefreshPose();if(Quaternion.Angle(before,bone.localRotation)>.01f)throw new InvalidOperationException("Normal Undo did not restore output");
            Undo.PerformRedo();scenario.RefreshPose();if(Quaternion.Angle(changed,bone.localRotation)>.01f)throw new InvalidOperationException("Normal Redo did not restore output");
            window.SetStrength(0);scenario.RefreshPose();if(Quaternion.Angle(bone.localRotation,scenario.BaseJointRotation(4))>.01f)throw new InvalidOperationException("Normal strength zero is not identity");
            window.SetRotation(4,original);window.SetStrength(1);window.SaveChanges();window.Close();scenario.Actor.SetFrozen(false);
            if(scenario.PoseDraft!=null)throw new InvalidOperationException("Normal draft was not detached");
            scenario.Actor.gameObject.SetActive(false);scenario.Actor.gameObject.SetActive(true);
            if(!scenario.Actor.FreeWalk.InertialTransitions)throw new InvalidOperationException("Reactivation lost composite transition mode");
            scenario.RefreshPose();var held=scenario.PoseJoint(4).localRotation;
            for(int i=0;i<10;i++)scenario.RefreshPose();
            if(Quaternion.Angle(held,scenario.PoseJoint(4).localRotation)>.01f)throw new InvalidOperationException("Reactivated correction accumulated");
            var free=Resources.Load<AnimationLabFreeWalkData>("FreeWalkReview");var saved=free.poseProfile;var temporary=UnityEngine.Object.Instantiate(saved);
            try
            {
                temporary.idle.Set(4,Quaternion.AngleAxis(5,Vector3.right));free.poseProfile=temporary;
                scenario.SelectCase(8);scenario.SetManualInput(Vector3.forward);scenario.RefreshPose();
                scenario.Actor.StepReviewFrame(1f/120);
                if(scenario.Current.clip==free.idle||scenario.LegacyIdleContribution<.8f)throw new InvalidOperationException("Outgoing authored Idle FK vanished at Start entry");
                float previous=scenario.LegacyIdleContribution;
                scenario.SetManualInput(Vector3.zero);scenario.Actor.StepReviewFrame(1f/120);
                if(Mathf.Abs(scenario.LegacyIdleContribution-previous)>.2f)throw new InvalidOperationException("Interrupted correction weight jumped");
            }
            finally{free.poseProfile=saved;UnityEngine.Object.DestroyImmediate(temporary);scenario.SelectCase(8);}
            Debug.Log("NORMAL_POSE_CONTROLS live=true frozen=true noAccumulation=true undoRedo=true strengthZero=true saveDetach=true");
        }
        static void Finish()
        {
            Application.logMessageReceived-=Log;
            string suffix=PostureAuto?"-auto":PostureAblation?"-raw":Environment.GetCommandLineArgs().Contains("-firstStepPoseControls")?"-controls":video?"-film":"";
            SessionState.SetBool(Key,false);File.WriteAllText(Path.Combine(output,(Mixed?"mixed-":Posture?"posture-":Response?"normal-response-":"first-step-")+variant+suffix+".json"),JsonUtility.ToJson(result));
            Debug.Log("FIRST_STEP_DONE "+variant+" "+result.error);EditorApplication.Exit(result.error.Length==0?0:2);
        }
        static void Log(string message,string stack,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception)return;
            if(stack.Contains("UnityEditor.Search"))return;
            result.error+=message+"\n"+stack+"\n";
        }
    }
    [DefaultExecutionOrder(-500)] public sealed class AnimationLabFirstStepInput:MonoBehaviour {public Vector3 value;void Update(){AnimationLabFirstStepChecks.Feed();}}
}
#endif

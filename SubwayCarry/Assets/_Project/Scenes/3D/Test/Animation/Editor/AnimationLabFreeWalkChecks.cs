#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    [InitializeOnLoad]
    public static class AnimationLabFreeWalkChecks
    {
        const string Key="SubwayCarry.FreeWalk.Check";
        static AnimationLabCharacter actor;
        static AnimationLabWalkReview review;
        static float start,lastSample,freezeAt=-1,stoppedFor,wrongFor;
        static bool frozen,resumed,reused,finished,prepared,baseline;
        static Vector3 baselinePosition;
        static float minFoot=999,maxFoot=-999;
        static long cpuFrames,cpuTicks,cpuBytes;
        static int phases;
        static int lastFrame=-1,maxPoint;
        static Vector3 frozenRoot,frozenFoot;
        static Result result;
        static int poseClips;
        static Vector3 poseRoot,poseModel;
        static Quaternion poseRotation;
        static Transform[] sourceStarts,sourceEnds;
        [Serializable] public class Result
        {
            public string error="";
            public string scope="";
            public int samples,transitions,maxRoutePoint;
            public float meanDirectionError,maxDirectionError,maxUnexplainedStop,elapsed;
            public float maxRootDrift=-1;
            public bool humanoid,oneOwner,ikOff,motionMatchingOff,freezePreserved,reactivation,defaultPlaybackRestored;
            public int phasesVisited;
            public float footMotionRange;
            public long measuredPresentationFrames,presentationManagedBytes=-1;
            public double presentationMeanCpuMs=-1;
            public bool locomotionMeasured,nativeAnimationCpuMeasured;
            public int sourceBoneSegments,sourceInFrustum,targetInFrustum;
            public bool sourceRelativePoseChecked,rootDriftMeasured;
            public float sourceRelativeLimbPeak,bendDifferencePeak,sourceTimeDifference,maxModelRootDrift;
            public List<string> diagnostics=new List<string>();
        }
        static AnimationLabFreeWalkChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
        public static void Run()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Isolated batch only");
            SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(AnimationLabBuilder.Root+"/Normal.unity");
            if(!SessionState.GetBool(Key+".Pose",false))UnityEngine.Object.FindFirstObjectByType<AnimationLabWalkReview>().freePoseReview=false;
            EditorApplication.EnterPlaymode();
        }
        public static void RunPose(){SessionState.SetBool(Key+".Pose",true);Run();}
        static void Changed(PlayModeStateChange change)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(change==PlayModeStateChange.EnteredPlayMode){Time.captureDeltaTime=1f/60;result=new Result();start=Time.time;Application.logMessageReceived+=Log;}
        }
        static void Log(string message,string stack,LogType kind)
        {
            if(kind!=LogType.Error&&kind!=LogType.Exception)return;
            if(stack.Contains("UnityEditor.Search")){result.diagnostics.Add(message);return;}
            result.error+="\n"+message+"\n"+stack;
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||Time.frameCount==lastFrame||finished||result==null)return;
            lastFrame=Time.frameCount;
            try
            {
                if(SessionState.GetBool(Key+".Pose",false)){TickPose();return;}
                if(!prepared)
                {
                    actor=UnityEngine.Object.FindFirstObjectByType<AnimationLabCharacter>();review=UnityEngine.Object.FindFirstObjectByType<AnimationLabWalkReview>();
                    if(actor==null||review==null||Time.frameCount<5)return;
                    Require(actor.FreeWalk!=null&&actor.FreeWalk.Ready,"Free Walk did not connect to the existing Walk scene");
                    review.Automated=true;result.humanoid=actor.animator.isHuman;
                    result.ikOff=!actor.contacts.enabled;result.motionMatchingOff=actor.GetComponent<Metro3DMotionPlayer>()==null;
                    result.oneOwner=actor.animator.runtimeAnimatorController==null&&actor.FreeWalk.Ready;
                    Require(result.humanoid&&result.ikOff&&result.motionMatchingOff&&result.oneOwner,"Animation ownership/IK contract");
                    result.scope="Legacy route smoke, not an animation quality gate";result.locomotionMeasured=true;
                    start=Time.time;prepared=true;actor.FreeWalk.MeasurePerformance=true;
                }
                float elapsed=Time.time-start;
                if(baseline)
                {
                    actor.SetInput(Vector3.forward);actor.Automated=true;
                    if(elapsed<40)return;
                    Require(actor.FreeWalk==null&&actor.Ready&&!actor.animator.isHuman,"Default Generic presentation was not restored");
                    Require(actor.LegacyStateHash!=0&&Vector3.Distance(baselinePosition,actor.transform.position)>.2f,"Default controller/Motor did not resume");
                    result.defaultPlaybackRestored=true;Finish();return;
                }
                if(elapsed>2&&!frozen){frozen=true;freezeAt=Time.time;frozenRoot=actor.transform.position;frozenFoot=actor.animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;actor.SetFrozen(true);}
                if(frozen&&!resumed&&Time.time-freezeAt>.5f)
                {
                    Require(Vector3.Distance(frozenRoot,actor.transform.position)<.0001f,"Freeze changed capsule position");
                    Require(Vector3.Distance(frozenFoot,actor.animator.GetBoneTransform(HumanBodyBones.LeftFoot).position)<.001f,"Freeze changed foot pose");
                    actor.SetFrozen(false);resumed=true;result.freezePreserved=true;
                }
                if(elapsed>8&&!reused){reused=true;Vector3 point=actor.transform.position;CollectCpu();actor.gameObject.SetActive(false);actor.gameObject.SetActive(true);Require(actor.FreeWalk!=null&&actor.FreeWalk.Ready,"Reactivation did not reconnect presentation");Require(Vector3.Distance(point,actor.transform.position)<.0001f,"Reactivation teleported capsule");actor.FreeWalk.MeasurePerformance=true;result.reactivation=true;}
                maxPoint=Mathf.Max(maxPoint,review.FreePoint);
                if(!actor.Frozen&&Time.time>=lastSample+.05f)
                {
                    lastSample=Time.time;float speed=actor.ActualVelocity.magnitude;
                    phases|=1<<(int)actor.FreeWalk.State;
                    float foot=actor.animator.transform.InverseTransformPoint(actor.animator.GetBoneTransform(HumanBodyBones.LeftFoot).position).z;
                    minFoot=Mathf.Min(minFoot,foot);maxFoot=Mathf.Max(maxFoot,foot);
                    if(speed>.15f)
                    {
                        Vector3 side=actor.animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position-actor.animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
                        Vector3 up=actor.animator.GetBoneTransform(HumanBodyBones.Neck).position-actor.animator.GetBoneTransform(HumanBodyBones.Hips).position;
                        Vector3 forward=Vector3.ProjectOnPlane(Vector3.Cross(side,up),Vector3.up).normalized;
                        float angle=Vector3.Angle(forward,actor.ActualVelocity);
                        result.meanDirectionError+=angle;result.maxDirectionError=Mathf.Max(result.maxDirectionError,angle);result.samples++;
                        wrongFor=angle>60?wrongFor+.05f:0;
                        Require(wrongFor<.6f,"Sustained model/capsule heading disagreement");
                    }
                    stoppedFor=speed<.03f&&actor.DesiredVelocity.magnitude>.2f?stoppedFor+.05f:0;
                    result.maxUnexplainedStop=Mathf.Max(result.maxUnexplainedStop,stoppedFor);
                    Require(float.IsFinite(actor.transform.position.sqrMagnitude+actor.FreeWalk.Time),"Nonfinite movement/clip time");
                    Require(!actor.animator.applyRootMotion,"Root motion took capsule ownership");
                }
                if(elapsed>38)
                {
                    Require(maxPoint>=5,"Automatic review did not reach every route segment");
                    Require(result.maxUnexplainedStop<1,"Persistent blocked review route");
                    Require(phases==15,"Missing Idle/Start/Walk/Stop playback phase");
                    Require(maxFoot-minFoot>.1f,"Runtime foot pose did not animate");
                    CollectCpu();result.measuredPresentationFrames=cpuFrames;result.presentationManagedBytes=cpuBytes;
                    result.presentationMeanCpuMs=cpuTicks*1000.0/System.Diagnostics.Stopwatch.Frequency/Math.Max(1,cpuFrames);
                    result.phasesVisited=phases;result.footMotionRange=maxFoot-minFoot;
                    result.transitions=actor.FreeWalk.Transitions;result.maxRoutePoint=maxPoint;result.elapsed=elapsed;
                    result.meanDirectionError/=Mathf.Max(1,result.samples);
                    actor.DisableFreeWalk();baseline=true;baselinePosition=actor.transform.position;
                }
            }
            catch(Exception e){result.error+="\n"+e;Finish();}
        }
        static readonly HumanBodyBones[] limbStarts={HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg};
        static readonly HumanBodyBones[] limbEnds={HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot};
        static Quaternion BodyFrame(Animator a)
        {
            Vector3 side=a.GetBoneTransform(HumanBodyBones.RightUpperLeg).position-a.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
            return Quaternion.Inverse(Quaternion.LookRotation(Vector3.Cross(Vector3.ProjectOnPlane(side,Vector3.up),Vector3.up),Vector3.up));
        }
        static Vector3 Limb(Animator a,Quaternion frame,int i)=>frame*(a.GetBoneTransform(limbEnds[i]).position-a.GetBoneTransform(limbStarts[i]).position).normalized;
        static void TickPose()
        {
            if(!prepared)
            {
                actor=UnityEngine.Object.FindFirstObjectByType<AnimationLabCharacter>();review=UnityEngine.Object.FindFirstObjectByType<AnimationLabWalkReview>();
                if(actor==null||review==null||Time.frameCount<5)return;
                Require(actor.FreeWalk!=null&&actor.FreeWalk.Ready&&review.PoseReviewActive,"Synchronized source/target review not connected");
                review.Automated=true;prepared=true;start=Time.time;
                result.scope="Original recorded skeleton/skin and raw Generic curves A0 versus target A2; single Idle/Walk; Motor, transitions and IK OFF; no visual approval";
                result.humanoid=actor.animator.isHuman;
                result.ikOff=!actor.contacts.enabled;result.motionMatchingOff=actor.GetComponent<Metro3DMotionPlayer>()==null;
                result.oneOwner=actor.FreeWalk.Ready&&actor.animator.runtimeAnimatorController==null&&review.PoseSourceAnimator==null;
                Require(result.humanoid&&result.ikOff&&result.motionMatchingOff&&result.oneOwner,"Single pose ownership contract");
                poseRoot=actor.transform.position;poseRotation=actor.transform.rotation;poseModel=actor.animator.transform.localPosition;result.rootDriftMeasured=true;result.maxRootDrift=0;
                var bones=review.PoseSourceRoot.GetComponentsInChildren<Transform>();
                var starts=new[]{"LeftArm","LeftForeArm","RightArm","RightForeArm","LeftUpLeg","LeftLeg","RightUpLeg","RightLeg"};
                var ends=new[]{"LeftForeArm","LeftHand","RightForeArm","RightHand","LeftLeg","LeftFoot","RightLeg","RightFoot"};
                sourceStarts=new Transform[8];sourceEnds=new Transform[8];
                for(int i=0;i<8;i++)foreach(var bone in bones){if(bone.name==starts[i])sourceStarts[i]=bone;if(bone.name==ends[i])sourceEnds[i]=bone;}
                for(int i=0;i<8;i++)Require(sourceStarts[i]!=null&&sourceEnds[i]!=null,"Recorded source skeleton missing limb");
            }
            float elapsed=Time.time-start;
            result.maxRootDrift=Mathf.Max(result.maxRootDrift,Vector3.Distance(poseRoot,actor.transform.position));
            result.maxModelRootDrift=Mathf.Max(result.maxModelRootDrift,Vector3.Distance(poseModel,actor.animator.transform.localPosition));
            Require(result.maxRootDrift<.0001f&&result.maxModelRootDrift<.0001f&&Quaternion.Angle(poseRotation,actor.transform.rotation)<.001f,"Motor/root changed during single pose isolation");
            if(elapsed>2&&!frozen){frozen=true;freezeAt=Time.time;frozenFoot=actor.animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;actor.SetFrozen(true);}
            if(frozen&&!resumed&&Time.time-freezeAt>.5f){Require(Vector3.Distance(frozenFoot,actor.animator.GetBoneTransform(HumanBodyBones.LeftFoot).position)<.001f,"Pose freeze moved foot");actor.SetFrozen(false);resumed=true;result.freezePreserved=true;}
            if(elapsed>8&&!reused){reused=true;actor.gameObject.SetActive(false);actor.gameObject.SetActive(true);Require(actor.FreeWalk!=null&&actor.FreeWalk.Ready,"Pose reactivation failed");result.reactivation=true;return;}
            if(!actor.Frozen&&Time.time>=lastSample+.05f)
            {
                lastSample=Time.time;var side=sourceStarts[6].position-sourceStarts[4].position;
                var sf=Quaternion.Inverse(Quaternion.LookRotation(Vector3.Cross(Vector3.ProjectOnPlane(side,Vector3.up),Vector3.up),Vector3.up));var tf=BodyFrame(actor.animator);
                for(int i=0;i<limbStarts.Length;i++)
                {
                    var s=sf*(sourceEnds[i].position-sourceStarts[i].position).normalized;var t=Limb(actor.animator,tf,i);
                    if(Vector3.Angle(s,t)>20)Debug.Log("FREE_WALK_RUNTIME_LIMB_MISMATCH limb="+limbStarts[i]+" mapped="+actor.animator.GetBoneTransform(limbStarts[i]).name+" source="+s+" target="+t+" sourceTime="+review.PoseSourceTime+" targetTime="+actor.FreeWalk.Time);
                    result.sourceRelativeLimbPeak=Mathf.Max(result.sourceRelativeLimbPeak,Vector3.Angle(s,t));
                    if(i==0||i==2)Require(!(s.y<-.5f&&t.y>0),"Target arm raised against source pose");
                    if(i%2==0)result.bendDifferencePeak=Mathf.Max(result.bendDifferencePeak,Mathf.Abs(Vector3.Angle(s,sf*(sourceEnds[i+1].position-sourceStarts[i+1].position).normalized)-Vector3.Angle(t,Limb(actor.animator,tf,i+1))));
                }
                result.sourceTimeDifference=Mathf.Max(result.sourceTimeDifference,(float)Math.Abs(actor.FreeWalk.Time-review.PoseSourceTime));
                result.sourceBoneSegments=review.SourceBoneSegments;Require(result.sourceBoneSegments>20,"Source skeleton render geometry missing");
                var camera=actor.orbit.GetComponent<Camera>();int sourceVisible=0,targetVisible=0;
                for(int i=0;i<8;i++)
                {
                    Vector3 s=camera.WorldToViewportPoint(sourceEnds[i].position),t=camera.WorldToViewportPoint(actor.animator.GetBoneTransform(limbEnds[i]).position);
                    if(s.z>0&&s.x>0&&s.x<1&&s.y>0&&s.y<1)sourceVisible++;
                    if(t.z>0&&t.x>0&&t.x<1&&t.y>0&&t.y<1)targetVisible++;
                }
                result.sourceInFrustum=sourceVisible;result.targetInFrustum=targetVisible;
                Require(sourceVisible>=4&&targetVisible>=4,"Source/target comparison outside default camera frustum");
                Require(result.sourceRelativeLimbPeak<20&&result.bendDifferencePeak<20,"Runtime source-relative limb mismatch");
                Require(result.sourceTimeDifference<.001f,"Source/target phase drift");
                poseClips|=actor.FreeWalk.Clip.name=="Idle"?1:2;result.samples++;
            }
            if(elapsed>12)
            {
                Require(poseClips==3,"Both single Idle and Walk were not evaluated");
                result.sourceRelativePoseChecked=true;result.elapsed=elapsed;Finish();
            }
        }
        static void CollectCpu(){cpuFrames+=actor.FreeWalk.MeasuredFrames;cpuTicks+=actor.FreeWalk.MeasuredTicks;cpuBytes+=actor.FreeWalk.MeasuredBytes;}
        static void Finish()
        {
            if(finished)return;finished=true;SessionState.SetBool(Key,false);SessionState.SetBool(Key+".Pose",false);Application.logMessageReceived-=Log;
            // Logs/CrowdBehavior/Walking/Unity -> Logs/MotionMatching, not another project.
            string path=Path.Combine(Directory.GetParent(Application.dataPath).Parent.Parent.Parent.FullName,"MotionMatching/free-walk-runtime.json");
            File.WriteAllText(path,JsonUtility.ToJson(result,true));Debug.Log("FREE_WALK_RUNTIME "+JsonUtility.ToJson(result));
            EditorApplication.Exit(string.IsNullOrEmpty(result.error)?0:2);
        }
    }
}
#endif

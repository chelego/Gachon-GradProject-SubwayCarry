#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    [InitializeOnLoad]
    public static class AnimationLabScenarioChecks
    {
        const string Key="SubwayCarry.NormalSituations.Check";
        static AnimationLabScenario driver;
        static int field,caseIndex,lastFrame=-1,samples;
        static float began,lastReport,maxSlip,steadySlip,peakSpeed,peakModelDrift,peakFacingError;
        static Vector3 lastFoot,lastModel;
        static string error="";
        static bool finished,froze,unfroze,reused,walkedLoop;
        static Vector3 freezeRoot,freezeFoot;
        static float freezePhase;
        static int lastSwitch;
        static float lastSupport;
        public static void ReplayAudit()
        {
            Guard();var set=Resources.Load<AnimationLabMotionSet>("ReviewMotions");var free=Resources.Load<AnimationLabFreeWalkData>("FreeWalkReview");
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AnimationLabBuilder.Root+"/Prefabs/Character.prefab"));
            var animator=root.GetComponentInChildren<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            using(var player=new AnimationLabFreeWalkPlayer(animator,free))
            {
                var lf=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
                var ll=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);var rl=animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);var knee=animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                float leg=Vector3.Distance(ll.position,knee.position)+Vector3.Distance(knee.position,lf.position);
                foreach(var entry in set.entries.Where(e=>e.usable))
                {
                    root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);player.SelectExternal(entry.clip,0,0);
                    float rootResidual=0,footResidual=0,yawResidual=0,slip=0;Vector3 previous=Vector3.zero;float previousSupport=0;
                    for(int i=0;i<entry.frames.Length;i++)
                    {
                        var frame=entry.frames[i];float t=i/entry.sampleRate;root.transform.SetPositionAndRotation(frame.root,Quaternion.Euler(0,frame.yaw,0));player.EvaluateExternal(t,0,1,root.transform);
                        var body=Quaternion.LookRotation(Vector3.Cross(Vector3.ProjectOnPlane(rl.position-ll.position,Vector3.up),Vector3.up));var inv=Quaternion.Inverse(body);
                        rootResidual=Mathf.Max(rootResidual,Vector3.Distance(inv*(hips.position-animator.transform.position)/leg,frame.hips));
                        footResidual=Mathf.Max(footResidual,Vector3.Distance(inv*(lf.position-hips.position)/leg,frame.leftFoot));
                        yawResidual=Mathf.Max(yawResidual,Mathf.Abs(Mathf.DeltaAngle(Mathf.DeltaAngle(root.transform.eulerAngles.y,body.eulerAngles.y),frame.bodyYaw)));
                        if(entry.label=="Walking"&&i%10==0)Debug.Log($"REPLAY_DETAIL t={t:F2} animator={animator.transform.eulerAngles} root={root.transform.eulerAngles} body={body.eulerAngles.y:F2} expectedBody={frame.bodyYaw:F2} foot={inv*(lf.position-hips.position)/leg} expectedFoot={frame.leftFoot} hips={inv*(hips.position-animator.transform.position)/leg} expectedHips={frame.hips}");
                        if(i>0&&frame.leftSupport>.8f&&previousSupport>.8f)slip=Mathf.Max(slip,Vector3.ProjectOnPlane(lf.position-previous,Vector3.up).magnitude*entry.sampleRate);
                        previous=lf.position;previousSupport=frame.leftSupport;
                        var before=lf.position;player.EvaluateExternal(t,0,1,root.transform);Require(Vector3.Distance(before,lf.position)<.0001f,"Repeated frame conversion accumulated");
                    }
                    Debug.Log($"REPLAY_AUDIT clip={entry.label} hipsFeatureResidual={rootResidual:F4} footFeatureResidual={footResidual:F4} bodyYawResidual={yawResidual:F3} inferredSupportPeakSlip={slip:F3}");
                    Require(rootResidual<.01f&&footResidual<.01f&&yawResidual<.1f,"Native pose/root frame mismatch: "+entry.label);
                }
            }
            UnityEngine.Object.DestroyImmediate(root);EditorApplication.Exit(0);
        }
        static AnimationLabScenarioChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
        static void Guard()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Existing isolated QA only");
        }
        public static void Prepare()
        {
            Guard();string root=AnimationLabBuilder.Root;
            if(File.Exists(root+"/Walk.unity")&&!File.Exists(root+"/Normal.unity"))
            {
                string result=AssetDatabase.MoveAsset(root+"/Walk.unity",root+"/Normal.unity");if(result.Length>0)throw new IOException(result);
            }
            foreach(string name in new[]{"Run","Sit","Lean","Stairs"})if(File.Exists(root+"/"+name+".unity"))AssetDatabase.DeleteAsset(root+"/"+name+".unity");
            AnimationLabBuilder.BuildMissing();AssetDatabase.SaveAssets();Debug.Log("NORMAL_SITUATIONS_PREPARED");EditorApplication.Exit(0);
        }
        public static void Run()
        {
            Guard();SessionState.SetBool(Key,true);SessionState.SetInt(Key+".Field",0);SessionState.SetInt(Key+".Case",0);
            EditorSceneManager.OpenScene(AnimationLabBuilder.Root+"/Normal.unity");EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){Time.captureDeltaTime=1f/60;Application.logMessageReceived+=Log;field=SessionState.GetInt(Key+".Field",0);caseIndex=SessionState.GetInt(Key+".Case",0);}
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                Application.logMessageReceived-=Log;
                if(SessionState.GetInt(Key+".Field",0)>1){SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;}
                EditorSceneManager.OpenScene(AnimationLabBuilder.Root+"/Situations.unity");EditorApplication.EnterPlaymode();
            }
        }
        static void Log(string text,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error){if(stack.Contains("UnityEditor.Search"))return;error+=text+"\n"+stack;}}
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static void Begin()
        {
            driver.SelectCase(caseIndex);began=Time.time;lastReport=0;samples=0;maxSlip=steadySlip=peakSpeed=peakModelDrift=peakFacingError=0;froze=unfroze=reused=walkedLoop=false;lastSwitch=driver.Switches;
            lastFoot=driver.Actor.animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;lastModel=driver.Actor.animator.transform.localPosition;lastSupport=0;
        }
        static void Tick()
        {
            if(finished||!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            try
            {
                if(driver==null)
                {
                    if(Time.frameCount<5)return;driver=UnityEngine.Object.FindFirstObjectByType<AnimationLabScenario>();
                    Require(driver!=null&&driver.Current!=null,"Normal/Situations scene did not connect to native motion playback");
                    driver.Automated=true;Begin();
                    Require(driver.Actor.animator.isHuman&&driver.Actor.animator.avatar.isValid,"Target Avatar");
                    Require(!driver.Actor.animator.applyRootMotion&&driver.Actor.animator.runtimeAnimatorController==null,"Single Motor/graph ownership");
                    Require(driver.Data.entries.Count(e=>e.mirrored)==3,"Mirrored start/stop/turn data missing");
                    Require(driver.Data.entries.Any(e=>e.usable&&(e.uses&AnimationLabMotionSet.Use.Strafe)!=0),"No true lateral source");
                }
                Require(error.Length==0,error);
                var actor=driver.Actor;float elapsed=Time.time-began;
                if(driver.Current.loop&&(driver.Current.uses&AnimationLabMotionSet.Use.Forward)!=0&&driver.Blend>=1)walkedLoop=true;
                if(field==0&&caseIndex==0)
                {
                    if(!froze&&elapsed>=3.4f){actor.SetFrozen(true);freezeRoot=actor.transform.position;freezeFoot=actor.animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;freezePhase=driver.TimeInClip;froze=true;}
                    if(froze&&!unfroze){Require(Vector3.Distance(freezeRoot,actor.transform.position)<.0001f&&Mathf.Abs(freezePhase-driver.TimeInClip)<.0001f,"F6 advanced root/time");Require(Vector3.Distance(freezeFoot,actor.animator.GetBoneTransform(HumanBodyBones.LeftFoot).position)<.0001f,"F6 pose drift");if(elapsed>=3.7f){actor.SetFrozen(false);unfroze=true;}}
                    if(!reused&&elapsed>=5){var identity=driver.Data;var clip=driver.Current.clip;float phase=driver.TimeInClip;var before=actor.animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;actor.gameObject.SetActive(false);actor.gameObject.SetActive(true);Require(driver.Data==identity&&actor.FreeWalk.Clip==clip&&Mathf.Abs(driver.TimeInClip-phase)<.001f,"Reactivation lost data/playback");Require(Vector3.Distance(before,actor.animator.GetBoneTransform(HumanBodyBones.LeftFoot).position)<.001f,"Reactivation displayed a bind/idle pose");reused=true;}
                }
                Require(float.IsFinite(actor.transform.position.x)&&float.IsFinite(actor.transform.rotation.w),"Invalid root");
                var foot=actor.animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                if(driver.LeftSupport>.8f&&lastSupport>.8f&&samples>0)maxSlip=Mathf.Max(maxSlip,Vector3.ProjectOnPlane(foot-lastFoot,Vector3.up).magnitude/(1f/60));
                if(driver.LeftSupport>.8f&&lastSupport>.8f&&driver.Blend>=1&&driver.Switches==lastSwitch&&!actor.Frozen&&samples>0)steadySlip=Mathf.Max(steadySlip,Vector3.ProjectOnPlane(foot-lastFoot,Vector3.up).magnitude/(1f/60));
                lastFoot=foot;lastSupport=driver.LeftSupport;
                lastSwitch=driver.Switches;
                if(actor.ActualVelocity.magnitude>.3f&&(driver.Current.uses&AnimationLabMotionSet.Use.Forward)!=0&&driver.Blend>=1)
                {
                    var left=actor.animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);var right=actor.animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                    var body=Vector3.Cross(Vector3.ProjectOnPlane(right.position-left.position,Vector3.up),Vector3.up);peakFacingError=Mathf.Max(peakFacingError,Vector3.Angle(body,actor.ActualVelocity));
                }
                peakModelDrift=Mathf.Max(peakModelDrift,Vector3.Distance(lastModel,actor.animator.transform.localPosition));peakSpeed=Mathf.Max(peakSpeed,actor.ActualVelocity.magnitude);samples++;
                if(elapsed-lastReport>.5f){lastReport=elapsed;Debug.Log($"REVIEW_SAMPLE field={field} case={caseIndex} t={elapsed:F1} clip={driver.Current.label} phase={driver.TimeInClip:F2} speed={actor.ActualVelocity.magnitude:F2} root={actor.transform.position} yaw={actor.transform.eulerAngles.y:F1} reason={driver.Reason}");}
                if(elapsed<10)return;
                Require(peakModelDrift<.001f,"Animation moved the visual root outside the Motor");
                if(field==0&&caseIndex<5)Require(peakSpeed>.3f,"Normal case did not actually move");
                if(field==0&&caseIndex==0){Require(actor.ActualVelocity.magnitude<.15f,"Straight case did not stop");Require(walkedLoop,"Start motion repeated without reaching a real walking cycle");}
                double cpu=driver.Search.Searches==0?0:driver.Search.Ticks*1000d/System.Diagnostics.Stopwatch.Frequency/driver.Search.Searches;
                double pose=driver.PoseFrames==0?0:driver.PoseTicks*1000d/System.Diagnostics.Stopwatch.Frequency/driver.PoseFrames;
                Debug.Log($"REVIEW_CASE field={field} case={caseIndex} samples={samples} transitions={driver.Switches} peakSpeed={peakSpeed:F3} peakPelvisFacingVsMotion={peakFacingError:F2} inferredLeftSupportPeakSlip={maxSlip:F3} steadySupportPeakSlip={steadySlip:F3} modelDrift={peakModelDrift:F6} searchMeanMs={cpu:F3} searchBytes={driver.Search.Bytes} presentationMeanMs={pose:F3} presentationBytes={driver.PoseBytes} freeze={unfroze} reuse={reused} missing={driver.Missing}");
                caseIndex++;int limit=field==0?8:6;
                if(caseIndex<limit){Begin();return;}
                field++;SessionState.SetInt(Key+".Field",field);SessionState.SetInt(Key+".Case",0);driver=null;
                EditorApplication.ExitPlaymode();
            }
            catch(Exception e){finished=true;SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(2);}
        }
    }
}
#endif

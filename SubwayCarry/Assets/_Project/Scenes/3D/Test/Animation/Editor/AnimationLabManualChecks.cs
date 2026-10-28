#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Timestamped virtual controls in the existing isolated Unity. Never sends OS input.
    [InitializeOnLoad]
    public static class AnimationLabManualChecks
    {
        const string Key="SubwayCarry.ManualFeedback.Check";
        static AnimationLabScenario scenario;
        static AnimationLabManualInput feeder;
        static float begin,previousTime;
        static int caseIndex,rateIndex,lastFrame=-1,lastSwitch;
        static long nextFrame;
        static Vector3 lastRoot,lastLeft,lastRight,lastHips,lastVelocity;
        static float lastYaw,lastYawRate;
        static CaseResult current;
        static Results results;
        static string fault="";
        static readonly int[] Rates={30,60,144,0};
        static readonly int[] VariableMilliseconds={7,14,5,28,9,17,45,8,11,6,22};
        static readonly string[] Names={"W start/stop","W-WD-W","W-D-W","W-A-W","short left/right","camera yaw + W","moving W-S","standing S","cancel/reinput"};
        [Serializable] public class Sample
        {
            public float t,dt,camera,inputX,inputY,yaw,clipTime,oldTime,blend,query,sourceL,sourceR,rootYawRate;
            public Vector3 desired,velocity,root,left,right,hips;
            public int clip,oldClip,decision,switches;
        }
        [Serializable] public class CaseResult
        {
            public string name;public int fps;
            public int samples,zeroMotionFrames,continuousFrames,switches;
            public float maxFootStep,maxPelvisStep,maxFootSpeed,maxFootVelocityJump,maxYawRateJump,maxDirectionError,firstMotion=-1,firstHeading=-1,firstLanding=-1;
            public List<Sample> trace=new List<Sample>();
        }
        [Serializable] public class Results
        {
            public string error="",scope="Native Unity, real variable deltaTime, virtual raw controls, all transition frames included; not user visual approval";
            public List<CaseResult> cases=new List<CaseResult>();
            public string[] clips;
        }
        static AnimationLabManualChecks(){EditorApplication.update+=Tick;}
        public static void Run()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Existing isolated QA only");
            SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(AnimationLabBuilder.Root+"/Normal.unity");EditorApplication.EnterPlaymode();
        }
        static void Begin()
        {
            scenario.SelectCase(8);scenario.Automated=true;scenario.Actor.Automated=true;
            begin=Time.unscaledTime;previousTime=begin;lastRoot=scenario.transform.position;lastLeft=Foot(true);lastRight=Foot(false);lastHips=Hip();lastVelocity=Vector3.zero;lastYaw=scenario.transform.eulerAngles.y;lastYawRate=0;lastSwitch=scenario.Switches;
            current=new CaseResult{name=Names[caseIndex],fps=Rates[rateIndex]};results.cases.Add(current);
        }
        static Vector3 Foot(bool left)=>scenario.Actor.animator.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot).position;
        static Vector3 Hip()=>scenario.Actor.animator.GetBoneTransform(HumanBodyBones.Hips).position;
        public static void Feed()
        {
            if(scenario==null||current==null)return;
            float t=Time.unscaledTime-begin;Vector2 raw=Vector2.zero;float yaw=0;
            if(t>=.3f&&t<3.4f)
            {
                raw=Vector2.up;
                if(caseIndex==1&&t>=1.25f&&t<2.6f)raw=new Vector2(1,1);
                if(caseIndex==2&&t>=1.25f&&t<2.8f)raw=Vector2.right;
                if(caseIndex==3&&t>=1.25f&&t<2.8f)raw=Vector2.left;
                if(caseIndex==4&&t>=1.25f&&t<2.8f)raw=new Vector2(((int)((t-1.25f)/.16f))%2==0?1:-1,1);
                if(caseIndex==5)yaw=Mathf.Clamp((t-1.25f)*70,0,140);
                if(caseIndex==6&&t>=1.25f)raw=Vector2.down;
                if(caseIndex==7)raw=Vector2.down;
                if(caseIndex==8){if(t>=1.2f&&t<1.65f)raw=Vector2.right;else if(t>=1.65f&&t<1.9f)raw=Vector2.zero;else if(t>=1.9f&&t<2.45f)raw=Vector2.left;}
            }
            scenario.Actor.orbit.yaw=yaw;
            var forward=Quaternion.Euler(0,yaw,0)*Vector3.forward;
            scenario.SetManualInput(forward*raw.y+Vector3.Cross(Vector3.up,forward)*raw.x);
            feeder.raw=raw;feeder.basis=yaw;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||lastFrame==Time.frameCount)return;
            lastFrame=Time.frameCount;
            try
            {
                if(scenario==null)
                {
                    if(Time.frameCount<6)return;
                    scenario=UnityEngine.Object.FindFirstObjectByType<AnimationLabScenario>();
                    if(scenario==null||scenario.Current==null)throw new InvalidOperationException("Manual harness did not initialize");
                    results=new Results{clips=Array.ConvertAll(scenario.Data.entries,e=>e.label)};
                    Time.captureDeltaTime=0;Application.SetStackTraceLogType(LogType.Log,StackTraceLogType.None);
                    feeder=scenario.gameObject.AddComponent<AnimationLabManualInput>();Application.logMessageReceived+=Log;Begin();
                }
                if(fault.Length>0)throw new InvalidOperationException(fault);
                float now=Time.unscaledTime,t=now-begin,dt=now-previousTime;if(dt<=0)return;
                var root=scenario.transform.position;var left=Foot(true);var right=Foot(false);var hips=Hip();
                Vector3 velocity=(root-lastRoot)/dt;float yaw=scenario.transform.eulerAngles.y,yawRate=Mathf.DeltaAngle(lastYaw,yaw)/dt;
                var footDelta=Vector3.Max(Abs(left-lastLeft),Abs(right-lastRight));
                current.samples++;current.maxFootStep=Mathf.Max(current.maxFootStep,footDelta.magnitude);current.maxPelvisStep=Mathf.Max(current.maxPelvisStep,(hips-lastHips).magnitude);
                float footSpeed=Mathf.Max((left-lastLeft).magnitude,(right-lastRight).magnitude)/dt;current.maxFootSpeed=Mathf.Max(current.maxFootSpeed,footSpeed);
                current.maxFootVelocityJump=Mathf.Max(current.maxFootVelocityJump,(velocity-lastVelocity).magnitude);current.maxYawRateJump=Mathf.Max(current.maxYawRateJump,Mathf.Abs(yawRate-lastYawRate));
                if(t>.5f&&scenario.Desired.magnitude>.4f&&scenario.Actor.ActualVelocity.magnitude>.4f){current.continuousFrames++;if((root-lastRoot).sqrMagnitude<1e-10f)current.zeroMotionFrames++;}
                if(t>.3f&&current.firstMotion<0&&velocity.magnitude>.15f)current.firstMotion=t-.3f;
                if(t>1.25f&&current.firstHeading<0&&Mathf.Abs(Mathf.DeltaAngle(0,yaw))>5)current.firstHeading=t-1.25f;
                if(scenario.Desired.sqrMagnitude>.01f)current.maxDirectionError=Mathf.Max(current.maxDirectionError,Vector3.Angle(scenario.transform.forward,scenario.Desired));
                current.trace.Add(new Sample{t=t,dt=dt,camera=feeder.basis,inputX=feeder.raw.x,inputY=feeder.raw.y,yaw=yaw,clipTime=scenario.TimeInClip,oldTime=scenario.PreviousTime,blend=scenario.Blend,query=scenario.QueryTime,sourceL=scenario.LeftSupport,sourceR=scenario.RightSupport,desired=scenario.Desired,velocity=velocity,root=root,left=left,right=right,hips=hips,clip=scenario.EntryIndex,oldClip=scenario.PreviousIndex,decision=scenario.DecisionStatus,switches=scenario.Switches,rootYawRate=yawRate});
                previousTime=now;lastRoot=root;lastLeft=left;lastRight=right;lastHips=hips;lastYaw=yaw;lastYawRate=yawRate;lastVelocity=velocity;lastSwitch=scenario.Switches;
                if(t>=4.5f)
                {
                    current.switches=scenario.Switches;Debug.Log($"MANUAL_CASE {current.fps} {current.name} frames={current.samples} stationary={current.zeroMotionFrames}/{current.continuousFrames} footStep={current.maxFootStep:F3} yawJump={current.maxYawRateJump:F1} switches={current.switches}");
                    caseIndex++;if(caseIndex>=Names.Length){caseIndex=0;rateIndex++;}
                    if(rateIndex>=Rates.Length){Finish();return;}Begin();
                }
                // QA-only throttling creates actual wall-clock render dt, not captureDeltaTime.
                int ms=Rates[rateIndex]>0?Mathf.RoundToInt(1000f/Rates[rateIndex]):VariableMilliseconds[Time.frameCount%VariableMilliseconds.Length];
                Thread.Sleep(ms);
            }
            catch(Exception e){if(results==null)results=new Results();results.error=e.ToString();Debug.LogException(e);Finish();}
        }
        static Vector3 Abs(Vector3 v)=>new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
        static void Log(string m,string stack,LogType kind){if((kind==LogType.Exception||kind==LogType.Error)&&!stack.Contains("UnityEditor.Search"))fault+=m+"\n"+stack;}
        static void Finish()
        {
            SessionState.SetBool(Key,false);Application.logMessageReceived-=Log;
            string variant=Array.IndexOf(Environment.GetCommandLineArgs(),"-manualAfter")>=0?"after":"before";
            string path=Path.GetFullPath("../../../../MotionMatching/manual-feedback-"+variant+".json");
            // QA project is Logs/CrowdBehavior/Walking/Unity; resolve the existing log area explicitly.
            path=Path.Combine(Directory.GetParent(Application.dataPath).Parent.Parent.Parent.FullName,"MotionMatching/manual-feedback-"+variant+".json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(results));
            Debug.Log("MANUAL_RESULTS "+path+" "+results.error);EditorApplication.Exit(results.error.Length==0?0:2);
        }
    }
    [DefaultExecutionOrder(-500)]
    public sealed class AnimationLabManualInput:MonoBehaviour
    {
        public Vector2 raw;public float basis;
        void Update(){AnimationLabManualChecks.Feed();}
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Profiling;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    [InitializeOnLoad] public static class AnimationLabPhoneReplayChecks
    {
        const string Key="SubwayCarry.PhoneReplay.QA";
        static bool leaving,failed;static int frame=-1,verified;static float began,maxError;
        static GameObject sampler;static Transform wrist,sampledWrist;
        static ProfilerRecorder poseRecorder,upperRecorder;
        static readonly List<double> poseMs=new List<double>(1024),upperMs=new List<double>(1024);
        static AnimationLabPhoneReplayChecks(){EditorApplication.update+=Tick;}
        public static void Run()=>Begin(false);
        public static void Baseline()=>Begin(true);
        static void Begin(bool baseline)
        {
            AnimationLabPhoneBuild.Guard();SessionState.SetBool(Key+".Baseline",baseline);SessionState.SetInt(Key,1);
            EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.Root+"/Normal.unity");EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(SessionState.GetInt(Key,0)==0)return;
            if(leaving){if(!EditorApplication.isPlaying){SessionState.SetInt(Key,0);EditorApplication.Exit(failed?2:0);}return;}
            if(!EditorApplication.isPlaying||AnimationLabPhoneCase.Active==null||frame==Time.frameCount)return;frame=Time.frameCount;
            try
            {
                var trial=AnimationLabPhoneCase.Active;var actor=trial.Passenger;bool baseline=SessionState.GetBool(Key+".Baseline",false);
                if(sampler==null)
                {
                    Time.captureFramerate=30;Application.targetFrameRate=30;began=Time.time;actor.PhoneLayer.PresentationEnabled=!baseline;
                    sampler=UnityEngine.Object.Instantiate(trial.Data.rig);sampler.hideFlags=HideFlags.HideAndDontSave;
                    foreach(var component in sampler.GetComponentsInChildren<Component>(true))if(!(component is Transform))UnityEngine.Object.DestroyImmediate(component);
                    wrist=actor.transform.Find(trial.Data.rightHandPath);sampledWrist=sampler.transform.Find(trial.Data.rightHandPath);
                    poseRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"SubwayCarry.PhoneCase.Pose",1);upperRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"SubwayCarry.PhoneCase.UpperBody",1);
                }
                if(!baseline&&actor.PhoneLayer.Weight>.999f)
                {trial.Data.upperBody.SampleAnimation(sampler,actor.PhoneLayer.ClipTime);maxError=Mathf.Max(maxError,Quaternion.Angle(wrist.localRotation,sampledWrist.localRotation));verified++;}
                if(Time.time-began>2){if(poseRecorder.Valid&&poseRecorder.Count>0)poseMs.Add(poseRecorder.LastValue*1e-6);if(upperRecorder.Valid&&upperRecorder.Count>0)upperMs.Add(upperRecorder.LastValue*1e-6);}
                if(Time.time-began<30)return;
                var root=actor.transform.position;var legs=actor.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("thigh")||t.name.Contains("shin")||t.name.Contains("foot")).ToArray();var rotations=legs.Select(t=>t.localRotation).ToArray();actor.PhoneLayer.RestoreBase();
                bool unchanged=Vector3.Distance(root,actor.transform.position)<.00001f&&legs.Select((t,i)=>Quaternion.Angle(t.localRotation,rotations[i])<.01f).All(v=>v);
                var result=new Result{revision=trial.Data.publishedRevision,verifiedFrames=verified,maxWristErrorDegrees=maxError,rootAndLegsUnchanged=unchanged,arrived=trial.State==AnimationLabPhoneCase.Attention.Arrived,poseMedianMs=Median(poseMs),poseMaxMs=poseMs.Count>0?poseMs.Max():-1,upperMedianMs=Median(upperMs),upperMaxMs=upperMs.Count>0?upperMs.Max():-1,profileSamples=poseMs.Count};
                string directory=Path.GetFullPath("../../../MotionMatching/PhonePassenger");Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,baseline?"baseline.json":"normal-replay.json"),JsonUtility.ToJson(result,true));Debug.Log("PHONE_NORMAL_REPLAY "+JsonUtility.ToJson(result));
                failed=!result.arrived||!unchanged||!baseline&&(verified<60||maxError>.2f);if(failed)Debug.LogError("Phone Normal replay did not meet acceptance.");Finish();
            }
            catch(Exception e){Debug.LogException(e);failed=true;Finish();}
        }
        static double Median(List<double> rows){if(rows.Count==0)return -1;var sorted=rows.ToArray();Array.Sort(sorted);return sorted[sorted.Length/2];}
        static void Finish(){Time.captureFramerate=0;poseRecorder.Dispose();upperRecorder.Dispose();if(sampler!=null)UnityEngine.Object.DestroyImmediate(sampler);leaving=true;EditorApplication.isPlaying=false;}
        [Serializable] public sealed class Result{public string revision;public int verifiedFrames,profileSamples;public float maxWristErrorDegrees;public bool rootAndLegsUnchanged,arrived;public double poseMedianMs,poseMaxMs,upperMedianMs,upperMaxMs;}
    }
}
#endif

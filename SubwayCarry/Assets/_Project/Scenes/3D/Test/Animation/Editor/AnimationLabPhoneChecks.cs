#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    [InitializeOnLoad] public static class AnimationLabPhoneChecks
    {
        const string Key="SubwayCarry.PhoneCase.QA";
        static bool leaving,failed;static MediaEncoder encoder;static int lastFrame=-1;static float began;static int samples;static float maxHeading;static double poseTime;
        static Camera camera;static RenderTexture rt;static Texture2D pixels;static string dir;
        static AnimationLabPhoneChecks(){EditorApplication.update+=Tick;}
        public static void Run()
        {
            Begin(true,false);
        }
        public static void Dry()=>Begin(false,false);
        public static void Variant()=>Begin(false,true);
        static void Begin(bool capture,bool variant)
        {
            AnimationLabPhoneBuild.Guard();try{var data=AnimationLabPhoneBuild.Build();SessionState.SetString(Key+".Original",JsonUtility.ToJson(data));data.pillar=new Rect();
                if(variant){Vector2 Rotate(Vector2 p){var q=Quaternion.Euler(0,67,0)*new Vector3(p.x,0,p.y);return new Vector2(q.x+.2f,q.z-.15f);}data.start=Rotate(data.start);data.destination=Rotate(data.destination);data.peerStart=Rotate(new Vector2(.1f,4.5f));data.peerDestination=Rotate(data.peerDestination);}
                EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);SessionState.SetBool(Key+".Capture",capture);SessionState.SetBool(Key+".Variant",variant);SessionState.SetInt(Key,1);EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.Root+"/Situations.unity");EditorApplication.isPlaying=true;}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
        }
        static void Tick()
        {
            if(SessionState.GetInt(Key,0)==0)return;
            if(leaving){if(!EditorApplication.isPlaying){var data=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);JsonUtility.FromJsonOverwrite(SessionState.GetString(Key+".Original",""),data);EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);SessionState.SetInt(Key,0);EditorApplication.Exit(failed?2:0);}return;}
            if(!EditorApplication.isPlaying||AnimationLabPhoneCase.Active==null||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            try
            {
                var trial=AnimationLabPhoneCase.Active;
                bool capture=SessionState.GetBool(Key+".Capture",false);
                if(camera==null){camera=Camera.main;Time.captureFramerate=30;Application.targetFrameRate=30;began=Time.time;if(capture){rt=new RenderTexture(1920,1080,24);pixels=new Texture2D(1920,1080,TextureFormat.RGBA32,false);}dir=Path.GetFullPath("../../../MotionMatching/PhonePassenger/frames");Directory.CreateDirectory(dir);if(capture)encoder=new MediaEncoder(Path.Combine(Path.GetDirectoryName(dir),"승객_폰보기_회피_도착.mp4"),new VideoTrackAttributes{width=1920,height=1080,frameRate=new MediaRational(30),includeAlpha=false});Debug.Log("PHONE_CASE_RUNTIME_STARTED "+dir);}
                float elapsed=Time.time-began;var actor=trial.Passenger;
                if(actor.ActualVelocity.magnitude>.15f)maxHeading=Mathf.Max(maxHeading,Vector3.Angle(actor.ActualVelocity,actor.transform.forward));
                if(trial.Agent.MovementTarget!=trial.Data.destination)throw new InvalidOperationException("Phone transition changed destination");
                if(capture){var previous=camera.targetTexture;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pixels.Apply();RenderTexture.active=null;camera.targetTexture=previous;if(!encoder.AddFrame(pixels))throw new InvalidOperationException("Video frame encoding failed");if(samples%90==0)File.WriteAllBytes(Path.Combine(dir,samples.ToString("D4")+".png"),pixels.EncodeToPNG());if(samples==90)CaptureGrip(trial);}samples++;
                if(elapsed<30)return;
                var result=new Result{samples=samples,duration=elapsed,sawPhone=trial.SawPhone,sawRisk=trial.SawRisk,resumedPhone=trial.ResumedPhone,arrived=trial.State==AnimationLabPhoneCase.Attention.Arrived,minSeparation=trial.MinimumSeparation,maxHeading=maxHeading,distance=trial.Agent.DistanceToTarget,paths=trial.Navigation.PathCount,failures=trial.Navigation.FailedPathCount,transitions=trial.AttentionChanges};
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(dir),SessionState.GetBool(Key+".Variant",false)?"variant.json":"case.json"),JsonUtility.ToJson(result,true));Debug.Log("PHONE_CASE_RUNTIME_RESULT "+JsonUtility.ToJson(result));
                failed=!result.sawPhone||!result.sawRisk||!result.resumedPhone||!result.arrived||result.minSeparation<.49f||result.maxHeading>10;if(failed)Debug.LogError("Phone case acceptance failed");encoder?.Dispose();encoder=null;Time.captureFramerate=0;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);leaving=true;EditorApplication.isPlaying=false;
            }
            catch(Exception e){encoder?.Dispose();encoder=null;Debug.LogException(e);SessionState.SetInt(Key,0);EditorApplication.Exit(2);}
        }
        static void CaptureGrip(AnimationLabPhoneCase trial)
        {
            var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            try
            {
                var actor=trial.Passenger.transform;var target=actor.position+Vector3.up*1.25f;var eye=target+actor.forward*1.35f+actor.right*.65f+Vector3.up*.12f;
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));camera.fieldOfView=45;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(dir),"phone-grip.png"),pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=null;camera.targetTexture=null;camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
        }
        [Serializable] public sealed class Result{public int samples,paths,failures,transitions;public float duration,minSeparation,maxHeading,distance;public bool sawPhone,sawRisk,resumedPhone,arrived;}
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Media;
using UnityEngine;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    [InitializeOnLoad] public static class AnimationLabPhoneSetChecks
    {
        const string Key="SubwayCarry.PhoneSet.QA";
        static int frame=-1,index,samples,checks;static bool leaving,failed;static float began,maxLeft,maxHandStep;static Vector3 previousHand,previousRoot;static string directory;
        static Camera camera;static Texture2D pixels;static RenderTexture rt;static MediaEncoder encoder;
        static readonly float[] Times={0,4,8,12,18,23,27,31,32,37};
        static readonly int[] Modes={2,1,4,3,1,2,4,1,4,1};
        static readonly float[] VerifyTimes={3.5f,7.5f,11.5f,17,22.5f,26.5f,30.5f,36.5f,41.5f};
        static readonly AnimationLabPhoneMode[] Expected={AnimationLabPhoneMode.Read,AnimationLabPhoneMode.Stored,AnimationLabPhoneMode.Carry,AnimationLabPhoneMode.Landscape,AnimationLabPhoneMode.Stored,AnimationLabPhoneMode.Read,AnimationLabPhoneMode.Carry,AnimationLabPhoneMode.Carry,AnimationLabPhoneMode.Stored};
        static AnimationLabPhoneSetChecks(){EditorApplication.update+=Tick;}
        public static void Run()
        {
            AnimationLabPhoneBuild.Guard();SessionState.SetInt(Key,1);EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.Root+"/Situations.unity");EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(SessionState.GetInt(Key,0)==0)return;if(leaving){if(!EditorApplication.isPlaying){SessionState.SetInt(Key,0);EditorApplication.Exit(failed?2:0);}return;}
            if(!EditorApplication.isPlaying||AnimationLabPhoneCase.Active==null||frame==Time.frameCount)return;frame=Time.frameCount;
            try
            {
                var trial=AnimationLabPhoneCase.Active;var layer=trial.Passenger.PhoneLayer;
                if(camera==null)
                {
                    if(!trial.Data.HasActionSet)throw new InvalidOperationException("New action set was not loaded");
                    began=Time.time;Time.captureFramerate=30;Application.targetFrameRate=30;camera=Camera.main;rt=new RenderTexture(1280,720,24);pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);
                    directory=Path.GetFullPath("../../../MotionMatching/PhonePassenger/PhoneSet");Directory.CreateDirectory(directory);encoder=new MediaEncoder(Path.Combine(directory,"폰동작_한씬검수.mp4"),new VideoTrackAttributes{width=1280,height=720,frameRate=new MediaRational(30),includeAlpha=false});
                    previousHand=trial.Passenger.transform.Find(trial.Data.rightHandPath).position;previousRoot=trial.Passenger.transform.position;
                    Debug.Log("PHONE_SET_RUNTIME_STARTED sources="+trial.Data.actions.Length);
                }
                float elapsed=Time.time-began;
                if(index<Times.Length&&elapsed>=Times[index]){trial.SetReviewMode(Modes[index]);Debug.Log("PHONE_SET_MODE t="+elapsed+" mode="+Modes[index]);index++;}
                if(checks<VerifyTimes.Length&&elapsed>=VerifyTimes[checks])
                {
                    if(layer.IsTransitioning||layer.SettledMode!=Expected[checks]||layer.PhoneInHand!=(Expected[checks]!=AnimationLabPhoneMode.Stored))throw new InvalidOperationException("Wrong settled/ownership state t="+elapsed+" expected="+Expected[checks]+" actual="+layer.SettledMode+" action="+layer.CurrentActionName+" fault="+layer.AttachmentFault);
                    Capture(trial,true,checks%3);File.WriteAllBytes(Path.Combine(directory,checks.ToString("D2")+"_"+Expected[checks]+".png"),pixels.EncodeToPNG());Debug.Log("PHONE_SET_STATE_OK "+Expected[checks]+" handoffs="+layer.HandoffCount);checks++;
                }
                if(layer.AttachmentFault.Length>0)throw new InvalidOperationException(layer.AttachmentFault+" t="+elapsed);
                if(layer.LeftGripError>=0)maxLeft=Mathf.Max(maxLeft,layer.LeftGripError);
                var root=trial.Passenger.transform.position;var hand=trial.Passenger.transform.Find(trial.Data.rightHandPath).position;maxHandStep=Mathf.Max(maxHandStep,(hand-previousHand-(root-previousRoot)).magnitude);previousHand=hand;previousRoot=root;
                Capture(trial,false,0);if(!encoder.AddFrame(pixels))throw new IOException("Video encoder rejected frame");samples++;
                if(elapsed<42)return;
                if(checks!=Expected.Length||maxLeft>.025f||layer.MaxHandoffError>.013f||maxHandStep>.18f)throw new InvalidOperationException("Contact/transition acceptance failed left="+maxLeft+" handoff="+layer.MaxHandoffError+" handStep="+maxHandStep);
                Debug.Log("PHONE_SET_RUNTIME_OK samples="+samples+" states="+checks+" handoffs="+layer.HandoffCount+" maxTransfer="+layer.MaxHandoffError+" maxLeftGrip="+maxLeft+" maxHandStep="+maxHandStep+" sameScene=true interruptedRequest=true");Finish();
            }
            catch(Exception e){Debug.LogException(e);failed=true;Finish();}
        }
        static void Capture(AnimationLabPhoneCase trial,bool close,int angle)
        {
            var actor=trial.Passenger.transform;var target=actor.position+Vector3.up*(close?1.13f:.98f);float yaw=angle==1?88:angle==2?-90:25;var direction=Quaternion.AngleAxis(yaw,Vector3.up)*actor.forward;
            var eye=target+direction*(close?1.65f:2.8f)+Vector3.up*.12f;camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));camera.fieldOfView=50;
            var old=camera.targetTexture;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pixels.Apply();RenderTexture.active=null;camera.targetTexture=old;
        }
        static void Finish(){encoder?.Dispose();encoder=null;Time.captureFramerate=0;if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);if(rt!=null)UnityEngine.Object.DestroyImmediate(rt);leaving=true;EditorApplication.isPlaying=false;}
    }
}
#endif

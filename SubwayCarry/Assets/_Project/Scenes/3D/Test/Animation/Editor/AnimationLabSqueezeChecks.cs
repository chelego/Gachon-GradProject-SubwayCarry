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
    [InitializeOnLoad] public static class AnimationLabSqueezeChecks
    {
        const string Key="SubwayCarry.Squeeze.QA";static bool leaving,failed,cancelRequested;static int frame=-1,index,samples;static float began,nextLog,maxStep,maxFoot,minDistance=10,unexplainedStop,maxYaw;static Vector3 previous;static string id,dir;
        static bool Baseline=>SessionState.GetBool(Key+".baseline",false);static bool ready;static Camera camera;static RenderTexture rt;static Texture2D pixels;static MediaEncoder encoder,profileEncoder;
        static AnimationLabSqueezeChecks(){EditorApplication.update+=Tick;}
        public static void Run(){AnimationLabPhoneBuild.Guard();SessionState.SetBool(Key+".baseline",Environment.GetCommandLineArgs().Contains("-squeezeBaseline"));SessionState.SetInt(Key,1);SessionState.SetBool(Key+".single",Environment.GetCommandLineArgs().Contains("-squeezeSingle"));EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.Root+"/Situations.unity");EditorApplication.isPlaying=true;}
        static void Assert(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        static void Begin(AnimationLabPhoneCase trial)
        {
            trial.Squeeze.End();trial.Agent.TeleportAndSetTarget(trial.Data.start,trial.Data.destination);trial.Passenger.Goal=trial.Data.destination;
            trial.Squeeze.Begin(index==2?1.5f:index==3?.69f:1.12f,index==1?45:0,index==1?-1:1);began=Time.time;nextLog=0;previous=trial.Passenger.transform.position;maxStep=0;maxFoot=0;unexplainedStop=0;maxYaw=0;cancelRequested=false;
            if(index<2){string tag=(Baseline?"기존_":"")+(index==0?"오른어깨":"왼어깨");encoder=new MediaEncoder(Path.Combine(dir,"옆디딤_"+tag+"_수정후.mp4"),new VideoTrackAttributes{width=1280,height=720,frameRate=new MediaRational(30),includeAlpha=false});profileEncoder=new MediaEncoder(Path.Combine(dir,"옆디딤_"+tag+"_측면.mp4"),new VideoTrackAttributes{width=1280,height=720,frameRate=new MediaRational(30),includeAlpha=false});}
            Debug.Log("SQUEEZE_CASE_BEGIN mode="+trial.Squeeze.PassageMode+" "+index+" id="+trial.PassengerId);
        }
        static void Tick()
        {
            if(SessionState.GetInt(Key,0)==0)return;if(leaving){if(!EditorApplication.isPlaying){SessionState.SetInt(Key,0);EditorApplication.Exit(failed?2:0);}return;}
            if(!EditorApplication.isPlaying||AnimationLabPhoneCase.Active==null||frame==Time.frameCount)return;frame=Time.frameCount;
            try
            {
                var trial=AnimationLabPhoneCase.Active;
                if(camera==null)
                {
                    if(Baseline)trial.Data.squeeze=trial.Data.squeeze.Where(e=>e.passageMode==0).ToArray();Assert(trial.Data.HasSqueezeSet,"Squeeze sources missing");Time.captureFramerate=30;Application.targetFrameRate=0;id=trial.PassengerId;trial.SetReviewMode(1);
                    camera=Camera.main;rt=new RenderTexture(1280,720,24);pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);dir=Path.GetFullPath("../../../MotionMatching/PhonePassenger/Squeeze");Directory.CreateDirectory(dir);
                    
                    var menu=trial.Menu;menu.HoverAt(menu.Header.center,10);Assert(menu.Open,"Hover did not open");menu.HoverAt(menu.Panel.center,11);Assert(menu.Open,"Panel lost hover");menu.HoverAt(Vector2.zero,11.1f);Assert(menu.Open,"Hover bridge closed immediately");menu.HoverAt(Vector2.zero,11.4f);Assert(!menu.Open,"Hover did not close");Debug.Log("HOVER_MENU_OK open=true panel=true debounce=true close=true");
                }
                if(!ready){if(trial.Passenger.PhoneLayer.IsTransitioning||trial.Passenger.PhoneLayer.PhoneInHand||Time.time<.3f)return;ready=true;trial.SetReviewMode(5);Begin(trial);}
                float elapsed=Time.time-began;var p=trial.Passenger.transform.position;if(trial.Squeeze.Body!=null&&trial.Squeeze.Body.Active){if(trial.Passenger.ActualVelocity.magnitude<.15f)unexplainedStop+=Time.deltaTime;var flow=index==1?new Vector2(.7071068f,.7071068f):Vector2.up;maxYaw=Mathf.Max(maxYaw,Vector3.Angle(new Vector3(flow.x,0,flow.y),trial.Passenger.transform.forward));}
                maxStep=Mathf.Max(maxStep,(p-previous).magnitude);previous=p;samples++;
                if(trial.Squeeze.Body!=null&&trial.Squeeze.Body.Active)maxFoot=Mathf.Max(maxFoot,trial.Squeeze.Body.FootError);minDistance=Mathf.Min(minDistance,trial.Squeeze.MinimumDistance);
                if(elapsed>nextLog){nextLog+=2;Debug.Log("SQUEEZE_TRACE case="+index+" t="+elapsed+" state="+trial.Squeeze.State+" gap="+trial.Squeeze.Gap+" p="+p.ToString("F3")+" v="+trial.Passenger.ActualVelocity.magnitude+" phase="+trial.Squeeze.Body?.Phase+" clock="+trial.Squeeze.Body?.PhaseTime+" foot="+maxFoot+" sep="+trial.Squeeze.MinimumDistance+" blocked="+trial.Squeeze.Body?.BlockedSweeps);}
                Assert(trial.PassengerId==id,"Passenger identity changed");Assert(trial.Passenger.PhoneLayer.AttachmentFault.Length==0,"Phone attachment fault");
                if(index<2){Capture(trial,elapsed,false);Assert(encoder.AddFrame(pixels),"Video frame rejected");Capture(trial,elapsed,true);Assert(profileEncoder.AddFrame(pixels),"Profile video frame rejected");}
                bool done=trial.Squeeze.State==AnimationLabSqueezeReview.Stage.Done;
                if(index==4){if(!cancelRequested&&trial.Squeeze.State==AnimationLabSqueezeReview.Stage.Passing&&trial.Squeeze.Body.PhaseTime>.3f){trial.SetReviewMode(4);cancelRequested=true;Assert(trial.ReviewMode==5&&trial.Squeeze.PendingMode==4,"Unsafe immediate mode change inside group");}done=cancelRequested&&trial.ReviewMode==4;}
                if(index==3&&elapsed>6){Assert(trial.Squeeze.State==AnimationLabSqueezeReview.Stage.Waiting,"Too-narrow gap did not wait");done=true;}
                if(!done){Assert(elapsed<32,"Passage timed out: "+trial.Squeeze.State);return;}
                if(index!=3)Assert(Vector2.Distance(trial.Agent.MovementTarget,trial.Data.destination)<.001f,"Passage lost original destination");
                Assert(maxStep<.13f,"Root teleport or collision jump: "+maxStep);Assert(maxFoot<.065f,"Foot support error: "+maxFoot);
                Debug.Log("SQUEEZE_CASE_OK "+index+" seconds="+elapsed+" maxStep="+maxStep+" footError="+maxFoot+" minSeparation="+trial.Squeeze.MinimumDistance+" sameId=true lowSpeedSeconds="+unexplainedStop+" maxBodyYaw="+maxYaw);
                if(index<2){encoder.Dispose();encoder=null;profileEncoder.Dispose();profileEncoder=null;}if(++index<(SessionState.GetBool(Key+".single",false)?1:5)){Begin(trial);return;}
                Debug.Log("SQUEEZE_RUNTIME_OK cases="+(index)+" fullSuite="+(!SessionState.GetBool(Key+".single",false))+" menuHover=true sameScene=true samples="+samples+" minSeparation="+minDistance);Finish();
            }
            catch(Exception e){Debug.LogException(e);failed=true;Finish();}
        }
        static void Capture(AnimationLabPhoneCase trial,float t,bool profile)
        {
            var target=trial.Passenger.transform.position+Vector3.up*(profile?.40f:.88f);var flow=trial.Squeeze.TravelFlow;var forward=new Vector3(flow.x,0,flow.y);var right=new Vector3(flow.y,0,-flow.x);var eye=target-forward*(profile?2.0f:3.0f)+right*(profile?-.3f:-.7f)+Vector3.up*(profile?.65f:1.0f);camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));camera.fieldOfView=55;
            var old=camera.targetTexture;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pixels.Apply();RenderTexture.active=null;camera.targetTexture=old;
            int shot=Mathf.RoundToInt(t*30);if(shot==65||shot==130||shot==220||shot==330||shot==450)File.WriteAllBytes(Path.Combine(dir,(Baseline?"옆디딤_기존_실행_":"옆디딤_실행_")+(index==0?"오른어깨":"왼어깨")+(profile?"_측면_":"_사선_")+shot+".png"),pixels.EncodeToPNG());
        }
        static void Finish(){encoder?.Dispose();encoder=null;profileEncoder?.Dispose();profileEncoder=null;Time.captureFramerate=0;leaving=true;EditorApplication.isPlaying=false;}
    }
}
#endif

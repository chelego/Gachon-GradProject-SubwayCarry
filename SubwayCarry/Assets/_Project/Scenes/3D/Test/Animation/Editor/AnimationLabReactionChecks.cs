#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Media;
using UnityEngine;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    [InitializeOnLoad] public static class AnimationLabReactionChecks
    {
        const string Key="SubwayCarry.MovingReaction.Checks";static int frame=-1,segment,samples;static bool leaving,failed;static float began,nextLog,minSeparation,stopTime,maxStep,maxFoot,minSpeed;static Vector3 previous;static Vector2 recordFlow;static bool newClosest;static int kind=8;static Camera camera;static RenderTexture rt;static Texture2D pixels;static MediaEncoder full,near;static string dir;static TextMesh label;static readonly System.Collections.Generic.HashSet<string> sourceIds=new System.Collections.Generic.HashSet<string>();
        static AnimationLabReactionChecks(){EditorApplication.update+=Tick;}
        public static void Run(){AnimationLabPhoneBuild.Guard();var args=Environment.GetCommandLineArgs();int k=Array.IndexOf(args,"-reactionKind");if(k>=0)kind=int.Parse(args[k+1]);SessionState.SetInt(Key+".kind",kind);SessionState.SetInt(Key,1);EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.Root+"/Situations.unity");EditorApplication.isPlaying=true;}
        static void Assert(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        static string Name(int k)=>k==8?"정면접근_왼쪽비껴가기":k==9?"정면접근_오른쪽비껴가기":k==10?"오른쪽통과_왼쪽간격내기":"왼쪽통과_오른쪽간격내기";
        static void Begin(AnimationLabPhoneCase trial)
        {
            recordFlow=(trial.Data.destination-trial.Data.start).normalized;trial.Reaction.End();trial.Agent.TeleportAndSetTarget(trial.Data.start,trial.Data.destination);trial.Passenger.Goal=trial.Data.destination;trial.Passenger.SyncWalkPhase((segment/2)*.25f);trial.Reaction.DisableAnimation=segment%2==0;trial.Reaction.ShowObstacle=true;trial.SetReviewMode(6+kind);trial.Reaction.Begin(kind,(segment/2)*.25f);began=Time.time;nextLog=0;previous=trial.Passenger.transform.position;minSeparation=100;stopTime=maxStep=maxFoot=0;minSpeed=100;
            label.text=(segment%2==0?"기존 일반 보행":"수정 회피")+" · "+(new[]{"왼발 지지 초반","왼발 지지 후반","오른발 지지 초반","오른발 지지 후반"}[segment/2]);Debug.Log("MOVING_CASE_BEGIN kind="+kind+" segment="+segment+" baseline="+trial.Reaction.DisableAnimation);
        }
        static void Tick()
        {
            if(SessionState.GetInt(Key,0)==0)return;if(leaving){if(!EditorApplication.isPlaying){SessionState.SetInt(Key,0);EditorApplication.Exit(failed?2:0);}return;}if(!EditorApplication.isPlaying||AnimationLabPhoneCase.Active==null||frame==Time.frameCount)return;frame=Time.frameCount;
            try
            {
                var trial=AnimationLabPhoneCase.Active;
                if(camera==null){kind=SessionState.GetInt(Key+".kind",8);Time.captureFramerate=30;Application.targetFrameRate=0;camera=Camera.main;camera.fieldOfView=48;rt=new RenderTexture(1280,720,24);pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);dir=Path.GetFullPath("../../../MotionMatching/PhonePassenger/Reaction");Directory.CreateDirectory(dir);full=new MediaEncoder(Path.Combine(dir,Name(kind)+"_전후비교.mp4"),new VideoTrackAttributes{width=1280,height=720,frameRate=new MediaRational(30),includeAlpha=false});near=new MediaEncoder(Path.Combine(dir,Name(kind)+"_접지몸통비교.mp4"),new VideoTrackAttributes{width=1280,height=720,frameRate=new MediaRational(30),includeAlpha=false});var go=new GameObject("QA comparison caption");go.transform.SetParent(camera.transform,false);go.transform.localPosition=new Vector3(-.7f,.35f,1.2f);label=go.AddComponent<TextMesh>();label.font=trial.Data.font;label.fontSize=40;label.characterSize=.012f;label.anchor=TextAnchor.UpperLeft;label.color=Color.white;go.GetComponent<MeshRenderer>().sharedMaterial=trial.Data.font.material;trial.SetReviewMode(1);Begin(trial);}
                float elapsed=Time.time-began;var p=trial.Passenger.transform.position;maxStep=Mathf.Max(maxStep,(p-previous).magnitude);previous=p;samples++;var reaction=trial.Reaction;var body=reaction.Body;
                newClosest=false;if(reaction.Other!=null){float distance=Vector2.Distance(trial.Passenger.Position,reaction.Other.Position);if(distance<minSeparation){minSeparation=distance;newClosest=true;}}if(body!=null&&!reaction.DisableAnimation)maxFoot=Mathf.Max(maxFoot,body.MaxSupportError);if(body!=null&&body.ControlsMovement){sourceIds.Add(body.Entry.sourceId);maxFoot=Mathf.Max(maxFoot,body.MaxSupportError);float speed=trial.Passenger.ActualVelocity.magnitude;minSpeed=Mathf.Min(minSpeed,speed);if(speed<.06f)stopTime+=Time.deltaTime;}
                Capture(trial,false);Assert(full.AddFrame(pixels),"Whole comparison frame rejected");Capture(trial,true);Assert(near.AddFrame(pixels),"Contact comparison frame rejected");if(newClosest&&minSeparation<.9f)File.WriteAllBytes(Path.Combine(dir,Name(kind)+"_"+segment+"_최근접.png"),pixels.EncodeToPNG());
                if(elapsed>nextLog){nextLog+=1;Debug.Log("MOVING_TRACE segment="+segment+" t="+elapsed+" state="+reaction.State+" v="+trial.Passenger.ActualVelocity.magnitude+" body="+body?.Clock+" appeared="+reaction.AppearedAt+" detected="+reaction.DetectedAt+" cpa="+reaction.ClosestTime+" offset="+reaction.ClosestOffset+" sep="+minSeparation+" foot="+maxFoot);}
                Assert(maxStep<.13f,"Unexpected root jump "+maxStep);if(elapsed<7.0f)return;
                if(segment%2==1){Assert(reaction.Reactions==1,"Expected one perception-driven reaction, got "+reaction.Reactions);Assert(reaction.State==AnimationLabReactionReview.Stage.Done,"No continuous return: "+reaction.State);Assert(reaction.DetectedAt>=reaction.AppearedAt&&reaction.AppearedAt>=0,"Reaction anticipated fixture");Assert(body!=null&&(int)body.Entry.kind==kind,"Wrong relative passing response "+body?.Entry?.kind);Assert(minSeparation>.475f,"Capsule overlap "+minSeparation);Assert(stopTime<.12f,"Ordinary encounter stopped "+stopTime);Assert(maxFoot<.045f,"Support error "+maxFoot);Assert(Vector2.Distance(trial.Agent.MovementTarget,trial.Data.destination)<.001f,"Original goal lost");}
                Debug.Log("MOVING_CASE_OK kind="+kind+" segment="+segment+" minSeparation="+minSeparation+" stopped="+stopTime+" minSpeed="+minSpeed+" foot="+maxFoot+" gait="+(body!=null?body.Entry?.gaitPhase:-1)+" originalGoal=true");if(++segment<8){Begin(trial);return;}Assert(sourceIds.Count==4,"Four gait cues did not exercise both authored sources");Debug.Log("MOVING_REACTION_RUNTIME_OK kind="+kind+" segments=8 bothSupportStarts=true fourGaitPhases=true movingOpponent=true baselineComparison=true samples="+samples);Finish();
            }
            catch(Exception e){Debug.LogException(e);failed=true;Finish();}
        }
        static void Capture(AnimationLabPhoneCase trial,bool close)
        {
            var forward=new Vector3(recordFlow.x,0,recordFlow.y);var right=new Vector3(forward.z,0,-forward.x);var target=trial.Passenger.transform.position+Vector3.up*(close?.8f:.95f);var eye=target+right*(close?2.0f:2.5f)-forward*(close?1.2f:4.0f)+Vector3.up*(close?.45f:1.0f);camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));var old=camera.targetTexture;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();RenderTexture.active=null;camera.targetTexture=old;
            if(segment%2==1&&trial.Reaction.Body!=null&&Mathf.Abs(trial.Reaction.Body.Clock-.3f)<.024f)File.WriteAllBytes(Path.Combine(dir,Name(kind)+"_"+segment+(close?"_근접":"_전체")+".png"),pixels.EncodeToPNG());
        }
        static void Finish(){full?.Dispose();near?.Dispose();full=near=null;Time.captureFramerate=0;leaving=true;EditorApplication.isPlaying=false;}
    }
}
#endif

#if UNITY_EDITOR
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    [UnityEditor.InitializeOnLoad] public static class AnimationLabReactionRegressions
    {
        const string Key="SubwayCarry.MovingReaction.Regression";static bool leaving,failed,requested;static int index,frame=-1;static float began,phaseAt;static UnityEngine.Vector2 goal;static UnityEngine.Vector3 previous;static float maxStep,minSeparation;
        static AnimationLabReactionRegressions(){UnityEditor.EditorApplication.update+=Tick;}
        public static void Run(){AnimationLabPhoneBuild.Guard();UnityEditor.SessionState.SetInt(Key,1);UnityEditor.SceneManagement.EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.Root+"/Situations.unity");UnityEditor.EditorApplication.isPlaying=true;}
        static void Assert(bool ok,string why){if(!ok)throw new System.InvalidOperationException(why);}
        static void Begin(AnimationLabPhoneCase trial)
        {
            trial.Reaction.End();trial.Squeeze.End();trial.SetReviewMode(1);var start=index==0?new UnityEngine.Vector2(-3,-5):trial.Data.start;goal=index==0?new UnityEngine.Vector2(4,3):trial.Data.destination;trial.Agent.TeleportAndSetTarget(start,goal);trial.Passenger.Goal=goal;var direction=(goal-start).normalized;trial.Passenger.transform.rotation=UnityEngine.Quaternion.LookRotation(new UnityEngine.Vector3(direction.x,0,direction.y));trial.Passenger.SyncWalkPhase(.25f);trial.Reaction.ShowObstacle=index!=1;trial.Reaction.DisableAnimation=false;
            if(index==5)trial.SetReviewMode(2);else if(index==6)trial.SetReviewMode(5);else{trial.SetReviewMode(14);trial.Reaction.Begin(index==0?10:index==2?12:index==3?14:index==4?13:8,.25f);}
            began=UnityEngine.Time.time;phaseAt=-1;requested=false;previous=trial.Passenger.transform.position;maxStep=0;minSeparation=100;UnityEngine.Debug.Log("MOVING_REGRESSION_BEGIN "+index);
        }
        static void Tick()
        {
            if(UnityEditor.SessionState.GetInt(Key,0)==0)return;if(leaving){if(!UnityEditor.EditorApplication.isPlaying){UnityEditor.SessionState.SetInt(Key,0);UnityEditor.EditorApplication.Exit(failed?2:0);}return;}if(!UnityEditor.EditorApplication.isPlaying||AnimationLabPhoneCase.Active==null||frame==UnityEngine.Time.frameCount)return;frame=UnityEngine.Time.frameCount;
            try
            {
                var trial=AnimationLabPhoneCase.Active;if(began==0){UnityEngine.Time.captureFramerate=30;UnityEngine.Application.targetFrameRate=0;Begin(trial);}float elapsed=UnityEngine.Time.time-began;var reaction=trial.Reaction;var body=reaction.Body;maxStep=UnityEngine.Mathf.Max(maxStep,(trial.Passenger.transform.position-previous).magnitude);previous=trial.Passenger.transform.position;if(reaction.Other!=null)minSeparation=UnityEngine.Mathf.Min(minSeparation,UnityEngine.Vector2.Distance(trial.Passenger.Position,reaction.Other.Position));Assert(maxStep<.13f,"Root discontinuity "+maxStep);if(body!=null&&body.Active)Assert(body.MaxSupportError<.045f,"Support recovery error");
                bool done=false;
                if(index==0){done=reaction.State==AnimationLabReactionReview.Stage.Done;if(done)Assert(reaction.Reactions==1&&body!=null&&body.Entry.kind==AnimationLabReactionKind.PassRight,"Rotated relative-side classification failed");}
                else if(index>=1&&index<=3&&elapsed>6){Assert(reaction.Reactions==0,"False positive in clear/diverging encounter "+index);done=true;}
                else if(index==4&&elapsed>6){Assert(reaction.State==AnimationLabReactionReview.Stage.Holding&&reaction.Reactions==0,"Blocked alternatives did not retain safety hold state="+reaction.State+" reactions="+reaction.Reactions+" separation="+minSeparation);Assert(minSeparation>.475f,"Unsafe braking collision");done=true;}
                else if(index==5)
                {
                    if(!requested&&elapsed>1.5f&&trial.Passenger.PhoneLayer.PhoneInHand&&!trial.Passenger.PhoneLayer.IsTransitioning){trial.SetReviewMode(14);requested=true;}
                    if(requested&&reaction.State==AnimationLabReactionReview.Stage.Reacting){Assert(!trial.Passenger.PhoneLayer.PhoneInHand,"Reaction started with unfinished phone handoff");if(reaction.PendingMode<0){trial.SetReviewMode(4);Assert(trial.ReviewMode==14&&reaction.PendingMode==4,"Unsafe interruption in reaction");}}
                    if(requested&&trial.ReviewMode==4&&trial.Passenger.PhoneLayer.PhoneInHand&&!trial.Passenger.PhoneLayer.IsTransitioning)done=true;
                }
                else if(index==6)
                {
                    if(!requested&&trial.Squeeze.Body!=null&&trial.Squeeze.Body.Active&&trial.Squeeze.Body.PhaseTime>.2f){trial.SetReviewMode(14);Assert(trial.ReviewMode==5&&trial.Squeeze.PendingMode==14,"Squeeze did not defer reaction selection");requested=true;}
                    if(requested&&trial.ReviewMode==14){Assert(trial.Squeeze.Body==null||!trial.Squeeze.Body.Active,"Two movement actions active");if(reaction.State==AnimationLabReactionReview.Stage.Done)done=true;}
                }
                Assert(trial.Passenger.PhoneLayer.AttachmentFault.Length==0,"Phone attachment regression");if(!done){Assert(elapsed<(index==6?35:15),"Regression timed out "+index+" state="+reaction.State);return;}
                if(index!=4)Assert(UnityEngine.Vector2.Distance(trial.Agent.MovementTarget,goal)<.001f,"Original goal not restored in "+index);UnityEngine.Debug.Log("MOVING_REGRESSION_OK "+index+" seconds="+elapsed+" separation="+minSeparation+" maxStep="+maxStep);if(++index<7){Begin(trial);return;}UnityEngine.Debug.Log("MOVING_REGRESSION_SUITE_OK rotated=true noPerson=true safePassing=true diverging=true bothBlocked=true phoneEntryExit=true squeezeHandoff=true");Finish();
            }
            catch(System.Exception e){UnityEngine.Debug.LogException(e);failed=true;CaptureFailure();Finish();}
        }
        static void CaptureFailure()
        {
            var trial=AnimationLabPhoneCase.Active;var camera=UnityEngine.Camera.main;if(trial==null||camera==null)return;var target=trial.Passenger.transform.position+UnityEngine.Vector3.up*.9f;camera.transform.SetPositionAndRotation(target+new UnityEngine.Vector3(3,1.3f,-4),UnityEngine.Quaternion.LookRotation(new UnityEngine.Vector3(-3,-1.3f,4)));var rt=new UnityEngine.RenderTexture(1280,720,24);var pixels=new UnityEngine.Texture2D(1280,720,UnityEngine.TextureFormat.RGBA32,false);camera.targetTexture=rt;camera.Render();UnityEngine.RenderTexture.active=rt;pixels.ReadPixels(new UnityEngine.Rect(0,0,1280,720),0,0);pixels.Apply();UnityEngine.RenderTexture.active=null;camera.targetTexture=null;System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath("../../../MotionMatching/PhonePassenger/Reaction/회귀_실패_"+index+".png"),pixels.EncodeToPNG());UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);
        }
        static void Finish(){UnityEngine.Time.captureFramerate=0;leaving=true;UnityEditor.EditorApplication.isPlaying=false;}
    }
}
#endif

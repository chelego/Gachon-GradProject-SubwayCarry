#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SubwayCarry.Gameplay3D;
using SubwayCarry.Core.Contracts;

namespace SubwayCarry.AI.HelpNPC.Editor
{
    [InitializeOnLoad]
    public static class HelperIngameCheck
    {
        const string Key="SubwayCarry.HelperIngameCheck",Request="Temp/MLAgents/ingame.request";
        const string ScenePath="Assets/_Project/Scenes/3D/Ingame/Game/SubwayCarry_3D_FirstPerson_Delivery.unity";
        static double next;
        static Metro3DWorld world;
        static Metro3DHelperAgent candidate;
        static float started,probed;
        static int inferenceCount,supportedFrames,helpFrames,helpers,acceptedFrames;
        static readonly int[] actions=new int[4];
        static string error="";
        [Serializable]sealed class SavedScenes{public SceneSetup[] scenes;}
        [Serializable]sealed class Result{public int helpers,inferences,supportedFrames,helpFrames,acceptedFrames;public int[] actions;public float risk,safeSpace;public bool balanceActive,usesPersonalBalance;public string status,error;}
        static HelperIngameCheck(){EditorApplication.update+=Update;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)) {
                world=UnityEngine.Object.FindFirstObjectByType<Metro3DWorld>();started=Time.time;probed=0;
                inferenceCount=supportedFrames=helpFrames=helpers=acceptedFrames=0;Array.Clear(actions,0,4);error="";
                Application.logMessageReceived+=Log;
            }
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+".Restore",false)) {
                SessionState.SetBool(Key+".Restore",false);
                var setup=JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key+".Scenes",""));
                if(setup!=null)EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
            }
        }
        static void Log(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error)error+=message+"\n";}
        static void Update()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(!EditorApplication.isPlaying) {
                if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
                if(!File.Exists(Request))return;File.Delete(Request);
                for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty){File.WriteAllText("Temp/MLAgents/ingame.result","BLOCKED: unsaved scene");return;}
                SessionState.SetString(Key+".Scenes",JsonUtility.ToJson(new SavedScenes{scenes=EditorSceneManager.GetSceneManagerSetup()}));
                EditorSceneManager.OpenScene(ScenePath);SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Restore",true);EditorApplication.EnterPlaymode();return;
            }
            if(!SessionState.GetBool(Key,false)||world==null)return;
            if(probed==0&&Time.time-started>2) {
                var all=UnityEngine.Object.FindObjectsByType<Metro3DHelperAgent>(FindObjectsSortMode.None);helpers=all.Length;
                foreach(var h in all){var p=h.GetComponent<Metro3DPassenger>();if(p!=null&&p.inTrain&&p.CanAssist){candidate=h;break;}}
                if(candidate==null){Finish("FAILED: no eligible helper");return;}
                world.session.Accept();
                Vector3 local=world.train.transform.InverseTransformPoint(candidate.transform.position);local.x=0;local.y=.06f;
                world.player.TeleportForReturn(world.train.transform.TransformPoint(local));
                world.player.transform.rotation=world.train.transform.rotation;probed=Time.time;
            }
            if(probed>0&&Time.time-probed>.4f) {
                if(Time.time-probed<.7f)world.player.TriggerBalanceMotion(TrainMotionPhase.Departing,Vector2.right,1);
                if(candidate.CurrentAction!=HelperAction.ContinueJourney)helpFrames++;
                actions[(int)candidate.CurrentAction]++;if(candidate.IsHelping)acceptedFrames++;
                if(world.player.NpcSupported)supportedFrames++;
                inferenceCount=candidate.InferenceCount;
            }
            if(probed>0&&Time.time-probed>9)Finish(error.Length==0&&inferenceCount>0&&acceptedFrames>0&&supportedFrames>0?"PASS":"FAILED");
        }
        static void Finish(string status)
        {
            Application.logMessageReceived-=Log;
            File.WriteAllText("Temp/MLAgents/ingame.result",JsonUtility.ToJson(new Result{status=status,helpers=helpers,inferences=inferenceCount,supportedFrames=supportedFrames,helpFrames=helpFrames,acceptedFrames=acceptedFrames,actions=actions,risk=candidate!=null?candidate.LastSituation.balanceRisk:0,safeSpace=candidate!=null?candidate.LastSituation.safeSpace:0,balanceActive=world!=null&&world.player.BalanceActive,usesPersonalBalance=world!=null&&world.player.UsesPersonalBalance,error=error},true));
            SessionState.SetBool(Key,false);EditorApplication.ExitPlaymode();
        }
    }
}
#endif

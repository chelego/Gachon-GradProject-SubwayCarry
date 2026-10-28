#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PoseClip=SubwayCarry.Gameplay3D.AnimationLab.AnimationLabPoseProfile.Clip;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    [InitializeOnLoad]
    public static class AnimationLabPoseEditorChecks
    {
        const string Key="SubwayCarry.PoseEditor.Check";
        static readonly int Generation;
        static AnimationLabPoseWindow window;
        static bool done;
        static Result result;
        [Serializable] sealed class Result
        {
            public string error="";
            public bool identity,holdNoDrift,undoRedo,clipIsolation,strengthOff,worldHandleMath,reset,
                sourceAndLengthsPreserved,sourceMaterial,sourceVisibility,saveDuringPlay,playExitPersistence,
                domainPersistence,unsavedDraftRecovered,reactivation,sceneUnchanged,targetDisappearance;
            public float worstDrift;
        }
        static AnimationLabPoseEditorChecks()
        {
            Generation=SessionState.GetInt(Key+".Generation",0)+1;SessionState.SetInt(Key+".Generation",Generation);
            EditorApplication.update+=Tick;
        }
        public static void Run()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Existing isolated QA only");
            var asset=AssetDatabase.LoadAssetAtPath<AnimationLabPoseProfile>(AnimationLabPoseWindow.ProfilePath);
            if(asset==null)throw new InvalidOperationException("Missing identity profile");
            SessionState.SetString(Key+".Original",JsonUtility.ToJson(asset));SessionState.SetInt(Key+".Stage",1);
            SessionState.SetInt(Key+".StartGeneration",Generation);SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(AnimationLabPoseWindow.ScenePath);EditorApplication.EnterPlaymode();
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static void Tick()
        {
            if(done||!SessionState.GetBool(Key,false))return;
            try
            {
                int stage=SessionState.GetInt(Key+".Stage",0);
                if(stage==1&&EditorApplication.isPlaying&&Time.frameCount>6)
                {
                    var review=UnityEngine.Object.FindFirstObjectByType<AnimationLabWalkReview>();
                    if(review==null||!review.PoseReviewActive)return;
                    result=new Result();RunInPlay(review);
                    SessionState.SetString(Key+".Result",JsonUtility.ToJson(result));SessionState.SetInt(Key+".Stage",2);
                    EditorApplication.ExitPlaymode();
                }
                else if(stage==2&&!EditorApplication.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    result=JsonUtility.FromJson<Result>(SessionState.GetString(Key+".Result","{}"));
                    CheckSaved();result.playExitPersistence=true;
                    window=ScriptableObject.CreateInstance<AnimationLabPoseWindow>();
                    CheckDraft();result.unsavedDraftRecovered=true;
                    SessionState.SetString(Key+".Result",JsonUtility.ToJson(result));SessionState.SetInt(Key+".Stage",3);
                    EditorUtility.RequestScriptReload();
                }
                else if(stage==3&&Generation>SessionState.GetInt(Key+".StartGeneration",0))
                {
                    result=JsonUtility.FromJson<Result>(SessionState.GetString(Key+".Result","{}"));
                    CheckSaved();
                    var windows=Resources.FindObjectsOfTypeAll<AnimationLabPoseWindow>();
                    window=windows.Length>0?windows[0]:ScriptableObject.CreateInstance<AnimationLabPoseWindow>();
                    CheckDraft();result.domainPersistence=true;Finish();
                }
            }
            catch(Exception e){if(result==null)result=new Result();result.error=e.ToString();Finish();}
        }
        static Quaternion SavedOffset=>Quaternion.Euler(0,0,-7);
        static Quaternion DraftOffset=>Quaternion.Euler(0,0,-9);
        static void CheckSaved()
        {
            var asset=AssetDatabase.LoadAssetAtPath<AnimationLabPoseProfile>(AnimationLabPoseWindow.ProfilePath);
            Require(Quaternion.Angle(asset.idle.Get(4),SavedOffset)<.005f,"Saved profile lost on Play exit/domain reload");
            Require(asset.hideFlags==HideFlags.None,"Transient draft flags copied to saved asset");
        }
        static void CheckDraft(){Require(window.Draft!=null&&window.hasUnsavedChanges&&Quaternion.Angle(window.Draft.idle.Get(4),DraftOffset)<.005f,"Unsaved draft lost on window/domain transition");}
        static void RunInPlay(AnimationLabWalkReview review)
        {
            window=ScriptableObject.CreateInstance<AnimationLabPoseWindow>();window.Attach(review);window.SelectClip(PoseClip.Idle,.3f);
            bool dirty=review.gameObject.scene.isDirty;
            var joint=review.PoseJoint(4);Require(joint!=null,"Missing test upper arm");
            var rig=review.manual.animator.GetComponentsInChildren<Transform>();
            var positions=new Vector3[rig.Length];var scales=new Vector3[rig.Length];
            for(int i=0;i<rig.Length;i++){positions[i]=rig[i].localPosition;scales[i]=rig[i].localScale;}
            var source=review.PoseSourceRoot.GetComponentsInChildren<Transform>();var sourceRot=new Quaternion[source.Length];
            for(int i=0;i<source.Length;i++)sourceRot[i]=source[i].localRotation;
            Vector3 root=review.manual.transform.position;Quaternion baseline=joint.localRotation;
            Require(Quaternion.Angle(baseline,review.BaseJointRotation(4))<.005f,"Identity is not baseline");result.identity=true;
            Undo.IncrementCurrentGroup();window.SetRotation(4,SavedOffset);Undo.FlushUndoRecordObjects();
            var expected=baseline*SavedOffset;
            for(int i=0;i<60;i++){review.EvaluatePoseNow();result.worstDrift=Mathf.Max(result.worstDrift,Quaternion.Angle(expected,joint.localRotation));}
            Require(result.worstDrift<.02f,"Offsets accumulate or are overwritten during hold");result.holdNoDrift=true;
            Undo.PerformUndo();Require(Quaternion.Angle(joint.localRotation,baseline)<.02f,"Undo failed");
            Undo.PerformRedo();Require(Quaternion.Angle(joint.localRotation,expected)<.02f,"Redo failed");result.undoRedo=true;
            window.SetStrength(0);Require(Quaternion.Angle(joint.localRotation,baseline)<.02f,"Strength zero differs from baseline");
            window.SetStrength(1);window.SetCorrectionEnabled(false);Require(Quaternion.Angle(joint.localRotation,baseline)<.02f,"Correction OFF differs from baseline");
            window.SetCorrectionEnabled(true);result.strengthOff=true;
            window.SelectClip(PoseClip.Walk,.3f);Require(Quaternion.Angle(joint.localRotation,review.BaseJointRotation(4))<.02f,"Idle offset leaked into Walk");
            window.SelectClip(PoseClip.Idle,.3f);Require(Quaternion.Angle(joint.localRotation,expected)<.02f,"Idle offset lost on clip switch");result.clipIsolation=true;
            window.ResetJoint(4);Require(Quaternion.Angle(joint.localRotation,baseline)<.02f,"Joint reset failed");
            window.SetRotation(0,Quaternion.Euler(0,0,3));window.SetStrength(.5f);
            Quaternion desired=Quaternion.AngleAxis(3,joint.forward)*joint.rotation;
            window.ApplyWorldRotation(4,desired);Require(Quaternion.Angle(desired,joint.rotation)<.03f,"World/local handle conversion failed at partial strength with parent offset");result.worldHandleMath=true;
            window.ResetClip();window.SetStrength(1);Require(Quaternion.Angle(joint.localRotation,baseline)<.02f,"Clip reset failed");result.reset=true;
            for(int i=0;i<rig.Length;i++){Require(Vector3.Distance(positions[i],rig[i].localPosition)<.00002f,"FK changed bone position/length");Require(Vector3.Distance(scales[i],rig[i].localScale)<.00002f,"FK changed bone scale");}
            for(int i=0;i<source.Length;i++)Require(Quaternion.Angle(sourceRot[i],source[i].localRotation)<.03f,"FK/source clip switch changed A0 "+source[i].name+" angle="+Quaternion.Angle(sourceRot[i],source[i].localRotation));
            Require(Vector3.Distance(root,review.manual.transform.position)<.00002f,"FK moved root");result.sourceAndLengthsPreserved=true;
            var skins=review.PoseSourceRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
            Require(skins.Length>0,"Source skin not inspected");
            var targetShader=review.manual.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial.shader;
            foreach(var skin in skins)Require(skin.sharedMaterial!=null&&skin.sharedMaterial.shader==targetShader,"Source skin still uses a legacy/default shader");result.sourceMaterial=true;
            review.ShowPoseSource(false);foreach(var renderer in review.PoseSourceRoot.GetComponentsInChildren<Renderer>())Require(!renderer.enabled,"Source hide failed");review.ShowPoseSource(true);result.sourceVisibility=true;
            Require(review.PoseJoint(-1)==null&&review.PoseJoint(999)==null,"Missing joint not safe");
            review.manual.gameObject.SetActive(false);review.manual.gameObject.SetActive(true);review.EvaluatePoseNow();Require(review.manual.FreeWalk!=null&&review.manual.FreeWalk.Ready,"Reactivation failed");result.reactivation=true;
            window.SetRotation(4,SavedOffset);window.SaveChanges();CheckSaved();Require(!window.hasUnsavedChanges,"Saved draft still dirty");result.saveDuringPlay=true;
            Require(review.gameObject.scene.isDirty==dirty,"Profile save changed scene dirty state");result.sceneUnchanged=true;
            window.SetRotation(4,DraftOffset);Require(window.hasUnsavedChanges,"Unsaved state missing");CheckSaved();
            UnityEngine.Object.DestroyImmediate(review.manual.gameObject);window.EditorTick();
            Require(window.Review==null,"Destroyed target retained by editor");result.targetDisappearance=true;
            // Destruction simulates domain teardown, not a user close (which uses Unity's Save/Discard/Cancel prompt).
            window.PreserveDraftBeforeReload();UnityEngine.Object.DestroyImmediate(window);window=null;
        }
        static void Finish()
        {
            if(done)return;done=true;SessionState.SetBool(Key,false);
            try
            {
                var asset=AssetDatabase.LoadAssetAtPath<AnimationLabPoseProfile>(AnimationLabPoseWindow.ProfilePath);
                string original=SessionState.GetString(Key+".Original","");
                if(asset!=null&&!string.IsNullOrEmpty(original)){JsonUtility.FromJsonOverwrite(original,asset);EditorUtility.SetDirty(asset);AssetDatabase.SaveAssetIfDirty(asset);}
                foreach(var current in Resources.FindObjectsOfTypeAll<AnimationLabPoseWindow>()){current.DiscardChanges();UnityEngine.Object.DestroyImmediate(current);}
            }
            catch(Exception e){result.error+="\ncleanup: "+e;}
            Debug.Log("WALK_POSE_EDITOR_CHECKS "+JsonUtility.ToJson(result));EditorApplication.Exit(string.IsNullOrEmpty(result.error)?0:2);
        }
    }
}
#endif

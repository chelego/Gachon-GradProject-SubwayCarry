#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using InputKey=UnityEngine.InputSystem.Key;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    [InitializeOnLoad]
    public static class AnimationLabChecks
    {
        const string Key="AnimationLabChecks";
        static AnimationLabCharacter actor;
        static int fieldIndex,testIndex,lastFrame=-1;
        static float began;
        static bool prepared,finished,cancelled,switched,repeatedCancel;
        static Result result;
        static Vector3 frozenPosition,frozenFoot;
        static Keyboard testKeyboard;
        static Mouse testMouse;
        static InputSettings previousInputSettings,manualInputSettings;
        static float initialCameraDistance;
        [Serializable] public sealed class Result {public string error="";public List<string> editorDiagnostics=new List<string>();public List<string> checks=new List<string>();public List<PoseCheck> poses=new List<PoseCheck>();public List<Row> rows=new List<Row>();}
        [Serializable] public sealed class PoseCheck {public string clip;public float boneLengthVariation,wristRotationRange,shoulderHeightRange;}
        [Serializable] public sealed class Row {public string field,state,output,clip,reason;public bool candidateMotor,candidateAnimation,contacts;public float time,speed,footError,pelvis,backError,rootYaw,clipTime;public Vector3 position,leftFoot,rightFoot;}
        static AnimationLabChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
        public static void Run()
        {
            AnimationLabScenarioChecks.Run();
        }
        // Historical five-field test implementation. Not a current Normal/Situations coverage claim.
        static void RetiredFiveFields()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Independent batch only");
            AnimationLabBuilder.BuildMissing();DiagnosePoses();SessionState.SetBool(Key,true);SessionState.SetInt(Key+".Field",0);SessionState.EraseString(Key+".Result");EditorSceneManager.OpenScene(AnimationLabBuilder.Root+"/Normal.unity");EditorApplication.EnterPlaymode();
        }
        static void DiagnosePoses()
        {
            var lib=AssetDatabase.LoadAssetAtPath<AnimationLabLibrary>(AnimationLabBuilder.Root+"/Data/Library.asset");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AnimationLabBuilder.Root+"/Prefabs/Character.prefab");
            var o=UnityEngine.Object.Instantiate(prefab);var model=o.GetComponentInChildren<Animator>();var bones=model.GetComponentsInChildren<Transform>();
            var wrist=bones.First(t=>t.name=="DEF-hand.L");var shoulder=bones.First(t=>t.name=="DEF-shoulder.L");
            var testBones=bones.Where(t=>t.name.StartsWith("DEF-")).ToArray();var baseLengths=testBones.Select(t=>t.localPosition.magnitude).ToArray();
            var diagnostics=new Result();
            float measured=AnimationLabBuilder.Supports(lib.walk,o,out _,out _);diagnostics.checks.Add("Current preserved model CC0 walk low-support speed remeasured: "+measured+" m/s");
            foreach(var clip in new[]{lib.idle,lib.walk,lib.run,lib.sitEnter,lib.sitIdle,lib.sitExit}.Concat(lib.converted)){
                var row=new PoseCheck{clip=clip.name};Quaternion initial=Quaternion.identity;float min=100,max=-100;
                for(int n=0;n<=60;n++){clip.SampleAnimation(model.gameObject,clip.length*n/60);if(n==0){initial=wrist.localRotation;for(int b=0;b<testBones.Length;b++)baseLengths[b]=testBones[b].localPosition.magnitude;}
                    row.wristRotationRange=Mathf.Max(row.wristRotationRange,Quaternion.Angle(initial,wrist.localRotation));min=Mathf.Min(min,shoulder.position.y);max=Mathf.Max(max,shoulder.position.y);
                    for(int b=0;b<testBones.Length;b++)if(testBones[b].name!="DEF-hips")row.boneLengthVariation=Mathf.Max(row.boneLengthVariation,Mathf.Abs(testBones[b].localPosition.magnitude-baseLengths[b]));
                }
                row.shoulderHeightRange=max-min;diagnostics.poses.Add(row);Debug.Log("ANIMATION_POSE "+JsonUtility.ToJson(row));
            }
            UnityEngine.Object.DestroyImmediate(o);SessionState.SetString(Key+".Poses",JsonUtility.ToJson(diagnostics));
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){fieldIndex=SessionState.GetInt(Key+".Field",0);result=JsonUtility.FromJson<Result>(SessionState.GetString(Key+".Result",SessionState.GetString(Key+".Poses","{}")));Time.captureDeltaTime=1f/60;prepared=false;finished=false;lastFrame=-1;testIndex=0;Application.logMessageReceived+=Log;}
            if(state==PlayModeStateChange.EnteredEditMode){Application.logMessageReceived-=Log;fieldIndex=SessionState.GetInt(Key+".Field",0);EditorSceneManager.OpenScene(AnimationLabBuilder.Root+"/"+AnimationLabBuilder.Fields[fieldIndex]+".unity");EditorApplication.EnterPlaymode();}
        }
        static void Log(string text,string stack,LogType type){if(type!=LogType.Exception&&type!=LogType.Error)return;if(stack.Contains("UnityEditor.Search")){result.editorDiagnostics.Add(text);return;}result.error+="\n"+text+"\n"+stack;}
        static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);result.checks.Add(text);}
        static void Teleport(Vector3 point,Quaternion rotation){actor.motor.enabled=false;actor.transform.SetPositionAndRotation(point,rotation);actor.motor.enabled=true;Physics.SyncTransforms();}
        static void Begin(int n)
        {
            testIndex=n;began=Time.time;cancelled=switched=repeatedCancel=false;
            actor.SetFrozen(false);actor.SetInput(Vector3.zero);
            if(fieldIndex==0){Time.captureDeltaTime=n==3?1f/30:n==4?1f/120:1f/60;actor.ResetActor();actor.WalkCandidate=n!=0;actor.WalkAnimationCandidate=n>=2;actor.contacts.correction=n>=3;if(n==1){actor.SetPipeline(AnimationLabCharacter.Pipeline.ConvertedClip);Check(actor.Mode==AnimationLabCharacter.Pipeline.Original,"Walk rejects converted pipeline");}if(n==2){actor.SetPipeline(AnimationLabCharacter.Pipeline.Matching);Check(actor.Mode==AnimationLabCharacter.Pipeline.Original,"Walk rejects M pipeline");}}
            else if(fieldIndex==1)actor.ResetActor();
            else if(fieldIndex==2||fieldIndex==3){actor.ResetActor();var anchor=actor.anchors[n];Check(anchor.Contact(out _,out _),"Valid surface "+anchor.label);Teleport(anchor.approach.position+anchor.contact.forward*.12f,anchor.contact.rotation);Check(actor.BeginInteraction(anchor),"Interaction starts "+anchor.label);}
            else {actor.ResetActor();Teleport(new Vector3(-3,.03f,0),Quaternion.identity);}
            if(fieldIndex==0&&n==5){
                // Virtual devices exist only in this independent Unity process.
                // This does not synthesize Windows/user-editor input.
                previousInputSettings=InputSystem.settings;manualInputSettings=UnityEngine.Object.Instantiate(previousInputSettings);InputSystem.settings=manualInputSettings;
                manualInputSettings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
                manualInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                manualInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                testKeyboard=InputSystem.AddDevice<Keyboard>("AnimationLabVirtualKeyboard");testMouse=InputSystem.AddDevice<Mouse>("AnimationLabVirtualMouse");actor.Automated=false;initialCameraDistance=actor.orbit.distance;
            }
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||Time.frameCount==lastFrame)return;
            lastFrame=Time.frameCount;
            try {
                if(!prepared){actor=UnityEngine.Object.FindFirstObjectByType<AnimationLabCharacter>();if(actor==null||!actor.Ready||Time.frameCount<4)return;actor.Automated=true;Check(actor.contacts.Ready,"Generic rig connected "+AnimationLabBuilder.Fields[fieldIndex]);Check(!actor.animator.applyRootMotion,"Motor owns root "+fieldIndex);prepared=true;Begin(0);}
                if(!actor.Ready)return;
                float t=Time.time-began;
                if(fieldIndex==0){
                    if(testIndex<5)actor.SetInput(t<1?Vector3.zero:t<3?Vector3.forward:t<5?Vector3.right:t<7?Vector3.back:Vector3.zero);
                    else {InputSystem.QueueStateEvent(testKeyboard,t<1||t>=5?new KeyboardState():t<3?new KeyboardState(InputKey.W):new KeyboardState(InputKey.W,InputKey.LeftShift));InputSystem.QueueStateEvent(testMouse,new MouseState{scroll=t<.05f?new Vector2(0,120):Vector2.zero});InputSystem.Update();}
                    if(testIndex==2&&t>7.2f&&t<7.3f&&!actor.Frozen){frozenPosition=actor.transform.position;frozenFoot=actor.contacts.Foot(0);actor.SetFrozen(true);}
                    if(testIndex==2&&t>7.7f&&actor.Frozen){Check(Vector3.Distance(frozenPosition,actor.transform.position)<.0001f,"Local freeze preserves Motor");Check(Vector3.Distance(frozenFoot,actor.contacts.Foot(0))<.001f,"Local freeze preserves played pose");actor.SetFrozen(false);}
                }else if(fieldIndex==1)actor.SetInput(t<1?Vector3.zero:t<3.5f?Vector3.forward:Vector3.zero,true);
                else if(fieldIndex==2||fieldIndex==3){if(fieldIndex==2&&t>2.5f&&!switched){actor.SetPipeline(testIndex%2==0?AnimationLabCharacter.Pipeline.ConvertedClip:AnimationLabCharacter.Pipeline.Matching);switched=true;}if(t>3&&!cancelled){Check(actor.State== (fieldIndex==2?AnimationLabCharacter.Action.Seated:AnimationLabCharacter.Action.Lean),"Activity reached "+actor.anchors[testIndex].label);Check(actor.contacts.MaxFootError<.15f,"Bounded foot reach "+actor.anchors[testIndex].label);if(fieldIndex==2){Check(Mathf.Abs(actor.contacts.Hips.y-(actor.Anchor.contact.position.y+.072f))<.025f,"Pelvis fitted to seat height");Check(actor.LegacyStateHash==Animator.StringToHash("Seated"),"Seated pose survives pipeline change");}actor.CancelInteraction("");cancelled=true;}}
                else actor.SetInput(t<1?Vector3.zero:t<5?Vector3.forward:Vector3.zero);
                for(int i=0;i<2;i++)CheckFinite(actor.contacts.Foot(i));
                if((fieldIndex==2||fieldIndex==3)&&cancelled&&!repeatedCancel&&t>3.2f){var before=actor.State;actor.CancelInteraction("");Check(before!=AnimationLabCharacter.Action.Exit||actor.State==AnimationLabCharacter.Action.Exit,"Repeated cancel preserves exit transition");repeatedCancel=true;}
                if(fieldIndex==3&&actor.ContactWeight>.99f&&t>1.2f&&t<3&&actor.contacts.BackError>.06f)throw new InvalidOperationException("Persistent back separation "+actor.contacts.BackError);
                if(Time.frameCount%10==0){result.rows.Add(new Row{field=actor.field,time=t,state=actor.State.ToString(),output=actor.Output,clip=actor.ClipName,reason=actor.Reason,candidateMotor=actor.WalkCandidate,candidateAnimation=actor.WalkAnimationCandidate,contacts=actor.contacts.correction,speed=actor.ActualVelocity.magnitude,footError=actor.contacts.MaxFootError,pelvis=actor.contacts.PelvisCorrection,backError=actor.contacts.BackError,rootYaw=actor.transform.eulerAngles.y,clipTime=actor.ClipTime,position=actor.transform.position,leftFoot=actor.contacts.Foot(0),rightFoot=actor.contacts.Foot(1)});}
                float duration=fieldIndex==0?9:fieldIndex==1?5:fieldIndex<4?5:7;
                if(t>duration){
                    Check(!actor.animator.applyRootMotion,"No root-motion regression "+actor.field+" "+testIndex);
                    if(fieldIndex==0&&testIndex<5){Begin(testIndex+1);return;}
                    if(fieldIndex==0&&testIndex==5){Check(actor.transform.position.z>4,"Virtual WASD and Shift drive character");Check(actor.orbit.distance<initialCameraDistance,"Virtual wheel drives orbit distance");actor.Automated=true;RestoreInput();}
                    if((fieldIndex==2||fieldIndex==3)&&testIndex+1<actor.anchors.Length){Begin(testIndex+1);return;}
                    if(fieldIndex==1)Check(actor.transform.position.z>5,"Run produces real movement");
                    if(fieldIndex==4)Check(actor.transform.position.y>.8f,"CharacterController reaches stair landing");
                    Time.captureDeltaTime=1f/60;fieldIndex++;if(fieldIndex>=AnimationLabBuilder.Fields.Length){Finish();return;}
                    SessionState.SetInt(Key+".Field",fieldIndex);SessionState.SetString(Key+".Result",JsonUtility.ToJson(result));EditorApplication.ExitPlaymode();
                }
            }catch(Exception e){result.error+="\n"+e;Finish();}
        }
        static void CheckFinite(Vector3 p){if(float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.z))throw new InvalidOperationException("Non-finite pose");}
        static void RestoreInput(){if(testKeyboard!=null)InputSystem.RemoveDevice(testKeyboard);if(testMouse!=null)InputSystem.RemoveDevice(testMouse);testKeyboard=null;testMouse=null;if(previousInputSettings!=null){InputSystem.settings=previousInputSettings;previousInputSettings=null;}if(manualInputSettings!=null)UnityEngine.Object.Destroy(manualInputSettings);manualInputSettings=null;}
        static void Finish(){if(finished)return;finished=true;RestoreInput();var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../MotionMatching/animation-fields.json"));Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(result,true));Debug.Log("ANIMATION_LAB_FINISHED checks="+result.checks.Count+" rows="+result.rows.Count+" error="+result.error);SessionState.SetBool(Key,false);EditorApplication.Exit(string.IsNullOrEmpty(result.error)?0:2);}
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Motion=SubwayCarry.Gameplay3D.AnimationLab.AnimationLabAuthoredMotion;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    public static class AnimationLabPhoneSetEditorChecks
    {
        static void Assert(bool value,string why){if(!value)throw new InvalidOperationException(why);}
        public static void Run()
        {
            AnimationLabPhoneBuild.Guard();AnimationLabMotionWindow window=null;
            try
            {
                EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);SessionState.EraseString("SubwayCarry.MotionAuthoring.Draft");var data=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);
                string userPath=AssetDatabase.GetAssetPath(data.authored),userFile=File.ReadAllText(userPath),userRevision=data.publishedRevision;
                window=EditorWindow.GetWindow<AnimationLabMotionWindow>();int checkedSources=0;
                foreach(var role in Enum.GetValues(typeof(AnimationLabPhoneAction)).Cast<AnimationLabPhoneAction>())
                {
                    data=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);var entry=data.Find(role);var source=entry.authored;string original=JsonUtility.ToJson(source),sourcePath=AssetDatabase.GetAssetPath(source);var paths=new[]{sourcePath,AssetDatabase.GetAssetPath(source.generatedClip),AssetDatabase.GetAssetPath(source.generatedFbx)};var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
                    Assert(source.keys.Count==source.lastFrame+1,"Source is not a full motion: "+role);
                    var mask=(AnimationLabPhoneMotion.BothArms(role)?data.bothArmPaths:data.upperPaths).Select(p=>Array.IndexOf(source.paths,p)).ToArray();
                    if(role==AnimationLabPhoneAction.CarryLoop){int upper=Array.IndexOf(source.paths,data.rightUpperPath);float excursion=source.keys.Max(p=>Quaternion.Angle(p.rotations[upper],source.keys[0].rotations[upper]));Assert(excursion>10,"Carry source has no actual walk arm swing");Debug.Log("PHONE_CARRY_MOTION excursion="+excursion);}
                    window.LoadSource(source);window.Seek(0);Assert(window.PhoneProp!=null,"Authoring prop preview missing: "+role);AnimationLabMotionAuthoring.Bind(window.Draft,window.Rig);
                    if(role==AnimationLabPhoneAction.TakeOut||role==AnimationLabPhoneAction.PutAway)
                    {window.Seek(entry.contactFrame-1);var before=window.PhoneProp.transform.position;window.Seek(entry.contactFrame);Assert(Vector3.Distance(before,window.PhoneProp.transform.position)<.015f,"Editor prop handoff jumps: "+role);}
                    int frame=source.lastFrame/2,wrist=Array.IndexOf(source.paths,data.rightHandPath);window.Seek(frame);var changed=window.Draft.Evaluate(frame).rotations[wrist]*Quaternion.AngleAxis(1,Vector3.right);window.SetRotation(wrist,changed);window.SaveKeyAdvance();window.SaveBundle();Assert(window.Status.Contains("Normal 적용 완료"),"F2 publication failed: "+window.Status);
                    source=AssetDatabase.LoadAssetAtPath<Motion>(sourcePath);data=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);entry=data.Find(role);
                    Assert(Quaternion.Angle(source.Evaluate(frame,false).rotations[wrist],changed)<.1f&&entry.clip==source.generatedClip&&entry.sourceId==source.id,"Edited source not bound to role: "+role);
                    Assert(paths.Select(AssetDatabase.AssetPathToGUID).SequenceEqual(guids),"Source GUID changed: "+role);
                    var restore=ScriptableObject.CreateInstance<Motion>();JsonUtility.FromJsonOverwrite(original,restore);restore.hideFlags=HideFlags.HideAndDontSave;AnimationLabMotionAuthoring.SaveBundle(restore,ref source,AnimationLabMotionAuthoring.Revision(source));UnityEngine.Object.DestroyImmediate(restore);
                    Assert(AnimationLabPhonePublication.OwnedSource(source.generatedFbx,out bool owned)==source&&owned,"FBX did not resolve to editable source");checkedSources++;Debug.Log("PHONE_SOURCE_F3_F2_OK "+role);
                }
                data=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);Assert(File.ReadAllText(userPath)==userFile&&data.publishedRevision==userRevision,"Approved user hold was modified");
                var dependencies=AssetDatabase.GetDependencies(AnimationLabPhoneBuild.BindingPath,true);Assert(!dependencies.Any(p=>p.Contains("/FreeWalk/")||p.Contains("/Mixamo/")||p.Contains("/LocalSample/")||p.Contains("/Logs/")),"New common set depends on private content");
                Debug.Log("PHONE_SET_EDITOR_OK sources="+checkedSources+" realF3Edit=true roleF2Binding=true sameGuids=true editorProps=true rigSignaturePreserved=true userSourceUntouched=true portableBinding=true");window.Close();EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);if(window!=null)window.Close();EditorApplication.Exit(2);}
        }
    }
}
#endif

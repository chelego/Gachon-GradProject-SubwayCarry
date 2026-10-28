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
    public static class AnimationLabSqueezeEditorChecks
    {
        static void Assert(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        public static void Run()
        {
            AnimationLabPhoneBuild.Guard();AnimationLabMotionWindow window=null;
            try
            {
                EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);SessionState.EraseString("SubwayCarry.MotionAuthoring.Draft");var data=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);
                var user=File.ReadAllBytes(AssetDatabase.GetAssetPath(data.authored));var phoneIds=data.actions.Select(e=>e.sourceId).ToArray();window=EditorWindow.GetWindow<AnimationLabMotionWindow>();int count=0;
                foreach(var entry in data.squeeze)
                {
                    var source=entry.authored;string original=JsonUtility.ToJson(source),path=AssetDatabase.GetAssetPath(source);var paths=new[]{path,AssetDatabase.GetAssetPath(source.generatedClip),AssetDatabase.GetAssetPath(source.generatedFbx)};var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
                    Assert(source.keys.Count==37&&source.loop==(entry.phase==1),"Not a real editable transition/loop");
                    if(entry.phase==1)Assert(Vector3.Distance(source.keys[0].root,source.keys[36].root)>.27f,"Root trajectory was lost");
                    window.LoadSource(source);window.Seek(18);AnimationLabMotionAuthoring.Bind(window.Draft,window.Rig);Assert(window.PhoneProp!=null,"Stored prop preview absent");
                    int wrist=Array.IndexOf(source.paths,data.rightHandPath);var changed=window.Draft.Evaluate(18).rotations[wrist]*Quaternion.AngleAxis(.5f,Vector3.right);window.SetRotation(wrist,changed);window.SaveKeyAdvance();window.SaveBundle();Assert(window.Status.Contains("사람 사이 통과"),"F2 did not publish squeeze role: "+window.Status);
                    source=AssetDatabase.LoadAssetAtPath<Motion>(path);Assert(Quaternion.Angle(source.Evaluate(18,false).rotations[wrist],changed)<.1f,"F3 edit was lost");Assert(paths.Select(AssetDatabase.AssetPathToGUID).SequenceEqual(guids),"GUID changed");
                    var restore=ScriptableObject.CreateInstance<Motion>();JsonUtility.FromJsonOverwrite(original,restore);restore.hideFlags=HideFlags.HideAndDontSave;AnimationLabMotionAuthoring.SaveBundle(restore,ref source,AnimationLabMotionAuthoring.Revision(source));UnityEngine.Object.DestroyImmediate(restore);
                    Assert(AnimationLabPhonePublication.OwnedSource(source.generatedFbx,out bool owned)==source&&owned,"Output/source owner lookup failed");count++;
                }
                data=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);Assert(File.ReadAllBytes(AssetDatabase.GetAssetPath(data.authored)).SequenceEqual(user),"User hand source changed");Assert(data.actions.Select(e=>e.sourceId).SequenceEqual(phoneIds),"Phone action IDs changed");
                Assert(!AssetDatabase.GetDependencies(AnimationLabPhoneBuild.BindingPath,true).Any(p=>p.Contains("/Logs/")||p.Contains("/FreeWalk/")||p.Contains("/Mixamo/")),"Runtime binding depends on local QA assets");
                window.Close();Debug.Log("SQUEEZE_EDITOR_OK sources="+count+" F3F2=true sameGuids=true storedProps=true rootTrajectory=true sameRig=true portable=true userHandUntouched=true");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);if(window!=null)window.Close();EditorApplication.Exit(2);}
        }
    }
}
#endif

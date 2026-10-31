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
    public static class AnimationLabReactionEditorChecks
    {
        public static void Run()
        {
            AnimationLabPhoneBuild.Guard();AnimationLabMotionWindow window=null;
            try
            {
                EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);SessionState.EraseString("SubwayCarry.MotionAuthoring.Draft");var data=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);var user=File.ReadAllBytes(AssetDatabase.GetAssetPath(data.authored));var ids=data.actions.Select(e=>e.sourceId).ToArray();var squeeze=data.squeeze.Select(e=>e.sourceId).ToArray();int count=0;
                window=EditorWindow.GetWindow<AnimationLabMotionWindow>();
                foreach(var entry in data.reactions.Where(e=>(int)e.kind>=8))
                {
                    var source=entry.authored;string path=AssetDatabase.GetAssetPath(source),original=JsonUtility.ToJson(source);var outputs=new[]{path,AssetDatabase.GetAssetPath(source.generatedClip),AssetDatabase.GetAssetPath(source.generatedFbx)};var guids=outputs.Select(AssetDatabase.AssetPathToGUID).ToArray();int frame=source.lastFrame/3;
                    if(!AssetDatabase.OpenAsset(source.generatedFbx))throw new InvalidOperationException("Owned FBX did not open source");window.LoadSource(source);window.Seek(frame);int wrist=Array.IndexOf(source.paths,data.rightHandPath);var changed=window.Draft.Evaluate(frame).rotations[wrist]*Quaternion.AngleAxis(.5f,Vector3.right);window.SetRotation(wrist,changed);if(!window.MoveRootHandleWorld(window.RootHandlePosition+Vector3.right*.003f))throw new InvalidOperationException("ROOT edit rejected");window.SaveKeyAdvance();window.SaveBundle();
                    if(!window.Status.Contains("회피"))throw new InvalidOperationException("F2 did not publish reaction");source=AssetDatabase.LoadAssetAtPath<Motion>(path);var e=data.FindReactionSource(source.id);
                    if(e.clip!=source.generatedClip||e.root.Length!=source.lastFrame+1||e.leftContact.Length!=e.root.Length||e.rightContact.Length!=e.root.Length||Vector3.Distance(e.root[frame],source.Evaluate(frame,false).root)>.00001f||Quaternion.Angle(source.Evaluate(frame,false).rotations[wrist],changed)>.1f||!outputs.Select(AssetDatabase.AssetPathToGUID).SequenceEqual(guids))throw new InvalidOperationException("F3/F2 output, metadata or GUID mismatch");
                    var restore=ScriptableObject.CreateInstance<Motion>();JsonUtility.FromJsonOverwrite(original,restore);restore.hideFlags=HideFlags.HideAndDontSave;AnimationLabMotionAuthoring.SaveBundle(restore,ref source,AnimationLabMotionAuthoring.Revision(source));UnityEngine.Object.DestroyImmediate(restore);if(AnimationLabPhonePublication.OwnedSource(source.generatedFbx,out bool owned)!=source||!owned)throw new InvalidOperationException("Published FBX owner mismatch");count++;
                }
                if(count!=16||!File.ReadAllBytes(AssetDatabase.GetAssetPath(data.authored)).SequenceEqual(user)||!ids.SequenceEqual(data.actions.Select(e=>e.sourceId))||!squeeze.SequenceEqual(data.squeeze.Select(e=>e.sourceId)))throw new InvalidOperationException("Wrong source count or protected phone/squeeze changed");
                if(AssetDatabase.GetDependencies(AnimationLabPhoneBuild.BindingPath,true).Any(p=>p.Contains("/Logs/")||p.Contains("/Mixamo/")||p.Contains("/FreeWalk/")))throw new InvalidOperationException("Local-only source dependency");
                window.Close();Debug.Log("MOVING_REACTION_EDITOR_OK sources="+count+" F3F2=true ownedFbx=true sameGuids=true rootContactsRecomputed=true phoneAndSqueezePreserved=true portable=true");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);if(window!=null)window.Close();EditorApplication.Exit(2);}
        }
    }
}
#endif

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
        static void InspectGround(AnimationLabPhoneMotion data,AnimationLabPhoneMotion.SqueezeClip entry)
        {
            var actor=AnimationLabMotionAuthoring.CreateRig(data.rig);AnimationLabMotionAuthoring.StripAnimators(actor);var mesh=new Mesh();
            try
            {
                var bones=AnimationLabMotionAuthoring.Bind(entry.authored,actor);var verts=new System.Collections.Generic.List<Vector3>();
                float Low(){float low=100;foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.BakeMesh(mesh);mesh.GetVertices(verts);foreach(var v in verts)low=Mathf.Min(low,skin.transform.TransformPoint(v).y);}return low;}
                data.idle.SampleAnimation(actor.transform.Find(data.animatorPath).gameObject,0);float reference=Low();float min=100,max=-100;int count=0;
                for(int f=0;f<=entry.authored.lastFrame;f++)
                {
                    AnimationLabMotionAuthoring.Apply(entry.authored,bones,entry.authored.Evaluate(f,false));
                    if(entry.leftContact[f]==0&&entry.rightContact[f]==0)continue;
                    float gap=Low()-reference;min=Mathf.Min(min,gap);max=Mathf.Max(max,gap);count++;
                }
                Debug.Log("SQUEEZE_SOURCE_GROUND side="+entry.side+" phase="+entry.phase+" supportFrames="+count+" soleGap="+min+".."+max+" skinnedMesh=true");
                Assert(min>-.012f&&max<.015f,"Authored sole/ground discrepancy: "+min+".."+max);
            }
            finally{UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(actor);}
        }
        public static void Run()
        {
            AnimationLabPhoneBuild.Guard();AnimationLabMotionWindow window=null;
            try
            {
                EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);SessionState.EraseString("SubwayCarry.MotionAuthoring.Draft");var data=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);
                var user=File.ReadAllBytes(AssetDatabase.GetAssetPath(data.authored));var phoneIds=data.actions.Select(e=>e.sourceId).ToArray();window=EditorWindow.GetWindow<AnimationLabMotionWindow>();int count=0;
                foreach(var entry in data.squeeze)
                {
                    var source=entry.authored;InspectGround(data,entry);string original=JsonUtility.ToJson(source),path=AssetDatabase.GetAssetPath(source);var paths=new[]{path,AssetDatabase.GetAssetPath(source.generatedClip),AssetDatabase.GetAssetPath(source.generatedFbx)};var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
                    Assert(source.keys.Count==source.lastFrame+1&&source.loop==(entry.phase==1),"Not a real editable transition/loop");
                    if(entry.phase==1){var displacement=source.keys[source.keys.Count-1].root-source.keys[0].root;Assert(displacement.magnitude>.05f&&Vector3.Distance(entry.root[entry.root.Length-1]-entry.root[0],displacement)<.00001f,"Saved ROOT displacement metadata differs from authored motion");}
                    int middle=source.lastFrame/2;window.LoadSource(source);window.Seek(middle);AnimationLabMotionAuthoring.Bind(window.Draft,window.Rig);Assert(window.PhoneProp!=null,"Stored prop preview absent");
                    int wrist=Array.IndexOf(source.paths,data.rightHandPath);var changed=window.Draft.Evaluate(middle).rotations[wrist]*Quaternion.AngleAxis(.5f,Vector3.right);window.SetRotation(wrist,changed);var root=window.RootHandlePosition;Assert(window.MoveRootHandleWorld(root+Vector3.right*.003f),"Root handle edit failed");window.SaveKeyAdvance();window.SaveBundle();Assert(window.Status.Contains("사람 사이 통과"),"F2 did not publish squeeze role: "+window.Status);
                    source=AssetDatabase.LoadAssetAtPath<Motion>(path);Assert(Quaternion.Angle(source.Evaluate(middle,false).rotations[wrist],changed)<.1f,"F3 edit was lost");Assert(paths.Select(AssetDatabase.AssetPathToGUID).SequenceEqual(guids),"GUID changed");Assert(entry.HasTrajectory&&Vector3.Distance(entry.root[middle],source.Evaluate(middle,false).root)<.00001f,"F2 trajectory metadata stale");Assert(entry.leftContact.Length==source.lastFrame+1&&entry.rightContact.Length==entry.leftContact.Length,"F2 contacts stale");Assert(Mathf.Abs(AnimationLabMotionAuthoring.ImportedClip(paths[2]).length-source.Duration)<.034f,"FBX preserved stale take length");
                    var restore=ScriptableObject.CreateInstance<Motion>();JsonUtility.FromJsonOverwrite(original,restore);restore.hideFlags=HideFlags.HideAndDontSave;AnimationLabMotionAuthoring.SaveBundle(restore,ref source,AnimationLabMotionAuthoring.Revision(source));UnityEngine.Object.DestroyImmediate(restore);
                    Assert(AnimationLabPhonePublication.OwnedSource(source.generatedFbx,out bool owned)==source&&owned,"Output/source owner lookup failed");count++;
                }
                data=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);Assert(File.ReadAllBytes(AssetDatabase.GetAssetPath(data.authored)).SequenceEqual(user),"User hand source changed");Assert(data.actions.Select(e=>e.sourceId).SequenceEqual(phoneIds),"Phone action IDs changed");
                Assert(!AssetDatabase.GetDependencies(AnimationLabPhoneBuild.BindingPath,true).Any(p=>p.Contains("/Logs/")||p.Contains("/FreeWalk/")||p.Contains("/Mixamo/")),"Runtime binding depends on local QA assets");
                window.Close();Debug.Log("SQUEEZE_EDITOR_OK sources="+count+" F3F2=true authoredRootMetadata=true derivedContacts=true resizedFbxTake=true sameGuids=true storedProps=true rootTrajectory=true sameRig=true portable=true userHandUntouched=true");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);if(window!=null)window.Close();EditorApplication.Exit(2);}
        }
    }
}
#endif

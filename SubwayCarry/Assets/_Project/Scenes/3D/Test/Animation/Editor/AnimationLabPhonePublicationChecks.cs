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
    public static class AnimationLabPhonePublicationChecks
    {
        static void Assert(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        public static void OpenAcrossScenes()
        {
            AnimationLabPhoneBuild.Guard();try{
                SessionState.EraseString("SubwayCarry.MotionAuthoring.Draft");EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.Root+"/Normal.unity");
                var binding=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);string path=AssetDatabase.GetAssetPath(binding.authored);
                Assert(AnimationLabMotionWindow.OpenSavedAsset(binding.authored.generatedFbx.GetInstanceID(),0),"Cross-scene callback was not handled");
                var window=Resources.FindObjectsOfTypeAll<AnimationLabMotionWindow>().Single();Assert(window.SavedSource!=null&&AssetDatabase.GetAssetPath(window.SavedSource)==path,"Scene switch lost persistent source");window.Close();Debug.Log("PHONE_CROSS_SCENE_OPEN_OK");EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
        }
        public static void Run()
        {
            AnimationLabPhoneBuild.Guard();AnimationLabMotionWindow window=null;GameObject sample=null;Motion full=null;
            try
            {
                EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);SessionState.EraseString("SubwayCarry.MotionAuthoring.Draft");var binding=AnimationLabPhoneBuild.Build();binding.pillar=new Rect();EditorUtility.SetDirty(binding);AssetDatabase.SaveAssetIfDirty(binding);
                var source=binding.authored;Debug.Log("PHONE_INITIAL_SOURCE path="+AssetDatabase.GetAssetPath(source)+" persistent="+EditorUtility.IsPersistent(source)+" flags="+source.hideFlags+" name="+source.name+" id="+source.id);var paths=new[]{AssetDatabase.GetAssetPath(source),AssetDatabase.GetAssetPath(source.generatedClip),AssetDatabase.GetAssetPath(source.generatedFbx)};var ids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
                Assert(AnimationLabMotionWindow.OpenSavedAsset(source.generatedFbx.GetInstanceID(),0),"Owned FBX did not open source");window=Resources.FindObjectsOfTypeAll<AnimationLabMotionWindow>().Single();Assert(window.SavedSource==source,"FBX resolved wrong source ID");
                window.SelectMotionFolder();Assert(AssetDatabase.GetAssetPath(Selection.activeObject)==AnimationLabMotionAuthoring.Exports,"Top folder button opened wrong Project folder");window.SelectMotionFolder(true);Assert(AssetDatabase.GetAssetPath(Selection.activeObject)==AnimationLabMotionAuthoring.Authored,"Original folder selection failed");
                Assert(AnimationLabMotionWindow.OpenSavedAsset(source.generatedClip.GetInstanceID(),0)&&window.SavedSource==source,"Owned .anim did not open source");
                var unowned=AssetDatabase.LoadAssetAtPath<GameObject>(AnimationLabMotionAuthoring.SharedMotion);Assert(!AnimationLabMotionWindow.OpenSavedAsset(unowned.GetInstanceID(),0),"Foreign FBX opening was intercepted");
                int wrist=Array.IndexOf(source.paths,source.humanPaths[(int)HumanBodyBones.RightHand]);var original=source.Evaluate(30,false).rotations[wrist];var first=source.Evaluate(0,false).rotations[wrist];var last=source.Evaluate(60,false).rotations[wrist];
                window.Draft.loop=true;
                window.Seek(30);
                var changed=first*Quaternion.AngleAxis(3,Vector3.right);
                window.SetRotation(wrist,changed);
                window.SaveBundle();
                Assert(window.Status.StartsWith("원본 +"),"Draft-only F2 save failed: "+window.Status);
                source=AssetDatabase.LoadAssetAtPath<Motion>(paths[0]);
                Debug.Log("PHONE_SOURCE_CHECK path="+paths[0]+" exists="+File.Exists(paths[0])+" type="+AssetDatabase.GetMainAssetTypeAtPath(paths[0])+" source="+(source!=null)+" windowSource="+(window.SavedSource!=null));
                Assert(source!=null,"Published source is missing from AssetDatabase");
                Assert(source.keys!=null&&source.drafts!=null,"Published source lists are null");
                var savedPose=source.Evaluate(30,false);
                Assert(savedPose!=null&&savedPose.rotations!=null,"Published source pose is null");
                Debug.Log("PHONE_DRAFT_CHECK angle="+Quaternion.Angle(savedPose.rotations[wrist],original)+" pending="+(source.Exact(source.drafts,30)!=null)+" keys="+source.keys.Count+" drafts="+source.drafts.Count);
                Assert(Quaternion.Angle(savedPose.rotations[wrist],original)<.1f&&source.Exact(source.drafts,30)!=null,"Unconfirmed draft was published as a key");
                window.SaveKeyAdvance();Assert(window.Frame==31&&window.Draft.Exact(window.Draft.drafts,31)!=null,"F3 next-frame draft contract changed");window.SaveBundle();Assert(window.Status.Contains("Normal 적용 완료"),"F2 did not publish Normal binding: "+window.Status);source=AssetDatabase.LoadAssetAtPath<Motion>(paths[0]);binding=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);
                Assert(Quaternion.Angle(source.Evaluate(30,false).rotations[wrist],changed)<.1f&&Quaternion.Angle(source.Evaluate(0,false).rotations[wrist],first)<.1f&&Quaternion.Angle(source.Evaluate(60,false).rotations[wrist],last)<.1f,"F3 changed unrelated key frames");
                Assert(paths.Select(AssetDatabase.AssetPathToGUID).SequenceEqual(ids)&&binding.upperBody==source.generatedClip&&binding.sourceId==source.id,"F2 changed GUID/source identity or binding");
                var files=paths.Concat(new[]{AnimationLabPhoneBuild.BindingPath}).ToArray();var before=files.Select(File.ReadAllBytes).ToArray();var baseline=AnimationLabMotionAuthoring.Revision(source);window.Seek(30);window.SetRotation(wrist,changed*Quaternion.AngleAxis(2,Vector3.up));window.SaveKeyAdvance();string unfinished=JsonUtility.ToJson(window.Draft);
                AnimationLabMotionAuthoring.CandidateValidationProbe=()=>throw new IOException("QA candidate failure");window.SaveBundle();AnimationLabMotionAuthoring.CandidateValidationProbe=null;
                Assert(window.Status.Contains("이전 정상")&&files.Select((p,i)=>File.ReadAllBytes(p).SequenceEqual(before[i])).All(v=>v),"Failed candidate changed last-good files");Assert(JsonUtility.ToJson(window.Draft)==unfinished,"Failed candidate lost user keys/drafts");
                AnimationLabMotionAuthoring.PublicationProbe=()=>throw new IOException("QA publication failure");window.SaveBundle();AnimationLabMotionAuthoring.PublicationProbe=null;
                Assert(window.Status.Contains("이전 정상")&&files.Select((p,i)=>File.ReadAllBytes(p).SequenceEqual(before[i])).All(v=>v),"Partial publication did not restore exact last-good files");Assert(paths.Select(AssetDatabase.AssetPathToGUID).SequenceEqual(ids),"Rollback changed GUIDs");
                Assert(!Directory.EnumerateFiles(AnimationLabMotionAuthoring.Exports,"__Candidate_*").Any(),"Candidate assets were left in active exports");
                // Full CC0 import must retain every source frame, not three interpolated snapshots.
                sample=AnimationLabMotionAuthoring.CreateRig();full=AnimationLabMotionAuthoring.CreateFullClipCopy(sample,binding.walk);Assert(full.keys.Count==full.lastFrame+1&&full.Duration>=binding.walk.length&&full.Duration-binding.walk.length<=1f/full.sampleRate+.0001f,"Full import lost source duration/frames");
                var copied=AnimationLabMotionAuthoring.Bake(full);float angle=AnimationLabMotionAuthoring.Compare(full,copied,out float position);Assert(angle<.15f&&position<.001f,"Full sampled motion changed on bake");UnityEngine.Object.DestroyImmediate(copied);
                string evidence=Path.GetFullPath("../../../MotionMatching/PhonePassenger");Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"roundtrip.json"),JsonUtility.ToJson(new Result{sourceId=source.id,revision=binding.publishedRevision,wristPath=source.paths[wrist],before=original,after=changed,source=paths[0],clip=paths[1],fbx=paths[2],guids=ids},true));
                Debug.Log("PHONE_EDIT_ROUNDTRIP_OK FBXAndAnimResolveSource=true folderButtons=true foreignFBXNotIntercepted=true draftsNotPublished=true F3FrameAdvance=true sameGuids=true NormalBindingUpdated=true untouchedEndpointKeys=true candidateFailureSafe=true publicationRollbackExact=true fullCC0FrameImport=true");
                source.drafts.Clear();EditorUtility.SetDirty(source);AssetDatabase.SaveAssetIfDirty(source);var dependencies=AssetDatabase.GetDependencies(AnimationLabPhoneBuild.BindingPath,true);Assert(!dependencies.Any(p=>p.Contains("/FreeWalk/")||p.Contains("/Mixamo/")||p.Contains("/MotionMatching/")||p.Contains("/Logs/")),"Common review binding depends on local comparison data");File.WriteAllLines(Path.Combine(evidence,"dependencies.txt"),dependencies);window.Close();SessionState.EraseString("SubwayCarry.MotionAuthoring.Draft");EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.Root+"/Normal.unity");var reopened=AssetDatabase.LoadAssetAtPath<GameObject>(paths[2]);Assert(AnimationLabMotionWindow.OpenSavedAsset(reopened.GetInstanceID(),0),"Cross-scene FBX callback failed");window=Resources.FindObjectsOfTypeAll<AnimationLabMotionWindow>().Single();Assert(window.SavedSource!=null&&AssetDatabase.GetAssetPath(window.SavedSource)==paths[0],"Cross-scene source was unloaded");Debug.Log("PHONE_CROSS_SCENE_OPEN_OK");window.Close();UnityEngine.Object.DestroyImmediate(sample);UnityEngine.Object.DestroyImmediate(full);EditorApplication.Exit(0);
            }
            catch(Exception e){AnimationLabMotionAuthoring.CandidateValidationProbe=AnimationLabMotionAuthoring.PublicationProbe=null;Debug.LogException(e);if(window!=null)window.Close();if(sample!=null)UnityEngine.Object.DestroyImmediate(sample);if(full!=null)UnityEngine.Object.DestroyImmediate(full);EditorApplication.Exit(2);}
        }
        [Serializable] public sealed class Result{public string sourceId,revision,wristPath,source,clip,fbx;public Quaternion before,after;public string[] guids;}
    }
}
#endif

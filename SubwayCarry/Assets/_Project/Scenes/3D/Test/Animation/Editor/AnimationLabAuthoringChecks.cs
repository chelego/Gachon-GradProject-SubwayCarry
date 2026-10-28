#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Motion=SubwayCarry.Gameplay3D.AnimationLab.AnimationLabAuthoredMotion;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    [InitializeOnLoad] public static class AnimationLabAuthoringChecks
    {
        const string ReloadKey="SubwayCarry.Authoring.ReloadCheck";
        static bool requestingReload;
        static AnimationLabAuthoringChecks(){EditorApplication.update+=ReloadTick;if(Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets")&&Environment.GetCommandLineArgs().Contains("SubwayCarry.Gameplay3D.AnimationLab.AnimationLabAuthoringChecks.Interactive"))EditorApplication.delayCall+=DockInteractive;}
        static void DockInteractive()
        {
            foreach(var view in Resources.FindObjectsOfTypeAll<SceneView>())if(view.titleContent.text.Contains("isolated Motion QA"))view.Close();
            foreach(var panel in Resources.FindObjectsOfTypeAll<AnimationLabMotionWindow>())panel.Close();
            Type inspector=typeof(global::UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");window=EditorWindow.GetWindow<AnimationLabMotionWindow>("Motion Authoring · isolated QA",true,inspector);window.Show();window.Begin();
            var scene=Resources.FindObjectsOfTypeAll<SceneView>().FirstOrDefault();if(scene!=null){scene.Focus();window.FocusRig();}Debug.Log("AUTHORING_DOCKED_QA_READY");
        }
        public static void Interactive()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Isolated QA only");
            EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);DockInteractive();window.LoadSource(AssetDatabase.LoadAssetAtPath<Motion>(AnimationLabAuthoringField.Example));window.Begin();
            Debug.Log("AUTHORING_INTERACTIVE_READY isolated QA windows only");
        }
        public static void ReopenEdit()
        {
            Guard();
            try
            {
                EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);SessionState.EraseString("SubwayCarry.MotionAuthoring.Draft");
                string path=Directory.GetFiles(AnimationLabMotionAuthoring.Authored,"Controls_ToolFlowProbe*.asset").OrderByDescending(File.GetLastWriteTimeUtc).First();var source=AssetDatabase.LoadAssetAtPath<Motion>(path);
                Assert(source!=null&&source.keys.Count==3&&source.Exact(source.drafts,0)!=null&&source.Exact(source.drafts,1)!=null,"Disk source did not preserve keys and per-frame drafts across editor restart");
                string[] paths={path,AssetDatabase.GetAssetPath(source.generatedClip),AssetDatabase.GetAssetPath(source.generatedFbx)};string[] ids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
                Assert(AssetDatabase.OpenAsset(source),"Unity asset open callback not handled");window=Resources.FindObjectsOfTypeAll<AnimationLabMotionWindow>().Single();
                Assert(window.Rig!=null&&window.Draft.id==source.id&&window.Draft!=source,"Source did not reopen as independent editable draft");
                window.SelectSaved(source);Assert(Selection.activeObject==source,"Find saved source button selected wrong file");window.SelectSaved(source.generatedClip);Assert(Selection.activeObject==source.generatedClip,"Find Unity clip button failed");window.SelectSaved(source.generatedFbx);Assert(Selection.activeObject==source.generatedFbx,"Find FBX button failed");
                window.Seek(30);int joint=Array.IndexOf(source.paths,source.humanPaths[(int)HumanBodyBones.LeftShoulder]);var edited=window.Draft.Evaluate(30).rotations[joint]*Quaternion.Euler(0,0,2);
                window.SetRotation(joint,edited);Key(KeyCode.F3);Key(KeyCode.F2);Assert(window.Status.StartsWith("원본 +"),"Reopened source was not resaved/exported: "+window.Status);
                Assert(paths.Select(AssetDatabase.AssetPathToGUID).SequenceEqual(ids),"Re-edit changed source/output GUIDs");window.Close();SessionState.EraseString("SubwayCarry.MotionAuthoring.Draft");
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);source=AssetDatabase.LoadAssetAtPath<Motion>(path);AssetDatabase.OpenAsset(source);window=Resources.FindObjectsOfTypeAll<AnimationLabMotionWindow>().Single();
                Assert(Quaternion.Angle(window.Draft.Exact(window.Draft.keys,30).rotations[joint],edited)<.01f,"Re-edit did not persist on disk/reopen");
                var baked=AssetDatabase.LoadAssetAtPath<AnimationClip>(paths[1]);float angle=AnimationLabMotionAuthoring.Compare(source,baked,out float position);Assert(angle<.15f&&position<.001f,"Updated Unity animation differs from source");
                var fbx=AnimationLabMotionAuthoring.ImportedClip(paths[2]);angle=AnimationLabMotionAuthoring.Compare(source,fbx,out position);Assert(angle<.15f&&position<.001f,"Updated FBX differs from source");
                var dependencies=AssetDatabase.GetDependencies(new[]{AnimationLabAuthoringField.Example,AnimationLabMotionAuthoring.ScenePath},true);Assert(!dependencies.Any(p=>p.Contains("/FreeWalk/")||p.Contains("/Logs/")),"Shared source/scene still references local-only assets");Directory.CreateDirectory("Library/ShareVerification");File.WriteAllLines("Library/ShareVerification/Dependencies.txt",dependencies);
                Debug.Log("AUTHORING_SHARED_DEPENDENCIES_OK count="+dependencies.Length+" localFreeWalkReferences=0");
                Debug.Log("AUTHORING_REOPEN_EDIT_OK freshEditorProcess=true assetDoubleClickCallback=true keysAndDraftsFromDisk=true independentDraft=true sourceAnimFbxButtons=true reeditResaveReopen=true allGuidsStable=true unityAndFbxUpdated=true");
                Cleanup();EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);Cleanup();EditorApplication.Exit(2);}
        }
        public static void Proof()
        {
            Guard();
            try
            {
                EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);window=EditorWindow.GetWindow<AnimationLabMotionWindow>();window.LoadSource(AssetDatabase.LoadAssetAtPath<Motion>(AnimationLabAuthoringField.Example));
                imported=AnimationLabMotionAuthoring.ImportedClip(AssetDatabase.GetAssetPath(window.Draft.generatedFbx));importedRig=AnimationLabMotionAuthoring.CreateRig();AnimationLabMotionAuthoring.StripAnimators(importedRig);
                target=new RenderTexture(640,640,24);pixels=new Texture2D(1280,640,TextureFormat.RGB24,false);Directory.CreateDirectory(Path.Combine(Evidence,"authoring-preview"));shot=0;
                for(int frame=0;frame<=window.Draft.lastFrame;frame++){window.Seek(frame);Capture(frame/(float)window.Draft.sampleRate);}
                Debug.Log("AUTHORING_FULL_CLIP_PROOF samples=61 first=0 last=2 sourceAndReimportedFbx=true deterministicSampling=true notAnInputRecording=true");Cleanup();EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);Cleanup();EditorApplication.Exit(2);}
        }
        public static void PelvisEditing()
        {
            Guard();float maximumFootError=0;int samples=0;
            try
            {
                EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);window=EditorWindow.GetWindow<AnimationLabMotionWindow>();
                var example=AssetDatabase.LoadAssetAtPath<Motion>(AnimationLabAuthoringField.Example);string diskBefore=File.ReadAllText(AssetDatabase.GetAssetPath(example));
                int Joint(Motion data,HumanBodyBones human)=>Array.IndexOf(data.paths,data.humanPaths[(int)human]);
                window.LoadSource(example);window.Seek(17);var rootBones=AnimationLabMotionAuthoring.Bind(window.Draft,window.Rig);var rootBefore=rootBones.Select(t=>t.position).ToArray();var localBefore=rootBones.Select(t=>t.localPosition).ToArray();
                var center=window.RootHandlePosition;Assert(center.y>rootBones[window.Draft.hipsIndex].position.y&&center.y<rootBones[Joint(window.Draft,HumanBodyBones.Chest)].position.y,"Whole-model handle is not at torso center");
                var rootDelta=new Vector3(.12f,.04f,.08f);Assert(window.MoveRootHandleWorld(center+rootDelta),"Whole-model center handle failed");
                for(int i=0;i<rootBones.Length;i++){Assert(Vector3.Distance(rootBones[i].position,rootBefore[i]+rootDelta)<.00001f,"Root center drag did not translate whole model");if(i>0)Assert(Vector3.Distance(rootBones[i].localPosition,localBefore[i])<.000001f,"Root center drag changed local pose");}
                Assert(Vector3.Distance(window.RootHandlePosition,center+rootDelta)<.00001f,"Root center handle jumped relative to body");
                for(int mode=0;mode<4;mode++)
                {
                    window.LoadSource(example);window.Seek(17);window.PelvisFootLock=mode;var data=window.Draft;var bones=AnimationLabMotionAuthoring.Bind(data,window.Rig);
                    int left=Joint(data,HumanBodyBones.LeftFoot),right=Joint(data,HumanBodyBones.RightFoot);var fixedPositions=bones.Select(t=>t.localPosition).ToArray();var scales=bones.Select(t=>t.localScale).ToArray();
                    var feet=new[]{bones[left].position,bones[right].position};var rotations=new[]{bones[left].rotation,bones[right].rotation};var hips=bones[data.hipsIndex].position;var before=data.Evaluate(17);string keys=string.Join("|",data.keys.Select(JsonUtility.ToJson));
                    float PinError(){float error=0;for(int side=0;side<2;side++)if(mode==0||mode==side+1){int foot=side==0?left:right;error=Mathf.Max(error,Vector3.Distance(bones[foot].position,feet[side]));Assert(Quaternion.Angle(bones[foot].rotation,rotations[side])<.1f,"Pinned foot rotation changed");}return error;}
                    void FixedBones(){for(int i=0;i<bones.Length;i++){Assert(Vector3.Distance(bones[i].localScale,scales[i])<.000001f,"Pelvis edit changed scale");if(i!=0&&i!=data.hipsIndex)Assert(Vector3.Distance(bones[i].localPosition,fixedPositions[i])<.000001f,"Pelvis edit changed bone length");}Assert(Vector3.Distance(bones[0].localPosition,before.root)<.000001f&&Quaternion.Angle(bones[0].localRotation,before.rotations[0])<.1f,"Pelvis moved the rig ROOT");}
                    Vector3 delta=window.Rig.transform.forward*.08f-Vector3.up*.03f;
                    for(int step=1;step<=24;step++)
                    {
                        Assert(window.MoveJointWorld(data.hipsIndex,hips+delta*(step/24f)),"Pelvis drag failed");float error=PinError();maximumFootError=Mathf.Max(maximumFootError,error);Assert(error<.00015f,"Planted foot slipped mode="+mode+" error="+error);FixedBones();samples++;
                    }
                    Assert(Vector3.Distance(bones[data.hipsIndex].position,hips)>.02f,"Reachable pelvis drag did not move");
                    if(mode==3)Assert(Vector3.Distance(bones[left].position,feet[0]+delta)<.0001f&&Vector3.Distance(bones[right].position,feet[1]+delta)<.0001f,"Released feet stopped following pelvis");
                    else
                    {
                        int heldUpper=Joint(data,mode==2?HumanBodyBones.RightUpperLeg:HumanBodyBones.LeftUpperLeg);Assert(Quaternion.Angle(bones[heldUpper].localRotation,before.rotations[heldUpper])>.5f,"Planted leg did not compensate");
                        if(mode==1||mode==2){int released=mode==1?right:left;var original=mode==1?feet[1]:feet[0];Assert(Vector3.Distance(bones[released].position,original+bones[data.hipsIndex].position-hips)<.0001f,"Released leg was unexpectedly pinned");}
                    }
                    Assert(keys==string.Join("|",data.keys.Select(JsonUtility.ToJson)),"Pelvis drag overwrote committed keys");
                    Undo.FlushUndoRecordObjects();Undo.IncrementCurrentGroup();window.ResetPositionDrag();var undoPose=data.Evaluate(17);window.MoveJointWorld(data.hipsIndex,bones[data.hipsIndex].position+window.Rig.transform.right*.025f-Vector3.up*.01f);Undo.FlushUndoRecordObjects();var redoPose=data.Evaluate(17);Undo.PerformUndo();
                    Assert(Vector3.Distance(data.Evaluate(17).hips,undoPose.hips)<.000001f,"Pelvis Undo failed");Undo.PerformRedo();Assert(Vector3.Distance(data.Evaluate(17).hips,redoPose.hips)<.000001f,"Pelvis Redo failed");FixedBones();
                    if(mode!=3)
                    {
                        foreach(var far in new[]{Vector3.up*100,-Vector3.up*100,window.Rig.transform.forward*100})
                        {
                            window.ResetPositionDrag();var heldBefore=new[]{bones[left].position,bones[right].position};Assert(window.MoveJointWorld(data.hipsIndex,bones[data.hipsIndex].position+far),"Unreachable pelvis target rejected instead of constrained");
                            for(int side=0;side<2;side++)if(mode==0||mode==side+1)Assert(Vector3.Distance(bones[side==0?left:right].position,heldBefore[side])<.00015f,"Unreachable target moved pinned foot");FixedBones();
                            foreach(var chain in new[]{new[]{HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot},new[]{HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot}})
                            {var u=bones[Joint(data,chain[0])];var m=bones[Joint(data,chain[1])];var t=bones[Joint(data,chain[2])];float bend=180-Vector3.Angle(u.position-m.position,t.position-m.position);Assert(bend<145.1f,"Pelvis constraint overfolded knee");}
                        }
                    }
                }
                window.LoadSource(example);window.Seek(17);window.PelvisFootLock=0;var motion=window.Draft;var live=AnimationLabMotionAuthoring.Bind(motion,window.Rig);
                window.SetRotation(0,Quaternion.Euler(0,67,0)*live[0].localRotation);var leftHeld=live[Joint(motion,HumanBodyBones.LeftFoot)].position;var rightHeld=live[Joint(motion,HumanBodyBones.RightFoot)].position;
                window.MoveJointWorld(motion.hipsIndex,live[motion.hipsIndex].position+window.Rig.transform.forward*.06f-Vector3.up*.025f);
                Assert(Vector3.Distance(live[Joint(motion,HumanBodyBones.LeftFoot)].position,leftHeld)<.00015f&&Vector3.Distance(live[Joint(motion,HumanBodyBones.RightFoot)].position,rightHeld)<.00015f,"Rotated rig pelvis pin failed");
                window.ResetPositionDrag();window.SetPosition(motion.hipsIndex,live[motion.hipsIndex].localPosition+new Vector3(.01f,-.01f,.01f));Assert(Vector3.Distance(live[Joint(motion,HumanBodyBones.LeftFoot)].position,leftHeld)<.00015f,"Numeric pelvis editing bypassed foot pins");
                window.Remember();string json=JsonUtility.ToJson(motion);window.Close();window=EditorWindow.GetWindow<AnimationLabMotionWindow>();window.Begin();Assert(window.Frame==17&&JsonUtility.ToJson(window.Draft)==json,"Pelvis edit lost unfinished draft/frame on reopen");
                window.ForkForAuthor();window.Draft.author="PelvisChecks";window.Draft.clipName="Pinned_"+Guid.NewGuid().ToString("N").Substring(0,8);window.SaveKeyAdvance();window.SaveBundle();Assert(window.Status.StartsWith("원본 +"),"Pelvis FK/FBX export failed: "+window.Status);
                Assert(File.ReadAllText(AssetDatabase.GetAssetPath(example))==diskBefore,"Pelvis checks overwrote source example");
                Debug.Log("AUTHORING_PELVIS_EDIT_OK samples="+samples+" maxFootErrorMeters="+maximumFootError+" torsoRootHandle=true noRootHandleJump=true wholeModelTranslation=true bothSingleAndFree=true fixedLengthsAndScales=true rootUnchanged=true continuousDrag=true unreachableClamped=true rotatedRig=true numericEntry=true undoRedo=true keysPreserved=true unfinishedDraftReopens=true FKAnimFbxRoundTrip=true sourceUntouched=true");Cleanup();EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);Cleanup();EditorApplication.Exit(2);}
        }
        public static void PositionEditing()
        {
            Guard();
            try
            {
                EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);window=EditorWindow.GetWindow<AnimationLabMotionWindow>();window.LoadSource(AssetDatabase.LoadAssetAtPath<Motion>(AnimationLabAuthoringField.Example));window.Seek(0);
                var data=window.Draft;var bones=AnimationLabMotionAuthoring.Bind(data,window.Rig);var positions=bones.Select(t=>t.localPosition).ToArray();var scales=bones.Select(t=>t.localScale).ToArray();
                Key(KeyCode.E);Assert(!window.PositionTool,"E rotation shortcut failed");window.UpdateSceneNavigation(new Event{type=EventType.MouseDown,button=1});
                foreach(var key in new[]{KeyCode.W,KeyCode.A,KeyCode.S,KeyCode.D,KeyCode.E,KeyCode.Space}){var e=new Event{type=EventType.KeyDown,keyCode=key};window.HandleKey(e,true);Assert(e.type==EventType.KeyDown&&!window.PositionTool&&!window.Previewing,"Camera navigation key was consumed: "+key);}
                window.UpdateSceneNavigation(new Event{type=EventType.MouseUp,button=1});var editKey=new Event{type=EventType.KeyDown,keyCode=KeyCode.W};window.HandleKey(editKey,true);Assert(window.PositionTool&&editKey.type==EventType.Used,"Position shortcut did not return after camera navigation");
                int Joint(HumanBodyBones human)=>Array.IndexOf(data.paths,data.humanPaths[(int)human]);
                void FixedLengths(){for(int i=0;i<bones.Length;i++){Assert(Vector3.Distance(bones[i].localScale,scales[i])<.000001f,"Position edit changed bone scale");if(i!=0&&i!=data.hipsIndex)Assert(Vector3.Distance(bones[i].localPosition,positions[i])<.000001f,"Position edit changed bone length");}}
                foreach(var human in new[]{HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot})
                {
                    window.Seek(0);int tip=Joint(human);var before=data.Evaluate(0);var rotation=bones[tip].rotation;var start=bones[tip].position;
                    var target=start+window.Rig.transform.forward*.08f+Vector3.up*.07f;
                    Assert(window.MoveJointWorld(tip,target),"Tip translation unsupported: "+human);Undo.FlushUndoRecordObjects();
                    Assert(Vector3.Distance(bones[tip].position,target)<.001f,"Reachable tip did not reach target: "+human);bool leg=human==HumanBodyBones.LeftFoot||human==HumanBodyBones.RightFoot;Assert(leg?Quaternion.Angle(rotation,bones[tip].rotation)<.1f:Quaternion.Angle(before.rotations[tip],bones[tip].localRotation)<.1f,"Foot world/hand local orientation changed");FixedLengths();
                    Undo.PerformUndo();var restored=data.Evaluate(0);float undoAngle=Enumerable.Range(0,before.rotations.Length).Max(i=>Quaternion.Angle(before.rotations[i],restored.rotations[i]));
                    Assert(undoAngle<.1f&&Vector3.Distance(before.root,restored.root)<.000001f&&Vector3.Distance(before.hips,restored.hips)<.000001f,"IK edit Undo changed source pose: "+human+" maxAngle="+undoAngle+" root="+Vector3.Distance(before.root,restored.root)+" hips="+Vector3.Distance(before.hips,restored.hips));Undo.PerformRedo();Assert(Vector3.Distance(bones[tip].position,target)<.001f,"IK Redo failed");
                    window.SaveKeyAdvance();window.Seek(0);
                }
                foreach(var human in new[]{HumanBodyBones.LeftLowerArm,HumanBodyBones.RightLowerArm,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg})
                {
                    window.ResetPositionDrag();int middle=Joint(human);
                    var end=human==HumanBodyBones.LeftLowerArm?HumanBodyBones.LeftHand:human==HumanBodyBones.RightLowerArm?HumanBodyBones.RightHand:human==HumanBodyBones.LeftLowerLeg?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot;var tip=bones[Joint(end)];var held=tip.position;bool leg=end==HumanBodyBones.LeftFoot||end==HumanBodyBones.RightFoot;var rotation=leg?tip.rotation:tip.localRotation;
                    Assert(window.MoveJointWorld(middle,bones[middle].position+window.Rig.transform.right*.04f),"Bend hint unsupported");Assert(Vector3.Distance(tip.position,held)<.001f&&Quaternion.Angle(leg?tip.rotation:tip.localRotation,rotation)<.1f,"Bend hint displaced tip");FixedLengths();
                }
                window.Seek(12);var stationaryHips=bones[data.hipsIndex].position;var leftFoot=bones[Joint(HumanBodyBones.LeftFoot)].position;var rightFoot=bones[Joint(HumanBodyBones.RightFoot)].position;
                foreach(var side in new[]{HumanBodyBones.LeftShoulder,HumanBodyBones.RightShoulder})
                {
                    window.ResetPositionDrag();var before=data.Evaluate(12);int shoulder=Joint(side);var target=window.MoveHandlePosition(shoulder)+window.Rig.transform.forward*.025f+Vector3.up*.015f;
                    Assert(window.MoveJointWorld(shoulder,target)&&Vector3.Distance(window.MoveHandlePosition(shoulder),target)<.003f,"Shoulder control did not reach small target: "+side);FixedLengths();
                    Assert(Vector3.Distance(bones[data.hipsIndex].position,stationaryHips)<.00001f&&Vector3.Distance(bones[Joint(HumanBodyBones.LeftFoot)].position,leftFoot)<.00001f&&Vector3.Distance(bones[Joint(HumanBodyBones.RightFoot)].position,rightFoot)<.00001f,"Shoulder control moved pelvis/feet");
                    float body= new[]{HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.UpperChest}.Max(h=>Quaternion.Angle(before.rotations[Joint(h)],bones[Joint(h)].localRotation));Assert(body>.01f,"Shoulder adjustment did not distribute into upper body");
                }
                window.Seek(18);int smoothHand=Joint(HumanBodyBones.LeftHand);var smoothStart=bones[smoothHand].position;var last=smoothStart;
                for(int step=1;step<=24;step++){var target=smoothStart+window.Rig.transform.forward*(.12f*step/24)+Vector3.up*(.08f*step/24);Assert(window.MoveJointWorld(smoothHand,target),"Repeated hand drag failed");Assert(Vector3.Distance(last,bones[smoothHand].position)<.025f,"Continuous hand drag jumped");last=bones[smoothHand].position;FixedLengths();}
                window.ResetPositionDrag();int hand=Joint(HumanBodyBones.LeftHand),upper=Joint(HumanBodyBones.LeftUpperArm),middleArm=Joint(HumanBodyBones.LeftLowerArm);float arm=Vector3.Distance(bones[upper].position,bones[middleArm].position)+Vector3.Distance(bones[middleArm].position,bones[hand].position);
                Assert(window.MoveJointWorld(hand,bones[upper].position+Vector3.up*100),"Far target rejected");Assert(Vector3.Distance(bones[upper].position,bones[hand].position)<arm,"Far target stretched arm");Assert(window.MoveJointWorld(hand,bones[upper].position),"Root target rejected");FixedLengths();
                window.ResetPositionDrag();var original=window.Rig.transform.rotation;window.SetRotation(0,Quaternion.Euler(0,67,0)*original);int foot=Joint(HumanBodyBones.RightFoot);var rotatedTarget=bones[foot].position+window.Rig.transform.forward*.04f+Vector3.up*.05f;Assert(window.MoveJointWorld(foot,rotatedTarget)&&Vector3.Distance(bones[foot].position,rotatedTarget)<.001f,"Rotated rig target failed");FixedLengths();window.SetRotation(0,original);
                window.Seek(30);int draftCount=data.drafts.Count;Assert(draftCount>0,"Position edits lost frame drafts");window.Remember();string draftJson=JsonUtility.ToJson(data);window.Close();window=EditorWindow.GetWindow<AnimationLabMotionWindow>();window.Begin();Assert(JsonUtility.ToJson(window.Draft)==draftJson,"Position editing lost draft on reopen");
                window.ForkForAuthor();window.Draft.author="PositionChecks";window.Draft.clipName="Pose_"+Guid.NewGuid().ToString("N").Substring(0,8);foreach(int keyFrame in new[]{0,12,18}){window.Seek(keyFrame);window.SaveKeyAdvance();}Key(KeyCode.F2);Assert(window.Status.StartsWith("원본 +"),"Position edited FK/FBX save failed: "+window.Status);
                Debug.Log("AUTHORING_POSITION_EDIT_OK cameraWASDEAndSpacePassThrough=true editWEOutsideNavigation=true fourEndTargets=true fourBendHints=true bothShouldersAndUpperBody=true pelvisAndFeetStable=true continuous24StepDrag=true boneLengthsFixed=true scalesFixed=true handFollowsArmFootWorldRotationHeld=true undoRedo=true unreachableClamp=true rotatedRig=true draftsReopen=true FKAnimFbxRoundTrip=true");Cleanup();EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);Cleanup();EditorApplication.Exit(2);}
        }
        public static void TeamSharing()
        {
            Guard();GameObject sample=null;Motion draft=null;string duplicate="";
            try
            {
                string example=AnimationLabAuthoringField.Example;var old=AssetDatabase.LoadAssetAtPath<Motion>(example);string[] prior=old!=null?new[]{example,AssetDatabase.GetAssetPath(old.generatedClip),AssetDatabase.GetAssetPath(old.generatedFbx)}:Array.Empty<string>();string[] ids=prior.Select(AssetDatabase.AssetPathToGUID).ToArray();
                AnimationLabAuthoringField.Build();Assert(prior.Select(AssetDatabase.AssetPathToGUID).SequenceEqual(ids),"Shared CC0 example regeneration changed GUIDs");
                EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);var shared=AssetDatabase.LoadAssetAtPath<Motion>(example);Assert(AssetDatabase.GetAssetPath(shared.startingClip)==AnimationLabMotionAuthoring.SharedMotion,"Example still depends on local motion");
                sample=AnimationLabMotionAuthoring.CreateRig();draft=AnimationLabMotionAuthoring.CreateSource(sample);Assert(AssetDatabase.GetAssetPath(draft.startingClip)==AnimationLabMotionAuthoring.SharedMotion,"Blank authoring still uses local clips");
                Motion saved=null;bool rejected=false;try{AnimationLabMotionAuthoring.SaveBundle(draft,ref saved,"");}catch(InvalidOperationException e){rejected=e.Message.Contains("제작자");}Assert(rejected,"Unnamed team author was accepted");
                draft.author="TeamA";draft.clipName="Shared_"+Guid.NewGuid().ToString("N").Substring(0,8);string a=AnimationLabMotionAuthoring.SaveBundle(draft,ref saved,"");Assert(a.StartsWith(AnimationLabMotionAuthoring.Authored+"/TeamA_",StringComparison.Ordinal),"New source missing author prefix");
                foreach(var clip in new[]{saved.generatedClip,AnimationLabMotionAuthoring.ImportedClip(AssetDatabase.GetAssetPath(saved.generatedFbx))})
                {
                    var root=AnimationLabMotionAuthoring.CreateRig();Motion reused=null;try{reused=AnimationLabMotionAuthoring.CreateSource(root,clip,.5f);var expected=saved.Evaluate(15,false);for(int i=0;i<reused.paths.Length;i++)Assert(Quaternion.Angle(reused.bindRotations[i],expected.rotations[i])<.15f,"Authored start clip sampled on wrong hierarchy root");}finally{if(reused!=null)UnityEngine.Object.DestroyImmediate(reused);UnityEngine.Object.DestroyImmediate(root);}
                }
                string original=File.ReadAllText(a),aId=saved.id;string[] outputA={a,AssetDatabase.GetAssetPath(saved.generatedClip),AssetDatabase.GetAssetPath(saved.generatedFbx)};string[] guidA=outputA.Select(AssetDatabase.AssetPathToGUID).ToArray();
                window=EditorWindow.GetWindow<AnimationLabMotionWindow>();window.LoadSource(saved);window.ForkForAuthor();Assert(window.SavedSource==null&&window.Draft.id!=aId&&window.Draft.generatedClip==null&&window.Draft.generatedFbx==null,"Fork retained original ownership");window.Draft.author="TeamB";Key(KeyCode.F2);Assert(window.Status.StartsWith("원본 +"),window.Status);string b=AssetDatabase.GetAssetPath(window.SavedSource);
                Assert(b.StartsWith(AnimationLabMotionAuthoring.Authored+"/TeamB_",StringComparison.Ordinal)&&b!=a,"Team B path collision");Assert(File.ReadAllText(a)==original&&outputA.Select(AssetDatabase.AssetPathToGUID).SequenceEqual(guidA),"Fork save changed Team A source/outputs");
                duplicate=AnimationLabMotionAuthoring.Authored+"/QA_Duplicate_"+Guid.NewGuid().ToString("N")+".asset";Assert(AssetDatabase.CopyAsset(a,duplicate),"Duplicate fixture creation failed");rejected=false;try{AnimationLabMotionAuthoring.SaveBundle(saved,ref saved,AnimationLabMotionAuthoring.Revision(saved));}catch(InvalidOperationException e){rejected=e.Message.Contains("같은 원본 ID");}Assert(rejected&&File.ReadAllText(a)==original,"Copied source can overwrite original outputs");AssetDatabase.DeleteAsset(duplicate);duplicate="";
                Debug.Log("AUTHORING_TEAM_SHARING_OK CC0Default=true localMotionNotReferenced=true exampleGuidsStable=true authorPrefix=true independentFork=true reuseAnimAndFbxStart=true duplicateIdGuard=true originalFilesUnchanged=true");Cleanup();EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);Cleanup();EditorApplication.Exit(2);}
            finally{if(duplicate.Length>0)AssetDatabase.DeleteAsset(duplicate);if(sample!=null)UnityEngine.Object.DestroyImmediate(sample);if(draft!=null)UnityEngine.Object.DestroyImmediate(draft);}
        }
        public static void Reload()
        {
            Guard();
            try
            {
                EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);var before=QualitySettings.renderPipeline;SessionState.SetString(ReloadKey+".Pipeline",before!=null?AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(before)):"");
                window=EditorWindow.GetWindow<AnimationLabMotionWindow>();window.Show();window.LoadSource(AssetDatabase.LoadAssetAtPath<Motion>(AnimationLabAuthoringField.Example));window.Seek(17);
                int joint=Array.IndexOf(window.Draft.paths,window.Draft.humanPaths[(int)HumanBodyBones.Neck]);window.SetRotation(joint,window.Draft.Evaluate(17).rotations[joint]*Quaternion.Euler(2,0,0));window.Remember();
                var hips=window.Rig.transform.Find(window.Draft.paths[window.Draft.hipsIndex]);window.PelvisFootLock=0;window.MoveJointWorld(window.Draft.hipsIndex,hips.position+window.Rig.transform.forward*.06f-Vector3.up*.02f);window.Remember();SessionState.SetString(ReloadKey+".FullDraft",JsonUtility.ToJson(window.Draft));
                SessionState.SetString(ReloadKey+".Pose",JsonUtility.ToJson(window.Draft.Evaluate(17)));SessionState.SetInt(ReloadKey,1);requestingReload=true;EditorUtility.RequestScriptReload();
            }
            catch(Exception e){Debug.LogException(e);Cleanup();EditorApplication.Exit(2);}
        }
        static void ReloadTick()
        {
            if(SessionState.GetInt(ReloadKey,0)!=1||requestingReload)return;
            try
            {
                window=EditorWindow.GetWindow<AnimationLabMotionWindow>();window.Show();window.Begin();Assert(window.Frame==17&&window.Draft.Exact(window.Draft.drafts,17)!=null,"Reload lost frame/draft");Assert(JsonUtility.ToJson(window.Draft.Evaluate(17))==SessionState.GetString(ReloadKey+".Pose",""),"Reload changed pose");
                Assert(JsonUtility.ToJson(window.Draft)==SessionState.GetString(ReloadKey+".FullDraft",""),"Reload changed unfinished keys/drafts/source metadata");
                window.Close();var restored=QualitySettings.renderPipeline;string guid=restored!=null?AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(restored)):"";Assert(guid==SessionState.GetString(ReloadKey+".Pipeline",""),"Reload/close did not restore preview pipeline");
                Debug.Log("AUTHORING_RELOAD_OK exactFrame=17 draftPose=true fullUnfinishedDraft=true pelvisPinsBakedToFK=true pipelineRestored=true sourceUntouched=true");SessionState.SetInt(ReloadKey,0);EditorApplication.Exit(0);
            }
            catch(Exception e){SessionState.SetInt(ReloadKey,0);Debug.LogException(e);Cleanup();EditorApplication.Exit(2);}
        }
        static AnimationLabMotionWindow window;
        static GameObject importedRig;
        static AnimationClip imported;
        static RenderTexture target;
        static Texture2D pixels;
        static double nextShot;
        static int shot;
        static readonly System.Collections.Generic.List<float> times=new System.Collections.Generic.List<float>();
        static string Evidence=>Path.Combine(System.IO.Directory.GetParent(Application.dataPath).Parent.Parent.Parent.FullName,"MotionMatching");
        static void Guard(){if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Isolated QA only");}
        static void Assert(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        public static void PreparePortableRig()
        {
            Guard();GameObject rig=null;
            try
            {
                string target=AnimationLabMotionAuthoring.Root+"/Prefabs/MotionRig.prefab";
                Assert(!File.Exists(target),"Portable rig already exists; preserve it");
                rig=AnimationLabMotionAuthoring.CreateRig(AssetDatabase.LoadAssetAtPath<GameObject>(AnimationLabMotionAuthoring.Root+"/Prefabs/Character.prefab"));
                string signature=AnimationLabMotionAuthoring.Signature(rig);var originals=AnimationLabMotionAuthoring.Bones(rig);var positions=originals.Select(t=>t.localPosition).ToArray();var rotations=originals.Select(t=>t.localRotation).ToArray();
                if(PrefabUtility.IsPartOfPrefabInstance(rig))PrefabUtility.UnpackPrefabInstance(rig,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                foreach(var animator in rig.GetComponentsInChildren<Animator>(true)){animator.runtimeAnimatorController=null;animator.applyRootMotion=false;}
                foreach(var t in rig.GetComponentsInChildren<Transform>(true))t.gameObject.hideFlags=HideFlags.None;
                var saved=PrefabUtility.SaveAsPrefabAsset(rig,target);Assert(saved!=null,"Portable rig save failed");
                var copy=AnimationLabMotionAuthoring.CreateRig(saved);
                try{var bones=AnimationLabMotionAuthoring.Bones(copy);Assert(signature==AnimationLabMotionAuthoring.Signature(copy)&&bones.Length==originals.Length,"Portable rig changed skeleton/scale");for(int i=0;i<bones.Length;i++)Assert(Vector3.Distance(positions[i],bones[i].localPosition)<.000001f&&Quaternion.Angle(rotations[i],bones[i].localRotation)<.1f,"Portable rig changed reference pose");Assert(copy.GetComponentsInChildren<MonoBehaviour>(true).Length==0&&copy.GetComponentsInChildren<Animator>(true).All(a=>a.runtimeAnimatorController==null),"Portable rig still depends on gameplay/lab scripts");}finally{UnityEngine.Object.DestroyImmediate(copy);}
                Debug.Log("AUTHORING_PORTABLE_RIG_OK identicalHierarchyPoseScale=true gameplayLabComponents=0 animationController=none");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
            finally{if(rig!=null)UnityEngine.Object.DestroyImmediate(rig);}
        }
        public static void PrepareSharedExample()
        {
            Guard();Motion draft=null;
            try
            {
                var source=AssetDatabase.LoadAssetAtPath<Motion>(AnimationLabAuthoringField.Example);var paths=new[]{AnimationLabAuthoringField.Example,AssetDatabase.GetAssetPath(source.generatedClip),AssetDatabase.GetAssetPath(source.generatedFbx)};var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();var poses=JsonUtility.ToJson(source.keys[0]);string revision=AnimationLabMotionAuthoring.Revision(source);draft=UnityEngine.Object.Instantiate(source);
                AnimationLabMotionAuthoring.SaveBundle(draft,ref source,revision);Assert(paths.Select(AssetDatabase.AssetPathToGUID).SequenceEqual(guids)&&poses==JsonUtility.ToJson(source.keys[0]),"Portable example changed GUID or pose");
                var dependencies=AssetDatabase.GetDependencies(new[]{AnimationLabAuthoringField.Example,AnimationLabMotionAuthoring.ScenePath},true);Assert(!dependencies.Any(p=>p.Contains("/MotionMatching/")||p.Contains("/FreeWalk/")||p.EndsWith("AnimationLabCharacter.cs")||p.EndsWith("AnimationLabLibrary.cs")),"Shared module still needs unshared locomotion experiments");Directory.CreateDirectory("Library/ShareVerification");File.WriteAllLines("Library/ShareVerification/Dependencies.txt",dependencies);
                Debug.Log("AUTHORING_MINIMAL_SHARE_OK examplePoseGuidsPreserved=true privateLocomotionDependencies=0 dependencies="+dependencies.Length);EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
            finally{if(draft!=null)UnityEngine.Object.DestroyImmediate(draft);}
        }
        static void Key(KeyCode key)=>window.HandleKey(new Event{type=EventType.KeyDown,keyCode=key});
        public static void Workflow()
        {
            Guard();
            try
            {
                AnimationLabAuthoringField.Build();EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);
                var previous=QualitySettings.renderPipeline;SessionState.EraseString("SubwayCarry.MotionAuthoring.Draft");
                window=EditorWindow.GetWindow<AnimationLabMotionWindow>();window.Show();Key(KeyCode.F1);Assert(window.Rig!=null,"F1 did not create editor rig");
                var data=window.Draft;data.clipName="ToolFlowProbe";data.author="Controls";
                int shoulder=Array.IndexOf(data.paths,data.humanPaths[(int)HumanBodyBones.LeftShoulder]);var original=data.Evaluate(0).rotations[shoulder];
                window.SetRotation(shoulder,original*Quaternion.Euler(0,0,3));Undo.FlushUndoRecordObjects();Key(KeyCode.F3);
                Assert(window.Frame==1&&data.keys.Count==1&&data.Exact(data.drafts,1)!=null,"F3 save/advance/draft separation failed");
                Key(KeyCode.LeftArrow);Assert(window.Frame==0,"Left arrow failed");window.SetRotation(shoulder,original*Quaternion.Euler(0,0,5));Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();Assert(data.Exact(data.drafts,0)==null,"Undo did not restore prior draft");Undo.PerformRedo();Assert(data.Exact(data.drafts,0)!=null,"Redo lost frame draft");
                Key(KeyCode.RightArrow);Assert(window.Frame==1&&data.keys.Count==1&&data.Exact(data.drafts,0)!=null,"Frame navigation lost draft or created a key");
                Assert(Quaternion.Angle(data.Evaluate(1).rotations[shoulder],original*Quaternion.Euler(0,0,3))<.01f,"Next draft was overwritten by previous-frame edit");
                window.Seek(30);window.SetRotation(shoulder,original*Quaternion.Euler(0,0,-3));Key(KeyCode.F3);window.Seek(60);Key(KeyCode.F3);Key(KeyCode.RightArrow);Assert(window.Frame==60,"End frame overflow");window.Seek(0);Key(KeyCode.LeftArrow);Assert(window.Frame==0,"Frame zero overflow");
                window.Seek(30);Assert(window.Ghost.activeSelf&&window.Ghost.GetComponentsInChildren<Collider>().Length==0&&window.Ghost.GetComponentsInChildren<Renderer>().All(r=>r.shadowCastingMode==ShadowCastingMode.Off),"Ghost collision/shadow separation failed");
                Key(KeyCode.F2);Assert(window.Status.StartsWith("원본 +"),"F2 save/export failed: "+window.Status);
                string path=AssetDatabase.GetAssetPath(window.SavedSource);var source=AssetDatabase.LoadAssetAtPath<Motion>(path);Assert(source!=null,"Source missing");
                string[] paths={path,AssetDatabase.GetAssetPath(source.generatedClip),AssetDatabase.GetAssetPath(source.generatedFbx)};string[] ids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
                window.Close();Assert(QualitySettings.renderPipeline==previous,"Editor preview pipeline was not restored");
                window=EditorWindow.GetWindow<AnimationLabMotionWindow>();window.Show();Key(KeyCode.F1);Assert(window.Frame==30&&window.Draft.Exact(window.Draft.drafts,0)!=null,"Close/reopen lost frame draft");
                Key(KeyCode.F2);Assert(window.Status.StartsWith("원본 +")&&paths.Select(AssetDatabase.AssetPathToGUID).SequenceEqual(ids),"Resave changed GUID or failed");
                Assert(!AssetDatabase.LoadAllAssetsAtPath(paths[2]).Any(o=>o is Mesh),"FBX duplicated a mesh");
                string author=source.author;source.author+=" external edit";EditorUtility.SetDirty(source);AssetDatabase.SaveAssetIfDirty(source);Key(KeyCode.F2);Assert(window.Status.Contains("외부에서 변경"),"Stale source overwrite was not blocked");source.author=author;EditorUtility.SetDirty(source);AssetDatabase.SaveAssetIfDirty(source);
                Debug.Log("AUTHORING_CONTROLS_OK F1 F3 arrows boundaries draftIsolation undoRedo ghostNoCollision ghostNoShadow F2 reopen GUIDs staleEditGuard meshesExcluded");
                window.LoadSource(AssetDatabase.LoadAssetAtPath<Motion>(AnimationLabAuthoringField.Example));window.Seek(0);
                imported=AnimationLabMotionAuthoring.ImportedClip(AssetDatabase.GetAssetPath(window.Draft.generatedFbx));importedRig=AnimationLabMotionAuthoring.CreateRig();AnimationLabMotionAuthoring.StripAnimators(importedRig);
                target=new RenderTexture(640,640,24);pixels=new Texture2D(1280,640,TextureFormat.RGB24,false);Directory.CreateDirectory(Path.Combine(Evidence,"authoring-preview"));
                Capture(0); // Warm the renderer before starting the real editor preview clock.
                times.Clear();shot=0;Key(KeyCode.Space);Assert(window.Previewing,"Space did not start preview");nextShot=EditorApplication.timeSinceStartup;EditorApplication.update+=PreviewTick;
            }
            catch(Exception e){Debug.LogException(e);Cleanup();EditorApplication.Exit(2);}
        }
        [Serializable] sealed class PreviewTimes{public float[] seconds;public string scope="Real editor preview clock; left source+onion skin, right reimported Generic FBX; no user visual approval";}
        static void PreviewTick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup<nextShot)return;nextShot=EditorApplication.timeSinceStartup+1.0/30;
                float at=window.Previewing?window.PreviewFrame/window.Draft.sampleRate:window.Draft.Duration;Capture(at);
                if(window.Previewing)return;
                Assert(times.Count>=20,"Not enough real preview frames captured");File.WriteAllText(Path.Combine(Evidence,"authoring-preview.json"),JsonUtility.ToJson(new PreviewTimes{seconds=times.ToArray()}));Debug.Log("AUTHORING_PREVIEW_OK actualEditorClock=true frames="+times.Count+" duration="+at+" sourceAndFbx=true");Cleanup();EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);Cleanup();EditorApplication.Exit(2);}
        }
        static void Capture(float seconds)
        {
            var data=window.Draft;var camera=Camera.main;var saved=RenderTexture.active;camera.targetTexture=target;camera.aspect=1;var focus=new Vector3(0,.88f,0);camera.transform.position=focus+new Vector3(2.4f,.25f,-3.6f);camera.transform.LookAt(focus);camera.orthographic=true;camera.orthographicSize=1;
            bool showGhost=window.Ghost.activeSelf;
            for(int side=0;side<2;side++)
            {
                window.Rig.SetActive(side==0);window.Ghost.SetActive(side==0&&showGhost);importedRig.SetActive(side==1);
                if(side==1){var bones=AnimationLabMotionAuthoring.Bind(data,importedRig);AnimationLabMotionAuthoring.Apply(data,bones,new Motion.Pose{root=data.bindPositions[0],hips=data.bindPositions[data.hipsIndex],rotations=data.bindRotations});imported.SampleAnimation(importedRig,seconds);}
                camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,640,640),side*640,0,false);
            }
            pixels.Apply();RenderTexture.active=saved;camera.targetTexture=null;window.Rig.SetActive(true);window.Ghost.SetActive(showGhost);importedRig.SetActive(false);
            File.WriteAllBytes(Path.Combine(Evidence,"authoring-preview",shot++.ToString("D4")+".png"),pixels.EncodeToPNG());times.Add(seconds);
        }
        static void Cleanup(){EditorApplication.update-=PreviewTick;if(window!=null)window.Close();if(importedRig!=null)UnityEngine.Object.DestroyImmediate(importedRig);if(target!=null)UnityEngine.Object.DestroyImmediate(target);if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);}
        public static void RoundTrip()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Isolated QA only");
            GameObject rig=null;Motion data=null;
            try
            {
                rig=AnimationLabMotionAuthoring.CreateRig();data=AnimationLabMotionAuthoring.CreateSource(rig);data.clipName="RoundTripProbe";
                var pose=data.keys[0].Copy(30);pose.hips+=new Vector3(.035f,-.015f,0);
                int bone=Array.IndexOf(data.paths,data.humanPaths[(int)HumanBodyBones.LeftShoulder]);if(bone<0)throw new Exception("No mapped shoulder in common rig");
                pose.rotations[bone]*=Quaternion.Euler(0,0,6);data.StoreKey(pose);data.StoreKey(data.keys[0].Copy(60));
                var clip=AnimationLabMotionAuthoring.Bake(data);float unity=AnimationLabMotionAuthoring.Compare(data,clip,out float unityPosition);
                if(unity>.1f||unityPosition>.0001f)throw new Exception("Unity bake sample mismatch");
                data.author="Checks";Motion saved=AssetDatabase.LoadAssetAtPath<Motion>(AnimationLabMotionAuthoring.Authored+"/Checks_RoundTripProbe.asset");string revision=saved!=null?AnimationLabMotionAuthoring.Revision(saved):"";if(saved!=null){data.id=saved.id;data.generatedClip=saved.generatedClip;data.generatedFbx=saved.generatedFbx;}
                string path=AnimationLabMotionAuthoring.SaveBundle(data,ref saved,revision);
                var imported=AnimationLabMotionAuthoring.ImportedClip(AnimationLabMotionAuthoring.Exports+"/RoundTripProbe.fbx");
                float angle=AnimationLabMotionAuthoring.Compare(data,imported,out float position);
                Debug.Log($"AUTHORING_ROUNDTRIP_OK source={path} bones={data.paths.Length} fps={data.sampleRate} duration={imported.length:F4} unityAngle={unity:F5} unityPosition={unityPosition:F6} fbxAngle={angle:F5} fbxPosition={position:F6} root={rig.name}");
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
            finally{if(rig!=null)UnityEngine.Object.DestroyImmediate(rig);if(data!=null)UnityEngine.Object.DestroyImmediate(data);}
        }
    }
}
#endif

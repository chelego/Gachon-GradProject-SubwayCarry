#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Offline preparation only. Never runs from InitializeOnLoad or the user's editor.
    public static class AnimationLabFreeWalkBuild
    {
        public const string Local = AnimationLabBuilder.Root + "/FreeWalk";
        const string Model = "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/AnimationLibrary_Unity_Standard.fbx";
        const string Cache = "Assets/_Project/Scenes/3D/Ingame/Game/MotionMatching/Source~/Kubold/Package";
        static readonly string[] Used = { "Idle", "WalkFwdLoop", "WalkFwdStart", "WalkFwdStop_LU", "WalkFwdStop_RU" };

        static void Guard()
        {
            if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))
                throw new InvalidOperationException("Free Walk preparation is restricted to the existing isolated batch project.");
        }
        public static void ProbeBatch()
        {
            Guard();
            try { Prepare(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(2); }
        }
        public static void RepairImportBatch()
        {
            Guard();
            try
            {
                string path=Local+"/Motion.fbx";
                byte[] originalMeta=File.ReadAllBytes(path+".meta");
                Dictionary<string,Vector3[][]> raw;
                try
                {
                    var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                    importer.animationType=ModelImporterAnimationType.Generic;
                    importer.animationCompression=ModelImporterAnimationCompression.Off;
                    importer.SaveAndReimport();
                    raw=CaptureSource(false);
                    var rawRig=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    try
                    {
                        var animator=rawRig.GetComponent<Animator>();if(animator!=null)UnityEngine.Object.DestroyImmediate(animator);
                        rawRig.name="Original motion skeleton A0";
                        PrefabUtility.SaveAsPrefabAsset(rawRig,Local+"/SourceSkeleton.prefab");
                    }
                    finally{UnityEngine.Object.DestroyImmediate(rawRig);}
                    foreach(string name in new[]{"Idle","WalkFwdLoop"})
                    {
                        var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>c.name==name);
                        string asset=Local+(name=="Idle"?"/SourceIdle.anim":"/SourceWalk.anim");
                        var copy=UnityEngine.Object.Instantiate(clip);var saved=AssetDatabase.LoadAssetAtPath<AnimationClip>(asset);
                        if(saved==null)AssetDatabase.CreateAsset(copy,asset);else{EditorUtility.CopySerialized(copy,saved);EditorUtility.SetDirty(saved);UnityEngine.Object.DestroyImmediate(copy);}
                        var skeleton=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Local+"/SourceSkeleton.prefab"));
                        try{var sampled=Capture(skeleton,clip,false,SourceParents,SourceEnds,"LeftUpLeg","RightUpLeg");CompareSegments(raw[name],sampled,"A0_vs_preserved_source_skeleton "+name);ValidateLimbs(raw[name],sampled,name+" preserved source skeleton");}
                        finally{UnityEngine.Object.DestroyImmediate(skeleton);}
                    }
                    AssetDatabase.SaveAssets();
                }
                finally
                {
                    // Same GUID and same QA path: no active duplicate import of the source file.
                    File.WriteAllBytes(path+".meta",originalMeta);
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                }
                var before=CaptureSource(true);
                CompareSource(raw,before,"A0_vs_rejected_A1");
                Prepare();
                var after=CaptureSource(true);
                CompareSource(raw,after,"A0_vs_repaired_A1");
                var importerAfter=(ModelImporter)AssetImporter.GetAtPath(path);
                if(!importerAfter.humanDescription.human.Any(b=>b.humanName=="Chest"&&b.boneName=="Spine1"))
                    throw new InvalidDataException("Source animated Spine1 is not mapped to Chest");
                CompareTarget(raw);
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
        }
        // Semantic segments, not absolute world coordinates or bone lengths.
        static readonly string[] SourceParents={"Hips","Spine","Spine1","Neck","LeftShoulder","LeftArm","LeftForeArm","LeftHand","RightShoulder","RightArm","RightForeArm","RightHand","LeftUpLeg","LeftLeg","LeftFoot","RightUpLeg","RightLeg","RightFoot"};
        static readonly string[] SourceEnds={"Spine","Spine1","Neck","Head","LeftArm","LeftForeArm","LeftHand","LeftHandMiddle1","RightArm","RightForeArm","RightHand","RightHandMiddle1","LeftLeg","LeftFoot","LeftToeBase","RightLeg","RightFoot","RightToeBase"};
        static readonly string[] TargetParents={"DEF-hips","DEF-spine.001","DEF-spine.003","DEF-neck","DEF-shoulder.L","DEF-upper_arm.L","DEF-forearm.L","DEF-hand.L","DEF-shoulder.R","DEF-upper_arm.R","DEF-forearm.R","DEF-hand.R","DEF-thigh.L","DEF-shin.L","DEF-foot.L","DEF-thigh.R","DEF-shin.R","DEF-foot.R"};
        static readonly string[] TargetEnds={"DEF-spine.001","DEF-spine.003","DEF-neck","DEF-head","DEF-upper_arm.L","DEF-forearm.L","DEF-hand.L","DEF-f_middle.01.L","DEF-upper_arm.R","DEF-forearm.R","DEF-hand.R","DEF-f_middle.01.R","DEF-shin.L","DEF-foot.L","DEF-toe.L","DEF-shin.R","DEF-foot.R","DEF-toe.R"};
        static Dictionary<string,Vector3[][]> CaptureSource(bool human)
        {
            var captured=new Dictionary<string,Vector3[][]>();
            foreach(string name in Used)
            {
                var clip=AssetDatabase.LoadAllAssetsAtPath(Local+"/Motion.fbx").OfType<AnimationClip>().First(c=>c.name==name);
                var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Local+"/Motion.fbx"));
                try{captured.Add(name,Capture(root,clip,human,SourceParents,SourceEnds,"LeftUpLeg","RightUpLeg"));}
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            return captured;
        }
        static Vector3[][] Capture(GameObject root,AnimationClip clip,bool human,string[] parents,string[] ends,string leftHip,string rightHip)
        {
            var bones=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name);
            var graph=default(PlayableGraph);var player=default(AnimationClipPlayable);
            if(human)
            {
                var animator=root.GetComponentInChildren<Animator>();animator.runtimeAnimatorController=null;
                animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
                graph=PlayableGraph.Create("Raw versus Humanoid same-time diagnostic");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                player=AnimationClipPlayable.Create(graph,clip);player.SetApplyFootIK(false);player.SetApplyPlayableIK(false);
                AnimationPlayableOutput.Create(graph,"Uncorrected source pose",animator).SetSourcePlayable(player);graph.Play();
            }
            var samples=new Vector3[61][];
            try
            {
                for(int i=0;i<samples.Length;i++)
                {
                    float time=clip.length*i/(samples.Length-1);
                    if(human){player.SetTime(time);graph.Evaluate(0);}else clip.SampleAnimation(root,time);
                    Vector3 side=Vector3.ProjectOnPlane(bones[rightHip].position-bones[leftHip].position,Vector3.up).normalized;
                    var frame=Quaternion.Inverse(Quaternion.LookRotation(Vector3.Cross(side,Vector3.up),Vector3.up));
                    samples[i]=new Vector3[parents.Length];
                    for(int b=0;b<parents.Length;b++)samples[i][b]=frame*(bones[ends[b]].position-bones[parents[b]].position).normalized;
                }
            }
            finally{if(graph.IsValid())graph.Destroy();}
            return samples;
        }
        static void CompareSource(Dictionary<string,Vector3[][]> raw,Dictionary<string,Vector3[][]> human,string label)
        {
            foreach(string clip in Used)CompareSegments(raw[clip],human[clip],label+" "+clip);
        }
        static void CompareSegments(Vector3[][] raw,Vector3[][] candidate,string label)
        {
            var means=new float[SourceParents.Length];var peaks=new float[means.Length];
            for(int i=0;i<raw.Length;i++)for(int b=0;b<means.Length;b++)
            {float error=Vector3.Angle(raw[i][b],candidate[i][b]);means[b]+=error/raw.Length;peaks[b]=Mathf.Max(peaks[b],error);}
            Debug.Log("FREE_WALK_IMPORT_COMPARE "+label+" mean="+string.Join(",",means.Select(v=>v.ToString("F2")))+" peak="+string.Join(",",peaks.Select(v=>v.ToString("F2"))));
        }
        static void CompareTarget(Dictionary<string,Vector3[][]> raw)
        {
            foreach(string name in Used)
            {
                var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
                try
                {
                    root.GetComponent<Animator>().avatar=AssetDatabase.LoadAssetAtPath<Avatar>(Local+"/TargetAvatar.asset");
                    var clip=AssetDatabase.LoadAllAssetsAtPath(Local+"/Motion.fbx").OfType<AnimationClip>().First(c=>c.name==name);
                    var captured=Capture(root,clip,true,TargetParents,TargetEnds,"DEF-thigh.L","DEF-thigh.R");
                    CompareSegments(raw[name],captured,"A0_vs_target_A2 "+name);
                    if(name=="Idle"||name=="WalkFwdLoop")ValidateLimbs(raw[name],captured,name+" model");
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
                var prefab=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AnimationLabBuilder.Root+"/Prefabs/Character.prefab"));
                try
                {
                    prefab.GetComponentInChildren<Animator>().avatar=AssetDatabase.LoadAssetAtPath<Avatar>(Local+"/TargetAvatar.asset");
                    var clip=AssetDatabase.LoadAllAssetsAtPath(Local+"/Motion.fbx").OfType<AnimationClip>().First(c=>c.name==name);
                    var captured=Capture(prefab,clip,true,TargetParents,TargetEnds,"DEF-thigh.L","DEF-thigh.R");
                    CompareSegments(raw[name],captured,"A0_vs_actual_prefab_A2 "+name);
                    if(name=="Idle"||name=="WalkFwdLoop")ValidateLimbs(raw[name],captured,name+" actual prefab");
                }
                finally{UnityEngine.Object.DestroyImmediate(prefab);}
            }
        }
        static void ValidateLimbs(Vector3[][] source,Vector3[][] target,string label)
        {
            float maxBendDifference=0,maxLimbDirection=0;int unsupportedRaisedArms=0;
            for(int i=0;i<source.Length;i++)
            {
                foreach(int arm in new[]{5,9})if(source[i][arm].y<-.5f&&target[i][arm].y>0)unsupportedRaisedArms++;
                foreach(int limb in new[]{5,6,9,10,12,13,15,16})maxLimbDirection=Mathf.Max(maxLimbDirection,Vector3.Angle(source[i][limb],target[i][limb]));
                foreach(int bend in new[]{5,9,12,15})maxBendDifference=Mathf.Max(maxBendDifference,Mathf.Abs(Vector3.Angle(source[i][bend],source[i][bend+1])-Vector3.Angle(target[i][bend],target[i][bend+1])));
            }
            Debug.Log("FREE_WALK_LIMB_GATE "+label+" unsupportedRaisedArms="+unsupportedRaisedArms+" directionPeak="+maxLimbDirection+" bendDifferencePeak="+maxBendDifference);
            // Source-relative safety gate only; not clinical ROM or final skin/visual approval.
            if(unsupportedRaisedArms!=0||maxLimbDirection>20||maxBendDifference>20)throw new InvalidDataException("Source-relative limb pose gate failed: "+label);
        }
        static void PreserveSourceChest(ModelImporter importer)
        {
            var description=importer.humanDescription;
            if(!description.skeleton.Any(b=>b.name=="Spine1"))throw new InvalidDataException("Expected source chest bone missing");
            var human=description.human.Where(b=>b.humanName!="Chest").ToList();
            human.Add(new HumanBone{boneName="Spine1",humanName="Chest",limit=new HumanLimit{useDefaultValues=true}});
            description.human=human.ToArray();importer.humanDescription=description;
        }
        static void CopyAbsent(string source, string target)
        {
            if (!File.Exists(source)) throw new FileNotFoundException("Existing official free sample missing", source);
            if (!File.Exists(target)) File.Copy(source, target);
        }
        public static void Prepare()
        {
            Guard();
            string project = Application.dataPath.Replace('\\', '/').Split(new[] { "/Logs/CrowdBehavior/Walking/Unity/Assets" }, StringSplitOptions.None)[0];
            string cache = project + "/" + Cache;
            Directory.CreateDirectory(Local);
            CopyAbsent(cache + "/f1a1175069a76bd47a4b87723930b382/asset", Local + "/Motion.fbx");
            CopyAbsent(cache + "/9624609cbb240b3429e65cd837453fa9/asset", Local + "/SourceModel.fbx");
            CopyAbsent(cache + "/83f9a6d6e2f35124e81f45e1dab13007/asset", Local + "/License.txt");
            CopyAbsent(cache + "/7da6a6487edfc584a97b43932710bb54/asset", Local + "/Source.txt");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string file in new[] { "/Motion.fbx", "/SourceModel.fbx" })
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(Local + file);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.optimizeGameObjects = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                PreserveSourceChest(importer);
                var clips = importer.defaultClipAnimations;
                if(file=="/Motion.fbx")clips=clips.Where(c=>Used.Contains(c.name)).ToArray();
                foreach (var clip in clips)
                {
                    clip.loopTime = clip.name == "Idle" || clip.name == "WalkFwdLoop";
                    clip.loopPose = false;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionXZ = true;
                    clip.keepOriginalPositionY = true;
                    clip.lockRootRotation = false;
                    clip.lockRootHeightY = false;
                    clip.lockRootPositionXZ = false;
                }
                if (clips.Length > 0) importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
            foreach (string file in new[] { "/Motion.fbx", "/SourceModel.fbx" })
            {
                var avatar = AssetDatabase.LoadAllAssetsAtPath(Local + file).OfType<Avatar>().FirstOrDefault();
                Debug.Log("FREE_WALK_SOURCE " + file + " avatar=" + (avatar != null && avatar.isValid && avatar.isHuman));
            }
            var sourceClips = AssetDatabase.LoadAllAssetsAtPath(Local + "/Motion.fbx").OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            foreach (var clip in sourceClips) Debug.Log("FREE_WALK_CLIP " + clip.name + " duration=" + clip.length + " human=" + clip.humanMotion + " speed=" + clip.averageSpeed);
            foreach (var name in Used) if (!sourceClips.Any(c => c.name == name)) throw new InvalidDataException("Missing free sample motion: " + name);
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            try
            {
                var avatar = TargetAvatar(root);
                Debug.Log("FREE_WALK_TARGET avatar=" + avatar.isValid + " human=" + avatar.isHuman);
                if (!avatar.isValid || !avatar.isHuman) throw new InvalidDataException("Target Humanoid mapping failed; shared importer was not changed.");
                string path = Local + "/TargetAvatar.asset";
                var savedAvatar=AssetDatabase.LoadAssetAtPath<Avatar>(path);
                if(savedAvatar==null)AssetDatabase.CreateAsset(avatar,path);
                else
                {
                    // Update the actual serialized Avatar in place; keep its GUID and every consumer reference.
                    EditorUtility.CopySerialized(avatar,savedAvatar);EditorUtility.SetDirty(savedAvatar);
                    UnityEngine.Object.DestroyImmediate(avatar);AssetDatabase.SaveAssets();
                }
                root.GetComponent<Animator>().avatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);
                Inspect(root, sourceClips.First(c => c.name == "Idle"), "target-idle");
                Inspect(root, sourceClips.First(c => c.name == "WalkFwdLoop"), "target-walk");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            var original = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Local + "/SourceModel.fbx"));
            try { Inspect(original, sourceClips.First(c => c.name == "Idle"), "source-idle"); Inspect(original, sourceClips.First(c => c.name == "WalkFwdLoop"), "source-walk"); }
            finally { UnityEngine.Object.DestroyImmediate(original); }
            Directory.CreateDirectory(Local + "/Resources");
            string dataPath=Local+"/Resources/FreeWalkReview.asset";
            var data=AssetDatabase.LoadAssetAtPath<AnimationLabFreeWalkData>(dataPath);
            bool create=data==null;if(create)data=ScriptableObject.CreateInstance<AnimationLabFreeWalkData>();
            data.avatar=AssetDatabase.LoadAssetAtPath<Avatar>(Local+"/TargetAvatar.asset");
            data.poseProfile=AssetDatabase.LoadAssetAtPath<AnimationLabPoseProfile>(AnimationLabBuilder.Root+"/Data/WalkPoseProfile.asset");
            data.sourceSkeleton=AssetDatabase.LoadAssetAtPath<GameObject>(Local+"/SourceSkeleton.prefab");
            data.sourceIdle=AssetDatabase.LoadAssetAtPath<AnimationClip>(Local+"/SourceIdle.anim");data.sourceWalk=AssetDatabase.LoadAssetAtPath<AnimationClip>(Local+"/SourceWalk.anim");
            if(data.sourceIdle!=null)SourceRootDrift(data.sourceSkeleton,data.sourceIdle,out data.sourceIdleOrigin,out data.sourceIdleVelocity);
            if(data.sourceWalk!=null)SourceRootDrift(data.sourceSkeleton,data.sourceWalk,out data.sourceWalkOrigin,out data.sourceWalkVelocity);
            data.idle=sourceClips.First(c=>c.name=="Idle");data.walk=sourceClips.First(c=>c.name=="WalkFwdLoop");
            data.start=sourceClips.First(c=>c.name=="WalkFwdStart");data.stopLeft=sourceClips.First(c=>c.name=="WalkFwdStop_LU");data.stopRight=sourceClips.First(c=>c.name=="WalkFwdStop_RU");
            var target=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AnimationLabBuilder.Root+"/Prefabs/Character.prefab"));
            try
            {
                var a=target.GetComponentInChildren<Animator>();
                a.runtimeAnimatorController=null;a.avatar=data.avatar;a.applyRootMotion=false;
                a.cullingMode=AnimatorCullingMode.AlwaysAnimate;a.Rebind();
                Feet(a,data.stopLeft,0,out data.stopLeftL,out data.stopLeftR);
                Feet(a,data.stopRight,0,out data.stopRightL,out data.stopRightR);
                const int samples=121;var left=new Vector3[samples];var right=new Vector3[samples];
                SampleFeet(a,data.walk,left,right);
                var speeds=new List<float>();float lowL=left.Min(v=>v.y),lowR=right.Min(v=>v.y);float dt=data.walk.length/(samples-1);
                Vector3 side=a.GetBoneTransform(HumanBodyBones.RightUpperLeg).position-a.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
                Vector3 up=a.GetBoneTransform(HumanBodyBones.Neck).position-a.GetBoneTransform(HumanBodyBones.Hips).position;
                Vector3 forward=a.transform.InverseTransformDirection(Vector3.ProjectOnPlane(Vector3.Cross(side,up),Vector3.up).normalized);
                // Magnitude is axis independent; heading agreement is a separate runtime check.
                for(int i=1;i<samples-1;i++)foreach(bool isLeft in new[]{true,false}){var positions=isLeft?left:right;float low=isLeft?lowL:lowR;float speed=Vector3.ProjectOnPlane((positions[i+1]-positions[i-1])/(2*dt),Vector3.up).magnitude;if(positions[i].y<low+.045f&&speed>.2f)speeds.Add(speed);}
                Debug.Log("FREE_WALK_FOOT_RANGE L="+left.Min(v=>v.z)+".."+left.Max(v=>v.z)+" R="+right.Min(v=>v.z)+".."+right.Max(v=>v.z)+" forward="+forward+" count="+speeds.Count);
                if(speeds.Count<5)throw new InvalidDataException("No stance interval in free walking clip");
                speeds.Sort();data.speed=speeds[speeds.Count/2]*Mathf.Abs(a.transform.lossyScale.x);
                Debug.Log("FREE_WALK_REFERENCE speed="+data.speed+" samples="+speeds.Count+" humanScale="+a.humanScale+" forward="+forward+" modelScale="+a.transform.lossyScale);
                Inspect(a.gameObject,data.idle,"prefab-idle");Inspect(a.gameObject,data.walk,"prefab-walk");
            }
            finally {UnityEngine.Object.DestroyImmediate(target);}
            if(!data.Valid)throw new InvalidDataException("Free Walk data failed validation");
            if(create)AssetDatabase.CreateAsset(data,dataPath);else EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }
        static void SourceRootDrift(GameObject model,AnimationClip clip,out Vector3 origin,out Vector3 velocity)
        {
            var root=UnityEngine.Object.Instantiate(model);
            try
            {
                var hips=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Hips");
                clip.SampleAnimation(root,0);origin=root.transform.InverseTransformPoint(hips.position);origin.y=0;
                clip.SampleAnimation(root,clip.length);var end=root.transform.InverseTransformPoint(hips.position);end.y=0;
                velocity=(end-origin)/clip.length;
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static Avatar TargetAvatar(GameObject root)
        {
            // Let Unity's model import pipeline validate/derive the reference T-pose.
            // BuildHumanAvatar from the untouched Generic reset pose skips that step.
            // This is the existing QA asset path, restored byte-for-byte in finally.
            byte[] saved=File.ReadAllBytes(Model+".meta");
            try
            {
                var description=TargetDescription(root);description.skeleton=Array.Empty<SkeletonBone>();
                var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
                importer.animationType=ModelImporterAnimationType.Human;
                importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation=false;importer.optimizeGameObjects=false;
                importer.humanDescription=description;importer.SaveAndReimport();
                var avatar=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().FirstOrDefault();
                if(avatar==null||!avatar.isValid||!avatar.isHuman)throw new InvalidDataException("Target model import could not create a valid Humanoid reference pose");
                Debug.Log("FREE_WALK_TARGET_IMPORT skeleton="+importer.humanDescription.skeleton.Length+" mapped="+importer.humanDescription.human.Length);
                return UnityEngine.Object.Instantiate(avatar);
            }
            finally
            {
                File.WriteAllBytes(Model+".meta",saved);
                AssetDatabase.ImportAsset(Model,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            }
        }
        static HumanDescription TargetDescription(GameObject root)
        {
            var bones = root.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
            foreach(string bone in new[]{"DEF-hips","DEF-spine.001","DEF-spine.003","DEF-neck","DEF-head","DEF-shoulder.L","DEF-upper_arm.L","DEF-forearm.L","DEF-hand.L","DEF-thigh.L","DEF-shin.L","DEF-foot.L"})
            {var t=bones[bone];Debug.Log("FREE_WALK_TARGET_BIND "+bone+" parent="+t.parent.name+" local="+t.localPosition+" euler="+t.localEulerAngles+" world="+root.transform.InverseTransformPoint(t.position));}
            var map = new Dictionary<HumanBodyBones, string>
            {
                { HumanBodyBones.Hips, "DEF-hips" }, { HumanBodyBones.Spine, "DEF-spine.001" },
                { HumanBodyBones.Chest, "DEF-spine.002" }, { HumanBodyBones.UpperChest, "DEF-spine.003" },
                { HumanBodyBones.Neck, "DEF-neck" }, { HumanBodyBones.Head, "DEF-head" }
            };
            foreach (bool left in new[] { true, false })
            {
                string side = left ? "Left" : "Right", suffix = left ? ".L" : ".R";
                var names = new[] { "Shoulder", "UpperArm", "LowerArm", "Hand", "UpperLeg", "LowerLeg", "Foot", "Toes" };
                var rig = new[] { "shoulder", "upper_arm", "forearm", "hand", "thigh", "shin", "foot", "toe" };
                for (int i = 0; i < names.Length; i++) map[(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + names[i])] = "DEF-" + rig[i] + suffix;
                foreach (string finger in new[] { "Thumb", "Index", "Middle" })
                    for (int i = 0; i < 3; i++)
                    {
                        string bone = "DEF-" + (finger == "Thumb" ? "thumb" : "f_" + finger.ToLowerInvariant()) + ".0" + (i + 1) + suffix;
                        if (bones.ContainsKey(bone)) map[(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + finger + new[] { "Proximal", "Intermediate", "Distal" }[i])] = bone;
                    }
            }
            var description = new HumanDescription
            {
                human = map.Select(k => new HumanBone { humanName = HumanTrait.BoneName[(int)k.Key], boneName = k.Value, limit = new HumanLimit { useDefaultValues = true } }).ToArray(),
                skeleton = root.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray(),
                upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f,
                armStretch = 0, legStretch = 0, feetSpacing = 0, hasTranslationDoF = false
            };
            foreach (var pair in map) if (!bones.ContainsKey(pair.Value)) throw new InvalidDataException("Required target bone missing: " + pair.Value);
            return description;
        }
        static void Inspect(GameObject root, AnimationClip clip, string label)
        {
            var animator = root.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) throw new InvalidDataException("Invalid source/target Animator: " + label);
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var graph = PlayableGraph.Create("Free sample single clip check");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip); playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
                AnimationPlayableOutput.Create(graph, "Single raw Humanoid clip", animator).SetSourcePlayable(playable); graph.Play();
                float minTilt = 999, maxTilt = -999, maxNeck = 0;
                for (int i = 0; i < 61; i++)
                {
                    playable.SetTime(clip.length * i / 60f); graph.Evaluate(0);
                    Vector3 hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                    Vector3 neck = animator.GetBoneTransform(HumanBodyBones.Neck).position;
                    Vector3 head = animator.GetBoneTransform(HumanBodyBones.Head).position;
                    float tilt = Vector3.SignedAngle(Vector3.up, neck - hips, root.transform.right);
                    minTilt = Mathf.Min(minTilt, tilt); maxTilt = Mathf.Max(maxTilt, tilt);
                    maxNeck = Mathf.Max(maxNeck, Vector3.Angle(Vector3.up, head - neck));
                    if (!float.IsFinite(hips.sqrMagnitude + neck.sqrMagnitude + head.sqrMagnitude)) throw new InvalidDataException("Nonfinite Humanoid pose");
                }
                Debug.Log("FREE_WALK_POSE " + label + " torsoTilt=" + minTilt + ".." + maxTilt + " neckMax=" + maxNeck + " scale=" + root.transform.lossyScale + " skins=" + root.GetComponentsInChildren<SkinnedMeshRenderer>().Length);
            }
            finally { graph.Destroy(); }
        }
        static void Feet(Animator animator,AnimationClip clip,float time,out Vector3 left,out Vector3 right)
        {
            var graph=PlayableGraph.Create("Offline free gait measurement");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var player=AnimationClipPlayable.Create(graph,clip);
                player.SetApplyFootIK(false);player.SetApplyPlayableIK(false);
                AnimationPlayableOutput.Create(graph,"Raw Humanoid feet",animator).SetSourcePlayable(player);graph.Play();player.SetTime(time);graph.Evaluate(0);
                left=animator.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position);
                right=animator.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightFoot).position);
            }
            finally{graph.Destroy();}
        }
        static void SampleFeet(Animator animator,AnimationClip clip,Vector3[] left,Vector3[] right)
        {
            var graph=PlayableGraph.Create("Offline free gait cycle");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var player=AnimationClipPlayable.Create(graph,clip);
                player.SetApplyFootIK(false);player.SetApplyPlayableIK(false);
                AnimationPlayableOutput.Create(graph,"Uncorrected gait cycle",animator).SetSourcePlayable(player);graph.Play();
                for(int i=0;i<left.Length;i++){player.SetTime(clip.length*i/(left.Length-1));graph.Evaluate(0);left[i]=animator.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position);right[i]=animator.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightFoot).position);}
            }
            finally{graph.Destroy();}
        }
    }
}
#endif

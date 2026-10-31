#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;
using UnityEngine.Rendering;
using Motion=SubwayCarry.Gameplay3D.AnimationLab.AnimationLabAuthoredMotion;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    public static class AnimationLabMotionAuthoring
    {
        public const string Root="Assets/_Project/Scenes/3D/Test/Animation";
        public const string ScenePath=Root+"/MotionAuthoring.unity";
        public const string RigPath=Root+"/Prefabs/MotionRig.prefab";
        public const string Authored=Root+"/Authored",Exports=Root+"/Exports";
        public const string ExamplePath=Authored+"/WeightShift_Example.asset";
        public const string SharedMotion="Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/AnimationLibrary_Unity_Standard.fbx";
        public static void ResolveSharedRig(Motion data)
        {
            var shared=AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);if(shared==null)throw new InvalidOperationException("공유 MotionRig.prefab과 .meta를 함께 받아야 합니다.");if(data.sourceRig==shared)return;
            if(data.sourceRig!=null&&AssetDatabase.GetAssetPath(data.sourceRig)!=Root+"/Prefabs/Character.prefab")throw new InvalidOperationException("다른 골격 원본은 자동 교체하지 않습니다.");
            var probe=CreateRig(shared);try{Bind(data,probe);}finally{UnityEngine.Object.DestroyImmediate(probe);}data.sourceRig=shared;
        }
        public static GameObject CreateRig(GameObject source=null)
        {
            var rig=UnityEngine.Object.Instantiate(source!=null?source:AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
            rig.name="MotionRig";rig.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            foreach(var c in rig.GetComponentsInChildren<Component>(true))
                if(c!=null&&!(c is Transform)&&!(c is Renderer)&&!(c is MeshFilter)&&!(c is Animator))UnityEngine.Object.DestroyImmediate(c);
            foreach(var t in rig.GetComponentsInChildren<Transform>(true)){t.gameObject.layer=2;t.gameObject.hideFlags=HideFlags.HideAndDontSave;}
            return rig;
        }
        public static Transform[] Bones(GameObject rig)=>rig.GetComponentsInChildren<Transform>(true);
        public static string[] Paths(GameObject rig)=>Bones(rig).Select(t=>AnimationUtility.CalculateTransformPath(t,rig.transform)).ToArray();
        public static string Signature(GameObject rig)
        {
            var text=new StringBuilder(Motion.RigVersion);
            foreach(var t in Bones(rig)){text.Append('|').Append(AnimationUtility.CalculateTransformPath(t,rig.transform));text.Append(':').Append(t.localScale.ToString("F5"));}
            using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","");
        }
        public static Motion CreateSource(GameObject rig,AnimationClip starting=null,float time=0)
        {
            var data=ScriptableObject.CreateInstance<Motion>();data.id=Guid.NewGuid().ToString("N");data.sourceRig=AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            data.hideFlags=HideFlags.HideAndDontSave;data.author="";
            var animator=rig.GetComponentInChildren<Animator>();
            var selected=starting!=null?starting:AssetDatabase.LoadAllAssetsAtPath(SharedMotion).OfType<AnimationClip>().FirstOrDefault(c=>c.name.EndsWith("|Idle_Loop",StringComparison.Ordinal)||c.name=="Idle_Loop");
            if(selected==null)throw new InvalidOperationException("공유 CC0 Idle_Loop이 없습니다. Quaternius FBX와 .meta의 Git LFS 수신을 확인하세요.");
            RequireSharedClip(selected);data.startingClip=selected;
            // Labels for the existing versioned Generic rig; no Avatar or local mocap dependency.
            data.humanPaths=CommonRigPaths(rig);
            Transform hips=rig.transform.Find(data.humanPaths[(int)HumanBodyBones.Hips]);
            if(selected.isHumanMotion)throw new InvalidOperationException("제작기는 현재 공통 Generic 골격용입니다. Humanoid 리타기팅은 별도 검수가 필요합니다.");
            bool authored=AssetDatabase.GetAssetPath(selected).StartsWith(Exports+"/",StringComparison.Ordinal);
            selected.SampleAnimation(authored?rig:animator.gameObject,Mathf.Clamp(time,0,selected.length));
            if(hips==null)hips=Bones(rig).First(t=>t.name=="Hips"||t.name.EndsWith(":Hips"));
            if(starting==null)
            {
                // Place the new standing authoring rig on its stage once. No per-state height fix.
                float lowest=float.PositiveInfinity;var mesh=new Mesh();
                foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.BakeMesh(mesh);foreach(var v in mesh.vertices)lowest=Mathf.Min(lowest,skin.transform.TransformPoint(v).y);}
                UnityEngine.Object.DestroyImmediate(mesh);if(float.IsFinite(lowest))rig.transform.position-=Vector3.up*lowest;
            }
            var all=Bones(rig);data.hipsIndex=Array.IndexOf(all,hips);data.paths=Paths(rig);data.bindPositions=all.Select(t=>t.localPosition).ToArray();data.bindRotations=all.Select(t=>t.localRotation.normalized).ToArray();data.bindScales=all.Select(t=>t.localScale).ToArray();data.rigSignature=Signature(rig);
            foreach(var a in rig.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(a);
            data.StoreKey(Capture(data,rig,0));return data;
        }
        public static void StripAnimators(GameObject rig){foreach(var a in rig.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(a);}
        public static Motion CreateFullClipCopy(GameObject rig,AnimationClip clip)
        {
            RequireSharedClip(clip);if(clip.isHumanMotion)throw new InvalidOperationException("Humanoid 원본은 현재 직접 편집하지 않습니다. 권리/리그가 확인된 Generic 타깃 베이크가 필요합니다.");
            var animator=rig.GetComponentInChildren<Animator>();var animationRoot=animator!=null?animator.gameObject:rig;
            bool owned=AssetDatabase.GetAssetPath(clip).StartsWith(Exports+"/",StringComparison.Ordinal);var data=CreateSource(rig,clip,0);
            data.clipName=clip.name.Replace("Rig|","")+"_편집";data.loop=clip.isLooping||clip.wrapMode==WrapMode.Loop;data.lastFrame=Mathf.Max(1,Mathf.CeilToInt(clip.length*data.sampleRate));data.keys.Clear();data.drafts.Clear();var bones=Bind(data,rig);
            var reference=new Motion.Pose{root=data.bindPositions[0],hips=data.bindPositions[data.hipsIndex],rotations=data.bindRotations};
            for(int f=0;f<=data.lastFrame;f++){Apply(data,bones,reference);clip.SampleAnimation(owned?rig:animationRoot,Mathf.Min(clip.length,f/(float)data.sampleRate));data.StoreKey(Capture(data,rig,f));}return data;
        }
        static string[] CommonRigPaths(GameObject rig)
        {
            var map=new Dictionary<HumanBodyBones,string>{{HumanBodyBones.Hips,"DEF-hips"},{HumanBodyBones.Spine,"DEF-spine.001"},{HumanBodyBones.Chest,"DEF-spine.002"},{HumanBodyBones.UpperChest,"DEF-spine.003"},{HumanBodyBones.Neck,"DEF-neck"},{HumanBodyBones.Head,"DEF-head"}};
            foreach(bool left in new[]{true,false})
            {
                string side=left?"Left":"Right",suffix=left?".L":".R";
                var labels=new[]{"Shoulder","UpperArm","LowerArm","Hand","UpperLeg","LowerLeg","Foot","Toes"};var names=new[]{"shoulder","upper_arm","forearm","hand","thigh","shin","foot","toe"};
                for(int i=0;i<labels.Length;i++)map[(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+labels[i])]="DEF-"+names[i]+suffix;
                foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Little"})for(int part=0;part<3;part++)map[(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+finger+new[]{"Proximal","Intermediate","Distal"}[part])]="DEF-"+(finger=="Thumb"?"thumb":"f_"+(finger=="Little"?"pinky":finger.ToLowerInvariant()))+".0"+(part+1)+suffix;
            }
            var bones=Bones(rig);return Enumerable.Range(0,(int)HumanBodyBones.LastBone).Select(i=>{var bone=map.TryGetValue((HumanBodyBones)i,out string name)?bones.FirstOrDefault(t=>t.name==name):null;return bone!=null?AnimationUtility.CalculateTransformPath(bone,rig.transform):"";}).ToArray();
        }
        static void RequireSharedClip(AnimationClip clip)
        {
            if(clip==null)return;string path=AssetDatabase.GetAssetPath(clip);
            if(path==SharedMotion)return;
            if(path.StartsWith(Exports+"/",StringComparison.Ordinal)&&AssetImporter.GetAtPath(path).userData.StartsWith("SubwayCarry.MotionAuthoring:",StringComparison.Ordinal))return;
            throw new InvalidOperationException("팀 공유 제작 원본은 확인된 Quaternius CC0 또는 이 도구에서 직접 제작한 출력만 시작 클립으로 사용합니다. 로컬 전용 샘플은 저장하지 않습니다.");
        }
        public static Transform[] Bind(Motion data,GameObject rig)
        {
            var all=data.paths.Select(path=>path.Length==0?rig.transform:rig.transform.Find(path)).ToArray();
            if(data.rigVersion!=Motion.RigVersion||all.Any(t=>t==null)||Signature(rig)!=data.rigSignature)throw new InvalidOperationException("제작 골격/본 경로/스케일 불일치. 원본 rig를 바꾸지 마세요.");
            for(int i=1;i<all.Length;i++)if(i!=data.hipsIndex&&Vector3.Distance(all[i].localPosition,data.bindPositions[i])>.0001f)throw new InvalidOperationException("공통 rig의 본 길이/기준 위치가 변경됐습니다.");return all;
        }
        public static Motion.Pose Capture(Motion data,GameObject rig,int frame)
        {
            var bones=Bind(data,rig);return new Motion.Pose{frame=frame,root=bones[0].localPosition,hips=bones[data.hipsIndex].localPosition,rotations=bones.Select(t=>t.localRotation.normalized).ToArray()};
        }
        public static void Apply(Motion data,Transform[] bones,Motion.Pose pose)
        {
            for(int i=0;i<bones.Length;i++){bones[i].localPosition=i==0?pose.root:i==data.hipsIndex?pose.hips:data.bindPositions[i];bones[i].localRotation=pose.rotations[i];bones[i].localScale=data.bindScales[i];}
        }
        public static AnimationClip Bake(Motion data)
        {
            if(!data.Validate(out var reason)||data.keys.Count==0)throw new InvalidOperationException(reason.Length>0?reason:"저장한 키가 없습니다.");
            var clip=new AnimationClip{name=data.clipName,legacy=true,frameRate=data.sampleRate};
            var poses=Enumerable.Range(0,data.lastFrame+1).Select(f=>data.Evaluate(f,false)).ToArray();
            for(int bone=0;bone<data.paths.Length;bone++)
            {
                Quaternion previous=poses[0].rotations[bone];
                for(int f=0;f<poses.Length;f++){var q=Motion.SameHemisphere(previous,poses[f].rotations[bone]);poses[f].rotations[bone]=previous=q;}
                for(int axis=0;axis<4;axis++){int b=bone,a=axis;Curve(clip,data.paths[b],"localRotation."+"xyzw"[a],poses.Select(p=>p.rotations[b][a]).ToArray(),data.sampleRate);}
                if(bone==0||bone==data.hipsIndex)
                    for(int axis=0;axis<3;axis++){int b=bone,a=axis;Curve(clip,data.paths[b],"localPosition."+"xyz"[a],poses.Select(p=>(b==0?p.root:p.hips)[a]).ToArray(),data.sampleRate);}
            }
            clip.EnsureQuaternionContinuity();clip.wrapMode=data.loop?WrapMode.Loop:WrapMode.ClampForever;var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=data.loop;AnimationUtility.SetAnimationClipSettings(clip,settings);return clip;
        }
        static void Curve(AnimationClip clip,string path,string property,float[] values,int rate)
        {
            bool constant=values.All(v=>Mathf.Abs(v-values[0])<.0000001f);
            var curve=new AnimationCurve(constant?new[]{new Keyframe(0,values[0]),new Keyframe((values.Length-1)/(float)rate,values[values.Length-1])}:values.Select((v,f)=>new Keyframe(f/(float)rate,v)).ToArray());
            for(int k=0;k<curve.length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear);}
            clip.SetCurve(path,typeof(Transform),property,curve);
        }
        public static string ExportFbx(Motion data,AnimationClip clip,string path)
        {
            var rig=CreateRig(data.sourceRig);
            try
            {
                StripAnimators(rig);var bones=Bind(data,rig);Apply(data,bones,data.Evaluate(0,false));
                // Animation-only hierarchy: no duplicated mesh, textures, ghosts or room.
                foreach(var renderer in rig.GetComponentsInChildren<Renderer>(true))UnityEngine.Object.DestroyImmediate(renderer);
                foreach(var mesh in rig.GetComponentsInChildren<MeshFilter>(true))UnityEngine.Object.DestroyImmediate(mesh);
                foreach(var t in Bones(rig))t.gameObject.hideFlags=HideFlags.None;
                var animation=rig.AddComponent<Animation>();animation.playAutomatically=false;animation.AddClip(clip,clip.name);animation.clip=clip;
                var options=new ExportModelOptions{ModelAnimIncludeOption=Include.ModelAndAnim,ExportFormat=ExportFormat.Binary,UseMayaCompatibleNames=false,PreserveImportSettings=true,ObjectPosition=ObjectPosition.WorldAbsolute};
                string result=ModelExporter.ExportObject(Path.GetFullPath(path),rig,options);
                if(string.IsNullOrEmpty(result)||!File.Exists(path))throw new InvalidOperationException("FBX Exporter가 파일을 내보내지 못했습니다.");
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;importer.animationCompression=ModelImporterAnimationCompression.Off;importer.importCameras=false;importer.importLights=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;ApplyLoop(importer,data.loop,data.Duration,data.sampleRate);importer.SaveAndReimport();
                return path;
            }
            finally{UnityEngine.Object.DestroyImmediate(rig);}
        }
        public static AnimationClip ImportedClip(string path)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview__"));
        public static float Compare(Motion data,AnimationClip clip,out float positionError)
        {
            if(clip==null)throw new InvalidOperationException("재임포트된 FBX에 애니메이션이 없습니다.");
            var rig=CreateRig(data.sourceRig);float angle=0;positionError=0;
            // Verify the stored terminal key, not the wrapped first frame of a translating loop.
            var sampleClip=UnityEngine.Object.Instantiate(clip);sampleClip.wrapMode=WrapMode.ClampForever;var sampleSettings=AnimationUtility.GetAnimationClipSettings(sampleClip);sampleSettings.loopTime=false;AnimationUtility.SetAnimationClipSettings(sampleClip,sampleSettings);
            try
            {
                StripAnimators(rig);var bones=Bind(data,rig);
                for(int f=0;f<=data.lastFrame;f++)
                {
                    var expected=data.Evaluate(f,false);
                    // Reset to the base pose, not to expected. Missing/wrong FBX channels must fail.
                    Apply(data,bones,new Motion.Pose{root=data.bindPositions[0],hips=data.bindPositions[data.hipsIndex],rotations=data.bindRotations});sampleClip.SampleAnimation(rig,f/(float)data.sampleRate);
                    for(int b=0;b<bones.Length;b++){angle=Mathf.Max(angle,Quaternion.Angle(expected.rotations[b],bones[b].localRotation));var p=b==0?expected.root:b==data.hipsIndex?expected.hips:data.bindPositions[b];positionError=Mathf.Max(positionError,Vector3.Distance(p,bones[b].localPosition));if(Vector3.Distance(bones[b].localScale,data.bindScales[b])>.0001f)throw new InvalidOperationException("재임포트에서 본 스케일 변경");}
                }
                return angle;
            }
            finally{UnityEngine.Object.DestroyImmediate(sampleClip);UnityEngine.Object.DestroyImmediate(rig);}
        }
        public static string Revision(Motion source)
        {
            string path=AssetDatabase.GetAssetPath(source);using var hash=SHA256.Create();return JsonUtility.ToJson(source)+"|"+(File.Exists(path)?BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))):"missing");
        }
        public static string SaveBundle(Motion draft,ref Motion source,string expectedJson)
        {
            ResolveSharedRig(draft);
            if(!draft.Validate(out string reason))throw new InvalidOperationException(reason);
            RequireSharedClip(draft.startingClip);
            if(draft.keys.Count==0)throw new InvalidOperationException("F3으로 키를 하나 이상 저장하세요.");
            if(source!=null&&!RevisionUnchanged(source,expectedJson))throw new InvalidOperationException("원본이 외부에서 변경됐습니다. 현재 초안을 유지하고 원본을 다시 확인하세요.");
            Directory.CreateDirectory(Authored);Directory.CreateDirectory(Exports);AssetDatabase.Refresh();
            if(source==null&&string.IsNullOrWhiteSpace(draft.author))throw new InvalidOperationException("새 원본은 제작자 이름/고유 별칭을 입력한 뒤 저장하세요.");
            var currentSource=source;
            if(AssetDatabase.FindAssets("t:AnimationLabAuthoredMotion",new[]{Authored}).Select(g=>AssetDatabase.LoadAssetAtPath<Motion>(AssetDatabase.GUIDToAssetPath(g))).Any(m=>m!=null&&m!=currentSource&&m.id==draft.id))throw new InvalidOperationException("같은 원본 ID의 복사 파일이 있습니다. 제작 창의 내 사본 만들기로 독립 원본을 만들어 주세요.");
            string sourcePath=source!=null?AssetDatabase.GetAssetPath(source):Authored+"/"+SafeName(draft.author)+"_"+SafeName(draft.clipName)+".asset";
            if(source==null&&File.Exists(sourcePath))throw new InvalidOperationException("같은 제작자/동작 이름의 원본이 있습니다. 기존 원본을 열거나 독립 사본 이름을 지정하세요.");
            string stem=Path.GetFileNameWithoutExtension(sourcePath),animPath=Exports+"/"+stem+".anim",fbxPath=Exports+"/"+stem+".fbx";
            if(source!=null&&source.generatedClip!=null)animPath=AssetDatabase.GetAssetPath(source.generatedClip);
            if(source!=null&&source.generatedFbx!=null)fbxPath=AssetDatabase.GetAssetPath(source.generatedFbx);
            if(!animPath.StartsWith(Exports+"/",StringComparison.Ordinal)||!fbxPath.StartsWith(Exports+"/",StringComparison.Ordinal))throw new InvalidOperationException("제작 도구의 Exports 밖에 있는 파일은 덮지 않습니다.");
            if(source==null&&(File.Exists(animPath)||File.Exists(fbxPath)))throw new InvalidOperationException("동일 이름의 출력이 있습니다. 다른 클립 이름을 선택하세요.");
            if(source!=null&&(File.Exists(animPath)&&source.generatedClip!=AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath)||File.Exists(fbxPath)&&source.generatedFbx!=AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath)))throw new InvalidOperationException("다른 파일이 출력 경로를 사용하고 있습니다. 기존 출력 참조를 임의 교체하지 않습니다.");
            if(source!=null)foreach(string output in new[]{animPath,fbxPath})if(File.Exists(output)&&AssetImporter.GetAtPath(output).userData!="SubwayCarry.MotionAuthoring:"+draft.id)throw new InvalidOperationException("출력의 제작 원본 ID가 다릅니다. 다른 파일은 덮지 않습니다.");
            if(source!=null&&source.id!=draft.id)throw new InvalidOperationException("다른 제작 원본은 덮지 않습니다.");
            var targets=AnimationLabPhonePublication.Targets(draft);var oldSource=source;var oldClip=draft.generatedClip;var oldFbx=draft.generatedFbx;
            var backups=new Dictionary<string,byte[]>();bool publishing=false;AnimationClip baked=null;string token=Guid.NewGuid().ToString("N"),candidateFbx=Exports+"/__Candidate_"+token+".fbx";
            try
            {
                // Check temporary candidates before touching the last-good source, playback clip or binding.
                baked=Bake(draft);ExportFbx(draft,baked,candidateFbx);CandidateValidationProbe?.Invoke();
                ValidateRoundTrip(draft,baked);ValidateRoundTrip(draft,ImportedClip(candidateFbx));AnimationLabPhonePublication.Validate(draft,baked,targets);
                if(source!=null&&!RevisionUnchanged(source,expectedJson))throw new InvalidOperationException("검증 중 원본이 외부에서 변경됐습니다. 현재 초안과 기존 재생본을 유지합니다.");
                var paths=new[]{sourcePath,animPath,fbxPath}.Concat(targets.Select(AssetDatabase.GetAssetPath)).ToArray();
                foreach(string path in paths)foreach(string file in new[]{path,path+".meta"})backups[file]=File.Exists(file)?File.ReadAllBytes(file):null;
                var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
                if(existing!=null&&EditorUtility.IsDirty(existing))throw new InvalidOperationException("재생 클립에 저장하지 않은 외부 편집이 있습니다. 덮어쓰지 않습니다.");
                publishing=true;
                if(existing==null){existing=UnityEngine.Object.Instantiate(baked);AssetDatabase.CreateAsset(existing,animPath);}else{EditorUtility.CopySerialized(baked,existing);existing.name=Path.GetFileNameWithoutExtension(animPath);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing);}
                SetOwner(animPath,draft.id);File.Copy(candidateFbx,fbxPath,true);AssetDatabase.ImportAsset(fbxPath,ImportAssetOptions.ForceSynchronousImport);
                var importer=(ModelImporter)AssetImporter.GetAtPath(fbxPath);importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;importer.animationCompression=ModelImporterAnimationCompression.Off;importer.importCameras=false;importer.importLights=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.userData="SubwayCarry.MotionAuthoring:"+draft.id;ApplyLoop(importer,draft.loop,draft.Duration,draft.sampleRate);importer.SaveAndReimport();
                ValidateRoundTrip(draft,existing);ValidateRoundTrip(draft,ImportedClip(fbxPath));
                if(source==null){source=UnityEngine.Object.Instantiate(draft);source.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(source,sourcePath);}else JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(draft),source);
                draft.generatedClip=source.generatedClip=existing;draft.generatedFbx=source.generatedFbx=AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);EditorUtility.SetDirty(source);AssetDatabase.SaveAssetIfDirty(source);
                PublicationProbe?.Invoke();AnimationLabPhonePublication.Publish(source,existing,targets);AssetDatabase.ImportAsset(sourcePath,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);source=AssetDatabase.LoadAssetAtPath<Motion>(sourcePath);if(source==null)throw new InvalidOperationException("저장한 편집 원본을 재임포트할 수 없습니다.");
                return sourcePath;
            }
            catch(Exception error)
            {
                if(publishing)
                {
                    foreach(var backup in backups)if(backup.Value!=null)File.WriteAllBytes(backup.Key,backup.Value);else if(File.Exists(backup.Key))File.Delete(backup.Key);
                    foreach(string path in backups.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                }
                source=oldSource!=null?AssetDatabase.LoadAssetAtPath<Motion>(sourcePath):null;draft.generatedClip=oldClip;draft.generatedFbx=oldFbx;
                throw new InvalidOperationException("저장·적용 실패 · 이전 정상 재생본과 현재 초안 유지: "+error.Message,error);
            }
            finally{AssetDatabase.DeleteAsset(candidateFbx);if(baked!=null)UnityEngine.Object.DestroyImmediate(baked);}
        }
        internal static Action CandidateValidationProbe,PublicationProbe;
        static bool RevisionUnchanged(Motion source,string expected)
        {string actual=Revision(source);return actual==expected||expected!=null&&!expected.Contains("\"loop\":")&&actual.Replace(",\"loop\":false","")==expected;}
        static void ApplyLoop(ModelImporter importer,bool repeat,float duration,int sampleRate){var takes=importer.clipAnimations;if(takes.Length==0)takes=importer.defaultClipAnimations;foreach(var take in takes){take.firstFrame=0;take.lastFrame=duration*sampleRate;take.loopTime=repeat;take.loopPose=false;}importer.clipAnimations=takes;}
        static void ValidateRoundTrip(Motion data,AnimationClip clip)
        {float angle=Compare(data,clip,out float position);if(angle>.15f||position>.001f||Mathf.Abs(clip.length-data.Duration)>1f/data.sampleRate+.0001f)throw new InvalidOperationException($"출력 왕복 오차: 회전 {angle:F3}°, 위치 {position:F4}m, 길이 {clip.length:F3}s");}
        static void SetOwner(string path,string id){var importer=AssetImporter.GetAtPath(path);importer.userData="SubwayCarry.MotionAuthoring:"+id;AssetDatabase.WriteImportSettingsIfDirty(path);}
        static string SafeName(string text){var invalid=Path.GetInvalidFileNameChars();string name=new string(text.Trim().Where(c=>!invalid.Contains(c)).ToArray());if(name.Length==0)throw new InvalidOperationException("클립 이름이 필요합니다.");return name;}
    }
}
#endif

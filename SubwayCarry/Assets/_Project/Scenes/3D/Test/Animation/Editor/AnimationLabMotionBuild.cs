#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Use=SubwayCarry.Gameplay3D.AnimationLab.AnimationLabMotionSet.Use;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    public static class AnimationLabMotionBuild
    {
        const string Root=AnimationLabBuilder.Root;
        static readonly HumanBodyBones[] Starts={HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg};
        static readonly HumanBodyBones[] Ends={HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot};
        static readonly string[] RawStarts={"LeftArm","LeftForeArm","RightArm","RightForeArm","LeftUpLeg","LeftLeg","RightUpLeg","RightLeg"};
        static readonly string[] RawEnds={"LeftForeArm","LeftHand","RightForeArm","RightHand","LeftLeg","LeftFoot","RightLeg","RightFoot"};
        const int Rate=30;
        public static void Run()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Existing isolated QA only");
            try{Build();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
        }
        static Transform Bone(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).First(t=>t.name==name||t.name.EndsWith(":"+name));
        static Quaternion Body(Transform left,Transform right)=>Quaternion.LookRotation(Vector3.Cross(Vector3.ProjectOnPlane(right.position-left.position,Vector3.up),Vector3.up),Vector3.up);
        static AnimationClip Clip(string path)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
        static Vector3[][] Raw(string path,out float travel)
        {
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                foreach(var a in root.GetComponentsInChildren<Animator>())UnityEngine.Object.DestroyImmediate(a);
                var clip=Clip(path);var hips=Bone(root,"Hips");var left=Bone(root,"LeftUpLeg");var right=Bone(root,"RightUpLeg");
                var starts=RawStarts.Select(n=>Bone(root,n)).ToArray();var ends=RawEnds.Select(n=>Bone(root,n)).ToArray();
                var poses=new Vector3[61][];Vector3 begin=default,end=default;
                for(int i=0;i<61;i++){clip.SampleAnimation(root,clip.length*i/60);if(i==0)begin=hips.position;end=hips.position;var frame=Quaternion.Inverse(Body(left,right));poses[i]=new Vector3[8];for(int b=0;b<8;b++)poses[i][b]=frame*(ends[b].position-starts[b].position).normalized;}
                travel=Vector3.ProjectOnPlane(end-begin,Vector3.up).magnitude;return poses;
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static void Import(string path)
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.animationCompression=ModelImporterAnimationCompression.Off;
            var clips=importer.defaultClipAnimations;
            foreach(var c in clips){c.loopTime=false;c.loopPose=false;c.keepOriginalOrientation=true;c.keepOriginalPositionXZ=true;c.keepOriginalPositionY=true;c.lockRootRotation=false;c.lockRootPositionXZ=false;c.lockRootHeightY=false;}
            // Derived sub-clips, not a modified/downloaded FBX. Keep the original take intact.
            if(path.EndsWith("@Walking Left Turn.fbx")||path.EndsWith("@Start Walking.fbx")||path.EndsWith("@Stop Walking.fbx"))
            {
                var mirror=importer.defaultClipAnimations[0];var original=clips[0];
                mirror.name=original.name+"_Mirrored";mirror.mirror=true;mirror.loopTime=false;mirror.loopPose=false;
                mirror.keepOriginalOrientation=true;mirror.keepOriginalPositionXZ=true;mirror.keepOriginalPositionY=true;
                mirror.lockRootRotation=false;mirror.lockRootPositionXZ=false;mirror.lockRootHeightY=false;
                clips=clips.Concat(new[]{mirror}).ToArray();
            }
            importer.clipAnimations=clips;importer.SaveAndReimport();
            var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if(avatar==null||!avatar.isHuman||!avatar.isValid)throw new InvalidDataException("Mixamo Avatar invalid: "+path);
        }
        public static void Build()
        {
            var all=new List<AnimationLabMotionSet.Entry>();
            foreach(string file in Directory.GetFiles(Root+"/Mixamo","*.fbx").OrderBy(p=>p))
            {
                string path=file.Replace('\\','/');var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                byte[] previous=File.ReadAllBytes(path+".meta");Vector3[][] raw;float rawTravel;
                try{importer.animationType=ModelImporterAnimationType.Generic;importer.SaveAndReimport();raw=Raw(path,out rawTravel);}
                finally{File.WriteAllBytes(path+".meta",previous);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);}
                Import(path);
                foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")))
                {
                    bool mirrored=clip.name.EndsWith("_Mirrored");var source=raw;
                    if(mirrored){source=new Vector3[raw.Length][];int[] swap={2,3,0,1,6,7,4,5};for(int f=0;f<raw.Length;f++){source[f]=new Vector3[8];for(int b=0;b<8;b++){var v=raw[f][swap[b]];v.x=-v.x;source[f][b]=v;}}}
                    var row=Sample(clip,path,source);row.rawTravel=rawTravel;row.mirrored=mirrored;
                    using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))row.sha256=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
                    row.label=Path.GetFileNameWithoutExtension(path).Replace("X Bot@","")+(mirrored?" [좌우 반전]":"");Assess(row);
                    all.Add(row);Debug.Log("MIXAMO_MEASURE "+row.label+" seconds="+row.duration+" travel="+row.travel+" raw="+rawTravel+" yaw="+row.netYaw+" heading="+row.initialHeading+" delta="+row.frames[row.frames.Length-1].root+" speed="+row.meanSpeed+" peak="+row.peakSpeed+" head="+row.headYawRange+" armsUp="+row.armUpFraction+" pose="+row.retargetMean+"/"+row.retargetPeak+" uses="+row.uses+" loop="+row.loop+" usable="+row.usable);
                }
            }
            var free=AssetDatabase.LoadAssetAtPath<AnimationLabFreeWalkData>(Root+"/FreeWalk/Resources/FreeWalkReview.asset");
            var idle=Sample(free.idle,"Kubold official LITE Idle (existing license)",null);idle.label="Idle (Kubold LITE)";idle.uses=Use.Idle;idle.loop=true;idle.usable=true;all.Add(idle);
            AddNormalFamily(all,free);
            Directory.CreateDirectory(Root+"/Data/Resources");string assetPath=Root+"/Data/Resources/ReviewMotions.asset";
            var set=AssetDatabase.LoadAssetAtPath<AnimationLabMotionSet>(assetPath);bool create=set==null;if(create)set=ScriptableObject.CreateInstance<AnimationLabMotionSet>();
            set.entries=all.ToArray();if(create)AssetDatabase.CreateAsset(set,assetPath);else EditorUtility.SetDirty(set);AssetDatabase.SaveAssets();
        }
        internal static AnimationLabMotionSet.Entry Sample(AnimationClip clip,string path,Vector3[][] raw)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Character.prefab");
            var root=UnityEngine.Object.Instantiate(prefab);var animator=root.GetComponentInChildren<Animator>();
            var graph=PlayableGraph.Create("Motion analysis - target root extraction only");
            try
            {
                animator.runtimeAnimatorController=null;animator.avatar=AssetDatabase.LoadAssetAtPath<Avatar>(Root+"/FreeWalk/TargetAvatar.asset");
                animator.applyRootMotion=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var p=AnimationClipPlayable.Create(graph,clip);p.SetApplyFootIK(false);p.SetApplyPlayableIK(false);
                AnimationPlayableOutput.Create(graph,"Isolated target pose and motion",animator).SetSourcePlayable(p);graph.Play();graph.Evaluate(0);
                int count=Mathf.RoundToInt(clip.length*Rate)+1;float sampleRate=(count-1)/clip.length;var frames=new AnimationLabMotionSet.Frame[count];
                var feetWorld=new[]{new Vector3[count],new Vector3[count]};
                var hips=animator.GetBoneTransform(HumanBodyBones.Hips);var lf=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var rf=animator.GetBoneTransform(HumanBodyBones.RightFoot);
                var lk=animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);var rk=animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                var ll=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);var rl=animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                float leg=Vector3.Distance(ll.position,lk.position)+Vector3.Distance(lk.position,lf.position);
                var start=animator.transform.position;float lastYaw=animator.transform.eulerAngles.y,yaw=0;
                var initialBody=Body(ll,rl);float initialHeading=Mathf.DeltaAngle(animator.transform.eulerAngles.y,initialBody.eulerAngles.y);
                // Root trajectories use the Animator/root frame, NOT the stance-dependent pelvis axis.
                // A strafe's rotated pelvis is pose content, not an import-axis error.
                var basis=Quaternion.Inverse(Quaternion.Euler(0,animator.transform.eulerAngles.y,0));float headMin=999,headMax=-999,raised=0;
                var head=animator.GetBoneTransform(HumanBodyBones.Head);var initialHead=Quaternion.Inverse(initialBody)*head.rotation;
                for(int i=0;i<count;i++)
                {
                    if(i>0)graph.Evaluate(1/sampleRate);
                    float heading=animator.transform.eulerAngles.y;yaw+=Mathf.DeltaAngle(lastYaw,heading);lastYaw=heading;
                    var local=Quaternion.Inverse(Body(ll,rl));var a=frames[i];a.root=basis*(animator.transform.position-start);a.yaw=yaw;a.bodyYaw=Mathf.DeltaAngle(heading,Body(ll,rl).eulerAngles.y);
                    a.leftFoot=local*(lf.position-hips.position)/leg;a.rightFoot=local*(rf.position-hips.position)/leg;
                    a.leftKnee=local*(lk.position-hips.position)/leg;a.rightKnee=local*(rk.position-hips.position)/leg;a.hips=local*(hips.position-animator.transform.position)/leg;
                    frames[i]=a;feetWorld[0][i]=lf.position;feetWorld[1][i]=rf.position;
                    var headDelta=(local*head.rotation)*Quaternion.Inverse(initialHead);
                    float hy=Vector3.SignedAngle(Vector3.forward,headDelta*Vector3.forward,Vector3.up);headMin=Mathf.Min(headMin,hy);headMax=Mathf.Max(headMax,hy);
                    var headFrame=frames[i];headFrame.lookYaw=hy;frames[i]=headFrame;
                    foreach(var side in new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm}){var upper=animator.GetBoneTransform(side);var lower=animator.GetBoneTransform(side==HumanBodyBones.LeftUpperArm?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);if((lower.position-upper.position).normalized.y>-.3f)raised+=.5f/count;}
                }
                float groundL=feetWorld[0].Min(v=>v.y),groundR=feetWorld[1].Min(v=>v.y),speedSum=0,peak=0;
                for(int i=0;i<count;i++)
                {
                    int a=Mathf.Max(0,i-1),b=Mathf.Min(count-1,i+1);float dt=Mathf.Max(.001f,(b-a)/sampleRate);var f=frames[i];
                    f.speed=Vector3.ProjectOnPlane(frames[b].root-frames[a].root,Vector3.up).magnitude/dt;f.yawRate=(frames[b].yaw-frames[a].yaw)/dt;
                    f.leftVelocity=(frames[b].leftFoot-frames[a].leftFoot)/dt;f.rightVelocity=(frames[b].rightFoot-frames[a].rightFoot)/dt;
                    f.leftSupport=feetWorld[0][i].y<groundL+.045f&&Vector3.ProjectOnPlane(feetWorld[0][b]-feetWorld[0][a],Vector3.up).magnitude/dt<.3f?1:0;
                    f.rightSupport=feetWorld[1][i].y<groundR+.045f&&Vector3.ProjectOnPlane(feetWorld[1][b]-feetWorld[1][a],Vector3.up).magnitude/dt<.3f?1:0;
                    frames[i]=f;speedSum+=f.speed;peak=Mathf.Max(peak,f.speed);
                }
                float sum=0,max=0;
                if(raw!=null)
                {
                    for(int i=0;i<61;i++)
                    {
                        p.SetTime(clip.length*i/60);graph.Evaluate(0);var body=Quaternion.Inverse(Body(ll,rl));
                        for(int b=0;b<8;b++){var d=body*(animator.GetBoneTransform(Ends[b]).position-animator.GetBoneTransform(Starts[b]).position).normalized;float error=Vector3.Angle(raw[i][b],d);sum+=error/(61*8);max=Mathf.Max(max,error);}
                    }
                }
                return new AnimationLabMotionSet.Entry{clip=clip,source=path,sampleRate=sampleRate,duration=clip.length,frames=frames,travel=Vector3.ProjectOnPlane(frames[count-1].root,Vector3.up).magnitude,netYaw=yaw,meanSpeed=speedSum/count,peakSpeed=peak,headYawRange=headMax-headMin,armUpFraction=raised,retargetMean=sum,retargetPeak=max,initialHeading=initialHeading};
            }
            finally{graph.Destroy();UnityEngine.Object.DestroyImmediate(root);}
        }
        internal static void AddNormalFamily(List<AnimationLabMotionSet.Entry> rows,AnimationLabFreeWalkData free)
        {
            foreach(var row in rows)if(row.clip==free.idle)row.postureFamily=1;
            var clips=new[]{free.start,free.walk,free.stopLeft,free.stopRight};
            var names=new[]{"Kubold Start","Kubold Walk","Kubold Stop L","Kubold Stop R"};
            var uses=new[]{Use.Forward|Use.Start,Use.Forward,Use.Stop,Use.Stop};
            for(int i=0;i<clips.Length;i++)
            {
                if(rows.Any(e=>e.clip==clips[i]&&e.postureFamily==1))continue;
                string path=AssetDatabase.GetAssetPath(clips[i]);var row=Sample(clips[i],path,null);
                row.label=names[i];row.uses=uses[i];row.loop=i==1;row.usable=true;row.postureFamily=1;row.retargetMean=row.retargetPeak=-1;
                row.sha256=BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(path))).Replace("-","");
                row.notes="Compatible target posture family measured across Idle/Start/Walk/Stop; existing Kubold LITE source. Segment retarget error not recalculated.";
                rows.Add(row);
            }
        }
        static void Assess(AnimationLabMotionSet.Entry e)
        {
            e.usable=e.retargetPeak<35;var end=e.frames[e.frames.Length-1];var first=e.frames[0];float turn=Mathf.Abs(e.netYaw);
            if(e.meanSpeed<.15f&&turn<20)e.uses=e.headYawRange>25?Use.Look:Use.Idle;
            else if(turn>25)e.uses=e.travel>.25f?Use.MovingTurn:Use.Pivot;
            else if(Mathf.Abs(end.root.x)>Mathf.Abs(end.root.z)*1.25f)e.uses=Use.Strafe;
            else if(end.root.z<-.2f)e.uses=Use.Back;
            else e.uses=Use.Forward;
            if(first.speed<.35f&&end.speed>.5f)e.uses|=Use.Start;
            if(first.speed>.5f&&end.speed<.35f)e.uses|=Use.Stop;
            if(e.source.Contains("Stairs")){e.uses=Use.Elevated;e.notes="계단 전용 원본, 이번 Normal/Situations 이동 데이터에서 제외";}
            if(e.source.Contains("Dodging Back")){e.usable=false;e.notes="큰 회피 동작 원본: 가벼운 접촉 반응으로 대체하지 않음";}
            e.loop=turn<8&&Mathf.Abs(first.speed-end.speed)<.4f&&(first.leftFoot-end.leftFoot).magnitude+(first.rightFoot-end.rightFoot).magnitude<.25f&&e.meanSpeed>.4f;
            if(e.retargetPeak>=35)e.notes="원본 대비 사지 방향 차이가 큼: 재생 후보에서 제외, 사용자 검수 필요";
        }
    }
}
#endif

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
    public static class AnimationLabPhoneSetAuthoring
    {
        static AnimationLabPhoneMotion data;static Motion original;static GameObject rig;static Transform[] bones;
        static Motion.Pose idle,read,mirror;static Vector3 grabPosition,liftPosition,readPosition,landPosition;
        static Quaternion grabRotation,readRotation,landRotation,rightLandRotation,leftLandRotation;
        static Vector3 rightLandPosition,leftLandPosition;static float maximumWrist,maximumElbow;
        static int Joint(HumanBodyBones h)=>Array.IndexOf(original.paths,original.humanPaths[(int)h]);
        static Transform Bone(HumanBodyBones h)=>bones[Joint(h)];
        static Motion.Pose Capture(int frame)=>AnimationLabMotionAuthoring.Capture(original,rig,frame);
        static void Pose(Motion.Pose pose)=>AnimationLabMotionAuthoring.Apply(original,bones,pose);
        static Quaternion Reflect(Quaternion q)=>new Quaternion(q.x,-q.y,-q.z,q.w);
        static float Smooth(float t)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(t));
        static void Hand(bool left,Vector3 point,Quaternion rotation)
        {
            var a=Bone(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);var b=Bone(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var h=Bone(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            AnimationLabContacts.Solve(a,b,h,point,new Vector3(left?-.65f:.65f,-.75f,-.05f),1);h.rotation=rotation;
            maximumElbow=Mathf.Max(maximumElbow,180-Vector3.Angle(a.position-b.position,h.position-b.position));
            var f=Bone(left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal);maximumWrist=Mathf.Max(maximumWrist,Vector3.Angle(h.position-b.position,f.position-h.position));
        }
        public static void Build()
        {
            AnimationLabPhoneBuild.Guard();
            try
            {
                EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);data=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);original=data.authored;
                string userBefore=File.ReadAllText(AssetDatabase.GetAssetPath(original));rig=AnimationLabMotionAuthoring.CreateRig(data.rig);AnimationLabMotionAuthoring.StripAnimators(rig);bones=AnimationLabMotionAuthoring.Bind(original,rig);
                data.idle.SampleAnimation(rig.transform.Find(data.animatorPath).gameObject,0);idle=Capture(0);var rest=bones.Select(t=>t.rotation).ToArray();read=original.Evaluate(0,false);Pose(read);
                foreach(HumanBodyBones h in Enum.GetValues(typeof(HumanBodyBones)))
                {
                    string name=h.ToString();if(!name.StartsWith("Left"))continue;var right=(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),name.Replace("Left","Right"));int l=Joint(h),r=Joint(right);if(l<0||r<0)continue;
                    bones[l].rotation=Reflect(bones[r].rotation*Quaternion.Inverse(rest[r]))*rest[l];
                }
                mirror=Capture(0);Pose(read);var hand=Bone(HumanBodyBones.RightHand);readPosition=hand.TransformPoint(data.phoneLocalPosition);readRotation=hand.rotation*data.phoneLocalRotation;
                data.hipsPath=original.humanPaths[(int)HumanBodyBones.Hips];data.rightUpperPath=original.humanPaths[(int)HumanBodyBones.RightUpperArm];data.rightLowerPath=original.humanPaths[(int)HumanBodyBones.RightLowerArm];data.leftUpperPath=original.humanPaths[(int)HumanBodyBones.LeftUpperArm];data.leftLowerPath=original.humanPaths[(int)HumanBodyBones.LeftLowerArm];data.leftHandPath=original.humanPaths[(int)HumanBodyBones.LeftHand];
                var leftShoulder=original.humanPaths[(int)HumanBodyBones.LeftShoulder];data.bothArmPaths=original.paths.Where(p=>data.upperPaths.Contains(p)||p==leftShoulder||p.StartsWith(leftShoulder+"/",StringComparison.Ordinal)).ToArray();
                Pose(idle);var hips=Bone(HumanBodyBones.Hips);var center=hips.position+new Vector3(.22f,-.055f,.06f);var normal=Vector3.ProjectOnPlane((hand.rotation*data.phoneLocalRotation)*Vector3.forward,Vector3.up).normalized;
                var mount=Quaternion.LookRotation(normal,Vector3.up);data.pocketLocalPosition=hips.InverseTransformPoint(center+Vector3.up*.085f);data.pocketLocalRotation=Quaternion.Inverse(hips.rotation)*mount;data.pocketPhonePosition=new Vector3(0,-.085f,0);data.pocketPhoneRotation=Quaternion.Euler(0,0,180);
                grabPosition=center;grabRotation=mount*data.pocketPhoneRotation;liftPosition=center+new Vector3(.025f,.20f,.015f);
                data.pocketPrefab=Pocket();
                // Landscape frame: long phone edge horizontal, screen toward the face. Palms meet its two ends.
                landPosition=new Vector3(0,readPosition.y-.055f,Mathf.Max(.29f,readPosition.z));var face=(Bone(HumanBodyBones.Head).position-landPosition).normalized;landRotation=Quaternion.LookRotation(face,Vector3.right);
                rightLandPosition=new Vector3(.11f,landPosition.y-.09f,landPosition.z-.05f);leftLandPosition=new Vector3(-.11f,rightLandPosition.y,rightLandPosition.z);
                Pose(read);rightLandRotation=GripRotation(false,face,new Vector3(-.35f,.78f,.52f));Pose(mirror);leftLandRotation=GripRotation(true,face,new Vector3(.35f,.78f,.52f));
                data.landscapePhonePosition=Quaternion.Inverse(rightLandRotation)*(landPosition-rightLandPosition);data.landscapePhoneRotation=Quaternion.Inverse(rightLandRotation)*landRotation;
                data.landscapeLeftPosition=Quaternion.Inverse(landRotation)*(leftLandPosition-landPosition);data.landscapeLeftRotation=Quaternion.Inverse(landRotation)*leftLandRotation;
                if(data.actions==null||data.actions.Length!=8)data.actions=new AnimationLabPhoneMotion.ActionClip[8];
                string[] names={"폰_주머니에서꺼내기","폰_주머니에넣기","폰_올려보기","폰_내려들기","폰_양손가로잡기","폰_양손풀기","폰_양손가로유지","폰_들고걷기"};
                for(int i=0;i<8;i++)
                {
                    var role=(AnimationLabPhoneAction)i;var entry=data.actions[i]??new AnimationLabPhoneMotion.ActionClip();data.actions[i]=entry;entry.role=role;entry.displayName=names[i];
                    string path=AnimationLabMotionAuthoring.Authored+"/AI_"+names[i]+".asset";var saved=AssetDatabase.LoadAssetAtPath<Motion>(path);
                    var draft=UnityEngine.Object.Instantiate(original);draft.hideFlags=HideFlags.HideAndDontSave;draft.id=saved!=null?saved.id:Guid.NewGuid().ToString("N");draft.author="AI";draft.clipName=names[i];draft.generatedClip=saved!=null?saved.generatedClip:null;draft.generatedFbx=saved!=null?saved.generatedFbx:null;draft.keys.Clear();draft.drafts.Clear();draft.sampleRate=30;
                    entry.sourceId=draft.id;entry.authored=saved;entry.contactFrame=-1;entry.carryFrame=-1;
                    draft.lastFrame=role==AnimationLabPhoneAction.TakeOut||role==AnimationLabPhoneAction.PutAway?60:role==AnimationLabPhoneAction.LandscapeEnter||role==AnimationLabPhoneAction.LandscapeExit?36:role==AnimationLabPhoneAction.LandscapeHold?60:role==AnimationLabPhoneAction.CarryLoop?Mathf.RoundToInt(data.walk.length*30):27;
                    draft.loop=role==AnimationLabPhoneAction.LandscapeHold||role==AnimationLabPhoneAction.CarryLoop;
                    draft.startingClip=role==AnimationLabPhoneAction.CarryLoop?data.walk:original.startingClip;
                    if(role==AnimationLabPhoneAction.TakeOut){entry.contactFrame=18;entry.carryFrame=33;entry.contactStart=.18f;entry.contactEnd=.27f;}
                    if(role==AnimationLabPhoneAction.PutAway){entry.contactFrame=42;entry.carryFrame=27;entry.contactStart=.58f;entry.contactEnd=.67f;}
                    for(int f=0;f<=draft.lastFrame;f++)
                    {
                        float t=f/(float)draft.lastFrame;
                        if(role==AnimationLabPhoneAction.TakeOut)Take(t);
                        else if(role==AnimationLabPhoneAction.PutAway)Take(1-t);
                        else if(role==AnimationLabPhoneAction.Raise)Take(Mathf.Lerp(.55f,1,t));
                        else if(role==AnimationLabPhoneAction.Lower)Take(Mathf.Lerp(1,.55f,t));
                        else if(role==AnimationLabPhoneAction.LandscapeEnter)Landscape(t);
                        else if(role==AnimationLabPhoneAction.LandscapeExit)Landscape(1-t);
                        else if(role==AnimationLabPhoneAction.LandscapeHold){Landscape(1);var motion=new Vector3(0,Mathf.Sin(t*Mathf.PI*2)*.0035f,Mathf.Sin(t*Mathf.PI*2)*.0015f);Hand(false,rightLandPosition+motion,rightLandRotation);Hand(true,leftLandPosition+motion,leftLandRotation);}
                        else {data.walk.SampleAnimation(rig.transform.Find(data.animatorPath).gameObject,f/30f);for(int b=0;b<bones.Length;b++)if(original.paths[b].EndsWith(".R")&&(original.paths[b].Contains("f_")||original.paths[b].Contains("thumb")))bones[b].localRotation=read.rotations[b];}
                        var pose=Capture(f);pose.ease=0;draft.StoreKey(pose);
                    }
                    AnimationLabMotionAuthoring.SaveBundle(draft,ref saved,saved!=null?AnimationLabMotionAuthoring.Revision(saved):null);UnityEngine.Object.DestroyImmediate(draft);entry.authored=saved;entry.clip=saved.generatedClip;entry.loop=saved.loop;
                    EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);Debug.Log("PHONE_SET_SOURCE "+role+" keys="+saved.keys.Count+" seconds="+saved.Duration);
                }
                if(!data.HasActionSet||File.ReadAllText(AssetDatabase.GetAssetPath(original))!=userBefore)throw new InvalidOperationException("Incomplete set or approved user source changed");
                Debug.Log("PHONE_SET_BUILD_OK realSources=8 sameScene=true maxWristAim="+maximumWrist+" maxElbowFlex="+maximumElbow+" userSourceUntouched=true");UnityEngine.Object.DestroyImmediate(rig);rig=null;EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);if(rig!=null)UnityEngine.Object.DestroyImmediate(rig);EditorApplication.Exit(2);}
        }
        static Quaternion GripRotation(bool left,Vector3 normal,Vector3 up)
        {
            var hand=Bone(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);var middle=Bone(left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal);var index=Bone(left?HumanBodyBones.LeftIndexProximal:HumanBodyBones.RightIndexProximal);var little=Bone(left?HumanBodyBones.LeftLittleProximal:HumanBodyBones.RightLittleProximal);
            var finger=(middle.position-hand.position).normalized;var palm=Vector3.Cross(index.position-little.position,finger).normalized;if(Vector3.Dot(palm,normal)<0)palm=-palm;
            return Quaternion.LookRotation(normal,up)*Quaternion.Inverse(Quaternion.LookRotation(palm,finger))*hand.rotation;
        }
        static void Take(float t)
        {
            Pose(idle);if(t<=.000001f)return;if(t>=.999999f){Pose(read);return;}var idleHand=Bone(HumanBodyBones.RightHand);var start=idleHand.position;var startRot=idleHand.rotation;
            var gripRot=grabRotation*Quaternion.Inverse(data.phoneLocalRotation);var gripPos=grabPosition-gripRot*data.phoneLocalPosition;var liftPos=liftPosition-gripRot*data.phoneLocalPosition;
            var readRot=readRotation*Quaternion.Inverse(data.phoneLocalRotation);var readPos=readPosition-readRot*data.phoneLocalPosition;
            Vector3 target;Quaternion rotation;
            if(t<.28f){float s=Smooth(t/.28f);target=Vector3.Lerp(start,gripPos,s)+new Vector3(.045f,0,0)*Mathf.Sin(s*Mathf.PI);rotation=Quaternion.Slerp(startRot,gripRot,s);}
            else if(t<.36f){target=gripPos;rotation=gripRot;}
            else if(t<.55f){float s=Smooth((t-.36f)/.19f);target=Vector3.Lerp(gripPos,liftPos,s);rotation=gripRot;}
            else{float s=Smooth((t-.55f)/.45f);target=Vector3.Lerp(liftPos,readPos,s)+Vector3.right*.025f*Mathf.Sin(s*Mathf.PI);rotation=Quaternion.Slerp(gripRot,readRot,s);}
            for(int i=0;i<bones.Length;i++)if(original.paths[i].EndsWith(".R")&&(original.paths[i].Contains("f_")||original.paths[i].Contains("thumb")))bones[i].localRotation=Quaternion.Slerp(idle.rotations[i],read.rotations[i],Smooth(t/.28f));
            Hand(false,target,rotation);
            // Positional IK alone does not preserve the user's forearm roll. Join the exact approved FK pose.
            float finish=Smooth((t-.80f)/.20f);if(finish>0)for(int i=0;i<bones.Length;i++)if(data.upperPaths.Contains(original.paths[i]))bones[i].localRotation=Quaternion.Slerp(bones[i].localRotation,read.rotations[i],finish);
        }
        static void Landscape(float t)
        {
            Pose(read);if(t<=.000001f)return;float right=Smooth(t),left=Smooth((t-.12f)/.88f);var phonePosition=Vector3.Lerp(readPosition,landPosition,right);var phoneRotation=Quaternion.Slerp(readRotation,landRotation,right);
            var anchorPosition=Vector3.Lerp(data.phoneLocalPosition,data.landscapePhonePosition,right);var anchorRotation=Quaternion.Slerp(data.phoneLocalRotation,data.landscapePhoneRotation,right);var handRotation=phoneRotation*Quaternion.Inverse(anchorRotation);Hand(false,phonePosition-handRotation*anchorPosition,handRotation);
            var l=Bone(HumanBodyBones.LeftHand);var start=l.position;var rotation=l.rotation;
            for(int i=0;i<bones.Length;i++)if(original.paths[i].EndsWith(".L")&&(original.paths[i].Contains("f_")||original.paths[i].Contains("thumb")))bones[i].localRotation=Quaternion.Slerp(idle.rotations[i],mirror.rotations[i],left);
            Hand(true,Vector3.Lerp(start,leftLandPosition,left)+Vector3.left*.035f*Mathf.Sin(left*Mathf.PI),Quaternion.Slerp(rotation,leftLandRotation,left));
            float begin=Smooth(t/.18f);if(begin<1)for(int i=0;i<bones.Length;i++)if(data.bothArmPaths.Contains(original.paths[i]))bones[i].localRotation=Quaternion.Slerp(read.rotations[i],bones[i].localRotation,begin);
        }
        static GameObject Pocket()
        {
            string path=AnimationLabMotionAuthoring.Root+"/Prefabs/PhonePocket.prefab";
            string materialPath=AnimationLabMotionAuthoring.Root+"/Materials/PhonePocket.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.075f,.09f,.12f));mat.SetFloat("_Smoothness",.12f);AssetDatabase.CreateAsset(mat,materialPath);}
            var root=new GameObject("Review waist pocket");
            void Box(string name,Vector3 center,Vector3 size){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root.transform,false);g.transform.localPosition=center;g.transform.localScale=size;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=mat;}
            Box("Outer",new Vector3(0,-.09f,.019f),new Vector3(.105f,.18f,.003f));Box("Back",new Vector3(0,-.09f,-.019f),new Vector3(.105f,.18f,.003f));Box("Left seam",new Vector3(-.052f,-.09f,0),new Vector3(.003f,.18f,.038f));Box("Right seam",new Vector3(.052f,-.09f,0),new Vector3(.003f,.18f,.038f));Box("Bottom",new Vector3(0,-.18f,0),new Vector3(.105f,.003f,.038f));Box("Belt attachment",new Vector3(0,-.018f,.023f),new Vector3(.028f,.050f,.009f));
            var result=PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);return result;
        }
    }
}
#endif

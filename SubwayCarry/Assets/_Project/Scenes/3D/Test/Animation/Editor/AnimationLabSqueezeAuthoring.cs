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
    public static class AnimationLabSqueezeAuthoring
    {
        static AnimationLabPhoneMotion data;static Motion template;static GameObject rig;static Transform[] bones;static Motion.Pose idle;
        static Vector3[] startFeet=new Vector3[2];static Quaternion[] footRot=new Quaternion[2],handRot=new Quaternion[2];
        static Transform Bone(HumanBodyBones h)=>bones[Array.IndexOf(template.paths,template.humanPaths[(int)h])];
        static float Smooth(float n)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(n));
        public static void Build()
        {
            AnimationLabPhoneBuild.Guard();
            try
            {
                EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);data=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);template=data.authored;
                string userBefore=File.ReadAllText(AssetDatabase.GetAssetPath(template));rig=AnimationLabMotionAuthoring.CreateRig(data.rig);AnimationLabMotionAuthoring.StripAnimators(rig);bones=AnimationLabMotionAuthoring.Bind(template,rig);
                data.idle.SampleAnimation(rig.transform.Find(data.animatorPath).gameObject,0);idle=AnimationLabMotionAuthoring.Capture(template,rig,0);
                for(int i=0;i<2;i++){var foot=Bone(i==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);startFeet[i]=foot.position;footRot[i]=foot.rotation;handRot[i]=Bone(i==0?HumanBodyBones.LeftHand:HumanBodyBones.RightHand).rotation;}
                if(data.squeeze==null||data.squeeze.Length!=6)data.squeeze=new AnimationLabPhoneMotion.SqueezeClip[6];
                for(int side=-1;side<=1;side+=2)for(int phase=0;phase<3;phase++)
                {
                    int index=(side<0?0:3)+phase;var e=data.squeeze[index]??new AnimationLabPhoneMotion.SqueezeClip();data.squeeze[index]=e;e.phase=phase;e.side=side;
                    string name="사람사이_"+(side<0?"왼어깨_":"오른어깨_")+(phase==0?"진입":phase==1?"옆걸음유지":"빠져나오기");string path=AnimationLabMotionAuthoring.Authored+"/AI_"+name+".asset";
                    var saved=AssetDatabase.LoadAssetAtPath<Motion>(path);var draft=UnityEngine.Object.Instantiate(template);draft.hideFlags=HideFlags.HideAndDontSave;draft.id=saved!=null?saved.id:Guid.NewGuid().ToString("N");draft.clipName=name;draft.author="AI";draft.generatedClip=saved!=null?saved.generatedClip:null;draft.generatedFbx=saved!=null?saved.generatedFbx:null;draft.keys.Clear();draft.drafts.Clear();draft.sampleRate=30;draft.lastFrame=36;draft.loop=phase==1;draft.startingClip=data.idle;
                    e.sourceId=draft.id;e.authored=saved;
                    for(int f=0;f<=36;f++){Pose(phase,side,f/36f);var pose=AnimationLabMotionAuthoring.Capture(template,rig,f);pose.ease=0;draft.StoreKey(pose);}
                    AnimationLabMotionAuthoring.SaveBundle(draft,ref saved,saved!=null?AnimationLabMotionAuthoring.Revision(saved):null);UnityEngine.Object.DestroyImmediate(draft);e.authored=saved;e.clip=saved.generatedClip;EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);
                    Debug.Log("SQUEEZE_SOURCE "+name+" keys="+saved.keys.Count+" loop="+saved.loop);
                }
                if(!data.HasSqueezeSet||File.ReadAllText(AssetDatabase.GetAssetPath(template))!=userBefore)throw new InvalidOperationException("Squeeze data incomplete or user source changed");
                Debug.Log("SQUEEZE_BUILD_OK sources=6 Generic=true sameRig=true userSourceUntouched=true rootTrajectoryPreserved=true");UnityEngine.Object.DestroyImmediate(rig);EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);if(rig!=null)UnityEngine.Object.DestroyImmediate(rig);EditorApplication.Exit(2);}
        }
        static void Pose(int phase,int side,float t)
        {
            AnimationLabMotionAuthoring.Apply(template,bones,idle);float u=phase==2?1-t:t;float turn=phase==1?1:Smooth(u);var root=rig.transform;
            root.rotation=Quaternion.Euler(0,-side*90*turn,0);root.position=phase==1?Vector3.forward*(data.squeezeStride*t):Vector3.zero;
            for(int i=0;i<2;i++)
            {
                bool lead=side>0?i==1:i==0;var narrow=root.rotation*new Vector3(i==0?-.085f:.085f,startFeet[i].y,.015f);
                Vector3 target;Quaternion rotation;
                if(phase==1)
                {
                    float a=lead?.08f:.56f,b=lead?.44f:.92f;float step=Smooth((t-a)/(b-a));target=narrow+Vector3.forward*(data.squeezeStride*step);
                    if(t>a&&t<b)target.y+=Mathf.Sin((t-a)/(b-a)*Mathf.PI)*.045f;rotation=root.rotation*footRot[i];
                }
                else
                {
                    float a=lead?.08f:.55f,b=lead?.52f:.99f;float step=Smooth((u-a)/(b-a));target=Vector3.Lerp(startFeet[i],narrow,step);if(u>a&&u<b)target.y+=Mathf.Sin((u-a)/(b-a)*Mathf.PI)*.045f;
                    rotation=Quaternion.Slerp(footRot[i],root.rotation*footRot[i],step);
                }
                var thigh=Bone(i==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);var shin=Bone(i==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);var foot=Bone(i==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                AnimationLabContacts.Solve(thigh,shin,foot,target,root.forward,1);foot.rotation=rotation;
                var upper=Bone(i==0?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);var lower=Bone(i==0?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var hand=Bone(i==0?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                var tucked=root.TransformPoint(new Vector3(i==0?-.075f:.075f,1.10f,.11f));var targetHand=Vector3.Lerp(hand.position,tucked,turn);
                AnimationLabContacts.Solve(upper,lower,hand,targetHand,root.TransformDirection(new Vector3(i==0?-.3f:.3f,-1,.08f)),1);hand.localRotation=idle.rotations[Array.IndexOf(bones,hand)];
            }
            // Share the look-back over neck/head, keeping forward awareness without rotating the pelvis twice.
            var neck=Bone(HumanBodyBones.Neck);var head=Bone(HumanBodyBones.Head);neck.rotation=Quaternion.AngleAxis(side*25*turn,Vector3.up)*neck.rotation;head.rotation=Quaternion.AngleAxis(side*43*turn,Vector3.up)*head.rotation;
        }
    }
}
#endif

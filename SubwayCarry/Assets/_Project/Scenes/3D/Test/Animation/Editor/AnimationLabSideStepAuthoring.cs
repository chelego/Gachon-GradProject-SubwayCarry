#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Direct Generic authoring. Contact foot plans and pelvis reach are solved together.
    public static class AnimationLabSideStepAuthoring
    {
        public static void BuildRight(){AnimationLabPhoneBuild.Guard();try{for(int phase=0;phase<3;phase++)BuildOne(phase);Debug.Log("SIDE_RIGHT_BUILD_OK phases=3 existingSourcesPreserved=true");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}}
        static void BuildOne(int phase)
        {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var d=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);var m=d.authored;
                var actor=AnimationLabMotionAuthoring.CreateRig(d.rig);AnimationLabMotionAuthoring.StripAnimators(actor);var bones=AnimationLabMotionAuthoring.Bind(m,actor);
                Transform B(HumanBodyBones h)=>actor.transform.Find(m.humanPaths[(int)h]);
                var reference=new AnimationLabAuthoredMotion.Pose{root=m.bindPositions[0],hips=m.bindPositions[m.hipsIndex],rotations=m.bindRotations};
                AnimationLabMotionAuthoring.Apply(m,bones,reference);d.idle.SampleAnimation(actor.transform.Find(d.animatorPath).gameObject,0);
                var idle=AnimationLabMotionAuthoring.Capture(m,actor,0);AnimationLabMotionAuthoring.Apply(m,bones,idle);var foot=new[]{B(HumanBodyBones.LeftFoot),B(HumanBodyBones.RightFoot)};var toe=new[]{B(HumanBodyBones.LeftToes),B(HumanBodyBones.RightToes)};
                var normalRot=new[]{foot[0].rotation,foot[1].rotation};var toeLocal=new[]{toe[0].localRotation,toe[1].localRotation};float[] footY={foot[0].position.y,foot[1].position.y};
                for(int i=0;i<2;i++){float yaw=Vector3.SignedAngle(Vector3.forward,Vector3.ProjectOnPlane(toe[i].position-foot[i].position,Vector3.up),Vector3.up);normalRot[i]=Quaternion.AngleAxis(-75-yaw,Vector3.up)*normalRot[i];}
                float hipYaw=Vector3.SignedAngle(Vector3.forward,Vector3.Cross(B(HumanBodyBones.RightUpperLeg).position-B(HumanBodyBones.LeftUpperLeg).position,Vector3.up),Vector3.up);
                var entry=d.FindSqueeze(phase,1,1);var saved=entry?.authored;var draft=UnityEngine.Object.Instantiate(saved!=null?saved:m);if(saved==null){draft.id=Guid.NewGuid().ToString("N");draft.author="AI";draft.clipName="사람사이_오른어깨_옆디딤_"+new[]{"진입","유지","이탈"}[phase];}draft.keys.Clear();draft.drafts.Clear();draft.generatedClip=null;draft.generatedFbx=null;draft.sampleRate=30;draft.lastFrame=phase==1?30:48;draft.loop=phase==1;draft.startingClip=d.walk;
                var stance=new[]{Quaternion.Euler(0,-65,0)*new Vector3(-.105f,0,0),Quaternion.Euler(0,-65,0)*new Vector3(.105f,0,0)};for(int i=0;i<2;i++)stance[i].y=footY[i];
                AnimationLabMotionAuthoring.Apply(m,bones,reference);d.walk.SampleAnimation(actor.transform.Find(d.animatorPath).gameObject,.15f*d.walk.length);var walkPose=AnimationLabMotionAuthoring.Capture(m,actor,0);var walkFeet=new[]{foot[0].position,foot[1].position};var walkRotation=new[]{foot[0].rotation,foot[1].rotation};AnimationLabMotionAuthoring.Apply(m,bones,idle);
                float total=phase==1?.22f:phase==0?.60f:.72f;int steps=phase==1?2:4;var startFeet=phase==0?walkFeet:stance;var startRot=phase==0?walkRotation:normalRot;var destinations=new Vector3[steps];var destinationRotation=new Quaternion[steps];int firstFoot=phase==0&&walkFeet[0].y>walkFeet[1].y?0:1;
                for(int n=0;n<steps;n++){int leg=n%2==0?firstFoot:1-firstFoot;float pair=(n/2+1)/(steps/2f);float angle=phase==1?65:phase==0?65*pair:65*(1-pair);Vector3 p0=phase==2&&pair>=1?walkFeet[leg]:Quaternion.Euler(0,-angle,0)*new Vector3(leg==0?-.105f:.105f,0,0);if(!(phase==2&&pair>=1))p0.y=footY[leg];destinations[n]=Vector3.forward*(total*pair)+p0;destinationRotation[n]=phase==2&&pair>=1?walkRotation[leg]:Quaternion.AngleAxis(75*(1-angle/65),Vector3.up)*normalRot[leg];}
                float maxError=0,maxKnee=0,maxWidth=0,minGround=100,maxGround=-100;int worstFrame=0,worstLeg=0;var mesh=new Mesh();
                // Stable sole vertex membership is measured from the source mesh before editing.
                var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();var sole=new System.Collections.Generic.List<int>[skins.Length,2];var verts=new System.Collections.Generic.List<Vector3>();float floor=100;
                for(int k=0;k<skins.Length;k++){skins[k].BakeMesh(mesh);mesh.GetVertices(verts);for(int i=0;i<2;i++)sole[k,i]=new System.Collections.Generic.List<int>();for(int v=0;v<verts.Count;v++){var w=skins[k].transform.TransformPoint(verts[v]);floor=Mathf.Min(floor,w.y);if(w.y>Mathf.Max(footY[0],footY[1])+.12f)continue;int i=(w-foot[0].position).sqrMagnitude<(w-foot[1].position).sqrMagnitude?0:1;sole[k,i].Add(v);}}
                float[] soleLow=new float[2];
                for(int f=0;f<=draft.lastFrame;f++)
                {
                    float t=f/(float)draft.lastFrame,x=Mathf.Min(steps-.000001f,t*steps);int step=(int)x;float u=t>=1?1:x-step;int moving=step%2==0?firstFoot:1-firstFoot;float s=Mathf.SmoothStep(0,1,Mathf.Clamp01((u-.10f)/.70f));float lift=.040f*16*s*s*(1-s)*(1-s);float shape=phase==0?Mathf.SmoothStep(0,1,t):phase==2?1-Mathf.SmoothStep(0,1,t):1;
                    var target=(Vector3[])startFeet.Clone();var fixedRotation=(Quaternion[])startRot.Clone();for(int n=0;n<step;n++){int leg=n%2==0?firstFoot:1-firstFoot;target[leg]=destinations[n];fixedRotation[leg]=destinationRotation[n];}target[moving]=Vector3.Lerp(target[moving],destinations[step],s)+Vector3.up*lift;
                    var basePose=idle.Copy(f);for(int j=0;j<basePose.rotations.Length;j++)basePose.rotations[j]=Quaternion.Slerp(walkPose.rotations[j],idle.rotations[j],shape);basePose.hips=Vector3.Lerp(walkPose.hips,idle.hips,shape);AnimationLabMotionAuthoring.Apply(m,bones,basePose);
                    float progress=phase==0?1.6f*t-.6f*t*t:phase==2?.5f*t+.5f*t*t:t;var root=Vector3.forward*(total*progress);actor.transform.SetPositionAndRotation(root,Quaternion.Euler(0,-65*shape,0));
                    var hips=B(HumanBodyBones.Hips);float currentHipYaw=Vector3.SignedAngle(Vector3.forward,Vector3.Cross(B(HumanBodyBones.RightUpperLeg).position-B(HumanBodyBones.LeftUpperLeg).position,Vector3.up),Vector3.up);hips.rotation=Quaternion.AngleAxis(Mathf.DeltaAngle(currentHipYaw,-65*shape),Vector3.up)*hips.rotation;
                    float transfer=Mathf.SmoothStep(0,1,Mathf.Clamp01((u-.76f)/.24f));float rightLoad=moving==1?transfer:1-transfer;var support=Vector3.Lerp(target[0],target[1],rightLoad);var center=Vector3.Lerp(hips.position,root,shape);
                    hips.position=new Vector3(center.x+Mathf.Clamp(support.x-center.x,-.08f,.08f)*.8f*shape,.86f,center.z+Mathf.Clamp(support.z-center.z,-.055f,.055f)*shape);
                    float allowedY=10;for(int leg=0;leg<2;leg++){var a=B(leg==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);var b=B(leg==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);float l1=Vector3.Distance(a.position,b.position),l2=Vector3.Distance(b.position,foot[leg].position);bool loaded=(leg!=moving||u<=.1f||u>=.8f)&&target[leg].y<footY[leg]+.018f;float reach=loaded?Mathf.Sqrt(l1*l1+l2*l2+2*l1*l2*Mathf.Cos(20*Mathf.Deg2Rad)):l1+l2-.009f;float horizontal=Vector3.ProjectOnPlane(target[leg]-a.position,Vector3.up).sqrMagnitude;float top=target[leg].y+Mathf.Sqrt(Mathf.Max(0,reach*reach-horizontal))-(a.position.y-hips.position.y);allowedY=Mathf.Min(allowedY,top);}hips.position=new Vector3(hips.position.x,allowedY,hips.position.z);
                    var chestForward=Vector3.Cross(B(HumanBodyBones.RightUpperArm).position-B(HumanBodyBones.LeftUpperArm).position,Vector3.up);float extra=Mathf.DeltaAngle(Vector3.SignedAngle(Vector3.forward,chestForward,Vector3.up),-77*shape);
                    var spine=new[]{HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.UpperChest};float[] share={.40f,.35f,.25f};for(int j=0;j<3;j++){var b=B(spine[j]);b.rotation=Quaternion.AngleAxis(extra*share[j],Vector3.up)*b.rotation;}
                    foreach(var h in new[]{HumanBodyBones.Neck,HumanBodyBones.Head}){var b=B(h);b.rotation=Quaternion.AngleAxis(21*shape,Vector3.up)*b.rotation;}
                    for(int i=0;i<2;i++)
                    {
                        bool l=i==0;var arm=B(l?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);var fore=B(l?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var hand=B(l?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                        AnimationLabContacts.Solve(arm,fore,hand,Vector3.Lerp(hand.position,actor.transform.TransformPoint(new Vector3(l?-.225f:.225f,l?.94f:.99f,l?-.13f:.25f)),shape),actor.transform.TransformDirection(new Vector3(l?-.35f:.35f,-1,-.10f)),1);
                        var old=d.FindSqueeze(1,1).authored.Evaluate(0,false);for(int j=0;j<m.paths.Length;j++)if(m.paths[j].StartsWith(m.humanPaths[(int)(l?HumanBodyBones.LeftHand:HumanBodyBones.RightHand)]+"/",StringComparison.Ordinal))bones[j].localRotation=Quaternion.Slerp(bones[j].localRotation,old.rotations[j],shape);
                        var a=B(l?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);var b=B(l?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);var c=foot[i];var rotation=i==moving?Quaternion.Slerp(fixedRotation[i],destinationRotation[step],s):fixedRotation[i];var direction=Quaternion.AngleAxis(Vector3.SignedAngle(Vector3.forward,Vector3.ProjectOnPlane(rotation*Quaternion.Inverse(normalRot[i])*(Quaternion.Euler(0,-75,0)*Vector3.forward),Vector3.up),Vector3.up),Vector3.up)*Vector3.forward;
                        AnimationLabContacts.Solve(a,b,c,target[i],direction,1);c.rotation=rotation;toe[i].localRotation=Quaternion.Slerp(toe[i].localRotation,toeLocal[i],shape);
                        float err=Vector3.Distance(c.position,target[i]);if(err>maxError){maxError=err;worstFrame=f;worstLeg=i;}
                        maxKnee=Mathf.Max(maxKnee,180-Vector3.Angle(a.position-b.position,c.position-b.position));
                    }
                    for(int pass=0;pass<2;pass++)
                    {
                        soleLow[0]=soleLow[1]=100;float lo=100,hi=-100;
                        for(int k=0;k<skins.Length;k++){skins[k].BakeMesh(mesh);mesh.GetVertices(verts);for(int v=0;v<verts.Count;v++){var w=skins[k].transform.TransformPoint(verts[v]);lo=Mathf.Min(lo,w.x);hi=Mathf.Max(hi,w.x);}for(int i=0;i<2;i++)foreach(int v in sole[k,i])soleLow[i]=Mathf.Min(soleLow[i],skins[k].transform.TransformPoint(verts[v]).y);}
                        maxWidth=Mathf.Max(maxWidth,hi-lo);
                        for(int i=0;i<2;i++)if((i!=moving||u<=.1f||u>=.8f)&&target[i].y<footY[i]+.018f){float dy=floor-soleLow[i];if(Mathf.Abs(dy)>.025f)throw new InvalidOperationException("Cycle sole correction rejected f="+f+" leg="+i+" dy="+dy);var a=B(i==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);var b=B(i==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);var r=foot[i].rotation;AnimationLabContacts.Solve(a,b,foot[i],foot[i].position+Vector3.up*dy,Quaternion.Euler(0,-75,0)*Vector3.forward,1);foot[i].rotation=r;}
                    }
                    float low=Mathf.Min(soleLow[0],soleLow[1])-floor;minGround=Mathf.Min(minGround,low);maxGround=Mathf.Max(maxGround,low);
                    var pose=AnimationLabMotionAuthoring.Capture(m,actor,f);pose.ease=0;draft.StoreKey(pose);
                }
                if(draft.loop){var first=draft.keys[0];var last=draft.keys[draft.lastFrame];last.rotations=(Quaternion[])first.rotations.Clone();last.hips=first.hips;}
                Debug.Log("SIDE_SOURCE phase="+phase+" seconds="+draft.Duration+" root="+total+" maxReachError="+maxError+" worstFrame="+worstFrame+" worstLeg="+worstLeg+" kneeFlexMax="+maxKnee+" meshWidth="+maxWidth+" soleGap="+minGround+".."+maxGround);
                if(maxError>.008f||minGround<-.008f||maxGround>.012f||(phase==1&&maxWidth>.54f))throw new InvalidOperationException("Cycle not accepted; no source published");
                if(entry==null){entry=new AnimationLabPhoneMotion.SqueezeClip{phase=phase,side=1,passageMode=1,sourceId=draft.id};d.squeeze=d.squeeze.Concat(new[]{entry}).ToArray();}
                AnimationLabMotionAuthoring.SaveBundle(draft,ref saved,saved!=null?AnimationLabMotionAuthoring.Revision(saved):null);entry.authored=saved;entry.clip=saved.generatedClip;entry.endWalkPhase=.15f;EditorUtility.SetDirty(d);AssetDatabase.SaveAssetIfDirty(d);
                UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(actor);UnityEngine.Object.DestroyImmediate(draft);
        }
    }
}
#endif

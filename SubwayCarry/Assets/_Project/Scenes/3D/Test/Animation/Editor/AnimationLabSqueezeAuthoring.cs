#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Motion=SubwayCarry.Gameplay3D.AnimationLab.AnimationLabAuthoredMotion;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // CC0 walking supplies the gait, lift, pelvis rhythm and arm baseline.
    // The oblique passage, shorter steps and relaxed arms are directly authored edits.
    public static class AnimationLabSqueezeAuthoring
    {
        static AnimationLabPhoneMotion data;static Motion template;static GameObject rig;static Transform[] bones;static Motion.Pose reference;
        sealed class SoleMesh {public SkinnedMeshRenderer skin;public Mesh mesh=new Mesh();public System.Collections.Generic.List<Vector3> vertices=new System.Collections.Generic.List<Vector3>();public System.Collections.Generic.List<int>[] indices={new System.Collections.Generic.List<int>(),new System.Collections.Generic.List<int>()};}
        static readonly System.Collections.Generic.List<SoleMesh> soles=new System.Collections.Generic.List<SoleMesh>();
        static readonly float[] soleFloor={100f,100f};
        static readonly float[] floorFoot={100f,100f};
        static readonly Quaternion[] neutralFoot=new Quaternion[2],neutralToe=new Quaternion[2];
        static readonly AnimationCurve gaitTiming=new AnimationCurve(new Keyframe(0,0,1,1),new Keyframe(.166667f,.172f,1,1),new Keyframe(.333333f,.330f,1,1),new Keyframe(.5f,.5f,1,1),new Keyframe(.666667f,.661f,1,1),new Keyframe(.833333f,.836f,1,1),new Keyframe(1,1,1,1));
        static Transform Bone(HumanBodyBones h)=>bones[Array.IndexOf(template.paths,template.humanPaths[(int)h])];
        static float Smooth(float t)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(t));
        public static void Build()=>BuildSides(-1,1);
        public static void BuildRight()=>BuildSides(1,1);
        public static void BuildLeft()=>BuildSides(-1,-1);
        static void BuildSides(int first,int last)
        {
            AnimationLabPhoneBuild.Guard();
            try
            {
                EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);
                data=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);template=data.authored;
                string userBefore=File.ReadAllText(AssetDatabase.GetAssetPath(template));
                rig=AnimationLabMotionAuthoring.CreateRig(data.rig);AnimationLabMotionAuthoring.StripAnimators(rig);bones=AnimationLabMotionAuthoring.Bind(template,rig);
                reference=new Motion.Pose{root=template.bindPositions[0],hips=template.bindPositions[template.hipsIndex],rotations=template.bindRotations};
                InspectWalk();
                for(int side=first;side<=last;side+=2)for(int phase=0;phase<3;phase++)BuildOne(side,phase);
                if(File.ReadAllText(AssetDatabase.GetAssetPath(template))!=userBefore)throw new InvalidOperationException("User hand source changed");
                Debug.Log("SQUEEZE_BUILD_OK sides="+first+".."+last+" gait=CC0_Walk plusDirectEdits=true noIdleSineGait=true sameRig=true userHandUntouched=true");
                ReleaseSoles();UnityEngine.Object.DestroyImmediate(rig);EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);ReleaseSoles();if(rig!=null)UnityEngine.Object.DestroyImmediate(rig);EditorApplication.Exit(2);}
        }
        static void SampleWalk(float phase)
        {
            AnimationLabMotionAuthoring.Apply(template,bones,reference);
            data.walk.SampleAnimation(rig.transform.Find(data.animatorPath).gameObject,Mathf.Repeat(phase,1)*data.walk.length);
        }
        static void InspectWalk()
        {
            data.idle.SampleAnimation(rig.transform.Find(data.animatorPath).gameObject,0);for(int i=0;i<2;i++){neutralFoot[i]=Bone(i==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot).rotation;neutralToe[i]=Bone(i==0?HumanBodyBones.LeftToes:HumanBodyBones.RightToes).localRotation;}
            CaptureSoles();
            float low=100,high=-100,hipsLow=100,hipsHigh=-100;
            for(int f=0;f<=60;f++){SampleWalk(f/60f);for(int i=0;i<2;i++)floorFoot[i]=Mathf.Min(floorFoot[i],Bone(i==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot).position.y);var foot=Bone(HumanBodyBones.LeftFoot);low=Mathf.Min(low,foot.position.y);high=Mathf.Max(high,foot.position.y);hipsLow=Mathf.Min(hipsLow,Bone(HumanBodyBones.Hips).position.y);hipsHigh=Mathf.Max(hipsHigh,Bone(HumanBodyBones.Hips).position.y);}
            Debug.Log("SQUEEZE_CC0_SAMPLE clip="+data.walk.name+" length="+data.walk.length+" footHeight="+low+".."+high+" pelvisHeight="+hipsLow+".."+hipsHigh+" generic="+!data.walk.isHumanMotion);
        }
        static void BuildOne(int side,int phase)
        {
            var e=data.FindSqueeze(phase,side);if(e==null||e.authored==null)throw new InvalidOperationException("Existing squeeze source required");
            var saved=e.authored;var draft=UnityEngine.Object.Instantiate(saved);draft.hideFlags=HideFlags.HideAndDontSave;
            draft.keys.Clear();draft.drafts.Clear();draft.sampleRate=30;draft.loop=phase==1;draft.startingClip=data.walk;
            int cycles=phase==1?3:1;draft.lastFrame=Mathf.RoundToInt(cycles*data.walk.length/1.10f*30);int count=draft.lastFrame+1;
            var root=new Vector3[count];var poses=new Motion.Pose[count];var feet=new Vector3[2,count];var footRotation=new Quaternion[2,count];var knee=new Vector3[2,count];
            float distance=0,previousScale=.9f;
            for(int f=0;f<count;f++)
            {
                float t=f/(float)draft.lastFrame;float shape=phase==0?Smooth(t):phase==2?1-Smooth(t):1;
                float stride=phase==0?Mathf.Lerp(.90f,.55f,Smooth(t)):phase==2?Mathf.Lerp(.55f,.90f,Smooth(t)):.55f;
                // Three deliberately edited step pairs, periodic at the boundary; no frame-wise noise.
                if(phase==1)stride+=.022f*(t<.33f?Smooth(t/.33f):t<.67f?Mathf.Lerp(1,-.65f,Smooth((t-.33f)/.34f)):Mathf.Lerp(-.65f,0,Smooth((t-.67f)/.33f)));
                if(f>0)distance+=(stride+previousScale)*.5f*data.referenceSpeed*cycles*data.walk.length/draft.lastFrame;
                previousScale=stride;root[f]=Vector3.forward*distance;SampleWalk(cycles*(phase==1?gaitTiming.Evaluate(t):t));
                for(int i=0;i<2;i++)
                {
                    bool left=i==0;var foot=Bone(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                    var p=foot.position;p.x*=Mathf.Lerp(1,.82f,shape);p.z*=stride;p.y=floorFoot[i]+(p.y-floorFoot[i])*Mathf.Lerp(1,.55f,shape);p.x+=(i==0?-1:1)*shape*(.55f-stride)*.4f;feet[i,f]=root[f]+p;footRotation[i,f]=Quaternion.Slerp(neutralFoot[i],foot.rotation,Mathf.Lerp(1,.55f,shape));var toe=Bone(left?HumanBodyBones.LeftToes:HumanBodyBones.RightToes);toe.localRotation=Quaternion.Slerp(neutralToe[i],toe.localRotation,Mathf.Lerp(1,.55f,shape));
                    var shin=Bone(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                    knee[i,f]=(shin.position-(Bone(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg).position+foot.position)*.5f).normalized;
                }
                rig.transform.SetPositionAndRotation(root[f],Quaternion.Euler(0,-side*45*shape,0));
                // Retain recorded pelvis rhythm; the extra lateral adjustment follows the current support.
                var hips=Bone(HumanBodyBones.Hips);hips.position+=Vector3.right*(Mathf.Clamp((feet[1,f].y-feet[0,f].y)*.22f,-.009f,.009f)*shape);
                float leadingLoad=Mathf.Clamp01((feet[side>0?0:1,f].y-feet[side>0?1:0,f].y)*12);hips.position+=Vector3.forward*(.008f*shape*leadingLoad);
                var chest=Bone(HumanBodyBones.UpperChest);chest.rotation=Quaternion.AngleAxis(1.8f*shape*leadingLoad,Vector3.right)*chest.rotation;chest.rotation=Quaternion.AngleAxis(side*8*shape,Vector3.up)*chest.rotation;
                foreach(var h in new[]{HumanBodyBones.Neck,HumanBodyBones.Head}){var b=Bone(h);b.rotation=Quaternion.AngleAxis(side*(h==HumanBodyBones.Neck?12:8)*shape,Vector3.up)*b.rotation;}
                for(int i=0;i<2;i++)
                {
                    bool left=i==0,lead=side>0?!left:left;
                    var upper=Bone(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);var lower=Bone(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var hand=Bone(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                    var local=new Vector3(left?-.225f:.225f,lead?.99f:.94f,lead?.25f:-.13f);
                    var wanted=Vector3.Lerp(hand.position,rig.transform.TransformPoint(local),shape);
                    AnimationLabContacts.Solve(upper,lower,hand,wanted,rig.transform.TransformDirection(new Vector3(left?-.35f:.35f,-1,-.10f)),shape);
                    RelaxFingers(left,shape);
                }
                poses[f]=AnimationLabMotionAuthoring.Capture(template,rig,f);
            }
            float error=0,step=0;var previous=new Vector3[2];var plant=new Vector3[2];var supporting=new bool[2];var correction=new Vector3[2];
            var minY=new[]{100f,100f};for(int i=0;i<2;i++)for(int f=0;f<count;f++)minY[i]=Mathf.Min(minY[i],feet[i,f].y);
            for(int f=0;f<count;f++)
            {
                AnimationLabMotionAuthoring.Apply(template,bones,poses[f]);
                for(int i=0;i<2;i++)
                {
                    bool left=i==0;var a=Bone(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);var b=Bone(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);var c=Bone(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                    float velocity=(feet[i,Mathf.Min(f+1,count-1)]-feet[i,Mathf.Max(0,f-1)]).magnitude*30/(f==0||f==count-1?1:2);
                    bool grounded=feet[i,f].y<minY[i]+.020f&&velocity<.30f;var target=feet[i,f];
                    if(grounded){if(!supporting[i])plant[i]=target;correction[i]=plant[i]-target;target=plant[i];}
                    else {correction[i]=Vector3.MoveTowards(correction[i],Vector3.zero,.012f);target+=correction[i];}
                    supporting[i]=grounded;AnimationLabContacts.Solve(a,b,c,target,knee[i,f],1);c.rotation=footRotation[i,f];
                    float reachError=Vector3.Distance(c.position,target);if(reachError>error+.01f)Debug.Log("SQUEEZE_REACH side="+side+" phase="+phase+" frame="+f+" leg="+i+" error="+reachError+" hip="+a.position.ToString("F3")+" target="+target.ToString("F3"));error=Mathf.Max(error,reachError);if(f>0)step=Mathf.Max(step,Vector3.Distance(c.position,previous[i]));previous[i]=c.position;
                }
                CorrectSoles(f,knee);var pose=AnimationLabMotionAuthoring.Capture(template,rig,f);pose.ease=0;draft.StoreKey(pose);
            }
            if(draft.loop){var a=draft.keys[0];var b=draft.keys[draft.keys.Count-1];b.rotations=(Quaternion[])a.rotations.Clone();b.hips=a.hips;}
            if(error>.035f)throw new InvalidOperationException("Squeeze target outside leg reach: "+error+" side="+side+" phase="+phase);
            AnimationLabMotionAuthoring.SaveBundle(draft,ref saved,AnimationLabMotionAuthoring.Revision(saved));UnityEngine.Object.DestroyImmediate(draft);e.authored=saved;e.clip=saved.generatedClip;
            data.squeezeSpeed=.70f;EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);
            Debug.Log("SQUEEZE_SOURCE side="+side+" phase="+phase+" keys="+saved.keys.Count+" seconds="+saved.Duration+" root="+distance+" maxReachError="+error+" maxFootStep="+step+" editable=true");
        }
        static void CaptureSoles()
        {
            var feet=new[]{Bone(HumanBodyBones.LeftFoot),Bone(HumanBodyBones.RightFoot)};
            foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var row=new SoleMesh{skin=skin};skin.BakeMesh(row.mesh);row.mesh.GetVertices(row.vertices);
                for(int v=0;v<row.vertices.Count;v++)
                {
                    var world=skin.transform.TransformPoint(row.vertices[v]);if(world.y>Mathf.Max(feet[0].position.y,feet[1].position.y)+.12f)continue;
                    int i=(world-feet[0].position).sqrMagnitude<(world-feet[1].position).sqrMagnitude?0:1;
                    row.indices[i].Add(v);soleFloor[i]=Mathf.Min(soleFloor[i],world.y);
                }
                soles.Add(row);
            }
            if(soleFloor[0]>1||soleFloor[1]>1)throw new InvalidOperationException("Foot mesh reference unavailable");
        }
        static void CorrectSoles(int frame,Vector3[,] knee)
        {
            // Stable vertex membership comes from the neutral mesh, then the actual skinned
            // vertices (including toe articulation) provide the floor constraint. No bone scaling.
            for(int pass=0;pass<2;pass++)
            {
                var low=new[]{100f,100f};
                foreach(var row in soles){row.skin.BakeMesh(row.mesh);row.mesh.GetVertices(row.vertices);for(int i=0;i<2;i++)foreach(int v in row.indices[i])low[i]=Mathf.Min(low[i],row.skin.transform.TransformPoint(row.vertices[v]).y);}
                bool corrected=false;
                for(int i=0;i<2;i++)
                {
                    float lift=soleFloor[i]-low[i];if(lift<=.001f)continue;if(lift>.07f)throw new InvalidOperationException("Excessive sole correction: "+lift);
                    var a=Bone(i==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);var b=Bone(i==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);var c=Bone(i==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);var rotation=c.rotation;
                    AnimationLabContacts.Solve(a,b,c,c.position+Vector3.up*lift,knee[i,frame],1);c.rotation=rotation;corrected=true;
                }
                if(!corrected)break;
            }
        }
        static void ReleaseSoles(){foreach(var row in soles)UnityEngine.Object.DestroyImmediate(row.mesh);soles.Clear();}
        static void RelaxFingers(bool left,float blend)
        {
            var hand=Bone(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);var middle=Bone(left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal);var index=Bone(left?HumanBodyBones.LeftIndexProximal:HumanBodyBones.RightIndexProximal);var little=Bone(left?HumanBodyBones.LeftLittleProximal:HumanBodyBones.RightLittleProximal);
            Vector3 along=(middle.position-hand.position).normalized;Vector3 normal=Vector3.Cross(index.position-little.position,along).normalized*(left?-1:1);int fingerIndex=0;
            foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Little"})
            {
                int segmentIndex=0;
                foreach(string segment in new[]{"Proximal","Intermediate","Distal"})
                {
                    var h=(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),(left?"Left":"Right")+finger+segment);var bone=Bone(h);var rest=data.rig.transform.Find(template.humanPaths[(int)h]);bone.localRotation=Quaternion.Slerp(bone.localRotation,rest.localRotation,blend);
                    if(bone.childCount>0){var axis=bone.InverseTransformDirection(Vector3.Cross((bone.GetChild(0).position-bone.position).normalized,normal)).normalized;bone.localRotation*=Quaternion.AngleAxis((segmentIndex==0?10+fingerIndex*2:segmentIndex==1?15+fingerIndex*2:9)*blend,axis);}
                    segmentIndex++;
                }
                fingerIndex++;
            }
        }
    }
}
#endif


#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Motion=SubwayCarry.Gameplay3D.AnimationLab.AnimationLabAuthoredMotion;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // CC0 Generic Walk + directly keyed braking/away placements. No mocap/force simulation claim.
    public static class AnimationLabReactionAuthoring
    {
        public static void BuildAll(){AnimationLabPhoneBuild.Guard();try{for(int kind=8;kind<=11;kind++)Build(kind,false);Debug.Log("MOVING_AVOID_ALL_BUILD_OK kinds=4 gaitVariants=4");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}}
        public static void BuildFrontLeft()=>Build(8);
        public static void BuildFrontRight()=>Build(9);
        public static void BuildRightPass()=>Build(10);
        public static void BuildLeftPass()=>Build(11);
        static float Ease(float x)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(x));
        sealed class Sole
        {
            public SkinnedMeshRenderer skin;public Mesh mesh=new Mesh();public System.Collections.Generic.List<Vector3> vertices=new System.Collections.Generic.List<Vector3>();
            public System.Collections.Generic.List<int>[] indices={new System.Collections.Generic.List<int>(),new System.Collections.Generic.List<int>()};
        }
        static void Build(int kind,bool finish=true)
        {
            AnimationLabPhoneBuild.Guard();
            try
            {
                EditorSceneManager.OpenScene(AnimationLabMotionAuthoring.ScenePath);
                var d=AssetDatabase.LoadAssetAtPath<AnimationLabPhoneMotion>(AnimationLabPhoneBuild.BindingPath);var m=d.authored;
                var rig=AnimationLabMotionAuthoring.CreateRig(d.rig);AnimationLabMotionAuthoring.StripAnimators(rig);var bones=AnimationLabMotionAuthoring.Bind(m,rig);
                Transform B(HumanBodyBones h)=>rig.transform.Find(m.humanPaths[(int)h]);
                var reference=new Motion.Pose{root=m.bindPositions[0],hips=m.bindPositions[m.hipsIndex],rotations=m.bindRotations};
                void Walk(float phase){AnimationLabMotionAuthoring.Apply(m,bones,reference);d.walk.SampleAnimation(rig.transform.Find(d.animatorPath).gameObject,Mathf.Repeat(phase,1)*d.walk.length);}
                AnimationLabMotionAuthoring.Apply(m,bones,reference);d.idle.SampleAnimation(rig.transform.Find(d.animatorPath).gameObject,0);var idle=AnimationLabMotionAuthoring.Capture(m,rig,0);
                var foot=new[]{B(HumanBodyBones.LeftFoot),B(HumanBodyBones.RightFoot)};var toe=new[]{B(HumanBodyBones.LeftToes),B(HumanBodyBones.RightToes)};
                var flatRotation=new[]{foot[0].rotation,foot[1].rotation};var toeLocal=new[]{toe[0].localRotation,toe[1].localRotation};var flatY=new[]{foot[0].position.y,foot[1].position.y};var tipLocal=new[]{foot[0].InverseTransformPoint(toe[0].position),foot[1].InverseTransformPoint(toe[1].position)};
                float floor=100;var soles=new System.Collections.Generic.List<Sole>();
                foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>()){var row=new Sole{skin=skin};skin.BakeMesh(row.mesh);row.mesh.GetVertices(row.vertices);for(int v=0;v<row.vertices.Count;v++){var w=skin.transform.TransformPoint(row.vertices[v]);floor=Mathf.Min(floor,w.y);if(w.y>Mathf.Max(flatY[0],flatY[1])+.12f)continue;int leg=(w-foot[0].position).sqrMagnitude<(w-foot[1].position).sqrMagnitude?0:1;row.indices[leg].Add(v);}soles.Add(row);}
                bool front=kind<10,left=kind==8||kind==10;float sign=left?-1:1,duration=front?1.4f:1.2f,lateral=front?.62f:.44f;
                int frames=Mathf.RoundToInt(duration*30);var roots=new Vector3[frames+1];float z=0,previous=d.referenceSpeed;
                for(int f=0;f<=frames;f++){float t=f/30f,u=t/duration,v=Mathf.Lerp(d.referenceSpeed,front?.24f:.52f,Ease(t/(front?.22f:.18f)));v=Mathf.Lerp(v,d.referenceSpeed,Ease((u-.32f)/.68f));if(f>0)z+=(previous+v)/60;previous=v;roots[f]=new Vector3(sign*lateral*Ease((u-.13f)/.82f),0,z);}
                Vector3 Root(float time){float f=Mathf.Clamp(time*30,0,frames);int a=(int)f;return Vector3.Lerp(roots[a],roots[Mathf.Min(frames,a+1)],f-a);}
                for(int pi=0;pi<4;pi++)
                {
                    float gait=pi*.25f;Walk(gait);var initial=AnimationLabMotionAuthoring.Capture(m,rig,0);var firstFeet=new[]{foot[0].position,foot[1].position};var firstRotation=new[]{foot[0].rotation,foot[1].rotation};int first=firstFeet[0].y>firstFeet[1].y?0:1,lead=left?0:1;bool catchFirst=first!=lead;
                    float[] land=catchFirst?new[]{.16f,.48f,.78f,1.08f,1.4f}:new[]{.28f,.58f,.88f,1.18f,1.4f};for(int n=0;n<5;n++)land[n]*=duration/1.4f;
                    int stance=1-first;float wantedZ=Root(land[3]).z+.10f-roots[frames].z,endPhase=0,bestPhaseCost=100;
                    for(int c=0;c<64;c++){Walk(c/64f);var fp=foot[stance].position;if(fp.y>flatY[stance]+.025f)continue;float cost=Mathf.Abs(fp.z-wantedZ)+Mathf.Abs(fp.y-flatY[stance])*3;if(cost<bestPhaseCost){bestPhaseCost=cost;endPhase=c/64f;}}
                    Walk(endPhase);var endPose=AnimationLabMotionAuthoring.Capture(m,rig,0);var endFeet=new[]{foot[0].position,foot[1].position};var endRotation=new[]{foot[0].rotation,foot[1].rotation};
                    var targets=new Vector3[5];var rotations=new Quaternion[5];var previousFoot=(Vector3[])firstFeet.Clone();
                    for(int n=0;n<5;n++){int leg=n%2==0?first:1-first;var root=Root(land[n]);if(n>=3){targets[n]=roots[frames]+endFeet[leg];rotations[n]=endRotation[leg];}else{float away=leg==lead?(front?.16f:.08f):0;targets[n]=root+new Vector3((leg==0?-.105f:.105f)+sign*away,flatY[leg],catchFirst&&n==0?.055f:.10f);if(leg==0)targets[n].x=Mathf.Min(targets[n].x,previousFoot[1].x-.15f);else targets[n].x=Mathf.Max(targets[n].x,previousFoot[0].x+.15f);rotations[n]=Quaternion.AngleAxis(sign*(front?10:6),Vector3.up)*flatRotation[leg];}previousFoot[leg]=targets[n];}
                    string name="돌발회피_"+(kind==8?"정면접근_왼쪽비껴가기":kind==9?"정면접근_오른쪽비껴가기":kind==10?"오른쪽통과_왼쪽간격내기":"왼쪽통과_오른쪽간격내기")+(pi==0?"_왼발지지":pi==1?"_왼발지지_후반":pi==2?"_오른발지지":"_오른발지지_후반");
                    var entry=d.reactions.FirstOrDefault(e=>(int)e.kind==kind&&Mathf.Abs(e.gaitPhase-gait)<.01f);var saved=entry?.authored;var draft=UnityEngine.Object.Instantiate(saved!=null?saved:m);draft.hideFlags=HideFlags.HideAndDontSave;if(saved==null){draft.id=Guid.NewGuid().ToString("N");draft.author="AI";draft.clipName=name;draft.generatedClip=null;draft.generatedFbx=null;}draft.keys.Clear();draft.drafts.Clear();draft.startingClip=d.walk;draft.lastFrame=frames;draft.sampleRate=30;draft.loop=false;
                    float maxReach=0,maxKnee=0,maxSole=0,hipLow=100,hipHigh=-100;int worstFrame=0,worstLeg=0,hipFrame=0;Vector3 lowL=default,lowR=default,lowRoot=default;
                    for(int f=0;f<=frames;f++)
                    {
                        float t=f/30f,u=t/duration;Walk(gait+roots[f].z/d.referenceSpeed/d.walk.length);float release=Ease((u-.78f)/.22f);for(int j=1;j<bones.Length;j++)bones[j].localRotation=Quaternion.Slerp(bones[j].localRotation,endPose.rotations[j],release);
                        var p=(Vector3[])firstFeet.Clone();var r=(Quaternion[])firstRotation.Clone();var loaded=new[]{true,true};
                        for(int n=0;n<5;n++){int leg=n%2==0?first:1-first;float begin=n==0?0:land[n-1]+.025f*duration/1.4f;if(t>=land[n]){p[leg]=targets[n];r[leg]=rotations[n];continue;}if(t<begin)break;float s=Ease((t-begin)/(land[n]-begin));p[leg]=Vector3.Lerp(p[leg],targets[n],s)+Vector3.up*(.040f*16*s*s*(1-s)*(1-s));r[leg]=Quaternion.Slerp(r[leg],rotations[n],s);loaded[leg]=false;break;}
                        var root=roots[f];float yaw=sign*(front?12:7)*Mathf.Sin(Mathf.PI*u);rig.transform.SetPositionAndRotation(root,Quaternion.Euler(0,yaw,0));var hips=B(HumanBodyBones.Hips);var h=hips.position;float load=loaded[0]&&!loaded[1]?0:loaded[1]&&!loaded[0]?1:.5f;var support=Vector3.Lerp(p[0],p[1],load);float shift=Ease(u/.18f)*(1-release);hips.position=new Vector3(h.x+Mathf.Clamp(support.x-h.x,-.065f,.065f)*.55f*shift,h.y,h.z+Mathf.Clamp(support.z-h.z,-.055f,.055f)*.5f*shift);
                        float ceiling=10;for(int leg=0;leg<2;leg++){var a=B(leg==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);var b=B(leg==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);float l1=Vector3.Distance(a.position,b.position),l2=Vector3.Distance(b.position,foot[leg].position);float length=l1+l2-.010f;float horizontal=Vector3.ProjectOnPlane(p[leg]-a.position,Vector3.up).sqrMagnitude;ceiling=Mathf.Min(ceiling,p[leg].y+Mathf.Sqrt(Mathf.Max(0,length*length-horizontal))-(a.position.y-hips.position.y));}hips.position=new Vector3(hips.position.x,Mathf.Min(hips.position.y,ceiling),hips.position.z);
                        float recoil=Ease(t/.12f)*(1-Ease((t-.18f)/.42f));foreach(var part in new[]{HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.UpperChest}){var b=B(part);b.rotation=Quaternion.AngleAxis(-(front?1.0f:.55f)*recoil,rig.transform.right)*b.rotation;if(!front)b.rotation=Quaternion.AngleAxis(-sign*.8f*recoil,rig.transform.forward)*b.rotation;}
                        B(HumanBodyBones.Neck).rotation=Quaternion.AngleAxis(-1.2f*recoil,rig.transform.right)*B(HumanBodyBones.Neck).rotation;B(HumanBodyBones.Head).rotation=Quaternion.AngleAxis(-.7f*recoil,rig.transform.right)*B(HumanBodyBones.Head).rotation;
                        // Reduce ordinary arm swing without raising/tucking either arm. User hand keys are untouched.
                        for(int j=1;j<bones.Length;j++)if(m.paths[j].Contains("upper_arm")||m.paths[j].Contains("forearm"))bones[j].localRotation=Quaternion.Slerp(bones[j].localRotation,idle.rotations[j],.30f*recoil);
                        for(int leg=0;leg<2;leg++){var a=B(leg==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);var b=B(leg==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);AnimationLabContacts.Solve(a,b,foot[leg],p[leg],rig.transform.forward,1);foot[leg].rotation=r[leg];float e=Vector3.Distance(foot[leg].position,p[leg]);if(e>maxReach){maxReach=e;worstFrame=f;worstLeg=leg;}maxKnee=Mathf.Max(maxKnee,180-Vector3.Angle(a.position-b.position,foot[leg].position-b.position));}
                        for(int pass=0;pass<2;pass++){var low=new[]{100f,100f};foreach(var row in soles){row.skin.BakeMesh(row.mesh);row.mesh.GetVertices(row.vertices);for(int leg=0;leg<2;leg++)foreach(int v in row.indices[leg])low[leg]=Mathf.Min(low[leg],row.skin.transform.TransformPoint(row.vertices[v]).y);}for(int leg=0;leg<2;leg++){float lift=Mathf.Max(0,floor-low[leg]);if(loaded[leg]&&p[leg].y<flatY[leg]+.018f)lift=floor-low[leg];maxSole=Mathf.Max(maxSole,Mathf.Abs(lift));if(Mathf.Abs(lift)>.045f)throw new InvalidOperationException("Reaction sole outside correction range frame="+f+" leg="+leg+" amount="+lift);var a=B(leg==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);var b=B(leg==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);var keep=foot[leg].rotation;AnimationLabContacts.Solve(a,b,foot[leg],foot[leg].position+Vector3.up*lift,rig.transform.forward,1);foot[leg].rotation=keep;}}
                        if(hips.position.y<hipLow){hipLow=hips.position.y;hipFrame=f;lowL=p[0];lowR=p[1];lowRoot=root;}hipHigh=Mathf.Max(hipHigh,hips.position.y);var pose=AnimationLabMotionAuthoring.Capture(m,rig,f);pose.ease=0;draft.StoreKey(pose);
                    }
                    Debug.Log("MOVING_AVOID_SOURCE kind="+kind+" gait="+gait+" first="+first+" maxReach="+maxReach+" worstFrame="+worstFrame+" worstLeg="+worstLeg+" kneeMax="+maxKnee+" hips="+hipLow+".."+hipHigh+" maxSoleCorrection="+maxSole+" root="+roots[frames]+" endWalkPhase="+endPhase);
                    if(maxReach>.008f||hipLow<.78f||maxKnee>100){Debug.Log("REACTION_LOW_HIP frame="+hipFrame+" root="+lowRoot.ToString("F4")+" L="+lowL.ToString("F4")+" R="+lowR.ToString("F4")+" firstFeet="+firstFeet[0]+" / "+firstFeet[1]+" targets="+string.Join(" | ",targets.Select(v=>v.ToString("F4"))));System.IO.File.WriteAllText(System.IO.Path.GetFullPath("../../../MotionMatching/PhonePassenger/Reaction/reaction-candidate.json"),JsonUtility.ToJson(draft));throw new InvalidOperationException("Reaction source rejected; not published");}
                    if(entry==null){entry=new AnimationLabPhoneMotion.ReactionClip{kind=(AnimationLabReactionKind)kind,gaitPhase=gait,sourceId=draft.id};d.reactions=d.reactions.Concat(new[]{entry}).ToArray();}entry.referenceSpeed=d.referenceSpeed;entry.endWalkPhase=endPhase;entry.provenance="Quaternius CC0 Generic Walk + authored continuous braking, supported away steps and small recoil; no muscle dynamics";
                    AnimationLabMotionAuthoring.SaveBundle(draft,ref saved,saved!=null?AnimationLabMotionAuthoring.Revision(saved):null);entry.authored=saved;entry.clip=saved.generatedClip;EditorUtility.SetDirty(d);AssetDatabase.SaveAssetIfDirty(d);UnityEngine.Object.DestroyImmediate(draft);
                }
                foreach(var row in soles)UnityEngine.Object.DestroyImmediate(row.mesh);UnityEngine.Object.DestroyImmediate(rig);Debug.Log("MOVING_AVOID_BUILD_OK kind="+kind+" gaitVariants=4");if(finish)EditorApplication.Exit(0);
            }
            catch(Exception e){if(!finish)throw;Debug.LogException(e);EditorApplication.Exit(2);}
        }
    }
}
#endif

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
    // Measures the existing licensed clips before deciding which performances belong together.
    [InitializeOnLoad] public static class AnimationLabPostureChecks
    {
        const string ReloadKey="AnimationLab.QueryReloadProbe";
        static bool reloadPending;
        static AnimationLabPostureChecks(){EditorApplication.update+=ReloadTick;}
        public static void RunReloadProbe()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Isolated QA only");
            SessionState.SetInt(ReloadKey,1);
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(AnimationLabBuilder.Root+"/Normal.unity");EditorApplication.EnterPlaymode();
        }
        static void ReloadTick()
        {
            int stage=SessionState.GetInt(ReloadKey,0);if(stage==0||!EditorApplication.isPlaying||Time.frameCount<16)return;
            var s=UnityEngine.Object.FindFirstObjectByType<AnimationLabScenario>();if(s==null||s.Actor==null||s.Current==null)return;
            if(stage==1)
            {
                Debug.Log("QUERY_RELOAD_BEFORE family="+s.Search.postureFamily+" refine="+s.Search.refinePhase+" rows="+s.Data.entries.Count(e=>e.postureFamily==1)+" clip="+s.Current.label);
                SessionState.SetInt(ReloadKey,2);reloadPending=true;EditorUtility.RequestScriptReload();
            }
            else
            {
                if(reloadPending)return;
                bool bound=s.PoseJoint(0)!=null;
                Debug.Log("QUERY_RELOAD_AFTER family="+s.Search.postureFamily+" refine="+s.Search.refinePhase+" rows="+s.Data.entries.Count(e=>e.postureFamily==1)+" clip="+s.Current.label+" ready="+s.Actor.Ready+" poseBound="+bound);
                bool passed=!Environment.GetCommandLineArgs().Contains("-expectQueryRestored")||s.Search.postureFamily==1&&s.Search.refinePhase&&bound;
                SessionState.SetInt(ReloadKey,0);EditorApplication.Exit(passed?0:2);
            }
        }
        [Serializable] public class Pose
        {
            public string clip,rig;public float time,leg,meshTop,meshBottom,hipY,chestY,upperChestY,shoulderY,headY,lean,neckLean,leftKnee,rightKnee,leftCollar,rightCollar;
            public Vector3 rootScale,visualScale;public float[] lengths;
        }
        [Serializable] public class Result {public string error="";public List<Pose> poses=new List<Pose>();}
        public static void Run()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Existing isolated QA only");
            var result=new Result();
            try
            {
                string root=AnimationLabBuilder.Root;
                var free=Resources.Load<AnimationLabFreeWalkData>("FreeWalkReview");var motions=Resources.Load<AnimationLabMotionSet>("ReviewMotions");
                var clips=new List<(string,AnimationClip)>{("Kubold Idle",free.idle),("Kubold Start",free.start),("Kubold Walk",free.walk),("Kubold Stop L",free.stopLeft),("Kubold Stop R",free.stopRight)};
                foreach(var entry in motions.entries.Where(e=>e.label=="Start Walking"||e.label=="Walking"||e.label=="Walking Left Turn"||e.label=="Right Turn (1)"))clips.Add((entry.label,entry.clip));
                foreach(var item in clips)
                {
                    var target=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(root+"/Prefabs/Character.prefab"));
                    foreach(var c in target.GetComponentsInChildren<MonoBehaviour>())c.enabled=false;
                    var animator=target.GetComponentInChildren<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    using(var player=new AnimationLabFreeWalkPlayer(animator,free))
                    {
                        player.SelectExternal(item.Item2,0,0);
                        for(int i=0;i<=60;i++){float t=item.Item2.length*i/60;player.EvaluateExternal(t,0,1,target.transform);result.poses.Add(Read(animator,item.Item1,"target raw",t,true));}
                    }
                    UnityEngine.Object.DestroyImmediate(target);
                    var model=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(item.Item2));if(model==null)continue;
                    var source=UnityEngine.Object.Instantiate(model);animator=source.GetComponentInChildren<Animator>();
                    if(animator==null||!animator.isHuman){UnityEngine.Object.DestroyImmediate(source);continue;}
                    animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
                    var graph=PlayableGraph.Create("Posture source content audit");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var clip=AnimationClipPlayable.Create(graph,item.Item2);clip.SetApplyFootIK(false);clip.SetApplyPlayableIK(false);AnimationPlayableOutput.Create(graph,"Source",animator).SetSourcePlayable(clip);graph.Play();
                    for(int i=0;i<=60;i++){float t=item.Item2.length*i/60;clip.SetTime(t);graph.Evaluate(0);result.poses.Add(Read(animator,item.Item1,"source",t,false));}
                    graph.Destroy();UnityEngine.Object.DestroyImmediate(source);
                }
            }
            catch(Exception e){result.error=e.ToString();Debug.LogException(e);}
            string output=Path.Combine(Directory.GetParent(Application.dataPath).Parent.Parent.Parent.FullName,"MotionMatching/posture-source.json");
            File.WriteAllText(output,JsonUtility.ToJson(result));Debug.Log("POSTURE_SOURCE "+result.error);EditorApplication.Exit(result.error.Length==0?0:2);
        }
        static Pose Read(Animator a,string clip,string rig,float time,bool mesh)
        {
            Transform B(HumanBodyBones b)=>a.GetBoneTransform(b);
            var hip=B(HumanBodyBones.Hips);var chest=B(HumanBodyBones.Chest);var upper=B(HumanBodyBones.UpperChest);if(upper==null)upper=chest;
            var lu=B(HumanBodyBones.LeftUpperLeg);var ru=B(HumanBodyBones.RightUpperLeg);var lk=B(HumanBodyBones.LeftLowerLeg);var rk=B(HumanBodyBones.RightLowerLeg);var lf=B(HumanBodyBones.LeftFoot);var rf=B(HumanBodyBones.RightFoot);
            var la=B(HumanBodyBones.LeftUpperArm);var ra=B(HumanBodyBones.RightUpperArm);var head=B(HumanBodyBones.Head);var neck=B(HumanBodyBones.Neck);
            var forward=Vector3.Cross(Vector3.ProjectOnPlane(ru.position-lu.position,Vector3.up),Vector3.up).normalized;
            float Lean(Vector3 v)=>Mathf.Atan2(Vector3.Dot(v,forward),v.y)*Mathf.Rad2Deg;
            float Knee(Transform h,Transform k,Transform f)=>180-Vector3.Angle(h.position-k.position,f.position-k.position);
            float Collar(Transform shoulder,Transform arm){var v=arm.position-shoulder.position;return Mathf.Atan2(v.y,Vector3.ProjectOnPlane(v,Vector3.up).magnitude)*Mathf.Rad2Deg;}
            float y=a.transform.position.y,top=-999,bottom=999;
            if(mesh)foreach(var skin in a.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked=new Mesh();skin.BakeMesh(baked);foreach(var v in baked.vertices){float height=skin.transform.TransformPoint(v).y-y;top=Mathf.Max(top,height);bottom=Mathf.Min(bottom,height);}UnityEngine.Object.DestroyImmediate(baked);
            }
            return new Pose{clip=clip,rig=rig,time=time,leg=Vector3.Distance(lu.position,lk.position)+Vector3.Distance(lk.position,lf.position),meshTop=top,meshBottom=bottom,hipY=hip.position.y-y,chestY=chest.position.y-y,upperChestY=upper.position.y-y,headY=head.position.y-y,shoulderY=(la.position.y+ra.position.y)*.5f-y,lean=Lean(chest.position-hip.position),neckLean=Lean(head.position-neck.position),leftKnee=Knee(lu,lk,lf),rightKnee=Knee(ru,rk,rf),leftCollar=Collar(B(HumanBodyBones.LeftShoulder),la),rightCollar=Collar(B(HumanBodyBones.RightShoulder),ra),rootScale=a.transform.root.lossyScale,visualScale=a.transform.lossyScale,lengths=new[]{Vector3.Distance(lu.position,lk.position),Vector3.Distance(lk.position,lf.position),Vector3.Distance(la.position,B(HumanBodyBones.LeftLowerArm).position),Vector3.Distance(hip.position,chest.position)}};
        }
        public static void BuildCompatibleLocomotion()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Isolated QA only");
            var set=Resources.Load<AnimationLabMotionSet>("ReviewMotions");var free=Resources.Load<AnimationLabFreeWalkData>("FreeWalkReview");
            // Chosen only after source/target measurements: this existing family's target mesh
            // height/torso/clavicle ranges agree across Idle/Start/Walk/Stop. Preserve every source.
            var rows=set.entries.Where(e=>e.postureFamily!=1||e.clip==free.idle).ToList();
            AnimationLabMotionBuild.AddNormalFamily(rows,free);
            // Keep the original Idle row/reference and every Mixamo entry for other review modes.
            if(!rows.Any(e=>e.clip==free.idle)){var idle=AnimationLabMotionBuild.Sample(free.idle,AssetDatabase.GetAssetPath(free.idle),null);idle.label="Idle (Kubold LITE)";idle.uses=AnimationLabMotionSet.Use.Idle;idle.loop=idle.usable=true;idle.postureFamily=1;rows.Add(idle);}
            set.entries=rows.ToArray();EditorUtility.SetDirty(set);AssetDatabase.SaveAssetIfDirty(set);
            Debug.Log("NORMAL_COMPATIBLE_FAMILY_READY "+rows.Count);EditorApplication.Exit(0);
        }
        public static void VerifyReferences()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Isolated QA only");
            var set=Resources.Load<AnimationLabMotionSet>("ReviewMotions");var free=Resources.Load<AnimationLabFreeWalkData>("FreeWalkReview");
            var legacy=new AnimationLabMotionSearch{intent=AnimationLabMotionSearch.Intent.Travel};
            var normal=new AnimationLabMotionSearch{postureFamily=1,refinePhase=true};int compatible=0;
            foreach(var row in set.entries.Where(e=>e.postureFamily==1))
            {
                if(row.clip==null||row.frames==null||row.frames.Length<3)throw new InvalidOperationException("Compatible row reference/data missing");
                compatible++;
                bool idle=(row.uses&AnimationLabMotionSet.Use.Idle)!=0,stop=(row.uses&AnimationLabMotionSet.Use.Stop)!=0;
                normal.intent=idle?AnimationLabMotionSearch.Intent.Rest:stop?AnimationLabMotionSearch.Intent.Brake:AnimationLabMotionSearch.Intent.Travel;
                if(!normal.Allowed(row))throw new InvalidOperationException("Normal cannot select its declared compatible action");
                if(!idle&&legacy.Allowed(row))throw new InvalidOperationException("Normal trial changed Situations candidate set");
            }
            if(compatible!=5||!set.entries.Any(e=>e.postureFamily==1&&e.clip==free.idle))throw new InvalidOperationException("Missing compatible family member");
            Debug.Log("POSTURE_REFERENCES_OK compatible=5 legacyPoolPreserved=true sourceAvatar="+free.avatar.name);EditorApplication.Exit(0);
        }
    }
}
#endif

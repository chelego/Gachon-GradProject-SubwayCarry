using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Independent authored motion source. Existing Idle/Walk FK profile indices never change.
    public sealed class AnimationLabAuthoredMotion : ScriptableObject
    {
        public const string RigVersion="SubwayCarry-Dummy-01";
        public string id,clipName="WeightShift",author="",rigVersion=RigVersion,rigSignature;
        public GameObject sourceRig;
        public AnimationClip startingClip;
        public AnimationClip generatedClip;
        public GameObject generatedFbx;
        public int sampleRate=30,lastFrame=60,hipsIndex=-1;
        public string[] paths=Array.Empty<string>();
        public string[] humanPaths=Array.Empty<string>();
        public Vector3[] bindPositions=Array.Empty<Vector3>(),bindScales=Array.Empty<Vector3>();
        public Quaternion[] bindRotations=Array.Empty<Quaternion>();
        public List<Pose> keys=new List<Pose>(),drafts=new List<Pose>();
        [Serializable] public sealed class Pose
        {
            public int frame;
            [Range(0,1)] public float ease=.5f;
            public Vector3 root,hips;
            public Quaternion[] rotations=Array.Empty<Quaternion>();
            public Pose Copy(int at)=>new Pose{frame=at,ease=ease,root=root,hips=hips,rotations=(Quaternion[])rotations.Clone()};
        }
        public float Duration=>lastFrame/(float)Mathf.Max(1,sampleRate);
        public Pose Exact(List<Pose> rows,int frame)=>rows.Find(p=>p.frame==frame);
        public Pose Evaluate(float frame,bool includeDraft=true)
        {
            frame=Mathf.Clamp(frame,0,lastFrame);
            int integer=Mathf.RoundToInt(frame);
            if(includeDraft&&Mathf.Abs(frame-integer)<.00001f)
            {var draft=Exact(drafts,integer);if(draft!=null)return draft.Copy(integer);}
            if(keys.Count==0)return new Pose{frame=integer,root=bindPositions[0],hips=bindPositions[hipsIndex],rotations=(Quaternion[])bindRotations.Clone()};
            Pose a=null,b=null;
            foreach(var key in keys){if(key.frame<=frame&&(a==null||key.frame>a.frame))a=key;if(key.frame>=frame&&(b==null||key.frame<b.frame))b=key;}
            if(a==null)return b.Copy(integer);if(b==null||a.frame==b.frame)return a.Copy(integer);
            float t=(frame-a.frame)/(b.frame-a.frame);t=Mathf.Lerp(t,t*t*(3-2*t),a.ease);
            var result=new Pose{frame=integer,ease=a.ease,root=Vector3.Lerp(a.root,b.root,t),hips=Vector3.Lerp(a.hips,b.hips,t),rotations=new Quaternion[paths.Length]};
            for(int i=0;i<paths.Length;i++)result.rotations[i]=Quaternion.Slerp(a.rotations[i],SameHemisphere(a.rotations[i],b.rotations[i]),t);
            return result;
        }
        public static Quaternion SameHemisphere(Quaternion a,Quaternion b)=>Quaternion.Dot(a,b)<0?new Quaternion(-b.x,-b.y,-b.z,-b.w):b;
        public void StoreDraft(Pose pose){Put(drafts,pose.Copy(Mathf.Clamp(pose.frame,0,lastFrame)));}
        public void StoreKey(Pose pose){Put(keys,pose.Copy(Mathf.Clamp(pose.frame,0,lastFrame)));drafts.RemoveAll(p=>p.frame==pose.frame);}
        static void Put(List<Pose> rows,Pose pose){rows.RemoveAll(p=>p.frame==pose.frame);rows.Add(pose);rows.Sort((a,b)=>a.frame.CompareTo(b.frame));}
        public bool Validate(out string reason)
        {
            if(sourceRig==null||paths.Length==0||hipsIndex<0||hipsIndex>=paths.Length||sampleRate<1||sampleRate>120||lastFrame<1||lastFrame>18000||bindPositions.Length!=paths.Length||bindScales.Length!=paths.Length||bindRotations.Length!=paths.Length)
            {reason="제작 골격/프레임 범위가 유효하지 않습니다.";return false;}
            var seen=new HashSet<string>();foreach(string path in paths)if(!seen.Add(path)){reason="중복 본 경로";return false;}
            foreach(var rows in new[]{keys,drafts})
            {
                var frames=new HashSet<int>();foreach(var pose in rows)
                {
                    if(pose.frame<0||pose.frame>lastFrame||!frames.Add(pose.frame)||pose.rotations.Length!=paths.Length){reason="키/초안 프레임 또는 본 개수 불일치";return false;}
                    foreach(var q in pose.rotations)if(!float.IsFinite(q.x+q.y+q.z+q.w)||Mathf.Abs(Quaternion.Dot(q,q)-1)>.005f){reason="유효하지 않은 관절 회전";return false;}
                }
            }
            reason="";return true;
        }
    }
}

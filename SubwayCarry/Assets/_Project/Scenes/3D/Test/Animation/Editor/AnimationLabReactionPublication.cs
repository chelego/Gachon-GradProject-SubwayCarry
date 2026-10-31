#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    public static class AnimationLabReactionPublication
    {
        public static void Publish(AnimationLabPhoneMotion binding,AnimationLabPhoneMotion.ReactionClip entry,AnimationLabAuthoredMotion source,AnimationClip clip)
        {
            var rig=AnimationLabMotionAuthoring.CreateRig(binding.rig);AnimationLabMotionAuthoring.StripAnimators(rig);
            try
            {
                var bones=AnimationLabMotionAuthoring.Bind(source,rig);int n=source.lastFrame+1;entry.root=new Vector3[n];entry.rootRotation=new Quaternion[n];entry.leftContact=new byte[n];entry.rightContact=new byte[n];
                var feet=new Transform[2];var toes=new Transform[2];var points=new Vector3[2,n];var tips=new Vector3[2,n];var floors=new float[2];var toeFloors=new float[2];
                binding.idle.SampleAnimation(rig.transform.Find(binding.animatorPath).gameObject,0);
                for(int j=0;j<2;j++){feet[j]=rig.transform.Find(source.humanPaths[(int)(j==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot)]);toes[j]=rig.transform.Find(source.humanPaths[(int)(j==0?HumanBodyBones.LeftToes:HumanBodyBones.RightToes)]);floors[j]=feet[j].position.y;toeFloors[j]=toes[j].position.y;}
                for(int f=0;f<n;f++){var pose=source.Evaluate(f,false);AnimationLabMotionAuthoring.Apply(source,bones,pose);entry.root[f]=pose.root;entry.rootRotation[f]=pose.rotations[0];for(int j=0;j<2;j++){points[j,f]=feet[j].position;tips[j,f]=toes[j].position;}}
                for(int j=0;j<2;j++)for(int f=0;f<n;f++)
                {
                    int a=Mathf.Max(0,f-1),b=Mathf.Min(n-1,f+1);float dt=(b-a)/(float)source.sampleRate;
                    float speed=Vector3.Distance(points[j,a],points[j,b])/Mathf.Max(.001f,dt),toeSpeed=Vector3.Distance(tips[j,a],tips[j,b])/Mathf.Max(.001f,dt);
                    bool low=points[j,f].y<floors[j]+.022f,lowToe=tips[j,f].y<toeFloors[j]+.018f;
                    byte support=low&&speed<.27f?(byte)1:lowToe&&toeSpeed<.27f?(byte)2:(byte)0;
                    if(j==0)entry.leftContact[f]=support;else entry.rightContact[f]=support;
                }
                if(entry.kind==AnimationLabReactionKind.Stop||entry.kind==AnimationLabReactionKind.LeftDouble||entry.kind==AnimationLabReactionKind.RightDouble||(int)entry.kind>=8)
                {
                    var end=new Vector3[2];for(int j=0;j<2;j++)end[j]=rig.transform.InverseTransformPoint(feet[j].position);float best=float.PositiveInfinity;
                    // Offline nearest support pose for a short restart blend, not a learned/motion-matching system.
                    for(int f=0;f<64;f++)
                    {
                        AnimationLabMotionAuthoring.Apply(source,bones,source.keys[0]);binding.walk.SampleAnimation(rig.transform.Find(binding.animatorPath).gameObject,binding.walk.length*f/64f);float cost=0;
                        for(int j=0;j<2;j++){var d=rig.transform.InverseTransformPoint(feet[j].position)-end[j];d.y*=2;cost+=d.sqrMagnitude;}
                        if(cost<best){best=cost;entry.endWalkPhase=f/64f;}
                    }
                }
                entry.authored=source;entry.clip=clip;using(var hash=SHA256.Create())entry.revision=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(AssetDatabase.GetAssetPath(clip)))).Replace("-","").Substring(0,12);
            }
            finally{UnityEngine.Object.DestroyImmediate(rig);}
        }
    }
}
#endif

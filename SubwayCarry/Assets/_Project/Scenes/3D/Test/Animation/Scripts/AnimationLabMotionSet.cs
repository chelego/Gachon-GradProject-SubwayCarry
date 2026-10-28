using System;
using UnityEngine;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Shared review data. Selection is pose/trajectory based; production NPCs are unchanged.
    public sealed class AnimationLabMotionSet : ScriptableObject
    {
        [Flags] public enum Use { None=0,Idle=1,Forward=2,Start=4,Stop=8,MovingTurn=16,Pivot=32,Strafe=64,Back=128,Look=256,Elevated=512 }
        [Serializable] public struct Frame
        {
            public Vector3 root,leftFoot,rightFoot,leftKnee,rightKnee,hips,leftVelocity,rightVelocity;
            public float yaw,speed,yawRate,leftSupport,rightSupport,lookYaw,bodyYaw;
        }
        [Serializable] public sealed class Entry
        {
            public AnimationClip clip;
            public string label,source,sha256,notes;
            public Use uses;
            public bool loop,usable,estimatedContacts=true;
            public float sampleRate,duration,travel,netYaw,meanSpeed,peakSpeed,headYawRange,armUpFraction,retargetMean,retargetPeak,rawTravel,initialHeading;
            public bool mirrored;
            public int postureFamily;
            public Frame[] frames;
            public Frame At(float time)
            {
                if(frames==null||frames.Length==0)return default;
                float f=Mathf.Clamp(time*sampleRate,0,frames.Length-1);int i=(int)f,j=Mathf.Min(i+1,frames.Length-1);float t=f-i;
                var a=frames[i];var b=frames[j];
                return new Frame{root=Vector3.Lerp(a.root,b.root,t),yaw=Mathf.Lerp(a.yaw,b.yaw,t),speed=Mathf.Lerp(a.speed,b.speed,t),yawRate=Mathf.Lerp(a.yawRate,b.yawRate,t),leftFoot=Vector3.Lerp(a.leftFoot,b.leftFoot,t),rightFoot=Vector3.Lerp(a.rightFoot,b.rightFoot,t),leftKnee=Vector3.Lerp(a.leftKnee,b.leftKnee,t),rightKnee=Vector3.Lerp(a.rightKnee,b.rightKnee,t),hips=Vector3.Lerp(a.hips,b.hips,t),leftSupport=Mathf.Lerp(a.leftSupport,b.leftSupport,t),rightSupport=Mathf.Lerp(a.rightSupport,b.rightSupport,t),lookYaw=Mathf.Lerp(a.lookYaw,b.lookYaw,t),bodyYaw=Mathf.LerpAngle(a.bodyYaw,b.bodyYaw,t),leftVelocity=Vector3.Lerp(a.leftVelocity,b.leftVelocity,t),rightVelocity=Vector3.Lerp(a.rightVelocity,b.rightVelocity,t)};
            }
            public void Delta(float from,float to,out Vector3 position,out float yaw)
            {
                if(!loop){var a=At(from);var b=At(to);position=b.root-a.root;yaw=b.yaw-a.yaw;return;}
                int ca=Mathf.FloorToInt(from/duration),cb=Mathf.FloorToInt(to/duration);
                var x=At(Mathf.Repeat(from,duration));var y=At(Mathf.Repeat(to,duration));var first=At(0);var last=At(duration);
                position=y.root-x.root+(last.root-first.root)*(cb-ca);yaw=y.yaw-x.yaw+(last.yaw-first.yaw)*(cb-ca);
            }
        }
        public Entry[] entries=Array.Empty<Entry>();
        public string provenance="Existing Mixamo and licensed Kubold LITE clips; source clips and Avatar preserved. Normal uses a measured compatible posture family. Contact labels are estimates.";
    }
}

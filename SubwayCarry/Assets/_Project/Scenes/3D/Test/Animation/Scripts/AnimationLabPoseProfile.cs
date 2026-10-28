using System;
using UnityEngine;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // User-authored local FK deltas only. No source motion, bone translation or rig data.
    public sealed class AnimationLabPoseProfile : ScriptableObject
    {
        public enum Clip { Idle, Walk }
        public static readonly HumanBodyBones[] Bones = {
            HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck,
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand
        };
        public static readonly string[] Labels = {
            "가슴", "윗가슴", "목", "왼 어깨", "왼 위팔", "왼 팔꿈치", "왼 손목",
            "오른 어깨", "오른 위팔", "오른 팔꿈치", "오른 손목"
        };
        [Serializable]
        public sealed class Pose
        {
            public bool enabled = true;
            [Range(0, 1)] public float strength = 1;
            public Quaternion[] rotations = Array.Empty<Quaternion>();
            public Quaternion Get(int index)
            {
                if(index<0||rotations==null||index>=rotations.Length)return Quaternion.identity;
                var q=rotations[index];float sq=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
                return float.IsFinite(sq)&&sq>.0001f?q.normalized:Quaternion.identity;
            }
            public void Set(int index,Quaternion value)
            {
                if(index<0||index>=Bones.Length)return;
                if(rotations==null||rotations.Length!=Bones.Length)
                {
                    var next=new Quaternion[Bones.Length];for(int i=0;i<next.Length;i++)next[i]=Get(i);rotations=next;
                }
                rotations[index]=value.normalized;
            }
            public void Reset(){rotations=Array.Empty<Quaternion>();}
        }
        public Pose idle=new Pose(),walk=new Pose();
        public Pose For(Clip clip)=>clip==Clip.Idle?idle:walk;
    }
}

using System;
using UnityEngine;
using MxM;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    [CreateAssetMenu(menuName="SubwayCarry/Animation Lab Library")]
    public sealed class AnimationLabLibrary : ScriptableObject
    {
        public RuntimeAnimatorController controller;
        public Font font;
        public AnimationClip idle,walk,run,sitEnter,sitIdle,sitExit;
        public AnimationClip[] converted;
        public MxMAnimData matchingData;
        public float walkSpeed=.996f,runSpeed=2.5f;
        public float enterSeconds=.9f,exitSeconds=.9f;
        public AnimationCurve leftSupport,rightSupport,runLeftSupport,runRightSupport;
    }
}

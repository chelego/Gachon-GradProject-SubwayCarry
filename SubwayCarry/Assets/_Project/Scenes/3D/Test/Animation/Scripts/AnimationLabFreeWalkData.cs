using UnityEngine;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Local review data; no new package, motion database, or gameplay contract.
    public sealed class AnimationLabFreeWalkData : ScriptableObject
    {
        public Avatar avatar;
        public AnimationLabPoseProfile poseProfile;
        public AnimationClip idle, walk, start, stopLeft, stopRight;
        public GameObject sourceSkeleton;
        public AnimationClip sourceIdle,sourceWalk;
        public Vector3 sourceIdleOrigin,sourceIdleVelocity,sourceWalkOrigin,sourceWalkVelocity;
        public float speed;
        public Vector3 stopLeftL, stopLeftR, stopRightL, stopRightR;
        public string source = "Kubold Movement Animset Pro LITE v1.2 — official free sample";
        public bool Valid => avatar != null && avatar.isValid && avatar.isHuman && idle != null && walk != null && start != null && stopLeft != null && stopRight != null && speed > .4f && speed < 2.5f;
        public bool PoseValid=>Valid&&sourceSkeleton!=null&&sourceIdle!=null&&sourceWalk!=null&&!sourceIdle.humanMotion&&!sourceWalk.humanMotion;
    }
}

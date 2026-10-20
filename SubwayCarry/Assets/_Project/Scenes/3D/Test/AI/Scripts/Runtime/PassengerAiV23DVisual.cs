using UnityEngine;

namespace SubwayCarry.AI.V2
{
    [DisallowMultipleComponent]
    public sealed class PassengerAiV23DVisual : MonoBehaviour
    {
        [SerializeField] private PassengerAiV2Agent agent;
        [SerializeField] private Transform model;
        [SerializeField] private Animator animator;
        [SerializeField] private Vector3 seatedHipsInHeading = new Vector3(0,.527f,-.323f);
        public void ConfigureSeatPose(Vector3 hipsInHeading) { seatedHipsInHeading=hipsInHeading; }
        private static readonly int Speed = Animator.StringToHash("Speed");
        private float smoothedSpeed;
        private PassengerAiV2TrainInteriorAction interior;
        private PassengerAiV2FareGateTraversalAction gateAction;
        private Transform hips;
        private int activity;
        private bool sittingTransition;
        private PassengerAiV23DMotor motor;
        public void UseInterior(PassengerAiV2TrainInteriorAction action) { interior=action; }
        public void RefreshActivitySources() { gateAction=GetComponent<PassengerAiV2FareGateTraversalAction>(); }
        private void Awake()
        {
            motor=GetComponentInChildren<PassengerAiV23DMotor>();
            foreach (Transform bone in animator.GetComponentsInChildren<Transform>())
                if(bone.name=="DEF-hips") hips=bone;
        }

        public void Configure(PassengerAiV2Agent owner, Transform visual, Animator animation)
        {
            agent = owner; model = visual; animator = animation;
            animator.applyRootMotion = false;
        }
        private void LateUpdate()
        {
            if (agent == null || model == null || animator == null) return;
            int desired=interior!=null && interior.IsUsingActivity
                ? (interior.SelectedKind==PassengerAiV2InteriorSpotKind.Seat?1:interior.SelectedKind==PassengerAiV2InteriorSpotKind.Lean?2:0)
                : gateAction!=null && gateAction.IsTaggingCard?3:0;
            if(desired!=activity)
            {
                int previous=activity; activity=desired; sittingTransition=activity==1;
                string state=activity==1?"SitDown":activity==3?"Interact":previous==1?"StandUp":"Locomotion";
                if(animator.HasState(0,Animator.StringToHash(state))) animator.CrossFadeInFixedTime(state,.18f);
            }
            AnimatorStateInfo info=animator.GetCurrentAnimatorStateInfo(0);
            if(sittingTransition && info.IsName("SitDown") && info.normalizedTime>=.93f)
            { animator.CrossFadeInFixedTime("Sitting",.15f); sittingTransition=false; }
            if(activity==0 && info.IsName("StandUp") && info.normalizedTime>=.93f) animator.CrossFadeInFixedTime("Locomotion",.15f);
            Vector2 velocity = agent.Velocity;
            float speed = velocity.magnitude;
            smoothedSpeed = Mathf.MoveTowards(smoothedSpeed, speed, Time.deltaTime * 5f);
            bool stairWalk=motor!=null && motor.IsOnStairs && speed>.07f;
            animator.SetFloat(Speed,stairWalk?1.35f:smoothedSpeed);
            float playback=stairWalk?Mathf.Clamp(speed/1.35f,.35f,.65f):1f;
            animator.speed=Mathf.MoveTowards(animator.speed,playback,Time.deltaTime*2f);
            // Never squash the rig or rotate its logical/physics parent to imitate sitting.
            model.localPosition=Vector3.zero;
            if(interior!=null && interior.IsUsingActivity && interior.SelectedSpot!=null)
            {
                Transform anchor=interior.SelectedSpot.transform;
                Quaternion facing=Quaternion.LookRotation(anchor.forward,Vector3.up);
                if(activity==2) facing*=Quaternion.Euler(-6,0,0);
                model.rotation=Quaternion.RotateTowards(model.rotation,facing,Time.deltaTime*160);
                if(activity==1)
                {
                    // Plant the feet in front of the cushion throughout SitDown. The authored
                    // animation lowers the pelvis; only the completed pose is contact-aligned.
                    model.position=anchor.position-model.rotation*seatedHipsInHeading;
                    if(!sittingTransition && hips!=null) model.position+=anchor.position-hips.position;
                }
            }
            else if (speed > 0.07f)
            {
                Quaternion facing = Quaternion.LookRotation(new Vector3(velocity.x, 0f, velocity.y));
                model.rotation = Quaternion.RotateTowards(model.rotation, facing, 220f * Time.deltaTime);
            }
        }
    }
}

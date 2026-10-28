using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Unity.Profiling;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // One presentation owner only. It never writes the capsule/root or runs IK.
    public sealed class AnimationLabFreeWalkPlayer : IDisposable
    {
        static readonly ProfilerMarker Profile = new ProfilerMarker("SubwayCarry.FreeWalk.Presentation");
        public enum Phase { Idle, Start, Walk, Stop }
        public Phase State { get; private set; }
        public AnimationClip Clip { get; private set; }
        public int Transitions { get; private set; }
        public float Rate { get; private set; } = 1;
        public float Time => current.IsValid() ? (float)current.GetTime() : 0;
        public float ReferenceSpeed => data.speed;
        public bool Ready => graph.IsValid();
        public bool MeasurePerformance { get; set; }
        public long MeasuredFrames { get; private set; }
        public long MeasuredTicks { get; private set; }
        public long MeasuredBytes { get; private set; }
        readonly Animator animator;
        readonly AnimationLabFreeWalkData data;
        readonly Avatar previousAvatar;
        readonly RuntimeAnimatorController previousController;
        readonly Transform leftFoot, rightFoot,hips;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable current, previous;
        int slot;
        float blendTime, blendDuration;
        public bool ExternalPlayback {get;private set;}
        public bool InertialTransitions {get;set;}
        readonly Transform[] poseBones=new Transform[(int)HumanBodyBones.LastBone];
        readonly Quaternion[] rest=new Quaternion[(int)HumanBodyBones.LastBone],shown=new Quaternion[(int)HumanBodyBones.LastBone],incoming=new Quaternion[(int)HumanBodyBones.LastBone];
        readonly Vector3[] shownVelocity=new Vector3[(int)HumanBodyBones.LastBone],residual=new Vector3[(int)HumanBodyBones.LastBone],residualVelocity=new Vector3[(int)HumanBodyBones.LastBone];
        Vector3 restHips,shownHips,shownHipsVelocity,hipResidual,hipResidualVelocity;
        bool hasShown,pendingResidual;
        float residualDuration,residualAt;

        public AnimationLabFreeWalkPlayer(Animator target, AnimationLabFreeWalkData value)
        {
            if (target == null || value == null || !value.Valid) throw new ArgumentException("Free Walk source/Avatar not prepared");
            animator = target; data = value;
            previousAvatar = animator.avatar; previousController = animator.runtimeAnimatorController;
            try
            {
                animator.runtimeAnimatorController = null;
                animator.avatar = data.avatar;
                animator.applyRootMotion = false;
                animator.Rebind();
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                restHips=hips.localPosition;
                for(int i=0;i<poseBones.Length;i++){poseBones[i]=animator.GetBoneTransform((HumanBodyBones)i);if(poseBones[i]!=null)rest[i]=poseBones[i].localRotation;}
                if (leftFoot == null || rightFoot == null) throw new InvalidOperationException("Humanoid feet missing");
                graph = PlayableGraph.Create("Free Walk single presentation owner");
                graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                mixer = AnimationMixerPlayable.Create(graph, 2);
                AnimationPlayableOutput.Create(graph, "Free Walk Humanoid pose", animator).SetSourcePlayable(mixer);
                graph.Play(); Play(data.idle, Phase.Idle, 0); graph.Evaluate(0);
            }
            catch { Dispose(); throw; }
        }
        void Play(AnimationClip clip, Phase state, float fade)
        {
            if (previous.IsValid()) { graph.Disconnect(mixer, 1 - slot); graph.DestroyPlayable(previous); }
            previous = current; slot = 1 - slot;
            current = AnimationClipPlayable.Create(graph, clip);
            current.SetApplyFootIK(false); current.SetApplyPlayableIK(false);
            graph.Connect(current, 0, mixer, slot);
            blendTime = 0; blendDuration = fade;
            mixer.SetInputWeight(slot, fade <= 0 ? 1 : 0);
            mixer.SetInputWeight(1 - slot, fade <= 0 ? 0 : 1);
            Clip = clip; State = state; Transitions++;
        }
        public void Tick(float actualSpeed, float wantedSpeed, float dt)
        {
            using var profile = Profile.Auto();
            long began=MeasurePerformance?System.Diagnostics.Stopwatch.GetTimestamp():0;
            long allocated=MeasurePerformance?GC.GetAllocatedBytesForCurrentThread():0;
            TickPose(actualSpeed,wantedSpeed,dt);
            if(MeasurePerformance)
            {
                MeasuredTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-began;
                MeasuredBytes+=GC.GetAllocatedBytesForCurrentThread()-allocated;MeasuredFrames++;
            }
        }
        void TickPose(float actualSpeed, float wantedSpeed, float dt)
        {
            bool wants = wantedSpeed > data.speed * .12f;
            if (State == Phase.Idle && wants) Play(data.start, Phase.Start, .1f);
            else if (State == Phase.Start && !wants) Stop();
            else if (State == Phase.Start && Time >= data.start.length - .12f) Play(data.walk, Phase.Walk, .12f);
            else if (State == Phase.Walk && wantedSpeed < data.speed * .35f && actualSpeed < data.speed * .85f) Stop();
            else if (State == Phase.Stop && wants) Play(data.start, Phase.Start, .15f);
            else if (State == Phase.Stop && Time >= Clip.length - .15f) Play(data.idle, Phase.Idle, .15f);
            Rate = State == Phase.Walk ? Mathf.Clamp(actualSpeed / data.speed, .65f, 1.2f) : 1;
            current.SetSpeed(Rate);
            if (previous.IsValid())
            {
                blendTime += dt;
                float w = blendDuration > 0 ? Mathf.Clamp01(blendTime / blendDuration) : 1;
                mixer.SetInputWeight(slot, w); mixer.SetInputWeight(1 - slot, 1 - w);
                if (w >= 1) { graph.Disconnect(mixer, 1 - slot); graph.DestroyPlayable(previous); previous = default; }
            }
        }
        void Stop()
        {
            Vector3 l = animator.transform.InverseTransformPoint(leftFoot.position), r = animator.transform.InverseTransformPoint(rightFoot.position);
            float a = (l - data.stopLeftL).sqrMagnitude + (r - data.stopLeftR).sqrMagnitude;
            float b = (l - data.stopRightL).sqrMagnitude + (r - data.stopRightR).sqrMagnitude;
            Play(a <= b ? data.stopLeft : data.stopRight, Phase.Stop, .12f);
        }
        public void Pause(bool value) { if (graph.IsValid()) graph.SetTimeUpdateMode(value||ExternalPlayback ? DirectorUpdateMode.Manual : DirectorUpdateMode.GameTime); }
        public void SelectExternal(AnimationClip clip,float time,float fade)
        {
            ExternalPlayback=true;graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            if(fade<=0){hasShown=false;shownHipsVelocity=Vector3.zero;Array.Clear(shownVelocity,0,shownVelocity.Length);}
            pendingResidual=InertialTransitions&&hasShown&&fade>0;residualDuration=Mathf.Max(.001f,fade);residualAt=time;
            Play(clip,Phase.Idle,fade);current.SetTime(time);
        }
        public void EvaluateExternal(float time,float oldTime,float blend,Transform motorFrame=null,float poseDeltaTime=0)
        {
            if(!Ready||!current.IsValid())return;
            if(InertialTransitions){EvaluateInertial(time,blend,motorFrame,poseDeltaTime);return;}
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);current.SetTime(time);
            if(previous.IsValid())previous.SetTime(oldTime);
            float w=Mathf.SmoothStep(0,1,blend);mixer.SetInputWeight(slot,w);mixer.SetInputWeight(1-slot,1-w);
            graph.Evaluate(0);
            if(motorFrame!=null&&hips!=null)
            {
                // Humanoid clips are expressed in Animator +Z. The retained Generic visual has
                // its own model-to-capsule axis. Convert the fresh pose, not the approved model
                // Transform, Avatar, source curves or Motor/root. Never apply this to legacy FK.
                float yaw=Vector3.SignedAngle(Vector3.ProjectOnPlane(animator.transform.forward,Vector3.up),Vector3.ProjectOnPlane(motorFrame.forward,Vector3.up),Vector3.up);
                var conversion=Quaternion.AngleAxis(yaw,Vector3.up);
                hips.position=animator.transform.position+conversion*(hips.position-animator.transform.position);
                hips.rotation=conversion*hips.rotation;
            }
            if(w>=1&&previous.IsValid()){graph.Disconnect(mixer,1-slot);graph.DestroyPlayable(previous);previous=default;}
        }
        void EvaluateBase(float at,Transform motorFrame)
        {
            // Optional/unkeyed bones must not inherit last frame's post-animation FK correction.
            for(int i=0;i<poseBones.Length;i++)if(poseBones[i]!=null)poseBones[i].localRotation=rest[i];
            hips.localPosition=restHips;
            current.SetTime(at);mixer.SetInputWeight(slot,1);mixer.SetInputWeight(1-slot,0);graph.Evaluate(0);
            if(motorFrame==null)return;
            float yaw=Vector3.SignedAngle(Vector3.ProjectOnPlane(animator.transform.forward,Vector3.up),Vector3.ProjectOnPlane(motorFrame.forward,Vector3.up),Vector3.up);
            var conversion=Quaternion.AngleAxis(yaw,Vector3.up);
            hips.position=animator.transform.position+conversion*(hips.position-animator.transform.position);hips.rotation=conversion*hips.rotation;
        }
        void EvaluateInertial(float at,float blend,Transform motorFrame,float dt)
        {
            // Finite cubic residual: retain the displayed composite pose/velocity on interruption,
            // then converge to the new native pose. Root collision/movement stays outside this graph.
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            if(pendingResidual)
            {
                float next=Mathf.Min(Clip.length,residualAt+1f/120),sample=Mathf.Max(.0001f,next-residualAt);
                EvaluateBase(next,motorFrame);var futureHips=hips.localPosition;
                for(int i=0;i<poseBones.Length;i++)if(poseBones[i]!=null)incoming[i]=poseBones[i].localRotation;
                EvaluateBase(residualAt,motorFrame);
                for(int i=0;i<poseBones.Length;i++)if(poseBones[i]!=null)
                {
                    var basis=poseBones[i].localRotation;
                    residual[i]=LogRotation(shown[i]*Quaternion.Inverse(basis));
                    residualVelocity[i]=Vector3.ClampMagnitude(shownVelocity[i]-LogRotation(incoming[i]*Quaternion.Inverse(basis))/sample,20);
                }
                hipResidual=shownHips-hips.localPosition;
                hipResidualVelocity=Vector3.ClampMagnitude(shownHipsVelocity-(futureHips-hips.localPosition)/sample,3);
                pendingResidual=false;
                EvaluateBase(at,motorFrame);
            }
            else EvaluateBase(at,motorFrame);
            float t=Mathf.Clamp01(blend),a=2*t*t*t-3*t*t+1,b=(t*t*t-2*t*t+t)*residualDuration;
            for(int i=0;i<poseBones.Length;i++)if(poseBones[i]!=null)
            {
                var value=ExpRotation(residual[i]*a+residualVelocity[i]*b)*poseBones[i].localRotation;
                if(hasShown&&dt>0)shownVelocity[i]=LogRotation(value*Quaternion.Inverse(shown[i]))/dt;
                shown[i]=value;poseBones[i].localRotation=value;
            }
            var hip=hips.localPosition+hipResidual*a+hipResidualVelocity*b;
            if(hasShown&&dt>0)shownHipsVelocity=(hip-shownHips)/dt;
            shownHips=hips.localPosition=hip;hasShown=true;
            if(previous.IsValid()){graph.Disconnect(mixer,1-slot);graph.DestroyPlayable(previous);previous=default;}
        }
        static Vector3 LogRotation(Quaternion q)
        {
            if(q.w<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
            q.ToAngleAxis(out float degrees,out var axis);if(degrees>180)degrees-=360;
            return float.IsFinite(axis.x)?axis*(degrees*Mathf.Deg2Rad):Vector3.zero;
        }
        static Quaternion ExpRotation(Vector3 value)=>value.sqrMagnitude<1e-12f?Quaternion.identity:Quaternion.AngleAxis(value.magnitude*Mathf.Rad2Deg,value.normalized);
        public void SamplePose(AnimationClip clip,float time)
        {
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            if(Clip!=clip)Play(clip,Phase.Idle,0);
            current.SetTime(time);graph.Evaluate(0);
        }
        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            if (animator != null) { animator.avatar = previousAvatar; animator.runtimeAnimatorController = previousController; animator.Rebind(); }
        }
    }
}

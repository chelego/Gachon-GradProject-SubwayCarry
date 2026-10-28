using System;
using UnityEngine;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Opt-in full-body Generic playback. CharacterController remains the only root/position writer.
    public sealed class AnimationLabSqueezeBody : MonoBehaviour
    {
        static readonly Unity.Profiling.ProfilerMarker PoseProfile=new Unity.Profiling.ProfilerMarker("SubwayCarry.Squeeze.Pose");
        AnimationLabPhoneMotion data;AnimationLabPhoneMotor motor;GameObject sample;
        Transform[] pose,live;Transform[] upper=new Transform[2],lower=new Transform[2],feet=new Transform[2];
        Vector3[] planted=new Vector3[2];bool[] contact=new bool[2];Quaternion[] plantedRotation=new Quaternion[2];
        Quaternion[] baseRotation;Vector3 baseHips;bool hasBase;
        readonly RaycastHit[] hits=new RaycastHit[32];
        int phase,side;float clock,weight,exitYaw;Vector3 flow;AnimationLabPhoneMotion.SqueezeClip entry;
        public bool Active {get;private set;}
        public int Phase=>phase;
        public float PhaseTime=>clock;
        public bool PhaseComplete=>entry!=null&&clock>=entry.clip.length-.0001f;
        public float Cycle=>entry!=null?Mathf.Repeat(clock/entry.clip.length,1):0;
        public float FootError {get;private set;}
        public float MinimumSweep {get;private set;}=10;
        public int BlockedSweeps {get;private set;}
        public string CurrentClip=>entry!=null?entry.authored.clipName:"";
        public void Initialize(AnimationLabPhoneMotor owner,AnimationLabPhoneMotion binding)
        {
            motor=owner;data=binding;motor.SqueezeBody=this;var source=data.squeeze[0].authored;
            sample=Instantiate(data.rig);sample.name="Squeeze pose sampler · not an actor";sample.hideFlags=HideFlags.HideAndDontSave;
            foreach(var r in sample.GetComponentsInChildren<Renderer>())r.enabled=false;
            foreach(var c in sample.GetComponentsInChildren<Collider>())c.enabled=false;
            foreach(var a in sample.GetComponentsInChildren<Animator>())a.enabled=false;
            pose=new Transform[source.paths.Length];live=new Transform[pose.Length];
            baseRotation=new Quaternion[pose.Length];
            for(int i=0;i<pose.Length;i++){pose[i]=source.paths[i].Length==0?sample.transform:sample.transform.Find(source.paths[i]);live[i]=source.paths[i].Length==0?transform:transform.Find(source.paths[i]);}
            for(int i=0;i<2;i++){bool l=i==0;upper[i]=transform.Find(source.humanPaths[(int)(l?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg)]);lower[i]=transform.Find(source.humanPaths[(int)(l?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg)]);feet[i]=transform.Find(source.humanPaths[(int)(l?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot)]);}
        }
        public void Begin(Vector2 direction,int leadingSide)
        {
            flow=new Vector3(direction.x,0,direction.y).normalized;side=leadingSide<0?-1:1;Active=true;weight=0;FootError=0;MinimumSweep=10;BlockedSweeps=0;ChangePhase(0);
        }
        public void ChangePhase(int value)
        {
            phase=value;clock=0;entry=data.FindSqueeze(phase,side);for(int i=0;i<2;i++)contact[i]=false;
            if(phase==2)exitYaw=Vector3.SignedAngle(flow,transform.forward,Vector3.up);
        }
        public void RestoreBase(){if(!hasBase)return;for(int i=1;i<live.Length;i++)live[i].localRotation=baseRotation[i];live[entry.authored.hipsIndex].localPosition=baseHips;hasBase=false;}
        public void End(){RestoreBase();Active=false;weight=0;motor.SetRadius(.24f);for(int i=0;i<2;i++)contact[i]=false;}
        public Vector3 Constrain(Vector3 desired,float dt,out Vector3 facing)
        {
            float n=entry!=null?Mathf.Clamp01(clock/entry.clip.length):1;
            float turn=phase==0?-side*90*Mathf.SmoothStep(0,1,n):phase==1?-side*90:Mathf.Lerp(exitYaw,0,Mathf.SmoothStep(0,1,n));
            var target=Quaternion.AngleAxis(turn,Vector3.up)*flow;facing=Vector3.RotateTowards(transform.forward,target,125*Mathf.Deg2Rad*dt,0).normalized;
            float narrow=Mathf.InverseLerp(70,88,Mathf.Abs(Vector3.SignedAngle(flow,facing,Vector3.up)));motor.SetRadius(Mathf.Lerp(.24f,.18f,narrow));
            // Keep the social solver's accepted forward speed. Deliberate sideways gait has no diagonal root drift.
            float speed=Mathf.Clamp(Vector3.Dot(desired,flow),0,phase==1?data.squeezeSpeed:.11f);
            var command=flow*speed;float distance=command.magnitude*dt;
            if(distance>.000001f)
            {
                int count=Physics.BoxCastNonAlloc(transform.position+Vector3.up*.96f,new Vector3(.255f,.50f,.19f),flow,hits,Quaternion.LookRotation(facing),distance+.018f,~0,QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++){var h=hits[i];if(h.collider==null||h.collider.transform.IsChildOf(transform))continue;MinimumSweep=Mathf.Min(MinimumSweep,h.distance);if(h.distance<distance+.018f){speed=Mathf.Min(speed,Mathf.Max(0,h.distance-.018f)/dt);BlockedSweeps++;}}
                command=flow*speed;
            }
            return command;
        }
        public void Apply(float dt)
        {
            if(!Active||entry==null||sample==null)return;using var marker=PoseProfile.Auto();
            if(phase==1)clock+=dt*Mathf.Clamp(motor.ActualVelocity.magnitude/Mathf.Max(.05f,data.squeezeStride/entry.clip.length),0,1.35f);
            else clock=Mathf.Min(entry.clip.length,clock+dt);
            float t=phase==1?Mathf.Repeat(clock,entry.clip.length):clock;entry.clip.SampleAnimation(sample,t);weight=Mathf.MoveTowards(weight,1,dt*8);
            for(int i=1;i<live.Length;i++)baseRotation[i]=live[i].localRotation;baseHips=live[entry.authored.hipsIndex].localPosition;hasBase=true;
            for(int i=1;i<live.Length;i++)if(live[i]!=null&&pose[i]!=null)live[i].localRotation=Quaternion.Slerp(live[i].localRotation,pose[i].localRotation,weight);
            int hips=entry.authored.hipsIndex;live[hips].localPosition=Vector3.Lerp(live[hips].localPosition,pose[hips].localPosition,weight);
            float n=t/entry.clip.length;
            for(int i=0;i<2;i++)
            {
                bool lead=side>0?i==1:i==0;float a=lead?.08f:.56f,b=lead?.44f:.92f;
                if(phase!=1){a=lead?.08f:.55f;b=lead?.52f:.99f;if(phase==2){a=1-b;b=1-(lead?.08f:.55f);}}
                bool grounded=weight>=.999f&&!(n>a&&n<b);
                if(grounded&&!contact[i]){planted[i]=feet[i].position;plantedRotation[i]=feet[i].rotation;}
                contact[i]=grounded;
                if(!grounded)continue;
                var target=planted[i];if(Physics.Raycast(target+Vector3.up*.16f,Vector3.down,out var ground,.4f,1,QueryTriggerInteraction.Ignore))target.y=ground.point.y+.1005f;
                Vector3 bend=lower[i].position-(upper[i].position+feet[i].position)*.5f;
                AnimationLabContacts.Solve(upper[i],lower[i],feet[i],target,bend,weight);feet[i].rotation=Quaternion.Slerp(feet[i].rotation,plantedRotation[i],weight);
                float error=Vector3.Distance(feet[i].position,target);
#if UNITY_EDITOR
                if(Application.isBatchMode&&error>FootError+.012f)Debug.Log("SQUEEZE_FOOT_DIAG phase="+phase+" n="+n+" side="+side+" leg="+i+" error="+error+" weight="+weight+" reach="+Vector3.Distance(upper[i].position,target)+" length="+(Vector3.Distance(upper[i].position,lower[i].position)+Vector3.Distance(lower[i].position,feet[i].position))+" bodyYaw="+Vector3.SignedAngle(flow,transform.forward,Vector3.up)+" point="+feet[i].position.ToString("F3")+" target="+target.ToString("F3"));
#endif
                FootError=Mathf.Max(FootError,error);
            }
        }
        void OnDestroy(){if(sample!=null)Destroy(sample);}
    }
}

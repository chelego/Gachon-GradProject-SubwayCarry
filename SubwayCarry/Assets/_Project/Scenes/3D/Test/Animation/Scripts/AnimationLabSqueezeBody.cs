using System;
using UnityEngine;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Generic pose playback; the existing motor is the sole root/rotation owner.
    public sealed class AnimationLabSqueezeBody : MonoBehaviour
    {
        static readonly Unity.Profiling.ProfilerMarker PoseProfile=new Unity.Profiling.ProfilerMarker("SubwayCarry.Squeeze.Pose");
        AnimationLabPhoneMotion data;AnimationLabPhoneMotor motor;GameObject sample;Transform[] pose,live;
        readonly Transform[] upper=new Transform[2],lower=new Transform[2],feet=new Transform[2],toes=new Transform[2];
        readonly Vector3[] planted=new Vector3[2];readonly Quaternion[] plantedRotation=new Quaternion[2];readonly byte[] contact=new byte[2];
        Quaternion[] baseRotation;Vector3 baseHips;bool hasBase;
        readonly RaycastHit[] hits=new RaycastHit[32];Transform[] armProbes;
        int phase,side,passageMode;float clock,weight;Vector3 flow;AnimationLabPhoneMotion.SqueezeClip entry;
        public bool Active {get;private set;}
        public int Phase=>phase;public float PhaseTime=>clock;
        public bool PhaseComplete=>entry!=null&&clock>=entry.clip.length-.0001f;
        public float Cycle=>entry!=null?Mathf.Repeat(clock/entry.clip.length,1):0;
        public float FootError {get;private set;}
        public float MinimumSweep {get;private set;}=10;public int BlockedSweeps {get;private set;}
        public string CurrentClip=>entry!=null?entry.authored.clipName:"";
        public void Initialize(AnimationLabPhoneMotor owner,AnimationLabPhoneMotion binding)
        {
            motor=owner;data=binding;motor.SqueezeBody=this;var source=data.squeeze[0].authored;
            sample=Instantiate(data.rig);sample.name="Squeeze pose sampler · not an actor";sample.hideFlags=HideFlags.HideAndDontSave;
            foreach(var r in sample.GetComponentsInChildren<Renderer>())r.enabled=false;
            foreach(var c in sample.GetComponentsInChildren<Collider>())c.enabled=false;
            foreach(var a in sample.GetComponentsInChildren<Animator>())a.enabled=false;
            pose=new Transform[source.paths.Length];live=new Transform[pose.Length];baseRotation=new Quaternion[pose.Length];
            for(int i=0;i<pose.Length;i++){pose[i]=source.paths[i].Length==0?sample.transform:sample.transform.Find(source.paths[i]);live[i]=source.paths[i].Length==0?transform:transform.Find(source.paths[i]);}
            for(int i=0;i<2;i++){bool l=i==0;upper[i]=transform.Find(source.humanPaths[(int)(l?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg)]);lower[i]=transform.Find(source.humanPaths[(int)(l?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg)]);feet[i]=transform.Find(source.humanPaths[(int)(l?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot)]);toes[i]=transform.Find(source.humanPaths[(int)(l?HumanBodyBones.LeftToes:HumanBodyBones.RightToes)]);}
            var labels=new[]{HumanBodyBones.LeftLowerArm,HumanBodyBones.RightLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightHand};
            armProbes=Array.ConvertAll(labels,h=>transform.Find(source.humanPaths[(int)h]));
        }
        public void Begin(Vector2 direction,int leadingSide)=>Begin(direction,leadingSide,0);
        public void Begin(Vector2 direction,int leadingSide,int mode)
        {
            passageMode=mode;flow=new Vector3(direction.x,0,direction.y).normalized;side=leadingSide<0?-1:1;Active=true;weight=0;FootError=0;MinimumSweep=10;BlockedSweeps=0;
            for(int i=0;i<2;i++)contact[i]=0;ChangePhase(0);
        }
        public void ChangePhase(int value)
        {
            RestoreBase();phase=value;clock=0;entry=data.FindSqueeze(phase,side,passageMode);
            if(entry==null||!entry.HasTrajectory)throw new InvalidOperationException("Saved squeeze trajectory/contact metadata missing");
        }
        public void RestoreBase(){if(!hasBase)return;for(int i=1;i<live.Length;i++)live[i].localRotation=baseRotation[i];live[entry.authored.hipsIndex].localPosition=baseHips;hasBase=false;}
        public void End()
        {
            RestoreBase();if(Active&&entry!=null)motor.SyncWalkPhase(entry.endWalkPhase);
            Active=false;weight=0;motor.SetRadius(.24f);for(int i=0;i<2;i++)contact[i]=0;
        }
        float SampleFrame=>entry==null?0:(phase==1?Mathf.Repeat(clock,entry.clip.length):Mathf.Min(clock,entry.clip.length))*entry.authored.sampleRate;
        Vector3 SourceVelocity
        {
            get
            {
                int f=Mathf.Clamp(Mathf.FloorToInt(SampleFrame),0,entry.root.Length-2);
                return Vector3.ProjectOnPlane(entry.root[f+1]-entry.root[f],Vector3.up)*entry.authored.sampleRate;
            }
        }
        float SourceSpeed=>SourceVelocity.magnitude;
        Vector3 SourceTravel=>Quaternion.LookRotation(flow)*SourceVelocity.normalized;
        public Vector3 Constrain(Vector3 desired,float dt,out Vector3 facing)
        {
            float frame=SampleFrame;int a=Mathf.Clamp(Mathf.FloorToInt(frame),0,entry.rootRotation.Length-1),b=Mathf.Min(a+1,entry.rootRotation.Length-1);
            var relative=Quaternion.Slerp(entry.rootRotation[a],entry.rootRotation[b],frame-a);
            var target=Quaternion.LookRotation(flow)*relative;
            facing=Vector3.RotateTowards(transform.forward,target*Vector3.forward,125*Mathf.Deg2Rad*dt,0).normalized;
            // Never shrink the original capsule. Torso and arm probes use the actual oblique posture.
            var travel=SourceTravel;float speed=Mathf.Clamp(Vector3.Dot(desired,travel),0,SourceSpeed);
            var next=transform.position+travel*(speed*dt);if(!data.bounds.Contains(new Vector2(next.x,next.z)))speed=0;
            float distance=speed*dt;
            if(distance>.000001f)
            {
                var box=Quaternion.LookRotation(facing);
                int n=Physics.BoxCastNonAlloc(transform.position+Vector3.up*.98f,new Vector3(.24f,.45f,.135f),travel,hits,box,distance+.014f,~0,QueryTriggerInteraction.Ignore);
                speed=Limit(n,distance,dt,speed);
                foreach(var probe in armProbes){n=Physics.SphereCastNonAlloc(probe.position,.035f,travel,hits,distance+.014f,~0,QueryTriggerInteraction.Ignore);speed=Limit(n,distance,dt,speed);}
            }
            return travel*speed;
        }
        float Limit(int count,float distance,float dt,float speed)
        {
            for(int i=0;i<count;i++){var h=hits[i];if(h.collider==null||h.collider.transform.IsChildOf(transform)||h.collider.bounds.max.y<transform.position.y+.05f)continue;MinimumSweep=Mathf.Min(MinimumSweep,h.distance);if(h.distance<distance+.014f){speed=Mathf.Min(speed,Mathf.Max(0,h.distance-.014f)/dt);BlockedSweeps++;}}return speed;
        }
        public void Apply(float dt)
        {
            if(!Active||entry==null||sample==null)return;using var marker=PoseProfile.Auto();
            float rate=SourceSpeed<.02f?1:Mathf.Clamp(Mathf.Max(0,Vector3.Dot(motor.ActualVelocity,SourceTravel))/SourceSpeed,0,1.3f);
            clock=phase==1?clock+dt*rate:Mathf.Min(entry.clip.length,clock+dt*rate);
            float t=phase==1?Mathf.Repeat(clock,entry.clip.length):clock;
            entry.clip.SampleAnimation(sample,t);weight=Mathf.MoveTowards(weight,1,dt*7);
            for(int i=1;i<live.Length;i++)baseRotation[i]=live[i].localRotation;baseHips=live[entry.authored.hipsIndex].localPosition;hasBase=true;
            for(int i=1;i<live.Length;i++)live[i].localRotation=Quaternion.Slerp(live[i].localRotation,pose[i].localRotation,weight);
            int hips=entry.authored.hipsIndex;live[hips].localPosition=Vector3.Lerp(live[hips].localPosition,pose[hips].localPosition,weight);
            int f=Mathf.Clamp(Mathf.RoundToInt(t*entry.authored.sampleRate),0,entry.root.Length-1);
            for(int i=0;i<2;i++)
            {
                byte label=weight<.999f?(byte)0:(i==0?entry.leftContact[f]:entry.rightContact[f]);
                if(label==0){contact[i]=0;continue;}
                Vector3 pivot=label==2?toes[i].position:feet[i].position;
                if(contact[i]!=label){planted[i]=pivot;plantedRotation[i]=feet[i].rotation;}
                contact[i]=label;Vector3 target=planted[i]-(label==2?toes[i].position-feet[i].position:Vector3.zero);
                Vector3 bend=lower[i].position-(upper[i].position+feet[i].position)*.5f;
                var authoredRotation=feet[i].rotation;AnimationLabContacts.Solve(upper[i],lower[i],feet[i],target,bend,1);
                feet[i].rotation=label==1?plantedRotation[i]:authoredRotation;
                FootError=Mathf.Max(FootError,Vector3.Distance(feet[i].position,target));
            }
        }
        void OnDestroy(){if(sample!=null)Destroy(sample);}
    }
}


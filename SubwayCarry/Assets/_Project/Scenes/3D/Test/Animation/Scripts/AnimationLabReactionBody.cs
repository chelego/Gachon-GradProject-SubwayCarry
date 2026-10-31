using UnityEngine;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // The Motor owns the one CC move. Animation time commits only accepted root displacement.
    public sealed class AnimationLabReactionBody : MonoBehaviour
    {
        AnimationLabPhoneCase owner;AnimationLabPhoneMotor motor;AnimationLabPhoneMotion data;GameObject sampler;Transform[] live,sample;Quaternion[] baseline,entryRot;Vector3 baselineHips,entryHips;bool corrected,releasing;
        readonly Transform[] thighs=new Transform[2],knees=new Transform[2],feet=new Transform[2],toes=new Transform[2];readonly Vector3[] anchors=new Vector3[2],toeOffsets=new Vector3[2];readonly Vector3[] releaseOffset=new Vector3[2],lastFoot=new Vector3[2];readonly byte[] contacts=new byte[2];readonly float[] floor=new float[2];readonly RaycastHit[] hits=new RaycastHit[24];
        Quaternion basis;float clock,rate,weight,pendingTime;Vector3 pendingDelta;
        public AnimationLabPhoneMotion.ReactionClip Entry {get;private set;}
        public bool Active {get;private set;}
        public bool ControlsMovement=>Active&&!releasing;
        public bool Complete=>Entry!=null&&clock>=Entry.clip.length-.0001f;
        public float Clock=>clock;
        public float MaxSupportError {get;private set;}
        public int BlockedSweeps {get;private set;}
        public Vector3 WorldDelta=>Entry!=null?basis*(Entry.root[Entry.root.Length-1]-Entry.root[0]):Vector3.zero;
        public void Initialize(AnimationLabPhoneCase value)
        {
            owner=value;motor=value.Passenger;data=value.Data;motor.Reaction=this;var m=data.authored;sampler=Instantiate(data.rig);sampler.name="Reaction pose sampler";sampler.hideFlags=HideFlags.HideAndDontSave;
            foreach(var r in sampler.GetComponentsInChildren<Renderer>())r.enabled=false;foreach(var c in sampler.GetComponentsInChildren<Collider>())c.enabled=false;foreach(var a in sampler.GetComponentsInChildren<Animator>())a.enabled=false;
            live=new Transform[m.paths.Length];sample=new Transform[live.Length];baseline=new Quaternion[live.Length];entryRot=new Quaternion[live.Length];for(int i=0;i<live.Length;i++){live[i]=m.paths[i].Length==0?transform:transform.Find(m.paths[i]);sample[i]=m.paths[i].Length==0?sampler.transform:sampler.transform.Find(m.paths[i]);}
            for(int i=0;i<2;i++){Transform B(HumanBodyBones l,HumanBodyBones r)=>transform.Find(m.humanPaths[(int)(i==0?l:r)]);thighs[i]=B(HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg);knees[i]=B(HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg);feet[i]=B(HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot);toes[i]=B(HumanBodyBones.LeftToes,HumanBodyBones.RightToes);toeOffsets[i]=feet[i].InverseTransformPoint(toes[i].position);floor[i]=data.rig.transform.Find(m.humanPaths[(int)(i==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot)]).position.y-data.rig.transform.position.y;}
        }
        public AnimationLabPhoneMotion.ReactionClip Choose(AnimationLabReactionKind kind)=>Choose(kind,out _);
        public AnimationLabPhoneMotion.ReactionClip Choose(AnimationLabReactionKind kind,out float matchCost)
        {
            AnimationLabPhoneMotion.ReactionClip best=null;float cost=float.PositiveInfinity;float phase=Mathf.Repeat(motor.WalkTime/data.walk.length,1);
            foreach(var e in data.reactions){if(e.kind!=kind||e.clip==null||e.authored==null||e.root.Length!=e.authored.lastFrame+1)continue;float score=Mathf.Abs(Mathf.DeltaAngle(phase*360,e.gaitPhase*360))*.5f;var p=e.authored.keys[0];for(int i=0;i<2;i++){score+=Quaternion.Angle(thighs[i].localRotation,p.rotations[System.Array.IndexOf(live,thighs[i])]);score+=Quaternion.Angle(knees[i].localRotation,p.rotations[System.Array.IndexOf(live,knees[i])]);}for(int i=0;i<2;i++){bool planted=feet[i].position.y<Mathf.Min(feet[0].position.y,feet[1].position.y)+.018f;bool authored=(i==0?e.leftContact[0]:e.rightContact[0])!=0;if(planted!=authored)score+=30;}if(score<cost){cost=score;best=e;}}matchCost=cost;return best;
        }
        public void Begin(AnimationLabPhoneMotion.ReactionClip clip)
        {
            RestoreBase();Entry=clip;clock=0;weight=1;releasing=false;Active=true;basis=Quaternion.LookRotation(motor.ActualVelocity.sqrMagnitude>.05f?motor.ActualVelocity.normalized:transform.forward);rate=Mathf.Clamp(motor.ActualVelocity.magnitude/Mathf.Max(.1f,clip.referenceSpeed),.7f,1.2f);MaxSupportError=0;BlockedSweeps=0;for(int i=1;i<live.Length;i++)entryRot[i]=live[i].localRotation;entryHips=live[clip.authored.hipsIndex].localPosition;for(int i=0;i<2;i++){contacts[i]=feet[i].position.y<Mathf.Min(feet[0].position.y,feet[1].position.y)+.018f?(byte)1:(byte)0;anchors[i]=lastFoot[i]=feet[i].position;releaseOffset[i]=Vector3.zero;}
        }
        public Vector3 Constrain(Vector3 desired,float dt,out Vector3 facing)
        {
            pendingTime=Mathf.Min(Entry.clip.length,clock+dt*rate);pendingDelta=basis*(Root(pendingTime)-Root(clock));float f=clock*Entry.authored.sampleRate;int a=Mathf.Clamp((int)f,0,Entry.rootRotation.Length-1),b=Mathf.Min(Entry.rootRotation.Length-1,a+1);var target=basis*Quaternion.Slerp(Entry.rootRotation[a],Entry.rootRotation[b],f-a);facing=Vector3.RotateTowards(transform.forward,target*Vector3.forward,125*Mathf.Deg2Rad*dt,0);
            if(Complete||pendingDelta.sqrMagnitude<.00000001f)return Vector3.zero;float speed=Mathf.Min(pendingDelta.magnitude/dt,desired.magnitude);if(Vector3.Dot(desired,pendingDelta.normalized)<-.05f)speed=0;var command=pendingDelta.normalized*speed;var end=motor.Position+new Vector2(command.x,command.z)*dt;if(!data.bounds.Contains(end)||!owner.Navigation.IsClear(motor.Position,end))return Vector3.zero;
            float distance=speed*dt;int count=Physics.CapsuleCastNonAlloc(motor.CapsuleBottom,motor.CapsuleTop,motor.BodyRadius,command.normalized,hits,distance+.015f,~0,QueryTriggerInteraction.Ignore);for(int i=0;i<count;i++){var h=hits[i];if(h.collider==null||h.collider.transform.IsChildOf(transform)||h.normal.y>.7f||h.collider.bounds.max.y<=transform.position.y+.035f)continue;float safe=Mathf.Max(0,h.distance-.015f);if(safe<distance){distance=safe;BlockedSweeps++;}}return pendingDelta.normalized*(distance/dt);
        }
        public void AfterMove(Vector3 acceptedVelocity,float dt)
        {
            if(!ControlsMovement||pendingDelta.sqrMagnitude<.00000001f)return;float fraction=Mathf.Clamp01(Vector3.Dot(acceptedVelocity*dt,pendingDelta.normalized)/pendingDelta.magnitude);clock=Mathf.Lerp(clock,pendingTime,fraction);if(Complete)Release();
        }
        Vector3 Root(float time){float f=Mathf.Clamp(time*Entry.authored.sampleRate,0,Entry.root.Length-1);int a=(int)f;return Vector3.Lerp(Entry.root[a],Entry.root[Mathf.Min(Entry.root.Length-1,a+1)],f-a);}
        public void RestoreBase(){if(!corrected)return;for(int i=1;i<live.Length;i++)live[i].localRotation=baseline[i];live[Entry.authored.hipsIndex].localPosition=baselineHips;corrected=false;}
        public void Release(){if(releasing)return;releasing=true;motor.SyncWalkPhase(Entry.endWalkPhase);}
        public void Cancel(){RestoreBase();Active=false;contacts[0]=contacts[1]=0;MaxSupportError=0;BlockedSweeps=0;}
        public void Apply(float dt)
        {
            if(!Active||Entry==null)return;if(releasing)weight=Mathf.Max(0,weight-dt*7);Entry.clip.SampleAnimation(sampler,Mathf.Min(clock,Entry.clip.length));int hips=Entry.authored.hipsIndex;for(int i=1;i<live.Length;i++)baseline[i]=live[i].localRotation;baselineHips=live[hips].localPosition;corrected=true;float join=Mathf.SmoothStep(0,1,Mathf.Clamp01(clock/.09f));
            for(int i=1;i<live.Length;i++)live[i].localRotation=Quaternion.Slerp(live[i].localRotation,Quaternion.Slerp(entryRot[i],sample[i].localRotation,join),weight);live[hips].localPosition=Vector3.Lerp(live[hips].localPosition,Vector3.Lerp(entryHips,sample[hips].localPosition,join),weight);
            int f=Mathf.Clamp(Mathf.RoundToInt(clock*Entry.authored.sampleRate),0,Entry.root.Length-1);for(int i=0;i<2;i++){byte mode=releasing?contacts[i]:i==0?Entry.leftContact[f]:Entry.rightContact[f];if(releasing&&feet[i].position.y>transform.position.y+floor[i]+.024f)mode=0;if(mode!=0&&mode!=contacts[i])anchors[i]=mode==2?toes[i].position:feet[i].position;if(mode==0){if(contacts[i]!=0)releaseOffset[i]=lastFoot[i]-feet[i].position;contacts[i]=0;releaseOffset[i]=Vector3.MoveTowards(releaseOffset[i],Vector3.zero,dt*.9f);if(releaseOffset[i].sqrMagnitude>.000001f){var bend0=knees[i].position-(thighs[i].position+feet[i].position)*.5f;AnimationLabContacts.Solve(thighs[i],knees[i],feet[i],feet[i].position+releaseOffset[i],bend0,1);}lastFoot[i]=feet[i].position;continue;}contacts[i]=mode;releaseOffset[i]=Vector3.zero;var target=mode==2?anchors[i]-feet[i].rotation*toeOffsets[i]:anchors[i];var bend=knees[i].position-(thighs[i].position+feet[i].position)*.5f;AnimationLabContacts.Solve(thighs[i],knees[i],feet[i],target,bend,1);MaxSupportError=Mathf.Max(MaxSupportError,Vector3.Distance(feet[i].position,target));lastFoot[i]=feet[i].position;}
            if(releasing&&weight<=0){contacts[0]=contacts[1]=0;Active=false;}
        }
        void OnDestroy(){if(sampler!=null)Destroy(sampler);}
    }
}

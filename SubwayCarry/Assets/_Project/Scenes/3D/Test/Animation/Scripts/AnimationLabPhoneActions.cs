using UnityEngine;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // The existing phone layer owns the upper pose and prop. Motor still owns all character movement.
    public sealed partial class AnimationLabPhoneLayer
    {
        public AnimationLabPhoneMode RequestedMode {get;set;}=AnimationLabPhoneMode.Read;
        public AnimationLabPhoneMode SettledMode {get;private set;}=AnimationLabPhoneMode.Stored;
        public bool IsTransitioning=>action!=null;
        public bool HasActionSet=>actionSetReady;
        public bool PhoneInHand=>inHand;
        public float LocomotionTime {get;set;}
        public float MaxHandoffError {get;private set;}
        public float LeftGripError {get;private set;}=-1;
        public int HandoffCount {get;private set;}
        public string AttachmentFault {get;private set;}="";
        public string CurrentActionName=>action!=null?action.displayName:SettledMode==AnimationLabPhoneMode.Read?"한 손으로 폰 보기":SettledMode==AnimationLabPhoneMode.Stored?"주머니 보관":hold!=null?hold.displayName:"";
        public AnimationLabAuthoredMotion CurrentSource=>action!=null?action.authored:hold!=null?hold.authored:SettledMode==AnimationLabPhoneMode.Read?data.authored:null;
        public float ActionClipTime=>action!=null?clock:hold!=null&&hold.role==AnimationLabPhoneAction.CarryLoop?Mathf.Repeat(LocomotionTime,hold.clip.length):hold!=null?Mathf.Repeat(holdTime,hold.clip.length):Mathf.Repeat(holdTime,data.upperBody.length);
        bool actionSetReady,inHand,eventDone,previousValid;
        AnimationLabPhoneMotion.ActionClip action,hold;
        AnimationLabPhoneMode actionEnd;
        float clock,endClock,entryBlend,holdTime;
        Quaternion[] previousPose,entryPose;
        bool[] rightMask;
        GameObject pocket,reference,referenceModel;
        Transform rUpper,rLower,lUpper,lLower,lHand;
        Transform[] referenceBones;
        void InitializeActions()
        {
            actionSetReady=data.HasActionSet;if(!actionSetReady)return;
            SettledMode=AnimationLabPhoneMode.Stored;RequestedMode=AnimationLabPhoneMode.Read;action=hold=null;clock=holdTime=0;inHand=previousValid=false;AttachmentFault="";MaxHandoffError=0;HandoffCount=0;
            previousPose=new Quaternion[targets.Length];entryPose=new Quaternion[targets.Length];rightMask=new bool[targets.Length];
            for(int i=0;i<targets.Length;i++)rightMask[i]=System.Array.IndexOf(data.upperPaths,data.bothArmPaths[i])>=0;
            rUpper=transform.Find(data.rightUpperPath);rLower=transform.Find(data.rightLowerPath);lUpper=transform.Find(data.leftUpperPath);lLower=transform.Find(data.leftLowerPath);lHand=transform.Find(data.leftHandPath);
            pocket=Instantiate(data.pocketPrefab,transform.Find(data.hipsPath),false);pocket.transform.localPosition=data.pocketLocalPosition;pocket.transform.localRotation=data.pocketLocalRotation;
            phone.transform.SetParent(pocket.transform,false);phone.transform.localPosition=data.pocketPhonePosition;phone.transform.localRotation=data.pocketPhoneRotation;
            reference=Instantiate(sample);reference.name="Carry reference · bones only";reference.hideFlags=HideFlags.HideAndDontSave;referenceModel=reference.transform.Find(data.animatorPath).gameObject;referenceBones=new Transform[targets.Length];for(int i=0;i<targets.Length;i++)referenceBones[i]=reference.transform.Find(data.bothArmPaths[i]);
        }
        void CleanupActions(){if(pocket!=null)Destroy(pocket);if(reference!=null)Destroy(reference);pocket=reference=null;actionSetReady=false;}
        void BeginAction(AnimationLabPhoneAction role,AnimationLabPhoneMode end,float start=0,float finish=1)
        {
            action=data.Find(role);actionEnd=end;clock=action.clip.length*start;endClock=action.clip.length*finish;entryBlend=0;eventDone=false;
            for(int i=0;i<entryPose.Length;i++)entryPose[i]=previousValid?previousPose[i]:baseRotations[i];
        }
        void Dispatch()
        {
            if(action!=null||RequestedMode==SettledMode)return;
            if(SettledMode==AnimationLabPhoneMode.Stored)
            {
                var take=data.Find(AnimationLabPhoneAction.TakeOut);bool carry=RequestedMode==AnimationLabPhoneMode.Carry;
                BeginAction(AnimationLabPhoneAction.TakeOut,carry?AnimationLabPhoneMode.Carry:AnimationLabPhoneMode.Read,0,carry?take.carryFrame/(float)take.authored.lastFrame:1);return;
            }
            if(SettledMode==AnimationLabPhoneMode.Landscape){BeginAction(AnimationLabPhoneAction.LandscapeExit,AnimationLabPhoneMode.Read);return;}
            if(RequestedMode==AnimationLabPhoneMode.Stored)
            {
                var put=data.Find(AnimationLabPhoneAction.PutAway);BeginAction(AnimationLabPhoneAction.PutAway,AnimationLabPhoneMode.Stored,SettledMode==AnimationLabPhoneMode.Carry?put.carryFrame/(float)put.authored.lastFrame:0);return;
            }
            if(SettledMode==AnimationLabPhoneMode.Carry){BeginAction(AnimationLabPhoneAction.Raise,AnimationLabPhoneMode.Read);return;}
            BeginAction(RequestedMode==AnimationLabPhoneMode.Carry?AnimationLabPhoneAction.Lower:AnimationLabPhoneAction.LandscapeEnter,RequestedMode);
        }
        void HandAnchor(out Vector3 position,out Quaternion rotation)
        {
            float t=SettledMode==AnimationLabPhoneMode.Landscape?1:0;
            if(action!=null&&action.role==AnimationLabPhoneAction.LandscapeEnter)t=Mathf.SmoothStep(0,1,Mathf.Clamp01(clock/action.clip.length));
            if(action!=null&&action.role==AnimationLabPhoneAction.LandscapeExit)t=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(clock/action.clip.length));
            position=Vector3.Lerp(data.phoneLocalPosition,data.landscapePhonePosition,t);rotation=Quaternion.Slerp(data.phoneLocalRotation,data.landscapePhoneRotation,t);
        }
        void ApplyActions(float dt)
        {
            for(int i=0;i<targets.Length;i++)baseRotations[i]=targets[i].localRotation;baseNeck=neck.localRotation;baseHead=head.localRotation;
            Dispatch();hold=SettledMode==AnimationLabPhoneMode.Carry?data.Find(AnimationLabPhoneAction.CarryLoop):SettledMode==AnimationLabPhoneMode.Landscape?data.Find(AnimationLabPhoneAction.LandscapeHold):null;
            var clip=action!=null?action.clip:hold!=null?hold.clip:data.upperBody;
            bool both=action!=null?AnimationLabPhoneMotion.BothArms(action.role):SettledMode==AnimationLabPhoneMode.Landscape;
            bool settle=action==null&&previousValid&&holdTime<.24f;
            if(action!=null||SettledMode!=AnimationLabPhoneMode.Stored||settle)
            {
                clip.SampleAnimation(sample,ActionClipTime);bool carry=action==null&&SettledMode==AnimationLabPhoneMode.Carry;if(carry)data.walk.SampleAnimation(referenceModel,Mathf.Repeat(LocomotionTime,data.walk.length));
                float mix=action!=null?Mathf.SmoothStep(0,1,Mathf.Clamp01(entryBlend/.22f)):settle?Mathf.SmoothStep(0,1,holdTime/.24f):1;
                for(int i=0;i<targets.Length;i++)if(both||rightMask[i]||settle)
                {
                    var target=(action==null&&SettledMode==AnimationLabPhoneMode.Stored)||(!both&&!rightMask[i])?baseRotations[i]:carry?(samples[i].localRotation*Quaternion.Inverse(referenceBones[i].localRotation))*baseRotations[i]:samples[i].localRotation;
                    targets[i].localRotation=Quaternion.Slerp(action!=null||settle?entryPose[i]:baseRotations[i],target,mix);
                }
            }
            HandAnchor(out var handPosition,out var handRotation);
            if(action!=null&&action.contactFrame>=0)
            {
                float n=clock/action.clip.length;bool transfer=action.role==AnimationLabPhoneAction.TakeOut?!inHand:inHand;
                if(transfer&&n>=action.contactStart)
                {
                    var expected=pocket.transform.TransformPoint(data.pocketPhonePosition);var expectedRotation=pocket.transform.rotation*data.pocketPhoneRotation;
                    var wristRotation=expectedRotation*Quaternion.Inverse(handRotation);var wristPosition=expected-wristRotation*handPosition;
                    float error=Vector3.Distance(wristPosition,hand.position);float weight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(action.contactStart,action.contactEnd,n));
                    if(error<.095f){AnimationLabContacts.Solve(rUpper,rLower,hand,wristPosition,Vector3.ProjectOnPlane(rLower.position-rUpper.position,wristPosition-rUpper.position),weight);hand.rotation=Quaternion.Slerp(hand.rotation,wristRotation,weight);}
                    float eventTime=action.contactFrame/(float)action.authored.sampleRate;
                    if(clock>=eventTime&&!eventDone)
                    {
                        float gap=Vector3.Distance(hand.TransformPoint(handPosition),expected);float angle=Quaternion.Angle(hand.rotation*handRotation,expectedRotation);
                        if(gap<.012f&&angle<8)
                        {
                            Vector3 before=phone.transform.position;phone.transform.SetParent(action.role==AnimationLabPhoneAction.TakeOut?hand:pocket.transform,true);inHand=action.role==AnimationLabPhoneAction.TakeOut;
                            phone.transform.localPosition=inHand?handPosition:data.pocketPhonePosition;phone.transform.localRotation=inHand?handRotation:data.pocketPhoneRotation;
                            MaxHandoffError=Mathf.Max(MaxHandoffError,Vector3.Distance(before,phone.transform.position));HandoffCount++;eventDone=true;AttachmentFault="";
                        }
                        else{clock=eventTime;AttachmentFault="주머니 접점 확인 필요";}
                    }
                }
            }
            if(inHand){phone.transform.localPosition=handPosition;phone.transform.localRotation=handRotation;}
            float grip=SettledMode==AnimationLabPhoneMode.Landscape?1:0;
            if(action!=null&&action.role==AnimationLabPhoneAction.LandscapeEnter)grip=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,.85f,clock/action.clip.length));
            if(action!=null&&action.role==AnimationLabPhoneAction.LandscapeExit)grip=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.55f,clock/action.clip.length));
            LeftGripError=-1;
            if(grip>0&&inHand)
            {
                var goal=phone.transform.TransformPoint(data.landscapeLeftPosition);float gap=Vector3.Distance(lHand.position,goal);
                if(gap<.095f){AnimationLabContacts.Solve(lUpper,lLower,lHand,goal,Vector3.ProjectOnPlane(lLower.position-lUpper.position,goal-lUpper.position),grip);lHand.rotation=Quaternion.Slerp(lHand.rotation,phone.transform.rotation*data.landscapeLeftRotation,grip);}
                if(grip>.99f)LeftGripError=Vector3.Distance(lHand.position,goal);
            }
            float gaze=SettledMode==AnimationLabPhoneMode.Read||SettledMode==AnimationLabPhoneMode.Landscape?1:0;
            if(action!=null)gaze=action.role==AnimationLabPhoneAction.TakeOut?Mathf.Min(.98f,clock/action.clip.length):action.role==AnimationLabPhoneAction.PutAway?Mathf.Max(0,1-clock/action.clip.length):action.role==AnimationLabPhoneAction.Raise?clock/action.clip.length:action.role==AnimationLabPhoneAction.Lower?1-clock/action.clip.length:1;
            Weight=Mathf.MoveTowards(Weight,gaze,dt*3);AimPhone(dt,Vector3.Lerp(LookTarget,phone.transform.position,Weight));
            for(int i=0;i<targets.Length;i++)previousPose[i]=targets[i].localRotation;previousValid=applied=true;entryBlend+=dt;holdTime+=dt;
            if(action!=null)
            {
                bool blocked=AttachmentFault.Length>0&&action.contactFrame>=0&&!eventDone;
                if(clock>=endClock&&!blocked){SettledMode=actionEnd;action=null;holdTime=0;for(int i=0;i<entryPose.Length;i++)entryPose[i]=previousPose[i];}
                else if(!blocked)
                {
                    float next=Mathf.Min(endClock,clock+dt);
                    if(!eventDone&&action.contactFrame>=0){float contact=action.contactFrame/(float)action.authored.sampleRate;if(clock<contact&&next>contact)next=contact;}
                    clock=next;
                }
            }
        }
        void AimPhone(float dt,Vector3 target)
        {
            var local=transform.InverseTransformDirection(target-head.position);float wy=Mathf.Clamp(Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg,-45,45),wp=Mathf.Clamp(-Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg,-15,22);
            yaw=Mathf.SmoothDampAngle(yaw,wy,ref yawVelocity,.22f,95,dt);pitch=Mathf.SmoothDampAngle(pitch,wp,ref pitchVelocity,.22f,75,dt);
            var turn=transform.rotation*Quaternion.Euler(pitch,yaw,0)*neutralHeadInActor*Quaternion.Inverse(head.rotation);var before=head.rotation;neck.rotation=Quaternion.Slerp(Quaternion.identity,turn,.55f)*neck.rotation;head.rotation=turn*before;
        }
    }
}

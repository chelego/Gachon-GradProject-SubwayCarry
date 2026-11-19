using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    internal enum Metro3DLifeAction { None, PhoneNotification, WrongNotification, CopyCarry, CheckBag, GuardianResponse }

    public sealed partial class Metro3DPassengerContacts
    {
        Metro3DLifeAction lifeAction;
        float lifeStart,lifeEnd,lifeCooldown,lifeCancel=-1;
        Vector3 lifeFocus;
        System.Guid lifeOwner;
        internal string LifeLabel => lifeAction switch {
            Metro3DLifeAction.PhoneNotification=>"알림 확인",
            Metro3DLifeAction.WrongNotification=>"자기 폰 알림인지 확인",
            Metro3DLifeAction.CopyCarry=>"상자 들기 따라 하기",
            Metro3DLifeAction.CheckBag=>"가방 잠금 확인",
            Metro3DLifeAction.GuardianResponse=>"아이 행동 확인",
            _=>""
        };
        internal bool LifeActive=>lifeAction!=Metro3DLifeAction.None;
        internal int LifePoseFrame{get;private set;}=-1;
        internal bool PhoneLeavingHands=>phonePhase==PhonePhase.Exit;
        internal bool CanMimicCarry=>CanLifeAction&&actor.world.Traffic.SupportFor(actor)==null;
        float LifeSecondsAvailable=>actor==null?0:actor.inTrain?
            actor.Riding!=null&&actor.Riding.Moving?actor.Riding.MovementRemaining-6:0:
            actor.Journey.CanBoard?actor.world.Transit.TimeToBoarding(actor)-1.5f:0;
        internal bool CanLifeAction=>actor!=null&&actor.SettledStanding&&!actor.PortalCommitted&&
            !actor.QueueActive&&!actor.Journey.OnStairs&&!actor.WantsToAlight&&actor.Seat==null&&
            actor.CompanionService==null&&actor.NarrowPoseWeight<.01f&&
            LifeSecondsAvailable>0;

        internal bool BeginLifeAction(Metro3DLifeAction action,Vector3 focus,float delay=0)
        {
            if(LifeActive||Time.time<lifeCooldown||!CanLifeAction||!rigReady)return false;
            float duration=action==Metro3DLifeAction.CopyCarry?4.6f:action==Metro3DLifeAction.CheckBag?3.8f:2.5f;
            // Budget the actual short action and delayed reaction before boarding.
            if(LifeSecondsAvailable<duration+delay+.45f)return false;
            bool glance=action==Metro3DLifeAction.PhoneNotification||action==Metro3DLifeAction.WrongNotification||action==Metro3DLifeAction.GuardianResponse;
            var support=actor.world.Traffic.SupportFor(actor);
            if(support!=null&&(!glance||!support.lean))return false;
            if(action==Metro3DLifeAction.CopyCarry&&HasPhoneInUse){
                // A noticed carrying action may draw the child's attention, but
                // free both hands through the existing authored put-away first.
                if(phonePhase==PhonePhase.Read&&LifeSecondsAvailable>duration+phoneLibrary.exit.pose.duration+.8f)
                    BeginPhoneExit();
                return false;
            }
            if(action==Metro3DLifeAction.PhoneNotification&&phonePhase!=PhonePhase.Read||
               !glance&&HasPhoneInUse||glance&&HasPhoneInUse&&phonePhase!=PhonePhase.Read)return false;
            lifeAction=action;lifeOwner=actor.PersistentId;lifeStart=Time.time+delay;
            lifeEnd=lifeStart+duration;
            lifeFocus=focus;lifeCancel=-1;lifeCooldown=Time.time+65;
            return true;
        }
        void ResetLifeAction()
        {
            lifeAction=Metro3DLifeAction.None;lifeCancel=-1;lifeCooldown=0;lifeOwner=System.Guid.Empty;LifePoseFrame=-1;
        }
        void TickLifeAction()
        {
            if(!LifeActive)return;
            if(actor.PersistentId!=lifeOwner){ResetLifeAction();return;}
            if((!CanLifeAction||!actor.world.settings.ambientEventsEnabled)&&lifeCancel<0)lifeCancel=Time.time;
            if(Time.time>=lifeEnd||lifeCancel>=0&&Time.time-lifeCancel>.22f){
                actor.RecordDecision(MetroDecision.Resume,lifeCancel>=0?MetroDecisionReason.ActivityChanged:MetroDecisionReason.EventFinished);
                lifeAction=Metro3DLifeAction.None;
            }
        }
        bool ApplyLifeAction()
        {
            if(!LifeActive||Time.time<lifeStart)return false;
            LifePoseFrame=Time.frameCount;
            float t=Time.time-lifeStart;
            float weight=Mathf.SmoothStep(0,1,t/.45f)*Mathf.SmoothStep(0,1,(lifeEnd-Time.time)/.65f);
            if(lifeCancel>=0)weight*=1-Mathf.SmoothStep(0,1,(Time.time-lifeCancel)/.22f);
            var up=actor.transform.up;var forward=actor.transform.forward;var right=actor.transform.right;
            var hips=Bone("Hips");float size=actor.HeightRatio;
            Vector3 focus=lifeFocus;
            if(lifeAction==Metro3DLifeAction.PhoneNotification){ApplyPhonePose();return true;}
            if(HasPhoneInUse)ApplyPhonePose();
            if(lifeAction==Metro3DLifeAction.WrongNotification&&HasPhoneInUse) {
                if(t<1.25f&&phoneProp!=null)focus=phoneProp.transform.position;
            } else if(lifeAction==Metro3DLifeAction.WrongNotification) {
                Vector3 pocket=hips.position+right*.15f*size-forward*.01f-up*.075f*size;
                Hand(arms[1],pocket,-up,-right,weight,false,true);
                if(t<1.25f)focus=pocket;
            } else if(lifeAction==Metro3DLifeAction.CheckBag) {
                // The wardrobe's satchel is weighted to lower spine, on the
                // actor's left side. Use its body frame, not a fixed world point.
                var lower=Bone("Spine");
                Vector3 bag=lower.position-right*.235f*size-forward*.035f*size-up*.07f*size;
                Hand(arms[0],bag,forward,-right,weight,false,true);
                focus=bag;
            } else if(lifeAction==Metro3DLifeAction.CopyCarry) {
                for(int i=0;i<2;i++) {
                    Vector3 palm=hips.position+forward*.22f*size+right*(i==0?-.14f:.14f)*size+up*.15f*size;
                    Hand(arms[i],palm,forward,up,weight,false,true);
                }
                if(t>2.9f&&actor.HasCompanion)focus=actor.Companion.transform.position+up*(1.55f*actor.Companion.HeightRatio);
            }else if(lifeAction==Metro3DLifeAction.GuardianResponse){
                if(actor.HasCompanion)focus=actor.Companion.transform.position+up*(1.4f*actor.Companion.HeightRatio);
            }
            if(bones.TryGetValue("DEF-head",out var h)) {
                Vector3 relative=actor.transform.InverseTransformDirection(focus-h.position);
                float yaw=Mathf.Clamp(Mathf.Atan2(relative.x,relative.z)*Mathf.Rad2Deg,-28,28);
                float pitch=Mathf.Clamp(-Mathf.Atan2(relative.y,new Vector2(relative.x,relative.z).magnitude)*Mathf.Rad2Deg,-18,18);
                Remember(h);h.rotation=Quaternion.AngleAxis(yaw*weight,up)*Quaternion.AngleAxis(pitch*weight,right)*h.rotation;
            }
            return true;
        }
    }
}

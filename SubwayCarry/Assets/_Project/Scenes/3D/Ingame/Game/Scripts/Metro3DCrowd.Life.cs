using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public sealed partial class Metro3DCrowd
    {
        readonly Metro3DPassenger[] lifeCandidates=new Metro3DPassenger[32];
        System.Random lifeRandom;
        float nextLifeCheck,nextRoutineEvent,lifeBusyUntil;
        public int LifeEventsStarted {get;private set;}
        public int LifeParticipants {get;private set;}

        void TickLife()
        {
            if(!world.settings.ambientEventsEnabled||world.player==null||world.session.SimulationPaused||
                Time.time<nextLifeCheck||Time.time<lifeBusyUntil)return;
            nextLifeCheck=Time.time+1.2f;
            lifeRandom??=new System.Random(world.settings.ambientEventSeed);
            var player=world.player;
            Query(player.transform.position,4);
            int n=0;
            foreach(var actor in nearby)if(n<lifeCandidates.Length&&actor!=null&&
                (actor.transform.position-player.transform.position).sqrMagnitude<100&&
                Mathf.Abs(actor.transform.position.y-player.transform.position.y)<1.3f)
                lifeCandidates[n++]=actor;
            if(n==0)return;
            int start=lifeRandom.Next(n);
            // Perceived opportunities are short-lived; do not suppress them for
            // the longer routine phone/bag cadence. Still one foreground event.
            for(int pass=0;pass<2;pass++){
            if(pass==1&&Time.time<nextRoutineEvent)break;
            for(int i=0;i<n;i++) {
                var actor=lifeCandidates[(start+i)%n];
                if(pass==0&&(actor.RiderLook!=Metro3DRiderLook.Child||!actor.HasCompanion))continue;
                var contact=actor.GetComponent<Metro3DPassengerContacts>();
                if(!contact.CanLifeAction||contact.LifeActive)continue;
                // Keep foreground actions out of the walking corridor. These
                // queries use the existing spatial index, never a scene scan.
                if(LocalDensity(actor.transform.position,actor.transform.parent)>.48f)continue;
                bool copy=actor.RiderLook==Metro3DRiderLook.Child&&actor.HasCompanion&&contact.CanMimicCarry&&
                    player.cargo!=null&&player.cargo.gameObject.activeInHierarchy&&
                    (actor.Companion.transform.position-actor.transform.position).sqrMagnitude<36&&
                    CanPerceive(actor,player.cargo.position,player.transform,true);
                var action=pass==0?(copy?Metro3DLifeAction.CopyCarry:Metro3DLifeAction.None):contact.HasPhoneInUse?Metro3DLifeAction.PhoneNotification:
                    actor.RiderLook==Metro3DRiderLook.Office?Metro3DLifeAction.CheckBag:Metro3DLifeAction.None;
                if(action==Metro3DLifeAction.None)continue;
                if(!contact.BeginLifeAction(action,player.transform.position+Vector3.up*1.15f)){
                    // Let the noticed action finish putting its phone away before
                    // starting an unrelated ambient event and losing this window.
                    if(action==Metro3DLifeAction.CopyCarry&&contact.PhoneLeavingHands){nextLifeCheck=Time.time+.25f;return;}
                    continue;
                }
                int participants=1;
                actor.RecordDecision(action==Metro3DLifeAction.CopyCarry?MetroDecision.SawCarry:action==Metro3DLifeAction.CheckBag?MetroDecision.CheckBag:MetroDecision.HeardPhone,
                    action==Metro3DLifeAction.CopyCarry?MetroDecisionReason.VisibleCarrying:action==Metro3DLifeAction.CheckBag?MetroDecisionReason.PersonalRoutine:MetroDecisionReason.AudibleNearby);
                if(action==Metro3DLifeAction.CopyCarry){
                    var guardian=actor.Companion;
                    Vector3 childEye=actor.transform.position+Vector3.up*actor.HeightRatio*1.4f;
                    if(CanPerceive(guardian,childEye,actor.transform,true)&&guardian.GetComponent<Metro3DPassengerContacts>()
                        .BeginLifeAction(Metro3DLifeAction.GuardianResponse,childEye,1.3f)){
                        participants++;guardian.RecordDecision(MetroDecision.GuardianResponse,MetroDecisionReason.VisibleChild);
                    }
                }
                if(action==Metro3DLifeAction.PhoneNotification) {
                    world.session.PhoneNotification(actor.transform.position);
                    for(int k=0;k<n&&participants<3;k++) {
                        var other=lifeCandidates[k];
                        if(other==actor||other.transform.parent!=actor.transform.parent||
                            (other.transform.position-actor.transform.position).sqrMagnitude>5.8f||
                            !CanPerceive(other,actor.transform.position+Vector3.up*actor.HeightRatio*1.3f,actor.transform,false))continue;
                        if(other.GetComponent<Metro3DPassengerContacts>().BeginLifeAction(Metro3DLifeAction.WrongNotification,
                            actor.transform.position+Vector3.up*actor.HeightRatio*1.3f,.35f+participants*.3f)){participants++;other.RecordDecision(MetroDecision.HeardPhone,MetroDecisionReason.AudibleNearby);}
                    }
                }
                LifeEventsStarted++;LifeParticipants+=participants;
                lifeBusyUntil=Time.time+7;nextRoutineEvent=Time.time+18+(float)lifeRandom.NextDouble()*15;
                return;
            }
            }
        }
    }
}

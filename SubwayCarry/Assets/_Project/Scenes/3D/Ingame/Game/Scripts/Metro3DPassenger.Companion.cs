using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public sealed partial class Metro3DPassenger
    {
        internal Metro3DTrain CompanionService { get; private set; }
        float companionBoardUntil, companionRetryAt, nextRegroup;
        int companionDoor;
        bool companionReturning,companionAdmissionStarted;

        internal bool CanFinishGroupBoarding(Metro3DTrain train)
            => HasCompanion && CompanionService==train && !companionReturning &&
               Time.time<companionBoardUntil && train!=null && train.DoorsOpen && !train.Moving;

        internal bool BeginCompanionBoarding(Metro3DTrain train,int preferredDoor=-1)
        {
            if(!HasCompanion)return true;
            if(CanFinishGroupBoarding(train))return true;
            var other=Companion;
            if(WaitForCompanion()||other.seat!=null||other.state==Activity.StandUp||
                other.PortalCommitted||other.Journey.OnStairs||
                world.Transit.BoardingSeconds(train)<8)return false;
            int chosen=preferredDoor>=0?preferredDoor:train.NearestDoor((transform.position+other.transform.position)*.5f);
            var entry=train.DoorPoint(chosen,true);
            if((entry-transform.position).sqrMagnitude>16||(entry-other.transform.position).sqrMagnitude>16)return false;
            CompanionService=other.CompanionService=train;
            companionDoor=other.companionDoor=chosen;
            // Queue time while passengers alight is not boarding time. The
            // bounded group crossing window begins at the first door claim.
            companionAdmissionStarted=other.companionAdmissionStarted=false;
            companionBoardUntil=other.companionBoardUntil=Time.time+world.Transit.BoardingSeconds(train);
            world.Traffic.TrackCompanionBoarding(this);
            world.Traffic.TrackCompanionBoarding(other);
            world.Traffic.JoinCompanionDoorQueue(this,other,train,chosen);
            return true;
        }

        // This is an existing door reservation extension, never a second Motor.
        // If a member cannot get in, the boarded member walks back out while
        // doors remain open. A timeout must not send a child off alone.
        internal bool CompanionNeedsOpenDoor(Metro3DTrain train)
            => CompanionService==train && HasCompanion &&
                (inTrain!=Companion.inTrain || DoorCrossing || Companion.DoorCrossing);

        internal void CompanionDoorClaimed(Metro3DTrain train)
        {
            if(!HasCompanion||CompanionService!=train||companionAdmissionStarted||companionReturning)return;
            companionAdmissionStarted=Companion.companionAdmissionStarted=true;
            companionBoardUntil=Companion.companionBoardUntil=Time.time+14;
        }

        void ClearCompanionBoarding()
        {
            world?.Traffic?.UntrackCompanionBoarding(this);
            CompanionService=null;companionBoardUntil=0;companionReturning=companionAdmissionStarted=false;
        }

        bool UpdateCompanionBoarding()
        {
            if(CompanionService==null)return false;
            if(!HasCompanion){ClearCompanionBoarding();return false;}
            var other=Companion;
            if(inTrain&&other.inTrain&&riding==other.riding) {
                ClearCompanionBoarding();other.ClearCompanionBoarding();
                triedSeat=false;other.triedSeat=false;return false;
            }
            if(Time.time>=companionBoardUntil||companionReturning||other.companionReturning) {
                // Do not reverse either capsule midway across an owned portal.
                if(DoorCrossing||other.DoorCrossing)return false;
                companionReturning=other.companionReturning=true;
                companionRetryAt=other.companionRetryAt=Time.time+7;
                if(inTrain) {
                    if(state==Activity.Sit){BeginStand();return true;}
                    if(state!=Activity.Alight) {
                        ReleaseSeat();world.Traffic.ReleaseStanding(this);
                        door=companionDoor;waypoint=0;state=Activity.Alight;
                        SetTarget(riding.DoorPoint(door,false),true);
                    }
                    AdvanceDoor(false);return true;
                }
                if(state==Activity.Board){world.Traffic.ReleaseDoor(this);boardingTrain=null;Journey.CancelBoard();SetWaitingPoint();}
                if(!other.inTrain) {ClearCompanionBoarding();other.ClearCompanionBoarding();}
                return true;
            }
            if(inTrain) {
                if(state!=Activity.Walk&&state!=Activity.Wait)WaitJourney();
                // Leave the threshold clear while the other member uses the
                // ordinary door queue. Don't claim a seat before both board.
                if(state==Activity.Wait&&!triedSeat) {
                    triedSeat=true;WalkJourney(world.Traffic.TrainStandingPoint(this,riding),true);
                }
                if(state==Activity.Walk&&FlatDistance(transform.position,target)<.23f)WaitJourney();
                return true;
            }
            return false;
        }

        bool RegroupOnPlatform()
        {
            if(!HasCompanion||inTrain||Companion.inTrain||!Journey.CanBoard||!Companion.Journey.CanBoard||
                Companion.transform.parent!=transform.parent||CompanionService!=null||PortalCommitted||Journey.OnStairs)return false;
            // The adult keeps a readable waiting location; the child joins a
            // real free adjacent point instead of copying the adult's position.
            if(RiderLook!=Metro3DRiderLook.Child)return false;
            float separation=Vector3.Distance(transform.position,Companion.transform.position);
            if(separation<1.65f)return false;
            if(seat!=null){if(state==Activity.Sit)BeginStand();return true;}
            if(state==Activity.Walk&&(CurrentGoal-Companion.transform.position).sqrMagnitude<2.56f&&
                FlatDistance(transform.position,CurrentGoal)>.24f&&Velocity.sqrMagnitude>.015f&&BlockedSeconds<1.2f)return true;
            if(Time.time<nextRegroup)return state==Activity.Walk;
            nextRegroup=Time.time+1.2f;
            if(world.Traffic.TryCompanionWaitingPoint(this,Companion,out var point)) {
                WalkJourney(point,true);return true;
            }
            return false;
        }
    }
}

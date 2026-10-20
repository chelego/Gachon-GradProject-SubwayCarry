using SubwayCarry.AI.V2;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // 3D spatial/action adapter for the team's existing full-journey planner/blackboard.
    // Destination is chosen once per lifetime, never by position, a timer or an index remainder at a stop.
    public sealed class Metro3DPassengerJourney : MonoBehaviour
    {
        public int OriginStop {get;private set;}
        public int DestinationStop {get;private set;}
        public string OriginId {get;private set;}
        public string DestinationId {get;private set;}
        public PassengerAiV2JourneyBlackboard Blackboard {get;private set;}
        public PassengerAiV2FullJourneyPlanner Planner {get;private set;}
        public bool Waiting => Blackboard.CurrentStage==PassengerAiV2FullJourneyStage.WaitForOriginTrain||TransferWaiting;
        public bool TransferWaiting {get;private set;}
        public bool AtDestination(int stop)=>stop>=DestinationStop;
        public float SpeedScale {get;private set;}
        public float SeatPreference {get;private set;}
        public PassengerAiV2RuntimePersonality Personality {get;private set;}
        Metro3DPassenger actor;
        Metro3DFacility gate;
        public bool CrossingGate => crossing;
        public bool OnStairs => stairCommitted;
        bool far,crossing,stairCommitted,tagging;
        float tagUntil;
        int gateSlot=-1;
        Vector3 commanded;
        bool hasCommand;
        public void Initialize(Metro3DPassenger passenger,int origin,bool inside,bool existing,bool farEntrance)
        {
            actor=passenger; OriginStop=origin; far=farEntrance;
            var route=actor.world.session.Route.stops;
            var random=new System.Random(unchecked(actor.index*7919+origin*104729+17));
            DestinationStop=random.Next(Mathf.Min(origin+1,route.Length-1),route.Length);
            OriginId=route[origin].id; DestinationId=route[DestinationStop].id;
            SpeedScale=Mathf.Lerp(.86f,1.12f,(float)random.NextDouble()); SeatPreference=Mathf.Lerp(.25f,.88f,(float)random.NextDouble());
            var profiles=actor.world.settings.deliveries.passengerProfiles;
            Personality=profiles!=null&&profiles.Length>0&&profiles[actor.index%profiles.Length]!=null
                ?profiles[actor.index%profiles.Length].CreateRuntimePersonality(actor.index):PassengerAiV2RuntimePersonality.CreateFallback(actor.index);
            SpeedScale*=Mathf.Clamp(Personality.WalkingSpeed/1.35f,.7f,1.3f);
            SeatPreference=Mathf.Clamp01(SeatPreference*.5f+Personality.SeatPreference*.5f);
            Blackboard=GetComponent<PassengerAiV2JourneyBlackboard>();
            if(Blackboard==null) Blackboard=gameObject.AddComponent<PassengerAiV2JourneyBlackboard>();
            Planner=GetComponent<PassengerAiV2FullJourneyPlanner>();
            if(Planner==null) Planner=gameObject.AddComponent<PassengerAiV2FullJourneyPlanner>();
            Planner.Initialize(Blackboard); gate=null; tagging=crossing=stairCommitted=TransferWaiting=hasCommand=false; gateSlot=-1;
            if(existing) {
                Complete(PassengerAiV2FullJourneyStage.EnterOriginStation); Blackboard.MarkEntryCardTagged();
                Complete(PassengerAiV2FullJourneyStage.PassOriginEntryGate);Complete(PassengerAiV2FullJourneyStage.ReachOriginPlatform);
                if(inside) {Complete(PassengerAiV2FullJourneyStage.WaitForOriginTrain);OnBoard();}
            }
        }
        void Complete(PassengerAiV2FullJourneyStage stage) {Planner.CompleteCurrentStage(stage);hasCommand=false;}
        void Move(Vector3 local,bool navigate)
        {
            Vector3 goal=actor.transform.parent.TransformPoint(local);
            if(hasCommand&&(goal-commanded).sqrMagnitude<.02f) return;
            hasCommand=true;commanded=goal;actor.WalkJourney(goal,navigate);
        }
        bool Reached(Vector3 local,float tolerance=.25f)
        {
            Vector3 p=actor.transform.parent.InverseTransformPoint(actor.transform.position)-local;
            return new Vector2(p.x,p.z).magnitude<tolerance&&Mathf.Abs(p.y)<.4f;
        }
        public bool Tick()
        {
            var stage=Blackboard.CurrentStage;
            switch(stage) {
                case PassengerAiV2FullJourneyStage.EnterOriginStation:
                    // Leave the entrance throat before committing to a particular gate lane.
                    var enter=new Vector3(far?-.55f:.55f,actor.world.settings.departureFloor,far?118.8f:-18.7f);
                    Move(enter,true); if(Reached(enter)) Complete(stage);return true;
                case PassengerAiV2FullJourneyStage.PassOriginEntryGate:
                case PassengerAiV2FullJourneyStage.PassDestinationExitGate:
                    Gate(stage==PassengerAiV2FullJourneyStage.PassDestinationExitGate);return true;
                case PassengerAiV2FullJourneyStage.ReachOriginPlatform:
                case PassengerAiV2FullJourneyStage.ReachDestinationConcourse:
                    Stair(stage==PassengerAiV2FullJourneyStage.ReachDestinationConcourse);return true;
                case PassengerAiV2FullJourneyStage.LeaveDestinationStation:
                    var exit=Metro3DStationTraffic.Entrance(far,actor.world.settings.departureFloor);
                    exit.x=far?.55f:-.55f;
                    Move(exit,true);
                    if(Reached(exit)) {Blackboard.MarkJourneyCompleted();Complete(stage);actor.world.Traffic.Recycle(actor,true);}
                    return true;
            }
            return false;
        }
        void Gate(bool outgoing)
        {
            var traffic=actor.world.Traffic;
            if(gate!=null&&(!gate.isActiveAndEnabled||gate.gateDoor==null||!gate.gateDoor.isActiveAndEnabled)) {
                traffic.ReleaseGate(actor);gate=null;crossing=tagging=false;Planner.ReportRecoverableFailure("Fare gate unavailable; release and choose again");
            }
            if(gate==null) {gate=traffic.SelectGate(actor,far,outgoing);gateSlot=-1;hasCommand=tagging=false;}
            if(gate==null) {Planner.ReportRecoverableFailure("No cached fare-gate bank for station");return;}
            float direction=(far?-1:1)*(outgoing?-1:1);
            Vector3 p=gate.transform.localPosition; p.y=actor.world.settings.departureFloor;
            if(!crossing) {
                int slot=traffic.GateSlot(actor,gate);
                Vector3 approach=actor.transform.parent.InverseTransformPoint(traffic.GateApproach(actor,gate,direction));
                if(gateSlot!=slot) {gateSlot=slot;hasCommand=false;}
                actor.FollowQueueGoal(actor.transform.parent.TransformPoint(approach));
                if(slot!=0||!Reached(approach,.08f)) {tagging=false;return;}
                actor.WaitJourney();
                if(!tagging) {tagging=true;tagUntil=Time.time+.25f+actor.QueueReactionDelay;}
                if(Time.time<tagUntil) return;
                if(!traffic.GateTurn(actor,gate)) return;
                crossing=true; hasCommand=false;
                if(outgoing) Blackboard.MarkExitCardTagged(); else Blackboard.MarkEntryCardTagged();
            }
            gate.gateDoor.HoldForPassenger();
            Vector3 feet=actor.transform.parent.InverseTransformPoint(actor.transform.position);
            if(Mathf.Abs(feet.x-p.x)>.09f) {
                // Align inside the reserved approach/corridor before moving
                // forward. The previous .25m arrival tolerance exceeds the
                // real lane's capsule clearance beside its steel housing.
                Move(new Vector3(p.x,p.y,feet.z),false);return;
            }
            Vector3 beyond=p+Vector3.forward*direction*1.15f; Move(beyond,false);
            if(!Reached(beyond)) return;
            traffic.ReleaseGate(actor);gate=null;crossing=false;gateSlot=-1;
            Complete(Blackboard.CurrentStage);
        }
        void Stair(bool ascending)
        {
            Vector3 top=Metro3DStationTraffic.StairTop(far,actor.world.ServiceSide,actor.world.settings.departureFloor);
            Vector3 bottom=Metro3DStationTraffic.StairBottom(far,actor.world.ServiceSide);
            // Opposing flows keep to separate sides of the actual stair tread.
            float lane=ascending?-.26f:.26f;top.x+=lane;bottom.x+=lane;
            Vector3 entry=ascending?bottom:top,exit=ascending?top:bottom;
            Vector3 feet=actor.transform.parent.InverseTransformPoint(actor.transform.position);
            if(stairCommitted&&Mathf.Abs(feet.x+actor.world.ServiceSide*7.76f)>.6f&&Mathf.Abs(feet.y-entry.y)<.4f) {
                stairCommitted=false;hasCommand=false;
            }
            if(!stairCommitted) {
                Move(entry,true);if(!Reached(entry)) return;
                stairCommitted=true;hasCommand=false;
            }
            Move(exit,false);
            if(!Reached(exit)) return;
            stairCommitted=false;Complete(Blackboard.CurrentStage);
            if(!ascending) {actor.WaitJourney();actor.SetWaitingPoint();}
        }
        public void BeginBoard()
        {
            if(TransferWaiting) return;
            Complete(PassengerAiV2FullJourneyStage.WaitForOriginTrain);
        }
        public void CancelBoard() {if(!TransferWaiting) Blackboard.SetStage(PassengerAiV2FullJourneyStage.BoardOriginTrain);}
        public bool CanBoard => Waiting||Blackboard.CurrentStage==PassengerAiV2FullJourneyStage.BoardOriginTrain;
        public void OnBoard()
        {
            if(TransferWaiting) {TransferWaiting=false;return;}
            Blackboard.MarkBoardedTrain();Complete(PassengerAiV2FullJourneyStage.BoardOriginTrain);
        }
        public void ActivityChosen()
        {
            if(Blackboard.CurrentStage!=PassengerAiV2FullJourneyStage.ChooseTrainActivity) return;
            Blackboard.MarkTrainActivityUsed();Complete(PassengerAiV2FullJourneyStage.ChooseTrainActivity);
        }
        public void PreparingToAlight() {Blackboard.MarkPreparedToAlight();}
        public void OnAlight(int stop)
        {
            if(!AtDestination(stop)) {TransferWaiting=true;actor.SetWaitingPoint();return;}
            Complete(PassengerAiV2FullJourneyStage.RideAndPrepareToAlight);
            Blackboard.MarkAlightedTrain();Complete(PassengerAiV2FullJourneyStage.AlightAtDestination);
            far=actor.transform.localPosition.z>Metro3DWorld.PlatformLength*.5f;stairCommitted=hasCommand=false;
        }
    }
}

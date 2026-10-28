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
        public string DestinationLabel {get;private set;}
        public bool HasModeledDestination {get;private set;}
        public PassengerAiV2JourneyBlackboard Blackboard {get;private set;}
        public PassengerAiV2FullJourneyPlanner Planner {get;private set;}
        public bool Waiting => Blackboard.CurrentStage==PassengerAiV2FullJourneyStage.WaitForOriginTrain||TransferWaiting;
        public bool TransferWaiting {get;private set;}
        public int TravelDirection {get;private set;}=1;
        public int TrainSide => actor.world.ServiceSide*TravelDirection;
        public bool AtDestination(int stop)=>HasModeledDestination&&(TravelDirection>0?stop>=DestinationStop:stop<=DestinationStop);
        public float SpeedScale {get;private set;}
        public float SeatPreference {get;private set;}
        public PassengerAiV2RuntimePersonality Personality {get;private set;}
        Metro3DPassenger actor;
        Metro3DFacility gate;
        public enum GatePhase { None, Approach, Queue, Align, Tag, DoorWait, Cross, Exit, ExitClear }
        public GatePhase GateAction {get;private set;}
        public Metro3DFacility ReservedGate => gate;
        public int GateQueueSlot => gateSlot;
        public float TagRemaining => tagging?Mathf.Max(0,tagUntil-Time.time):0;
        public int ContinuationMisses {get;private set;}
        // Diagnostic compatibility; orientation data is owned by the passenger executor.
        public Vector3 GateFacing => gate!=null?actor.BodyFacingIntent:Vector3.zero;
        public bool CrossingGate => crossing;
        public bool OnStairs => stairCommitted;
        public bool StairApproaching {get;private set;}
        public bool ReviewStairActive {get;private set;}
        public string ReviewStairPhase=>!ReviewStairActive?"평지":stairCommitted?"계단 통과":"계단 진입 준비";
        Vector3 reviewEntry,reviewExit,reviewAfter;
        bool reviewStairFinished,reviewApproachReady;
        float reviewStairHalfWidth=.5f;
        public void SetReviewStair(Vector3 entry,Vector3 exit,Vector3 after,float halfWidth=.5f)
        {
            reviewEntry=entry;reviewExit=exit;reviewAfter=after;reviewStairHalfWidth=halfWidth;reviewStairFinished=reviewApproachReady=false;ReviewStairActive=StairApproaching=true;stairCommitted=false;hasCommand=false;
        }
        public bool ReviewStairSurface(Vector3 current,Vector3 next)
        {
            Vector3 axis=reviewExit-reviewEntry;axis.y=0;axis.Normalize();
            Vector3 side=Vector3.Cross(Vector3.up,axis);
            float before=Mathf.Abs(Vector3.Dot(current-reviewEntry,side)),after=Mathf.Abs(Vector3.Dot(next-reviewEntry,side));
            return after<reviewStairHalfWidth||after<before-.002f;
        }
        public void TickReviewStair()
        {
            if(!ReviewStairActive)return;
            if(!actor.WalkReviewCandidate) {
                if(!stairCommitted){Move(reviewEntry,true);if(!Reached(reviewEntry))return;stairCommitted=true;hasCommand=false;}
            } else if(!stairCommitted) {
                Vector3 axis=reviewExit-reviewEntry;axis.y=0;axis.Normalize();
                // Prepare the flight tangent on the landing, rather than first
                // discovering it after a lateral arrival at the entry point.
                float lead=Mathf.Clamp(actor.world.settings.npcSpeed*SpeedScale/(actor.ComfortableTurnRate*Mathf.Deg2Rad)+.25f,.65f,1.15f);
                Vector3 leadPoint=reviewEntry-axis*lead;
                Vector3 toEntry=reviewEntry-actor.transform.localPosition;toEntry.y=0;
                if(!reviewApproachReady&&toEntry.sqrMagnitude>.01f&&Vector3.Dot(toEntry.normalized,axis)>.7071f)reviewApproachReady=true;
                if(!reviewApproachReady){MoveStairLanding(leadPoint);Vector3 toLead=actor.transform.localPosition-leadPoint;toLead.y=0;if(toLead.magnitude>Mathf.Max(.45f,actor.Velocity.magnitude*.6f))return;reviewApproachReady=true;hasCommand=false;}
                MoveStairLanding(reviewEntry);
                Vector3 offset=actor.transform.localPosition-reviewEntry;
                float distance=Vector3.Dot(offset,axis),lateral=Mathf.Abs(Vector3.Dot(offset,Vector3.Cross(Vector3.up,axis)));
                // A bounded entrance portal, before the first tread. No position snap.
                if(distance<-.5f||distance>.4f||lateral>Mathf.Min(.45f,reviewStairHalfWidth)||Mathf.Abs(offset.y)>.4f)return;
                stairCommitted=true;hasCommand=false;
            }
            StairApproaching=false;Move(reviewExit,false);
            if(!Reached(reviewExit,.3f))return;
            stairCommitted=false;ReviewStairActive=false;reviewStairFinished=true;hasCommand=false;Move(reviewAfter,false);
        }
        void MoveStairLanding(Vector3 point)
        {
            Vector3 worldPoint=actor.transform.parent.TransformPoint(point);
            if(hasCommand&&(worldPoint-commanded).sqrMagnitude<.02f)return;
            // Keep a valid same-floor diagonal; do not project an upper landing
            // onto a lower planar grid solely because their X/Z coordinates match.
            Move(point,!actor.CanWalkDirect(worldPoint));
        }
        bool far,crossing,stairCommitted,tagging,exitLease;
        float tagUntil;
        float gateProgressAt;
        Vector3 gateProgressPosition;
        bool gateOutgoing;
        int gateSlot=-1;
        Vector3 commanded;
        bool hasCommand;
        internal static int ChooseDestination(int origin,int count,int direction,System.Random random)
        {
            if(count<2||origin<0||origin>=count)throw new System.ArgumentOutOfRangeException(nameof(origin));
            if(origin==0)direction=1;else if(origin==count-1)direction=-1;
            if(direction>0)return random.Next(origin+1,count);
            if(direction<0)return random.Next(0,origin);
            int destination=random.Next(0,count-1);
            return destination>=origin?destination+1:destination;
        }
        public void Initialize(Metro3DPassenger passenger,int origin,bool inside,bool existing,bool farEntrance)
        {
            ReviewStairActive=StairApproaching=reviewStairFinished=false;
            actor=passenger; OriginStop=origin; far=farEntrance;
            var route=actor.world.session.Route.stops;
            var random=new System.Random(unchecked(actor.index*7919+origin*104729+17));
            int direction=inside&&actor.Riding!=null?actor.Riding.TravelDirection:actor.InitialDirection;
            OriginId=route[origin].id;
            actor.world.Traffic.ChoosePassengerDestination(origin,inside,direction,random,out int destination,out string id,out string label,out int travelDirection);
            DestinationStop=destination;DestinationId=id;DestinationLabel=label;TravelDirection=travelDirection;
            HasModeledDestination=destination>=0&&destination!=origin&&(destination>origin?1:-1)==TravelDirection;
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
            Planner.Initialize(Blackboard); gate=null; tagging=crossing=exitLease=stairCommitted=TransferWaiting=hasCommand=false; gateSlot=-1;
            GateAction=GatePhase.None;actor.FaceTravel();ContinuationMisses=0;
            if(existing) {
                Complete(PassengerAiV2FullJourneyStage.EnterOriginStation); Blackboard.MarkEntryCardTagged();
                Complete(PassengerAiV2FullJourneyStage.PassOriginEntryGate);Complete(PassengerAiV2FullJourneyStage.ReachOriginPlatform);
                if(inside) {Complete(PassengerAiV2FullJourneyStage.WaitForOriginTrain);OnBoard();}
            }
        }
        void Complete(PassengerAiV2FullJourneyStage stage) {Planner.CompleteCurrentStage(stage);hasCommand=false;}
        public void EnterConcourse() {Complete(PassengerAiV2FullJourneyStage.EnterOriginStation);}
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
                    // Retain the entrance branch rather than funnel everybody through one centre point.
                    var enter=actor.transform.localPosition;
                    enter.x=Mathf.Clamp(enter.x,-1.3f,1.3f);enter.y=actor.world.settings.departureFloor;enter.z=far?118.8f:-18.7f;
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
                CancelGate();actor.WaitJourney();Planner.ReportRecoverableFailure("Fare gate unavailable; reservation released");return;
            }
            if(gate==null) {
                gate=traffic.SelectGate(actor,far,outgoing);gateSlot=-1;hasCommand=tagging=false;
                gateProgressAt=Time.time;gateProgressPosition=actor.transform.position;gateOutgoing=outgoing;GateAction=GatePhase.Approach;
            }
            if(gate==null) {Planner.ReportRecoverableFailure("No cached fare-gate bank for station");return;}
            float direction=(far?-1:1)*(outgoing?-1:1);
            Vector3 p=gate.transform.localPosition; p.y=actor.world.settings.departureFloor;
            Vector3 axis=actor.transform.parent.TransformDirection(Vector3.forward*direction);
            if((actor.transform.position-gateProgressPosition).sqrMagnitude>.04f) {gateProgressPosition=actor.transform.position;gateProgressAt=Time.time;}
            if(!crossing) {
                Vector3 approach=actor.transform.parent.InverseTransformPoint(traffic.GateApproach(actor,gate,direction));
                int slot=traffic.GateSlot(actor,gate);
                if(gateSlot!=slot) {gateSlot=slot;hasCommand=false;}
                if(tagging&&slot!=0) {tagging=false;actor.CancelJourneyContinuation();}
                if(!tagging) {
                    GateAction=slot<0?GatePhase.Approach:slot==0?GatePhase.Align:GatePhase.Queue;
                    actor.FollowQueueGoal(actor.transform.parent.TransformPoint(approach));
                    if(slot<0&&!traffic.GateAtCapacity(gate)&&Time.time-gateProgressAt>8) {CancelGate();Planner.ReportRecoverableFailure("Gate approach blocked; release selection and retry");return;}
                    if(slot!=0||!Reached(approach,.08f))return;
                    actor.FaceJourney(axis);actor.HoldJourney();
                    if(actor.Velocity.sqrMagnitude>.0144f||Vector3.Dot(actor.transform.forward,axis)<.97f)return;
                    tagging=true;tagUntil=Time.time+.25f+actor.QueueReactionDelay;GateAction=GatePhase.Tag;
                    actor.PrepareJourneyContinuation(actor.transform.parent.TransformPoint(p+Vector3.forward*direction*(actor.GateClearanceDistance(gate.gateDoor.passageHalfLength)+.08f)),actor.transform.parent.TransformPoint(AfterGate(outgoing)));
                }
                actor.FaceJourney(axis);actor.HoldJourney();
                if(Time.time<tagUntil) {GateAction=GatePhase.Tag;return;}
                GateAction=GatePhase.DoorWait;
                if(!traffic.GateTurn(actor,gate))return;
                crossing=true;tagging=false;hasCommand=false;GateAction=GatePhase.Cross;
                if(outgoing) Blackboard.MarkExitCardTagged(); else Blackboard.MarkEntryCardTagged();
            }
            gate.gateDoor.HoldForPassenger();
            // Aim beyond the completion plane; do not brake to a waypoint inside the exit clearance.
            Move(p+Vector3.forward*direction*(actor.GateClearanceDistance(gate.gateDoor.passageHalfLength)+.5f),false);
            actor.FaceJourney(axis);
            FinishGatePassage();
        }
        Vector3 AfterGate(bool outgoing)
        {
            if(outgoing) {var exit=Metro3DStationTraffic.Entrance(far,actor.world.settings.departureFloor);exit.x=far?.55f:-.55f;return exit;}
            var top=Metro3DStationTraffic.StairTop(far,TrainSide,actor.world.settings.departureFloor);top.x+=.26f;return top;
        }
        // Called after the single motor step as well as the low-frequency action tick.
        public void FinishGatePassage()
        {
            if(exitLease&&gate!=null) {
                Vector3 local=actor.transform.localPosition-gate.transform.localPosition;
                float flow=(far?-1:1)*(gateOutgoing?-1:1);
                gate.gateDoor.HoldForPassenger();
                if(Mathf.Abs(local.x)>.65f||local.z*flow>actor.GateClearanceDistance(gate.gateDoor.passageHalfLength)+.65f) {
                    actor.world.Traffic.ReleaseGate(actor);gate=null;gateSlot=-1;exitLease=false;
                }
                return;
            }
            if(!crossing||gate==null)return;
            float direction=(far?-1:1)*(gateOutgoing?-1:1);
            Vector3 feet=actor.transform.localPosition,p=gate.transform.localPosition;
            float progress=(feet.z-p.z)*direction;
            if(progress<gate.gateDoor.passageHalfLength+.24f+.14f||Mathf.Abs(feet.x-p.x)>.12f)return;
            GateAction=GatePhase.ExitClear;
            // Ownership ends by spatial clearance, not by a release timer. Keep
            // the successor out while this body still needs the turning outlet.
            if(progress<actor.GateClearanceDistance(gate.gateDoor.passageHalfLength))return;
            Vector3 next=actor.transform.parent.TransformPoint(AfterGate(gateOutgoing));
            // Keep the FIFO head until the turning body clears the outlet.
            // Its motor is free to follow the prepared next path meanwhile.
            actor.ClearQueueContext();exitLease=true;crossing=false;GateAction=GatePhase.Exit;
            Complete(Blackboard.CurrentStage);
            hasCommand=true;commanded=next;
            if(!actor.ContinueJourney(next))ContinuationMisses++;
        }
        public void CancelGate()
        {
            if(actor!=null&&actor.world!=null&&actor.world.Traffic!=null)actor.world.Traffic.ReleaseGate(actor);
            gate=null;crossing=tagging=exitLease=hasCommand=false;gateSlot=-1;GateAction=GatePhase.None;
            if(actor!=null) {actor.FaceTravel();actor.CancelJourneyContinuation();}
        }
        void OnDisable() {CancelGate();}
        void Stair(bool ascending)
        {
            if(stairCommitted&&GateAction==GatePhase.Exit)GateAction=GatePhase.None;
            Vector3 top=Metro3DStationTraffic.StairTop(far,TrainSide,actor.world.settings.departureFloor);
            Vector3 bottom=Metro3DStationTraffic.StairBottom(far,TrainSide);
            // Opposing flows keep to separate sides of the actual stair tread.
            float lane=ascending?-.26f:.26f;top.x+=lane;bottom.x+=lane;
            Vector3 entry=ascending?bottom:top,exit=ascending?top:bottom;
            if(actor.WalkReviewCandidate) {
                // Same entrance-portal execution used by Walk's minimal fixture;
                // normal ingame passengers never opt into it before review.
                if(!ReviewStairActive&&!reviewStairFinished){Vector3 tangent=exit-entry;tangent.y=0;SetReviewStair(entry,exit,exit+tangent.normalized*.55f,.28f);}
                TickReviewStair();if(!reviewStairFinished)return;
                reviewStairFinished=false;Complete(Blackboard.CurrentStage);
                if(!ascending){actor.WaitJourney();actor.SetWaitingPoint();}return;
            }
            Vector3 feet=actor.transform.parent.InverseTransformPoint(actor.transform.position);
            if(stairCommitted&&Mathf.Abs(feet.x+TrainSide*7.76f)>.6f&&Mathf.Abs(feet.y-entry.y)<.4f) {
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
        public bool CanBoard => HasModeledDestination&&(Waiting||Blackboard.CurrentStage==PassengerAiV2FullJourneyStage.BoardOriginTrain);
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

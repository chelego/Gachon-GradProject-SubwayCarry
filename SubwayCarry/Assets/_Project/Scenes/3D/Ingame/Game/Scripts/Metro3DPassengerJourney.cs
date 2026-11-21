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
        internal bool IsEnteringStation =>
            Blackboard.CurrentStage==PassengerAiV2FullJourneyStage.EnterOriginStation ||
            Blackboard.CurrentStage==PassengerAiV2FullJourneyStage.PassOriginEntryGate;
        public bool TransferWaiting {get;private set;}
        public int TravelDirection {get;private set;}=1;
        public int TrainSide => actor.world.Transit.PlatformSideAt(actor.stationIndex,TravelDirection);
        internal Metro3DStationLayout Layout => actor.world.LayoutAt(actor.stationIndex);
        Vector3 StairTop(int side) => Layout ? Layout.Stair(side,true) : Metro3DStationTraffic.StairTop(far,side,actor.world.settings.departureFloor);
        Vector3 StairBottom(int side) => Layout ? Layout.Stair(side,false) : Metro3DStationTraffic.StairBottom(far,side);
        float StairHeading(int side) => Layout ? Mathf.Sign(StairBottom(side).z-StairTop(side).z) : (far?-1f:1f);
        // Physical support belongs to the current leg, not the next boarding choice.
        internal int SurfaceSide=>arrivalSide!=0&&(transferMotionStage==1||AtDestination(actor.stationIndex))?arrivalSide:TrainSide;
        public string NextLine=>actor.world.Transit.LineAt(actor.stationIndex,TravelDirection);
        int transferMotionStage,transferFromSide,arrivalSide;
        public string TransferMotion=>transferMotionStage==1?"도착 승강장 → 연결통로":transferMotionStage==2?"연결통로 이동":transferMotionStage==3?"환승 승강장으로 내려가기":TransferWaiting?"환승 열차 대기":"없음";
        public bool AtDestination(int stop)=>HasModeledDestination&&stop==DestinationStop;
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
        internal bool CardMixup {get;private set;}
        internal float TagProgress=>Mathf.InverseLerp(tagStarted,tagUntil,Time.time);
        float tagStarted;
        public int ContinuationMisses {get;private set;}
        // Diagnostic compatibility; orientation data is owned by the passenger executor.
        public Vector3 GateFacing => gate!=null?actor.BodyFacingIntent:Vector3.zero;
        public bool CrossingGate => crossing;
        public bool OnStairs => stairCommitted;
        internal Vector3 StairForward {get;private set;}
        internal Vector3 StairOutward {get;private set;}
        bool stairNextPrepared,stairNextAvailable;
        Vector3 stairNext,stairLanding;
        Vector3? descendingWaitPoint;
        internal Vector3? DescendingWaitPoint=>descendingWaitPoint;
        float landingExtension;
        float landingProgressAt;
        float landingRemaining;
        internal bool ClearingStairLanding {get;private set;}
        const float StairLaneOffset=.30f;
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
        bool far,crossing,stairCommitted,aligning,tagging,exitLease;
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
            transferMotionStage=transferFromSide=arrivalSide=0;
            ReviewStairActive=StairApproaching=reviewStairFinished=false;
            StairForward=StairOutward=stairLanding=Vector3.zero;
            stairNextPrepared=ClearingStairLanding=false;
            descendingWaitPoint=null;
            actor=passenger; OriginStop=origin; far=farEntrance&&!Layout;
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
            Planner.Initialize(Blackboard); gate=null; aligning=tagging=crossing=exitLease=stairCommitted=TransferWaiting=hasCommand=false; gateSlot=-1;
            GateAction=GatePhase.None;CardMixup=false;actor.FaceTravel();ContinuationMisses=0;
            if(existing) {
                Complete(PassengerAiV2FullJourneyStage.EnterOriginStation); Blackboard.MarkEntryCardTagged();
                Complete(PassengerAiV2FullJourneyStage.PassOriginEntryGate);Complete(PassengerAiV2FullJourneyStage.ReachOriginPlatform);
                if(inside) {Complete(PassengerAiV2FullJourneyStage.WaitForOriginTrain);OnBoard();}
            }
        }
        internal void UseRiderLook(Metro3DRiderLook look)
        {
            if(look==Metro3DRiderLook.Senior) {SpeedScale*=.89f;SeatPreference=Mathf.Clamp01(SeatPreference+.13f);}
            if(look==Metro3DRiderLook.Child) {SpeedScale*=.83f;SeatPreference=Mathf.Clamp01(SeatPreference+.07f);}
        }
        internal void ShareDestination(Metro3DPassengerJourney companion)
        {
            DestinationStop=companion.DestinationStop;DestinationId=companion.DestinationId;
            DestinationLabel=companion.DestinationLabel;TravelDirection=companion.TravelDirection;
            HasModeledDestination=companion.HasModeledDestination;
        }
        void Complete(PassengerAiV2FullJourneyStage stage) {Planner.CompleteCurrentStage(stage);hasCommand=false;}
        internal void ArrivesAtHub(int origin,string id,int direction)
        {
            OriginStop=Mathf.Max(0,origin);OriginId=id;
            DestinationStop=0;DestinationId="gachon";DestinationLabel=actor.world.StopLabel(0);
            TravelDirection=direction;HasModeledDestination=true;
        }
        internal void RestoreRider(Metro3DStationTraffic.RiderSnapshot rider)
        {
            OriginStop=rider.origin;OriginId=rider.originId;DestinationStop=rider.stop;
            DestinationId=rider.destinationId;DestinationLabel=rider.label;TravelDirection=rider.direction;
            HasModeledDestination=rider.stop>=0;SpeedScale=rider.speed;SeatPreference=rider.seatPreference;
        }
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
            if(transferMotionStage>0){TickTransfer();return true;}
            var stage=Blackboard.CurrentStage;
            switch(stage) {
                case PassengerAiV2FullJourneyStage.EnterOriginStation:
                    var layout = Layout;
                    if (layout && layout.preferDirectEntranceWalk)
                    {
                        Vector3 entranceGoal = layout.Local(layout.unpaidHall);
                        entranceGoal.x += Mathf.Clamp(actor.transform.localPosition.x, -1.3f, 1.3f);
                        // This is a hall boundary. Riders can choose their gate
                        // once they have reached it, even after stepping sideways.
                        if (Reached(entranceGoal, 1f))
                        {
                            Complete(stage);
                            return true;
                        }
                        // Keep one goal for the entrance leg. Steering around another
                        // rider must not replace an in-flight request or its result.
                        if (!hasCommand)
                        {
                            bool direct = actor.CanWalkDirect(actor.transform.parent.TransformPoint(entranceGoal));
                            // The walking motor's existing blocked recovery requests
                            // a path to this same goal if local avoidance stalls.
                            Move(entranceGoal, !direct);
                        }
                        return true;
                    }
                    // Leave the entrance throat before committing to a particular gate lane.
                    // Retain the entrance branch rather than funnel everybody through one centre point.
                    var enter=actor.transform.localPosition;
                    enter.x=Mathf.Clamp(enter.x,-1.3f,1.3f);enter.y=actor.world.settings.departureFloor;enter.z=far?118.8f:-18.7f;
                    if(layout) enter=layout.Local(layout.unpaidHall)+Vector3.right*enter.x;
                    Move(enter,true); if(Reached(enter)) Complete(stage);return true;
                case PassengerAiV2FullJourneyStage.PassOriginEntryGate:
                case PassengerAiV2FullJourneyStage.PassDestinationExitGate:
                    Gate(stage==PassengerAiV2FullJourneyStage.PassDestinationExitGate);return true;
                case PassengerAiV2FullJourneyStage.ReachOriginPlatform:
                case PassengerAiV2FullJourneyStage.ReachDestinationConcourse:
                    Stair(stage==PassengerAiV2FullJourneyStage.ReachDestinationConcourse);return true;
                case PassengerAiV2FullJourneyStage.LeaveDestinationStation:
                    Vector3 exit=ExitDestination();
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
                gate=traffic.SelectGate(actor,far,outgoing);gateSlot=-1;hasCommand=aligning=tagging=false;
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
                if(slot!=0) {
                    aligning=false;
                    if(tagging) {tagging=false;actor.CancelJourneyContinuation();}
                }
                if(!tagging) {
                    GateAction=slot<0?GatePhase.Approach:slot==0?GatePhase.Align:GatePhase.Queue;
                    if(aligning&&!Reached(approach,.22f))aligning=false;
                    if(!aligning) {
                        Vector3 feet=actor.transform.localPosition;
                        // A follower's target is a spacing estimate, not a place to walk
                        // backwards to when the person ahead slows down.
                        if(slot>0&&Mathf.Abs(feet.x-approach.x)<.24f&&(approach.z-feet.z)*direction<=.18f) {
                            actor.FaceJourney(axis);
                            actor.HoldJourney();
                            return;
                        }
                        if(slot!=0||!Reached(approach,.14f)||Mathf.Abs(feet.x-approach.x)>.09f) {
                            var target=actor.transform.parent.TransformPoint(approach);
                            if(outgoing&&slot<0)actor.ApproachExitGate(target);
                            else actor.FollowQueueGoal(target,preferDirect:true);
                            if(slot<0&&!traffic.GateAtCapacity(gate)&&Time.time-gateProgressAt>8) {
                                CancelGate();Planner.ReportRecoverableFailure("Gate approach blocked; release selection and retry");
                            }
                            return;
                        }
                        aligning=true;
                    }
                    actor.FaceJourney(axis);actor.HoldJourney();
                    if(actor.Velocity.sqrMagnitude>.0144f||Vector3.Dot(actor.transform.forward,axis)<.97f)return;
                    aligning=false;tagging=true;tagStarted=Time.time;
                    CardMixup=actor.world.settings.ambientEventsEnabled&&(!outgoing&&actor.RiderLook==Metro3DRiderLook.Office&&actor.index%23==7&&!traffic.GateHasFollower(gate));
                    tagUntil=tagStarted+.25f+actor.QueueReactionDelay+(CardMixup?.48f:0);
                    GateAction=GatePhase.Tag;
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
        Vector3 ExitDestination()
        {
            var layout = Layout;
            Vector3 exit = layout ? layout.Local(layout.exit ? layout.exit : layout.entrance)
                : Metro3DStationTraffic.Entrance(far,actor.world.settings.departureFloor);
            // Spread targets without spilling back into the opposing lane or stair wall.
            float spread = layout && layout.exit ? .22f : 1.55f;
            uint choice=unchecked((uint)actor.index*2654435761u+(uint)actor.stationIndex*2246822519u);
            exit.x+=Mathf.Lerp(-spread,spread,(choice>>16)/65535f);
            return exit;
        }
        Vector3 AfterGate(bool outgoing)
        {
            if(outgoing)return ExitDestination();
            var top=StairTop(TrainSide);
            top.x+=StairLaneOffset;
            // Prepare exactly the approach point that the stair action takes
            // ownership of, otherwise its first tick drops the prepared path.
            return top-Vector3.forward*(StairHeading(TrainSide)*.65f);
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
            if(progress<actor.GateClearanceDistance(gate.gateDoor.passageHalfLength)||Mathf.Abs(feet.x-p.x)>.12f)return;
            Vector3 next=actor.transform.parent.TransformPoint(AfterGate(gateOutgoing));
            // Keep the FIFO head until the turning body clears the outlet.
            // Its motor is free to follow the prepared next path meanwhile.
            actor.ClearQueueContext();exitLease=true;crossing=false;GateAction=GatePhase.Exit;
            Complete(Blackboard.CurrentStage);
            hasCommand=true;commanded=next;
            Vector3 forward=actor.transform.parent.TransformDirection(Vector3.forward*direction);
            if(!actor.ContinueJourney(next,forward))ContinuationMisses++;
        }
        public void CancelGate()
        {
            if(actor!=null&&actor.world!=null&&actor.world.Traffic!=null)actor.world.Traffic.ReleaseGate(actor);
            gate=null;aligning=crossing=tagging=exitLease=hasCommand=false;gateSlot=-1;GateAction=GatePhase.None;
            if(actor!=null) {actor.FaceTravel();actor.CancelJourneyContinuation();}
        }
        void OnDisable() {CancelGate();}
        void Stair(bool ascending,int sideOverride=0,bool transfer=false)
        {
            if(stairCommitted&&GateAction==GatePhase.Exit)GateAction=GatePhase.None;
            int stairSide=sideOverride!=0?sideOverride:ascending&&arrivalSide!=0?arrivalSide:TrainSide;
            Vector3 top=StairTop(stairSide);
            Vector3 bottom=StairBottom(stairSide);
            // Opposing flows keep to separate sides of the actual stair tread.
            float lane=ascending?-StairLaneOffset:StairLaneOffset;top.x+=lane;bottom.x+=lane;
            Vector3 entry=ascending?bottom:top,exit=ascending?top:bottom;
            if(actor.WalkReviewCandidate) {
                // Same entrance-portal execution used by Walk's minimal fixture;
                // normal ingame passengers never opt into it before review.
                if(!ReviewStairActive&&!reviewStairFinished){Vector3 tangent=exit-entry;tangent.y=0;SetReviewStair(entry,exit,exit+tangent.normalized*.55f,.28f);}
                TickReviewStair();if(!reviewStairFinished)return;
                reviewStairFinished=false;if(transfer){FinishTransferStair(ascending);return;}Complete(Blackboard.CurrentStage);
                if(!ascending){actor.WaitJourney();actor.SetWaitingPoint();}return;
            }
            Vector3 feet=actor.transform.parent.InverseTransformPoint(actor.transform.position);
            Vector3 axis=exit-entry;axis.y=0;axis.Normalize();
            float along=Vector3.Dot(feet-entry,axis);
            var frame=actor.transform.parent;
            Vector3 forward=frame.TransformDirection(axis);
            if(ClearingStairLanding) {
                Vector3 remaining=stairLanding-feet;remaining.y=0;
                float distance=remaining.magnitude;
                if(distance<=.24f&&Mathf.Abs(stairLanding.y-feet.y)<.18f)FinishStairLanding();
                else if(distance<landingRemaining) {
                    landingRemaining=distance-.15f;
                    landingProgressAt=Time.time;
                } else if(Time.time-landingProgressAt>2.5f) {
                    // A checked route can still fill after the rider commits.
                    // Try a nearby outlet before waiting for a planar path.
                    if(TryFindStairOutlet(exit,axis)) {
                        landingRemaining=float.PositiveInfinity;
                        Move(stairLanding,false);
                    }
                    Vector3 goal=frame.TransformPoint(stairLanding);
                    if(actor.CanWalkDirect(goal))actor.TryWalkStairApproach(goal);
                    else {
                        hasCommand=false;
                        Move(stairLanding,true);
                    }
                    landingProgressAt=Time.time;
                }
                return;
            }
            StairOutward=frame.TransformDirection(Vector3.right*Mathf.Sign(lane));
            // Commitment starts on the flat, before the first tread. A rider
            // still on that flat may leave the entry lane to let climbers out.
            if(!ascending&&Mathf.Abs(feet.y-entry.y)<.16f&&along>=-4.1f&&along<=.40f&&
                Mathf.Abs(feet.x-entry.x)<3.4f&&actor.HasAscendingAtStair(top-Vector3.right*lane,bottom-Vector3.right*lane,axis)&&
                YieldAtStairLanding(entry,axis,stairSide))return;
            descendingWaitPoint=null;
            if(!stairCommitted) {
                float lateral=Mathf.Abs(feet.x-entry.x);
                bool onLanding=Mathf.Abs(feet.y-entry.y)<.4f;
                // Lock the straight approach before an ordinary avoidance
                // turn takes someone out of the lane at the foot of the flight.
                bool aligned=onLanding&&along>=-3.6f&&along<-.32f&&lateral<.72f;
                if(aligned&&!StairApproaching) {
                    Vector3 point=actor.transform.parent.TransformPoint(entry);
                    if(actor.TryWalkStairApproach(point))StairApproaching=true;
                } else if(!aligned)StairApproaching=false;
                StairForward=StairApproaching?forward:Vector3.zero;
                if(!onLanding||along<-.32f||along>.65f||lateral>.20f) {
                    // From the side, join the lane just before the flight.
                    // A path ending at the first step can overshoot the entry
                    // and ask the actor to walk back into oncoming traffic.
                    if(!StairApproaching) {
                        Vector3 join=entry-axis*.65f;
                        if(onLanding&&along>=-.32f&&along<=.65f&&lateral<.72f)
                            join=entry+axis*Mathf.Max(0,along);
                        MoveStairLanding(join);
                    }
                    return;
                }
                // Once inside, keep the flight destination through crowd contact.
                stairCommitted=true;hasCommand=false;StairApproaching=false;landingExtension=0;
                StairForward=forward;
            }
            // The authored entrance/exit markers sit .8 m out on the landing.
            // Start the next leg once the capsule clears the actual end of the
            // flight. The current straight aim remains beyond that plane.
            Vector3 clearLanding=exit+axis*(actor.WalkingRadius+.14f-.8f);
            if((!ascending||!transfer)&&!stairNextPrepared&&Mathf.Abs(feet.y-clearLanding.y)<(ascending?.8f:.65f)&&
                Vector3.Dot(clearLanding-feet,axis)<2.2f) {
                if(ascending) {
                    PlanStairLanding(exit,clearLanding,axis,stairSide);
                    gate=actor.world.Traffic.SelectGate(actor,far,true);
                    stairNextAvailable=gate!=null;
                    if(stairNextAvailable)stairNext=actor.world.Traffic.OutgoingGateApproach(actor,gate,far);
                } else {
                    stairNext=actor.world.Traffic.PlatformWaitingPoint(actor);
                    stairNextAvailable=(stairNext-actor.transform.position).sqrMagnitude>.04f;
                }
                if(stairNextAvailable) {
                    Vector3 start=stairLanding!=Vector3.zero?stairLanding:clearLanding+axis*landingExtension;
                    actor.PrepareJourneyContinuation(frame.TransformPoint(start),stairNext);
                }
                stairNextPrepared=true;
            }
            clearLanding+=axis*landingExtension;
            Move(clearLanding+axis*.38f,false);
            if(Mathf.Abs(feet.y-exit.y)>.18f)return;
            if(Vector3.Dot(feet-clearLanding,axis)<0) {
                // Once both feet are on the landing, don't insist on walking
                // straight into a blocked turn point. Check the real footprint
                // from here, including the railing edge and continuous floor.
                if(!ascending||transfer||stairLanding==Vector3.zero||
                    Vector3.Dot(feet-exit,axis)<-.70f||!TryFindStairOutlet(exit,axis))return;
            }
            stairCommitted=false;StairForward=StairOutward=Vector3.zero;
            if(transfer){FinishTransferStair(ascending);return;}
            if(ascending) {
                if(stairLanding!=Vector3.zero) {
                    ClearingStairLanding=true;
                    landingProgressAt=Time.time;landingRemaining=float.PositiveInfinity;
                    Move(stairLanding,false);
                } else FinishStairLanding();
                return;
            }
            Complete(Blackboard.CurrentStage);
            if(stairNextPrepared&&stairNextAvailable)actor.ContinueToPlatformWait(stairNext,forward);
            else actor.SetWaitingPoint();
            stairNextPrepared=false;
        }

        bool TryFindStairOutlet(Vector3 top,Vector3 axis)
        {
            for(int i=0;i<5;i++) {
                float shift=i==0?0:(i%2==0?1:-1)*(i<=2?.65f:1.2f);
                Vector3 point=stairLanding+axis*shift;
                float beyond=Vector3.Dot(point-top,axis);
                if(beyond<-.35f||beyond>2.8f||!actor.CanWaitOffStair(point))continue;
                if(i!=0) {
                    stairLanding=point;
                    actor.CancelJourneyContinuation();
                    if(stairNextAvailable)actor.PrepareJourneyContinuation(actor.transform.parent.TransformPoint(point),stairNext);
                }
                return true;
            }
            return false;
        }

        bool YieldAtStairLanding(Vector3 entry,Vector3 descent,int side)
        {
            if(descendingWaitPoint==null) {
                var feet=actor.transform.localPosition;
                Vector3 inward=Vector3.right*side;
                Vector3 center=entry-Vector3.right*StairLaneOffset;
                float lateral=Vector3.Dot(feet-center,inward);
                float beyond=Vector3.Dot(center-feet,descent);
                if((lateral>2.45f||lateral<-.62f)&&beyond>1.5f) {
                    descendingWaitPoint=feet;
                } else {
                    // Use distinct spots beyond the exit corridor. On layouts
                    // with a wall at the outer rail, fall back deeper into the hall.
                    for(int i=0;i<8;i++) {
                        bool outside=i<4;
                        float depth=outside?1.65f+i*.36f:.25f+(i-4)*.50f;
                        float across=outside?-.65f:2.50f;
                        Vector3 hold=center-descent*depth+inward*across;
                        if(actor.CanWaitOffStair(hold)) {descendingWaitPoint=hold;break;}
                    }
                    if(descendingWaitPoint==null) {
                        if(beyond<=2.6f&&(beyond<=1.1f||lateral>=-.3f))return false;
                        descendingWaitPoint=feet;
                    }
                }
                hasCommand=false;
            }
            StairApproaching=stairCommitted=false;
            StairForward=StairOutward=Vector3.zero;
            Move(descendingWaitPoint.Value,false);
            return true;
        }

        void PlanStairLanding(Vector3 marker,Vector3 stepsEnd,Vector3 axis,int side)
        {
            var frame=actor.transform.parent;
            Vector3 start=frame.TransformPoint(stepsEnd);
            // The marker is already beyond the top tread. Check the straight
            // segment and the diagonal separately while the rider is still climbing.
            Vector3 towardHall=Vector3.right*side;
            for(float extra=.35f;extra<=1.55f;extra+=.4f) {
                Vector3 turn=marker+axis*extra;
                if(!actor.CanClearLanding(start,frame.TransformPoint(turn+axis*.38f)))continue;
                Vector3 exit=turn+towardHall*1.65f+axis*.55f;
                if(!actor.CanClearLanding(frame.TransformPoint(turn),frame.TransformPoint(exit),pastStair:true))continue;
                stairLanding=exit;landingExtension=Vector3.Dot(turn-stepsEnd,axis);
                return;
            }
            // Some landings only offer space in front of the stairs.
            Vector3 exitStraight=marker+axis*1.4f;
            if(actor.CanClearLanding(start,frame.TransformPoint(exitStraight+axis*.38f)))
                landingExtension=Vector3.Dot(exitStraight-stepsEnd,axis);
        }

        void FinishStairLanding()
        {
            ClearingStairLanding=false;
            Complete(Blackboard.CurrentStage);
            if(gate!=null&&stairNextPrepared&&stairNextAvailable) {
                gateOutgoing=true;gateSlot=-1;gateProgressAt=Time.time;gateProgressPosition=actor.transform.position;
                GateAction=GatePhase.Approach;
                Vector3 towardGate=Vector3.ProjectOnPlane(stairNext-actor.transform.position,Vector3.up).normalized;
                if(!actor.ContinueJourney(stairNext,towardGate))ContinuationMisses++;
            } else Gate(true);
            stairLanding=Vector3.zero;stairNextPrepared=false;
        }
        void FinishTransferStair(bool ascending)
        {
            hasCommand=false;stairNextPrepared=false;transferMotionStage=ascending?2:0;
            if(!ascending)actor.SetWaitingPoint();
        }
        void TickTransfer()
        {
            if(transferMotionStage==1){Stair(true,transferFromSide,true);return;}
            if(transferMotionStage==3){Stair(false,TrainSide,true);return;}
            Vector3 top=StairTop(TrainSide);
            // Existing paid-area connection and stair entrances, no rail crossing.
            top.z-=StairHeading(TrainSide)*1.2f;
            Move(top,true);
            if(Reached(top,.35f)){transferMotionStage=3;hasCommand=false;}
        }
        public void BeginBoard()
        {
            if(TransferWaiting) return;
            Complete(PassengerAiV2FullJourneyStage.WaitForOriginTrain);
        }
        public void CancelBoard() {if(!TransferWaiting) Blackboard.SetStage(PassengerAiV2FullJourneyStage.BoardOriginTrain);}
        public bool CanBoard => transferMotionStage==0&&(HasModeledDestination||OriginStop==0&&TravelDirection<0)
            &&(Waiting||Blackboard.CurrentStage==PassengerAiV2FullJourneyStage.BoardOriginTrain);
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
        public void PreparingToAlight()
        {
            // A rider already at the next stop can prepare before selecting a cabin activity.
            if(Blackboard.CurrentStage==PassengerAiV2FullJourneyStage.ChooseTrainActivity)
                Complete(PassengerAiV2FullJourneyStage.ChooseTrainActivity);
            Blackboard.MarkPreparedToAlight();
        }
        public void OnAlight(int stop,int fromSide=0,string fromLine=null)
        {
            arrivalSide=fromSide;
            stairLanding=Vector3.zero;ClearingStairLanding=stairNextPrepared=false;
            descendingWaitPoint=null;
            if(!AtDestination(stop)) {
                TransferWaiting=true;
                if(fromSide!=0&&fromLine!=null&&fromLine!=NextLine&&actor.world.session.Route.stops[stop].transfer){
                    transferMotionStage=1;transferFromSide=fromSide;far=!Layout&&actor.transform.localPosition.z>Metro3DWorld.PlatformLength*.5f;
                    stairCommitted=hasCommand=StairApproaching=stairNextPrepared=false;return;
                }
                actor.SetWaitingPoint();return;
            }
            Complete(PassengerAiV2FullJourneyStage.RideAndPrepareToAlight);
            Blackboard.MarkAlightedTrain();Complete(PassengerAiV2FullJourneyStage.AlightAtDestination);
            far=!Layout&&actor.transform.localPosition.z>Metro3DWorld.PlatformLength*.5f;stairCommitted=hasCommand=false;
        }
    }
}

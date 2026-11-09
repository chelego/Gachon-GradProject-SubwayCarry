using System.Collections.Generic;
using SubwayCarry.Prototype.ArtMapSlice;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // One lifetime/arrival and smart-object service. Agent decisions use cached local queries.
    [DefaultExecutionOrder(-40)]
    public sealed class Metro3DStationTraffic : MonoBehaviour
    {
        public Metro3DWorld world;
        public int SpawnedCount {get;private set;}
        public int CompletedCount {get;private set;}
        public int EntranceArrivals {get;private set;}
        public int BackgroundPooled {get;private set;}
        public int LogicalArrivals {get;private set;}
        public int PoolCount => pool.Count;
        public int DoorClaimCount => doorOwners.Count;
        public int BackgroundRiderCount {
            get {
                int count=offMapRiders.Count;
                foreach(var riders in detachedRiders.Values)count+=riders.Count;
                return count;
            }
        }

        internal sealed class RiderSnapshot
        {
            public System.Guid id;
            public int index,origin,stop,direction;
            public string originId,destinationId,label;
            public float due,speed,seatPreference;
        }

        readonly Dictionary<Metro3DTrain,List<RiderSnapshot>> detachedRiders=new();
        readonly List<RiderSnapshot> offMapRiders=new();
        System.Random trainArrivals;
        static System.Random TrainRandom()
        {
#if UNITY_EDITOR
            if(Application.isBatchMode) {
                var args=System.Environment.GetCommandLineArgs();
                int at=System.Array.IndexOf(args,"-journeySeed");
                if(at>=0&&at+1<args.Length&&int.TryParse(args[at+1],out int seed))return new System.Random(seed);
            }
#endif
            return new System.Random();
        }
        readonly Stack<Metro3DPassenger> pool=new Stack<Metro3DPassenger>();
        readonly Dictionary<Transform,int> occupancy=new Dictionary<Transform,int>();
        readonly HashSet<Transform> seeded=new HashSet<Transform>();
        readonly Dictionary<Transform,Metro3DFacility[]> gates=new Dictionary<Transform,Metro3DFacility[]>();
        readonly Dictionary<Metro3DFacility,List<Metro3DPassenger>> gateQueues=new Dictionary<Metro3DFacility,List<Metro3DPassenger>>();
        // A selected lane is intent, not a place in the physical FIFO.
        readonly Dictionary<Metro3DPassenger,Metro3DFacility> gateSelections=new Dictionary<Metro3DPassenger,Metro3DFacility>();
        readonly Dictionary<Metro3DFacility,int> gateIntentCounts=new Dictionary<Metro3DFacility,int>();
        const int GateQueueCapacity=6;
        public int GateSelectionCount => gateSelections.Count;
        readonly Dictionary<(Metro3DTrain,int,int),Metro3DPassenger> doorOwners=new Dictionary<(Metro3DTrain,int,int),Metro3DPassenger>();
        readonly Dictionary<(Metro3DTrain,int,bool),List<Metro3DPassenger>> doorQueues=new Dictionary<(Metro3DTrain,int,bool),List<Metro3DPassenger>>();
        readonly Dictionary<Metro3DPassenger,int> doorSides=new Dictionary<Metro3DPassenger,int>();
        readonly HashSet<Metro3DPassenger> gateCrossings=new HashSet<Metro3DPassenger>();
        readonly Dictionary<Metro3DPassenger,Vector3> standing=new Dictionary<Metro3DPassenger,Vector3>();
        public sealed class SupportSpot
        {
            public Metro3DTrain train;
            public Transform frame;
            public Vector3 feet,grip,normal;
            // Keep the phone hand on the open side of a side lean.
            public Vector3 LeanFacing=>sideLean?Vector3.Cross(normal,Vector3.up).normalized:normal;
            public bool lean,doorSide,screenDoor,sideLean;
            public Collider surface;
            public Metro3DPassenger occupant;
        }
        readonly Dictionary<Metro3DTrain,List<SupportSpot>> supports=new Dictionary<Metro3DTrain,List<SupportSpot>>();
        readonly Dictionary<Transform,List<SupportSpot>> platformSupports=new Dictionary<Transform,List<SupportSpot>>();
        readonly Dictionary<Metro3DPassenger,SupportSpot> supportClaims=new Dictionary<Metro3DPassenger,SupportSpot>();
        readonly Collider[] contacts=new Collider[16];
        readonly RaycastHit[] ground=new RaycastHit[16];
        readonly System.Random arrivals=new System.Random(20261022);
        Transform dormant;
        float nextArrival;
        int serial,entranceSerial;
        struct Demand {public string id,label;public int physicalDirection;}
        readonly Dictionary<string,List<Demand>> backgroundDemand=new Dictionary<string,List<Demand>>();
        int demandRevision=-1;
        // Catalog demand is independent of the currently accepted delivery.
        // This is a first-line adapter, not a new route graph or a transfer implementation.
        public void ChoosePassengerDestination(int origin,bool aboard,int direction,System.Random random,out int stop,out string id,out string label,out int travelDirection)
        {
            var route=world.session.Route.stops;
            if(!aboard||origin==0&&direction<0) {
                if(demandRevision!=world.RouteRevision)BuildBackgroundDemand();
                if(backgroundDemand.TryGetValue(route[origin].id,out var choices)&&choices.Count>0) {
                    int count=0;
                    foreach(var choice in choices)if(IsServedDemand(choice,origin,direction,route,out _))count++;
                    if(count>0) {
                        int pick=random.Next(count);
                        foreach(var choice in choices) {
                            if(!IsServedDemand(choice,origin,direction,route,out int modeledStop)||pick--!=0)continue;
                            id=choice.id;label=choice.label;travelDirection=choice.physicalDirection*world.ServiceSide;stop=modeledStop;
                            return;
                        }
                    }
                }
            }
            stop=Metro3DPassengerJourney.ChooseDestination(origin,route.Length,direction,random);
            id=route[stop].id;label=route[stop].label;travelDirection=stop>origin?1:-1;
        }
        bool IsServedDemand(Demand choice,int origin,int direction,SliceRouteStop[] route,out int modeledStop)
        {
            modeledStop=-1;
            int heading=choice.physicalDirection*world.ServiceSide;
            if(direction!=0&&direction!=heading)return false;
            for(int i=origin+heading;i>=0&&i<route.Length&&route[i].line==route[origin].line;i+=heading)
                if(route[i].id==choice.id) {modeledStop=i;return true;}
            // Only the opposite platform at the hub has a service that
            // continues beyond the currently constructed route.
            return origin==0&&heading<0;
        }
        void BuildBackgroundDemand()
        {
            backgroundDemand.Clear();demandRevision=world.RouteRevision;
            foreach(var order in world.settings.deliveries.orders) {
                var stops=order.stops;
                if(stops==null||stops.Length<2||stops[0].id!="gachon")continue;
                // Existing catalog's first leg: south toward Moran, north toward Bokjeong/Suseo.
                int physical=stops[1].id=="moran"?1:stops[1].id=="bokjeong"||stops[1].id=="suseo"?-1:0;
                if(physical==0)continue;
                int end=stops.Length-1;
                for(int i=1;i<stops.Length;i++)if(stops[i].line!=stops[0].line) {end=i-1;break;}
                for(int i=0;i<=end;i++)for(int j=0;j<=end;j++) {
                    if(i==j||stops[i].id==stops[j].id)continue;
                    if(!backgroundDemand.TryGetValue(stops[i].id,out var list)) {list=new List<Demand>();backgroundDemand[stops[i].id]=list;}
                    int heading=(j>i?1:-1)*physical;bool duplicate=false;
                    foreach(var item in list)if(item.id==stops[j].id&&item.physicalDirection==heading){duplicate=true;break;}
                    if(!duplicate)list.Add(new Demand{id=stops[j].id,label=stops[j].label,physicalDirection=heading});
                }
            }
        }
        public bool HasPassengers(Transform root)=>occupancy.TryGetValue(root,out int count)&&count>0;
        public int CountAt(Transform root)=>occupancy.TryGetValue(root,out int count)?count:0;
        void Adjust(Transform root,int amount) {if(root!=null) occupancy[root]=Mathf.Max(0,CountAt(root)+amount);}
        public void ChangedParent(Transform previous,Transform current) {Adjust(previous,-1);Adjust(current,1);world.GetComponent<Metro3DCrowd>().InvalidateSpatial();}

        void Update()
        {
            if(world==null||world.session==null||world.session.SimulationPaused||Time.time<nextArrival) return;
            nextArrival=Time.time+world.settings.passengerArrivalSeconds*(.8f+(float)arrivals.NextDouble()*.8f);
            for(int i=offMapRiders.Count-1;i>=0;i--) {
                if(Time.time<offMapRiders[i].due)continue;
                LogicalArrivals++;
                offMapRiders.RemoveAt(i);
            }
            // The hub keeps receiving people even when the player never leaves its concourse.
            // Other physically constructed stations run the same arrival stream.
            for(int stop=0;stop<world.stations.Count;stop++) {
                Transform root=world.stations[stop];
                if(CountAt(root)>=world.settings.passengerCount) continue;
                int group=arrivals.Next(2,5);
                for(int person=0;person<group&&CountAt(root)<world.settings.passengerCount;person++) {
                    int entry=entranceSerial++%3;bool far=entry!=0;
                    Vector3 p=entry==2?new Vector3(14.6f,world.settings.departureFloor,55+Metro3DWorld.PlatformExpansion):Entrance(far,world.settings.departureFloor);
                    p.x+=((float)arrivals.NextDouble()-.5f)*1.1f;
                    p.z+=((float)arrivals.NextDouble()-.5f)*.8f;
                    if(!SpawnClear(root,p)) continue;
                    Rent(stop,false,p,false,far,null,0,entry==2);EntranceArrivals++;
                }
            }
        }
        bool SpawnClear(Transform root,Vector3 local)
        {
            Vector3 point=root.TransformPoint(local);
            int n=Physics.OverlapCapsuleNonAlloc(point+Vector3.up*.32f,point+Vector3.up*1.45f,.28f,contacts,~0,QueryTriggerInteraction.Ignore);
            if(n!=0)return false;
            int hits=Physics.RaycastNonAlloc(point+Vector3.up*.35f,Vector3.down,ground,.7f,~0,QueryTriggerInteraction.Ignore);
            if(hits==ground.Length)return false;
            for(int i=0;i<hits;i++)if(!(ground[i].collider is CharacterController)&&ground[i].transform.IsChildOf(root)&&ground[i].normal.y>.65f&&Mathf.Abs(ground[i].point.y-point.y)<.1f)return true;
            return false;
        }
        public static Vector3 Entrance(bool far,float floor)=>new Vector3(0,floor,far?62.5f+Metro3DWorld.PlatformExpansion:-25f);
        public static Vector3 StairTop(bool far,int side,float floor)=>new Vector3(-side*7.76f,floor,far?107.7f+.8f:-.8f);
        public static Vector3 StairBottom(bool far,int side)=>new Vector3(-side*7.76f,0,far?Metro3DWorld.FarStairBottom-.8f:11.9f);

        public void Seed(int stop,bool inside)
        {
            if(inside){SeedTrain(world.train,stop);return;}
            Transform root=world.stations[stop];
            if(!seeded.Add(root)) return;
            Physics.SyncTransforms();
            int count=world.settings.passengerCount;
            for(int i=0;i<count;i++) {
                bool concourse=i>=count/2,far=(i&1)!=0;
                int direction=0;
                // Preview the same deterministic lifetime choice before testing its actual platform floor.
                // Do not test a point on the track then mirror it to an unchecked platform afterwards.
                if(!concourse)ChoosePassengerDestination(stop,false,0,new System.Random(unchecked(serial*7919+stop*104729+17)),out _,out _,out _,out direction);
                Vector3 p=concourse?new Vector3(-4+(i%4)*2.5f,world.settings.departureFloor,far?117+(i/4%3)*2.1f:-14+(i/4%3)*2.2f)
                    :new Vector3(-world.ServiceSide*direction*(4.6f+(i%2)*.78f),0,Metro3DTrain.DoorZ[i%Metro3DTrain.DoorZ.Length]+(i%2==0?-.95f:.95f));
                if(!SpawnClear(root,p))continue;
                Rent(stop,false,p,!concourse,concourse?far:p.z>Metro3DWorld.PlatformLength*.5f,null,0,concourse);
            }
        }
        public void SeedTrain(Metro3DTrain train,int stop,bool incomingHub=false)
        {
            using(var seed=SeedTrainCars(train,stop,incomingHub))
                while(seed.MoveNext()) { }
        }
        internal IEnumerator<int> SeedTrainCars(Metro3DTrain train,int stop,bool incomingHub)
        {
            if(train==null||!seeded.Add(train.transform))yield break;
            Physics.SyncTransforms();
            int capacity=world.settings.passengersPerCar;
            if(incomingHub)trainArrivals??=TrainRandom();
            var boardingPositions=new int[capacity];
            for(int car=0;car<Metro3DTrain.CarCount;car++) {
                for(int i=0;i<capacity;i++)boardingPositions[i]=i;
                if(incomingHub)for(int i=capacity-1;i>0;i--) {
                    int other=trainArrivals.Next(i+1);
                    (boardingPositions[i],boardingPositions[other])=(boardingPositions[other],boardingPositions[i]);
                }
                int count=incomingHub?trainArrivals.Next(Mathf.CeilToInt(capacity*.65f),capacity+1):capacity;
                int alighters=incomingHub?Mathf.Max(1,Mathf.RoundToInt(count*Mathf.Lerp(.18f,.34f,(float)trainArrivals.NextDouble()))):0;
                for(int i=0;i<count;i++) {
                    Vector3 p=BoardingPosition(car,boardingPositions[i]);
                    if(!SpawnClear(train.transform,p))continue;
                    var actor=Rent(stop,true,p,true,false,train,train.TravelDirection);
                    if(alighters<=0)continue;
                    ChoosePassengerDestination(0,false,-train.TravelDirection,trainArrivals,out int origin,out string originId,out _,out _);
                    actor.Journey.ArrivesAtHub(origin,originId,train.TravelDirection);
                    alighters--;
                }
                yield return car;
            }
        }

        Vector3 BoardingPosition(int car,int slot)
            => new Vector3(slot%2==0?-.38f:.38f,0,Metro3DTrain.FloorStart+car*Metro3DTrain.CarPitch+1.45f+slot/2*1.95f);

        internal int ParkHubRiders(Metro3DTrain train,bool leavesModeledRoute)
        {
            List<RiderSnapshot> riders=null;
            int count=0;
            for(int i=world.passengers.Count-1;i>=0;i--) {
                var actor=world.passengers[i];
                if(actor==null||!actor.inTrain||actor.Riding!=train||actor.PortalCommitted)continue;
                if(actor.TryGetComponent<SubwayCarry.AI.HelpNPC.Metro3DHelperAgent>(out var helper)&&helper.IsHelping)continue;
                var trip=actor.Journey;
                var snapshot=new RiderSnapshot{id=actor.PersistentId,index=actor.index,origin=trip.OriginStop,originId=trip.OriginId,
                    stop=trip.DestinationStop,destinationId=trip.DestinationId,label=trip.DestinationLabel,direction=trip.TravelDirection,speed=trip.SpeedScale,seatPreference=trip.SeatPreference};
                if(leavesModeledRoute) {
                    snapshot.due=Time.time+OutsideTravelSeconds(snapshot.destinationId);
                    offMapRiders.Add(snapshot);
                } else {
                    if(riders==null&&!detachedRiders.TryGetValue(train,out riders)) {
                        riders=new List<RiderSnapshot>();
                        detachedRiders.Add(train,riders);
                    }
                    riders.Add(snapshot);
                }
                Recycle(actor,false);
                count++;
            }
            BackgroundPooled+=count;
            return count;
        }

        float OutsideTravelSeconds(string destination)
        {
            int hops=1;
            foreach(var order in world.settings.deliveries.orders) {
                var stops=order.stops;
                if(stops==null||stops.Length<2||stops[0].id!="gachon")continue;
                bool north=stops[1].id=="bokjeong"||stops[1].id=="suseo";
                if(north!=(world.ServiceSide>0))continue;
                for(int i=1;i<stops.Length&&stops[i].line==stops[0].line;i++)
                    if(stops[i].id==destination)hops=Mathf.Max(hops,i);
            }
            return hops*world.settings.travelSeconds+(hops-1)*world.settings.dwellSeconds;
        }

        internal void ArriveWithBackgroundRiders(Metro3DTrain train)
        {
            if(!detachedRiders.TryGetValue(train,out var riders))return;
            string station=world.session.Route.stops[train.CurrentStop].id;
            var player=world.player;
            var view=world.Observer!=null?world.Observer.View:player!=null?player.view:null;
            bool visible=player!=null&&(player.Riding==train||world.StationAt(player.transform.position)==train.CurrentStop)
                ||view!=null&&world.StationAt(view.transform.position)==train.CurrentStop;
            if(!visible) {
                LogicalArrivals+=riders.RemoveAll(r=>r.destinationId==station);
                if(riders.Count==0)detachedRiders.Remove(train);
                return;
            }
            Physics.SyncTransforms();
            int capacity=world.settings.passengersPerCar;
            int slot=0;
            for(int i=riders.Count-1;i>=0&&slot<capacity*Metro3DTrain.CarCount;i--) {
                while(slot<capacity*Metro3DTrain.CarCount) {
                    Vector3 position=BoardingPosition(slot/capacity,slot%capacity);
                    slot++;
                    if(!SpawnClear(train.transform,position))continue;
                    Rent(train.CurrentStop,true,position,true,false,train,train.TravelDirection,snapshot:riders[i]);
                    Physics.SyncTransforms();
                    riders.RemoveAt(i);
                    break;
                }
            }
            if(riders.Count==0)detachedRiders.Remove(train);
        }

        Metro3DPassenger Rent(int stop,bool inside,Vector3 position,bool existing,bool far,Metro3DTrain service=null,int direction=0,bool concourse=false,RiderSnapshot snapshot=null)
        {
            Metro3DPassenger actor;
            if(pool.Count>0) actor=pool.Pop();
            else {
                var o=Instantiate(world.settings.passengerPrefab);
                actor=o.GetComponent<Metro3DPassenger>(); if(actor==null) actor=o.AddComponent<Metro3DPassenger>();
            }
            var root=inside?(service!=null?service:world.train).transform:world.stations[stop];
            actor.gameObject.SetActive(false);
            actor.transform.SetParent(root,false); actor.transform.localPosition=position;
            actor.transform.localRotation=Quaternion.Euler(0,inside?90:world.ServiceSide==1?90:270,0);
            actor.InitialDirection=direction;
            actor.Initialize(world,snapshot!=null?snapshot.index:serial++,stop,inside,existing,far);
            if(snapshot!=null)actor.RestoreBackgroundJourney(snapshot);
            if(!inside&&existing) {
                // The chosen destination owns the platform, not the delivery's direction.
                var local=actor.transform.localPosition;local.x=-actor.Journey.TrainSide*Mathf.Abs(local.x);actor.transform.localPosition=local;
            }
            if(concourse)actor.Journey.EnterConcourse();
            SubwayCarry.AI.HelpNPC.Metro3DHelperAgent.Configure(actor);
            world.passengers.Add(actor); Adjust(root,1); SpawnedCount++;
            actor.gameObject.SetActive(true); return actor;
        }
        public void Recycle(Metro3DPassenger actor,bool completed)
        {
            if(actor==null||!actor.gameObject.activeSelf) return;
            ReleaseGate(actor); ReleaseDoor(actor);ReleaseStanding(actor);Adjust(actor.transform.parent,-1);
            if(completed) CompletedCount++;
            world.passengers.Remove(actor); actor.ResetForPool(); actor.gameObject.SetActive(false);
            if(dormant==null) {dormant=new GameObject("Passenger pool - inactive").transform;dormant.SetParent(transform,false);}
            actor.transform.SetParent(dormant,false); pool.Push(actor);
        }
        public void RecycleAll()
        {
            for(int i=world.passengers.Count-1;i>=0;i--) Recycle(world.passengers[i],false);
            detachedRiders.Clear();offMapRiders.Clear();seeded.Clear(); gates.Clear(); gateQueues.Clear();gateCrossings.Clear();gateSelections.Clear();gateIntentCounts.Clear(); doorOwners.Clear(); doorQueues.Clear();doorSides.Clear(); occupancy.Clear();standing.Clear();supports.Clear();platformSupports.Clear();supportClaims.Clear();
        }
        public void RetireTrain(Metro3DTrain train)
        {
            // Terminal/transfer dwell waits for alighters. Do not silently drop unfinished journeys.
            for(int i=world.passengers.Count-1;i>=0;i--) {
                var actor=world.passengers[i];
                if(actor.transform.parent!=train.transform) continue;
                Debug.LogWarning("Passenger remained on retiring service: "+actor.name+" goal="+actor.Journey.DestinationId);
                Recycle(actor,false);
            }
            if(detachedRiders.TryGetValue(train,out var riders)) {
                // A service retiring is not evidence of a destination arrival.
                foreach(var rider in riders) Debug.LogWarning("Detached passenger remained on retiring service: "+rider.destinationId);
                detachedRiders.Remove(train);
            }
            seeded.Remove(train.transform);
            supports.Remove(train);occupancy.Remove(train.transform);
            world.GetComponent<Metro3DCrowd>().ForgetSurface(train.transform);
            for(int d=0;d<Metro3DTrain.DoorZ.Length;d++) {for(int lane=-1;lane<=1;lane++)doorOwners.Remove((train,d,lane));doorQueues.Remove((train,d,true));doorQueues.Remove((train,d,false));}
        }
        static void PruneQueue(List<Metro3DPassenger> queue)
        {
            for(int i=queue.Count-1;i>=0;i--) if(queue[i]==null||!queue[i].isActiveAndEnabled) queue.RemoveAt(i);
        }
        List<Metro3DPassenger> GateQueue(Metro3DFacility gate)
        {
            if(!gateQueues.TryGetValue(gate,out var queue)) {queue=new List<Metro3DPassenger>();gateQueues[gate]=queue;}
            PruneQueue(queue);return queue;
        }
        public Metro3DFacility SelectGate(Metro3DPassenger actor,bool far,bool outgoing)
        {
            Transform root=world.stations[actor.stationIndex];
            if(!gates.TryGetValue(root,out var bank)) {
                var found=new List<Metro3DFacility>();
                foreach(var f in world.facilities) if(f!=null&&f.transform.parent==root&&f.gateDoor!=null) found.Add(f);
                found.Sort((a,b)=> {int z=a.transform.localPosition.z.CompareTo(b.transform.localPosition.z);return z!=0?z:a.transform.localPosition.x.CompareTo(b.transform.localPosition.x);});
                bank=found.ToArray();gates[root]=bank;
            }
            if(bank.Length<10) return null;
            float direction=(far?-1:1)*(outgoing?-1:1);
            Metro3DFacility chosen=null;float best=float.PositiveInfinity;
            int begin=(far?5:0)+(outgoing?3:0),end=(far?5:0)+(outgoing?5:3);
            var crowd=world.GetComponent<Metro3DCrowd>();
            for(int i=begin;i<end;i++) {
                var gate=bank[i];
                if(gate==null||!gate.isActiveAndEnabled||gate.gateDoor==null||!gate.gateDoor.isActiveAndEnabled) continue;
                var queue=GateQueue(gate);
                Vector3 head=gate.transform.localPosition+Vector3.back*direction*1.05f;head.y=world.settings.departureFloor;
                Vector3 tail=GateTail(actor,queue,root,head,Vector3.forward*direction);
                if(!crowd.StaticSpaceFree(tail,root,.24f)) continue;
                float travel=Vector3.Distance(actor.transform.position,tail)/Mathf.Max(.3f,world.settings.npcSpeed*actor.Journey.SpeedScale);
                gateIntentCounts.TryGetValue(gate,out int intents);
                int approaching=Mathf.Max(0,intents-queue.Count);
                float wait=(queue.Count+approaching*.5f)*(1.1f+actor.QueueReactionDelay);
                float score=travel+wait+crowd.LocalDensity(tail,root)*1.5f;
                if(score<best) {best=score;chosen=gate;}
            }
            if(chosen==null) return null;
            ReleaseGate(actor);gateSelections[actor]=chosen;
            gateIntentCounts.TryGetValue(chosen,out int count);gateIntentCounts[chosen]=count+1;return chosen;
        }
        public int GateSlot(Metro3DPassenger actor,Metro3DFacility gate)
        {
            var queue=GateQueue(gate);return queue.IndexOf(actor);
        }
        public bool GateAtCapacity(Metro3DFacility gate)=>GateQueue(gate).Count>=GateQueueCapacity;
        public bool GateSurfaceAllowed(Metro3DPassenger actor,Vector3 current,Vector3 candidate)
        {
            if(actor.inTrain||current.y<2||!gates.TryGetValue(actor.transform.parent,out var bank))return true;
            foreach(var gate in bank) {
                if(gate==null)continue;float z=gate.transform.localPosition.z;
                float before=current.z-z,after=candidate.z-z;
                if(before*after<0)return false;
                if(Mathf.Abs(after)<.9f&&Mathf.Abs(after)<Mathf.Abs(before)-.002f)return false;
            }
            return true;
        }
        public Vector3 GateApproach(Metro3DPassenger actor,Metro3DFacility gate,float direction)
        {
            var queue=GateQueue(gate);int slot=queue.IndexOf(actor);
            Vector3 head=gate.transform.localPosition+Vector3.back*direction*1.05f;head.y=world.settings.departureFloor;
            Vector3 axis=Vector3.forward*direction;
            if(slot<0) {
                actor.ClearQueueContext();
                Vector3 join=GateTail(actor,queue,gate.transform.parent,head,axis);
                Vector3 delta=gate.transform.parent.InverseTransformPoint(actor.transform.position)-gate.transform.parent.InverseTransformPoint(join);
                float along=Vector3.Dot(delta,axis);Vector3 lateral=delta-axis*along;lateral.y=0;
                if(gateSelections.TryGetValue(actor,out var selected)&&selected==gate&&queue.Count<GateQueueCapacity&&Mathf.Abs(delta.y)<.4f&&along<=.15f&&along>=-.95f&&lateral.sqrMagnitude<.45f*.45f) {
                    queue.Add(actor);slot=queue.Count-1;
                } else return join;
            }
            Metro3DPassenger previous=slot>0?queue[slot-1]:null;
            actor.SetQueueContext(previous,gate.transform.parent.TransformDirection(axis),QueueGap(actor),slot!=0||!gate.gateDoor.Passable);
            return QueuePoint(actor,previous,gate.transform.parent,head,axis);
        }
        static Vector3 GateTail(Metro3DPassenger actor,List<Metro3DPassenger> queue,Transform root,Vector3 head,Vector3 axis)
        {
            if(queue.Count==0)return root.TransformPoint(head);
            // Only people who have physically joined can extend a line through the concourse.
            return QueuePoint(actor,queue[queue.Count-1],root,head,axis);
        }
        static float QueueGap(Metro3DPassenger actor)
            => .48f+Mathf.Clamp(actor.Journey.Personality.PersonalSpace,.12f,.4f)*.7f+.07f*actor.Journey.SpeedScale;
        static Vector3 QueuePoint(Metro3DPassenger actor,Metro3DPassenger previous,Transform root,Vector3 head,Vector3 axis)
        {
            if(previous==null) return root.TransformPoint(head);
            Vector3 local=root.InverseTransformPoint(previous.transform.position);
            float lag=Mathf.Max(0,Vector3.Dot(previous.Velocity,root.TransformDirection(axis)))*actor.QueueReactionDelay;
            float along=Mathf.Min(0,Vector3.Dot(local-head,axis))-QueueGap(actor)-lag;
            return root.TransformPoint(head+axis*along);
        }
        public bool GateTurn(Metro3DPassenger actor,Metro3DFacility gate)
        {
            if(gate==null||!gate.isActiveAndEnabled||GateSlot(actor,gate)!=0) return false;
            gate.gateDoor.HoldForPassenger();
            Vector3 beyond=gate.transform.position+actor.QueueForward*1.15f;beyond.y=actor.transform.position.y;
            if(!world.GetComponent<Metro3DCrowd>().PassageFree(actor,actor.transform.position,beyond)) {
                actor.SetQueueContext(null,actor.QueueForward,QueueGap(actor),true);return false;
            }
            if(!gate.gateDoor.Passable)return false;
            gateCrossings.Add(actor);return true;
        }
        public void ReleaseGate(Metro3DPassenger actor)
        {
            gateCrossings.Remove(actor);
            if(gateSelections.TryGetValue(actor,out var selected)) {
                if(gateIntentCounts.TryGetValue(selected,out int count))gateIntentCounts[selected]=Mathf.Max(0,count-1);
                gateSelections.Remove(actor);
            }
            foreach(var queue in gateQueues.Values) queue.Remove(actor);
            actor.ClearQueueContext();
        }
        public Vector3 DoorApproach(Metro3DPassenger actor,Metro3DTrain train,int door,bool boarding)
        {
            var key=(train,door,boarding);
            if(!doorQueues.TryGetValue(key,out var queue)) {queue=new List<Metro3DPassenger>();doorQueues[key]=queue;}
            PruneQueue(queue);
            if(!queue.Contains(actor)) queue.Add(actor);
            if(!doorSides.TryGetValue(actor,out int side)) {
                side=train.transform.InverseTransformPoint(actor.transform.position).z>=Metro3DTrain.DoorZ[door]?1:-1;
                doorSides[actor]=side;
            }
            // Admission order is stable across both approach branches. Repeated
            // nearest-door sorting starves the slightly more distant branch.
            int slot=queue.IndexOf(actor);
            Vector3 entry=train.DoorPoint(door,boarding);
            Metro3DPassenger previous=null;
            for(int i=slot-1;i>=0;i--) {
                var candidate=queue[i];
                if(!doorSides.TryGetValue(candidate,out int previousSide)||previousSide!=side||
                    boarding&&(candidate.transform.position-entry).sqrMagnitude>2.65f*2.65f)continue;
                previous=candidate;break;
            }
            Vector3 p=train.transform.InverseTransformPoint(entry);
            bool yielding=boarding&&HasAlightersNear(train,door);
            if(boarding) {
                var crowd=world.GetComponent<Metro3DCrowd>();
                bool crowded=NearbyBoarders(queue,entry)>=4;
                if(!yielding&&!crowded) {
                    actor.ClearQueueContext();
                    if(DoorOwner(train,door,0)!=null||DoorOwner(train,door,side)!=null)
                        return train.DoorPoint(door,true,side)+train.transform.TransformDirection(Vector3.left*train.side)*.56f;
                    return DoorOwner(train,door,-side)!=null||OppositeBoarderReady(actor,train,door,queue,side,1.65f)
                        ?train.DoorPoint(door,true,side):entry;
                }
                Vector3 outward=train.transform.TransformDirection(Vector3.left*train.side);
                if(yielding&&(actor.transform.position-entry).sqrMagnitude<1.65f*1.65f&&
                    !crowd.OccupiesAlightingLanding(actor,train,door)) {
                    actor.SetQueueContext(null,-outward,QueueGap(actor),true);
                    return actor.transform.position;
                }
                p.x-=train.side*(crowded?.45f:.12f);p.z+=side*.88f;
                if(!crowded)previous=null;
            } else {
                // Opposite door destinations must not build opposing queues
                // against the same bench-side line. Keep right in the central
                // aisle until passage is granted, then align to the actual door.
                p.x=-side*.28f;p.z+=side*.72f;
            }
            Vector3 axis=Vector3.back*side;
            actor.SetQueueContext(previous,train.transform.TransformDirection(axis),QueueGap(actor),
                slot!=0||!train.DoorsOpen||yielding);
            return QueuePoint(actor,previous,train.transform,p,axis);
        }
        static int NearbyBoarders(List<Metro3DPassenger> queue,Vector3 entry)
        {
            int count=0;
            foreach(var passenger in queue) {
                if((passenger.transform.position-entry).sqrMagnitude>2.65f*2.65f)continue;
                if(++count>=4)break;
            }
            return count;
        }

        internal bool IsAlightingHead(Metro3DTrain train,int door,Metro3DPassenger actor)
            => doorQueues.TryGetValue((train,door,false),out var queue)&&queue.Count>0&&queue[0]==actor;
        internal bool TryAlternateAlightingDoor(Metro3DPassenger actor,Metro3DTrain train,int door,int tried,out int alternate)
        {
            alternate=-1;
            int first=door/Metro3DTrain.DoorsPerCar*Metro3DTrain.DoorsPerCar;
            float best=float.PositiveInfinity;
            var crowd=world.GetComponent<Metro3DCrowd>();
            for(int i=first;i<first+Metro3DTrain.DoorsPerCar;i++) {
                if((tried&(1<<i))!=0)continue;
                if(DoorOwner(train,i,0)!=null||DoorOwner(train,i,-1)!=null||DoorOwner(train,i,1)!=null)continue;
                if(doorQueues.TryGetValue((train,i,false),out var queue)) {
                    PruneQueue(queue);
                    if(queue.Count>0)continue;
                }
                Vector3 inside=train.DoorPoint(i,false),outside=train.DoorPoint(i,true);
                float distance=(inside-actor.transform.position).sqrMagnitude;
                if(distance>=best||!crowd.PassageFree(actor,inside,outside))continue;
                best=distance;alternate=i;
            }
            return alternate>=0;
        }
        Metro3DPassenger DoorOwner(Metro3DTrain train,int door,int lane)
            =>doorOwners.TryGetValue((train,door,lane),out var owner)&&owner!=null&&owner.isActiveAndEnabled?owner:null;

        bool OppositeBoarderReady(Metro3DPassenger actor,Metro3DTrain train,int door,List<Metro3DPassenger> queue,int side,float distance)
        {
            var entry=train.DoorPoint(door,true,-side);
            foreach(var other in queue) {
                if(other==null||other==actor||!other.isActiveAndEnabled||other.DoorCrossing||
                    !doorSides.TryGetValue(other,out int otherSide)||otherSide!=-side)continue;
                if((other.transform.position-entry).sqrMagnitude<distance*distance)return true;
            }
            return false;
        }

        internal bool IsSharedDoorCrossing(Metro3DTrain train,int door,int lane)
            =>train!=null&&lane!=0&&DoorOwner(train,door,-lane)!=null;

        internal bool TryDoor(Metro3DPassenger actor,Metro3DTrain train,int door,bool boarding,out int lane)
        {
            lane=0;
            if(!train.DoorsOpen||train.Moving||!doorQueues.TryGetValue((train,door,boarding),out var queue))return false;
            int slot=queue.IndexOf(actor);
            if(slot<0||!boarding&&slot!=0||boarding&&(HasAlightersNear(train,door)||!train.CanBoard))return false;
            if(DoorOwner(train,door,0)!=null)return false;
            var left=DoorOwner(train,door,-1);var right=DoorOwner(train,door,1);
            var crowd=world.GetComponent<Metro3DCrowd>();
            if(boarding&&doorSides.TryGetValue(actor,out int side)) {
                if((side<0?left:right)!=null)return false;
                bool shared=left!=null||right!=null;
                Vector3 laneEntry=train.DoorPoint(door,true,side);
                bool near=(actor.transform.position-laneEntry).sqrMagnitude<1.18f*1.18f;
                if(shared&&!near)return false;
                if(near&&(shared||OppositeBoarderReady(actor,train,door,queue,side,1.18f)))lane=side;
            }
            if(lane==0&&(left!=null||right!=null))return false;
            Vector3 entry=train.DoorPoint(door,boarding,lane),exit=train.DoorPoint(door,!boarding,lane);
            if(!crowd.CanApproachDirect(actor,entry))return false;
            if(boarding) {
                // The nearer ready branch gets the first claim. A second rider
                // can use the other lane only once both are at the opening.
                for(int i=0;i<slot;i++) {
                    var previous=queue[i];
                    if(previous==null||!previous.isActiveAndEnabled||previous.DoorCrossing)continue;
                    if(lane!=0&&doorSides.TryGetValue(previous,out int previousSide)&&previousSide!=lane)continue;
                    if((previous.transform.position-entry).sqrMagnitude<1.18f*1.18f&&crowd.CanApproachDirect(previous,entry))return false;
                }
                crowd.ClearLanding(train,door,actor);
            }
            if(!crowd.PassageFree(actor,entry,exit)||!crowd.PassageFree(actor,actor.transform.position,entry))return false;
            doorOwners[(train,door,lane)]=actor;return true;
        }
        public int PassageClaims(Metro3DPassenger except,Vector3[] starts,Vector3[] ends)
        {
            if(doorOwners.Count==0&&gateCrossings.Count==0)return 0;
            int count=0;Vector3 position=except.transform.position;
            foreach(var pair in doorOwners) {
                var owner=pair.Value;var train=pair.Key.Item1;
                if(owner==null||owner==except||!owner.isActiveAndEnabled||!owner.PortalCommitted||train==null||train.Moving)continue;
                Vector3 inside=train.DoorPoint(pair.Key.Item2,false,pair.Key.Item3),outside=train.DoorPoint(pair.Key.Item2,true,pair.Key.Item3);
                if(Mathf.Abs(inside.y-position.y)>1.5f||((inside+outside)*.5f-position).sqrMagnitude>16)continue;
                if(count+2>starts.Length)break;
                starts[count]=inside;ends[count++]=outside;
                starts[count]=owner.transform.position;ends[count++]=owner.CurrentGoal;
            }
            foreach(var owner in gateCrossings) {
                if(owner==null||owner==except||!owner.isActiveAndEnabled||!owner.Journey.CrossingGate)continue;
                if(Mathf.Abs(owner.transform.position.y-position.y)>1.5f||(owner.transform.position-position).sqrMagnitude>16||count>=starts.Length)continue;
                starts[count]=owner.transform.position;ends[count++]=owner.CurrentGoal;
            }
            return count;
        }
        public Vector3 PlatformWaitingPoint(Metro3DPassenger actor)=>PlatformWaitingPoint(actor,null);
        public Vector3 PlatformWaitingPoint(Metro3DPassenger actor,Vector3? avoid)
        {
            ReleaseStanding(actor);
            Transform root=world.stations[actor.stationIndex];var crowd=world.GetComponent<Metro3DCrowd>();
            Vector3 selected=actor.transform.position;float best=float.PositiveInfinity;
            foreach(float doorZ in Metro3DTrain.DoorZ) for(int row=0;row<3;row++) for(int flank=-1;flank<=1;flank+=2) {
                Vector3 local=new Vector3(actor.Journey.TrainSide*(-4.25f-row*.74f),0,doorZ+flank*.95f);
                Vector3 point=root.TransformPoint(local);float distance=Vector3.Distance(point,actor.transform.position);
                if(avoid.HasValue&&(point-avoid.Value).sqrMagnitude<.65f*.65f)continue;
                if(distance>best||!crowd.SpaceFree(point,actor,.62f)||!crowd.StaticSpaceFree(point,root)) continue;
                bool claimed=false;
                foreach(var claim in standing) if(claim.Key!=null&&claim.Key!=actor&&!claim.Key.inTrain&&claim.Key.transform.parent==root&&(claim.Value-local).sqrMagnitude<.62f*.62f) {claimed=true;break;}
                if(claimed) continue;
                float score=distance+crowd.LocalDensity(point,root)*6;
                if(score<best) {best=score;selected=point;}
            }
            standing[actor]=root.InverseTransformPoint(selected);return selected;
        }
        public bool TryPlatformRest(Metro3DPassenger actor)
        {
            if(actor.inTrain||!actor.Journey.CanBoard)return false;
            Transform root=actor.transform.parent;Vector3 local=actor.transform.localPosition;
            float side=-actor.Journey.TrainSide*local.x;
            if(Mathf.Abs(local.y)>.18f||side<4||side>6.5f||local.z<10||local.z>Metro3DWorld.PlatformLength-10)return false;
            if(side<4.8f&&NearDoor(local.z,1.2f))return false;
            foreach(bool far in new[]{false,true}) {
                Vector3 delta=local-StairBottom(far,actor.Journey.TrainSide);delta.y=0;
                if(delta.sqrMagnitude<1.5f*1.5f)return false;
            }
            foreach(var claim in standing) if(claim.Key!=null&&claim.Key!=actor&&!claim.Key.inTrain&&claim.Key.transform.parent==root&&(claim.Value-local).sqrMagnitude<.6f*.6f)return false;
            var crowd=world.GetComponent<Metro3DCrowd>();Vector3 point=root.TransformPoint(local);
            if(crowd.LocalDensity(point,root)>.38f||!crowd.SpaceFree(point,actor,.72f)||!crowd.StaticSpaceFree(point,root))return false;
            if(world.player!=null&&(world.player.transform.position-point).sqrMagnitude<.6f*.6f)return false;
            ReleaseStanding(actor);standing[actor]=local;return true;
        }
        public Vector3 TrainStandingPoint(Metro3DPassenger actor,Metro3DTrain train)=>TrainStandingPoint(actor,train,null);
        public Vector3 TrainStandingPoint(Metro3DPassenger actor,Metro3DTrain train,Vector3? clearFrom)
        {
            ReleaseStanding(actor);Vector3 best=actor.transform.position;float distance=float.PositiveInfinity;
            var crowd=world.GetComponent<Metro3DCrowd>();
            SupportSpot chosen=null;
            foreach(var spot in Supports(train)) {
                if(spot.occupant!=null||spot.doorSide) continue;
                if(!spot.lean&&NearDoor(spot.feet.z,1.3f)) continue;
                var point=train.transform.TransformPoint(spot.feet);
                if(clearFrom.HasValue&&(point-clearFrom.Value).sqrMagnitude<.85f*.85f)continue;
                float score=(point-actor.transform.position).sqrMagnitude+(spot.lean?Mathf.Lerp(3,.3f,actor.Journey.Personality.PersonalSpace):0);
                if(score>=distance)continue;
                score+=crowd.LocalDensity(point,train.transform)*9;
                if(score>=distance||!crowd.SpaceFree(point,actor,.58f)||!crowd.StaticSpaceFree(point,train.transform)||Reserved(train,spot.feet,actor)) continue;
                chosen=spot;best=point;distance=score;
            }
            for(int car=0;car<Metro3DTrain.CarCount;car++) for(int row=0;row<25;row++) {
                float z=Metro3DTrain.FloorStart+car*Metro3DTrain.CarPitch+1+row*.7f;
                if(NearDoor(z,1.3f))continue;
                for(int side=-1;side<=1;side+=2) {
                    Vector3 local=new Vector3(side*.38f,0,z),point=train.transform.TransformPoint(local);
                    if(clearFrom.HasValue&&(point-clearFrom.Value).sqrMagnitude<.85f*.85f)continue;
                    float score=(point-actor.transform.position).sqrMagnitude+2;
                    if(score>=distance)continue;
                    score+=crowd.LocalDensity(point,train.transform)*9;
                    if(score>=distance||!crowd.SpaceFree(point,actor,.58f)||!crowd.StaticSpaceFree(point,train.transform)||Reserved(train,local,actor))continue;
                    chosen=null;best=point;distance=score;
                }
            }
            if(float.IsPositiveInfinity(distance)) {
                // No valid place: remain in a clear aisle and retry later, never
                // accept a spot inside a bench merely because selection failed.
                Vector3 local=train.transform.InverseTransformPoint(best);local.x=0;
                return train.transform.TransformPoint(local);
            }
            standing[actor]=train.transform.InverseTransformPoint(best);
            if(chosen!=null) ClaimSupport(actor,chosen,out best);
            return best;
        }
        bool Reserved(Metro3DTrain train,Vector3 local,Metro3DPassenger actor)
        {
            foreach(var r in standing) if(r.Key!=actor&&r.Key!=null&&r.Key.Riding==train&&
                r.Key.transform.parent==actor.transform.parent&&(r.Value-local).sqrMagnitude<.58f*.58f) return true;
            return false;
        }
        public SupportSpot SupportFor(Metro3DPassenger actor)=>supportClaims.TryGetValue(actor,out var spot)?spot:null;
        internal int OccupiedSeatNeighbors(Metro3DFacility seat)
        {
            Vector3 origin=seat.anchor.localPosition;
            int occupied=0;
            foreach(var other in world.facilities) {
                if(other==null||other.Available||other.kind!=Metro3DFacilityKind.Seat||other.anchor==null||
                    other==seat||other.anchor.parent!=seat.anchor.parent)continue;
                Vector3 delta=other.anchor.localPosition-origin;
                if(delta.sqrMagnitude<.64f*.64f&&Mathf.Abs(Vector3.Dot(delta,seat.anchor.localRotation*Vector3.forward))<.15f)
                    occupied++;
            }
            return occupied;
        }
        static bool NearDoor(float z,float distance) {foreach(float door in Metro3DTrain.DoorZ) if(Mathf.Abs(z-door)<distance) return true;return false;}
        public bool TryLeanPoint(Metro3DPassenger actor,Metro3DTrain train,out Vector3 point)
        {
            point=actor.transform.position;SupportSpot best=null;float distance=2.5f*2.5f;
            var crowd=world.GetComponent<Metro3DCrowd>();
            foreach(var spot in Supports(train)) {
                if(!spot.lean||spot.occupant!=null||spot.doorSide&&train.MovementRemaining<13) continue;
                Vector3 p=train.transform.TransformPoint(spot.feet);float d=(p-point).sqrMagnitude;
                if(spot.doorSide&&!crowd.CanFollowWalkingSegment(actor,p))continue;
                if(d>=distance||!crowd.SpaceFree(p,actor,.58f)||!crowd.StaticSpaceFree(p,train.transform)||Reserved(train,spot.feet,actor)) continue;
                distance=d;best=spot;
            }
            return ClaimSupport(actor,best,out point);
        }
        bool ClaimSupport(Metro3DPassenger actor,SupportSpot spot,out Vector3 point)
        {
            point=actor.transform.position;
            if(spot==null)return false;
            float sidePreference=Mathf.Repeat(actor.index*.5698403f+.39f,1f);
            spot.sideLean=spot.lean&&sidePreference<(spot.screenDoor?.9f:.1f);
            ReleaseStanding(actor);spot.occupant=actor;supportClaims[actor]=spot;standing[actor]=spot.feet;
            point=spot.frame.TransformPoint(spot.feet);return true;
        }
        internal bool TryPlatformLeanPoint(Metro3DPassenger actor,out Vector3 point)
        {
            var frame=actor.transform.parent;
            if(!platformSupports.TryGetValue(frame,out var spots)) {
                spots=new List<SupportSpot>();platformSupports.Add(frame,spots);
                for(int side=-1;side<=1;side+=2) {
                    for(float z=8;z<Metro3DWorld.PlatformLength-8;z+=3.6f) {
                        Vector3 feet=new Vector3(side*(Metro3DWorld.ExpandedPlatformOuterX-.48f),0,z);
                        AddLeanSpot(spots,frame,null,feet,Vector3.right*side,"Visible wall ground boundary");
                    }
                    foreach(float door in Metro3DTrain.DoorZ) for(int flank=-1;flank<=1;flank+=2) {
                        float z=door+flank*1.6f;
                        if(NearDoor(z,1.45f))continue;
                        Vector3 feet=new Vector3(side*(Metro3DWorld.ScreenDoorX+.45f),0,z);
                        AddLeanSpot(spots,frame,null,feet,Vector3.left*side,"PSD fixed glazing boundary");
                    }
                }
            }
            var crowd=world.GetComponent<Metro3DCrowd>();
            SupportSpot best=null;float distance=8.5f*8.5f;
            foreach(var spot in spots) {
                if(spot.occupant!=null||spot.surface==null||!spot.surface.enabled||spot.feet.x*actor.Journey.TrainSide>=0)continue;
                Vector3 p=frame.TransformPoint(spot.feet);
                float d=(p-actor.transform.position).sqrMagnitude;
                if(d>=distance||!crowd.SpaceFree(p,actor,.58f)||!crowd.StaticSpaceFree(p,frame)||
                    Reserved(null,spot.feet,actor))continue;
                float score=d+crowd.LocalDensity(p,frame)*10+(crowd.CanFollowWalkingSegment(actor,p)?0:5);
                if(score>=distance)continue;
                best=spot;distance=score;
            }
            return ClaimSupport(actor,best,out point);
        }
        void AddLeanSpot(List<SupportSpot> spots,Transform frame,Metro3DTrain train,Vector3 feet,Vector3 back,string colliderName)
        {
            Vector3 origin=frame.TransformPoint(feet)+Vector3.up*1.28f;
            Vector3 direction=frame.TransformDirection(back);
            int count=Physics.RaycastNonAlloc(origin,direction,ground,.58f,~0,QueryTriggerInteraction.Ignore);
            if(count==ground.Length)return;
            RaycastHit high=default;
            float nearest=float.PositiveInfinity;
            for(int i=0;i<count;i++) {
                var hit=ground[i];
                if(hit.collider is CharacterController||hit.distance>=nearest)continue;
                high=hit;nearest=hit.distance;
            }
            if(high.collider==null||high.collider.name!=colliderName||!high.collider.transform.IsChildOf(frame)||Mathf.Abs(high.normal.y)>.15f)return;
            if(!high.collider.Raycast(new Ray(origin-Vector3.up*.46f,direction),out var low,.58f)||Vector3.Dot(high.normal,low.normal)<.92f)return;
            spots.Add(new SupportSpot {train=train,frame=frame,feet=feet,lean=true,doorSide=train!=null,grip=frame.InverseTransformPoint(high.point),
                normal=frame.InverseTransformDirection(high.normal),surface=high.collider,screenDoor=colliderName=="PSD fixed glazing boundary"});
        }
        public void ReleaseStanding(Metro3DPassenger actor) {
            standing.Remove(actor);
            if(supportClaims.TryGetValue(actor,out var spot)) {if(spot.occupant==actor) spot.occupant=null;supportClaims.Remove(actor);}
        }
        List<SupportSpot> Supports(Metro3DTrain train)
        {
            if(supports.TryGetValue(train,out var cached)) return cached;
            var list=new List<SupportSpot>();supports[train]=list;
            // Read the existing Blender rail instances once, not the scene on
            // every passenger/frame. Crossbar contact matches the authored mesh.
            foreach(Transform module in train.transform) if(module.name=="GrabRail") {
                Vector3 origin=module.localPosition;
                for(int i=-3;i<=3;i++) list.Add(new SupportSpot {train=train,frame=train.transform,feet=new Vector3(Mathf.Sign(origin.x)*.38f,0,origin.z+i*.5f),grip=origin+new Vector3(0,1.72f,i*.5f)});
            }
            for(int side=-1;side<=1;side+=2) foreach(float door in Metro3DTrain.DoorZ)
                for(int end=-1;end<=1;end+=2) {
                    Vector3 feet=new Vector3(side*.97f,0,door+end*1.20f);
                    if(!NearDoor(feet.z,1.02f))AddLeanSpot(list,train.transform,train,feet,Vector3.right*side,"Visible carriage side boundary");
                }
            for(int car=0;car<Metro3DTrain.CarCount;car++) foreach(bool end in new[]{false,true}) foreach(float x in new[]{-.95f,.95f}) {
                float edge=Metro3DTrain.FloorStart+car*Metro3DTrain.CarPitch+(end?Metro3DTrain.CarLength:0);
                Vector3 feet=new Vector3(x,0,edge+(end?-.37f:.37f));
                Vector3 back=train.transform.TransformDirection(end?Vector3.forward:Vector3.back);
                Vector3 origin=train.transform.TransformPoint(feet)+Vector3.up*1.3f;
                if(!Physics.Raycast(origin,back,out var high,.5f,~0,QueryTriggerInteraction.Ignore)||!high.collider.transform.IsChildOf(train.transform)||high.collider is CharacterController||Mathf.Abs(high.normal.y)>.15f) continue;
                if(!Physics.Raycast(origin-Vector3.up*.45f,back,out var low,.5f,~0,QueryTriggerInteraction.Ignore)||low.collider!=high.collider||Vector3.Dot(low.normal,high.normal)<.92f) continue;
                list.Add(new SupportSpot {train=train,frame=train.transform,feet=feet,lean=true,grip=train.transform.InverseTransformPoint(high.point),normal=train.transform.InverseTransformDirection((low.normal+high.normal).normalized),surface=high.collider});
            }
            return list;
        }
        bool HasAlightersNear(Metro3DTrain train,int door)=>world.GetComponent<Metro3DCrowd>().AlightersAt(train,door);
        public void ReleaseDoor(Metro3DPassenger actor)
        {
            actor.ClearQueueContext();doorSides.Remove(actor);
            // At most 16 doors/service; no full crowd search per agent.
            foreach(var train in world.Trains) if(train!=null)
                for(int d=0;d<Metro3DTrain.DoorZ.Length;d++) for(int lane=-1;lane<=1;lane++)
                    if(doorOwners.TryGetValue((train,d,lane),out var owner)&&owner==actor)doorOwners.Remove((train,d,lane));
            foreach(var queue in doorQueues.Values) queue.Remove(actor);
        }
        public bool HasDoorCrossing(Metro3DTrain train)
        {
            for(int d=0;d<Metro3DTrain.DoorZ.Length;d++) for(int lane=-1;lane<=1;lane++)
                if(DoorOwner(train,d,lane)!=null)return true;
            return false;
        }
        public bool HasAlighters(Metro3DTrain train) {foreach(var a in world.passengers) if(a.inTrain&&a.Riding==train&&a.WantsToAlight) return true;return false;}
    }
}

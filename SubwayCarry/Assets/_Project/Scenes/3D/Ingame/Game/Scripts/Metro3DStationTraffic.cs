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
        public int DoorClaimCount => doorOwners.Count+doorFollowers.Count;
        public int BackgroundRiderCount {
            get {
                int count=offMapRiders.Count;
                foreach(var riders in detachedRiders.Values)count+=riders.Count;
                return count;
            }
        }

        internal sealed class RiderSnapshot
        {
            public System.Guid id,companionId;
            public int index,origin,stop,direction;
            public string originId,destinationId,label;
            public float due,speed,seatPreference;
            public Metro3DRiderLook look;
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
        void OnDestroy()=>wardrobe.Dispose();

        readonly Stack<Metro3DPassenger> pool=new Stack<Metro3DPassenger>();
        readonly Dictionary<System.Guid,Metro3DPassenger> liveRiders=new();
        readonly Metro3DRiderWardrobe wardrobe=new Metro3DRiderWardrobe();
        readonly Dictionary<Transform,int> occupancy=new Dictionary<Transform,int>();
        readonly HashSet<Transform> seeded=new HashSet<Transform>();
        readonly Dictionary<Transform,Metro3DFacility[]> gates=new Dictionary<Transform,Metro3DFacility[]>();
        readonly Dictionary<Metro3DFacility,List<Metro3DPassenger>> gateQueues=new Dictionary<Metro3DFacility,List<Metro3DPassenger>>();
        // A selected lane is intent, not a place in the physical FIFO.
        readonly Dictionary<Metro3DPassenger,Metro3DFacility> gateSelections=new Dictionary<Metro3DPassenger,Metro3DFacility>();
        readonly Dictionary<Metro3DFacility,int> gateIntentCounts=new Dictionary<Metro3DFacility,int>();
        const int GateQueueCapacity=6;
        public int GateSelectionCount => gateSelections.Count;
        readonly Dictionary<(Metro3DTrain,int,int),Metro3DPassenger> doorOwners=new();
        readonly Dictionary<(Metro3DTrain,int,int),Metro3DPassenger> doorFollowers=new();
        readonly Dictionary<Metro3DPassenger,(Metro3DTrain,int,int)> doorClaimByRider=new();
        readonly Dictionary<(Metro3DTrain,int,bool),List<Metro3DPassenger>> doorQueues=new Dictionary<(Metro3DTrain,int,bool),List<Metro3DPassenger>>();
        readonly Dictionary<Metro3DPassenger,int> doorSides=new Dictionary<Metro3DPassenger,int>();
        readonly HashSet<Metro3DPassenger> companionBoarders=new();
        internal void TrackCompanionBoarding(Metro3DPassenger actor)=>companionBoarders.Add(actor);
        internal void UntrackCompanionBoarding(Metro3DPassenger actor)=>companionBoarders.Remove(actor);
        readonly HashSet<Metro3DPassenger> gateCrossings=new HashSet<Metro3DPassenger>();
        readonly Dictionary<Metro3DPassenger,Vector3> standing=new Dictionary<Metro3DPassenger,Vector3>();
        readonly Dictionary<Metro3DPassenger,(int door,int flank)> platformLine=
            new Dictionary<Metro3DPassenger,(int door,int flank)>();
        static readonly float[] QueueTailAngles={0,-24,24,-48,48};
        public sealed class SupportSpot
        {
            public Metro3DTrain train;
            public Transform frame;
            public Vector3 feet,grip,normal;
            // Keep the phone hand on the open side of a side lean.
            public Vector3 LeanFacing=>sideLean?Vector3.Cross(normal,Vector3.up).normalized:normal;
            public Vector3 Facing=>lean?LeanFacing:normal;
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
                    var adult=Rent(stop,false,p,false,far,null,0,entry==2);EntranceArrivals++;
                    if(adult.RiderLook==Metro3DRiderLook.Student||EntranceArrivals%9!=3||CountAt(root)>=world.settings.passengerCount)continue;
                    Vector3 childPoint=p+new Vector3(p.x>0?-.66f:.66f,0,.42f);
                    if(!SpawnClear(root,childPoint))continue;
                    var child=Rent(stop,false,childPoint,false,far,null,0,entry==2,look:Metro3DRiderLook.Child);
                    child.Journey.ShareDestination(adult.Journey);
                    adult.PairWith(child);child.PairWith(adult);
                    EntranceArrivals++;person++;
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
                    stop=trip.DestinationStop,destinationId=trip.DestinationId,label=trip.DestinationLabel,direction=trip.TravelDirection,speed=trip.SpeedScale,seatPreference=trip.SeatPreference,look=actor.RiderLook,companionId=actor.CompanionId};
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

        Metro3DPassenger Rent(int stop,bool inside,Vector3 position,bool existing,bool far,Metro3DTrain service=null,int direction=0,bool concourse=false,RiderSnapshot snapshot=null,Metro3DRiderLook? look=null)
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
            int pattern=(actor.index*17+stop*7)%13;
            var appearance=snapshot?.look??look??(pattern<4?Metro3DRiderLook.Senior:pattern<8?Metro3DRiderLook.Student:Metro3DRiderLook.Office);
            actor.SetRiderLook(wardrobe,appearance);
            if(snapshot!=null)actor.RestoreBackgroundJourney(snapshot);
            else actor.Journey.UseRiderLook(appearance);
            if(!inside&&existing) {
                // The chosen destination owns the platform, not the delivery's direction.
                var local=actor.transform.localPosition;local.x=-actor.Journey.TrainSide*Mathf.Abs(local.x);actor.transform.localPosition=local;
            }
            if(concourse)actor.Journey.EnterConcourse();
            SubwayCarry.AI.HelpNPC.Metro3DHelperAgent.Configure(actor);
            world.passengers.Add(actor);liveRiders[actor.PersistentId]=actor;Adjust(root,1);SpawnedCount++;
            actor.gameObject.SetActive(true);
            if(snapshot!=null&&liveRiders.TryGetValue(snapshot.companionId,out var companion)) {
                actor.PairWith(companion);companion.PairWith(actor);
            }
            return actor;
        }
        public void Recycle(Metro3DPassenger actor,bool completed)
        {
            if(actor==null||!actor.gameObject.activeSelf) return;
            liveRiders.Remove(actor.PersistentId);ReleaseGate(actor);ReleaseDoor(actor);ReleaseStanding(actor);Adjust(actor.transform.parent,-1);
            if(completed) CompletedCount++;
            world.passengers.Remove(actor); actor.ResetForPool(); actor.gameObject.SetActive(false);
            if(dormant==null) {dormant=new GameObject("Passenger pool - inactive").transform;dormant.SetParent(transform,false);}
            actor.transform.SetParent(dormant,false); pool.Push(actor);
        }
        public void RecycleAll()
        {
            for(int i=world.passengers.Count-1;i>=0;i--) Recycle(world.passengers[i],false);
            companionBoarders.Clear();detachedRiders.Clear();offMapRiders.Clear();liveRiders.Clear();seeded.Clear(); gates.Clear(); gateQueues.Clear();gateCrossings.Clear();gateSelections.Clear();gateIntentCounts.Clear(); doorOwners.Clear(); doorFollowers.Clear();doorClaimByRider.Clear();doorQueues.Clear();doorSides.Clear(); occupancy.Clear();standing.Clear();platformLine.Clear();supports.Clear();platformSupports.Clear();supportClaims.Clear();
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
            for(int d=0;d<Metro3DTrain.DoorZ.Length;d++) {
                for(int lane=-1;lane<=1;lane++) {
                    var key=(train,d,lane);
                    if(doorOwners.Remove(key,out var owner))doorClaimByRider.Remove(owner);
                    if(doorFollowers.Remove(key,out var follower))doorClaimByRider.Remove(follower);
                }
                doorQueues.Remove((train,d,true));doorQueues.Remove((train,d,false));
            }
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
        internal bool GateHasFollower(Metro3DFacility gate)=>GateQueue(gate).Count>1;
        public bool GateSurfaceAllowed(Metro3DPassenger actor,Vector3 current,Vector3 candidate)
        {
            if(actor.inTrain||current.y<2||!gates.TryGetValue(actor.transform.parent,out var bank))return true;
            foreach(var gate in bank) {
                if(gate==null)continue;float z=gate.transform.localPosition.z;
                float before=current.z-z,after=candidate.z-z;
                if(before*after<0)return false;
                if(Mathf.Abs(after)<.9f&&Mathf.Abs(after)<Mathf.Abs(before)-.002f) {
                    var reserved=actor.Journey.ReservedGate;
                    // Reaching a reader stays on the same side of the barrier.
                    // Queue ownership and the lane bound are both required.
                    if(actor.Journey.GateAction!=Metro3DPassengerJourney.GatePhase.Align||reserved==null||
                       Mathf.Abs(reserved.transform.localPosition.z-z)>.025f||Mathf.Abs(after)<.64f||
                       Mathf.Abs(candidate.x-reserved.transform.localPosition.x)>.20f||
                       !gateQueues.TryGetValue(reserved,out var queue)||queue.Count==0||queue[0]!=actor)return false;
                }
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
            // The queue head stays at its existing spacing. Only the
            // reserved passenger advances to the reachable reader position.
            return slot==0?gate.transform.parent.TransformPoint(head+axis*.235f):
                QueuePoint(actor,previous,gate.transform.parent,head,axis);
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
            // Fare gates feed a shared staircase landing. Keep their existing
            // headway until the downstream merge can accept the faster flow.
            float lag=actor.ContinuousQueueAllowed?0:
                Mathf.Max(0,Vector3.Dot(previous.Velocity,root.TransformDirection(axis)))*actor.QueueReactionDelay;
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
        internal void JoinCompanionDoorQueue(Metro3DPassenger a,Metro3DPassenger b,Metro3DTrain train,int door)
        {
            var key=(train,door,true);
            if(!doorQueues.TryGetValue(key,out var queue)){queue=new List<Metro3DPassenger>();doorQueues[key]=queue;}
            PruneQueue(queue);
            // A newly arriving pair joins behind existing riders. Within the
            // pair, physical approach order wins over Update/component order,
            // so the nearer child doesn't walk backwards across the adult.
            if(queue.Contains(a)||queue.Contains(b))return;
            var entry=train.DoorPoint(door,true);
            bool aFirst=(a.transform.position-entry).sqrMagnitude<=(b.transform.position-entry).sqrMagnitude;
            queue.Add(aFirst?a:b);queue.Add(aFirst?b:a);
            doorSides[a]=train.transform.InverseTransformPoint(a.transform.position).z>=Metro3DTrain.DoorZ[door]?1:-1;
            doorSides[b]=train.transform.InverseTransformPoint(b.transform.position).z>=Metro3DTrain.DoorZ[door]?1:-1;
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
            int slot=queue.IndexOf(actor);
            Vector3 entry=train.DoorPoint(door,boarding);
            if(boarding)return PlatformDoorQueue(actor,train,door,side,entry,queue);

            Vector3 p=train.transform.InverseTransformPoint(entry);
            Vector3 feet=train.transform.InverseTransformPoint(actor.transform.position);
            var crowd=world.GetComponent<Metro3DCrowd>();
            Vector3 flank=train.DoorPoint(door,false,side);
            bool head=ReadyAlighter(train,door,queue)==actor;
            if(head&&train.DoorsOpen&&
                ((entry-actor.transform.position).sqrMagnitude<1.28f*1.28f||crowd.CanApproachDirect(actor,flank))) {
                actor.ClearQueueContext();
                return crowd.CanApproachDirect(actor,flank)?flank:entry;
            }

            // Follow the person physically ahead in the same strip of the
            // aisle. A rider on the other side is not this person's leader.
            Vector3 axis=Vector3.back*side;
            Metro3DPassenger previous=null;
            float nearest=2.4f;
            foreach(var candidate in queue) {
                if(candidate==actor||candidate.DoorCrossing||
                    !doorSides.TryGetValue(candidate,out int previousSide)||previousSide!=side)continue;
                Vector3 other=train.transform.InverseTransformPoint(candidate.transform.position);
                float ahead=(feet.z-other.z)*side;
                if(ahead<.08f||ahead>=nearest||Mathf.Abs(other.x-feet.x)>.34f)continue;
                previous=candidate;nearest=ahead;
            }
            p.x=Mathf.Clamp(feet.x,-.56f,.56f);
            p.z+=side*(head&&train.DoorsOpen?.32f:.62f);
            float gap=previous==null?QueueGap(actor):Mathf.Max(actor.WalkingRadius+previous.WalkingRadius+.045f,
                QueueGap(actor)*.83f);
            if(previous!=null) {
                float lead=train.transform.InverseTransformPoint(previous.transform.position).z;
                p.z=p.z+side*(Mathf.Max(0,(lead-p.z)*side)+gap);
            }
            p.z=feet.z-side*Mathf.Max(0,(feet.z-p.z)*side);
            actor.SetQueueContext(previous,train.transform.TransformDirection(axis),gap,!head||!train.DoorsOpen);
            return train.transform.TransformPoint(p);
        }

        const float ExitShoulderDepth=2.1f;
        Vector3 PlatformDoorQueue(Metro3DPassenger actor,Metro3DTrain train,int door,int side,Vector3 entry,
            List<Metro3DPassenger> queue)
        {
            var crowd=world.GetComponent<Metro3DCrowd>();
            Vector3 outward=-train.transform.right*train.side, flank=train.transform.forward*side;
            var temperament=actor.Journey.Personality;
            var perception=crowd.ObserveDoor(train,door);
            bool boardingFlow=train.DoorsOpen&&train.CanBoard&&!perception.incoming;
            Vector3 flankEntry=train.DoorPoint(door,true,side);
            float clearWidth=Mathf.Max(Metro3DTrain.DoorWidth*.5f+(boardingFlow?.04f:actor.BodyRadius-.12f),actor.BodyRadius+.62f);
            float distanceFromDoor=Mathf.Lerp(.26f,.48f,temperament.Patience);
            // The first waiting rider keeps the same approach when the
            // claim opens. A closed claim only prevents crossing the threshold.
            Vector3 head=boardingFlow?flankEntry+outward*.22f:entry+outward*distanceFromDoor+flank*clearWidth;
            Vector3 along=(-outward*.47f-flank*.87f).normalized;
            Vector3 feet=actor.transform.position;
            Vector3 toOpening=head-feet;toOpening.y=0;
            float outsideDepth=Vector3.Dot(feet-entry,outward);
            if(boardingFlow&&outsideDepth>.52f&&!actor.HasCompanion)
                along=Vector3.Lerp(along,toOpening.normalized,Mathf.InverseLerp(.52f,2.1f,outsideDepth)*.83f).normalized;
            if(actor.StagedBoarding&&boardingFlow&&Time.time<actor.QueueSideStepUntil&&
                (actor.CurrentGoal-feet).sqrMagnitude<.62f*.62f)
                return actor.CurrentGoal;
            float ownDepth=Vector3.Dot(feet-head,-along);
            float ownDistanceToHead=(feet-head).sqrMagnitude;
            // A staged rider outside the discharge stream can let the people
            // in the carriage finish without migrating to a moving queue mark.
            bool settledFlank=outsideDepth>=.16f&&Vector3.Dot(feet-entry,flank)>=clearWidth-.10f&&
                !crowd.OccupiesAlightingLanding(actor,train,door);
            if(settledFlank&&perception.incoming&&!MayUseOppositeLane(train,door,side)) {
                actor.ReadBoardingOpportunity(train,door,false,perception.pressure,0);
                bool facingTrain=Vector3.Dot(actor.transform.forward,-outward)>.80f;
                Vector3 facing=facingTrain?actor.transform.forward:(-outward-flank*.25f).normalized;
                actor.SetQueueContext(null,facing,QueueGap(actor),true);
                actor.HoldPlatformFlank(outsideDepth<ExitShoulderDepth?flank:Vector3.zero);
                return feet;
            }
            Metro3DPassenger leader=null;
            float nearest=2.3f;
            foreach(var candidate in queue) {
                if(candidate==actor||candidate.inTrain||
                    !doorSides.TryGetValue(candidate,out int otherSide)||otherSide!=side)continue;
                // Follow the passenger through their handoff to the door.
                // Dropping them at the instant of admission resets the queue.
                if(candidate.DoorCrossing&&(!boardingFlow||candidate.TravelsOutward||
                    candidate.CrossingLane!=side&&candidate.CrossingLane!=0))continue;
                Vector3 delta=candidate.transform.position-feet;
                float forward=Vector3.Dot(delta,along);
                // Only follow someone further along this branch. The
                // actor-relative diagonal alone can pick a neighbor who
                // is still behind us and exchange the lead repeatedly.
                if(boardingFlow&&((candidate.transform.position-head).sqrMagnitude+.025f>=ownDistanceToHead||
                    Vector3.Dot(delta,outward)>.12f))continue;
                if(forward<.09f||forward>=nearest||delta.sqrMagnitude>2.65f*2.65f||
                    delta.y*delta.y>.22f*.22f||Vector3.Dot(candidate.transform.position-head,-along)<-.66f)continue;
                float corridor=boardingFlow?Mathf.Lerp(.57f,.80f,Mathf.InverseLerp(1.2f,2.7f,outsideDepth)):.82f;
                if((delta-along*forward).sqrMagnitude>corridor*corridor)continue;
                leader=candidate;nearest=forward;
            }

            bool sideOpen=CanFollowAtDoor(train,door,side,true)&&
                (DoorOwner(train,door,0)==null||CanFollowAtDoor(train,door,0,true));
            bool spareLane=perception.incoming&&!actor.StagedBoarding&&MayUseOppositeLane(train,door,side);
            bool available=sideOpen&&(!perception.incoming||spareLane);
            // Model the visible stopping space. A stationary person too close
            // to the entrance devalues it even when the reservation is free.
            float room=side<0?perception.leftRoom:perception.rightRoom;
            bool nearFlank=(feet-flankEntry).sqrMagnitude<.92f*.92f;
            float pressure=!perception.incoming?0:spareLane?perception.pressure*Mathf.Lerp(.53f,.23f,room):perception.pressure;
            if(spareLane&&sideOpen&&nearFlank&&crowd.CanApproachDirect(actor,flankEntry)&&
                crowd.DoorPassageFree(actor,train,door,flankEntry,train.DoorPoint(door,false,side))) {
                // A visibly empty lane has less opposing pressure even while
                // somebody is leaving through the other half of the doorway.
                pressure*=.42f;
            }
            bool ready=actor.ReadBoardingOpportunity(train,door,available,pressure,room);
            bool currentlyAtEntry=nearFlank&&crowd.CanApproachDirect(actor,flankEntry);
            if(ready) {
                // Reserve the next approach for the first physically reachable
                // rider on this flank. A rider already on the flank takes priority.
                float ownDistance=(feet-flankEntry).sqrMagnitude;
                bool front=currentlyAtEntry||leader==null;
                if(front)for(int i=0;i<queue.Count;i++) {
                    var other=queue[i];
                    if(other==actor||other.DoorCrossing||
                        !doorSides.TryGetValue(other,out int otherSide)||otherSide!=side)continue;
                    float distance=(other.transform.position-flankEntry).sqrMagnitude;
                    if(distance>ownDistance+.11f||distance>1.35f*1.35f||
                        distance>ownDistance-.11f&&other.index>actor.index||
                        !crowd.CanApproachDirect(other,flankEntry))continue;
                    front=false;
                    break;
                }
                if(front&&(!perception.incoming||!crowd.OccupiesAlightingLanding(actor,train,door)))leader=null;
                else ready=false;
            }
            if(ready&&leader==null) {
                Vector3 approach=boardingFlow?head:flankEntry+outward*.13f;
                if(actor.StagedBoarding&&boardingFlow&&actor.Velocity.sqrMagnitude<.20f*.20f&&
                    outsideDepth>=.24f&&(approach-feet).sqrMagnitude<1.20f*1.20f&&
                    (approach-feet).sqrMagnitude>.28f*.28f&&
                    !crowd.DoorPassageFree(actor,train,door,feet,approach)) {
                    if(Time.time>=actor.NextQueueSideStep&&
                        TryQueueSideStep(actor,crowd,train,door,feet,approach,outward,flank,out var detour)) {
                        actor.NextQueueSideStep=Time.time+1.4f;
                        actor.QueueSideStepUntil=Time.time+.55f;
                        actor.SetQueueContext(null,(detour-feet).normalized,QueueGap(actor),true);
                        return detour;
                    }
                    // The full approach can be occupied while the next
                    // diagonal step is already open. Keep feeding the branch.
                    Vector3 step=Vector3.MoveTowards(feet,approach,.42f);
                    if(crowd.CanFollowWalkingSegment(actor,step)&&
                        crowd.DoorPassageFree(actor,train,door,feet,step)) {
                        actor.ClearQueueContext();
                        return step;
                    }
                    actor.SetQueueContext(null,(entry-feet).normalized,QueueGap(actor),true);
                    return feet;
                }
                actor.ClearQueueContext();
                return approach;
            }

            Vector3 target=head;
            float spacing=QueueGap(actor);
            if(leader!=null) {
                Vector3 lead=leader.transform.position;
                Vector3 chord=head-lead;chord.y=0;
                Vector3 tangent=chord.sqrMagnitude>.15f*.15f?Vector3.Lerp(along,chord.normalized,.35f).normalized:along;
                float advance=Vector3.Dot(leader.Velocity,tangent);
                if(advance>.17f&&Vector3.Dot(leader.Velocity.normalized,tangent)>.53f)
                    tangent=Vector3.Lerp(tangent,leader.Velocity.normalized,.24f).normalized;
                spacing=Mathf.Max(actor.BodyRadius+leader.BodyRadius+.06f,
                    spacing-Mathf.Clamp01(queue.Count/10f)*Mathf.Lerp(.02f,.11f,1-temperament.Patience));
                target=lead-tangent*spacing+flank*((temperament.PersonalSpace-.25f)*.21f);
                if(boardingFlow&&Vector3.Dot(target-feet,tangent)<0&&
                    Vector3.Dot(feet-entry,outward)>=.22f&&
                    Vector3.Dot(feet-entry,flank)>=Metro3DTrain.DoorLaneOffset-.06f)target=feet;
            } else if(!boardingFlow&&ownDepth<.25f&&Vector3.Dot(feet-entry,flank)>=clearWidth-.08f&&
                Vector3.Dot(feet-entry,outward)>.18f&&!crowd.OccupiesAlightingLanding(actor,train,door)) {
                target=feet;
            }

            Vector3 offset=target-entry;
            float depth=Mathf.Max(Vector3.Dot(offset,outward),.22f);
            float sideways=Vector3.Dot(offset,flank);
            // Waiting riders feed in from the sides. A packed tail can spill
            // toward the center further back, without plugging the threshold.
            float flankWidth=clearWidth;
            if(boardingFlow) {
                float lateral=Vector3.Dot(feet-entry,flank);
                float stagedWidth=Mathf.Lerp(clearWidth*.76f,Metro3DTrain.DoorLaneOffset,
                    Mathf.InverseLerp(.65f,1.75f,depth)*Mathf.InverseLerp(8,18,queue.Count));
                flankWidth=Mathf.Max(Metro3DTrain.DoorLaneOffset,Mathf.Min(stagedWidth,lateral));
            }
            sideways=Mathf.Max(sideways,flankWidth);
            float footOffset=Vector3.Dot(feet-entry,flank);
            // The next claim is in front of the rider. When a waiting slot
            // moves a few centimeters behind them, hold the depth they won.
            // Keep larger lateral adjustments and alighting clearance intact.
            if(boardingFlow&&!ready&&leader==null&&outsideDepth>=0&&outsideDepth<.22f&&depth>outsideDepth&&
                footOffset>.10f&&footOffset<Metro3DTrain.DoorWidth*.5f&&Mathf.Abs(sideways-footOffset)<.13f)
                depth=outsideDepth;
            target=entry+flank*sideways+outward*depth;
            if(boardingFlow&&actor.DoorStallReady&&!actor.HasCompanion&&leader!=null&&
                leader.Velocity.sqrMagnitude<.14f*.14f&&outsideDepth>=.24f&&outsideDepth<1.55f&&
                Time.time>=actor.NextQueueSideStep) {
                // Commit to a new step only if the whole approach has room.
                // Do not try both sides repeatedly while the queue is moving.
                Vector3 approach=flankEntry+outward*.25f;
                actor.NextQueueSideStep=Time.time+.85f;
                if(TryQueueSideStep(actor,crowd,train,door,feet,approach,outward,flank,out var pass)) {
                    actor.QueueSideStepUntil=Time.time+.55f;
                    actor.SetQueueContext(null,(pass-feet).normalized,QueueGap(actor),true);
                    return pass;
                }
            }

            // Once settled with a stationary leader, keep the waiting place.
            // An unavailable opening is not a new path around the crowd.
            float correction=(target-feet).sqrMagnitude;
            bool clearOfExit=Vector3.Dot(feet-entry,flank)>= (boardingFlow?.48f:clearWidth-.12f);
            bool staticLeader=leader==null||leader.Velocity.sqrMagnitude<.14f*.14f;
            if(actor.StagedBoarding&&clearOfExit&&outsideDepth>=.08f&&staticLeader&&correction<1.05f*1.05f&&
                actor.Velocity.sqrMagnitude<.19f*.19f) {
                Vector3 forward=(entry-feet).normalized;
                bool settled=correction<.30f*.30f||!crowd.DoorPassageFree(actor,train,door,feet,target);
                if(settled) {
                    if(boardingFlow&&correction>.30f*.30f&&leader!=null&&
                        Vector3.Dot(leader.Velocity,-outward)>.12f&&Vector3.Dot(target-feet,-outward)>.15f) {
                        Vector3 stepForward=Vector3.MoveTowards(feet,target,.34f);
                        if(crowd.CanFollowWalkingSegment(actor,stepForward)&&
                            crowd.DoorPassageFree(actor,train,door,feet,stepForward)) {
                            actor.SetQueueContext(leader,forward,spacing,true);
                            return stepForward;
                        }
                    }
                    if(correction>.30f*.30f&&boardingFlow&&Time.time>=actor.NextQueueSideStep&&
                        TryQueueSideStep(actor,crowd,train,door,feet,target,outward,flank,out var step)) {
                        actor.NextQueueSideStep=Time.time+1.4f;
                        actor.QueueSideStepUntil=Time.time+.45f;
                        actor.SetQueueContext(null,(step-feet).normalized,spacing,true);
                        return step;
                    }
                    actor.SetQueueContext(leader,forward,spacing,true);
                    return feet;
                }
            }
            Vector3 link=leader!=null?leader.transform.position-target:entry-target;
            link.y=0;
            actor.SetQueueContext(leader,link.sqrMagnitude>.04f?link.normalized:along,spacing,true);
            return target;
        }

        static bool TryQueueSideStep(Metro3DPassenger actor,Metro3DCrowd crowd,Metro3DTrain train,int door,
            Vector3 feet,Vector3 target,Vector3 outward,Vector3 flank,out Vector3 step)
        {
            step=feet;
            Vector3 entry=train.DoorPoint(door,true);
            float best=Mathf.Pow(Vector3.Distance(feet,target)+.34f,2);
            for(int attempt=0;attempt<2;attempt++) {
                Vector3 candidate=feet+flank*(attempt==0?.47f:-.47f)-outward*.21f;
                if(Vector3.Dot(candidate-entry,flank)<.48f||
                    Vector3.Dot(candidate-entry,outward)<.18f)continue;
                float score=(candidate-target).sqrMagnitude;
                if(score>=best||!crowd.CanFollowWalkingSegment(actor,candidate)||
                    !crowd.DoorPassageFree(actor,train,door,feet,candidate,leaving:true))continue;
                Vector3 onward=Vector3.MoveTowards(candidate,target,.45f);
                if(!crowd.DoorPassageFree(actor,train,door,candidate,onward,leaving:true))continue;
                step=candidate;
                best=score;
            }
            return step!=feet;
        }

        internal void BoardingLoad(Metro3DPassenger actor,Metro3DTrain train,int door,out int queued,out int alighting,out float density)
        {
            queued=alighting=0;
            Vector3 outside=train.DoorPoint(door,true),inside=train.DoorPoint(door,false);
            if(doorQueues.TryGetValue((train,door,true),out var b))foreach(var a in b)
                if(a!=null&&a!=actor&&a.isActiveAndEnabled&&(a.transform.position-outside).sqrMagnitude<16)queued++;
            if(doorQueues.TryGetValue((train,door,false),out var x))foreach(var a in x)
                if(a!=null&&a.isActiveAndEnabled)alighting++;
            density=world.GetComponent<Metro3DCrowd>().LocalDensity(inside,train.transform);
        }
        static Metro3DPassenger ReadyAlighter(Metro3DTrain train,int door,List<Metro3DPassenger> queue)
        {
            var entry=train.DoorPoint(door,false);
            Metro3DPassenger head=null;
            float nearest=float.PositiveInfinity;
            foreach(var candidate in queue) {
                if(candidate==null||!candidate.isActiveAndEnabled||candidate.DoorCrossing)continue;
                float distance=(candidate.transform.position-entry).sqrMagnitude;
                // Stable arrival order wins among people at the same depth.
                // An actor visibly closer can leave before a rider still in the aisle.
                if(distance>=nearest-.10f)continue;
                nearest=distance;head=candidate;
            }
            return head;
        }
        internal bool IsAlightingHead(Metro3DTrain train,int door,Metro3DPassenger actor)
            => doorQueues.TryGetValue((train,door,false),out var queue)&&ReadyAlighter(train,door,queue)==actor;
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
                // A free door is only an alternative when the first part
                // of the route through the same car is actually reachable.
                if(distance>=best||distance>7.1f*7.1f||!crowd.PassageFree(actor,inside,outside))continue;
                Vector3 firstStep=Vector3.MoveTowards(actor.transform.position,inside,1.05f);
                if(!crowd.CanFollowWalkingSegment(actor,firstStep))continue;
                best=distance;alternate=i;
            }
            return alternate>=0;
        }
        Metro3DPassenger DoorOwner(Metro3DTrain train,int door,int lane)
        {
            var key=(train,door,lane);
            if(doorOwners.TryGetValue(key,out var owner)&&owner!=null&&owner.isActiveAndEnabled)return owner;
            if(doorFollowers.TryGetValue(key,out var follower)&&follower!=null&&follower.isActiveAndEnabled) {
                doorOwners[key]=follower;
                doorFollowers.Remove(key);
                return follower;
            }
            return null;
        }

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

        bool CanFollowAtDoor(Metro3DTrain train,int door,int lane,bool boarding)
        {
            var head=DoorOwner(train,door,lane);
            if(doorFollowers.TryGetValue((train,door,lane),out var tail)&&tail!=null&&tail.isActiveAndEnabled)return false;
            if(head==null)return true;
            if(!head.DoorCrossing||head.TravelsOutward==boarding)return false;
            Vector3 entry=train.DoorPoint(door,boarding,lane);
            Vector3 axis=train.transform.right*train.side*(boarding?1:-1);
            float progress=Vector3.Dot(head.transform.position-entry,axis);
            // The follower still checks the actual free segment before
            // claiming. A compact leader can expose that segment sooner.
            float releaseDepth=Mathf.Max(.40f,head.WalkingRadius+Metro3DPassenger.StandingRadius+.032f);
            return progress>releaseDepth&&Vector3.Dot(head.Velocity,axis)>.16f;
        }

        internal bool HasMovingDoorLeader(Metro3DPassenger follower,Metro3DTrain train,int door,int lane,Vector3 axis)
        {
            if(!doorFollowers.TryGetValue((train,door,lane),out var tail)||tail!=follower)return false;
            var head=DoorOwner(train,door,lane);
            return head!=null&&head!=follower&&head.DoorCrossing&&head.TravelsOutward==follower.TravelsOutward&&
                Vector3.Dot(head.Velocity,axis)>.16f&&
                Vector3.Dot(head.transform.position-follower.transform.position,axis)>head.WalkingRadius+follower.WalkingRadius+.16f;
        }

        bool MayUseOppositeLane(Metro3DTrain train,int door,int side)
        {
            if(DoorOwner(train,door,0)!=null||DoorOwner(train,door,side)!=null)return false;
            var opposite=DoorOwner(train,door,-side);
            if(opposite!=null)return opposite.IsAlighting;
            if(!doorQueues.TryGetValue((train,door,false),out var leaving)||leaving.Count==0)return true;
            var front=ReadyAlighter(train,door,leaving);
            return front!=null&&doorSides.TryGetValue(front,out int exitSide)&&exitSide!=side;
        }

        internal bool TryDoor(Metro3DPassenger actor,Metro3DTrain train,int door,bool boarding,out int lane)
        {
            lane=0;
            if(!train.DoorsOpen||train.Moving||!doorQueues.TryGetValue((train,door,boarding),out var queue))return false;
            int slot=queue.IndexOf(actor);
            if(slot<0||!boarding&&ReadyAlighter(train,door,queue)!=actor||
                boarding&&!train.CanBoard&&!actor.CanFinishGroupBoarding(train))return false;
            bool yielding=boarding&&HasApproachingAlighters(train,door);
            if(yielding&&actor.StagedBoarding)return false;
            var center=DoorOwner(train,door,0);
            if(center!=null&&!CanFollowAtDoor(train,door,0,boarding))return false;
            var left=DoorOwner(train,door,-1);var right=DoorOwner(train,door,1);
            var crowd=world.GetComponent<Metro3DCrowd>();
            if(center==null&&boarding&&doorSides.TryGetValue(actor,out int side)) {
                if(!CanFollowAtDoor(train,door,side,true))return false;
                bool shared=left!=null||right!=null;
                Vector3 laneEntry=train.DoorPoint(door,true,side);
                bool near=(actor.transform.position-laneEntry).sqrMagnitude<1.18f*1.18f;
                if(yielding) {
                    if(!actor.IntendsDoorAdvance||!near||!MayUseOppositeLane(train,door,side))return false;
                    lane=side;
                } else {
                    if(shared&&!near)return false;
                    if(near&&(shared||OppositeBoarderReady(actor,train,door,queue,side,1.18f)||
                        crowd.CanApproachDirect(actor,laneEntry)&&crowd.DoorPassageFree(actor,train,door,laneEntry,train.DoorPoint(door,false,side))))lane=side;
                }
            }
            else if(center==null&&!boarding&&doorSides.TryGetValue(actor,out int exitSide)) {
                float lateral=train.transform.InverseTransformPoint(actor.transform.position).z-Metro3DTrain.DoorZ[door];
                for(int attempt=0;attempt<2;attempt++,exitSide=-exitSide) {
                    // Don't send someone diagonally across an active exit stream
                    // to reach the other branch from the far edge of the aisle.
                    if(lateral*exitSide<-.21f||!CanFollowAtDoor(train,door,exitSide,false))continue;
                    Vector3 laneEntry=train.DoorPoint(door,false,exitSide);
                    if((actor.transform.position-laneEntry).sqrMagnitude>=1.18f*1.18f||
                        !crowd.CanApproachDirect(actor,laneEntry))continue;
                    var occupied=DoorOwner(train,door,exitSide)!=null;
                    Vector3 laneExit=train.DoorPoint(door,true,exitSide);
                    if(occupied||crowd.DoorPassageFree(actor,train,door,laneEntry,laneExit,true)) {lane=exitSide;break;}
                }
            }
            if(lane==0&&(left!=null||right!=null))return false;
            var previousOwner=DoorOwner(train,door,lane);
            Vector3 entry=train.DoorPoint(door,boarding,lane),exit=train.DoorPoint(door,!boarding,lane);
            Vector3 feet=actor.transform.position;
            // Once an alighter has reached the alignment tolerance, the
            // action takes its next step toward the platform. Testing a
            // return to the mark can mistake the queue behind for a blocker.
            bool pastAlignment=!boarding&&(feet-entry).sqrMagnitude<=.15f*.15f&&
                Vector3.Dot(feet-entry,exit-entry)>=0;
            Vector3 firstStep=pastAlignment?Vector3.MoveTowards(feet,exit,.34f):entry;
            if(!crowd.CanApproachDirect(actor,firstStep))return false;
            if(boarding) {
                // The nearer ready branch gets the first claim. A second rider
                // can use the other lane only once both are at the opening.
                float here=(actor.transform.position-entry).sqrMagnitude;
                for(int i=0;i<slot;i++) {
                    var previous=queue[i];
                    if(previous==null||!previous.isActiveAndEnabled||previous.DoorCrossing)continue;
                    if(lane!=0&&doorSides.TryGetValue(previous,out int previousSide)&&previousSide!=lane)continue;
                    if((previous.transform.position-entry).sqrMagnitude<Mathf.Min(1.18f*1.18f,here+.15f)&&
                        crowd.CanApproachDirect(previous,entry)&&crowd.DoorPassageFree(previous,train,door,previous.transform.position,entry))return false;
                }
                crowd.ClearLanding(train,door,actor,lane);
            }
            // Keep room for a step into the doorway while a standing rider
            // inside reacts. Sharing with oncoming alighters still requires
            // the whole separate lane to be physically clear.
            Vector3 admissionEnd=boarding&&!yielding||previousOwner!=null
                ?Vector3.MoveTowards(entry,exit,previousOwner==null?.62f:.38f):exit;
            if(boarding&&!yielding&&previousOwner!=null) {
                // A follower can approach the vacated part of the lane.
                // Requiring the full nominal approach stalls an otherwise moving line.
                Vector3 axis=(exit-entry).normalized;
                float free=Vector3.Dot(previousOwner.transform.position-entry,axis)-
                    actor.WalkingRadius-previousOwner.WalkingRadius-.012f;
                if(free<=.025f)return false;
                admissionEnd=entry+axis*Mathf.Min(.38f,free);
            }
            if((boarding||previousOwner!=null)&&!crowd.DoorPassageFree(actor,train,door,entry,admissionEnd,!boarding))
                return false;
            if(!crowd.DoorPassageFree(actor,train,door,feet,firstStep,!boarding))return false;
            var claim=(train,door,lane);
            if(previousOwner==null)doorOwners[claim]=actor;
            else doorFollowers[claim]=actor;
            doorClaimByRider[actor]=claim;
            if(boarding)actor.CompanionDoorClaimed(train);
            return true;
        }
        public int PassageClaims(Metro3DPassenger except,Vector3[] starts,Vector3[] ends)
        {
            if(doorOwners.Count==0&&gateCrossings.Count==0)return 0;
            int count=0;Vector3 position=except.transform.position;
            foreach(var pair in doorOwners) {
                var owner=pair.Value;var train=pair.Key.Item1;
                if(owner==null||owner==except||!owner.isActiveAndEnabled||!owner.PortalCommitted||train==null||train.Moving)continue;
                // A follower's alighting reservation protects people crossing
                // the route, never blocks the rider already ahead on that route.
                // The forward rider still checks real bodies and architecture.
                bool sharesExit=owner.IsAlighting&&except.ClearingDoor(train,pair.Key.Item2)||
                    owner.RetreatingDoor&&except.ClearingRetreatPath(train,pair.Key.Item2);
                if(sharesExit&&Vector3.Dot(position-owner.transform.position,-train.transform.right*train.side)>.08f)continue;
                if(!owner.TravelsOutward&&except.ApproachingBoardingDoor(train,pair.Key.Item2)&&
                    doorSides.TryGetValue(except,out int approachSide)&&approachSide==pair.Key.Item3&&
                    Vector3.Dot(owner.transform.position-position,train.transform.right*train.side)>.33f)
                    continue; // The rear of the rider, not its future corridor, sets the following gap.
                if(except.CrossingTrain==train&&except.CrossingDoor==pair.Key.Item2&&
                    except.TravelsOutward==owner.TravelsOutward&&
                    (except.CrossingLane==pair.Key.Item3||except.CrossingLane!=0&&pair.Key.Item3!=0))
                    continue; // Active parallel lanes are separated by the actual bodies and jambs.
                Vector3 inside=train.DoorPoint(pair.Key.Item2,false,pair.Key.Item3),outside=train.DoorPoint(pair.Key.Item2,true,pair.Key.Item3);
                if(Mathf.Abs(inside.y-position.y)>1.5f||((inside+outside)*.5f-position).sqrMagnitude>16)continue;
                // The reservation protects the crossing from side traffic.
                // A boarded rider ahead must be able to clear the same opening.
                if(!owner.TravelsOutward&&except.inTrain&&except.Riding==train&&
                    Mathf.Abs(Vector3.Dot(position-inside,train.transform.forward))<Metro3DTrain.DoorWidth*.5f+.16f&&
                    Vector3.Dot(position-owner.transform.position,train.transform.right*train.side)>.15f)
                    continue;
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
        internal bool TryCompanionWaitingPoint(Metro3DPassenger actor,Metro3DPassenger partner,out Vector3 point)
        {
            point=actor.transform.position;var frame=actor.transform.parent;
            var crowd=world.GetComponent<Metro3DCrowd>();float best=float.PositiveInfinity;bool found=false;
            for(int i=0;i<8;i++) {
                float angle=i*Mathf.PI*.25f;
                var candidate=partner.transform.position+frame.TransformDirection(new Vector3(Mathf.Cos(angle)*.85f,0,Mathf.Sin(angle)*.85f));
                var local=frame.InverseTransformPoint(candidate);
                float side=-actor.Journey.TrainSide*local.x;
                if(side<4.3f||side>6.4f||NearDoor(local.z,1.0f)&&side<4.9f)continue;
                float d=(candidate-actor.transform.position).sqrMagnitude;
                if(d>=best||!crowd.SpaceFree(candidate,actor,.5f)||!crowd.StaticSpaceFree(candidate,frame))continue;
                best=d;point=candidate;found=true;
            }
            return found;
        }
        public Vector3 PlatformWaitingPoint(Metro3DPassenger actor)=>PlatformWaitingPoint(actor,null);
        public Vector3 PlatformWaitingPoint(Metro3DPassenger actor,Vector3? avoid)
            =>PlatformWaitingPoint(actor,avoid,true);
        // The platform strip is centered .60 m from the screen wall and is
        // .60 m wide in the station module. Leave room for the whole rider.
        static float PlatformStandingClearance(Metro3DPassenger actor)
            =>Metro3DWorld.ScreenDoorX+.90f+actor.BodyRadius+.07f;

        internal bool StandingOnPlatformStrip(Metro3DPassenger actor)
        {
            Vector3 p=actor.transform.localPosition;
            float side=-actor.Journey.TrainSide*p.x;
            return Mathf.Abs(p.y)<.18f&&side>=Metro3DWorld.ScreenDoorX&&side<PlatformStandingClearance(actor);
        }

        Vector3 PlatformWaitingPoint(Metro3DPassenger actor,Vector3? avoid,bool reserve)
        {
            if(reserve)ReleaseStanding(actor);
            Transform root=world.stations[actor.stationIndex];var crowd=world.GetComponent<Metro3DCrowd>();
            if(reserve&&!avoid.HasValue&&TryJoinPlatformLine(actor,root,crowd,out var linePoint))
                return linePoint;
            Vector3 selected=actor.transform.position;float best=float.PositiveInfinity;
            float edge=PlatformStandingClearance(actor)+.18f;
            float rear=Metro3DWorld.ExpandedPlatformOuterX-1.01f;
            float clearance=Metro3DTrain.DoorWidth*.5f+actor.BodyRadius+.08f;
            foreach(float doorZ in Metro3DTrain.DoorZ) for(int row=0;row<2;row++) for(int flank=-1;flank<=1;flank+=2) {
                Vector3 local=new Vector3(-actor.Journey.TrainSide*(row==0?edge:rear),0,doorZ+flank*clearance);
                Vector3 point=root.TransformPoint(local);float distance=Vector3.Distance(point,actor.transform.position);
                if(avoid.HasValue&&(point-avoid.Value).sqrMagnitude<.65f*.65f)continue;
                if(distance>best||!crowd.SpaceFree(point,actor,.62f)||!crowd.StaticSpaceFree(point,root)) continue;
                bool claimed=false;
                foreach(var claim in standing) if(claim.Key!=null&&claim.Key!=actor&&!claim.Key.inTrain&&claim.Key.transform.parent==root&&(claim.Value-local).sqrMagnitude<.62f*.62f) {claimed=true;break;}
                if(claimed) continue;
                float score=distance+crowd.LocalDensity(point,root)*6;
                if(row==1)score-=actor.Journey.Personality.Patience*.8f;
                if(actor.HasCompanion&&actor.Companion.transform.parent==root&&standing.TryGetValue(actor.Companion,out var partnerSpot))
                    score+=Mathf.Min(3,Vector3.Distance(local,partnerSpot)*.65f);
                if(score<best) {best=score;selected=point;}
            }
            if(reserve&&!float.IsPositiveInfinity(best))standing[actor]=root.InverseTransformPoint(selected);
            return selected;
        }
        internal bool StagedPlatformDoor(Metro3DPassenger actor,out int door,out int flank)
        {
            bool assigned=platformLine.TryGetValue(actor,out var place);
            door=assigned?place.door:-1;
            flank=place.flank;
            return assigned;
        }

        internal bool PreserveStagedDoorSide(Metro3DPassenger actor,int door)
        {
            if(!StagedPlatformDoor(actor,out int staged,out int flank))return false;
            if(!actor.HasCompanion&&staged==door)doorSides[actor]=flank;
            return true;
        }

        internal bool TryRejoinPlatformLine(Metro3DPassenger actor,Metro3DTrain train,int previousDoor,out Vector3 point)
        {
            var root=actor.transform.parent;
            var crowd=world.GetComponent<Metro3DCrowd>();
            point=actor.transform.position;
            Vector3 feet=root.InverseTransformPoint(point);
            int side=actor.Journey.TrainSide;
            float along=feet.z-Metro3DTrain.DoorZ[previousDoor];
            float depth=-side*feet.x;
            int flank=along<0?-1:1;
            float edge=PlatformStandingClearance(actor);
            if(Mathf.Abs(feet.y)<.32f&&depth>=edge-.24f&&Mathf.Abs(along)>=Metro3DTrain.DoorWidth*.5f+.02f&&
                Metro3DTrain.NearestDoorIndex(feet.z)==previousDoor) {
                Vector3 towardHead=root.TransformDirection((Vector3.right*side-Vector3.forward*flank*.52f).normalized);
                if(crowd.HasPlatformLeader(actor,train,previousDoor,towardHead)) {
                    // This rider already has a place in the line. Preserve the
                    // physical spacing they achieved instead of walking around it.
                    standing[actor]=feet;platformLine[actor]=(previousDoor,flank);
                    return true;
                }
            }
            return TryJoinPlatformLine(actor,root,crowd,out point,previousDoor);
        }

        bool TryJoinPlatformLine(Metro3DPassenger actor,Transform root,Metro3DCrowd crowd,out Vector3 point,int previousDoor=-1)
        {
            point=actor.transform.position;
            Vector3 feet=root.InverseTransformPoint(point);
            int side=actor.Journey.TrainSide;
            if(actor.inTrain||side==0||actor.transform.parent!=root||
                Mathf.Abs(feet.y)>.72f||-feet.x*side<Metro3DWorld.ScreenDoorX)
                return false;

            Vector3 away=Vector3.left*side;
            float edge=PlatformStandingClearance(actor)+.12f;
            float outer=Metro3DWorld.ExpandedPlatformOuterX-1.25f;
            float sideClearance=Metro3DTrain.DoorWidth*.5f+actor.BodyRadius+.06f;
            float personal=QueueGap(actor);
            int nearest=Metro3DTrain.NearestDoorIndex(feet.z);
            float best=float.PositiveInfinity;
            Vector3 chosen=default;
            (int door,int flank) place=default;
            for(int door=Mathf.Max(0,nearest-1);door<=Mathf.Min(nearest+1,Metro3DTrain.DoorZ.Length-1);door++) {
                float center=Metro3DTrain.DoorZ[door];
                for(int flank=-1;flank<=1;flank+=2) {
                    Vector3 lateral=Vector3.forward*flank;
                    Vector3 head=away*edge+new Vector3(0,0,center)+lateral*sideClearance;
                    Vector3 tail=head;
                    Metro3DPassenger tailActor=null;
                    float extension=0;
                    int depth=0;
                    foreach(var queued in platformLine) {
                        if(queued.Value.door!=door||queued.Value.flank!=flank||
                            queued.Key.transform.parent!=root||queued.Key.Journey.TrainSide!=side||
                            !standing.TryGetValue(queued.Key,out var anchor))continue;
                        depth++;
                        float reach=(anchor-head).sqrMagnitude;
                        if(tailActor==null||reach>extension) {
                            tail=anchor;tailActor=queued.Key;extension=reach;
                        }
                    }
                    float gap=tailActor==null?0:Mathf.Max(actor.BodyRadius+tailActor.BodyRadius+.11f,personal);
                    // The first link clears the door sideways. Later links
                    // curve into the spare platform depth, following the tail.
                    float bend=Mathf.InverseLerp(0,.9f,Vector3.Dot(tail-head,lateral));
                    Vector3 direction=(away*Mathf.Lerp(.50f,.95f,bend)+lateral*Mathf.Lerp(.86f,.15f,bend)).normalized;
                    int samples=tailActor==null?1:QueueTailAngles.Length;
                    for(int i=0;i<samples;i++) {
                        Vector3 local=tail+Quaternion.AngleAxis(QueueTailAngles[i]*flank*side,Vector3.up)*direction*gap;
                        float crossPlatform=Vector3.Dot(local,away);
                        float besideDoor=(local.z-center)*flank;
                        if(crossPlatform<edge-.10f||crossPlatform>outer||besideDoor<sideClearance-.10f||
                            Metro3DTrain.NearestDoorIndex(local.z)!=door)continue;
                        float score=Vector3.Distance(local,feet)+depth*.19f+Mathf.Abs(QueueTailAngles[i])*.007f;
                        if(previousDoor>=0)score+=door==previousDoor?0:Mathf.Lerp(.24f,1.1f,actor.Journey.Personality.Patience);
                        if(score>=best||!PlatformQueueSlotFree(actor,root,crowd,local))continue;
                        if(previousDoor>=0&&!crowd.CanFollowWalkingSegment(actor,root.TransformPoint(local))) {
                            Vector3 toward=local-feet;
                            if(toward.sqrMagnitude>3.35f*3.35f||Vector3.Dot(toward,away)<-.14f||
                                !crowd.CanFollowWalkingSegment(actor,root.TransformPoint(feet+Vector3.ClampMagnitude(toward,.56f))))continue;
                            score+=.5f;
                            if(score>=best)continue;
                        }
                        best=score;chosen=local;place=(door,flank);
                    }
                }
            }
            if(float.IsPositiveInfinity(best))return false;
            standing[actor]=chosen;platformLine[actor]=place;
            point=root.TransformPoint(chosen);
            return true;
        }

        bool PlatformQueueSlotFree(Metro3DPassenger actor,Transform root,Metro3DCrowd crowd,Vector3 local)
        {
            const float reservedGap=.54f;
            foreach(var reservation in standing) {
                var other=reservation.Key;
                if(other!=null&&other!=actor&&!other.inTrain&&other.transform.parent==root&&
                    (reservation.Value-local).sqrMagnitude<reservedGap*reservedGap)return false;
            }
            Vector3 point=root.TransformPoint(local);
            return crowd.StaticSpaceFree(point,root,actor.WalkingRadius)&&crowd.SpaceFree(point,actor,.48f);
        }

        internal bool TryGivePlatformRoom(Metro3DPassenger actor,out Vector3 point)
        {
            point=PlatformWaitingPoint(actor,actor.transform.position,false);
            float distance=(point-actor.transform.position).sqrMagnitude;
            if(distance<.35f*.35f||distance>2.5f*2.5f||!world.GetComponent<Metro3DCrowd>().CanFollowWalkingSegment(actor,point))return false;
            ReleaseStanding(actor);standing[actor]=actor.transform.parent.InverseTransformPoint(point);return true;
        }
        public bool TryPlatformRest(Metro3DPassenger actor)
        {
            if(actor.inTrain||!actor.Journey.CanBoard)return false;
            Transform root=actor.transform.parent;Vector3 local=actor.transform.localPosition;
            float side=-actor.Journey.TrainSide*local.x;
            float edge=PlatformStandingClearance(actor);
            float rear=Metro3DWorld.ExpandedPlatformOuterX-1.32f;
            if(Mathf.Abs(local.y)>.18f||side<edge||side>Metro3DWorld.ExpandedPlatformOuterX-.28f||
                (side>edge+.48f&&side<rear)||local.z<10||local.z>Metro3DWorld.PlatformLength-10)return false;
            if(side<rear&&NearDoor(local.z,Metro3DTrain.DoorWidth*.5f+actor.BodyRadius))return false;
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
        public Vector3 TrainStandingPoint(Metro3DPassenger actor,Metro3DTrain train,Vector3? clearFrom,
            int entrance=-1,bool reachableOnly=false)
        {
            ReleaseStanding(actor);Vector3 best=actor.transform.position;float distance=float.PositiveInfinity;
            var crowd=world.GetComponent<Metro3DCrowd>();
            Vector3? companionPoint=actor.RiderLook==Metro3DRiderLook.Child&&actor.HasCompanion&&actor.Companion.Riding==train
                ?actor.Companion.CurrentGoal:(Vector3?)null;
            SupportSpot chosen=null;
            foreach(var spot in Supports(train)) {
                if(spot.occupant!=null||spot.doorSide||!spot.lean&&actor.RiderLook==Metro3DRiderLook.Child) continue;
                if(entrance>=0&&Metro3DTrain.NearestDoorIndex(spot.feet.z)/Metro3DTrain.DoorsPerCar!=entrance/Metro3DTrain.DoorsPerCar)continue;
                if(!spot.lean&&NearDoor(spot.feet.z,1.3f)) continue;
                var point=train.transform.TransformPoint(spot.feet);
                if(clearFrom.HasValue&&(point-clearFrom.Value).sqrMagnitude<.85f*.85f)continue;
                float score=(point-actor.transform.position).sqrMagnitude+(spot.lean?Mathf.Lerp(3,.3f,actor.Journey.Personality.PersonalSpace):0);
                if(companionPoint.HasValue)score+=Mathf.Min(24,(point-companionPoint.Value).sqrMagnitude*2);
                if(score>=distance)continue;
                score+=crowd.LocalDensity(point,train.transform)*9;
                if(entrance>=0) {
                    if(spot.lean)score-=Mathf.Lerp(7f,19f,actor.Journey.Personality.PersonalSpace);
                    else if(Mathf.Sign(spot.feet.x)==train.side)score-=3.3f;
                }
                if(score>=distance||!crowd.SpaceFree(point,actor,.58f)||!crowd.StaticSpaceFree(point,train.transform)||Reserved(train,spot.feet,actor)) continue;
                if(entrance>=0) {
                    if(!crowd.TryCabinDestination(actor,train,entrance,point,reachableOnly,out float traffic))continue;
                    score+=traffic*5;
                    if(score>=distance)continue;
                }
                chosen=spot;best=point;distance=score;
            }
            int firstCar=entrance<0?0:entrance/Metro3DTrain.DoorsPerCar;
            int carLimit=entrance<0?Metro3DTrain.CarCount:firstCar+1;
            for(int car=firstCar;car<carLimit;car++) for(int row=0;row<25;row++) {
                float z=Metro3DTrain.FloorStart+car*Metro3DTrain.CarPitch+1+row*.7f;
                if(NearDoor(z,1.3f))continue;
                for(int side=-1;side<=1;side+=2) {
                    Vector3 local=new Vector3(side*.38f,0,z),point=train.transform.TransformPoint(local);
                    if(clearFrom.HasValue&&(point-clearFrom.Value).sqrMagnitude<.85f*.85f)continue;
                    float score=(point-actor.transform.position).sqrMagnitude+2;
                    if(companionPoint.HasValue)score+=Mathf.Min(24,(point-companionPoint.Value).sqrMagnitude*2);
                    if(score>=distance)continue;
                    score+=crowd.LocalDensity(point,train.transform)*9;
                    if(score>=distance||!crowd.SpaceFree(point,actor,.58f)||!crowd.StaticSpaceFree(point,train.transform)||Reserved(train,local,actor))continue;
                    if(entrance>=0) {
                        if(!crowd.TryCabinDestination(actor,train,entrance,point,reachableOnly,out float traffic))continue;
                        score+=traffic*5+(side==train.side?-.7f:.45f);
                        if(score>=distance)continue;
                    }
                    chosen=null;best=point;distance=score;
                }
            }
            if(float.IsPositiveInfinity(distance)) {
                if(reachableOnly)return actor.transform.position;
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
            platformLine.Remove(actor);
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
                float benchSide=Mathf.Sign(origin.x);
                for(int i=-3;i<=3;i++) {
                    float along=i*.5f;
                    list.Add(new SupportSpot {
                        train=train,frame=train.transform,normal=Vector3.right*benchSide,
                        feet=new Vector3(benchSide*.40f,0,origin.z+along),
                        grip=origin+new Vector3(0,1.72f,along+benchSide*.045f)
                    });
                }
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
        // People who cleared the threshold are still physical obstacles,
        // but only approaching traffic holds the opposing admission phase.
        bool HasApproachingAlighters(Metro3DTrain train,int door)=>world.GetComponent<Metro3DCrowd>().ObserveDoor(train,door).incoming;
        public void ReleaseDoor(Metro3DPassenger actor)
        {
            actor.ClearQueueContext();doorSides.Remove(actor);
            if(doorClaimByRider.Remove(actor,out var claim)) {
                if(doorOwners.TryGetValue(claim,out var owner)&&owner==actor) {
                    if(doorFollowers.Remove(claim,out var follower)&&follower!=null&&follower.isActiveAndEnabled)
                        doorOwners[claim]=follower;
                    else doorOwners.Remove(claim);
                } else if(doorFollowers.TryGetValue(claim,out var follower)&&follower==actor)
                    doorFollowers.Remove(claim);
            }
            foreach(var queue in doorQueues.Values) queue.Remove(actor);
        }
        public bool HasDoorCrossing(Metro3DTrain train)
        {
            foreach(var actor in companionBoarders)if(actor!=null&&actor.CompanionNeedsOpenDoor(train))return true;
            for(int d=0;d<Metro3DTrain.DoorZ.Length;d++) for(int lane=-1;lane<=1;lane++)
                if(DoorOwner(train,d,lane)!=null)return true;
            return false;
        }
        internal bool HasWaitingBoarders(Metro3DTrain train)
        {
            for(int door=0;door<Metro3DTrain.DoorZ.Length;door++) {
                if(!doorQueues.TryGetValue((train,door,true),out var queue))continue;
                Vector3 entry=train.DoorPoint(door,true);
                foreach(var actor in queue)
                    if(actor!=null&&actor.isActiveAndEnabled&&!actor.inTrain&&!actor.DoorCrossing&&
                        (actor.transform.position-entry).sqrMagnitude<3f*3f)return true;
            }
            return false;
        }
        public bool HasAlighters(Metro3DTrain train) {foreach(var a in world.passengers) if(a.inTrain&&a.Riding==train&&a.WantsToAlight) return true;return false;}
    }
}

using System.Collections.Generic;
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
        readonly Dictionary<(Metro3DTrain,int),Metro3DPassenger> doorOwners=new Dictionary<(Metro3DTrain,int),Metro3DPassenger>();
        readonly Dictionary<(Metro3DTrain,int,bool),List<Metro3DPassenger>> doorQueues=new Dictionary<(Metro3DTrain,int,bool),List<Metro3DPassenger>>();
        readonly Dictionary<Metro3DPassenger,int> doorSides=new Dictionary<Metro3DPassenger,int>();
        readonly Dictionary<Metro3DPassenger,Vector3> standing=new Dictionary<Metro3DPassenger,Vector3>();
        public sealed class SupportSpot
        {
            public Metro3DTrain train;
            public Vector3 feet,grip,normal;
            public bool lean;
            public Collider surface;
            public Metro3DPassenger occupant;
        }
        readonly Dictionary<Metro3DTrain,List<SupportSpot>> supports=new Dictionary<Metro3DTrain,List<SupportSpot>>();
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
            if(!aboard) {
                if(demandRevision!=world.RouteRevision)BuildBackgroundDemand();
                if(backgroundDemand.TryGetValue(route[origin].id,out var choices)&&choices.Count>0) {
                    int count=0;
                    foreach(var choice in choices)if(direction==0||choice.physicalDirection==direction*world.ServiceSide)count++;
                    if(count>0) {
                        int pick=random.Next(count);
                        foreach(var choice in choices) {
                            if(direction!=0&&choice.physicalDirection!=direction*world.ServiceSide)continue;
                            if(pick--!=0)continue;
                            id=choice.id;label=choice.label;travelDirection=choice.physicalDirection*world.ServiceSide;stop=-1;
                            for(int i=0;i<route.Length;i++)if(route[i].id==id&&(i>origin?1:-1)==travelDirection){stop=i;break;}
                            return;
                        }
                    }
                }
            }
            stop=Metro3DPassengerJourney.ChooseDestination(origin,route.Length,direction,random);
            id=route[stop].id;label=route[stop].label;travelDirection=stop>origin?1:-1;
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
        public void SeedTrain(Metro3DTrain train,int stop)
        {
            if(train==null||!seeded.Add(train.transform))return;
            Physics.SyncTransforms();
            for(int i=0;i<Metro3DTrain.CarCount*world.settings.passengersPerCar;i++) {
                int car=i/world.settings.passengersPerCar,row=(i%world.settings.passengersPerCar)/2;
                Vector3 p=new Vector3(i%2==0?-.38f:.38f,0,Metro3DTrain.FloorStart+car*Metro3DTrain.CarPitch+1.45f+row*1.95f);
                if(!SpawnClear(train.transform,p))continue;
                Rent(stop,true,p,true,false,train,train.TravelDirection);
            }
        }
        Metro3DPassenger Rent(int stop,bool inside,Vector3 position,bool existing,bool far,Metro3DTrain service=null,int direction=0,bool concourse=false)
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
            actor.InitialDirection=direction;actor.Initialize(world,serial++,stop,inside,existing,far);
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
            seeded.Clear(); gates.Clear(); gateQueues.Clear();gateSelections.Clear();gateIntentCounts.Clear(); doorOwners.Clear(); doorQueues.Clear();doorSides.Clear(); occupancy.Clear();standing.Clear();supports.Clear();supportClaims.Clear();
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
            seeded.Remove(train.transform);
            supports.Remove(train);occupancy.Remove(train.transform);
            world.GetComponent<Metro3DCrowd>().ForgetSurface(train.transform);
            for(int d=0;d<Metro3DTrain.DoorZ.Length;d++) {doorOwners.Remove((train,d));doorQueues.Remove((train,d,true));doorQueues.Remove((train,d,false));}
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
            return gate.gateDoor.Passable;
        }
        public void ReleaseGate(Metro3DPassenger actor)
        {
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
            Metro3DPassenger previous=null;
            for(int i=slot-1;i>=0;i--) if(doorSides.TryGetValue(queue[i],out int previousSide)&&previousSide==side) {previous=queue[i];break;}
            Vector3 p=train.transform.InverseTransformPoint(train.DoorPoint(door,boarding));
            if(boarding) {p.x-=train.side*.55f;p.z+=side*.95f;}
            else {
                // Opposite door destinations must not build opposing queues
                // against the same bench-side line. Keep right in the central
                // aisle until passage is granted, then align to the actual door.
                p.x=-side*.28f;p.z+=side*.72f;
            }
            Vector3 axis=Vector3.back*side;
            actor.SetQueueContext(previous,train.transform.TransformDirection(axis),QueueGap(actor),
                slot!=0||!train.DoorsOpen||(boarding&&HasAlightersNear(train,door)));
            return QueuePoint(actor,previous,train.transform,p,axis);
        }
        public bool TryDoor(Metro3DPassenger actor,Metro3DTrain train,int door,bool boarding)
        {
            if(!train.DoorsOpen||!doorQueues.TryGetValue((train,door,boarding),out var queue)||queue.Count==0||queue[0]!=actor) return false;
            if(boarding&&(HasAlightersNear(train,door)||!train.CanBoard)) return false;
            if(doorOwners.TryGetValue((train,door),out var owner)&&owner!=null&&owner.isActiveAndEnabled&&owner!=actor) return false;
            if(boarding) world.GetComponent<Metro3DCrowd>().ClearLanding(train,door,actor);
            if(!world.GetComponent<Metro3DCrowd>().PassageFree(actor,train,door))return false;
            doorOwners[(train,door)]=actor; return true;
        }
        public int PassageClaims(Metro3DPassenger except,Vector3[] starts,Vector3[] ends)
        {
            int count=0;Vector3 position=except.transform.position;
            foreach(var pair in doorOwners) {
                var owner=pair.Value;var train=pair.Key.Item1;
                if(owner==null||owner==except||!owner.isActiveAndEnabled||!owner.PortalCommitted||train==null||train.Moving)continue;
                Vector3 inside=train.DoorPoint(pair.Key.Item2,false),outside=train.DoorPoint(pair.Key.Item2,true);
                if(Mathf.Abs(inside.y-position.y)>1.5f||((inside+outside)*.5f-position).sqrMagnitude>16)continue;
                if(count+2>starts.Length)break;
                starts[count]=inside;ends[count++]=outside;
                starts[count]=owner.transform.position;ends[count++]=owner.CurrentGoal;
            }
            foreach(var pair in gateQueues) {
                if(pair.Value.Count==0)continue;var owner=pair.Value[0];
                if(owner==null||owner==except||!owner.isActiveAndEnabled||!owner.Journey.CrossingGate)continue;
                if(Mathf.Abs(owner.transform.position.y-position.y)>1.5f||(owner.transform.position-position).sqrMagnitude>16||count>=starts.Length)continue;
                starts[count]=owner.transform.position;ends[count++]=owner.CurrentGoal;
            }
            return count;
        }
        public Vector3 PlatformWaitingPoint(Metro3DPassenger actor)=>PlatformWaitingPoint(actor,null);
        public Vector3 PlatformWaitingPoint(Metro3DPassenger actor,Vector3? avoid)
        {
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
                float score=distance+crowd.LocalDensity(point,root)*2;
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
            if(!crowd.SpaceFree(point,actor,.52f)||!crowd.StaticSpaceFree(point,root))return false;
            if(world.player!=null&&(world.player.transform.position-point).sqrMagnitude<.6f*.6f)return false;
            standing[actor]=local;return true;
        }
        public Vector3 TrainStandingPoint(Metro3DPassenger actor,Metro3DTrain train)=>TrainStandingPoint(actor,train,null);
        public Vector3 TrainStandingPoint(Metro3DPassenger actor,Metro3DTrain train,Vector3? clearFrom)
        {
            ReleaseStanding(actor);Vector3 best=actor.transform.position;float distance=float.PositiveInfinity;
            var crowd=world.GetComponent<Metro3DCrowd>();
            SupportSpot chosen=null;
            foreach(var spot in Supports(train)) {
                if(spot.occupant!=null) continue;
                if(!spot.lean&&NearDoor(spot.feet.z,1.3f)) continue;
                var point=train.transform.TransformPoint(spot.feet);
                if(clearFrom.HasValue&&(point-clearFrom.Value).sqrMagnitude<.85f*.85f)continue;
                float score=(point-actor.transform.position).sqrMagnitude+(spot.lean?Mathf.Lerp(3,.3f,actor.Journey.Personality.PersonalSpace):0);
                if(score>=distance||!crowd.SpaceFree(point,actor,.58f)||!crowd.StaticSpaceFree(point,train.transform)||Reserved(train,spot.feet,actor)) continue;
                chosen=spot;best=point;distance=score;
            }
            for(int car=0;car<Metro3DTrain.CarCount;car++) for(int row=0;row<25;row++) foreach(float x in new[]{-.38f,.38f}) {
                float z=Metro3DTrain.FloorStart+car*Metro3DTrain.CarPitch+1+row*.7f;
                if(NearDoor(z,1.3f)) continue;
                Vector3 local=new Vector3(x,0,z),point=train.transform.TransformPoint(local);
                if(clearFrom.HasValue&&(point-clearFrom.Value).sqrMagnitude<.85f*.85f)continue;
                float score=(point-actor.transform.position).sqrMagnitude+2;if(score>=distance||!crowd.SpaceFree(point,actor,.58f)||!crowd.StaticSpaceFree(point,train.transform)||Reserved(train,local,actor)) continue;
                chosen=null;best=point;distance=score;
            }
            if(float.IsPositiveInfinity(distance)) {
                // No valid place: remain in a clear aisle and retry later, never
                // accept a spot inside a bench merely because selection failed.
                Vector3 local=train.transform.InverseTransformPoint(best);local.x=0;
                return train.transform.TransformPoint(local);
            }
            standing[actor]=train.transform.InverseTransformPoint(best);
            if(chosen!=null) {chosen.occupant=actor;supportClaims[actor]=chosen;}
            return best;
        }
        bool Reserved(Metro3DTrain train,Vector3 local,Metro3DPassenger actor)
        {
            foreach(var r in standing) if(r.Key!=actor&&r.Key!=null&&r.Key.Riding==train&&(r.Value-local).sqrMagnitude<.58f*.58f) return true;
            return false;
        }
        public SupportSpot SupportFor(Metro3DPassenger actor)=>supportClaims.TryGetValue(actor,out var spot)?spot:null;
        static bool NearDoor(float z,float distance) {foreach(float door in Metro3DTrain.DoorZ) if(Mathf.Abs(z-door)<distance) return true;return false;}
        public bool TryLeanPoint(Metro3DPassenger actor,Metro3DTrain train,out Vector3 point)
        {
            point=actor.transform.position;SupportSpot best=null;float distance=2.5f*2.5f;
            var crowd=world.GetComponent<Metro3DCrowd>();
            foreach(var spot in Supports(train)) {
                if(!spot.lean||spot.occupant!=null) continue;
                Vector3 p=train.transform.TransformPoint(spot.feet);float d=(p-point).sqrMagnitude;
                if(d>=distance||!crowd.SpaceFree(p,actor,.58f)||!crowd.StaticSpaceFree(p,train.transform)||Reserved(train,spot.feet,actor)) continue;
                distance=d;best=spot;
            }
            if(best==null) return false;
            ReleaseStanding(actor);best.occupant=actor;supportClaims[actor]=best;standing[actor]=best.feet;point=train.transform.TransformPoint(best.feet);return true;
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
                for(int i=-3;i<=3;i++) list.Add(new SupportSpot {train=train,feet=new Vector3(Mathf.Sign(origin.x)*.38f,0,origin.z+i*.5f),grip=origin+new Vector3(0,1.72f,i*.5f)});
            }
            for(int car=0;car<Metro3DTrain.CarCount;car++) foreach(bool end in new[]{false,true}) foreach(float x in new[]{-.95f,.95f}) {
                float edge=Metro3DTrain.FloorStart+car*Metro3DTrain.CarPitch+(end?Metro3DTrain.CarLength:0);
                Vector3 feet=new Vector3(x,0,edge+(end?-.37f:.37f));
                Vector3 back=train.transform.TransformDirection(end?Vector3.forward:Vector3.back);
                Vector3 origin=train.transform.TransformPoint(feet)+Vector3.up*1.3f;
                if(!Physics.Raycast(origin,back,out var high,.5f,~0,QueryTriggerInteraction.Ignore)||!high.collider.transform.IsChildOf(train.transform)||high.collider is CharacterController||Mathf.Abs(high.normal.y)>.15f) continue;
                if(!Physics.Raycast(origin-Vector3.up*.45f,back,out var low,.5f,~0,QueryTriggerInteraction.Ignore)||low.collider!=high.collider||Vector3.Dot(low.normal,high.normal)<.92f) continue;
                list.Add(new SupportSpot {train=train,feet=feet,lean=true,grip=train.transform.InverseTransformPoint(high.point),normal=train.transform.InverseTransformDirection((low.normal+high.normal).normalized),surface=high.collider});
            }
            return list;
        }
        bool HasAlightersNear(Metro3DTrain train,int door)=>world.GetComponent<Metro3DCrowd>().AlightersAt(train,door);
        public void ReleaseDoor(Metro3DPassenger actor)
        {
            actor.ClearQueueContext();doorSides.Remove(actor);
            // At most 16 doors/service; no full crowd search per agent.
            if(actor.Riding!=null) for(int d=0;d<Metro3DTrain.DoorZ.Length;d++) if(doorOwners.TryGetValue((actor.Riding,d),out var owner)&&owner==actor) doorOwners.Remove((actor.Riding,d));
            foreach(var train in world.Trains)if(train!=null)for(int d=0;d<Metro3DTrain.DoorZ.Length;d++)if(doorOwners.TryGetValue((train,d),out var owner)&&owner==actor)doorOwners.Remove((train,d));
            foreach(var queue in doorQueues.Values) queue.Remove(actor);
        }
        public bool HasDoorCrossing(Metro3DTrain train) {for(int d=0;d<Metro3DTrain.DoorZ.Length;d++) if(doorOwners.TryGetValue((train,d),out var owner)&&owner!=null&&owner.isActiveAndEnabled) return true;return false;}
        public bool HasAlighters(Metro3DTrain train) {foreach(var a in world.passengers) if(a.inTrain&&a.Riding==train&&a.WantsToAlight) return true;return false;}
    }
}

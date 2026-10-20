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
        Transform dormant;
        float nextArrival;
        int serial,entranceSerial;
        public bool HasPassengers(Transform root)=>occupancy.TryGetValue(root,out int count)&&count>0;
        public int CountAt(Transform root)=>occupancy.TryGetValue(root,out int count)?count:0;
        void Adjust(Transform root,int amount) {if(root!=null) occupancy[root]=Mathf.Max(0,CountAt(root)+amount);}
        public void ChangedParent(Transform previous,Transform current) {Adjust(previous,-1);Adjust(current,1);world.GetComponent<Metro3DCrowd>().InvalidateSpatial();}

        void Update()
        {
            if(world==null||world.session==null||world.session.SimulationPaused||Time.time<nextArrival) return;
            nextArrival=Time.time+world.settings.passengerArrivalSeconds;
            // The hub keeps receiving people even when the player never leaves its concourse.
            // Other physically constructed stations run the same arrival stream.
            for(int stop=0;stop<world.stations.Count&&stop<world.session.Route.stops.Length-1;stop++) {
                Transform root=world.stations[stop];
                if(CountAt(root)>=world.settings.passengerCount) continue;
                bool far=(entranceSerial++&1)!=0;
                Vector3 p=Entrance(far,world.settings.departureFloor);
                p.x=(entranceSerial%2==0?-.45f:.45f);
                if(!SpawnClear(root.TransformPoint(p))) continue;
                Rent(stop,false,p,false,far); EntranceArrivals++;
            }
        }
        bool SpawnClear(Vector3 point)
        {
            int n=Physics.OverlapCapsuleNonAlloc(point+Vector3.up*.32f,point+Vector3.up*1.45f,.28f,contacts,~0,QueryTriggerInteraction.Ignore);
            return n==0;
        }
        public static Vector3 Entrance(bool far,float floor)=>new Vector3(0,floor,far?124.5f:-26.5f);
        public static Vector3 StairTop(bool far,int side,float floor)=>new Vector3(-side*7.76f,floor,far?107.7f+.8f:-.8f);
        public static Vector3 StairBottom(bool far,int side)=>new Vector3(-side*7.76f,0,far?Metro3DWorld.FarStairBottom-.8f:11.9f);

        public void Seed(int stop,bool inside)
        {
            Transform root=inside?world.train.transform:world.stations[stop];
            if(!seeded.Add(root)) return;
            if(!inside&&stop>=world.session.Route.stops.Length-1) return;
            int count=inside?Metro3DTrain.CarCount*world.settings.passengersPerCar:world.settings.passengerCount;
            for(int i=0;i<count;i++) {
                // 18/car: two staggered aisle lanes, not a formula that overflows into another carriage.
                int car=i/world.settings.passengersPerCar,row=(i%world.settings.passengersPerCar)/2;
                Vector3 p=inside?new Vector3(i%2==0?-.38f:.38f,0,Metro3DTrain.FloorStart+car*Metro3DTrain.CarPitch+1.45f+row*1.95f):world.PlatformWaitingPoint(i);
                // Platform rows repeat only after all 16 doors; keep separate physical spots.
                if(!inside) p.z+=Mathf.Floor(i/(float)Metro3DTrain.DoorZ.Length)*.63f-.63f;
                Rent(stop,inside,p,true,!inside&&p.z>Metro3DWorld.PlatformLength*.5f);
            }
        }
        Metro3DPassenger Rent(int stop,bool inside,Vector3 position,bool existing,bool far)
        {
            Metro3DPassenger actor;
            if(pool.Count>0) actor=pool.Pop();
            else {
                var o=Instantiate(world.settings.passengerPrefab);
                actor=o.GetComponent<Metro3DPassenger>(); if(actor==null) actor=o.AddComponent<Metro3DPassenger>();
            }
            var root=inside?world.train.transform:world.stations[stop];
            actor.gameObject.SetActive(false);
            actor.transform.SetParent(root,false); actor.transform.localPosition=position;
            actor.transform.localRotation=Quaternion.Euler(0,inside?90:world.ServiceSide==1?90:270,0);
            actor.Initialize(world,serial++,stop,inside,existing,far);
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
            seeded.Clear(); gates.Clear(); gateQueues.Clear(); doorOwners.Clear(); doorQueues.Clear();doorSides.Clear(); occupancy.Clear();standing.Clear();supports.Clear();supportClaims.Clear();
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
                Vector3 tail=queue.Count==0?root.TransformPoint(head):QueuePoint(actor,queue[queue.Count-1],root,head,Vector3.forward*direction);
                if(!crowd.StaticSpaceFree(tail,root,.24f)) continue;
                float travel=Vector3.Distance(actor.transform.position,tail)/Mathf.Max(.3f,world.settings.npcSpeed*actor.Journey.SpeedScale);
                float wait=queue.Count*(1.1f+actor.QueueReactionDelay);
                float score=travel+wait+crowd.LocalDensity(tail,root)*1.5f;
                if(score<best) {best=score;chosen=gate;}
            }
            if(chosen==null) return null;
            ReleaseGate(actor);GateQueue(chosen).Add(actor);return chosen;
        }
        public int GateSlot(Metro3DPassenger actor,Metro3DFacility gate)
        {
            var queue=GateQueue(gate);return queue.IndexOf(actor);
        }
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
            Metro3DPassenger previous=slot>0?queue[slot-1]:null;
            Vector3 head=gate.transform.localPosition+Vector3.back*direction*1.05f;head.y=world.settings.departureFloor;
            Vector3 axis=Vector3.forward*direction;
            actor.SetQueueContext(previous,gate.transform.parent.TransformDirection(axis),QueueGap(actor),slot!=0||!gate.gateDoor.Passable);
            return QueuePoint(actor,previous,gate.transform.parent,head,axis);
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
            if(boarding&&(HasAlightersNear(train,door)||!world.session.CanPassengersBoard)) return false;
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
                Vector3 local=new Vector3(world.ServiceSide*(-4.25f-row*.74f),0,doorZ+flank*.95f);
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
            float side=-world.ServiceSide*local.x;
            if(Mathf.Abs(local.y)>.18f||side<4||side>6.5f||local.z<10||local.z>Metro3DWorld.PlatformLength-10)return false;
            if(side<4.8f&&NearDoor(local.z,1.2f))return false;
            foreach(bool far in new[]{false,true}) {
                Vector3 delta=local-StairBottom(far,world.ServiceSide);delta.y=0;
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
            if(world.train!=null) for(int d=0;d<Metro3DTrain.DoorZ.Length;d++) if(doorOwners.TryGetValue((world.train,d),out var owner)&&owner==actor) doorOwners.Remove((world.train,d));
            foreach(var queue in doorQueues.Values) queue.Remove(actor);
        }
        public bool HasDoorCrossing(Metro3DTrain train) {for(int d=0;d<Metro3DTrain.DoorZ.Length;d++) if(doorOwners.TryGetValue((train,d),out var owner)&&owner!=null&&owner.isActiveAndEnabled) return true;return false;}
        public bool HasAlighters(Metro3DTrain train) {foreach(var a in world.passengers) if(a.inTrain&&a.Riding==train&&a.WantsToAlight) return true;return false;}
    }
}

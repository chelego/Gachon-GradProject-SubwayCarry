using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // Shared spatial perception and bounded path queue, not per-agent full crowd traversal.
    [DefaultExecutionOrder(-50)]
    public sealed partial class Metro3DCrowd : MonoBehaviour
    {
        public Metro3DWorld world;
        public float LastSteeringCost {get;private set;}
        public int LastStaticRejects {get;private set;}
        public long SteeringTicks {get;private set;}
        public int SteeringCalls {get;private set;}
        public int LearnedSteeringCalls {get;private set;}
        public string SteeringPolicy => learnedSteering?.Label ?? "규칙";
        public long LearnedSteeringTicks {get;private set;}
        Metro3DLearnedSteering learnedSteering;
        [System.NonSerialized] bool loadedSteering;
        public long WalkingStepTicks {get;private set;}
        public int WalkingStepCalls {get;private set;}
        public Metro3DStopReason LastWalkingRejection {get;private set;}
        public int WalkCorridorQueries {get;private set;}
        public long WalkCorridorTicks {get;private set;}
        public long WalkCorridorAllocated {get;private set;}
        public bool CanShortenWalkingPath(Metro3DPassenger actor,Vector3 target)
            =>CanShortenWalkingPath(actor,actor.transform.position,target);
        // Prediction queries start at their predicted position, not repeatedly at
        // the live capsule. This is a read-only corridor test, never a Motor write.
        public bool CanShortenWalkingPath(Metro3DPassenger actor,Vector3 start,Vector3 target)
            =>CanShortenWalkingPath(actor,start,target,false);

        internal bool CanShortenWalkingPath(Metro3DPassenger actor,Vector3 start,Vector3 target,bool pastStair)
        {
            long started=System.Diagnostics.Stopwatch.GetTimestamp(),allocated=System.GC.GetAllocatedBytesForCurrentThread();
            try{
            WalkCorridorQueries++;
            Vector3 delta=target-start;
            float length=new Vector2(delta.x,delta.z).magnitude;
            if(Mathf.Abs(delta.y)>.25f||length>20)return false;
            if(length<.03f)return true;
            Vector3 direction=new Vector3(delta.x,0,delta.z)/length;
            int n=Physics.CapsuleCastNonAlloc(start+Vector3.up*.33f,start+Vector3.up*1.45f,.27f,direction,hits,length,~0,QueryTriggerInteraction.Ignore);
            if(n==hits.Length)return false;
            for(int i=0;i<n;i++)if(!(hits[i].collider is CharacterController)&&!hits[i].collider.transform.IsChildOf(actor.transform))return false;
            Transform root=actor.transform.parent;
            Vector3 side=Vector3.Cross(Vector3.up,direction)*.24f;
            int steps=Mathf.CeilToInt(length/.35f);
            Vector3 previous=root.InverseTransformPoint(start);
            for(int i=1;i<=steps;i++) {
                Vector3 p=Vector3.Lerp(start,target,i/(float)steps),local=root.InverseTransformPoint(p);
                if(!OnOwnedSurface(actor,previous,local,pastStair))return false;
                for(int edge=-1;edge<=1;edge+=2) {
                    int supports=Physics.RaycastNonAlloc(p+side*edge+Vector3.up*.35f,Vector3.down,hits,.65f,~0,QueryTriggerInteraction.Ignore);
                    if(supports==hits.Length)return false;
                    bool supported=false;
                    for(int k=0;k<supports;k++)if(!(hits[k].collider is CharacterController)&&hits[k].collider.transform.IsChildOf(root)&&hits[k].normal.y>.65f&&Mathf.Abs(hits[k].point.y-p.y)<.16f){supported=true;break;}
                    if(!supported)return false;
                }
                previous=local;
            }
            int claims=world.Traffic.PassageClaims(actor,passageStarts,passageEnds);
            for(int i=0;i<claims;i++)if(SegmentGapSquared(start,target,passageStarts[i],passageEnds[i])<.52f*.52f)return false;
            foreach(var claim in standSpaces)if(claim.Key!=actor&&claim.Value.root!=null&&Mathf.Abs(claim.Value.root.TransformPoint(claim.Value.local).y-start.y)<.7f&&SegmentDistanceSquared(claim.Value.root.TransformPoint(claim.Value.local),start,target)<.52f*.52f)return false;
            return true;
            }finally{WalkCorridorTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-started;WalkCorridorAllocated+=System.GC.GetAllocatedBytesForCurrentThread()-allocated;}
        }
        readonly Dictionary<System.Guid,Metro3DPassenger> representations=new Dictionary<System.Guid,Metro3DPassenger>();
        public bool TryPassenger(System.Guid id,out Metro3DPassenger passenger)
        {
            if(representations.TryGetValue(id,out passenger)&&passenger!=null&&passenger.PersistentId==id&&passenger.isActiveAndEnabled)return true;
            passenger=null;return false;
        }
        public int PathRequests {get;private set;}
        public int PathSolves {get;private set;}
        public long PathTicks {get;private set;}
        readonly List<Metro3DPassenger> agents=new List<Metro3DPassenger>();
        readonly Dictionary<(Transform,Vector3Int),List<Metro3DPassenger>> cells=new Dictionary<(Transform,Vector3Int),List<Metro3DPassenger>>();
        readonly List<Transform> scopes=new List<Transform>(8);
        readonly Dictionary<Transform,Bounds> scopeBounds=new Dictionary<Transform,Bounds>();
        struct NeighborSample
        {
            public Metro3DPassenger actor;
            public Vector3 offset,velocity;
            public float compression;
            public bool seated,queued,resting;
        }
        readonly NeighborSample[] neighbors=new NeighborSample[128];
        readonly Vector3[] passageStarts=new Vector3[64],passageEnds=new Vector3[64];
        int neighborCount;
        Transform playerFrame;
        Vector3 previousPlayerLocal,measuredPlayerVelocity;
        float playerSampleTime;
        bool playerSampled;
        bool hashDirty=true;
        readonly List<List<Metro3DPassenger>> buckets=new List<List<Metro3DPassenger>>();
        readonly Queue<Metro3DPassenger> paths=new Queue<Metro3DPassenger>();
        readonly HashSet<Metro3DPassenger> queued=new HashSet<Metro3DPassenger>();
        readonly List<Metro3DPassenger> nearby=new List<Metro3DPassenger>(64);
        readonly Dictionary<(Metro3DTrain,int),(bool leaving,float pressure,float leftRoom,float rightRoom,bool incoming)> alightersByDoor=new(32);
        int alightersFrame=-1;
        readonly Dictionary<(Metro3DTrain,int,int,bool),int> landingChecked=new(48);
        int landingFrame=-1;
        readonly RaycastHit[] hits=new RaycastHit[24];
        readonly Metro3DPassenger[] landing=new Metro3DPassenger[32];
        struct StandClaim {public Transform root;public Vector3 local;}
        readonly Dictionary<Metro3DPassenger,StandClaim> standSpaces=new Dictionary<Metro3DPassenger,StandClaim>();
        readonly Collider[] contacts=new Collider[24];
        Metro3DNavigation navigation;
        public void ForgetSurface(Transform root) {navigation?.Forget(root);}
        float nextHash;
        static readonly Unity.Profiling.ProfilerMarker SteeringMarker=new Unity.Profiling.ProfilerMarker("SubwayCarry.Crowd.Steering");
        static readonly float[] Angles={0,25,50,75,-25,-50,-75,90,-90,135,-135,180};
        static readonly float[] AnticipationAngles={0,12,25,50,75,-12,-25,-50,-75,90,-90};
        static readonly float[] Speeds={1,.65f,.3f,0};
        static readonly float[] DoorAngles={0,12,-12,24,-24};
        Vector3Int Cell(Vector3 p)=>new Vector3Int(Mathf.FloorToInt(p.x/1.6f),Mathf.FloorToInt(p.y/2),Mathf.FloorToInt(p.z/1.6f));
        public void Register(Metro3DPassenger p) { if(!agents.Contains(p)) {agents.Add(p);InvalidateSpatial(); if(buckets.Count<agents.Count) buckets.Add(new List<Metro3DPassenger>(8));} if(p.PersistentId!=System.Guid.Empty)representations[p.PersistentId]=p; }
        public void Unregister(Metro3DPassenger p) {agents.Remove(p);InvalidateSpatial(); queued.Remove(p);standSpaces.Remove(p);if(representations.TryGetValue(p.PersistentId,out var bound)&&bound==p)representations.Remove(p.PersistentId);}
        public void Request(Metro3DPassenger p) { if(queued.Add(p)) {paths.Enqueue(p);PathRequests++;} }
        void Update()
        {
            if(world==null||world.session==null||world.session.SimulationPaused)return;
            SamplePlayerMotion();TickLife();
            if(navigation==null) navigation=new Metro3DNavigation();
            if(hashDirty||Time.time>=nextHash) RebuildHash();
            for(int i=0;i<2&&paths.Count>0;i++) {
                var p=paths.Dequeue();queued.Remove(p);
                if(p!=null) {long started=System.Diagnostics.Stopwatch.GetTimestamp();p.SolvePath(navigation);PathTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-started;PathSolves++;}
            }
        }
        void RebuildHash()
        {
            hashDirty=false;nextHash=Time.time+.1f;
            foreach(var list in buckets) list.Clear();
            cells.Clear();scopes.Clear();scopeBounds.Clear();int bucket=0;
            foreach(var p in agents) if(p!=null&&p.isActiveAndEnabled) {
                Transform root=p.transform.parent;
                if(root==null) continue;
                if(!scopes.Contains(root)) scopes.Add(root);
                if(scopeBounds.TryGetValue(root,out var extent)) {extent.Encapsulate(p.transform.localPosition);scopeBounds[root]=extent;}
                else scopeBounds[root]=new Bounds(p.transform.localPosition,Vector3.zero);
                var key=(root,Cell(p.transform.localPosition));
                if(!cells.TryGetValue(key,out var list)) {list=buckets[bucket++];cells.Add(key,list);}
                list.Add(p);
            }
        }
        public void InvalidateSpatial() {hashDirty=true;alightersFrame=-1;}
        public void PathObstacles(Metro3DPassenger actor,List<Vector3> positions)
        {
            positions.Clear();Transform root=actor.transform.parent;
            // One bounded path request uses the central registration snapshot;
            // walking agents still use TTC rather than becoming static walls.
            foreach(var other in agents) {
                if(other==null||other==actor||!other.isActiveAndEnabled||other.transform.parent!=root)continue;
                if(other.Velocity.sqrMagnitude>.01f&&!other.IsResting&&!other.IsSeated)continue;
                positions.Add(other.transform.localPosition);
            }
        }
        void Query(Vector3 point,int radius=1,Transform onlyRoot=null)
        {
            if(hashDirty) RebuildHash();
            nearby.Clear();
            // Hash in each stable pedestrian frame. The train can move between
            // perception ticks without leaving its passengers in obsolete world cells.
            if(onlyRoot!=null) {
                if(scopeBounds.TryGetValue(onlyRoot,out var extent))AppendNearby(point,radius,onlyRoot,extent);
                return;
            }
            foreach(var root in scopes)
                if(root!=null)AppendNearby(point,radius,root,scopeBounds[root]);
        }
        internal bool HasAscendingAtStair(Transform root,Vector3 top,Vector3 bottom,Vector3 descent)
        {
            if(hashDirty)RebuildHash();
            // Inspect only this flight's cells in its own station frame. Holding
            // departures must begin while the ascending group is on the flight.
            Vector3 padding=new Vector3(.92f,.25f,1.65f);
            var min=Cell(Vector3.Min(top,bottom)-padding);
            var max=Cell(Vector3.Max(top,bottom)+padding);
            Vector3 heading=root.TransformDirection(descent);
            float depth=Vector3.Dot(bottom-top,descent);
            for(int x=min.x;x<=max.x;x++)for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++) {
                if(!cells.TryGetValue((root,new Vector3Int(x,y,z)),out var occupants))continue;
                foreach(var other in occupants) {
                    if(!other||!other.isActiveAndEnabled||other.transform.parent!=root)continue;
                    var journey=other.Journey;
                    bool ascending=(journey.OnStairs||journey.StairApproaching)&&Vector3.Dot(journey.StairForward,heading)<-.85f;
                    if(!ascending&&!journey.ClearingStairLanding)continue;
                    Vector3 offset=other.transform.localPosition-top;
                    float along=Vector3.Dot(offset,descent);
                    if(along>=-1.65f&&along<=depth+1&&Mathf.Abs(offset.x)<.92f)return true;
                }
            }
            return false;
        }

        internal bool CanWaitOffStair(Metro3DPassenger actor,Vector3 local)
        {
            var frame=actor.transform.parent;
            Vector3 point=frame.TransformPoint(local);
            if(!SpaceFree(point,actor,.62f))return false;
            Query(point,2,frame);
            foreach(var other in nearby) {
                if(!other||other==actor||!other.isActiveAndEnabled)continue;
                if(other.Journey.DescendingWaitPoint is Vector3 held&&(held-local).sqrMagnitude<.68f*.68f)return false;
            }
            return CanShortenWalkingPath(actor,actor.transform.position,point,pastStair:true);
        }
        void AppendNearby(Vector3 point,int radius,Transform root,Bounds bounds)
        {
            Vector3 local=root.InverseTransformPoint(point);
            Vector3 min=bounds.min,max=bounds.max;float reach=(radius+1)*1.6f;
            if(local.x<min.x-reach||local.x>max.x+reach||local.z<min.z-reach||local.z>max.z+reach||local.y<min.y-1.5f||local.y>max.y+1.5f)return;
            var k=Cell(local);
            int lo=Mathf.Max(Mathf.FloorToInt(min.y/2),Mathf.FloorToInt((local.y-1.5f)/2));
            int hi=Mathf.Min(Mathf.FloorToInt(max.y/2),Mathf.FloorToInt((local.y+1.5f)/2));
            for(int y=lo;y<=hi;y++)for(int x=-radius;x<=radius;x++)for(int z=-radius;z<=radius;z++)
                if(cells.TryGetValue((root,new Vector3Int(k.x+x,y,k.z+z)),out var list))nearby.AddRange(list);
        }
        void SamplePlayerMotion()
        {
            if(world.player==null) return;
            var player=world.player.transform;var frame=player.parent;
            Vector3 local=frame!=null?frame.InverseTransformPoint(player.position):player.position;
            float elapsed=Time.time-playerSampleTime;
            if(playerSampled&&frame==playerFrame&&elapsed>.0001f) {
                Vector3 delta=local-previousPlayerLocal;
                measuredPlayerVelocity=delta.sqrMagnitude<Mathf.Pow(elapsed*8+.1f,2)?delta/elapsed:Vector3.zero;
                if(frame!=null) measuredPlayerVelocity=frame.TransformDirection(measuredPlayerVelocity);
                measuredPlayerVelocity.y=0;
            } else measuredPlayerVelocity=Vector3.zero;
            playerFrame=frame;previousPlayerLocal=local;playerSampleTime=Time.time;playerSampled=true;
        }
        public bool AlightersAt(Metro3DTrain train,int door)=>ObserveDoor(train,door).leaving;
        internal (bool leaving,float pressure,float leftRoom,float rightRoom,bool incoming) ObserveDoor(Metro3DTrain train,int door)
        {
            if(alightersFrame!=Time.frameCount) {
                alightersFrame=Time.frameCount;alightersByDoor.Clear();
            }
            var key=(train,door);
            if(alightersByDoor.TryGetValue(key,out var observation))return observation;
            Vector3 p=train.DoorPoint(door,false),outwards=-train.transform.right*train.side;
            Vector3 onPlatform=train.DoorPoint(door,true);
            bool present=false,incoming=false;
            float pressure=0,leftRoom=1,rightRoom=1;
            Vector3 left=train.DoorPoint(door,false,-1),right=train.DoorPoint(door,false,1);
            Query(p,3);
            foreach(var a in nearby) {
                if(a==null)continue;
                Vector3 feet=a.transform.position;
                float gap=(feet-p).sqrMagnitude;
                if(gap>=25)continue;
                if(a.Riding==train||a.CrossingTrain==train) {
                    float support=a.WalkingRadius+.24f;
                    leftRoom=Mathf.Min(leftRoom,Mathf.InverseLerp(support,.92f,(feet-left).magnitude));
                    rightRoom=Mathf.Min(rightRoom,Mathf.InverseLerp(support,.92f,(feet-right).magnitude));
                }
                bool outside=a.ClearingDoor(train,door);
                bool crossing=a.RetreatingDoor&&a.CrossingTrain==train&&a.CrossingDoor==door;
                bool preparing=a.Riding==train&&a.WantsToAlight&&(gap<2.05f*2.05f||a.DoorCrossing)&&
                    (a.AlightingDoor>=0?a.AlightingDoor:train.NearestDoor(a.transform.position))==door;
                if(!outside&&!crossing&&!preparing)continue;
                present=true;
                incoming|=crossing||preparing;
                float distance=outside?Mathf.Max(0,Vector3.Dot(a.transform.position-onPlatform,outwards)):Mathf.Sqrt(gap);
                float approaching=Mathf.InverseLerp(2.05f,.24f,distance);
                float motion=Mathf.Clamp01(Vector3.Dot(a.Velocity,outwards)/.75f);
                pressure=Mathf.Max(pressure,Mathf.Clamp01(.20f+.62f*approaching+.18f*motion));
            }
            var reading=(present,pressure,leftRoom,rightRoom,incoming);
            alightersByDoor[key]=reading;
            return reading;
        }
        public float LocalDensity(Vector3 point,Transform root)
        {
            Query(point,1,root);int count=0;
            foreach(var p in nearby)if(p!=null&&p.transform.parent==root&&(p.transform.position-point).sqrMagnitude<4)count++;
            return Mathf.Clamp01(count/8f);
        }
        internal bool HasPassingPassenger(Metro3DPassenger actor)
        {
            Vector3 center=actor.transform.position;
            Query(center,1,actor.transform.parent);
            foreach(var other in nearby){
                if(other==null||other==actor||other==actor.Companion||other.IsSeated)continue;
                Vector3 toWaiter=center-other.transform.position;
                if(Mathf.Abs(toWaiter.y)>.45f)continue;
                toWaiter.y=0;
                Vector3 motion=other.Velocity;motion.y=0;
                float speed=motion.magnitude;
                if(speed<.27f||toWaiter.sqrMagnitude>1.45f*1.45f)continue;
                Vector3 direction=motion/speed;
                float along=Vector3.Dot(toWaiter,direction);
                float width=actor.BodyRadius+other.BodyRadius+.13f;
                if(along>.08f&&along<speed*1.1f+.32f&&(toWaiter-direction*along).sqrMagnitude<width*width)
                    return true;
            }
            return false;
        }
        public bool SpaceFree(Vector3 point,Metro3DPassenger except,float radius=.6f)
        {
            if(world.player!=null&&Mathf.Abs(world.player.transform.position.y-point.y)<.7f&&(world.player.transform.position-point).sqrMagnitude<radius*radius)return false;
            foreach(var reservation in standSpaces) if(reservation.Key!=except&&reservation.Key!=null&&reservation.Value.root!=null&&(reservation.Value.root.TransformPoint(reservation.Value.local)-point).sqrMagnitude<radius*radius) return false;
            Query(point);
            foreach(var other in nearby) if(other!=null&&other!=except&&Mathf.Abs(other.transform.position.y-point.y)<.7f&&(other.transform.position-point).sqrMagnitude<radius*radius) return false;
            return true;
        }
        public bool StaticSpaceFree(Vector3 point,Transform root,float radius=.28f)
        {
            int n=Physics.OverlapCapsuleNonAlloc(point+Vector3.up*.33f,point+Vector3.up*1.45f,radius,contacts,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++) if(!(contacts[i] is CharacterController)&&contacts[i].transform.IsChildOf(root)) return false;
            return n<contacts.Length;
        }
        public bool PrepareStandSpace(Metro3DPassenger actor,Vector3 point)
        {
            // A request to stand is not an exclusive claim on already occupied
            // space. Grant it atomically only after the landing has cleared.
            standSpaces.Remove(actor);
            Query(point);int count=0;
            foreach(var other in nearby) if(count<landing.Length&&other!=null&&other!=actor&&other.transform.parent==actor.transform.parent&&!other.WantsToAlight&&(other.transform.position-point).sqrMagnitude<.75f*.75f) landing[count++]=other;
            for(int i=0;i<count;i++) {landing[i].ClearDoorLanding(point);landing[i]=null;}
            if(!SpaceFree(point,actor,.52f)) return false;
            foreach(var other in nearby) {
                if(other==null||other==actor||other.transform.parent!=actor.transform.parent||Mathf.Abs(other.transform.position.y-point.y)>.7f)continue;
                Vector3 motion=other.Velocity;motion.y=0;
                Vector3 travel=motion*(.1f+motion.magnitude/(2*3.8f));
                Vector3 offset=point-other.transform.position;offset.y=0;
                float t=Mathf.Clamp01(Vector3.Dot(offset,travel)/Mathf.Max(.0001f,travel.sqrMagnitude));
                if((offset-travel*t).sqrMagnitude<.52f*.52f)return false;
            }
            int n=Physics.OverlapCapsuleNonAlloc(point+Vector3.up*.33f,point+Vector3.up*1.45f,.23f,contacts,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++) if(!(contacts[i] is CharacterController)&&!contacts[i].transform.IsChildOf(actor.transform)) return false;
            if(n==contacts.Length)return false;
            standSpaces[actor]=new StandClaim{root=actor.transform.parent,local=actor.transform.parent.InverseTransformPoint(point)};
            return true;
        }
        public void ReleaseStandSpace(Metro3DPassenger actor) {standSpaces.Remove(actor);}
        public Vector3 SafeVelocity(Metro3DPassenger actor,Vector3 desired)
        {
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            try {using(SteeringMarker.Auto()) return SelectVelocity(actor,desired);}
            finally {SteeringTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-started;SteeringCalls++;}
        }
        Vector3 SelectVelocity(Metro3DPassenger actor,Vector3 desired)
        {
            if(desired.sqrMagnitude<.0001f) return Vector3.zero;
            Transform pedestrian=actor.transform;
            Vector3 origin=pedestrian.position;
            Transform frame=pedestrian.parent;
            Query(origin,2,actor.Riding!=null&&actor.Riding.Moving?frame:null);
            float requested=desired.magnitude,density=0;
            Vector3 forward=desired/requested;
            if(actor.Journey.StairForward.sqrMagnitude>.5f)
                forward=AnticipateStairPass(actor,origin,forward);
            float walkingRadius=actor.WalkingRadius;
            bool contactDoor=actor.DoorContactContext(out var localTrain,out int localDoor);
            Vector3 currentVelocity=actor.Velocity;
            bool checkingDoorStall=contactDoor&&actor.DoorStallReady&&!actor.HasCompanion;
            if(checkingDoorStall&&!actor.TravelsOutward) {
                checkingDoorStall=!ObserveDoor(localTrain,localDoor).incoming;
                Query(origin,2,actor.Riding!=null&&actor.Riding.Moving?frame:null);
            }
            actor.DoorStallPressure=false;
            neighborCount=0;
            int farthest=0;
            float farthestDistance=0;
            foreach(var other in nearby) {
                if(!RelativePosition(actor,other,origin,frame,out Vector3 offset))continue;
                offset.y=0;
                float distance=offset.sqrMagnitude;
                if(distance<4) density++;
                if(neighborCount==neighbors.Length&&distance>=farthestDistance)continue;
                bool seated=other.IsSeated;
                float compression=contactDoor&&!seated?DoorCompression(localTrain,localDoor,other):0;
                if(!seated&&ShoulderContact(actor,forward,offset))compression=Mathf.Max(compression,.028f);
                var sample=new NeighborSample{actor=other,offset=offset,velocity=other.Velocity,seated=seated,
                    compression=compression,
                    queued=other.QueueActive,resting=other.IsResting};
                if(neighborCount<neighbors.Length) {
                    if(distance>farthestDistance) {farthest=neighborCount;farthestDistance=distance;}
                    neighbors[neighborCount++]=sample;
                } else {
                    neighbors[farthest]=sample;
                    farthest=0;farthestDistance=neighbors[0].offset.sqrMagnitude;
                    for(int i=1;i<neighborCount;i++) {
                        float storedDistance=neighbors[i].offset.sqrMagnitude;
                        if(storedDistance>farthestDistance) {farthest=i;farthestDistance=storedDistance;}
                    }
                }
            }
            if(actor.CanYieldToFlow) {
                if(TryFlowYield(actor,origin,forward,out var courtesy)) {
                    LastSteeringCost=0;LastStaticRejects=0;return courtesy;
                }
                if(actor.ContinueNarrowFlow(origin,forward)) {
                    actor.FollowingNarrowFlow=true;
                    Vector3 axis=actor.YieldFlow,offset=origin-actor.YieldOrigin;
                    float progress=Vector3.Dot(offset,axis);
                    Vector3 center=actor.YieldOrigin+axis*progress;center.y=origin.y;
                    Vector3 correction=center-origin;
                    // Follow a point further down the lane. A pure sideways
                    // centering command overshoots under inertia, then flips
                    // around to cross the same center again.
                    Vector3 lead=center+axis*.7f;
                    forward=correction.sqrMagnitude>.10f*.10f&&CanFollowWalkingSegment(actor,lead)
                        ?(lead-origin).normalized:(axis+correction*.8f).normalized;
                }
            }
            density=Mathf.Clamp01(density/8);
            float personal=Mathf.Lerp(Mathf.Clamp(actor.Journey.Personality.PersonalSpace,.08f,.45f),.06f,density);
            // Authored bidirectional queue lanes are .56m apart. Compress only
            // their comfort margin, leaving the .48m body and motor unchanged.
            if(actor.inTrain&&actor.QueueActive)personal=Mathf.Min(personal,.06f);
            if(actor.PortalCommitted||actor.ClearingOwnDoor) personal=.03f;
            float preferred=requested*actor.PerceivedCarrySpeedScale(forward),leaderGap=float.PositiveInfinity;
            bool stairFlow=actor.Journey.StairForward.sqrMagnitude>.5f;
            bool laneLocked=actor.PortalCommitted&&!actor.RetreatingDoor||actor.QueueCommitted||
                actor.FollowingQueueContinuously||stairFlow||actor.NarrowWalking||actor.FollowingNarrowFlow;
            if(stairFlow||actor.NarrowWalking)personal=Mathf.Min(personal,.045f);
            if(contactDoor&&(actor.DoorCrossing||actor.DoorCourtesyStep))personal=Mathf.Min(personal,.025f);
            bool outgoingFlow=contactDoor&&(actor.TravelsOutward||actor.ClearingOwnDoor);
            bool doorFlow=actor.DoorCrossing||outgoingFlow;
            float followTime=doorFlow?(.17f+.78f*actor.QueueReactionDelay)/(1+.18f*actor.BoardingUrgency):0;
            LastStaticRejects=0;
            bool freeWalking=!laneLocked&&actor.CanYieldToFlow&&!actor.RecoveryActive&&!world.walkingReviewOnly;
            bool merging=freeWalking&&!contactDoor&&neighborCount>0;
            bool establishedFlow=false;
            int anticipated=freeWalking?AnticipatePassing(actor,origin,forward,requested,walkingRadius):0;
            // A passenger approaching an open flank can use the group policy
            // for local movement. A committed queue or door retains its lane.
            bool freeBoardApproach=contactDoor&&!laneLocked&&actor.StagedBoarding&&actor.IntendsDoorAdvance&&
                actor.ApproachingOpenDoor&&!actor.WaitingForQueue;
            bool useLearned=(freeWalking||freeBoardApproach)&&(anticipated!=0||neighborCount>=2)&&
                requested>=.35f&&neighborCount>0&&EvaluateLearned(actor,forward,requested,currentVelocity,walkingRadius,anticipated!=0);
            actor.LearnedSteeringActive=useLearned;
            // Following is a speed constraint before it is a reason to overtake.
            // The queue owner, not this movement layer, grants passage rights.
            for(int neighbor=0;neighbor<neighborCount;neighbor++) {
                var other=neighbors[neighbor];Vector3 offset=other.offset;
                if(actor.FollowingQueueContinuously&&other.actor==actor.QueueLeader)continue;
                float ahead=Vector3.Dot(offset,forward);
                if(ahead>3||ahead<=0&&(!merging||establishedFlow||ahead< -1.65f))continue;
                float lateralSquared=(offset-forward*ahead).sqrMagnitude;
                if(merging&&!establishedFlow&&!other.seated&&!other.resting&&!other.queued&&
                    Mathf.Abs(ahead)<1.65f&&lateralSquared<.48f*.48f) {
                    float flowSpeed=Vector3.Dot(other.velocity,forward);
                    establishedFlow=flowSpeed>.42f&&flowSpeed*flowSpeed>.73f*other.velocity.sqrMagnitude;
                }
                if(ahead<=0)continue;
                float leadingSpeed=doorFlow?Vector3.Dot(other.velocity,forward):0;
                bool sameOutgoing=outgoingFlow&&other.actor.AtDoorContact(localTrain,localDoor)&&
                    (other.actor.TravelsOutward||other.actor.ClearingOwnDoor)&&
                    Vector3.Dot(other.actor.DesiredVelocity,forward)>.15f;
                bool sameIncoming=actor.DoorCrossing&&!actor.TravelsOutward&&!other.actor.TravelsOutward&&
                    (leadingSpeed>.14f||other.actor.DoorCrossing&&
                        other.actor.CrossingTrain==actor.CrossingTrain&&other.actor.CrossingDoor==actor.CrossingDoor&&
                        other.actor.TravelsOutward==actor.TravelsOutward);
                bool sameFlow=!other.seated&&(sameOutgoing||sameIncoming);
                if(sameFlow) {
                    // Tordeux et al., Collision-free speed model (2016).
                    // The remaining space sets the closing speed against a
                    // measured leader; opposing traffic keeps its own checks.
                    float body=walkingRadius+other.actor.WalkingRadius-other.compression;
                    float intercept=body*body-lateralSquared;
                    if(intercept<=0)continue;
                    float freeSpace=ahead-Mathf.Sqrt(intercept)-.025f;
                    float leadSpeed=Mathf.Max(0,leadingSpeed);
                    preferred=Mathf.Min(preferred,Mathf.Max(0,leadSpeed+freeSpace/followTime));
                    continue;
                }
                float lateralLimit=stairFlow?.34f:.43f;
                if(lateralSquared>lateralLimit*lateralLimit)continue;
                if(actor.PortalCommitted&&other.queued) continue;
                // A seat occupant or someone waiting in an adjacent facility
                // is an obstacle to pass, not a leader of this walking flow.
                if(other.seated||other.resting||other.queued&&!actor.QueueCommitted) continue;
                bool stopped=other.velocity.sqrMagnitude<.01f;
                if(!actor.QueueCommitted&&stopped&&(!actor.FollowingNarrowFlow||Vector3.Dot(other.actor.DesiredVelocity,forward)<.18f))continue;
                float alignment=!stopped?Vector3.Dot(other.velocity.normalized,forward):1;
                if(alignment<.45f) continue;
                float gap=actor.QueueCommitted?actor.QueueGap:
                    actor.WalkingRadius+other.actor.WalkingRadius+personal+requested*Mathf.Clamp(actor.Journey.Personality.ReactionDelay,.12f,.5f);
                float cap=Mathf.Max(0,Vector3.Dot(other.velocity,forward)+(ahead-gap)/.7f);
                if(ahead<leaderGap) {leaderGap=ahead;preferred=Mathf.Min(preferred,cap);}
            }
            if(!actor.PortalCommitted&&actor.QueueCommitted&&!actor.FollowingQueueContinuously&&actor.QueueLeader!=null) {
                Vector3 offset=actor.QueueLeader.transform.position-origin;offset.y=0;
                float ahead=Vector3.Dot(offset,actor.QueueForward);
                float cap=Mathf.Max(0,Vector3.Dot(actor.QueueLeader.Velocity,actor.QueueForward)+(ahead-actor.QueueGap)/.65f);
                preferred=Mathf.Min(preferred,cap);
            }
            float best=float.PositiveInfinity;Vector3 selected=Vector3.zero;int selectedSide=0;
            int passageCount=world.Traffic.PassageClaims(actor,passageStarts,passageEnds);
            Matrix4x4 worldToFrame=actor.transform.parent.worldToLocalMatrix;
            var player=world.player;
            Vector3 playerOffset=player!=null?player.transform.position-origin:Vector3.zero;playerOffset.y=0;
            bool checkPlayer=player!=null&&Mathf.Abs(player.transform.position.y-origin.y)<1.5f&&
                !(actor.inTrain&&actor.Riding!=null&&actor.Riding.Moving&&player.Riding!=actor.Riding);
            Vector3 localOrigin=worldToFrame.MultiplyPoint3x4(origin);
            float lookahead=actor.FollowingQueueContinuously?Mathf.Max(.25f,requested*.6f):
                Mathf.Min(Mathf.Max(.25f,requested*.6f),actor.SteeringDistance+.04f);
            float speedDenominator=Mathf.Max(.2f,requested);
            if(checkingDoorStall)actor.DoorStallPressure=HasStationaryDoorBlocker(actor,localTrain,localDoor);
            bool squeezing=contactDoor&&actor.DoorCrossing&&!actor.RetreatingDoor&&
                (actor.HasDoorShoulderContact||actor.DoorStallPressure)&&currentVelocity.sqrMagnitude<.32f*.32f&&
                !actor.HasCompanion&&Vector3.Dot(forward,localTrain.transform.right*localTrain.side)*
                    (actor.TravelsOutward?-1:1)>.72f;
            foreach(float angle in squeezing?DoorAngles:anticipated!=0?AnticipationAngles:Angles) {
                float absolute=Mathf.Abs(angle);
                if(laneLocked&&angle!=0&&!squeezing) break;
                if(squeezing&&angle!=0&&best<.60f)break;
                // A rider returning to the platform can step around the
                // nearest waiting shoulder, within the same checked opening.
                if(actor.RetreatingDoor&&absolute>50)continue;
                if(absolute>100&&!actor.RecoveryActive) continue;
                // Every other term is nonnegative, so an angle that cannot
                // beat the current choice needs neither a cast nor TTC work.
                float angleCost=absolute*.013f;
                if(angleCost>=best)continue;
                float available=laneLocked||angle==0?preferred:requested;
                if((requested-available)/speedDenominator*1.15f+angleCost>=best)continue;
                Vector3 direction=angle==0?forward:Quaternion.Euler(0,angle,0)*forward;
                if(actor.ExitingPlatformQueue&&Vector3.Dot(direction,forward)<.70f)continue;
                if(squeezing&&angle!=0) {
                    Vector3 lane=localTrain.DoorPoint(localDoor,!actor.TravelsOutward,actor.CrossingLane);
                    Vector3 lateral=localTrain.transform.forward;
                    float offset=Vector3.Dot(origin-lane,lateral);
                    float shifted=offset+Vector3.Dot(direction,lateral)*Mathf.Min(requested*.45f,.48f);
                    // Stay inside the reserved branch and the static jamb.
                    if(Mathf.Abs(shifted)>Mathf.Max(.22f,Mathf.Abs(offset))+.001f)continue;
                }
                float clear=Clearance(actor,origin+Vector3.up*.72f,direction,lookahead);
                foreach(float scale in Speeds) {
                    if(scale==0&&angle!=0) break;
                    float speed=available*scale;
                    float cost=(requested-speed)/speedDenominator*1.15f+angleCost;
                    if(cost>=best)break;
                    if(speed>.01f&&speed*.45f+.06f>clear) {LastStaticRejects++;continue;}
                    Vector3 candidate=direction*speed;
                    // An established stream keeps its heading while a
                    // crossing rider picks an opening. TTC still applies.
                    bool checkMerge=merging&&(!establishedFlow||absolute>18)&&speed>.18f;
                    for(int claim=0;claim<passageCount;claim++) {
                        float before=SegmentDistanceSquared(origin,passageStarts[claim],passageEnds[claim]);
                        float after=SegmentDistanceSquared(origin+candidate*.55f,passageStarts[claim],passageEnds[claim]);
                        if(before<.52f*.52f&&after>before+.002f)continue;
                        if(after<.52f*.52f)cost+=45+(.52f-Mathf.Sqrt(after))*100;
                    }
                    if(useLearned)cost+=learnedSteering.Cost(angle,scale);
                    if(anticipated!=0) {
                        float lateral=Vector3.Dot(candidate,Vector3.Cross(Vector3.up,forward))*anticipated;
                        cost+=Mathf.Clamp01((.24f-lateral)/.24f)*.72f;
                        cost+=Mathf.Max(0,absolute-25)*.016f;
                    }
                    if(angle<0) cost+=.08f;
                    if(actor.PassingSide!=0&&Time.time<actor.PassingUntil&&Mathf.Abs(angle)>.1f&&Mathf.Sign(angle)!=actor.PassingSide) cost+=1.25f;
                    cost+=(candidate-currentVelocity).sqrMagnitude*.32f;
                    if(merging&&angle!=0&&leaderGap<1.55f&&preferred>requested*.52f)
                        cost+=Mathf.Min(absolute,45)*.007f;
                    if(cost>=best)continue;
                    Vector3 predicted=worldToFrame.MultiplyPoint3x4(origin+candidate*.45f);
                    if(!OnOwnedSurface(actor,localOrigin,predicted)) continue;
                    foreach(var reservation in standSpaces) {
                        if(reservation.Key==actor||reservation.Key==null||reservation.Value.root==null||Mathf.Abs(reservation.Value.root.TransformPoint(reservation.Value.local).y-origin.y)>1.2f) continue;
                        Vector3 offset=reservation.Value.root.TransformPoint(reservation.Value.local)-origin;offset.y=0;
                        if(offset.sqrMagnitude<.55f*.55f&&Vector3.Dot(offset,candidate)<-.015f) continue;
                        float distance=(offset-candidate*.5f).magnitude;
                        if(distance<.52f) cost+=45+(.52f-distance)*100;
                    }
                    for(int neighbor=0;neighbor<neighborCount&&cost<best;neighbor++) {
                        var other=neighbors[neighbor];Vector3 offset=other.offset;
                        // Preserve an escape from existing light contact without adding repulsion forces.
                        float compression=other.compression;
                        float body=walkingRadius+(other.seated?.10f:other.actor.WalkingRadius)-compression;
                        if(offset.sqrMagnitude<body*body&&Vector3.Dot(offset,candidate-other.velocity)<-.015f) continue;
                        bool following=actor.FollowingQueueContinuously&&other.actor==actor.QueueLeader;
                        if(checkMerge&&!other.seated&&!other.queued&&offset.sqrMagnitude<2.45f*2.45f)
                            cost+=MergeConflict(actor,other,forward,requested,candidate,speed,body);
                        cost+=CollisionCost(offset,currentVelocity,candidate,other.velocity,body,following?.025f:personal,
                            following?QueueBrakingHorizon(candidate,other.velocity):compression>0||actor.ClearingOwnDoor||actor.DoorCrossing?.35f:1.5f);
                    }
                    if(checkPlayer&&cost<best)cost+=CollisionCost(playerOffset,currentVelocity,candidate,measuredPlayerVelocity,.48f,personal+.06f);
                    if(cost<best) {best=cost;selected=candidate;selectedSide=Mathf.Abs(angle)>.1f?(angle>0?1:-1):0;}
                }
            }
            LastSteeringCost=best;
            if(best>=35||float.IsInfinity(best)) return Vector3.zero;
            if(selectedSide!=0&&selected.sqrMagnitude>.02f) actor.CommitPassingSide(selectedSide);
            return selected;
        }
        bool HasStationaryDoorBlocker(Metro3DPassenger actor,Metro3DTrain train,int door)
        {
            Vector3 inward=train.transform.right*train.side*(actor.TravelsOutward?-1:1);
            bool blocked=false;
            for(int i=0;i<neighborCount;i++) {
                var sample=neighbors[i];
                float ahead=Vector3.Dot(sample.offset,inward);
                if(ahead<=.06f||ahead>.62f||
                    (sample.offset-inward*ahead).sqrMagnitude>.36f*.36f)continue;
                // A moving pedestrian owns the next step across the opening.
                // A seated rider does not offer a pushable route either.
                if(sample.seated||sample.velocity.sqrMagnitude>.17f*.17f)return false;
                if(sample.actor.AtDoorContact(train,door)&&!sample.actor.WaitingForQueue)
                    blocked=true;
            }
            return blocked;
        }

        static float MergeConflict(Metro3DPassenger actor,NeighborSample other,Vector3 forward,float requested,
            Vector3 candidate,float stepSpeed,float body)
        {
            Vector3 peer=other.velocity;
            float peerSquared=peer.sqrMagnitude;
            if(peerSquared<.16f||other.resting)return 0;
            float determinant=candidate.x*peer.z-candidate.z*peer.x;
            if(determinant*determinant<stepSpeed*stepSpeed*peerSquared*.48f*.48f)return 0;
            Vector3 offset=other.offset;
            float crossing=(offset.x*peer.z-offset.z*peer.x)/determinant;
            float theirCrossing=(offset.x*candidate.z-offset.z*candidate.x)/determinant;
            if(crossing<.10f||crossing>1.4f||theirCrossing<.14f||theirCrossing>1.45f)return 0;
            float expected=crossing*stepSpeed/Mathf.Max(.2f,requested);
            // Keep the established stream's right of way. A faster clear
            // crossing or a small slowdown that slots in behind both work.
            if(Vector3.Dot(forward,candidate)>.94f*stepSpeed&&expected<theirCrossing-.24f)return 0;
            if(Mathf.Abs(expected-theirCrossing)<.16f&&actor.index<other.actor.index&&
                actor.Velocity.sqrMagnitude>.18f)return 0;
            float margin=Mathf.Clamp(body/Mathf.Max(.55f,Mathf.Sqrt(peerSquared))+.11f,.30f,.74f);
            return Mathf.Max(0,margin-Mathf.Abs(crossing-theirCrossing))*3.4f;
        }

        int AnticipatePassing(Metro3DPassenger actor,Vector3 origin,Vector3 forward,float speed,float radius)
        {
            if(speed<.28f)return 0;
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            float earliest=2.05f;
            int side=0;Metro3DPassenger peer=null;
            for(int i=0;i<neighborCount;i++) {
                var sample=neighbors[i];
                if(sample.seated||sample.resting||sample.queued||sample.actor.transform.parent!=actor.transform.parent||
                    Mathf.Abs(sample.actor.transform.position.y-origin.y)>.45f)continue;
                float ahead=Vector3.Dot(sample.offset,forward);
                if(ahead<.24f||ahead>3.45f)continue;
                Vector3 theirIntent=sample.actor.DesiredVelocity;
                Vector3 heading=theirIntent.sqrMagnitude>.04f?theirIntent:sample.velocity;
                if(heading.sqrMagnitude<.03f||Vector3.Dot(heading,forward)>.45f*heading.magnitude)continue;
                Vector3 otherVelocity=sample.velocity;
                if(otherVelocity.sqrMagnitude<.16f&&theirIntent.sqrMagnitude>.09f)
                    otherVelocity=Vector3.Lerp(otherVelocity,theirIntent,.65f);
                Vector3 relative=forward*speed-otherVelocity;
                float t=Vector3.Dot(sample.offset,relative)/Mathf.Max(.01f,relative.sqrMagnitude);
                if(t<.03f||t>=earliest)continue;
                Vector3 gap=sample.offset-relative*t;
                float space=radius+sample.actor.WalkingRadius+.20f;
                if(gap.sqrMagnitude>space*space)continue;
                float lateral=Vector3.Dot(gap,right);
                // Keep a chosen side through the encounter. Both directions
                // prefer their own right when neither person has a clear side.
                side=Time.time<actor.PassingUntil?actor.PassingSide:lateral>.20f?-1:1;
                earliest=t;peer=sample.actor;
            }
            if(side==0)return 0;
            if(TightAcross(actor,origin,right)||TightAcross(actor,origin+forward*(speed*earliest),right))return 0;
            Vector3 chest=origin+Vector3.up*.72f;
            Vector3 aim=(forward+right*side*.35f).normalized;
            if(Clearance(actor,chest,aim,.92f)<.89f) {
                aim=(forward-right*side*.35f).normalized;
                if(Clearance(actor,chest,aim,.92f)<.89f)return 0;
                side=-side;
            }
            if(earliest<1.1f)actor.AcknowledgePassing(peer);
            return side;
        }
        bool EvaluateLearned(Metro3DPassenger actor,Vector3 forward,float speed,Vector3 velocity,float radius,bool crossing)
        {
            if(!loadedSteering) {learnedSteering=Metro3DLearnedSteering.Load();loadedSteering=true;}
            if(learnedSteering==null||!crossing&&!learnedSteering.GroupPolicy)return false;
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            learnedSteering.Begin(forward,speed,velocity,Time.time<actor.PassingUntil?actor.PassingSide:0);
            for(int i=0;i<neighborCount;i++) {
                var sample=neighbors[i];
                learnedSteering.Observe(sample.offset,sample.velocity,radius+(sample.seated?.10f:sample.actor.WalkingRadius));
            }
            bool applied=learnedSteering.Evaluate();
            if(applied)LearnedSteeringCalls++;
            LearnedSteeringTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-started;
            return applied;
        }
        bool TryFlowYield(Metro3DPassenger actor,Vector3 origin,Vector3 forward,out Vector3 velocity)
        {
            velocity=Vector3.zero;
            if(actor.FlowYieldActive) {
                if(!actor.RejoiningFlow&&!ShouldKeepYielding(actor))actor.RejoinFlow();
                Vector3 goal=actor.YieldSpot;
                if(actor.RejoiningFlow) {
                    Vector3 axis=actor.YieldFlow;
                    Vector3 offset=origin-actor.YieldOrigin;offset.y=0;
                    float along=Vector3.Dot(offset,axis);
                    Vector3 lateral=offset-axis*along;
                    if(lateral.sqrMagnitude<.11f*.11f||Time.time>=actor.YieldLimit) {
                        actor.FinishFlowRejoin();
                        return false;
                    }
                    // Rejoin while advancing. Returning to the original mark
                    // first introduces a lateral stop and another turn.
                    Vector3 lane=actor.YieldOrigin+axis*Mathf.Max(0,along);
                    goal=lane+axis*.76f;
                    if(!CanFollowWalkingSegment(actor,goal)) {
                        goal=lane+axis*.28f;
                        if(!CanFollowWalkingSegment(actor,goal))goal=lane;
                    }
                }
                Vector3 delta=goal-origin;delta.y=0;
                if(delta.sqrMagnitude>.095f*.095f)
                    velocity=Vector3.ClampMagnitude(delta*2.5f,actor.RejoiningFlow?1.05f:.85f);
                else actor.FaceJourney(actor.YieldFlow);
                return true;
            }
            // A pair with room can use ordinary avoidance. Only look for
            // a pocket when another rider is feeding the same narrow encounter.
            if(neighborCount==0||actor.NarrowWalking)return false;
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            float ownSpeed=Mathf.Max(0,Vector3.Dot(actor.Velocity,forward));
            int nearest=-1;
            bool closeLeader=false;
            Metro3DPassenger yieldingLeader=null;
            float leaderAhead=1.9f;
            float earliest=float.PositiveInfinity;
            for(int i=0;i<neighborCount;i++) {
                var sample=neighbors[i];var other=sample.actor;
                if(sample.resting||sample.seated||sample.queued||other.transform.parent!=actor.transform.parent)continue;
                if(Mathf.Abs(other.transform.position.y-origin.y)>.45f)continue;
                float ahead=Vector3.Dot(sample.offset,forward);
                float lateral=Vector3.Dot(sample.offset,right);
                if(other.FlowYieldActive) {
                    float alignment=Vector3.Dot(other.YieldFlow,forward);
                    if(alignment>.78f&&ahead>.10f&&ahead<leaderAhead&&Mathf.Abs(lateral)<1.04f&&
                        !other.RejoiningFlow&&other.HasYieldingRider) {
                        yieldingLeader=other;
                        leaderAhead=ahead;
                    }
                    if(alignment<-.78f&&!other.RejoiningFlow)continue;
                }
                if(ahead>.12f&&ahead<1.15f&&Mathf.Abs(lateral)<.43f&&Vector3.Dot(other.DesiredVelocity,forward)>.18f)
                    closeLeader=true;
                if(ahead<.38f||ahead>3.5f||Mathf.Abs(lateral)>.55f)continue;
                float otherSpeed=-Vector3.Dot(sample.velocity,forward);
                if(otherSpeed<-.10f||otherSpeed<.16f&&Vector3.Dot(other.DesiredVelocity,forward)>-.20f)continue;
                float approachingSpeed=Mathf.Max(0,-Vector3.Dot(other.DesiredVelocity,forward));
                float combined=Mathf.Max(ownSpeed,Vector3.Dot(actor.DesiredVelocity,forward)*.75f)+
                    Mathf.Max(otherSpeed,approachingSpeed*.75f);
                float time=(ahead-.48f)/Mathf.Max(.30f,combined);
                if(time>1.75f&&ahead>1.05f||time>=earliest)continue;
                earliest=time;nearest=i;
            }
            if(yieldingLeader!=null) {
                Vector3 mergePoint=origin+right*Vector3.Dot(yieldingLeader.YieldOrigin-origin,right);
                float offset=Vector3.Dot(yieldingLeader.YieldSpot-yieldingLeader.YieldOrigin,right);
                Vector3 pocket=mergePoint+right*offset-forward*.10f;
                // The followers on this side clear the same flank,
                // leaving an uninterrupted landing for the oncoming group.
                if(CanFollowWalkingSegment(actor,pocket)&&StaticSpaceFree(pocket,actor.transform.parent,.25f)&&
                    SpaceFree(pocket,actor,.53f)) {
                    actor.YieldToFlow(yieldingLeader.YieldingFor,pocket,mergePoint,forward);
                    velocity=(pocket-origin).normalized*.85f;
                    return true;
                }
            }
            if(nearest<0) {
                if(closeLeader&&TightAcross(actor,origin+forward*.95f,right))
                    actor.FollowingNarrowFlow=true;
                return false;
            }
            var approaching=neighbors[nearest];
            Vector3 opposite=origin+approaching.offset;
            bool ownInside=TightAcross(actor,origin,right);
            bool alreadyInside=TryBottleneckCenter(actor,opposite,right,out var center);
            if(!ownInside&&!alreadyInside&&!TryBottleneckCenter(actor,(origin+opposite)*.5f,right,out center))return false;
            actor.FollowingNarrowFlow=true;
            if(ownInside)return false;
            if(!alreadyInside) {
                float otherSpeed=-Vector3.Dot(approaching.velocity,forward);
                // The pedestrian already moving through has priority. Use a
                // stable tie break only when neither has a measurable lead.
                if(ownSpeed>otherSpeed+.20f||
                    ownSpeed>=otherSpeed-.20f&&actor.index<approaching.actor.index)return false;
            }
            Vector3 returnPoint=origin+right*Vector3.Dot(center-origin,right);
            if(!CanFollowWalkingSegment(actor,returnPoint))return false;
            for(int i=0;i<2;i++) {
                Vector3 point=returnPoint+right*(i==0?.78f:-.78f)-forward*.12f;
                if(!CanFollowWalkingSegment(actor,point)||!StaticSpaceFree(point,actor.transform.parent,.25f)||
                    !SpaceFree(point,actor,.53f))continue;
                bool closingPocket=false;
                for(int n=0;n<neighborCount;n++) {
                    var other=neighbors[n];
                    if((origin+other.offset+other.velocity*.55f-point).sqrMagnitude<.52f*.52f) {
                        closingPocket=true;break;
                    }
                }
                if(closingPocket)continue;
                actor.YieldToFlow(approaching.actor,point,returnPoint,forward);
                velocity=(point-origin).normalized*.85f;
                return true;
            }
            return false;
        }
        bool ShouldKeepYielding(Metro3DPassenger actor)
        {
            if(Time.time>=actor.YieldLimit)return false;
            Vector3 forward=actor.YieldFlow,origin=actor.YieldOrigin;
            // The pedestrian already granted passage owns the turn
            // until they clear this merge. Time-to-contact alone is not
            // a reason to reclaim the corridor while they are inside it.
            if(actor.HasYieldingRider) {
                var peer=actor.YieldingFor;
                Vector3 offset=peer.transform.position-origin;
                if(Mathf.Abs(offset.y)<.45f&&peer.transform.parent==actor.transform.parent) {
                    offset.y=0;
                    float ahead=Vector3.Dot(offset,forward);
                    Vector3 goal=peer.CurrentGoal-peer.transform.position;
                    // A follower inherits the turn before the oncoming
                    // head is close to their own pocket. Keep the grant
                    // across the full detection range plus one follower gap.
                    if(ahead>=-.43f&&ahead<5.5f&&(offset-forward*ahead).sqrMagnitude<.68f*.68f&&
                        (ahead<.46f||Vector3.Dot(goal,forward)<-.28f||Vector3.Dot(peer.Velocity,forward)<-.1f))
                        return true;
                }
            }
            // Wait for the occupied stretch, not just the first pedestrian.
            // Otherwise a follower still in the opening meets our return step.
            for(int i=0;i<neighborCount;i++) {
                var sample=neighbors[i];var other=sample.actor;
                if(sample.resting||sample.seated||other.transform.parent!=actor.transform.parent)continue;
                Vector3 offset=other.transform.position-origin;
                if(Mathf.Abs(offset.y)>.45f)continue;
                offset.y=0;
                float ahead=Vector3.Dot(offset,forward);
                if(ahead<-.43f||ahead>3.35f||(offset-forward*ahead).sqrMagnitude>.65f*.65f)continue;
                float toward=-Vector3.Dot(sample.velocity,forward);
                float intended=-Vector3.Dot(other.DesiredVelocity,forward);
                if(toward>.12f&&ahead<=.86f+toward*1.38f||
                    toward>=-.08f&&ahead<1.47f&&intended>.18f)return true;
            }
            return false;
        }
        bool TightAcross(Metro3DPassenger actor,Vector3 point,Vector3 right)
            =>TryBottleneckCenter(actor,point,right,out _);
        bool TryBottleneckCenter(Metro3DPassenger actor,Vector3 point,Vector3 right,out Vector3 center)
        {
            center=point;
            Vector3 chest=point+Vector3.up*.72f;
            float a=Clearance(actor,chest,right,.94f,.06f);
            if(a>=.94f)return false;
            float b=Clearance(actor,chest,-right,.94f,.06f);
            if(a+b>=.94f)return false;
            center=point+right*((a-b)*.5f);return true;
        }
        Vector3 AnticipateStairPass(Metro3DPassenger actor,Vector3 origin,Vector3 fallback)
        {
            Vector3 axis=actor.Journey.StairForward,outward=actor.Journey.StairOutward;
            var root=actor.transform.parent;
            foreach(var other in nearby) {
                if(other==null||other==actor||other.transform.parent!=root||!other.isActiveAndEnabled||
                    Vector3.Dot(other.Journey.StairForward,axis)>-.85f)continue;
                Vector3 offset=other.transform.position-origin;
                if(Mathf.Abs(offset.y)>1.5f)continue;
                float ahead=Vector3.Dot(offset,axis),side=Vector3.Dot(offset,outward);
                if(ahead<-.12f||ahead>3.4f||side<-.56f||side>.14f)continue;
                actor.RememberStairPass();
                break;
            }
            float shift=actor.UpdateStairPassOffset();
            if(shift<=.001f||Vector3.Dot(fallback,axis)<.7f)return fallback;
            Vector3 goal=actor.CurrentGoal-origin;
            float forward=Vector3.Dot(goal,axis);
            if(forward<.38f)return fallback;
            // Open a shoulder-width lane before meeting the oncoming passenger.
            // The actual capsule and railing still decide whether it fits.
            float lateral=Vector3.Dot(goal,outward)+shift;
            Vector3 direction=(axis*Mathf.Min(forward,1.05f)+outward*lateral).normalized;
            if(Clearance(actor,origin+Vector3.up*.72f,direction,.45f)<.45f)return fallback;
            return direction;
        }
        // One bounded candidate check after turn/acceleration constraints. The
        // steering search remains staggered at 10 Hz; this is not another solver.
        public Vector3 ValidateWalkingVelocity(Metro3DPassenger actor,Vector3 candidate)
            =>ValidateWalkingVelocity(actor,candidate,false);

        internal Vector3 ValidateWalkingVelocity(Metro3DPassenger actor,Vector3 candidate,bool contact)
        {
            LastWalkingRejection=Metro3DStopReason.None;
            if(candidate.sqrMagnitude<.000001f)return Vector3.zero;
            long started=System.Diagnostics.Stopwatch.GetTimestamp();WalkingStepCalls++;
            try {
                Vector3 origin=actor.transform.position;
                float speed=candidate.magnitude;
                float lookahead=speed*(Time.fixedDeltaTime+.08f)+speed*speed/(2*3.8f);
                if(Vector3.Dot(candidate,actor.Journey.StairForward)<-.001f)return RejectWalking(Metro3DStopReason.Surface);
                if(Clearance(actor,origin+Vector3.up*.72f,candidate/speed,lookahead)<lookahead)return RejectWalking(Metro3DStopReason.StaticObstacle);
                var frame=actor.transform.parent;
                if(!OnOwnedSurface(actor,frame.InverseTransformPoint(origin),frame.InverseTransformPoint(origin+candidate*.45f)))return RejectWalking(Metro3DStopReason.Surface);
                int claims=world.Traffic.PassageClaims(actor,passageStarts,passageEnds);
                for(int i=0;i<claims;i++) {
                    float before=SegmentDistanceSquared(origin,passageStarts[i],passageEnds[i]);
                    float after=SegmentDistanceSquared(origin+candidate*.55f,passageStarts[i],passageEnds[i]);
                    if(contact&&actor.DoorCourtesyStep&&before<.52f*.52f&&after>before+.00002f) {
                        Vector3 axis=passageEnds[i]-passageStarts[i],offset=origin-passageStarts[i];
                        float along=Mathf.Clamp01(Vector3.Dot(offset,axis)/Mathf.Max(.0001f,axis.sqrMagnitude));
                        if(Vector3.Dot(candidate,offset-axis*along)>=0)continue;
                    }
                    if(after<.52f*.52f&&!(before<.52f*.52f&&after>before+.002f))return RejectWalking(Metro3DStopReason.ReservedPassage);
                }
                foreach(var reservation in standSpaces) {
                    if(reservation.Key==actor||reservation.Key==null||reservation.Value.root==null)continue;
                    Vector3 offset=reservation.Value.root.TransformPoint(reservation.Value.local)-origin;
                    if(Mathf.Abs(offset.y)>1.2f)continue;offset.y=0;
                    if(offset.sqrMagnitude<.55f*.55f&&Vector3.Dot(offset,candidate)<-.015f)continue;
                    if((offset-candidate*.5f).sqrMagnitude<.52f*.52f)return RejectWalking(Metro3DStopReason.ReservedPassage);
                }
                // A doorway's final braking check works at contact range.
                // Steering has already considered the wider group, and the
                // controlled motor still checks other bodies and geometry.
                bool contactDoor=actor.DoorContactContext(out var train,out int door);
                bool closeContact=contact||actor.DoorCrossing||actor.ClearingOwnDoor||actor.DoorCourtesyStep||
                    contactDoor&&actor.ApproachingBoardingDoor(train,door);
                Query(origin,closeContact?1:2,actor.Riding!=null&&actor.Riding.Moving?frame:null);
                Vector3 current=actor.Velocity,estimate=Vector3.Lerp(current,candidate,.7f);
                float radius=actor.WalkingRadius;
                float longestHorizon=closeContact?.65f:1.5f;
                foreach(var other in nearby) {
                    if(!RelativePosition(actor,other,origin,frame,out Vector3 offset))continue;
                    offset.y=0;
                    Vector3 otherVelocity=other.Velocity,relative=estimate-otherVelocity;
                    // The prediction cannot cross this rectangle even with
                    // the longest horizon and the uncompressed capsule.
                    // Skip distant neighbours before doorway and shoulder checks.
                    float reach=radius+Mathf.Max(.10f,other.WalkingRadius)+.001f;
                    if(Mathf.Abs(offset.x)>reach+Mathf.Abs(relative.x)*longestHorizon||
                        Mathf.Abs(offset.z)>reach+Mathf.Abs(relative.z)*longestHorizon)continue;
                    float compression=other.IsSeated?0:DoorCompression(contactDoor?train:null,door,other);
                    bool shoulder=!other.IsSeated&&ShoulderContact(actor,candidate/speed,offset);
                    if(shoulder)compression=Mathf.Max(compression,.028f);
                    float body=radius+(other.IsSeated?.10f:other.WalkingRadius)-compression,bodySquared=body*body;
                    if(contact&&actor.DoorCourtesyStep&&Vector3.Dot(offset,candidate)<-.005f)continue;
                    if(offset.sqrMagnitude<bodySquared&&Vector3.Dot(offset,candidate-otherVelocity)<-.015f)continue;
                    float horizon=contact?.14f:closeContact||shoulder?.27f:1.5f;
                    if(!contact&&actor.FollowingQueueContinuously&&other==actor.QueueLeader)
                        horizon=QueueBrakingHorizon(candidate,otherVelocity);
                    else if(actor.CanYieldToFlow&&actor.PassingSide!=0&&Time.time<actor.PassingUntil&&
                        Vector3.Dot(candidate,otherVelocity)<-.025f)
                        horizon=Mathf.Clamp(.18f+(candidate-otherVelocity).magnitude/(2*3.8f),.24f,.65f);
                    if(ClosestGapSquared(offset,relative,out _,horizon)<bodySquared)return RejectWalking(Metro3DStopReason.Passenger);
                }
                var player=world.player;
                if(player!=null&&Mathf.Abs(player.transform.position.y-origin.y)<1.5f&&
                    !(actor.inTrain&&actor.Riding!=null&&actor.Riding.Moving&&player.Riding!=actor.Riding)) {
                    Vector3 offset=player.transform.position-origin;offset.y=0;
                    if(ClosestGapSquared(offset,estimate-measuredPlayerVelocity,out _)<.48f*.48f)return RejectWalking(Metro3DStopReason.Passenger);
                }
                return candidate;
            } finally {WalkingStepTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-started;}
        }
        static bool ShoulderContact(Metro3DPassenger actor,Vector3 direction,Vector3 offset)
            =>actor.NarrowPoseWeight>.70f&&Vector3.Cross(direction,offset).sqrMagnitude>.28f*.28f;

        static float DoorCompression(Metro3DTrain train,int door,Metro3DPassenger other)
            =>train!=null&&other.AtDoorContact(train,door)?.054f:0;

        const float DoorShoulderContactAllowance=.010f;
        internal const float DoorContactLoadLimit=82f;

        internal bool InDoorBodyContact(Metro3DPassenger actor,Metro3DPassenger other)
        {
            if(!Related(actor,other))return false;
            Vector3 delta=actor.transform.position-other.transform.position;
            if(Mathf.Abs(delta.y)>.22f)return false;
            float reach=actor.DoorContactRadius+other.DoorContactRadius+DoorShoulderContactAllowance;
            return delta.x*delta.x+delta.z*delta.z<=reach*reach;
        }

        internal Vector3 DoorContactForce(Metro3DPassenger actor,Metro3DTrain train,int door,out float effort,
            out Metro3DPassenger strongest,out float peakLoad)
        {
            effort=0;strongest=null;peakLoad=0;
            Vector3 ownDrive=actor.DoorDrive(train),origin=actor.transform.position;
            Query(origin,1);
            Vector3 net=Vector3.zero;
            foreach(var other in nearby) {
                if(!Related(actor,other)||other.IsSeated)continue;
                Vector3 away=origin-other.transform.position;
                if(Mathf.Abs(away.y)>.22f)continue;
                away.y=0;
                float ideal=actor.DoorContactRadius+other.DoorContactRadius;
                float distance=away.sqrMagnitude,reach=ideal+DoorShoulderContactAllowance;
                if(distance<.0001f||distance>reach*reach)continue;
                distance=Mathf.Sqrt(distance);
                Vector3 normal=away/distance;
                // A nearby walker can feel a shoulder even after leaving the
                // boarding action. Register only a genuinely closing body pair.
                if(!other.AtDoorContact(train,door)&&!other.inTrain&&
                    Vector3.Dot(ownDrive,-normal)>.10f&&Vector3.Dot(actor.DesiredVelocity,-normal)>=-.065f)
                    other.NoteDoorContact(train);
                if(!other.AtDoorContact(train,door))continue;
                float transmitted=Mathf.Max(0,Vector3.Dot(other.CabinContactForce,normal))*.55f;
                float drive=Mathf.Max(0,Vector3.Dot(other.DoorDrive(train),normal));
                float relative=Mathf.Max(0,Vector3.Dot(other.Velocity-actor.Velocity,normal));
                float contact=Mathf.InverseLerp(reach,ideal+.004f,distance);
                effort=Mathf.Max(effort,Mathf.Max(0,Vector3.Dot(ownDrive,-normal))*contact);
                // A rider held at contact can still press toward the
                // train. Explicitly moving away must release that pressure.
                float approach=Vector3.Dot(other.DesiredVelocity,normal);
                bool withdrawing=approach<-.065f&&relative<.065f;
                if((drive<.05f||withdrawing)&&transmitted<3)continue;
                float spring=Mathf.Clamp(ideal-distance,0,.055f)*230f;
                float load=Mathf.Min(DoorContactLoadLimit,drive*70+relative*24+transmitted+spring)*contact;
                if(load>peakLoad) {peakLoad=load;strongest=other;}
                net+=normal*load;
            }
            effort=Mathf.Clamp01(effort);
            return Vector3.ClampMagnitude(net,124);
        }

        Vector3 RejectWalking(Metro3DStopReason reason) {LastWalkingRejection=reason;return Vector3.zero;}
        internal bool CanPlanBoardingApproach(Metro3DPassenger actor,Vector3 target)
        {
            Vector3 start=actor.transform.position,delta=target-start;
            if(actor.inTrain||Mathf.Abs(delta.y)>.25f||delta.sqrMagnitude>144)return false;
            var frame=actor.transform.parent;
            if(!OnOwnedSurface(actor,frame.InverseTransformPoint(start),frame.InverseTransformPoint(target)))return false;
            delta.y=0;
            // Planning checks static reachability; active passage reservations
            // still arbitrate actual movement and TryDoor admission separately.
            return delta.sqrMagnitude<.0025f||Clearance(actor,start+Vector3.up*.72f,delta.normalized,delta.magnitude+.04f)>=delta.magnitude;
        }
        public bool CanFollowWalkingSegment(Metro3DPassenger actor,Vector3 target)
            =>CanFollowWalkingSegment(actor,target,false);
        bool CanFollowWalkingSegment(Metro3DPassenger actor,Vector3 target,bool leavingDoorSpace)
        {
            if(!CanApproachDirect(actor,target))return false;
            var frame=actor.transform.parent;Vector3 start=actor.transform.position;
            if(!OnOwnedSurface(actor,frame.InverseTransformPoint(start),frame.InverseTransformPoint(target)))return false;
            int count=world.Traffic.PassageClaims(actor,passageStarts,passageEnds);
            for(int i=0;i<count;i++) {
                Vector3 from=passageStarts[i],to=passageEnds[i];
                if(SegmentGapSquared(start,target,from,to)>=.52f*.52f)continue;
                if(leavingDoorSpace) {
                    Vector3 claim=to-from,offset=start-from;claim.y=offset.y=0;
                    float projection=Mathf.Clamp01(Vector3.Dot(offset,claim)/Mathf.Max(.0001f,claim.sqrMagnitude));
                    Vector3 normal=offset-claim*projection;
                    float before=normal.sqrMagnitude;
                    // Accept a target leaving an occupied corridor only when
                    // the proposed motion starts away from the reservation.
                    if(before<.52f*.52f&&Vector3.Dot(target-start,normal)>=0&&
                        SegmentDistanceSquared(Vector3.Lerp(start,target,.25f),from,to)>before+.0001f)
                        continue;
                }
                return false;
            }
            return true;
        }
        static float SegmentGapSquared(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            Vector3 u=b-a,v=d-c,w=c-a;
            float cross=u.x*v.z-u.z*v.x;
            if(Mathf.Abs(cross)>.00001f) {
                float s=(w.x*v.z-w.z*v.x)/cross,t=(w.x*u.z-w.z*u.x)/cross;
                if(s>=0&&s<=1&&t>=0&&t<=1)return 0;
            }
            return Mathf.Min(Mathf.Min(SegmentDistanceSquared(a,c,d),SegmentDistanceSquared(b,c,d)),Mathf.Min(SegmentDistanceSquared(c,a,b),SegmentDistanceSquared(d,a,b)));
        }
        bool Related(Metro3DPassenger actor,Metro3DPassenger other)
            =>RelativePosition(actor,other,actor.transform.position,actor.transform.parent,out _);
        static bool RelativePosition(Metro3DPassenger actor,Metro3DPassenger other,Vector3 origin,Transform frame,out Vector3 offset)
        {
            offset=Vector3.zero;
            if(other==null||other==actor||!other.isActiveAndEnabled)return false;
            var otherTransform=other.transform;
            offset=otherTransform.position-origin;
            if(Mathf.Abs(offset.y)>1.5f)return false;
            // A moving train and its platform cannot share a pedestrian surface.
            return frame==otherTransform.parent||
                (actor.Riding==null||!actor.Riding.Moving)&&(other.Riding==null||!other.Riding.Moving);
        }
        bool OnOwnedSurface(Metro3DPassenger actor,Vector3 current,Vector3 candidate,bool pastStair=false)
        {
            if(actor.PortalCommitted)return true;
            if(!world.Traffic.GateSurfaceAllowed(actor,current,candidate))return false;
            if(!pastStair&&actor.Journey.OnStairs) {
                if(actor.WalkReviewCandidate&&actor.Journey.ReviewStairActive) {
                    return actor.Journey.ReviewStairSurface(current,candidate);
                }
                // Authored handrail clearance 1.648m minus two .24m body
                // radii leaves +/- .584m; retain .024m edge safety per side.
                float center=-actor.Journey.SurfaceSide*7.76f;
                if(actor.Journey.Layout) center=actor.Journey.Layout.Stair(actor.Journey.SurfaceSide,true).x;
                float oldSide=Mathf.Max(0,Mathf.Abs(current.x-center)-.56f);
                float newSide=Mathf.Max(0,Mathf.Abs(candidate.x-center)-.56f);
                if(newSide>.0001f&&newSide>=oldSide-.002f)return false;
            }
            float before,after;
            if(actor.inTrain) {before=Mathf.Max(0,Mathf.Abs(current.x)-1.13f);after=Mathf.Max(0,Mathf.Abs(candidate.x)-1.13f);}
            else if(candidate.y<2) {
                float limit=Metro3DWorld.ScreenDoorX+.28f;
                before=Mathf.Max(0,limit+actor.Journey.SurfaceSide*current.x);after=Mathf.Max(0,limit+actor.Journey.SurfaceSide*candidate.x);
            } else return true;
            // Normal movement cannot enter the track/leave the aisle. If a
            // moving collider has displaced someone, only motion restoring
            // their own surface is allowed; the physical motor still applies.
            return after<.0001f||after<before-.002f;
        }
        static float SegmentDistanceSquared(Vector3 point,Vector3 a,Vector3 b)
        {
            Vector3 line=b-a,offset=point-a;line.y=offset.y=0;
            float t=Mathf.Clamp01(Vector3.Dot(offset,line)/Mathf.Max(.0001f,line.sqrMagnitude));
            return (offset-line*t).sqrMagnitude;
        }
        internal bool NeedsGripClearance(Metro3DPassenger holder,Metro3DStationTraffic.SupportSpot spot)
        {
            var frame=spot.frame;
            Vector3 origin=frame.TransformPoint(spot.feet);
            Vector3 facing=frame.TransformDirection(spot.Facing),aisle=frame.forward;
            Query(origin,1,frame);
            foreach(var other in nearby) {
                if(!RelativePosition(holder,other,origin,frame,out Vector3 delta)||other.IsSeated||other.IsResting)continue;
                Vector3 motion=other.Velocity.sqrMagnitude>.15f*.15f?other.Velocity:other.DesiredVelocity;
                if(ApproachesBehindGrip(delta,motion,facing,aisle,holder.WalkingRadius+other.WalkingRadius))return true;
            }
            var player=world.player;
            return player!=null&&player.isActiveAndEnabled&&player.Riding==spot.train&&
                ApproachesBehindGrip(player.transform.position-origin,measuredPlayerVelocity,facing,aisle,holder.WalkingRadius+.24f);
        }

        static bool ApproachesBehindGrip(Vector3 delta,Vector3 motion,Vector3 facing,Vector3 aisle,float clearance)
        {
            if(Mathf.Abs(delta.y)>.36f)return false;
            float back=-Vector3.Dot(delta,facing);
            if(back<.07f||back>clearance+.055f)return false;
            float along=Vector3.Dot(delta,aisle),travel=Vector3.Dot(motion,aisle);
            if(Mathf.Abs(travel)<.19f||Mathf.Abs(along)>1.10f)return false;
            if(Mathf.Abs(along)<.30f)return true;
            if(along*travel>=0)return false;
            float time=Mathf.Min(.65f,-along/travel);
            return Mathf.Abs(along+travel*time)<.35f&&back-Vector3.Dot(motion,facing)*time>.02f;
        }

        internal bool TryCabinDestination(Metro3DPassenger actor,Metro3DTrain train,int entrance,
            Vector3 point,bool directOnly,out float cost)
        {
            Vector3 origin=actor.transform.position;
            Transform frame=train.transform;
            Vector3 inward=frame.right*train.side;
            cost=0;
            if(Vector3.Dot(point-origin,inward)<-.30f&&
                Vector3.Dot(origin-train.DoorPoint(entrance,false),inward)<.55f)return false;
            if(!SpaceFree(point,actor,.44f))return false;
            if(directOnly&&(!CanFollowWalkingSegment(actor,point,leavingDoorSpace:true)||
                !DoorPassageFree(actor,train,entrance,origin,point,leaving:true)))return false;
            Vector3 delta=point-origin;delta.y=0;
            Vector3 lead=origin+Vector3.ClampMagnitude(delta,1.45f);
            Query((origin+lead)*.5f,1,frame);
            foreach(var other in nearby) {
                if(other==actor||!other.inTrain||other.Riding!=train||other.IsSeated)continue;
                Vector3 position=other.transform.position;
                if(Mathf.Abs(position.y-point.y)>.42f)continue;
                Vector3 predicted=position+Vector3.ClampMagnitude(other.Velocity*.54f,.68f);
                if((predicted-point).sqrMagnitude<.54f*.54f&&other.Velocity.sqrMagnitude>.17f*.17f)cost+=2.7f;
                if(SegmentDistanceSquared(predicted,origin,lead)<.53f*.53f)cost+=other.WantsToAlight?2.8f:1.15f;
            }
            return cost<(directOnly?2.8f:8f);
        }

        internal bool TryCabinEntryGoal(Metro3DPassenger actor,Metro3DTrain train,int door,out Vector3 goal)
        {
            var frame=train.transform;
            Vector3 feet=actor.transform.position,local=frame.InverseTransformPoint(feet);
            float opening=Metro3DTrain.DoorZ[door],offset=local.z-opening;
            int flank=Mathf.Abs(offset)>.08f?(offset>0?1:-1):(actor.index&1)==0?1:-1;
            goal=feet;
            for(int option=0;option<5;option++) {
                float shift=option==0?0:((option&1)==1?flank:-flank)*(option<3?.42f:.78f);
                Vector3 point=frame.TransformPoint(new Vector3(train.side*.14f,0,local.z+shift));
                if(!CanFollowWalkingSegment(actor,point,leavingDoorSpace:true)||!SpaceFree(point,actor,.49f)||
                    !PassageFree(actor,feet,point,leavingFollowers:true))continue;
                goal=point;return true;
            }
            // Take the part of the forward corridor that is free now.
            // The nearest capsule limits step length, as in the headway check.
            Vector3 forward=frame.right*train.side;
            float advance=Mathf.Min(.68f,.14f-local.x*train.side);
            Query(feet+forward*.24f,1,frame);
            foreach(var other in nearby) {
                if(!RelativePosition(actor,other,feet,frame,out var delta)||other.transform.parent!=frame||other.IsSeated||
                    Mathf.Abs(delta.y)>.42f)continue;
                delta.y=0;
                float ahead=Vector3.Dot(delta,forward);
                if(ahead<=0||ahead>1.35f)continue;
                float radius=actor.WalkingRadius+other.WalkingRadius+.018f;
                float intercept=radius*radius-(delta-forward*ahead).sqrMagnitude;
                if(intercept>0)
                    advance=Mathf.Min(advance,ahead-Mathf.Sqrt(intercept));
            }
            if(advance<.16f)return false;
            Vector3 partial=feet+forward*advance;
            if(!CanFollowWalkingSegment(actor,partial,leavingDoorSpace:true)||!SpaceFree(partial,actor,.47f))return false;
            goal=partial;return true;
        }

        public bool PassageFree(Metro3DPassenger actor,Metro3DTrain train,int door)
        {
            Vector3 inside=train.DoorPoint(door,false),outside=train.DoorPoint(door,true);
            Vector3 entry=actor.inTrain?inside:outside;
            return PassageFree(actor,inside,outside)&&PassageFree(actor,actor.transform.position,entry);
        }
        public bool PassageFree(Metro3DPassenger actor,Vector3 from,Vector3 to)
            =>PassageFree(actor,from,to,false);
        internal bool DoorPassageFree(Metro3DPassenger actor,Metro3DTrain train,int door,Vector3 from,Vector3 to,bool leaving=false)
            =>PassageFree(actor,from,to,leaving,train,door);

        bool PassageFree(Metro3DPassenger actor,Vector3 from,Vector3 to,bool leavingFollowers,
            Metro3DTrain train=null,int door=-1)
        {
            Query((from+to)*.5f,2);
            foreach(var other in nearby) {
                if(!Related(actor,other)||other.IsSeated)continue;
                Vector3 point=other.transform.position;
                Vector3 motion=other.Velocity;motion.y=0;
                Vector3 stop=point+motion*(.1f+motion.magnitude/(2*3.8f));
                if(leavingFollowers&&Vector3.Dot(point-from,to-from)<=0&&Vector3.Dot(stop-from,to-from)<=0)
                    continue;
                // Door admission can use a real shoulder-width gap; the
                // steering and motor still apply their dynamic contact tests.
                float clearance=train!=null?actor.WalkingRadius+other.WalkingRadius+.015f-
                    DoorCompression(train,door,other):.52f;
                if(SegmentDistanceSquared(point,from,to)<clearance*clearance||
                    SegmentDistanceSquared(stop,from,to)<clearance*clearance)return false;
            }
            if(world.player!=null&&Mathf.Abs(world.player.transform.position.y-from.y)<1.2f&&SegmentDistanceSquared(world.player.transform.position,from,to)<.52f*.52f)return false;
            return true;
        }
        static float QueueBrakingHorizon(Vector3 candidate,Vector3 leader)
            =>Mathf.Clamp(.20f+(candidate-leader).magnitude/3.8f,.20f,.65f);

        static float CollisionCost(Vector3 offset,Vector3 current,Vector3 candidate,Vector3 other,float body,float margin,float horizon=1.5f)
        {
            // Include the current achievable motion while the motor accelerates/brakes.
            Vector3 relative=Vector3.Lerp(current,candidate,.7f)-other;
            float squared=ClosestGapSquared(offset,relative,out float t,horizon),comfort=body+margin;
            if(squared>=comfort*comfort)return 0;
            float distance=Mathf.Sqrt(squared);
            return distance<body?42+(body-distance)*100:(comfort-distance)*8*(1-t/2);
        }
        static float ClosestGapSquared(Vector3 offset,Vector3 relative,out float time,float horizon=1.5f)
        {
            time=Mathf.Clamp(Vector3.Dot(offset,relative)/Mathf.Max(.001f,relative.sqrMagnitude),0,horizon);
            return (offset-relative*time).sqrMagnitude;
        }

        internal bool TryNarrowPassage(Metro3DPassenger actor,Vector3 desired,out int shoulder)
        {
            Vector3 feet=actor.transform.position,forward=desired.normalized;
            Vector3 right=Vector3.Cross(Vector3.up,forward),chest=feet+Vector3.up*.72f;
            shoulder=0;
            if(actor.Journey.StairForward.sqrMagnitude>.5f&&Vector3.Dot(forward,actor.Journey.StairForward)<.75f)return false;
            float normal=actor.BodyRadius-.01f,compact=actor.CompactRadius-.01f;
            if(Clearance(actor,chest,forward,.58f,normal)<.58f&&Clearance(actor,chest,forward,.58f,compact)>=.58f) {
                shoulder=Clearance(actor,chest,right,.35f,.10f)<Clearance(actor,chest,-right,.35f,.10f)?1:-1;
                return true;
            }
            Query(feet,1);
            foreach(var other in nearby) {
                if(!Related(actor,other)||other.IsSeated)continue;
                Vector3 offset=other.transform.position-feet;offset.y=0;
                float ahead=Vector3.Dot(offset,forward);
                if(ahead<-.18f||ahead>1.2f)continue;
                float side=Vector3.Dot(offset,right),gap=Mathf.Abs(side);
                bool opposingStair=Vector3.Dot(actor.Journey.StairForward,other.Journey.StairForward)<-.8f;
                float minimum=actor.CompactRadius+(opposingStair?other.CompactRadius:other.WalkingRadius);
                // Only compact for a lateral gap. A person directly in the
                // center of our lane is a leader or a real obstruction.
                // At shoulder range the skin can touch, but a head-on
                // obstruction still has to yield or be followed.
                if(gap<minimum-.018f||gap>actor.BodyRadius+other.BodyRadius+.045f)continue;
                shoulder=side>0?1:-1;return true;
            }
            return false;
        }
        internal bool CanRestoreWalkingRadius(Metro3DPassenger actor)
        {
            Vector3 feet=actor.transform.position;
            int n=Physics.OverlapCapsuleNonAlloc(feet+Vector3.up*.32f,feet+Vector3.up*1.48f,
                actor.BodyRadius,contacts,~0,QueryTriggerInteraction.Ignore);
            if(n==contacts.Length)return false;
            for(int i=0;i<n;i++)if(!contacts[i].transform.IsChildOf(actor.transform))return false;
            return true;
        }
        float Clearance(Metro3DPassenger actor,Vector3 origin,Vector3 direction,float length,float radius=0)
        {
            if(radius<=0)radius=actor.WalkingRadius-.01f;
            int n=Physics.SphereCastNonAlloc(origin,radius,direction,hits,length,~0,QueryTriggerInteraction.Ignore);
            float clear=n==hits.Length?0:float.PositiveInfinity;
            for(int i=0;i<n;i++) {
                var c=hits[i].collider;
                if(c is CharacterController||c.transform.IsChildOf(actor.transform)) continue;
                if(hits[i].distance<=.001f&&(c is BoxCollider||c is SphereCollider||c is CapsuleCollider||(c is MeshCollider m&&m.convex))) {
                    Vector3 away=origin-c.ClosestPoint(origin);
                    if(away.sqrMagnitude>.000001f&&Vector3.Dot(away.normalized,direction)>.05f) continue;
                }
                clear=Mathf.Min(clear,Mathf.Max(0,hits[i].distance));
            }
            return clear;
        }
        public bool CanApproachDirect(Metro3DPassenger actor,Vector3 target)
        {
            Vector3 delta=target-actor.transform.position;
            if(Mathf.Abs(delta.y)>.25f||delta.sqrMagnitude>6.25f) return false;
            delta.y=0;
            return delta.sqrMagnitude<.0025f||Clearance(actor,actor.transform.position+Vector3.up*.72f,delta.normalized,delta.magnitude+.04f)>=delta.magnitude;
        }
        public bool TryBackoff(Metro3DPassenger actor,out Vector3 velocity)
        {
            velocity=Vector3.zero;
            if(actor.QueueCommitted||actor.PortalCommitted) return false;
            // Yield relative to the current walkable leg. The final goal may
            // be on the opposite side of the wall or around the next corner.
            Vector3 forward=actor.DesiredVelocity;forward.y=0;
            if(forward.sqrMagnitude<.01f) return false;
            forward.Normalize();
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            // A short, checked yield/retreat. Never changes the journey or teleports.
            for(int i=0;i<3;i++) {
                if(actor.Journey.OnStairs&&Mathf.Abs(actor.CurrentGoal.y-actor.transform.position.y)>.4f&&i<2)continue;
                Vector3 direction=i==0?right:i==1?-right:-forward;
                Vector3 target=actor.transform.position+direction*.3f;
                Transform root=actor.transform.parent;
                if(!OnOwnedSurface(actor,root.InverseTransformPoint(actor.transform.position),root.InverseTransformPoint(target)))continue;
                if(CanApproachDirect(actor,target)&&SpaceFree(target,actor,.52f)) {velocity=direction*.3f;return true;}
            }
            return false;
        }
        public void RequestPassage(Metro3DPassenger actor)
        {
            if(actor.WaitingForQueue||actor.PortalCommitted)return;
            Vector3 direction=actor.CurrentGoal-actor.transform.position;direction.y=0;
            if(direction.sqrMagnitude<.01f)return;direction.Normalize();
            Query(actor.transform.position,1);Metro3DPassenger blocker=null;float closest=1.6f;
            foreach(var other in nearby) {
                if(!Related(actor,other)||!other.IsResting||other.QueueActive||other.WantsToAlight||other.transform.parent!=actor.transform.parent)continue;
                Vector3 delta=other.transform.position-actor.transform.position;delta.y=0;
                float ahead=Vector3.Dot(delta,direction);
                if(ahead<=.1f||ahead>=closest||(delta-direction*ahead).sqrMagnitude>.48f*.48f)continue;
                blocker=other;closest=ahead;
            }
            // A request to one stationary bystander, not a crowd-wide repulsion
            // force or a learned-help override. Reservations/destination remain.
            if(blocker==null)return;
            if(!actor.inTrain&&!blocker.inTrain&&actor.DoorContactContext(out var train,out int door)&&
                train.DoorsOpen&&!train.Moving&&blocker.CanReceiveDoorYield&&
                blocker.TryYieldDoorLanding(train,door,actor.transform.position,relay:true))return;
            blocker.ClearDoorLanding(actor.transform.position+direction*.6f);
        }
        internal const float ExitLandingDepth=1.30f;
        internal bool OccupiesAlightingLanding(Metro3DPassenger actor,Metro3DTrain train,int door)
        {
            Vector3 inside=train.DoorPoint(door,false),outside=train.DoorPoint(door,true);
            return BlocksLanding(actor,inside,outside+(outside-inside).normalized*ExitLandingDepth,.66f);
        }

        internal Vector3 DoorDepartureTarget(Metro3DPassenger actor,Metro3DTrain train,int door,Vector3 entry,out bool completes)
        {
            Transform station=actor.transform.parent;
            Vector3 origin=actor.transform.position;
            Vector3 outward=-station.right*train.side,lateral=station.forward;
            float depth=Vector3.Dot(origin-entry,outward),offset=Vector3.Dot(origin-entry,lateral);
            float tail=ExitLandingDepth+.22f;
            Query(entry+outward*1.8f,2,station);
            foreach(var other in nearby) {
                if(other==actor||!other.QueuedAtPlatformDoor(train,door))continue;
                Vector3 delta=other.transform.position-entry;
                if(Mathf.Abs(delta.y)>.40f||Mathf.Abs(Vector3.Dot(delta,lateral))>1.85f)continue;
                tail=Mathf.Max(tail,Vector3.Dot(delta,outward)+actor.WalkingRadius+other.WalkingRadius+.13f);
            }
            float outer=Metro3DWorld.ExpandedPlatformOuterX-Mathf.Abs(station.InverseTransformPoint(entry).x)-.72f;
            tail=Mathf.Min(tail,outer);
            if(depth>=tail-.05f) {completes=true;return origin;}
            float ahead=Mathf.Clamp(depth+1.38f,ExitLandingDepth,Mathf.Max(ExitLandingDepth,tail));
            Vector3 best=origin;
            float bestCost=float.PositiveInfinity;
            completes=false;
            for(int i=0;i<7;i++) {
                int side=(i&1)==1?1:-1;
                bool branch=i>=5;
                if(branch&&(depth<.92f||depth>=tail-.18f))continue;
                float width=i==0?Mathf.Clamp(offset,-.36f,.36f):branch?side*1.55f:side*(i<3?.28f:.53f);
                float forward=branch?Mathf.Min(tail,depth+.75f):ahead;
                Vector3 point=entry+outward*forward+lateral*width;
                if((point-origin).sqrMagnitude<.024f||!CanApproachDirect(actor,point)||
                    !FreeOfPlatformLine(actor,train,door,origin,point))continue;
                if(branch&&(!DoorPassageFree(actor,train,door,origin,point,leaving:true)||
                    !FreeOfPlatformLine(actor,train,door,point,point+outward*.65f)||
                    !StaticSpaceFree(point+outward*.65f,station,actor.WalkingRadius)))continue;
                float score=(branch?.53f:0)+Mathf.Abs(offset-width)*(branch?.27f:.65f)+
                    LocalDensity(point,station)*.40f;
                if(!DoorPassageFree(actor,train,door,origin,point,leaving:true))score+=.75f;
                if(score>=bestCost)continue;
                bestCost=score;best=point;completes=branch||forward>=tail-.06f;
            }
            if(best!=origin)return best;
            // Try for the free part of the existing outward lane. A stopped
            // head does not make occupied queue shoulders into exit routes.
            for(int i=0;i<5;i++) {
                float width=i==0?0:(i%2==1?1:-1)*(i<3?.13f:.23f);
                Vector3 step=origin+outward*(i<3?.42f:.30f)+lateral*width;
                if(CanApproachDirect(actor,step)&&FreeOfPlatformLine(actor,train,door,origin,step))return step;
            }
            Vector3 aim=origin+outward*.26f;
            return CanApproachDirect(actor,aim)?aim:origin;
        }

        bool FreeOfPlatformLine(Metro3DPassenger actor,Metro3DTrain train,int door,Vector3 from,Vector3 to)
        {
            Query((from+to)*.5f,1,actor.transform.parent);
            foreach(var other in nearby) {
                if(other==actor||!other.QueuedAtPlatformDoor(train,door))continue;
                Vector3 point=other.transform.position;
                if(Mathf.Abs(point.y-from.y)>.42f)continue;
                point.y=from.y;
                float clearance=actor.WalkingRadius+other.WalkingRadius-.028f;
                float before=(point-from).sqrMagnitude;
                if(before<clearance*clearance&&Vector3.Dot(point-from,to-from)<0)continue;
                if(SegmentDistanceSquared(point,from,to)<clearance*clearance)return false;
            }
            return true;
        }

        internal bool TryCloseDoorYield(Metro3DPassenger actor,Metro3DTrain train,int door,Vector3 approachingFrom,out Vector3 goal,bool relay=false)
        {
            Vector3 origin=actor.transform.position,outward=-train.transform.right*train.side;
            Vector3 lateral=train.transform.forward;
            Vector3 entry=train.DoorPoint(door,!actor.inTrain);
            // Clear the approaching passenger's lane, which may be offset
            // inside the opening, instead of the doorway's center line.
            entry+=lateral*Vector3.Dot(approachingFrom-entry,lateral);
            float clearingDepth=actor.inTrain?.55f:Mathf.Max(ExitLandingDepth,Vector3.Dot(approachingFrom-entry,outward)+.7f);
            Vector3 edge=entry+outward*clearingDepth;
            Vector3 start=approachingFrom;
            float before=SegmentDistanceSquared(origin,start,edge);
            int preferred=Vector3.Dot(origin-entry,lateral)>=0?1:-1;
            Vector3 back=actor.inTrain?-outward:outward;
            goal=origin;
            float best=float.NegativeInfinity;
            // A side already occupied by a planned step is scored at its
            // landing position as well as at the current capsule position.
            for(int i=0;i<6;i++) {
                float side=i==3?0:i==5?-preferred:preferred;
                float depth=i==0?.08f:i==1?.19f:i==2?.32f:i==3?.35f:i==4?.18f:.18f;
                float width=i==0?.34f:i==1?.27f:i==2?.22f:i==3?0:i==4?.50f:.25f;
                if(relay) {depth=Mathf.Clamp(depth,.18f,.32f);width=Mathf.Min(width,.31f);}
                Vector3 point=origin+back*depth+lateral*(side*width);
                float opening=SegmentDistanceSquared(point,start,edge)-before;
                if(opening<.009f||actor.inTrain&&!train.Contains(point)||!CanApproachDirect(actor,point))continue;
                if(relay&&!CanFollowWalkingSegment(actor,point,leavingDoorSpace:true))continue;
                if(world.player!=null&&(world.player.transform.position-point).sqrMagnitude<.49f*.49f)continue;
                Query(point,1,actor.transform.parent);
                float cost=0;
                bool claimed=false;
                foreach(var other in nearby) {
                    if(other==actor||other==null)continue;
                    Vector3 feet=other.transform.position;
                    if(Mathf.Abs(feet.y-point.y)>.55f)continue;
                    float reach=Mathf.Max(.31f,actor.WalkingRadius+other.WalkingRadius-.042f);
                    if(other.TryGetDoorYieldGoal(train,door,out var reserved)) {
                        float clearance=SegmentDistanceSquared(reserved,origin,point);
                        if((reserved-point).sqrMagnitude<reach*reach||
                            (reserved-origin).sqrMagnitude>reach*reach&&clearance<reach*reach*.62f) {
                            claimed=true;break;
                        }
                    }
                    Vector3 predicted=feet+Vector3.ClampMagnitude(other.Velocity*.23f,.19f);
                    float gap=Mathf.Min((feet-point).sqrMagnitude,(predicted-point).sqrMagnitude);
                    if(gap<reach*reach)cost+=(reach-Mathf.Sqrt(gap))*2.2f;
                }
                if(claimed)continue;
                float score=Mathf.Min(.27f,opening)-cost-(i==5?.10f:0)-.14f*(point-origin).magnitude;
                if(relay)score+=depth*.36f;
                if(score<=best)continue;
                best=score;goal=point;
            }
            return best>float.NegativeInfinity;
        }

        internal bool HasPlatformLeader(Metro3DPassenger actor,Metro3DTrain train,int door,Vector3 towardHead)
        {
            Vector3 origin=actor.transform.position;
            Query(origin,1,actor.transform.parent);
            foreach(var other in nearby) {
                if(other==actor||!other.QueuedAtPlatformDoor(train,door)||other.Velocity.sqrMagnitude>.30f*.30f)continue;
                Vector3 delta=other.transform.position-origin;
                if(Mathf.Abs(delta.y)>.28f)continue;
                delta.y=0;
                float ahead=Vector3.Dot(delta,towardHead);
                if(ahead<.35f||ahead>1.24f||(delta-towardHead*ahead).sqrMagnitude>.43f*.43f)continue;
                return true;
            }
            return false;
        }

        internal bool TryRelayDoorYield(Metro3DPassenger front,Metro3DTrain train,int door,Vector3 outward,bool contactOnly)
        {
            Vector3 origin=front.transform.position;
            Query(origin,1,front.transform.parent);
            Metro3DPassenger rear=null;
            float nearest=.78f;
            foreach(var other in nearby) {
                if(other==front||other==null||!other.CanReceiveDoorYield||other.inTrain)continue;
                Vector3 offset=other.transform.position-origin;
                if(Mathf.Abs(offset.y)>.25f)continue;
                offset.y=0;
                float depth=Vector3.Dot(offset,outward);
                if(depth<.19f||depth>=nearest||(offset-outward*depth).sqrMagnitude>.31f*.31f)continue;
                rear=other;nearest=depth;
            }
            if(rear==null||contactOnly&&!InDoorBodyContact(front,rear)||
                !CanPerceive(rear,origin+Vector3.up*(1.42f*front.HeightRatio),front.transform,true))
                return false;
            float step=Mathf.Lerp(.22f,.15f,Mathf.InverseLerp(.42f,.76f,nearest));
            Vector3 feet=rear.transform.position;
            Vector3 lateral=train.transform.forward;
            float side=Vector3.Dot(feet-train.DoorPoint(door,true),lateral);
            // Follow the nearest body through a short diagonal when its
            // shoulder has room. The final motor still validates the step.
            Vector3 diagonal=feet+outward*step+lateral*(side<0?-.10f:.10f);
            if(Mathf.Abs(side)<.94f&&CanFollowWalkingSegment(rear,diagonal,leavingDoorSpace:true)&&
                SpaceFree(diagonal,rear,.40f)&&rear.TryFollowDoorYield(train,door,diagonal))return true;
            return rear.TryFollowDoorYield(train,door,feet+outward*step);
        }

        static bool BlocksLanding(Metro3DPassenger actor,Vector3 from,Vector3 to,float width=.52f)
        {
            Vector3 feet=actor.transform.position,motion=actor.Velocity;
            if(Mathf.Abs(feet.y-from.y)>.7f)return false;
            motion.y=0;
            Vector3 stop=feet+motion*(.1f+motion.magnitude/(2*3.8f));
            return SegmentDistanceSquared(feet,from,to)<width*width||SegmentDistanceSquared(stop,from,to)<width*width;
        }

        internal bool TryClearCabinApproach(Metro3DPassenger actor,Metro3DTrain train,int door,Vector3 from,out Vector3 step)
        {
            Vector3 feet=actor.transform.position,entry=train.DoorPoint(door,false);
            Vector3 inward=train.transform.right*train.side,lateral=train.transform.forward;
            Vector3 opening=train.DoorPoint(door,true);
            float current=Mathf.Min(SegmentDistanceSquared(feet,from,entry),SegmentDistanceSquared(feet,entry,opening));
            int side=Vector3.Dot(feet-entry,lateral)<0?-1:1;
            for(int i=0;i<5;i++) {
                float along=i==0?0:(i%2==1?side:-side)*(i<3?.47f:.72f);
                Vector3 point=feet+inward*(i>=3?.14f:.36f)+lateral*along;
                float next=Mathf.Min(SegmentDistanceSquared(point,from,entry),SegmentDistanceSquared(point,entry,opening));
                if(next<current+.08f||!train.Contains(point)||!CanFollowWalkingSegment(actor,point,leavingDoorSpace:true)||
                    !SpaceFree(point,actor,.47f))continue;
                step=point;return true;
            }
            step=feet;return false;
        }

        public void ClearLanding(Metro3DTrain train,int door,Metro3DPassenger crossing,int lane=0)
        {
            if(!train.DoorsOpen||train.Moving)return;
            Vector3 inside=train.DoorPoint(door,false,lane),outside=train.DoorPoint(door,true,lane);
            Vector3 from=crossing.transform.position;
            bool discharging=crossing.DischargingThrough(train,door);
            bool alighting=crossing.TravelsOutward||crossing.ClearingDoor(train,door)||discharging;
            if(alighting&&!discharging&&!crossing.DoorCrossing&&!crossing.ClearingDoor(train,door)&&
                (!world.Traffic.IsAlightingHead(train,door,crossing)||(from-inside).sqrMagnitude>1.45f*1.45f))return;
            if(landingFrame!=Time.frameCount) {
                landingFrame=Time.frameCount;
                landingChecked.Clear();
            }
            // All riders using the same entrance share one clearance request
            // for this frame. Alighting approach still checks its own aisle.
            var check=(train,door,lane,alighting);
            if(!alighting&&landingChecked.ContainsKey(check))return;
            if(!alighting)landingChecked[check]=landingFrame;
            Query(discharging?from:inside,discharging?1:2,discharging?crossing.transform.parent:null);
            int count=0;
            Metro3DPassenger platformLead=null;
            float platformGap=float.PositiveInfinity;
            foreach(var other in nearby) {
                if(count>=landing.Length||other==null||other==crossing||other.IsSeated||other.WantsToAlight||other.PortalCommitted)continue;
                bool onTrain=other.inTrain&&other.Riding==train;
                bool onPlatform=alighting&&!other.inTrain&&other.stationIndex==train.CurrentStop&&
                    other.transform.parent==world.stations[train.CurrentStop];
                if(!onTrain&&!onPlatform)continue;
                if(onPlatform) {
                    if(other.ExitingPlatformQueue||other.JustLeftDoor(train,door)||!other.CanReceiveDoorYield)continue;
                    Vector3 outward=-train.transform.right*train.side;
                    Vector3 start=discharging?from:inside;
                    Vector3 end=discharging?from+outward*.80f:outside+(outside-inside).normalized*ExitLandingDepth;
                    if(!BlocksLanding(other,start,end,crossing.WalkingRadius+other.WalkingRadius-.02f))continue;
                    float distance=(other.transform.position-from).sqrMagnitude;
                    if(distance>=platformGap||distance>2.35f*2.35f)continue;
                    platformLead=other;platformGap=distance;
                } else if(onTrain&&(BlocksLanding(other,inside-(outside-inside).normalized*.45f,outside)||
                    alighting&&BlocksLanding(other,from,inside)))landing[count++]=other;
            }
            if(platformLead!=null&&CanPerceive(platformLead,from+Vector3.up*(1.42f*crossing.HeightRatio),crossing.transform,true))
                platformLead.TryYieldDoorLanding(train,door,from,relay:true);
            for(int i=0;i<count;i++) {
                var other=landing[i];landing[i]=null;
                if(!other.inTrain) {
                    other.NoteDoorContact(train);
                }
                if((alighting||other.inTrain)&&other.TryYieldDoorLanding(train,door,from))continue;
                other.ClearDoorLanding(other.inTrain?inside:outside,true,alighting?door:-1,from);
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // Shared spatial perception and bounded path queue, not per-agent full crowd traversal.
    [DefaultExecutionOrder(-50)]
    public sealed class Metro3DCrowd : MonoBehaviour
    {
        public Metro3DWorld world;
        public float LastSteeringCost {get;private set;}
        public int LastStaticRejects {get;private set;}
        public long SteeringTicks {get;private set;}
        public int SteeringCalls {get;private set;}
        public int PathRequests {get;private set;}
        public int PathSolves {get;private set;}
        public long PathTicks {get;private set;}
        readonly List<Metro3DPassenger> agents=new List<Metro3DPassenger>();
        readonly Dictionary<(Transform,Vector3Int),List<Metro3DPassenger>> cells=new Dictionary<(Transform,Vector3Int),List<Metro3DPassenger>>();
        readonly List<Transform> scopes=new List<Transform>(8);
        readonly Dictionary<Transform,Bounds> scopeBounds=new Dictionary<Transform,Bounds>();
        struct NeighborSample {public Metro3DPassenger actor;public Vector3 offset,velocity;public bool seated,queued,resting;}
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
        readonly List<Metro3DPassenger> nearby=new List<Metro3DPassenger>(32);
        readonly RaycastHit[] hits=new RaycastHit[24];
        readonly Metro3DPassenger[] landing=new Metro3DPassenger[32];
        struct StandClaim {public Transform root;public Vector3 local;}
        readonly Dictionary<Metro3DPassenger,StandClaim> standSpaces=new Dictionary<Metro3DPassenger,StandClaim>();
        readonly Collider[] contacts=new Collider[24];
        Metro3DNavigation navigation;
        float nextHash;
        static readonly Unity.Profiling.ProfilerMarker SteeringMarker=new Unity.Profiling.ProfilerMarker("SubwayCarry.Crowd.Steering");
        static readonly float[] Angles={0,25,50,75,-25,-50,-75,90,-90,135,-135,180};
        static readonly float[] Speeds={1,.65f,.3f,0};
        Vector3Int Cell(Vector3 p)=>new Vector3Int(Mathf.FloorToInt(p.x/1.6f),Mathf.FloorToInt(p.y/2),Mathf.FloorToInt(p.z/1.6f));
        public void Register(Metro3DPassenger p) { if(!agents.Contains(p)) {agents.Add(p);hashDirty=true; if(buckets.Count<agents.Count) buckets.Add(new List<Metro3DPassenger>(8));} }
        public void Unregister(Metro3DPassenger p) {agents.Remove(p);hashDirty=true; queued.Remove(p);standSpaces.Remove(p);}
        public void Request(Metro3DPassenger p) { if(queued.Add(p)) {paths.Enqueue(p);PathRequests++;} }
        void Update()
        {
            if(world==null) return;
            SamplePlayerMotion();
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
        public void InvalidateSpatial() {hashDirty=true;}
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
        void Query(Vector3 point,int radius=1)
        {
            if(hashDirty) RebuildHash();
            nearby.Clear();
            // Hash in each stable pedestrian frame. The train can move between
            // perception ticks without leaving its passengers in obsolete world cells.
            foreach(var root in scopes) if(root!=null) {
                Vector3 local=root.InverseTransformPoint(point);var bounds=scopeBounds[root];
                Vector3 min=bounds.min,max=bounds.max;float reach=(radius+1)*1.6f;
                if(local.x<min.x-reach||local.x>max.x+reach||local.z<min.z-reach||local.z>max.z+reach||local.y<min.y-1.5f||local.y>max.y+1.5f) continue;
                var k=Cell(local);
                int lo=Mathf.Max(Mathf.FloorToInt(min.y/2),Mathf.FloorToInt((local.y-1.5f)/2));
                int hi=Mathf.Min(Mathf.FloorToInt(max.y/2),Mathf.FloorToInt((local.y+1.5f)/2));
                for(int y=lo;y<=hi;y++) for(int x=-radius;x<=radius;x++) for(int z=-radius;z<=radius;z++)
                    if(cells.TryGetValue((root,new Vector3Int(k.x+x,y,k.z+z)),out var list)) nearby.AddRange(list);
            }
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
        public bool AlightersAt(Metro3DTrain train,int door)
        {
            Vector3 p=train.DoorPoint(door,false); Query(p,3);
            foreach(var a in nearby) if(a!=null&&(a.WantsToAlight&&(a.AlightingDoor>=0?a.AlightingDoor:train.NearestDoor(a.transform.position))==door||a.ClearingDoor(train,door))&&(a.transform.position-p).sqrMagnitude<25) return true;
            return false;
        }
        public float LocalDensity(Vector3 point,Transform root)
        {
            Query(point);int count=0;
            foreach(var p in nearby)if(p!=null&&p.transform.parent==root&&(p.transform.position-point).sqrMagnitude<4)count++;
            return Mathf.Clamp01(count/8f);
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
            foreach(var other in nearby) if(count<landing.Length&&other!=null&&other!=actor&&other.inTrain&&other.Riding==actor.Riding&&!other.WantsToAlight&&(other.transform.position-point).sqrMagnitude<.75f*.75f) landing[count++]=other;
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
            Query(actor.transform.position,2);
            Vector3 origin=actor.transform.position,forward=desired.normalized;
            float requested=desired.magnitude,density=0;
            neighborCount=0;
            foreach(var other in nearby) {
                if(!Related(actor,other)) continue;
                Vector3 offset=other.transform.position-origin;offset.y=0;
                if(offset.sqrMagnitude<4) density++;
                var sample=new NeighborSample{actor=other,offset=offset,velocity=other.Velocity,seated=other.IsSeated,queued=other.QueueActive,resting=other.IsResting};
                if(neighborCount<neighbors.Length) neighbors[neighborCount++]=sample;
                else {
                    int farthest=0;
                    for(int i=1;i<neighborCount;i++)if(neighbors[i].offset.sqrMagnitude>neighbors[farthest].offset.sqrMagnitude)farthest=i;
                    if(offset.sqrMagnitude<neighbors[farthest].offset.sqrMagnitude)neighbors[farthest]=sample;
                }
            }
            density=Mathf.Clamp01(density/8);
            float personal=Mathf.Lerp(Mathf.Clamp(actor.Journey.Personality.PersonalSpace,.08f,.45f),.06f,density);
            // Authored bidirectional queue lanes are .56m apart. Compress only
            // their comfort margin, leaving the .48m body and motor unchanged.
            if(actor.inTrain&&actor.QueueActive)personal=Mathf.Min(personal,.06f);
            if(actor.PortalCommitted) personal=.03f;
            float preferred=requested,leaderGap=float.PositiveInfinity;
            bool laneLocked=actor.PortalCommitted||actor.QueueCommitted;
            LastStaticRejects=0;
            // Following is a speed constraint before it is a reason to overtake.
            // The queue owner, not this movement layer, grants passage rights.
            for(int neighbor=0;neighbor<neighborCount;neighbor++) {
                var other=neighbors[neighbor];Vector3 offset=other.offset;
                float ahead=Vector3.Dot(offset,forward);
                float lateral=(offset-forward*ahead).magnitude;
                if(ahead<=0||ahead>3||lateral>.43f) continue;
                if(actor.PortalCommitted&&other.queued) continue;
                // A seat occupant or someone waiting in an adjacent facility
                // is an obstacle to pass, not a leader of this walking flow.
                if(other.seated||other.resting||other.queued&&!actor.QueueCommitted) continue;
                if(!actor.QueueCommitted&&other.velocity.sqrMagnitude<.01f)continue;
                float alignment=other.velocity.sqrMagnitude>.01f?Vector3.Dot(other.velocity.normalized,forward):1;
                if(alignment<.45f) continue;
                float gap=actor.QueueCommitted?actor.QueueGap:
                    .48f+personal+requested*Mathf.Clamp(actor.Journey.Personality.ReactionDelay,.12f,.5f);
                float cap=Mathf.Max(0,Vector3.Dot(other.velocity,forward)+(ahead-gap)/.7f);
                if(ahead<leaderGap) {leaderGap=ahead;preferred=Mathf.Min(preferred,cap);}
            }
            if(!actor.PortalCommitted&&actor.QueueCommitted&&actor.QueueLeader!=null) {
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
            foreach(float angle in Angles) {
                if(laneLocked&&Mathf.Abs(angle)>.1f) continue;
                if(Mathf.Abs(angle)>100&&!actor.RecoveryActive) continue;
                Vector3 direction=Quaternion.Euler(0,angle,0)*forward;
                float lookahead=Mathf.Min(Mathf.Max(.25f,requested*.6f),actor.SteeringDistance+.04f);
                float clear=Clearance(actor,origin+Vector3.up*.72f,direction,lookahead);
                foreach(float scale in Speeds) {
                    if(scale==0&&angle!=0) continue;
                    // Lane followers do not turn a slow leader into an overtaking race.
                    float available=laneLocked||Mathf.Abs(angle)<.1f?preferred:requested;
                    float speed=available*scale;
                    if(speed>.01f&&speed*.45f+.06f>clear) {LastStaticRejects++;continue;}
                    Vector3 candidate=direction*speed;
                    float cost=(requested-speed)/Mathf.Max(.2f,requested)*1.15f+Mathf.Abs(angle)*.013f;
                    for(int claim=0;claim<passageCount;claim++) {
                        float before=SegmentDistanceSquared(origin,passageStarts[claim],passageEnds[claim]);
                        float after=SegmentDistanceSquared(origin+candidate*.55f,passageStarts[claim],passageEnds[claim]);
                        if(before<.52f*.52f&&after>before+.002f)continue;
                        if(after<.52f*.52f)cost+=45+(.52f-Mathf.Sqrt(after))*100;
                    }
                    if(angle<0) cost+=.08f;
                    if(actor.PassingSide!=0&&Time.time<actor.PassingUntil&&Mathf.Abs(angle)>.1f&&Mathf.Sign(angle)!=actor.PassingSide) cost+=1.25f;
                    cost+=(candidate-actor.Velocity).sqrMagnitude*.32f;
                    Vector3 predicted=worldToFrame.MultiplyPoint3x4(origin+candidate*.45f);
                    if(!OnOwnedSurface(actor,worldToFrame.MultiplyPoint3x4(origin),predicted)) continue;
                    foreach(var reservation in standSpaces) {
                        if(reservation.Key==actor||reservation.Key==null||reservation.Value.root==null||Mathf.Abs(reservation.Value.root.TransformPoint(reservation.Value.local).y-origin.y)>1.2f) continue;
                        Vector3 offset=reservation.Value.root.TransformPoint(reservation.Value.local)-origin;offset.y=0;
                        if(offset.sqrMagnitude<.55f*.55f&&Vector3.Dot(offset,candidate)<-.015f) continue;
                        float distance=(offset-candidate*.5f).magnitude;
                        if(distance<.52f) cost+=45+(.52f-distance)*100;
                    }
                    for(int neighbor=0;neighbor<neighborCount;neighbor++) {
                        var other=neighbors[neighbor];Vector3 offset=other.offset;
                        // Preserve an escape from existing light contact without adding repulsion forces.
                        if(offset.sqrMagnitude<.48f*.48f&&Vector3.Dot(offset,candidate-other.velocity)<-.015f) continue;
                        cost+=CollisionCost(offset,actor.Velocity,candidate,other.velocity,other.seated?.34f:.48f,personal);
                    }
                    if(checkPlayer) cost+=CollisionCost(playerOffset,actor.Velocity,candidate,measuredPlayerVelocity,.48f,personal+.06f);
                    if(cost<best) {best=cost;selected=candidate;selectedSide=Mathf.Abs(angle)>.1f?(angle>0?1:-1):0;}
                }
            }
            LastSteeringCost=best;
            if(best>=35||float.IsInfinity(best)) return Vector3.zero;
            if(selectedSide!=0&&selected.sqrMagnitude>.02f) actor.CommitPassingSide(selectedSide);
            return selected;
        }
        bool Related(Metro3DPassenger actor,Metro3DPassenger other)
        {
            if(other==null||other==actor||!other.isActiveAndEnabled||Mathf.Abs(other.transform.position.y-actor.transform.position.y)>1.5f) return false;
            // A train passing a platform is not a shared pedestrian surface.
            if(actor.transform.parent!=other.transform.parent&&
               ((actor.Riding!=null&&actor.Riding.Moving)||(other.Riding!=null&&other.Riding.Moving))) return false;
            return true;
        }
        bool OnOwnedSurface(Metro3DPassenger actor,Vector3 current,Vector3 candidate)
        {
            if(actor.PortalCommitted)return true;
            if(!world.Traffic.GateSurfaceAllowed(actor,current,candidate))return false;
            if(actor.Journey.OnStairs) {
                // Authored handrail clearance 1.648m minus two .24m body
                // radii leaves +/- .584m; retain .024m edge safety per side.
                float center=-world.ServiceSide*7.76f;
                float oldSide=Mathf.Max(0,Mathf.Abs(current.x-center)-.56f);
                float newSide=Mathf.Max(0,Mathf.Abs(candidate.x-center)-.56f);
                if(newSide>.0001f&&newSide>=oldSide-.002f)return false;
            }
            float before,after;
            if(actor.inTrain) {before=Mathf.Max(0,Mathf.Abs(current.x)-1.13f);after=Mathf.Max(0,Mathf.Abs(candidate.x)-1.13f);}
            else if(candidate.y<2) {
                float limit=Metro3DWorld.ScreenDoorX+.28f;
                before=Mathf.Max(0,limit+world.ServiceSide*current.x);after=Mathf.Max(0,limit+world.ServiceSide*candidate.x);
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
        public bool PassageFree(Metro3DPassenger actor,Metro3DTrain train,int door)
        {
            Vector3 inside=train.DoorPoint(door,false),outside=train.DoorPoint(door,true);
            Vector3 entry=actor.inTrain?inside:outside;
            return PassageFree(actor,inside,outside)&&PassageFree(actor,actor.transform.position,entry);
        }
        public bool PassageFree(Metro3DPassenger actor,Vector3 from,Vector3 to)
        {
            Query((from+to)*.5f,2);
            foreach(var other in nearby) {
                if(!Related(actor,other)||other.IsSeated)continue;
                Vector3 point=other.transform.position;
                Vector3 motion=other.Velocity;motion.y=0;
                Vector3 stop=point+motion*(.1f+motion.magnitude/(2*3.8f));
                if(SegmentDistanceSquared(point,from,to)<.52f*.52f||SegmentDistanceSquared(stop,from,to)<.52f*.52f)return false;
            }
            if(world.player!=null&&Mathf.Abs(world.player.transform.position.y-from.y)<1.2f&&SegmentDistanceSquared(world.player.transform.position,from,to)<.52f*.52f)return false;
            return true;
        }
        static float CollisionCost(Vector3 offset,Vector3 current,Vector3 candidate,Vector3 other,float body,float margin)
        {
            // Include the current achievable motion while the motor accelerates/brakes.
            Vector3 relative=Vector3.Lerp(current,candidate,.7f)-other;
            float t=Mathf.Clamp(Vector3.Dot(offset,relative)/Mathf.Max(.001f,relative.sqrMagnitude),0,1.5f);
            float distance=(offset-relative*t).magnitude;
            if(distance<body) return 42+(body-distance)*100;
            float comfort=body+margin;
            return distance<comfort?(comfort-distance)*8*(1-t/2):0;
        }
        float Clearance(Metro3DPassenger actor,Vector3 origin,Vector3 direction,float length)
        {
            int n=Physics.SphereCastNonAlloc(origin,.23f,direction,hits,length,~0,QueryTriggerInteraction.Ignore);
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
            Vector3 forward=actor.CurrentGoal-actor.transform.position;forward.y=0;
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
            if(blocker!=null)blocker.ClearDoorLanding(actor.transform.position+direction*.6f);
        }
        public void ClearLanding(Metro3DTrain train,int door,Metro3DPassenger crossing)
        {
            Vector3 point=train.DoorPoint(door,false),outside=train.DoorPoint(door,true);Query(point,2);
            int count=0;
            foreach(var other in nearby) {
                if(count>=landing.Length||other==null||other==crossing||other.WantsToAlight)continue;
                bool inside=other.inTrain&&other.Riding==train&&(other.transform.position-point).sqrMagnitude<1.3f*1.3f;
                bool platform=crossing.IsAlighting&&!other.inTrain&&other.stationIndex==train.CurrentStop&&other.Journey.CanBoard&&(other.transform.position-outside).sqrMagnitude<.65f*.65f;
                if(inside||platform)landing[count++]=other;
            }
            // Clearing a landing queries standing-space perception, so do not mutate Query's buffer while enumerating it.
            for(int i=0;i<count;i++) {landing[i].ClearDoorLanding(point);landing[i]=null;}
        }
    }
}

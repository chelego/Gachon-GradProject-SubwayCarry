using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // Shared spatial perception and bounded path queue, not per-agent full crowd traversal.
    [DefaultExecutionOrder(-50)]
    public sealed class Metro3DCrowd : MonoBehaviour
    {
        public Metro3DWorld world;
        readonly List<Metro3DPassenger> agents=new List<Metro3DPassenger>();
        readonly Dictionary<Vector3Int,List<Metro3DPassenger>> cells=new Dictionary<Vector3Int,List<Metro3DPassenger>>();
        readonly List<List<Metro3DPassenger>> buckets=new List<List<Metro3DPassenger>>();
        readonly Queue<Metro3DPassenger> paths=new Queue<Metro3DPassenger>();
        readonly HashSet<Metro3DPassenger> queued=new HashSet<Metro3DPassenger>();
        readonly List<Metro3DPassenger> nearby=new List<Metro3DPassenger>(32);
        readonly RaycastHit[] hits=new RaycastHit[24];
        Metro3DNavigation navigation;
        float nextHash;
        static readonly float[] Angles={0,22,-22,45,-45,70,-70};
        Vector3Int Cell(Vector3 p)=>new Vector3Int(Mathf.FloorToInt(p.x/1.6f),Mathf.FloorToInt(p.y/2),Mathf.FloorToInt(p.z/1.6f));
        public void Register(Metro3DPassenger p) { if(!agents.Contains(p)) {agents.Add(p); if(buckets.Count<agents.Count) buckets.Add(new List<Metro3DPassenger>(8));} }
        public void Unregister(Metro3DPassenger p) {agents.Remove(p); queued.Remove(p);}
        public void Request(Metro3DPassenger p) { if(queued.Add(p)) paths.Enqueue(p); }
        void Update()
        {
            if(world==null) return;
            if(navigation==null) navigation=new Metro3DNavigation();
            if(Time.time>=nextHash) {
                nextHash=Time.time+.1f;
                foreach(var list in buckets) list.Clear(); cells.Clear(); int bucket=0;
                foreach(var p in agents) if(p!=null&&p.isActiveAndEnabled) {
                    var key=Cell(p.transform.position);
                    if(!cells.TryGetValue(key,out var list)) {list=buckets[bucket++];cells.Add(key,list);} list.Add(p);
                }
            }
            for(int i=0;i<2&&paths.Count>0;i++) {var p=paths.Dequeue(); queued.Remove(p); if(p!=null) p.SolvePath(navigation);}
        }
        void Query(Vector3 point,int radius=1)
        {
            nearby.Clear(); var k=Cell(point);
            for(int x=-radius;x<=radius;x++) for(int z=-radius;z<=radius;z++) if(cells.TryGetValue(k+new Vector3Int(x,0,z),out var list)) nearby.AddRange(list);
        }
        public bool AlightersAt(Metro3DTrain train,int door)
        {
            Vector3 p=train.DoorPoint(door,false); Query(p,3);
            foreach(var a in nearby) if(a!=null&&a.WantsToAlight&&train.NearestDoor(a.transform.position)==door&&(a.transform.position-p).sqrMagnitude<25) return true;
            return false;
        }
        public Vector3 SafeVelocity(Metro3DPassenger actor,Vector3 desired)
        {
            Query(actor.transform.position); float best=float.PositiveInfinity; Vector3 selected=Vector3.zero;
            foreach(float angle in Angles) {
                Vector3 v=Quaternion.Euler(0,angle,0)*desired;
                if(Obstructed(actor,actor.transform.position+Vector3.up*.72f,v.normalized,Mathf.Max(.48f,v.magnitude*.5f))) continue;
                float cost=Mathf.Abs(angle)*.009f+(angle<0?.045f:0);
                if(actor.Velocity.sqrMagnitude>.02f) cost+=(1-Vector3.Dot(v.normalized,actor.Velocity.normalized))*.9f;
                foreach(var other in nearby) {
                    if(other==null||other==actor) continue;
                    Vector3 relative=other.transform.position-actor.transform.position; relative.y=0;
                    Vector3 rv=v-other.Velocity;
                    float t=Mathf.Clamp(Vector3.Dot(relative,rv)/Mathf.Max(.001f,rv.sqrMagnitude),0,.85f);
                    float d=(relative-rv*t).magnitude;
                    if(d<.48f) cost+=25+(.48f-d)*70; else if(d<.78f) cost+=(.78f-d)*7;
                }
                Vector3 player=world.player.transform.position-actor.transform.position; player.y=0;
                if(Mathf.Abs(world.player.transform.position.y-actor.transform.position.y)<1) {
                    float d=(player-v*.45f).magnitude; if(d<.66f) cost+=20+(.66f-d)*40;
                }
                if(cost<best) {best=cost; selected=v;}
            }
            if(best>18) return Vector3.zero;
            return selected*Mathf.Clamp01(1-best*.08f);
        }
        bool Obstructed(Metro3DPassenger p,Vector3 origin,Vector3 direction,float length)
        {
            int n=Physics.SphereCastNonAlloc(origin,.23f,direction,hits,length,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++) {var c=hits[i].collider; if(c is CharacterController||c.transform.IsChildOf(p.transform)) continue; return true;}
            return false;
        }
    }
}

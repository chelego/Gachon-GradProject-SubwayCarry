using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // Local-space clearance grid. Crossing a door is a separate committed action.
    public sealed class Metro3DNavigation
    {
        const float Step=.35f;
        sealed class Grid
        {
            public Transform root; public float minX,minZ,floor,step; public int width,height;
            public bool[] clear,closed,sampled; public float[] g,crowdCost; public int[] parent;
            public Matrix4x4[] liftFrames;
            public float[] gateBanks;
            public readonly List<int> open=new List<int>(256);
            public Vector3 Point(int i)=>new Vector3(minX+(i%width)*step,floor,minZ+(i/width)*step);
            public int Index(Vector3 p)=>Mathf.Clamp(Mathf.RoundToInt((p.z-minZ)/step),0,height-1)*width+Mathf.Clamp(Mathf.RoundToInt((p.x-minX)/step),0,width-1);
        }
        readonly Dictionary<(Transform,int),Grid> grids=new Dictionary<(Transform,int),Grid>();
        readonly Collider[] overlaps=new Collider[24];
        readonly RaycastHit[] groundHits=new RaycastHit[24];
        readonly int[] reverse=new int[4096];
        readonly List<Vector3> obstacles=new List<Vector3>(128);
        Grid Build(Transform root,bool train,Vector3 start)
        {
            Vector3 p=root.InverseTransformPoint(start);bool upper=!train&&p.y>2;
            int side=train?0:upper?(p.z>60?3:2):(p.x<0?-1:1);
            var key=(root,side); if(grids.TryGetValue(key,out var existing)) return existing;
            // Navigate in the actual aisle, not a rectangular car outline that
            // includes occupied seats. Symmetric lanes pass either side of poles.
            // Gate lanes have .74m physical clearance and use a .28m planning
            // capsule: their valid centre band is only .18m wide. The former
            // .35m concourse grid can miss an entire lane.
            float step=train||upper?.16f:Step;
            float minX=train?-.48f:upper?-8.6f:(side<0?-8.6f:Metro3DWorld.ScreenDoorX+.32f);
            float maxX=train?.48f:upper?8.6f:(side<0?-Metro3DWorld.ScreenDoorX-.32f:8.6f);
            float minZ=train?Metro3DTrain.FloorStart+.35f:upper?(side==3?91.5f:-27):-1.5f;
            float maxZ=train?Metro3DTrain.FloorEnd-.35f:upper?(side==3?125.5f:15.5f):Metro3DWorld.PlatformLength-2.5f;
            var grid=new Grid {root=root,minX=minX,minZ=minZ,floor=upper?3.6f:0,step=step,width=Mathf.FloorToInt((maxX-minX)/step)+1,height=Mathf.FloorToInt((maxZ-minZ)/step)+1};
            int total=grid.width*grid.height; grid.clear=new bool[total]; grid.sampled=new bool[total]; grid.g=new float[total]; grid.crowdCost=new float[total]; grid.parent=new int[total]; grid.closed=new bool[total];
            var lifts=train?System.Array.Empty<Metro3DLift>():root.GetComponentsInChildren<Metro3DLift>();
            grid.liftFrames=new Matrix4x4[lifts.Length];
            for(int i=0;i<lifts.Length;i++)grid.liftFrames[i]=lifts[i].transform.worldToLocalMatrix*root.localToWorldMatrix;
            var banks=new List<float>();
            if(upper)foreach(var facility in root.GetComponentsInChildren<Metro3DFacility>())
                if(facility.gateDoor!=null&&facility.transform.parent==root&&!banks.Contains(facility.transform.localPosition.z))banks.Add(facility.transform.localPosition.z);
            grid.gateBanks=banks.ToArray();
            grids[key]=grid; return grid;
        }
        bool Clear(Grid grid,int i)
        {
            if(grid.sampled[i]) return grid.clear[i];
                foreach(float gateZ in grid.gateBanks) {
                    // Approach paths stop outside the .65m half housing plus
                    // planning radius. Only the reserved gate action crosses.
                    if(Mathf.Abs(grid.Point(i).z-gateZ)<.94f) {grid.sampled[i]=true;return grid.clear[i]=false;}
                }
                // The current journey uses the stair smart object. A moving
                // cabin is not part of the station's unreserved walking floor.
                // Bounds match Metro3DLift's authored interior plus planning radius.
                foreach(var frame in grid.liftFrames) {
                    Vector3 cabin=frame.MultiplyPoint3x4(grid.Point(i));
                    if(Mathf.Abs(cabin.x)<1.17f&&cabin.z> -1.17f&&cabin.z<1.22f) {grid.sampled[i]=true;return grid.clear[i]=false;}
                }
                Vector3 w=grid.root.TransformPoint(grid.Point(i));
                int n=Physics.OverlapCapsuleNonAlloc(w+Vector3.up*.33f,w+Vector3.up*1.45f,.28f,overlaps,~0,QueryTriggerInteraction.Ignore);
                bool clear=true;
                for(int j=0;j<n;j++) {var c=overlaps[j]; if(c is CharacterController||!c.transform.IsChildOf(grid.root)) continue; clear=false;break;}
                if(clear) {
                    int support=Physics.RaycastNonAlloc(w+Vector3.up*.4f,Vector3.down,groundHits,.8f,~0,QueryTriggerInteraction.Ignore);
                    bool grounded=false;
                    for(int j=0;j<support;j++) {
                        var hit=groundHits[j];
                        if(!(hit.collider is CharacterController)&&hit.collider.transform.IsChildOf(grid.root)&&hit.normal.y>.6f&&Mathf.Abs(hit.point.y-w.y)<.12f) {grounded=true;break;}
                    }
                    clear=grounded&&support<groundHits.Length;
                }
                grid.sampled[i]=true; return grid.clear[i]=clear;
        }
        public int Find(Transform root,bool train,Vector3 start,Vector3 target,Vector3[] output)
            => Find(root,train,start,target,output,null);
        public int Find(Transform root,bool train,Vector3 start,Vector3 target,Vector3[] output,Metro3DPassenger requester)
        {
            var grid=Build(root,train,start);Vector3 localStart=root.InverseTransformPoint(start);
            System.Array.Clear(grid.crowdCost,0,grid.crowdCost.Length);
            if(requester!=null&&!train) {
                requester.world.GetComponent<Metro3DCrowd>().PathObstacles(requester,obstacles);
                foreach(var point in obstacles) {
                    if(Mathf.Abs(point.y-grid.floor)>1.3f)continue;
                    int minX=Mathf.Max(0,Mathf.FloorToInt((point.x-.75f-grid.minX)/grid.step));
                    int maxX=Mathf.Min(grid.width-1,Mathf.CeilToInt((point.x+.75f-grid.minX)/grid.step));
                    int minZ=Mathf.Max(0,Mathf.FloorToInt((point.z-.75f-grid.minZ)/grid.step));
                    int maxZ=Mathf.Min(grid.height-1,Mathf.CeilToInt((point.z+.75f-grid.minZ)/grid.step));
                    for(int z=minZ;z<=maxZ;z++)for(int x=minX;x<=maxX;x++) {
                        int cell=z*grid.width+x;Vector3 delta=grid.Point(cell)-point;delta.y=0;
                        float d=delta.magnitude;
                        // Soft route cost, never an exemption from body collision.
                        // Keep a path available if the crowd occupies every lane.
                        float cost=d<.52f?12f:d<.75f?2f*(.75f-d)/.23f:0;
                        grid.crowdCost[cell]=Mathf.Max(grid.crowdCost[cell],cost);
                    }
                }
            }
            int a=Nearest(grid,localStart),b=Nearest(grid,root.InverseTransformPoint(target));
            if(a<0||b<0) return 0;
            if(a==b) {output[0]=grid.Point(b);return 1;}
            for(int i=0;i<grid.clear.Length;i++) {grid.g[i]=float.PositiveInfinity;grid.parent[i]=-1;grid.closed[i]=false;}
            grid.open.Clear(); grid.open.Add(a);grid.g[a]=0;
            while(grid.open.Count>0) {
                int pick=0; float score=float.PositiveInfinity;
                for(int i=0;i<grid.open.Count;i++) {int c=grid.open[i];float f=grid.g[c]+(grid.Point(c)-grid.Point(b)).magnitude/grid.step;if(f<score){score=f;pick=i;}}
                int current=grid.open[pick];grid.open.RemoveAt(pick); if(current==b) break; grid.closed[current]=true;
                int x=current%grid.width,z=current/grid.width;
                for(int dx=-1;dx<=1;dx++) for(int dz=-1;dz<=1;dz++) {
                    if(dx==0&&dz==0||x+dx<0||x+dx>=grid.width||z+dz<0||z+dz>=grid.height) continue;
                    int n=(z+dz)*grid.width+x+dx;
                    if(grid.closed[n]||!Clear(grid,n)) continue;
                    if(dx!=0&&dz!=0&&(!Clear(grid,current+dx)||!Clear(grid,current+dz*grid.width))) continue;
                    float cost=grid.g[current]+(dx==0||dz==0?1:1.414214f)+grid.crowdCost[n];
                    if(cost>=grid.g[n]) continue; grid.g[n]=cost;grid.parent[n]=current;if(!grid.open.Contains(n)) grid.open.Add(n);
                }
            }
            if(a!=b&&grid.parent[b]<0) return 0;
            int count=0,node=b;
            while(node!=a&&count<reverse.Length) {reverse[count++]=node;node=grid.parent[node];if(node<0)return 0;}
            // Capacity is for compressed turns, not the raw .16m grid walk.
            // A four-car route has >384 raw nodes but only a handful of turns.
            int written=0,oldDx=99,oldDz=99;
            // Compression is valid from the sampled start node, not from any
            // arbitrary sub-cell position. Skipping it cuts across a railing
            // corner even when all subsequent grid edges are collision-clear.
            Vector3 entry=grid.Point(a)-localStart;entry.y=0;
            if(entry.sqrMagnitude>.08f*.08f) output[written++]=grid.Point(a);
            for(int i=count-1;i>=0;i--) {
                if(written>=output.Length) return 0;
                int next=reverse[i], previous=i==count-1?a:reverse[i+1];int dx=next%grid.width-previous%grid.width,dz=next/grid.width-previous/grid.width;
                if(written>0&&dx==oldDx&&dz==oldDz) output[written-1]=grid.Point(next); else output[written++]=grid.Point(next);
                oldDx=dx;oldDz=dz;
            }
            return written;
        }
        int Nearest(Grid grid,Vector3 point)
        {
            int center=grid.Index(point); if(Clear(grid,center)) return center;
            int best=-1;float distance=4; int cx=center%grid.width,cz=center/grid.width;
            for(int x=Mathf.Max(0,cx-4);x<=Mathf.Min(grid.width-1,cx+4);x++) for(int z=Mathf.Max(0,cz-4);z<=Mathf.Min(grid.height-1,cz+4);z++) {
                int i=z*grid.width+x;if(!Clear(grid,i))continue;float d=(grid.Point(i)-point).sqrMagnitude;if(d<distance){distance=d;best=i;}
            }
            return best;
        }
    }
}

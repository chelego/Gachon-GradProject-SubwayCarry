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
            public Transform root; public float minX,minZ; public int width,height;
            public bool[] clear,closed,sampled; public float[] g; public int[] parent;
            public readonly List<int> open=new List<int>(256);
            public Vector3 Point(int i)=>new Vector3(minX+(i%width)*Step,0,minZ+(i/width)*Step);
            public int Index(Vector3 p)=>Mathf.Clamp(Mathf.RoundToInt((p.z-minZ)/Step),0,height-1)*width+Mathf.Clamp(Mathf.RoundToInt((p.x-minX)/Step),0,width-1);
        }
        readonly Dictionary<(Transform,int),Grid> grids=new Dictionary<(Transform,int),Grid>();
        readonly Collider[] overlaps=new Collider[24];
        readonly int[] reverse=new int[4096];
        Grid Build(Transform root,bool train,Vector3 start)
        {
            Vector3 p=root.InverseTransformPoint(start); int side=train?0:(p.x<0?-1:1);
            var key=(root,side); if(grids.TryGetValue(key,out var existing)) return existing;
            float minX=train?-1.14f:(side<0?-8.6f:Metro3DWorld.ScreenDoorX+.32f);
            float maxX=train?1.14f:(side<0?-Metro3DWorld.ScreenDoorX-.32f:8.6f);
            var grid=new Grid {root=root,minX=minX,minZ=train?Metro3DTrain.FloorStart+.35f:-1.5f,width=Mathf.FloorToInt((maxX-minX)/Step)+1,height=Mathf.FloorToInt((train?Metro3DTrain.FloorEnd-Metro3DTrain.FloorStart-.7f:Metro3DWorld.PlatformLength-1)/Step)+1};
            int total=grid.width*grid.height; grid.clear=new bool[total]; grid.sampled=new bool[total]; grid.g=new float[total]; grid.parent=new int[total]; grid.closed=new bool[total];
            grids[key]=grid; return grid;
        }
        bool Clear(Grid grid,int i)
        {
            if(grid.sampled[i]) return grid.clear[i];
                Vector3 w=grid.root.TransformPoint(grid.Point(i));
                int n=Physics.OverlapCapsuleNonAlloc(w+Vector3.up*.33f,w+Vector3.up*1.45f,.23f,overlaps,~0,QueryTriggerInteraction.Ignore);
                bool clear=true;
                for(int j=0;j<n;j++) {var c=overlaps[j]; if(c is CharacterController||!c.transform.IsChildOf(grid.root)) continue; clear=false;break;}
                grid.sampled[i]=true; return grid.clear[i]=clear;
        }
        public int Find(Transform root,bool train,Vector3 start,Vector3 target,Vector3[] output)
        {
            var grid=Build(root,train,start); int a=Nearest(grid,root.InverseTransformPoint(start)),b=Nearest(grid,root.InverseTransformPoint(target));
            if(a<0||b<0) return 0;
            if(a==b) {output[0]=grid.Point(b);return 1;}
            for(int i=0;i<grid.clear.Length;i++) {grid.g[i]=float.PositiveInfinity;grid.parent[i]=-1;grid.closed[i]=false;}
            grid.open.Clear(); grid.open.Add(a);grid.g[a]=0;
            while(grid.open.Count>0) {
                int pick=0; float score=float.PositiveInfinity;
                for(int i=0;i<grid.open.Count;i++) {int c=grid.open[i];float f=grid.g[c]+(grid.Point(c)-grid.Point(b)).magnitude/Step;if(f<score){score=f;pick=i;}}
                int current=grid.open[pick];grid.open.RemoveAt(pick); if(current==b) break; grid.closed[current]=true;
                int x=current%grid.width,z=current/grid.width;
                for(int dx=-1;dx<=1;dx++) for(int dz=-1;dz<=1;dz++) {
                    if(dx==0&&dz==0||x+dx<0||x+dx>=grid.width||z+dz<0||z+dz>=grid.height) continue;
                    int n=(z+dz)*grid.width+x+dx;
                    if(grid.closed[n]||!Clear(grid,n)) continue;
                    if(dx!=0&&dz!=0&&(!Clear(grid,current+dx)||!Clear(grid,current+dz*grid.width))) continue;
                    float cost=grid.g[current]+(dx==0||dz==0?1:1.414214f);
                    if(cost>=grid.g[n]) continue; grid.g[n]=cost;grid.parent[n]=current;if(!grid.open.Contains(n)) grid.open.Add(n);
                }
            }
            if(a!=b&&grid.parent[b]<0) return 0;
            int count=0,node=b;
            while(node!=a&&count<reverse.Length) {reverse[count++]=node;node=grid.parent[node];if(node<0)return 0;}
            if(count>=output.Length) return 0;
            int written=0,oldDx=99,oldDz=99;
            for(int i=count-1;i>=0;i--) {
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

using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public sealed class Metro3DPlatformDoors : MonoBehaviour
    {
        public Metro3DWorld world;
        public int stationIndex;
        public int trainSide=1;
        public List<Transform> panels=new List<Transform>();
        public List<Collider> blockers=new List<Collider>();
        Vector3[] rest;
        float amount,appliedAmount=-1;
        void Start()
        {
            Metro3DTrain.ResizeSlidingLeaves(panels);
            foreach(var blocker in blockers)
                if(blocker is BoxCollider box)Metro3DTrain.StretchBox(box,Metro3DTrain.DoorScale);
            foreach(Transform part in transform) {
                if(part.name=="PSD fixed glazing boundary") {
                    Metro3DTrain.ResizeFixedBoundary(part,part.GetComponent<BoxCollider>());
                } else if(part.name=="PlatformGlazing") {
                    var p=part.localPosition;
                    int i=Metro3DTrain.NearestDoorIndex(p.z);
                    float door=Metro3DTrain.DoorZ[i];
                    // Each fixed bay is tiled by equal glazing modules. Move all
                    // modules in the bay together to keep their joints closed.
                    float left=-2,right=Metro3DWorld.PlatformLength-2;
                    if(p.z>door) {
                        left=door+Metro3DTrain.AuthoredDoorWidth*.5f;
                        if(i+1<Metro3DTrain.DoorZ.Length)right=Metro3DTrain.DoorZ[i+1]-Metro3DTrain.AuthoredDoorWidth*.5f;
                    } else {
                        right=door-Metro3DTrain.AuthoredDoorWidth*.5f;
                        if(i>0)left=Metro3DTrain.DoorZ[i-1]+Metro3DTrain.AuthoredDoorWidth*.5f;
                    }
                    float a=Metro3DTrain.ResizeFixedEdge(left),b=Metro3DTrain.ResizeFixedEdge(right);
                    float ratio=(b-a)/(right-left);
                    p.z=a+(p.z-left)*ratio;part.localPosition=p;
                    Metro3DTrain.ScaleAlongLength(part,transform,ratio);
                } else if(part.name=="PSD sliding glass pair") {
                    foreach(Transform child in part)
                        if(child.name=="DoorPocket")Metro3DTrain.ScaleAlongLength(child,part,Metro3DTrain.DoorScale);
                }
            }
            rest=new Vector3[panels.Count];for(int i=0;i<panels.Count;i++)rest[i]=panels[i].localPosition;
            Physics.SyncTransforms();
        }
        void Update()
        {
            if(world.session==null||world.session.SimulationPaused||stationIndex>=world.stations.Count)return;
            float target=0;
            foreach(var train in world.Trains)if(train!=null&&train.side==trainSide&&train.CurrentStop==stationIndex&&!train.Moving
                &&Mathf.Abs(train.transform.position.z-world.stations[stationIndex].position.z)<.2f)target=Mathf.Max(target,train.OpenAmount);
            amount=Mathf.MoveTowards(amount,target,Time.deltaTime/world.settings.doorSeconds);
            if(amount==appliedAmount)return;
            for(int i=0;i<panels.Count;i++) panels[i].localPosition=rest[i]+Vector3.forward*(i%2==0?-1:1)*Metro3DTrain.DoorSlide*amount;
            bool blocked=amount<.85f;
            if(appliedAmount<0||blocked!=(appliedAmount<.85f))
                foreach(var b in blockers) b.enabled=blocked;
            appliedAmount=amount;
        }
    }
}

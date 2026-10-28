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
        float amount;
        void Start() { rest=new Vector3[panels.Count]; for(int i=0;i<panels.Count;i++) rest[i]=panels[i].localPosition; }
        void Update()
        {
            if(world.session.SimulationPaused) return;
            float target=0;
            foreach(var train in world.Trains)if(train!=null&&train.side==trainSide&&train.CurrentStop==stationIndex&&!train.Moving
                &&Mathf.Abs(train.transform.position.z-world.stations[stationIndex].position.z)<.2f)target=Mathf.Max(target,train.OpenAmount);
            amount=Mathf.MoveTowards(amount,target,Time.deltaTime/world.settings.doorSeconds);
            for(int i=0;i<panels.Count;i++) panels[i].localPosition=rest[i]+Vector3.forward*(i%2==0?-1:1)*.68f*amount;
            foreach(var b in blockers) b.enabled=amount<.85f;
        }
    }
}

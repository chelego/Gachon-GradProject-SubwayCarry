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
            if(world.session.Blocked) return;
            float target=world.train.side==trainSide&&world.train.CurrentStop==stationIndex&&!world.train.Moving?world.train.OpenAmount:0;
            amount=Mathf.MoveTowards(amount,target,Time.deltaTime/world.settings.doorSeconds);
            for(int i=0;i<panels.Count;i++) panels[i].localPosition=rest[i]+Vector3.forward*(i%2==0?-1:1)*.68f*amount;
            foreach(var b in blockers) b.enabled=amount<.85f;
        }
    }
}

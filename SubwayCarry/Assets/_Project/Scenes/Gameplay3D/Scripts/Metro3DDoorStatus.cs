using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public sealed class Metro3DDoorStatus : MonoBehaviour
    {
        public Metro3DWorld world;
        public int stationIndex,side;
        Renderer lamp;
        MaterialPropertyBlock properties;
        int last=-1;
        void Start()
        {
            foreach(var r in GetComponentsInChildren<Renderer>()) if(r.sharedMaterial.name=="Metro_Luminous") lamp=r;
            properties=new MaterialPropertyBlock();
        }
        void Update()
        {
            if(lamp==null||world.train==null) return;
            var train=world.train;
            int state=train.side==side&&train.CurrentStop==stationIndex&&!train.Moving?(train.DoorsOpen?2:1):0;
            if(state==last) return; last=state;
            Color color=state==2?new Color(.06f,.68f,.20f):state==1?new Color(.9f,.21f,.025f):new Color(.18f,.12f,.04f);
            properties.SetColor("_BaseColor",color); properties.SetColor("_EmissionColor",color*(state==0?.1f:2)); lamp.SetPropertyBlock(properties);
        }
    }
}

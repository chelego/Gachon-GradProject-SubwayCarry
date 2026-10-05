using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    // Two real levels, moving cabin and separate landing leaves. No scene change/teleport.
    public sealed class Metro3DLift : MonoBehaviour
    {
        public Metro3DWorld world;
        public Transform cabin;
        public Transform[] leaves;
        public Collider[] doorBlockers;
        public float upper=3.6f;
        public float OpenAmount {get;private set;}=1;
        public bool Moving {get;private set;}
        int floor,target;
        bool requested;
        float hold;
        Vector3[] rest;
        Transform rider,previousParent;
        void Start() {rest=new Vector3[leaves.Length]; for(int i=0;i<rest.Length;i++) rest[i]=leaves[i].localPosition;}
        public void Request(int level)
        {
            if(Moving) return;
            target=level<0?1-floor:Mathf.Clamp(level,0,1);
            if(target==floor) {hold=3; requested=false; return;}
            requested=true; hold=.8f;
        }
        void Update()
        {
            if(world.session==null||world.session.Blocked||rest==null) return;
            Vector3 p=transform.InverseTransformPoint(world.player.transform.position);
            bool inside=Mathf.Abs(p.x)<.89f&&p.z>-.89f&&p.z<.94f&&Mathf.Abs(p.y-cabin.localPosition.y)<.35f;
            if(inside&&rider==null) {rider=world.player.transform; previousParent=rider.parent; rider.SetParent(cabin,true);}
            if(!inside&&rider!=null&&!Moving) {rider.SetParent(previousParent,true); rider=null;}
            hold=Mathf.Max(0,hold-Time.deltaTime);
            // Never close/move while a human straddles the actual threshold.
            bool threshold=Mathf.Abs(p.x)<.84f&&p.z>.69f&&p.z<1.25f&&Mathf.Abs(p.y-cabin.localPosition.y)<.45f;
            float desiredOpen=!Moving&&(!requested||hold>0||threshold)?1:0;
            OpenAmount=Mathf.MoveTowards(OpenAmount,desiredOpen,Time.deltaTime/1.0f);
            if(requested&&hold==0&&OpenAmount==0) Moving=true;
            if(Moving) {
                var pos=cabin.localPosition; pos.y=Mathf.MoveTowards(pos.y,target*upper+.025f,.65f*Time.deltaTime); cabin.localPosition=pos;
                if(Mathf.Abs(pos.y-(target*upper+.025f))<.001f) {Moving=false; floor=target; requested=false; hold=2;}
            }
            for(int i=0;i<leaves.Length;i++) {
                int leafFloor=i/2; float open=leafFloor==floor&&!Moving?OpenAmount:0;
                leaves[i].localPosition=rest[i]+Vector3.right*(i%2==0?-.69f:.69f)*open;
            }
            for(int i=0;i<doorBlockers.Length;i++) doorBlockers[i].enabled=Moving||floor!=i||OpenAmount<.9f;
            Physics.SyncTransforms();
        }
        void OnDestroy() {if(rider!=null) rider.SetParent(previousParent,true);}
    }
}

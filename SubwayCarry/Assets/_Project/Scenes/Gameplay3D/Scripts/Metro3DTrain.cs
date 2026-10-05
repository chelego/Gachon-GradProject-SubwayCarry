using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    [DefaultExecutionOrder(-100)]
    public sealed class Metro3DTrain : MonoBehaviour
    {
        // A 19.6m design carriage with four 1.3m door openings, shared with PSD placement.
        // Dimensions are game design estimates, not a vehicle engineering certification.
        public const int CarCount=4, DoorsPerCar=4;
        public const float CarLength=19.6f, CarPitch=20.4f;
        public const float FloorStart=10, FloorEnd=FloorStart+CarLength+(CarCount-1)*CarPitch;
        public static readonly float[] DoorZ = BuildDoors();
        static float[] BuildDoors() {var a=new float[CarCount*DoorsPerCar];for(int c=0;c<CarCount;c++) for(int d=0;d<DoorsPerCar;d++) a[c*DoorsPerCar+d]=FloorStart+c*CarPitch+2.5f+d*4.9f; return a;}
        public static string DoorNumber(int i)=>(i/DoorsPerCar+1)+"-"+(i%DoorsPerCar+1);
        public Metro3DWorld world;
        public int side=1;
        public List<Transform> panels=new List<Transform>();
        public List<BoxCollider> blockers=new List<BoxCollider>();
        public float OpenAmount {get;private set;}
        public float MotionSpeed {get;private set;}
        public int CurrentStop {get;private set;}
        public bool DoorsOpen => OpenAmount>.92f;
        public bool Moving {get;private set;}
        float startZ,endZ,duration,elapsed,targetDoors;
        Vector3[] panelRest;

        void Start() { Cache(); }
        void Cache() {
            if(panelRest!=null) return; panelRest=new Vector3[panels.Count];
            for(int i=0;i<panels.Count;i++) panelRest[i]=panels[i].localPosition;
        }
        public void MoveTo(int station,float seconds)
        {
            CurrentStop=station; startZ=transform.localPosition.z; endZ=station*Metro3DWorld.StationSpacing;
            duration=Mathf.Max(1,seconds); elapsed=0; Moving=true; SetDoors(false);
        }
        public void SetDoors(bool open) { targetDoors=open?1:0; }
        public void DepartOutOfRoute(float seconds)
        {
            startZ=transform.localPosition.z; endZ=(world.session.Order.stops.Length+2)*Metro3DWorld.StationSpacing;
            duration=seconds; elapsed=0; Moving=true; SetDoors(false);
        }
        void Update()
        {
            if(world.session==null||world.session.Blocked) return;
            Cache(); float previous=transform.position.z;
            if(Moving) {
                elapsed+=Time.deltaTime; float t=Mathf.Clamp01(elapsed/duration); float smooth=t*t*(3-2*t);
                var p=transform.localPosition; p.z=Mathf.Lerp(startZ,endZ,smooth); transform.localPosition=p;
                if(t>=1) Moving=false;
            }
            MotionSpeed=(transform.position.z-previous)/Mathf.Max(.001f,Time.deltaTime);
            OpenAmount=Mathf.MoveTowards(OpenAmount,targetDoors,Time.deltaTime/world.settings.doorSeconds);
            for(int i=0;i<panels.Count;i++) panels[i].localPosition=panelRest[i]+Vector3.forward*(i%2==0?-1:1)*.68f*OpenAmount;
            // The 0.48m capsule can pass only when the visible aperture is wide enough.
            foreach(var c in blockers) c.enabled=OpenAmount<.85f;
            // Keep the kinematic carriage's ground/walls current before CharacterController.Move.
            Physics.SyncTransforms();
        }
        public bool Contains(Vector3 p)
        {
            p=transform.InverseTransformPoint(p); return Mathf.Abs(p.x)<1.46f&&p.z>FloorStart&&p.z<FloorEnd&&p.y<2.3f&&p.y>-.2f;
        }
        public Vector3 DoorPoint(int i,bool platform)
            => transform.TransformPoint(new Vector3(platform?-side*(Metro3DWorld.ScreenDoorX- Metro3DWorld.TrackCenterX+.55f):-side*.63f,0,DoorZ[Mathf.Clamp(i,0,DoorZ.Length-1)]));
        public int NearestDoor(Vector3 point) {float z=transform.InverseTransformPoint(point).z,best=float.PositiveInfinity;int result=0;for(int i=0;i<DoorZ.Length;i++) {float d=Mathf.Abs(z-DoorZ[i]);if(d<best){best=d;result=i;}}return result;}
    }
}

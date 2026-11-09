using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    [DefaultExecutionOrder(-100)]
    public sealed class Metro3DTrain : MonoBehaviour
    {
        // A 19.6m design carriage with four shared train/PSD openings.
        // Dimensions are game design estimates, not a vehicle engineering certification.
        public const int CarCount=4, DoorsPerCar=4;
        public const float CarLength=19.6f, CarPitch=20.4f;
        public const float FloorStart=10, FloorEnd=FloorStart+CarLength+(CarCount-1)*CarPitch;
        internal const float AuthoredDoorWidth=1.30f, DoorWidth=1.76f;
        internal const float DoorScale=DoorWidth/AuthoredDoorWidth, DoorSlide=.68f*DoorScale;
        internal const float DoorLaneOffset=.36f;
        const float BenchDepth=.49f, BenchCenter=1.10f;
        const float AisleGain=(BenchCenter-BenchDepth*.5f)*.15f;
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
        public int TravelDirection {get;private set;}=1;
        public bool Autonomous {get;set;}
        public bool BoardingWindow {get;set;}
        public bool CanBoard => DoorsOpen&&!Moving&&(Autonomous?BoardingWindow:world.session.CanPassengersBoard);
        public float MovementRemaining => Moving?Mathf.Max(0,duration-elapsed):0;
        public void ConfigureService(int direction,int stop) {TravelDirection=direction<0?-1:1;CurrentStop=stop;}
        float startZ,endZ,duration,elapsed,targetDoors,appliedDoorAmount=-1;
        Vector3[] panelRest;

        void Start() { Cache(); }
        bool passengerSpaceConfigured;
        internal void ConfigurePassengerSpace()
        {
            if(passengerSpaceConfigured)return;
            passengerSpaceConfigured=true;
            ResizeSlidingLeaves(panels);
            foreach(Transform part in transform) {
                Vector3 position=part.localPosition;
                switch(part.name) {
                    case "Visible carriage side boundary":
                        ResizeFixedBoundary(part,part.GetComponent<BoxCollider>());break;
                    case "Train closed door":
                    case "Opposite non-service door":
                    case "Boarding tread ground contact":
                        StretchBox(part.GetComponent<BoxCollider>(),DoorScale);break;
                    case "Boarding threshold":
                        ScaleAlongLength(part,transform,DoorScale);break;
                    case "Sliding door pair":
                        if(Mathf.Sign(position.x)!=-side) foreach(Transform leaf in part)
                            if(leaf.name=="SlidingDoor")ResizeSlidingLeaf(leaf);
                        break;
                    case "Bench physical base":
                        var bench=part.GetComponent<BoxCollider>();
                        var size=bench.size;size.z-=AisleGain;bench.size=size;
                        position.x+=Mathf.Sign(position.x)*AisleGain*.5f;part.localPosition=position;
                        break;
                    case "TrainBench":
                        var scale=part.localScale;scale.z*=1-AisleGain/.55f;part.localScale=scale;
                        position.x+=Mathf.Sign(position.x)*AisleGain*.5f;part.localPosition=position;
                        break;
                    case "Feet anchor":
                        position.x+=Mathf.Sign(position.x)*AisleGain*.70f;part.localPosition=position;
                        break;
                    case "Approach anchor":
                        position.x+=Mathf.Sign(position.x)*AisleGain;part.localPosition=position;
                        break;
                    default:
                        if(part.name.StartsWith("Seat_",System.StringComparison.Ordinal)) {
                            position.x+=Mathf.Sign(position.x)*AisleGain*.70f;part.localPosition=position;
                        }
                        break;
                }
            }
        }

        internal static float ResizeFixedEdge(float z)
        {
            float door=DoorZ[NearestDoorIndex(z)],delta=z-door;
            return Mathf.Abs(Mathf.Abs(delta)-AuthoredDoorWidth*.5f)<.015f
                ? door+Mathf.Sign(delta)*DoorWidth*.5f:z;
        }
        internal static void ResizeFixedBoundary(Transform part,BoxCollider box)
        {
            var p=part.localPosition;
            float a=ResizeFixedEdge(p.z-box.size.z*.5f),b=ResizeFixedEdge(p.z+box.size.z*.5f);
            p.z=(a+b)*.5f;part.localPosition=p;
            var size=box.size;size.z=b-a;box.size=size;
        }
        internal static void StretchBox(BoxCollider box,float ratio)
        {
            var size=box.size;size.z*=ratio;box.size=size;
        }
        internal static void ResizeSlidingLeaves(IEnumerable<Transform> leaves)
        {
            foreach(var leaf in leaves)ResizeSlidingLeaf(leaf);
        }
        static void ResizeSlidingLeaf(Transform leaf)
        {
            var p=leaf.localPosition;p.z*=DoorScale;leaf.localPosition=p;
            ScaleAlongLength(leaf,leaf.parent,DoorScale);
        }
        internal static void ScaleAlongLength(Transform part,Transform frame,float ratio)
        {
            var axis=part.InverseTransformDirection(frame.forward);
            var scale=part.localScale;
            if(Mathf.Abs(axis.x)>.9f)scale.x*=ratio;
            else if(Mathf.Abs(axis.z)>.9f)scale.z*=ratio;
            else if(Mathf.Abs(axis.y)>.9f)scale.y*=ratio;
            part.localScale=scale;
        }
        void Cache() {
            if(panelRest!=null) return; ConfigurePassengerSpace();panelRest=new Vector3[panels.Count];
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
            startZ=transform.localPosition.z; endZ=(TravelDirection>0?world.session.Route.stops.Length+1:-2)*Metro3DWorld.StationSpacing;
            duration=seconds; elapsed=0; Moving=true; SetDoors(false);
        }
        void Update()
        {
            if(world.session==null||world.session.SimulationPaused) return;
            Cache(); float previous=transform.position.z;
            bool moved=Moving;
            if(Moving) {
                elapsed+=Time.deltaTime; float t=Mathf.Clamp01(elapsed/duration); float smooth=t*t*(3-2*t);
                var p=transform.localPosition; p.z=Mathf.Lerp(startZ,endZ,smooth); transform.localPosition=p;
                if(t>=1) Moving=false;
            }
            MotionSpeed=(transform.position.z-previous)/Mathf.Max(.001f,Time.deltaTime);
            OpenAmount=Mathf.MoveTowards(OpenAmount,targetDoors,Time.deltaTime/world.settings.doorSeconds);
            bool doorsChanged=OpenAmount!=appliedDoorAmount;
            if(doorsChanged) {
                for(int i=0;i<panels.Count;i++) panels[i].localPosition=panelRest[i]+Vector3.forward*(i%2==0?-1:1)*DoorSlide*OpenAmount;
                // The 0.48m capsule can pass only when the visible aperture is wide enough.
                bool blocked=OpenAmount<.85f;
                if(appliedDoorAmount<0||blocked!=(appliedDoorAmount<.85f))
                    foreach(var c in blockers) c.enabled=blocked;
                appliedDoorAmount=OpenAmount;
            }
            // Keep the kinematic carriage's ground/walls current before CharacterController.Move.
            if(moved||doorsChanged)Physics.SyncTransforms();
        }
        public bool Contains(Vector3 p)
        {
            p=transform.InverseTransformPoint(p); return Mathf.Abs(p.x)<1.46f&&p.z>FloorStart&&p.z<FloorEnd&&p.y<2.3f&&p.y>-.2f;
        }
        public Vector3 DoorPoint(int i,bool platform)=>DoorPoint(i,platform,0);
        internal Vector3 DoorPoint(int i,bool platform,int lane)
            => transform.TransformPoint(new Vector3(platform?-side*(Metro3DWorld.ScreenDoorX- Metro3DWorld.TrackCenterX+.55f):-side*.63f,0,
                DoorZ[Mathf.Clamp(i,0,DoorZ.Length-1)]+lane*DoorLaneOffset));
        public int NearestDoor(Vector3 point)=>NearestDoorIndex(transform.InverseTransformPoint(point).z);
        internal static int NearestDoorIndex(float z)
        {
            float best=float.PositiveInfinity;int result=0;
            for(int i=0;i<DoorZ.Length;i++) {float d=Mathf.Abs(z-DoorZ[i]);if(d<best){best=d;result=i;}}
            return result;
        }
        void OnDestroy() {if(world!=null)world.Trains.Remove(this);}
    }
}

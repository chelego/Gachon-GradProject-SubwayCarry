using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SubwayCarry.Gameplay3D
{
    [DefaultExecutionOrder(-200)]
    public sealed partial class Metro3DWorld : MonoBehaviour
    {
        public const float StationSpacing = 240;
        public const float TrackCenterX = 1.7f;
        public const float ScreenDoorX = 3.52f;
        public Vector3 HubSpawn => new Vector3(0, settings.departureFloor, -13);
        public int ServiceSide {get;private set;}=1;
        public Metro3DSettings settings;
        public Metro3DPlayer player;
        public Metro3DSession session;
        public Metro3DTrain train;
        public List<Transform> stations = new List<Transform>();
        public List<Metro3DFacility> facilities = new List<Metro3DFacility>();
        public List<Metro3DPassenger> passengers = new List<Metro3DPassenger>();
        readonly Dictionary<string, GameObject> modules = new Dictionary<string, GameObject>();
        Transform routeDecks;
        float nextStationVisibility;

        void Awake()
        {
            if (player == null) BuildInitial();
            Metro3DWorldSign.PrepareExisting(transform);
            session.Initialize(this);
            if (passengers.Count == 0) SpawnPassengers(0, false);
        }

        void Update()
        {
            if(player==null||Time.unscaledTime<nextStationVisibility) return;
            nextStationVisibility=Time.unscaledTime+.5f;
            foreach(var station in stations) if(station!=null) {
                bool nearby=Mathf.Abs(player.transform.position.z-(station.position.z+PlatformLength*.5f-2))<145;
                if(station.gameObject.activeSelf!=nearby) station.gameObject.SetActive(nearby);
            }
        }

        public void BuildInitial()
        {
            if (settings == null) return;
            var scope = gameObject.AddComponent<Metro3DRenderScope>(); scope.pipeline = settings.renderPipeline;
            CreateStation(0, "가천대", "Gachon University");
            train = CreateTrain(1, 0);
            var actor = new GameObject("Player_FirstPerson"); actor.transform.SetParent(transform);
            actor.transform.position = HubSpawn;
            var cc = actor.AddComponent<CharacterController>(); cc.height = 1.75f; cc.radius = .24f;
            cc.center = new Vector3(0, .88f, 0); cc.stepOffset = .22f; cc.slopeLimit = 48; cc.skinWidth = .035f;
            player = actor.AddComponent<Metro3DPlayer>(); player.world = this;
            var cameraObject = new GameObject("FirstPersonCamera"); cameraObject.transform.SetParent(actor.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 1.62f, 0);
            player.view = cameraObject.AddComponent<Camera>(); player.view.tag = "MainCamera";
            player.view.nearClipPlane = .035f; player.view.farClipPlane = 260; player.view.fieldOfView = 72;
            player.view.clearFlags = CameraClearFlags.SolidColor; player.view.backgroundColor = new Color(.025f,.027f,.027f);
            cameraObject.AddComponent<AudioListener>();
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            player.package = actor.AddComponent<SubwayCarry.Gameplay.PackageDurability>();
            var cargo = Module("CakeBox", cameraObject.transform, new Vector3(0,-.47f,.62f));
            cargo.transform.localRotation = Quaternion.identity;
            player.cargo = cargo.transform; cargo.SetActive(false);
            session = gameObject.AddComponent<Metro3DSession>();
            var hud = gameObject.AddComponent<Metro3DHud>(); hud.world = this;
        }

        public GameObject Module(string name, Transform parent, Vector3 position, float yaw = 0)
        {
            if (!modules.TryGetValue(name, out var template))
            {
                var t = settings.environmentLibrary.transform.Find(name);
                if (t == null) throw new System.InvalidOperationException("Missing Blender module: " + name);
                modules.Add(name, template = t.gameObject);
            }
            var go = Instantiate(template, parent); go.name = name;
            go.SetActive(true); go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(0,yaw,0);
            return go;
        }

        GameObject DoorLeaf(string name,Transform parent,Vector3 position,float yaw,Vector3 meetingDirection)
        {
            var leaf=Module(name,parent,position,yaw);
            // Blender +X becomes Unity -X inside the preserved FBX axis transform.
            // Read the exported marker instead of assuming the importer coordinate handedness.
            Transform meeting=null;
            foreach(var child in leaf.GetComponentsInChildren<Transform>())
                if(child.name.EndsWith("_MeetingEdge",System.StringComparison.Ordinal)) {meeting=child;break;}
            if(meeting==null) throw new System.InvalidOperationException("Door meeting-edge marker missing: "+name);
            var edge=parent.InverseTransformVector(meeting.position-leaf.transform.position);
            if(Vector3.Dot(edge,meetingDirection)<0)
                leaf.transform.localScale=new Vector3(-1,1,1);
            return leaf;
        }

        GameObject Slab(string name, Transform parent, Vector3 center, Vector3 size, Material material, bool collision = true)
        {
            var go = Module("FloorPanel", parent, center + new Vector3(0,size.y*.5f,0)); go.name = name;
            go.transform.localScale = new Vector3(size.x/2,size.y/.2f,size.z/2);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = material;
            // Slab dimensions must not stretch the floor's physical-size tile texture.
            if(material!=null&&material.mainTexture!=null) {
                Vector2 baseScale=material.mainTextureScale;
                var uv=go.AddComponent<Metro3DSurfaceUv>();
                uv.textureTransform=new Vector4(size.x*.5f*baseScale.x,-size.z*.5f*baseScale.y,center.x/ .6f*baseScale.x,-center.z/ .6f*baseScale.y); uv.Apply();
            }
            if (collision) Box(parent, name + "_GroundContact", center, size);
            return go;
        }

        public static BoxCollider Box(Transform parent, string name, Vector3 center, Vector3 size, bool trigger = false)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = center;
            var col = go.AddComponent<BoxCollider>(); col.size = size; col.isTrigger = trigger; return col;
        }

        void Wall(Transform root, Vector3 position, float yaw)
        {
            Module("WallPanel",root,position,yaw);
            var col = Box(root,"Wall_Footprint",position+Vector3.up*1.5f,new Vector3(2,.0f+3,.22f));
            col.transform.localRotation = Quaternion.Euler(0,yaw,0);
        }

        void Sign(Transform root, Vector3 position, string label, float yaw = 180, float width = 3)
        {
            Slab("Wayfinding sign",root,position,new Vector3(width,.42f,.06f),settings.yellow,false);
            Text(root,position+new Vector3(0,0,-.045f),label,Color.black,.12f,yaw);
        }

        public void Text(Transform parent, Vector3 position, string text, Color color, float size = .1f, float yaw = 0, float maxWidth = 3f, float maxHeight = .24f)
        {
            var o = new GameObject("Label_"+text); o.transform.SetParent(parent,false); o.transform.localPosition=position;
            o.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var tm=o.AddComponent<TextMesh>(); tm.text=text; tm.font=settings.font; tm.fontSize=64;
            tm.characterSize=size; tm.anchor=TextAnchor.MiddleCenter; tm.alignment=TextAlignment.Center; tm.color=color;
            o.AddComponent<Metro3DWorldSign>().Configure(maxWidth,maxHeight);
        }

        void Lighting(Transform root, Vector3 p, float range = 7,float length=1.35f,float yaw=0,bool shadow=false)
        {
            var fixture=Module("FluorescentLight",root,p,yaw); fixture.transform.localScale=new Vector3(length/1.35f,1,1);
            var o=new GameObject("Fluorescent illumination"); o.transform.SetParent(root,false); o.transform.localPosition=p+new Vector3(0,2.62f,0);
            var l=o.AddComponent<Light>(); l.type=LightType.Point; l.color=new Color(.91f,.94f,1); l.intensity=2.2f;
            l.range=range; l.shadows=shadow?LightShadows.Hard:LightShadows.None; l.shadowStrength=.52f; l.shadowBias=.02f; l.shadowNormalBias=.15f;
            if(shadow) o.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
        }

        public void CreateBench(Transform t,Vector3 p,float yaw,bool onTrain)
        {
            Module(onTrain?"TrainBench":"StationBench",t,p,yaw);
            var rotation=Quaternion.Euler(0,yaw,0);
            float width=onTrain?2.70f:1.74f,spacing=onTrain?.53f:.56f;
            var body=Box(t,"Bench physical base",p+Vector3.up*.25f,new Vector3(width,.50f,.49f)); body.transform.localRotation=rotation;
            int limit=onTrain?2:1;
            for(int i=-limit;i<=limit;i++) {
                var marker=Box(t,"Seat_"+i,p+rotation*new Vector3(i*spacing,.65f,.13f),new Vector3(.48f,.48f,.48f),true);
                var f=marker.gameObject.AddComponent<Metro3DFacility>(); f.kind=Metro3DFacilityKind.Seat; f.trainFacility=onTrain; f.caption="앉기";
                var a=new GameObject("Feet anchor"); a.transform.SetParent(t,false);
                a.transform.localPosition=p+rotation*new Vector3(i*spacing,0,.13f); a.transform.localRotation=rotation; f.anchor=a.transform;
                var b=new GameObject("Approach anchor"); b.transform.SetParent(t,false); b.transform.localPosition=p+rotation*new Vector3(i*spacing,0,.75f); f.approach=b.transform;
                facilities.Add(f);
            }
        }

        public Metro3DTrain CreateTrain(int side,int startIndex)
        {
            var root=new GameObject("Train_RealMoving_"+side); root.transform.SetParent(transform,false);
            root.transform.localPosition=new Vector3(-side*TrackCenterX,0,startIndex*StationSpacing-Metro3DTrain.FloorEnd-30);
            var trainComponent=root.AddComponent<Metro3DTrain>(); trainComponent.world=this; trainComponent.side=side;
            var rb=root.AddComponent<Rigidbody>(); rb.isKinematic=true; rb.useGravity=false;
            var t=root.transform;
            // The entire shell, curved roof, underframe and bogies are authored in Blender.
            for(int car=0;car<Metro3DTrain.CarCount;car++) {
              float begin=Metro3DTrain.FloorStart+car*Metro3DTrain.CarPitch, end=begin+Metro3DTrain.CarLength;
              var shell=Module("TrainCarBody",t,new Vector3(0,0,begin)); shell.name="Carriage_"+(car+1);
              Box(t,"Carriage floor",new Vector3(0,-.06f,(begin+end)*.5f),new Vector3(2.85f,.12f,Metro3DTrain.CarLength));
              Box(t,"Carriage overhead boundary",new Vector3(0,2.40f,(begin+end)*.5f),new Vector3(2.70f,.04f,Metro3DTrain.CarLength));
              if(car<Metro3DTrain.CarCount-1) {
                  var joint=Module("TrainGangway",t,new Vector3(0,0,end)); joint.name="Walkable gangway "+(car+1)+"-"+(car+2);
                  Box(t,"Gangway continuous floor",new Vector3(0,-.045f,end+.4f),new Vector3(1.16f,.09f,.8f));
                  foreach(int s in new[]{-1,1}) Box(t,"Gangway side",new Vector3(s*.62f,1.05f,end+.4f),new Vector3(.08f,2.1f,.8f));
              }
              foreach(float boundary in new[]{begin,end}) {
                  foreach(int s in new[]{-1,1}) Box(t,"Bulkhead beside open passage",new Vector3(s*1.015f,1.2f,boundary),new Vector3(.87f,2.4f,.08f));
                  Box(t,"Gangway header",new Vector3(0,2.3f,boundary),new Vector3(1.16f,.2f,.08f));
              }
              foreach(float x in new[]{-1.43f,1.43f}) {
                float edge=begin;
                for(int door=0;door<Metro3DTrain.DoorsPerCar;door++) { float d=Metro3DTrain.DoorZ[car*4+door];
                    Box(t,"Visible carriage side boundary",new Vector3(x,1.1f,(edge+d-.65f)*.5f),new Vector3(.12f,2.2f,d-.65f-edge)); edge=d+.65f;
                }
                Box(t,"Visible carriage side boundary",new Vector3(x,1.1f,(edge+end)*.5f),new Vector3(.12f,2.2f,end-edge));
                for(int door=0;door<Metro3DTrain.DoorsPerCar;door++) { float d=Metro3DTrain.DoorZ[car*4+door];
                    var doorRoot=new GameObject("Sliding door pair"); doorRoot.transform.SetParent(t,false); doorRoot.transform.localPosition=new Vector3(x,0,d);
                    bool service=Mathf.Sign(x)==-side;
                    for(int i=0;i<2;i++) {
                        var panel=DoorLeaf("SlidingDoor",doorRoot.transform,new Vector3(0,0,i==0?-.325f:.325f),x<0?270:90,i==0?Vector3.forward:Vector3.back);
                        if(service) trainComponent.panels.Add(panel.transform);
                    }
                    var blocker=Box(t,service?"Train closed door":"Opposite non-service door",new Vector3(x,1.05f,d),new Vector3(.12f,2.1f,1.30f));
                    if(service) trainComponent.blockers.Add(blocker);
                    var threshold=Module("TrainThreshold",t,new Vector3(Mathf.Sign(x)*1.64f,0,d)); threshold.name="Boarding threshold";
                    Box(t,"Boarding tread ground contact",new Vector3(Mathf.Sign(x)*1.64f,-.018f,d),new Vector3(.44f,.036f,1.3f));
                    Text(t,new Vector3(x-Mathf.Sign(x)*.075f,2.03f,d),Metro3DTrain.DoorNumber(car*4+door),Color.black,.075f,x<0?90:270,.55f,.12f);
                }
              }
              foreach(float localZ in new[]{4.95f,9.85f,14.75f}) foreach(float x in new[]{-1.10f,1.10f}) { float z=begin+localZ;
                CreateBench(t,new Vector3(x,0,z),x<0?90:270,true);
                Module("GrabRail",t,new Vector3(x*.60f,0,z));
              }
              for(float z=begin+2;z<end;z+=4) Lighting(t,new Vector3(0,-.615f,z),4.5f);
            }
            Module("TrainCab",t,new Vector3(0,0,Metro3DTrain.FloorStart-.25f),180);
            Module("TrainCab",t,new Vector3(0,0,Metro3DTrain.FloorEnd+.25f));
            Module("TrainCabPartition",t,new Vector3(0,0,Metro3DTrain.FloorStart+.04f));
            Module("TrainCabPartition",t,new Vector3(0,0,Metro3DTrain.FloorEnd-.04f),180);
            foreach(float z in Metro3DTrain.DoorZ) {
                Module("SupportPole",t,new Vector3(0,0,z)); var col=Box(t,"Pole interaction",new Vector3(0,1,z),new Vector3(.09f,2,.09f));
                var f=col.gameObject.AddComponent<Metro3DFacility>(); f.kind=Metro3DFacilityKind.Hold; f.trainFacility=true; f.caption="잡기";
                var a=new GameObject("Support standing anchor"); a.transform.SetParent(t,false); a.transform.localPosition=new Vector3(.45f,0,z); f.anchor=a.transform; facilities.Add(f);
            }
            var leaning=Box(t,"Lean end wall",new Vector3(.9f,1,Metro3DTrain.FloorEnd-.18f),new Vector3(.3f,1.4f,.2f),true);
            var lean=leaning.gameObject.AddComponent<Metro3DFacility>(); lean.kind=Metro3DFacilityKind.Lean; lean.trainFacility=true; lean.caption="기대기";
            var leanAnchor=new GameObject("Lean feet anchor"); leanAnchor.transform.SetParent(t,false); leanAnchor.transform.localPosition=new Vector3(.9f,0,Metro3DTrain.FloorEnd-.45f); leanAnchor.transform.localRotation=Quaternion.Euler(0,180,0); lean.anchor=leanAnchor.transform; facilities.Add(lean);
            foreach(float z in new[]{Metro3DTrain.FloorStart,Metro3DTrain.FloorEnd}) Box(t,"Carriage end",new Vector3(0,1.20f,z),new Vector3(2.85f,2.4f,.08f));
            Text(t,new Vector3(0,2.15f,10.05f),"수인분당선  |  Suin–Bundang",Color.black,.07f,180,1.4f,.13f);
            Text(t,new Vector3(0,1.23f,Metro3DTrain.FloorStart+.11f),"승무원실  |  출입 금지",Color.black,.055f,180,.72f,.10f);
            Text(t,new Vector3(0,1.23f,Metro3DTrain.FloorEnd-.11f),"승무원실  |  출입 금지",Color.black,.055f,0,.72f,.10f);
            return trainComponent;
        }

        void TrainSide(Transform root,float x,float start,float end)
        {
            float length=end-start;
            int count=Mathf.CeilToInt(length/1.8f);
            float span=length/count;
            for(int i=0;i<count;i++) {
                var module=Module("TrainWindow",root,new Vector3(x,0,start+(i+.5f)*span),x<0?270:90);
                module.transform.localScale=new Vector3(span/1.8f,1,1);
            }
            Box(root,"Visible carriage side boundary",new Vector3(x,1.05f,(start+end)*.5f),new Vector3(.17f,2.24f,length));
        }

        public void EnsureRoute()
        {
            var route=session.Order.stops;
            int direction=route.Length>1&&(route[1].id=="suseo"||route[1].id=="bokjeong")?-1:1;
            if(direction!=ServiceSide) {
                ServiceSide=direction;
                for(int i=passengers.Count-1;i>=0;i--) if(passengers[i]!=null&&passengers[i].inTrain) { var actor=passengers[i]; passengers.RemoveAt(i); Destroy(actor.gameObject); }
                facilities.RemoveAll(f=>f==null||f.trainFacility);
                Destroy(train.gameObject); train=CreateTrain(ServiceSide,0); SpawnPassengers(0,true);
                foreach(var actor in passengers) if(actor!=null&&!actor.inTrain) actor.RelocateToPlatform();
            }
            // Different deliveries reuse the hub, but never retain another route's station labels/decks.
            for(int i=stations.Count-1;i>0;i--) { var removed=stations[i]; facilities.RemoveAll(f=>f==null||f.transform.IsChildOf(removed)); Destroy(removed.gameObject); stations.RemoveAt(i); }
            if(routeDecks!=null) Destroy(routeDecks.gameObject);
            routeDecks=new GameObject("Current physical route tracks").transform; routeDecks.SetParent(transform,false);
            while(stations.Count<route.Length) { int i=stations.Count; CreateStation(i,route[i].label,route[i].id); }
            for(int i=0;i<route.Length-1;i++) {
                Slab("Rail corridor deck",routeDecks,new Vector3(0,-1.55f,i*StationSpacing+StationSpacing*.5f),new Vector3(6.54f,.15f,StationSpacing),settings.rubber);
                float start=i*StationSpacing+PlatformLength-2;
                float end=(i+1)*StationSpacing-2;
                float center=(start+end)*.5f,length=end-start;
                foreach(float track in new[]{-TrackCenterX,TrackCenterX}) for(float z=start+1;z<end;z+=2) Module("TrackModule",routeDecks,new Vector3(track,-1,z));
                for(float z=start+1;z<end;z+=2) {var tunnel=Module("TunnelSegment",routeDecks,new Vector3(0,0,z)); AddStaticMeshContact(tunnel);}
                for(float z=start+10;z<end;z+=20) {
                    var lightObject=new GameObject("Tunnel service light"); lightObject.transform.SetParent(routeDecks,false); lightObject.transform.localPosition=new Vector3(-3.16f,2.35f,z);
                    var light=lightObject.AddComponent<Light>(); light.type=LightType.Point; light.color=new Color(.82f,.86f,.9f); light.range=10; light.intensity=.65f;
                    Slab("Tunnel lamp",routeDecks,new Vector3(-3.16f,2.5f,z),new Vector3(.16f,.08f,.38f),settings.luminous,false);
                }
            }
        }

        public void ResetServiceSide() { ServiceSide=1; }

        public void SpawnPassengers(int stop,bool inside)
        {
            int count=inside?Metro3DTrain.CarCount*6:settings.passengerCount;
            for(int i=0;i<count;i++) {
                var o=Instantiate(settings.passengerPrefab,inside?train.transform:stations[stop]); o.name="Passenger_"+i;
                o.transform.localPosition=inside?new Vector3((i%2==0?-.3f:.3f),0,Metro3DTrain.FloorStart+(i%Metro3DTrain.CarCount)*Metro3DTrain.CarPitch+3.2f+(i/Metro3DTrain.CarCount)*2.35f):PlatformWaitingPoint(i);
                o.transform.localRotation=Quaternion.Euler(0,inside?(i%2==0?90:270):(ServiceSide==1?90:270),0);
                var p=o.AddComponent<Metro3DPassenger>(); p.world=this; p.inTrain=inside; p.index=i; p.stationIndex=stop; passengers.Add(p);
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public sealed partial class Metro3DWorld
    {
        // Reference topology, NOT a measured architectural survey. Platform length is compressed
        // to the existing playable carriage. Do not label other stations as accurate replicas.
        // KRIC: https://station.kric.go.kr/hc/ext/images/visual/handicapped/cnv/KR/KR_K1_0011.png
        // Platform: https://commons.wikimedia.org/wiki/File:Korail-Bundang-line-K223-Gachon-university-station-platform-20181124-152922.jpg
        // Concourse (2015): https://blog.naver.com/overroad89/220897034220
        // Campus connection: https://www.gachon.ac.kr/intl/12864/subview.do
        const float OuterWallX=9, StairRun=11.1f;
        public const float PlatformExpansion=(Metro3DTrain.CarCount-1)*Metro3DTrain.CarPitch;
        public const float PlatformLength=50+PlatformExpansion;
        public const float FarStairBottom=35.4f+PlatformExpansion;

        public Vector3 PlatformWaitingPoint(int i)
            => new Vector3(ServiceSide*(-4.25f-(i%3)*.74f),0,Metro3DTrain.DoorZ[i%Metro3DTrain.DoorZ.Length]+(i%2==0?-.85f:.85f));

        public Transform CreateStation(int index,string label,string english)
        {
            var room=new GameObject("Station_"+index+"_"+label); room.transform.SetParent(transform,false);
            room.transform.localPosition=Vector3.forward*(index*StationSpacing);
            var t=room.transform; stations.Add(t);
            var reference=room.AddComponent<Metro3DStationReference>();
            reference.referenceStation=index==0?"가천대 / K223":"게임용 공통역: "+label;
            reference.realTopology=index==0;
            reference.dimensionsAreEstimated=true;
            float h=settings.departureFloor;
            var openings=new List<Rect> {
                new Rect(-8.98f,0,4.45f,StairRun+.05f),new Rect(4.53f,0,4.45f,StairRun+.05f),
                new Rect(-8.98f,35.35f,4.45f,StairRun+.1f),new Rect(4.53f,35.35f,4.45f,StairRun+.1f),
                new Rect(-8.72f,12.88f,2.24f,2.24f),new Rect(6.48f,30.88f,2.24f,2.24f)};

            // B1 has two end concourses and a connecting passage; B2 has TWO SIDE platforms.
            Surface(t,"B1 near concourse floor",new Rect(-9,-18,18,34),openings,h,false);
            Surface(t,"B1 far concourse floor",new Rect(-9,30,18,27),openings,h,false);
            var connection=new GameObject("B1 extended connecting corridor").transform; connection.SetParent(t,false);
            Surface(connection,"B1 connection floor",new Rect(-2,16,4,14+PlatformExpansion),null,h,false);
            Surface(t,"B1 near concourse ceiling",new Rect(-9,-18,18,34),null,h,true,true);
            Surface(t,"B1 far concourse ceiling",new Rect(-9,30,18,27),null,h,true,true);
            Surface(connection,"B1 connection ceiling",new Rect(-2,16,4,14+PlatformExpansion),null,h,true,true);
            openings[2]=new Rect(-8.98f,35.35f+PlatformExpansion,4.45f,StairRun+.1f);
            openings[3]=new Rect(4.53f,35.35f+PlatformExpansion,4.45f,StairRun+.1f);
            openings[5]=new Rect(6.48f,30.88f+PlatformExpansion,2.24f,2.24f);
            foreach(int side in new[]{1,-1}) {
                float min=side==1?-OuterWallX:ScreenDoorX;
                var footprint=new Rect(min,-2,OuterWallX-ScreenDoorX,PlatformLength);
                Surface(t,side==1?"B2 Jeongja side platform":"B2 Wangsimni side platform",footprint,null,0,false);
                Surface(t,"B2 suspended ceiling",footprint,openings,0,true);
                for(float z=-1;z<PlatformLength-2;z+=2) StationWall(t,new Vector3(-side*OuterWallX,0,z),side==1?90:270,false);
                for(float x=min+1;x<min+footprint.width;x+=2) {
                    StationWall(t,new Vector3(x,0,-2),180,false);
                    StationWall(t,new Vector3(x,0,PlatformLength-2),0,false);
                }
                BuildScreenDoors(t,index,side,label);
                BuildVerticalCirculation(t,side,false);
                BuildVerticalCirculation(t,side,true);
                for(float z=7.7f;z<39.3f+PlatformExpansion;z+=.6f) Module("TactileBlock",t,new Vector3(-side*(ScreenDoorX+.6f),0,z));
                for(int car=0;car<Metro3DTrain.CarCount;car++) foreach(float baseZ in new[]{21f,28f}) {float z=baseZ+car*Metro3DTrain.CarPitch;
                    CreateBench(t,new Vector3(-side*8.45f,0,z),side==1?90:270,false);
                    WallName(t,new Vector3(-side*8.82f,1.96f,z),label,index==0?"Gachon Univ.":StationEnglish(english),side==1?270:90,index==0?"K223":"");
                }
                for(float z=10;z<39+PlatformExpansion;z+=4) Lighting(t,new Vector3(-side*6.2f,0,z),7,3.9f,0,z==22);
                foreach(float z in new[]{16f,32f+PlatformExpansion}) {
                    Board(t,new Vector3(-side*5.7f,2.55f,z),DirectionBoard(index,side),settings.blue,Color.white,0,4.1f);
                    Board(t,new Vector3(-side*5.7f,2.55f,z+.18f),index==0?"← 나가는 곳  1 · 2 · 3 · 4 · 5":"← 나가는 곳 / Exit",settings.yellow,Color.black,180,4.1f);
                }
                var cabinet=Module("FireCabinet",t,new Vector3(-side*8.77f,0,25),side==1?90:270);
                var cabinetContact=Box(t,"Fire cabinet floor contact",cabinet.transform.localPosition+Vector3.up*.8f,new Vector3(.70f,1.6f,.40f));
                cabinetContact.transform.localRotation=cabinet.transform.localRotation;
                Notice(t,new Vector3(-side*8.81f,0,24),side==1?90:270,"안전 안내 / Safety",index==0);
                foreach(float z in new[]{17f,34f}) Module("VentPanel",t,new Vector3(-side*8.84f,0,z),side==1?90:270);
            }
            // Tracks are physically separated from BOTH platform edges, with visible rails/sleepers.
            Slab("Two track ballast bed",t,new Vector3(0,-1.55f,PlatformLength*.5f-2),new Vector3(6.54f,.18f,PlatformLength),settings.rubber);
            foreach(float x in new[]{-TrackCenterX,TrackCenterX}) {
                for(float z=-1;z<PlatformLength-2;z+=2) Module("TrackModule",t,new Vector3(x,-1.0f,z));
            }
            foreach(float z in new[]{-18f,57f}) for(float x=-8;x<9;x+=2) {
                if(Mathf.Abs(x)<2.1f) continue;
                StationWall(t,new Vector3(x,h,z),z<0?180:0,true);
            }
            foreach(float z in new[]{-18f,57f,16f,30f}) foreach(float x in new[]{-2.5f,2.5f}) StationWall(t,new Vector3(x,h,z),z==-18||z==30?180:0,true,1);
            foreach(float z in new[]{-17f,-15f,-13f,-11f,-9f,-7f,-5f,-3f,-1f,1f,3f,5f,7f,9f,11f,13f,15f,31f,33f,35f,37f,39f,41f,43f,45f,47f,49f,51f,53f,55f,56.5f}) {
                StationWall(t,new Vector3(-9,h,z),90,true);
                if(z<53) StationWall(t,new Vector3(9,h,z),270,true);
            }
            for(float z=17;z<30+PlatformExpansion;z+=2) {
                StationWall(connection,new Vector3(-2,h,z),90,true); StationWall(connection,new Vector3(2,h,z),270,true);
            }
            foreach(float z in new[]{16f,30f}) for(float x=-8;x<9;x+=2) if(Mathf.Abs(x)>2.1f) StationWall(t,new Vector3(x,h,z),z==16?0:180,true);
            foreach(float z in new[]{-15f,-10f,-5f,0f,5f,9f,37f,42f,47f,52f}) foreach(float x in new[]{-5.4f,0,5.4f}) Lighting(t,new Vector3(x,h,z),7.5f,4.8f,90,x==0&&(z==-10||z==47));
            for(float z=11;z<39+PlatformExpansion;z+=6) Lighting(connection,new Vector3(0,h,z),5);
            GateBank(t,index,h,-5);
            GateBank(t,index,h,51);
            Board(t,new Vector3(-6.45f,h+2.35f,-.45f),"↓ "+DirectionBoard(index,1),settings.blue,Color.white,0,3.5f);
            Board(t,new Vector3(6.45f,h+2.35f,-.45f),"↓ "+DirectionBoard(index,-1),settings.blue,Color.white,0,3.5f);
            Board(t,new Vector3(-6.45f,h+2.35f,47.17f),"↓ "+DirectionBoard(index,1),settings.blue,Color.white,180,3.5f);
            Board(t,new Vector3(6.45f,h+2.35f,47.17f),"↓ "+DirectionBoard(index,-1),settings.blue,Color.white,180,3.5f);
            Board(t,new Vector3(0,h+2.55f,-8.2f),index==0?"↓ 타는 곳  |  수인분당선":"↑ 나가는 곳  |  Exit",settings.blue,Color.white,0,5.5f);
            Board(t,new Vector3(0,h+2.55f,-8.4f),index==0?"① 가천대학교 · 비전타워  ↑":"↑ 나가는 곳 / Exit",settings.yellow,Color.black,180,5.5f);
            Board(t,new Vector3(0,h+2.55f,50),index==0?"④ EX-HUB 판교방향  |  ⑤ 송파방향 ↑":"↑ 나가는 곳 / Exit",settings.yellow,Color.black,0,6.2f);
            for(float z=-16;z<-6;z+=.6f) Module("TactileGuideBlock",t,new Vector3(0,h,z));
            for(float x=-7.2f;x<7.3f;x+=.6f) Module("TactileGuideBlock",t,new Vector3(x,h,-2),90);
            foreach(float x in new[]{-7.76f,-5.37f,5.37f,7.76f}) {
                Module("TactileGuideBlock",t,new Vector3(x,h,-1.4f));
                Module("TactileBlock",t,new Vector3(x,h,-.65f));
            }
            Notice(t,new Vector3(-8.82f,h,-11),90,"역 이용 안내 / Station guide",index==0);
            Module("ServiceDoor",t,new Vector3(8.83f,h,-9),270);
            Emergency(t,new Vector3(8.78f,h+2.55f,-9),270);
            foreach(float z in new[]{-13.2f,-12.2f,-11.2f}) {
                Module("TicketMachine",t,new Vector3(8.52f,h,z),270);
                Box(t,"Ticket machine footprint",new Vector3(8.52f,h+.8f,z),new Vector3(.6f,1.6f,.76f));
            }
            Connector(t,h,-18,-28,index==0?"① 가천대학교 · 비전타워":"나가는 곳 / Exit");
            Connector(t,h,57,65,index==0?"④ · ⑤ EX-HUB 연결통로":"나가는 곳 / Exit");
            SideExitPassage(t,h,index==0);
            BuildLiftShaft(t,new Vector3(-7.6f,0,14),90,h);
            BuildLiftShaft(t,new Vector3(7.6f,0,32+PlatformExpansion),270,h);
            foreach(var p in new[]{new Vector3(-8.4f,h,-8),new Vector3(8.4f,h,48)}) {
                Module("WasteBin",t,p); Box(t,"Waste sorting footprint",p+Vector3.up*.44f,new Vector3(1,.88f,.43f));
            }
            // Structural columns stay outside gate lanes, stair approaches and tactile route.
            foreach(float z in new[]{-11f,49f}) foreach(float x in new[]{-5f,5f}) {
                Module("Pillar",t,new Vector3(x,h,z)); Box(t,"Column actual footprint",new Vector3(x,h+1.5f,z),new Vector3(.68f,3,.68f));
            }
            // Move the entire far B1 concourse (including its matching floor cuts), not merely its stair art.
            // B2 platform geometry and the connecting corridor already use the expanded dimensions.
            foreach(Transform child in t) if(child!=connection&&child.localPosition.y>=h-.01f&&child.localPosition.z>=30)
                child.localPosition+=Vector3.forward*PlatformExpansion;
            return t;
        }

        void StationWall(Transform t,Vector3 p,float yaw,bool concourse,float width=2)
        {
            var model=Module(concourse?"ConcourseWall":"WallPanel",t,p,yaw); model.transform.localScale=new Vector3(width*.5f,1,1);
            if(width!=2) {var uv=model.AddComponent<Metro3DSurfaceUv>(); uv.textureTransform=new Vector4(width*.5f,1,0,0); uv.Apply();}
            var boundary=Box(t,"Visible wall ground boundary",p+Vector3.up*1.5f,new Vector3(width,3,.2f));
            boundary.transform.localRotation=Quaternion.Euler(0,yaw,0);
        }

        // Rectangle subtraction keeps upper floors/ceilings out of the staircase and lift shafts.
        void Surface(Transform t,string name,Rect area,List<Rect> holes,float floor,bool ceiling,bool wood=false)
        {
            var xs=new SortedSet<float>{area.xMin,area.xMax}; var zs=new SortedSet<float>{area.yMin,area.yMax};
            if(holes!=null) foreach(var r in holes) if(area.Overlaps(r)) {
                xs.Add(Mathf.Clamp(r.xMin,area.xMin,area.xMax)); xs.Add(Mathf.Clamp(r.xMax,area.xMin,area.xMax));
                zs.Add(Mathf.Clamp(r.yMin,area.yMin,area.yMax)); zs.Add(Mathf.Clamp(r.yMax,area.yMin,area.yMax));
            }
            var xx=new List<float>(xs); var zz=new List<float>(zs);
            for(int i=0;i<xx.Count-1;i++) for(int j=0;j<zz.Count-1;j++) {
                var r=Rect.MinMaxRect(xx[i],zz[j],xx[i+1],zz[j+1]); bool cut=false;
                if(holes!=null) foreach(var hole in holes) if(hole.Contains(r.center)) {cut=true;break;}
                if(cut||r.width<.001f||r.height<.001f) continue;
                if(!ceiling) Slab(name,t,new Vector3(r.center.x,floor-.12f,r.center.y),new Vector3(r.width,.24f,r.height),settings.stone);
                else {
                    int nx=Mathf.CeilToInt(r.width/2),nz=Mathf.CeilToInt(r.height/2);
                    float sx=r.width/nx,sz=r.height/nz;
                    for(int u=0;u<nx;u++) for(int v=0;v<nz;v++) {
                        var panel=Module(wood?"ConcourseCeiling":"CeilingPanel",t,new Vector3(r.xMin+(u+.5f)*sx,floor,r.yMin+(v+.5f)*sz));
                        panel.name=name; panel.transform.localScale=new Vector3(sx*.5f,1,sz*.5f);
                    }
                }
            }
        }

        void BuildVerticalCirculation(Transform t,int side,bool far)
        {
            float yaw=far?180:0,bottom=far?FarStairBottom:StairRun,top=far?46.5f+PlatformExpansion:0;
            var rotation=Quaternion.Euler(0,yaw,0);
            var stair=new GameObject("B1_B2_"+(side==1?"Jeongja":"Wangsimni")+"_"+(far?"far":"near")+"_Stairs");
            stair.transform.SetParent(t,false); stair.transform.localPosition=new Vector3(-side*7.76f,0,bottom); stair.transform.localRotation=rotation;
            var stairModel=Module("StationStaircase",stair.transform,Vector3.zero);
            AddStaticMeshContact(stairModel);
            var escalator=new GameObject("B1_B2_Escalator"); escalator.transform.SetParent(t,false);
            escalator.transform.localPosition=new Vector3(-side*5.37f,0,bottom); escalator.transform.localRotation=rotation;
            var escalatorModel=Module("StationEscalator",escalator.transform,Vector3.zero); AddStaticMeshContact(escalatorModel);
            var carry=Box(escalator.transform,"Escalator carrying zone",new Vector3(0,2,-StairRun*.5f),new Vector3(.99f,5,StairRun),true);
            var conveyor=carry.gameObject.AddComponent<Metro3DEscalator>(); conveyor.world=this;
            conveyor.uphill=escalator.transform.TransformDirection(new Vector3(0,3.6f,-StairRun).normalized); conveyor.direction=far?-1:1;
            foreach(float x in new[]{-8.95f,-4.53f}) {
                float px=side==1?x:-x;
                int n=Mathf.CeilToInt(StairRun/1.2f); float span=StairRun/n;
                for(int i=0;i<n;i++) {
                    var guard=Module("GuardRail",t,new Vector3(px,settings.departureFloor,Mathf.Min(bottom,top)+(i+.5f)*span),90);
                    guard.transform.localScale=new Vector3(span/1.2f,1,1);
                }
                Box(t,"Upper shaft side guard",new Vector3(px,settings.departureFloor+.55f,(bottom+top)*.5f),new Vector3(.07f,1.1f,StairRun));
            }
            var infill=Module("GuardRail",t,new Vector3(-side*6.39f,settings.departureFloor,top)); infill.transform.localScale=new Vector3(.515f/1.2f,1,1);
            Box(t,"Upper inter-machine void guard",new Vector3(-side*6.39f,settings.departureFloor+.55f,top),new Vector3(.515f,1.1f,.07f));
            Board(t,new Vector3(-side*6.6f,2.6f,bottom+(far?-.7f:.7f)),"↑ 나가는 곳 / Exit",settings.yellow,Color.black,far?0:180,3.7f);
            Module("TactileBlock",t,new Vector3(-side*7.76f,0,bottom+(far?-.35f:.35f)));
        }

        static void AddStaticMeshContact(GameObject model)
        {
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>()) {
                if(filter.transform.parent.name.StartsWith("MovingStep_")) continue;
                var c=filter.gameObject.AddComponent<MeshCollider>(); c.sharedMesh=filter.sharedMesh;
                if(filter.name.StartsWith("CollisionSurface")) filter.GetComponent<Renderer>().enabled=false;
            }
        }

        void BuildScreenDoors(Transform t,int index,int side,string label)
        {
            var group=new GameObject(side==1?"PSD_Jeongja_Incheon":"PSD_Wangsimni_Cheongnyangni"); group.transform.SetParent(t,false);
            var doors=group.AddComponent<Metro3DPlatformDoors>(); doors.world=this; doors.stationIndex=index; doors.trainSide=side;
            float x=-side*ScreenDoorX;
            foreach(float d in Metro3DTrain.DoorZ) {
                var pair=new GameObject("PSD sliding glass pair"); pair.transform.SetParent(group.transform,false); pair.transform.localPosition=new Vector3(x-side*.04f,0,d);
                Module("DoorPocket",pair.transform,Vector3.zero,side==1?270:90);
                for(int j=0;j<2;j++) doors.panels.Add(DoorLeaf("PlatformDoor",pair.transform,new Vector3(0,0,j==0?-.325f:.325f),side==1?270:90,j==0?Vector3.forward:Vector3.back).transform);
                doors.blockers.Add(Box(group.transform,"PSD actual opening boundary",new Vector3(x,1.2f,d),new Vector3(.12f,2.4f,1.3f)));
                Text(group.transform,new Vector3(x-side*.15f,2.54f,d),label+"  |  "+DirectionDestination(index,side),Color.black,.085f,side==1?90:270,2.8f,.11f);
                Text(group.transform,new Vector3(x-side*.075f,.28f,d),Metro3DTrain.DoorNumber(System.Array.IndexOf(Metro3DTrain.DoorZ,d)),Color.black,.085f,side==1?90:270,.45f,.14f);
                var lamp=Module("DoorStatusLight",group.transform,new Vector3(x-side*.16f,2.645f,d),side==1?270:90);
                var status=lamp.AddComponent<Metro3DDoorStatus>(); status.world=this; status.stationIndex=index; status.side=side;
            }
            float start=-2;
            foreach(float d in Metro3DTrain.DoorZ) { FixedGlazing(group.transform,x,start,d-.65f); start=d+.65f; }
            FixedGlazing(group.transform,x,start,PlatformLength-2);
            for(float z=-1;z<PlatformLength-2;z+=2) Module("PlatformHeader",group.transform,new Vector3(x,0,z),side==1?270:90);
        }

        void FixedGlazing(Transform t,float x,float start,float end)
        {
            int n=Mathf.CeilToInt((end-start)/2); float length=(end-start)/n;
            for(int i=0;i<n;i++) {
                var glass=Module("PlatformGlazing",t,new Vector3(x,0,start+(i+.5f)*length),x<0?270:90);
                glass.transform.localScale=new Vector3(length*.5f,1,1);
            }
            Box(t,"PSD fixed glazing boundary",new Vector3(x,1.2f,(start+end)*.5f),new Vector3(.12f,2.4f,end-start));
        }

        void GateBank(Transform t,int index,float floor,float z)
        {
            const float first=-2.8f,spacing=1.1f;
            for(int i=0;i<6;i++) {
                float x=first+i*spacing;
                Module("FareGate",t,new Vector3(x,floor,z));
                Box(t,"Fare gate visible steel footprint",new Vector3(x,floor+.53f,z),new Vector3(.34f,1.06f,1.2f));
                if(i==5) continue;
                float lane=x+spacing*.5f;
                var barrier=Box(t,"FareGate_Interaction",new Vector3(lane,floor+.65f,z),new Vector3(.76f,1.3f,.06f));
                var motor=barrier.gameObject.AddComponent<Metro3DFareDoor>(); motor.blocker=barrier;
                foreach(int s in new[]{-1,1}) {
                    var hinge=new GameObject("Fare leaf physical hinge"); hinge.transform.SetParent(t,false); hinge.transform.localPosition=new Vector3(lane+s*.38f,floor,z);
                    var leaf=Module("FareLeaf",hinge.transform,new Vector3(-s*.19f,0,0)); leaf.transform.localScale=new Vector3(.5f,1,1);
                    if(s<0) motor.left=hinge.transform; else motor.right=hinge.transform;
                }
                var f=barrier.gameObject.AddComponent<Metro3DFacility>(); f.kind=index==0?Metro3DFacilityKind.FareGate:Metro3DFacilityKind.ExitGate;
                f.blocker=barrier; f.anchor=motor.left; f.gateDoor=motor; f.caption="개찰구"; facilities.Add(f);
            }
            foreach(float side in new[]{-1f,1f}) {
                float edge=side<0?-2.97f:2.87f;
                float outer=side*8.9f,span=Mathf.Abs(outer-edge);
                int n=Mathf.CeilToInt(span/1.2f); float length=span/n;
                for(int i=0;i<n;i++) {var guard=Module("GuardRail",t,new Vector3(Mathf.Min(edge,outer)+(i+.5f)*length,floor,z)); guard.transform.localScale=new Vector3(length/1.2f,1,1);}
                Box(t,"Fare paid area fixed barrier",new Vector3((edge+outer)*.5f,floor+.55f,z),new Vector3(span,1.1f,.06f));
            }
        }

        void Board(Transform t,Vector3 p,string label,Material backing,Color text,float yaw,float width)
        {
            var frame=Module("WayfindingBoard",t,p,yaw+180); frame.transform.localScale=new Vector3(width/3.5f,1,1);
            float ceiling=(p.y>=settings.departureFloor?settings.departureFloor:0)+2.98f;
            var rotation=Quaternion.Euler(0,yaw,0);
            foreach(float x in new[]{-1.15f,1.15f}) {
                var mount=Module("SignSuspension",t,p+rotation*new Vector3(x*width/3.5f,.275f,0));
                mount.transform.localScale=new Vector3(1,Mathf.Max(.02f,ceiling-p.y-.275f),1);
            }
            foreach(var r in frame.GetComponentsInChildren<Renderer>()) {
                var mats=r.sharedMaterials; for(int i=0;i<mats.Length;i++) if(mats[i].name=="Metro_Blue") mats[i]=backing; r.sharedMaterials=mats;
            }
            Text(t,p+Quaternion.Euler(0,yaw,0)*new Vector3(0,0,-.061f),label,text,.085f,yaw,width-.22f,.35f);
        }

        void WallName(Transform t,Vector3 p,string korean,string english,float yaw,string code)
        {
            var rot=Quaternion.Euler(0,yaw,0);
            Module("StationNameBoard",t,p,yaw+180);
            Text(t,p+rot*new Vector3(0,.05f,-.04f),code+"  "+korean,Color.black,.155f,yaw,2.42f,.23f);
            if(!string.IsNullOrEmpty(english)) Text(t,p+rot*new Vector3(0,-.17f,-.04f),english,Color.black,.077f,yaw,2.42f,.12f);
        }

        static string StationEnglish(string id)
        {
            // Do not print internal IDs such as station_서현 on a physical name plate.
            if(string.IsNullOrEmpty(id)) return null;
            switch(id) {
                case "moran":return "Moran"; case "jeongja":return "Jeongja";
                case "bokjeong":return "Bokjeong"; case "suseo":return "Suseo";
                case "imae":return "Imae"; case "seolleung":return "Seolleung";
                case "wangsimni":return "Wangsimni"; case "jamsil":return "Jamsil";
                case "garak":return "Garak Market";
                default:return id.StartsWith("station_",System.StringComparison.Ordinal)?null:id;
            }
        }

        string DirectionDestination(int index,int side)
        {
            if(index==0) return side==1?"정자 · 인천":"왕십리 · 청량리";
            var order=session!=null?session.Order:null;
            if(order==null||index>=order.stops.Length) return "타는 곳";
            if(side!=ServiceSide) return order.stops[index-1].label+" 방면";
            return index+1<order.stops.Length?order.stops[index+1].label+" 방면":"종착 / 내리는 곳";
        }

        string DirectionBoard(int index,int side)
        {
            if(index==0) return side==1?"태평 · 모란 · 정자 →\n인천 방면":"복정 · 수서 · 왕십리 →\n청량리 방면";
            var order=session!=null?session.Order:null;
            string line=order!=null&&index<order.stops.Length?order.stops[index].line:"승강장";
            return DirectionDestination(index,side)+"\n"+line;
        }

        void Notice(Transform t,Vector3 p,float yaw,string heading,bool gachon)
        {
            Module("NoticeBoard",t,p,yaw); yaw+=180; var rot=Quaternion.Euler(0,yaw,0);
            Text(t,p+rot*new Vector3(0,2.03f,-.075f),heading,Color.white,.055f,yaw,.87f,.13f);
            Text(t,p+rot*new Vector3(0,1.75f,-.074f),"B1  대합실  /  B2  승강장",Color.black,.065f,yaw,.87f,.13f);
            Text(t,p+rot*new Vector3(0,1.47f,-.074f),gachon?"① 가천대  ② 서초교  ③ 태평중\n④ 판교방향  ⑤ 송파방향":"나가는 곳은 위층 대합실\n계단 · 에스컬레이터 · 엘리베이터",Color.black,.061f,yaw,.87f,.29f);
            Text(t,p+rot*new Vector3(0,1.15f,-.074f),"먼저 내린 뒤 탑승하세요",Color.black,.062f,yaw,.87f,.13f);
        }

        void Emergency(Transform t,Vector3 p,float yaw)
        {
            Module("EmergencySign",t,p,yaw);
            Text(t,p+Quaternion.Euler(0,yaw,0)*new Vector3(0,0,.045f),"비상구  EXIT",Color.white,.05f,yaw+180,.74f,.13f);
        }

        void Connector(Transform t,float h,float start,float end,string exit)
        {
            float center=(start+end)*.5f,length=Mathf.Abs(end-start);
            Slab("Exit passage floor",t,new Vector3(0,h-.12f,center),new Vector3(4,.24f,length),settings.stone);
            Surface(t,"Exit timber ceiling",new Rect(-2,Mathf.Min(start,end),4,length),null,h,true,true);
            for(float z=Mathf.Min(start,end)+1;z<Mathf.Max(start,end);z+=2) {
                StationWall(t,new Vector3(-2,h,z),90,true); StationWall(t,new Vector3(2,h,z),270,true);
            }
            Board(t,new Vector3(0,h+2.5f,center),exit,settings.yellow,Color.black,start<0?180:0,3.5f);
            Lighting(t,new Vector3(0,h,center));
            // Beyond the modeled station is not yet a modeled street/campus scene.
            Slab("Exit modeled extent",t,new Vector3(0,h+1.5f,end),new Vector3(4,3,.18f),settings.green);
        }

        void BuildLiftShaft(Transform t,Vector3 p,float yaw,float h)
        {
            var root=new GameObject("B1_B2_MovingElevator"); root.transform.SetParent(t,false); root.transform.localPosition=p; root.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var r=root.transform; var lift=root.AddComponent<Metro3DLift>(); lift.world=this; lift.upper=h;
            AddStaticMeshContact(Module("LiftShaft",r,Vector3.zero));
            lift.cabin=Module("LiftCar",r,Vector3.up*.025f).transform; AddStaticMeshContact(lift.cabin.gameObject);
            var rb=lift.cabin.gameObject.AddComponent<Rigidbody>(); rb.isKinematic=true; rb.useGravity=false;
            var leaves=new List<Transform>(); var blockers=new List<Collider>();
            for(int i=0;i<2;i++) {
                float floor=i*h; Module("ElevatorFront",r,new Vector3(0,floor,1.05f));
                Text(r,new Vector3(0,floor+2.58f,1.15f),(i==0?"B2 승강장":"B1 대합실")+"  |  엘리베이터",Color.black,.065f,180,1.75f,.12f);
                for(int j=0;j<2;j++) leaves.Add(DoorLeaf("LiftDoorLeaf",r,new Vector3(j==0?-.325f:.325f,floor,1.05f),0,j==0?Vector3.right:Vector3.left).transform);
                blockers.Add(Box(r,"Elevator actual landing door",new Vector3(0,floor+1.1f,1.06f),new Vector3(1.3f,2.2f,.08f)));
                var button=Box(r,"Lift call button E",new Vector3(.96f,floor+1.1f,1.11f),new Vector3(.12f,.27f,.06f),true);
                var call=button.gameObject.AddComponent<Metro3DFacility>(); call.kind=Metro3DFacilityKind.ElevatorCall; call.lift=lift; call.liftFloor=i; call.caption="엘리베이터 호출";
                call.approach=new GameObject("Lift call standing point").transform; call.approach.SetParent(r,false); call.approach.localPosition=new Vector3(.96f,floor,1.85f); facilities.Add(call);
            }
            lift.leaves=leaves.ToArray(); lift.doorBlockers=blockers.ToArray(); blockers[0].enabled=false;
            var panel=Box(lift.cabin,"Lift floor choice E",new Vector3(.875f,1.1f,.48f),new Vector3(.04f,.3f,.22f),true);
            var ride=panel.gameObject.AddComponent<Metro3DFacility>(); ride.kind=Metro3DFacilityKind.ElevatorRide; ride.lift=lift; ride.liftFloor=-1; ride.caption="층 이동";
            ride.approach=new GameObject("Lift inside standing point").transform; ride.approach.SetParent(lift.cabin,false); ride.approach.localPosition=new Vector3(.3f,0,.3f); facilities.Add(ride);
        }

        void SideExitPassage(Transform t,float h,bool gachon)
        {
            Surface(t,"Exit 2 3 connection floor",new Rect(9,53,8,4),null,h,false);
            Surface(t,"Exit 2 3 timber ceiling",new Rect(9,53,8,4),null,h,true,true);
            for(float x=10;x<17;x+=2) {
                StationWall(t,new Vector3(x,h,53),180,true); StationWall(t,new Vector3(x,h,57),0,true);
            }
            StationWall(t,new Vector3(17,h,54),270,true); StationWall(t,new Vector3(17,h,56),270,true);
            Board(t,new Vector3(12.5f,h+2.5f,55),gachon?"② 성남서초교  |  ③ 태평중학교 →":"나가는 곳 / Exit →",settings.yellow,Color.black,270,3.5f);
            Lighting(t,new Vector3(12.5f,h,55),7,3.5f);
        }
    }

}

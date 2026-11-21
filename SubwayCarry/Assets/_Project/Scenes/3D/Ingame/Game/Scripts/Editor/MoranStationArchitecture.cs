using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SubwayCarry.Gameplay3D.Editor
{
    internal static partial class MoranStationBuilder
    {
        const float HallFloor = 3.6f;
        const float PlatformFirst = 3f, PlatformLast = 98f;
        const float WestWall = -13.2f, EastWall = 12.4f;
        const float PlatformEdge = Metro3DWorld.ScreenDoorX;
        const float StairHalfWidth = 1.5f;
        static Transform architectureLibrary;

        static void BuildArchitecture(Transform root)
        {
            architectureLibrary = AssetDatabase.LoadAssetAtPath<GameObject>(
                Metro3DBuilder.Root + "/Prefabs/BlenderStationKit.prefab").transform;
            var shell = Group(root, "Architecture");
            var hall = Group(shell, "B1 | Ticket hall");
            var lower = Group(shell, "B2 | Side platforms");
            var track = Group(shell, "Permanent way and portals");
            var wells = new[] {
                Rect.MinMaxRect(-9.4f-StairHalfWidth,35,-9.4f+StairHalfWidth,46.1f),
                Rect.MinMaxRect(9.1f-StairHalfWidth,46.9f,9.1f+StairHalfWidth,58),
                Rect.MinMaxRect(-12.2f,58.5f,-9.4f,61.5f),
                Rect.MinMaxRect(9,31.5f,11.8f,34.5f)
            };
            var hallBounds = Rect.MinMaxRect(WestWall,18,EastWall,72);
            var platformBounds = Rect.MinMaxRect(WestWall,PlatformFirst,EastWall,PlatformLast);
            SurfaceWithWells(hall, "Concourse terrazzo", hallBounds, wells, HallFloor-.12f,.24f,
                Mat("Metro_ConcourseFloor"), true);
            SurfaceWithWells(lower, "Platform ceiling", platformBounds, wells,3.37f,.10f,
                Mat("Metro_White"), true);
            FloorPanel(hall, "Concourse ceiling", hallBounds,7.76f,.12f,Mat("Metro_White"), true);
            BuildCeilingGrid(lower, "Platform ceiling", "MoranPlatformGrid");
            BuildCeilingGrid(hall, "Concourse ceiling", "MoranConcourseGrid");

            // The transit system places service side +1 on the negative-X track.
            foreach (int serviceSide in new[] {1,-1})
            {
                bool west = serviceSide>0;
                int xSign = -serviceSide;
                float wall = west ? WestWall : EastWall;
                var platform = Group(lower, west ? "West | Yatap - Jeongja" : "East | Gachon Univ. - Wangsimni");
                var bounds = Rect.MinMaxRect(west?wall:PlatformEdge,PlatformFirst,west?-PlatformEdge:wall,PlatformLast);
                FloorPanel(platform,"Platform terrazzo",bounds,-.13f,.26f,Mat("Metro_Floor"),true);
                Box(platform,"Track-facing concrete foundation",
                    new Vector3(xSign*(PlatformEdge+.14f),-.8f,bounds.center.y),
                    new Vector3(.28f,1.3f,bounds.height),Mat("Metro_BlackStone"));
                AlongWall(platform, "Platform", wall,PlatformFirst,PlatformLast,0,3.32f,west);
                AlongWall(hall,"Concourse",wall,18,72,HallFloor,4.1f,west);
                foreach (float z in new[]{PlatformFirst,PlatformLast})
                    EndWall(platform,bounds.xMin,bounds.xMax,z,0,3.32f);
                foreach(float z in new[]{12.9f,28.4f,69.2f,88.6f})
                    SupportColumn(platform,new Vector3(xSign*7.65f,0,z),3.32f);
                foreach(float z in new[]{23.7f,40.7f,55.3f,69.1f})
                    SupportColumn(hall,new Vector3(xSign*5.6f,HallFloor,z),4.1f);
                BuildMoranScreens(platform,xSign);
            }
            EndWall(hall,WestWall,-2.1f,18,HallFloor,4.1f);
            EndWall(hall,2.1f,EastWall,18,HallFloor,4.1f);
            BuildMoranEntrance(hall);
            EndWall(hall,WestWall,EastWall,72,HallFloor,4.1f);
            BuildMoranTrack(track);
            BuildMoranStair(Group(shell,"West stair | Yatap - Jeongja"),-9.4f,46.1f,0);
            BuildMoranStair(Group(shell,"East stair | Gachon Univ. - Wangsimni"),9.1f,46.9f,180);
            BuildMoranGates(hall);
        }

        static GameObject KitPart(Transform parent,string module,Vector3 point,float yaw=0)
        {
            var source = architectureLibrary.Find(module);
            if (source==null) throw new InvalidOperationException("Missing station kit module " + module);
            var go = UnityEngine.Object.Instantiate(source.gameObject,parent,false);
            go.name = module;
            go.transform.SetLocalPositionAndRotation(point,Quaternion.Euler(0,yaw,0));
            go.SetActive(true);
            return go;
        }

        static void FloorPanel(Transform parent,string name,Rect area,float y,float depth,Material mat,bool solid)
        {
            var part=Box(parent,name,new Vector3(area.center.x,y,area.center.y),
                new Vector3(area.width,depth,area.height),mat,solid);
            if (mat.mainTexture==null) return;
            // Unity's cube top UVs run along +X/-Z. Anchor successive pieces to station coordinates.
            const float repeat=2.4f;
            var uv=part.GetComponent<Metro3DSurfaceUv>()??part.AddComponent<Metro3DSurfaceUv>();
            uv.textureTransform=new Vector4(area.width/repeat,area.height/repeat,area.xMin/repeat,-area.yMax/repeat);
            uv.Apply();
        }

        static void SurfaceWithWells(Transform parent,string name,Rect area,Rect[] wells,
            float y,float depth,Material mat,bool solid)
        {
            // Subtract rectangles without tessellating the rest of the floor into a Cartesian grid.
            var pieces=new List<Rect>{area};
            foreach (var cut in wells)
            {
                for (int i=pieces.Count-1; i>=0; --i)
                {
                    var p=pieces[i];
                    if (!p.Overlaps(cut)) continue;
                    pieces.RemoveAt(i);
                    float x0=Mathf.Max(p.xMin,cut.xMin), x1=Mathf.Min(p.xMax,cut.xMax);
                    float z0=Mathf.Max(p.yMin,cut.yMin), z1=Mathf.Min(p.yMax,cut.yMax);
                    AddPatch(pieces,p.xMin,p.yMin,p.xMax,z0);
                    AddPatch(pieces,p.xMin,z1,p.xMax,p.yMax);
                    AddPatch(pieces,p.xMin,z0,x0,z1);
                    AddPatch(pieces,x1,z0,p.xMax,z1);
                }
            }
            foreach (var piece in pieces) FloorPanel(parent,name,piece,y,depth,mat,solid);
        }

        static void AddPatch(List<Rect> pieces,float x0,float z0,float x1,float z1)
        {
            if (x1-x0>.001f && z1-z0>.001f) pieces.Add(Rect.MinMaxRect(x0,z0,x1,z1));
        }

        // Ingame's enamel ceiling kit has steel runners on a 0.5 m module. Clip
        // the two grids to the actual deck pieces so the stair and lift wells remain open.
        static void BuildCeilingGrid(Transform room, string deckName, string meshName)
        {
            var strips = new List<CombineInstance>();
            const float pitch = .5f, thickness = .009f;
            foreach (Transform deck in room)
            {
                if (deck.name != deckName) continue;
                var cube = deck.GetComponent<MeshFilter>().sharedMesh;
                Vector3 center = deck.localPosition;
                Vector3 size = deck.localScale;
                Vector3 min = center - size * .5f, max = center + size * .5f;
                for (int i = Mathf.CeilToInt(min.x / pitch); i * pitch < max.x; i++)
                    strips.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(
                        new Vector3(i * pitch, min.y - .005f, center.z), Quaternion.identity,
                        new Vector3(thickness, .012f, size.z)) });
                for (int i = Mathf.CeilToInt(min.z / pitch); i * pitch < max.z; i++)
                    strips.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(
                        new Vector3(center.x, min.y - .006f, i * pitch), Quaternion.identity,
                        new Vector3(size.x, .012f, thickness)) });
            }
            var mesh = new Mesh { name = meshName };
            mesh.CombineMeshes(strips.ToArray());
            var path = Game + "/Art/Models/" + meshName + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved)
            {
                EditorUtility.CopySerialized(mesh, saved);
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(saved);
                AssetDatabase.SaveAssetIfDirty(saved);
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, path);
                saved = mesh;
            }
            var grid = Group(room, "Suspended ceiling runners").gameObject;
            grid.AddComponent<MeshFilter>().sharedMesh = saved;
            grid.AddComponent<MeshRenderer>().sharedMaterial = Mat("Circulation_Steel");
        }

        static void AlongWall(Transform parent,string name,float x,float start,float end,float y,float h,bool west)
        {
            float sign=west?1:-1;
            var wall=Group(parent,name+" tiled perimeter");
            float span=end-start;
            var tile=Box(wall,"Glazed ceramic cladding",new Vector3(x-sign*.09f,y+h*.5f,(start+end)*.5f),
                new Vector3(.18f,h,span),Mat("Metro_CeramicWall"));
            var uv=tile.GetComponent<Metro3DSurfaceUv>()??tile.AddComponent<Metro3DSurfaceUv>();
            uv.textureTransform=new Vector4(span/1.8f,h/1.8f,0,0); uv.Apply();
            Box(wall,"Honed stone skirting",new Vector3(x+sign*.018f,y+.13f,(start+end)*.5f),
                new Vector3(.045f,.26f,span),Mat("Metro_BlackStone"),false);
            FinishWall(tile.transform, sign * Vector3.right, y >= HallFloor);
        }

        static void EndWall(Transform parent,float x0,float x1,float z,float y,float height)
        {
            float outward=(z==PlatformFirst || z==18)?-1:1;
            var tile=Box(parent,"Tiled end wall",new Vector3((x0+x1)*.5f,y+height*.5f,z+outward*.09f),
                new Vector3(x1-x0,height,.18f),Mat("Metro_CeramicWall"));
            var uv=tile.GetComponent<Metro3DSurfaceUv>()??tile.AddComponent<Metro3DSurfaceUv>();
            uv.textureTransform=new Vector4((x1-x0)/1.8f,height/1.8f,0,0); uv.Apply();
            FinishWall(tile.transform, -outward * Vector3.forward, y >= HallFloor);
        }

        static void FinishWall(Transform tile, Vector3 inward, bool concourse)
        {
            if (concourse) tile.GetComponent<MeshRenderer>().sharedMaterial = Mat("Metro_ConcourseTile");
            Vector3 foot = tile.localPosition - Vector3.up * (tile.localScale.y * .5f) + inward * .095f;
            void Band(string name, float y, float height, string material)
            {
                var size = tile.localScale;
                size.y = height;
                if (inward.x != 0) size.x = .011f;
                else size.z = .011f;
                var strip = Box(tile.parent, name, foot + Vector3.up * y, size, Mat(material), false);
                var uv = strip.AddComponent<Metro3DSurfaceUv>();
                uv.textureTransform = new Vector4(Mathf.Max(size.x, size.z) / 1.2f, height / 1.2f, 0, 0);
                uv.Apply();
            }
            if (concourse) Band("Ochre ceramic band", 1.58f, .24f, "Metro_AccentTile");
            else
            {
                Band("Platform tile band", 1.18f, .15f, "Metro_Brown");
                Band("Bundang line fascia", 2.76f, .19f, "Circulation_Yellow");
            }
        }

        static void SupportColumn(Transform parent,Vector3 feet,float height)
        {
            var column=KitPart(parent,"Pillar",feet);
            column.transform.localScale=new Vector3(.82f,height/3,.82f);
            var contact=column.AddComponent<CapsuleCollider>();
            contact.center=Vector3.up*1.5f;
            contact.height=3;
            contact.radius=.562f;
        }

        static void BuildMoranEntrance(Transform hall)
        {
            var entrance=Group(hall,"Public entrance | Exit passage");
            var floor=Mat("Metro_ConcourseFloor");
            var ceramic=Mat("Metro_CeramicWall");
            var metal=Mat("Metal");
            FloorPanel(entrance,"Passage floor",Rect.MinMaxRect(-2.1f,10.8f,2.1f,18),HallFloor-.10f,.2f,floor,true);
            FloorPanel(entrance,"Passage return",Rect.MinMaxRect(2.1f,10.8f,3.3f,15),HallFloor-.10f,.2f,floor,true);
            FloorPanel(entrance,"Passage soffit",Rect.MinMaxRect(-2.1f,10.8f,2.1f,18),7.25f,.18f,Mat("Ceiling"),true);
            FloorPanel(entrance,"Return soffit",Rect.MinMaxRect(2.1f,10.8f,8.5f,15),7.25f,.18f,Mat("Ceiling"),true);
            AlongWall(entrance,"West return",-2.1f,10.8f,18,HallFloor,3.6f,true);
            AlongWall(entrance,"East return",2.1f,15,18,HallFloor,3.6f,false);
            var front=Box(entrance,"Turn wall",new Vector3(3.2f,5.40f,10.71f),new Vector3(10.6f,3.6f,.18f),ceramic);
            var uv=front.AddComponent<Metro3DSurfaceUv>();
            uv.textureTransform=new Vector4(5.9f,2,0,0); uv.Apply();
            EndWall(entrance,2.1f,8.5f,15,HallFloor,3.6f);
            Box(entrance,"Entrance lintel",new Vector3(0,7.44f,18),new Vector3(4.2f,.52f,.22f),ceramic);
            foreach(float x in new[]{-2.06f,2.06f})
                Box(entrance,"Brushed portal jamb",new Vector3(x,5.39f,18),new Vector3(.09f,3.58f,.25f),metal);
            var directory=Group(entrance,"Exit direction",new Vector3(0,6.30f,10.86f));
            directory.localRotation=Quaternion.Euler(0,180,0);
            Box(directory,"Passage sign",Vector3.zero,new Vector3(3.15f,.55f,.08f),Finish("Ink"),false);
            Lettering(directory,"나가는 곳   Exit   ←",new Vector3(0,0,-.045f),new Vector2(2.96f,.43f),2.4f,Color.white);
            const float first=3.3f;
            for(int i=0; i<8; i++)
            {
                float h=(i+1)*.15f, x=first+(i+.5f)*.30f;
                Box(entrance,"Exit stair tread",new Vector3(x,HallFloor+h*.5f,12.9f),new Vector3(.3f,h,4.2f),Mat("Circulation_Granite"));
                Box(entrance,"Tread safety inset",new Vector3(x-.128f,HallFloor+h+.002f,12.9f),
                    new Vector3(.026f,.004f,4.05f),Mat("Yellow"),false);
            }
            FloorPanel(entrance,"Exit vestibule landing",Rect.MinMaxRect(5.7f,10.8f,8.5f,15),4.70f,.20f,floor,true);
            AlongWall(entrance,"Vestibule",8.50f,10.8f,15,HallFloor,3.6f,false);
            var end=Group(entrance,"Closed upper passage doors",new Vector3(8.38f,4.8f,12.9f));
            end.localRotation=Quaternion.Euler(0,90,0);
            Box(end,"Recessed jamb",new Vector3(0,1.09f,0),new Vector3(2.22f,2.18f,.12f),metal);
            foreach(float side in new[]{-1f,1f}) {
                Box(end,"Enamel door leaf",new Vector3(side*.535f,1.068f,-.073f),new Vector3(1.04f,2.08f,.045f),Finish("Ink"));
                Box(end,"Frosted vision panel",new Vector3(side*.535f,1.50f,-.101f),new Vector3(.82f,.86f,.015f),Finish("Seafoam"),false);
                Box(end,"Stainless push rail",new Vector3(side*.535f,.98f,-.129f),new Vector3(.82f,.055f,.067f),metal,false);
            }
            foreach(float z in new[]{13.2f,16.4f})
                KitPart(entrance,"FluorescentLight",new Vector3(0,4.16f,z));
            KitPart(entrance,"FluorescentLight",new Vector3(6.6f,4.16f,12.9f));
        }

        static void BuildMoranTrack(Transform parent)
        {
            const float extension=10;
            const float first=PlatformFirst-extension, last=PlatformLast+extension;
            FloorPanel(parent,"Continuous ballast bed", Rect.MinMaxRect(-PlatformEdge,first,PlatformEdge,last),
                -1.56f,.16f,Mat("Metro_Ballast"),true);
            int count=Mathf.CeilToInt((last-first)/2);
            float step=(last-first)/count;
            for(int i=0; i<count; i++)
            {
                float z=first+(i+.5f)*step;
                foreach(float x in new[]{-Metro3DWorld.TrackCenterX,Metro3DWorld.TrackCenterX})
                    KitPart(parent,"TrackModule",new Vector3(x,-1,z)).transform.localScale=new Vector3(1,1,step/2);
            }
            foreach(float start in new[]{first, PlatformLast})
            {
                for(int i=0; i<5; i++)
                    KitPart(parent,"TunnelSegment",new Vector3(0,0,start+1+i*2));
                float z=start==first ? first : last;
                Box(parent,"Distant tunnel shadow",new Vector3(0,.94f,z),new Vector3(7.55f,4.9f,.14f),Mat("Rubber"));
            }
            for (float z=6; z<PlatformLast; z+=6)
                Box(parent,"Track ceiling cross beam",new Vector3(0,3.20f,z),
                    new Vector3(7.06f,.24f,.34f),Mat("Metro_BlackStone"),false);
        }

        static void BuildMoranScreens(Transform parent,int side)
        {
            var doors=Group(parent,"Platform screen doors");
            float x=side*PlatformEdge, yaw=side<0?270:90;
            // Match the 1.76 m in-game opening after Metro3DTrain expands the authored 1.30 m leaves.
            const float width=1.76f;
            float start=PlatformFirst;
            for(int i=0; i<Metro3DTrain.DoorZ.Length; i++)
            {
                float z=Metro3DTrain.DoorZ[i];
                GlazingRun(doors,x,yaw,start,z-width/2);
                var pair=Group(doors,"Door "+Metro3DTrain.DoorNumber(i),new Vector3(x,0,z));
                var pocket=KitPart(pair,"DoorPocket",Vector3.zero,yaw);
                pocket.transform.localScale=new Vector3(width/1.30f,1,1);
                for(int leafIndex=0;leafIndex<2;leafIndex++)
                {
                    int dir=leafIndex==0?-1:1;
                    var leaf=KitPart(pair,"PlatformDoor",new Vector3(side*.04f,0,dir*width*.25f),yaw);
                    float flip=1;
                    foreach(var marker in leaf.GetComponentsInChildren<Transform>())
                        if(marker.name.EndsWith("_MeetingEdge",StringComparison.Ordinal)) {
                            float dz=pair.InverseTransformVector(marker.position-leaf.transform.position).z;
                            if (dz*dir>0) flip=-1;
                            break;
                        }
                    leaf.transform.localScale=new Vector3(flip*width/1.30f,1,1);
                }
                Metro3DWorld.Box(pair,"Closed screen door",new Vector3(0,1.2f,0),new Vector3(.12f,2.4f,width));
                KitPart(pair,"DoorStatusLight",new Vector3(side*.145f,2.635f,0),yaw);
                start=z+width/2;
            }
            GlazingRun(doors,x,yaw,start,PlatformLast);
            int headers=Mathf.CeilToInt((PlatformLast-PlatformFirst)/2);
            float headerSpan=(PlatformLast-PlatformFirst)/headers;
            for(int i=0; i<headers; i++)
                KitPart(doors,"PlatformHeader",new Vector3(x,0,PlatformFirst+(i+.5f)*headerSpan),yaw)
                    .transform.localScale=new Vector3(headerSpan*.5f,1,1);
            var tactile=Group(parent,"Platform edge warning surface");
            const float warningStart=7.5f;
            for(int i=0; i<145; i++)
                KitPart(tactile,"TactileBlock",new Vector3(x+side*.6f,.002f,warningStart+(i+.5f)*.6f));
        }

        static void GlazingRun(Transform parent,float x,float yaw,float start,float end)
        {
            int count=Mathf.CeilToInt((end-start)*.5f);
            float span=(end-start)/count;
            for(int i=0; i<count; i++)
                KitPart(parent,"PlatformGlazing",new Vector3(x,0,start+(i+.5f)*span),yaw)
                    .transform.localScale=new Vector3(span*.5f,1,1);
            Metro3DWorld.Box(parent,"Fixed glazing contact",new Vector3(x,1.2f,(start+end)*.5f),
                new Vector3(.12f,2.4f,end-start));
        }

        static void BuildMoranStair(Transform group,float x,float z,float yaw)
        {
            group.SetLocalPositionAndRotation(new Vector3(x,0,z),Quaternion.Euler(0,yaw,0));
            // Blender stair has two flights, a 1.8 m half-landing and tactile nosings.
            var model=KitPart(group,"StationStaircase",Vector3.zero);
            model.transform.localScale=new Vector3(1.35f,1,1);
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
                if (filter.name.Contains("CreamTile")) filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
            // Supporting simplified surfaces avoid tiny nosing bevels snagging the visitor's capsule.
            var path=new[]{
                new Vector2(0,.008f),new Vector2(-1.35f,.008f),new Vector2(-4.8f,1.81f),
                new Vector2(-6.15f,1.81f),new Vector2(-9.6f,3.607f),new Vector2(-11.10f,3.607f)
            };
            for(int i=1; i<path.Length; i++)
            {
                var a=new Vector3(0,path[i-1].y,path[i-1].x);
                var b=new Vector3(0,path[i].y,path[i].x);
                var rot=Quaternion.LookRotation(a-b,Vector3.up);
                var box=Metro3DWorld.Box(group,"Stair walking surface",(a+b)*.5f-rot*Vector3.up*.09f,
                    new Vector3(2.39f,.18f,Vector3.Distance(a,b)+.015f));
                box.transform.localRotation=rot;
            }
            Group(group,"B1 Landing",new Vector3(0,HallFloor,-11.5f));
            Group(group,"B2 Landing",new Vector3(0,0,.45f));
            foreach(float side in new[]{-1f,1f})
                RailRun(group,new Vector3(side*(StairHalfWidth+.045f),HallFloor,-5.55f),11.1f,90);
            RailRun(group,new Vector3(0,HallFloor,.05f),2*StairHalfWidth+.09f,0);
        }

        static void RailRun(Transform parent,Vector3 center,float span,float yaw)
        {
            int count=Mathf.CeilToInt(span/1.2f);
            float length=span/count;
            var rail=Group(parent,"Continuous glass balustrade",center);
            rail.localRotation=Quaternion.Euler(0,yaw,0);
            for (int i=0; i<count; i++)
                KitPart(rail,"GuardRail",new Vector3(-span*.5f+(i+.5f)*length,0,0))
                    .transform.localScale=new Vector3(length/1.2f,1,1);
            Metro3DWorld.Box(rail,"Balustrade contact",Vector3.up*.55f,new Vector3(span,1.1f,.08f));
        }

        static void BuildMoranGates(Transform hall)
        {
            var bank=Group(hall,"Fare control | Five passages",new Vector3(0,HallFloor,29));
            const float pitch=1.25f, first=-3.125f;
            for(int i=0; i<6; i++)
            {
                float x=first+i*pitch;
                KitPart(bank,"FareGate",Vector3.right*x);
                Metro3DWorld.Box(bank,"Stainless gate cabinet",new Vector3(x,.54f,0),new Vector3(.36f,1.08f,1.30f));
                if(i==5) break;
                var lane=Group(bank,"Fare passage "+(i+1),new Vector3(x+pitch*.5f,0,0));
                var arm=Group(lane,"Turnstile hinge",new Vector3(-pitch*.5f+.198f,.89f,0));
                KitPart(arm,"FareLeaf",Vector3.zero);
                var blocker=Metro3DWorld.Box(lane,"Tripod closed barrier",Vector3.up*.65f,new Vector3(pitch-.36f,1.3f,.06f));
                var motor=lane.gameObject.AddComponent<Metro3DFareDoor>();
                motor.left=arm; motor.blocker=blocker; motor.tripod=true;
                var facility=lane.gameObject.AddComponent<Metro3DFacility>();
                facility.kind=Metro3DFacilityKind.FareGate; facility.caption="모란역 개찰구";
                facility.gateDoor=motor; facility.blocker=blocker;
                facility.anchor=Group(lane,"Gate center");
                facility.approach=Group(lane,"Unpaid approach",Vector3.back*1.2f);
            }
            const float end=3.305f;
            RailRun(bank,new Vector3((WestWall-end)/2,0,0),-end-WestWall,0);
            RailRun(bank,new Vector3((EastWall+end)/2,0,0),EastWall-end,0);
            var approach=Group(bank,"Tactile route to ticket hall");
            for(int i=0; i<12; i++) KitPart(approach,"TactileGuideBlock",new Vector3(0,.002f,-1.2f-i*.6f));
        }
    }
}

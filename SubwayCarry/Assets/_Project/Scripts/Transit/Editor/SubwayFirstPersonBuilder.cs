#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SubwayCarry.Transit.Editor
{
    /// <summary>Persistent, metre-scale 3D car authored through the Unity Editor.</summary>
    public static class SubwayFirstPersonBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Subway_QuarterView.unity";
        public const float DesiredAisleWidth = 2f;
        private const string Assets = "Assets/_Project/Art/SubwayFirstPerson";
        private static Material ivory, steel, darkSteel, rubber, seat, prioritySeat, glass, dividerGlass, black, gold, white, light, teal;
        private static Font font;
        private static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        private static readonly float[] DoorZ = { -7.25f, -2.45f, 2.45f, 7.25f };
        private static readonly float[] BenchZ = { -9f, -4.85f, 0f, 4.85f, 9f };
        private static readonly int[] SeatCount = { 3, 7, 7, 7, 3 };

        [MenuItem("SubwayCarry/Train/Convert Quarter View to 3D First Person")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building.");
            if (SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Open Subway_QuarterView first.");
            EnsureFolder(Assets); EnsureFolder(Assets + "/Meshes"); EnsureFolder(Assets + "/Materials");
            EnsureFolder("Assets/_Project/Scenes/Legacy");
            string backup = "Assets/_Project/Scenes/Legacy/Subway_QuarterView_2D_Backup.unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(backup))
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), backup, true);
            PrepareMaterials();
            int renderer = PrepareRenderer();
            var old3D = GameObject.Find("Subway_3D_FirstPerson");
            if (old3D) Undo.DestroyObjectImmediate(old3D);
            foreach (var old in SceneManager.GetActiveScene().GetRootGameObjects())
            { Undo.RecordObject(old, "Archive quarter-view objects"); old.SetActive(false); }
            var root = Group(null, "Subway_3D_FirstPerson");
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create 3D subway");
            var shell = Group(root, "01_CarShell_19.6m_x_2.9m");
            var seats = Group(root, "02_Seating_54_Places");
            var doors = Group(root, "03_SlidingDoors_8");
            var rails = Group(root, "04_StainlessRailsAndStraps");
            var signs = Group(root, "05_RouteMapsAndPassengerInformation");
            var lighting = Group(root, "06_CeilingAndLighting");
            var outside = Group(root, "07_TunnelBackdrop");
            var anchors = Group(root, "08_IntegrationAnchors");
            Box(shell, "RubberFloor", new Vector3(0,-.08f,0), new Vector3(2.9f,.16f,19.6f), rubber, true);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(shell, "FloorSkirting", new Vector3(side*1.40f,.07f,0), new Vector3(.08f,.14f,19.6f), darkSteel, false);
                float start = -9.8f;
                foreach (float z in DoorZ) { Wall(shell, signs, side, start, z-.70f); start=z+.70f; }
                Wall(shell, signs, side, start, 9.8f);
                Box(shell,"ContinuousHeader", new Vector3(side*1.46f,2.32f,0), new Vector3(.16f,.32f,19.6f), ivory, true);
                Box(shell,"UpperLineAccent", new Vector3(side*1.37f,2.20f,0), new Vector3(.025f,.035f,19.55f), gold, false);
                Box(shell,"LowerSteelBand", new Vector3(side*1.38f,.21f,0), new Vector3(.025f,.08f,19.55f), steel, false);
                // Prevent the inspection prototype from leaving through an open doorway.
                var guard = Group(shell, "InteriorBoundary_"+side);
                guard.localPosition = new Vector3(side*1.51f,1.3f,0);
                var collider = guard.gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(.1f,2.6f,19.6f);
                Box(outside,"TunnelWall",new Vector3(side*2.2f,1.3f,0),new Vector3(.15f,2.7f,22),darkSteel,false);
                for(float z=-10;z<=10;z+=1.6f)
                { Box(outside,"TunnelRib",new Vector3(side*2.10f,1.3f,z),new Vector3(.12f,2.7f,.08f),black,false); }
                Box(outside,"ServiceLightStrip",new Vector3(side*2.09f,1.5f,0),new Vector3(.025f,.025f,22),teal,false);
                foreach(float z in DoorZ) CreateDoor(doors, signs, anchors, side, z);
            }
            Box(outside,"ExteriorDarkDeck",new Vector3(0,-.18f,0),new Vector3(4.4f,.10f,22),black,false);
            for(int side=-1;side<=1;side+=2)
            {
                for(int bank=0;bank<BenchZ.Length;bank++) Bench(seats, rails, anchors, side, bank);
                for(float z=-8.8f;z<=9f;z+=.72f)
                {
                    if (Mathf.Abs(z)>8.3f) continue;
                    float x=side*.62f;
                    Cylinder(rails,"HandStrapMount",new Vector3(x,2.23f,z),new Vector3(x,2.30f,z),.026f,steel);
                    Box(rails,"WovenHangingStrap",new Vector3(x,2.08f,z),new Vector3(.028f,.28f,.015f),darkSteel,false);
                    var ring=MeshObject(rails,"IvoryHandGrip",RingMesh(.09f,.013f),white);
                    ring.localPosition=new Vector3(x,1.88f,z);
                    ring.localScale=new Vector3(1,1.14f,1);
                }
                Cylinder(rails,"ContinuousOverheadRail",new Vector3(side*.62f,2.25f,-9.3f),new Vector3(side*.62f,2.25f,9.3f),.024f,steel);
                foreach(float z in new[]{-7.25f,-2.45f,2.45f,7.25f})
                {
                    Cylinder(rails,"VerticalGrabPole",new Vector3(side*.77f,.06f,z),new Vector3(side*.77f,2.28f,z),.022f,steel,true);
                    Cylinder(rails,"PoleCeilingReturn",new Vector3(side*.77f,2.28f,z),new Vector3(side*1.39f,2.28f,z),.022f,steel);
                    Cylinder(rails,"PoleFootCollar",new Vector3(side*.77f,.015f,z),new Vector3(side*.77f,.045f,z),.055f,darkSteel);
                }
            }
            Ceiling(lighting);
            EndWall(shell, signs, rails, -1); EndWall(shell, signs, rails, 1);
            for(int i=0;i<6;i++)
            {
                float z=-8f+i*3.2f;
                var go=Group(lighting,"CeilingFill_"+(i+1)); go.localPosition=new Vector3(0,2.24f,z);
                var l=go.gameObject.AddComponent<Light>(); l.type=LightType.Point; l.range=4.6f;
                l.color=new Color(1,.94f,.82f); l.intensity=1.6f; l.shadows=LightShadows.None;
            }
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.38f,.41f,.44f);
            RenderSettings.ambientEquatorColor=new Color(.24f,.27f,.30f);
            RenderSettings.ambientGroundColor=new Color(.13f,.15f,.18f);
            RenderSettings.skybox=null; RenderSettings.fog=false;
            var player=Group(root,"FirstPersonPlayer"); player.localPosition=new Vector3(0,.04f,-6.1f);
            var cc=player.gameObject.AddComponent<CharacterController>(); cc.height=1.8f; cc.radius=.23f;
            cc.center=Vector3.up*.9f; cc.stepOffset=.15f; cc.skinWidth=.025f; cc.slopeLimit=45;
            var eye=Group(player,"Main Camera"); eye.localPosition=Vector3.up*1.63f;
            var cam=eye.gameObject.AddComponent<Camera>(); cam.tag="MainCamera";
            cam.fieldOfView=68; cam.nearClipPlane=.035f; cam.farClipPlane=55;
            cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.025f,.035f,.045f);
            cam.allowHDR=true; eye.gameObject.AddComponent<AudioListener>();
            var cameraData=cam.GetUniversalAdditionalCameraData(); cameraData.SetRenderer(renderer);
            cameraData.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            var controller=player.gameObject.AddComponent<SubwayFirstPersonController>(); controller.eye=eye;
            var spawn=Group(anchors,"PlayerSpawn"); spawn.localPosition=player.localPosition;
            var front=Group(anchors,"GangwayForward"); front.localPosition=new Vector3(0,0,9.5f);
            var rear=Group(anchors,"GangwayRear"); rear.localPosition=new Vector3(0,0,-9.5f);
            SetAisleWidth(root, 1.5f, DesiredAisleWidth);
            SetupSignMaterial(root);
            SetupScenePipeline(root,renderer);
            Selection.activeGameObject=root.gameObject;
            if(SceneView.lastActiveSceneView)
            {
                var sv=SceneView.lastActiveSceneView; sv.in2DMode=false;
                sv.LookAt(new Vector3(0,1.5f,-2),Quaternion.Euler(10,12,0),5f,false,true);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("3D subway saved: 54 seats, 8 sliding doors, first-person player. Click to capture mouse; WASD, Shift, Ctrl, E, Esc, Home.");
        }

        /// <summary>Moves each side assembly without stretching seats, doors or fittings.</summary>
        public static void SetAisleWidth(Transform root, float currentAisleWidth, float targetAisleWidth)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before changing car width.");
            if (!root || root.name != "Subway_3D_FirstPerson") throw new ArgumentException("Expected the 3D subway root.");
            if (targetAisleWidth < 1f) throw new ArgumentOutOfRangeException(nameof(targetAisleWidth));
            float offset = (targetAisleWidth - currentAisleWidth) * .5f;
            if (Mathf.Abs(offset) < .0001f) return;
            foreach (Transform group in root)
            {
                if (group.name.StartsWith("01_CarShell"))
                {
                    foreach (Transform part in group)
                    {
                        if (part.name == "RubberFloor" || part.name == "EndBulkhead") GrowAcrossCar(part, offset * 2f);
                        else ShiftOutward(part, offset);
                    }
                    Undo.RecordObject(group.gameObject, "Set subway aisle width");
                    group.name = "01_CarShell_19.6m_x_" + group.Find("RubberFloor").localScale.x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "m";
                }
                else if (group.name.StartsWith("02_Seating"))
                {
                    foreach (Transform bank in group) ShiftOutward(bank, offset, bank.name.StartsWith("Left_") ? -1 : 1);
                }
                else if (group.name.StartsWith("03_SlidingDoors") || group.name.StartsWith("08_IntegrationAnchors"))
                {
                    foreach (Transform part in group) ShiftOutward(part, offset);
                }
                else if (group.name.StartsWith("04_StainlessRails"))
                {
                    foreach (Transform part in group)
                        if (part.name != "GangwayDoorHandle") ShiftOutward(part, offset);
                }
                else if (group.name.StartsWith("05_RouteMaps"))
                {
                    foreach (Transform part in group)
                    {
                        var panel = part.Find("MapPanel");
                        if (panel) ShiftOutward(part, offset, panel.localPosition.x < 0 ? -1 : 1);
                        else ShiftOutward(part, offset);
                    }
                }
                else if (group.name.StartsWith("06_Ceiling"))
                {
                    foreach (Transform part in group)
                    {
                        if (part.name == "CentralCeiling" || part.name == "CeilingPanelSeam") GrowAcrossCar(part, offset * 2f);
                        else ShiftOutward(part, offset);
                    }
                }
                else if (group.name.StartsWith("07_Tunnel"))
                {
                    foreach (Transform part in group)
                    {
                        if (part.name == "ExteriorDarkDeck") GrowAcrossCar(part, offset * 2f);
                        else ShiftOutward(part, offset);
                    }
                }
            }
        }

        private static void ShiftOutward(Transform part, float offset, int side = 0)
        {
            if (side == 0) side = part.localPosition.x > 0 ? 1 : part.localPosition.x < 0 ? -1 : 0;
            if (side == 0) return;
            Undo.RecordObject(part, "Set subway aisle width");
            var position = part.localPosition;
            position.x += side * offset;
            part.localPosition = position;
        }

        private static void GrowAcrossCar(Transform part, float extraWidth)
        {
            Undo.RecordObject(part, "Set subway aisle width");
            var size = part.localScale;
            size.x += extraWidth;
            part.localScale = size;
        }

        private static void PrepareMaterials()
        {
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Art/Fonts/NanumGothic-Regular.ttf");
            ivory=Mat("WarmIvoryPanel","D5D6CC",.18f,.28f);
            steel=Mat("BrushedStainless","A9B5B8",.72f,.57f);
            darkSteel=Mat("CharcoalFrame","35474D",.48f,.35f);
            rubber=Mat("LightGreyRubberFloor","B9C0C1",0,.18f);
            seat=Mat("MetroBlueSeatFabric","3B5DB0",0,.24f);
            prioritySeat=Mat("PriorityRedSeatFabric","B95762",0,.24f);
            glass=Mat("TintedWindowGlass","102832",.5f,.88f);
            dividerGlass=Mat("ClearSeatDivider","9CC5CF",.05f,.72f);
            dividerGlass.color=new Color(.61f,.77f,.81f,.16f);
            dividerGlass.SetFloat("_Surface",1);dividerGlass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            dividerGlass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);dividerGlass.SetFloat("_ZWrite",0);
            dividerGlass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");dividerGlass.SetOverrideTag("RenderType","Transparent");
            dividerGlass.renderQueue=(int)RenderQueue.Transparent;
            black=Mat("BlackRubberSeal","121D22",0,.15f);
            gold=Mat("SuinBundangYellow","E9B937",.15f,.32f);
            white=Mat("IvoryHandlePlastic","ECEDE2",0,.34f);
            light=Mat("WarmLED","FFF2D5",0,.2f,new Color(1,.89f,.64f)*2);
            teal=Mat("TunnelServiceTeal","447E83",0,.2f,new Color(.08f,.25f,.27f));
            rubber.SetTexture("_BaseMap",NoiseTexture("FloorGrain",new Color(.70f,.73f,.74f),.21f));
            seat.SetTexture("_BaseMap",NoiseTexture("SeatWeave",Color.white,.10f));
            prioritySeat.SetTexture("_BaseMap",seat.GetTexture("_BaseMap"));
            rubber.mainTextureScale=new Vector2(3,18);
            meshCache.Clear();
        }
        private static Texture2D NoiseTexture(string name,Color color,float strength)
        {
            string path=Assets+"/Materials/"+name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(path); if(existing)return existing;
            var t=new Texture2D(256,256,TextureFormat.RGBA32,true);t.name=name;t.wrapMode=TextureWrapMode.Repeat;
            var random=new System.Random(412);var pixels=new Color[256*256];
            for(int y=0;y<256;y++) for(int x=0;x<256;x++)
            { float v=1-strength*(float)random.NextDouble(); if(name=="SeatWeave")v-=((x+y)%3==0?.045f:0);pixels[y*256+x]=color*v;pixels[y*256+x].a=1; }
            t.SetPixels(pixels);t.Apply();AssetDatabase.CreateAsset(t,path);return t;
        }
        private static Material Mat(string name,string hex,float metallic,float smoothness,Color? emission=null)
        {
            string path=Assets+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.color=Hex(hex);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smoothness);
            if(emission.HasValue){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission.Value);}
            EditorUtility.SetDirty(m);return m;
        }
        private static int PrepareRenderer()
        {
            const string path="Assets/Settings/SubwayFirstPersonRenderer.asset";
            var data=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if(!data){data=ScriptableObject.CreateInstance<UniversalRendererData>();data.name="SubwayFirstPersonRenderer";AssetDatabase.CreateAsset(data,path);}
            var pipeline=GraphicsSettings.currentRenderPipeline;
            var so=new SerializedObject(pipeline);var list=so.FindProperty("m_RendererDataList");
            int index=-1;for(int i=0;i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).objectReferenceValue==data)index=i;
            if(index<0){index=list.arraySize;list.arraySize++;list.GetArrayElementAtIndex(index).objectReferenceValue=data;so.ApplyModifiedProperties();}
            return index;
        }
        private static void Wall(Transform parent,Transform signs,int side,float min,float max)
        {
            float length=max-min,center=(min+max)*.5f;
            Box(parent,"WallLowerPanel",new Vector3(side*1.46f,.47f,center),new Vector3(.16f,.94f,length),ivory,true);
            Box(parent,"WindowHeader",new Vector3(side*1.46f,2.05f,center),new Vector3(.16f,.30f,length),ivory,true);
            int count=length>3?2:1;
            for(int i=0;i<count;i++)
            {
                float segment=length/count,z=min+segment*(i+.5f),width=segment-.24f;
                Box(parent,"WindowRubberSurround",new Vector3(side*1.449f,1.43f,z),new Vector3(.17f,1.04f,width+.08f),black,false,.065f);
                Box(parent,"BrushedWindowFrame",new Vector3(side*1.397f,1.43f,z),new Vector3(.075f,.99f,width),steel,false,.055f);
                Box(parent,"DarkTunnelWindow",new Vector3(side*1.35f,1.43f,z),new Vector3(.018f,.88f,width-.10f),glass,false,.04f);
                Box(parent,"WindowSill",new Vector3(side*1.32f,.956f,z),new Vector3(.10f,.035f,width+.08f),steel,false);
                Box(parent,"WallPillar",new Vector3(side*1.458f,1.43f,min+segment*i+.045f),new Vector3(.18f,1.06f,.09f),ivory,true);
                Box(parent,"WindowReflection",new Vector3(side*1.337f,1.81f,z),new Vector3(.007f,.015f,width-.25f),teal,false);
            }
            Box(parent,"WallEndPillar",new Vector3(side*1.458f,1.43f,max-.045f),new Vector3(.18f,1.06f,.09f),ivory,true);
        }
        private static void Bench(Transform parent,Transform rails,Transform anchors,int side,int bank)
        {
            int count=SeatCount[bank];float z=BenchZ[bank],length=count*.46f;
            var group=Group(parent,(side<0?"Left":"Right")+"_Bench_"+(bank+1));
            Box(group,"SeatMetalBase",new Vector3(side*1.05f,.38f,z),new Vector3(.59f,.12f,length+.04f),steel,true,.035f);
            Box(group,"RecessedUnderSeat",new Vector3(side*1.15f,.22f,z),new Vector3(.33f,.36f,length-.05f),darkSteel,true);
            for(int i=0;i<count;i++)
            {
                float position=z+(i-(count-1)*.5f)*.46f;var fabric=bank==0||bank==4?prioritySeat:seat;
                Box(group,"SeatCushion_"+(i+1),new Vector3(side*1.043f,.505f,position),new Vector3(.53f,.16f,.443f),fabric,true,.055f);
                var back=Box(group,"Backrest_"+(i+1),new Vector3(side*1.29f,.765f,position),new Vector3(.14f,.48f,.443f),fabric,true,.055f);
                back.localRotation=Quaternion.Euler(0,0,-side*6);
                Box(group,"CushionSeam",new Vector3(side*.775f,.53f,position),new Vector3(.008f,.013f,.34f),gold,false);
                var spot=Group(anchors,(side<0?"Left":"Right")+"_Seat_"+bank+"_"+i);
                spot.localPosition=new Vector3(side*1.02f,.60f,position);spot.localRotation=Quaternion.Euler(0,-side*90,0);
            }
            foreach(int end in new[]{-1,1})
            {
                float pos=z+end*(length*.5f+.03f);
                Box(group,"SeatDividerGlass",new Vector3(side*1.075f,.895f,pos),new Vector3(.60f,.67f,.035f),dividerGlass,false,.013f);
                Cylinder(rails,"SeatEndArmrest",new Vector3(side*.78f,.76f,pos),new Vector3(side*1.36f,.76f,pos),.022f,steel);
                Cylinder(rails,"SeatEndGrabRail",new Vector3(side*.78f,.57f,pos),new Vector3(side*.78f,1.15f,pos),.022f,steel,true);
                Cylinder(rails,"DividerTopRail",new Vector3(side*.78f,1.15f,pos),new Vector3(side*1.36f,1.15f,pos),.022f,steel);
            }
        }
        private static void CreateDoor(Transform parent,Transform signs,Transform anchors,int side,float z)
        {
            var d=Group(parent,(side<0?"Left":"Right")+"_Door_"+Array.IndexOf(DoorZ,z));d.localPosition=new Vector3(side*1.45f,0,z);
            Box(d,"DoorTopTrack",new Vector3(0,2.13f,0),new Vector3(.22f,.13f,1.40f),steel,false);
            foreach(int end in new[]{-1,1})Box(d,"DoorJamb",new Vector3(0,1.08f,end*.69f),new Vector3(.23f,2.15f,.065f),steel,true);
            Box(d,"RibbedThreshold",new Vector3(-side*.06f,.012f,0),new Vector3(.34f,.024f,1.34f),steel,false);
            Box(d,"StandBehindYellowLine",new Vector3(-side*.29f,.009f,0),new Vector3(.05f,.012f,1.42f),gold,false);
            for(int i=0;i<10;i++)Box(d,"ThresholdGroove",new Vector3(-side*.055f,.027f,-.59f+i*.13f),new Vector3(.29f,.006f,.013f),darkSteel,false);
            Transform negative=null,positive=null;
            foreach(int end in new[]{-1,1})
            {
                var leaf=Group(d,end<0?"NegativeSlidingLeaf":"PositiveSlidingLeaf");leaf.localPosition=new Vector3(0,0,end*.326f);
                Box(leaf,"StainlessDoorPanel",new Vector3(0,1.04f,0),new Vector3(.095f,2.05f,.645f),steel,true,.015f);
                Box(leaf,"WindowGasket",new Vector3(-side*.052f,1.44f,0),new Vector3(.023f,.94f,.43f),black,false,.05f);
                Box(leaf,"DoorWindow",new Vector3(-side*.07f,1.44f,0),new Vector3(.016f,.84f,.34f),glass,false,.04f);
                Box(leaf,"RubberCenterSeal",new Vector3(-side*.018f,1.06f,-end*.318f),new Vector3(.12f,2.06f,.022f),black,false);
                Box(leaf,"DoorWarningBand",new Vector3(-side*.055f,.90f,0),new Vector3(.018f,.055f,.57f),gold,false);
                Box(leaf,"DoorLowerInset",new Vector3(-side*.051f,.44f,0),new Vector3(.012f,.63f,.51f),ivory,false,.018f);
                Label(leaf,"손 끼임 주의",new Vector3(-side*.077f,.84f,0),Quaternion.Euler(0,side>0?90:-90,0),.038f,Hex("35474D"));
                if(end<0)negative=leaf;else positive=leaf;
            }
            var behavior=d.gameObject.AddComponent<SubwayDoor3D>();behavior.negativeLeaf=negative;behavior.positiveLeaf=positive;
            var anchor=Group(anchors,(side<0?"Left":"Right")+"_DoorAnchor_"+Array.IndexOf(DoorZ,z));anchor.localPosition=new Vector3(side*1.25f,0,z);
            SideLabel(signs,"출입문  |  DOOR",side,z,2.15f,.070f,Hex("35474D"),1.315f);
            if(Array.IndexOf(DoorZ,z)%2==0) RouteMap(signs,side,z);
            else SideLabel(signs,"내리실 문은 오른쪽입니다",side,z,2.40f,.072f,Hex("35474D"));
        }
        private static void RouteMap(Transform parent,int side,float z)
        {
            var map=Group(parent,"SuinBundang_RouteMap");
            Box(map,"MapPanel",new Vector3(side*1.366f,2.405f,z),new Vector3(.022f,.20f,2.24f),white,false);
            Box(map,"YellowRouteLine",new Vector3(side*1.349f,2.399f,z),new Vector3(.015f,.018f,1.92f),gold,false);
            string[] names={"왕십리","수서","복정","가천대"};
            for(int i=0;i<4;i++)
            {
                float pos=z-.75f+i*.5f;
                var dot=GameObject.CreatePrimitive(PrimitiveType.Sphere);dot.name="StationNode";dot.transform.SetParent(map,false);
                dot.transform.localPosition=new Vector3(side*1.33f,2.399f,pos);dot.transform.localScale=Vector3.one*.04f;
                dot.GetComponent<Renderer>().sharedMaterial=i==2?darkSteel:gold;UnityEngine.Object.DestroyImmediate(dot.GetComponent<Collider>());
                SideLabel(map,names[i],side,pos,2.454f,.055f,Hex("35474D"),1.327f);
            }
        }
        private static void Ceiling(Transform parent)
        {
            Box(parent,"CentralCeiling",new Vector3(0,2.61f,0),new Vector3(1.75f,.10f,19.6f),ivory,true);
            foreach(int side in new[]{-1,1})
            {
                var c=Box(parent,"AngledCeilingHaunch",new Vector3(side*1.16f,2.48f,0),new Vector3(.65f,.08f,19.6f),ivory,true);
                c.localRotation=Quaternion.Euler(0,0,-side*25);
                Box(parent,"ContinuousLEDHousing",new Vector3(side*.84f,2.495f,0),new Vector3(.17f,.10f,19.45f),steel,false);
                Box(parent,"FrostedLEDStrip",new Vector3(side*.84f,2.437f,0),new Vector3(.115f,.025f,19.3f),light,false);
                for(float z=-9.45f;z<9.5f;z+=.22f)
                    Box(parent,"HVACVentSlot",new Vector3(side*.41f,2.550f,z),new Vector3(.19f,.008f,.016f),darkSteel,false);
            }
            for(float z=-9.8f;z<=9.8f;z+=2.45f)
                Box(parent,"CeilingPanelSeam",new Vector3(0,2.548f,z),new Vector3(1.48f,.008f,.011f),steel,false);
        }
        private static void EndWall(Transform parent,Transform signs,Transform rails,int end)
        {
            float z=end*9.8f;
            Box(parent,"EndBulkhead",new Vector3(0,1.3f,z),new Vector3(2.9f,2.6f,.12f),ivory,true);
            Box(parent,"GangwayRubberFrame",new Vector3(0,1.16f,z-end*.078f),new Vector3(1.12f,2.31f,.08f),black,false,.075f);
            Box(parent,"GangwayMetalDoor",new Vector3(0,1.13f,z-end*.13f),new Vector3(.94f,2.20f,.06f),steel,false,.03f);
            Box(parent,"GangwayWindowSeal",new Vector3(0,1.42f,z-end*.17f),new Vector3(.64f,1.18f,.025f),black,false,.07f);
            Box(parent,"GangwayWindow",new Vector3(0,1.42f,z-end*.19f),new Vector3(.54f,1.06f,.018f),glass,false,.055f);
            Cylinder(rails,"GangwayDoorHandle",new Vector3(.38f,.86f,z-end*.21f),new Vector3(.38f,1.15f,z-end*.21f),.021f,steel);
            var display=Box(signs,"PassengerDestinationDisplay",new Vector3(0,2.40f,z-end*.10f),new Vector3(1.68f,.24f,.10f),black,false,.025f);
            Label(signs,"이번 역  복정  /  Bokjeong",new Vector3(0,2.41f,z-end*.164f),Quaternion.Euler(0,end>0?0:180,0),.065f,Hex("F4C44A"));
            foreach(int side in new[]{-1,1})
            {
                Box(signs,"PrioritySeatPlaque",new Vector3(side*.93f,1.86f,z-end*.072f),new Vector3(.55f,.38f,.024f),prioritySeat,false,.03f);
                Label(signs,"교통약자\n배려석",new Vector3(side*.93f,1.87f,z-end*.091f),Quaternion.Euler(0,end>0?0:180,0),.080f,Color.white);
                Box(signs,"EquipmentServicePanel",new Vector3(side*.98f,.38f,z-end*.082f),new Vector3(.52f,.62f,.035f),steel,false,.015f);
                for(int i=0;i<9;i++)Box(signs,"ServiceVent",new Vector3(side*.98f,.25f+i*.035f,z-end*.106f),new Vector3(.38f,.01f,.008f),darkSteel,false);
            }
            Label(signs,"수인·분당선",new Vector3(0,2.17f,z-end*.178f),Quaternion.Euler(0,end>0?0:180,0),.065f,Hex("35474D"));
        }
        private static Transform Group(Transform parent,string name)
        { var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform; }
        private static Transform Box(Transform parent,string name,Vector3 pos,Vector3 size,Material mat,bool collision,float radius=0)
        {
            Transform t;
            if(radius>0)t=MeshObject(parent,name,RoundedBox(size,radius),mat);
            else {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;t=go.transform;if(!collision)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());}
            t.localPosition=pos;
            if(radius>0&&collision){var c=t.gameObject.AddComponent<BoxCollider>();c.size=size;}
            return t;
        }
        private static Transform MeshObject(Transform parent,string name,Mesh mesh,Material mat)
        {var t=Group(parent,name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;return t;}
        private static void Cylinder(Transform parent,string name,Vector3 a,Vector3 b,float radius,Material mat,bool collision=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=(a+b)*.5f;go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
            go.transform.localScale=new Vector3(radius*2,(b-a).magnitude*.5f,radius*2);go.GetComponent<Renderer>().sharedMaterial=mat;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            if(collision){var c=go.AddComponent<CapsuleCollider>();c.radius=.5f;c.height=2;}
        }
        private static Mesh RoundedBox(Vector3 size,float radius)
        {
            string key="Rounded_"+size.x.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+size.y.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+size.z.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+radius.ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
            if(meshCache.TryGetValue(key,out var cached))return cached;
            string path=Assets+"/Meshes/"+key+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved){meshCache[key]=saved;return saved;}
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uvs=new List<Vector2>();var triangles=new List<int>();
            Vector3 h=size*.5f;float r=Mathf.Min(radius,Mathf.Min(h.x,Mathf.Min(h.y,h.z))*.95f);
            Vector3 inner=h-Vector3.one*r;const int n=8;
            Vector3[] faceNormals={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            foreach(var normal in faceNormals)
            {
                Vector3 u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.up;
                Vector3 v=Vector3.Cross(normal,u);int offset=vertices.Count;
                for(int y=0;y<=n;y++)for(int x=0;x<=n;x++)
                {
                    var p=Vector3.Scale(normal+u*(2f*x/n-1)+v*(2f*y/n-1),h);
                    var q=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));
                    var direction=(p-q).normalized;vertices.Add(q+direction*r);normals.Add(direction);uvs.Add(new Vector2((float)x/n,(float)y/n));
                }
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {int a=offset+y*(n+1)+x,b=a+1,c=a+n+1,d=c+1;triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(b);triangles.Add(d);triangles.Add(c);}
            }
            var mesh=new Mesh{name=key};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);meshCache[key]=mesh;return mesh;
        }
        private static Mesh RingMesh(float radius,float tube)
        {
            const string key="HandGripRing";if(meshCache.TryGetValue(key,out var cached))return cached;
            string path=Assets+"/Meshes/"+key+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved){meshCache[key]=saved;return saved;}
            const int segments=32,sides=8;var v=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<=segments;i++)for(int j=0;j<=sides;j++)
            {float a=i*Mathf.PI*2/segments,b=j*Mathf.PI*2/sides;var radial=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);var n=radial*Mathf.Cos(b)+Vector3.forward*Mathf.Sin(b);v.Add(radial*radius+n*tube);normals.Add(n);}
            for(int i=0;i<segments;i++)for(int j=0;j<sides;j++){int a=i*(sides+1)+j,b=a+sides+1;triangles.Add(a);triangles.Add(b);triangles.Add(a+1);triangles.Add(a+1);triangles.Add(b);triangles.Add(b+1);}
            var mesh=new Mesh{name=key};mesh.SetVertices(v);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);meshCache[key]=mesh;return mesh;
        }
        private static void SideLabel(Transform parent,string text,int side,float z,float y,float size,Color color,float x=1.35f)
        {Label(parent,text,new Vector3(side*x,y,z),Quaternion.Euler(0,side>0?90:-90,0),size,color);}
        private static void Label(Transform parent,string text,Vector3 pos,Quaternion rotation,float size,Color color)
        {
            var t=Group(parent,"Sign_"+text.Replace('\n',' '));t.localPosition=pos;t.localRotation=rotation;
            var tm=t.gameObject.AddComponent<TextMesh>();tm.text=text;tm.font=font;tm.fontSize=64;
            tm.characterSize=size*10f/64f;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.color=color;
            tm.GetComponent<MeshRenderer>().sharedMaterial=font.material;
        }
        private static void SetupSignMaterial(Transform root)
        {
            var texts=root.GetComponentsInChildren<TextMesh>();
            string all="";foreach(var text in texts)all+=text.text;
            font.RequestCharactersInTexture(all,64,FontStyle.Normal);
            string path=Assets+"/Materials/DepthTestedSignFont.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("SubwayCarry/World Sign"));AssetDatabase.CreateAsset(mat,path);}
            mat.shader=Shader.Find("SubwayCarry/World Sign");
            mat.SetTexture("_BaseMap",font.material.mainTexture);mat.color=Color.white;
            mat.renderQueue=2450;
            foreach(var text in texts)text.GetComponent<MeshRenderer>().sharedMaterial=mat;
            var sync=root.gameObject.AddComponent<SubwaySignFont3D>();sync.font=font;sync.signMaterial=mat;
            EditorUtility.SetDirty(mat);
        }
        private static void SetupScenePipeline(Transform root,int renderer)
        {
            const string path="Assets/Settings/SubwayFirstPersonPipeline.asset";
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if(!pipeline)
            {
                pipeline=UnityEngine.Object.Instantiate((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline);
                pipeline.name="SubwayFirstPersonPipeline";AssetDatabase.CreateAsset(pipeline,path);
            }
            var so=new SerializedObject(pipeline);so.FindProperty("m_DefaultRendererIndex").intValue=renderer;
            so.FindProperty("m_MSAA").intValue=4;
            so.FindProperty("m_AdditionalLightsPerObjectLimit").intValue=8;
            so.ApplyModifiedProperties();
            var overridePipeline=root.gameObject.AddComponent<SubwaySceneRenderPipeline3D>();
            overridePipeline.originalPipeline=GraphicsSettings.defaultRenderPipeline;
            overridePipeline.pipeline=pipeline;overridePipeline.Apply();
        }
        private static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out var c);return c;}
        private static void EnsureFolder(string path)
        {if(AssetDatabase.IsValidFolder(path))return;int i=path.LastIndexOf('/');EnsureFolder(path.Substring(0,i));AssetDatabase.CreateFolder(path.Substring(0,i),path.Substring(i+1));}
    }
}
#endif


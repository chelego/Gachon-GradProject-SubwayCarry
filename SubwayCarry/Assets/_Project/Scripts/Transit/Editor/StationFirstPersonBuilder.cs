#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SubwayCarry.Transit.Editor
{
    /// <summary>Reconstructs original station sprite silhouettes as lit, metre-scale 3D geometry.</summary>
    public static class StationFirstPersonBuilder
    {
        private const string Art = "Assets/_Project/Art/StationsSprite3D";
        private const string Sprites = "Assets/_Project/Art/Sprites/";
        private const string Scenes = "Assets/_Project/Scenes/";
        public const float PlatformLength = 76.8f;
        private const float HalfLength = PlatformLength * .5f;
        private const float GachonBenchWidthScale = 1.872f;
        private const float GachonBenchLengthScale = 1.5525f;
        private const float GachonColumnSize = 1.16f; // Cladding and base span one 1.2m floor tile.
        private static Material floor, concrete, ceramic, steel, seal, glass, wood, yellow, tactileStud, blue, orange, black, led, red, green, signFont;
        private static Material divider, grout, productWhite, woodEnd, vendingGraphic;
        private static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        private static Font font;
        private static Transform root, anchors;
        private static readonly List<Transform> doorAnchors = new List<Transform>();
        private static readonly List<Transform> landingAnchors = new List<Transform>();
        private static readonly List<Transform> seatAnchors = new List<Transform>();
        private static readonly float[] DoorPositions = MakeDoorPositions();

        private static float[] MakeDoorPositions()
        { var positions=new float[16];for(int i=0;i<positions.Length;i++)positions[i]=(i-7.5f)*4.8f;return positions; }

        [MenuItem("SubwayCarry/Stations/Create GachonUniv and Wangsimni 3D (missing only)")]
        public static void BuildMissing()
        {
            RequireSaved(); Prepare();
            if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(Scenes+"GachonUniv_3D.unity"))Build(false);
            if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(Scenes+"Wangsimni_3D.unity"))Build(true);
        }

        public static void RebuildGenerated()
        { RequireSaved(); Prepare(); Build(false); Build(true); }

        private static void RequireSaved()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before authoring.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save the open scene before authoring station scenes.");
        }

        private static void Prepare()
        {
            EnsureFolder(Art);EnsureFolder(Art+"/Materials");EnsureFolder(Art+"/Textures");EnsureFolder(Art+"/Meshes");
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Art/Fonts/NanumGothic-Regular.ttf");
            if(!font)throw new InvalidOperationException("The existing Korean font is missing.");
            // Station assets are authored independently from the train's furniture and materials.
            concrete=Mat("StationWarmPanel",Hex("D6D5CB"),0,.31f);
            ceramic=Mat("SpriteWhitePillar",Hex("E4E5E2"),0,.30f);
            steel=Mat("SpriteStainlessTrim",Hex("B1BEC1"),.64f,.48f);
            seal=Mat("SpriteGraphiteFrame",Hex("282D2D"),.30f,.28f);
            glass=Mat("SpriteScreenTealGlass",Hex("263F46"),.22f,.64f);
            wood=Mat("SpriteGoldenWood",Hex("C59D64"),0,.30f);wood.SetTexture("_BaseMap",WoodGrain());
            woodEnd=Mat("SpriteWoodEndGrain",Hex("AF8752"),0,.25f);woodEnd.SetTexture("_BaseMap",WoodGrain());
            yellow=Mat("SpriteSafetyYellow",Hex("EDC72F"),.02f,.30f);
            led=Mat("WarmStationDiffuser",Hex("FFF4D4"),0,.30f,new Color(1,.91f,.74f)*2f);
            divider=Mat("DisplayGlass",new Color(.69f,.82f,.82f,.11f),.02f,.63f);
            divider.SetFloat("_Surface",1);divider.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);divider.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            divider.SetFloat("_ZWrite",0);divider.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");divider.renderQueue=3000;
            floor=Mat("WarmGreyStationTerrazzo",Hex("9A9A8F"),0,.30f);
            floor.SetTexture("_BaseMap",StoneGrain());floor.SetTextureScale("_BaseMap",Vector2.one);
            grout=Mat("RecessedTileJoint",Hex("646A64"),0,.22f);
            tactileStud=Mat("TactileStuds",Hex("BA952F"),.08f,.27f);
            blue=Mat("SpriteVendingBlue",Hex("347793"),.10f,.36f);
            orange=Mat("SpriteVendingOrange",Hex("E3803B"),.03f,.31f);
            productWhite=Mat("SpriteVendingCream",Hex("DBD8C8"),0,.30f);
            vendingGraphic=Mat("OriginalVendingSideArtwork",Color.white,0,.32f);
            vendingGraphic.SetTexture("_BaseMap",VendingSideArtwork());
            black=Mat("TrackDark",Hex("111B20"),0,.16f);
            red=Mat("DoorIndicatorRed",Hex("D8462F"),0,.2f,new Color(.65f,.08f,.015f));
            green=Mat("ExitGreen",Hex("38836A"),0,.2f,new Color(.08f,.25f,.12f));
            string fontPath=Art+"/Materials/StationWorldSignFont.mat";
            signFont=AssetDatabase.LoadAssetAtPath<Material>(fontPath);
            if(!signFont)
            {
                var shader=Shader.Find("SubwayCarry/World Sign");
                if(!shader)throw new InvalidOperationException("The existing 3D world sign shader is missing.");
                signFont=new Material(shader);AssetDatabase.CreateAsset(signFont,fontPath);
            }
            signFont.SetColor("_BaseColor",Color.white);signFont.renderQueue=2450;
            AssetDatabase.SaveAssets();
        }

        private static void Build(bool island)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string sceneName=island?"Wangsimni_3D":"GachonUniv_3D";
            string korean=island?"왕십리":"가천대";
            string english=island?"Wangsimni":"Gachon Univ.";
            float halfWidth=island?6f:4f;
            root=Group(null,sceneName);Undo.RegisterCreatedObjectUndo(root.gameObject,"Create 3D station");
            var architecture=Group(root,"01_PlatformArchitecture");
            var platformDoors=Group(root,"02_PlatformScreenDoors");
            var furnishings=Group(root,"03_SpriteBasedWoodBenchesAndVendingMachines");
            var circulation=Group(root,"04_WalkableEscalatorsAndLandings");
            var signs=Group(root,"05_WayfindingAndStationIdentity");
            var lights=Group(root,"06_CeilingAndLighting");
            var tracks=Group(root,"07_TrackAndTunnelBackdrop");
            anchors=Group(root,"08_IntegrationAnchors");
            doorAnchors.Clear();landingAnchors.Clear();seatAnchors.Clear();
            Box(architecture,"PlatformConcreteSlab_76_8m",new Vector3(0,-.14f,0),new Vector3(halfWidth*2,.28f,PlatformLength),concrete,true);
            FloorSurface(architecture,halfWidth);
            ScreenLine(platformDoors,tracks,signs,-1,halfWidth,korean,english);
            if(island)ScreenLine(platformDoors,tracks,signs,1,halfWidth,korean,english);
            else
            {
                Box(architecture,"IvoryBackWall",new Vector3(4.06f,2.62f,0),new Vector3(.20f,5.25f,PlatformLength),ceramic,true);
                Box(architecture,"BrushedSteelSkirting",new Vector3(3.945f,.15f,0),new Vector3(.045f,.30f,PlatformLength-.2f),steel,false);
                Box(architecture,"LineIdentityBand",new Vector3(3.925f,2.45f,0),new Vector3(.02f,.055f,PlatformLength-.2f),yellow,false);
                for(float z=-HalfLength+.6f;z<HalfLength;z+=1.2f)
                {
                    Rounded(architecture,"BeveledIvoryWallPanel",new Vector3(3.936f,1.40f,z),new Vector3(.038f,2.13f,1.18f),.016f,ceramic,false);
                    Box(architecture,"RecessedWallJoint",new Vector3(3.94f,3.83f,z+.6f),new Vector3(.012f,2.38f,.012f),seal,false);
                }
                foreach(float z in new[]{-20f,-8f,4f,16f})WallPoster(signs,3.90f,z,"가천대  GACHON UNIV.","수인분당선  ·  SUIN-BUNDANG LINE");
            }
            foreach(int end in new[]{-1,1})
            {
                Box(architecture,"EndWall",new Vector3(0,2.62f,end*(HalfLength+.08f)),new Vector3(halfWidth*2,5.25f,.16f),ceramic,true);
                Box(architecture,"EndWallBase",new Vector3(0,.16f,end*(HalfLength-.02f)),new Vector3(halfWidth*2,.32f,.06f),steel,false);
                var nameSign=Group(signs,"EndStationName");nameSign.localPosition=new Vector3(0,2.4f,end*(HalfLength-.04f));
                nameSign.localRotation=Quaternion.Euler(0,end>0?0:180,0);
                Rounded(nameSign,"StationNamePanel",Vector3.zero,new Vector3(3.6f,.92f,.06f),.025f,seal,false);
                Box(nameSign,"YellowIdentifier",new Vector3(-1.65f,0,-.037f),new Vector3(.09f,.83f,.01f),yellow,false);
                Label(nameSign,korean,new Vector3(0,.13f,-.04f),Quaternion.identity,.27f,Color.white);
                Label(nameSign,english,new Vector3(0,-.18f,-.04f),Quaternion.identity,.115f,Hex("ECEADF"));
            }
            if(island)
            {
                // Leave two 1.2m floor tiles between the benches in the widened column bay.
                foreach(float z in new[]{-18f,-9.627f,-2.373f,6f,18f})
                {
                    Bench(furnishings,new Vector3(0,0,z),3.8f,false);
                }
                foreach(float z in new[]{-24f,-13.5f,1.5f,12f,24f})Column(architecture,new Vector3(0,0,z),.88f,korean,english);
                // Cabinet backs meet the east column panels; displays face the open aisle.
                VendingMachine(furnishings,new Vector3(.94f,0,1.5f),-90f);
                VendingMachine(furnishings,new Vector3(.94f,0,-13.5f),-90f);
                foreach(float z in new[]{-34f,26f})foreach(int side in new[]{-1,1})
                    Escalator(circulation,new Vector3(side*1.05f,0,z),6.3f,2.65f,"ExitEscalator_"+(z<0?"South":"North")+"_"+side);
                foreach(float z in new[]{-25f,-14.5f,.5f,11f,23f})
                    HangingSign(signs,new Vector3(0,3.50f,z),korean,english,"환승 / 출구  ↑",halfWidth);
                TransferTotem(signs,new Vector3(-1.15f,0,24f));
            }
            else
            {
                // Keep the wider seat rim flush with the finished wall panel at x=3.917.
                foreach(float z in new[]{-20f,-8f,4f,16f})Bench(furnishings,new Vector3(3.917f-.395f*GachonBenchWidthScale,0,z),4.7f,true);
                // The rear column cladding touches the finished wall at x=3.917.
                foreach(float z in new[]{-26f,-14f,-2f,10f,22f,34f})Column(architecture,new Vector3(3.917f-(GachonColumnSize*.5f+.02f),0,z),GachonColumnSize,korean,english);
                // Cabinet back meets the nearest column; the display faces the open west aisle.
                VendingMachine(furnishings,new Vector3(3.917f-GachonColumnSize-.04f-.48f,0,-2f),90f);
                foreach(float z in new[]{-35f,25f})Escalator(circulation,new Vector3(2.05f,0,z),6.4f,2.65f,"ExitEscalator_"+(z<0?"South":"North"));
                foreach(float z in new[]{-27f,-15f,-3f,9f,21f,33f})
                    HangingSign(signs,new Vector3(-.75f,3.50f,z),korean,english,"나가는 곳  ↗",halfWidth);
            }
            Ceiling(lights,halfWidth,island);
            var spawn=Group(anchors,"PlayerSpawn");
            // Wangsimni starts beyond the south escalator so the platform is visible immediately.
            spawn.localPosition=island?new Vector3(3.2f,.04f,-22.5f):new Vector3(-.45f,.04f,-31.5f);
            spawn.localRotation=Quaternion.Euler(0,island?-12f:-9f,0);
            CreatePlayer(spawn);
            var layout=root.gameObject.AddComponent<StationFirstPersonLayout>();
            layout.referenceScene=Scenes+(island?"Wangsimni":"GachonUniv")+".unity";
            layout.stationName=korean;layout.islandPlatform=island;layout.playerSpawn=spawn;
            layout.platformLength=PlatformLength;layout.platformWidth=halfWidth*2;
            layout.platformDoors=doorAnchors.ToArray();layout.escalatorLandings=landingAnchors.ToArray();layout.seatPositions=seatAnchors.ToArray();
            SetupFont();SetupPipeline();
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.40f,.40f,.38f);RenderSettings.ambientEquatorColor=new Color(.27f,.28f,.28f);RenderSettings.ambientGroundColor=new Color(.15f,.16f,.17f);
            RenderSettings.skybox=null;RenderSettings.fog=false;
            // Fixed architecture batches separately from moving doors and the player.
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                if(!renderer.GetComponentInParent<SubwayDoor3D>()&&!renderer.GetComponent<TextMesh>())
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,Scenes+sceneName+".unity");
            Selection.activeGameObject=root.gameObject;
            var view=SceneView.lastActiveSceneView;
            if(view){view.in2DMode=false;view.LookAt(new Vector3(0,1.7f,-4),Quaternion.Euler(5,-8,0),7f,false,true);view.Repaint();}
            Debug.Log("Saved "+sceneName+": "+PlatformLength+"m platform, sprite-derived 3D furniture, "+doorAnchors.Count+" screen doors, "+landingAnchors.Count+" walkable escalators, first-person player.");
        }

        private static void FloorSurface(Transform parent,float halfWidth)
        {
            var mesh=new Mesh{name="TerrazzoFloor_"+halfWidth+"_76_8"};
            mesh.vertices=new[]{new Vector3(-halfWidth,.006f,-HalfLength),new Vector3(-halfWidth,.006f,HalfLength),new Vector3(halfWidth,.006f,HalfLength),new Vector3(halfWidth,.006f,-HalfLength)};
            mesh.uv=new[]{Vector2.zero,new Vector2(0,PlatformLength),new Vector2(halfWidth*2,PlatformLength),new Vector2(halfWidth*2,0)};
            mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();
            MeshObject(parent,"GraphiteTerrazzoFloor",SaveMesh(mesh),floor);
            for(float z=-HalfLength;z<HalfLength;z+=1.2f)
                Box(parent,"RecessedFloorJoint",new Vector3(0,.008f,z),new Vector3(halfWidth*2,.002f,.012f),grout,false);
            for(float x=-halfWidth+.8f;x<halfWidth;x+=1.2f)
                Box(parent,"RecessedFloorJoint",new Vector3(x,.008f,0),new Vector3(.012f,.002f,PlatformLength),grout,false);
        }

        private static void ScreenLine(Transform parent,Transform tracks,Transform signs,int side,float width,string korean,string english)
        {
            float x=side*width;
            var line=Group(parent,(side<0?"Left":"Right")+"_ScreenDoorLine");
            line.localPosition=new Vector3(x,0,0);
            // The screen doors can be inspected, while a rail-side boundary keeps the player on the platform.
            var boundary=Group(line,"TrackSafetyBoundary");boundary.localPosition=new Vector3(side*.16f,1.4f,0);
            boundary.gameObject.AddComponent<BoxCollider>().size=new Vector3(.10f,2.8f,PlatformLength);
            Box(line,"LowerScreenBase",new Vector3(0,.075f,0),new Vector3(.19f,.15f,PlatformLength),seal,false);
            Box(line,"IvoryScreenHeader",new Vector3(0,2.80f,0),new Vector3(.27f,.44f,PlatformLength),concrete,false);
            Box(line,"BrushedSteelHeaderTrim",new Vector3(-side*.15f,2.57f,0),new Vector3(.025f,.04f,PlatformLength),steel,false);
            Box(line,"SuinBundangYellowHeader",new Vector3(-side*.143f,2.625f,0),new Vector3(.015f,.035f,PlatformLength-.05f),yellow,false);
            Box(line,"ContinuousCanopyLED",new Vector3(-side*.15f,3.13f,0),new Vector3(.022f,.027f,PlatformLength-.1f),led,false);
            Box(line,"IvoryUpperCanopy",new Vector3(side*.025f,4.1f,0),new Vector3(.21f,2.04f,PlatformLength),concrete,false);
            Box(line,"UpperCanopySteelTrim",new Vector3(-side*.09f,3.18f,0),new Vector3(.045f,.05f,PlatformLength),steel,false);
            for(float z=-HalfLength+.6f;z<HalfLength;z+=1.2f)
            {
                Box(line,"UpperCanopyPanelJoint",new Vector3(-side*.085f,4.1f,z+.6f),new Vector3(.019f,1.82f,.009f),seal,false);
                for(int slot=0;slot<4;slot++)Box(line,"CanopyVent",new Vector3(-side*.09f,4.79f+slot*.05f,z),new Vector3(.018f,.018f,.62f),seal,false);
            }
            float last=-HalfLength;
            for(int i=0;i<DoorPositions.Length;i++)
            {
                float z=DoorPositions[i];FixedGlass(line,side,last,z-.84f);ScreenDoor(line,side,z,i+1,korean,english);
                last=z+.84f;
            }
            FixedGlass(line,side,last,HalfLength);
            Box(root,"TactileStrip",new Vector3(x-side*.59f,.014f,0),new Vector3(.80f,.028f,PlatformLength-.05f),yellow,false);
            TactileStuds(parent,x-side*.59f);
            Box(root,"StandBehindYellowLine",new Vector3(x-side*1.08f,.013f,0),new Vector3(.055f,.018f,PlatformLength-.1f),yellow,false);
            Box(tracks,"TrackBed",new Vector3(x+side*1.9f,-.79f,0),new Vector3(3.8f,.12f,PlatformLength+1),black,false);
            foreach(float offset in new[]{1.15f,2.45f})
                Box(tracks,"Rail",new Vector3(x+side*offset,-.47f,0),new Vector3(.09f,.16f,PlatformLength+1),steel,false);
            for(float z=-HalfLength-.1f;z<HalfLength+.5f;z+=.65f)
                Box(tracks,"RailSleeper",new Vector3(x+side*1.8f,-.63f,z),new Vector3(2.8f,.12f,.18f),seal,false);
            Box(tracks,"TunnelWall",new Vector3(x+side*3.9f,1.9f,0),new Vector3(.20f,5.2f,PlatformLength+1.2f),seal,false);
            for(float z=-HalfLength;z<HalfLength+.5f;z+=2.4f)
            {
                Box(tracks,"TunnelServiceRib",new Vector3(x+side*3.76f,1.9f,z),new Vector3(.10f,5.2f,.10f),black,false);
                Box(tracks,"TunnelMarker",new Vector3(x+side*3.69f,1.1f,z),new Vector3(.02f,.04f,.32f),led,false);
            }
        }

        private static void FixedGlass(Transform parent,int side,float min,float max)
        {
            if(max-min<.05f)return;
            int count=Mathf.Max(1,Mathf.RoundToInt((max-min)/1.45f));float step=(max-min)/count;
            for(int i=0;i<count;i++)
            {
                float z=min+(i+.5f)*step;
                Rounded(parent,"TealFixedScreenGlass",new Vector3(0,1.38f,z),new Vector3(.055f,2.34f,step-.07f),.024f,glass,true);
                foreach(int end in new[]{-1,1})
                    Box(parent,"FixedScreenMullion",new Vector3(-side*.025f,1.39f,z+end*(step-.035f)*.5f),new Vector3(.13f,2.39f,.055f),steel,false);
                Box(parent,"GlassLowerRail",new Vector3(-side*.035f,.24f,z),new Vector3(.11f,.07f,step),steel,false);
                Box(parent,"GlassUpperRail",new Vector3(-side*.035f,2.54f,z),new Vector3(.11f,.08f,step),steel,false);
                Box(parent,"SoftGlassReflection",new Vector3(-side*.033f,2.36f,z),new Vector3(.009f,.014f,step-.18f),steel,false);
                Box(parent,"GlassFrostedSafetyBand",new Vector3(-side*.034f,1.46f,z),new Vector3(.008f,.055f,step-.18f),steel,false);
            }
        }

        private static void ScreenDoor(Transform line,int side,float z,int index,string korean,string english)
        {
            var door=Group(line,"ScreenDoor_"+index.ToString("00"));door.localPosition=new Vector3(0,0,z);
            Transform negative=null,positive=null;
            foreach(int end in new[]{-1,1})
            {
                Box(door,"DoorJamb",new Vector3(-side*.02f,1.38f,end*.82f),new Vector3(.22f,2.46f,.07f),steel,true);
                var leaf=Group(door,end<0?"NegativeLeaf":"PositiveLeaf");leaf.localPosition=new Vector3(0,0,end*.402f);
                Box(leaf,"DoorTintedGlass",new Vector3(0,1.39f,0),new Vector3(.065f,2.31f,.787f),black,true);
                Rounded(leaf,"DoorWindowUpper",new Vector3(-side*.044f,1.97f,0),new Vector3(.018f,.91f,.64f),.008f,glass,false);
                Rounded(leaf,"DoorWindowLower",new Vector3(-side*.044f,.91f,0),new Vector3(.018f,1.03f,.64f),.008f,glass,false);
                foreach(int edge in new[]{-1,1})
                    Box(leaf,"LeafStainlessEdge",new Vector3(-side*.047f,1.4f,edge*.386f),new Vector3(.07f,2.34f,.036f),steel,false);
                foreach(float y in new[]{.25f,1.47f,2.56f})
                    Box(leaf,"LeafHorizontalFrame",new Vector3(-side*.049f,y,0),new Vector3(.07f,.045f,.787f),steel,false);
                SideLabel(leaf,"발빠짐 주의",side,-.082f,0,.66f,.065f,Hex("D4D8D5"));
                if(end<0)negative=leaf;else positive=leaf;
            }
            var animation=door.gameObject.AddComponent<SubwayDoor3D>();animation.negativeLeaf=negative;animation.positiveLeaf=positive;
            animation.slideDistance=.80f;animation.transitionDuration=.95f;
            Box(door,"DoorStatus",new Vector3(-side*.145f,2.93f,0),new Vector3(.016f,.045f,.21f),red,false);
            Box(door,"BoardingPositionPlate",new Vector3(-side*.145f,2.76f,.59f),new Vector3(.018f,.15f,.34f),seal,false);
            SideLabel(door,((index-1)/4+1)+"-"+((index-1)%4+1),side,-.160f,.59f,2.76f,.087f,Color.white);
            SideLabel(door,korean,side,-.16f,-.26f,2.81f,.115f,Hex("293E46"));
            SideLabel(door,english,side,-.16f,-.26f,2.67f,.055f,Hex("293E46"));
            Box(door,"ThresholdGrate",new Vector3(-side*.08f,.03f,0),new Vector3(.29f,.032f,1.58f),steel,false);
            var anchor=Group(anchors,(side<0?"Left":"Right")+"_Boarding_"+index);
            anchor.localPosition=new Vector3(line.localPosition.x-side*1.3f,0,z);anchor.localRotation=Quaternion.Euler(0,side*90,0);
            doorAnchors.Add(anchor);
        }

        private static void TactileStuds(Transform parent,float x)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();const int sides=8;
            for(float z=-HalfLength+.1f;z<HalfLength;z+=.15f)for(int row=0;row<5;row++)
            {
                float px=x+(row-2)*.145f;int start=vertices.Count;
                vertices.Add(new Vector3(px,.042f,z));
                for(int i=0;i<=sides;i++){float a=i*Mathf.PI*2/sides;vertices.Add(new Vector3(px+Mathf.Cos(a)*.025f,.039f,z+Mathf.Sin(a)*.025f));}
                for(int i=0;i<sides;i++){triangles.Add(start);triangles.Add(start+i+2);triangles.Add(start+i+1);}
            }
            var mesh=new Mesh{name="RaisedTactileStuds_"+x.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            MeshObject(parent,"RaisedTactileStuds",SaveMesh(mesh),tactileStud);
            for(float z=-HalfLength;z<HalfLength;z+=.6f)
                Box(parent,"TactileTileJoint",new Vector3(x,.031f,z),new Vector3(.8f,.006f,.012f),tactileStud,false);
        }

        private static void Bench(Transform parent,Vector3 pos,float length,bool boxed)
        {
            var bench=Group(parent,boxed?"Gachon_SpriteWoodBlockBench":"Wangsimni_SpriteThinFrameBench");bench.localPosition=pos;bench.localScale=boxed?new Vector3(GachonBenchWidthScale,1f,GachonBenchLengthScale):new Vector3(1.2f,1f,1.25f);
            if(boxed)
            {
                Rounded(bench,"SolidGoldenWoodBody",new Vector3(0,.275f,0),new Vector3(.75f,.49f,length),.018f,woodEnd,false);
                Box(bench,"BenchCollision",new Vector3(0,.29f,0),new Vector3(.79f,.58f,length+.08f),woodEnd,true).GetComponent<Renderer>().enabled=false;
                Box(bench,"RecessedPlinth",new Vector3(0,.025f,0),new Vector3(.64f,.05f,length-.12f),seal,false);
                // Long narrow slats reproduce the original bench_gachon sprite's striped top.
                for(int i=0;i<18;i++)Rounded(bench,"NarrowTopSlat",new Vector3((i-8.5f)*.04f,.531f,0),new Vector3(.037f,.037f,length-.10f),.009f,wood,false);
                foreach(int side in new[]{-1,1})
                {
                    Rounded(bench,"SilverLongTopRim",new Vector3(side*.379f,.525f,0),new Vector3(.032f,.058f,length+.03f),.01f,steel,false);
                    Rounded(bench,"SilverEndRim",new Vector3(0,.525f,side*length*.5f),new Vector3(.79f,.058f,.032f),.01f,steel,false);
                    Box(bench,"WoodEndPanel",new Vector3(0,.277f,side*(length*.5f+.002f)),new Vector3(.743f,.467f,.008f),woodEnd,false);
                }
                foreach(float z in new[]{-length*.45f,length*.45f})Box(bench,"RecessedFoot",new Vector3(0,.025f,z),new Vector3(.63f,.045f,.11f),seal,false);
            }
            else
            {
                // bench_2: thin ochre perforated seat, open black square frame, no back or arms.
                var top=MeshObject(bench,"PerforatedOchreSeat",PerforatedSeat(.58f,length),wood);top.localPosition=Vector3.up*.515f;
                var collision=Group(bench,"SeatCollision");collision.localPosition=Vector3.up*.30f;
                collision.gameObject.AddComponent<BoxCollider>().size=new Vector3(.64f,.60f,length+.08f);
                foreach(int side in new[]{-1,1})
                {
                    Rounded(bench,"BlackTopSideRail",new Vector3(side*.308f,.488f,0),new Vector3(.045f,.058f,length+.07f),.009f,seal,false);
                    Box(bench,"OpenLowerFrameRail",new Vector3(side*.308f,.11f,0),new Vector3(.038f,.038f,length+.07f),seal,false);
                    foreach(float z in new[]{-length*.5f,0,length*.5f})
                    {
                        Rounded(bench,"SquareSteelLeg",new Vector3(side*.308f,.255f,z),new Vector3(.043f,.51f,.043f),.007f,seal,false);
                        Box(bench,"MetalFootPad",new Vector3(side*.308f,.016f,z),new Vector3(.073f,.032f,.083f),seal,false);
                    }
                    Box(bench,"EndFrameLowerRail",new Vector3(0,.11f,side*length*.5f),new Vector3(.65f,.038f,.038f),seal,false);
                    Box(bench,"EndFrameSeatSupport",new Vector3(0,.486f,side*length*.5f),new Vector3(.65f,.058f,.045f),seal,false);
                }
                foreach(float z in new[]{-length*.43f,length*.43f})foreach(int side in new[]{-1,1})
                    Tube(bench,"ExposedSeatBolt",new Vector3(side*.29f,.51f,z),new Vector3(side*.29f,.518f,z),.012f,steel);
            }
            int seats=Mathf.FloorToInt(length*bench.localScale.z/.57f);
            for(int i=0;i<seats;i++)
            {
                var seat=Group(anchors,"SeatPosition_"+seatAnchors.Count);seat.localPosition=pos+new Vector3(0,.56f,(i-(seats-1)*.5f)*.57f);
                seat.localRotation=Quaternion.Euler(0,-90,0);seatAnchors.Add(seat);
            }
        }

        private static Mesh PerforatedSeat(float width,float length)
        {
            const int rows=7,sides=8;int columns=Mathf.RoundToInt(length/.082f);
            float dx=width/rows,dz=length/columns;var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
            for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)
            {
                float cx=-width*.5f+(row+.5f)*dx,cz=-length*.5f+(col+.5f)*dz;
                for(int j=0;j<sides;j++)
                {
                    float a=j*Mathf.PI*2/sides,b=(j+1)*Mathf.PI*2/sides;
                    var d0=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var d1=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                    float t0=Mathf.Min(Mathf.Abs(d0.x)<.001f?999:dx*.5f/Mathf.Abs(d0.x),Mathf.Abs(d0.y)<.001f?999:dz*.5f/Mathf.Abs(d0.y));
                    float t1=Mathf.Min(Mathf.Abs(d1.x)<.001f?999:dx*.5f/Mathf.Abs(d1.x),Mathf.Abs(d1.y)<.001f?999:dz*.5f/Mathf.Abs(d1.y));
                    var center=new Vector3(cx,0,cz);var inner0=center+new Vector3(d0.x*.011f,0,d0.y*.011f);var inner1=center+new Vector3(d1.x*.011f,0,d1.y*.011f);
                    var outer0=center+new Vector3(d0.x*t0,0,d0.y*t0);var outer1=center+new Vector3(d1.x*t1,0,d1.y*t1);
                    Quad(v,n,uv,tri,outer0,outer1,inner1,inner0,Vector3.up);
                    Quad(v,n,uv,tri,inner0,inner1,inner1-Vector3.up*.035f,inner0-Vector3.up*.035f,new Vector3(-d0.x,0,-d0.y));
                }
            }
            var mesh=new Mesh{name="PerforatedBench_"+width+"_"+length};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateBounds();return SaveMesh(mesh);
        }
        private static void Quad(List<Vector3> v,List<Vector3> n,List<Vector2> uv,List<int> t,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal)
        {
            int i=v.Count;v.AddRange(new[]{a,b,c,d});n.AddRange(new[]{normal,normal,normal,normal});
            uv.AddRange(new[]{new Vector2(a.x,a.z),new Vector2(b.x,b.z),new Vector2(c.x,c.z),new Vector2(d.x,d.z)});
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)>=0)t.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});else t.AddRange(new[]{i,i+2,i+1,i,i+3,i+2});
        }

        private static void Column(Transform parent,Vector3 pos,float size,string korean,string english)
        {
            var column=Group(parent,"WhiteSquareColumn");column.localPosition=pos;
            Box(column,"DarkColumnCore",new Vector3(0,2.64f,0),new Vector3(size,5.28f,size),seal,true);
            foreach(int side in new[]{-1,1})
            {
                Box(column,"WhitePanelX",new Vector3(side*(size*.5f+.008f),2.72f,0),new Vector3(.024f,5.08f,size-.035f),ceramic,false);
                Box(column,"WhitePanelZ",new Vector3(0,2.72f,side*(size*.5f+.008f)),new Vector3(size-.035f,5.08f,.024f),ceramic,false);
                Box(column,"YellowWayfindingBandX",new Vector3(side*(size*.5f+.025f),2.38f,0),new Vector3(.01f,.04f,size-.01f),yellow,false);
                Box(column,"YellowWayfindingBandZ",new Vector3(0,2.38f,side*(size*.5f+.025f)),new Vector3(size-.01f,.04f,.01f),yellow,false);
            }
            Box(column,"BlackBase",new Vector3(0,.12f,0),new Vector3(size+.04f,.24f,size+.04f),seal,false);
            Label(column,korean,new Vector3(0,2.13f,-size*.5f-.024f),Quaternion.identity,.115f,Hex("34484E"));
            Label(column,english,new Vector3(0,1.92f,-size*.5f-.024f),Quaternion.identity,.060f,Hex("34484E"));
        }

        private static void VendingMachine(Transform parent,Vector3 position,float yaw=0f)
        {
            var machine=Group(parent,"SpriteOrangeBlueVendingMachine");machine.localPosition=position;machine.localRotation=Quaternion.Euler(0,yaw,0);
            Rounded(machine,"OrangeCabinet",new Vector3(0,1.10f,0),new Vector3(1.10f,2.20f,.92f),.032f,orange,true);
            Rounded(machine,"CreamTopCap",new Vector3(0,2.22f,0),new Vector3(1.14f,.08f,.96f),.025f,productWhite,false);
            Rounded(machine,"CreamControlStrip",new Vector3(.395f,1.20f,-.475f),new Vector3(.26f,1.77f,.05f),.018f,productWhite,false);
            Box(machine,"BlueDisplaySurround",new Vector3(-.14f,1.43f,-.478f),new Vector3(.82f,1.24f,.032f),blue,false);
            Box(machine,"ProductWindowRecess",new Vector3(-.14f,1.43f,-.50f),new Vector3(.74f,1.14f,.02f),seal,false);
            for(int row=0;row<3;row++)
            {
                float y=1.03f+row*.345f;
                Box(machine,"ProductShelf",new Vector3(-.14f,y-.115f,-.53f),new Vector3(.75f,.025f,.17f),steel,false);
                for(int col=0;col<4;col++)
                {
                    float x=-.425f+col*.188f;Material color=(col+row)%3==0?red:(col+row)%3==1?green:orange;
                    if((row+col)%2==0)
                    {
                        Rounded(machine,"ModeledSnackPack",new Vector3(x,y,-.54f),new Vector3(.119f,.211f,.078f),.017f,color,false);
                        Rounded(machine,"PacketLabel",new Vector3(x,y+.005f,-.586f),new Vector3(.082f,.10f,.012f),.014f,productWhite,false);
                        Tube(machine,"PacketRoundLogo",new Vector3(x,y+.008f,-.60f),new Vector3(x,y+.008f,-.611f),.026f,yellow);
                    }
                    else
                    {
                        Tube(machine,"ModeledDrinkCan",new Vector3(x,y-.103f,-.54f),new Vector3(x,y+.096f,-.54f),.052f,col%2==0?blue:color);
                        Tube(machine,"CanTop",new Vector3(x,y+.096f,-.54f),new Vector3(x,y+.105f,-.54f),.049f,steel);
                        Rounded(machine,"CanLabel",new Vector3(x,y,-.594f),new Vector3(.082f,.10f,.012f),.011f,productWhite,false);
                    }
                }
                Box(machine,"BlueShelfStripe",new Vector3(-.14f,y-.112f,-.613f),new Vector3(.74f,.020f,.009f),blue,false);
            }
            Box(machine,"DisplayGlazing",new Vector3(-.14f,1.43f,-.635f),new Vector3(.76f,1.14f,.012f),divider,false);
            foreach(int side in new[]{-1,1})
            {
                var artwork=MeshObject(machine,"OriginalSpriteSideBranding",QuadMesh("VendingSideBranding",.88f,2.14f),vendingGraphic);
                artwork.localPosition=new Vector3(side*.558f,1.115f,0);artwork.localRotation=Quaternion.Euler(0,side<0?90:-90,0);
            }
            Box(machine,"LCDGreenRecess",new Vector3(.395f,1.62f,-.511f),new Vector3(.145f,.065f,.014f),seal,false);
            Label(machine,"1500",new Vector3(.395f,1.62f,-.523f),Quaternion.identity,.042f,Hex("86C480"));
            Rounded(machine,"BlackButtonPanel",new Vector3(.395f,1.34f,-.511f),new Vector3(.159f,.34f,.018f),.008f,seal,false);
            for(int i=0;i<12;i++)Rounded(machine,"KeypadButton",new Vector3(.35f+i%3*.045f,1.447f-i/3*.061f,-.53f),new Vector3(.029f,.029f,.01f),.005f,i>8?yellow:steel,false);
            Rounded(machine,"CoinSlot",new Vector3(.40f,1.02f,-.511f),new Vector3(.12f,.022f,.027f),.008f,seal,false);
            Rounded(machine,"PaymentReader",new Vector3(.395f,.885f,-.513f),new Vector3(.17f,.12f,.027f),.008f,seal,false);
            Box(machine,"FrontLogoPlate",new Vector3(-.15f,.716f,-.48f),new Vector3(.71f,.19f,.019f),yellow,false);
            Label(machine,"StoryWay",new Vector3(-.15f,.716f,-.496f),Quaternion.identity,.084f,Hex("BA4028"));
            Rounded(machine,"BlackPickupOpening",new Vector3(-.12f,.344f,-.48f),new Vector3(.81f,.30f,.031f),.01f,seal,false);
            for(int i=0;i<5;i++)Box(machine,"PickupShutterLouver",new Vector3(-.12f,.254f+i*.043f,-.50f),new Vector3(.74f,.02f,.019f),black,false);
            Rounded(machine,"RecessedBase",new Vector3(0,.034f,0),new Vector3(1.0f,.067f,.83f),.02f,seal,false);
            foreach(int side in new[]{-1,1})Box(machine,"BaseVent",new Vector3(side*.47f,.155f,-.469f),new Vector3(.07f,.09f,.012f),seal,false);
        }

        private static void Escalator(Transform parent,Vector3 bottom,float run,float rise,string name)
        {
            var escalator=Group(parent,name);escalator.localPosition=bottom;
            float angle=-Mathf.Atan2(rise,run)*Mathf.Rad2Deg;
            int count=24;float dz=run/count,dy=rise/count;
            Box(escalator,"LowerCombPlate",new Vector3(0,.04f,-.42f),new Vector3(1.10f,.08f,.84f),steel,false);
            for(int i=0;i<count;i++)
            {
                float y=(i+1)*dy,z=(i+.5f)*dz;
                Box(escalator,"Step_"+i.ToString("00"),new Vector3(0,y-.06f,z),new Vector3(.96f,.12f,dz+.012f),seal,false);
                Box(escalator,"YellowStepNosing",new Vector3(0,y+.004f,z-dz*.5f+.025f),new Vector3(.93f,.018f,.035f),yellow,false);
                for(int rib=0;rib<8;rib++)
                    Box(escalator,"TreadRib",new Vector3((rib-3.5f)*.11f,y+.005f,z),new Vector3(.012f,.012f,dz-.06f),steel,false);
            }
            // A continuous collider makes the detailed treads comfortable for the CharacterController.
            var ramp=Group(escalator,"WalkableSlopeCollider");
            ramp.localPosition=new Vector3(0,rise*.5f+.055f,run*.5f);ramp.localRotation=Quaternion.Euler(angle,0,0);
            ramp.gameObject.AddComponent<BoxCollider>().size=new Vector3(.99f,.10f,Mathf.Sqrt(run*run+rise*rise)+.16f);
            foreach(int side in new[]{-1,1})
            {
                var skirt=Box(escalator,"StainlessSideSkirt",new Vector3(side*.59f,rise*.5f+.20f,run*.5f),new Vector3(.18f,.44f,Mathf.Sqrt(run*run+rise*rise)+.35f),steel,false);
                skirt.localRotation=Quaternion.Euler(angle,0,0);
                var panel=Box(escalator,"TealBalustrade",new Vector3(side*.62f,rise*.5f+.73f,run*.5f),new Vector3(.045f,.80f,Mathf.Sqrt(run*run+rise*rise)+.35f),glass,true);
                panel.localRotation=Quaternion.Euler(angle,0,0);
                Tube(escalator,"BlackRubberHandrail",new Vector3(side*.62f,1.04f,-.23f),new Vector3(side*.62f,rise+1.04f,run+.23f),.047f,seal);
                Tube(escalator,"SilverHandrailTrim",new Vector3(side*.62f,.94f,-.23f),new Vector3(side*.62f,rise+.94f,run+.23f),.026f,steel);
                Tube(escalator,"HandrailBottomReturn",new Vector3(side*.62f,.25f,-.38f),new Vector3(side*.62f,1.04f,-.23f),.047f,seal);
                Tube(escalator,"HandrailTopReturn",new Vector3(side*.62f,rise+.25f,run+.38f),new Vector3(side*.62f,rise+1.04f,run+.23f),.047f,seal);
            }
            var landing=Group(escalator,"UpperLanding");landing.localPosition=new Vector3(0,rise,run+1f);
            Box(landing,"LandingFloor",new Vector3(0,-.06f,0),new Vector3(1.45f,.12f,1.85f),concrete,true);
            Box(landing,"UpperCombPlate",new Vector3(0,.005f,-.73f),new Vector3(.98f,.016f,.25f),steel,false);
            foreach(int side in new[]{-1,1})
            {
                Box(landing,"LandingRailCollider",new Vector3(side*.69f,.56f,0),new Vector3(.09f,1.12f,1.85f),steel,true);
                Tube(landing,"LandingTopRail",new Vector3(side*.69f,1.09f,-.87f),new Vector3(side*.69f,1.09f,.92f),.038f,seal);
            }
            Box(landing,"LandingEndRail",new Vector3(0,.56f,.93f),new Vector3(1.45f,1.12f,.07f),glass,true);
            Box(landing,"ExitIndicator",new Vector3(0,1.60f,.97f),new Vector3(1.3f,.35f,.06f),green,false);
            Label(landing,"나가는 곳  EXIT",new Vector3(0,1.6f,.926f),Quaternion.identity,.12f,Color.white);
            var integration=Group(anchors,name+"_LandingAnchor");integration.localPosition=bottom+new Vector3(0,rise,run+.65f);landingAnchors.Add(integration);
        }

        private static void HangingSign(Transform parent,Vector3 pos,string korean,string english,string instruction,float halfWidth)
        {
            var sign=Group(parent,"DoubleSidedWayfindingSign");sign.localPosition=pos;
            Rounded(sign,"StainlessSignRim",Vector3.zero,new Vector3(3.9f,.85f,.17f),.055f,steel,false);
            foreach(int face in new[]{-1,1})
            {
                var panel=Group(sign,"SignFace");panel.localRotation=Quaternion.Euler(0,face<0?0:180,0);
                Box(panel,"CharcoalDisplay",new Vector3(0,0,-.091f),new Vector3(3.77f,.73f,.018f),seal,false);
                Box(panel,"LineIdentifier",new Vector3(-1.75f,0,-.105f),new Vector3(.09f,.68f,.011f),yellow,false);
                Label(panel,korean,new Vector3(-.85f,.15f,-.11f),Quaternion.identity,.23f,Color.white);
                Label(panel,english,new Vector3(-.85f,-.16f,-.11f),Quaternion.identity,.094f,Hex("E0E1D8"));
                Label(panel,instruction,new Vector3(.91f,.07f,-.11f),Quaternion.identity,.124f,Hex("F4D34A"));
                Label(panel,"수인분당선",new Vector3(.91f,-.17f,-.11f),Quaternion.identity,.074f,Color.white);
            }
            foreach(int side in new[]{-1,1})Tube(sign,"CeilingSuspension",new Vector3(side*1.45f,.4f,0),new Vector3(side*1.45f,5.12f-pos.y,0),.016f,steel);
        }

        private static void TransferTotem(Transform parent,Vector3 pos)
        {
            var t=Group(parent,"WangsimniTransferTotem");t.localPosition=pos;
            Rounded(t,"TotemBody",new Vector3(0,1.55f,0),new Vector3(.77f,3.1f,.24f),.04f,steel,true);
            Box(t,"InformationFace",new Vector3(0,1.65f,-.13f),new Vector3(.67f,2.76f,.012f),seal,false);
            Label(t,"환승  ↑",new Vector3(0,2.72f,-.14f),Quaternion.identity,.16f,Color.white);
            Label(t,"TRANSFER",new Vector3(0,2.48f,-.14f),Quaternion.identity,.08f,Color.white);
            Material[] mats={green,blue,red,yellow};string[] lines={"2호선","5호선","경의중앙","수인분당"};
            for(int i=0;i<4;i++)
            {
                float y=2.13f-i*.43f;Rounded(t,"LineColor",new Vector3(-.21f,y,-.148f),new Vector3(.055f,.26f,.02f),.01f,mats[i],false);
                Label(t,lines[i],new Vector3(.02f,y,-.15f),Quaternion.identity,.11f,Color.white);
            }
            Rounded(t,"TotemFoot",new Vector3(0,.04f,0),new Vector3(.87f,.08f,.42f),.03f,seal,false);
        }

        private static void WallPoster(Transform parent,float x,float z,string title,string subtitle)
        {
            var poster=Group(parent,"ModernRouteInformationPanel");poster.localPosition=new Vector3(x,2.00f,z);poster.localRotation=Quaternion.Euler(0,90,0);
            Rounded(poster,"StainlessPanelRim",Vector3.zero,new Vector3(3.8f,.88f,.055f),.025f,steel,false);
            Box(poster,"IvoryRoutePanel",new Vector3(0,0,-.034f),new Vector3(3.68f,.78f,.016f),concrete,false);
            Label(poster,title,new Vector3(0,.23f,-.05f),Quaternion.identity,.155f,Hex("263C47"));
            Box(poster,"BundangRoute",new Vector3(0,-.025f,-.049f),new Vector3(3.1f,.025f,.009f),yellow,false);
            string[] stops={"왕십리","선릉","수서","복정","가천대","태평","모란"};
            for(int i=0;i<stops.Length;i++)
            {
                float sx=(i-3)*.47f;var circle=Group(poster,"RouteStop");circle.localPosition=new Vector3(sx,-.025f,-.061f);
                var cylinder=GameObject.CreatePrimitive(PrimitiveType.Cylinder);cylinder.transform.SetParent(circle,false);
                cylinder.transform.localRotation=Quaternion.Euler(90,0,0);cylinder.transform.localScale=new Vector3(.059f,.009f,.059f);
                cylinder.GetComponent<Renderer>().sharedMaterial=i==4?yellow:seal;UnityEngine.Object.DestroyImmediate(cylinder.GetComponent<Collider>());
                Label(poster,stops[i],new Vector3(sx,-.18f,-.051f),Quaternion.identity,.071f,Hex("263C47"));
            }
            Label(poster,subtitle,new Vector3(0,-.33f,-.05f),Quaternion.identity,.063f,Hex("536064"));
        }

        private static void Ceiling(Transform parent,float width,bool island)
        {
            Box(parent,"WarmIvoryCeiling",new Vector3(0,5.20f,0),new Vector3(width*2+.2f,.14f,PlatformLength+.2f),concrete,false);
            for(float z=-HalfLength+.6f;z<HalfLength;z+=1.2f)
            {
                Box(parent,"CeilingPanelJoint",new Vector3(0,5.119f,z),new Vector3(width*2,.013f,.012f),seal,false);
                Box(parent,"TransverseSteelRib",new Vector3(0,5.02f,z+.6f),new Vector3(width*2,.11f,.055f),steel,false);
                foreach(float x in island?new[]{-3.8f,0f,3.8f}:new[]{-1.6f,1.5f})
                {
                    Rounded(parent,"RecessedLEDChannel",new Vector3(x,5.055f,z),new Vector3(.35f,.13f,1.17f),.025f,seal,false);
                    Rounded(parent,"WarmDiffuser",new Vector3(x,4.975f,z),new Vector3(.26f,.035f,1.15f),.014f,led,false);
                }
                for(int vent=0;vent<6;vent++)Box(parent,"VentLouver",new Vector3(width-.9f,5.11f,z+(vent-2.5f)*.105f),new Vector3(.43f,.017f,.028f),seal,false);
            }
            for(float z=-HalfLength+3f;z<HalfLength;z+=6f)
            {
                foreach(float x in island?new[]{-2.9f,2.9f}:new[]{-.65f,2.4f})
                {
                    var lamp=Group(parent,"WarmPlatformFill");lamp.localPosition=new Vector3(x,4.70f,z);
                    var l=lamp.gameObject.AddComponent<Light>();l.type=LightType.Point;l.color=new Color(1,.94f,.82f);l.intensity=5.8f;l.range=8.8f;l.shadows=LightShadows.None;
                }
            }
            var fill=Group(parent,"ArchitecturalFill");var sun=fill.gameObject.AddComponent<Light>();
            sun.type=LightType.Directional;sun.intensity=.36f;sun.color=new Color(.94f,.97f,1f);sun.shadows=LightShadows.None;fill.localRotation=Quaternion.Euler(65,-35,0);
        }

        private static void CreatePlayer(Transform spawn)
        {
            var player=Group(root,"FirstPersonPlayer");player.SetPositionAndRotation(spawn.position,spawn.rotation);
            var cc=player.gameObject.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.23f;cc.center=Vector3.up*.9f;cc.stepOffset=.24f;cc.slopeLimit=45;cc.skinWidth=.025f;
            var eye=Group(player,"Main Camera");eye.localPosition=Vector3.up*1.63f;
            var camera=eye.gameObject.AddComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=68;camera.nearClipPlane=.035f;camera.farClipPlane=100;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Hex("111B20");camera.allowHDR=true;
            camera.GetUniversalAdditionalCameraData().SetRenderer(1);eye.gameObject.AddComponent<AudioListener>();
            var movement=player.gameObject.AddComponent<SubwayFirstPersonController>();movement.eye=eye;
        }

        private static void SetupFont()
        {
            string chars="";var texts=root.GetComponentsInChildren<TextMesh>();foreach(var t in texts)chars+=t.text;
            font.RequestCharactersInTexture(chars,64,FontStyle.Normal);signFont.SetTexture("_BaseMap",font.material.mainTexture);
            foreach(var t in texts)t.GetComponent<MeshRenderer>().sharedMaterial=signFont;
            var sync=root.gameObject.AddComponent<SubwaySignFont3D>();sync.font=font;sync.signMaterial=signFont;
            EditorUtility.SetDirty(signFont);
        }
        private static void SetupPipeline()
        {
            // The long platform contains more than eight visible point lights. Forward+ keeps
            // their contribution independent of camera-frustum changes on large floor/ceiling meshes.
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/SubwayFirstPersonRenderer.asset");
            if(!renderer)throw new InvalidOperationException("Missing 3D renderer.");
            renderer.renderingMode=RenderingMode.ForwardPlus;EditorUtility.SetDirty(renderer);
            var pipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/SubwayFirstPersonPipeline.asset");
            if(!pipeline)throw new InvalidOperationException("The existing 3D rendering pipeline is missing.");
            var scope=root.gameObject.AddComponent<SubwaySceneRenderPipeline3D>();scope.originalPipeline=GraphicsSettings.defaultRenderPipeline;scope.pipeline=pipeline;scope.Apply();
        }

        private static Material Mat(string name,Color color,float metallic,float smoothness,Color? emission=null)
        {
            string path=Art+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.shader=Shader.Find("Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smoothness);
            if(emission.HasValue){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission.Value);}
            EditorUtility.SetDirty(m);return m;
        }
        private static Texture2D WoodGrain()
        {
            string path=Art+"/Textures/FineWoodGrain.asset";var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(existing)return existing;
            const int w=256,h=256;var t=new Texture2D(w,h,TextureFormat.RGBA32,true);t.name="FineWoodGrain";t.filterMode=FilterMode.Trilinear;t.wrapMode=TextureWrapMode.Repeat;t.anisoLevel=8;
            var pixels=new Color[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {float ripple=Mathf.Sin(x*.73f+Mathf.Sin(y*.05f)*.28f)*.027f;float grain=Mathf.PerlinNoise(x*.31f,y*.023f)*.08f;float c=.91f+ripple+grain;pixels[y*w+x]=new Color(c,c,c,1);}
            t.SetPixels(pixels);t.Apply();AssetDatabase.CreateAsset(t,path);return t;
        }

        private static Texture2D VendingSideArtwork()
        {
            string path=Art+"/Textures/OriginalVendingSideArtwork.asset";var saved=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(saved)return saved;
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false);source.LoadImage(File.ReadAllBytes(Sprites+"vending.png"));
            const int w=256,h=512;var t=new Texture2D(w,h,TextureFormat.RGBA32,true);t.name="OriginalVendingSideArtwork";t.filterMode=FilterMode.Trilinear;t.wrapMode=TextureWrapMode.Clamp;t.anisoLevel=8;
            // UV unwrap the actual sprite's right face, retaining its snack artwork and blue/orange split.
            Vector2 bl=new Vector2(177,6),br=new Vector2(266,53),tl=new Vector2(177,330),tr=new Vector2(266,377);
            var pixels=new Color[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                Vector2 p=Vector2.Lerp(Vector2.Lerp(bl,br,(x+.5f)/w),Vector2.Lerp(tl,tr,(x+.5f)/w),(y+.5f)/h);
                var c=source.GetPixelBilinear(p.x/source.width,p.y/source.height);c.a=1;pixels[y*w+x]=c;
            }
            t.SetPixels(pixels);t.Apply();AssetDatabase.CreateAsset(t,path);UnityEngine.Object.DestroyImmediate(source);return t;
        }

        private static Texture2D StoneGrain()
        {
            string path=Art+"/Textures/FineTerrazzo.asset";var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(existing)return existing;
            const int n=256;var t=new Texture2D(n,n,TextureFormat.RGBA32,true);t.name="FineTerrazzo";t.filterMode=FilterMode.Trilinear;t.wrapMode=TextureWrapMode.Repeat;t.anisoLevel=8;
            var rng=new System.Random(9183);var pixels=new Color[n*n];
            for(int i=0;i<pixels.Length;i++)
            {float v=.91f+(float)rng.NextDouble()*.12f;if(rng.NextDouble()<.035)v=.70f;pixels[i]=new Color(v,v,v,1);}
            t.SetPixels(pixels);t.Apply();AssetDatabase.CreateAsset(t,path);return t;
        }

        private static Transform Rounded(Transform parent,string name,Vector3 position,Vector3 size,float radius,Material material,bool collision)
        {
            var t=MeshObject(parent,name,RoundedBox(size,radius),material);t.localPosition=position;
            if(collision)t.gameObject.AddComponent<BoxCollider>().size=size;return t;
        }

        private static Mesh RoundedBox(Vector3 size,float radius)
        {
            string key="Bevel_"+size.x.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+size.y.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+size.z.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+radius.ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
            if(meshCache.TryGetValue(key,out var cached)&&cached)return cached;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uvs=new List<Vector2>();var triangles=new List<int>();
            Vector3 h=size*.5f;float r=Mathf.Min(radius,Mathf.Min(h.x,Mathf.Min(h.y,h.z))*.95f);Vector3 inner=h-Vector3.one*r;const int n=8;
            Vector3[] faces={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            foreach(var normal in faces)
            {
                Vector3 u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.up;Vector3 v=Vector3.Cross(normal,u);int offset=vertices.Count;
                for(int y=0;y<=n;y++)for(int x=0;x<=n;x++)
                {
                    var p=Vector3.Scale(normal+u*(2f*x/n-1)+v*(2f*y/n-1),h);
                    var q=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));
                    var direction=(p-q).normalized;vertices.Add(q+direction*r);normals.Add(direction);uvs.Add(new Vector2((float)x/n,(float)y/n));
                }
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {int a=offset+y*(n+1)+x,b=a+1,c=a+n+1,d=c+1;triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(b);triangles.Add(d);triangles.Add(c);}
            }
            var mesh=new Mesh{name=key};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mesh=SaveMesh(mesh);meshCache[key]=mesh;return mesh;
        }

        private static Transform Group(Transform parent,string name)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        private static Transform Box(Transform parent,string name,Vector3 position,Vector3 size,Material material,bool collision)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
            go.GetComponent<MeshRenderer>().sharedMaterial=material;if(!collision)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go.transform;
        }
        private static Transform MeshObject(Transform parent,string name,Mesh mesh,Material material)
        {var t=Group(parent,name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;return t;}
        private static Mesh SaveMesh(Mesh mesh)
        {
            string path=Art+"/Meshes/"+mesh.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing){UnityEngine.Object.DestroyImmediate(mesh);return existing;}AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        private static Mesh QuadMesh(string name,float width,float height)
        {
            var mesh=new Mesh{name=name};mesh.vertices=new[]{new Vector3(-width*.5f,-height*.5f,0),new Vector3(-width*.5f,height*.5f,0),new Vector3(width*.5f,height*.5f,0),new Vector3(width*.5f,-height*.5f,0)};
            mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();return SaveMesh(mesh);
        }
        private static void Tube(Transform parent,string name,Vector3 a,Vector3 b,float radius,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=(a+b)*.5f;
            go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);go.transform.localScale=new Vector3(radius*2,(b-a).magnitude*.5f,radius*2);
            go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        private static void SideLabel(Transform parent,string text,int side,float x,float z,float y,float size,Color color)
        {Label(parent,text,new Vector3(side*x,y,z),Quaternion.Euler(0,side>0?90:-90,0),size,color);}
        private static void Label(Transform parent,string text,Vector3 pos,Quaternion rotation,float size,Color color)
        {
            var t=Group(parent,"Sign_"+text);t.localPosition=pos;t.localRotation=rotation;var tm=t.gameObject.AddComponent<TextMesh>();
            tm.font=font;tm.text=text;tm.fontSize=64;tm.characterSize=size*10/64;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.color=QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color;
            tm.GetComponent<MeshRenderer>().sharedMaterial=signFont;
        }
        private static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out var c);return c;}
        private static void EnsureFolder(string path)
        {if(AssetDatabase.IsValidFolder(path))return;int i=path.LastIndexOf('/');EnsureFolder(path.Substring(0,i));AssetDatabase.CreateFolder(path.Substring(0,i),path.Substring(i+1));}
    }
}
#endif




#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SubwayCarry.Transit.Editor
{
    /// <summary>2:1 quarter-view car, authored as persistent 2D meshes and original sprites.</summary>
    public static class SubwayQuarterBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Subway_QuarterView.unity";
        private const string Art = "Assets/_Project/Art/Sprites/SubwayQuarterView/";
        private const string Sprites = "Assets/_Project/Art/Sprites/";
        private const string DoorPrefab = "Assets/_Project/Prefabs/Train/SubwayDoor_QuarterView.prefab";
        private static readonly Color Ink = Hex("29343B");
        private static readonly Color Steel = Hex("BBC3C3");
        private static readonly Color Light = Hex("E2E5DF");
        private static readonly Color Shadow = Hex("879398");
        private static readonly Color Gold = Hex("CCB85F");
        private static readonly float[] DoorXs = { -13.5f, -4.5f, 4.5f, 13.5f };
        private static readonly float[] BankXs = { -17.1f, -9f, 0f, 9f, 17.1f };
        private static readonly int[] Counts = { 3, 7, 7, 7, 3 };
        private static Material solidMaterial, floorMaterial, doorMaterial, spriteMaterial;
        private static Font font;
        private static int meshIndex;
        private static readonly List<GameObject> fullForeground = new List<GameObject>();
        private static readonly List<GameObject> cutForeground = new List<GameObject>();

        // Cross-car layout is compacted to real-car proportions; both screen axes remain 2:1.
        public static Vector2 Project(float u, float v, float h = 0f) => new Vector2(u + .75f * v, .5f * u - .375f * v + h);

        [MenuItem("SubwayCarry/Train/Create Quarter View Interior (missing only)")]
        public static void BuildMissing()
        {
            if (SceneManager.GetActiveScene().path == ScenePath) return;
            RequireSavedScenes();
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else Build();
        }

        public static void RebuildGenerated() { RequireSavedScenes(); Build(); }

        private static void RequireSavedScenes()
        {
            for (int i=0;i<SceneManager.sceneCount;i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before building/opening the quarter-view car.");
        }

        private static void Build()
        {
            Prepare();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = Group(null, "Subway_QuarterView");
            var shell = Group(root, "01_CarShell");
            var floor = Group(root, "02_ProjectedLinoleum");
            var rearWall = Group(root, "03_FarWallAndOriginalWindows");
            var seating = Group(root, "04_OriginalSeatsAndRearVariants_54");
            var doors = Group(root, "05_QuarterSlidingDoors_8");
            var details = Group(root, "06_PolesSignsAndEquipment");
            var frontWall = Group(root, "07_ForegroundWall");
            var frontFull = Group(frontWall, "FullHeightForeground");
            var frontCut = Group(frontWall, "LowCutawayForeground");
            fullForeground.Add(frontFull.gameObject); cutForeground.Add(frontCut.gameObject);
            var collision = Group(root, "08_FloorPlaneColliders");
            var anchors = Group(root, "09_IntegrationAnchors");
            var seatAnchors = Group(anchors, "SeatAnchors");
            var doorAnchors = Group(anchors, "DoorAnchors");
            Point(anchors,"PlayerSpawn",0,0);
            foreach (var uv in new[]{new Vector2(-18.5f,-3.35f),new Vector2(18.5f,-3.35f),new Vector2(18.5f,3.35f),new Vector2(-18.5f,3.35f)})
                Point(anchors,"InteriorCorner",uv.x,uv.y);

            var shadow = new Surface();
            shadow.Tile(-19.25f,-3.96f,19.25f,3.96f,-.32f,Hex("182126"));
            shadow.Save(shell,"CarUndershadow",-1200);
            var frame = new Surface();
            frame.Prism(-19.10f,-3.87f,19.10f,3.87f,-.25f,0,Ink,Hex("626E73"));
            frame.Save(shell,"ClosedCarFrame",-1100);
            var grain = new Surface();
            grain.Quad(Project(-18.9f,-3.61f),Project(18.9f,-3.61f),Project(18.9f,3.61f),Project(-18.9f,3.61f),Color.white,
                new Vector2(-18.9f/3.27f,-3.61f/3.27f),new Vector2(18.9f/3.27f,-3.61f/3.27f),new Vector2(18.9f/3.27f,3.61f/3.27f),new Vector2(-18.9f/3.27f,3.61f/3.27f));
            grain.Save(floor,"LinoleumTexture",-1050,floorMaterial);
            var floorDetail = new Surface();
            foreach(int side in new[]{-1,1})
            {
                floorDetail.Tile(-18.9f,side*1.56f-.025f,18.9f,side*1.56f+.025f,0,Hex("939C95"));
                floorDetail.Tile(-18.9f,side*3.45f-.035f,18.9f,side*3.45f+.035f,.01f,Light);
            }
            for(float u=-17.6f;u<=18;u+=3.2f)
                floorDetail.Tile(u,-3.4f,u+.012f,3.4f,0,new Color(.30f,.35f,.34f,.2f));
            foreach(float u in DoorXs) foreach(int side in new[]{-1,1})
            {
                float v=side*3.34f;
                floorDetail.Tile(u-1.32f,v-.22f,u+1.32f,v+.22f,.01f,Hex("8D9896"));
                floorDetail.Tile(u-1.26f,side*3.02f-.035f,u+1.26f,side*3.02f+.035f,.015f,Gold);
                for(int i=0;i<13;i++) floorDetail.Tile(u-1.2f+i*.2f,v-.17f,u-1.18f+i*.2f,v+.17f,.02f,Light);
            }
            floorDetail.Save(floor,"WeldsAndDoorThresholds",-1000);

            // Walls share the same footprint; only the near wall is cut away for visibility.
            foreach(int side in new[]{-1,1})
            {
                float start=-18.95f;
                foreach(float u in DoorXs)
                {
                    Wall(side,start,u-1.36f,rearWall,frontFull,frontCut,collision);
                    start=u+1.36f;
                }
                Wall(side,start,18.95f,rearWall,frontFull,frontCut,collision);
            }
            int seatNumber=0;
            foreach(int side in new[]{-1,1})
            {
                for(int b=0;b<BankXs.Length;b++)
                {
                    float center=BankXs[b]; int count=Counts[b]; float length=count*.82f;
                    var bank=Group(seating,(side<0?"Far":"Near")+"_Bench_"+(b+1));
                    var support=new Surface();
                    support.Tile(center-length*.5f-.05f,side*2.48f-.55f,center+length*.5f+.05f,side*2.48f+.55f,0,new Color(.16f,.20f,.21f,.32f));
                    support.Prism(center-length*.5f,side*2.48f-.44f,center+length*.5f,side*2.48f+.44f,.12f,.24f,Ink,Shadow);
                    support.Save(bank,"BenchPlinthAndShadow",700);
                    Footprint(collision,(side<0?"Far":"Near")+"_SeatBankCollider_"+b,center,side*2.53f,length+.1f,1.22f);
                    for(int s=0;s<count;s++)
                    {
                        float u=center+(s-(count-1)*.5f)*.82f;
                        var foot=Project(u,side*2.5f);
                        if(side<0)
                        {
                            string file=s==0?"subway_seat(right)":s==count-1?"subway_seat":"subway_seat_open";
                            var sprite=Original(file);
                            const float scale=.265f;
                            Vector2 offset=(sprite.rect.center-new Vector2(516,214))*scale/100f;
                            DrawSprite(bank,"OriginalSeat_"+(++seatNumber).ToString("00"),sprite,foot+offset,scale,Order(foot));
                        }
                        else
                        {
                            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"SeatRear.png");
                            DrawSprite(bank,"RearSeat_"+(++seatNumber).ToString("00"),sprite,foot,.215f,Order(foot));
                        }
                        Point(seatAnchors,"Seat_"+seatNumber.ToString("00"),u,side*2.34f);
                    }
                    foreach(int end in new[]{-1,1})
                    {
                        float u=center+end*(length*.5f+.09f);
                        Pole(details,collision,u,side*1.86f,1.65f);
                        var divider=new Surface();
                        divider.Quad(Project(u,side*1.88f,.65f),Project(u,side*2.94f,.65f),Project(u,side*2.94f,.75f),Project(u,side*1.88f,.75f),Light);
                        divider.Save(details,"SeatEndGrabRail",Order(Project(u,side*2.2f))+4);
                    }
                    var wallParent=side<0?rearWall:frontFull;
                    int panes=count==3?1:2;
                    float paneWidth=2.30f;
                    for(int p=0;p<panes;p++)
                    {
                        float u=center+(p-(panes-1)*.5f)*(paneWidth+.15f)-paneWidth*.5f;
                        OriginalWindow(wallParent,u,side*3.6f,.46f,paneWidth,side<0?-690:4300);
                    }
                    if(count==3)
                    {
                        var plate=new Surface();plate.Wall(center-.48f,center+.48f,side*3.595f,2.37f,2.55f,Hex("977485"));
                        plate.Save(wallParent,"PriorityPlate",side<0?-600:4390);
                        Label(wallParent,"우선석",Project(center,side*3.59f,2.45f),.105f,Light,26.565f,side<0?-590:4395);
                    }
                }
            }

            var prefab=CreateDoorPrefab();
            var doorList=new List<SubwayQuarterDoor>();
            int doorNumber=0;
            foreach(int side in new[]{-1,1}) foreach(float u in DoorXs)
            {
                var door=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                door.name=(side<0?"Far":"Near")+"_Door_"+(++doorNumber).ToString("00");
                door.transform.SetParent(doors,false);door.transform.localPosition=Project(u,side*3.6f);
                foreach(var renderer in door.GetComponentsInChildren<MeshRenderer>(true)) renderer.sortingOrder+=side<0?-500:4500;
                foreach(var t in door.GetComponentsInChildren<Transform>(true))
                {
                    if(t.name=="FullHeight") { if(side>0) fullForeground.Add(t.gameObject); else t.gameObject.SetActive(true); }
                    if(t.name=="Cutaway") { if(side>0) cutForeground.Add(t.gameObject); else t.gameObject.SetActive(false); }
                }
                var controller=door.GetComponent<SubwayQuarterDoor>();
                controller.Configure(door.transform.Find("LeftLeaf"),door.transform.Find("RightLeaf"),1.27f);
                doorList.Add(controller);
                var anchor=Point(doorAnchors,"Door_"+doorNumber.ToString("00"),u,side*2.8f);
                Point(anchor,"Outside",side*.0f,side*1.45f);
                if(side<0) Label(details,"1-"+((doorNumber-1)%4+1),Project(u,side*3.6f,2.58f),.13f,Ink,26.565f,-300);
            }
            Ends(shell,details,collision);
            Equipment(details);
            var camObject=new GameObject("Main Camera");camObject.tag="MainCamera";
            var camera=camObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=6.5f;
            camera.allowMSAA=false;camera.allowHDR=false;camera.allowDynamicResolution=false;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Hex("232D33");camera.nearClipPlane=.1f;camera.farClipPlane=100f;
            camObject.AddComponent<AudioListener>();
            camObject.AddComponent<SubwayQuarterPreview>().Configure(doorList.ToArray(),fullForeground.ToArray(),cutForeground.ToArray());
            var overviewObject=new GameObject("Overview Camera (disabled)");var overview=overviewObject.AddComponent<Camera>();
            overview.CopyFrom(camera);overview.orthographicSize=14.2f;overview.enabled=false;overviewObject.transform.position=new Vector3(0,1,-10);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            if(SceneView.lastActiveSceneView!=null)
            {
                SceneView.lastActiveSceneView.in2DMode=true;
                SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(0,1,0),new Vector3(47,27,1)),false);
            }
            Selection.activeGameObject=root.gameObject;
            Debug.Log("Quarter-view carriage saved: original far seats, matching rear seats, 8 diagonal doors, near-wall cutaway.");
        }

        private static void Prepare()
        {
            meshIndex=0;fullForeground.Clear();cutForeground.Clear();
            Directory.CreateDirectory(Art+"Geometry");
            solidMaterial=MaterialAsset("QuarterSolid",null);
            floorMaterial=MaterialAsset("QuarterFloor",AssetDatabase.LoadAssetAtPath<Texture2D>(Sprites+"SubwayTopDown/Floor.png"));
            doorMaterial=MaterialAsset("QuarterDoor",AssetDatabase.LoadAssetAtPath<Texture2D>(Sprites+"SubwayTopDown/Door.png"));
            spriteMaterial=AssetDatabase.LoadAssetAtPath<Material>(Sprites+"SubwayTopDown/InteriorUnlit.mat");
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Art/Fonts/NanumGothic-Regular.ttf");
        }

        private static Shader RequireSurfaceShader()
        {
            const string path = Art + "SubwayQuarterSurface.shader";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
                throw new InvalidOperationException("Missing quarter-view shader. Restore " + path + " and its .meta from Git.");
            if (ShaderUtil.ShaderHasError(shader) || !shader.isSupported)
                throw new InvalidOperationException("Quarter-view shader cannot compile or is unsupported. Select " + path + " and check the Console.");
            return shader;
        }

        [MenuItem("SubwayCarry/Train/Repair Quarter View Material Shaders")]
        public static void RepairMaterialShaders()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before repairing quarter-view materials.");

            const string shaderPath = Art + "SubwayQuarterSurface.shader";
            AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var shader = RequireSurfaceShader();
            var materials = new List<Material>();
            foreach (string name in new[] { "QuarterSolid", "QuarterFloor", "QuarterDoor" })
            {
                string path = Art + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                    throw new InvalidOperationException("Missing quarter-view material: " + path);
                materials.Add(material);
            }

            int repaired = 0;
            foreach (var material in materials)
            {
                if (material.shader != shader)
                {
                    Undo.RecordObject(material, "Repair quarter-view shader");
                    material.shader = shader;
                    EditorUtility.SetDirty(material);
                    AssetDatabase.SaveAssetIfDirty(material);
                    repaired++;
                }
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(material),
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            SceneView.RepaintAll();
            Debug.Log("Quarter-view material shaders verified: 3/3; reassigned: " + repaired
                + ". Door, floor and wall materials use " + shader.name + ".");
        }

        private static Material MaterialAsset(string name, Texture texture)
        {
            string path=Art+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=RequireSurfaceShader();
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            else material.shader=shader;
            material.mainTexture=texture;EditorUtility.SetDirty(material);return material;
        }

        private static void Wall(int side,float start,float end,Transform far,Transform nearFull,Transform nearLow,Transform colliders)
        {
            float v=side*3.6f;
            var mesh=new Surface();
            mesh.Wall(start,end,v,0,2.56f,Steel);
            mesh.Wall(start,end,v,.02f,.13f,Ink);
            mesh.Wall(start,end,v,.17f,.22f,Light);
            mesh.Wall(start,end,v,2.48f,2.59f,Light);
            mesh.Wall(start,end,v,2.60f,2.66f,Ink);
            mesh.Save(side<0?far:nearFull,"WallModule",side<0?-800:4200);
            if(side>0)
            {
                var cut=new Surface();cut.Prism(start,3.51f,end,3.80f,0,.25f,Ink,Steel);
                cut.Tile(start,3.53f,end,3.61f,.255f,Light);
                cut.Save(nearLow,"CutawayWallCap",4200);
            }
            Footprint(colliders,"WallCollider",(start+end)*.5f,v,end-start,.23f);
        }

        private static void OriginalWindow(Transform parent,float u,float v,float h,float width,int order)
        {
            var sprite=Original("subway_window");float scale=width/(sprite.rect.width/100f);
            Vector2 center=Project(u,v,h)+new Vector2(sprite.rect.width,sprite.rect.height)*(.5f*scale/100f);
            DrawSprite(parent,"OriginalStationWindow",sprite,center,scale,order);
        }

        private static GameObject CreateDoorPrefab()
        {
            var root=Group(null,"SubwayDoor_QuarterView");
            var fixedParts=Group(root,"FixedFrame");
            var full=Group(fixedParts,"FullHeight");var low=Group(fixedParts,"Cutaway");
            var recess=new Surface();recess.Wall(-1.27f,1.27f,0,0,2.28f,Hex("233137"));recess.Save(full,"OpenDoorRecess",-1);
            var frame=new Surface();
            frame.Wall(-1.40f,-1.27f,0,0,2.40f,Shadow);frame.Wall(1.27f,1.40f,0,0,2.40f,Shadow);
            frame.Wall(-1.35f,-1.30f,0,.02f,2.40f,Light);frame.Wall(1.30f,1.35f,0,.02f,2.40f,Light);
            frame.Wall(-1.40f,1.40f,0,2.29f,2.42f,Light);frame.Wall(-1.40f,1.40f,0,2.42f,2.47f,Ink);
            frame.Wall(-.16f,.16f,0,2.31f,2.38f,Hex("AC5148"));frame.Save(full,"SteelJambsAndHeader",10);
            var threshold=new Surface();threshold.Tile(-1.4f,-.20f,1.4f,.20f,.025f,Shadow);
            threshold.Tile(-1.32f,-.06f,1.32f,.06f,.03f,Light);threshold.Save(root,"FixedSill",11);
            var cutFrame=new Surface();cutFrame.Prism(-1.4f,-.14f,-1.27f,.14f,0,.28f,Ink,Light);
            cutFrame.Prism(1.27f,-.14f,1.4f,.14f,0,.28f,Ink,Light);cutFrame.Save(low,"CutawayJambCaps",10);
            Transform left=null,right=null;
            foreach(int side in new[]{-1,1})
            {
                var leaf=Group(root,side<0?"LeftLeaf":"RightLeaf");leaf.localPosition=Project(side*.635f,0);
                var leafFull=Group(leaf,"FullHeight");var leafLow=Group(leaf,"Cutaway");
                var face=new Surface();Rect uv=side<0?new Rect(290f/2172,145f/724,790f/2172,420f/724):new Rect(1092f/2172,145f/724,790f/2172,420f/724);
                face.Quad(Project(-.635f,0,.06f),Project(.635f,0,.06f),Project(.635f,0,2.27f),Project(-.635f,0,2.27f),Color.white,
                    new Vector2(uv.xMin,uv.yMin),new Vector2(uv.xMax,uv.yMin),new Vector2(uv.xMax,uv.yMax),new Vector2(uv.xMin,uv.yMax));
                face.Save(leafFull,"DoorSkin",0,doorMaterial);
                var cutSkin=new Surface();cutSkin.Prism(-.635f,-.075f,.635f,.075f,0,.23f,Shadow,Steel);
                cutSkin.Wall(-.635f,.635f,.075f,.015f,.06f,Gold);cutSkin.Save(leafLow,"CutawayLeafCap",0);
                LocalFootprint(leaf,"LeafCollider",0,0,1.27f,.17f);
                if(side<0)left=leaf;else right=leaf;
            }
            LocalFootprint(root,"LeftJambCollider",-1.35f,0,.14f,.25f);
            LocalFootprint(root,"RightJambCollider",1.35f,0,.14f,.25f);
            root.gameObject.AddComponent<SubwayQuarterDoor>().Configure(left,right,1.27f);
            foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name=="Cutaway")t.gameObject.SetActive(false);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,DoorPrefab);
            UnityEngine.Object.DestroyImmediate(root.gameObject);return prefab;
        }

        private static void Pole(Transform parent,Transform collision,float u,float v,float height)
        {
            Vector2 p=Project(u,v);var pole=new Surface();
            pole.Tile(u-.13f,v-.13f,u+.13f,v+.13f,0,Ink);
            pole.Quad(p+new Vector2(-.04f,0),p+new Vector2(.04f,0),p+new Vector2(.04f,height),p+new Vector2(-.04f,height),Shadow);
            pole.Quad(p+new Vector2(-.027f,0),p+new Vector2(-.002f,0),p+new Vector2(-.002f,height),p+new Vector2(-.027f,height),Light);
            pole.Save(parent,"StainlessSeatEndPole",Order(p)+5);
            var go=Group(collision,"PoleFootCollider");go.localPosition=p;go.gameObject.AddComponent<CircleCollider2D>().radius=.065f;
        }

        private static void Ends(Transform shell,Transform detail,Transform collision)
        {
            foreach(int end in new[]{-1,1})
            {
                float u=end*18.95f;float height=end>0?2.55f:.38f;
                var wall=new Surface();
                foreach(int side in new[]{-1,1})
                {
                    float v0=side<0?-3.65f:1.0f,v1=side<0?-1.0f:3.65f;
                    wall.EndWall(u,v0,v1,0,height,Shadow);wall.EndWall(u,v0,v1,height,height+.08f,Light);
                    Footprint(collision,"EndBulkheadCollider",u,(v0+v1)*.5f,.24f,v1-v0);
                }
                wall.Save(shell,end>0?"FarEndBulkhead":"NearEndCutaway",end>0?-650:4700);
                var gangway=new Surface();
                gangway.Tile(u-.25f,-1.05f,u+.4f,1.05f,-.01f,Ink);
                for(int i=0;i<6;i++) gangway.Tile(u-.16f+i*.075f,-.95f,u-.14f+i*.075f,.95f,.01f,Shadow);
                if(end>0)
                {
                    gangway.EndWall(u,-1.02f,1.02f,0,2.34f,Ink);
                    gangway.EndWall(u,-.90f,.90f,.10f,2.26f,Steel);
                    gangway.EndWall(u,-.55f,.55f,.95f,1.92f,Hex("3C515A"));
                    gangway.EndWall(u,-.025f,.025f,.1f,2.26f,Ink);
                }
                gangway.Save(shell,"GangwayDoorAndBellows",end>0?-640:4701);
                Footprint(collision,"GangwayBoundary",u+end*.24f,0,.16f,2.06f);
            }
            Label(detail,"1001",Project(18.7f,-2.7f,2.25f),.17f,Ink,-26.565f,-580);
        }

        private static void Equipment(Transform parent)
        {
            var extinguisher=new Surface();float u=-18.4f,v=-3.43f;
            extinguisher.Wall(u-.17f,u+.17f,v,.17f,.80f,Ink);
            extinguisher.Wall(u-.12f,u+.12f,v,.21f,.69f,Hex("A3564C"));
            extinguisher.Wall(u-.09f,u+.09f,v,.43f,.52f,Light);
            extinguisher.Save(parent,"ExtinguisherAndCase",-420);
            foreach(float center in new[]{-9f,9f})
            {
                var route=new Surface();route.Wall(center-1.6f,center+1.6f,-3.595f,2.44f,2.63f,Ink);
                route.Wall(center-1.55f,center+1.55f,-3.59f,2.46f,2.61f,Hex("D5D8CA"));
                route.Wall(center-1.42f,center+1.42f,-3.585f,2.53f,2.56f,Gold);
                for(int n=0;n<7;n++)route.Wall(center-1.35f+n*.45f-.025f,center-1.35f+n*.45f+.025f,-3.58f,2.50f,2.59f,Ink);
                route.Save(parent,"RouteDiagram",-380);
            }
        }

        private static Transform Group(Transform parent,string name)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;
        }
        private static Transform Point(Transform parent,string name,float u,float v)
        {
            var go=Group(parent,name);go.localPosition=Project(u,v);return go;
        }
        private static Sprite Original(string name) => AssetDatabase.LoadAllAssetsAtPath(Sprites+name+".png").OfType<Sprite>().First();
        private static int Order(Vector2 foot) => 2400-Mathf.RoundToInt(foot.y*50f);
        private static void DrawSprite(Transform parent,string name,Sprite sprite,Vector2 position,float scale,int order)
        {
            var t=Group(parent,name);t.localPosition=position;t.localScale=Vector3.one*scale;
            var renderer=t.gameObject.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sharedMaterial=spriteMaterial;renderer.sortingOrder=order;
        }
        private static void Footprint(Transform parent,string name,float u,float v,float width,float depth)
        {
            var go=Group(parent,name);go.localPosition=Project(u,v);LocalFootprint(go,name,0,0,width,depth);
        }
        private static void LocalFootprint(Transform parent,string name,float u,float v,float width,float depth)
        {
            var collider=parent.gameObject.AddComponent<PolygonCollider2D>();
            collider.points=new[]{Project(u-width*.5f,v-depth*.5f),Project(u+width*.5f,v-depth*.5f),Project(u+width*.5f,v+depth*.5f),Project(u-width*.5f,v+depth*.5f)};
        }
        private static void Label(Transform parent,string text,Vector2 position,float height,Color color,float angle,int order)
        {
            if(font==null)return;var t=Group(parent,text);t.localPosition=position;t.localRotation=Quaternion.Euler(0,0,angle);
            var mesh=t.gameObject.AddComponent<TextMesh>();mesh.font=font;mesh.fontSize=64;mesh.characterSize=height/6.4f;
            mesh.text=text;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.color=color;
            var renderer=t.GetComponent<MeshRenderer>();renderer.sharedMaterial=font.material;renderer.sortingOrder=order;
        }
        private static Color Hex(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var c);return c;}

        private sealed class Surface
        {
            private readonly List<Vector3> vertices=new List<Vector3>();
            private readonly List<int> triangles=new List<int>();
            private readonly List<Vector2> uv=new List<Vector2>();
            private readonly List<Color> colors=new List<Color>();
            public void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
                => Quad(a,b,c,d,color,Vector2.zero,Vector2.right,Vector2.one,Vector2.up);
            public void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color,Vector2 ta,Vector2 tb,Vector2 tc,Vector2 td)
            {
                int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});uv.Add(ta);uv.Add(tb);uv.Add(tc);uv.Add(td);
                Color linear=QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color;
                for(int i=0;i<4;i++)colors.Add(linear);
            }
            public void Tile(float u0,float v0,float u1,float v1,float h,Color color)
                => Quad(Project(u0,v0,h),Project(u1,v0,h),Project(u1,v1,h),Project(u0,v1,h),color);
            public void Wall(float u0,float u1,float v,float h0,float h1,Color color)
                => Quad(Project(u0,v,h0),Project(u1,v,h0),Project(u1,v,h1),Project(u0,v,h1),color);
            public void EndWall(float u,float v0,float v1,float h0,float h1,Color color)
                => Quad(Project(u,v0,h0),Project(u,v1,h0),Project(u,v1,h1),Project(u,v0,h1),color);
            public void Prism(float u0,float v0,float u1,float v1,float h0,float h1,Color face,Color top)
            {
                Wall(u0,u1,v1,h0,h1,face);EndWall(u0,v0,v1,h0,h1,face*.85f);Tile(u0,v0,u1,v1,h1,top);
            }
            public void Save(Transform parent,string name,int order,Material material=null)
            {
                var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.RecalculateBounds();
                string path=Art+"Geometry/Quarter_"+(meshIndex++).ToString("D3")+".asset";
                mesh.name=Path.GetFileNameWithoutExtension(path);
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing!=null)
                {
                    // SetVertices/SetTriangles also invalidate GPU buffers during an in-editor rebuild.
                    existing.Clear();existing.SetVertices(vertices);existing.SetTriangles(triangles,0);
                    existing.SetUVs(0,uv);existing.SetColors(colors);existing.RecalculateBounds();
                    existing.name=Path.GetFileNameWithoutExtension(path);
                    UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);
                }
                else AssetDatabase.CreateAsset(mesh,path);
                var t=Group(parent,name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=t.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material!=null?material:solidMaterial;renderer.sortingOrder=order;
            }
        }
    }
}
#endif

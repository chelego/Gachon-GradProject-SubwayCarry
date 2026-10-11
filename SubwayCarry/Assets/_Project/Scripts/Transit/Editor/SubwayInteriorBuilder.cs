#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SubwayCarry.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SubwayCarry.Transit.Editor
{
    /// <summary>Authors the standalone top-down carriage without touching reference scenes.</summary>
    public static class SubwayInteriorBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Subway_TopDown.unity";
        private const string Art = "Assets/_Project/Art/Sprites/SubwayTopDown/";
        private const string PrefabPath = "Assets/_Project/Prefabs/Train/SubwayDoor_TopDown.prefab";
        private static readonly Color Ink = Hex("293238");
        private static readonly Color Metal = Hex("AEB9BB");
        private static readonly Color Light = Hex("DCE2DF");
        private static readonly Color Glass = Hex("35474E");
        private static readonly Color Gold = Hex("BEAB64");
        private static Sprite solid;
        private static Sprite poleSprite;
        private static Material material;
        private static Font font;
        private static readonly float[] DoorXs = { -13.5f, -4.5f, 4.5f, 13.5f };
        private static readonly float[] BankXs = { -17.1f, -9f, 0f, 9f, 17.1f };
        private static readonly int[] BankSeats = { 3, 7, 7, 7, 3 };

        [MenuItem("SubwayCarry/Train/Create Top Down Interior (missing only)")]
        public static void BuildMissing()
        {
            if (File.Exists(ScenePath))
            {
                if (SceneManager.GetActiveScene().path == ScenePath) return;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty)
                        throw new InvalidOperationException("Save open scenes before opening the carriage.");
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            Build();
        }

        public static void RebuildGenerated() => Build();

        private static void Build()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before building the carriage.");
            PrepareAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = Group("Subway_TopDown", null);
            var architecture = Group("01_CarBody", root);
            var flooring = Group("02_LinoleumFloor", root);
            var windows = Group("03_WindowsAndPanels", root);
            var seating = Group("04_Seats_54", root);
            var doors = Group("05_SlidingDoors_8", root);
            var detail = Group("06_GrabRailsAndSigns", root);
            var integration = Group("07_IntegrationAnchors", root);
            var seatAnchors = Group("SeatAnchors", integration);
            var doorAnchors = Group("DoorAnchors", integration);
            Group("PlayerSpawn", integration).localPosition = Vector3.zero;
            Group("WalkableBounds_Min", integration).localPosition = new Vector3(-18.2f, -3.6f, 0f);
            Group("WalkableBounds_Max", integration).localPosition = new Vector3(18.2f, 3.6f, 0f);

            Rect("OuterShadow", architecture, 0f, -.17f, 39.2f, 10.25f, Hex("151D22"), -50);
            Rect("OuterShell", architecture, 0f, 0f, 38.9f, 9.96f, Ink, -40);
            Rect("SteelRim", architecture, 0f, 0f, 38.72f, 9.76f, Metal, -39);
            Rect("InteriorWell", architecture, 0f, 0f, 38.25f, 9.26f, Hex("687579"), -38);
            var floor = SpriteObject("SpeckledLinoleum", flooring, Load("Floor", null), 0, 0, 1, 1, -30);
            floor.transform.localScale = Vector3.one;
            floor.drawMode = SpriteDrawMode.Tiled;
            floor.size = new Vector2(37.75f, 8.95f);
            floor.tileMode = SpriteTileMode.Continuous;
            for (int side = -1; side <= 1; side += 2)
            {
                Rect("AisleEdge", flooring, 0f, side * 1.95f, 37.4f, .055f, Hex("909791"), -28);
                Rect("SeatZone", flooring, 0f, side * 3.17f, 37.4f, 1.94f, new Color(.45f, .48f, .46f, .10f), -29);
                Rect("OuterWallLip", architecture, 0f, side * 4.72f, 38.45f, .18f, Light, 9);
                Rect("OuterWallSeam", architecture, 0f, side * 4.58f, 38.35f, .06f, Ink, 10);
                // Wall colliders leave the four door apertures genuinely open.
                float start = -19f;
                foreach (float dx in DoorXs)
                {
                    WallSegment(architecture, side, start, dx - 1.5f);
                    start = dx + 1.5f;
                }
                WallSegment(architecture, side, start, 19f);
            }
            for (int i = -5; i <= 5; i++)
                Rect("FloorWeld", flooring, i * 3.2f, 0f, .015f, 3.84f, new Color(.28f, .32f, .31f, .17f), -27);

            foreach (float x in DoorXs)
            {
                Rect("VestibuleInset", flooring, x, 0, 2.9f, 8.92f, new Color(.36f, .40f, .38f, .075f), -26);
                foreach (int side in new[] { -1, 1 })
                {
                    Rect("ThresholdShadow", flooring, x, side * 3.73f, 2.88f, .64f, Hex("707A78"), -20);
                    Rect("YellowSafetyEdge", flooring, x, side * 3.38f, 2.74f, .075f, Gold, -18);
                    for (int t = 0; t < 9; t++)
                        Rect("ThresholdRib", flooring, x - 1.20f + t * .30f, side * 3.72f, .025f, .38f, Hex("A7AFAD"), -19);
                    Rect("StandingLine", flooring, x, side * 2.37f, 1.05f, .035f, new Color(.42f, .46f, .45f, .40f), -22);
                }
            }

            int seatIndex = 0;
            foreach (int side in new[] { 1, -1 })
            {
                for (int bankIndex = 0; bankIndex < BankXs.Length; bankIndex++)
                {
                    float x = BankXs[bankIndex];
                    int count = BankSeats[bankIndex];
                    float width = count * .80f;
                    var bank = Group((side > 0 ? "North" : "South") + "_Bench_" + (bankIndex + 1), seating);
                    Rect("BenchShadow", bank, x + .07f, side * 2.88f - .08f, width + .18f, 1.52f, new Color(.16f,.20f,.20f,.35f), -2);
                    Rect("SteelPlinth", bank, x, side * 3.00f, width + .10f, 1.36f, Hex("66767B"), 1);
                    Rect("FrontRail", bank, x, side * 2.34f, width + .06f, .09f, Ink, 3);
                    Box("SeatBankCollider", bank, x, side * 2.98f, width + .08f, 1.40f);
                    for (int s = 0; s < count; s++)
                    {
                        float sx = x + (s - (count - 1) * .5f) * .80f;
                        var chair = SpriteObject("Seat_" + (++seatIndex).ToString("00"), bank, Load("Seat", "Seat"), sx, side * 2.99f, .78f, 1.32f, 5);
                        if (side < 0) chair.transform.localRotation = Quaternion.Euler(0, 0, 180);
                        var anchor = Group("Seat_" + seatIndex.ToString("00"), seatAnchors);
                        anchor.localPosition = new Vector3(sx, side * 2.63f, 0);
                        anchor.localRotation = Quaternion.Euler(0, 0, side > 0 ? 0 : 180);
                    }
                    foreach (int end in new[] { -1, 1 })
                    {
                        float ex = x + end * (width * .5f + .045f);
                        Rect("EndDividerOutline", bank, ex, side * 2.98f, .10f, 1.52f, Ink, 7);
                        Rect("EndDividerSilver", bank, ex - .01f, side * 2.98f, .055f, 1.41f, Light, 8);
                        Pole(detail, ex, side * 2.21f);
                    }
                    WindowBank(windows, x, side, width, count > 3 ? 3 : 1);
                    if (bankIndex == 0 || bankIndex == 4)
                    {
                        Rect("PriorityPlate", detail, x, side * 3.82f, .86f, .15f, Hex("9C7387"), 14);
                        Label("PrioritySeats", detail, "우선석", x, side * 3.82f, .105f, Light, 15);
                    }
                }
            }
            var doorPrefab = BuildDoorPrefab();
            var controllers = new List<TrainDoorController>();
            int doorIndex = 0;
            foreach (int side in new[] { 1, -1 })
                foreach (float x in DoorXs)
                {
                    var door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
                    door.name = (side > 0 ? "North" : "South") + "_Door_" + (++doorIndex).ToString("00");
                    door.transform.SetParent(doors, false);
                    door.transform.localPosition = new Vector3(x, side * 4.11f, 0f);
                    door.transform.localRotation = Quaternion.Euler(0, 0, side > 0 ? 0 : 180);
                    controllers.Add(door.GetComponent<TrainDoorController>());
                    var anchor = Group("Door_" + doorIndex.ToString("00"), doorAnchors);
                    anchor.localPosition = new Vector3(x, side * 3.05f, 0);
                    var outside = Group("Outside", anchor);
                    outside.localPosition = new Vector3(0, side * 2.15f, 0);
                    Label("DoorNumber", detail, "1-" + (((doorIndex - 1) % 4) + 1), x, side * 4.78f, .105f, Ink, 16);
                }

            EndBulkhead(architecture, detail, -1);
            EndBulkhead(architecture, detail, 1);
            RoutePanel(detail, -9f, 1);
            RoutePanel(detail, 9f, -1);
            Rect("ExtinguisherCase", detail, -18.44f, 1.64f, .34f, .68f, Ink, 14);
            Rect("Extinguisher", detail, -18.44f, 1.61f, .21f, .45f, Hex("98554A"), 15);
            Rect("ExtinguisherLabel", detail, -18.44f, 1.60f, .17f, .09f, Light, 16);
            Rect("ExtinguisherHandle", detail, -18.44f, 1.89f, .15f, .06f, Metal, 16);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 0, -10);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.1f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Hex("20292F");
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<SubwayInteriorPreview>().Configure(controllers.ToArray());
            var overviewObject = new GameObject("Overview Camera (disabled)");
            overviewObject.transform.position = new Vector3(0, 0, -10);
            var overview = overviewObject.AddComponent<Camera>();
            overview.CopyFrom(camera);
            overview.orthographicSize = 11.8f;
            overview.enabled = false;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.in2DMode = true;
                SceneView.lastActiveSceneView.Frame(new Bounds(Vector3.zero, new Vector3(40f,10.5f,1f)), false);
            }
            Selection.activeGameObject = root.gameObject;
            Debug.Log("Subway_TopDown saved: 54 seats, 8 animated doors, 10 bench colliders and clean center aisle.");
        }

        private static void PrepareAssets()
        {
            Directory.CreateDirectory("Assets/_Project/Prefabs/Train");
            solid = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/StationSolid.png");
            if (solid == null) throw new InvalidOperationException("StationSolid sprite is required.");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Art/Fonts/NanumGothic-Regular.ttf");
            var spriteShader = ProjectShaderConnections.RequireShader(
                "Packages/com.unity.render-pipelines.universal/Shaders/2D/Sprite-Unlit-Default.shader");
            material = AssetDatabase.LoadAssetAtPath<Material>(Art + "InteriorUnlit.mat");
            if (material == null)
            {
                material = new Material(spriteShader);
                AssetDatabase.CreateAsset(material, Art + "InteriorUnlit.mat");
            }
            else if (material.shader != spriteShader)
            {
                material.shader = spriteShader;
                EditorUtility.SetDirty(material);
            }
            string polePath = Art + "PoleCap.png";
            if (!File.Exists(polePath))
            {
                var texture = new Texture2D(24,24,TextureFormat.RGBA32,false);
                for (int y=0;y<24;y++) for(int x=0;x<24;x++)
                {
                    float d = Vector2.Distance(new Vector2(x,y), new Vector2(11.5f,11.5f));
                    Color c = d>10f ? Color.clear : d>8.5f ? Ink : d>6.5f ? Hex("89999D") : d>5f ? Light : Metal;
                    texture.SetPixel(x,y,c);
                }
                texture.Apply(); File.WriteAllBytes(polePath, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(polePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(polePath);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.spritePixelsPerUnit = 64;
                importer.alphaIsTransparency = true; importer.SaveAndReimport();
            }
            poleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(polePath);
        }

        private static Sprite Load(string file, string name)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Art + file + ".png").OfType<Sprite>();
            var sprite = name == null ? sprites.FirstOrDefault() : sprites.FirstOrDefault(s => s.name == name);
            if (sprite == null) throw new InvalidOperationException("Missing sprite: " + file + "/" + name);
            return sprite;
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static SpriteRenderer SpriteObject(string name, Transform parent, Sprite sprite, float x, float y, float width, float height, int order)
        {
            var go = Group(name, parent);
            go.localPosition = new Vector3(x, y, 0);
            go.localScale = new Vector3(width / sprite.bounds.size.x, height / sprite.bounds.size.y, 1);
            var renderer = go.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = material; renderer.sortingOrder = order;
            return renderer;
        }

        private static SpriteRenderer Rect(string name, Transform parent, float x, float y, float width, float height, Color color, int order)
        {
            var renderer = SpriteObject(name, parent, solid, x, y, width, height, order);
            renderer.color = color; return renderer;
        }

        private static void Box(string name, Transform parent, float x, float y, float width, float height)
        {
            var go = Group(name, parent); go.localPosition = new Vector3(x,y,0);
            go.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(width,height);
        }

        private static void WallSegment(Transform parent, int side, float start, float end)
        {
            float x = (start + end) * .5f, width = end - start;
            Rect("WallPanel", parent, x, side * 4.15f, width, .80f, Metal, 0);
            Rect("WallInnerRail", parent, x, side * 3.73f, width, .07f, Ink, 2);
            Box("WallCollider", parent, x, side * 4.22f, width, .67f);
        }

        private static void WindowBank(Transform parent, float x, int side, float width, int panes)
        {
            float paneWidth = (width - .24f) / panes;
            for (int i=0;i<panes;i++)
            {
                float wx = x + (i - (panes-1)*.5f)*paneWidth;
                Rect("WindowSeal", parent, wx, side*4.12f, paneWidth-.09f, .47f, Ink, 3);
                Rect("WindowGlass", parent, wx, side*4.12f, paneWidth-.16f, .34f, Glass, 4);
                Rect("GlassReflection", parent, wx, side*4.18f, paneWidth-.24f, .06f, Hex("52666D"), 5);
                Rect("WindowSill", parent, wx, side*3.88f, paneWidth-.05f, .07f, Light, 6);
            }
        }

        private static void Pole(Transform parent, float x, float y)
        {
            var pole = SpriteObject("GrabPole", parent, poleSprite, x, y, .21f, .21f, 12);
            var collider = pole.gameObject.AddComponent<CircleCollider2D>();
            collider.radius = poleSprite.bounds.size.x * .40f;
            Rect("GrabRail", parent, x, y + Mathf.Sign(y)*.18f, .035f, .38f, Light, 11);
        }

        private static GameObject BuildDoorPrefab()
        {
            var root = Group("SubwayDoor_TopDown", null);
            Rect("DoorRecess",root,0,0,3f,.95f,Ink,10);
            var left = SpriteObject("subway_door_leaf_left",root,Load("Door","LeftLeaf"),-.672f,0,1.344f,.75f,11);
            var right = SpriteObject("subway_door_leaf_right",root,Load("Door","RightLeaf"),.672f,0,1.344f,.75f,11);
            var aperture = Group("DoorApertureMask", root);
            aperture.localScale = new Vector3(2.70f / solid.bounds.size.x, .78f / solid.bounds.size.y, 1f);
            var mask = aperture.gameObject.AddComponent<SpriteMask>();
            mask.sprite = solid; mask.isCustomRangeActive = true;
            mask.frontSortingOrder = 11; mask.backSortingOrder = 10;
            foreach(var leaf in new[]{left,right})
            {
                leaf.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                var collider = leaf.gameObject.AddComponent<BoxCollider2D>(); collider.size = leaf.sprite.bounds.size;
                var body = leaf.gameObject.AddComponent<Rigidbody2D>();body.bodyType = RigidbodyType2D.Kinematic;body.useFullKinematicContacts = true;
            }
            // Fixed jambs render above leaves as they retract along the wall.
            SpriteObject("subway_door_fixed_Header",root,Load("Door","Header"),0,.429f,2.91f,.10f,14);
            SpriteObject("subway_door_fixed_Sill",root,Load("Door","Sill"),0,-.428f,2.81f,.10f,14);
            SpriteObject("subway_door_fixed_LeftJamb",root,Load("Door","LeftJamb"),-1.455f,0,.17f,.97f,15);
            SpriteObject("subway_door_fixed_RightJamb",root,Load("Door","RightJamb"),1.455f,0,.17f,.97f,15);
            Box("LeftJambCollider",root,-1.45f,0,.18f,.97f);
            Box("RightJambCollider",root,1.45f,0,.18f,.97f);
            var door = root.gameObject.AddComponent<TrainDoorController>(); door.Configure(left.transform,right.transform);
            var serialized = new SerializedObject(door);
            serialized.FindProperty("slideDistance").floatValue = 1.344f;
            serialized.FindProperty("transitionDuration").floatValue = .65f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject,PrefabPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        private static void EndBulkhead(Transform parent, Transform detail, int side)
        {
            float x=side*18.94f;
            foreach(int end in new[]{-1,1})
            {
                Rect("EndWallOutline",parent,x,end*2.84f,.52f,3.65f,Ink,9);
                Rect("EndWallPanel",parent,x-.025f,end*2.84f,.36f,3.49f,Metal,10);
                Box("EndWallCollider",parent,x,end*2.84f,.52f,3.65f);
                Rect("ServiceCabinet",detail,side*18.48f,end*3.18f,.40f,1.12f,Ink,12);
                Rect("CabinetFace",detail,side*18.48f,end*3.18f,.30f,1.02f,Hex("BBC4C2"),13);
                Rect("CabinetHandle",detail,side*18.43f,end*3.18f,.035f,.18f,Glass,14);
            }
            Rect("GangwaySeal",parent,x,0,.62f,2.15f,Ink,9);
            Rect("GangwayGlass",parent,x,0,.37f,1.89f,Glass,10);
            Rect("GangwayCenterSeam",parent,x,0,.41f,.045f,Ink,11);
            Rect("GangwayThreshold",parent,side*18.54f,0,.08f,2.14f,Metal,11);
            Box("GangwayEndBoundary",parent,side*19.14f,0,.20f,2.2f);
            for(int t=0;t<6;t++) Rect("GangwayBellows",parent,side*(19.23f+t*.055f),0,.02f,2.1f,Hex("657175"),10);
            Label("CarNumber",detail,"1001",side*18.36f,-1.24f,.12f,Ink,16);
        }

        private static void RoutePanel(Transform parent, float x, int side)
        {
            Rect("RoutePanelFrame",parent,x,side*4.74f,4.15f,.28f,Ink,16);
            Rect("RoutePanelPaper",parent,x,side*4.74f,4.07f,.22f,Hex("D3D5C7"),17);
            Rect("RouteLine",parent,x,side*4.75f,3.55f,.026f,Gold,18);
            for(int i=0;i<7;i++) Rect("StationDot",parent,x-1.65f+i*.55f,side*4.75f,.065f,.065f,Ink,19);
        }

        private static void Label(string name, Transform parent, string text, float x, float y, float size, Color color, int order)
        {
            if(font==null) return;
            var go=Group(name,parent);go.localPosition=new Vector3(x,y,-.01f);
            var mesh=go.gameObject.AddComponent<TextMesh>(); mesh.text=text;mesh.font=font;mesh.fontSize=48;
            mesh.characterSize=size*.28f;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.color=color;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=font.material;renderer.sortingOrder=order;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#"+hex,out Color color);return color;
        }
    }
}
#endif

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
    /// <summary>Photo-proportioned, editable reconstruction. Coordinates are metres, +Z toward campus.</summary>
    public static class GachonHubSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/GachonUniv_Hub.unity";
        public const float EntryWallX = 13.1f;
        public const float EntryZ = 4f;
        public const float StairX = 9.9f;
        public const float StairWidth = 5.2f;
        public const float AscentRun = 9.5f;
        public const float AscentTopZ = 17f;
        public const float AscentBottomZ = AscentTopZ-AscentRun;
        public const float AscentRise = 5.1f;
        public const float FreedomWidth = 26f;
        public const float FreedomLength = 58f;
        public static Quaternion ApproachRotation => Quaternion.Euler(0,-90,0);
        public static Vector3 ApproachPoint(Vector3 point) => new Vector3(EntryWallX,0,EntryZ)+ApproachRotation*point;
        public static Vector3 ApproachLocal(Vector3 point) => Quaternion.Inverse(ApproachRotation)*(point-new Vector3(EntryWallX,0,EntryZ));
        private const string Art = "Assets/_Project/Art/GachonUnivHub";
        private static Transform root;
        private static Font font;
        private static Material ivory, trim, gold, dark, steel, glass, wood, warm, navy, yellow, green, water, sky, fontMat, stone;
        private static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        private static CharacterController visitor;

        [MenuItem("SubwayCarry/Gachon Hub/Create scene (missing only)")]
        public static void CreateMissing()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("GachonUniv_Hub already exists. Open it to edit; creation never overwrites a scene.");
            Build();
        }

        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            for (int i=0;i<SceneManager.sceneCount;i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene before building.");
            Prepare();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            root = Group(null, "GachonUniv_Hub");
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create Gachon University hub");
            SetupEnvironment();
            CreateVisitor();
            var approach=Group(root,"01_StationApproachFrame");approach.localPosition=new Vector3(EntryWallX,0,EntryZ);approach.localRotation=ApproachRotation;
            Concourse();var station=root.Find("01_StationConcourse");station.SetParent(approach,false);station.localPosition=Vector3.forward*7;station.localScale=new Vector3(.65f,1,1);
            Connector();var connector=root.Find("02_UniversityConnectingPassage");connector.SetParent(approach,false);connector.localScale=new Vector3(.65f,.89f,.5f);
            Atrium();
            Campus();
            var anchors = Group(root, "08_IntegrationAnchors");
            Anchor(anchors,"StationConcourse",ApproachPoint(new Vector3(0,0,-18)));
            Anchor(anchors,"FareGate",ApproachPoint(new Vector3(0,0,-8)));
            Anchor(anchors,"FareGateExitView",ApproachPoint(new Vector3(0,0,-5.5f)));
            Anchor(anchors,"FountainPlaza",ApproachPoint(new Vector3(0,0,2)));
            Anchor(anchors,"StairBottom",new Vector3(StairX,0,AscentBottomZ));
            Anchor(anchors,"CampusArrival",new Vector3(StairX,5.1f,22));
            Anchor(anchors,"FuturePlatformConnection",ApproachPoint(new Vector3(0,0,-24)));
            Anchor(anchors,"DownEscalatorLeftTurn",new Vector3(3.6f,0,3.8f));
            Anchor(anchors,"FreedomFarEnd",new Vector3(3,5.1f,78));
            var atlas = root.gameObject.AddComponent<SubwaySignFont3D>();
            atlas.font=font; atlas.signMaterial=fontMat;
            string characters="";
            foreach(var text in root.GetComponentsInChildren<TextMesh>()) characters += text.text;
            font.RequestCharactersInTexture(characters,64,FontStyle.Normal);
            fontMat.SetTexture("_BaseMap",font.material.mainTexture);
            PreviewCamera("Preview_Atrium",new Vector3(4.8f,1.8f,.8f),new Vector3(3.6f,4.1f,12),78);
            PreviewCamera("Preview_Concourse",ApproachPoint(new Vector3(0,1.7f,-19)),ApproachPoint(new Vector3(0,2,-2)),64);
            PreviewCamera("Preview_Exit",ApproachPoint(new Vector3(0,1.7f,1.2f)),ApproachPoint(new Vector3(3,3,13)),76);
            PreviewCamera("Preview_Campus",new Vector3(3,6.8f,25),new Vector3(3,9.2f,73),65);
            PreviewCamera("Preview_Portal",new Vector3(5,2.2f,2),new Vector3(EntryWallX,4,EntryZ),72);
            PreviewCamera("Preview_DescentLeft",new Vector3(3.6f,1.75f,3.8f),new Vector3(EntryWallX,2.8f,EntryZ),78);
            PreviewCamera("Preview_DescentWalk",new Vector3(3.6f,1.75f,AscentBottomZ-.8f),new Vector3(EntryWallX,2.8f,EntryZ),78);
            PreviewCamera("Preview_Banner",new Vector3(2.3f,5.2f,2.6f),new Vector3(.9f,5.2f,5.3f),50);
            SceneView.lastActiveSceneView?.LookAt(new Vector3(0,4,9),Quaternion.Euler(23,27,0),19,false,true);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath);
            Debug.Log("GachonUniv_Hub saved. Click Game view, WASD + mouse; Shift run, E tap card, Esc release, Home reset.");
        }

        private static void Prepare()
        {
            Directory.CreateDirectory(Art+"/Materials"); Directory.CreateDirectory(Art+"/Meshes"); Directory.CreateDirectory(Art+"/Textures");
            AssetDatabase.Refresh();
            meshes.Clear();
            ivory=Mat("Warm limestone","#E3DDCA",0,.28f);
            trim=Mat("Carved pale stone","#F2EBDC",0,.36f);
            gold=Mat("Brass ornaments","#BB9955",.65f,.55f);
            dark=Mat("Graphite","#202C32",.35f,.36f);
            steel=Mat("Brushed stainless steel","#A8B8BA",.7f,.5f);
            glass=Mat("Blue glass","#537E8A",.45f,.78f);
            wood=Mat("Walnut slats","#66432C",0,.3f);
            warm=Mat("Warm illuminated windows","#FFD09A",0,.3f,new Color(1,.62f,.26f)*.65f);
            navy=Mat("University navy","#102E5B",.1f,.32f);
            yellow=Mat("Tactile yellow","#E4BE40",.1f,.38f);
            green=Mat("Landscape foliage","#3B6548",0,.1f);
            stone=Mat("Water wall stone","#776D59",0,.2f);
            stone.SetTexture("_BaseMap",NoiseTexture("Stone",256,false));
            water=Mat("Reflecting pool","#216C76",.55f,.92f,new Color(.01f,.08f,.09f));
            water.SetTexture("_BaseMap",NoiseTexture("Ripples",256,false));
            sky=Mat("Painted sky ceiling","#FFFFFF",0,0);
            sky.shader=Shader.Find("Universal Render Pipeline/Unlit");
            sky.SetTexture("_BaseMap",NoiseTexture("CloudCeiling",1024,true));
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Art/Fonts/NanumGothic-Regular.ttf");
            if(!font) throw new InvalidOperationException("Korean font is required.");
            fontMat=Mat("Korean wayfinding","#FFFFFF",0,0);
            fontMat.shader=Shader.Find("SubwayCarry/World Sign");
            fontMat.renderQueue=2450;
            PreparePipeline();
        }
        public static void PreparePipeline()
        {
            string path=Art+"/GachonHubPipeline.asset";
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if(!pipeline)
            {
                pipeline=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/SubwayFirstPersonPipeline.asset"));
                pipeline.name="GachonHubPipeline";AssetDatabase.CreateAsset(pipeline,path);
            }
            var serialized=new SerializedObject(pipeline);
            serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue=2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static Material Mat(string n,string hex,float metal,float smooth,Color? emission=null)
        {
            string path=Art+"/Materials/"+n+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m) {m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            ColorUtility.TryParseHtmlString(hex,out var color);
            m.SetColor("_BaseColor",color); m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);
            if(emission.HasValue){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission.Value);}
            EditorUtility.SetDirty(m); return m;
        }
        private static Texture2D NoiseTexture(string name,int size,bool clouds)
        {
            string path=Art+"/Textures/"+name+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(saved) return saved;
            var t=new Texture2D(size,size,TextureFormat.RGB24,true){name=name,wrapMode=TextureWrapMode.Repeat};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=y/(float)size;
                float n=Mathf.PerlinNoise(u*7+13,v*7+5)*.56f+Mathf.PerlinNoise(u*17+27,v*17+4)*.28f+Mathf.PerlinNoise(u*41,v*41)*.16f;
                if(clouds)
                {
                    float c=Mathf.SmoothStep(.42f,.72f,n);
                    c=Mathf.Clamp01((n-.47f)*4.6f);
                    pixels[y*size+x]=Color.Lerp(new Color(.1f,.43f,.72f),new Color(.96f,.96f,.93f),c);
                }
                else
                {
                    float speckle=Mathf.PerlinNoise(x*.47f,y*.47f);
                    float k=.68f+n*.25f+speckle*.09f;
                    if(name=="Paving" && (x%64<2 || y%64<2)) k*=.68f;
                    pixels[y*size+x]=new Color(k,k,k);
                }
            }
            t.SetPixels(pixels);t.Apply();AssetDatabase.CreateAsset(t,path);return t;
        }
        private static Transform Group(Transform parent,string n)
        {var g=new GameObject(n);g.transform.SetParent(parent,false);return g.transform;}
        private static GameObject Box(Transform p,string n,Vector3 pos,Vector3 size,Material mat,bool collide=true)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);
            g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collide) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }
        private static GameObject Cylinder(Transform p,string n,Vector3 pos,float radius,float height,Material mat,bool collide=false)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.SetParent(p,false);
            g.transform.localPosition=pos;g.transform.localScale=new Vector3(radius*2,height*.5f,radius*2);
            g.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collide)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }
        private static GameObject MeshObject(Transform p,string n,Mesh mesh,Material mat, bool collider=false)
        {
            var g=new GameObject(n,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(p,false);
            g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<Renderer>().sharedMaterial=mat;
            if(collider)g.AddComponent<MeshCollider>().sharedMesh=mesh;
            return g;
        }
        private static Mesh SaveMesh(string name,List<Vector3> v,List<int> tri,List<Vector2> uv=null)
        {
            if(meshes.TryGetValue(name,out var cached))return cached;
            var m=new Mesh{name=name};m.SetVertices(v);m.SetTriangles(tri,0);
            if(uv!=null)m.SetUVs(0,uv); m.RecalculateNormals();m.RecalculateBounds();
            string path=Art+"/Meshes/"+name+".asset";var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(previous){EditorUtility.CopySerialized(m,previous);UnityEngine.Object.DestroyImmediate(m);m=previous;}
            else AssetDatabase.CreateAsset(m,path);
            meshes[name]=m;return m;
        }
        private static Mesh RingMesh(string name,float inner,float outer,float height,int count=64)
        {
            var v=new List<Vector3>();var tr=new List<int>();var uv=new List<Vector2>();
            for(int i=0;i<=count;i++)
            {
                float a=i*Mathf.PI*2/count;float x=Mathf.Cos(a),z=Mathf.Sin(a);
                v.Add(new Vector3(x*inner,0,z*inner));v.Add(new Vector3(x*outer,0,z*outer));
                v.Add(new Vector3(x*inner,height,z*inner));v.Add(new Vector3(x*outer,height,z*outer));
                for(int j=0;j<4;j++)uv.Add(new Vector2(i/(float)count,j*.3f));
                if(i<count){int b=i*4;
                    tr.AddRange(new[]{b,b+4,b+2,b+2,b+4,b+6,b+1,b+3,b+5,b+3,b+7,b+5,b+2,b+6,b+3,b+3,b+6,b+7});}
            }
            return SaveMesh(name,v,tr,uv);
        }
        private static void Ring(Transform p,string n,Vector3 pos,float inner,float outer,float h,Material mat,Vector3? scale=null,bool collide=false)
        {
            var g=MeshObject(p,n,RingMesh(n+"_"+inner+"_"+outer+"_"+h,inner,outer,h),mat,collide);
            g.transform.localPosition=pos;if(scale.HasValue)g.transform.localScale=scale.Value;
        }
        private static void Beam(Transform p,string n,Vector3 a,Vector3 b,float width,Material mat,bool collide=false)
        {
            var g=Box(p,n,(a+b)*.5f,new Vector3(width,width,Vector3.Distance(a,b)),mat,collide);
            g.transform.localRotation=Quaternion.LookRotation(b-a);
        }
        private static void Arch(Transform p,Vector3 pos,float radius,float thickness,Material mat)
        {
            string name="Arch_"+radius+"_"+thickness;
            if(!meshes.TryGetValue(name,out var m))
            {
                var v=new List<Vector3>();var tri=new List<int>();
                for(int i=0;i<=32;i++)
                {
                    float a=i*Mathf.PI/32;
                    for(int k=0;k<4;k++){float r=radius+(k%2)*thickness;v.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,k<2?-.12f:.12f));}
                    if(i<32){int b=i*4;tri.AddRange(new[]{b,b+4,b+1,b+1,b+4,b+5,b+2,b+3,b+6,b+3,b+7,b+6,b,b+4,b+2,b+2,b+4,b+6,b+1,b+3,b+5,b+3,b+7,b+5});}
                }
                m=SaveMesh(name,v,tri);
            }
            MeshObject(p,"Carved semicircular arch",m,mat).transform.localPosition=pos;
        }
        private static TextMesh Text(Transform p,string text,Vector3 pos,float size,Color color,float yaw=0)
        {
            var g=new GameObject("Sign • "+text.Replace("\n"," "));g.transform.SetParent(p,false);
            g.transform.localPosition=pos;g.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var t=g.AddComponent<TextMesh>();t.font=font;t.text=text;t.fontSize=64;t.characterSize=size;
            t.characterSize=size*.25f;
            t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;
            g.GetComponent<Renderer>().sharedMaterial=fontMat;return t;
        }
        private static Light LightAt(Transform p,string n,Vector3 pos,Color color,float intensity,float range)
        {
            var g=Group(p,n);g.localPosition=pos;var l=g.gameObject.AddComponent<Light>();
            l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;return l;
        }
        private static void Floor(Transform p,string n,Vector3 center,float width,float length,string tint)
        {
            var m=Mat(n,tint,0,.38f);m.SetTexture("_BaseMap",NoiseTexture("Paving",256,false));
            m.SetTextureScale("_BaseMap",new Vector2(width/4,length/4));
            Box(p,n+" slab",center-Vector3.up*.2f,new Vector3(width,.4f,length),ivory);
            var plane=GameObject.CreatePrimitive(PrimitiveType.Plane);plane.name=n+" jointed finish";plane.transform.SetParent(p,false);
            plane.transform.localPosition=center+Vector3.up*.005f;plane.transform.localScale=new Vector3(width/10,1,length/10);
            plane.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(plane.GetComponent<Collider>());
        }
        private static void Anchor(Transform p,string n,Vector3 point){Group(p,n).localPosition=point;}

        private static void SetupEnvironment()
        {
            var settings=Group(root,"00_LightingAndRendering");
            var bridge=settings.gameObject.AddComponent<SubwaySceneRenderPipeline3D>();
            bridge.pipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(Art+"/GachonHubPipeline.asset");
            bridge.originalPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/UniversalRP.asset");bridge.Apply();
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.61f,.7f,.8f);
            RenderSettings.ambientEquatorColor=new Color(.47f,.48f,.46f);
            RenderSettings.ambientGroundColor=new Color(.23f,.22f,.2f);
            RenderSettings.ambientIntensity=1;
            var skybox=Mat("Outdoor atmosphere","#91B9DB",0,0);skybox.shader=Shader.Find("Skybox/Procedural");
            skybox.SetFloat("_SunSize",.025f);skybox.SetFloat("_AtmosphereThickness",.9f);skybox.SetColor("_SkyTint",new Color(.55f,.63f,.72f));
            RenderSettings.skybox=skybox;
            var sun=Group(settings,"Daylight");sun.localRotation=Quaternion.Euler(48,-35,0);
            var light=sun.gameObject.AddComponent<Light>();light.type=LightType.Directional;light.color=new Color(1,.91f,.77f);light.intensity=1.3f;
            light.shadows=LightShadows.Soft;light.shadowBias=.035f;RenderSettings.sun=light;
        }
        private static void CreateVisitor()
        {
            var p=Group(root,"07_Visitor_WASD_Mouse_E_Card");p.localPosition=ApproachPoint(new Vector3(0,.06f,-18));p.localRotation=ApproachRotation;
            visitor=p.gameObject.AddComponent<CharacterController>();visitor.height=1.8f;visitor.radius=.28f;
            visitor.center=Vector3.up*.9f;visitor.stepOffset=.32f;visitor.slopeLimit=50;visitor.skinWidth=.035f;
            var eye=Group(p,"Main Camera");eye.localPosition=Vector3.up*1.63f;eye.tag="MainCamera";
            var camera=eye.gameObject.AddComponent<Camera>();camera.fieldOfView=68;camera.nearClipPlane=.05f;camera.farClipPlane=230;
            camera.allowHDR=true;eye.gameObject.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            var controller=p.gameObject.AddComponent<SubwayFirstPersonController>();controller.eye=eye;controller.walkSpeed=3;controller.sprintSpeed=5;
        }
        private static void PreviewCamera(string n,Vector3 pos,Vector3 look,float fov)
        {
            var p=Group(root,n);p.position=pos;p.LookAt(look);var c=p.gameObject.AddComponent<Camera>();
            c.fieldOfView=fov;c.nearClipPlane=.05f;c.farClipPlane=220;c.enabled=false;
            c.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            c.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.FastApproximateAntialiasing;
        }

        private static void Concourse()
        {
            var p=Group(root,"01_StationConcourse");
            Floor(p,"Concourse grey granite",new Vector3(0,0,-23),18,18,"#B4B5AF");
            Box(p,"West ceramic wall",new Vector3(-9,2,-23),new Vector3(.4f,4,18),ivory);
            Box(p,"East ceramic wall",new Vector3(9,2,-23),new Vector3(.4f,4,18),ivory);
            Box(p,"Station rear wall",new Vector3(0,2,-32),new Vector3(18,4,.4f),ivory);
            Box(p,"Station ceiling",new Vector3(0,4.15f,-23),new Vector3(18,.3f,18),trim);
            for(int z=-30;z<=-16;z+=3)
            {
                Box(p,"Ceiling aluminium rail",new Vector3(0,3.96f,z),new Vector3(18,.12f,.13f),steel,false);
                for(int x=-6;x<=6;x+=6)
                {Box(p,"Fluorescent diffuser",new Vector3(x,3.91f,z),new Vector3(2.6f,.07f,.35f),warm,false);
                LightAt(p,"Station ceiling light",new Vector3(x,3.6f,z),new Color(.89f,.95f,1),2.1f,7);}
            }
            for(int z=-30;z<=-17;z+=1)
            {Box(p,"Tile joint west",new Vector3(-8.78f,1.6f,z),new Vector3(.01f,3.2f,.018f),steel,false);
             Box(p,"Tile joint east",new Vector3(8.78f,1.6f,z),new Vector3(.01f,3.2f,.018f),steel,false);}
            Box(p,"Station name band",new Vector3(-8.76f,2.6f,-24),new Vector3(.035f,.7f,12),yellow,false);
            Text(p,"K223  가천대\nGachon Univ.",new Vector3(-8.7f,2.6f,-24),.2f,Color.black,-90);
            Box(p,"Exit sign",new Vector3(0,3.2f,-15.4f),new Vector3(9,.72f,.15f),navy,false);
            Text(p,"나가는 곳  ↑   가천대학교  ·  비전타워",new Vector3(0,3.2f,-15.5f),.15f,Color.white);
            Text(p,"EXIT  1   /   GACHON UNIVERSITY",new Vector3(0,2.85f,-15.5f),.1f,Color.white);
            Box(p,"Platform connection marker",new Vector3(0,2,-31.75f),new Vector3(6,2.8f,.05f),navy);
            Text(p,"↓  타는 곳\n수인분당선 · 왕십리 / 수원\n승강장 연결 지점",new Vector3(0,2,-31.68f),.18f,Color.white,180);
            Tactile(p,new Vector3(0,.025f,-23),.55f,16);
            for(int x=-6;x<=6;x+=3) Gate(p,x);
            for(int i=0;i<4;i++)Box(p,"Gate lane divider",new Vector3(-4.5f+i*3,.5f,-14.6f),new Vector3(.9f,1,.7f),steel);
            for(int s=-1;s<=1;s+=2)Box(p,"Gate row end barrier",new Vector3(s*8,.5f,-14.6f),new Vector3(1.9f,1,.7f),steel);
            for(int s=-1;s<=1;s+=2)Box(p,"Concourse to passage shoulder",new Vector3(s*8,2.5f,-14),new Vector3(2,3,.4f),ivory);
            for(int i=0;i<3;i++)
            {
                var machine=Group(p,"Ticket vending machine");machine.localPosition=new Vector3(7.9f,0,-28+i*1.7f);machine.localRotation=Quaternion.Euler(0,-90,0);
                Box(machine,"Cabinet",new Vector3(0,.85f,0),new Vector3(1.2f,1.7f,.7f),steel);
                Box(machine,"Blue display surround",new Vector3(0,1.25f,-.37f),new Vector3(.9f,.65f,.03f),navy,false);
                Box(machine,"Touchscreen",new Vector3(0,1.27f,-.39f),new Vector3(.65f,.38f,.02f),glass,false);
                Text(machine,"승차권 발매",new Vector3(0,1.65f,-.4f),.08f,Color.white);
            }
        }
        private static void Tactile(Transform p,Vector3 pos,float width,float length)
        {
            Box(p,"Yellow tactile route",pos,new Vector3(width,.025f,length),yellow,false);
            for(int j=0;j<3;j++)Box(p,"Tactile raised rib",pos+new Vector3((j-1)*width*.25f,.02f,0),new Vector3(.035f,.025f,length),gold,false);
        }
        private static void Gate(Transform p,float x)
        {
            var g=Group(p,"FareGate_"+x);g.localPosition=new Vector3(x,0,-14.6f);
            for(int s=-1;s<=1;s+=2)
            {
                Box(g,"Stainless gate body",new Vector3(s*.85f,.5f,0),new Vector3(.42f,1,1.9f),steel);
                Box(g,"Reader black inset",new Vector3(s*.85f,1.03f,-.57f),new Vector3(.33f,.07f,.5f),dark,false);
                Box(g,"RFID blue pad",new Vector3(s*.85f,1.075f,-.57f),new Vector3(.23f,.02f,.25f),navy,false);
            }
            var left=Group(g,"Left gate pivot");left.localPosition=new Vector3(-.63f,.55f,0);
            Box(left,"Swinging glass leaf",new Vector3(.3f,0,0),new Vector3(.6f,.65f,.06f),glass);
            var right=Group(g,"Right gate pivot");right.localPosition=new Vector3(.63f,.55f,0);
            Box(right,"Swinging glass leaf",new Vector3(-.3f,0,0),new Vector3(.6f,.65f,.06f),glass);
            var reader=Group(g,"CardReader");reader.localPosition=new Vector3(.85f,1.1f,-.6f);
            var script=g.gameObject.AddComponent<GachonHubGate>();script.leftWing=left;script.rightWing=right;script.reader=reader;script.visitor=visitor.transform;
            script.indicator=Text(g,"E",new Vector3(.85f,.75f,-.98f),.18f,Color.cyan);
            Text(g,"카드 · E",new Vector3(0,1.4f,-.95f),.1f,Color.white);
        }
        private static void Connector()
        {
            var p=Group(root,"02_UniversityConnectingPassage");
            Floor(p,"Passage stone",new Vector3(0,0,-7),14,14,"#B5B1A6");
            Box(p,"Connector west wall",new Vector3(-7,2.15f,-7),new Vector3(.35f,4.3f,14),ivory);
            Box(p,"Connector east wall",new Vector3(7,2.15f,-7),new Vector3(.35f,4.3f,14),ivory);
            Box(p,"Connector ceiling",new Vector3(0,4.4f,-7),new Vector3(14,.2f,14),wood);
            for(float z=-13;z<0;z+=.65f)
            {
                Box(p,"Timber ceiling fins",new Vector3(0,4.13f,z),new Vector3(14,.28f,.1f),wood,false);
                if(Mathf.RoundToInt(z*10)%2==0)Box(p,"Linear light",new Vector3(0,3.96f,z),new Vector3(10,.04f,.04f),warm,false);
            }
            for(int z=-12;z<0;z+=3)
            {
                LightAt(p,"Passage downlight",new Vector3(0,3.7f,z),new Color(1,.85f,.64f),2,7);
                for(int i=0;i<5;i++)Box(p,"Wall wood fin",new Vector3(-6.75f,2,z+i*.22f),new Vector3(.18f,4,.08f),wood,false);
                Planter(p,new Vector3(-6.2f,0,z),.55f);
            }
            Tactile(p,new Vector3(0,.03f,-7),.55f,14);
            Text(p,"가천대학교\nGACHON UNIVERSITY",new Vector3(-6.7f,2.4f,-6),.22f,new Color(.05f,.15f,.3f),-90);
            Text(p,"무한한 가능성의 시작",new Vector3(6.76f,2.1f,-6),.23f,new Color(.05f,.15f,.3f),90);
            var infinity=Group(p,"University infinity emblem");infinity.localPosition=new Vector3(6.76f,2.7f,-10);infinity.localRotation=Quaternion.Euler(0,90,0);
            for(int i=0;i<64;i++)
            {
                float a=i*Mathf.PI*2/64,b=(i+1)*Mathf.PI*2/64;
                Vector3 aa=new Vector3(Mathf.Cos(a)*1.8f,Mathf.Sin(a)*Mathf.Cos(a)*1.4f,0);
                Vector3 bb=new Vector3(Mathf.Cos(b)*1.8f,Mathf.Sin(b)*Mathf.Cos(b)*1.4f,0);
                Beam(infinity,"Infinity ribbon",aa,bb,.15f,navy);
            }
        }
        private static void Atrium()
        {
            // Dimensions inferred from people, stair risers and repeated facade bays in the photographs.
            // The column perimeter and pool occupy most of the hall; the ascent starts beside the FRONT row.
            var p=Group(root,"03_FountainAtrium");
            Floor(p,"Atrium ash limestone",new Vector3(.7f,0,10),24.8f,20,"#AAA79E");
            Box(p,"Painted cloud ceiling",new Vector3(.7f,10.4f,10),new Vector3(24.8f,.25f,20),sky);
            var roof=Box(p,"Opaque roof above painted ceiling",new Vector3(.7f,10.65f,10),new Vector3(24.8f,.2f,20),ivory);
            roof.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.ShadowsOnly;
            var west=Group(p,"West Italianate arcade");west.localPosition=new Vector3(-11.7f,0,10);west.localRotation=Quaternion.Euler(0,-90,0);west.localScale=new Vector3(1,.8f,1);Facade(west,20,7);
            var closed=Group(p,"South Italianate arcade");closed.localPosition=new Vector3(.7f,0,0);closed.localRotation=Quaternion.Euler(0,180,0);closed.localScale=new Vector3(1,.8f,1);Facade(closed,24.8f,8);
            // The station opens in the SIDE wall: descending faces -Z and turns left (+X).
            var south=Group(p,"Station triple arch portal");south.localPosition=new Vector3(EntryWallX,0,EntryZ);south.localRotation=Quaternion.Euler(0,90,0);south.localScale=new Vector3(.65f,.8f,1);
            float portalMin=(EntryZ-20)/.65f, portalMax=EntryZ/.65f;
            Box(south,"West portal wing",new Vector3((6+portalMax)*.5f,6.4f,0),new Vector3(portalMax-6,12.8f,.5f),ivory);
            Box(south,"East portal wing",new Vector3((portalMin-6)*.5f,6.4f,0),new Vector3(-6-portalMin,12.8f,.5f),ivory);
            Box(south,"Portal upper wall",new Vector3(0,9,0),new Vector3(12,7.6f,.5f),ivory);
            for(int i=-1;i<=1;i++)
            {
                float x=i*4;Arch(south,new Vector3(x,3.05f,-.22f),1.8f,.28f,trim);
                for(int s=-1;s<=1;s+=2)Pilaster(south,x+s*1.95f,3.05f);
            }
            AddPortalSpandrels(south);
            Box(south,"Portal cornice",new Vector3((EntryZ-10)/.65f,5.4f,-.25f),new Vector3(20/.65f,.22f,.7f),trim,false);
            Arch(south,new Vector3(0,5.8f,-.29f),3.2f,.19f,trim);
            Medallion(south,new Vector3(0,7.4f,-.36f),.75f);
            Text(south,"가천대역 · 지하철",new Vector3(0,4.95f,-.65f),.18f,new Color(.23f,.25f,.25f));
            var rear=Group(p,"North fountain stone wall");rear.localPosition=new Vector3(-4.7f,0,20);
            Box(rear,"Upper cream backdrop",new Vector3(0,7.5f,0),new Vector3(14,5.8f,.5f),ivory);
            Box(rear,"Stone water wall",new Vector3(0,2.4f,-.2f),new Vector3(14,4.8f,.5f),stone);
            var accent=Mat("Water wall light stone","#918877",0,.23f);
            for(int row=0;row<21;row++)for(int col=0;col<20;col++)
            {
                float x=-6.7f+col*.69f+(row%2)*.2f;
                if(x>6.8f)continue;
                Box(rear,"Thin stacked stone",new Vector3(x,.13f+row*.22f,-.51f),new Vector3(.65f,.19f,.08f+(col%3)*.025f),(row+col)%3==0?accent:stone,false);
            }
            Box(rear,"Water wall coping",new Vector3(0,4.9f,-.5f),new Vector3(14,.18f,.7f),trim,false);
            for(int i=0;i<7;i++)
            {
                float x=-6+i*2;
                Box(rear,"Gallery glowing niche",new Vector3(x,6.6f,-.3f),new Vector3(1.8f,2.8f,.06f),warm,false);
                Arch(rear,new Vector3(x,8,-.4f),.9f,.14f,trim);
                Pilaster(rear,x-1,8);
            }
            Rail(rear,new Vector3(-6.7f,5,-.8f),new Vector3(6.7f,5,-.8f),.95f,dark);
            Fountain(p);
            for(int i=0;i<3;i++)Bench(p,new Vector3(-10.85f,0,3+i*6),-90);
            Bench(p,new Vector3(9.2f,0,1),180);
            LampPost(p,new Vector3(6.65f,0,.8f));
            LampPost(p,new Vector3(-10.95f,0,6),90);
            for(int z=3;z<=18;z+=5)
                for(int x=-8;x<=8;x+=8)LightAt(p,"Atrium ceiling fill",new Vector3(x,9.8f,z),new Color(1,.89f,.72f),7,13);
            var circulation=Group(root,"04_EscalatorsAndCampusStair");
            Escalator(circulation,3.6f,false);
            Escalator(circulation,5.8f,true);
            Stair(circulation,StairX,StairWidth,AscentBottomZ,AscentRun,AscentRise);
            // Shared upper landing. No separate extension behind the fountain hall.
            Floor(circulation,"Upper arrival landing",new Vector3(7.7f,5.1f,18.5f),10.8f,3,"#B8B6AF");
            Box(circulation,"Landing foundation",new Vector3(7.7f,2.35f,18.5f),new Vector3(10.8f,4.7f,3),ivory);
            Box(circulation,"Exit lintel",new Vector3(7.7f,10.1f,20),new Vector3(10.8f,.6f,.65f),ivory);
            Text(circulation,"프리덤광장  ↑",new Vector3(7.7f,9.4f,19.65f),.19f,new Color(.09f,.19f,.3f));
            Rail(circulation,new Vector3(2.3f,5.1f,17),new Vector3(2.3f,5.1f,20),1.05f,trim);
        }

        private static void AddPortalSpandrels(Transform south)
        {
            var v=new List<Vector3>();var tr=new List<int>();
            const int count=40;
            for(int i=0;i<=count;i++)
            {
                float x=-2+i*4f/count;
                float bottom=3.05f+Mathf.Sqrt(Mathf.Max(0,1.8f*1.8f-x*x));
                v.Add(new Vector3(x,bottom,-.24f));v.Add(new Vector3(x,5.21f,-.24f));
                v.Add(new Vector3(x,bottom,.24f));v.Add(new Vector3(x,5.21f,.24f));
                if(i<count){int b=i*4;tr.AddRange(new[]{b,b+1,b+4,b+1,b+5,b+4,b+2,b+6,b+3,b+3,b+6,b+7,b,b+4,b+2,b+2,b+4,b+6});}
            }
            Mesh m=SaveMesh("PortalSpandrel",v,tr);
            for(int i=-1;i<=1;i++)MeshObject(south,"Solid masonry above portal arch",m,ivory).transform.localPosition=new Vector3(i*4,0,0);
            foreach(float x in new[]{-22.5f,-19,-15.5f,-12,-8.5f,8.5f,12,15.5f})
            {
                if(x<(EntryZ-20)/.65f+1.5f || x>EntryZ/.65f-1.5f)continue;
                Arch(south,new Vector3(x,2.7f,-.32f),1.4f,.18f,trim);
                Pilaster(south,x-1.65f,2.7f);
                Medallion(south,new Vector3(x,4.8f,-.34f),.34f);
                Window(south,x,7.1f,.85f,1.6f);
                if(Mathf.Abs(x)>9)Window(south,x,10.3f,.75f,1.35f);
                Sconce(south,new Vector3(x+1.2f,7.15f,-.6f));
            }
        }
        private static void Facade(Transform p,float width,int bays)
        {
            Box(p,"Limestone facade wall",new Vector3(0,6.4f,.15f),new Vector3(width,12.8f,.5f),ivory);
            float spacing=width/bays;
            for(int i=0;i<bays;i++)
            {
                float x=-width*.5f+spacing*(i+.5f);float r=spacing*.38f;
                Box(p,"Recessed arch niche",new Vector3(x,1.6f,-.12f),new Vector3(r*2,3.2f,.05f),trim,false);
                Arch(p,new Vector3(x,2.7f,-.32f),r,.18f,trim);
                Pilaster(p,x-spacing*.5f,2.7f);
                Medallion(p,new Vector3(x,4.8f,-.32f),.34f);
                Window(p,x,7.1f,.85f,1.6f);
                Window(p,x,10.3f,.75f,1.35f);
                Sconce(p,new Vector3(x+spacing*.38f,7.15f,-.6f));
                Box(p,"Upper pilaster",new Vector3(x-spacing*.5f,9,-.25f),new Vector3(.22f,6.9f,.18f),trim,false);
            }
            for(int i=0;i<3;i++)
            {
                float y=i==0?5.5f:i==1?8.8f:12.4f;
                Box(p,"Projecting cornice",new Vector3(0,y,-.3f),new Vector3(width,.22f,.65f),trim,false);
                Box(p,"Cornice fine edge",new Vector3(0,y+.15f,-.4f),new Vector3(width,.07f,.7f),trim,false);
                if(i==0)Box(p,"Warm concealed cornice light",new Vector3(0,y-.13f,-.42f),new Vector3(width,.045f,.1f),warm,false);
            }
            for(float x=-width*.5f+.2f;x<width*.5f;x+=.38f)
                Box(p,"Dentil frieze",new Vector3(x,12.07f,-.32f),new Vector3(.17f,.25f,.3f),trim,false);
        }
        private static void Pilaster(Transform p,float x,float height)
        {
            Box(p,"Arcade pier",new Vector3(x,height*.5f,-.25f),new Vector3(.46f,height,.46f),trim);
            Box(p,"Pier base",new Vector3(x,.16f,-.3f),new Vector3(.66f,.32f,.6f),trim,false);
            Box(p,"Pier capital",new Vector3(x,height,-.32f),new Vector3(.76f,.22f,.64f),trim,false);
            Box(p,"Pier inset panel",new Vector3(x,height*.48f,-.49f),new Vector3(.26f,height*.72f,.025f),ivory,false);
        }
        private static void Window(Transform p,float x,float y,float width,float height)
        {
            Box(p,"Window stone surround",new Vector3(x,y,-.22f),new Vector3(width+.22f,height+.2f,.2f),trim,false);
            Box(p,"Dark window frame",new Vector3(x,y,-.34f),new Vector3(width,height,.06f),dark,false);
            Box(p,"Amber window panes",new Vector3(x,y,-.38f),new Vector3(width-.12f,height-.12f,.025f),warm,false);
            Box(p,"Window mullion",new Vector3(x,y,-.41f),new Vector3(.045f,height,.045f),dark,false);
            for(int j=-1;j<=1;j++)Box(p,"Window transom",new Vector3(x,y+j*height*.28f,-.41f),new Vector3(width,.045f,.04f),dark,false);
            Arch(p,new Vector3(x,y+height*.5f,-.29f),width*.5f,.12f,trim);
            Box(p,"Projecting window sill",new Vector3(x,y-height*.5f-.15f,-.36f),new Vector3(width+.4f,.18f,.45f),trim,false);
            Box(p,"Lower sill moulding",new Vector3(x,y-height*.5f-.29f,-.29f),new Vector3(width+.18f,.1f,.32f),trim,false);
        }
        private static void Medallion(Transform p,Vector3 pos,float r)
        {
            var a=Group(p,"Circular wall medallion");a.localPosition=pos;a.localRotation=Quaternion.Euler(-90,0,0);
            Cylinder(a,"Stone roundel",Vector3.zero,r,.1f,trim);
            Ring(a,"Brass rim",new Vector3(0,.07f,0),r*.77f,r*.85f,.035f,gold);
            Cylinder(a,"Roundel centre",new Vector3(0,.1f,0),r*.52f,.08f,ivory);
        }
        private static Transform Column(Transform p,float x,float z)
        {
            var g=Group(p,"Fluted Corinthian column");g.localPosition=new Vector3(x,.65f,z);
            Cylinder(g,"Circular plinth",new Vector3(0,.12f,0),1.04f,.24f,trim,true);
            Cylinder(g,"Lower torus",new Vector3(0,.36f,0),.94f,.18f,trim);
            Cylinder(g,"Upper torus",new Vector3(0,.55f,0),.88f,.16f,trim);
            string name="FlutedColumnShaft";
            if(!meshes.TryGetValue(name,out var mesh))
            {
                var v=new List<Vector3>();var tr=new List<int>();
                const int segments=160;
                for(int row=0;row<2;row++)
                for(int i=0;i<=segments;i++)
                {
                    float a=i*Mathf.PI*2/segments;
                    float r=(row==0?.77f:.65f)-.038f*(1+Mathf.Cos(a*20));
                    v.Add(new Vector3(Mathf.Cos(a)*r,row*10.3f,Mathf.Sin(a)*r));
                    if(row==0&&i<segments){int b=i;tr.AddRange(new[]{b,b+segments+1,b+1,b+1,b+segments+1,b+segments+2});}
                }
                mesh=SaveMesh(name,v,tr);
            }
            var shaft=MeshObject(g,"Twenty-flute tapered stone shaft",mesh,trim);shaft.transform.localPosition=Vector3.up*.63f;
            var col=g.gameObject.AddComponent<CapsuleCollider>();col.radius=.8f;col.height=12;col.center=Vector3.up*6;
            Cylinder(g,"Capital neck",new Vector3(0,11,0),.82f,.2f,trim);
            Cylinder(g,"Acanthus capital",new Vector3(0,11.3f,0),.94f,.45f,ivory);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI*2/12;
                var leaf=Box(g,"Capital carved leaf",new Vector3(Mathf.Cos(a)*.84f,11.35f,Mathf.Sin(a)*.84f),new Vector3(.22f,.55f,.15f),trim,false);
                leaf.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,18);
            }
            for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)
            {
                var volute=Group(g,"Ionic volute");volute.localPosition=new Vector3(sx*.65f,11.6f,sz*.7f);volute.localRotation=Quaternion.Euler(90,0,0);
                Ring(volute,"Spiral capital ring",Vector3.zero,.12f,.3f,.14f,trim);
                Cylinder(volute,"Scroll eye",new Vector3(0,.09f,0),.09f,.09f,ivory);
            }
            Box(g,"Abacus",new Vector3(0,11.9f,0),new Vector3(2.1f,.25f,2.1f),trim,false);
            return g;
        }
        private static void Fountain(Transform p)
        {
            var f=Group(p,"Fountain • low stepped pool and column perimeter");f.localPosition=new Vector3(-3.1f,0,11.1f);
            var ellipse=new Vector3(1,1,1.4f);
            // Low basin: the source photos show a small central stone/nozzle, not a multi-tier bowl.
            Ring(f,"Pool outer broad step",new Vector3(0,.02f,0),3.95f,4.7f,.16f,ivory,ellipse,true);
            Ring(f,"Pool second broad step",new Vector3(0,.18f,0),3.95f,4.55f,.17f,trim,ellipse,true);
            Ring(f,"Pool rolled coping",new Vector3(0,.35f,0),3.9f,4.35f,.3f,trim,ellipse,true);
            Ring(f,"Pool fine moulding",new Vector3(0,.63f,0),3.88f,4.4f,.07f,trim,ellipse);
            var surface=Cylinder(f,"Dark reflecting water",new Vector3(0,.43f,0),3.93f,.025f,water);
            surface.transform.localScale=new Vector3(7.86f,.0125f,11);
            var motion=f.gameObject.AddComponent<GachonHubWater>();motion.waterSurfaces=new[]{surface.GetComponent<Renderer>()};
            Transform bannerColumn=null;
            foreach(float x in new[]{-7.1f,-3.1f,.9f})foreach(float z in new[]{5.3f,11.1f,16.9f})
            {
                if(Mathf.Abs(x+3.1f)<.01f && Mathf.Abs(z-11.1f)<.01f)continue;
                var pad=Group(p,"Scalloped column pool edge");pad.localPosition=new Vector3(x,0,z);
                Cylinder(pad,"Column island",new Vector3(0,.38f,0),1.05f,.76f,ivory,true);
                Ring(pad,"Rounded column coping",new Vector3(0,.72f,0),.8f,1.12f,.1f,trim);
                var c=Column(p,x,z);c.localScale=new Vector3(.67f,.77f,.67f);
                if(Mathf.Abs(x-.9f)<.01f && Mathf.Abs(z-5.3f)<.01f)bannerColumn=c;
            }
            Cylinder(f,"Low circular nozzle island",new Vector3(0,.51f,0),1.25f,.15f,trim);
            var rock=GameObject.CreatePrimitive(PrimitiveType.Sphere);rock.name="Low carved central stone";
            rock.transform.SetParent(f,false);rock.transform.localPosition=new Vector3(0,.78f,0);rock.transform.localScale=new Vector3(1.1f,.5f,.85f);
            rock.GetComponent<Renderer>().sharedMaterial=stone;UnityEngine.Object.DestroyImmediate(rock.GetComponent<Collider>());
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*2/8;var jet=Group(f,"Fountain water jet");jet.localPosition=new Vector3(Mathf.Cos(a)*2.3f,.48f,Mathf.Sin(a)*3.1f);
                var ps=jet.gameObject.AddComponent<ParticleSystem>();var main=ps.main;main.loop=true;main.startLifetime=.7f;main.startSpeed=2.6f;
                main.startSize=.025f;main.startColor=new Color(.74f,.91f,.94f,.7f);main.gravityModifier=.8f;main.maxParticles=50;main.simulationSpace=ParticleSystemSimulationSpace.World;
                var emission=ps.emission;emission.rateOverTime=30;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=2;shape.radius=.02f;shape.rotation=new Vector3(-90,0,0);
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=Mat("Fountain droplets","#BDDDE5",.1f,.7f,new Color(.15f,.23f,.25f));
            }
            AttachColumnBanner(bannerColumn);
        }

        private static void AttachColumnBanner(Transform column)
        {
            if(!column)throw new InvalidOperationException("Banner column not found.");
            var mount=Group(column,"Column-mounted GACHON banner");
            float bottom=(1.3f-column.localPosition.y)/column.localScale.y;
            float top=(8.9f-column.localPosition.y)/column.localScale.y;
            var v=new List<Vector3>();var tr=new List<int>();var uv=new List<Vector2>();
            const int across=40, rows=24;
            for(int row=0;row<=rows;row++)
            {
                float y=Mathf.Lerp(bottom,top,row/(float)rows);
                float taper=Mathf.Clamp01((y-.63f)/10.3f);
                for(int j=0;j<=across;j++)
                {
                    float a=-Mathf.PI*.5f+Mathf.Lerp(-.52f,.52f,j/(float)across);
                    float radius=Mathf.Lerp(.77f,.65f,taper)-.038f*(1+Mathf.Cos(a*20))+.014f;
                    v.Add(new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius));
                    uv.Add(new Vector2(j/(float)across,row/(float)rows));
                    if(row<rows && j<across)
                    {
                        int b=row*(across+1)+j,n=across+1;
                        tr.AddRange(new[]{b,b+n,b+1,b+1,b+n,b+n+1});
                    }
                }
            }
            var material=Mat("Gachon crimson banner","#871D3D",0,.3f);
            MeshObject(mount,"Column university banner",SaveMesh("GachonColumnAttachedBanner",v,tr,uv),material);
            const string letters="GACHON";
            for(int i=0;i<letters.Length;i++)
            {
                float worldY=6.475f-i*.55f;
                float y=(worldY-column.localPosition.y)/column.localScale.y;
                float taper=Mathf.Clamp01((y-.63f)/10.3f);
                // Keep each small glyph in front of the flute ridges across its full width.
                float radius=Mathf.Lerp(.77f,.65f,taper)+.02f;
                var letter=Text(mount,letters[i].ToString(),new Vector3(0,y,-radius),.2f,Color.white);
                letter.transform.localScale=new Vector3(1/column.localScale.x,1/column.localScale.y,1/column.localScale.z);
            }
        }

        private static void Stair(Transform p,float x,float width,float start,float run,float rise)
        {
            var g=Group(p,"Walkable stone staircase");g.localPosition=new Vector3(x,0,start);
            int count=Mathf.CeilToInt(rise/.17f);
            float landing=Mathf.Abs(rise-5.1f)<.01f?1.2f:0;
            float depth=(run-landing)/count,height=rise/count;
            for(int i=0;i<count;i++)
            {
                float y=(i+1)*height;
                float z=i*depth+(i>=count/2?landing:0);
                Box(g,"Stair tread "+(i+1),new Vector3(0,y*.5f,z+depth*.5f),new Vector3(width,y,depth+.01f),ivory);
                Box(g,"Stone stair nosing",new Vector3(0,y+.012f,z+.035f),new Vector3(width,.025f,.07f),trim,false);
                Box(g,"Anti-slip strip",new Vector3(0,y+.026f,z+.1f),new Vector3(width-.2f,.012f,.035f),dark,false);
            }
            if(landing>0)Box(g,"Mid-flight rest landing",new Vector3(0,height*(count/2)*.5f,depth*(count/2)+landing*.5f),new Vector3(width,height*(count/2),landing+.01f),ivory);
            for(int side=-1;side<=1;side+=2)
            {
                float xx=side*width*.5f;
                var points=landing>0?new[]{new Vector3(xx,0,0),new Vector3(xx,height*(count/2),depth*(count/2)),new Vector3(xx,height*(count/2),depth*(count/2)+landing),new Vector3(xx,rise,run)}:new[]{new Vector3(xx,0,0),new Vector3(xx,rise,run)};
                for(int j=0;j<points.Length-1;j++)
                {
                    Beam(g,"Inclined marble stair edging",points[j]+Vector3.up*.3f,points[j+1]+Vector3.up*.3f,.2f,trim,true);
                    Rail(g,points[j],points[j+1],1.05f,trim);
                    Beam(g,"Stainless grasp rail",points[j]+new Vector3(-side*.15f,.9f,0),points[j+1]+new Vector3(-side*.15f,.9f,0),.055f,steel);
                }
            }

        }
        private static void Escalator(Transform p,float x,bool upwards)
        {
            var clear=Mat("Escalator clear balustrade","#86B7C1",.1f,.85f);
            clear.SetColor("_BaseColor",new Color(.52f,.72f,.76f,.24f));
            clear.SetFloat("_Surface",1);clear.SetFloat("_ZWrite",0);
            clear.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);clear.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            clear.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");clear.SetOverrideTag("RenderType","Transparent");clear.renderQueue=3000;
            clear.SetShaderPassEnabled("ShadowCaster",false);
            var g=Group(p,upwards?"Escalator UP to campus":"Escalator DOWN to station");g.localPosition=new Vector3(x,0,AscentBottomZ);
            const float run=AscentRun,rise=AscentRise;
            var v=new List<Vector3>{new Vector3(-.76f,-.16f,0),new Vector3(.76f,-.16f,0),new Vector3(-.76f,rise-.16f,run),new Vector3(.76f,rise-.16f,run)};
            var ramp=MeshObject(g,"Continuous walkable incline",SaveMesh("EscalatorCollisionRamp",v,new List<int>{0,2,1,1,2,3}),dark,true);
            var e=g.gameObject.AddComponent<GachonHubEscalator>();e.visitor=visitor;e.run=run;e.rise=rise;e.width=1.4f;e.upwards=upwards;
            var treads=new List<Transform>();
            const int stepCount=32;
            for(int i=0;i<stepCount;i++)
            {
                float z=i*run/stepCount;
                var tread=Box(g,"Moving ribbed step",new Vector3(0,z*rise/run-.08f,z),new Vector3(1.4f,.16f,.4f),dark,false);
                Box(tread.transform,"Yellow step edge",new Vector3(0,.55f,-.44f),new Vector3(.97f,.08f,.09f),yellow,false);
                for(int j=-5;j<=5;j++)Box(tread.transform,"Tread groove",new Vector3(j*.078f,.51f,0),new Vector3(.012f,.035f,.9f),steel,false);
                treads.Add(tread.transform);
            }
            e.treads=treads.ToArray();
            for(int side=-1;side<=1;side+=2)
            {
                float xx=side*.91f;
                Beam(g,"Stainless escalator skirt",new Vector3(xx,.1f,0),new Vector3(xx,rise+.1f,run),.25f,steel,true);
                Beam(g,"Blue balustrade",new Vector3(xx,.62f,0),new Vector3(xx,rise+.62f,run),.15f,clear);
                var panel=Box(g,"Escalator glass side panel",new Vector3(xx,rise*.5f+.55f,run*.5f),new Vector3(.07f,.84f,Mathf.Sqrt(run*run+rise*rise)),clear,false);
                panel.transform.localRotation=Quaternion.LookRotation(new Vector3(0,rise,run));
                Beam(g,"Rubber moving handrail",new Vector3(xx,1.05f,0),new Vector3(xx,rise+1.05f,run),.12f,dark,true);
                Box(g,"Lower rounded newel",new Vector3(xx,.53f,-.5f),new Vector3(.28f,1.04f,1),steel);
                Box(g,"Upper rounded newel",new Vector3(xx,rise+.53f,run+.5f),new Vector3(.28f,1.04f,1),steel);
                Cylinder(g,"Handrail end",new Vector3(xx,.96f,-.5f),.13f,.18f,dark);
            }
            Box(g,"Bottom comb landing",new Vector3(0,.015f,-.6f),new Vector3(1.5f,.03f,1.2f),steel,false);
            Box(g,"Top comb landing",new Vector3(0,rise+.015f,run+.6f),new Vector3(1.5f,.03f,1.2f),steel,false);
            Text(g,upwards?"↑":"↓",new Vector3(.91f,.7f,-1.02f),.23f,upwards?Color.green:Color.cyan);
        }
        private static void Rail(Transform p,Vector3 a,Vector3 b,float height,Material mat)
        {
            Beam(p,"Handrail",a+Vector3.up*height,b+Vector3.up*height,.09f,mat);
            Beam(p,"Bottom rail",a+Vector3.up*.18f,b+Vector3.up*.18f,.07f,mat);
            int count=Mathf.CeilToInt(Vector3.Distance(a,b)/.5f);
            for(int i=0;i<=count;i++)
            {
                var pos=Vector3.Lerp(a,b,i/(float)count);
                Beam(p,"Baluster",pos,pos+Vector3.up*height,.07f,mat);
            }
            var barrier=Box(p,"Safety rail collision",(a+b)*.5f+Vector3.up*height*.5f,new Vector3(.15f,height,Vector3.Distance(a,b)),mat);
            barrier.transform.localRotation=Quaternion.LookRotation(b-a);barrier.GetComponent<Renderer>().enabled=false;
        }
        private static void Bench(Transform p,Vector3 pos,float yaw)
        {
            var g=Group(p,"Walnut and cast iron bench");g.localPosition=pos;g.localRotation=Quaternion.Euler(0,yaw,0);
            for(int i=0;i<5;i++)
            {
                Box(g,"Seat slat",new Vector3(0,.47f,-.25f+i*.12f),new Vector3(2.7f,.075f,.09f),wood);
                Box(g,"Back slat",new Vector3(0,.69f+i*.11f,.32f),new Vector3(2.7f,.085f,.065f),wood);
            }
            for(int s=-1;s<=1;s+=2)
            {
                Beam(g,"Iron leg",new Vector3(s*1.05f,.05f,-.22f),new Vector3(s*1.05f,.55f,-.22f),.08f,dark);
                Beam(g,"Iron rear leg",new Vector3(s*1.05f,.05f,.27f),new Vector3(s*1.05f,1.16f,.32f),.075f,dark);
                Beam(g,"Arm rest",new Vector3(s*1.28f,.72f,-.3f),new Vector3(s*1.28f,.72f,.3f),.07f,dark);
            }
        }
        private static void Sconce(Transform p,Vector3 pos)
        {
            var g=Group(p,"Warm wall lantern");g.localPosition=pos;
            Box(g,"Wall bracket",new Vector3(0,-.3f,.2f),new Vector3(.12f,.45f,.15f),dark,false);
            Box(g,"Lantern light",Vector3.zero,new Vector3(.19f,.32f,.19f),warm,false);
            for(int s=-1;s<=1;s+=2)Box(g,"Lantern frame",new Vector3(s*.12f,0,0),new Vector3(.035f,.42f,.25f),dark,false);
            Box(g,"Lantern cap",new Vector3(0,.22f,0),new Vector3(.32f,.07f,.32f),dark,false);
            LightAt(g,"Lantern glow",new Vector3(0,0,-.25f),new Color(1,.66f,.31f),.65f,3.5f);
        }
        private static void LampPost(Transform p,Vector3 pos,float yaw=0)
        {
            var g=Group(p,"Twin plaza lantern");g.localPosition=pos;g.localRotation=Quaternion.Euler(0,yaw,0);
            Cylinder(g,"Flared base",Vector3.up*.15f,.2f,.3f,dark,true);
            Cylinder(g,"Lamp standard",Vector3.up*1.6f,.065f,3.2f,dark);
            Beam(g,"Crossarm",new Vector3(-.5f,3.05f,0),new Vector3(.5f,3.05f,0),.07f,dark);
            for(int s=-1;s<=1;s+=2)
            {
                Box(g,"Glowing lantern",new Vector3(s*.5f,3.32f,0),new Vector3(.28f,.48f,.28f),warm,false);
                Box(g,"Lantern roof",new Vector3(s*.5f,3.61f,0),new Vector3(.42f,.1f,.42f),dark,false);
                for(int j=-1;j<=1;j+=2)Box(g,"Lantern uprights",new Vector3(s*.5f+j*.15f,3.32f,0),new Vector3(.025f,.52f,.32f),dark,false);
            }
            LightAt(g,"Lantern illumination",new Vector3(0,3.3f,0),new Color(1,.82f,.54f),1.5f,8);
        }
        private static void Planter(Transform p,Vector3 pos,float r)
        {
            var g=Group(p,"Planted container");g.localPosition=pos;
            Cylinder(g,"Stone planter",Vector3.up*.3f,r,.6f,ivory,true);
            Cylinder(g,"Soil",Vector3.up*.61f,r*.9f,.03f,wood);
            for(int i=0;i<7;i++)
            {
                float a=i*Mathf.PI*2/7;
                var leaf=GameObject.CreatePrimitive(PrimitiveType.Sphere);leaf.name="Shrub foliage";leaf.transform.SetParent(g,false);
                leaf.transform.localPosition=new Vector3(Mathf.Cos(a)*r*.5f,.85f,Mathf.Sin(a)*r*.5f);
                leaf.transform.localScale=new Vector3(r*.65f,.8f,r*.65f);leaf.GetComponent<Renderer>().sharedMaterial=green;
                UnityEngine.Object.DestroyImmediate(leaf.GetComponent<Collider>());
            }
        }
        private static void Campus()
        {
            // Freedom is the paved SUNKEN court of Vision Tower, distinct from the upper Stardom square.
            var p=Group(root,"05_FreedomPlaza_SunkenCourtyard");
            const float y=5.1f;
            var concrete=Mat("Freedom grey granite cladding","#ACAAA2",0,.25f);
            var pale=Mat("Freedom white columns","#E5E5DF",0,.35f);
            var blue=Mat("Freedom teal curtain wall","#3D7D8D",.65f,.82f);
            var stripe=Mat("Freedom paving dark bands","#646A69",0,.3f);
            var salmon=Mat("Freedom paving warm squares","#B89D88",0,.3f);
            Floor(p,"Freedom square granite",new Vector3(3,y,42),34,44,"#B8BBB5");
            for(int x=-10;x<=16;x+=8)Box(p,"Long charcoal paving band",new Vector3(x,y+.018f,42),new Vector3(.75f,.025f,44),stripe,false);
            for(int z=23;z<64;z+=6)for(int x=-12;x<20;x+=4)
                Box(p,"Warm square in paving",new Vector3(x,y+.021f,z),new Vector3(.65f,.02f,.65f),salmon,false);
            // The glass hall with tall white pilotis and separate slatted canopies.
            var hall=Group(p,"Vision Tower curtain wall and pilotis");hall.localPosition=new Vector3(-14,y,42);
            Box(hall,"Blue glass hall",new Vector3(-2.5f,8,0),new Vector3(5,16,44),blue);
            for(float yy=1.3f;yy<16;yy+=1.55f)
                Box(hall,"Horizontal curtain wall joint",new Vector3(.04f,yy,0),new Vector3(.1f,.07f,44),steel,false);
            for(float z=-22;z<=22;z+=2)
                Box(hall,"Vertical curtain wall joint",new Vector3(.06f,8,z),new Vector3(.08f,16,.07f),steel,false);
            Box(hall,"Overhanging limestone upper building",new Vector3(-.5f,20.5f,0),new Vector3(10,9,44),concrete);
            Box(hall,"Warm timber soffit",new Vector3(-.4f,15.97f,0),new Vector3(10,.12f,44),wood,false);
            for(int i=0;i<7;i++)
            {
                float z=-19+i*6.3f;
                Cylinder(hall,"Tall white circular piloti",new Vector3(3.1f,8,z),.48f,16,pale,true);
                for(int side=-1;side<=1;side+=2)
                {
                    Box(hall,"Canopy side frame",new Vector3(3.5f,11.5f,z+side*2.65f),new Vector3(6,.22f,.13f),steel,false);
                    Box(hall,"Canopy end frame",new Vector3(3.5f+side*3,11.5f,z),new Vector3(.13f,.22f,5.3f),steel,false);
                }
                for(int j=0;j<12;j++)Box(hall,"Sunshade louvre",new Vector3(3.5f,11.65f,z-2.45f+j*.44f),new Vector3(5.8f,.12f,.13f),pale,false);
                Bench(p,new Vector3(-12.2f,y,23+i*6),90);
            }
            for(float z=-20;z<=20;z+=4)
                for(float yy=18;yy<=23;yy+=3)
                    Box(hall,"Upper inset rectangular window",new Vector3(4.53f,yy,z),new Vector3(.04f,.9f,1.8f),blue,false);
            // Opposite side: open multi-level circulation galleries on cylindrical columns.
            var galleries=Group(p,"East layered open galleries");galleries.localPosition=new Vector3(20,y,42);
            Box(galleries,"Rear shop and gallery wall",new Vector3(4.1f,7,0),new Vector3(.4f,14,44),concrete);
            for(int floor=0;floor<3;floor++)
            {
                float yy=floor*3.6f;
                Box(galleries,"Gallery deck",new Vector3(1.4f,yy+3.35f,0),new Vector3(6,.45f,44),concrete);
                for(int i=0;i<8;i++)
                {
                    float z=-20+i*5.7f;
                    Cylinder(galleries,"Gallery circular support",new Vector3(-.7f,yy+1.6f,z),.32f,3.2f,pale,true);
                    Box(galleries,"Shopfront dark frame",new Vector3(3.86f,yy+1.45f,z),new Vector3(.12f,2.65f,4.6f),dark);
                    Box(galleries,"Shopfront glass",new Vector3(3.77f,yy+1.45f,z),new Vector3(.04f,2.35f,4.3f),blue,false);
                    if(floor==0)Bench(p,new Vector3(21.3f,y,22+i*5.7f),-90);
                }
                Rail(galleries,new Vector3(-1.55f,yy+3.6f,-22),new Vector3(-1.55f,yy+3.6f,22),1.1f,pale);
            }
            Text(p,"프리덤광장",new Vector3(23.68f,y+2.9f,28),.18f,Color.white,90);
            Text(p,"FREEDOM PLAZA",new Vector3(23.67f,y+2.5f,28),.1f,Color.white,90);
            var galleryStairs=Group(p,"Exposed zigzag gallery stairs");galleryStairs.localPosition=new Vector3(0,y,0);
            Stair(galleryStairs,16.9f,2.7f,26,16,3.6f);
            Floor(galleryStairs,"Gallery intermediate landing",new Vector3(18,3.6f,43),5,2,"#ACAAA2");
            var returnFlight=Group(galleryStairs,"Return flight to second gallery");returnFlight.localPosition=new Vector3(16.9f,3.6f,42);returnFlight.localRotation=Quaternion.Euler(0,180,0);
            Stair(returnFlight,0,2.7f,0,16,3.6f);
            Floor(galleryStairs,"Gallery upper landing",new Vector3(18,7.2f,25),5,2,"#ACAAA2");
            // Near-side building wraps the court; the fountain exit remains a real walkable opening.
            Box(p,"South west retaining facade",new Vector3(-6,y+5.2f,19.8f),new Vector3(16,10.4f,.5f),concrete);
            Box(p,"South east retaining facade",new Vector3(16,y+5.2f,19.8f),new Vector3(8,10.4f,.5f),concrete);
            Box(p,"Building above fountain exit",new Vector3(3,y+13.4f,19.8f),new Vector3(34,6,.8f),concrete);
            Box(p,"Open upper corridor bridge",new Vector3(3,y+8,20.8f),new Vector3(34,.6f,3.2f),concrete);
            Rail(p,new Vector3(-14,y+8.3f,22.3f),new Vector3(20,y+8.3f,22.3f),1.1f,pale);
            Text(p,"↓ 가천대역",new Vector3(7,y+4.5f,20.25f),.2f,Color.white,180);
            for(float x=-12;x<19;x+=5)
            {
                Cylinder(p,"South gallery piloti",new Vector3(x,y+4,21.8f),.3f,8,pale,true);
                for(float yy=17;yy<=19;yy+=2)
                    Box(p,"South upper window",new Vector3(x,yy,20.26f),new Vector3(2,.85f,.04f),blue,false);
            }
            // Broad, shallow steps lead onward to Stardom, beyond Freedom's level.
            var upper=Group(p,"Stardom connection beyond Freedom");
            upper.localPosition=new Vector3(0,y,0);
            Stair(upper,3,26,64,11.2f,3.2f);
            Floor(p,"Upper Stardom approach",new Vector3(3,y+3.2f,78),30,6,"#B8B6AF");
            var far=Group(p,"North campus frontage");far.localPosition=new Vector3(3,y+3.2f,83);
            Building(far,34,12,5,false);
            Box(far,"Recessed central entry",new Vector3(0,2.1f,-2.56f),new Vector3(6,4.2f,.08f),dark);
            for(int i=-2;i<=2;i++)
                Box(far,"Entry door",new Vector3(i*1.1f,1.65f,-2.62f),new Vector3(1.05f,3.3f,.04f),blue,false);
            Text(far,"GACHON UNIVERSITY",new Vector3(0,5,-2.65f),.19f,Color.white);
            Box(p,"Road bridge across far upper level",new Vector3(3,y+9.3f,68),new Vector3(40,1,7),concrete);
            Rail(p,new Vector3(-17,y+9.8f,64.5f),new Vector3(23,y+9.8f,64.5f),1.2f,steel);
            for(int s=-1;s<=1;s+=2)Cylinder(p,"Bridge pier",new Vector3(3+s*14,y+4.5f,68),.65f,9,pale,true);
            // Wire-like suspended figures observed in official campus VR and the 2023 renovation article.
            SuspendedFigure(p,new Vector3(-3,y+14,39),steel);
            SuspendedFigure(p,new Vector3(8,y+15.7f,53),steel);
            SuspendedFigure(p,new Vector3(-10.5f,y+12.8f,29),steel);
            ReproportionFreedom(p);
        }
        private static void ReproportionFreedom(Transform p)
        {
            // Calibrate the architectural envelope around its arrival edge, rather than widening
            // the open court. Keep people, seats and cylindrical supports at their original sizes.
            float sx=FreedomWidth/34f, sz=FreedomLength/44f;
            p.localScale=new Vector3(sx,1,sz);
            p.localPosition=new Vector3(3*(1-sx),0,20*(1-sz));
            foreach(var t in p.GetComponentsInChildren<Transform>())
            {
                if(t==p)continue;
                bool fixture=t.name=="Walnut and cast iron bench" || t.name.StartsWith("Suspended wire figure") || t.name=="Warm square in paving";
                if(fixture)
                {
                    bool sideways=Mathf.Abs(t.TransformDirection(Vector3.right).z)>.9f;
                    t.localScale=Vector3.Scale(t.localScale,sideways?new Vector3(1/sz,1,1/sx):new Vector3(1/sx,1,1/sz));
                }
                if(t.name=="Tall white circular piloti" || t.name=="Gallery circular support" || t.name=="South gallery piloti" || t.name=="Bridge pier")
                    t.localScale=Vector3.Scale(t.localScale,new Vector3(1/sx,1,1/sz));
                if(t.name.EndsWith("jointed finish"))
                {
                    var material=t.GetComponent<Renderer>().sharedMaterial;
                    material.SetTextureScale("_BaseMap",Vector2.Scale(material.GetTextureScale("_BaseMap"),new Vector2(sx,sz)));
                    EditorUtility.SetDirty(material);
                }
            }
            // Preserve the full indoor stair landing opening despite the narrower outdoor court.
            var shoulder=p.Find("South east retaining facade");
            float right=3+17*sx, left=EntryWallX;
            var pos=shoulder.position;pos.x=(left+right)*.5f;shoulder.position=pos;
            var scale=shoulder.localScale;scale.x=(right-left)/sx;shoulder.localScale=scale;
        }
        private static void SuspendedFigure(Transform p,Vector3 pos,Material mat)
        {
            var g=Group(p,"Suspended wire figure • simplified");g.localPosition=pos;
            var head=GameObject.CreatePrimitive(PrimitiveType.Sphere);head.name="Sculpture head";head.transform.SetParent(g,false);
            head.transform.localPosition=Vector3.up*.8f;head.transform.localScale=Vector3.one*.22f;head.GetComponent<Renderer>().sharedMaterial=mat;
            UnityEngine.Object.DestroyImmediate(head.GetComponent<Collider>());
            for(int i=0;i<5;i++)Beam(g,"Open wire torso",new Vector3((i-2)*.065f,.66f,0),new Vector3((i-2)*.045f,0,.03f),.018f,mat);
            Beam(g,"Wire left arm",new Vector3(-.13f,.55f,0),new Vector3(-.75f,.25f,.1f),.035f,mat);
            Beam(g,"Wire right arm",new Vector3(.13f,.55f,0),new Vector3(.7f,.85f,0),.035f,mat);
            Beam(g,"Wire left leg",Vector3.zero,new Vector3(-.3f,-.65f,.2f),.04f,mat);
            Beam(g,"Wire right leg",Vector3.zero,new Vector3(.3f,-.55f,-.2f),.04f,mat);
            Beam(g,"Fine suspension cable",new Vector3(-12,4,0),new Vector3(12,4,0),.009f,mat);
            Beam(g,"Fine figure hanger",new Vector3(0,.9f,0),new Vector3(0,4,0),.008f,mat);
        }

        private static void Building(Transform p,float width,float height,float depth,bool tower)
        {
            Box(p,"Building solid volume",new Vector3(0,height*.5f,0),new Vector3(width,height,depth),glass);
            for(float y=1;y<height;y+=3.2f)
            {
                Box(p,"Front horizontal curtain-wall mullion",new Vector3(0,y,-depth*.5f-.035f),new Vector3(width,.12f,.12f),steel,false);
                Box(p,"Side horizontal curtain-wall mullion",new Vector3(width*.5f+.035f,y,0),new Vector3(.12f,.12f,depth),steel,false);
                if(!tower)Box(p,"Stone spandrel",new Vector3(0,y-.4f,-depth*.5f-.06f),new Vector3(width,.65f,.15f),ivory,false);
            }
            for(float x=-width*.5f;x<=width*.5f;x+=1.7f)
                Box(p,"Front vertical mullion",new Vector3(x,height*.5f,-depth*.5f-.06f),new Vector3(.085f,height,.12f),steel,false);
            for(float z=-depth*.5f;z<=depth*.5f;z+=1.7f)
                Box(p,"Side vertical mullion",new Vector3(width*.5f+.06f,height*.5f,z),new Vector3(.12f,height,.085f),steel,false);
            Box(p,"Roof parapet",new Vector3(0,height,0),new Vector3(width+.25f,.4f,depth+.25f),trim,false);
            if(tower)
            {
                for(int i=0;i<5;i++)
                {
                    float y=2+i*4.5f;
                    Beam(p,"Expressed diagonal tower brace",new Vector3(-width*.45f,y,-depth*.5f-.15f),new Vector3(width*.45f,y+4.5f,-depth*.5f-.15f),.16f,steel);
                }
            }
        }
        private static void Tree(Transform p,Vector3 pos,int seed)
        {
            var g=Group(p,"Campus tree");g.localPosition=pos;
            Cylinder(g,"Tree planter curb",Vector3.up*.2f,1.45f,.4f,ivory,true);
            Cylinder(g,"Tree soil",Vector3.up*.41f,1.3f,.04f,wood);
            Cylinder(g,"Tree trunk",Vector3.up*2,.18f,4,wood,true);
            for(int i=0;i<9;i++)
            {
                float a=i*2.4f+seed;
                var branch=new Vector3(Mathf.Cos(a)*1.3f,3.4f+(i%3)*.7f,Mathf.Sin(a)*1.3f);
                Beam(g,"Tree branch",Vector3.up*2.4f,branch,.09f,wood);
                var leaf=GameObject.CreatePrimitive(PrimitiveType.Sphere);leaf.name="Tree canopy";leaf.transform.SetParent(g,false);leaf.transform.localPosition=branch+Vector3.up*.4f;
                leaf.transform.localScale=new Vector3(2.3f,1.8f,2.1f);leaf.GetComponent<Renderer>().sharedMaterial=green;
                UnityEngine.Object.DestroyImmediate(leaf.GetComponent<Collider>());
            }
        }
    }
}
#endif





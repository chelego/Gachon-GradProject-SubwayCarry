#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MxM;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    public static class AnimationLabBuilder
    {
        public const string Root="Assets/_Project/Scenes/3D/Test/Animation";
        const string Game="Assets/_Project/Scenes/3D/Ingame/Game";
        const string Original="Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/AnimationLibrary_Unity_Standard.fbx";
        public static readonly string[] Fields={"Normal","Situations"};
        static Material floor,edge,seat,wall,mark;
        public static void Batch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))throw new InvalidOperationException("Use the independent validation project only");
            try{BuildMissing();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
        }
        [MenuItem("SubwayCarry/Animation/Create missing fields")]
        public static void BuildMissing()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("No Play or unsaved scene edits are permitted");
            var previous=EditorSceneManager.GetSceneManagerSetup();
            try {
                Directory.CreateDirectory(Root+"/Data");Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory(Root+"/Materials");AssetDatabase.Refresh();
                floor=Mat("Floor",new Color(.22f,.255f,.29f));edge=Mat("Structure",new Color(.38f,.43f,.47f));seat=Mat("Seat",new Color(.19f,.39f,.44f));wall=Mat("Wall",new Color(.63f,.65f,.64f));mark=Mat("Markers",new Color(.87f,.66f,.23f));
                var library=Library();var prefab=Character(library);
                foreach(string field in Fields)if(!File.Exists(Root+"/"+field+".unity"))BuildScene(field,prefab);
                if(Application.isBatchMode&&Application.dataPath.Replace('\\','/').Contains("/Logs/CrowdBehavior/Walking/Unity/Assets"))foreach(string field in Fields){var scene=EditorSceneManager.OpenScene(Root+"/"+field+".unity");bool changed=false;foreach(var text in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))if(text.font!=LabelFont()){text.font=LabelFont();text.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;EditorUtility.SetDirty(text);changed=true;}if(changed)EditorSceneManager.SaveScene(scene);}
                AssetDatabase.SaveAssets();Debug.Log("ANIMATION_FIELDS_BUILT "+string.Join(",",Fields));
            }finally{if(!Application.isBatchMode&&previous.All(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(previous);}
        }
        static Material Mat(string name,Color color)
        {
            string path=Root+"/Materials/"+name+".mat";var value=AssetDatabase.LoadAssetAtPath<Material>(path);if(value!=null)return value;
            value=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};value.SetColor("_BaseColor",color);value.SetFloat("_Smoothness",.25f);AssetDatabase.CreateAsset(value,path);return value;
        }
        static AnimationLabLibrary Library()
        {
            string path=Root+"/Data/Library.asset";var lib=AssetDatabase.LoadAssetAtPath<AnimationLabLibrary>(path);
            var clips=AssetDatabase.LoadAllAssetsAtPath(Original).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            AnimationClip Clip(string name)=>clips.First(c=>c.name.EndsWith("|"+name));
            if(lib!=null){if(lib.font==null){lib.font=LabelFont();EditorUtility.SetDirty(lib);}if(lib.idle!=null&&lib.idle.name.StartsWith("__preview__")){lib.idle=Clip("Idle_Loop");var existingController=(AnimatorController)lib.controller;var existingTree=(BlendTree)existingController.layers[0].stateMachine.states.First(s=>s.state.name=="Locomotion").state.motion;var children=existingTree.children;children[0].motion=lib.idle;existingTree.children=children;EditorUtility.SetDirty(existingTree);EditorUtility.SetDirty(lib);}return lib;}
            lib=ScriptableObject.CreateInstance<AnimationLabLibrary>();lib.font=LabelFont();lib.idle=Clip("Idle_Loop");lib.walk=Clip("Walk_Loop");lib.run=Clip("Jog_Fwd_Loop");lib.sitEnter=Clip("Sitting_Enter");lib.sitIdle=Clip("Sitting_Idle_Loop");lib.sitExit=Clip("Sitting_Exit");
            lib.matchingData=AssetDatabase.LoadAssetAtPath<MxMAnimData>(Game+"/MotionMatching/Resources/PassengerMotionTrial.asset");
            lib.converted=lib.matchingData.Clips.Where(c=>c.name.StartsWith("CMU_")).ToArray();
            var sample=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Game+"/Prefabs/Passenger3D.prefab"));
            try{lib.walkSpeed=Supports(lib.walk,sample,out lib.leftSupport,out lib.rightSupport);lib.runSpeed=Supports(lib.run,sample,out lib.runLeftSupport,out lib.runRightSupport);}finally{UnityEngine.Object.DestroyImmediate(sample);}
            if(lib.walkSpeed<.4f||lib.walkSpeed>1.8f||lib.runSpeed<1.4f||lib.runSpeed>7)throw new InvalidDataException("Unexpected CC0 authored travel speed: "+lib.walkSpeed+","+lib.runSpeed);
            var controller=AnimatorController.CreateAnimatorControllerAtPath(Root+"/Data/Lab.controller");controller.AddParameter("Speed",AnimatorControllerParameterType.Float);controller.AddParameter("Gait",AnimatorControllerParameterType.Float);controller.parameters=controller.parameters.Select(p=>{if(p.name=="Gait")p.defaultFloat=1;return p;}).ToArray();
            var sm=controller.layers[0].stateMachine;var loco=sm.AddState("Locomotion");var tree=new BlendTree{name="CC0 speed ranges",blendParameter="Speed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(tree,controller);tree.AddChild(lib.idle,0);tree.AddChild(lib.walk,lib.walkSpeed);tree.AddChild(lib.run,lib.runSpeed);loco.motion=tree;loco.speedParameter="Gait";loco.speedParameterActive=true;sm.defaultState=loco;
            foreach(var row in new[]{("SitDown",lib.sitEnter), ("Seated",lib.sitIdle),("StandUp",lib.sitExit)}){var state=sm.AddState(row.Item1);state.motion=row.Item2;state.speed=row.Item1=="Seated"?1:row.Item2.length/.9f;}
            lib.controller=controller;AssetDatabase.CreateAsset(lib,path);return lib;
        }
        internal static float Supports(AnimationClip clip,GameObject root,out AnimationCurve left,out AnimationCurve right)
        {
            var model=root.GetComponentInChildren<Animator>();var all=model.GetComponentsInChildren<Transform>();var feet=new[]{all.First(t=>t.name=="DEF-foot.L"),all.First(t=>t.name=="DEF-foot.R")};
            const int n=81;var samples=new Vector3[2,n];var minimum=new[]{100f,100f};var speeds=new System.Collections.Generic.List<float>();
            for(int k=0;k<n;k++){clip.SampleAnimation(model.gameObject,clip.length*k/(n-1));for(int f=0;f<2;f++){samples[f,k]=root.transform.InverseTransformPoint(feet[f].position);minimum[f]=Mathf.Min(minimum[f],samples[f,k].y);}}
            float dt=clip.length/(n-1);
            for(int f=0;f<2;f++)for(int k=1;k<n-1;k++){var v=(samples[f,k+1]-samples[f,k-1])/(2*dt);if(samples[f,k].y<minimum[f]+.035f&&v.z<-.15f)speeds.Add(-v.z);}
            if(speeds.Count<2)throw new InvalidDataException("No support-foot speed in "+clip.name);speeds.Sort();float speed=speeds[speeds.Count/2];
            var result=new AnimationCurve[2];for(int f=0;f<2;f++){var keys=new Keyframe[n];for(int k=0;k<n;k++){int a=Mathf.Max(0,k-1),b=Mathf.Min(n-1,k+1);var v=(samples[f,b]-samples[f,a])/Mathf.Max(.0001f,(b-a)*dt)+Vector3.forward*speed;float value=samples[f,k].y<minimum[f]+.045f&&v.magnitude<.65f?1:0;keys[k]=new Keyframe(k/(float)(n-1),value);}result[f]=new AnimationCurve(keys);for(int i=0;i<n;i++){AnimationUtility.SetKeyLeftTangentMode(result[f],i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(result[f],i,AnimationUtility.TangentMode.Linear);}}
            left=result[0];right=result[1];Debug.Log("ANIMATION_SUPPORT "+clip.name+" speed="+speed+" lowFoot="+minimum[0]+","+minimum[1]);return speed;
        }
        static GameObject Character(AnimationLabLibrary library)
        {
            string path=Root+"/Prefabs/Character.prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab!=null)return prefab;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Game+"/Prefabs/Passenger3D.prefab"));
            go.name="AnimationCharacter";
            foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2; // exclude this actor from environment probes
            var actor=go.AddComponent<AnimationLabCharacter>();actor.library=library;actor.animator=go.GetComponentInChildren<Animator>(true);actor.motor=go.GetComponent<CharacterController>();actor.environment=1;actor.animator.runtimeAnimatorController=library.controller;
            actor.contacts=go.AddComponent<AnimationLabContacts>();actor.contacts.character=actor;actor.anchors=Array.Empty<AnimationLabAnchor>();
            prefab=PrefabUtility.SaveAsPrefabAsset(go,path);UnityEngine.Object.DestroyImmediate(go);return prefab;
        }
        static void BuildScene(string field,GameObject prefab)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=new GameObject("TEST_Animation_"+field);
            var scope=root.AddComponent<Metro3DRenderScope>();scope.pipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(Game+"/Data/Pipeline3D.asset");
            RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.52f,.55f);
            var light=new GameObject("Neutral key").AddComponent<Light>();light.type=LightType.Directional;light.color=Color.white;light.intensity=1.6f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(48,-38,0);light.transform.SetParent(root.transform);
            var fill=new GameObject("Neutral fill").AddComponent<Light>();fill.type=LightType.Directional;fill.color=Color.white;fill.intensity=.5f;fill.transform.rotation=Quaternion.Euler(35,145,0);fill.transform.SetParent(root.transform);
            Box(root.transform,"Ground",new Vector3(0,-.15f,3),new Vector3(24,.3f,24),floor);
            for(int i=-10;i<=10;i++){Box(root.transform,"Metre X "+i,new Vector3(i,.004f,3),new Vector3(.012f,.006f,24),edge,false);Box(root.transform,"Metre Z "+i,new Vector3(0,.004f,i+3),new Vector3(24,.006f,.012f),edge,false);}
            Label(root.transform,field+" — Animation validation",new Vector3(0,.05f,-3),0,.15f);
            if(field=="Normal"){
                foreach(float yaw in new[]{-35f,35f}){var lane=new GameObject("Corner "+yaw);lane.transform.SetParent(root.transform);lane.transform.position=new Vector3(yaw<0?-4:4,0,5);lane.transform.rotation=Quaternion.Euler(0,yaw,0);Box(lane.transform,"Inner barrier",new Vector3(-1,.55f,1),new Vector3(.18f,1.1f,5),edge);Box(lane.transform,"Outer barrier",new Vector3(1,.55f,1),new Vector3(.18f,1.1f,5),edge);}
                Ramp(root.transform,new Vector3(-7,0,-1),7);Ramp(root.transform,new Vector3(7,0,-1),14);
            }
            if(field=="Sit"){
                Seat(root.transform,new Vector3(-4,0,4),180,.38f);Seat(root.transform,new Vector3(0,0,4),145,.455f);Seat(root.transform,new Vector3(4,0,4),220,.54f);
            }
            if(field=="Lean"){
                Wall(root.transform,new Vector3(-4,0,4),180,0);Wall(root.transform,new Vector3(0,0,5),145,5);Wall(root.transform,new Vector3(4,0,4),220,-5);
                var column=new GameObject("Round support");column.transform.SetParent(root.transform);column.transform.position=new Vector3(6,0,-2);var body=GameObject.CreatePrimitive(PrimitiveType.Cylinder);body.name="Column surface";body.transform.SetParent(column.transform,false);body.transform.localPosition=new Vector3(0,1.3f,0);body.transform.localScale=new Vector3(1.2f,1.3f,1.2f);body.GetComponent<Renderer>().sharedMaterial=wall;
                Anchor(column,AnimationLabAnchor.Kind.Wall,body.GetComponent<Collider>(),new Vector3(0,1.4f,-.6f),new Vector3(0,0,-.95f),180,"곡면 지지");
            }
            if(field=="Stairs"){
                for(int i=0;i<7;i++)Box(root.transform,"Step "+(i+1),new Vector3(-3,(i+1)*.09f,2+i*.3f),new Vector3(2,(i+1)*.18f,.3f),edge);
                Box(root.transform,"Landing",new Vector3(-3,.63f,4.7f),new Vector3(2,1.26f,1.5f),edge);Ramp(root.transform,new Vector3(3,0,2),14);
                Label(root.transform,"Stair clip missing — bounded foot adaptation only",new Vector3(0,.05f,0),0,.09f);
            }
            var actor=((GameObject)PrefabUtility.InstantiatePrefab(prefab)).GetComponent<AnimationLabCharacter>();actor.field="Walk";actor.transform.position=Vector3.zero;actor.anchors=root.GetComponentsInChildren<AnimationLabAnchor>();
            var camera=new GameObject("Third person camera").AddComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=48;camera.nearClipPlane=.05f;camera.farClipPlane=80;camera.backgroundColor=new Color(.38f,.43f,.48f);camera.clearFlags=CameraClearFlags.SolidColor;camera.gameObject.AddComponent<AudioListener>();camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
            var orbit=camera.gameObject.AddComponent<AnimationLabCamera>();orbit.character=actor;actor.orbit=orbit;camera.transform.SetPositionAndRotation(new Vector3(0,1.8f,-3.4f),Quaternion.Euler(12,0,0));
            root.AddComponent<AnimationLabWalkReview>().manual=actor;
            EditorSceneManager.SaveScene(scene,Root+"/"+field+".unity");
        }
        static void Seat(Transform root,Vector3 position,float yaw,float height)
        {
            var go=new GameObject("Seat "+height.ToString("F3")+"m");go.transform.SetParent(root);go.transform.position=position;go.transform.rotation=Quaternion.Euler(0,yaw,0);
            var cushion=Box(go.transform,"Cushion",new Vector3(0,height-.035f,0),new Vector3(.64f,.07f,.54f),seat);Box(go.transform,"Back",new Vector3(0,height+.23f,-.25f),new Vector3(.64f,.48f,.06f),seat);
            foreach(float x in new[]{-.24f,.24f})foreach(float z in new[]{-.20f,.20f})Box(go.transform,"Leg",new Vector3(x,(height-.07f)/2,z),new Vector3(.04f,height-.07f,.04f),edge);
            Anchor(go,AnimationLabAnchor.Kind.Seat,cushion.GetComponent<Collider>(),new Vector3(0,height,0),new Vector3(0,0,.56f),0,"좌면 "+height.ToString("F3")+"m");
        }
        static void Wall(Transform root,Vector3 position,float yaw,float tilt)
        {
            var go=new GameObject("Wall "+yaw+" / tilt "+tilt);go.transform.SetParent(root);go.transform.position=position;go.transform.rotation=Quaternion.Euler(0,yaw,0);
            var surface=Box(go.transform,"Support panel",new Vector3(0,1.3f,0),new Vector3(2.2f,2.6f,.12f),wall);surface.transform.localRotation=Quaternion.Euler(tilt,0,0);
            float faceZ=Mathf.Tan(tilt*Mathf.Deg2Rad)*.1f+.06f;Anchor(go,AnimationLabAnchor.Kind.Wall,surface.GetComponent<Collider>(),new Vector3(0,1.4f,faceZ),new Vector3(0,0,.36f),0,"벽 "+yaw+"° / 경사 "+tilt+"°");
        }
        static void Anchor(GameObject go,AnimationLabAnchor.Kind kind,Collider surface,Vector3 point,Vector3 approach,float yaw,string label)
        {
            var anchor=go.AddComponent<AnimationLabAnchor>();anchor.kind=kind;anchor.surface=surface;anchor.label=label;
            anchor.contact=new GameObject("Contact").transform;anchor.contact.SetParent(go.transform,false);anchor.contact.localPosition=point;anchor.contact.localRotation=Quaternion.Euler(0,yaw,0);
            anchor.approach=new GameObject("Approach").transform;anchor.approach.SetParent(go.transform,false);anchor.approach.localPosition=approach;
            anchor.exit=new GameObject("Exit").transform;anchor.exit.SetParent(go.transform,false);anchor.exit.localPosition=approach+anchor.contact.localRotation*Vector3.forward*.7f;
            Box(go.transform,"Approach marker",approach+Vector3.up*.009f,new Vector3(.36f,.01f,.18f),mark,false);
            Label(go.transform,label,new Vector3(0,.025f,approach.z+.5f),yaw,.075f);
        }
        static void Ramp(Transform root,Vector3 position,float degrees)
        {
            var go=new GameObject("Slope "+degrees+" degrees");go.transform.SetParent(root);go.transform.position=position;
            float rise=Mathf.Tan(degrees*Mathf.Deg2Rad)*4;
            var ramp=Box(go.transform,"Ramp",new Vector3(0,rise/2,2),new Vector3(2,.12f,4/Mathf.Cos(degrees*Mathf.Deg2Rad)),edge);ramp.transform.localRotation=Quaternion.Euler(-degrees,0,0);
            Label(go.transform,degrees+" degrees",new Vector3(0,.03f,-.5f),0,.1f);
        }
        static GameObject Box(Transform parent,string name,Vector3 position,Vector3 scale,Material material,bool collision=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;if(!collision)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static void Label(Transform root,string text,Vector3 position,float yaw,float size)
        {
            var go=new GameObject(text);go.transform.SetParent(root,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(90,yaw,0);var label=go.AddComponent<TextMesh>();label.font=LabelFont();go.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;label.text=text;label.fontSize=48;label.characterSize=size;label.anchor=TextAnchor.MiddleCenter;label.color=Color.white;
        }
        static Font LabelFont()=>AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/Fonts/NanumGothic/NanumGothic.ttf");
    }
}
#endif

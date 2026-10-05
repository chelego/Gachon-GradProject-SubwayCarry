#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using SubwayCarry.Prototype.ArtMapSlice;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SubwayCarry.Gameplay3D.Editor
{
    [InitializeOnLoad]
    public static class Metro3DBuilder
    {
        public const string Root="Assets/_Project/Scenes/Gameplay3D";
        public const string ScenePath=Root+"/SubwayCarry_3D_FirstPerson_Delivery.unity";
        static string Request=>Path.Combine(Application.dataPath,"../Temp/ThreeDRebuild/build3d.request");
        static double nextPoll;
        static Metro3DBuilder() { EditorApplication.update-=Poll; EditorApplication.update+=Poll; }
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll||EditorApplication.isCompiling||EditorApplication.isUpdating) return;
            nextPoll=EditorApplication.timeSinceStartup+3;
            string statusRequest=Path.Combine(Application.dataPath,"../Temp/ThreeDRebuild/status.request");
            if(File.Exists(statusRequest)) {
                File.Delete(statusRequest);
                var state=new EditorState {playing=EditorApplication.isPlayingOrWillChangePlaymode,activeScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path};
                for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++) {
                    var s=UnityEngine.SceneManagement.SceneManager.GetSceneAt(i); state.loadedScenes.Add(s.path); if(s.isDirty) state.dirtyScenes.Add(s.path);
                }
                File.WriteAllText(Path.Combine(Application.dataPath,"../Temp/ThreeDRebuild/editor-state.json"),JsonUtility.ToJson(state));
            }
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(!File.Exists(Request)) return;
            string request=File.ReadAllText(Request).Trim();
            File.Delete(Request); // A request runs once. Never overwrite a user's scene on repeated refreshes.
            if(request=="REFRESH") { AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); return; }
            try {
                if(request.StartsWith("REBUILD ",StringComparison.Ordinal)) {
                    var existing=EditorSceneManager.GetSceneByPath(ScenePath);
                    if(existing.isLoaded&&existing.isDirty) throw new InvalidOperationException("3D scene has unsaved edits; regeneration refused to preserve edits.");
                    using(var sha=SHA256.Create()) {
                        string hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(ScenePath))).Replace("-","");
                        if(hash!=request.Substring(8)) throw new InvalidOperationException("New 3D scene changed since verification; regeneration refused.");
                    }
                    string backup=Path.Combine(Application.dataPath,"../Temp/ThreeDRebuild/before-station-layout.unity");
                    File.Copy(ScenePath,backup,true);
                    bool loaded=existing.isLoaded,active=loaded&&UnityEngine.SceneManagement.SceneManager.GetActiveScene()==existing;
                    UnityEngine.SceneManagement.Scene placeholder=default;
                    if(loaded) {
                        placeholder=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                        // Unity rejects a second additive NewScene while an untitled placeholder exists.
                        // Save only our EMPTY temporary placeholder, never the user's edited scene.
                        EditorSceneManager.SaveScene(placeholder,"Temp/ThreeDRebuild/RebuildPlaceholder.unity");
                        EditorSceneManager.CloseScene(existing,true);
                    }
                    try { Generate(true); } catch { File.Copy(backup,ScenePath,true); throw; }
                    finally {
                        if(loaded) {
                            var reopened=EditorSceneManager.OpenScene(ScenePath,UnityEditor.SceneManagement.OpenSceneMode.Additive);
                            if(active) UnityEngine.SceneManagement.SceneManager.SetActiveScene(reopened);
                            EditorSceneManager.CloseScene(placeholder,true);
                        }
                    }
                } else Build();
                File.WriteAllText(Path.Combine(Application.dataPath,"../Temp/ThreeDRebuild/build.result"),"SUCCESS "+ScenePath);
            }
            catch(Exception ex) { Debug.LogException(ex); File.WriteAllText(Path.Combine(Application.dataPath,"../Temp/ThreeDRebuild/build.result"),"FAILED "+ex); }
        }

        [Serializable] sealed class EditorState { public bool playing; public string activeScene; public List<string> loadedScenes=new List<string>(); public List<string> dirtyScenes=new List<string>(); }

        [MenuItem("SubwayCarry/3D/Build First-Person Delivery Scene")]
        public static void Build() { Generate(false); }

        static void Generate(bool verifiedOwnedScene)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before building the 3D scene.");
            if(File.Exists(ScenePath)&&!verifiedOwnedScene) throw new InvalidOperationException("Existing 3D scene preserved. Remove the generated scene only after explicitly approving regeneration.");
            Directory.CreateDirectory(Root+"/Data"); Directory.CreateDirectory(Root+"/Art/Materials"); Directory.CreateDirectory(Root+"/Art/Textures"); Directory.CreateDirectory(Root+"/Prefabs");
            // All temporary modeling objects belong to the new scene, never the user's active scene.
            var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var mats=Metro3DKitImporter.Prepare();
            var settings=LoadOrCreate<Metro3DSettings>(Root+"/Data/Metro3DSettings.asset");
            settings.deliveries=AssetDatabase.LoadAssetAtPath<SliceDeliveryCatalog>("Assets/_Project/Scenes/Gameplay/Data/SliceDeliveryCatalog.asset");
            if(settings.deliveries==null||settings.deliveries.orders==null||settings.deliveries.orders.Length==0) throw new InvalidOperationException("Existing delivery catalog is required.");
            settings.font=settings.deliveries.uiFont;
            settings.tile=mats["Tile"]; settings.stone=mats["Stone"]; settings.metal=mats["Metal"]; settings.glass=mats["Glass"]; settings.yellow=mats["Yellow"];
            settings.rubber=mats["Rubber"]; settings.seat=mats["Seat"]; settings.ceiling=mats["Ceiling"]; settings.luminous=mats["Luminous"]; settings.box=mats["Box"];
            settings.wood=mats["Wood"]; settings.green=mats["Green"]; settings.brown=mats["Brown"]; settings.blue=mats["Blue"]; settings.white=mats["White"]; settings.red=mats["Red"];
            var renderer=LoadOrCreate<UniversalRendererData>(Root+"/Data/Renderer3D.asset"); renderer.renderingMode=RenderingMode.ForwardPlus;
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"/Data/Pipeline3D.asset");
            if(pipeline==null) { pipeline=UniversalRenderPipelineAsset.Create(renderer); AssetDatabase.CreateAsset(pipeline,Root+"/Data/Pipeline3D.asset"); }
            pipeline.supportsHDR=true; pipeline.msaaSampleCount=2; pipeline.shadowDistance=35;
            var pipelineData=new SerializedObject(pipeline);
            pipelineData.FindProperty("m_AdditionalLightsRenderingMode").intValue=(int)LightRenderingMode.PerPixel;
            pipelineData.FindProperty("m_AdditionalLightShadowsSupported").boolValue=true;
            pipelineData.FindProperty("m_AdditionalLightsShadowmapResolution").intValue=1024;
            pipelineData.FindProperty("m_AdditionalLightsShadowResolutionTierLow").intValue=128;
            pipelineData.FindProperty("m_AdditionalLightsShadowResolutionTierMedium").intValue=128;
            pipelineData.ApplyModifiedPropertiesWithoutUndo();
            pipeline.useSRPBatcher=true; settings.renderPipeline=pipeline;
            settings.environmentLibrary=CreateLibrary(mats);
            settings.passengerPrefab=CreatePassenger(settings,mats);
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            EditorUtility.SetDirty(pipeline); AssetDatabase.SaveAssetIfDirty(pipeline);
            EditorUtility.SetDirty(renderer); AssetDatabase.SaveAssetIfDirty(renderer);
            // Save only the validated new scene; retain the existing scene file/GUID until then.
                RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(.44f,.48f,.5f); RenderSettings.ambientEquatorColor=new Color(.24f,.26f,.27f); RenderSettings.ambientGroundColor=new Color(.14f,.15f,.15f);
                RenderSettings.fog=true; RenderSettings.fogMode=FogMode.Linear; RenderSettings.fogColor=new Color(.075f,.078f,.08f); RenderSettings.fogStartDistance=50; RenderSettings.fogEndDistance=160;
                RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture=AssetDatabase.LoadAssetAtPath<Cubemap>(Root+"/Art/Textures/Metro/IndoorReflection.hdr");
                if(RenderSettings.customReflectionTexture==null) throw new InvalidOperationException("Blender indoor reflection cubemap failed import.");
                var go=new GameObject("SubwayCarry_3D_World"); var world=go.AddComponent<Metro3DWorld>(); world.settings=settings; world.BuildInitial();
                var volumeObject=new GameObject("Indoor presentation"); var volume=volumeObject.AddComponent<Volume>(); volume.isGlobal=true;
                var profile=LoadOrCreate<VolumeProfile>(Root+"/Data/IndoorPost.asset");
                if(!profile.TryGet<Tonemapping>(out var tone)) tone=profile.Add<Tonemapping>(); tone.mode.Override(TonemappingMode.Neutral);
                if(!profile.TryGet<Bloom>(out var bloom)) bloom=profile.Add<Bloom>(); bloom.intensity.Override(.055f); bloom.threshold.Override(1.3f);
                if(!profile.TryGet<ColorAdjustments>(out var colors)) colors=profile.Add<ColorAdjustments>(); colors.contrast.Override(7); colors.saturation.Override(-9);
                volume.sharedProfile=profile; EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
                Validate(scene,world);
                if(!EditorSceneManager.SaveScene(scene,ScenePath)) throw new IOException("Unable to save 3D scene.");
            } finally { EditorSceneManager.CloseScene(scene,true); if(previous.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous); }
            Debug.Log("[SubwayCarry 3D] Generated first-person delivery scene with Blender assets. Existing 2D scenes and project rendering configuration unchanged.");
        }

        static T LoadOrCreate<T>(string path) where T:ScriptableObject
        {
            var asset=AssetDatabase.LoadAssetAtPath<T>(path); if(asset!=null) return asset;
            asset=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset,path); return asset;
        }

        static GameObject CreateLibrary(Dictionary<string,Material> mats)
        {
            var root=new GameObject("Blender_StationKit");
            foreach(string path in Directory.GetFiles(Root+"/Art/Models","*.fbx")) {
                string name=Path.GetFileNameWithoutExtension(path); if(name=="PassengerHuman") continue;
                var imported=AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\','/'));
                if(imported==null) throw new InvalidOperationException("FBX import failed: "+path);
                // Preserve Blender FBX's axis/unit transform underneath an identity placement pivot.
                var pivot=new GameObject(name); pivot.transform.SetParent(root.transform,false);
                var child=UnityEngine.Object.Instantiate(imported,pivot.transform); child.name="BlenderSource";
                foreach(var r in child.GetComponentsInChildren<Renderer>()) {
                    var mapped=r.sharedMaterials;
                    for(int i=0;i<mapped.Length;i++) { string materialName=mapped[i]!=null?mapped[i].name:"Tile"; if(mats.TryGetValue(materialName,out var m)) mapped[i]=m; }
                    r.sharedMaterials=mapped;
                }
                pivot.SetActive(false);
            }
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/BlenderStationKit.prefab"); UnityEngine.Object.DestroyImmediate(root); return prefab;
        }

        static GameObject CreatePassenger(Metro3DSettings settings,Dictionary<string,Material> mats)
        {
            const string path=Root+"/Art/Models/PassengerHuman.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path); importer.animationType=ModelImporterAnimationType.Generic; importer.importAnimation=true;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;
            var clips=importer.defaultClipAnimations;
            foreach(var clip in clips) { clip.loopTime=clip.name.EndsWith("Idle")||clip.name.EndsWith("Walk")||clip.name.EndsWith("|Sit"); clip.lockRootRotation=true; clip.lockRootPositionXZ=true; clip.keepOriginalPositionY=true; }
            importer.clipAnimations=clips; importer.SaveAndReimport();
            var all=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            settings.idle=all.FirstOrDefault(c=>c.name.EndsWith("Idle")); settings.walk=all.FirstOrDefault(c=>c.name.EndsWith("Walk")); settings.sit=all.FirstOrDefault(c=>c.name.EndsWith("|Sit"));
            if(settings.idle==null||settings.walk==null||settings.sit==null) throw new InvalidOperationException("Idle/Walk/Sit clips missing: "+string.Join(",",all.Select(c=>c.name)));
            string controllerPath=Root+"/Data/Passenger3D.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(controller==null) controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.parameters=new[] {new AnimatorControllerParameter{name="Speed",type=AnimatorControllerParameterType.Float},new AnimatorControllerParameter{name="Seated",type=AnimatorControllerParameterType.Bool},new AnimatorControllerParameter{name="Gait",type=AnimatorControllerParameterType.Float,defaultFloat=1}};
            var sm=controller.layers[0].stateMachine;
            foreach(var old in sm.states) sm.RemoveState(old.state);
            foreach(var old in AssetDatabase.LoadAllAssetsAtPath(controllerPath).OfType<BlendTree>()) UnityEngine.Object.DestroyImmediate(old,true);
            var move=sm.AddState("Locomotion"); var sit=sm.AddState("Seated"); var down=sm.AddState("SitDown"); var up=sm.AddState("StandUp"); sm.defaultState=move;
            var blend=new BlendTree {name="Idle to Walk",blendParameter="Speed",useAutomaticThresholds=false}; AssetDatabase.AddObjectToAsset(blend,controller); blend.AddChild(settings.idle,0); blend.AddChild(settings.walk,1.25f); move.motion=blend; sit.motion=settings.sit;
            move.speedParameter="Gait"; move.speedParameterActive=true;
            down.motion=all.Single(c=>c.name.EndsWith("SitDown")); up.motion=all.Single(c=>c.name.EndsWith("StandUp"));
            var settle=down.AddTransition(sit); settle.hasExitTime=true; settle.exitTime=1; settle.duration=.08f;
            var resume=up.AddTransition(move); resume.hasExitTime=true; resume.exitTime=1; resume.duration=.12f;
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssetIfDirty(controller);
            var root=new GameObject("Passenger_3D_Human");
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.transform); model.name="HumanModel";
            var anim=model.GetComponent<Animator>(); if(anim==null) anim=model.AddComponent<Animator>(); anim.runtimeAnimatorController=controller; anim.applyRootMotion=false; anim.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
            var skin=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Art/Materials/PassengerClothed.mat");
            if(skin==null) { skin=new Material(Shader.Find("Universal Render Pipeline/Lit")); skin.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/Quaternius/AnimatedHuman/Textures/ClothedLightSkin.png")); skin.SetFloat("_Smoothness",.22f); AssetDatabase.CreateAsset(skin,Root+"/Art/Materials/PassengerClothed.mat"); }
            skin.SetColor("_BaseColor",Color.white); skin.enableInstancing=true; EditorUtility.SetDirty(skin); AssetDatabase.SaveAssetIfDirty(skin);
            foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>()) { r.sharedMaterial=r.name.Contains("CommuterShoe")?mats["Rubber"]:skin; r.updateWhenOffscreen=false; }
            var cc=root.AddComponent<CharacterController>(); cc.height=1.74f; cc.radius=.22f; cc.center=Vector3.up*.88f; cc.stepOffset=.21f; cc.skinWidth=.025f;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/Passenger3D.prefab"); UnityEngine.Object.DestroyImmediate(root); return prefab;
        }

        static Dictionary<string,Material> CreateMaterials()
        {
            var colors=new Dictionary<string,Color> {
                {"Tile",new Color(.86f,.83f,.67f)}, {"Stone",new Color(.48f,.49f,.47f)}, {"Metal",new Color(.62f,.65f,.66f)}, {"Glass",new Color(.71f,.8f,.8f,.1f)},
                {"Yellow",new Color(.96f,.72f,.04f)}, {"Rubber",new Color(.055f,.06f,.065f)}, {"Seat",new Color(.24f,.34f,.4f)}, {"Ceiling",new Color(.84f,.85f,.82f)}, {"Luminous",new Color(.93f,.97f,1)}, {"Box",new Color(.69f,.45f,.25f)},
                {"Wood",new Color(.5f,.32f,.13f)}, {"Green",new Color(.07f,.31f,.18f)}, {"Brown",new Color(.32f,.23f,.13f)}, {"Blue",new Color(.025f,.12f,.28f)}, {"White",new Color(.88f,.89f,.86f)}, {"Red",new Color(.6f,.04f,.025f)} };
            var output=new Dictionary<string,Material>();
            foreach(var item in colors) {
                string name=item.Key,path=Root+"/Art/Materials/"+name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path); if(mat==null) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path); }
                mat.SetColor("_BaseColor",item.Value); mat.SetFloat("_Smoothness",name=="Metal"?.68f:name=="Tile"?.38f:name=="Glass"?.84f:.25f); mat.SetFloat("_Metallic",name=="Metal"?1:0);
                mat.enableInstancing=true;
                if(name=="Tile"||name=="Stone"||name=="Metal"||name=="Wood") {
                    Textures(name,item.Value);
                    mat.SetColor("_BaseColor",Color.white); mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/Textures/"+name+"_BaseColor.png"));
                    mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/Textures/"+name+"_Normal.png")); mat.SetFloat("_BumpScale",.45f); mat.EnableKeyword("_NORMALMAP");
                    mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/Textures/"+name+"_MetallicSmoothness.png")); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    mat.SetTexture("_OcclusionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/Textures/"+name+"_AO.png")); mat.SetFloat("_OcclusionStrength",.65f);
                    mat.mainTextureScale=name=="Stone"?Vector2.one/1.2f:name=="Tile"?Vector2.one/.6f:Vector2.one;
                }
                if(name=="Glass") { mat.SetFloat("_Surface",1); mat.SetFloat("_Blend",0); mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha); mat.SetFloat("_ZWrite",0); mat.SetFloat("_Cull",0); mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue=3000; }
                if(name=="Luminous"||name=="Blue") { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor",item.Value*(name=="Blue"?.3f:2)); }
                EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat); output.Add(name,mat);
            }
            return output;
        }

        static void Textures(string name,Color baseColor)
        {
            const int n=512; var baseMap=new Color32[n*n]; var normal=new Color32[n*n]; var rough=new Color32[n*n]; var metal=new Color32[n*n]; var ao=new Color32[n*n]; var packed=new Color32[n*n];
            for(int y=0;y<n;y++) for(int x=0;x<n;x++) {
                int index=y*n+x; float u=x/(float)n,v=y/(float)n;
                float noise=Mathf.PerlinNoise(u*190+23,v*190+71); int cell=name=="Stone"?256:128;
                bool grout=(name=="Tile"||name=="Stone")&&(x%cell<2||y%cell<2);
                float variation=name=="Stone"?(noise-.5f)*.045f+(((x/cell+y/cell)%2==0)? .052f:-.035f):name=="Metal"?(Mathf.Sin(y*1.3f)*.006f):name=="Wood"?(Mathf.PerlinNoise(u*6,v*150)-.5f)*.065f:(Mathf.PerlinNoise(Mathf.Floor(x/128f)*2+3,Mathf.Floor(y/128f)*3)-.5f)*.025f;
                baseMap[index]=grout?new Color(.46f,.47f,.44f):new Color(baseColor.r+variation,baseColor.g+variation,baseColor.b+variation);
                float nx=0,ny=0;
                if(name=="Tile"||name=="Stone") { if(x%cell==2) nx=.25f; if(x%cell==cell-1) nx=-.25f; if(y%cell==2) ny=.25f; if(y%cell==cell-1) ny=-.25f; }
                normal[index]=new Color(.5f+nx*.5f,.5f+ny*.5f,1,1);
                float r=grout?.82f:name=="Stone"?.57f+(noise-.5f)*.06f:name=="Metal"?.28f+Mathf.Sin(y*3)*.015f:name=="Wood"?.53f+(noise-.5f)*.04f:.4f+noise*.035f;
                float m=name=="Metal"?1:0; float oc=grout?.8f:1;
                rough[index]=new Color(r,r,r); metal[index]=new Color(m,m,m); ao[index]=new Color(oc,oc,oc);
                packed[index]=new Color(m,m,m,1-r);
            }
            WriteTexture(name,"BaseColor",baseMap,false); WriteTexture(name,"Normal",normal,true); WriteTexture(name,"Roughness",rough,false,true);
            WriteTexture(name,"Metallic",metal,false,true); WriteTexture(name,"AO",ao,false,true); WriteTexture(name,"MetallicSmoothness",packed,false,true);
        }
        static void WriteTexture(string name,string channel,Color32[] pixels,bool isNormal,bool linear=false)
        {
            string path=Root+"/Art/Textures/"+name+"_"+channel+".png"; var texture=new Texture2D(512,512,TextureFormat.RGBA32,false); texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport); var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=isNormal?TextureImporterType.NormalMap:TextureImporterType.Default; importer.sRGBTexture=!isNormal&&!linear; importer.mipmapEnabled=true; importer.wrapMode=TextureWrapMode.Repeat; importer.anisoLevel=4; importer.SaveAndReimport();
        }
        internal static void Validate(UnityEngine.SceneManagement.Scene scene,Metro3DWorld world)
        {
            if(world.player==null||world.train==null||world.settings.passengerPrefab==null||world.stations.Count!=1) throw new InvalidOperationException("3D scene wiring incomplete.");
            if(world.train.panels.Count!=Metro3DTrain.DoorZ.Length*2||world.train.blockers.Count!=Metro3DTrain.DoorZ.Length) throw new InvalidOperationException("Train doors incomplete.");
            if(world.facilities.Count(f=>f.kind==Metro3DFacilityKind.Seat)<24) throw new InvalidOperationException("Seat anchors incomplete.");
            foreach(var filter in world.GetComponentsInChildren<MeshFilter>(true)) if(filter.sharedMesh==null) throw new InvalidOperationException("Missing Blender mesh: "+filter.name);
            Physics.SyncTransforms();
            var station=world.stations[0];
            var psds=station.GetComponentsInChildren<Metro3DPlatformDoors>();
            if(psds.Length!=2||psds.Any(d=>d.panels.Count!=Metro3DTrain.DoorZ.Length*2||d.blockers.Count!=Metro3DTrain.DoorZ.Length)||psds.Select(d=>d.trainSide).Distinct().Count()!=2)
                throw new InvalidOperationException("Two independent directional side-platform screen doors required.");
            var floor=station.Find("B2 Jeongja side platform").GetComponentInChildren<MeshRenderer>();
            if(Mathf.Abs(floor.bounds.size.x-(9-Metro3DWorld.ScreenDoorX))>.05f||Mathf.Abs(floor.bounds.size.z-Metro3DWorld.PlatformLength)>.05f||Mathf.Abs(floor.bounds.max.y)>.02f)
                throw new InvalidOperationException("Side-platform imported floor axis/scale mismatch: "+floor.bounds);
            foreach(int side in new[]{1,-1}) {
                var near=station.Find("B1_B2_"+(side==1?"Jeongja":"Wangsimni")+"_near_Stairs");
                var far=station.Find("B1_B2_"+(side==1?"Jeongja":"Wangsimni")+"_far_Stairs");
                if(near==null||far==null||near.GetComponentsInChildren<MeshCollider>().Length==0)
                    throw new InvalidOperationException("Both-end circulation incomplete.");
                Bounds stairBounds=new Bounds(); bool first=true;
                foreach(var mesh in near.GetComponentsInChildren<MeshRenderer>()) { if(first) {stairBounds=mesh.bounds;first=false;} else stairBounds.Encapsulate(mesh.bounds); }
                if(Mathf.Abs(stairBounds.min.z)>.04f||Mathf.Abs(stairBounds.max.z-11.1f)>.04f||Mathf.Abs(stairBounds.max.y-4.635f)>.06f)
                    throw new InvalidOperationException("Stair imported orientation or rise differs from collision: "+stairBounds);
                var anchors=near.GetComponentsInChildren<Transform>().Where(a=>a.name.Contains("StepContact_")).ToArray();
                if(anchors.Length!=24) throw new InvalidOperationException("Blender staircase must provide 24 real tread contacts.");
                foreach(var anchor in anchors) {
                    if(!Physics.Raycast(anchor.position+Vector3.up*.075f,Vector3.down,out var hit,.1f,~0,QueryTriggerInteraction.Ignore)||!hit.transform.IsChildOf(near))
                        throw new InvalidOperationException("Visible tread lacks matching physical floor: "+anchor.name);
                }
            }
            // Actual imported scene physics: unobstructed headroom along each flight and floor holes.
            foreach(float x in new[]{-7.76f,7.76f}) foreach(bool far in new[]{false,true}) for(int i=0;i<24;i++) {
                float offset=(i<12?1.5f:6.3f)+(i%12+.5f)*.30f;
                float z=far?Metro3DWorld.FarStairBottom+offset:11.1f-offset;
                float y=(i+1)*.15f;
                // Exclude the permitted .22m step-up band: the capsule's foot intersects the NEXT
                // .15m riser by design, and CharacterController.stepOffset handles that contact.
                var overlaps=Physics.OverlapCapsule(new Vector3(x,y+.48f,z),new Vector3(x,y+1.49f,z),.22f,~0,QueryTriggerInteraction.Ignore);
                if(overlaps.Length>0) throw new InvalidOperationException("Stair headroom obstructed: "+x+", "+y+", "+z+" by "+string.Join(",",overlaps.Select(c=>c.name)));
            }
            foreach(var f in world.facilities.Where(f=>f.kind==Metro3DFacilityKind.Seat&&!f.trainFacility))
                if(Physics.CheckCapsule(f.approach.position+Vector3.up*.3f,f.approach.position+Vector3.up*1.4f,.22f,~0,QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException("Seat approach blocked: "+f.approach.position);
            if(world.facilities.Count(f=>f.kind==Metro3DFacilityKind.FareGate)!=10) throw new InvalidOperationException("Both concourse gate banks required.");
            var carriage=world.train.transform.Find("Carriage floor").GetComponent<BoxCollider>();
            if(Mathf.Abs(carriage.size.x-2.85f)>.01f||Mathf.Abs(carriage.size.z-Metro3DTrain.CarLength)>.01f)
                throw new InvalidOperationException("Carriage collision scale mismatch: "+carriage.size);
            if(world.train.transform.Cast<Transform>().Count(t=>t.name.StartsWith("Carriage_"))!=4||world.train.transform.Cast<Transform>().Count(t=>t.name.StartsWith("Walkable gangway"))!=3)
                throw new InvalidOperationException("Four real carriages and three walkable connections required.");
            // Continuous ground and headroom through all three connections, independent of decorative geometry.
            for(int c=0;c<3;c++) for(int sample=0;sample<=12;sample++) {
                Vector3 feet=world.train.transform.TransformPoint(new Vector3(0,0,Metro3DTrain.FloorStart+Metro3DTrain.CarLength+c*Metro3DTrain.CarPitch-.2f+sample*.1f));
                if(!Physics.Raycast(feet+Vector3.up*.07f,Vector3.down,out var hit,.15f)||!hit.transform.IsChildOf(world.train.transform)) throw new InvalidOperationException("Gangway floor discontinuity at "+feet);
                if(Physics.CheckCapsule(feet+Vector3.up*.3f,feet+Vector3.up*1.49f,.24f,~0,QueryTriggerInteraction.Ignore)) throw new InvalidOperationException("Gangway passage obstructed at "+feet);
            }
            var lifts=station.GetComponentsInChildren<Metro3DLift>();
            if(lifts.Length!=2||lifts.Any(l=>l.cabin==null||l.leaves.Length!=4||l.doorBlockers.Length!=2)) throw new InvalidOperationException("Two moving B1/B2 lifts required.");
            var escalators=station.GetComponentsInChildren<Metro3DEscalator>();
            if(escalators.Length!=4||escalators.Any(e=>e.transform.parent.GetComponentsInChildren<Transform>().Count(a=>a.name.StartsWith("MovingStep_")&&a.name.Length==13)<25))
                throw new InvalidOperationException("Four complete independently moving Blender step bands required.");
            foreach(var escalator in escalators) {
                var frame=escalator.transform.parent;
                var ramp=frame.GetComponentsInChildren<MeshCollider>().SingleOrDefault(c=>c.name.StartsWith("CollisionSurface"));
                if(ramp==null||!ramp.enabled) throw new InvalidOperationException("Escalator Blender collision surface missing.");
                for(int i=0;i<=48;i++) {
                    float along=Mathf.Lerp(.02f,Metro3DEscalator.Length-.02f,i/48f);
                    Vector3 feet=frame.TransformPoint(new Vector3(0,Metro3DEscalator.Height(along),-along));
                    // Check the authored surface directly: a global first-hit query arbitrarily
                    // picks the coplanar station slab under either flat landing.
                    if(!ramp.Raycast(new Ray(feet+Vector3.up*.08f,Vector3.down),out var hit,.15f)||Mathf.Abs(hit.point.y-feet.y)>.065f)
                        throw new InvalidOperationException("Escalator authored profile lacks matching floor at "+feet+"; ramp bounds "+ramp.bounds);
                    var blocked=Physics.OverlapCapsule(feet+Vector3.up*.48f,feet+Vector3.up*1.49f,.24f,~0,QueryTriggerInteraction.Ignore);
                    if(blocked.Length>0) throw new InvalidOperationException("Escalator headroom obstructed at "+feet+" by "+string.Join(",",blocked.Select(c=>c.name)));
                }
            }
            foreach(var sign in world.GetComponentsInChildren<Metro3DWorldSign>()) if(sign.GetComponent<TextMesh>().font==null) throw new InvalidOperationException("Sign font missing.");
            Debug.Log("[SubwayCarry 3D] Structural validation passed: opposing side platforms, both-end stairs, unobstructed headroom/seat approaches, 2 gate banks, 2 independently controlled PSD sides, moving carriage. Not visual or Play Mode approval.");
        }
        [MenuItem("SubwayCarry/3D/Open First-Person Delivery Scene")]
        static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
#endif

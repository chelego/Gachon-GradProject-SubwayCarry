#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.InferenceEngine;
using SubwayCarry.Gameplay3D;
using Kit=SubwayCarry.EditorTools.AI.V2.PassengerAiV23DSceneBuilder;

namespace SubwayCarry.AI.HelpNPC.Editor
{
    [InitializeOnLoad]
    public static class HelperTrainingBuilder
    {
        public const string Root="Assets/_Project/Scenes/3D/Test/AI/HelpNPC";
        public const string TrainingScene=Root+"/AI_V2_06_Helper_Training_Test.unity", DemoScene=Root+"/AI_V2_06_Helper_Demo_Test.unity";
        public const string PreviewScene=Root+"/AI_V2_06_Helper_LearningPreview_Test.unity";
        const string Request="Temp/MLAgents/setup.request",Result="Temp/MLAgents/setup.result";
        static double nextCheck;
        static HelperTrainingBuilder(){EditorApplication.update+=Poll;}
        public static void BuildBatch()
        {
            try {
                if(!File.Exists(TrainingScene))BuildScenes();
                Directory.CreateDirectory("Temp/MLAgents/Build");
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=new[]{TrainingScene},locationPathName="Temp/MLAgents/Build/HelperTraining.exe",
                    target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
                if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Training build "+report.summary.result);
                File.WriteAllText(Result,"PASS batch "+DateTime.UtcNow.ToString("o"));
                EditorApplication.Exit(0);
            }catch(Exception ex){File.WriteAllText(Result,"FAILED "+ex);Debug.LogException(ex);EditorApplication.Exit(1);}
        }
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextCheck||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            nextCheck=EditorApplication.timeSinceStartup+1;
            if(!File.Exists(Request))return;
            string action=File.ReadAllText(Request).Trim();File.Delete(Request);
            if(EditorApplication.isPlayingOrWillChangePlaymode){File.WriteAllText(Result,"BLOCKED: Play active");return;}
            foreach(var scene in EnumerableScenes())if(scene.isDirty){File.WriteAllText(Result,"BLOCKED: Unsaved scene "+scene.path);return;}
            if(action=="show"){EditorSceneManager.OpenScene(DemoScene);File.WriteAllText(Result,"PASS: helper demo opened");return;}
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try {
                if(action=="setup")BuildScenes();
                else if(action=="visual"){BuildScenes();BindModel();}
                else if(action=="model")BindModel();
                else if(action=="preview")BuildLearningPreview();
                else if(action!="build")throw new InvalidOperationException("Unknown helper job");
                if(action!="model"&&action!="visual"&&action!="preview") {
                    Directory.CreateDirectory("Temp/MLAgents/Build");
                    var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                        scenes=new[]{TrainingScene},locationPathName="Temp/MLAgents/Build/HelperTraining.exe",
                        target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
                    if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Training build: "+report.summary.result);
                }
                File.WriteAllText(Result,"PASS "+action+" "+DateTime.UtcNow.ToString("o"));
            }catch(Exception ex){File.WriteAllText(Result,"FAILED "+ex);Debug.LogException(ex);}
            finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }
        static System.Collections.Generic.IEnumerable<Scene> EnumerableScenes(){for(int i=0;i<SceneManager.sceneCount;i++)yield return SceneManager.GetSceneAt(i);}
        static void BuildLearningPreview()
        {
            // Separate presentation build: the same arena and policy contract as training.
            // The demo ONNX is deliberately removed; actions come from the Python trainer.
            EditorSceneManager.OpenScene(DemoScene);
            var visible=UnityEngine.Object.FindFirstObjectByType<HelperTrainingArena>();
            var demo=visible.GetComponent<HelperDemoController>();
            if(demo!=null)UnityEngine.Object.DestroyImmediate(demo);
            visible.demonstration=false;
            var bp=visible.GetComponent<BehaviorParameters>();
            bp.Model=null;bp.BehaviorType=BehaviorType.Default;bp.DeterministicInference=false;
            for(int i=1;i<32;i++)Arena(new Vector3((i%8)*20,0,(i/8)*20),false);
            var recorder=visible.gameObject.AddComponent<HelperLearningRecorder>();
            recorder.cameraView=Camera.main;
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),PreviewScene);
            Directory.CreateDirectory("Temp/MLAgents/PreviewBuild");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{PreviewScene},locationPathName="Temp/MLAgents/PreviewBuild/HelperLearning.exe",
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Preview build: "+report.summary.result);
        }
        [MenuItem("SubwayCarry/AI/Build helper learning fields")]
        public static void BuildScenes()
        {
            Directory.CreateDirectory(Root+"/Resources");Directory.CreateDirectory(Root+"/Materials");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            for(int i=0;i<32;i++)Arena(new Vector3((i%8)*20,0,(i/8)*20),false);
            EditorSceneManager.SaveScene(scene,TrainingScene);
            scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var arena=Arena(Vector3.zero,true);
            var pipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/_Project/Scenes/3D/Ingame/Game/Data/Pipeline3D.asset");
            new GameObject("SceneRenderScope").AddComponent<Metro3DRenderScope>().pipeline=pipeline;
            var floor=Material("FieldFloor",new Color(.26f,.30f,.33f));
            var wall=Material("FieldWalls",new Color(.62f,.66f,.68f));
            var line=Material("ClearanceMark",new Color(.83f,.64f,.16f));
            Fit("Floor",Kit.FindMesh("FloorPanel"),floor,new Vector3(0,-.10f,0),new Vector3(7,.2f,12));
            Fit("WallLeft",Kit.FindMesh("WallPanel"),wall,new Vector3(-3.55f,1.5f,0),new Vector3(.16f,3,12));
            Fit("WallBack",Kit.FindMesh("WallPanel"),wall,new Vector3(0,1.5f,6),new Vector3(7,3,.16f));
            Fit("SafeSpace",Kit.FindMesh("FloorPanel"),line,new Vector3(2.2f,.006f,-.5f),new Vector3(1.1f,.012f,1.1f));
            arena.safeMarker=GameObject.Find("SafeSpace").transform;
            arena.helperVisual=Visual("HelperCitizen",new Color(.90f,.42f,.12f),out arena.helperAnimator);
            arena.playerVisual=Visual("PlayerProxy",new Color(.23f,.38f,.62f),out arena.playerAnimator);
            arena.helperRenderers=arena.helperVisual.GetComponentsInChildren<Renderer>();
            arena.playerVisual.position=new Vector3(-.1f,0,-3.7f);arena.helperVisual.position=new Vector3(.85f,0,-3.4f);
            var cargo=new GameObject("CarriedLoad").transform;cargo.SetParent(arena.playerVisual,false);cargo.localPosition=new Vector3(0,1.05f,.4f);
            var cargoModel=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Scenes/3D/Ingame/Game/Art/Models/CakeBox.fbx"));
            var cargoRenderers=cargoModel.GetComponentsInChildren<Renderer>();Bounds cargoBounds=cargoRenderers[0].bounds;
            foreach(var r in cargoRenderers)cargoBounds.Encapsulate(r.bounds);
            float cargoScale=.60f/Mathf.Max(.01f,cargoBounds.size.x);
            Vector3 importedScale=cargoModel.transform.localScale,importedPosition=cargoModel.transform.position;
            cargoModel.transform.SetParent(cargo,false);cargoModel.transform.localScale=importedScale*cargoScale;cargoModel.transform.localPosition=(importedPosition-cargoBounds.center)*cargoScale;
            var cardboard=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Scenes/3D/Ingame/Game/Art/Materials/Metro/Metro_Cardboard.mat");
            if(cardboard!=null)foreach(var r in cargoRenderers){var materials=r.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=cardboard;r.sharedMaterials=materials;}
            arena.cargoVisual=cargo;
            Kit.AddLight("NeutralKey",Quaternion.Euler(55,-25,0),1.5f,Color.white,true);
            Kit.AddLight("NeutralFill",Quaternion.Euler(25,150,0),.55f,new Color(.91f,.95f,1),false);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.35f,.38f,.42f);
            var cam=new GameObject("Main Camera").AddComponent<Camera>();cam.tag="MainCamera";
            cam.transform.position=new Vector3(5.8f,3.6f,-7.8f);cam.transform.LookAt(new Vector3(0,1,-.6f));
            cam.fieldOfView=47;cam.nearClipPlane=.1f;cam.farClipPlane=100;cam.backgroundColor=new Color(.12f,.15f,.18f);cam.clearFlags=CameraClearFlags.SolidColor;
            var demo=arena.gameObject.AddComponent<HelperDemoController>();demo.arena=arena;demo.cameraView=cam;
            demo.hudFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/Fonts/NanumGothic/NanumGothic.ttf");
            EditorSceneManager.SaveScene(scene,DemoScene);AssetDatabase.SaveAssets();
        }
        static Material Material(string name,Color color)
        {
            string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.28f);EditorUtility.SetDirty(m);return m;
        }
        static GameObject Fit(string name,Mesh mesh,Material material,Vector3 center,Vector3 size)
        {
            var b=mesh.bounds;var scale=new Vector3(size.x/Mathf.Max(.001f,b.size.x),size.y/Mathf.Max(.001f,b.size.y),size.z/Mathf.Max(.001f,b.size.z));
            return Kit.Piece(name,mesh,material,center-Vector3.Scale(b.center,scale),scale,null,false);
        }
        static HelperTrainingArena Arena(Vector3 position,bool visual)
        {
            var go=new GameObject(visual?"TEST_06_Helper_Demo":"TEST_06_Helper_TrainingArena");go.SetActive(false);go.transform.position=position;
            var arena=go.AddComponent<HelperTrainingArena>();arena.demonstration=visual;arena.demonstrationCase=2;
            var bp=go.AddComponent<BehaviorParameters>();bp.BehaviorName="HelpfulCitizen";
            bp.BrainParameters.VectorObservationSize=HelperSituation.ObservationCount;
            bp.BrainParameters.NumStackedVectorObservations=1;bp.BrainParameters.ActionSpec=ActionSpec.MakeDiscrete(4);
            bp.DeterministicInference=visual;bp.InferenceDevice=InferenceDevice.Burst;
            var model=AssetDatabase.LoadAssetAtPath<ModelAsset>(Root+"/Resources/HelpfulCitizen.onnx");
            if(visual&&model!=null){bp.Model=model;bp.BehaviorType=BehaviorType.InferenceOnly;}
            var agent=go.AddComponent<HelpfulCitizenAgent>();agent.arena=arena;agent.MaxStep=0;arena.agent=agent;
            var decision=go.AddComponent<DecisionRequester>();decision.DecisionPeriod=5;decision.TakeActionsBetweenDecisions=false;
            go.SetActive(true);return arena;
        }
        static Transform Visual(string name,Color color,out Animator animator)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Scenes/3D/Test/AI/Assets/Prefabs/TestMannequin3D.prefab");
            var heading=prefab.transform.Find("WorldBody3D/Heading");
            if(heading==null)throw new InvalidOperationException("Approved mannequin heading missing");
            var go=UnityEngine.Object.Instantiate(heading.gameObject);go.name=name;
            go.transform.SetParent(null);go.transform.position=Vector3.zero;
            animator=go.GetComponentInChildren<Animator>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var shell=Material(name+"Shell",color);
            foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                var materials=r.sharedMaterials;for(int i=0;i<materials.Length;i++)if(materials[i]!=null&&!materials[i].name.Contains("Joint"))materials[i]=shell;
                r.sharedMaterials=materials;
            }
            return go.transform;
        }
        static void BindModel()
        {
            AssetDatabase.ImportAsset(Root+"/Resources/HelpfulCitizen.onnx",ImportAssetOptions.ForceSynchronousImport);
            var model=AssetDatabase.LoadAssetAtPath<ModelAsset>(Root+"/Resources/HelpfulCitizen.onnx");
            if(model==null)throw new InvalidOperationException("Exported ONNX missing");
            var scene=EditorSceneManager.OpenScene(DemoScene);
            foreach(var bp in UnityEngine.Object.FindObjectsByType<BehaviorParameters>(FindObjectsSortMode.None)){bp.Model=model;bp.BehaviorType=BehaviorType.InferenceOnly;bp.DeterministicInference=true;}
            var demo=UnityEngine.Object.FindFirstObjectByType<HelperDemoController>();
            if(demo!=null)demo.hudFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/Fonts/NanumGothic/NanumGothic.ttf");
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif

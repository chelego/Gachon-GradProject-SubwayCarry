#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using SubwayCarry.AI.V2;
using SubwayCarry.Gameplay3D;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static SubwayCarry.EditorTools.AI.V2.PassengerAiV23DSceneBuilder;

namespace SubwayCarry.EditorTools.AI.V2
{
    public static class PassengerAiV23DFieldsBuilder
    {
        public static readonly string[] Paths = {
            ScenePath,
            SceneRoot+"/AI_V2_02_StairMerge_NarrowEntrance3D_Test.unity",
            SceneRoot+"/AI_V2_03_FareGate_QueueAndDirection3D_Test.unity",
            SceneRoot+"/AI_V2_04_TrainDoor_AlightBeforeBoard3D_Test.unity",
            SceneRoot+"/AI_V2_05_TrainInterior_SeatStandLean3D_Test.unity" };
        private static readonly Rect Lobby=Rect.MinMaxRect(-14,-8.8f,14,8.8f);
        private static Material floor,metal,accent,seat,wall,cyan,orange;
        private static GameObject prefab;

        [MenuItem("SubwayCarry/Passenger AI V2/3D/Apply obstacles 01 and build missing 02-05")]
        public static void BuildRequestedFields()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
            Scene loaded=SceneManager.GetSceneByPath(ScenePath);
            if(loaded.IsValid() && loaded.isDirty) throw new InvalidOperationException("01 has unsaved user edits; save it yourself before updating.");
            PrepareAnimations(); PrepareMaterials();
            prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/TestMannequin3D.prefab");
            if(prefab==null) throw new InvalidOperationException("First-field mannequin prefab missing.");
            ConfigureSeatPose();
            UpgradeCorridor();
            for(int number=2;number<=5;number++)
            {
                if(File.Exists(Paths[number-1])) continue; // Preserve existing scenes and GUIDs on retries.
                BuildField(number);
            }
            ValidateAll();
            Debug.Log("AI3D_ALL_FIELDS_BUILT: 01 obstacles;02 stairs;03 reserved gates;04 alight/board;05 interior Utility. Original 2D unchanged.");
        }
        private static void PrepareMaterials()
        {
            floor=NewMaterial("TestFloor",new Color(.24f,.28f,.31f),.36f);
            metal=NewMaterial("FacilityMetal",new Color(.37f,.42f,.46f),.48f);
            accent=NewMaterial("SafetyAmber",new Color(.95f,.58f,.12f),.28f);
            seat=NewMaterial("SeatBlue",new Color(.12f,.28f,.38f),.3f);
            wall=NewMaterial("BoundaryWall",new Color(.60f,.64f,.66f),.3f);
            cyan=NewMaterial("Mannequin_TealShell",new Color(.20f,.52f,.57f),.32f);
            orange=NewMaterial("Mannequin_TanShell",new Color(.72f,.49f,.31f),.32f);
        }
        private static void PrepareAnimations()
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
            var clips=new List<ModelImporterClipAnimation>();
            string[] wanted={"Idle_Loop","Walk_Loop","Jog_Fwd_Loop","Sitting_Enter","Sitting_Idle_Loop","Sitting_Exit","Interact"};
            foreach(var clip in importer.defaultClipAnimations)
            {
                bool found=false; foreach(string name in wanted) if(clip.name==name || clip.name.EndsWith("|"+name)) found=true;
                if(!found) continue;
                clip.loopTime=clip.name.EndsWith("Loop"); clip.loopPose=clip.loopTime;
                clip.lockRootRotation=true; clip.lockRootHeightY=true; clip.lockRootPositionXZ=true;
                clip.keepOriginalOrientation=true; clip.keepOriginalPositionY=true; clip.keepOriginalPositionXZ=true;
                clips.Add(clip);
            }
            if(clips.Count!=7) throw new InvalidOperationException("Seven required source animations missing.");
            importer.clipAnimations=clips.ToArray(); importer.SaveAndReimport();
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Root+"/Data/TestMannequin.controller");
            AddState(controller,"SitDown",FindClip("Sitting_Enter"));
            AddState(controller,"Sitting",FindClip("Sitting_Idle_Loop"));
            AddState(controller,"StandUp",FindClip("Sitting_Exit"));
            AddState(controller,"Interact",FindClip("Interact"));
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssetIfDirty(controller);
        }
        private static void AddState(AnimatorController controller,string name,AnimationClip clip)
        {
            var machine=controller.layers[0].stateMachine;
            foreach(var item in machine.states) if(item.state.name==name) return;
            machine.AddState(name).motion=clip;
        }
        private static void ConfigureSeatPose()
        {
            string path=Root+"/Prefabs/TestMannequin3D.prefab";
            GameObject contents=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var animator=contents.GetComponentInChildren<Animator>();
                Transform heading=contents.transform.Find("WorldBody3D/Heading"),hips=null;
                FindClip("Sitting_Idle_Loop").SampleAnimation(animator.gameObject,0);
                foreach(Transform bone in animator.GetComponentsInChildren<Transform>()) if(bone.name=="DEF-hips") hips=bone;
                if(hips==null) throw new InvalidOperationException("Sitting pelvis bone missing");
                contents.GetComponent<PassengerAiV23DVisual>().ConfigureSeatPose(heading.InverseTransformPoint(hips.position));
                FindClip("Idle_Loop").SampleAnimation(animator.gameObject,0);
                PrefabUtility.SaveAsPrefabAsset(contents,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        private static void UpgradeCorridor()
        {
            Scene previous=SceneManager.GetActiveScene(), scene=SceneManager.GetSceneByPath(ScenePath);
            bool opened=!scene.IsValid() || !scene.isLoaded;
            if(opened) scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var scenario=Find<PassengerAiV2Corridor3DScenario>(scene);
                if(scenario==null) throw new InvalidOperationException("01 scenario missing.");
                if(scenario.transform.Find("ObstacleCourse_Blender")!=null) return;
                var geometry=new GameObject("ObstacleCourse_Blender").transform; geometry.SetParent(scenario.transform,false);
                var footprints=new List<Rect>();
                foreach(Vector2 p in new[]{new Vector2(-8,-1.15f),new Vector2(0,1.1f),new Vector2(8,-1.1f)})
                { Prop("Column",metal,new Vector3(p.x,0,p.y),Vector3.one,geometry,true); footprints.Add(new Rect(p.x-.47f,p.y-.47f,.94f,.94f)); }
                Prop("SafetyBarrier",accent,new Vector3(-4,0,2.35f),Vector3.one,geometry,true); footprints.Add(new Rect(-5,2.05f,2,.6f));
                Prop("SafetyBarrier",accent,new Vector3(4,0,-2.35f),Vector3.one,geometry,true); footprints.Add(new Rect(3,-2.65f,2,.6f));
                Prop("Bench",seat,new Vector3(-11,0,-3.15f),Vector3.one,geometry,true); footprints.Add(new Rect(-12,-3.43f,2,.56f));
                Prop("Bench",seat,new Vector3(11,0,3.15f),Vector3.one,geometry,true).transform.rotation=Quaternion.Euler(0,180,0); footprints.Add(new Rect(10,2.87f,2,.56f));
                var paths=scenario.gameObject.AddComponent<PassengerAiV23DNavigation>();
                paths.Configure(new Rect(-18,-3.5f,36,7),footprints.ToArray()); scenario.ConfigureNavigation(paths);
                EditorUtility.SetDirty(scenario); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if(opened) EditorSceneManager.CloseScene(scene,true);
            }
        }
        private static void BuildField(int number)
        {
            Scene previous=SceneManager.GetActiveScene();
            bool empty=Application.isBatchMode && string.IsNullOrEmpty(previous.path) && previous.rootCount==0;
            if(!empty && string.IsNullOrEmpty(previous.path) && previous.isDirty) throw new InvalidOperationException("Save the untitled editing scene yourself first.");
            Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,empty?NewSceneMode.Single:NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var root=new GameObject("TEST_0"+number+"_3D_"+(number==2?"StairMerge":number==3?"FareGate":number==4?"TrainDoor":"TrainInterior"));
                var geo=new GameObject("Blender_Facilities").transform; geo.SetParent(root.transform,false);
                var crowd=new GameObject("CrowdManager_SpatialHash").AddComponent<PassengerAiV2CrowdManager>(); crowd.transform.SetParent(root.transform,false);
                var nav=root.AddComponent<PassengerAiV23DNavigation>(); var obstacles=new List<Rect>();
                PassengerAiV2StairSmartObject stair=null; PassengerAiV2FareGateSmartObject[] gates=null;
                PassengerAiV2TrainDoorSmartObject door=null; PassengerAiV2TrainInteriorEnvironment interior=null;
                Rect bounds=number==5?new Rect(-3,-9,6,18):Lobby;
                if(number==2) stair=Stairs(geo,obstacles);
                else if(number==3) gates=Gates(geo,obstacles);
                else if(number==4) door=Door(geo,obstacles);
                else interior=Interior(root.transform,geo,obstacles);
                if(number!=2) Floor(geo,bounds,0);
                OuterWalls(geo,bounds,number==2);
                nav.Configure(bounds,obstacles.ToArray());
                int count=number==2?16:number==3?24:number==4?20:18;
                var agents=new PassengerAiV2Agent[count]; var starts=new Vector2[count]; var goals=new Vector2[count];
                for(int i=0;i<count;i++)
                {
                    int per=count/2,index=i%per; bool forward=i<per;
                    Vector2 start,target;
                    if(number==5)
                    { start=new Vector2((i%3-1)*.8f,-7.5f+(i/3)*2.5f); target=start; }
                    else if(number==4)
                    {
                        start=forward?new Vector2(index%2==0?-.62f:.62f,1.7f+(index/2)*1.12f)
                            :new Vector2(index%2==0?-3.1f:3.1f,-2.4f-(index/2)*1.12f);
                        target=new Vector2((index%2==0?-1:1)*(3.3f+(index/2)*1.3f),forward?-7.5f:7.5f);
                    }
                    else
                    {
                        float x=Mathf.Lerp(-10,10,(float)(index%6)/5);
                        start=new Vector2(forward?x:-x,(forward?-1:1)*(6.6f+(index/6)*1.1f));
                        target=new Vector2(start.x,forward?7.8f:-7.8f);
                    }
                    starts[i]=start; goals[i]=target;
                    var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform);
                    obj.name="Passenger_"+i.ToString("00"); obj.transform.position=new Vector3(start.x,number==2?PassengerAiV23DFieldScenario.StairHeight(start):0,start.y);
                    ReplaceShell(obj,forward?cyan:orange,NewMaterial("Mannequin_DarkJoints",new Color(.045f,.055f,.065f),.28f));
                    obj.transform.Find("WorldBody3D/Heading").rotation=Quaternion.Euler(0,forward?0:180,0);
                    agents[i]=obj.GetComponent<PassengerAiV2Agent>();
                }
                root.AddComponent<PassengerAiV23DFieldScenario>().Configure(number,
                    AssetDatabase.LoadAssetAtPath<PassengerAiV2PersonalityProfile>(PassengerAiV2SceneBuilder.BaselineProfilePath),
                    crowd,agents,starts,goals,bounds,nav,stair,gates,door,interior);
                SetupView(number);
                if(!EditorSceneManager.SaveScene(scene,Paths[number-1])) throw new InvalidOperationException("Scene save failed.");
            }
            finally
            {
                if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if(scene.isLoaded && SceneManager.sceneCount>1) EditorSceneManager.CloseScene(scene,true);
            }
        }
        private static PassengerAiV2StairSmartObject Stairs(Transform geo,List<Rect> obstacles)
        {
            Floor(geo,new Rect(-14,-8.8f,28,4.64f),0); Floor(geo,new Rect(-14,4.16f,28,4.64f),4.16f);
            var steps=Prop("StairFlight",wall,Vector3.zero,Vector3.one,geo,true);
            // Verify the exported mesh's rising direction rather than assuming a Blender/FBX axis.
            Mesh mesh=steps.GetComponent<MeshFilter>().sharedMesh; float near=0,far=0;
            foreach(Vector3 v in mesh.vertices) { if(v.z< -3.8f) near=Mathf.Max(near,v.y); if(v.z>3.8f) far=Mathf.Max(far,v.y); }
            bool reverse=near>far; if(reverse) steps.transform.rotation=Quaternion.Euler(0,180,0);
            var rails=Prop("StairHandrails",metal,Vector3.zero,Vector3.one,geo,false); rails.transform.rotation=steps.transform.rotation;
            foreach(int side in new[]{-1,1})
            {
                SolidBox("StairSideMass",new Vector3(side*7.95f,0,0),new Vector3(12.1f,4.16f,8.32f),wall,geo);
                obstacles.Add(new Rect(side<0?-14:1.9f,-4.16f,12.1f,8.32f));
                Prop("Bench",seat,new Vector3(side*8,side<0?0:4.16f,side<0?-8:8),Vector3.one,geo,true);
            }
            var stair=new GameObject("SmartObject_Stair_Capacity10").AddComponent<PassengerAiV2StairSmartObject>(); stair.transform.SetParent(geo,false);
            stair.Configure("3d_stair",new Vector2(0,-4.8f),new Vector2(0,-3.9f),new Vector2(0,3.9f),new Vector2(0,4.8f),10,.78f);
            return stair;
        }
        private static PassengerAiV2FareGateSmartObject[] Gates(Transform geo,List<Rect> obstacles)
        {
            var result=new PassengerAiV2FareGateSmartObject[4];
            for(int i=0;i<4;i++)
            {
                float x=-4.5f+i*3; int sign=i<2?1:-1;
                var root=new GameObject("ReservedGate_"+i).transform; root.SetParent(geo,false);
                foreach(float side in new[]{-.62f,.62f})
                { Prop("GateHousing",metal,new Vector3(x+side,0,0),Vector3.one,root,true); obstacles.Add(new Rect(x+side-.195f,-1.325f,.39f,2.65f)); }
                Prop("CardReader",cyan,new Vector3(x-.62f,1.11f,-sign*1.02f),Vector3.one,root,false);
                var leaf=Prop("GateLeaf",accent,new Vector3(x,0,0),Vector3.one,root,true);
                GameObjectUtility.SetStaticEditorFlags(leaf,0);
                var gate=root.gameObject.AddComponent<PassengerAiV2FareGateSmartObject>();
                gate.Configure("3d_gate_"+i,i<2?PassengerAiV2FareGateDirection.LowerToUpper:PassengerAiV2FareGateDirection.UpperToLower,
                    new Vector2(x,-sign*2.05f),new Vector2(x,-sign*1.02f),new Vector2(x,sign*.9f),new Vector2(x,sign*2.05f),null);
                root.gameObject.AddComponent<PassengerAiV23DFacilityVisual>().Configure(gate,null,leaf.transform,null); result[i]=gate;
                for(int row=0;row<5;row++) Prop("RouteArrow",i<2?cyan:orange,new Vector3(x,.005f,-sign*(2.8f+row)),Vector3.one,root,false).transform.rotation=Quaternion.Euler(0,sign==1?0:180,0);
            }
            foreach(float x in new[]{-10.1f,10.1f})
            { SolidBox("SideBarrier",new Vector3(x,0,0),new Vector3(6.8f,1.05f,.32f),metal,geo); obstacles.Add(new Rect(x-3.4f,-.16f,6.8f,.32f)); }
            for(int i=0;i<3;i++)
            { float x=-3+i*3; SolidBox("BetweenGateFence",new Vector3(x,0,0),new Vector3(1.35f,1.05f,.25f),metal,geo); obstacles.Add(new Rect(x-.675f,-.125f,1.35f,.25f)); }
            return result;
        }
        private static PassengerAiV2TrainDoorSmartObject Door(Transform geo,List<Rect> obstacles)
        {
            foreach(int side in new[]{-1,1})
            {
                SolidBox("DoorSideWall",new Vector3(side*7.6f,0,0),new Vector3(12.6f,2.2f,.15f),wall,geo);
                obstacles.Add(new Rect(side<0?-13.9f:1.3f,-.075f,12.6f,.15f));
                Prop("Bench",seat,new Vector3(side*11,0,-7.7f),Vector3.one,geo,true);
                for(int i=0;i<4;i++) Prop("TrainSeat",seat,new Vector3(side*(4.5f+i*.6f),0,7.7f),Vector3.one,geo,true).transform.rotation=Quaternion.Euler(0,180,0);
            }
            var left=Prop("TrainDoorPanel",metal,new Vector3(-.5f,0,0),Vector3.one,geo,true);
            var right=Prop("TrainDoorPanel",metal,new Vector3(.5f,0,0),Vector3.one,geo,true);
            GameObjectUtility.SetStaticEditorFlags(left,0); GameObjectUtility.SetStaticEditorFlags(right,0);
            var door=new GameObject("SmartObject_TrainDoor_TwoLane").AddComponent<PassengerAiV2TrainDoorSmartObject>(); door.transform.SetParent(geo,false);
            door.Configure("3d_train_door",Vector2.zero,null,null,2);
            door.gameObject.AddComponent<PassengerAiV23DFacilityVisual>().Configure(null,door,left.transform,right.transform);
            return door;
        }
        private static PassengerAiV2TrainInteriorEnvironment Interior(Transform root,Transform geo,List<Rect> obstacles)
        {
            var env=new GameObject("SmartObject_Interior_Utility").AddComponent<PassengerAiV2TrainInteriorEnvironment>(); env.transform.SetParent(root,false);
            for(int side=-1;side<=1;side+=2)
            {
                for(int row=0;row<9;row++)
                { var panel=Prop("TrainSideWall",wall,new Vector3(side*3.06f,0,-8+row*2),Vector3.one,geo,true); panel.transform.rotation=Quaternion.Euler(0,90,0); }
                for(int i=0;i<4;i++)
                {
                    float z=-5.4f+i*3.6f;
                    var chair=Prop("TrainSeat",seat,new Vector3(side*2.55f,0,z),Vector3.one,geo,true);
                    chair.transform.rotation=Quaternion.Euler(0,side<0?90:-90,0);
                    // Occupied seats are approached from the aisle; only the rear of the cushion is solid.
                    chair.GetComponent<MeshCollider>().enabled=false;
                    SolidBox("SeatBackCollider",new Vector3(side*2.83f,0,z),new Vector3(.12f,1,.56f),metal,geo);
                    obstacles.Add(new Rect(side<0?-2.89f:2.77f,z-.28f,.12f,.56f));
                    Spot("Seat",PassengerAiV2InteriorSpotKind.Seat,new Vector3(side*2.48f,.53f,z),side<0?90:-90,1,.4f,root);
                }
                foreach(float z in new[]{-7.8f,7.8f})
                {
                    var panel=Prop("LeanPanel",metal,new Vector3(side*2.94f,0,z),Vector3.one,geo,true);
                    panel.transform.rotation=Quaternion.Euler(0,side<0?90:-90,0);
                    obstacles.Add(new Rect(side<0?-3:2.89f,z-.55f,.11f,1.1f));
                    Spot("Lean",PassengerAiV2InteriorSpotKind.Lean,new Vector3(side*2.59f,0,z),side<0?90:-90,.7f,.1f,root);
                }
                for(int row=0;row<5;row++)
                    Spot("Stand",PassengerAiV2InteriorSpotKind.Stand,new Vector3(side*1.1f,0,-6.4f+row*3.2f),side<0?90:-90,.52f,.1f,root);
            }
            // Open roof for inspection, real floor and side/end boundaries at metre scale.
            foreach(float z in new[]{-9.08f,9.08f}) SolidBox("CarEnd",new Vector3(0,0,z),new Vector3(6.2f,1.1f,.16f),wall,geo);
            for(int i=0;i<18;i++) Spot("Prepare_"+i,PassengerAiV2InteriorSpotKind.DoorPrepare,new Vector3((i%3-1)*.8f,0,-7.5f+(i/3)*2.8f),0,.4f,0,root);
            return env;
        }
        private static void Spot(string name,PassengerAiV2InteriorSpotKind kind,Vector3 p,float yaw,float comfort,float cost,Transform parent)
        {
            var spot=new GameObject("SmartObject_"+name).AddComponent<PassengerAiV2InteriorSpotSmartObject>(); spot.transform.SetParent(parent,false);
            spot.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));
            spot.Configure(spot.name+"_"+p.x+"_"+p.z,kind,new Vector2(p.x,p.z),comfort,cost,null);
        }
        private static void Floor(Transform parent,Rect bounds,float height)
        {
            // Whole authored 2m panels; scale only edge strips to meet exact boundaries.
            for(float x=bounds.xMin;x<bounds.xMax-.01f;x+=2) for(float z=bounds.yMin;z<bounds.yMax-.01f;z+=2)
            {
                float w=Mathf.Min(2,bounds.xMax-x),d=Mathf.Min(2,bounds.yMax-z);
                Prop("FloorPanel",floor,new Vector3(x+w/2,height,z+d/2),new Vector3(w/2,1,d/2),parent,true);
            }
        }
        private static void OuterWalls(Transform geo,Rect bounds,bool stairs)
        {
            foreach(float z in new[]{bounds.yMin-.08f,bounds.yMax+.08f})
                SolidBox("Boundary",new Vector3(bounds.center.x,stairs&&z>0?4.16f:0,z),new Vector3(bounds.width,1.05f,.16f),wall,geo);
            if(stairs) return;
            foreach(float x in new[]{bounds.xMin-.08f,bounds.xMax+.08f})
                SolidBox("Boundary",new Vector3(x,0,bounds.center.y),new Vector3(.16f,1.05f,bounds.height),wall,geo);
        }
        private static void SolidBox(string name,Vector3 p,Vector3 size,Material mat,Transform parent)
        {
            // Scale the Blender-authored wall module; no Unity primitive mesh creation.
            var obj=Prop("WallPanel",mat,p,new Vector3(size.x/2,size.y/2.5f,size.z/.16f),parent,true); obj.name=name;
        }
        private static GameObject Prop(string mesh,Material mat,Vector3 p,Vector3 scale,Transform parent,bool collision)
        { return Piece(mesh,FindMesh(mesh),mat,p,scale,parent,collision); }
        private static void SetupView(int number)
        {
            var camera=new GameObject("Main Camera").AddComponent<Camera>(); camera.tag="MainCamera";
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.09f,.12f,.15f);
            camera.fieldOfView=54; camera.nearClipPlane=.1f; camera.farClipPlane=150;
            var controls=camera.gameObject.AddComponent<PassengerAiV23DTestCamera>();
            Vector3 centre=new Vector3(0,number==2?2.1f:.7f,0); float distance=number==5?23:34;
            controls.Frame(centre,distance); Quaternion view=Quaternion.Euler(43,-25,0);
            camera.transform.SetPositionAndRotation(centre-view*Vector3.forward*distance,view);
            camera.gameObject.AddComponent<AudioListener>();
            var scope=new GameObject("SceneOnly_3D_RenderScope").AddComponent<Metro3DRenderScope>();
            scope.pipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/_Project/Scenes/3D/Ingame/Game/Data/Pipeline3D.asset");
            AddLight("Key Light",Quaternion.Euler(48,-35,0),1.1f,new Color(1,.95f,.88f),true);
            AddLight("Fill Light",Quaternion.Euler(35,135,0),.35f,new Color(.77f,.87f,1),false);
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.3f,.33f,.36f); RenderSettings.fog=false;
        }
        internal static T Find<T>(Scene scene) where T:Component
        { foreach(var root in scene.GetRootGameObjects()) { var value=root.GetComponentInChildren<T>(); if(value!=null) return value; } return null; }
        public static void ValidateAll()
        {
            for(int i=0;i<Paths.Length;i++)
            {
                Scene previous=SceneManager.GetActiveScene(),scene=SceneManager.GetSceneByPath(Paths[i]); bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened) scene=EditorSceneManager.OpenScene(Paths[i],OpenSceneMode.Additive);
                try
                {
                    int count=0;
                    foreach(var root in scene.GetRootGameObjects()) foreach(var agent in root.GetComponentsInChildren<PassengerAiV2Agent>())
                    {
                        count++;
                        if(agent.GetComponentInChildren<PassengerAiV23DMotor>()==null) throw new InvalidOperationException("3D motor missing");
                        var animator=agent.GetComponentInChildren<Animator>();
                        if(animator==null||animator.avatar==null||!animator.avatar.isValid||animator.runtimeAnimatorController==null) throw new InvalidOperationException("Invalid mannequin rig");
                    }
                    if(count<(i==0?16:1)) throw new InvalidOperationException("Missing NPCs");
                    var nav=Find<PassengerAiV23DNavigation>(scene);
                    if(nav==null) throw new InvalidOperationException("Navigation missing");
                    if(i==0)
                    {
                        if(nav.ObstacleCount!=7) throw new InvalidOperationException("01 needs seven authored obstacles");
                        foreach(float lane in new[]{-2.35f,-.8f,.8f,2.35f})
                            if(!nav.CheckRoute(new Vector2(-16.65f,lane),new Vector2(16.95f,lane))) throw new InvalidOperationException("01 blocked route");
                        if(typeof(PassengerAiV2Corridor3DScenario).GetMethod("OnGUI",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!=null)
                            throw new InvalidOperationException("Unwanted 3D overlay remains");
                    }
                    if(i==1)
                    {
                        Mesh steps=FindMesh("StairFlight");
                        if(Mathf.Abs(steps.bounds.size.y-4.16f)>.05f||Mathf.Abs(steps.bounds.size.z-8.32f)>.05f) throw new InvalidOperationException("Stair mesh axes or dimensions incorrect: "+steps.bounds);
                        if(!nav.CheckRoute(new Vector2(-10,-6.6f),new Vector2(0,-4.8f))) throw new InvalidOperationException("Stair approach inaccessible");
                    }
                    if(i==2 && Find<PassengerAiV2FareGateSmartObject>(scene)==null) throw new InvalidOperationException("Gate reservations missing");
                    if(i==3 && Find<PassengerAiV2TrainDoorSmartObject>(scene)==null) throw new InvalidOperationException("Door priority missing");
                    if(i==4 && Find<PassengerAiV2TrainInteriorEnvironment>(scene)==null) throw new InvalidOperationException("Interior Utility environment missing");
                    Debug.Log("AI3D_STATIC_FIELD_PASS: "+(i+1)+" agents="+count);
                }
                finally { if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous); if(opened) EditorSceneManager.CloseScene(scene,true); }
            }
        }
    }
}
#endif

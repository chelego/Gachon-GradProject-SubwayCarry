#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SubwayCarry.Gameplay3D.Editor
{
    // Non-visual import/scale/font checks. Never opens, regenerates or saves a gameplay scene.
    [InitializeOnLoad]
    public static class Metro3DAssetChecks
    {
        static double nextPoll;
        static string Folder => Path.Combine(Application.dataPath,"../Temp/ThreeDRebuild");
        static Metro3DAssetChecks() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextPoll = EditorApplication.timeSinceStartup + 3;
            string request = Path.Combine(Folder,"asset-checks.request");
            string sceneRequest = Path.Combine(Folder,"integration-checks.request");
            if (File.Exists(sceneRequest)) {
                File.Delete(sceneRequest);
                try { File.WriteAllText(Path.Combine(Folder,"integration-checks.result"),ValidateSavedScene()); }
                catch(Exception ex) {File.WriteAllText(Path.Combine(Folder,"integration-checks.result"),"FAILED: "+ex); Debug.LogException(ex);}
            }
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { Validate(); File.WriteAllText(Path.Combine(Folder,"asset-checks.result"),"PASS: two metre-scale Blender imports; UV/mesh references; 3 sign fitting cases; font atlas; depth shader without compile errors. No scene saved or rebuilt."); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(Folder,"asset-checks.result"),"FAILED: "+ex); Debug.LogException(ex); }
        }

        static string ValidateSavedScene()
        {
            var scene=EditorSceneManager.GetSceneByPath(Metro3DBuilder.ScenePath);
            if(!scene.isLoaded||scene.isDirty) throw new InvalidOperationException("Saved owned 3D scene must be loaded and clean; no scene opened or saved by this check.");
            var objects=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).ToArray();
            var world=objects.Select(t=>t.GetComponent<Metro3DWorld>()).FirstOrDefault(w=>w!=null);
            if(world==null) throw new InvalidOperationException("Saved scene world is missing.");
            Metro3DBuilder.Validate(scene,world);
            int passengerPaths=CheckPassengerRoutes(world);
            int contacts=CheckDetailContacts(world);
            foreach(var t in objects) {
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0) throw new InvalidOperationException("Missing script on "+t.name);
                foreach(var filter in t.GetComponents<MeshFilter>()) if(filter.sharedMesh==null) throw new InvalidOperationException("Missing mesh on "+t.name);
                foreach(var renderer in t.GetComponents<Renderer>()) foreach(var mat in renderer.sharedMaterials)
                    if(mat==null||mat.shader==null||ShaderUtil.ShaderHasError(mat.shader)) throw new InvalidOperationException("Invalid material/shader on "+t.name);
                foreach(var collider in t.GetComponents<MeshCollider>()) if(collider.sharedMesh==null) throw new InvalidOperationException("Missing collision mesh on "+t.name);
            }
            foreach(var sign in objects.Select(t=>t.GetComponent<Metro3DWorldSign>()).Where(s=>s!=null)) {
                var renderer=sign.GetComponent<MeshRenderer>(); var text=sign.GetComponent<TextMesh>();
                var size=renderer.localBounds.size;
                if(text.font==null||size.x>sign.maximumWidth+.01f||size.y>sign.maximumHeight+.01f) throw new InvalidOperationException("Saved label exceeds face: "+text.text);
            }
            var kit=world.settings.environmentLibrary;
            int kitCount=0;
            foreach(var name in ("WallPanel ConcourseWall FloorPanel CeilingPanel ConcourseCeiling Pillar FluorescentLight VentPanel StationBench TrainBench FareGate FareLeaf GuardRail TactileBlock TactileGuideBlock EmergencySign DoorStatusLight SlidingDoor PlatformDoor LiftDoorLeaf TrainThreshold PlatformGlazing PlatformHeader DoorPocket WayfindingBoard SignSuspension StationNameBoard NoticeBoard FireCabinet TicketMachine WasteBin ServiceDoor ElevatorFront LiftCar LiftShaft TunnelSegment TrackModule SupportPole GrabRail TrainCarBody TrainGangway TrainCabPartition TrainCab CakeBox").Split(' ')) {
                var module=kit.transform.Find(name);
                if(module==null||module.GetComponentsInChildren<MeshFilter>(true).Length==0) throw new InvalidOperationException("Blender module missing from saved library: "+name);
                foreach(var filter in module.GetComponentsInChildren<MeshFilter>(true)) if(filter.sharedMesh==null||filter.sharedMesh.uv.Length!=filter.sharedMesh.vertexCount) throw new InvalidOperationException("Imported kit UV missing: "+name);
                foreach(var renderer in module.GetComponentsInChildren<MeshRenderer>(true)) foreach(var mat in renderer.sharedMaterials) {
                    if(mat==null||mat.shader.name!="Universal Render Pipeline/Lit") throw new InvalidOperationException("Kit material not remapped: "+name);
                    if(mat.GetFloat("_Surface")==0) foreach(var slot in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap","_OcclusionMap"})
                        if(mat.GetTexture(slot)==null) throw new InvalidOperationException("Kit PBR map missing: "+name+" "+slot);
                }
                kitCount++;
            }
            if(RenderSettings.customReflectionTexture==null) throw new InvalidOperationException("Indoor metal reflection missing.");
            int renderers=objects.Sum(t=>t.GetComponents<MeshRenderer>().Length);
            int colliders=objects.Sum(t=>t.GetComponents<Collider>().Count(c=>!c.isTrigger));
            int signs=objects.Count(t=>t.GetComponent<Metro3DWorldSign>()!=null);
            return "PASS: saved scene; 4 carriages / 16 numbered doors / 3 continuous gangways / "+Metro3DWorld.PlatformLength+"m platforms; "+passengerPaths+" passenger routes; "+contacts+" door/threshold/fixture contacts; 5 gait/posture clips; "+kitCount+" Blender kit modules; "+renderers+" mesh renderers; "+colliders+" solid colliders; "+signs+" bounded labels. No Play Mode or visual/performance approval.";
        }

        static int CheckDetailContacts(Metro3DWorld world)
        {
            int count=0;
            foreach(var group in world.GetComponentsInChildren<Transform>().Where(t=>t.name=="Sliding door pair"||t.name=="PSD sliding glass pair")) {
                var leaves=group.Cast<Transform>().Where(t=>t.name=="SlidingDoor"||t.name=="PlatformDoor").ToArray();
                if(leaves.Length!=2) throw new InvalidOperationException("Door pair missing a leaf.");
                CheckMeetingEdges(leaves[0],leaves[1]); count++;
            }
            foreach(var lift in world.GetComponentsInChildren<Metro3DLift>()) for(int i=0;i<lift.leaves.Length;i+=2) {
                CheckMeetingEdges(lift.leaves[i],lift.leaves[i+1]); count++;
            }
            foreach(var threshold in world.train.transform.Cast<Transform>().Where(t=>t.name=="Boarding threshold")) {
                var meshes=threshold.GetComponentsInChildren<MeshRenderer>(); Bounds b=meshes[0].bounds;
                foreach(var mesh in meshes.Skip(1)) b.Encapsulate(mesh.bounds);
                var min=world.train.transform.InverseTransformPoint(b.min); var max=world.train.transform.InverseTransformPoint(b.max);
                if(Mathf.Abs(b.size.x-.44f)>.015f||Mathf.Abs(b.size.z-1.3f)>.015f||max.y>.004f||max.y<-.002f)
                    throw new InvalidOperationException("Single flush boarding tread mismatch: "+b);
                float inner=Mathf.Min(Mathf.Abs(min.x),Mathf.Abs(max.x));
                if(inner>1.425f+.003f) throw new InvalidOperationException("Boarding tread detached from carriage floor."); count++;
            }
            foreach(var mount in world.stations[0].GetComponentsInChildren<Transform>().Where(t=>t.name=="SignSuspension")) {
                float top=mount.GetComponentsInChildren<MeshRenderer>().Max(r=>r.bounds.max.y);
                float floor=mount.localPosition.y>=world.settings.departureFloor?world.settings.departureFloor:0;
                if(Mathf.Abs(top-(floor+2.98f))>.01f) throw new InvalidOperationException("Wayfinding hanger not fixed to ceiling."); count++;
            }
            if(world.train.transform.Cast<Transform>().Count(t=>t.name=="Boarding threshold")!=32||world.train.transform.Cast<Transform>().Count(t=>t.name=="TrainCabPartition")!=2)
                throw new InvalidOperationException("32 single treads and two visible crew partitions required.");
            return count;
        }

        static void CheckMeetingEdges(Transform left,Transform right)
        {
            var a=left.GetComponentsInChildren<Transform>().SingleOrDefault(t=>t.name.EndsWith("_MeetingEdge"));
            var b=right.GetComponentsInChildren<Transform>().SingleOrDefault(t=>t.name.EndsWith("_MeetingEdge"));
            if(a==null||b==null||Vector3.Distance(a.position,b.position)>.012f)
                throw new InvalidOperationException("Door gaskets must meet at the centre, not the outer jamb: "+left.parent.name+"; edges "+(a==null?"missing":a.position.ToString("F4"))+" / "+(b==null?"missing":b.position.ToString("F4"))+"; placement yaw/scale "+left.localEulerAngles+" "+left.localScale+" / "+right.localEulerAngles+" "+right.localScale);
        }

        static int CheckPassengerRoutes(Metro3DWorld world)
        {
            var navigation=new Metro3DNavigation(); var output=new Vector3[384]; int count=0;
            foreach(var seat in world.facilities.Where(f=>f.trainFacility&&f.kind==Metro3DFacilityKind.Seat)) {
                int door=world.train.NearestDoor(seat.approach.position);
                if(navigation.Find(world.train.transform,true,world.train.DoorPoint(door,false),seat.approach.position,output)<=0)
                    throw new InvalidOperationException("No clear door-to-seat route: "+seat.approach.position);
                count++;
            }
            if(navigation.Find(world.train.transform,true,world.train.DoorPoint(0,false),world.train.DoorPoint(15,false),output)<=0)
                throw new InvalidOperationException("Four-car navigation is disconnected."); count++;
            for(int i=0;i<16;i++) {
                var station=world.stations[0];
                var destination=station.TransformPoint(new Vector3(-world.ServiceSide*(Metro3DWorld.ScreenDoorX+.55f),0,Metro3DTrain.DoorZ[i]));
                if(navigation.Find(station,false,station.TransformPoint(world.PlatformWaitingPoint(i)),destination,output)<=0)
                    throw new InvalidOperationException("Platform waiting-to-door route obstructed: "+Metro3DTrain.DoorNumber(i)); count++;
            }
            for(int i=0;i<16;i++) if(Metro3DTrain.DoorNumber(i)!=(i/4+1)+"-"+(i%4+1)) throw new InvalidOperationException("Door numbering mismatch.");
            var controller=world.settings.passengerPrefab.GetComponentInChildren<Animator>().runtimeAnimatorController;
            foreach(string name in new[]{"Idle","Walk","Sit","SitDown","StandUp"}) if(!controller.animationClips.Any(c=>c.name.EndsWith("|"+name)))
                throw new InvalidOperationException("Posture clip missing: "+name);
            GameObject human=null;
            try {
                human=UnityEngine.Object.Instantiate(world.settings.passengerPrefab); human.hideFlags=HideFlags.HideAndDontSave;
                human.GetComponent<CharacterController>().enabled=false;
                var animator=human.GetComponentInChildren<Animator>(); animator.enabled=false;
                foreach(var clip in controller.animationClips.Distinct()) {
                    clip.SampleAnimation(animator.gameObject,0);
                    var hips=human.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Hips");
                    if(hips==null) throw new InvalidOperationException("Human rig missing pelvis.");
                    if(clip.name.EndsWith("|Sit")&&Mathf.Abs(hips.position.y-.585f)>.06f) throw new InvalidOperationException("Seated pelvis does not match the .455m cushion: "+hips.position);
                }
            } finally {if(human!=null) UnityEngine.Object.DestroyImmediate(human);}
            return count;
        }

        [MenuItem("SubwayCarry/3D/Validate Blender Assets And Signs")]
        public static void Validate()
        {
            CheckModel("StationStaircase",2.224f,11.1f,4.635f);
            CheckModel("StationEscalator",1.54f,11.0932f,4.627f);
            CheckPrefab("StationStaircase"); CheckPrefab("StationEscalator");
            var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/Fonts/NanumGothic/NanumGothic.ttf");
            if (font == null) throw new InvalidOperationException("Existing licensed Korean font missing.");
            var shader=Resources.Load<Shader>("WorldSignText");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("World text shader missing or failed to compile.");
            CheckLabel(font,"④ EX-HUB 판교방향  |  ⑤ 송파방향 →",5.2f,.35f);
            CheckLabel(font,"K223  가천대",2.42f,.23f);
            CheckLabel(font,"① 가천대  ② 서초교  ③ 태평중\n④ 판교방향  ⑤ 송파방향",.87f,.29f);
        }

        static void CheckModel(string name,float width,float length,float maxHeight)
        {
            var path=Metro3DBuilder.Root+"/Art/Models/"+name+".fbx";
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(model==null) throw new InvalidOperationException("Blender FBX not imported: "+path);
            GameObject instance=null;
            try {
                instance=UnityEngine.Object.Instantiate(model); instance.hideFlags=HideFlags.HideAndDontSave;
                bool first=true; Bounds bounds=default;
                foreach(var renderer in instance.GetComponentsInChildren<MeshRenderer>()) {
                    var mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if(mesh==null||mesh.uv.Length!=mesh.vertexCount) throw new InvalidOperationException(name+" missing mesh or UVs.");
                    if(first) {bounds=renderer.bounds; first=false;} else bounds.Encapsulate(renderer.bounds);
                }
                if(first||Mathf.Abs(bounds.size.x-width)>.08f||Mathf.Abs(bounds.size.z-length)>.08f||Mathf.Abs(bounds.max.y-maxHeight)>.10f)
                    throw new InvalidOperationException(name+" metre-scale mismatch: "+bounds);
                foreach(var marker in new[]{"EntranceFloor","ExitFloor"}) {
                    bool found=false;
                    foreach(var child in instance.GetComponentsInChildren<Transform>()) if(child.name==name+"_"+marker) {found=true; break;}
                    if(!found) throw new InvalidOperationException(name+" missing floor anchor "+marker);
                }
            } finally { if(instance!=null) UnityEngine.Object.DestroyImmediate(instance); }
        }

        static void CheckLabel(Font font,string content,float width,float height)
        {
            var go=new GameObject("Hidden sign dimension check"); go.hideFlags=HideFlags.HideAndDontSave;
            try {
                var text=go.AddComponent<TextMesh>(); text.font=font; text.text=content; text.anchor=TextAnchor.MiddleCenter; text.characterSize=.155f;
                go.AddComponent<Metro3DWorldSign>().Configure(width,height);
                var renderer=go.GetComponent<MeshRenderer>();
                if(renderer.localBounds.size.x<.05f||renderer.localBounds.size.y<.02f||renderer.localBounds.size.x>width+.005f||renderer.localBounds.size.y>height+.005f)
                    throw new InvalidOperationException("Sign exceeds its face: "+content+" "+renderer.localBounds);
                if(renderer.sharedMaterial.shader.name!="SubwayCarry/3D/WorldSignText"||renderer.sharedMaterial.mainTexture!=font.material.mainTexture)
                    throw new InvalidOperationException("Sign uses overlay/stale font material.");
            } finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static void CheckPrefab(string name)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Metro3DBuilder.Root+"/Prefabs/"+name+".prefab");
            if(prefab==null) throw new InvalidOperationException("PBR prefab not prepared: "+name);
            foreach(var renderer in prefab.GetComponentsInChildren<MeshRenderer>()) foreach(var material in renderer.sharedMaterials) {
                if(material==null||material.shader.name!="Universal Render Pipeline/Lit") throw new InvalidOperationException(name+" missing URP material.");
                if(material.name=="Circulation_SafetyGlass") {
                    if(material.GetFloat("_Surface")!=1||material.renderQueue!=(int)UnityEngine.Rendering.RenderQueue.Transparent)
                        throw new InvalidOperationException("Glass is opaque.");
                } else foreach(var slot in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap","_OcclusionMap"}) {
                    if(material.GetTexture(slot)==null) throw new InvalidOperationException(name+" missing PBR texture "+slot);
                }
            }
        }
    }
}
#endif

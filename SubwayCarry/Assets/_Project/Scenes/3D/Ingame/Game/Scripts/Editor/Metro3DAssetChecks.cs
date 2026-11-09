#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SubwayCarry.Gameplay3D.Editor
{
    // Non-visual import/scale/font checks. Never opens, regenerates or saves a gameplay scene.
    [InitializeOnLoad]
    public static class Metro3DAssetChecks
    {
        static double nextPoll;
        static bool capturing;
        static string Folder => Path.Combine(Application.dataPath,"../Temp/ThreeDRebuild");
        [Serializable] sealed class ReviewRestore { public string pipelineGuid; public int ambientMode; public bool asynchronousShaders; }
        static string RestorePath=>Path.Combine(Folder,"presentation-review.restore");
        static Metro3DAssetChecks()
        {
            EditorApplication.update -= Poll; EditorApplication.update += Poll;
            AssemblyReloadEvents.beforeAssemblyReload+=RestoreReview;
            EditorApplication.update+=RecoverReview;
        }
        static void RecoverReview() {EditorApplication.update-=RecoverReview; RestoreReview();}
        static void RestoreReview()
        {
            if(!File.Exists(RestorePath)) return;
            var state=JsonUtility.FromJson<ReviewRestore>(File.ReadAllText(RestorePath));
            QualitySettings.renderPipeline=string.IsNullOrEmpty(state.pipelineGuid)?null:AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(state.pipelineGuid));
            RenderSettings.ambientMode=(AmbientMode)state.ambientMode;
            ShaderUtil.allowAsyncCompilation=state.asynchronousShaders;
            foreach(var camera in Resources.FindObjectsOfTypeAll<Camera>().Where(c=>c.name=="Temporary station material review camera"&&c.gameObject.hideFlags==HideFlags.HideAndDontSave)) UnityEngine.Object.DestroyImmediate(camera.gameObject);
            File.Delete(RestorePath);
        }
        static void Poll()
        {
            if(Application.isBatchMode) return;
            if (capturing || EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextPoll = EditorApplication.timeSinceStartup + 3;
            string request = Path.Combine(Folder,"asset-checks.request");
            string sceneRequest = Path.Combine(Folder,"integration-checks.request");
            string presentationRequest=Path.Combine(Folder,"presentation-review.request");
            string reflectionRequest=Path.Combine(Folder,"station-reflections.request");
            if(File.Exists(reflectionRequest)) {
                string hash=File.ReadAllText(reflectionRequest).Trim(); File.Delete(reflectionRequest);
                BakeReflectionRequest(hash); return;
            }
            if(File.Exists(presentationRequest)) {
                File.Delete(presentationRequest);
                CapturePresentationRequest(); return;
            }
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

        static async void CapturePresentationRequest()
        {
            capturing=true;
            try { File.WriteAllText(Path.Combine(Folder,"presentation-review.result"),await CapturePresentation()); }
            catch(Exception ex) { File.WriteAllText(Path.Combine(Folder,"presentation-review.result"),"FAILED: "+ex); Debug.LogException(ex); }
            finally {capturing=false;}
        }

        static Task NextEditorFrame()
        {
            var next=new TaskCompletionSource<bool>();
            EditorApplication.CallbackFunction advance=null;
            advance=()=>{EditorApplication.update-=advance; next.TrySetResult(true);};
            EditorApplication.update+=advance;
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
            return next.Task;
        }

        static string SceneHash()
        {
            using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Metro3DBuilder.ScenePath))).Replace("-","");
        }

        static async void BakeReflectionRequest(string expectedHash)
        {
            capturing=true;
            var previous=QualitySettings.renderPipeline;
            var previousMode=RenderSettings.ambientMode; var previousAmbient=RenderSettings.ambientProbe;
            bool asynchronousShaders=ShaderUtil.allowAsyncCompilation;
            GameObject temporary=null; RenderTexture target=null; Texture2D readback=null;
            ReflectionProbe[] oldProbes=Array.Empty<ReflectionProbe>(); bool[] enabled=Array.Empty<bool>();
            try {
                var scene=EditorSceneManager.GetSceneByPath(Metro3DBuilder.ScenePath);
                if(!scene.isLoaded||scene.isDirty||SceneHash()!=expectedHash) throw new InvalidOperationException("Reflection capture refused: saved owned scene changed.");
                File.Copy(Metro3DBuilder.ScenePath,Path.Combine(Folder,"before-room-reflections.unity"),true);
                var world=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Metro3DWorld>(true)).Single();
                File.WriteAllText(RestorePath,JsonUtility.ToJson(new ReviewRestore{pipelineGuid=previous==null?null:AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(previous)),ambientMode=(int)previousMode,asynchronousShaders=asynchronousShaders}));
                QualitySettings.renderPipeline=world.settings.renderPipeline; ShaderUtil.allowAsyncCompilation=false;
                Metro3DWorldSign.PrepareExisting(world.transform);
                oldProbes=world.GetComponentsInChildren<ReflectionProbe>(true); enabled=oldProbes.Select(p=>p.enabled).ToArray();
                foreach(var probe in oldProbes) probe.enabled=false;
                temporary=new GameObject("Temporary station material review camera"){hideFlags=HideFlags.HideAndDontSave};
                var camera=temporary.AddComponent<Camera>(); camera.enabled=false; camera.fieldOfView=90; camera.aspect=1;
                camera.allowHDR=true; camera.nearClipPlane=.08f; camera.farClipPlane=160;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
                var cameraData=camera.GetUniversalAdditionalCameraData(); cameraData.SetRenderer(0); cameraData.renderPostProcessing=false;
                target=new RenderTexture(128,128,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear); target.Create();
                readback=new Texture2D(128,128,TextureFormat.RGBAFloat,false,true);
                Vector3[] positions={new Vector3(0,world.settings.departureFloor+1.5f,-7),new Vector3(-6.25f,1.5f,42),new Vector3(6.25f,1.5f,42)};
                Vector3[] directions={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
                Vector3[] ups={Vector3.up,Vector3.up,Vector3.back,Vector3.forward,Vector3.up,Vector3.up};
                string[] zones={"Concourse","WestPlatform","EastPlatform"}; var maps=new Cubemap[3];
                await NextEditorFrame();
                for(int zone=0;zone<3;zone++) {
                    var cube=new Cubemap(128,TextureFormat.RGBAFloat,true){name="StationReflection_"+zones[zone],wrapMode=TextureWrapMode.Clamp};
                    float total=0;
                    for(int face=0;face<6;face++) {
                        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Reflection capture interrupted by Play Mode.");
                        camera.transform.position=positions[zone]; camera.transform.rotation=Quaternion.LookRotation(directions[face],ups[face]);
                        for(int warmup=0;warmup<3;warmup++) RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                        await NextEditorFrame(); Metro3DRenderScope.ApplyIndoorAmbient();
                        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                        var active=RenderTexture.active;
                        try {RenderTexture.active=target; readback.ReadPixels(new Rect(0,0,128,128),0,0); readback.Apply();}
                        finally {RenderTexture.active=active;}
                        var pixels=readback.GetPixels(); cube.SetPixelData(pixels,0,(CubemapFace)face);
                        if(cube.GetPixels((CubemapFace)face).Sum(c=>c.r+c.g+c.b)<.001f) throw new InvalidOperationException("Stored cubemap face is empty: "+zones[zone]+" "+face);
                        total+=pixels.Sum(c=>c.r+c.g+c.b);
                    }
                    if(total<100||float.IsNaN(total)) {UnityEngine.Object.DestroyImmediate(cube); throw new InvalidOperationException("Empty/invalid room reflection: "+zones[zone]);}
                    cube.Apply(true,false);
                    string path=Metro3DBuilder.Root+"/Art/Textures/Metro/StationReflection_"+zones[zone]+".asset";
                    var existing=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
                    if(existing==null) {AssetDatabase.CreateAsset(cube,path); maps[zone]=cube;}
                    else {EditorUtility.CopySerialized(cube,existing); UnityEngine.Object.DestroyImmediate(cube); maps[zone]=existing; EditorUtility.SetDirty(existing); AssetDatabase.SaveAssetIfDirty(existing);}
                }
                if(scene.isDirty||SceneHash()!=expectedHash) throw new InvalidOperationException("Scene edited during reflection capture; result maps preserved but scene not saved.");
                world.settings.concourseReflection=maps[0]; world.settings.westPlatformReflection=maps[1]; world.settings.eastPlatformReflection=maps[2];
                EditorUtility.SetDirty(world.settings); AssetDatabase.SaveAssetIfDirty(world.settings);
                foreach(var probe in oldProbes) if(probe.name.StartsWith("Baked reflection - ",StringComparison.Ordinal)||probe.name.StartsWith("Station reflection - ",StringComparison.Ordinal)) UnityEngine.Object.DestroyImmediate(probe.gameObject);
                foreach(var station in world.stations) world.ConfigureStationReflections(station);
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save owned scene reflections.");
                foreach(var zone in zones) AssetDatabase.DeleteAsset(Metro3DBuilder.Root+"/Art/Textures/Metro/StationReflection_0_"+zone+".exr");
                File.WriteAllText(Path.Combine(Folder,"station-reflections.result"),"PASS: three non-empty linear HDR room cubemaps, 18 camera faces, persistent box-projected station probes; no lightmap GI or Play/performance claim.");
            } catch(Exception ex) {File.WriteAllText(Path.Combine(Folder,"station-reflections.result"),"FAILED: "+ex); Debug.LogException(ex);}
            finally {
                for(int i=0;i<oldProbes.Length;i++) if(oldProbes[i]!=null) oldProbes[i].enabled=enabled[i];
                if(temporary!=null) UnityEngine.Object.DestroyImmediate(temporary);
                if(target!=null) {target.Release(); UnityEngine.Object.DestroyImmediate(target);}
                if(readback!=null) UnityEngine.Object.DestroyImmediate(readback);
                QualitySettings.renderPipeline=previous; RenderSettings.ambientMode=previousMode; RenderSettings.ambientProbe=previousAmbient;
                ShaderUtil.allowAsyncCompilation=asynchronousShaders; if(File.Exists(RestorePath)) File.Delete(RestorePath);
                capturing=false;
            }
        }

        static async Task<string> CapturePresentation()
        {
            var scene=EditorSceneManager.GetSceneByPath(Metro3DBuilder.ScenePath);
            if(!scene.isLoaded||scene.isDirty) throw new InvalidOperationException("Presentation review needs the saved clean 3D scene; no edited scene is saved or discarded.");
            var world=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Metro3DWorld>(true)).Single();
            var previous=QualitySettings.renderPipeline;
            var previousAmbient=RenderSettings.ambientProbe;
            var previousAmbientMode=RenderSettings.ambientMode;
            bool asynchronousShaders=ShaderUtil.allowAsyncCompilation;
            File.WriteAllText(RestorePath,JsonUtility.ToJson(new ReviewRestore{pipelineGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(previous)),ambientMode=(int)previousAmbientMode,asynchronousShaders=asynchronousShaders}));
            GameObject temporary=null; RenderTexture target=null; Texture2D readback=null;
            try {
                QualitySettings.renderPipeline=world.settings.renderPipeline;
                ShaderUtil.allowAsyncCompilation=false;
                Metro3DRenderScope.ApplyIndoorAmbient();
                Metro3DWorldSign.PrepareExisting(world.transform);
                await NextEditorFrame();
                temporary=new GameObject("Temporary station material review camera"); temporary.hideFlags=HideFlags.HideAndDontSave;
                var camera=temporary.AddComponent<Camera>(); camera.enabled=false; camera.nearClipPlane=.05f; camera.farClipPlane=150; camera.fieldOfView=70;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.04f,.045f,.05f);
                var data=temporary.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing=true;
                target=new RenderTexture(1280,800,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB); target.Create();
                float floor=world.settings.departureFloor;
                var trainMarkers=world.train.GetComponentsInChildren<Transform>();
                var carStart=trainMarkers.First(t=>t.name=="TrainCarBody_FloorMin").position;
                var carEnd=trainMarkers.First(t=>t.name=="TrainCarBody_FloorMax").position;
                var carriageForward=(carEnd-carStart).normalized;
                string[] names={"Concourse","Column","Platform","Stair","Floor","TrainInterior"};
                Vector3[] origins={new Vector3(0,floor+1.62f,-13),new Vector3(-2.8f,floor+1.62f,-14),new Vector3(-6.4f,1.62f,17),new Vector3(-7.76f,1.62f,12.2f),new Vector3(-6.4f,1.62f,20),carStart+carriageForward*3+Vector3.up*1.62f};
                Vector3[] points={new Vector3(0,floor+1.35f,-4),new Vector3(-5,floor+1.3f,-11),new Vector3(-6.4f,1.5f,42),new Vector3(-7.76f,2.4f,4),new Vector3(-5.8f,.05f,22),carStart+carriageForward*17+Vector3.up*1.55f};
                string output=Path.Combine(Folder,"Presentation"); Directory.CreateDirectory(output);
                for(int i=0;i<names.Length;i++) {
                    camera.transform.position=origins[i]; camera.transform.rotation=Quaternion.LookRotation(points[i]-origins[i]);
                    camera.targetTexture=target;
                    for(int warmup=0;warmup<3;warmup++) RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                    await NextEditorFrame();
                    if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Material review cancelled because Play Mode was requested.");
                    Metro3DRenderScope.ApplyIndoorAmbient();
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                    var active=RenderTexture.active;
                    try {
                        RenderTexture.active=target; readback=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                        readback.ReadPixels(new Rect(0,0,target.width,target.height),0,0); readback.Apply();
                        File.WriteAllBytes(Path.Combine(output,names[i]+".png"),readback.EncodeToPNG());
                    } finally {RenderTexture.active=active; if(readback!=null) UnityEngine.Object.DestroyImmediate(readback); readback=null;}
                }
                var fixtures=world.GetComponentsInChildren<Light>().Where(l=>l.name=="Fluorescent illumination").ToArray();
                var tubes=world.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial!=null&&r.sharedMaterial.name=="Metro_LightDiffuser").ToArray();
                var label=world.GetComponentsInChildren<TextMesh>().First(t=>t.text=="↓ 타는 곳  |  수인분당선");
                var board=world.GetComponentsInChildren<Transform>().Where(t=>t.name=="WayfindingBoard").OrderBy(t=>(t.position-label.transform.position).sqrMagnitude).First();
                var faces=board.GetComponentsInChildren<MeshRenderer>(); var bound=faces[0].bounds;
                foreach(var face in faces.Skip(1)) bound.Encapsulate(face.bounds);
                return "PASS: six saved-scene Unity/URP material views rendered; "+fixtures.Length+" enabled fixtures; "+tubes.Length+" emission meshes; ambient "+RenderSettings.ambientSkyColor+" SH "+RenderSettings.ambientProbe[0,0]+"; color space "+QualitySettings.activeColorSpace+"; first fixture "+fixtures[0].transform.position+" forward "+fixtures[0].transform.forward+" intensity "+fixtures[0].intensity+"; sign at "+label.transform.position.ToString("F4")+" board min "+bound.min.ToString("F4")+" max "+bound.max.ToString("F4")+" label material "+label.GetComponent<MeshRenderer>().sharedMaterial.name+"; car floor "+carStart.ToString("F3")+" to "+carEnd.ToString("F3")+"; no Play Mode, no user camera edit, no performance claim.";
            } finally {
                if(temporary!=null) UnityEngine.Object.DestroyImmediate(temporary);
                if(target!=null) {target.Release(); UnityEngine.Object.DestroyImmediate(target);}
                QualitySettings.renderPipeline=previous;
                RenderSettings.ambientMode=previousAmbientMode;
                RenderSettings.ambientProbe=previousAmbient;
                ShaderUtil.allowAsyncCompilation=asynchronousShaders;
                if(File.Exists(RestorePath)) File.Delete(RestorePath);
            }
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
                var sdf=sign.GetComponentInChildren<TMPro.TextMeshPro>();
                if(sdf==null||renderer.enabled||sdf.mesh.vertexCount==0) throw new InvalidOperationException("Station SDF lettering missing: "+text.text);
                // A correctly fitted label is still wrong if an opposite-facing board
                // hides it. Test the front approach against all nearby sign housings.
                var normal=-sign.transform.forward;
                var ray=new Ray(sign.transform.position+normal*.4f,-normal);
                foreach(var housing in objects.Where(o=>o.name=="WayfindingBoard"))
                    foreach(var face in housing.GetComponentsInChildren<MeshRenderer>())
                        if(face.bounds.IntersectRay(ray,out float distance)&&(face.bounds.Contains(sign.transform.position)||face.bounds.Contains(ray.origin)||(distance>=0&&distance<.397f)))
                            throw new InvalidOperationException("Sign housing occludes lettering: "+text.text+" at "+sign.transform.position+" housing "+housing.position+" face "+face.bounds+" hit distance "+distance);
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
            foreach(string name in new[]{"Idle_Loop","Walk_Loop","Sitting_Idle_Loop","Sitting_Enter","Sitting_Exit"}) if(!controller.animationClips.Any(c=>c.name==name||c.name.EndsWith("|"+name)))
                throw new InvalidOperationException("Posture clip missing: "+name);
            GameObject human=null;
            try {
                human=UnityEngine.Object.Instantiate(world.settings.passengerPrefab); human.hideFlags=HideFlags.HideAndDontSave;
                human.GetComponent<CharacterController>().enabled=false;
                var animator=human.GetComponentInChildren<Animator>(); animator.enabled=false;
                foreach(var clip in controller.animationClips.Distinct()) {
                    clip.SampleAnimation(animator.gameObject,0);
                    var hips=human.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="DEF-hips");
                    if(hips==null) throw new InvalidOperationException("Human rig missing pelvis.");
                    // The 1.78m mannequin's pelvis is .072m above the .455m cushion.
                    if(clip.name.EndsWith("Sitting_Idle_Loop")&&Mathf.Abs(hips.position.y-.527f)>.03f) throw new InvalidOperationException("Seated mannequin pelvis does not match the .455m cushion: "+hips.position);
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
                var sdf=go.GetComponentInChildren<TMPro.TextMeshPro>();
                var bounds=sdf.textBounds;
                if(bounds.size.x<.05f||bounds.size.y<.02f||bounds.size.x>width+.005f||bounds.size.y>height+.005f)
                    throw new InvalidOperationException("Sign exceeds its face: "+content+" "+bounds);
                if(renderer.enabled||sdf.fontSharedMaterial.shader.name!="SubwayCarry/3D/WorldSignText"||sdf.textInfo.characterCount!=content.Length)
                    throw new InvalidOperationException("Sign uses overlay/stale font material or missing SDF glyphs.");
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

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SubwayCarry.Gameplay3D.Editor
{
    [InitializeOnLoad]
    public static class Metro3DPassengerChecks
    {
        const string Key="SubwayCarry.PassengerRegression";
        static double poll,nextSnapshot;
        static Metro3DWorld world;
        static float start;
        static string progress;
        static Metro3DPassenger watched;
        static int initialArrivals;
        static bool captured;
        static int captureCount;
        static bool contactReview,contactCaptured;
        static bool oldProfiler;
        static string runtimeError;
        static readonly StringBuilder trace=new StringBuilder();
        static string Folder=>Path.Combine(Application.dataPath,"../Temp/ThreeDRebuild");
        static Metro3DPassengerChecks() {
            EditorApplication.update+=Poll;
            Application.logMessageReceived+=(message,stack,type)=> {
                if(SessionState.GetBool(Key,false)&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&stack.Contains("SubwayCarry.Gameplay3D")) runtimeError=message;
            };
        }
        static void Poll()
        {
            if(SessionState.GetBool(Key,false)) {
                string stop=Path.Combine(Folder,"passenger-stop.request");
                if(File.Exists(stop)) {File.Delete(stop);End("STOPPED for correction\n"+Snapshot());return;}
                if(EditorApplication.isPlaying&&!EditorApplication.isCompiling) Check();
                else if(!EditorApplication.isPlayingOrWillChangePlaymode) SessionState.SetBool(Key,false);
                return;
            }
            if(EditorApplication.timeSinceStartup<poll||EditorApplication.isCompiling||EditorApplication.isUpdating) return;
            poll=EditorApplication.timeSinceStartup+2;
            string path=Path.Combine(Folder,"passenger-checks.request");if(!File.Exists(path)) return;
            string command=File.ReadAllText(path).Trim();File.Delete(path);
            try {
                if(command=="PLAY"||command=="REVIEW") {
                    var scene=SceneManager.GetActiveScene();
                    if(EditorApplication.isPlayingOrWillChangePlaymode||scene.isDirty||scene.path!=Metro3DBuilder.ScenePath) throw new InvalidOperationException("Passenger check needs the saved 3D scene outside Play.");
                    world=null;watched=null;captured=contactReview=contactCaptured=false;captureCount=0;runtimeError=null;trace.Clear();SessionState.SetBool(Key+".ReviewOnly",command=="REVIEW");oldProfiler=UnityEngine.Profiling.Profiler.enabled;SessionState.SetBool(Key+".Profiler",oldProfiler);UnityEngine.Profiling.Profiler.enabled=false;SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
                } else if(command=="AUDIT") AuditClip();
                else if(command=="STOP") End("STOPPED\n"+Snapshot());
                else File.WriteAllText(Path.Combine(Folder,"passenger-checks.result"),Snapshot());
            } catch(Exception ex) {File.WriteAllText(Path.Combine(Folder,"passenger-checks.result"),"FAILED: "+ex);}
        }
        static void Check()
        {
            try {
                if(runtimeError!=null) throw new InvalidOperationException("Gameplay runtime error: "+runtimeError);
                if(world==null) {
                    world=UnityEngine.Object.FindFirstObjectByType<Metro3DWorld>();if(world==null||world.session.Economy==null) return;
                    start=Time.time;progress=PlayerPrefs.GetString("SubwayCarry.3D.Progress.v1");initialArrivals=world.Traffic.EntranceArrivals;
                    if(world.passengers.Count(p=>p.inTrain)!=72||world.passengers.Count(p=>!p.inTrain)!=48) throw new InvalidOperationException("Expected 72 onboard / 48 station passengers.");
                    foreach(var p in world.passengers) {
                        if(p.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.quality!=SkinQuality.Bone4)) throw new InvalidOperationException("Passenger lost its four-bone skinning override.");
                        if(p.Journey.DestinationStop<=p.Journey.OriginStop||p.Journey.DestinationStop>=world.session.Route.stops.Length) throw new InvalidOperationException("Invalid fixed destination.");
                        if(p.inTrain&&!world.train.Contains(p.transform.position)) throw new InvalidOperationException("Seeded passenger outside carriage.");
                    }
                    trace.AppendLine("PASS initial 3x population: 48 station, 72 train, valid persistent forward destinations, four-car bounds.");
                    // Phone stays open and the player stays in the concourse for this whole check.
                    if(!world.session.PhoneOpen) throw new InvalidOperationException("Expected the startup phone overlay.");
                }
                if(watched==null) watched=world.passengers.FirstOrDefault(p=>p.Journey.Blackboard.CurrentStage==SubwayCarry.AI.V2.PassengerAiV2FullJourneyStage.EnterOriginStation);
                if(!captured&&Time.time-start>24+captureCount*.28f) {
                    CapturePassengers(captureCount++);captured=captureCount>=4;
                }
                if(!contactReview&&Time.time-start>26) {
                    contactReview=true;
                    // Review culling only: no position, activity, seat claim or
                    // destination is fabricated for the contact check.
                    foreach(var p in world.passengers.Where(p=>p.inTrain)) {
                        p.GetComponentInChildren<Animator>().cullingMode=AnimatorCullingMode.AlwaysAnimate;
                        p.GetComponent<Metro3DPassengerContacts>().ReviewWhileCulled=true;
                    }
                }
                if(!contactCaptured&&Time.time-start>30) {
                    contactCaptured=true;CaptureContacts();
                    if(SessionState.GetBool(Key+".ReviewOnly",false)) {End("PASS contact/runtime review; journey regression not covered.\n"+trace+Snapshot());return;}
                }
                if(EditorApplication.timeSinceStartup>=nextSnapshot) {
                    nextSnapshot=EditorApplication.timeSinceStartup+5;File.WriteAllText(Path.Combine(Folder,"passenger-checks.progress"),Snapshot());
                    trace.AppendLine("t="+(Time.time-start).ToString("F1")+" arrival="+world.Traffic.EntranceArrivals+" completed="+world.Traffic.CompletedCount+" trainStop="+world.train.CurrentStop+" onboard="+world.passengers.Count(p=>p.inTrain));
                }
                if(Time.time-start>70&&world.stations.Count<2) throw new InvalidOperationException("Service stuck at first stop; abort early for collision diagnosis.");
                if(Time.time-start<200) return;
                if(world.Traffic.EntranceArrivals<=initialArrivals) throw new InvalidOperationException("No continuous entrance arrivals while player remained at hub.");
                if(world.stations.Count<3) throw new InvalidOperationException("Ambient train did not continue beyond the first destination stop.");
                if(!world.session.PhoneOpen||(world.player.transform.position-world.HubSpawn).magnitude>1) throw new InvalidOperationException("Test changed the player/phone state.");
                if(world.Traffic.CompletedCount==0) throw new InvalidOperationException("No full destination-to-exit journey completed.");
                if(PlayerPrefs.GetString("SubwayCarry.3D.Progress.v1")!=progress) throw new InvalidOperationException("Saved player progress changed.");
                End("PASS continuous hub arrivals, autonomous service, completed passenger exits; phone/player/save unchanged.\n"+trace+Snapshot());
            } catch(Exception ex) {End("FAILED: "+ex+"\n"+trace+Snapshot());}
        }
        static string Snapshot()
        {
            var w=world!=null?world:UnityEngine.Object.FindFirstObjectByType<Metro3DWorld>();if(w==null) return "No runtime world.";
            var s=new StringBuilder();s.AppendLine("playing="+EditorApplication.isPlaying+" time="+Time.time+" phone="+w.session.PhoneOpen+" stop="+w.train.CurrentStop+" moving="+w.train.Moving+" doors="+w.train.DoorsOpen+" timer="+w.session.Timer);
            if(w.Traffic==null) return s.ToString();
            s.AppendLine("spawned="+w.Traffic.SpawnedCount+" arrivals="+w.Traffic.EntranceArrivals+" completed="+w.Traffic.CompletedCount+" active="+w.passengers.Count+" onboard="+w.passengers.Count(p=>p.inTrain)+" alighters="+w.passengers.Count(p=>p.WantsToAlight)+" leased="+w.Traffic.HasDoorCrossing(w.train));
            var contacts=w.passengers.Select(p=>p.GetComponent<Metro3DPassengerContacts>()).Where(c=>c!=null).ToArray();
            s.AppendLine("holding="+contacts.Count(c=>c.Holding)+" leaning="+contacts.Count(c=>c.Leaning)+" worstGrip="+contacts.Where(c=>c.Holding).Select(c=>c.GripError).DefaultIfEmpty(0).Max());
            foreach(var p in w.passengers.Where(p=>p.WantsToAlight&&!p.DoorCrossing).Take(16)) {
                s.AppendLine("ALIGHT WAIT "+p.index+" p="+p.transform.localPosition+" target="+typeof(Metro3DPassenger).GetField("targetLocal",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(p)+" v="+p.Velocity);
                var buffer=(Vector3[])typeof(Metro3DPassenger).GetField("path",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(p);
                int at=(int)typeof(Metro3DPassenger).GetField("pathIndex",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(p);
                s.AppendLine("  next path="+buffer[at]+" cost="+p.SteeringCost+" staticRejects="+p.StaticRejects+" motor="+p.GetComponent<CharacterController>().enabled);
                foreach(var other in w.passengers.Where(a=>a!=p&&(a.transform.position-p.transform.position).sqrMagnitude<1.44f).Take(8)) s.AppendLine("  neighbour="+other.index+" p="+other.transform.localPosition+" v="+other.Velocity+" seated="+!other.GetComponent<CharacterController>().enabled);
                foreach(string field in new[]{"state","pendingPath","pathCount","pathIndex","waypoint","blockedFor"}) s.AppendLine("  "+field+"="+typeof(Metro3DPassenger).GetField(field,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(p));
            }
            foreach(var group in w.passengers.GroupBy(p=>p.Journey.Blackboard.CurrentStage)) s.AppendLine(group.Key+"="+group.Count());
            foreach(var p in w.passengers.Where(p=>p.DoorCrossing)) {
                var goal=(Vector3)typeof(Metro3DPassenger).GetField("target",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(p);
                s.AppendLine("CROSSING "+p.index+" p="+p.transform.position+" goal="+goal+" inTrain="+p.inTrain);
                foreach(string field in new[]{"state","pendingPath","pathCount","pathIndex","velocity","steering","targetLocal"}) s.AppendLine("  "+field+"="+typeof(Metro3DPassenger).GetField(field,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(p));
                s.AppendLine("  motor="+p.GetComponent<CharacterController>().enabled+" radius="+p.GetComponent<CharacterController>().radius+" grounded="+p.GetComponent<CharacterController>().isGrounded);
                Vector3 to=goal-p.transform.position;to.y=0;
                foreach(var hit in Physics.SphereCastAll(p.transform.position+Vector3.up*.72f,.22f,to.normalized,to.magnitude,~0,QueryTriggerInteraction.Ignore).Where(h=>!(h.collider is CharacterController)).Take(5)) s.AppendLine("  crossing obstruction="+hit.collider.name+" at="+hit.point);
                foreach(var hit in Physics.OverlapSphere(p.transform.position+Vector3.up*.8f,.7f,~0,QueryTriggerInteraction.Ignore).Where(c=>c is CharacterController&&!c.transform.IsChildOf(p.transform)).Take(6)) {
                    var other=hit.GetComponent<Metro3DPassenger>();
                    s.AppendLine("  nearby capsule="+(other!=null?other.index.ToString():hit.name)+" p="+hit.transform.position+" v="+(other!=null?other.Velocity.ToString():"player"));
                    if(other!=null) s.AppendLine("    inTrain="+other.inTrain+" stage="+other.Journey.Blackboard.CurrentStage+" state="+typeof(Metro3DPassenger).GetField("state",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(other));
                }
            }
            foreach(var p in w.passengers.Where(p=>!p.inTrain).Take(12)) s.AppendLine(p.name+" id="+p.index+" "+p.Journey.OriginId+"->"+p.Journey.DestinationId+" stage="+p.Journey.Blackboard.CurrentStage+" p="+p.transform.localPosition+" v="+p.Velocity+" recoveries="+p.Journey.Blackboard.RecoveryCount+" last="+p.Journey.Blackboard.LastRecoveryReason);
            return s.ToString();
        }
        static void CapturePassengers(int sample)
        {
            var cameraObject=new GameObject("Temporary passenger review camera"){hideFlags=HideFlags.HideAndDontSave};
            RenderTexture target=null;Texture2D pixels=null;
            try {
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=55;camera.nearClipPlane=.08f;camera.farClipPlane=100;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.045f,.05f);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
                target=new RenderTexture(1100,800,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();
                pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                var examples=new[]{world.passengers.First(p=>!p.inTrain&&p.transform.localPosition.y>3),world.passengers.First(p=>p.inTrain&&p.transform.localPosition.x<0&&!p.GetComponent<CharacterController>().enabled)};
                for(int i=0;i<(sample==0?examples.Length:1);i++) {
                    var actor=examples[i].transform;Vector3 point=actor.position;
                    Vector3 offset=i==0?actor.forward*2.6f+actor.right*.7f+Vector3.up*1.4f:actor.forward*1.45f+actor.right*.35f+Vector3.up*1.2f;
                    if(i==0&&sample==2) offset=actor.right*2.6f+actor.forward*.15f+Vector3.up*1.25f;
                    camera.transform.position=point+offset;camera.transform.LookAt(point+Vector3.up*(i==0?.92f:.76f));
                    if(i==1) {
                        Vector3 local=actor.localPosition;
                        camera.transform.position=actor.parent.TransformPoint(new Vector3(0,1.25f,local.z-.45f));
                        camera.transform.LookAt(point+Vector3.up*.76f);
                    }
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                    var active=RenderTexture.active;
                    try {RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Folder,i==0?(sample==0?"PassengerPlatform.png":"PassengerPlatform_Walk"+sample+".png"):"PassengerTrain.png"),pixels.EncodeToPNG());}
                    finally {RenderTexture.active=active;}
                }
            } finally {if(target!=null) {target.Release();UnityEngine.Object.DestroyImmediate(target);}if(pixels!=null) UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(cameraObject);}
        }
        static void End(string output) {File.WriteAllText(Path.Combine(Folder,"passenger-checks.result"),output);UnityEngine.Profiling.Profiler.enabled=SessionState.GetBool(Key+".Profiler",false);SessionState.SetBool(Key,false);EditorApplication.ExitPlaymode();}
        static void CaptureContacts()
        {
            var holding=world.passengers.FirstOrDefault(p=>p.GetComponent<Metro3DPassengerContacts>().Holding&&p.GetComponent<Metro3DPassengerContacts>().GripError<.025f);
            var leaning=world.passengers.FirstOrDefault(p=>p.GetComponent<Metro3DPassengerContacts>().Leaning);
            if(holding==null) throw new InvalidOperationException("No passenger reached a real handle contact.");
            trace.AppendLine("PASS real handle reached, palm contact error "+holding.GetComponent<Metro3DPassengerContacts>().GripError.ToString("F4")+"m; lean count="+world.passengers.Count(p=>p.GetComponent<Metro3DPassengerContacts>().Leaning));
            var seated=world.passengers.FirstOrDefault(p=>p.IsSeated);
            if(seated!=null) {
                trace.AppendLine("CONTACT seat foot error="+seated.GetComponent<Metro3DPassengerContacts>().SeatFootError+" poseframe="+seated.GetComponent<Metro3DPassengerContacts>().PoseFrame+" frame="+Time.frameCount+" forced="+seated.GetComponent<Metro3DPassengerContacts>().ReviewWhileCulled);
                trace.AppendLine("CONTACT anchor local="+seated.transform.InverseTransformPoint(seated.Seat.anchor.position)+" forward="+seated.transform.InverseTransformDirection(seated.Seat.anchor.forward));
                var bone=seated.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name,t=>t);
                foreach(string name in new[]{"Hips","LeftUpLeg","LeftLeg","LeftFoot","LeftToeBase","Spine2","LeftArm","LeftForeArm","LeftHand"}) trace.AppendLine("SEAT BONE "+name+" p="+seated.transform.InverseTransformPoint(bone[name].position)+" scale="+bone[name].lossyScale);
            }
            foreach(var p in world.passengers.Where(p=>p.IsSeated)) {
                if(p.Seat==null||p.Seat.Occupant!=p) throw new InvalidOperationException("Seated passenger does not own its seat.");
                if(p.SittingWeight>.99f&&(p.transform.position-p.Seat.anchor.position).magnitude>.03f) throw new InvalidOperationException("Seated rider detached from moving-carriage seat.");
                var contact=p.GetComponent<Metro3DPassengerContacts>();
                if(contact.ReviewWhileCulled&&p.SittingWeight>.99f&&contact.SeatFootError>.03f) throw new InvalidOperationException("Seated foot contact failed: "+contact.SeatFootError);
            }
            foreach(var p in new[]{holding,leaning}) {
                if(p==null) continue;
                var obj=new GameObject("Temporary contact review camera"){hideFlags=HideFlags.HideAndDontSave};var rt=new RenderTexture(1100,800,24);var pixels=new Texture2D(1100,800,TextureFormat.RGB24,false);
                try {
                    var camera=obj.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.04f;camera.fieldOfView=55;rt.Create();
                    camera.transform.position=p.transform.position+p.transform.forward*1.4f+p.transform.right*.7f+Vector3.up*1.42f;camera.transform.LookAt(p.transform.position+Vector3.up*1.25f);
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                    var old=RenderTexture.active;try {RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1100,800),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Folder,p==holding?"PassengerGrip.png":"PassengerLean.png"),pixels.EncodeToPNG());}finally {RenderTexture.active=old;}
                } finally {rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(obj);}
            }
        }
        static void AuditClip()
        {
            var preview=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();GameObject model=null;
            try {
                const string root="Assets/_Project/Scenes/3D/Ingame/Game";
                model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(root+"/Art/Models/PassengerHuman.fbx"));SceneManager.MoveGameObjectToScene(model,preview);
                var report=new StringBuilder();var bones=model.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name,t=>t);
                foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(root+"/Art/Models/PassengerHuman.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__"))) {
                    clip.SampleAnimation(model,0);report.AppendLine(clip.name+" length="+clip.length);
                    foreach(string name in new[]{"Hips","LeftUpLeg","LeftLeg","LeftFoot","LeftArm","LeftForeArm","LeftHand"}) report.AppendLine(name+" "+model.transform.InverseTransformPoint(bones[name].position));
                    foreach(var binding in AnimationUtility.GetCurveBindings(clip).Where(b=>b.path.EndsWith("LeftUpLeg")&&b.propertyName.Contains("Rotation"))) {
                        var curve=AnimationUtility.GetEditorCurve(clip,binding);report.AppendLine(binding.path+" "+binding.propertyName+"="+curve.Evaluate(0));
                    }
                }
                File.WriteAllText(Path.Combine(Folder,"passenger-clip-audit.result"),report.ToString());
            } finally {if(model!=null) UnityEngine.Object.DestroyImmediate(model);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);}
        }
    }
}
#endif

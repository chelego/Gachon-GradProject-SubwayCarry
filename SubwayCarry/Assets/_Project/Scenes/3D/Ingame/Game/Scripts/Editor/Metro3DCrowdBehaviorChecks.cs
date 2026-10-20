#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;

namespace SubwayCarry.Gameplay3D.Editor
{
    // Passive observations of the actual integrated scene. Never changes colliders,
    // damage rules, destinations, passenger count, service timing or source scenes.
    [InitializeOnLoad]
    public static class Metro3DCrowdBehaviorChecks
    {
        const string Key="SubwayCarry.CrowdBehaviorCheck";
        const string ScenePath="Assets/_Project/Scenes/3D/Ingame/Game/SubwayCarry_3D_FirstPerson_Delivery.unity";
        static Metro3DWorld world;
        static float started,nextSample,lastSampleTime;
        static int lastFrame;
        static bool exiting;
        static ProfilerRecorder steering;
        static ProfilerRecorder gc;
        static readonly Dictionary<int,Vector3> velocities=new Dictionary<int,Vector3>(256);
        static readonly Dictionary<int,int> recoveries=new Dictionary<int,int>(256);
        static readonly Dictionary<string,int> recoveryReasons=new Dictionary<string,int>();
        static readonly Dictionary<int,float> stalls=new Dictionary<int,float>();
        static readonly Dictionary<int,float> queueWaits=new Dictionary<int,float>();
        static readonly List<float> finishedQueueWaits=new List<float>();
        struct Contact { public float duration; public int sample; }
        static readonly Dictionary<long,Contact> contacts=new Dictionary<long,Contact>(2048);
        static Result result;
        [Serializable] public sealed class Result
        {
            public string label,scene,status,error;
            public int seed=20261020,samples,activeMaximum,spawned,completed,arrivals,stop;
            public int headingReversals,recoveryEvents,contactSamples,contactEpisodes;
            public int doorSafetyViolations,longestStallActor;
            public float longestNonQueueStall;
            public string longestStallStage;
            public string maximumContactActors,longestStallDetail;
            public float maximumContactTime,contactClosingSpeed;
            public float longestStandTransitionPause;
            public int completedQueueWaits;
            public float queueWaitMedian,queueWaitP95,queueWaitMaximum,unfinishedQueueWaitMaximum;
            public double steeringTotalMs;
            public int steeringFrames;
            public long gcAllocatedBytes;
            public int pathRequests,pathSolves;
            public double pathMeasuredMs,pathMillisecondsPerSolve;
            public int steeringCalls;
            public double steeringMeasuredMs,steeringMicrosecondsPerCall;
            public float steeringMeanMs,maximumCapsulePenetration,longestOverlap,meanSpeed,meanAcceleration;
            public int[] stages=new int[12];
            public string[] invariants;
            public string[] recoveryReasons;
            public float duration,npcSpeed;
            public int stationCount,passengerCount,passengersPerCar;
        }
        static Metro3DCrowdBehaviorChecks()
        {
            EditorApplication.update+=Tick;
            EditorApplication.quitting+=PreserveRegressionResults;
            EditorApplication.playModeStateChanged+=Changed;
        }
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Crowd comparison requires edit mode.");
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Unsaved scene: comparison will not save or discard edits.");
            string label=Argument("-crowdLabel","baseline");
            Directory.CreateDirectory("Logs/CrowdBehavior");
            SessionState.SetString(Key+".Label",label);
            SessionState.SetString(Key+".Progress",PlayerPrefs.GetString("SubwayCarry.3D.Progress.v1",""));
            SessionState.SetBool(Key+".HadProgress",PlayerPrefs.HasKey("SubwayCarry.3D.Progress.v1"));
            SessionState.SetBool(Key+".Batch",Application.isBatchMode);
            SessionState.SetFloat(Key+".CaptureDelta",Time.captureDeltaTime);
            SessionState.SetFloat(Key+".Seconds",float.Parse(Argument("-crowdSeconds","90"),System.Globalization.CultureInfo.InvariantCulture));
            // Equal starting delivery state in both runs. Restored after leaving Play.
            PlayerPrefs.DeleteKey("SubwayCarry.3D.Progress.v1");
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Restore",true);
            EditorApplication.EnterPlaymode();
        }
        static string Argument(string name,string fallback)
        {
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++) if(args[i]==name) return args[i+1];
            return fallback;
        }
        static void Changed(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key+".Aux",false)&&SessionState.GetString(Key+".AuxStage","")=="helper") {
                Time.captureDeltaTime=1f/60;
                // Player Update intentionally requires a Keyboard. Batch mode
                // has no native input device; supply zero-input test devices,
                // not a shortcut around boarding/help eligibility rules.
                if(UnityEngine.InputSystem.Keyboard.current==null)
                    SessionState.SetInt(Key+".Keyboard",UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>().deviceId);
                if(UnityEngine.InputSystem.Mouse.current==null)
                    SessionState.SetInt(Key+".Mouse",UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>().deviceId);
            }
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
            {
                world=null;result=new Result{label=SessionState.GetString(Key+".Label","baseline"),scene=ScenePath,status="running"};
                // Headless rendering otherwise runs at an unbounded variable rate,
                // changing decision/arrival phases despite using the same seed.
                Time.captureDeltaTime=1f/60;
                velocities.Clear();recoveries.Clear();recoveryReasons.Clear();stalls.Clear();queueWaits.Clear();finishedQueueWaits.Clear();contacts.Clear();lastFrame=-1;lastSampleTime=0;exiting=false;
                UnityEngine.Random.InitState(result.seed);
                steering=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"SubwayCarry.Crowd.Steering",128,
                    ProfilerRecorderOptions.Default|ProfilerRecorderOptions.SumAllSamplesInFrame);
                gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",128,ProfilerRecorderOptions.Default);
                Application.logMessageReceived+=Log;
            }
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+".Restore",false))
            {
                SessionState.SetBool(Key+".Restore",false);SessionState.SetBool(Key,false);
                if(SessionState.GetBool(Key+".HadProgress",false)) PlayerPrefs.SetString("SubwayCarry.3D.Progress.v1",SessionState.GetString(Key+".Progress",""));
                else PlayerPrefs.DeleteKey("SubwayCarry.3D.Progress.v1");
                PlayerPrefs.Save();
                Time.captureDeltaTime=SessionState.GetFloat(Key+".CaptureDelta",0);
                if(SessionState.GetBool(Key+".Batch",false)) EditorApplication.delayCall+=()=>EditorApplication.Exit(result!=null&&result.status=="complete"?0:1);
            }
        }
        static void Log(string message,string stack,LogType type)
        {
            if(type==LogType.Exception||type==LogType.Error) {if(result!=null) result.error+=(result.error==null?"":"\n")+message;}
        }
        static void Tick()
        {
            if(SessionState.GetBool(Key+".Aux",false)) {TickRegressions();return;}
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling||exiting) return;
            try
            {
                if(world==null)
                {
                    world=UnityEngine.Object.FindFirstObjectByType<Metro3DWorld>();
                    if(world==null||world.Traffic==null||world.passengers.Count==0) return;
                    started=Time.time;nextSample=started+5;
                    result.npcSpeed=world.settings.npcSpeed;result.passengerCount=world.settings.passengerCount;result.passengersPerCar=world.settings.passengersPerCar;
                }
                if(lastFrame!=Time.frameCount&&Time.time-started>5)
                {
                    lastFrame=Time.frameCount;
                    if(steering.Valid&&steering.Count>0) {result.steeringTotalMs+=steering.LastValue/1000000.0;result.steeringFrames++;}
                    if(gc.Valid&&gc.Count>0)result.gcAllocatedBytes+=gc.LastValue;
                }
                if(Time.time>=nextSample) {nextSample=Time.time+.1f;Sample();}
                if(Time.time-started>=SessionState.GetFloat(Key+".Seconds",90)) Finish();
            }
            catch(Exception ex) {if(result!=null) result.error+="\n"+ex;Finish();}
        }
        static void Sample()
        {
            float elapsed=lastSampleTime>0?Time.time-lastSampleTime:0;lastSampleTime=Time.time;
            var people=world.passengers;result.samples++;result.activeMaximum=Mathf.Max(result.activeMaximum,people.Count);
            float totalSpeed=0,totalAcceleration=0;
            for(int i=0;i<people.Count;i++)
            {
                var a=people[i];if(a==null) continue;
                Vector3 v=a.Velocity;totalSpeed+=v.magnitude;
                if(a.QueueActive) {
                    queueWaits.TryGetValue(a.index,out float wait);
                    if(a.WaitingForQueue&&v.sqrMagnitude<.01f)wait+=elapsed;
                    queueWaits[a.index]=wait;
                } else if(queueWaits.TryGetValue(a.index,out float wait)) {finishedQueueWaits.Add(wait);queueWaits.Remove(a.index);}
                result.longestStandTransitionPause=Mathf.Max(result.longestStandTransitionPause,a.TransitionBlockedFor);
                Vector3 toGoal=a.CurrentGoal-a.transform.position;toGoal.y=0;
                stalls.TryGetValue(a.index,out float stalled);
                bool shouldMove=!a.IsResting&&!a.IsSeated&&a.SittingWeight<=0&&!a.WaitingForQueue&&toGoal.sqrMagnitude>.16f;
                stalled=shouldMove&&v.sqrMagnitude<.045f*.045f?stalled+elapsed:0;stalls[a.index]=stalled;
                if(stalled>result.longestNonQueueStall) {result.longestNonQueueStall=stalled;result.longestStallActor=a.index;result.longestStallStage=a.Journey.Blackboard.CurrentStage.ToString();result.longestStallDetail=Describe(a);}
                if(a.DoorCrossing&&(a.Riding!=null?a.Riding.Moving:world.train.Moving))result.doorSafetyViolations++;
                if(velocities.TryGetValue(a.index,out var previous))
                {
                    totalAcceleration+=(v-previous).magnitude/.1f;
                    if(previous.sqrMagnitude>.09f&&v.sqrMagnitude>.09f&&Vector3.Dot(previous.normalized,v.normalized)<-.2f) result.headingReversals++;
                }
                velocities[a.index]=v;
                int count=a.Journey.Blackboard.RecoveryCount;
                recoveries.TryGetValue(a.index,out int old);result.recoveryEvents+=Mathf.Max(0,count-old);recoveries[a.index]=count;
                if(count>old) {
                    string reason=a.Journey.Blackboard.CurrentStage+": "+a.Journey.Blackboard.LastRecoveryReason;
                    recoveryReasons.TryGetValue(reason,out int seen);recoveryReasons[reason]=seen+count-old;
                }
                if(a.IsSeated) continue;
                for(int j=i+1;j<people.Count;j++)
                {
                    var b=people[j];if(b==null||b.IsSeated) continue;
                    Vector3 delta=b.transform.position-a.transform.position;
                    // Capsule-body proxy for unseated characters, not a mesh/cloth penetration measurement.
                    float vertical=Mathf.Max(0,Mathf.Abs(delta.y)-1.27f);
                    float penetration=.48f-Mathf.Sqrt(delta.x*delta.x+delta.z*delta.z+vertical*vertical);
                    if(penetration<=.015f) continue;
                    result.contactSamples++;
                    if(penetration>result.maximumCapsulePenetration) {
                        result.maximumCapsulePenetration=penetration;result.maximumContactTime=Time.time-started;
                        result.maximumContactActors=Describe(a)+" | "+Describe(b);
                        result.contactClosingSpeed=Mathf.Max(0,-Vector3.Dot(b.Velocity-a.Velocity,delta.normalized));
                    }
                    long key=((long)Mathf.Min(a.index,b.index)<<32)|(uint)Mathf.Max(a.index,b.index);
                    contacts.TryGetValue(key,out var contact);
                    if(contact.sample!=result.samples-1) {contact.duration=0;result.contactEpisodes++;}
                    contact.duration+=.1f;contact.sample=result.samples;contacts[key]=contact;
                    result.longestOverlap=Mathf.Max(result.longestOverlap,contact.duration);
                }
            }
            if(people.Count>0) {result.meanSpeed+=totalSpeed/people.Count;result.meanAcceleration+=totalAcceleration/people.Count;}
        }
        static string Describe(Metro3DPassenger a)
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var motor=a.GetComponent<CharacterController>();
            var path=(Vector3[])typeof(Metro3DPassenger).GetField("path",flags).GetValue(a);
            int pathIndex=(int)typeof(Metro3DPassenger).GetField("pathIndex",flags).GetValue(a),pathCount=(int)typeof(Metro3DPassenger).GetField("pathCount",flags).GetValue(a);
            Vector3 waypoint=pathIndex<pathCount?a.transform.parent.TransformPoint(path[pathIndex]):a.CurrentGoal;
            return a.index+" "+a.Journey.Blackboard.CurrentStage+" pos="+a.transform.position+" goal="+a.CurrentGoal+" velocity="+a.Velocity+
                " state="+typeof(Metro3DPassenger).GetField("state",flags).GetValue(a)+" motor="+motor.enabled+" stair="+a.Journey.OnStairs+
                " queue="+a.QueueActive+" portal="+a.PortalCommitted+" waypoint="+waypoint;
        }
        static void Finish()
        {
            if(exiting) return;exiting=true;Application.logMessageReceived-=Log;steering.Dispose();gc.Dispose();
            if(result==null) result=new Result{label=SessionState.GetString(Key+".Label","unknown"),error="World did not initialize"};
            result.status=string.IsNullOrEmpty(result.error)?"complete":"error";
            if(world!=null)
            {
                result.duration=Time.time-started;result.spawned=world.Traffic.SpawnedCount;result.completed=world.Traffic.CompletedCount;
                result.arrivals=world.Traffic.EntranceArrivals;result.stop=world.train.CurrentStop;result.stationCount=world.stations.Count;
                var crowd=world.GetComponent<Metro3DCrowd>();result.steeringCalls=crowd.SteeringCalls;
                result.pathRequests=crowd.PathRequests;result.pathSolves=crowd.PathSolves;
                result.pathMeasuredMs=crowd.PathTicks*1000.0/System.Diagnostics.Stopwatch.Frequency;
                result.pathMillisecondsPerSolve=result.pathMeasuredMs/Math.Max(1,result.pathSolves);
                result.steeringMeasuredMs=crowd.SteeringTicks*1000.0/System.Diagnostics.Stopwatch.Frequency;
                result.steeringMicrosecondsPerCall=result.steeringMeasuredMs*1000/Math.Max(1,result.steeringCalls);
                foreach(var p in world.passengers) if(p!=null) {int n=(int)p.Journey.Blackboard.CurrentStage;if(n>=0&&n<result.stages.Length) result.stages[n]++;}
            }
            if(result.samples>0) {result.meanSpeed/=result.samples;result.meanAcceleration/=result.samples;}
            if(result.steeringFrames>0) result.steeringMeanMs=(float)(result.steeringTotalMs/result.steeringFrames);
            var reasons=new List<string>();foreach(var pair in recoveryReasons)reasons.Add(pair.Value+" x "+pair.Key);result.recoveryReasons=reasons.ToArray();
            finishedQueueWaits.Sort();result.completedQueueWaits=finishedQueueWaits.Count;
            if(finishedQueueWaits.Count>0) {
                result.queueWaitMedian=finishedQueueWaits[(finishedQueueWaits.Count-1)/2];
                result.queueWaitP95=finishedQueueWaits[Mathf.CeilToInt((finishedQueueWaits.Count-1)*.95f)];
                result.queueWaitMaximum=finishedQueueWaits[finishedQueueWaits.Count-1];
            }
            foreach(var wait in queueWaits.Values)result.unfinishedQueueWaitMaximum=Mathf.Max(result.unfinishedQueueWaitMaximum,wait);
            if(world!=null&&!result.label.StartsWith("baseline")) {
                try {result.invariants=CheckInvariants();}
                catch(Exception ex) {result.invariants=new[]{"FAIL invariant execution: "+ex};}
                foreach(var check in result.invariants) if(check.StartsWith("FAIL")) {result.status="error";result.error+="\n"+check;}
            }
            File.WriteAllText("Logs/CrowdBehavior/"+result.label+".json",JsonUtility.ToJson(result,true));
            if(result.label.Contains("trace")) TraceBlocked();
            Debug.Log("Crowd behavior comparison: "+result.label+" "+result.status);
            EditorApplication.ExitPlaymode();
        }
        static void TraceBlocked()
        {
            if(world==null)return;
            Debug.Log("CROWD TRACE service="+world.session.ServiceLabel+" timer="+world.session.Timer+" open="+world.train.DoorsOpen);
            int count=0;
            foreach(var p in world.passengers) {
                if(p==null||p.IsSeated||(p.CurrentGoal-p.transform.position).sqrMagnitude<.16f)continue;
                if(p.Velocity.sqrMagnitude>.045f*.045f)continue;
                if(count++>=18)break;
                var cc=p.GetComponent<CharacterController>();
                var text=new System.Text.StringBuilder("CROWD actor="+p.index+" stage="+p.Journey.Blackboard.CurrentStage+" position="+p.transform.position+" target="+p.CurrentGoal+" actual="+p.Velocity+" cost="+p.SteeringCost+" static="+p.StaticRejects+" queue="+p.QueueActive+" portal="+p.PortalCommitted+" grounded="+cc.isGrounded);
                foreach(string field in new[]{"state","steering","velocity","pendingPath","pathIndex","pathCount","waypoint"})
                    text.Append(" "+field+"="+typeof(Metro3DPassenger).GetField(field,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(p));
                text.Append(" leader="+(p.QueueLeader==null?-1:p.QueueLeader.index)+" committed="+p.QueueCommitted+" recovery="+p.Journey.Blackboard.LastRecoveryReason);
                int nearest=0;foreach(var other in world.passengers) {
                    if(other==p||Mathf.Abs(other.transform.position.y-p.transform.position.y)>1.5f||(other.transform.position-p.transform.position).sqrMagnitude>2.25f)continue;
                    if(nearest++>=5)break;
                    text.Append(" neighbor="+other.index+":"+(other.transform.position-p.transform.position)+":"+other.Velocity+":seat="+other.IsSeated+":queue="+other.QueueActive);
                }
                Vector3 goal=p.CurrentGoal-p.transform.position;goal.y=0;
                foreach(var hit in Physics.SphereCastAll(p.transform.position+Vector3.up*.72f,.23f,goal.normalized,.8f,~0,QueryTriggerInteraction.Ignore))
                    if(!(hit.collider is CharacterController))text.Append(" obstacle="+hit.collider.name+":"+hit.distance.ToString("F3"));
                Debug.Log(text.ToString());
            }
        }
        static string[] CheckInvariants()
        {
            var answers=new List<string>();var crowd=world.GetComponent<Metro3DCrowd>();
            var claimed=new HashSet<Metro3DPassenger>();bool unique=true,live=true;
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            foreach(string name in new[]{"gateQueues","doorQueues"}) {
                var queues=(System.Collections.IDictionary)typeof(Metro3DStationTraffic).GetField(name,flags).GetValue(world.Traffic);
                foreach(System.Collections.IEnumerable queue in queues.Values)foreach(Metro3DPassenger passenger in queue) {
                    if(passenger==null||!passenger.isActiveAndEnabled)live=false;
                    else if(!claimed.Add(passenger))unique=false;
                }
            }
            answers.Add((unique?"PASS":"FAIL")+" no passenger belongs to duplicate live facility queues");
            answers.Add((live?"PASS":"FAIL")+" facility queues contain no inactive passenger reservations");
            var a=new GameObject("Temporary neighbor-frame probe A").AddComponent<Metro3DPassenger>();
            var b=new GameObject("Temporary neighbor-frame probe B").AddComponent<Metro3DPassenger>();
            var root=world.train.transform;Vector3 trainPosition=root.position;GameObject probe=null;
            try {
                a.transform.SetParent(root,false);b.transform.SetParent(root,false);
                a.transform.localPosition=new Vector3(1000,3.99f,1000);b.transform.localPosition=new Vector3(1000,4.01f,1000);
                crowd.Register(a);crowd.Register(b);crowd.InvalidateSpatial();
                bool adjacent=Mathf.Abs(crowd.LocalDensity(a.transform.position,root)-.25f)<.001f;
                answers.Add((adjacent?"PASS":"FAIL")+" neighboring height bins preserve nearby pedestrians");
                root.position+=Vector3.forward*80;
                bool moving=Mathf.Abs(crowd.LocalDensity(a.transform.position,root)-.25f)<.001f;
                answers.Add((moving?"PASS":"FAIL")+" moving train frame preserves cached neighbors without rehash");
                b.transform.localPosition+=Vector3.up*3.6f;crowd.InvalidateSpatial();
                answers.Add((Mathf.Abs(crowd.LocalDensity(a.transform.position,root)-.125f)<.001f?"PASS":"FAIL")+" separate floors do not share local density");
                root.position=trainPosition;
                probe=UnityEngine.Object.Instantiate(world.settings.passengerPrefab);probe.SetActive(false);
                probe.transform.SetParent(world.stations[0],false);probe.transform.localPosition=new Vector3(.5f,world.settings.departureFloor,-10);
                var passenger=probe.GetComponent<Metro3DPassenger>();if(passenger==null)passenger=probe.AddComponent<Metro3DPassenger>();
                passenger.Initialize(world,2000000,0,false,true,false);probe.SetActive(true);
                string destination=passenger.Journey.DestinationId;
                var first=world.Traffic.SelectGate(passenger,false,false);world.Traffic.ReleaseGate(passenger);
                passenger.index+=1001;
                var second=world.Traffic.SelectGate(passenger,false,false);world.Traffic.ReleaseGate(passenger);
                answers.Add((first!=null&&first==second?"PASS":"FAIL")+" gate utility is independent of passenger index for identical state");
                if(first!=null) {
                    bool enabled=first.enabled;first.enabled=false;
                    try {var alternate=world.Traffic.SelectGate(passenger,false,false);answers.Add((alternate!=null&&alternate!=first?"PASS":"FAIL")+" closed facility is excluded");}
                    finally {first.enabled=enabled;world.Traffic.ReleaseGate(passenger);}
                    answers.Add((world.Traffic.GateSlot(passenger,first)==-1?"PASS":"FAIL")+" released passenger leaves no gate reservation");
                }
                answers.Add((destination==passenger.Journey.DestinationId?"PASS":"FAIL")+" facility changes preserve destination");
                var method=typeof(Metro3DStationTraffic).GetMethod("QueuePoint",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                Vector3 head=new Vector3(1000,0,1000);a.transform.localPosition=head-Vector3.forward*2;
                Vector3 before=(Vector3)method.Invoke(null,new object[]{passenger,a,root,head,Vector3.forward});
                a.transform.localPosition+=Vector3.forward*.2f;
                Vector3 after=(Vector3)method.Invoke(null,new object[]{passenger,a,root,head,Vector3.forward});
                answers.Add((Mathf.Abs(Vector3.Distance(before,after)-.2f)<.002f?"PASS":"FAIL")+" queue target follows real leader displacement continuously");
                var stateField=typeof(Metro3DPassenger).GetField("state",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                stateField.SetValue(passenger,Enum.Parse(stateField.FieldType,"Board"));
                passenger.FollowQueueGoal(passenger.transform.position+Vector3.forward*.1f);
                answers.Add((stateField.GetValue(passenger).ToString()=="Board"?"PASS":"FAIL")+" queue locomotion preserves boarding action");
                stateField.SetValue(passenger,Enum.Parse(stateField.FieldType,"Walk"));
                passenger.SetQueueContext(null,Vector3.forward,.8f,true);
                passenger.WalkJourney(passenger.transform.position+Vector3.right,false);
                answers.Add((!passenger.QueueCommitted?"PASS":"FAIL")+" side approach does not become a forward-only queue lane");
                passenger.ClearQueueContext();
                var stairs=typeof(Metro3DPassengerJourney).GetField("stairCommitted",flags);
                stairs.SetValue(passenger.Journey,true);int requestsBefore=crowd.PathRequests;
                typeof(Metro3DPassenger).GetMethod("RequestPath",flags).Invoke(passenger,null);
                bool noPlanarRequest=crowd.PathRequests==requestsBefore&&!(bool)typeof(Metro3DPassenger).GetField("pendingPath",flags).GetValue(passenger);
                stairs.SetValue(passenger.Journey,false);
                answers.Add((noPlanarRequest?"PASS":"FAIL")+" committed stair traversal never requests a planar floor path");
                // Decision-level relative-velocity probe, not a physical player
                // collision test. World/player poses are restored immediately.
                passenger.transform.localPosition=new Vector3(1000,3.6f,1000);
                var player=world.player.transform;Vector3 savedPlayer=player.position;
                var playerMotor=player.GetComponent<CharacterController>();bool motorEnabled=playerMotor.enabled;
                var motionField=typeof(Metro3DCrowd).GetField("measuredPlayerVelocity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                Vector3 savedMotion=(Vector3)motionField.GetValue(crowd);
                try {
                    playerMotor.enabled=false;player.position=passenger.transform.position+Vector3.forward*1.5f;crowd.InvalidateSpatial();
                    motionField.SetValue(crowd,-Vector3.forward*3);
                    float closing=Vector3.Dot(crowd.SafeVelocity(passenger,Vector3.forward*1.2f),Vector3.forward);
                    motionField.SetValue(crowd,Vector3.forward*3);
                    float leaving=Vector3.Dot(crowd.SafeVelocity(passenger,Vector3.forward*1.2f),Vector3.forward);
                    answers.Add((leaving>closing+.05f?"PASS":"FAIL")+" steering distinguishes approaching and receding player velocity");
                } finally {player.position=savedPlayer;playerMotor.enabled=motorEnabled;motionField.SetValue(crowd,savedMotion);}
                crowd.ReleaseStandSpace(passenger);
            }
            finally {
                root.position=trainPosition;crowd.Unregister(a);crowd.Unregister(b);
                UnityEngine.Object.DestroyImmediate(a.gameObject);UnityEngine.Object.DestroyImmediate(b.gameObject);
                if(probe!=null) {var passenger=probe.GetComponent<Metro3DPassenger>();world.Traffic.ReleaseGate(passenger);UnityEngine.Object.DestroyImmediate(probe);}
                crowd.InvalidateSpatial();
            }
            return answers.ToArray();
        }
        public static void RunRegressions()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Cannot interrupt Play.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Unsaved scene preserved.");
            Directory.CreateDirectory("Logs/CrowdBehavior");Directory.CreateDirectory("Temp/AiV2_3D");
            string mode=Argument("-crowdRegression","fields");
            if(mode!="fields"&&mode!="helper")throw new ArgumentException("Regression must be fields or helper");
            SessionState.SetBool(Key+".Aux",true);SessionState.SetString(Key+".AuxStage",mode);
            SessionState.SetFloat(Key+".AuxStarted",(float)EditorApplication.timeSinceStartup);
            SessionState.SetFloat(Key+".AuxCaptureDelta",Time.captureDeltaTime);
            SessionState.SetBool(Key+".AuxHadProgress",PlayerPrefs.HasKey("SubwayCarry.3D.Progress.v1"));
            SessionState.SetString(Key+".AuxProgress",PlayerPrefs.GetString("SubwayCarry.3D.Progress.v1",""));
            EditorSceneManager.OpenScene(ScenePath);
            if(mode=="fields") SubwayCarry.EditorTools.AI.V2.PassengerAiV23DFieldChecks.BeginAll();
            else {
                Directory.CreateDirectory("Temp/MLAgents");
                if(File.Exists("Temp/MLAgents/ingame.result"))File.Delete("Temp/MLAgents/ingame.result");
                PlayerPrefs.DeleteKey("SubwayCarry.3D.Progress.v1");
                File.WriteAllText("Temp/MLAgents/ingame.request","RUN");
            }
        }
        static void PreserveRegressionResults()
        {
            if(!SessionState.GetBool(Key+".Aux",false))return;
            string stage=SessionState.GetString(Key+".AuxStage","");
            string source=stage=="fields"?"Temp/AiV2_3D/FieldChecks.result":"Temp/MLAgents/ingame.result";
            if(File.Exists(source))File.Copy(source,"Logs/CrowdBehavior/"+stage+".result",true);
            foreach(string name in new[]{"Keyboard","Mouse"}) {
                var device=UnityEngine.InputSystem.InputSystem.GetDeviceById(SessionState.GetInt(Key+"."+name,-1));
                if(device!=null)UnityEngine.InputSystem.InputSystem.RemoveDevice(device);
                SessionState.EraseInt(Key+"."+name);
            }
            Time.captureDeltaTime=SessionState.GetFloat(Key+".AuxCaptureDelta",0);
            if(SessionState.GetBool(Key+".AuxHadProgress",false)) PlayerPrefs.SetString("SubwayCarry.3D.Progress.v1",SessionState.GetString(Key+".AuxProgress",""));
            else PlayerPrefs.DeleteKey("SubwayCarry.3D.Progress.v1");
            PlayerPrefs.Save();SessionState.SetBool(Key+".Aux",false);
        }
        static void TickRegressions()
        {
            if(EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".AuxStarted",0)>600) {
                PreserveRegressionResults();Debug.LogError("Crowd regression timed out; progress restored.");
                if(Application.isBatchMode)EditorApplication.Exit(2);
                return;
            }
            string stage=SessionState.GetString(Key+".AuxStage","");
            if(stage=="helper"&&File.Exists("Temp/MLAgents/ingame.result")) {
                string output=File.ReadAllText("Temp/MLAgents/ingame.result");
                bool passed=output.Contains("\"PASS\"");
                PreserveRegressionResults();
                if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(passed?0:1);
            }
        }
    }
}
#endif

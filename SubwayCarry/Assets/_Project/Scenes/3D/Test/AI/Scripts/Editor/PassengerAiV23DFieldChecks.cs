#if UNITY_EDITOR
using System;
using System.IO;
using SubwayCarry.AI.V2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SubwayCarry.EditorTools.AI.V2
{
    /// <summary>Short isolated field checks, never the 06 full-journey test.
    /// SessionState carries failures across Play domain reloads; clean editing scenes are restored.</summary>
    [InitializeOnLoad]
    public static class PassengerAiV23DFieldChecks
    {
        private const string Key="SubwayCarry.AI3D.FieldsCheck.";
        private const string Result="Temp/AiV2_3D/FieldChecks.result";
        [Serializable] private class Setup { public string[] paths; public bool[] loaded,active; }
        private static PassengerAiV2Agent[] agents;
        private static PassengerAiV23DMotor[] motors;
        private static Vector2[] last;
        private static float[] travelled;
        private static PassengerAiV2CrowdManager crowd;
        private static PassengerAiV2StairSmartObject stair;
        private static PassengerAiV2FareGateSmartObject[] gates;
        private static PassengerAiV2TrainDoorSmartObject door;
        private static PassengerAiV2TrainInteriorEnvironment interior;
        private static PassengerAiV2TrainInteriorAction[] activities;
        private static double started;
        private static float simulatedAt,minimumHeight,maximumHeight,lastSampleTime,maxStairPlanar,maxStairVertical;
        private static Vector3[] previousWorld;
        private static int deadlocks,seats,stands,leans,prepared;
        private static string failure;
        private static bool reportedActivityPositions;
        public static bool IsRunning => SessionState.GetBool(Key+"Pending",false);
        public static void ClearIdleCheck()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Refusing to interrupt Play.");
            SessionState.SetBool(Key+"Pending",false); SessionState.SetBool(Key+"Finished",false);
            SessionState.SetBool(Key+"NeedsNext",false); SessionState.SetBool(Key+"EnteredPlay",false);
            SessionState.SetInt(Key+"Generation",SessionState.GetInt(Key+"Generation",0)+1);
            EditorApplication.update-=Tick; EditorApplication.playModeStateChanged-=State;
        }
        static PassengerAiV23DFieldChecks()
        {
            Application.logMessageReceived+=TrackRuntimeError;
            if(!SessionState.GetBool(Key+"Pending",false)) return;
            Attach();
            int generation=SessionState.GetInt(Key+"Generation",0);
            EditorApplication.delayCall+=()=> { if(generation==SessionState.GetInt(Key+"Generation",0)) ResumeAfterReload(); };
        }
        private static void TrackRuntimeError(string message,string stack,LogType type)
        {
            if(SessionState.GetBool(Key+"Pending",false)&&EditorApplication.isPlaying && (type==LogType.Error||type==LogType.Exception||type==LogType.Assert))
                SessionState.SetString(Key+"RuntimeError",message);
        }
        private static void ResumeAfterReload()
        {
            if(!SessionState.GetBool(Key+"Pending",false)||EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(SessionState.GetBool(Key+"NeedsNext",false)) { Next(); return; }
            if(!SessionState.GetBool(Key+"Finished",false))
            {
                if(SceneManager.GetActiveScene().path!=PassengerAiV23DFieldsBuilder.Paths[SessionState.GetInt(Key+"Number",1)-1])
                {File.AppendAllText(Result,"ABORTED: idle Play transition lost; editing scene preserved\n");ClearIdleCheck();return;}
                if(!SessionState.GetBool(Key+"EnteredPlay",false)) {SessionState.SetBool(Key+"NeedsNext",true);Next();}
                return;
            }
            int number=SessionState.GetInt(Key+"Number",1);
            if(SceneManager.GetActiveScene().path!=PassengerAiV23DFieldsBuilder.Paths[number-1])
            { SessionState.SetBool(Key+"NeedsNext",true); Next(); }
            else Advance();
        }
        private static void Attach()
        {
            EditorApplication.playModeStateChanged-=State; EditorApplication.playModeStateChanged+=State;
            EditorApplication.update-=Tick; EditorApplication.update+=Tick;
        }
        [MenuItem("SubwayCarry/Passenger AI V2/3D/Check fields 01-05 (clean scenes only)")]
        public static void BeginAll() { Begin(0); }
        public static void Begin(int onlyField)
        {
            if(SessionState.GetBool(Key+"Pending",false)) throw new InvalidOperationException("A field check is already active; it must finish before another begins.");
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Cannot interrupt an active Play session.");
            SceneSetup[] original=EditorSceneManager.GetSceneManagerSetup();
            var setup=new Setup {paths=new string[original.Length],loaded=new bool[original.Length],active=new bool[original.Length]};
            for(int i=0;i<original.Length;i++)
            {
                Scene scene=SceneManager.GetSceneByPath(original[i].path);
                if(string.IsNullOrEmpty(original[i].path)||scene.IsValid()&&scene.isDirty)
                    throw new InvalidOperationException("Unsaved editing scene preserved. Save it yourself before field checks.");
                setup.paths[i]=original[i].path; setup.loaded[i]=original[i].isLoaded; setup.active[i]=original[i].isActive;
            }
            SessionState.SetString(Key+"Setup",JsonUtility.ToJson(setup));
            SessionState.SetInt(Key+"Generation",SessionState.GetInt(Key+"Generation",0)+1);
            SessionState.SetInt(Key+"Number",onlyField>0?onlyField:1);
            SessionState.SetInt(Key+"Last",onlyField>0?onlyField:5);
            SessionState.SetBool(Key+"Pending",true); SessionState.SetBool(Key+"AnyFailure",false);
            SessionState.SetBool(Key+"EnteredPlay",false);
            SessionState.EraseBool(Key+"Finished");
            SessionState.SetBool(Key+"NeedsNext",true);
            string header="RUNNING: isolated 30-second field checks; no full journey; requested="+onlyField+"\n";
            if(onlyField>0) File.AppendAllText(Result,header); else File.WriteAllText(Result,header);
            Attach(); EditorApplication.delayCall+=Next;
        }
        private static void Next()
        {
            if(!SessionState.GetBool(Key+"Pending",false)||!SessionState.GetBool(Key+"NeedsNext",false)||EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(Key+"NeedsNext",false);
            SessionState.SetBool(Key+"EnteredPlay",false);
            int number=SessionState.GetInt(Key+"Number",1);
            EditorSceneManager.OpenScene(PassengerAiV23DFieldsBuilder.Paths[number-1],OpenSceneMode.Single);
            SessionState.EraseBool(Key+"Finished");
            EditorApplication.isPlaying=true;
        }
        private static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key+"Pending",false)) return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(Key+"EnteredPlay",true);
                Application.runInBackground=true;
                agents=UnityEngine.Object.FindObjectsByType<PassengerAiV2Agent>(FindObjectsSortMode.None);
                motors=new PassengerAiV23DMotor[agents.Length]; last=new Vector2[agents.Length]; travelled=new float[agents.Length];
                previousWorld=new Vector3[agents.Length];
                for(int i=0;i<agents.Length;i++) { motors[i]=agents[i].GetComponentInChildren<PassengerAiV23DMotor>(); last[i]=agents[i].Position; previousWorld[i]=motors[i].WorldPosition; }
                crowd=UnityEngine.Object.FindFirstObjectByType<PassengerAiV2CrowdManager>();
                stair=UnityEngine.Object.FindFirstObjectByType<PassengerAiV2StairSmartObject>();
                gates=UnityEngine.Object.FindObjectsByType<PassengerAiV2FareGateSmartObject>(FindObjectsSortMode.None);
                door=UnityEngine.Object.FindFirstObjectByType<PassengerAiV2TrainDoorSmartObject>();
                interior=UnityEngine.Object.FindFirstObjectByType<PassengerAiV2TrainInteriorEnvironment>();
                activities=UnityEngine.Object.FindObjectsByType<PassengerAiV2TrainInteriorAction>(FindObjectsSortMode.None);
                started=EditorApplication.timeSinceStartup; simulatedAt=Time.time;
                minimumHeight=float.PositiveInfinity; maximumHeight=float.NegativeInfinity;
                lastSampleTime=Time.time; maxStairPlanar=maxStairVertical=0;
                deadlocks=seats=stands=leans=prepared=0; failure="";
                reportedActivityPositions=false; SessionState.EraseString(Key+"RuntimeError");
            }
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                if(!SessionState.GetBool(Key+"EnteredPlay",false)) return;
                if(!SessionState.GetBool(Key+"Finished",false))
                { File.AppendAllText(Result,"FAIL: interrupted Play; no pass claimed\n"); SessionState.SetBool(Key+"AnyFailure",true); Restore(); return; }
                Advance();
            }
        }
        private static void Advance()
        {
            if(SessionState.GetBool(Key+"NeedsNext",false)) {EditorApplication.delayCall+=Next;return;}
            int number=SessionState.GetInt(Key+"Number",1);
            if(number<SessionState.GetInt(Key+"Last",5))
            { SessionState.SetInt(Key+"Number",number+1); SessionState.SetBool(Key+"NeedsNext",true); EditorApplication.delayCall+=Next; }
            else Restore();
        }
        private static void Restore()
        {
            EditorApplication.update-=Tick; EditorApplication.playModeStateChanged-=State;
            SessionState.SetBool(Key+"Pending",false);
            var saved=JsonUtility.FromJson<Setup>(SessionState.GetString(Key+"Setup","{}"));
            if(saved.paths!=null)
            {
                var setup=new SceneSetup[saved.paths.Length];
                for(int i=0;i<setup.Length;i++) setup[i]=new SceneSetup {path=saved.paths[i],isLoaded=saved.loaded[i],isActive=saved.active[i]};
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            bool failed=SessionState.GetBool(Key+"AnyFailure",true);
            File.AppendAllText(Result,failed?"OVERALL FAIL\n":"OVERALL PASS: numeric field checks only; visual/Profiler approval not performed\n");
            Debug.Log("AI3D_FIELD_CHECKS_"+(failed?"FAIL":"PASS")+": original clean scene setup restored.");
            if(Application.isBatchMode) EditorApplication.Exit(failed?2:0);
        }
        private static void Tick()
        {
            // delayCall can wait for an Inspector repaint while Unity is in the background.
            // Drive owned, explicit test transitions from the editor update instead.
            if(IsRunning && !EditorApplication.isPlayingOrWillChangePlaymode &&
                !EditorApplication.isCompiling && !EditorApplication.isUpdating && SessionState.GetBool(Key+"NeedsNext",false))
            { Next(); return; }
            if(!EditorApplication.isPlaying||agents==null||SessionState.GetBool(Key+"Finished",false)) return;
            int number=SessionState.GetInt(Key+"Number",1);
            float sampleTime=Time.time-lastSampleTime;
            deadlocks=Mathf.Max(deadlocks,crowd!=null?crowd.DeadlockedCount:0);
            for(int i=0;i<agents.Length;i++)
            {
                float distance=Vector2.Distance(last[i],agents[i].Position); if(distance<2) travelled[i]+=distance; last[i]=agents[i].Position;
                if(motors[i]==null) {failure="3D motor missing";continue;}
                float y=motors[i].WorldPosition.y; minimumHeight=Mathf.Min(minimumHeight,y); maximumHeight=Mathf.Max(maximumHeight,y);
                if(number==2)
                { maxStairPlanar=Mathf.Max(maxStairPlanar,motors[i].ObservedStairPlanarSpeed); maxStairVertical=Mathf.Max(maxStairVertical,motors[i].ObservedStairVerticalSpeed); }
                if(sampleTime>.00001f)
                {
                    Vector3 current=motors[i].WorldPosition,delta=current-previousWorld[i];
                    previousWorld[i]=current;
                }
                if(agents[i].GetComponent<Rigidbody2D>().simulated) failure="2D physics active";
                if((agents[i].transform.localScale-Vector3.one).sqrMagnitude>.001f) failure="3D mannequin scale was squashed";
            }
            if(sampleTime>.00001f) lastSampleTime=Time.time;
            if(interior!=null)
            { seats=Mathf.Max(seats,interior.SeatOccupiedCount); stands=Mathf.Max(stands,interior.StandOccupiedCount); leans=Mathf.Max(leans,interior.LeanOccupiedCount); prepared=Mathf.Max(prepared,interior.PreparedCount); }
            float elapsed=Time.time-simulatedAt;
            if(number==5 && elapsed>10 && !reportedActivityPositions)
            {
                reportedActivityPositions=true;
                foreach(var action in activities)
                {
                    var agent=action.GetComponent<PassengerAiV2Agent>(); var motor=action.GetComponentInChildren<PassengerAiV23DMotor>();
                    File.AppendAllText(Result,"ACTIVITY "+agent.name+" kind="+action.SelectedKind+" using="+action.IsUsingActivity+" position="+agent.Position.ToString("F2")+" target="+agent.MovementTarget.ToString("F2")+" blocked="+(motor.LastBlockingCollider!=null?motor.LastBlockingCollider.name:"none")+"\n");
                }
            }
            if(elapsed<30 && EditorApplication.timeSinceStartup-started<55) return;
            string metrics="agents="+agents.Length+" deadlockMax="+deadlocks+" height="+minimumHeight.ToString("F2")+".."+maximumHeight.ToString("F2");
            if(elapsed<29) failure="Simulation did not advance sufficiently";
            string runtimeError=SessionState.GetString(Key+"RuntimeError","");
            if(!string.IsNullOrEmpty(runtimeError)) failure="Runtime console error: "+runtimeError;
            if(number==1)
            {
                float minimum=float.PositiveInfinity; foreach(float value in travelled) minimum=Mathf.Min(minimum,value);
                metrics+=" minimumTravel="+minimum.ToString("F2");
                if(minimum<5||deadlocks>0) failure="Corridor movement/deadlock threshold failed";
            }
            if(number==2)
            {
                metrics+=" completed="+(stair!=null?stair.CompletedTraversalCount:0);
                metrics+=" maxPlanar="+maxStairPlanar.ToString("F2")+" maxVertical="+maxStairVertical.ToString("F2");
                if(stair==null||stair.CompletedTraversalCount<2||maximumHeight<4.1f) failure="Stair traversals incomplete";
                if(maxStairPlanar>.82f||maxStairVertical>.95f) failure="Stair movement exceeded its speed limit";
            }
            if(number==3)
            {
                int tags=0,passed=0,invalid=0; foreach(var gate in gates) {tags+=gate.CardTagCount;passed+=gate.CompletedPassCount;invalid+=gate.InvalidPassAttemptCount;}
                metrics+=" tags="+tags+" passed="+passed+" invalid="+invalid;
                if(tags<4||passed<4||invalid>0) failure="Fare gate traversal failed";
            }
            if(number==4)
            {
                metrics+=" alighted="+(door!=null?door.AlightedCount:0)+" boarded="+(door!=null?door.BoardedCount:0);
                if(door==null||door.AlightedCount<1||door.BoardedCount<1||door.InvalidPassAttemptCount>0||door.BoardingBeforeAlightingClearCount>0||door.OutOfOrderBoardingCount>0)
                    failure="Door passage/priority failed";
            }
            if(number==5)
            {
                metrics+=" seat/stand/lean="+seats+"/"+stands+"/"+leans+" prepared="+prepared;
                if(interior==null||seats<1||stands<1||leans<1||interior.InvalidReservationCount>0) failure="Interior activities/reservations failed";
            }
            bool pass=string.IsNullOrEmpty(failure);
            File.AppendAllText(Result,(pass?"PASS":"FAIL")+" field="+number+" "+metrics+(pass?"":" reason="+failure)+"\n");
            Debug.Log("AI3D_FIELD_METRICS: "+number+" "+metrics+(pass?"":" FAIL "+failure));
            if(!pass) SessionState.SetBool(Key+"AnyFailure",true);
            SessionState.SetBool(Key+"Finished",true); EditorApplication.isPlaying=false;
        }
    }
}
#endif

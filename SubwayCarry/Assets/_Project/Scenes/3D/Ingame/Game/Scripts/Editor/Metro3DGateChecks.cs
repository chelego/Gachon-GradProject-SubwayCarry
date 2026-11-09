#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace SubwayCarry.Gameplay3D.Editor
{
    // Read-only diagnostics and an explicit Play regression. Runtime test input/funds
    // disappear on exit; no scene or PlayerPrefs are saved by this checker.
    [InitializeOnLoad]
    public static class Metro3DGateChecks
    {
        static double nextPoll;
        const string RunKey="SubwayCarry.GateCheck.Play";
        static Keyboard testKeyboard,previousKeyboard;
        static Metro3DWorld testWorld;
        static Metro3DFacility testGate;
        static double started;
        static int phase;
        static int cashBefore;
        static readonly StringBuilder playOutput=new StringBuilder();
        static string storedProgress;
        static string Folder => Path.Combine(Application.dataPath,"../Temp/ThreeDRebuild");
        static Metro3DGateChecks() { EditorApplication.update-=Poll; EditorApplication.update+=Poll; }
        static void Poll()
        {
            if(SessionState.GetBool(RunKey,false)) {
                if(EditorApplication.isPlaying&&!EditorApplication.isCompiling) RunPlayCheck();
                else if(!EditorApplication.isPlayingOrWillChangePlaymode) SessionState.SetBool(RunKey,false);
                return;
            }
            if(Application.isBatchMode||EditorApplication.timeSinceStartup<nextPoll||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextPoll=EditorApplication.timeSinceStartup+2;
            string request=Path.Combine(Folder,"gate-checks.request");
            if(!File.Exists(request)) return;
            string command=File.ReadAllText(request).Trim(); File.Delete(request);
            try {
                if(command=="PLAY") {
                    RunPlayRegression(); return;
                }
                File.WriteAllText(Path.Combine(Folder,"gate-checks.result"),Inspect());
            }
            catch(Exception ex) { File.WriteAllText(Path.Combine(Folder,"gate-checks.result"),"FAILED: "+ex); Debug.LogException(ex); }
        }
        [MenuItem("SubwayCarry/3D/Checks/Fare Gate Play Regression")]
        public static void RunPlayRegression()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=Metro3DBuilder.ScenePath||scene.isDirty) throw new InvalidOperationException("Play check requires the unchanged, saved 3D scene outside Play Mode.");
            phase=0; testWorld=null; testGate=null; testKeyboard=null; playOutput.Clear();
            SessionState.SetBool(RunKey,true); EditorApplication.EnterPlaymode();
        }
        static void RunPlayCheck()
        {
            try {
                if(phase==0) {
                    testWorld=UnityEngine.Object.FindFirstObjectByType<Metro3DWorld>();
                    if(testWorld==null||testWorld.session.Economy==null) return;
                    previousKeyboard=Keyboard.current;
                    testKeyboard=InputSystem.AddDevice<Keyboard>("Gate regression input"); testKeyboard.MakeCurrent();
                    storedProgress=PlayerPrefs.GetString("SubwayCarry.3D.Progress.v1");
                    testWorld.session.Accept();
                    var actor=testWorld.player;
                    var findTarget=typeof(Metro3DPlayer).GetMethod("FindTarget",BindingFlags.Instance|BindingFlags.NonPublic);
                    var clearPath=typeof(Metro3DPlayer).GetMethod("HasClearInteractionPath",BindingFlags.Instance|BindingFlags.NonPublic);
                    int approaches=0;
                    foreach(var gate in testWorld.facilities.Where(f=>f.kind==Metro3DFacilityKind.FareGate&&f.transform.parent==testWorld.stations[0])) foreach(int side in new[]{-1,1}) {
                        actor.TeleportForReturn(gate.transform.position+new Vector3(0,-.65f,side*1.05f));
                        actor.transform.rotation=Quaternion.Euler(0,side<0?0:180,0); actor.view.transform.localRotation=Quaternion.identity;
                        Physics.SyncTransforms(); findTarget.Invoke(actor,null);
                        if(actor.Target!=gate) throw new InvalidOperationException("Gate not selected from approach "+side+" at "+gate.transform.position);
                        var eye=actor.view.transform.position; var to=gate.transform.position-eye;
                        var wall=new GameObject("Temporary gate occlusion regression wall");
                        try {
                            var contact=wall.AddComponent<BoxCollider>(); contact.size=new Vector3(.4f,.4f,.06f);
                            wall.transform.position=eye+to*.55f; Physics.SyncTransforms();
                            if((bool)clearPath.Invoke(actor,new object[]{gate,to})) throw new InvalidOperationException("Interaction incorrectly sees through a wall.");
                        } finally { UnityEngine.Object.DestroyImmediate(wall); }
                        approaches++;
                    }
                    playOutput.AppendLine("PASS: "+approaches+" saved-scene gate approaches; self collider ignored; intervening walls still block.");
                    var economy=testWorld.session.Economy;
                    int savedCash=economy.CurrentEconomyState.CurrentCash;
                    // Establish both rejection and success conditions without changing saved progress.
                    economy.ApplyDelta(-savedCash);
                    var first=testWorld.facilities.First(f=>f.kind==Metro3DFacilityKind.FareGate);
                    testWorld.session.UseGate(first);
                    if(!first.blocker.enabled||economy.CurrentEconomyState.CurrentCash!=0||testWorld.session.Notice!="교통비가 부족합니다.") throw new InvalidOperationException("Insufficient fare did not keep the gate closed.");
                    playOutput.AppendLine("PASS: insufficient fare refused without charge. Original saved cash="+savedCash);
                    economy.ApplyDelta(testWorld.session.Order.outboundFare*2);
                    testGate=testWorld.facilities.Where(f=>f.kind==Metro3DFacilityKind.FareGate).OrderBy(f=>Mathf.Abs(f.transform.position.x)+Mathf.Abs(f.transform.position.z+5)).First();
                    var p=testGate.transform.position+new Vector3(0,-.65f,-1.05f);
                    testWorld.player.TeleportForReturn(p);
                    testWorld.player.view.transform.localRotation=Quaternion.identity;
                    cashBefore=testWorld.session.Economy.CurrentEconomyState.CurrentCash;
                    InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());
                    InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.W));
                    started=EditorApplication.timeSinceStartup; phase=5; return;
                }
                testKeyboard.MakeCurrent();
                if(phase==5&&EditorApplication.timeSinceStartup-started>.65) {
                    if(testWorld.player.transform.position.z>=testGate.transform.position.z-.20f||testWorld.session.Economy.CurrentEconomyState.CurrentCash!=cashBefore) throw new InvalidOperationException("Closed gate allowed passage or charged a fare without tagging.");
                    playOutput.AppendLine("PASS: closed gate physically blocks W movement before E.");
                    InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());
                    testWorld.player.TeleportForReturn(testGate.transform.position+new Vector3(0,-.65f,-1.05f));
                    testWorld.player.view.transform.localRotation=Quaternion.identity;
                    started=EditorApplication.timeSinceStartup; phase=1; return;
                }
                if(phase==1&&EditorApplication.timeSinceStartup-started>.3) {
                    playOutput.AppendLine("Target="+(testWorld.player.Target!=null?testWorld.player.Target.name:"none"));
                    var p=testWorld.player; var to=testGate.transform.position-p.view.transform.position;
                    playOutput.AppendLine("Approach player="+p.transform.position+" eye="+p.view.transform.position+" forward="+p.view.transform.forward+" dot="+Vector3.Dot(p.view.transform.forward,to.normalized)+" active="+testGate.gameObject.activeInHierarchy+" available="+testGate.Available+" blocked="+testWorld.session.Blocked);
                    foreach(var hit in Physics.RaycastAll(p.view.transform.position,to.normalized,to.magnitude,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
                        playOutput.AppendLine("Ray hit="+hit.collider.name+" dist="+hit.distance+" own="+hit.transform.IsChildOf(p.transform));
                    InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.E));
                    started=EditorApplication.timeSinceStartup; phase=2; return;
                }
                if(phase==2&&EditorApplication.timeSinceStartup-started>.2) {
                    InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());
                    started=EditorApplication.timeSinceStartup; phase=3; return;
                }
                if(phase==3&&EditorApplication.timeSinceStartup-started>.45) {
                    playOutput.AppendLine("Opened blocker="+testGate.blocker.enabled+" rotation="+testGate.anchor.localRotation.eulerAngles+" Notice="+testWorld.session.Notice);
                    InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.W));
                    started=EditorApplication.timeSinceStartup; phase=4; return;
                }
                if(phase==4&&EditorApplication.timeSinceStartup-started>1.4) {
                    InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());
                    float travel=testWorld.player.transform.position.z-testGate.transform.position.z;
                    int charged=cashBefore-testWorld.session.Economy.CurrentEconomyState.CurrentCash;
                    playOutput.AppendLine("Crossing position="+testWorld.player.transform.position+" beyond gate="+travel+" charged="+charged+" expected="+testWorld.session.Order.outboundFare);
                    if(travel<=.85f||charged!=testWorld.session.Order.outboundFare) throw new InvalidOperationException("E tag and W movement failed to cross with exactly one fare.");
                    testWorld.session.UseGate(testGate);
                    if(cashBefore-testWorld.session.Economy.CurrentEconomyState.CurrentCash!=charged) throw new InvalidOperationException("Re-tag charged the outbound fare twice.");
                    started=EditorApplication.timeSinceStartup; phase=6; return;
                }
                if(phase==6&&EditorApplication.timeSinceStartup-started>5.7) {
                    if(!testGate.blocker.enabled) throw new InvalidOperationException("Gate did not close after its passage interval.");
                    playOutput.AppendLine("PASS: repeat tag does not double-charge; gate closes after 5 seconds.");
                    if(PlayerPrefs.GetString("SubwayCarry.3D.Progress.v1")!=storedProgress) throw new InvalidOperationException("Stored progress changed during regression.");
                    EndPlayCheck("PASS: actual E-key tagging and W-key crossing; saved progress unchanged.\n"+playOutput);
                }
            } catch(Exception ex) { EndPlayCheck("FAILED: "+ex+"\n"+playOutput); }
        }
        static void EndPlayCheck(string result)
        {
            if(testKeyboard!=null) InputSystem.RemoveDevice(testKeyboard);
            if(previousKeyboard!=null&&previousKeyboard.added) previousKeyboard.MakeCurrent();
            File.WriteAllText(Path.Combine(Folder,"gate-checks.result"),result);
            SessionState.SetBool(RunKey,false); EditorApplication.ExitPlaymode();
        }
        static string Inspect()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=Metro3DBuilder.ScenePath) throw new InvalidOperationException("Expected the existing 3D delivery scene.");
            var world=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Metro3DWorld>(true)).Single();
            Physics.SyncTransforms();
            var output=new StringBuilder();
            foreach(var gate in world.facilities.Where(f=>f.kind==Metro3DFacilityKind.FareGate||f.kind==Metro3DFacilityKind.ExitGate)) {
                var center=gate.transform.position; float floor=center.y-.65f;
                output.AppendLine("GATE "+center+" kind="+gate.kind+" door="+(gate.gateDoor!=null)+" tripod="+(gate.gateDoor!=null&&gate.gateDoor.tripod));
                foreach(int side in new[]{-1,1}) {
                    var feet=new Vector3(center.x,floor,center.z+side*1.05f);
                    var eye=feet+Vector3.up*1.62f; var to=center-eye;
                    bool hit=Physics.Raycast(eye,to.normalized,out var obstruction,to.magnitude,~0,QueryTriggerInteraction.Ignore);
                    output.AppendLine("  APPROACH "+side+" eye hit="+(hit?obstruction.collider.name:"none")+" distance="+(hit?obstruction.distance:0));
                }
                var contacts=Physics.OverlapBox(new Vector3(center.x,floor+.88f,center.z),new Vector3(.275f,.84f,.9f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
                foreach(var contact in contacts) output.AppendLine("  CONTACT "+contact.name+" "+contact.GetType().Name+" bounds="+contact.bounds);
            }
            return output.ToString();
        }
    }
}
#endif

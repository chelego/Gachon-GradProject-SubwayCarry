#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SubwayCarry.Transit.Editor
{
    /// <summary>Runs the real CharacterController through the scene, using frame-driven Play mode physics.</summary>
    public static class GachonHubPlayVerifier
    {
        private static CharacterController body;
        private static GachonHubGate gate;
        private static readonly List<string> lines = new List<string>();
        private static Vector3[] route;
        private static int stage, waypoint, lastFrame, firstFrame;
        private static float stageTime, lastDistance, stalled;
        private static double started;
        private static string output;
        private static bool priorBackground;
        private static float dismountZ;
        private static bool checkedWalkBeforeTurn;
        public static bool Running { get; private set; }
        private static Vector3 AP(float x,float y,float z) => GachonHubSceneBuilder.ApproachPoint(new Vector3(x,y,z));

        public static void Start(string reportPath)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            body=UnityEngine.Object.FindFirstObjectByType<SubwayFirstPersonController>().GetComponent<CharacterController>();
            gate=GameObject.Find("FareGate_0").GetComponent<GachonHubGate>();
            output=reportPath;lines.Clear();stage=0;waypoint=0;stageTime=0;stalled=0;lastDistance=1000;
            firstFrame=lastFrame=Time.frameCount;started=EditorApplication.timeSinceStartup;
            priorBackground=Application.runInBackground;Application.runInBackground=true;
            Time.timeScale=1;Running=true;
            Place(AP(0,.08f,-9.5f));
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            lines.Add("GachonUniv_Hub real CharacterController Play mode validation");
            Vector3 exit=AP(0,0,-5.5f);
            Vector3 ascent=GameObject.Find("StairBottom").transform.position+Vector3.forward*(GachonHubSceneBuilder.AscentRun*.5f)-exit;ascent.y=0;
            float bearing=Vector3.SignedAngle(GachonHubSceneBuilder.ApproachRotation*Vector3.forward,ascent,Vector3.up);
            if(bearing<30 || bearing>65){Finish(false,"Ascent should be diagonally right of fare-gate exit: "+bearing);return;}
            lines.Add("PASS: staircase is diagonally right of the fare-gate exit, bearing="+bearing.ToString("F1")+" degrees");
            float lower=GachonHubSceneBuilder.AscentBottomZ;
            Vector3 toPassage=AP(0,0,0)-new Vector3(3.6f,0,lower-.04f);
            float leftBearing=Vector3.SignedAngle(Vector3.back,toPassage,Vector3.up);
            if(leftBearing> -35 || leftBearing< -135){Finish(false,"Passage must be LEFT when descending: "+leftBearing);return;}
            lines.Add("PASS: passage is LEFT of the descending visitor, signed bearing="+leftBearing.ToString("F1")+" degrees");
            if(lower-GachonHubSceneBuilder.EntryZ<3){Finish(false,"Insufficient walking space between escalator and left passage");return;}
            lines.Add("PASS: shorter ascent run="+GachonHubSceneBuilder.AscentRun.ToString("F1")+", lower end is "+(lower-GachonHubSceneBuilder.EntryZ).ToString("F1")+" metres before the passage centre");
            var tread=GameObject.Find("04_EscalatorsAndCampusStair").transform.Find("Walkable stone staircase/Stair tread 1").GetComponent<Renderer>();
            if(tread.bounds.size.x<5){Finish(false,"Staircase width was not expanded");return;}
            lines.Add("PASS: adjacent staircase tread width="+tread.bounds.size.x.ToString("F1")+" model metres");
            var plaza=GameObject.Find("Freedom square granite slab").GetComponent<Renderer>().bounds;
            if(plaza.size.z/plaza.size.x<2){Finish(false,"Freedom Plaza is not elongated");return;}
            lines.Add("PASS: Freedom court envelope="+plaza.size.x.ToString("F1")+" x "+plaza.size.z.ToString("F1")+" model metres");
        }
        private static void Place(Vector3 p)
        {
            body.enabled=false;body.transform.position=p;body.enabled=true;Physics.SyncTransforms();
        }
        private static void Next(int value)
        {stage=value;stageTime=0;stalled=0;lastDistance=1000;}
        private static void Tick()
        {
            if(!Running)return;
            if(!EditorApplication.isPlaying){Finish(false,"Play mode interrupted");return;}
            EditorApplication.QueuePlayerLoopUpdate();
            if(EditorApplication.timeSinceStartup-started>150){Finish(false,"Frame-driven verification timed out");return;}
            if(Time.frameCount==lastFrame)return;
            lastFrame=Time.frameCount;
            float dt=Mathf.Min(Time.deltaTime,.05f);stageTime+=dt;
            switch(stage)
            {
                case 0:
                    body.Move((GachonHubSceneBuilder.ApproachRotation*Vector3.forward*2+Vector3.down*2)*dt);
                    if(stageTime>1.5f)
                    {
                        if(GachonHubSceneBuilder.ApproachLocal(body.transform.position).z> -7.7f){Finish(false,"Closed fare gate did not block");return;}
                        lines.Add("PASS: closed fare gate blocks the visitor at "+body.transform.position);
                        gate.RequestPass();Next(1);
                    }
                    break;
                case 1:
                    if(stageTime>.75f)
                    {
                        if(!gate.IsOpen){Finish(false,"Card passage did not open");return;}
                        lines.Add("PASS: RequestPass opens both fare-gate leaves");
                        float x=GachonHubSceneBuilder.StairX;
                        route=new[]{AP(0,0,-5),AP(0,0,2),new Vector3(x,0,4),
                            new Vector3(x,0,GachonHubSceneBuilder.AscentBottomZ-.5f),new Vector3(x,5.1f,19),new Vector3(x,5.1f,30),new Vector3(3,5.1f,73)};
                        waypoint=0;Next(2);
                    }
                    break;
                case 2:
                    if(WalkRoute(dt))
                    {
                        if(body.transform.position.y<4.9f){Finish(false,"Campus landing height incorrect");return;}
                        lines.Add("PASS: fare gate -> connecting passage -> fountain atrium -> staircase -> outdoor plaza at "+body.transform.position);
                        float x=GachonHubSceneBuilder.StairX;
                        route=new[]{new Vector3(x,5.1f,30),new Vector3(x,5.1f,19),new Vector3(x,0,4),AP(0,0,2),AP(0,0,-3)};
                        waypoint=0;Next(3);
                    }
                    break;
                case 3:
                    if(WalkRoute(dt))
                    {
                        lines.Add("PASS: outdoor plaza -> stairs -> fountain atrium -> station passage");
                        Place(new Vector3(5.8f,.55f,GachonHubSceneBuilder.AscentBottomZ+1));Next(4);
                    }
                    break;
                case 4:
                    body.Move(Vector3.down*2*dt);
                    if(body.transform.position.z>GachonHubSceneBuilder.AscentTopZ+.04f)
                    {
                        if(body.transform.position.y<4.85f){Finish(false,"Up escalator height incorrect");return;}
                        lines.Add("PASS: UP escalator carries stationary visitor to campus at "+body.transform.position);
                        Place(new Vector3(3.6f,5.15f,16.5f));body.transform.rotation=Quaternion.Euler(0,180,0);Next(5);
                    }
                    else if(stageTime>45){Finish(false,"Up escalator stalled at "+body.transform.position);return;}
                    break;
                case 5:
                    body.Move(Vector3.down*2*dt);
                    if(body.transform.position.z<GachonHubSceneBuilder.AscentBottomZ-.04f)
                    {
                        lines.Add("PASS: DOWN escalator returns stationary visitor to atrium at "+body.transform.position);
                        int particles=0;
                        foreach(var p in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))particles+=p.particleCount;
                        if(particles==0){Finish(false,"Fountain particles are not advancing");return;}
                        lines.Add("PASS: fountain animation active, particle count="+particles);
                        body.transform.Rotate(0,-90,0);
                        dismountZ=body.transform.position.z;checkedWalkBeforeTurn=false;
                        route=new[]{new Vector3(3.6f,0,3.8f),AP(0,0,2),AP(0,0,-10)};
                        waypoint=0;Next(6);
                    }
                    else if(stageTime>45){Finish(false,"Down escalator stalled at "+body.transform.position);return;}
                    break;
                case 6:
                    if(Vector3.Distance(body.transform.position,gate.transform.position)<3)gate.RequestPass();
                    bool completed=WalkRoute(dt);
                    if(!Running)break;
                    if(!checkedWalkBeforeTurn && waypoint>0)
                    {
                        float walked=dismountZ-body.transform.position.z;
                        if(walked<3){Finish(false,"Visitor turned left too soon after dismounting");return;}
                        lines.Add("PASS: visitor walks "+walked.ToString("F1")+" metres forward after dismounting before turning LEFT");
                        checkedWalkBeforeTurn=true;
                    }
                    if(completed)
                    {
                        lines.Add("PASS: DOWN escalator -> dismount -> LEFT turn -> side-wall passage -> fare gate -> station at "+body.transform.position);
                        Finish(true,"All circulation checks passed.");
                    }
                    break;
            }
        }
        private static bool WalkRoute(float dt)
        {
            var target=route[waypoint];var delta=target-body.transform.position;delta.y=0;
            if(delta.magnitude<.22f)
            {waypoint++;lastDistance=1000;stalled=0;if(waypoint==route.Length)return true;return false;}
            if(delta.magnitude>=lastDistance-.002f)stalled+=dt;else stalled=0;
            lastDistance=delta.magnitude;
            if(stalled>3){Finish(false,"Route blocked at waypoint "+waypoint+" position="+body.transform.position);return false;}
            body.Move((delta.normalized*4+Vector3.down*3)*dt);
            return false;
        }
        private static void Finish(bool success,string note)
        {
            Running=false;EditorApplication.update-=Tick;
            Application.runInBackground=priorBackground;
            lines.Add((success?"PASS: ":"FAIL: ")+note);
            lines.Add("Advanced frames: "+(Time.frameCount-firstFrame));
            lines.Add("Elapsed seconds: "+(EditorApplication.timeSinceStartup-started).ToString("F1"));
            File.WriteAllLines(output,lines);
            if(success)Debug.Log(string.Join("\n",lines));else Debug.LogError(string.Join("\n",lines));
        }
    }
}
#endif




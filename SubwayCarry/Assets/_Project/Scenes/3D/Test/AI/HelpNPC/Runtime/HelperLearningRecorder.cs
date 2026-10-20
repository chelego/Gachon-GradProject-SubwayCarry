using System;
using System.Globalization;
using System.IO;
using Unity.MLAgents;
using UnityEngine;

namespace SubwayCarry.AI.HelpNPC
{
    // Opt-in camera recording of an actual external-trainer session, not inference.
    public sealed class HelperLearningRecorder : MonoBehaviour
    {
        public Camera cameraView;
        string folder;
        double started, nextFrame;
        int frames;
        RenderTexture target;
        Texture2D pixels;
        void Start()
        {
            foreach(string arg in Environment.GetCommandLineArgs())
                if(arg.StartsWith("--helper-record=",StringComparison.Ordinal))folder=arg.Substring(16);
            if(string.IsNullOrEmpty(folder)){enabled=false;return;}
            Directory.CreateDirectory(folder);
            target=new RenderTexture(1280,720,24);
            pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
        }
        void LateUpdate()
        {
            if(!Academy.Instance.IsCommunicatorOn||cameraView==null)return;
            double now=Time.realtimeSinceStartupAsDouble;
            if(started==0){started=now;nextFrame=now;}
            if(now-started>=60){Finish();enabled=false;return;}
            if(now<nextFrame)return;
            nextFrame=now+.1;
            var previous=cameraView.targetTexture;var active=RenderTexture.active;
            try {
                cameraView.targetTexture=target;cameraView.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply(false);
                File.WriteAllBytes(Path.Combine(folder,"Frame"+frames.ToString("D4")+".png"),pixels.EncodeToPNG());
                File.AppendAllText(Path.Combine(folder,"frames.csv"),frames+","+(now-started).ToString("F4",CultureInfo.InvariantCulture)+","+Academy.Instance.StepCount+Environment.NewLine);
                frames++;
            }finally{cameraView.targetTexture=previous;RenderTexture.active=active;}
        }
        void Finish()
        {
            if(string.IsNullOrEmpty(folder))return;
            File.WriteAllText(Path.Combine(folder,"recording.txt"),"External trainer connected; camera-only recording. Frames="+frames+"; elapsed="+(Time.realtimeSinceStartupAsDouble-started).ToString("F3",CultureInfo.InvariantCulture));
        }
        void OnDestroy(){Finish();if(target!=null)target.Release();if(pixels!=null)Destroy(pixels);}
    }
}

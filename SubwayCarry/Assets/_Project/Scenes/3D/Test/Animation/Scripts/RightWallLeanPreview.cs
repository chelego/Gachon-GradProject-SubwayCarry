using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Plays the saved .anim outputs against the same Generic rig used by MotionAuthoring.
    [ExecuteAlways]
    public sealed class RightWallLeanPreview : MonoBehaviour
    {
        public Animation player;
        public RenderPipelineAsset previewPipeline;
        RenderPipelineAsset previousPipeline;
        bool previousRunInBackground;
        void OnEnable()
        {
            previousPipeline = QualitySettings.renderPipeline;
            if (previewPipeline != null) QualitySettings.renderPipeline = previewPipeline;
            if (Application.isPlaying) { previousRunInBackground = Application.runInBackground; Application.runInBackground = true; }
        }
        void OnDisable()
        {
            if (QualitySettings.renderPipeline == previewPipeline) QualitySettings.renderPipeline = previousPipeline;
            if (Application.isPlaying) Application.runInBackground = previousRunInBackground;
        }
        public AnimationClip startClip, loopClip, exitClip;
        [Min(1)] public int loopRepeats = 3;
        [Range(.1f, 2f)] public float speed = 1f;
        public bool repeatSequence = true;
        public bool paused;
        public string Phase => phase == 0 ? "START" : phase == 1 ? "LOOP" : "EXIT";
        public float ClipTime => clipTime;
        public int CompletedLoops => completedLoops;
        int phase, completedLoops;
        float clipTime, holdTime;
        bool exitRequested;
        AnimationClip Current => phase == 0 ? startClip : phase == 1 ? loopClip : exitClip;

        void Start()
        {
            if (!Application.isPlaying) return;
            if (player == null || startClip == null || loopClip == null || exitClip == null)
            { Debug.LogError("Right wall lean preview is missing its rig or saved clips.", this); enabled = false; return; }
            foreach (var clip in new[] {startClip, loopClip, exitClip}) player.AddClip(clip, clip.name);
            player.playAutomatically = false;
            player.cullingType = AnimationCullingType.AlwaysAnimate;
            Restart();
        }
        public void Restart()
        {
            phase = completedLoops = 0; clipTime = holdTime = 0; exitRequested = paused = false;
            Sample();
        }
        public void RequestExit() { exitRequested = true; }
        void Update()
        {
            if (!Application.isPlaying) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) paused = !paused;
                if (keyboard.rKey.wasPressedThisFrame) Restart();
                if (keyboard.eKey.wasPressedThisFrame) RequestExit();
            }
            if (paused) return;
            if (phase == 2 && clipTime >= exitClip.length)
            {
                holdTime += Time.deltaTime * speed;
                if (repeatSequence && holdTime >= 1f) Restart();
                return;
            }
            clipTime += Time.deltaTime * speed;
            while (clipTime >= Current.length)
            {
                clipTime -= Current.length;
                if (phase == 0) phase = 1;
                else if (phase == 1)
                {
                    completedLoops++;
                    if (exitRequested || completedLoops >= loopRepeats) phase = 2;
                }
                else { clipTime = exitClip.length; break; }
            }
            Sample();
        }
        void Sample()
        {
            if (player == null || Current == null) return;
            player.Play(Current.name);
            var state = player[Current.name];
            state.speed = 0; state.wrapMode = WrapMode.ClampForever; state.time = clipTime;
            player.Sample();
        }
        void OnGUI()
        {
            if (!Application.isPlaying) return;
            GUILayout.BeginArea(new Rect(18, 18, 340, 220), GUI.skin.box);
            GUILayout.Label("RIGHT WALL LEAN  |  60 FPS");
            GUILayout.Label(Phase + "   " + Mathf.RoundToInt(clipTime * 60) + "F   |   loops " + completedLoops + " / " + loopRepeats);
            GUILayout.Label("Stand > lean right > breathe > stand");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(paused ? "Resume [Space]" : "Pause [Space]")) paused = !paused;
            if (GUILayout.Button("Restart [R]")) Restart();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Exit after this loop [E]")) RequestExit();
            repeatSequence = GUILayout.Toggle(repeatSequence, "Repeat full sequence");
            GUILayout.Label("Speed " + speed.ToString("F2") + "x");
            speed = GUILayout.HorizontalSlider(speed, .1f, 2f);
            GUILayout.EndArea();
        }
    }
}

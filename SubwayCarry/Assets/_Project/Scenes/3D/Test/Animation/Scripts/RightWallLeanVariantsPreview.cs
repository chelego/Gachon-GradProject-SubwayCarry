using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    [ExecuteAlways]
    public sealed class RightWallLeanVariantsPreview : MonoBehaviour
    {
        public GameObject mannequin;
        public AnimationClip crossedArmsClip, phoneClip;
        public GameObject phone;
        public RenderPipelineAsset previewPipeline;
        public int selected;
        public bool paused;
        public bool repeat = true;
        [Range(.1f, 2f)] public float speed = 1f;
        public float clipTime;
        public int completedSequences;
        RenderPipelineAsset previousPipeline;
        bool previousBackground;
        public AnimationClip Current => selected == 0 ? crossedArmsClip : phoneClip;
        public string Activity => selected == 0 ? "CROSS ARMS" : "USE PHONE";
        void OnEnable()
        {
            previousPipeline = QualitySettings.renderPipeline;
            if (previewPipeline != null) QualitySettings.renderPipeline = previewPipeline;
            if (Application.isPlaying)
            {
                previousBackground = Application.runInBackground;
                Application.runInBackground = true;
            }
        }
        void OnDisable()
        {
            if (QualitySettings.renderPipeline == previewPipeline) QualitySettings.renderPipeline = previousPipeline;
            if (Application.isPlaying) Application.runInBackground = previousBackground;
        }
        void Start()
        {
            if (!Application.isPlaying) return;
            if (mannequin == null || crossedArmsClip == null || phoneClip == null)
            {
                Debug.LogError("Assign the mannequin and both saved variant clips.", this);
                enabled = false;
                return;
            }
            Select(0);
        }
        public void Select(int index)
        {
            selected = Mathf.Clamp(index, 0, 1);
            clipTime = 0;
            paused = false;
            Sample();
        }
        public void Sample()
        {
            if (mannequin == null || Current == null) return;
            clipTime = Mathf.Clamp(clipTime, 0, Current.length);
            Current.SampleAnimation(mannequin, clipTime);
            if (phone != null) phone.SetActive(selected == 1 && clipTime >= 3.5f && clipTime < 10.85f);
        }
        void Update()
        {
            if (!Application.isPlaying) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) Select(0);
                if (keyboard.digit2Key.wasPressedThisFrame) Select(1);
                if (keyboard.spaceKey.wasPressedThisFrame) paused = !paused;
                if (keyboard.rKey.wasPressedThisFrame) Select(selected);
            }
            if (paused || Current == null) return;
            clipTime += Time.deltaTime * speed;
            if (clipTime > Current.length)
            {
                completedSequences++;
                if (repeat) clipTime %= Current.length;
                else { clipTime = Current.length; paused = true; }
            }
            Sample();
        }
        void OnGUI()
        {
            if (!Application.isPlaying || Current == null) return;
            GUILayout.BeginArea(new Rect(18, 18, 360, 200), GUI.skin.box);
            GUILayout.Label("RIGHT WALL LEAN VARIANTS | 60 FPS");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cross arms [1]")) Select(0);
            if (GUILayout.Button("Use phone [2]")) Select(1);
            GUILayout.EndHorizontal();
            GUILayout.Label(Activity + " | " + Mathf.RoundToInt(clipTime * 60) + " / " + Mathf.RoundToInt(Current.length * 60) + "F");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(paused ? "Resume [Space]" : "Pause [Space]")) paused = !paused;
            if (GUILayout.Button("Restart [R]")) Select(selected);
            repeat = GUILayout.Toggle(repeat, "Repeat");
            GUILayout.EndHorizontal();
            float seek = GUILayout.HorizontalSlider(clipTime, 0, Current.length);
            if (Mathf.Abs(seek - clipTime) > .001f) { clipTime = seek; paused = true; Sample(); }
            GUILayout.Label("Speed " + speed.ToString("F2") + "x | completed " + completedSequences);
            speed = GUILayout.HorizontalSlider(speed, .1f, 2f);
            GUILayout.EndArea();
        }
    }
}
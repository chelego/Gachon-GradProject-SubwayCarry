using System.Collections;
using UnityEngine;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animation))]
    public sealed class AnimationLabSequencePreview : MonoBehaviour
    {
        [SerializeField] private AnimationClip enterClip;
        [SerializeField] private AnimationClip loopClip;
        [SerializeField] private AnimationClip exitClip;
        [SerializeField, Min(0.5f)] private float loopPreviewSeconds = 5f;
        [SerializeField, Min(0f)] private float seatedPauseSeconds = 1f;
        [SerializeField, Range(0f, 0.5f)] private float crossFadeSeconds = 0.12f;
        [SerializeField] private string activityLabel = "Seated activity";

        private Animation animationPlayer;
        private Coroutine sequence;
        private string phase = "Ready";
        private bool paused;

        public void Configure(
            AnimationClip enter,
            AnimationClip loop,
            AnimationClip exit,
            float loopSeconds = 5f,
            string label = "Seated activity")
        {
            enterClip = enter;
            loopClip = loop;
            exitClip = exit;
            loopPreviewSeconds = Mathf.Max(0.5f, loopSeconds);
            activityLabel = string.IsNullOrWhiteSpace(label) ? "Seated activity" : label;
        }

        private void Awake()
        {
            animationPlayer = GetComponent<Animation>();
            animationPlayer.playAutomatically = false;
            Register(enterClip, WrapMode.Once);
            Register(loopClip, WrapMode.Loop);
            Register(exitClip, WrapMode.ClampForever);
        }

        private void Start()
        {
            Restart();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                Restart();
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                paused = !paused;
                foreach (AnimationState state in animationPlayer)
                {
                    state.speed = paused ? 0f : 1f;
                }
            }
        }

        private void Restart()
        {
            paused = false;
            if (sequence != null)
            {
                StopCoroutine(sequence);
            }

            animationPlayer.Stop();
            sequence = StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            while (true)
            {
                phase = "Enter";
                animationPlayer.Play(enterClip.name);
                yield return WaitForPlayback(enterClip.length);

                phase = "Loop";
                animationPlayer.CrossFade(loopClip.name, crossFadeSeconds);
                yield return WaitForPlayback(loopPreviewSeconds);

                phase = "Exit";
                animationPlayer.CrossFade(exitClip.name, crossFadeSeconds);
                yield return WaitForPlayback(exitClip.length);

                phase = "Base seated pose";
                yield return WaitForPlayback(seatedPauseSeconds);
            }
        }

        private IEnumerator WaitForPlayback(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (!paused)
                {
                    elapsed += Time.deltaTime;
                }

                yield return null;
            }
        }

        private void Register(AnimationClip clip, WrapMode wrapMode)
        {
            if (clip == null)
            {
                return;
            }

            if (animationPlayer.GetClip(clip.name) == null)
            {
                animationPlayer.AddClip(clip, clip.name);
            }

            animationPlayer[clip.name].wrapMode = wrapMode;
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(18f, 18f, 330f, 70f), GUIContent.none);
            GUI.Label(new Rect(32f, 28f, 300f, 22f), activityLabel + " animation set preview");
            GUI.Label(new Rect(32f, 50f, 300f, 22f), phase + "   |   R: restart   P: pause");
        }
    }
}

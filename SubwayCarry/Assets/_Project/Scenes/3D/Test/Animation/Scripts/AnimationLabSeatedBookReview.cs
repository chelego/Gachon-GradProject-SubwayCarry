using System;
using UnityEngine;
namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Exact-source opt-in for the seated reading review. The prop never controls the skeleton.
    public sealed class AnimationLabSeatedBookReview : ScriptableObject
    {
        public const string ResourceName = "SeatedBookReview";
        public AnimationLabAuthoredMotion enter, hold, exit;
        public GameObject bookPrefab;
        public Vector3 bookHandPosition, restHipsPosition, readHipsPosition;
        public Quaternion bookHandRotation = Quaternion.identity, restHipsRotation = Quaternion.identity, readHipsRotation = Quaternion.identity;
        public Vector3 leftHandPosition, rightHandPosition;
        public Quaternion leftHandRotation = Quaternion.identity, rightHandRotation = Quaternion.identity;
        public int takeFrame = 16, storeFrame = 48;
        public float halfWidth = .17f, height = .25f, fold = 10f, thickness = .0096f;
        public int Role(AnimationLabAuthoredMotion source) => source == null ? -1 : enter != null && source.id == enter.id ? 0 : hold != null && source.id == hold.id ? 1 : exit != null && source.id == exit.id ? 2 : -1;
        public static Visual Create(AnimationLabAuthoredMotion source, GameObject actor)
        { var d = Resources.Load<AnimationLabSeatedBookReview>(ResourceName); return d != null && d.Role(source) >= 0 ? new Visual(d, actor, null) : null; }
        public static Visual Create(GameObject actor, Animation player, AnimationClip enter, AnimationClip hold, AnimationClip exit)
        {
            var d = Resources.Load<AnimationLabSeatedBookReview>(ResourceName);
            return d != null && d.enter != null && d.hold != null && d.exit != null && d.enter.generatedClip == enter && d.hold.generatedClip == hold && d.exit.generatedClip == exit ? new Visual(d, actor, player) : null;
        }
        public float Surface(float x) => Mathf.Abs(x) * Mathf.Tan(fold * Mathf.Deg2Rad) - .004f * Mathf.Exp(-Mathf.Abs(x) * 45);
        public sealed class Visual : IDisposable
        {
            readonly AnimationLabSeatedBookReview data; readonly Transform hand, hips; readonly GameObject root; readonly AnimationState[] states;
            public GameObject Book => root;
            public Visual(AnimationLabSeatedBookReview data, GameObject actor, Animation player)
            {
                this.data = data; hand = actor.transform.Find(data.hold.humanPaths[(int)HumanBodyBones.RightHand]); hips = actor.transform.Find(data.hold.humanPaths[(int)HumanBodyBones.Hips]);
                if (hand == null || hips == null || data.bookPrefab == null) throw new InvalidOperationException("Seated book binding is incomplete");
                root = UnityEngine.Object.Instantiate(data.bookPrefab); root.name = "Seated book prop"; root.hideFlags = HideFlags.DontSave;
                foreach (var c in root.GetComponentsInChildren<Collider>()) c.enabled = false;
                if (player != null) states = new[] { player[data.enter.generatedClip.name], player[data.hold.generatedClip.name], player[data.exit.generatedClip.name] };
                Apply(0, 0);
            }
            public void Playback()
            {
                if (states == null) return; int role = -1; float weight = -1;
                for (int i = 0; i < states.Length; i++) if (states[i] != null && states[i].enabled && states[i].weight > weight) { role = i; weight = states[i].weight; }
                if (role < 0) return; var s = role == 0 ? data.enter : role == 1 ? data.hold : data.exit;
                float t = s.loop ? Mathf.Repeat(states[role].time, s.Duration) : Mathf.Clamp(states[role].time, 0, s.Duration); Apply(role, t * s.sampleRate);
            }
            public void Apply(AnimationLabAuthoredMotion source, float frame) => Apply(data.Role(source), frame);
            public void Apply(int role, float frame)
            {
                if (role < 0 || root == null) return;
                bool held = role == 1 || role == 0 && frame >= data.takeFrame || role == 2 && frame < data.storeFrame;
                root.transform.SetPositionAndRotation(held ? hand.TransformPoint(data.bookHandPosition) : hips.TransformPoint(data.restHipsPosition), held ? hand.rotation * data.bookHandRotation : hips.rotation * data.restHipsRotation);
            }
            public void Dispose() { if (root == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(root); else UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}

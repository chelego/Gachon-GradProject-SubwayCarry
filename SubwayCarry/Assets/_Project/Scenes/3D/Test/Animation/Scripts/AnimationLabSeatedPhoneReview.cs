using System;
using UnityEngine;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    // Optional, exact-source binding for the seated phone review; never changes bones or Motor ownership.
    public sealed class AnimationLabSeatedPhoneReview : ScriptableObject
    {
        public const string ResourceName = "SeatedPhoneReview";
        public AnimationLabAuthoredMotion enter, hold, exit;
        public GameObject phonePrefab, pocketPrefab;
        public Vector3 phoneHandPosition, pocketHipsPosition, storedPhonePosition;
        public Quaternion phoneHandRotation = Quaternion.identity, pocketHipsRotation = Quaternion.identity, storedPhoneRotation = Quaternion.identity;
        public int takeFrame = 16, storeFrame = 36;

        public void HandAnchor(int role, float frame, out Vector3 position, out Quaternion rotation)
        { position = phoneHandPosition; rotation = phoneHandRotation; }

        public int Role(AnimationLabAuthoredMotion source)
        {
            if (source == null) return -1;
            return enter != null && source.id == enter.id ? 0 : hold != null && source.id == hold.id ? 1 : exit != null && source.id == exit.id ? 2 : -1;
        }
        public static Visual Create(AnimationLabAuthoredMotion source, GameObject actor)
        {
            var data = Resources.Load<AnimationLabSeatedPhoneReview>(ResourceName);
            return data != null && data.Role(source) >= 0 ? new Visual(data, actor, null) : null;
        }
        public static Visual Create(GameObject actor, Animation player, AnimationClip enter, AnimationClip hold, AnimationClip exit)
        {
            var data = Resources.Load<AnimationLabSeatedPhoneReview>(ResourceName);
            return data != null && data.enter != null && data.hold != null && data.exit != null &&
                data.enter.generatedClip == enter && data.hold.generatedClip == hold && data.exit.generatedClip == exit ? new Visual(data, actor, player) : null;
        }
        public sealed class Visual : IDisposable
        {
            readonly AnimationLabSeatedPhoneReview data;
            readonly Transform hand, hips;
            readonly GameObject root, phone, pocket;
            readonly AnimationState[] states;
            public GameObject Phone => phone;
            public Visual(AnimationLabSeatedPhoneReview data, GameObject actor, Animation player)
            {
                this.data = data;
                var source = data.hold;
                hand = actor.transform.Find(source.humanPaths[(int)HumanBodyBones.RightHand]);
                hips = actor.transform.Find(source.humanPaths[(int)HumanBodyBones.Hips]);
                if (hand == null || hips == null || data.phonePrefab == null) throw new InvalidOperationException("Seated phone binding is incomplete");
                // Outside the rig hierarchy: authoring's bone count and rig signature stay identical.
                root = new GameObject("Seated phone props") { hideFlags = HideFlags.DontSave };
                phone = UnityEngine.Object.Instantiate(data.phonePrefab, root.transform, false);
                if (data.pocketPrefab != null) pocket = UnityEngine.Object.Instantiate(data.pocketPrefab, root.transform, false);
                foreach (var collider in root.GetComponentsInChildren<Collider>()) collider.enabled = false;
                if (player != null) states = new[] { player[data.enter.generatedClip.name], player[data.hold.generatedClip.name], player[data.exit.generatedClip.name] };
                Apply(0, 0);
            }
            public void Apply(AnimationLabAuthoredMotion source, float frame) => Apply(data.Role(source), frame);
            public void Playback()
            {
                if (states == null) return;
                int selected = -1; float weight = -1;
                for (int i = 0; i < states.Length; i++) if (states[i] != null && states[i].enabled && states[i].weight > weight) { selected = i; weight = states[i].weight; }
                if (selected < 0) return;
                var source = selected == 0 ? data.enter : selected == 1 ? data.hold : data.exit;
                float seconds = source.loop ? Mathf.Repeat(states[selected].time, source.Duration) : Mathf.Clamp(states[selected].time, 0, source.Duration);
                Apply(selected, seconds * source.sampleRate);
            }
            public void Apply(int role, float frame)
            {
                if (role < 0 || phone == null) return;
                if (pocket != null) pocket.transform.SetPositionAndRotation(hips.TransformPoint(data.pocketHipsPosition), hips.rotation * data.pocketHipsRotation);
                bool inHand = role == 1 || role == 0 && frame >= data.takeFrame || role == 2 && frame < data.storeFrame;
                if (inHand) { data.HandAnchor(role, frame, out var position, out var rotation); phone.transform.SetPositionAndRotation(hand.TransformPoint(position), hand.rotation * rotation); }
                else
                {
                    var parent = pocket != null ? pocket.transform : hips;
                    phone.transform.SetPositionAndRotation(parent.TransformPoint(data.storedPhonePosition), parent.rotation * data.storedPhoneRotation);
                }
            }
            public void Dispose()
            {
                if (root == null) return;
                if (Application.isPlaying) UnityEngine.Object.Destroy(root); else UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}

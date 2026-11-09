using System;
using System.Collections.Generic;
using SubwayCarry.AI.V2;
using SubwayCarry.Gameplay3D.AnimationLab;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public enum Metro3DSeatedAction { Phone, Book, Doze, FoldArms, CrossLegs }

    [Serializable]
    public sealed class Metro3DSeatedActionSource
    {
        public Metro3DSeatedAction action;
        public AnimationLabAuthoredMotion enter, hold, exit;
    }

    // One pose library per world. Each rider owns only its choice, time, and bone references.
    internal sealed class Metro3DSeatedActionLibrary
    {
        internal sealed class Sequence
        {
            internal Metro3DSeatedActionSource source;
            internal PoseSamples[] poses;
            internal int[] animatedBones;
        }

        internal sealed class PoseSamples
        {
            readonly Quaternion[][] frames;
            readonly int rate;
            internal readonly float duration;

            internal PoseSamples(AnimationLabAuthoredMotion source)
            {
                rate = source.sampleRate;
                duration = source.Duration;
                frames = new Quaternion[source.lastFrame + 1][];
                for (int frame = 0; frame < frames.Length; frame++)
                    frames[frame] = source.Evaluate(frame, false).rotations;
            }

            internal Quaternion Rotation(int bone, float time)
            {
                float frame = Mathf.Clamp(time * rate, 0, frames.Length - 1);
                int left = (int)frame;
                return Quaternion.Slerp(frames[left][bone], frames[Mathf.Min(left + 1, frames.Length - 1)][bone], frame - left);
            }
        }

        internal readonly List<Sequence> sequences = new();
        internal readonly AnimationLabSeatedPhoneReview phone;
        internal readonly AnimationLabSeatedBookReview book;
        internal readonly string[] paths;

        internal Metro3DSeatedActionLibrary(Metro3DSeatedActionSource[] sources)
        {
            if (sources == null || sources.Length == 0) return;
            phone = Resources.Load<AnimationLabSeatedPhoneReview>(AnimationLabSeatedPhoneReview.ResourceName);
            book = Resources.Load<AnimationLabSeatedBookReview>(AnimationLabSeatedBookReview.ResourceName);
            foreach (var source in sources)
            {
                if (source == null || !Valid(source.enter) || !Valid(source.hold) || !Valid(source.exit)
                    || !source.hold.loop || source.enter.loop || source.exit.loop
                    || !Matches(source.enter, source.hold) || !Matches(source.enter, source.exit)
                    || paths != null && !SamePaths(paths, source.enter.paths)) continue;
                if (source.action == Metro3DSeatedAction.Phone && (phone == null || phone.phonePrefab == null ||
                    phone.enter != source.enter || phone.hold != source.hold || phone.exit != source.exit)) continue;
                if (source.action == Metro3DSeatedAction.Book && (book == null || book.bookPrefab == null ||
                    book.enter != source.enter || book.hold != source.hold || book.exit != source.exit)) continue;

                paths ??= source.enter.paths;
                var bones = new List<int>();
                for (int i = source.enter.hipsIndex + 1; i < paths.Length; i++)
                {
                    // The existing seat solver owns pelvis position and the planted foot.
                    // The authored right leg is the one lifted in the leg-cross sequence.
                    if (paths[i].Contains("/DEF-spine.") ||
                        source.action == Metro3DSeatedAction.CrossLegs && paths[i].Contains("/DEF-thigh.R"))
                        bones.Add(i);
                }
                sequences.Add(new Sequence
                {
                    source = source,
                    poses = new[] { new PoseSamples(source.enter), new PoseSamples(source.hold), new PoseSamples(source.exit) },
                    animatedBones = bones.ToArray()
                });
            }
        }

        static bool Valid(AnimationLabAuthoredMotion source)
            => source != null && source.generatedClip != null && source.keys.Count > 0 && source.Validate(out _);

        static bool Matches(AnimationLabAuthoredMotion a, AnimationLabAuthoredMotion b)
            => a.rigSignature == b.rigSignature && SamePaths(a.paths, b.paths);

        static bool SamePaths(string[] a, string[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }

    internal sealed class Metro3DSeatedPastime
    {
        const float BlendTime = .20f;
        const float MinimumHold = 4;
        enum Phase { None = -1, Enter, Hold, Exit }
        readonly Metro3DPassenger actor;
        readonly Metro3DSeatedActionLibrary library;
        readonly Transform[] bones;
        readonly System.Random random;
        Metro3DSeatedActionLibrary.Sequence sequence;
        AnimationLabSeatedPhoneReview.Visual phone;
        AnimationLabSeatedBookReview.Visual book;
        Phase phase = Phase.None, previousPhase;
        float clock, previousTime, previousWeight, holdSeconds, chooseAt;
        internal string Label => sequence == null ? "" : sequence.source.action switch
        {
            Metro3DSeatedAction.Phone => "폰 보기",
            Metro3DSeatedAction.Book => "독서",
            Metro3DSeatedAction.Doze => "졸기",
            Metro3DSeatedAction.FoldArms => "팔짱",
            _ => "다리 꼬기"
        };
        internal string Stage => phase switch { Phase.Enter => "시작", Phase.Hold => "유지", Phase.Exit => "풀기", _ => "" };
        internal bool Active => phase != Phase.None;
        internal bool NeedsLegRoom => Active && phase != Phase.Exit && sequence.source.action == Metro3DSeatedAction.CrossLegs;

        internal Metro3DSeatedPastime(Metro3DPassenger actor, Metro3DSeatedActionLibrary library)
        {
            this.actor = actor;
            this.library = library;
            random = new System.Random(unchecked(actor.index * 16777619 + 31991));
            chooseAt = Time.time + Range(1.4f, 4.5f);
            if (library.paths == null) return;
            bones = new Transform[library.paths.Length];
            for (int i = 1; i < bones.Length; i++)
            {
                bones[i] = actor.transform.Find(library.paths[i]);
                if (bones[i] == null) { bones = null; break; }
            }
        }

        internal void Choose(float available, Metro3DCrowd crowd)
        {
            if (bones == null || available < MinimumHold || Time.time < chooseAt) return;
            var personality = actor.Journey.Personality;
            float density = crowd.LocalDensity(actor.transform.position, actor.transform.parent);
            bool canCrossLegs = density < .55f && crowd.SpaceFree(actor.Seat.anchor.position, actor, .57f);

            // Reserve a neutral option. A rider may choose to leave their hands free.
            float total = .54f;
            Metro3DSeatedActionLibrary.Sequence selected = null;
            foreach (var candidate in library.sequences)
            {
                var kind = candidate.source.action;
                float minimum = candidate.poses[0].duration + candidate.poses[2].duration + MinimumHold;
                if (available < minimum || kind == Metro3DSeatedAction.CrossLegs && !canCrossLegs
                    || kind == Metro3DSeatedAction.Doze && available < 13) continue;
                float weight = Mathf.Max(.06f, Preference(kind, personality, density, available));
                total += weight;
                if (random.NextDouble() * total < weight) selected = candidate;
            }
            if (selected == null)
            {
                chooseAt = Time.time + Range(5, 9);
                return;
            }
            sequence = selected;
            float room = available - sequence.poses[0].duration - sequence.poses[2].duration;
            float maxHold = sequence.source.action switch
            {
                Metro3DSeatedAction.Book => 17,
                Metro3DSeatedAction.Doze => 18,
                Metro3DSeatedAction.Phone => 14,
                _ => 11
            };
            holdSeconds = Mathf.Min(room, Range(MinimumHold + 2, maxHold) * Mathf.Lerp(.85f, 1.15f, personality.Patience));
            ChangePhase(Phase.Enter);
        }

        float Preference(Metro3DSeatedAction action, PassengerAiV2RuntimePersonality p, float density, float available)
        {
            // Stable per-rider interests complement the existing social and physical traits.
            uint bits = unchecked((uint)actor.index * 747796405u + (uint)action * 2891336453u);
            bits = (bits ^ (bits >> 16)) * 2246822519u;
            float interest = (bits & 1023u) / 1023f;
            float weight = action switch
            {
                Metro3DSeatedAction.Phone => .36f + p.SelfInterest * .32f + p.CrowdTolerance * .14f,
                Metro3DSeatedAction.Book => .12f + p.Patience * .52f + p.PersonalSpace * .12f - density * .32f,
                Metro3DSeatedAction.Doze => .04f + p.FatigueSensitivity * .62f + (1 - p.Mobility) * .21f
                    + Mathf.InverseLerp(13, 34, available) * .28f - density * .36f,
                Metro3DSeatedAction.FoldArms => .15f + p.PersonalSpace * .32f + density * .30f,
                _ => .12f + p.Mobility * .30f + p.Patience * .13f - density * .48f
            };
            return weight * Mathf.Lerp(.25f, 2.0f, interest * interest);
        }

        internal void Tick(float dt)
        {
            if (!Active) return;
            clock += dt;
            if (phase == Phase.Enter && clock >= sequence.poses[0].duration) ChangePhase(Phase.Hold);
            else if (phase == Phase.Hold && clock >= holdSeconds) ChangePhase(Phase.Exit);
            else if (phase == Phase.Exit && clock >= sequence.poses[2].duration) Clear();
        }

        internal bool Release()
        {
            if (!Active) return false;
            if (phase != Phase.Exit) ChangePhase(Phase.Exit);
            return true;
        }

        void ChangePhase(Phase next)
        {
            previousTime = SampleTime();
            previousWeight = phase == Phase.Enter ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(clock / BlendTime)) : 1;
            previousPhase = phase;
            phase = next;
            clock = 0;
        }

        float SampleTime()
        {
            if (!Active) return 0;
            float duration = sequence.poses[(int)phase].duration;
            return phase == Phase.Hold ? Mathf.Repeat(clock, duration) : Mathf.Min(clock, duration);
        }

        internal void ApplyPose()
        {
            if (!Active) return;
            float time = SampleTime();
            var sample = sequence.poses[(int)phase];
            float blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(clock / BlendTime));
            float release = phase == Phase.Exit
                ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((sample.duration - clock) / BlendTime)) : 1;
            foreach (int bone in sequence.animatedBones)
            {
                Quaternion neutral = bones[bone].localRotation;
                Quaternion from = previousPhase == Phase.None ? neutral
                    : Quaternion.Slerp(neutral, sequence.poses[(int)previousPhase].Rotation(bone, previousTime), previousWeight);
                Quaternion pose = Quaternion.Slerp(from, sample.Rotation(bone, time), blend);
                bones[bone].localRotation = Quaternion.Slerp(neutral, pose, release);
            }
            ApplyProp(time);
        }

        void ApplyProp(float time)
        {
            var source = phase == Phase.Enter ? sequence.source.enter : phase == Phase.Hold ? sequence.source.hold : sequence.source.exit;
            float frame = time * source.sampleRate;
            if (sequence.source.action == Metro3DSeatedAction.Phone)
            {
                phone ??= new AnimationLabSeatedPhoneReview.Visual(library.phone, actor.gameObject, null);
                if (StoppedBeforePickup(library.phone.takeFrame)) phone.Apply(0, 0);
                else phone.Apply((int)phase, frame);
            }
            else if (sequence.source.action == Metro3DSeatedAction.Book)
            {
                book ??= new AnimationLabSeatedBookReview.Visual(library.book, actor.gameObject, null);
                if (StoppedBeforePickup(library.book.takeFrame)) book.Apply(0, 0);
                else book.Apply((int)phase, frame);
            }
        }

        bool StoppedBeforePickup(int takeFrame)
            => phase == Phase.Exit && previousPhase == Phase.Enter && previousTime * sequence.source.enter.sampleRate < takeFrame;

        internal void Clear()
        {
            if (!Active) return;
            phone?.Dispose();
            book?.Dispose();
            phone = null;
            book = null;
            phase = Phase.None;
            sequence = null;
            chooseAt = Time.time + Range(5, 11);
        }

        float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}

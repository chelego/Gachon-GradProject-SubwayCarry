using System;
using SubwayCarry.Gameplay3D.AnimationLab;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public sealed class Metro3DWaitGestures : ScriptableObject
    {
        public AnimationLabAuthoredMotion weightShift, lookAlongPlatform;

        internal sealed class Pose
        {
            readonly Metro3DSeatedActionLibrary.PoseSamples samples;
            readonly int[] indices;
            readonly Quaternion[] neutral;
            internal float Length => samples.duration;

            internal Pose(AnimationLabAuthoredMotion source, string[] bones)
            {
                indices = Array.ConvertAll(bones, name =>
                    Array.FindIndex(source.paths, path => path.EndsWith("/" + name, StringComparison.Ordinal)));
                samples = new Metro3DSeatedActionLibrary.PoseSamples(source);
                neutral = Array.ConvertAll(indices, i => i < 0 ? Quaternion.identity : samples.Rotation(i, 0));
            }

            internal Quaternion Delta(int bone, float time)
                => indices[bone] < 0 ? Quaternion.identity : Quaternion.Inverse(neutral[bone]) * samples.Rotation(indices[bone], time);
        }

        internal static readonly string[] Bones = { "DEF-spine.001", "DEF-spine.002", "DEF-neck", "DEF-head" };
        internal Pose Weight { get; private set; }
        internal Pose Look { get; private set; }

        internal static Metro3DWaitGestures Load()
        {
            var asset = Resources.Load<Metro3DWaitGestures>("PassengerAI/PassengerWaitGestures");
            if (asset == null) return null;
            if (asset.Weight == null && IsValid(asset.weightShift)) asset.Weight = new Pose(asset.weightShift, Bones);
            if (asset.Look == null && IsValid(asset.lookAlongPlatform)) asset.Look = new Pose(asset.lookAlongPlatform, Bones);
            return asset;
        }

        static bool IsValid(AnimationLabAuthoredMotion source)
            => source != null && source.keys.Count > 1 && source.generatedClip != null && source.Validate(out _);
    }
}

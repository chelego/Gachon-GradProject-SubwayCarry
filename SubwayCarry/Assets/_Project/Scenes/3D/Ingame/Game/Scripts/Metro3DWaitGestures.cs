using System;
using SubwayCarry.Gameplay3D.AnimationLab;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public sealed class Metro3DWaitGestures : ScriptableObject
    {
        public AnimationLabAuthoredMotion weightShift, lookAlongPlatform, standingLoad;
        public AnimationLabAuthoredMotion[] platformMotions, gateMotions;

        internal static readonly string[] PlatformBones = { "DEF-spine.001", "DEF-spine.002", "DEF-spine.003",
            "DEF-neck", "DEF-head", "DEF-upper_arm.L", "DEF-upper_arm.R", "DEF-forearm.L", "DEF-forearm.R" };
        internal Pose[] Platform {get;private set;}
        internal Pose[] Gate {get;private set;}

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
        internal StandingTransfer Transfer { get; private set; }

        internal sealed class StandingTransfer
        {
            readonly float[] offsets;
            internal readonly float Range;

            internal StandingTransfer(AnimationLabAuthoredMotion source)
            {
                float neutral=source.Evaluate(0,false).hips.x;
                int peak=0;
                for(int frame=1;frame<=source.lastFrame;frame++) {
                    float displacement=Mathf.Abs(source.Evaluate(frame,false).hips.x-neutral);
                    if(displacement>Range) {Range=displacement;peak=frame;}
                }
                offsets=new float[peak+1];
                if(Range<.0001f)return;
                for(int frame=1;frame<=peak;frame++)
                    offsets[frame]=Mathf.Clamp01(Mathf.Abs(source.Evaluate(frame,false).hips.x-neutral)/Range);
            }

            internal float WeightAt(float progress)
            {
                if(offsets.Length<2)return Mathf.SmoothStep(0,1,progress);
                float position=Mathf.Clamp01(progress)*(offsets.Length-1);
                int index=(int)position;
                return Mathf.Lerp(offsets[index],offsets[Mathf.Min(index+1,offsets.Length-1)],position-index);
            }
        }

        internal static Metro3DWaitGestures Load()
        {
            var asset = Resources.Load<Metro3DWaitGestures>("PassengerAI/PassengerWaitGestures");
            if (asset == null) return null;
            if (asset.Weight == null && IsValid(asset.weightShift)) asset.Weight = new Pose(asset.weightShift, Bones);
            if (asset.Look == null && IsValid(asset.lookAlongPlatform)) asset.Look = new Pose(asset.lookAlongPlatform, Bones);
            if (asset.Transfer == null && IsValid(asset.standingLoad)) asset.Transfer = new StandingTransfer(asset.standingLoad);
            if(asset.Platform==null)asset.Platform=LoadGestures(asset.platformMotions);
            if(asset.Gate==null)asset.Gate=LoadGestures(asset.gateMotions);
            return asset;
        }

        static Pose[] LoadGestures(AnimationLabAuthoredMotion[] sources)
        {
            if(sources==null||sources.Length==0)return null;
            var clips=new Pose[sources.Length];
            for(int i=0;i<clips.Length;i++)if(IsValid(sources[i]))clips[i]=new Pose(sources[i],PlatformBones);
            return clips;
        }

        static bool IsValid(AnimationLabAuthoredMotion source)
            => source != null && source.keys.Count > 1 && source.generatedClip != null && source.Validate(out _);
    }
}

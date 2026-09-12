using System;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeTrainMotionProvider : MonoBehaviour,
        ITrainMotionProvider
    {
        private int sequenceId;

        public TrainMotionSnapshot CurrentTrainMotion { get; private set; }

        public event Action<TrainMotionSnapshot> TrainMotionChanged;

        public void Emit(
            TrainMotionPhase phase,
            Vector2 inertiaDirection,
            float intensity)
        {
            sequenceId++;
            CurrentTrainMotion = new TrainMotionSnapshot(
                sequenceId,
                phase,
                inertiaDirection,
                intensity);
            TrainMotionChanged?.Invoke(CurrentTrainMotion);
        }

        public void Stop()
        {
            Emit(TrainMotionPhase.Stopped, Vector2.zero, 0f);
        }
    }
}

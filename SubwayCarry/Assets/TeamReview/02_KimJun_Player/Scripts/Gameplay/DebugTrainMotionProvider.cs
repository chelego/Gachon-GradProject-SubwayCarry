using System;
using SubwayCarry.TeamReview.KimJun.Core.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.TeamReview.KimJun.Gameplay
{
    // Temporary stand-in for role 3's real ITrainMotionProvider, used to verify PlayerBalance
    // in Play mode before the actual train movement system exists. Remove once role 3 ships one.
    [DisallowMultipleComponent]
    public sealed class DebugTrainMotionProvider : MonoBehaviour, ITrainMotionProvider
    {
        private int sequenceId;

        public TrainMotionSnapshot CurrentTrainMotion { get; private set; }
        public event Action<TrainMotionSnapshot> TrainMotionChanged;

        private void Update()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.digit6Key.wasPressedThisFrame)
            {
                Emit(TrainMotionPhase.Departing);
            }

            if (Keyboard.current.digit7Key.wasPressedThisFrame)
            {
                Emit(TrainMotionPhase.SpeedChanging);
            }

            if (Keyboard.current.digit8Key.wasPressedThisFrame)
            {
                Emit(TrainMotionPhase.Arriving);
            }

            if (Keyboard.current.digit9Key.wasPressedThisFrame)
            {
                Emit(TrainMotionPhase.EmergencyBraking);
            }
        }

        private void Emit(TrainMotionPhase phase)
        {
            sequenceId++;
            CurrentTrainMotion = new TrainMotionSnapshot(sequenceId, phase, Vector2.right, 1f);
            TrainMotionChanged?.Invoke(CurrentTrainMotion);
        }
    }
}

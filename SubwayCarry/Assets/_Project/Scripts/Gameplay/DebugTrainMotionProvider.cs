using System;
using SubwayCarry.Core.Contracts;
using SubwayCarry.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Gameplay
{
    // Temporary stand-in for role 3's real ITrainMotionProvider, used to verify PlayerBalance
    // in Play mode before the actual train movement system exists. Remove once role 3 ships one.
    [DisallowMultipleComponent]
    public sealed class DebugTrainMotionProvider : MonoBehaviour, ITrainMotionProvider
    {
        [SerializeField] private PlayerBalance balance;
        [SerializeField] private bool showBalanceHud = true;

        private int sequenceId;

        public TrainMotionSnapshot CurrentTrainMotion { get; private set; }
        public event Action<TrainMotionSnapshot> TrainMotionChanged;

        private void Awake()
        {
            if (!showBalanceHud)
            {
                return;
            }

            if (balance == null)
            {
                balance = FindFirstObjectByType<PlayerBalance>();
            }

            if (balance == null)
            {
                return;
            }

            BalanceHudPresenter hud = GetComponent<BalanceHudPresenter>();
            if (hud == null)
            {
                hud = gameObject.AddComponent<BalanceHudPresenter>();
            }

            hud.Configure(balance);
        }

        private void Update()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.digit6Key.wasPressedThisFrame)
            {
                Emit(TrainMotionPhase.Departing, Vector2.left, 1f);
            }

            if (Keyboard.current.digit7Key.wasPressedThisFrame)
            {
                Emit(TrainMotionPhase.SpeedChanging, Vector2.down, 1f);
            }

            if (Keyboard.current.digit8Key.wasPressedThisFrame)
            {
                Emit(TrainMotionPhase.Arriving, Vector2.right, 1f);
            }

            if (Keyboard.current.digit9Key.wasPressedThisFrame)
            {
                Emit(TrainMotionPhase.EmergencyBraking, Vector2.up, 1.5f);
            }
        }

        private void Emit(
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
    }
}

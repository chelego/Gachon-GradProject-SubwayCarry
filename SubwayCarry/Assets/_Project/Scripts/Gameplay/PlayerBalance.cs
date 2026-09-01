using System;
using SubwayCarry.Core.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerPosture))]
    public sealed class PlayerBalance : MonoBehaviour, IBalanceStateProvider
    {
        private static readonly Key[] PromptKeys = { Key.W, Key.A, Key.S, Key.D };

        [SerializeField] private MonoBehaviour trainMotionProviderSource;
        [SerializeField, Min(1)] private int standingPromptCount = 3;
        [SerializeField, Min(1)] private int overheadPromptCount = 4;
        [SerializeField, Min(0.1f)] private float standingTimePerPrompt = 0.6f;
        [SerializeField, Min(0.1f)] private float overheadTimePerPrompt = 0.45f;

        private ITrainMotionProvider trainMotionProvider;
        private PlayerPosture posture;

        private Key[] activeSequence;
        private int activeIndex;
        private float promptTimer;
        private int sequenceId;
        private bool hasFailed;

        public bool IsActive { get; private set; }
        public Key CurrentPromptKey => IsActive ? activeSequence[activeIndex] : default;
        public float PromptTimeRemaining => promptTimer;

        public BalanceStateSnapshot CurrentBalanceState => new BalanceStateSnapshot(
            sequenceId,
            IsActive,
            ToBalanceInputDirection(CurrentPromptKey),
            Mathf.Max(0f, promptTimer),
            hasFailed);

        public event Action<BalanceStateSnapshot> BalanceStateChanged;

        private void Awake()
        {
            posture = GetComponent<PlayerPosture>();
            trainMotionProvider = trainMotionProviderSource as ITrainMotionProvider;

            if (trainMotionProviderSource != null && trainMotionProvider == null)
            {
                Debug.LogWarningFormat(
                    this,
                    "{0}: assigned trainMotionProviderSource does not implement ITrainMotionProvider.",
                    name);
            }
        }

        private void OnEnable()
        {
            if (trainMotionProvider != null)
            {
                trainMotionProvider.TrainMotionChanged += HandleTrainMotionChanged;
            }
        }

        private void OnDisable()
        {
            if (trainMotionProvider != null)
            {
                trainMotionProvider.TrainMotionChanged -= HandleTrainMotionChanged;
            }

            if (IsActive)
            {
                EndCheck(false);
            }
        }

        private void Update()
        {
            if (!IsActive)
            {
                return;
            }

            if (posture.CurrentState != CarryPosture.Standing && posture.CurrentState != CarryPosture.OverheadCarry)
            {
                EndCheck(false);
                return;
            }

            Key pressedKey = ReadPressedPromptKey();
            if (pressedKey != default)
            {
                if (pressedKey == activeSequence[activeIndex])
                {
                    AdvancePrompt();
                }
                else
                {
                    Fail();
                }

                return;
            }

            promptTimer = Mathf.Max(0f, promptTimer - Time.deltaTime);
            if (promptTimer <= 0f)
            {
                Fail();
                return;
            }

            NotifyBalanceStateChanged();
        }

        private void HandleTrainMotionChanged(TrainMotionSnapshot snapshot)
        {
            if (IsActive)
            {
                return;
            }

            bool isSpeedEvent = snapshot.Phase == TrainMotionPhase.Departing
                || snapshot.Phase == TrainMotionPhase.Arriving
                || snapshot.Phase == TrainMotionPhase.SpeedChanging
                || snapshot.Phase == TrainMotionPhase.EmergencyBraking;

            if (!isSpeedEvent)
            {
                return;
            }

            if (posture.CurrentState != CarryPosture.Standing && posture.CurrentState != CarryPosture.OverheadCarry)
            {
                return;
            }

            StartCheck();
        }

        private void StartCheck()
        {
            bool overhead = posture.CurrentState == CarryPosture.OverheadCarry;
            int promptCount = overhead ? overheadPromptCount : standingPromptCount;

            sequenceId++;
            activeSequence = new Key[promptCount];
            for (int i = 0; i < promptCount; i++)
            {
                activeSequence[i] = PromptKeys[UnityEngine.Random.Range(0, PromptKeys.Length)];
            }

            activeIndex = 0;
            promptTimer = overhead ? overheadTimePerPrompt : standingTimePerPrompt;
            hasFailed = false;
            IsActive = true;
            NotifyBalanceStateChanged();
        }

        private void AdvancePrompt()
        {
            activeIndex++;
            if (activeIndex >= activeSequence.Length)
            {
                EndCheck(false);
                return;
            }

            bool overhead = posture.CurrentState == CarryPosture.OverheadCarry;
            promptTimer = overhead ? overheadTimePerPrompt : standingTimePerPrompt;
            NotifyBalanceStateChanged();
        }

        private void Fail()
        {
            posture.Fall();
            EndCheck(true);
        }

        private void EndCheck(bool failed)
        {
            IsActive = false;
            activeSequence = null;
            activeIndex = 0;
            promptTimer = 0f;
            hasFailed = failed;
            NotifyBalanceStateChanged();
        }

        private void NotifyBalanceStateChanged()
        {
            BalanceStateChanged?.Invoke(CurrentBalanceState);
        }

        private static BalanceInputDirection ToBalanceInputDirection(Key key)
        {
            switch (key)
            {
                case Key.W:
                    return BalanceInputDirection.Up;

                case Key.A:
                    return BalanceInputDirection.Left;

                case Key.S:
                    return BalanceInputDirection.Down;

                case Key.D:
                    return BalanceInputDirection.Right;

                default:
                    return BalanceInputDirection.None;
            }
        }

        private static Key ReadPressedPromptKey()
        {
            if (Keyboard.current == null)
            {
                return default;
            }

            foreach (Key key in PromptKeys)
            {
                if (Keyboard.current[key].wasPressedThisFrame)
                {
                    return key;
                }
            }

            return default;
        }
    }
}

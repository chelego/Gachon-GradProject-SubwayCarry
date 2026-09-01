using System;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerPosture : MonoBehaviour, IPlayerCarryStateProvider, IStaminaStateProvider
    {
        [SerializeField] private float leaningTransitionTime = 0.3f;
        [SerializeField] private float sittingTransitionTime = 0.4f;
        [SerializeField] private float holdingTransitionTime = 0.2f;
        [SerializeField] private float overheadTransitionTime = 0.5f;
        [SerializeField, Range(0f, 1f)] private float overheadMoveSpeedMultiplier = 0.5f;
        [SerializeField, Min(0f)] private float fallenRecoveryTime = 1.5f;

        [SerializeField, Min(0f)] private float maxStamina = 5f;
        [SerializeField, Min(0f)] private float staminaDrainRate = 1f;
        [SerializeField, Min(0f)] private float staminaRecoverRate = 0.5f;
        [SerializeField, Min(0f)] private float staminaRecoverDelay = 1f;

        private CarryPosture currentState = CarryPosture.Standing;
        private CarryPosture targetState = CarryPosture.Standing;
        private float transitionTimer = 0f;

        private float currentStamina;
        private float recoverDelayTimer;
        private float fallenTimer;

        public float StaminaRatio => maxStamina <= 0f ? 0f : currentStamina / maxStamina;

        public CarryPosture CurrentState => currentState;

        public PlayerCarryStateSnapshot CurrentCarryState => new PlayerCarryStateSnapshot(
            currentState,
            IsTransitioning,
            CanMove);

        public StaminaStateSnapshot CurrentStaminaState => new StaminaStateSnapshot(
            currentStamina,
            maxStamina,
            recoverDelayTimer > 0f);

        public bool CanMove => !IsTransitioning && (currentState == CarryPosture.Standing || currentState == CarryPosture.OverheadCarry);

        public float MoveSpeedMultiplier => currentState == CarryPosture.OverheadCarry ? overheadMoveSpeedMultiplier : 1f;

        public bool IsTransitioning { get; private set; }

        public event Action<PlayerCarryStateSnapshot> CarryStateChanged;
        public event Action<StaminaStateSnapshot> StaminaStateChanged;

        public bool TryTransition(CarryPosture target)
        {
            if (IsTransitioning || target == currentState)
            {
                return false;
            }

            targetState = target;
            transitionTimer = GetTransitionTime(target);
            IsTransitioning = true;
            NotifyCarryStateChanged();
            return true;
        }

        public void Fall()
        {
            if (currentState == CarryPosture.Fallen)
            {
                return;
            }

            IsTransitioning = false;
            currentState = CarryPosture.Fallen;
            targetState = CarryPosture.Fallen;
            fallenTimer = fallenRecoveryTime;
            NotifyCarryStateChanged();
        }

        private void Awake()
        {
            currentStamina = maxStamina;
        }

        private void Update()
        {
            if (IsTransitioning)
            {
                transitionTimer -= Time.deltaTime;
                if (transitionTimer <= 0f)
                {
                    currentState = targetState;
                    IsTransitioning = false;
                    NotifyCarryStateChanged();
                }
            }

            if (currentState == CarryPosture.Fallen)
            {
                fallenTimer -= Time.deltaTime;
                if (fallenTimer <= 0f)
                {
                    TryTransition(CarryPosture.Standing);
                }

                return;
            }

            UpdateStamina();
        }

        private void UpdateStamina()
        {
            StaminaStateSnapshot previousState = CurrentStaminaState;

            if (currentState == CarryPosture.OverheadCarry)
            {
                currentStamina -= staminaDrainRate * Time.deltaTime;
                currentStamina = Mathf.Max(currentStamina, 0f);
                recoverDelayTimer = staminaRecoverDelay;

                if (currentStamina <= 0f)
                {
                    TryTransition(CarryPosture.Standing);
                }

                NotifyStaminaStateChangedIfNeeded(previousState);
                return;
            }

            if (recoverDelayTimer > 0f)
            {
                recoverDelayTimer = Mathf.Max(0f, recoverDelayTimer - Time.deltaTime);
                NotifyStaminaStateChangedIfNeeded(previousState);
                return;
            }

            currentStamina += staminaRecoverRate * Time.deltaTime;
            currentStamina = Mathf.Min(currentStamina, maxStamina);
            NotifyStaminaStateChangedIfNeeded(previousState);
        }

        private void NotifyCarryStateChanged()
        {
            CarryStateChanged?.Invoke(CurrentCarryState);
        }

        private void NotifyStaminaStateChangedIfNeeded(in StaminaStateSnapshot previousState)
        {
            StaminaStateSnapshot currentStateSnapshot = CurrentStaminaState;
            bool changed = !Mathf.Approximately(previousState.Current, currentStateSnapshot.Current)
                || !Mathf.Approximately(previousState.Maximum, currentStateSnapshot.Maximum)
                || previousState.RecoveryLocked != currentStateSnapshot.RecoveryLocked;

            if (changed)
            {
                StaminaStateChanged?.Invoke(currentStateSnapshot);
            }
        }

        private float GetTransitionTime(CarryPosture target)
        {
            switch (target)
            {
                case CarryPosture.Leaning:
                    return leaningTransitionTime;

                case CarryPosture.Sitting:
                    return sittingTransitionTime;

                case CarryPosture.HoldingSupport:
                    return holdingTransitionTime;

                case CarryPosture.OverheadCarry:
                    return overheadTransitionTime;

                default:
                    return 0.3f;
            }
        }
    }
}

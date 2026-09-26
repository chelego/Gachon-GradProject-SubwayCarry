using System;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerPosture : MonoBehaviour, IPlayerCarryStateProvider,
        IStaminaStateProvider, IPlayerBalanceParticipant
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
        private float transitionDuration = 0f;

        private float currentStamina;
        private float recoverDelayTimer;
        private float fallenTimer;
        private float staminaBonusPercent;
        private float baseMaxStamina;

        public void ConfigureStaminaMultiplier(float multiplier)
        {
            float ratio = StaminaRatio;
            maxStamina = baseMaxStamina * Mathf.Clamp(multiplier, 1, 3);
            currentStamina = EffectiveMaxStamina * ratio;
            StaminaStateChanged?.Invoke(CurrentStaminaState);
        }

        public void RestorePosture(CarryPosture posture, float staminaRatio)
        {
            if (!Enum.IsDefined(typeof(CarryPosture), posture) || float.IsNaN(staminaRatio)) return;
            currentState = targetState = posture;
            IsTransitioning = false; transitionTimer = 0;
            fallenTimer = posture == CarryPosture.Fallen ? fallenRecoveryTime : 0;
            currentStamina = Mathf.Clamp01(staminaRatio) * EffectiveMaxStamina;
            NotifyCarryStateChanged(); StaminaStateChanged?.Invoke(CurrentStaminaState);
        }

        public float BaseMaxStamina => baseMaxStamina;

        public float StaminaBonusPercent => staminaBonusPercent;

        public float EffectiveMaxStamina =>
            maxStamina * (1f + staminaBonusPercent / 100f);

        public float StaminaRatio => EffectiveMaxStamina <= 0f
            ? 0f
            : currentStamina / EffectiveMaxStamina;

        public CarryPosture CurrentState => currentState;

        public CarryPosture TargetState => targetState;

        public float TransitionProgress => !IsTransitioning || transitionDuration <= 0f
            ? 1f
            : Mathf.Clamp01(1f - transitionTimer / transitionDuration);

        public CarryPosture CurrentBalancePosture => currentState;

        public PlayerCarryStateSnapshot CurrentCarryState => new PlayerCarryStateSnapshot(
            currentState,
            IsTransitioning,
            CanMove);

        public StaminaStateSnapshot CurrentStaminaState => new StaminaStateSnapshot(
            currentStamina,
            EffectiveMaxStamina,
            recoverDelayTimer > 0f);

        public bool CanMove => !IsTransitioning && (currentState == CarryPosture.Standing || currentState == CarryPosture.OverheadCarry);

        public bool CanChangeFacing => !IsTransitioning &&
            (currentState == CarryPosture.Standing ||
             currentState == CarryPosture.OverheadCarry);

        public float MoveSpeedMultiplier => currentState == CarryPosture.OverheadCarry ? overheadMoveSpeedMultiplier : 1f;

        public bool IsTransitioning { get; private set; }

        public event Action<PlayerCarryStateSnapshot> CarryStateChanged;
        public event Action<StaminaStateSnapshot> StaminaStateChanged;

        public void SetStaminaBonusPercent(float bonusPercent)
        {
            float previousMaximum = EffectiveMaxStamina;
            float previousRatio = previousMaximum > 0f
                ? Mathf.Clamp01(currentStamina / previousMaximum)
                : 1f;
            staminaBonusPercent = Mathf.Max(0f, bonusPercent);
            currentStamina = EffectiveMaxStamina * previousRatio;
            StaminaStateChanged?.Invoke(CurrentStaminaState);
        }

        public bool TryTransition(CarryPosture target)
        {
            if (IsTransitioning || target == currentState)
            {
                return false;
            }

            targetState = target;
            transitionDuration = GetTransitionTime(target);
            transitionTimer = transitionDuration;
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
            transitionDuration = 0f;
            transitionTimer = 0f;
            fallenTimer = fallenRecoveryTime;
            NotifyCarryStateChanged();
        }

        public void FallFromBalance()
        {
            Fall();
        }

        private void Awake()
        {
            baseMaxStamina = maxStamina;
            currentStamina = EffectiveMaxStamina;
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
                    transitionTimer = 0f;
                    transitionDuration = 0f;
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
            currentStamina = Mathf.Min(currentStamina, EffectiveMaxStamina);
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

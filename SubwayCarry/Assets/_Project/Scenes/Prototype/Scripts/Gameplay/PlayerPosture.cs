using System;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Prototype.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerPosture : MonoBehaviour, IPlayerBalanceParticipant
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

        private PostureState currentState = PostureState.Standing;
        private PostureState targetState = PostureState.Standing;
        private float transitionTimer = 0f;

        private float currentStamina;
        private float recoverDelayTimer;
        private float fallenTimer;

        public float StaminaRatio => maxStamina <= 0f ? 0f : currentStamina / maxStamina;

        public PostureState CurrentState => currentState;

        public CarryPosture CurrentBalancePosture
        {
            get
            {
                switch (currentState)
                {
                    case PostureState.Leaning:
                        return CarryPosture.Leaning;
                    case PostureState.Sitting:
                        return CarryPosture.Sitting;
                    case PostureState.Holding:
                        return CarryPosture.HoldingSupport;
                    case PostureState.OverheadCarry:
                        return CarryPosture.OverheadCarry;
                    case PostureState.Fallen:
                        return CarryPosture.Fallen;
                    default:
                        return CarryPosture.Standing;
                }
            }
        }

        public bool CanMove => !IsTransitioning && (currentState == PostureState.Standing || currentState == PostureState.OverheadCarry);

        public float MoveSpeedMultiplier => currentState == PostureState.OverheadCarry ? overheadMoveSpeedMultiplier : 1f;

        public bool IsTransitioning { get; private set; }
        public bool TryTransition(PostureState target)
        {
            if (IsTransitioning || target == currentState)
            {
                return false;
            }

            targetState = target;
            transitionTimer = GetTransitionTime(target);
            IsTransitioning = true;
            return true;
        }

        public void Fall()
        {
            if (currentState == PostureState.Fallen)
            {
                return;
            }

            IsTransitioning = false;
            currentState = PostureState.Fallen;
            targetState = PostureState.Fallen;
            fallenTimer = fallenRecoveryTime;
        }

        public void FallFromBalance()
        {
            Fall();
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
                    //Debug.Log($"Posture changed to {currentState}"); // temporary
                }
            }

            if (currentState == PostureState.Fallen)
            {
                fallenTimer -= Time.deltaTime;
                if (fallenTimer <= 0f)
                {
                    TryTransition(PostureState.Standing);
                }

                return;
            }

            UpdateStamina();
        }

        private void UpdateStamina()
        {
            if (currentState == PostureState.OverheadCarry)
            {
                currentStamina -= staminaDrainRate * Time.deltaTime;
                currentStamina = Mathf.Max(currentStamina, 0f);
                recoverDelayTimer = staminaRecoverDelay;

                if (currentStamina <= 0f)
                {
                    TryTransition(PostureState.Standing);
                }

                return;
            }

            if (recoverDelayTimer > 0f)
            {
                recoverDelayTimer -= Time.deltaTime;
                return;
            }

            currentStamina += staminaRecoverRate * Time.deltaTime;
            currentStamina = Mathf.Min(currentStamina, maxStamina);
        }

        private float GetTransitionTime(PostureState target)
        {
            switch (target)
            {
                case PostureState.Leaning:
                    return leaningTransitionTime;

                case PostureState.Sitting:
                    return sittingTransitionTime;

                case PostureState.Holding:
                    return holdingTransitionTime;

                case PostureState.OverheadCarry:
                    return overheadTransitionTime;

                default:
                    return 0.3f;
            }
        }
    }
}

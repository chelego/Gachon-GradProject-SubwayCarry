using System;
using UnityEngine;

namespace SubwayCarry.Core.Contracts
{
    public enum CarryPosture
    {
        Standing,
        Leaning,
        Sitting,
        HoldingSupport,
        OverheadCarry,
        Fallen
    }

    public enum BalanceInputDirection
    {
        None,
        Up,
        Left,
        Down,
        Right
    }

    public enum BalanceInputAxis
    {
        None,
        Horizontal,
        Vertical
    }

    public enum BalanceChallengePattern
    {
        None,
        CounterTap,
        CenterGauge,
        ImpactTiming
    }

    public enum BalanceChallengePhase
    {
        Inactive,
        Warning,
        Active,
        Succeeded,
        Failed,
        Cancelled
    }

    public readonly struct PlayerCarryStateSnapshot
    {
        public PlayerCarryStateSnapshot(
            CarryPosture posture,
            bool isTransitioning,
            bool canMove)
        {
            Posture = posture;
            IsTransitioning = isTransitioning;
            CanMove = canMove;
        }

        public CarryPosture Posture { get; }
        public bool IsTransitioning { get; }
        public bool CanMove { get; }
    }

    public readonly struct StaminaStateSnapshot
    {
        public StaminaStateSnapshot(float current, float maximum, bool recoveryLocked)
        {
            Current = current;
            Maximum = maximum;
            RecoveryLocked = recoveryLocked;
        }

        public float Current { get; }
        public float Maximum { get; }
        public bool RecoveryLocked { get; }
        public float Ratio => Maximum <= 0f ? 0f : Mathf.Clamp01(Current / Maximum);
    }

    public readonly struct BalanceStateSnapshot
    {
        public BalanceStateSnapshot(
            int sequenceId,
            BalanceChallengePattern pattern,
            BalanceChallengePhase phase,
            BalanceInputAxis inputAxis,
            BalanceInputDirection requiredInput,
            float remainingSeconds,
            float progress,
            float indicatorValue,
            float targetValue,
            float safeZoneHalfWidth)
        {
            SequenceId = sequenceId;
            Pattern = pattern;
            Phase = phase;
            InputAxis = inputAxis;
            RequiredInput = requiredInput;
            RemainingSeconds = Mathf.Max(0f, remainingSeconds);
            Progress = Mathf.Clamp01(progress);
            IndicatorValue = Mathf.Clamp(indicatorValue, -1.2f, 1.2f);
            TargetValue = Mathf.Clamp(targetValue, -1f, 1f);
            SafeZoneHalfWidth = Mathf.Clamp(safeZoneHalfWidth, 0f, 1f);
        }

        public int SequenceId { get; }
        public BalanceChallengePattern Pattern { get; }
        public BalanceChallengePhase Phase { get; }
        public BalanceInputAxis InputAxis { get; }
        public BalanceInputDirection RequiredInput { get; }
        public float RemainingSeconds { get; }
        public float Progress { get; }
        public float IndicatorValue { get; }
        public float TargetValue { get; }
        public float SafeZoneHalfWidth { get; }
        public bool IsActive => Phase == BalanceChallengePhase.Warning ||
                                Phase == BalanceChallengePhase.Active;
        public bool HasFailed => Phase == BalanceChallengePhase.Failed;
    }

    public readonly struct PackageImpactData
    {
        public PackageImpactData(
            GameObject source,
            Vector2 contactPoint,
            float relativeSpeed,
            float compressionRatio)
        {
            Source = source;
            ContactPoint = contactPoint;
            RelativeSpeed = Mathf.Max(0f, relativeSpeed);
            CompressionRatio = Mathf.Clamp01(compressionRatio);
        }

        public GameObject Source { get; }
        public Vector2 ContactPoint { get; }
        public float RelativeSpeed { get; }
        public float CompressionRatio { get; }
    }

    public readonly struct PackageDurabilitySnapshot
    {
        public PackageDurabilitySnapshot(
            float boxDurability,
            float cakeDurability,
            bool deliveryFailed)
        {
            BoxDurability = Mathf.Clamp(boxDurability, 0f, 100f);
            CakeDurability = Mathf.Clamp(cakeDurability, 0f, 100f);
            DeliveryFailed = deliveryFailed;
        }

        public float BoxDurability { get; }
        public float CakeDurability { get; }
        public bool DeliveryFailed { get; }
    }

    public interface IPlayerCarryStateProvider
    {
        PlayerCarryStateSnapshot CurrentCarryState { get; }
        event Action<PlayerCarryStateSnapshot> CarryStateChanged;
    }

    public interface IStaminaStateProvider
    {
        StaminaStateSnapshot CurrentStaminaState { get; }
        event Action<StaminaStateSnapshot> StaminaStateChanged;
    }

    public interface IBalanceStateProvider
    {
        BalanceStateSnapshot CurrentBalanceState { get; }
        event Action<BalanceStateSnapshot> BalanceStateChanged;
    }

    public interface IPlayerBalanceParticipant
    {
        CarryPosture CurrentBalancePosture { get; }
        void FallFromBalance();
    }

    public interface IPackageImpactReceiver
    {
        void ApplyImpact(in PackageImpactData impact);
    }

    public interface IPackageImpactSource
    {
        Vector2 ImpactVelocity { get; }
    }

    public interface IPackageDurabilityProvider
    {
        PackageDurabilitySnapshot CurrentDurability { get; }
        event Action<PackageDurabilitySnapshot> DurabilityChanged;
    }

    public interface IPackageDurabilityResetter
    {
        void ResetToFull();
    }

    public interface IPackageAvailabilityController
    {
        bool HasPackage { get; }
        void SetPackageAvailable(bool available);
    }
}

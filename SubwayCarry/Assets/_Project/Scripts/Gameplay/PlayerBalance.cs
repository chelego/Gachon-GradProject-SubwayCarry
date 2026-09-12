using System;
using SubwayCarry.Core.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerBalance : MonoBehaviour, IBalanceStateProvider
    {
        private static readonly Key[] PromptKeys = { Key.W, Key.A, Key.S, Key.D };

        [Header("Dependencies")]
        [SerializeField] private MonoBehaviour trainMotionProviderSource;
        [SerializeField] private MonoBehaviour balanceParticipantSource;
        [SerializeField] private MonoBehaviour packageImpactReceiverSource;

        [Header("Counter Tap")]
        [SerializeField, Min(1)] private int standingPromptCount = 3;
        [SerializeField, Min(1)] private int overheadPromptCount = 4;
        [SerializeField, Min(0.1f)] private float standingTimePerPrompt = 0.6f;
        [SerializeField, Min(0.1f)] private float overheadTimePerPrompt = 0.45f;
        [SerializeField, Range(0f, 1f)] private float wrongInputProgressPenalty = 0.25f;

        [Header("Center Gauge")]
        [SerializeField, Min(0.1f)] private float standingCenterDuration = 2.4f;
        [SerializeField, Min(0.1f)] private float overheadCenterDuration = 2.6f;
        [SerializeField, Range(0.05f, 0.9f)] private float standingCenterSafeZone = 0.36f;
        [SerializeField, Range(0.05f, 0.9f)] private float overheadCenterSafeZone = 0.26f;
        [SerializeField, Min(0.05f)] private float centerFailureGrace = 0.65f;
        [SerializeField, Min(0f)] private float centerInitialVelocity = 0.45f;
        [SerializeField, Min(0f)] private float centerInertiaForce = 0.8f;
        [SerializeField, Min(0f)] private float centerSwayForce = 1.1f;
        [SerializeField, Min(0.05f)] private float centerSwayFrequency = 0.8f;
        [SerializeField, Min(0f)] private float centerControlForce = 4.5f;
        [SerializeField, Min(0f)] private float centerDamping = 2.5f;

        [Header("Impact Timing")]
        [SerializeField, Min(0.2f)] private float standingTimingDuration = 1.5f;
        [SerializeField, Min(0.2f)] private float overheadTimingDuration = 1.25f;
        [SerializeField, Range(0.1f, 0.9f)] private float timingTarget = 0.72f;
        [SerializeField, Range(0.02f, 0.45f)] private float standingTimingWindow = 0.14f;
        [SerializeField, Range(0.02f, 0.45f)] private float overheadTimingWindow = 0.09f;
        [SerializeField, Range(0f, 0.2f)] private float wrongTimingWindowPenalty = 0.02f;
        [SerializeField, Range(0.01f, 0.2f)] private float minimumTimingWindow = 0.04f;

        [Header("Common")]
        [SerializeField, Min(0f)] private float warningDuration = 0.35f;
        [SerializeField, Min(0f)] private float failureImpactSpeed = 2f;

        private ITrainMotionProvider trainMotionProvider;
        private IPlayerBalanceParticipant balanceParticipant;
        private IPackageImpactReceiver directPackageImpactReceiver;
        private PlayerPackageCarrier packageCarrier;
        private bool providerSubscribed;

        private BalanceChallengePattern activePattern;
        private BalanceChallengePhase phase;
        private BalanceInputAxis inputAxis;
        private BalanceInputDirection requiredInput;
        private BalanceInputDirection initialCounterInput;
        private int requiredTapCount;
        private float challengeDuration;
        private float phaseTimer;
        private float progress;
        private float motionIntensity = 1f;
        private float indicatorValue;
        private float targetValue;
        private float safeZoneHalfWidth;
        private float centerVelocity;
        private float centerInertiaSign;
        private float centerElapsed;
        private float centerUnsafeTimer;
        private float currentTimingWindow;
        private int sequenceId;

        public bool IsActive => phase == BalanceChallengePhase.Warning ||
                                phase == BalanceChallengePhase.Active;
        public bool ConsumesMovementInput => IsActive;
        public Key CurrentPromptKey => IsActive ? ToKey(requiredInput) : default;
        public float PromptTimeRemaining => phaseTimer;
        public float ProgressRatio => progress;
        public int RequiredTapCount => requiredTapCount;

        public BalanceStateSnapshot CurrentBalanceState => new BalanceStateSnapshot(
            sequenceId,
            activePattern,
            phase,
            inputAxis,
            IsActive ? requiredInput : BalanceInputDirection.None,
            phaseTimer,
            progress,
            indicatorValue,
            targetValue,
            safeZoneHalfWidth);

        public event Action<BalanceStateSnapshot> BalanceStateChanged;

        public void Configure(
            MonoBehaviour motionProviderSource,
            MonoBehaviour participantSource = null,
            MonoBehaviour impactReceiverSource = null)
        {
            UnsubscribeFromProvider();
            trainMotionProviderSource = motionProviderSource;
            balanceParticipantSource = participantSource;
            packageImpactReceiverSource = impactReceiverSource;
            ResolveDependencies();
            SubscribeToProvider();
        }

        public void CancelCurrentChallenge()
        {
            if (IsActive)
            {
                ResolveChallenge(BalanceChallengePhase.Cancelled);
            }
        }

        private void Awake()
        {
            ResolveDependencies();
            ValidateAssignedSources();
        }

        private void OnEnable()
        {
            ResolveDependencies();
            SubscribeToProvider();
        }

        private void OnDisable()
        {
            UnsubscribeFromProvider();
            CancelCurrentChallenge();
        }

        private void Update()
        {
            if (!IsActive)
            {
                return;
            }

            if (balanceParticipant == null ||
                !CanBalanceInPosture(balanceParticipant.CurrentBalancePosture))
            {
                ResolveChallenge(BalanceChallengePhase.Cancelled);
                return;
            }

            if (phase == BalanceChallengePhase.Warning)
            {
                UpdateWarning();
                return;
            }

            switch (activePattern)
            {
                case BalanceChallengePattern.CounterTap:
                    UpdateCounterTap();
                    break;

                case BalanceChallengePattern.CenterGauge:
                    UpdateCenterGauge();
                    break;

                case BalanceChallengePattern.ImpactTiming:
                    UpdateImpactTiming();
                    break;

                default:
                    ResolveChallenge(BalanceChallengePhase.Cancelled);
                    break;
            }
        }

        private void HandleTrainMotionChanged(TrainMotionSnapshot snapshot)
        {
            if (IsActive || snapshot.Intensity <= 0f || balanceParticipant == null ||
                !CanBalanceInPosture(balanceParticipant.CurrentBalancePosture))
            {
                return;
            }

            BalanceChallengePattern pattern = GetPattern(snapshot.Phase);
            if (pattern == BalanceChallengePattern.None)
            {
                return;
            }

            StartChallenge(pattern, snapshot);
        }

        private void StartChallenge(
            BalanceChallengePattern pattern,
            in TrainMotionSnapshot snapshot)
        {
            bool overhead =
                balanceParticipant.CurrentBalancePosture == CarryPosture.OverheadCarry;
            float intensityMultiplier = Mathf.Clamp(snapshot.Intensity, 0.5f, 2f);

            sequenceId++;
            activePattern = pattern;
            phase = warningDuration > 0f
                ? BalanceChallengePhase.Warning
                : BalanceChallengePhase.Active;
            initialCounterInput = GetCounterDirection(snapshot.InertiaDirection);
            requiredInput = initialCounterInput;
            inputAxis = GetInputAxis(requiredInput);
            motionIntensity = Mathf.Max(0f, snapshot.Intensity);
            progress = 0f;
            indicatorValue = 0f;
            targetValue = 0f;
            centerVelocity = 0f;
            centerInertiaSign = 0f;
            centerElapsed = 0f;
            centerUnsafeTimer = 0f;

            switch (pattern)
            {
                case BalanceChallengePattern.CounterTap:
                {
                    int baseTapCount = overhead
                        ? overheadPromptCount
                        : standingPromptCount;
                    float timePerTap = overhead
                        ? overheadTimePerPrompt
                        : standingTimePerPrompt;
                    requiredTapCount = Mathf.Max(1,
                        Mathf.RoundToInt(baseTapCount * intensityMultiplier));
                    challengeDuration = Mathf.Max(0.1f,
                        baseTapCount * timePerTap);
                    safeZoneHalfWidth = 0f;
                    break;
                }

                case BalanceChallengePattern.CenterGauge:
                {
                    requiredTapCount = 0;
                    challengeDuration = overhead
                        ? overheadCenterDuration
                        : standingCenterDuration;
                    safeZoneHalfWidth = overhead
                        ? overheadCenterSafeZone
                        : standingCenterSafeZone;
                    centerInertiaSign = GetInertiaSign(
                        snapshot.InertiaDirection,
                        inputAxis,
                        initialCounterInput);
                    centerVelocity = centerInertiaSign * centerInitialVelocity *
                                     intensityMultiplier;
                    requiredInput = GetCenterCorrectionDirection();
                    break;
                }

                case BalanceChallengePattern.ImpactTiming:
                {
                    requiredTapCount = 1;
                    challengeDuration = overhead
                        ? overheadTimingDuration
                        : standingTimingDuration;
                    currentTimingWindow = overhead
                        ? overheadTimingWindow
                        : standingTimingWindow;
                    targetValue = timingTarget * 2f - 1f;
                    safeZoneHalfWidth = currentTimingWindow * 2f;
                    indicatorValue = -1f;
                    break;
                }
            }

            phaseTimer = phase == BalanceChallengePhase.Warning
                ? warningDuration
                : challengeDuration;
            NotifyBalanceStateChanged();
        }

        private void UpdateWarning()
        {
            phaseTimer = Mathf.Max(0f, phaseTimer - Time.deltaTime);
            if (phaseTimer <= 0f)
            {
                phase = BalanceChallengePhase.Active;
                phaseTimer = challengeDuration;
            }

            NotifyBalanceStateChanged();
        }

        private void UpdateCounterTap()
        {
            BalanceInputDirection pressedDirection = ReadPressedDirection();
            if (pressedDirection != BalanceInputDirection.None)
            {
                if (pressedDirection == requiredInput)
                {
                    progress = Mathf.Clamp01(progress + 1f / requiredTapCount);
                    if (progress >= 1f)
                    {
                        ResolveChallenge(BalanceChallengePhase.Succeeded);
                        return;
                    }
                }
                else
                {
                    progress = Mathf.Max(0f,
                        progress - wrongInputProgressPenalty);
                }
            }

            phaseTimer = Mathf.Max(0f, phaseTimer - Time.deltaTime);
            if (phaseTimer <= 0f)
            {
                Fail();
                return;
            }

            NotifyBalanceStateChanged();
        }

        private void UpdateCenterGauge()
        {
            float deltaTime = Time.deltaTime;
            centerElapsed += deltaTime;
            float input = ReadHeldAxis(inputAxis);
            float wave = Mathf.Sin(
                (centerElapsed * centerSwayFrequency + sequenceId * 0.173f) *
                Mathf.PI * 2f);
            float intensity = Mathf.Clamp(motionIntensity, 0.5f, 2f);
            float force = centerInertiaSign * centerInertiaForce * intensity +
                          wave * centerSwayForce * intensity +
                          input * centerControlForce;

            centerVelocity += force * deltaTime;
            centerVelocity *= Mathf.Exp(-centerDamping * deltaTime);
            indicatorValue = Mathf.Clamp(
                indicatorValue + centerVelocity * deltaTime,
                -1.15f,
                1.15f);
            requiredInput = GetCenterCorrectionDirection();

            if (Mathf.Abs(indicatorValue) > safeZoneHalfWidth)
            {
                centerUnsafeTimer += deltaTime;
            }
            else
            {
                centerUnsafeTimer = Mathf.Max(
                    0f,
                    centerUnsafeTimer - deltaTime * 1.5f);
            }

            if (centerUnsafeTimer >= centerFailureGrace)
            {
                Fail();
                return;
            }

            phaseTimer = Mathf.Max(0f, phaseTimer - deltaTime);
            progress = Mathf.Clamp01(1f - phaseTimer / challengeDuration);
            if (phaseTimer <= 0f)
            {
                ResolveChallenge(BalanceChallengePhase.Succeeded);
                return;
            }

            NotifyBalanceStateChanged();
        }

        private void UpdateImpactTiming()
        {
            BalanceInputDirection pressedDirection = ReadPressedDirection();
            float normalizedTime = Mathf.Clamp01(
                1f - phaseTimer / challengeDuration);

            if (pressedDirection != BalanceInputDirection.None)
            {
                bool correctDirection = pressedDirection == requiredInput;
                bool insideTimingWindow =
                    Mathf.Abs(normalizedTime - timingTarget) <= currentTimingWindow;
                if (correctDirection && insideTimingWindow)
                {
                    progress = 1f;
                    indicatorValue = normalizedTime * 2f - 1f;
                    ResolveChallenge(BalanceChallengePhase.Succeeded);
                    return;
                }

                currentTimingWindow = Mathf.Max(
                    minimumTimingWindow,
                    currentTimingWindow - wrongTimingWindowPenalty);
                safeZoneHalfWidth = currentTimingWindow * 2f;
            }

            phaseTimer = Mathf.Max(0f, phaseTimer - Time.deltaTime);
            progress = Mathf.Clamp01(1f - phaseTimer / challengeDuration);
            indicatorValue = progress * 2f - 1f;
            if (phaseTimer <= 0f)
            {
                Fail();
                return;
            }

            NotifyBalanceStateChanged();
        }

        private void Fail()
        {
            ApplyFailureImpact();
            balanceParticipant?.FallFromBalance();
            ResolveChallenge(BalanceChallengePhase.Failed);
        }

        private void ApplyFailureImpact()
        {
            if (failureImpactSpeed <= 0f)
            {
                return;
            }

            if (directPackageImpactReceiver != null)
            {
                Vector2 contactPoint = packageImpactReceiverSource != null
                    ? packageImpactReceiverSource.transform.position
                    : transform.position;
                directPackageImpactReceiver.ApplyImpact(new PackageImpactData(
                    gameObject,
                    contactPoint,
                    failureImpactSpeed * motionIntensity,
                    0f));
                return;
            }

            if (packageCarrier == null || packageCarrier.CurrentPackage == null)
            {
                return;
            }

            Transform packageRoot = packageCarrier.CurrentPackage;
            MonoBehaviour[] behaviours =
                packageRoot.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (!(behaviour is IPackageImpactReceiver receiver))
                {
                    continue;
                }

                receiver.ApplyImpact(new PackageImpactData(
                    gameObject,
                    packageRoot.position,
                    failureImpactSpeed * motionIntensity,
                    0f));
                return;
            }
        }

        private void ResolveChallenge(BalanceChallengePhase result)
        {
            phase = result;
            requiredInput = BalanceInputDirection.None;
            phaseTimer = 0f;
            NotifyBalanceStateChanged();
        }

        private void ResolveDependencies()
        {
            trainMotionProvider = trainMotionProviderSource as ITrainMotionProvider;
            balanceParticipant =
                balanceParticipantSource as IPlayerBalanceParticipant;
            directPackageImpactReceiver =
                packageImpactReceiverSource as IPackageImpactReceiver;
            packageCarrier = GetComponent<PlayerPackageCarrier>();

            if (balanceParticipant == null)
            {
                MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour is IPlayerBalanceParticipant participant)
                    {
                        balanceParticipant = participant;
                        break;
                    }
                }
            }
        }

        private void ValidateAssignedSources()
        {
            if (trainMotionProviderSource != null && trainMotionProvider == null)
            {
                Debug.LogWarningFormat(
                    this,
                    "{0}: assigned trainMotionProviderSource does not implement ITrainMotionProvider.",
                    name);
            }

            if (balanceParticipantSource != null && balanceParticipant == null)
            {
                Debug.LogWarningFormat(
                    this,
                    "{0}: assigned balanceParticipantSource does not implement IPlayerBalanceParticipant.",
                    name);
            }

            if (packageImpactReceiverSource != null &&
                directPackageImpactReceiver == null)
            {
                Debug.LogWarningFormat(
                    this,
                    "{0}: assigned packageImpactReceiverSource does not implement IPackageImpactReceiver.",
                    name);
            }
        }

        private void SubscribeToProvider()
        {
            if (!isActiveAndEnabled || providerSubscribed ||
                trainMotionProvider == null)
            {
                return;
            }

            trainMotionProvider.TrainMotionChanged += HandleTrainMotionChanged;
            providerSubscribed = true;
        }

        private void UnsubscribeFromProvider()
        {
            if (!providerSubscribed || trainMotionProvider == null)
            {
                providerSubscribed = false;
                return;
            }

            trainMotionProvider.TrainMotionChanged -= HandleTrainMotionChanged;
            providerSubscribed = false;
        }

        private BalanceInputDirection GetCenterCorrectionDirection()
        {
            float correctionSource = Mathf.Abs(indicatorValue) > 0.035f
                ? indicatorValue
                : centerVelocity;
            if (Mathf.Abs(correctionSource) <= 0.035f)
            {
                return initialCounterInput;
            }

            if (inputAxis == BalanceInputAxis.Horizontal)
            {
                return correctionSource > 0f
                    ? BalanceInputDirection.Left
                    : BalanceInputDirection.Right;
            }

            return correctionSource > 0f
                ? BalanceInputDirection.Down
                : BalanceInputDirection.Up;
        }

        private static bool CanBalanceInPosture(CarryPosture posture)
        {
            return posture == CarryPosture.Standing ||
                   posture == CarryPosture.OverheadCarry;
        }

        private static BalanceChallengePattern GetPattern(TrainMotionPhase phase)
        {
            switch (phase)
            {
                case TrainMotionPhase.Departing:
                case TrainMotionPhase.Arriving:
                    return BalanceChallengePattern.CounterTap;

                case TrainMotionPhase.SpeedChanging:
                    return BalanceChallengePattern.CenterGauge;

                case TrainMotionPhase.EmergencyBraking:
                    return BalanceChallengePattern.ImpactTiming;

                default:
                    return BalanceChallengePattern.None;
            }
        }

        private static BalanceInputDirection GetCounterDirection(
            Vector2 inertiaDirection)
        {
            if (inertiaDirection.sqrMagnitude <= 0.0001f)
            {
                return ToBalanceInputDirection(
                    PromptKeys[UnityEngine.Random.Range(0, PromptKeys.Length)]);
            }

            if (Mathf.Abs(inertiaDirection.x) >= Mathf.Abs(inertiaDirection.y))
            {
                return inertiaDirection.x > 0f
                    ? BalanceInputDirection.Left
                    : BalanceInputDirection.Right;
            }

            return inertiaDirection.y > 0f
                ? BalanceInputDirection.Down
                : BalanceInputDirection.Up;
        }

        private static BalanceInputAxis GetInputAxis(
            BalanceInputDirection direction)
        {
            return direction == BalanceInputDirection.Left ||
                   direction == BalanceInputDirection.Right
                ? BalanceInputAxis.Horizontal
                : BalanceInputAxis.Vertical;
        }

        private static float GetInertiaSign(
            Vector2 inertiaDirection,
            BalanceInputAxis axis,
            BalanceInputDirection counterDirection)
        {
            float value = axis == BalanceInputAxis.Horizontal
                ? inertiaDirection.x
                : inertiaDirection.y;
            if (Mathf.Abs(value) > 0.0001f)
            {
                return Mathf.Sign(value);
            }

            return counterDirection == BalanceInputDirection.Left ||
                   counterDirection == BalanceInputDirection.Down
                ? 1f
                : -1f;
        }

        private static Key ToKey(BalanceInputDirection direction)
        {
            switch (direction)
            {
                case BalanceInputDirection.Up:
                    return Key.W;

                case BalanceInputDirection.Left:
                    return Key.A;

                case BalanceInputDirection.Down:
                    return Key.S;

                case BalanceInputDirection.Right:
                    return Key.D;

                default:
                    return default;
            }
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

        private static BalanceInputDirection ReadPressedDirection()
        {
            if (Keyboard.current == null)
            {
                return BalanceInputDirection.None;
            }

            foreach (Key key in PromptKeys)
            {
                if (Keyboard.current[key].wasPressedThisFrame)
                {
                    return ToBalanceInputDirection(key);
                }
            }

            return BalanceInputDirection.None;
        }

        private static float ReadHeldAxis(BalanceInputAxis axis)
        {
            if (Keyboard.current == null)
            {
                return 0f;
            }

            if (axis == BalanceInputAxis.Horizontal)
            {
                return (Keyboard.current.dKey.isPressed ? 1f : 0f) -
                       (Keyboard.current.aKey.isPressed ? 1f : 0f);
            }

            return (Keyboard.current.wKey.isPressed ? 1f : 0f) -
                   (Keyboard.current.sKey.isPressed ? 1f : 0f);
        }

        private void NotifyBalanceStateChanged()
        {
            BalanceStateChanged?.Invoke(CurrentBalanceState);
        }
    }
}

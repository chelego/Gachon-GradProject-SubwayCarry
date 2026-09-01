using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerPackageCarrier : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour carryStateProviderSource;
        [SerializeField] private PlayerController facingSource;
        [SerializeField] private Transform packageAnchorFront;
        [SerializeField] private Transform packageAnchorOverhead;
        [SerializeField] private Transform packageRoot;
        [SerializeField, Min(0f)] private float frontAnchorDistance = 0.75f;

        private IPlayerCarryStateProvider carryStateProvider;

        public Transform CurrentPackage => packageRoot;
        public Transform CurrentAnchor { get; private set; }

        public void SetPackage(Transform newPackageRoot)
        {
            packageRoot = newPackageRoot;
            if (packageRoot == null)
            {
                CurrentAnchor = null;
                return;
            }

            ConfigurePackageCollisionIgnoring();

            if (carryStateProvider != null)
            {
                SyncPackageToCarryState(carryStateProvider.CurrentCarryState);
            }
        }

        private void Awake()
        {
            ResolveCarryStateProvider();
            ResolveFacingSource();
            UpdateFrontAnchorFacing();
            ConfigurePackageCollisionIgnoring();
        }

        private void OnEnable()
        {
            if (carryStateProvider == null)
            {
                ResolveCarryStateProvider();
            }

            if (facingSource == null)
            {
                ResolveFacingSource();
            }

            UpdateFrontAnchorFacing();

            if (carryStateProvider == null)
            {
                return;
            }

            carryStateProvider.CarryStateChanged -= OnCarryStateChanged;
            carryStateProvider.CarryStateChanged += OnCarryStateChanged;
            SyncPackageToCarryState(carryStateProvider.CurrentCarryState);
        }

        private void OnDisable()
        {
            if (carryStateProvider != null)
            {
                carryStateProvider.CarryStateChanged -= OnCarryStateChanged;
            }
        }

        private void LateUpdate()
        {
            UpdateFrontAnchorFacing();
        }

        private void OnCarryStateChanged(PlayerCarryStateSnapshot snapshot)
        {
            SyncPackageToCarryState(snapshot);
        }

        private void SyncPackageToCarryState(in PlayerCarryStateSnapshot snapshot)
        {
            if (snapshot.IsTransitioning || packageRoot == null)
            {
                return;
            }

            Transform targetAnchor = snapshot.Posture == CarryPosture.OverheadCarry
                ? packageAnchorOverhead
                : packageAnchorFront;
            if (targetAnchor == null)
            {
                Debug.LogWarningFormat(
                    this,
                    "{0}: No package anchor is assigned for {1}.",
                    name,
                    snapshot.Posture);
                return;
            }

            if (CurrentAnchor == targetAnchor && packageRoot.parent == targetAnchor)
            {
                return;
            }

            packageRoot.SetParent(targetAnchor, false);
            packageRoot.localPosition = Vector3.zero;
            packageRoot.localRotation = Quaternion.identity;
            CurrentAnchor = targetAnchor;
        }

        private void ConfigurePackageCollisionIgnoring()
        {
            if (packageRoot == null)
            {
                return;
            }

            PackageImpactSensor impactSensor = packageRoot.GetComponentInChildren<PackageImpactSensor>(true);
            if (impactSensor != null)
            {
                impactSensor.SetIgnoredOwnerRoot(transform);
            }
        }

        private void UpdateFrontAnchorFacing()
        {
            if (facingSource == null || packageAnchorFront == null)
            {
                return;
            }

            Vector2 worldDirection = facingSource.FacingDirection;
            if (worldDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 localDirection3D = transform.InverseTransformDirection(
                new Vector3(worldDirection.x, worldDirection.y, 0f));
            Vector2 localDirection = new Vector2(localDirection3D.x, localDirection3D.y).normalized;
            packageAnchorFront.localPosition = new Vector3(
                localDirection.x * frontAnchorDistance,
                localDirection.y * frontAnchorDistance,
                0f);

            float facingAngle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
            packageAnchorFront.localRotation = Quaternion.Euler(0f, 0f, facingAngle + 90f);
        }

        private void ResolveFacingSource()
        {
            if (facingSource == null)
            {
                facingSource = GetComponent<PlayerController>();
            }
        }

        private void ResolveCarryStateProvider()
        {
            carryStateProvider = carryStateProviderSource as IPlayerCarryStateProvider;
            if (carryStateProvider == null && carryStateProviderSource == null)
            {
                MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour is IPlayerCarryStateProvider provider)
                    {
                        carryStateProviderSource = behaviour;
                        carryStateProvider = provider;
                        break;
                    }
                }
            }

            if (carryStateProvider == null)
            {
                Debug.LogWarningFormat(
                    this,
                    "{0}: carryStateProviderSource must implement IPlayerCarryStateProvider.",
                    name);
            }
        }
    }
}

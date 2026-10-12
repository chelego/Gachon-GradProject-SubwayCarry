using System;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Gameplay
{
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class PackageDamageVisual3D : MonoBehaviour
    {
        private const string DamageModelResource = "CakeBoxDamageStages";

        [Header("References")]
        [SerializeField] private MonoBehaviour durabilityProviderSource;
        [SerializeField] private Transform originalVisual;

        [Header("Box Damage Thresholds")]
        [SerializeField, Range(0f, 100f)] private float slightBoxThreshold = 75f;
        [SerializeField, Range(0f, 100f)] private float heavyBoxThreshold = 50f;

        [Header("Runtime State")]
        [SerializeField, Tooltip("Current box damage stage for visual verification.")]
        private PackageDamageStage currentStage;

        private IPackageDurabilityProvider durabilityProvider;
        private GameObject runtimeModel;
        private Transform intactStage;
        private Transform slightStage;
        private Transform heavyStage;
        private Transform destroyedStage;
        private bool modelLoadErrorLogged;

        public PackageDamageStage CurrentStage => currentStage;

        public event Action<PackageDamageStage> DamageStageChanged;

        private void OnEnable()
        {
            ResolveDurabilityProvider();
            SubscribeToDurabilityProvider();
            EnsureDamageModel();

            SetDamageStage(
                durabilityProvider != null
                    ? EvaluateStage(durabilityProvider.CurrentDurability)
                    : PackageDamageStage.Intact,
                true);
        }

        private void OnDisable()
        {
            UnsubscribeFromDurabilityProvider();
        }

        private void OnDestroy()
        {
            UnsubscribeFromDurabilityProvider();

            if (originalVisual != null)
            {
                originalVisual.gameObject.SetActive(true);
            }
        }

        private void ResolveDurabilityProvider()
        {
            durabilityProvider = durabilityProviderSource as IPackageDurabilityProvider;
            if (durabilityProvider != null)
            {
                return;
            }

            Transform current = transform;
            while (current != null)
            {
                MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour is IPackageDurabilityProvider provider)
                    {
                        durabilityProvider = provider;
                        durabilityProviderSource = behaviour;
                        return;
                    }
                }

                current = current.parent;
            }
        }

        private void SubscribeToDurabilityProvider()
        {
            if (durabilityProvider == null)
            {
                return;
            }

            durabilityProvider.DurabilityChanged -= HandleDurabilityChanged;
            durabilityProvider.DurabilityChanged += HandleDurabilityChanged;
        }

        private void UnsubscribeFromDurabilityProvider()
        {
            if (durabilityProvider != null)
            {
                durabilityProvider.DurabilityChanged -= HandleDurabilityChanged;
            }
        }

        private void HandleDurabilityChanged(PackageDurabilitySnapshot snapshot)
        {
            SetDamageStage(EvaluateStage(snapshot), false);
        }

        private PackageDamageStage EvaluateStage(
            in PackageDurabilitySnapshot snapshot)
        {
            if (snapshot.BoxDurability <= 0f)
            {
                return PackageDamageStage.Destroyed;
            }

            if (snapshot.BoxDurability <= heavyBoxThreshold)
            {
                return PackageDamageStage.Heavy;
            }

            if (snapshot.BoxDurability <= slightBoxThreshold)
            {
                return PackageDamageStage.Slight;
            }

            return PackageDamageStage.Intact;
        }

        private void SetDamageStage(PackageDamageStage stage, bool force)
        {
            bool changed = currentStage != stage;
            currentStage = stage;

            if (force || changed)
            {
                ApplyStageVisibility();
            }

            if (changed)
            {
                DamageStageChanged?.Invoke(currentStage);
            }
        }

        private bool EnsureDamageModel()
        {
            if (runtimeModel != null)
            {
                return true;
            }

            GameObject modelAsset = Resources.Load<GameObject>(DamageModelResource);
            if (modelAsset == null)
            {
                LogModelLoadError(
                    $"Resources/{DamageModelResource}.fbx could not be loaded.");
                return false;
            }

            runtimeModel = Instantiate(modelAsset, transform, false);
            runtimeModel.name = "CakeBox Damage Stages";

            intactStage = FindDescendant(runtimeModel.transform, "Stage_Intact");
            slightStage = FindDescendant(runtimeModel.transform, "Stage_Slight");
            heavyStage = FindDescendant(runtimeModel.transform, "Stage_Heavy");
            destroyedStage = FindDescendant(runtimeModel.transform, "Stage_Destroyed");

            if (intactStage == null || slightStage == null ||
                heavyStage == null || destroyedStage == null)
            {
                LogModelLoadError(
                    "The damage model does not contain all four stage roots.");

                if (Application.isPlaying)
                {
                    Destroy(runtimeModel);
                }
                else
                {
                    DestroyImmediate(runtimeModel);
                }

                runtimeModel = null;
                intactStage = null;
                slightStage = null;
                heavyStage = null;
                destroyedStage = null;
                return false;
            }

            if (originalVisual != null)
            {
                originalVisual.gameObject.SetActive(false);
            }

            return true;
        }

        private void ApplyStageVisibility()
        {
            if (!EnsureDamageModel())
            {
                return;
            }

            intactStage.gameObject.SetActive(
                currentStage == PackageDamageStage.Intact);
            slightStage.gameObject.SetActive(
                currentStage == PackageDamageStage.Slight);
            heavyStage.gameObject.SetActive(
                currentStage == PackageDamageStage.Heavy);
            destroyedStage.gameObject.SetActive(
                currentStage == PackageDamageStage.Destroyed);
        }

        private void LogModelLoadError(string reason)
        {
            if (modelLoadErrorLogged)
            {
                return;
            }

            Debug.LogErrorFormat(
                this,
                "{0}: {1} The original cake-box visual remains active.",
                name,
                reason);
            modelLoadErrorLogged = true;
        }

        private static Transform FindDescendant(Transform root, string targetName)
        {
            if (root.name == targetName)
            {
                return root;
            }

            for (int index = 0; index < root.childCount; index++)
            {
                Transform result = FindDescendant(root.GetChild(index), targetName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void OnValidate()
        {
            heavyBoxThreshold = Mathf.Min(
                heavyBoxThreshold,
                slightBoxThreshold);
        }
    }
}

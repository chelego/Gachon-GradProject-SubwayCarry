using System;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Gameplay
{
    public enum PackageDamageStage
    {
        Intact,
        Slight,
        Heavy,
        Destroyed
    }

    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PackageDamageVisual : MonoBehaviour
    {
        private const string DamageMaterialResource =
            "SubwayCarry_CakePackage_Damage";

        private static readonly int DamageStageId =
            Shader.PropertyToID("_DamageStage");
        private static readonly int SpriteUvRectId =
            Shader.PropertyToID("_SpriteUVRect");

        [Header("References")]
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private MonoBehaviour durabilityProviderSource;
        [SerializeField] private PlayerPackageCarrier packageCarrier;

        [Header("Damage Thresholds")]
        [SerializeField, Range(0f, 100f)] private float slightBoxThreshold = 75f;
        [SerializeField, Range(0f, 100f)] private float heavyBoxThreshold = 50f;
        [SerializeField, Range(0f, 100f)] private float slightCakeThreshold = 75f;
        [SerializeField, Range(0f, 100f)] private float heavyCakeThreshold = 50f;
        [SerializeField, Range(0f, 100f)] private float destroyedCakeThreshold = 20f;

        private IPackageDurabilityProvider durabilityProvider;
        private Transform observedPackage;
        private Sprite observedSprite;
        private Material originalMaterial;
        private Color originalRendererColor = Color.white;
        private bool originalAppearanceCaptured;
        private Material runtimeMaterial;
        private MaterialPropertyBlock propertyBlock;
        [SerializeField, Tooltip("Current runtime damage stage for visual verification.")]
        private PackageDamageStage currentStage;
        private PackageDamageStage appliedStage = (PackageDamageStage)(-1);
        private bool materialLoadWarningLogged;

        public PackageDamageStage CurrentStage => currentStage;

        public event Action<PackageDamageStage> DamageStageChanged;

        public void Configure(
            SpriteRenderer renderer,
            MonoBehaviour providerSource = null)
        {
            if (renderer != null)
            {
                targetRenderer = renderer;
            }

            // Animator setup can run after the flow controller depending on
            // Unity's Awake order. A renderer-only configure call must not erase
            // an explicit durability provider that was already connected.
            if (providerSource != null)
            {
                durabilityProviderSource = providerSource;
            }
            ResolveReferences();
            InitializeMaterial();
            ResolveDurabilityProvider(true);
            ApplyVisual(true);
        }

        private void Awake()
        {
            ResolveReferences();
            InitializeMaterial();
        }

        private void OnEnable()
        {
            ResolveReferences();
            InitializeMaterial();
            ResolveDurabilityProvider(true);
            ApplyVisual(true);
        }

        private void OnDisable()
        {
            UnsubscribeFromDurabilityProvider();
        }

        private void OnDestroy()
        {
            UnsubscribeFromDurabilityProvider();

            if (targetRenderer != null &&
                runtimeMaterial != null &&
                targetRenderer.sharedMaterial == runtimeMaterial)
            {
                targetRenderer.sharedMaterial = originalMaterial;
            }

            if (targetRenderer != null && originalAppearanceCaptured)
            {
                targetRenderer.color = originalRendererColor;
            }

            if (runtimeMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(runtimeMaterial);
                }
                else
                {
                    DestroyImmediate(runtimeMaterial);
                }
            }
        }

        private void LateUpdate()
        {
            if (durabilityProviderSource == null)
            {
                ResolveDurabilityProvider(false);
            }

            ApplyVisual(false);
        }

        private void ResolveReferences()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }

            if (packageCarrier == null)
            {
                packageCarrier = GetComponentInParent<PlayerPackageCarrier>();
            }
        }

        private void InitializeMaterial()
        {
            if (targetRenderer == null)
            {
                return;
            }

            if (!originalAppearanceCaptured)
            {
                originalMaterial = targetRenderer.sharedMaterial;
                originalRendererColor = targetRenderer.color;
                originalAppearanceCaptured = true;
            }

            if (runtimeMaterial != null)
            {
                return;
            }

            Material template = Resources.Load<Material>(DamageMaterialResource);
            if (template == null)
            {
                if (!materialLoadWarningLogged)
                {
                    Debug.LogWarningFormat(
                        this,
                        "{0}: Resources/{1}.mat could not be loaded.",
                        name,
                        DamageMaterialResource);
                    materialLoadWarningLogged = true;
                }

                return;
            }

            runtimeMaterial = new Material(template)
            {
                name = template.name + " (Runtime)",
                hideFlags = HideFlags.DontSave
            };
            targetRenderer.sharedMaterial = runtimeMaterial;
            propertyBlock = new MaterialPropertyBlock();
        }

        private void ResolveDurabilityProvider(bool force)
        {
            if (durabilityProviderSource != null)
            {
                SetDurabilityProvider(
                    durabilityProviderSource as IPackageDurabilityProvider,
                    null,
                    force);
                return;
            }

            if (packageCarrier == null)
            {
                IPackageDurabilityProvider parentProvider =
                    FindDurabilityProviderInParents(transform);
                Transform providerRoot = parentProvider is MonoBehaviour behaviour
                    ? behaviour.transform
                    : null;
                SetDurabilityProvider(parentProvider, providerRoot, force);
                return;
            }

            Transform currentPackage = packageCarrier != null
                ? packageCarrier.CurrentPackage
                : null;
            if (!force && currentPackage == observedPackage)
            {
                return;
            }

            IPackageDurabilityProvider provider =
                FindDurabilityProvider(currentPackage);
            SetDurabilityProvider(provider, currentPackage, force);
        }

        private void SetDurabilityProvider(
            IPackageDurabilityProvider provider,
            Transform package,
            bool force)
        {
            if (!force && ReferenceEquals(durabilityProvider, provider) &&
                observedPackage == package)
            {
                return;
            }

            UnsubscribeFromDurabilityProvider();
            durabilityProvider = provider;
            observedPackage = package;

            if (durabilityProvider != null && isActiveAndEnabled)
            {
                durabilityProvider.DurabilityChanged +=
                    HandleDurabilityChanged;
            }

            SetDamageStage(durabilityProvider != null
                ? EvaluateStage(durabilityProvider.CurrentDurability)
                : PackageDamageStage.Intact);
        }

        private void UnsubscribeFromDurabilityProvider()
        {
            if (durabilityProvider != null)
            {
                durabilityProvider.DurabilityChanged -=
                    HandleDurabilityChanged;
            }
        }

        private void HandleDurabilityChanged(PackageDurabilitySnapshot snapshot)
        {
            SetDamageStage(EvaluateStage(snapshot));
        }

        private PackageDamageStage EvaluateStage(
            in PackageDurabilitySnapshot snapshot)
        {
            if (snapshot.DeliveryFailed ||
                snapshot.CakeDurability <= destroyedCakeThreshold)
            {
                return PackageDamageStage.Destroyed;
            }

            if (snapshot.BoxDurability <= heavyBoxThreshold ||
                snapshot.CakeDurability <= heavyCakeThreshold)
            {
                return PackageDamageStage.Heavy;
            }

            if (snapshot.BoxDurability <= slightBoxThreshold ||
                snapshot.CakeDurability <= slightCakeThreshold)
            {
                return PackageDamageStage.Slight;
            }

            return PackageDamageStage.Intact;
        }

        private void SetDamageStage(PackageDamageStage stage)
        {
            if (currentStage == stage)
            {
                return;
            }

            currentStage = stage;
            ApplyVisual(true);
            DamageStageChanged?.Invoke(currentStage);
        }

        private void ApplyVisual(bool force)
        {
            if (targetRenderer == null)
            {
                return;
            }


            targetRenderer.color = originalRendererColor *
                                   GetStageTint(currentStage);

            if (runtimeMaterial == null)
            {
                return;
            }

            Sprite sprite = targetRenderer.sprite;
            if (!force && observedSprite == sprite &&
                appliedStage == currentStage)
            {
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(DamageStageId, (float)currentStage);
            propertyBlock.SetVector(SpriteUvRectId, GetSpriteUvRect(sprite));
            targetRenderer.SetPropertyBlock(propertyBlock);

            // This renderer owns a unique runtime material. Mirroring the values
            // onto it keeps the damage stage reliable across renderer/property
            // block rebuilds performed by Unity's sprite animation path.
            runtimeMaterial.SetFloat(DamageStageId, (float)currentStage);
            runtimeMaterial.SetVector(SpriteUvRectId, GetSpriteUvRect(sprite));

            observedSprite = sprite;
            appliedStage = currentStage;
        }

        private static Color GetStageTint(PackageDamageStage stage)
        {
            switch (stage)
            {
                case PackageDamageStage.Slight:
                    return new Color(1f, 0.92f, 0.82f, 1f);

                case PackageDamageStage.Heavy:
                    return new Color(0.88f, 0.72f, 0.58f, 1f);

                case PackageDamageStage.Destroyed:
                    return new Color(0.64f, 0.45f, 0.36f, 1f);

                default:
                    return Color.white;
            }
        }

        private static Vector4 GetSpriteUvRect(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return new Vector4(0f, 0f, 1f, 1f);
            }

            Rect textureRect = sprite.textureRect;
            float textureWidth = Mathf.Max(1f, sprite.texture.width);
            float textureHeight = Mathf.Max(1f, sprite.texture.height);
            return new Vector4(
                textureRect.x / textureWidth,
                textureRect.y / textureHeight,
                textureRect.width / textureWidth,
                textureRect.height / textureHeight);
        }

        private static IPackageDurabilityProvider FindDurabilityProvider(
            Transform package)
        {
            if (package == null)
            {
                return null;
            }

            MonoBehaviour[] behaviours =
                package.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IPackageDurabilityProvider provider)
                {
                    return provider;
                }
            }

            return null;
        }

        private static IPackageDurabilityProvider FindDurabilityProviderInParents(
            Transform child)
        {
            Transform current = child;
            while (current != null)
            {
                MonoBehaviour[] behaviours =
                    current.GetComponents<MonoBehaviour>();
                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour is IPackageDurabilityProvider provider)
                    {
                        return provider;
                    }
                }

                current = current.parent;
            }

            return null;
        }

        private void OnValidate()
        {
            heavyBoxThreshold = Mathf.Min(
                heavyBoxThreshold,
                slightBoxThreshold);
            heavyCakeThreshold = Mathf.Min(
                heavyCakeThreshold,
                slightCakeThreshold);
            destroyedCakeThreshold = Mathf.Min(
                destroyedCakeThreshold,
                heavyCakeThreshold);
        }
    }
}

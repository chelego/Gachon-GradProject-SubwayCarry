using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SubwayCarry.Gameplay.Editor;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using ReviewGameplay = SubwayCarry.Gameplay;

namespace SubwayCarry.Prototype.ArtMapSlice.Editor
{
    public static class PlayerSittingSpriteSetup
    {
        private const string ReviewRoot =
            "Assets/_Project/";
        private const string SittingBodySpriteSheetPath =
            ReviewRoot + "Art/Concepts/Characters/SubwayCarry_Player_Sitting_FrontBack_v1.png";
        private const string SittingPackageSpriteSheetPath =
            ReviewRoot + "Art/Concepts/Characters/SubwayCarry_CakePackage_Sitting_FrontBack_v1.png";
        private const string ReviewPlayerPrefabPath =
            ReviewRoot + "Prefabs/Gameplay/Player.prefab";
        private const string SlicePlayerPrefabPath =
            "Assets/_Project/Scenes/Prototype/ArtMap_GameplaySlice/Player_Slice.prefab";
        private const string SourceSittingBodySpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_Player_Sitting_FrontBack_v1.png";
        private const string SourceSittingPackageSpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_CakePackage_Sitting_FrontBack_v1.png";
        private const string SourcePlayerPrefabPath =
            "Assets/_Project/Prefabs/Gameplay/Player.prefab";
        private const int SittingDirectionCount = 2;

        private static readonly string[] SittingDirectionNames =
        {
            "South",
            "North"
        };

        [MenuItem("SubwayCarry/Art/Apply Player Sitting Sprites")]
        public static void ApplyAll()
        {
            // The owner prepares shared sprite sheets. Integration only reads them.

            IReadOnlyDictionary<string, Sprite> bodySprites =
                LoadSpritesByName(SittingBodySpriteSheetPath);
            IReadOnlyDictionary<string, Sprite> packageSprites =
                LoadSpritesByName(SittingPackageSpriteSheetPath);

            ApplyToPrefab(SlicePlayerPrefabPath, bodySprites, packageSprites);

            Debug.Log("Applied shared animation data to the vertical-slice player prefab; source assets were not modified.");
        }

        [MenuItem("SubwayCarry/Art/Validate Player Sitting Sprites")]
        public static void ValidateAll()
        {
            ValidateSpriteSheet(SourceSittingBodySpriteSheetPath);
            ValidateSpriteSheet(SourceSittingPackageSpriteSheetPath);
            ValidatePrefab(SourcePlayerPrefabPath);
            ValidatePrefab(SlicePlayerPrefabPath);
            ValidateDirectionMapping(typeof(SubwayCarry.Gameplay.PlayerSpriteAnimator));

            Debug.Log("Validated sitting sprite sheets, prefab references, package renderer, and front/back direction mapping.");
        }

        private static void ConfigureSpriteSheet(
            string spriteSheetPath,
            string spritePrefix)
        {
            AssetDatabase.ImportAsset(spriteSheetPath, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer = AssetImporter.GetAtPath(spriteSheetPath) as TextureImporter;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(spriteSheetPath);
            if (importer == null || texture == null)
            {
                throw new InvalidOperationException(
                    $"Sitting sprite sheet or importer was not found for {spriteSheetPath}.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = texture.height / 2f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider dataProvider =
                factories.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            Dictionary<string, GUID> existingIds = dataProvider.GetSpriteRects()
                .GroupBy(spriteRect => spriteRect.name)
                .ToDictionary(group => group.Key, group => group.First().spriteID);
            var spriteRects = new List<SpriteRect>(SittingDirectionCount);

            for (int direction = 0; direction < SittingDirectionCount; direction++)
            {
                int left = Mathf.RoundToInt(
                    direction * texture.width / (float)SittingDirectionCount);
                int right = Mathf.RoundToInt(
                    (direction + 1) * texture.width / (float)SittingDirectionCount);
                string name = GetSpriteName(direction, spritePrefix);
                GUID spriteId = existingIds.TryGetValue(name, out GUID existingId)
                    ? existingId
                    : GUID.Generate();

                spriteRects.Add(new SpriteRect
                {
                    name = name,
                    rect = new Rect(left, 0f, right - left, texture.height),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(0.5f, 0.14f),
                    border = Vector4.zero,
                    spriteID = spriteId
                });
            }

            SpriteRect[] spriteRectArray = spriteRects.ToArray();
            dataProvider.SetSpriteRects(spriteRectArray);
            ISpriteNameFileIdDataProvider nameProvider =
                dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameProvider.SetNameFileIdPairs(spriteRectArray.Select(spriteRect =>
                new SpriteNameFileIdPair(spriteRect.name, spriteRect.spriteID)));
            dataProvider.Apply();
            importer.SaveAndReimport();
        }

        private static IReadOnlyDictionary<string, Sprite> LoadSpritesByName(
            string spriteSheetPath)
        {
            Dictionary<string, Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(spriteSheetPath)
                .OfType<Sprite>()
                .ToDictionary(sprite => sprite.name);
            if (sprites.Count != SittingDirectionCount)
            {
                throw new InvalidOperationException(
                    $"Expected {SittingDirectionCount} sprites at {spriteSheetPath}, but found {sprites.Count}.");
            }

            return sprites;
        }

        private static void ApplyToPrefab(
            string prefabPath,
            IReadOnlyDictionary<string, Sprite> bodySprites,
            IReadOnlyDictionary<string, Sprite> packageSprites)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ReviewGameplay.PlayerSpriteAnimator animator =
                    prefabRoot.GetComponent<ReviewGameplay.PlayerSpriteAnimator>();
                Transform bodyVisual = prefabRoot.transform.Find("BodyVisual");
                SpriteRenderer bodyRenderer = bodyVisual != null
                    ? bodyVisual.GetComponent<SpriteRenderer>()
                    : null;
                if (animator == null || bodyRenderer == null)
                {
                    throw new InvalidOperationException(
                        $"{prefabPath} requires the shared PlayerSpriteAnimator and BodyVisual/SpriteRenderer.");
                }

                SpriteRenderer packageRenderer =
                    EnsurePackageRenderer(prefabRoot.transform, bodyVisual, bodyRenderer);
                var serializedAnimator = new SerializedObject(animator);
                serializedAnimator.FindProperty("packageSpriteRenderer").objectReferenceValue =
                    packageRenderer;

                SerializedProperty sittingBodySprites =
                    serializedAnimator.FindProperty("sittingBodySprites");
                sittingBodySprites.arraySize = SittingDirectionCount;
                SerializedProperty sittingPackageSprites =
                    serializedAnimator.FindProperty("sittingPackageSprites");
                sittingPackageSprites.arraySize = SittingDirectionCount;

                for (int direction = 0; direction < SittingDirectionCount; direction++)
                {
                    sittingBodySprites.GetArrayElementAtIndex(direction).objectReferenceValue =
                        bodySprites[GetSpriteName(direction, "Player_Sitting")];
                    sittingPackageSprites.GetArrayElementAtIndex(direction).objectReferenceValue =
                        packageSprites[GetSpriteName(direction, "CakePackage_Sitting")];
                }

                // Keep the slice's movement/outline overrides; reuse the shared animation data.
                var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePlayerPrefabPath);
                var sourceComponent = sourcePrefab != null
                    ? sourcePrefab.GetComponent<ReviewGameplay.PlayerSpriteAnimator>()
                    : null;
                if (sourceComponent == null)
                    throw new InvalidOperationException($"Missing shared PlayerSpriteAnimator at {SourcePlayerPrefabPath}.");
                var sourceAnimator = new SerializedObject(sourceComponent);
                foreach (string field in new[] { "fallenBodySprites", "fallenPackageSprites", "fallenFramesPerDirection", "fallenFramesPerSecond" })
                    serializedAnimator.CopyFromSerializedProperty(sourceAnimator.FindProperty(field));
                serializedAnimator.ApplyModifiedPropertiesWithoutUndo();

                ReviewGameplay.PlayerPackageCarrier carrier =
                    prefabRoot.GetComponent<ReviewGameplay.PlayerPackageCarrier>();
                if (carrier != null)
                {
                    var serializedCarrier = new SerializedObject(carrier);
                    serializedCarrier.FindProperty("sittingAnchorDistance").floatValue = 0.35f;
                    serializedCarrier.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static SpriteRenderer EnsurePackageRenderer(
            Transform prefabRoot,
            Transform bodyVisual,
            SpriteRenderer bodyRenderer)
        {
            Transform packageVisual = prefabRoot.Find("PackageVisual");
            if (packageVisual == null)
            {
                var packageObject = new GameObject("PackageVisual");
                packageObject.layer = bodyVisual.gameObject.layer;
                packageVisual = packageObject.transform;
                packageVisual.SetParent(prefabRoot, false);
            }

            packageVisual.localPosition = bodyVisual.localPosition;
            packageVisual.localRotation = bodyVisual.localRotation;
            packageVisual.localScale = bodyVisual.localScale;

            SpriteRenderer packageRenderer = packageVisual.GetComponent<SpriteRenderer>();
            if (packageRenderer == null)
            {
                packageRenderer = packageVisual.gameObject.AddComponent<SpriteRenderer>();
            }

            packageRenderer.sprite = null;
            packageRenderer.color = Color.white;
            packageRenderer.sharedMaterial = bodyRenderer.sharedMaterial;
            packageRenderer.drawMode = SpriteDrawMode.Simple;
            packageRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            packageRenderer.sortingOrder = bodyRenderer.sortingOrder + 1;
            packageRenderer.enabled = false;
            return packageRenderer;
        }

        private static void ValidateSpriteSheet(string spriteSheetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(spriteSheetPath) as TextureImporter;
            if (importer == null ||
                importer.spriteImportMode != SpriteImportMode.Multiple ||
                importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                throw new InvalidOperationException(
                    $"Sitting sprite import settings are invalid at {spriteSheetPath}.");
            }

            if (AssetDatabase.LoadAllAssetsAtPath(spriteSheetPath).OfType<Sprite>().Count() !=
                SittingDirectionCount)
            {
                throw new InvalidOperationException(
                    $"Sitting sprite sheet must contain two sprites at {spriteSheetPath}.");
            }
        }

        private static void ValidatePrefab(string prefabPath)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                MonoBehaviour animator = prefabRoot.GetComponents<MonoBehaviour>()
                    .SingleOrDefault(component => component != null &&
                        component.GetType().Name == nameof(SubwayCarry.Gameplay.PlayerSpriteAnimator));
                if (animator == null)
                {
                    throw new InvalidOperationException(
                        $"PlayerSpriteAnimator is missing from {prefabPath}.");
                }

                var serializedAnimator = new SerializedObject(animator);
                ValidateSpriteArray(
                    serializedAnimator.FindProperty("sittingBodySprites"),
                    prefabPath,
                    "sittingBodySprites");
                ValidateSpriteArray(
                    serializedAnimator.FindProperty("sittingPackageSprites"),
                    prefabPath,
                    "sittingPackageSprites");

                if (serializedAnimator.FindProperty("packageSpriteRenderer").objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        $"PackageVisual SpriteRenderer is not connected in {prefabPath}.");
                }

                MonoBehaviour carrier = prefabRoot.GetComponents<MonoBehaviour>()
                    .SingleOrDefault(component => component != null &&
                        component.GetType().Name == nameof(SubwayCarry.Gameplay.PlayerPackageCarrier));
                if (carrier == null ||
                    !Mathf.Approximately(
                        new SerializedObject(carrier)
                            .FindProperty("sittingAnchorDistance").floatValue,
                        0.35f))
                {
                    throw new InvalidOperationException(
                        $"Sitting package anchor distance is invalid in {prefabPath}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ValidateSpriteArray(
            SerializedProperty sprites,
            string prefabPath,
            string propertyName)
        {
            if (sprites == null || sprites.arraySize != SittingDirectionCount)
            {
                throw new InvalidOperationException(
                    $"{propertyName} must contain two sprites in {prefabPath}.");
            }

            for (int index = 0; index < sprites.arraySize; index++)
            {
                if (sprites.GetArrayElementAtIndex(index).objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        $"{propertyName}[{index}] is missing in {prefabPath}.");
                }
            }
        }

        private static void ValidateDirectionMapping(Type animatorType)
        {
            MethodInfo mapping = animatorType.GetMethod(
                "GetSittingDirectionIndex",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (mapping == null)
            {
                throw new InvalidOperationException(
                    $"Sitting direction mapping is missing from {animatorType.FullName}.");
            }

            for (int direction = 0; direction < 8; direction++)
            {
                int expected = direction >= 3 && direction <= 5 ? 1 : 0;
                int actual = (int)mapping.Invoke(null, new object[] { direction });
                if (actual != expected)
                {
                    throw new InvalidOperationException(
                        $"Unexpected sitting direction mapping in {animatorType.FullName}: {direction} -> {actual}.");
                }
            }
        }

        private static string GetSpriteName(int direction, string spritePrefix)
        {
            return $"{spritePrefix}_{SittingDirectionNames[direction]}";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace SubwayCarry.Gameplay.Editor
{
    public static class PlayerSpriteSetupUtility
    {
        private const string UnarmedSpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_Player_Unarmed_8Dir_IdleWalk_v1.png";
        private const string CarryingPoseSpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_Player_CarryingPose_8Dir_IdleWalk_v1.png";
        private const string CakePackageSpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_CakePackage_8Dir_IdleWalk_v1.png";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Gameplay/Player.prefab";
        private const int DirectionCount = 8;
        private const int RowCount = 4;
        private const int WalkFrameCount = 3;

        private static readonly string[] DirectionNames =
        {
            "South",
            "SouthWest",
            "West",
            "NorthWest",
            "North",
            "NorthEast",
            "East",
            "SouthEast"
        };

        [MenuItem("SubwayCarry/Art/Apply Player 8-Direction Sprites")]
        public static void ApplyPlayerSprites()
        {
            ConfigureSpriteSheet(UnarmedSpriteSheetPath, "Player_Unarmed");
            ConfigureSpriteSheet(CarryingPoseSpriteSheetPath, "Player_CarryingPose");
            ConfigureSpriteSheet(CakePackageSpriteSheetPath, "CakePackage");
            IReadOnlyDictionary<string, Sprite> unarmedSprites =
                LoadSpritesByName(UnarmedSpriteSheetPath);
            IReadOnlyDictionary<string, Sprite> carryingSprites =
                LoadSpritesByName(CarryingPoseSpriteSheetPath);
            IReadOnlyDictionary<string, Sprite> packageSprites =
                LoadSpritesByName(CakePackageSpriteSheetPath);
            ApplySpritesToPlayerPrefab(unarmedSprites, carryingSprites, packageSprites);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"Applied {unarmedSprites.Count} unarmed, {carryingSprites.Count} carrying-pose, and {packageSprites.Count} package sprites to {PlayerPrefabPath}.");
        }

        private static void ConfigureSpriteSheet(string spriteSheetPath, string spritePrefix)
        {
            AssetDatabase.ImportAsset(spriteSheetPath, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer = AssetImporter.GetAtPath(spriteSheetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Texture importer was not found for {spriteSheetPath}.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 160f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(spriteSheetPath);
            if (texture == null)
            {
                throw new InvalidOperationException($"Texture asset was not found at {spriteSheetPath}.");
            }

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            Dictionary<string, GUID> existingIds = dataProvider.GetSpriteRects()
                .GroupBy(spriteRect => spriteRect.name)
                .ToDictionary(group => group.Key, group => group.First().spriteID);
            var spriteRects = new List<SpriteRect>(DirectionCount * RowCount);

            for (int row = 0; row < RowCount; row++)
            {
                int top = Mathf.RoundToInt(row * texture.height / (float)RowCount);
                int bottom = Mathf.RoundToInt((row + 1) * texture.height / (float)RowCount);

                for (int direction = 0; direction < DirectionCount; direction++)
                {
                    int left = Mathf.RoundToInt(direction * texture.width / (float)DirectionCount);
                    int right = Mathf.RoundToInt((direction + 1) * texture.width / (float)DirectionCount);
                    string name = GetSpriteName(direction, row, spritePrefix);
                    GUID spriteId = existingIds.TryGetValue(name, out GUID existingId)
                        ? existingId
                        : GUID.Generate();

                    spriteRects.Add(new SpriteRect
                    {
                        name = name,
                        rect = new Rect(left, texture.height - bottom, right - left, bottom - top),
                        alignment = SpriteAlignment.Custom,
                        pivot = new Vector2(0.5f, 0.14f),
                        border = Vector4.zero,
                        spriteID = spriteId
                    });
                }
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

            int expectedCount = DirectionCount * RowCount;
            if (sprites.Count != expectedCount)
            {
                throw new InvalidOperationException(
                    $"Expected {expectedCount} sprites at {spriteSheetPath}, but found {sprites.Count}.");
            }

            return sprites;
        }

        private static void ApplySpritesToPlayerPrefab(
            IReadOnlyDictionary<string, Sprite> unarmedSprites,
            IReadOnlyDictionary<string, Sprite> carryingSprites,
            IReadOnlyDictionary<string, Sprite> packageSprites)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            try
            {
                PlayerController controller = prefabRoot.GetComponent<PlayerController>();
                PlayerPosture posture = prefabRoot.GetComponent<PlayerPosture>();
                Transform bodyVisual = prefabRoot.transform.Find("BodyVisual");
                SpriteRenderer spriteRenderer = bodyVisual != null ? bodyVisual.GetComponent<SpriteRenderer>() : null;

                if (controller == null || posture == null || spriteRenderer == null)
                {
                    throw new InvalidOperationException(
                        "Player prefab requires PlayerController, PlayerPosture, and BodyVisual/SpriteRenderer.");
                }

                Transform packageVisual = prefabRoot.transform.Find("PackageVisual");
                if (packageVisual == null)
                {
                    var packageObject = new GameObject("PackageVisual");
                    packageObject.layer = bodyVisual.gameObject.layer;
                    packageVisual = packageObject.transform;
                    packageVisual.SetParent(prefabRoot.transform, false);
                }
                packageVisual.localPosition = bodyVisual.localPosition;
                packageVisual.localRotation = bodyVisual.localRotation;
                packageVisual.localScale = bodyVisual.localScale;

                SpriteRenderer packageSpriteRenderer =
                    packageVisual.GetComponent<SpriteRenderer>();
                if (packageSpriteRenderer == null)
                {
                    packageSpriteRenderer =
                        packageVisual.gameObject.AddComponent<SpriteRenderer>();
                }
                packageSpriteRenderer.sprite = null;
                packageSpriteRenderer.color = Color.white;
                packageSpriteRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
                packageSpriteRenderer.drawMode = SpriteDrawMode.Simple;
                packageSpriteRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                packageSpriteRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
                packageSpriteRenderer.enabled = false;

                PlayerSpriteAnimator animator = prefabRoot.GetComponent<PlayerSpriteAnimator>();
                if (animator == null)
                {
                    animator = prefabRoot.AddComponent<PlayerSpriteAnimator>();
                }

                var serializedAnimator = new SerializedObject(animator);
                serializedAnimator.FindProperty("controller").objectReferenceValue = controller;
                serializedAnimator.FindProperty("posture").objectReferenceValue = posture;
                serializedAnimator.FindProperty("packageCarrier").objectReferenceValue =
                    prefabRoot.GetComponent<PlayerPackageCarrier>();
                serializedAnimator.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
                serializedAnimator.FindProperty("packageSpriteRenderer").objectReferenceValue =
                    packageSpriteRenderer;
                serializedAnimator.FindProperty("walkFramesPerDirection").intValue = WalkFrameCount;
                serializedAnimator.FindProperty("walkFramesPerSecond").floatValue = 8f;
                serializedAnimator.FindProperty("useMovementDirectionWhileMoving").boolValue = true;

                SerializedProperty idleSprites = serializedAnimator.FindProperty("idleSprites");
                idleSprites.arraySize = DirectionCount;

                SerializedProperty walkSprites = serializedAnimator.FindProperty("walkSprites");
                walkSprites.arraySize = DirectionCount * WalkFrameCount;

                SerializedProperty carryingIdleSprites =
                    serializedAnimator.FindProperty("carryingIdleSprites");
                carryingIdleSprites.arraySize = DirectionCount;

                SerializedProperty carryingWalkSprites =
                    serializedAnimator.FindProperty("carryingWalkSprites");
                carryingWalkSprites.arraySize = DirectionCount * WalkFrameCount;

                SerializedProperty packageIdleSprites =
                    serializedAnimator.FindProperty("packageIdleSprites");
                packageIdleSprites.arraySize = DirectionCount;

                SerializedProperty packageWalkSprites =
                    serializedAnimator.FindProperty("packageWalkSprites");
                packageWalkSprites.arraySize = DirectionCount * WalkFrameCount;

                for (int direction = 0; direction < DirectionCount; direction++)
                {
                    idleSprites.GetArrayElementAtIndex(direction).objectReferenceValue =
                        unarmedSprites[GetSpriteName(direction, 0, "Player_Unarmed")];
                    carryingIdleSprites.GetArrayElementAtIndex(direction).objectReferenceValue =
                        carryingSprites[GetSpriteName(direction, 0, "Player_CarryingPose")];
                    packageIdleSprites.GetArrayElementAtIndex(direction).objectReferenceValue =
                        packageSprites[GetSpriteName(direction, 0, "CakePackage")];

                    for (int frame = 0; frame < WalkFrameCount; frame++)
                    {
                        int destinationIndex = direction * WalkFrameCount + frame;
                        walkSprites.GetArrayElementAtIndex(destinationIndex).objectReferenceValue =
                            unarmedSprites[GetSpriteName(direction, frame + 1, "Player_Unarmed")];
                        carryingWalkSprites.GetArrayElementAtIndex(destinationIndex).objectReferenceValue =
                            carryingSprites[GetSpriteName(direction, frame + 1, "Player_CarryingPose")];
                        packageWalkSprites.GetArrayElementAtIndex(destinationIndex).objectReferenceValue =
                            packageSprites[GetSpriteName(direction, frame + 1, "CakePackage")];
                    }
                }

                serializedAnimator.ApplyModifiedPropertiesWithoutUndo();

                spriteRenderer.sprite = unarmedSprites[GetSpriteName(0, 0, "Player_Unarmed")];
                spriteRenderer.color = Color.white;
                spriteRenderer.drawMode = SpriteDrawMode.Simple;
                spriteRenderer.sortingOrder = 1;
                bodyVisual.localScale = Vector3.one;
                packageVisual.localScale = bodyVisual.localScale;

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static string GetSpriteName(int direction, int row, string spritePrefix)
        {
            return row == 0
                ? $"{spritePrefix}_{DirectionNames[direction]}_Idle"
                : $"{spritePrefix}_{DirectionNames[direction]}_Walk_{row - 1}";
        }
    }
}

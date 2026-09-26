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
        private const string SittingBodySpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_Player_Sitting_FrontBack_v1.png";
        private const string SittingPackageSpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_CakePackage_Sitting_FrontBack_v1.png";
        private const string HoldingSupportBodySpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_Player_HoldingSupport_FrontBack_v1.png";
        private const string SittingTransitionBodySpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_Player_SittingTransition_FrontBack_5Frame_v2.png";
        private const string SittingTransitionPackageSpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_CakePackage_SittingTransition_FrontBack_5Frame_v2.png";
        private const string OverheadBodySpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_Player_OverheadCarry_8Dir_IdleWalk_v1.png";
        private const string OverheadPackageSpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_CakePackage_OverheadCarry_8Dir_IdleWalk_v1.png";
        private const string OverheadTransitionBodySpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_Player_OverheadTransition_FrontBack_5Frame_v2.png";
        private const string OverheadTransitionPackageSpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_CakePackage_OverheadTransition_FrontBack_5Frame_v1.png";
        private const string FallenBodySpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_Player_Fallen_FrontBack_5Frame_v1.png";
        private const string FallenPackageSpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_CakePackage_Fallen_FrontBack_5Frame_v1.png";
        private const string FallenBodySideSpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_Player_Fallen_LeftRight_5Frame_v1.png";
        private const string FallenPackageSideSpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_CakePackage_Fallen_LeftRight_5Frame_v1.png";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Gameplay/Player.prefab";
        private const int DirectionCount = 8;
        private const int RowCount = 4;
        private const int WalkFrameCount = 3;
        private const int SittingDirectionCount = 2;
        private const int SittingTransitionFrameCount = 5;
        private const int OverheadTransitionDirectionCount = 2;
        private const int OverheadTransitionFrameCount = 5;
        private const int FallenDirectionCount = 4;
        private const int FallenFrameCount = 5;
        private const int FallenSourceHeight = 1536;

        private static readonly int[] FallenTopBounds =
        {
            0,
            380,
            713,
            1029,
            1305,
            FallenSourceHeight
        };

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

        private static readonly string[] SittingDirectionNames =
        {
            "South",
            "North"
        };

        // Direction-major pixel offsets that keep the character centered on the
        // standing pose while the generated sitting frames play.
        private static readonly int[] SittingTransitionVisualOffsetsPixels =
        {
            -9, -5, 10, 26, 43,
            -9, -6, 8, 27, 43
        };

        // Absolute pivot positions in each 192x384 cell. These match the
        // original carrying sheet's body center and foot baseline.
        private static readonly Vector2[] OverheadSpritePivotPixels =
        {
            new Vector2(99.5f, 33.8f),
            new Vector2(99.0f, 34.8f),
            new Vector2(106.0f, 33.8f),
            new Vector2(100.5f, 33.8f),
            new Vector2(90.0f, 34.8f),
            new Vector2(86.5f, 33.8f),
            new Vector2(81.5f, 34.8f),
            new Vector2(86.5f, 33.8f),
            new Vector2(98.0f, 41.8f),
            new Vector2(98.0f, 39.8f),
            new Vector2(91.5f, 38.8f),
            new Vector2(91.0f, 39.8f),
            new Vector2(92.0f, 39.8f),
            new Vector2(89.0f, 39.8f),
            new Vector2(88.0f, 41.8f),
            new Vector2(85.5f, 39.8f),
            new Vector2(100.0f, 41.8f),
            new Vector2(96.0f, 40.8f),
            new Vector2(91.5f, 44.8f),
            new Vector2(88.5f, 42.8f),
            new Vector2(90.5f, 43.8f),
            new Vector2(95.0f, 48.8f),
            new Vector2(89.0f, 45.8f),
            new Vector2(88.5f, 42.8f),
            new Vector2(101.5f, 48.8f),
            new Vector2(92.5f, 45.8f),
            new Vector2(91.0f, 45.8f),
            new Vector2(90.5f, 46.8f),
            new Vector2(91.5f, 46.8f),
            new Vector2(92.5f, 44.8f),
            new Vector2(93.0f, 45.8f),
            new Vector2(89.5f, 46.8f)
        };

        // Absolute pivot positions in direction-major order. The generated
        // front/back rows have different margins, so both axes are corrected.
        private static readonly Vector2[] OverheadTransitionPivotPixels =
        {
            new Vector2(187.913f, 5.162f),
            new Vector2(178.913f, 4.162f),
            new Vector2(167.413f, 4.162f),
            new Vector2(152.413f, 4.162f),
            new Vector2(147.413f, 4.162f),
            new Vector2(188.798f, 41.706f),
            new Vector2(180.798f, 41.706f),
            new Vector2(168.298f, 41.706f),
            new Vector2(153.798f, 41.706f),
            new Vector2(149.798f, 41.706f)
        };

        private static readonly string[] FallenDirectionNames =
        {
            "South",
            "West",
            "North",
            "East"
        };

        private static readonly string[] FallenFrontBackDirectionNames =
        {
            "South",
            "North"
        };

        private static readonly string[] FallenSideDirectionNames =
        {
            "West",
            "East"
        };

        [MenuItem("SubwayCarry/Art/Apply Player 8-Direction Sprites")]
        public static void ApplyPlayerSprites()
        {
            ConfigureSpriteSheet(UnarmedSpriteSheetPath, "Player_Unarmed");
            ConfigureSpriteSheet(CarryingPoseSpriteSheetPath, "Player_CarryingPose");
            ConfigureSpriteSheet(CakePackageSpriteSheetPath, "CakePackage");
            ConfigureSittingSpriteSheet(SittingBodySpriteSheetPath, "Player_Sitting");
            ConfigureSittingSpriteSheet(SittingPackageSpriteSheetPath, "CakePackage_Sitting");
            ConfigureSittingSpriteSheet(
                HoldingSupportBodySpriteSheetPath,
                "Player_HoldingSupport");
            ConfigureSittingTransitionSpriteSheet(
                SittingTransitionBodySpriteSheetPath,
                "Player_SittingTransition");
            ConfigureSittingTransitionSpriteSheet(
                SittingTransitionPackageSpriteSheetPath,
                "CakePackage_SittingTransition");
            ConfigureSpriteSheet(
                OverheadBodySpriteSheetPath,
                "Player_OverheadCarry",
                OverheadSpritePivotPixels);
            ConfigureSpriteSheet(
                OverheadPackageSpriteSheetPath,
                "CakePackage_OverheadCarry",
                OverheadSpritePivotPixels);
            ConfigureSittingTransitionSpriteSheet(
                OverheadTransitionBodySpriteSheetPath,
                "Player_OverheadTransition",
                OverheadTransitionPivotPixels);
            ConfigureSittingTransitionSpriteSheet(
                OverheadTransitionPackageSpriteSheetPath,
                "CakePackage_OverheadTransition",
                OverheadTransitionPivotPixels);
            ConfigureFallenSpriteSheet(
                FallenBodySpriteSheetPath,
                "Player_Fallen",
                FallenFrontBackDirectionNames);
            ConfigureFallenSpriteSheet(
                FallenPackageSpriteSheetPath,
                "CakePackage_Fallen",
                FallenFrontBackDirectionNames);
            ConfigureFallenSpriteSheet(
                FallenBodySideSpriteSheetPath,
                "Player_Fallen",
                FallenSideDirectionNames);
            ConfigureFallenSpriteSheet(
                FallenPackageSideSpriteSheetPath,
                "CakePackage_Fallen",
                FallenSideDirectionNames);
            IReadOnlyDictionary<string, Sprite> unarmedSprites =
                LoadSpritesByName(UnarmedSpriteSheetPath, DirectionCount * RowCount);
            IReadOnlyDictionary<string, Sprite> carryingSprites =
                LoadSpritesByName(CarryingPoseSpriteSheetPath, DirectionCount * RowCount);
            IReadOnlyDictionary<string, Sprite> packageSprites =
                LoadSpritesByName(CakePackageSpriteSheetPath, DirectionCount * RowCount);
            IReadOnlyDictionary<string, Sprite> sittingBodySprites =
                LoadSpritesByName(SittingBodySpriteSheetPath, SittingDirectionCount);
            IReadOnlyDictionary<string, Sprite> sittingPackageSprites =
                LoadSpritesByName(SittingPackageSpriteSheetPath, SittingDirectionCount);
            IReadOnlyDictionary<string, Sprite> holdingSupportBodySprites =
                LoadSpritesByName(HoldingSupportBodySpriteSheetPath, SittingDirectionCount);
            IReadOnlyDictionary<string, Sprite> sittingTransitionBodySprites =
                LoadSpritesByName(
                    SittingTransitionBodySpriteSheetPath,
                    SittingDirectionCount * SittingTransitionFrameCount);
            IReadOnlyDictionary<string, Sprite> sittingTransitionPackageSprites =
                LoadSpritesByName(
                    SittingTransitionPackageSpriteSheetPath,
                    SittingDirectionCount * SittingTransitionFrameCount);
            IReadOnlyDictionary<string, Sprite> overheadBodySprites =
                LoadSpritesByName(OverheadBodySpriteSheetPath, DirectionCount * RowCount);
            IReadOnlyDictionary<string, Sprite> overheadPackageSprites =
                LoadSpritesByName(OverheadPackageSpriteSheetPath, DirectionCount * RowCount);
            IReadOnlyDictionary<string, Sprite> overheadTransitionBodySprites =
                LoadSpritesByName(
                    OverheadTransitionBodySpriteSheetPath,
                    OverheadTransitionDirectionCount * OverheadTransitionFrameCount);
            IReadOnlyDictionary<string, Sprite> overheadTransitionPackageSprites =
                LoadSpritesByName(
                    OverheadTransitionPackageSpriteSheetPath,
                    OverheadTransitionDirectionCount * OverheadTransitionFrameCount);
            IReadOnlyDictionary<string, Sprite> fallenBodySprites =
                LoadSpritesByName(FallenBodySpriteSheetPath, 2 * FallenFrameCount)
                    .Concat(LoadSpritesByName(
                        FallenBodySideSpriteSheetPath,
                        2 * FallenFrameCount))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
            IReadOnlyDictionary<string, Sprite> fallenPackageSprites =
                LoadSpritesByName(FallenPackageSpriteSheetPath, 2 * FallenFrameCount)
                    .Concat(LoadSpritesByName(
                        FallenPackageSideSpriteSheetPath,
                        2 * FallenFrameCount))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
            ApplySpritesToPlayerPrefab(
                unarmedSprites,
                carryingSprites,
                packageSprites,
                sittingBodySprites,
                sittingPackageSprites,
                holdingSupportBodySprites,
                sittingTransitionBodySprites,
                sittingTransitionPackageSprites,
                overheadBodySprites,
                overheadPackageSprites,
                overheadTransitionBodySprites,
                overheadTransitionPackageSprites,
                fallenBodySprites,
                fallenPackageSprites);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"Applied walking, sitting, holding-support, overhead carry, overhead transition, and fallen body/package sprites to {PlayerPrefabPath}.");
        }

        private static void ConfigureSpriteSheet(
            string spriteSheetPath,
            string spritePrefix,
            IReadOnlyList<Vector2> pivotPixels = null)
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

            if (pivotPixels != null && pivotPixels.Count != DirectionCount * RowCount)
            {
                throw new InvalidOperationException(
                    $"Expected {DirectionCount * RowCount} pivots for {spriteSheetPath}, but found {pivotPixels.Count}.");
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
                    int spriteIndex = row * DirectionCount + direction;
                    Vector2 pivot = pivotPixels == null
                        ? new Vector2(0.5f, 0.14f)
                        : new Vector2(
                            pivotPixels[spriteIndex].x / (right - left),
                            pivotPixels[spriteIndex].y / (bottom - top));

                    spriteRects.Add(new SpriteRect
                    {
                        name = name,
                        rect = new Rect(left, texture.height - bottom, right - left, bottom - top),
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
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

        private static void ConfigureSittingSpriteSheet(
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
                int left = Mathf.RoundToInt(direction * texture.width / (float)SittingDirectionCount);
                int right = Mathf.RoundToInt((direction + 1) * texture.width / (float)SittingDirectionCount);
                string name = GetSittingSpriteName(direction, spritePrefix);
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

        private static void ConfigureFallenSpriteSheet(
            string spriteSheetPath,
            string spritePrefix,
            IReadOnlyList<string> directionNames)
        {
            AssetDatabase.ImportAsset(spriteSheetPath, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer = AssetImporter.GetAtPath(spriteSheetPath) as TextureImporter;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(spriteSheetPath);
            if (importer == null || texture == null)
            {
                throw new InvalidOperationException(
                    $"Fallen sprite sheet or importer was not found for {spriteSheetPath}.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 256f;
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
            var spriteRects = new List<SpriteRect>(directionNames.Count * FallenFrameCount);

            for (int direction = 0; direction < directionNames.Count; direction++)
            {
                int left = Mathf.RoundToInt(
                    direction * texture.width / (float)directionNames.Count);
                int right = Mathf.RoundToInt(
                    (direction + 1) * texture.width / (float)directionNames.Count);

                for (int frame = 0; frame < FallenFrameCount; frame++)
                {
                    int top = Mathf.RoundToInt(
                        FallenTopBounds[frame] * texture.height / (float)FallenSourceHeight);
                    int bottom = Mathf.RoundToInt(
                        FallenTopBounds[frame + 1] * texture.height / (float)FallenSourceHeight);
                    string name = GetFallenSpriteName(
                        directionNames[direction],
                        frame,
                        spritePrefix);
                    GUID spriteId = existingIds.TryGetValue(name, out GUID existingId)
                        ? existingId
                        : GUID.Generate();

                    spriteRects.Add(new SpriteRect
                    {
                        name = name,
                        rect = new Rect(
                            left,
                            texture.height - bottom,
                            right - left,
                            bottom - top),
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

        private static void ConfigureSittingTransitionSpriteSheet(
            string spriteSheetPath,
            string spritePrefix,
            IReadOnlyList<Vector2> pivotPixels = null)
        {
            AssetDatabase.ImportAsset(spriteSheetPath, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer = AssetImporter.GetAtPath(spriteSheetPath) as TextureImporter;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(spriteSheetPath);
            if (importer == null || texture == null)
            {
                throw new InvalidOperationException(
                    $"Front/back transition sprite sheet or importer was not found for {spriteSheetPath}.");
            }

            if (pivotPixels != null &&
                pivotPixels.Count != SittingDirectionCount * SittingTransitionFrameCount)
            {
                throw new InvalidOperationException(
                    $"Expected {SittingDirectionCount * SittingTransitionFrameCount} pivots for {spriteSheetPath}, but found {pivotPixels.Count}.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = texture.height / (SittingDirectionCount * 2f);
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
            var spriteRects = new List<SpriteRect>(
                SittingDirectionCount * SittingTransitionFrameCount);

            for (int direction = 0; direction < SittingDirectionCount; direction++)
            {
                int top = Mathf.RoundToInt(
                    direction * texture.height / (float)SittingDirectionCount);
                int bottom = Mathf.RoundToInt(
                    (direction + 1) * texture.height / (float)SittingDirectionCount);

                for (int frame = 0; frame < SittingTransitionFrameCount; frame++)
                {
                    int left = Mathf.RoundToInt(
                        frame * texture.width / (float)SittingTransitionFrameCount);
                    int right = Mathf.RoundToInt(
                        (frame + 1) * texture.width / (float)SittingTransitionFrameCount);
                    int spriteIndex = direction * SittingTransitionFrameCount + frame;
                    Vector2 pivot = pivotPixels == null
                        ? new Vector2(
                            0.5f - SittingTransitionVisualOffsetsPixels[spriteIndex] /
                            (float)(right - left),
                            0.14f)
                        : new Vector2(
                            pivotPixels[spriteIndex].x / (right - left),
                            pivotPixels[spriteIndex].y / (bottom - top));
                    string name = GetSittingTransitionSpriteName(
                        direction,
                        frame,
                        spritePrefix);
                    GUID spriteId = existingIds.TryGetValue(name, out GUID existingId)
                        ? existingId
                        : GUID.Generate();

                    spriteRects.Add(new SpriteRect
                    {
                        name = name,
                        rect = new Rect(
                            left,
                            texture.height - bottom,
                            right - left,
                            bottom - top),
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
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
            string spriteSheetPath,
            int expectedCount)
        {
            Dictionary<string, Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(spriteSheetPath)
                .OfType<Sprite>()
                .ToDictionary(sprite => sprite.name);

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
            IReadOnlyDictionary<string, Sprite> packageSprites,
            IReadOnlyDictionary<string, Sprite> sittingBodySprites,
            IReadOnlyDictionary<string, Sprite> sittingPackageSprites,
            IReadOnlyDictionary<string, Sprite> holdingSupportBodySprites,
            IReadOnlyDictionary<string, Sprite> sittingTransitionBodySprites,
            IReadOnlyDictionary<string, Sprite> sittingTransitionPackageSprites,
            IReadOnlyDictionary<string, Sprite> overheadBodySprites,
            IReadOnlyDictionary<string, Sprite> overheadPackageSprites,
            IReadOnlyDictionary<string, Sprite> overheadTransitionBodySprites,
            IReadOnlyDictionary<string, Sprite> overheadTransitionPackageSprites,
            IReadOnlyDictionary<string, Sprite> fallenBodySprites,
            IReadOnlyDictionary<string, Sprite> fallenPackageSprites)
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
                serializedAnimator.FindProperty("sittingTransitionFramesPerDirection").intValue =
                    SittingTransitionFrameCount;
                serializedAnimator.FindProperty("overheadTransitionFramesPerDirection").intValue =
                    OverheadTransitionFrameCount;
                serializedAnimator.FindProperty("fallenFramesPerDirection").intValue =
                    FallenFrameCount;
                serializedAnimator.FindProperty("fallenFramesPerSecond").floatValue = 9f;
                serializedAnimator.FindProperty("useMovementDirectionWhileMoving").boolValue = false;

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

                SerializedProperty sittingBodySpriteArray =
                    serializedAnimator.FindProperty("sittingBodySprites");
                sittingBodySpriteArray.arraySize = SittingDirectionCount;

                SerializedProperty sittingPackageSpriteArray =
                    serializedAnimator.FindProperty("sittingPackageSprites");
                sittingPackageSpriteArray.arraySize = SittingDirectionCount;

                SerializedProperty holdingSupportBodySpriteArray =
                    serializedAnimator.FindProperty("holdingSupportBodySprites");
                holdingSupportBodySpriteArray.arraySize = SittingDirectionCount;

                SerializedProperty sittingTransitionBodySpriteArray =
                    serializedAnimator.FindProperty("sittingTransitionBodySprites");
                sittingTransitionBodySpriteArray.arraySize =
                    SittingDirectionCount * SittingTransitionFrameCount;

                SerializedProperty sittingTransitionPackageSpriteArray =
                    serializedAnimator.FindProperty("sittingTransitionPackageSprites");
                sittingTransitionPackageSpriteArray.arraySize =
                    SittingDirectionCount * SittingTransitionFrameCount;

                SerializedProperty overheadIdleSpriteArray =
                    serializedAnimator.FindProperty("overheadIdleSprites");
                overheadIdleSpriteArray.arraySize = DirectionCount;

                SerializedProperty overheadWalkSpriteArray =
                    serializedAnimator.FindProperty("overheadWalkSprites");
                overheadWalkSpriteArray.arraySize = DirectionCount * WalkFrameCount;

                SerializedProperty overheadPackageIdleSpriteArray =
                    serializedAnimator.FindProperty("overheadPackageIdleSprites");
                overheadPackageIdleSpriteArray.arraySize = DirectionCount;

                SerializedProperty overheadPackageWalkSpriteArray =
                    serializedAnimator.FindProperty("overheadPackageWalkSprites");
                overheadPackageWalkSpriteArray.arraySize = DirectionCount * WalkFrameCount;

                SerializedProperty overheadTransitionBodySpriteArray =
                    serializedAnimator.FindProperty("overheadTransitionBodySprites");
                overheadTransitionBodySpriteArray.arraySize =
                    OverheadTransitionDirectionCount * OverheadTransitionFrameCount;

                SerializedProperty overheadTransitionPackageSpriteArray =
                    serializedAnimator.FindProperty("overheadTransitionPackageSprites");
                overheadTransitionPackageSpriteArray.arraySize =
                    OverheadTransitionDirectionCount * OverheadTransitionFrameCount;

                SerializedProperty fallenBodySpriteArray =
                    serializedAnimator.FindProperty("fallenBodySprites");
                fallenBodySpriteArray.arraySize = FallenDirectionCount * FallenFrameCount;

                SerializedProperty fallenPackageSpriteArray =
                    serializedAnimator.FindProperty("fallenPackageSprites");
                fallenPackageSpriteArray.arraySize = FallenDirectionCount * FallenFrameCount;

                for (int direction = 0; direction < DirectionCount; direction++)
                {
                    idleSprites.GetArrayElementAtIndex(direction).objectReferenceValue =
                        unarmedSprites[GetSpriteName(direction, 0, "Player_Unarmed")];
                    carryingIdleSprites.GetArrayElementAtIndex(direction).objectReferenceValue =
                        carryingSprites[GetSpriteName(direction, 0, "Player_CarryingPose")];
                    packageIdleSprites.GetArrayElementAtIndex(direction).objectReferenceValue =
                        packageSprites[GetSpriteName(direction, 0, "CakePackage")];
                    overheadIdleSpriteArray.GetArrayElementAtIndex(direction).objectReferenceValue =
                        overheadBodySprites[
                            GetSpriteName(direction, 0, "Player_OverheadCarry")];
                    overheadPackageIdleSpriteArray.GetArrayElementAtIndex(direction)
                        .objectReferenceValue = overheadPackageSprites[
                            GetSpriteName(direction, 0, "CakePackage_OverheadCarry")];

                    for (int frame = 0; frame < WalkFrameCount; frame++)
                    {
                        int destinationIndex = direction * WalkFrameCount + frame;
                        walkSprites.GetArrayElementAtIndex(destinationIndex).objectReferenceValue =
                            unarmedSprites[GetSpriteName(direction, frame + 1, "Player_Unarmed")];
                        carryingWalkSprites.GetArrayElementAtIndex(destinationIndex).objectReferenceValue =
                            carryingSprites[GetSpriteName(direction, frame + 1, "Player_CarryingPose")];
                        packageWalkSprites.GetArrayElementAtIndex(destinationIndex).objectReferenceValue =
                            packageSprites[GetSpriteName(direction, frame + 1, "CakePackage")];
                        overheadWalkSpriteArray.GetArrayElementAtIndex(destinationIndex)
                            .objectReferenceValue = overheadBodySprites[
                                GetSpriteName(
                                    direction,
                                    frame + 1,
                                    "Player_OverheadCarry")];
                        overheadPackageWalkSpriteArray.GetArrayElementAtIndex(destinationIndex)
                            .objectReferenceValue = overheadPackageSprites[
                                GetSpriteName(
                                    direction,
                                    frame + 1,
                                    "CakePackage_OverheadCarry")];
                    }
                }

                for (int direction = 0; direction < SittingDirectionCount; direction++)
                {
                    sittingBodySpriteArray.GetArrayElementAtIndex(direction).objectReferenceValue =
                        sittingBodySprites[GetSittingSpriteName(direction, "Player_Sitting")];
                    sittingPackageSpriteArray.GetArrayElementAtIndex(direction).objectReferenceValue =
                        sittingPackageSprites[GetSittingSpriteName(direction, "CakePackage_Sitting")];
                    holdingSupportBodySpriteArray.GetArrayElementAtIndex(direction)
                        .objectReferenceValue = holdingSupportBodySprites[
                            GetSittingSpriteName(direction, "Player_HoldingSupport")];

                    for (int frame = 0; frame < SittingTransitionFrameCount; frame++)
                    {
                        int spriteIndex = direction * SittingTransitionFrameCount + frame;
                        sittingTransitionBodySpriteArray.GetArrayElementAtIndex(spriteIndex)
                            .objectReferenceValue = sittingTransitionBodySprites[
                                GetSittingTransitionSpriteName(
                                    direction,
                                    frame,
                                    "Player_SittingTransition")];
                        sittingTransitionPackageSpriteArray.GetArrayElementAtIndex(spriteIndex)
                            .objectReferenceValue = sittingTransitionPackageSprites[
                                GetSittingTransitionSpriteName(
                                    direction,
                                    frame,
                                    "CakePackage_SittingTransition")];
                        overheadTransitionBodySpriteArray.GetArrayElementAtIndex(spriteIndex)
                            .objectReferenceValue = overheadTransitionBodySprites[
                                GetSittingTransitionSpriteName(
                                    direction,
                                    frame,
                                    "Player_OverheadTransition")];
                        overheadTransitionPackageSpriteArray.GetArrayElementAtIndex(spriteIndex)
                            .objectReferenceValue = overheadTransitionPackageSprites[
                                GetSittingTransitionSpriteName(
                                    direction,
                                    frame,
                                    "CakePackage_OverheadTransition")];
                    }
                }

                for (int direction = 0; direction < FallenDirectionCount; direction++)
                {
                    for (int frame = 0; frame < FallenFrameCount; frame++)
                    {
                        int spriteIndex = direction * FallenFrameCount + frame;
                        fallenBodySpriteArray.GetArrayElementAtIndex(spriteIndex).objectReferenceValue =
                            fallenBodySprites[GetFallenSpriteName(
                                FallenDirectionNames[direction],
                                frame,
                                "Player_Fallen")];
                        fallenPackageSpriteArray.GetArrayElementAtIndex(spriteIndex).objectReferenceValue =
                            fallenPackageSprites[GetFallenSpriteName(
                                FallenDirectionNames[direction],
                                frame,
                                "CakePackage_Fallen")];
                    }
                }

                serializedAnimator.ApplyModifiedPropertiesWithoutUndo();

                PlayerPackageCarrier packageCarrier =
                    prefabRoot.GetComponent<PlayerPackageCarrier>();
                if (packageCarrier != null)
                {
                    var serializedCarrier = new SerializedObject(packageCarrier);
                    serializedCarrier.FindProperty("sittingAnchorDistance").floatValue = 0.35f;
                    serializedCarrier.ApplyModifiedPropertiesWithoutUndo();
                }

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

        private static string GetSittingSpriteName(int direction, string spritePrefix)
        {
            return $"{spritePrefix}_{SittingDirectionNames[direction]}";
        }

        private static string GetSittingTransitionSpriteName(
            int direction,
            int frame,
            string spritePrefix)
        {
            return $"{spritePrefix}_{SittingDirectionNames[direction]}_{frame}";
        }

        private static string GetFallenSpriteName(
            string directionName,
            int frame,
            string spritePrefix)
        {
            return $"{spritePrefix}_{directionName}_{frame}";
        }
    }
}

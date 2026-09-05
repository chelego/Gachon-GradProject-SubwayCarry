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
        private const string SpriteSheetPath =
            "Assets/_Project/Art/Concepts/Characters/SubwayCarry_Player_Unarmed_8Dir_IdleWalk_v1.png";
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
            ConfigureSpriteSheet();
            IReadOnlyDictionary<string, Sprite> sprites = LoadSpritesByName();
            ApplySpritesToPlayerPrefab(sprites);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Applied {sprites.Count} unarmed player sprites to {PlayerPrefabPath}.");
        }

        private static void ConfigureSpriteSheet()
        {
            AssetDatabase.ImportAsset(SpriteSheetPath, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer = AssetImporter.GetAtPath(SpriteSheetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Texture importer was not found for {SpriteSheetPath}.");
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

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteSheetPath);
            if (texture == null)
            {
                throw new InvalidOperationException($"Texture asset was not found at {SpriteSheetPath}.");
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
                    string name = GetSpriteName(direction, row);
                    GUID spriteId = existingIds.TryGetValue(name, out GUID existingId)
                        ? existingId
                        : GUID.Generate();

                    spriteRects.Add(new SpriteRect
                    {
                        name = name,
                        rect = new Rect(left, texture.height - bottom, right - left, bottom - top),
                        alignment = SpriteAlignment.Custom,
                        pivot = new Vector2(0.5f, 0.075f),
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

        private static IReadOnlyDictionary<string, Sprite> LoadSpritesByName()
        {
            Dictionary<string, Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(SpriteSheetPath)
                .OfType<Sprite>()
                .ToDictionary(sprite => sprite.name);

            int expectedCount = DirectionCount * RowCount;
            if (sprites.Count != expectedCount)
            {
                throw new InvalidOperationException(
                    $"Expected {expectedCount} sprites at {SpriteSheetPath}, but found {sprites.Count}.");
            }

            return sprites;
        }

        private static void ApplySpritesToPlayerPrefab(IReadOnlyDictionary<string, Sprite> sprites)
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

                PlayerSpriteAnimator animator = prefabRoot.GetComponent<PlayerSpriteAnimator>();
                if (animator == null)
                {
                    animator = prefabRoot.AddComponent<PlayerSpriteAnimator>();
                }

                var serializedAnimator = new SerializedObject(animator);
                serializedAnimator.FindProperty("controller").objectReferenceValue = controller;
                serializedAnimator.FindProperty("posture").objectReferenceValue = posture;
                serializedAnimator.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
                serializedAnimator.FindProperty("walkFramesPerDirection").intValue = WalkFrameCount;
                serializedAnimator.FindProperty("walkFramesPerSecond").floatValue = 8f;
                serializedAnimator.FindProperty("useMovementDirectionWhileMoving").boolValue = true;

                SerializedProperty idleSprites = serializedAnimator.FindProperty("idleSprites");
                idleSprites.arraySize = DirectionCount;

                SerializedProperty walkSprites = serializedAnimator.FindProperty("walkSprites");
                walkSprites.arraySize = DirectionCount * WalkFrameCount;

                for (int direction = 0; direction < DirectionCount; direction++)
                {
                    idleSprites.GetArrayElementAtIndex(direction).objectReferenceValue =
                        sprites[GetSpriteName(direction, 0)];

                    for (int frame = 0; frame < WalkFrameCount; frame++)
                    {
                        int destinationIndex = direction * WalkFrameCount + frame;
                        walkSprites.GetArrayElementAtIndex(destinationIndex).objectReferenceValue =
                            sprites[GetSpriteName(direction, frame + 1)];
                    }
                }

                serializedAnimator.ApplyModifiedPropertiesWithoutUndo();

                spriteRenderer.sprite = sprites[GetSpriteName(0, 0)];
                spriteRenderer.color = Color.white;
                spriteRenderer.drawMode = SpriteDrawMode.Simple;
                spriteRenderer.sortingOrder = 1;
                bodyVisual.localScale = Vector3.one;

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static string GetSpriteName(int direction, int row)
        {
            return row == 0
                ? $"Player_Unarmed_{DirectionNames[direction]}_Idle"
                : $"Player_Unarmed_{DirectionNames[direction]}_Walk_{row - 1}";
        }
    }
}

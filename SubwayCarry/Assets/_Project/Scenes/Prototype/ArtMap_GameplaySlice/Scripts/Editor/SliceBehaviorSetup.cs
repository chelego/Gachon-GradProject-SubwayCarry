using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SubwayCarry.AI.V2;

namespace SubwayCarry.Prototype.ArtMapSlice.Editor
{
    public static class SliceBehaviorSetup
    {
        public const string SettingsPath = "Assets/_Project/Scenes/Ai_v2/Profiles/AI_V2_ContextualActivity_Default.asset";
        public static PassengerAiV2ActivitySettings LoadSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PassengerAiV2ActivitySettings>(SettingsPath);
            if (settings == null) { settings = ScriptableObject.CreateInstance<PassengerAiV2ActivitySettings>(); AssetDatabase.CreateAsset(settings, SettingsPath); }
            return settings;
        }
        public static void Configure(SliceGameController controller)
        {
            controller.activitySettings = LoadSettings();
            // Authoring information; the common executor never identifies a map by name or index.
            controller.maps[0].placeKind = PassengerAiV2PlaceKind.Platform;
            controller.maps[1].placeKind = PassengerAiV2PlaceKind.TrainInterior;
            controller.maps[2].placeKind = PassengerAiV2PlaceKind.Platform;
        }
        public static string Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            var scene = SceneManager.GetSceneByPath(SliceSceneBuilder.ScenePath); bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("Save your scene edits first.");
            if (opened) scene = EditorSceneManager.OpenScene(SliceSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                SliceGameController controller = null;
                foreach (var root in scene.GetRootGameObjects()) if (root.TryGetComponent(out SliceGameController c)) controller = c;
                if (controller == null) throw new InvalidOperationException("Slice controller missing.");
                Configure(controller); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
                return "Contextual behavior settings connected; original art and AI 01-06 scenes were not rebuilt.";
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}

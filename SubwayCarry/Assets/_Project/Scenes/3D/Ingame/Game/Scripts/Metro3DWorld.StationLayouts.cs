using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    public sealed partial class Metro3DWorld
    {
        [Serializable]
        sealed class StationLayoutOption
        {
            public string layoutId;
            public Metro3DStationLayout prefab;
        }

        [Serializable]
        internal sealed class StationLayoutChoice
        {
            public string stationId;
            public string layoutId;
        }

        [SerializeField] StationLayoutOption[] stationLayouts = Array.Empty<StationLayoutOption>();
        [SerializeField] StationLayoutChoice[] fixedStationLayouts = Array.Empty<StationLayoutChoice>();
        readonly Dictionary<string, string> stationLayoutChoices = new(StringComparer.Ordinal);
        readonly System.Random stationLayoutRandom = new();

        internal bool UsesStationLayouts => stationLayouts.Length > 0;

        Metro3DStationLayout SelectStationPrefab(string stationId)
        {
            if (!UsesStationLayouts || stationId == "gachon") return null;

            foreach (var rule in fixedStationLayouts)
                if (rule.stationId == stationId)
                    return FindStationLayout(rule.layoutId)?.prefab;

            if (stationLayoutChoices.TryGetValue(stationId, out var chosenId))
            {
                var chosen = FindStationLayout(chosenId);
                if (chosen != null) return chosen.prefab;
                // A removed layout falls back to the first (common) option.
                return stationLayouts[0].prefab;
            }

            var option = stationLayouts[stationLayoutRandom.Next(stationLayouts.Length)];
            stationLayoutChoices.Add(stationId, option.layoutId);
            return option.prefab;
        }

        StationLayoutOption FindStationLayout(string layoutId) =>
            Array.Find(stationLayouts, option => option.layoutId == layoutId);

        internal StationLayoutChoice[] CaptureStationLayouts()
        {
            if (!UsesStationLayouts) return null;
            var choices = new StationLayoutChoice[stationLayoutChoices.Count];
            int i = 0;
            foreach (var entry in stationLayoutChoices)
                choices[i++] = new StationLayoutChoice { stationId = entry.Key, layoutId = entry.Value };
            return choices;
        }

        internal void RestoreStationLayouts(StationLayoutChoice[] choices, IEnumerable<string> legacyRoute)
        {
            if (!UsesStationLayouts) return;
            stationLayoutChoices.Clear();
            if (choices != null)
            {
                foreach (var choice in choices)
                    if (choice != null && !string.IsNullOrEmpty(choice.stationId))
                        stationLayoutChoices[choice.stationId] = choice.layoutId;
                return;
            }

            foreach (string stationId in legacyRoute)
                stationLayoutChoices[stationId] = stationLayouts[0].layoutId;
        }

        internal void ResetStationLayouts()
        {
            if (!UsesStationLayouts) return;
            stationLayoutChoices.Clear();
            routeIdentity = string.Empty;
        }
    }
}

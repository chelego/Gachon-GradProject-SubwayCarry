using System;
using UnityEngine;

namespace SubwayCarry.Transit
{
    /// <summary>Authored station geometry and integration points; does not own delivery or train state.</summary>
    public sealed class StationSceneLayout : MonoBehaviour
    {
        [Serializable]
        public sealed class Level
        {
            public string label;
            public GameObject geometry;
            public Transform arrival;
            public Transform cameraTarget;
            public float cameraSize = 14f;
        }

        [SerializeField] private StationData stationData;
        [SerializeField] private Camera stationCamera;
        [SerializeField] private Level[] levels = Array.Empty<Level>();
        [SerializeField] private Transform[] boardingPoints = Array.Empty<Transform>();
        [SerializeField] private Transform[] exitPoints = Array.Empty<Transform>();
        [SerializeField] private int activeLevel;

        public StationData Data => stationData;
        public Level[] Levels => levels;
        public Transform[] BoardingPoints => boardingPoints;
        public Transform[] ExitPoints => exitPoints;
        public int ActiveLevel => activeLevel;

        public void Configure(StationData data, Camera camera, Level[] authoredLevels,
            Transform[] boarding, Transform[] exits)
        {
            stationData = data;
            stationCamera = camera;
            levels = authoredLevels;
            boardingPoints = boarding;
            exitPoints = exits;
            ShowLevel(0);
        }

        public bool ShowLevel(int index)
        {
            if (index < 0 || index >= levels.Length || levels[index].geometry == null)
                return false;
            activeLevel = index;
            for (int i = 0; i < levels.Length; i++)
                if (levels[i].geometry != null) levels[i].geometry.SetActive(i == index);
            Level level = levels[index];
            if (stationCamera != null && level.cameraTarget != null)
            {
                Vector3 target = level.cameraTarget.position;
                stationCamera.transform.position = new Vector3(target.x, target.y, -10f);
                stationCamera.orthographicSize = level.cameraSize;
            }
            return true;
        }

        [ContextMenu("Preview next station level")]
        private void NextLevel()
        {
            if (levels.Length > 0) ShowLevel((activeLevel + 1) % levels.Length);
        }
    }
}

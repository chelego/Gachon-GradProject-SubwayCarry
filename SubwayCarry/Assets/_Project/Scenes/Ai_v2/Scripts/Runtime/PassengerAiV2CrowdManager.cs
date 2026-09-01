using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI.V2
{
    /// <summary>
    /// V2 승객 등록과 근접 조회를 한 곳에서 처리한다.
    /// 개별 승객이 전체 승객 목록을 순회하지 않도록 Spatial Hash를 공유한다.
    /// </summary>
    public sealed class PassengerAiV2CrowdManager : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float cellSize = 2.5f;
        [SerializeField, Range(1f, 30f)] private float spatialUpdateHz = 10f;

        private readonly List<PassengerAiV2Agent> agents = new List<PassengerAiV2Agent>(32);
        private readonly Dictionary<Vector2Int, List<PassengerAiV2Agent>> cells =
            new Dictionary<Vector2Int, List<PassengerAiV2Agent>>(64);
        private float nextSpatialUpdateTime;

        public int RegisteredCount => agents.Count;
        public int DeadlockedCount { get; private set; }
        public float SpatialUpdateHz => spatialUpdateHz;

        private void Awake()
        {
            nextSpatialUpdateTime = Time.time;
        }

        private void Update()
        {
            if (Time.time < nextSpatialUpdateTime)
            {
                return;
            }

            float interval = 1f / Mathf.Max(1f, spatialUpdateHz);
            nextSpatialUpdateTime = Time.time + interval;
            RebuildSpatialHash();
        }

        public void Register(PassengerAiV2Agent agent)
        {
            if (agent != null && !agents.Contains(agent))
            {
                agents.Add(agent);
            }
        }

        public void Unregister(PassengerAiV2Agent agent)
        {
            if (agent != null)
            {
                agents.Remove(agent);
            }
        }

        public void QueryNearby(
            Vector2 position,
            float radius,
            PassengerAiV2Agent requester,
            List<PassengerAiV2Agent> results)
        {
            results.Clear();

            float safeRadius = Mathf.Max(0.1f, radius);
            float radiusSquared = safeRadius * safeRadius;
            Vector2Int min = ToCell(position - Vector2.one * safeRadius);
            Vector2Int max = ToCell(position + Vector2.one * safeRadius);

            for (int y = min.y; y <= max.y; y++)
            {
                for (int x = min.x; x <= max.x; x++)
                {
                    if (!cells.TryGetValue(new Vector2Int(x, y), out List<PassengerAiV2Agent> bucket))
                    {
                        continue;
                    }

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        PassengerAiV2Agent candidate = bucket[i];
                        if (candidate == null || candidate == requester)
                        {
                            continue;
                        }

                        Vector2 offset = candidate.Position - position;
                        if (offset.sqrMagnitude <= radiusSquared)
                        {
                            results.Add(candidate);
                        }
                    }
                }
            }
        }

        private void RebuildSpatialHash()
        {
            foreach (KeyValuePair<Vector2Int, List<PassengerAiV2Agent>> pair in cells)
            {
                pair.Value.Clear();
            }

            DeadlockedCount = 0;
            for (int i = agents.Count - 1; i >= 0; i--)
            {
                PassengerAiV2Agent agent = agents[i];
                if (agent == null)
                {
                    agents.RemoveAt(i);
                    continue;
                }

                Vector2Int cell = ToCell(agent.Position);
                if (!cells.TryGetValue(cell, out List<PassengerAiV2Agent> bucket))
                {
                    bucket = new List<PassengerAiV2Agent>(8);
                    cells.Add(cell, bucket);
                }

                bucket.Add(agent);
                if (agent.IsDeadlocked)
                {
                    DeadlockedCount++;
                }
            }
        }

        private Vector2Int ToCell(Vector2 position)
        {
            float size = Mathf.Max(0.5f, cellSize);
            return new Vector2Int(
                Mathf.FloorToInt(position.x / size),
                Mathf.FloorToInt(position.y / size));
        }
    }
}

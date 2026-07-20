using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    public sealed class PassengerPrototypeAgent : MonoBehaviour
    {
        [SerializeField] private GridNavigation2D navigation;
        [SerializeField] private Transform[] targets;
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float repathInterval = 0.5f;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.08f;
        [SerializeField] private LineRenderer pathRenderer;

        private readonly List<Vector3> path = new List<Vector3>();
        private int pathIndex;
        private int targetIndex;
        private float nextRepathTime;
        private Vector3 lastTargetPosition;

        public void Configure(
            GridNavigation2D gridNavigation,
            Transform[] routeTargets,
            LineRenderer lineRenderer)
        {
            navigation = gridNavigation;
            targets = routeTargets;
            pathRenderer = lineRenderer;
        }

        private void Start()
        {
            RebuildPath();
        }

        private void Update()
        {
            if (navigation == null || targets == null || targets.Length == 0)
            {
                return;
            }

            Transform target = targets[targetIndex];
            if (target == null)
            {
                return;
            }

            if (Time.time >= nextRepathTime ||
                Vector3.SqrMagnitude(target.position - lastTargetPosition) > 0.01f)
            {
                RebuildPath();
            }

            if (path.Count == 0 || pathIndex >= path.Count)
            {
                if (Vector3.Distance(transform.position, target.position) <= arrivalDistance + 0.25f)
                {
                    targetIndex = (targetIndex + 1) % targets.Length;
                    RebuildPath();
                }

                return;
            }

            Vector3 waypoint = path[pathIndex];
            Vector3 direction = waypoint - transform.position;
            transform.position = Vector3.MoveTowards(
                transform.position,
                waypoint,
                moveSpeed * Time.deltaTime);

            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.up = direction.normalized;
            }

            if (Vector3.Distance(transform.position, waypoint) <= arrivalDistance)
            {
                pathIndex++;
            }
        }

        private void RebuildPath()
        {
            if (navigation == null || targets == null || targets.Length == 0)
            {
                return;
            }

            Transform target = targets[targetIndex];
            if (target == null)
            {
                return;
            }

            path.Clear();
            path.AddRange(navigation.FindWorldPath(transform.position, target.position));
            pathIndex = path.Count > 1 ? 1 : 0;
            lastTargetPosition = target.position;
            nextRepathTime = Time.time + repathInterval;
            UpdatePathRenderer();
        }

        private void UpdatePathRenderer()
        {
            if (pathRenderer == null)
            {
                return;
            }

            pathRenderer.positionCount = path.Count + 1;
            pathRenderer.SetPosition(0, transform.position);

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 point = path[i];
                point.z = transform.position.z + 0.1f;
                pathRenderer.SetPosition(i + 1, point);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI
{
    public static class AStarGrid
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        public static List<Vector2Int> FindPath(
            Vector2Int start,
            Vector2Int goal,
            int width,
            int height,
            Func<Vector2Int, bool> isWalkable)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Grid size must be positive.");
            }

            if (isWalkable == null)
            {
                throw new ArgumentNullException(nameof(isWalkable));
            }

            if (!IsInside(start, width, height) ||
                !IsInside(goal, width, height) ||
                !isWalkable(start) ||
                !isWalkable(goal))
            {
                return new List<Vector2Int>();
            }

            var open = new List<Vector2Int> { start };
            var openLookup = new HashSet<Vector2Int> { start };
            var closed = new HashSet<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var gScore = new Dictionary<Vector2Int, int> { [start] = 0 };

            while (open.Count > 0)
            {
                Vector2Int current = GetBestNode(open, gScore, goal);
                if (current == goal)
                {
                    return ReconstructPath(cameFrom, current);
                }

                open.Remove(current);
                openLookup.Remove(current);
                closed.Add(current);

                foreach (Vector2Int direction in Directions)
                {
                    Vector2Int neighbor = current + direction;
                    if (!IsInside(neighbor, width, height) ||
                        closed.Contains(neighbor) ||
                        !isWalkable(neighbor))
                    {
                        continue;
                    }

                    int tentativeScore = gScore[current] + 1;
                    int knownScore;
                    if (gScore.TryGetValue(neighbor, out knownScore) && tentativeScore >= knownScore)
                    {
                        continue;
                    }

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeScore;

                    if (openLookup.Add(neighbor))
                    {
                        open.Add(neighbor);
                    }
                }
            }

            return new List<Vector2Int>();
        }

        private static Vector2Int GetBestNode(
            List<Vector2Int> open,
            Dictionary<Vector2Int, int> gScore,
            Vector2Int goal)
        {
            Vector2Int best = open[0];
            int bestHeuristic = Heuristic(best, goal);
            int bestScore = gScore[best] + bestHeuristic;

            for (int i = 1; i < open.Count; i++)
            {
                Vector2Int candidate = open[i];
                int heuristic = Heuristic(candidate, goal);
                int score = gScore[candidate] + heuristic;

                if (score < bestScore || score == bestScore && heuristic < bestHeuristic)
                {
                    best = candidate;
                    bestScore = score;
                    bestHeuristic = heuristic;
                }
            }

            return best;
        }

        private static List<Vector2Int> ReconstructPath(
            Dictionary<Vector2Int, Vector2Int> cameFrom,
            Vector2Int current)
        {
            var path = new List<Vector2Int> { current };

            while (cameFrom.TryGetValue(current, out Vector2Int previous))
            {
                current = previous;
                path.Add(current);
            }

            path.Reverse();
            return path;
        }

        private static int Heuristic(Vector2Int from, Vector2Int to)
        {
            return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
        }

        private static bool IsInside(Vector2Int cell, int width, int height)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI.V2
{
    public enum PassengerAiV2StairDirection
    {
        None = 0,
        LowerToUpper = 1,
        UpperToLower = -1
    }

    /// <summary>
    /// 좁은 계단의 접근, 입구, 출구, 방향, 용량과 대기열을 소유한다.
    /// 평상시에는 양방향을 동시에 열고 현재 군중이 만든 흐름에 합류할 수 있는 통로를 제공한다.
    /// 예약은 방향별 순번제가 아니라 계단 전체가 과밀할 때만 진입을 늦추는 안전장치다.
    /// </summary>
    public sealed class PassengerAiV2StairSmartObject : MonoBehaviour, IPassengerAiV2SmartObject
    {
        private sealed class RequestRecord
        {
            public PassengerAiV2Agent Agent;
            public PassengerAiV2StairDirection Direction;
            public float RequestedAt;
            public float ApproachDistance;
            public bool Granted;
            public bool Entered;
        }

        [SerializeField] private string smartObjectId = "stair_merge_01";
        [SerializeField, Min(1)] private int capacity = 10;
        [SerializeField, Range(0f, 1.25f)] private float directionalLaneOffset;
        [SerializeField, Range(2f, 20f)] private float reservationUpdateHz = 10f;
        [SerializeField] private Vector2 lowerApproach;
        [SerializeField] private Vector2 lowerEntry;
        [SerializeField] private Vector2 upperEntry;
        [SerializeField] private Vector2 upperApproach;

        private readonly List<RequestRecord> requests = new List<RequestRecord>(16);
        private float nextReservationUpdateTime;
        private int emergentLaneOrientation;

        public string SmartObjectId => smartObjectId;
        public int Capacity => capacity;
        public int CompletedTraversalCount { get; private set; }
        public int WaitingLowerToUpper => CountRequests(PassengerAiV2StairDirection.LowerToUpper, false);
        public int WaitingUpperToLower => CountRequests(PassengerAiV2StairDirection.UpperToLower, false);
        public int ReservedCount => CountGranted(false);
        public int TraversingCount => CountGranted(true);
        public int ActiveLowerToUpper => CountGranted(PassengerAiV2StairDirection.LowerToUpper);
        public int ActiveUpperToLower => CountGranted(PassengerAiV2StairDirection.UpperToLower);
        public int TraversingLowerToUpper => CountGranted(PassengerAiV2StairDirection.LowerToUpper, true);
        public int TraversingUpperToLower => CountGranted(PassengerAiV2StairDirection.UpperToLower, true);
        public int EmergentLaneOrientation => emergentLaneOrientation;
        public Rect TraversalBounds
        {
            get
            {
                const float halfWidth = 1.65f;
                float centerX = (lowerEntry.x + upperEntry.x) * 0.5f;
                float minY = Mathf.Min(lowerEntry.y, upperEntry.y) - 0.5f;
                float maxY = Mathf.Max(lowerEntry.y, upperEntry.y) + 0.5f;
                return Rect.MinMaxRect(centerX - halfWidth, minY, centerX + halfWidth, maxY);
            }
        }
        public Rect FlowInfluenceBounds
        {
            get
            {
                Rect bounds = TraversalBounds;
                bounds.xMin -= 1.1f;
                bounds.xMax += 1.1f;
                bounds.yMin -= 1.4f;
                bounds.yMax += 1.4f;
                return bounds;
            }
        }

        public void Configure(
            string objectId,
            Vector2 lowerApproachPoint,
            Vector2 lowerEntryPoint,
            Vector2 upperEntryPoint,
            Vector2 upperApproachPoint,
            int simultaneousCapacity,
            float laneOffset)
        {
            smartObjectId = objectId;
            lowerApproach = lowerApproachPoint;
            lowerEntry = lowerEntryPoint;
            upperEntry = upperEntryPoint;
            upperApproach = upperApproachPoint;
            capacity = Mathf.Max(1, simultaneousCapacity);
            directionalLaneOffset = Mathf.Clamp(laneOffset, 0f, 1.25f);
            emergentLaneOrientation = 0;
        }

        private void Update()
        {
            if (Time.time < nextReservationUpdateTime)
            {
                return;
            }

            nextReservationUpdateTime = Time.time + 1f / Mathf.Max(2f, reservationUpdateHz);
            RemoveDestroyedRequests();
            ProcessAdmissions();
        }

        public bool RequestUse(PassengerAiV2Agent agent, int direction, float approachDistance)
        {
            if (agent == null)
            {
                return false;
            }

            PassengerAiV2StairDirection stairDirection = direction >= 0
                ? PassengerAiV2StairDirection.LowerToUpper
                : PassengerAiV2StairDirection.UpperToLower;
            SeedFlowOrientation(agent, stairDirection);
            RequestRecord request = FindRequest(agent);
            if (request == null)
            {
                request = new RequestRecord
                {
                    Agent = agent,
                    Direction = stairDirection,
                    RequestedAt = Time.time,
                    ApproachDistance = approachDistance
                };
                requests.Add(request);
            }
            else
            {
                request.Direction = stairDirection;
                request.ApproachDistance = approachDistance;
            }

            ProcessAdmissions();
            return request.Granted;
        }

        public bool HasReservation(PassengerAiV2Agent agent)
        {
            RequestRecord request = FindRequest(agent);
            return request != null && request.Granted;
        }

        public void MarkEntered(PassengerAiV2Agent agent)
        {
            RequestRecord request = FindRequest(agent);
            if (request != null && request.Granted)
            {
                request.Entered = true;
            }
        }

        public void Release(PassengerAiV2Agent agent)
        {
            RequestRecord request = FindRequest(agent);
            if (request == null)
            {
                return;
            }

            if (request.Entered)
            {
                CompletedTraversalCount++;
            }

            requests.Remove(request);
            ProcessAdmissions();
        }

        public void Cancel(PassengerAiV2Agent agent)
        {
            RequestRecord request = FindRequest(agent);
            if (request != null)
            {
                requests.Remove(request);
                ProcessAdmissions();
            }
        }

        public Vector2 GetApproachPoint(PassengerAiV2StairDirection direction)
        {
            Vector2 point = direction == PassengerAiV2StairDirection.LowerToUpper
                ? lowerApproach
                : upperApproach;
            point.x += GetLaneX(direction);
            return point;
        }

        public Vector2 GetEntryPoint(PassengerAiV2StairDirection direction)
        {
            Vector2 point = direction == PassengerAiV2StairDirection.LowerToUpper
                ? lowerEntry
                : upperEntry;
            point.x += GetLaneX(direction);
            return point;
        }

        public Vector2 GetExitPoint(PassengerAiV2StairDirection direction)
        {
            Vector2 point = direction == PassengerAiV2StairDirection.LowerToUpper
                ? upperEntry
                : lowerEntry;
            point.x += GetLaneX(direction);
            return point;
        }

        public Vector2 GetReleasePoint(PassengerAiV2StairDirection direction)
        {
            Vector2 point = direction == PassengerAiV2StairDirection.LowerToUpper
                ? upperApproach
                : lowerApproach;
            point.x += GetLaneX(direction);
            return point;
        }

        public Vector2 GetQueuePosition(PassengerAiV2Agent agent, PassengerAiV2StairDirection direction)
        {
            int queueIndex = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request.Direction != direction || request.Granted)
                {
                    continue;
                }

                if (request.Agent == agent)
                {
                    break;
                }

                queueIndex++;
            }

            Vector2 forward = direction == PassengerAiV2StairDirection.LowerToUpper
                ? Vector2.up
                : Vector2.down;
            return GetApproachPoint(direction) - forward * (queueIndex * 0.9f);
        }

        private void ProcessAdmissions()
        {
            int grantedCount = CountGranted(false);
            while (grantedCount < capacity)
            {
                RequestRecord best = FindBestWaitingRequest();
                if (best == null)
                {
                    break;
                }

                best.Granted = true;
                grantedCount++;
            }
        }

        private RequestRecord FindBestWaitingRequest()
        {
            RequestRecord best = null;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request.Granted || request.Agent == null)
                {
                    continue;
                }

                float waitBonus = Mathf.Min(3f, Time.time - request.RequestedAt) * 0.35f;
                float score = request.ApproachDistance - waitBonus;
                if (score < bestScore)
                {
                    best = request;
                    bestScore = score;
                }
            }

            return best;
        }

        private RequestRecord FindRequest(PassengerAiV2Agent agent)
        {
            for (int i = 0; i < requests.Count; i++)
            {
                if (requests[i].Agent == agent)
                {
                    return requests[i];
                }
            }

            return null;
        }

        private int CountRequests(PassengerAiV2StairDirection direction, bool enteredOnly)
        {
            int count = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request.Direction == direction && !request.Granted && (!enteredOnly || request.Entered))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountGranted(bool enteredOnly)
        {
            int count = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request.Granted && (!enteredOnly || request.Entered))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountGranted(PassengerAiV2StairDirection direction)
        {
            return CountGranted(direction, false);
        }

        private int CountGranted(PassengerAiV2StairDirection direction, bool enteredOnly)
        {
            int count = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request.Granted && request.Direction == direction && (!enteredOnly || request.Entered))
                {
                    count++;
                }
            }

            return count;
        }

        private void RemoveDestroyedRequests()
        {
            for (int i = requests.Count - 1; i >= 0; i--)
            {
                if (requests[i].Agent == null)
                {
                    requests.RemoveAt(i);
                }
            }

            ProcessAdmissions();
        }

        private float GetLaneX(PassengerAiV2StairDirection direction)
        {
            if (emergentLaneOrientation == 0)
            {
                return 0f;
            }

            return direction == PassengerAiV2StairDirection.LowerToUpper
                ? directionalLaneOffset * emergentLaneOrientation
                : -directionalLaneOffset * emergentLaneOrientation;
        }

        private void SeedFlowOrientation(
            PassengerAiV2Agent agent,
            PassengerAiV2StairDirection direction)
        {
            if (emergentLaneOrientation != 0 || agent == null || directionalLaneOffset <= 0.01f)
            {
                return;
            }

            float centerX = (lowerEntry.x + upperEntry.x) * 0.5f;
            int arrivingWorldSide = agent.Position.x >= centerX ? 1 : -1;
            emergentLaneOrientation = direction == PassengerAiV2StairDirection.LowerToUpper
                ? arrivingWorldSide
                : -arrivingWorldSide;
        }
    }
}

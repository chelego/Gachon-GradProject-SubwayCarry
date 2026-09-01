using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI.V2
{
    public enum PassengerAiV2FareGateDirection
    {
        LowerToUpper = 1,
        UpperToLower = -1
    }

    /// <summary>
    /// 한 방향 개찰구의 접근점, 대기열, 카드 리더, 문 상태와 통과 완료를 소유한다.
    /// 카드 태그 전에는 문을 열지 않으며 한 번에 한 승객만 같은 통로를 사용한다.
    /// </summary>
    public sealed class PassengerAiV2FareGateSmartObject : MonoBehaviour, IPassengerAiV2SmartObject
    {
        private sealed class RequestRecord
        {
            public PassengerAiV2Agent Agent;
            public float RequestedAt;
            public float ApproachDistance;
            public bool Granted;
            public bool CardTagged;
            public bool Passed;
        }

        [SerializeField] private string smartObjectId = "fare_gate_01";
        [SerializeField] private PassengerAiV2FareGateDirection allowedDirection =
            PassengerAiV2FareGateDirection.LowerToUpper;
        [SerializeField, Range(2f, 20f)] private float queueUpdateHz = 10f;
        [SerializeField, Min(0.4f)] private float admissionDistance = 1.35f;
        [SerializeField] private Vector2 approachPoint;
        [SerializeField] private Vector2 readerPoint;
        [SerializeField] private Vector2 passPoint;
        [SerializeField] private Vector2 releasePoint;
        [SerializeField] private SpriteRenderer barrierRenderer;

        private readonly List<RequestRecord> requests = new List<RequestRecord>(16);
        private Vector3 closedBarrierScale = Vector3.one;
        private float nextQueueUpdateTime;

        public string SmartObjectId => smartObjectId;
        public int Capacity => 1;
        public PassengerAiV2FareGateDirection AllowedDirection => allowedDirection;
        public int QueueLoad => requests.Count;
        public int WaitingCount => CountWaiting();
        public bool IsOpen { get; private set; }
        public int CardTagCount { get; private set; }
        public int CompletedPassCount { get; private set; }
        public int InvalidPassAttemptCount { get; private set; }

        public void Configure(
            string objectId,
            PassengerAiV2FareGateDirection direction,
            Vector2 approach,
            Vector2 reader,
            Vector2 pass,
            Vector2 release,
            SpriteRenderer barrier)
        {
            smartObjectId = objectId;
            allowedDirection = direction;
            approachPoint = approach;
            readerPoint = reader;
            passPoint = pass;
            releasePoint = release;
            barrierRenderer = barrier;
            if (barrierRenderer != null)
            {
                closedBarrierScale = barrierRenderer.transform.localScale;
            }

            SetOpen(false);
        }

        private void Update()
        {
            if (Time.time < nextQueueUpdateTime)
            {
                return;
            }

            nextQueueUpdateTime = Time.time + 1f / Mathf.Max(2f, queueUpdateHz);
            RemoveDestroyedRequests();
            ProcessQueue();
        }

        public bool IsCompatible(PassengerAiV2FareGateDirection direction)
        {
            return allowedDirection == direction;
        }

        public bool RequestUse(PassengerAiV2Agent agent, int direction, float approachDistance)
        {
            if (agent == null || direction != (int)allowedDirection)
            {
                return false;
            }

            RequestRecord request = FindRequest(agent);
            if (request == null)
            {
                request = new RequestRecord
                {
                    Agent = agent,
                    RequestedAt = Time.time,
                    ApproachDistance = approachDistance
                };
                requests.Add(request);
            }
            else
            {
                request.ApproachDistance = approachDistance;
            }

            ProcessQueue();
            return request.Granted;
        }

        public bool HasReservation(PassengerAiV2Agent agent)
        {
            RequestRecord request = FindRequest(agent);
            return request != null && request.Granted;
        }

        public bool TagCard(PassengerAiV2Agent agent)
        {
            RequestRecord request = FindRequest(agent);
            if (request == null || !request.Granted)
            {
                return false;
            }

            if (!request.CardTagged)
            {
                request.CardTagged = true;
                CardTagCount++;
            }

            SetOpen(true);
            return true;
        }

        public bool CanPass(PassengerAiV2Agent agent)
        {
            RequestRecord request = FindRequest(agent);
            return request != null && request.Granted && request.CardTagged && IsOpen;
        }

        public bool MarkPassed(PassengerAiV2Agent agent)
        {
            RequestRecord request = FindRequest(agent);
            if (request == null || !CanPass(agent))
            {
                InvalidPassAttemptCount++;
                return false;
            }

            request.Passed = true;
            return true;
        }

        public void Release(PassengerAiV2Agent agent)
        {
            RequestRecord request = FindRequest(agent);
            if (request == null)
            {
                return;
            }

            if (request.Passed)
            {
                CompletedPassCount++;
            }

            requests.Remove(request);
            SetOpen(false);
            ProcessQueue();
        }

        public void Cancel(PassengerAiV2Agent agent)
        {
            RequestRecord request = FindRequest(agent);
            if (request == null)
            {
                return;
            }

            bool wasGranted = request.Granted;
            requests.Remove(request);
            if (wasGranted)
            {
                SetOpen(false);
            }

            ProcessQueue();
        }

        public Vector2 GetApproachPoint()
        {
            return approachPoint;
        }

        public Vector2 GetReaderPoint()
        {
            return readerPoint;
        }

        public Vector2 GetPassPoint()
        {
            return passPoint;
        }

        public Vector2 GetReleasePoint()
        {
            return releasePoint;
        }

        public Vector2 GetQueuePosition(PassengerAiV2Agent agent)
        {
            int queueIndex = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request.Granted)
                {
                    continue;
                }

                if (request.Agent == agent)
                {
                    break;
                }

                queueIndex++;
            }

            Vector2 forward = allowedDirection == PassengerAiV2FareGateDirection.LowerToUpper
                ? Vector2.up
                : Vector2.down;
            return approachPoint - forward * (queueIndex * 0.88f);
        }

        private void ProcessQueue()
        {
            if (HasGrantedUser())
            {
                return;
            }

            RequestRecord best = null;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request.Agent == null || request.Granted || request.ApproachDistance > admissionDistance)
                {
                    continue;
                }

                float waitBonus = Mathf.Min(4f, Time.time - request.RequestedAt) * 0.3f;
                float score = request.ApproachDistance - waitBonus;
                if (score < bestScore)
                {
                    best = request;
                    bestScore = score;
                }
            }

            if (best != null)
            {
                best.Granted = true;
            }
        }

        private bool HasGrantedUser()
        {
            for (int i = 0; i < requests.Count; i++)
            {
                if (requests[i].Granted)
                {
                    return true;
                }
            }

            return false;
        }

        private int CountWaiting()
        {
            int count = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                if (!requests[i].Granted)
                {
                    count++;
                }
            }

            return count;
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

        private void RemoveDestroyedRequests()
        {
            bool removedGranted = false;
            for (int i = requests.Count - 1; i >= 0; i--)
            {
                if (requests[i].Agent != null)
                {
                    continue;
                }

                removedGranted |= requests[i].Granted;
                requests.RemoveAt(i);
            }

            if (removedGranted)
            {
                SetOpen(false);
            }
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            if (barrierRenderer == null)
            {
                return;
            }

            Vector3 scale = closedBarrierScale;
            scale.x = open ? Mathf.Min(0.14f, closedBarrierScale.x) : closedBarrierScale.x;
            barrierRenderer.transform.localScale = scale;
            barrierRenderer.color = open
                ? new Color(0.2f, 0.88f, 0.48f, 1f)
                : new Color(1f, 0.46f, 0.1f, 1f);
        }
    }
}

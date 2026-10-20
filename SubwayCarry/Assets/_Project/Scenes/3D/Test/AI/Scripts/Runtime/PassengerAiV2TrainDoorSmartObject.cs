using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI.V2
{
    public enum PassengerAiV2TrainDoorPhase
    {
        Closed,
        Opening,
        Alighting,
        Boarding,
        ClosingWarning
    }

    public enum PassengerAiV2TrainDoorFlow
    {
        Alight = 1,
        Board = -1
    }

    /// <summary>
    /// 열차 문의 주기와 하차 우선권, 두 개 통과 Lane, 방향별 FIFO 대기열과 예약을 소유한다.
    /// 하차 무리가 거의 빠져나오면 마지막 하차자와 다른 Lane부터 탑승 흐름을 겹쳐 시작한다.
    /// </summary>
    public sealed class PassengerAiV2TrainDoorSmartObject : MonoBehaviour, IPassengerAiV2SmartObject
    {
        private sealed class RequestRecord
        {
            public PassengerAiV2Agent Agent;
            public PassengerAiV2TrainDoorFlow Flow;
            public int Cycle;
            public int Lane = -1;
            public int QueueSide;
            public long QueueOrder;
            public float RequestedAt;
            public float ApproachDistance;
            public bool Granted;
            public bool Passed;
            public bool Completed;
        }

        [SerializeField] private string smartObjectId = "train_door_01";
        [SerializeField, Min(1)] private int capacity = 2;
        [SerializeField, Range(2f, 20f)] private float updateHz = 10f;
        [SerializeField, Min(0.2f)] private float closedDuration = 2f;
        [SerializeField, Min(0.2f)] private float openingDuration = 1.1f;
        [SerializeField, Min(0.2f)] private float minimumFlowDuration = 1.4f;
        [SerializeField, Min(0.2f)] private float closingWarningDuration = 2f;
        [SerializeField, Min(0.2f)] private float admissionDistance = 1.15f;
        [SerializeField, Min(0.2f)] private float laneOffset = 0.62f;
        [SerializeField, Min(0.8f)] private float convoySpacing = 1.32f;
        [SerializeField, Range(0f, 0.4f)] private float convoyLookAhead = 0.14f;
        [SerializeField] private Vector2 doorCenter;
        [SerializeField] private Transform leftDoorPanel;
        [SerializeField] private Transform rightDoorPanel;

        private readonly List<RequestRecord> requests = new List<RequestRecord>(24);
        private Vector3 leftClosedPosition;
        private Vector3 rightClosedPosition;
        private float phaseStartedAt;
        private float nextUpdateTime;
        private long nextQueueOrder;

        public string SmartObjectId => smartObjectId;
        public int Capacity => capacity;
        public PassengerAiV2TrainDoorPhase Phase { get; private set; }
        public int CycleIndex { get; private set; }
        public bool IsDoorOpen => Phase != PassengerAiV2TrainDoorPhase.Closed;
        public int WaitingToAlight => CountWaiting(PassengerAiV2TrainDoorFlow.Alight);
        public int WaitingToBoard => CountWaiting(PassengerAiV2TrainDoorFlow.Board);
        public int ActivePassageCount => CountGranted();
        public int AlightedCount { get; private set; }
        public int BoardedCount { get; private set; }
        public int InvalidPassAttemptCount { get; private set; }
        public int BoardingBeforeAlightingClearCount { get; private set; }
        public int BoardingOverlapStartCount { get; private set; }
        public int OutOfOrderBoardingCount { get; private set; }

        public void Configure(
            string objectId,
            Vector2 center,
            Transform leftPanel,
            Transform rightPanel,
            int simultaneousCapacity)
        {
            smartObjectId = objectId;
            doorCenter = center;
            leftDoorPanel = leftPanel;
            rightDoorPanel = rightPanel;
            capacity = Mathf.Max(1, simultaneousCapacity);
            if (leftDoorPanel != null)
            {
                leftClosedPosition = leftDoorPanel.localPosition;
            }

            if (rightDoorPanel != null)
            {
                rightClosedPosition = rightDoorPanel.localPosition;
            }

            SetPhase(PassengerAiV2TrainDoorPhase.Closed);
        }

        private void Update()
        {
            if (Time.time < nextUpdateTime)
            {
                return;
            }

            nextUpdateTime = Time.time + 1f / Mathf.Max(2f, updateHz);
            RemoveDestroyedRequests();
            TickDoorCycle();
            ProcessReservations();
        }

        public bool RequestUse(PassengerAiV2Agent agent, int direction, float approachDistance)
        {
            if (agent == null)
            {
                return false;
            }

            PassengerAiV2TrainDoorFlow flow = direction >= 0
                ? PassengerAiV2TrainDoorFlow.Alight
                : PassengerAiV2TrainDoorFlow.Board;
            RequestRecord request = FindRequest(agent);
            if (request == null)
            {
                request = new RequestRecord
                {
                    Agent = agent,
                    Flow = flow,
                    Cycle = CycleIndex,
                    QueueSide = ChooseQueueSide(agent, flow),
                    QueueOrder = ++nextQueueOrder,
                    RequestedAt = Time.time,
                    ApproachDistance = approachDistance
                };
                requests.Add(request);
            }
            else
            {
                if (request.Cycle != CycleIndex)
                {
                    ResetForCycle(request, flow);
                }

                request.Flow = flow;
                request.ApproachDistance = approachDistance;
            }

            ProcessReservations();
            return request.Granted;
        }

        public bool HasReservation(PassengerAiV2Agent agent)
        {
            RequestRecord request = FindRequest(agent);
            return request != null
                && request.Cycle == CycleIndex
                && request.Granted
                && !request.Completed;
        }

        public bool CanPass(PassengerAiV2Agent agent)
        {
            RequestRecord request = FindRequest(agent);
            return request != null
                && request.Cycle == CycleIndex
                && request.Granted
                && !request.Completed
                && IsFlowPhase(request);
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
            if (request.Flow == PassengerAiV2TrainDoorFlow.Board
                && HasUnpassedAlighterInLane(request.Lane))
            {
                BoardingBeforeAlightingClearCount++;
            }

            if (request.Flow == PassengerAiV2TrainDoorFlow.Board
                && HasEarlierBoarderInSameQueue(request))
            {
                OutOfOrderBoardingCount++;
            }

            // 몸이 문턱을 완전히 넘은 시점부터는 다음 승객이 같은 Lane을 이어서 쓸 수 있다.
            // 완료 집계는 안전 이탈점에 도착한 뒤 Release에서 처리한다.
            request.Granted = false;
            ProcessReservations();
            return true;
        }

        public void Release(PassengerAiV2Agent agent)
        {
            RequestRecord request = FindRequest(agent);
            if (request == null || request.Completed)
            {
                return;
            }

            if (!request.Passed)
            {
                InvalidPassAttemptCount++;
                return;
            }

            request.Completed = true;
            request.Granted = false;
            request.Lane = -1;
            if (request.Flow == PassengerAiV2TrainDoorFlow.Alight)
            {
                AlightedCount++;
            }
            else
            {
                BoardedCount++;
            }

            ProcessReservations();
        }

        public void Cancel(PassengerAiV2Agent agent)
        {
            RequestRecord request = FindRequest(agent);
            if (request != null)
            {
                requests.Remove(request);
                ProcessReservations();
            }
        }

        public Vector2 GetQueuePosition(
            PassengerAiV2Agent agent,
            PassengerAiV2TrainDoorFlow flow)
        {
            RequestRecord ownRequest = FindRequest(agent);
            int side = ownRequest != null && ownRequest.QueueSide != 0
                ? ownRequest.QueueSide
                : (agent != null && agent.Position.x >= doorCenter.x ? 1 : -1);
            int queueIndex = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request.Cycle != CycleIndex
                    || request.Flow != flow
                    || request.QueueSide != side
                    || request.Completed
                    || request.Granted
                    || request.Passed)
                {
                    continue;
                }

                if (ownRequest == null || request.QueueOrder < ownRequest.QueueOrder)
                {
                    queueIndex++;
                }
            }

            int row = queueIndex;
            if (flow == PassengerAiV2TrainDoorFlow.Alight)
            {
                return doorCenter + new Vector2(
                    side * laneOffset,
                    1.45f + row * 0.82f);
            }

            return doorCenter + new Vector2(
                side * (2.05f + row * 0.22f),
                -1.65f - row * 0.88f);
        }

        public bool IsFlowReleased(PassengerAiV2TrainDoorFlow flow)
        {
            return flow == PassengerAiV2TrainDoorFlow.Alight
                ? Phase == PassengerAiV2TrainDoorPhase.Alighting
                : Phase == PassengerAiV2TrainDoorPhase.Boarding;
        }

        public Vector2 GetConvoyPosition(
            PassengerAiV2Agent agent,
            PassengerAiV2TrainDoorFlow flow)
        {
            RequestRecord ownRequest = FindRequest(agent);
            if (ownRequest == null
                || ownRequest.Cycle != CycleIndex
                || !IsFlowReleased(flow))
            {
                return GetQueuePosition(agent, flow);
            }

            RequestRecord predecessor = FindImmediatePredecessor(ownRequest);
            float behindDirection = flow == PassengerAiV2TrainDoorFlow.Alight ? 1f : -1f;
            float laneX = doorCenter.x + ownRequest.QueueSide * laneOffset;
            if (predecessor != null && predecessor.Agent != null)
            {
                Vector2 movingAnchor = predecessor.Agent.Position
                    + predecessor.Agent.Velocity * convoyLookAhead;
                return new Vector2(
                    Mathf.Lerp(movingAnchor.x, laneX, 0.32f),
                    movingAnchor.y + behindDirection * convoySpacing);
            }

            float headY = flow == PassengerAiV2TrainDoorFlow.Alight ? 1.04f : -1.04f;
            return doorCenter + new Vector2(ownRequest.QueueSide * laneOffset, headY);
        }

        public Vector2 GetApproachPoint(PassengerAiV2Agent agent)
        {
            RequestRecord request = FindRequest(agent);
            if (request == null)
            {
                return doorCenter;
            }

            float x = request.Lane == 0 ? -laneOffset : laneOffset;
            float y = request.Flow == PassengerAiV2TrainDoorFlow.Alight ? 0.92f : -0.92f;
            return doorCenter + new Vector2(x, y);
        }

        public Vector2 GetPassPoint(PassengerAiV2Agent agent)
        {
            RequestRecord request = FindRequest(agent);
            if (request == null)
            {
                return doorCenter;
            }

            float x = request.Lane == 0 ? -laneOffset : laneOffset;
            float y = request.Flow == PassengerAiV2TrainDoorFlow.Alight ? -0.78f : 0.78f;
            return doorCenter + new Vector2(x, y);
        }

        public Vector2 GetReleasePoint(PassengerAiV2Agent agent)
        {
            RequestRecord request = FindRequest(agent);
            if (request == null)
            {
                return doorCenter;
            }

            float x = request.Lane == 0 ? -laneOffset : laneOffset;
            float y = request.Flow == PassengerAiV2TrainDoorFlow.Alight ? -1.85f : 1.85f;
            return doorCenter + new Vector2(x, y);
        }

        private void TickDoorCycle()
        {
            float elapsed = Time.time - phaseStartedAt;
            switch (Phase)
            {
                case PassengerAiV2TrainDoorPhase.Closed:
                    if (elapsed >= closedDuration)
                    {
                        CycleIndex++;
                        SetPhase(PassengerAiV2TrainDoorPhase.Opening);
                    }
                    break;

                case PassengerAiV2TrainDoorPhase.Opening:
                    if (elapsed >= openingDuration)
                    {
                        SetPhase(PassengerAiV2TrainDoorPhase.Alighting);
                    }
                    break;

                case PassengerAiV2TrainDoorPhase.Alighting:
                    if (elapsed >= minimumFlowDuration
                        && CanBeginBoardingOverlap())
                    {
                        SetPhase(PassengerAiV2TrainDoorPhase.Boarding);
                    }
                    break;

                case PassengerAiV2TrainDoorPhase.Boarding:
                    if (elapsed >= minimumFlowDuration
                        && CountGranted() == 0
                        && !HasPending(PassengerAiV2TrainDoorFlow.Alight)
                        && !HasPending(PassengerAiV2TrainDoorFlow.Board))
                    {
                        SetPhase(PassengerAiV2TrainDoorPhase.ClosingWarning);
                    }
                    break;

                case PassengerAiV2TrainDoorPhase.ClosingWarning:
                    if (elapsed >= closingWarningDuration && CountGranted() == 0)
                    {
                        SetPhase(PassengerAiV2TrainDoorPhase.Closed);
                    }
                    break;
            }
        }

        private void ProcessReservations()
        {
            PassengerAiV2TrainDoorFlow allowedFlow;
            if (Phase == PassengerAiV2TrainDoorPhase.Alighting)
            {
                allowedFlow = PassengerAiV2TrainDoorFlow.Alight;
            }
            else if (Phase == PassengerAiV2TrainDoorPhase.Boarding)
            {
                allowedFlow = PassengerAiV2TrainDoorFlow.Board;
            }
            else
            {
                return;
            }

            int grantedCount = CountGranted();
            for (int lane = 0; lane < 2 && grantedCount < capacity; lane++)
            {
                if (IsLaneUsed(lane))
                {
                    continue;
                }

                RequestRecord best = FindFrontWaiting(allowedFlow, lane);
                if (best == null)
                {
                    continue;
                }

                best.Granted = true;
                best.Lane = lane;
                grantedCount++;
            }
        }

        private RequestRecord FindFrontWaiting(PassengerAiV2TrainDoorFlow flow, int lane)
        {
            int queueSide = lane == 0 ? -1 : 1;
            RequestRecord front = null;
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request.Agent == null
                    || request.Cycle != CycleIndex
                    || request.Flow != flow
                    || request.QueueSide != queueSide
                    || request.Completed
                    || request.Granted
                    || request.Passed)
                {
                    continue;
                }

                if (front == null || request.QueueOrder < front.QueueOrder)
                {
                    front = request;
                }
            }

            return front != null && front.ApproachDistance <= admissionDistance
                ? front
                : null;
        }

        private bool IsLaneUsed(int lane)
        {
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request.Granted
                    && !request.Completed
                    && request.Cycle == CycleIndex
                    && request.Lane == lane)
                {
                    return true;
                }
            }

            return false;
        }

        private int ChooseQueueSide(
            PassengerAiV2Agent agent,
            PassengerAiV2TrainDoorFlow flow)
        {
            if (agent == null)
            {
                return -1;
            }

            float sideOffset = flow == PassengerAiV2TrainDoorFlow.Alight
                ? laneOffset
                : 2.05f;
            float leftDistance = Mathf.Abs(agent.Position.x - (doorCenter.x - sideOffset));
            float rightDistance = Mathf.Abs(agent.Position.x - (doorCenter.x + sideOffset));
            float leftScore = leftDistance + CountQueueSide(flow, -1) * 0.58f;
            float rightScore = rightDistance + CountQueueSide(flow, 1) * 0.58f;
            if (Mathf.Abs(leftScore - rightScore) <= 0.08f)
            {
                return agent.Position.x <= doorCenter.x ? -1 : 1;
            }

            return leftScore < rightScore ? -1 : 1;
        }

        private int CountQueueSide(PassengerAiV2TrainDoorFlow flow, int side)
        {
            int count = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request.Cycle == CycleIndex
                    && request.Flow == flow
                    && request.QueueSide == side
                    && !request.Completed)
                {
                    count++;
                }
            }

            return count;
        }

        private bool HasPending(PassengerAiV2TrainDoorFlow flow)
        {
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request.Cycle == CycleIndex
                    && request.Flow == flow
                    && !request.Completed)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanBeginBoardingOverlap()
        {
            int unpassedAlighters = 0;
            bool remainingAlighterGranted = false;
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request.Cycle != CycleIndex
                    || request.Flow != PassengerAiV2TrainDoorFlow.Alight
                    || request.Completed
                    || request.Passed)
                {
                    continue;
                }

                unpassedAlighters++;
                remainingAlighterGranted |= request.Granted;
                if (unpassedAlighters > 1)
                {
                    return false;
                }
            }

            return unpassedAlighters == 0
                || (unpassedAlighters == 1 && remainingAlighterGranted);
        }

        private bool HasUnpassedAlighterInLane(int lane)
        {
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request.Cycle == CycleIndex
                    && request.Flow == PassengerAiV2TrainDoorFlow.Alight
                    && request.Lane == lane
                    && !request.Completed
                    && !request.Passed)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasEarlierBoarderInSameQueue(RequestRecord boarder)
        {
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request == boarder
                    || request.Cycle != CycleIndex
                    || request.Flow != PassengerAiV2TrainDoorFlow.Board
                    || request.QueueSide != boarder.QueueSide
                    || request.Completed
                    || request.Passed)
                {
                    continue;
                }

                if (request.QueueOrder < boarder.QueueOrder)
                {
                    return true;
                }
            }

            return false;
        }

        private RequestRecord FindImmediatePredecessor(RequestRecord ownRequest)
        {
            RequestRecord predecessor = null;
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request == ownRequest
                    || request.Agent == null
                    || request.Cycle != CycleIndex
                    || request.Flow != ownRequest.Flow
                    || request.QueueSide != ownRequest.QueueSide
                    || request.Completed
                    || request.Passed
                    || request.QueueOrder >= ownRequest.QueueOrder)
                {
                    continue;
                }

                if (predecessor == null || request.QueueOrder > predecessor.QueueOrder)
                {
                    predecessor = request;
                }
            }

            return predecessor;
        }

        private int CountWaiting(PassengerAiV2TrainDoorFlow flow)
        {
            int count = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request.Cycle == CycleIndex
                    && request.Flow == flow
                    && !request.Completed
                    && !request.Granted
                    && !request.Passed)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountGranted()
        {
            int count = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                RequestRecord request = requests[i];
                if (request.Cycle == CycleIndex && request.Granted && !request.Completed)
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsFlowPhase(RequestRecord request)
        {
            return request.Flow == PassengerAiV2TrainDoorFlow.Alight
                ? Phase == PassengerAiV2TrainDoorPhase.Alighting
                    || (Phase == PassengerAiV2TrainDoorPhase.Boarding && request.Granted)
                : Phase == PassengerAiV2TrainDoorPhase.Boarding;
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

        private void ResetForCycle(RequestRecord request, PassengerAiV2TrainDoorFlow flow)
        {
            request.Flow = flow;
            request.Cycle = CycleIndex;
            request.Lane = -1;
            request.QueueSide = ChooseQueueSide(request.Agent, flow);
            request.QueueOrder = ++nextQueueOrder;
            request.RequestedAt = Time.time;
            request.ApproachDistance = float.PositiveInfinity;
            request.Granted = false;
            request.Passed = false;
            request.Completed = false;
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
        }

        private void SetPhase(PassengerAiV2TrainDoorPhase phase)
        {
            if (phase == PassengerAiV2TrainDoorPhase.Boarding
                && HasPending(PassengerAiV2TrainDoorFlow.Alight))
            {
                BoardingOverlapStartCount++;
            }

            Phase = phase;
            phaseStartedAt = Time.time;
            bool open = phase != PassengerAiV2TrainDoorPhase.Closed;
            if (leftDoorPanel != null)
            {
                leftDoorPanel.localPosition = open
                    ? leftClosedPosition + Vector3.left * 0.78f
                    : leftClosedPosition;
            }

            if (rightDoorPanel != null)
            {
                rightDoorPanel.localPosition = open
                    ? rightClosedPosition + Vector3.right * 0.78f
                    : rightClosedPosition;
            }
        }
    }
}

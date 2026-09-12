using System;
using UnityEngine;

namespace SubwayCarry.TeamReview.KimJun.Core.Contracts
{
    public enum DoorOpeningSide
    {
        Left,
        Right,
        Both
    }

    public enum TrainDoorState
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    public enum TrainMotionPhase
    {
        Stopped,
        Departing,
        Cruising,
        SpeedChanging,
        Arriving,
        EmergencyBraking
    }

    public readonly struct TrainMotionSnapshot
    {
        public TrainMotionSnapshot(
            int sequenceId,
            TrainMotionPhase phase,
            Vector2 inertiaDirection,
            float intensity)
        {
            SequenceId = sequenceId;
            Phase = phase;
            InertiaDirection = inertiaDirection.sqrMagnitude > 0f
                ? inertiaDirection.normalized
                : Vector2.zero;
            Intensity = Mathf.Max(0f, intensity);
        }

        public int SequenceId { get; }
        public TrainMotionPhase Phase { get; }
        public Vector2 InertiaDirection { get; }
        public float Intensity { get; }
    }

    public readonly struct TrainDoorSnapshot
    {
        public TrainDoorSnapshot(
            string stationId,
            DoorOpeningSide openingSide,
            TrainDoorState state)
        {
            StationId = stationId;
            OpeningSide = openingSide;
            State = state;
        }

        public string StationId { get; }
        public DoorOpeningSide OpeningSide { get; }
        public TrainDoorState State { get; }
    }

    public readonly struct TransitProgressSnapshot
    {
        public TransitProgressSnapshot(
            string currentStationId,
            string nextStationId,
            int routeIndex,
            int routeStopCount,
            bool isTransferStop,
            bool isDestination,
            DoorOpeningSide openingSide)
        {
            CurrentStationId = currentStationId;
            NextStationId = nextStationId;
            RouteIndex = routeIndex;
            RouteStopCount = routeStopCount;
            IsTransferStop = isTransferStop;
            IsDestination = isDestination;
            OpeningSide = openingSide;
        }

        public string CurrentStationId { get; }
        public string NextStationId { get; }
        public int RouteIndex { get; }
        public int RouteStopCount { get; }
        public bool IsTransferStop { get; }
        public bool IsDestination { get; }
        public DoorOpeningSide OpeningSide { get; }
    }

    public interface ITrainMotionProvider
    {
        TrainMotionSnapshot CurrentTrainMotion { get; }
        event Action<TrainMotionSnapshot> TrainMotionChanged;
    }

    public interface ITrainDoorStateProvider
    {
        TrainDoorSnapshot CurrentDoorState { get; }
        event Action<TrainDoorSnapshot> TrainDoorStateChanged;
    }

    public interface ITransitProgressProvider
    {
        TransitProgressSnapshot CurrentTransitProgress { get; }
        event Action<TransitProgressSnapshot> TransitProgressChanged;
    }
}

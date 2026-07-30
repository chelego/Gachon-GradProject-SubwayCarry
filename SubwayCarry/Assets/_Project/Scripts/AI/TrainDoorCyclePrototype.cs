using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    public sealed class TrainDoorCyclePrototype : MonoBehaviour
    {
        [SerializeField] private TrainDoorController[] doors;
        [SerializeField, Min(0f)] private float initialDelay = 1.5f;
        [SerializeField, Min(0.5f)] private float openDuration = 12f;
        [SerializeField, Min(0f)] private float boardingClearanceDelay = 0.6f;
        [SerializeField, Min(0.5f)] private float travelDuration = 30f;

        private PassengerDoorway[] controlledDoorways;

        public int StopNumber { get; private set; }
        public bool IsTravelling { get; private set; }
        public float TravelTimeRemaining { get; private set; }

        public void Configure(TrainDoorController[] controlledDoors)
        {
            doors = controlledDoors;
        }

        public void StopCycle()
        {
            StopAllCoroutines();
            IsTravelling = false;
            TravelTimeRemaining = 0f;
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            EnsureDoors();
            EnsureDoorways();
            SetDoorsOpen(false);
            SetBoardingAdmission(false);
            yield return new WaitForSeconds(initialDelay);

            while (true)
            {
                StopNumber++;
                SetBoardingAdmission(true);
                SetDoorsOpen(true);
                yield return new WaitUntil(AreDoorsOpen);
                yield return WaitForExitingPassengers();
                yield return new WaitForSeconds(openDuration);
                SetBoardingAdmission(false);
                yield return WaitForActiveBoardingTraffic();

                SetDoorsOpen(false);
                yield return new WaitUntil(AreDoorsClosed);
                yield return WaitForTravel();
            }
        }

        private void EnsureDoorways()
        {
            PassengerDoorway[] found = FindObjectsByType<PassengerDoorway>(
                FindObjectsSortMode.None);
            var matchingDoorways = new List<PassengerDoorway>();
            foreach (PassengerDoorway doorway in found)
            {
                if (doorway == null || doorway.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                foreach (TrainDoorController controlledDoor in doors)
                {
                    if (doorway.Door == controlledDoor)
                    {
                        matchingDoorways.Add(doorway);
                        break;
                    }
                }
            }

            controlledDoorways = matchingDoorways.ToArray();
        }

        private IEnumerator WaitForActiveBoardingTraffic()
        {
            while (HasActiveBoardingTraffic())
            {
                yield return null;
            }

            if (boardingClearanceDelay > 0f)
            {
                yield return new WaitForSeconds(boardingClearanceDelay);
            }
        }

        private IEnumerator WaitForExitingPassengers()
        {
            while (HasExitingPassengers())
            {
                yield return null;
            }
        }

        private bool HasExitingPassengers()
        {
            if (controlledDoorways == null)
            {
                return false;
            }

            foreach (PassengerDoorway doorway in controlledDoorways)
            {
                if (doorway != null && doorway.ExitReservationCount > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasActiveBoardingTraffic()
        {
            if (controlledDoorways == null)
            {
                return false;
            }

            foreach (PassengerDoorway doorway in controlledDoorways)
            {
                if (doorway != null && doorway.HasActiveBoardingTraffic())
                {
                    return true;
                }
            }

            return false;
        }

        private void SetBoardingAdmission(bool open)
        {
            if (controlledDoorways == null)
            {
                return;
            }

            foreach (PassengerDoorway doorway in controlledDoorways)
            {
                doorway?.SetBoardingAdmission(open);
            }
        }

        private void EnsureDoors()
        {
            if (doors != null && doors.Length > 0)
            {
                return;
            }

            TrainDoorController[] found = FindObjectsByType<TrainDoorController>(
                FindObjectsSortMode.None);
            var sameSceneDoors = new List<TrainDoorController>(found.Length);

            foreach (TrainDoorController door in found)
            {
                if (door != null && door.gameObject.scene == gameObject.scene)
                {
                    sameSceneDoors.Add(door);
                }
            }

            doors = sameSceneDoors.ToArray();
        }

        private IEnumerator WaitForTravel()
        {
            IsTravelling = true;
            TravelTimeRemaining = travelDuration;

            while (TravelTimeRemaining > 0f)
            {
                TravelTimeRemaining = Mathf.Max(0f, TravelTimeRemaining - Time.deltaTime);
                yield return null;
            }

            IsTravelling = false;
        }

        private void SetDoorsOpen(bool open)
        {
            if (doors == null)
            {
                return;
            }

            foreach (TrainDoorController door in doors)
            {
                door?.SetOpen(open);
            }
        }

        private bool AreDoorsOpen()
        {
            return AreDoorsInState(true);
        }

        private bool AreDoorsClosed()
        {
            return AreDoorsInState(false);
        }

        private bool AreDoorsInState(bool open)
        {
            if (doors == null || doors.Length == 0)
            {
                return true;
            }

            foreach (TrainDoorController door in doors)
            {
                if (door == null)
                {
                    continue;
                }

                if ((open && !door.IsOpen) || (!open && !door.IsClosed))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

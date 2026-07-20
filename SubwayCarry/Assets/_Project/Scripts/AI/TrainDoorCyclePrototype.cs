using System.Collections;
using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    public sealed class TrainDoorCyclePrototype : MonoBehaviour
    {
        [SerializeField] private TrainDoorController[] doors;
        [SerializeField, Min(0f)] private float initialDelay = 1.5f;
        [SerializeField, Min(0.5f)] private float openDuration = 6f;
        [SerializeField, Min(0.5f)] private float travelDuration = 30f;

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
            SetDoorsOpen(false);
            yield return new WaitForSeconds(initialDelay);

            while (true)
            {
                StopNumber++;
                SetDoorsOpen(true);
                yield return new WaitUntil(AreDoorsOpen);
                yield return new WaitForSeconds(openDuration);

                SetDoorsOpen(false);
                yield return new WaitUntil(AreDoorsClosed);
                yield return WaitForTravel();
            }
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

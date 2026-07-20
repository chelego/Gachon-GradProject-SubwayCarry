using System.Collections;
using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    public sealed class TrainDoorCyclePrototype : MonoBehaviour
    {
        [SerializeField] private TrainDoorController[] doors;
        [SerializeField, Min(0f)] private float initialDelay = 1.5f;
        [SerializeField, Min(0.5f)] private float openDuration = 3f;
        [SerializeField, Min(0.5f)] private float travelDuration = 30f;

        public int StopNumber { get; private set; }

        public void Configure(TrainDoorController[] controlledDoors)
        {
            doors = controlledDoors;
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
                yield return new WaitForSeconds(travelDuration);
            }
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

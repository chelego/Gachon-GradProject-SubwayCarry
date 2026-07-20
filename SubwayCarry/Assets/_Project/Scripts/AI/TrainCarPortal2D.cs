using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class TrainCarPortal2D : MonoBehaviour
    {
        [SerializeField] private Transform destination;
        [SerializeField] private Transform destinationCarCenter;
        [SerializeField] private GridNavigation2D destinationNavigation;
        [SerializeField] private TrainCarCameraController cameraController;

        public void Configure(
            Transform destinationPoint,
            Transform carCenter,
            GridNavigation2D navigation,
            TrainCarCameraController trainCarCamera)
        {
            destination = destinationPoint;
            destinationCarCenter = carCenter;
            destinationNavigation = navigation;
            cameraController = trainCarCamera;
        }

        private void Reset()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerBoardingCyclePrototype player = other.GetComponentInParent<PlayerBoardingCyclePrototype>();
            if (player != null && destination != null && destinationCarCenter != null)
            {
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.position = destination.position;
                }
                else
                {
                    player.transform.position = destination.position;
                }

                player.EnterTrainCar(destinationCarCenter.position);
                cameraController?.FocusOn(destinationCarCenter);
                return;
            }

            PassengerPrototypeAgent passenger = other.GetComponentInParent<PassengerPrototypeAgent>();
            if (passenger == null || destination == null || destinationNavigation == null)
            {
                return;
            }

            passenger.transform.position = destination.position;
            passenger.EnterTrainCar(destinationNavigation);
            cameraController?.FocusOn(destinationCarCenter);
        }
    }
}

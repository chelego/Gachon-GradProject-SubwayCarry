using SubwayCarry.Gameplay;
using UnityEngine;

namespace SubwayCarry.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class PrototypeAreaTrigger : MonoBehaviour
    {
        [SerializeField] private PrototypeGameFlowController flowController;
        [SerializeField] private PrototypeAreaAction action;

        public void Configure(
            PrototypeGameFlowController flow,
            PrototypeAreaAction areaAction)
        {
            flowController = flow;
            action = areaAction;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (flowController == null ||
                other.GetComponentInParent<PlayerController>() == null)
            {
                return;
            }

            switch (action)
            {
                case PrototypeAreaAction.EnterDepartureEscalator:
                    flowController.NotifyEnteredDepartureEscalator();
                    break;

                case PrototypeAreaAction.ReturnToDepartureConcourse:
                    flowController.NotifyReturnedToDepartureConcourse();
                    break;

                case PrototypeAreaAction.EnterDeparturePlatform:
                    flowController.NotifyEnteredDeparturePlatform();
                    break;

                case PrototypeAreaAction.ReturnToDepartureEscalator:
                    flowController.NotifyReturnedToDepartureEscalator();
                    break;

                case PrototypeAreaAction.BoardTrain:
                    flowController.NotifyBoardedTrain();
                    break;

                case PrototypeAreaAction.LeaveTrainAtDestination:
                    flowController.NotifyLeftTrainAtDestination();
                    break;

                case PrototypeAreaAction.EnterDestinationEscalator:
                    flowController.NotifyEnteredDestinationEscalator();
                    break;

                case PrototypeAreaAction.ReturnToDestinationPlatform:
                    flowController.NotifyReturnedToDestinationPlatform();
                    break;

                case PrototypeAreaAction.EnterDestinationConcourse:
                    flowController.NotifyEnteredDestinationConcourse();
                    break;

                case PrototypeAreaAction.ReturnToDestinationEscalator:
                    flowController.NotifyReturnedToDestinationEscalator();
                    break;

                case PrototypeAreaAction.CompleteDeliveryAtDestinationExit:
                    flowController.NotifyExitedDestinationGate();
                    break;
            }
        }
    }
}

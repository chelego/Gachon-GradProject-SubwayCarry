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
                case PrototypeAreaAction.BoardTrain:
                    flowController.NotifyBoardedTrain();
                    break;

                case PrototypeAreaAction.LeaveTrainAtDestination:
                    flowController.NotifyLeftTrainAtDestination();
                    break;
            }
        }
    }
}

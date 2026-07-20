using UnityEngine;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    public sealed class TrainCarCameraController : MonoBehaviour
    {
        [SerializeField] private float cameraZ = -10f;

        public void FocusOn(Transform trainCarCenter)
        {
            if (trainCarCenter == null)
            {
                return;
            }

            Vector3 center = trainCarCenter.position;
            transform.position = new Vector3(center.x, center.y, cameraZ);
        }
    }
}

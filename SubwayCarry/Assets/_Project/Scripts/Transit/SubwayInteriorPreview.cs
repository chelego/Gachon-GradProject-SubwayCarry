using SubwayCarry.AI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Transit
{
    /// <summary>Camera and door inspection controls for the standalone art scene.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class SubwayInteriorPreview : MonoBehaviour
    {
        [SerializeField] private TrainDoorController[] doors;
        [SerializeField] private float carHalfWidth = 20f;
        [SerializeField] private float closeViewSize = 6.1f;
        private Camera viewCamera;
        private bool overview;
        private bool doorsOpen;

        public void Configure(TrainDoorController[] sceneDoors) => doors = sceneDoors;

        private void Awake()
        {
            viewCamera = GetComponent<Camera>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.fKey.wasPressedThisFrame)
                {
                    overview = !overview;
                    if (!overview) viewCamera.orthographicSize = closeViewSize;
                }
                if (keyboard.homeKey.wasPressedThisFrame)
                {
                    overview = false;
                    viewCamera.orthographicSize = closeViewSize;
                    transform.position = new Vector3(0f, 0f, -10f);
                }
                if (keyboard.spaceKey.wasPressedThisFrame)
                {
                    doorsOpen = !doorsOpen;
                    if (doors != null)
                        foreach (var door in doors)
                            if (door != null) door.SetOpen(doorsOpen);
                }
                if (!overview)
                {
                    float direction = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                        - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                    transform.position += Vector3.right * (direction * 8f * Time.unscaledDeltaTime);
                }
            }
            if (overview)
            {
                viewCamera.orthographicSize = Mathf.Max(6.1f, 20.8f / Mathf.Max(.1f, viewCamera.aspect));
                transform.position = new Vector3(0f, 0f, -10f);
            }
            else if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                viewCamera.orthographicSize = Mathf.Clamp(viewCamera.orthographicSize - scroll * .004f, 4.7f, 12.5f);
            }
            float limit = Mathf.Max(0f, carHalfWidth - viewCamera.orthographicSize * viewCamera.aspect);
            Vector3 position = transform.position;
            position.x = Mathf.Clamp(position.x, -limit, limit);
            transform.position = position;
        }
    }
}

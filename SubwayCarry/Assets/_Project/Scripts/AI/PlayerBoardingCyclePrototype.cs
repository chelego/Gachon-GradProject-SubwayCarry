using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class PlayerBoardingCyclePrototype : MonoBehaviour
    {
        private enum CycleState
        {
            WaitingForBoarding,
            Boarding,
            Riding,
            Exiting,
            GameOver,
            Completed
        }

        [SerializeField] private TrainDoorController boardingDoor;
        [SerializeField] private TrainDoorCyclePrototype doorCycle;
        [SerializeField] private Vector2 trainCenter;
        [SerializeField] private Vector2 trainInteriorHalfExtents = new Vector2(8.8f, 3.75f);
        [SerializeField, Min(0.1f)] private float moveSpeed = 3f;
        [SerializeField] private CycleState state;

        private Rigidbody2D body;
        private Vector2 moveInput;

        public string CurrentState => state.ToString();

        public void Configure(
            TrainDoorController targetDoor,
            TrainDoorCyclePrototype cycle,
            Vector2 carCenter)
        {
            boardingDoor = targetDoor;
            doorCycle = cycle;
            trainCenter = carCenter;
        }

        public void EnterTrainCar(Vector2 carCenter)
        {
            trainCenter = carCenter;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            state = CycleState.WaitingForBoarding;
        }

        private void Update()
        {
            ReadMovementInput();
            UpdateCycleState();
        }

        private void FixedUpdate()
        {
            if (!CanMove())
            {
                return;
            }

            body.MovePosition(body.position + moveInput * moveSpeed * Time.fixedDeltaTime);
        }

        private void ReadMovementInput()
        {
            moveInput = Vector2.zero;
            if (!CanMove() || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.wKey.isPressed)
            {
                moveInput.y += 1f;
            }

            if (Keyboard.current.sKey.isPressed)
            {
                moveInput.y -= 1f;
            }

            if (Keyboard.current.aKey.isPressed)
            {
                moveInput.x -= 1f;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                moveInput.x += 1f;
            }

            moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        }

        private void UpdateCycleState()
        {
            if (boardingDoor == null || state == CycleState.GameOver || state == CycleState.Completed)
            {
                return;
            }

            switch (state)
            {
                case CycleState.WaitingForBoarding:
                    if (boardingDoor.IsOpen)
                    {
                        state = CycleState.Boarding;
                    }
                    break;

                case CycleState.Boarding:
                    if (boardingDoor.IsClosed)
                    {
                        state = IsInsideTrain() ? CycleState.Riding : CycleState.GameOver;
                        if (state == CycleState.GameOver)
                        {
                            EndCycle();
                        }
                    }
                    break;

                case CycleState.Riding:
                    if (boardingDoor.IsOpen)
                    {
                        state = CycleState.Exiting;
                    }
                    break;

                case CycleState.Exiting:
                    if (!IsInsideTrain())
                    {
                        state = CycleState.Completed;
                        EndCycle();
                    }
                    else if (boardingDoor.IsClosed)
                    {
                        state = CycleState.GameOver;
                        EndCycle();
                    }
                    break;
            }
        }

        private bool IsInsideTrain()
        {
            Vector2 offset = body.position - trainCenter;
            return Mathf.Abs(offset.x) <= trainInteriorHalfExtents.x &&
                   Mathf.Abs(offset.y) <= trainInteriorHalfExtents.y;
        }

        private bool CanMove()
        {
            return state != CycleState.GameOver && state != CycleState.Completed;
        }

        private void EndCycle()
        {
            moveInput = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            doorCycle?.StopCycle();
        }

        private void OnGUI()
        {
            if (state != CycleState.GameOver && state != CycleState.Completed)
            {
                return;
            }

            string message = state == CycleState.Completed ? "CYCLE COMPLETE" : "GAME OVER";
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 36,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = state == CycleState.Completed
                ? new Color(0.45f, 1f, 0.65f)
                : new Color(1f, 0.35f, 0.35f);

            GUI.Label(new Rect(0f, Screen.height * 0.38f, Screen.width, 80f), message, style);
        }
    }
}

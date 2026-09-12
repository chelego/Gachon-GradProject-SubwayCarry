using SubwayCarry.Gameplay;
using UnityEngine;

namespace SubwayCarry.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class StationMapPortalPrototype : MonoBehaviour
    {
        [SerializeField] private StationMapScreenSwitcherPrototype screenSwitcher;
        [SerializeField] private GameObject targetScreen;
        [SerializeField] private Transform arrivalPoint;

        public GameObject TargetScreen => targetScreen;
        public Transform ArrivalPoint => arrivalPoint;

        public void Configure(
            StationMapScreenSwitcherPrototype switcher,
            GameObject destinationScreen,
            Transform destinationPoint)
        {
            screenSwitcher = switcher;
            targetScreen = destinationScreen;
            arrivalPoint = destinationPoint;
        }

        public bool Transfer(PlayerController player)
        {
            if (player == null ||
                screenSwitcher == null ||
                targetScreen == null ||
                arrivalPoint == null)
            {
                return false;
            }

            Vector2 destination = arrivalPoint.position;
            screenSwitcher.Activate(targetScreen);

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = destination;
                body.linearVelocity = Vector2.zero;
            }

            player.transform.position = new Vector3(
                destination.x,
                destination.y,
                player.transform.position.z);
            Physics2D.SyncTransforms();
            return true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                Transfer(player);
            }
        }
    }
}

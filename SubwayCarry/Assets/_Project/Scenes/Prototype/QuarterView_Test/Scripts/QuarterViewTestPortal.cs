using UnityEngine;

namespace SubwayCarry.Prototype.QuarterView
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class QuarterViewTestPortal : MonoBehaviour
    {
        [SerializeField] private QuarterViewTestFlow flow;
        [SerializeField] private GameObject destinationMap;
        [SerializeField] private Transform destinationSpawn;

        public GameObject DestinationMap => destinationMap;
        public Transform DestinationSpawn => destinationSpawn;

        public void Configure(
            QuarterViewTestFlow transitionFlow,
            GameObject targetMap,
            Transform targetSpawn)
        {
            flow = transitionFlow;
            destinationMap = targetMap;
            destinationSpawn = targetSpawn;
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            QuarterViewTestPlayer player = other.GetComponentInParent<QuarterViewTestPlayer>();
            if (player != null)
            {
                flow?.TryTravel(destinationMap, destinationSpawn);
            }
        }
    }
}

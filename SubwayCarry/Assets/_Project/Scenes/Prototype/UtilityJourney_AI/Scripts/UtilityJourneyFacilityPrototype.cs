using UnityEngine;

namespace SubwayCarry.AI.UtilityJourney
{
    [DisallowMultipleComponent]
    public sealed class UtilityJourneyFacilityPrototype : MonoBehaviour
    {
        [SerializeField] private string facilityId;
        [SerializeField] private UtilityJourneyFacilityKind kind;
        [SerializeField] private UtilityJourneyArea area;
        [SerializeField] private UtilityJourneyDirection direction;
        [SerializeField, Min(0.05f)] private float acceptanceRadius = 0.22f;
        [SerializeField, Min(0f)] private float interactionSeconds = 0.25f;
        [SerializeField, Range(0f, 1f)] private float crowdCost;
        [SerializeField, Min(0f)] private float estimatedQueueSeconds;
        [SerializeField] private bool available = true;
        [SerializeField] private UtilityJourneyWaitingStyle waitingStyle;
        [SerializeField] private string routeGroup;
        [SerializeField, Range(0f, 1f)] private float comfortValue = 0.5f;
        [SerializeField, Range(0f, 1f)] private float transferConvenience = 0.5f;
        [SerializeField] private UtilityJourneyPassagePrototype passage;
        [SerializeField] private Transform traversalEntry;
        [SerializeField] private Transform traversalDestination;
        [SerializeField] private UtilityJourneyArea destinationArea;
        [SerializeField] private Transform destinationSpawn;
        [SerializeField] private Transform destinationEntry;
        [SerializeField] private Transform destinationRelease;

        public string FacilityId => facilityId;
        public UtilityJourneyFacilityKind Kind => kind;
        public UtilityJourneyArea Area => area;
        public UtilityJourneyDirection Direction => direction;
        public float AcceptanceRadius => acceptanceRadius;
        public float InteractionSeconds => interactionSeconds;
        public float CrowdCost => crowdCost;
        public float EstimatedQueueSeconds => estimatedQueueSeconds;
        public bool IsAvailable => available;
        public UtilityJourneyWaitingStyle WaitingStyle => waitingStyle;
        public string RouteGroup => routeGroup ?? string.Empty;
        public float ComfortValue => comfortValue;
        public float TransferConvenience => transferConvenience;
        public UtilityJourneyPassagePrototype Passage => passage;
        public bool HasTraversal => traversalDestination != null;
        public Transform TraversalEntry => traversalEntry;
        public Transform TraversalDestination => traversalDestination;
        public bool HasTransition => destinationSpawn != null;
        public UtilityJourneyArea DestinationArea => destinationArea;
        public Transform DestinationSpawn => destinationSpawn;
        public Transform DestinationEntry => destinationEntry;
        public Transform DestinationRelease => destinationRelease;
        public int UseCount { get; private set; }

        public void Configure(
            string id,
            UtilityJourneyFacilityKind facilityKind,
            UtilityJourneyArea facilityArea,
            UtilityJourneyDirection facilityDirection,
            float radius,
            float duration,
            float congestion = 0f,
            float queueSeconds = 0f)
        {
            facilityId = id;
            kind = facilityKind;
            area = facilityArea;
            direction = facilityDirection;
            acceptanceRadius = Mathf.Max(0.05f, radius);
            interactionSeconds = Mathf.Max(0f, duration);
            crowdCost = Mathf.Clamp01(congestion);
            estimatedQueueSeconds = Mathf.Max(0f, queueSeconds);
            available = true;
        }

        public void ConfigureBehavior(
            UtilityJourneyWaitingStyle style,
            string group)
        {
            waitingStyle = style;
            routeGroup = group ?? string.Empty;
        }

        public void ConfigureDecisionTraits(
            float comfort,
            float convenience)
        {
            comfortValue = Mathf.Clamp01(comfort);
            transferConvenience = Mathf.Clamp01(convenience);
        }

        public void ConfigureTraversal(
            Transform entry,
            Transform destination,
            UtilityJourneyPassagePrototype controlledPassage = null)
        {
            traversalEntry = entry;
            traversalDestination = destination;
            passage = controlledPassage;
        }

        public void ConfigureTransition(
            UtilityJourneyArea nextArea,
            Transform arrival)
        {
            ConfigureTransition(nextArea, arrival, null, null);
        }

        public void ConfigureTransition(
            UtilityJourneyArea nextArea,
            Transform arrival,
            Transform arrivalEntry,
            Transform release)
        {
            destinationArea = nextArea;
            destinationSpawn = arrival;
            destinationEntry = arrivalEntry;
            destinationRelease = release;
        }

        public void SetAvailable(bool value)
        {
            available = value;
        }

        public void SetDynamicCosts(float congestion, float queueSeconds)
        {
            crowdCost = Mathf.Clamp01(congestion);
            estimatedQueueSeconds = Mathf.Max(0f, queueSeconds);
        }

        public void RecordUse()
        {
            UseCount++;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, acceptanceRadius);
        }
    }
}

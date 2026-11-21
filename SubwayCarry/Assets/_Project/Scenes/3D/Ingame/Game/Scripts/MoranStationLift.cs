using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Gameplay3D
{
    // The cab, the two landings and their doors belong to the station instance.
    public sealed class MoranStationLift : MonoBehaviour
    {
        [SerializeField] Transform cabin;
        [SerializeField] Transform[] landings;
        [SerializeField] Transform[] leaves;
        [SerializeField] Collider[] landingBarriers;
        [SerializeField] Collider cabinBarrier;

        enum Phase { Open, Closing, Travel, Opening }
        Phase phase;
        int floor;
        int destination;
        float aperture = 1f;
        CharacterController visitor;
        Transform rider;
        Transform previousParent;

        public Transform Cabin => cabin;
        public int CurrentFloor => floor;
        public bool Moving => phase == Phase.Travel;
        public bool DoorsOpen => phase == Phase.Open;
        public Transform Landing(int level) => landings[level];

        public void Configure(Transform car, Transform[] stops, Transform[] doorPanels,
            Collider[] stopBarriers, Collider carBarrier)
        {
            cabin = car;
            landings = stops;
            leaves = doorPanels;
            landingBarriers = stopBarriers;
            cabinBarrier = carBarrier;
            SetDoors();
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                var main = Camera.main;
                if (main != null) TryInteract(main.GetComponentInParent<CharacterController>());
            }
            switch (phase)
            {
                case Phase.Closing:
                    bool doorwayOccupied = AtThreshold(visitor);
                    aperture = Mathf.MoveTowards(aperture, doorwayOccupied ? 1f : 0f, Time.deltaTime * 1.2f);
                    if (aperture <= 0f)
                    {
                        if (Inside(visitor))
                        {
                            rider = visitor.transform;
                            previousParent = rider.parent;
                            rider.SetParent(cabin, true);
                        }
                        phase = Phase.Travel;
                    }
                    SetDoors();
                    break;
                case Phase.Travel:
                    var p = cabin.localPosition;
                    float goal = landings[destination].localPosition.y;
                    p.y = Mathf.MoveTowards(p.y, goal, Time.deltaTime * .95f);
                    cabin.localPosition = p;
                    Physics.SyncTransforms();
                    if (p.y == goal)
                    {
                        floor = destination;
                        phase = Phase.Opening;
                        ReleaseRider();
                    }
                    break;
                case Phase.Opening:
                    aperture = Mathf.MoveTowards(aperture, 1f, Time.deltaTime * 1.2f);
                    if (aperture >= 1f) phase = Phase.Open;
                    SetDoors();
                    break;
            }
        }

        public bool TryInteract(CharacterController actor)
        {
            if (actor == null || phase != Phase.Open || !GetDestination(actor, out destination)) return false;
            visitor = actor;
            if (destination != floor) phase = Phase.Closing;
            return true;
        }

        public string InteractionHint(CharacterController actor)
        {
            if (!GetDestination(actor, out var target)) return null;
            if (phase != Phase.Open) return "엘리베이터가 움직입니다. 문이 열리면 이동하세요.";
            if (Inside(actor)) return floor == 0 ? "E   B1 대합실로 올라가기" : "E   B2 승강장으로 내려가기";
            return target == floor ? "문이 열렸습니다. 안으로 들어가세요." : "E   엘리베이터 호출";
        }

        bool GetDestination(CharacterController actor, out int target)
        {
            if (Inside(actor))
            {
                target = 1 - floor;
                return true;
            }
            var point = FootPosition(actor);
            target = Mathf.Abs(point.y - landings[1].localPosition.y) < .4f ? 1 : 0;
            return Mathf.Abs(point.x) <= 1.45f && point.z >= 1.15f && point.z <= 2.85f
                && Mathf.Abs(point.y - landings[target].localPosition.y) <= .4f;
        }

        Vector3 FootPosition(CharacterController actor)
        {
            var foot = actor.center - Vector3.up * (actor.height * .5f);
            return transform.InverseTransformPoint(actor.transform.TransformPoint(foot));
        }

        bool Inside(CharacterController actor)
        {
            if (actor == null) return false;
            var p = FootPosition(actor);
            return Mathf.Abs(p.x) < .89f && p.z > -.92f && p.z < .59f
                && Mathf.Abs(p.y - cabin.localPosition.y) < .4f;
        }

        bool AtThreshold(CharacterController actor)
        {
            if (actor == null) return false;
            var p = FootPosition(actor);
            return Mathf.Abs(p.x) < .92f && p.z >= .59f && p.z <= 1.35f
                && Mathf.Abs(p.y - cabin.localPosition.y) < .4f;
        }

        void SetDoors()
        {
            // Four telescopic leaves fit inside a narrow shaft without protruding through its sides.
            for (int bank = 0; bank < 3; bank++)
            {
                float open = bank == floor || bank == 2 ? aperture : 0f;
                for (int i = 0; i < 4; i++)
                {
                    var leaf = leaves[4 * bank + i];
                    var p = leaf.localPosition;
                    bool inner = i == 1 || i == 2;
                    float side = i < 2 ? -1f : 1f;
                    p.x = side * Mathf.Lerp(inner ? .1625f : .4875f, inner ? .815f : .85f, open);
                    leaf.localPosition = p;
                }
            }
            for (int i = 0; i < landingBarriers.Length; i++)
                landingBarriers[i].enabled = i != floor || aperture < .88f;
            cabinBarrier.enabled = aperture < .88f;
        }

        void ReleaseRider()
        {
            if (rider != null && rider.parent == cabin) rider.SetParent(previousParent, true);
            rider = null;
            previousParent = null;
        }

        void OnDisable() => ReleaseRider();
    }
}

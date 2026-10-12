using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SubwayCarry.Gameplay3D
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Metro3DPlayer))]
    public sealed class Metro3DInteractionSelector : MonoBehaviour
    {
        public readonly struct InteractionOption
        {
            public InteractionOption(
                Metro3DFacilityKind kind,
                string label,
                Metro3DFacility facility,
                int candidateCount)
            {
                Kind = kind;
                Label = label;
                Facility = facility;
                CandidateCount = candidateCount;
            }

            public Metro3DFacilityKind Kind { get; }
            public string Label { get; }
            public Metro3DFacility Facility { get; }
            public int CandidateCount { get; }
        }

        private struct CandidateGroup
        {
            public Metro3DFacilityKind Kind;
            public Metro3DFacility Best;
            public float BestScore;
            public float CurrentScore;
            public int Count;
        }

        [SerializeField, Min(0.5f)] private float acquireRange = 1.85f;
        [SerializeField, Min(0.5f)] private float releaseRange = 2.3f;
        [SerializeField, Range(-0.5f, 1f)] private float minimumViewDot = 0.05f;
        [SerializeField, Min(0f)] private float viewPenalty = 0.65f;
        [SerializeField, Min(0f)] private float switchAdvantage = 0.28f;
        [SerializeField, Range(1, 6)] private int maximumOptions = 4;

        private readonly List<CandidateGroup> groups = new List<CandidateGroup>(8);
        private readonly List<InteractionOption> options = new List<InteractionOption>(8);
        private Metro3DPlayer player;
        private Metro3DFacility selectedFacility;
        private Metro3DFacilityKind selectedKind;
        private bool hasSelectedKind;
        private float scrollCooldown;

        public IReadOnlyList<InteractionOption> Options => options;
        public int OptionCount => options.Count;
        public int SelectedIndex { get; private set; }
        public Metro3DFacility SelectedFacility => selectedFacility;

        private void Awake()
        {
            player = GetComponent<Metro3DPlayer>();
        }

        public Metro3DFacility RefreshSelection(Mouse mouse)
        {
            RebuildOptions();
            if (options.Count == 0)
            {
                return null;
            }

            scrollCooldown = Mathf.Max(0f, scrollCooldown - Time.unscaledDeltaTime);
            if (options.Count > 1 && mouse != null && scrollCooldown <= 0f)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    SelectRelative(scroll > 0f ? -1 : 1);
                    scrollCooldown = 0.12f;
                }
            }

            return selectedFacility;
        }

        public void SelectRelative(int direction)
        {
            if (options.Count == 0 || direction == 0)
            {
                return;
            }

            SelectedIndex = (SelectedIndex + (direction > 0 ? 1 : -1) + options.Count) % options.Count;
            InteractionOption option = options[SelectedIndex];
            selectedKind = option.Kind;
            hasSelectedKind = true;
            selectedFacility = option.Facility;
        }

        public void ClearSelection()
        {
            groups.Clear();
            options.Clear();
            SelectedIndex = 0;
            selectedFacility = null;
            hasSelectedKind = false;
            scrollCooldown = 0f;
        }

        private void RebuildOptions()
        {
            groups.Clear();
            options.Clear();

            if (player == null || player.world == null || player.view == null)
            {
                ClearSelection();
                return;
            }

            List<Metro3DFacility> facilities = player.world.facilities;
            for (int index = 0; index < facilities.Count; index++)
            {
                Metro3DFacility facility = facilities[index];
                if (!TryScore(facility, facility == selectedFacility, out float score))
                {
                    continue;
                }

                int groupIndex = FindGroup(facility.kind);
                if (groupIndex < 0)
                {
                    groups.Add(new CandidateGroup
                    {
                        Kind = facility.kind,
                        Best = facility,
                        BestScore = score,
                        CurrentScore = facility == selectedFacility ? score : float.PositiveInfinity,
                        Count = 1
                    });
                    continue;
                }

                CandidateGroup group = groups[groupIndex];
                group.Count++;
                if (score < group.BestScore)
                {
                    group.Best = facility;
                    group.BestScore = score;
                }

                if (facility == selectedFacility)
                {
                    group.CurrentScore = score;
                }

                groups[groupIndex] = group;
            }

            if (groups.Count == 0)
            {
                ClearSelection();
                return;
            }

            Metro3DFacilityKind fallbackKind = groups[0].Kind;
            float fallbackScore = groups[0].BestScore;
            for (int index = 0; index < groups.Count; index++)
            {
                CandidateGroup group = groups[index];
                if (group.CurrentScore <= group.BestScore + switchAdvantage)
                {
                    group.Best = selectedFacility;
                    group.BestScore = group.CurrentScore;
                    groups[index] = group;
                }

                if (group.BestScore < fallbackScore)
                {
                    fallbackKind = group.Kind;
                    fallbackScore = group.BestScore;
                }
            }

            groups.Sort((left, right) => ((int)left.Kind).CompareTo((int)right.Kind));
            if (!hasSelectedKind || FindGroup(selectedKind) < 0)
            {
                selectedKind = fallbackKind;
                hasSelectedKind = true;
            }

            int optionLimit = Mathf.Min(maximumOptions, groups.Count);
            for (int index = 0; index < optionLimit; index++)
            {
                CandidateGroup group = groups[index];
                options.Add(new InteractionOption(
                    group.Kind,
                    LabelFor(group.Best),
                    group.Best,
                    group.Count));
            }

            int selectedGroupIndex = FindGroup(selectedKind);
            if (selectedGroupIndex >= optionLimit)
            {
                CandidateGroup group = groups[selectedGroupIndex];
                options[optionLimit - 1] = new InteractionOption(
                    group.Kind,
                    LabelFor(group.Best),
                    group.Best,
                    group.Count);
                options.Sort((left, right) => ((int)left.Kind).CompareTo((int)right.Kind));
            }

            SelectedIndex = 0;
            for (int index = 0; index < options.Count; index++)
            {
                if (options[index].Kind != selectedKind)
                {
                    continue;
                }

                SelectedIndex = index;
                break;
            }

            InteractionOption selected = options[SelectedIndex];
            selectedKind = selected.Kind;
            selectedFacility = selected.Facility;
        }

        private int FindGroup(Metro3DFacilityKind kind)
        {
            for (int index = 0; index < groups.Count; index++)
            {
                if (groups[index].Kind == kind)
                {
                    return index;
                }
            }

            return -1;
        }

        private bool TryScore(Metro3DFacility facility, bool current, out float score)
        {
            score = 0f;
            if (facility == null || !facility.gameObject.activeInHierarchy ||
                (!facility.Available && facility.Occupant != player))
            {
                return false;
            }

            Vector3 interactionPoint = InteractionPoint(facility);
            Vector3 playerPoint = player.transform.position + Vector3.up * 0.65f;
            float distance = Vector3.Distance(playerPoint, interactionPoint);
            float allowedRange = current ? Mathf.Max(acquireRange, releaseRange) : acquireRange;
            if (distance > allowedRange)
            {
                return false;
            }

            Vector3 viewOffset = interactionPoint - player.view.transform.position;
            if (viewOffset.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            bool gate = IsGate(facility);
            float planarDistance = Vector3.Distance(
                new Vector3(player.transform.position.x, 0f, player.transform.position.z),
                new Vector3(interactionPoint.x, 0f, interactionPoint.z));
            bool closeGate = gate && planarDistance <= 1.15f;
            bool closeCurrent = current && planarDistance <= 0.8f;
            bool stableCloseRange = closeGate || closeCurrent;
            float viewDot = Vector3.Dot(player.view.transform.forward, viewOffset.normalized);
            float requiredDot = stableCloseRange
                ? -0.2f
                : current
                    ? minimumViewDot - 0.15f
                    : minimumViewDot;
            if (viewDot < requiredDot || (!stableCloseRange && IsOccluded(facility, viewOffset)))
            {
                return false;
            }

            score = distance + (1f - viewDot) * viewPenalty;
            return true;
        }

        private bool IsOccluded(Metro3DFacility facility, Vector3 viewOffset)
        {
            float distance = viewOffset.magnitude;
            if (!Physics.Raycast(
                    player.view.transform.position,
                    viewOffset / distance,
                    out RaycastHit hit,
                    distance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (hit.collider == facility.blocker || hit.transform == facility.transform ||
                hit.transform.IsChildOf(facility.transform))
            {
                return false;
            }

            return hit.distance < distance - 0.2f;
        }

        private static Vector3 InteractionPoint(Metro3DFacility facility)
        {
            if (IsGate(facility))
            {
                // Gate colliders are centred near waist height. Raising the query point keeps
                // the prompt stable when the camera reaches the blocker at close range.
                return facility.transform.position + Vector3.up * 0.45f;
            }

            if (facility.approach != null)
            {
                return facility.approach.position + Vector3.up * 0.75f;
            }

            return facility.transform.position + Vector3.up * 0.35f;
        }

        private static bool IsGate(Metro3DFacility facility)
        {
            return facility.kind == Metro3DFacilityKind.FareGate ||
                   facility.kind == Metro3DFacilityKind.ExitGate;
        }

        private static string LabelFor(Metro3DFacility facility)
        {
            switch (facility.kind)
            {
                case Metro3DFacilityKind.Seat:
                    return "앉기";
                case Metro3DFacilityKind.Lean:
                    return "기대기";
                case Metro3DFacilityKind.Hold:
                    return "손잡이 잡기";
                case Metro3DFacilityKind.FareGate:
                    return "개찰구 통과";
                case Metro3DFacilityKind.ExitGate:
                    return "목적지 출구 이용";
                case Metro3DFacilityKind.ElevatorCall:
                    return "엘리베이터 호출";
                case Metro3DFacilityKind.ElevatorRide:
                    return "엘리베이터 층 이동";
                default:
                    return string.IsNullOrWhiteSpace(facility.caption) ? "상호작용" : facility.caption;
            }
        }
    }
}

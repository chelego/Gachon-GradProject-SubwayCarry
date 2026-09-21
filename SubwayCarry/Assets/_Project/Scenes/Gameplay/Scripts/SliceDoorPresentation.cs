using System.Collections.Generic;
using SubwayCarry.Core.Contracts;
using UnityEngine;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    public sealed class SliceDoorPresentation : MonoBehaviour
    {
        struct Leaf { public Transform transform; public Vector3 closed, slide; }
        readonly List<Leaf> leaves = new List<Leaf>(24);
        SliceGameController world;
        SliceJourneyController journey;
        float opening;
        public void Initialize(SliceGameController source, SliceJourneyController flow)
        {
            world = source; journey = flow;
            journey.TrainDoorStateChanged += DoorChanged; Rebind();
        }
        public void Rebind()
        {
            // Restore the previous map before caching another one, so repeated trips never accumulate offsets.
            foreach (var leaf in leaves) if (leaf.transform != null) leaf.transform.localPosition = leaf.closed;
            leaves.Clear(); opening = 0;
            foreach (var renderer in world.CurrentMap.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (renderer.sprite == null) continue;
                string name = renderer.sprite.name.ToLowerInvariant();
                if (!name.Contains("door_leaf")) continue;
                if (world.CurrentMapIndex == 1 && !renderer.name.Contains("ServiceSide")) continue;
                Vector3 tangent = new Vector3(1, 0.5f, 0).normalized;
                float direction = name.Contains("left") ? -1 : 1;
                Vector3 localSlide = renderer.transform.parent.InverseTransformVector(tangent * direction * 0.72f);
                leaves.Add(new Leaf { transform = renderer.transform, closed = renderer.transform.localPosition, slide = localSlide });
            }
        }
        void DoorChanged(TrainDoorSnapshot state)
        {
            if (state.State != TrainDoorState.Opening || world.CurrentMapIndex != 1 || world.Posture.CurrentState != CarryPosture.Leaning) return;
            foreach (var portal in world.CurrentMap.portals)
            {
                if (portal.side != DoorOpeningSide.Right || (portal.position - world.PlayerPosition).sqrMagnitude > 1.5f) continue;
                world.Posture.FallFromBalance();
                world.Package.ApplyImpact(new PackageImpactData(gameObject, world.PlayerPosition, 2, 0));
                break;
            }
        }
        void Update()
        {
            if (world == null) return;
            bool open = journey.CurrentDoorState.State == TrainDoorState.Opening || journey.CurrentDoorState.State == TrainDoorState.Open;
            float next = Mathf.MoveTowards(opening, open ? 1 : 0, Time.deltaTime / Mathf.Max(0.1f, journey.Catalog.doorAnimationSeconds));
            if (Mathf.Approximately(next, opening)) return;
            opening = next;
            foreach (var leaf in leaves) if (leaf.transform != null) leaf.transform.localPosition = leaf.closed + leaf.slide * opening;
        }
        void OnDestroy()
        {
            if (journey != null) journey.TrainDoorStateChanged -= DoorChanged;
            foreach (var leaf in leaves) if (leaf.transform != null) leaf.transform.localPosition = leaf.closed;
        }
    }
}

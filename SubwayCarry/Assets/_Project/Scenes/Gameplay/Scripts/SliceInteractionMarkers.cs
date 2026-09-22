using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    public sealed class SliceInteractionMarkers : MonoBehaviour
    {
        readonly List<Transform> markers = new List<Transform>();
        readonly List<Renderer> doorMarkers = new List<Renderer>();
        Material material;
        Sprite signPixel;
        SliceJourneyController flow;
        SliceMap map;
        public void Initialize(SliceMap source, SliceJourneyController journey)
        {
            map = source; flow = journey;
            material = new Material(Shader.Find("Sprites/Default"));
            foreach (var portal in map.portals)
            {
                if (map.placeKind == SubwayCarry.AI.V2.PassengerAiV2PlaceKind.TrainInterior && portal.side != SubwayCarry.Core.Contracts.DoorOpeningSide.Right) continue;
                var line = Ring(portal.position, portal.stationConnection ? new Color32(108, 237, 196, 255) : new Color32(244, 201, 85, 255));
                if (!portal.stationConnection) doorMarkers.Add(line);
            }
            if (map.hasFareGate) Ring(map.fareGate, new Color32(108, 237, 196, 255));
            var layout = map.GetComponent<SliceStationLayout>();
            if (layout != null)
            {
                signPixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f, Texture2D.whiteTexture.width);
                for (int i = 0; i < layout.signs.Count; i++)
                {
                    var board = new GameObject("Mounted wayfinding board"); board.transform.SetParent(transform, false); board.transform.position = layout.signs[i];
                    board.transform.localScale = new Vector3(Mathf.Max(1.8f, layout.signLabels[i].Length * .18f), .48f, 1);
                    var back = board.AddComponent<SpriteRenderer>(); back.sprite = signPixel; back.color = new Color32(25, 40, 54, 255); back.sortingOrder = 20009;
                    var go = new GameObject("Station sign " + i); go.transform.SetParent(transform, false); go.transform.position = layout.signs[i];
                    var text = go.AddComponent<TextMesh>(); text.font = journey.Catalog.uiFont; text.fontSize = 48; text.characterSize = .085f;
                    text.text = layout.signLabels[i]; text.anchor = TextAnchor.MiddleCenter; text.color = new Color32(237, 244, 234, 255);
                    var renderer = text.GetComponent<MeshRenderer>(); renderer.sharedMaterial = text.font.material; renderer.sortingOrder = 20010;
                }
            }
        }
        LineRenderer Ring(Vector2 point, Color color)
        {
            var go = new GameObject("Interaction landing"); go.transform.SetParent(transform, false); go.transform.position = point;
            go.transform.localScale = Vector3.one / Mathf.Max(.01f, transform.lossyScale.x);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = material; line.useWorldSpace = false; line.loop = true;
            line.positionCount = 24; line.widthMultiplier = .045f; line.startColor = line.endColor = color;
            line.sortingOrder = map.GroundOrder(point) + 2;
            for (int i = 0; i < 24; i++) { float a = i * Mathf.PI * 2 / 24; line.SetPosition(i, new Vector3(Mathf.Cos(a) * .55f, Mathf.Sin(a) * .27f)); }
            markers.Add(go.transform); return line;
        }
        void Update()
        {
            bool active = flow != null && !flow.InputBlocked && !flow.World.IsChangingMap;
            foreach (var marker in markers) if (marker.gameObject.activeSelf != active) marker.gameObject.SetActive(active);
            foreach (var marker in doorMarkers) marker.enabled = active && flow.World.DoorsOpen;
        }
        void OnDestroy() { if (material != null) Destroy(material); if (signPixel != null) Destroy(signPixel); }
    }
}

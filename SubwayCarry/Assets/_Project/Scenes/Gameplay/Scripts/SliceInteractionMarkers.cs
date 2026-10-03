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
            // The screen-space E key is the only interaction marker. Keep physical station signs.
            var layout = map.GetComponent<SliceStationLayout>();
            signPixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f, Texture2D.whiteTexture.width);
            if (layout != null)
            {
                for (int i = 0; i < layout.signs.Count; i++)
                    Sign(layout.signs[i], layout.signLabels[i], journey);
            }
            else if (map.travelDirection != SliceTravelDirection.Unspecified)
                Sign(map.entry + Vector2.up * 2.5f, map.travelDirection == SliceTravelDirection.Jeongja ? "정자 · 수원 방면" : "왕십리 · 청량리 방면", journey);
        }
        void Sign(Vector2 point, string label, SliceJourneyController journey)
        {
            var root = new GameObject("Mounted wayfinding board"); root.transform.SetParent(transform, false); root.transform.position = point;
            root.transform.localScale = Vector3.one / Mathf.Max(.01f, transform.lossyScale.x);
            var board = new GameObject("Sign panel"); board.transform.SetParent(root.transform, false);
            var back = board.AddComponent<SpriteRenderer>(); back.sprite = signPixel; back.color = new Color32(25,40,54,255); back.sortingOrder = 20009;
            var go = new GameObject("Direction label"); go.transform.SetParent(root.transform, false);
            var text = go.AddComponent<TextMesh>(); text.font = journey.Catalog.uiFont; text.fontSize = 64; text.characterSize = .055f;
            text.text = label; text.anchor = TextAnchor.MiddleCenter; text.color = new Color32(237,244,234,255);
            var renderer = text.GetComponent<MeshRenderer>(); renderer.sharedMaterial = text.font.material; renderer.sortingOrder = 20010;
            journey.Catalog.uiFont.RequestCharactersInTexture(label,64);
            // Size from the actual font, not number of Korean characters times an unrelated constant.
            float width = 0;
            foreach (char c in label) if (text.font.GetCharacterInfo(c,out var info,64)) width += info.advance * .055f / 10f;
            board.transform.localScale = new Vector3(Mathf.Max(2,width+.4f),.52f,1);
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
            foreach (var marker in markers)
            {
                bool nearby = active && ((Vector2)marker.position - flow.World.PlayerPosition).sqrMagnitude < 6.25f;
                if (marker.gameObject.activeSelf != nearby) marker.gameObject.SetActive(nearby);
            }
            foreach (var marker in doorMarkers) marker.enabled = active && flow.World.DoorsOpen;
        }
        void OnDestroy() { if (material != null) Destroy(material); if (signPixel != null) Destroy(signPixel); }
    }
}

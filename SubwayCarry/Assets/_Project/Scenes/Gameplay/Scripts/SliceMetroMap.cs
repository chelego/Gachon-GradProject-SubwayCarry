using UnityEngine;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // Display the supplied map unchanged. Never rebuild real railway geometry from guessed coordinates.
    // Clipping happens in texture UVs, not rotated GUI matrices (which leaked lines outside the old phone).
    public sealed class SliceMetroMap
    {
        Texture2D image;
        Vector2 pan;
        float zoom, minimumZoom;
        int dragControl;
        GUIStyle toolbar;
        TextAsset selectedPng;
        Texture2D selectedRoute;
        Rect selectedBounds;
        void SelectRoute(SliceRouteHighlight route)
        {
            TextAsset next = route != null ? route.png : null;
            if (next == selectedPng) return;
            ReleaseRoute(); selectedPng = next;
            if (next == null) return;
            selectedBounds = route.normalizedRect;
            // Only the selected, tightly cropped PNG is decoded. Never decode in every GUI event.
            selectedRoute = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Selected delivery route", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            if (!ImageConversion.LoadImage(selectedRoute, next.bytes, true)) ReleaseRoute();
        }
        public void ReleaseRoute()
        {
            if (selectedRoute != null) Object.Destroy(selectedRoute);
            selectedRoute = null; selectedPng = null;
        }
        public static Vector2 ZoomPan(Vector2 oldPan, Vector2 pointer, float before, float after)
            => new Vector2(SlicePresentationRules.ZoomPan(oldPan.x, pointer.x, before, after),
                SlicePresentationRules.ZoomPan(oldPan.y, pointer.y, before, after));
        void Fit(Rect area, bool diagramOnly)
        {
            Rect focus = diagramOnly ? new Rect(.035f, .245f, .93f, .72f) : new Rect(0, 0, 1, 1);
            zoom = Mathf.Min(area.width / (image.width * focus.width), area.height / (image.height * focus.height));
            minimumZoom = Mathf.Min(area.width / image.width, area.height / image.height) * .8f;
            pan = area.size * .5f - Vector2.Scale(focus.center, new Vector2(image.width, image.height)) * zoom;
        }
        public void Draw(Rect viewport, Texture2D source, SliceRouteHighlight route, GUIStyle style)
        {
            Color previous = GUI.color; GUI.color = Color.white;
            GUI.DrawTexture(viewport, Texture2D.whiteTexture);
            if (source == null) { GUI.color = previous; return; }
            Rect area = new Rect(viewport.x, viewport.y + 34, viewport.width, viewport.height - 34);
            if (toolbar == null) toolbar = new GUIStyle(style) { normal = { textColor = new Color32(35, 52, 63, 255) }, alignment = TextAnchor.MiddleCenter };
            if (image != source) { image = source; Fit(area, true); }
            SelectRoute(route);
            int id = GUIUtility.GetControlID(0x52AC7, FocusType.Passive, area);
            Event e = Event.current;
            if (!GUI.enabled && dragControl != 0) { if (GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0; dragControl = 0; }
            if (GUI.enabled && area.Contains(e.mousePosition))
            {
                if (e.type == EventType.ScrollWheel)
                {
                    float next = Mathf.Clamp(zoom * Mathf.Exp(-e.delta.y * .09f), minimumZoom, 3f);
                    pan = ZoomPan(pan, e.mousePosition - area.position, zoom, next); zoom = next; e.Use();
                }
                if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 2))
                { dragControl = id; GUIUtility.hotControl = id; e.Use(); }
            }
            if (dragControl == id && GUIUtility.hotControl == id)
            {
                if (e.type == EventType.MouseDrag) { pan += e.delta; e.Use(); }
                if (e.type == EventType.MouseUp || e.type == EventType.Ignore)
                { GUIUtility.hotControl = 0; dragControl = 0; if (e.type == EventType.MouseUp) e.Use(); }
            }
            pan.x = Mathf.Clamp(pan.x, -image.width * zoom + 32, area.width - 32);
            pan.y = Mathf.Clamp(pan.y, -image.height * zoom + 32, area.height - 32);
            Rect destination = new Rect(area.position + pan, new Vector2(image.width, image.height) * zoom);
            GUI.color = new Color(1, 1, 1, selectedRoute != null ? .22f : 1);
            DrawClipped(area, destination, image);
            GUI.color = Color.white;
            if (selectedRoute != null)
            {
                Rect routeRect = new Rect(destination.x + selectedBounds.x * destination.width,
                    destination.y + selectedBounds.y * destination.height,
                    selectedBounds.width * destination.width, selectedBounds.height * destination.height);
                DrawClipped(area, routeRect, selectedRoute);
            }
            if (GUI.Button(new Rect(viewport.x + 4, viewport.y + 3, 84, 27), "전체 보기", toolbar)) Fit(area, true);
            if (GUI.Button(new Rect(viewport.x + 94, viewport.y + 3, 72, 27), "범례 포함", toolbar)) Fit(area, false);
            GUI.Label(new Rect(viewport.x + 178, viewport.y + 3, 260, 27), "휠 확대 · 드래그 이동", toolbar);
            GUI.color = previous;
        }
        static void DrawClipped(Rect clip, Rect imageRect, Texture2D texture)
        {
            Rect draw = Rect.MinMaxRect(Mathf.Max(clip.xMin, imageRect.xMin), Mathf.Max(clip.yMin, imageRect.yMin),
                Mathf.Min(clip.xMax, imageRect.xMax), Mathf.Min(clip.yMax, imageRect.yMax));
            if (draw.width <= 0 || draw.height <= 0) return;
            Rect uv = new Rect((draw.x - imageRect.x) / imageRect.width,
                1 - (draw.yMax - imageRect.y) / imageRect.height, draw.width / imageRect.width, draw.height / imageRect.height);
            GUI.DrawTextureWithTexCoords(draw, texture, uv, true);
        }
    }
}

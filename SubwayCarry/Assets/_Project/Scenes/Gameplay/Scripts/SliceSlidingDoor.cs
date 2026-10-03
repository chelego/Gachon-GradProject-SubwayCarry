using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // Two opaque train leaves with separate windows. Leaves retract INSIDE their jambs, not over the next door.
    public sealed class SliceSlidingDoor : MonoBehaviour
    {
        struct Panel { public float x0, x1, y0, y1; public int side; public Color color; }
        readonly List<Panel> panels = new List<Panel>();
        readonly SliceTransitGeometry frame = new SliceTransitGeometry();
        Mesh mesh;
        Vector3[] vertices;
        Color[] colors;
        Vector2 ground, tangent;
        float halfWidth, height, lastOpening = -1;
        Matrix4x4 assemblyToLocal;
        public bool serviceSide = true;
        public void Build(Vector2 foot, Vector2 axis, float width, float doorHeight, int order)
        {
            ground = foot; tangent = axis.normalized; halfWidth = width * .5f; height = doorHeight;
            assemblyToLocal = transform.worldToLocalMatrix;
            var rim = new Color32(53, 65, 73, 255); var silver = new Color32(181, 195, 201, 255);
            frame.Panel(foot - tangent * (halfWidth + .12f), foot - tangent * halfWidth, height + .12f, rim, order + 3);
            frame.Panel(foot + tangent * halfWidth, foot + tangent * (halfWidth + .12f), height + .12f, rim, order + 3);
            frame.Panel(foot - tangent * (halfWidth + .12f) + Vector2.up * height,
                foot + tangent * (halfWidth + .12f) + Vector2.up * height, .2f, silver, order + 3);
            frame.Rail(foot - tangent * halfWidth, foot + tangent * halfWidth, .07f, rim, order + 3);
            frame.Flush(transform, "Door jamb and recessed pocket");
            for (int side = -1; side <= 1; side += 2)
            {
                float a = side < 0 ? -halfWidth : 0, b = a + halfWidth;
                Add(a, b, 0, height, side, rim);
                Add(a + .035f, b - .035f, .045f, height - .045f, side, silver);
                Add(a + .09f, b - .09f, .82f, height - .3f, side, new Color32(43, 62, 73, 255));
                Add(a + .135f, b - .135f, .87f, height - .35f, side, new Color32(65, 95, 106, 255));
                Add(a + .14f, b - .14f, height - .47f, height - .41f, side, new Color32(115, 145, 154, 255));
                Add(a + .09f, b - .09f, .22f, .25f, side, new Color32(116, 135, 144, 255));
                Add(side < 0 ? b - .065f : a, side < 0 ? b : a + .065f, .01f, height - .03f, side, rim);
            }
            vertices = new Vector3[panels.Count * 4]; colors = new Color[vertices.Length]; var indices = new int[panels.Count * 6];
            for (int i = 0; i < panels.Count; i++)
            { int at = i * 4, t = i * 6; indices[t] = at; indices[t+1] = at+1; indices[t+2] = at+2; indices[t+3] = at; indices[t+4] = at+2; indices[t+5] = at+3; }
            mesh = new Mesh { name = "Clipped sliding door leaves" }; mesh.MarkDynamic(); mesh.vertices = vertices; mesh.triangles = indices; mesh.uv = new Vector2[vertices.Length];
            var leaf = new GameObject(mesh.name); leaf.transform.SetParent(transform, false); leaf.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = leaf.AddComponent<MeshRenderer>(); renderer.sharedMaterial = frame.Material; renderer.sortingOrder = order + 2;
            SetOpening(0);
        }
        void Add(float a, float b, float y0, float y1, int side, Color color)
            => panels.Add(new Panel { x0 = a, x1 = b, y0 = y0, y1 = y1, side = side, color = color });
        public void SetOpening(float value)
        {
            value = Mathf.Clamp01(value); if (mesh == null || Mathf.Approximately(lastOpening, value)) return;
            lastOpening = value;
            for (int i = 0; i < panels.Count; i++)
            {
                var p = panels[i]; float slide = value * (halfWidth + .02f) * p.side;
                float a = Mathf.Clamp(p.x0 + slide, -halfWidth, halfWidth), b = Mathf.Clamp(p.x1 + slide, -halfWidth, halfWidth);
                Vector2 left = ground + tangent * a, right = ground + tangent * b;
                int at = i * 4;
                vertices[at] = assemblyToLocal.MultiplyPoint3x4(left + Vector2.up * p.y0);
                vertices[at+1] = assemblyToLocal.MultiplyPoint3x4(right + Vector2.up * p.y0);
                vertices[at+2] = assemblyToLocal.MultiplyPoint3x4(right + Vector2.up * p.y1);
                vertices[at+3] = assemblyToLocal.MultiplyPoint3x4(left + Vector2.up * p.y1);
                Color color = p.color; if (b - a < .001f) color.a = 0;
                for (int k = 0; k < 4; k++) colors[at+k] = color;
            }
            mesh.vertices = vertices; mesh.colors = colors; mesh.RecalculateBounds();
        }
        void OnDestroy() { frame.Dispose(); if (mesh != null) Destroy(mesh); }
    }
}

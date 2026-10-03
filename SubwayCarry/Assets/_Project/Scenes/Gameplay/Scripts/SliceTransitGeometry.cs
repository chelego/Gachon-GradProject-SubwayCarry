using System.Collections.Generic;
using UnityEngine;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    // World-space, 2:1 projected surfaces. One mesh per depth/material, not a renderer per tread or rivet.
    public sealed class SliceTransitGeometry : System.IDisposable
    {
        sealed class Batch
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Color> colors = new List<Color>();
            public readonly List<int> indices = new List<int>();
        }
        readonly Dictionary<int, Batch> batches = new Dictionary<int, Batch>();
        readonly List<Mesh> meshes = new List<Mesh>();
        Material material;
        public Material Material => material != null ? material : material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color, int order)
        {
            if (!batches.TryGetValue(order, out var batch)) { batch = new Batch(); batches.Add(order, batch); }
            int at = batch.vertices.Count;
            batch.vertices.Add(a); batch.vertices.Add(b); batch.vertices.Add(c); batch.vertices.Add(d);
            for (int i = 0; i < 4; i++) batch.colors.Add(color);
            batch.indices.Add(at); batch.indices.Add(at + 1); batch.indices.Add(at + 2);
            batch.indices.Add(at); batch.indices.Add(at + 2); batch.indices.Add(at + 3);
        }
        public void Panel(Vector2 a, Vector2 b, float height, Color color, int order)
            => Quad(a, b, b + Vector2.up * height, a + Vector2.up * height, color, order);
        public void Rail(Vector2 a, Vector2 b, float width, Color color, int order)
        {
            Vector2 delta = (b - a).normalized, n = new Vector2(-delta.y, delta.x) * width * .5f;
            Quad(a - n, b - n, b + n, a + n, color, order);
        }
        public void Flush(Transform parent, string label)
        {
            foreach (var pair in batches)
            {
                var mesh = new Mesh { name = label }; meshes.Add(mesh);
                var b = pair.Value;
                for (int i = 0; i < b.vertices.Count; i++) b.vertices[i] = parent.InverseTransformPoint(b.vertices[i]);
                mesh.SetVertices(b.vertices); mesh.SetColors(b.colors); mesh.SetTriangles(b.indices, 0);
                mesh.uv = new Vector2[b.vertices.Count]; mesh.RecalculateBounds();
                var go = new GameObject(label + " " + pair.Key); go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = Material; renderer.sortingOrder = pair.Key;
            }
            batches.Clear();
        }
        public void Dispose()
        {
            foreach (var mesh in meshes) if (mesh != null) Object.Destroy(mesh);
            if (material != null) Object.Destroy(material);
        }
    }
}

using UnityEngine;

namespace SubwayCarry.Prototype.ArtMapSlice
{
    public sealed class SliceStationGate : MonoBehaviour
    {
        SliceMap map;
        SliceJourneyController journey;
        Mesh flap;
        Material material;
        readonly Vector3[] vertices = new Vector3[4];
        Vector2 hinge, closedTip;
        float extension = 1;
        PolygonCollider2D passage;
        public void Initialize(SliceMap source)
        {
            map = source;
            hinge = SliceTrainLayout.Project(6,2.22f); closedTip = SliceTrainLayout.Project(6,3.78f);
            var flapObject = new GameObject("Retracting vertical fare flap"); flapObject.transform.SetParent(transform,false);
            flap = new Mesh { name = "Fare flap vertical plane" }; flap.MarkDynamic(); flap.vertices = vertices;
            flap.triangles = new[] {0,1,2,0,2,3}; flap.uv = new Vector2[4];
            Color color = new Color32(222,157,66,255); flap.colors = new[] {color,color,color,color};
            flapObject.AddComponent<MeshFilter>().sharedMesh=flap;
            var renderer=flapObject.AddComponent<MeshRenderer>();
            material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            renderer.sharedMaterial=material; renderer.sortingOrder=map.GroundOrder(SliceTrainLayout.Project(6,3))+1;
            UpdateFlap();
            var go = new GameObject("Closed gate foot boundary"); go.transform.SetParent(transform, false);
            passage = go.AddComponent<PolygonCollider2D>();
            var points = new SliceSolidFootprint { center = SliceTrainLayout.Project(6, 3), halfSize = new Vector2(1, .065f), slope = -.5f }.Corners();
            for (int i = 0; i < points.Length; i++) points[i] = go.transform.InverseTransformPoint(points[i]);
            passage.points = points;
            map.Grid.PassageAllowed = Allows;
        }
        public bool Allows(Vector2 from, Vector2 to, float radius)
        {
            float a = from.x*.5f + from.y - 6, b = to.x*.5f + to.y - 6;
            float across = to.y - to.x*.5f;
            return SlicePresentationRules.AllowsFarePassage(a,b,across,radius,journey != null && journey.GateOpen);
        }
        public void Bind(SliceJourneyController source) { journey = source; }
        void UpdateFlap()
        {
            Vector2 tip=Vector2.Lerp(hinge,closedTip,extension);
            vertices[0]=transform.InverseTransformPoint(hinge+Vector2.up*.35f);
            vertices[1]=transform.InverseTransformPoint(tip+Vector2.up*.35f);
            vertices[2]=transform.InverseTransformPoint(tip+Vector2.up*.95f);
            vertices[3]=transform.InverseTransformPoint(hinge+Vector2.up*.95f);
            flap.vertices=vertices; flap.RecalculateBounds();
        }
        void Update()
        {
            if (journey == null || flap == null) return;
            bool open = journey.GateOpen;
            passage.enabled = !open;
            float next = Mathf.MoveTowards(extension,open ? .01f : 1,Time.deltaTime*4);
            if (!Mathf.Approximately(next,extension)) { extension=next; UpdateFlap(); }
        }
        void OnDestroy() { if (flap != null) Destroy(flap); if (material != null) Destroy(material); }
    }
}

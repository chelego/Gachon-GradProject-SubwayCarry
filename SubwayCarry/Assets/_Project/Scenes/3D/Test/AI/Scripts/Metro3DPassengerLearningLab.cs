using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace SubwayCarry.Gameplay3D
{
    public sealed class Metro3DPassengerLearningLab : MonoBehaviour
    {
        [SerializeField] TextAsset evaluation;
        [SerializeField] Metro3DSettings settings;
        [SerializeField] Camera view;

        [Serializable] sealed class Trace { public float width, neck; public bool[] present; public float[] points; }
        [Serializable] sealed class Metrics
        {
            public int episodes;
            public Trace[] replay;
        }
        [Serializable] sealed class Report
        {
            public string algorithm, runId;
            public int agentsPerArena, frames;
            public float dt, goal;
            public Metrics rule, imitation, ppo;
        }
        sealed class Lane
        {
            public Transform root, start, finish;
            public Mesh floor;
            public Transform[] people;
            public Animator[] animators;
            public LineRenderer[] trails;
            public Trace trace;
            public int trailFrame = -1, arrived;
            public readonly Vector3[] floorVertices = new Vector3[ArenaSections * 4];
        }

        static readonly string[] Cases = {"교행", "합류", "추종", "병목"};
        const int ArenaSections = 53, TrailSamples = 6;
        const float ArenaStart = -4.65f, ArenaEnd = 7.85f, Shoulder = .24f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Speed = Animator.StringToHash("Speed"), Gait = Animator.StringToHash("Gait");
        static readonly Color CoolFlow = new(.45f, .67f, .89f), WarmFlow = new(.92f, .65f, .43f);
        readonly Lane[] lanes = new Lane[2];
        readonly Material[] materials = new Material[5];
        Report report;
        MaterialPropertyBlock tint;
        float clock;
        int scenario, viewWidth, viewHeight;
        bool paused;
        bool useImitation = true, showTrails = true;
        public bool Ready { get; private set; }
        public float ReplayTime => Mathf.Min(clock, Duration);
        public float ReplayDuration => report == null ? 0 : Duration;
        public bool ReplayPaused { get => paused; set => paused = value; }
        public bool TrailsVisible { get => showTrails; set => showTrails = value; }
        public bool ComparePreviousPpo => useImitation;
        public int Scenario => scenario;
        public int BaselineArrivals => Ready ? lanes[0].arrived : 0;
        public int TrainedArrivals => Ready ? lanes[1].arrived : 0;
        public string ReplayRunId => report?.runId;

        public void SetComparison(bool previousPpo)
        {
            if (useImitation == previousPpo) return;
            useImitation = previousPpo;
            SelectCase(scenario);
        }

        public void SeekReplay(float seconds)
        {
            paused = true;
            Seek(seconds);
        }

        public void RestartReplay() => Restart();

        public bool LoadReplay(string json, string runId)
        {
            var next = JsonUtility.FromJson<Report>(json);
            if (!ValidReport(next) || next.runId != runId || !Mathf.Approximately(next.goal, report.goal)) return false;
            report = next;
            SelectCase(scenario);
            return true;
        }

        public void LoadDefaultReplay()
        {
            report = JsonUtility.FromJson<Report>(evaluation.text);
            SelectCase(scenario);
        }

        public void Configure(TextAsset data, Metro3DSettings projectSettings, Camera cameraView)
        {
            evaluation = data; settings = projectSettings; view = cameraView;
        }

        static bool ValidReport(Report value)
        {
            if (value == null || value.algorithm != "GroupPPO" || value.agentsPerArena != 6 ||
                value.frames < 2 || value.frames > 600 || value.dt <= 0 || value.goal <= 0) return false;
            return ValidMetrics(value.rule, value) && ValidMetrics(value.imitation, value) && ValidMetrics(value.ppo, value);
        }

        static bool ValidMetrics(Metrics data, Report value)
        {
            if (data == null || data.episodes < 4 || data.replay?.Length != Cases.Length) return false;
            foreach (var trace in data.replay)
                if (trace == null || trace.points?.Length != value.frames * value.agentsPerArena * 2 ||
                    trace.present?.Length != value.agentsPerArena - 1 || trace.width <= 0) return false;
            return true;
        }

        void Awake()
        {
            if (evaluation == null || settings == null || view == null) throw new InvalidOperationException("Learning lab references missing");
            report = JsonUtility.FromJson<Report>(evaluation.text);
            if (!ValidReport(report)) throw new InvalidOperationException("Invalid learning report");
            var focus = new Vector3(0, .15f, (ArenaStart + ArenaEnd) * .5f);
            var offset = new Vector3(-6.2f, 13.4f, 0);
            view.transform.SetPositionAndRotation(focus + offset, Quaternion.LookRotation(-offset, Vector3.left));
            view.backgroundColor = new Color(.063f, .077f, .093f);
            UpdateViewport();
            var model = settings.passengerPrefab.transform.Find("HumanModel");
            if (model == null) throw new InvalidOperationException("Passenger visual prefab missing");
            tint = new MaterialPropertyBlock();
            materials[0] = Surface(new Color(.19f, .235f, .275f));
            materials[1] = Surface(new Color(.33f, .43f, .50f));
            materials[2] = Surface(new Color(.31f, .41f, .48f), true);
            materials[3] = Surface(Color.Lerp(materials[0].GetColor(BaseColor), CoolFlow, .58f), true);
            materials[4] = Surface(Color.Lerp(materials[0].GetColor(BaseColor), WarmFlow, .58f), true);
            lanes[0] = BuildLane(-2.62f, "Baseline replay", model);
            lanes[1] = BuildLane(2.62f, "Group PPO replay", model);
            SelectCase(3);
            Ready = true;
        }

        Material Surface(Color color, bool unlit = false)
        {
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var material = new Material(shader);
            material.SetColor(BaseColor, color);
            if (!unlit) material.SetFloat("_Smoothness", .14f);
            return material;
        }

        static Transform Block(Transform parent, string label, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = label;
            part.transform.SetParent(parent, false);
            part.transform.SetLocalPositionAndRotation(position, Quaternion.identity);
            part.transform.localScale = size;
            part.GetComponent<BoxCollider>().enabled = false;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part.transform;
        }

        void UpdateViewport()
        {
            viewWidth = Screen.width;
            viewHeight = Screen.height;
            if (viewWidth <= 0 || viewHeight <= 0) return;
            view.rect = new Rect(0, 0, 1, 1);
            view.ResetAspect();
            view.orthographicSize = Mathf.Max(4.88f, 6.7f * viewHeight / viewWidth);
        }

        Lane BuildLane(float x, string label, Transform model)
        {
            int count = report.agentsPerArena;
            var lane = new Lane
            {
                root = new GameObject(label).transform,
                people = new Transform[count],
                animators = new Animator[count],
                trails = new LineRenderer[count],
                floor = new Mesh { name = "Evaluation corridor", subMeshCount = 2 }
            };
            lane.root.SetParent(transform, false);
            lane.root.localPosition = Vector3.right * x;
            var floor = new GameObject("Training boundaries", typeof(MeshFilter), typeof(MeshRenderer));
            floor.transform.SetParent(lane.root, false);
            lane.floor.MarkDynamic();
            floor.GetComponent<MeshFilter>().sharedMesh = lane.floor;
            floor.GetComponent<MeshRenderer>().sharedMaterials = new[] {materials[0], materials[1]};
            lane.start = Block(lane.root, "Entry line", new Vector3(0, .002f, 0), Vector3.one, materials[2]);
            lane.finish = Block(lane.root, "Exit line", new Vector3(0, .002f, report.goal), Vector3.one, materials[2]);

            for (int i = 0; i < count; i++)
            {
                var person = new GameObject("Passenger " + (i + 1)).transform;
                person.SetParent(lane.root, false);
                var visual = Instantiate(model, person, false);
                lane.people[i] = person;
                var path = new GameObject("Recent path " + (i + 1));
                path.transform.SetParent(lane.root, false);
                var trail = path.AddComponent<LineRenderer>();
                lane.trails[i] = trail;
                trail.useWorldSpace = false;
                trail.startWidth = .008f;
                trail.endWidth = .027f;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows = false;
                lane.animators[i] = visual.GetComponentInChildren<Animator>();
                if (lane.animators[i] != null) lane.animators[i].applyRootMotion = false;
            }
            return lane;
        }

        float CorridorLimit(Trace trace, float z)
        {
            // Same lateral center limits as Group.limit in train_group_steering.py.
            return scenario switch
            {
                1 => Mathf.Min(trace.width, .76f + Mathf.Max(0, 3.45f - z) * .48f),
                3 => Mathf.Min(trace.width, trace.neck + Mathf.Max(0, Mathf.Abs(z - report.goal * .52f) - .37f) * .74f),
                _ => trace.width
            };
        }

        void SetArena(Lane lane, Trace trace)
        {
            var floor = new int[(ArenaSections - 1) * 6];
            var edges = new int[floor.Length * 2];
            for (int i = 0; i < ArenaSections; i++)
            {
                float z = Mathf.Lerp(ArenaStart, ArenaEnd, i / (float)(ArenaSections - 1));
                float width = CorridorLimit(trace, z);
                int v = i * 4;
                lane.floorVertices[v] = new Vector3(-width - Shoulder, .02f, z);
                lane.floorVertices[v + 1] = new Vector3(-width, -.018f, z);
                lane.floorVertices[v + 2] = new Vector3(width, -.018f, z);
                lane.floorVertices[v + 3] = new Vector3(width + Shoulder, .02f, z);
                if (i == ArenaSections - 1) break;
                Quad(floor, i * 6, v + 1, v + 2, v + 5, v + 6);
                Quad(edges, i * 12, v, v + 1, v + 4, v + 5);
                Quad(edges, i * 12 + 6, v + 2, v + 3, v + 6, v + 7);
            }
            lane.floor.vertices = lane.floorVertices;
            lane.floor.SetTriangles(floor, 0, false);
            lane.floor.SetTriangles(edges, 1, false);
            lane.floor.RecalculateNormals();
            lane.floor.RecalculateBounds();
            lane.start.localScale = new Vector3(CorridorLimit(trace, 0) * 2, .012f, .034f);
            lane.finish.localScale = new Vector3(CorridorLimit(trace, report.goal) * 2, .012f, .034f);
        }

        static void Quad(int[] indices, int at, int left, int right, int nextLeft, int nextRight)
        {
            indices[at] = left;
            indices[at + 1] = nextLeft;
            indices[at + 2] = right;
            indices[at + 3] = right;
            indices[at + 4] = nextLeft;
            indices[at + 5] = nextRight;
        }

        public void SelectCase(int index)
        {
            scenario = Mathf.Clamp(index, 0, Cases.Length - 1);
            SetTrace(lanes[0], (useImitation ? report.imitation : report.rule).replay[scenario]);
            SetTrace(lanes[1], report.ppo.replay[scenario]);
            Restart();
        }

        void SetTrace(Lane lane, Trace trace)
        {
            if (trace.points.Length != report.frames * report.agentsPerArena * 2 || trace.present.Length != report.agentsPerArena-1)
                throw new InvalidOperationException("Incomplete held-out replay");
            lane.trace = trace;
            SetArena(lane, trace);
            for (int i = 0; i < lane.people.Length; i++)
            {
                bool secondFlow = scenario == 2 ? i < 2 : i >= 3;
                tint.SetColor(BaseColor, secondFlow ? WarmFlow : CoolFlow);
                foreach (var renderer in lane.people[i].GetComponentsInChildren<Renderer>(true))
                    renderer.SetPropertyBlock(tint);
                lane.trails[i].sharedMaterial = materials[secondFlow ? 4 : 3];
                bool present = i == 0 || trace.present[i - 1];
                lane.people[i].gameObject.SetActive(present);
                lane.trails[i].enabled = present && showTrails;
            }
        }

        void Restart()
        {
            Seek(0);
        }

        float Duration => (report.frames - 1) * report.dt;

        void Seek(float time)
        {
            clock = Mathf.Clamp(time, 0, Duration);
            foreach (var lane in lanes) RenderTrace(lane, true);
        }

        void Update()
        {
            if (!Ready) return;
            if (Screen.width != viewWidth || Screen.height != viewHeight) UpdateViewport();
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SelectCase(0);
                if (keyboard.digit2Key.wasPressedThisFrame) SelectCase(1);
                if (keyboard.digit3Key.wasPressedThisFrame) SelectCase(2);
                if (keyboard.digit4Key.wasPressedThisFrame) SelectCase(3);
                if (keyboard.spaceKey.wasPressedThisFrame) paused = !paused;
                if (keyboard.rKey.wasPressedThisFrame) Restart();
            }
            if (!paused) clock += Time.unscaledDeltaTime;
            if (clock > Duration + 2.3f) Restart();
            else foreach (var lane in lanes) RenderTrace(lane, false);
        }

        Vector3 Point(Trace trace, int frame, int actor)
        {
            int sample = (frame * report.agentsPerArena + actor) * 2;
            return new Vector3(trace.points[sample], 0, trace.points[sample + 1]);
        }

        void RenderTrace(Lane lane, bool reset)
        {
            float frame = Mathf.Min(clock / report.dt, report.frames - 1);
            int left = (int)frame, right = Mathf.Min(left + 1, report.frames - 1);
            lane.arrived = 0;
            for (int i = 0; i < lane.people.Length; i++)
            {
                var person = lane.people[i];
                if (i > 0 && !lane.trace.present[i - 1]) continue;
                var from = Point(lane.trace, left, i);
                var to = Point(lane.trace, right, i);
                person.localPosition = Vector3.Lerp(from, to, frame - left);
                bool reverse = Point(lane.trace,0,i).z >= report.goal;
                bool arrived = reverse ? person.localPosition.z <= 0 : person.localPosition.z >= report.goal;
                if (arrived) lane.arrived++;
                bool onDeck = !arrived;
                var trail = lane.trails[i];
                if (trail.enabled != showTrails) trail.enabled = showTrails;
                if (showTrails)
                {
                    int begin = Mathf.Max(0, left - TrailSamples + 1);
                    int count = left - begin + 2;
                    if (reset || lane.trailFrame != left || trail.positionCount != count)
                    {
                        trail.positionCount = count;
                        for (int point = begin; point <= left; point++)
                            trail.SetPosition(point - begin, Point(lane.trace, point, i) + Vector3.up * .031f);
                    }
                    trail.SetPosition(count - 1, person.localPosition + Vector3.up * .031f);
                }
                if (person.gameObject.activeSelf != onDeck) person.gameObject.SetActive(onDeck);
                if (!onDeck) continue;
                var movement = to - from;
                if (movement.sqrMagnitude > .00005f)
                {
                    var facing = Quaternion.LookRotation(movement);
                    person.localRotation = reset ? facing : Quaternion.RotateTowards(person.localRotation, facing, 300 * Time.unscaledDeltaTime);
                }
                else if (reset) person.localRotation = reverse ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
                var animator = lane.animators[i];
                if (animator == null) continue;
                float speed = paused ? 0 : movement.magnitude / report.dt;
                animator.SetFloat(Speed, speed);
                animator.SetFloat(Gait, speed > .09f ? 1 : 0);
            }
            lane.trailFrame = showTrails ? left : -1;
        }

        void OnDestroy()
        {
            foreach (var lane in lanes) if (lane?.floor != null) Destroy(lane.floor);
            foreach (var material in materials) if (material != null) Destroy(material);
        }
    }
}

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

        [Serializable] sealed class Trace { public float width, neck; public bool[] present; public bool contact, success; public float[] points; }
        [Serializable] sealed class CaseMetrics { public int episodes; public float success; }
        [Serializable] sealed class Metrics
        {
            public int episodes;
            public float success, contact, wait, reward;
            public Trace[] replay;
            public CaseMetrics[] cases;
        }
        [Serializable] sealed class Checkpoint { public int updates; public float reward; }
        [Serializable] sealed class Report
        {
            public string algorithm, device;
            public int arenas, agentsPerArena, transitions, selectedTransitions, selectedUpdate, frames, testSeed;
            public float dt, goal, seconds;
            public Checkpoint[] history;
            public Metrics rule, imitation, ppo;
        }
        sealed class Lane
        {
            public Transform root, left, right;
            public Transform[] people;
            public Animator[] animators;
            public LineRenderer[] trails;
            public Trace trace;
        }

        static readonly string[] Cases = {"양방향 교행", "두 흐름 합류", "느린 무리 추종", "병목 교행"};
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Speed = Animator.StringToHash("Speed"), Gait = Animator.StringToHash("Gait");
        static readonly Color Ink = new(.043f, .068f, .108f), Panel = new(.067f, .102f, .153f);
        static readonly Color Line = new(.16f, .235f, .32f), Muted = new(.69f, .76f, .84f);
        static readonly Color Amber = new(1f, .67f, .30f), Mint = new(.33f, .90f, .76f);
        readonly Lane[] lanes = new Lane[2];
        readonly Material[] materials = new Material[6];
        Report report;
        GUIStyle title, body, small, large;
        float clock, rewardMin, rewardMax;
        int scenario;
        bool paused, useImitation;
#if UNITY_EDITOR
        [Serializable] sealed class LiveProgress
        {
            public int updates, totalUpdates, transitions;
            public float seconds;
            public bool complete;
            public Checkpoint[] history;
        }
        System.Diagnostics.Process trainer;
        LiveProgress live;
        string progressPath, trainingStatus = "";
        float nextProgressPoll;

        void StartTraining(string python, int updates, int arenas)
        {
            if (trainer != null && !trainer.HasExited) return;
            var project = System.IO.Directory.GetParent(Application.dataPath).FullName;
            var game = System.IO.Path.Combine(Application.dataPath, "_Project/Scenes/3D/Ingame/Game");
            var output = System.IO.Path.Combine(project, "Logs/CrowdBehavior/PassengerLearning/Interactive", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            System.IO.Directory.CreateDirectory(output);
            string Quote(string value) => "\"" + value + "\"";
            progressPath = System.IO.Path.Combine(output, "training-progress.json");
            trainer?.Dispose();
            trainer = null;
            live = null;
            try
            {
                trainer = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = python,
                    Arguments = "-B -u " + Quote(System.IO.Path.Combine(game, "Training~/train_group_steering.py")) +
                        " --base " + Quote(System.IO.Path.Combine(game, "Resources/PassengerSteeringPPO.json")) +
                        " --output " + Quote(output) + " --progress " + Quote(progressPath) +
                        " --updates " + updates + " --arenas " + arenas,
                    WorkingDirectory = project, UseShellExecute = false, CreateNoWindow = true
                });
                trainingStatus = "GPU 학습 시작 중";
                nextProgressPoll = 0;
            }
            catch (Exception error) {trainingStatus = "학습 실행 실패";Debug.LogWarning(error.Message);}
        }

        void LaunchTraining()
        {
            string python = Environment.GetEnvironmentVariable("SUBWAYCARRY_PYTHON");
            if (string.IsNullOrEmpty(python)) python = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Miniforge3", "envs", "projecthive-ml", "python.exe");
            if (!System.IO.File.Exists(python)) python = UnityEditor.EditorUtility.OpenFilePanel("PyTorch CUDA가 설치된 Python", "", "exe");
            if (!string.IsNullOrEmpty(python)) StartTraining(python, 224, 384);
        }

        void PollTraining()
        {
            if (trainer == null || Time.unscaledTime < nextProgressPoll) return;
            nextProgressPoll = Time.unscaledTime + .5f;
            try
            {
                if (System.IO.File.Exists(progressPath))
                {
                    using var stream = System.IO.File.Open(progressPath, System.IO.FileMode.Open, System.IO.FileAccess.Read,
                        System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
                    using var reader = new System.IO.StreamReader(stream);
                    live = JsonUtility.FromJson<LiveProgress>(reader.ReadToEnd());
                    if (live != null) trainingStatus = $"GPU 학습 {live.updates}/{live.totalUpdates} · {live.transitions:N0} 스텝";
                }
                if (!trainer.HasExited) return;
                trainingStatus = trainer.ExitCode == 0 && live?.complete == true ? "재학습 완료 · 로그에 저장" : $"학습 종료 · 코드 {trainer.ExitCode}";
                trainer.Dispose();trainer = null;
            }
            catch (System.IO.IOException) { }
        }
#endif
        public bool Ready { get; private set; }
        public float ReplayTime => clock;

        public void Configure(TextAsset data, Metro3DSettings projectSettings, Camera cameraView)
        {
            evaluation = data; settings = projectSettings; view = cameraView;
        }

        void Awake()
        {
            if (evaluation == null || settings == null || view == null) throw new InvalidOperationException("Learning lab references missing");
            report = JsonUtility.FromJson<Report>(evaluation.text);
            if (report == null || report.algorithm != "GroupPPO" || report.agentsPerArena != 6 || report.frames < 2 || report.dt <= 0 || report.history.Length < 2)
                throw new InvalidOperationException("Invalid learning report");
            view.orthographicSize = 5.15f;
            view.transform.LookAt(new Vector3(0, 1.05f, 1.8f));
            var model = settings.passengerPrefab.transform.Find("HumanModel");
            if (model == null) throw new InvalidOperationException("Passenger visual prefab missing");
            rewardMin = float.PositiveInfinity; rewardMax = float.NegativeInfinity;
            foreach (var row in report.history) {rewardMin = Mathf.Min(rewardMin, row.reward);rewardMax = Mathf.Max(rewardMax, row.reward);}
            materials[0] = Surface(new Color(.16f, .21f, .29f));
            materials[1] = Surface(new Color(.09f, .14f, .22f));
            materials[2] = Surface(new Color(.35f, .45f, .54f));
            materials[3] = Surface(Amber, true);
            materials[4] = Surface(Mint, true);
            materials[5] = Surface(new Color(.12f, .19f, .28f));
            lanes[0] = BuildLane(-6.3f, "Baseline replay", model, Amber, materials[3]);
            lanes[1] = BuildLane(6.3f, "PPO and rules replay", model, Mint, materials[4]);
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

        Lane BuildLane(float x, string label, Transform model, Color accent, Material pathMaterial)
        {
            var lane = new Lane {root = new GameObject(label).transform,
                people=new Transform[report.agentsPerArena], animators=new Animator[report.agentsPerArena],
                trails=new LineRenderer[report.agentsPerArena]};
            lane.root.SetParent(transform, false);
            lane.root.localPosition = Vector3.right * x;
            Block(lane.root, "Deck", new Vector3(0, -.12f, 1.38f), new Vector3(4.4f, .20f, 13.2f), materials[0]);
            for (int i = 0; i < 7; i++)
                Block(lane.root, "Deck joint", new Vector3(0, -.016f, i - .64f), new Vector3(4.37f, .005f, .019f), materials[2]);
            for (int i = 0; i < 9; i++)
                Block(lane.root, "Direction guide", new Vector3(0, -.014f, i * .54f), new Vector3(.023f, .005f, .23f), materials[2]);
            Block(lane.root, "Start", Vector3.zero, new Vector3(4.25f, .012f, .04f), materials[2]);
            Block(lane.root, "Finish", new Vector3(0, .002f, report.goal), new Vector3(4.25f, .012f, .075f), pathMaterial);
            lane.left = Block(lane.root, "Left boundary", new Vector3(-1.7f, .09f, 2.35f), new Vector3(.055f, .22f, 12.8f), materials[5]);
            lane.right = Block(lane.root, "Right boundary", new Vector3(1.7f, .09f, 2.35f), new Vector3(.055f, .22f, 12.8f), materials[5]);
            var tint = new MaterialPropertyBlock();
            for (int i = 0; i < lane.people.Length; i++)
            {
                var person = new GameObject(i == 0 ? "Policy passenger" : "Background passenger").transform;
                person.SetParent(lane.root, false);
                var visual = Instantiate(model, person, false);
                lane.people[i] = person;
                var path = new GameObject("Measured path " + i);
                path.transform.SetParent(lane.root, false);
                var trail = path.AddComponent<LineRenderer>();
                lane.trails[i]=trail;trail.sharedMaterial=pathMaterial;trail.useWorldSpace=false;
                trail.startWidth=trail.endWidth=.019f;
                trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;
                lane.animators[i] = visual.GetComponentInChildren<Animator>();
                if (lane.animators[i] != null) lane.animators[i].applyRootMotion = false;
                tint.SetColor(BaseColor, i < 3 ? accent : new Color(.52f, .68f, .86f));
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.SetPropertyBlock(tint);
            }
            return lane;
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
            lane.left.localPosition = new Vector3(-trace.width - .25f, .09f, 2.35f);
            lane.right.localPosition = new Vector3(trace.width + .25f, .09f, 2.35f);
            for (int i = 1; i < lane.people.Length; i++) lane.people[i].gameObject.SetActive(trace.present[i - 1]);
        }

        void Restart()
        {
            clock = 0;
            foreach (var lane in lanes) RenderTrace(lane, true);
        }

        float Duration => (report.frames - 1) * report.dt;

        void Update()
        {
            if (!Ready) return;
#if UNITY_EDITOR
            PollTraining();
#endif
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
            for (int i = 0; i < lane.people.Length; i++)
            {
                var person = lane.people[i];
                if (i > 0 && !lane.trace.present[i - 1]) continue;
                var from = Point(lane.trace, left, i);
                var to = Point(lane.trace, right, i);
                person.localPosition = Vector3.Lerp(from, to, frame - left);
                bool reverse = Point(lane.trace,0,i).z >= report.goal;
                bool arrived = reverse ? person.localPosition.z <= 0 : person.localPosition.z >= report.goal;
                bool onDeck = !arrived;
                var trail=lane.trails[i];
                if(reset||trail.positionCount!=left+1) {
                    trail.positionCount=left+1;
                    for(int point=0;point<=left;point++)trail.SetPosition(point,Point(lane.trace,point,i)+Vector3.up*.03f);
                }
                if (person.gameObject.activeSelf != onDeck) person.gameObject.SetActive(onDeck);
                if (!onDeck) continue;
                var movement = to - from;
                if (movement.sqrMagnitude > .00005f)
                {
                    var facing = Quaternion.LookRotation(movement);
                    person.localRotation = reset ? facing : Quaternion.RotateTowards(person.localRotation, facing, 300 * Time.unscaledDeltaTime);
                }
                else if (reset) person.localRotation = Quaternion.identity;
                var animator = lane.animators[i];
                if (animator == null) continue;
                float speed = paused ? 0 : movement.magnitude / report.dt;
                animator.SetFloat(Speed, speed);
                animator.SetFloat(Gait, speed > .09f ? 1 : 0);
            }

        }

        void Styles()
        {
            if (title != null) return;
            body = new GUIStyle(GUI.skin.label) {font = settings.font, fontSize = 17, alignment = TextAnchor.MiddleLeft};
            small = new GUIStyle(body) {fontSize = 14};
            large = new GUIStyle(body) {fontSize = 22, fontStyle = FontStyle.Bold};
            title = new GUIStyle(large) {fontSize = 35};
        }

        static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }

        static void Text(float x, float y, float width, float height, string label, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(new Rect(x, y, width, height), label, style);
        }

        bool Tab(Rect rect, string label, bool selected)
        {
            Fill(rect, selected ? new Color(.12f, .23f, .29f) : Panel);
            if (selected) Fill(new Rect(rect.x, rect.yMax - 2, rect.width, 2), Mint);
            Text(rect.x + 11, rect.y, rect.width - 16, rect.height, label, small, selected ? Mint : Muted);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        void OnGUI()
        {
            if (!Ready) return;
            Styles();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) * .5f, (Screen.height - 720 * scale) * .5f), Quaternion.identity, Vector3.one * scale);
            Fill(new Rect(0, 0, 1280, 149), Ink);
            Fill(new Rect(0, 0, 1280, 3), Mint);
            Text(32, 13, 650, 23, "SUBWAYCARRY  /  PASSENGER INTELLIGENCE", small, Mint);
            Text(32, 36, 680, 47, "다중 승객 군중 학습", title, Color.white);
            Text(32, 83, 730, 26, "여섯 승객의 상호 반응 → 공유 PPO → 기존 이동·충돌 규칙", body, Muted);
            Text(812, 22, 440, 28, $"공유 PPO  /  {report.arenas:N0}개 군중", large, Mint);
            Text(812, 54, 440, 23, $"GPU 학습  {report.transitions:N0} 스텝 · {report.seconds:F1}초", body, Color.white);
            Text(812, 79, 300, 23, report.device, small, Muted);
#if UNITY_EDITOR
            if (trainer == null && Tab(new Rect(1108, 77, 140, 27), "GPU 재학습", false)) LaunchTraining();
#endif
            for (int i = 0; i < Cases.Length; i++)
                if (Tab(new Rect(32 + i * 169, 115, 160, 28), $"0{i + 1}   {Cases[i]}", i == scenario)) SelectCase(i);
            if (Tab(new Rect(812, 115, 201, 28), useImitation ? "비교 기준   기존 모델" : "비교 기준   규칙 기반", false))
            {useImitation = !useImitation;SelectCase(scenario);}
            if (Tab(new Rect(1021, 115, 103, 28), paused ? "계속" : "일시정지", false)) paused = !paused;
            if (Tab(new Rect(1132, 115, 116, 28), "다시 재생", false)) Restart();
            Text(32, 157, 420, 31, useImitation ? "A   이전 PPO + 규칙" : "A   규칙 기반", large, Amber);
            Text(704, 157, 410, 31, "B   군중 PPO + 규칙", large, Mint);
            var baselineCase = (useImitation ? report.imitation : report.rule).cases[scenario];
            var trainedCase = report.ppo.cases[scenario];
            Text(32, 187, 480, 22, $"이 상황의 무접촉 도착   {baselineCase.success:P1} / {baselineCase.episodes:N0}회", small, Muted);
            Text(704, 187, 510, 22, $"이 상황의 무접촉 도착   {trainedCase.success:P1} / {trainedCase.episodes:N0}회", small, Muted);
            DrawReplayFooter();
            DrawResults();
            GUI.matrix = matrix;
        }

        void DrawReplayFooter()
        {
            Fill(new Rect(0, 477, 1280, 43), Ink);
            float progress = Mathf.Clamp01(clock / Duration);
            Fill(new Rect(32, 480, 1216, 2), Line);
            Fill(new Rect(32, 480, 1216 * progress, 2), Mint);
            string State(Trace trace) => clock < Duration ? "평가 궤적 재생" : trace.success ? "무접촉 도착" : trace.contact ? "접촉 기록" : "제한 시간 내 미도착";
            Text(32, 488, 355, 25, $"A   {State(lanes[0].trace)}", small, Amber);
            Text(704, 488, 340, 25, $"B   {State(lanes[1].trace)}", small, Mint);
            Text(1094, 488, 154, 25, $"{Mathf.Min(clock, Duration):F1} / {Duration:F1}초", small, Muted);
        }

        void DrawResults()
        {
            Fill(new Rect(0, 520, 1280, 200), Ink);
            Fill(new Rect(32, 530, 650, 151), Panel);
            Fill(new Rect(698, 530, 550, 151), Panel);
            Text(49, 535, 610, 27, $"분리 평가   {report.ppo.episodes:N0}개 군중 / 정책", body, Color.white);
            Text(298, 563, 111, 23, "규칙", small, Amber);
            Text(411, 563, 113, 23, "이전 PPO", small, Muted);
            Text(538, 563, 124, 23, "PPO + 규칙", small, Mint);
            Metric(588, "무접촉 도착률", report.rule.success, report.imitation.success, report.ppo.success, true);
            Metric(615, "접촉 발생률", report.rule.contact, report.imitation.contact, report.ppo.contact, true);
            Metric(642, "평균 정지 시간", report.rule.wait, report.imitation.wait, report.ppo.wait, false);
            Text(716, 535, 480, 27, "학습 곡선   /   검증 평균 보상", body, Color.white);
            DrawCurve(new Rect(720, 571, 497, 71));
            string checkpoint = $"선택 체크포인트 {report.selectedUpdate}   ·   {report.selectedTransitions:N0} 스텝";
#if UNITY_EDITOR
            if (trainingStatus.Length > 0) checkpoint = trainingStatus;
#endif
            Text(717, 648, 506, 23, checkpoint, small, Muted);
            Text(32, 688, 1020, 25, $"고정 평가 재생 · Seed {report.testSeed}   /   인게임 이동은 기존 경로·시설 예약·물리 검사 사용", small, Muted);
            Text(1025, 688, 228, 25, "1–4 상황  /  Space 정지  /  R 재생", small, Muted);
        }

        void Metric(float y, string label, float baseline, float imitation, float trained, bool percent)
        {
            string Format(float value) => percent ? value.ToString("P1") : $"{value:F2}초";
            Fill(new Rect(49, y, 613, 1), Line);
            Text(49, y + 1, 229, 24, label, small, Muted);
            Text(298, y + 1, 111, 24, Format(baseline), body, Color.white);
            Text(411, y + 1, 111, 24, Format(imitation), body, Color.white);
            Text(538, y + 1, 111, 24, Format(trained), body, Mint);
        }

        void DrawCurve(Rect area)
        {
            for (int i = 0; i < 3; i++) Fill(new Rect(area.x, area.y + i * area.height * .5f, area.width, 1), Line);
            Vector2 previous = default;
            var history = report.history;
            float min = rewardMin, max = rewardMax;
            int selected = report.selectedUpdate;
            int last = history[history.Length - 1].updates;
#if UNITY_EDITOR
            if (live?.history != null && live.history.Length > 0)
            {
                history = live.history;last = live.totalUpdates;selected = -1;
                foreach (var row in history) {min = Mathf.Min(min, row.reward);max = Mathf.Max(max, row.reward);}
            }
#endif
            foreach (var row in history)
            {
                var point = new Vector2(area.x + area.width * row.updates / Mathf.Max(1, last),
                    area.yMax - area.height * Mathf.InverseLerp(min - .05f, max + .05f, row.reward));
                if (row.updates > 0)
                {
                    var delta = point - previous;
                    var matrix = GUI.matrix;
                    GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, previous);
                    Fill(new Rect(previous.x, previous.y, delta.magnitude, 2), Mint);
                    GUI.matrix = matrix;
                }
                if (row.updates == selected) Fill(new Rect(point.x - 3, point.y - 3, 7, 7), Amber);
                previous = point;
            }
        }

        void OnDestroy()
        {
#if UNITY_EDITOR
            trainer?.Dispose();
#endif
            foreach (var material in materials) if (material != null) Destroy(material);
        }
    }
}

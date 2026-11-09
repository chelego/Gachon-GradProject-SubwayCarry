using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SubwayCarry.Gameplay3D.Editor
{
    public sealed class PassengerLearningExperimentsWindow : EditorWindow
    {
        const string ScenePath = "Assets/_Project/Scenes/3D/Test/PassengerLearning.unity";
        const string GamePath = "Assets/_Project/Scenes/3D/Ingame/Game";
        const string WorkerKey = "SubwayCarry.PassengerExperiments.Worker.";
        const string WorkerLaunchKey = "SubwayCarry.PassengerExperiments.Launched.";
        const string PythonKey = "SubwayCarry.PassengerExperiments.Python";
        static string ProjectPath => Directory.GetParent(Application.dataPath).FullName;
        static string RunRoot => Path.Combine(ProjectPath, "Logs/CrowdBehavior/PassengerLearning/Interactive");
        static string TrainerPath => Path.Combine(ProjectPath, GamePath, "Training~/train_group_steering.py");
        static string BasePath => Path.Combine(ProjectPath, GamePath, "Resources/PassengerSteeringPPO.json");

        [Serializable] sealed class RunConfig
        {
            public string runId, algorithm, baseSha256;
            public int seed, updates, arenas, agentsPerArena, horizon, validationArenas, testArenas;
            public int validationSeed, testSeed, epochs, batchSize;
            public float dt, learningRate, gamma, gaeLambda, clip;
        }

        [Serializable] sealed class Progress
        {
            public string stage, device;
            public int pid, updates, totalUpdates, transitions, selectedUpdate, checkpointUpdate;
            public float seconds, stepsPerSecond;
            public bool complete;
        }

        [Serializable] sealed class ResultMetrics { public int episodes; public float success, contact, wait; }
        [Serializable] sealed class Evaluation { public int testSeed; public ResultMetrics rule, imitation, ppo; }

        sealed class Run
        {
            public readonly string folder, id;
            public RunConfig config;
            public Progress progress;
            public Evaluation evaluation;
            public bool active, stopping, hasCheckpoint, hasEvaluation, hasLog;
            public string state = "Unknown";
            DateTime lastProgress;

            public Run(string folder)
            {
                this.folder = folder;
                id = Path.GetFileName(folder);
            }

            public string FilePath(string name) => Path.Combine(folder, name);

            public void Refresh()
            {
                config ??= ReadJson<RunConfig>(FilePath("run-config.json"));
                var stamp = File.GetLastWriteTimeUtc(FilePath("training-progress.json"));
                if (stamp != lastProgress)
                {
                    progress = ReadJson<Progress>(FilePath("training-progress.json"));
                    lastProgress = stamp;
                }
                int startedPid = SessionState.GetInt(WorkerKey + id, 0);
                active = IsWorkerAlive(startedPid, this) || IsStarting(startedPid, id) ||
                    (progress?.complete != true && progress?.stage != "paused" && IsWorkerAlive(progress?.pid ?? 0, this));
                if (!active && startedPid != 0)
                {
                    SessionState.EraseInt(WorkerKey + id);
                    SessionState.EraseString(WorkerLaunchKey + id);
                }
                stopping = active && File.Exists(FilePath("stop.request")) && progress?.stage != "paused";
                hasCheckpoint = File.Exists(FilePath("latest.pt"));
                hasEvaluation = progress?.complete == true && File.Exists(FilePath("PassengerLearningReport.json"));
                hasLog = File.Exists(FilePath("run.log"));
                if (hasEvaluation) evaluation ??= ReadJson<Evaluation>(FilePath("PassengerLearningReport.json"));
                state = active ? (stopping ? "Stopping" : ActiveStage(progress?.stage)) :
                    hasEvaluation ? "Complete" : hasCheckpoint ? "Checkpoint saved" : "Incomplete";
            }

            static string ActiveStage(string stage) => stage switch
            {
                "training" => "Training",
                "validation" => "Validation",
                "heldout" => "Held-out evaluation",
                _ => "Starting"
            };
        }

        readonly List<Run> runs = new();
        [SerializeField] string selectedId, pendingReplayId;
        Run selected;
        MultiColumnListView runList;
        IntegerField seedField;
        PopupField<string> preset;
        Label runHeading, status, settings, evaluation, message, replaySource, arrivals;
        ProgressBar progressBar;
        Button start, stop, resume, openFolder, openLog, openConfig, openCheckpoint, loadReplay, tensorBoardButton;
        VisualElement replayControls;
        Slider replayTime;
        PopupField<string> scenario, baseline;
        Toggle trails;
        Button playPause;
        Metro3DPassengerLearningLab replay;
        IVisualElementScheduledItem poll;
        Process tensorBoard;
        Task tensorBoardConnect;
        TcpClient tensorBoardClient;
        int tensorBoardPort;
        double tensorBoardDeadline;
        bool openTensorBoardPending;

        [MenuItem("Window/SubwayCarry/Passenger AI Experiments")]
        public static void Open()
        {
            var window = GetWindow<PassengerLearningExperimentsWindow>();
            window.titleContent = new GUIContent("Passenger AI");
            window.minSize = new Vector2(620, 370);
        }

        public void CreateGUI()
        {
            poll?.Pause();
            var root = rootVisualElement;
            root.Clear();
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(RefreshList) { text = "Refresh" });
            toolbar.Add(new ToolbarButton(() => OpenSource(TrainerPath)) { text = "Trainer source" });
            toolbar.Add(new ToolbarButton(() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(GamePath + "/Resources/PassengerSteeringPPO.json")) { text = "Base policy" });
            toolbar.Add(new ToolbarSpacer { flex = true });
            tensorBoardButton = new ToolbarButton(OpenTensorBoard) { text = "Open TensorBoard" };
            toolbar.Add(tensorBoardButton);
            root.Add(toolbar);

            var split = new TwoPaneSplitView(0, 345, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            root.Add(split);
            runList = new MultiColumnListView { itemsSource = runs, selectionType = SelectionType.Single, fixedItemHeight = 23 };
            runList.style.minWidth = 200;
            runList.viewDataKey = "subway-passenger-runs";
            runList.columns.Add(MakeColumn("run", "Run ID", 174, i => runs[i].id, true));
            runList.columns.Add(MakeColumn("state", "State", 98, i => runs[i].state));
            runList.columns.Add(MakeColumn("steps", "Steps", 80, i => runs[i].progress?.transitions.ToString("N0") ?? "—"));
            runList.selectionChanged += items =>
            {
                foreach (object item in items) { SelectRun((Run)item); return; }
                SelectRun(null);
            };
            split.Add(runList);

            var inspector = new ScrollView(ScrollViewMode.Vertical);
            inspector.style.minWidth = 245;
            inspector.contentContainer.style.paddingLeft = 9;
            inspector.contentContainer.style.paddingRight = 9;
            inspector.contentContainer.style.paddingTop = 5;
            split.Add(inspector);
            BuildInspector(inspector);
            RefreshList();
            poll = root.schedule.Execute(Poll).Every(400);
        }

        static Column MakeColumn(string name, string heading, int width, Func<int, string> value, bool stretch = false) => new()
        {
            name = name, title = heading, width = width, minWidth = 60, stretchable = stretch,
            makeCell = () => new Label(), bindCell = (element, index) => ((Label)element).text = value(index)
        };

        void BuildInspector(VisualElement parent)
        {
            var launch = new Foldout { text = "New run", value = true };
            preset = new PopupField<string>("Preset", new List<string> { "Full · 384 × 224", "Smoke · 32 × 8" }, 0);
            seedField = new IntegerField("Training seed") { value = 71592, isDelayed = true };
            launch.Add(preset);
            launch.Add(seedField);
            start = AddButton(launch, "Start", StartNew);
            parent.Add(launch);

            var run = new Foldout { text = "Run inspector", value = true };
            runHeading = new Label();
            runHeading.style.unityFontStyleAndWeight = FontStyle.Bold;
            run.Add(runHeading);
            status = AddReadout(run);
            progressBar = new ProgressBar { lowValue = 0, highValue = 1 };
            run.Add(progressBar);
            var controls = Row(run);
            stop = AddButton(controls, "Stop", RequestStop);
            resume = AddButton(controls, "Resume", ResumeRun);
            openFolder = AddButton(controls, "Folder", () => EditorUtility.RevealInFinder(selected.folder));
            openLog = AddButton(controls, "Log", () => OpenSource(selected.FilePath("run.log")));

            var config = new Foldout { text = "Configuration and checkpoints", value = true };
            settings = AddReadout(config);
            var assets = Row(config);
            openConfig = AddButton(assets, "run-config.json", () => OpenSource(selected.FilePath("run-config.json")));
            openCheckpoint = AddButton(assets, "latest.pt", () => EditorUtility.RevealInFinder(selected.FilePath("latest.pt")));
            run.Add(config);

            var heldout = new Foldout { text = "Held-out evaluation", value = true };
            evaluation = AddReadout(heldout);
            loadReplay = AddButton(heldout, "Load into replay", QueueReplay);
            run.Add(heldout);
            parent.Add(run);

            var playback = new Foldout { text = "Scene replay", value = true };
            replaySource = AddReadout(playback);
            replayControls = new VisualElement();
            playback.Add(replayControls);
            var bar = Row(replayControls);
            playPause = AddButton(bar, "Pause", () => { if (replay != null) replay.ReplayPaused = !replay.ReplayPaused; });
            AddButton(bar, "Restart", () => replay?.RestartReplay());
            AddButton(bar, "Baseline report", () =>
            {
                pendingReplayId = null;
                replay?.LoadDefaultReplay();
            });
            scenario = new PopupField<string>("Scenario", new List<string> { "Bidirectional", "Merge", "Following", "Bottleneck" }, 3);
            baseline = new PopupField<string>("Top lane (A)", new List<string> { "Previous PPO", "Rules" }, 0);
            trails = new Toggle("Trails") { value = true };
            replayTime = new Slider("Time", 0, 9) { showInputField = true };
            scenario.RegisterValueChangedCallback(_ => replay?.SelectCase(scenario.index));
            baseline.RegisterValueChangedCallback(_ => replay?.SetComparison(baseline.index == 0));
            trails.RegisterValueChangedCallback(evt => { if (replay != null) replay.TrailsVisible = evt.newValue; });
            replayTime.RegisterValueChangedCallback(evt => replay?.SeekReplay(evt.newValue));
            replayControls.Add(scenario);
            replayControls.Add(baseline);
            replayControls.Add(trails);
            replayControls.Add(replayTime);
            arrivals = AddReadout(replayControls);
            parent.Add(playback);
            message = AddReadout(parent);
        }

        static VisualElement Row(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            parent.Add(row);
            return row;
        }

        static Button AddButton(VisualElement parent, string text, Action action)
        {
            var button = new Button(action) { text = text };
            parent.Add(button);
            return button;
        }

        static Label AddReadout(VisualElement parent)
        {
            var label = new Label();
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 3;
            label.style.marginBottom = 4;
            parent.Add(label);
            return label;
        }

        void RefreshList()
        {
            string keep = selectedId;
            runs.Clear();
            if (Directory.Exists(RunRoot))
            {
                foreach (string folder in Directory.GetDirectories(RunRoot))
                {
                    string id = Path.GetFileName(folder);
                    if (File.Exists(Path.Combine(folder, "run-config.json")) || SessionState.GetInt(WorkerKey + id, 0) != 0)
                        runs.Add(new Run(folder));
                }
                runs.Sort((left, right) => Directory.GetLastWriteTimeUtc(right.folder).CompareTo(Directory.GetLastWriteTimeUtc(left.folder)));
            }
            foreach (var run in runs) ReadRun(run);
            runList.Rebuild();
            int index = runs.FindIndex(run => run.id == keep);
            if (index < 0 && runs.Count > 0) index = 0;
            runList.SetSelectionWithoutNotify(index >= 0 ? new[] { index } : Array.Empty<int>());
            SelectRun(index >= 0 ? runs[index] : null);
        }

        void SelectRun(Run run)
        {
            selected = run;
            selectedId = run?.id;
            UpdateInspector();
        }

        void Poll()
        {
            foreach (var run in runs) ReadRun(run);
            runList.RefreshItems();
            UpdateInspector();
            PollReplay();
            PollTensorBoard();
        }

        void ReadRun(Run run)
        {
            try { run.Refresh(); }
            catch (IOException) { }
            catch (ArgumentException error) { if (run == selected) SetMessage(error.Message); }
        }

        void UpdateInspector()
        {
            bool busy = runs.Exists(run => run.active);
            start.SetEnabled(!busy);
            stop.SetEnabled(selected?.active == true && !selected.stopping && selected.progress?.stage != "heldout");
            resume.SetEnabled(!busy && selected?.hasCheckpoint == true && selected.config?.algorithm == "GroupPPO" && !selected.hasEvaluation);
            openFolder.SetEnabled(selected != null);
            openLog.SetEnabled(selected?.hasLog == true);
            openConfig.SetEnabled(selected?.config != null);
            openCheckpoint.SetEnabled(selected?.hasCheckpoint == true);
            loadReplay.SetEnabled(selected?.hasEvaluation == true);
            runHeading.text = selected?.id ?? "Select a run";
            var data = selected?.progress;
            progressBar.value = data == null ? 0 : Mathf.Clamp01(data.updates / (float)Mathf.Max(1, data.totalUpdates));
            progressBar.title = data == null ? "" : $"{data.updates:N0} / {data.totalUpdates:N0} updates";
            status.text = data == null ? selected?.state ?? "" :
                $"{selected.state} · {data.device}\n{data.transitions:N0} steps · {data.seconds:F1} s" +
                (selected.active && data.stepsPerSecond > 0 ? $" · {data.stepsPerSecond:N0} steps/s" : "");

            var cfg = selected?.config;
            int saved = data?.complete == true ? data.updates : data?.checkpointUpdate ?? 0;
            settings.text = cfg == null ? "" :
                $"{cfg.algorithm} · {cfg.arenas:N0} environments × {cfg.agentsPerArena} actors\n" +
                $"Seed {cfg.seed} · Horizon {cfg.horizon} · Δt {cfg.dt:F2}s\n" +
                $"LR {cfg.learningRate:G} · γ {cfg.gamma:G} · GAE {cfg.gaeLambda:G} · Clip {cfg.clip:G}\n" +
                $"{cfg.epochs} epochs · Batch {cfg.batchSize:N0}\n" +
                $"Validation {cfg.validationArenas:N0} (seed {cfg.validationSeed}) · Test {cfg.testArenas:N0} (seed {cfg.testSeed})\n" +
                $"Latest update {saved} · Selected update {data?.selectedUpdate ?? 0}";
            var result = selected?.evaluation;
            evaluation.text = result?.ppo == null ? "Awaiting a completed evaluation" :
                $"{result.ppo.episodes:N0} groups / policy · Seed {result.testSeed}\n" +
                FormatResult("Rules", result.rule) + "\n" + FormatResult("Previous PPO", result.imitation) + "\n" + FormatResult("Group PPO", result.ppo);
        }

        static string FormatResult(string name, ResultMetrics row) => row == null ? name :
            $"{name}   {row.success:P2} clean · {row.contact:P2} contact · {row.wait:F3}s stopped";

        void StartNew()
        {
            if (seedField.value < 0) { SetMessage("Training seed must be nonnegative."); return; }
            bool smoke = preset.index == 1;
            string id = "crowd-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            StartWorker(new Run(Path.Combine(RunRoot, id)), new RunConfig
            {
                seed = seedField.value, arenas = smoke ? 32 : 384, updates = smoke ? 8 : 224,
                validationArenas = smoke ? 128 : 1024, testArenas = smoke ? 256 : 4096
            }, false);
        }

        void ResumeRun()
        {
            if (selected?.config != null) StartWorker(selected, selected.config, true);
        }

        void StartWorker(Run run, RunConfig config, bool resumeRun)
        {
            if (runs.Exists(item => item.active)) return;
            string python = ResolvePython();
            if (string.IsNullOrEmpty(python)) return;
            try
            {
                Directory.CreateDirectory(run.folder);
                var arguments = "-B -u " + Quote(TrainerPath) + " --base " + Quote(BasePath) +
                    " --output " + Quote(run.folder) + " --log-file" +
                    FormattableString.Invariant($" --seed {config.seed} --updates {config.updates} --arenas {config.arenas} --validation-arenas {config.validationArenas} --test-arenas {config.testArenas}") +
                    (resumeRun ? " --resume" : "");
                using var process = Process.Start(ProcessInfo(python, arguments));
                if (process == null) throw new InvalidOperationException("CUDA process did not start");
                SessionState.SetInt(WorkerKey + run.id, process.Id);
                SessionState.SetString(WorkerLaunchKey + run.id, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
                selectedId = run.id;
                RefreshList();
                SetMessage(resumeRun ? "Resuming checkpoint" : "Started CUDA worker");
            }
            catch (Exception error) { SetMessage(error.Message); }
        }

        void RequestStop()
        {
            if (selected?.active != true || selected.stopping || selected.progress?.stage == "heldout") return;
            try { File.WriteAllText(selected.FilePath("stop.request"), ""); }
            catch (IOException error) { SetMessage(error.Message); }
        }

        static bool IsWorkerAlive(int pid, Run run)
        {
            if (pid <= 0) return false;
            try
            {
                using var process = Process.GetProcessById(pid);
                return !process.HasExited && process.ProcessName.StartsWith("python", StringComparison.OrdinalIgnoreCase) &&
                    process.StartTime.ToUniversalTime() >= Directory.GetCreationTimeUtc(run.folder).AddSeconds(-1);
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (System.ComponentModel.Win32Exception) { return false; }
        }

        static bool IsStarting(int pid, string id)
        {
            if (pid <= 0 || !long.TryParse(SessionState.GetString(WorkerLaunchKey + id, ""), out long ticks)) return false;
            long age = DateTime.UtcNow.Ticks - ticks;
            return age >= 0 && age < TimeSpan.FromSeconds(3).Ticks;
        }

        void QueueReplay()
        {
            if (selected?.hasEvaluation != true) return;
            pendingReplayId = selected.id;
            if (replay == null) SetMessage("Open PassengerLearning and enter Play to load this evaluation.");
            PollReplay();
        }

        void PollReplay()
        {
            bool active = EditorApplication.isPlaying && SceneManager.GetActiveScene().path == ScenePath;
            if (!active) replay = null;
            else if (replay == null) replay = FindFirstObjectByType<Metro3DPassengerLearningLab>();
            bool ready = replay != null && replay.Ready;
            replayControls.SetEnabled(ready);
            if (!ready)
            {
                replaySource.text = string.IsNullOrEmpty(pendingReplayId) ? "PassengerLearning · Play mode" : $"Queued · {pendingReplayId}";
                return;
            }
            if (!string.IsNullOrEmpty(pendingReplayId))
            {
                string id = pendingReplayId;
                pendingReplayId = null;
                try
                {
                    if (!replay.LoadReplay(File.ReadAllText(Path.Combine(RunRoot, id, "PassengerLearningReport.json")), id))
                        SetMessage("Run ID or replay format does not match.");
                    else SetMessage("Loaded " + id);
                }
                catch (IOException error) { SetMessage(error.Message); }
                catch (ArgumentException error) { SetMessage(error.Message); }
            }
            replaySource.text = replay.ReplayRunId ?? "Baseline report";
            replayTime.highValue = replay.ReplayDuration;
            replayTime.SetValueWithoutNotify(replay.ReplayTime);
            scenario.SetValueWithoutNotify(scenario.choices[replay.Scenario]);
            baseline.SetValueWithoutNotify(baseline.choices[replay.ComparePreviousPpo ? 0 : 1]);
            trails.SetValueWithoutNotify(replay.TrailsVisible);
            playPause.text = replay.ReplayPaused ? "Play" : "Pause";
            arrivals.text = $"Top A · {replay.BaselineArrivals}/6 arrived\nBottom B · Group PPO · {replay.TrainedArrivals}/6 arrived";
        }

        void OpenTensorBoard()
        {
            if (tensorBoard != null && !tensorBoard.HasExited)
            {
                if (!openTensorBoardPending && !Application.isBatchMode) Application.OpenURL(TensorBoardUrl());
                return;
            }
            string python = ResolvePython();
            if (string.IsNullOrEmpty(python)) return;
            try
            {
                Directory.CreateDirectory(RunRoot);
                var socket = new TcpListener(IPAddress.Loopback, 0);
                socket.Start();
                try { tensorBoardPort = ((IPEndPoint)socket.LocalEndpoint).Port; }
                finally { socket.Stop(); }
                tensorBoard?.Dispose();
                tensorBoard = Process.Start(ProcessInfo(python, "-m tensorboard.main --logdir " + Quote(RunRoot) +
                    " --host 127.0.0.1 --port " + tensorBoardPort.ToString(CultureInfo.InvariantCulture)));
                if (tensorBoard == null) throw new InvalidOperationException("TensorBoard did not start");
                tensorBoardDeadline = EditorApplication.timeSinceStartup + 25;
                openTensorBoardPending = true;
                SetMessage("Starting local TensorBoard");
            }
            catch (Exception error) { SetMessage(error.Message); }
        }

        void PollTensorBoard()
        {
            if (!openTensorBoardPending) return;
            if (tensorBoard.HasExited || EditorApplication.timeSinceStartup > tensorBoardDeadline)
            {
                StopTensorBoard();
                SetMessage("TensorBoard did not respond. Check the selected Python environment.");
                return;
            }
            if (tensorBoardConnect == null)
            {
                tensorBoardClient = new TcpClient();
                tensorBoardConnect = tensorBoardClient.ConnectAsync(IPAddress.Loopback, tensorBoardPort);
                return;
            }
            if (!tensorBoardConnect.IsCompleted) return;
            bool connected = tensorBoardClient.Connected;
            _ = tensorBoardConnect.Exception;
            tensorBoardClient.Dispose();
            tensorBoardConnect = null;
            if (!connected) return;
            openTensorBoardPending = false;
            SetMessage("TensorBoard · " + TensorBoardUrl());
            if (!Application.isBatchMode) Application.OpenURL(TensorBoardUrl());
        }

        string TensorBoardUrl() => $"http://127.0.0.1:{tensorBoardPort}";

        void StopTensorBoard()
        {
            openTensorBoardPending = false;
            tensorBoardClient?.Dispose();
            tensorBoardConnect = null;
            if (tensorBoard == null) return;
            try { if (!tensorBoard.HasExited) tensorBoard.Kill(); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            finally { tensorBoard.Dispose(); tensorBoard = null; }
        }

        void OnDisable()
        {
            poll?.Pause();
            StopTensorBoard();
        }

        void SetMessage(string value) { if (message != null) message.text = value; }
        static string Quote(string value) => "\"" + value + "\"";
        static void OpenSource(string path) { if (File.Exists(path)) InternalEditorUtility.OpenFileAtLineExternal(path, 1); }
        static ProcessStartInfo ProcessInfo(string python, string arguments) => new()
        {
            FileName = python, Arguments = arguments, WorkingDirectory = ProjectPath,
            UseShellExecute = false, CreateNoWindow = true
        };

        static string ResolvePython()
        {
            string python = EditorPrefs.GetString(PythonKey);
            if (!File.Exists(python)) python = Environment.GetEnvironmentVariable("SUBWAYCARRY_PYTHON");
            if (string.IsNullOrEmpty(python)) python = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Miniforge3", "envs", "projecthive-ml", "python.exe");
            if (!File.Exists(python)) python = EditorUtility.OpenFilePanel("PyTorch CUDA Python", "", "exe");
            if (File.Exists(python)) EditorPrefs.SetString(PythonKey, python);
            return python;
        }

        static T ReadJson<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            using var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(file);
            return JsonUtility.FromJson<T>(reader.ReadToEnd());
        }
    }
}

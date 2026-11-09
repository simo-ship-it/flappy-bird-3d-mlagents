using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;

[DefaultExecutionOrder(-1000)]
public class FlappySession : MonoBehaviour
{
    public static FlappySession Instance;
    public FlappyWorld MainWorld;
    public bool Training, Tutorial;
    public ModelAsset Model;
    public int TrainingAreas = 24;
    public bool Evaluation { get; private set; }
    public bool RandomControl { get; private set; }
    public bool Trace { get; private set; }
    public bool Recording => !string.IsNullOrEmpty(captureDirectory);
    public int Seed = 2026, TargetPipes = 40;
    public int Completed { get; private set; }
    private int targetEpisodes = 30, captureFrames = 1050, captureFrame;
    private string output, captureDirectory, mechanicsOutput;
    private bool queuedFlap, quitting;
    private readonly List<Trial> trials = new List<Trial>();
    private GUIStyle title, text, score, small;

    private void Awake()
    {
        Instance = this;
        Application.runInBackground = true;
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--random") RandomControl = true;
            if (args[i] == "--trace") Trace = true;
            if (i + 1 >= args.Length) continue;
            if (args[i] == "--evaluation") { Evaluation = true; output = args[++i]; }
            else if (args[i] == "--episodes") targetEpisodes = int.Parse(args[++i]);
            else if (args[i] == "--seed") Seed = int.Parse(args[++i]);
            else if (args[i] == "--target-pipes") TargetPipes = int.Parse(args[++i]);
            else if (args[i] == "--capture") captureDirectory = args[++i];
            else if (args[i] == "--capture-frames") captureFrames = int.Parse(args[++i]);
            else if (args[i] == "--mechanics-check") mechanicsOutput = args[++i];
        }
        if (Evaluation || Recording || RandomControl || mechanicsOutput != null) Training = false;
        if (Evaluation || mechanicsOutput != null) Time.timeScale = 30;
        if (Recording)
        {
            Directory.CreateDirectory(captureDirectory);
            Time.captureFramerate = 30; Time.timeScale = 1;
            Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
        }
        MainWorld.gameObject.SetActive(false);
        for (int i = (Training ? TrainingAreas : 1) - 1; i >= 0; i--)
        {
            var world = i == 0 ? MainWorld : Instantiate(MainWorld, MainWorld.transform.parent);
            world.name = "Arena " + (i + 1);
            world.transform.position = Training ? new Vector3((i % 6) * 140, 0, (i / 6) * 140) : Vector3.zero;
            world.SetIndex(i);
            world.Bird.freeMovement = Tutorial;
            world.Agent.MaxStep = Training ? 4000 : (Evaluation ? 6000 : 0);
            var behavior = world.Agent.GetComponent<BehaviorParameters>();
            behavior.Model = Model;
            behavior.DeterministicInference = true;
            behavior.InferenceDevice = InferenceDevice.Default;
            behavior.BehaviorType = Training ? BehaviorType.Default :
                (Tutorial || RandomControl || Model == null ? BehaviorType.HeuristicOnly : BehaviorType.InferenceOnly);
            foreach (var a in world.GetComponentsInChildren<Animator>(true)) a.enabled = !Training;
            foreach (var r in world.GetComponentsInChildren<Renderer>(true)) r.enabled = !Training || i == 0;
            world.gameObject.SetActive(true);
            if (!Training)
                foreach (var a in world.GetComponentsInChildren<Animator>())
                    if (a.HasState(0, Animator.StringToHash("Fly"))) a.Play("Fly");
        }
    }

    private void Start()
    {
        if (mechanicsOutput == null) return;
        StartCoroutine(CheckMechanics());
    }
    private IEnumerator CheckMechanics()
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        var w = MainWorld; var p = w.Pipes[2];
        int lives = w.Lives;
        p.PrizeAvailable = true; p.prize.SetActive(true);
        bool lifeAdded = p.Collect() && w.Lives == lives + 1;
        bool noDuplicatePrize = !p.Collect() && w.Lives == lives + 1;
        var first = w.Pipes[0]; first.Entered = true;
        bool scoring = first.TryScore() && !first.TryScore() && w.Score == 1;
        w.LoseLife();
        bool lifeConsumed = w.Lives == 1 && Completed == 0;
        bool noDuplicateAfterRespawn = true;
        foreach (var pipe in w.Pipes)
            if (pipe.GateId == 3) noDuplicateAfterRespawn &= !pipe.PrizeAvailable && !pipe.Collect();
        bool noDuplicateByReactivation = !w.ClaimPrize(3);
        w.LoseLife();
        bool gameOverAndReset = Completed == 1 && w.Lives == 1 &&
            trials.Count == 1 && trials[0].reason == "death";
        bool valid = lifeAdded && noDuplicatePrize && noDuplicateAfterRespawn && noDuplicateByReactivation && scoring && lifeConsumed && gameOverAndReset;
        File.WriteAllText(mechanicsOutput, "{\"passed\":" + (valid ? "true" : "false") +
            ",\"extra_life\":" + lifeAdded.ToString().ToLowerInvariant() +
            ",\"no_duplicate_prize\":" + noDuplicatePrize.ToString().ToLowerInvariant() +
            ",\"no_duplicate_after_respawn\":" + noDuplicateAfterRespawn.ToString().ToLowerInvariant() +
            ",\"unique_prize_id\":" + noDuplicateByReactivation.ToString().ToLowerInvariant() +
            ",\"score_once\":" + scoring.ToString().ToLowerInvariant() +
            ",\"life_consumed\":" + lifeConsumed.ToString().ToLowerInvariant() +
            ",\"game_over_and_reset\":" + gameOverAndReset.ToString().ToLowerInvariant() + "}");
        Application.Quit(valid ? 0 : 1);
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || Training || Evaluation || Recording) return;
        if (keyboard.spaceKey.wasPressedThisFrame) queuedFlap = true;
        if (keyboard.rKey.wasPressedThisFrame) MainWorld.Agent.EndEpisode();
        if (keyboard.escapeKey.wasPressedThisFrame) Application.Quit();
    }
    public bool ConsumeFlap() { bool value = queuedFlap; queuedFlap = false; return value; }

    public void Report(Trial trial)
    {
        if (quitting) return;
        Completed++;
        if (!Training) trials.Add(trial);
        if (Evaluation && trials.Count >= targetEpisodes)
        {
            quitting = true;
            WriteEvaluation(output);
            Application.Quit();
        }
    }
    private void WriteEvaluation(string path)
    {
        var summary = new Summary { seed = Seed, targetPipes = TargetPipes,
            episodes = trials.Count, controller = RandomControl ? "random-20-percent-flap" : "trained-ONNX",
            trials = trials.ToArray() };
        foreach (var t in trials)
        {
            summary.meanPipes += t.pipes; summary.meanSeconds += t.seconds;
            summary.meanPrizes += t.prizes; summary.meanCollisions += t.collisions;
            if (t.reason == "target") summary.completedTarget++;
            if (t.reason == "target" && t.collisions == 0) summary.cleanTarget++;
        }
        if (trials.Count > 0)
        {
            summary.meanPipes /= trials.Count; summary.meanSeconds /= trials.Count;
            summary.meanPrizes /= trials.Count; summary.meanCollisions /= trials.Count;
        }
        File.WriteAllText(path, JsonUtility.ToJson(summary, true));
    }
    private void LateUpdate()
    {
        if (!Recording || quitting) return;
        if (captureFrame < captureFrames)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(captureDirectory, captureFrame.ToString("D5") + ".png"));
            captureFrame++;
        }
        else
        {
            quitting = true;
            File.WriteAllText(Path.Combine(captureDirectory, "recording.json"),
                "{\"frames\":" + captureFrame + ",\"fps\":30,\"pipes\":" + MainWorld.Score +
                ",\"prizes\":" + MainWorld.Prizes + ",\"collisions\":" + MainWorld.Hits + "}");
            StartCoroutine(QuitAfterCapture());
        }
    }
    private IEnumerator QuitAfterCapture()
    {
        yield return new WaitForSecondsRealtime(1);
        Application.Quit();
    }

    private void OnGUI()
    {
        if (Training || mechanicsOutput != null) return;
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            text = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            score = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            small = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        }
        float scale = Screen.width / 1280f;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        GUI.color = new Color(0.04f, 0.08f, 0.12f, 0.91f);
        GUI.DrawTexture(new Rect(0, 0, 1280, 92), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(26, 12, 620, 32), "Flappy Bird 3D", title);
        GUI.Label(new Rect(26, 50, 700, 30),
            (Tutorial ? "Gioco manuale" : "Agente in gioco") + "   |   Vite: " + MainWorld.Lives +
            "   Premi: " + MainWorld.Prizes, text);
        GUI.Label(new Rect(940, 14, 310, 48), "Tubi: " + MainWorld.Score, score);
        GUI.color = new Color(1, 1, 1, 0.85f);
        GUI.Label(new Rect(26, 670, 1120, 28), Tutorial ?
            "Spazio: battito   Frecce: movimento   R: ricomincia   Esc: esci" :
            "Le sfere dorate danno una vita in più.   R: ricomincia   Esc: esci", small);
        if (MainWorld.Seconds < MainWorld.NoticeUntil && !string.IsNullOrEmpty(MainWorld.Notice))
        {
            float width = Mathf.Max(140, text.CalcSize(new GUIContent(MainWorld.Notice)).x + 40);
            float left = (1280 - width) / 2;
            GUI.color = new Color(0.08f, 0.12f, 0.17f, 0.85f);
            GUI.DrawTexture(new Rect(left, 105, width, 38), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(left + 20, 110, width - 40, 30), MainWorld.Notice, text);
        }
    }

    [Serializable] public class Trial
    {
        public int pipes, prizes, collisions, flaps;
        public float seconds, reward;
        public string reason;
    }
    [Serializable] public class Summary
    {
        public int seed, targetPipes, episodes, completedTarget, cleanTarget;
        public string controller;
        public float meanPipes, meanSeconds, meanPrizes, meanCollisions;
        public Trial[] trials;
    }
}


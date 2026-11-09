using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;

[DefaultExecutionOrder(100)]
public class FlappyWorld : MonoBehaviour
{
    public FlappyAgent Agent;
    public BirdPlayerScript Bird;
    public PipesMoves[] Pipes;
    public int Lives, Score, Prizes, Hits, Flaps;
    public bool PendingHit, Recorded;
    public float Seconds, Speed;
    public string Notice;
    public float NoticeUntil;
    public float BirdY => Bird.transform.position.y - transform.position.y;
    public float BirdX => Bird.transform.position.x - transform.position.x;
    public float EpisodeReward;
    public float PrizeReward { get; private set; }
    public float CollisionPenalty { get; private set; }
    public float AlignmentReward { get; private set; }
    private System.Random random;
    private System.Random actionRandom;
    private int serial, index;
    private readonly HashSet<int> collectedPrizeIds = new HashSet<int>();
    private float protection;
    public void SetIndex(int value) { index = value; random = new System.Random(42 + value * 1009); actionRandom = new System.Random(719 + value); }
    public void BeginEpisode()
    {
        if (random == null) SetIndex(0);
        serial++;
        if (FlappySession.Instance.Evaluation || FlappySession.Instance.Recording)
            random = new System.Random(FlappySession.Instance.Seed + serial * 7919 + index);
        Lives = 1; Score = Prizes = Hits = Flaps = 0; Seconds = EpisodeReward = 0;
        PrizeReward = Academy.Instance.EnvironmentParameters.GetWithDefault("prize_reward", 5f);
        CollisionPenalty = Academy.Instance.EnvironmentParameters.GetWithDefault("collision_penalty", 10f);
        AlignmentReward = Academy.Instance.EnvironmentParameters.GetWithDefault("alignment_reward", 0.3f);
        // Expose extra-life states during training before they are mastered by
        // collecting a prize. Real games and evaluation always start with one.
        if (FlappySession.Instance.Training &&
            Academy.Instance.EnvironmentParameters.GetWithDefault("random_initial_lives", 0) > 0)
            Lives = random.Next(1, 4);
        Recorded = PendingHit = false; collectedPrizeIds.Clear(); Speed = Range(9, 11);
        ResetFlight();
        Notice = null; NoticeUntil = 0;
    }
    private void ResetFlight()
    {
        protection = 0.12f; PendingHit = false;
        Bird.ResetFlight(transform.position + new Vector3(0, 12, 0));
        for (int i = 0; i < Pipes.Length; i++) Spawn(Pipes[i], 22 + i * 18, Score + i + 1);
        Physics.SyncTransforms();
    }
    private void Spawn(PipesMoves p, float x, int gateId)
    {
        p.Configure(x, Range(8, 16), gateId % 3 == 0 && !collectedPrizeIds.Contains(gateId), gateId);
    }
    public bool ClaimPrize(int gateId) => collectedPrizeIds.Add(gateId);
    private void FixedUpdate()
    {
        if (Agent == null || Recorded) return;
        Seconds += Time.fixedDeltaTime; protection -= Time.fixedDeltaTime;
        if (PendingHit || BirdY > 25 || (!Bird.freeMovement && BirdY < 1))
        {
            PendingHit = false;
            if (protection <= 0) { LoseLife(); return; }
        }
        foreach (var p in Pipes)
        {
            p.X -= Speed * Time.fixedDeltaTime;
            p.transform.localPosition = new Vector3(p.X, 0, 0);
            // Moving colliders must not carry the character through an obstacle.
            // These bounds match the capsule and the inner faces of the pipe lips.
            if (!Bird.freeMovement && protection <= 0 && Mathf.Abs(p.X - BirdX) < 2.05f &&
                (BirdY - 0.65f < p.GapY - 3.5f || BirdY + 0.65f > p.GapY + 3.5f))
            {
                LoseLife(); return;
            }
            if (p.PrizeAvailable &&
                Vector3.Distance(p.prize.transform.position, Bird.transform.position) < 1.35f) p.Collect();
            if (!p.Passed && p.X + 2.2f < BirdX) p.TryScore();
            if (p.X < -22)
            {
                float furthest = -22;
                int lastId = 0;
                foreach (var other in Pipes) { furthest = Mathf.Max(furthest, other.X); lastId = Mathf.Max(lastId, other.GateId); }
                Spawn(p, furthest + 18, lastId + 1);
            }
        }
        if (FlappySession.Instance.Evaluation && Score >= FlappySession.Instance.TargetPipes)
        {
            Finish("target"); Agent.EndEpisode();
        }
    }
    public void Reward(float value) { EpisodeReward += value; Agent.AddReward(value); }
    public void LoseLife()
    {
        if (Recorded) return;
        if (FlappySession.Instance.Trace) Debug.Log("HIT t=" + Seconds + " bird=" + Bird.transform.localPosition + " score=" + Score + " prizes=" + Prizes + " lives=" + Lives);
        Hits++; Lives--; Reward(-CollisionPenalty);
        if (Lives <= 0)
        {
            Reward(-3); Finish("death"); Agent.EndEpisode();
        }
        else
        {
            ResetFlight(); Notice = "Vita persa. Si riparte."; NoticeUntil = Seconds + 1.5f;
        }
    }
    public void Finish(string reason)
    {
        if (Recorded) return; Recorded = true;
        var stats = Academy.Instance.StatsRecorder;
        stats.Add("Game/PipesPassed", Score);
        stats.Add("Game/SurvivalSeconds", Seconds);
        stats.Add("Game/PrizesCollected", Prizes);
        stats.Add("Game/ExtraLivesGranted", Prizes);
        stats.Add("Game/Collisions", Hits);
        stats.Add("Game/Reached40Pipes", Score >= 40 ? 1 : 0);
        FlappySession.Instance.Report(new FlappySession.Trial {
            pipes = Score, seconds = Seconds, prizes = Prizes, collisions = Hits,
            flaps = Flaps, reward = EpisodeReward, reason = reason
        });
    }
    public PipesMoves NextPipe(PipesMoves skip = null)
    {
        PipesMoves best = null;
        foreach (var p in Pipes) if (p != skip && !p.Passed && (best == null || p.X < best.X)) best = p;
        if (best == null) foreach (var p in Pipes) if (p != skip && (best == null || p.X > best.X)) best = p;
        return best;
    }
    public PipesMoves NextPrize()
    {
        PipesMoves best = null;
        foreach (var p in Pipes)
            if (p.PrizeAvailable && p.X > BirdX - 1 && (best == null || p.X < best.X)) best = p;
        return best;
    }
    public bool RandomAction() => actionRandom.NextDouble() < 0.2;
    private float Range(float min, float max) => min + (max - min) * (float)random.NextDouble();
}


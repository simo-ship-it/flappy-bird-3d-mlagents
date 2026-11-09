using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class FlappyAgent : Agent
{
    public FlappyWorld World;
    private bool started;
    public override void OnEpisodeBegin()
    {
        if (started && !World.Recorded) World.Finish("horizon");
        started = true; World.BeginEpisode();
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        var a = World.NextPipe(); var b = World.NextPipe(a); var prize = World.NextPrize();
        float y = World.BirdY;
        sensor.AddObservation(y / 24f);
        sensor.AddObservation(World.Bird.VerticalVelocity / 16f);
        sensor.AddObservation((a.X - World.BirdX) / 60f);
        sensor.AddObservation((a.GapY - y) / 12f);
        sensor.AddObservation(3.5f / 12f);
        sensor.AddObservation((b.X - World.BirdX) / 60f);
        sensor.AddObservation((b.GapY - y) / 12f);
        sensor.AddObservation(prize == null ? 0 : (prize.X - World.BirdX) / 60f);
        sensor.AddObservation(prize == null ? 0 : (prize.GapY - y) / 12f);
        sensor.AddObservation(prize == null ? 0 : 1f);
        sensor.AddObservation(World.Speed / 12f);
    }
    public override void OnActionReceived(ActionBuffers actions)
    {
        World.Reward(0.01f);
        var gate = World.NextPipe();
        if (gate.PrizeAvailable && gate.X > World.BirdX && gate.X - World.BirdX < 18)
        {
            float error = gate.GapY - World.BirdY;
            World.Reward(World.AlignmentReward * Mathf.Exp(-error * error / 4));
        }
        if (actions.DiscreteActions[0] == 1) World.Bird.RequestFlap();
    }
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var session = FlappySession.Instance;
        var actions = actionsOut.DiscreteActions;
        actions[0] = session.RandomControl ?
            (World.RandomAction() ? 1 : 0) : (session.ConsumeFlap() ? 1 : 0);
    }
}


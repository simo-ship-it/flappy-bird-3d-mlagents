using UnityEngine;

public class PipesMoves : MonoBehaviour
{
    public FlappyWorld World;
    public Transform top, bottom, middle;
    public GameObject prize;
    public bool Passed, Entered, PrizeAvailable;
    public float X, GapY;
    public int GateId;
    public void Configure(float x, float gapY, bool hasPrize, int gateId = 0)
    {
        GateId = gateId;
        X = x; GapY = gapY; Passed = Entered = false; PrizeAvailable = hasPrize;
        transform.localPosition = new Vector3(x, 0, 0);
        float low = gapY - 3.65f, high = gapY + 3.65f;
        SetBody(bottom, low / 2, low / 2, true);
        SetBody(top, (high + 26) / 2, (26 - high) / 2, false);
        middle.localPosition = new Vector3(0, gapY, 0);
        middle.localScale = new Vector3(3, 7, 3);
        prize.transform.localPosition = new Vector3(0, gapY, 0);
        prize.SetActive(hasPrize);
    }
    private void SetBody(Transform body, float y, float scaleY, bool lower)
    {
        body.localPosition = new Vector3(0, y, 0);
        body.localScale = new Vector3(3, scaleY, 3);
        var cap = body.GetChild(0);
        cap.localPosition = new Vector3(0, lower ? 1 : -1, 0);
        cap.localScale = new Vector3(1, 0.15f / scaleY, 1);
    }
    public bool TryScore()
    {
        if (Passed || !Entered) return false;
        Passed = true; World.Score++; World.Reward(1);
        return true;
    }
    public bool Collect()
    {
        if (!PrizeAvailable || !World.ClaimPrize(GateId)) return false;
        if (FlappySession.Instance.Trace) Debug.Log("PRIZE t=" + World.Seconds + " pipeX=" + X + " bird=" + World.Bird.transform.localPosition + " score=" + World.Score + " lives=" + World.Lives);
        PrizeAvailable = false; prize.SetActive(false);
        World.Prizes++; World.Lives++; World.Reward(World.PrizeReward);
        World.Notice = "+1 vita";
        World.NoticeUntil = World.Seconds + 1.6f;
        return true;
    }
}


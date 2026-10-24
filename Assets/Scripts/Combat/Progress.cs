using UnityEngine;

/// Tells when an AI stops getting closer to where it is going: the distance has not dropped by "step" tiles for
/// "time" seconds (a river with no bridge between it and its target, a path that only leads to the bank...).
public class Progress
{
    public float time = 1.5f, step = 0.3f;
    float since = -1f, best;

    public void Reset() => since = -1f;

    public bool Stuck(float distance)
    {
        if (since < 0f || distance < best - step)
        {
            since = Time.time;
            best = distance;
            return false;
        }
        return Time.time - since >= time;
    }
}

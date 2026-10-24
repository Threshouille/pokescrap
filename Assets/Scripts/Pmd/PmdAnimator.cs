using UnityEngine;

/// Plays a PmdAnimSet animation on a SpriteRenderer, using the original frame durations.
[RequireComponent(typeof(SpriteRenderer))]
public class PmdAnimator : MonoBehaviour
{
    const float TicksPerSecond = 60f;

    public PmdAnimSet animSet;
    public string animName = "Idle";
    /// 0 = down, then counter-clockwise: down-right, right, up-right, up, up-left, left, down-left.
    public int direction;

    SpriteRenderer spriteRenderer;
    PmdAnimSet.Anim anim;
    int frame;
    float timer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        SetAnimSet(animSet);
    }

    public void SetAnimSet(PmdAnimSet set)
    {
        animSet = set;
        anim = null;
        Play(animName);
    }

    public bool Has(string name) => animSet != null && animSet.Get(name) != null;

    const float FaceHold = 0.15f; // seconds a new walking direction must last before the sprite turns to it
    int pendingDirection = -1;
    float pendingSince;

    /// Turns toward a walking or looking direction, but only once it has lasted FaceHold seconds: an AI nudged left
    /// and right in a crowd no longer seems to spin on itself. Attacks still set "direction" at once.
    public void Face(Vector2 toward)
    {
        if (toward == Vector2.zero) return;
        int index = DirectionIndex(toward);
        if (index == direction)
        {
            pendingDirection = -1;
            return;
        }
        if (index != pendingDirection)
        {
            pendingDirection = index;
            pendingSince = Time.time;
            return;
        }
        if (Time.time - pendingSince >= FaceHold)
        {
            direction = index;
            pendingDirection = -1;
        }
    }

    /// Sprite sheet row for a direction: 0 = down, then counter-clockwise in 45° steps.
    public static int DirectionIndex(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        return ((Mathf.RoundToInt((angle + 90f) / 45f) % 8) + 8) % 8;
    }

    /// Animation that overrides whatever the controllers ask for (e.g. Hurt while stunned); null when none.
    public string Forced
    {
        get => forced;
        set
        {
            if (value == forced) return;
            forced = value;
            if (value != null) Play(value);
        }
    }
    string forced;

    public void Play(string newAnim)
    {
        if (forced != null && newAnim != forced) return;
        if (anim != null && newAnim == animName) return;
        animName = newAnim;
        anim = animSet != null ? animSet.Get(newAnim) : null;
        frame = 0;
        timer = 0f;
    }

    void Update()
    {
        if (anim == null || anim.durations.Length == 0) return;
        timer += Time.deltaTime * TicksPerSecond;
        for (int guard = 0; guard < anim.durations.Length && timer >= anim.durations[frame]; guard++)
        {
            timer -= anim.durations[frame];
            frame = (frame + 1) % anim.durations.Length;
        }
        spriteRenderer.sprite = animSet.GetSprite(anim, direction, frame);
    }
}

using UnityEngine;

/// Keeps the camera centred on a target, snapped to whole screen pixels so tile edges never flicker.
/// When the target jumps (the player takes control of another Pokémon) the camera slides to it instead of cutting.
[DefaultExecutionOrder(200)] // after the team object has caught up with the controlled Pokémon
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    const float JumpDistance = 1.5f; // tiles: a larger gap in one frame is a jump, not normal movement
    const float ArrivalDistance = 0.05f;

    public Transform target;
    [Tooltip("Approximate duration of the slide towards a new target, in seconds")]
    public float slideTime = 0.18f;

    static float shakeStrength, shakeUntil, shakeLength;

    /// Shakes the camera (tiles, seconds), fading out: a cave's door opening.
    public static void Shake(float strength, float time)
    {
        shakeStrength = strength;
        shakeLength = Mathf.Max(0.01f, time);
        shakeUntil = Time.time + time;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetShake() => shakeUntil = 0f;

    Camera cam;
    Vector2 position; // unsnapped camera position
    Vector2 velocity;
    bool sliding;
    bool started;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null) return;
        Vector2 goal = target.position;
        if (!started)
        {
            position = goal; // no slide from wherever the camera was placed in the scene
            started = true;
        }

        if (!sliding && Vector2.Distance(position, goal) > JumpDistance)
        {
            sliding = true;
            velocity = Vector2.zero;
        }
        if (sliding)
        {
            position = Vector2.SmoothDamp(position, goal, ref velocity, slideTime);
            if (Vector2.Distance(position, goal) < ArrivalDistance) sliding = false;
        }
        else
        {
            position = goal;
        }

        var shown = position;
        if (Time.time < shakeUntil) shown += Random.insideUnitCircle * shakeStrength * (shakeUntil - Time.time) / shakeLength;
        float unitsPerPixel = 2f * cam.orthographicSize / cam.pixelHeight;
        transform.position = new Vector3(
            Mathf.Round(shown.x / unitsPerPixel) * unitsPerPixel,
            Mathf.Round(shown.y / unitsPerPixel) * unitsPerPixel,
            transform.position.z);
    }
}

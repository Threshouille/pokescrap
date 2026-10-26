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
    static Vector2 focusPoint;
    static float focusUntil, focusSlide = 0.5f, zoom = 1f, zoomUntil;

    /// Centres the camera on a point for a while (a boss's second phase), sliding there and back over "slide"
    /// seconds (eased), then follows its target again.
    public static void Focus(Vector2 point, float time, float slide = 0.5f)
    {
        focusPoint = point;
        focusUntil = Time.time + time;
        focusSlide = slide;
    }

    /// Closes in by that factor for a while (eased), then back.
    public static void Zoom(float factor, float time)
    {
        zoom = Mathf.Max(0.1f, factor);
        zoomUntil = Time.time + time;
    }

    /// Shakes the camera (tiles, seconds), fading out: a cave's door opening.
    public static void Shake(float strength, float time)
    {
        shakeStrength = strength;
        shakeLength = Mathf.Max(0.01f, time);
        shakeUntil = Time.time + time;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetShake()
    {
        shakeUntil = 0f;
        focusUntil = 0f;
        zoomUntil = 0f;
    }

    Camera cam;
    Vector2 position; // unsnapped camera position
    Vector2 velocity;
    bool sliding, wasFocused, slowSlide;
    float baseSize;
    bool started;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null) return;
        bool focused = Time.time < focusUntil;
        Vector2 goal = focused ? focusPoint : (Vector2)target.position;
        // Zoom in and out smoothly around the base size.
        if (baseSize <= 0f) baseSize = cam.orthographicSize;
        float wanted = Time.time < zoomUntil ? baseSize / zoom : baseSize;
        cam.orthographicSize = Mathf.MoveTowards(cam.orthographicSize, wanted, baseSize * Time.deltaTime * 1.5f);
        if (focused != wasFocused)
        {
            sliding = true; // to the boss and back: a slow slide, not a cut
            velocity = Vector2.zero;
            wasFocused = focused;
            slowSlide = true;
        }
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
            position = Vector2.SmoothDamp(position, goal, ref velocity, slowSlide ? focusSlide : slideTime);
            if (Vector2.Distance(position, goal) < ArrivalDistance) sliding = slowSlide = false;
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

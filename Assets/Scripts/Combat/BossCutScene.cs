using UnityEngine;

/// A boss's second phase on screen (EnemyUltimate; items.json enemy cut*): the fight stops (Combatant.HoldStill),
/// a short freeze with a light flash; the rest of the screen darkens while the boss stays lit above it, its white
/// silhouette switching between its two forms faster and faster (a Tera: one form, in its type's colour); then the
/// reveal: a big flash, a shake, its new name in big letters, the dark fading out. The camera closes in on the point
/// between the team and the boss, or slides slowly to it when it is off screen. Visual only.
public class BossCutScene : MonoBehaviour
{
    const int DimOrder = 60, BossOrder = 61; // above everything of the arena, the boss above the dark
    const float FirstFlash = 0.3f, RevealFlash = 0.9f;

    Combatant boss;
    PmdAnimator animator;
    SpriteRenderer body, dim;
    PmdAnimSet before, after;
    Material kept;
    int keptOrder;
    Color tint;
    string title;
    float startedAt, freeze, reveal, total, darkness, nextSwap;
    bool showingBefore, revealed;
    GUIStyle titleStyle;

    /// Plays it for that boss: "before" its old form's sprites (null for a Tera), "title" shown at the reveal,
    /// "colour" the silhouette's (white for a mega).
    /// "length": seconds in all (a guardian's, shorter), the boss's by default.
    public static void Play(Combatant boss, PmdAnimSet before, string title, Color colour, float length = 0f)
    {
        var s = SpecialItems.Enemy;
        float total = length > 0f ? length : s.bossCutScene;
        if (boss == null || total <= 0f) return;
        var go = new GameObject("Cinématique du boss");
        var scene = go.AddComponent<BossCutScene>();
        scene.Begin(boss, before, title, colour, s, total);
    }

    void Begin(Combatant who, PmdAnimSet old, string name, Color colour, SpecialItems.EnemySettings s, float length)
    {
        boss = who;
        animator = who.GetComponent<PmdAnimator>();
        body = who.GetComponent<SpriteRenderer>();
        before = old;
        after = animator != null ? animator.animSet : null;
        title = name;
        tint = colour;
        total = length;
        // A shorter scene keeps its proportions.
        freeze = s.cutFreeze * length / s.bossCutScene;
        reveal = s.cutReveal * length / s.bossCutScene;
        darkness = s.cutDim;
        startedAt = Time.time;
        Combatant.HoldStill(who, total);
        // The camera: closer on the fight when the boss is in sight, a slow slide to it otherwise.
        var cam = Camera.main;
        var team = FindAnyObjectByType<PlayerController>();
        Vector2 at = who.transform.position;
        Vector2 point = at;
        if (cam != null && team != null && team.Controlled != null)
        {
            var v = cam.WorldToViewportPoint(at);
            if (v.x > 0.1f && v.x < 0.9f && v.y > 0.1f && v.y < 0.9f) point = ((Vector2)team.Controlled.transform.position + at) / 2f;
        }
        CameraFollow.Focus(point, total, s.cutSlide);
        CameraFollow.Zoom(s.cutZoom, total);
        // The dark over the arena, the boss above it.
        dim = new GameObject("Voile").AddComponent<SpriteRenderer>();
        dim.transform.SetParent(transform, false);
        dim.sprite = Shapes.Square;
        dim.color = new Color(0f, 0f, 0f, 0f);
        dim.sortingOrder = DimOrder;
        dim.transform.localScale = Vector3.one * 200f;
        if (body != null)
        {
            keptOrder = body.sortingOrder;
            kept = body.sharedMaterial;
            body.sortingOrder = BossOrder;
        }
    }

    void Update()
    {
        if (boss == null) { Finish(); return; }
        float t = Time.time - startedAt;
        if (Camera.main != null) dim.transform.position = (Vector2)Camera.main.transform.position;
        float revealAt = total - reveal;
        // The dark: comes in after the freeze, fades out during the reveal.
        float shade = t < freeze ? 0f : t < revealAt ? Mathf.Clamp01((t - freeze) / 0.25f) : 1f - Mathf.Clamp01((t - revealAt) / reveal);
        dim.color = new Color(0f, 0f, 0f, darkness * shade);
        if (t >= freeze && t < revealAt && body != null)
        {
            // The silhouette: switching between its two forms faster and faster (or pulsing, for a Tera).
            body.sharedMaterial = Evolution.Silhouette;
            float progress = Mathf.Clamp01((t - freeze) / Mathf.Max(0.01f, revealAt - freeze));
            if (Time.time >= nextSwap)
            {
                nextSwap = Time.time + Mathf.Lerp(0.22f, 0.04f, progress);
                showingBefore = !showingBefore && before != null;
                if (animator != null) animator.SetAnimSet(showingBefore ? before : after);
            }
            body.color = before != null ? Color.white : Color.Lerp(Color.white, tint, 0.5f + 0.5f * Mathf.Sin(Time.time * 20f));
        }
        if (t >= revealAt && !revealed)
        {
            revealed = true;
            Restore();
            CameraFollow.Shake(0.15f, 0.2f);
            EffectPlayer.Spawn("evolution_eclat", boss.transform.position, Vector2.right);
        }
        if (t >= total) Finish();
    }

    void Restore()
    {
        if (body != null)
        {
            body.sharedMaterial = kept;
            body.color = Color.white;
        }
        if (animator != null && after != null) animator.SetAnimSet(after);
    }

    void Finish()
    {
        Restore();
        if (body != null) body.sortingOrder = keptOrder;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (body != null && body.sortingOrder == BossOrder) body.sortingOrder = keptOrder;
    }

    // The flashes and the title.
    void OnGUI()
    {
        float t = Time.time - startedAt, revealAt = total - reveal;
        float flash = t < 0.2f ? FirstFlash * (1f - t / 0.2f) : t >= revealAt ? RevealFlash * Mathf.Clamp01(1f - (t - revealAt) / 0.35f) : 0f;
        if (flash > 0f) HudLayout.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 1f, 1f, flash));
        if (t < revealAt || string.IsNullOrEmpty(title)) return;
        float s = HudLayout.Scale, alpha = 1f - Mathf.Clamp01((t - (total - 0.15f)) / 0.15f);
        titleStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        titleStyle.fontSize = Mathf.RoundToInt(20f * s);
        var rect = new Rect(0, Screen.height * 0.32f, Screen.width, 30f * s);
        titleStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f * alpha);
        GUI.Label(new Rect(rect.x + 2f * s, rect.y + 2f * s, rect.width, rect.height), title, titleStyle);
        titleStyle.normal.textColor = new Color(1f, 0.92f, 0.5f, alpha);
        GUI.Label(rect, title, titleStyle);
    }
}

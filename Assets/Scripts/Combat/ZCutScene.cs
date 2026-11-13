using UnityEngine;

/// The Z move's cut-scene (GDD, "Objets spéciaux", mise en scène; items.json zCutScene). The player's: everybody else holds still (Combatant.HoldStill, through the strike too: strikeHold); a
/// freeze with a light flash; the pose: the screen darkens but for the user, its crystal rises above its head, rings
/// of the type's colour close in on it while it flashes, black bands slide in and the camera closes in; the title:
/// a band of the type's colour sweeps across with "CAPACITÉ Z", the move's name and the user's; the release: a flash
/// of the type's colour, the crystal bursts, and "release" is called (in FixedUpdate) for the strike.
/// An enemy's is only a warning of enemyWarn seconds: the crystal and the rings on it, nobody else stops, no veil,
/// bands nor title (its strike can be dodged: Ultimates.StrikeZ).
public class ZCutScene : MonoBehaviour
{
    public enum Phase { Freeze, Pose, Title, Done }

    const int DimOrder = 60, ActorOrder = 61, ShowOrder = 62;
    const float RingEvery = 0.14f, RingTime = 0.45f, Outro = 0.35f;

    static ZCutScene current;

    /// The player's Z cut-scene is on screen (an enemy waits for its end before its own Z).
    public static bool Playing => current != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => current = null;

    /// The phase at "t" seconds from the start (pure, for the tests).
    public static Phase PhaseAt(float t, float freeze, float pose, float title) =>
        t < freeze ? Phase.Freeze : t < freeze + pose ? Phase.Pose : t < freeze + pose + title ? Phase.Title : Phase.Done;

    Combatant actor;
    System.Action release;
    SpriteRenderer body, dim, crystal;
    Material kept;
    int keptOrder;
    Color colour;
    string moveName, userName;
    bool enemy, released;
    float startedAt, freeze, pose, title, cast, nextRing, releasedAt;
    GUIStyle smallStyle, bigStyle;

    /// Plays it for that user: "moveName" the Z move's name, "type" its crystal's, "release" called at the end.
    public static void Play(Combatant who, string moveName, int type, bool enemy, System.Action release)
    {
        if (who == null) { release?.Invoke(); return; }
        if (!enemy && current != null) current.Finish();
        var go = new GameObject(enemy ? "Avertissement Z" : "Cinématique Z");
        var scene = go.AddComponent<ZCutScene>();
        if (!enemy) current = scene;
        scene.Begin(who, moveName, type, enemy, release);
    }

    void Begin(Combatant who, string name, int type, bool foe, System.Action onRelease)
    {
        var c = SpecialItems.ZCutScene;
        actor = who;
        release = onRelease;
        moveName = name;
        userName = who.Stats != null ? who.Stats.name : who.name;
        enemy = foe;
        colour = GameData.TypeColor(type);
        freeze = foe ? 0f : c.freeze;
        pose = foe ? c.enemyWarn : c.pose;
        title = foe ? 0f : c.title;
        cast = freeze + pose + title;
        startedAt = Time.time;
        nextRing = startedAt + freeze;
        who.BeginEvolution(cast); // still and unharmed until the release
        if (!foe)
        {
            Combatant.HoldStill(who, cast + c.strikeHold);
            CameraFollow.Focus(who.transform.position, cast + Outro, 0.25f);
            CameraFollow.Zoom(c.zoom, cast);
        }

        dim = new GameObject("Voile").AddComponent<SpriteRenderer>();
        dim.transform.SetParent(transform, false);
        dim.sprite = Shapes.Square;
        dim.color = Color.clear;
        dim.sortingOrder = DimOrder;
        dim.transform.localScale = Vector3.one * 200f;

        body = who.GetComponent<SpriteRenderer>();
        if (body != null)
        {
            keptOrder = body.sortingOrder;
            kept = body.sharedMaterial;
            if (!foe) body.sortingOrder = ActorOrder; // above the veil
        }

        var aura = EffectPlayer.Spawn("evolution_aura", who.transform.position + Vector3.up * Evolution.AuraHeight, Vector2.right, who.transform, cast);
        if (aura != null)
        {
            var r = aura.GetComponent<SpriteRenderer>();
            r.sortingOrder = ShowOrder;
            r.color = colour;
        }

        var icon = SpecialItems.Icon(SpecialItems.Crystal(type)?.icon);
        crystal = new GameObject("Cristal Z").AddComponent<SpriteRenderer>();
        crystal.transform.SetParent(transform, false);
        Unlit.Apply(crystal);
        crystal.sprite = icon != null ? icon : Shapes.Circle;
        crystal.color = icon != null ? Color.white : colour;
        crystal.sortingOrder = ShowOrder;
        crystal.enabled = false;
    }

    void FixedUpdate()
    {
        if (released || actor == null) return;
        if (Time.time - startedAt < cast) return;
        released = true;
        releasedAt = Time.time;
        Restore();
        if (crystal != null)
        {
            EffectPlayer.Spawn("evolution_eclat", crystal.transform.position, Vector2.right);
            crystal.enabled = false;
        }
        release?.Invoke();
    }

    void Update()
    {
        if (actor == null && !released) { Finish(); return; }
        float t = Time.time - startedAt;
        if (Camera.main != null) dim.transform.position = (Vector2)Camera.main.transform.position;
        var c = SpecialItems.ZCutScene;
        float shade = released ? 1f - Mathf.Clamp01((Time.time - releasedAt) / Outro) : Mathf.Clamp01((t - freeze) / 0.2f);
        dim.color = new Color(0f, 0f, 0f, enemy ? 0f : c.dim * shade);
        if (released)
        {
            if (Time.time - releasedAt >= Outro) Finish();
            return;
        }
        var phase = PhaseAt(t, freeze, pose, title);
        if (phase == Phase.Freeze || actor == null) return;
        Vector2 at = actor.transform.position;
        // The crystal: rises over the head, grows, bobs, spins faster in the title.
        float grow = Mathf.Clamp01((t - freeze) / 0.3f);
        crystal.enabled = true;
        crystal.transform.position = at + Vector2.up * (0.9f + 0.6f * grow + 0.08f * Mathf.Sin(t * 6f));
        crystal.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.3f, 1f - (1f - grow) * (1f - grow));
        crystal.transform.rotation = Quaternion.Euler(0f, 0f, phase == Phase.Title ? Mathf.Sin(t * 30f) * 12f : 0f);
        // The user flashes in the type's colour, faster and faster.
        if (body != null)
        {
            float rate = Mathf.Lerp(6f, 22f, Mathf.Clamp01((t - freeze) / (pose + title)));
            bool lit = Mathf.Repeat(t * rate, 1f) < 0.35f;
            body.sharedMaterial = lit ? Evolution.Silhouette : kept;
            body.color = lit ? colour : Color.white;
        }
        // Rings closing in during the pose.
        if (phase == Phase.Pose && Time.time >= nextRing)
        {
            nextRing = Time.time + RingEvery;
ZFx.Draw(Shapes.Ring, new Color(colour.r, colour.g, colour.b, 0.9f), at, 6f, 0f, RingTime, ShowOrder).Grow(0.3f);
        }
    }

    void Restore()
    {
        if (body == null) return;
        body.sharedMaterial = kept;
        body.color = Color.white;
        body.sortingOrder = keptOrder;
    }

    void Finish()
    {
        if (!released) Restore();
        if (current == this) current = null;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (current == this) current = null;
        if (body != null && body.sortingOrder == ActorOrder) Restore();
    }

    // Flashes, black bands, the title.
    void OnGUI()
    {
        if (enemy) return; // a warning: nothing over the screen
        float t = Time.time - startedAt, w = Screen.width, h = Screen.height, s = HudLayout.Scale;
        var c = SpecialItems.ZCutScene;
        float after = released ? Time.time - releasedAt : 0f;
        // Flashes: a light one at the start, one of the type's colour at the release.
        if (t < 0.18f) HudLayout.Fill(new Rect(0, 0, w, h), new Color(1f, 1f, 1f, 0.35f * (1f - t / 0.18f)));
        if (released && after < 0.3f) HudLayout.Fill(new Rect(0, 0, w, h), new Color(Mathf.Lerp(colour.r, 1f, 0.5f), Mathf.Lerp(colour.g, 1f, 0.5f), Mathf.Lerp(colour.b, 1f, 0.5f), 0.8f * (1f - after / 0.3f)));
        // Black bands.
        float bands = released ? 1f - Mathf.Clamp01(after / Outro) : Mathf.Clamp01((t - freeze) / 0.25f);
        float band = c.bands * h * (1f - (1f - bands) * (1f - bands));
        if (band > 0.5f)
        {
            HudLayout.Fill(new Rect(0, 0, w, band), Color.black);
            HudLayout.Fill(new Rect(0, h - band, w, band), Color.black);
        }
        if (released || PhaseAt(t, freeze, pose, title) != Phase.Title) return;
        // The title: a band of the type's colour sweeping in from the left, the text after it.
        float u = Mathf.Clamp01((t - freeze - pose) / Mathf.Max(0.01f, title));
        float slide = 1f - Mathf.Pow(1f - Mathf.Clamp01(u / 0.3f), 3f);
        float fade = 1f - Mathf.Clamp01((u - 0.85f) / 0.15f);
        float y = h * 0.30f, height = 62f * s;
        var stripe = new Rect(-w + w * slide, y, w, height);
        HudLayout.Fill(stripe, new Color(colour.r * 0.55f, colour.g * 0.55f, colour.b * 0.55f, 0.92f * fade));
        HudLayout.Fill(new Rect(stripe.x, y, w, 3f * s), new Color(1f, 1f, 1f, 0.85f * fade));
        HudLayout.Fill(new Rect(stripe.x, y + height - 3f * s, w, 3f * s), new Color(1f, 1f, 1f, 0.85f * fade));
        smallStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        bigStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        smallStyle.fontSize = Mathf.RoundToInt(10f * s);
        bigStyle.fontSize = Mathf.RoundToInt(24f * s);
        float x = (1f - slide) * 40f * s; // the text arrives a little after the band
        string head = enemy ? "CAPACITÉ Z ENNEMIE" : "CAPACITÉ Z";
        Shadowed(new Rect(x, y + 2f * s, w, 16f * s), head, smallStyle, enemy ? new Color(1f, 0.45f, 0.4f, fade) : new Color(1f, 0.92f, 0.5f, fade), s);
        Shadowed(new Rect(x, y + 14f * s, w, 34f * s), moveName.ToUpperInvariant(), bigStyle, new Color(1f, 1f, 1f, fade), s);
        Shadowed(new Rect(x, y + 44f * s, w, 16f * s), userName, smallStyle, new Color(1f, 1f, 1f, 0.85f * fade), s);
    }

    static void Shadowed(Rect rect, string text, GUIStyle style, Color colour, float s)
    {
        style.normal.textColor = new Color(0f, 0f, 0f, 0.8f * colour.a);
        GUI.Label(new Rect(rect.x + 2f * s, rect.y + 2f * s, rect.width, rect.height), text, style);
        style.normal.textColor = colour;
        GUI.Label(rect, text, style);
    }
}

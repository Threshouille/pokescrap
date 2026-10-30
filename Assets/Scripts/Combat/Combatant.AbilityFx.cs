using System.Collections.Generic;
using UnityEngine;

/// What the abilities look like (visual fields of ability_effects.json): one-shot sheets when they act, looping marks
/// kept on a Pokémon while their condition holds (trapped, unnerved, guarded, mega auras), the look of its shots.
public partial class Combatant
{
    const float MarkCheckEvery = 0.2f; // seconds between two checks of the marks
    const float FxGap = 0.8f; // seconds between two plays of its own fx

    enum MarkPlace { Feet, Body, Head, Standing } // Standing: the sheet's bottom on its feet

    readonly Dictionary<string, GameObject> marks = new();
    readonly Dictionary<string, (MarkPlace place, float size)> wantedMarks = new();
    float nextMarkCheck, fxAt = -FxGap;
    bool dodgeEndPending;
    float nextWardPulse; // the light toward its wards
    float showAgainAt; // a blink: hidden until then (the arrival sheet shows it coming back)
    float cursedUntil, nextCurseFx; // Cursed Body: its move locked, the padlock shown again and again
    string curseFx;

    float SpriteTop => spriteRenderer != null && spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.max.y * 0.85f : 1f;

    // Height in its own units (its scale applies). scale: the sheet's size on screen over its own (a stretched aura
    // stands as high as it is drawn).
    float PlaceHeight(MarkPlace place, EffectLibrary.Sheet sheet, float scale = 1f) => place switch
    {
        MarkPlace.Body => SpriteTop * 0.5f,
        MarkPlace.Head => SpriteTop + 0.3f,
        MarkPlace.Standing => (sheet.frame_height / 2f - 2f) / EffectLibrary.PixelsPerUnit * scale / Mathf.Max(0.01f, transform.lossyScale.y),
        _ => 0f
    };

    /// A sheet on it, following it (size: width in tiles, its own when ≤ 0).
    GameObject PlayOn(string name, MarkPlace place, float size = 0f)
    {
        var sheet = EffectLibrary.Get(name);
        if (sheet == null) return null;
        var go = EffectPlayer.Spawn(name, transform.position, Vector2.right, transform, 0f, size);
        float scale = size > 0f ? size * EffectLibrary.PixelsPerUnit / sheet.frame_width : 1f;
        go.transform.localPosition = new Vector3(0f, PlaceHeight(place, sheet, scale), 0f);
        return go;
    }

    /// Charizard X, Manectric: its aura as wide as it is tall at least, behind it (the flames lick its outline).
    GameObject PlayAura(string name)
    {
        var sheet = EffectLibrary.Get(name);
        if (sheet == null) return null;
        float own = sheet.frame_width / (float)EffectLibrary.PixelsPerUnit;
        var go = PlayOn(name, MarkPlace.Standing, Mathf.Max(own, SpriteTop * transform.lossyScale.y * 2.4f));
        go.GetComponent<SpriteRenderer>().sortingOrder = 0; // under the Pokémon (1)
        return go;
    }

    /// Its ability's fx (or "name") played on it, not more often than every "gap" seconds.
    public void AbilityFx(string name = null, float gap = FxGap)
    {
        name ??= Ability?.fx;
        if (string.IsNullOrEmpty(name) || Time.time - fxAt < gap) return;
        fxAt = Time.time;
        PlayOn(name, MarkPlace.Body);
    }

    /// Someone's ability acting on it: above its head for a small icon, on its body otherwise.
    public void TargetFx(string name)
    {
        var sheet = EffectLibrary.Get(name);
        if (sheet != null && IsAlive) PlayOn(name, sheet.frame_width <= 16 ? MarkPlace.Head : MarkPlace.Body);
    }

    /// Cursed Body locked one of its moves for this long: the padlock over its head until it is free.
    public void Cursed(string fx, float seconds)
    {
        curseFx = fx;
        cursedUntil = Mathf.Max(cursedUntil, Time.time + seconds);
        nextCurseFx = 0f;
    }

    /// Alakazam: it vanishes where its dodge starts (the end plays in UpdateAbilityMarks).
    void DodgeFx()
    {
        var a = Ability;
        if (a == null) return;
        SpawnStanding(a.dodgeFx);
        dodgeEndPending = !string.IsNullOrEmpty(a.dodgeEndFx);
    }

    /// Alakazam: gone from here, back at the furthest free spot along "direction" within "distance" tiles (same level,
    /// no wall, no obstacle, no solid). False when there is none (it dodges as usual then).
    bool TeleportDodge(Vector2 direction, float distance)
    {
        var arena = DungeonGenerator.Current;
        Vector2 from = transform.position;
        int level = arena != null ? arena.HeightAt(from) : 0;
        for (float d = distance; d >= 0.75f; d -= 0.25f)
        {
            Vector2 to = from + direction * d;
            if (arena != null && (!arena.WalkableAt(to, 0.3f) || arena.HeightAt(to) != level)) continue;
            if (Obstacles.BlocksAt(to, this)) continue;
            bool solid = false;
            foreach (var hit in Physics2D.OverlapCircleAll(to, 0.3f))
                if (!hit.isTrigger && hit.GetComponentInParent<Combatant>() == null) { solid = true; break; }
            if (solid) continue;
            var a = Ability;
            SpawnStanding(a.dodgeFx);
            if (body != null)
            {
                body.position = to;
                body.linearVelocity = Vector2.zero;
            }
            transform.position = to;
            SpawnStanding(a.dodgeEndFx);
            if (!string.IsNullOrEmpty(a.dodgeEndFx))
            {
                ShowSelf(false);
                showAgainAt = Time.time + 0.3f;
            }
            return true;
        }
        return false;
    }

    void SpawnStanding(string name)
    {
        var sheet = EffectLibrary.Get(name);
        if (sheet != null) EffectPlayer.Spawn(name, transform.position + Vector3.up * PlaceHeight(MarkPlace.Standing, sheet) * transform.lossyScale.y, Vector2.right);
    }

    /// Lucario, Latios: what its ability adds to one of its shots, moving with it.
    public void DecorateShot(GameObject shot, MoveDef move, Vector2 direction)
    {
        var a = Ability;
        if (a == null || direction == Vector2.zero || (string.IsNullOrEmpty(a.shotFx) && string.IsNullOrEmpty(a.shotTrail))) return;
        direction = direction.normalized;
        // A shot that turns toward where it flies (Projectile) faces its direction already: its decorations follow it.
        if (!move.upright) shot.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        EffectPlayer.Spawn(a.shotFx, shot.transform.position, direction, shot.transform);
        var trail = EffectPlayer.Spawn(a.shotTrail, shot.transform.position, direction, shot.transform);
        if (trail == null) return;
        const float length = 2f; // tiles
        float scale = Mathf.Max(0.01f, shot.transform.lossyScale.x);
        trail.transform.localScale = Vector3.one / scale;
        trail.transform.position = shot.transform.position - (Vector3)(direction * (length / 2f + 0.2f));
        var r = trail.GetComponent<SpriteRenderer>();
        r.drawMode = SpriteDrawMode.Tiled;
        r.size = new Vector2(length, EffectLibrary.Get(a.shotTrail).frame_height / (float)EffectLibrary.PixelsPerUnit);
        r.sortingOrder = 2; // under the shot
    }

    bool Flies => Stats != null && (Stats.movementType == 2 || Stats.movementType == 3
        || (Stats.types != null && System.Array.IndexOf(Stats.types, 10) >= 0));

    void Want(string name, MarkPlace place, float size = 0f)
    {
        if (!string.IsNullOrEmpty(name)) wantedMarks[name] = (place, size);
    }

    /// Each physics step: the end of a dodge; every MarkCheckEvery, the looping marks it should wear.
    void UpdateAbilityMarks()
    {
        if (dodgeEndPending && Time.time >= dodgeUntil)
        {
            dodgeEndPending = false;
            SpawnStanding(Ability?.dodgeEndFx);
        }
        if (Ability is { wards: > 0 } w && !string.IsNullOrEmpty(w.wardColor) && IsAlive && Time.time >= nextWardPulse
            && ColorUtility.TryParseHtmlString(w.wardColor, out var colour))
        {
            nextWardPulse = Time.time + WardPulse.Every;
            foreach (var ward in wards)
                if (Covers(ward, 0f)) WardPulse.Launch(this, ward, colour);
        }
        if (showAgainAt > 0f && Time.time >= showAgainAt)
        {
            showAgainAt = 0f;
            ShowSelf(true);
        }
        if (Time.time < cursedUntil && Time.time >= nextCurseFx)
        {
            nextCurseFx = Time.time + 0.8f;
            TargetFx(curseFx);
        }
        if (Time.time < nextMarkCheck) return;
        nextMarkCheck = Time.time + MarkCheckEvery;

        wantedMarks.Clear();
        if (IsAlive)
        {
            Vector2 me = transform.position;
            foreach (var c in all)
            {
                var a = c.Ability;
                if (c == this || !c.IsAlive || a == null) continue;
                float d = Vector2.Distance(c.transform.position, me);
                if (c.team != team)
                {
                    if (a.trapRadius > 0f && Abilities.Traps(a, d, Stats?.types, Flies)) Want(a.trapMark, MarkPlace.Feet);
                    if (a.foesHealMul < 1f && d <= a.foesHealRadius) Want(a.foeMark, MarkPlace.Head);
                    if (a.nightmareRadius > 0f && d <= a.nightmareRadius && HasStatus("sleep")) Want(a.sleepMark, MarkPlace.Head);
                }
                else
                {
                    if (c.Covers(this, Mathf.Max(a.allyGuardRadius, a.allyStatusShieldRadius))) Want(a.allyMark, MarkPlace.Head);
                    if (a.wards > 0 && c.Covers(this, 0f)) Want(a.wardMark, MarkPlace.Feet, a.wardMarkSize); // a ward of c's
                }
            }
            var own = Ability;
            bool signature = own != null && !(string.IsNullOrEmpty(own.aura) && string.IsNullOrEmpty(own.groundAura) && string.IsNullOrEmpty(own.field));
            if (Mega != null)
            {
                Want(Abilities.Config.megaAura, MarkPlace.Feet);
                if (!signature) Want(Abilities.Config.megaAuraRise, MarkPlace.Standing); // its own aura is enough
            }
            if (own != null)
            {
                Want(own.aura, MarkPlace.Standing, -1f); // -1: PlayAura
                Want(own.groundAura, MarkPlace.Feet);
                if (own.wards > 0) Want(own.allyMark, MarkPlace.Head); // the same halo as its wards: they go together
                // Centred where ShotSlow measures from, its wavy edge (43 of the sheet's 48 pixels) on the radius.
                if (own.shotSlowRadius > 0f) Want(own.field, MarkPlace.Feet, own.shotSlowRadius * 2f * 48f / 43f);
            }
        }

        List<string> gone = null;
        foreach (var pair in marks)
            if (pair.Value == null || !wantedMarks.ContainsKey(pair.Key)) (gone ??= new List<string>()).Add(pair.Key);
        if (gone != null)
            foreach (var key in gone)
            {
                if (marks[key] != null) Destroy(marks[key]);
                marks.Remove(key);
            }
        foreach (var pair in wantedMarks)
            if (!marks.ContainsKey(pair.Key))
            {
                var go = pair.Value.size < 0f ? PlayAura(pair.Key) : PlayOn(pair.Key, pair.Value.place, pair.Value.size);
                if (go != null) marks[pair.Key] = go;
            }
    }
}

/// A small light going from a mega to one of its wards (Gardevoir, Latias, Altaria): where their protection comes from.
public class WardPulse : MonoBehaviour
{
    public const float Every = 2.5f; // seconds between two lights
    const float Travel = 0.45f, Size = 0.22f; // seconds, tiles

    Transform from, to;
    float startedAt;
    SpriteRenderer dot;
    Color colour;

    public static void Launch(Combatant from, Combatant to, Color colour)
    {
        var go = new GameObject("Lien");
        var pulse = go.AddComponent<WardPulse>();
        pulse.from = from.transform;
        pulse.to = to.transform;
        pulse.colour = colour;
        pulse.startedAt = Time.time;
        pulse.dot = go.AddComponent<SpriteRenderer>();
        Unlit.Apply(pulse.dot); // keeps its colour at night (DayNight)
        pulse.dot.sprite = Shapes.Circle;
        pulse.dot.sortingOrder = 3; // above the Pokémon
        go.transform.localScale = Vector3.one * Size;
        pulse.Update();
    }

    void Update()
    {
        if (from == null || to == null) { Destroy(gameObject); return; }
        float t = (Time.time - startedAt) / Travel;
        if (t >= 1f) { Destroy(gameObject); return; }
        // A low arc from body to body, fading in and out.
        Vector3 a = from.position + Vector3.up * 0.4f, b = to.position + Vector3.up * 0.4f;
        transform.position = Vector3.Lerp(a, b, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.5f;
        var c = colour;
        c.a = Mathf.Sin(t * Mathf.PI);
        dot.color = c;
    }
}

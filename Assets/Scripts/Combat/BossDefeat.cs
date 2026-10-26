using System.Linq;
using UnityEngine;

/// A boss's fall on screen (items.json enemy bossDefeat*): the fight stops (Combatant.HoldStill), the camera closes in
/// on it; its body turns to a blinking white silhouette that rises and dissolves into a burst (wave, shake), a title in
/// big letters; the minions still standing vanish in a puff, and the team gets bossDefeatHeal of its max HP back.
/// Called by BossAI when its boss falls. Visual, apart from the minions and the heal.
public class BossDefeat : MonoBehaviour
{
    const int GhostOrder = 61;
    const float BurstAt = 0.6f; // share of the scene when the body bursts
    const float MinionReach = 25f; // tiles: the minions around that go with it

    SpriteRenderer ghost;
    Vector2 at;
    float startedAt, total, baseScale;
    bool burst;
    string title;
    GUIStyle titleStyle;

    public static void Play(Combatant boss)
    {
        var s = SpecialItems.Enemy;
        Play(boss, s.bossDefeat, s.bossDefeatTitle, s.bossDefeatHeal);
    }

    /// A key dungeon's guardian: shorter, its own title, a smaller heal.
    public static void PlayGuardian(Combatant guardian)
    {
        var s = SpecialItems.Enemy;
        Play(guardian, s.guardianDefeat, s.guardianDefeatTitle, s.guardianDefeatHeal);
    }

    static void Play(Combatant who, float length, string title, float heal)
    {
        if (who == null || length <= 0f) return;
        new GameObject("Chute du boss").AddComponent<BossDefeat>().Begin(who, length, title, heal);
    }

    void Begin(Combatant boss, float length, string name, float heal)
    {
        var s = SpecialItems.Enemy;
        at = boss.transform.position;
        total = length;
        title = name;
        startedAt = Time.time;
        // Its last look, kept as a white silhouette (the boss itself goes at once).
        var body = boss.GetComponent<SpriteRenderer>();
        ghost = new GameObject("Silhouette").AddComponent<SpriteRenderer>();
        ghost.transform.SetParent(transform, false);
        ghost.transform.position = boss.transform.position;
        baseScale = boss.transform.lossyScale.x;
        ghost.transform.localScale = boss.transform.lossyScale;
        if (body != null)
        {
            ghost.sprite = body.sprite;
            ghost.flipX = body.flipX;
        }
        ghost.sharedMaterial = Evolution.Silhouette;
        ghost.sortingOrder = GhostOrder;
        Combatant.HoldStill(null, total);
        CameraFollow.Focus(at, total, SpecialItems.Enemy.cutSlide);
        CameraFollow.Zoom(SpecialItems.Enemy.cutZoom, total);
        // Its minions go with it.
        foreach (var c in Combatant.All.ToArray())
        {
            if (c == null || c == boss || !c.IsAlive || c.team != Team.Enemy || Vector2.Distance(c.transform.position, at) > MinionReach) continue;
            EffectPlayer.Spawn("evolution_impact", c.transform.position, Vector2.right);
            c.phaseFloorShare = 0f;
            c.TakeFixedDamage(c.hp + 1f);
        }
        // The team gets its breath back.
        var team = FindAnyObjectByType<PlayerController>();
        if (team != null)
            foreach (var m in team.Members)
                if (m != null && m.IsAlive) m.Heal(m.maxHp * heal);
    }

    void Update()
    {
        float t = (Time.time - startedAt) / Mathf.Max(0.01f, total);
        if (ghost != null)
        {
            // Blinks faster and faster while it rises, then bursts and fades.
            float rise = Mathf.Min(t, BurstAt) / BurstAt;
            ghost.transform.position = at + Vector2.up * 0.5f * rise;
            ghost.transform.localScale = Vector3.one * baseScale * (1f + 0.25f * rise);
            float blink = Mathf.Lerp(6f, 30f, rise);
            float alpha = t < BurstAt ? (Mathf.Sin(Time.time * blink) > -0.3f ? 1f : 0.35f) : 1f - (t - BurstAt) / (1f - BurstAt);
            ghost.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }
        if (t >= BurstAt && !burst)
        {
            burst = true;
            EffectPlayer.Spawn("evolution_eclat", at, Vector2.right);
            EffectPlayer.Spawn("evolution_onde", at, Vector2.right, size: 6f);
            CameraFollow.Shake(0.2f, 0.25f);
        }
        if (t >= 1f) Destroy(gameObject);
    }

    void OnGUI()
    {
        float t = (Time.time - startedAt) / Mathf.Max(0.01f, total);
        float flash = t >= BurstAt ? 0.85f * Mathf.Clamp01(1f - (t - BurstAt) / 0.2f) : 0f;
        if (flash > 0f) HudLayout.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 1f, 1f, flash));
        if (t < BurstAt || string.IsNullOrEmpty(title)) return;
        float s = HudLayout.Scale, alpha = 1f - Mathf.Clamp01((t - 0.9f) / 0.1f);
        titleStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        titleStyle.fontSize = Mathf.RoundToInt(20f * s);
        var rect = new Rect(0, Screen.height * 0.32f, Screen.width, 30f * s);
        titleStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f * alpha);
        GUI.Label(new Rect(rect.x + 2f * s, rect.y + 2f * s, rect.width, rect.height), title, titleStyle);
        titleStyle.normal.textColor = new Color(1f, 0.92f, 0.5f, alpha);
        GUI.Label(rect, title, titleStyle);
    }
}

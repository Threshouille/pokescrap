using System.Collections.Generic;
using UnityEngine;

/// A knocked-out enemy may ask to join: the earliest playable form of its evolution line is unlocked for later runs
/// (beating a Charizard unlocks Charmander). The chance is the ROM recruit rate of that form, since evolved forms
/// often have a negative one; legendaries (rate ≤ 0) never join. Only Pokémon not unlocked yet are rolled.
/// Added at runtime by EnemySpawner, which lists this run's recruits on the end screen.
public class Recruitment : MonoBehaviour
{
    const float ReferenceHeight = 360f;

    /// Pokémon unlocked during this run, in order.
    public readonly List<int> recruits = new();

    readonly System.Random rng = new();
    readonly Queue<int> pending = new();
    int shown = -1;
    float shownUntil;
    GUIStyle text;

    void OnEnable() => Combatant.AnyDied += OnDied;
    void OnDisable() => Combatant.AnyDied -= OnDied;

    void OnDied(Combatant combatant)
    {
        if (combatant.team != Team.Enemy || combatant.Stats == null) return;
        var baseForm = GameData.Pokemon(PokemonRoster.FirstPlayableForm(combatant.Stats.id));
        if (baseForm == null || SaveData.IsUnlocked(baseForm.id)) return;
        float chance = baseForm.recruitRate * SaveData.Settings.recruitMultiplier / 100f;
        if (chance <= 0f || rng.NextDouble() >= chance) return;

        SaveData.Unlock(baseForm.id);
        recruits.Add(baseForm.id);
        pending.Enqueue(baseForm.id);
    }

    void Update()
    {
        // Unscaled time: the message keeps going while the upgrade screen freezes the game.
        if (shown >= 0 && Time.unscaledTime < shownUntil) return;
        shown = pending.Count > 0 ? pending.Dequeue() : -1;
        shownUntil = Time.unscaledTime + SaveData.Settings.recruitMessageTime;
    }

    void OnGUI()
    {
        if (shown < 0) return;
        float s = Mathf.Max(1f, Mathf.Round(Screen.height / ReferenceHeight));
        text ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
        text.fontSize = Mathf.RoundToInt(8f * s);

        string name = GameData.Pokemon(shown)?.name ?? "?";
        var message = new GUIContent($"{name} veut rejoindre l'équipe !");
        float pad = 2f * s, height = 28f * s;
        float width = height + 2f * pad + text.CalcSize(message).x + 4f * s;
        var box = new Rect((Screen.width - width) / 2f, 36f * s, width, height);
        Fill(box, new Color(0.08f, 0.08f, 0.12f, 0.85f));
        var portrait = PokemonRoster.Portrait(shown);
        if (portrait != null) GUI.DrawTexture(new Rect(box.x + pad, box.y + pad, height - 2f * pad, height - 2f * pad), portrait);
        GUI.Label(new Rect(box.x + height + pad, box.y, width - height - pad, height), message, text);
    }

    static void Fill(Rect rect, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// On-screen display, bottom left: portrait and health of the Pokémon the player controls, and its four moves,
/// each greyed out while it recharges. The two allies are shown as small portraits above it.
[RequireComponent(typeof(PlayerController))]
public class TeamHud : MonoBehaviour
{
    const float ReferenceHeight = 360f; // the layout below is written for a screen this tall, then scaled up
    const float IconSize = 10f;
    const float SmallPortrait = 20f, SmallGap = 5f, SmallBar = 3f;
    const float Margin = 8f, Portrait = 40f, Gap = 6f, BarWidth = 98f, BarHeight = 9f, Slot = 22f, SlotGap = 3f;
    static readonly Color DodgeColor = new(0.45f, 0.55f, 0.65f);
    static readonly string[] PadLabels = { "R1", "R2", "L1", "L2" };
    static readonly string[] KeyLabels = { "U", "I", "O", "P" }; // shown when no gamepad is connected
    static string[] SlotLabels => UnityEngine.InputSystem.Gamepad.current != null ? PadLabels : KeyLabels;
    static readonly Color Frame = new(0.08f, 0.08f, 0.12f, 0.85f);
    static readonly Color BarBack = new(0f, 0f, 0f, 0.7f);
    static readonly Color HealthHigh = new(0.3f, 0.85f, 0.3f), HealthLow = new(0.9f, 0.25f, 0.2f);
    static readonly Color KnockedOut = new(0f, 0f, 0f, 0.65f), Poisoned = new(0.69f, 0.31f, 0.85f);
    static readonly Color Experience = new(0.35f, 0.65f, 1f);
    static readonly Color MegaFilling = new(0.45f, 0.3f, 0.6f), MegaFull = new(0.85f, 0.45f, 1f);
    static readonly Color Recharging = new(0.25f, 0.25f, 0.25f, 0.8f), Shade = new(0f, 0f, 0f, 0.55f);

    static readonly Dictionary<int, Texture2D> portraits = new();

    readonly List<Combatant.EffectIcon> effects = new();
    PlayerController team;
    Progression progression;
    Evolution evolution;
    Ultimates mega;
    GUIStyle text;

    // Statics can outlive a play session in the editor.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => portraits.Clear();

    void Awake()
    {
        team = GetComponent<PlayerController>();
        progression = GetComponent<Progression>();
        evolution = GetComponent<Evolution>();
        mega = GetComponent<Ultimates>();
    }

    static Texture2D PortraitOf(int pokemonId)
    {
        if (!portraits.TryGetValue(pokemonId, out var texture))
            portraits[pokemonId] = texture = PokemonRoster.Portrait(pokemonId);
        return texture;
    }

    void OnGUI()
    {
        var combatant = team.Controlled;
        if (combatant == null || combatant.Stats == null || team.GameOver) return;

        float s = Mathf.Max(1f, Mathf.Round(Screen.height / ReferenceHeight));
        text ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        text.fontSize = Mathf.RoundToInt(7f * s);

        // Portrait.
        var portrait = new Rect(Margin * s, Screen.height - (Margin + Portrait) * s, Portrait * s, Portrait * s);
        Fill(Grow(portrait, 2f * s), Frame);
        var picture = PortraitOf(combatant.Mega?.id ?? combatant.Stats.id);
        if (picture != null) GUI.DrawTexture(portrait, picture, ScaleMode.StretchToFill);
        else Fill(portrait, GameData.TypeColor(combatant.Stats.types.Length > 0 ? combatant.Stats.types[0] : 0));

        // Allies, in d-pad order: the left one is reached with d-pad left, the right one with d-pad right.
        var leftAlly = team.Neighbour(-1);
        var rightAlly = team.Neighbour(1);
        DrawAlly(leftAlly, 0, portrait, s);
        if (rightAlly != leftAlly) DrawAlly(rightAlly, 1, portrait, s);

        // Name and health bar.
        float x = portrait.xMax + Gap * s;
        string evolve = evolution != null && evolution.ReadyForm(combatant) != null ? "   Triangle : évoluer" : "";
        Label(new Rect(x, portrait.y - 12f * s, BarWidth * 2f * s, 10f * s), $"{combatant.Stats.name}   Niv. {combatant.level}{evolve}", TextAnchor.MiddleLeft);
        if (progression != null)
        {
            // Thin experience bar between the name and the health bar.
            var xp = new Rect(x, portrait.y, BarWidth * s, 3f * s);
            Fill(Grow(xp, s), BarBack);
            Fill(new Rect(xp.x, xp.y, xp.width * Mathf.Clamp01(progression.Xp / progression.XpToNext), xp.height), Experience);
        }
        var bar = new Rect(x, portrait.y + 6f * s, BarWidth * s, BarHeight * s);
        float ratio = combatant.maxHp > 0f ? Mathf.Clamp01(combatant.hp / combatant.maxHp) : 0f;
        Fill(Grow(bar, s), BarBack);
        Fill(new Rect(bar.x, bar.y, bar.width * ratio, bar.height), Color.Lerp(HealthLow, HealthHigh, ratio));
        Label(bar, $"{Mathf.CeilToInt(combatant.hp)}/{Mathf.CeilToInt(combatant.maxHp)}", TextAnchor.MiddleCenter);

        // Statuses and stat changes of the controlled Pokémon, to the right of its health bar.
        effects.Clear();
        combatant.GetEffects(effects);
        for (int i = 0; i < effects.Count; i++)
        {
            if (Combatant.Blinking(effects[i].remaining)) continue;
            DrawSprite(new Rect(bar.xMax + (5f + i * (IconSize + 1f)) * s, bar.y - 1f * s, IconSize * s, IconSize * s),
                Shapes.StatusIcon(effects[i].key));
        }

        // The four moves, aligned with the bottom of the portrait.
        for (int i = 0; i < Combatant.MoveSlots; i++)
        {
            var slot = new Rect(x + i * (Slot + SlotGap) * s, portrait.yMax - Slot * s, Slot * s, Slot * s);
            var move = combatant.Moves[i];
            // Button name on the top line of the frame, move name (or time left) on the bottom line.
            var top = new Rect(slot.x, slot.y + s, slot.width, 9f * s);
            var bottom = new Rect(slot.x, slot.y + 10f * s, slot.width, 11f * s);
            Fill(slot, Frame);
            if (move == null)
            {
                Label(top, SlotLabels[i], TextAnchor.MiddleCenter);
                continue;
            }

            Fill(Grow(slot, -s), GameData.TypeColor(move.type));
            float left = combatant.CooldownLeft(i);
            if (left > 0f)
            {
                // Greyed out, with a darker shade that shrinks as the move comes back.
                Fill(Grow(slot, -s), Recharging);
                float total = combatant.CooldownOf(move);
                float share = total > 0f ? Mathf.Clamp01(left / total) : 0f;
                Fill(new Rect(slot.x, slot.y, slot.width, slot.height * share), Shade);
                Label(bottom, left.ToString("0.0"), TextAnchor.MiddleCenter);
            }
            else
            {
                Label(bottom, Short(move.name), TextAnchor.MiddleCenter);
            }
            Label(top, SlotLabels[i], TextAnchor.MiddleCenter);
        }

        // Dodge, after the moves.
        {
            var slot = new Rect(x + Combatant.MoveSlots * (Slot + SlotGap) * s, portrait.yMax - Slot * s, Slot * s, Slot * s);
            var top = new Rect(slot.x, slot.y + s, slot.width, 9f * s);
            var bottom = new Rect(slot.x, slot.y + 10f * s, slot.width, 11f * s);
            Fill(slot, Frame);
            float left = combatant.DodgeCooldownLeft;
            Fill(Grow(slot, -s), left > 0f ? Recharging : DodgeColor);
            if (left > 0f)
            {
                float total = SaveData.Settings.dodgeCooldown;
                Fill(new Rect(slot.x, slot.y, slot.width, slot.height * (total > 0f ? Mathf.Clamp01(left / total) : 0f)), Shade);
            }
            Label(top, UnityEngine.InputSystem.Gamepad.current != null ? "X" : "Ctrl", TextAnchor.MiddleCenter); // ✕ on a pad
            Label(bottom, left > 0f ? left.ToString("0.0") : "Esq.", TextAnchor.MiddleCenter);
        }

        // Ultimate gauge, after the dodge: it rises as enemies fall; the time left of a mega or Terastal phase. Its
        // name is the controlled Pokémon's ultimate (Méga, Z, Téra), in its type's colour for Z and Tera.
        if (mega != null)
        {
            string kind = Ultimates.Label(combatant);
            var e = combatant.Special;
            Color full = e != null && e.family != SpecialItems.Family.Mega ? GameData.TypeColor(e.type) : MegaFull;
            var slot = new Rect(x + (Combatant.MoveSlots + 1) * (Slot + SlotGap) * s, portrait.yMax - Slot * s, Slot * s, Slot * s);
            var top = new Rect(slot.x, slot.y + s, slot.width, 9f * s);
            var bottom = new Rect(slot.x, slot.y + 10f * s, slot.width, 11f * s);
            Fill(slot, Frame);
            var inner = Grow(slot, -s);
            float timeLeft = mega.TimeLeft(combatant);
            if (timeLeft > 0f)
            {
                Fill(inner, full);
                Fill(new Rect(inner.x, inner.y, inner.width, inner.height * (1f - Mathf.Clamp01(timeLeft / mega.Length(combatant)))), Shade);
                Label(bottom, timeLeft.ToString("0"), TextAnchor.MiddleCenter);
            }
            else
            {
                Fill(inner, Recharging);
                float h = inner.height * mega.Gauge;
                Fill(new Rect(inner.x, inner.yMax - h, inner.width, h), mega.Full ? full : MegaFilling);
                Label(bottom, kind == "" ? "—" : mega.Ready ? kind + " !" : kind, TextAnchor.MiddleCenter);
            }
            if (SpecialItems.Icon(SpecialItems.IconName(e)) is { } icon)
            {
                float side = 9f * s;
                var t = icon.texture;
                var tr = icon.textureRect;
                GUI.DrawTextureWithTexCoords(new Rect(slot.xMax - side, slot.y - side * 0.6f, side, side), t,
                    new Rect(tr.x / t.width, tr.y / t.height, tr.width / t.width, tr.height / t.height));
            }
            Label(top, UnityEngine.InputSystem.Gamepad.current != null ? "L3R3" : "F", TextAnchor.MiddleCenter);
        }
    }

    void DrawAlly(Combatant ally, int index, Rect portrait, float s)
    {
        if (ally == null || ally.Stats == null) return;
        float size = SmallPortrait * s;
        var rect = new Rect(portrait.x + index * (size + SmallGap * s), portrait.y - size - (SmallBar + 8f) * s, size, size);
        Fill(Grow(rect, s), Frame);
        var picture = PortraitOf(ally.Mega?.id ?? ally.Stats.id);
        if (picture != null) GUI.DrawTexture(rect, picture, ScaleMode.StretchToFill);
        else Fill(rect, GameData.TypeColor(ally.Stats.types.Length > 0 ? ally.Stats.types[0] : 0));

        var bar = new Rect(rect.x, rect.yMax + 2f * s, size, SmallBar * s);
        Fill(Grow(bar, s), BarBack);
        if (!ally.IsAlive)
        {
            Fill(rect, KnockedOut);
            Label(rect, "K.O.", TextAnchor.MiddleCenter);
            return;
        }
        float ratio = ally.maxHp > 0f ? Mathf.Clamp01(ally.hp / ally.maxHp) : 0f;
        Fill(new Rect(bar.x, bar.y, bar.width * ratio, bar.height), Color.Lerp(HealthLow, HealthHigh, ratio));
        // Small purple corner while poisoned.
        if (ally.IsPoisoned) Fill(new Rect(rect.xMax - 5f * s, rect.y, 5f * s, 5f * s), Poisoned);
    }

    static void DrawSprite(Rect rect, Sprite sprite)
    {
        if (sprite == null) return;
        var texture = sprite.texture;
        var area = sprite.rect;
        GUI.DrawTextureWithTexCoords(rect, texture,
            new Rect(area.x / texture.width, area.y / texture.height, area.width / texture.width, area.height / texture.height));
    }

    static string Short(string name) => name.Length <= 4 ? name : name.Substring(0, 4);

    static Rect Grow(Rect rect, float amount) =>
        new(rect.x - amount, rect.y - amount, rect.width + 2f * amount, rect.height + 2f * amount);

    static void Fill(Rect rect, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    // White text with a one-pixel dark shadow, readable on any background.
    void Label(Rect rect, string content, TextAnchor anchor)
    {
        text.alignment = anchor;
        text.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), content, text);
        text.normal.textColor = Color.white;
        GUI.Label(rect, content, text);
    }
}

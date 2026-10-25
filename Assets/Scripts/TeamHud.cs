using System.Collections.Generic;
using UnityEngine;

/// On-screen display, bottom left: portrait and health of the Pokémon the player controls, and its four moves,
/// each greyed out while it recharges. The two allies are shown as small portraits above it.
[RequireComponent(typeof(PlayerController))]
public class TeamHud : MonoBehaviour
{
    const float ReferenceHeight = 360f; // the layout below is written for a screen this tall, then scaled up
    const float IconSize = 10f;
    const float SmallPortrait = 20f, SmallGap = 5f, SmallBar = 3f, HeldIcon = 9f;
    const float AllyColumn = SmallPortrait + 3f + HeldIcon; // allies left of the portrait, their held item beside them
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
        var portrait = new Rect((Margin + AllyColumn + Gap) * s, Screen.height - (Margin + Portrait) * s, Portrait * s, Portrait * s);
        Fill(Grow(portrait, 2f * s), FrameOf(combatant));
        var picture = PortraitOf(combatant.Mega?.id ?? combatant.Stats.id);
        if (picture != null) GUI.DrawTexture(portrait, picture, ScaleMode.StretchToFill);
        else Fill(portrait, GameData.TypeColor(combatant.Stats.types.Length > 0 ? combatant.Stats.types[0] : 0));

        // Allies, in d-pad order: the left one is reached with d-pad left, the right one with d-pad right.
        var leftAlly = team.Neighbour(-1);
        var rightAlly = team.Neighbour(1);
        DrawBag(s);
        DrawCard(s);
        DrawAlly(leftAlly, 0, portrait, s);
        if (rightAlly != leftAlly) DrawAlly(rightAlly, 1, portrait, s);

        // Name and health bar.
        float x = portrait.xMax + Gap * s;
        // From the top: name and level, experience, health, then its held item (icon on its tier's colour, name, what it
        // does), then the moves along the bottom of the portrait.
        string evolve = evolution != null && evolution.ReadyForm(combatant) != null ? "   Triangle : évoluer" : "";
        Label(new Rect(x, portrait.y - 25f * s, BarWidth * 2f * s, 10f * s), $"{combatant.Stats.name} · Niv. {combatant.level}{(combatant.TeraType >= 0 ? $" · Téra {GameData.TypeName(combatant.TeraType)}" : "")}{evolve}", TextAnchor.MiddleLeft);
        if (progression != null)
        {
            var xp = new Rect(x, portrait.y - 13f * s, BarWidth * s, 3f * s);
            Fill(Grow(xp, s), BarBack);
            Fill(new Rect(xp.x, xp.y, xp.width * Mathf.Clamp01(progression.Xp / progression.XpToNext), xp.height), Experience);
        }
        var bar = new Rect(x, portrait.y - 7f * s, BarWidth * s, BarHeight * s);
        float ratio = combatant.maxHp > 0f ? Mathf.Clamp01(combatant.hp / combatant.maxHp) : 0f;
        Fill(Grow(bar, s), BarBack);
        Fill(new Rect(bar.x, bar.y, bar.width * ratio, bar.height), Color.Lerp(HealthLow, HealthHigh, ratio));
        Label(bar, $"{Mathf.CeilToInt(combatant.hp)}/{Mathf.CeilToInt(combatant.maxHp)}", TextAnchor.MiddleCenter);
        if (combatant.Held != null)
        {
            var spot = new Rect(x, portrait.y + 5f * s, 10f * s, 10f * s);
            if (HeldItems.TierOf(combatant.Held.tier) is { } tier) Fill(Grow(spot, s), tier.Colour);
            Fill(spot, Frame);
            DrawSprite(spot, SpecialItems.Icon(combatant.Held.icon));
            NameAndEffect(new Rect(spot.xMax + 4f * s, spot.y, 300f * s, spot.height), combatant.Held.FullName,
                HeldItems.Describe(combatant.Held), TextAnchor.MiddleLeft);
        }

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
        float step = size + (SmallBar + 2f + SmallGap) * s;
        var rect = new Rect(Margin * s, portrait.yMax - (SmallBar + 2f) * s - size - (1 - index) * step, size, size);
        Fill(Grow(rect, ally.TeraType >= 0 ? 2f * s : s), FrameOf(ally));
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
        // Its held item, beside its portrait (on its tier's colour).
        if (ally.Held != null)
        {
            var spot = new Rect(rect.xMax + 3f * s, rect.y + (size - HeldIcon * s) / 2f, HeldIcon * s, HeldIcon * s);
            Fill(Grow(spot, s), HeldItems.TierOf(ally.Held.tier)?.Colour ?? Frame);
            Fill(spot, Frame);
            DrawSprite(spot, SpecialItems.Icon(ally.Held.icon));
        }
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

    // The team's bag, bottom right: one square per slot, the consumable's sprite (else its colour and letter), how many; the key to use
    // it above (1 to 4), or the pad's selection framed.
    void DrawBag(float s)
    {
        if (bag == null) bag = team.GetComponent<Bag>();
        if (bag == null) return;
        var slots = bag.Contents;
        bool pad = UnityEngine.InputSystem.Gamepad.current != null;
        float size = Slot * s, gap = SlotGap * s;
        for (int i = 0; i < slots.Count; i++)
        {
            var r = new Rect(Screen.width - Margin * s - (slots.Count - i) * (size + gap), Screen.height - Margin * s - size, size, size);
            if (i == bag.Selected && slots[i] != null) Fill(Grow(r, s), Color.white);
            Fill(r, Frame);
            var slot = slots[i];
            if (slot != null)
            {
                var inner = Grow(r, -2f * s);
                var icon = SpecialItems.Icon(slot.item.icon);
                if (icon != null)
                {
                    var t = icon.texture;
                    var tr = icon.textureRect;
                    GUI.DrawTextureWithTexCoords(inner, t, new Rect(tr.x / t.width, tr.y / t.height, tr.width / t.width, tr.height / t.height));
                }
                else // no sprite (setup not run): its colour and letter
                {
                    Fill(inner, slot.item.Colour);
                    Label(inner, slot.item.letter, TextAnchor.MiddleCenter);
                }
                Label(new Rect(r.x, r.yMax - 9f * s, r.width - s, 9f * s), $"{slot.count}", TextAnchor.MiddleRight);
            }
            if (!pad) Label(new Rect(r.x, r.y - 9f * s, r.width, 9f * s), $"{i + 1}", TextAnchor.MiddleCenter);
        }
        // The selected consumable (picked with the d-pad, or the last key pressed): its name and what it does.
        var chosen = bag.Selected >= 0 && bag.Selected < slots.Count ? slots[bag.Selected] : null;
        float right = Screen.width - Margin * s;
        if (chosen != null)
            NameAndEffect(new Rect(right - 300f * s, Screen.height - Margin * s - size - 20f * s, 300f * s, 9f * s),
                chosen.item.name, $"{BagItems.Describe(chosen.item)}{(pad ? "   (□)" : $"   ({bag.Selected + 1})")}", TextAnchor.MiddleRight);
    }

    Bag bag;

    // A card in the middle of the screen for a moment: what was just picked up and what it does.
    string cardTitle, cardBody;
    Sprite cardIcon;
    float cardUntil;
    const float CardTime = 3f;

    public void ShowCard(string title, string body, Sprite icon)
    {
        cardTitle = title;
        cardBody = body;
        cardIcon = icon;
        cardUntil = Time.unscaledTime + CardTime;
    }

    void DrawCard(float s)
    {
        if (Time.unscaledTime >= cardUntil || string.IsNullOrEmpty(cardTitle)) return;
        var previous = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((cardUntil - Time.unscaledTime) / 0.4f));
        var r = new Rect(Screen.width / 2f - 130f * s, Screen.height * 0.27f, 260f * s, 30f * s);
        HudLayout.Panel(r, HudLayout.PanelColour, new Color(0.35f, 0.6f, 0.95f), s);
        var icon = new Rect(r.x + 4f * s, r.y + 5f * s, 20f * s, 20f * s);
        DrawSprite(icon, cardIcon);
        Label(new Rect(icon.xMax + 5f * s, r.y + 4f * s, r.width - 34f * s, 10f * s), cardTitle, TextAnchor.MiddleLeft);
        Label(new Rect(icon.xMax + 5f * s, r.y + 15f * s, r.width - 34f * s, 10f * s), cardBody, TextAnchor.MiddleLeft, HudLayout.Secondary);
        GUI.color = previous;
    }

    // Portrait frame: the Tera type's colour while Terastallized.
    static Color FrameOf(Combatant who) => who.TeraType >= 0 ? GameData.TypeColor(who.TeraType) : Frame;

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
    void Label(Rect rect, string content, TextAnchor anchor) => Label(rect, content, anchor, Color.white);

    void Label(Rect rect, string content, TextAnchor anchor, Color colour)
    {
        text.alignment = anchor;
        text.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), content, text);
        text.normal.textColor = colour;
        GUI.Label(rect, content, text);
    }

    // An item's name in white then what it does in grey, on one line (from the left, or ending at the right edge).
    void NameAndEffect(Rect rect, string name, string effect, TextAnchor anchor)
    {
        float nameWidth = text.CalcSize(new GUIContent(name)).x, effectWidth = text.CalcSize(new GUIContent(effect)).x, gap = 6f;
        float start = anchor == TextAnchor.MiddleRight ? rect.xMax - nameWidth - gap - effectWidth : rect.x;
        Label(new Rect(start, rect.y, nameWidth + 2f, rect.height), name, TextAnchor.MiddleLeft);
        Label(new Rect(start + nameWidth + gap, rect.y, effectWidth + 2f, rect.height), effect, TextAnchor.MiddleLeft, HudLayout.Secondary);
    }
}

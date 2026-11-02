using System.Collections.Generic;
using UnityEngine;
using static HudLayout;

/// A list of every ability that does something, to give one to a dev character (the menu's TeamMenu, J in a run:
/// AbilityPickerOverlay): up / down moves, L1 / R1 (left / right) filters by family, Cross picks, Circle closes;
/// the description of the ability under the cursor below the list.
public class AbilityPicker
{
    const int Visible = 12;

    readonly List<AbilityEffect> all = new();
    List<AbilityEffect> shown;
    int cursor, filter = -1; // filter -1: every family

    public int Chosen { get; private set; }

    public AbilityPicker(int current)
    {
        all.AddRange(Abilities.All);
        all.Sort((a, b) => string.CompareOrdinal(NameOf(a), NameOf(b)));
        shown = all;
        cursor = Mathf.Max(0, shown.FindIndex(e => e.id == current));
        Chosen = current;
    }

    static string NameOf(AbilityEffect e) => AbilityLibrary.Get(e.id)?.name ?? e.id.ToString();

    /// Returns true when the list is done: picked (Chosen) or closed.
    public bool Handle(int vertical, int family, bool pick, bool close)
    {
        if (family != 0 && Abilities.Families.Count > 0) SetFilter(filter + family);
        if (vertical != 0 && shown.Count > 0) cursor = Mathf.Clamp(cursor + vertical, 0, shown.Count - 1);
        if (pick && shown.Count > 0)
        {
            Chosen = shown[cursor].id;
            return true;
        }
        return close;
    }

    void SetFilter(int index)
    {
        int count = Abilities.Families.Count + 1;
        filter = (index + 1 + count) % count - 1;
        shown = filter < 0 ? all : all.FindAll(e => Abilities.FamilyOf(e) == Abilities.Families[filter].key);
        cursor = 0;
    }

    public void Draw(Rect panel, float s, string title)
    {
        float pad = 6f * s, line = 11f * s, width = panel.width - 2f * pad, y = panel.y + pad;
        var text = Style("abilityPicker", 8f, true);
        GUI.Label(new Rect(panel.x + pad, y, width, line), title, text);
        y += line + 2f * s;
        string family = filter < 0 ? "Toutes les familles" : Abilities.Families[filter].name;
        GUI.Label(new Rect(panel.x + pad, y, width, line), $"‹ {family} ({shown.Count}) ›", text);
        y += line;
        int first = Mathf.Clamp(cursor - Visible / 2, 0, Mathf.Max(0, shown.Count - Visible));
        for (int i = first; i < shown.Count && i < first + Visible; i++)
        {
            var row = new Rect(panel.x + pad, y, width, line);
            if (i == cursor) Fill(row, new Color(1f, 0.85f, 0.2f, 0.25f));
            string mark = shown[i].id == Chosen ? "• " : "  ";
            GUI.Label(row, mark + NameOf(shown[i]), text);
            y += line;
        }
        if (shown.Count == 0) return;
        var wrap = Style("abilityPickerDesc", 7f, false, TextAnchor.UpperLeft, wrap: true);
        float top = Mathf.Max(y + 2f * s, panel.yMax - pad - 4f * line);
        Fill(new Rect(panel.x + pad, top - 2f * s, width, s), new Color(1f, 1f, 1f, 0.2f));
        var current = shown[cursor];
        GUI.Label(new Rect(panel.x + pad, top, width, panel.yMax - pad - top),
            $"{Abilities.FamilyName(Abilities.FamilyOf(current))} : {Abilities.Describe(current)}", wrap);
    }
}

/// J in a run (dev mode): the ability list over a paused game, for the controlled Pokémon. Shift + J: the next one.
public class AbilityPickerOverlay : MonoBehaviour
{
    AbilityPicker picker;
    Combatant target;
    float timeScale = 1f;
    Vector2Int heldDirection;
    float nextRepeat;
    int openedFrame;

    public bool Open => picker != null;

    public void Show(Combatant who)
    {
        if (picker != null || who == null) return;
        target = who;
        picker = new AbilityPicker(who.AbilityId);
        openedFrame = Time.frameCount;
        timeScale = Time.timeScale;
        Time.timeScale = 0f;
    }

    void Update()
    {
        if (picker == null || Time.frameCount == openedFrame) return; // the J that opened it
        var pad = UnityEngine.InputSystem.Gamepad.current;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        Vector2 v = pad != null ? pad.dpad.ReadValue() : Vector2.zero;
        int vertical = 0, family = 0;
        if (kb != null)
        {
            if (kb.upArrowKey.isPressed) v.y = 1f;
            if (kb.downArrowKey.isPressed) v.y = -1f;
            if (kb.leftArrowKey.wasPressedThisFrame) family = -1;
            if (kb.rightArrowKey.wasPressedThisFrame) family = 1;
        }
        var direction = new Vector2Int(0, v.y > 0.5f ? -1 : v.y < -0.5f ? 1 : 0);
        if (direction != heldDirection) nextRepeat = 0f;
        heldDirection = direction;
        if (direction.y != 0 && Time.unscaledTime >= nextRepeat)
        {
            nextRepeat = Time.unscaledTime + 0.15f;
            vertical = direction.y;
        }
        if (pad != null && pad.leftShoulder.wasPressedThisFrame) family = -1;
        if (pad != null && pad.rightShoulder.wasPressedThisFrame) family = 1;
        bool pick = (pad != null && pad.buttonSouth.wasPressedThisFrame) || (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame));
        bool close = (pad != null && pad.buttonEast.wasPressedThisFrame) || (kb != null && (kb.backspaceKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame));
        if (!picker.Handle(vertical, family, pick, close)) return;
        if (pick && target != null)
        {
            target.SetAbility(picker.Chosen);
            if (GameData.IsDev(target.OriginId)) SaveData.SetDevAbility(target.OriginId, picker.Chosen);
        }
        picker = null;
        Time.timeScale = timeScale;
    }

    void OnGUI()
    {
        if (picker == null) return;
        float s = Scale, width = 220f * s, height = 230f * s;
        var panel = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        HudLayout.Panel(panel, s);
        picker.Draw(panel, s, $"Talent de {target?.Stats?.name}");
    }
}

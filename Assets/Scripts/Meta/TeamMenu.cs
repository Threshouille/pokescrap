using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Menu scene: pick 3 Pokémon among the unlocked ones and their starting moves, then start a run.
/// Locked Pokémon show as silhouettes.
/// Gamepad: d-pad or left stick moves the cursor, Cross adds or removes, Triangle opens the starting moves of the
/// Pokémon under the cursor (Cross picks, Circle or Triangle closes), Options starts once the team is full.
/// Keyboard: arrows, Space, E (moves), Escape, Enter.
public class TeamMenu : MonoBehaviour
{
    const float ReferenceHeight = 360f;
    const int Columns = 6;
    const float Cell = 34f, CellGap = 4f, DeadZone = 0.5f, RepeatDelay = 0.25f;
    const float PanelWidth = 190f, PanelHeight = 214f, Line = 11f;
    const int VisibleMoves = 13;
    static readonly Color Frame = new(0.08f, 0.08f, 0.12f, 0.9f);
    static readonly Color Cursor = new(1f, 0.85f, 0.2f), Picked = new(0.3f, 0.85f, 0.3f);
    static readonly Color Silhouette = new(0f, 0f, 0f, 0.85f);

    public int teamSize = 3;

    readonly List<int> choices = new();
    readonly List<int> team = new();
    readonly Dictionary<int, Texture2D> portraits = new();
    int cursor;
    int firstRow; // first grid row on screen: the grid scrolls to keep the cursor visible
    // Starting moves screen: open for one Pokémon, with its move pool and the moves picked so far.
    bool editingMoves;
    List<MoveDef> pool;
    readonly List<int> picked = new();
    int moveCursor;
    Vector2Int heldDirection;
    float nextRepeat;
    GUIStyle text, title;

    void Start()
    {
        Time.timeScale = 1f;
        choices.AddRange(PokemonRoster.Selectable());
        foreach (int id in SaveData.LastTeam)
            if (team.Count < teamSize && SaveData.IsUnlocked(id) && choices.Contains(id) && !team.Contains(id)) team.Add(id);
        if (team.Count > 0) cursor = choices.IndexOf(team[0]);
    }

    void Update()
    {
        if (choices.Count == 0) return;
        var pad = Gamepad.current;
        var kb = Keyboard.current;

        var direction = ReadDirection(pad, kb);
        if (direction != heldDirection) nextRepeat = 0f;
        heldDirection = direction;
        bool repeat = direction != Vector2Int.zero && Time.unscaledTime >= nextRepeat;
        if (repeat) nextRepeat = Time.unscaledTime + RepeatDelay;
        bool cross = (pad != null && pad.buttonSouth.wasPressedThisFrame) || (kb != null && kb.spaceKey.wasPressedThisFrame);
        bool triangle = (pad != null && pad.buttonNorth.wasPressedThisFrame) || (kb != null && kb.eKey.wasPressedThisFrame);
        bool circle = (pad != null && pad.buttonEast.wasPressedThisFrame) || (kb != null && kb.escapeKey.wasPressedThisFrame);

        if (editingMoves)
        {
            if (repeat && direction.y != 0) moveCursor = Mathf.Clamp(moveCursor + direction.y, 0, pool.Count - 1);
            if (cross) ToggleMove(pool[moveCursor].id);
            if (triangle || circle) CloseMoves();
            return;
        }
        if (triangle && SaveData.IsUnlocked(choices[cursor]))
        {
            OpenMoves(choices[cursor]);
            return;
        }

        if (repeat)
        {
            int column = cursor % Columns + direction.x, row = cursor / Columns + direction.y;
            int next = row * Columns + Mathf.Clamp(column, 0, Columns - 1);
            if (next >= 0 && next < choices.Count) cursor = next;
        }

        if (cross) Toggle(choices[cursor]);
        if (team.Count == teamSize
            && ((pad != null && pad.startButton.wasPressedThisFrame) || (kb != null && kb.enterKey.wasPressedThisFrame)))
            Run.Start(team.ToArray());
    }

    static Vector2Int ReadDirection(Gamepad pad, Keyboard kb)
    {
        Vector2 v = Vector2.zero;
        if (pad != null)
        {
            v = pad.dpad.ReadValue();
            if (v == Vector2.zero) v = pad.leftStick.ReadValue();
        }
        if (kb != null)
        {
            if (kb.leftArrowKey.isPressed) v.x = -1f;
            if (kb.rightArrowKey.isPressed) v.x = 1f;
            if (kb.upArrowKey.isPressed) v.y = 1f;
            if (kb.downArrowKey.isPressed) v.y = -1f;
        }
        // Screen rows go down, so pushing up means the previous row.
        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y)) return Mathf.Abs(v.x) < DeadZone ? Vector2Int.zero : new Vector2Int((int)Mathf.Sign(v.x), 0);
        return Mathf.Abs(v.y) < DeadZone ? Vector2Int.zero : new Vector2Int(0, -(int)Mathf.Sign(v.y));
    }

    void Toggle(int id)
    {
        if (team.Remove(id) || !SaveData.IsUnlocked(id)) return;
        if (team.Count < teamSize) team.Add(id);
    }

    void OpenMoves(int id)
    {
        editingMoves = true;
        pool = GameData.MovePool(id);
        picked.Clear();
        picked.AddRange(SaveData.StartingMoves(id));
        moveCursor = picked.Count > 0 ? Mathf.Max(0, pool.FindIndex(m => m.id == picked[0])) : 0;
    }

    int MoveCount => Mathf.Min(SaveData.Settings.startingMoveCount, pool.Count);

    // A full selection drops its oldest move to make room, so the player never has to remove one first.
    void ToggleMove(int id)
    {
        if (picked.Remove(id)) return;
        if (picked.Count >= MoveCount) picked.RemoveAt(0);
        picked.Add(id);
    }

    // The choice is kept only when complete; otherwise the previous one stays.
    void CloseMoves()
    {
        if (picked.Count == MoveCount) SaveData.SetStartingMoves(choices[cursor], picked.ToArray());
        editingMoves = false;
    }

    Texture2D PortraitOf(int id)
    {
        if (!portraits.TryGetValue(id, out var texture))
            portraits[id] = texture = Resources.Load<Texture2D>("Portraits/" + id);
        return texture;
    }

    void OnGUI()
    {
        float s = Mathf.Max(1f, Mathf.Round(Screen.height / ReferenceHeight));
        text ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = false };
        title ??= new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
        text.fontSize = Mathf.RoundToInt(8f * s);
        title.fontSize = Mathf.RoundToInt(16f * s);

        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.05f, 0.05f, 0.08f));
        GUI.Label(new Rect(0, 8f * s, Screen.width, 24f * s), "Choisis ton équipe", title);

        int unlocked = choices.FindAll(SaveData.IsUnlocked).Count;
        float gridWidth = Columns * Cell + (Columns - 1) * CellGap;
        float panelWidth = PanelWidth;
        float left = (Screen.width - (gridWidth + 16f + panelWidth) * s) / 2f, top = 44f * s;

        // Grid of portraits: only the rows that fit above the team bar, scrolled to keep the cursor in view.
        float by = Screen.height - 70f * s;
        int visibleRows = Mathf.Max(1, Mathf.FloorToInt((by - 12f * s - top) / ((Cell + CellGap) * s)));
        int rows = (choices.Count + Columns - 1) / Columns, cursorRow = cursor / Columns;
        if (cursorRow < firstRow) firstRow = cursorRow;
        if (cursorRow >= firstRow + visibleRows) firstRow = cursorRow - visibleRows + 1;
        firstRow = Mathf.Clamp(firstRow, 0, Mathf.Max(0, rows - visibleRows));
        float gridBottom = top + visibleRows * (Cell + CellGap) * s;
        if (firstRow > 0) Label(new Rect(left, top - 10f * s, gridWidth * s, 10f * s), "^", TextAnchor.MiddleCenter);
        if (firstRow + visibleRows < rows) Label(new Rect(left, gridBottom - 2f * s, gridWidth * s, 10f * s), "v", TextAnchor.MiddleCenter);
        for (int i = firstRow * Columns; i < choices.Count && i < (firstRow + visibleRows) * Columns; i++)
        {
            int id = choices[i];
            var rect = new Rect(left + (i % Columns) * (Cell + CellGap) * s, top + (i / Columns - firstRow) * (Cell + CellGap) * s, Cell * s, Cell * s);
            if (i == cursor) Fill(Grow(rect, 2f * s), Cursor);
            else if (team.Contains(id)) Fill(Grow(rect, 2f * s), Picked);
            Fill(rect, Frame);
            if (PortraitOf(id) is { } picture)
            {
                if (SaveData.IsUnlocked(id)) GUI.DrawTexture(rect, picture);
                else
                {
                    var previous = GUI.color;
                    GUI.color = Silhouette;
                    GUI.DrawTexture(rect, picture);
                    GUI.color = previous;
                }
            }
            if (!SaveData.IsUnlocked(id)) Label(rect, "?", TextAnchor.MiddleCenter);
            int slot = team.IndexOf(id);
            if (slot >= 0) Label(new Rect(rect.x + 2f * s, rect.y, rect.width, 10f * s), (slot + 1).ToString(), TextAnchor.UpperLeft);
        }

        // Details of the Pokémon under the cursor.
        float px = left + (gridWidth + 16f) * s;
        var panel = new Rect(px, top, panelWidth * s, PanelHeight * s);
        Fill(panel, Frame);
        if (editingMoves) DrawMoves(choices[cursor], panel, s);
        else if (choices.Count > 0) DrawDetails(choices[cursor], panel, s);

        // Team and help.
        for (int i = 0; i < teamSize; i++)
        {
            var rect = new Rect(Screen.width / 2f + (i - teamSize / 2f) * 40f * s + 3f * s, by, 34f * s, 34f * s);
            Fill(rect, Frame);
            if (i < team.Count && PortraitOf(team[i]) is { } picture) GUI.DrawTexture(rect, picture);
        }
        string help = editingMoves
            ? $"Haut / bas : parcourir     Croix ou Espace : choisir ({picked.Count}/{MoveCount})     Rond, Triangle ou Échap : valider"
            : "Croix directionnelle : déplacer     Croix ou Espace : ajouter / retirer     Triangle ou E : attaques     "
              + (team.Count == teamSize ? "Options ou Entrée : lancer la partie" : $"(choisir {teamSize} Pokémon)");
        Label(new Rect(0, by + 38f * s, Screen.width, 12f * s), help, TextAnchor.MiddleCenter);
        Label(new Rect(0, by + 52f * s, Screen.width, 12f * s),
            $"Débloqués : {unlocked}/{choices.Count}     Parties : {SaveData.Runs}     Victoires : {SaveData.Wins}", TextAnchor.MiddleCenter);
    }

    void DrawDetails(int id, Rect panel, float s)
    {
        var stats = GameData.Pokemon(id);
        float pad = 6f * s, line = 12f * s;
        var portrait = new Rect(panel.x + pad, panel.y + pad, 40f * s, 40f * s);
        if (PortraitOf(id) is { } picture)
        {
            var previous = GUI.color;
            if (!SaveData.IsUnlocked(id)) GUI.color = Silhouette;
            GUI.DrawTexture(portrait, picture);
            GUI.color = previous;
        }
        float x = portrait.xMax + pad, w = panel.xMax - x - pad;
        if (!SaveData.IsUnlocked(id))
        {
            Label(new Rect(x, panel.y + pad, w, line), "???", TextAnchor.MiddleLeft);
            Label(new Rect(panel.x + pad, portrait.yMax + pad, panel.width - 2f * pad, line * 2f),
                "À recruter en le battant\n(ou une de ses évolutions)", TextAnchor.UpperLeft);
            return;
        }
        Label(new Rect(x, panel.y + pad, w, line), stats?.name ?? "?", TextAnchor.MiddleLeft);
        Label(new Rect(x, panel.y + pad + line, w, line), RoleName(GameData.RoleFromMoves(System.Array.ConvertAll(SaveData.StartingMoves(id), m => GameData.Move(m)))), TextAnchor.MiddleLeft);
        if (stats != null)
            for (int t = 0; t < stats.types.Length; t++)
                Fill(new Rect(x + t * 14f * s, panel.y + pad + 2f * line + 2f * s, 12f * s, 6f * s), GameData.TypeColor(stats.types[t]));

        float y = portrait.yMax + pad;
        Label(new Rect(panel.x + pad, y, panel.width - 2f * pad, line), "Attaques de départ :", TextAnchor.MiddleLeft);
        y += line;
        foreach (int moveId in SaveData.StartingMoves(id))
        {
            var move = GameData.Move(moveId);
            if (move == null) continue;
            Fill(new Rect(panel.x + pad, y + 3f * s, 6f * s, 6f * s), GameData.TypeColor(move.type));
            Label(new Rect(panel.x + pad + 10f * s, y, panel.width - 2f * pad, line), move.name, TextAnchor.MiddleLeft);
            y += line;
        }
        Label(new Rect(panel.x + pad, y, panel.width - 2f * pad, line), $"{GameData.MovePool(id).Count} attaques au choix", TextAnchor.MiddleLeft);
    }

    // The move pool, the picked moves ticked, and what the move under the cursor does.
    void DrawMoves(int id, Rect panel, float s)
    {
        float pad = 6f * s, line = Line * s, width = panel.width - 2f * pad;
        float y = panel.y + pad;
        Label(new Rect(panel.x + pad, y, width, line), $"{GameData.Pokemon(id)?.name} : {MoveCount} attaques de départ", TextAnchor.MiddleLeft);
        y += line + 2f * s;

        int first = Mathf.Clamp(moveCursor - VisibleMoves / 2, 0, Mathf.Max(0, pool.Count - VisibleMoves));
        for (int i = first; i < pool.Count && i < first + VisibleMoves; i++)
        {
            var move = pool[i];
            var row = new Rect(panel.x + pad, y, width, line);
            if (i == moveCursor) Fill(row, new Color(1f, 0.85f, 0.2f, 0.25f));
            int slot = picked.IndexOf(move.id);
            Label(new Rect(row.x, y, 10f * s, line), slot >= 0 ? (slot + 1).ToString() : "·", TextAnchor.MiddleCenter);
            Fill(new Rect(row.x + 12f * s, y + 3f * s, 6f * s, 6f * s), GameData.TypeColor(move.type));
            Label(new Rect(row.x + 22f * s, y, width - 22f * s, line), move.name, TextAnchor.MiddleLeft);
            y += line;
        }

        var current = pool[moveCursor];
        float infoY = panel.yMax - pad - 2f * line;
        Fill(new Rect(panel.x + pad, infoY - 2f * s, width, s), new Color(1f, 1f, 1f, 0.2f));
        Label(new Rect(panel.x + pad, infoY, width, line), MoveText.Summary(current), TextAnchor.MiddleLeft);
        Label(new Rect(panel.x + pad, infoY + line, width, line), MoveText.Effects(current), TextAnchor.MiddleLeft);
    }

    static string RoleName(string role) => role switch
    {
        "ranged" => "Distance",
        "support" => "Soutien",
        _ => "Corps à corps",
    };

    static Rect Grow(Rect rect, float amount) =>
        new(rect.x - amount, rect.y - amount, rect.width + 2f * amount, rect.height + 2f * amount);

    static void Fill(Rect rect, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    // White text with a one-pixel dark shadow.
    void Label(Rect rect, string content, TextAnchor anchor)
    {
        text.alignment = anchor;
        text.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), content, text);
        text.normal.textColor = Color.white;
        GUI.Label(rect, content, text);
    }
}

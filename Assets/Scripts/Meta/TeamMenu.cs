using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Menu scene: pick 3 Pokémon among the unlocked ones and their starting moves, then start a run.
/// Locked Pokémon show as silhouettes.
/// Gamepad: d-pad or left stick moves the cursor, Cross adds or removes, Triangle opens the starting moves of the
/// Pokémon under the cursor (Cross picks, L1 / R1 or left / right filter by type, Circle or Triangle closes), Options
/// starts once the team is full.
/// Keyboard: arrows (left / right: type filter on the moves screen), Space, E (moves), Escape, Enter.
/// Special items (SpecialItems): each portrait shows a notch per family that applies to it (M for a line with a mega,
/// Z, T), filled when an item is in that slot and framed when it is the active one; Square (O) opens the items of the
/// Pokémon under the cursor: up / down a family, left / right the items owned, Cross (Space) makes it the active one,
/// Circle, Square or Escape (O) closes.
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
    // The pool can be filtered by type: "pool" holds the moves shown, "fullPool" all of them.
    bool editingMoves;
    List<MoveDef> fullPool, pool;
    readonly List<int> filterTypes = new(); // types found in the pool; filter -1 shows every move
    int filter = -1;
    readonly List<int> picked = new();
    int moveCursor;
    // Special items screen: open for one Pokémon, one row per family that applies to it.
    bool editingItems;
    SaveData.Loadout loadout;
    readonly List<SpecialItems.Family> itemRows = new();
    int itemRow;
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

        bool square = (pad != null && pad.buttonWest.wasPressedThisFrame) || (kb != null && kb.oKey.wasPressedThisFrame);
        if (editingItems)
        {
            if (repeat && direction.y != 0) itemRow = Mathf.Clamp(itemRow + direction.y, 0, itemRows.Count - 1);
            if (repeat && direction.x != 0) CycleItem(direction.x);
            if (cross) ToggleActive();
            if (circle || square) CloseItems();
            return;
        }
        if (editingMoves)
        {
            if (repeat && direction.y != 0) moveCursor = Mathf.Clamp(moveCursor + direction.y, 0, pool.Count - 1);
            bool previous = (pad != null && pad.leftShoulder.wasPressedThisFrame) || (repeat && direction.x < 0);
            bool next = (pad != null && pad.rightShoulder.wasPressedThisFrame) || (repeat && direction.x > 0);
            if ((previous || next) && filterTypes.Count > 1) SetFilter(filter + (next ? 1 : -1));
            if (cross) ToggleMove(pool[moveCursor].id);
            if (triangle || circle) CloseMoves();
            return;
        }
        if (triangle && SaveData.IsUnlocked(choices[cursor]))
        {
            OpenMoves(choices[cursor]);
            return;
        }
        if (square && SaveData.IsUnlocked(choices[cursor]) && !GameData.IsDev(choices[cursor]))
        {
            OpenItems(choices[cursor]);
            return;
        }

        if (repeat)
        {
            int column = cursor % Columns + direction.x, row = cursor / Columns + direction.y;
            int next = row * Columns + Mathf.Clamp(column, 0, Columns - 1);
            if (next >= 0 && next < choices.Count) cursor = next;
        }

        if (cross) Toggle(choices[cursor]);
        if (circle && team.Count > 0) team.RemoveAt(team.Count - 1);
        if (team.Count > 0
            && ((pad != null && pad.startButton.wasPressedThisFrame) || (kb != null && kb.enterKey.wasPressedThisFrame)))
            Run.Start(team.ToArray());
        // Dev mode: the terrain sandbox with the same team.
        if (team.Count > 0 && SaveData.Settings.devMode
            && ((pad != null && pad.selectButton.wasPressedThisFrame) || (kb != null && kb.tKey.wasPressedThisFrame)))
            Run.Start(team.ToArray(), true);
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
        pool = fullPool = GameData.MovePool(id);
        filterTypes.Clear();
        foreach (var move in fullPool)
            if (!filterTypes.Contains(move.type)) filterTypes.Add(move.type);
        filterTypes.Sort();
        filter = -1;
        picked.Clear();
        picked.AddRange(SaveData.StartingMoves(id));
        moveCursor = picked.Count > 0 ? Mathf.Max(0, pool.FindIndex(m => m.id == picked[0])) : 0;
    }

    // -1: every move, then each type of the pool in turn (wrapping around).
    void SetFilter(int index)
    {
        int count = filterTypes.Count + 1;
        filter = (index + 1 + count) % count - 1;
        pool = filter < 0 ? fullPool : fullPool.FindAll(m => m.type == filterTypes[filter]);
        moveCursor = 0;
    }

    int MoveCount => Mathf.Min(SaveData.StartingMoveCount(choices[cursor]), fullPool.Count);

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

    // ---- Special items ----

    void OpenItems(int id)
    {
        editingItems = true;
        loadout = SaveData.LoadoutOf(id);
        itemRows.Clear();
        if (SpecialItems.HasMegaLine(id)) itemRows.Add(SpecialItems.Family.Mega);
        itemRows.Add(SpecialItems.Family.Z);
        itemRows.Add(SpecialItems.Family.Tera);
        itemRow = 0;
    }

    void CloseItems()
    {
        SaveData.SetLoadout(loadout);
        editingItems = false;
    }

    // What a family's slot may hold for this Pokémon: "" (empty) then the items owned, in order.
    List<string> Options(SpecialItems.Family family, int id)
    {
        var options = new List<string> { "" };
        switch (family)
        {
            case SpecialItems.Family.Mega:
                foreach (var m in SpecialItems.MegasOfLine(id))
                {
                    if (m.id == SpecialItems.RayquazaMega) { options.Add(SpecialItems.RayquazaKey); continue; }
                    var stone = SpecialItems.StoneOf(m.id);
                    if (stone != null && SaveData.HasStone(stone.key)) options.Add(stone.key);
                }
                break;
            case SpecialItems.Family.Z:
                foreach (var c in SpecialItems.Crystals) if (SaveData.ZLevel(c.type) > 0) options.Add(c.type.ToString());
                break;
            case SpecialItems.Family.Tera:
                foreach (var t in SpecialItems.Shards) if (SaveData.TeraLevel(t.type) > 0) options.Add(t.type.ToString());
                break;
        }
        return options;
    }

    string SlotValue(SpecialItems.Family family) => family switch
    {
        SpecialItems.Family.Mega => loadout.mega,
        SpecialItems.Family.Z => loadout.z >= 0 ? loadout.z.ToString() : "",
        _ => loadout.tera >= 0 ? loadout.tera.ToString() : "",
    };

    void CycleItem(int step)
    {
        var family = itemRows[itemRow];
        var options = Options(family, loadout.pokemonId);
        int i = Mathf.Max(0, options.IndexOf(SlotValue(family)));
        string value = options[(i + step + options.Count) % options.Count];
        switch (family)
        {
            case SpecialItems.Family.Mega: loadout.mega = value; break;
            case SpecialItems.Family.Z: loadout.z = value == "" ? -1 : int.Parse(value); break;
            default: loadout.tera = value == "" ? -1 : int.Parse(value); break;
        }
        if (value == "" && loadout.active == Key(family)) loadout.active = "";
    }

    // The row's family becomes the active one (or none again); an empty slot cannot be.
    void ToggleActive()
    {
        var family = itemRows[itemRow];
        if (SlotValue(family) == "") return;
        loadout.active = loadout.active == Key(family) ? "" : Key(family);
    }

    static string Key(SpecialItems.Family family) => family switch
    {
        SpecialItems.Family.Mega => "mega",
        SpecialItems.Family.Z => "z",
        SpecialItems.Family.Tera => "tera",
        _ => "",
    };

    static string FamilyName(SpecialItems.Family family) => family switch
    {
        SpecialItems.Family.Mega => "Méga-gemme",
        SpecialItems.Family.Z => "Cristal Z",
        _ => "Téra-Éclat",
    };

    string ItemName(SpecialItems.Family family, string value)
    {
        if (value == "") return "(vide)";
        switch (family)
        {
            case SpecialItems.Family.Mega:
                return value == SpecialItems.RayquazaKey ? "Aucune gemme (Draco Météor requis)" : SpecialItems.Stone(value)?.name ?? value;
            case SpecialItems.Family.Z:
                int z = int.Parse(value);
                return $"{SpecialItems.Crystal(z)?.name} (niv. {SaveData.ZLevel(z)})";
            default:
                int t = int.Parse(value);
                return $"{SpecialItems.Shard(t)?.name} (niv. {SaveData.TeraLevel(t)})";
        }
    }

    // A warning under an item that would not work as things stand.
    string Requirement(SpecialItems.Family family, string value, int id)
    {
        if (value == "") return "";
        var moves = System.Array.ConvertAll(SaveData.StartingMoves(id), GameData.Move);
        switch (family)
        {
            case SpecialItems.Family.Mega:
                if (value == SpecialItems.RayquazaKey) return System.Array.Exists(moves, m => m != null && m.id == SpecialItems.RayquazaMove) ? "" : "Doit connaître Draco Météor";
                var mega = GameData.Mega(SpecialItems.Stone(value)?.mega ?? 0);
                return mega != null && mega.@base != id ? $"Utile une fois {GameData.Pokemon(mega.@base)?.name}" : "";
            case SpecialItems.Family.Z:
                int z = int.Parse(value);
                return SpecialItems.KnowsMoveOfType(moves, z) ? "" : $"Doit connaître une attaque {GameData.TypeName(z)}";
            default:
                return "";
        }
    }

    void DrawItems(int id, Rect panel, float s)
    {
        float pad = 6f * s, line = Line * s, width = panel.width - 2f * pad;
        float y = panel.y + pad;
        Label(new Rect(panel.x + pad, y, width, line), $"{GameData.Pokemon(id)?.name} : objets spéciaux", TextAnchor.MiddleLeft);
        y += line + 4f * s;
        for (int r = 0; r < itemRows.Count; r++)
        {
            var family = itemRows[r];
            string value = SlotValue(family);
            var row = new Rect(panel.x + pad, y, width, line * 3f);
            if (r == itemRow) Fill(row, new Color(1f, 0.85f, 0.2f, 0.2f));
            bool isActive = loadout.active == Key(family) && value != "";
            Label(new Rect(row.x + 2f * s, y, width, line), (isActive ? "★ " : "") + FamilyName(family) + (isActive ? " (active)" : ""), TextAnchor.MiddleLeft);
            float indent = 8f * s;
            if (ItemIcon(family, value) is { } icon)
            {
                float side = 2f * line;
                DrawIcon(new Rect(row.x + indent, y + line, side, side), icon);
                indent += side + 4f * s;
            }
            Label(new Rect(row.x + indent, y + line, width - indent, line), $"‹ {ItemName(family, value)} ›", TextAnchor.MiddleLeft);
            string need = Requirement(family, value, id);
            if (need != "") Label(new Rect(row.x + indent, y + 2f * line, width - indent, line), need, TextAnchor.MiddleLeft);
            y += line * 3f + 2f * s;
        }
        float infoY = panel.yMax - pad - 3f * line;
        Fill(new Rect(panel.x + pad, infoY - 2f * s, width, s), new Color(1f, 1f, 1f, 0.2f));
        string about = itemRows[itemRow] switch
        {
            SpecialItems.Family.Mega => "Méga-évolution le temps de la jauge.",
            SpecialItems.Family.Z => "Capacité Z : la plus forte attaque du type, décuplée.",
            _ => "Téracristal : prend le type de l'éclat un moment.",
        };
        Label(new Rect(panel.x + pad, infoY, width, line), about, TextAnchor.MiddleLeft);
        Label(new Rect(panel.x + pad, infoY + line, width, line), "Un seul objet actif par partie.", TextAnchor.MiddleLeft);
    }

    static Sprite ItemIcon(SpecialItems.Family family, string value) =>
        value == "" ? null : SpecialItems.Icon(SpecialItems.IconName(family, value, int.TryParse(value, out int type) ? type : -1));

    // Draws an item's sprite in a rect, keeping its proportions.
    static void DrawIcon(Rect rect, Sprite icon)
    {
        var t = icon.texture;
        var r = icon.textureRect;
        GUI.DrawTextureWithTexCoords(rect, t, new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height));
    }

    // The notches of a portrait: one per family that applies to the Pokémon, filled when its slot holds an item,
    // framed when that item is the active one.
    void DrawNotches(int id, Rect cell, float s)
    {
        if (!SaveData.IsUnlocked(id) || GameData.IsDev(id)) return;
        var l = SaveData.LoadoutOf(id);
        var families = new List<(string letter, bool filled, bool active, Color colour, Sprite icon)>();
        if (SpecialItems.HasMegaLine(id))
            families.Add(("M", l.mega != "", l.active == "mega", new Color(0.85f, 0.45f, 1f), ItemIcon(SpecialItems.Family.Mega, l.mega)));
        families.Add(("Z", l.z >= 0, l.active == "z", l.z >= 0 ? GameData.TypeColor(l.z) : Color.gray,
            l.z >= 0 ? ItemIcon(SpecialItems.Family.Z, l.z.ToString()) : null));
        families.Add(("T", l.tera >= 0, l.active == "tera", l.tera >= 0 ? GameData.TypeColor(l.tera) : Color.gray,
            l.tera >= 0 ? ItemIcon(SpecialItems.Family.Tera, l.tera.ToString()) : null));
        float size = 9f * s;
        for (int k = 0; k < families.Count; k++)
        {
            var (letter, filled, isActive, colour, icon) = families[k];
            var r = new Rect(cell.xMax - (families.Count - k) * (size + s) - s, cell.yMax - size - s, size, size);
            if (isActive) Fill(Grow(r, s), Color.white);
            Fill(r, filled ? (icon != null ? new Color(0f, 0f, 0f, 0.6f) : colour) : new Color(0f, 0f, 0f, 0.6f));
            if (filled && icon != null)
            {
                DrawIcon(r, icon);
                continue;
            }
            text.fontSize = Mathf.RoundToInt(5f * s);
            Label(r, letter, TextAnchor.MiddleCenter);
            text.fontSize = Mathf.RoundToInt(8f * s);
        }
    }

    Texture2D PortraitOf(int id)
    {
        if (!portraits.TryGetValue(id, out var texture))
            portraits[id] = texture = PokemonRoster.Portrait(id);
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
            DrawNotches(id, rect, s);
        }

        // Details of the Pokémon under the cursor.
        float px = left + (gridWidth + 16f) * s;
        var panel = new Rect(px, top, panelWidth * s, PanelHeight * s);
        Fill(panel, Frame);
        if (editingItems) DrawItems(choices[cursor], panel, s);
        else if (editingMoves) DrawMoves(choices[cursor], panel, s);
        else if (choices.Count > 0) DrawDetails(choices[cursor], panel, s);

        // Team and help.
        for (int i = 0; i < teamSize; i++)
        {
            var rect = new Rect(Screen.width / 2f + (i - teamSize / 2f) * 40f * s + 3f * s, by, 34f * s, 34f * s);
            Fill(rect, Frame);
            if (i < team.Count && PortraitOf(team[i]) is { } picture) GUI.DrawTexture(rect, picture);
        }
        string help = editingItems
            ? "Haut / bas : famille     Gauche / droite : objet     Croix ou Espace : objet actif     Rond, Carré, Échap ou O : valider"
            : editingMoves
            ? $"Haut / bas : parcourir     Croix ou Espace : choisir ({picked.Count}/{MoveCount})     Rond, Triangle ou Échap : valider"
            : "Croix directionnelle : déplacer     Croix ou Espace : ajouter / retirer     Rond ou Échap : retirer le dernier     Triangle ou E : attaques     Carré ou O : objets     "
              + (team.Count > 0 ? "Options ou Entrée : lancer la partie" + (SaveData.Settings.devMode ? "     Select ou T : terrain de test" : "") : $"(choisir 1 à {teamSize} Pokémon)");
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
        if (filterTypes.Count > 1)
        {
            if (filter >= 0) Fill(new Rect(panel.x + pad, y + 3f * s, 6f * s, 6f * s), GameData.TypeColor(filterTypes[filter]));
            string shown = filter < 0 ? "Tous les types" : GameData.TypeName(filterTypes[filter]);
            Label(new Rect(panel.x + pad + 10f * s, y, width - 10f * s, line), $"‹ {shown} ({pool.Count}) ›", TextAnchor.MiddleLeft);
            y += line;
        }

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

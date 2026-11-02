using System.Collections.Generic;
using UnityEngine;

/// The look of the dungeon entrances (GDD « Entrées des donjons », Data/entrances.json): stairs with a rim by ground,
/// their glow, the bubble and icon shown once found, the key dungeon's dolmen, the quarters' gates. Sheets of
/// Resources/Effects (entrance_*, secret_*, key_dungeon*, quarter_gate*), played by EntranceSprite with the pivot the
/// object needs (centred on the ground, at the foot when it stands).
public static class EntranceArt
{
    public enum Icon { Down, Up, Secret, Sealed, Key, Boss, GateShut, GateOpen }

    public const int GroundOrder = -2; // over the terrain (-3), under the walls and the ground marks
    public const int StandOrder = 1; // stands like a Pokémon, sorted by its foot
    public const int BubbleOrder = 13; // above everything attached to a Pokémon
    public static readonly Vector2 Centre = new(0.5f, 0.5f), Foot = new(0.5f, 0f);

    [System.Serializable]
    public class Settings
    {
        public float bubbleHeight = 1.6f, gateBubbleHeight = 3.4f, clearingReveal = 2.5f, mapIcon = 9f;
        public string defaultRim = "grass";
    }

    static Settings data;
    static Dictionary<string, int> rims, keyMaterials;
    static Dictionary<string, string> byBiome;
    static readonly Dictionary<(string, int, Vector2), Sprite> sprites = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        data = null;
        sprites.Clear();
    }

    public static Settings Data
    {
        get
        {
            if (data != null) return data;
            var json = Resources.Load<TextAsset>("Data/entrances");
            data = json != null ? JsonUtility.FromJson<Settings>(json.text) : new Settings();
            // Plain maps: JsonUtility reads no dictionaries.
            rims = Map(json, "rims");
            keyMaterials = Map(json, "keyMaterials");
            byBiome = new Dictionary<string, string>();
            if (json != null)
                foreach (var (k, v) in Pairs(json.text, "byBiome")) byBiome[k] = v.Trim('"');
            return data;
        }
    }

    static Dictionary<string, int> Map(TextAsset json, string field)
    {
        var map = new Dictionary<string, int>();
        if (json != null)
            foreach (var (k, v) in Pairs(json.text, field))
                if (int.TryParse(v, out int n)) map[k] = n;
        return map;
    }

    // The "key": value pairs of a flat object of the file.
    static IEnumerable<(string, string)> Pairs(string text, string field)
    {
        int at = text.IndexOf($"\"{field}\"", System.StringComparison.Ordinal);
        if (at < 0) yield break;
        int open = text.IndexOf('{', at), close = text.IndexOf('}', open);
        foreach (var part in text.Substring(open + 1, close - open - 1).Split(','))
        {
            var kv = part.Split(':');
            if (kv.Length == 2) yield return (kv[0].Trim().Trim('"'), kv[1].Trim());
        }
    }

    /// The ground of a sub-biome or theme key (grass, sand, rock, volcano, ice, ruins).
    public static string GroundOf(string biome)
    {
        _ = Data;
        return biome != null && byBiome.TryGetValue(biome, out var g) ? g : data.defaultRim;
    }

    /// The frame of entrance_rim_biomes for that ground; of key_dungeon for its material.
    public static int RimOf(string biome) { _ = Data; return rims.TryGetValue(GroundOf(biome), out int n) ? n : 0; }
    public static int KeyMaterialOf(string biome) { _ = Data; return keyMaterials.TryGetValue(GroundOf(biome), out int n) ? n : 0; }

    /// A frame of a sheet with that pivot (null when the sheet is missing).
    public static Sprite Frame(string sheet, int index, Vector2 pivot)
    {
        var s = EffectLibrary.Get(sheet);
        if (s == null) return null;
        index = Mathf.Clamp(index, 0, s.frame_count - 1);
        if (sprites.TryGetValue((sheet, index, pivot), out var sprite) && sprite != null) return sprite;
        sprite = Sprite.Create(s.texture, new Rect(index * s.frame_width, 0, s.frame_width, s.frame_height), pivot,
            EffectLibrary.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprites[(sheet, index, pivot)] = sprite;
        return sprite;
    }

    /// Stairs on the ground at "at": a rim of that frame of entrance_rim_biomes (or a plain sheet), their glow.
    public static GameObject Stairs(string name, Vector2 at, string sheet, int frame = 0, bool glow = true)
    {
        var go = new GameObject(name);
        go.transform.position = at;
        EntranceSprite.Make(go.transform, Vector2.zero, Centre, GroundOrder).Still(sheet, frame);
        if (glow) EntranceSprite.Make(go.transform, Vector2.zero, Centre, 0, unlit: true).Play("entrance_glow");
        return go;
    }

    /// The bubble above an entrance, with its icon; hidden until Show(true).
    public static EntranceBubble Bubble(Transform on, Icon icon, float height) =>
        EntranceBubble.Make(on, icon, height);

    /// The icon's sprite (entrance_icons), for the mini-map.
    public static Sprite IconSprite(Icon icon) => Frame("entrance_icons", (int)icon, Centre);
}

/// One sheet of an entrance played on its own renderer: a still frame, a loop, or once then another sheet.
public class EntranceSprite : MonoBehaviour
{
    SpriteRenderer body;
    Vector2 pivot;
    string sheet, then;
    bool loop, playing;
    int frame;
    float timer;
    System.Action done;

    public SpriteRenderer Renderer => body;
    public string Sheet => sheet;

    public static EntranceSprite Make(Transform parent, Vector2 local, Vector2 pivot, int order, bool unlit = false)
    {
        var go = new GameObject("Entrée");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        var s = go.AddComponent<EntranceSprite>();
        s.body = go.AddComponent<SpriteRenderer>();
        s.body.sortingOrder = order;
        if (order == EntranceArt.StandOrder) s.body.spriteSortPoint = SpriteSortPoint.Pivot;
        if (unlit) Unlit.Apply(s.body);
        s.pivot = pivot;
        return s;
    }

    /// A still frame.
    public EntranceSprite Still(string name, int index = 0)
    {
        sheet = name;
        playing = false;
        frame = index;
        body.sprite = EntranceArt.Frame(name, index, pivot);
        return this;
    }

    /// Plays a sheet: in a loop, or once (then "next" in a loop, or "finished" called, the last frame kept).
    public EntranceSprite Play(string name, bool repeat = true, string next = null, System.Action finished = null)
    {
        sheet = name;
        loop = repeat;
        then = next;
        done = finished;
        playing = true;
        frame = 0;
        timer = 0f;
        body.sprite = EntranceArt.Frame(name, 0, pivot);
        return this;
    }

    void Update()
    {
        if (!playing) return;
        var s = EffectLibrary.Get(sheet);
        if (s == null) { playing = false; return; }
        timer += Time.deltaTime * 1000f;
        for (int guard = 0; guard < s.frame_count && timer >= Duration(s, frame); guard++)
        {
            timer -= Duration(s, frame);
            if (frame + 1 < s.frame_count) { frame++; continue; }
            if (loop) { frame = 0; continue; }
            playing = false;
            var finished = done;
            done = null;
            if (then != null) Play(then);
            finished?.Invoke();
            return;
        }
        body.sprite = EntranceArt.Frame(sheet, frame, pivot);
    }

    static float Duration(EffectLibrary.Sheet s, int index)
    {
        var d = s.frame_duration_ms;
        return d != null && index < d.Length && d[index] > 0 ? d[index] : 100f;
    }
}

/// The bubble floating above an entrance once found, its icon inside.
public class EntranceBubble : MonoBehaviour
{
    EntranceSprite bubble;
    SpriteRenderer icon;
    float height;
    public EntranceArt.Icon Kind { get; private set; }

    public static EntranceBubble Make(Transform on, EntranceArt.Icon kind, float height)
    {
        var go = new GameObject("Bulle");
        go.transform.SetParent(on, false);
        go.transform.localPosition = new Vector2(0f, height);
        var b = go.AddComponent<EntranceBubble>();
        b.height = height;
        b.bubble = EntranceSprite.Make(go.transform, Vector2.zero, EntranceArt.Centre, EntranceArt.BubbleOrder, unlit: true);
        b.bubble.Still("entrance_bubble");
        var i = new GameObject("Icône");
        i.transform.SetParent(go.transform, false);
        i.transform.localPosition = new Vector2(0f, 1f / 24f); // in the bubble, above its point
        b.icon = i.AddComponent<SpriteRenderer>();
        b.icon.sortingOrder = EntranceArt.BubbleOrder + 1;
        Unlit.Apply(b.icon);
        b.SetIcon(kind);
        go.SetActive(false);
        return b;
    }

    public void SetIcon(EntranceArt.Icon kind)
    {
        Kind = kind;
        if (icon != null) icon.sprite = EntranceArt.IconSprite(kind);
    }

    public void Show(bool shown)
    {
        if (gameObject.activeSelf != shown) gameObject.SetActive(shown);
    }

    // Floats a pixel up and down, its icon with it.
    void Update() => transform.localPosition = new Vector2(0f, height + (Mathf.Sin(Time.time * 4f) > 0f ? 1f : 0f) / 24f);
}

/// A gate between two quarters of the island (IslandRun's passage ends): a stone arch with a Time Gear socket, barred by
/// a grey veil until the quarter's gear is found, then a turquoise swirl (it opens before the team's eyes the first
/// time it shows open); it pushes back a team without the gear. Its bubble shows once its tile is seen.
public class QuarterGate : MonoBehaviour
{
    EntranceSprite arch;
    EntranceBubble bubble;
    bool shown, open;

    public bool Open => open;

    public static QuarterGate Make(string name, Vector2 at)
    {
        var go = new GameObject(name);
        go.transform.position = at;
        var gate = go.AddComponent<QuarterGate>();
        // Its foot at the bottom of its tile, the way through on the tile.
        gate.arch = EntranceSprite.Make(go.transform, new Vector2(0f, -0.5f), EntranceArt.Foot, EntranceArt.StandOrder).Still("quarter_gate");
        gate.bubble = EntranceArt.Bubble(go.transform, EntranceArt.Icon.GateShut, EntranceArt.Data.gateBubbleHeight);
        return gate;
    }

    /// Shut or open; "seen": its tile is revealed (its bubble shows). Opening plays its sheet the first time.
    public void Set(bool isOpen, bool seen)
    {
        bubble.Show(seen);
        if (shown && isOpen == open) return;
        bool opening = shown && isOpen && !open;
        shown = true;
        open = isOpen;
        bubble.SetIcon(open ? EntranceArt.Icon.GateOpen : EntranceArt.Icon.GateShut);
        if (!open) arch.Still("quarter_gate");
        else if (opening) arch.Play("quarter_gate_opening", false, "quarter_gate_open");
        else arch.Play("quarter_gate_open");
    }

    /// The veil pushes the team back (no gear).
    public void Refuse()
    {
        if (!open) arch.Play("quarter_gate_refuse", false, null, () => { if (!open) arch.Still("quarter_gate"); });
    }
}

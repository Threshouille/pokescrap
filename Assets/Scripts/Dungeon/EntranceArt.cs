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
        public bool rampart; // the island's other zones drawn as the rampart (RampartTiles), its passages as its gates
        public int rampartDepth = 12; // tiles of rampart drawn around a zone (the rest of the map is not laid)
        // Opening a gate (QuarterGate.Unlock): tiles from the dolmen to set the gear in; seconds the gear flies; veins
        // lit along the rampart within veinRadius tiles of the gate, veinStep seconds later per tile away.
        public float gateReach = 2.5f, gateFlyTime = 0.8f, veinRadius = 8f, veinStep = 0.05f;
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

/// A gate between two zones of the island (IslandRun's passage ends): in the rampart that hides the other zones, its
/// gate (rampart_gate*, seen from the face, the back or a side) and a dolmen whose socket takes the quarter's Time Gear
/// (the centre's: four sockets); without the rampart, a stone arch with a grey veil. It opens before the team's eyes
/// the first time it shows open (the gear set, then the gate); its bubble shows once its tile is seen.
public class QuarterGate : MonoBehaviour
{
    // How the rampart is seen from the passage end: its face to the north, its back to the south, a side.
    enum Look { Arch, Face, Back, Side }

    EntranceSprite arch, dolmen;
    EntranceBubble bubble;
    Look look;
    bool shown, open, quad;
    int gears = -1;
    Vector2Int cell, dir; // the passage end's tile (grid) and the way to the rampart
    int reach; // tiles from it to the rampart
    Transform veins;
    float deniedAt = -10f;

    public bool Open => open;
    // What it shows (checks, IslandCheck.RampartShots and tests).
    public string Seen => look.ToString();
    public string GateShown => arch.Sheet;
    public string DolmenShown => dolmen != null ? dolmen.Sheet : null;
    public bool Lit => veins != null;

    /// Where the gear is set in (the team comes near it to open the gate).
    public Vector2 DolmenAt => dolmen != null ? (Vector2)dolmen.transform.position : (Vector2)transform.position;

    /// The opening is being played (Unlock).
    public bool Unlocking { get; private set; }

    /// Whether a point stands in the gateway: anywhere across its width (3 tiles, 2 for a side gate), from "reach"
    /// tiles before the passage end up to the rampart; around the passage end for an arch.
    public bool InGateway(Vector2 point, float reach)
    {
        Vector2 rel = point - (Vector2)transform.position;
        if (look == Look.Arch) return rel.magnitude <= reach;
        var along = new Vector2(Mathf.Abs(dir.y), Mathf.Abs(dir.x));
        float side = Mathf.Abs(Vector2.Dot(rel, along)), across = Vector2.Dot(rel, (Vector2)dir);
        return side <= (look == Look.Side ? 1f : 1.5f) && across >= -reach && across <= reach + 0.5f;
    }

    /// A passage end at "at" (the centre of its tile). Next to the rampart (EntranceArt.Settings.rampart), the
    /// rampart's gate where it is and, when the way asks for a gear ("gated"), a dolmen in front (towards the centre,
    /// "toCentre": with 4 sockets); else the quarter's arch. The passage end moves to the tile in front of the gate.
    public static QuarterGate Make(string name, Vector2 at, bool gated = true, bool toCentre = false)
    {
        var go = new GameObject(name);
        go.transform.position = at;
        var gate = go.AddComponent<QuarterGate>();
        gate.quad = gated && toCentre;
        var arena = DungeonGenerator.Current;
        var cell = arena != null ? arena.CellOf(at) : Vector2Int.zero;
        // The nearest rampart in a straight line (made straight there, DungeonGenerator.StraightenAtPassages).
        var dir = Vector2Int.zero;
        int k = 0;
        if (arena != null) arena.RampartWay(cell, out dir, out k);
        // The way in starts right in front of the gate: walk there over open ground.
        while (dir != Vector2Int.zero && k > 1 && arena.Walkable(cell.x + dir.x, cell.y + dir.y))
        {
            cell += dir;
            k--;
        }
        if (arena != null) go.transform.position = arena.CentreOf(cell.x, cell.y);
        gate.cell = cell;
        gate.dir = dir;
        gate.reach = k;
        gate.look = dir == Vector2Int.up ? Look.Face : dir == Vector2Int.down ? Look.Back : dir != Vector2Int.zero ? Look.Side : Look.Arch;
        var t = go.transform;
        switch (gate.look)
        {
            case Look.Face: // the face's tile, the gate in it
                gate.arch = EntranceSprite.Make(t, new Vector2(0f, k), EntranceArt.Centre, 0);
                break;
            case Look.Back: // the rampart's edge, the gate going down into it
                gate.arch = EntranceSprite.Make(t, new Vector2(0f, 0.5f - k), new Vector2(0.5f, 1f), 0);
                break;
            case Look.Side: // an alcove in the rampart's side (drawn for the rampart to the east)
                gate.arch = EntranceSprite.Make(t, new Vector2(dir.x * (k - 0.5f), 0f), new Vector2(0f, 0.5f), 0);
                gate.arch.Renderer.flipX = dir.x < 0;
                break;
            default: // its foot at the bottom of its tile, the way through on the tile
                gate.arch = EntranceSprite.Make(t, new Vector2(0f, -0.5f), EntranceArt.Foot, EntranceArt.StandOrder);
                break;
        }
        // The dolmen on the ground in front of the gate, beside it.
        if (gate.look != Look.Arch && gated)
        {
            var spot = gate.look switch
            {
                Look.Face => new Vector2(gate.quad ? -3f : -2.5f, k - 1.5f),
                Look.Back => new Vector2(gate.quad ? -3f : -2.5f, 0.5f - k),
                _ => new Vector2(dir.x * (k - (gate.quad ? 2f : 1.5f)), -2.5f),
            };
            gate.dolmen = EntranceSprite.Make(t, spot, EntranceArt.Foot, EntranceArt.StandOrder);
            // Its plinth stops the Pokémon (the stele stands on it).
            var plinth = gate.dolmen.gameObject.AddComponent<BoxCollider2D>();
            plinth.size = new Vector2(PlinthWidth(gate.quad), 0.4f);
            plinth.offset = new Vector2(0f, 0.25f);
        }
        gate.Shut();
        gate.bubble = EntranceArt.Bubble(t, EntranceArt.Icon.GateShut, EntranceArt.Data.gateBubbleHeight);
        return gate;
    }

    static float PlinthWidth(bool quad) => quad ? 2.5f : 1.5f; // tiles: the dolmen's plinth

    string GateSheet => look switch { Look.Face => "rampart_gate", Look.Back => "rampart_gate_back", Look.Side => "rampart_gate_side", _ => "quarter_gate" };

    void Shut()
    {
        arch.Still(GateSheet);
        if (dolmen == null) return;
        if (quad) dolmen.Still("gear_dolmen_quad", Mathf.Clamp(gears, 0, 4));
        else dolmen.Still("gear_dolmen");
    }

    /// Shut or open; "seen": its tile is revealed (its bubble shows); "found": Time Gears found so far (the centre's
    /// dolmen shows them). Opening plays its sheets the first time (the gear set in the dolmen, then the gate).
    public void Set(bool isOpen, bool seen, int found = 0)
    {
        bubble.Show(seen);
        bool more = quad && found != gears;
        gears = found;
        if (shown && isOpen == open)
        {
            if (more && !open) Shut();
            return;
        }
        bool opening = shown && isOpen && !open;
        shown = true;
        open = isOpen;
        bubble.SetIcon(open ? EntranceArt.Icon.GateOpen : EntranceArt.Icon.GateShut);
        string sheet = GateSheet;
        if (!open) Shut();
        else if (look == Look.Arch)
        {
            if (opening) arch.Play("quarter_gate_opening", false, "quarter_gate_open");
            else arch.Play("quarter_gate_open");
        }
        else if (opening && dolmen != null)
        {
            System.Action swing = () => { Light(true); arch.Play(sheet + "_opening", false, sheet + "_open"); };
            if (quad) { dolmen.Play("gear_dolmen_quad_active"); swing(); }
            else dolmen.Play("gear_dolmen_insert", false, "gear_dolmen_active", swing);
        }
        else
        {
            arch.Play(sheet + "_open");
            if (dolmen != null)
            {
                dolmen.Play(quad ? "gear_dolmen_quad_active" : "gear_dolmen_active");
                Light(false);
            }
        }
    }

    /// The team came to the dolmen with the gear: it flies into its socket, the veins of the rampart light up, the
    /// gate swings open; everyone stands still meanwhile, the camera on the gate.
    public void Unlock(Combatant leader, string towards)
    {
        if (Unlocking || (shown && open)) return;
        Unlocking = true;
        shown = open = true;
        bubble.SetIcon(EntranceArt.Icon.GateOpen);
        StartCoroutine(Opening(leader, towards));
    }

    System.Collections.IEnumerator Opening(Combatant leader, string towards)
    {
        var s = EntranceArt.Data;
        const float Settle = 0.5f, Swing = 0.8f, After = 0.6f; // seconds: veins spreading, the gate opening, then
        float insert = dolmen != null && !quad ? 0.8f : 0.3f;
        float total = s.gateFlyTime + insert + Settle + Swing + After;
        Combatant.HoldStill(null, total);
        Vector2 socket = DolmenAt + new Vector2(0f, quad ? 1.6f : 1.45f);
        CameraFollow.Focus(Vector2.Lerp(socket, arch.transform.position, 0.5f), total, 0.4f);
        // The gear flies from the leader into the socket, spinning, in an arc.
        var gear = new GameObject("Engrenage du Temps");
        var r = gear.AddComponent<SpriteRenderer>();
        r.sprite = RomArt.Get("GroundObjects/time_gear") ?? Shapes.Gear;
        r.sortingOrder = EntranceArt.BubbleOrder;
        Unlit.Apply(r);
        Vector2 from = leader != null ? (Vector2)leader.transform.position + Vector2.up : socket + Vector2.up * 2f;
        for (float t = 0f; t < s.gateFlyTime; t += Time.deltaTime)
        {
            float u = t / s.gateFlyTime;
            gear.transform.position = Vector2.Lerp(from, socket, u) + Vector2.up * (Mathf.Sin(u * Mathf.PI) * 1.5f);
            gear.transform.rotation = Quaternion.Euler(0f, 0f, -540f * u);
            gear.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, u);
            yield return null;
        }
        Destroy(gear);
        // Set in its socket.
        bool set = false;
        if (dolmen == null) set = true;
        else if (quad)
        {
            dolmen.Play("gear_dolmen_quad_active");
            EffectPlayer.Spawn("evolution_eclat", socket, Vector2.right);
            set = true;
        }
        else dolmen.Play("gear_dolmen_insert", false, "gear_dolmen_active", () => set = true);
        while (!set) yield return null;
        Light(true);
        yield return new WaitForSeconds(Settle);
        CameraFollow.Shake(0.12f, 0.7f);
        bool swung = false;
        arch.Play(GateSheet + "_opening", false, GateSheet + "_open", () => swung = true);
        while (!swung) yield return null;
        var glow = PointLights.Attach(arch.gameObject, "caveMouth");
        if (glow != null) PointLights.Want(glow, true);
        FindAnyObjectByType<Progression>()?.Announce($"Le passage vers {towards} est ouvert", Notice.Loot);
        Unlocking = false;
    }

    /// The team came to the dolmen without the gear: a mark over the empty socket (not too often).
    public void Deny()
    {
        if (open || dolmen == null || Time.time - deniedAt < 2.5f) return;
        deniedAt = Time.time;
        EffectPlayer.Spawn("seal_no_effect", DolmenAt + new Vector2(0f, quad ? 1.6f : 1.45f), Vector2.right);
    }

    // The veins of the rampart's faces near the gate, lit (rampart_face_lit), spreading from it or all at once.
    void Light(bool spread)
    {
        var arena = DungeonGenerator.Current;
        if (veins != null || arena == null || look == Look.Arch) return;
        veins = new GameObject("Veines allumées").transform;
        veins.SetParent(transform, false);
        var s = EntranceArt.Data;
        var gateTile = cell + dir * reach;
        int radius = Mathf.CeilToInt(s.veinRadius);
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                var c = gateTile + new Vector2Int(dx, dy);
                int face = arena.RampartFaceAt(c);
                if (face < 0 || dx * dx + dy * dy > s.veinRadius * s.veinRadius) continue;
                if (look == Look.Face && dy == 0 && Mathf.Abs(dx) <= 1) continue; // the gate's own tiles
                var go = new GameObject("Veine");
                go.transform.SetParent(veins, false);
                go.transform.position = arena.CentreOf(c.x, c.y);
                var vein = go.AddComponent<RampartVein>();
                vein.Begin(face, spread ? Mathf.Sqrt(dx * dx + dy * dy) * s.veinStep : 0f);
            }
    }

    /// The veil pushes the team back (no gear).
    public void Refuse()
    {
        if (!open && look == Look.Arch) arch.Play("quarter_gate_refuse", false, null, () => { if (!open) arch.Still("quarter_gate"); });
    }
}

/// A face of the rampart with its veins lit (rampart_face_lit: left end, middle, right end), its light running along.
public class RampartVein : MonoBehaviour
{
    static readonly Dictionary<(int frame, int column), Sprite> sprites = new();
    SpriteRenderer body;
    int column;
    float start;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => sprites.Clear();

    /// Face kind (DungeonGenerator.RampartFaceAt), lit after that many seconds.
    public void Begin(int face, float delay)
    {
        column = face == 0 ? 0 : face == 2 ? 2 : 1;
        start = Time.time + delay;
        body = gameObject.AddComponent<SpriteRenderer>();
        body.sortingOrder = 0; // over the walls (-1), under the Pokémon
        body.enabled = false;
    }

    void Update()
    {
        var sheet = EffectLibrary.Get("rampart_face_lit");
        if (sheet == null || Time.time < start) return;
        body.enabled = true;
        float ms = sheet.frame_duration_ms != null && sheet.frame_duration_ms.Length > 0 && sheet.frame_duration_ms[0] > 0 ? sheet.frame_duration_ms[0] : 120f;
        int frame = (int)((Time.time - start) * 1000f / ms) % sheet.frame_count;
        if (!sprites.TryGetValue((frame, column), out var sprite) || sprite == null)
        {
            sprite = Sprite.Create(sheet.texture, new Rect(frame * sheet.frame_width + column * 24, 0, 24, 24), new Vector2(0.5f, 0.5f),
                EffectLibrary.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprites[(frame, column)] = sprite;
        }
        body.sprite = sprite;
    }
}

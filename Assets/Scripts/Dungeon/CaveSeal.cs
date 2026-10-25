using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// The sealed cave of the island (IslandPlan.sealedCave; GDD, "Grottes scellées"), added by IslandRun: draws its
/// mechanism (a stand topped by a stone in the colour of what opens it: types, weathers, night), shows its hint
/// nearby and opens the door (IslandPlan.OpenCave, DungeonGenerator.RedrawIsland) once its condition is met:
/// for now an attack of one of its types on the mechanism ('stages' hits), one of its weathers, the night; K in
/// devMode opens it at once.
public class CaveSeal : MonoBehaviour
{
    const float HintRange = 3.5f, WeatherRange = 10f, HitMargin = 0.7f; // tiles
    const float FlashTime = 0.25f, CycleTime = 1f; // seconds

    IslandRun island;
    PlayerController team;
    DungeonGenerator arena;
    CaveSeals.Seal seal;
    GameObject mechanism;
    SpriteRenderer stone, glow;
    int hits;
    float flashUntil;
    Color flash;

    IslandPlan Plan => island != null ? island.Plan : null;

    /// The cave is in that zone and still shut.
    public bool Waiting(int inZone) =>
        Plan != null && Plan.sealedCave.x >= 0 && !Plan.caveOpen
        && Plan.region[Plan.Index(Plan.sealedCave.x, Plan.sealedCave.y)] == inZone;

    void Awake()
    {
        island = GetComponent<IslandRun>();
        island.Built += OnBuilt;
        Obstacles.Hit += OnHit;
    }

    void OnDestroy()
    {
        if (island != null) island.Built -= OnBuilt;
        Obstacles.Hit -= OnHit;
        Clear();
    }

    /// Forgets the mechanism (the island is left, or rebuilt).
    public void Clear()
    {
        if (mechanism != null) Destroy(mechanism);
        mechanism = null;
        seal = null;
    }

    void OnBuilt()
    {
        Clear();
        team = FindAnyObjectByType<PlayerController>();
        arena = DungeonGenerator.Current;
        var plan = Plan;
        if (plan == null || plan.sealedCave.x < 0 || plan.region[plan.Index(plan.caveMechanism.x, plan.caveMechanism.y)] != island.Zone) return;
        seal = CaveSeals.Get(plan.caveSeal);
        hits = 0;
        mechanism = new GameObject("Mécanisme de la grotte");
        mechanism.transform.position = island.Cell(plan.caveMechanism);
        mechanism.AddComponent<SortingGroup>().sortingOrder = 1; // the Pokémon's, sorted by its foot
        var stand = Part("Socle", Shapes.Square, new Color(0.35f, 0.3f, 0.27f), new Vector2(0f, -0.1f), new Vector2(0.8f, 0.55f), 0);
        stand.spriteSortPoint = SpriteSortPoint.Pivot;
        Part("Bord", Shapes.Square, new Color(0.5f, 0.45f, 0.4f), new Vector2(0f, 0.18f), new Vector2(0.9f, 0.12f), 1);
        glow = Part("Lueur", Shapes.Circle, Color.clear, new Vector2(0f, 0.42f), Vector2.one * 0.75f, 2);
        stone = Part("Pierre", Shapes.Circle, Color.gray, new Vector2(0f, 0.42f), Vector2.one * 0.4f, 3);
    }

    SpriteRenderer Part(string name, Sprite sprite, Color colour, Vector2 offset, Vector2 scale, int order)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.transform.SetParent(mechanism.transform, false);
        r.transform.localPosition = offset;
        r.transform.localScale = scale;
        r.sprite = sprite;
        r.color = colour;
        r.sortingOrder = order;
        return r;
    }

    // The colours of what opens it (types, weathers, night), in turn.
    List<Color> Colours()
    {
        var list = new List<Color>();
        if (seal == null) return list;
        foreach (int type in seal.types) list.Add(GameData.TypeColor(type));
        foreach (var w in seal.weathers)
            list.Add(w switch
            {
                "sun" => new Color(1f, 0.8f, 0.25f),
                "rain" => new Color(0.3f, 0.5f, 1f),
                "sand" => new Color(0.85f, 0.7f, 0.4f),
                "hail" => new Color(0.8f, 0.92f, 1f),
                _ => Color.white,
            });
        if (seal.night) list.Add(new Color(0.4f, 0.4f, 0.8f));
        return list;
    }

    void Update()
    {
        if (mechanism == null || stone == null) return;
        bool open = Plan != null && Plan.caveOpen;
        var colours = Colours();
        var colour = colours.Count > 0 ? colours[(int)(Time.time / CycleTime) % colours.Count] : Color.gray;
        if (Time.time < flashUntil) colour = flash;
        stone.color = open ? Color.Lerp(colour, Color.white, 0.3f) : Color.Lerp(colour, Color.black, 0.25f);
        // A glow that grows with each hit, steady once open.
        float share = seal != null && seal.stages > 1 ? (float)hits / seal.stages : 0f;
        float pulse = open ? 0.55f : share * (0.35f + 0.15f * Mathf.Sin(Time.time * 6f));
        glow.color = new Color(colour.r, colour.g, colour.b, pulse);
    }

    void FixedUpdate()
    {
        if (mechanism == null || seal == null || Plan == null || Plan.caveOpen) return;
        var leader = team != null ? team.Controlled : null;
        float near = leader != null ? Vector2.Distance(leader.transform.position, mechanism.transform.position) : float.MaxValue;
        if (seal.kind != "element" || near > WeatherRange) return;
        if (Climate.Now is { } weather && System.Array.IndexOf(seal.weathers, weather.key) >= 0) Open();
        else if (seal.night && FindAnyObjectByType<EnemySpawner>() is { Night: true }) Open();
    }

    void LateUpdate()
    {
        // Tests: K opens it at once.
        if (SaveData.Settings.devMode && Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame
            && mechanism != null && Plan != null && !Plan.caveOpen) Open();
    }

    void OnHit(MoveDef move, Vector2 at, float radius, Combatant caster)
    {
        if (mechanism == null || seal == null || Plan == null || Plan.caveOpen || caster == null || caster.team != Team.Player) return;
        if (Vector2.Distance(at, mechanism.transform.position) > Mathf.Max(radius, 0.4f) + HitMargin) return;
        if (seal.kind != "element" || System.Array.IndexOf(seal.types, move.type) < 0)
        {
            Flash(new Color(0.45f, 0.45f, 0.45f)); // no effect
            return;
        }
        hits++;
        Flash(Color.white);
        if (hits >= Mathf.Max(1, seal.stages)) Open();
    }

    void Flash(Color colour)
    {
        flash = colour;
        flashUntil = Time.time + FlashTime;
    }

    /// Opens the door: the wall goes, the mouth shows, the camera shakes and dust flies.
    public void Open()
    {
        if (Plan == null || Plan.caveOpen) return;
        var changed = Plan.OpenCave();
        if (arena != null) arena.RedrawIsland(changed);
        CameraFollow.Shake(0.15f, 0.6f);
        var door = Plan.sealedCaveFront + Vector2Int.up * 2;
        StartCoroutine(Dust(island.Cell(door)));
    }

    IEnumerator Dust(Vector2 at)
    {
        var puffs = new List<(SpriteRenderer r, Vector2 velocity)>();
        for (int k = 0; k < 14; k++)
        {
            var r = new GameObject("Poussière").AddComponent<SpriteRenderer>();
            r.sprite = Shapes.Circle;
            r.sortingOrder = 2;
            r.transform.position = at + new Vector2(Random.Range(-1.4f, 1.4f), Random.Range(-1f, 0.6f));
            r.transform.localScale = Vector3.one * Random.Range(0.4f, 0.8f);
            puffs.Add((r, new Vector2(Random.Range(-1f, 1f), Random.Range(-0.6f, 0.4f))));
        }
        const float time = 0.8f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            foreach (var (r, velocity) in puffs)
            {
                if (r == null) continue;
                r.transform.position += (Vector3)(velocity * Time.deltaTime);
                r.transform.localScale += Vector3.one * Time.deltaTime * 0.6f;
                r.color = new Color(0.75f, 0.68f, 0.58f, 0.7f * (1f - t / time));
            }
            yield return null;
        }
        foreach (var (r, _) in puffs) if (r != null) Destroy(r.gameObject);
    }

    void OnGUI()
    {
        if (mechanism == null || seal == null || Plan == null || Plan.caveOpen || string.IsNullOrEmpty(seal.hint)) return;
        var leader = team != null ? team.Controlled : null;
        var cam = Camera.main;
        if (leader == null || cam == null) return;
        if (Vector2.Distance(leader.transform.position, mechanism.transform.position) > HintRange) return;
        var screen = cam.WorldToScreenPoint(mechanism.transform.position + Vector3.up * 1.2f);
        float scale = Screen.height / 720f;
        var style = new GUIStyle(GUI.skin.box) { fontSize = Mathf.RoundToInt(16 * scale), wordWrap = true, alignment = TextAnchor.MiddleCenter };
        string text = $"{seal.name}\n{seal.hint}";
        float width = 280f * scale, height = style.CalcHeight(new GUIContent(text), width);
        GUI.Box(new Rect(screen.x - width / 2f, Screen.height - screen.y - height, width, height), text, style);
    }
}

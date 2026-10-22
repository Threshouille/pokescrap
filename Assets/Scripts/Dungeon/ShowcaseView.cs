using System;
using System.Collections.Generic;
using UnityEngine;

/// Draws the showcase (ShowcaseMap) in the new island art (Assets/Resources/IslandArt, assets.json): the ground in one
/// piece with the OrganicGround shader (its fields baked in a small texture, 4 texels per tile), then the decor
/// elements at their foot, sorted by depth with the Pokémon (same sorting order, lower on screen in front), each with
/// its shadow (a silhouette drawn half see-through); the trees as trunk and foliage, the bridge over the river.
/// DungeonGenerator builds the gameplay grid under it and hides its own tiles.
public class ShowcaseView : MonoBehaviour
{
    const float PixelsPerTile = 24f;
    const int FieldTexels = 4; // per tile
    const int GroundOrder = -6, ShadowOrder = -2, BridgeOrder = -1, PropOrder = 1; // the Pokémon are at 1
    static readonly Color ShadowColour = new(0f, 0f, 0f, 0.28f);
    static readonly Color Sky = new(0.62f, 0.8f, 0.95f);

    [Serializable]
    class Asset
    {
        public string file, kind;
        public int width, height, frame_width, frame_height, frame_count;
        public Point anchor = new(), canopy_offset;
        public string shadow_of;
    }
    [Serializable] class Point { public float x, y; }
    [Serializable] class AssetFile { public Asset[] assets = new Asset[0]; }

    Dictionary<string, Asset> assets;
    readonly Dictionary<string, Sprite> sprites = new();
    readonly List<UnityEngine.Object> made = new();
    Transform root;
    Color previousBackground;
    bool shown;

    /// Draws the map over the arena DungeonGenerator has just built from it.
    public void Build(DungeonGenerator arena, ShowcaseMap map)
    {
        Clear();
        LoadAssets();
        root = new GameObject("Vitrine").transform;
        Vector2 origin = arena.CentreOf(arena.Interior.xMin, arena.Interior.yMin) - new Vector2(0.5f, 0.5f);
        Ground(map, origin);
        foreach (var p in map.props) Prop(p.file, origin + p.at);
        Bridge(map, origin);
        if (Camera.main != null)
        {
            if (!shown) previousBackground = Camera.main.backgroundColor;
            Camera.main.backgroundColor = Sky;
        }
        shown = true;
    }

    public void Clear()
    {
        if (root != null) Destroy(root.gameObject);
        root = null;
        foreach (var o in made) if (o != null) Destroy(o);
        made.Clear();
        sprites.Clear();
        if (shown && Camera.main != null) Camera.main.backgroundColor = previousBackground;
        shown = false;
    }

    void OnDestroy() => Clear();

    void LoadAssets()
    {
        if (assets != null) return;
        assets = new Dictionary<string, Asset>();
        var text = Resources.Load<TextAsset>("IslandArt/assets");
        if (text == null) return;
        foreach (var a in JsonUtility.FromJson<AssetFile>(text.text).assets) assets[a.file.Replace(".png", "")] = a;
    }

    // The ground: one quad over the map, the shader picking each pixel's material from the fields.
    void Ground(ShowcaseMap map, Vector2 origin)
    {
        int w = map.width * FieldTexels, h = map.height * FieldTexels;
        // Linear: these are distances, not colours (sRGB would bend them).
        var fields = new Texture2D(w, h, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        static byte Encode(float d) => (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f + d / 4f) * 255f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2((x + 0.5f) / FieldTexels, (y + 0.5f) / FieldTexels);
                px[y * w + x] = new Color32(Encode(map.PathField(p)), Encode(map.BankField(p)), Encode(map.WaterField(p)), Encode(map.LandField(p)));
            }
        fields.SetPixels32(px);
        fields.Apply();
        made.Add(fields);

        var shader = Resources.Load<Shader>("Materials/OrganicGround");
        var material = new Material(shader);
        made.Add(material);
        material.SetTexture("_Splat", fields);
        foreach (var (property, file) in new[] { ("_GrassA", "sol_herbe_a"), ("_GrassB", "sol_herbe_b"), ("_GrassC", "sol_herbe_c"),
                     ("_Path", "sol_chemin"), ("_Bank", "sol_berge"), ("_Water", "eau_fond"), ("_Ripples", "eau_reflets"), ("_Foam", "eau_ecume") })
            material.SetTexture(property, Resources.Load<Texture2D>("IslandArt/" + file));
        material.SetVector("_Origin", origin);
        material.SetVector("_Size", new Vector2(map.width, map.height));
        material.SetVector("_Flow", new Vector2(0f, -1f));

        var mesh = new Mesh
        {
            vertices = new[] { new Vector3(0, 0), new Vector3(map.width, 0), new Vector3(0, map.height), new Vector3(map.width, map.height) },
            triangles = new[] { 0, 2, 1, 2, 3, 1 },
        };
        mesh.RecalculateBounds();
        made.Add(mesh);
        var go = new GameObject("Sol");
        go.transform.SetParent(root, false);
        go.transform.position = origin;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = GroundOrder;
    }

    // A decor element at its foot; a tree trunk brings its foliage; each one with its shadow when it has one.
    void Prop(string file, Vector2 foot)
    {
        Sprite(file, foot, PropOrder, Color.white);
        string shadow = file.EndsWith("_tronc") ? file.Replace("_tronc", "_ombre") : file + "_ombre";
        if (assets.ContainsKey(shadow)) Sprite(shadow, foot, ShadowOrder, ShadowColour, false);
        if (file.EndsWith("_tronc"))
        {
            string foliage = file.Replace("_tronc", "_feuillage");
            var offset = assets.TryGetValue(foliage, out var a) && a.canopy_offset != null ? new Vector2(a.canopy_offset.x, a.canopy_offset.y) / PixelsPerTile : Vector2.zero;
            // Sorted as the trunk's foot: a Pokémon behind the tree is under its foliage, one in front over it.
            Sprite(foliage, foot + offset, PropOrder, Color.white);
        }
    }

    // The bridge over the river where the path crosses it, its shadow on the water.
    void Bridge(ShowcaseMap map, Vector2 origin)
    {
        string file = map.bridgeAcross ? "pont_horizontal" : "pont_vertical";
        if (!assets.TryGetValue(file, out var a)) return;
        // Its anchor is at the middle of its bottom: lowered by half its height to centre it on the crossing.
        var foot = origin + map.bridgeAt - new Vector2(0f, a.height / PixelsPerTile / 2f);
        Sprite(file + "_ombre", foot, ShadowOrder, ShadowColour, false);
        Sprite(file, foot, BridgeOrder, Color.white, false);
    }

    SpriteRenderer Sprite(string file, Vector2 foot, int order, Color colour, bool sortByFoot = true)
    {
        var sprite = Get(file);
        if (sprite == null) return null;
        var go = new GameObject(file);
        go.transform.SetParent(root, false);
        go.transform.position = foot;
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.color = colour;
        r.sortingOrder = order;
        if (sortByFoot) r.spriteSortPoint = SpriteSortPoint.Pivot;
        return r;
    }

    // The sprite of a file, its pivot at its anchor (first frame for an animation).
    Sprite Get(string file)
    {
        if (sprites.TryGetValue(file, out var s)) return s;
        var tex = Resources.Load<Texture2D>("IslandArt/" + file);
        if (tex == null || !assets.TryGetValue(file, out var a)) return null;
        float w = a.frame_width > 0 ? a.frame_width : tex.width, h = a.frame_height > 0 ? a.frame_height : tex.height;
        s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(a.anchor.x / w, a.anchor.y / h), PixelsPerTile, 0, SpriteMeshType.FullRect);
        sprites[file] = s;
        made.Add(s);
        return s;
    }
}

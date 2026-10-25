using UnityEngine;

/// What shows a Terastallized Pokémon at a glance, whatever its model: a crystal outline of its Tera type around its
/// sprite (four silhouette copies just behind it, shifted by a pixel or two; white and wide when it starts, then
/// pulsing), and the Tera shard's icon above its head, bobbing on a disc of that colour. Combatant.Terastallize adds
/// it, RevertTera removes it.
public class TeraAura : MonoBehaviour
{
    const float Outline = 1.5f / 24f, ArriveOutline = 4f / 24f; // tiles (24 pixels a tile)
    const float IconSize = 0.6f, DiscSize = 0.78f, IconGap = 0.15f, MinIconHeight = 1.1f; // tiles
    const float Arrive = 0.5f;
    static readonly Vector2[] Sides = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };

    SpriteRenderer body;
    readonly SpriteRenderer[] copies = new SpriteRenderer[4];
    Transform top;
    Color colour;
    float bornAt, iconHeight;

    public static TeraAura Attach(Combatant who, int type)
    {
        var go = new GameObject("Téra");
        go.transform.SetParent(who.transform, false);
        var aura = go.AddComponent<TeraAura>();
        aura.Build(who.GetComponent<SpriteRenderer>(), type);
        return aura;
    }

    void Build(SpriteRenderer of, int type)
    {
        body = of;
        colour = Color.Lerp(GameData.TypeColor(type), Color.white, 0.15f);
        bornAt = Time.time;
        for (int i = 0; i < copies.Length; i++)
        {
            var go = new GameObject("Contour");
            go.transform.SetParent(transform, false);
            copies[i] = go.AddComponent<SpriteRenderer>();
            Unlit.Apply(copies[i]); // keeps its colours at night (DayNight)
            copies[i].sharedMaterial = Evolution.Silhouette;
            copies[i].sortingOrder = 0; // just under the Pokémon (1), never its order
        }
        // The icon keeps its size in tiles whatever the Pokémon's scale (elites are bigger).
        top = new GameObject("Icône").transform;
        top.SetParent(transform, false);
        top.localScale = Vector3.one / Mathf.Max(0.01f, transform.lossyScale.x);
        var disc = Part("Disque", Shapes.Circle, 2, DiscSize);
        disc.color = new Color(colour.r * 0.35f, colour.g * 0.35f, colour.b * 0.35f, 0.85f);
        var icon = Part("Éclat", SpecialItems.Icon(SpecialItems.IconName(SpecialItems.Family.Tera, null, type)), 3, IconSize);
        if (icon.sprite == null) { icon.sprite = Shapes.Circle; icon.color = colour; }
        LateUpdate();
    }

    SpriteRenderer Part(string name, Sprite sprite, int order, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(top, false);
        var r = go.AddComponent<SpriteRenderer>();
        Unlit.Apply(r); // keeps its colours at night (DayNight)
        r.sprite = sprite;
        r.sortingOrder = order; // above the Pokémon
        float width = sprite != null ? sprite.bounds.size.x : 1f;
        go.transform.localScale = Vector3.one * size / Mathf.Max(0.01f, width);
        return r;
    }

    // Visual only (after the animator has set this frame's sprite): nothing here touches the game state.
    void LateUpdate()
    {
        if (body == null) return;
        float scale = Mathf.Max(0.01f, transform.lossyScale.x);
        float arrive = Mathf.Clamp01((Time.time - bornAt) / Arrive);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
        float offset = Mathf.Lerp(ArriveOutline, Outline, arrive) / scale;
        var tint = Color.Lerp(Color.white, colour, arrive);
        tint.a = Mathf.Lerp(0.7f, 1f, pulse) * body.color.a;
        for (int i = 0; i < copies.Length; i++)
        {
            var c = copies[i];
            c.sprite = body.sprite;
            c.flipX = body.flipX;
            c.flipY = body.flipY;
            c.color = tint;
            c.transform.localPosition = (Vector3)(Sides[i] * offset);
        }
        // Above the top of its sprite (the frame's top runs a little above the head), never lower than a small one's.
        float spriteTop = body.sprite != null ? body.sprite.bounds.max.y * scale * 0.85f : 0f;
        iconHeight = Mathf.Lerp(iconHeight > 0f ? iconHeight : spriteTop, Mathf.Max(MinIconHeight, spriteTop + IconGap), 0.1f);
        top.localPosition = new Vector3(0f, (iconHeight + 0.06f * Mathf.Sin(Time.time * 3f)) / scale, 0f);
    }
}

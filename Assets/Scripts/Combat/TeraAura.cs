using UnityEngine;

/// What shows a Terastallized Pokémon at a glance: a crystal hexagon of its Tera type under its feet (it closes in from
/// wide when it starts, then pulses and turns slowly), and the Tera shard's icon above its head, bobbing on a disc of
/// that colour. Combatant.Terastallize adds it, RevertTera removes it.
public class TeraAura : MonoBehaviour
{
    const float HaloSize = 1.25f, IconHeight = 1.2f, IconSize = 0.6f, DiscSize = 0.78f; // tiles
    const float Arrive = 0.45f, ArriveScale = 3.5f;

    SpriteRenderer halo, inner, disc;
    Transform top;
    Color colour;
    float bornAt;

    public static TeraAura Attach(Combatant who, int type)
    {
        var go = new GameObject("Téra");
        go.transform.SetParent(who.transform, false);
        // Sizes in tiles whatever the Pokémon's own scale (elites are bigger).
        go.transform.localScale = Vector3.one / Mathf.Max(0.01f, who.transform.lossyScale.x);
        var aura = go.AddComponent<TeraAura>();
        aura.Build(type);
        return aura;
    }

    void Build(int type)
    {
        colour = Color.Lerp(GameData.TypeColor(type), Color.white, 0.15f);
        bornAt = Time.time;
        halo = Part("Hexagone", transform, Shapes.Hexagon, 0, HaloSize);
        inner = Part("Hexagone intérieur", transform, Shapes.Hexagon, 0, HaloSize * 0.7f);
        top = new GameObject("Icône").transform;
        top.SetParent(transform, false);
        disc = Part("Disque", top, Shapes.Circle, 2, DiscSize);
        disc.color = new Color(colour.r * 0.35f, colour.g * 0.35f, colour.b * 0.35f, 0.85f);
        var icon = Part("Éclat", top, SpecialItems.Icon(SpecialItems.IconName(SpecialItems.Family.Tera, null, type)), 3, IconSize);
        if (icon.sprite == null) icon.sprite = Shapes.Hexagon;
        Update();
    }

    static SpriteRenderer Part(string name, Transform parent, Sprite sprite, int order, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = order; // 0 on the ground under the Pokémon, 2-3 above it
        float width = sprite != null ? sprite.bounds.size.x : 1f;
        go.transform.localScale = Vector3.one * size / Mathf.Max(0.01f, width);
        return r;
    }

    // Visual only: nothing here touches the game state.
    void Update()
    {
        float age = Time.time - bornAt, arrive = Mathf.Clamp01(age / Arrive);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
        float spread = Mathf.Lerp(ArriveScale, 1f, 1f - (1f - arrive) * (1f - arrive));
        halo.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 25f);
        halo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        halo.transform.localScale = Vector3.one * HaloSize * spread * (1f + 0.06f * pulse) / Mathf.Max(0.01f, Shapes.Hexagon.bounds.size.x);
        halo.color = new Color(colour.r, colour.g, colour.b, Mathf.Lerp(0.55f, 0.95f, pulse));
        inner.transform.localRotation = Quaternion.Euler(0f, 0f, -Time.time * 40f);
        inner.transform.localPosition = halo.transform.localPosition;
        inner.color = new Color(colour.r, colour.g, colour.b, arrive * Mathf.Lerp(0.25f, 0.5f, 1f - pulse));
        top.localPosition = new Vector3(0f, IconHeight + 0.06f * Mathf.Sin(Time.time * 3f), 0f);
    }
}

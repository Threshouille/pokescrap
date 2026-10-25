using UnityEngine;

/// Small health bar floating above a Combatant.
[RequireComponent(typeof(Combatant))]
public class HealthBar : MonoBehaviour
{
    const float Width = 1f, Height = 0.12f, OffsetY = 0.8f;

    Combatant combatant;
    Transform fill;

    void Start()
    {
        combatant = GetComponent<Combatant>();
        var color = combatant.team == Team.Player ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.9f, 0.25f, 0.25f);
        MakeBar("HealthBarBack", new Color(0f, 0f, 0f, 0.6f), 10);
        fill = MakeBar("HealthBarFill", color, 11);
    }

    Transform MakeBar(string barName, Color color, int order)
    {
        var bar = new GameObject(barName).transform;
        bar.SetParent(transform, false);
        bar.localPosition = new Vector3(0f, OffsetY, 0f);
        bar.localScale = new Vector3(Width, Height, 1f);
        var renderer = bar.gameObject.AddComponent<SpriteRenderer>();
        Unlit.Apply(renderer); // keeps its colours at night (DayNight)
        renderer.sprite = Shapes.Square;
        renderer.color = color;
        renderer.sortingOrder = order;
        return bar;
    }

    void LateUpdate()
    {
        float ratio = combatant.maxHp > 0f ? Mathf.Clamp01(combatant.hp / combatant.maxHp) : 0f;
        fill.localScale = new Vector3(Width * ratio, Height, 1f);
        fill.localPosition = new Vector3(-Width * (1f - ratio) / 2f, OffsetY, 0f);
    }
}

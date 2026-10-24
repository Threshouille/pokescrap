using UnityEngine;

/// The rank of an enemy drawn by Encounters (commun, peu commun, rare, épique): a ring of its colour under it, none
/// for the commun ones.
public class RarityMark : MonoBehaviour
{
    public Encounters.Rarity Rarity { get; private set; }

    /// Whether this enemy's rank makes it change obstacles to reach its target (repairs in encounters.json).
    public static bool Repairs(Combatant c)
    {
        var mark = c != null ? c.GetComponent<RarityMark>() : null;
        return mark != null && mark.Rarity != null && mark.Rarity.repairs;
    }

    public static void Put(Combatant c, Encounters.Rarity rarity)
    {
        var mark = c.gameObject.AddComponent<RarityMark>();
        mark.Rarity = rarity;
        if (!rarity.Shown) return;
        var ring = new GameObject("Rang").AddComponent<SpriteRenderer>();
        ring.transform.SetParent(c.transform, false);
        ring.transform.localPosition = new Vector3(0f, -0.32f, 0f);
        ring.transform.localScale = new Vector3(0.9f, 0.4f, 1f);
        ring.sprite = Shapes.Ring;
        ring.color = rarity.Colour;
        ring.sortingOrder = 0; // ground marks, under the Pokémon
    }
}

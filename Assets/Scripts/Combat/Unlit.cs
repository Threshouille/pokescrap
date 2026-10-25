using UnityEngine;

/// Sprites that keep their colours whatever the light (DayNight): health bars, damage numbers, aim marks, bubbles,
/// icons, effects. They use Resources/Materials/SpriteUnlit (URP's Sprite-Unlit-Default).
public static class Unlit
{
    static Material material;

    public static Material Material
    {
        get
        {
            if (material == null) material = Resources.Load<Material>("Materials/SpriteUnlit");
            return material;
        }
    }

    /// Draws that renderer unlit; returns it.
    public static T Apply<T>(T renderer) where T : Renderer
    {
        if (renderer != null && Material != null) renderer.sharedMaterial = Material;
        return renderer;
    }
}

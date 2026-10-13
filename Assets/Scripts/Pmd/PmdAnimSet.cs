using System;
using System.Collections.Generic;
using UnityEngine;

/// Animations of one Pokémon, built from an exported AnimData.xml (see Pokescrap > Build Anim Sets).
public class PmdAnimSet : ScriptableObject
{
    public const int PixelsPerUnit = 24; // one dungeon tile = 24 px = 1 unit

    [Serializable]
    public class Anim
    {
        public string name;
        public Texture2D texture; // one row per direction, one column per frame
        public int frameWidth;
        public int frameHeight;
        public int[] durations; // per frame, in 1/60 s
    }

    public int pokemonId; // id in the game data, read from id.txt next to the sprites
    public List<Anim> anims = new();

    [NonSerialized] Dictionary<string, Anim> byName;
    [NonSerialized] Dictionary<(Texture2D, int, int), Sprite> sprites;

    public Anim Get(string animName)
    {
        if (byName == null)
        {
            byName = new Dictionary<string, Anim>();
            foreach (var a in anims) byName[a.name] = a;
        }
        return byName.TryGetValue(animName, out var anim) ? anim : null;
    }

    public Sprite GetSprite(Anim anim, int direction, int frame)
    {
        sprites ??= new Dictionary<(Texture2D, int, int), Sprite>();
        int rows = anim.texture.height / anim.frameHeight;
        int row = direction < rows ? direction : 0; // some animations have a single direction
        var key = (anim.texture, row, frame);
        // Unity destroys runtime sprites when play mode ends while this asset (and its cache) survives,
        // so a cached entry can be a destroyed object: recreate it.
        if (!sprites.TryGetValue(key, out var sprite) || sprite == null)
        {
            var rect = new Rect(frame * anim.frameWidth, anim.texture.height - (row + 1) * anim.frameHeight,
                anim.frameWidth, anim.frameHeight);
            sprite = Sprite.Create(anim.texture, rect, new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprites[key] = sprite;
        }
        return sprite;
    }
}

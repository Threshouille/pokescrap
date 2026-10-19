using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

/// Import settings and asset generation for sprites (Assets/Sprites/Pokemon) and tilesets (Assets/Tilesets).
public class PmdSpriteImport : AssetPostprocessor
{
    const string Root = "Assets/Sprites/Pokemon";
    const string TilesetRoot = "Assets/Tilesets";
    const string EffectRoot = "Assets/Resources/Effects";
    const string PortraitRoot = "Assets/Resources/Portraits";
    const string ObstacleTileRoot = "Assets/Resources/ObstacleTiles";
    const string AnimSetRoot = "Assets/Resources/" + PokemonRoster.AnimSetPath;

    void OnPreprocessTexture()
    {
        if (assetPath.StartsWith(Root + "/") || assetPath.StartsWith(TilesetRoot + "/") || assetPath.StartsWith(EffectRoot + "/") || assetPath.StartsWith(PortraitRoot + "/") || assetPath.StartsWith(ObstacleTileRoot + "/"))
            Configure((TextureImporter)assetImporter);
    }

    static bool Configure(TextureImporter importer)
    {
        bool changed = importer.filterMode != FilterMode.Point
            || importer.textureCompression != TextureImporterCompression.Uncompressed
            || importer.mipmapEnabled || importer.npotScale != TextureImporterNPOTScale.None;
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        return changed;
    }

    // Entry point of Tools/setup.py (Unity in batch mode): rebuilds every asset made from the ROM.
    public static void BuildEverything()
    {
        AssetDatabase.Refresh();
        BuildTilesets();
        BuildAll();
    }

    [MenuItem("Pokescrap/Build Tilesets")]
    public static void BuildTilesets()
    {
        foreach (var folder in AssetDatabase.GetSubFolders(TilesetRoot))
        {
            var jsonPath = folder + "/tileset.json";
            var texPath = folder + "/chunks.png";
            if (!File.Exists(jsonPath) || AssetImporter.GetAtPath(texPath) is not TextureImporter importer) continue;
            if (Configure(importer)) importer.SaveAndReimport();

            var assetPath = folder + "/Tileset.asset";
            var tileset = AssetDatabase.LoadAssetAtPath<DungeonTileset>(assetPath);
            if (tileset == null)
            {
                tileset = ScriptableObject.CreateInstance<DungeonTileset>();
                AssetDatabase.CreateAsset(tileset, assetPath);
            }
            JsonUtility.FromJsonOverwrite(File.ReadAllText(jsonPath), tileset);
            tileset.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            EditorUtility.SetDirty(tileset);
            Debug.Log($"{assetPath}: {tileset.wall.Length / 3} wall masks");
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Pokescrap/Build Anim Sets")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(AnimSetRoot);
        AssetDatabase.Refresh();
        var ids = new System.Collections.Generic.List<int>();
        int done = 0;
        foreach (var folder in AssetDatabase.GetSubFolders(Root))
        {
            var xmlPath = folder + "/AnimData.xml";
            var idPath = folder + "/id.txt";
            if (!File.Exists(xmlPath) || !File.Exists(idPath)) continue;
            int id = int.Parse(File.ReadAllText(idPath).Trim());

            // Anim sets live in Resources/AnimSets/<id> so the game loads them one at a time; older ones sat
            // next to the sprites and are moved (keeping their GUID, so scene references survive).
            var assetPath = $"{AnimSetRoot}/{id}.asset";
            var oldPath = folder + "/AnimSet.asset";
            if (File.Exists(oldPath) && !File.Exists(assetPath)) AssetDatabase.MoveAsset(oldPath, assetPath);
            var set = AssetDatabase.LoadAssetAtPath<PmdAnimSet>(assetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<PmdAnimSet>();
                AssetDatabase.CreateAsset(set, assetPath);
            }
            set.pokemonId = id;
            if (!ids.Contains(id)) ids.Add(id);

            set.anims.Clear();
            var nodes = XDocument.Load(xmlPath).Root.Element("Anims").Elements("Anim").ToList();
            foreach (var node in nodes)
            {
                // <CopyOf> entries reuse the sheet and timings of another animation.
                var source = node.Element("CopyOf") is { } copy
                    ? nodes.First(n => (string)n.Element("Name") == (string)copy)
                    : node;
                var texPath = $"{folder}/{(string)source.Element("Name")}-Anim.png";
                if (AssetImporter.GetAtPath(texPath) is not TextureImporter importer) continue;
                if (Configure(importer)) importer.SaveAndReimport();

                set.anims.Add(new PmdAnimSet.Anim
                {
                    name = (string)node.Element("Name"),
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath),
                    frameWidth = (int)source.Element("FrameWidth"),
                    frameHeight = (int)source.Element("FrameHeight"),
                    durations = source.Element("Durations").Elements("Duration").Select(d => (int)d).ToArray(),
                });
            }
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);
            if (set.anims.Count == 0) Debug.LogWarning($"{assetPath} ({folder}): no animations");
            // Hundreds of uncompressed sheets do not fit in memory: release them as we go.
            if (++done % 25 == 0) EditorUtility.UnloadUnusedAssetsImmediate();
        }
        BuildRoster(ids);
        AssetDatabase.SaveAssets();
        Debug.Log($"Build Anim Sets: {done} anim sets");
    }

    // Lists every anim set in Resources so the game can find any Pokémon's sprites by id.
    static void BuildRoster(System.Collections.Generic.List<int> ids)
    {
        var path = $"Assets/Resources/{PokemonRoster.ResourcePath}.asset";
        var roster = AssetDatabase.LoadAssetAtPath<PokemonRoster>(path);
        if (roster == null)
        {
            roster = ScriptableObject.CreateInstance<PokemonRoster>();
            AssetDatabase.CreateAsset(roster, path);
        }
        ids.Sort();
        roster.ids = ids;
        EditorUtility.SetDirty(roster);
        Debug.Log($"{path}: {roster.ids.Count} Pokémon");
    }

    [MenuItem("Pokescrap/Effacer la sauvegarde")]
    public static void EraseSave()
    {
        SaveData.Erase();
        Debug.Log($"Save erased: {SaveData.Path}");
    }
}

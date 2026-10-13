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

    void OnPreprocessTexture()
    {
        if (assetPath.StartsWith(Root + "/") || assetPath.StartsWith(TilesetRoot + "/") || assetPath.StartsWith(EffectRoot + "/"))
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
        foreach (var folder in AssetDatabase.GetSubFolders(Root))
        {
            var xmlPath = folder + "/AnimData.xml";
            if (!File.Exists(xmlPath)) continue;

            var assetPath = folder + "/AnimSet.asset";
            var set = AssetDatabase.LoadAssetAtPath<PmdAnimSet>(assetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<PmdAnimSet>();
                AssetDatabase.CreateAsset(set, assetPath);
            }

            var idPath = folder + "/id.txt";
            if (File.Exists(idPath)) set.pokemonId = int.Parse(File.ReadAllText(idPath).Trim());

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
            Debug.Log($"{assetPath}: {set.anims.Count} animations");
        }
        AssetDatabase.SaveAssets();
    }
}

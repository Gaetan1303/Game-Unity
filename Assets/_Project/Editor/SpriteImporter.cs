#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Règle l'import des PNG de Resources/Sprites : Sprite, filtre, pivot, pixels-par-unité PAR DOSSIER.
/// Les réglages viennent de Editor/SpritePack.json (modifié par AssetPack/tools/import_back.py) :
/// remplacer un sprite par une version plus détaillée ne change donc pas sa taille en jeu.
/// </summary>
public class SpriteImporter : AssetPostprocessor
{
    public const string Root = "Assets/_Project/Resources/Sprites";
    const string ConfigPath = "Assets/_Project/Editor/SpritePack.json";

    [System.Serializable] class Entry { public string folder; public float base_ppu = 16f; public float ppu = 16f; public string filter = "Point"; }
    [System.Serializable] class Config { public Entry[] folders; }

    static Config LoadConfig()
    {
        try { return JsonUtility.FromJson<Config>(File.ReadAllText(ConfigPath)); }
        catch { return null; }
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root)) return;
        var ti = (TextureImporter)assetImporter;

        string folder = assetPath.Substring(Root.Length).TrimStart('/').Split('/')[0];
        float ppu = 16f; string filter = "Point";
        var cfg = LoadConfig();
        if (cfg != null && cfg.folders != null)
            foreach (var f in cfg.folders)
                if (f.folder == folder) { ppu = f.ppu; filter = f.filter; }

        bool env = folder == "Env";
        bool centered = env || folder == "Fx";

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = ppu;
        ti.filterMode = filter == "Point" ? FilterMode.Point : FilterMode.Bilinear;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 4096;

        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteAlignment = (int)(centered ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
        s.spriteMeshType = env ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
        ti.SetTextureSettings(s);
    }

    // si SpritePack.json change (ex: import_back.py en mode hd), on réimporte tous les sprites automatiquement
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (var p in imported)
        {
            if (p != ConfigPath) continue;
            EditorApplication.delayCall += () =>
                AssetDatabase.ImportAsset(Root, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            break;
        }
    }
}
#endif

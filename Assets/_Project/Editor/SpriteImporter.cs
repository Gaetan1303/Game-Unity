#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Règle automatiquement l'import des PNG de Resources/Sprites : Sprite, pixel net, taille par dossier.</summary>
public class SpriteImporter : AssetPostprocessor
{
    public const string Root = "Assets/_Project/Resources/Sprites";

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root)) return;
        var ti = (TextureImporter)assetImporter;

        float ppu = 16f; bool pixel = true, env = false;
        if (assetPath.Contains("/Hero/")) ppu = 26f;        // 52 px -> ~2 unités de haut
        else if (assetPath.Contains("/Enemies/") || assetPath.Contains("/Allies/")) ppu = 10f; // 16 px -> 1,6 unité
        else if (assetPath.Contains("/Env/")) { ppu = 16f; env = true; }
        else if (assetPath.Contains("/Fx/")) { ppu = 256f; pixel = false; } // 256 px -> 1 unité

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = ppu;
        ti.filterMode = pixel ? FilterMode.Point : FilterMode.Bilinear;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.textureCompression = TextureImporterCompression.Uncompressed;

        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteAlignment = (int)((env || assetPath.Contains("/Fx/")) ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
        s.spriteMeshType = env ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
        ti.SetTextureSettings(s);
    }
}
#endif

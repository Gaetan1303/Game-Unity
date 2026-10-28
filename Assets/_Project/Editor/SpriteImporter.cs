#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Règle l'import des PNG de Resources/Sprites avec une densité cohérente pour le pixel art.</summary>
public class SpriteImporter : AssetPostprocessor
{
    public const string Root = "Assets/_Project/Resources/Sprites";
    const float PixelArtPixelsPerUnit = 24f;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root)) return;
        var ti = (TextureImporter)assetImporter;

        float ppu = PixelArtPixelsPerUnit;
        bool pixel = true, env = assetPath.Contains("/Env/");
        if (assetPath.Contains("/Fx/")) { ppu = 256f; pixel = false; }

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

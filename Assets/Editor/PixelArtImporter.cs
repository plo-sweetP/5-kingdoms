using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for all pixel art under Assets/Art/Resources/Sprites: 32 pixels per tile, point filtering,
/// no compression or mipmaps, full-rect meshes. Character textures stay readable so the game can build
/// hit-flash silhouettes from them. Drop a PNG in the right folder and it is ready to use.
/// </summary>
sealed class PixelArtImporter : AssetPostprocessor
{
    const string Root = "Assets/Art/Resources/Sprites/";

    public override uint GetVersion() => 1;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.isReadable = assetPath.StartsWith(Root + "Characters/");

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
    }
}

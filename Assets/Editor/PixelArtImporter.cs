using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for all pixel art under Assets/Art/Resources/Sprites (written by Tools/pixelart/build_art.py):
/// 64 pixels per tile, point filtering, no compression or mipmaps, full-rect meshes, the texture kept at its own size.
/// Animation strips stay single textures: the game cuts them into frames from the art manifest. The heroes' layers
/// and head parts stay readable because the game stacks them into each hero's sprite sheet at run time, the monsters
/// because the hit flash makes silhouettes from them. Drop a PNG in the right folder and it is ready to use.
/// </summary>
sealed class PixelArtImporter : AssetPostprocessor
{
    const string Root = "Assets/Art/Resources/Sprites/";
    static readonly string[] ReadableFolders = { "Heroes/", "Heads/", "Monsters/" };

    public override uint GetVersion() => 2;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048; // build_art.py wraps strips so nothing is larger.
        importer.isReadable = System.Array.Exists(ReadableFolders, folder => assetPath.StartsWith(Root + folder));

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
    }
}

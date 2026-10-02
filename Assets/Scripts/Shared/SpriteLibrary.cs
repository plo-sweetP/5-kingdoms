using System.Collections.Generic;
using UnityEngine;

namespace FiveKingdoms
{
    /// <summary>
    /// Loads pixel art from Resources/Sprites (Assets/Art/Resources/Sprites). Missing art falls back to a
    /// flat-colored square, so the game keeps running while assets are being made or replaced.
    /// </summary>
    public static class SpriteLibrary
    {
        public const int PixelsPerUnit = 32;
        public const float Pixel = 1f / PixelsPerUnit;

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<Sprite, Sprite> Silhouettes = new Dictionary<Sprite, Sprite>();
        static Sprite white;

        public static Sprite Get(string path, Color fallback)
        {
            if (Cache.TryGetValue(path, out var sprite)) return sprite;
            sprite = Resources.Load<Sprite>("Sprites/" + path);
            if (sprite == null)
            {
                Debug.LogWarning($"Missing sprite Resources/Sprites/{path}; using a placeholder square.");
                sprite = CreateSolid(fallback, PixelsPerUnit);
            }
            Cache[path] = sprite;
            return sprite;
        }

        /// <summary>A 1x1 white sprite one world unit wide. Scale and tint it for bars and particles.</summary>
        public static Sprite White => white != null ? white : (white = CreateSolid(Color.white, 1));

        /// <summary>
        /// All-white copy of a sprite (alpha kept) for hit flashes. Needs Read/Write on the texture, which
        /// PixelArtImporter enables for Characters; returns null otherwise.
        /// </summary>
        public static Sprite Silhouette(Sprite source)
        {
            if (source == null) return null;
            if (Silhouettes.TryGetValue(source, out var cached)) return cached;

            Sprite result = null;
            var texture = source.texture;
            if (texture.isReadable)
            {
                var rect = source.rect;
                int width = (int)rect.width, height = (int)rect.height;
                var colors = texture.GetPixels((int)rect.x, (int)rect.y, width, height);
                for (int i = 0; i < colors.Length; i++) colors[i] = new Color(1f, 1f, 1f, colors[i].a);

                var copy = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                copy.SetPixels(colors);
                copy.Apply();
                var pivot = new Vector2(source.pivot.x / width, source.pivot.y / height);
                result = Sprite.Create(copy, new Rect(0, 0, width, height), pivot, source.pixelsPerUnit);
            }
            Silhouettes[source] = result;
            return result;
        }

        static Sprite CreateSolid(Color color, int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}

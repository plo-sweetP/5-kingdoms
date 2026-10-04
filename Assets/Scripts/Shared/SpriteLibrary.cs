using System.Collections.Generic;
using UnityEngine;

namespace FiveKingdoms
{
    /// <summary>One animation: its frames and how it plays.</summary>
    public sealed class SpriteAnim
    {
        public SpriteAnim(string name, Sprite[] frames, float fps, bool loop, int impact = -1)
        {
            Name = name;
            Frames = frames;
            Fps = fps;
            Loop = loop;
            Impact = impact;
        }

        public string Name { get; }
        public Sprite[] Frames { get; }
        public float Fps { get; }
        public bool Loop { get; }

        /// <summary>The frame on which an attack lands, or -1.</summary>
        public int Impact { get; }

        public float Length => Frames.Length / Fps;
    }

    /// <summary>
    /// Everything an actor is drawn with: its animations by name (idle, run, attack, ...), its shadow and a small
    /// portrait for the HUD. Monsters' come straight from the art; heroes' are stacked by <see cref="HeroComposer"/>.
    /// </summary>
    public sealed class ActorSprites
    {
        readonly Dictionary<string, SpriteAnim> anims = new Dictionary<string, SpriteAnim>();

        public Sprite Shadow;
        public Sprite Portrait;

        /// <summary>Where its weapon hand is at rest, from its tile's centre, facing right (heroes: a ring twinkles there).</summary>
        public Vector2 Hand = new Vector2(0.3f, -0.1f);

        public void Add(SpriteAnim anim) => anims[anim.Name] = anim;

        public bool Has(string name) => anims.ContainsKey(name);

        /// <summary>The named animation, or the first of the fallbacks it has, or idle.</summary>
        public SpriteAnim Get(string name, params string[] fallbacks)
        {
            if (anims.TryGetValue(name, out var anim)) return anim;
            foreach (string other in fallbacks)
                if (anims.TryGetValue(other, out anim)) return anim;
            return anims["idle"];
        }

        public SpriteAnim Idle => anims["idle"];
    }

    /// <summary>
    /// Loads pixel art from Resources/Sprites (Assets/Art/Resources/Sprites, written by Tools/pixelart/build_art.py):
    /// single sprites by path, and animation strips cut into frames the way the art manifest says. Missing art falls
    /// back to a flat-colored square (and is counted), so the game keeps running while assets are being made.
    /// </summary>
    public static class SpriteLibrary
    {
        public const int PixelsPerUnit = 64;
        public const float Pixel = 1f / PixelsPerUnit;

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, SpriteAnim> Strips = new Dictionary<string, SpriteAnim>();
        static readonly Dictionary<string, ActorSprites> Monsters = new Dictionary<string, ActorSprites>();
        static readonly Dictionary<Sprite, Sprite> Silhouettes = new Dictionary<Sprite, Sprite>();
        static readonly Dictionary<Sprite, float> VisibleTops = new Dictionary<Sprite, float>();
        static Sprite white;

        /// <summary>How many sprites were asked for and not found since the scene loaded (tests expect zero).</summary>
        public static int MissingCount { get; private set; }

        /// <summary>"Enter Play Mode" keeps statics: sprites made at run time are gone by the next session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Cache.Clear();
            Strips.Clear();
            Monsters.Clear();
            Silhouettes.Clear();
            VisibleTops.Clear();
            white = null;
            MissingCount = 0;
        }

        /// <summary>A single-image sprite by its path under Resources/Sprites (tiles).</summary>
        public static Sprite Get(string path, Color fallback)
        {
            if (Cache.TryGetValue(path, out var sprite)) return sprite;
            sprite = Resources.Load<Sprite>("Sprites/" + path);
            if (sprite == null) sprite = Placeholder(path, fallback);
            Cache[path] = sprite;
            return sprite;
        }

        /// <summary>An animation strip (or a single image with a pivot) by its name in the art manifest, e.g. "Effects/arrow".</summary>
        public static SpriteAnim Strip(string name)
        {
            if (Strips.TryGetValue(name, out var anim)) return anim;
            var info = ArtManifest.Current.Strip(name);
            anim = info != null ? FromInfo(info) : null;
            if (anim == null) anim = new SpriteAnim(name, new[] { Placeholder(name, Color.magenta) }, 10f, false);
            Strips[name] = anim;
            return anim;
        }

        /// <summary>The first frame of a strip: for the art that doesn't move (decorations, items, icons).</summary>
        public static Sprite Still(string name) => Strip(name).Frames[0];

        /// <summary>A monster's animations, shadow and portrait, by its id (spider, bat, troll).</summary>
        public static ActorSprites Monster(string id)
        {
            if (Monsters.TryGetValue(id, out var sprites)) return sprites;
            sprites = new ActorSprites();
            var info = ArtManifest.Current.Monster(id);
            if (info != null)
            {
                foreach (var strip in info.anims)
                    if (FromInfo(strip) is SpriteAnim anim) sprites.Add(anim);
                sprites.Shadow = FromInfo(info.shadow)?.Frames[0];
                sprites.Portrait = Resources.Load<Sprite>("Sprites/" + info.portrait);
            }
            if (!sprites.Has("idle"))
            {
                var square = Placeholder("Monsters/" + id, new Color(0.4f, 0.8f, 0.4f));
                sprites.Add(new SpriteAnim("idle", new[] { square }, 10f, true));
                sprites.Portrait = square;
            }
            Monsters[id] = sprites;
            return sprites;
        }

        /// <summary>Cuts a strip's texture into frames; null when the texture is missing.</summary>
        public static SpriteAnim FromInfo(StripInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.path)) return null;
            var texture = Resources.Load<Texture2D>("Sprites/" + info.path);
            if (texture == null)
            {
                MissingCount++;
                Debug.LogWarning($"Missing texture Resources/Sprites/{info.path}.");
                return null;
            }
            var frames = new Sprite[info.count];
            var pivot = new Vector2(info.pivotX / (float)info.w, 1f - info.pivotY / (float)info.h);
            for (int i = 0; i < info.count; i++)
            {
                int column = i % info.columns, row = i / info.columns;
                var rect = new Rect(column * info.w, texture.height - (row + 1) * info.h, info.w, info.h);
                frames[i] = Sprite.Create(texture, rect, pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            return new SpriteAnim(info.name, frames, Mathf.Max(1, info.fps), info.loop, info.impact);
        }

        /// <summary>A 1x1 white sprite one world unit wide. Scale and tint it for bars and particles.</summary>
        public static Sprite White => white != null ? white : (white = CreateSolid(Color.white, 1));

        /// <summary>
        /// All-white copy of a sprite (alpha kept) for hit flashes. Needs Read/Write on the texture, which
        /// PixelArtImporter enables for monsters and which the heroes' stacked textures have; returns null otherwise.
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
                result = Sprite.Create(copy, new Rect(0, 0, width, height), pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            Silhouettes[source] = result;
            return result;
        }

        /// <summary>
        /// Height of the sprite's highest opaque pixel above its pivot, in world units: where a head ends, for
        /// placing HP bars. Uses the full sprite height when the texture isn't readable.
        /// </summary>
        public static float VisibleTop(Sprite sprite)
        {
            if (VisibleTops.TryGetValue(sprite, out var top)) return top;
            var rect = sprite.rect;
            int width = (int)rect.width, height = (int)rect.height;
            top = (height - sprite.pivot.y) / sprite.pixelsPerUnit;
            if (sprite.texture.isReadable)
            {
                var colors = sprite.texture.GetPixels((int)rect.x, (int)rect.y, width, height); // Bottom row first.
                for (int i = colors.Length - 1; i >= 0; i--)
                {
                    if (colors[i].a <= 0f) continue;
                    top = (i / width + 1 - sprite.pivot.y) / sprite.pixelsPerUnit;
                    break;
                }
            }
            VisibleTops[sprite] = top;
            return top;
        }

        static Sprite Placeholder(string path, Color fallback)
        {
            MissingCount++;
            Debug.LogWarning($"Missing sprite Resources/Sprites/{path}; using a placeholder square.");
            return CreateSolid(fallback, PixelsPerUnit);
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

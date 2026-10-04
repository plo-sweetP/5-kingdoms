using System.Collections.Generic;
using UnityEngine;

namespace FiveKingdoms
{
    /// <summary>
    /// How a hero looks (docs/design/ART.md, "Equipment that shows on the sprite"): who it is, the weapon (which picks
    /// the body it is drawn on), the armor set (head piece and colours), whether the head piece is worn, and the ring
    /// set whose colour twinkles on its hand. Until gear exists every hero has a default look; milestone 1h sets it
    /// from the equipped items.
    /// </summary>
    public sealed class HeroLook
    {
        public string Hero;
        public string Weapon;
        public string Armor;
        public bool HeadPiece = true;
        /// <summary>A cosmetic head piece's id (hair_bow, crown, headband), worn instead of the armor set's; or null.</summary>
        public string Cosmetic;
        /// <summary>A ring set's id (art manifest, "auras": its colour), or null for none.</summary>
        public string Aura;

        public string Key => $"{Hero}|{Weapon}|{Armor}|{HeadPiece}|{Cosmetic}";

        /// <summary>The look a hero starts with (the art manifest's defaults).</summary>
        public static HeroLook Default(string heroId)
        {
            var hero = ArtManifest.Current.Hero(heroId);
            return new HeroLook { Hero = heroId, Weapon = hero?.weapon, Armor = hero?.armor };
        }
    }

    /// <summary>
    /// The looks in use: each hero's default unless one was set (a launch flag for trying looks out today; the equipped
    /// gear from milestone 1h on). The dungeon view and the HUD both draw heroes from here.
    /// </summary>
    public static class HeroLooks
    {
        static readonly Dictionary<string, HeroLook> Chosen = new Dictionary<string, HeroLook>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Chosen.Clear();

        public static void Clear() => Chosen.Clear();

        public static void Set(HeroLook look) => Chosen[look.Hero] = look;

        public static HeroLook For(string heroId) => Chosen.TryGetValue(heroId, out var look) ? look : HeroLook.Default(heroId);

        /// <summary>The sprites of a hero in its current look.</summary>
        public static ActorSprites Sprites(string heroId) => HeroComposer.Build(For(heroId));

        /// <summary>
        /// Reads looks like "haiden=great_sword,mage_robe;uzuki=mage_staff,bare;kristela=crown;all=hawks_eye": per hero
        /// (or "all"), any of a weapon, an armor set, "bare" for no head piece, a cosmetic head piece (hair_bow, crown,
        /// headband) and a ring set, in any order.
        /// </summary>
        public static void Parse(string text, IEnumerable<string> heroIds)
        {
            if (string.IsNullOrEmpty(text)) return;
            var manifest = ArtManifest.Current;
            foreach (string entry in text.Split(';'))
            {
                int equals = entry.IndexOf('=');
                if (equals <= 0) continue;
                string who = entry.Substring(0, equals).Trim().ToLowerInvariant();
                var parts = entry.Substring(equals + 1).Split(',');
                foreach (string heroId in heroIds)
                {
                    if (who != "all" && who != heroId) continue;
                    var look = For(heroId);
                    look = new HeroLook
                    {
                        Hero = heroId, Weapon = look.Weapon, Armor = look.Armor, HeadPiece = look.HeadPiece, Cosmetic = look.Cosmetic, Aura = look.Aura,
                    };
                    foreach (string raw in parts)
                    {
                        string part = raw.Trim().ToLowerInvariant();
                        if (part.Length == 0) continue;
                        if (part == "bare") look.HeadPiece = false;
                        else if (manifest.RigOf(part) != null) look.Weapon = part;
                        else if (manifest.Armor(part) != null) look.Armor = part;
                        else if (manifest.Cosmetic(part) != null) look.Cosmetic = part;
                        else if (manifest.Aura(part) != null) look.Aura = part;
                        else Debug.LogWarning($"Unknown look part '{part}' (not a weapon, armor set, head piece or ring set).");
                    }
                    Set(look);
                }
            }
        }
    }

    /// <summary>
    /// Stacks a hero's sprite sheet from layers at run time, so any hero can show any weapon and armor set without a
    /// drawing for every combination. Bottom to top: the hero's back hair, the weapon's parts behind the body, the
    /// body in the armor's colours, the weapon's parts between body and head, the head with its hair, the armor's head
    /// piece and plume, the weapon's parts in front, the weapon's effects. The head parts follow the body's head
    /// position frame by frame. Tools/pixelart/looks.py does the same stacking for the preview sheets.
    /// </summary>
    public static class HeroComposer
    {
        const int MaxTextureSize = 2048;
        const int PortraitSize = 48;

        // The pack's Blue unit colours, which an armor set's colours replace on the body.
        static readonly Color32[] PackCloth = { Hex("#485884"), Hex("#4697ac") };
        static readonly Color32[] PackMetal = { Hex("#688c8a"), Hex("#9cbeaa"), Hex("#d4edc2") };

        static readonly Dictionary<string, ActorSprites> Built = new Dictionary<string, ActorSprites>();
        static readonly Dictionary<string, Pixels> Sources = new Dictionary<string, Pixels>();

        /// <summary>A texture's pixels, bottom row first (as Unity stores them).</summary>
        sealed class Pixels
        {
            public int Width, Height;
            public Color32[] Data;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Built.Clear();
            Sources.Clear();
        }

        /// <summary>Lets go of the layers' pixels once the party's sheets are built (they are only needed for stacking).</summary>
        public static void ReleaseSources() => Sources.Clear();

        /// <summary>The hero's animations, shadow and portrait for this look; cached per look.</summary>
        public static ActorSprites Build(HeroLook look)
        {
            if (Built.TryGetValue(look.Key, out var cached)) return cached;
            var sprites = Compose(look) ?? Fallback(look.Hero);
            Built[look.Key] = sprites;
            return sprites;
        }

        static ActorSprites Compose(HeroLook look)
        {
            var manifest = ArtManifest.Current;
            var hero = manifest.Hero(look.Hero);
            if (hero == null) return null;
            string weapon = manifest.RigOf(look.Weapon) != null ? look.Weapon : hero.weapon;
            var rig = manifest.RigOf(weapon);
            if (rig == null) return null;
            var armor = manifest.Armor(look.Armor);
            var cosmetic = manifest.Cosmetic(look.Cosmetic);
            var worn = cosmetic == null && look.HeadPiece ? armor : null;
            var recolor = armor != null ? BodyColors(armor) : null;

            int frames = rig.headX.Length;
            int columns = Mathf.Max(1, Mathf.Min(frames, MaxTextureSize / rig.cellW));
            int rows = (frames + columns - 1) / columns;
            var sheet = new Pixels { Width = columns * rig.cellW, Height = rows * rig.cellH };
            sheet.Data = new Color32[sheet.Width * sheet.Height];

            var body = hero.skirt ? rig.Layer("body_skirt") ?? rig.Layer("body") : rig.Layer("body");
            string hair = cosmetic != null ? cosmetic.hair : worn == null || !HasArt(worn.piece) ? "full" : worn.hair;
            for (int f = 0; f < frames; f++)
            {
                int cellX = f % columns * rig.cellW, cellY = f / columns * rig.cellH;
                int headX = rig.headX[f], headY = rig.headY[f];
                DrawPart(sheet, rig, cellX, cellY, hero.Part("back"), headX, headY, null);
                DrawLayer(sheet, rig, cellX, cellY, rig.Layer(weapon + "_back"), f, null);
                DrawLayer(sheet, rig, cellX, cellY, body, f, recolor);
                DrawLayer(sheet, rig, cellX, cellY, rig.Layer(weapon + "_mid"), f, recolor);
                DrawPart(sheet, rig, cellX, cellY, hero.Part("base"), headX, headY, null);
                if (hair == "full") DrawPart(sheet, rig, cellX, cellY, hero.Part("full"), headX, headY, null);
                else if (hair == "bangs") DrawPart(sheet, rig, cellX, cellY, hero.Part("bangs"), headX, headY, null);
                if (cosmetic != null) DrawPart(sheet, rig, cellX, cellY, cosmetic.piece, headX, headY, null);
                if (worn != null)
                {
                    DrawPart(sheet, rig, cellX, cellY, worn.piece, headX, headY, null);
                    // A helmet's plume sways on the body it was drawn for; elsewhere it rides on the head, still.
                    var plume = rig.Layer("plume_" + worn.id);
                    if (plume != null) DrawLayer(sheet, rig, cellX, cellY, plume, f, recolor);
                    else DrawPart(sheet, rig, cellX, cellY, worn.plume, headX, headY, recolor);
                }
                DrawLayer(sheet, rig, cellX, cellY, rig.Layer(weapon + "_front"), f, null);
                DrawLayer(sheet, rig, cellX, cellY, rig.Layer(weapon + "_fx"), f, null);
            }

            var texture = new Texture2D(sheet.Width, sheet.Height, TextureFormat.RGBA32, false)
            {
                name = "Hero " + look.Key,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(sheet.Data);
            texture.Apply(false, false); // Stays readable: the hit flash makes silhouettes from it.

            var sprites = new ActorSprites();
            var pivot = new Vector2(rig.pivotX / (float)rig.cellW, 1f - rig.pivotY / (float)rig.cellH);
            foreach (var anim in rig.anims)
            {
                var list = new Sprite[anim.count];
                for (int i = 0; i < anim.count; i++)
                {
                    int f = anim.start + i;
                    var rect = new Rect(f % columns * rig.cellW, sheet.Height - (f / columns + 1) * rig.cellH, rig.cellW, rig.cellH);
                    list[i] = Sprite.Create(texture, rect, pivot, SpriteLibrary.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                }
                sprites.Add(new SpriteAnim(anim.name, list, Mathf.Max(1, anim.fps), anim.loop, anim.impact));
            }
            sprites.Shadow = SpriteLibrary.FromInfo(rig.shadow)?.Frames[0];
            sprites.Portrait = Portrait(sheet, rig);
            sprites.Hand = new Vector2(rig.handX - rig.pivotX, rig.pivotY - rig.handY) / SpriteLibrary.PixelsPerUnit;
            return sprites;
        }

        /// <summary>The head from the first frame, for the party cards and the turn-order strip.</summary>
        static Sprite Portrait(Pixels sheet, RigInfo rig)
        {
            // Head space: the face is around (24, 30) from the head's origin.
            int left = rig.headX[0] + 24 - PortraitSize / 2, top = rig.headY[0] + 26 - PortraitSize / 2;
            var pixels = new Color32[PortraitSize * PortraitSize];
            for (int y = 0; y < PortraitSize; y++)
            {
                for (int x = 0; x < PortraitSize; x++)
                {
                    int sx = left + x, sy = top + y;
                    if (sx < 0 || sy < 0 || sx >= rig.cellW || sy >= rig.cellH) continue;
                    pixels[(PortraitSize - 1 - y) * PortraitSize + x] = sheet.Data[(sheet.Height - 1 - sy) * sheet.Width + sx];
                }
            }
            var texture = new Texture2D(PortraitSize, PortraitSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, PortraitSize, PortraitSize), new Vector2(0.5f, 0.5f), SpriteLibrary.PixelsPerUnit);
        }

        static bool HasArt(PartInfo part) => part != null && !string.IsNullOrEmpty(part.path);

        static void DrawLayer(Pixels sheet, RigInfo rig, int cellX, int cellY, RigLayer layer, int frame, Dictionary<int, Color32> recolor)
        {
            if (layer == null) return;
            var source = Load(layer.path);
            if (source == null) return;
            int srcX = frame % layer.columns * layer.w, srcY = frame / layer.columns * layer.h;
            Blit(sheet, rig, cellX, cellY, source, srcX, srcY, layer.w, layer.h, layer.x, layer.y, recolor);
        }

        static void DrawPart(Pixels sheet, RigInfo rig, int cellX, int cellY, PartInfo part, int headX, int headY, Dictionary<int, Color32> recolor)
        {
            if (!HasArt(part)) return;
            var source = Load(part.path);
            if (source == null) return;
            Blit(sheet, rig, cellX, cellY, source, 0, 0, source.Width, source.Height, headX + part.x, headY + part.y, recolor);
        }

        /// <summary>
        /// Alpha-over of a source rectangle into one cell of the sheet, clipped to the cell. Positions count from the
        /// top-left; the pixel arrays run bottom row first, hence the flipped row indices.
        /// </summary>
        static void Blit(Pixels sheet, RigInfo rig, int cellX, int cellY, Pixels source, int srcX, int srcY, int width, int height,
            int destX, int destY, Dictionary<int, Color32> recolor)
        {
            for (int y = 0; y < height; y++)
            {
                int dy = destY + y;
                if (dy < 0 || dy >= rig.cellH) continue;
                int srcRow = (source.Height - 1 - (srcY + y)) * source.Width + srcX;
                int destRow = (sheet.Height - 1 - (cellY + dy)) * sheet.Width + cellX;
                for (int x = 0; x < width; x++)
                {
                    int dx = destX + x;
                    if (dx < 0 || dx >= rig.cellW) continue;
                    var color = source.Data[srcRow + x];
                    if (color.a == 0) continue;
                    if (recolor != null && recolor.TryGetValue(Rgb(color), out var swapped)) color = new Color32(swapped.r, swapped.g, swapped.b, color.a);
                    int index = destRow + dx;
                    sheet.Data[index] = color.a == 255 ? color : Over(color, sheet.Data[index]);
                }
            }
        }

        static Color32 Over(Color32 top, Color32 under)
        {
            if (under.a == 0) return top;
            int a = top.a, outA = a * 255 + under.a * (255 - a);
            byte Mix(int s, int d) => (byte)((s * a * 255 + d * under.a * (255 - a) + outA / 2) / outA);
            return new Color32(Mix(top.r, under.r), Mix(top.g, under.g), Mix(top.b, under.b), (byte)((outA + 127) / 255));
        }

        static Pixels Load(string path)
        {
            if (Sources.TryGetValue(path, out var pixels)) return pixels;
            var texture = Resources.Load<Texture2D>("Sprites/" + path);
            if (texture == null || !texture.isReadable)
            {
                Debug.LogWarning($"Hero layer Resources/Sprites/{path} is missing or not readable.");
                pixels = null;
            }
            else
            {
                pixels = new Pixels { Width = texture.width, Height = texture.height, Data = texture.GetPixels32() };
            }
            Sources[path] = pixels;
            return pixels;
        }

        static Dictionary<int, Color32> BodyColors(ArmorInfo armor)
        {
            var map = new Dictionary<int, Color32>();
            for (int i = 0; i < PackCloth.Length && i < armor.cloth.Length; i++) map[Rgb(PackCloth[i])] = Hex(armor.cloth[i]);
            for (int i = 0; i < PackMetal.Length && i < armor.metal.Length; i++) map[Rgb(PackMetal[i])] = Hex(armor.metal[i]);
            return map;
        }

        static int Rgb(Color32 color) => color.r << 16 | color.g << 8 | color.b;

        static Color32 Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var color) ? (Color32)color : new Color32(255, 0, 255, 255);

        /// <summary>No art for this hero: a plain square, like any other missing sprite.</summary>
        static ActorSprites Fallback(string heroId)
        {
            var square = SpriteLibrary.Get("Heroes/" + heroId, new Color(0.3f, 0.5f, 1f));
            var sprites = new ActorSprites { Portrait = square };
            sprites.Add(new SpriteAnim("idle", new[] { square }, 10f, true));
            return sprites;
        }
    }
}

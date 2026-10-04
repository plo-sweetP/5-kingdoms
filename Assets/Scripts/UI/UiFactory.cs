using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FiveKingdoms.UI
{
    /// <summary>
    /// Helpers for building uGUI from code. The HUD is assembled in code so its layout can change quickly; it can move
    /// to prefabs once it settles. Its art is the Tiny Swords UI kit (Resources/Sprites/UI, listed in the art
    /// manifest with 9-slice borders), drawn at a whole number of screen pixels per art pixel on any screen
    /// (<see cref="UiArtScaler"/>): corners and outlines keep their pixels, only the flat middles stretch.
    /// </summary>
    public static class UiFactory
    {
        const int UiLayer = 5;
        const float ArtPixelsPerUnit = 100f; // The canvas's reference pixels per unit: one art pixel is one canvas unit before scaling.

        static readonly Dictionary<string, Sprite> Arts = new Dictionary<string, Sprite>();
        static Font font;

        public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        /// <summary>"Enter Play Mode" keeps statics: sprites made at run time are gone by the next session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Arts.Clear();

        /// <summary>A piece of HUD art by its name in the art manifest ("frame", "tiny_blue", "panel", ...), or null.</summary>
        public static Sprite Art(string name)
        {
            if (Arts.TryGetValue(name, out var sprite)) return sprite;
            var info = ArtManifest.Current.Ui(name);
            var texture = info != null ? Resources.Load<Texture2D>("Sprites/" + info.path) : null;
            if (texture != null)
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), ArtPixelsPerUnit, 0,
                    SpriteMeshType.FullRect, new Vector4(info.left, info.bottom, info.right, info.top));
            }
            else
            {
                Debug.LogWarning($"Missing HUD art '{name}'.");
            }
            Arts[name] = sprite;
            return sprite;
        }

        public static bool HasArt(string name) => ArtManifest.Current.Ui(name) != null;

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Pins a rect to one anchor point of its parent (e.g. (0, 0) = bottom-left) at an offset and size.</summary>
        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>A plain image: a flat colour (no sprite) or any sprite, stretched to its rect.</summary>
        public static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, bool raycast = false)
        {
            var image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>
        /// HUD art stretched over its rect by its 9-slice border (panels, buttons, bars, frames). Its corners stay whole
        /// art pixels on any screen.
        /// </summary>
        public static Image CreatePanel(string name, Transform parent, string art, Color? tint = null, bool raycast = false)
        {
            var image = CreateImage(name, parent, Art(art), tint ?? Color.white, raycast);
            image.type = Image.Type.Sliced;
            UiArtScaler.Register(image);
            return image;
        }

        /// <summary>
        /// HUD art at its own size (icons, portraits, the D-pad): the rect takes the art's size in whole screen pixels per
        /// art pixel. With <paramref name="sprite"/> the image shows that sprite instead of a named piece.
        /// </summary>
        public static Image CreateIcon(string name, Transform parent, string art, Color? tint = null, Sprite sprite = null)
        {
            var image = CreateImage(name, parent, sprite != null ? sprite : art != null ? Art(art) : null, tint ?? Color.white);
            UiArtScaler.RegisterIcon(image);
            return image;
        }

        public static Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment, Color color)
        {
            var text = CreateRect(name, parent).gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.086f, 0.11f, 0.18f, 0.9f); // The pack's outline colour.
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        /// <summary>
        /// A bar: a dark trough in the pack's outline and a fill whose width is set through its anchorMax.x. The fill is
        /// white art tinted with <paramref name="color"/>.
        /// </summary>
        public static Image CreateBar(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var back = CreatePanel(name + "Back", parent, "bar_slim");
            Place(back.rectTransform, new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
            var fill = CreateImage(name + "Fill", back.transform, null, color);
            var rect = fill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            UiArtScaler.RegisterInset(rect, 2); // Inside the trough's 2 px outline.
            return fill;
        }
    }

    /// <summary>
    /// Keeps HUD art on whole screen pixels. The canvas scales with the screen's height, so one art pixel would cover
    /// a fractional number of screen pixels on most screens; this picks a whole number instead (2 on a 1080p screen,
    /// like the world's zoom) and sizes sliced borders, icons and insets to match. Sits on the HUD's canvas.
    /// </summary>
    public sealed class UiArtScaler : MonoBehaviour
    {
        const float ReferenceHeight = 1080f;

        static UiArtScaler current;
        readonly List<Image> panels = new List<Image>();
        readonly List<Image> icons = new List<Image>();
        readonly List<(RectTransform rect, int pixels)> insets = new List<(RectTransform, int)>();
        int appliedHeight;

        /// <summary>Canvas units one art pixel covers right now (2 on a 1080p screen; between 1.2 and 2.1 elsewhere).</summary>
        public static float UnitsPerArtPixel { get; private set; } = 2f;

        public static void Register(Image image)
        {
            if (current == null) return;
            current.panels.Add(image);
            image.pixelsPerUnitMultiplier = 1f / UnitsPerArtPixel;
        }

        public static void RegisterIcon(Image image)
        {
            if (current != null) current.icons.Add(image);
            Size(image);
        }

        public static void RegisterInset(RectTransform rect, int artPixels)
        {
            if (current != null) current.insets.Add((rect, artPixels));
            Inset(rect, artPixels);
        }

        /// <summary>Call after swapping an icon's sprite for one of another size.</summary>
        public static void Size(Image image)
        {
            if (image.sprite != null) image.rectTransform.sizeDelta = image.sprite.rect.size * UnitsPerArtPixel;
        }

        static void Inset(RectTransform rect, int artPixels)
        {
            float inset = artPixels * UnitsPerArtPixel;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        void Awake()
        {
            current = this;
            Apply();
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }

        void Update()
        {
            if (Screen.height != appliedHeight) Apply();
        }

        void Apply()
        {
            appliedHeight = Screen.height;
            float canvasScale = appliedHeight / ReferenceHeight;
            int screenPixels = Mathf.Max(1, Mathf.FloorToInt(2f * canvasScale + 0.25f));
            UnitsPerArtPixel = screenPixels / canvasScale;
            panels.RemoveAll(image => image == null);
            icons.RemoveAll(image => image == null);
            insets.RemoveAll(entry => entry.rect == null);
            foreach (var image in panels) image.pixelsPerUnitMultiplier = 1f / UnitsPerArtPixel;
            foreach (var image in icons) Size(image);
            foreach (var (rect, pixels) in insets) Inset(rect, pixels);
        }
    }
}

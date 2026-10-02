using UnityEngine;
using UnityEngine.UI;

namespace FiveKingdoms.UI
{
    /// <summary>
    /// Helpers for building uGUI from code. The prototype HUD is assembled in code so its layout can change
    /// quickly; it can move to prefabs once it settles. Shapes are generated, so no UI art is needed yet.
    /// </summary>
    public static class UiFactory
    {
        const int UiLayer = 5;

        static Font font;
        static Sprite circle;
        static Sprite roundedRect;
        static Sprite triangle;

        public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        public static Sprite Circle => circle != null ? circle : (circle = MakeCircle(128));
        public static Sprite RoundedRect => roundedRect != null ? roundedRect : (roundedRect = MakeRoundedRect(64, 18));
        public static Sprite Triangle => triangle != null ? triangle : (triangle = MakeTriangle(64));

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

        public static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, bool raycast = false)
        {
            var image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
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
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        static Texture2D NewTexture(int width, int height) =>
            new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

        static Sprite MakeCircle(int size)
        {
            var texture = NewTexture(size, size);
            var pixels = new Color32[size * size];
            float radius = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius, dy = y + 0.5f - radius;
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>White rounded rectangle with 9-slice borders, for panels and pill buttons.</summary>
        static Sprite MakeRoundedRect(int size, int radius)
        {
            var texture = NewTexture(size, size);
            var pixels = new Color32[size * size];
            float half = size / 2f, inner = half - radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - inner, 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - inner, 0f);
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            float border = radius + 1;
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        /// <summary>Upward-pointing triangle, anti-aliased by 4x4 supersampling.</summary>
        static Sprite MakeTriangle(int size)
        {
            var texture = NewTexture(size, size);
            var pixels = new Color32[size * size];
            var a = new Vector2(size * 0.5f, size * 0.88f);
            var b = new Vector2(size * 0.12f, size * 0.18f);
            var c = new Vector2(size * 0.88f, size * 0.18f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            if (InTriangle(new Vector2(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f), a, b, c)) inside++;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(inside * 255 / 16));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float Cross(Vector2 u, Vector2 v, Vector2 w) => (v.x - u.x) * (w.y - u.y) - (v.y - u.y) * (w.x - u.x);
            float d1 = Cross(a, b, p), d2 = Cross(b, c, p), d3 = Cross(c, a, p);
            bool hasNegative = d1 < 0 || d2 < 0 || d3 < 0, hasPositive = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNegative && hasPositive);
        }
    }
}

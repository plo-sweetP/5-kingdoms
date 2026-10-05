using FiveKingdoms.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FiveKingdoms.UI
{
    /// <summary>How large the minimap is drawn (the pause menu's setting).</summary>
    public enum MinimapSize { Off, Small, Large }

    /// <summary>
    /// The minimap (HUD.md, "Minimap"): the whole floor, see-through, top right under the button row. It shows what
    /// the run says the party has explored (<see cref="DungeonRun.IsExplored"/>) and marks the hero the player
    /// controls (white, blinking slowly), the partners (blue), the foes the party sees right now (red,
    /// <see cref="DungeonRun.PartySees"/>), items on explored tiles (yellow) and the way down once its tile is explored
    /// (green, pointing down). Three texels a tile, so the marks have shapes; a texel covers a whole number of screen
    /// pixels (6 a tile on a 1080p screen for Small, 9 for Large). It takes no input.
    /// </summary>
    public sealed class Minimap : MonoBehaviour
    {
        const int Sub = 3;                 // Texels per tile, each way.
        const float ReferenceHeight = 1080f;
        const float BlinkSeconds = 1.4f;

        static readonly Color32 VeilColor = new Color32(10, 13, 23, 90);       // The dark veil: about 35%.
        static readonly Color32 FloorColor = new Color32(206, 214, 220, 153);  // Explored floor: about 60%.
        static readonly Color32 PartnerColor = new Color32(92, 158, 242, 255);
        static readonly Color32 EnemyColor = new Color32(235, 72, 62, 255);
        static readonly Color32 ItemColor = new Color32(255, 214, 64, 255);
        static readonly Color32 StairsColor = new Color32(120, 255, 170, 255);

        // Marks, bottom row first.
        static readonly string[] Full = { "XXX", "XXX", "XXX" };
        static readonly string[] Diamond = { ".X.", "XXX", ".X." };
        static readonly string[] Down = { ".X.", "X.X", "X.X" };

        RawImage image;
        RectTransform rect;
        Image leader;
        Texture2D texture;
        Color32[] pixels;
        MinimapSize size = MinimapSize.Small;
        int appliedHeight;
        GridPos leaderTile;

        public static Minimap Create(Transform parent, Vector2 topRight)
        {
            var map = UiFactory.CreateRect("Minimap", parent).gameObject.AddComponent<Minimap>();
            map.rect = (RectTransform)map.transform;
            UiFactory.Place(map.rect, new Vector2(1f, 1f), topRight, Vector2.zero, new Vector2(1f, 1f));
            map.image = map.gameObject.AddComponent<RawImage>();
            map.image.raycastTarget = false;
            map.leader = UiFactory.CreateImage("Leader", map.transform, null, Color.white);
            map.leader.rectTransform.anchorMin = map.leader.rectTransform.anchorMax = map.leader.rectTransform.pivot = Vector2.zero;
            return map;
        }

        public void SetSize(MinimapSize value)
        {
            size = value;
            gameObject.SetActive(value != MinimapSize.Off);
            Layout();
        }

        /// <summary>Redraws from the run's state (after every action, and on a new floor).</summary>
        public void Refresh(DungeonRun run)
        {
            var map = run.Map;
            if (texture == null || texture.width != map.Width * Sub || texture.height != map.Height * Sub)
            {
                if (texture != null) Destroy(texture);
                texture = new Texture2D(map.Width * Sub, map.Height * Sub, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                pixels = new Color32[texture.width * texture.height];
                image.texture = texture;
                Layout();
            }

            for (int i = 0; i < pixels.Length; i++) pixels[i] = VeilColor;
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (run.IsExplored(new GridPos(x, y))) Stamp(new GridPos(x, y), Full, FloorColor);
            if (!run.IsBossFloor && run.IsExplored(map.Stairs)) Stamp(map.Stairs, Down, StairsColor);
            foreach (var item in run.Items)
                if (run.IsExplored(item.Pos)) Stamp(item.Pos, Diamond, ItemColor);
            foreach (var actor in run.Actors)
            {
                if (!actor.IsAlive || actor == run.Hero) continue;
                if (actor.Team == Team.Hero) Stamp(actor.Pos, Full, PartnerColor);
                else if (run.PartySees(actor.Pos)) Stamp(actor.Pos, Full, EnemyColor);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);

            leaderTile = run.Hero.Pos;
            PlaceLeader();
        }

        void Stamp(GridPos tile, string[] mark, Color32 color)
        {
            if (tile.X < 0 || tile.Y < 0 || tile.X * Sub >= texture.width || tile.Y * Sub >= texture.height) return;
            for (int dy = 0; dy < Sub; dy++)
                for (int dx = 0; dx < Sub; dx++)
                    if (mark[dy][dx] == 'X') pixels[(tile.Y * Sub + dy) * texture.width + tile.X * Sub + dx] = color;
        }

        /// <summary>Canvas units one texel covers: a whole number of screen pixels on any screen.</summary>
        float TexelUnits()
        {
            float canvasScale = Mathf.Max(1, Screen.height) / ReferenceHeight;
            int screenPixels = Mathf.Max(1, Mathf.RoundToInt((size == MinimapSize.Large ? 3f : 2f) * canvasScale));
            return screenPixels / canvasScale;
        }

        void Layout()
        {
            appliedHeight = Screen.height;
            if (texture == null) return;
            rect.sizeDelta = new Vector2(texture.width, texture.height) * TexelUnits();
            PlaceLeader();
        }

        void PlaceLeader()
        {
            float tile = Sub * TexelUnits();
            leader.rectTransform.sizeDelta = new Vector2(tile, tile);
            leader.rectTransform.anchoredPosition = new Vector2(leaderTile.X, leaderTile.Y) * tile;
        }

        void Update()
        {
            if (Screen.height != appliedHeight) Layout();
            // The player's own hero blinks slowly, so it is found at a glance among the partners.
            float wave = 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * 2f * Mathf.PI / BlinkSeconds);
            leader.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.35f, 1f, wave));
        }

        void OnDestroy()
        {
            if (texture != null) Destroy(texture);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace FiveKingdoms
{
    /// <summary>
    /// How the camera keeps an aimed action's targets in view (docs/design/ART.md, "View size"). Peter's choice
    /// (2026-10-04): ZoomOut is the game's behavior; Wide stays for a player setting later; Lead looked wrong to him
    /// (the heroes slide to the edge of the screen) and is only a debug flag now.
    /// </summary>
    public enum ViewMode
    {
        /// <summary>The usual zoom; while aiming, the camera moves toward the targets, however far.</summary>
        Lead,

        /// <summary>
        /// The usual zoom. While aiming, a small nudge of the camera if that shows every target; otherwise one whole
        /// step out, and back when the aim ends.
        /// </summary>
        ZoomOut,

        /// <summary>Always one whole step further out: more of the map, smaller sprites.</summary>
        Wide,
    }

    /// <summary>
    /// Orthographic camera that keeps pixel art crisp on any screen: it picks the whole-number zoom that shows closest
    /// to <see cref="TilesHigh"/> tiles vertically (the Near view), or one whole step further out when the player
    /// chose the Far view (<see cref="Far"/>; docs/design/HUD.md, "The view on the tablet"), and snaps to screen
    /// pixels. It follows a target smoothly, can shake, and while the player aims it frames the targets so that none
    /// of them is off screen.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PixelCamera : MonoBehaviour
    {
        /// <summary>The zoom and the place the camera looks at while an aim's targets are framed.</summary>
        public readonly struct Framing
        {
            public readonly int Zoom;
            public readonly Vector3 Position;

            public Framing(int zoom, Vector3 position)
            {
                Zoom = zoom;
                Position = position;
            }
        }

        public Transform Target;
        public float TilesHigh = 11f;
        public float FollowSharpness = 14f;
        /// <summary>The game's view behavior while aiming; `-fk-view` sets another one (for debugging).</summary>
        public ViewMode Mode = ViewMode.ZoomOut;

        /// <summary>
        /// The player's view (Settings, "View"): Near is the zoom the rule picks, Far is one whole step further out.
        /// On a screen where the rule already picks 1x there is no Far view (<see cref="FarAvailable"/>) and this
        /// changes nothing. The camera while aiming works from the chosen view.
        /// </summary>
        public bool Far;

        /// <summary>
        /// What the HUD covers at the screen's edges, as shares of the screen's height (the HUD's size follows the
        /// height): the floor title and the boss's bar on top and the message log below, the party and the buttons at
        /// the sides. Framed targets stay out from under it where the screen is big enough.
        /// </summary>
        const float HudTopBottom = 0.17f, HudSides = 0.35f;

        /// <summary>World units kept clear inside that, for the sprite that stands on a framed tile.</summary>
        const float SpriteMargin = 0.5f;

        /// <summary>
        /// World units kept clear at the screen's edges where the targets are too far apart for the room the HUD
        /// leaves: all of them on screen, some under the HUD, is better than one of them off screen.
        /// </summary>
        const float EdgeMargin = 1.25f;

        /// <summary>
        /// How far the camera may move off the one aiming before stepping out is better than sliding, as a share of
        /// half the view: the party stays in the middle third of the screen.
        /// </summary>
        const float MaxNudge = 0.35f;

        Camera cam;
        Vector3 focus;
        float shakeTime;
        float shakeStrength;
        int screenHeight;
        int baseZoom = 1;
        int appliedHeightLogged;
        int zoom = 1;
        float worldPerScreenPixel = 1f;
        ViewMode appliedMode;
        bool appliedFar;
        Vector3? framed;      // Where the camera looks while aiming, instead of at the target.
        bool framedWide;      // The aim's targets need the zoomed-out view.

        /// <summary>Screen pixels per art pixel right now (a whole number).</summary>
        public int Zoom => zoom;

        /// <summary>Whether this screen has a Far view: only where the Near view plays above 1x.</summary>
        public bool FarAvailable => HasFarView(Screen.height, TilesHigh);

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            UpdateZoom();
        }

        public void SnapToTarget()
        {
            if (Target != null) focus = framed ?? Target.position;
            Apply();
        }

        public void Shake(float strength, float duration)
        {
            shakeStrength = Mathf.Max(shakeStrength, strength);
            shakeTime = Mathf.Max(shakeTime, duration);
        }

        /// <summary>
        /// Aiming: keep these points on screen (the first is the one aiming, the second, if given, the target marked
        /// right now), by the rule in <see cref="FrameFor"/>. Null (or no points) ends it.
        /// </summary>
        public void Frame(IReadOnlyList<Vector3> points, Vector3? marked = null)
        {
            if (points == null || points.Count == 0)
            {
                framed = null;
                framedWide = false;
                SetZoom(baseZoom);
                return;
            }
            var framing = FrameFor(Mode, screenHeight, cam.aspect, baseZoom, points, marked);
            framedWide = framing.Zoom < baseZoom;
            SetZoom(framing.Zoom);
            framed = framing.Position;
        }

        /// <summary>
        /// The whole-number zoom a screen plays at: the one that shows closest to <paramref name="tilesHigh"/> tiles top
        /// to bottom (2x on a 1080p phone), one step further out in the Far view (<paramref name="far"/>), and one
        /// more in <see cref="ViewMode.Wide"/>. Never below 1x, never a fraction.
        /// </summary>
        public static int BaseZoomFor(ViewMode mode, int screenHeight, float tilesHigh, bool far = false)
        {
            int zoom = NearZoomFor(screenHeight, tilesHigh);
            if (far && zoom > 1) zoom--;
            return mode == ViewMode.Wide && zoom > 1 ? zoom - 1 : zoom;
        }

        /// <summary>The zoom of the Near view: the whole number that shows closest to <paramref name="tilesHigh"/> tiles top to bottom.</summary>
        public static int NearZoomFor(int screenHeight, float tilesHigh) =>
            Mathf.Max(1, Mathf.RoundToInt(screenHeight / (SpriteLibrary.PixelsPerUnit * tilesHigh)));

        /// <summary>Whether a screen has a Far view at all: one whole step out of a Near view above 1x.</summary>
        public static bool HasFarView(int screenHeight, float tilesHigh) => NearZoomFor(screenHeight, tilesHigh) > 1;

        /// <summary>
        /// The framing rule on its own, for any screen (the tests check it for the phone, the tablet and small
        /// screens). The camera moves no further off the one aiming (the first point) than it must to show every
        /// point clear of the HUD. In <see cref="ViewMode.ZoomOut"/> it steps out a whole zoom level instead when they
        /// don't fit, or fit only by sliding the party out of the middle of the screen. Where the room the HUD leaves
        /// is too small even then (or it can't step out: a small screen is already at 1x), points may sit under the
        /// HUD; where the screen itself is too small, the camera looks between the one aiming and the marked target.
        /// </summary>
        public static Framing FrameFor(ViewMode mode, int screenHeight, float aspect, int baseZoom,
            IReadOnlyList<Vector3> points, Vector3? marked = null)
        {
            var aiming = points[0];
            var bounds = new Bounds(aiming, Vector3.zero);
            foreach (var point in points) bounds.Encapsulate(point);

            var clear = HalfView(screenHeight, aspect, baseZoom, true);
            bool close = Fits(bounds, clear)
                && (mode != ViewMode.ZoomOut || IsSmallNudge(Nearest(bounds, aiming, clear) - aiming, screenHeight, aspect, baseZoom));
            int zoom = !close && mode == ViewMode.ZoomOut && baseZoom > 1 ? baseZoom - 1 : baseZoom;

            clear = HalfView(screenHeight, aspect, zoom, true);
            if (Fits(bounds, clear)) return new Framing(zoom, Nearest(bounds, aiming, clear));
            var onScreen = HalfView(screenHeight, aspect, zoom, false);
            if (Fits(bounds, onScreen)) return new Framing(zoom, Nearest(bounds, aiming, onScreen));
            return new Framing(zoom, (aiming + (marked ?? bounds.center)) / 2f);
        }

        /// <summary>
        /// Half of the view that framed points may use at a zoom, in world units: what the HUD leaves clear, or, with
        /// <paramref name="clearOfHud"/> off, everything but the screen's edges.
        /// </summary>
        static Vector2 HalfView(int screenHeight, float aspect, int atZoom, bool clearOfHud)
        {
            float viewHeight = (float)screenHeight / (SpriteLibrary.PixelsPerUnit * atZoom);
            return clearOfHud
                ? new Vector2(viewHeight * (aspect / 2f - HudSides) - SpriteMargin, viewHeight * (0.5f - HudTopBottom) - SpriteMargin)
                : new Vector2(viewHeight * aspect / 2f - EdgeMargin, viewHeight / 2f - EdgeMargin);
        }

        static bool Fits(Bounds bounds, Vector2 half) => bounds.extents.x <= half.x && bounds.extents.y <= half.y;

        /// <summary>The camera position nearest to a point that shows all of the bounds (which must fit).</summary>
        static Vector3 Nearest(Bounds bounds, Vector3 from, Vector2 half) => new Vector3(
            Mathf.Clamp(from.x, bounds.max.x - half.x, bounds.min.x + half.x),
            Mathf.Clamp(from.y, bounds.max.y - half.y, bounds.min.y + half.y),
            from.z);

        static bool IsSmallNudge(Vector3 offset, int screenHeight, float aspect, int atZoom)
        {
            float halfHeight = screenHeight / (2f * SpriteLibrary.PixelsPerUnit * atZoom);
            return Mathf.Abs(offset.x) <= MaxNudge * halfHeight * aspect && Mathf.Abs(offset.y) <= MaxNudge * halfHeight;
        }

        void LateUpdate()
        {
            if (Screen.height != screenHeight || Mode != appliedMode || Far != appliedFar) UpdateZoom();
            var goal = framed ?? (Target != null ? Target.position : focus);
            focus = Vector3.Lerp(focus, goal, 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime));
            Apply();
        }

        void UpdateZoom()
        {
            screenHeight = Screen.height;
            appliedMode = Mode;
            bool farBefore = appliedFar;
            appliedFar = Far;
            int before = baseZoom;
            baseZoom = BaseZoomFor(Mode, screenHeight, TilesHigh, Far);
            // In the player's log: what a device really plays at (a tablet's view is judged from this, HUD.md).
            if (baseZoom != before || appliedHeightLogged != screenHeight || Far != farBefore)
            {
                appliedHeightLogged = screenHeight;
                Debug.Log(DescribeView(Screen.width, screenHeight, TilesHigh, Mode, Far));
            }
            SetZoom(framedWide && baseZoom > 1 ? baseZoom - 1 : baseZoom);
        }

        /// <summary>The log's line about the view: "View: W x H at zoom N, T tiles high (Near)", or Far, or that the screen has one view only.</summary>
        public static string DescribeView(int screenWidth, int screenHeight, float tilesHigh, ViewMode mode, bool far)
        {
            int zoom = BaseZoomFor(mode, screenHeight, tilesHigh, far);
            string view = !HasFarView(screenHeight, tilesHigh) ? "Near, the only view on this screen" : far ? "Far" : "Near";
            return $"View: {screenWidth} x {screenHeight} at zoom {zoom}, " +
                   $"{screenHeight / (float)(SpriteLibrary.PixelsPerUnit * zoom):0.0} tiles high ({view}).";
        }

        void SetZoom(int value)
        {
            zoom = Mathf.Max(1, value);
            cam.orthographicSize = screenHeight / (2f * SpriteLibrary.PixelsPerUnit * zoom);
            worldPerScreenPixel = 1f / (SpriteLibrary.PixelsPerUnit * zoom);
        }

        void Apply()
        {
            var position = focus;
            if (shakeTime > 0f)
            {
                shakeTime -= Time.deltaTime;
                position += (Vector3)(Random.insideUnitCircle * shakeStrength);
                if (shakeTime <= 0f) shakeStrength = 0f;
            }
            // Snap to whole screen pixels so sprites don't shimmer while the camera scrolls.
            position.x = Mathf.Round(position.x / worldPerScreenPixel) * worldPerScreenPixel;
            position.y = Mathf.Round(position.y / worldPerScreenPixel) * worldPerScreenPixel;
            position.z = -10f;
            transform.position = position;
        }
    }
}

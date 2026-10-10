using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// The camera's rule while aiming (docs/design/ART.md, "View size"; Peter's choice on 2026-10-04): every target in
    /// view, the party near the middle of the screen, whole zoom steps only. Checked for the screens the game runs on,
    /// without a scene: the rule is plain arithmetic.
    /// </summary>
    public class PixelCameraTests
    {
        const float TilesHigh = 11f;
        static readonly Vector3 Hero = new Vector3(20.5f, 12.5f, 0f);

        // Phone 19.5:9 at 1080p, the two smoke-test windows, and Peter's devices (S22 Ultra, Tab S8+).
        static readonly Vector2Int Phone = new Vector2Int(2340, 1080);
        static readonly Vector2Int SmallPhone = new Vector2Int(1560, 720);
        static readonly Vector2Int SmallTablet = new Vector2Int(1440, 900);
        static readonly Vector2Int S22Ultra = new Vector2Int(3088, 1440);
        static readonly Vector2Int TabS8Plus = new Vector2Int(2800, 1752);

        static List<Vector3> Points(params Vector2[] targets)
        {
            var points = new List<Vector3> { Hero };
            foreach (var target in targets) points.Add(Hero + (Vector3)target);
            return points;
        }

        /// <summary>The hero aims at targets given as tile offsets; the first one is marked.</summary>
        static PixelCamera.Framing Aim(ViewMode mode, Vector2Int screen, params Vector2[] targets)
        {
            var points = Points(targets);
            int baseZoom = PixelCamera.BaseZoomFor(mode, screen.y, TilesHigh);
            return PixelCamera.FrameFor(mode, screen.y, screen.x / (float)screen.y, baseZoom, points, points[1]);
        }

        static void AssertStaysOnHero(PixelCamera.Framing framing, string why)
        {
            Assert.AreEqual(Hero.x, framing.Position.x, 0.001f, why);
            Assert.AreEqual(Hero.y, framing.Position.y, 0.001f, why);
        }

        /// <summary>Every point is on screen with its sprite: at least half a tile inside the edges.</summary>
        static void AssertAllOnScreen(PixelCamera.Framing framing, Vector2Int screen, params Vector2[] targets)
        {
            float halfHeight = screen.y / (2f * SpriteLibrary.PixelsPerUnit * framing.Zoom);
            float halfWidth = halfHeight * screen.x / screen.y;
            foreach (var point in Points(targets))
            {
                Assert.LessOrEqual(Mathf.Abs(point.x - framing.Position.x), halfWidth - 0.5f, $"{point} is on screen sideways");
                Assert.LessOrEqual(Mathf.Abs(point.y - framing.Position.y), halfHeight - 0.5f, $"{point} is on screen top to bottom");
            }
        }

        [Test]
        public void TheZoomIsAWholeNumberNearElevenTilesHigh()
        {
            Assert.AreEqual(1, PixelCamera.BaseZoomFor(ViewMode.ZoomOut, 720, TilesHigh));
            Assert.AreEqual(1, PixelCamera.BaseZoomFor(ViewMode.ZoomOut, 900, TilesHigh));
            Assert.AreEqual(2, PixelCamera.BaseZoomFor(ViewMode.ZoomOut, 1080, TilesHigh));
            Assert.AreEqual(2, PixelCamera.BaseZoomFor(ViewMode.ZoomOut, 1440, TilesHigh));
            Assert.AreEqual(2, PixelCamera.BaseZoomFor(ViewMode.ZoomOut, 1752, TilesHigh));
            Assert.AreEqual(3, PixelCamera.BaseZoomFor(ViewMode.ZoomOut, 2160, TilesHigh));
            Assert.AreEqual(1, PixelCamera.BaseZoomFor(ViewMode.Wide, 1080, TilesHigh), "Wide is one step out");
            Assert.AreEqual(1, PixelCamera.BaseZoomFor(ViewMode.Wide, 720, TilesHigh), "never below 1x");
        }

        [Test]
        public void TargetsNearbyDoNotMoveTheCamera()
        {
            var targets = new[] { new Vector2(1f, 1f), new Vector2(0f, 2f), new Vector2(-4f, -1f) };
            var framing = Aim(ViewMode.ZoomOut, Phone, targets);
            Assert.AreEqual(2, framing.Zoom);
            AssertStaysOnHero(framing, "everything is in view already");
        }

        [Test]
        public void ATargetThreeTilesUpNudgesTheCameraAtFullZoom()
        {
            var framing = Aim(ViewMode.ZoomOut, Phone, new Vector2(0f, 3f));
            Assert.AreEqual(2, framing.Zoom);
            Assert.AreEqual(Hero.x, framing.Position.x, 0.001f);
            float nudge = framing.Position.y - Hero.y;
            Assert.Greater(nudge, 0.3f, "the camera moves up toward the target");
            Assert.Less(nudge, 1.5f, "but the hero stays in the middle third of the screen");
            // The target clears the floor title and the boss's bar: 17% of the screen's height, 1.43 tiles at 2x.
            float halfHeight = Phone.y / (2f * SpriteLibrary.PixelsPerUnit * framing.Zoom);
            Assert.LessOrEqual(Hero.y + 3f - framing.Position.y, halfHeight - 1.43f - 0.49f);
        }

        [Test]
        public void TargetsFiveTilesUpAndDownStepTheViewOutWithoutSliding()
        {
            var targets = new[] { new Vector2(0f, 5f), new Vector2(0f, -5f) };
            foreach (var screen in new[] { Phone, S22Ultra, TabS8Plus })
            {
                var framing = Aim(ViewMode.ZoomOut, screen, targets);
                Assert.AreEqual(1, framing.Zoom, $"{screen}: one whole step out of 2x");
                AssertStaysOnHero(framing, $"{screen}: both fit at 1x around the hero");
                AssertAllOnScreen(framing, screen, targets);
            }
        }

        [Test]
        public void ATargetFarUpStepsOutInsteadOfSlidingThePartyToTheEdge()
        {
            // At 2x on the phone the camera would have to move 2.7 tiles of the 4.2 between the middle and the edge.
            var framing = Aim(ViewMode.ZoomOut, Phone, new Vector2(0f, 5f));
            Assert.AreEqual(1, framing.Zoom);
            AssertStaysOnHero(framing, "at 1x it is in view without a move");
        }

        [Test]
        public void TallerScreensNudgeWhereThePhoneStepsOut()
        {
            foreach (var screen in new[] { S22Ultra, TabS8Plus })
            {
                var framing = Aim(ViewMode.ZoomOut, screen, new Vector2(0f, 5f));
                Assert.AreEqual(2, framing.Zoom, $"{screen}");
                float halfHeight = screen.y / (2f * SpriteLibrary.PixelsPerUnit * framing.Zoom);
                Assert.LessOrEqual(framing.Position.y - Hero.y, 0.35f * halfHeight + 0.001f, $"{screen}: a small nudge");
                AssertAllOnScreen(framing, screen, new Vector2(0f, 5f));
            }
        }

        [Test]
        public void SidewaysShotsStayAtFullZoomOnAWideScreen()
        {
            var targets = new[] { new Vector2(5f, 0f), new Vector2(-5f, 0f) };
            var framing = Aim(ViewMode.ZoomOut, Phone, targets);
            Assert.AreEqual(2, framing.Zoom);
            AssertStaysOnHero(framing, "five tiles to each side fit between the party cards and the buttons");
        }

        [Test]
        public void ASmallScreenCannotStepOut()
        {
            var targets = new[] { new Vector2(0f, 5f), new Vector2(0f, -5f) };

            // 900 high: both are on screen around the hero, close to the HUD's top and bottom.
            var tablet = Aim(ViewMode.ZoomOut, SmallTablet, targets);
            Assert.AreEqual(1, tablet.Zoom);
            AssertStaysOnHero(tablet, "both fit on screen");
            AssertAllOnScreen(tablet, SmallTablet, targets);

            // 720 high shows 11 tiles: they can't both be seen, so the hero and the marked one are.
            var phone = Aim(ViewMode.ZoomOut, SmallPhone, targets);
            Assert.AreEqual(1, phone.Zoom);
            Assert.AreEqual(Hero.y + 2.5f, phone.Position.y, 0.001f, "between the hero and the marked target");
            AssertAllOnScreen(phone, SmallPhone, targets[0]);

            // One target is no trouble there either.
            var single = Aim(ViewMode.ZoomOut, SmallPhone, new Vector2(0f, 5f));
            Assert.AreEqual(1, single.Zoom);
            AssertAllOnScreen(single, SmallPhone, new Vector2(0f, 5f));
        }

        [Test]
        public void TheOtherModesNeverChangeTheZoomWhileAiming()
        {
            var targets = new[] { new Vector2(0f, 5f), new Vector2(0f, -5f) };

            var wide = Aim(ViewMode.Wide, Phone, targets);
            Assert.AreEqual(1, wide.Zoom, "Wide plays at 1x on the phone");
            AssertStaysOnHero(wide, "and shows both without a move");

            var lead = Aim(ViewMode.Lead, Phone, targets);
            Assert.AreEqual(2, lead.Zoom, "Lead stays at 2x");
            Assert.AreEqual(Hero.y + 2.5f, lead.Position.y, 0.001f, "and slides toward the marked target");
        }

        // ---- The view: Near and Far (docs/design/HUD.md, "The view on the tablet") ----

        /// <summary>The same aim from the Far view.</summary>
        static PixelCamera.Framing AimFromFar(Vector2Int screen, params Vector2[] targets)
        {
            var points = Points(targets);
            int baseZoom = PixelCamera.BaseZoomFor(ViewMode.ZoomOut, screen.y, TilesHigh, far: true);
            return PixelCamera.FrameFor(ViewMode.ZoomOut, screen.y, screen.x / (float)screen.y, baseZoom, points, points[1]);
        }

        [Test]
        public void TheFarViewIsOneWholeStepOutWhereTheScreenHasOne()
        {
            Assert.AreEqual(1, PixelCamera.BaseZoomFor(ViewMode.ZoomOut, TabS8Plus.y, TilesHigh, far: true), "the tablet: 2x Near, 1x Far");
            Assert.AreEqual(1, PixelCamera.BaseZoomFor(ViewMode.ZoomOut, Phone.y, TilesHigh, far: true), "the phone too");
            Assert.AreEqual(2, PixelCamera.BaseZoomFor(ViewMode.ZoomOut, 2160, TilesHigh, far: true), "a 4K screen: 3x Near, 2x Far");
            Assert.AreEqual(1, PixelCamera.BaseZoomFor(ViewMode.ZoomOut, 900, TilesHigh, far: true), "never below 1x");
            Assert.IsTrue(PixelCamera.HasFarView(TabS8Plus.y, TilesHigh));
            Assert.IsTrue(PixelCamera.HasFarView(Phone.y, TilesHigh));
            Assert.IsFalse(PixelCamera.HasFarView(900, TilesHigh), "at 1x there is no step further out: the choice is greyed out");
            Assert.IsFalse(PixelCamera.HasFarView(720, TilesHigh));
        }

        [Test]
        public void FromAFarViewAt1xTheCameraOnlySlides()
        {
            // Five tiles up and five down fit the tablet's Far view as it stands.
            var both = new[] { new Vector2(0f, 5f), new Vector2(0f, -5f) };
            var framing = AimFromFar(TabS8Plus, both);
            Assert.AreEqual(1, framing.Zoom);
            AssertStaysOnHero(framing, "nothing to move for");

            // A target far to the side: no step out is left, so it slides, and no further than it must.
            var side = new[] { new Vector2(17f, 0f) };
            var slid = AimFromFar(TabS8Plus, side);
            Assert.AreEqual(1, slid.Zoom, "never below 1x, never a fraction");
            AssertAllOnScreen(slid, TabS8Plus, side);
            Assert.Greater(slid.Position.x, Hero.x);
            Assert.Less(slid.Position.x, Hero.x + 17f);

            var phone = AimFromFar(Phone, new Vector2(0f, 5f));
            Assert.AreEqual(1, phone.Zoom);
            AssertAllOnScreen(phone, Phone, new Vector2(0f, 5f));
        }

        [Test]
        public void FromAFarViewAbove1xTheCameraStillStepsOutWhileAiming()
        {
            var screen = new Vector2Int(3840, 2160);
            var near = AimFromFar(screen, new Vector2(0f, 3f));
            Assert.AreEqual(2, near.Zoom, "close targets: the Far view as it is");
            var far = AimFromFar(screen, new Vector2(0f, 9f), new Vector2(0f, -9f));
            Assert.AreEqual(1, far.Zoom, "as from Near: one whole step out when they don't fit");
            AssertAllOnScreen(far, screen, new Vector2(0f, 9f), new Vector2(0f, -9f));
        }

        [Test]
        public void TheLogSaysWhichViewIsOn()
        {
            Assert.AreEqual("View: 2800 x 1752 at zoom 2, 13.7 tiles high (Near).",
                PixelCamera.DescribeView(2800, 1752, TilesHigh, ViewMode.ZoomOut, far: false));
            Assert.AreEqual("View: 2800 x 1752 at zoom 1, 27.4 tiles high (Far).",
                PixelCamera.DescribeView(2800, 1752, TilesHigh, ViewMode.ZoomOut, far: true));
            Assert.AreEqual("View: 1440 x 900 at zoom 1, 14.1 tiles high (Near, the only view on this screen).",
                PixelCamera.DescribeView(1440, 900, TilesHigh, ViewMode.ZoomOut, far: true));
        }

        [Test]
        public void ATabletIsATouchScreenAtLeast600DpOnItsShorterSide()
        {
            // The Tab S8+ by its panel's dpi and by Android's density setting: a tablet either way.
            Assert.AreEqual(ScreenKind.Tablet, PixelCamera.ScreenKindFor(true, TabS8Plus.x, TabS8Plus.y, 266f));
            Assert.AreEqual(ScreenKind.Tablet, PixelCamera.ScreenKindFor(true, TabS8Plus.x, TabS8Plus.y, 340f));
            Assert.AreEqual(ScreenKind.Tablet, PixelCamera.ScreenKindFor(true, TabS8Plus.y, TabS8Plus.x, 266f), "the shorter side, whichever way it is held");
            // Peter's phone at its two resolutions, by the panel's dpi and by the density setting: a phone every way.
            var s22AtFullHd = new Vector2Int(2316, 1080);
            foreach (float dpi in new[] { 375f, 450f, 500f })
                Assert.AreEqual(ScreenKind.Phone, PixelCamera.ScreenKindFor(true, s22AtFullHd.x, s22AtFullHd.y, dpi), $"FHD+ at {dpi} dpi");
            foreach (float dpi in new[] { 500f, 600f })
                Assert.AreEqual(ScreenKind.Phone, PixelCamera.ScreenKindFor(true, S22Ultra.x, S22Ultra.y, dpi), $"WQHD+ at {dpi} dpi");
            Assert.AreEqual(ScreenKind.Phone, PixelCamera.ScreenKindFor(true, TabS8Plus.x, TabS8Plus.y, 0f), "no dpi reported: a phone, so Near");
            Assert.AreEqual(ScreenKind.Desktop, PixelCamera.ScreenKindFor(false, 2560, 1440, 96f), "a PC, however large its monitor");
        }

        [Test]
        public void FarIsTheDefaultOnATabletOnly()
        {
            Assert.IsTrue(PixelCamera.FarByDefault(ScreenKind.Tablet, TabS8Plus.y, TilesHigh));
            Assert.IsFalse(PixelCamera.FarByDefault(ScreenKind.Phone, Phone.y, TilesHigh));
            Assert.IsFalse(PixelCamera.FarByDefault(ScreenKind.Phone, S22Ultra.y, TilesHigh));
            Assert.IsFalse(PixelCamera.FarByDefault(ScreenKind.Desktop, 1440, TilesHigh));
            Assert.IsFalse(PixelCamera.FarByDefault(ScreenKind.Tablet, 800, TilesHigh), "a tablet that plays at 1x has no Far view");
        }

        [Test]
        public void AViewPickedInSettingsIsKeptOverTheDefault()
        {
            Assert.AreEqual(false, FiveKingdoms.Dungeon.DungeonController.PickedView(hasPick: true, far: false, alwaysWide: false), "Near, picked on a tablet, stays");
            Assert.AreEqual(true, FiveKingdoms.Dungeon.DungeonController.PickedView(hasPick: true, far: true, alwaysWide: false));
            Assert.AreEqual(false, FiveKingdoms.Dungeon.DungeonController.PickedView(hasPick: true, far: false, alwaysWide: true), "the newer setting counts");
            Assert.AreEqual(true, FiveKingdoms.Dungeon.DungeonController.PickedView(hasPick: false, far: false, alwaysWide: true), "\"always wide\" was this same view");
            Assert.IsNull(FiveKingdoms.Dungeon.DungeonController.PickedView(hasPick: false, far: false, alwaysWide: false), "nobody picked: the screen's default");
        }

        [Test]
        public void TheNumbersOverTheActorsScaleWithTheView()
        {
            // Half the zoom, half the size: the same proportion to a hero as in Near.
            Assert.AreEqual(1f, PixelCamera.WorldTextScaleFor(ScreenKind.Tablet, 2, 2));
            Assert.AreEqual(0.5f, PixelCamera.WorldTextScaleFor(ScreenKind.Tablet, 1, 2));
            Assert.AreEqual(0.5f, PixelCamera.WorldTextScaleFor(ScreenKind.Desktop, 1, 2));
            Assert.AreEqual(2f / 3f, PixelCamera.WorldTextScaleFor(ScreenKind.Tablet, 2, 3), 0.0001f, "a 4K screen: 3x Near, 2x Far");
            // On a phone they stop at three quarters: the smallest words stay about 1.5 mm high.
            Assert.AreEqual(1f, PixelCamera.WorldTextScaleFor(ScreenKind.Phone, 2, 2));
            Assert.AreEqual(PixelCamera.PhoneTextFloor, PixelCamera.WorldTextScaleFor(ScreenKind.Phone, 1, 2));
            Assert.AreEqual(1f, PixelCamera.WorldTextScaleFor(ScreenKind.Phone, 1, 1), "a screen with one view only");
            Assert.AreEqual(1f, PixelCamera.WorldTextScaleFor(ScreenKind.Desktop, 3, 2), "never larger than in Near");

            Assert.AreEqual(44, FiveKingdoms.UI.DungeonHud.FloatingTextSize(1f, 1f), "a damage number in Near");
            Assert.AreEqual(22, FiveKingdoms.UI.DungeonHud.FloatingTextSize(1f, 0.5f), "and in the tablet's Far view");
            Assert.AreEqual(33, FiveKingdoms.UI.DungeonHud.FloatingTextSize(1f, PixelCamera.PhoneTextFloor), "and in the phone's");
            Assert.AreEqual(23, FiveKingdoms.UI.DungeonHud.FloatingTextSize(0.7f, PixelCamera.PhoneTextFloor), "the smallest word on the phone");
        }

        [Test]
        public void TheLaunchFlagPicksTheView()
        {
            Assert.AreEqual(true, FiveKingdoms.Dungeon.LaunchOptions.FarViewArg("far"));
            Assert.AreEqual(true, FiveKingdoms.Dungeon.LaunchOptions.FarViewArg("Far"));
            Assert.AreEqual(false, FiveKingdoms.Dungeon.LaunchOptions.FarViewArg("near"));
            Assert.IsNull(FiveKingdoms.Dungeon.LaunchOptions.FarViewArg(null), "no flag: the player's setting");
            Assert.IsNull(FiveKingdoms.Dungeon.LaunchOptions.FarViewArg("wide"));
        }
    }
}

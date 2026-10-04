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
    }
}

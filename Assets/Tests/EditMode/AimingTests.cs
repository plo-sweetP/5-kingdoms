using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;
using static FiveKingdoms.Tests.PartyTests;

namespace FiveKingdoms.Tests
{
    /// <summary>What the targeting highlight shows (PROGRESSION.md, "Attack range highlight"): reach and valid targets.</summary>
    public class AimingTests
    {
        static readonly string[] Room =
        {
            "##########",
            "#........#",
            "#........#",
            "#@.......#",
            "#........#",
            "#........#",
            "##########",
        };

        [Test]
        public void AShotTargetsTheFirstFoeOnEachLineInReach()
        {
            var run = Run(new[] { ActorCatalog.Uzuki, ActorCatalog.Haiden }, Room);
            Place(run.Hero, 1, 3);
            Place(run.Party[1], 2, 3);
            var near = Dummy(run, 4, 3);
            Dummy(run, 5, 3); // Behind the first: not a target.
            var diagonal = Dummy(run, 3, 5);
            Dummy(run, 7, 1); // Not on a line.

            var aim = run.AimFor(run.Hero, SkillCatalog.PowerShot);
            Assert.IsTrue(aim.NeedsAim);
            CollectionAssert.AreEquivalent(new[] { near, diagonal }, aim.Options.Select(option => option.Target).ToArray());
            Assert.IsTrue(aim.TryFind(near.Pos, out var east));
            Assert.AreEqual(Direction8.E, east.Direction);
            Assert.Contains(new GridPos(2, 3), aim.Reach.ToList(), "the line runs past Haiden");
            Assert.IsFalse(aim.Reach.Contains(new GridPos(5, 3)), "it stops at the first foe");
        }

        [Test]
        public void AStrikeTargetsTheFoesNextToTheUser()
        {
            var run = Run(new[] { ActorCatalog.Kristela }, Room);
            var east = Dummy(run, 2, 3);
            Dummy(run, 3, 3);
            var aim = run.AimFor(run.Hero, SkillCatalog.PiercingPunch);
            Assert.AreEqual(east, aim.Options.Single().Target);
            Assert.AreEqual(5, aim.Reach.Count, "the five open tiles around her, against the west wall");
        }

        [Test]
        public void TheWeaponAttackAimsLikeTheWeapon()
        {
            var run = Run(new[] { ActorCatalog.Uzuki }, Room);
            var far = Dummy(run, 6, 3);
            Assert.AreEqual(far, run.AimFor(run.Hero, null).Options.Single().Target, "Quick Shot reaches 5 tiles");

            var melee = Run(new[] { ActorCatalog.Haiden }, Room);
            Dummy(melee, 6, 3);
            Assert.AreEqual(0, melee.AimFor(melee.Hero, null).Options.Count, "Sword Slash only reaches the neighbors");
        }

        [Test]
        public void VolleyShowsItsArea()
        {
            var run = Run(new[] { ActorCatalog.Uzuki }, Room);
            Dummy(run, 4, 3);
            var aim = run.AimFor(run.Hero, SkillCatalog.Volley);
            Assert.AreEqual(1, aim.AreaRadius);
            Assert.AreEqual(1, aim.Options.Count);
        }

        [Test]
        public void RollingShotPicksWhereToRoll()
        {
            var run = Run(new[] { ActorCatalog.Uzuki }, Room);
            Place(run.Hero, 4, 3);
            var aim = run.AimFor(run.Hero, SkillCatalog.RollingShot);
            Assert.IsTrue(aim.PicksTile);
            Assert.AreEqual(8, aim.Options.Count, "two tiles every way in an open room");
            Assert.IsTrue(aim.TryFind(new GridPos(2, 3), out var west));
            Assert.AreEqual(Direction8.W, west.Direction);
        }

        [Test]
        public void HealsAndAurasArentAimed()
        {
            var run = Run(new[] { ActorCatalog.Haiden }, Room);
            Assert.IsFalse(run.AimFor(run.Hero, SkillCatalog.PaladinHeal).NeedsAim);
            Assert.IsFalse(run.AimFor(run.Hero, SkillCatalog.AuraOfProtection).NeedsAim);
        }
    }
}

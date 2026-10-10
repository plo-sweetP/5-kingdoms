using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// PROGRESSION.md, "Building 1g", step 8: the balance report's heroes spend their points the way a player would
    /// (<see cref="HeroBuilds"/>): their own class as far as it goes, one path per milestone. Spending is tested on
    /// the made-up Scout class, so the real classes can change without touching these tests.
    /// </summary>
    public class HeroBuildsTests
    {
        /// <summary>A hero of the made-up Scout class (tiers 5 and 10 written, 15 not).</summary>
        static readonly ActorDefinition Scout = new ActorDefinition("test_scout_hero", "Scout", maxHp: 400, attack: 60, defense: 30,
            expReward: 0, skills: new[] { SkillCatalog.HuntersMark, SkillCatalog.PowerShot, SkillCatalog.RollingShot },
            ultimate: SkillCatalog.Volley, weapon: WeaponCatalog.HunterBow, attackRange: SkillCatalog.RangedReach,
            startingClass: ClassRulesTests.Scout);

        [Test]
        public void ABuildSpendsEveryPointOnTheHerosOwnClass()
        {
            var hero = new HeroProgress(Scout, level: 4);
            Assert.AreEqual(3, HeroBuilds.Spend(hero), "tier 1 was the hero's already");
            Assert.AreEqual(4, hero.TierOf(ClassRulesTests.Scout));
            Assert.AreEqual(0, hero.PointsFree);
            Assert.AreEqual(0, HeroBuilds.Spend(hero), "nothing left to spend");
        }

        [Test]
        public void TheDefaultBuildTakesTheFirstPathAtEveryMilestone()
        {
            var hero = new HeroProgress(Scout, level: 12);
            HeroBuilds.Spend(hero);
            Assert.AreEqual(12, hero.TierOf(ClassRulesTests.Scout));
            Assert.AreSame(ClassRulesTests.HeavyDraw, hero.PickAt(ClassRulesTests.Scout, 5));
            Assert.AreSame(ClassRulesTests.DoubleJab, hero.PickAt(ClassRulesTests.Scout, 10));
        }

        [Test]
        public void ABuildNamesAPathPerMilestoneAndTheLastOneStandsForTheRest()
        {
            var one = new HeroProgress(Scout, level: 10);
            HeroBuilds.Spend(one, new[] { 1 });
            Assert.AreSame(ClassRulesTests.LearnJolt, one.PickAt(ClassRulesTests.Scout, 5));
            Assert.AreSame(ClassRulesTests.LongDraw, one.PickAt(ClassRulesTests.Scout, 10), "the same path again");

            var mixed = new HeroProgress(Scout, level: 10);
            HeroBuilds.Spend(mixed, new[] { 0, 2 });
            Assert.AreSame(ClassRulesTests.HeavyDraw, mixed.PickAt(ClassRulesTests.Scout, 5));
            Assert.AreSame(ClassRulesTests.LearnNova, mixed.PickAt(ClassRulesTests.Scout, 10));
        }

        [Test]
        public void ABuildStopsWhereTheClassDoes()
        {
            var hero = new HeroProgress(Scout, level: 20);
            HeroBuilds.Spend(hero);
            Assert.AreEqual(14, hero.TierOf(ClassRulesTests.Scout), "tier 15 isn't written");
            Assert.AreEqual(6, hero.PointsFree, "the points stay free: a build never goes into another class");
        }

        [Test]
        public void ABuildGoesOnFromWhereTheHeroStands()
        {
            // As in the campaign: levels are kept between runs, and each run's new points are spent before the next.
            var hero = new HeroProgress(Scout, level: 6);
            HeroBuilds.Spend(hero, new[] { 2, 1 });
            Assert.AreSame(ClassRulesTests.LearnSprint, hero.PickAt(ClassRulesTests.Scout, 5));
            hero.Record(11, 0);
            Assert.AreEqual(5, HeroBuilds.Spend(hero, new[] { 2, 1 }));
            Assert.AreSame(ClassRulesTests.LearnSprint, hero.PickAt(ClassRulesTests.Scout, 5), "a pick stays");
            Assert.AreSame(ClassRulesTests.LongDraw, hero.PickAt(ClassRulesTests.Scout, 10));
        }

        [Test]
        public void ABuildCanStopBeforeAMilestone()
        {
            // To measure what a milestone adds: the same hero without it, its other points unspent.
            var hero = new HeroProgress(Scout, level: 12);
            Assert.AreEqual(3, HeroBuilds.Spend(hero, new[] { HeroBuilds.Stop }));
            Assert.AreEqual(4, hero.TierOf(ClassRulesTests.Scout));
            Assert.AreEqual(8, hero.PointsFree);

            var later = new HeroProgress(Scout, level: 12);
            HeroBuilds.Spend(later, new[] { 1, HeroBuilds.Stop });
            Assert.AreEqual(9, later.TierOf(ClassRulesTests.Scout), "the first milestone taken, the second left");

            Assert.IsTrue(HeroBuilds.TryParse("uzuki:hunter,none", out _, out var paths, out string error), error);
            CollectionAssert.AreEqual(new[] { 1, HeroBuilds.Stop }, paths);
        }

        [Test]
        public void AMonsterHasNothingToSpend()
        {
            Assert.AreEqual(0, HeroBuilds.Spend(new HeroProgress(ActorCatalog.Spider, level: 9)));
        }

        [Test]
        public void TheStartingPartySpendsItsPointsOnItsOwnClasses()
        {
            foreach (var definition in ActorCatalog.StartingParty)
            {
                var hero = new HeroProgress(definition, level: 30);
                HeroBuilds.Spend(hero);
                int open = definition.StartingClass.HighestOpenTier;
                Assert.AreEqual(open, hero.TierOf(definition.StartingClass), definition.Name);
                Assert.AreEqual(1, hero.Classes.Count, definition.Name);
                for (int tier = ClassDefinition.MilestoneEvery; tier <= open; tier += ClassDefinition.MilestoneEvery)
                    Assert.AreEqual(HeroBuilds.DefaultPath, hero.PickAt(definition.StartingClass, tier).Path, $"{definition.Name}, tier {tier}");
                StringAssert.StartsWith($"{definition.Name}: {definition.StartingClass.Name} {open}", HeroBuilds.Describe(hero));
            }
        }

        [Test]
        public void ABuildPutsTheSkillAnOptionTeachesInTheLoadout()
        {
            // A new skill only goes in by itself while a slot is free; a build gives up one of its three for it.
            var hunter = new HeroProgress(ActorCatalog.Uzuki, level: 5);
            HeroBuilds.Spend(hunter, new[] { 1 });
            CollectionAssert.AreEqual(new[] { "crippling_shot", "power_shot", "rolling_shot" }, hunter.LoadoutIds, "in Hunter's Mark's place");
            StringAssert.Contains("Crippling Shot", string.Join(", ", hunter.Kit.Skills.Select(skill => skill.Name)));

            var marksman = new HeroProgress(ActorCatalog.Uzuki, level: 5);
            HeroBuilds.Spend(marksman);
            CollectionAssert.AreEqual(new[] { "hunters_mark", "power_shot", "rolling_shot" }, marksman.LoadoutIds, "an upgrade changes no slot");
        }

        [Test]
        public void AToolCanGiveABuildAnotherLoadout()
        {
            var hunter = new HeroProgress(ActorCatalog.Uzuki, level: 5);
            HeroBuilds.Spend(hunter, new[] { 1 });
            Assert.AreEqual(3, HeroBuilds.Equip(hunter, new[] { "hunters_mark", "crippling_shot", "rolling_shot" }));
            CollectionAssert.AreEqual(new[] { "hunters_mark", "crippling_shot", "rolling_shot" }, hunter.LoadoutIds);
            Assert.AreEqual(0, HeroBuilds.Equip(hunter, new[] { "bouncing_shot" }), "a skill the hero hasn't learned stays out");
            CollectionAssert.AreEqual(new[] { "hunters_mark", "crippling_shot", "rolling_shot" }, hunter.LoadoutIds);
        }

        [Test]
        public void ABuildIsReadFromTheCommandLine()
        {
            Assert.IsTrue(HeroBuilds.TryParse("uzuki:hunter", out var hero, out var paths, out string error), error);
            Assert.AreSame(ActorCatalog.Uzuki, hero);
            CollectionAssert.AreEqual(new[] { 1 }, paths);

            Assert.IsTrue(HeroBuilds.TryParse("Kristela:duelist,EnGarde,foot", out hero, out paths, out error), error);
            Assert.AreSame(ActorCatalog.Kristela, hero);
            CollectionAssert.AreEqual(new[] { 0, 2, 1 }, paths, "without regard to case or spaces; the first letters are enough");

            Assert.IsFalse(HeroBuilds.TryParse("spider:hunter", out _, out _, out error));
            StringAssert.Contains("not a hero", error);
            Assert.IsFalse(HeroBuilds.TryParse("haiden:hunter", out _, out _, out error));
            StringAssert.Contains("Guardian, Devotion, Crusader", error);
            Assert.IsFalse(HeroBuilds.TryParse("haiden", out _, out _, out error));
            StringAssert.Contains("haiden:guardian", error);
        }

        [Test]
        public void ABuildSaysWhatItIs()
        {
            var hero = new HeroProgress(Scout, level: 10);
            HeroBuilds.Spend(hero, new[] { 1, 2 });
            Assert.AreEqual("Scout: Scout 10 (Two, Three)", HeroBuilds.Describe(hero));
            Assert.AreEqual("Scout: Scout 1", HeroBuilds.Describe(new HeroProgress(Scout)));
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// PROGRESSION.md, "Classes" and "Building 1g", part 1: a point per level, classes of 25 tiers, a stat bump every
    /// tier, three options at every milestone, the hero's own skill pool and loadout, unlearning, and how all of it
    /// reaches a run and a save file. The rules are tested on classes made up here, so the real ones can be tuned
    /// without touching these tests.
    /// </summary>
    public class ClassRulesTests
    {
        static readonly SkillDefinition Jolt = new SkillDefinition("test_jolt", "Jolt", "Jolt", SkillEffect.Strike, power: 180);

        static readonly SkillDefinition Sprint = new SkillDefinition("test_sprint", "Sprint", "Sprint", SkillEffect.Dash, power: 3,
            costPercent: SkillDefinition.QuickCostPercent);

        static readonly SkillDefinition Nova = new SkillDefinition("test_nova", "Nova", "Nova", SkillEffect.Area, power: 300,
            ultimate: true, range: 3, radius: 1);

        static readonly ClassOption HeavyDraw = new ClassOption("test_heavy_draw", "Heavy Draw", 0, "Power Shot hits harder and knocks back 2 tiles.",
            upgrades: SkillCatalog.PowerShot, change: skill =>
            {
                skill.Power = 360;
                skill.Knockback = 2;
            });

        static readonly ClassOption LearnJolt = new ClassOption("test_learn_jolt", "Jolt", 1, "A new strike.", teaches: Jolt);
        static readonly ClassOption LearnSprint = new ClassOption("test_learn_sprint", "Sprint", 2, "A Quick dash.", teaches: Sprint);

        static readonly ClassOption DoubleJab = new ClassOption("test_double_jab", "Double Jab", 0, "Jab hits twice.",
            attackOf: WeaponFamily.Fists, change: attack =>
            {
                attack.Hits = 2;
                attack.Power = 120;
            });

        static readonly ClassOption LongDraw = new ClassOption("test_long_draw", "Long Draw", 1, "Power Shot reaches further.",
            upgrades: SkillCatalog.PowerShot, change: skill => skill.Range += 1);

        static readonly ClassOption LearnNova = new ClassOption("test_learn_nova", "Nova", 2, "Another ultimate.", teaches: Nova);

        /// <summary>A light class with content at tiers 5 and 10; tier 15 isn't written, so it stops at 14.</summary>
        static readonly ClassDefinition Scout = new ClassDefinition("test_scout", "Scout", WeaponFamily.Bow, speedModifier: 5,
            paths: new[] { "One", "Two", "Three" },
            bumps: new[] { new StatBump(StatKind.Atk, 4), new StatBump(StatKind.CritRate, 2) },
            milestones: new[]
            {
                new ClassMilestone(5, HeavyDraw, LearnJolt, LearnSprint),
                new ClassMilestone(10, DoubleJab, LongDraw, LearnNova),
            });

        /// <summary>A heavy class with no milestones written at all.</summary>
        static readonly ClassDefinition Bulwark = new ClassDefinition("test_bulwark", "Bulwark", WeaponFamily.None, speedModifier: -5,
            paths: new[] { "One", "Two", "Three" }, bumps: new[] { new StatBump(StatKind.Hp, 4), new StatBump(StatKind.Def, 4) });

        static ClassDefinition Find(string id) => id == Scout.Id ? Scout : id == Bulwark.Id ? Bulwark : ClassCatalog.Find(id);

        static HeroProgress Hero(ActorDefinition definition, int level) => new HeroProgress(definition, level);

        static string Ids(IEnumerable<SkillDefinition> skills) => string.Join(",", skills.Select(skill => skill.Id));

        static void RaiseTo(HeroProgress hero, ClassDefinition definition, int tier, params ClassOption[] picks)
        {
            while (hero.TierOf(definition) < tier)
            {
                int next = hero.TierOf(definition) + 1;
                var pick = ClassDefinition.IsMilestone(next) ? picks.First(option => definition.MilestoneAt(next).Options.Contains(option)) : null;
                Assert.IsTrue(hero.Raise(definition, pick), $"tier {next}: {hero.CheckRaise(definition, pick)}");
            }
        }

        // ---- Points and tiers ----

        [Test]
        public void AHeroStartsAtTierOneOfItsOwnClassWithItsStartingKit()
        {
            var uzuki = Hero(ActorCatalog.Uzuki, 5);
            Assert.AreEqual(5, uzuki.Points, "a point per level");
            Assert.AreEqual(1, uzuki.PointsSpent);
            Assert.AreEqual(4, uzuki.PointsFree);
            Assert.AreEqual(1, uzuki.TierOf(ClassCatalog.Archer));
            Assert.AreEqual(0, uzuki.TierOf(ClassCatalog.Paladin));
            Assert.AreSame(ClassCatalog.Archer, uzuki.Classes.Single().Class);

            CollectionAssert.AreEqual(new[] { "hunters_mark", "power_shot", "rolling_shot" }, uzuki.LoadoutIds);
            Assert.AreEqual("volley", uzuki.UltimateId);
            var kit = uzuki.Kit;
            CollectionAssert.AreEqual(ActorCatalog.Uzuki.Skills, kit.Skills, "the catalog's own skills: nothing is upgraded yet");
            Assert.AreSame(SkillCatalog.Volley, kit.Ultimate);
            Assert.AreEqual("Quick Shot", kit.WeaponAttack.Name);
            Assert.AreEqual(CombatRules.BasicAttackPercent, kit.WeaponAttack.Power);
            Assert.AreEqual(1, kit.WeaponAttack.Hits);
        }

        [Test]
        public void TheStartingPartysClassesAreTheApprovedOnes()
        {
            Assert.AreSame(ClassCatalog.Archer, ActorCatalog.Uzuki.StartingClass);
            Assert.AreSame(ClassCatalog.Paladin, ActorCatalog.Haiden.StartingClass);
            Assert.AreSame(ClassCatalog.Fencer, ActorCatalog.Kristela.StartingClass, "a Fencer since 2026-10-05; the Monk stays as a class");
            CollectionAssert.Contains(ClassCatalog.All, ClassCatalog.Monk);
            Assert.IsNull(ActorCatalog.Spider.StartingClass);
            foreach (var definition in ClassCatalog.All)
            {
                Assert.AreSame(definition, ClassCatalog.Find(definition.Id));
                Assert.AreEqual(ClassDefinition.PathCount, definition.Paths.Count, definition.Name);
                Assert.That(definition.SpeedModifier, Is.InRange(-5, 5), definition.Name);
                foreach (var bump in definition.Bumps) Assert.AreNotEqual(StatKind.Spd, bump.Stat, definition.Name);
            }
        }

        [Test]
        public void APointBuysTheNextTierOfAnyClass()
        {
            var uzuki = Hero(ActorCatalog.Uzuki, 3);
            Assert.AreEqual(RaiseCheck.Ok, uzuki.CheckRaise(Bulwark));
            Assert.IsTrue(uzuki.Raise(Bulwark), "any hero can put points into any base class");
            Assert.IsTrue(uzuki.Raise(ClassCatalog.Archer));
            Assert.AreEqual(1, uzuki.TierOf(Bulwark));
            Assert.AreEqual(2, uzuki.TierOf(ClassCatalog.Archer));
            Assert.AreEqual(0, uzuki.PointsFree);

            Assert.AreEqual(RaiseCheck.NoPoints, uzuki.CheckRaise(Bulwark));
            Assert.IsFalse(uzuki.Raise(Bulwark));
            Assert.AreEqual(1, uzuki.TierOf(Bulwark), "nothing spent");
        }

        [Test]
        public void EveryTierGivesTheClasssStatBumps()
        {
            var uzuki = Hero(ActorCatalog.Uzuki, 10);
            RaiseTo(uzuki, Scout, 3);
            RaiseTo(uzuki, Bulwark, 2);
            var kit = uzuki.Kit;
            Assert.AreEqual(12, kit.Percent[StatKind.Atk], "+0.4% ATK a tier");
            Assert.AreEqual(6, kit.Flat[StatKind.CritRate], "+0.2% Crit Rate a tier: a percentage stat, so it just adds");
            Assert.AreEqual(8, kit.Percent[StatKind.Hp]);
            Assert.AreEqual(8, kit.Percent[StatKind.Def]);
            Assert.AreEqual(0, kit.Percent[StatKind.Spd] + kit.Flat[StatKind.Spd], "never SPD");

            var plain = new Actor(1, ActorCatalog.Uzuki, Team.Hero, default, 10);
            var built = new Actor(2, ActorCatalog.Uzuki, Team.Hero, default, 10, kit);
            Assert.AreEqual(plain.Attack * 1012 / 1000, built.Attack, "the bump scales base, level growth and weapon alike");
            Assert.AreEqual(plain.MaxHp * 1008 / 1000, built.MaxHp);
            Assert.AreEqual(built.MaxHp, built.Hp, "and it starts the run at its full HP");
            Assert.AreEqual(plain.Defense * 1008 / 1000, built.Defense);
            Assert.AreEqual(plain.CritRate + 6, built.CritRate);
            Assert.AreEqual(plain.CritDmg, built.CritDmg);
        }

        [Test]
        public void ClassesNeverGiveSpeed()
        {
            Assert.Throws<System.ArgumentException>(() => new StatBump(StatKind.Spd, 1));
        }

        // ---- Milestones ----

        [Test]
        public void AMilestoneTakesAPickAmongItsThreeOptions()
        {
            var uzuki = Hero(ActorCatalog.Uzuki, 12);
            RaiseTo(uzuki, Scout, 4);
            Assert.AreEqual(RaiseCheck.NeedsPick, uzuki.CheckRaise(Scout), "tier 5 is a milestone");
            Assert.IsFalse(uzuki.Raise(Scout));
            Assert.AreEqual(RaiseCheck.WrongOption, uzuki.CheckRaise(Scout, LongDraw), "that one belongs to tier 10");
            Assert.AreEqual(RaiseCheck.Ok, uzuki.CheckRaise(Scout, LearnJolt));

            Assert.IsTrue(uzuki.Raise(Scout, LearnJolt));
            Assert.AreEqual(5, uzuki.TierOf(Scout));
            Assert.AreSame(LearnJolt, uzuki.PickAt(Scout, 5));
            Assert.IsNull(uzuki.PickAt(Scout, 10));
            Assert.AreEqual(RaiseCheck.WrongOption, uzuki.CheckRaise(Scout, HeavyDraw), "one pick per milestone: tier 6 takes none");
            Assert.AreEqual(20, uzuki.Kit.Percent[StatKind.Atk], "a milestone tier gives the stat bump too");
        }

        [Test]
        public void PicksMayComeFromAnyPath()
        {
            var uzuki = Hero(ActorCatalog.Uzuki, 12);
            RaiseTo(uzuki, Scout, 10, HeavyDraw, LearnNova);
            Assert.AreEqual(0, uzuki.PickAt(Scout, 5).Path);
            Assert.AreEqual(2, uzuki.PickAt(Scout, 10).Path, "let them mix (Peter, 2026-10-03)");
        }

        [Test]
        public void AMilestoneThatIsNotWrittenYetCannotBePassed()
        {
            Assert.AreEqual(14, Scout.HighestOpenTier);
            Assert.AreEqual(4, Bulwark.HighestOpenTier);
            Assert.IsNull(Scout.MilestoneAt(15));

            var uzuki = Hero(ActorCatalog.Uzuki, 20);
            RaiseTo(uzuki, Bulwark, 4);
            Assert.AreEqual(RaiseCheck.Locked, uzuki.CheckRaise(Bulwark));
            Assert.IsFalse(uzuki.Raise(Bulwark));
            RaiseTo(uzuki, Scout, 14, LearnJolt, LongDraw);
            Assert.AreEqual(RaiseCheck.Locked, uzuki.CheckRaise(Scout, LearnJolt));
            Assert.AreEqual(1, uzuki.PointsFree, "the points stay free for another class");
        }

        [Test]
        public void AClassStopsAtTierTwentyFive()
        {
            var options = Enumerable.Range(1, 5).Select(row => new[] { 0, 1, 2 }.Select(path =>
                new ClassOption($"test_full_{row}_{path}", "Option", path, "", teaches: Jolt)).ToArray()).ToArray();
            var full = new ClassDefinition("test_full", "Full", WeaponFamily.None, 0, new[] { "A", "B", "C" },
                milestones: options.Select((row, index) => new ClassMilestone((index + 1) * 5, row)).ToArray());
            Assert.AreEqual(ClassDefinition.MaxTier, full.HighestOpenTier);

            var hero = Hero(TestHeroes.Classic, 40);
            RaiseTo(hero, full, 25, options.Select(row => row[1]).ToArray());
            Assert.AreEqual(RaiseCheck.MaxTier, hero.CheckRaise(full));
            Assert.AreEqual(15, hero.PointsFree);
        }

        // ---- The pool and the loadout ----

        [Test]
        public void AnOptionTeachesASkillForThePool()
        {
            var uzuki = Hero(ActorCatalog.Uzuki, 6);
            RaiseTo(uzuki, Scout, 5, LearnJolt);
            Assert.AreEqual("hunters_mark,power_shot,rolling_shot,test_jolt", Ids(uzuki.KnownSkills));
            Assert.AreEqual("hunters_mark,power_shot,rolling_shot", Ids(uzuki.Kit.Skills), "the loadout is full: the player chooses what makes way");

            Assert.AreEqual(EquipCheck.Ok, uzuki.CheckEquip(2, Jolt));
            Assert.IsTrue(uzuki.Equip(2, Jolt));
            Assert.AreEqual("hunters_mark,power_shot,test_jolt", Ids(uzuki.Kit.Skills));
            Assert.AreSame(Jolt, uzuki.Kit.Skills[2]);
        }

        [Test]
        public void AnUpgradeChangesTheHerosOwnCopyOfASkill()
        {
            var uzuki = Hero(ActorCatalog.Uzuki, 6);
            RaiseTo(uzuki, Scout, 5, HeavyDraw);
            var shot = uzuki.Kit.Skills[1];
            Assert.AreEqual("power_shot", shot.Id, "still that skill, to the loadout and the saves");
            Assert.AreEqual(360, shot.Power);
            Assert.AreEqual(2, shot.Knockback);
            Assert.AreEqual(SkillCatalog.PowerShot.Range, shot.Range, "what the option doesn't touch stays");

            Assert.AreEqual(300, SkillCatalog.PowerShot.Power, "the catalog's skill is never touched");
            Assert.AreEqual(300, Hero(ActorCatalog.Uzuki, 6).Kit.Skills[1].Power, "nor another hero's");
        }

        [Test]
        public void UpgradesOfTheSameSkillAddUp()
        {
            var uzuki = Hero(ActorCatalog.Uzuki, 11);
            RaiseTo(uzuki, Scout, 10, HeavyDraw, LongDraw);
            var shot = uzuki.Kit.Skills[1];
            Assert.AreEqual(360, shot.Power);
            Assert.AreEqual(SkillCatalog.PowerShot.Range + 1, shot.Range);
        }

        [Test]
        public void AnUpgradeOfASkillTheHeroDoesntKnowTeachesThatSkillInstead()
        {
            var kristela = Hero(ActorCatalog.Kristela, 6);
            RaiseTo(kristela, Scout, 5, HeavyDraw);
            var shot = kristela.KnownSkills.Single(skill => skill.Id == "power_shot");
            Assert.AreSame(SkillCatalog.PowerShot, shot, "as the catalog has it: 300%, one tile");

            Assert.AreEqual(EquipCheck.WrongWeapon, kristela.CheckEquip(0, shot), "a bow skill, and she fights with her fists");
            Assert.IsFalse(kristela.Equip(0, shot));
            Assert.AreEqual("piercing_punch,ki_heal,stun_strike", Ids(kristela.Kit.Skills));
        }

        [Test]
        public void ASkillTiedToNoWeaponGoesInAnyHerosLoadout()
        {
            var haiden = Hero(ActorCatalog.Haiden, 6);
            RaiseTo(haiden, Scout, 5, LearnJolt);
            Assert.AreEqual(WeaponFamily.None, Jolt.Weapon);
            Assert.IsTrue(haiden.Equip(1, Jolt));
            Assert.AreEqual("paladin_heal,test_jolt,shoulder_bash", Ids(haiden.Kit.Skills));
        }

        [Test]
        public void TheLoadoutHoldsAtMostOneQuickSkill()
        {
            var kristela = Hero(ActorCatalog.Kristela, 6);
            RaiseTo(kristela, Scout, 5, LearnSprint);
            Assert.IsTrue(Sprint.IsQuick);
            Assert.AreEqual(EquipCheck.TooManyQuick, kristela.CheckEquip(0, Sprint), "Ki Heal is her Quick skill");
            Assert.AreEqual(EquipCheck.Ok, kristela.CheckEquip(1, Sprint), "in Ki Heal's place it is the only one");
            Assert.IsTrue(kristela.Equip(1, Sprint));
            Assert.AreEqual("piercing_punch,test_sprint,stun_strike", Ids(kristela.Kit.Skills));
            Assert.AreEqual(EquipCheck.TooManyQuick, kristela.CheckEquip(0, SkillCatalog.KiHeal));
        }

        [Test]
        public void ASkillFromAnotherSlotTradesPlaces()
        {
            var uzuki = Hero(ActorCatalog.Uzuki, 1);
            Assert.IsTrue(uzuki.Equip(0, SkillCatalog.RollingShot));
            Assert.AreEqual("rolling_shot,power_shot,hunters_mark", Ids(uzuki.Kit.Skills));
            Assert.IsTrue(uzuki.Equip(2, SkillCatalog.HuntersMark), "a Quick skill moving to its own slot is still the only one");
        }

        [Test]
        public void OnlyKnownSkillsAndOnlySkillsGoInASkillSlot()
        {
            var uzuki = Hero(ActorCatalog.Uzuki, 1);
            Assert.AreEqual(EquipCheck.NotKnown, uzuki.CheckEquip(0, Jolt));
            Assert.AreEqual(EquipCheck.WrongSlot, uzuki.CheckEquip(0, SkillCatalog.Volley), "an ultimate isn't a skill");
            Assert.AreEqual(EquipCheck.WrongSlot, uzuki.CheckEquip(3, SkillCatalog.PowerShot));
            Assert.AreEqual(EquipCheck.WrongSlot, uzuki.CheckEquipUltimate(SkillCatalog.PowerShot));
            Assert.AreEqual(EquipCheck.NotKnown, uzuki.CheckEquipUltimate(Nova));
        }

        [Test]
        public void AnotherUltimateCanTakeTheUltimatesPlace()
        {
            var haiden = Hero(ActorCatalog.Haiden, 11);
            RaiseTo(haiden, Scout, 10, LearnJolt, LearnNova);
            Assert.AreEqual("aura_of_protection,test_nova", Ids(haiden.KnownUltimates));
            Assert.AreSame(SkillCatalog.AuraOfProtection, haiden.Kit.Ultimate, "learning one doesn't change the loadout");
            Assert.IsTrue(haiden.EquipUltimate(Nova));
            Assert.AreSame(Nova, haiden.Kit.Ultimate);
        }

        [Test]
        public void AnOptionCanChangeTheWeaponAttackOfItsWeaponFamily()
        {
            var kristela = Hero(ActorCatalog.Kristela, 11);
            RaiseTo(kristela, Scout, 10, LearnJolt, DoubleJab);
            Assert.AreEqual(2, kristela.Kit.WeaponAttack.Hits);
            Assert.AreEqual(120, kristela.Kit.WeaponAttack.Power);
            Assert.AreEqual("Jab", kristela.Kit.WeaponAttack.Name);

            var haiden = Hero(ActorCatalog.Haiden, 11);
            RaiseTo(haiden, Scout, 10, LearnJolt, DoubleJab);
            Assert.AreEqual(1, haiden.Kit.WeaponAttack.Hits, "his Sword Slash isn't a Jab");
        }

        // ---- Unlearning ----

        [Test]
        public void UnlearningAClassReturnsItsPointsAndTakesItsSkillsAway()
        {
            var uzuki = Hero(ActorCatalog.Uzuki, 8);
            RaiseTo(uzuki, Scout, 6, LearnJolt);
            Assert.IsTrue(uzuki.Equip(0, Jolt));
            Assert.AreEqual(1, uzuki.PointsFree);

            Assert.AreEqual(6, uzuki.Unlearn(Scout), "free for now (PROGRESSION.md, \"Respec\")");
            Assert.AreEqual(7, uzuki.PointsFree);
            Assert.AreEqual(0, uzuki.TierOf(Scout));
            Assert.IsNull(uzuki.PickAt(Scout, 5));
            Assert.AreEqual("hunters_mark,power_shot,rolling_shot", Ids(uzuki.KnownSkills));
            Assert.AreEqual(3, uzuki.Kit.Skills.Count, "the slot Jolt left is filled from what he still knows");
            CollectionAssert.AreEquivalent(new[] { "hunters_mark", "power_shot", "rolling_shot" }, uzuki.Kit.Skills.Select(skill => skill.Id));
            Assert.AreEqual(0, uzuki.Kit.Percent[StatKind.Atk]);
            Assert.AreEqual(0, uzuki.Unlearn(Scout), "nothing left to return");
        }

        [Test]
        public void AHeroCanEvenUnlearnItsOwnClass()
        {
            var haiden = Hero(ActorCatalog.Haiden, 4);
            Assert.AreEqual(1, haiden.Unlearn(ClassCatalog.Paladin));
            Assert.AreEqual(4, haiden.PointsFree);
            Assert.AreEqual(0, haiden.Classes.Count);
            Assert.AreEqual("paladin_heal,divine_strike,shoulder_bash", Ids(haiden.Kit.Skills), "the starting kit is the hero's own, not the class's");
            Assert.AreEqual(ActorCatalog.Haiden.Speed, haiden.Kit.SpeedFor(ActorCatalog.Haiden.Speed), "no class, no modifier");
        }

        // ---- Speed ----

        [Test]
        public void TheHighestClassSetsTheSpeedModifier()
        {
            var uzuki = Hero(ActorCatalog.Uzuki, 6);
            Assert.AreEqual(95, uzuki.Kit.SpeedFor(ActorCatalog.Uzuki.Speed), "his own 90, the Archer's +5");
            Assert.IsTrue(uzuki.Raise(Bulwark));
            Assert.AreEqual(95, uzuki.Kit.SpeedFor(90), "tier 1 each: his own class decides");
            Assert.IsTrue(uzuki.Raise(Bulwark));
            Assert.AreEqual(85, uzuki.Kit.SpeedFor(90), "the Bulwark's -5 now");
            Assert.AreEqual(HeroKit.MinSpeed, uzuki.Kit.SpeedFor(86), "never under 85 before gear");

            RaiseTo(uzuki, ClassCatalog.Archer, 3);
            Assert.AreEqual(HeroKit.MaxSpeed, uzuki.Kit.SpeedFor(98), "nor over 100");
        }

        [Test]
        public void SpeedsStayAsTheyWere()
        {
            foreach (var (definition, speed) in new[] { (ActorCatalog.Uzuki, 101), (ActorCatalog.Haiden, 90), (ActorCatalog.Kristela, 100) })
            {
                Assert.AreEqual(speed, new Actor(1, definition, Team.Hero, default).Speed, definition.Name);
                Assert.AreEqual(speed, new Actor(1, definition, Team.Hero, default, 7, Hero(definition, 7).Kit).Speed, definition.Name);
            }
            Assert.AreEqual(130, new Actor(1, ActorCatalog.Bat, Team.Enemy, default).Speed, "monsters have no class and no clamp");
        }

        // ---- Into a run, and out of it ----

        [Test]
        public void TheRunFightsWithTheHerosOwnKit()
        {
            var uzuki = Hero(ActorCatalog.Uzuki, 6);
            RaiseTo(uzuki, Scout, 5, HeavyDraw);
            var config = new DungeonRunConfig
            {
                MapFactory = (floor, seed) => DungeonMap.FromAscii("############", "#@.........#", "############"),
                Populate = false,
                RegenIntervalAv = 0,
                Boss = null,
            };
            var run = new DungeonRun(7, config, new[] { uzuki });
            var hero = run.Hero;
            Assert.AreSame(uzuki.Kit, hero.Kit);
            Assert.AreEqual(360, hero.Skills[1].Power);
            Assert.AreEqual(3, hero.SkillCooldowns.Length);

            var spider = run.SpawnEnemy(new GridPos(4, 1));
            spider.MaxHp = spider.Hp = 100000;
            spider.Statuses.Add(new StatusEffect(StatusKind.Rooted, spider.Id, 0, 999, endsOnSourceTurn: false)); // It doesn't walk back.
            Assert.IsTrue(run.UseSkillAt(1, spider.Pos));
            Assert.AreEqual(new GridPos(6, 1), spider.Pos, "knocked back two tiles, as his own Power Shot does");
            Assert.AreSame(hero.Skills[1], run.Events.OfType<SkillUsedEvent>().Single().Skill, "the event carries his version of the skill");
        }

        [Test]
        public void LevelsGainedInARunArePointsForAfterwards()
        {
            var uzuki = Hero(ActorCatalog.Uzuki, 1);
            var config = new DungeonRunConfig
            {
                MapFactory = (floor, seed) => DungeonMap.FromAscii("############", "#@.........#", "############"),
                Populate = false,
                Boss = null,
            };
            var run = new DungeonRun(7, config, new[] { uzuki });
            var kit = run.Hero.Kit;
            var spider = run.SpawnEnemy(new GridPos(2, 1));
            spider.ExpReward = 100;
            spider.Hp = 1;
            Assert.IsTrue(run.AttackAt(spider.Pos));

            Assert.Greater(uzuki.Level, 1);
            Assert.AreEqual(uzuki.Level - 1, uzuki.PointsFree, "one more point per level, to spend between runs");
            Assert.AreSame(kit, run.Hero.Kit, "the kit a hero entered with is the one it finishes the run with");
        }

        // ---- Saving ----

        [Test]
        public void ASavedBuildComesBackAsItWas()
        {
            var uzuki = Hero(ActorCatalog.Uzuki, 14);
            RaiseTo(uzuki, Scout, 10, LearnJolt, LearnNova);
            RaiseTo(uzuki, Bulwark, 2);
            Assert.IsTrue(uzuki.Equip(0, Jolt));
            Assert.IsTrue(uzuki.EquipUltimate(Nova));

            var restored = HeroProgress.Restore(ActorCatalog.Uzuki, uzuki.Level, uzuki.Exp, uzuki.SaveClasses(), uzuki.LoadoutIds, uzuki.UltimateId, Find);
            CollectionAssert.AreEqual(new[] { "archer", "test_scout", "test_bulwark" }, restored.Classes.Select(progress => progress.Class.Id));
            Assert.AreEqual(1, restored.TierOf(ClassCatalog.Archer));
            Assert.AreEqual(10, restored.TierOf(Scout));
            Assert.AreEqual(2, restored.TierOf(Bulwark));
            Assert.AreSame(LearnJolt, restored.PickAt(Scout, 5));
            Assert.AreSame(LearnNova, restored.PickAt(Scout, 10));
            CollectionAssert.AreEqual(uzuki.LoadoutIds, restored.LoadoutIds);
            Assert.AreEqual("test_nova", restored.UltimateId);
            Assert.AreEqual(uzuki.PointsFree, restored.PointsFree);
        }

        [Test]
        public void WhatASaveHoldsThatTheRulesDontAllowIsLeftOut()
        {
            var classes = new List<SavedClass>
            {
                new SavedClass("archer", 3, new string[0]),
                new SavedClass("a_class_that_is_gone", 4, new string[0]),
                new SavedClass(Scout.Id, 9, new[] { "an_option_that_is_gone" }),
                new SavedClass(Bulwark.Id, 30, null),
            };
            var hero = HeroProgress.Restore(ActorCatalog.Uzuki, 12, 0, classes, new[] { "test_jolt", null, "power_shot", "extra" }, "test_nova", Find);

            Assert.AreEqual(3, hero.TierOf(ClassCatalog.Archer));
            Assert.AreEqual(4, hero.TierOf(Scout), "its tier-5 pick no longer exists: it stops before the milestone");
            Assert.AreEqual(4, hero.TierOf(Bulwark), "as far as the class goes today");
            Assert.AreEqual(1, hero.PointsFree, "what couldn't be spent again is free");
            Assert.AreEqual(3, hero.Kit.Skills.Count);
            Assert.AreEqual("power_shot", hero.Kit.Skills[2].Id, "the one saved skill he still knows keeps its slot");
            Assert.AreEqual("volley", hero.UltimateId, "an ultimate he doesn't know gives way to his own");
        }

        [Test]
        public void ASaveNeverSpendsMorePointsThanTheHeroHas()
        {
            var classes = new List<SavedClass> { new SavedClass("archer", 4, null), new SavedClass(Bulwark.Id, 4, null) };
            var hero = HeroProgress.Restore(ActorCatalog.Uzuki, 5, 0, classes, null, null, Find);
            Assert.AreEqual(4, hero.TierOf(ClassCatalog.Archer));
            Assert.AreEqual(1, hero.TierOf(Bulwark));
            Assert.AreEqual(0, hero.PointsFree);
        }

        [Test]
        public void AHeroWithNoClassesSavedHasNone()
        {
            var hero = HeroProgress.Restore(ActorCatalog.Haiden, 6, 0, new List<SavedClass>(), null, null);
            Assert.AreEqual(0, hero.Classes.Count, "it unlearned its class: that is kept");
            Assert.AreEqual(6, hero.PointsFree);
            Assert.AreEqual("paladin_heal,divine_strike,shoulder_bash", Ids(hero.Kit.Skills));
        }
    }
}

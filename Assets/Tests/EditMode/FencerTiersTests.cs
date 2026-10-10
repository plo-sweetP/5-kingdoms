using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;
using static FiveKingdoms.Tests.PartyTests;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// The Fencer's milestone options (PROGRESSION.md, "Starting class content"; Peter, 2026-10-10: "fencer options
    /// are fine as drafted"), on Kristela: what each one changes on her own copy of a skill, what it does in a run,
    /// and what the party's AI does with it.
    /// </summary>
    public class FencerTiersTests
    {
        static readonly string[] Corridor =
        {
            "############",
            "#@.........#",
            "############",
        };

        static HeroProgress Fencer(int level, params string[] picks) => TestHeroes.Built(ActorCatalog.Kristela, level, picks);

        static int Slot(Actor hero, SkillDefinition skill) => TestHeroes.Slot(hero, skill);

        static SkillDefinition Own(HeroProgress hero, SkillDefinition skill) => hero.Kit.Skills.Single(own => own.Id == skill.Id);

        /// <summary>A dummy that takes hits at face value (no DEF) and never dies.</summary>
        static Actor Target(DungeonRun run, int x, int y)
        {
            var dummy = Dummy(run, x, y);
            dummy.Defense = 0;
            return dummy;
        }

        /// <summary>Asserts a melee hit of <paramref name="percent"/>% ATK that didn't crit: the formula within its 85-100% spread.</summary>
        static void AssertHitOf(int percent, Actor attacker, DamageEvent hit, string what)
        {
            int full = attacker.Attack * percent / 100;
            Assert.IsFalse(hit.Critical, what);
            Assert.That(hit.Amount, Is.InRange(full * CombatRules.SpreadMinPercent / 100 - 1, full), what);
        }

        // ---- Tier 5 ----

        [Test]
        public void TierFiveHasAnOptionOnEachPath()
        {
            var tier = ClassCatalog.Fencer.MilestoneAt(5);
            Assert.IsNotNull(tier, "tier 5 is written");
            CollectionAssert.AreEqual(new[] { "fencer_precise_thrusts", "fencer_long_lunge", "fencer_sharp_riposte" }, tier.Options.Select(option => option.Id));
            Assert.AreSame(SkillCatalog.TripleThrust, tier.Options[0].Upgrades, "the Duelist's flurry");
            Assert.AreSame(SkillCatalog.Lunge, tier.Options[1].Upgrades, "Footwork's lunge");
            Assert.AreSame(SkillCatalog.Riposte, tier.Options[2].Upgrades, "En Garde's counter");
        }

        [Test]
        public void PreciseThrustsMakesTheThirdThrustTheHeavyOne()
        {
            var kristela = Fencer(5, "fencer_precise_thrusts");
            var thrusts = Own(kristela, SkillCatalog.TripleThrust);
            Assert.AreEqual(150, thrusts.CritRateBonus, "in tenths of a percent");
            Assert.AreEqual(130, thrusts.LastHitPower);
            Assert.AreEqual(90 + 90 + 130, thrusts.TotalPower);
            Assert.AreEqual(0, SkillCatalog.TripleThrust.CritRateBonus, "the catalog's skill is never changed");
            Assert.AreEqual(270, SkillCatalog.TripleThrust.TotalPower);
            string text = SkillText.Describe(thrusts);
            StringAssert.Contains("3 hits of 90% ATK each, the last one of 130%, on a foe next to the hero.", text);
            StringAssert.Contains("Each hit has +15% Crit Rate.", text);

            var run = TestRuns.With(kristela, Corridor);
            run.Hero.CritRate = -150; // With the skill's +15% that is no chance at all: the hits can be measured.
            var foe = Target(run, 2, 1);
            Assert.IsTrue(run.UseSkillAt(Slot(run.Hero, SkillCatalog.TripleThrust), foe.Pos));
            var hits = run.Events.OfType<DamageEvent>().Where(hit => hit.TargetId == foe.Id).ToList();
            Assert.AreEqual(3, hits.Count);
            AssertHitOf(90, run.Hero, hits[0], "the first thrust");
            AssertHitOf(90, run.Hero, hits[1], "the second");
            AssertHitOf(130, run.Hero, hits[2], "the third");
        }

        [Test]
        public void PreciseThrustsAddsItsCritRateToEachThrustOnly()
        {
            var kristela = Fencer(5, "fencer_precise_thrusts");
            var run = TestRuns.With(kristela, Corridor);
            run.Hero.CritRate = 850; // 85% of her own, and the skill's 15%: every thrust crits.
            var foe = Target(run, 2, 1);
            Assert.IsTrue(run.UseSkillAt(Slot(run.Hero, SkillCatalog.TripleThrust), foe.Pos));
            var hits = run.Events.OfType<DamageEvent>().Where(hit => hit.TargetId == foe.Id).ToList();
            Assert.AreEqual(3, hits.Count);
            Assert.IsTrue(hits.All(hit => hit.Critical));

            // Her other blows keep her own Crit Rate: with none, a Thrust never crits.
            run.Hero.CritRate = 0;
            for (int i = 0; i < 20; i++)
            {
                Assert.IsTrue(run.AttackAt(foe.Pos));
                Assert.IsFalse(run.Events.OfType<DamageEvent>().First(hit => hit.TargetId == foe.Id).Critical);
            }
        }

        [Test]
        public void LongLungeReachesFourTilesAndHitsHarder()
        {
            var kristela = Fencer(5, "fencer_long_lunge");
            var lunge = Own(kristela, SkillCatalog.Lunge);
            Assert.AreEqual(3, lunge.DashTiles);
            Assert.AreEqual(4, lunge.StrikeReach);
            Assert.AreEqual(240, lunge.Power);
            Assert.AreEqual(2, SkillCatalog.Lunge.DashTiles, "the catalog's skill is never changed");
            StringAssert.Contains("A hit of 240% ATK on a foe up to 4 tiles away in a straight line", SkillText.Describe(lunge));

            var run = TestRuns.With(kristela, Corridor); // She stands at (1, 1).
            run.Hero.CritRate = 0;
            var tooFar = Target(run, 6, 1);
            int slot = Slot(run.Hero, SkillCatalog.Lunge);
            Assert.IsFalse(run.UseSkillAt(slot, tooFar.Pos), "five tiles away: out of reach");
            Place(tooFar, 9, 1);
            var foe = Target(run, 5, 1);
            Assert.IsTrue(run.UseSkillAt(slot, foe.Pos), "four tiles away");
            Assert.AreEqual(new GridPos(4, 1), run.Hero.Pos, "she lands in front of it");
            AssertHitOf(240, run.Hero, run.Events.OfType<DamageEvent>().First(hit => hit.TargetId == foe.Id), "the lunge");
        }

        [Test]
        public void SharpRiposteCutsMoreAndAnswersHarder()
        {
            var kristela = Fencer(5, "fencer_sharp_riposte");
            var riposte = Own(kristela, SkillCatalog.Riposte);
            Assert.AreEqual(320, riposte.Power);
            Assert.AreEqual(60, riposte.StatusPower);
            Assert.AreEqual(30, riposte.BossStatusPower);
            Assert.AreEqual(250, SkillCatalog.Riposte.Power, "the catalog's skill is never changed");
            StringAssert.Contains("it takes 60% less damage (30% less from a boss), and the first foe that hits it from the next tile is struck back for 320% ATK",
                SkillText.Describe(riposte));

            var run = TestRuns.With(kristela, Corridor);
            run.Hero.MaxHp = run.Hero.Hp = 100000;
            run.Hero.CritRate = 0;
            var foe = Target(run, 2, 1);
            var troll = run.SpawnEnemy(new GridPos(9, 1), ActorCatalog.Troll);
            Assert.IsTrue(run.UseSkill(Slot(run.Hero, SkillCatalog.Riposte)));
            // The foe bit her on its turn and was answered at once.
            var counter = run.Events.OfType<CounterEvent>().Single();
            Assert.AreEqual(foe.Id, counter.TargetId);
            AssertHitOf(320, run.Hero, run.Events.OfType<DamageEvent>().First(hit => hit.TargetId == foe.Id), "the counter");

            // The stance's cut, as it stood: taken again by hand, since her next turn has ended the first one.
            run.Hero.Statuses.Add(new StatusEffect(StatusKind.Riposte, run.Hero.Id, riposte.StatusPower, 0, endsOnSourceTurn: true,
                bossPower: riposte.BossStatusPower, counterPercent: riposte.Power));
            Assert.AreEqual(40, run.DamageTakenPercent(run.Hero, foe));
            Assert.AreEqual(70, run.DamageTakenPercent(run.Hero, troll));
        }

        // ---- What the party's AI does with them ----

        [Test]
        public void TheAiUsesALongLungeAsABlowWhileTripleThrustRests()
        {
            var run = TestRuns.With(Fencer(5, "fencer_long_lunge"), Corridor);
            var foe = Target(run, 2, 1);
            int thrusts = Slot(run.Hero, SkillCatalog.TripleThrust), lunge = Slot(run.Hero, SkillCatalog.Lunge);
            run.Hero.SkillCooldowns[Slot(run.Hero, SkillCatalog.Riposte)] = 9; // No stance in this one: the choice of blow.

            var first = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommandKind.Skill, first.Kind);
            Assert.AreEqual(thrusts, first.Slot, "270% in three thrusts beats 240%");
            run.Hero.SkillCooldowns[thrusts] = 1;
            var second = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommandKind.Skill, second.Kind);
            Assert.AreEqual(lunge, second.Slot, "at 240% the lunge beats her Thrust's 200%, on a foe next to her too");

            // As the catalog has it (200%), a Lunge is for closing the distance: next to a foe she thrusts.
            var plain = TestRuns.With(Fencer(1), Corridor);
            Target(plain, 2, 1);
            plain.Hero.SkillCooldowns[Slot(plain.Hero, SkillCatalog.TripleThrust)] = 1;
            plain.Hero.SkillCooldowns[Slot(plain.Hero, SkillCatalog.Riposte)] = 9;
            Assert.AreEqual(HeroCommandKind.Attack, AutoPilot.Decide(plain).Kind);
        }
    }
}

using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;
using static FiveKingdoms.Tests.PartyTests;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// The Paladin's milestone options (PROGRESSION.md, "Starting class content"), on Haiden: what each one changes on
    /// his own copy of a skill, what it does in a run, and what the party's AI does with it.
    /// </summary>
    public class PaladinTiersTests
    {
        static readonly string[] Room =
        {
            "###########",
            "#.........#",
            "#.........#",
            "#@........#",
            "#.........#",
            "#.........#",
            "###########",
        };

        static HeroProgress Paladin(int level, params string[] picks) => TestHeroes.Built(ActorCatalog.Haiden, level, picks);

        static int Slot(Actor hero, SkillDefinition skill) => TestHeroes.Slot(hero, skill);

        static SkillDefinition Own(HeroProgress hero, SkillDefinition skill) => hero.Kit.Skills.Single(own => own.Id == skill.Id);

        static bool Taunted(Actor foe) => foe.FindStatus(StatusKind.Taunt) != null;

        /// <summary>
        /// A dummy too slow to act twice before the heroes are up again: after an action of the leader's it has had
        /// exactly one turn, so a test can tell a taunt of one turn from one of two.
        /// </summary>
        static Actor Slow(Actor foe)
        {
            foe.Speed = 50;
            return foe;
        }

        /// <summary>
        /// Kristela leads with Haiden beside her and two foes that stand next to both: a monster goes for the nearest
        /// hero, and between equals for the first in the party, so both are after her.
        /// </summary>
        static DungeonRun TwoOnKristela(HeroProgress haiden, out Actor paladin, out Actor upper, out Actor lower, int foeHp = 100000)
        {
            var run = TestRuns.With(new[] { new HeroProgress(ActorCatalog.Kristela), haiden }, Room);
            paladin = run.Party[1];
            Place(run.Hero, 3, 3);
            Place(paladin, 3, 2);
            upper = Slow(Dummy(run, 4, 3, foeHp));
            lower = Slow(Dummy(run, 4, 2, foeHp));
            Assert.AreSame(run.Hero, EnemyBrain.TargetOf(run, upper), "the test's foes are after Kristela");
            Assert.AreSame(run.Hero, EnemyBrain.TargetOf(run, lower));
            return run;
        }

        // ---- Tier 5 ----

        [Test]
        public void TierFiveHasAnOptionOnEachPath()
        {
            var tier = ClassCatalog.Paladin.MilestoneAt(5);
            Assert.IsNotNull(tier, "tier 5 is written");
            CollectionAssert.AreEqual(new[] { "paladin_challenge", "paladin_greater_heal", "paladin_searing_smite" }, tier.Options.Select(option => option.Id));
            Assert.AreSame(SkillCatalog.ShoulderBash, tier.Options[0].Upgrades, "the Guardian's bash taunts them all");
            Assert.AreSame(SkillCatalog.PaladinHeal, tier.Options[1].Upgrades, "Devotion heals more");
            Assert.AreSame(SkillCatalog.DivineStrike, tier.Options[2].Upgrades, "the Crusader smites harder");
            foreach (var option in tier.Options) Assert.IsNull(option.Teaches, option.Name + " upgrades a skill of his starting kit: no slot changes");
        }

        [Test]
        public void ChallengeTauntsEveryFoeNextToHim()
        {
            var haiden = Paladin(5, "paladin_challenge");
            var bash = Own(haiden, SkillCatalog.ShoulderBash);
            Assert.IsTrue(bash.StatusAround);
            Assert.AreEqual(2, bash.StatusTurns);
            Assert.IsFalse(SkillCatalog.ShoulderBash.StatusAround, "the catalog's skill is never changed");
            Assert.AreEqual(1, SkillCatalog.ShoulderBash.StatusTurns);
            StringAssert.Contains("Taunts the foe and every other foe next to the hero: for 2 turns they go for the hero.", SkillText.Describe(bash));

            var run = TestRuns.With(haiden, Room); // Haiden at (1, 3).
            var target = Slow(Dummy(run, 2, 3));
            var above = Slow(Dummy(run, 2, 4));
            var beside = Slow(Dummy(run, 1, 4));
            var apart = Slow(Dummy(run, 4, 5)); // Two tiles off: not next to him.
            Assert.IsTrue(run.UseSkillAt(Slot(run.Hero, SkillCatalog.ShoulderBash), target.Pos));

            var taunts = run.Events.OfType<StatusAppliedEvent>().Where(status => status.Kind == StatusKind.Taunt).Select(status => status.ActorId).ToList();
            CollectionAssert.AreEquivalent(new[] { target.Id, above.Id, beside.Id }, taunts, "the one he bashed and the two next to him");
            Assert.IsTrue(Taunted(target) && Taunted(above) && Taunted(beside), "a turn of theirs later it still holds: it lasts two");
            Assert.AreEqual(1, above.FindStatus(StatusKind.Taunt).TurnsLeft);
            Assert.IsFalse(Taunted(apart));
        }

        [Test]
        public void WithoutChallengeTheBashTauntsItsTargetOnly()
        {
            var run = TwoOnKristela(Paladin(5, "paladin_searing_smite"), out var haiden, out var upper, out var lower);
            Assert.IsTrue(run.SwitchLeader(1) || run.Hero == haiden);
            Assert.IsTrue(run.UseSkillAt(Slot(haiden, SkillCatalog.ShoulderBash), lower.Pos));
            var taunts = run.Events.OfType<StatusAppliedEvent>().Where(status => status.Kind == StatusKind.Taunt).Select(status => status.ActorId).ToList();
            CollectionAssert.AreEqual(new[] { lower.Id }, taunts);
            Assert.IsFalse(Taunted(lower), "one turn of its own, and the taunt is over");
            Assert.AreSame(run.Party[0], EnemyBrain.TargetOf(run, upper), "the other one is still after Kristela");
        }

        [Test]
        public void ChallengeTurnsTheFoesOnAnAllyToHaiden()
        {
            var run = TwoOnKristela(Paladin(5, "paladin_challenge"), out var haiden, out var upper, out var lower);
            run.SwitchLeader(1);
            Assert.AreSame(haiden, run.Hero);
            Assert.IsTrue(run.UseSkillAt(Slot(haiden, SkillCatalog.ShoulderBash), lower.Pos));
            var taunts = run.Events.OfType<StatusAppliedEvent>().Where(status => status.Kind == StatusKind.Taunt).Select(status => status.ActorId).ToList();
            CollectionAssert.AreEquivalent(new[] { lower.Id, upper.Id }, taunts);
            Assert.AreSame(haiden, EnemyBrain.TargetOf(run, upper), "it was after Kristela");
            Assert.AreSame(haiden, EnemyBrain.TargetOf(run, lower));
        }

        [Test]
        public void GreaterHealRestoresMoreOfHisHp()
        {
            var haiden = Paladin(5, "paladin_greater_heal");
            var heal = Own(haiden, SkillCatalog.PaladinHeal);
            Assert.AreEqual(24, heal.Power);
            Assert.AreEqual(20, SkillCatalog.PaladinHeal.Power, "the catalog's skill is never changed");
            StringAssert.Contains("for 24% of the hero's max HP", SkillText.Describe(heal));

            var run = TestRuns.With(haiden, Room);
            run.Hero.Hp = 1;
            Assert.IsTrue(run.UseSkill(Slot(run.Hero, SkillCatalog.PaladinHeal)));
            Assert.AreEqual(run.Hero.MaxHp * 24 / 100, run.Events.OfType<HealedEvent>().Single().Amount);
        }

        [Test]
        public void SearingSmiteHitsForThreeHundredAndTwenty()
        {
            var haiden = Paladin(5, "paladin_searing_smite");
            var smite = Own(haiden, SkillCatalog.DivineStrike);
            Assert.AreEqual(320, smite.Power);
            Assert.AreEqual(250, SkillCatalog.DivineStrike.Power, "the catalog's skill is never changed");
            StringAssert.Contains("A hit of 320% ATK on a foe next to the hero.", SkillText.Describe(smite));
            StringAssert.Contains("Fire damage.", SkillText.Describe(smite), "it is still his smite");

            var run = TestRuns.With(haiden, Room);
            run.Hero.CritRate = 0;
            var foe = Dummy(run, 2, 3);
            foe.Defense = 0;
            Assert.IsTrue(run.UseSkillAt(Slot(run.Hero, SkillCatalog.DivineStrike), foe.Pos));
            int full = run.Hero.Attack * 320 / 100;
            Assert.That(run.Events.OfType<DamageEvent>().First(hit => hit.TargetId == foe.Id).Amount,
                Is.InRange(full * CombatRules.SpreadMinPercent / 100 - 1, full));
        }

        // ---- What the party's AI does with them ----

        [Test]
        public void TheAiChallengesWhenAFoeNextToHimIsAfterAnAlly()
        {
            // Weak foes: a taunt on one of them alone isn't worth a turn (it falls in a hit or two).
            var run = TwoOnKristela(Paladin(5, "paladin_challenge"), out var haiden, out var upper, out var lower, foeHp: 150);
            int bash = Slot(haiden, SkillCatalog.ShoulderBash);
            var command = PartnerBrain.Decide(run, haiden);
            Assert.AreEqual(HeroCommandKind.Skill, command.Kind);
            Assert.AreEqual(bash, command.Slot, "two foes next to him are after Kristela: the challenge first");

            // Once they are his, it is a blow like any other: the smite hits harder.
            foreach (var foe in new[] { upper, lower }) foe.Statuses.Add(new StatusEffect(StatusKind.Taunt, haiden.Id, 0, 2, endsOnSourceTurn: false));
            command = PartnerBrain.Decide(run, haiden);
            Assert.IsFalse(command.Kind == HeroCommandKind.Skill && command.Slot == bash, "nobody left to turn");
        }

        [Test]
        public void WithoutChallengeTheAiKeepsTheBashForSturdyFoes()
        {
            var run = TwoOnKristela(Paladin(5, "paladin_searing_smite"), out var haiden, out _, out _, foeHp: 150);
            var command = PartnerBrain.Decide(run, haiden);
            Assert.AreEqual(HeroCommandKind.Skill, command.Kind);
            Assert.AreEqual(Slot(haiden, SkillCatalog.DivineStrike), command.Slot, "his hardest blow on a foe that falls in a hit or two");
        }
    }
}

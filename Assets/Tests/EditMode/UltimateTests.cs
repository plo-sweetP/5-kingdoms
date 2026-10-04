using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;
using static FiveKingdoms.Tests.PartyTests;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// No mana (PROGRESSION.md, "Skill resources"): the ultimate's charge meter, filled by acting, hitting and being hit,
    /// and the starting party's ultimates: Volley, Aura of Protection and Flurry of Blows.
    /// </summary>
    public class UltimateTests
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

        static readonly string[] Corridor =
        {
            "############",
            "#@.........#",
            "############",
        };

        static ActorDefinition[] Only(ActorDefinition hero) => new[] { hero };

        static int HitsOn(DungeonRun run, Actor target) => run.Events.OfType<DamageEvent>().Count(hit => hit.TargetId == target.Id);

        [Test]
        public void ActingHittingAndBeingHitChargeTheMeter()
        {
            var run = Run(Only(ActorCatalog.Kristela), Corridor);
            Dummy(run, 2, 1, attack: 50);
            Assert.AreEqual(0, run.Hero.Charge);

            run.Attack(Direction8.E); // Her jab lands, then the slime bites back.
            Assert.AreEqual(CombatRules.ChargePerAction + CombatRules.ChargePerHitDealt + CombatRules.ChargePerHitTaken, run.Hero.Charge);
            Assert.IsTrue(run.Events.OfType<ChargeChangedEvent>().All(e => e.ActorId == run.Hero.Id && e.Amount > 0));
        }

        [Test]
        public void TheMeterStopsWhenFull()
        {
            var run = Run(Only(ActorCatalog.Kristela), Corridor);
            Dummy(run, 2, 1, attack: 50);
            run.Hero.Charge = CombatRules.MaxCharge - 1;
            run.Attack(Direction8.E);
            Assert.AreEqual(CombatRules.MaxCharge, run.Hero.Charge);
            Assert.IsTrue(run.Hero.UltimateReady);
        }

        [Test]
        public void AnUltimateNeedsAFullMeterAndEmptiesIt()
        {
            var run = Run(Only(ActorCatalog.Kristela), Corridor);
            Dummy(run, 2, 1);
            Assert.AreEqual(SkillCheck.NotCharged, run.CheckUltimate(Direction8.E));
            Assert.IsFalse(run.UseUltimate(Direction8.E));
            Assert.AreEqual(0, run.Turn, "refused: no turn used");

            run.Hero.Charge = CombatRules.MaxCharge;
            Assert.AreEqual(SkillCheck.Ready, run.CheckUltimate(Direction8.E));
            Assert.IsTrue(run.UseUltimate(Direction8.E));
            var used = run.Events.OfType<SkillUsedEvent>().Single();
            Assert.AreSame(SkillCatalog.FlurryOfBlows, used.Skill, "skill events carry the ultimate, for its cutscene later");
            Assert.AreEqual(-CombatRules.MaxCharge, run.Events.OfType<ChargeChangedEvent>().First().Amount);
            Assert.AreEqual(CombatRules.ChargePerHitTaken, run.Hero.Charge, "its own five hits gave nothing back; only the slime's bite did");
        }

        [Test]
        public void ChargeCarriesOverToTheNextFloor()
        {
            var run = Run(Only(ActorCatalog.Uzuki), "#####", "#@.>#", "#####");
            run.Hero.Charge = 60;
            run.Hero.Pos = run.Map.Stairs;
            Assert.IsTrue(run.Descend());
            Assert.AreEqual(60, run.Hero.Charge);
        }

        [Test]
        public void FlurryOfBlowsStrikesFiveTimesAndHerNextTurnComesSooner()
        {
            var run = Run(Only(ActorCatalog.Kristela), Corridor);
            var slime = Dummy(run, 2, 1);
            run.Wait(); // The fight starts; Kristela (100) is up at 100 AV.
            Assert.IsTrue(run.InCombat);
            run.Hero.Charge = CombatRules.MaxCharge;

            Assert.IsTrue(run.UseUltimate(Direction8.E));
            Assert.AreEqual(5, HitsOn(run, slime));
            Assert.AreEqual(AvTime.FromWhole(170), run.Forecast(1)[0].Time, "70% of a turn: 30% sooner");
        }

        [Test]
        public void FlurryOfBlowsMovesOnWhenItsTargetFalls()
        {
            var run = Run(Only(ActorCatalog.Kristela), Room);
            var weak = Dummy(run, 2, 3, hp: 1);
            var other = Dummy(run, 2, 4);
            run.Hero.Charge = CombatRules.MaxCharge;

            Assert.IsTrue(run.UseUltimate(Direction8.E));
            Assert.IsFalse(weak.IsAlive);
            Assert.AreEqual(4, HitsOn(run, other));
        }

        [Test]
        public void VolleyRainsOnTheAreaAroundTheTarget()
        {
            var run = Run(Only(ActorCatalog.Uzuki), Room); // Uzuki at (1, 3).
            var center = Dummy(run, 5, 3);
            var beside = Dummy(run, 6, 4);
            var outside = Dummy(run, 8, 3);
            run.Hero.Charge = CombatRules.MaxCharge;

            Assert.IsTrue(run.UseUltimate(Direction8.E));
            var area = run.Events.OfType<AreaAttackEvent>().Single();
            Assert.AreEqual(new GridPos(5, 3), area.Center);
            Assert.AreEqual(1, area.Radius, "3x3");
            Assert.AreEqual(2, HitsOn(run, center));
            Assert.AreEqual(2, HitsOn(run, beside));
            Assert.AreEqual(0, HitsOn(run, outside));
        }

        [Test]
        public void VolleyCanBeCenteredOnAnyFoeInSight()
        {
            var run = Run(Only(ActorCatalog.Uzuki), Room); // Uzuki at (1, 3).
            var onLine = Dummy(run, 3, 3);
            var offLine = Dummy(run, 5, 5); // Four over, two up: on none of the 8 lines, and out of the first one's area.
            run.Hero.Charge = CombatRules.MaxCharge;

            Assert.AreEqual(SkillCheck.Ready, run.CheckUltimateAt(run.Hero, offLine.Pos));
            Assert.AreEqual(SkillCheck.NoTarget, run.CheckUltimateAt(run.Hero, new GridPos(4, 4)), "it needs a foe to center on");
            Assert.IsTrue(run.Execute(HeroCommand.UltimateAt(new GridPos(5, 5))));
            Assert.AreEqual(new GridPos(5, 5), run.Events.OfType<AreaAttackEvent>().Single().Center);
            Assert.AreEqual(2, HitsOn(run, offLine));
            Assert.AreEqual(0, HitsOn(run, onLine));
        }

        [Test]
        public void AuraOfProtectionShieldsAndHealsHaidenAndTheAlliesNextToHim()
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela, ActorCatalog.Uzuki }, Room);
            foreach (var member in run.Party) run.SetTactic(member, PartyTactic.Hold);
            var haiden = run.Hero;
            var kristela = run.Party[1];
            var uzuki = run.Party[2];
            Place(haiden, 3, 3);
            Place(kristela, 4, 3);
            Place(uzuki, 1, 1);
            kristela.Hp = 300;
            haiden.Hp = haiden.MaxHp - 200;
            haiden.Charge = CombatRules.MaxCharge;
            int heal = haiden.MaxHp * SkillCatalog.AuraOfProtection.Power / 100;

            Assert.IsTrue(run.UseUltimate());
            Assert.AreEqual(70, run.DamageTakenPercent(kristela), "30% less next to Haiden");
            Assert.AreEqual(70, run.DamageTakenPercent(haiden), "and for Haiden himself: the aura covers its caster too");
            Assert.AreEqual(100, run.DamageTakenPercent(uzuki), "not next to him");
            Assert.AreEqual(300 + heal, kristela.Hp, "healed as his next turn starts");
            Assert.AreEqual(haiden.MaxHp - 200 + heal, haiden.Hp, "and so is he");

            run.Wait();
            Assert.AreEqual(300 + 2 * heal, kristela.Hp);
            Assert.AreEqual(haiden.MaxHp - 200 + 2 * heal, haiden.Hp);
            run.Wait();
            Assert.AreEqual(kristela.MaxHp, kristela.Hp, "a third heal, up to her max");
            Assert.AreEqual(haiden.MaxHp - 200 + 3 * heal, haiden.Hp);
            Assert.IsNull(haiden.FindStatus(StatusKind.Aura), "three of his turns, then it's over");
            Assert.AreEqual(100, run.DamageTakenPercent(kristela));
            Assert.AreEqual(100, run.DamageTakenPercent(haiden));
        }

        [Test]
        public void HaidenRaisesTheAuraForHimselfToo()
        {
            var run = Run(Only(ActorCatalog.Haiden), Room);
            Dummy(run, 2, 3);
            run.Hero.Charge = CombatRules.MaxCharge;
            Assert.AreNotEqual(HeroCommandKind.Ultimate, AutoPilot.Decide(run).Kind, "not before a fight");

            run.Wait(); // The slime bites: the fight starts.
            Assert.IsTrue(run.InCombat);
            run.Hero.Charge = CombatRules.MaxCharge;
            Assert.AreEqual(HeroCommandKind.Ultimate, AutoPilot.Decide(run).Kind, "alone and in melee: the aura covers him");
        }

        [Test]
        public void TheAutoPilotDoesntWasteTheVolleyOnAFoeAShotWouldFinish()
        {
            var run = Run(Only(ActorCatalog.Uzuki), Room);
            Dummy(run, 5, 3, hp: 50);
            run.Hero.Charge = CombatRules.MaxCharge;
            Assert.AreNotEqual(HeroCommandKind.Ultimate, AutoPilot.Decide(run).Kind, "one weak slime isn't worth it");

            Dummy(run, 5, 4, hp: 50);
            var command = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommandKind.Ultimate, command.Kind, "two in the area are");
            Assert.AreEqual(HeroCommand.UltimateAt(new GridPos(5, 3)), command, "centered on one of them");
        }

        [Test]
        public void TheAutoPilotCentersTheVolleyWhereItCatchesTheMost()
        {
            var run = Run(Only(ActorCatalog.Uzuki), Room); // Uzuki at (1, 3).
            Dummy(run, 3, 3, hp: 50); // The weakest, and alone.
            Dummy(run, 5, 4);
            Dummy(run, 6, 5);
            Dummy(run, 6, 3);
            run.Hero.Charge = CombatRules.MaxCharge;
            Assert.AreEqual(HeroCommand.UltimateAt(new GridPos(5, 4)), AutoPilot.Decide(run), "the three to the east, around the middle one");
        }

        [Test]
        public void APartnerRaisesTheAuraWhenAnAllyNextToItIsInMelee()
        {
            var run = Run(new[] { ActorCatalog.Kristela, ActorCatalog.Haiden }, Room);
            var kristela = run.Hero;
            var haiden = run.Party[1];
            Place(kristela, 3, 3);
            Place(haiden, 2, 3);
            Dummy(run, 4, 3);
            run.Wait(); // The fight starts.
            Assert.IsTrue(run.InCombat);
            haiden.Charge = CombatRules.MaxCharge;
            Assert.AreEqual(HeroCommandKind.Ultimate, PartnerBrain.Decide(run, haiden).Kind);
        }
    }
}

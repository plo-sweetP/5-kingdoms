using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;
using static FiveKingdoms.Tests.PartyTests;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// Footing in a boss fight (PROGRESSION.md, "Footing in a boss fight"): what counts as a way out of the slam, and
    /// what the slam says about the heroes it catches.
    /// </summary>
    public class FootingTests
    {
        static readonly string[] Arena =
        {
            "#########",
            "#.......#",
            "#.......#",
            "#.@.....#",
            "#.......#",
            "#########",
        };

        static readonly string[] Corridor =
        {
            "############",
            "#@.........#",
            "############",
        };

        /// <summary>A room (y 3-4) over an alcove three tiles wide and two deep (x 2-4, y 1-2).</summary>
        static readonly string[] Alcove =
        {
            "#######",
            "#@....#",
            "#.....#",
            "##...##",
            "##...##",
            "#######",
        };

        static Actor Troll(DungeonRun run, int x, int y)
        {
            var troll = run.SpawnEnemy(new GridPos(x, y), ActorCatalog.Troll);
            troll.SpecialCooldown = 0;
            troll.Alerted = true;
            return troll;
        }

        /// <summary>One hero on <see cref="Arena"/> at (x, 2) with the Troll just east of it, ready to wind up.</summary>
        static (DungeonRun run, Actor boss) BossEastOfHero(int x)
        {
            var run = TestRuns.OnMap(1, new HeroProgress(ActorCatalog.Uzuki, 10), ActorCatalog.Troll, Arena);
            Place(run.Hero, x, 2);
            return (run, Troll(run, x + 1, 2));
        }

        static SlamCaughtEvent Caught(DungeonRun run, Actor hero) =>
            run.Events.OfType<SlamCaughtEvent>().SingleOrDefault(caught => caught.TargetId == hero.Id);

        // ---- A way out ----

        [Test]
        public void InTheOpenThereIsAWayOut()
        {
            var (run, boss) = BossEastOfHero(2);
            Assert.IsTrue(HeroTactics.HasWayOut(run, run.Hero, boss));
        }

        [Test]
        public void WithItsBackToTheWallThereIsNone()
        {
            // Every free tile beside the hero is next to the boss too.
            var (run, boss) = BossEastOfHero(1);
            Assert.IsFalse(HeroTactics.HasWayOut(run, run.Hero, boss));
        }

        [Test]
        public void AHeroOutOfTheSlamsReachHasOne()
        {
            var (run, boss) = BossEastOfHero(1);
            Place(boss, 3, 2);
            Assert.IsTrue(HeroTactics.HasWayOut(run, run.Hero, boss));
        }

        [Test]
        public void BoxedInByAnAllyThereIsNone()
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela }, Corridor);
            var haiden = run.Party[0];
            var kristela = run.Party[1];
            Place(kristela, 1, 1);
            Place(haiden, 2, 1);
            var boss = Troll(run, 3, 1);
            Assert.IsFalse(HeroTactics.HasWayOut(run, haiden, boss), "Kristela stands on the only tile behind him");
            Assert.IsTrue(HeroTactics.HasWayOut(run, kristela, boss), "she is out of reach");
        }

        [Test]
        public void ATileAnotherHeroNeedsAsItsOnlyWayOutIsNotOne()
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela }, Alcove);
            var haiden = run.Party[0];
            var kristela = run.Party[1];
            var boss = Troll(run, 3, 3);
            Place(haiden, 2, 2);
            Place(kristela, 4, 2);
            Assert.IsTrue(HeroTactics.HasWayOut(run, haiden, boss), "two tiles behind each of them");
            Assert.IsTrue(HeroTactics.HasWayOut(run, kristela, boss));

            Dummy(run, 4, 1); // Kristela is left with (3, 1) only.
            Assert.IsTrue(HeroTactics.HasWayOut(run, kristela, boss));
            Assert.IsTrue(HeroTactics.HasWayOut(run, haiden, boss), "he still has (2, 1) to himself");

            Dummy(run, 2, 1); // Now both have (3, 1) only: it can't be the way out of both.
            Assert.IsFalse(HeroTactics.HasWayOut(run, haiden, boss));
            Assert.IsFalse(HeroTactics.HasWayOut(run, kristela, boss));

            Place(kristela, 1, 4); // Without her it is his.
            Assert.IsTrue(HeroTactics.HasWayOut(run, haiden, boss));
        }

        // ---- What the slam says ----

        [Test]
        public void TheSlamSaysAHeroItCatchesHadNoWayOut()
        {
            var (run, boss) = BossEastOfHero(1);
            run.Wait(); // The boss winds up.
            Assert.IsTrue(boss.Charging);
            Assert.AreEqual(SlamFooting.Cornered, run.Hero.Footing);

            run.Wait();
            var caught = Caught(run, run.Hero);
            Assert.IsNotNull(caught);
            Assert.AreEqual(SlamFooting.Cornered, caught.Footing);
            Assert.IsFalse(caught.Braced);
            Assert.AreEqual(boss.Id, caught.BossId);
            // The view takes the slam's hits from what follows the boss's action: the note comes before it.
            var events = run.Events.ToList();
            int slam = events.FindIndex(e => e is BossActionEvent action && action.Action == BossAction.Slam);
            Assert.AreEqual(slam - 1, events.IndexOf(caught));
            Assert.IsInstanceOf<DamageEvent>(events[slam + 1]);
        }

        [Test]
        public void TheSlamSaysAHeroThatStayedHadAWayOut()
        {
            var (run, _) = BossEastOfHero(2);
            run.Wait();
            run.Wait(); // By hand a hero may stay where it is.
            Assert.AreEqual(SlamFooting.WayOut, Caught(run, run.Hero).Footing);
        }

        [Test]
        public void TheSlamSaysAHeroIsBracedBehindAGuard()
        {
            var (run, _) = BossEastOfHero(1);
            run.Wait();
            run.Hero.Statuses.Add(new StatusEffect(StatusKind.Guard, run.Hero.Id, 50, 5, endsOnSourceTurn: false));
            run.Wait();
            Assert.IsTrue(Caught(run, run.Hero).Braced);
        }

        [Test]
        public void AHeroThatSteppedOutIsNotCaught()
        {
            var (run, _) = BossEastOfHero(2);
            run.Wait();
            run.Move(Direction8.W);
            Assert.IsTrue(run.Events.OfType<BossActionEvent>().Any(e => e.Action == BossAction.Slam));
            Assert.IsFalse(run.Events.OfType<SlamCaughtEvent>().Any());
        }

        // ---- Staying out of a wind-up ----

        /// <summary>Haiden leads on <see cref="Arena"/> with Kristela beside him and the Troll some tiles east, the fight on.</summary>
        static DungeonRun TwoAgainstTheTroll(out Actor haiden, out Actor kristela, out Actor troll, int trollX)
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela }, Arena);
            haiden = run.Party[0];
            kristela = run.Party[1];
            Place(haiden, 1, 3);
            Place(kristela, 1, 2);
            troll = Troll(run, trollX, 2);
            troll.SpecialCooldown = 99;
            return run;
        }

        static int Slot(Actor hero, SkillDefinition skill)
        {
            for (int i = 0; i < hero.Skills.Count; i++)
                if (hero.Skills[i].Id == skill.Id) return i;
            return -1;
        }

        [Test]
        public void APartnerDoesNotWalkIntoAWindUp()
        {
            var run = TwoAgainstTheTroll(out _, out var kristela, out var troll, trollX: 3);
            kristela.SkillCooldowns[Slot(kristela, SkillCatalog.Lunge)] = 9;
            Assert.AreEqual(HeroCommandKind.Move, PartnerBrain.Decide(run, kristela).Kind, "she closes in");

            troll.Charging = true;
            var command = PartnerBrain.Decide(run, kristela);
            Assert.IsTrue(command.KeepingClear, "but not into the slam");
            Assert.AreEqual(HeroCommandKind.Wait, command.Kind);
        }

        [Test]
        public void SheDoesNotLungeIntoAWindUp()
        {
            var run = TwoAgainstTheTroll(out _, out var kristela, out var troll, trollX: 4);
            Assert.AreEqual(HeroCommand.SkillAt(Slot(kristela, SkillCatalog.Lunge), troll.Pos), PartnerBrain.Decide(run, kristela));

            troll.Charging = true;
            Assert.IsTrue(PartnerBrain.Decide(run, kristela).KeepingClear);
        }

        [Test]
        public void TheAutoLeaderKeepsClearTooAndTheRulesSaySo()
        {
            var run = Run(new[] { ActorCatalog.Haiden }, Arena);
            Place(run.Hero, 1, 2);
            var troll = Troll(run, 3, 2);
            troll.Charging = true;
            troll.SpecialCooldown = 99;

            var command = AutoPilot.Decide(run);
            Assert.IsTrue(command.KeepingClear);
            run.Execute(command);
            var waited = run.Events.OfType<HeroWaitedEvent>().Single();
            Assert.AreEqual(WaitReason.KeepsClear, waited.Reason);
            Assert.AreEqual(run.Hero.Id, waited.ActorId);
        }

        [Test]
        public void OutOfReachAHeroStillUsesItsTurnWhereItCan()
        {
            // Waiting is only for a hero with nothing to do from where it stands: a heal or a shot comes first.
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Uzuki }, Arena);
            var haiden = run.Party[0];
            var uzuki = run.Party[1];
            Place(haiden, 1, 2);
            Place(uzuki, 1, 3);
            var troll = Troll(run, 3, 2);
            troll.Charging = true;
            troll.SpecialCooldown = 99;

            var shot = PartnerBrain.Decide(run, uzuki);
            Assert.IsFalse(shot.KeepingClear);
            Assert.IsTrue(shot.Targeted, "she shoots from out of reach");
            Assert.IsTrue(AutoPilot.Decide(run).KeepingClear, "he has nothing that reaches");

            haiden.Hp = haiden.MaxHp / 4;
            var heal = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommandKind.Skill, heal.Kind);
            Assert.AreEqual(SkillEffect.Heal, haiden.Skills[heal.Slot].Effect, "hurt, he heals instead of waiting");
        }

        [Test]
        public void ByHandAHeroMayWalkIntoAWindUp()
        {
            var (run, boss) = BossEastOfHero(1);
            Place(boss, 3, 2);
            boss.Charging = true;
            Assert.IsTrue(run.Move(Direction8.E), "the rule is the AI's, not the player's");
        }

        // ---- One more blow ----

        [Test]
        public void WithTimeForOneMoreBlowAHeroStrikesBeforeItStepsOut()
        {
            var run = Run(new[] { ActorCatalog.Haiden }, Arena);
            var haiden = run.Hero;
            haiden.MaxHp = haiden.Hp = 100000;
            var troll = Troll(run, 3, 2);
            troll.SpecialCooldown = 99;
            run.Wait(); // It bites: the fight is on, and turns follow the timeline.
            Assert.IsTrue(run.InCombat);
            for (int slot = 0; slot < haiden.SkillCooldowns.Length; slot++) haiden.SkillCooldowns[slot] = 9;

            troll.Charging = true;
            run.SetSpeed(troll, 1); // The slam is a long way off: Haiden is up again before it.
            Assert.IsFalse(run.ActsBefore(troll, haiden));
            Assert.AreEqual(HeroCommand.AttackAt(troll.Pos), AutoPilot.Decide(run));

            run.SetSpeed(troll, 2000); // Now it lands before his next turn.
            Assert.IsTrue(run.ActsBefore(troll, haiden));
            Assert.AreEqual(HeroCommandKind.Move, AutoPilot.Decide(run).Kind);
        }

        // ---- A tile with a way out ----

        [Test]
        public void SteppingOutAHeroLeavesAnAllyItsOnlyWayOut()
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela }, Alcove);
            var haiden = run.Party[0];
            var kristela = run.Party[1];
            var boss = Troll(run, 3, 3);
            Place(haiden, 2, 2);
            Place(kristela, 4, 2);
            Dummy(run, 2, 1); // Haiden is left with (3, 1) only; Kristela has (3, 1) and (4, 1).
            boss.Charging = true;

            Assert.AreEqual(HeroCommand.Move(Direction8.S), PartnerBrain.Decide(run, kristela), "she takes (4, 1) and leaves him (3, 1)");
            Assert.AreEqual(HeroCommand.Move(Direction8.SE), AutoPilot.Decide(run));
        }

        [Test]
        public void OnAQuietTurnACorneredMeleeHeroMovesToATileWithAWayOut()
        {
            var run = Run(new[] { ActorCatalog.Haiden }, Arena);
            var haiden = run.Hero;
            Place(haiden, 1, 2);
            var boss = Troll(run, 2, 2);
            boss.SpecialCooldown = 99;
            Assert.IsFalse(HeroTactics.HasWayOut(run, haiden, boss));

            var command = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommandKind.Move, command.Kind, "he gives up one attack for it");
            var next = haiden.Pos + command.Direction.ToOffset();
            Assert.AreEqual(1, GridPos.ChebyshevDistance(next, boss.Pos), "still next to the boss");
            Assert.IsTrue(HeroTactics.HasWayOut(run, haiden, boss, next));

            run.Execute(command);
            Assert.IsTrue(HeroTactics.HasWayOut(run, haiden, boss));
            Assert.AreEqual(HeroCommandKind.Skill, AutoPilot.Decide(run).Kind, "and from there he fights on");
        }

        [Test]
        public void AHeroWithAWayOutStaysAndFights()
        {
            var run = Run(new[] { ActorCatalog.Haiden }, Arena);
            Place(run.Hero, 2, 2);
            var boss = Troll(run, 3, 2);
            boss.SpecialCooldown = 99;
            Assert.IsFalse(HeroTactics.TryFindFooting(run, run.Hero, out _));
        }

        [Test]
        public void ARangedHeroIsNotMovedForFooting()
        {
            var run = Run(new[] { ActorCatalog.Uzuki }, Arena);
            Place(run.Hero, 1, 2);
            var boss = Troll(run, 2, 2);
            boss.SpecialCooldown = 99;
            Assert.IsFalse(HeroTactics.TryFindFooting(run, run.Hero, out _));
        }

        [Test]
        public void WhereAHeroCouldComeInItPrefersATileWithAWayOut()
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela }, Alcove);
            var haiden = run.Party[0];
            var kristela = run.Party[1];
            var boss = Troll(run, 3, 3);
            Place(haiden, 1, 4);
            Assert.IsTrue(HeroTactics.HasFootingAt(run, kristela, new GridPos(2, 2)), "two tiles behind it");
            Dummy(run, 2, 1);
            Dummy(run, 3, 1);
            Assert.IsFalse(HeroTactics.HasFootingAt(run, kristela, new GridPos(2, 2)), "the alcove is full behind it");
            Assert.IsTrue(HeroTactics.HasFootingAt(run, kristela, new GridPos(2, 4)), "up in the room there is space");
            Assert.IsTrue(HeroTactics.HasFootingAt(run, kristela, new GridPos(1, 1)), "nowhere near the boss");
        }

        // ---- The slam is coming and there is no way out ----

        [Test]
        public void ACorneredHeroDashesOutWhereItCan()
        {
            // Against the wall the tiles beside him are in the slam too, but a dash along the wall ends beyond it.
            var run = TestRuns.OnMap(1, null, ActorCatalog.Troll, Arena);
            Place(run.Hero, 1, 2);
            var boss = Troll(run, 2, 2);
            boss.Charging = true;
            var command = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommandKind.Skill, command.Kind);
            Assert.AreEqual(SkillEffect.Dash, run.Hero.Skills[command.Slot].Effect);
            run.Execute(command);
            Assert.Greater(GridPos.ChebyshevDistance(run.Hero.Pos, boss.Pos), EnemyBrain.SlamRadius);
        }

        [Test]
        public void ACorneredArcherRollsOut()
        {
            var (run, boss) = BossEastOfHero(1);
            boss.Charging = true;
            var command = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommandKind.Skill, command.Kind);
            Assert.Greater(run.Hero.Skills[command.Slot].RollTiles, 0);
            run.Execute(command);
            Assert.Greater(GridPos.ChebyshevDistance(run.Hero.Pos, boss.Pos), EnemyBrain.SlamRadius);
            Assert.IsFalse(run.Events.OfType<SlamCaughtEvent>().Any());
        }

        [Test]
        public void ACorneredHeroRaisesItsAuraWhenItIsCharged()
        {
            var run = Run(new[] { ActorCatalog.Haiden }, Arena);
            var haiden = run.Hero;
            Place(haiden, 1, 2);
            var boss = Troll(run, 2, 2);
            boss.Charging = true;
            Assert.AreNotEqual(HeroCommandKind.Ultimate, AutoPilot.Decide(run).Kind, "nothing to brace with: he fights on");

            haiden.Charge = CombatRules.MaxCharge;
            Assert.AreEqual(HeroCommandKind.Ultimate, AutoPilot.Decide(run).Kind);
        }

        [Test]
        public void AStanceIsOnlyTakenWhenTheSlamLandsBeforeTheNextTurn()
        {
            var run = Run(new[] { ActorCatalog.Kristela }, Corridor);
            var kristela = run.Hero;
            kristela.MaxHp = kristela.Hp = 100000;
            var troll = Troll(run, 2, 1);
            troll.SpecialCooldown = 99;
            run.Wait();
            Assert.IsTrue(run.InCombat);
            for (int slot = 0; slot < kristela.SkillCooldowns.Length; slot++) kristela.SkillCooldowns[slot] = 0;

            troll.Charging = true;
            run.SetSpeed(troll, 1);
            Assert.IsFalse(run.ActsBefore(troll, kristela));
            Assert.AreNotEqual(HeroCommand.Skill(Slot(kristela, SkillCatalog.Riposte)), AutoPilot.Decide(run), "she is up again first: a blow now");

            run.SetSpeed(troll, 2000);
            Assert.AreEqual(HeroCommand.Skill(Slot(kristela, SkillCatalog.Riposte)), AutoPilot.Decide(run));
        }

        /// <summary>Kristela leads at (2, 1) of the corridor, low on HP, Haiden behind her at (1, 1), the Troll winding up at (3, 1).</summary>
        static DungeonRun BoxedInUnderASlam(out Actor kristela, out Actor haiden, bool kristelaLeads)
        {
            var members = kristelaLeads
                ? new[] { ActorCatalog.Kristela, ActorCatalog.Haiden }
                : new[] { ActorCatalog.Haiden, ActorCatalog.Kristela };
            var run = Run(members, Corridor);
            kristela = run.Party[kristelaLeads ? 0 : 1];
            haiden = run.Party[kristelaLeads ? 1 : 0];
            Place(haiden, 1, 1);
            Place(kristela, 2, 1);
            var troll = Troll(run, 3, 1);
            troll.Charging = true;
            haiden.MaxHp = haiden.Hp = 10 * run.SlamDamage(troll, haiden);
            kristela.Hp = 1;
            return run;
        }

        [Test]
        public void AHeroThatWouldFallTradesPlacesWithAnAllyThatCanTakeTheBlow()
        {
            var run = BoxedInUnderASlam(out var kristela, out var haiden, kristelaLeads: true);
            Assert.IsTrue(run.IsShelterSwap(kristela, haiden));

            var command = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommand.Move(Direction8.W), command);
            run.Execute(command);
            var swap = run.Events.OfType<SwappedEvent>().First();
            Assert.AreEqual(SwapReason.Shelter, swap.Reason);
            Assert.AreEqual(kristela.Id, swap.HurtId);
            Assert.IsTrue(kristela.IsAlive, "the slam fell on Haiden");
            Assert.IsTrue(haiden.IsAlive);
        }

        [Test]
        public void NobodyIsPutUnderASlamThatWouldLeaveItBadlyHurt()
        {
            var run = BoxedInUnderASlam(out var kristela, out var haiden, kristelaLeads: true);
            haiden.Hp = run.SlamDamage(run.Boss, haiden) + haiden.MaxHp * HeroTactics.ShelterHpPercent / 100 - 1;
            Assert.IsFalse(run.IsShelterSwap(kristela, haiden));
            Assert.AreEqual(HeroCommand.Skill(Slot(kristela, SkillCatalog.Riposte)), AutoPilot.Decide(run), "she braces instead");
        }

        [Test]
        public void TheLeaderIsNeverPutUnderASlam()
        {
            var run = BoxedInUnderASlam(out var kristela, out var haiden, kristelaLeads: false);
            Assert.AreSame(haiden, run.Hero);
            Assert.IsFalse(run.IsShelterSwap(kristela, haiden));
            Assert.AreEqual(HeroCommand.Skill(Slot(kristela, SkillCatalog.Riposte)), PartnerBrain.Decide(run, kristela));
        }

        [Test]
        public void AHeroWhoCanTakeTheSlamIsNotSwappedOut()
        {
            var run = BoxedInUnderASlam(out var kristela, out var haiden, kristelaLeads: true);
            kristela.MaxHp = kristela.Hp = 10 * run.SlamDamage(run.Boss, kristela);
            Assert.IsFalse(run.IsShelterSwap(kristela, haiden));
        }

        [Test]
        public void AHeroStandingOnAnAllysOnlyWayOutMakesWay()
        {
            var run = Run(new[] { ActorCatalog.Uzuki, ActorCatalog.Haiden, ActorCatalog.Kristela }, Corridor);
            var haiden = run.Party[1];
            var kristela = run.Party[2];
            Place(haiden, 4, 1);
            Place(kristela, 5, 1);
            var troll = Troll(run, 6, 1);
            troll.Charging = true;
            Assert.AreEqual(HeroCommand.Move(Direction8.W), PartnerBrain.Decide(run, haiden), "he steps back so she can");

            Place(kristela, 3, 1); // Nobody in the slam: he has no reason to move away.
            Assert.IsTrue(PartnerBrain.Decide(run, haiden).KeepingClear);
        }

        [Test]
        public void AHeroWithNoTurnSinceTheWindUpIsSaidSo()
        {
            // A boss three times as fast winds up and slams before the hero is up again.
            var (run, boss) = BossEastOfHero(1);
            boss.SpecialCooldown = 5;
            run.Wait(); // The fight starts.
            run.SetSpeed(boss, run.Hero.Speed * 3);
            boss.SpecialCooldown = 1;
            for (int i = 0; i < 4 && !run.Events.OfType<SlamCaughtEvent>().Any(); i++) run.Wait();
            Assert.AreEqual(SlamFooting.NoTurn, Caught(run, run.Hero).Footing);
        }
    }
}

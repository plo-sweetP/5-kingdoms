using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;
using static FiveKingdoms.Tests.PartyTests;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// PROGRESSION.md, "Ranged vs melee": ranged hits are weaker (and weaker still at point-blank range), ranged heroes
    /// step out of melee and hang back, melee partners close in and may swap past a ranged ally but never loop, and
    /// floors place monsters in packs.
    /// </summary>
    public class FormationTests
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
            "##########",
            "#@.......#",
            "##########",
        };

        static void HoldAll(DungeonRun run)
        {
            foreach (var member in run.Party) run.SetTactic(member, PartyTactic.Hold);
        }

        static void Root(Actor actor) => actor.Statuses.Add(new StatusEffect(StatusKind.Rooted, actor.Id, 0, 999, endsOnSourceTurn: false));

        [Test]
        public void RangedHitsDealThreeQuartersAndLessAtPointBlankRange()
        {
            Assert.AreEqual(100, CombatRules.ReachPercent(ranged: false, pointBlank: false));
            Assert.AreEqual(75, CombatRules.ReachPercent(ranged: true, pointBlank: false));
            Assert.AreEqual(52, CombatRules.ReachPercent(ranged: true, pointBlank: true), "75% of a melee hit, then 30% less");
        }

        [Test]
        public void AShotWithAFoeNextToTheShooterDealsLess()
        {
            int Shot(bool crowded)
            {
                var run = Run(new[] { ActorCatalog.Uzuki }, Room); // Uzuki at (1, 3).
                run.Hero.CritRate = 0;
                var target = Dummy(run, 4, 3);
                if (crowded) Dummy(run, 1, 2);
                run.Attack(Direction8.E);
                return run.Events.OfType<DamageEvent>().Single(hit => hit.TargetId == target.Id).Amount;
            }
            int clear = Shot(false);
            Assert.AreEqual(clear * 70 / 100, Shot(true), 2);
        }

        [Test]
        public void AnArcherStepsBackToATileSheCanStillShootFrom()
        {
            var run = Run(new[] { ActorCatalog.Uzuki }, Room);
            var uzuki = run.Hero;
            Place(uzuki, 3, 3);
            Dummy(run, 4, 3);
            uzuki.SkillCooldowns[2] = 1; // Rolling Shot is cooling down.

            var command = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommand.Move(Direction8.W), command, "back out of melee, still in line with it");
            run.Execute(command);
            Assert.AreEqual(1, uzuki.RetreatSteps);
        }

        [Test]
        public void IfTheFoeKeepsFollowingTheArcherShootsAnyway()
        {
            var run = Run(new[] { ActorCatalog.Uzuki }, Room);
            var uzuki = run.Hero;
            Place(uzuki, 3, 3);
            Dummy(run, 4, 3);
            for (int slot = 0; slot < uzuki.SkillCooldowns.Length; slot++) uzuki.SkillCooldowns[slot] = 1;
            uzuki.RetreatSteps = 1; // She already stepped back once and it followed.

            Assert.AreEqual(HeroCommand.AttackToward(Direction8.E), AutoPilot.Decide(run), "a point-blank Quick Shot rather than retreating forever");
        }

        [Test]
        public void MeleePartnersCloseInOnAFoeInAFight()
        {
            var run = Run(new[] { ActorCatalog.Uzuki, ActorCatalog.Haiden }, Room);
            var haiden = run.Party[1];
            run.SetTactic(haiden, PartyTactic.Follow);
            Place(run.Hero, 1, 3);
            Place(haiden, 1, 4);
            var slime = Dummy(run, 6, 3);
            Root(slime);

            run.Wait(); // The slime notices them: the fight starts.
            Assert.IsTrue(run.InCombat);
            int before = GridPos.ChebyshevDistance(haiden.Pos, slime.Pos);
            run.Wait();
            Assert.Less(GridPos.ChebyshevDistance(haiden.Pos, slime.Pos), before, "Follow or not, he goes in");
        }

        [Test]
        public void AMeleePartnerSwapsPastARangedAllyToReachAFoe()
        {
            var run = Run(new[] { ActorCatalog.Uzuki, ActorCatalog.Kristela }, Corridor);
            var uzuki = run.Hero;
            var kristela = run.Party[1];
            Place(kristela, 1, 1);
            Place(uzuki, 2, 1);
            Root(Dummy(run, 4, 1));
            HoldAll(run);
            run.Wait(); // The fight starts.
            Assert.IsTrue(run.InCombat);
            run.SetTactic(kristela, PartyTactic.Follow);

            run.Wait();
            Assert.AreEqual(new GridPos(2, 1), kristela.Pos);
            Assert.AreEqual(new GridPos(1, 1), uzuki.Pos);
            Assert.IsTrue(run.Events.OfType<SwappedEvent>().Any(e => e.ActorId == kristela.Id && e.OtherId == uzuki.Id));
        }

        [Test]
        public void MeleeHeroesNeverSwapWithEachOther()
        {
            var run = Run(new[] { ActorCatalog.Kristela, ActorCatalog.Haiden }, Corridor);
            var kristela = run.Hero;
            var haiden = run.Party[1];
            Place(haiden, 1, 1);
            Place(kristela, 2, 1);
            Root(Dummy(run, 4, 1));
            Assert.IsFalse(run.CanSwap(haiden, kristela));

            run.Wait();
            run.Wait();
            Assert.AreEqual(new GridPos(1, 1), haiden.Pos, "he waits behind her");
        }

        [Test]
        public void ASwapMustBringTheMeleeHeroCloserAndCantBeUndoneRightAway()
        {
            var run = Run(new[] { ActorCatalog.Uzuki, ActorCatalog.Kristela }, Corridor);
            var uzuki = run.Hero;
            var kristela = run.Party[1];
            Place(uzuki, 2, 1);
            Place(kristela, 3, 1);
            Dummy(run, 5, 1);
            Assert.IsFalse(run.CanSwap(kristela, uzuki), "Uzuki's tile is farther from the slime");

            Place(uzuki, 3, 1);
            Place(kristela, 2, 1);
            Assert.IsTrue(run.CanSwap(kristela, uzuki));
            kristela.SwappedWithId = uzuki.Id;
            kristela.SwapBlockTurns = DungeonRun.SwapBlockTurns;
            Assert.IsFalse(run.CanSwap(kristela, uzuki), "the two just swapped");
            Assert.IsFalse(run.CanSwap(uzuki, kristela), "and a ranged hero never swaps forward");
        }

        [Test]
        public void ARangedPartnerFindsATileToShootFromAwayFromTheFoe()
        {
            var run = Run(new[] { ActorCatalog.Kristela, ActorCatalog.Uzuki }, Room);
            var uzuki = run.Party[1];
            Place(run.Hero, 1, 3);
            Place(uzuki, 1, 1);
            var slime = Dummy(run, 4, 2);
            Root(slime);
            HoldAll(run);
            run.Wait(); // The fight starts.
            Assert.IsTrue(run.InCombat);
            run.SetTactic(uzuki, PartyTactic.Follow);
            Assert.IsFalse(run.AnyFoeInLine(uzuki.Pos, uzuki.Team, 5), "no shot from where she stands");

            var command = PartnerBrain.Decide(run, uzuki);
            Assert.AreEqual(HeroCommandKind.Move, command.Kind);
            var next = uzuki.Pos + command.Direction.ToOffset();
            Assert.IsTrue(run.AnyFoeInLine(next, uzuki.Team, 5), "a shot from there");
            Assert.IsFalse(run.FoeAdjacent(next, uzuki.Team), "without walking into melee");
        }

        [Test]
        public void FloorsPlaceMonstersInPacksAndSomePacksBringABat()
        {
            int bats = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var run = new DungeonRun(seed, new DungeonRunConfig { Party = ActorCatalog.StartingParty });
                var enemies = run.Actors.Where(actor => actor.Team == Team.Enemy).ToList();
                Assert.That(enemies.Count, Is.InRange(1, run.Config.EnemiesOnFirstFloor), $"seed {seed}");
                int alone = enemies.Count(enemy => !enemies.Any(other => other != enemy && GridPos.ChebyshevDistance(other.Pos, enemy.Pos) <= 2));
                Assert.LessOrEqual(alone, 1, $"seed {seed}: at most one leftover monster on its own");
                bats += enemies.Count(enemy => enemy.Definition == ActorCatalog.Bat);
            }
            Assert.Greater(bats, 0, "fast monsters show up");
        }

        [Test]
        public void TheBatIsFastAndFrail()
        {
            Assert.AreEqual(130, ActorCatalog.Bat.Speed);
            Assert.Less(ActorCatalog.Bat.MaxHp, ActorCatalog.Slime.MaxHp);
        }
    }
}

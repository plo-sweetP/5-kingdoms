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

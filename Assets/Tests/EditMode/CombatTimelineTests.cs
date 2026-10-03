using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    /// <summary>How the timeline plays out inside a run: exploring vs. fighting, speed, and AV-based floor timers.</summary>
    public class CombatTimelineTests
    {
        static readonly string[] Hall =
        {
            "##########",
            "#........#",
            "#.@......#",
            "#........#",
            "##########",
        };

        static ActorDefinition Slime(int speed, int attack = 1) =>
            new ActorDefinition("slime", "Slime", maxHp: 50, attack: attack, defense: 0, expReward: 1, speed: speed);

        static DungeonRun RunWithHero(int heroSpeed, int heroHp = 500)
        {
            var hero = new ActorDefinition("uzuki", "Uzuki", maxHp: heroHp, attack: 1, defense: 50, expReward: 0, speed: heroSpeed);
            return TestRuns.OnMap(1, new HeroProgress(hero), null, Hall);
        }

        static int EnemyTurnsIn(DungeonRun run, Actor enemy) =>
            run.Events.Count(e => e is AttackEvent a && a.AttackerId == enemy.Id || e is MovedEvent m && m.ActorId == enemy.Id);

        [Test]
        public void ExploringThereIsNoTimeline()
        {
            var run = RunWithHero(100);
            run.Move(Direction8.E);
            Assert.IsFalse(run.InCombat);
            Assert.AreEqual(0, run.Forecast(5).Count);
            Assert.AreEqual(AvTime.FromWhole(100), run.RunTime, "a step still costs 10000 / Speed AV");
        }

        [Test]
        public void CombatStartsWhenAnEnemyNoticesTheHeroAndEndsWhenNoneDo()
        {
            var run = RunWithHero(100);
            var slime = run.SpawnEnemy(run.Hero.Pos + new GridPos(1, 0), Slime(100));
            slime.Hp = 1;

            run.Wait(); // The slime notices and bites.
            Assert.IsTrue(run.InCombat);
            Assert.IsTrue(run.Events.OfType<CombatStartedEvent>().Any());
            Assert.AreEqual(run.Hero, run.Forecast(1)[0].Actor, "the hero's turn comes first");

            run.AttackAt(slime.Pos); // Defeat it.
            Assert.IsFalse(run.InCombat);
            Assert.IsTrue(run.Events.OfType<CombatEndedEvent>().Any());
        }

        [Test]
        public void AFasterEnemyActsTwicePerHeroTurn()
        {
            var run = RunWithHero(100);
            var quick = run.SpawnEnemy(run.Hero.Pos + new GridPos(1, 0), Slime(200));
            run.Wait(); // Exploring: one turn each, then the fight starts and the quick slime ambushes (50 AV).
            Assert.IsTrue(run.InCombat);

            run.Wait();
            Assert.AreEqual(2, EnemyTurnsIn(run, quick));
            run.Wait();
            Assert.AreEqual(2, EnemyTurnsIn(run, quick));
        }

        [Test]
        public void AFasterHeroActsTwiceForEachSlowEnemyTurn()
        {
            var run = RunWithHero(200);
            var slow = run.SpawnEnemy(run.Hero.Pos + new GridPos(1, 0), Slime(100));
            run.Wait(); // Fight starts: the hero is due at 50 AV, the slime at 100.

            int[] enemyTurns = new int[4];
            for (int i = 0; i < 4; i++)
            {
                run.Wait();
                enemyTurns[i] = EnemyTurnsIn(run, slow);
            }
            // Hero at 50 (next 100); tie at 100 goes to the hero, so the slime acts after the hero's following turn.
            CollectionAssert.AreEqual(new[] { 0, 1, 0, 1 }, enemyTurns);
        }

        [Test]
        public void RegenerationRunsOnAvTimeSoFasterHeroesTakeMoreStepsPerHp()
        {
            Assert.AreEqual(20, HpRegainedIn12Steps(heroSpeed: 100), "12 steps at speed 100 = 1200 AV = 2 ticks of 10 HP");
            Assert.AreEqual(10, HpRegainedIn12Steps(heroSpeed: 200), "12 steps at speed 200 = 600 AV = 1 tick of 10 HP");
        }

        static int HpRegainedIn12Steps(int heroSpeed)
        {
            var hero = new ActorDefinition("uzuki", "Uzuki", maxHp: 100, attack: 1, defense: 1, expReward: 0, speed: heroSpeed);
            var config = new DungeonRunConfig
            {
                MapFactory = (floor, seed) => DungeonMap.FromAscii(Hall),
                Populate = false,
                Boss = null,
                RegenIntervalAv = 600,
            };
            var run = new DungeonRun(1, config, new HeroProgress(hero));
            run.Hero.Hp = 10;
            for (int i = 0; i < 12; i++) run.Wait();
            return run.Hero.Hp - 10;
        }

        [Test]
        public void ASpeedChangeMidFightKeepsProgressTowardTheNextTurn()
        {
            var run = RunWithHero(100);
            var slime = run.SpawnEnemy(run.Hero.Pos + new GridPos(1, 0), Slime(50));
            run.Wait(); // Fight starts: the hero is due at 100 AV, the slow slime at 200.

            run.SetSpeed(slime, 100); // It still has 5000 of its gauge to cover: 50 AV at the new speed.
            Assert.AreEqual(AvTime.FromWhole(150), run.Forecast(5).First(turn => turn.Actor == slime).Time,
                "progress is kept, not reset to a full turn (which would be 200)");
        }

        [Test]
        public void ASlowBossGivesTheHeroTwoTurnsToEscapeItsSlam()
        {
            var boss = new ActorDefinition("king_slime", "King Slime", maxHp: 500, attack: 20, defense: 5, expReward: 0,
                brain: ActorBrain.SlimeKing, speed: 50);
            var run = TestRuns.OnMap(1, new HeroProgress(ActorCatalog.Uzuki, 10), boss, Hall);
            var king = run.SpawnEnemy(run.Hero.Pos + new GridPos(1, 0), boss);
            king.SpecialCooldown = 0;
            king.Alerted = true;

            run.Wait(); // The king winds up; the fight starts. Hero due at 100 AV, the king at 200.
            Assert.IsTrue(king.Charging);
            var upcoming = run.Forecast(3);
            Assert.AreEqual(run.Hero, upcoming[0].Actor);
            Assert.AreEqual(run.Hero, upcoming[1].Actor, "the hero acts twice before the slow king");
            Assert.AreEqual(king, upcoming[2].Actor);
            Assert.IsTrue(upcoming[2].Actor.Charging, "the HUD can mark that turn as the slam");

            run.Move(Direction8.W); // Two tiles from the king: out of reach. The king still hasn't acted.
            Assert.IsFalse(run.Events.OfType<BossActionEvent>().Any());
            run.Wait(); // Now the slam lands, on nothing.
            Assert.IsTrue(run.Events.OfType<BossActionEvent>().Any(e => e.Action == BossAction.Slam));
            Assert.IsFalse(run.Events.OfType<DamageEvent>().Any());
        }
    }
}

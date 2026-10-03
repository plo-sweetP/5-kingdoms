using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    public class BossTests
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

        /// <summary>A single-floor run on <see cref="Arena"/> with the King Slime standing just east of the hero.</summary>
        static (DungeonRun run, Actor boss) BossNextToHero(int heroLevel = 10)
        {
            var run = TestRuns.OnMap(1, new HeroProgress(ActorCatalog.Uzuki, heroLevel), ActorCatalog.KingSlime, Arena);
            var boss = run.SpawnEnemy(run.Hero.Pos + new GridPos(1, 0), ActorCatalog.KingSlime);
            boss.SpecialCooldown = 0;
            boss.Alerted = true;
            return (run, boss);
        }

        [Test]
        public void TheLastFloorIsTheBossArena()
        {
            var run = new DungeonRun(11, new DungeonRunConfig { FloorCount = 1 });

            Assert.IsTrue(run.IsBossFloor);
            Assert.IsFalse(run.Map.InBounds(run.Map.Stairs), "no stairs: the boss is the way out");
            Assert.IsNotNull(run.Boss);
            Assert.AreEqual(1, run.Map.RoomIndexAt(run.Boss.Pos), "the boss waits in the arena");
            Assert.AreEqual(0, run.Map.RoomIndexAt(run.Hero.Pos), "the hero arrives in the antechamber");
            Assert.AreEqual(ActorCatalog.KingSlime.MaxHp, run.Boss.MaxHp, "bosses don't get the per-floor bonus");
            Assert.AreEqual(run.Config.BossFloorBerries, run.Items.Count);
        }

        [Test]
        public void WithoutABossTheLastFloorHasStairs()
        {
            var run = new DungeonRun(11, new DungeonRunConfig { FloorCount = 1, Boss = null });
            Assert.IsFalse(run.IsBossFloor);
            Assert.IsTrue(run.Map.InBounds(run.Map.Stairs));
            Assert.IsNull(run.Boss);
        }

        [Test]
        public void DefeatingTheBossClearsTheDungeon()
        {
            var (run, boss) = BossNextToHero();
            boss.Hp = 1;

            run.AttackAt(boss.Pos);

            Assert.AreEqual(RunState.Won, run.State);
            Assert.IsTrue(run.Events.OfType<ExpGainedEvent>().Any(), "the boss gives EXP");
            Assert.IsTrue(run.Events.Last() is RunEndedEvent ended && ended.Won);
        }

        [Test]
        public void TheSlamIsTelegraphedThenHitsEverythingAdjacent()
        {
            var (run, boss) = BossNextToHero();

            run.Wait();
            Assert.IsTrue(boss.Charging);
            Assert.IsTrue(run.Events.OfType<BossActionEvent>().Any(e => e.Action == BossAction.Charge));

            int hp = run.Hero.Hp;
            run.Wait();
            Assert.IsTrue(run.Events.OfType<BossActionEvent>().Any(e => e.Action == BossAction.Slam));
            Assert.Less(run.Hero.Hp, hp);
            Assert.IsFalse(boss.Charging);
            Assert.AreEqual(EnemyBrain.SlamCooldown, boss.SpecialCooldown);
        }

        [Test]
        public void SteppingAwayDuringTheWindUpDodgesTheSlam()
        {
            var (run, boss) = BossNextToHero();
            run.Wait(); // The boss winds up.
            int hp = run.Hero.Hp;

            run.Move(Direction8.W); // Two tiles from the boss: out of reach.

            Assert.IsTrue(run.Events.OfType<BossActionEvent>().Any(e => e.Action == BossAction.Slam), "the slam still happens");
            Assert.IsFalse(run.Events.OfType<DamageEvent>().Any(), "but it hits nothing");
            Assert.AreEqual(hp, run.Hero.Hp);
        }

        [Test]
        public void AtHalfHpTheBossCallsForHelpOnce()
        {
            var (run, boss) = BossNextToHero();
            boss.SpecialCooldown = 5; // Keep the slam out of the way.
            boss.Hp = boss.MaxHp / 2;

            run.Wait();
            Assert.IsTrue(boss.CalledForHelp);
            Assert.IsTrue(run.Events.OfType<BossActionEvent>().Any(e => e.Action == BossAction.Summon));
            Assert.AreEqual(EnemyBrain.HelpersSummoned, run.Events.OfType<ActorSpawnedEvent>().Count());
            Assert.AreEqual(1 + 1 + EnemyBrain.HelpersSummoned, run.Actors.Count);

            run.Wait();
            Assert.IsFalse(run.Events.OfType<BossActionEvent>().Any(e => e.Action == BossAction.Summon), "only once");
        }

        [Test]
        public void BossFloorsAreValidAcrossManySeeds()
        {
            var config = new DungeonGenConfig();
            for (int seed = 0; seed < 100; seed++)
            {
                var map = DungeonGenerator.GenerateBossFloor(seed, config);
                Assert.AreEqual(2, map.Rooms.Count);
                Assert.IsTrue(map.IsWalkable(map.Start));
                Assert.IsTrue(map.IsWalkable(map.Rooms[1].Center), $"seed {seed}: the boss's spot is open");
                Assert.IsTrue(Pathfinder.TryFirstStep(map, map.Start, map.Rooms[1].Center, null, 200, out _), $"seed {seed}: arena reachable");
            }
        }
    }
}

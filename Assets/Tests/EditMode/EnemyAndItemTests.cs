using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    public class EnemyAndItemTests
    {
        [Test]
        public void AdjacentEnemyBitesTheHero()
        {
            var run = TestRuns.OnMap(
                "######",
                "#@...#",
                "######");
            run.SpawnEnemy(new GridPos(2, 1));
            int hp = run.Hero.Hp;
            run.Wait();
            Assert.Less(run.Hero.Hp, hp);
        }

        [Test]
        public void AlertedEnemyClosesTheDistance()
        {
            var run = TestRuns.OnMap(
                "#########",
                "#@......#",
                "#########");
            var spider = run.SpawnEnemy(new GridPos(6, 1));
            run.Wait();
            Assert.AreEqual(new GridPos(5, 1), spider.Pos);
            Assert.IsTrue(spider.Alerted);
        }

        [Test]
        public void WalkingOverABerryPicksItUp()
        {
            var run = TestRuns.OnMap(
                "#####",
                "#@..#",
                "#####");
            run.PlaceItem(new GridPos(2, 1), ItemKind.Berry);
            run.Move(Direction8.E);
            Assert.AreEqual(1, run.Berries);
            Assert.AreEqual(0, run.Items.Count);
            Assert.IsTrue(run.Events.OfType<ItemPickedUpEvent>().Any());
        }

        [Test]
        public void BerryHealsUpToMaxAndUsesATurn()
        {
            var run = TestRuns.OnMap(
                "#####",
                "#@..#",
                "#####");
            run.Berries = 2;
            run.Hero.Hp = run.Hero.MaxHp - 5;

            Assert.IsTrue(run.UseBerry());
            Assert.AreEqual(run.Hero.MaxHp, run.Hero.Hp);
            Assert.AreEqual(1, run.Berries);
            Assert.IsInstanceOf<ItemUsedEvent>(run.Events[0]);
            Assert.AreEqual(5, run.Events.OfType<HealedEvent>().Single().Amount);
            Assert.AreEqual(1, run.Turn);
        }

        [Test]
        public void BerryIsRefusedAtFullHpOrWhenOut()
        {
            var run = TestRuns.OnMap(
                "#####",
                "#@..#",
                "#####");
            run.Berries = 1;
            Assert.IsFalse(run.UseBerry(), "full HP: the berry would do nothing");
            Assert.AreEqual(1, run.Berries);

            run.Berries = 0;
            run.Hero.Hp = 1;
            Assert.IsFalse(run.UseBerry(), "no berries");
            Assert.AreEqual(0, run.Turn);
        }

        [Test]
        public void HowMuchABerryHealsIsUpToTheDungeon()
        {
            var config = new DungeonRunConfig
            {
                MapFactory = (floor, seed) => DungeonMap.FromAscii("#####", "#@..#", "#####"),
                Populate = false,
                Boss = null,
                BerryHealHp = 30,
            };
            var run = new DungeonRun(1, config) { Berries = 1 };
            run.Hero.Hp = 100;

            Assert.IsTrue(run.UseBerry());
            Assert.AreEqual(130, run.Hero.Hp);
        }
    }
}

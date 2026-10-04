using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    public class ProgressionTests
    {
        static readonly string[] Corridor =
        {
            "########",
            "#@.....#",
            "########",
        };

        [Test]
        public void HeroEntersAtTheirSavedLevelWithMatchingStats()
        {
            var progress = new HeroProgress(TestHeroes.Classic, level: 4, exp: 7);
            var run = TestRuns.OnMap(1, progress, null, Corridor);
            var hero = run.Hero;
            var basis = TestHeroes.Classic;

            Assert.AreEqual(4, hero.Level);
            Assert.AreEqual(7, hero.Exp);
            Assert.AreEqual(basis.MaxHp + 3 * basis.HpGrowth, hero.MaxHp);
            Assert.AreEqual(hero.MaxHp, hero.Hp, "a run starts at full HP");
            Assert.AreEqual(basis.Attack + 3 * basis.AtkGrowth, hero.Attack);
            Assert.AreEqual(basis.Defense + 3 * basis.DefGrowth, hero.Defense);
        }

        [Test]
        public void ExpEarnedInARunIsRecordedInTheProgress()
        {
            var progress = new HeroProgress(ActorCatalog.Uzuki);
            var run = TestRuns.OnMap(1, progress, null, Corridor);
            var spider = run.SpawnEnemy(new GridPos(2, 1));
            spider.Hp = 1;
            spider.ExpReward = 30;

            run.AttackAt(spider.Pos);

            Assert.AreEqual(run.Hero.Level, progress.Level);
            Assert.AreEqual(run.Hero.Exp, progress.Exp);
            Assert.Greater(progress.Level, 1);
        }

        [Test]
        public void ProgressIsKeptAfterADefeat()
        {
            var progress = new HeroProgress(ActorCatalog.Uzuki);
            var run = TestRuns.OnMap(1, progress, null, Corridor);
            var weakling = run.SpawnEnemy(new GridPos(2, 1));
            weakling.Hp = 1;
            weakling.ExpReward = 5;
            run.AttackAt(weakling.Pos); // Defeat it: 5 EXP.

            var brute = run.SpawnEnemy(new GridPos(2, 1));
            brute.Attack = 999;
            run.Wait();

            Assert.AreEqual(RunState.Lost, run.State);
            Assert.AreEqual(5, progress.Exp);
        }

        [Test]
        public void TheNextRunStartsWhereTheLastOneLeftOff()
        {
            var progress = new HeroProgress(ActorCatalog.Uzuki);
            var first = TestRuns.OnMap(1, progress, null, Corridor);
            var spider = first.SpawnEnemy(new GridPos(2, 1));
            spider.Hp = 1;
            spider.ExpReward = 100;
            first.AttackAt(spider.Pos);
            int level = first.Hero.Level;

            var second = TestRuns.OnMap(1, progress, null, Corridor);

            Assert.AreEqual(level, second.Hero.Level);
            Assert.AreEqual(first.Hero.MaxHp, second.Hero.MaxHp);
            Assert.AreEqual(second.Hero.MaxHp, second.Hero.Hp);
        }

        [Test]
        public void CatalogFindsDefinitionsById()
        {
            Assert.AreSame(ActorCatalog.Uzuki, ActorCatalog.Find("uzuki"));
            Assert.AreSame(ActorCatalog.Troll, ActorCatalog.Find("troll"));
            Assert.IsNull(ActorCatalog.Find("nobody"));
            Assert.IsTrue(new[] { ActorCatalog.Uzuki, ActorCatalog.Spider }.All(d => !d.IsBoss));
        }
    }
}

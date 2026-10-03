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
            var progress = new HeroProgress(ActorCatalog.Uzuki, level: 4, exp: 7);
            var run = TestRuns.OnMap(1, progress, null, Corridor);
            var hero = run.Hero;
            var basis = ActorCatalog.Uzuki;

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
            var slime = run.SpawnEnemy(new GridPos(2, 1));
            slime.Hp = 1;
            slime.ExpReward = 30;

            run.Move(Direction8.E);

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
            run.Move(Direction8.E); // Defeat it: 5 EXP.

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
            var slime = first.SpawnEnemy(new GridPos(2, 1));
            slime.Hp = 1;
            slime.ExpReward = 100;
            first.Move(Direction8.E);
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
            Assert.AreSame(ActorCatalog.KingSlime, ActorCatalog.Find("king_slime"));
            Assert.IsNull(ActorCatalog.Find("nobody"));
            Assert.IsTrue(new[] { ActorCatalog.Uzuki, ActorCatalog.Slime }.All(d => !d.IsBoss));
        }
    }
}

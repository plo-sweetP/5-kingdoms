using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    public class CombatTests
    {
        static DungeonRun Corridor() => TestRuns.OnMap(
            "########",
            "#@.....#",
            "########");

        [Test]
        public void BumpingAnEnemyAttacksItInsteadOfMoving()
        {
            var run = Corridor();
            var slime = run.SpawnEnemy(new GridPos(2, 1));
            slime.MaxHp = slime.Hp = 100;

            Assert.IsTrue(run.Move(Direction8.E));
            Assert.AreEqual(new GridPos(1, 1), run.Hero.Pos);
            Assert.Less(slime.Hp, 100);
            var attack = run.Events.OfType<AttackEvent>().First();
            Assert.AreEqual(run.Hero.Id, attack.AttackerId);
            Assert.AreEqual(slime.Id, attack.TargetId);
        }

        [Test]
        public void AttackingThinAirStillUsesTheTurn()
        {
            var run = Corridor();
            Assert.IsTrue(run.Attack());
            Assert.AreEqual(1, run.Turn);
            Assert.AreEqual(-1, run.Events.OfType<AttackEvent>().First().TargetId);
        }

        [Test]
        public void DamageIsAlwaysAtLeastOne()
        {
            var rng = new Rng(3);
            var weak = new Actor(1, new ActorDefinition("a", "Weak", 10, 1, 0, 0), Team.Hero, default);
            var tank = new Actor(2, new ActorDefinition("b", "Tank", 10, 1, 999, 0), Team.Enemy, default);
            for (int i = 0; i < 200; i++) Assert.GreaterOrEqual(CombatRules.RollBasicAttack(weak, tank, rng).Amount, 1);
        }

        [Test]
        public void DefeatingAnEnemyGivesExpAndCanLevelUp()
        {
            var run = Corridor();
            var slime = run.SpawnEnemy(new GridPos(2, 1));
            slime.Hp = 1;
            slime.ExpReward = 100;
            int attackBefore = run.Hero.Attack;

            run.Move(Direction8.E);

            Assert.IsNull(run.FindActor(slime.Id));
            Assert.IsTrue(run.Events.OfType<DiedEvent>().Any(e => e.ActorId == slime.Id));
            Assert.Greater(run.Hero.Level, 1);
            Assert.AreEqual(run.Hero.Level - 1, run.Events.OfType<LevelUpEvent>().Count());
            Assert.AreEqual(attackBefore + (run.Hero.Level - 1) * CombatRules.LevelUpAttack, run.Hero.Attack);
        }

        [Test]
        public void AttacksCannotReachAroundAWallCorner()
        {
            var run = TestRuns.OnMap(
                "####",
                "##.#",
                "#@##",
                "####");
            var slime = run.SpawnEnemy(new GridPos(2, 2));
            int heroHp = run.Hero.Hp;

            run.Move(Direction8.NE); // Corner is walled: no attack, just a turn in place.
            Assert.AreEqual(slime.MaxHp, slime.Hp);
            for (int i = 0; i < 5; i++) run.Wait();
            Assert.AreEqual(heroHp, run.Hero.Hp, "the slime can't bite around the corner either");
        }

        [Test]
        public void HeroDefeatEndsTheRun()
        {
            var run = Corridor();
            var slime = run.SpawnEnemy(new GridPos(2, 1));
            slime.Attack = 999;
            run.Hero.Hp = 1;

            run.Wait();

            Assert.AreEqual(RunState.Lost, run.State);
            Assert.IsTrue(run.Events.OfType<RunEndedEvent>().Single().Won == false);
        }
    }
}

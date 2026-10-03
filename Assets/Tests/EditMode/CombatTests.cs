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
        public void WalkingIntoAnEnemyOnlyTurnsToFaceIt()
        {
            var run = Corridor();
            var slime = run.SpawnEnemy(new GridPos(2, 1));
            slime.MaxHp = slime.Hp = 100;
            run.Hero.Facing = Direction8.S;

            Assert.IsFalse(run.Move(Direction8.E), "like bumping a wall: no turn used");
            Assert.AreEqual(0, run.Turn);
            Assert.AreEqual(new GridPos(1, 1), run.Hero.Pos);
            Assert.AreEqual(Direction8.E, run.Hero.Facing);
            Assert.AreEqual(100, slime.Hp, "attacks are deliberate");
            Assert.IsInstanceOf<FacingChangedEvent>(run.Events.Single());
        }

        [Test]
        public void TheWeaponAttackHitsTheFoeOnTheTileItIsAimedAt()
        {
            var run = Corridor();
            var slime = run.SpawnEnemy(new GridPos(2, 1));
            slime.MaxHp = slime.Hp = 100;
            run.Hero.Facing = Direction8.S;

            Assert.IsTrue(run.Execute(HeroCommand.AttackAt(slime.Pos)));
            Assert.AreEqual(1, run.Turn);
            Assert.Less(slime.Hp, 100);
            Assert.AreEqual(Direction8.E, run.Hero.Facing, "turns to face its target");
            var attack = run.Events.OfType<AttackEvent>().First();
            Assert.AreEqual(run.Hero.Id, attack.AttackerId);
            Assert.AreEqual(slime.Id, attack.TargetId);
            Assert.AreEqual(slime.Pos, attack.To);
        }

        [Test]
        public void AnAttackAtATileWithNoFoeInReachIsRefused()
        {
            var run = Corridor();
            var far = run.SpawnEnemy(new GridPos(3, 1));
            Assert.IsFalse(run.AttackAt(new GridPos(2, 1)), "nobody there");
            Assert.IsFalse(run.AttackAt(far.Pos), "two tiles away: out of a melee hero's reach");
            Assert.IsNull(run.AttackTargetAt(run.Hero, far.Pos));
            Assert.AreEqual(0, run.Turn, "refused: no turn used");
            Assert.AreEqual(far.MaxHp, far.Hp);
        }

        [Test]
        public void AttackingThinAirStillUsesTheTurn()
        {
            var run = Corridor();
            Assert.IsTrue(run.Attack());
            Assert.AreEqual(1, run.Turn);
            var swing = run.Events.OfType<AttackEvent>().First();
            Assert.AreEqual(-1, swing.TargetId);
            Assert.AreEqual(run.Hero.Pos + run.Hero.Facing.ToOffset(), swing.To);
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

            run.AttackAt(slime.Pos);

            Assert.IsNull(run.FindActor(slime.Id));
            Assert.IsTrue(run.Events.OfType<DiedEvent>().Any(e => e.ActorId == slime.Id));
            Assert.Greater(run.Hero.Level, 1);
            Assert.AreEqual(run.Hero.Level - 1, run.Events.OfType<LevelUpEvent>().Count());
            Assert.AreEqual(attackBefore + (run.Hero.Level - 1) * run.Hero.Definition.AtkGrowth, run.Hero.Attack);
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

            Assert.IsFalse(run.AttackAt(slime.Pos), "the corner is walled: it isn't in reach");
            run.Attack(Direction8.NE); // A swing that way hits nothing.
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

using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    public class SkillTests
    {
        const int Strike = 0, Heal = 1, Dash = 2;

        static readonly string[] Hall =
        {
            "##########",
            "#........#",
            "#.@......#",
            "#........#",
            "##########",
        };

        static DungeonRun Run() => TestRuns.OnMap(1, new HeroProgress(ActorCatalog.Uzuki), null, Hall);

        static Actor SlimeAt(DungeonRun run, int dx, int dy, int hp = 100)
        {
            var slime = run.SpawnEnemy(run.Hero.Pos + new GridPos(dx, dy));
            slime.MaxHp = slime.Hp = hp;
            slime.Attack = 1;
            return slime;
        }

        [Test]
        public void UzukiHasThreeSkillsAndAFullManaBar()
        {
            var run = Run();
            CollectionAssert.AreEqual(new[] { "spirit_strike", "second_wind", "dash" }, run.Hero.Definition.Skills.Select(s => s.Id).ToArray());
            Assert.AreEqual(ActorCatalog.Uzuki.MaxMp, run.Hero.MaxMp);
            Assert.AreEqual(run.Hero.MaxMp, run.Hero.Mp);
        }

        [Test]
        public void SpiritStrikeHitsHarderAndBuildsMana()
        {
            var run = Run();
            var slime = SlimeAt(run, 1, 0);
            run.Hero.Mp = 0;

            Assert.IsTrue(run.UseSkill(Strike));
            Assert.Less(slime.Hp, 100);
            Assert.AreEqual(SkillCatalog.SpiritStrike.ManaGain, run.Hero.Mp);
            Assert.IsTrue(run.Events.OfType<SkillUsedEvent>().Any());
            Assert.AreEqual(slime.Id, run.Events.OfType<AttackEvent>().First().TargetId);
        }

        [Test]
        public void SpiritStrikeFindsAnAdjacentEnemyAndNeedsOne()
        {
            var run = Run();
            Assert.AreEqual(SkillCheck.NoTarget, run.CheckSkill(Strike));
            Assert.IsFalse(run.UseSkill(Strike));
            Assert.AreEqual(0, run.Turn);

            var slime = SlimeAt(run, 0, -1); // South of the hero, who looks the other way.
            run.Hero.Facing = Direction8.N;
            Assert.AreEqual(SkillCheck.Ready, run.CheckSkill(Strike));
            run.UseSkill(Strike);
            Assert.AreEqual(Direction8.S, run.Hero.Facing, "turns to face the enemy it hits");
            Assert.Less(slime.Hp, 100);
        }

        [Test]
        public void SpiritStrikeTakesLongerOnTheTimeline()
        {
            var run = Run();
            SlimeAt(run, 1, 0);
            run.Wait(); // The fight starts; the hero is due at 100 AV.
            Assert.IsTrue(run.InCombat);

            run.UseSkill(Strike); // 125% of a turn.
            Assert.AreEqual(AvTime.FromWhole(225), run.Forecast(1)[0].Time);
        }

        [Test]
        public void SecondWindHealsForMana()
        {
            var run = Run();
            run.Hero.Hp = 5;
            int mp = run.Hero.Mp;

            Assert.IsTrue(run.UseSkill(Heal));
            Assert.AreEqual(5 + run.Hero.MaxHp * SkillCatalog.SecondWind.Power / 100, run.Hero.Hp);
            Assert.AreEqual(mp - SkillCatalog.SecondWind.ManaCost, run.Hero.Mp);
        }

        [Test]
        public void SecondWindIsRefusedAtFullHpOrWithoutMana()
        {
            var run = Run();
            Assert.AreEqual(SkillCheck.NotNeeded, run.CheckSkill(Heal));
            run.Hero.Hp = 5;
            run.Hero.Mp = SkillCatalog.SecondWind.ManaCost - 1;
            Assert.AreEqual(SkillCheck.NotEnoughMana, run.CheckSkill(Heal));
            Assert.IsFalse(run.UseSkill(Heal));
            Assert.AreEqual(0, run.Turn);
        }

        [Test]
        public void DashMovesUpToThreeTilesStraightAhead()
        {
            var run = Run(); // Hero at (2, 2) in a 10-wide hall.
            run.Hero.Facing = Direction8.E;
            Assert.IsTrue(run.UseSkill(Dash));
            Assert.AreEqual(new GridPos(5, 2), run.Hero.Pos);
            var dashed = run.Events.OfType<DashedEvent>().Single();
            Assert.AreEqual(new GridPos(2, 2), dashed.From);
            Assert.AreEqual(new GridPos(5, 2), dashed.To);
        }

        [Test]
        public void DashStopsAtAWall()
        {
            var run = Run();
            run.Hero.Facing = Direction8.W; // The wall is two tiles west.
            Assert.IsTrue(run.UseSkill(Dash));
            Assert.AreEqual(new GridPos(1, 2), run.Hero.Pos);
        }

        [Test]
        public void DashStopsInFrontOfAnActor()
        {
            var run = Run();
            run.Hero.Facing = Direction8.E;
            SlimeAt(run, 2, 0);
            Assert.IsTrue(run.UseSkill(Dash));
            Assert.AreEqual(new GridPos(3, 2), run.Hero.Pos);
        }

        [Test]
        public void DashIsBlockedFacingAWallAndHasACooldown()
        {
            var run = Run();
            run.Hero.Facing = Direction8.W;
            run.Move(Direction8.W); // Now at (1, 2), facing the west wall.
            Assert.AreEqual(SkillCheck.Blocked, run.CheckSkill(Dash));

            run.Hero.Facing = Direction8.E;
            Assert.IsTrue(run.UseSkill(Dash));
            for (int turn = 0; turn < SkillCatalog.Dash.Cooldown; turn++)
            {
                Assert.AreEqual(SkillCheck.OnCooldown, run.CheckSkill(Dash), $"still cooling down after {turn} turns");
                run.Wait();
            }
            Assert.AreEqual(SkillCheck.Ready, run.CheckSkill(Dash));
        }

        [Test]
        public void AnAimedDashTurnsTheHeroFirst()
        {
            var run = Run();
            run.Hero.Pos = new GridPos(1, 2); // Against the west wall, facing south.
            Assert.AreEqual(SkillCheck.Blocked, run.CheckSkill(Dash, Direction8.W));
            Assert.IsFalse(run.Execute(HeroCommand.Skill(Dash, Direction8.W)));
            Assert.AreEqual(Direction8.S, run.Hero.Facing, "a refused skill doesn't turn the hero");

            Assert.IsTrue(run.Execute(HeroCommand.Skill(Dash, Direction8.E)));
            Assert.AreEqual(new GridPos(4, 2), run.Hero.Pos);
            Assert.AreEqual(Direction8.E, run.Hero.Facing);
        }

        [Test]
        public void AnAimedStrikeHitsTheEnemyItIsAimedAt()
        {
            var run = Run();
            var east = SlimeAt(run, 1, 0);
            var west = SlimeAt(run, -1, 0);
            Assert.IsTrue(run.Execute(HeroCommand.Skill(Strike, Direction8.W)));
            Assert.AreEqual(west.Id, run.Events.OfType<AttackEvent>().First().TargetId);
            Assert.AreEqual(100, east.Hp);
        }

        [Test]
        public void TheAutoPilotDashesDownALongWayToTheStairs()
        {
            var run = TestRuns.OnMap(2, new HeroProgress(ActorCatalog.Uzuki), null,
                "##########",
                "#@......>#",
                "##########");
            var command = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommandKind.Skill, command.Kind);
            Assert.AreEqual(Dash, command.Slot);
            Assert.AreEqual(Direction8.E, command.Direction);

            run.Execute(command);
            Assert.AreEqual(new GridPos(4, 1), run.Hero.Pos);

            run.Hero.SkillCooldowns[Dash] = 0; // Four tiles to go: three of them in one dash.
            run.Execute(AutoPilot.Decide(run));
            Assert.AreEqual(new GridPos(7, 1), run.Hero.Pos);

            run.Hero.SkillCooldowns[Dash] = 0;
            Assert.AreEqual(HeroCommandKind.Move, AutoPilot.Decide(run).Kind, "one tile to go: just walk");
        }

        [Test]
        public void DashIsQuickOnTheTimeline()
        {
            var run = Run();
            SlimeAt(run, 3, 0);
            run.Wait(); // The fight starts; the hero is due at 100 AV.
            run.Hero.Facing = Direction8.W;
            run.UseSkill(Dash); // 50% of a turn.
            Assert.AreEqual(AvTime.FromWhole(150), run.Forecast(1)[0].Time);
        }

        [Test]
        public void BasicAttacksBuildALittleMana()
        {
            var run = Run();
            SlimeAt(run, 1, 0);
            run.Hero.Mp = 0;
            run.Move(Direction8.E);
            Assert.AreEqual(CombatRules.BasicAttackManaGain, run.Hero.Mp);
        }

        [Test]
        public void LevelingUpGrowsMaxMana()
        {
            var run = TestRuns.OnMap(1, new HeroProgress(ActorCatalog.Uzuki, level: 5), null, Hall);
            Assert.AreEqual(ActorCatalog.Uzuki.MaxMp + 4 * CombatRules.LevelUpMp, run.Hero.MaxMp);
        }

        [Test]
        public void TheAutoPilotHealsWithTheSkillWhenLow()
        {
            var run = Run();
            run.Hero.Hp = 5;
            var command = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommandKind.Skill, command.Kind);
            Assert.AreEqual(Heal, command.Slot);
        }

        [Test]
        public void TheAutoPilotEatsABerryToPayForAHeal()
        {
            var run = Run();
            run.Hero.Hp = 5;
            run.Hero.Mp = 0;
            run.Berries = 1;
            Assert.AreEqual(HeroCommandKind.UseBerry, AutoPilot.Decide(run).Kind);
        }

        [Test]
        public void TheAutoPilotTopsUpManaBetweenFightsButNotDuringOne()
        {
            var run = Run();
            run.Hero.Mp = SkillCatalog.SecondWind.ManaCost - 1;
            run.Berries = 1;
            Assert.AreEqual(HeroCommandKind.UseBerry, AutoPilot.Decide(run).Kind, "no enemy around: refill for the next heal");

            SlimeAt(run, 1, 0);
            run.Wait(); // The slime notices the hero; the fight is on.
            Assert.IsTrue(run.InCombat);
            Assert.AreEqual(HeroCommandKind.Skill, AutoPilot.Decide(run).Kind, "healthy and next to an enemy: strike for mana instead");
        }
    }
}

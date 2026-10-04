using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    /// <summary>The party of 3 (milestone 1f): turns, following, swapping, switching leader, defeat and EXP.</summary>
    public class PartyTests
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
            "############",
            "#..@.......#",
            "############",
        };

        /// <summary>The three heroes with the archer in front, for tests that name who stands where (whatever the playtest's order is).</summary>
        static readonly ActorDefinition[] UzukiLeads = { ActorCatalog.Uzuki, ActorCatalog.Haiden, ActorCatalog.Kristela };

        public static DungeonRun Run(ActorDefinition[] members, params string[] rows)
        {
            var config = new DungeonRunConfig
            {
                MapFactory = (floor, seed) => DungeonMap.FromAscii(rows),
                Populate = false,
                RegenIntervalAv = 0,
                Boss = null,
            };
            return Run(config, members);
        }

        public static DungeonRun Run(DungeonRunConfig config, ActorDefinition[] members)
        {
            var heroes = members.Select(definition => new HeroProgress(definition)).ToArray();
            return new DungeonRun(7, config, heroes);
        }

        public static void Place(Actor actor, int x, int y) => actor.Pos = actor.PreviousPos = new GridPos(x, y);

        /// <summary>A spider that won't die or hurt anyone unless the test says so.</summary>
        public static Actor Dummy(DungeonRun run, int x, int y, int hp = 100000, int attack = 1)
        {
            var spider = run.SpawnEnemy(new GridPos(x, y));
            spider.MaxHp = spider.Hp = hp;
            spider.Attack = attack;
            return spider;
        }

        static void HoldAll(DungeonRun run)
        {
            foreach (var member in run.Party) run.SetTactic(member, PartyTactic.Hold);
        }

        [Test]
        public void TheStartingPartyStandsTogetherWithAMeleeHeroInTheLead()
        {
            var run = Run(ActorCatalog.StartingParty, Room);
            CollectionAssert.AreEqual(new[] { "haiden", "kristela", "uzuki" }, run.Party.Select(member => member.Definition.Id).ToArray());
            Assert.AreSame(run.Party[0], run.Hero);
            Assert.IsFalse(run.Hero.Definition.IsRanged, "the first playtest starts with a melee leader (PROGRESSION.md)");
            foreach (var member in run.Party)
            {
                Assert.IsNotNull(run.FindActor(member.Id), $"{member.Name} is on the floor");
                Assert.LessOrEqual(GridPos.ChebyshevDistance(member.Pos, run.Hero.Pos), 1);
            }
        }

        [Test]
        public void EachHeroHasTheirSpeed()
        {
            var run = Run(UzukiLeads, Room);
            Assert.AreEqual(101, run.Party[0].Speed, "Uzuki 95 + the Hunter Bow's 6");
            Assert.AreEqual(90, run.Party[1].Speed, "Haiden");
            Assert.AreEqual(100, run.Party[2].Speed, "Kristela");
        }

        [Test]
        public void TheLeaderSwapsPlacesWithAPartner()
        {
            var run = Run(UzukiLeads, Corridor);
            var leader = run.Hero;
            var haiden = run.Party[1];
            var kristela = run.Party[2];
            Place(leader, 3, 1);
            Place(haiden, 2, 1);
            Place(kristela, 4, 1);

            Assert.IsTrue(run.Move(Direction8.E));
            Assert.AreEqual(new GridPos(4, 1), leader.Pos);
            Assert.IsTrue(run.Events.OfType<SwappedEvent>().Any(e => e.ActorId == leader.Id && e.OtherId == kristela.Id));
            // Haiden, next in line behind the leader, then gets past her the same way: the line stays in order.
            Assert.AreEqual(new GridPos(3, 1), haiden.Pos);
            Assert.AreEqual(new GridPos(2, 1), kristela.Pos);
        }

        [Test]
        public void PartnersFollowInALine()
        {
            var run = Run(ActorCatalog.StartingParty, Corridor);
            Place(run.Hero, 3, 1);
            Place(run.Party[1], 2, 1);
            Place(run.Party[2], 1, 1);

            for (int i = 0; i < 3; i++) Assert.IsTrue(run.Move(Direction8.E));
            Assert.AreEqual(new GridPos(6, 1), run.Hero.Pos);
            Assert.AreEqual(new GridPos(5, 1), run.Party[1].Pos);
            Assert.AreEqual(new GridPos(4, 1), run.Party[2].Pos);
        }

        [Test]
        public void AHoldingPartnerStaysPut()
        {
            var run = Run(ActorCatalog.StartingParty, Corridor);
            Place(run.Hero, 3, 1);
            Place(run.Party[1], 2, 1);
            Place(run.Party[2], 1, 1);
            run.SetTactic(run.Party[1], PartyTactic.Hold);

            for (int i = 0; i < 3; i++) run.Move(Direction8.E);
            Assert.AreEqual(new GridPos(2, 1), run.Party[1].Pos);
        }

        [Test]
        public void AnAttackingPartnerGoesAfterAFoeInSight()
        {
            var run = Run(UzukiLeads, Room);
            HoldAll(run);
            var kristela = run.Party[2];
            run.SetTactic(kristela, PartyTactic.Attack);
            Place(run.Hero, 1, 3);
            Place(run.Party[1], 1, 5);
            Place(kristela, 2, 1);
            var spiderStart = new GridPos(6, 1);
            Dummy(run, spiderStart.X, spiderStart.Y);

            run.Wait();
            Assert.AreEqual(3, GridPos.ChebyshevDistance(kristela.Pos, spiderStart), "one step toward the spider");
        }

        [Test]
        public void PartnersFightOnTheirOwnTurns()
        {
            var run = Run(UzukiLeads, Corridor);
            Place(run.Hero, 1, 1);
            Place(run.Party[1], 2, 1);
            Place(run.Party[2], 3, 1);
            var spider = Dummy(run, 4, 1);

            run.Wait();
            Assert.IsTrue(run.Events.OfType<AttackEvent>().Any(attack => attack.AttackerId == run.Party[2].Id && attack.TargetId == spider.Id),
                "Kristela hit the spider next to her");
        }

        [Test]
        public void EveryoneTakesTurnsOnTheTimelineAtTheirOwnSpeed()
        {
            var run = Run(UzukiLeads, Room);
            HoldAll(run);
            Place(run.Hero, 1, 3);
            Place(run.Party[1], 1, 4);
            Place(run.Party[2], 1, 2);
            Dummy(run, 5, 3);

            run.Wait(); // The spider notices the party: the fight starts.
            Assert.IsTrue(run.InCombat);
            var turns = run.Forecast(4);
            Assert.AreSame(run.Hero, turns[0].Actor, "Uzuki (101) is first");
            Assert.AreEqual(Timeline.TurnLength(101), turns[0].Time);
            Assert.AreSame(run.Party[2], turns[1].Actor, "Kristela (100) ties the spider and has the lower id");
            Assert.AreSame(run.Party[1], turns[3].Actor, "Haiden (90) is last");
            Assert.AreEqual(Timeline.TurnLength(90), turns[3].Time);
        }

        [Test]
        public void WhenTheLeaderFallsTheNextHeroTakesOver()
        {
            var run = Run(UzukiLeads, Corridor);
            HoldAll(run);
            Place(run.Hero, 4, 1);
            Place(run.Party[1], 1, 1);
            Place(run.Party[2], 2, 1);
            run.Hero.Hp = 1;
            Dummy(run, 5, 1, attack: 99999);
            var uzuki = run.Hero;

            run.Wait();
            Assert.IsFalse(uzuki.IsAlive);
            Assert.AreEqual(RunState.InProgress, run.State, "the others fight on");
            Assert.AreSame(run.Party[1], run.Hero, "Haiden is next in line");
            Assert.IsTrue(run.Events.OfType<LeaderChangedEvent>().Any(e => e.ActorId == run.Party[1].Id));
        }

        [Test]
        public void TheRunIsLostWhenTheWholePartyHasFallen()
        {
            var run = Run(new[] { TestHeroes.Classic, TestHeroes.Classic }, Corridor);
            HoldAll(run);
            Place(run.Hero, 4, 1);
            Place(run.Party[1], 3, 1);
            foreach (var member in run.Party) member.Hp = 1;
            Dummy(run, 5, 1, attack: 99999);

            run.Wait();
            Assert.AreEqual(RunState.InProgress, run.State, "one hero left");
            for (int i = 0; i < 10 && run.State == RunState.InProgress; i++) run.Wait();
            Assert.AreEqual(RunState.Lost, run.State);
        }

        [Test]
        public void ADungeonCanEndTheRunWhenTheLeaderFalls()
        {
            var config = new DungeonRunConfig
            {
                MapFactory = (floor, seed) => DungeonMap.FromAscii(Corridor),
                Populate = false,
                RegenIntervalAv = 0,
                Boss = null,
                DefeatWhenLeaderFalls = true,
            };
            var run = Run(config, ActorCatalog.StartingParty);
            HoldAll(run);
            Place(run.Hero, 4, 1);
            Place(run.Party[1], 1, 1);
            Place(run.Party[2], 2, 1);
            run.Hero.Hp = 1;
            Dummy(run, 5, 1, attack: 99999);

            run.Wait();
            Assert.AreEqual(RunState.Lost, run.State);
        }

        [Test]
        public void SwitchingLeaderWhileExploringTakesNoTime()
        {
            var run = Run(ActorCatalog.StartingParty, Room);
            Assert.IsFalse(run.Execute(HeroCommand.SwitchLeader(2)), "no turn used");
            Assert.AreSame(run.Party[2], run.Hero);
            Assert.AreEqual(0, run.Turn);
            Assert.IsTrue(run.Events.OfType<LeaderChangedEvent>().Any());
            Assert.IsFalse(run.Execute(HeroCommand.SwitchLeader(2)), "already leading");
        }

        [Test]
        public void SwitchingLeaderMidFightHandsTheTurnToTheNewLeader()
        {
            var run = Run(UzukiLeads, Room);
            HoldAll(run);
            Place(run.Hero, 1, 3);
            Place(run.Party[1], 1, 4);
            Place(run.Party[2], 1, 2);
            Dummy(run, 5, 3);
            run.Wait();
            Assert.IsTrue(run.InCombat);

            Assert.IsTrue(run.Execute(HeroCommand.SwitchLeader(1)), "the old leader's turn is played for it");
            Assert.AreSame(run.Party[1], run.Hero);
            Assert.AreSame(run.Party[1], run.Forecast(1)[0].Actor, "and now it's Haiden's turn");
        }

        [Test]
        public void EveryoneStandingGetsTheExp()
        {
            var run = Run(ActorCatalog.StartingParty, Corridor);
            Place(run.Hero, 1, 1);
            Place(run.Party[1], 4, 1);
            Place(run.Party[2], 5, 1);
            var spider = Dummy(run, 2, 1, hp: 1);
            spider.ExpReward = 5;

            run.AttackAt(spider.Pos);
            Assert.IsFalse(spider.IsAlive);
            foreach (var member in run.Party)
            {
                Assert.AreEqual(5, member.Exp, member.Name);
                Assert.AreEqual(5, run.ProgressOf(member).Exp, member.Name + "'s progress");
            }
        }

        [Test]
        public void TheWholePartyGoesDownTheStairs()
        {
            var run = Run(ActorCatalog.StartingParty, "#####", "#@.>#", "#...#", "#####");
            run.Hero.Pos = run.Map.Stairs;
            Assert.IsTrue(run.Descend());
            Assert.AreEqual(2, run.Floor);
            foreach (var member in run.Party) Assert.IsNotNull(run.FindActor(member.Id), member.Name);
        }

        [Test]
        public void PartnersPickUpBerriesTheyStepOn()
        {
            var run = Run(ActorCatalog.StartingParty, Corridor);
            Place(run.Hero, 3, 1);
            Place(run.Party[1], 2, 1);
            Place(run.Party[2], 1, 1);
            run.PlaceItem(new GridPos(3, 1), ItemKind.Berry); // Under the leader: the partner behind steps onto it next.
            run.Move(Direction8.E);
            Assert.AreEqual(1, run.Berries);
        }

        [Test]
        public void StatusesScaleDamageTaken()
        {
            var run = Run(ActorCatalog.StartingParty, Room);
            var target = run.Hero;
            Assert.AreEqual(100, run.DamageTakenPercent(target));
            target.Statuses.Add(new StatusEffect(StatusKind.Mark, 99, 20, 3, endsOnSourceTurn: false));
            Assert.AreEqual(120, run.DamageTakenPercent(target));
            target.Statuses.Add(new StatusEffect(StatusKind.Guard, 98, 35, 0, endsOnSourceTurn: true));
            Assert.AreEqual(85, run.DamageTakenPercent(target));
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;
using static FiveKingdoms.Tests.PartyTests;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// PROGRESSION.md, "Doorways and corridors": the hero in front holds a doorway against a crowd instead of stepping
    /// out among it, the hurt hero that holds the way trades places with the fresh melee hero behind it (and heals
    /// there), and with only a few foes close the party goes in, the hero in the corridor's mouth making way for the one
    /// behind. Partners do all of it for themselves, but never move the hero the player controls.
    /// </summary>
    public class DoorTacticsTests
    {
        /// <summary>A corridor from the west into a room: (4, 3) is the doorway, (5, 3) the room's first tile.</summary>
        static readonly string[] Doorway =
        {
            "#############",
            "#####.......#",
            "#####.......#",
            "#@..........#",
            "#####.......#",
            "#####......>#",
            "#############",
        };

        static readonly string[] Corridor =
        {
            "##########",
            "#@......>#",
            "##########",
        };

        static readonly string[] Room =
        {
            "##########",
            "#........#",
            "#........#",
            "#@......>#",
            "#........#",
            "#........#",
            "##########",
        };

        static readonly GridPos Door = new GridPos(4, 3);
        static readonly GridPos Mouth = new GridPos(5, 3);

        static void Root(Actor actor) => actor.Statuses.Add(new StatusEffect(StatusKind.Rooted, actor.Id, 0, 999, endsOnSourceTurn: false));

        /// <summary>The party in a line along row <paramref name="y"/>, the first hero at <paramref name="frontX"/> and the rest behind it to the west.</summary>
        static void LineUp(DungeonRun run, int frontX, int y, params Actor[] order)
        {
            for (int i = 0; i < order.Length; i++) Place(order[i], frontX - i, y);
        }

        /// <summary>Spiders that stay where they are, two tiles into the room from its first tile.</summary>
        static List<Actor> Crowd(DungeonRun run, int count)
        {
            var spots = new[] { new GridPos(7, 3), new GridPos(7, 4), new GridPos(7, 2), new GridPos(7, 5) };
            var crowd = new List<Actor>();
            for (int i = 0; i < count; i++)
            {
                var spider = Dummy(run, spots[i].X, spots[i].Y);
                Root(spider);
                crowd.Add(spider);
            }
            return crowd;
        }

        /// <summary>Everyone stands still for one turn while the foes notice them; then the partners fight as usual.</summary>
        static void StartFight(DungeonRun run)
        {
            foreach (var member in run.Party) run.SetTactic(member, PartyTactic.Hold);
            run.Wait();
            Assert.IsTrue(run.InCombat);
            foreach (var member in run.Party) run.SetTactic(member, PartyTactic.Attack);
        }

        static Actor Member(DungeonRun run, ActorDefinition definition) => run.Party.First(member => member.Definition == definition);

        // ---- The ground ----

        [Test]
        public void CorridorsAndDoorwaysAreNarrowAndRoomsAreNot()
        {
            var map = DungeonMap.FromAscii(Doorway);
            Assert.IsTrue(map.IsNarrow(new GridPos(2, 3)), "a corridor tile: one way in, one way out");
            Assert.IsTrue(map.IsNarrow(Door), "the doorway: the walls beside it cut the room's corners off");
            Assert.AreEqual(2, map.OpenNeighbors(Door));
            Assert.IsFalse(map.IsNarrow(Mouth), "the room's first tile");
            Assert.AreEqual(6, map.OpenNeighbors(Mouth), "the doorway and five tiles of the room");
            Assert.AreEqual(8, map.OpenNeighbors(new GridPos(8, 3)));
            Assert.IsFalse(map.IsNarrow(new GridPos(0, 0)), "a wall is nothing to stand on");
        }

        [Test]
        public void OnlyTheTileStraightAheadReachesAHeroInADoorway()
        {
            var run = Run(ActorCatalog.StartingParty, Doorway);
            LineUp(run, Door.X, Door.Y, run.Party[0], run.Party[1], run.Party[2]);
            var ahead = Dummy(run, 5, 3);
            var above = Dummy(run, 5, 4);
            var below = Dummy(run, 5, 2);

            Assert.AreEqual(IntentKind.Attack, EnemyBrain.Decide(run, ahead).Kind);
            Assert.AreNotEqual(IntentKind.Attack, EnemyBrain.Decide(run, above).Kind, "the wall's corner is between them");
            Assert.AreNotEqual(IntentKind.Attack, EnemyBrain.Decide(run, below).Kind);
            Assert.AreEqual(1, run.FoesInMeleeWith(run.Hero), "and he can strike only that one");
            Assert.IsTrue(run.InShotReach(run.Party[2].Pos, ahead.Pos, SkillCatalog.RangedReach), "the archer shoots past the two in front of him");
        }

        // ---- Hold the door ----

        [Test]
        public void TheLeaderHoldsTheDoorwayAgainstACrowd()
        {
            var run = Run(ActorCatalog.StartingParty, Doorway);
            LineUp(run, Door.X, Door.Y, run.Party[0], run.Party[1], run.Party[2]);
            Crowd(run, 3);

            Assert.AreEqual(HeroCommand.HoldTheDoor, AutoPilot.Decide(run), "three close beyond the doorway: he lets them come");
            Assert.IsTrue(run.Execute(AutoPilot.Decide(run)));
            Assert.AreEqual(Door, run.Hero.Pos);
            Assert.AreEqual(1, run.Hero.HeldTurns);
        }

        [Test]
        public void WithTwoFoesCloseThePartyGoesIn()
        {
            var run = Run(ActorCatalog.StartingParty, Doorway);
            LineUp(run, Door.X, Door.Y, run.Party[0], run.Party[1], run.Party[2]);
            Crowd(run, HeroTactics.SafeCrowd);

            Assert.AreEqual(HeroCommand.Move(Direction8.E), AutoPilot.Decide(run), "Kristela can get into that fight beside him");
        }

        [Test]
        public void FoesThatHaveNotSeenTheHeroAreNotClose()
        {
            var run = Run(ActorCatalog.StartingParty, Doorway);
            LineUp(run, Door.X, Door.Y, run.Party[0], run.Party[1], run.Party[2]);
            foreach (int y in new[] { 2, 3, 4 }) Root(Dummy(run, Door.X + run.Config.SightRange + 1, y));

            Assert.AreEqual(HeroCommand.Move(Direction8.E), AutoPilot.Decide(run), "six steps off: there's room to bring the others in");
        }

        [Test]
        public void AHeroWithNoMeleeAllyToBringInHoldsAgainstTwo()
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Uzuki }, Doorway);
            LineUp(run, Door.X, Door.Y, run.Party[0], run.Party[1]);
            var crowd = Crowd(run, 2);
            Assert.AreEqual(HeroCommand.HoldTheDoor, AutoPilot.Decide(run), "going in would only let both of them at him");

            crowd[1].Pos = new GridPos(11, 5); // The far corner: one close.
            Assert.AreEqual(HeroCommand.Move(Direction8.E), AutoPilot.Decide(run));
        }

        [Test]
        public void AHurtHeroHoldsAgainstTwoAsWell()
        {
            var run = Run(ActorCatalog.StartingParty, Doorway);
            LineUp(run, Door.X, Door.Y, run.Party[0], run.Party[1], run.Party[2]);
            var crowd = Crowd(run, 2);
            StartFight(run); // They are after him: before that he would heal up first.
            run.Hero.Hp = run.Hero.MaxHp * (HeroTactics.FitPercent - 10) / 100;
            Assert.AreEqual(HeroCommand.HoldTheDoor, AutoPilot.Decide(run), "not fit to stand between two: he lets them come");

            crowd[1].Pos = new GridPos(11, 5);
            Assert.AreEqual(HeroCommand.Move(Direction8.E), AutoPilot.Decide(run), "one he goes out to meet");
        }

        [Test]
        public void AHeroNobodyComesForGoesInAfterAWhile()
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela }, Doorway);
            LineUp(run, Door.X, Door.Y, run.Party[0], run.Party[1]);
            Crowd(run, 3); // Rooted: they never come.

            for (int turn = 0; turn < HeroTactics.DoorPatience; turn++)
            {
                Assert.AreEqual(HeroCommand.HoldTheDoor, AutoPilot.Decide(run), $"turn {turn}");
                run.Execute(AutoPilot.Decide(run));
            }
            Assert.AreEqual(HeroTactics.DoorPatience, run.Hero.HeldTurns);
            Assert.AreEqual(HeroCommand.Move(Direction8.E), AutoPilot.Decide(run), "they aren't coming: he goes in after all");
        }

        [Test]
        public void APartnerInFrontHoldsTheDoorToo()
        {
            var run = Run(new[] { ActorCatalog.Uzuki, ActorCatalog.Haiden, ActorCatalog.Kristela }, Doorway);
            var haiden = run.Party[1];
            LineUp(run, Door.X, Door.Y, haiden, run.Party[2], run.Hero);
            Crowd(run, 3);
            StartFight(run);

            Assert.AreEqual(HeroCommand.HoldTheDoor, PartnerBrain.Decide(run, haiden));
            run.Wait();
            Assert.AreEqual(Door, haiden.Pos, "whoever leads, the one in front holds");
        }

        [Test]
        public void AHeroWhoseAllyIsAlreadyOutThereFollowsIt()
        {
            var run = Run(ActorCatalog.StartingParty, Doorway);
            var kristela = run.Party[1];
            Place(run.Hero, 5, 4); // Haiden has stepped in and aside.
            Place(kristela, Door.X, Door.Y);
            Place(run.Party[2], 3, 3);
            Crowd(run, 3);
            StartFight(run);

            Assert.IsFalse(HeroTactics.HoldsTheDoor(run, kristela, Mouth), "she isn't the one in front");
            Assert.AreEqual(HeroCommand.Move(Direction8.E), PartnerBrain.Decide(run, kristela));
        }

        [Test]
        public void TheDoorIsNotHeldAgainstABoss()
        {
            var run = Run(ActorCatalog.StartingParty, Doorway);
            LineUp(run, Door.X, Door.Y, run.Party[0], run.Party[1], run.Party[2]);
            Crowd(run, 2);
            Root(run.SpawnEnemy(new GridPos(7, 5), ActorCatalog.Troll));

            Assert.AreEqual(HeroCommand.Move(Direction8.E), AutoPilot.Decide(run), "a slam is dodged in the open");
        }

        [Test]
        public void TheCrowdComesToTheDoorOneAtATime()
        {
            var run = Run(ActorCatalog.StartingParty, Doorway);
            var haiden = run.Hero;
            LineUp(run, Door.X, Door.Y, haiden, run.Party[1], run.Party[2]);
            foreach (var spot in new[] { new GridPos(7, 3), new GridPos(7, 4), new GridPos(7, 2), new GridPos(8, 3) }) Dummy(run, spot.X, spot.Y);

            bool archerShot = false;
            for (int action = 0; action < 10; action++)
            {
                Assert.IsTrue(run.Execute(AutoPilot.Decide(run)), $"action {action}");
                Assert.AreEqual(Door, haiden.Pos, $"he stays in the doorway (action {action})");
                Assert.LessOrEqual(run.FoesInMeleeWith(haiden), 1, $"one spider at a time (action {action})");
                Assert.IsFalse(run.Events.OfType<AttackEvent>().Any(attack => attack.TargetId == run.Party[1].Id || attack.TargetId == run.Party[2].Id),
                    $"nothing gets past him (action {action})");
                archerShot |= run.Events.OfType<AttackEvent>().Any(attack => attack.AttackerId == run.Party[2].Id && attack.TargetId >= 0);
            }
            Assert.IsTrue(archerShot, "Uzuki shoots past him");
        }

        // ---- Rotate the front ----

        /// <summary>A corridor fight: <paramref name="front"/> at (4, 1) with a spider in its face and at 40% HP, <paramref name="back"/> right behind it.</summary>
        static DungeonRun HurtFront(ActorDefinition[] members, ActorDefinition front, ActorDefinition back, out Actor frontHero, out Actor backHero)
        {
            var run = Run(members, Corridor);
            frontHero = Member(run, front);
            backHero = Member(run, back);
            Place(frontHero, 4, 1);
            Place(backHero, 3, 1);
            foreach (var member in run.Party)
                if (member != frontHero && member != backHero) Place(member, 2, 1);
            Root(Dummy(run, 5, 1));
            StartFight(run);
            frontHero.Hp = frontHero.MaxHp * 40 / 100;
            return run;
        }

        static readonly ActorDefinition[] UzukiLeads = { ActorCatalog.Uzuki, ActorCatalog.Haiden, ActorCatalog.Kristela };

        [Test]
        public void TheFreshMeleeHeroBehindTakesTheHurtOnesPlace()
        {
            var run = HurtFront(UzukiLeads, ActorCatalog.Haiden, ActorCatalog.Kristela, out var haiden, out var kristela);
            Assert.IsTrue(run.IsFrontRotation(haiden, kristela));
            Assert.IsTrue(run.CanSwap(kristela, haiden));
            Assert.AreEqual(HeroCommand.Move(Direction8.E), PartnerBrain.Decide(run, kristela), "she steps up; her turn was only waiting");

            run.Wait();
            Assert.AreEqual(new GridPos(4, 1), kristela.Pos, "the fresh one fights");
            Assert.AreEqual(new GridPos(3, 1), haiden.Pos);
            Assert.IsTrue(run.Events.OfType<SwappedEvent>().Any(swap => swap.ActorId == kristela.Id && swap.OtherId == haiden.Id));
            Assert.IsFalse(run.CanSwap(haiden, kristela), "and they can't trade back for a few turns");
            Assert.IsFalse(run.CanSwap(kristela, haiden));
        }

        [Test]
        public void TheHurtOneHealsBehind()
        {
            var run = HurtFront(UzukiLeads, ActorCatalog.Kristela, ActorCatalog.Haiden, out var kristela, out var haiden);
            run.Wait(); // Haiden takes the front.
            Assert.AreEqual(new GridPos(4, 1), haiden.Pos);
            kristela.Hp = kristela.MaxHp * 70 / 100;
            kristela.SkillCooldowns[1] = 0;

            Assert.IsTrue(HeroTactics.IsBehindTheFront(run, kristela));
            var command = PartnerBrain.Decide(run, kristela);
            Assert.AreEqual(HeroCommandKind.Skill, command.Kind, "at 70% she wouldn't heal in the open yet");
            Assert.AreSame(SkillCatalog.KiHeal, kristela.Skills[command.Slot]);

            kristela.Hp = kristela.MaxHp * 80 / 100;
            Assert.AreEqual(HeroCommand.Wait, PartnerBrain.Decide(run, kristela), "a quarter of her HP would be half wasted now");
        }

        [Test]
        public void APartnerNeverTakesTheFrontFromTheLeader()
        {
            var run = HurtFront(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela }, ActorCatalog.Haiden, ActorCatalog.Kristela, out var haiden, out var kristela);
            Assert.AreSame(haiden, run.Hero);
            Assert.IsTrue(run.IsFrontRotation(haiden, kristela), "he is hurt and she is fresh");
            Assert.IsFalse(run.CanSwap(kristela, haiden), "but the hero the player controls only moves when the player says so");
            Assert.AreEqual(HeroCommand.Wait, PartnerBrain.Decide(run, kristela));

            run.Wait();
            Assert.AreEqual(new GridPos(4, 1), haiden.Pos, "still in front");
        }

        [Test]
        public void TheAutoPilotsLeaderStepsBackBehindTheFreshOne()
        {
            var run = HurtFront(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela }, ActorCatalog.Haiden, ActorCatalog.Kristela, out var haiden, out var kristela);
            Assert.AreEqual(HeroCommand.Move(Direction8.W), AutoPilot.Decide(run), "before healing at the front: he heals behind her");

            run.Execute(AutoPilot.Decide(run));
            Assert.AreEqual(new GridPos(3, 1), haiden.Pos);
            Assert.AreEqual(new GridPos(4, 1), kristela.Pos);
        }

        [Test]
        public void TheAutoPilotsLeaderRelievesAHurtPartner()
        {
            var run = HurtFront(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela }, ActorCatalog.Kristela, ActorCatalog.Haiden, out var kristela, out var haiden);
            Assert.AreSame(haiden, run.Hero);
            Assert.AreEqual(HeroCommand.Move(Direction8.E), AutoPilot.Decide(run));

            run.Execute(AutoPilot.Decide(run));
            Assert.AreEqual(new GridPos(4, 1), haiden.Pos);
            Assert.AreEqual(new GridPos(3, 1), kristela.Pos);
        }

        [Test]
        public void TheFrontRotatesOnlyForAHurtHeroAndAClearlyFresherMeleeOne()
        {
            var run = HurtFront(ActorCatalog.StartingParty, ActorCatalog.Haiden, ActorCatalog.Kristela, out var haiden, out var kristela);
            var uzuki = run.Party[2];
            Assert.IsTrue(run.IsFrontRotation(haiden, kristela));

            haiden.Hp = haiden.MaxHp * DungeonRun.RotateOutPercent / 100;
            Assert.IsFalse(run.IsFrontRotation(haiden, kristela), "at half his HP he still holds");
            haiden.Hp = haiden.MaxHp * 40 / 100;
            kristela.Hp = kristela.MaxHp * 55 / 100;
            Assert.IsFalse(run.IsFrontRotation(haiden, kristela), "she is hardly fresher");
            kristela.Hp = kristela.MaxHp * 60 / 100;
            Assert.IsTrue(run.IsFrontRotation(haiden, kristela), "20 points more of her HP left");

            Place(uzuki, 3, 1);
            Place(kristela, 2, 1);
            Assert.IsFalse(run.IsFrontRotation(haiden, uzuki), "an archer doesn't take the front");
        }

        [Test]
        public void InTheOpenNobodyTradesPlaces()
        {
            var run = Run(new[] { ActorCatalog.Uzuki, ActorCatalog.Haiden, ActorCatalog.Kristela }, Room);
            var haiden = run.Party[1];
            var kristela = run.Party[2];
            Place(run.Hero, 1, 1);
            Place(haiden, 4, 3);
            Place(kristela, 3, 3);
            Root(Dummy(run, 5, 3));
            haiden.Hp = haiden.MaxHp * 40 / 100;

            Assert.IsFalse(run.IsFrontRotation(haiden, kristela), "she can walk around him and fight beside him");
        }

        [Test]
        public void AHurtHeroInTheMouthOfACorridorIsRelievedOnlyAgainstAFew()
        {
            var run = Run(new[] { ActorCatalog.Uzuki, ActorCatalog.Haiden, ActorCatalog.Kristela }, Doorway);
            var haiden = run.Party[1];
            var kristela = run.Party[2];
            Place(run.Hero, 2, 3);
            Place(haiden, Mouth.X, Mouth.Y);
            Place(kristela, Door.X, Door.Y);
            Root(Dummy(run, 6, 3));
            Root(Dummy(run, 6, 4));
            haiden.Hp = haiden.MaxHp * 40 / 100;
            Assert.IsTrue(run.IsFrontRotation(haiden, kristela), "two on him: she takes his place and he heals in the doorway");

            Root(Dummy(run, 6, 2));
            Assert.IsFalse(run.IsFrontRotation(haiden, kristela), "three: a fresh hero isn't fed to the crowd");
        }

        [Test]
        public void AHurtHeroAtTheDoorWaitsToBeRelieved()
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela }, Doorway);
            var haiden = run.Hero;
            var kristela = run.Party[1];
            Place(kristela, Door.X, Door.Y);
            Place(haiden, 3, 3);
            Crowd(run, 1);
            StartFight(run);
            kristela.Hp = kristela.MaxHp * 40 / 100;

            Assert.AreEqual(HeroCommand.HoldTheDoor, PartnerBrain.Decide(run, kristela), "one spider she would go in for, but not hurt, with Haiden behind her");
            Assert.AreEqual(HeroCommand.Move(Direction8.E), AutoPilot.Decide(run), "he takes the front before the fight reaches them");
        }

        // ---- Step in and aside ----

        /// <summary>Haiden fighting in the room's first tile, Kristela in the doorway behind him. (No archer: his Power Shot would knock the spiders about.)</summary>
        static DungeonRun TankInTheMouth(out Actor haiden, out Actor kristela)
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela }, Doorway);
            haiden = run.Hero;
            kristela = run.Party[1];
            LineUp(run, Mouth.X, Mouth.Y, haiden, kristela);
            return run;
        }

        [Test]
        public void AHeroFightingInTheMouthOfACorridorMakesWayForTheMeleeAllyBehind()
        {
            var run = TankInTheMouth(out var haiden, out var kristela);
            Root(Dummy(run, 6, 3));
            Root(Dummy(run, 6, 4));
            StartFight(run);
            Assert.IsFalse(run.InMelee(kristela), "she can't reach anything from the doorway");

            var command = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommandKind.Move, command.Kind, "one turn of his, for all of hers");
            var aside = haiden.Pos + command.Direction.ToOffset();
            Assert.AreEqual(Mouth.X, aside.X, "aside, along the wall");

            run.Execute(command);
            Assert.AreEqual(aside, haiden.Pos);
            Assert.IsTrue(run.InMelee(haiden), "he still fights from there");
            Assert.AreEqual(Mouth, kristela.Pos, "and she has come out");
            Assert.IsTrue(run.InMelee(kristela));
        }

        [Test]
        public void HeDoesNotMakeWayAgainstASingleFoe()
        {
            var run = TankInTheMouth(out _, out _);
            var spider = Dummy(run, 6, 3);
            Root(spider);
            StartFight(run);

            var command = AutoPilot.Decide(run);
            Assert.AreNotEqual(HeroCommandKind.Move, command.Kind, "it would fall as fast to him alone");
            Assert.IsTrue(command.Targeted);
            Assert.AreEqual(spider.Pos, command.Target);
        }

        [Test]
        public void HeDoesNotMakeWayWithNowhereElseToFightFrom()
        {
            var run = TankInTheMouth(out var haiden, out _);
            foreach (var spot in new[] { new GridPos(6, 3), new GridPos(6, 4), new GridPos(6, 2), new GridPos(5, 4), new GridPos(5, 2) })
                Root(Dummy(run, spot.X, spot.Y));
            StartFight(run);

            Assert.IsFalse(HeroTactics.TryMakeWay(run, haiden, out _));
        }
    }
}

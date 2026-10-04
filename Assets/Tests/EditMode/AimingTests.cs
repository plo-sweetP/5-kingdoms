using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;
using static FiveKingdoms.Tests.PartyTests;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// What the targeting highlight shows (PROGRESSION.md, "Targeting and input"): reach and valid targets. Shots reach
    /// anything in sight within range; walls and wall corners block them.
    /// </summary>
    public class AimingTests
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

        [Test]
        public void AShotTargetsEveryFoeInSightWithinReach()
        {
            var run = Run(new[] { ActorCatalog.Uzuki, ActorCatalog.Haiden }, Room);
            Place(run.Hero, 1, 3);
            Place(run.Party[1], 2, 3);
            var near = Dummy(run, 4, 3);
            var behind = Dummy(run, 5, 3); // Behind the first: other actors don't block a shot.
            var diagonal = Dummy(run, 3, 5);
            var offLine = Dummy(run, 4, 1); // Three over, two down: on none of the 8 lines.
            Dummy(run, 7, 3); // Six tiles away: out of reach.

            var aim = run.AimFor(run.Hero, SkillCatalog.PowerShot);
            Assert.IsTrue(aim.NeedsAim);
            CollectionAssert.AreEquivalent(new[] { near, behind, diagonal, offLine }, aim.Options.Select(option => option.Target).ToArray());
            Assert.IsTrue(aim.TryFind(near.Pos, out var east));
            Assert.AreEqual(Direction8.E, east.Direction);
            Assert.IsTrue(aim.TryFind(offLine.Pos, out var southEast));
            Assert.AreEqual(Direction8.SE, southEast.Direction, "the nearest of the 8 directions");
            Assert.Contains(new GridPos(2, 3), aim.Reach.ToList(), "allies don't block it");
            Assert.AreEqual(29, aim.Reach.Count, "every tile of the open room within 5 of her (6 x 5, less her own)");
            Assert.IsFalse(aim.Reach.Contains(new GridPos(7, 3)), "out of reach");
        }

        [Test]
        public void WallsBlockTheLineOfSight()
        {
            var map = DungeonMap.FromAscii(
                "#########",
                "#.......#",
                "#...#...#",
                "#@..#...#",
                "#...#...#",
                "#.......#",
                "#########");
            var from = new GridPos(1, 3);
            Assert.IsTrue(map.HasLineOfSight(from, new GridPos(3, 3)), "up to the wall");
            Assert.IsFalse(map.HasLineOfSight(from, new GridPos(5, 3)), "not through it");
            Assert.IsFalse(map.HasLineOfSight(from, new GridPos(6, 4)), "nor at a slant through it");
            Assert.IsTrue(map.HasLineOfSight(new GridPos(1, 5), new GridPos(6, 5)), "along the open row above it");
            Assert.IsTrue(map.HasLineOfSight(from, from));

            var run = Run(new[] { ActorCatalog.Uzuki }, "#########", "#.......#", "#...#...#", "#@..#...#", "#...#...#", "#.......#", "#########");
            var hidden = Dummy(run, 5, 3);
            Assert.IsNull(run.AttackTargetAt(run.Hero, hidden.Pos));
            Assert.AreEqual(0, run.AimFor(run.Hero, null).Options.Count, "a foe behind the wall isn't a target");
            Assert.IsFalse(run.AimFor(run.Hero, null).Reach.Contains(new GridPos(5, 3)));
        }

        [Test]
        public void ShotsCantCutAWallCorner()
        {
            // A doorway at (3, 2) between two rooms.
            var map = DungeonMap.FromAscii(
                "########",
                "#..#...#",
                "#@.....#",
                "#..#...#",
                "########");
            var door = new GridPos(3, 2);
            Assert.IsFalse(map.HasLineOfSight(door, new GridPos(4, 3)), "diagonally past the door frame");
            Assert.IsTrue(map.HasLineOfSight(door, new GridPos(5, 3)), "two over, one up: clear of the frame");
            Assert.IsFalse(map.HasLineOfSight(new GridPos(2, 1), new GridPos(4, 3)), "nor through the door at a diagonal");
            Assert.IsTrue(map.HasLineOfSight(new GridPos(1, 2), new GridPos(6, 2)), "straight through the door");
        }

        [Test]
        public void SightIsTheSameBothWaysAndMirrored()
        {
            var map = DungeonMap.FromAscii(
                "#########",
                "#.......#",
                "#.#...#.#",
                "#...#...#",
                "#.#...#.#",
                "#...@...#",
                "#########");
            for (int ax = 1; ax <= 7; ax++)
                for (int ay = 1; ay <= 5; ay++)
                    for (int bx = 1; bx <= 7; bx++)
                        for (int by = 1; by <= 5; by++)
                        {
                            var a = new GridPos(ax, ay);
                            var b = new GridPos(bx, by);
                            bool seen = map.HasLineOfSight(a, b);
                            Assert.AreEqual(seen, map.HasLineOfSight(b, a), $"{a} and {b}: whoever can shoot can be shot back");
                            // The map is its own mirror image left to right.
                            Assert.AreEqual(seen, map.HasLineOfSight(new GridPos(8 - ax, ay), new GridPos(8 - bx, by)), $"{a} and {b}, mirrored");
                        }
        }

        [Test]
        public void AShotAlongOneOfTheEightLinesIsTheOldStepByStepWalk()
        {
            // Sight along a row, column or diagonal is exactly "every step is a legal step" (the 1f part 1 rule).
            for (int seed = 1; seed <= 5; seed++)
            {
                var map = DungeonGenerator.Generate(seed, new DungeonGenConfig());
                foreach (var room in map.Rooms)
                {
                    var from = room.Center;
                    foreach (var dir in Directions.All)
                    {
                        bool open = true;
                        var pos = from;
                        for (int step = 1; step <= 5; step++)
                        {
                            open = open && map.CanStep(pos, dir);
                            pos += dir.ToOffset();
                            Assert.AreEqual(open, map.IsWalkable(pos) && map.HasLineOfSight(from, pos), $"seed {seed}, {from} toward {dir}, step {step}");
                        }
                    }
                }
            }
        }

        [Test]
        public void AStrikeTargetsTheFoesNextToTheUser()
        {
            var run = Run(new[] { ActorCatalog.Kristela }, Room);
            var east = Dummy(run, 2, 3);
            Dummy(run, 3, 3);
            var aim = run.AimFor(run.Hero, SkillCatalog.PiercingPunch);
            Assert.AreEqual(east, aim.Options.Single().Target);
            Assert.AreEqual(5, aim.Reach.Count, "the five open tiles around her, against the west wall");
        }

        [Test]
        public void TheWeaponAttackAimsLikeTheWeapon()
        {
            var run = Run(new[] { ActorCatalog.Uzuki }, Room);
            var far = Dummy(run, 6, 3);
            Assert.AreEqual(far, run.AimFor(run.Hero, null).Options.Single().Target, "Quick Shot reaches 5 tiles");

            var melee = Run(new[] { ActorCatalog.Haiden }, Room);
            Dummy(melee, 6, 3);
            Assert.AreEqual(0, melee.AimFor(melee.Hero, null).Options.Count, "Sword Slash only reaches the neighbors");
        }

        [Test]
        public void VolleyShowsItsArea()
        {
            var run = Run(new[] { ActorCatalog.Uzuki }, Room);
            Dummy(run, 4, 3);
            var aim = run.AimFor(run.Hero, SkillCatalog.Volley);
            Assert.AreEqual(1, aim.AreaRadius);
            Assert.AreEqual(1, aim.Options.Count);
        }

        [Test]
        public void RollingShotPicksWhereToRoll()
        {
            var run = Run(new[] { ActorCatalog.Uzuki }, Room);
            Place(run.Hero, 4, 3);
            var aim = run.AimFor(run.Hero, SkillCatalog.RollingShot);
            Assert.IsTrue(aim.PicksTile);
            Assert.AreEqual(8, aim.Options.Count, "two tiles every way in an open room");
            Assert.IsTrue(aim.TryFind(new GridPos(2, 3), out var west));
            Assert.AreEqual(Direction8.W, west.Direction);
            Assert.AreEqual(HeroCommand.Skill(2, Direction8.W), west.ToCommand(HeroCommandKind.Skill, 2), "a roll is aimed by direction");

            Dummy(run, 6, 3);
            aim = run.AimFor(run.Hero, SkillCatalog.RollingShot);
            int Safety(AimOption option) => run.DistanceToNearestFoe(option.Tile, run.Hero.Team);
            Assert.AreEqual(aim.Options.Max(Safety), Safety(aim.Options[aim.Default]), "marked to begin with: a spot farthest from the foe");
            Assert.AreEqual(4, Safety(aim.Options[aim.Default]));
        }

        [Test]
        public void ALoneTargetIsMarkedToBeginWith()
        {
            var run = Run(new[] { ActorCatalog.Kristela }, Room); // Kristela at (1, 3), facing south.
            Assert.AreEqual(-1, run.AimFor(run.Hero, null).Default, "nothing in reach: nothing to mark");

            var spider = Dummy(run, 2, 4);
            var aim = run.AimFor(run.Hero, null);
            Assert.AreSame(spider, aim.Options[aim.Default].Target, "one more tap confirms it");
            Assert.AreEqual(HeroCommand.AttackAt(spider.Pos), aim.Options[aim.Default].ToCommand(HeroCommandKind.Attack, 0));
            Assert.AreEqual(HeroCommand.SkillAt(2, spider.Pos), aim.Options[aim.Default].ToCommand(HeroCommandKind.Skill, 2));
            Assert.AreEqual(HeroCommand.UltimateAt(spider.Pos), aim.Options[aim.Default].ToCommand(HeroCommandKind.Ultimate, 0));
        }

        [Test]
        public void TheFoeTheHeroFacesIsMarkedFirstElseTheNearest()
        {
            var run = Run(new[] { ActorCatalog.Uzuki }, Room); // Uzuki at (1, 3).
            var northEast = Dummy(run, 3, 4); // Two over, one up: nearest to north-east.
            var east = Dummy(run, 5, 3);
            var south = Dummy(run, 1, 1);

            Actor Marked()
            {
                var aim = run.AimFor(run.Hero, null);
                return aim.Options[aim.Default].Target;
            }
            run.Hero.Facing = Direction8.E;
            Assert.AreSame(east, Marked(), "the one she faces, though others are nearer");
            run.Hero.Facing = Direction8.S;
            Assert.AreSame(south, Marked());
            run.Hero.Facing = Direction8.NE;
            Assert.AreSame(northEast, Marked(), "off the 8 lines: by the nearest direction");
            Place(south, 1, 2);
            run.Hero.Facing = Direction8.W;
            Assert.AreSame(south, Marked(), "nobody that way: the nearest");
        }

        [Test]
        public void ADirectionPressPicksTheNearestTargetThatWayThenTheNextOneOut()
        {
            var run = Run(new[] { ActorCatalog.Uzuki }, Room); // Uzuki at (1, 3).
            var northEast = Dummy(run, 3, 4);
            var east = Dummy(run, 5, 3);
            var farEast = Dummy(run, 6, 4); // Five over, one up: still nearest to east.
            var south = Dummy(run, 1, 1);
            var aim = run.AimFor(run.Hero, SkillCatalog.PowerShot);
            int Index(Actor foe) => aim.IndexAt(foe.Pos);

            Assert.AreEqual(Index(east), aim.Toward(Direction8.E, Index(south)));
            Assert.AreEqual(Index(farEast), aim.Toward(Direction8.E, Index(east)), "pressed again: the next one out");
            Assert.AreEqual(Index(east), aim.Toward(Direction8.E, Index(farEast)), "then around again");
            Assert.AreEqual(Index(northEast), aim.Toward(Direction8.N, Index(south)), "nobody north: the closest direction to it");
            Assert.AreEqual(Index(south), aim.Toward(Direction8.SW, Index(east)));

            Assert.AreEqual(Index(east), aim.NearestToward(Direction8.E), "hold a direction and press: fires at once");
            Assert.AreEqual(-1, aim.NearestToward(Direction8.N), "only at a target that really lies that way");
            Assert.AreEqual(-1, aim.IndexAt(new GridPos(8, 5)));
        }

        [Test]
        public void HealsAndAurasArentAimed()
        {
            var run = Run(new[] { ActorCatalog.Haiden }, Room);
            Assert.IsFalse(run.AimFor(run.Hero, SkillCatalog.PaladinHeal).NeedsAim);
            Assert.IsFalse(run.AimFor(run.Hero, SkillCatalog.AuraOfProtection).NeedsAim);
        }
    }
}

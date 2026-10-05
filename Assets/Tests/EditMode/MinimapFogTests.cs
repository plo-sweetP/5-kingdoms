using System.Collections.Generic;
using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    /// <summary>The minimap's fog (HUD.md, "Minimap"): what the party has explored of a floor, and what it sees right now.</summary>
    public class MinimapFogTests
    {
        static readonly RectI LeftRoom = new RectI(1, 1, 4, 3);
        static readonly RectI RightRoom = new RectI(11, 1, 4, 3);

        /// <summary>Two rooms of 4 x 3 joined by a corridor six tiles long; the hero starts in the left one, the way down is in the right one.</summary>
        static DungeonMap TwoRooms()
        {
            var map = DungeonMap.FromAscii(
                "################",
                "#....######....#",
                "#.@...........>#",
                "#....######....#",
                "################");
            map.Rooms.Clear(); // FromAscii counts the whole map as one room.
            map.Rooms.Add(LeftRoom);
            map.Rooms.Add(RightRoom);
            return map;
        }

        static DungeonRun SoloRun() => new DungeonRun(1,
            new DungeonRunConfig { FloorCount = 3, Populate = false, MapFactory = (floor, seed) => TwoRooms() },
            new[] { new HeroProgress(ActorCatalog.Haiden) });

        static GridPos Corridor(int x) => new GridPos(x, 2);

        static void AssertRoom(DungeonRun run, RectI room, bool explored, string why)
        {
            for (int y = room.Y; y <= room.YMax; y++)
                for (int x = room.X; x <= room.XMax; x++)
                    Assert.AreEqual(explored, run.IsExplored(new GridPos(x, y)), $"({x}, {y}): {why}");
        }

        static void Walk(DungeonRun run, Direction8 direction, int steps)
        {
            for (int i = 0; i < steps; i++) Assert.IsTrue(run.Execute(HeroCommand.Move(direction)), $"step {i + 1}");
        }

        [Test]
        public void AFloorStartsWithTheRoomThePartyStandsIn()
        {
            var run = SoloRun();
            Assert.AreEqual(new GridPos(2, 2), run.Hero.Pos);
            AssertRoom(run, LeftRoom, true, "the room a hero stands in shows as a whole");
            Assert.IsFalse(run.IsExplored(Corridor(5)), "the corridor shows only as the party walks it");
            AssertRoom(run, RightRoom, false, "nobody has been there");
            Assert.IsFalse(run.IsExplored(new GridPos(0, 0)), "walls are never explored");
            Assert.IsFalse(run.IsExplored(new GridPos(-1, 40)), "nor is anything off the map");
        }

        [Test]
        public void ACorridorAppearsAStretchAtATimeAndARoomAsAWholeFromItsDoorway()
        {
            var run = SoloRun();
            Walk(run, Direction8.E, 2); // To the room's edge: still inside it.
            Assert.IsFalse(run.IsExplored(Corridor(5)));

            Walk(run, Direction8.E, 1); // Onto the corridor's first tile.
            Assert.AreEqual(Corridor(5), run.Hero.Pos);
            Assert.IsTrue(run.IsExplored(Corridor(5)));
            Assert.IsTrue(run.IsExplored(Corridor(7)), "two steps ahead");
            Assert.IsFalse(run.IsExplored(Corridor(8)), "not three");

            Walk(run, Direction8.E, 4); // One tile short of the doorway: the room's first tile is two steps away.
            Assert.AreEqual(Corridor(9), run.Hero.Pos);
            Assert.IsTrue(run.IsExplored(Corridor(10)));
            AssertRoom(run, RightRoom, false, "a room never shows in part");

            Walk(run, Direction8.E, 1); // The doorway: one step from the room.
            AssertRoom(run, RightRoom, true, "from its doorway the room shows as a whole");
            Assert.IsTrue(run.IsExplored(run.Map.Stairs));

            AssertRoom(run, LeftRoom, true, "what was explored stays");
            for (int x = 5; x <= 10; x++) Assert.IsTrue(run.IsExplored(Corridor(x)), $"corridor tile {x} stays");
        }

        [Test]
        public void ANewFloorStartsEmpty()
        {
            var run = SoloRun();
            Walk(run, Direction8.E, 12);
            Assert.IsTrue(run.HeroOnStairs);
            int version = run.ExploredVersion;
            Assert.IsTrue(run.Execute(HeroCommand.Descend));
            Assert.AreEqual(2, run.Floor);
            Assert.Greater(run.ExploredVersion, version, "a view is told to redraw");
            AssertRoom(run, LeftRoom, true, "the new floor's first room");
            Assert.IsFalse(run.IsExplored(Corridor(7)), "the corridor is unknown again");
            AssertRoom(run, RightRoom, false, "and so is the far room");
        }

        [Test]
        public void ThePartySeesItsOwnRoomAndWhatIsInSightRange()
        {
            var run = SoloRun();
            Assert.AreEqual(5, run.Config.SightRange);
            Assert.IsTrue(run.PartySees(new GridPos(4, 3)), "the same room");
            Assert.IsTrue(run.PartySees(Corridor(7)), "five tiles down the corridor, nothing in the way");
            Assert.IsFalse(run.PartySees(Corridor(8)), "six tiles is out of sight");
            Assert.IsFalse(run.PartySees(new GridPos(13, 2)), "the far room");
            Assert.IsTrue(run.PartySees(run.Hero.Pos));
        }

        [Test]
        public void OnAGeneratedFloorExploredTilesStayAndEveryHerosSurroundingsAreExplored()
        {
            var run = new DungeonRun(7);
            var known = new HashSet<GridPos>();
            int floor = run.Floor;
            for (int action = 0; action < 250 && run.State == RunState.InProgress; action++)
            {
                run.Execute(AutoPilot.Decide(run));
                if (run.Floor != floor)
                {
                    floor = run.Floor;
                    known.Clear(); // A new floor starts empty.
                }
                foreach (var tile in known) Assert.IsTrue(run.IsExplored(tile), $"action {action}: {tile} was explored before");
                foreach (var hero in run.Party)
                {
                    if (!hero.IsAlive) continue;
                    Assert.IsTrue(run.IsExplored(hero.Pos), $"action {action}: {hero.Name} stands on an explored tile");
                    int room = run.Map.RoomIndexAt(hero.Pos);
                    if (room < 0) continue;
                    var rect = run.Map.Rooms[room];
                    for (int y = rect.Y; y <= rect.YMax; y++)
                        for (int x = rect.X; x <= rect.XMax; x++)
                            if (run.Map.IsWalkable(new GridPos(x, y)))
                                Assert.IsTrue(run.IsExplored(new GridPos(x, y)), $"action {action}: the whole room around {hero.Name}");
                }
                for (int y = 0; y < run.Map.Height; y++)
                    for (int x = 0; x < run.Map.Width; x++)
                    {
                        var tile = new GridPos(x, y);
                        if (!run.IsExplored(tile)) continue;
                        Assert.IsTrue(run.Map.IsWalkable(tile), $"only walkable tiles are explored: {tile}");
                        known.Add(tile);
                    }
            }
            Assert.Greater(known.Count, 0);
        }
    }
}

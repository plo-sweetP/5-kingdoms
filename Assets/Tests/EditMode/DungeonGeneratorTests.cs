using System.Collections.Generic;
using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    public class DungeonGeneratorTests
    {
        static readonly DungeonGenConfig Config = new DungeonGenConfig();

        [Test]
        public void SameSeedGivesSameFloor()
        {
            Assert.AreEqual(DungeonGenerator.Generate(1234, Config).ToAscii(), DungeonGenerator.Generate(1234, Config).ToAscii());
        }

        [Test]
        public void DifferentSeedsGiveDifferentFloors()
        {
            Assert.AreNotEqual(DungeonGenerator.Generate(1, Config).ToAscii(), DungeonGenerator.Generate(2, Config).ToAscii());
        }

        [Test]
        public void FloorsAreValidAcrossManySeeds()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                var map = DungeonGenerator.Generate(seed, Config);
                string context = $"seed {seed}\n{map.ToAscii()}";

                Assert.GreaterOrEqual(map.Rooms.Count, Config.MinRooms, context);
                Assert.IsTrue(map.IsWalkable(map.Start), context);
                Assert.IsTrue(map.IsWalkable(map.Stairs), context);
                Assert.AreNotEqual(map.RoomIndexAt(map.Start), map.RoomIndexAt(map.Stairs), "start and stairs share a room: " + context);
                AssertEdgesAreWall(map, context);
                AssertEveryFloorTileReachable(map, context);
            }
        }

        static void AssertEdgesAreWall(DungeonMap map, string context)
        {
            for (int x = 0; x < map.Width; x++)
            {
                Assert.IsFalse(map.IsWalkable(new GridPos(x, 0)), context);
                Assert.IsFalse(map.IsWalkable(new GridPos(x, map.Height - 1)), context);
            }
            for (int y = 0; y < map.Height; y++)
            {
                Assert.IsFalse(map.IsWalkable(new GridPos(0, y)), context);
                Assert.IsFalse(map.IsWalkable(new GridPos(map.Width - 1, y)), context);
            }
        }

        /// <summary>Flood fill using orthogonal steps only. Stricter than 8-way, so the corner rule can't strand anything.</summary>
        static void AssertEveryFloorTileReachable(DungeonMap map, string context)
        {
            var reached = new HashSet<GridPos> { map.Start };
            var frontier = new Queue<GridPos>();
            frontier.Enqueue(map.Start);
            var steps = new[] { Direction8.N, Direction8.E, Direction8.S, Direction8.W };
            while (frontier.Count > 0)
            {
                var p = frontier.Dequeue();
                foreach (var dir in steps)
                {
                    var next = p + dir.ToOffset();
                    if (map.IsWalkable(next) && reached.Add(next)) frontier.Enqueue(next);
                }
            }

            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (map.IsWalkable(new GridPos(x, y)))
                        Assert.IsTrue(reached.Contains(new GridPos(x, y)), $"({x}, {y}) is unreachable: {context}");
        }
    }
}

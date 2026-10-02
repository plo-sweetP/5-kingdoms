using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    public class PathfinderTests
    {
        [Test]
        public void StepsAroundAWallWithoutCuttingCorners()
        {
            var map = DungeonMap.FromAscii(
                "#######",
                "#.....#",
                "#@#...#",
                "#.#...#",
                "#######");
            // Goal is just past the wall. NE would cut the wall's corner, so the only first step is N.
            Assert.IsTrue(Pathfinder.TryFirstStep(map, map.Start, new GridPos(3, 2), null, 50, out var step));
            Assert.AreEqual(Direction8.N, step);
        }

        [Test]
        public void TakesTheDiagonalWhenItIsOpen()
        {
            var map = DungeonMap.FromAscii(
                "#####",
                "#...#",
                "#...#",
                "#@..#",
                "#####");
            Assert.IsTrue(Pathfinder.TryFirstStep(map, map.Start, new GridPos(3, 3), null, 50, out var step));
            Assert.AreEqual(Direction8.NE, step);
        }

        [Test]
        public void FailsWhenTheGoalIsWalledOff()
        {
            var map = DungeonMap.FromAscii(
                "#######",
                "#@.#..#",
                "#######");
            Assert.IsFalse(Pathfinder.TryFirstStep(map, map.Start, new GridPos(4, 1), null, 50, out _));
        }

        [Test]
        public void TreatsBlockedTilesAsWallsButStillReachesTheGoal()
        {
            var map = DungeonMap.FromAscii(
                "######",
                "#@...#",
                "######");
            var blocker = new GridPos(3, 1);
            Assert.IsFalse(Pathfinder.TryFirstStep(map, map.Start, new GridPos(4, 1), p => p == blocker, 50, out _));
            Assert.IsTrue(Pathfinder.TryFirstStep(map, map.Start, blocker, p => p == blocker, 50, out var step));
            Assert.AreEqual(Direction8.E, step);
        }
    }
}

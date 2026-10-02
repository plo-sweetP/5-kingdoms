using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    public class MovementTests
    {
        [Test]
        public void HeroWalksOntoFloorAndUsesATurn()
        {
            var run = TestRuns.OnMap(
                "#####",
                "#@..#",
                "#####");
            Assert.IsTrue(run.Move(Direction8.E));
            Assert.AreEqual(new GridPos(2, 1), run.Hero.Pos);
            Assert.AreEqual(1, run.Turn);
        }

        [Test]
        public void BumpingAWallOnlyTurnsTheHero()
        {
            var run = TestRuns.OnMap(
                "#####",
                "#@..#",
                "#####");
            Assert.IsFalse(run.Move(Direction8.N));
            Assert.AreEqual(new GridPos(1, 1), run.Hero.Pos);
            Assert.AreEqual(Direction8.N, run.Hero.Facing);
            Assert.AreEqual(0, run.Turn);
            Assert.IsInstanceOf<FacingChangedEvent>(run.Events[0]);
        }

        [Test]
        public void DiagonalStepCannotCutAWallCorner()
        {
            var run = TestRuns.OnMap(
                "####",
                "##.#",
                "#@.#",
                "####");
            Assert.IsFalse(run.Move(Direction8.NE));
            Assert.AreEqual(new GridPos(1, 1), run.Hero.Pos);
        }

        [Test]
        public void DiagonalStepWorksWhenBothCornersAreOpen()
        {
            var run = TestRuns.OnMap(
                "#####",
                "#...#",
                "#@..#",
                "#####");
            Assert.IsTrue(run.Move(Direction8.NE));
            Assert.AreEqual(new GridPos(2, 2), run.Hero.Pos);
        }

        [Test]
        public void StairsLeadToTheNextFloorAndTheLastOneClearsTheDungeon()
        {
            var run = TestRuns.OnMap(2,
                "#####",
                "#@>.#",
                "#####");
            Assert.IsFalse(run.Descend(), "can't descend away from the stairs");

            run.Move(Direction8.E);
            Assert.IsTrue(run.HeroOnStairs);
            Assert.IsTrue(run.Descend());
            Assert.AreEqual(2, run.Floor);
            Assert.AreEqual(run.Map.Start, run.Hero.Pos);

            run.Move(Direction8.E);
            Assert.IsTrue(run.Descend());
            Assert.AreEqual(RunState.Won, run.State);
            Assert.IsFalse(run.Move(Direction8.W), "no actions after the run ends");
        }
    }
}

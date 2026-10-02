using System.Collections.Generic;
using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    /// <summary>Lets the autopilot play many full runs on real generated floors and checks the rules never break.</summary>
    public class SoakTests
    {
        const int Seeds = 40;
        const int MaxActionsPerRun = 3000;

        [Test]
        public void AutoPilotRunsKeepTheWorldConsistent()
        {
            int deepestFloor = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var run = new DungeonRun(seed);
                for (int step = 0; step < MaxActionsPerRun && run.State == RunState.InProgress; step++)
                {
                    run.Execute(AutoPilot.Decide(run));
                    AssertConsistent(run, $"seed {seed}, action {step}");
                }
                Assert.AreNotEqual(RunState.InProgress, run.State,
                    $"seed {seed}: the autopilot stalled on B{run.Floor}F at {run.Hero.Pos} (stairs {run.Map.Stairs})");
                if (run.Floor > deepestFloor) deepestFloor = run.Floor;
            }
            Assert.GreaterOrEqual(deepestFloor, 3, "the autopilot should get a few floors deep on some seed");
        }

        static void AssertConsistent(DungeonRun run, string context)
        {
            var occupied = new HashSet<GridPos>();
            foreach (var actor in run.Actors)
            {
                Assert.IsTrue(run.Map.IsWalkable(actor.Pos), $"{actor} is inside a wall ({context})");
                Assert.IsTrue(occupied.Add(actor.Pos), $"two actors share {actor.Pos} ({context})");
                Assert.That(actor.Hp, Is.InRange(1, actor.MaxHp), $"{actor} has bad HP ({context})");
            }
            if (run.State == RunState.InProgress)
                Assert.IsTrue(run.Actors.Contains(run.Hero), $"hero missing while the run is in progress ({context})");
            foreach (var item in run.Items)
                Assert.IsTrue(run.Map.IsWalkable(item.Pos), $"item inside a wall ({context})");
            Assert.That(run.Berries, Is.InRange(0, run.Config.MaxBerries), context);
        }
    }
}

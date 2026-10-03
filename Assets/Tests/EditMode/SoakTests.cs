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
        public void AutoPilotRunsKeepTheWorldConsistent() => Soak(seed => new DungeonRun(seed));

        [Test]
        public void PartyRunsKeepTheWorldConsistent() =>
            Soak(seed => new DungeonRun(seed, new DungeonRunConfig { Party = ActorCatalog.StartingParty }));

        [Test]
        public void PartyRunsLedByHaidenKeepTheWorldConsistent() =>
            Soak(seed => new DungeonRun(seed, new DungeonRunConfig { Party = new[] { ActorCatalog.Haiden, ActorCatalog.Uzuki, ActorCatalog.Kristela } }));

        [Test]
        public void PartyRunsLedByKristelaKeepTheWorldConsistent() =>
            Soak(seed => new DungeonRun(seed, new DungeonRunConfig { Party = new[] { ActorCatalog.Kristela, ActorCatalog.Haiden, ActorCatalog.Uzuki } }));

        static void Soak(System.Func<int, DungeonRun> start)
        {
            int deepestFloor = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var run = start(seed);
                var swaps = new Dictionary<(int, int), List<int>>();
                for (int step = 0; step < MaxActionsPerRun && run.State == RunState.InProgress; step++)
                {
                    run.Execute(AutoPilot.Decide(run));
                    string context = $"seed {seed}, action {step}";
                    AssertConsistent(run, context);
                    AssertNoSwapLoops(run, swaps, context);
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
            {
                Assert.IsTrue(run.Actors.Contains(run.Hero), $"leader missing while the run is in progress ({context})");
                foreach (var member in run.Party)
                    Assert.AreEqual(member.IsAlive, run.Actors.Contains(member), $"{member} standing iff alive ({context})");
            }
            foreach (var item in run.Items)
                Assert.IsTrue(run.Map.IsWalkable(item.Pos), $"item inside a wall ({context})");
            Assert.That(run.Berries, Is.InRange(0, run.Config.MaxBerries), context);
            foreach (var member in run.Party)
                Assert.That(member.Charge, Is.InRange(0, CombatRules.MaxCharge), $"{member}'s charge ({context})");
        }

        /// <summary>
        /// PROGRESSION.md, "No swap loops": fails if the same two heroes swap back and forth (three swaps within six
        /// leader turns). A melee partner swapping to the front as a fight starts and a ranged leader walking back through
        /// it once the fight is over are fine.
        /// </summary>
        static void AssertNoSwapLoops(DungeonRun run, Dictionary<(int, int), List<int>> swaps, string context)
        {
            foreach (var swapped in run.Events.OfType<SwappedEvent>())
            {
                var pair = (System.Math.Min(swapped.ActorId, swapped.OtherId), System.Math.Max(swapped.ActorId, swapped.OtherId));
                if (!swaps.TryGetValue(pair, out var turns)) swaps[pair] = turns = new List<int>();
                turns.Add(run.Turn);
                Assert.Less(turns.Count(turn => run.Turn - turn < 6), 3, $"heroes {pair} keep swapping places ({context})");
            }
        }
    }
}

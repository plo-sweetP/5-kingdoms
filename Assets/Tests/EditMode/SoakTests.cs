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
        public void PartyRunsKeepTheWorldConsistent()
        {
            var totals = Soak(seed => new DungeonRun(seed, new DungeonRunConfig { Party = ActorCatalog.StartingParty }));
            // The rules under test must actually come up, or the checks above prove nothing.
            Assert.Greater(totals.Delays, 0, "no stun ever landed");
            Assert.Greater(totals.SafetySwaps, 0, "nobody ever ran to safety");
            Assert.Greater(totals.OffLineShots, 0, "no shot ever flew off the 8 lines");
        }

        [Test]
        public void PartyRunsLedByUzukiKeepTheWorldConsistent() =>
            Soak(seed => new DungeonRun(seed, new DungeonRunConfig { Party = new[] { ActorCatalog.Uzuki, ActorCatalog.Haiden, ActorCatalog.Kristela } }));

        [Test]
        public void PartyRunsLedByKristelaKeepTheWorldConsistent() =>
            Soak(seed => new DungeonRun(seed, new DungeonRunConfig { Party = new[] { ActorCatalog.Kristela, ActorCatalog.Haiden, ActorCatalog.Uzuki } }));

        /// <summary>How often the part-2 rules came up in a soak.</summary>
        sealed class Totals
        {
            public int Delays, SafetySwaps, OffLineShots;
        }

        static Totals Soak(System.Func<int, DungeonRun> start)
        {
            var totals = new Totals();
            int deepestFloor = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var run = start(seed);
                var swaps = new Dictionary<(int, int), List<int>>();
                var delays = new HashSet<(int, int)>();
                for (int step = 0; step < MaxActionsPerRun && run.State == RunState.InProgress; step++)
                {
                    string context = $"seed {seed}, action {step}";
                    var command = AutoPilot.Decide(run);
                    AssertDeliberate(run, command, context);
                    // Who might run to safety during this action: badly hurt, with a foe next to them.
                    var hurt = run.Party.Where(member => member.IsAlive && DungeonRun.IsBadlyHurt(member) && run.FoeAdjacent(member))
                        .Select(member => member.Id).ToList();
                    Assert.IsTrue(run.Execute(command), $"the autopilot's {command} was refused ({context})");
                    AssertConsistent(run, context);
                    AssertNoSwapLoops(run, swaps, context);
                    AssertDelayedOncePerTurn(run, delays, context);
                    AssertHeroAttacksHaveTargets(run, context);
                    totals.Delays += run.Events.OfType<TurnDelayedEvent>().Count();
                    totals.SafetySwaps += run.Events.OfType<SwappedEvent>().Count(swap => hurt.Contains(swap.ActorId));
                    totals.OffLineShots += run.Events.OfType<AttackEvent>().Count(shot => shot.Ranged && shot.TargetId >= 0 && IsOffLine(run, shot));
                }
                Assert.AreNotEqual(RunState.InProgress, run.State,
                    $"seed {seed}: the autopilot stalled on B{run.Floor}F at {run.Hero.Pos} (stairs {run.Map.Stairs})");
                if (run.Floor > deepestFloor) deepestFloor = run.Floor;
            }
            Assert.GreaterOrEqual(deepestFloor, 3, "the autopilot should get a few floors deep on some seed");
            return totals;
        }

        /// <summary>
        /// Attacks are deliberate (PROGRESSION.md, "Targeting and input"): the autopilot never walks into an enemy, and
        /// its attacks and target skills name the tile of a foe they can reach.
        /// </summary>
        static void AssertDeliberate(DungeonRun run, HeroCommand command, string context)
        {
            var hero = run.Hero;
            if (command.Kind == HeroCommandKind.Move)
            {
                var occupant = run.ActorAt(hero.Pos + command.Direction.ToOffset());
                Assert.IsFalse(occupant != null && occupant.Team != hero.Team, $"the autopilot walked into {occupant} ({context})");
            }
            if (command.Kind == HeroCommandKind.Attack)
            {
                Assert.IsTrue(command.Targeted, $"an attack without a target ({context})");
                Assert.IsNotNull(run.AttackTargetAt(hero, command.Target), $"an attack on a tile out of reach ({context})");
            }
        }

        /// <summary>No hero swings or shoots at nothing: every attack by a party member, partners included, has a target.</summary>
        static void AssertHeroAttacksHaveTargets(DungeonRun run, string context)
        {
            foreach (var attack in run.Events.OfType<AttackEvent>())
            {
                bool byHero = run.Party.Any(member => member.Id == attack.AttackerId);
                Assert.IsFalse(byHero && attack.TargetId < 0, $"hero {attack.AttackerId} attacked nothing ({context})");
            }
        }

        /// <summary>
        /// PROGRESSION.md, "Delays / stuns": an actor is delayed at most once per its own turn, by at most 50% of a turn
        /// (25% for a boss). Every delay says how many turns the actor had taken, so two with the same count would mean it
        /// was pushed back twice before acting.
        /// </summary>
        static void AssertDelayedOncePerTurn(DungeonRun run, HashSet<(int, int)> delays, string context)
        {
            foreach (var delay in run.Events.OfType<TurnDelayedEvent>())
            {
                Assert.IsTrue(delays.Add((delay.ActorId, delay.TurnsTaken)), $"actor {delay.ActorId} was delayed twice before it acted ({context})");
                Assert.That(delay.Percent, Is.InRange(1, CombatRules.MaxDelayPercent), context);
                var actor = run.FindActor(delay.ActorId);
                if (actor != null && actor.Definition.IsBoss) Assert.LessOrEqual(delay.Percent, CombatRules.MaxBossDelayPercent, context);
            }
        }

        /// <summary>A shot that landed on a tile off the shooter's row, column and diagonals (where the shooter stands now).</summary>
        static bool IsOffLine(DungeonRun run, AttackEvent shot)
        {
            var shooter = run.FindActor(shot.AttackerId);
            if (shooter == null) return false;
            int dx = System.Math.Abs(shot.To.X - shooter.Pos.X), dy = System.Math.Abs(shot.To.Y - shooter.Pos.Y);
            return dx != 0 && dy != 0 && dx != dy;
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

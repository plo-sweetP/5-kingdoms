using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// Regression anchor for equal-speed play: 25 seeded autopilot runs, fingerprinted action by action. Any change to
    /// turn order, damage rolls or AI decisions shows up as a different value. History: first recorded from the
    /// alternating-turn engine before the action-value timeline existed, and the timeline matched it exactly (1c).
    /// Re-recorded at 1e, when the multiplicative damage formula and the 10x stat rescale changed every hit on purpose.
    /// </summary>
    public class GoldenReplayTests
    {
        /// <summary>Recorded on 2026-10-03 at milestone 1e (multiplicative damage, 10x stats, 5% base crit).</summary>
        const ulong RecordedFingerprint = 14233019579046662732UL;

        [Test]
        public void EqualSpeedsReplayTheOriginalTurnOrder()
        {
            Assert.AreEqual(RecordedFingerprint, Fingerprint());
        }

        /// <summary>25 seeded autopilot runs, hashed action by action (FNV-1a over the visible game state).</summary>
        static ulong Fingerprint()
        {
            ulong hash = 14695981039346656037UL;
            void Add(long value)
            {
                unchecked
                {
                    hash ^= (ulong)value;
                    hash *= 1099511628211UL;
                }
            }

            for (int seed = 1; seed <= 25; seed++)
            {
                var run = new DungeonRun(seed, EqualSpeedConfig());
                for (int step = 0; step < 600 && run.State == RunState.InProgress; step++)
                {
                    var command = AutoPilot.Decide(run);
                    Add((int)command.Kind);
                    Add((int)command.Direction);
                    Add(run.Execute(command) ? 1 : 0);
                    Add(run.Floor);
                    Add(run.Turn);
                    Add((int)run.State);
                    Add(run.Berries);
                    Add(run.Items.Count);
                    foreach (var actor in run.Actors)
                    {
                        Add(actor.Id);
                        Add(actor.Pos.X);
                        Add(actor.Pos.Y);
                        Add(actor.Hp);
                        Add(actor.Level);
                        Add(actor.Exp);
                    }
                }
            }
            return hash;
        }

        /// <summary>
        /// Pinned rules and stats, independent of later tuning in the catalog: the original stats on the 10x scale,
        /// every speed 100, no skills or mana, berries that heal 300 HP.
        /// </summary>
        static DungeonRunConfig EqualSpeedConfig() => new DungeonRunConfig
        {
            BerryHealHp = 300,
            BerryRestoreMp = 0,
            Hero = new ActorDefinition("uzuki", "Uzuki", maxHp: 400, attack: 60, defense: 30, expReward: 0, speed: 100,
                hpGrowth: 50, atkGrowth: 10, defGrowth: 10),
            Enemy = new ActorDefinition("slime", "Slime", maxHp: 140, attack: 50, defense: 10, expReward: 6, speed: 100,
                hpGrowth: 30, atkGrowth: 10, defGrowth: 5, expGrowth: 2),
            Boss = new ActorDefinition("king_slime", "King Slime", maxHp: 1100, attack: 110, defense: 50, expReward: 80,
                brain: ActorBrain.SlimeKing, speed: 100),
        };
    }
}

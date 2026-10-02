using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// Regression anchor for the action-value timeline: with every speed equal, autopilot runs must replay exactly as
    /// they did under the original alternating turns (hero, then every enemy once, in id order). The fingerprint was
    /// recorded from that engine before the timeline existed; any change to turn order, damage rolls or AI decisions
    /// shows up as a different value.
    /// </summary>
    public class GoldenReplayTests
    {
        /// <summary>Recorded on 2026-10-02 from commit 0ff431e (alternating turns), before the timeline.</summary>
        const ulong RecordedFingerprint = 384843934597161559UL;

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
        /// The original stats with every speed pinned to 100, independent of later tuning in the catalog, so this stays
        /// a test of the turn order alone.
        /// </summary>
        static DungeonRunConfig EqualSpeedConfig() => new DungeonRunConfig
        {
            Hero = new ActorDefinition("uzuki", "Uzuki", maxHp: 40, attack: 6, defense: 3, expReward: 0, speed: 100),
            Enemy = new ActorDefinition("slime", "Slime", maxHp: 14, attack: 5, defense: 1, expReward: 6, speed: 100),
            Boss = new ActorDefinition("king_slime", "King Slime", maxHp: 110, attack: 11, defense: 5, expReward: 80,
                brain: ActorBrain.SlimeKing, speed: 100),
        };
    }
}

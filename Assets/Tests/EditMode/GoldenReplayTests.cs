using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// Regression anchor for equal-speed play: 25 seeded autopilot runs, fingerprinted action by action. Any change to
    /// turn order, damage rolls or AI decisions shows up as a different value. History: first recorded from the
    /// alternating-turn engine before the action-value timeline existed, and the timeline matched it exactly (1c).
    /// Re-recorded at 1e, when the multiplicative damage formula and the 10x stat rescale changed every hit on purpose;
    /// at 1f, when the autopilot stopped waiting behind a monster that blocks the only way to the stairs and fights
    /// through it instead (the party rewrite itself left these solo replays unchanged); and at 1f part 2, when attacks
    /// became deliberate: the autopilot no longer attacks by walking into a foe but with a command naming its target's
    /// tile (now part of the fingerprint), and of several foes in reach it picks the lowest HP instead of the first in
    /// turn order; and at C1 (corridor tactics), when the lone hero started to hold a doorway against two or more foes
    /// close beyond it, letting them come to it one at a time, instead of stepping out among them; and at playtest
    /// pass 1 (footing in a boss fight), when the hero stopped walking into a slam that is winding up, and started
    /// to move off a tile next to the boss that has no way out of the slam.
    /// </summary>
    public class GoldenReplayTests
    {
        /// <summary>Recorded on 2026-10-10 at playtest pass 1 (the hero keeps out of a wind-up and minds its footing next to the boss).</summary>
        const ulong RecordedFingerprint = 12582735607441105301UL;

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
                    Add(command.Targeted ? 1 : 0);
                    Add(command.Target.X);
                    Add(command.Target.Y);
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
        /// Pinned rules and stats, independent of later tuning in the catalog and config: the original stats on the 10x
        /// scale, the original enemy counts placed one by one (no packs), every speed 100, no skills or ultimate, berries
        /// that heal 300 HP, one melee hero.
        /// </summary>
        static DungeonRunConfig EqualSpeedConfig() => new DungeonRunConfig
        {
            BerryHealHp = 300,
            EnemiesOnFirstFloor = 5,
            ExtraEnemiesPerFloor = 1,
            MaxEnemies = 10,
            PackSizeMin = 1,
            PackSizeMax = 1,
            FastEnemy = null,
            Hero = new ActorDefinition("uzuki", "Uzuki", maxHp: 400, attack: 60, defense: 30, expReward: 0, speed: 100,
                hpGrowth: 50, atkGrowth: 10, defGrowth: 10),
            Enemy = new ActorDefinition("spider", "Spider", maxHp: 140, attack: 50, defense: 10, expReward: 6, speed: 100,
                hpGrowth: 30, atkGrowth: 10, defGrowth: 5, expGrowth: 2),
            Boss = new ActorDefinition("troll", "Troll", maxHp: 1100, attack: 110, defense: 50, expReward: 80,
                brain: ActorBrain.Troll, speed: 100),
        };
    }
}

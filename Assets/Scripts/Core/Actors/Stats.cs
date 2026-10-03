using System;

namespace FiveKingdoms.Core
{
    /// <summary>
    /// The fighting stats from GEAR.md. HP, ATK, DEF and SPD are plain numbers. Crit Rate, Crit DMG, Affinity (chance to
    /// land status effects) and Resist (chance to shrug them off) are percentages kept in tenths of a percent, so
    /// 5% Crit Rate is 50 and 50% Crit DMG is 500.
    /// </summary>
    public enum StatKind { Hp, Atk, Def, Spd, CritRate, CritDmg, Affinity, Resist }

    /// <summary>One number per <see cref="StatKind"/>.</summary>
    public sealed class StatBlock
    {
        public static readonly int Count = Enum.GetValues(typeof(StatKind)).Length;

        readonly int[] values = new int[Count];

        public int this[StatKind kind]
        {
            get => values[(int)kind];
            set => values[(int)kind] = value;
        }

        public void Clear() => Array.Clear(values, 0, values.Length);

        public void Add(StatBlock other)
        {
            for (int i = 0; i < values.Length; i++) values[i] += other.values[i];
        }
    }

    /// <summary>
    /// How an actor's final stats are put together (GEAR.md): (base + level growth + weapon) x (1 + % bonuses) + flat
    /// bonuses. Weapon stats count as base, like Honkai: Star Rail's light cones, so % bonuses scale them; flat bonuses
    /// (a helmet's HP, gloves' ATK) are not scaled. Only HP, ATK and DEF take % bonuses. SPD and the percentage stats
    /// (crit, Affinity, Resist) only ever add up. Crit Rate caps at 100%.
    /// </summary>
    public sealed class StatSheet
    {
        public const int MaxCritRate = 1000;

        /// <summary>Base, level growth and weapon stats.</summary>
        public StatBlock Base { get; } = new StatBlock();

        /// <summary>% bonuses for HP, ATK and DEF, in tenths of a percent (gear, sets, class tiers).</summary>
        public StatBlock Percent { get; } = new StatBlock();

        /// <summary>Flat bonuses, added after the % bonuses.</summary>
        public StatBlock Flat { get; } = new StatBlock();

        public int Final(StatKind kind)
        {
            int value = Base[kind] + Flat[kind];
            switch (kind)
            {
                case StatKind.Hp:
                case StatKind.Atk:
                case StatKind.Def:
                    return (int)((long)Base[kind] * (1000 + Percent[kind]) / 1000) + Flat[kind];
                case StatKind.CritRate:
                    return Math.Min(MaxCritRate, value);
                default:
                    return value;
            }
        }
    }
}

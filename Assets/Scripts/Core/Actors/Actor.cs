using System;

namespace FiveKingdoms.Core
{
    public enum Team { Hero, Enemy }

    /// <summary>
    /// A character or monster standing in the dungeon, with its current stats. Its <see cref="Stats"/> sheet is built
    /// from the definition, its level and its weapon; the final stats below are read from the sheet whenever it
    /// changes (see <see cref="RecalculateStats"/>).
    /// </summary>
    public sealed class Actor
    {
        public Actor(int id, ActorDefinition definition, Team team, GridPos pos, int level = 1)
        {
            Id = id;
            Definition = definition;
            Team = team;
            Pos = pos;
            Level = Math.Max(1, level);
            Weapon = definition.Weapon;
            SkillCooldowns = new int[definition.Skills.Count];
            RecalculateStats();
            Speed = Stats.Final(StatKind.Spd);
            Hp = MaxHp;
            Mp = MaxMp;
        }

        public int Id { get; }
        public ActorDefinition Definition { get; }
        public string Name => Definition.Name;
        public Team Team { get; }
        public GridPos Pos { get; set; }
        public Direction8 Facing { get; set; } = Direction8.S;

        public int Level { get; set; } = 1;
        public int Exp { get; set; }

        /// <summary>Where the final stats come from: (base + level growth + weapon) x % bonuses + flat bonuses.</summary>
        public StatSheet Stats { get; } = new StatSheet();

        /// <summary>Counts toward the base stats; null fights unarmed (monsters).</summary>
        public WeaponDefinition Weapon { get; set; }

        public int MaxHp { get; set; }
        public int Hp { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int ExpReward { get; set; }

        /// <summary>Chance to crit and the extra damage a crit does, in tenths of a percent (50 = 5%, 500 = +50%).</summary>
        public int CritRate { get; set; }
        public int CritDmg { get; set; }

        /// <summary>Chance to land status effects, and to shrug them off, in tenths of a percent.</summary>
        public int Affinity { get; set; }
        public int Resist { get; set; }

        /// <summary>
        /// Combat speed: one turn every 10000 / Speed AV. Change it mid-fight through DungeonRun.SetSpeed so the
        /// timeline keeps the actor's progress toward its next turn.
        /// </summary>
        public int Speed { get; set; }

        /// <summary>Mana for skills. Refilled at the start of each run; berries and mana-building skills restore it.</summary>
        public int Mp { get; set; }
        public int MaxMp { get; set; }

        /// <summary>Own turns left before each skill (by slot) can be used again.</summary>
        public int[] SkillCooldowns { get; }

        /// <summary>Enemy AI state: has noticed the hero and is giving chase.</summary>
        public bool Alerted { get; set; }

        /// <summary>Boss state: wound up for a special attack that lands on its next turn.</summary>
        public bool Charging { get; set; }

        /// <summary>Boss state: turns until its special attack is ready again.</summary>
        public int SpecialCooldown { get; set; }

        /// <summary>Boss state: has already called for reinforcements this fight.</summary>
        public bool CalledForHelp { get; set; }

        public bool IsAlive => Hp > 0;

        /// <summary>
        /// Rebuilds the sheet's base (definition + level growth + weapon) and reads every final stat back from the sheet.
        /// Current HP and MP move with their maximums, so a level-up heals by what it adds. Speed is left alone: it only
        /// changes through the timeline mid-fight, and levels never raise it.
        /// </summary>
        public void RecalculateStats()
        {
            var definition = Definition;
            int levels = Level - 1;
            var b = Stats.Base;
            b.Clear();
            b[StatKind.Hp] = definition.MaxHp + levels * definition.HpGrowth + (Weapon?.Hp ?? 0);
            b[StatKind.Atk] = definition.Attack + levels * definition.AtkGrowth + (Weapon?.Atk ?? 0);
            b[StatKind.Def] = definition.Defense + levels * definition.DefGrowth + (Weapon?.Def ?? 0);
            b[StatKind.Spd] = definition.Speed + (Weapon?.Spd ?? 0);
            b[StatKind.CritRate] = definition.CritRate;
            b[StatKind.CritDmg] = definition.CritDmg;

            int maxHpBefore = MaxHp, maxMpBefore = MaxMp;
            MaxHp = Stats.Final(StatKind.Hp);
            Attack = Stats.Final(StatKind.Atk);
            Defense = Stats.Final(StatKind.Def);
            CritRate = Stats.Final(StatKind.CritRate);
            CritDmg = Stats.Final(StatKind.CritDmg);
            Affinity = Stats.Final(StatKind.Affinity);
            Resist = Stats.Final(StatKind.Resist);
            MaxMp = definition.MaxMp + levels * definition.MpGrowth;
            ExpReward = definition.ExpReward + levels * definition.ExpGrowth;
            Hp = Math.Max(0, Math.Min(MaxHp, Hp + MaxHp - maxHpBefore));
            Mp = Math.Max(0, Math.Min(MaxMp, Mp + MaxMp - maxMpBefore));
        }

        public override string ToString() => $"{Name}#{Id} {Pos} HP {Hp}/{MaxHp}";
    }
}

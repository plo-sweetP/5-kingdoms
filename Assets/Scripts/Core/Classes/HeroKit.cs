using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>
    /// What an actor fights with in a run: its three skills (in button order) and its ultimate, its always-ready weapon
    /// attack, and for a hero what its class tiers add (stat bumps, a speed modifier). A hero's comes from its
    /// <see cref="HeroProgress"/> (its loadout, with the upgrades it picked) and stays as it is for the whole run:
    /// loadouts change between runs only. A monster's is just what its definition says.
    /// </summary>
    public sealed class HeroKit
    {
        /// <summary>PROGRESSION.md, "Speed protection": a hero's speed before gear stays within these, whatever its class.</summary>
        public const int MinSpeed = 85;
        public const int MaxSpeed = 100;

        static readonly SkillDefinition[] NoSkills = Array.Empty<SkillDefinition>();

        public HeroKit(IReadOnlyList<SkillDefinition> skills, SkillDefinition ultimate, SkillDefinition weaponAttack,
            StatBlock percent = null, StatBlock flat = null, int speedModifier = 0)
        {
            Skills = skills ?? NoSkills;
            Ultimate = ultimate;
            WeaponAttack = weaponAttack ?? throw new ArgumentNullException(nameof(weaponAttack));
            Percent = percent ?? new StatBlock();
            Flat = flat ?? new StatBlock();
            SpeedModifier = speedModifier;
        }

        /// <summary>Up to three skills, in button order, as this hero has them (with its upgrades).</summary>
        public IReadOnlyList<SkillDefinition> Skills { get; }

        /// <summary>Used when the charge meter is full (<see cref="Actor.Charge"/>); null for monsters.</summary>
        public SkillDefinition Ultimate { get; }

        /// <summary>
        /// The always-ready weapon attack (Quick Shot, Sword Slash, Jab): its name, how hard it hits and how often. A
        /// class option can change it for one weapon family.
        /// </summary>
        public SkillDefinition WeaponAttack { get; }

        /// <summary>What the hero's class tiers add to HP, ATK and DEF, in tenths of a percent of the base.</summary>
        public StatBlock Percent { get; }

        /// <summary>What they add to the percentage stats (Crit Rate, Crit DMG, Affinity, Resist), in tenths of a percent.</summary>
        public StatBlock Flat { get; }

        /// <summary>From the hero's highest-tier class: about -5 to +5.</summary>
        public int SpeedModifier { get; }

        /// <summary>The speed before gear of a hero whose own base is <paramref name="baseSpeed"/>. Without a class, just that.</summary>
        public int SpeedFor(int baseSpeed) =>
            SpeedModifier == 0 ? baseSpeed : Math.Max(MinSpeed, Math.Min(MaxSpeed, baseSpeed + SpeedModifier));

        /// <summary>
        /// The kit of an actor that brings no saved progress: a monster, or a hero as it starts out (tier 1 of its own
        /// class, its starting kit).
        /// </summary>
        public static HeroKit Starting(ActorDefinition definition) =>
            definition.StartingClass == null && definition.Skills.Count == 0 && definition.Ultimate == null
                ? new HeroKit(NoSkills, null, WeaponAttackOf(definition))
                : new HeroProgress(definition).Kit;

        /// <summary>
        /// A definition's weapon attack before any class option changes it: one hit of 200% ATK on a foe next to it, or,
        /// with reach, a shot at a foe in sight. It never sits out a turn.
        /// </summary>
        public static SkillDefinition WeaponAttackOf(ActorDefinition definition) =>
            new SkillDefinition("weapon_attack", definition.AttackName, definition.AttackName,
                definition.IsRanged ? SkillEffect.Shot : SkillEffect.Strike, CombatRules.BasicAttackPercent, cooldown: 0,
                reach: definition.IsRanged ? AttackReach.Ranged : AttackReach.Melee, range: definition.AttackRange,
                weapon: definition.Weapon?.Family ?? WeaponFamily.None);
    }
}

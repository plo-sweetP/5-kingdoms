using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>
    /// What every tier of a class adds to one stat (PROGRESSION.md, "Classes": a small bump every tier, milestones too).
    /// HP, ATK and DEF grow by a percentage; Crit Rate, Crit DMG, Affinity and Resist are percentages themselves and
    /// simply add up. Both are in tenths of a percent, so 4 a tier is +10% at tier 25. Never SPD.
    /// </summary>
    public readonly struct StatBump
    {
        public readonly StatKind Stat;

        /// <summary>In tenths of a percent, per tier.</summary>
        public readonly int PerTier;

        public StatBump(StatKind stat, int perTier)
        {
            if (stat == StatKind.Spd) throw new ArgumentException("Classes never give SPD (PROGRESSION.md, \"Speed protection\").");
            Stat = stat;
            PerTier = perTier;
        }

        /// <summary>Whether the bump scales the stat (HP, ATK, DEF) rather than adding to a percentage stat.</summary>
        public bool IsPercentOfBase => Stat == StatKind.Hp || Stat == StatKind.Atk || Stat == StatKind.Def;
    }

    /// <summary>
    /// One of the three choices at a milestone tier: it teaches a new skill (or an alternate ultimate), upgrades a skill
    /// the hero knows, changes the weapon attack of one weapon family, or gives a passive. An option that upgrades a
    /// skill the hero doesn't know teaches that skill instead, as it is in the catalog.
    /// </summary>
    public sealed class ClassOption
    {
        /// <param name="path">Which of the class's three paths it belongs to (0-2): its column in the tree.</param>
        /// <param name="teaches">A new skill or ultimate for the hero's pool.</param>
        /// <param name="upgrades">The skill it changes, as the catalog has it.</param>
        /// <param name="change">What it changes on that skill, or on the weapon attack.</param>
        /// <param name="attackOf">It changes the weapon attack of this family instead of a skill.</param>
        public ClassOption(string id, string name, int path, string description, SkillDefinition teaches = null,
            SkillDefinition upgrades = null, Action<SkillDefinition> change = null, WeaponFamily attackOf = WeaponFamily.None)
        {
            if (path < 0 || path >= ClassDefinition.PathCount) throw new ArgumentOutOfRangeException(nameof(path));
            if ((upgrades != null || attackOf != WeaponFamily.None) != (change != null))
                throw new ArgumentException($"{id}: an upgrade needs both the skill (or the weapon attack) and what it changes.");
            if (teaches != null && (upgrades != null || attackOf != WeaponFamily.None))
                throw new ArgumentException($"{id}: an option either teaches a skill or upgrades one.");
            Id = id;
            Name = name;
            Path = path;
            Description = description;
            Teaches = teaches;
            Upgrades = upgrades;
            Change = change;
            AttackOf = attackOf;
        }

        /// <summary>Stable key, used for saves.</summary>
        public string Id { get; }
        public string Name { get; }
        public int Path { get; }

        /// <summary>What it does, in a sentence or two, for the tree's info panel.</summary>
        public string Description { get; }

        public SkillDefinition Teaches { get; }
        public SkillDefinition Upgrades { get; }
        public Action<SkillDefinition> Change { get; }
        public WeaponFamily AttackOf { get; }

        /// <summary>The skill the tree shows for it: the one it teaches or upgrades. Null for the weapon attack and for passives.</summary>
        public SkillDefinition Skill => Teaches ?? Upgrades;
    }

    /// <summary>A milestone tier (5, 10, 15, 20, 25) and its three options, one per path. None yet: the row shows as locked.</summary>
    public sealed class ClassMilestone
    {
        public ClassMilestone(int tier, params ClassOption[] options)
        {
            if (!ClassDefinition.IsMilestone(tier)) throw new ArgumentException($"Tier {tier} is not a milestone.");
            if (options.Length != 0 && options.Length != ClassDefinition.PathCount)
                throw new ArgumentException($"Tier {tier}: a milestone has {ClassDefinition.PathCount} options (Peter, 2026-10-03), or none yet.");
            for (int path = 0; path < options.Length; path++)
                if (options[path].Path != path) throw new ArgumentException($"Tier {tier}: option {options[path].Id} is listed under the wrong path.");
            Tier = tier;
            Options = options;
        }

        public int Tier { get; }

        /// <summary>By path (0-2), or empty while the tier has no content.</summary>
        public IReadOnlyList<ClassOption> Options { get; }
    }

    /// <summary>
    /// A class (PROGRESSION.md, "Classes"): 25 tiers at one point each. Every tier gives the class's stat bumps; tiers
    /// 5, 10, 15, 20 and 25 are milestones where the hero picks one of three options, from any of the class's three
    /// paths. A tier whose options aren't written yet can't be reached. The hero's highest class also nudges its
    /// speed ("Speed protection"). Any hero can put points into any base class.
    /// </summary>
    public sealed class ClassDefinition
    {
        public const int MaxTier = 25;
        public const int MilestoneEvery = 5;
        public const int PathCount = 3;

        /// <param name="weapon">The weapon family its skills are mostly tied to (shown in the tree; each skill says its own).</param>
        /// <param name="speedModifier">About -5 to +5, for a hero whose highest class this is.</param>
        public ClassDefinition(string id, string name, WeaponFamily weapon, int speedModifier, IReadOnlyList<string> paths,
            IReadOnlyList<StatBump> bumps = null, IReadOnlyList<ClassMilestone> milestones = null)
        {
            if (paths == null || paths.Count != PathCount) throw new ArgumentException($"{id}: a class has {PathCount} paths.");
            Id = id;
            Name = name;
            Weapon = weapon;
            SpeedModifier = speedModifier;
            Paths = paths;
            Bumps = bumps ?? Array.Empty<StatBump>();
            Milestones = milestones ?? Array.Empty<ClassMilestone>();
        }

        /// <summary>Stable key, used for saves.</summary>
        public string Id { get; }
        public string Name { get; }
        public WeaponFamily Weapon { get; }
        public int SpeedModifier { get; }

        /// <summary>The three paths' names, left to right in the tree.</summary>
        public IReadOnlyList<string> Paths { get; }

        /// <summary>What each tier adds.</summary>
        public IReadOnlyList<StatBump> Bumps { get; }

        /// <summary>The milestone tiers that have content, in any order.</summary>
        public IReadOnlyList<ClassMilestone> Milestones { get; }

        public static bool IsMilestone(int tier) => tier > 0 && tier <= MaxTier && tier % MilestoneEvery == 0;

        /// <summary>The milestone at <paramref name="tier"/> if its options are written, else null.</summary>
        public ClassMilestone MilestoneAt(int tier)
        {
            foreach (var milestone in Milestones)
                if (milestone.Tier == tier && milestone.Options.Count > 0) return milestone;
            return null;
        }

        /// <summary>Whether a hero can stand on <paramref name="tier"/>: a milestone needs its options.</summary>
        public bool IsOpen(int tier) => tier >= 1 && tier <= MaxTier && (!IsMilestone(tier) || MilestoneAt(tier) != null);

        /// <summary>The highest tier a hero can reach today: the one before the first milestone that isn't written yet.</summary>
        public int HighestOpenTier
        {
            get
            {
                int tier = 0;
                while (tier < MaxTier && IsOpen(tier + 1)) tier++;
                return tier;
            }
        }

        /// <summary>The option with this id, on any tier, or null.</summary>
        public ClassOption FindOption(string id, out int tier)
        {
            foreach (var milestone in Milestones)
                foreach (var option in milestone.Options)
                    if (option.Id == id)
                    {
                        tier = milestone.Tier;
                        return option;
                    }
            tier = 0;
            return null;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// The classes heroes can learn. Part 1 of milestone 1g has the starting party's three and the Monk (PROGRESSION.md,
    /// "Starting class content"); the other base classes, the advanced and the inherited ones come later. Each has
    /// its "Every tier" stat bumps (in tenths of a percent a tier, so 4 is +10% at tier 25; never SPD); the
    /// milestones' options are not written yet, so a class stops at tier 4 for now.
    /// </summary>
    public static class ClassCatalog
    {
        /// <summary>Uzuki's class: ranged physical damage, traps and control. A light, quick class.</summary>
        public static readonly ClassDefinition Archer = new ClassDefinition("archer", "Archer", WeaponFamily.Bow, speedModifier: 5,
            paths: new[] { "Marksman", "Hunter", "Trickshot" },
            bumps: new[] { new StatBump(StatKind.Atk, 4), new StatBump(StatKind.CritRate, 2) });

        /// <summary>Haiden's class: a holy warrior in armor, protection and some healing. A heavy, slower class.</summary>
        public static readonly ClassDefinition Paladin = new ClassDefinition("paladin", "Paladin", WeaponFamily.Sword, speedModifier: -5,
            paths: new[] { "Guardian", "Devotion", "Crusader" },
            bumps: new[] { new StatBump(StatKind.Hp, 4), new StatBump(StatKind.Def, 4) });

        /// <summary>
        /// Kristela's class since 2026-10-05 (Peter: "I'm not feeling the monk abilities for her"): a light blade,
        /// flurries, lunges and counters. A quick class, like the Monk she was until then.
        /// </summary>
        public static readonly ClassDefinition Fencer = new ClassDefinition("fencer", "Fencer", WeaponFamily.Sword, speedModifier: 5,
            paths: new[] { "Duelist", "Footwork", "En Garde" },
            bumps: new[] { new StatBump(StatKind.CritRate, 2), new StatBump(StatKind.CritDmg, 4) });

        /// <summary>
        /// Fast unarmed martial arts, many hits. A quick class. No starting hero has it since Kristela became a Fencer;
        /// its tiers are built when a hero or a weapon brings it into play.
        /// </summary>
        public static readonly ClassDefinition Monk = new ClassDefinition("monk", "Monk", WeaponFamily.Fists, speedModifier: 5,
            paths: new[] { "Striker", "Windwalker", "Mystic" },
            bumps: new[] { new StatBump(StatKind.Atk, 4), new StatBump(StatKind.CritDmg, 4) });

        /// <summary>Every class, in the order the tree lists them.</summary>
        public static readonly ClassDefinition[] All = { Archer, Paladin, Fencer, Monk };

        /// <summary>Looks a class up by its id (e.g. from a save file). Null if unknown.</summary>
        public static ClassDefinition Find(string id) => Array.Find(All, definition => definition.Id == id);
    }
}

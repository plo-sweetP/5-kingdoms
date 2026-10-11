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
    /// its "Every tier" stat bumps (in tenths of a percent a tier, so 4 is +10% at tier 25; never SPD). Tier 5 is
    /// written for the Archer, the Paladin and the Fencer, which stop at tier 9; the Monk stops at tier 4.
    /// </summary>
    public static class ClassCatalog
    {
        /// <summary>
        /// Uzuki's class: ranged physical damage, traps and control. A light, quick class. Its paths: the Marksman
        /// (one target), the Hunter (traps and control) and the Trickshot (several targets).
        /// </summary>
        public static readonly ClassDefinition Archer = new ClassDefinition("archer", "Archer", WeaponFamily.Bow, speedModifier: 5,
            paths: new[] { "Marksman", "Hunter", "Trickshot" },
            bumps: new[] { new StatBump(StatKind.Atk, 4), new StatBump(StatKind.CritRate, 2) },
            milestones: new[]
            {
                new ClassMilestone(5,
                    // Peter, 2026-10-10: "mark on the foe he shoots. and yes, always on skill. basically similar to
                    // passives". The first always-on skill in a loadout (SkillDefinition.AlwaysOn).
                    new ClassOption("archer_deadly_mark", "Deadly Mark", 0,
                        "Hunter's Mark becomes always on: no button and no time. The hero's shots mark the foe they are aimed at, and each further shot on it adds 10% to the hero's damage on it, up to 40%. A shot at another foe moves the mark, and it starts over.",
                        upgrades: SkillCatalog.HuntersMark, change: skill =>
                        {
                            skill.AlwaysOn = true;
                            skill.Power = 0;
                            skill.BuildPercent = 10;
                            skill.MaxPower = 40;
                        }),
                    new ClassOption("archer_crippling_shot", "Crippling Shot", 1,
                        "A new skill: a shot that slows what it hits, so its next turn comes later.",
                        teaches: SkillCatalog.CripplingShot),
                    new ClassOption("archer_bouncing_shot", "Bouncing Shot", 2,
                        "A new skill: one arrow that bounces on from foe to foe, a little weaker each time.",
                        teaches: SkillCatalog.BouncingShot)),
            });

        /// <summary>
        /// Haiden's class: a holy warrior in armor, protection and some healing. A heavy, slower class. Its paths: the
        /// Guardian (protection), Devotion (healing) and the Crusader (damage).
        /// </summary>
        public static readonly ClassDefinition Paladin = new ClassDefinition("paladin", "Paladin", WeaponFamily.Sword, speedModifier: -5,
            paths: new[] { "Guardian", "Devotion", "Crusader" },
            bumps: new[] { new StatBump(StatKind.Hp, 4), new StatBump(StatKind.Def, 4) },
            milestones: new[]
            {
                new ClassMilestone(5,
                    // Peter, 2026-10-10: Challenge is to be worth about as much as Greater Heal. The draft (a 2-turn
                    // taunt) was worth 2 points of a level-5 party's 38% and no number on the blow changed that:
                    // the Troll goes for the tank anyway. A taunt of 4 turns covers the Troll's whole slam cycle,
                    // so it stays on him while he steps out and back (nobody else is hit meanwhile), and the cut
                    // on a challenged foe's hits is the Guardian's own protection: 43% of runs won without the cut,
                    // 48% with 15%, 54% with 20%.
                    new ClassOption("paladin_challenge", "Challenge", 0,
                        "Shoulder Bash becomes a challenge to everyone near: it also taunts every other foe next to the hero, the taunt lasts 4 turns (was 1), and a foe under it does 15% less damage to the hero.",
                        upgrades: SkillCatalog.ShoulderBash, change: skill =>
                        {
                            skill.StatusAround = true;
                            skill.StatusTurns = 4;
                            skill.StatusPower = 15;
                        }),
                    // The draft's 30% was a must-pick: with the balance report's players a level-5 party won 62% of its
                    // runs with it, 38% without, 40% with either of the other two. Every 1% of heal is worth over two
                    // points there; 24% (47%) is in line with the other classes' best options.
                    new ClassOption("paladin_greater_heal", "Greater Heal", 1,
                        "Heal mends more: 24% of the hero's max HP (was 20%).",
                        upgrades: SkillCatalog.PaladinHeal, change: skill => skill.Power = 24),
                    new ClassOption("paladin_searing_smite", "Searing Smite", 2,
                        "Divine Strike burns hotter: it hits for 320% ATK (was 250%).",
                        upgrades: SkillCatalog.DivineStrike, change: skill => skill.Power = 320)),
            });

        /// <summary>
        /// Kristela's class since 2026-10-05 (Peter: "I'm not feeling the monk abilities for her"): a light blade,
        /// flurries, lunges and counters. A quick class, like the Monk she was until then.
        /// </summary>
        public static readonly ClassDefinition Fencer = new ClassDefinition("fencer", "Fencer", WeaponFamily.Sword, speedModifier: 5,
            paths: new[] { "Duelist", "Footwork", "En Garde" },
            bumps: new[] { new StatBump(StatKind.CritRate, 2), new StatBump(StatKind.CritDmg, 4) },
            milestones: new[]
            {
                // Peter, 2026-10-10: "fencer options are fine as drafted". All three upgrade her starting kit.
                new ClassMilestone(5,
                    new ClassOption("fencer_precise_thrusts", "Precise Thrusts", 0,
                        "Triple Thrust finds the gaps: each of its hits has +15% Crit Rate, and the third hits for 130% ATK (was 90%).",
                        upgrades: SkillCatalog.TripleThrust, change: skill =>
                        {
                            skill.CritRateBonus = 150;
                            skill.LastHitPower = 130;
                        }),
                    new ClassOption("fencer_long_lunge", "Long Lunge", 1,
                        "Lunge carries further and hits harder: a foe up to 4 tiles away (was 3), for 240% ATK (was 200%).",
                        upgrades: SkillCatalog.Lunge, change: skill =>
                        {
                            skill.DashTiles = 3;
                            skill.Power = 240;
                        }),
                    new ClassOption("fencer_sharp_riposte", "Sharp Riposte", 2,
                        "Riposte answers harder and turns more aside: the counter hits for 320% ATK (was 250%), and the stance takes 60% off a hit (was 50%), 30% off a boss's (was 25%).",
                        upgrades: SkillCatalog.Riposte, change: skill =>
                        {
                            skill.Power = 320;
                            skill.StatusPower = 60;
                            skill.BossStatusPower = 30;
                        })),
            });

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

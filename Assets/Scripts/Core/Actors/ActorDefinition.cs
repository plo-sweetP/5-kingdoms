using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>Which AI an actor uses. Bosses get their own brains.</summary>
    public enum ActorBrain { Chaser, SlimeKing }

    /// <summary>
    /// Base stats for a character or monster species at level 1, and what each level adds. Monsters spawn at the floor's
    /// level, so their growth is what makes deeper floors tougher. Will move to data assets once there are more than a
    /// handful.
    /// </summary>
    public sealed class ActorDefinition
    {
        static readonly SkillDefinition[] NoSkills = Array.Empty<SkillDefinition>();

        public ActorDefinition(string id, string name, int maxHp, int attack, int defense, int expReward,
            ActorBrain brain = ActorBrain.Chaser, int speed = DefaultSpeed, int maxMp = 0, IReadOnlyList<SkillDefinition> skills = null,
            int hpGrowth = 0, int atkGrowth = 0, int defGrowth = 0, int mpGrowth = 0, int expGrowth = 0,
            int critRate = BaseCritRate, int critDmg = BaseCritDmg, WeaponDefinition weapon = null)
        {
            Id = id;
            Name = name;
            MaxHp = maxHp;
            Attack = attack;
            Defense = defense;
            ExpReward = expReward;
            Brain = brain;
            Speed = speed;
            MaxMp = maxMp;
            Skills = skills ?? NoSkills;
            HpGrowth = hpGrowth;
            AtkGrowth = atkGrowth;
            DefGrowth = defGrowth;
            MpGrowth = mpGrowth;
            ExpGrowth = expGrowth;
            CritRate = critRate;
            CritDmg = critDmg;
            Weapon = weapon;
        }

        /// <summary>The hero's baseline. Keep species within roughly 80-130 so turn order stays readable.</summary>
        public const int DefaultSpeed = 100;

        /// <summary>Everyone's crit before gear: 5% Crit Rate for +50% damage (Honkai: Star Rail), in tenths of a percent.</summary>
        public const int BaseCritRate = 50;
        public const int BaseCritDmg = 500;

        /// <summary>Stable key, used for saves and to find the actor's sprite.</summary>
        public string Id { get; }
        public string Name { get; }

        /// <summary>Stats at level 1.</summary>
        public int MaxHp { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int ExpReward { get; }
        public ActorBrain Brain { get; }

        /// <summary>Combat speed: one turn every 10000 / Speed AV. Never randomized, and levels never raise it.</summary>
        public int Speed { get; }

        /// <summary>Mana at level 1; 0 for actors without mana.</summary>
        public int MaxMp { get; }

        /// <summary>Up to three skills, in button order (the ultimate comes later).</summary>
        public IReadOnlyList<SkillDefinition> Skills { get; }

        /// <summary>What each level after the first adds.</summary>
        public int HpGrowth { get; }
        public int AtkGrowth { get; }
        public int DefGrowth { get; }
        public int MpGrowth { get; }
        public int ExpGrowth { get; }

        /// <summary>In tenths of a percent.</summary>
        public int CritRate { get; }
        public int CritDmg { get; }

        /// <summary>The weapon it fights with until gear exists, or null.</summary>
        public WeaponDefinition Weapon { get; }

        public bool IsBoss => Brain != ActorBrain.Chaser;
    }

    /// <summary>
    /// Everyone's stats, on GEAR.md's 10x scale (Uzuki 400 HP / 60 ATK / 30 DEF): big enough numbers that % bonuses
    /// and a 100-level climb stay readable.
    /// </summary>
    public static class ActorCatalog
    {
        public static readonly ActorDefinition Uzuki = new ActorDefinition("uzuki", "Uzuki", maxHp: 400, attack: 60, defense: 30, expReward: 0,
            maxMp: 30, skills: new[] { SkillCatalog.SpiritStrike, SkillCatalog.SecondWind, SkillCatalog.Dash },
            hpGrowth: 50, atkGrowth: 10, defGrowth: 10, mpGrowth: 2);

        /// <summary>
        /// ATK 40 bites a level-1 hero for what it did before the multiplicative formula (70); a gentle +6 per floor
        /// lets a leveled hero outgrow the cave, since DEF alone no longer shrugs weak hits off.
        /// </summary>
        public static readonly ActorDefinition Slime = new ActorDefinition("slime", "Slime", maxHp: 140, attack: 40, defense: 10, expReward: 6,
            hpGrowth: 30, atkGrowth: 6, defGrowth: 5, expGrowth: 2);

        /// <summary>
        /// Slow and heavy (Speed 85): the hero sometimes gets two turns before it acts, e.g. to escape a slam. Tuned with
        /// -balance: a fresh level-1 hero rarely wins (about 4%); with levels kept, players win around Lv 9 on the
        /// 2nd-3rd attempt. The long fight (1300 HP) is what favors a leveled hero's HP and mana.
        /// </summary>
        public static readonly ActorDefinition KingSlime = new ActorDefinition("king_slime", "King Slime", maxHp: 1300, attack: 80, defense: 50,
            expReward: 80, brain: ActorBrain.SlimeKing, speed: 85);

        static readonly ActorDefinition[] All = { Uzuki, Slime, KingSlime };

        /// <summary>Looks a definition up by its Id (e.g. from a save file). Null if unknown.</summary>
        public static ActorDefinition Find(string id) => Array.Find(All, definition => definition.Id == id);
    }
}

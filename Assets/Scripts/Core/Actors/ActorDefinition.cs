using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>Which AI an actor uses. Bosses get their own brains.</summary>
    public enum ActorBrain { Chaser, Troll }

    /// <summary>
    /// Base stats for a character or monster species at level 1, and what each level adds. Monsters spawn at the floor's
    /// level, so their growth is what makes deeper floors tougher. Will move to data assets once there are more than a
    /// handful.
    /// </summary>
    public sealed class ActorDefinition
    {
        static readonly SkillDefinition[] NoSkills = Array.Empty<SkillDefinition>();

        public ActorDefinition(string id, string name, int maxHp, int attack, int defense, int expReward,
            ActorBrain brain = ActorBrain.Chaser, int speed = DefaultSpeed, IReadOnlyList<SkillDefinition> skills = null,
            SkillDefinition ultimate = null, int hpGrowth = 0, int atkGrowth = 0, int defGrowth = 0, int expGrowth = 0,
            int critRate = BaseCritRate, int critDmg = BaseCritDmg, WeaponDefinition weapon = null, int attackRange = 1)
        {
            AttackRange = attackRange;
            Id = id;
            Name = name;
            MaxHp = maxHp;
            Attack = attack;
            Defense = defense;
            ExpReward = expReward;
            Brain = brain;
            Speed = speed;
            Skills = skills ?? NoSkills;
            Ultimate = ultimate;
            HpGrowth = hpGrowth;
            AtkGrowth = atkGrowth;
            DefGrowth = defGrowth;
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

        /// <summary>Up to three skills, in button order.</summary>
        public IReadOnlyList<SkillDefinition> Skills { get; }

        /// <summary>Used when the charge meter is full (<see cref="Actor.Charge"/>); null for monsters.</summary>
        public SkillDefinition Ultimate { get; }

        /// <summary>What each level after the first adds.</summary>
        public int HpGrowth { get; }
        public int AtkGrowth { get; }
        public int DefGrowth { get; }
        public int ExpGrowth { get; }

        /// <summary>In tenths of a percent.</summary>
        public int CritRate { get; }
        public int CritDmg { get; }

        /// <summary>The weapon it fights with until gear exists, or null.</summary>
        public WeaponDefinition Weapon { get; }

        /// <summary>How far its weapon attack reaches: 1 is melee, more is a shot at any foe in sight within that many tiles.</summary>
        public int AttackRange { get; }

        /// <summary>The always-ready weapon attack's name (Uzuki's Quick Shot, Haiden's Sword Slash, Kristela's Jab).</summary>
        public string AttackName => Weapon?.AttackName ?? "Attack";

        /// <summary>Fights from a distance (its weapon attack is a shot): hangs back in a fight and never swaps forward.</summary>
        public bool IsRanged => AttackRange > 1;

        public bool IsBoss => Brain != ActorBrain.Chaser;
    }

    /// <summary>
    /// Everyone's stats, on GEAR.md's 10x scale (Uzuki 400 HP / 60 ATK / 30 DEF): big enough numbers that % bonuses
    /// and a 100-level climb stay readable.
    /// </summary>
    public static class ActorCatalog
    {
        /// <summary>
        /// Archer (Medieval Realm), utility ranged DPS built around traps: his weapon attack (Quick Shot) reaches any foe in
        /// sight within 5 tiles; Hunter's Mark, Power Shot and Rolling Shot (which leaves a snare); ultimate Volley. The
        /// Hunter Bow's +6 SPD takes his base 95 to 101.
        /// </summary>
        public static readonly ActorDefinition Uzuki = new ActorDefinition("uzuki", "Uzuki", maxHp: 400, attack: 60, defense: 30, expReward: 0,
            speed: 95, skills: new[] { SkillCatalog.HuntersMark, SkillCatalog.PowerShot, SkillCatalog.RollingShot },
            ultimate: SkillCatalog.Volley, hpGrowth: 50, atkGrowth: 10, defGrowth: 10, weapon: WeaponCatalog.HunterBow,
            attackRange: SkillCatalog.RangedReach);

        /// <summary>
        /// Paladin (Dynasty Nation), a tank first with some healing: the most HP and DEF, the slowest. Heal, Divine Strike,
        /// Shoulder Bash; ultimate Aura of Protection. He trains Rune Warrior later, toward Runegod Fire Blade.
        /// </summary>
        public static readonly ActorDefinition Haiden = new ActorDefinition("haiden", "Haiden", maxHp: 520, attack: 55, defense: 45, expReward: 0,
            speed: 90, skills: new[] { SkillCatalog.PaladinHeal, SkillCatalog.DivineStrike, SkillCatalog.ShoulderBash },
            ultimate: SkillCatalog.AuraOfProtection, hpGrowth: 65, atkGrowth: 9, defGrowth: 14, weapon: WeaponCatalog.LongSword);

        /// <summary>
        /// Monk (Medieval Realm, a princess), speed-build melee DPS: the most ATK, the least HP and DEF. Piercing Punch, Ki
        /// Heal, Stun Strike; ultimate Flurry of Blows.
        /// </summary>
        public static readonly ActorDefinition Kristela = new ActorDefinition("kristela", "Kristela", maxHp: 380, attack: 64, defense: 26, expReward: 0,
            speed: 100, skills: new[] { SkillCatalog.PiercingPunch, SkillCatalog.KiHeal, SkillCatalog.StunStrike },
            ultimate: SkillCatalog.FlurryOfBlows, hpGrowth: 45, atkGrowth: 11, defGrowth: 8, weapon: WeaponCatalog.Gauntlets);

        /// <summary>
        /// The party for the first playtest, leader first (the player's own character joins later). A melee hero leads
        /// (PROGRESSION.md, "Targeting and input"): Haiden the tank in front, Kristela behind him, Uzuki at the back. The
        /// player can switch to anyone, and saves don't depend on the order.
        /// </summary>
        public static readonly ActorDefinition[] StartingParty = { Haiden, Kristela, Uzuki };

        /// <summary>
        /// Tuned for a party of three (with -balance), so a spider alone is no threat but a roomful is. Little EXP: every
        /// hero standing gets it in full, and dungeon EXP is meant to be a bonus on top of EXP books (PROGRESSION.md).
        /// </summary>
        public static readonly ActorDefinition Spider = new ActorDefinition("spider", "Spider", maxHp: 330, attack: 66, defense: 10, expReward: 3,
            hpGrowth: 66, atkGrowth: 10, defGrowth: 5, expGrowth: 1);

        /// <summary>
        /// Slow and heavy (Speed 85): a hero sometimes gets two turns before it acts, e.g. to escape a slam. Tuned with
        /// -balance for the party of three: a fresh level-1 party rarely wins (about 6%); with levels kept, players win
        /// around Lv 9 on the 2nd-3rd attempt. Hitting hard (ATK 260) is what makes levels count: a low-level party's
        /// thinner HP and DEF suffer most. 16000 HP (10000 in 1f part 1, when ranged hits dealt 75% and only flew along the
        /// 8 lines, and before Aura of Protection covered Haiden himself): more HP is what holds a low-level party back
        /// without stopping a Lv 9 one.
        /// </summary>
        public static readonly ActorDefinition Troll = new ActorDefinition("troll", "Troll", maxHp: 16000, attack: 260, defense: 50,
            expReward: 80, brain: ActorBrain.Troll, speed: 85);

        /// <summary>
        /// Fast and frail (Speed 130, the top of the readable range): it closes distance quickly, so a ranged hero can't
        /// just keep backing away. Some packs bring one (PROGRESSION.md, "Bigger fights").
        /// </summary>
        public static readonly ActorDefinition Bat = new ActorDefinition("bat", "Giant Bat", maxHp: 200, attack: 55, defense: 5, expReward: 2,
            speed: 130, hpGrowth: 40, atkGrowth: 8, defGrowth: 3, expGrowth: 1);

        static readonly ActorDefinition[] All = { Uzuki, Haiden, Kristela, Spider, Troll, Bat };

        /// <summary>Looks a definition up by its Id (e.g. from a save file). Null if unknown.</summary>
        public static ActorDefinition Find(string id) => Array.Find(All, definition => definition.Id == id);
    }
}

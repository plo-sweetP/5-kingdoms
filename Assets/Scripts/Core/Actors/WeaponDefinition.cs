using System;

namespace FiveKingdoms.Core
{
    /// <summary>Weapon passives (GEAR.md). Only the Hunter Bow's exists so far; the others come with the gear milestone.</summary>
    public enum WeaponPassive
    {
        None,

        /// <summary>Ranged physical skills fire 2 arrows at <see cref="WeaponDefinition.PassivePower"/>% damage each.</summary>
        Multishot,
    }

    /// <summary>Weapon types; classes and kits will key off them (the Monk fights with gauntlets).</summary>
    public enum WeaponType { Bow, LongSword, Gauntlets }

    /// <summary>
    /// What a skill can be tied to (PROGRESSION.md, "Classes"): a bow, a sword, fists, or nothing. A skill tied to a
    /// family only goes in the loadout while the hero holds a weapon of that family. GEAR.md's other weapons join these
    /// families (or add one) when gear arrives.
    /// </summary>
    public enum WeaponFamily { None, Bow, Sword, Fists }

    /// <summary>
    /// A weapon (GEAR.md): flat HP, ATK and a little DEF that count as base stats, so % bonuses scale them, plus on a few
    /// weapons some SPD and a passive. Flat stats grow with the item's level (1-100); SPD doesn't.
    /// </summary>
    public sealed class WeaponDefinition
    {
        /// <summary>The most SPD any weapon may give (the Hunter Bow's +6); the speed budget depends on it.</summary>
        public const int MaxSpeed = 6;

        public WeaponDefinition(string id, string name, WeaponType type, string attackName, int hp, int atk, int def,
            int hpPerLevel = 0, int atkPerLevel = 0, int defPerLevel = 0, int spd = 0,
            WeaponPassive passive = WeaponPassive.None, int passivePower = 0)
        {
            Id = id;
            Name = name;
            Type = type;
            AttackName = attackName;
            Hp = hp;
            Atk = atk;
            Def = def;
            HpPerLevel = hpPerLevel;
            AtkPerLevel = atkPerLevel;
            DefPerLevel = defPerLevel;
            Spd = spd;
            Passive = passive;
            PassivePower = passivePower;
        }

        /// <summary>Stable key, used for saves.</summary>
        public string Id { get; }
        public string Name { get; }
        public WeaponType Type { get; }

        /// <summary>Which skills it lets its bearer use.</summary>
        public WeaponFamily Family =>
            Type == WeaponType.Bow ? WeaponFamily.Bow : Type == WeaponType.LongSword ? WeaponFamily.Sword : WeaponFamily.Fists;

        /// <summary>What its always-ready weapon attack is called (names are drafts).</summary>
        public string AttackName { get; }

        /// <summary>Flat stats at item level 1, and what each item level adds.</summary>
        public int Hp { get; }
        public int Atk { get; }
        public int Def { get; }
        public int HpPerLevel { get; }
        public int AtkPerLevel { get; }
        public int DefPerLevel { get; }

        public int Spd { get; }
        public WeaponPassive Passive { get; }

        /// <summary>The passive's strength (Multishot: damage % per arrow).</summary>
        public int PassivePower { get; }

        public int HpAt(int itemLevel) => Hp + (itemLevel - 1) * HpPerLevel;
        public int AtkAt(int itemLevel) => Atk + (itemLevel - 1) * AtkPerLevel;
        public int DefAt(int itemLevel) => Def + (itemLevel - 1) * DefPerLevel;
    }

    /// <summary>
    /// The starting party's weapons (GEAR.md, PROGRESSION.md), sized like GEAR.md asks: ATK about a third of a same-level
    /// hero's base ATK, HP and DEF about a fifth, growing with item level to stay that size. The Long Sword and Gauntlets
    /// have no passive until the gear milestone.
    /// </summary>
    public static class WeaponCatalog
    {
        /// <summary>Uzuki's: Multishot (2 arrows at 60% on ranged physical skills) and +6 SPD, the most any weapon gives.</summary>
        public static readonly WeaponDefinition HunterBow = new WeaponDefinition("hunter_bow", "Hunter Bow", WeaponType.Bow, "Quick Shot",
            hp: 60, atk: 20, def: 4, hpPerLevel: 8, atkPerLevel: 3, defPerLevel: 1, spd: 6,
            passive: WeaponPassive.Multishot, passivePower: 60);

        /// <summary>Haiden's: sturdier, for a frontliner.</summary>
        public static readonly WeaponDefinition LongSword = new WeaponDefinition("long_sword", "Long Sword", WeaponType.LongSword, "Sword Slash",
            hp: 90, atk: 20, def: 8, hpPerLevel: 12, atkPerLevel: 3, defPerLevel: 2);

        /// <summary>Kristela's: the Monk and fist-fighter weapon type.</summary>
        public static readonly WeaponDefinition Gauntlets = new WeaponDefinition("gauntlets", "Gauntlets", WeaponType.Gauntlets, "Jab",
            hp: 60, atk: 22, def: 4, hpPerLevel: 8, atkPerLevel: 3, defPerLevel: 1);

        public static readonly WeaponDefinition[] All = { HunterBow, LongSword, Gauntlets };

        public static WeaponDefinition Find(string id) => Array.Find(All, weapon => weapon.Id == id);
    }
}

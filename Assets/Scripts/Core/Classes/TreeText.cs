using System.Collections.Generic;
using System.Globalization;

namespace FiveKingdoms.Core
{
    /// <summary>
    /// The skill-tree screen's words about classes, tiers and options (PROGRESSION.md, "Building 1g", step 6), written
    /// from the rules' own numbers like <see cref="SkillText"/>, so the screen never says something the rules don't do.
    /// </summary>
    public static class TreeText
    {
        /// <summary>"+0.4% ATK, +0.2% Crit Rate": what <paramref name="tiers"/> tiers of a class add. Empty for a class without bumps.</summary>
        public static string Bumps(ClassDefinition definition, int tiers = 1)
        {
            var parts = new List<string>();
            foreach (var bump in definition.Bumps) parts.Add($"+{Tenths(bump.PerTier * tiers)}% {StatName(bump.Stat)}");
            return string.Join(", ", parts);
        }

        /// <summary>Tenths of a percent as a number: 4 is "0.4", 100 is "10".</summary>
        public static string Tenths(int tenths) => (tenths / 10m).ToString("0.#", CultureInfo.InvariantCulture);

        public static string StatName(StatKind stat)
        {
            switch (stat)
            {
                case StatKind.Hp: return "HP";
                case StatKind.Atk: return "ATK";
                case StatKind.Def: return "DEF";
                case StatKind.Spd: return "SPD";
                case StatKind.CritRate: return "Crit Rate";
                case StatKind.CritDmg: return "Crit DMG";
                default: return stat.ToString();
            }
        }

        /// <summary>"a bow", "a sword", "fists"; null for no weapon.</summary>
        public static string WeaponName(WeaponFamily family)
        {
            switch (family)
            {
                case WeaponFamily.Bow: return "a bow";
                case WeaponFamily.Sword: return "a sword";
                case WeaponFamily.Fists: return "fists";
                default: return null;
            }
        }

        /// <summary>"+5", "-5", "+0".</summary>
        public static string Signed(int value) => value < 0 ? value.ToString(CultureInfo.InvariantCulture) : "+" + value.ToString(CultureInfo.InvariantCulture);

        /// <summary>A skill's tags for the line under its name: "Quick", "needs a bow", "ultimate".</summary>
        public static string Tags(SkillDefinition skill)
        {
            var parts = new List<string>();
            if (skill.IsUltimate) parts.Add("ultimate");
            if (skill.IsQuick) parts.Add("Quick");
            string weapon = WeaponName(skill.Weapon);
            if (weapon != null) parts.Add("needs " + weapon);
            return string.Join(", ", parts);
        }

        /// <summary>Why a skill can't go in a loadout slot, for the screen.</summary>
        public static string Why(EquipCheck check, SkillDefinition skill, HeroProgress hero)
        {
            switch (check)
            {
                case EquipCheck.WrongWeapon:
                    string held = WeaponName(hero.Definition.Weapon?.Family ?? WeaponFamily.None);
                    return $"{skill.Name} needs {WeaponName(skill.Weapon)}: {hero.Definition.Name} holds {held ?? "no weapon"}.";
                case EquipCheck.TooManyQuick: return "A loadout holds one Quick skill: take the other one out first.";
                case EquipCheck.NotKnown: return $"{hero.Definition.Name} hasn't learned {skill.Name}.";
                case EquipCheck.WrongSlot: return skill.IsUltimate ? "An ultimate goes in the ultimate's slot." : "Only an ultimate goes in this slot.";
                default: return "";
            }
        }
    }
}

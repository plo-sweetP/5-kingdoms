namespace FiveKingdoms.Core
{
    /// <summary>What a skill does when used.</summary>
    public enum SkillEffect
    {
        /// <summary>A hit on the faced enemy (or any adjacent one); Power is the damage percent of a basic attack.</summary>
        Strike,

        /// <summary>Heals the user; Power is the percent of max HP restored.</summary>
        Heal,

        /// <summary>Moves the user up to Power tiles straight ahead in one quick motion.</summary>
        Dash,
    }

    /// <summary>
    /// One of a character's three skills. Skills either spend mana, build mana, or cost no mana and lean on a
    /// cooldown and a unique effect. Each also has its own action-value cost: a quick skill brings the user's next
    /// turn sooner, a heavy one pushes it back.
    /// </summary>
    public sealed class SkillDefinition
    {
        public SkillDefinition(string id, string name, string shortName, SkillEffect effect, int power,
            int manaCost = 0, int manaGain = 0, int costPercent = 100, int cooldown = 0)
        {
            Id = id;
            Name = name;
            ShortName = shortName;
            Effect = effect;
            Power = power;
            ManaCost = manaCost;
            ManaGain = manaGain;
            CostPercent = costPercent;
            Cooldown = cooldown;
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>Fits on a skill button.</summary>
        public string ShortName { get; }

        public SkillEffect Effect { get; }

        /// <summary>Damage percent (Strike), heal percent of max HP (Heal) or tiles (Dash).</summary>
        public int Power { get; }

        public int ManaCost { get; }
        public int ManaGain { get; }

        /// <summary>Action-value cost in percent of a normal turn.</summary>
        public int CostPercent { get; }

        /// <summary>The user's own turns before it can be used again.</summary>
        public int Cooldown { get; }
    }

    public static class SkillCatalog
    {
        /// <summary>Builds mana: a solid hit that restores MP, but slow (125% of a turn).</summary>
        public static readonly SkillDefinition SpiritStrike = new SkillDefinition("spirit_strike", "Spirit Strike", "Strike",
            SkillEffect.Strike, power: 120, manaGain: 8, costPercent: 125);

        /// <summary>Spends mana: the main way to heal in a dungeon.</summary>
        public static readonly SkillDefinition SecondWind = new SkillDefinition("second_wind", "Second Wind", "Heal",
            SkillEffect.Heal, power: 50, manaCost: 15);

        /// <summary>No mana, a unique effect: three tiles in half a turn, then a cooldown. Good for escaping a slam.</summary>
        public static readonly SkillDefinition Dash = new SkillDefinition("dash", "Dash", "Dash",
            SkillEffect.Dash, power: 3, costPercent: 50, cooldown: 4);
    }
}

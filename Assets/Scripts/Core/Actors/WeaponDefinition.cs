namespace FiveKingdoms.Core
{
    /// <summary>
    /// A weapon's stats (GEAR.md): flat HP, ATK and a little DEF that count as base stats, so % bonuses scale them, and
    /// on a few weapons some SPD. Passives, weapon levels and item level come with the gear milestone.
    /// </summary>
    public sealed class WeaponDefinition
    {
        /// <summary>The most SPD any weapon may give (the Hunter Bow's +6); the speed budget depends on it.</summary>
        public const int MaxSpeed = 6;

        public WeaponDefinition(string id, string name, int hp, int atk, int def, int spd = 0)
        {
            Id = id;
            Name = name;
            Hp = hp;
            Atk = atk;
            Def = def;
            Spd = spd;
        }

        /// <summary>Stable key, used for saves.</summary>
        public string Id { get; }
        public string Name { get; }
        public int Hp { get; }
        public int Atk { get; }
        public int Def { get; }
        public int Spd { get; }
    }
}

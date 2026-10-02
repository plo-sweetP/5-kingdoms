namespace FiveKingdoms.Core
{
    /// <summary>Base stats for a character or monster species. Will move to data assets once there are more than a handful.</summary>
    public sealed class ActorDefinition
    {
        public ActorDefinition(string id, string name, int maxHp, int attack, int defense, int expReward)
        {
            Id = id;
            Name = name;
            MaxHp = maxHp;
            Attack = attack;
            Defense = defense;
            ExpReward = expReward;
        }

        /// <summary>Stable key, also used to find the actor's sprite.</summary>
        public string Id { get; }
        public string Name { get; }
        public int MaxHp { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int ExpReward { get; }
    }

    public static class ActorCatalog
    {
        public static readonly ActorDefinition Uzuki = new ActorDefinition("uzuki", "Uzuki", maxHp: 40, attack: 6, defense: 3, expReward: 0);
        public static readonly ActorDefinition Slime = new ActorDefinition("slime", "Slime", maxHp: 14, attack: 5, defense: 1, expReward: 6);
    }
}

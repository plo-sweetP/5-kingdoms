using System;

namespace FiveKingdoms.Core
{
    /// <summary>Which AI an actor uses. Bosses get their own brains.</summary>
    public enum ActorBrain { Chaser, SlimeKing }

    /// <summary>Base stats for a character or monster species. Will move to data assets once there are more than a handful.</summary>
    public sealed class ActorDefinition
    {
        public ActorDefinition(string id, string name, int maxHp, int attack, int defense, int expReward,
            ActorBrain brain = ActorBrain.Chaser, int speed = DefaultSpeed)
        {
            Id = id;
            Name = name;
            MaxHp = maxHp;
            Attack = attack;
            Defense = defense;
            ExpReward = expReward;
            Brain = brain;
            Speed = speed;
        }

        /// <summary>The hero's baseline. Keep species within roughly 80-130 so turn order stays readable.</summary>
        public const int DefaultSpeed = 100;

        /// <summary>Stable key, used for saves and to find the actor's sprite.</summary>
        public string Id { get; }
        public string Name { get; }
        public int MaxHp { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int ExpReward { get; }
        public ActorBrain Brain { get; }

        /// <summary>Combat speed: one turn every 10000 / Speed AV. Never randomized.</summary>
        public int Speed { get; }

        public bool IsBoss => Brain != ActorBrain.Chaser;
    }

    public static class ActorCatalog
    {
        public static readonly ActorDefinition Uzuki = new ActorDefinition("uzuki", "Uzuki", maxHp: 40, attack: 6, defense: 3, expReward: 0);
        public static readonly ActorDefinition Slime = new ActorDefinition("slime", "Slime", maxHp: 14, attack: 5, defense: 1, expReward: 6);
        /// <summary>Slow and heavy (Speed 85): the hero sometimes gets two turns before it acts, e.g. to escape a slam.</summary>
        public static readonly ActorDefinition KingSlime = new ActorDefinition("king_slime", "King Slime", maxHp: 110, attack: 11, defense: 5,
            expReward: 80, brain: ActorBrain.SlimeKing, speed: 85);

        static readonly ActorDefinition[] All = { Uzuki, Slime, KingSlime };

        /// <summary>Looks a definition up by its Id (e.g. from a save file). Null if unknown.</summary>
        public static ActorDefinition Find(string id) => Array.Find(All, definition => definition.Id == id);
    }
}

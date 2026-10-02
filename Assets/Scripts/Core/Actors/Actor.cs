namespace FiveKingdoms.Core
{
    public enum Team { Hero, Enemy }

    /// <summary>A character or monster standing in the dungeon, with its current stats.</summary>
    public sealed class Actor
    {
        public Actor(int id, ActorDefinition definition, Team team, GridPos pos)
        {
            Id = id;
            Definition = definition;
            Team = team;
            Pos = pos;
            MaxHp = Hp = definition.MaxHp;
            Attack = definition.Attack;
            Defense = definition.Defense;
            ExpReward = definition.ExpReward;
        }

        public int Id { get; }
        public ActorDefinition Definition { get; }
        public string Name => Definition.Name;
        public Team Team { get; }
        public GridPos Pos { get; set; }
        public Direction8 Facing { get; set; } = Direction8.S;

        public int Level { get; set; } = 1;
        public int Exp { get; set; }
        public int MaxHp { get; set; }
        public int Hp { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int ExpReward { get; set; }

        /// <summary>Enemy AI state: has noticed the hero and is giving chase.</summary>
        public bool Alerted { get; set; }

        public bool IsAlive => Hp > 0;

        public override string ToString() => $"{Name}#{Id} {Pos} HP {Hp}/{MaxHp}";
    }
}

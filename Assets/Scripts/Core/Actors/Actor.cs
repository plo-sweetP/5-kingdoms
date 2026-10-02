namespace FiveKingdoms.Core
{
    public enum Team { Hero, Enemy }

    /// <summary>A character or monster standing in the dungeon, with its current stats.</summary>
    public sealed class Actor
    {
        public Actor(int id, ActorDefinition definition, Team team, GridPos pos, int level = 1)
        {
            Id = id;
            Definition = definition;
            Team = team;
            Pos = pos;
            MaxHp = definition.MaxHp;
            Attack = definition.Attack;
            Defense = definition.Defense;
            ExpReward = definition.ExpReward;
            Speed = definition.Speed;
            for (int i = 1; i < level; i++) CombatRules.ApplyLevelUp(this); // Same growth as leveling up in a run.
            Hp = MaxHp;
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

        /// <summary>
        /// Combat speed: one turn every 10000 / Speed AV. Change it mid-fight through DungeonRun.SetSpeed so the
        /// timeline keeps the actor's progress toward its next turn.
        /// </summary>
        public int Speed { get; set; }

        /// <summary>Enemy AI state: has noticed the hero and is giving chase.</summary>
        public bool Alerted { get; set; }

        /// <summary>Boss state: wound up for a special attack that lands on its next turn.</summary>
        public bool Charging { get; set; }

        /// <summary>Boss state: turns until its special attack is ready again.</summary>
        public int SpecialCooldown { get; set; }

        /// <summary>Boss state: has already called for reinforcements this fight.</summary>
        public bool CalledForHelp { get; set; }

        public bool IsAlive => Hp > 0;

        public override string ToString() => $"{Name}#{Id} {Pos} HP {Hp}/{MaxHp}";
    }
}

namespace FiveKingdoms.Core
{
    public enum HeroCommandKind { Move, Attack, Wait, UseBerry, Descend, Skill }

    /// <summary>One thing the player (or the autopilot) asks the hero to do.</summary>
    public readonly struct HeroCommand
    {
        public readonly HeroCommandKind Kind;

        /// <summary>Where to move; for an aimed skill, where to aim it.</summary>
        public readonly Direction8 Direction;

        /// <summary>Skill slot (0-2) for <see cref="HeroCommandKind.Skill"/>.</summary>
        public readonly int Slot;

        /// <summary>A skill aimed at <see cref="Direction"/>; otherwise it goes the way the hero faces.</summary>
        public readonly bool Aimed;

        HeroCommand(HeroCommandKind kind, Direction8 direction, int slot = 0, bool aimed = false)
        {
            Kind = kind;
            Direction = direction;
            Slot = slot;
            Aimed = aimed;
        }

        public static HeroCommand Move(Direction8 direction) => new HeroCommand(HeroCommandKind.Move, direction);
        public static HeroCommand Skill(int slot) => new HeroCommand(HeroCommandKind.Skill, Direction8.S, slot);
        public static HeroCommand Skill(int slot, Direction8 aim) => new HeroCommand(HeroCommandKind.Skill, aim, slot, aimed: true);
        public static readonly HeroCommand Attack = new HeroCommand(HeroCommandKind.Attack, Direction8.S);
        public static readonly HeroCommand Wait = new HeroCommand(HeroCommandKind.Wait, Direction8.S);
        public static readonly HeroCommand UseBerry = new HeroCommand(HeroCommandKind.UseBerry, Direction8.S);
        public static readonly HeroCommand Descend = new HeroCommand(HeroCommandKind.Descend, Direction8.S);

        public override string ToString() =>
            Kind == HeroCommandKind.Move ? $"Move {Direction}"
            : Kind == HeroCommandKind.Skill ? (Aimed ? $"Skill {Slot + 1} {Direction}" : $"Skill {Slot + 1}")
            : Kind.ToString();
    }
}

namespace FiveKingdoms.Core
{
    public enum HeroCommandKind { Move, Attack, Wait, UseBerry, Descend }

    /// <summary>One thing the player (or the autopilot) asks the hero to do.</summary>
    public readonly struct HeroCommand
    {
        public readonly HeroCommandKind Kind;
        public readonly Direction8 Direction;

        HeroCommand(HeroCommandKind kind, Direction8 direction)
        {
            Kind = kind;
            Direction = direction;
        }

        public static HeroCommand Move(Direction8 direction) => new HeroCommand(HeroCommandKind.Move, direction);
        public static readonly HeroCommand Attack = new HeroCommand(HeroCommandKind.Attack, Direction8.S);
        public static readonly HeroCommand Wait = new HeroCommand(HeroCommandKind.Wait, Direction8.S);
        public static readonly HeroCommand UseBerry = new HeroCommand(HeroCommandKind.UseBerry, Direction8.S);
        public static readonly HeroCommand Descend = new HeroCommand(HeroCommandKind.Descend, Direction8.S);

        public override string ToString() => Kind == HeroCommandKind.Move ? $"Move {Direction}" : Kind.ToString();
    }
}

namespace FiveKingdoms.Core
{
    public enum HeroCommandKind { Move, Attack, Wait, UseBerry, Descend, Skill, SwitchLeader, Ultimate }

    /// <summary>One thing the player (or the autopilot) asks the hero to do.</summary>
    public readonly struct HeroCommand
    {
        public readonly HeroCommandKind Kind;

        /// <summary>Where to move; for an aimed skill, where to aim it.</summary>
        public readonly Direction8 Direction;

        /// <summary>Skill slot (0-2) for <see cref="HeroCommandKind.Skill"/>; party index for <see cref="HeroCommandKind.SwitchLeader"/>.</summary>
        public readonly int Slot;

        /// <summary>A skill or attack aimed at <see cref="Direction"/>; otherwise it goes the way the hero faces.</summary>
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

        /// <summary>The hero's ultimate (needs a full charge meter), aimed or the way the hero faces.</summary>
        public static HeroCommand Ultimate(Direction8 aim) => new HeroCommand(HeroCommandKind.Ultimate, aim, aimed: true);
        public static readonly HeroCommand UltimateFacing = new HeroCommand(HeroCommandKind.Ultimate, Direction8.S);

        /// <summary>A basic attack turned toward <paramref name="aim"/> first (a ranged hero's shot along that line).</summary>
        public static HeroCommand AttackToward(Direction8 aim) => new HeroCommand(HeroCommandKind.Attack, aim, aimed: true);

        /// <summary>Take control of another party member (by party order).</summary>
        public static HeroCommand SwitchLeader(int partyIndex) => new HeroCommand(HeroCommandKind.SwitchLeader, Direction8.S, partyIndex);

        public static readonly HeroCommand Attack = new HeroCommand(HeroCommandKind.Attack, Direction8.S);
        public static readonly HeroCommand Wait = new HeroCommand(HeroCommandKind.Wait, Direction8.S);
        public static readonly HeroCommand UseBerry = new HeroCommand(HeroCommandKind.UseBerry, Direction8.S);
        public static readonly HeroCommand Descend = new HeroCommand(HeroCommandKind.Descend, Direction8.S);

        public override string ToString() =>
            Kind == HeroCommandKind.Move ? $"Move {Direction}"
            : Kind == HeroCommandKind.Skill ? (Aimed ? $"Skill {Slot + 1} {Direction}" : $"Skill {Slot + 1}")
            : Kind == HeroCommandKind.Attack && Aimed ? $"Attack {Direction}"
            : Kind == HeroCommandKind.SwitchLeader ? $"Lead {Slot + 1}"
            : Kind == HeroCommandKind.Ultimate ? (Aimed ? $"Ultimate {Direction}" : "Ultimate")
            : Kind.ToString();
    }
}

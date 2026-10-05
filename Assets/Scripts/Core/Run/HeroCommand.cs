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

        /// <summary>
        /// An attack, skill or ultimate aimed at <see cref="Direction"/> (or at <see cref="Target"/>); otherwise it goes
        /// the way the hero faces.
        /// </summary>
        public readonly bool Aimed;

        /// <summary>Aimed at the foe standing on <see cref="Target"/> (the player tapped it, or the AI picked it).</summary>
        public readonly bool Targeted;
        public readonly GridPos Target;

        /// <summary>
        /// A wait by the party's AI that holds a doorway against foes that are on their way (<see cref="HoldTheDoor"/>).
        /// The run counts these (<see cref="Actor.HeldTurns"/>), so a hero nobody comes for goes in after all.
        /// </summary>
        public readonly bool Holding;

        /// <summary>
        /// A wait by the autopilot's leader while the party tops itself up between fights (<see cref="Rest"/>). The run
        /// counts these too (<see cref="Actor.RestedTurns"/>), so it moves on when no heal comes of it.
        /// </summary>
        public readonly bool Resting;

        HeroCommand(HeroCommandKind kind, Direction8 direction, int slot = 0, bool aimed = false, bool targeted = false, GridPos target = default,
            bool holding = false, bool resting = false)
        {
            Kind = kind;
            Direction = direction;
            Slot = slot;
            Aimed = aimed || targeted;
            Targeted = targeted;
            Target = target;
            Holding = holding;
            Resting = resting;
        }

        public static HeroCommand Move(Direction8 direction) => new HeroCommand(HeroCommandKind.Move, direction);
        public static HeroCommand Skill(int slot) => new HeroCommand(HeroCommandKind.Skill, Direction8.S, slot);
        public static HeroCommand Skill(int slot, Direction8 aim) => new HeroCommand(HeroCommandKind.Skill, aim, slot, aimed: true);

        /// <summary>The hero's ultimate (needs a full charge meter), aimed or the way the hero faces.</summary>
        public static HeroCommand Ultimate(Direction8 aim) => new HeroCommand(HeroCommandKind.Ultimate, aim, aimed: true);
        public static readonly HeroCommand UltimateFacing = new HeroCommand(HeroCommandKind.Ultimate, Direction8.S);

        /// <summary>The weapon attack, a skill or the ultimate on the foe at <paramref name="target"/> (anything in reach and in sight).</summary>
        public static HeroCommand AttackAt(GridPos target) => new HeroCommand(HeroCommandKind.Attack, Direction8.S, targeted: true, target: target);
        public static HeroCommand SkillAt(int slot, GridPos target) => new HeroCommand(HeroCommandKind.Skill, Direction8.S, slot, targeted: true, target: target);
        public static HeroCommand UltimateAt(GridPos target) => new HeroCommand(HeroCommandKind.Ultimate, Direction8.S, targeted: true, target: target);

        /// <summary>The weapon attack turned toward <paramref name="aim"/> first: on the foe that way, else any in reach.</summary>
        public static HeroCommand AttackToward(Direction8 aim) => new HeroCommand(HeroCommandKind.Attack, aim, aimed: true);

        /// <summary>Take control of another party member (by party order).</summary>
        public static HeroCommand SwitchLeader(int partyIndex) => new HeroCommand(HeroCommandKind.SwitchLeader, Direction8.S, partyIndex);

        public static readonly HeroCommand Attack = new HeroCommand(HeroCommandKind.Attack, Direction8.S);
        public static readonly HeroCommand Wait = new HeroCommand(HeroCommandKind.Wait, Direction8.S);

        /// <summary>The party AI's wait at a doorway, for the foes to come to it (PROGRESSION.md, "Doorways and corridors").</summary>
        public static readonly HeroCommand HoldTheDoor = new HeroCommand(HeroCommandKind.Wait, Direction8.S, holding: true);

        /// <summary>The autopilot's wait between fights, while the party heals up (PROGRESSION.md, "Heroes heal between fights").</summary>
        public static readonly HeroCommand Rest = new HeroCommand(HeroCommandKind.Wait, Direction8.S, resting: true);
        public static readonly HeroCommand UseBerry = new HeroCommand(HeroCommandKind.UseBerry, Direction8.S);
        public static readonly HeroCommand Descend = new HeroCommand(HeroCommandKind.Descend, Direction8.S);

        public override string ToString()
        {
            string aim = Targeted ? $" @{Target}" : Aimed ? $" {Direction}" : "";
            return Kind == HeroCommandKind.Move ? $"Move {Direction}"
                : Holding ? "Hold the door"
                : Resting ? "Rest"
                : Kind == HeroCommandKind.Skill ? $"Skill {Slot + 1}{aim}"
                : Kind == HeroCommandKind.Attack || Kind == HeroCommandKind.Ultimate ? Kind + aim
                : Kind == HeroCommandKind.SwitchLeader ? $"Lead {Slot + 1}"
                : Kind.ToString();
        }
    }
}

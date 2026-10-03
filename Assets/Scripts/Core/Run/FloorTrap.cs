namespace FiveKingdoms.Core
{
    /// <summary>
    /// A trap set on a dungeon tile (Uzuki's Rolling Shot leaves a snare). It goes off when an enemy steps or is pushed
    /// onto it, then it's gone. Heroes walk over their own traps safely.
    /// </summary>
    public sealed class FloorTrap
    {
        public FloorTrap(int id, TrapKind kind, GridPos pos, int ownerId)
        {
            Id = id;
            Kind = kind;
            Pos = pos;
            OwnerId = ownerId;
        }

        public int Id { get; }
        public TrapKind Kind { get; }
        public GridPos Pos { get; }

        /// <summary>The hero who set it.</summary>
        public int OwnerId { get; }
    }
}

namespace FiveKingdoms.Core
{
    public enum ItemKind { Berry }

    /// <summary>An item lying on a dungeon tile, waiting to be picked up.</summary>
    public sealed class FloorItem
    {
        public FloorItem(int id, ItemKind kind, GridPos pos)
        {
            Id = id;
            Kind = kind;
            Pos = pos;
        }

        public int Id { get; }
        public ItemKind Kind { get; }
        public GridPos Pos { get; }
    }
}

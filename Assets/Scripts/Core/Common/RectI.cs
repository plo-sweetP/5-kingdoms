namespace FiveKingdoms.Core
{
    /// <summary>Axis-aligned rectangle of tiles. (X, Y) is the bottom-left tile; XMax/YMax are inclusive.</summary>
    public readonly struct RectI
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;

        public RectI(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int XMax => X + Width - 1;
        public int YMax => Y + Height - 1;
        public GridPos Center => new GridPos(X + Width / 2, Y + Height / 2);

        public bool Contains(GridPos p) => p.X >= X && p.X <= XMax && p.Y >= Y && p.Y <= YMax;

        /// <summary>True if the rectangles overlap or come within <paramref name="margin"/> tiles of each other.</summary>
        public bool Overlaps(RectI other, int margin) =>
            X - margin <= other.XMax && other.X <= XMax + margin &&
            Y - margin <= other.YMax && other.Y <= YMax + margin;

        public override string ToString() => $"[{X}, {Y} {Width}x{Height}]";
    }
}

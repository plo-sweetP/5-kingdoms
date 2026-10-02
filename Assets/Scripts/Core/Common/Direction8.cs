using System;

namespace FiveKingdoms.Core
{
    /// <summary>The eight movement directions, clockwise from north. Odd values are diagonals.</summary>
    public enum Direction8 { N, NE, E, SE, S, SW, W, NW }

    public static class Directions
    {
        public static readonly Direction8[] All =
        {
            Direction8.N, Direction8.NE, Direction8.E, Direction8.SE,
            Direction8.S, Direction8.SW, Direction8.W, Direction8.NW,
        };

        static readonly GridPos[] Offsets =
        {
            new GridPos(0, 1), new GridPos(1, 1), new GridPos(1, 0), new GridPos(1, -1),
            new GridPos(0, -1), new GridPos(-1, -1), new GridPos(-1, 0), new GridPos(-1, 1),
        };

        public static GridPos ToOffset(this Direction8 dir) => Offsets[(int)dir];

        public static bool IsDiagonal(this Direction8 dir) => ((int)dir & 1) == 1;

        /// <summary>The direction whose offset has the same signs as (dx, dy). False when both are zero.</summary>
        public static bool TryFromDelta(int dx, int dy, out Direction8 dir)
        {
            int sx = Math.Sign(dx), sy = Math.Sign(dy);
            for (int i = 0; i < Offsets.Length; i++)
            {
                if (Offsets[i].X == sx && Offsets[i].Y == sy)
                {
                    dir = (Direction8)i;
                    return true;
                }
            }
            dir = Direction8.S;
            return false;
        }

        /// <summary>Direction from one tile toward another (by the signs of the delta).</summary>
        public static Direction8 Toward(GridPos from, GridPos to)
        {
            TryFromDelta(to.X - from.X, to.Y - from.Y, out var dir);
            return dir;
        }
    }
}

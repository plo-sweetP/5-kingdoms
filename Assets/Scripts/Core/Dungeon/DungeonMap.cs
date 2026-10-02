using System;
using System.Collections.Generic;
using System.Text;

namespace FiveKingdoms.Core
{
    public enum TileType : byte { Wall, Floor }

    /// <summary>One dungeon floor's terrain. Coordinates are tiles with (0, 0) at the bottom-left.</summary>
    public sealed class DungeonMap
    {
        readonly TileType[] tiles;

        public DungeonMap(int width, int height)
        {
            if (width < 3 || height < 3) throw new ArgumentException("A map must be at least 3x3.");
            Width = width;
            Height = height;
            tiles = new TileType[width * height]; // Everything starts as wall.
        }

        public int Width { get; }
        public int Height { get; }
        public List<RectI> Rooms { get; } = new List<RectI>();
        public GridPos Start { get; set; }
        public GridPos Stairs { get; set; }

        public bool InBounds(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        /// <summary>Reading outside the map returns Wall, so callers never need bounds checks.</summary>
        public TileType this[GridPos p]
        {
            get => InBounds(p) ? tiles[p.Y * Width + p.X] : TileType.Wall;
            set
            {
                if (!InBounds(p)) throw new ArgumentOutOfRangeException(nameof(p), $"{p} is outside the {Width}x{Height} map.");
                tiles[p.Y * Width + p.X] = value;
            }
        }

        public TileType this[int x, int y]
        {
            get => this[new GridPos(x, y)];
            set => this[new GridPos(x, y)] = value;
        }

        public bool IsWalkable(GridPos p) => this[p] == TileType.Floor;

        /// <summary>
        /// Mystery Dungeon rule: a diagonal step or attack is blocked when either tile beside the corner is a wall.
        /// Always true for orthogonal directions.
        /// </summary>
        public bool IsCornerClear(GridPos from, Direction8 dir)
        {
            if (!dir.IsDiagonal()) return true;
            var o = dir.ToOffset();
            return IsWalkable(new GridPos(from.X + o.X, from.Y)) && IsWalkable(new GridPos(from.X, from.Y + o.Y));
        }

        /// <summary>Terrain-only check for stepping one tile in a direction (actors are not considered).</summary>
        public bool CanStep(GridPos from, Direction8 dir) => IsWalkable(from + dir.ToOffset()) && IsCornerClear(from, dir);

        /// <summary>Index into <see cref="Rooms"/>, or -1 for corridors.</summary>
        public int RoomIndexAt(GridPos p)
        {
            for (int i = 0; i < Rooms.Count; i++)
                if (Rooms[i].Contains(p)) return i;
            return -1;
        }

        /// <summary>
        /// Builds a map from text, top row first: '#' wall, '.' floor, '@' start, '>' stairs.
        /// The whole map counts as one room. Used by tests and handy for hand-made floors.
        /// </summary>
        public static DungeonMap FromAscii(params string[] rows)
        {
            if (rows == null || rows.Length == 0) throw new ArgumentException("No rows given.");
            int height = rows.Length, width = rows[0].Length;
            var map = new DungeonMap(width, height);
            bool hasStart = false, hasStairs = false;

            for (int r = 0; r < height; r++)
            {
                if (rows[r].Length != width) throw new ArgumentException($"Row {r} is {rows[r].Length} wide, expected {width}.");
                int y = height - 1 - r;
                for (int x = 0; x < width; x++)
                {
                    var p = new GridPos(x, y);
                    switch (rows[r][x])
                    {
                        case '#':
                            break;
                        case '.':
                            map[p] = TileType.Floor;
                            break;
                        case '@':
                            map[p] = TileType.Floor;
                            map.Start = p;
                            hasStart = true;
                            break;
                        case '>':
                            map[p] = TileType.Floor;
                            map.Stairs = p;
                            hasStairs = true;
                            break;
                        default:
                            throw new ArgumentException($"Unknown map character '{rows[r][x]}'.");
                    }
                }
            }

            if (!hasStart) throw new ArgumentException("The map needs an '@' start tile.");
            if (!hasStairs) map.Stairs = new GridPos(-1, -1); // No stairs on this floor.
            map.Rooms.Add(new RectI(0, 0, width, height));
            return map;
        }

        /// <summary>Text dump in the same format as <see cref="FromAscii"/>; useful in test failure messages.</summary>
        public string ToAscii()
        {
            var sb = new StringBuilder((Width + 1) * Height);
            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < Width; x++)
                {
                    var p = new GridPos(x, y);
                    sb.Append(p == Start ? '@' : p == Stairs ? '>' : IsWalkable(p) ? '.' : '#');
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}

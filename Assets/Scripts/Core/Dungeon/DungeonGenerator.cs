using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    public sealed class DungeonGenConfig
    {
        public int Width = 56;
        public int Height = 32;
        public int MinRooms = 5;
        public int MaxRooms = 8;
        public int MinRoomSize = 4;
        public int MaxRoomWidth = 11;
        public int MaxRoomHeight = 7;

        /// <summary>Corridors added beyond the spanning tree, so floors have loops instead of dead-end chains.</summary>
        public int ExtraCorridors = 2;
    }

    /// <summary>
    /// Rooms-and-corridors floor generator: scatter non-overlapping rooms, join them with a minimum spanning
    /// tree of L-shaped corridors plus a few extra links, then pick start and stairs in different rooms.
    /// The same seed and config always give the same map.
    /// </summary>
    public static class DungeonGenerator
    {
        const int PlacementAttempts = 300;
        const int MaxRetries = 20;
        const int RoomGap = 2; // Wall tiles kept between rooms, and between rooms and the map edge.

        public static DungeonMap Generate(int seed, DungeonGenConfig config)
        {
            for (int retry = 0; retry < MaxRetries; retry++)
            {
                var map = TryGenerate(new Rng(Rng.DeriveSeed(seed, retry)), config);
                if (map != null) return map;
            }
            throw new InvalidOperationException($"Could not generate a floor for seed {seed}. Check the DungeonGenConfig sizes.");
        }

        /// <summary>
        /// The boss floor: a small antechamber where the hero arrives, a corridor, and a wide arena with two pillars to
        /// dodge around. There are no stairs; defeating the boss clears the dungeon. Rooms[0] is the antechamber and
        /// Rooms[1] the arena.
        /// </summary>
        public static DungeonMap GenerateBossFloor(int seed, DungeonGenConfig config)
        {
            var rng = new Rng(seed);
            var map = new DungeonMap(config.Width, config.Height);
            int middle = config.Height / 2;

            var antechamber = new RectI(4 + rng.Range(0, 3), middle - 2, 6, 5);
            int arenaWidth = 15 + 2 * rng.Range(0, 2), arenaHeight = 9 + 2 * rng.Range(0, 2); // Odd sizes keep a center tile.
            var arena = new RectI(antechamber.XMax + 8 + rng.Range(0, 4), middle - arenaHeight / 2, arenaWidth, arenaHeight);
            if (arena.XMax > config.Width - 1 - RoomGap || arena.Y < RoomGap || arena.YMax > config.Height - 1 - RoomGap)
                throw new InvalidOperationException($"Map {config.Width}x{config.Height} is too small for the boss floor.");

            map.Rooms.Add(antechamber);
            map.Rooms.Add(arena);
            Carve(map, antechamber);
            Carve(map, arena);
            CarveLine(map, new GridPos(antechamber.XMax, middle), new GridPos(arena.X, middle));

            // Two pillars, off the center line so the entrance and the boss's spot stay open.
            map[arena.X + arena.Width / 4, middle + 2] = TileType.Wall;
            map[arena.XMax - arena.Width / 4, middle - 2] = TileType.Wall;

            map.Start = antechamber.Center;
            map.Stairs = new GridPos(-1, -1);
            return map;
        }

        static DungeonMap TryGenerate(Rng rng, DungeonGenConfig c)
        {
            var map = new DungeonMap(c.Width, c.Height);
            int targetRooms = rng.Range(c.MinRooms, c.MaxRooms + 1);

            for (int i = 0; i < PlacementAttempts && map.Rooms.Count < targetRooms; i++)
            {
                int w = rng.Range(c.MinRoomSize, c.MaxRoomWidth + 1);
                int h = rng.Range(c.MinRoomSize, c.MaxRoomHeight + 1);
                int maxX = c.Width - w - RoomGap, maxY = c.Height - h - RoomGap;
                if (maxX < RoomGap || maxY < RoomGap) continue;

                var room = new RectI(rng.Range(RoomGap, maxX + 1), rng.Range(RoomGap, maxY + 1), w, h);
                if (!OverlapsAny(room, map.Rooms)) map.Rooms.Add(room);
            }
            if (map.Rooms.Count < Math.Max(2, c.MinRooms)) return null;

            foreach (var room in map.Rooms) Carve(map, room);
            ConnectRooms(map, rng, c.ExtraCorridors);

            int startRoom = rng.Range(0, map.Rooms.Count);
            int stairsRoom = (startRoom + rng.Range(1, map.Rooms.Count)) % map.Rooms.Count;
            map.Start = RandomTileIn(map.Rooms[startRoom], rng);
            map.Stairs = RandomTileIn(map.Rooms[stairsRoom], rng);
            return map;
        }

        static bool OverlapsAny(RectI room, List<RectI> rooms)
        {
            foreach (var other in rooms)
                if (room.Overlaps(other, RoomGap)) return true;
            return false;
        }

        static void Carve(DungeonMap map, RectI room)
        {
            for (int y = room.Y; y <= room.YMax; y++)
                for (int x = room.X; x <= room.XMax; x++)
                    map[x, y] = TileType.Floor;
        }

        /// <summary>Prim's minimum spanning tree over room centers, then extra links from random rooms to their nearest unlinked neighbor.</summary>
        static void ConnectRooms(DungeonMap map, Rng rng, int extraCorridors)
        {
            var rooms = map.Rooms;
            int n = rooms.Count;
            var linked = new HashSet<(int, int)>();
            var inTree = new bool[n];
            inTree[0] = true;

            for (int added = 1; added < n; added++)
            {
                int bestA = -1, bestB = -1, bestDistance = int.MaxValue;
                for (int a = 0; a < n; a++)
                {
                    if (!inTree[a]) continue;
                    for (int b = 0; b < n; b++)
                    {
                        if (inTree[b]) continue;
                        int d = DistanceSquared(rooms[a].Center, rooms[b].Center);
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            bestA = a;
                            bestB = b;
                        }
                    }
                }
                inTree[bestB] = true;
                Link(map, rng, linked, bestA, bestB);
            }

            for (int i = 0; i < extraCorridors; i++)
            {
                int a = rng.Range(0, n);
                int b = NearestUnlinked(rooms, linked, a);
                if (b >= 0) Link(map, rng, linked, a, b);
            }
        }

        static int NearestUnlinked(List<RectI> rooms, HashSet<(int, int)> linked, int a)
        {
            int best = -1, bestDistance = int.MaxValue;
            for (int b = 0; b < rooms.Count; b++)
            {
                if (b == a || linked.Contains(Key(a, b))) continue;
                int d = DistanceSquared(rooms[a].Center, rooms[b].Center);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = b;
                }
            }
            return best;
        }

        static void Link(DungeonMap map, Rng rng, HashSet<(int, int)> linked, int a, int b)
        {
            linked.Add(Key(a, b));
            var from = map.Rooms[a].Center;
            var to = map.Rooms[b].Center;
            var corner = rng.Chance(50) ? new GridPos(to.X, from.Y) : new GridPos(from.X, to.Y);
            CarveLine(map, from, corner);
            CarveLine(map, corner, to);
        }

        /// <summary>Carves a straight horizontal or vertical line of floor, endpoints included.</summary>
        static void CarveLine(DungeonMap map, GridPos a, GridPos b)
        {
            int dx = Math.Sign(b.X - a.X), dy = Math.Sign(b.Y - a.Y);
            var p = a;
            map[p] = TileType.Floor;
            while (p != b)
            {
                p = new GridPos(p.X + dx, p.Y + dy);
                map[p] = TileType.Floor;
            }
        }

        static GridPos RandomTileIn(RectI room, Rng rng) =>
            new GridPos(rng.Range(room.X, room.XMax + 1), rng.Range(room.Y, room.YMax + 1));

        static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

        static int DistanceSquared(GridPos a, GridPos b)
        {
            int dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }
    }
}

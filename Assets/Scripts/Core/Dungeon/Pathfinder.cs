using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    public static class Pathfinder
    {
        const sbyte Unvisited = -1;
        const sbyte StartMarker = 8;

        /// <summary>
        /// Breadth-first search over 8-way moves (respecting the corner rule). Gives the first step of a
        /// shortest path from <paramref name="start"/> to <paramref name="goal"/>, searching at most
        /// <paramref name="maxSteps"/> steps out. <paramref name="isBlocked"/> marks tiles that can't be
        /// entered, such as ones holding another actor; the goal tile itself is always allowed.
        /// </summary>
        public static bool TryFirstStep(DungeonMap map, GridPos start, GridPos goal, Func<GridPos, bool> isBlocked,
            int maxSteps, out Direction8 firstStep)
        {
            firstStep = Direction8.S;
            if (start == goal || !map.InBounds(start) || !map.InBounds(goal)) return false;

            int width = map.Width;
            var firstDirection = new sbyte[width * map.Height];
            var depth = new int[width * map.Height];
            for (int i = 0; i < firstDirection.Length; i++) firstDirection[i] = Unvisited;

            var queue = new Queue<GridPos>();
            firstDirection[start.Y * width + start.X] = StartMarker;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int currentIndex = current.Y * width + current.X;
                if (depth[currentIndex] >= maxSteps) continue;

                foreach (var dir in Directions.All)
                {
                    if (!map.CanStep(current, dir)) continue;
                    var next = current + dir.ToOffset();
                    int nextIndex = next.Y * width + next.X;
                    if (firstDirection[nextIndex] != Unvisited) continue;

                    bool isGoal = next == goal;
                    if (!isGoal && isBlocked != null && isBlocked(next)) continue;

                    firstDirection[nextIndex] = current == start ? (sbyte)dir : firstDirection[currentIndex];
                    if (isGoal)
                    {
                        firstStep = (Direction8)firstDirection[nextIndex];
                        return true;
                    }
                    depth[nextIndex] = depth[currentIndex] + 1;
                    queue.Enqueue(next);
                }
            }
            return false;
        }
    }
}

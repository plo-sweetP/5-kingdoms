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
            int maxSteps, out Direction8 firstStep) =>
            TryFirstStep(map, start, goal, isBlocked, maxSteps, out firstStep, out _);

        /// <summary>Same as the overload above, also giving the length of the path found, in steps.</summary>
        public static bool TryFirstStep(DungeonMap map, GridPos start, GridPos goal, Func<GridPos, bool> isBlocked,
            int maxSteps, out Direction8 firstStep, out int pathLength)
        {
            firstStep = Direction8.S;
            pathLength = 0;
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
                        pathLength = depth[currentIndex] + 1;
                        return true;
                    }
                    depth[nextIndex] = depth[currentIndex] + 1;
                    queue.Enqueue(next);
                }
            }
            return false;
        }

        /// <summary>
        /// How many steps' walk (8-way, respecting the corner rule, actors aside) every tile within
        /// <paramref name="maxSteps"/> of <paramref name="start"/> is from it; -1 for the tiles farther off or walled
        /// away. Indexed y * width + x.
        /// </summary>
        public static int[] StepsFrom(DungeonMap map, GridPos start, int maxSteps)
        {
            int width = map.Width;
            var steps = new int[width * map.Height];
            for (int i = 0; i < steps.Length; i++) steps[i] = Unvisited;
            if (!map.InBounds(start)) return steps;

            var queue = new Queue<GridPos>();
            steps[start.Y * width + start.X] = 0;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int taken = steps[current.Y * width + current.X];
                if (taken >= maxSteps) continue;

                foreach (var dir in Directions.All)
                {
                    if (!map.CanStep(current, dir)) continue;
                    var next = current + dir.ToOffset();
                    int nextIndex = next.Y * width + next.X;
                    if (steps[nextIndex] != Unvisited) continue;
                    steps[nextIndex] = taken + 1;
                    queue.Enqueue(next);
                }
            }
            return steps;
        }

        /// <summary>
        /// Breadth-first search for the nearest tile (by steps) that passes <paramref name="isGoal"/>, at most
        /// <paramref name="maxSteps"/> away, never entering blocked tiles. Gives the first step toward it and the path's
        /// length, or false if none is in reach (or <paramref name="start"/> itself passes).
        /// </summary>
        public static bool TryFindNearest(DungeonMap map, GridPos start, Func<GridPos, bool> isGoal, Func<GridPos, bool> isBlocked,
            int maxSteps, out Direction8 firstStep, out GridPos goal, out int pathLength)
        {
            firstStep = Direction8.S;
            goal = start;
            pathLength = 0;
            if (!map.InBounds(start) || isGoal(start)) return false;

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
                    if (isBlocked != null && isBlocked(next)) continue;

                    firstDirection[nextIndex] = current == start ? (sbyte)dir : firstDirection[currentIndex];
                    if (isGoal(next))
                    {
                        firstStep = (Direction8)firstDirection[nextIndex];
                        goal = next;
                        pathLength = depth[currentIndex] + 1;
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

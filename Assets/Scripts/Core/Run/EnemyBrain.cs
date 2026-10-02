namespace FiveKingdoms.Core
{
    public enum IntentKind { Wait, Move, Attack }

    public readonly struct Intent
    {
        public readonly IntentKind Kind;
        public readonly Direction8 Direction;

        Intent(IntentKind kind, Direction8 direction)
        {
            Kind = kind;
            Direction = direction;
        }

        public static readonly Intent Wait = new Intent(IntentKind.Wait, Direction8.S);
        public static Intent Move(Direction8 direction) => new Intent(IntentKind.Move, direction);
        public static Intent Attack(Direction8 direction) => new Intent(IntentKind.Attack, direction);
    }

    /// <summary>
    /// Wild monster AI: notice the hero when close or in the same room, chase along the shortest path,
    /// bite when adjacent, otherwise wander. Decides only; <see cref="DungeonRun"/> carries the intent out.
    /// </summary>
    public static class EnemyBrain
    {
        const int MaxChaseSteps = 30;

        public static Intent Decide(DungeonRun run, Actor self)
        {
            var hero = run.Hero;
            var map = run.Map;
            int distance = GridPos.ChebyshevDistance(self.Pos, hero.Pos);
            UpdateAlert(run, self, distance);

            if (distance == 1)
            {
                var toward = Directions.Toward(self.Pos, hero.Pos);
                if (map.IsCornerClear(self.Pos, toward)) return Intent.Attack(toward);
            }

            if (self.Alerted &&
                Pathfinder.TryFirstStep(map, self.Pos, hero.Pos, p => run.ActorAt(p) != null, MaxChaseSteps, out var step))
                return Intent.Move(step);

            return Wander(run, self);
        }

        static void UpdateAlert(DungeonRun run, Actor self, int distance)
        {
            if (distance <= run.Config.SightRange)
            {
                self.Alerted = true;
                return;
            }
            int heroRoom = run.Map.RoomIndexAt(run.Hero.Pos);
            if (heroRoom >= 0 && heroRoom == run.Map.RoomIndexAt(self.Pos))
                self.Alerted = true;
            else if (distance > run.Config.SightRange * 3)
                self.Alerted = false;
        }

        static Intent Wander(DungeonRun run, Actor self)
        {
            if (!run.Random.Chance(50)) return Intent.Wait;
            int first = run.Random.Range(0, 8);
            for (int i = 0; i < 8; i++)
            {
                var dir = (Direction8)((first + i) % 8);
                if (run.Map.CanStep(self.Pos, dir) && run.ActorAt(self.Pos + dir.ToOffset()) == null)
                    return Intent.Move(dir);
            }
            return Intent.Wait;
        }
    }
}

namespace FiveKingdoms.Core
{
    public enum IntentKind { Wait, Move, Attack, Charge, Slam, Summon }

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
        public static readonly Intent Charge = new Intent(IntentKind.Charge, Direction8.S);
        public static readonly Intent Slam = new Intent(IntentKind.Slam, Direction8.S);
        public static readonly Intent Summon = new Intent(IntentKind.Summon, Direction8.S);
        public static Intent Move(Direction8 direction) => new Intent(IntentKind.Move, direction);
        public static Intent Attack(Direction8 direction) => new Intent(IntentKind.Attack, direction);
    }

    /// <summary>
    /// Monster AI. Decides only; <see cref="DungeonRun"/> carries the intent out.
    /// Chasers notice the nearest party member when close or in the same room, chase along the shortest path,
    /// bite when adjacent, and otherwise wander. A taunted monster goes after its taunter instead. The Troll adds
    /// a telegraphed slam and a call for help.
    /// </summary>
    public static class EnemyBrain
    {
        const int MaxChaseSteps = 30;

        /// <summary>The slam hits every tile within this many steps of the boss.</summary>
        public const int SlamRadius = 1;

        /// <summary>The boss starts winding up when a target is this close.</summary>
        public const int SlamTriggerRange = 2;

        /// <summary>Boss turns between slams.</summary>
        public const int SlamCooldown = 3;

        /// <summary>The slam hits for this percent of ATK (a basic attack is 200%).</summary>
        public const int SlamDamagePercent = 320;
        public const int HelpersSummoned = 2;

        public static Intent Decide(DungeonRun run, Actor self) =>
            self.Definition.Brain == ActorBrain.Troll ? DecideTroll(run, self) : DecideChaser(run, self);

        /// <summary>The closest member of the opposing team: any party member, not just the leader.</summary>
        public static Actor NearestFoe(DungeonRun run, Actor self)
        {
            Actor nearest = null;
            int best = int.MaxValue;
            foreach (var actor in run.Actors)
            {
                if (actor.Team == self.Team || !actor.IsAlive) continue;
                int distance = GridPos.ChebyshevDistance(self.Pos, actor.Pos);
                if (distance < best)
                {
                    best = distance;
                    nearest = actor;
                }
            }
            return nearest;
        }

        /// <summary>Who a monster goes after: its taunter while that one still stands, otherwise the nearest foe.</summary>
        public static Actor TargetOf(DungeonRun run, Actor self)
        {
            var taunt = self.FindStatus(StatusKind.Taunt);
            var taunter = taunt != null ? run.FindActor(taunt.SourceId) : null;
            return taunter != null && taunter.IsAlive ? taunter : NearestFoe(run, self);
        }

        static Intent DecideTroll(DungeonRun run, Actor self)
        {
            if (self.Charging) return Intent.Slam;
            if (!self.CalledForHelp && self.Hp * 2 <= self.MaxHp) return Intent.Summon;

            var target = TargetOf(run, self);
            if (target != null && self.Alerted && self.SpecialCooldown == 0 &&
                GridPos.ChebyshevDistance(self.Pos, target.Pos) <= SlamTriggerRange)
                return Intent.Charge;
            return DecideChaser(run, self);
        }

        static Intent DecideChaser(DungeonRun run, Actor self)
        {
            var target = TargetOf(run, self);
            if (target == null) return Wander(run, self);

            var map = run.Map;
            int distance = GridPos.ChebyshevDistance(self.Pos, target.Pos);
            UpdateAlert(run, self, target, distance);

            if (distance == 1)
            {
                var toward = Directions.Toward(self.Pos, target.Pos);
                if (map.IsCornerClear(self.Pos, toward)) return Intent.Attack(toward);
            }

            if (self.Alerted &&
                Pathfinder.TryFirstStep(map, self.Pos, target.Pos, p => run.ActorAt(p) != null, MaxChaseSteps, out var step))
                return Intent.Move(step);

            return Wander(run, self);
        }

        static void UpdateAlert(DungeonRun run, Actor self, Actor target, int distance)
        {
            if (distance <= run.Config.SightRange)
            {
                self.Alerted = true;
                return;
            }
            int targetRoom = run.Map.RoomIndexAt(target.Pos);
            if (targetRoom >= 0 && targetRoom == run.Map.RoomIndexAt(self.Pos))
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

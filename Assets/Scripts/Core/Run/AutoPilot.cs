namespace FiveKingdoms.Core
{
    /// <summary>
    /// Plays the hero automatically: eat a berry when low, fight adjacent enemies, chase nearby ones,
    /// pick up berries in the current room, otherwise head for the stairs. Drives the soak tests, the
    /// balance report and the unattended autoplay smoke test. Not meant to play well, just plausibly.
    /// </summary>
    public static class AutoPilot
    {
        const int ChaseRange = 6;

        public static HeroCommand Decide(DungeonRun run)
        {
            var hero = run.Hero;
            var map = run.Map;

            if (run.Berries > 0 && hero.Hp * 100 < hero.MaxHp * 40) return HeroCommand.UseBerry;

            Actor nearestEnemy = null;
            int enemyDistance = int.MaxValue;
            foreach (var actor in run.Actors)
            {
                if (actor.Team == hero.Team) continue;
                int distance = GridPos.ChebyshevDistance(hero.Pos, actor.Pos);
                if (distance == 1)
                {
                    var toward = Directions.Toward(hero.Pos, actor.Pos);
                    if (map.IsCornerClear(hero.Pos, toward)) return HeroCommand.Move(toward); // Bump attack.
                }
                if (distance < enemyDistance)
                {
                    enemyDistance = distance;
                    nearestEnemy = actor;
                }
            }

            if (run.HeroOnStairs) return HeroCommand.Descend;

            var goal = map.Stairs;
            if (nearestEnemy != null && enemyDistance <= ChaseRange)
            {
                goal = nearestEnemy.Pos;
            }
            else if (run.Berries < run.Config.MaxBerries)
            {
                // Only detour for berries in the hero's room: a plain distance check flip-flops between
                // berry and stairs when the path to the berry leads away first.
                int heroRoom = map.RoomIndexAt(hero.Pos);
                int best = int.MaxValue;
                foreach (var item in run.Items)
                {
                    if (heroRoom < 0 || map.RoomIndexAt(item.Pos) != heroRoom) continue;
                    int distance = GridPos.ChebyshevDistance(hero.Pos, item.Pos);
                    if (distance < best)
                    {
                        best = distance;
                        goal = item.Pos;
                    }
                }
            }

            return Pathfinder.TryFirstStep(map, hero.Pos, goal, p => run.ActorAt(p) != null, 200, out var step)
                ? HeroCommand.Move(step)
                : HeroCommand.Wait;
        }
    }
}

using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>
    /// Plays the hero automatically: step out of a boss's wind-up, eat a berry when low, fight adjacent enemies,
    /// chase nearby ones, pick up nearby berries, otherwise head for the stairs (or the boss, on the boss floor).
    /// Drives the soak tests, the balance report and the unattended autoplay smoke test. Not meant to play well,
    /// just plausibly. Targets are chosen by walking distance, which only shrinks while the hero follows the path;
    /// choosing by straight-line distance made it flip between two goals at doorways.
    /// </summary>
    public static class AutoPilot
    {
        const int ChaseRange = 6;
        const int BerryDetourRange = 6;
        const int FarSearchLimit = 200;

        public static HeroCommand Decide(DungeonRun run)
        {
            var hero = run.Hero;
            var map = run.Map;
            Func<GridPos, bool> blocked = p => run.ActorAt(p) != null;

            foreach (var actor in run.Actors)
            {
                if (actor.Team == hero.Team || !actor.Charging) continue;
                if (GridPos.ChebyshevDistance(hero.Pos, actor.Pos) <= EnemyBrain.SlamRadius && TryStepAway(run, actor.Pos, out var away))
                    return HeroCommand.Move(away);
            }

            if (run.Berries > 0 && hero.Hp * 100 < hero.MaxHp * 40) return HeroCommand.UseBerry;

            var enemies = new List<GridPos>();
            foreach (var actor in run.Actors)
            {
                if (actor.Team == hero.Team) continue;
                if (GridPos.ChebyshevDistance(hero.Pos, actor.Pos) == 1)
                {
                    var toward = Directions.Toward(hero.Pos, actor.Pos);
                    if (map.IsCornerClear(hero.Pos, toward)) return HeroCommand.Move(toward); // Bump attack.
                }
                enemies.Add(actor.Pos);
            }

            if (run.HeroOnStairs) return HeroCommand.Descend;

            Direction8 step;
            if (!map.InBounds(map.Stairs))
            {
                // Boss floor: no stairs, so the only way forward is through the enemies.
                if (TryStepTowardNearest(run, enemies, FarSearchLimit, blocked, out step)) return HeroCommand.Move(step);
                return HeroCommand.Wait;
            }

            if (TryStepTowardNearest(run, enemies, ChaseRange, blocked, out step)) return HeroCommand.Move(step);

            if (run.Berries < run.Config.MaxBerries)
            {
                var berries = new List<GridPos>();
                foreach (var item in run.Items) berries.Add(item.Pos);
                if (TryStepTowardNearest(run, berries, BerryDetourRange, blocked, out step)) return HeroCommand.Move(step);
            }

            return Pathfinder.TryFirstStep(map, hero.Pos, map.Stairs, blocked, FarSearchLimit, out step)
                ? HeroCommand.Move(step)
                : HeroCommand.Wait;
        }

        /// <summary>First step toward whichever goal is the shortest walk away, if any is within <paramref name="maxSteps"/>.</summary>
        static bool TryStepTowardNearest(DungeonRun run, List<GridPos> goals, int maxSteps, Func<GridPos, bool> blocked, out Direction8 step)
        {
            step = Direction8.S;
            int best = int.MaxValue;
            foreach (var goal in goals)
            {
                if (GridPos.ChebyshevDistance(run.Hero.Pos, goal) > maxSteps) continue; // Can't be within reach.
                if (Pathfinder.TryFirstStep(run.Map, run.Hero.Pos, goal, blocked, maxSteps, out var first, out int length) && length < best)
                {
                    best = length;
                    step = first;
                }
            }
            return best != int.MaxValue;
        }

        /// <summary>A step that takes the hero out of reach of an attack centered on <paramref name="threat"/>.</summary>
        static bool TryStepAway(DungeonRun run, GridPos threat, out Direction8 away)
        {
            away = Direction8.S;
            int best = -1;
            foreach (var dir in Directions.All)
            {
                var next = run.Hero.Pos + dir.ToOffset();
                if (!run.Map.CanStep(run.Hero.Pos, dir) || run.ActorAt(next) != null) continue;
                int distance = GridPos.ChebyshevDistance(next, threat);
                if (distance > EnemyBrain.SlamRadius && distance > best)
                {
                    best = distance;
                    away = dir;
                }
            }
            return best >= 0;
        }
    }
}

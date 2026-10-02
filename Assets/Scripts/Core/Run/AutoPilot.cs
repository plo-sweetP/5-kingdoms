using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>
    /// Plays the hero automatically: eat a berry when low, fight adjacent enemies, chase nearby ones,
    /// pick up nearby berries, otherwise head for the stairs. Drives the soak tests, the balance report
    /// and the unattended autoplay smoke test. Not meant to play well, just plausibly.
    /// Targets are chosen by walking distance, which only shrinks while the hero follows the path; choosing
    /// by straight-line distance made it flip between two goals at doorways.
    /// </summary>
    public static class AutoPilot
    {
        const int ChaseRange = 6;
        const int BerryDetourRange = 6;
        const int StairsSearchLimit = 200;

        public static HeroCommand Decide(DungeonRun run)
        {
            var hero = run.Hero;
            var map = run.Map;

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

            Func<GridPos, bool> blocked = p => run.ActorAt(p) != null;
            if (TryStepTowardNearest(run, enemies, ChaseRange, blocked, out var step)) return HeroCommand.Move(step);

            if (run.Berries < run.Config.MaxBerries)
            {
                var berries = new List<GridPos>();
                foreach (var item in run.Items) berries.Add(item.Pos);
                if (TryStepTowardNearest(run, berries, BerryDetourRange, blocked, out step)) return HeroCommand.Move(step);
            }

            return Pathfinder.TryFirstStep(map, hero.Pos, map.Stairs, blocked, StairsSearchLimit, out step)
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
    }
}

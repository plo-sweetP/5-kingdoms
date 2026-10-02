using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>
    /// Plays the hero automatically: step out of a boss's wind-up; when low, heal with a skill (or eat a berry that heals
    /// or pays for the heal); fight adjacent enemies, with a mana-building strike when short of mana for a heal; between
    /// fights, eat a berry if a heal isn't affordable; chase nearby enemies, pick up nearby berries, otherwise head for
    /// the stairs, dashing down straight stretches (or for the boss, on the boss floor).
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

            int heal = SkillSlot(hero, SkillEffect.Heal);
            int healCost = heal >= 0 ? hero.Definition.Skills[heal].ManaCost : 0;
            bool berryFundsHeal = heal >= 0 && run.Config.BerryRestoreMp > 0 && hero.Mp < healCost && run.Berries > 0;
            if (hero.Hp * 100 < hero.MaxHp * 40)
            {
                if (heal >= 0 && run.CheckSkill(heal) == SkillCheck.Ready) return HeroCommand.Skill(heal);
                bool berryHeals = run.Config.BerryHealHp > 0 && run.Berries > 0;
                if (berryHeals || berryFundsHeal) return HeroCommand.UseBerry;
            }

            int strike = SkillSlot(hero, SkillEffect.Strike);
            var enemies = new List<GridPos>();
            foreach (var actor in run.Actors)
            {
                if (actor.Team == hero.Team) continue;
                if (GridPos.ChebyshevDistance(hero.Pos, actor.Pos) == 1)
                {
                    var toward = Directions.Toward(hero.Pos, actor.Pos);
                    if (map.IsCornerClear(hero.Pos, toward))
                    {
                        // Short of mana for a heal: a mana-building strike instead of a plain bump.
                        if (strike >= 0 && hero.Mp < healCost && run.CheckSkill(strike) == SkillCheck.Ready) return HeroCommand.Skill(strike);
                        return HeroCommand.Move(toward); // Bump attack.
                    }
                }
                enemies.Add(actor.Pos);
            }

            // Between fights, eat a berry rather than walk into the next one without mana for a heal.
            if (!run.InCombat && berryFundsHeal) return HeroCommand.UseBerry;

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

            if (!Pathfinder.TryFirstStep(map, hero.Pos, map.Stairs, blocked, FarSearchLimit, out step, out int length)) return HeroCommand.Wait;
            int dash = SkillSlot(hero, SkillEffect.Dash);
            return DashSaves(run, dash, map.Stairs, step, length, blocked) ? HeroCommand.Skill(dash, step) : HeroCommand.Move(step);
        }

        /// <summary>True when dashing along the first step of the path gets at least two tiles closer to the goal.</summary>
        static bool DashSaves(DungeonRun run, int dash, GridPos goal, Direction8 step, int pathLength, Func<GridPos, bool> blocked)
        {
            if (dash < 0 || run.CheckSkill(dash, step) != SkillCheck.Ready) return false;
            var end = run.DashDestination(run.Hero.Definition.Skills[dash].Power, step);
            int rest = end == goal ? 0
                : Pathfinder.TryFirstStep(run.Map, end, goal, blocked, FarSearchLimit, out _, out int length) ? length
                : int.MaxValue;
            return rest <= pathLength - 2;
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

        /// <summary>Slot of the hero's first skill with this effect, or -1.</summary>
        static int SkillSlot(Actor hero, SkillEffect effect)
        {
            var skills = hero.Definition.Skills;
            for (int i = 0; i < skills.Count; i++)
                if (skills[i].Effect == effect) return i;
            return -1;
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

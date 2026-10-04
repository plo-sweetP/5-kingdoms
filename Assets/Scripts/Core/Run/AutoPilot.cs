using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>
    /// Plays the leader automatically: step out of a boss's wind-up; use a charged ultimate when it's worth it; when low,
    /// heal with a skill or eat a berry; heal or guard the party; a ranged leader gets out of melee; a badly hurt one
    /// swaps back behind a healthier ally; fight a foe in reach, with the target and skill chosen by
    /// <see cref="HeroTactics"/> (the marked enemy first, then the lowest HP); in a fight, a ranged leader finds a tile to
    /// shoot from; otherwise chase nearby enemies; with partners in a fight, go for the foes that are after the party,
    /// closing up behind the partners that hold the way to them (it doesn't walk off while they fight); then pick up
    /// nearby berries and head for the stairs, dashing down straight stretches (or for the boss, on the boss floor).
    /// It never walks into a foe: every attack is an explicit command naming its target. Partners play themselves
    /// (<see cref="PartnerBrain"/>).
    /// Drives the soak tests, the balance report and the unattended autoplay smoke test. Not meant to play well,
    /// just plausibly. Where to walk is chosen by walking distance, which only shrinks while the hero follows the path;
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
            // Foes are in the way, and so is a partner the leader just swapped with (no swapping back and forth). In a fight
            // the leader keeps the formation too: a ranged leader never swaps forward, a melee one never past another melee hero.
            Func<GridPos, bool> blocked = p => run.ActorAt(p) is Actor other && other != hero &&
                (other.Team != hero.Team || other.Id == hero.SwappedWithId && hero.SwapBlockTurns > 0 ||
                 run.InCombat && (hero.Definition.IsRanged || !other.Definition.IsRanged));

            if (HeroTactics.TryDodge(run, hero, out var command)) return command;
            if (HeroTactics.TryUltimate(run, hero, out command)) return command;

            if (hero.Hp * 100 < hero.MaxHp * HeroTactics.SelfHealPercent)
            {
                int heal = HeroTactics.SkillSlot(hero, SkillEffect.Heal);
                if (heal >= 0 && run.CheckSkill(heal) == SkillCheck.Ready) return HeroCommand.Skill(heal);
                if (run.Config.BerryHealHp > 0 && run.Berries > 0) return HeroCommand.UseBerry;
            }
            if (HeroTactics.TryHealParty(run, hero, out command)) return command;
            if (HeroTactics.TryGuard(run, hero, out command)) return command;
            if (HeroTactics.TryStepOutOfMelee(run, hero, out command)) return command;
            if (HeroTactics.TryRunToSafety(run, hero, out command)) return command;
            if (HeroTactics.TryMark(run, hero, out command)) return command;
            if (HeroTactics.TryAttack(run, hero, out command)) return command;
            // A ranged leader hangs back at a tile it can shoot from; a melee one chases below, as it always has.
            if (run.InCombat && hero.Definition.IsRanged && HeroTactics.TryTakeFiringPosition(run, hero, out command)) return command;
            var enemies = HeroTactics.FoePositions(run, hero);

            if (run.HeroOnStairs) return HeroCommand.Descend;

            Direction8 step;
            if (!map.InBounds(map.Stairs))
            {
                // Boss floor: no stairs, so the only way forward is through the enemies.
                if (TryStepTowardNearest(run, enemies, FarSearchLimit, blocked, out step)) return Walk(run, step);
                return HeroCommand.Wait;
            }

            if (TryStepTowardNearest(run, enemies, ChaseRange, blocked, out step)) return Walk(run, step);

            // In a fight the party stays together: a leader with partners goes for the foes that are after them, farther
            // off too, by the straight way. Where a partner holds that way (a corridor, a doorway) it closes up behind
            // it, ready to take a place at the front, rather than walk off to the berries and the stairs.
            if (run.InCombat && HasPartners(run))
            {
                var hunters = new List<GridPos>();
                foreach (var actor in run.Actors)
                    if (actor.Team != hero.Team && actor.Alerted) hunters.Add(actor.Pos);
                if (TryStepTowardNearest(run, hunters, HeroTactics.FightSearchSteps, p => run.ActorAt(p) is Actor other && other.Team != hero.Team, out step))
                {
                    var next = hero.Pos + step.ToOffset();
                    bool heldByPartner = run.ActorAt(next) is Actor ally && ally.Team == hero.Team && blocked(next);
                    return heldByPartner ? HeroCommand.Wait : Walk(run, step);
                }
            }

            if (run.Berries < run.Config.MaxBerries)
            {
                var berries = new List<GridPos>();
                foreach (var item in run.Items) berries.Add(item.Pos);
                if (TryStepTowardNearest(run, berries, BerryDetourRange, blocked, out step)) return Walk(run, step);
            }

            // A monster dozing in a corridor can block every way to the stairs: then walk up to it and fight through
            // (allies still only as the formation allows).
            Func<GridPos, bool> alliesInTheWay = p => run.ActorAt(p) is Actor other && other.Team == hero.Team && blocked(p);
            if (!Pathfinder.TryFirstStep(map, hero.Pos, map.Stairs, blocked, FarSearchLimit, out step, out int length) &&
                !Pathfinder.TryFirstStep(map, hero.Pos, map.Stairs, alliesInTheWay, FarSearchLimit, out step, out length))
                return HeroCommand.Wait;
            int dash = HeroTactics.SkillSlot(hero, SkillEffect.Dash);
            return DashSaves(run, dash, map.Stairs, step, length, blocked) ? HeroCommand.Skill(dash, step) : Walk(run, step);
        }

        static bool HasPartners(DungeonRun run)
        {
            foreach (var member in run.Party)
                if (member != run.Hero && member.IsAlive && run.FindActor(member.Id) != null) return true;
            return false;
        }

        /// <summary>
        /// One step along a path. Walking into a foe would only turn the leader to face it, so when one stands on the next
        /// tile the leader attacks it instead (and waits if its weapon can't reach it).
        /// </summary>
        static HeroCommand Walk(DungeonRun run, Direction8 step)
        {
            var next = run.Hero.Pos + step.ToOffset();
            if (!(run.ActorAt(next) is Actor other) || other.Team == run.Hero.Team) return HeroCommand.Move(step);
            return run.AttackTargetAt(run.Hero, next) != null ? HeroCommand.AttackAt(next) : HeroCommand.Wait;
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

    }
}

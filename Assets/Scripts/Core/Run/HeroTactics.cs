using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>
    /// Fighting decisions shared by the leader's <see cref="AutoPilot"/> and the partners' <see cref="PartnerBrain"/>:
    /// get out of a boss's wind-up, use a charged ultimate when it's worth it, heal whoever needs it, guard, get a ranged
    /// hero out of melee, swap a badly hurt hero away from the foes, and pick an attack on a foe in reach. Attacks are
    /// deliberate (PROGRESSION.md, "Targeting and input"): every one names its target, chosen the same way by every hero
    /// (<see cref="PickTarget"/>: the marked enemy first, then the lowest HP), and nobody attacks by walking into a foe.
    /// In a fight, melee heroes close in on a foe and ranged ones find a tile to shoot from behind them ("Ranged vs
    /// melee"). Works from a hero's skills by their effects, so new kits need no new AI.
    /// </summary>
    public static class HeroTactics
    {
        /// <summary>Allies below this share of their max HP get a heal.</summary>
        const int AllyHealPercent = 50;

        /// <summary>A hero heals itself below this share of its max HP.</summary>
        public const int SelfHealPercent = 40;

        /// <summary>A utility status (a mark, a stun, a taunt) is only worth it on a foe with more HP than this many times the hero's ATK.</summary>
        const int UtilityTargetHits = 4;


        /// <summary>Allies under this share of max HP make an aura worth raising.</summary>
        const int AuraHurtPercent = 70;

        /// <summary>How far (in steps) a hero looks for a tile to fight from.</summary>
        public const int FightSearchSteps = 12;

        /// <summary>Slot of the hero's first skill with this effect, or -1.</summary>
        public static int SkillSlot(Actor hero, SkillEffect effect)
        {
            var skills = hero.Definition.Skills;
            for (int i = 0; i < skills.Count; i++)
                if (skills[i].Effect == effect) return i;
            return -1;
        }

        /// <summary>Slot of the hero's rolling skill (Rolling Shot), or -1.</summary>
        static int RollSlot(Actor hero)
        {
            var skills = hero.Definition.Skills;
            for (int i = 0; i < skills.Count; i++)
                if (skills[i].RollTiles > 0) return i;
            return -1;
        }

        // ---- Targets ----

        /// <summary>
        /// The foe to go for among those in reach (PROGRESSION.md, "Auto picks targets deliberately"): the marked enemy
        /// first, then the lowest HP; between equals the nearest, then the lower id. Null if there are none.
        /// </summary>
        public static Actor PickTarget(Actor hero, List<Actor> foes)
        {
            Actor best = null;
            foreach (var foe in foes)
                if (best == null || ComesBefore(hero, foe, best)) best = foe;
            return best;
        }

        static bool ComesBefore(Actor hero, Actor a, Actor b)
        {
            bool aMarked = a.FindStatus(StatusKind.Mark) != null, bMarked = b.FindStatus(StatusKind.Mark) != null;
            if (aMarked != bMarked) return aMarked;
            if (a.Hp != b.Hp) return a.Hp < b.Hp;
            int aDistance = GridPos.ChebyshevDistance(hero.Pos, a.Pos), bDistance = GridPos.ChebyshevDistance(hero.Pos, b.Pos);
            if (aDistance != bDistance) return aDistance < bDistance;
            return a.Id < b.Id;
        }

        /// <summary>The foes a melee blow from <paramref name="hero"/> reaches: next to it, corners allowing.</summary>
        public static List<Actor> FoesInStrikeReach(DungeonRun run, Actor hero)
        {
            var foes = new List<Actor>();
            foreach (var actor in run.Actors)
                if (actor.Team != hero.Team && run.StrikeTargetAt(hero, actor.Pos) != null) foes.Add(actor);
            return foes;
        }

        /// <summary>Every foe one of the hero's attacks could hit from where it stands: next to it, or in sight within its shots' reach.</summary>
        static List<Actor> FoesInReach(DungeonRun run, Actor hero)
        {
            int reach = Reach(hero);
            var foes = reach > 1 ? run.FoesInSight(hero, reach) : new List<Actor>();
            foreach (var foe in FoesInStrikeReach(run, hero))
                if (!foes.Contains(foe)) foes.Add(foe);
            return foes;
        }

        // ---- Decisions, in the order the brains ask them ----

        /// <summary>
        /// A boss is winding up a slam that would hit <paramref name="hero"/>: step out of reach, or, if there's nowhere
        /// to go, raise a guard.
        /// </summary>
        public static bool TryDodge(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            foreach (var actor in run.Actors)
            {
                if (actor.Team == hero.Team || !actor.Charging) continue;
                if (GridPos.ChebyshevDistance(hero.Pos, actor.Pos) > EnemyBrain.SlamRadius) continue;
                if (TryStepAway(run, hero, actor.Pos, out var away))
                {
                    command = HeroCommand.Move(away);
                    return true;
                }
                int guard = SkillSlot(hero, SkillEffect.Guard);
                if (guard >= 0 && run.CheckSkill(hero, guard, hero.Facing) == SkillCheck.Ready)
                {
                    command = HeroCommand.Skill(guard);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The hero's ultimate, once its meter is full and it's worth it (not on a foe a weapon attack would finish): a
        /// Volley centered where it catches the most foes; a Flurry on the foe it would attack anyway; an Aura in a fight
        /// when the hero or an ally next to it (everyone it covers) is in melee or hurt, or the boss is fighting.
        /// </summary>
        public static bool TryUltimate(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            var ultimate = hero.Definition.Ultimate;
            if (ultimate == null || !hero.UltimateReady) return false;
            switch (ultimate.Effect)
            {
                case SkillEffect.Area:
                {
                    // Among centers that catch as many, the one the hero would attack anyway.
                    var centers = run.FoesInSight(hero, ultimate.Range);
                    centers.Sort((a, b) => ComesBefore(hero, a, b) ? -1 : ComesBefore(hero, b, a) ? 1 : 0);
                    int best = 0;
                    foreach (var center in centers)
                    {
                        int worth = 0;
                        foreach (var actor in run.Actors)
                            if (actor.Team != hero.Team && GridPos.ChebyshevDistance(actor.Pos, center.Pos) <= ultimate.Radius)
                                worth += actor.Definition.IsBoss ? 2 : 1;
                        if (worth <= best || worth == 1 && !Sturdy(hero, center)) continue;
                        best = worth;
                        command = HeroCommand.UltimateAt(center.Pos);
                    }
                    return best > 0;
                }
                case SkillEffect.Strike:
                {
                    var near = FoesInStrikeReach(run, hero);
                    var target = PickTarget(hero, near);
                    if (target == null || near.Count < 2 && !Sturdy(hero, target)) return false;
                    if (run.CheckUltimateAt(hero, target.Pos) != SkillCheck.Ready) return false;
                    command = HeroCommand.UltimateAt(target.Pos);
                    return true;
                }
                case SkillEffect.Aura:
                {
                    // It covers the hero and the allies next to it.
                    if (!run.InCombat) return false;
                    bool needed = run.Boss != null && run.Boss.Alerted;
                    foreach (var member in run.Party)
                    {
                        if (!member.IsAlive || GridPos.ChebyshevDistance(member.Pos, hero.Pos) > ultimate.Radius) continue;
                        if (run.FindActor(member.Id) == null) continue;
                        if (run.FoeAdjacent(member) || member.Hp * 100 < member.MaxHp * AuraHurtPercent) needed = true;
                    }
                    if (!needed) return false;
                    command = HeroCommand.Ultimate(hero.Facing);
                    return true;
                }
                default:
                    return false;
            }
        }

        /// <summary>A heal when someone it would reach is badly hurt (the hero below 40%, an ally below 50%).</summary>
        public static bool TryHealParty(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            int heal = SkillSlot(hero, SkillEffect.Heal);
            if (heal < 0 || run.CheckSkill(hero, heal, hero.Facing) != SkillCheck.Ready) return false;
            foreach (var member in run.HealTargets(hero, hero.Definition.Skills[heal]))
            {
                int threshold = member == hero ? SelfHealPercent : AllyHealPercent;
                if (member.Hp * 100 < member.MaxHp * threshold)
                {
                    command = HeroCommand.Skill(heal);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Hunter's Mark (a Quick action) on the foe the hero is about to attack, if it's worth it (a boss, or one that
        /// will take several hits) and the hero's mark isn't on anyone yet. Everyone then goes for the marked foe.
        /// </summary>
        public static bool TryMark(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            int mark = SkillSlot(hero, SkillEffect.Mark);
            if (mark < 0) return false;
            foreach (var actor in run.Actors)
            {
                var status = actor.FindStatus(StatusKind.Mark);
                if (status != null && status.SourceId == hero.Id) return false; // Already hunting something.
            }
            var target = PickTarget(hero, run.FoesInSight(hero, hero.Definition.Skills[mark].Range));
            if (target == null || !WorthAStatus(hero, target) || run.CheckSkillAt(hero, mark, target.Pos) != SkillCheck.Ready) return false;
            command = HeroCommand.SkillAt(mark, target.Pos);
            return true;
        }

        /// <summary>
        /// A ranged hero with a foe next to it gets out of melee (PROGRESSION.md, "Ranged heroes step out of melee"): its
        /// rolling shot when that lands on a tile clear of foes with something to shoot (out and attacking in one turn),
        /// else a step back to such a tile. It steps back only once in a row: if the foe keeps following, or there's no
        /// such tile, it shoots anyway at the point-blank penalty (<see cref="TryAttack"/>).
        /// </summary>
        public static bool TryStepOutOfMelee(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            if (!hero.Definition.IsRanged || !run.FoeAdjacent(hero)) return false;

            int roll = RollSlot(hero);
            if (roll >= 0)
            {
                var skill = hero.Definition.Skills[roll];
                int best = 0;
                foreach (var dir in Directions.All)
                {
                    if (run.CheckSkill(hero, roll, dir) != SkillCheck.Ready) continue;
                    var end = run.DashDestination(hero, skill.RollTiles, dir);
                    if (run.FoeAdjacent(end, hero.Team) || !run.AnyFoeInSight(end, hero.Team, skill.Range)) continue;
                    int distance = run.DistanceToNearestFoe(end, hero.Team);
                    if (distance <= best) continue;
                    best = distance;
                    command = HeroCommand.Skill(roll, dir);
                }
                if (best > 0) return true;
            }

            if (hero.RetreatSteps > 0) return false; // It keeps following: stand and shoot.
            int reach = Reach(hero), bestStep = 0;
            foreach (var dir in Directions.All)
            {
                var next = hero.Pos + dir.ToOffset();
                if (!run.Map.CanStep(hero.Pos, dir) || run.ActorAt(next) != null) continue;
                if (run.FoeAdjacent(next, hero.Team) || !run.AnyFoeInSight(next, hero.Team, reach)) continue;
                int distance = run.DistanceToNearestFoe(next, hero.Team);
                if (distance <= bestStep) continue;
                bestStep = distance;
                command = HeroCommand.Move(dir);
            }
            return bestStep > 0;
        }

        /// <summary>
        /// "Run to safety" (PROGRESSION.md, "Swaps, without loops"): a badly hurt hero with a foe next to it swaps places
        /// with a healthier ally standing farther from the foes, two melee heroes included. Of several such allies, the one
        /// standing farthest from them. The pair can't swap back for a few turns (<see cref="DungeonRun.CanSwap"/>).
        /// </summary>
        public static bool TryRunToSafety(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            if (!DungeonRun.IsBadlyHurt(hero) || !run.FoeAdjacent(hero)) return false;
            int best = 0;
            foreach (var dir in Directions.All)
            {
                var ally = run.ActorAt(hero.Pos + dir.ToOffset());
                if (ally == null || !run.IsSaferSwap(hero, ally) || !run.CanSwap(hero, ally)) continue;
                int distance = run.DistanceToNearestFoe(ally.Pos, hero.Team);
                if (distance <= best) continue;
                best = distance;
                command = HeroCommand.Move(dir);
            }
            return best > 0;
        }

        /// <summary>A foe that would survive the hero's weapon attack (bosses always): worth an ultimate on its own.</summary>
        static bool Sturdy(Actor hero, Actor target) =>
            target.Definition.IsBoss || target.Hp > hero.Attack * CombatRules.BasicAttackPercent / 100;

        /// <summary>A status that only helps against a strong foe (a mark, a stun, a taunt) is wasted on one that falls in a hit or two.</summary>
        static bool WorthAStatus(Actor hero, Actor target) =>
            target.Definition.IsBoss || target.Hp > hero.Attack * UtilityTargetHits;

        /// <summary>A guard when the hero is crowded (two or more foes next to it) or a slam is coming at it or a guarded ally.</summary>
        public static bool TryGuard(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            int guard = SkillSlot(hero, SkillEffect.Guard);
            if (guard < 0 || run.CheckSkill(hero, guard, hero.Facing) != SkillCheck.Ready) return false;
            int radius = hero.Definition.Skills[guard].Radius;
            int adjacentFoes = 0;
            foreach (var actor in run.Actors)
            {
                if (actor.Team == hero.Team) continue;
                if (GridPos.ChebyshevDistance(actor.Pos, hero.Pos) == 1) adjacentFoes++;
                if (!actor.Charging) continue;
                foreach (var member in run.Party)
                {
                    if (!member.IsAlive || GridPos.ChebyshevDistance(member.Pos, hero.Pos) > radius) continue;
                    if (GridPos.ChebyshevDistance(member.Pos, actor.Pos) <= EnemyBrain.SlamRadius)
                    {
                        command = HeroCommand.Skill(guard);
                        return true;
                    }
                }
            }
            if (adjacentFoes < 2) return false;
            command = HeroCommand.Skill(guard);
            return true;
        }

        /// <summary>
        /// An attack on a foe in reach, if there is one: the target by <see cref="PickTarget"/> (the marked enemy first,
        /// then the lowest HP) among the foes next to the hero and, for heroes with shots, those in sight within range.
        /// The attack on it, best first: a skill that puts a status or a stun on a strong target that doesn't have it yet,
        /// the hardest-hitting skill that beats the weapon attack, and otherwise the weapon attack. Skills cost nothing
        /// but a turn's cooldown, so a hero alternates between them. The command names the target's tile.
        /// </summary>
        public static bool TryAttack(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            var foes = FoesInReach(run, hero);
            // The best target first; one that nothing ready reaches (only a skill that's cooling down) gives way to the next.
            while (foes.Count > 0)
            {
                var target = PickTarget(hero, foes);
                if (TryAttack(run, hero, target, out command)) return true;
                foes.Remove(target);
            }
            return false;
        }

        static bool TryAttack(DungeonRun run, Actor hero, Actor target, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            var skills = hero.Definition.Skills;
            int control = -1, strongest = -1, strongestPower = CombatRules.BasicAttackPercent;
            for (int slot = 0; slot < skills.Count; slot++)
            {
                var skill = skills[slot];
                if (!skill.DealsDamage || skill.RollTiles > 0) continue; // Rolls are for getting out of melee.
                if (run.CheckSkillAt(hero, slot, target.Pos) != SkillCheck.Ready) continue;
                bool fresh = skill.Status.HasValue && target.FindStatus(skill.Status.Value) == null || skill.StunChance > 0 && run.CanDelay(target);
                if (fresh && WorthAStatus(hero, target) && control < 0) control = slot;
                int power = skill.Power * skill.Hits;
                if (power <= strongestPower) continue;
                strongestPower = power;
                strongest = slot;
            }
            int chosen = control >= 0 ? control : strongest;
            if (chosen >= 0)
            {
                command = HeroCommand.SkillAt(chosen, target.Pos);
                return true;
            }
            if (run.AttackTargetAt(hero, target.Pos) == null) return false; // Only skills reach that one.
            command = HeroCommand.AttackAt(target.Pos);
            return true;
        }

        /// <summary>
        /// Moving in a fight (PROGRESSION.md, "Battle formation"): a melee hero closes in on a foe (<see cref="TryEngage"/>),
        /// a ranged one goes to a tile it can shoot from (<see cref="TryTakeFiringPosition"/>).
        /// </summary>
        public static bool TryJoinFight(DungeonRun run, Actor hero, out HeroCommand command) =>
            hero.Definition.IsRanged ? TryTakeFiringPosition(run, hero, out command) : TryEngage(run, hero, out command);

        /// <summary>
        /// A melee partner heads for the nearest tile next to a foe in the fight: around allies when it can, or swapping
        /// past a ranged one when that's the shorter way and the swap is allowed (<see cref="DungeonRun.CanSwap"/>).
        /// </summary>
        public static bool TryEngage(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            Func<GridPos, bool> isGoal = p => IsAttackSpot(run, hero, p);
            bool around = Pathfinder.TryFindNearest(run.Map, hero.Pos, isGoal, p => run.ActorAt(p) != null, FightSearchSteps,
                out var aroundStep, out _, out int aroundLength);

            // Through ranged allies it isn't blocked from swapping with: when that's shorter, the first step must be an allowed swap.
            Func<GridPos, bool> blocked = p => run.ActorAt(p) is Actor other && (other.Team != hero.Team || !MaySwapThrough(hero, other));
            bool through = Pathfinder.TryFindNearest(run.Map, hero.Pos, isGoal, blocked, FightSearchSteps,
                out var throughStep, out _, out int throughLength);
            if (through && (!around || throughLength < aroundLength))
            {
                var occupant = run.ActorAt(hero.Pos + throughStep.ToOffset());
                if (occupant == null || run.CanSwap(hero, occupant))
                {
                    command = HeroCommand.Move(throughStep);
                    return true;
                }
            }
            if (!around) return false;
            command = HeroCommand.Move(aroundStep);
            return true;
        }

        /// <summary>The pair rules alone (a melee hero past a ranged one, not straight back): for tiles further along a path.</summary>
        static bool MaySwapThrough(Actor hero, Actor other) =>
            other.Definition.IsRanged && !hero.Definition.IsRanged &&
            !(hero.SwappedWithId == other.Id && hero.SwapBlockTurns > 0) && !(other.SwappedWithId == hero.Id && other.SwapBlockTurns > 0);

        /// <summary>A tile next to a foe in the fight (alerted, or within sight of the hero), with the corner clear for an attack.</summary>
        static bool IsAttackSpot(DungeonRun run, Actor hero, GridPos pos)
        {
            foreach (var actor in run.Actors)
            {
                if (actor.Team == hero.Team || GridPos.ChebyshevDistance(actor.Pos, pos) != 1) continue;
                if (!actor.Alerted && GridPos.ChebyshevDistance(actor.Pos, hero.Pos) > run.Config.SightRange) continue;
                if (run.Map.IsCornerClear(pos, Directions.Toward(pos, actor.Pos))) return true;
            }
            return false;
        }

        /// <summary>
        /// A ranged hero with nothing to shoot heads for the nearest tile it could shoot from that isn't next to a foe, so
        /// it hangs back while the melee heroes close in.
        /// </summary>
        public static bool TryTakeFiringPosition(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            int reach = Reach(hero);
            Func<GridPos, bool> isGoal = p => !run.FoeAdjacent(p, hero.Team) && run.AnyFoeInSight(p, hero.Team, reach);
            if (!Pathfinder.TryFindNearest(run.Map, hero.Pos, isGoal, p => run.ActorAt(p) != null, FightSearchSteps, out var step, out _, out _))
                return false;
            command = HeroCommand.Move(step);
            return true;
        }

        /// <summary>How far the hero's attacks reach (its weapon attack or its longest shot); 1 for a melee hero.</summary>
        public static int Reach(Actor hero)
        {
            int reach = hero.Definition.AttackRange;
            foreach (var skill in hero.Definition.Skills)
                if (skill.Effect == SkillEffect.Shot && skill.Range > reach) reach = skill.Range;
            return reach;
        }

        /// <summary>A step that takes <paramref name="hero"/> out of reach of an attack centered on <paramref name="threat"/>.</summary>
        public static bool TryStepAway(DungeonRun run, Actor hero, GridPos threat, out Direction8 away)
        {
            away = Direction8.S;
            int best = -1;
            foreach (var dir in Directions.All)
            {
                var next = hero.Pos + dir.ToOffset();
                if (!run.Map.CanStep(hero.Pos, dir) || run.ActorAt(next) != null) continue;
                int distance = GridPos.ChebyshevDistance(next, threat);
                if (distance > EnemyBrain.SlamRadius && distance > best)
                {
                    best = distance;
                    away = dir;
                }
            }
            return best >= 0;
        }

        /// <summary>Positions of every foe on the floor.</summary>
        public static List<GridPos> FoePositions(DungeonRun run, Actor hero)
        {
            var foes = new List<GridPos>();
            foreach (var actor in run.Actors)
                if (actor.Team != hero.Team) foes.Add(actor.Pos);
            return foes;
        }
    }
}

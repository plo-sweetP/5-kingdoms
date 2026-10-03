using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>
    /// Fighting decisions shared by the leader's <see cref="AutoPilot"/> and the partners' <see cref="PartnerBrain"/>:
    /// get out of a boss's wind-up, use a charged ultimate when it's worth it, heal whoever needs it, guard, get a ranged
    /// hero out of melee, and pick an attack on a foe in reach. In a fight, melee heroes close in on a foe and ranged ones
    /// find a tile to shoot from behind them (PROGRESSION.md, "Ranged vs melee"). Works from a hero's skills by their
    /// effects, so new kits need no new AI.
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
        /// Volley where it catches the most foes; a Flurry on a foe in reach; an Aura in a fight when allies next to the
        /// hero are in melee or hurt, or the boss is fighting.
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
                    int best = 0;
                    foreach (var dir in Directions.All)
                    {
                        var center = run.FirstFoeInLine(hero, dir, ultimate.Range, out _);
                        if (center == null) continue;
                        int worth = 0;
                        foreach (var actor in run.Actors)
                            if (actor.Team != hero.Team && GridPos.ChebyshevDistance(actor.Pos, center.Pos) <= ultimate.Radius)
                                worth += actor.Definition.IsBoss ? 2 : 1;
                        if (worth <= best || worth == 1 && !Sturdy(hero, center)) continue;
                        best = worth;
                        command = HeroCommand.Ultimate(dir);
                    }
                    return best > 0;
                }
                case SkillEffect.Strike:
                {
                    if (!TryPickTarget(run, hero, out var target, out var toward, out int distance) || distance != 1) return false;
                    int adjacent = 0;
                    foreach (var actor in run.Actors)
                        if (actor.Team != hero.Team && GridPos.ChebyshevDistance(actor.Pos, hero.Pos) == 1) adjacent++;
                    if (adjacent < 2 && !Sturdy(hero, target) || run.CheckUltimate(hero, toward) != SkillCheck.Ready) return false;
                    command = HeroCommand.Ultimate(toward);
                    return true;
                }
                case SkillEffect.Aura:
                {
                    if (!run.InCombat) return false;
                    bool covers = false, needed = run.Boss != null && run.Boss.Alerted;
                    foreach (var member in run.Party)
                    {
                        if (member == hero || !member.IsAlive || GridPos.ChebyshevDistance(member.Pos, hero.Pos) > ultimate.Radius) continue;
                        if (run.FindActor(member.Id) == null) continue;
                        covers = true;
                        if (run.FoeAdjacent(member) || member.Hp * 100 < member.MaxHp * AuraHurtPercent) needed = true;
                    }
                    if (!covers || !needed) return false;
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
        /// Hunter's Mark (a Quick action) on a foe worth it (a boss, or one that will take several hits) when the hero's
        /// mark isn't on anyone yet.
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
            var skill = hero.Definition.Skills[mark];
            var target = run.FindShotTarget(hero, hero.Facing, skill.Range, out var toward, out _);
            if (target == null || !WorthAStatus(hero, target) || run.CheckSkill(hero, mark, toward) != SkillCheck.Ready) return false;
            command = HeroCommand.Skill(mark, toward);
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
                    if (run.FoeAdjacent(end, hero.Team) || !run.AnyFoeInLine(end, hero.Team, skill.Range)) continue;
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
                if (run.FoeAdjacent(next, hero.Team) || !run.AnyFoeInLine(next, hero.Team, reach)) continue;
                int distance = run.DistanceToNearestFoe(next, hero.Team);
                if (distance <= bestStep) continue;
                bestStep = distance;
                command = HeroCommand.Move(dir);
            }
            return bestStep > 0;
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
        /// An attack on a foe in reach, if there is one. The target: the first adjacent foe in turn order (corners
        /// allowing), or for heroes with ranged attacks the nearest foe along a clear straight line. The attack, best
        /// first: a skill that puts a status (or a stun) on a strong target that doesn't have it yet, the hardest-hitting
        /// skill that beats the weapon attack, and otherwise the weapon attack. Skills cost nothing but a turn's cooldown,
        /// so a hero alternates between them.
        /// </summary>
        public static bool TryAttack(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            if (!TryPickTarget(run, hero, out var target, out var toward, out int distance)) return false;

            var skills = hero.Definition.Skills;
            int control = -1, strongest = -1, strongestPower = CombatRules.BasicAttackPercent;
            for (int slot = 0; slot < skills.Count; slot++)
            {
                var skill = skills[slot];
                if (!skill.DealsDamage || skill.RollTiles > 0 || !InReach(skill, distance)) continue; // Rolls are for getting out of melee.
                if (run.CheckSkill(hero, slot, toward) != SkillCheck.Ready) continue;
                bool controls = skill.Status.HasValue || skill.StunChance > 0;
                bool fresh = (!skill.Status.HasValue || target.FindStatus(skill.Status.Value) == null) && target.FindStatus(StatusKind.Stunned) == null;
                if (controls && fresh && WorthAStatus(hero, target) && control < 0) control = slot;
                int power = skill.Power * skill.Hits;
                if (power <= strongestPower) continue;
                strongestPower = power;
                strongest = slot;
            }
            int chosen = control >= 0 ? control : strongest;
            if (chosen >= 0)
            {
                command = HeroCommand.Skill(chosen, toward);
                return true;
            }
            if (distance > hero.Definition.AttackRange) return false; // Only skills reach that far.
            // Melee: bump into it (the original autopilot's attack). Ranged: shoot along the line.
            command = distance == 1 && hero.Definition.AttackRange == 1 ? HeroCommand.Move(toward) : HeroCommand.AttackToward(toward);
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
            Func<GridPos, bool> isGoal = p => !run.FoeAdjacent(p, hero.Team) && run.AnyFoeInLine(p, hero.Team, reach);
            if (!Pathfinder.TryFindNearest(run.Map, hero.Pos, isGoal, p => run.ActorAt(p) != null, FightSearchSteps, out var step, out _, out _))
                return false;
            command = HeroCommand.Move(step);
            return true;
        }

        /// <summary>How far the hero's attacks reach along a line (its weapon attack or its longest shot).</summary>
        public static int Reach(Actor hero)
        {
            int reach = hero.Definition.AttackRange;
            foreach (var skill in hero.Definition.Skills)
                if (skill.Effect == SkillEffect.Shot && skill.Range > reach) reach = skill.Range;
            return reach;
        }

        static bool InReach(SkillDefinition skill, int distance) =>
            skill.Effect == SkillEffect.Strike ? distance == 1 : distance <= skill.Range;

        /// <summary>
        /// The foe to attack: the first adjacent one in turn order whose corner is clear, else (for ranged heroes) the
        /// nearest one along a clear straight line within reach.
        /// </summary>
        static bool TryPickTarget(DungeonRun run, Actor hero, out Actor target, out Direction8 toward, out int distance)
        {
            foreach (var actor in run.Actors)
            {
                if (actor.Team == hero.Team || GridPos.ChebyshevDistance(hero.Pos, actor.Pos) != 1) continue;
                var dir = Directions.Toward(hero.Pos, actor.Pos);
                if (!run.Map.IsCornerClear(hero.Pos, dir)) continue;
                target = actor;
                toward = dir;
                distance = 1;
                return true;
            }
            target = null;
            toward = hero.Facing;
            distance = 0;
            if (Reach(hero) <= 1) return false;
            target = run.FindShotTarget(hero, hero.Facing, Reach(hero), out toward, out distance);
            return target != null;
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

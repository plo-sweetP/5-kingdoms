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
    /// melee"). Partners stay together doing it: they fight near the leader, and with allies in the way (a corridor, a
    /// doorway) they take a way around only if it's short, and otherwise wait their turn right behind them.
    /// They use the corridors too ("Doorways and corridors"): the hero in front holds a doorway against a crowd rather
    /// than step out among it, the hurt hero that holds the way trades places with the fresh melee hero behind it, who
    /// fights while it heals, and a hero fighting in a corridor's mouth makes way for the one behind.
    /// Works from a hero's skills by their effects, so new kits need no new AI.
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

        /// <summary>Partners fight (and go after foes) within this many steps' walk of the leader, so the party doesn't scatter.</summary>
        public const int LeashRange = 6;

        /// <summary>
        /// A partner walks around the allies in its way only if that's at most this many steps longer than the straight
        /// way through them. In a corridor or a doorway the way around is a tour of the floor: there it waits behind them.
        /// </summary>
        public const int DetourSteps = 4;

        /// <summary>
        /// With at most this many foes close beyond a doorway the party goes in, and the melee hero behind gets into the
        /// fight too; with more, the hero in front holds the doorway (PROGRESSION.md, "Doorways and corridors").
        /// </summary>
        public const int SafeCrowd = 2;

        /// <summary>
        /// The same for a hero that has no melee ally to bring into the fight, or that is hurt: going in gains it nothing
        /// (or costs it too much), so it goes in against one foe only and holds the doorway against two.
        /// </summary>
        public const int LoneCrowd = 1;

        /// <summary>
        /// With at least this share of its max HP a hero is fit to step out among <see cref="SafeCrowd"/> foes. Set with
        /// the run simulations: at 60% nearly as many heroes fell before the boss as with no such rule, from 85% up
        /// about half as many.
        /// </summary>
        public const int FitPercent = 90;

        /// <summary>
        /// Turns a hero holds a doorway with no foe coming into its reach before it goes in after all: foes that are
        /// after someone they can't get to only mill about.
        /// </summary>
        public const int DoorPatience = 5;

        /// <summary>
        /// How near a foe is "close", in steps' walk: the foes' sight range. A foe that near has noticed the hero and is
        /// on its way. From a doorway that is 4 tiles into the room (PROGRESSION.md: "within 3-4 tiles").
        /// </summary>
        public static int CloseSteps(DungeonRun run) => run.Config.SightRange;

        /// <summary>Slot of the hero's first skill with this effect, or -1.</summary>
        public static int SkillSlot(Actor hero, SkillEffect effect)
        {
            var skills = hero.Skills;
            for (int i = 0; i < skills.Count; i++)
                if (skills[i].Effect == effect) return i;
            return -1;
        }

        /// <summary>Slot of the hero's rolling skill (Rolling Shot), or -1.</summary>
        static int RollSlot(Actor hero)
        {
            var skills = hero.Skills;
            for (int i = 0; i < skills.Count; i++)
                if (skills[i].RollTiles > 0) return i;
            return -1;
        }

        /// <summary>Slot of the hero's strike that dashes up to its target first (Lunge), or -1.</summary>
        static int LungeSlot(Actor hero)
        {
            var skills = hero.Skills;
            for (int i = 0; i < skills.Count; i++)
                if (skills[i].Effect == SkillEffect.Strike && skills[i].DashTiles > 0) return i;
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
        /// Volley centered where it catches the most foes; a Flurry on the foe it would attack anyway; a Blade Dance
        /// when its area holds a boss or at least two foes, aimed where it holds the most; an Aura in a fight when the
        /// hero or an ally next to it (everyone it covers) is in melee or hurt, or the boss is fighting.
        /// </summary>
        public static bool TryUltimate(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            var ultimate = hero.Ultimate;
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
                case SkillEffect.SharedStrikes:
                {
                    // Among the foes next to the hero, the one whose area holds the most; between equals, the one it would attack anyway.
                    var centers = FoesInStrikeReach(run, hero);
                    centers.Sort((a, b) => ComesBefore(hero, a, b) ? -1 : ComesBefore(hero, b, a) ? 1 : 0);
                    int best = 0;
                    foreach (var center in centers)
                    {
                        int foes = 0;
                        bool boss = false;
                        foreach (var actor in run.Actors)
                        {
                            if (actor.Team == hero.Team || GridPos.ChebyshevDistance(actor.Pos, center.Pos) > ultimate.Radius) continue;
                            foes++;
                            boss |= actor.Definition.IsBoss;
                        }
                        if (foes <= best || foes < 2 && !boss) continue;
                        if (run.CheckUltimateAt(hero, center.Pos) != SkillCheck.Ready) continue;
                        best = foes;
                        command = HeroCommand.UltimateAt(center.Pos);
                    }
                    return best > 0;
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

        /// <summary>
        /// A heal when someone it would reach is badly hurt (the hero below 40%, an ally below 50%). A hero waiting
        /// behind the one that holds a corridor or a doorway heals sooner, whenever none of it is wasted: "the fresh one
        /// fights, the hurt one heals behind" (PROGRESSION.md, "Doorways and corridors"). Between fights the party tops
        /// itself up: a heal is used whenever it's worth it (<see cref="WorthToppingUp"/>).
        /// </summary>
        public static bool TryHealParty(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            int heal = SkillSlot(hero, SkillEffect.Heal);
            if (heal < 0 || run.CheckSkill(hero, heal, hero.Facing) != SkillCheck.Ready) return false;
            var skill = hero.Skills[heal];
            if (!run.InCombat && WouldTopUp(run, hero, skill))
            {
                command = HeroCommand.Skill(heal);
                return true;
            }
            bool resting = IsBehindTheFront(run, hero);
            foreach (var member in run.HealTargets(hero, skill))
            {
                int threshold = member == hero ? SelfHealPercent : AllyHealPercent;
                if (member.Hp * 100 < member.MaxHp * threshold ||
                    resting && member.MaxHp - member.Hp >= DungeonRun.HealAmount(hero, member, skill))
                {
                    command = HeroCommand.Skill(heal);
                    return true;
                }
            }
            return false;
        }

        // ---- Between fights (PROGRESSION.md, "Heroes heal between fights") ----

        /// <summary>
        /// Between fights a heal is worth using on someone missing at least this share of what it restores: at most
        /// half of it is wasted, and nobody spends a turn on a scratch. With Haiden's 20% heal that tops him up to over
        /// 90% of his HP, which is what makes him fit to step into a room (<see cref="FitPercent"/>).
        /// </summary>
        public const int TopUpPercent = 50;

        /// <summary>
        /// Turns in a row the autopilot's leader waits for the party to heal up without a heal landing on anyone,
        /// before it moves on: long enough for a hurt partner to walk over to the healer (<see cref="LeashRange"/>).
        /// </summary>
        public const int RestPatience = 6;

        /// <summary>Whether <paramref name="healer"/>'s heal, used on <paramref name="target"/> between fights, would mostly go to use.</summary>
        public static bool WorthToppingUp(Actor healer, Actor target, SkillDefinition heal) =>
            (target.MaxHp - target.Hp) * 100 >= DungeonRun.HealAmount(healer, target, heal) * TopUpPercent;

        /// <summary>Whether <paramref name="healer"/>'s heal, used where it stands, would top up someone it reaches.</summary>
        static bool WouldTopUp(DungeonRun run, Actor healer, SkillDefinition heal)
        {
            foreach (var member in run.HealTargets(healer, heal))
                if (WorthToppingUp(healer, member, heal)) return true;
            return false;
        }

        /// <summary>
        /// The party member whose heal could top <paramref name="hero"/> up between fights, if the hero went and stood
        /// next to it: the nearest one, within <see cref="LeashRange"/> steps' walk, whose heal reaches its neighbors
        /// and would be worth using on the hero. Null if there is none, or if the hero has a heal of its own that it
        /// will use on itself instead.
        /// </summary>
        public static Actor HealerFor(DungeonRun run, Actor hero)
        {
            if (run.InCombat || !hero.IsAlive) return null;
            int own = SkillSlot(hero, SkillEffect.Heal);
            if (own >= 0 && WorthToppingUp(hero, hero, hero.Skills[own])) return null;

            Actor nearest = null;
            int best = int.MaxValue;
            int[] steps = null;
            foreach (var member in run.Party)
            {
                if (member == hero || !member.IsAlive || run.FindActor(member.Id) == null) continue;
                int slot = SkillSlot(member, SkillEffect.Heal);
                if (slot < 0) continue;
                var heal = member.Skills[slot];
                if (heal.HealTarget == HealTarget.Self || !WorthToppingUp(member, hero, heal)) continue;
                steps ??= Pathfinder.StepsFrom(run.Map, hero.Pos, LeashRange);
                int distance = steps[member.Pos.Y * run.Map.Width + member.Pos.X];
                if (distance < 0 || distance >= best) continue;
                best = distance;
                nearest = member;
            }
            return nearest;
        }

        /// <summary>
        /// Between fights, a hurt hero that can't mend itself goes and stands next to the party member that can heal
        /// it (<see cref="HealerFor"/>), and stays there until it's topped up. False when nobody can, or when there is
        /// no way to a tile next to the healer (its own allies fill the corridor).
        /// </summary>
        public static bool TrySeekHealer(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            var healer = HealerFor(run, hero);
            if (healer == null) return false;
            if (GridPos.ChebyshevDistance(hero.Pos, healer.Pos) <= 1) return true; // In its reach: stay for the heal.
            if (!Pathfinder.TryFindNearest(run.Map, hero.Pos, p => GridPos.ChebyshevDistance(p, healer.Pos) == 1,
                    p => run.ActorAt(p) != null, LeashRange + 2, out var step, out _, out _))
                return false;
            command = HeroCommand.Move(step);
            return true;
        }

        /// <summary>
        /// Whether the party is still healing up between fights: someone's heal would top up a hero it reaches (now, or
        /// once it's ready again), or a hurt hero is on its way to the one that can heal it.
        /// </summary>
        public static bool IsToppingUp(DungeonRun run)
        {
            if (run.InCombat) return false;
            foreach (var member in run.Party)
            {
                if (!member.IsAlive || run.FindActor(member.Id) == null) continue;
                int slot = SkillSlot(member, SkillEffect.Heal);
                if (slot >= 0 && WouldTopUp(run, member, member.Skills[slot])) return true;
                var healer = HealerFor(run, member);
                if (healer != null && GridPos.ChebyshevDistance(member.Pos, healer.Pos) > 1) return true;
            }
            return false;
        }

        /// <summary>
        /// The autopilot's leader between fights: it doesn't move on while the party is healing up
        /// (<see cref="IsToppingUp"/>). Hurt with no heal of its own, it goes to the partner that can heal it. It
        /// gives up after <see cref="RestPatience"/> turns in which no heal landed (<see cref="Actor.RestedTurns"/>).
        /// </summary>
        public static bool TryRest(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Rest;
            if (run.InCombat || hero.RestedTurns >= RestPatience) return false;
            if (TrySeekHealer(run, hero, out var seek))
            {
                if (seek.Kind == HeroCommandKind.Move) command = seek;
                return true;
            }
            return IsToppingUp(run);
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
            var target = PickTarget(hero, run.FoesInSight(hero, hero.Skills[mark].Range));
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
                var skill = hero.Skills[roll];
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
            int radius = hero.Skills[guard].Radius;
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
        /// A counter stance (Riposte) when it would be answered: a foe next to the hero comes up before the hero's next
        /// turn and is going for the hero, not held by another hero's taunt or busy with one (<see cref="WouldStrike"/>).
        /// Never inside a boss's slam that is winding up (the hero steps out, <see cref="TryDodge"/>, or fights on),
        /// and not while a skill that hits harder than the weapon attack is ready for a foe in reach: Triple Thrust
        /// comes first.
        /// </summary>
        public static bool TryRiposte(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            int stance = SkillSlot(hero, SkillEffect.Counter);
            if (stance < 0 || run.CheckSkill(hero, stance, hero.Facing) != SkillCheck.Ready) return false;
            int cost = hero.Skills[stance].CostPercent;
            bool answered = false;
            foreach (var foe in run.Actors)
            {
                if (foe.Team == hero.Team) continue;
                if (foe.Charging && GridPos.ChebyshevDistance(hero.Pos, foe.Pos) <= EnemyBrain.SlamRadius) return false;
                answered |= WouldStrike(run, foe, hero, cost);
            }
            if (!answered) return false;
            if (TryAttack(run, hero, out var attack) && attack.Kind == HeroCommandKind.Skill) return false;
            command = HeroCommand.Skill(stance);
            return true;
        }

        /// <summary>
        /// Whether <paramref name="foe"/> is set to hit <paramref name="hero"/> before the hero's next turn, if the
        /// hero now spends <paramref name="costPercent"/> of a turn: it stands next to the hero (corner allowing), its
        /// turn comes first, and the hero is the one it goes for (<see cref="EnemyBrain.TargetOf"/>: its taunter, else
        /// the nearest hero). Measured with -balance: without that last check two stances in three went unanswered
        /// (1.2 a run, 0.4 answered), with it one in five (0.5 a run, 0.4 answered).
        /// </summary>
        static bool WouldStrike(DungeonRun run, Actor foe, Actor hero, int costPercent)
        {
            if (run.StrikeTargetAt(hero, foe.Pos) == null || !run.ActsBefore(foe, hero, costPercent)) return false;
            return EnemyBrain.TargetOf(run, foe) == hero;
        }

        /// <summary>
        /// "Lunge to reach a foe instead of walking up to it" (PROGRESSION.md, "Kristela's Fencer kit"): a foe that the
        /// hero's dashing strike reaches from further off than the next tile, picked like any target (the marked one
        /// first, then the lowest HP). It passes the checks of a step toward the foes: the tile the hero lands on is
        /// near the leader (the leash), and a dash out of a corridor or a doorway is only made where a step out of it
        /// would be (<see cref="HoldsTheDoor"/>), and only from its last tile, where that can be told.
        /// </summary>
        public static bool TryLunge(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            int slot = LungeSlot(hero);
            if (slot < 0 || hero.SkillCooldowns[slot] > 0) return false;
            int dash = hero.Skills[slot].DashTiles;
            var near = NearLeader(run, hero);
            var foes = new List<Actor>();
            foreach (var foe in run.Actors)
            {
                if (foe.Team == hero.Team || GridPos.ChebyshevDistance(hero.Pos, foe.Pos) < 2) continue;
                if (run.StrikeTargetAt(hero, foe.Pos, dash) == null) continue;
                var dir = Directions.Toward(hero.Pos, foe.Pos);
                var landing = foe.Pos - dir.ToOffset();
                if (near(landing) && !DashLeavesTheDoor(run, hero, dir, landing)) foes.Add(foe);
            }
            var target = PickTarget(hero, foes);
            if (target == null || run.CheckSkillAt(hero, slot, target.Pos) != SkillCheck.Ready) return false;
            command = HeroCommand.SkillAt(slot, target.Pos);
            return true;
        }

        /// <summary>
        /// Whether a dash from the hero's tile along <paramref name="dir"/> to <paramref name="landing"/> would carry it
        /// out of a corridor or a doorway that it should hold, or that it hasn't reached the end of yet.
        /// </summary>
        static bool DashLeavesTheDoor(DungeonRun run, Actor hero, Direction8 dir, GridPos landing)
        {
            var map = run.Map;
            for (var pos = hero.Pos; pos != landing;)
            {
                var next = pos + dir.ToOffset();
                if (map.IsNarrow(pos) && !map.IsNarrow(next) && (pos != hero.Pos || HoldsTheDoor(run, hero, next))) return true;
                pos = next;
            }
            return false;
        }

        /// <summary>
        /// A step toward the foes, as the party's AI takes one: not at all where the hero holds the doorway it stands
        /// in (<see cref="HoldsTheDoor"/>: it waits there), and as a Lunge when that reaches a foe at once
        /// (<see cref="TryLunge"/>). Swaps and steps that aren't toward the foes don't come through here.
        /// </summary>
        public static HeroCommand StepToward(DungeonRun run, Actor hero, Direction8 step)
        {
            var next = hero.Pos + step.ToOffset();
            if (HoldsTheDoor(run, hero, next)) return HeroCommand.HoldTheDoor;
            return run.ActorAt(next) == null && TryLunge(run, hero, out var lunge) ? lunge : HeroCommand.Move(step);
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
            var skills = hero.Skills;
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

        // ---- Doorways and corridors (PROGRESSION.md, "Doorways and corridors") ----

        /// <summary>
        /// "Hold the door": whether <paramref name="hero"/>, about to step out of a corridor or a doorway onto
        /// <paramref name="next"/>, should wait where it stands instead. There only the tile straight ahead can reach
        /// it (the corner rule), and the archer shoots past it; out there the crowd would be all around it while the
        /// others watched from the corridor. It holds when more than <see cref="SafeCrowd"/> foes are close beyond the
        /// doorway. With that many or fewer the party goes in, so that the melee hero behind gets into the fight too:
        /// if there is one to bring, and the hero is fit (<see cref="FitPercent"/>); otherwise it goes in against one foe
        /// only. Hurt, with a fresher melee ally behind it, it waits to be relieved
        /// (<see cref="DungeonRun.IsFrontRotation"/>). It doesn't hold when an ally is out there already (it isn't the
        /// one in front), against a boss (a slam is dodged in the open, not taken in a corridor), or for ever
        /// (<see cref="DoorPatience"/>). Ranged heroes keep their distance their own way.
        /// </summary>
        public static bool HoldsTheDoor(DungeonRun run, Actor hero, GridPos next)
        {
            var map = run.Map;
            if (hero.Definition.IsRanged || hero.HeldTurns >= DoorPatience) return false;
            if (!map.IsNarrow(hero.Pos) || map.IsNarrow(next)) return false;

            // Who is beyond the doorway, in steps from the tile outside it and not back through the hero's own.
            var beyond = Pathfinder.StepsFrom(map, next, CloseSteps(run) - 1, p => p == hero.Pos);
            int close = 0;
            foreach (var actor in run.Actors)
            {
                if (actor == hero || beyond[actor.Pos.Y * map.Width + actor.Pos.X] < 0) continue;
                if (actor.Team == hero.Team || actor.Definition.IsBoss) return false;
                close++;
            }
            if (close == 0) return false;
            if (AwaitsRelief(run, hero)) return true;
            bool fit = hero.Hp * 100 >= hero.MaxHp * FitPercent;
            return close > (fit && BringsAMeleeAlly(run, hero) ? SafeCrowd : LoneCrowd);
        }

        /// <summary>A fresher melee ally stands next to the hurt hero, ready to take the front from it.</summary>
        static bool AwaitsRelief(DungeonRun run, Actor hero)
        {
            foreach (var dir in Directions.All)
            {
                if (!run.Map.CanStep(hero.Pos, dir)) continue;
                var back = run.ActorAt(hero.Pos + dir.ToOffset());
                if (back != null && back.Team == hero.Team && run.IsFrontRotation(hero, back)) return true;
            }
            return false;
        }

        /// <summary>Another melee hero of the party is near enough to follow the hero into a fight (within <see cref="LeashRange"/> steps' walk).</summary>
        static bool BringsAMeleeAlly(DungeonRun run, Actor hero)
        {
            var steps = Pathfinder.StepsFrom(run.Map, hero.Pos, LeashRange);
            foreach (var member in run.Party)
            {
                if (member == hero || !member.IsAlive || member.Definition.IsRanged || run.FindActor(member.Id) == null) continue;
                if (steps[member.Pos.Y * run.Map.Width + member.Pos.X] >= 0) return true;
            }
            return false;
        }

        /// <summary>
        /// "Rotate the front", from behind: a melee hero behind a hurt ally that holds the way takes its place, so the
        /// fresh one fights and the hurt one heals behind (<see cref="DungeonRun.IsFrontRotation"/>). A partner never
        /// does this to the leader (<see cref="DungeonRun.IsRotateSwap"/>).
        /// </summary>
        public static bool TryTakeTheFront(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            foreach (var dir in Directions.All)
            {
                var front = run.ActorAt(hero.Pos + dir.ToOffset());
                if (front == null || front.Team != hero.Team || !run.IsFrontRotation(front, back: hero) || !run.CanSwap(hero, front)) continue;
                command = HeroCommand.Move(dir);
                return true;
            }
            return false;
        }

        /// <summary>
        /// "Rotate the front", from the front: the hurt hero that holds the way steps back behind the fresh melee ally
        /// next to it. For the leader's autopilot; a partner in front leaves the move to the one behind it, whose turn
        /// would be spent waiting anyway.
        /// </summary>
        public static bool TryGiveUpTheFront(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            foreach (var dir in Directions.All)
            {
                var back = run.ActorAt(hero.Pos + dir.ToOffset());
                if (back == null || back.Team != hero.Team || !run.IsFrontRotation(hero, back) || !run.CanSwap(hero, back)) continue;
                command = HeroCommand.Move(dir);
                return true;
            }
            return false;
        }

        /// <summary>
        /// "Steps in and aside": a melee hero that fights in the mouth of a corridor while a melee ally behind it can't
        /// get out moves over to another tile it can fight from (the one with the fewest ways to reach it), so the ally
        /// comes out and fights beside it. Only with more than one foe near: against one, the turn buys nothing.
        /// </summary>
        public static bool TryMakeWay(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            var map = run.Map;
            if (hero.Definition.IsRanged || !run.InCombat || !run.InMelee(hero) || !HoldsUpAnAlly(run, hero)) return false;
            if (FoesWithin(run, hero, CloseSteps(run)) < 2) return false;
            int best = int.MaxValue;
            foreach (var dir in Directions.All)
            {
                var next = hero.Pos + dir.ToOffset();
                if (!map.CanStep(hero.Pos, dir) || run.ActorAt(next) != null || map.IsNarrow(next) || !IsAttackSpot(run, hero, next)) continue;
                int open = map.OpenNeighbors(next);
                if (open >= best) continue;
                best = open;
                command = HeroCommand.Move(dir);
            }
            return best != int.MaxValue;
        }

        /// <summary>
        /// Whether <paramref name="hero"/> waits behind the front: no foe in its own reach, next to an ally that is in
        /// melee, where one of the two stands in a corridor or a doorway (so nothing gets past that ally to it).
        /// </summary>
        public static bool IsBehindTheFront(DungeonRun run, Actor hero)
        {
            if (FoesInReach(run, hero).Count > 0) return false;
            bool narrow = run.Map.IsNarrow(hero.Pos);
            foreach (var dir in Directions.All)
            {
                if (!run.Map.CanStep(hero.Pos, dir)) continue;
                var ally = run.ActorAt(hero.Pos + dir.ToOffset());
                if (ally != null && ally.Team == hero.Team && (narrow || run.Map.IsNarrow(ally.Pos)) && run.InMelee(ally)) return true;
            }
            return false;
        }

        /// <summary>A melee ally next to the hero, in a corridor or a doorway, with no foe in its reach: the hero stands in its way out.</summary>
        static bool HoldsUpAnAlly(DungeonRun run, Actor hero)
        {
            foreach (var dir in Directions.All)
            {
                if (!run.Map.CanStep(hero.Pos, dir)) continue;
                var ally = run.ActorAt(hero.Pos + dir.ToOffset());
                if (ally == null || ally.Team != hero.Team || ally.Definition.IsRanged) continue;
                if (run.Map.IsNarrow(ally.Pos) && !run.InMelee(ally)) return true;
            }
            return false;
        }

        /// <summary>How many foes are within <paramref name="steps"/> steps' walk of the hero (walls count, actors don't).</summary>
        static int FoesWithin(DungeonRun run, Actor hero, int steps)
        {
            var from = Pathfinder.StepsFrom(run.Map, hero.Pos, steps);
            int foes = 0;
            foreach (var actor in run.Actors)
                if (actor.Team != hero.Team && from[actor.Pos.Y * run.Map.Width + actor.Pos.X] >= 0) foes++;
            return foes;
        }

        /// <summary>
        /// Moving in a fight (PROGRESSION.md, "Battle formation"): a melee hero closes in on a foe (<see cref="TryEngage"/>),
        /// a ranged one goes to a tile it can shoot from (<see cref="TryTakeFiringPosition"/>).
        /// </summary>
        public static bool TryJoinFight(DungeonRun run, Actor hero, out HeroCommand command) =>
            hero.Definition.IsRanged ? TryTakeFiringPosition(run, hero, out command) : TryEngage(run, hero, out command);

        /// <summary>
        /// A melee partner heads for the nearest tile next to a foe in the fight (near the leader): around the allies in
        /// its way when that's a short way (<see cref="DetourSteps"/>), or swapping past a ranged one when that's shorter
        /// and brings it closer to the foes (<see cref="DungeonRun.IsEngageSwap"/>). With no such way in (allies hold the
        /// corridor or the doorway) it closes up behind them and waits there, ready to take a place at the front, instead
        /// of going off to look for another way into the fight.
        /// </summary>
        public static bool TryEngage(DungeonRun run, Actor hero, out HeroCommand command)
        {
            if (!TryFindWayIn(run, hero, out command)) return false;
            // In front, at a doorway, with a crowd beyond it: let them come. Otherwise a Lunge gets there sooner than a step.
            if (command.Kind == HeroCommandKind.Move) command = StepToward(run, hero, command.Direction);
            return true;
        }

        static bool TryFindWayIn(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            var near = NearLeader(run, hero);
            Func<GridPos, bool> isGoal = p => near(p) && IsAttackSpot(run, hero, p);

            // The straight way to the fight, as if no ally stood in it.
            if (!Pathfinder.TryFindNearest(run.Map, hero.Pos, isGoal, p => IsFoeAt(run, hero, p), FightSearchSteps,
                    out var straightStep, out _, out int straightLength))
                return false;
            int steps = Math.Min(FightSearchSteps, straightLength + DetourSteps);
            bool around = Pathfinder.TryFindNearest(run.Map, hero.Pos, isGoal, p => run.ActorAt(p) != null, steps,
                out var aroundStep, out _, out int aroundLength);

            // Through ranged allies it isn't blocked from swapping with: when that's shorter, the first step must be an allowed swap.
            Func<GridPos, bool> blocked = p => run.ActorAt(p) is Actor other && (other.Team != hero.Team || !MaySwapThrough(hero, other));
            bool through = Pathfinder.TryFindNearest(run.Map, hero.Pos, isGoal, blocked, steps,
                out var throughStep, out _, out int throughLength);
            if (through && (!around || throughLength < aroundLength))
            {
                var occupant = run.ActorAt(hero.Pos + throughStep.ToOffset());
                if (occupant == null || MaySwapForward(run, hero, occupant))
                {
                    command = HeroCommand.Move(throughStep);
                    return true;
                }
            }
            if (around)
            {
                command = HeroCommand.Move(aroundStep);
                return true;
            }
            // Close up along the straight way (a ranged ally lets it by), then wait behind the melee ally that holds it.
            var inTheWay = run.ActorAt(hero.Pos + straightStep.ToOffset());
            if (inTheWay == null || MaySwapForward(run, hero, inTheWay)) command = HeroCommand.Move(straightStep);
            return true;
        }

        /// <summary>A swap toward the fight: only a melee hero past a ranged ally, and only closer to the foes.</summary>
        static bool MaySwapForward(DungeonRun run, Actor hero, Actor other) => run.IsEngageSwap(hero, other) && run.CanSwap(hero, other);

        static bool IsFoeAt(DungeonRun run, Actor hero, GridPos pos) => run.ActorAt(pos) is Actor other && other.Team != hero.Team;

        /// <summary>
        /// Where a partner may go to fight: the tiles within <see cref="LeashRange"/> steps' walk of the leader (a foe just
        /// behind a wall can be a long way off). The leader itself goes where it likes.
        /// </summary>
        public static Func<GridPos, bool> NearLeader(DungeonRun run, Actor hero)
        {
            if (hero == run.Hero) return p => true;
            var steps = Pathfinder.StepsFrom(run.Map, run.Hero.Pos, LeashRange);
            int width = run.Map.Width;
            return p => run.Map.InBounds(p) && steps[p.Y * width + p.X] >= 0;
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
        /// it hangs back while the melee heroes close in. A partner picks one near the leader. Around the allies in its
        /// way it walks only when that's a short way (<see cref="DetourSteps"/>): if they fill the corridor between it
        /// and the fight, it stays behind them.
        /// </summary>
        public static bool TryTakeFiringPosition(DungeonRun run, Actor hero, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            int reach = Reach(hero);
            var near = NearLeader(run, hero);
            Func<GridPos, bool> isGoal = p => near(p) && !run.FoeAdjacent(p, hero.Team) && run.AnyFoeInSight(p, hero.Team, reach);
            // How far the nearest such tile is as if no ally stood in the way (or on it).
            if (!Pathfinder.TryFindNearest(run.Map, hero.Pos, isGoal, p => IsFoeAt(run, hero, p), FightSearchSteps, out _, out _, out int straightLength))
                return false;
            if (!Pathfinder.TryFindNearest(run.Map, hero.Pos, isGoal, p => run.ActorAt(p) != null,
                    Math.Min(FightSearchSteps, straightLength + DetourSteps), out var step, out _, out _))
                return false;
            command = HeroCommand.Move(step);
            return true;
        }

        /// <summary>How far the hero's attacks reach (its weapon attack or its longest shot); 1 for a melee hero.</summary>
        public static int Reach(Actor hero)
        {
            int reach = hero.Definition.AttackRange;
            foreach (var skill in hero.Skills)
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

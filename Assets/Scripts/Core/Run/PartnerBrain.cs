using System;

namespace FiveKingdoms.Core
{
    /// <summary>
    /// AI for the party members the player isn't controlling (GAME_PLAN.md, Party). Each turn a partner gets out of a
    /// boss's wind-up and stays out until the slam has landed (PROGRESSION.md, "Footing in a boss fight"), uses its ultimate when it's worth it, heals or guards when the party needs it, gets out of melee if
    /// it's a ranged hero, swaps back behind a healthier ally if it's badly hurt, takes a counter stance when a foe is
    /// about to hit it, and attacks a foe in reach (the marked enemy first, then the lowest HP; <see cref="HeroTactics"/>).
    /// Otherwise it moves: in a fight, melee partners close in on a foe, with a Lunge where that gets there at once,
    /// and ranged ones find a tile to shoot from (PROGRESSION.md, "Battle formation"); while exploring, its
    /// <see cref="PartyTactic"/> decides: Attack goes after foes it can see while staying near the leader, Follow keeps
    /// in line behind the member ahead of it. Hold stays where it is, even in a fight. The party stays together: a
    /// partner whose way is held by its own allies queues up behind them rather than walking around the floor.
    /// Between fights it uses its heal to top the party up, or, hurt with no heal of its own, goes to the partner that
    /// can heal it (PROGRESSION.md, "Heroes heal between fights").
    /// At doorways and in corridors (PROGRESSION.md, "Doorways and corridors") the partner in front holds the door
    /// against a crowd, the one behind takes a hurt partner's place at the front, and the one in a corridor's mouth
    /// makes way; none of it ever moves the leader.
    /// Decides only; <see cref="DungeonRun"/> carries the command out.
    /// </summary>
    public static class PartnerBrain
    {
        /// <summary>How far (in steps) an attacking partner will go after a foe while exploring.</summary>
        const int ChaseSteps = 8;

        const int FarSearchLimit = 200;

        public static HeroCommand Decide(DungeonRun run, Actor partner) => HeroTactics.KeepClear(run, partner, Choose(run, partner));

        static HeroCommand Choose(DungeonRun run, Actor partner)
        {
            if (HeroTactics.TryDodge(run, partner, out var command)) return command;
            bool holds = partner.Tactic == PartyTactic.Hold;
            if (!holds && HeroTactics.TryTakeTheFront(run, partner, out command)) return command;
            if (HeroTactics.TryUltimate(run, partner, out command)) return command;
            if (HeroTactics.TryChallenge(run, partner, out command)) return command;
            if (HeroTactics.TryHealParty(run, partner, out command)) return command;
            if (HeroTactics.TryGuard(run, partner, out command)) return command;
            if (HeroTactics.TryStepOutOfMelee(run, partner, out command)) return command;
            if (HeroTactics.TryRunToSafety(run, partner, out command)) return command;
            if (!holds && HeroTactics.TryFindFooting(run, partner, out command)) return command;
            if (HeroTactics.TryMark(run, partner, out command)) return command;
            if (!holds && HeroTactics.TryMakeWay(run, partner, out command)) return command;
            if (HeroTactics.TryRiposte(run, partner, out command)) return command;
            if (HeroTactics.TryAttack(run, partner, out command)) return command;

            if (holds) return HeroCommand.Wait;
            if (run.InCombat && HeroTactics.TryJoinFight(run, partner, out command)) return command;
            // Between fights a hurt partner that can't mend itself goes to the one that can heal it.
            if (HeroTactics.TrySeekHealer(run, partner, out command)) return command;
            if (partner.Tactic == PartyTactic.Attack && !partner.Definition.IsRanged && TryChase(run, partner, out command)) return command;
            return Follow(run, partner);
        }

        /// <summary>A step toward the nearest foe that is after the party or in plain sight, if it's near the leader.</summary>
        static bool TryChase(DungeonRun run, Actor partner, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            Func<GridPos, bool> blocked = p => run.ActorAt(p) != null;
            var near = HeroTactics.NearLeader(run, partner);
            int best = int.MaxValue;
            foreach (var actor in run.Actors)
            {
                if (actor.Team == partner.Team) continue;
                int distance = GridPos.ChebyshevDistance(partner.Pos, actor.Pos);
                if (!actor.Alerted && distance > run.Config.SightRange) continue;
                if (distance > ChaseSteps || !near(actor.Pos)) continue;
                if (Pathfinder.TryFirstStep(run.Map, partner.Pos, actor.Pos, blocked, ChaseSteps, out var step, out int length) && length < best)
                {
                    best = length;
                    command = HeroCommand.Move(step);
                }
            }
            if (best == int.MaxValue) return false;
            command = HeroTactics.StepToward(run, partner, command.Direction);
            return true;
        }

        /// <summary>
        /// Keep up with the party member ahead in line (the leader, then the others in party order): stay put when next to
        /// it, step into the tile it just left when that's next to us (a tidy line in corridors), otherwise walk toward it.
        /// Party members in the way: around them when that's a short way (<see cref="HeroTactics.DetourSteps"/>).
        /// Otherwise, as in a corridor, it keeps to the straight way: up to whoever stands in it, and past a partner that
        /// comes after it in line (<see cref="DungeonRun.IsRegroupSwap"/>), so a corridor can't split the party.
        /// </summary>
        static HeroCommand Follow(DungeonRun run, Actor partner)
        {
            var ahead = MemberAhead(run, partner);
            if (ahead == null || GridPos.ChebyshevDistance(partner.Pos, ahead.Pos) <= 1) return HeroCommand.Wait;

            var trail = ahead.PreviousPos;
            if (trail != ahead.Pos && GridPos.ChebyshevDistance(partner.Pos, trail) == 1 && run.ActorAt(trail) == null)
            {
                var toward = Directions.Toward(partner.Pos, trail);
                if (run.Map.CanStep(partner.Pos, toward)) return HeroCommand.Move(toward);
            }

            Func<GridPos, bool> foes = p => run.ActorAt(p) is Actor other && other.Team != partner.Team;
            if (!Pathfinder.TryFirstStep(run.Map, partner.Pos, ahead.Pos, foes, FarSearchLimit, out var straight, out int length))
            {
                // Foes hold every way there: walk up to them, to fight a way through.
                if (!Pathfinder.TryFirstStep(run.Map, partner.Pos, ahead.Pos, null, FarSearchLimit, out straight)) return HeroCommand.Wait;
                return run.ActorAt(partner.Pos + straight.ToOffset()) == null ? HeroCommand.Move(straight) : HeroCommand.Wait;
            }
            if (Pathfinder.TryFirstStep(run.Map, partner.Pos, ahead.Pos, p => run.ActorAt(p) != null, length + HeroTactics.DetourSteps, out var around))
                return HeroCommand.Move(around);
            var inTheWay = run.ActorAt(partner.Pos + straight.ToOffset());
            return inTheWay == null || run.IsRegroupSwap(partner, inTheWay) && run.CanSwap(partner, inTheWay)
                ? HeroCommand.Move(straight)
                : HeroCommand.Wait;
        }

        /// <summary>Who this partner follows: the member before it in line (leader first, then party order).</summary>
        public static Actor MemberAhead(DungeonRun run, Actor partner)
        {
            Actor previous = run.Hero;
            foreach (var member in run.Party)
            {
                if (member == run.Hero || !member.IsAlive || run.FindActor(member.Id) == null) continue;
                if (member == partner) return previous;
                previous = member;
            }
            return run.Hero;
        }
    }
}

using System;

namespace FiveKingdoms.Core
{
    /// <summary>
    /// AI for the party members the player isn't controlling (GAME_PLAN.md, Party). Each turn a partner gets out of a
    /// boss's wind-up, uses its ultimate when it's worth it, heals or guards when the party needs it, gets out of melee if
    /// it's a ranged hero, swaps back behind a healthier ally if it's badly hurt, and attacks a foe in reach (the marked
    /// enemy first, then the lowest HP; <see cref="HeroTactics"/>). Otherwise it moves: in a fight, melee partners close
    /// in on a foe and ranged ones find a tile to shoot from (PROGRESSION.md, "Battle formation"); while exploring, its
    /// <see cref="PartyTactic"/> decides: Attack goes after foes it can see while staying near the leader, Follow keeps
    /// in line behind the member ahead of it. Hold stays where it is, even in a fight. Decides only;
    /// <see cref="DungeonRun"/> carries the command out.
    /// </summary>
    public static class PartnerBrain
    {
        /// <summary>How far (in steps) an attacking partner will go after a foe while exploring.</summary>
        const int ChaseSteps = 8;

        /// <summary>An attacking partner only goes after foes this close to the leader, so the party doesn't scatter.</summary>
        const int LeashRange = 6;

        const int FarSearchLimit = 200;

        public static HeroCommand Decide(DungeonRun run, Actor partner)
        {
            if (HeroTactics.TryDodge(run, partner, out var command)) return command;
            if (HeroTactics.TryUltimate(run, partner, out command)) return command;
            if (HeroTactics.TryHealParty(run, partner, out command)) return command;
            if (HeroTactics.TryGuard(run, partner, out command)) return command;
            if (HeroTactics.TryStepOutOfMelee(run, partner, out command)) return command;
            if (HeroTactics.TryRunToSafety(run, partner, out command)) return command;
            if (HeroTactics.TryMark(run, partner, out command)) return command;
            if (HeroTactics.TryAttack(run, partner, out command)) return command;

            if (partner.Tactic == PartyTactic.Hold) return HeroCommand.Wait;
            if (run.InCombat && HeroTactics.TryJoinFight(run, partner, out command)) return command;
            if (partner.Tactic == PartyTactic.Attack && !partner.Definition.IsRanged && TryChase(run, partner, out command)) return command;
            return Follow(run, partner);
        }

        /// <summary>A step toward the nearest foe that is after the party or in plain sight, if it's near the leader.</summary>
        static bool TryChase(DungeonRun run, Actor partner, out HeroCommand command)
        {
            command = HeroCommand.Wait;
            Func<GridPos, bool> blocked = p => run.ActorAt(p) != null;
            int best = int.MaxValue;
            foreach (var actor in run.Actors)
            {
                if (actor.Team == partner.Team) continue;
                int distance = GridPos.ChebyshevDistance(partner.Pos, actor.Pos);
                if (!actor.Alerted && distance > run.Config.SightRange) continue;
                if (distance > ChaseSteps || GridPos.ChebyshevDistance(run.Hero.Pos, actor.Pos) > LeashRange) continue;
                if (Pathfinder.TryFirstStep(run.Map, partner.Pos, actor.Pos, blocked, ChaseSteps, out var step, out int length) && length < best)
                {
                    best = length;
                    command = HeroCommand.Move(step);
                }
            }
            return best != int.MaxValue;
        }

        /// <summary>
        /// Keep up with the party member ahead in line (the leader, then the others in party order): stay put when next to
        /// it, step into the tile it just left when that's next to us (a tidy line in corridors), otherwise walk toward it.
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
            return Pathfinder.TryFirstStep(run.Map, partner.Pos, ahead.Pos, p => run.ActorAt(p) != null, FarSearchLimit, out var step)
                ? HeroCommand.Move(step)
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

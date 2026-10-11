namespace FiveKingdoms.Core
{
    /// <summary>
    /// Something that happened during a turn, recorded in order. The presentation layer animates these;
    /// the rules never read them back.
    /// </summary>
    public abstract class GameEvent { }

    public sealed class FloorStartedEvent : GameEvent
    {
        public readonly int Floor;
        public FloorStartedEvent(int floor) { Floor = floor; }
    }

    public sealed class MovedEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly GridPos From;
        public readonly GridPos To;
        public readonly Direction8 Direction;

        public MovedEvent(int actorId, GridPos from, GridPos to, Direction8 direction)
        {
            ActorId = actorId;
            From = from;
            To = to;
            Direction = direction;
        }
    }

    /// <summary>The actor turned in place (for example, the hero bumped into a wall).</summary>
    public sealed class FacingChangedEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly Direction8 Direction;

        public FacingChangedEvent(int actorId, Direction8 direction)
        {
            ActorId = actorId;
            Direction = direction;
        }
    }

    /// <summary>
    /// A swing or a shot. TargetId is -1 for a miss; a DamageEvent follows when it connects. A shot can fly at any angle:
    /// <see cref="To"/> is the tile it lands on, <see cref="Direction"/> the nearest of the 8 directions.
    /// </summary>
    public sealed class AttackEvent : GameEvent
    {
        public readonly int AttackerId;
        public readonly int TargetId;
        public readonly Direction8 Direction;
        public readonly bool Ranged;

        /// <summary>Tiles from the attacker to the target, or to where a missed shot stopped.</summary>
        public readonly int Distance;

        /// <summary>The tile it lands on: the target's, or where a missed shot stopped.</summary>
        public readonly GridPos To;

        /// <summary>
        /// Where a shot comes from when that isn't its shooter: an arrow that bounced flies on from the foe it hit
        /// before (Bouncing Shot). Null otherwise.
        /// </summary>
        public readonly GridPos? From;

        public AttackEvent(int attackerId, int targetId, Direction8 direction, GridPos to, bool ranged = false, int distance = 1,
            GridPos? from = null)
        {
            From = from;
            AttackerId = attackerId;
            TargetId = targetId;
            Direction = direction;
            To = to;
            Ranged = ranged;
            Distance = distance;
        }
    }

    public sealed class DamageEvent : GameEvent
    {
        public readonly int TargetId;
        public readonly int Amount;
        public readonly bool Critical;
        public readonly int HpAfter;

        public DamageEvent(int targetId, int amount, bool critical, int hpAfter)
        {
            TargetId = targetId;
            Amount = amount;
            Critical = critical;
            HpAfter = hpAfter;
        }
    }

    public sealed class DiedEvent : GameEvent
    {
        public readonly int ActorId;
        public DiedEvent(int actorId) { ActorId = actorId; }
    }

    public sealed class ExpGainedEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly int Amount;

        public ExpGainedEvent(int actorId, int amount)
        {
            ActorId = actorId;
            Amount = amount;
        }
    }

    public sealed class LevelUpEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly int Level;

        public LevelUpEvent(int actorId, int level)
        {
            ActorId = actorId;
            Level = level;
        }
    }

    public sealed class HealedEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly int Amount;
        public readonly int HpAfter;

        public HealedEvent(int actorId, int amount, int hpAfter)
        {
            ActorId = actorId;
            Amount = amount;
            HpAfter = hpAfter;
        }
    }

    public sealed class ItemPickedUpEvent : GameEvent
    {
        public readonly int ItemId;
        public readonly ItemKind Kind;

        public ItemPickedUpEvent(int itemId, ItemKind kind)
        {
            ItemId = itemId;
            Kind = kind;
        }
    }

    /// <summary>An item from the bag was used; its effects (heal, mana) follow as their own events.</summary>
    public sealed class ItemUsedEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly ItemKind Kind;

        public ItemUsedEvent(int actorId, ItemKind kind)
        {
            ActorId = actorId;
            Kind = kind;
        }
    }

    public sealed class ActorSpawnedEvent : GameEvent
    {
        public readonly int ActorId;
        public ActorSpawnedEvent(int actorId) { ActorId = actorId; }
    }

    public enum BossAction
    {
        /// <summary>Winding up: the special attack lands on the boss's next turn.</summary>
        Charge,

        /// <summary>The area attack; DamageEvents for everyone caught follow.</summary>
        Slam,

        /// <summary>Called for help; ActorSpawnedEvents for the arrivals follow.</summary>
        Summon,
    }

    public sealed class BossActionEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly BossAction Action;

        public BossActionEvent(int actorId, BossAction action)
        {
            ActorId = actorId;
            Action = action;
        }
    }

    /// <summary>
    /// How a hero stood toward a boss's wind-up when its latest turn began (PROGRESSION.md, "Footing in a boss fight").
    /// </summary>
    public enum SlamFooting
    {
        /// <summary>It has had no turn since the wind-up began.</summary>
        NoTurn,

        /// <summary>Out of the slam's reach.</summary>
        OutOfReach,

        /// <summary>In reach, with a way out (<see cref="HeroTactics.HasWayOut"/>).</summary>
        WayOut,

        /// <summary>In reach with no way out: its back to a wall, a pillar or a corner, or boxed in.</summary>
        Cornered,
    }

    /// <summary>
    /// The slam that follows catches this hero (the BossActionEvent comes next, then the DamageEvents).
    /// <see cref="Footing"/> says how it stood when its last turn began, so the balance report can count the slams
    /// that hit a hero who had no way out; <see cref="Braced"/>: it takes the blow behind a guard, a stance or an aura.
    /// </summary>
    public sealed class SlamCaughtEvent : GameEvent
    {
        public readonly int BossId;
        public readonly int TargetId;
        public readonly SlamFooting Footing;
        public readonly bool Braced;

        public SlamCaughtEvent(int bossId, int targetId, SlamFooting footing, bool braced)
        {
            BossId = bossId;
            TargetId = targetId;
            Footing = footing;
            Braced = braced;
        }
    }

    /// <summary>
    /// A skill or ultimate was used; its effects (attack, heal, dash, charge) follow as their own events. Carries the
    /// hero and skill, so a presentation layer can show a manga panel or an ultimate's cutscene.
    /// </summary>
    public sealed class SkillUsedEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly SkillDefinition Skill;

        public SkillUsedEvent(int actorId, SkillDefinition skill)
        {
            ActorId = actorId;
            Skill = skill;
        }
    }

    /// <summary>A hero's ultimate charge went up (positive) or was used up by its ultimate (negative).</summary>
    public sealed class ChargeChangedEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly int Amount;
        public readonly int ChargeAfter;

        public ChargeChangedEvent(int actorId, int amount, int chargeAfter)
        {
            ActorId = actorId;
            Amount = amount;
            ChargeAfter = chargeAfter;
        }
    }

    /// <summary>
    /// A hero in a counter stance (Riposte) answers the foe that just hit it; the blow follows as an AttackEvent and its
    /// DamageEvent. It comes in the middle of the foe's own turn.
    /// </summary>
    public sealed class CounterEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly int TargetId;

        public CounterEvent(int actorId, int targetId)
        {
            ActorId = actorId;
            TargetId = targetId;
        }
    }

    /// <summary>
    /// An area skill comes down on every tile within Radius of Center. Volley: the hits follow as DamageEvents. Blade
    /// Dance: each strike follows as its own AttackEvent and DamageEvent.
    /// </summary>
    public sealed class AreaAttackEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly GridPos Center;
        public readonly int Radius;

        public AreaAttackEvent(int actorId, GridPos center, int radius)
        {
            ActorId = actorId;
            Center = center;
            Radius = radius;
        }
    }

    /// <summary>Why two party members traded places, so the view can say what the party's AI is up to.</summary>
    public enum SwapReason
    {
        /// <summary>Just passing: the player walked into a partner, or the leader's autopilot did on its way.</summary>
        Passing,

        /// <summary>A melee hero went past a ranged one to get at the foes.</summary>
        Engage,

        /// <summary>A badly hurt hero ran behind a healthier ally ("run to safety").</summary>
        Safety,

        /// <summary>A partner got past the one that follows it in line.</summary>
        Regroup,

        /// <summary>The hurt hero that held a corridor or a doorway gave the front to the fresh one behind it ("rotate the front").</summary>
        Rotate,

        /// <summary>A hero boxed in under a slam it wouldn't survive traded places with an ally that will ("Footing in a boss fight").</summary>
        Shelter,
    }

    /// <summary>Two party members swapped places (their moves follow as MovedEvents).</summary>
    public sealed class SwappedEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly int OtherId;
        public readonly SwapReason Reason;

        /// <summary>
        /// The hero that stepped back: the hurt one (<see cref="SwapReason.Safety"/>, <see cref="SwapReason.Rotate"/>),
        /// or the one that got out from under a slam (<see cref="SwapReason.Shelter"/>); else -1.
        /// </summary>
        public readonly int HurtId;

        public SwappedEvent(int actorId, int otherId, SwapReason reason = SwapReason.Passing, int hurtId = -1)
        {
            ActorId = actorId;
            OtherId = otherId;
            Reason = reason;
            HurtId = hurtId;
        }
    }

    /// <summary>Why a hero stood still on purpose.</summary>
    public enum WaitReason
    {
        /// <summary>It holds a doorway and lets the foes come to it (PROGRESSION.md, "Doorways and corridors").</summary>
        HoldsTheDoor,

        /// <summary>The autopilot's leader waits while the party heals up between fights ("Heroes heal between fights").</summary>
        Rests,

        /// <summary>It stays out of the reach of a slam that is winding up, until it has come down ("Footing in a boss fight").</summary>
        KeepsClear,
    }

    /// <summary>
    /// A hero's AI passed its turn on purpose, so the view can say why: it isn't stuck. <see cref="Turns"/> counts the
    /// waits in a row so far (1 for the first).
    /// </summary>
    public sealed class HeroWaitedEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly WaitReason Reason;
        public readonly int Turns;

        public HeroWaitedEvent(int actorId, WaitReason reason, int turns)
        {
            ActorId = actorId;
            Reason = reason;
            Turns = turns;
        }
    }

    /// <summary>A multi-tile dash in one motion.</summary>
    public sealed class DashedEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly GridPos From;
        public readonly GridPos To;
        public readonly Direction8 Direction;

        public DashedEvent(int actorId, GridPos from, GridPos to, Direction8 direction)
        {
            ActorId = actorId;
            From = from;
            To = to;
            Direction = direction;
        }
    }

    /// <summary>An enemy noticed the hero: turns now follow the action-value timeline until no enemy is alerted.</summary>
    public sealed class CombatStartedEvent : GameEvent { }

    /// <summary>No enemy is chasing the hero any more: back to exploring.</summary>
    public sealed class CombatEndedEvent : GameEvent { }

    public sealed class RunEndedEvent : GameEvent
    {
        public readonly bool Won;
        public RunEndedEvent(bool won) { Won = won; }
    }

    /// <summary>The player now controls this party member (switched by hand, or because the leader fell).</summary>
    public sealed class LeaderChangedEvent : GameEvent
    {
        public readonly int ActorId;

        public LeaderChangedEvent(int actorId)
        {
            ActorId = actorId;
        }
    }

    public sealed class StatusAppliedEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly StatusKind Kind;
        public readonly int SourceId;

        /// <summary>How strong it is, in percent (a guard's cut, a mark's bonus as it starts); 0 for those without a number.</summary>
        public readonly int Power;

        public StatusAppliedEvent(int actorId, StatusKind kind, int sourceId, int power = 0)
        {
            ActorId = actorId;
            Kind = kind;
            SourceId = sourceId;
            Power = power;
        }
    }

    /// <summary>
    /// An always-on mark grew (the Archer's Deadly Mark): <see cref="HunterId"/> shot the foe that carries its mark
    /// again, and from this shot on the foe takes <see cref="Power"/>% more damage from that hunter.
    /// </summary>
    public sealed class MarkBuiltEvent : GameEvent
    {
        public readonly int HunterId;
        public readonly int ActorId;
        public readonly int Power;

        public MarkBuiltEvent(int hunterId, int actorId, int power)
        {
            HunterId = hunterId;
            ActorId = actorId;
            Power = power;
        }
    }

    public sealed class StatusEndedEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly StatusKind Kind;

        public StatusEndedEvent(int actorId, StatusKind kind)
        {
            ActorId = actorId;
            Kind = kind;
        }
    }

    /// <summary>
    /// A delay: the actor's next turn was pushed back by this percent of one of its turns (a stun, a slow, a snare under
    /// a boss). An actor can be delayed at most once per its own turn, so no two of these for one actor carry the same
    /// <see cref="TurnsTaken"/>.
    /// </summary>
    public sealed class TurnDelayedEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly int Percent;

        /// <summary>How many turns the actor had taken when it was delayed (<see cref="Actor.TurnsTaken"/>).</summary>
        public readonly int TurnsTaken;

        /// <summary>It was a stun (Stun Strike) rather than a slow or a snare.</summary>
        public readonly bool Stun;

        public TurnDelayedEvent(int actorId, int percent, int turnsTaken, bool stun = false)
        {
            ActorId = actorId;
            Percent = percent;
            TurnsTaken = turnsTaken;
            Stun = stun;
        }
    }

    public sealed class TrapPlacedEvent : GameEvent
    {
        public readonly int TrapId;
        public readonly TrapKind Kind;
        public readonly GridPos Pos;

        public TrapPlacedEvent(int trapId, TrapKind kind, GridPos pos)
        {
            TrapId = trapId;
            Kind = kind;
            Pos = pos;
        }
    }

    /// <summary>A trap went off under an enemy (its effect follows as a status) and is gone.</summary>
    public sealed class TrapTriggeredEvent : GameEvent
    {
        public readonly int TrapId;
        public readonly int ActorId;

        public TrapTriggeredEvent(int trapId, int actorId)
        {
            TrapId = trapId;
            ActorId = actorId;
        }
    }

    /// <summary>Someone was shoved or knocked back. The move itself follows as a MovedEvent.</summary>
    public sealed class PushedEvent : GameEvent
    {
        public readonly int ActorId;
        public readonly bool Blocked;

        public PushedEvent(int actorId, bool blocked)
        {
            ActorId = actorId;
            Blocked = blocked;
        }
    }
}

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

    /// <summary>An attack swing. TargetId is -1 when it hit nothing. A DamageEvent follows when it connects.</summary>
    public sealed class AttackEvent : GameEvent
    {
        public readonly int AttackerId;
        public readonly int TargetId;
        public readonly Direction8 Direction;

        public AttackEvent(int attackerId, int targetId, Direction8 direction)
        {
            AttackerId = attackerId;
            TargetId = targetId;
            Direction = direction;
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

    public sealed class RunEndedEvent : GameEvent
    {
        public readonly bool Won;
        public RunEndedEvent(bool won) { Won = won; }
    }
}

using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>One upcoming turn on the timeline: who, when (AV since the fight began) and in which cycle.</summary>
    public readonly struct TimelineTurn
    {
        public readonly Actor Actor;
        public readonly AvTime Time;
        public readonly int Cycle;

        public TimelineTurn(Actor actor, AvTime time, int cycle)
        {
            Actor = actor;
            Time = time;
            Cycle = cycle;
        }

        public override string ToString() => $"{Actor.Name}#{Actor.Id} @ {Time} (cycle {Cycle})";
    }

    /// <summary>
    /// The combat turn order, in the style of Honkai: Star Rail's action value (AV). Each actor's next turn comes one
    /// gauge (10000) divided by its Speed after its last one, scaled by the cost of the action it took, and whoever's
    /// turn comes soonest goes next. Ties go to the leader, then to the lower actor id. All arithmetic is exact
    /// (<see cref="AvTime"/>), so breakpoints hold: Speed 134 acts twice in the 150 AV first cycle, Speed 133 doesn't.
    /// Handles any number of actors per team.
    /// </summary>
    public sealed class Timeline
    {
        /// <summary>Gauge every actor covers between turns; one turn at Speed S lasts Gauge / S AV.</summary>
        public const int Gauge = 10000;

        public const int FirstCycleLength = 150;
        public const int CycleLength = 100;

        readonly Dictionary<int, Slot> slots = new Dictionary<int, Slot>();

        sealed class Slot
        {
            public Actor Actor;
            public AvTime Next;
        }

        /// <summary>AV elapsed since the fight began.</summary>
        public AvTime Now { get; private set; }

        public int Count => slots.Count;

        /// <summary>How long one turn lasts at <paramref name="speed"/>, for an action costing <paramref name="costPercent"/>% of a normal turn.</summary>
        public static AvTime TurnLength(int speed, int costPercent = 100) => new AvTime((long)Gauge * costPercent, 100L * speed);

        /// <summary>
        /// The cycle a moment falls in: cycle 0 is the first 150 AV, every later cycle is 100 AV. A moment exactly on
        /// a boundary belongs to the cycle that ends there (Speed 120's third turn, at exactly 250, is still cycle 1).
        /// </summary>
        public static int CycleOf(AvTime time)
        {
            var firstCycleEnd = AvTime.FromWhole(FirstCycleLength);
            if (time <= firstCycleEnd) return 0;
            return (int)(time - firstCycleEnd).Scale(1, CycleLength).Ceiling();
        }

        /// <summary>A fight begins: the clock returns to 0 and everyone's first turn is one full turn away.</summary>
        public void Start(IEnumerable<Actor> actors)
        {
            Clear();
            foreach (var actor in actors) Add(actor);
        }

        public void Clear()
        {
            slots.Clear();
            Now = AvTime.Zero;
        }

        /// <summary>
        /// Someone joins the fight. Normally their first turn is one full turn from now (a summoned helper).
        /// <paramref name="readyNow"/> gives them a turn at the current moment instead, after everyone already due
        /// now (a reinforcement that walked in between rounds, as it did before the timeline existed).
        /// </summary>
        public void Add(Actor actor, bool readyNow = false) =>
            slots[actor.Id] = new Slot { Actor = actor, Next = readyNow ? Now : Now + TurnLength(actor.Speed) };

        public void Remove(int actorId) => slots.Remove(actorId);

        public bool Contains(int actorId) => slots.ContainsKey(actorId);

        public AvTime NextTurnOf(int actorId) => slots[actorId].Next;

        /// <summary>Who acts next: the soonest turn; ties go to <paramref name="leader"/>, then the lower actor id. Null when empty.</summary>
        public Actor PeekNext(Actor leader)
        {
            Slot best = null;
            foreach (var slot in slots.Values)
                if (best == null || GoesBefore(slot.Actor, slot.Next, best.Actor, best.Next, leader)) best = slot;
            return best?.Actor;
        }

        /// <summary>Moves the clock forward to <paramref name="actor"/>'s turn; call with the actor <see cref="PeekNext"/> returned.</summary>
        public void AdvanceTo(Actor actor) => Now = slots[actor.Id].Next;

        /// <summary>After acting, the actor's next turn is one turn after now, scaled by what the action cost.</summary>
        public void EndTurn(Actor actor, int costPercent) => slots[actor.Id].Next = Now + TurnLength(actor.Speed, costPercent);

        /// <summary>
        /// Changes an actor's speed mid-fight. The distance it still has to cover is kept and its next turn is
        /// recomputed at the new speed; the gauge is not reset.
        /// </summary>
        public void ChangeSpeed(Actor actor, int newSpeed)
        {
            if (slots.TryGetValue(actor.Id, out var slot))
                slot.Next = Now + (slot.Next - Now).Scale(actor.Speed, newSpeed); // Remaining distance = time left x old speed.
            actor.Speed = newSpeed;
        }

        /// <summary>The next <paramref name="count"/> turns in order, assuming every action costs a normal turn. Changes nothing.</summary>
        public List<TimelineTurn> Forecast(Actor leader, int count)
        {
            var upcoming = new List<(Actor actor, AvTime next)>();
            foreach (var slot in slots.Values) upcoming.Add((slot.Actor, slot.Next));
            var turns = new List<TimelineTurn>(count);
            while (turns.Count < count && upcoming.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < upcoming.Count; i++)
                    if (GoesBefore(upcoming[i].actor, upcoming[i].next, upcoming[best].actor, upcoming[best].next, leader)) best = i;
                var (actor, next) = upcoming[best];
                turns.Add(new TimelineTurn(actor, next, CycleOf(next)));
                upcoming[best] = (actor, next + TurnLength(actor.Speed));
            }
            return turns;
        }

        static bool GoesBefore(Actor a, AvTime aTime, Actor b, AvTime bTime, Actor leader)
        {
            int byTime = aTime.CompareTo(bTime);
            if (byTime != 0) return byTime < 0;
            if ((a == leader) != (b == leader)) return a == leader;
            return a.Id < b.Id;
        }
    }
}

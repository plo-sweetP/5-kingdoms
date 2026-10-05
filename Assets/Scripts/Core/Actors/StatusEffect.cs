namespace FiveKingdoms.Core
{
    /// <summary>
    /// Status effects on an actor, kept few and short (they show as icons). These come from the starting party's kits
    /// (PROGRESSION.md); poison, paralysis, sleep, bleed and slow come later. A stun isn't one of them: it pushes the
    /// target's next turn back on the timeline (<see cref="Actor.IsDelayed"/>) instead of skipping it.
    /// </summary>
    public enum StatusKind
    {
        /// <summary>Takes <see cref="StatusEffect.Power"/>% less damage until the guard's caster takes its next turn.</summary>
        Guard,

        /// <summary>Goes after the taunter instead of the nearest foe, while it can reach it (Shoulder Bash).</summary>
        Taunt,

        /// <summary>Takes <see cref="StatusEffect.Power"/>% more damage from whoever marked it (Hunter's Mark).</summary>
        Mark,

        /// <summary>Can't move, though it can still attack what's next to it (a snare trap).</summary>
        Rooted,

        /// <summary>
        /// On the aura's holder: it and the allies next to it take <see cref="StatusEffect.Power"/>% less damage, and it
        /// heals them all at the start of each of its turns, for <see cref="StatusEffect.TurnsLeft"/> of them (Aura of
        /// Protection).
        /// </summary>
        Aura,

        /// <summary>
        /// A counter stance, until its holder's next turn (Riposte): it takes <see cref="StatusEffect.Power"/>% less
        /// damage (<see cref="StatusEffect.BossPower"/>% from a boss), and the first foe that hits it from the next
        /// tile is struck back for <see cref="StatusEffect.CounterPercent"/>% of its ATK.
        /// </summary>
        Riposte,
    }

    public sealed class StatusEffect
    {
        public StatusEffect(StatusKind kind, int sourceId, int power, int turns, bool endsOnSourceTurn, int healPercent = 0,
            int bossPower = 0, int counterPercent = 0)
        {
            Kind = kind;
            SourceId = sourceId;
            Power = power;
            TurnsLeft = turns;
            EndsOnSourceTurn = endsOnSourceTurn;
            HealPercent = healPercent;
            BossPower = bossPower;
            CounterPercent = counterPercent;
        }

        public StatusKind Kind { get; }

        /// <summary>Who applied it.</summary>
        public int SourceId { get; }

        public int Power { get; }

        /// <summary>The owner's own turns left; unused when it ends on the source's turn instead.</summary>
        public int TurnsLeft { get; set; }

        /// <summary>Ends when the source's next turn starts (a guard lasts "until Haiden acts again").</summary>
        public bool EndsOnSourceTurn { get; }

        /// <summary>An aura's heal at the start of each of its holder's turns, in percent of the holder's max HP.</summary>
        public int HealPercent { get; }

        /// <summary>A counter stance: what it takes off a boss's hits, in percent (<see cref="Power"/> is for everyone else's).</summary>
        public int BossPower { get; }

        /// <summary>
        /// A counter stance: the blow that answers the first foe to hit its holder from the next tile, in percent of
        /// the holder's ATK. 0 once that blow has been struck: one counter per stance, while the damage cut stays.
        /// </summary>
        public int CounterPercent { get; set; }
    }
}

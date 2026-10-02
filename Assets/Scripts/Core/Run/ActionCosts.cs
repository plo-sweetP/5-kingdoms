using System;

namespace FiveKingdoms.Core
{
    public enum ActionKind { Move, Attack, Wait, Item, Special }

    /// <summary>
    /// How much of a normal turn's AV each kind of action costs, in percent. Everything costs a full turn for now;
    /// skills will cost more or less later (a quick jab at 70%, a heavy blow at 130%).
    /// </summary>
    public sealed class ActionCosts
    {
        public int Move = 100;
        public int Attack = 100;
        public int Wait = 100;
        public int Item = 100;

        /// <summary>Boss moves: wind-ups, slams, calls for help.</summary>
        public int Special = 100;

        public int PercentFor(ActionKind kind)
        {
            switch (kind)
            {
                case ActionKind.Move: return Move;
                case ActionKind.Attack: return Attack;
                case ActionKind.Wait: return Wait;
                case ActionKind.Item: return Item;
                case ActionKind.Special: return Special;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
    }
}

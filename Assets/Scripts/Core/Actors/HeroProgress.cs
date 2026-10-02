using System;

namespace FiveKingdoms.Core
{
    /// <summary>
    /// A character's progress outside any one dungeon run: level and EXP for now, gear later. A run starts the
    /// hero from it and records every EXP gain back into it as it happens, so progress is kept win or lose.
    /// </summary>
    public sealed class HeroProgress
    {
        public HeroProgress(ActorDefinition definition, int level = 1, int exp = 0)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Level = Math.Max(1, level);
            Exp = Math.Max(0, exp);
        }

        public ActorDefinition Definition { get; }
        public int Level { get; private set; }
        public int Exp { get; private set; }

        internal void Record(int level, int exp)
        {
            Level = level;
            Exp = exp;
        }

        public override string ToString() => $"{Definition.Name} Lv {Level} ({Exp} EXP)";
    }
}

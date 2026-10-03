using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>One thing an action can be aimed at: a foe in reach, or (for rolls and dashes) a tile to move to.</summary>
    public readonly struct AimOption
    {
        public AimOption(GridPos tile, Direction8 direction, Actor target, int distance)
        {
            Tile = tile;
            Direction = direction;
            Target = target;
            Distance = distance;
        }

        /// <summary>The foe's tile, or where the user would end up.</summary>
        public GridPos Tile { get; }

        /// <summary>
        /// Which of the 8 ways it lies from the user (the nearest, for a foe off the 8 lines): what a direction press
        /// picks it by, and for a roll or a dash the way the command is aimed.
        /// </summary>
        public Direction8 Direction { get; }

        /// <summary>The foe it would hit; null for a move.</summary>
        public Actor Target { get; }

        /// <summary>Tiles from the user (counting diagonals as one).</summary>
        public int Distance { get; }

        /// <summary>
        /// The command that carries the action out on this option: an attack, skill or ultimate on the foe's tile, or a
        /// roll or dash that way. <paramref name="slot"/> is the skill's slot (ignored otherwise).
        /// </summary>
        public HeroCommand ToCommand(HeroCommandKind kind, int slot)
        {
            if (kind != HeroCommandKind.Attack && kind != HeroCommandKind.Skill && kind != HeroCommandKind.Ultimate)
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only attacks, skills and ultimates are aimed.");
            if (Target == null)
                return kind == HeroCommandKind.Attack ? HeroCommand.AttackToward(Direction)
                    : kind == HeroCommandKind.Ultimate ? HeroCommand.Ultimate(Direction)
                    : HeroCommand.Skill(slot, Direction);
            return kind == HeroCommandKind.Attack ? HeroCommand.AttackAt(Tile)
                : kind == HeroCommandKind.Ultimate ? HeroCommand.UltimateAt(Tile)
                : HeroCommand.SkillAt(slot, Tile);
        }
    }

    /// <summary>
    /// Where an attack, skill or ultimate can reach and what it can be aimed at, for the two-step targeting
    /// (PROGRESSION.md, "Targeting and input"): the tiles it reaches, the valid targets, for area skills the size of the
    /// area around each target, and which target is marked to begin with. Built by <see cref="DungeonRun.AimFor"/>.
    /// </summary>
    public sealed class AimInfo
    {
        readonly List<GridPos> reach = new List<GridPos>();
        readonly List<AimOption> options = new List<AimOption>();

        /// <summary>False for actions that aren't aimed (heals, guards, auras): they just happen.</summary>
        public bool NeedsAim { get; internal set; }

        /// <summary>The tiles the action reaches (everything in sight for shots, the neighbors for strikes, paths for rolls).</summary>
        public IReadOnlyList<GridPos> Reach => reach;

        /// <summary>Everything it can be aimed at; empty when nothing is in reach.</summary>
        public IReadOnlyList<AimOption> Options => options;

        /// <summary>Area skills (Volley): every tile within this many steps of the target is hit. 0 otherwise.</summary>
        public int AreaRadius { get; internal set; }

        /// <summary>Rolls and dashes pick a tile to move to rather than a foe.</summary>
        public bool PicksTile { get; internal set; }

        /// <summary>
        /// The option marked when aiming starts, so a lone valid target only needs confirming: the nearest foe the way the
        /// user faces, else the nearest foe; for a roll or a dash, the spot farthest from the foes. -1 with no options.
        /// </summary>
        public int Default { get; internal set; } = -1;

        internal void AddReach(GridPos tile)
        {
            if (!reach.Contains(tile)) reach.Add(tile);
        }

        internal void AddOption(AimOption option) => options.Add(option);

        /// <summary>The option aimed at <paramref name="tile"/> (a target standing there, or a move ending there), if any.</summary>
        public bool TryFind(GridPos tile, out AimOption option)
        {
            int index = IndexAt(tile);
            option = index >= 0 ? options[index] : default;
            return index >= 0;
        }

        /// <summary>Index of the option on <paramref name="tile"/>, or -1.</summary>
        public int IndexAt(GridPos tile)
        {
            for (int i = 0; i < options.Count; i++)
                if (options[i].Tile == tile) return i;
            return -1;
        }

        /// <summary>
        /// The nearest option lying exactly <paramref name="direction"/> of the user, or -1: what holding a direction while
        /// pressing an action fires at, at once.
        /// </summary>
        public int NearestToward(Direction8 direction)
        {
            int best = -1;
            for (int i = 0; i < options.Count; i++)
                if (options[i].Direction == direction && (best < 0 || options[i].Distance < options[best].Distance)) best = i;
            return best;
        }

        /// <summary>
        /// Where a direction press moves the mark from <paramref name="selected"/>: to the nearest option that way (or, if
        /// none lies that way, in the closest direction to it); pressed again, to the next one out the same way, then
        /// around to the nearest again. So every target can be reached with the direction keys, even several one way.
        /// </summary>
        public int Toward(Direction8 direction, int selected)
        {
            if (options.Count == 0) return selected;
            int closest = int.MaxValue;
            foreach (var option in options) closest = Math.Min(closest, Turn(option.Direction, direction));

            // The options that way, nearest first (in the order they were found between equals).
            var group = new List<int>();
            for (int i = 0; i < options.Count; i++)
            {
                if (Turn(options[i].Direction, direction) != closest) continue;
                int at = group.Count;
                while (at > 0 && options[group[at - 1]].Distance > options[i].Distance) at--;
                group.Insert(at, i);
            }
            return group[(group.IndexOf(selected) + 1) % group.Count];
        }

        /// <summary>Eighths of a turn between two directions (0-4).</summary>
        static int Turn(Direction8 a, Direction8 b)
        {
            int turn = Math.Abs((int)a - (int)b) % 8;
            return Math.Min(turn, 8 - turn);
        }
    }
}

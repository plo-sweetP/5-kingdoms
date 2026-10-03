using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>One thing an action can be aimed at: a foe in reach, or (for rolls and dashes) a tile to move to.</summary>
    public readonly struct AimOption
    {
        public AimOption(GridPos tile, Direction8 direction, Actor target)
        {
            Tile = tile;
            Direction = direction;
            Target = target;
        }

        /// <summary>The foe's tile, or where the user would end up.</summary>
        public GridPos Tile { get; }

        /// <summary>The direction to aim the command (HeroCommand.Skill(slot, Direction) and the like).</summary>
        public Direction8 Direction { get; }

        /// <summary>The foe it would hit; null for a move.</summary>
        public Actor Target { get; }
    }

    /// <summary>
    /// Where an attack, skill or ultimate can reach and what it can be aimed at, for the targeting highlight
    /// (PROGRESSION.md, "Attack range highlight"): the tiles it reaches, the valid targets, and for area skills the size
    /// of the area around each target. Built by <see cref="DungeonRun.AimFor"/>.
    /// </summary>
    public sealed class AimInfo
    {
        readonly List<GridPos> reach = new List<GridPos>();
        readonly List<AimOption> options = new List<AimOption>();

        /// <summary>False for actions that aren't aimed (heals, guards, auras): they just happen.</summary>
        public bool NeedsAim { get; internal set; }

        /// <summary>The tiles the action reaches (lines for shots, the neighbors for strikes, paths for rolls).</summary>
        public IReadOnlyList<GridPos> Reach => reach;

        /// <summary>Everything it can be aimed at; empty when nothing is in reach.</summary>
        public IReadOnlyList<AimOption> Options => options;

        /// <summary>Area skills (Volley): every tile within this many steps of the target is hit. 0 otherwise.</summary>
        public int AreaRadius { get; internal set; }

        /// <summary>Rolls and dashes pick a tile to move to rather than a foe.</summary>
        public bool PicksTile { get; internal set; }

        internal void AddReach(GridPos tile)
        {
            if (!reach.Contains(tile)) reach.Add(tile);
        }

        internal void AddOption(AimOption option) => options.Add(option);

        /// <summary>The option aimed at <paramref name="tile"/> (a target standing there, or a move ending there), if any.</summary>
        public bool TryFind(GridPos tile, out AimOption option)
        {
            foreach (var candidate in options)
            {
                if (candidate.Tile != tile) continue;
                option = candidate;
                return true;
            }
            option = default;
            return false;
        }
    }
}

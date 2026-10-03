using System;

namespace FiveKingdoms.Core
{
    /// <summary>Tuning for one dungeon. Defaults describe the prototype "Slime Cave".</summary>
    public sealed class DungeonRunConfig
    {
        public string Name = "Slime Cave";
        public int FloorCount = 5;
        public DungeonGenConfig Generation = new DungeonGenConfig();

        /// <summary>Hero used when a run starts without a <see cref="HeroProgress"/>.</summary>
        public ActorDefinition Hero = ActorCatalog.Uzuki;
        public ActorDefinition Enemy = ActorCatalog.Slime;

        /// <summary>
        /// Boss waiting in an arena on the last floor; defeating it clears the dungeon.
        /// Null: the last floor is an ordinary floor whose stairs clear the dungeon.
        /// </summary>
        public ActorDefinition Boss = ActorCatalog.KingSlime;

        /// <summary>Berries waiting in the boss floor's antechamber, to prepare with.</summary>
        public int BossFloorBerries = 2;
        public int EnemiesOnFirstFloor = 5;
        public int ExtraEnemiesPerFloor = 1;
        public int MaxEnemies = 10;

        /// <summary>AV between reinforcement spawns on a floor (40 turns at Speed 100). 0 turns them off.</summary>
        public int ReinforcementIntervalAv = 4000;

        public int ItemsPerFloor = 3;
        public int MaxBerries = 9;

        /// <summary>Mana a berry restores. Healing comes from skills (Second Wind) and slow regeneration.</summary>
        public int BerryRestoreMp = 15;

        /// <summary>HP a berry heals. 0 in the game (berries restore mana); the prototype healed 300 (30 before the 10x rescale).</summary>
        public int BerryHealHp;

        /// <summary>AV between ticks of natural regeneration (6 turns at Speed 100). 0 turns it off.</summary>
        public int RegenIntervalAv = 600;

        /// <summary>HP each regeneration tick restores.</summary>
        public int RegenHp = 10;

        /// <summary>What each kind of action costs on the timeline, in percent of a normal turn.</summary>
        public ActionCosts Costs = new ActionCosts();

        /// <summary>Enemies notice the hero within this many tiles, or anywhere in the same room.</summary>
        public int SightRange = 5;

        /// <summary>Optional terrain override taking (floor, floorSeed). Used by tests and hand-built floors.</summary>
        public Func<int, int, DungeonMap> MapFactory;

        /// <summary>When false, floors start without enemies or items and nothing spawns later. Used by tests.</summary>
        public bool Populate = true;
    }
}

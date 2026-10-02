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

        /// <summary>Turns between reinforcement spawns. 0 turns them off.</summary>
        public int ReinforcementInterval = 40;

        public int ItemsPerFloor = 3;
        public int MaxBerries = 9;
        public int BerryHeal = 30;

        /// <summary>Hero turns per 1 HP of natural regeneration. 0 turns it off.</summary>
        public int RegenInterval = 6;

        /// <summary>Enemies notice the hero within this many tiles, or anywhere in the same room.</summary>
        public int SightRange = 5;

        /// <summary>Optional terrain override taking (floor, floorSeed). Used by tests and hand-built floors.</summary>
        public Func<int, int, DungeonMap> MapFactory;

        /// <summary>When false, floors start without enemies or items and nothing spawns later. Used by tests.</summary>
        public bool Populate = true;
    }
}

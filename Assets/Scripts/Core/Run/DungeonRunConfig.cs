using System;

namespace FiveKingdoms.Core
{
    /// <summary>Tuning for one dungeon. Defaults describe the prototype "Slime Cave".</summary>
    public sealed class DungeonRunConfig
    {
        public string Name = "Slime Cave";
        public int FloorCount = 5;
        public DungeonGenConfig Generation = new DungeonGenConfig();

        public ActorDefinition Hero = ActorCatalog.Uzuki;
        public ActorDefinition Enemy = ActorCatalog.Slime;
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

using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>Tuning for one dungeon. Defaults describe the prototype "Slime Cave".</summary>
    public sealed class DungeonRunConfig
    {
        public string Name = "Slime Cave";
        public int FloorCount = 5;
        public DungeonGenConfig Generation = new DungeonGenConfig();

        /// <summary>Hero used when a run starts without a <see cref="HeroProgress"/> and without a <see cref="Party"/>.</summary>
        public ActorDefinition Hero = ActorCatalog.Uzuki;

        /// <summary>
        /// Heroes used when a run starts without any <see cref="HeroProgress"/>, leader first. Null means a lone
        /// <see cref="Hero"/> (tests); the game passes the saved party (<see cref="ActorCatalog.StartingParty"/>).
        /// </summary>
        public IReadOnlyList<ActorDefinition> Party;

        /// <summary>When true the run is lost as soon as the leader falls; otherwise only when the whole party has.</summary>
        public bool DefeatWhenLeaderFalls;
        public ActorDefinition Enemy = ActorCatalog.Slime;

        /// <summary>
        /// Boss waiting in an arena on the last floor; defeating it clears the dungeon.
        /// Null: the last floor is an ordinary floor whose stairs clear the dungeon.
        /// </summary>
        public ActorDefinition Boss = ActorCatalog.KingSlime;

        /// <summary>Berries waiting in the boss floor's antechamber, to prepare with.</summary>
        public int BossFloorBerries = 2;
        /// <summary>Enemies on each floor (B1F 7, +2 a floor, at most 14): a party of three needs company.</summary>
        public int EnemiesOnFirstFloor = 7;
        public int ExtraEnemiesPerFloor = 2;
        public int MaxEnemies = 14;

        /// <summary>
        /// Floors place their enemies in packs of this many, so each fight takes the whole party (PROGRESSION.md, "Bigger
        /// fights"). A maximum of 1 places them one by one, as before the party.
        /// </summary>
        public int PackSizeMin = 2;
        public int PackSizeMax = 4;

        /// <summary>A fast monster (the bat) that this percent of packs bring along. Null: none.</summary>
        public ActorDefinition FastEnemy = ActorCatalog.Bat;
        public int FastEnemyPercent = 50;

        /// <summary>AV between reinforcement spawns on a floor (40 turns at Speed 100). 0 turns them off.</summary>
        public int ReinforcementIntervalAv = 4000;

        public int ItemsPerFloor = 3;

        /// <summary>Traps one floor holds at once (Uzuki's snares); setting another removes the oldest.</summary>
        public int MaxTrapsPerFloor = 3;
        public int MaxBerries = 9;

        /// <summary>HP a berry heals the leader (there is no mana; in milestone 1d berries restored it instead).</summary>
        public int BerryHealHp = 300;

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

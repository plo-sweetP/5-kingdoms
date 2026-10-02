using FiveKingdoms.Core;

namespace FiveKingdoms.Tests
{
    static class TestRuns
    {
        /// <summary>A run on a hand-drawn floor (repeated for every floor) with no random enemies, items or regen.</summary>
        public static DungeonRun OnMap(int floorCount, params string[] rows)
        {
            var config = new DungeonRunConfig
            {
                FloorCount = floorCount,
                MapFactory = (floor, seed) => DungeonMap.FromAscii(rows),
                Populate = false,
                RegenInterval = 0,
            };
            return new DungeonRun(7, config);
        }

        public static DungeonRun OnMap(params string[] rows) => OnMap(1, rows);
    }
}

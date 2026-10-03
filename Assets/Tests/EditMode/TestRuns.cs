using FiveKingdoms.Core;

namespace FiveKingdoms.Tests
{
    static class TestRuns
    {
        /// <summary>
        /// A run on a hand-drawn floor (repeated for every floor) with no random enemies, items, regen or boss, and the
        /// <see cref="TestHeroes.Classic"/> hero unless one is given.
        /// Pass a boss to make the last floor a boss floor; it still has to be spawned by the test.
        /// </summary>
        public static DungeonRun OnMap(int floorCount, HeroProgress hero, ActorDefinition boss, params string[] rows)
        {
            var config = new DungeonRunConfig
            {
                FloorCount = floorCount,
                MapFactory = (floor, seed) => DungeonMap.FromAscii(rows),
                Populate = false,
                RegenIntervalAv = 0,
                Boss = boss,
                Hero = TestHeroes.Classic,
            };
            return new DungeonRun(7, config, hero);
        }

        public static DungeonRun OnMap(int floorCount, params string[] rows) => OnMap(floorCount, null, null, rows);

        public static DungeonRun OnMap(params string[] rows) => OnMap(1, null, null, rows);
    }
}

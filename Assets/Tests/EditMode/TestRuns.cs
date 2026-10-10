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

        /// <summary>
        /// A run with built heroes (leader first) on a hand-drawn floor: no random enemies, no regeneration, no boss
        /// floor. For the tests of what a class option does in play.
        /// </summary>
        public static DungeonRun With(HeroProgress[] heroes, params string[] rows)
        {
            var config = new DungeonRunConfig
            {
                MapFactory = (floor, seed) => DungeonMap.FromAscii(rows),
                Populate = false,
                RegenIntervalAv = 0,
                Boss = null,
            };
            return new DungeonRun(7, config, heroes);
        }

        public static DungeonRun With(HeroProgress hero, params string[] rows) => With(new[] { hero }, rows);

        public static DungeonRun OnMap(int floorCount, params string[] rows) => OnMap(floorCount, null, null, rows);

        public static DungeonRun OnMap(params string[] rows) => OnMap(1, null, null, rows);
    }
}

using FiveKingdoms.Core;

namespace FiveKingdoms.Tests
{
    static class TestHeroes
    {
        /// <summary>
        /// Uzuki as he was in milestone 1d: melee, speed 100, no weapon and no ultimate, with Spirit Strike, Second Wind
        /// and Dash (without the mana he had then). Rules tests use him so they don't change whenever the real roster is
        /// re-tuned.
        /// </summary>
        public static readonly ActorDefinition Classic = new ActorDefinition("classic", "Classic", maxHp: 400, attack: 60, defense: 30,
            expReward: 0, skills: new[] { SkillCatalog.SpiritStrike, SkillCatalog.SecondWind, SkillCatalog.Dash },
            hpGrowth: 50, atkGrowth: 10, defGrowth: 10);

        /// <summary>
        /// The Monk's base kit on a hero of its own: Kristela as she was until 2026-10-05 (her stats, the Gauntlets,
        /// Piercing Punch, Ki Heal, Stun Strike and Flurry of Blows, speed 95 + 5). The tests of that kit use her,
        /// and so do the tests that need a melee hero with a heal of its own.
        /// </summary>
        public static readonly ActorDefinition Monk = new ActorDefinition("monk", "Monk", maxHp: 380, attack: 64, defense: 26,
            expReward: 0, speed: 95, skills: new[] { SkillCatalog.PiercingPunch, SkillCatalog.KiHeal, SkillCatalog.StunStrike },
            ultimate: SkillCatalog.FlurryOfBlows, hpGrowth: 45, atkGrowth: 11, defGrowth: 8, weapon: WeaponCatalog.Gauntlets,
            startingClass: ClassCatalog.Monk);
    }
}

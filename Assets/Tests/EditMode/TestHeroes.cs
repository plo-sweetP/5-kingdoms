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
    }
}

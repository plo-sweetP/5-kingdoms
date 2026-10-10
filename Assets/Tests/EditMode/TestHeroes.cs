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

        /// <summary>
        /// A hero at <paramref name="level"/> with its own class raised as far as its points and the written tiers go,
        /// taking at each milestone the option among <paramref name="picks"/> (by id). A milestone none of them
        /// belongs to stops it there, with the rest of the points free.
        /// </summary>
        public static HeroProgress Built(ActorDefinition definition, int level, params string[] picks)
        {
            var hero = new HeroProgress(definition, level);
            var own = definition.StartingClass;
            while (hero.PointsFree > 0)
            {
                int next = hero.TierOf(own) + 1;
                ClassOption pick = null;
                if (ClassDefinition.IsMilestone(next))
                    foreach (var option in own.MilestoneAt(next)?.Options ?? System.Array.Empty<ClassOption>())
                        if (System.Array.IndexOf(picks, option.Id) >= 0) pick = option;
                if (!hero.Raise(own, pick)) break;
            }
            return hero;
        }

        /// <summary>The slot of <paramref name="skill"/> (by id: a hero's own copy counts) in a hero's loadout, or -1.</summary>
        public static int Slot(Actor hero, SkillDefinition skill)
        {
            var skills = hero.Skills;
            for (int i = 0; i < skills.Count; i++)
                if (skills[i].Id == skill.Id) return i;
            return -1;
        }
    }
}

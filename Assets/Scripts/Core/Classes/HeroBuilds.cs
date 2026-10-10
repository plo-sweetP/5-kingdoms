using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>
    /// How a hero's points are spent when nobody is there to choose: by the balance report's players (the autopilot
    /// and the partners' AI) and by test runs that start at a higher level. A build is the hero's own class as far as
    /// its points and the written tiers go, with one path named per milestone; the default is the path of the hero's
    /// role (the planning hub's proposal, 2026-10-10): Uzuki the Marksman, Haiden the Guardian, Kristela the Duelist,
    /// each the first path of its class. The player's own heroes are never spent for them.
    /// </summary>
    public static class HeroBuilds
    {
        /// <summary>The path a hero takes at every milestone unless a build says otherwise: its class's first.</summary>
        public const int DefaultPath = 0;

        /// <summary>
        /// Spends every free point of <paramref name="hero"/> on its own class, tier by tier, until the points run out
        /// or the class stops (tier 25, or a milestone that isn't written). At a milestone it picks the option of the
        /// path <paramref name="paths"/> names for it: one entry per milestone in order (tier 5, 10, ...), the last
        /// one standing for the milestones after it; null or empty is <see cref="DefaultPath"/> throughout. Returns
        /// the points spent.
        /// </summary>
        public static int Spend(HeroProgress hero, IReadOnlyList<int> paths = null)
        {
            var own = hero.Definition.StartingClass;
            if (own == null) return 0;
            int spent = 0;
            while (true)
            {
                int next = hero.TierOf(own) + 1;
                ClassOption option = null;
                if (ClassDefinition.IsMilestone(next))
                {
                    var milestone = own.MilestoneAt(next);
                    if (milestone == null) break;
                    option = milestone.Options[PathAt(paths, next)];
                }
                if (!hero.Raise(own, option)) break;
                spent++;
            }
            return spent;
        }

        /// <summary>The path a build takes at a milestone tier.</summary>
        public static int PathAt(IReadOnlyList<int> paths, int tier)
        {
            if (paths == null || paths.Count == 0) return DefaultPath;
            return paths[Math.Min(paths.Count, tier / ClassDefinition.MilestoneEvery) - 1];
        }

        /// <summary>
        /// Reads a build as the tools take it on the command line: "uzuki:hunter" (that path at every milestone) or
        /// "uzuki:marksman,hunter" (tier 5, then tier 10 and after). A path is named as its class names it, without
        /// regard to case or spaces, and its first letters are enough ("eng" for En Garde). False, with what was
        /// wrong, when the hero or a path isn't known.
        /// </summary>
        public static bool TryParse(string text, out ActorDefinition hero, out int[] paths, out string error)
        {
            hero = null;
            paths = null;
            error = null;
            int colon = text.IndexOf(':');
            string heroId = colon < 0 ? text : text.Substring(0, colon);
            hero = ActorCatalog.Find(heroId.Trim().ToLowerInvariant());
            if (hero == null || hero.StartingClass == null)
            {
                error = $"\"{heroId}\" is not a hero.";
                return false;
            }
            if (colon < 0 || colon == text.Length - 1)
            {
                error = $"No path for {hero.Name}: write {hero.Id}:{Squeeze(hero.StartingClass.Paths[0])}.";
                return false;
            }
            var names = text.Substring(colon + 1).Split(',');
            paths = new int[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                paths[i] = PathNamed(hero.StartingClass, names[i]);
                if (paths[i] >= 0) continue;
                error = $"The {hero.StartingClass.Name} has no path \"{names[i]}\": {string.Join(", ", hero.StartingClass.Paths)}.";
                return false;
            }
            return true;
        }

        /// <summary>A build as a line of text: "Uzuki: Archer 5 (Marksman)", with the path of each milestone reached.</summary>
        public static string Describe(HeroProgress hero)
        {
            var own = hero.Definition.StartingClass;
            if (own == null) return hero.Definition.Name;
            var picks = new List<string>();
            for (int tier = ClassDefinition.MilestoneEvery; tier <= hero.TierOf(own); tier += ClassDefinition.MilestoneEvery)
                if (hero.PickAt(own, tier) is ClassOption option) picks.Add(own.Paths[option.Path]);
            return $"{hero.Definition.Name}: {own.Name} {hero.TierOf(own)}" + (picks.Count > 0 ? $" ({string.Join(", ", picks)})" : "");
        }

        static int PathNamed(ClassDefinition definition, string name)
        {
            string wanted = Squeeze(name);
            if (wanted.Length == 0) return -1;
            for (int path = 0; path < definition.Paths.Count; path++)
                if (Squeeze(definition.Paths[path]).StartsWith(wanted, StringComparison.Ordinal)) return path;
            return -1;
        }

        static string Squeeze(string name) => name.Replace(" ", "").Trim().ToLowerInvariant();
    }
}

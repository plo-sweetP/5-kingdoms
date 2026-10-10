using System;
using System.IO;
using FiveKingdoms.Core;
using FiveKingdoms.UI;
using UnityEngine;

namespace FiveKingdoms.Dungeon
{
    /// <summary>
    /// Optional settings for test and debug launches, read from the command line or set by tests through
    /// <see cref="DungeonController.Overrides"/>. Normal play uses none of them.
    /// </summary>
    public sealed class LaunchOptions
    {
        /// <summary>Dungeon length; 1 goes straight to the boss floor.</summary>
        public int? FloorCount;

        /// <summary>Start the hero at this level. Uses a throwaway save so a real save is never touched.</summary>
        public int? StartLevel;

        /// <summary>
        /// With <see cref="StartLevel"/>: the heroes' points are spent before the run, as the balance report's players
        /// spend them (<see cref="HeroBuilds"/>): "default" for each hero's default path, or builds by hero for the
        /// others, e.g. "uzuki:hunter;kristela:footwork,engarde". Null leaves the points free, to spend on the tree.
        /// </summary>
        public string Builds;

        public string SavePath;

        /// <summary>Delete the save at <see cref="SavePath"/> first, so the launch starts from scratch.</summary>
        public bool FreshSave;

        /// <summary>Start the HUD in this input mode instead of touch (to check the keyboard or controller layout).</summary>
        public InputMode? StartInputMode;

        /// <summary>The hero (by id: uzuki, haiden, kristela) who leads instead of the first in the party, for playtests.</summary>
        public string Leader;

        /// <summary>
        /// Other looks for the heroes, to try equipment art before gear exists (<see cref="HeroLooks.Parse"/>), e.g.
        /// "haiden=great_sword,mage_robe;uzuki=mage_staff,bare;kristela=crown;all=hawks_eye": per hero or "all", a
        /// weapon, an armor set, "bare" for no head piece, a cosmetic head piece, a ring set.
        /// </summary>
        public string Looks;

        /// <summary>How the camera keeps targets in view: lead (default), zoomout or wide (ART.md, "View size").</summary>
        public ViewMode? View;

        /// <summary>
        /// The view instead of the player's setting (HUD.md, "The view on the tablet"): false for Near, true for Far
        /// (one whole zoom step further out, where the screen has one).
        /// </summary>
        public bool? FarView;

        /// <summary>
        /// The kind of screen instead of what the device reports: a PC window that shows the phone's or the tablet's
        /// looks (the view a device starts in, how small the numbers over the actors get in Far).
        /// </summary>
        public ScreenKind? Device;

        /// <summary>
        /// Open the skill tree before the first run, on this hero ("" for the leader): a build can be changed there, and
        /// the run starts when the screen closes. For screenshots and tests of the screen.
        /// </summary>
        public string OpenTree;

        /// <summary>The run's seed, for the same floors every launch (comparing screenshots, chasing a bug).</summary>
        public int? Seed;

        /// <summary>The minimap's size or off, instead of the player's setting (to screenshot each size).</summary>
        public MinimapSize? Minimap;

        /// <summary>
        /// Flags: -fk-floors N, -fk-level N, -fk-save PATH, -fk-input touch|keyboard|gamepad, -fk-leader ID,
        /// -fk-look LOOKS, -fk-view lead|zoomout|wide, -fk-zoom near|far, -fk-device desktop|phone|tablet, -fk-tree [HERO], -fk-seed N, -fk-minimap off|small|large,
        /// -fk-build default|BUILDS (with -fk-level), and
        /// -fk-autoplay FOLDER (which also uses a fresh save inside FOLDER, so smoke tests never touch the player's save).
        /// </summary>
        /// <summary>"-fk-zoom near" or "-fk-zoom far"; anything else leaves the view to the player's setting.</summary>
        public static bool? FarViewArg(string value) =>
            string.Equals(value, "far", StringComparison.OrdinalIgnoreCase) ? true
            : string.Equals(value, "near", StringComparison.OrdinalIgnoreCase) ? false
            : (bool?)null;

        public static LaunchOptions FromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var options = new LaunchOptions
            {
                FloorCount = IntArg(args, "-fk-floors"),
                StartLevel = IntArg(args, "-fk-level"),
                Builds = StringArg(args, "-fk-build"),
                SavePath = StringArg(args, "-fk-save"),
                StartInputMode = Enum.TryParse(StringArg(args, "-fk-input"), ignoreCase: true, out InputMode mode) ? mode : (InputMode?)null,
                Leader = StringArg(args, "-fk-leader")?.ToLowerInvariant(),
                Looks = StringArg(args, "-fk-look"),
                View = Enum.TryParse(StringArg(args, "-fk-view"), ignoreCase: true, out ViewMode view) ? view : (ViewMode?)null,
                FarView = FarViewArg(StringArg(args, "-fk-zoom")),
                Device = Enum.TryParse(StringArg(args, "-fk-device"), ignoreCase: true, out ScreenKind device) ? device : (ScreenKind?)null,
                OpenTree = Array.IndexOf(args, "-fk-tree") >= 0 ? StringArg(args, "-fk-tree")?.ToLowerInvariant() ?? "" : null,
                Seed = IntArg(args, "-fk-seed"),
                Minimap = Enum.TryParse(StringArg(args, "-fk-minimap"), ignoreCase: true, out MinimapSize minimap) ? minimap : (MinimapSize?)null,
            };
            string autoplayFolder = StringArg(args, "-fk-autoplay");
            if (autoplayFolder != null && options.SavePath == null)
            {
                options.SavePath = Path.Combine(autoplayFolder, "autoplay-save.json");
                options.FreshSave = true;
            }
            if (options.StartLevel.HasValue && options.SavePath == null)
            {
                options.SavePath = Path.Combine(Application.temporaryCachePath, "debug-save.json");
                options.FreshSave = true;
            }
            return options;
        }

        /// <summary>
        /// A hero for a launch at <see cref="StartLevel"/>, with its points spent as <see cref="Builds"/> says: on the
        /// build that names it, else on its default path. With no builds given the points stay free.
        /// </summary>
        public HeroProgress Built(HeroProgress hero)
        {
            if (Builds == null) return hero;
            int[] paths = null;
            foreach (string build in Builds.Split(';'))
                if (HeroBuilds.TryParse(build.Trim(), out var named, out var chosen, out _) && named == hero.Definition) paths = chosen;
            HeroBuilds.Spend(hero, paths);
            return hero;
        }

        static string StringArg(string[] args, string flag)
        {
            int index = Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("-") ? args[index + 1] : null;
        }

        static int? IntArg(string[] args, string flag) => int.TryParse(StringArg(args, flag), out int value) ? value : (int?)null;
    }
}

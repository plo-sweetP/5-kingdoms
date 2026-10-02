using System;
using System.IO;
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

        public string SavePath;

        /// <summary>Delete the save at <see cref="SavePath"/> first, so the launch starts from scratch.</summary>
        public bool FreshSave;

        /// <summary>
        /// Flags: -fk-floors N, -fk-level N, -fk-save PATH, and -fk-autoplay FOLDER (which also uses a fresh save
        /// inside FOLDER, so smoke tests never touch the player's save).
        /// </summary>
        public static LaunchOptions FromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var options = new LaunchOptions
            {
                FloorCount = IntArg(args, "-fk-floors"),
                StartLevel = IntArg(args, "-fk-level"),
                SavePath = StringArg(args, "-fk-save"),
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

        static string StringArg(string[] args, string flag)
        {
            int index = Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("-") ? args[index + 1] : null;
        }

        static int? IntArg(string[] args, string flag) => int.TryParse(StringArg(args, flag), out int value) ? value : (int?)null;
    }
}

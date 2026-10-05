using System;
using System.Collections.Generic;
using System.IO;
using FiveKingdoms.Core;
using UnityEngine;

namespace FiveKingdoms
{
    /// <summary>
    /// The local save file (JSON): progress that outlives a dungeon run: each hero's level and EXP, the classes it has
    /// learned with the options it picked, and its loadout. Writes go to a temporary file that then replaces the save,
    /// so a crash mid-write can't corrupt it. The file carries a version number so later builds can migrate older
    /// saves; a save from a newer build is left alone.
    /// <para>
    /// Version 2 (milestone 1g) added the classes and the loadout. A hero saved by version 1 has only a level: it
    /// gets a point per level, with tier 1 of its own class spent and the rest free, and its starting kit
    /// (PROGRESSION.md, "Building 1g"). Each hero's entry says which version wrote it, so a file that still holds
    /// older entries next to new ones reads right.
    /// </para>
    /// </summary>
    public static class SaveSystem
    {
        public const int CurrentVersion = 2;

        /// <summary>The first version whose hero entries carry classes and a loadout.</summary>
        const int ClassesVersion = 2;
        static string filePath;

        /// <summary>Where the save lives. Tests and debug launches point this at a throwaway file; null restores the default.</summary>
        public static string FilePath
        {
            get => filePath ?? (filePath = Path.Combine(Application.persistentDataPath, "save.json"));
            set => filePath = value;
        }

        static string TempPath => FilePath + ".tmp";

        public static HeroProgress LoadHero(ActorDefinition definition) =>
            ToProgress(definition, Read(out _)?.heroes.Find(hero => hero.id == definition.Id));

        /// <summary>
        /// A hero from its save entry (a fresh start without one). An entry from before classes existed migrates: its
        /// level stays, tier 1 of its own class is spent, the other points are free.
        /// </summary>
        static HeroProgress ToProgress(ActorDefinition definition, HeroSave entry)
        {
            if (entry == null) return new HeroProgress(definition);
            if (entry.savedWith < ClassesVersion) return new HeroProgress(definition, entry.level, entry.exp);
            var classes = new List<SavedClass>();
            foreach (var saved in entry.classes ?? new List<ClassSave>()) classes.Add(new SavedClass(saved.id, saved.tier, saved.picks));
            return HeroProgress.Restore(definition, entry.level, entry.exp, classes, entry.loadout, entry.ultimate);
        }

        public static void SaveHero(HeroProgress progress) => SaveParty(new[] { progress });

        /// <summary>Each hero's saved progress, in the order given (a fresh start for heroes not in the save yet).</summary>
        public static HeroProgress[] LoadParty(IReadOnlyList<ActorDefinition> definitions)
        {
            var data = Read(out _);
            var party = new HeroProgress[definitions.Count];
            for (int i = 0; i < party.Length; i++)
            {
                var definition = definitions[i];
                party[i] = ToProgress(definition, data?.heroes.Find(hero => hero.id == definition.Id));
            }
            return party;
        }

        /// <summary>Saves every hero's progress in one write, keeping other heroes already in the save.</summary>
        public static void SaveParty(IEnumerable<HeroProgress> party)
        {
            var data = Read(out bool fromNewerBuild);
            if (fromNewerBuild) return; // Never overwrite a save written by a newer version of the game.
            data ??= new SaveData();
            foreach (var progress in party)
            {
                var entry = data.heroes.Find(hero => hero.id == progress.Definition.Id);
                if (entry == null)
                {
                    entry = new HeroSave { id = progress.Definition.Id };
                    data.heroes.Add(entry);
                }
                entry.level = progress.Level;
                entry.exp = progress.Exp;
                entry.savedWith = CurrentVersion;
                entry.classes = new List<ClassSave>();
                foreach (var saved in progress.SaveClasses())
                    entry.classes.Add(new ClassSave { id = saved.Id, tier = saved.Tier, picks = new List<string>(saved.Picks) });
                entry.loadout = new List<string>();
                foreach (string id in progress.LoadoutIds) entry.loadout.Add(id ?? "");
                entry.ultimate = progress.UltimateId ?? "";
            }
            data.version = CurrentVersion;
            Write(data);
        }

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            if (File.Exists(TempPath)) File.Delete(TempPath);
        }

        static SaveData Read(out bool fromNewerBuild)
        {
            fromNewerBuild = false;
            // A temp file without a save means a write was interrupted after the old save was removed: recover it.
            string path = File.Exists(FilePath) ? FilePath : File.Exists(TempPath) ? TempPath : null;
            if (path == null) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (data == null) return null;
                if (data.version > CurrentVersion)
                {
                    fromNewerBuild = true;
                    Debug.LogWarning($"Save file {path} is from a newer version of the game ({data.version}); ignoring it.");
                    return null;
                }
                data.heroes ??= new List<HeroSave>();
                return data;
            }
            catch (Exception e)
            {
                // Keep the unreadable file for inspection instead of silently overwriting it.
                Debug.LogWarning($"Could not read save file {path} ({e.Message}); starting fresh and keeping a copy as .corrupt.");
                File.Copy(path, path + ".corrupt", overwrite: true);
                return null;
            }
        }

        static void Write(SaveData data)
        {
            string directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(TempPath, JsonUtility.ToJson(data, prettyPrint: true));
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(TempPath, FilePath);
        }

        [Serializable]
        public sealed class SaveData
        {
            public int version = CurrentVersion;
            public List<HeroSave> heroes = new List<HeroSave>();
        }

        [Serializable]
        public sealed class HeroSave
        {
            public string id;
            public int level = 1;
            public int exp;

            /// <summary>The save version that wrote this entry; 0 in files from before entries said so (version 1).</summary>
            public int savedWith;

            /// <summary>The classes the hero has points in, in the order learned.</summary>
            public List<ClassSave> classes = new List<ClassSave>();

            /// <summary>Skill ids by loadout slot ("" for an empty one), and the ultimate's id.</summary>
            public List<string> loadout = new List<string>();
            public string ultimate = "";
        }

        [Serializable]
        public sealed class ClassSave
        {
            public string id;
            public int tier;

            /// <summary>The ids of the options picked at the milestones reached.</summary>
            public List<string> picks = new List<string>();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using FiveKingdoms.Core;
using UnityEngine;

namespace FiveKingdoms
{
    /// <summary>
    /// The local save file (JSON): progress that outlives a dungeon run, currently each hero's level and EXP.
    /// Writes go to a temporary file that then replaces the save, so a crash mid-write can't corrupt it. The file
    /// carries a version number so later builds can migrate older saves; a save from a newer build is left alone.
    /// </summary>
    public static class SaveSystem
    {
        public const int CurrentVersion = 1;
        static string filePath;

        /// <summary>Where the save lives. Tests and debug launches point this at a throwaway file; null restores the default.</summary>
        public static string FilePath
        {
            get => filePath ?? (filePath = Path.Combine(Application.persistentDataPath, "save.json"));
            set => filePath = value;
        }

        static string TempPath => FilePath + ".tmp";

        public static HeroProgress LoadHero(ActorDefinition definition)
        {
            var entry = Read(out _)?.heroes.Find(hero => hero.id == definition.Id);
            return entry != null ? new HeroProgress(definition, entry.level, entry.exp) : new HeroProgress(definition);
        }

        public static void SaveHero(HeroProgress progress)
        {
            var data = Read(out bool fromNewerBuild);
            if (fromNewerBuild) return; // Never overwrite a save written by a newer version of the game.
            data ??= new SaveData();
            var entry = data.heroes.Find(hero => hero.id == progress.Definition.Id);
            if (entry == null)
            {
                entry = new HeroSave { id = progress.Definition.Id };
                data.heroes.Add(entry);
            }
            entry.level = progress.Level;
            entry.exp = progress.Exp;
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
        }
    }
}

using System.IO;
using FiveKingdoms.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FiveKingdoms.Tests
{
    public class SaveSystemTests
    {
        string originalPath;
        string path;

        [SetUp]
        public void UseThrowawaySave()
        {
            originalPath = SaveSystem.FilePath;
            path = Path.Combine(Application.temporaryCachePath, "save-system-test.json");
            SaveSystem.FilePath = path;
            SaveSystem.Delete();
        }

        [TearDown]
        public void Restore()
        {
            SaveSystem.Delete();
            SaveSystem.FilePath = originalPath;
        }

        [Test]
        public void ANewPlayerStartsAtLevelOne()
        {
            var progress = SaveSystem.LoadHero(ActorCatalog.Uzuki);
            Assert.AreEqual(1, progress.Level);
            Assert.AreEqual(0, progress.Exp);
        }

        [Test]
        public void LevelAndExpSurviveASaveAndLoad()
        {
            SaveSystem.SaveHero(new HeroProgress(ActorCatalog.Uzuki, level: 7, exp: 13));
            var loaded = SaveSystem.LoadHero(ActorCatalog.Uzuki);
            Assert.AreEqual(7, loaded.Level);
            Assert.AreEqual(13, loaded.Exp);
            Assert.IsFalse(File.Exists(path + ".tmp"), "the temp file is moved into place");
        }

        [Test]
        public void ASaveFromANewerBuildIsNeverOverwritten()
        {
            string newer = "{\"version\": 99, \"heroes\": [{\"id\": \"uzuki\", \"level\": 30, \"exp\": 0}]}";
            File.WriteAllText(path, newer);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("newer version"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("newer version"));

            Assert.AreEqual(1, SaveSystem.LoadHero(ActorCatalog.Uzuki).Level, "unknown format: start fresh in memory");
            SaveSystem.SaveHero(new HeroProgress(ActorCatalog.Uzuki, level: 2));
            Assert.AreEqual(newer, File.ReadAllText(path));
        }

        [Test]
        public void ACorruptSaveIsKeptAsideAndPlayStartsFresh()
        {
            File.WriteAllText(path, "{ this is not json");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Could not read save file"));

            Assert.AreEqual(1, SaveSystem.LoadHero(ActorCatalog.Uzuki).Level);
            Assert.IsTrue(File.Exists(path + ".corrupt"));
            File.Delete(path + ".corrupt");
        }

        [Test]
        public void AnInterruptedWriteIsRecoveredFromTheTempFile()
        {
            SaveSystem.SaveHero(new HeroProgress(ActorCatalog.Uzuki, level: 5));
            File.Move(path, path + ".tmp"); // As if the game died between removing the old save and moving the new one in.
            Assert.AreEqual(5, SaveSystem.LoadHero(ActorCatalog.Uzuki).Level);
        }
    }
}

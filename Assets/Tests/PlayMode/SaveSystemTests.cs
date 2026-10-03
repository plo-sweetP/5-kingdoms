using System.IO;
using System.Linq;
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

        [Test]
        public void ThePartySavesAndLoadsTogether()
        {
            SaveSystem.SaveParty(new[]
            {
                new HeroProgress(ActorCatalog.Uzuki, level: 4, exp: 3),
                new HeroProgress(ActorCatalog.Haiden, level: 5, exp: 1),
                new HeroProgress(ActorCatalog.Kristela, level: 6, exp: 2),
            });
            var party = SaveSystem.LoadParty(new[] { ActorCatalog.Uzuki, ActorCatalog.Haiden, ActorCatalog.Kristela });
            CollectionAssert.AreEqual(new[] { 4, 5, 6 }, party.Select(hero => hero.Level).ToArray());
            CollectionAssert.AreEqual(new[] { 3, 1, 2 }, party.Select(hero => hero.Exp).ToArray());
            Assert.AreEqual(4, SaveSystem.LoadHero(ActorCatalog.Uzuki).Level, "one hero's entry, as before");

            // A save doesn't depend on the party's order: the starting party (a melee hero leads) finds everyone's own entry.
            var starting = SaveSystem.LoadParty(ActorCatalog.StartingParty);
            CollectionAssert.AreEqual(ActorCatalog.StartingParty, starting.Select(hero => hero.Definition).ToArray());
            Assert.AreEqual(5, starting.Single(hero => hero.Definition == ActorCatalog.Haiden).Level);
            Assert.AreEqual(6, starting.Single(hero => hero.Definition == ActorCatalog.Kristela).Level);
            Assert.AreEqual(4, starting.Single(hero => hero.Definition == ActorCatalog.Uzuki).Level);
        }
    }
}

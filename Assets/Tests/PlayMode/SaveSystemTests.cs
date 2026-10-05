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

        // ---- Classes and the loadout (save version 2, milestone 1g) ----

        const string VersionOneSave = "{\"version\": 1, \"heroes\": [{\"id\": \"uzuki\", \"level\": 7, \"exp\": 13}, {\"id\": \"haiden\", \"level\": 4, \"exp\": 2}]}";

        [Test]
        public void ASaveFromBeforeClassesMigrates()
        {
            File.WriteAllText(path, VersionOneSave);
            var uzuki = SaveSystem.LoadHero(ActorCatalog.Uzuki);
            Assert.AreEqual(7, uzuki.Level);
            Assert.AreEqual(13, uzuki.Exp);
            Assert.AreEqual(7, uzuki.Points, "a point per level");
            Assert.AreEqual(1, uzuki.TierOf(ClassCatalog.Archer), "tier 1 of his own class is spent");
            Assert.AreEqual(6, uzuki.PointsFree, "and the rest is free");
            CollectionAssert.AreEqual(new[] { "hunters_mark", "power_shot", "rolling_shot" }, uzuki.LoadoutIds);
            Assert.AreEqual("volley", uzuki.UltimateId);

            SaveSystem.SaveHero(uzuki);
            StringAssert.Contains("\"version\": 2", File.ReadAllText(path));
            StringAssert.Contains("\"archer\"", File.ReadAllText(path));
            Assert.AreEqual(6, SaveSystem.LoadHero(ActorCatalog.Uzuki).PointsFree, "the same hero from the new format");
        }

        [Test]
        public void KristelaMigratesAsAFencer()
        {
            File.WriteAllText(path, "{\"version\": 1, \"heroes\": [{\"id\": \"kristela\", \"level\": 5, \"exp\": 0}]}");
            var kristela = SaveSystem.LoadHero(ActorCatalog.Kristela);
            Assert.AreEqual(1, kristela.TierOf(ClassCatalog.Fencer), "her class since 2026-10-05");
            Assert.AreEqual(0, kristela.TierOf(ClassCatalog.Monk));
            Assert.AreEqual(4, kristela.PointsFree);
        }

        [Test]
        public void AnOlderEntryNextToNewOnesStillMigrates()
        {
            File.WriteAllText(path, VersionOneSave);
            SaveSystem.SaveHero(SaveSystem.LoadHero(ActorCatalog.Uzuki)); // The file is version 2 now; Haiden's entry is as it was.

            var haiden = SaveSystem.LoadHero(ActorCatalog.Haiden);
            Assert.AreEqual(4, haiden.Level);
            Assert.AreEqual(1, haiden.TierOf(ClassCatalog.Paladin), "his entry was written before classes: it migrates when it is read");
            Assert.AreEqual(3, haiden.PointsFree);
        }

        [Test]
        public void ClassesAndTheLoadoutSurviveASaveAndLoad()
        {
            var uzuki = new HeroProgress(ActorCatalog.Uzuki, level: 6, exp: 2);
            Assert.IsTrue(uzuki.Raise(ClassCatalog.Archer));
            Assert.IsTrue(uzuki.Raise(ClassCatalog.Archer));
            Assert.IsTrue(uzuki.Raise(ClassCatalog.Paladin));
            Assert.IsTrue(uzuki.Equip(0, SkillCatalog.RollingShot));
            SaveSystem.SaveHero(uzuki);

            var loaded = SaveSystem.LoadHero(ActorCatalog.Uzuki);
            Assert.AreEqual(3, loaded.TierOf(ClassCatalog.Archer));
            Assert.AreEqual(1, loaded.TierOf(ClassCatalog.Paladin));
            CollectionAssert.AreEqual(new[] { "archer", "paladin" }, loaded.Classes.Select(progress => progress.Class.Id).ToArray());
            Assert.AreEqual(2, loaded.PointsFree);
            CollectionAssert.AreEqual(new[] { "rolling_shot", "power_shot", "hunters_mark" }, loaded.LoadoutIds);
            Assert.AreEqual("volley", loaded.UltimateId);
        }

        [Test]
        public void AHeroThatUnlearnedItsClassComesBackWithoutIt()
        {
            var haiden = new HeroProgress(ActorCatalog.Haiden, level: 3);
            Assert.AreEqual(1, haiden.Unlearn(ClassCatalog.Paladin));
            SaveSystem.SaveHero(haiden);

            var loaded = SaveSystem.LoadHero(ActorCatalog.Haiden);
            Assert.AreEqual(0, loaded.Classes.Count, "not migrated a second time");
            Assert.AreEqual(3, loaded.PointsFree);
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

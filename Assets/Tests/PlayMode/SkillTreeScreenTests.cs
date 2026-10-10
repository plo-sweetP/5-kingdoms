using System.Collections;
using System.IO;
using System.Linq;
using FiveKingdoms.Core;
using FiveKingdoms.Dungeon;
using FiveKingdoms.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// The skill-tree screen in the real scene (PROGRESSION.md, "Building 1g", step 6): where it opens, that it changes
    /// a build between runs only, that what it changes is saved and is the kit the next run starts with. What the
    /// screen knows and says is tested without a scene in `SkillTreeModelTests`. Saves go to a throwaway file.
    /// </summary>
    public class SkillTreeScreenTests
    {
        string savePath;

        [SetUp]
        public void UseThrowawaySave()
        {
            savePath = Path.Combine(Application.temporaryCachePath, "skill-tree-test-save.json");
        }

        [TearDown]
        public void ClearOverrides()
        {
            DungeonController.Overrides = null;
            Time.timeScale = 1f;
        }

        static IEnumerator LoadDungeon()
        {
            yield return SceneManager.LoadSceneAsync("Dungeon");
            yield return null; // Let Start() begin the run.
        }

        /// <summary>The scene at a level with points to spend, and its controller with the run just ended (the end panel is up).</summary>
        IEnumerator LoadBetweenRuns(int level)
        {
            DungeonController.Overrides = new LaunchOptions { SavePath = savePath, FreshSave = true, StartLevel = level };
            yield return LoadDungeon();
            Object.FindFirstObjectByType<DungeonController>().LeaveRun();
        }

        static HeroProgress Saved(ActorDefinition definition) =>
            SaveSystem.LoadParty(ActorCatalog.StartingParty).First(hero => hero.Definition == definition);

        [UnityTest]
        public IEnumerator BetweenRunsATierIsRaisedSavedAndTakenIntoTheNextRun()
        {
            yield return LoadBetweenRuns(level: 6);
            var controller = Object.FindFirstObjectByType<DungeonController>();
            Assert.AreEqual(RunState.Left, controller.Run.State);
            var leader = controller.Run.Hero.Definition;
            int hpBefore = controller.Run.Hero.MaxHp;

            controller.OpenSkillTree();
            var tree = controller.SkillTree;
            Assert.IsTrue(tree.IsOpen);
            Assert.IsFalse(tree.Model.ReadOnly, "the run is over: the build can change");
            Assert.AreSame(leader, tree.Model.Hero.Definition);
            Assert.AreSame(ClassCatalog.Paladin, tree.Model.Class, "Haiden leads, on his own class");
            StringAssert.Contains("Raise to tier 2", tree.InfoText + tree.Model.Info.Action);

            tree.Activate();
            tree.Activate();
            tree.Activate();
            Assert.AreEqual(4, tree.Model.Hero.TierOf(ClassCatalog.Paladin));
            Assert.AreEqual(4, Saved(leader).TierOf(ClassCatalog.Paladin), "every change is saved at once");
            Assert.AreEqual(2, Saved(leader).PointsFree);

            // Tier 5 is a milestone: the button leads to its row, and the pick is the press on one of its spheres.
            StringAssert.Contains("Choose at tier 5", tree.InfoText + tree.Model.Info.Action);
            tree.Activate();
            Assert.AreEqual(4, tree.Model.Hero.TierOf(ClassCatalog.Paladin), "nothing is spent before the pick");
            Assert.AreEqual(TreeZone.Options, tree.Model.Focus.Zone);
            tree.Tap(TreeFocus.Option(0, 1));
            var option = ClassCatalog.Paladin.MilestoneAt(5).Options[1];
            StringAssert.Contains(option.Name, tree.InfoText);
            Assert.AreEqual(OptionState.Open, tree.Model.StateOf(0, 1));
            yield return null;

            // A row that isn't written yet says so where the player looks: the last one always is, for now.
            int lastRow = SkillTreeModel.Rows - 1;
            tree.Tap(TreeFocus.Option(lastRow, 0));
            StringAssert.Contains("Coming soon.", tree.InfoText);
            Assert.AreEqual(OptionState.Locked, tree.Model.StateOf(lastRow, 0));
            yield return null;

            tree.Back();
            Assert.IsFalse(tree.IsOpen, "with nothing to step out of, Back closes the screen");
            Assert.AreEqual(RunState.Left, controller.Run.State, "back on the end panel");

            // The next run starts with the build made on the screen: three more Paladin tiers of HP.
            controller.RestartRun();
            yield return null;
            var hero = controller.Run.Hero;
            Assert.AreEqual(4 * 4, hero.Kit.Percent[StatKind.Hp], "four tiers at 0.4% HP each, in tenths of a percent");
            Assert.Greater(hero.MaxHp, hpBefore);
        }

        [UnityTest]
        public IEnumerator TheLoadoutMadeOnTheScreenIsTheOneTheNextRunHas()
        {
            yield return LoadBetweenRuns(level: 3);
            var controller = Object.FindFirstObjectByType<DungeonController>();
            var before = controller.Run.Hero.Skills.Select(skill => skill.Id).ToArray();

            controller.OpenSkillTree();
            var tree = controller.SkillTree;
            tree.Tap(TreeFocus.Slot(0));
            tree.Activate(); // Into the list of the skills that could go there.
            Assert.AreEqual(TreeZone.Choices, tree.Model.Focus.Zone);
            tree.Activate(); // The first of them: it trades places with the one in slot 1.
            var expected = new[] { before[1], before[0], before[2] };
            CollectionAssert.AreEqual(expected, tree.Model.Hero.LoadoutIds);
            CollectionAssert.AreEqual(expected, Saved(controller.Run.Hero.Definition).LoadoutIds);
            controller.CloseSkillTree();

            controller.RestartRun();
            yield return null;
            CollectionAssert.AreEqual(expected, controller.Run.Hero.Skills.Select(skill => skill.Id), "the buttons follow the loadout");
        }

        [UnityTest]
        public IEnumerator UnlearningAsksFirstAndGivesThePointsBack()
        {
            yield return LoadBetweenRuns(level: 4);
            var controller = Object.FindFirstObjectByType<DungeonController>();
            controller.OpenSkillTree(hero: 2);
            var tree = controller.SkillTree;
            var hero = tree.Model.Hero;
            tree.ShowClass(1); // The Paladin: Uzuki doesn't have it, so it stands under the "+".
            Assert.AreEqual(TreeZone.Adding, tree.Model.Focus.Zone);
            tree.Activate();
            tree.Activate();
            var other = tree.Model.Class;
            Assert.AreEqual(2, hero.TierOf(other));
            Assert.AreEqual(1, hero.PointsFree);

            tree.Tap(TreeFocus.Unlearn, press: true);
            Assert.IsTrue(tree.Model.Confirming, "it asks first");
            yield return null;
            tree.Activate(); // Cancel is the marked answer.
            Assert.AreEqual(2, hero.TierOf(other));

            tree.Tap(TreeFocus.Unlearn, press: true);
            tree.Tap(TreeFocus.Confirm(true), press: true);
            Assert.AreEqual(0, hero.TierOf(other));
            Assert.AreEqual(3, hero.PointsFree);
            Assert.AreEqual(0, Saved(hero.Definition).TierOf(other), "saved");
            Assert.AreEqual(3, Saved(hero.Definition).PointsFree);
        }

        [UnityTest]
        public IEnumerator TheListShowsTheHerosClassesAndThePlusAddsAnother()
        {
            yield return LoadBetweenRuns(level: 4);
            var controller = Object.FindFirstObjectByType<DungeonController>();
            controller.OpenSkillTree();
            var tree = controller.SkillTree;
            yield return null;
            string Row(int index) => tree.transform.Find("SafeArea/Class" + index).Find("Name").GetComponent<UnityEngine.UI.Text>().text;
            bool Shown(int index) => tree.transform.Find("SafeArea/Class" + index).gameObject.activeSelf;

            // Haiden has the Paladin: that, and the "+" under it. The rows for classes he doesn't have are gone.
            Assert.AreEqual("Paladin", Row(0));
            Assert.AreEqual("+  Add a class", Row(1));
            Assert.IsFalse(Shown(2));
            Assert.IsFalse(Shown(3));

            tree.Tap(TreeFocus.Class(1), press: true); // The "+": into the list of the others.
            Assert.AreEqual(TreeFocus.Add(0), tree.Model.Focus);
            StringAssert.Contains("Archer", tree.InfoText);
            StringAssert.Contains("Not learned", tree.InfoText);
            tree.Activate(); // Learn it.
            Assert.AreEqual(1, tree.Model.Hero.TierOf(ClassCatalog.Archer));
            yield return null;
            Assert.AreEqual("Archer", Row(1));
            Assert.AreEqual("+  Add a class", Row(2));
            Assert.IsTrue(Shown(2));
            Assert.AreEqual(1, Saved(tree.Model.Hero.Definition).TierOf(ClassCatalog.Archer), "saved");
        }

        [UnityTest]
        public IEnumerator DuringARunTheHeroStatsPageOpensTheTreeToLookAtOnly()
        {
            DungeonController.Overrides = new LaunchOptions { SavePath = savePath, FreshSave = true, StartLevel = 5 };
            yield return LoadDungeon();
            var controller = Object.FindFirstObjectByType<DungeonController>();
            var hud = Object.FindFirstObjectByType<DungeonHud>();
            controller.Paused = true;
            hud.PauseMenu.Move(3); // Hero stats.
            hud.PauseMenu.Activate();
            hud.PauseMenu.Move(3); // Its Skill tree button.
            hud.PauseMenu.Activate();

            var tree = controller.SkillTree;
            Assert.IsTrue(tree.IsOpen);
            Assert.IsTrue(tree.Model.ReadOnly);
            var hero = tree.Model.Hero;
            tree.Activate();
            Assert.AreEqual(1, hero.TierOf(tree.Model.Class), "nothing changes during a run");
            StringAssert.Contains("A build changes between runs only", tree.InfoText);
            yield return null;

            tree.Back();
            Assert.IsFalse(tree.IsOpen);
            Assert.IsTrue(controller.Paused, "back in the pause menu, the run still waiting");
            Assert.AreEqual(RunState.InProgress, controller.Run.State);
            controller.Paused = false;
        }

        [UnityTest]
        public IEnumerator TheLaunchFlagOpensTheTreeBeforeTheRunAndTheRunTakesTheBuild()
        {
            DungeonController.Overrides = new LaunchOptions { SavePath = savePath, FreshSave = true, StartLevel = 3, OpenTree = "uzuki" };
            yield return LoadDungeon();
            var controller = Object.FindFirstObjectByType<DungeonController>();
            var tree = controller.SkillTree;
            Assert.IsTrue(tree.IsOpen);
            Assert.IsFalse(tree.Model.ReadOnly, "before the first run a build can change");
            Assert.AreSame(ActorCatalog.Uzuki, tree.Model.Hero.Definition);
            tree.Activate();
            Assert.AreEqual(2, tree.Model.Hero.TierOf(ClassCatalog.Archer));
            yield return null;

            controller.CloseSkillTree();
            yield return null;
            Assert.IsFalse(tree.IsOpen);
            Assert.AreEqual(RunState.InProgress, controller.Run.State);
            var uzuki = controller.Run.Party.First(member => member.Definition == ActorCatalog.Uzuki);
            Assert.AreEqual(2 * 4, uzuki.Kit.Percent[StatKind.Atk], "the run that starts has the tier raised on the screen");
        }

        [UnityTest]
        public IEnumerator TheScreenFitsThePhoneAndTheTabletShapes()
        {
            yield return LoadBetweenRuns(level: 6);
            var controller = Object.FindFirstObjectByType<DungeonController>();
            controller.OpenSkillTree();
            yield return null;
            yield return null;
            // Everything the screen draws stays inside the canvas, whatever window the tests run in.
            var canvas = Object.FindFirstObjectByType<DungeonHud>().GetComponent<RectTransform>();
            var bounds = canvas.rect;
            var corners = new Vector3[4];
            foreach (var rect in controller.SkillTree.GetComponentsInChildren<RectTransform>())
            {
                if (rect.GetComponent<UnityEngine.UI.Graphic>() == null || rect.name == "Hit" || rect.name == "Glow") continue;
                if (rect.GetComponent<UnityEngine.UI.Text>() is UnityEngine.UI.Text text && string.IsNullOrEmpty(text.text)) continue;
                rect.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var local = canvas.InverseTransformPoint(corner);
                    Assert.IsTrue(local.x >= bounds.xMin - 1f && local.x <= bounds.xMax + 1f && local.y >= bounds.yMin - 1f && local.y <= bounds.yMax + 1f,
                        $"{rect.name} reaches outside the screen at {local} (canvas {bounds.size})");
                }
            }
        }
    }
}

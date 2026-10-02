using System.Collections;
using System.IO;
using FiveKingdoms.Core;
using FiveKingdoms.Dungeon;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// Loads the real dungeon scene and lets the autopilot play through the controller, view and HUD.
    /// Any exception or error logged along the way fails the test (Unity's test runner does that by default).
    /// Saves go to a throwaway file so a developer's own save is never touched.
    /// </summary>
    public class DungeonSceneSmokeTest
    {
        string savePath;

        [SetUp]
        public void UseThrowawaySave()
        {
            savePath = Path.Combine(Application.temporaryCachePath, "smoke-test-save.json");
        }

        [TearDown]
        public void ClearOverrides()
        {
            DungeonController.Overrides = null;
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator AutopilotPlaysTheSceneWithoutErrors()
        {
            DungeonController.Overrides = new LaunchOptions { SavePath = savePath, FreshSave = true };
            yield return LoadDungeon();
            var controller = Object.FindFirstObjectByType<DungeonController>();
            Assert.IsNotNull(controller, "the Dungeon scene should contain a DungeonController. Scene contents: " + DescribeScene());

            int actions = 0;
            yield return Autoplay(controller, maxActions: 150, onAction: () => actions++);

            Assert.Greater(actions, 30, "the autopilot should get through a good number of actions");
            Assert.IsTrue(controller.Run.Turn > 0);
            Assert.IsTrue(File.Exists(savePath) || controller.Run.Hero.Exp == 0 && controller.Run.Hero.Level == 1,
                "progress is saved once EXP is earned");
        }

        [UnityTest]
        public IEnumerator AStrongHeroBeatsTheKingSlime()
        {
            // One floor: straight into the boss arena, at a level that should win comfortably.
            DungeonController.Overrides = new LaunchOptions { SavePath = savePath, FreshSave = true, FloorCount = 1, StartLevel = 12 };
            yield return LoadDungeon();
            var controller = Object.FindFirstObjectByType<DungeonController>();
            Assert.IsTrue(controller.Run.IsBossFloor);
            Assert.IsNotNull(controller.Run.Boss);

            yield return Autoplay(controller, maxActions: 300, onAction: null);

            Assert.AreEqual(RunState.Won, controller.Run.State, $"ended on turn {controller.Run.Turn} with HP {controller.Run.Hero.Hp}");
            Assert.IsNull(controller.Run.Boss);
        }

        static IEnumerator LoadDungeon()
        {
            yield return SceneManager.LoadSceneAsync("Dungeon");
            yield return null; // Let Start() begin the run.
        }

        static IEnumerator Autoplay(DungeonController controller, int maxActions, System.Action onAction)
        {
            Time.timeScale = 4f; // Animations still play, just faster.
            int actions = 0;
            float deadline = Time.realtimeSinceStartup + 120f;
            while (actions < maxActions && Time.realtimeSinceStartup < deadline && controller.Run.State == RunState.InProgress)
            {
                if (controller.IsIdle)
                {
                    controller.Submit(AutoPilot.Decide(controller.Run));
                    actions++;
                    onAction?.Invoke();
                }
                yield return null;
            }
            while (controller.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null; // Finish the last turn.
            Time.timeScale = 1f;
        }

        static string DescribeScene()
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var parts = new string[roots.Length];
            for (int i = 0; i < roots.Length; i++)
            {
                var components = roots[i].GetComponents<Component>();
                var names = new string[components.Length];
                for (int c = 0; c < components.Length; c++) names[c] = components[c] == null ? "<missing script>" : components[c].GetType().Name;
                parts[i] = $"{roots[i].name} [{string.Join(", ", names)}]";
            }
            return $"'{scene.path}': {string.Join("; ", parts)}";
        }
    }
}

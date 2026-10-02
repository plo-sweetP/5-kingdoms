using System.Collections;
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
    /// </summary>
    public class DungeonSceneSmokeTest
    {
        const int Actions = 150;
        const float TimeLimitSeconds = 90f;

        [UnityTest]
        public IEnumerator AutopilotPlaysTheSceneWithoutErrors()
        {
            yield return SceneManager.LoadSceneAsync("Dungeon");
            var controller = Object.FindFirstObjectByType<DungeonController>();
            Assert.IsNotNull(controller, "the Dungeon scene should contain a DungeonController. Scene contents: " + DescribeScene());
            yield return null; // Let Start() begin the run.

            Time.timeScale = 4f; // Animations still play, just faster.
            int actions = 0;
            float deadline = Time.realtimeSinceStartup + TimeLimitSeconds;
            while (actions < Actions && Time.realtimeSinceStartup < deadline && controller.Run.State == RunState.InProgress)
            {
                if (controller.IsIdle)
                {
                    controller.Submit(AutoPilot.Decide(controller.Run));
                    actions++;
                }
                yield return null;
            }
            Time.timeScale = 1f;

            Assert.Greater(actions, 30, "the autopilot should get through a good number of actions");
            Assert.IsTrue(controller.Run.Turn > 0);
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

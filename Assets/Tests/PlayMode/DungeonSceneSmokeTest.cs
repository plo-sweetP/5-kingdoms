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
            Assert.AreEqual(0, SpriteLibrary.MissingCount, "every sprite the scene asks for exists: no placeholder squares");
        }

        [UnityTest]
        public IEnumerator EveryHeroCanShowEveryWeaponAndArmorSet()
        {
            // The looks 1h's gear will set: each hero stacked from layers with each of GEAR.md's nine weapons (which pick
            // the body) and each of its nine armor sets, with and without the head piece.
            var manifest = ArtManifest.Current;
            int weapons = 0;
            foreach (var rig in manifest.rigs) weapons += rig.weapons.Length;
            Assert.AreEqual(9, weapons, "GEAR.md's nine weapons");
            Assert.AreEqual(9, manifest.armors.Length, "GEAR.md's nine armor sets");
            Assert.AreEqual(3, manifest.heroes.Length);

            foreach (var hero in manifest.heroes)
            {
                foreach (var rig in manifest.rigs)
                    foreach (var weapon in rig.weapons)
                        AssertLookWorks(new HeroLook { Hero = hero.id, Weapon = weapon.id, Armor = hero.armor });
                foreach (var armor in manifest.armors)
                {
                    AssertLookWorks(new HeroLook { Hero = hero.id, Weapon = "gauntlets", Armor = armor.id });
                    yield return null;
                }
                AssertLookWorks(new HeroLook { Hero = hero.id, Weapon = hero.weapon, Armor = hero.armor, HeadPiece = false });
            }
            Assert.AreEqual(0, SpriteLibrary.MissingCount, "every layer and head part exists");
        }

        static void AssertLookWorks(HeroLook look)
        {
            var sprites = HeroComposer.Build(look);
            Assert.IsTrue(sprites.Has("idle") && sprites.Has("run"), $"{look.Key} stands and walks");
            Assert.IsTrue(sprites.Has("attack") || sprites.Has("cast"), $"{look.Key} can strike or cast");
            Assert.IsNotNull(sprites.Portrait, $"{look.Key} has a portrait");
            Assert.IsNotNull(sprites.Shadow, $"{look.Key} has a shadow");
            var frame = sprites.Idle.Frames[0];
            Assert.Greater(SpriteLibrary.VisibleTop(frame), 0.5f, $"{look.Key} is drawn (its head is above its tile's centre)");
        }

        [UnityTest]
        public IEnumerator AStrongHeroBeatsTheTroll()
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
            Assert.AreEqual(0, SpriteLibrary.MissingCount, "the boss floor's art exists too: no placeholder squares");
        }

        [UnityTest]
        public IEnumerator TheAutoButtonPlaysUntilTurnedOff()
        {
            DungeonController.Overrides = new LaunchOptions { SavePath = savePath, FreshSave = true };
            yield return LoadDungeon();
            var controller = Object.FindFirstObjectByType<DungeonController>();
            Time.timeScale = 4f;

            controller.AutoPilotEnabled = true;
            float deadline = Time.realtimeSinceStartup + 60f;
            while (controller.Run.Turn < 20 && controller.Run.State == RunState.InProgress && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(controller.Run.Turn >= 20 || controller.Run.State != RunState.InProgress, "auto-pilot plays on its own");

            // A move or action from the player is ignored while auto plays, and doesn't switch it off.
            controller.Submit(HeroCommand.Wait);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsTrue(controller.AutoPilotEnabled, "only the player turns the auto-pilot off, with the Auto button");

            controller.AutoPilotEnabled = false;
            while (controller.IsAnimating) yield return null;
            int turn = controller.Run.Turn;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(turn, controller.Run.Turn, "with auto-pilot off, nothing happens without input");
        }

        [UnityTest]
        public IEnumerator TheWeaponAttackIsAimedInTwoStepsAndATapOnAnEnemyAttacksIt()
        {
            DungeonController.Overrides = new LaunchOptions { SavePath = savePath, FreshSave = true };
            yield return LoadDungeon();
            var controller = Object.FindFirstObjectByType<DungeonController>();
            Time.timeScale = 4f;
            var run = controller.Run;

            // Nothing in reach at the start: the attack button says so, and no turn passes.
            controller.Submit(HeroCommand.Attack);
            yield return Settle(controller);
            Assert.IsFalse(controller.IsAiming, "nothing to aim at");
            Assert.AreEqual(0, run.Turn, "no swing at the air");

            yield return PlayUntilAFoeIsInReach(controller);
            Assert.Greater(run.AimFor(run.Hero, null).Options.Count, 0, $"the autopilot never reached a foe ({run.State} on turn {run.Turn})");

            // Step one: the button. The reach lights up, a target is marked, and nothing has happened yet.
            int turn = run.Turn;
            controller.Submit(HeroCommand.Attack);
            yield return Settle(controller);
            Assert.IsTrue(controller.IsAiming, "the weapon attack waits for its target");
            Assert.AreEqual(turn, run.Turn);

            // A tap away from every target lets it go.
            controller.Tap(run.Hero.Pos);
            yield return Settle(controller);
            Assert.IsFalse(controller.IsAiming);
            Assert.AreEqual(turn, run.Turn);

            // Step two: pressed again, it fires at the marked target.
            var aim = run.AimFor(run.Hero, null);
            var marked = aim.Options[aim.Default].Target;
            int hp = marked.Hp;
            controller.Submit(HeroCommand.Attack);
            yield return Settle(controller);
            Assert.IsTrue(controller.IsAiming);
            controller.Submit(HeroCommand.Attack);
            yield return Settle(controller);
            Assert.IsFalse(controller.IsAiming);
            Assert.AreEqual(turn + 1, run.Turn, "one attack, one turn");
            Assert.Less(marked.Hp, hp, "on the marked target");
            if (run.State != RunState.InProgress) yield break;

            // Tapping an enemy in reach when not aiming attacks it directly: one tap per hit.
            yield return PlayUntilAFoeIsInReach(controller);
            aim = run.AimFor(run.Hero, null);
            if (aim.Options.Count == 0) yield break; // The run ended first.
            var tapped = aim.Options[aim.Default].Target;
            hp = tapped.Hp;
            turn = run.Turn;
            controller.Tap(tapped.Pos);
            yield return Settle(controller);
            Assert.AreEqual(turn + 1, run.Turn);
            Assert.Less(tapped.Hp, hp);
        }

        [Test]
        public void WhileAutoPlaysThePlayersMovesAndActionsAreIgnored()
        {
            var run = new DungeonRun(3);
            var autoChoice = AutoPilot.Decide(run);
            var playerCommands = new[]
            {
                HeroCommand.Move(Direction8.N), HeroCommand.Attack, HeroCommand.AttackAt(new GridPos(1, 1)), HeroCommand.Wait, HeroCommand.UseBerry,
                HeroCommand.Descend,
            };
            foreach (var playerCommand in playerCommands)
            {
                var chosen = DungeonController.ChooseCommand(playerCommand, autoPilot: true, run);
                Assert.AreEqual(autoChoice, chosen.Value, $"{playerCommand} is ignored during auto");
                Assert.AreEqual(playerCommand, DungeonController.ChooseCommand(playerCommand, autoPilot: false, run).Value,
                    "without auto, the player's command is used");
            }
            Assert.IsNull(DungeonController.ChooseCommand(null, autoPilot: false, run), "no input, no auto: nothing happens");
        }

        [Test]
        public void WhileAutoPlaysThePlayerCanStillUseSkillsUltimatesAndSwitchHeroes()
        {
            var run = new DungeonRun(3);
            var commands = new[]
            {
                HeroCommand.Skill(1), HeroCommand.Skill(2, Direction8.E), HeroCommand.SkillAt(1, new GridPos(4, 2)), HeroCommand.UltimateFacing,
                HeroCommand.UltimateAt(new GridPos(4, 2)), HeroCommand.SwitchLeader(1),
            };
            foreach (var command in commands)
                Assert.AreEqual(command, DungeonController.ChooseCommand(command, autoPilot: true, run).Value);
        }

        [Test]
        public void ARefusedSkillOrUltimateSaysWhy()
        {
            // Haiden alone on a fresh floor: full HP, nobody near.
            var run = new DungeonRun(3, new DungeonRunConfig { Hero = ActorCatalog.Haiden });
            StringAssert.Contains("needs healing", DungeonController.SkillRefusalMessage(run, HeroCommand.Skill(0)));
            StringAssert.Contains("No enemy next to you", DungeonController.SkillRefusalMessage(run, HeroCommand.Skill(1)));
            var archer = new DungeonRun(3, new DungeonRunConfig { Hero = ActorCatalog.Uzuki });
            StringAssert.Contains("No enemy in sight", DungeonController.SkillRefusalMessage(archer, HeroCommand.Skill(1)));
            StringAssert.Contains("No enemy in sight", DungeonController.SkillRefusalMessage(archer, HeroCommand.SkillAt(1, archer.Hero.Pos + new GridPos(2, 1))));
            // The weapon attack with nothing in reach is stopped with a hint instead of a swing at the air.
            StringAssert.Contains("No enemy in sight within 5 tiles for Quick Shot", DungeonController.NoAttackTargetMessage(archer.Hero));
            StringAssert.Contains("No enemy next to you for Sword Slash", DungeonController.NoAttackTargetMessage(run.Hero));
            run.Hero.SkillCooldowns[1] = 1;
            StringAssert.Contains("ready again next turn", DungeonController.SkillRefusalMessage(run, HeroCommand.Skill(1)));
            StringAssert.Contains("is charging", DungeonController.UltimateRefusalMessage(run, HeroCommand.UltimateFacing));
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

        /// <summary>Lets the controller take the input in and play out whatever it started.</summary>
        static IEnumerator Settle(DungeonController controller)
        {
            yield return null;
            yield return null;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (controller.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>The autopilot plays until the leader has an enemy in its weapon attack's reach (or the run ends).</summary>
        static IEnumerator PlayUntilAFoeIsInReach(DungeonController controller)
        {
            float deadline = Time.realtimeSinceStartup + 120f;
            while (Time.realtimeSinceStartup < deadline && controller.Run.State == RunState.InProgress)
            {
                if (controller.IsIdle)
                {
                    var run = controller.Run;
                    if (run.AimFor(run.Hero, null).Options.Count > 0) yield break;
                    controller.Submit(AutoPilot.Decide(run));
                }
                yield return null;
            }
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

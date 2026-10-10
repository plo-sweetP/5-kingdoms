using System;
using System.Collections;
using System.IO;
using FiveKingdoms.Core;
using UnityEngine;

namespace FiveKingdoms.Dungeon
{
    /// <summary>
    /// Unattended smoke test for builds: launch the game with "-fk-autoplay &lt;folder&gt;" and the hero plays
    /// itself using the AutoPilot, saving screenshots along the way (some mid-animation) and quitting after
    /// a fixed number of actions. The first use of each targeted action goes through the player's two-step aiming
    /// (press, screenshot of the highlight, press again), so that path runs in the build too. It also captures the
    /// first times the leader holds a doorway against a crowd and the first times the front rotates ("door_*.png"),
    /// and the first times a hero waits out of a boss's wind-up ("keep_clear*.png").
    /// Does nothing in normal play.
    /// </summary>
    public sealed class AutoplayDriver : MonoBehaviour
    {
        const string Flag = "-fk-autoplay";
        const string DemoFlag = "-fk-demo"; // With "view": stage far targets and capture how the camera shows them.
        const int MaxActions = 400;
        const float TimeLimit = 120f;
        static readonly int[] ShotAfterAction = { 1, 12, 30, 60, 100, 160, 240, 330 };

        DungeonController controller;
        string folder;
        string demo;

        public static void AttachIfRequested(DungeonController controller)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Flag);
            if (index < 0) return;
            Application.runInBackground = true; // Unattended: keep playing when the window loses focus.
            var driver = controller.gameObject.AddComponent<AutoplayDriver>();
            driver.controller = controller;
            driver.folder = index + 1 < args.Length && !args[index + 1].StartsWith("-")
                ? args[index + 1]
                : Path.Combine(Application.persistentDataPath, "autoplay");
            int demoIndex = Array.IndexOf(args, DemoFlag);
            if (demoIndex >= 0 && demoIndex + 1 < args.Length) driver.demo = args[demoIndex + 1];
        }

        /// <summary>
        /// The view-size demo (docs/design/ART.md, "View size"): the leader in the middle of the longest straight
        /// north-south run of floor on the map, a foe five tiles up and one five tiles down, the widest a shot's targets
        /// can be apart. Captures the scene before and while the leader aims its weapon attack, to compare the camera's
        /// modes (-fk-view) with the same -fk-seed; then once more with a single foe three tiles away, which a small
        /// move of the camera shows without stepping out. Only moves actors on the autoplay's throwaway run.
        /// </summary>
        IEnumerator ViewDemo()
        {
            yield return new WaitForSeconds(2.6f); // The floor banner has gone.
            yield return Capture("view_1_exploring");

            var run = controller.Run;
            var map = run.Map;
            var hero = run.Hero;
            const int reach = SkillCatalog.RangedReach;
            GridPos? spot = null;
            for (int y = reach; y < map.Height - reach && spot == null; y++)
                for (int x = 0; x < map.Width && spot == null; x++)
                {
                    bool clear = true;
                    for (int d = -reach; d <= reach && clear; d++) clear = map.IsWalkable(new GridPos(x, y + d));
                    if (clear) spot = new GridPos(x, y);
                }
            if (!spot.HasValue)
            {
                Debug.Log("[Autoplay] View demo: this floor has no straight run of 11 tiles; try another -fk-seed.");
                Application.Quit();
                yield break;
            }
            var centre = spot.Value;
            foreach (var actor in run.Actors)
                if (actor.Team != hero.Team) actor.Pos = actor.PreviousPos = new GridPos(-50 - actor.Id, -50); // Out of the picture.
            hero.Pos = hero.PreviousPos = centre;
            int placed = 0;
            foreach (var member in run.Party)
            {
                if (member == hero) continue;
                var beside = new GridPos(centre.X + (placed == 0 ? -1 : 1), centre.Y);
                member.Pos = member.PreviousPos = map.IsWalkable(beside) ? beside : new GridPos(centre.X, centre.Y - 1 - placed);
                placed++;
            }
            var above = run.SpawnEnemy(new GridPos(centre.X, centre.Y + reach));
            var below = run.SpawnEnemy(new GridPos(centre.X, centre.Y - reach));
            controller.RefreshView();
            yield return new WaitForSeconds(0.5f);
            yield return Capture("view_2_foes_five_tiles_away");

            controller.Submit(HeroCommand.Attack);
            yield return new WaitForSeconds(0.8f);
            bool aimed = controller.IsAiming;
            yield return Capture(aimed ? "view_3_aiming" : "view_3_not_aiming");

            above.Pos = above.PreviousPos = new GridPos(centre.X, centre.Y + 3);
            below.Pos = below.PreviousPos = new GridPos(-40, -50);
            controller.RefreshView(); // Also lets the aim go.
            yield return new WaitForSeconds(0.5f);
            controller.Submit(HeroCommand.Attack);
            yield return new WaitForSeconds(0.8f);
            yield return Capture(controller.IsAiming ? "view_4_aiming_one_foe_three_tiles_away" : "view_4_not_aiming");
            Debug.Log($"[Autoplay] View demo captured at {centre} (aiming: {aimed}, then {controller.IsAiming}).");
            Application.Quit();
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            if (demo == "view")
            {
                yield return ViewDemo();
                yield break;
            }
            if (demo == "tree")
            {
                yield return TreeDemo();
                yield break;
            }
            // With -fk-tree the skill tree is up before the run: its states first, with a point spent where there is one.
            yield return null;
            if (controller.SkillTree.IsOpen) yield return TreeTour("00_tree", change: true);
            yield return new WaitForSeconds(0.6f);
            yield return Capture("00_banner");
            yield return new WaitForSeconds(1.8f);
            yield return Capture("01_start");
            // How the HUD looks while Auto plays: the D-pad hidden, the leader's buttons greyed out.
            controller.AutoPilotEnabled = true;
            yield return null;
            yield return Capture("01_auto_on");
            controller.AutoPilotEnabled = false;
            while (controller.IsAnimating) yield return null;
            // The pause menu's pages: the main one, a hero's stats, the settings, and a question before a restart.
            var menu = FindFirstObjectByType<FiveKingdoms.UI.DungeonHud>().PauseMenu;
            controller.Paused = true;
            yield return Capture("01_pause_menu");
            menu.Move(3);
            menu.Activate();
            yield return Capture("01_pause_hero_stats");
            menu.Back();
            menu.Move(2);
            menu.Activate();
            yield return Capture("01_pause_settings");
            menu.Back();
            menu.Move(1);
            menu.Activate();
            yield return Capture("01_pause_restart_question");
            menu.Back();
            // The skill tree as the hero stats page opens it during a run: to look at only.
            controller.OpenSkillTree(0);
            yield return TreeTour("01_tree", change: false);
            menu.Back();

            int actions = 0, shot = 0, attackShots = 0, chargeShots = 0, holdShots = 0, rotateShots = 0, restShots = 0, clearShots = 0;
            var skillsShown = new System.Collections.Generic.HashSet<string>();
            var ultimatesShown = new System.Collections.Generic.HashSet<string>();
            var aimsShown = new System.Collections.Generic.HashSet<string>();
            float started = Time.realtimeSinceStartup;
            while (actions < MaxActions && Time.realtimeSinceStartup - started < TimeLimit)
            {
                var run = controller.Run;
                if (run.State != RunState.InProgress) break;
                if (!controller.IsIdle)
                {
                    yield return null;
                    continue;
                }

                var command = AutoPilot.Decide(run);
                // The first time the autopilot picks each targeted action, do it the way a player does: press its button
                // (the reach lights up and a target is marked), take a screenshot, then press again to fire at the marked one.
                if (command.Targeted && !controller.IsAiming && aimsShown.Add(AimName(run, command)))
                {
                    var button = command.Kind == HeroCommandKind.Attack ? HeroCommand.Attack
                        : command.Kind == HeroCommandKind.Ultimate ? HeroCommand.UltimateFacing
                        : HeroCommand.Skill(command.Slot);
                    controller.Submit(button);
                    yield return null;
                    yield return null;
                    if (controller.IsAiming)
                    {
                        yield return new WaitForSeconds(0.3f); // The camera moves to frame the targets.
                        yield return Capture($"aim_{AimName(run, command)}_action{actions + 1}");
                        controller.Submit(button);
                        actions++;
                        yield return null;
                    }
                    continue;
                }
                bool attacks = command.Kind == HeroCommandKind.Attack; // Always an explicit command: nobody attacks by walking into a foe.
                bool bossWasHelped = run.Boss?.CalledForHelp ?? true;
                bool fighting = run.InCombat;
                var skill = command.Kind == HeroCommandKind.Skill ? run.Hero.Skills[command.Slot]
                    : command.Kind == HeroCommandKind.Ultimate ? run.Hero.Ultimate
                    : null;
                controller.Submit(command);
                actions++;
                yield return null; // Let the controller resolve the turn.
                var boss = controller.Run.Boss;
                if (skill != null && (skill.IsUltimate ? ultimatesShown.Add(skill.Id) : skillsShown.Add(skill.Id)))
                {
                    // The first use of each skill and each ultimate, at its showiest moment: the slash, the holy light,
                    // the heal sparkles, the afterimages, the arrow rain.
                    yield return new WaitForSeconds(skill.IsUltimate ? 0.55f : skill.Effect == SkillEffect.Strike ? 0.24f : skill.Effect == SkillEffect.Heal ? 0.2f : 0.08f);
                    yield return Capture($"{(skill.IsUltimate ? "ultimate" : "skill")}_{skill.Id}_action{actions}");
                }
                else if (boss != null && boss.Charging && chargeShots < 2)
                {
                    while (controller.IsAnimating) yield return null; // Wind-up pose, warning tiles and the refreshed turn order.
                    yield return Capture($"boss_charge{++chargeShots}_action{actions}");
                }
                else if (boss != null && boss.CalledForHelp && !bossWasHelped)
                {
                    yield return new WaitForSeconds(0.45f); // The reinforcements fading in.
                    yield return Capture($"boss_summon_action{actions}");
                }
                else if (clearShots < 2 && KeptClear(controller.Run))
                {
                    yield return new WaitForSeconds(0.2f); // A hero waits out of the slam's reach, with the word over its head.
                    yield return Capture($"keep_clear{++clearShots}_action{actions}");
                }
                else if (command.Holding && holdShots < 3)
                {
                    while (controller.IsAnimating) yield return null; // The leader in the doorway, the foes coming up to it.
                    yield return Capture($"door_hold{++holdShots}_action{actions}");
                }
                else if (fighting && rotateShots < 2 && FrontRotated(controller.Run))
                {
                    while (controller.IsAnimating) yield return null; // The fresh hero in front, the hurt one behind it.
                    yield return Capture($"door_rotate{++rotateShots}_action{actions}");
                }
                else if (command.Resting && restShots < 2)
                {
                    yield return new WaitForSeconds(0.2f); // The leader waits, the heals sparkle, the log says why.
                    yield return Capture($"rest{++restShots}_action{actions}");
                }
                else if (attacks && attackShots < 2)
                {
                    yield return new WaitForSeconds(0.12f); // Just after the lunge connects: slash and hit flash.
                    yield return Capture($"attack{++attackShots}_action{actions}");
                }
                else if (shot < ShotAfterAction.Length && actions >= ShotAfterAction[shot])
                {
                    yield return new WaitForSeconds(0.09f); // Mid-animation, to see steps, swings and hits.
                    yield return Capture($"{shot + 2:00}_action{actions}_{command.Kind}");
                    shot++;
                }
                yield return null;
            }

            while (controller.IsAnimating) yield return null; // Let the last turn finish so the end panel shows.
            yield return new WaitForSeconds(0.8f);
            yield return Capture("99_end");
            var final = controller.Run;
            Debug.Log($"[Autoplay] Finished after {actions} actions: {final.State} on B{final.Floor}F, " +
                      $"Lv {final.Hero.Level}, HP {final.Hero.Hp}/{final.Hero.MaxHp}, turn {final.Turn}.");
            Application.Quit();
        }

        /// <summary>Whether two melee heroes traded places in the last action: in a fight they only do that to rotate the front.</summary>
        static bool FrontRotated(DungeonRun run)
        {
            foreach (var e in run.Events)
                if (e is SwappedEvent swap && swap.Reason == SwapReason.Rotate) return true;
            return false;
        }

        /// <summary>Whether a hero's AI waited out of a wound-up slam's reach in the last action ("Footing in a boss fight").</summary>
        static bool KeptClear(DungeonRun run)
        {
            foreach (var e in run.Events)
                if (e is HeroWaitedEvent waited && waited.Reason == WaitReason.KeepsClear) return true;
            return false;
        }

        /// <summary>What a targeted command aims: the leader's weapon attack, or the skill or ultimate by its id.</summary>
        static string AimName(DungeonRun run, HeroCommand command) =>
            command.Kind == HeroCommandKind.Attack ? "attack_" + run.Hero.Definition.Id
            : command.Kind == HeroCommandKind.Ultimate ? run.Hero.Ultimate.Id
            : run.Hero.Skills[command.Slot].Id;

        /// <summary>
        /// The skill tree's states, captured as "<paramref name="prefix"/>_*.png": a class, a row that isn't written
        /// yet, the loadout with the skills that could go in a slot, the question before unlearning, another hero.
        /// With <paramref name="change"/> a point goes into the hero's own class first, where it has one.
        /// </summary>
        IEnumerator TreeTour(string prefix, bool change)
        {
            var tree = controller.SkillTree;
            yield return null;
            yield return Capture(prefix + "_1_class");
            if (change && tree.Model.Info.Enabled)
            {
                tree.Activate();
                yield return Capture(prefix + "_2_raised");
            }
            tree.Tap(TreeFocus.Option(0, 1));
            yield return Capture(prefix + "_3_locked_row");
            tree.Tap(TreeFocus.Slot(0));
            tree.Activate();
            yield return Capture(prefix + "_4_loadout");
            if (tree.Model.Focus.Zone == TreeZone.Choices) tree.Back();
            tree.Tap(TreeFocus.Unlearn, press: true);
            yield return Capture(prefix + "_5_unlearn");
            if (tree.Model.Confirming) tree.Back();
            tree.NextHero(1);
            tree.Tap(TreeFocus.Class(1));
            yield return Capture(prefix + "_6_next_hero");
            Debug.Log($"[Autoplay tour] Skill tree toured ({(tree.Model.ReadOnly ? "looking only" : "changing")}): {tree.Model.Notice ?? "no notice"}");
            controller.CloseSkillTree();
            yield return null;
        }

        /// <summary>
        /// "-fk-demo tree": the skill tree for every hero in turn, as between runs, then quit: the screen's screenshots
        /// without playing a run ("tree_<hero>_*.png"). Each hero's own class goes as far as it can, a second class gets
        /// two tiers, and the row that isn't written, the loadout and the question before unlearning are shown.
        /// </summary>
        IEnumerator TreeDemo()
        {
            yield return null;
            if (!controller.SkillTree.IsOpen) controller.OpenSkillTree(0);
            yield return new WaitForSeconds(0.3f);
            var tree = controller.SkillTree;
            for (int i = 0; i < tree.Model.Party.Count; i++)
            {
                string hero = tree.Model.Hero.Definition.Id;
                yield return Capture($"tree_{hero}_1_class");
                while (tree.Model.Focus.Zone == TreeZone.Classes && tree.Model.Info.Enabled) tree.Activate();
                yield return Capture($"tree_{hero}_2_as_far_as_it_goes");
                tree.Tap(TreeFocus.Option(0, 1));
                yield return Capture($"tree_{hero}_3_locked_row");
                tree.Tap(TreeFocus.Class((tree.Model.ClassIndex + 1) % tree.Model.Classes.Count));
                tree.Activate();
                tree.Activate();
                yield return Capture($"tree_{hero}_4_second_class");
                tree.Tap(TreeFocus.Slot(0));
                tree.Activate();
                yield return Capture($"tree_{hero}_5_loadout");
                if (tree.Model.Focus.Zone == TreeZone.Choices) tree.Back();
                tree.Tap(TreeFocus.Unlearn, press: true);
                yield return Capture($"tree_{hero}_6_unlearn_question");
                if (tree.Model.Confirming) tree.Back();
                tree.NextHero(1);
            }
            Debug.Log("[Autoplay] Skill tree demo captured.");
            Application.Quit();
        }

        IEnumerator Capture(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return null;
            yield return null;
        }
    }
}

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
    /// first times the leader holds a doorway against a crowd and the first times the front rotates ("door_*.png").
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
            yield return new WaitForSeconds(0.6f);
            yield return Capture("00_banner");
            yield return new WaitForSeconds(1.8f);
            yield return Capture("01_start");

            int actions = 0, shot = 0, attackShots = 0, chargeShots = 0, holdShots = 0, rotateShots = 0;
            var skillsShown = new System.Collections.Generic.HashSet<SkillEffect>();
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
                var skill = command.Kind == HeroCommandKind.Skill ? run.Hero.Definition.Skills[command.Slot]
                    : command.Kind == HeroCommandKind.Ultimate ? run.Hero.Definition.Ultimate
                    : null;
                controller.Submit(command);
                actions++;
                yield return null; // Let the controller resolve the turn.
                var boss = controller.Run.Boss;
                if (skill != null && (skill.IsUltimate ? ultimatesShown.Add(skill.Id) : skillsShown.Add(skill.Effect)))
                {
                    // The first of each kind of skill and each ultimate, at its showiest moment: the slash, the heal
                    // sparkles, the afterimages, the arrow rain.
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
                if (e is SwappedEvent swap && IsMeleeHero(run, swap.ActorId) && IsMeleeHero(run, swap.OtherId)) return true;
            return false;
        }

        static bool IsMeleeHero(DungeonRun run, int actorId)
        {
            foreach (var member in run.Party)
                if (member.Id == actorId) return !member.Definition.IsRanged;
            return false;
        }

        /// <summary>What a targeted command aims: the leader's weapon attack, or the skill or ultimate by its id.</summary>
        static string AimName(DungeonRun run, HeroCommand command) =>
            command.Kind == HeroCommandKind.Attack ? "attack_" + run.Hero.Definition.Id
            : command.Kind == HeroCommandKind.Ultimate ? run.Hero.Definition.Ultimate.Id
            : run.Hero.Definition.Skills[command.Slot].Id;

        IEnumerator Capture(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return null;
            yield return null;
        }
    }
}

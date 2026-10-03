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
    /// a fixed number of actions. Does nothing in normal play.
    /// </summary>
    public sealed class AutoplayDriver : MonoBehaviour
    {
        const string Flag = "-fk-autoplay";
        const int MaxActions = 400;
        const float TimeLimit = 120f;
        static readonly int[] ShotAfterAction = { 1, 12, 30, 60, 100, 160, 240, 330 };

        DungeonController controller;
        string folder;

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
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            yield return new WaitForSeconds(0.6f);
            yield return Capture("00_banner");
            yield return new WaitForSeconds(1.8f);
            yield return Capture("01_start");

            int actions = 0, shot = 0, attackShots = 0, chargeShots = 0;
            var skillsShown = new System.Collections.Generic.HashSet<SkillEffect>();
            var ultimatesShown = new System.Collections.Generic.HashSet<string>();
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
                bool attacks = command.Kind == HeroCommandKind.Attack || command.Kind == HeroCommandKind.Move &&
                    run.ActorAt(run.Hero.Pos + command.Direction.ToOffset()) is Actor target && target.Team != Team.Hero;
                bool bossWasHelped = run.Boss?.CalledForHelp ?? true;
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

        IEnumerator Capture(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return null;
            yield return null;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using FiveKingdoms.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FiveKingdoms.UI
{
    /// <summary>What the player last used: decides whether touch controls show and which button hints to give.</summary>
    public enum InputMode { Touch, Keyboard, Gamepad }

    /// <summary>
    /// Landscape HUD for the dungeon, built in code (docs/design/HUD.md). The party's HP, ultimate charge, EXP and levels
    /// and the floor across the top, plus a boss bar on boss floors; Wait, Berry, Auto and Pause in a row top-right;
    /// D-pad bottom-left; the weapon attack bottom-right with the three skills and the ultimate in an arc around it,
    /// within the thumb's reach (each starts aiming: pick it, then the target); a message log; an aiming prompt; floating numbers;
    /// fades, floor banner and end-of-run panel. The D-pad and attack button hide while a keyboard or controller is in
    /// use (PC, Steam Deck); the skill buttons stay, in a row with their keys, since they also show cooldowns, Quick
    /// tags and the charge. While Auto plays, the D-pad is hidden and every button that acts for the leader is greyed.
    /// Layout is in 1920x1080 reference pixels, scaled to the screen height and kept inside the safe area. The art is
    /// the Tiny Swords UI kit (round buttons, the gold-cornered panel, a ribbon for the dungeon's name) and frames,
    /// bars and rectangular buttons drawn to match it, at whole screen pixels per art pixel (<see cref="UiArtScaler"/>).
    /// </summary>
    public sealed class DungeonHud : MonoBehaviour
    {
        public static readonly Color TextColor = new Color(0.95f, 0.95f, 0.97f);
        public static readonly Color HintColor = new Color(0.75f, 0.75f, 0.82f);
        static readonly Color PanelColor = new Color(0.3f, 0.33f, 0.42f, 0.92f);
        static readonly Color QuickTagColor = new Color(0.45f, 0.9f, 0.68f);
        static readonly Color BossBarColor = new Color(0.86f, 0.36f, 0.3f);
        static readonly Color HeroTurnColor = new Color(0.36f, 0.62f, 0.95f);
        static readonly Color EnemyTurnColor = new Color(0.8f, 0.36f, 0.36f);
        static readonly Color BossTurnColor = new Color(0.72f, 0.45f, 0.9f);
        static readonly Color SlamColor = new Color(1f, 0.35f, 0.3f);

        // HUD art (Resources/Sprites/UI): the kit's round buttons for the attack, skills and ultimate, its rectangular
        // ones for the rest.
        const string AttackArt = "round_red";
        const string SkillArt = "tiny_blue";
        const string UltimateArt = "tiny_dark";
        const string UltimateReadyArt = "tiny_gold";
        const string ActionArt = "button_dark";
        const string ActiveArt = "button_gold";

        const int TimelineTurnsShown = 5;
        const float TimelineIcon = 52f;
        const float TimelineGap = 4f;
        const float TimelineDivider = 16f;

        /// <summary>Key hints sit just under the party's cards (3 heroes), the turn-order strip under the hints.</summary>
        static readonly float KeysTop = -24f - PartyPanel.Height(3) - 8f;

        const string KeyboardHint = "Move: WASD/arrows + QEZC   Attack: Space, then a target   Skills: 1 2 3   Ultimate: 4   Switch hero: Tab   Tactics: G\n" +
                                    "Wait: X   Berry: B   Go down: Enter   Auto: T   Pause: Esc   Aim: arrows pick, Space fires, Esc cancels (or click the enemy)";
        const string GamepadHint = "Move: stick/D-pad   Attack: A, then a target   Skills: LB LT RT   Ultimate: RB   Switch hero: B   Tactics: L3\n" +
                                   "Wait: Y   Berry: X   Go down: R3   Auto: View   Pause: Start   Aim: stick picks, A fires, B cancels";
        static readonly string[] KeyboardSkillKeys = { "1", "2", "3", "4" };
        static readonly string[] GamepadSkillKeys = { "LB", "LT", "RT", "RB" };

        // Skill buttons (three skills, then the ultimate): around the attack button for thumbs, or in a row for keys.
        // The arc is Peter's choice (HUD.md, 2026-10-05: he tried a row along the bottom and went back to the arc, "the
        // more reachable orientation"). Its top, 555 units up, stays under the minimap on every screen.
        static readonly Vector2[] TouchSkillPositions = { new Vector2(-470f, 120f), new Vector2(-455f, 335f), new Vector2(-330f, 485f), new Vector2(-135f, 460f) };
        static readonly float[] TouchSkillSizes = { 140f, 140f, 140f, 150f };
        static readonly Vector2[] RowSkillPositions = { new Vector2(-560f, 100f), new Vector2(-420f, 100f), new Vector2(-280f, 100f), new Vector2(-125f, 108f) };
        static readonly float[] RowSkillSizes = { 124f, 124f, 124f, 140f };

        // The top-right row, right to left: Pause, Auto, Berry, Wait.
        static readonly Vector2 TopButtonSize = new Vector2(124f, 80f);
        const float TopButtonGap = 12f;

        const float LogWidth = 660f;
        const float LogLineHeight = 38f;

        const int LogLines = 4;
        const float LogLifetime = 6f;
        const float LogFade = 1f;

        public event Action<HeroCommand> CommandRequested;
        public event Action RestartRequested;

        /// <summary>The end panel's Skills button: between runs is when a build changes.</summary>
        public event Action SkillTreeRequested;
        public event Action AutoPilotToggled;
        public event Action PauseRequested;

        /// <summary>A partner's tactic badge was tapped (party index).</summary>
        public event Action<int> TacticCycleRequested;

        public DPad DPad { get; private set; }

        /// <summary>Opened and closed by the controller, which also does what is chosen in it.</summary>
        public PauseMenu PauseMenu { get; private set; }

        /// <summary>The skill-tree screen. Like the pause menu, it only asks: the controller opens, saves and closes.</summary>
        public SkillTreeScreen SkillTree { get; private set; }

        readonly List<LogLine> log = new List<LogLine>();
        readonly List<FloatingText> floating = new List<FloatingText>();
        readonly List<GameObject> timelineRows = new List<GameObject>();
        RectTransform timelineRoot;
        Text timelineHeader;
        Camera worldCamera;
        PixelCamera pixelCamera;
        RectTransform canvasRect, safeArea, floatingLayer, logRoot, touchControls;
        Text floorText, bossName, keysText, bannerTitle, bannerSubtitle, endTitle, endDetail, aimPrompt;
        Image bossFill;
        PartyPanel party;
        Minimap minimap;
        GameObject bossPanel;
        int bossMaxHp;
        HoldButton berryButton, descendButton, againButton, skillsButton, autoButton, attackButton, waitButton, pauseButton;
        readonly HoldButton[] skillButtons = new HoldButton[4]; // Three skills, then the ultimate.
        readonly bool[] skillUsable = new bool[4];              // What the rules say; Auto greys them all the same.
        readonly Text[] skillKeys = new Text[4];
        readonly GameObject[] quickTags = new GameObject[3];
        bool autoPilotOn;
        bool hasBerries;
        float lastBlockedNotice = -10f;
        CanvasGroup banner, endPanel;
        Image fader;
        Coroutine bannerRoutine;
        InputMode inputMode = InputMode.Touch;

        sealed class LogLine
        {
            public Text Text;
            public float Born;
        }

        sealed class FloatingText
        {
            public Text Text;
            public Vector3 World;
            public float Age;
            public float Life;
        }

        public static DungeonHud Create(Camera worldCamera)
        {
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var go = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // Landscape: scale with screen height so controls keep their size.
            go.AddComponent<UiArtScaler>(); // Before anything is built: the HUD art sizes itself by it.

            var hud = go.AddComponent<DungeonHud>();
            hud.Build(worldCamera);
            return hud;
        }

        // ---- Public API used by the controller and the view ----

        public void Refresh(DungeonRun run)
        {
            party.Refresh(run);
            RefreshSkills(run);
            floorText.text = $"B{run.Floor}F";
            berryButton.SetLabel($"x{run.Berries}");
            hasBerries = run.Berries > 0;
            ApplyAvailability();
            minimap.Refresh(run);
            descendButton.gameObject.SetActive(run.State == RunState.InProgress && run.HeroOnStairs);
            RefreshTimeline(run);
        }

        /// <summary>
        /// The turn-order strip, shown only in combat: the next few turns top to bottom, how soon each comes (AV from
        /// now), dividers where a new cycle starts, and a SLAM tag on a winding-up boss's next turn.
        /// </summary>
        void RefreshTimeline(DungeonRun run)
        {
            foreach (var row in timelineRows) Destroy(row);
            timelineRows.Clear();
            bool show = run.InCombat && run.State == RunState.InProgress;
            timelineRoot.gameObject.SetActive(show);
            if (!show) return;

            int cycle = run.CombatCycle;
            timelineHeader.text = $"Turn order (Cycle {cycle + 1})";
            float y = -26f;
            // Only the fight itself: the party and the enemies chasing it. Monsters wandering elsewhere still take turns.
            var turns = new List<TimelineTurn>();
            foreach (var upcoming in run.Forecast(TimelineTurnsShown * 6))
                if (upcoming.Actor.Team == Team.Hero || upcoming.Actor.Alerted)
                    if (turns.Count < TimelineTurnsShown) turns.Add(upcoming);
            var listed = new HashSet<int>();
            for (int i = 0; i < turns.Count; i++)
            {
                var turn = turns[i];
                bool slam = turn.Actor.Charging && listed.Add(turn.Actor.Id); // Only its next turn is the slam.
                listed.Add(turn.Actor.Id);
                if (turn.Cycle != cycle)
                {
                    cycle = turn.Cycle;
                    var divider = UiFactory.CreateText("Cycle", timelineRoot, $"- Cycle {cycle + 1} -", 18, TextAnchor.MiddleLeft, HintColor);
                    UiFactory.Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(170f, TimelineDivider), new Vector2(0f, 1f));
                    timelineRows.Add(divider.gameObject);
                    y -= TimelineDivider;
                }
                timelineRows.Add(CreateTimelineRow(turn, run, isCurrent: i == 0, slam, y));
                y -= TimelineIcon + TimelineGap;
            }
        }

        GameObject CreateTimelineRow(TimelineTurn turn, DungeonRun run, bool isCurrent, bool slam, float y)
        {
            var actor = turn.Actor;
            var color = actor.Team == Team.Hero ? HeroTurnColor : actor.Definition.IsBoss ? BossTurnColor : EnemyTurnColor;
            var frame = UiFactory.CreatePanel("Turn", timelineRoot, "frame", slam ? SlamColor : color);
            UiFactory.Place(frame.rectTransform, new Vector2(0f, 1f), new Vector2(isCurrent ? 0f : 8f, y), new Vector2(TimelineIcon, TimelineIcon), new Vector2(0f, 1f));
            var icon = PartyPanel.CreatePortrait(frame.transform, new Vector2(0.5f, 0.5f), Vector2.zero, TimelineIcon - 8f, new Vector2(0.5f, 0.5f));
            icon.sprite = PartyPanel.PortraitOf(actor);
            icon.enabled = icon.sprite != null;
            UiArtScaler.Size(icon);

            int avFromNow = Mathf.RoundToInt((float)(turn.Time - run.CombatTime).ToDouble());
            string label = slam ? "SLAM!" : isCurrent ? "Now" : $"+{avFromNow}";
            var text = UiFactory.CreateText("When", frame.transform, label, 22, TextAnchor.MiddleLeft, slam ? SlamColor : isCurrent ? TextColor : HintColor);
            UiFactory.Place(text.rectTransform, new Vector2(1f, 0.5f), new Vector2(8f, 0f), new Vector2(90f, 30f), new Vector2(0f, 0.5f));
            return frame.gameObject;
        }

        /// <summary>Keeps a hero's card in step with the animation; the full refresh comes after the action.</summary>
        public void SetMemberHp(int actorId, int hp, int maxHp) => party.SetHp(actorId, hp, maxHp);

        public void SetMemberCharge(int actorId, int charge) => party.SetCharge(actorId, charge);

        /// <summary>
        /// The leader's buttons: the attack button names its weapon attack (Quick Shot, Sword Slash, Jab); each skill
        /// shows its name, a small Quick tag if it takes half a turn, and "next turn" while it sits out its cooldown; the
        /// ultimate shows its charge, and its name once it's ready. A skill that can't be used right now is dimmed but
        /// still answers a press, so the log can say why. <see cref="ApplyAvailability"/> sets what can be pressed.
        /// </summary>
        void RefreshSkills(DungeonRun run)
        {
            var hero = run.Hero;
            bool playing = run.State == RunState.InProgress;
            attackButton.SetLabel(hero.AttackName.Replace(' ', '\n'));
            var skills = hero.Skills;
            for (int i = 0; i < 3; i++)
            {
                var button = skillButtons[i];
                if (i >= skills.Count)
                {
                    button.SetLabel("-");
                    skillUsable[i] = false;
                    quickTags[i].SetActive(false);
                    continue;
                }
                var skill = skills[i];
                bool cooling = hero.SkillCooldowns[i] > 0;
                button.SetLabel(cooling ? $"{skill.ShortName}\n<size=22>next turn</size>" : skill.ShortName);
                quickTags[i].SetActive(skill.IsQuick);
                var check = run.CheckSkill(i);
                // Blocked only means "not the way the hero faces": aiming can still find room.
                skillUsable[i] = playing && (check == SkillCheck.Ready || check == SkillCheck.Blocked);
            }

            var ultimate = hero.Ultimate;
            var ult = skillButtons[3];
            if (ultimate == null)
            {
                ult.SetLabel("ULT");
                skillUsable[3] = false;
            }
            else
            {
                bool ready = hero.UltimateReady;
                ult.SetLabel(ready ? $"{ultimate.ShortName}\n<size=22>READY</size>" : $"ULT\n<size=22>{hero.Charge}%</size>");
                ult.SetArt(ready ? UltimateReadyArt : UltimateArt);
                skillUsable[3] = playing && ready;
            }
        }

        /// <summary>
        /// Aiming mode (PROGRESSION.md, "Targeting and input"): brackets close around the button being aimed and a
        /// prompt says how to fire. <paramref name="button"/>: 0-2 a skill, 3 the ultimate, -1 the weapon attack; null
        /// ends aiming.
        /// </summary>
        public void SetAiming(int? button, string prompt)
        {
            aimPrompt.gameObject.SetActive(button.HasValue);
            aimPrompt.text = prompt ?? "";
            attackButton.SetHighlight(button == -1);
            for (int i = 0; i < skillButtons.Length; i++) skillButtons[i].SetHighlight(button == i);
        }

        public void ShowBoss(string name, int hp, int maxHp)
        {
            bossName.text = name;
            bossMaxHp = Mathf.Max(1, maxHp);
            bossPanel.SetActive(true);
            SetBossHp(hp);
        }

        public void SetBossHp(int hp) => bossFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(hp / (float)bossMaxHp), 1f);

        public void HideBoss() => bossPanel.SetActive(false);

        /// <summary>
        /// Shows whether the auto-pilot is playing (the Auto button is gold while it does). While it plays the D-pad is
        /// hidden, and the attack, the skills, the ultimate, Wait and Berry are greyed out and don't act (HUD.md, "Auto").
        /// Auto, Pause, the party cards and the tactic badges still answer.
        /// </summary>
        public void SetAutoPilot(bool on)
        {
            autoPilotOn = on;
            autoButton.SetArt(on ? ActiveArt : ActionArt);
            DPad.Interactable = !on;
            DPad.gameObject.SetActive(!on);
            ApplyAvailability();
        }

        /// <summary>Which buttons answer: what the rules allow (a skill's cooldown, the berries left), and none of them while Auto plays.</summary>
        void ApplyAvailability()
        {
            bool free = !autoPilotOn;
            attackButton.Interactable = free;
            waitButton.Interactable = free;
            descendButton.Interactable = free;
            berryButton.Interactable = free && hasBerries;
            for (int i = 0; i < skillButtons.Length; i++) skillButtons[i].Interactable = free && skillUsable[i];
        }

        /// <summary>The minimap's size, or off (the pause menu's setting).</summary>
        public void SetMinimap(MinimapSize size) => minimap.SetSize(size);

        /// <summary>The game stands still under the pause menu: the Pause button is lit.</summary>
        public void SetPaused(bool on) => pauseButton.SetArt(on ? ActiveArt : ActionArt);

        /// <summary>The player tried to move or act while the auto-pilot plays (shown at most every couple of seconds).</summary>
        public void ShowAutoPilotBlocked()
        {
            if (Time.unscaledTime - lastBlockedNotice < 2f) return;
            lastBlockedNotice = Time.unscaledTime;
            AddMessage("Auto-pilot is playing. Press Auto to take over.", HintColor);
        }

        /// <summary>
        /// The D-pad and attack button show only for touch (or mouse); keyboard and controller get button hints instead,
        /// and the skill buttons move into a row labeled with their keys.
        /// </summary>
        public void SetInputMode(InputMode mode)
        {
            inputMode = mode;
            bool touch = mode == InputMode.Touch;
            touchControls.gameObject.SetActive(touch);
            // Between the D-pad and the buttons for touch; without the D-pad, bottom-left, clear of the skill row.
            UiFactory.Place(logRoot, touch ? new Vector2(0.5f, 0f) : Vector2.zero, touch ? new Vector2(-LogWidth / 2f, 40f) : new Vector2(32f, 40f),
                new Vector2(LogWidth, LogLines * LogLineHeight), Vector2.zero);
            for (int i = 0; i < skillButtons.Length; i++)
            {
                float size = touch ? TouchSkillSizes[i] : RowSkillSizes[i];
                UiFactory.Place((RectTransform)skillButtons[i].transform, new Vector2(1f, 0f),
                    touch ? TouchSkillPositions[i] : RowSkillPositions[i], new Vector2(size, size));
                skillKeys[i].text = mode == InputMode.Gamepad ? GamepadSkillKeys[i] : mode == InputMode.Keyboard ? KeyboardSkillKeys[i] : "";
            }
            bool desktop = !Application.isMobilePlatform;
            keysText.text = mode == InputMode.Gamepad ? GamepadHint : desktop || mode == InputMode.Keyboard ? KeyboardHint : "";
            descendButton.SetLabel(mode == InputMode.Gamepad ? "Descend (R3)" : mode == InputMode.Keyboard ? "Descend (Enter)" : "Descend");
            againButton.SetLabel(mode == InputMode.Gamepad ? "Try Again (A)" : mode == InputMode.Keyboard ? "Try Again (R)" : "Try Again");
            skillsButton.SetLabel(mode == InputMode.Gamepad ? "Skills (Y)" : mode == InputMode.Keyboard ? "Skills (K)" : "Skills");
            SkillTree.SetInputMode(mode);
        }

        public void AddMessage(string message, Color? color = null)
        {
            if (log.Count >= LogLines)
            {
                Destroy(log[0].Text.gameObject);
                log.RemoveAt(0);
            }
            var text = UiFactory.CreateText("Line", logRoot, message, 30, TextAnchor.LowerLeft, color ?? TextColor);
            UiFactory.Place(text.rectTransform, new Vector2(0f, 0f), Vector2.zero, new Vector2(LogWidth, 36f), new Vector2(0f, 0f));
            log.Add(new LogLine { Text = text, Born = Time.unscaledTime });
            for (int i = 0; i < log.Count; i++)
                log[i].Text.rectTransform.anchoredPosition = new Vector2(0f, (log.Count - 1 - i) * LogLineHeight);
        }

        public void ClearLog()
        {
            foreach (var line in log) Destroy(line.Text.gameObject);
            log.Clear();
        }

        /// <summary>A number or word that pops up at a world position and drifts upward (damage, heals, level up).</summary>
        public void ShowFloatingText(Vector3 worldPosition, string value, Color color, float scale = 1f)
        {
            // It scales with the view like the sprites it belongs to (HUD.md, "The view on the tablet"); the HUD doesn't.
            float withView = pixelCamera != null ? pixelCamera.WorldTextScale : 1f;
            var text = UiFactory.CreateText("Float", floatingLayer, value, FloatingTextSize(scale, withView), TextAnchor.MiddleCenter, color);
            text.fontStyle = FontStyle.Bold;
            text.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f) * Mathf.Max(0.5f, withView);
            text.rectTransform.sizeDelta = new Vector2(300f, 80f);
            floating.Add(new FloatingText { Text = text, World = worldPosition, Life = 0.8f });
            PositionFloating(floating[floating.Count - 1]);
        }

        /// <summary>The font size of a number or word over an actor: <paramref name="scale"/> is the word's own, <paramref name="withView"/> the view's (<see cref="PixelCamera.WorldTextScale"/>).</summary>
        public static int FloatingTextSize(float scale, float withView) => Mathf.RoundToInt(44 * scale * withView);

        public void ShowBanner(string title, string subtitle)
        {
            bannerTitle.text = title;
            bannerSubtitle.text = subtitle;
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine());
        }

        public IEnumerator Fade(float targetAlpha, float duration)
        {
            float start = fader.color.a;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                fader.color = new Color(0f, 0f, 0f, Mathf.Lerp(start, targetAlpha, t / duration));
                yield return null;
            }
            fader.color = new Color(0f, 0f, 0f, targetAlpha);
        }

        /// <summary>
        /// End-of-run panel: the result, what the party takes home (levels are kept win or lose), and who has points to
        /// spend in the skill tree (<paramref name="progress"/>: the heroes' saved progress).
        /// </summary>
        public void ShowRunEnd(DungeonRun run, IReadOnlyList<int> levelsAtStart, IReadOnlyList<HeroProgress> progress = null)
        {
            bool won = run.State == RunState.Won;
            bool left = run.State == RunState.Left; // The pause menu's Exit; "Return to the farm?" once there is one.
            endTitle.text = won ? "Dungeon Cleared!" : left ? "You left the dungeon." : run.Party.Count > 1 ? "The party fell..." : $"{run.Hero.Name} fainted...";
            endTitle.color = won ? new Color(1f, 0.85f, 0.3f) : left ? TextColor : new Color(1f, 0.55f, 0.5f);
            string result = !won ? $"Reached B{run.Floor}F of {run.Config.Name}."
                : run.Config.Boss != null ? $"Defeated the {run.Config.Boss.Name} and cleared {run.Config.Name} in {run.Turn} turns."
                : $"Cleared all {run.Config.FloorCount} floors of {run.Config.Name} in {run.Turn} turns.";
            var levels = new List<string>();
            for (int i = 0; i < run.Party.Count; i++)
            {
                var member = run.Party[i];
                int before = i < levelsAtStart.Count ? levelsAtStart[i] : member.Level;
                levels.Add(member.Level > before ? $"{member.Name} Lv {before} > {member.Level}" : $"{member.Name} Lv {member.Level}");
            }
            var spare = new List<string>();
            foreach (var hero in progress ?? new HeroProgress[0])
                if (hero.PointsFree > 0) spare.Add($"{hero.Definition.Name} {hero.PointsFree}");
            endDetail.text = result + "\n" + string.Join(",  ", levels) + "\nProgress saved." +
                             (spare.Count > 0 ? "\nPoints to spend:  " + string.Join(",  ", spare) : "");
            endPanel.alpha = 1f;
            endPanel.blocksRaycasts = true;
            endPanel.interactable = true;
        }

        public void HideRunEnd()
        {
            endPanel.alpha = 0f;
            endPanel.blocksRaycasts = false;
            endPanel.interactable = false;
        }

        // ---- Per frame ----

        void Update()
        {
            float now = Time.unscaledTime;
            foreach (var line in log)
            {
                float age = now - line.Born;
                var color = line.Text.color;
                color.a = age < LogLifetime ? 1f : Mathf.Clamp01(1f - (age - LogLifetime) / LogFade);
                line.Text.color = color;
            }

            for (int i = floating.Count - 1; i >= 0; i--)
            {
                var entry = floating[i];
                entry.Age += Time.deltaTime;
                if (entry.Age >= entry.Life)
                {
                    Destroy(entry.Text.gameObject);
                    floating.RemoveAt(i);
                    continue;
                }
                PositionFloating(entry);
            }
        }

        void PositionFloating(FloatingText entry)
        {
            float k = entry.Age / entry.Life;
            // Quick pop up, then a slow drift; fades over the last 40%.
            var world = entry.World + Vector3.up * (0.35f * (1f - (1f - k) * (1f - k)));
            var screen = RectTransformUtility.WorldToScreenPoint(worldCamera, world);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(floatingLayer, screen, null, out var local))
                entry.Text.rectTransform.anchoredPosition = local;
            var color = entry.Text.color;
            color.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            entry.Text.color = color;
            float pop = k < 0.15f ? Mathf.Lerp(0.6f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((k - 0.15f) / 0.2f));
            entry.Text.rectTransform.localScale = Vector3.one * pop;
        }

        IEnumerator BannerRoutine()
        {
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                banner.alpha = t / 0.3f;
                yield return null;
            }
            banner.alpha = 1f;
            yield return new WaitForSeconds(1.3f);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                banner.alpha = 1f - t / 0.5f;
                yield return null;
            }
            banner.alpha = 0f;
            bannerRoutine = null;
        }

        // ---- Layout ----

        void Build(Camera camera)
        {
            worldCamera = camera;
            pixelCamera = camera != null ? camera.GetComponent<PixelCamera>() : null;
            canvasRect = (RectTransform)transform;
            // Floating numbers sit below the controls and ignore the safe area so they line up with the world.
            floatingLayer = UiFactory.Stretch(UiFactory.CreateRect("FloatingText", canvasRect));
            safeArea = UiFactory.Stretch(UiFactory.CreateRect("SafeArea", canvasRect));
            safeArea.gameObject.AddComponent<SafeAreaFitter>();
            touchControls = UiFactory.Stretch(UiFactory.CreateRect("TouchControls", safeArea));

            BuildStatus();
            BuildBossBar();
            BuildTimeline();
            BuildLog();
            BuildControls();
            BuildOverlays();
            HideRunEnd();
            HideBoss();
            SetInputMode(InputMode.Touch);
        }

        /// <summary>Top of the screen: the party's cards on the left, the floor in the middle, key hints below the cards.</summary>
        void BuildStatus()
        {
            party = PartyPanel.Create(safeArea, new Vector2(28f, -24f));
            party.MemberTapped += index => CommandRequested?.Invoke(HeroCommand.SwitchLeader(index));
            party.TacticTapped += index => TacticCycleRequested?.Invoke(index);

            var floorPanel = UiFactory.CreatePanel("Floor", safeArea, "frame", PanelColor);
            UiFactory.Place(floorPanel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(200f, 72f), new Vector2(0.5f, 1f));
            floorText = UiFactory.CreateText("FloorText", floorPanel.transform, "", 40, TextAnchor.MiddleCenter, TextColor);
            UiFactory.Stretch(floorText.rectTransform);

            keysText = UiFactory.CreateText("Keys", safeArea, "", 20, TextAnchor.UpperLeft, HintColor);
            UiFactory.Place(keysText.rectTransform, new Vector2(0f, 1f), new Vector2(32f, KeysTop), new Vector2(1000f, 50f), new Vector2(0f, 1f));
        }

        void BuildBossBar()
        {
            var panel = UiFactory.CreatePanel("Boss", safeArea, "frame", PanelColor);
            UiFactory.Place(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(720f, 88f), new Vector2(0.5f, 1f));
            bossPanel = panel.gameObject;
            bossName = UiFactory.CreateText("Name", panel.transform, "", 30, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.75f));
            UiFactory.Place(bossName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(680f, 36f), new Vector2(0.5f, 1f));
            bossFill = UiFactory.CreateBar(panel.transform, "BossHp", new Vector2(20f, -46f), new Vector2(680f, 28f), BossBarColor);
        }

        /// <summary>Vertical turn-order strip under the status panel, clear of the D-pad; filled by RefreshTimeline.</summary>
        void BuildTimeline()
        {
            timelineRoot = UiFactory.CreateRect("Timeline", safeArea);
            UiFactory.Place(timelineRoot, new Vector2(0f, 1f), new Vector2(32f, KeysTop - 52f), new Vector2(170f, 300f), new Vector2(0f, 1f));
            timelineHeader = UiFactory.CreateText("Header", timelineRoot, "Turn order", 22, TextAnchor.UpperLeft, HintColor);
            UiFactory.Place(timelineHeader.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(170f, 26f), new Vector2(0f, 1f));
            timelineRoot.gameObject.SetActive(false);
        }

        void BuildLog()
        {
            logRoot = UiFactory.CreateRect("Log", safeArea); // Placed by SetInputMode.
        }

        void BuildControls()
        {
            var bottomLeft = new Vector2(0f, 0f);
            var bottomRight = new Vector2(1f, 0f);
            var topRight = new Vector2(1f, 1f);

            DPad = DPad.Create(touchControls, bottomLeft, new Vector2(270f, 270f), 420f);
            DPad.DisabledPressed += ShowAutoPilotBlocked;

            attackButton = HoldButton.Create(touchControls, "Attack", "ATK", bottomRight, new Vector2(-230f, 230f), new Vector2(230f, 230f), AttackArt, 40);
            attackButton.Pressed += () => CommandRequested?.Invoke(HeroCommand.Attack);
            attackButton.DisabledPressed += ShowAutoPilotBlocked;

            // The three skills and the ultimate. Outside the touch-only group: with keys or a controller they stay, in a
            // row, because they show cooldowns and the ultimate's charge.
            for (int i = 0; i < skillButtons.Length; i++)
            {
                bool ultimate = i == 3;
                float size = TouchSkillSizes[i];
                var button = HoldButton.Create(safeArea, ultimate ? "Ultimate" : $"Skill {i + 1}", ultimate ? "ULT" : "-", bottomRight,
                    TouchSkillPositions[i], new Vector2(size, size), ultimate ? UltimateArt : SkillArt, ultimate ? 32 : 30);
                var key = UiFactory.CreateText("Key", button.transform, "", 24, TextAnchor.LowerCenter, TextColor);
                UiFactory.Place(key.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 4f), new Vector2(90f, 30f), new Vector2(0.5f, 0f));
                skillButtons[i] = button;
                skillKeys[i] = key;
                // The controller turns a press into aiming, or explains why the action can't be used.
                var command = ultimate ? HeroCommand.UltimateFacing : HeroCommand.Skill(i);
                button.Pressed += () => CommandRequested?.Invoke(command);
                button.DisabledPressed += () => CommandRequested?.Invoke(command);
                if (ultimate) continue;

                // A small "Quick" tag for skills that take half a turn (never D&D terms like "bonus action").
                var tag = UiFactory.CreatePanel("Quick", button.transform, "frame", QuickTagColor);
                UiFactory.Place(tag.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -6f), new Vector2(80f, 32f), new Vector2(0.5f, 0.5f));
                var tagText = UiFactory.CreateText("Text", tag.transform, "Quick", 18, TextAnchor.MiddleCenter, new Color(0.05f, 0.12f, 0.08f));
                UiFactory.Stretch(tagText.rectTransform);
                tag.gameObject.SetActive(false);
                quickTags[i] = tag.gameObject;
            }

            aimPrompt = UiFactory.CreateText("AimPrompt", safeArea, "", 30, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.6f));
            UiFactory.Place(aimPrompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 380f), new Vector2(900f, 44f));
            aimPrompt.gameObject.SetActive(false);

            // One row along the top edge: icon buttons, so it stays short enough for the minimap under it.
            Vector2 TopButton(int fromRight) =>
                new Vector2(-28f - TopButtonSize.x / 2f - fromRight * (TopButtonSize.x + TopButtonGap), -24f - TopButtonSize.y / 2f);

            pauseButton = HoldButton.Create(safeArea, "Pause", "", topRight, TopButton(0), TopButtonSize, ActionArt, 30);
            pauseButton.SetIcon("icon_pause");
            pauseButton.Pressed += () => PauseRequested?.Invoke();

            autoButton = HoldButton.Create(safeArea, "Auto", "AUTO", topRight, TopButton(1), TopButtonSize, ActionArt, 30);
            autoButton.Pressed += () => AutoPilotToggled?.Invoke();

            berryButton = HoldButton.Create(safeArea, "Berry", "x0", topRight, TopButton(2), TopButtonSize, ActionArt, 32);
            berryButton.SetIcon("icon_berry", offset: -24f, labelShift: 26f);
            berryButton.Pressed += () => CommandRequested?.Invoke(HeroCommand.UseBerry);
            berryButton.DisabledPressed += () =>
            {
                if (autoPilotOn) ShowAutoPilotBlocked();
                else AddMessage("You have no berries.", HintColor);
            };

            waitButton = HoldButton.Create(safeArea, "Wait", "", topRight, TopButton(3), TopButtonSize, ActionArt, 32);
            waitButton.SetIcon("icon_wait");
            waitButton.Pressed += () => CommandRequested?.Invoke(HeroCommand.Wait);
            waitButton.DisabledPressed += ShowAutoPilotBlocked;

            // Under the row, see-through, and never in the way of a touch.
            minimap = Minimap.Create(safeArea, new Vector2(-28f, -24f - TopButtonSize.y - 12f));

            descendButton = HoldButton.Create(safeArea, "Descend", "Descend", new Vector2(0.5f, 0f), new Vector2(0f, 260f), new Vector2(330f, 96f), ActiveArt, 36);
            descendButton.Pressed += () => CommandRequested?.Invoke(HeroCommand.Descend);
            descendButton.DisabledPressed += ShowAutoPilotBlocked;
            descendButton.gameObject.SetActive(false);
        }

        void BuildOverlays()
        {
            fader = UiFactory.CreateImage("Fader", canvasRect, null, new Color(0f, 0f, 0f, 0f));
            UiFactory.Stretch(fader.rectTransform);

            var bannerRect = UiFactory.CreateRect("Banner", canvasRect);
            UiFactory.Place(bannerRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(900f, 200f));
            banner = bannerRect.gameObject.AddComponent<CanvasGroup>();
            banner.alpha = 0f;
            banner.blocksRaycasts = false;
            // The dungeon's name on one of the kit's ribbons, the floor under it.
            var ribbon = UiFactory.CreatePanel("Ribbon", bannerRect, "ribbon_blue");
            UiFactory.Place(ribbon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(920f, 206f));
            bannerTitle = UiFactory.CreateText("Title", bannerRect, "", 68, TextAnchor.MiddleCenter, TextColor);
            UiFactory.Place(bannerTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 48f), new Vector2(900f, 90f));
            bannerSubtitle = UiFactory.CreateText("Subtitle", bannerRect, "", 52, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f));
            UiFactory.Place(bannerSubtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(900f, 70f));

            PauseMenu = PauseMenu.Create(canvasRect);

            var end = UiFactory.CreatePanel("RunEnd", canvasRect, "panel", raycast: true);
            UiFactory.Place(end.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 520f));
            endPanel = end.gameObject.AddComponent<CanvasGroup>();
            endTitle = UiFactory.CreateText("Title", end.transform, "", 64, TextAnchor.MiddleCenter, TextColor);
            UiFactory.Place(endTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(820f, 90f));
            endDetail = UiFactory.CreateText("Detail", end.transform, "", 30, TextAnchor.MiddleCenter, TextColor);
            UiFactory.Place(endDetail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(820f, 170f));
            againButton = HoldButton.Create(end.transform, "TryAgain", "Try Again", new Vector2(0.5f, 0f), new Vector2(-195f, 95f), new Vector2(360f, 100f), "button_red", 38);
            againButton.Pressed += () => RestartRequested?.Invoke();
            // A build changes between runs: here is where the skill tree opens for that.
            skillsButton = HoldButton.Create(end.transform, "Skills", "Skills", new Vector2(0.5f, 0f), new Vector2(195f, 95f), new Vector2(360f, 100f), "button_gold", 38);
            skillsButton.Pressed += () => SkillTreeRequested?.Invoke();

            SkillTree = SkillTreeScreen.Create(canvasRect);
        }
    }
}

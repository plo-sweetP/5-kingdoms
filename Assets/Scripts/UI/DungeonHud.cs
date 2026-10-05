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
    /// Landscape HUD for the dungeon, built in code. The party's HP, ultimate charge, EXP and levels and the floor across
    /// the top, plus a boss bar on boss floors; D-pad bottom-left; the weapon attack, the three skills and the ultimate
    /// bottom-right (each starts aiming: pick it, then the target); Wait and Berry top-right; a message log; an aiming
    /// prompt; floating numbers; fades, floor banner and end-of-run panel. The D-pad and attack button hide while a keyboard or controller is in use (PC, Steam Deck); the
    /// skill buttons stay, in a row with their keys, since they also show cooldowns, Quick tags and the charge.
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
                                    "Wait: X   Berry: B   Go down: Enter   Auto: T   Aim: arrows pick, Space fires, Esc cancels (or click the enemy)";
        const string GamepadHint = "Move: stick/D-pad   Attack: A, then a target   Skills: LB LT RT   Ultimate: RB   Switch hero: B   Tactics: L3\n" +
                                   "Wait: Y   Berry: X   Go down: Start   Auto: View   Aim: stick picks, A fires, B cancels";
        static readonly string[] KeyboardSkillKeys = { "1", "2", "3", "4" };
        static readonly string[] GamepadSkillKeys = { "LB", "LT", "RT", "RB" };

        // Skill buttons (three skills, then the ultimate): around the attack button for thumbs, or in a row for keys.
        static readonly Vector2[] TouchSkillPositions = { new Vector2(-470f, 120f), new Vector2(-455f, 335f), new Vector2(-330f, 485f), new Vector2(-135f, 460f) };
        static readonly float[] TouchSkillSizes = { 140f, 140f, 140f, 150f };
        static readonly Vector2[] RowSkillPositions = { new Vector2(-560f, 100f), new Vector2(-420f, 100f), new Vector2(-280f, 100f), new Vector2(-125f, 108f) };
        static readonly float[] RowSkillSizes = { 124f, 124f, 124f, 140f };

        const int LogLines = 4;
        const float LogLifetime = 6f;
        const float LogFade = 1f;

        public event Action<HeroCommand> CommandRequested;
        public event Action RestartRequested;
        public event Action AutoPilotToggled;

        /// <summary>A partner's tactic badge was tapped (party index).</summary>
        public event Action<int> TacticCycleRequested;

        public DPad DPad { get; private set; }

        readonly List<LogLine> log = new List<LogLine>();
        readonly List<FloatingText> floating = new List<FloatingText>();
        readonly List<GameObject> timelineRows = new List<GameObject>();
        RectTransform timelineRoot;
        Text timelineHeader;
        Camera worldCamera;
        RectTransform canvasRect, safeArea, floatingLayer, logRoot, touchControls;
        Text floorText, bossName, keysText, bannerTitle, bannerSubtitle, endTitle, endDetail, aimPrompt;
        Image bossFill;
        PartyPanel party;
        GameObject bossPanel;
        int bossMaxHp;
        HoldButton berryButton, descendButton, againButton, autoButton, attackButton, waitButton;
        readonly HoldButton[] skillButtons = new HoldButton[4]; // Three skills, then the ultimate.
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
            berryButton.SetLabel($"Berry x{run.Berries}");
            hasBerries = run.Berries > 0;
            berryButton.Interactable = hasBerries && !autoPilotOn;
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
        /// still answers a press, so the log can say why.
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
                    button.Interactable = false;
                    quickTags[i].SetActive(false);
                    continue;
                }
                var skill = skills[i];
                bool cooling = hero.SkillCooldowns[i] > 0;
                button.SetLabel(cooling ? $"{skill.ShortName}\n<size=22>next turn</size>" : skill.ShortName);
                quickTags[i].SetActive(skill.IsQuick);
                var check = run.CheckSkill(i);
                // Blocked only means "not the way the hero faces": aiming can still find room.
                button.Interactable = playing && (check == SkillCheck.Ready || check == SkillCheck.Blocked);
            }

            var ultimate = hero.Ultimate;
            var ult = skillButtons[3];
            if (ultimate == null)
            {
                ult.SetLabel("ULT");
                ult.Interactable = false;
            }
            else
            {
                bool ready = hero.UltimateReady;
                ult.SetLabel(ready ? $"{ultimate.ShortName}\n<size=22>READY</size>" : $"ULT\n<size=22>{hero.Charge}%</size>");
                ult.SetArt(ready ? UltimateReadyArt : UltimateArt);
                ult.Interactable = playing && ready;
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
        /// Shows whether the auto-pilot is playing (gold while on). While it plays, movement and the normal action
        /// buttons are dimmed; skills and the ultimate are not affected.
        /// </summary>
        public void SetAutoPilot(bool on)
        {
            autoPilotOn = on;
            autoButton.SetLabel(on ? "Auto: On" : "Auto: Off");
            autoButton.SetArt(on ? ActiveArt : ActionArt);
            DPad.Interactable = !on;
            attackButton.Interactable = !on;
            waitButton.Interactable = !on;
            descendButton.Interactable = !on;
            berryButton.Interactable = !on && hasBerries;
        }

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
            UiFactory.Place(logRoot, touch ? new Vector2(0.5f, 0f) : Vector2.zero, touch ? new Vector2(-330f, 40f) : new Vector2(32f, 40f),
                new Vector2(660f, 160f), Vector2.zero);
            for (int i = 0; i < skillButtons.Length; i++)
            {
                float size = touch ? TouchSkillSizes[i] : RowSkillSizes[i];
                UiFactory.Place((RectTransform)skillButtons[i].transform, new Vector2(1f, 0f),
                    touch ? TouchSkillPositions[i] : RowSkillPositions[i], new Vector2(size, size));
                skillKeys[i].text = mode == InputMode.Gamepad ? GamepadSkillKeys[i] : mode == InputMode.Keyboard ? KeyboardSkillKeys[i] : "";
            }
            bool desktop = !Application.isMobilePlatform;
            keysText.text = mode == InputMode.Gamepad ? GamepadHint : desktop || mode == InputMode.Keyboard ? KeyboardHint : "";
            descendButton.SetLabel(mode == InputMode.Gamepad ? "Descend (RB)" : mode == InputMode.Keyboard ? "Descend (Enter)" : "Descend");
            againButton.SetLabel(mode == InputMode.Gamepad ? "Try Again (A)" : mode == InputMode.Keyboard ? "Try Again (R)" : "Try Again");
        }

        public void AddMessage(string message, Color? color = null)
        {
            if (log.Count >= LogLines)
            {
                Destroy(log[0].Text.gameObject);
                log.RemoveAt(0);
            }
            var text = UiFactory.CreateText("Line", logRoot, message, 30, TextAnchor.LowerLeft, color ?? TextColor);
            UiFactory.Place(text.rectTransform, new Vector2(0f, 0f), Vector2.zero, new Vector2(660f, 36f), new Vector2(0f, 0f));
            log.Add(new LogLine { Text = text, Born = Time.unscaledTime });
            for (int i = 0; i < log.Count; i++)
                log[i].Text.rectTransform.anchoredPosition = new Vector2(0f, (log.Count - 1 - i) * 38f);
        }

        public void ClearLog()
        {
            foreach (var line in log) Destroy(line.Text.gameObject);
            log.Clear();
        }

        /// <summary>A number or word that pops up at a world position and drifts upward (damage, heals, level up).</summary>
        public void ShowFloatingText(Vector3 worldPosition, string value, Color color, float scale = 1f)
        {
            var text = UiFactory.CreateText("Float", floatingLayer, value, Mathf.RoundToInt(44 * scale), TextAnchor.MiddleCenter, color);
            text.fontStyle = FontStyle.Bold;
            text.rectTransform.sizeDelta = new Vector2(300f, 80f);
            floating.Add(new FloatingText { Text = text, World = worldPosition, Life = 0.8f });
            PositionFloating(floating[floating.Count - 1]);
        }

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

        /// <summary>End-of-run panel: the result, and what the party takes home (levels are kept win or lose).</summary>
        public void ShowRunEnd(DungeonRun run, IReadOnlyList<int> levelsAtStart)
        {
            bool won = run.State == RunState.Won;
            endTitle.text = won ? "Dungeon Cleared!" : run.Party.Count > 1 ? "The party fell..." : $"{run.Hero.Name} fainted...";
            endTitle.color = won ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.55f, 0.5f);
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
            endDetail.text = result + "\n" + string.Join(",  ", levels) + "\nProgress saved.";
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

            berryButton = HoldButton.Create(safeArea, "Berry", "Berry x0", topRight, new Vector2(-140f, -64f), new Vector2(230f, 84f), ActionArt, 32);
            berryButton.Pressed += () => CommandRequested?.Invoke(HeroCommand.UseBerry);
            berryButton.DisabledPressed += () =>
            {
                if (autoPilotOn) ShowAutoPilotBlocked();
                else AddMessage("You have no berries.", HintColor);
            };

            waitButton = HoldButton.Create(safeArea, "Wait", "Wait", topRight, new Vector2(-390f, -64f), new Vector2(230f, 84f), ActionArt, 32);
            waitButton.Pressed += () => CommandRequested?.Invoke(HeroCommand.Wait);
            waitButton.DisabledPressed += ShowAutoPilotBlocked;

            autoButton = HoldButton.Create(safeArea, "Auto", "Auto: Off", topRight, new Vector2(-140f, -158f), new Vector2(230f, 76f), ActionArt, 30);
            autoButton.Pressed += () => AutoPilotToggled?.Invoke();

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

            var end = UiFactory.CreatePanel("RunEnd", canvasRect, "panel", raycast: true);
            UiFactory.Place(end.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 480f));
            endPanel = end.gameObject.AddComponent<CanvasGroup>();
            endTitle = UiFactory.CreateText("Title", end.transform, "", 64, TextAnchor.MiddleCenter, TextColor);
            UiFactory.Place(endTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(820f, 90f));
            endDetail = UiFactory.CreateText("Detail", end.transform, "", 30, TextAnchor.MiddleCenter, TextColor);
            UiFactory.Place(endDetail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(820f, 140f));
            againButton = HoldButton.Create(end.transform, "TryAgain", "Try Again", new Vector2(0.5f, 0f), new Vector2(0f, 95f), new Vector2(360f, 100f), "button_red", 38);
            againButton.Pressed += () => RestartRequested?.Invoke();
        }
    }
}

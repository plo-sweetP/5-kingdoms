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
    /// Landscape HUD for the dungeon, built in code. HP, MP, EXP, level and floor across the top, plus a boss bar on
    /// boss floors; D-pad bottom-left; attack plus the three skills and the ultimate (locked for now) bottom-right;
    /// Wait and Berry top-right; a message log; floating numbers; fades, floor banner and end-of-run panel. The D-pad
    /// and attack button hide while a keyboard or controller is in use (PC, Steam Deck); the skill buttons stay, in a
    /// row with their keys, since they also show mana costs and cooldowns.
    /// Layout is in 1920x1080 reference pixels, scaled to the screen height and kept inside the safe area.
    /// </summary>
    public sealed class DungeonHud : MonoBehaviour
    {
        public static readonly Color TextColor = new Color(0.95f, 0.95f, 0.97f);
        public static readonly Color HintColor = new Color(0.75f, 0.75f, 0.82f);
        static readonly Color PanelColor = new Color(0.06f, 0.05f, 0.1f, 0.72f);
        static readonly Color AttackColor = new Color(0.82f, 0.25f, 0.2f, 0.92f);
        static readonly Color ActionColor = new Color(0.18f, 0.2f, 0.3f, 0.88f);
        static readonly Color SkillColor = new Color(0.24f, 0.4f, 0.78f, 0.92f);
        static readonly Color UltimateColor = new Color(0.86f, 0.64f, 0.14f, 0.92f);
        static readonly Color DescendColor = new Color(0.86f, 0.64f, 0.14f, 0.95f);
        static readonly Color MpColor = new Color(0.32f, 0.6f, 1f);
        static readonly Color ExpColor = new Color(0.78f, 0.62f, 1f);
        static readonly Color BossBarColor = new Color(0.72f, 0.38f, 0.95f);
        static readonly Color HeroTurnColor = new Color(0.2f, 0.42f, 0.8f, 0.9f);
        static readonly Color EnemyTurnColor = new Color(0.55f, 0.18f, 0.18f, 0.9f);
        static readonly Color BossTurnColor = new Color(0.45f, 0.22f, 0.7f, 0.95f);
        static readonly Color SlamColor = new Color(1f, 0.35f, 0.3f);

        const int TimelineTurnsShown = 5;
        const float TimelineIcon = 50f;
        const float TimelineGap = 6f;

        const string KeyboardHint = "Move: WASD/arrows + QEZC   Attack: Space   Skills: 1 2 3 (hold a direction to aim)   Wait: X   Berry: B   Stairs: Enter   Auto: T";
        const string GamepadHint = "Move: stick/D-pad   Attack: A   Skills: LB LT RT (hold a direction to aim)   Wait: Y   Berry: X   Stairs: RB   Auto: View";
        static readonly string[] KeyboardSkillKeys = { "1", "2", "3", "4" };
        static readonly string[] GamepadSkillKeys = { "LB", "LT", "RT", "" };

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

        public DPad DPad { get; private set; }

        readonly List<LogLine> log = new List<LogLine>();
        readonly List<FloatingText> floating = new List<FloatingText>();
        readonly List<GameObject> timelineRows = new List<GameObject>();
        RectTransform timelineRoot;
        Text timelineHeader;
        Camera worldCamera;
        RectTransform canvasRect, safeArea, floatingLayer, logRoot, touchControls;
        Text heroText, hpText, mpText, expText, floorText, bossName, keysText, bannerTitle, bannerSubtitle, endTitle, endDetail;
        Image hpFill, mpFill, expFill, bossFill;
        GameObject bossPanel;
        int bossMaxHp;
        HoldButton berryButton, descendButton, againButton, autoButton, attackButton, waitButton;
        readonly HoldButton[] skillButtons = new HoldButton[4]; // Three skills, then the ultimate.
        readonly Text[] skillKeys = new Text[4];
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

            var hud = go.AddComponent<DungeonHud>();
            hud.Build(worldCamera);
            return hud;
        }

        // ---- Public API used by the controller and the view ----

        public void Refresh(DungeonRun run)
        {
            var hero = run.Hero;
            heroText.text = $"{hero.Name}   Lv {hero.Level}";
            SetHeroHp(hero.Hp, hero.MaxHp);
            SetHeroMp(hero.Mp, hero.MaxMp);
            RefreshSkills(run);
            int toNext = CombatRules.ExpToNextLevel(hero.Level);
            expFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(hero.Exp / (float)toNext), 1f);
            expText.text = $"{hero.Exp}/{toNext} EXP";
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
            float y = -30f;
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
                    UiFactory.Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(170f, 18f), new Vector2(0f, 1f));
                    timelineRows.Add(divider.gameObject);
                    y -= 20f;
                }
                timelineRows.Add(CreateTimelineRow(turn, run, isCurrent: i == 0, slam, y));
                y -= TimelineIcon + TimelineGap;
            }
        }

        GameObject CreateTimelineRow(TimelineTurn turn, DungeonRun run, bool isCurrent, bool slam, float y)
        {
            var actor = turn.Actor;
            var color = actor.Team == Team.Hero ? HeroTurnColor : actor.Definition.IsBoss ? BossTurnColor : EnemyTurnColor;
            var frame = UiFactory.CreateImage("Turn", timelineRoot, UiFactory.RoundedRect, slam ? SlamColor : color);
            UiFactory.Place(frame.rectTransform, new Vector2(0f, 1f), new Vector2(isCurrent ? 0f : 8f, y), new Vector2(TimelineIcon, TimelineIcon), new Vector2(0f, 1f));
            var icon = UiFactory.CreateImage("Icon", frame.transform, SpriteLibrary.Get("Characters/" + actor.Definition.Id, Color.gray), Color.white);
            icon.preserveAspect = true;
            UiFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TimelineIcon - 6f, TimelineIcon - 6f));

            int avFromNow = Mathf.RoundToInt((float)(turn.Time - run.CombatTime).ToDouble());
            string label = slam ? "SLAM!" : isCurrent ? "Now" : $"+{avFromNow}";
            var text = UiFactory.CreateText("When", frame.transform, label, 22, TextAnchor.MiddleLeft, slam ? SlamColor : isCurrent ? TextColor : HintColor);
            UiFactory.Place(text.rectTransform, new Vector2(1f, 0.5f), new Vector2(8f, 0f), new Vector2(90f, 30f), new Vector2(0f, 0.5f));
            return frame.gameObject;
        }

        public void SetHeroHp(int hp, int maxHp)
        {
            float ratio = maxHp > 0 ? Mathf.Clamp01(hp / (float)maxHp) : 0f;
            hpFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
            hpFill.color = ratio > 0.5f ? new Color(0.36f, 0.86f, 0.42f) : ratio > 0.25f ? new Color(0.95f, 0.78f, 0.25f) : new Color(0.92f, 0.3f, 0.26f);
            hpText.text = $"{hp}/{maxHp}";
        }

        public void SetHeroMp(int mp, int maxMp)
        {
            mpFill.rectTransform.anchorMax = new Vector2(maxMp > 0 ? Mathf.Clamp01(mp / (float)maxMp) : 0f, 1f);
            mpText.text = $"{mp}/{maxMp} MP";
        }

        /// <summary>
        /// Each skill button shows its name and what it costs (MP), builds (+MP) or how many turns it still needs to
        /// recharge. A skill that can't be used right now is dimmed but still answers a press, so the log can say why.
        /// </summary>
        void RefreshSkills(DungeonRun run)
        {
            var hero = run.Hero;
            var skills = hero.Definition.Skills;
            for (int i = 0; i < 3; i++)
            {
                var button = skillButtons[i];
                if (i >= skills.Count)
                {
                    button.SetLabel("-");
                    button.Interactable = false;
                    continue;
                }
                var skill = skills[i];
                int cooldown = hero.SkillCooldowns[i];
                string detail = cooldown > 0 ? $"{cooldown} turn{(cooldown == 1 ? "" : "s")}"
                    : skill.ManaCost > 0 ? $"{skill.ManaCost} MP"
                    : skill.ManaGain > 0 ? $"+{skill.ManaGain} MP"
                    : "Free";
                button.SetLabel($"{skill.ShortName}\n<size=22>{detail}</size>");
                var check = run.CheckSkill(i);
                // Blocked only means "not the way the hero faces": aiming (hold a direction) can still find room.
                button.Interactable = run.State == RunState.InProgress && (check == SkillCheck.Ready || check == SkillCheck.Blocked);
            }
        }

        public void ShowUltimateLocked() => AddMessage("Ultimates unlock in a later milestone.", HintColor);

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
            autoButton.SetColor(on ? UltimateColor : ActionColor);
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

        /// <summary>End-of-run panel: the result, and what the hero takes home (levels are kept win or lose).</summary>
        public void ShowRunEnd(DungeonRun run, int levelAtStart)
        {
            bool won = run.State == RunState.Won;
            var hero = run.Hero;
            endTitle.text = won ? "Dungeon Cleared!" : $"{hero.Name} fainted...";
            endTitle.color = won ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.55f, 0.5f);
            string result = !won ? $"Reached B{run.Floor}F of {run.Config.Name}."
                : run.Config.Boss != null ? $"Defeated the {run.Config.Boss.Name} and cleared {run.Config.Name} in {run.Turn} turns."
                : $"Cleared all {run.Config.FloorCount} floors of {run.Config.Name} in {run.Turn} turns.";
            int gained = hero.Level - levelAtStart;
            string progress = gained > 0
                ? $"{hero.Name} grew from Lv {levelAtStart} to Lv {hero.Level}. Progress saved."
                : $"{hero.Name} is Lv {hero.Level} with {hero.Exp} EXP. Progress saved.";
            endDetail.text = result + "\n" + progress;
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

        void BuildStatus()
        {
            var panel = UiFactory.CreateImage("Status", safeArea, UiFactory.RoundedRect, PanelColor);
            UiFactory.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(540f, 164f), new Vector2(0f, 1f));
            heroText = UiFactory.CreateText("Hero", panel.transform, "", 36, TextAnchor.UpperLeft, TextColor);
            UiFactory.Place(heroText.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -10f), new Vector2(490f, 44f), new Vector2(0f, 1f));

            hpFill = CreateBar(panel.transform, "Hp", new Vector2(24f, -58f), new Vector2(330f, 32f), Color.green);
            hpText = UiFactory.CreateText("HpText", panel.transform, "", 30, TextAnchor.MiddleLeft, TextColor);
            UiFactory.Place(hpText.rectTransform, new Vector2(0f, 1f), new Vector2(370f, -74f), new Vector2(160f, 40f), new Vector2(0f, 0.5f));

            mpFill = CreateBar(panel.transform, "Mp", new Vector2(24f, -98f), new Vector2(330f, 22f), MpColor);
            mpText = UiFactory.CreateText("MpText", panel.transform, "", 24, TextAnchor.MiddleLeft, new Color(0.7f, 0.82f, 1f));
            UiFactory.Place(mpText.rectTransform, new Vector2(0f, 1f), new Vector2(370f, -109f), new Vector2(170f, 32f), new Vector2(0f, 0.5f));

            expFill = CreateBar(panel.transform, "Exp", new Vector2(24f, -130f), new Vector2(330f, 14f), ExpColor);
            expText = UiFactory.CreateText("ExpText", panel.transform, "", 22, TextAnchor.MiddleLeft, HintColor);
            UiFactory.Place(expText.rectTransform, new Vector2(0f, 1f), new Vector2(370f, -137f), new Vector2(170f, 30f), new Vector2(0f, 0.5f));

            var floorPanel = UiFactory.CreateImage("Floor", safeArea, UiFactory.RoundedRect, PanelColor);
            UiFactory.Place(floorPanel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(200f, 72f), new Vector2(0.5f, 1f));
            floorText = UiFactory.CreateText("FloorText", floorPanel.transform, "", 40, TextAnchor.MiddleCenter, TextColor);
            UiFactory.Stretch(floorText.rectTransform);

            keysText = UiFactory.CreateText("Keys", safeArea, "", 22, TextAnchor.UpperLeft, HintColor);
            UiFactory.Place(keysText.rectTransform, new Vector2(0f, 1f), new Vector2(32f, -200f), new Vector2(1000f, 30f), new Vector2(0f, 1f));
        }

        void BuildBossBar()
        {
            var panel = UiFactory.CreateImage("Boss", safeArea, UiFactory.RoundedRect, PanelColor);
            UiFactory.Place(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(720f, 84f), new Vector2(0.5f, 1f));
            bossPanel = panel.gameObject;
            bossName = UiFactory.CreateText("Name", panel.transform, "", 30, TextAnchor.UpperCenter, new Color(0.9f, 0.75f, 1f));
            UiFactory.Place(bossName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(680f, 36f), new Vector2(0.5f, 1f));
            bossFill = CreateBar(panel.transform, "BossHp", new Vector2(20f, -46f), new Vector2(680f, 26f), BossBarColor);
        }

        /// <summary>Vertical turn-order strip under the status panel, clear of the D-pad; filled by RefreshTimeline.</summary>
        void BuildTimeline()
        {
            timelineRoot = UiFactory.CreateRect("Timeline", safeArea);
            UiFactory.Place(timelineRoot, new Vector2(0f, 1f), new Vector2(32f, -238f), new Vector2(170f, 360f), new Vector2(0f, 1f));
            timelineHeader = UiFactory.CreateText("Header", timelineRoot, "Turn order", 22, TextAnchor.UpperLeft, HintColor);
            UiFactory.Place(timelineHeader.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(170f, 26f), new Vector2(0f, 1f));
            timelineRoot.gameObject.SetActive(false);
        }

        /// <summary>A rounded bar (dark back, colored fill) whose fill width is set through its anchorMax.x.</summary>
        static Image CreateBar(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var back = UiFactory.CreateImage(name + "Back", parent, UiFactory.RoundedRect, new Color(0f, 0f, 0f, 0.65f));
            UiFactory.Place(back.rectTransform, new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
            var fill = UiFactory.CreateImage(name + "Fill", back.transform, UiFactory.RoundedRect, color);
            var rect = fill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            float inset = Mathf.Min(4f, size.y / 4f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return fill;
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

            attackButton = HoldButton.Create(touchControls, "Attack", "ATK", bottomRight, new Vector2(-230f, 230f), new Vector2(230f, 230f), AttackColor, 48, round: true);
            attackButton.Pressed += () => CommandRequested?.Invoke(HeroCommand.Attack);
            attackButton.DisabledPressed += ShowAutoPilotBlocked;

            // The three skills and the ultimate (locked until ultimates are designed). Outside the touch-only group:
            // with keys or a controller they stay, in a row, because they show costs and cooldowns.
            for (int i = 0; i < skillButtons.Length; i++)
            {
                bool ultimate = i == 3;
                float size = TouchSkillSizes[i];
                var button = HoldButton.Create(safeArea, ultimate ? "Ultimate" : $"Skill {i + 1}", ultimate ? "ULT" : "-", bottomRight,
                    TouchSkillPositions[i], new Vector2(size, size), ultimate ? UltimateColor : SkillColor, ultimate ? 34 : 30, round: true);
                var key = UiFactory.CreateText("Key", button.transform, "", 24, TextAnchor.LowerCenter, TextColor);
                UiFactory.Place(key.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 4f), new Vector2(90f, 30f), new Vector2(0.5f, 0f));
                skillButtons[i] = button;
                skillKeys[i] = key;
                if (ultimate)
                {
                    button.Interactable = false;
                    button.DisabledPressed += ShowUltimateLocked;
                    continue;
                }
                int slot = i;
                button.Pressed += () => CommandRequested?.Invoke(HeroCommand.Skill(slot));
                button.DisabledPressed += () => CommandRequested?.Invoke(HeroCommand.Skill(slot)); // The controller explains why not.
            }

            berryButton = HoldButton.Create(safeArea, "Berry", "Berry x0", topRight, new Vector2(-140f, -64f), new Vector2(230f, 84f), ActionColor, 32, round: false);
            berryButton.Pressed += () => CommandRequested?.Invoke(HeroCommand.UseBerry);
            berryButton.DisabledPressed += () =>
            {
                if (autoPilotOn) ShowAutoPilotBlocked();
                else AddMessage("You have no berries.", HintColor);
            };

            waitButton = HoldButton.Create(safeArea, "Wait", "Wait", topRight, new Vector2(-390f, -64f), new Vector2(230f, 84f), ActionColor, 32, round: false);
            waitButton.Pressed += () => CommandRequested?.Invoke(HeroCommand.Wait);
            waitButton.DisabledPressed += ShowAutoPilotBlocked;

            autoButton = HoldButton.Create(safeArea, "Auto", "Auto: Off", topRight, new Vector2(-140f, -158f), new Vector2(230f, 76f), ActionColor, 30, round: false);
            autoButton.Pressed += () => AutoPilotToggled?.Invoke();

            descendButton = HoldButton.Create(safeArea, "Descend", "Descend", new Vector2(0.5f, 0f), new Vector2(0f, 260f), new Vector2(330f, 96f), DescendColor, 36, round: false);
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
            bannerTitle = UiFactory.CreateText("Title", bannerRect, "", 72, TextAnchor.MiddleCenter, TextColor);
            UiFactory.Place(bannerTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(900f, 90f));
            bannerSubtitle = UiFactory.CreateText("Subtitle", bannerRect, "", 52, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f));
            UiFactory.Place(bannerSubtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -45f), new Vector2(900f, 70f));

            var end = UiFactory.CreateImage("RunEnd", canvasRect, UiFactory.RoundedRect, new Color(0.05f, 0.04f, 0.09f, 0.94f), raycast: true);
            UiFactory.Place(end.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 460f));
            endPanel = end.gameObject.AddComponent<CanvasGroup>();
            endTitle = UiFactory.CreateText("Title", end.transform, "", 64, TextAnchor.MiddleCenter, TextColor);
            UiFactory.Place(endTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(820f, 90f));
            endDetail = UiFactory.CreateText("Detail", end.transform, "", 30, TextAnchor.MiddleCenter, HintColor);
            UiFactory.Place(endDetail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(820f, 100f));
            againButton = HoldButton.Create(end.transform, "TryAgain", "Try Again", new Vector2(0.5f, 0f), new Vector2(0f, 85f), new Vector2(360f, 100f), AttackColor, 38, round: false);
            againButton.Pressed += () => RestartRequested?.Invoke();
        }
    }
}

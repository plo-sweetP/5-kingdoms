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
    /// <summary>
    /// Landscape HUD for the dungeon, built in code. HP, level and floor across the top; D-pad bottom-left;
    /// attack plus three skill slots and an ultimate bottom-right (locked until the skill system lands);
    /// Wait and Berry top-right; a message log; floating numbers; fades, floor banner and end-of-run panel.
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

        const int LogLines = 4;
        const float LogLifetime = 6f;
        const float LogFade = 1f;

        public event Action<HeroCommand> CommandRequested;
        public event Action RestartRequested;

        public DPad DPad { get; private set; }

        readonly List<LogLine> log = new List<LogLine>();
        readonly List<FloatingText> floating = new List<FloatingText>();
        Camera worldCamera;
        RectTransform canvasRect, safeArea, floatingLayer, logRoot;
        Text heroText, hpText, floorText, bannerTitle, bannerSubtitle, endTitle, endDetail;
        Image hpFill;
        HoldButton berryButton, descendButton;
        CanvasGroup banner, endPanel;
        Image fader;
        Coroutine bannerRoutine;

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
            floorText.text = $"B{run.Floor}F";
            berryButton.SetLabel($"Berry x{run.Berries}");
            berryButton.Interactable = run.Berries > 0;
            descendButton.gameObject.SetActive(run.State == RunState.InProgress && run.HeroOnStairs);
        }

        public void SetHeroHp(int hp, int maxHp)
        {
            float ratio = maxHp > 0 ? Mathf.Clamp01(hp / (float)maxHp) : 0f;
            hpFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
            hpFill.color = ratio > 0.5f ? new Color(0.36f, 0.86f, 0.42f) : ratio > 0.25f ? new Color(0.95f, 0.78f, 0.25f) : new Color(0.92f, 0.3f, 0.26f);
            hpText.text = $"{hp}/{maxHp}";
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

        public void ShowRunEnd(bool won, DungeonRun run)
        {
            endTitle.text = won ? "Dungeon Cleared!" : $"{run.Hero.Name} fainted...";
            endTitle.color = won ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.55f, 0.5f);
            endDetail.text = won
                ? $"Cleared all {run.Config.FloorCount} floors of {run.Config.Name} at Lv {run.Hero.Level} in {run.Turn} turns."
                : $"Reached B{run.Floor}F of {run.Config.Name} at Lv {run.Hero.Level}.";
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

            BuildStatus();
            BuildLog();
            BuildControls();
            BuildOverlays();
            HideRunEnd();
        }

        void BuildStatus()
        {
            var panel = UiFactory.CreateImage("Status", safeArea, UiFactory.RoundedRect, PanelColor);
            UiFactory.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(540f, 122f), new Vector2(0f, 1f));
            heroText = UiFactory.CreateText("Hero", panel.transform, "", 36, TextAnchor.UpperLeft, TextColor);
            UiFactory.Place(heroText.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -12f), new Vector2(490f, 44f), new Vector2(0f, 1f));

            var barBack = UiFactory.CreateImage("HpBack", panel.transform, UiFactory.RoundedRect, new Color(0f, 0f, 0f, 0.65f));
            UiFactory.Place(barBack.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -66f), new Vector2(330f, 36f), new Vector2(0f, 1f));
            hpFill = UiFactory.CreateImage("HpFill", barBack.transform, UiFactory.RoundedRect, Color.green);
            var fillRect = hpFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);
            hpText = UiFactory.CreateText("Hp", panel.transform, "", 30, TextAnchor.MiddleLeft, TextColor);
            UiFactory.Place(hpText.rectTransform, new Vector2(0f, 1f), new Vector2(370f, -84f), new Vector2(160f, 40f), new Vector2(0f, 0.5f));

            var floorPanel = UiFactory.CreateImage("Floor", safeArea, UiFactory.RoundedRect, PanelColor);
            UiFactory.Place(floorPanel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(200f, 72f), new Vector2(0.5f, 1f));
            floorText = UiFactory.CreateText("FloorText", floorPanel.transform, "", 40, TextAnchor.MiddleCenter, TextColor);
            UiFactory.Stretch(floorText.rectTransform);

            if (!Application.isMobilePlatform)
            {
                var keys = UiFactory.CreateText("Keys", safeArea,
                    "Move: WASD / arrows + QEZC or numpad   Attack: Space   Wait: X   Berry: B   Stairs: Enter",
                    22, TextAnchor.UpperLeft, HintColor);
                UiFactory.Place(keys.rectTransform, new Vector2(0f, 1f), new Vector2(32f, -156f), new Vector2(1000f, 30f), new Vector2(0f, 1f));
            }
        }

        void BuildLog()
        {
            logRoot = UiFactory.CreateRect("Log", safeArea);
            UiFactory.Place(logRoot, new Vector2(0.5f, 0f), new Vector2(-330f, 40f), new Vector2(660f, 160f), new Vector2(0f, 0f));
        }

        void BuildControls()
        {
            var bottomLeft = new Vector2(0f, 0f);
            var bottomRight = new Vector2(1f, 0f);
            var topRight = new Vector2(1f, 1f);

            DPad = DPad.Create(safeArea, bottomLeft, new Vector2(270f, 270f), 420f);

            var attack = HoldButton.Create(safeArea, "Attack", "ATK", bottomRight, new Vector2(-230f, 230f), new Vector2(230f, 230f), AttackColor, 48, round: true);
            attack.Pressed += () => CommandRequested?.Invoke(HeroCommand.Attack);

            // Three job/race skills and an ultimate are planned; they're visible now to judge the layout, but locked.
            var lockedSlots = new[]
            {
                ("Skill 1", "S1", new Vector2(-470f, 120f), 140f, SkillColor),
                ("Skill 2", "S2", new Vector2(-455f, 335f), 140f, SkillColor),
                ("Skill 3", "S3", new Vector2(-330f, 485f), 140f, SkillColor),
                ("Ultimate", "ULT", new Vector2(-135f, 460f), 150f, UltimateColor),
            };
            foreach (var (name, label, position, size, color) in lockedSlots)
            {
                var slot = HoldButton.Create(safeArea, name, label, bottomRight, position, new Vector2(size, size), color, 34, round: true);
                slot.Interactable = false;
                slot.DisabledPressed += () => AddMessage("Skills unlock in a later milestone.", HintColor);
            }

            berryButton = HoldButton.Create(safeArea, "Berry", "Berry x0", topRight, new Vector2(-140f, -64f), new Vector2(230f, 84f), ActionColor, 32, round: false);
            berryButton.Pressed += () => CommandRequested?.Invoke(HeroCommand.UseBerry);
            berryButton.DisabledPressed += () => AddMessage("You have no berries.", HintColor);

            var wait = HoldButton.Create(safeArea, "Wait", "Wait", topRight, new Vector2(-390f, -64f), new Vector2(230f, 84f), ActionColor, 32, round: false);
            wait.Pressed += () => CommandRequested?.Invoke(HeroCommand.Wait);

            descendButton = HoldButton.Create(safeArea, "Descend", "Descend", new Vector2(0.5f, 0f), new Vector2(0f, 260f), new Vector2(300f, 96f), DescendColor, 38, round: false);
            descendButton.Pressed += () => CommandRequested?.Invoke(HeroCommand.Descend);
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
            UiFactory.Place(end.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 420f));
            endPanel = end.gameObject.AddComponent<CanvasGroup>();
            endTitle = UiFactory.CreateText("Title", end.transform, "", 64, TextAnchor.MiddleCenter, TextColor);
            UiFactory.Place(endTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(780f, 90f));
            endDetail = UiFactory.CreateText("Detail", end.transform, "", 32, TextAnchor.MiddleCenter, HintColor);
            UiFactory.Place(endDetail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(780f, 60f));
            var again = HoldButton.Create(end.transform, "TryAgain", "Try Again", new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(320f, 100f), AttackColor, 40, round: false);
            again.Pressed += () => RestartRequested?.Invoke();
        }
    }
}

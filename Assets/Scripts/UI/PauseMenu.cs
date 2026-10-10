using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FiveKingdoms.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FiveKingdoms.UI
{
    /// <summary>
    /// The pause menu (HUD.md, "Pause button and menu"), built in code like the rest of the HUD: Resume, Restart,
    /// Settings, Hero stats, Exit, and Reset level for testing. Restart, Exit and Reset level ask first. Hero stats is
    /// a read-only page per hero: class, level and EXP, the weapon, the stats, and the weapon attack, the skills and the
    /// ultimate with what they do (<see cref="SkillText"/>), all read from the hero's own kit, and a button that opens
    /// the hero's skill tree to look at (<see cref="SkillTreeScreen"/>). Buttons answer a touch
    /// or a click; with keys or a controller the game moves a marked button (<see cref="Move"/>, <see cref="Side"/>,
    /// <see cref="Activate"/>, <see cref="Back"/>). The menu only asks: the controller does what was chosen.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        static readonly Color Veil = new Color(0.04f, 0.05f, 0.09f, 0.6f);
        const string HintHex = "BFBFD1"; // DungeonHud.HintColor, for rich text.
        static readonly Vector2 ButtonSize = new Vector2(320f, 90f);

        public event Action ResumeRequested;
        public event Action RestartConfirmed;
        public event Action ExitConfirmed;
        public event Action ResetLevelConfirmed;

        /// <summary>The view was switched (HUD.md, "The view on the tablet"): true for Far, false for Near.</summary>
        public event Action<bool> ViewChanged;

        /// <summary>The minimap was switched to another size, or off.</summary>
        public event Action<MinimapSize> MinimapChanged;

        /// <summary>The hero stats page's Skill tree button, with the hero's place in the party: during a run the tree only shows.</summary>
        public event Action<int> SkillTreeRequested;

        enum Page { Main, Confirm, Settings, Stats }

        readonly Dictionary<Page, GameObject> pages = new Dictionary<Page, GameObject>();
        readonly Dictionary<Page, List<HoldButton>> items = new Dictionary<Page, List<HoldButton>>();
        Page page;
        int selected;
        Text confirmText, statsTitle, statsLeft, statsRight;
        Image statsPortrait;
        HoldButton confirmButton, viewButton, minimapButton;
        MinimapSize minimap;
        Action confirmed;
        DungeonRun run;
        IReadOnlyList<HeroProgress> party;
        int shownHero;
        bool farView, farAvailable;

        public bool IsOpen => gameObject.activeSelf;

        public static PauseMenu Create(Transform canvas)
        {
            var root = UiFactory.Stretch(UiFactory.CreateRect("PauseMenu", canvas));
            var menu = root.gameObject.AddComponent<PauseMenu>();
            var veil = UiFactory.CreateImage("Veil", root, null, Veil, raycast: true); // Nothing under the menu answers.
            UiFactory.Stretch(veil.rectTransform);
            menu.BuildMain(root);
            menu.BuildConfirm(root);
            menu.BuildSettings(root);
            menu.BuildStats(root);
            root.gameObject.SetActive(false);
            return menu;
        }

        /// <summary>
        /// Opens on the main page. <paramref name="party"/>: the heroes' saved progress, for their classes.
        /// <paramref name="farAvailable"/>: whether this screen has a Far view; without one the choice is greyed out.
        /// </summary>
        public void Open(DungeonRun run, IReadOnlyList<HeroProgress> party, bool farView, bool farAvailable, MinimapSize minimap)
        {
            this.run = run;
            this.party = party;
            this.farView = farView;
            this.farAvailable = farAvailable;
            this.minimap = minimap;
            shownHero = 0;
            for (int i = 0; i < run.Party.Count; i++)
                if (run.Party[i] == run.Hero) shownHero = i;
            RefreshSettings();
            gameObject.SetActive(true);
            Show(Page.Main);
        }

        public void Close() => gameObject.SetActive(false);

        /// <summary>Keys or a controller: marks the next button (1) or the one before (-1).</summary>
        public void Move(int delta)
        {
            int count = items[page].Count;
            selected = ((selected + delta) % count + count) % count;
            Mark();
        }

        /// <summary>Left or right: the other answer of a question, or another hero's stats.</summary>
        public void Side(int delta)
        {
            if (page == Page.Stats) ShowStats(shownHero + delta);
            else if (page == Page.Confirm) Move(delta);
        }

        /// <summary>Presses the marked button.</summary>
        public void Activate() => items[page][selected].Press();

        /// <summary>One page back; from the main page, back to the game.</summary>
        public void Back()
        {
            if (page == Page.Main) ResumeRequested?.Invoke();
            else Show(Page.Main);
        }

        // ---- Pages ----

        void Show(Page which, int select = 0)
        {
            page = which;
            foreach (var entry in pages) entry.Value.SetActive(entry.Key == which);
            selected = Mathf.Clamp(select, 0, items[which].Count - 1);
            Mark();
        }

        void Mark()
        {
            var list = items[page];
            for (int i = 0; i < list.Count; i++) list[i].SetHighlight(i == selected);
        }

        Transform NewPage(Transform root, Page which, Vector2 size, string title)
        {
            var panel = UiFactory.CreatePanel(which.ToString(), root, "panel", raycast: true);
            UiFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            pages[which] = panel.gameObject;
            items[which] = new List<HoldButton>();
            if (title != null)
            {
                var text = UiFactory.CreateText("Title", panel.transform, title, 52, TextAnchor.MiddleCenter, DungeonHud.TextColor);
                UiFactory.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(size.x - 120f, 70f));
            }
            return panel.transform;
        }

        HoldButton AddButton(Page which, Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size, string art, Action pressed)
        {
            var button = HoldButton.Create(parent, label, label, anchor, position, size, art, 34);
            button.Pressed += pressed;
            items[which].Add(button);
            return button;
        }

        void BuildMain(Transform root)
        {
            var panel = NewPage(root, Page.Main, new Vector2(620f, 880f), "Paused");
            var entries = new (string label, string art, Action pressed)[]
            {
                ("Resume", "button_gold", () => ResumeRequested?.Invoke()),
                ("Restart", "button_dark", () => Ask("Start this run again from the first floor?\nLevels and EXP earned so far are kept.",
                    "Restart", () => RestartConfirmed?.Invoke())),
                ("Settings", "button_dark", () => Show(Page.Settings)),
                ("Hero stats", "button_dark", () => ShowStats(shownHero)),
                ("Exit", "button_dark", () => Ask("Leave the dungeon?\nLevels and EXP earned so far are kept.", "Exit", () => ExitConfirmed?.Invoke())),
                // For testing only (HUD.md): to be hidden or removed later.
                ("Reset level (testing)", "button_red", () => Ask("Testing only: every hero goes back to level 1\n" +
                    "with no EXP, and the run starts again.\nThis can't be undone.", "Reset", () => ResetLevelConfirmed?.Invoke())),
            };
            for (int i = 0; i < entries.Length; i++)
                AddButton(Page.Main, panel, entries[i].label, new Vector2(0.5f, 1f), new Vector2(0f, -205f - i * 104f), new Vector2(440f, 90f),
                    entries[i].art, entries[i].pressed);
        }

        void BuildConfirm(Transform root)
        {
            var panel = NewPage(root, Page.Confirm, new Vector2(1100f, 480f), null);
            confirmText = UiFactory.CreateText("Question", panel, "", 34, TextAnchor.MiddleCenter, DungeonHud.TextColor);
            UiFactory.Place(confirmText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(980f, 150f));
            confirmButton = AddButton(Page.Confirm, panel, "Yes", new Vector2(0.5f, 0f), new Vector2(-190f, 135f), ButtonSize, "button_red", () =>
            {
                var action = confirmed;
                confirmed = null;
                action?.Invoke();
            });
            AddButton(Page.Confirm, panel, "Cancel", new Vector2(0.5f, 0f), new Vector2(190f, 135f), ButtonSize, "button_dark", () => Show(Page.Main));
        }

        /// <summary>Restart, Exit and Reset level ask first; Cancel is the marked answer.</summary>
        void Ask(string question, string yes, Action onYes)
        {
            confirmText.text = question;
            confirmButton.SetLabel(yes);
            confirmed = onYes;
            Show(Page.Confirm, select: 1);
        }

        void BuildSettings(Transform root)
        {
            var panel = NewPage(root, Page.Settings, new Vector2(1100f, 650f), "Settings");
            var label = UiFactory.CreateText("ViewLabel", panel, "View", 32, TextAnchor.MiddleLeft, DungeonHud.TextColor);
            UiFactory.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(100f, -230f), new Vector2(420f, 60f), new Vector2(0f, 0.5f));
            viewButton = AddButton(Page.Settings, panel, "", new Vector2(1f, 1f), new Vector2(-330f, -230f), new Vector2(460f, 90f), "button_dark", () =>
            {
                farView = !farView;
                RefreshSettings();
                ViewChanged?.Invoke(farView);
            });
            var mapLabel = UiFactory.CreateText("MinimapLabel", panel, "Minimap", 32, TextAnchor.MiddleLeft, DungeonHud.TextColor);
            UiFactory.Place(mapLabel.rectTransform, new Vector2(0f, 1f), new Vector2(100f, -340f), new Vector2(420f, 60f), new Vector2(0f, 0.5f));
            minimapButton = AddButton(Page.Settings, panel, "", new Vector2(1f, 1f), new Vector2(-330f, -340f), new Vector2(460f, 90f), "button_dark", () =>
            {
                minimap = minimap == MinimapSize.Small ? MinimapSize.Large : minimap == MinimapSize.Large ? MinimapSize.Off : MinimapSize.Small;
                RefreshSettings();
                MinimapChanged?.Invoke(minimap);
            });
            AddButton(Page.Settings, panel, "Back", new Vector2(0.5f, 0f), new Vector2(0f, 135f), ButtonSize, "button_dark", () => Show(Page.Main));
        }

        void RefreshSettings()
        {
            // Where the screen already plays at 1x there is no step further out: Near, greyed out.
            viewButton.Interactable = farAvailable;
            viewButton.SetLabel(farView && farAvailable ? "Far" : "Near");
            minimapButton.SetLabel(minimap.ToString());
        }

        void BuildStats(Transform root)
        {
            var panel = NewPage(root, Page.Stats, new Vector2(1640f, 1000f), null);
            statsPortrait = PartyPanel.CreatePortrait(panel, new Vector2(0f, 1f), new Vector2(96f, -80f), 96f, new Vector2(0f, 1f));
            statsTitle = UiFactory.CreateText("Hero", panel, "", 46, TextAnchor.MiddleLeft, DungeonHud.TextColor);
            UiFactory.Place(statsTitle.rectTransform, new Vector2(0f, 1f), new Vector2(216f, -128f), new Vector2(900f, 60f), new Vector2(0f, 0.5f));
            statsLeft = Body(panel, "Stats", new Vector2(100f, -200f), new Vector2(400f, 610f));
            statsRight = Body(panel, "Skills", new Vector2(530f, -200f), new Vector2(1010f, 610f));
            AddButton(Page.Stats, panel, "Back", new Vector2(0.5f, 0f), new Vector2(0f, 125f), ButtonSize, "button_dark", () => Show(Page.Main));
            AddButton(Page.Stats, panel, "< Hero", new Vector2(0.5f, 0f), new Vector2(-360f, 125f), ButtonSize, "button_dark", () => ShowStats(shownHero - 1));
            AddButton(Page.Stats, panel, "Hero >", new Vector2(0.5f, 0f), new Vector2(360f, 125f), ButtonSize, "button_dark", () => ShowStats(shownHero + 1));
            AddButton(Page.Stats, panel, "Skill tree", new Vector2(1f, 1f), new Vector2(-270f, -128f), new Vector2(360f, 84f), "button_gold", () =>
            {
                var shown = run.Party[shownHero].Definition;
                int index = 0;
                for (int i = 0; party != null && i < party.Count; i++)
                    if (party[i].Definition == shown) index = i;
                SkillTreeRequested?.Invoke(index);
            });
        }

        static Text Body(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var text = UiFactory.CreateText(name, parent, "", 28, TextAnchor.UpperLeft, DungeonHud.TextColor);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.supportRichText = true;
            UiFactory.Place(text.rectTransform, new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
            return text;
        }

        /// <summary>One hero's page (the index wraps around the party): read-only, a build changes between runs.</summary>
        void ShowStats(int index)
        {
            int count = run.Party.Count;
            shownHero = (index % count + count) % count;
            var hero = run.Party[shownHero];
            var progress = party?.FirstOrDefault(member => member.Definition == hero.Definition);

            statsTitle.text = $"{hero.Name}   Lv {hero.Level}";
            statsPortrait.sprite = PartyPanel.PortraitOf(hero);
            statsPortrait.enabled = statsPortrait.sprite != null;
            UiArtScaler.Size(statsPortrait);

            var left = new StringBuilder();
            if (progress != null && progress.Classes.Count > 0)
                left.AppendLine(string.Join("\n", progress.Classes.Select(entry => $"{entry.Class.Name}, tier {entry.Tier}")));
            left.AppendLine($"EXP {hero.Exp} / {CombatRules.ExpToNextLevel(hero.Level)}");
            left.AppendLine();
            left.AppendLine($"<color=#{HintHex}>Weapon</color>");
            left.AppendLine(hero.Weapon != null ? hero.Weapon.Name : "None");
            left.AppendLine();
            left.AppendLine($"<color=#{HintHex}>Stats</color>");
            left.AppendLine($"HP  {hero.Hp} / {hero.MaxHp}");
            left.AppendLine($"ATK  {hero.Attack}");
            left.AppendLine($"DEF  {hero.Defense}");
            left.AppendLine($"SPD  {hero.Speed}");
            left.AppendLine($"Crit Rate  {Percent(hero.CritRate)}");
            left.Append($"Crit DMG  {Percent(hero.CritDmg)}");
            statsLeft.text = left.ToString();

            var right = new StringBuilder();
            void Entry(SkillDefinition skill, string kind)
            {
                if (skill == null) return;
                right.AppendLine($"<b>{skill.Name}</b>  <color=#{HintHex}>{kind}</color>");
                right.AppendLine($"<size=24>{SkillText.Describe(skill)}</size>");
                right.AppendLine("<size=8> </size>");
            }
            Entry(hero.Kit.WeaponAttack, "weapon attack");
            for (int i = 0; i < hero.Skills.Count; i++) Entry(hero.Skills[i], $"skill {i + 1}");
            Entry(hero.Ultimate, "ultimate");
            statsRight.text = right.ToString();

            Show(Page.Stats, page == Page.Stats ? selected : 0);
        }

        /// <summary>Crit Rate and Crit DMG are kept in tenths of a percent (50 = 5%).</summary>
        static string Percent(int tenths) => (tenths / 10f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";
    }
}

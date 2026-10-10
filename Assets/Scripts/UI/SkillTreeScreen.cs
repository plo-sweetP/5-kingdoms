using System;
using System.Collections.Generic;
using FiveKingdoms.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FiveKingdoms.UI
{
    /// <summary>
    /// The skill-tree screen (PROGRESSION.md, "Building 1g", step 6; ART.md, "Skill tree screen"), built in code like the
    /// rest of the HUD. Per hero: the classes with their tiers and the points left, the tree of spheres for one class
    /// (three paths side by side, rows for tiers 5 to 25, a track with a mark per tier), an info panel for whatever
    /// the cursor is on, the loadout, and unlearning a class. Everything it knows and does comes from a
    /// <see cref="SkillTreeModel"/>: this class only draws that model and passes on touches, keys and controller
    /// buttons (<see cref="Move"/>, <see cref="Activate"/>, <see cref="Back"/>, <see cref="NextHero"/>).
    /// <para>
    /// It is laid out in the canvas's units (1080 high on every screen) from the width it gets, like
    /// `Tools/pixelart/tree_mock.py` draws its mock-ups; all its art goes through <see cref="UiFactory"/>, at whole
    /// screen pixels. Its owner opens it (between runs to change a build, during a run to look only), saves when a
    /// build changed (<see cref="BuildChanged"/>) and closes it (<see cref="CloseRequested"/>).
    /// </para>
    /// </summary>
    public sealed class SkillTreeScreen : MonoBehaviour
    {
        /// <summary>The three paths, left to right; `Tools/pixelart/tree.py` has the same for the mock-ups.</summary>
        public static readonly Color[] PathColors = { Hex(0x5aa9ee), Hex(0x7ccb62), Hex(0xbb82f0) };

        static readonly Color Backdrop = new Color(0.063f, 0.075f, 0.11f, 1f);
        static readonly Color Dim = Hex(0x9aa3b5), Faint = Hex(0x5d667b), Gold = Hex(0xffd866);
        static readonly Color LockedTint = Hex(0x444a5e), PassedTint = Hex(0x666d82), SlotTint = Hex(0x8d98ad), UltimateTint = Hex(0xf0b93c);
        static readonly Color TrackAhead = Hex(0x4a5063), LineQuiet = Hex(0x3a4156), Unchosen = Hex(0xb9bfcc);
        const string DimHex = "9aa3b5";
        const int ChoiceRows = 4;

        /// <summary>The player wants out: Close, Esc or B with nothing left to step back from.</summary>
        public event Action CloseRequested;

        /// <summary>A hero's build changed: the owner saves it.</summary>
        public event Action BuildChanged;

        /// <summary>A sphere and what sits on it: the tree's options, the loadout's slots, the info panel's picture.</summary>
        sealed class Orb
        {
            public RectTransform Root;
            public Image Glow, Ring, Ball, Shine, Glyph, Lock, Upgrade, Learned;
            public Text Label;
            public bool Pulses;
        }

        sealed class Row
        {
            public RectTransform Rect;
            public Image Back, Bar, Icon;
            public Text Name, Detail;
        }

        /// <summary>A touch or a click on something that isn't a button of the kit.</summary>
        sealed class Hit : MonoBehaviour, IPointerDownHandler
        {
            public Action Pressed;

            public void OnPointerDown(PointerEventData eventData) => Pressed?.Invoke();
        }

        RectTransform content;
        Canvas canvas;
        InputMode inputMode = InputMode.Touch;
        float scale = 1f;
        Vector2 laidOutFor;
        float laidOutUnits;
        int choicesFrom;

        // What the layout worked out, for the cursor.
        float left, treeLeft, infoLeft, leftWidth, treeWidth, infoWidth, top, bottom, trackX, column, slotStep, boxTop, boxHeight;
        readonly float[] pathX = new float[ClassDefinition.PathCount];

        Image treePanel, infoPanel, infoBox, cursor, trackLine, confirmVeil, confirmPanel;
        Text pointsLabel, pointsValue, classesHeader, loadoutHeader, hint, treeTitle, treeTier, infoTitle, infoKind, infoBody, infoStatus, confirmText, moreChoices;
        HoldButton closeButton, unlearnButton, actionButton, yesButton, cancelButton;
        Orb infoOrb;
        readonly List<Row> tabs = new List<Row>();
        readonly List<Row> classRows = new List<Row>();
        readonly Row[] choiceRows = new Row[ChoiceRows];
        readonly Orb[] slots = new Orb[SkillTreeModel.Slots];
        readonly Orb[,] orbs = new Orb[SkillTreeModel.Rows, ClassDefinition.PathCount];
        readonly Image[,] pathLines = new Image[ClassDefinition.PathCount, SkillTreeModel.Rows];
        readonly Image[] rowLines = new Image[SkillTreeModel.Rows];
        readonly Image[] marks = new Image[ClassDefinition.MaxTier];
        readonly Text[] tierNumbers = new Text[SkillTreeModel.Rows];
        readonly Text[] pathNames = new Text[ClassDefinition.PathCount];
        readonly Text[] rowNotes = new Text[SkillTreeModel.Rows];

        /// <summary>What the screen shows and does; null while it is closed.</summary>
        public SkillTreeModel Model { get; private set; }

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>The info panel's words, for tests: title, what it is, what it does, why its button can or can't be pressed.</summary>
        public string InfoText => $"{infoTitle.text}\n{infoKind.text}\n{infoBody.text}\n{infoStatus.text}";

        public static SkillTreeScreen Create(Transform canvasRoot)
        {
            var root = UiFactory.Stretch(UiFactory.CreateRect("SkillTree", canvasRoot));
            var screen = root.gameObject.AddComponent<SkillTreeScreen>();
            screen.canvas = canvasRoot.GetComponentInParent<Canvas>();
            var veil = UiFactory.CreateImage("Veil", root, null, Backdrop, raycast: true); // Nothing under the screen answers.
            UiFactory.Stretch(veil.rectTransform);
            screen.content = UiFactory.Stretch(UiFactory.CreateRect("SafeArea", root));
            screen.content.gameObject.AddComponent<SafeAreaFitter>();
            root.gameObject.SetActive(false);
            return screen;
        }

        // ---- Opening and closing ----

        /// <summary>
        /// Shows the party's builds, starting on <paramref name="hero"/>. <paramref name="readOnly"/>: during a run
        /// nothing can be changed (a build changes between runs only).
        /// </summary>
        public void Open(IReadOnlyList<HeroProgress> party, int hero, bool readOnly, InputMode mode)
        {
            Model = new SkillTreeModel(party, hero, readOnly);
            Model.Changed += () => BuildChanged?.Invoke();
            inputMode = mode;
            choicesFrom = 0;
            gameObject.SetActive(true);
            transform.SetAsLastSibling(); // Over the pause menu and the end panel, whichever opened it.
            if (treePanel == null || tabs.Count != Model.Party.Count || classRows.Count != Model.Classes.Count)
            {
                // Built on first use, and again if the party or the class list has changed size since.
                for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
                tabs.Clear();
                classRows.Clear();
                Build();
            }
            Layout();
            Refresh();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            Model = null;
        }

        public void SetInputMode(InputMode mode)
        {
            if (inputMode == mode) return;
            inputMode = mode;
            if (IsOpen) Refresh();
        }

        // ---- Keys and a controller ----

        public void Move(int dx, int dy)
        {
            Model.Move(dx, dy);
            Refresh();
        }

        /// <summary>Enter, Space or A: presses what the cursor stands on.</summary>
        public void Activate()
        {
            Model.Activate();
            Refresh();
        }

        /// <summary>Esc or B: out of a question or a list; with nothing to step out of, the screen asks to be closed.</summary>
        public void Back()
        {
            if (Model.Back()) Refresh();
            else CloseRequested?.Invoke();
        }

        /// <summary>Q and E, L1 and R1: the hero before or after.</summary>
        public void NextHero(int delta) => SelectHero(Model.HeroIndex + delta);

        void SelectHero(int index)
        {
            if (Model.Confirming) return;
            Model.SelectHero(index);
            choicesFrom = 0;
            Refresh();
        }

        /// <summary>A touch on something: the cursor goes there; with <paramref name="press"/> it is pressed too (the kit's buttons).</summary>
        public void Tap(TreeFocus focus, bool press = false)
        {
            Model.Tap(focus);
            if (press) Model.Activate();
            Refresh();
        }

        // ---- Building (once) ----

        void Build()
        {
            treePanel = UiFactory.CreatePanel("Tree", content, "panel_dark");
            infoPanel = UiFactory.CreatePanel("Info", content, "panel_dark");

            // The top bar: a tab per hero, the points left, Close.
            for (int i = 0; i < Model.Party.Count; i++)
            {
                int index = i;
                var tab = NewRow("Hero" + i, () => SelectHero(index));
                tab.Icon = PartyPanel.CreatePortrait(tab.Rect, new Vector2(0f, 0.5f), new Vector2(12f, 0f), 64f, new Vector2(0f, 0.5f));
                tab.Name = NewText("Name", 28, TextAnchor.UpperLeft, DungeonHud.TextColor, tab.Rect);
                tab.Detail = NewText("Level", 22, TextAnchor.UpperLeft, Dim, tab.Rect);
                tabs.Add(tab);
            }
            pointsLabel = NewText("PointsLabel", 22, TextAnchor.UpperRight, Dim);
            pointsValue = NewText("Points", 36, TextAnchor.UpperRight, Gold);
            closeButton = HoldButton.Create(content, "Close", "Close", new Vector2(0f, 1f), Vector2.zero, new Vector2(170f, 72f), "button_dark", 28);
            closeButton.Pressed += () => CloseRequested?.Invoke();

            // The left column: the classes, the loadout, unlearn.
            classesHeader = NewText("ClassesHeader", 22, TextAnchor.UpperLeft, Dim);
            classesHeader.text = "Classes";
            for (int i = 0; i < Model.Classes.Count; i++)
            {
                int index = i;
                var row = NewRow("Class" + i, () => Tap(TreeFocus.Class(index)));
                row.Name = NewText("Name", 28, TextAnchor.UpperLeft, DungeonHud.TextColor, row.Rect);
                row.Detail = NewText("Tier", 20, TextAnchor.UpperLeft, Dim, row.Rect);
                classRows.Add(row);
            }
            loadoutHeader = NewText("LoadoutHeader", 22, TextAnchor.UpperLeft, Dim);
            loadoutHeader.text = "Loadout";
            for (int i = 0; i < slots.Length; i++)
            {
                int slot = i;
                slots[i] = NewOrb("Slot" + i, big: false, withLabel: false, () => Tap(TreeFocus.Slot(slot)), hitSize: 94f);
            }
            hint = NewText("Hint", 20, TextAnchor.UpperLeft, Faint);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            unlearnButton = HoldButton.Create(content, "Unlearn", "Unlearn", new Vector2(0f, 1f), Vector2.zero, new Vector2(380f, 76f), "button_red", 26);
            unlearnButton.Pressed += () => Tap(TreeFocus.Unlearn, press: true);
            unlearnButton.DisabledPressed += () => Tap(TreeFocus.Unlearn, press: true);

            // The tree: its lines first, then the track's marks, then the spheres over them.
            treeTitle = NewText("Class", 38, TextAnchor.UpperCenter, Gold);
            treeTier = NewText("Tier", 22, TextAnchor.UpperCenter, Dim);
            for (int path = 0; path < ClassDefinition.PathCount; path++)
            {
                for (int row = 0; row < SkillTreeModel.Rows; row++) pathLines[path, row] = UiFactory.CreatePanel("PathLine", content, "tree_line");
                pathNames[path] = NewText("Path" + path, 24, TextAnchor.UpperCenter, PathColors[path]);
            }
            for (int row = 0; row < SkillTreeModel.Rows; row++) rowLines[row] = UiFactory.CreatePanel("RowLine", content, "tree_line", LineQuiet);
            trackLine = UiFactory.CreatePanel("Track", content, "tree_line", LineQuiet);
            for (int tier = 1; tier <= ClassDefinition.MaxTier; tier++)
                marks[tier - 1] = UiFactory.CreateIcon("Tier" + tier, content, ClassDefinition.IsMilestone(tier) ? "tree_node" : "tree_pip");
            for (int row = 0; row < SkillTreeModel.Rows; row++)
            {
                tierNumbers[row] = NewText("Number" + row, 26, TextAnchor.UpperRight, Dim);
                tierNumbers[row].text = SkillTreeModel.TierOfRow(row).ToString();
                for (int path = 0; path < ClassDefinition.PathCount; path++)
                {
                    int r = row, p = path;
                    orbs[row, path] = NewOrb($"Option{row}_{path}", big: false, withLabel: true, () => Tap(TreeFocus.Option(r, p)));
                }
                rowNotes[row] = NewText("RowNote" + row, 20, TextAnchor.UpperCenter, Faint);
            }

            // The info panel: what the cursor is on, the skills that could go in a slot, and the button.
            infoOrb = NewOrb("Picture", big: true, withLabel: false, null);
            infoTitle = NewText("Title", 32, TextAnchor.MiddleLeft, Gold);
            infoTitle.resizeTextForBestFit = true;
            infoTitle.resizeTextMinSize = 20;
            infoTitle.resizeTextMaxSize = 32;
            infoTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            infoTitle.verticalOverflow = VerticalWrapMode.Truncate;
            infoKind = NewText("Kind", 20, TextAnchor.UpperLeft, Dim);
            infoKind.horizontalOverflow = HorizontalWrapMode.Wrap;
            infoBox = UiFactory.CreatePanel("Box", content, "panel_inset");
            infoBody = NewText("Body", 24, TextAnchor.UpperLeft, DungeonHud.TextColor);
            infoBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            infoBody.verticalOverflow = VerticalWrapMode.Truncate;
            infoBody.supportRichText = true;
            for (int i = 0; i < ChoiceRows; i++)
            {
                int index = i;
                var row = NewRow("Choice" + i, () => Tap(TreeFocus.Choice(choicesFrom + index)));
                row.Icon = UiFactory.CreateIcon("Icon", row.Rect, null);
                row.Icon.rectTransform.anchorMin = row.Icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                row.Icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                row.Name = NewText("Name", 24, TextAnchor.MiddleLeft, DungeonHud.TextColor, row.Rect);
                row.Detail = NewText("State", 18, TextAnchor.MiddleRight, Dim, row.Rect);
                choiceRows[i] = row;
            }
            moreChoices = NewText("More", 18, TextAnchor.UpperRight, Dim);
            var more = moreChoices.gameObject.AddComponent<Hit>();
            moreChoices.raycastTarget = true;
            more.Pressed = () =>
            {
                choicesFrom = choicesFrom + ChoiceRows < Model.Choices.Count ? choicesFrom + ChoiceRows : 0;
                Refresh();
            };
            infoStatus = NewText("Status", 20, TextAnchor.UpperLeft, Dim);
            infoStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            infoStatus.verticalOverflow = VerticalWrapMode.Truncate;
            infoStatus.supportRichText = true;
            actionButton = HoldButton.Create(content, "Action", "", new Vector2(0f, 1f), Vector2.zero, new Vector2(400f, 80f), "button_gold", 30);
            actionButton.Pressed += Activate;
            actionButton.DisabledPressed += Activate; // The model then says why not.

            cursor = UiFactory.CreatePanel("Cursor", content, "tree_cursor");

            // The question before unlearning, over everything else.
            confirmVeil = UiFactory.CreateImage("ConfirmVeil", content, null, new Color(0.04f, 0.05f, 0.09f, 0.7f), raycast: true);
            confirmPanel = UiFactory.CreatePanel("Confirm", content, "panel_dark", raycast: true);
            confirmText = NewText("Question", 30, TextAnchor.MiddleCenter, DungeonHud.TextColor, confirmPanel.rectTransform);
            confirmText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.Place(confirmText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(820f, 150f));
            yesButton = HoldButton.Create(confirmPanel.transform, "Yes", "Unlearn", new Vector2(0.5f, 0f), new Vector2(-180f, 95f), new Vector2(320f, 90f), "button_red", 30);
            yesButton.Pressed += () => Tap(TreeFocus.Confirm(true), press: true);
            cancelButton = HoldButton.Create(confirmPanel.transform, "Cancel", "Cancel", new Vector2(0.5f, 0f), new Vector2(180f, 95f), new Vector2(320f, 90f), "button_dark", 30);
            cancelButton.Pressed += () => Tap(TreeFocus.Confirm(false), press: true);
        }

        Text NewText(string name, int size, TextAnchor anchor, Color color, Transform parent = null)
        {
            var text = UiFactory.CreateText(name, parent != null ? parent : content, "", size, anchor, color);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0f, 1f);
            return text;
        }

        /// <summary>A sunken row that answers a touch: a hero's tab, a class, a skill in the list of choices.</summary>
        Row NewRow(string name, Action pressed)
        {
            var back = UiFactory.CreatePanel(name, content, "panel_inset", raycast: true);
            back.gameObject.AddComponent<Hit>().Pressed = pressed;
            var row = new Row { Rect = back.rectTransform, Back = back };
            row.Bar = UiFactory.CreateImage("Bar", back.transform, null, Gold);
            return row;
        }

        /// <param name="hitSize">How wide a touch counts, in units: a thumb's width where the neighbours leave room.</param>
        Orb NewOrb(string name, bool big, bool withLabel, Action pressed, float hitSize = 132f)
        {
            string art = big ? "sphere_big" : "sphere";
            var orb = new Orb { Root = UiFactory.CreateRect(name, content) };
            Image Layer(string layer, string piece)
            {
                var image = UiFactory.CreateIcon(layer, orb.Root, piece);
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                image.rectTransform.anchoredPosition = Vector2.zero;
                return image;
            }
            orb.Glow = Layer("Glow", art + "_glow");
            orb.Ring = Layer("Ring", art + "_ring");
            orb.Ball = Layer("Ball", art);
            orb.Shine = Layer("Shine", art + "_shine");
            orb.Glyph = Layer("Glyph", "skill_unknown");
            orb.Lock = Layer("Lock", "lock");
            orb.Lock.color = Dim;
            orb.Upgrade = Layer("Upgrade", "badge_upgrade");
            orb.Learned = Layer("Learned", "badge_learned");
            if (withLabel)
            {
                orb.Label = NewText(name + "Label", 20, TextAnchor.MiddleCenter, DungeonHud.TextColor);
                orb.Label.resizeTextForBestFit = true;
                orb.Label.resizeTextMinSize = 14;
                orb.Label.resizeTextMaxSize = 20;
                orb.Label.horizontalOverflow = HorizontalWrapMode.Wrap;
                orb.Label.verticalOverflow = VerticalWrapMode.Truncate;
            }
            if (pressed != null)
            {
                var hit = UiFactory.CreateImage("Hit", orb.Root, null, Color.clear, raycast: true);
                hit.rectTransform.anchorMin = hit.rectTransform.anchorMax = hit.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                hit.rectTransform.sizeDelta = new Vector2(hitSize, hitSize); // Whatever the sphere's size on this screen.
                hit.gameObject.AddComponent<Hit>().Pressed = pressed;
            }
            return orb;
        }

        // ---- Layout (whenever the screen's size changes) ----

        void LateUpdate()
        {
            if (Model == null) return;
            if (content.rect.size != laidOutFor || !Mathf.Approximately(laidOutUnits, UiArtScaler.UnitsPerArtPixel) ||
                canvas != null && !Mathf.Approximately(scale, canvas.scaleFactor))
            {
                Layout();
                Refresh();
            }
            // A sphere that can be picked right now breathes.
            float pulse = 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 4f);
            foreach (var orb in orbs)
            {
                if (!orb.Pulses) continue;
                var color = orb.Glow.color;
                color.a = pulse;
                orb.Glow.color = color;
            }
        }

        float Snap(float units) => Mathf.Round(units * scale) / scale;

        /// <summary>Places a rect by its top-left corner, y down from the top of the screen, on whole screen pixels.</summary>
        void Box(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(Snap(x), -Snap(y));
            rect.sizeDelta = new Vector2(Snap(width), Snap(height));
        }

        /// <summary>Places a rect by its middle (art keeps the size the scaler gave it).</summary>
        void Point(RectTransform rect, float x, float y)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(Snap(x), -Snap(y));
        }

        float TierY(int tier) => top + 166f + (tier - 1) * ((bottom - top - 250f) / (ClassDefinition.MaxTier - 1));

        void Layout()
        {
            laidOutFor = content.rect.size;
            laidOutUnits = UiArtScaler.UnitsPerArtPixel;
            scale = canvas != null ? canvas.scaleFactor : 1f;
            float width = laidOutFor.x, height = laidOutFor.y, art = laidOutUnits;
            const float margin = 16f, gap = 16f;
            float block = Mathf.Min(width - 2f * margin, 2012f);
            left = (width - block) / 2f;
            leftWidth = 380f;
            infoWidth = Mathf.Clamp(0.3f * block, 480f, 600f);
            treeWidth = block - leftWidth - infoWidth - 2f * gap;
            treeLeft = left + leftWidth + gap;
            infoLeft = treeLeft + treeWidth + gap;
            top = 104f;
            bottom = height - margin;

            // The top bar.
            const float tabWidth = 300f;
            for (int i = 0; i < tabs.Count; i++)
            {
                var tab = tabs[i];
                Box(tab.Rect, left + i * (tabWidth + 12f), 16f, tabWidth, 76f);
                Box(tab.Name.rectTransform, 88f, 6f, tabWidth - 96f, 36f);
                Box(tab.Detail.rectTransform, 88f, 42f, tabWidth - 96f, 28f);
                Box(tab.Bar.rectTransform, 8f, 68f, tabWidth - 16f, 4f);
            }
            Box(closeButton.GetComponent<RectTransform>(), left + block - 170f, 18f, 170f, 72f);
            Box(pointsLabel.rectTransform, left + block - 500f, 22f, 300f, 28f);
            Box(pointsValue.rectTransform, left + block - 500f, 46f, 300f, 44f);

            // The left column.
            Box(classesHeader.rectTransform, left + 4f, top, leftWidth, 28f);
            float y = top + 34f;
            foreach (var row in classRows)
            {
                Box(row.Rect, left, y, leftWidth, 84f);
                Box(row.Bar.rectTransform, 6f, 10f, 6f, 64f);
                Box(row.Name.rectTransform, 26f, 8f, leftWidth - 36f, 36f);
                Box(row.Detail.rectTransform, 26f, 46f, leftWidth - 36f, 28f);
                y += 92f;
            }
            y += 14f;
            Box(loadoutHeader.rectTransform, left + 4f, y, leftWidth, 28f);
            y += 34f;
            float sphere = 44f * art;
            slotStep = (leftWidth - 20f - slots.Length * sphere) / (slots.Length - 1) + sphere;
            for (int i = 0; i < slots.Length; i++) Point(slots[i].Root, left + 10f + sphere / 2f + i * slotStep, y + 50f);
            Box(hint.rectTransform, left + 4f, y + 112f, leftWidth - 8f, bottom - 76f - 16f - (y + 112f));
            Box(unlearnButton.GetComponent<RectTransform>(), left, bottom - 76f, leftWidth, 76f);

            // The tree.
            Box(treePanel.rectTransform, treeLeft, top, treeWidth, bottom - top);
            Box(treeTitle.rectTransform, treeLeft, top + 22f, treeWidth, 46f);
            Box(treeTier.rectTransform, treeLeft, top + 70f, treeWidth, 28f);
            trackX = treeLeft + 92f;
            float innerLeft = treeLeft + 150f, innerRight = treeLeft + treeWidth - 30f;
            column = (innerRight - innerLeft) / ClassDefinition.PathCount;
            float line = 4f * art;
            for (int path = 0; path < ClassDefinition.PathCount; path++)
            {
                pathX[path] = innerLeft + column * (path + 0.5f);
                Box(pathNames[path].rectTransform, pathX[path] - column / 2f, top + 106f, column, 30f);
                for (int row = 0; row < SkillTreeModel.Rows; row++)
                {
                    int tier = SkillTreeModel.TierOfRow(row);
                    float from = row == 0 ? top + 142f : TierY(tier - ClassDefinition.MilestoneEvery) + 82f;
                    Box(pathLines[path, row].rectTransform, pathX[path] - line / 2f, from, line, TierY(tier) - 40f - from);
                }
            }
            Box(trackLine.rectTransform, trackX - line / 2f, TierY(1), line, TierY(ClassDefinition.MaxTier) - TierY(1));
            for (int tier = 1; tier <= ClassDefinition.MaxTier; tier++) Point(marks[tier - 1].rectTransform, trackX, TierY(tier));
            for (int row = 0; row < SkillTreeModel.Rows; row++)
            {
                float rowY = TierY(SkillTreeModel.TierOfRow(row));
                Box(rowLines[row].rectTransform, trackX, rowY - line / 2f, pathX[ClassDefinition.PathCount - 1] - trackX, line);
                Box(tierNumbers[row].rectTransform, trackX - 96f, rowY - 17f, 70f, 34f);
                for (int path = 0; path < ClassDefinition.PathCount; path++)
                {
                    var orb = orbs[row, path];
                    Point(orb.Root, pathX[path], rowY);
                    Box(orb.Label.rectTransform, pathX[path] - (column - 12f) / 2f, rowY + 48f, column - 12f, 30f);
                    PlaceBadges(orb, sphere);
                }
                Box(rowNotes[row].rectTransform, pathX[0], rowY + 50f, pathX[ClassDefinition.PathCount - 1] - pathX[0], 28f);
            }
            foreach (var slot in slots) PlaceBadges(slot, sphere);

            // The info panel.
            Box(infoPanel.rectTransform, infoLeft, top, infoWidth, bottom - top);
            Point(infoOrb.Root, infoLeft + 92f, top + 96f);
            PlaceBadges(infoOrb, 56f * art);
            Box(infoTitle.rectTransform, infoLeft + 168f, top + 40f, infoWidth - 190f, 48f);
            Box(infoKind.rectTransform, infoLeft + 168f, top + 92f, infoWidth - 190f, 80f);
            boxTop = top + 190f;
            float buttonY = bottom - 110f;
            boxHeight = buttonY - 190f - boxTop;
            Box(infoBox.rectTransform, infoLeft + 24f, boxTop, infoWidth - 48f, boxHeight);
            for (int i = 0; i < ChoiceRows; i++)
            {
                var row = choiceRows[i];
                float rowWidth = infoWidth - 72f;
                Box(row.Rect, infoLeft + 36f, boxTop + boxHeight - 12f - (ChoiceRows - i) * 58f, rowWidth, 54f);
                row.Icon.rectTransform.anchoredPosition = new Vector2(Snap(34f), 0f);
                Box(row.Bar.rectTransform, 4f, 8f, 4f, 38f);
                Box(row.Name.rectTransform, 68f, 0f, rowWidth * 0.5f, 54f);
                Box(row.Detail.rectTransform, 68f + rowWidth * 0.5f, 0f, rowWidth * 0.5f - 80f, 54f);
            }
            Box(moreChoices.rectTransform, infoLeft + 36f, boxTop + boxHeight + 4f, infoWidth - 72f, 24f);
            Box(infoStatus.rectTransform, infoLeft + 28f, boxTop + boxHeight + 18f, infoWidth - 56f, buttonY - 10f - (boxTop + boxHeight + 18f));
            Box(actionButton.GetComponent<RectTransform>(), infoLeft + 24f, buttonY, infoWidth - 48f, 80f);

            // The question: in the middle of the screen.
            UiFactory.Stretch(confirmVeil.rectTransform);
            Box(confirmPanel.rectTransform, (width - 940f) / 2f, (height - 400f) / 2f, 940f, 400f);
        }

        /// <summary>The marks on a sphere's rim: the upgrade arrow up and to the right, the tick down and to the right.</summary>
        void PlaceBadges(Orb orb, float sphere)
        {
            float reach = Snap(sphere * 0.4f);
            orb.Upgrade.rectTransform.anchoredPosition = new Vector2(reach, reach);
            orb.Learned.rectTransform.anchoredPosition = new Vector2(reach, -reach);
        }

        // ---- Drawing the model ----

        void Refresh()
        {
            var model = Model;
            var hero = model.Hero;
            var focus = model.Focus;
            var shown = model.Class;
            int tier = hero.TierOf(shown);

            for (int i = 0; i < tabs.Count; i++)
            {
                var member = model.Party[i];
                bool chosen = i == model.HeroIndex;
                var tab = tabs[i];
                tab.Back.color = chosen ? Color.white : Unchosen;
                tab.Bar.enabled = chosen;
                tab.Name.text = member.Definition.Name;
                tab.Name.color = chosen ? Gold : DungeonHud.TextColor;
                tab.Detail.text = member.PointsFree > 0 ? $"Lv {member.Level}   <color=#ffd866>{member.PointsFree} free</color>" : $"Lv {member.Level}";
                tab.Detail.supportRichText = true;
                tab.Icon.sprite = HeroLooks.Sprites(member.Definition.Id).Portrait;
                tab.Icon.enabled = tab.Icon.sprite != null;
                UiArtScaler.Size(tab.Icon);
            }
            pointsLabel.text = "Points left";
            pointsValue.text = hero.PointsFree.ToString();
            pointsValue.color = hero.PointsFree > 0 ? Gold : Dim;

            for (int i = 0; i < classRows.Count; i++)
            {
                var definition = model.Classes[i];
                bool chosen = i == model.ClassIndex;
                var row = classRows[i];
                int reached = hero.TierOf(definition);
                row.Back.color = chosen ? Color.white : Unchosen;
                row.Bar.enabled = chosen;
                row.Name.text = definition.Name;
                row.Name.color = chosen ? Gold : DungeonHud.TextColor;
                row.Detail.text = reached > 0 ? $"Tier {reached} of {ClassDefinition.MaxTier}" : "Not learned";
            }

            for (int i = 0; i < slots.Length; i++)
            {
                var skill = model.SkillIn(i);
                Show(slots[i], skill != null ? (i == SkillTreeModel.UltimateSlot ? UltimateTint : SlotTint) : LockedTint, model.IconOf(skill),
                    glow: false, ring: false, shine: skill != null, glyph: Color.white);
            }
            hint.text = HintFor(model);
            unlearnButton.SetLabel($"Unlearn {shown.Name}");
            unlearnButton.Interactable = !model.ReadOnly && tier > 0;

            // The tree.
            treeTitle.text = shown.Name;
            treeTier.text = tier > 0 ? $"Tier {tier} of {ClassDefinition.MaxTier}" : "Not learned";
            bool written = shown.HighestOpenTier >= ClassDefinition.MilestoneEvery;
            for (int path = 0; path < ClassDefinition.PathCount; path++)
            {
                pathNames[path].text = shown.Paths[path];
                pathNames[path].color = written ? PathColors[path] : Scaled(PathColors[path], 0.6f);
                int deepest = 0;
                for (int row = 0; row < SkillTreeModel.Rows; row++)
                    if (model.StateOf(row, path) == OptionState.Picked) deepest = SkillTreeModel.TierOfRow(row);
                for (int row = 0; row < SkillTreeModel.Rows; row++)
                    pathLines[path, row].color = SkillTreeModel.TierOfRow(row) <= deepest ? PathColors[path] : Scaled(PathColors[path], 0.38f);
            }
            for (int t = 1; t <= ClassDefinition.MaxTier; t++)
            {
                var state = model.StateOf(t);
                marks[t - 1].color = state == TierState.Reached ? Gold : state == TierState.Next ? Color.white : TrackAhead;
            }
            for (int row = 0; row < SkillTreeModel.Rows; row++)
            {
                int rowTier = SkillTreeModel.TierOfRow(row);
                tierNumbers[row].color = rowTier <= tier ? Gold : Dim;
                bool locked = model.OptionAt(row, 0) == null;
                rowNotes[row].text = locked ? "Not written yet" : "";
                for (int path = 0; path < ClassDefinition.PathCount; path++)
                {
                    var orb = orbs[row, path];
                    var option = model.OptionAt(row, path);
                    var colour = PathColors[path];
                    orb.Label.text = option != null ? option.Name : "";
                    orb.Pulses = false;
                    switch (model.StateOf(row, path))
                    {
                        case OptionState.Locked:
                            Show(orb, LockedTint, null, glow: false, ring: false, shine: false, glyph: Color.white, locked: true);
                            break;
                        case OptionState.Picked:
                            Show(orb, colour, model.IconOf(option), glow: true, ring: true, shine: true, glyph: Color.white, learned: true);
                            orb.Label.color = DungeonHud.TextColor;
                            break;
                        case OptionState.Passed:
                            Show(orb, PassedTint, model.IconOf(option), glow: false, ring: false, shine: false, glyph: Hex(0x8a91a3));
                            orb.Label.color = Faint;
                            break;
                        case OptionState.Open:
                            Show(orb, colour, model.IconOf(option), glow: true, ring: false, shine: true, glyph: Color.white, upgrade: IsUpgrade(option));
                            orb.Label.color = DungeonHud.TextColor;
                            orb.Pulses = true;
                            break;
                        default:
                            Show(orb, Scaled(colour, 0.5f), model.IconOf(option), glow: false, ring: false, shine: false, glyph: Hex(0xaab1c2), upgrade: IsUpgrade(option));
                            orb.Label.color = Dim;
                            break;
                    }
                }
            }

            RefreshInfo(model);
            RefreshCursor(model);

            bool asking = model.Confirming;
            confirmVeil.gameObject.SetActive(asking);
            confirmPanel.gameObject.SetActive(asking);
            if (asking)
            {
                confirmText.text = model.Question;
                yesButton.SetHighlight(focus == TreeFocus.Confirm(true));
                cancelButton.SetHighlight(focus == TreeFocus.Confirm(false));
            }
        }

        void RefreshInfo(SkillTreeModel model)
        {
            var info = model.Info;
            var focus = model.Focus;
            infoTitle.text = info.Title;
            infoKind.text = info.Kind;
            var tint = info.Path >= 0 ? PathColors[info.Path] : focus.Zone == TreeZone.Loadout || focus.Zone == TreeZone.Choices ? SlotTint : LockedTint;
            Show(infoOrb, tint, info.Icon, glow: info.Path >= 0, ring: false, shine: info.Icon != null, glyph: Color.white, locked: info.Locked);

            // The skills that could go in the slot: a window of rows at the foot of the box, following the cursor.
            var choices = model.Choices;
            if (focus.Zone == TreeZone.Choices)
                choicesFrom = Mathf.Clamp(choicesFrom, Mathf.Max(0, focus.Index - ChoiceRows + 1), focus.Index);
            choicesFrom = Mathf.Clamp(choicesFrom, 0, Mathf.Max(0, choices.Count - 1));
            int shownRows = Mathf.Min(ChoiceRows, choices.Count - choicesFrom);
            for (int i = 0; i < ChoiceRows; i++)
            {
                var row = choiceRows[i];
                bool used = i < shownRows;
                row.Rect.gameObject.SetActive(used);
                if (!used) continue;
                var choice = choices[choicesFrom + i];
                bool marked = focus == TreeFocus.Choice(choicesFrom + i);
                bool fits = choice.Check == EquipCheck.Ok;
                // The rows hang from the box's foot: with fewer than four, they close up at the bottom.
                row.Rect.anchoredPosition = new Vector2(row.Rect.anchoredPosition.x, -Snap(boxTop + boxHeight - 12f - (shownRows - i) * 58f));
                row.Back.color = marked ? Color.white : Unchosen;
                row.Bar.enabled = marked;
                row.Name.text = choice.Skill.Name;
                row.Name.color = fits ? DungeonHud.TextColor : Faint;
                row.Detail.text = !fits ? (choice.Check == EquipCheck.WrongWeapon ? "other weapon" : "second Quick")
                    : choice.From >= 0 ? $"in slot {choice.From + 1}" : "";
                SetArt(row.Icon, model.IconOf(choice.Skill));
                row.Icon.color = fits ? Color.white : Faint;
            }
            moreChoices.text = choices.Count > ChoiceRows ? $"{choicesFrom + 1}-{choicesFrom + shownRows} of {choices.Count}  (more)" : "";
            moreChoices.gameObject.SetActive(choices.Count > ChoiceRows);

            float bodyHeight = boxHeight - 36f - (shownRows > 0 ? shownRows * 58f + 16f : 0f);
            Box(infoBody.rectTransform, infoLeft + 44f, boxTop + 18f, infoWidth - 88f, bodyHeight);
            infoBody.text = string.IsNullOrEmpty(info.Note) ? info.Body : $"{info.Body}\n<size=10> </size>\n<size=20><color=#{DimHex}>{info.Note}</color></size>";

            // What just happened comes first, then why the button can or can't be pressed.
            string status = info.Enabled ? $"<color=#ffd866>{info.Status}</color>" : info.Status;
            infoStatus.text = string.IsNullOrEmpty(model.Notice) || model.Notice == info.Status ? status
                : $"<color=#f2f2f7>{model.Notice}</color>\n{status}";
            infoStatus.color = Dim;

            bool hasButton = info.Action != null;
            actionButton.gameObject.SetActive(hasButton);
            if (!hasButton) return;
            actionButton.SetArt(info.Enabled ? "button_gold" : "button_dark");
            actionButton.SetLabel(inputMode == InputMode.Gamepad ? $"{info.Action} (A)" : inputMode == InputMode.Keyboard ? $"{info.Action} (Enter)" : info.Action);
            actionButton.Interactable = info.Enabled;
        }

        /// <summary>The gold box around what the cursor is on, like the reference's.</summary>
        void RefreshCursor(SkillTreeModel model)
        {
            var focus = model.Focus;
            float sphere = 44f * laidOutUnits;
            cursor.gameObject.SetActive(focus.Zone != TreeZone.Confirm);
            switch (focus.Zone)
            {
                case TreeZone.Classes:
                    Around(classRows[focus.Index].Rect, 6f);
                    break;
                case TreeZone.Loadout:
                    var slot = slots[focus.Index].Root.anchoredPosition;
                    Box(cursor.rectTransform, slot.x - sphere / 2f - 8f, -slot.y - sphere / 2f - 8f, sphere + 16f, sphere + 16f);
                    break;
                case TreeZone.Unlearn:
                    Around(unlearnButton.GetComponent<RectTransform>(), 6f);
                    break;
                case TreeZone.Options:
                    float rowY = TierY(SkillTreeModel.TierOfRow(focus.Index));
                    if (model.OptionAt(focus.Index, focus.Path) == null)
                    {
                        // A row that isn't written yet is one thing: the box takes all of it.
                        Box(cursor.rectTransform, pathX[0] - 76f, rowY - 58f, pathX[ClassDefinition.PathCount - 1] - pathX[0] + 152f, 138f);
                        break;
                    }
                    float half = Mathf.Min(column / 2f - 4f, 128f);
                    Box(cursor.rectTransform, pathX[focus.Path] - half, rowY - 58f, 2f * half, 138f);
                    break;
                case TreeZone.Choices:
                    int shown = focus.Index - choicesFrom;
                    if (shown >= 0 && shown < ChoiceRows) Around(choiceRows[shown].Rect, 4f);
                    break;
            }
        }

        void Around(RectTransform rect, float pad)
        {
            var position = rect.anchoredPosition;
            Box(cursor.rectTransform, position.x - pad, -position.y - pad, rect.sizeDelta.x + 2f * pad, rect.sizeDelta.y + 2f * pad);
        }

        /// <summary>One sphere in one state: its colour, what is on it, the light around it.</summary>
        void Show(Orb orb, Color tint, string icon, bool glow, bool ring, bool shine, Color glyph, bool locked = false, bool upgrade = false, bool learned = false)
        {
            orb.Ball.color = tint;
            orb.Glow.enabled = glow;
            orb.Glow.color = tint;
            orb.Ring.enabled = ring;
            orb.Shine.enabled = shine;
            orb.Lock.enabled = locked;
            orb.Glyph.enabled = !locked && icon != null;
            if (orb.Glyph.enabled)
            {
                SetArt(orb.Glyph, icon);
                orb.Glyph.color = glyph;
            }
            orb.Upgrade.enabled = upgrade;
            orb.Learned.enabled = learned;
        }

        /// <summary>A skill's icon on an image; a skill without one drawn yet gets the plain diamond.</summary>
        static void SetArt(Image image, string icon)
        {
            string art = icon != null && UiFactory.HasArt("skill_" + icon) ? "skill_" + icon : "skill_unknown";
            image.sprite = UiFactory.Art(art);
            UiArtScaler.Size(image);
        }

        static bool IsUpgrade(ClassOption option) => option != null && option.Teaches == null && (option.Upgrades != null || option.AttackOf != WeaponFamily.None);

        string HintFor(SkillTreeModel model)
        {
            string how = inputMode == InputMode.Gamepad ? "D-pad or stick: move.  A: press.\nL1 and R1: another hero.  B: back."
                : inputMode == InputMode.Keyboard ? "Arrows or WASD: move.  Enter: press.\nQ and E: another hero.  Esc: back."
                : "Tap a class, a sphere or a slot, then the button on the right.";
            return model.ReadOnly ? "Looking only: a build changes between runs.\n" + how : how;
        }

        static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        static Color Scaled(Color color, float factor) => new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
    }
}

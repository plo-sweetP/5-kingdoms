using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>How one of a milestone row's three spheres shows in the tree.</summary>
    public enum OptionState
    {
        /// <summary>The row's options aren't written yet.</summary>
        Locked,

        /// <summary>The hero picked this option.</summary>
        Picked,

        /// <summary>The hero picked another option of this row.</summary>
        Passed,

        /// <summary>The row is the class's next tier: one of its options can be picked.</summary>
        Open,

        /// <summary>Further down: the tiers before it come first.</summary>
        Ahead,
    }

    /// <summary>How a tier's mark on the track shows.</summary>
    public enum TierState
    {
        Reached,

        /// <summary>The tier a point buys next.</summary>
        Next,
        Ahead,

        /// <summary>A milestone that isn't written yet, and every tier after it.</summary>
        Locked,
    }

    /// <summary>The parts of the skill-tree screen a cursor can stand on.</summary>
    public enum TreeZone
    {
        /// <summary>
        /// The hero's list on the left: Index is the place in it, first the classes the hero has (Peter, 2026-10-10:
        /// "only show what class you have"), then the "+" entry that adds another, while there is one to add.
        /// </summary>
        Classes,

        /// <summary>The classes the hero doesn't have yet, listed by the "+" entry; Index is the place in that list.</summary>
        Adding,

        /// <summary>The loadout; Index is the slot (the three skills, then the ultimate).</summary>
        Loadout,

        /// <summary>The button that unlearns the class shown.</summary>
        Unlearn,

        /// <summary>The tree's spheres; Index is the row (0 is tier 5), Path the column.</summary>
        Options,

        /// <summary>The skills that could go in the loadout slot being changed; Index is the place in the list.</summary>
        Choices,

        /// <summary>The question before unlearning; Index 0 is yes, 1 is cancel.</summary>
        Confirm,
    }

    /// <summary>Where the cursor of the skill-tree screen stands.</summary>
    public readonly struct TreeFocus : IEquatable<TreeFocus>
    {
        public readonly TreeZone Zone;
        public readonly int Index;
        public readonly int Path;

        public TreeFocus(TreeZone zone, int index = 0, int path = 0)
        {
            Zone = zone;
            Index = index;
            Path = path;
        }

        public static TreeFocus Class(int index) => new TreeFocus(TreeZone.Classes, index);
        public static TreeFocus Add(int index) => new TreeFocus(TreeZone.Adding, index);
        public static TreeFocus Slot(int slot) => new TreeFocus(TreeZone.Loadout, slot);
        public static TreeFocus Option(int row, int path) => new TreeFocus(TreeZone.Options, row, path);
        public static TreeFocus Choice(int index) => new TreeFocus(TreeZone.Choices, index);
        public static TreeFocus Unlearn => new TreeFocus(TreeZone.Unlearn);
        public static TreeFocus Confirm(bool yes) => new TreeFocus(TreeZone.Confirm, yes ? 0 : 1);

        public bool Equals(TreeFocus other) => Zone == other.Zone && Index == other.Index && Path == other.Path;
        public override bool Equals(object obj) => obj is TreeFocus other && Equals(other);
        public override int GetHashCode() => ((int)Zone * 397 + Index) * 397 + Path;
        public static bool operator ==(TreeFocus a, TreeFocus b) => a.Equals(b);
        public static bool operator !=(TreeFocus a, TreeFocus b) => !a.Equals(b);
        public override string ToString() => Zone == TreeZone.Options ? $"Options({Index}, {Path})" : $"{Zone}({Index})";
    }

    /// <summary>What the info panel says about the thing under the cursor, and what its button does.</summary>
    public sealed class TreeInfo
    {
        public string Title { get; internal set; }

        /// <summary>One or two short lines under the title: where it belongs, what kind of thing it is.</summary>
        public string Kind { get; internal set; } = "";

        /// <summary>What it does.</summary>
        public string Body { get; internal set; } = "";

        /// <summary>More, in a quieter voice: the skill as the hero has it now, what every tier of the class gives.</summary>
        public string Note { get; internal set; } = "";

        /// <summary>Why the button can or can't be pressed, or what pressing it costs.</summary>
        public string Status { get; internal set; } = "";

        /// <summary>The button's label; null for no button.</summary>
        public string Action { get; internal set; }
        public bool Enabled { get; internal set; }

        /// <summary>The icon on the panel's sphere ("lunge", "attack_hunter_bow"), or null for none.</summary>
        public string Icon { get; internal set; }

        /// <summary>The path whose colour the sphere takes (0-2), or -1.</summary>
        public int Path { get; internal set; } = -1;

        /// <summary>The sphere shows a lock: a row that isn't written yet.</summary>
        public bool Locked { get; internal set; }

        /// <summary>The button only moves the cursor (into a list to look at): it works during a run too.</summary>
        internal bool Looks { get; set; }
    }

    /// <summary>A skill that could go in the loadout slot being changed.</summary>
    public sealed class TreeChoice
    {
        internal TreeChoice(SkillDefinition skill, EquipCheck check, int from)
        {
            Skill = skill;
            Check = check;
            From = from;
        }

        public SkillDefinition Skill { get; }
        public EquipCheck Check { get; }

        /// <summary>The loadout slot it sits in now (it would trade places), or -1.</summary>
        public int From { get; }
    }

    /// <summary>
    /// The skill-tree screen without its pictures (PROGRESSION.md, "Building 1g", step 6): which hero and class are
    /// shown, where the cursor stands, what the info panel says about it, and what pressing does. Raising a tier,
    /// picking an option, changing the loadout and unlearning all go through <see cref="HeroProgress"/>, so the screen
    /// can do nothing the rules don't allow. Touch sets the cursor (<see cref="Tap"/>) and presses the info panel's
    /// button (<see cref="Activate"/>); keys and a controller move the cursor (<see cref="Move"/>) and press on it.
    /// <para>
    /// During a run the screen is read-only (<see cref="ReadOnly"/>): a build changes between runs only.
    /// </para>
    /// </summary>
    public sealed class SkillTreeModel
    {
        /// <summary>The tree's rows: tiers 5, 10, 15, 20 and 25.</summary>
        public const int Rows = ClassDefinition.MaxTier / ClassDefinition.MilestoneEvery;

        /// <summary>The loadout as the screen shows it: the skills, then the ultimate.</summary>
        public const int Slots = HeroProgress.LoadoutSkills + 1;
        public const int UltimateSlot = HeroProgress.LoadoutSkills;

        /// <summary>The weapon attack's skill id (<see cref="HeroKit.WeaponAttackOf"/>): its icon goes by the weapon.</summary>
        const string WeaponAttackId = "weapon_attack";

        readonly List<TreeChoice> choices = new List<TreeChoice>();
        int choiceSlot = -1;
        TreeFocus beforeConfirm;

        /// <param name="classes">The classes listed; the catalog's by default (tests bring their own).</param>
        public SkillTreeModel(IReadOnlyList<HeroProgress> party, int hero = 0, bool readOnly = false, IReadOnlyList<ClassDefinition> classes = null)
        {
            if (party == null || party.Count == 0) throw new ArgumentException("The skill tree needs a hero.", nameof(party));
            Party = party;
            Classes = classes ?? ClassCatalog.All;
            if (Classes.Count == 0) throw new ArgumentException("The skill tree needs a class.", nameof(classes));
            ReadOnly = readOnly;
            SelectHero(hero);
        }

        public IReadOnlyList<HeroProgress> Party { get; }
        public IReadOnlyList<ClassDefinition> Classes { get; }

        /// <summary>Nothing can be changed: the screen was opened during a run.</summary>
        public bool ReadOnly { get; }

        public int HeroIndex { get; private set; }
        public HeroProgress Hero => Party[HeroIndex];

        /// <summary>The class whose tree is shown: one of the hero's, or one from the "+" list being looked at.</summary>
        public int ClassIndex { get; private set; }
        public ClassDefinition Class => Classes[ClassIndex];

        /// <summary>The classes the hero has a tier of, in the order learned: the list on the left.</summary>
        public IReadOnlyList<ClassDefinition> Learned
        {
            get
            {
                var learned = new List<ClassDefinition>();
                foreach (var progress in Hero.Classes)
                    if (progress.Tier > 0 && IndexOf(progress.Class) >= 0) learned.Add(progress.Class);
                return learned;
            }
        }

        /// <summary>The classes the hero doesn't have yet, in the catalog's order: what the "+" entry lists.</summary>
        public IReadOnlyList<ClassDefinition> Others
        {
            get
            {
                var others = new List<ClassDefinition>();
                foreach (var definition in Classes)
                    if (Hero.TierOf(definition) == 0) others.Add(definition);
                return others;
            }
        }

        /// <summary>The place of the "+" entry in the list on the left, or -1 when the hero has every class.</summary>
        public int AddIndex => Others.Count > 0 ? Learned.Count : -1;

        /// <summary>How many entries the list on the left has: the hero's classes and, while there is one to add, the "+".</summary>
        public int ListCount => Learned.Count + (Others.Count > 0 ? 1 : 0);

        public TreeFocus Focus { get; private set; }

        /// <summary>The question before unlearning is up; the cursor is on its answers.</summary>
        public bool Confirming => Focus.Zone == TreeZone.Confirm;

        /// <summary>The question being asked, while <see cref="Confirming"/>.</summary>
        public string Question { get; private set; }

        /// <summary>What just happened, or why nothing did ("Uzuki's Archer is tier 3 now.", "No points left."); null at first.</summary>
        public string Notice { get; private set; }

        /// <summary>A hero's build changed (a tier, a pick, the loadout, a class unlearned): the screen's owner saves it.</summary>
        public event Action Changed;

        public static int TierOfRow(int row) => (row + 1) * ClassDefinition.MilestoneEvery;

        // ---- What the screen draws ----

        public TierState StateOf(int tier)
        {
            int reached = Hero.TierOf(Class);
            if (tier <= reached) return TierState.Reached;
            if (tier > Class.HighestOpenTier) return TierState.Locked;
            return tier == reached + 1 ? TierState.Next : TierState.Ahead;
        }

        public OptionState StateOf(int row, int path)
        {
            int tier = TierOfRow(row);
            var milestone = Class.MilestoneAt(tier);
            if (milestone == null) return OptionState.Locked;
            var picked = Hero.PickAt(Class, tier);
            if (picked != null) return picked == milestone.Options[path] ? OptionState.Picked : OptionState.Passed;
            return tier == Hero.TierOf(Class) + 1 ? OptionState.Open : OptionState.Ahead;
        }

        /// <summary>The option on a sphere, or null where the row isn't written yet.</summary>
        public ClassOption OptionAt(int row, int path) => Class.MilestoneAt(TierOfRow(row))?.Options[path];

        /// <summary>The skill in a loadout slot as the hero has it (slot 3 is the ultimate), or null for an empty one.</summary>
        public SkillDefinition SkillIn(int slot)
        {
            if (slot == UltimateSlot) return Find(Hero.KnownUltimates, Hero.UltimateId);
            return slot >= 0 && slot < HeroProgress.LoadoutSkills ? Find(Hero.KnownSkills, Hero.LoadoutIds[slot]) : null;
        }

        /// <summary>
        /// The skills that could go in the slot under the cursor instead of the one there, each with whether the rules
        /// allow it. Empty unless the cursor is on the loadout or in this list.
        /// </summary>
        public IReadOnlyList<TreeChoice> Choices => choices;

        /// <summary>The icon of a skill of this hero: its id, or for the weapon attack the weapon's ("attack_hunter_bow").</summary>
        /// <summary>
        /// Whether the skill in a loadout slot is always on (<see cref="SkillDefinition.AlwaysOn"/>: a passive that
        /// keeps a slot): the screen draws its sphere lit, and its info says "Always on".
        /// </summary>
        public bool IsAlwaysOn(int slot) => SkillIn(slot)?.AlwaysOn == true;

        public string IconOf(SkillDefinition skill) =>
            skill == null ? null : skill.Id == WeaponAttackId ? "attack_" + (Hero.Definition.Weapon?.Id ?? "none") : skill.Id;

        /// <summary>The icon on an option's sphere: the skill it teaches or upgrades, the weapon attack it changes; null for a passive.</summary>
        public string IconOf(ClassOption option) =>
            option == null ? null
            : option.AttackOf != WeaponFamily.None ? IconOf(Hero.Kit.WeaponAttack)
            : option.Skill?.Id;

        // ---- The cursor ----

        /// <summary>Another hero's tree (the index wraps around the party). The cursor goes to that hero's own class.</summary>
        public void SelectHero(int index)
        {
            HeroIndex = (index % Party.Count + Party.Count) % Party.Count;
            // Its own class if it still has it, else the first it has, else its own again, to look at.
            var learned = Learned;
            int own = IndexOf(Hero.Definition.StartingClass);
            var shown = own >= 0 && Hero.TierOf(Classes[own]) > 0 ? Classes[own] : learned.Count > 0 ? learned[0] : Classes[Math.Max(0, own)];
            ClassIndex = IndexOf(shown);
            Question = null;
            Notice = null;
            SetFocus(Home());
        }

        /// <summary>The cursor onto a class wherever it stands: in the hero's list, or in the "+" list of the ones it doesn't have.</summary>
        public void Show(ClassDefinition definition)
        {
            if (Confirming || IndexOf(definition) < 0) return;
            ClassIndex = IndexOf(definition);
            SetFocus(Home());
        }

        /// <summary>Where the shown class stands on the left: its place in the hero's list, or in the "+" list.</summary>
        TreeFocus Home()
        {
            int place = IndexIn(Learned, Class);
            return place >= 0 ? TreeFocus.Class(place) : TreeFocus.Add(Math.Max(0, IndexIn(Others, Class)));
        }

        static int IndexIn(IReadOnlyList<ClassDefinition> list, ClassDefinition definition)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == definition) return i;
            return -1;
        }

        /// <summary>A touch or a click on something: the cursor goes there. Nothing is pressed.</summary>
        public void Tap(TreeFocus focus)
        {
            if (Confirming && focus.Zone != TreeZone.Confirm) return; // The question wants its answer first.
            SetFocus(Clamp(focus));
        }

        /// <summary>Keys or a controller: one step left or right (<paramref name="dx"/>), up or down (<paramref name="dy"/>, down is 1).</summary>
        public void Move(int dx, int dy)
        {
            var focus = Focus;
            switch (focus.Zone)
            {
                case TreeZone.Classes:
                    if (dx > 0) focus = TreeFocus.Option(RowInReach(), 0);
                    else if (dy > 0 && focus.Index == ListCount - 1) focus = TreeFocus.Slot(0);
                    else if (dy != 0) focus = TreeFocus.Class(Math.Max(0, focus.Index + dy));
                    break;
                case TreeZone.Adding:
                    if (dx < 0) focus = TreeFocus.Class(AddIndex);
                    else if (dx > 0) focus = TreeFocus.Option(RowInReach(), 0);
                    else if (dy != 0) focus = TreeFocus.Add(focus.Index + dy);
                    break;
                case TreeZone.Loadout:
                    if (dy < 0) focus = TreeFocus.Class(ListCount - 1);
                    else if (dy > 0) focus = TreeFocus.Unlearn;
                    else if (dx > 0 && focus.Index == Slots - 1) focus = TreeFocus.Option(2, 0);
                    else if (dx != 0) focus = TreeFocus.Slot(Math.Max(0, focus.Index + dx));
                    break;
                case TreeZone.Unlearn:
                    if (dy < 0) focus = TreeFocus.Slot(0);
                    else if (dx > 0) focus = TreeFocus.Option(Rows - 1, 0);
                    break;
                case TreeZone.Options:
                    if (dx < 0 && focus.Path == 0)
                        focus = focus.Index <= 1 ? Home() : focus.Index == Rows - 1 ? TreeFocus.Unlearn : TreeFocus.Slot(Slots - 1);
                    else
                        focus = TreeFocus.Option(focus.Index + dy, focus.Path + dx);
                    break;
                case TreeZone.Choices:
                    if (dx < 0) focus = TreeFocus.Slot(choiceSlot);
                    else if (dy != 0) focus = TreeFocus.Choice(focus.Index + dy);
                    break;
                case TreeZone.Confirm:
                    if (dx != 0) focus = TreeFocus.Confirm(focus.Index != 0);
                    break;
            }
            SetFocus(Clamp(focus));
        }

        /// <summary>
        /// Esc, B, or a step back: out of the question, out of the list of choices; otherwise the screen closes, which
        /// is the owner's to do. Returns false when there was nothing left to step out of.
        /// </summary>
        public bool Back()
        {
            if (Confirming)
            {
                Question = null;
                SetFocus(beforeConfirm);
                return true;
            }
            if (Focus.Zone == TreeZone.Choices)
            {
                SetFocus(TreeFocus.Slot(choiceSlot));
                return true;
            }
            if (Focus.Zone == TreeZone.Adding)
            {
                SetFocus(TreeFocus.Class(AddIndex));
                return true;
            }
            return false;
        }

        // ---- Pressing ----

        /// <summary>Presses what the cursor stands on: the info panel's button for it.</summary>
        public void Activate()
        {
            var focus = Focus;
            if (focus.Zone == TreeZone.Confirm)
            {
                bool yes = focus.Index == 0;
                var definition = Class;
                Question = null;
                SetFocus(beforeConfirm);
                if (!yes) return;
                int returned = Hero.Unlearn(definition);
                Notice = $"{Hero.Definition.Name} unlearned the {definition.Name}: {Points(returned)} back.";
                Rebuild();
                Changed?.Invoke();
                return;
            }

            var info = Info;
            if (!info.Enabled)
            {
                if (!string.IsNullOrEmpty(info.Status)) Notice = info.Status;
                return;
            }
            switch (focus.Zone)
            {
                case TreeZone.Classes:
                    if (focus.Index == AddIndex)
                    {
                        SetFocus(TreeFocus.Add(0)); // The "+": into the list of the classes the hero doesn't have.
                        return;
                    }
                    if (Hero.CheckRaise(Class) == RaiseCheck.NeedsPick)
                    {
                        SetFocus(TreeFocus.Option(RowInReach(), 0)); // "Choose at tier 5": over to the row.
                        return;
                    }
                    if (!Hero.Raise(Class)) return;
                    Notice = $"{Hero.Definition.Name}'s {Class.Name} is tier {Hero.TierOf(Class)} now.";
                    break;
                case TreeZone.Adding:
                    if (!Hero.Raise(Class)) return;
                    Notice = $"{Hero.Definition.Name} learned the {Class.Name}.";
                    SetFocus(Home()); // It is one of the hero's classes now: the cursor follows it into the list.
                    break;
                case TreeZone.Options:
                    var option = OptionAt(focus.Index, focus.Path);
                    if (!Hero.Raise(Class, option)) return;
                    Notice = $"{Hero.Definition.Name} took {option.Name}: the {Class.Name} is tier {Hero.TierOf(Class)} now.";
                    break;
                case TreeZone.Loadout:
                    SetFocus(TreeFocus.Choice(0));
                    return;
                case TreeZone.Choices:
                    var choice = choices[focus.Index];
                    bool done = choiceSlot == UltimateSlot ? Hero.EquipUltimate(choice.Skill) : Hero.Equip(choiceSlot, choice.Skill);
                    if (!done) return;
                    Notice = choiceSlot == UltimateSlot ? $"{choice.Skill.Name} is {Hero.Definition.Name}'s ultimate now."
                        : $"{choice.Skill.Name} is in slot {choiceSlot + 1} now.";
                    SetFocus(TreeFocus.Slot(choiceSlot));
                    break;
                case TreeZone.Unlearn:
                    beforeConfirm = focus;
                    int tier = Hero.TierOf(Class);
                    Question = $"Unlearn the {Class.Name}?\n{Hero.Definition.Name} gets {Points(tier)} back" +
                               (HasPicks(Class) ? " and forgets what its milestones taught." : ".");
                    SetFocus(TreeFocus.Confirm(false)); // Cancel is the marked answer.
                    return;
            }
            Rebuild();
            Changed?.Invoke();
        }

        // ---- The info panel ----

        /// <summary>What the info panel says about the thing under the cursor.</summary>
        public TreeInfo Info
        {
            get
            {
                TreeInfo info;
                switch (Focus.Zone)
                {
                    case TreeZone.Options: info = OptionInfo(Focus.Index, Focus.Path); break;
                    case TreeZone.Loadout: info = SlotInfo(Focus.Index); break;
                    case TreeZone.Choices: info = ChoiceInfo(choices[Focus.Index]); break;
                    case TreeZone.Unlearn: info = UnlearnInfo(); break;
                    case TreeZone.Confirm: info = UnlearnInfo(); break;
                    case TreeZone.Classes: info = Focus.Index == AddIndex ? AddInfo() : ClassInfo(); break;
                    default: info = ClassInfo(); break;
                }
                if (ReadOnly && info.Action != null && !info.Looks)
                {
                    info.Enabled = false;
                    info.Status = "A build changes between runs only: finish or leave this run first.";
                }
                return info;
            }
        }

        /// <summary>The "+" entry: what adding a class means, with the list of the ones the hero doesn't have.</summary>
        TreeInfo AddInfo()
        {
            int count = Others.Count;
            return new TreeInfo
            {
                Title = "Add a class",
                Kind = count == 1 ? "1 more to choose from" : $"{count} more to choose from",
                Body = "Any hero can learn any class. Its first tier costs a point like every other, and gives the class's stat bump.",
                Note = $"Pick one below to see its tree. It joins {Hero.Definition.Name}'s classes once a tier of it is learned.",
                Status = "Looking costs nothing.",
                Action = "Choose",
                Enabled = true,
                Looks = true,
            };
        }

        TreeInfo ClassInfo()
        {
            var hero = Hero;
            var definition = Class;
            int tier = hero.TierOf(definition);
            string name = hero.Definition.Name;
            var info = new TreeInfo
            {
                Title = definition.Name,
                Kind = (tier > 0 ? $"Tier {tier} of {ClassDefinition.MaxTier}" : "Not learned") +
                       (definition == hero.Definition.StartingClass ? $"\n{name}'s own class" : ""),
            };

            var body = new List<string>();
            string bumps = TreeText.Bumps(definition);
            if (bumps.Length > 0) body.Add($"Every tier: {bumps}." + (tier > 1 ? $" At tier {tier} that is {TreeText.Bumps(definition, tier)}." : ""));
            body.Add("Tiers 5, 10, 15, 20 and 25 are milestones: one of three options each, from any path.");
            info.Body = string.Join("\n", body);

            var note = new List<string>();
            string needs = TreeText.WeaponName(definition.Weapon);
            var held = hero.Definition.Weapon?.Family ?? WeaponFamily.None;
            if (needs != null && held != definition.Weapon)
                note.Add($"Its skills need {needs}; {name} holds {TreeText.WeaponName(held) ?? "no weapon"}.");
            if (definition.SpeedModifier != 0)
                note.Add($"Speed {TreeText.Signed(definition.SpeedModifier)} while it is {name}'s highest class.");
            info.Note = string.Join("\n", note);

            int next = tier + 1;
            switch (hero.CheckRaise(definition))
            {
                case RaiseCheck.Ok:
                    info.Action = next == 1 ? $"Learn the {definition.Name}" : $"Raise to tier {next}";
                    info.Enabled = true;
                    info.Status = "Costs 1 point." + SpeedChange(definition, null);
                    break;
                case RaiseCheck.NeedsPick:
                    info.Action = $"Choose at tier {next}";
                    info.Enabled = true;
                    info.Status = $"Tier {next} is a milestone: pick one of its three options.";
                    break;
                case RaiseCheck.Locked:
                    info.Action = "Locked";
                    info.Status = $"Tier {next} is coming soon: the {definition.Name} stops at tier {definition.HighestOpenTier} for now.";
                    break;
                case RaiseCheck.NoPoints:
                    info.Action = next == 1 ? $"Learn the {definition.Name}" : $"Raise to tier {next}";
                    info.Status = "No points left: a hero gets one with every level.";
                    break;
                default:
                    info.Action = "Highest tier";
                    info.Status = $"The {definition.Name} is at its highest tier.";
                    break;
            }
            return info;
        }

        TreeInfo OptionInfo(int row, int path)
        {
            var hero = Hero;
            var definition = Class;
            int tier = TierOfRow(row);
            var option = OptionAt(row, path);
            if (option == null)
            {
                return new TreeInfo
                {
                    Title = $"Tier {tier}",
                    Kind = $"{definition.Name}, a milestone\nOne of three options",
                    Body = $"Coming soon. The {definition.Name}'s options for tier {tier} come with a later update, so the class " +
                           $"stops at tier {definition.HighestOpenTier} for now.",
                    Note = "Points keep: they can go into another class meanwhile, or wait.",
                    Status = $"Tier {tier} is locked.",
                    Action = "Locked",
                    Locked = true,
                };
            }

            var info = new TreeInfo { Title = option.Name, Body = option.Description, Path = path, Icon = IconOf(option) };
            string where = $"{definition.Paths[path]}, tier {tier}";
            var note = new List<string>();
            if (option.Teaches != null)
            {
                string tags = TreeText.Tags(option.Teaches);
                info.Kind = $"{where}\nNew {(option.Teaches.IsUltimate ? "ultimate" : "skill")}" +
                            (option.Teaches.IsUltimate ? "" : tags.Length > 0 ? ": " + tags : "");
                note.Add(SkillText.Describe(option.Teaches));
            }
            else if (option.Upgrades != null)
            {
                var known = Find(option.Upgrades.IsUltimate ? hero.KnownUltimates : hero.KnownSkills, option.Upgrades.Id);
                if (known == null)
                {
                    // PROGRESSION.md, "Classes": an upgrade of a skill the hero doesn't know teaches the skill instead.
                    info.Kind = $"{where}\nTeaches {option.Upgrades.Name}";
                    note.Add($"{hero.Definition.Name} doesn't know {option.Upgrades.Name} yet, so this teaches it as it is:");
                    note.Add(SkillText.Describe(option.Upgrades));
                }
                else
                {
                    info.Kind = $"{where}\nUpgrades {known.Name}";
                    note.Add("Now: " + SkillText.Describe(known));
                    if (StateOf(row, path) != OptionState.Picked) note.Add("With it: " + SkillText.Describe(known.Change(option.Change)));
                }
            }
            else if (option.AttackOf != WeaponFamily.None)
            {
                var attack = hero.Kit.WeaponAttack;
                info.Kind = $"{where}\nChanges the attack with {TreeText.WeaponName(option.AttackOf)}";
                if (attack.Weapon != option.AttackOf)
                    note.Add($"{hero.Definition.Name} holds {TreeText.WeaponName(attack.Weapon) ?? "no weapon"}: this changes nothing until that changes.");
                else
                {
                    note.Add($"{attack.Name} now: " + SkillText.Describe(attack));
                    if (StateOf(row, path) != OptionState.Picked) note.Add("With it: " + SkillText.Describe(attack.Change(option.Change)));
                }
            }
            else
            {
                info.Kind = where;
            }
            info.Note = string.Join("\n", note);

            switch (StateOf(row, path))
            {
                case OptionState.Picked:
                    info.Status = $"{hero.Definition.Name} picked this.";
                    break;
                case OptionState.Passed:
                    info.Status = $"{hero.PickAt(definition, tier).Name} was picked at this tier. Unlearning the {definition.Name} lets {hero.Definition.Name} choose again.";
                    break;
                case OptionState.Ahead:
                    info.Action = "Learn";
                    info.Status = $"Reach tier {tier - 1} of the {definition.Name} first.";
                    break;
                default:
                    info.Action = "Learn";
                    if (hero.CheckRaise(definition, option) == RaiseCheck.Ok)
                    {
                        info.Enabled = true;
                        info.Status = $"Costs 1 point. A pick stays until the {definition.Name} is unlearned." + SpeedChange(definition, option);
                    }
                    else
                    {
                        info.Status = "No points left: a hero gets one with every level.";
                    }
                    break;
            }
            return info;
        }

        TreeInfo SlotInfo(int slot)
        {
            var hero = Hero;
            var skill = SkillIn(slot);
            bool ultimate = slot == UltimateSlot;
            string place = ultimate ? "The ultimate" : $"Skill {slot + 1} of 3";
            var info = new TreeInfo { Title = skill?.Name ?? "Empty", Kind = place };
            if (skill != null)
            {
                string tags = TreeText.Tags(skill);
                if (!ultimate && tags.Length > 0) info.Kind += "\n" + char.ToUpperInvariant(tags[0]) + tags.Substring(1);
                info.Body = SkillText.Describe(skill);
                info.Icon = IconOf(skill);
            }
            else
            {
                info.Body = ultimate ? $"{hero.Definition.Name} has no ultimate for this weapon." : "Nothing in this slot.";
            }
            info.Note = ultimate ? "A hero takes one ultimate into a run." : "A hero takes three skills into a run, at most one of them Quick.";
            info.Action = "Change";
            info.Enabled = choices.Count > 0;
            info.Status = choices.Count > 0 ? $"{hero.Definition.Name} knows {Count(choices.Count, ultimate ? "other ultimate" : "other skill")} for this slot."
                : $"{hero.Definition.Name} knows no other {(ultimate ? "ultimate" : "skill")} yet: the classes' milestones teach more.";
            return info;
        }

        TreeInfo ChoiceInfo(TreeChoice choice)
        {
            var hero = Hero;
            var skill = choice.Skill;
            bool ultimate = choiceSlot == UltimateSlot;
            string tags = TreeText.Tags(skill);
            var info = new TreeInfo
            {
                Title = skill.Name,
                Kind = (ultimate ? "For the ultimate's slot" : $"For skill slot {choiceSlot + 1}") +
                       (!ultimate && tags.Length > 0 ? "\n" + char.ToUpperInvariant(tags[0]) + tags.Substring(1) : ""),
                Body = SkillText.Describe(skill),
                Icon = IconOf(skill),
                Action = "Equip",
                Enabled = choice.Check == EquipCheck.Ok,
            };
            if (choice.Check != EquipCheck.Ok)
            {
                info.Status = TreeText.Why(choice.Check, skill, hero);
            }
            else
            {
                var current = SkillIn(choiceSlot);
                info.Status = current == null ? "Goes into the empty slot."
                    : choice.From >= 0 ? $"Trades places with {current.Name}."
                    : $"{current.Name} leaves the loadout; {hero.Definition.Name} still knows it.";
            }
            return info;
        }

        TreeInfo UnlearnInfo()
        {
            var hero = Hero;
            var definition = Class;
            int tier = hero.TierOf(definition);
            string name = hero.Definition.Name;
            var info = new TreeInfo { Title = $"Unlearn the {definition.Name}", Kind = tier > 0 ? $"Tier {tier} of {ClassDefinition.MaxTier}" : "Not learned" };
            if (tier == 0)
            {
                info.Body = $"{name} has no tier of the {definition.Name}.";
                info.Action = "Unlearn";
                info.Status = "Nothing to unlearn.";
                return info;
            }
            info.Body = $"{name} forgets the {definition.Name} and gets {Points(tier)} back, to spend again on any class." +
                        (HasPicks(definition) ? " What its milestones taught is forgotten with it." : "");
            var note = new List<string>();
            string bumps = TreeText.Bumps(definition, tier);
            if (bumps.Length > 0) note.Add($"Its stat bumps go too: {bumps}.");
            if (definition == hero.Definition.StartingClass) note.Add($"{name} keeps the starting kit.");
            note.Add("Free for now.");
            info.Note = string.Join("\n", note);
            info.Action = "Unlearn";
            info.Enabled = true;
            info.Status = "It asks first.";
            return info;
        }

        // ---- Helpers ----

        /// <summary>
        /// " It becomes Uzuki's highest class: SPD 95 to 85." when taking the next tier of a class would change the
        /// hero's speed (PROGRESSION.md, "Speed protection": the highest class sets the modifier); empty otherwise.
        /// </summary>
        string SpeedChange(ClassDefinition definition, ClassOption option)
        {
            var hero = Hero;
            var copy = HeroProgress.Restore(hero.Definition, hero.Level, hero.Exp, hero.SaveClasses(), hero.LoadoutIds, hero.UltimateId, FindClass);
            if (!copy.Raise(definition, option)) return "";
            int before = hero.Kit.SpeedFor(hero.Definition.Speed), after = copy.Kit.SpeedFor(hero.Definition.Speed);
            return before == after ? "" : $" It becomes {hero.Definition.Name}'s highest class: SPD {before} to {after}.";
        }

        ClassDefinition FindClass(string id)
        {
            foreach (var definition in Classes)
                if (definition.Id == id) return definition;
            return ClassCatalog.Find(id);
        }

        bool HasPicks(ClassDefinition definition)
        {
            for (int tier = ClassDefinition.MilestoneEvery; tier <= ClassDefinition.MaxTier; tier += ClassDefinition.MilestoneEvery)
                if (Hero.PickAt(definition, tier) != null) return true;
            return false;
        }

        /// <summary>The row a step from the class list into the tree lands on: the next milestone's, or the last.</summary>
        int RowInReach() => Math.Min(Rows - 1, Hero.TierOf(Class) / ClassDefinition.MilestoneEvery);

        int IndexOf(ClassDefinition definition)
        {
            for (int i = 0; i < Classes.Count; i++)
                if (Classes[i] == definition) return i;
            return -1;
        }

        static SkillDefinition Find(IReadOnlyList<SkillDefinition> skills, string id)
        {
            if (id == null) return null;
            foreach (var skill in skills)
                if (skill.Id == id) return skill;
            return null;
        }

        static string Points(int count) => count == 1 ? "1 point" : $"{count} points";

        static string Count(int count, string what) => count == 1 ? $"1 {what}" : $"{count} {what}s";

        TreeFocus Clamp(TreeFocus focus)
        {
            switch (focus.Zone)
            {
                case TreeZone.Classes: return TreeFocus.Class(Math.Max(0, Math.Min(ListCount - 1, focus.Index)));
                case TreeZone.Adding:
                    int others = Others.Count;
                    return others == 0 ? TreeFocus.Class(0) : TreeFocus.Add(Math.Max(0, Math.Min(others - 1, focus.Index)));
                case TreeZone.Loadout: return TreeFocus.Slot(Math.Max(0, Math.Min(Slots - 1, focus.Index)));
                case TreeZone.Options:
                    return TreeFocus.Option(Math.Max(0, Math.Min(Rows - 1, focus.Index)), Math.Max(0, Math.Min(ClassDefinition.PathCount - 1, focus.Path)));
                case TreeZone.Choices:
                    // The list belongs to the slot the cursor came from; without one there is nothing to stand on.
                    if (choices.Count == 0) return Focus.Zone == TreeZone.Loadout ? Focus : TreeFocus.Slot(Math.Max(0, choiceSlot));
                    return TreeFocus.Choice(Math.Max(0, Math.Min(choices.Count - 1, focus.Index)));
                case TreeZone.Confirm: return TreeFocus.Confirm(focus.Index == 0);
                default: return TreeFocus.Unlearn;
            }
        }

        void SetFocus(TreeFocus focus)
        {
            // The tree follows the cursor through the hero's classes and through the "+" list; on the "+" itself it stays.
            if (focus.Zone == TreeZone.Classes && focus.Index < Learned.Count) ClassIndex = IndexOf(Learned[focus.Index]);
            else if (focus.Zone == TreeZone.Adding) ClassIndex = IndexOf(Others[focus.Index]);
            Focus = focus;
            Rebuild();
        }

        /// <summary>The list of choices follows the cursor: the slot it is on, or the one it came from into the list.</summary>
        void Rebuild()
        {
            if (Focus.Zone == TreeZone.Loadout) choiceSlot = Focus.Index;
            else if (Focus.Zone != TreeZone.Choices) choiceSlot = -1;
            choices.Clear();
            if (choiceSlot < 0) return;
            var hero = Hero;
            bool ultimate = choiceSlot == UltimateSlot;
            var current = SkillIn(choiceSlot);
            foreach (var skill in ultimate ? hero.KnownUltimates : hero.KnownSkills)
            {
                if (current != null && skill.Id == current.Id) continue;
                int from = ultimate ? -1 : IndexOfSlot(skill.Id);
                choices.Add(new TreeChoice(skill, ultimate ? hero.CheckEquipUltimate(skill) : hero.CheckEquip(choiceSlot, skill), from));
            }
            if (Focus.Zone == TreeZone.Choices && choices.Count == 0) Focus = TreeFocus.Slot(choiceSlot);
        }

        int IndexOfSlot(string id)
        {
            for (int slot = 0; slot < HeroProgress.LoadoutSkills; slot++)
                if (Hero.LoadoutIds[slot] == id) return slot;
            return -1;
        }
    }
}

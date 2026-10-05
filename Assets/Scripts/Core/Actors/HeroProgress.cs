using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>Whether a hero can take the next tier of a class right now, and if not, why (for the tree's buttons and messages).</summary>
    public enum RaiseCheck
    {
        Ok,

        /// <summary>The class is at tier 25.</summary>
        MaxTier,

        /// <summary>The next tier is a milestone whose options aren't written yet.</summary>
        Locked,

        /// <summary>Every point is spent.</summary>
        NoPoints,

        /// <summary>The next tier is a milestone: one of its three options has to be picked.</summary>
        NeedsPick,

        /// <summary>That option doesn't belong to the next tier.</summary>
        WrongOption,
    }

    /// <summary>Whether a skill can go in a loadout slot, and if not, why.</summary>
    public enum EquipCheck
    {
        Ok,

        /// <summary>The hero hasn't learned it.</summary>
        NotKnown,

        /// <summary>It is tied to a weapon the hero doesn't hold.</summary>
        WrongWeapon,

        /// <summary>A loadout holds at most one Quick skill (PROGRESSION.md, "Turn rules").</summary>
        TooManyQuick,

        /// <summary>A skill offered as the ultimate, or an ultimate offered as a skill; or no such slot.</summary>
        WrongSlot,
    }

    /// <summary>A hero's tiers in one class, and the options it picked at the milestones it has reached.</summary>
    public sealed class ClassProgress
    {
        readonly ClassOption[] picks = new ClassOption[ClassDefinition.MaxTier / ClassDefinition.MilestoneEvery];

        internal ClassProgress(ClassDefinition definition)
        {
            Class = definition;
        }

        public ClassDefinition Class { get; }

        /// <summary>1 to 25: one point each.</summary>
        public int Tier { get; internal set; }

        /// <summary>The option picked at a milestone tier, or null (not reached, or not a milestone).</summary>
        public ClassOption PickAt(int tier) => ClassDefinition.IsMilestone(tier) ? picks[tier / ClassDefinition.MilestoneEvery - 1] : null;

        internal void Pick(int tier, ClassOption option) => picks[tier / ClassDefinition.MilestoneEvery - 1] = option;
    }

    /// <summary>A class as a save file has it: its tier and the ids of the options picked on the way.</summary>
    public readonly struct SavedClass
    {
        public readonly string Id;
        public readonly int Tier;
        public readonly IReadOnlyList<string> Picks;

        public SavedClass(string id, int tier, IReadOnlyList<string> picks)
        {
            Id = id;
            Tier = tier;
            Picks = picks;
        }
    }

    /// <summary>
    /// A character's progress outside any one dungeon run: level and EXP, the classes it has learned and the skills it
    /// takes into a run (PROGRESSION.md, "Classes"); gear later. A run starts the hero from it and records every EXP
    /// gain back into it as it happens, so progress is kept win or lose.
    /// <para>
    /// A hero has as many points as levels. A point buys the next tier of any class; a milestone tier (5, 10, 15, 20,
    /// 25) also takes a pick among its three options. The hero starts at tier 1 of its own class. Its skill pool is
    /// its own starting kit plus what the options teach, with their upgrades applied; the loadout is three of those
    /// and an ultimate, changed between runs only. Unlearning a class returns its points (free for now: the
    /// relearning rule from level 20 comes with part 2 of milestone 1g).
    /// </para>
    /// </summary>
    public sealed class HeroProgress
    {
        /// <summary>Skills a hero takes into a run, besides its weapon attack and its ultimate.</summary>
        public const int LoadoutSkills = 3;

        /// <summary>At most this many of them may be Quick (a mastery may raise it later).</summary>
        public const int MaxQuickSkills = 1;

        readonly List<ClassProgress> classes = new List<ClassProgress>();
        readonly string[] loadout = new string[LoadoutSkills];
        string ultimate;
        HeroKit kit;

        /// <summary>A hero as it starts out: tier 1 of its own class, its starting kit in the loadout, the other points free.</summary>
        public HeroProgress(ActorDefinition definition, int level = 1, int exp = 0)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Level = Math.Max(1, level);
            Exp = Math.Max(0, exp);
            if (definition.StartingClass != null) Raise(definition.StartingClass);
            Normalize();
        }

        public ActorDefinition Definition { get; }
        public int Level { get; private set; }
        public int Exp { get; private set; }

        internal void Record(int level, int exp)
        {
            Level = level;
            Exp = exp;
        }

        // ---- Points and classes ----

        /// <summary>One per level (PROGRESSION.md, "Levels 1-100").</summary>
        public int Points => Level;

        public int PointsSpent
        {
            get
            {
                int spent = 0;
                foreach (var progress in classes) spent += progress.Tier;
                return spent;
            }
        }

        public int PointsFree => Points - PointsSpent;

        /// <summary>The classes the hero has points in, in the order it learned them.</summary>
        public IReadOnlyList<ClassProgress> Classes => classes;

        public int TierOf(ClassDefinition definition) => Find(definition)?.Tier ?? 0;

        /// <summary>The option the hero picked at a milestone tier of a class, or null.</summary>
        public ClassOption PickAt(ClassDefinition definition, int tier) => Find(definition)?.PickAt(tier);

        ClassProgress Find(ClassDefinition definition)
        {
            foreach (var progress in classes)
                if (progress.Class == definition) return progress;
            return null;
        }

        /// <summary>
        /// Whether the hero can take the next tier of <paramref name="definition"/>: with <paramref name="option"/> when
        /// that tier is a milestone, without one when it isn't.
        /// </summary>
        public RaiseCheck CheckRaise(ClassDefinition definition, ClassOption option = null)
        {
            int next = TierOf(definition) + 1;
            if (next > ClassDefinition.MaxTier) return RaiseCheck.MaxTier;
            if (!ClassDefinition.IsMilestone(next))
                return PointsFree <= 0 ? RaiseCheck.NoPoints : option == null ? RaiseCheck.Ok : RaiseCheck.WrongOption;

            var milestone = definition.MilestoneAt(next);
            if (milestone == null) return RaiseCheck.Locked;
            if (PointsFree <= 0) return RaiseCheck.NoPoints;
            if (option == null) return RaiseCheck.NeedsPick;
            foreach (var choice in milestone.Options)
                if (choice == option) return RaiseCheck.Ok;
            return RaiseCheck.WrongOption;
        }

        /// <summary>
        /// Spends a point on the next tier of a class, picking <paramref name="option"/> if it is a milestone. False (and
        /// nothing spent) unless <see cref="CheckRaise"/> is Ok. A skill it teaches joins the pool; it goes in the
        /// loadout only if a slot is free.
        /// </summary>
        public bool Raise(ClassDefinition definition, ClassOption option = null)
        {
            if (CheckRaise(definition, option) != RaiseCheck.Ok) return false;
            var progress = Find(definition);
            if (progress == null) classes.Add(progress = new ClassProgress(definition));
            progress.Tier++;
            if (option != null) progress.Pick(progress.Tier, option);
            Normalize();
            return true;
        }

        /// <summary>
        /// "Returns the teachings": the hero forgets a class and gets its points back (PROGRESSION.md, "Respec"; free
        /// for now). Skills only that class taught leave the pool and the loadout. Returns the points returned.
        /// </summary>
        public int Unlearn(ClassDefinition definition)
        {
            var progress = Find(definition);
            if (progress == null) return 0;
            classes.Remove(progress);
            Normalize();
            return progress.Tier;
        }

        // ---- The skill pool and the loadout ----

        /// <summary>
        /// Every skill the hero has learned, as it has them: its starting kit first, then what its class options
        /// taught, each with the upgrades the hero picked.
        /// </summary>
        public IReadOnlyList<SkillDefinition> KnownSkills
        {
            get
            {
                Resolve(out var skills, out _, out _);
                return skills;
            }
        }

        /// <summary>The same for ultimates.</summary>
        public IReadOnlyList<SkillDefinition> KnownUltimates
        {
            get
            {
                Resolve(out _, out var ultimates, out _);
                return ultimates;
            }
        }

        /// <summary>The ids of the skills in the loadout, by slot; null for an empty slot.</summary>
        public IReadOnlyList<string> LoadoutIds => loadout;

        public string UltimateId => ultimate;

        /// <summary>Whether the hero's weapon lets it use <paramref name="skill"/> (a skill tied to no weapon always).</summary>
        public bool WeaponAllows(SkillDefinition skill) =>
            skill.Weapon == WeaponFamily.None || Definition.Weapon != null && Definition.Weapon.Family == skill.Weapon;

        /// <summary>Whether <paramref name="skill"/> could go in loadout slot <paramref name="slot"/> (0-2), replacing what's there.</summary>
        public EquipCheck CheckEquip(int slot, SkillDefinition skill)
        {
            if (slot < 0 || slot >= LoadoutSkills || skill == null || skill.IsUltimate) return EquipCheck.WrongSlot;
            Resolve(out var skills, out _, out _);
            var known = skills.Find(candidate => candidate.Id == skill.Id);
            if (known == null) return EquipCheck.NotKnown;
            if (!WeaponAllows(known)) return EquipCheck.WrongWeapon;
            if (!known.IsQuick) return EquipCheck.Ok;
            // Trading places with another slot's skill changes nothing; otherwise the one it replaces leaves.
            int quick = 0, from = Array.IndexOf(loadout, known.Id);
            for (int i = 0; i < LoadoutSkills; i++)
            {
                if (i == slot || i == from || loadout[i] == null) continue;
                var other = skills.Find(candidate => candidate.Id == loadout[i]);
                if (other != null && other.IsQuick) quick++;
            }
            if (from >= 0 && from != slot && loadout[slot] != null)
            {
                var displaced = skills.Find(candidate => candidate.Id == loadout[slot]);
                if (displaced != null && displaced.IsQuick) quick++;
            }
            return quick + 1 > MaxQuickSkills ? EquipCheck.TooManyQuick : EquipCheck.Ok;
        }

        /// <summary>
        /// Puts a skill in a loadout slot. One that already sits in another slot trades places with what was there.
        /// False (nothing changed) unless <see cref="CheckEquip"/> is Ok.
        /// </summary>
        public bool Equip(int slot, SkillDefinition skill)
        {
            if (CheckEquip(slot, skill) != EquipCheck.Ok) return false;
            int from = Array.IndexOf(loadout, skill.Id);
            if (from >= 0) loadout[from] = loadout[slot];
            loadout[slot] = skill.Id;
            Normalize();
            return true;
        }

        public EquipCheck CheckEquipUltimate(SkillDefinition skill)
        {
            if (skill == null || !skill.IsUltimate) return EquipCheck.WrongSlot;
            Resolve(out _, out var ultimates, out _);
            var known = ultimates.Find(candidate => candidate.Id == skill.Id);
            return known == null ? EquipCheck.NotKnown : WeaponAllows(known) ? EquipCheck.Ok : EquipCheck.WrongWeapon;
        }

        public bool EquipUltimate(SkillDefinition skill)
        {
            if (CheckEquipUltimate(skill) != EquipCheck.Ok) return false;
            ultimate = skill.Id;
            kit = null;
            return true;
        }

        /// <summary>
        /// What the hero takes into a run: its loadout, its weapon attack, its class tiers' stat bumps and its speed
        /// modifier. Built from the hero's choices and kept until one of them changes.
        /// </summary>
        public HeroKit Kit => kit ??= BuildKit();

        // ---- Saving ----

        /// <summary>The hero's classes as a save file keeps them.</summary>
        public List<SavedClass> SaveClasses()
        {
            var saved = new List<SavedClass>();
            foreach (var progress in classes)
            {
                var picks = new List<string>();
                for (int tier = ClassDefinition.MilestoneEvery; tier <= progress.Tier; tier += ClassDefinition.MilestoneEvery)
                    if (progress.PickAt(tier) != null) picks.Add(progress.PickAt(tier).Id);
                saved.Add(new SavedClass(progress.Class.Id, progress.Tier, picks));
            }
            return saved;
        }

        /// <summary>
        /// A hero as a save file has it. Its classes are learned again tier by tier under today's rules, so whatever the
        /// file holds that those don't allow (a class or an option that no longer exists, more tiers than points) is
        /// left out and its points stay free. Then the saved loadout, where it still stands.
        /// </summary>
        /// <param name="findClass">Looks a class up by id; the catalog's by default (tests bring their own classes).</param>
        public static HeroProgress Restore(ActorDefinition definition, int level, int exp, IEnumerable<SavedClass> savedClasses,
            IReadOnlyList<string> savedLoadout, string savedUltimate, Func<string, ClassDefinition> findClass = null)
        {
            var hero = new HeroProgress(definition, level, exp);
            hero.classes.Clear();
            foreach (var saved in savedClasses ?? Array.Empty<SavedClass>())
            {
                var learned = (findClass ?? ClassCatalog.Find)(saved.Id);
                if (learned == null || hero.Find(learned) != null) continue;
                for (int tier = 1; tier <= saved.Tier; tier++)
                {
                    ClassOption option = null;
                    if (ClassDefinition.IsMilestone(tier) && saved.Picks != null)
                        foreach (string id in saved.Picks)
                            if (learned.FindOption(id, out int at) is ClassOption found && at == tier) option = found;
                    if (!hero.Raise(learned, option)) break;
                }
            }
            for (int slot = 0; slot < LoadoutSkills; slot++)
                hero.loadout[slot] = savedLoadout != null && slot < savedLoadout.Count ? savedLoadout[slot] : null;
            hero.ultimate = savedUltimate;
            hero.Normalize();
            return hero;
        }

        // ---- How the pool, the loadout and the kit are worked out ----

        /// <summary>
        /// The hero's skills, ultimates and weapon attack with its picks applied, milestone by milestone and class by
        /// class in the order learned. An option that upgrades a skill the hero doesn't know teaches it that skill
        /// instead, as the catalog has it (PROGRESSION.md, "Classes").
        /// </summary>
        void Resolve(out List<SkillDefinition> skills, out List<SkillDefinition> ultimates, out SkillDefinition attack)
        {
            skills = new List<SkillDefinition>(Definition.Skills);
            ultimates = new List<SkillDefinition>();
            if (Definition.Ultimate != null) ultimates.Add(Definition.Ultimate);
            attack = HeroKit.WeaponAttackOf(Definition);
            for (int tier = ClassDefinition.MilestoneEvery; tier <= ClassDefinition.MaxTier; tier += ClassDefinition.MilestoneEvery)
            {
                foreach (var progress in classes)
                {
                    var option = progress.PickAt(tier);
                    if (option == null) continue;
                    if (option.AttackOf != WeaponFamily.None)
                    {
                        if (option.AttackOf == attack.Weapon) attack = attack.Change(option.Change);
                        continue;
                    }
                    var skill = option.Skill;
                    if (skill == null) continue;
                    var list = skill.IsUltimate ? ultimates : skills;
                    int index = list.FindIndex(candidate => candidate.Id == skill.Id);
                    if (index < 0) list.Add(skill);
                    else if (option.Change != null) list[index] = list[index].Change(option.Change);
                }
            }
        }

        /// <summary>
        /// Keeps the loadout standing after any change: skills the hero no longer knows or can't use with its weapon
        /// leave, a second Quick skill leaves, and the slots they leave fill up from the pool (the starting kit first).
        /// The skills that stay keep their slots, so the buttons the player knows don't move.
        /// </summary>
        void Normalize()
        {
            kit = null;
            Resolve(out var skills, out var ultimates, out _);
            var slots = new SkillDefinition[LoadoutSkills];
            int quick = 0;

            bool Fits(SkillDefinition skill) =>
                skill != null && Array.IndexOf(slots, skill) < 0 && WeaponAllows(skill) && (!skill.IsQuick || quick < MaxQuickSkills);

            for (int slot = 0; slot < LoadoutSkills; slot++)
            {
                var skill = loadout[slot] != null ? skills.Find(candidate => candidate.Id == loadout[slot]) : null;
                if (!Fits(skill)) continue;
                slots[slot] = skill;
                if (skill.IsQuick) quick++;
            }
            foreach (var skill in skills)
            {
                int free = Array.IndexOf(slots, null);
                if (free < 0) break;
                if (!Fits(skill)) continue;
                slots[free] = skill;
                if (skill.IsQuick) quick++;
            }
            // With fewer usable skills than slots, the ones there are close up: slot numbers and buttons always agree.
            int next = 0;
            foreach (var skill in slots)
                if (skill != null) loadout[next++] = skill.Id;
            while (next < LoadoutSkills) loadout[next++] = null;

            var chosen = ultimates.Find(candidate => candidate.Id == ultimate && WeaponAllows(candidate));
            ultimate = (chosen ?? ultimates.Find(WeaponAllows))?.Id;
        }

        HeroKit BuildKit()
        {
            Resolve(out var skills, out var ultimates, out var attack);
            var equipped = new List<SkillDefinition>();
            foreach (string id in loadout)
            {
                var skill = id != null ? skills.Find(candidate => candidate.Id == id) : null;
                if (skill != null) equipped.Add(skill);
            }
            var percent = new StatBlock();
            var flat = new StatBlock();
            foreach (var progress in classes)
                foreach (var bump in progress.Class.Bumps)
                    (bump.IsPercentOfBase ? percent : flat)[bump.Stat] += bump.PerTier * progress.Tier;
            return new HeroKit(equipped, ultimates.Find(candidate => candidate.Id == ultimate), attack, percent, flat, SpeedModifier());
        }

        /// <summary>
        /// PROGRESSION.md, "Speed protection": the modifier of the hero's highest-tier class. Between equals, its own
        /// class, then the one it learned first.
        /// </summary>
        int SpeedModifier()
        {
            ClassProgress highest = null;
            foreach (var progress in classes)
            {
                if (highest == null || progress.Tier > highest.Tier ||
                    progress.Tier == highest.Tier && progress.Class == Definition.StartingClass)
                    highest = progress;
            }
            return highest?.Class.SpeedModifier ?? 0;
        }

        public override string ToString() => $"{Definition.Name} Lv {Level} ({Exp} EXP)";
    }
}

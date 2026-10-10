using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// The skill-tree screen's logic without its pictures (PROGRESSION.md, "Building 1g", step 6; `SkillTreeModel`):
    /// what the tree shows for a hero, what the info panel says, what pressing does, how the cursor moves, and that
    /// nothing changes during a run. The real classes have no milestone options yet, so the options are tested on the
    /// classes `ClassRulesTests` makes up.
    /// </summary>
    public class SkillTreeModelTests
    {
        static readonly ClassDefinition Scout = ClassRulesTests.Scout;
        static readonly ClassDefinition Bulwark = ClassRulesTests.Bulwark;
        static readonly ClassDefinition[] TestClasses = { ClassCatalog.Archer, ClassCatalog.Paladin, Scout, Bulwark };
        const int ScoutIndex = 2;

        /// <summary>A party of one, on the made-up classes, with how often the screen said a build changed.</summary>
        sealed class Screen
        {
            public readonly HeroProgress Hero;
            public readonly SkillTreeModel Model;
            public int Changes;

            public Screen(ActorDefinition definition, int level, bool readOnly = false, ClassDefinition[] classes = null)
            {
                Hero = new HeroProgress(definition, level);
                Model = new SkillTreeModel(new[] { Hero }, 0, readOnly, classes ?? TestClasses);
                Model.Changed += () => Changes++;
            }

            /// <summary>Puts the cursor on a class (in the hero's list or under the "+") and presses until it is at <paramref name="tier"/> (no milestone on the way).</summary>
            public void RaiseTo(int classIndex, int tier)
            {
                Model.Show(Model.Classes[classIndex]);
                while (Hero.TierOf(Model.Class) < tier)
                {
                    int before = Hero.TierOf(Model.Class);
                    Model.Activate();
                    Assert.AreEqual(before + 1, Hero.TierOf(Model.Class), Model.Info.Status);
                }
            }

            public void Pick(int row, int path)
            {
                Model.Tap(TreeFocus.Option(row, path));
                Assert.IsTrue(Model.Info.Enabled, Model.Info.Status);
                Model.Activate();
            }
        }

        // ---- The classes and their tiers ----

        [Test]
        public void TheScreenOpensOnTheHerosOwnClass()
        {
            var party = ActorCatalog.StartingParty.Select(definition => new HeroProgress(definition, 3)).ToArray();
            var model = new SkillTreeModel(party, hero: 2);
            Assert.AreSame(ActorCatalog.Uzuki, model.Hero.Definition);
            Assert.AreSame(ClassCatalog.Archer, model.Class);
            Assert.AreEqual(TreeFocus.Class(0), model.Focus);
            var info = model.Info;
            Assert.AreEqual("Archer", info.Title);
            StringAssert.Contains("Tier 1 of 25", info.Kind);
            StringAssert.Contains("Uzuki's own class", info.Kind);
            StringAssert.Contains("Every tier: +0.4% ATK, +0.2% Crit Rate.", info.Body);
            Assert.AreEqual("Raise to tier 2", info.Action);
            Assert.IsTrue(info.Enabled);
            StringAssert.Contains("Costs 1 point.", info.Status);

            model.SelectHero(3); // Around the party: back to the first hero, on his own class.
            Assert.AreSame(ActorCatalog.Haiden, model.Hero.Definition);
            Assert.AreSame(ClassCatalog.Paladin, model.Class);
            model.SelectHero(-1);
            Assert.AreSame(ActorCatalog.Uzuki, model.Hero.Definition);
        }

        [Test]
        public void PressingOnAClassRaisesItATierForAPoint()
        {
            var screen = new Screen(ActorCatalog.Uzuki, 3);
            Assert.AreEqual(2, screen.Hero.PointsFree);
            screen.Model.Activate();
            Assert.AreEqual(2, screen.Hero.TierOf(ClassCatalog.Archer));
            Assert.AreEqual(1, screen.Hero.PointsFree);
            Assert.AreEqual(1, screen.Changes, "the owner is told, so it can save");
            Assert.AreEqual("Uzuki's Archer is tier 2 now.", screen.Model.Notice);
            StringAssert.Contains("At tier 2 that is +0.8% ATK, +0.4% Crit Rate.", screen.Model.Info.Body);
            Assert.AreEqual(TierState.Reached, screen.Model.StateOf(2));
            Assert.AreEqual(TierState.Next, screen.Model.StateOf(3));
            Assert.AreEqual(TierState.Ahead, screen.Model.StateOf(4));
        }

        [Test]
        public void WithoutAPointNothingIsRaisedAndTheScreenSaysWhy()
        {
            var screen = new Screen(ActorCatalog.Uzuki, 1);
            var info = screen.Model.Info;
            Assert.IsFalse(info.Enabled);
            StringAssert.Contains("No points left", info.Status);
            screen.Model.Activate();
            Assert.AreEqual(1, screen.Hero.TierOf(ClassCatalog.Archer));
            Assert.AreEqual(0, screen.Changes);
            StringAssert.Contains("No points left", screen.Model.Notice);
        }

        [Test]
        public void TheRealClassesStopAtTierFourAndTheScreenSaysWhy()
        {
            foreach (var definition in ActorCatalog.StartingParty)
            {
                var hero = new HeroProgress(definition, 10);
                var model = new SkillTreeModel(new[] { hero });
                var own = model.Class;
                while (hero.TierOf(own) < 4) model.Activate();

                var info = model.Info;
                Assert.AreEqual("Locked", info.Action, definition.Name);
                Assert.IsFalse(info.Enabled);
                StringAssert.Contains($"Tier 5 is coming soon: the {own.Name} stops at tier 4 for now.", info.Status);
                model.Activate();
                Assert.AreEqual(4, hero.TierOf(own), "a locked tier takes no point");
                Assert.AreEqual(6, hero.PointsFree);
                Assert.AreEqual(TierState.Reached, model.StateOf(4));
                Assert.AreEqual(TierState.Locked, model.StateOf(5));
                Assert.AreEqual(TierState.Locked, model.StateOf(6), "and everything after it");

                for (int row = 0; row < SkillTreeModel.Rows; row++)
                {
                    for (int path = 0; path < ClassDefinition.PathCount; path++)
                    {
                        Assert.AreEqual(OptionState.Locked, model.StateOf(row, path));
                        Assert.IsNull(model.OptionAt(row, path));
                    }
                }
                model.Tap(TreeFocus.Option(0, 1));
                info = model.Info;
                Assert.AreEqual("Tier 5", info.Title);
                Assert.IsTrue(info.Locked);
                StringAssert.Contains("Coming soon.", info.Body);
                Assert.IsFalse(info.Enabled);
                model.Activate();
                Assert.AreEqual(4, hero.TierOf(own));

                // The points aren't stuck: they go into any other class, four tiers each for now.
                int other = (model.ClassIndex + 1) % model.Classes.Count;
                model.Show(model.Classes[other]);
                Assert.AreEqual($"Learn the {model.Classes[other].Name}", model.Info.Action);
                Assert.IsTrue(model.Info.Enabled);
                model.Activate();
                Assert.AreEqual(1, hero.TierOf(model.Classes[other]));
            }
        }

        [Test]
        public void TakingAnotherClassPastTheHerosOwnSaysWhatItDoesToSpeed()
        {
            var screen = new Screen(ActorCatalog.Uzuki, 3);
            int before = screen.Hero.Kit.SpeedFor(ActorCatalog.Uzuki.Speed);
            screen.Model.Show(ClassCatalog.Paladin); // A slow class, under the "+" until he has a tier of it.
            StringAssert.Contains("Speed -5 while it is Uzuki's highest class.", screen.Model.Info.Note);
            StringAssert.Contains("Its skills need a sword; Uzuki holds a bow.", screen.Model.Info.Note);
            Assert.AreEqual("Costs 1 point.", screen.Model.Info.Status, "level with his own class, the Archer still sets his speed");
            screen.Model.Activate();
            int after = before - ClassCatalog.Archer.SpeedModifier + ClassCatalog.Paladin.SpeedModifier;
            Assert.AreEqual($"Costs 1 point. It becomes Uzuki's highest class: SPD {before} to {after}.", screen.Model.Info.Status);
            screen.Model.Activate();
            Assert.AreEqual(after, screen.Hero.Kit.SpeedFor(ActorCatalog.Uzuki.Speed), "and the rules do what the screen said");
        }

        // ---- Milestones ----

        [Test]
        public void AMilestoneTakesAPickAmongItsThreeOptions()
        {
            var screen = new Screen(ActorCatalog.Uzuki, 12);
            var model = screen.Model;
            screen.RaiseTo(ScoutIndex, 4);
            Assert.AreEqual(TierState.Next, model.StateOf(5));
            Assert.AreEqual(TierState.Locked, model.StateOf(15), "the Scout's tier 15 isn't written");
            for (int path = 0; path < 3; path++)
            {
                Assert.AreEqual(OptionState.Open, model.StateOf(0, path));
                Assert.AreEqual(OptionState.Ahead, model.StateOf(1, path));
                Assert.AreEqual(OptionState.Locked, model.StateOf(2, path));
            }

            // On the class, the button leads to the row instead of spending a point.
            var info = model.Info;
            Assert.AreEqual("Choose at tier 5", info.Action);
            StringAssert.Contains("Tier 5 is a milestone", info.Status);
            int changes = screen.Changes;
            model.Activate();
            Assert.AreEqual(TreeFocus.Option(0, 0), model.Focus);
            Assert.AreEqual(4, screen.Hero.TierOf(Scout));
            Assert.AreEqual(changes, screen.Changes);

            screen.Pick(0, 1);
            Assert.AreEqual(5, screen.Hero.TierOf(Scout));
            Assert.AreSame(ClassRulesTests.LearnJolt, screen.Hero.PickAt(Scout, 5));
            Assert.AreEqual("Uzuki took Jolt: the Scout is tier 5 now.", model.Notice);
            Assert.AreEqual(OptionState.Passed, model.StateOf(0, 0));
            Assert.AreEqual(OptionState.Picked, model.StateOf(0, 1));
            Assert.AreEqual(OptionState.Passed, model.StateOf(0, 2));
            Assert.IsTrue(screen.Hero.KnownSkills.Any(skill => skill.Id == ClassRulesTests.Jolt.Id));

            // The pick is final: its row's other options say so, and pressing changes nothing.
            model.Tap(TreeFocus.Option(0, 0));
            Assert.IsNull(model.Info.Action);
            StringAssert.Contains("Jolt was picked at this tier.", model.Info.Status);
            model.Activate();
            Assert.AreSame(ClassRulesTests.LearnJolt, screen.Hero.PickAt(Scout, 5));
            model.Tap(TreeFocus.Option(0, 1));
            Assert.AreEqual("Uzuki picked this.", model.Info.Status);
        }

        [Test]
        public void AnOptionFurtherDownWaitsForTheTiersBeforeIt()
        {
            var screen = new Screen(ActorCatalog.Uzuki, 12);
            screen.RaiseTo(ScoutIndex, 2);
            screen.Model.Tap(TreeFocus.Option(0, 0));
            var info = screen.Model.Info;
            Assert.AreEqual("Learn", info.Action);
            Assert.IsFalse(info.Enabled);
            Assert.AreEqual("Reach tier 4 of the Scout first.", info.Status);
            int changes = screen.Changes;
            screen.Model.Activate();
            Assert.AreEqual(2, screen.Hero.TierOf(Scout));
            Assert.AreEqual(changes, screen.Changes);
        }

        [Test]
        public void TheInfoPanelDescribesAnOptionFromTheHerosOwnSkills()
        {
            var screen = new Screen(ActorCatalog.Uzuki, 12);
            var model = screen.Model;
            screen.RaiseTo(ScoutIndex, 4);

            // An upgrade of a skill he knows: as he has it now, and as it would be.
            model.Tap(TreeFocus.Option(0, 0));
            var info = model.Info;
            Assert.AreEqual("Heavy Draw", info.Title);
            Assert.AreEqual("One, tier 5\nUpgrades Power Shot", info.Kind);
            Assert.AreEqual("Power Shot hits harder and knocks back 2 tiles.", info.Body);
            StringAssert.Contains("Now: A shot of 300% ATK", info.Note);
            StringAssert.Contains("With it: A shot of 360% ATK", info.Note);
            StringAssert.Contains("Knocks the foe back 2 tiles.", info.Note);
            Assert.AreEqual("power_shot", info.Icon);
            Assert.AreEqual(0, info.Path);
            Assert.AreEqual("Costs 1 point. A pick stays until the Scout is unlearned.", info.Status);

            // A new skill: what it does, and that it is Quick.
            model.Tap(TreeFocus.Option(0, 2));
            info = model.Info;
            Assert.AreEqual("Sprint", info.Title);
            Assert.AreEqual("Three, tier 5\nNew skill: Quick", info.Kind);
            StringAssert.Contains(SkillText.Describe(ClassRulesTests.Sprint), info.Note);
            Assert.AreEqual("test_sprint", info.Icon);

            // A change to the attack of a weapon he doesn't hold.
            model.Tap(TreeFocus.Option(1, 0));
            info = model.Info;
            Assert.AreEqual("One, tier 10\nChanges the attack with fists", info.Kind);
            StringAssert.Contains("Uzuki holds a bow: this changes nothing until that changes.", info.Note);
            Assert.AreEqual("attack_hunter_bow", info.Icon, "the weapon attack's icon goes by the weapon");

            // Another ultimate.
            model.Tap(TreeFocus.Option(1, 2));
            Assert.AreEqual("Three, tier 10\nNew ultimate", model.Info.Kind);
        }

        [Test]
        public void AnUpgradeOfASkillTheHeroDoesntKnowSaysItTeachesTheSkill()
        {
            var screen = new Screen(ActorCatalog.Haiden, 12);
            screen.RaiseTo(ScoutIndex, 4);
            screen.Model.Tap(TreeFocus.Option(0, 0));
            var info = screen.Model.Info;
            Assert.AreEqual("One, tier 5\nTeaches Power Shot", info.Kind);
            StringAssert.Contains("Haiden doesn't know Power Shot yet", info.Note);
            StringAssert.Contains(SkillText.Describe(SkillCatalog.PowerShot), info.Note);
        }

        // ---- The loadout ----

        [Test]
        public void ASkillTheHeroLearnedGoesIntoTheLoadoutFromTheListOfChoices()
        {
            var screen = new Screen(ActorCatalog.Uzuki, 12);
            var model = screen.Model;
            var hero = screen.Hero;

            // With the starting kit only, a slot can still trade places with another.
            model.Tap(TreeFocus.Slot(0));
            Assert.AreEqual("Hunter's Mark", model.Info.Title);
            Assert.AreEqual("Skill 1 of 3\nQuick", model.Info.Kind);
            Assert.AreEqual(SkillText.Describe(model.SkillIn(0)), model.Info.Body);
            Assert.AreEqual(2, model.Choices.Count);
            Assert.IsTrue(model.Choices.All(choice => choice.Check == EquipCheck.Ok && choice.From > 0));

            screen.RaiseTo(ScoutIndex, 4);
            screen.Pick(0, 1); // Jolt.
            CollectionAssert.AreEqual(new[] { "hunters_mark", "power_shot", "rolling_shot" }, hero.LoadoutIds, "a full loadout stays as it is");

            model.Tap(TreeFocus.Slot(1));
            Assert.AreEqual("Change", model.Info.Action);
            Assert.AreEqual("Uzuki knows 3 other skills for this slot.", model.Info.Status);
            int jolt = model.Choices.ToList().FindIndex(choice => choice.Skill.Id == "test_jolt");
            Assert.AreEqual(-1, model.Choices[jolt].From);
            int changes = screen.Changes;
            model.Activate();
            Assert.AreEqual(TreeZone.Choices, model.Focus.Zone, "the cursor goes into the list");
            model.Tap(TreeFocus.Choice(jolt));
            var info = model.Info;
            Assert.AreEqual("Jolt", info.Title);
            Assert.AreEqual("Equip", info.Action);
            Assert.AreEqual("Power Shot leaves the loadout; Uzuki still knows it.", info.Status);
            model.Activate();
            CollectionAssert.AreEqual(new[] { "hunters_mark", "test_jolt", "rolling_shot" }, hero.LoadoutIds);
            Assert.AreEqual(changes + 1, screen.Changes);
            Assert.AreEqual(TreeFocus.Slot(1), model.Focus);
            Assert.AreEqual("Jolt is in slot 2 now.", model.Notice);
            CollectionAssert.AreEqual(new[] { "hunters_mark", "test_jolt", "rolling_shot" }, hero.Kit.Skills.Select(skill => skill.Id), "and it is the kit a run takes");

            // A skill from another slot trades places.
            model.Activate();
            int mark = model.Choices.ToList().FindIndex(choice => choice.Skill.Id == "hunters_mark");
            model.Tap(TreeFocus.Choice(mark));
            Assert.AreEqual("Trades places with Jolt.", model.Info.Status);
            model.Activate();
            CollectionAssert.AreEqual(new[] { "test_jolt", "hunters_mark", "rolling_shot" }, hero.LoadoutIds);
        }

        [Test]
        public void ALoadoutHoldsOneQuickSkillAndTheListSaysSo()
        {
            var screen = new Screen(ActorCatalog.Uzuki, 12);
            var model = screen.Model;
            screen.RaiseTo(ScoutIndex, 4);
            screen.Pick(0, 2); // Sprint, a second Quick skill next to Hunter's Mark.

            model.Tap(TreeFocus.Slot(1)); // Power Shot's slot: Hunter's Mark would stay in.
            int sprint = model.Choices.ToList().FindIndex(choice => choice.Skill.Id == "test_sprint");
            Assert.AreEqual(EquipCheck.TooManyQuick, model.Choices[sprint].Check);
            model.Activate();
            model.Tap(TreeFocus.Choice(sprint));
            Assert.IsFalse(model.Info.Enabled);
            StringAssert.Contains("A loadout holds one Quick skill", model.Info.Status);
            int changes = screen.Changes;
            model.Activate();
            CollectionAssert.AreEqual(new[] { "hunters_mark", "power_shot", "rolling_shot" }, screen.Hero.LoadoutIds);
            Assert.AreEqual(changes, screen.Changes);

            model.Tap(TreeFocus.Slot(0)); // In Hunter's Mark's place it is the only one.
            sprint = model.Choices.ToList().FindIndex(choice => choice.Skill.Id == "test_sprint");
            Assert.AreEqual(EquipCheck.Ok, model.Choices[sprint].Check);
            model.Activate();
            model.Tap(TreeFocus.Choice(sprint));
            model.Activate();
            CollectionAssert.AreEqual(new[] { "test_sprint", "power_shot", "rolling_shot" }, screen.Hero.LoadoutIds);
        }

        [Test]
        public void ASkillTiedToAnotherWeaponCantGoInTheLoadout()
        {
            var screen = new Screen(ActorCatalog.Haiden, 12);
            var model = screen.Model;
            screen.RaiseTo(ScoutIndex, 4);
            screen.Pick(0, 0); // Heavy Draw teaches him Power Shot, a bow skill.
            Assert.IsTrue(screen.Hero.KnownSkills.Any(skill => skill.Id == "power_shot"));

            model.Tap(TreeFocus.Slot(0));
            int shot = model.Choices.ToList().FindIndex(choice => choice.Skill.Id == "power_shot");
            Assert.AreEqual(EquipCheck.WrongWeapon, model.Choices[shot].Check);
            model.Activate();
            model.Tap(TreeFocus.Choice(shot));
            Assert.IsFalse(model.Info.Enabled);
            Assert.AreEqual("Power Shot needs a bow: Haiden holds a sword.", model.Info.Status);
            var before = screen.Hero.LoadoutIds.ToArray();
            model.Activate();
            CollectionAssert.AreEqual(before, screen.Hero.LoadoutIds);
        }

        [Test]
        public void TheUltimatesSlotTakesAnotherUltimateTheHeroLearned()
        {
            var screen = new Screen(ActorCatalog.Uzuki, 12);
            var model = screen.Model;
            model.Tap(TreeFocus.Slot(SkillTreeModel.UltimateSlot));
            Assert.AreEqual("Volley", model.Info.Title);
            Assert.AreEqual("The ultimate", model.Info.Kind);
            Assert.IsFalse(model.Info.Enabled);
            StringAssert.Contains("Uzuki knows no other ultimate yet", model.Info.Status);
            model.Activate();
            Assert.AreEqual(TreeFocus.Slot(SkillTreeModel.UltimateSlot), model.Focus, "no list to go into");

            screen.RaiseTo(ScoutIndex, 4);
            screen.Pick(0, 1);
            screen.RaiseTo(ScoutIndex, 9);
            screen.Pick(1, 2); // Nova.
            Assert.AreEqual("volley", screen.Hero.UltimateId, "the ultimate he had stays in");
            model.Tap(TreeFocus.Slot(SkillTreeModel.UltimateSlot));
            Assert.AreEqual(1, model.Choices.Count);
            model.Activate();
            Assert.AreEqual("For the ultimate's slot", model.Info.Kind);
            model.Activate();
            Assert.AreEqual("test_nova", screen.Hero.UltimateId);
            Assert.AreEqual("Nova is Uzuki's ultimate now.", model.Notice);
            Assert.AreEqual("test_nova", screen.Hero.Kit.Ultimate.Id);
        }

        // ---- Unlearning ----

        [Test]
        public void UnlearningAsksFirstAndReturnsThePoints()
        {
            var screen = new Screen(ActorCatalog.Uzuki, 12);
            var model = screen.Model;
            screen.RaiseTo(ScoutIndex, 4);
            screen.Pick(0, 1);
            Assert.AreEqual(6, screen.Hero.PointsFree);

            model.Tap(TreeFocus.Unlearn);
            var info = model.Info;
            Assert.AreEqual("Unlearn the Scout", info.Title);
            StringAssert.Contains("Uzuki forgets the Scout and gets 5 points back", info.Body);
            StringAssert.Contains("What its milestones taught is forgotten with it.", info.Body);
            Assert.IsTrue(info.Enabled);
            int changes = screen.Changes;
            model.Activate();
            Assert.IsTrue(model.Confirming);
            Assert.AreEqual("Unlearn the Scout?\nUzuki gets 5 points back and forgets what its milestones taught.", model.Question);
            Assert.AreEqual(TreeFocus.Confirm(false), model.Focus, "Cancel is the marked answer");

            model.Tap(TreeFocus.Class(0));
            Assert.IsTrue(model.Confirming, "the question wants its answer first");
            model.Activate(); // Cancel.
            Assert.IsFalse(model.Confirming);
            Assert.AreEqual(5, screen.Hero.TierOf(Scout));
            Assert.AreEqual(changes, screen.Changes);
            Assert.AreEqual(TreeFocus.Unlearn, model.Focus);

            model.Activate();
            Assert.IsTrue(model.Back(), "Esc steps out of the question");
            Assert.IsFalse(model.Confirming);
            Assert.AreEqual(5, screen.Hero.TierOf(Scout));

            model.Activate();
            model.Move(-1, 0); // Over to Yes.
            Assert.AreEqual(TreeFocus.Confirm(true), model.Focus);
            model.Activate();
            Assert.IsFalse(model.Confirming);
            Assert.AreEqual(0, screen.Hero.TierOf(Scout));
            Assert.AreEqual(11, screen.Hero.PointsFree);
            Assert.IsFalse(screen.Hero.KnownSkills.Any(skill => skill.Id == "test_jolt"));
            Assert.AreEqual(changes + 1, screen.Changes);
            Assert.AreEqual("Uzuki unlearned the Scout: 5 points back.", model.Notice);

            // Nothing left of it to unlearn.
            Assert.IsFalse(model.Info.Enabled);
            Assert.AreEqual("Nothing to unlearn.", model.Info.Status);
            model.Activate();
            Assert.IsFalse(model.Confirming);
        }

        [Test]
        public void AHeroCanUnlearnItsOwnClassAndKeepsItsStartingKit()
        {
            var screen = new Screen(ActorCatalog.Kristela, 4, classes: ClassCatalog.All);
            var model = screen.Model;
            Assert.AreSame(ClassCatalog.Fencer, model.Class);
            model.Tap(TreeFocus.Unlearn);
            StringAssert.Contains("Kristela keeps the starting kit.", model.Info.Note);
            model.Activate();
            Assert.AreEqual("Unlearn the Fencer?\nKristela gets 1 point back.", model.Question);
            model.Tap(TreeFocus.Confirm(true));
            model.Activate();
            Assert.AreEqual(0, screen.Hero.TierOf(ClassCatalog.Fencer));
            Assert.AreEqual(4, screen.Hero.PointsFree);
            Assert.AreEqual(0, model.Learned.Count, "her list is empty now: only the + is left on it");
            Assert.AreEqual(0, model.AddIndex);
            Assert.AreEqual(1, model.ListCount);
            CollectionAssert.AreEqual(ActorCatalog.Kristela.Skills.Select(skill => skill.Id), screen.Hero.LoadoutIds);
        }

        // ---- During a run ----

        [Test]
        public void DuringARunTheScreenOnlyShows()
        {
            var screen = new Screen(ActorCatalog.Uzuki, 12, readOnly: true);
            var model = screen.Model;
            Assert.IsTrue(model.ReadOnly);
            var info = model.Info;
            Assert.AreEqual("Raise to tier 2", info.Action);
            Assert.IsFalse(info.Enabled);
            Assert.AreEqual("A build changes between runs only: finish or leave this run first.", info.Status);
            model.Activate();
            Assert.AreEqual(1, screen.Hero.TierOf(ClassCatalog.Archer));

            model.Tap(TreeFocus.Slot(0));
            Assert.IsFalse(model.Info.Enabled);
            model.Activate();
            Assert.AreEqual(TreeFocus.Slot(0), model.Focus);

            model.Tap(TreeFocus.Unlearn);
            Assert.IsFalse(model.Info.Enabled);
            model.Activate();
            Assert.IsFalse(model.Confirming);
            Assert.AreEqual(0, screen.Changes);

            // Looking around still works.
            model.Tap(TreeFocus.Option(0, 0));
            Assert.AreEqual("Tier 5", model.Info.Title);
            model.Move(1, 0);
            Assert.AreEqual(TreeFocus.Option(0, 1), model.Focus);
        }

        // ---- The cursor, for keys and a controller ----

        [Test]
        public void TheCursorWalksTheScreen()
        {
            var screen = new Screen(ActorCatalog.Uzuki, 12);
            var model = screen.Model;
            Assert.AreEqual(TreeFocus.Class(0), model.Focus);
            model.Move(0, -1);
            Assert.AreEqual(TreeFocus.Class(0), model.Focus, "nothing above his first class");
            model.Move(-1, 0);
            Assert.AreEqual(TreeFocus.Class(0), model.Focus);

            // Down his list: the Archer, then the "+". The tree stays on the class it showed.
            model.Move(0, 1);
            Assert.AreEqual(TreeFocus.Class(model.AddIndex), model.Focus);
            Assert.AreSame(ClassCatalog.Archer, model.Class);
            model.Move(0, 1);
            Assert.AreEqual(TreeFocus.Slot(0), model.Focus, "below the list, the loadout");

            // Along the loadout, then into the tree at its middle row.
            model.Move(1, 0);
            model.Move(1, 0);
            model.Move(1, 0);
            Assert.AreEqual(TreeFocus.Slot(SkillTreeModel.UltimateSlot), model.Focus);
            model.Move(1, 0);
            Assert.AreEqual(TreeFocus.Option(2, 0), model.Focus);
            model.Move(-1, 0);
            Assert.AreEqual(TreeFocus.Slot(SkillTreeModel.UltimateSlot), model.Focus, "and back");
            model.Move(-1, 0);
            model.Move(0, -1);
            Assert.AreEqual(TreeFocus.Class(model.AddIndex), model.Focus, "up from the loadout, the foot of the list");
            model.Move(0, 1);
            model.Move(0, 1);
            Assert.AreEqual(TreeFocus.Unlearn, model.Focus);
            model.Move(0, 1);
            Assert.AreEqual(TreeFocus.Unlearn, model.Focus, "nothing below it");
            model.Move(1, 0);
            Assert.AreEqual(TreeFocus.Option(SkillTreeModel.Rows - 1, 0), model.Focus);

            // Around the tree: it stops at its edges, and its left edge leads back to the left column.
            model.Move(0, 1);
            Assert.AreEqual(TreeFocus.Option(4, 0), model.Focus);
            model.Move(1, 0);
            model.Move(1, 0);
            model.Move(1, 0);
            Assert.AreEqual(TreeFocus.Option(4, 2), model.Focus);
            for (int i = 0; i < 6; i++) model.Move(0, -1);
            Assert.AreEqual(TreeFocus.Option(0, 2), model.Focus);
            model.Move(-1, 0);
            model.Move(-1, 0);
            model.Move(-1, 0);
            Assert.AreEqual(TreeFocus.Class(0), model.Focus, "the class whose tree it is, in his list");
            model.Move(1, 0);
            Assert.AreEqual(TreeFocus.Option(0, 0), model.Focus, "into the tree at the next milestone's row");
            model.Move(0, 1);
            model.Move(0, 1);
            model.Move(0, 1);
            model.Move(0, 1);
            model.Move(-1, 0);
            Assert.AreEqual(TreeFocus.Unlearn, model.Focus);

            // The "+" list: up and down in it (the tree follows), right into the tree and back, left or Back out of it.
            model.Tap(TreeFocus.Class(model.AddIndex));
            model.Activate();
            Assert.AreEqual(TreeFocus.Add(0), model.Focus);
            Assert.AreSame(ClassCatalog.Paladin, model.Class);
            model.Move(0, 1);
            Assert.AreEqual(TreeFocus.Add(1), model.Focus);
            Assert.AreSame(Scout, model.Class);
            for (int i = 0; i < 5; i++) model.Move(0, 1);
            Assert.AreEqual(TreeFocus.Add(2), model.Focus);
            Assert.AreSame(Bulwark, model.Class);
            model.Move(1, 0);
            Assert.AreEqual(TreeFocus.Option(0, 0), model.Focus);
            model.Move(-1, 0);
            Assert.AreEqual(TreeFocus.Add(2), model.Focus, "back to where the class stands: under the +");
            model.Move(-1, 0);
            Assert.AreEqual(TreeFocus.Class(model.AddIndex), model.Focus);
            model.Activate();
            Assert.IsTrue(model.Back());
            Assert.AreEqual(TreeFocus.Class(model.AddIndex), model.Focus);

            // A step from the classes lands on the row the class has reached.
            screen.RaiseTo(ScoutIndex, 4);
            screen.Pick(0, 1);
            model.Show(Scout);
            model.Move(1, 0);
            Assert.AreEqual(TreeFocus.Option(1, 0), model.Focus);

            // The list of choices: up and down in it, left or Back out of it.
            model.Tap(TreeFocus.Slot(2));
            model.Activate();
            Assert.AreEqual(TreeFocus.Choice(0), model.Focus);
            model.Move(0, -1);
            Assert.AreEqual(TreeFocus.Choice(0), model.Focus);
            model.Move(0, 1);
            Assert.AreEqual(TreeFocus.Choice(1), model.Focus);
            for (int i = 0; i < 5; i++) model.Move(0, 1);
            Assert.AreEqual(TreeFocus.Choice(model.Choices.Count - 1), model.Focus);
            model.Move(-1, 0);
            Assert.AreEqual(TreeFocus.Slot(2), model.Focus);
            model.Activate();
            Assert.IsTrue(model.Back());
            Assert.AreEqual(TreeFocus.Slot(2), model.Focus);
            Assert.IsFalse(model.Back(), "nothing left to step out of: the screen closes");
        }

        // ---- The hero's list and the "+" (Peter, 2026-10-10) ----

        [Test]
        public void TheListShowsTheClassesTheHeroHasAndAPlusForTheRest()
        {
            var screen = new Screen(ActorCatalog.Uzuki, 6);
            var model = screen.Model;
            CollectionAssert.AreEqual(new[] { ClassCatalog.Archer }, model.Learned);
            CollectionAssert.AreEqual(new[] { ClassCatalog.Paladin, Scout, Bulwark }, model.Others);
            Assert.AreEqual(2, model.ListCount);
            Assert.AreEqual(1, model.AddIndex);

            // The "+": what it is for, and into its list.
            model.Tap(TreeFocus.Class(model.AddIndex));
            Assert.AreSame(ClassCatalog.Archer, model.Class, "the tree stays on the class it showed");
            var info = model.Info;
            Assert.AreEqual("Add a class", info.Title);
            Assert.AreEqual("3 more to choose from", info.Kind);
            Assert.AreEqual("Choose", info.Action);
            Assert.IsTrue(info.Enabled);
            model.Activate();
            Assert.AreEqual(TreeFocus.Add(0), model.Focus);
            Assert.AreSame(ClassCatalog.Paladin, model.Class, "the tree shows the class being looked at");
            Assert.AreEqual(0, screen.Changes, "looking costs nothing");

            model.Tap(TreeFocus.Add(1));
            Assert.AreSame(Scout, model.Class);
            info = model.Info;
            Assert.AreEqual("Scout", info.Title);
            StringAssert.Contains("Not learned", info.Kind);
            Assert.AreEqual("Learn the Scout", info.Action);
            Assert.AreEqual(1, model.Learned.Count, "looking at it doesn't put it in his list");

            // Learning its first tier adds it to his list, and the cursor follows it there.
            model.Activate();
            Assert.AreEqual(1, screen.Hero.TierOf(Scout));
            Assert.AreEqual("Uzuki learned the Scout.", model.Notice);
            CollectionAssert.AreEqual(new[] { ClassCatalog.Archer, Scout }, model.Learned);
            Assert.AreEqual(TreeFocus.Class(1), model.Focus);
            Assert.AreEqual(2, model.AddIndex);
            Assert.AreEqual("Raise to tier 2", model.Info.Action);
            Assert.AreEqual(1, screen.Changes);
        }

        [Test]
        public void AClassUnlearnedLeavesTheListAndWithEveryClassLearnedThePlusGoes()
        {
            var screen = new Screen(ActorCatalog.Uzuki, 6);
            var model = screen.Model;
            foreach (var definition in new[] { ClassCatalog.Paladin, Scout, Bulwark })
            {
                model.Show(definition);
                model.Activate();
            }
            Assert.AreEqual(4, model.Learned.Count);
            Assert.AreEqual(-1, model.AddIndex, "nothing left to add");
            Assert.AreEqual(4, model.ListCount);
            model.Tap(TreeFocus.Class(9));
            Assert.AreEqual(TreeFocus.Class(3), model.Focus, "the list ends at his last class");

            model.Show(Scout);
            model.Tap(TreeFocus.Unlearn);
            model.Activate();
            model.Tap(TreeFocus.Confirm(true));
            model.Activate();
            CollectionAssert.AreEqual(new[] { ClassCatalog.Archer, ClassCatalog.Paladin, Bulwark }, model.Learned);
            Assert.AreEqual(3, model.AddIndex, "the + is back, with the Scout under it");
            CollectionAssert.AreEqual(new[] { Scout }, model.Others);
            Assert.AreSame(Scout, model.Class, "its tree is still the one shown");
        }

        [Test]
        public void DuringARunThePlusStillShowsTheOtherClasses()
        {
            var screen = new Screen(ActorCatalog.Uzuki, 6, readOnly: true);
            var model = screen.Model;
            model.Tap(TreeFocus.Class(model.AddIndex));
            Assert.IsTrue(model.Info.Enabled, "looking is allowed");
            model.Activate();
            Assert.AreEqual(TreeFocus.Add(0), model.Focus);
            Assert.AreEqual("Learn the Paladin", model.Info.Action);
            Assert.IsFalse(model.Info.Enabled);
            model.Activate();
            Assert.AreEqual(0, screen.Hero.TierOf(ClassCatalog.Paladin));
            Assert.AreEqual(0, screen.Changes);
        }

        [Test]
        public void EveryIconTheStartingKitsNeedHasAName()
        {
            foreach (var definition in ActorCatalog.StartingParty)
            {
                var hero = new HeroProgress(definition, 1);
                var model = new SkillTreeModel(new[] { hero });
                Assert.AreEqual("attack_" + definition.Weapon.Id, model.IconOf(hero.Kit.WeaponAttack));
                for (int slot = 0; slot < SkillTreeModel.Slots; slot++)
                    Assert.AreEqual(model.SkillIn(slot).Id, model.IconOf(model.SkillIn(slot)), $"{definition.Name}, slot {slot}");
            }
        }
    }
}

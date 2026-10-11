using System.Collections.Generic;
using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;
using static FiveKingdoms.Tests.PartyTests;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// The Archer's milestone options (PROGRESSION.md, "Starting class content"), on Uzuki: what each one changes on his
    /// own copy of a skill or teaches him, what it does in a run, and what the party's AI does with it.
    /// </summary>
    public class ArcherTiersTests
    {
        static readonly string[] Room =
        {
            "###########",
            "#.........#",
            "#.........#",
            "#@........#",
            "#.........#",
            "#.........#",
            "###########",
        };

        static readonly string[] Corridor =
        {
            "############",
            "#@.........#",
            "############",
        };

        static ClassOption Option(string id) => ClassCatalog.Archer.FindOption(id, out _);

        /// <summary>Uzuki at <paramref name="level"/> with his Archer tiers raised as far as his points go, taking <paramref name="picks"/>.</summary>
        static HeroProgress Archer(int level, params string[] picks) => TestHeroes.Built(ActorCatalog.Uzuki, level, picks);

        static DungeonRun Run(HeroProgress hero, params string[] rows) => TestRuns.With(hero, rows);

        static int Slot(Actor hero, SkillDefinition skill) => TestHeroes.Slot(hero, skill);

        static List<AttackEvent> ShotsBy(DungeonRun run, Actor shooter) =>
            run.Events.OfType<AttackEvent>().Where(attack => attack.AttackerId == shooter.Id).ToList();

        /// <summary>A dummy that stays where it is, takes hits at face value (no DEF) and never dies.</summary>
        static Actor Target(DungeonRun run, int x, int y)
        {
            var dummy = Dummy(run, x, y);
            dummy.Defense = 0;
            dummy.Statuses.Add(new StatusEffect(StatusKind.Rooted, dummy.Id, 0, 999, endsOnSourceTurn: false));
            return dummy;
        }

        /// <summary>Asserts a hit of <paramref name="percent"/>% ATK from a shooter who doesn't crit: the ranged cut, and the 85-100% spread.</summary>
        static void AssertShotOf(int percent, Actor shooter, DamageEvent hit, string what)
        {
            int full = shooter.Attack * percent / 100 * CombatRules.RangedDamagePercent / 100;
            Assert.That(hit.Amount, Is.InRange(full * CombatRules.SpreadMinPercent / 100 - 1, full), what);
        }

        // ---- Tier 5 ----

        [Test]
        public void TierFiveHasAnOptionOnEachPath()
        {
            var tier = ClassCatalog.Archer.MilestoneAt(5);
            Assert.IsNotNull(tier, "tier 5 is written");
            CollectionAssert.AreEqual(new[] { "archer_deadly_mark", "archer_crippling_shot", "archer_bouncing_shot" }, tier.Options.Select(option => option.Id));
            Assert.AreSame(SkillCatalog.HuntersMark, tier.Options[0].Upgrades, "the Marksman upgrades the mark");
            Assert.AreSame(SkillCatalog.CripplingShot, tier.Options[1].Teaches, "the Hunter learns a shot that slows");
            Assert.AreSame(SkillCatalog.BouncingShot, tier.Options[2].Teaches, "the Trickshot learns a shot that bounces");
            foreach (var skill in new[] { SkillCatalog.CripplingShot, SkillCatalog.BouncingShot })
            {
                Assert.AreEqual(WeaponFamily.Bow, skill.Weapon, skill.Name);
                Assert.AreEqual(SkillDefinition.DefaultCooldown, skill.Cooldown, skill.Name);
                Assert.IsFalse(skill.IsQuick, skill.Name);
            }
        }

        // Deadly Mark (Peter, 2026-10-10): Hunter's Mark as an always-on skill that his shots place and build up.

        /// <summary>Uzuki with Deadly Mark in a corridor, crits off so a hit can be measured; his mark is in slot 0.</summary>
        static DungeonRun MarksmanRun(params string[] rows)
        {
            var run = Run(Archer(5, "archer_deadly_mark"), rows);
            run.Hero.CritRate = 0;
            return run;
        }

        static int MarkOn(DungeonRun run, Actor foe) => foe.FindStatus(StatusKind.Mark)?.Power ?? -1;

        [Test]
        public void DeadlyMarkMakesTheMarkAlwaysOnOnUzukisOwnCopy()
        {
            var uzuki = Archer(5, "archer_deadly_mark");
            Assert.AreSame(Option("archer_deadly_mark"), uzuki.PickAt(ClassCatalog.Archer, 5));
            var mark = uzuki.Kit.Skills.Single(skill => skill.Id == SkillCatalog.HuntersMark.Id);
            Assert.IsTrue(mark.AlwaysOn);
            Assert.AreEqual(0, mark.Power, "the mark starts at nothing");
            Assert.AreEqual(10, mark.BuildPercent);
            Assert.AreEqual(40, mark.MaxPower);
            Assert.IsFalse(mark.IsQuick, "it takes no time at all: not the loadout's Quick skill");
            Assert.IsFalse(mark.NeedsAim);
            Assert.IsFalse(SkillCatalog.HuntersMark.AlwaysOn, "the catalog's skill is never changed");
            Assert.AreEqual(25, SkillCatalog.HuntersMark.Power);
            Assert.IsTrue(SkillCatalog.HuntersMark.IsQuick);
            CollectionAssert.AreEqual(new[] { "hunters_mark", "power_shot", "rolling_shot" }, uzuki.LoadoutIds, "it keeps its slot");

            string text = SkillText.Describe(mark);
            StringAssert.Contains("Each further shot on the marked foe adds 10% to the hero's damage on it, up to 40%.", text);
            StringAssert.Contains("Always on", text);
            StringAssert.DoesNotContain("Quick", text);
            StringAssert.DoesNotContain("Not two turns in a row", text);
            StringAssert.Contains(TreeText.AlwaysOn, TreeText.Tags(mark));
        }

        [Test]
        public void AnAlwaysOnSkillIsNeverUsed()
        {
            var run = MarksmanRun(Corridor);
            var foe = Target(run, 5, 1);
            Assert.AreEqual(SkillCheck.AlwaysOn, run.CheckSkill(0));
            Assert.AreEqual(SkillCheck.AlwaysOn, run.CheckSkillAt(run.Hero, 0, foe.Pos));
            int turns = run.Hero.TurnsTaken;
            Assert.IsFalse(run.UseSkillAt(0, foe.Pos), "refused");
            Assert.AreEqual(turns, run.Hero.TurnsTaken, "no turn used");
            Assert.IsNull(foe.FindStatus(StatusKind.Mark));
            Assert.IsFalse(HeroTactics.TryMark(run, run.Hero, out _), "the AI doesn't try to");
        }

        [Test]
        public void HisShotsMarkTheFoeTheyAreAimedAtAndBuildTheMarkUp()
        {
            var run = MarksmanRun(Corridor);
            var hero = run.Hero;
            var foe = Target(run, 5, 1);

            Assert.IsTrue(run.AttackAt(foe.Pos));
            Assert.AreEqual(0, MarkOn(run, foe), "the first shot marks it");
            var placed = run.Events.OfType<StatusAppliedEvent>().Single(applied => applied.Kind == StatusKind.Mark);
            Assert.AreEqual(foe.Id, placed.ActorId);
            Assert.AreEqual(hero.Id, placed.SourceId);
            AssertShotOf(200, hero, run.Events.OfType<DamageEvent>().First(hit => hit.TargetId == foe.Id), "the first shot: no bonus yet");

            for (int bonus = 10; bonus <= 40; bonus += 10)
            {
                Assert.IsTrue(run.AttackAt(foe.Pos));
                Assert.AreEqual(bonus, MarkOn(run, foe), "each further shot adds 10%");
                var built = run.Events.OfType<MarkBuiltEvent>().Single();
                Assert.AreEqual(hero.Id, built.HunterId);
                Assert.AreEqual(foe.Id, built.ActorId);
                Assert.AreEqual(bonus, built.Power);
                Assert.AreEqual(100 + bonus, run.DamageTakenPercent(foe, hero));
                // The shot that adds to the mark already hits that much harder.
                int full = hero.Attack * 200 / 100 * CombatRules.RangedDamagePercent / 100 * (100 + bonus) / 100;
                var hit = run.Events.OfType<DamageEvent>().First(damage => damage.TargetId == foe.Id);
                Assert.That(hit.Amount, Is.InRange(full * CombatRules.SpreadMinPercent / 100 - 2, full + 1), $"a shot at +{bonus}%");
            }

            Assert.IsTrue(run.AttackAt(foe.Pos));
            Assert.AreEqual(40, MarkOn(run, foe), "40% is the most");
            Assert.IsEmpty(run.Events.OfType<MarkBuiltEvent>());
            Assert.AreEqual(100, run.DamageTakenPercent(foe, Dummy(run, 9, 1)), "only the hunter's own hits use it");
        }

        [Test]
        public void AShotCountsOnceHoweverManyArrowsTheBowLooses()
        {
            var run = MarksmanRun(Corridor);
            var foe = Target(run, 5, 1);
            Assert.IsTrue(run.AttackAt(foe.Pos));
            // Power Shot with the Hunter Bow: two arrows, both at the only foe in sight.
            Assert.IsTrue(run.UseSkillAt(Slot(run.Hero, SkillCatalog.PowerShot), foe.Pos));
            Assert.AreEqual(2, run.Events.OfType<DamageEvent>().Count(hit => hit.TargetId == foe.Id));
            Assert.AreEqual(10, MarkOn(run, foe));
        }

        [Test]
        public void AShotAimedAtAnotherFoeMovesTheMarkAndItStartsOver()
        {
            var run = MarksmanRun(Room);
            var first = Target(run, 5, 3);
            var second = Target(run, 5, 5);
            for (int shot = 0; shot < 3; shot++) Assert.IsTrue(run.AttackAt(first.Pos));
            Assert.AreEqual(20, MarkOn(run, first));

            Assert.IsTrue(run.AttackAt(second.Pos));
            Assert.AreEqual(-1, MarkOn(run, first), "one mark per hunter");
            Assert.AreEqual(0, MarkOn(run, second), "it starts over");
            CollectionAssert.Contains(run.Events.OfType<StatusEndedEvent>().Select(ended => ended.ActorId), first.Id);

            Assert.IsTrue(run.AttackAt(first.Pos));
            Assert.AreEqual(0, MarkOn(run, first), "and over again when he comes back");
        }

        [Test]
        public void OnlyAnAimedShotMovesTheMark()
        {
            var run = MarksmanRun(Room);
            var hero = run.Hero;
            var marked = Target(run, 6, 1);
            var near = Target(run, 4, 3);
            Assert.IsTrue(run.AttackAt(marked.Pos));
            Assert.IsTrue(run.AttackAt(marked.Pos));
            Assert.AreEqual(10, MarkOn(run, marked));

            // Rolling Shot rolls away and shoots the nearest foe, which the player didn't pick: the mark stays.
            Assert.IsTrue(run.UseSkill(Slot(hero, SkillCatalog.RollingShot), Direction8.S));
            CollectionAssert.Contains(run.Events.OfType<DamageEvent>().Select(hit => hit.TargetId), near.Id, "the roll's shot went to the nearest");
            Assert.AreEqual(10, MarkOn(run, marked));
            Assert.AreEqual(-1, MarkOn(run, near));

            // The second arrow of a Power Shot goes to another foe in sight: it doesn't take the mark along either.
            Assert.IsTrue(run.UseSkillAt(Slot(hero, SkillCatalog.PowerShot), marked.Pos));
            CollectionAssert.Contains(run.Events.OfType<DamageEvent>().Select(hit => hit.TargetId), near.Id);
            Assert.AreEqual(20, MarkOn(run, marked));
            Assert.AreEqual(-1, MarkOn(run, near));
        }

        [Test]
        public void ARollsShotAtTheMarkedFoeBuildsItsMark()
        {
            var run = MarksmanRun(Corridor);
            var foe = Target(run, 4, 1);
            Place(run.Hero, 3, 1);
            Assert.IsTrue(run.AttackAt(foe.Pos));
            Assert.IsTrue(run.UseSkill(Slot(run.Hero, SkillCatalog.RollingShot), Direction8.W));
            Assert.AreEqual(10, MarkOn(run, foe), "a shot of his on the marked foe, like any other");
        }

        [Test]
        public void TheVolleyUsesTheMarkAndLeavesItAsItIs()
        {
            var run = MarksmanRun(Room);
            var hero = run.Hero;
            var marked = Target(run, 5, 3);
            var other = Target(run, 5, 4);
            for (int shot = 0; shot < 3; shot++) Assert.IsTrue(run.AttackAt(marked.Pos));
            Assert.AreEqual(20, MarkOn(run, marked));
            hero.Charge = CombatRules.MaxCharge;
            Assert.IsTrue(run.UseUltimateAt(other.Pos));
            Assert.AreEqual(20, MarkOn(run, marked), "centered on another foe: the mark doesn't move or grow");
            int full = hero.Attack * SkillCatalog.Volley.Power / 100 * CombatRules.RangedDamagePercent / 100;
            var onMarked = run.Events.OfType<DamageEvent>().First(hit => hit.TargetId == marked.Id);
            var onOther = run.Events.OfType<DamageEvent>().First(hit => hit.TargetId == other.Id);
            Assert.That(onMarked.Amount, Is.GreaterThan(full), "the marked foe takes 20% more: above an unmarked hit's most");
            Assert.That(onOther.Amount, Is.LessThanOrEqualTo(full));
        }

        [Test]
        public void WhenTheMarkedFoeFallsTheMarkJumpsWithHalfOfWhatItHadBuiltUp()
        {
            var run = MarksmanRun(Room);
            var first = Target(run, 5, 3);
            var second = Target(run, 5, 5);
            for (int shot = 0; shot < 3; shot++) Assert.IsTrue(run.AttackAt(first.Pos));
            Assert.AreEqual(20, MarkOn(run, first));
            first.Hp = 1;
            Assert.IsTrue(run.AttackAt(first.Pos)); // The shot builds it to 30% and fells the foe.
            Assert.IsFalse(first.IsAlive);
            Assert.AreEqual(15, MarkOn(run, second));
            Assert.AreSame(second, run.MarkedBy(run.Hero, out var mark));
            Assert.AreEqual(SkillCatalog.HuntersMark.StatusTurns - 1, mark.TurnsLeft, "and lasts its full time there (that foe has had a turn since)");

            Assert.IsTrue(run.AttackAt(second.Pos));
            Assert.AreEqual(25, MarkOn(run, second), "his next shot builds on from there");
        }

        [Test]
        public void TheMarkFadesWhenHeLeavesItsFoeAlone()
        {
            var run = MarksmanRun(Corridor);
            var foe = Target(run, 5, 1);
            foe.Alerted = true;
            Assert.IsTrue(run.AttackAt(foe.Pos));
            Assert.IsTrue(run.AttackAt(foe.Pos));
            run.Wait();
            Assert.AreEqual(10, MarkOn(run, foe), "two of its turns since his shot: still there");
            Assert.IsTrue(run.AttackAt(foe.Pos));
            Assert.AreEqual(20, MarkOn(run, foe), "a shot builds it and it lasts its full time again");
            run.Wait();
            Assert.AreEqual(20, MarkOn(run, foe));
            run.Wait();
            Assert.AreEqual(-1, MarkOn(run, foe), "three of its turns without a shot: gone");
            Assert.IsNull(run.MarkedBy(run.Hero, out _));
        }

        [Test]
        public void WithoutThePickTheMarkIsTheQuickButtonItWas()
        {
            var run = Run(Archer(5, "archer_crippling_shot"), Corridor);
            var foe = Target(run, 5, 1);
            Assert.IsNull(DungeonRun.AlwaysOnMarkOf(run.Hero));
            Assert.IsTrue(run.AttackAt(foe.Pos));
            Assert.IsNull(foe.FindStatus(StatusKind.Mark), "a shot doesn't mark");
            Assert.IsTrue(run.UseSkillAt(Slot(run.Hero, SkillCatalog.HuntersMark), foe.Pos));
            Assert.AreEqual(25, MarkOn(run, foe));
            Assert.IsTrue(run.AttackAt(foe.Pos));
            Assert.AreEqual(25, MarkOn(run, foe), "and a shot doesn't build it");
        }

        [Test]
        public void TheAiStaysOnTheFoeThatCarriesItsMark()
        {
            var run = MarksmanRun(Room);
            var hero = run.Hero;
            var marked = Target(run, 5, 3);
            var weaker = Target(run, 5, 5);
            Assert.IsTrue(run.AttackAt(marked.Pos));
            weaker.Hp = marked.Hp - 1000; // Lower HP: without the mark the AI would go for this one.
            Assert.IsTrue(HeroTactics.TryAttack(run, hero, out var command));
            Assert.AreEqual(marked.Pos, command.Target, "the bonus is on the marked foe");
        }

        [Test]
        public void ANewSkillJoinsThePoolAndWaitsForASlot()
        {
            var uzuki = Archer(5, "archer_crippling_shot");
            CollectionAssert.Contains(uzuki.KnownSkills.Select(skill => skill.Id), "crippling_shot");
            CollectionAssert.AreEqual(new[] { "hunters_mark", "power_shot", "rolling_shot" }, uzuki.LoadoutIds, "the loadout was full: it stays");
            Assert.IsTrue(uzuki.Equip(2, SkillCatalog.CripplingShot));
            CollectionAssert.AreEqual(new[] { "hunters_mark", "power_shot", "crippling_shot" }, uzuki.LoadoutIds);
            Assert.AreEqual(EquipCheck.NotKnown, Archer(5, "archer_deadly_mark").CheckEquip(2, SkillCatalog.CripplingShot), "another pick: not learned");
        }

        [Test]
        public void CripplingShotSlowsWhatItHits()
        {
            var uzuki = Archer(5, "archer_crippling_shot");
            uzuki.Equip(2, SkillCatalog.CripplingShot);
            var run = Run(uzuki, Corridor);
            run.Hero.CritRate = 0;
            var foe = Target(run, 5, 1);
            foe.Alerted = true;
            run.Wait(); // The fight starts: delays only land on the timeline.
            Assert.IsTrue(run.InCombat);

            Assert.IsTrue(run.UseSkillAt(Slot(run.Hero, SkillCatalog.CripplingShot), foe.Pos));
            var delay = run.Events.OfType<TurnDelayedEvent>().Single();
            Assert.AreEqual(foe.Id, delay.ActorId);
            Assert.AreEqual(30, delay.Percent);
            Assert.IsFalse(delay.Stun, "a slow, not a stun");
            // The Hunter Bow doubles it like any shot of his: two arrows at 60%, and one slow.
            var hits = run.Events.OfType<DamageEvent>().Where(hit => hit.TargetId == foe.Id).ToList();
            Assert.AreEqual(2, hits.Count);
            foreach (var hit in hits) AssertShotOf(180 * 60 / 100, run.Hero, hit, "each arrow");
            StringAssert.Contains("Slows each foe hit: its next turn comes 30% of a turn later.", SkillText.Describe(run.Hero.Skills[2]));
        }

        [Test]
        public void CripplingShotSlowsABossByItsOwnBudget()
        {
            var uzuki = Archer(5, "archer_crippling_shot");
            uzuki.Equip(2, SkillCatalog.CripplingShot);
            var run = Run(uzuki, Corridor);
            var troll = run.SpawnEnemy(new GridPos(5, 1), ActorCatalog.Troll);
            troll.SpecialCooldown = 99;
            troll.Alerted = true;
            run.Wait();
            Assert.IsTrue(run.UseSkillAt(Slot(run.Hero, SkillCatalog.CripplingShot), troll.Pos));
            Assert.AreEqual(CombatRules.MaxBossDelayPercent, run.Events.OfType<TurnDelayedEvent>().Single().Percent);
        }

        [Test]
        public void BouncingShotIsOneArrowThatFliesOnFromFoeToFoe()
        {
            var uzuki = Archer(5, "archer_bouncing_shot");
            uzuki.Equip(2, SkillCatalog.BouncingShot);
            var run = Run(uzuki, Room); // Uzuki at (1, 3).
            run.Hero.CritRate = 0;
            var first = Target(run, 4, 3);
            var second = Target(run, 6, 4);  // Two tiles from the first.
            var third = Target(run, 9, 5);   // Three from the second, five from the first.
            int slot = Slot(run.Hero, SkillCatalog.BouncingShot);

            Assert.IsTrue(run.UseSkillAt(slot, first.Pos));
            var shots = ShotsBy(run, run.Hero);
            CollectionAssert.AreEqual(new[] { first.Id, second.Id, third.Id }, shots.Select(shot => shot.TargetId), "one arrow, three foes: no second arrow from the bow");
            Assert.IsNull(shots[0].From, "the first flies from Uzuki");
            Assert.AreEqual(first.Pos, shots[1].From);
            Assert.AreEqual(second.Pos, shots[2].From);
            Assert.AreEqual(2, shots[1].Distance);
            Assert.IsTrue(shots.All(shot => shot.Ranged));

            var hits = run.Events.OfType<DamageEvent>().ToList();
            AssertShotOf(200, run.Hero, hits.Single(hit => hit.TargetId == first.Id), "the shot");
            AssertShotOf(140, run.Hero, hits.Single(hit => hit.TargetId == second.Id), "the first bounce: 70% of 200");
            AssertShotOf(98, run.Hero, hits.Single(hit => hit.TargetId == third.Id), "the second bounce: 70% of 140");
            Assert.AreEqual(CombatRules.ChargePerAction + 3 * CombatRules.ChargePerHitDealt, run.Hero.Charge, "one action, three hits landed");

            string text = SkillText.Describe(run.Hero.Skills[slot]);
            StringAssert.Contains("A shot of 200% ATK at a foe in sight within 5 tiles.", text);
            StringAssert.Contains("bounces to up to 2 more foes, each within 3 tiles of the last one hit, for 70% of the hit before", text);
        }

        [Test]
        public void ABounceNeedsAnotherFoeNearAndInSightOfTheLast()
        {
            var uzuki = Archer(5, "archer_bouncing_shot");
            uzuki.Equip(2, SkillCatalog.BouncingShot);
            string[] walled =
            {
                "###########",
                "#.....#...#",
                "#.....#...#",
                "#@........#",
                "#.........#",
                "#.........#",
                "###########",
            };
            var run = Run(uzuki, walled); // Uzuki at (1, 3); a wall stands at (6, 4) and (6, 5).
            var first = Target(run, 4, 3);
            var tooFar = Target(run, 8, 2);     // Four tiles from the first.
            var behindWall = Target(run, 7, 5); // Three tiles from it, with the wall between.
            Assert.IsFalse(run.Map.HasLineOfSight(first.Pos, behindWall.Pos), "the test's wall is in the way");

            Assert.IsTrue(run.UseSkillAt(Slot(run.Hero, SkillCatalog.BouncingShot), first.Pos));
            CollectionAssert.AreEqual(new[] { first.Id }, ShotsBy(run, run.Hero).Select(shot => shot.TargetId), "nothing to bounce to: the arrow stops");
            Assert.AreEqual(tooFar.MaxHp, tooFar.Hp);
            Assert.AreEqual(behindWall.MaxHp, behindWall.Hp);
        }

        [Test]
        public void ABounceNeverComesBackToAFoeItHit()
        {
            var uzuki = Archer(5, "archer_bouncing_shot");
            uzuki.Equip(2, SkillCatalog.BouncingShot);
            var run = Run(uzuki, Room);
            var first = Target(run, 4, 3);
            var second = Target(run, 5, 3);
            Assert.IsTrue(run.UseSkillAt(Slot(run.Hero, SkillCatalog.BouncingShot), first.Pos));
            CollectionAssert.AreEqual(new[] { first.Id, second.Id }, ShotsBy(run, run.Hero).Select(shot => shot.TargetId));
        }

        // ---- What the party's AI does with them ----

        [Test]
        public void TheAiBouncesAnArrowOnlyWhereItHasSomewhereToGo()
        {
            var uzuki = Archer(5, "archer_bouncing_shot");
            uzuki.Equip(1, SkillCatalog.BouncingShot); // In Power Shot's place: Mark, Bouncing Shot, Rolling Shot.
            var run = Run(uzuki, Room);
            var alone = Target(run, 5, 3);
            int slot = Slot(run.Hero, SkillCatalog.BouncingShot);
            run.Hero.SkillCooldowns[0] = 9; // No mark in these: only the choice of shot.

            Assert.AreEqual(200, HeroTactics.PowerOn(run, run.Hero, run.Hero.Skills[slot], alone));
            Assert.AreEqual(HeroCommand.AttackAt(alone.Pos).ToString(), AutoPilot.Decide(run).ToString(), "one foe: no better than a Quick Shot, which is always ready");

            var near = Target(run, 6, 4);
            Assert.AreEqual(200 + 140, HeroTactics.PowerOn(run, run.Hero, run.Hero.Skills[slot], alone));
            var command = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommandKind.Skill, command.Kind);
            Assert.AreEqual(slot, command.Slot);
            Assert.IsTrue(run.Execute(command));
            Assert.AreEqual(2, ShotsBy(run, run.Hero).Count);
            Assert.Less(near.Hp, near.MaxHp);
        }

        [Test]
        public void TheAiSlowsASturdyFoeOncePerTurnOfIts()
        {
            var uzuki = Archer(5, "archer_crippling_shot");
            uzuki.Equip(2, SkillCatalog.CripplingShot);
            var run = Run(uzuki, Corridor);
            var foe = Target(run, 6, 1);
            foe.Alerted = true;
            run.Hero.SkillCooldowns[0] = 99; // No mark in this one.
            run.Wait();
            Assert.IsTrue(run.InCombat);
            int slot = Slot(run.Hero, SkillCatalog.CripplingShot);

            var command = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommandKind.Skill, command.Kind);
            Assert.AreEqual(slot, command.Slot, "a foe that will take several hits, and can be slowed: the slow first");
            Assert.IsTrue(run.Execute(command));
            Assert.AreEqual(1, run.Events.OfType<TurnDelayedEvent>().Count());

            if (foe.IsDelayed)
            {
                run.Hero.SkillCooldowns[slot] = 0;
                command = AutoPilot.Decide(run);
                Assert.IsFalse(command.Kind == HeroCommandKind.Skill && command.Slot == slot && PowerShotReady(run),
                    "it can't be slowed again before it acts: the hardest shot instead");
            }
        }

        static bool PowerShotReady(DungeonRun run) => run.Hero.SkillCooldowns[Slot(run.Hero, SkillCatalog.PowerShot)] == 0;

        [Test]
        public void TheAiCountsBothArrowsOfTheHunterBow()
        {
            var run = Run(Archer(1), Room);
            var foe = Target(run, 5, 3);
            Assert.AreEqual(360, HeroTactics.PowerOn(run, run.Hero, SkillCatalog.PowerShot, foe), "two arrows at 60% of 300");
            Assert.AreEqual(216, HeroTactics.PowerOn(run, run.Hero, SkillCatalog.CripplingShot, foe), "better than a Quick Shot's 200");
        }
    }
}

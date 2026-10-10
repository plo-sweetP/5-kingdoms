using System.Collections.Generic;
using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;
using static FiveKingdoms.Tests.PartyTests;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// Kristela's Fencer kit (PROGRESSION.md, "Kristela's Fencer kit"): Thrust, Triple Thrust, Lunge, Riposte and the
    /// ultimate Blade Dance, and what the party's AI does with them.
    /// </summary>
    public class FencerKitTests
    {
        static readonly string[] Room =
        {
            "##########",
            "#........#",
            "#........#",
            "#@.......#",
            "#........#",
            "#........#",
            "##########",
        };

        static readonly string[] Corridor =
        {
            "############",
            "#@.........#",
            "############",
        };

        /// <summary>A corridor (x 1-4) that opens into a room (x 5-8); (4, 2) is its last tile, the doorway.</summary>
        static readonly string[] Doorway =
        {
            "##########",
            "#####....#",
            "#@.......#",
            "#####....#",
            "##########",
        };

        static ActorDefinition[] Only(ActorDefinition hero) => new[] { hero };

        static int Slot(Actor hero, SkillDefinition skill)
        {
            var skills = hero.Skills;
            for (int i = 0; i < skills.Count; i++)
                if (skills[i].Id == skill.Id) return i;
            return -1;
        }

        static int HitsOn(DungeonRun run, Actor target) => run.Events.OfType<DamageEvent>().Count(hit => hit.TargetId == target.Id);

        /// <summary>Whom the hero's blows went to, in order.</summary>
        static int[] StruckBy(DungeonRun run, Actor attacker) =>
            run.Events.OfType<AttackEvent>().Where(attack => attack.AttackerId == attacker.Id).Select(attack => attack.TargetId).ToArray();

        /// <summary>Kristela alone in the corridor with a foe next to her, the fight on and her turn come up (at 100 AV).</summary>
        static DungeonRun Duel(out Actor foe, ActorDefinition foeDefinition = null, int foeSpeed = 0)
        {
            var run = Run(Only(ActorCatalog.Kristela), Corridor);
            run.Hero.MaxHp = run.Hero.Hp = 100000;
            foe = run.SpawnEnemy(new GridPos(2, 1), foeDefinition);
            foe.MaxHp = foe.Hp = 100000;
            foe.SpecialCooldown = 99; // A boss just bites: no slam in these.
            if (foeSpeed > 0) run.SetSpeed(foe, foeSpeed);
            run.Wait(); // It bites: the fight starts.
            Assert.IsTrue(run.InCombat);
            return run;
        }

        // ---- The kit ----

        [Test]
        public void ThrustIsHerWeaponAttack()
        {
            var run = Run(Only(ActorCatalog.Kristela), Corridor);
            var spider = Dummy(run, 2, 1);
            Assert.AreEqual("Thrust", run.Hero.AttackName);
            Assert.AreEqual(CombatRules.BasicAttackPercent, run.Hero.Kit.WeaponAttack.Power);
            Assert.IsTrue(run.Attack(Direction8.E));
            Assert.AreEqual(1, HitsOn(run, spider));
        }

        [Test]
        public void FencerSkillsNeedASwordInHand()
        {
            foreach (var skill in ActorCatalog.Kristela.Skills.Append(ActorCatalog.Kristela.Ultimate))
                Assert.AreEqual(WeaponFamily.Sword, skill.Weapon, skill.Name);
            Assert.IsTrue(new HeroProgress(ActorCatalog.Haiden).WeaponAllows(SkillCatalog.Lunge), "any sword will do: Haiden could learn them");
            Assert.IsFalse(new HeroProgress(ActorCatalog.Uzuki).WeaponAllows(SkillCatalog.Lunge));
        }

        [Test]
        public void AnOlderSaveOfHersLoadsWithTheFencerKit()
        {
            var saved = HeroProgress.Restore(ActorCatalog.Kristela, level: 6, exp: 3,
                new[] { new SavedClass("fencer", 1, new string[0]) },
                new[] { "piercing_punch", "ki_heal", "stun_strike" }, "flurry_of_blows");
            Assert.AreEqual("triple_thrust,lunge,riposte", string.Join(",", saved.Kit.Skills.Select(skill => skill.Id)));
            Assert.AreSame(SkillCatalog.BladeDance, saved.Kit.Ultimate);
            Assert.AreEqual(6, saved.Level);
        }

        // ---- Triple Thrust ----

        [Test]
        public void TripleThrustLandsThreeHitsOnOneFoe()
        {
            var run = Run(Only(ActorCatalog.Kristela), Room);
            var target = Dummy(run, 2, 3);
            var other = Dummy(run, 2, 2);
            Assert.IsTrue(run.UseSkillAt(Slot(run.Hero, SkillCatalog.TripleThrust), target.Pos));
            Assert.AreEqual(3, HitsOn(run, target));
            Assert.AreEqual(0, HitsOn(run, other));
            int bites = HitsOn(run, run.Hero);
            Assert.AreEqual(CombatRules.ChargePerAction + 3 * CombatRules.ChargePerHitDealt + bites * CombatRules.ChargePerHitTaken, run.Hero.Charge,
                "one action and three hits landed");
        }

        [Test]
        public void TripleThrustIsForOneFoeOnly()
        {
            var run = Run(Only(ActorCatalog.Kristela), Room);
            var target = Dummy(run, 2, 3, hp: 1);
            var other = Dummy(run, 2, 2);
            Assert.IsTrue(run.UseSkillAt(Slot(run.Hero, SkillCatalog.TripleThrust), target.Pos));
            Assert.IsFalse(target.IsAlive);
            Assert.AreEqual(1, StruckBy(run, run.Hero).Length, "the thrusts left over are lost (a Flurry would move on)");
            Assert.AreEqual(0, HitsOn(run, other));
        }

        // ---- Lunge ----

        [TestCase(4, 3)]
        [TestCase(3, 2)]
        [TestCase(2, 1)]
        public void LungeDashesToTheTileInFrontOfItsTargetAndStrikes(int foeX, int landsOnX)
        {
            var run = Run(Only(ActorCatalog.Kristela), Corridor);
            var spider = Dummy(run, foeX, 1);
            int lunge = Slot(run.Hero, SkillCatalog.Lunge);
            Assert.IsTrue(run.UseSkillAt(lunge, spider.Pos));
            Assert.AreEqual(new GridPos(landsOnX, 1), run.Hero.Pos);
            Assert.AreEqual(landsOnX == 1 ? 0 : 1, run.Events.OfType<DashedEvent>().Count(), "no dash when the foe is next to her already");
            Assert.AreEqual(1, HitsOn(run, spider));
            Assert.Greater(run.Hero.SkillCooldowns[lunge], 0);
            Assert.AreEqual(1, run.Turn, "a full turn");
        }

        [Test]
        public void LungeNeedsAFoeWithinThreeTilesInAStraightLine()
        {
            var run = Run(Only(ActorCatalog.Kristela), Room); // Kristela at (1, 3).
            int lunge = Slot(run.Hero, SkillCatalog.Lunge);
            var far = Dummy(run, 5, 3);
            var offLine = Dummy(run, 3, 4);
            var diagonal = Dummy(run, 3, 1);
            Assert.AreEqual(SkillCheck.NoTarget, run.CheckSkillAt(run.Hero, lunge, far.Pos), "4 tiles off");
            Assert.AreEqual(SkillCheck.NoTarget, run.CheckSkillAt(run.Hero, lunge, offLine.Pos), "not on one of the 8 lines");
            Assert.AreEqual(SkillCheck.Ready, run.CheckSkillAt(run.Hero, lunge, diagonal.Pos), "2 tiles off on the diagonal");
            Assert.IsFalse(run.UseSkillAt(lunge, far.Pos));
            Assert.AreEqual(0, run.Turn, "refused: no turn used");

            Assert.IsTrue(run.UseSkillAt(lunge, diagonal.Pos));
            Assert.AreEqual(new GridPos(2, 2), run.Hero.Pos);
        }

        [Test]
        public void NobodyMayStandInTheWayOfALunge()
        {
            var run = Run(new[] { ActorCatalog.Kristela, ActorCatalog.Haiden }, Room);
            int lunge = Slot(run.Hero, SkillCatalog.Lunge);
            Place(run.Party[1], 1, 4); // Haiden, north of her.
            var behindAnAlly = Dummy(run, 1, 5);
            var front = Dummy(run, 2, 3);
            var behindAFoe = Dummy(run, 4, 3);
            Assert.AreEqual(SkillCheck.NoTarget, run.CheckSkillAt(run.Hero, lunge, behindAnAlly.Pos));
            Assert.AreEqual(SkillCheck.NoTarget, run.CheckSkillAt(run.Hero, lunge, behindAFoe.Pos));
            Assert.AreEqual(SkillCheck.Ready, run.CheckSkillAt(run.Hero, lunge, front.Pos));
        }

        [Test]
        public void ALungeDoesNotCutAWallCorner()
        {
            var run = Run(Only(ActorCatalog.Kristela),
                "#####",
                "#@#.#",
                "#...#",
                "#...#",
                "#####");
            // Maps are drawn top row first and y runs up: she stands at (1, 3), the wall east of her at (2, 3).
            int lunge = Slot(run.Hero, SkillCatalog.Lunge);
            var pastTheCorner = Dummy(run, 3, 1);
            var straight = Dummy(run, 1, 1);
            Assert.AreEqual(SkillCheck.NoTarget, run.CheckSkillAt(run.Hero, lunge, pastTheCorner.Pos), "the wall east of her blocks the diagonal");
            Assert.AreEqual(SkillCheck.Ready, run.CheckSkillAt(run.Hero, lunge, straight.Pos));
        }

        [Test]
        public void AimingALungeShowsItsLinesAndTheFoesAtTheirEnds()
        {
            var run = Run(Only(ActorCatalog.Kristela), Room); // Kristela at (1, 3).
            var inLine = Dummy(run, 4, 3);
            Dummy(run, 5, 3); // Behind it: out of reach.
            Dummy(run, 3, 4); // Off the lines.
            var aim = run.AimFor(run.Hero, SkillCatalog.Lunge);
            Assert.AreEqual(1, aim.Options.Count);
            Assert.AreSame(inLine, aim.Options[0].Target);
            Assert.AreEqual(3, aim.Options[0].Distance);
            CollectionAssert.Contains(aim.Reach.ToList(), new GridPos(3, 3));
            CollectionAssert.Contains(aim.Reach.ToList(), new GridPos(1, 1), "two tiles south");
            CollectionAssert.DoesNotContain(aim.Reach.ToList(), new GridPos(5, 3));
            Assert.AreEqual(aim.Options[0].ToCommand(HeroCommandKind.Skill, 1), HeroCommand.SkillAt(1, inLine.Pos));
        }

        // ---- Riposte ----

        /// <summary>The damage of the foe's first hit after Kristela's second action, which is Riposte or (for comparison) a wait.</summary>
        static int HitTaken(bool riposte, ActorDefinition foeDefinition, out DungeonRun run, out Actor foe)
        {
            run = Duel(out foe, foeDefinition);
            if (riposte) Assert.IsTrue(run.UseSkill(Slot(run.Hero, SkillCatalog.Riposte)));
            else run.Wait();
            var hero = run.Hero;
            return run.Events.OfType<DamageEvent>().First(hit => hit.TargetId == hero.Id).Amount;
        }

        [Test]
        public void RiposteHalvesTheDamageSheTakes()
        {
            int plain = HitTaken(false, null, out _, out _);
            int inStance = HitTaken(true, null, out _, out _);
            Assert.AreEqual(plain * 50 / 100, inStance, "the same roll, halved");
        }

        [Test]
        public void RiposteTakesOnlyAQuarterOffABosssHit()
        {
            int plain = HitTaken(false, ActorCatalog.Troll, out _, out _);
            int inStance = HitTaken(true, ActorCatalog.Troll, out var run, out var troll);
            Assert.AreEqual(plain * 75 / 100, inStance);
            Assert.AreEqual(1, run.Events.OfType<CounterEvent>().Count(), "a boss is struck back too");
            Assert.Less(troll.Hp, troll.MaxHp);
        }

        [Test]
        public void TheFirstFoeToHitHerIsStruckBackAndOnlyThatOnce()
        {
            // A fast spider (200) bites twice before her next turn.
            var run = Duel(out var spider, foeSpeed: 200);
            var kristela = run.Hero;
            int charge = kristela.Charge;
            Assert.IsTrue(run.UseSkill(Slot(kristela, SkillCatalog.Riposte)));

            Assert.AreEqual(2, HitsOn(run, kristela), "two bites in her stance");
            var counter = run.Events.OfType<CounterEvent>().Single();
            Assert.AreEqual(kristela.Id, counter.ActorId);
            Assert.AreEqual(spider.Id, counter.TargetId);
            Assert.AreEqual(1, HitsOn(run, spider), "one counter per stance");
            Assert.AreEqual(charge + CombatRules.ChargePerAction + CombatRules.ChargePerHitDealt + 2 * CombatRules.ChargePerHitTaken, kristela.Charge,
                "the counter charges the meter as a hit landed");
            Assert.IsNull(kristela.FindStatus(StatusKind.Riposte), "the stance ends as her turn comes up");
            Assert.AreEqual(1, run.Events.OfType<StatusEndedEvent>().Count(ended => ended.Kind == StatusKind.Riposte));
        }

        [Test]
        public void TheCounterHitsForTwoAndAHalfTimesHerAttack()
        {
            var run = Duel(out var spider);
            var kristela = run.Hero;
            spider.Defense = 0;
            kristela.CritRate = 0;
            Assert.IsTrue(run.UseSkill(Slot(kristela, SkillCatalog.Riposte)));
            int dealt = run.Events.OfType<DamageEvent>().Single(hit => hit.TargetId == spider.Id).Amount;
            int full = kristela.Attack * 250 / 100;
            Assert.That(dealt, Is.InRange(full * CombatRules.SpreadMinPercent / 100 - 1, full));
        }

        [Test]
        public void ACounterCanFellAFoeOnItsOwnTurn()
        {
            var run = Duel(out var spider);
            spider.Hp = 1;
            Assert.IsTrue(run.UseSkill(Slot(run.Hero, SkillCatalog.Riposte)));
            Assert.IsFalse(spider.IsAlive);
            CollectionAssert.DoesNotContain(run.Actors.ToList(), spider);
            Assert.IsFalse(run.InCombat, "the fight is over");
            Assert.AreEqual(RunState.InProgress, run.State);
        }

        [Test]
        public void RiposteJustHappensAndCostsAFullTurn()
        {
            Assert.IsFalse(SkillCatalog.Riposte.NeedsAim);
            Assert.IsFalse(SkillCatalog.Riposte.IsQuick);
            var run = Duel(out _);
            Assert.AreEqual(AvTime.FromWhole(100), run.Forecast(1)[0].Time);
            Assert.IsTrue(run.UseSkill(Slot(run.Hero, SkillCatalog.Riposte)));
            Assert.AreEqual(AvTime.FromWhole(200), run.Forecast(1)[0].Time);
        }

        // ---- Blade Dance ----

        /// <summary>Kristela at (1, 3) with a full meter and a Troll next to her at (2, 3) that only bites.</summary>
        static DungeonRun BossDance(out Actor troll)
        {
            var run = Run(Only(ActorCatalog.Kristela), Room);
            run.Hero.MaxHp = run.Hero.Hp = 100000;
            run.Hero.Charge = CombatRules.MaxCharge;
            troll = run.SpawnEnemy(new GridPos(2, 3), ActorCatalog.Troll);
            troll.SpecialCooldown = 99;
            return run;
        }

        [Test]
        public void BladeDanceGivesABossAloneAllFiveStrikes()
        {
            var run = BossDance(out var troll);
            var kristela = run.Hero;
            Assert.IsTrue(run.UseUltimateAt(troll.Pos));
            Assert.AreEqual(5, HitsOn(run, troll));
            Assert.AreEqual(new GridPos(1, 3), kristela.Pos, "she doesn't move");
            Assert.AreEqual(HitsOn(run, kristela) * CombatRules.ChargePerHitTaken, kristela.Charge, "its own hits don't charge the meter");
            var area = run.Events.OfType<AreaAttackEvent>().Single();
            Assert.AreEqual(troll.Pos, area.Center);
            Assert.AreEqual(1, area.Radius);
        }

        [Test]
        public void ABossWithTwoMinionsTakesTheFirstAndTheFourthStrike()
        {
            var run = BossDance(out var troll);
            var first = Dummy(run, 3, 2);
            var second = Dummy(run, 3, 4);
            Assert.IsTrue(run.UseUltimateAt(troll.Pos));
            CollectionAssert.AreEqual(new[] { troll.Id, first.Id, second.Id, troll.Id, first.Id }, StruckBy(run, run.Hero));
        }

        [Test]
        public void WhenTheMinionsFallTheBossTakesTheRest()
        {
            var run = BossDance(out var troll);
            var first = Dummy(run, 3, 2, hp: 1);
            var second = Dummy(run, 3, 4, hp: 1);
            Assert.IsTrue(run.UseUltimateAt(troll.Pos));
            CollectionAssert.AreEqual(new[] { troll.Id, first.Id, second.Id, troll.Id, troll.Id }, StruckBy(run, run.Hero));
        }

        [Test]
        public void WithoutABossTheFoeSheAimsAtComesFirstThenTheHardestHitter()
        {
            var run = Run(Only(ActorCatalog.Kristela), Room);
            run.Hero.Charge = CombatRules.MaxCharge;
            var weak = Dummy(run, 2, 4);
            var strong = Dummy(run, 3, 3, attack: 5);
            var aimed = Dummy(run, 2, 3);
            var outside = Dummy(run, 4, 5);
            Assert.IsTrue(run.UseUltimateAt(aimed.Pos));
            CollectionAssert.AreEqual(new[] { aimed.Id, strong.Id, weak.Id, aimed.Id, strong.Id }, StruckBy(run, run.Hero));
            Assert.AreEqual(0, HitsOn(run, outside), "outside the 3x3 around the foe she aimed at");
        }

        [Test]
        public void BladeDanceIsAimedAtAFoeNextToHer()
        {
            var run = Run(Only(ActorCatalog.Kristela), Room);
            run.Hero.Charge = CombatRules.MaxCharge;
            var apart = Dummy(run, 3, 3);
            Assert.AreEqual(SkillCheck.NoTarget, run.CheckUltimateAt(run.Hero, apart.Pos));
            var near = Dummy(run, 2, 3);
            Assert.AreEqual(SkillCheck.Ready, run.CheckUltimateAt(run.Hero, near.Pos));
            var aim = run.AimFor(run.Hero, SkillCatalog.BladeDance);
            Assert.AreEqual(1, aim.AreaRadius, "the targeting shows the 3x3 around each target");
            Assert.AreSame(near, aim.Options.Single().Target);
        }

        // ---- What the party's AI does with the kit ----

        [Test]
        public void OnAutoSheAlternatesTripleThrustAndRiposte()
        {
            var run = Run(Only(ActorCatalog.Kristela), Corridor);
            var kristela = run.Hero;
            kristela.MaxHp = kristela.Hp = 100000;
            var spider = Dummy(run, 2, 1);
            int thrusts = Slot(kristela, SkillCatalog.TripleThrust), stance = Slot(kristela, SkillCatalog.Riposte);

            Assert.AreEqual(HeroCommand.SkillAt(thrusts, spider.Pos), AutoPilot.Decide(run), "whenever it is ready and a foe is next to her");
            Assert.IsTrue(run.Execute(AutoPilot.Decide(run)));
            Assert.AreEqual(HeroCommand.Skill(stance), AutoPilot.Decide(run), "the spider bites before her next turn");
            Assert.IsTrue(run.Execute(AutoPilot.Decide(run)));
            Assert.AreEqual(1, run.Events.OfType<CounterEvent>().Count());
            Assert.AreEqual(HeroCommand.SkillAt(thrusts, spider.Pos), AutoPilot.Decide(run));
        }

        /// <summary>Haiden leads at (1, 3); Kristela stands apart from him at (4, 3) with a spider next to her at (5, 3); her Triple Thrust has just been used.</summary>
        static DungeonRun ApartFromHaiden(out Actor kristela, out Actor spider)
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela }, Room);
            kristela = run.Party[1];
            Place(kristela, 4, 3);
            spider = Dummy(run, 5, 3);
            kristela.SkillCooldowns[Slot(kristela, SkillCatalog.TripleThrust)] = 1;
            return run;
        }

        [Test]
        public void APartnerTakesTheStanceWhenAFoeNextToHerIsAboutToHitHer()
        {
            var run = ApartFromHaiden(out var kristela, out _);
            Assert.AreEqual(HeroCommand.Skill(Slot(kristela, SkillCatalog.Riposte)), PartnerBrain.Decide(run, kristela));
        }

        [Test]
        public void SheDoesNotRiposteAFoeHeldByHaidensTaunt()
        {
            var run = ApartFromHaiden(out var kristela, out var spider);
            spider.Statuses.Add(new StatusEffect(StatusKind.Taunt, run.Hero.Id, 0, 2, endsOnSourceTurn: false));
            Assert.AreEqual(HeroCommand.AttackAt(spider.Pos), PartnerBrain.Decide(run, kristela), "it is after Haiden: she thrusts instead");
        }

        [Test]
        public void SheDoesNotRiposteAFoeThatGoesForHaidenNextToIt()
        {
            var run = ApartFromHaiden(out var kristela, out var spider);
            Place(kristela, 2, 2);
            Place(spider, 2, 3); // Next to both; a monster bites the first of the nearest, and that is Haiden.
            Assert.AreSame(run.Hero, EnemyBrain.TargetOf(run, spider));
            Assert.AreEqual(HeroCommand.AttackAt(spider.Pos), PartnerBrain.Decide(run, kristela));
        }

        [Test]
        public void InsideASlamSheTakesTheStanceOnlyWithNoWayOut()
        {
            // Her back to the wall, a boss winding up in front of her: nowhere to step out to, so she meets the slam
            // in her stance (PROGRESSION.md, "Footing in a boss fight"), Triple Thrust ready or not.
            var run = Run(Only(ActorCatalog.Kristela), Corridor);
            var kristela = run.Hero;
            var troll = run.SpawnEnemy(new GridPos(2, 1), ActorCatalog.Troll);
            troll.Charging = true;
            Assert.AreEqual(HeroCommand.Skill(Slot(kristela, SkillCatalog.Riposte)), AutoPilot.Decide(run));

            // With room behind her she steps out instead.
            Place(kristela, 2, 1);
            Place(troll, 3, 1);
            Assert.AreEqual(HeroCommand.Move(Direction8.W), AutoPilot.Decide(run));
        }

        [Test]
        public void SheLungesAtAFoeInsteadOfWalkingUpToIt()
        {
            var run = Run(Only(ActorCatalog.Kristela), Room);
            var spider = Dummy(run, 4, 3);
            int lunge = Slot(run.Hero, SkillCatalog.Lunge);
            Assert.AreEqual(HeroCommand.SkillAt(lunge, spider.Pos), AutoPilot.Decide(run));

            run.Hero.SkillCooldowns[lunge] = 1;
            Assert.AreEqual(HeroCommandKind.Move, AutoPilot.Decide(run).Kind, "while it rests she walks");
        }

        [Test]
        public void APartnerLungesIntoTheFightToo()
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela }, Room);
            var kristela = run.Party[1];
            Place(kristela, 1, 2);
            var spider = Dummy(run, 4, 2);
            Assert.AreEqual(HeroCommand.SkillAt(Slot(kristela, SkillCatalog.Lunge), spider.Pos), PartnerBrain.Decide(run, kristela));
        }

        [Test]
        public void SheDoesNotLungeOutOfADoorwaySheShouldHold()
        {
            var run = Run(Only(ActorCatalog.Kristela), Doorway);
            var kristela = run.Hero;
            Place(kristela, 4, 2);
            var inLine = Dummy(run, 6, 2);
            Dummy(run, 6, 1);
            Dummy(run, 6, 3);
            Assert.AreEqual(SkillCheck.Ready, run.CheckSkillAt(kristela, Slot(kristela, SkillCatalog.Lunge), inLine.Pos), "she could");
            Assert.AreEqual(HeroCommand.HoldTheDoor, AutoPilot.Decide(run), "three of them beyond the doorway: let them come");
        }

        [Test]
        public void AgainstOneFoeBeyondTheDoorwaySheLungesIn()
        {
            var run = Run(Only(ActorCatalog.Kristela), Doorway);
            var kristela = run.Hero;
            Place(kristela, 4, 2);
            var spider = Dummy(run, 7, 2);
            Assert.AreEqual(HeroCommand.SkillAt(Slot(kristela, SkillCatalog.Lunge), spider.Pos), AutoPilot.Decide(run));
        }

        [Test]
        public void SheWalksToTheEndOfACorridorBeforeADashOutOfIt()
        {
            var run = Run(Only(ActorCatalog.Kristela), Doorway);
            var kristela = run.Hero;
            Place(kristela, 3, 2);
            Dummy(run, 6, 2);
            Assert.AreEqual(HeroCommand.Move(Direction8.E), AutoPilot.Decide(run), "the doorway is where she decides whether to go in");
        }

        [Test]
        public void OnAutoBladeDanceWaitsForABossOrTwoFoes()
        {
            var run = Run(Only(ActorCatalog.Kristela), Room);
            run.Hero.Charge = CombatRules.MaxCharge;
            var spider = Dummy(run, 2, 3);
            Assert.AreNotEqual(HeroCommandKind.Ultimate, AutoPilot.Decide(run).Kind, "one spider isn't worth it");

            Dummy(run, 3, 4);
            Assert.AreEqual(HeroCommand.UltimateAt(spider.Pos), AutoPilot.Decide(run), "two in the area");
        }

        [Test]
        public void OnAutoABossAloneIsWorthABladeDance()
        {
            var run = BossDance(out var troll);
            Assert.AreEqual(HeroCommand.UltimateAt(troll.Pos), AutoPilot.Decide(run));
        }

        [Test]
        public void WithNoHealOfHerOwnSheGoesToHaidenBetweenFights()
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela }, Room);
            var kristela = run.Party[1];
            Place(kristela, 6, 3);
            kristela.Hp = kristela.MaxHp / 2;
            Assert.AreSame(run.Hero, HeroTactics.HealerFor(run, kristela));
            Assert.AreEqual(HeroCommandKind.Move, PartnerBrain.Decide(run, kristela).Kind);
        }
    }
}

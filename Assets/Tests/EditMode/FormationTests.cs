using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;
using static FiveKingdoms.Tests.PartyTests;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// PROGRESSION.md, "Ranged vs melee": ranged hits are a little weaker (and weaker still at point-blank range), ranged
    /// heroes step out of melee and hang back, melee partners close in and may swap past a ranged ally, a badly hurt hero
    /// may swap back behind a healthier one ("run to safety"), nobody loops, and floors place monsters in packs.
    /// </summary>
    public class FormationTests
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
            "##########",
            "#@.......#",
            "##########",
        };

        static void HoldAll(DungeonRun run)
        {
            foreach (var member in run.Party) run.SetTactic(member, PartyTactic.Hold);
        }

        static void Root(Actor actor) => actor.Statuses.Add(new StatusEffect(StatusKind.Rooted, actor.Id, 0, 999, endsOnSourceTurn: false));

        [Test]
        public void RangedHitsDealNineTenthsAndLessAtPointBlankRange()
        {
            Assert.AreEqual(100, CombatRules.ReachPercent(ranged: false, pointBlank: false));
            Assert.AreEqual(90, CombatRules.ReachPercent(ranged: true, pointBlank: false));
            Assert.AreEqual(63, CombatRules.ReachPercent(ranged: true, pointBlank: true), "90% of a melee hit, then 30% less");
        }

        [Test]
        public void AShotDealsNineTenthsOfTheSameHitInMelee()
        {
            // Two fighters the same but for their reach, with the same rolls.
            ActorDefinition Fighter(int attackRange) => new ActorDefinition("fighter", "Fighter", maxHp: 400, attack: 100, defense: 30,
                expReward: 0, critRate: 0, attackRange: attackRange);
            int Hit(int attackRange)
            {
                var run = Run(new[] { Fighter(attackRange) }, Corridor);
                var target = Dummy(run, 3, 1); // Two tiles away: not point-blank.
                if (attackRange == 1) Place(run.Hero, 2, 1);
                run.AttackAt(target.Pos);
                return run.Events.OfType<DamageEvent>().First(hit => hit.TargetId == target.Id).Amount;
            }
            int melee = Hit(1);
            Assert.AreEqual(melee * 90 / 100, Hit(5), 2);
        }

        [Test]
        public void AShotWithAFoeNextToTheShooterDealsLess()
        {
            int Shot(bool crowded)
            {
                var run = Run(new[] { ActorCatalog.Uzuki }, Room); // Uzuki at (1, 3).
                run.Hero.CritRate = 0;
                var target = Dummy(run, 4, 3);
                if (crowded) Dummy(run, 1, 2);
                run.Attack(Direction8.E);
                return run.Events.OfType<DamageEvent>().Single(hit => hit.TargetId == target.Id).Amount;
            }
            int clear = Shot(false);
            Assert.AreEqual(clear * 70 / 100, Shot(true), 2);
        }

        [Test]
        public void AnArcherStepsBackToATileSheCanStillShootFrom()
        {
            var run = Run(new[] { ActorCatalog.Uzuki }, Room);
            var uzuki = run.Hero;
            Place(uzuki, 3, 3);
            Dummy(run, 4, 3);
            uzuki.SkillCooldowns[2] = 1; // Rolling Shot is cooling down.

            var command = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommandKind.Move, command.Kind, "back out of melee");
            var next = uzuki.Pos + command.Direction.ToOffset();
            Assert.IsFalse(run.FoeAdjacent(next, uzuki.Team));
            Assert.IsTrue(run.AnyFoeInSight(next, uzuki.Team, 5), "with the foe still in sight");
            run.Execute(command);
            Assert.AreEqual(next, uzuki.Pos);
            Assert.AreEqual(1, uzuki.RetreatSteps);
        }

        [Test]
        public void IfTheFoeKeepsFollowingTheArcherShootsAnyway()
        {
            var run = Run(new[] { ActorCatalog.Uzuki }, Room);
            var uzuki = run.Hero;
            Place(uzuki, 3, 3);
            var slime = Dummy(run, 4, 3);
            for (int slot = 0; slot < uzuki.SkillCooldowns.Length; slot++) uzuki.SkillCooldowns[slot] = 1;
            uzuki.RetreatSteps = 1; // She already stepped back once and it followed.

            Assert.AreEqual(HeroCommand.AttackAt(slime.Pos), AutoPilot.Decide(run), "a point-blank Quick Shot rather than retreating forever");
        }

        [Test]
        public void MeleePartnersCloseInOnAFoeInAFight()
        {
            var run = Run(new[] { ActorCatalog.Uzuki, ActorCatalog.Haiden }, Room);
            var haiden = run.Party[1];
            run.SetTactic(haiden, PartyTactic.Follow);
            Place(run.Hero, 1, 3);
            Place(haiden, 1, 4);
            var slime = Dummy(run, 6, 3);
            Root(slime);

            run.Wait(); // The slime notices them: the fight starts.
            Assert.IsTrue(run.InCombat);
            int before = GridPos.ChebyshevDistance(haiden.Pos, slime.Pos);
            run.Wait();
            Assert.Less(GridPos.ChebyshevDistance(haiden.Pos, slime.Pos), before, "Follow or not, he goes in");
        }

        [Test]
        public void AMeleePartnerSwapsPastARangedAllyToReachAFoe()
        {
            var run = Run(new[] { ActorCatalog.Uzuki, ActorCatalog.Kristela }, Corridor);
            var uzuki = run.Hero;
            var kristela = run.Party[1];
            Place(kristela, 1, 1);
            Place(uzuki, 2, 1);
            Root(Dummy(run, 4, 1));
            HoldAll(run);
            run.Wait(); // The fight starts.
            Assert.IsTrue(run.InCombat);
            run.SetTactic(kristela, PartyTactic.Follow);

            run.Wait();
            Assert.AreEqual(new GridPos(2, 1), kristela.Pos);
            Assert.AreEqual(new GridPos(1, 1), uzuki.Pos);
            Assert.IsTrue(run.Events.OfType<SwappedEvent>().Any(e => e.ActorId == kristela.Id && e.OtherId == uzuki.Id));
        }

        [Test]
        public void HealthyMeleeHeroesDontSwapWithEachOther()
        {
            var run = Run(new[] { ActorCatalog.Kristela, ActorCatalog.Haiden }, Corridor);
            var kristela = run.Hero;
            var haiden = run.Party[1];
            Place(haiden, 1, 1);
            Place(kristela, 2, 1);
            Root(Dummy(run, 4, 1));
            Assert.IsFalse(run.CanSwap(haiden, kristela));
            Assert.IsFalse(run.CanSwap(kristela, haiden));

            run.Wait();
            run.Wait();
            Assert.AreEqual(new GridPos(1, 1), haiden.Pos, "he waits behind her");
        }

        /// <summary>Haiden behind Kristela in a corridor, a slime in her face, her Ki Heal just used.</summary>
        static DungeonRun HurtMonkInFront(ActorDefinition[] members, out Actor kristela, out Actor haiden)
        {
            var run = Run(members, Corridor);
            kristela = run.Party.First(member => member.Definition == ActorCatalog.Kristela);
            haiden = run.Party.First(member => member.Definition == ActorCatalog.Haiden);
            Place(haiden, 2, 1);
            Place(kristela, 3, 1);
            Root(Dummy(run, 4, 1));
            kristela.Hp = kristela.MaxHp * 25 / 100;
            kristela.SkillCooldowns[1] = 1; // Ki Heal.
            return run;
        }

        [Test]
        public void ABadlyHurtHeroMaySwapBehindAHealthierAllyEvenAMeleeOne()
        {
            var run = HurtMonkInFront(new[] { ActorCatalog.Kristela, ActorCatalog.Haiden }, out var kristela, out var haiden);
            Assert.IsTrue(DungeonRun.IsBadlyHurt(kristela), "under 30%");
            Assert.IsTrue(run.CanSwap(kristela, haiden), "run to safety: he stands farther from the slime");
            Assert.IsFalse(run.CanSwap(haiden, kristela), "he isn't hurt, and melee heroes don't swap forward past each other");

            haiden.Hp = haiden.MaxHp * 25 / 100;
            Assert.IsFalse(run.CanSwap(kristela, haiden), "not with an ally as badly hurt");
            haiden.Hp = haiden.MaxHp;
            kristela.Hp = kristela.MaxHp * 30 / 100;
            Assert.IsFalse(run.CanSwap(kristela, haiden), "at 30% she isn't badly hurt");
        }

        [Test]
        public void ASwapToSafetyNeedsTheAllyToStandFartherFromTheFoes()
        {
            var run = Run(new[] { ActorCatalog.Kristela, ActorCatalog.Haiden }, Room);
            var kristela = run.Hero;
            var haiden = run.Party[1];
            Place(kristela, 3, 3);
            Place(haiden, 3, 4); // Beside her, and also next to the slime.
            Dummy(run, 4, 3);
            kristela.Hp = 1;
            Assert.IsFalse(run.CanSwap(kristela, haiden), "his tile is no safer");
            Assert.IsFalse(HeroTactics.TryRunToSafety(run, kristela, out _));

            Place(haiden, 2, 3);
            Assert.IsTrue(run.CanSwap(kristela, haiden));
        }

        [Test]
        public void ABadlyHurtPartnerRunsToSafety()
        {
            var run = HurtMonkInFront(new[] { ActorCatalog.Uzuki, ActorCatalog.Kristela, ActorCatalog.Haiden }, out var kristela, out var haiden);
            Place(run.Hero, 1, 1);
            Assert.AreEqual(HeroCommand.Move(Direction8.W), PartnerBrain.Decide(run, kristela), "back behind Haiden");

            run.Wait();
            Assert.AreEqual(new GridPos(2, 1), kristela.Pos);
            Assert.AreEqual(new GridPos(3, 1), haiden.Pos, "he takes the front");
            Assert.IsTrue(run.Events.OfType<SwappedEvent>().Any(e => e.ActorId == kristela.Id && e.OtherId == haiden.Id));
            Assert.IsFalse(run.CanSwap(kristela, haiden), "the two can't swap again for a few turns");
            Assert.IsFalse(run.CanSwap(haiden, kristela));
        }

        [Test]
        public void TheAutoPilotRunsABadlyHurtLeaderToSafety()
        {
            var run = HurtMonkInFront(new[] { ActorCatalog.Kristela, ActorCatalog.Haiden }, out var kristela, out var haiden);
            Assert.AreSame(kristela, run.Hero);
            Assert.AreEqual(HeroCommand.Move(Direction8.W), AutoPilot.Decide(run));
            run.Execute(AutoPilot.Decide(run));
            Assert.AreEqual(new GridPos(2, 1), kristela.Pos);
            Assert.AreEqual(new GridPos(3, 1), haiden.Pos);
        }

        [Test]
        public void AHurtHeroHealsRatherThanRunsWhenItCan()
        {
            var run = HurtMonkInFront(new[] { ActorCatalog.Kristela, ActorCatalog.Haiden }, out var kristela, out _);
            kristela.SkillCooldowns[1] = 0;
            var command = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommandKind.Skill, command.Kind);
            Assert.AreSame(SkillCatalog.KiHeal, kristela.Definition.Skills[command.Slot]);
        }

        [Test]
        public void ASwapMustBringTheMeleeHeroCloserAndCantBeUndoneRightAway()
        {
            var run = Run(new[] { ActorCatalog.Uzuki, ActorCatalog.Kristela }, Corridor);
            var uzuki = run.Hero;
            var kristela = run.Party[1];
            Place(uzuki, 2, 1);
            Place(kristela, 3, 1);
            Dummy(run, 5, 1);
            Assert.IsFalse(run.CanSwap(kristela, uzuki), "Uzuki's tile is farther from the slime");

            Place(uzuki, 3, 1);
            Place(kristela, 2, 1);
            Assert.IsTrue(run.CanSwap(kristela, uzuki));
            kristela.SwappedWithId = uzuki.Id;
            kristela.SwapBlockTurns = DungeonRun.SwapBlockTurns;
            Assert.IsFalse(run.CanSwap(kristela, uzuki), "the two just swapped");
            Assert.IsFalse(run.CanSwap(uzuki, kristela), "and a ranged hero never swaps forward");
        }

        [Test]
        public void ARangedPartnerFindsATileToShootFromAwayFromTheFoe()
        {
            // A wall down the middle of the room hides the slime from her.
            var run = Run(new[] { ActorCatalog.Kristela, ActorCatalog.Uzuki },
                "##########",
                "#........#",
                "#...#....#",
                "#@..#....#",
                "#...#....#",
                "#........#",
                "##########");
            var uzuki = run.Party[1];
            Place(run.Hero, 1, 3);
            Place(uzuki, 4, 5);
            var slime = Dummy(run, 6, 3);
            Root(slime);
            HoldAll(run);
            run.Wait(); // The fight starts.
            Assert.IsTrue(run.InCombat);
            run.SetTactic(uzuki, PartyTactic.Follow);
            Assert.IsFalse(run.AnyFoeInSight(uzuki.Pos, uzuki.Team, 5), "no shot from where she stands: the wall's corner is in the way");

            var command = PartnerBrain.Decide(run, uzuki);
            Assert.AreEqual(HeroCommandKind.Move, command.Kind);
            var next = uzuki.Pos + command.Direction.ToOffset();
            Assert.IsTrue(run.AnyFoeInSight(next, uzuki.Team, 5), "a shot from there");
            Assert.IsFalse(run.FoeAdjacent(next, uzuki.Team), "without walking into melee");
        }

        [Test]
        public void FloorsPlaceMonstersInPacksAndSomePacksBringABat()
        {
            int bats = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var run = new DungeonRun(seed, new DungeonRunConfig { Party = ActorCatalog.StartingParty });
                var enemies = run.Actors.Where(actor => actor.Team == Team.Enemy).ToList();
                Assert.That(enemies.Count, Is.InRange(1, run.Config.EnemiesOnFirstFloor), $"seed {seed}");
                int alone = enemies.Count(enemy => !enemies.Any(other => other != enemy && GridPos.ChebyshevDistance(other.Pos, enemy.Pos) <= 2));
                Assert.LessOrEqual(alone, 1, $"seed {seed}: at most one leftover monster on its own");
                bats += enemies.Count(enemy => enemy.Definition == ActorCatalog.Bat);
            }
            Assert.Greater(bats, 0, "fast monsters show up");
        }

        [Test]
        public void TheBatIsFastAndFrail()
        {
            Assert.AreEqual(130, ActorCatalog.Bat.Speed);
            Assert.Less(ActorCatalog.Bat.MaxHp, ActorCatalog.Slime.MaxHp);
        }
    }
}

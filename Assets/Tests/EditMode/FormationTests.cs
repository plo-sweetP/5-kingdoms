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
    /// The party stays together: partners wait behind the ally that holds a corridor instead of walking around the
    /// floor, fight near the leader, and get past each other when a corridor would split them.
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

        /// <summary>A ring of corridors: from the bottom right corner, the far side of the right-hand one is a long walk around.</summary>
        static readonly string[] Ring =
        {
            "######",
            "#....#",
            "#.##.#",
            "#.##.#",
            "#.##.#",
            "#@...#",
            "######",
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
            var spider = Dummy(run, 4, 3);
            for (int slot = 0; slot < uzuki.SkillCooldowns.Length; slot++) uzuki.SkillCooldowns[slot] = 1;
            uzuki.RetreatSteps = 1; // He already stepped back once and it followed.

            Assert.AreEqual(HeroCommand.AttackAt(spider.Pos), AutoPilot.Decide(run), "a point-blank Quick Shot rather than retreating forever");
        }

        [Test]
        public void MeleePartnersCloseInOnAFoeInAFight()
        {
            var run = Run(new[] { ActorCatalog.Uzuki, ActorCatalog.Haiden }, Room);
            var haiden = run.Party[1];
            run.SetTactic(haiden, PartyTactic.Follow);
            Place(run.Hero, 1, 3);
            Place(haiden, 1, 4);
            var spider = Dummy(run, 6, 3);
            Root(spider);

            run.Wait(); // The spider notices them: the fight starts.
            Assert.IsTrue(run.InCombat);
            int before = GridPos.ChebyshevDistance(haiden.Pos, spider.Pos);
            run.Wait();
            Assert.Less(GridPos.ChebyshevDistance(haiden.Pos, spider.Pos), before, "Follow or not, he goes in");
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

        /// <summary>Haiden behind Kristela in a corridor, a spider in her face, her Ki Heal just used.</summary>
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
            Assert.IsTrue(run.CanSwap(kristela, haiden), "run to safety: he stands farther from the spider");
            Assert.IsFalse(run.CanSwap(haiden, kristela), "he isn't hurt, and a partner never takes the front from the leader");

            haiden.Hp = haiden.MaxHp * 25 / 100;
            Assert.IsFalse(run.CanSwap(kristela, haiden), "not with an ally as badly hurt");
            haiden.Hp = haiden.MaxHp;
            kristela.Hp = kristela.MaxHp * 30 / 100;
            Assert.IsFalse(run.IsSaferSwap(kristela, haiden), "at 30% she isn't badly hurt");
            Assert.IsTrue(run.IsRotateSwap(kristela, haiden), "though holding a corridor, under half, she may give him the front (DoorTacticsTests)");
        }

        [Test]
        public void ASwapToSafetyNeedsTheAllyToStandFartherFromTheFoes()
        {
            var run = Run(new[] { ActorCatalog.Kristela, ActorCatalog.Haiden }, Room);
            var kristela = run.Hero;
            var haiden = run.Party[1];
            Place(kristela, 3, 3);
            Place(haiden, 3, 4); // Beside her, and also next to the spider.
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
            // In a room: in a corridor she would give Haiden the front and heal behind him (DoorTacticsTests).
            var run = Run(new[] { ActorCatalog.Kristela, ActorCatalog.Haiden }, Room);
            var kristela = run.Hero;
            var haiden = run.Party[1];
            Place(kristela, 3, 3);
            Place(haiden, 2, 3);
            Root(Dummy(run, 4, 3));
            kristela.Hp = kristela.MaxHp * 25 / 100;
            Assert.IsTrue(run.CanSwap(kristela, haiden), "she could run behind him");

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
            Assert.IsFalse(run.CanSwap(kristela, uzuki), "Uzuki's tile is farther from the spider");

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
            // A wall down the middle of the room hides the spider from him.
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
            var spider = Dummy(run, 6, 3);
            Root(spider);
            HoldAll(run);
            run.Wait(); // The fight starts.
            Assert.IsTrue(run.InCombat);
            run.SetTactic(uzuki, PartyTactic.Follow);
            Assert.IsFalse(run.AnyFoeInSight(uzuki.Pos, uzuki.Team, 5), "no shot from where he stands: the wall's corner is in the way");

            var command = PartnerBrain.Decide(run, uzuki);
            Assert.AreEqual(HeroCommandKind.Move, command.Kind);
            var next = uzuki.Pos + command.Direction.ToOffset();
            Assert.IsTrue(run.AnyFoeInSight(next, uzuki.Team, 5), "a shot from there");
            Assert.IsFalse(run.FoeAdjacent(next, uzuki.Team), "without walking into melee");
        }

        /// <summary>Haiden up the ring's right-hand corridor with a spider in his face, the fight on, everyone else still to place.</summary>
        static DungeonRun TankInTheRing(ActorDefinition[] members)
        {
            var run = Run(members, Ring);
            Place(run.Hero, 4, 2);
            for (int member = 1; member < run.Party.Count; member++) Place(run.Party[member], 5 - member, 1);
            Root(Dummy(run, 4, 3));
            HoldAll(run);
            run.Wait(); // The fight starts.
            Assert.IsTrue(run.InCombat);
            foreach (var member in run.Party) run.SetTactic(member, PartyTactic.Attack);
            return run;
        }

        [Test]
        public void AMeleePartnerWaitsBehindTheAllyThatHoldsTheCorridor()
        {
            var run = TankInTheRing(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela });
            var kristela = run.Party[1];
            Assert.AreEqual(new GridPos(4, 1), kristela.Pos, "right behind Haiden");

            // The spider's far side is 11 steps around the ring: she doesn't go looking for it.
            Assert.AreEqual(HeroCommand.Wait, PartnerBrain.Decide(run, kristela));
            for (int i = 0; i < 4; i++) run.Wait();
            Assert.AreEqual(new GridPos(4, 1), kristela.Pos, "still there, ready to take his place");
        }

        [Test]
        public void ARangedPartnerStaysBehindItsAlliesRatherThanWalkAroundTheFloor()
        {
            var run = TankInTheRing(ActorCatalog.StartingParty);
            var uzuki = run.Party[2];
            Assert.AreEqual(new GridPos(3, 1), uzuki.Pos, "around the corner, behind Kristela");
            Assert.IsFalse(run.AnyFoeInSight(uzuki.Pos, uzuki.Team, 5), "the corner hides the spider from him");
            Assert.IsTrue(run.AnyFoeInSight(new GridPos(4, 5), uzuki.Team, 5), "the top of the ring, 9 steps around, would give him a shot");

            Assert.AreEqual(HeroCommand.Wait, PartnerBrain.Decide(run, uzuki));
            for (int i = 0; i < 4; i++) run.Wait();
            Assert.AreEqual(new GridPos(3, 1), uzuki.Pos, "he stays with the party");
        }

        [Test]
        public void AMeleePartnerStillWalksAroundAnAllyWhenThatIsAShortWay()
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela }, Room);
            var kristela = run.Party[1];
            Place(run.Hero, 4, 3);
            Place(kristela, 3, 3);
            var spider = Dummy(run, 5, 3);
            Root(spider);
            HoldAll(run);
            run.Wait();
            Assert.IsTrue(run.InCombat);
            run.SetTactic(kristela, PartyTactic.Attack);

            run.Wait();
            run.Wait();
            Assert.AreEqual(1, GridPos.ChebyshevDistance(kristela.Pos, spider.Pos), "around Haiden, into the fight");
        }

        [Test]
        public void PartnersDontLeaveTheLeaderForAFoeThatIsALongWalkAway()
        {
            // Two corridors side by side that meet only at the far left: the spider is two tiles from Haiden through
            // the wall, and eleven steps' walk.
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela },
                "###########",
                "#.........#",
                "#.#########",
                "#@........#",
                "###########");
            var kristela = run.Party[1];
            Place(run.Hero, 6, 1);
            Place(kristela, 5, 1);
            Root(Dummy(run, 6, 3));
            HoldAll(run);
            run.Wait();
            Assert.IsTrue(run.InCombat, "it noticed them through the wall");
            run.SetTactic(kristela, PartyTactic.Attack);

            Assert.AreEqual(HeroCommand.Wait, PartnerBrain.Decide(run, kristela), "she stays with Haiden; it can come to them");
        }

        [Test]
        public void APartnerCutOffInACorridorGetsPastTheOneBehindItInLine()
        {
            var run = Run(ActorCatalog.StartingParty, Corridor); // Haiden leads, then Kristela, then Uzuki.
            var kristela = run.Party[1];
            var uzuki = run.Party[2];
            Place(run.Hero, 1, 1);
            Place(uzuki, 3, 1);
            Place(kristela, 4, 1);
            Assert.IsTrue(run.CanSwap(kristela, uzuki), "regroup: she comes before Uzuki in line");
            Assert.IsFalse(run.CanSwap(uzuki, kristela), "never the later one past the earlier");

            run.Wait();
            Assert.AreEqual(new GridPos(3, 1), kristela.Pos);
            Assert.AreEqual(new GridPos(4, 1), uzuki.Pos, "Uzuki let her by, rather than the two of them standing there for good");
            run.Wait();
            Assert.AreEqual(new GridPos(2, 1), kristela.Pos, "next to Haiden again");
            Assert.AreEqual(new GridPos(3, 1), uzuki.Pos, "with Uzuki behind her");
        }

        [Test]
        public void ARegroupSwapNeedsBothPartnersClearOfTheFoes()
        {
            var run = Run(ActorCatalog.StartingParty, Corridor);
            var kristela = run.Party[1];
            var uzuki = run.Party[2];
            Place(run.Hero, 1, 1);
            Place(uzuki, 3, 1);
            Place(kristela, 4, 1);
            Assert.IsTrue(run.IsRegroupSwap(kristela, uzuki));
            Assert.IsFalse(run.IsRegroupSwap(kristela, run.Hero), "the leader is never moved this way");

            Dummy(run, 5, 1);
            Assert.IsFalse(run.IsRegroupSwap(kristela, uzuki), "not with a spider next to her");
            Assert.IsFalse(run.CanSwap(kristela, uzuki), "and Uzuki's tile is no closer to it");
        }

        [Test]
        public void TheAutoPilotWaitsBehindAPartnerThatHoldsTheCorridorInsteadOfWalkingOff()
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela },
                "##########",
                "#>..@....#",
                "##########");
            var kristela = run.Party[1];
            Place(run.Hero, 4, 1);
            Place(kristela, 5, 1);
            Root(Dummy(run, 6, 1));
            run.Wait();
            Assert.IsTrue(run.InCombat);
            Assert.IsTrue(run.FoeAdjacent(kristela), "she holds the corridor");

            Assert.AreEqual(HeroCommand.Wait, AutoPilot.Decide(run), "not off to the stairs behind him while she fights");
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
            Assert.Less(ActorCatalog.Bat.MaxHp, ActorCatalog.Spider.MaxHp);
        }
    }
}

using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;
using static FiveKingdoms.Tests.PartyTests;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// PROGRESSION.md, "Heroes heal between fights": outside a fight the party's AI (the partners and the autopilot's
    /// leader) uses its heals to top the party up before moving on, a hurt hero that can't mend itself goes to the one
    /// that can heal it, and the autopilot's leader waits for all of that, but not for ever. Also the events that let
    /// the view say why a hero stands still or trades places ("A note when a hero waits").
    /// </summary>
    public class BetweenFightsTests
    {
        static readonly string[] Room =
        {
            "############",
            "#..........#",
            "#..........#",
            "#@........>#",
            "#..........#",
            "#..........#",
            "############",
        };

        static readonly string[] Corridor =
        {
            "############",
            "#@........>#",
            "############",
        };

        static Actor Member(DungeonRun run, ActorDefinition definition) => run.Party.First(member => member.Definition == definition);

        static int Percent(Actor hero) => hero.Hp * 100 / hero.MaxHp;

        static void Hurt(Actor hero, int percentLeft) => hero.Hp = hero.MaxHp * percentLeft / 100;

        /// <summary>Lets the autopilot play until it stops resting (or 40 actions have passed); returns how often it rested.</summary>
        static int RestUntilDone(DungeonRun run)
        {
            int rests = 0;
            for (int action = 0; action < 40; action++)
            {
                var command = AutoPilot.Decide(run);
                bool healing = command.Kind == HeroCommandKind.Skill && run.Hero.Skills[command.Slot].Effect == SkillEffect.Heal;
                if (!command.Resting && !healing) break;
                if (command.Resting) rests++;
                Assert.IsTrue(run.Execute(command));
            }
            return rests;
        }

        [Test]
        public void BetweenFightsAHeroHealsWhateverIsWorthAHeal()
        {
            var run = Run(new[] { ActorCatalog.Haiden, TestHeroes.Monk }, Room);
            var kristela = run.Party[1];
            Hurt(kristela, 80);
            NoHealing(run.Hero);

            var command = PartnerBrain.Decide(run, kristela);
            Assert.AreEqual(HeroCommandKind.Skill, command.Kind, "a quarter of her HP, and she is missing a fifth");
            Assert.AreSame(SkillCatalog.KiHeal, kristela.Skills[command.Slot]);

            Hurt(kristela, 90);
            Assert.AreEqual(HeroCommand.Wait, PartnerBrain.Decide(run, kristela), "more than half of it would be wasted");
        }

        [Test]
        public void InAFightHeroesStillSaveTheirHealsForWhenTheyAreNeeded()
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela }, Room);
            var kristela = run.Party[1];
            foreach (var member in run.Party) run.SetTactic(member, PartyTactic.Hold);
            Dummy(run, 6, 3);
            run.Wait();
            Assert.IsTrue(run.InCombat);

            Hurt(kristela, 80);
            Assert.AreNotEqual(HeroCommandKind.Skill, PartnerBrain.Decide(run, kristela).Kind);
            Hurt(run.Hero, 80);
            Assert.AreNotEqual(HeroCommandKind.Skill, AutoPilot.Decide(run).Kind);
        }

        [Test]
        public void HaidenTopsUpTheAllyNextToHim()
        {
            var run = Run(ActorCatalog.StartingParty, Room);
            var haiden = run.Hero;
            var uzuki = Member(run, ActorCatalog.Uzuki);
            Place(haiden, 3, 3);
            Place(run.Party[1], 2, 3);
            Place(uzuki, 3, 4);
            Hurt(uzuki, 70);

            var command = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommandKind.Skill, command.Kind);
            Assert.AreSame(SkillCatalog.PaladinHeal, haiden.Skills[command.Slot]);

            Assert.IsTrue(run.Execute(command));
            Assert.Greater(Percent(uzuki), 90);
        }

        [Test]
        public void TheAutoPilotsLeaderWaitsWhileAPartnerHealsUp()
        {
            var run = Run(new[] { ActorCatalog.Haiden, TestHeroes.Monk }, Room);
            var kristela = run.Party[1];
            Place(run.Hero, 3, 3);
            Place(kristela, 3, 1); // Not next to him: his own heal doesn't reach her.
            run.SetTactic(kristela, PartyTactic.Hold);
            Hurt(kristela, 30);

            Assert.AreEqual(HeroCommand.Rest, AutoPilot.Decide(run), "he doesn't walk off to the stairs while she mends herself");
            Assert.IsTrue(run.Execute(HeroCommand.Rest));
            var waited = run.Events.OfType<HeroWaitedEvent>().Single();
            Assert.AreEqual(run.Hero.Id, waited.ActorId);
            Assert.AreEqual(WaitReason.Rests, waited.Reason);
            Assert.AreEqual(1, waited.Turns);

            int rests = 1 + RestUntilDone(run);
            Assert.GreaterOrEqual(Percent(kristela), 100 - SkillCatalog.KiHeal.Power / 2 - 1, "topped up: another Ki Heal would be half wasted");
            Assert.LessOrEqual(rests, 6, "three heals, one every other turn of hers");
            Assert.AreEqual(HeroCommandKind.Move, AutoPilot.Decide(run).Kind, "and on they go");
            Assert.AreEqual(0, run.Hero.RestedTurns, "every heal that lands starts his patience over");
        }

        [Test]
        public void AHurtPartnerGoesToTheHealer()
        {
            var run = Run(ActorCatalog.StartingParty, Room);
            var haiden = run.Hero;
            var kristela = Member(run, ActorCatalog.Kristela);
            var uzuki = Member(run, ActorCatalog.Uzuki);
            Place(haiden, 2, 3);
            Place(kristela, 3, 3);
            Place(uzuki, 6, 3);
            Hurt(uzuki, 50);

            Assert.AreSame(haiden, HeroTactics.HealerFor(run, uzuki));
            Assert.AreEqual(HeroCommandKind.Move, PartnerBrain.Decide(run, uzuki).Kind);
            Assert.AreEqual(HeroCommand.Rest, AutoPilot.Decide(run), "Haiden waits for him");

            RestUntilDone(run);
            Assert.AreEqual(1, GridPos.ChebyshevDistance(uzuki.Pos, haiden.Pos), "he came over");
            Assert.Greater(Percent(uzuki), 85, "and Haiden healed him");
            Assert.IsNull(HeroTactics.HealerFor(run, uzuki));
        }

        [Test]
        public void AHeroWithAHealOfItsOwnDoesNotGoLookingForOne()
        {
            var run = Run(new[] { ActorCatalog.Haiden, TestHeroes.Monk, ActorCatalog.Uzuki }, Room);
            var kristela = Member(run, TestHeroes.Monk);
            Place(run.Hero, 2, 3);
            Place(kristela, 6, 3);
            Place(Member(run, ActorCatalog.Uzuki), 2, 4);
            Hurt(kristela, 50);

            Assert.IsNull(HeroTactics.HealerFor(run, kristela), "Ki Heal is hers");
            Assert.AreEqual(HeroCommandKind.Skill, PartnerBrain.Decide(run, kristela).Kind);
        }

        [Test]
        public void TheLeaderMovesOnWhenNoHealCanLand()
        {
            // Uzuki is hurt, Haiden could heal him, but Kristela stands between them in a corridor.
            var run = Run(ActorCatalog.StartingParty, Corridor);
            var haiden = run.Hero;
            var uzuki = Member(run, ActorCatalog.Uzuki);
            Place(haiden, 4, 1);
            Place(Member(run, ActorCatalog.Kristela), 3, 1);
            Place(uzuki, 2, 1);
            Hurt(uzuki, 50);

            for (int turn = 0; turn < HeroTactics.RestPatience; turn++)
            {
                Assert.AreEqual(HeroCommand.Rest, AutoPilot.Decide(run), $"turn {turn}");
                Assert.IsTrue(run.Execute(HeroCommand.Rest));
                Assert.AreEqual(turn + 1, run.Events.OfType<HeroWaitedEvent>().Single().Turns);
            }
            Assert.AreEqual(HeroTactics.RestPatience, haiden.RestedTurns);
            Assert.AreEqual(HeroCommand.Move(Direction8.E), AutoPilot.Decide(run), "nothing came of it: on to the stairs");
            Assert.AreEqual(uzuki.MaxHp * 50 / 100, uzuki.Hp);
        }

        [Test]
        public void APlayersLeaderIsNotMadeToWait()
        {
            var run = Run(new[] { ActorCatalog.Haiden, TestHeroes.Monk }, Room);
            var kristela = run.Party[1];
            Hurt(kristela, 30);
            Assert.IsTrue(HeroTactics.IsToppingUp(run));

            Assert.IsTrue(run.Move(Direction8.E), "by hand the player decides when to move on");
            Assert.IsFalse(run.Events.OfType<HeroWaitedEvent>().Any());
            Assert.Greater(Percent(kristela), 30, "and she heals on the way");
        }

        // ---- What the view is told ("A note when a hero waits") ----

        static readonly string[] Doorway =
        {
            "#############",
            "#####.......#",
            "#####.......#",
            "#@..........#",
            "#####.......#",
            "#####......>#",
            "#############",
        };

        [Test]
        public void HoldingADoorwayIsSaidEveryTurnItLasts()
        {
            var run = Run(ActorCatalog.StartingParty, Doorway);
            for (int i = 0; i < run.Party.Count; i++) Place(run.Party[i], 4 - i, 3);
            foreach (var spot in new[] { new GridPos(7, 3), new GridPos(7, 4), new GridPos(7, 2) })
                Dummy(run, spot.X, spot.Y).Statuses.Add(new StatusEffect(StatusKind.Rooted, 0, 0, 999, endsOnSourceTurn: false));

            for (int turn = 1; turn <= 2; turn++)
            {
                Assert.AreEqual(HeroCommand.HoldTheDoor, AutoPilot.Decide(run));
                Assert.IsTrue(run.Execute(HeroCommand.HoldTheDoor));
                var waited = run.Events.OfType<HeroWaitedEvent>().Single(e => e.ActorId == run.Hero.Id);
                Assert.AreEqual(WaitReason.HoldsTheDoor, waited.Reason);
                Assert.AreEqual(turn, waited.Turns);
            }
        }

        [Test]
        public void ARotationOfTheFrontSaysWhoStepsBack()
        {
            var run = Run(new[] { ActorCatalog.Uzuki, ActorCatalog.Haiden, ActorCatalog.Kristela }, Corridor);
            var haiden = Member(run, ActorCatalog.Haiden);
            var kristela = Member(run, ActorCatalog.Kristela);
            Place(haiden, 4, 1);
            Place(kristela, 3, 1);
            Place(run.Hero, 2, 1);
            Dummy(run, 5, 1).Statuses.Add(new StatusEffect(StatusKind.Rooted, 0, 0, 999, endsOnSourceTurn: false));
            foreach (var member in run.Party) run.SetTactic(member, PartyTactic.Hold);
            run.Wait();
            foreach (var member in run.Party) run.SetTactic(member, PartyTactic.Attack);
            Hurt(haiden, 40);

            run.Wait();
            var swap = run.Events.OfType<SwappedEvent>().Single();
            Assert.AreEqual(SwapReason.Rotate, swap.Reason);
            Assert.AreEqual(haiden.Id, swap.HurtId, "the hurt one steps back");
            Assert.AreEqual(kristela.Id, swap.ActorId, "the fresh one made the move");
        }

        [Test]
        public void APlainSwapIsJustPassing()
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela }, Corridor);
            Place(run.Hero, 3, 1);
            Place(run.Party[1], 4, 1);

            Assert.IsTrue(run.Move(Direction8.E));
            var swap = run.Events.OfType<SwappedEvent>().First();
            Assert.AreEqual(SwapReason.Passing, swap.Reason);
            Assert.AreEqual(-1, swap.HurtId);
        }
    }
}

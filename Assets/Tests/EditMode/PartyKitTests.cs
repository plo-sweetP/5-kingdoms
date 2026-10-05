using System.Collections.Generic;
using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;
using static FiveKingdoms.Tests.PartyTests;

namespace FiveKingdoms.Tests
{
    /// <summary>
    /// The starting kits approved in PROGRESSION.md: Uzuki the Archer (traps), Haiden the Paladin (tank first, some
    /// healing), Kristela the Monk (speed melee). Milestone 1f.
    /// </summary>
    public class PartyKitTests
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

        static ActorDefinition[] Only(ActorDefinition hero) => new[] { hero };

        static int Slot(Actor hero, SkillDefinition skill)
        {
            var skills = hero.Definition.Skills;
            for (int i = 0; i < skills.Count; i++)
                if (skills[i] == skill) return i;
            return -1;
        }

        static IEnumerable<AttackEvent> AttacksBy(DungeonRun run, Actor attacker) =>
            run.Events.OfType<AttackEvent>().Where(attack => attack.AttackerId == attacker.Id);

        /// <summary>Keeps a dummy where it stands (it can still bite what's next to it).</summary>
        static Actor Still(Actor actor)
        {
            actor.Statuses.Add(new StatusEffect(StatusKind.Rooted, actor.Id, 0, 999, endsOnSourceTurn: false));
            return actor;
        }

        [Test]
        public void TheKitsAreTheApprovedOnes()
        {
            string Ids(ActorDefinition hero) => string.Join(",", hero.Skills.Select(skill => skill.Id)) + " / " + hero.Ultimate.Id;
            Assert.AreEqual("hunters_mark,power_shot,rolling_shot / volley", Ids(ActorCatalog.Uzuki));
            Assert.AreEqual("paladin_heal,divine_strike,shoulder_bash / aura_of_protection", Ids(ActorCatalog.Haiden));
            Assert.AreEqual("piercing_punch,ki_heal,stun_strike / flurry_of_blows", Ids(ActorCatalog.Kristela));
        }

        [Test]
        public void EverySkillSitsOutOneTurnAndUltimatesNone()
        {
            foreach (var hero in ActorCatalog.StartingParty)
            {
                foreach (var skill in hero.Skills) Assert.AreEqual(1, skill.Cooldown, skill.Name);
                Assert.IsTrue(hero.Ultimate.IsUltimate, hero.Name);
                Assert.AreEqual(0, hero.Ultimate.Cooldown, hero.Name);
            }
        }

        [Test]
        public void EachHeroHasAtMostOneQuickSkill()
        {
            foreach (var hero in ActorCatalog.StartingParty)
                Assert.LessOrEqual(hero.Skills.Count(skill => skill.IsQuick), 1, hero.Name);
            Assert.IsTrue(SkillCatalog.HuntersMark.IsQuick);
            Assert.IsTrue(SkillCatalog.KiHeal.IsQuick);
        }

        [Test]
        public void TheSpeedBudgetHolds()
        {
            // GEAR.md: pre-gear speed stays within 85-100, and no weapon gives more than the Hunter Bow's +6.
            foreach (var hero in ActorCatalog.StartingParty)
                Assert.That(hero.Speed, Is.InRange(85, 100), hero.Name);
            foreach (var weapon in WeaponCatalog.All)
                Assert.LessOrEqual(weapon.Spd, WeaponDefinition.MaxSpeed, weapon.Name);
            Assert.AreEqual(WeaponDefinition.MaxSpeed, WeaponCatalog.HunterBow.Spd);
        }

        [Test]
        public void EachHeroCarriesTheirWeapon()
        {
            Assert.AreEqual(WeaponType.Bow, ActorCatalog.Uzuki.Weapon.Type);
            Assert.AreEqual(WeaponType.LongSword, ActorCatalog.Haiden.Weapon.Type);
            Assert.AreEqual(WeaponType.Gauntlets, ActorCatalog.Kristela.Weapon.Type);
        }

        // ---- Uzuki ----

        [Test]
        public void EachHeroHasAnAlwaysReadyWeaponAttack()
        {
            Assert.AreEqual("Quick Shot", ActorCatalog.Uzuki.AttackName);
            Assert.AreEqual("Sword Slash", ActorCatalog.Haiden.AttackName);
            Assert.AreEqual("Jab", ActorCatalog.Kristela.AttackName);
        }

        [Test]
        public void UzukisQuickShotReachesFiveTiles()
        {
            var run = Run(Only(ActorCatalog.Uzuki), Corridor);
            var near = Dummy(run, 6, 1);
            Assert.IsTrue(run.Attack(Direction8.E));
            var shot = AttacksBy(run, run.Hero).Single();
            Assert.IsTrue(shot.Ranged);
            Assert.AreEqual(near.Id, shot.TargetId);
            Assert.AreEqual(5, shot.Distance);
            Assert.AreEqual(new GridPos(6, 1), shot.To);
            Assert.Less(near.Hp, near.MaxHp);
        }

        [Test]
        public void AShotFallsShortOfAFoeOutOfReach()
        {
            var run = Run(Only(ActorCatalog.Uzuki), Corridor);
            var far = Dummy(run, 7, 1);
            Assert.IsFalse(run.AttackAt(far.Pos), "six tiles away: not a target");
            run.Attack(Direction8.E);
            var miss = AttacksBy(run, run.Hero).Single();
            Assert.AreEqual(-1, miss.TargetId);
            Assert.AreEqual(new GridPos(6, 1), miss.To, "the arrow flies its five tiles");
            Assert.AreEqual(far.MaxHp, far.Hp);
        }

        [Test]
        public void ShotsReachAnyFoeInSightNotOnlyAlongTheEightLines()
        {
            var run = Run(Only(ActorCatalog.Uzuki), Room); // Uzuki at (1, 3).
            var offLine = Still(Dummy(run, 4, 5)); // Three over, two up: on none of the 8 lines.
            var onLine = Still(Dummy(run, 1, 1));

            Assert.AreSame(offLine, run.AttackTargetAt(run.Hero, offLine.Pos));
            Assert.IsTrue(run.Execute(HeroCommand.AttackAt(offLine.Pos)));
            var shot = AttacksBy(run, run.Hero).Single();
            Assert.AreEqual(offLine.Id, shot.TargetId, "the tapped one, not the one on a line");
            Assert.AreEqual(offLine.Pos, shot.To, "the arrow flies straight at it, at any angle");
            Assert.AreEqual(3, shot.Distance);
            Assert.AreEqual(Direction8.NE, shot.Direction, "and he turns the nearest of the 8 ways");
            Assert.Less(offLine.Hp, offLine.MaxHp);
            Assert.AreEqual(onLine.MaxHp, onLine.Hp);
        }

        [Test]
        public void ADirectionAimedShotTakesTheNearestFoeThatWayElseTheNearestAnywhere()
        {
            var run = Run(Only(ActorCatalog.Uzuki), Room); // Uzuki at (1, 3).
            var east = Still(Dummy(run, 5, 4)); // Four over, one up: nearest to east.
            var north = Still(Dummy(run, 1, 5));
            run.Attack(Direction8.E);
            Assert.AreEqual(east.Id, AttacksBy(run, run.Hero).Single().TargetId);
            run.Attack(Direction8.SW); // Nobody that way: the nearest in sight.
            Assert.AreEqual(north.Id, AttacksBy(run, run.Hero).Single().TargetId);
        }

        [Test]
        public void ArrowsFlyPastAlliesButNotWallsOrCorners()
        {
            var run = Run(new[] { ActorCatalog.Uzuki, ActorCatalog.Haiden }, Corridor);
            Place(run.Hero, 1, 1);
            Place(run.Party[1], 2, 1);
            var spider = Dummy(run, 3, 1);
            run.Attack(Direction8.E);
            Assert.AreEqual(spider.Id, AttacksBy(run, run.Hero).Single().TargetId, "past Haiden");

            var walled = Run(Only(ActorCatalog.Uzuki), "#######", "#@.#..#", "#######");
            var behind = Dummy(walled, 4, 1);
            walled.Attack(Direction8.E);
            Assert.AreEqual(-1, AttacksBy(walled, walled.Hero).Single().TargetId, "the wall stops it");

            var corner = Run(Only(ActorCatalog.Uzuki), "#####", "##..#", "#@#.#", "#####");
            var diagonal = Dummy(corner, 2, 2);
            corner.Attack(Direction8.NE);
            Assert.AreEqual(-1, AttacksBy(corner, corner.Hero).Single().TargetId, "no shooting around a corner");
            Assert.AreEqual(diagonal.MaxHp, diagonal.Hp);
            Assert.AreEqual(behind.MaxHp, behind.Hp);
        }

        [Test]
        public void PowerShotFiresTwoArrowsAndKnocksTheTargetBackOnce()
        {
            var run = Run(Only(ActorCatalog.Uzuki), Corridor);
            var spider = Dummy(run, 4, 1);
            Assert.IsTrue(run.UseSkill(Slot(run.Hero, SkillCatalog.PowerShot), Direction8.E));
            var arrows = AttacksBy(run, run.Hero).ToList();
            Assert.AreEqual(2, arrows.Count, "the Hunter Bow's Multishot");
            Assert.IsTrue(arrows.All(arrow => arrow.TargetId == spider.Id && arrow.Ranged));
            Assert.AreEqual(4, arrows[1].Distance, "the second arrow flies to where the first knocked it");
            Assert.AreEqual(1, run.Events.OfType<PushedEvent>().Count(e => e.ActorId == spider.Id && !e.Blocked));
        }

        [Test]
        public void TheSecondArrowFindsAnotherTarget()
        {
            var run = Run(Only(ActorCatalog.Uzuki), Room);
            var east = Dummy(run, 4, 3);
            var north = Dummy(run, 1, 5);
            run.UseSkill(Slot(run.Hero, SkillCatalog.PowerShot), Direction8.E);
            CollectionAssert.AreEqual(new[] { east.Id, north.Id }, AttacksBy(run, run.Hero).Select(arrow => arrow.TargetId).ToArray());
        }

        [Test]
        public void HuntersMarkIsAQuickMarkThatOnlyUzukisHitsExploit()
        {
            var run = Run(Only(ActorCatalog.Uzuki), Corridor);
            var spider = Dummy(run, 4, 1);
            run.Wait(); // It comes closer; the fight starts. Uzuki (101) is up first.
            Assert.IsTrue(run.InCombat);

            Assert.IsTrue(run.UseSkill(Slot(run.Hero, SkillCatalog.HuntersMark), Direction8.E));
            Assert.AreEqual(125, run.DamageTakenPercent(spider, run.Hero), "+25% from Uzuki");
            Assert.AreEqual(100, run.DamageTakenPercent(spider, spider), "nobody else");
            Assert.AreEqual(Timeline.TurnLength(101) + Timeline.TurnLength(101, 50), run.Forecast(1)[0].Time, "Quick: half a turn");
        }

        [Test]
        public void TheMarkJumpsWhenItsTargetFalls()
        {
            var run = Run(Only(ActorCatalog.Uzuki), Corridor);
            var first = Dummy(run, 3, 1, hp: 1);
            var second = Dummy(run, 6, 1);
            Assert.IsTrue(run.UseSkill(Slot(run.Hero, SkillCatalog.HuntersMark), Direction8.E));
            Assert.IsNotNull(first.FindStatus(StatusKind.Mark));

            run.Attack(Direction8.E);
            Assert.IsFalse(first.IsAlive);
            var mark = second.FindStatus(StatusKind.Mark);
            Assert.IsNotNull(mark, "the mark jumped");
            Assert.AreEqual(run.Hero.Id, mark.SourceId);
        }

        [Test]
        public void RollingShotRollsAwayShootsAndLeavesASnare()
        {
            var run = Run(Only(ActorCatalog.Uzuki), Corridor);
            var uzuki = run.Hero;
            Place(uzuki, 5, 1);
            var spider = Dummy(run, 6, 1);

            Assert.IsTrue(run.UseSkill(Slot(uzuki, SkillCatalog.RollingShot), Direction8.W));
            Assert.AreEqual(new GridPos(3, 1), uzuki.Pos, "rolled 2 tiles");
            Assert.IsTrue(AttacksBy(run, uzuki).Any(arrow => arrow.TargetId == spider.Id), "then shot back");
            Assert.AreEqual(1, run.Events.OfType<TrapPlacedEvent>().Count(trap => trap.Pos == new GridPos(5, 1)));

            // The spider came after him and stepped on the snare where he stood.
            Assert.AreEqual(new GridPos(5, 1), spider.Pos);
            Assert.IsNotNull(spider.FindStatus(StatusKind.Rooted));
            Assert.AreEqual(0, run.Traps.Count, "a trap goes off once");
            run.Wait();
            Assert.AreEqual(new GridPos(5, 1), spider.Pos, "rooted: it can't follow");
        }

        [Test]
        public void HeroesWalkOverTheirOwnTraps()
        {
            var run = Run(Only(ActorCatalog.Uzuki), Corridor);
            Place(run.Hero, 5, 1);
            Dummy(run, 10, 1);
            run.UseSkill(Slot(run.Hero, SkillCatalog.RollingShot), Direction8.W);
            Assert.AreEqual(1, run.Traps.Count);
            run.Hero.Pos = new GridPos(4, 1);
            run.Move(Direction8.E);
            Assert.AreEqual(1, run.Traps.Count, "still there after Uzuki walked over it");
        }

        // ---- Haiden ----

        [Test]
        public void HaidensHealMendsTheMostHurtNeighborFromHisOwnMaxHp()
        {
            var run = Run(new[] { ActorCatalog.Haiden, ActorCatalog.Kristela, ActorCatalog.Uzuki }, Room);
            foreach (var member in run.Party) run.SetTactic(member, PartyTactic.Hold);
            var haiden = run.Hero;
            var kristela = run.Party[1];
            var uzuki = run.Party[2];
            Place(haiden, 1, 3);
            Place(kristela, 2, 3);
            Place(uzuki, 1, 4);
            kristela.Hp = 100;
            uzuki.Hp = 300;
            NoHealing(kristela); // Her own Ki Heal would follow his.

            Assert.IsTrue(run.UseSkill(Slot(haiden, SkillCatalog.PaladinHeal)));
            Assert.AreEqual(100 + haiden.MaxHp * 20 / 100, kristela.Hp, "the most hurt, by share of max HP");
            Assert.AreEqual(300, uzuki.Hp);
        }

        [Test]
        public void ShoulderBashShovesAndTaunts()
        {
            var run = Run(Only(ActorCatalog.Haiden), Corridor);
            var spider = Dummy(run, 2, 1);
            Assert.IsTrue(run.UseSkill(Slot(run.Hero, SkillCatalog.ShoulderBash), Direction8.E));
            Assert.IsTrue(run.Events.OfType<PushedEvent>().Any(e => e.ActorId == spider.Id && !e.Blocked));
            Assert.IsTrue(run.Events.OfType<StatusAppliedEvent>().Any(e => e.ActorId == spider.Id && e.Kind == StatusKind.Taunt));
        }

        [Test]
        public void ShoulderBashHitsHarderAgainstAWall()
        {
            int Bash(params string[] rows)
            {
                var run = Run(Only(ActorCatalog.Haiden), rows);
                var spider = Dummy(run, 2, 1);
                run.Hero.CritRate = 0;
                run.UseSkill(Slot(run.Hero, SkillCatalog.ShoulderBash), Direction8.E);
                return run.Events.OfType<DamageEvent>().First(hit => hit.TargetId == spider.Id).Amount;
            }
            int open = Bash("#####", "#@..#", "#####");
            int walled = Bash("####", "#@.#", "####");
            Assert.AreEqual(open * 3 / 2, walled, 2, "+50% when it can't be shoved");
        }

        [Test]
        public void DivineStrikeIsAFireSmite()
        {
            var run = Run(Only(ActorCatalog.Haiden), Corridor);
            var spider = Dummy(run, 2, 1);
            Assert.AreEqual(Element.Fire, SkillCatalog.DivineStrike.Element);
            Assert.IsTrue(run.UseSkill(Slot(run.Hero, SkillCatalog.DivineStrike), Direction8.E));
            Assert.Less(spider.Hp, spider.MaxHp);
        }

        // ---- Kristela ----

        [Test]
        public void PiercingPunchAlsoHitsTheEnemyBehind()
        {
            var run = Run(Only(ActorCatalog.Kristela), Corridor);
            var front = Dummy(run, 2, 1);
            var back = Dummy(run, 3, 1);
            Assert.IsTrue(run.UseSkill(Slot(run.Hero, SkillCatalog.PiercingPunch), Direction8.E));
            Assert.Less(front.Hp, front.MaxHp);
            Assert.Less(back.Hp, back.MaxHp);
        }

        [Test]
        public void KiHealIsAQuickSelfHeal()
        {
            var run = Run(Only(ActorCatalog.Kristela), Corridor);
            Dummy(run, 3, 1);
            run.Wait(); // The fight starts; Kristela (100) is up at 100 AV.
            var kristela = run.Hero;
            kristela.Hp = 100;
            Assert.IsTrue(run.UseSkill(Slot(kristela, SkillCatalog.KiHeal)));
            Assert.AreEqual(kristela.MaxHp * 25 / 100, run.Events.OfType<HealedEvent>().Single().Amount);
            Assert.AreEqual(AvTime.FromWhole(150), run.Forecast(1)[0].Time, "Quick: half a turn");
        }

        /// <summary>Kristela alone next to a foe, the fight already on and her stun a sure thing (her 60% isn't under test).</summary>
        static DungeonRun StunDuel(out Actor foe, ActorDefinition foeDefinition = null, int foeSpeed = 0)
        {
            var run = Run(Only(ActorCatalog.Kristela), Corridor);
            run.Hero.MaxHp = run.Hero.Hp = 100000;
            foe = run.SpawnEnemy(new GridPos(2, 1), foeDefinition);
            foe.MaxHp = foe.Hp = 100000;
            if (foeSpeed > 0) run.SetSpeed(foe, foeSpeed);
            run.Wait(); // It bites: the fight starts, and Kristela (100) is up at 100 AV.
            Assert.IsTrue(run.InCombat);
            run.Hero.Affinity = 100000;
            return run;
        }

        [Test]
        public void StunStrikePushesTheFoesNextTurnBackInsteadOfSkippingIt()
        {
            var run = StunDuel(out var spider); // The spider (100) was due at 100 AV too, right after her.
            Assert.IsTrue(run.Execute(HeroCommand.SkillAt(Slot(run.Hero, SkillCatalog.StunStrike), spider.Pos)));

            var delay = run.Events.OfType<TurnDelayedEvent>().Single();
            Assert.AreEqual(spider.Id, delay.ActorId);
            Assert.AreEqual(50, delay.Percent, "half a turn");
            Assert.IsTrue(delay.Stun);
            Assert.IsTrue(AttacksBy(run, spider).Any(), "delayed to 150 AV, not skipped: it still bit her before her next turn at 200");
            Assert.AreEqual(AvTime.FromWhole(250), run.Forecast(5).First(turn => turn.Actor == spider).Time, "and stays half a turn late");
        }

        [Test]
        public void AStunOutsideAFightDoesNothing()
        {
            var run = Run(Only(ActorCatalog.Kristela), Corridor);
            var spider = Dummy(run, 2, 1);
            run.Hero.Affinity = 100000;
            Assert.IsTrue(run.UseSkillAt(Slot(run.Hero, SkillCatalog.StunStrike), spider.Pos)); // Exploring: everyone acts once anyway.
            Assert.IsFalse(run.Events.OfType<TurnDelayedEvent>().Any());
            Assert.IsTrue(AttacksBy(run, spider).Any());
        }

        [Test]
        public void AFoeIsDelayedAtMostOncePerItsOwnTurn()
        {
            // A slow spider (50): due at 200 AV, while Kristela acts at 100, 200, 300.
            var run = StunDuel(out var spider, foeSpeed: 50);
            int stun = Slot(run.Hero, SkillCatalog.StunStrike);

            Assert.IsTrue(run.UseSkillAt(stun, spider.Pos)); // 100 AV: its turn moves from 200 to 300.
            var first = run.Events.OfType<TurnDelayedEvent>().Single();
            Assert.IsTrue(spider.IsDelayed);
            Assert.IsFalse(run.CanDelay(spider));
            Assert.AreEqual(AvTime.FromWhole(300), run.Forecast(5).First(turn => turn.Actor == spider).Time);

            Assert.IsTrue(run.AttackAt(spider.Pos)); // 200 AV: Stun Strike sits out a turn.
            Assert.IsTrue(spider.IsDelayed, "it still hasn't acted");

            Assert.IsTrue(run.UseSkillAt(stun, spider.Pos)); // 300 AV, just before the spider.
            Assert.IsFalse(run.Events.OfType<TurnDelayedEvent>().Any(), "a second stun before it acts does nothing: no stun-lock");
            Assert.IsTrue(AttacksBy(run, spider).Any(), "so it takes its turn at 300 as planned");
            Assert.IsFalse(spider.IsDelayed);

            Assert.IsTrue(run.AttackAt(spider.Pos)); // 400 AV.
            Assert.IsTrue(run.UseSkillAt(stun, spider.Pos)); // 500 AV: its next turn (500) can be pushed back again.
            var second = run.Events.OfType<TurnDelayedEvent>().Single();
            Assert.AreEqual(first.TurnsTaken + 1, second.TurnsTaken, "one delay for each of its turns");
        }

        [Test]
        public void AStunPushesABossBackHalfAsFar()
        {
            var run = StunDuel(out var boss, ActorCatalog.Troll);
            Assert.IsTrue(run.UseSkill(Slot(run.Hero, SkillCatalog.StunStrike), Direction8.E));
            var delay = run.Events.OfType<TurnDelayedEvent>().Single(e => e.ActorId == boss.Id);
            Assert.AreEqual(25, delay.Percent);
            Assert.AreEqual(25, CombatRules.DelayCap(boss));
            Assert.AreEqual(50, CombatRules.DelayCap(run.Hero));
        }

        [Test]
        public void ASnareCostsABossTimeInsteadOfRootingIt()
        {
            var run = Run(Only(ActorCatalog.Uzuki), Corridor);
            var uzuki = run.Hero;
            uzuki.MaxHp = uzuki.Hp = 100000;
            Place(uzuki, 5, 1);
            var boss = run.SpawnEnemy(new GridPos(6, 1), ActorCatalog.Troll);
            boss.MaxHp = boss.Hp = 100000;
            run.Wait(); // The fight starts.
            Assert.IsTrue(run.InCombat);

            // He rolls away, leaving a snare; the boss (85) follows onto it on its own turn at 10000 / 85 AV.
            Assert.IsTrue(run.UseSkill(Slot(uzuki, SkillCatalog.RollingShot), Direction8.W));
            Assert.AreEqual(new GridPos(5, 1), boss.Pos);
            Assert.IsNull(boss.FindStatus(StatusKind.Rooted), "bosses can't be rooted");
            var delay = run.Events.OfType<TurnDelayedEvent>().Single(e => e.ActorId == boss.Id);
            Assert.AreEqual(25, delay.Percent);
            Assert.IsFalse(delay.Stun);
            Assert.AreEqual(Timeline.TurnLength(85) + Timeline.TurnLength(85, 125), run.Forecast(5).First(turn => turn.Actor == boss).Time,
                "delayed during its own turn: the turn after it comes a quarter later");
            Assert.IsTrue(boss.IsDelayed, "and nothing else can push that turn back");
        }

        // ---- The AI uses the kits ----

        [Test]
        public void TheAutoPilotMarksABigFoeFirst()
        {
            var run = Run(Only(ActorCatalog.Uzuki), Corridor);
            Dummy(run, 4, 1);
            var command = AutoPilot.Decide(run);
            Assert.AreEqual(HeroCommandKind.Skill, command.Kind);
            Assert.AreSame(SkillCatalog.HuntersMark, run.Hero.Definition.Skills[command.Slot]);
        }

        [Test]
        public void TheArcherRollsAwayFromAFoeNextToHer()
        {
            var run = Run(Only(ActorCatalog.Uzuki), Corridor);
            Place(run.Hero, 5, 1);
            Dummy(run, 6, 1);
            var command = AutoPilot.Decide(run);
            Assert.AreSame(SkillCatalog.RollingShot, run.Hero.Definition.Skills[command.Slot]);
            Assert.AreEqual(Direction8.W, command.Direction);
        }

        [Test]
        public void APartnerHealsTheMostHurtNeighbor()
        {
            var run = Run(new[] { ActorCatalog.Kristela, ActorCatalog.Haiden }, Room);
            Place(run.Hero, 1, 3);
            Place(run.Party[1], 2, 3);
            run.Hero.Hp = 50;
            var command = PartnerBrain.Decide(run, run.Party[1]);
            Assert.AreEqual(HeroCommandKind.Skill, command.Kind);
            Assert.AreSame(SkillCatalog.PaladinHeal, run.Party[1].Definition.Skills[command.Slot]);
        }
    }
}

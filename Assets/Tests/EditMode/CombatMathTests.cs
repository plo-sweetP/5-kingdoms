using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    /// <summary>GEAR.md's damage formula, the stat sheet, crit stats and the 10x rescale (milestone 1e).</summary>
    public class CombatMathTests
    {
        static Actor Make(ActorDefinition definition, Team team = Team.Hero, int level = 1) =>
            new Actor(1, definition, team, default, level);

        static ActorDefinition Dummy(int attack = 60, int defense = 10, WeaponDefinition weapon = null) =>
            new ActorDefinition("dummy", "Dummy", maxHp: 1000, attack: attack, defense: defense, expReward: 0, weapon: weapon);

        [Test]
        public void UzukiStartsOnTheTenTimesScaleWithBaseCrit()
        {
            var definition = ActorCatalog.Uzuki;
            Assert.AreEqual(400, definition.MaxHp);
            Assert.AreEqual(60, definition.Attack);
            Assert.AreEqual(30, definition.Defense);

            var uzuki = Make(definition);
            var bow = WeaponCatalog.HunterBow;
            Assert.AreEqual(400 + bow.Hp, uzuki.MaxHp, "the bow's stats count as base");
            Assert.AreEqual(60 + bow.Atk, uzuki.Attack);
            Assert.AreEqual(30 + bow.Def, uzuki.Defense);
            Assert.AreEqual(50 + 2, uzuki.CritRate, "5% Crit Rate, in tenths of a percent, and the Archer's 0.2% at tier 1");
            Assert.AreEqual(500, uzuki.CritDmg, "+50% Crit DMG");
        }

        [Test]
        public void ABasicHitMatchesTheSpecAtLevelOne()
        {
            // GEAR.md: 200% of 60 ATK against a spider's 10 DEF is about 115 (K = 210, so x 210/220), before the spread.
            var attacker = Make(Dummy(attack: 60));
            attacker.CritRate = 0;
            var target = Make(Dummy(defense: 10), Team.Enemy);
            var rng = new Rng(11);
            int low = int.MaxValue, high = 0;
            for (int i = 0; i < 500; i++)
            {
                int amount = CombatRules.RollBasicAttack(attacker, target, rng).Amount;
                low = System.Math.Min(low, amount);
                high = System.Math.Max(high, amount);
            }
            Assert.AreEqual(114, high, "120 x 210 / 220, rounded down");
            Assert.AreEqual(97, low, "the 85% end of the spread");
        }

        [Test]
        public void DefenseCutsAShareOfTheHitThatShrinksWithTheAttackersLevel()
        {
            Assert.AreEqual(210, CombatRules.DefenseConstant(1));
            Assert.AreEqual(300, CombatRules.DefenseConstant(10));

            var target = Make(Dummy(defense: 300), Team.Enemy);
            var low = Make(Dummy(attack: 100), level: 1);
            var high = Make(Dummy(attack: 100), level: 10);
            low.CritRate = high.CritRate = 0;
            int lowHit = CombatRules.RollBasicAttack(low, target, new Rng(5)).Amount;
            int highHit = CombatRules.RollBasicAttack(high, target, new Rng(5)).Amount;
            Assert.Greater(highHit, lowHit, "same ATK and roll, but the higher level cuts through more DEF");
            Assert.Greater(lowHit, 1, "DEF never makes a hit worthless");
        }

        [Test]
        public void ACritMultipliesByOnePlusCritDmgOnTopOfEverything()
        {
            var target = Make(Dummy(defense: 0), Team.Enemy);
            var normal = Make(Dummy(attack: 1000));
            var critter = Make(Dummy(attack: 1000));
            normal.CritRate = 0;
            critter.CritRate = 1000;
            critter.CritDmg = 500;
            for (int seed = 1; seed <= 20; seed++)
            {
                var plain = CombatRules.RollDamage(normal, target, new Rng(seed), skillPercent: 200, damageBonus: 200);
                var crit = CombatRules.RollDamage(critter, target, new Rng(seed), skillPercent: 200, damageBonus: 200);
                Assert.IsFalse(plain.Critical);
                Assert.IsTrue(crit.Critical);
                Assert.AreEqual(plain.Amount * 3 / 2, crit.Amount, 1, "x1.5 on the same roll");
            }
        }

        [Test]
        public void SkillPercentExtraDamageAndDamageBonusAllScaleTheHit()
        {
            var target = Make(Dummy(defense: 0), Team.Enemy);
            var attacker = Make(Dummy(attack: 1000));
            attacker.CritRate = 0;
            int Hit(int percent, int extra, int bonus) =>
                CombatRules.RollDamage(attacker, target, new Rng(9), percent, extra, bonus).Amount;

            int basic = Hit(200, 0, 0);
            Assert.AreEqual(basic * 2, Hit(400, 0, 0), 1);
            Assert.AreEqual(basic * 3 / 2, Hit(200, 1000, 0), 1, "+1000 extra on a 2000 hit");
            Assert.AreEqual(basic * 6 / 5, Hit(200, 0, 200), 1, "+20% DMG bonus");
        }

        [Test]
        public void TheStatSheetScalesBaseAndWeaponButNotFlatBonuses()
        {
            var sword = new WeaponDefinition("test_sword", "Test Sword", WeaponType.LongSword, "Slash", hp: 100, atk: 20, def: 5, spd: 4);
            var actor = Make(Dummy(attack: 60, defense: 10, weapon: sword));
            Assert.AreEqual(80, actor.Attack, "60 base + 20 weapon");
            Assert.AreEqual(1100, actor.MaxHp);
            Assert.AreEqual(104, actor.Speed, "weapon SPD adds to the base");

            actor.Stats.Percent[StatKind.Atk] = 100; // +10%
            actor.Stats.Flat[StatKind.Atk] = 7;
            actor.RecalculateStats();
            Assert.AreEqual(95, actor.Attack, "(60 + 20) x 1.1 + 7");
        }

        [Test]
        public void WeaponStatsGrowWithItemLevelButSpeedDoesNot()
        {
            var bow = WeaponCatalog.HunterBow;
            Assert.AreEqual(bow.Atk, bow.AtkAt(1));
            Assert.AreEqual(bow.Atk + 9 * bow.AtkPerLevel, bow.AtkAt(10));
            Assert.AreEqual(bow.Hp + 9 * bow.HpPerLevel, bow.HpAt(10));

            var archer = Make(new ActorDefinition("archer", "Archer", maxHp: 400, attack: 60, defense: 30, expReward: 0, weapon: bow));
            int attackAtOne = archer.Attack;
            archer.WeaponLevel = 10;
            archer.RecalculateStats();
            Assert.AreEqual(attackAtOne + 9 * bow.AtkPerLevel, archer.Attack);
            Assert.AreEqual(100 + bow.Spd, archer.Speed, "SPD is flat, whatever the item level");
        }

        [Test]
        public void CritRateCapsAtOneHundredPercent()
        {
            var actor = Make(Dummy());
            actor.Stats.Flat[StatKind.CritRate] = 2000;
            actor.RecalculateStats();
            Assert.AreEqual(1000, actor.CritRate);
        }

        [Test]
        public void ALevelUpAddsTheDefinitionsGrowthAndHealsByIt()
        {
            var hero = Make(TestHeroes.Classic);
            hero.Hp = 100;
            CombatRules.ApplyLevelUp(hero);
            Assert.AreEqual(2, hero.Level);
            Assert.AreEqual(400 + TestHeroes.Classic.HpGrowth, hero.MaxHp);
            Assert.AreEqual(100 + TestHeroes.Classic.HpGrowth, hero.Hp);
            Assert.AreEqual(60 + TestHeroes.Classic.AtkGrowth, hero.Attack);
            Assert.AreEqual(TestHeroes.Classic.Speed, hero.Speed, "levels never raise speed");
        }

        [Test]
        public void MonstersSpawnAtTheFloorsLevel()
        {
            var first = TestRuns.OnMap("#####", "#@..#", "#####").SpawnEnemy(new GridPos(2, 1));
            Assert.AreEqual(1, first.Level);
            Assert.AreEqual(ActorCatalog.Spider.MaxHp, first.MaxHp);

            var deeper = new DungeonRun(4, new DungeonRunConfig { Populate = false, Boss = null });
            while (deeper.Floor < 3)
            {
                deeper.Hero.Pos = deeper.Map.Stairs;
                deeper.Descend();
            }
            var spider = deeper.SpawnEnemy(deeper.Hero.Pos + new GridPos(1, 0));
            Assert.AreEqual(3, spider.Level);
            Assert.AreEqual(ActorCatalog.Spider.MaxHp + 2 * ActorCatalog.Spider.HpGrowth, spider.MaxHp);
            Assert.AreEqual(ActorCatalog.Spider.Attack + 2 * ActorCatalog.Spider.AtkGrowth, spider.Attack);
            Assert.AreEqual(ActorCatalog.Spider.ExpReward + 2 * ActorCatalog.Spider.ExpGrowth, spider.ExpReward);
        }

        [Test]
        public void SkillsCarryTheirTags()
        {
            var strike = SkillCatalog.SpiritStrike;
            Assert.AreEqual(DamageKind.Physical, strike.Kind);
            Assert.AreEqual(AttackReach.Melee, strike.Reach);
            Assert.AreEqual(Element.None, strike.Element);
            Assert.AreEqual(240, strike.Power, "120% of a 200% basic attack, in percent of ATK");
        }
    }
}

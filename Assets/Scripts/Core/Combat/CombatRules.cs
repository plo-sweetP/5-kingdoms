using System;

namespace FiveKingdoms.Core
{
    public readonly struct DamageRoll
    {
        public readonly int Amount;
        public readonly bool Critical;

        public DamageRoll(int amount, bool critical)
        {
            Amount = amount;
            Critical = critical;
        }
    }

    /// <summary>Damage and leveling formulas, kept in one place so balance changes don't hunt through the code.</summary>
    public static class CombatRules
    {
        public const int CritChancePercent = 8;
        public const int LevelUpHp = 5;
        public const int LevelUpAttack = 1;
        public const int LevelUpDefense = 1;
        public const int LevelUpMp = 2;

        /// <summary>Mana a hero's basic attack restores when it connects.</summary>
        public const int BasicAttackManaGain = 2;

        /// <summary>
        /// Basic attack: 2 x ATK - DEF, times a random 85-100% spread (Pokemon-style), 1.5x on a critical hit.
        /// Always at least 1.
        /// </summary>
        public static DamageRoll RollBasicAttack(Actor attacker, Actor defender, Rng rng)
        {
            int raw = Math.Max(1, attacker.Attack * 2 - defender.Defense);
            int amount = raw * rng.Range(85, 101) / 100;
            bool critical = rng.Chance(CritChancePercent);
            if (critical) amount = amount * 3 / 2;
            return new DamageRoll(Math.Max(1, amount), critical);
        }

        /// <summary>A basic attack scaled up, for bosses' special moves.</summary>
        public static DamageRoll RollHeavyAttack(Actor attacker, Actor defender, Rng rng, int percent)
        {
            var roll = RollBasicAttack(attacker, defender, rng);
            return new DamageRoll(Math.Max(1, roll.Amount * percent / 100), roll.Critical);
        }

        public static int ExpToNextLevel(int level) => 8 + level * 6;

        public static void ApplyLevelUp(Actor actor)
        {
            actor.Level++;
            actor.MaxHp += LevelUpHp;
            actor.Hp += LevelUpHp;
            actor.Attack += LevelUpAttack;
            actor.Defense += LevelUpDefense;
            if (actor.Definition.MaxMp > 0)
            {
                actor.MaxMp += LevelUpMp;
                actor.Mp += LevelUpMp;
            }
        }
    }
}

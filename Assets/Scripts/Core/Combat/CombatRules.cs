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

    /// <summary>Damage and leveling formulas (GEAR.md), kept in one place so balance changes don't hunt through the code.</summary>
    public static class CombatRules
    {
        /// <summary>A basic attack hits for 200% of ATK; skills state their own percent.</summary>
        public const int BasicAttackPercent = 200;

        /// <summary>
        /// DEF mult = K / (K + target DEF), with K = KPerLevel x (attacker level + KLevelOffset). A higher-level attacker
        /// cuts through more DEF; DEF never makes a hit worthless.
        /// </summary>
        public const int KPerLevel = 10;
        public const int KLevelOffset = 20;

        /// <summary>Every hit varies a little, Mystery Dungeon style: 85-100% of the formula. Set both to 100 to turn it off.</summary>
        public const int SpreadMinPercent = 85;
        public const int SpreadMaxPercent = 100;

        /// <summary>
        /// PROGRESSION.md, "Ranged vs melee": ranged hits (weapon attacks, shots, area skills) deal 90% of an equivalent
        /// melee hit, and 30% less again with a foe next to the shooter (the point-blank rule).
        /// </summary>
        public const int RangedDamagePercent = 90;
        public const int PointBlankPercent = 70;

        /// <summary>
        /// The turn-manipulation budget for delays (PROGRESSION.md, "Delays / stuns"): one effect pushes a turn back by at
        /// most 50% of a turn, 25% on a boss, and an actor can be delayed at most once per its own turn.
        /// </summary>
        public const int MaxDelayPercent = 50;
        public const int MaxBossDelayPercent = 25;

        /// <summary>The most one delay may push <paramref name="target"/>'s turn back, in percent of one of its turns.</summary>
        public static int DelayCap(Actor target) => target.Definition.IsBoss ? MaxBossDelayPercent : MaxDelayPercent;

        /// <summary>
        /// The ultimate's charge meter (PROGRESSION.md, "Skill resources"): full at 100. It fills as a hero acts (each
        /// attack or skill), deals damage (each hit landed) and takes damage (each hit taken); an ultimate's own hits don't
        /// refill it. Tuned with -balance for about two ultimates per hero in the boss fight (counting the charge carried
        /// in) and one every two or three normal fights (approved by Peter, 2026-10-03): those are short (a hero acts
        /// about twice), so one per normal fight would put an ultimate in every other action. Each of the three gives 10.
        /// (They were 20 / 10 / 20 in 1f part 1; an archer who shoots every turn, and a tank whose aura covers himself and
        /// so gets used as soon as it's ready, both charged too fast at those rates.)
        /// </summary>
        public const int MaxCharge = 100;
        public const int ChargePerAction = 10;
        public const int ChargePerHitDealt = 10;
        public const int ChargePerHitTaken = 10;

        /// <summary>
        /// GEAR.md's formula: (skill% x ATK + extra damage) x (1 + DMG bonus) x crit x DEF mult x RES mult, times the
        /// random spread. Crit multiplies by 1 + Crit DMG. Everything multiplies, so crit always lands on top of every
        /// bonus. Integer math throughout, so a seeded run replays the same on every platform. Always at least 1.
        /// </summary>
        /// <param name="skillPercent">Damage in percent of ATK (a basic attack is <see cref="BasicAttackPercent"/>).</param>
        /// <param name="extraDamage">Flat damage added before the multipliers (set bonuses, weapon passives).</param>
        /// <param name="damageBonus">DMG bonus in tenths of a percent.</param>
        /// <param name="reachPercent">The ranged and point-blank cuts (<see cref="ReachPercent"/>); 100 for melee.</param>
        /// <param name="critRateBonus">Crit Rate this hit has on top of the attacker's, in tenths of a percent (a skill's own).</param>
        public static DamageRoll RollDamage(Actor attacker, Actor defender, Rng rng, int skillPercent, int extraDamage = 0,
            int damageBonus = 0, Element element = Element.None, int reachPercent = 100, int critRateBonus = 0)
        {
            long damage = (long)attacker.Attack * skillPercent / 100 + extraDamage;
            damage = damage * reachPercent / 100;
            damage = damage * (1000 + damageBonus) / 1000;
            damage = damage * rng.Range(SpreadMinPercent, SpreadMaxPercent + 1) / 100;
            bool critical = rng.Range(0, 1000) < attacker.CritRate + critRateBonus;
            if (critical) damage = damage * (1000 + attacker.CritDmg) / 1000;
            long k = DefenseConstant(attacker.Level);
            damage = damage * k / (k + Math.Max(0, defender.Defense));
            damage = damage * (1000 - ResistanceTo(defender, element)) / 1000;
            return new DamageRoll((int)Math.Max(1, Math.Min(int.MaxValue, damage)), critical);
        }

        /// <summary>
        /// The most a hit of <paramref name="skillPercent"/> does without a critical hit: the formula at the top of
        /// its spread. For the AI to weigh a blow that is coming (a boss's slam); never for dealing damage.
        /// </summary>
        public static int MaxDamage(Actor attacker, Actor defender, int skillPercent)
        {
            long damage = (long)attacker.Attack * skillPercent / 100;
            damage = damage * SpreadMaxPercent / 100;
            long k = DefenseConstant(attacker.Level);
            damage = damage * k / (k + Math.Max(0, defender.Defense));
            return (int)Math.Max(1, Math.Min(int.MaxValue, damage));
        }

        /// <summary>A basic (weapon) attack: 200% ATK, physical, cut by <paramref name="reachPercent"/> when it's a shot.</summary>
        public static DamageRoll RollBasicAttack(Actor attacker, Actor defender, Rng rng, int reachPercent = 100) =>
            RollDamage(attacker, defender, rng, BasicAttackPercent, reachPercent: reachPercent);

        /// <summary>What's left of a hit after the ranged cuts: 100 in melee, 90 for a shot, 63 for a shot at point-blank range.</summary>
        public static int ReachPercent(bool ranged, bool pointBlank) =>
            !ranged ? 100 : pointBlank ? RangedDamagePercent * PointBlankPercent / 100 : RangedDamagePercent;

        /// <summary>The K in the DEF multiplier for an attacker of this level.</summary>
        public static int DefenseConstant(int attackerLevel) => KPerLevel * (attackerLevel + KLevelOffset);

        /// <summary>
        /// Elemental resistance in tenths of a percent (negative is a weakness). Always 0 until the element chart exists
        /// (milestone 1j); the formula already applies it.
        /// </summary>
        static int ResistanceTo(Actor defender, Element element) => 0;

        public static int ExpToNextLevel(int level) => 8 + level * 6;

        /// <summary>One level up: the actor's level growth (and anything else on its stat sheet) is applied again.</summary>
        public static void ApplyLevelUp(Actor actor)
        {
            actor.Level++;
            actor.RecalculateStats();
        }
    }
}

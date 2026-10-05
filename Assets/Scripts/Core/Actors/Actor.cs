using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    public enum Team { Hero, Enemy }

    /// <summary>How an AI partner plays (GAME_PLAN.md): go after enemies, stay with the leader, or hold its ground.</summary>
    public enum PartyTactic { Attack, Follow, Hold }

    /// <summary>
    /// A character or monster standing in the dungeon, with its current stats. Its <see cref="Stats"/> sheet is built
    /// from the definition, its level and its weapon; the final stats below are read from the sheet whenever it
    /// changes (see <see cref="RecalculateStats"/>).
    /// </summary>
    public sealed class Actor
    {
        public Actor(int id, ActorDefinition definition, Team team, GridPos pos, int level = 1)
        {
            Id = id;
            Definition = definition;
            Team = team;
            Pos = PreviousPos = pos;
            Level = Math.Max(1, level);
            Weapon = definition.Weapon;
            SkillCooldowns = new int[definition.Skills.Count];
            RecalculateStats();
            Speed = Stats.Final(StatKind.Spd);
            Hp = MaxHp;
        }

        public int Id { get; }
        public ActorDefinition Definition { get; }
        public string Name => Definition.Name;
        public Team Team { get; }
        public GridPos Pos { get; set; }
        public Direction8 Facing { get; set; } = Direction8.S;

        /// <summary>Where it stood before its last step; partners follow along it in corridors.</summary>
        public GridPos PreviousPos { get; set; }

        /// <summary>For party members the leader isn't controlling.</summary>
        public PartyTactic Tactic { get; set; }

        /// <summary>Active status effects (guard, taunt, mark, root, aura).</summary>
        public List<StatusEffect> Statuses { get; } = new List<StatusEffect>();

        public StatusEffect FindStatus(StatusKind kind)
        {
            foreach (var status in Statuses)
                if (status.Kind == kind) return status;
            return null;
        }

        public int Level { get; set; } = 1;
        public int Exp { get; set; }

        /// <summary>Where the final stats come from: (base + level growth + weapon) x % bonuses + flat bonuses.</summary>
        public StatSheet Stats { get; } = new StatSheet();

        /// <summary>Counts toward the base stats; null fights unarmed (monsters).</summary>
        public WeaponDefinition Weapon { get; set; }

        /// <summary>The weapon's item level (1-100); its flat stats grow with it. Starter weapons are level 1.</summary>
        public int WeaponLevel { get; set; } = 1;

        public int MaxHp { get; set; }
        public int Hp { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int ExpReward { get; set; }

        /// <summary>Chance to crit and the extra damage a crit does, in tenths of a percent (50 = 5%, 500 = +50%).</summary>
        public int CritRate { get; set; }
        public int CritDmg { get; set; }

        /// <summary>Chance to land status effects, and to shrug them off, in tenths of a percent.</summary>
        public int Affinity { get; set; }
        public int Resist { get; set; }

        /// <summary>
        /// Combat speed: one turn every 10000 / Speed AV. Change it mid-fight through DungeonRun.SetSpeed so the
        /// timeline keeps the actor's progress toward its next turn.
        /// </summary>
        public int Speed { get; set; }

        /// <summary>
        /// The ultimate's charge meter, 0 to <see cref="CombatRules.MaxCharge"/>: it fills as the hero acts, deals damage and
        /// takes damage, and carries over between fights within a run. Full means the ultimate is ready.
        /// </summary>
        public int Charge { get; set; }

        public bool UltimateReady => Definition.Ultimate != null && Charge >= CombatRules.MaxCharge;

        /// <summary>Own turns left before each skill (by slot) can be used again.</summary>
        public int[] SkillCooldowns { get; }

        /// <summary>
        /// Party AI: the member this one last swapped places with, and its own turns left before the two may swap again
        /// (so a pair never swaps back and forth).
        /// </summary>
        public int SwappedWithId { get; set; }
        public int SwapBlockTurns { get; set; }

        /// <summary>Party AI: steps a ranged hero took in a row to get out of melee (it stops retreating after one).</summary>
        public int RetreatSteps { get; set; }

        /// <summary>
        /// Party AI: turns it has held a doorway, waiting for the foes to come to it, since it last attacked or used a
        /// skill (or the fight ended). After <see cref="HeroTactics.DoorPatience"/> of them it goes in after all.
        /// </summary>
        public int HeldTurns { get; set; }

        /// <summary>
        /// Party AI: turns in a row the autopilot's leader has waited for the party to heal up between fights, since a
        /// heal last landed on anyone (or a fight or a floor began). After <see cref="HeroTactics.RestPatience"/> of
        /// them it moves on: the heals that are left can't be used where the party stands.
        /// </summary>
        public int RestedTurns { get; set; }

        /// <summary>Own turns it has started so far (heroes: this run).</summary>
        public int TurnsTaken { get; set; }

        /// <summary>
        /// <see cref="TurnsTaken"/> as it was when its coming turn was last pushed back on the timeline, or -1. An actor
        /// can be delayed at most once per its own turn (PROGRESSION.md, "Delays / stuns"), so no stun-lock.
        /// </summary>
        public int DelayedOnTurn { get; set; } = -1;

        /// <summary>Its coming turn has already been pushed back (a stun, a slow): it can't be delayed again until it acts.</summary>
        public bool IsDelayed => DelayedOnTurn == TurnsTaken;

        /// <summary>Enemy AI state: has noticed the hero and is giving chase.</summary>
        public bool Alerted { get; set; }

        /// <summary>Boss state: wound up for a special attack that lands on its next turn.</summary>
        public bool Charging { get; set; }

        /// <summary>Boss state: turns until its special attack is ready again.</summary>
        public int SpecialCooldown { get; set; }

        /// <summary>Boss state: has already called for reinforcements this fight.</summary>
        public bool CalledForHelp { get; set; }

        public bool IsAlive => Hp > 0;

        /// <summary>
        /// Rebuilds the sheet's base (definition + level growth + weapon) and reads every final stat back from the sheet.
        /// Current HP moves with max HP, so a level-up heals by what it adds. Speed is left alone: it only changes through
        /// the timeline mid-fight, and levels never raise it.
        /// </summary>
        public void RecalculateStats()
        {
            var definition = Definition;
            int levels = Level - 1;
            var b = Stats.Base;
            b.Clear();
            b[StatKind.Hp] = definition.MaxHp + levels * definition.HpGrowth + (Weapon?.HpAt(WeaponLevel) ?? 0);
            b[StatKind.Atk] = definition.Attack + levels * definition.AtkGrowth + (Weapon?.AtkAt(WeaponLevel) ?? 0);
            b[StatKind.Def] = definition.Defense + levels * definition.DefGrowth + (Weapon?.DefAt(WeaponLevel) ?? 0);
            b[StatKind.Spd] = definition.Speed + (Weapon?.Spd ?? 0);
            b[StatKind.CritRate] = definition.CritRate;
            b[StatKind.CritDmg] = definition.CritDmg;

            int maxHpBefore = MaxHp;
            MaxHp = Stats.Final(StatKind.Hp);
            Attack = Stats.Final(StatKind.Atk);
            Defense = Stats.Final(StatKind.Def);
            CritRate = Stats.Final(StatKind.CritRate);
            CritDmg = Stats.Final(StatKind.CritDmg);
            Affinity = Stats.Final(StatKind.Affinity);
            Resist = Stats.Final(StatKind.Resist);
            ExpReward = definition.ExpReward + levels * definition.ExpGrowth;
            Hp = Math.Max(0, Math.Min(MaxHp, Hp + MaxHp - maxHpBefore));
        }

        public override string ToString() => $"{Name}#{Id} {Pos} HP {Hp}/{MaxHp}";
    }
}

using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    /// <summary>
    /// What a skill does, in a few plain sentences for the hero stats page (HUD.md, "Pause button and menu"). The text is
    /// written from the skill's own numbers, so a hero's copy of a skill that a class option changed reads as that hero
    /// has it; pass the skill from the hero's kit (<see cref="Actor.Skills"/>, <see cref="Actor.Ultimate"/>,
    /// <see cref="HeroKit.WeaponAttack"/>), never the catalog's.
    /// </summary>
    public static class SkillText
    {
        public static string Describe(SkillDefinition skill)
        {
            var parts = new List<string>();
            string hits = skill.Hits <= 1 ? $"A hit of {skill.Power}% ATK"
                : skill.LastHitPower > 0 ? $"{skill.Hits} hits of {skill.Power}% ATK each, the last one of {skill.LastHitPower}%,"
                : $"{skill.Hits} hits of {skill.Power}% ATK each";
            string theirs = skill.HealsFromUser ? "the hero's" : "their own";
            switch (skill.Effect)
            {
                case SkillEffect.Strike:
                    parts.Add(skill.DashTiles > 0
                        ? $"{hits} on a foe up to {skill.StrikeReach} tiles away in a straight line: the hero dashes up to it first."
                        : $"{hits} on a foe next to the hero.");
                    if (skill.CritRateBonus > 0) parts.Add($"Each hit has +{TreeText.Tenths(skill.CritRateBonus)}% Crit Rate.");
                    if (skill.MovesOn) parts.Add("When that foe falls, the hits that are left go to another one next to the hero.");
                    if (skill.Pierce) parts.Add("Also hits the foe right behind it.");
                    if (skill.Shove) parts.Add($"Shoves it one tile back; if it can't move, the hit does {skill.WallBonusPercent}% more.");
                    break;
                case SkillEffect.Shot:
                    string shot = skill.Hits > 1 ? $"{skill.Hits} shots of {skill.Power}% ATK each" : $"A shot of {skill.Power}% ATK";
                    if (skill.RollTiles > 0) parts.Add($"The hero rolls {Tiles(skill.RollTiles)} the way it is aimed, then shoots.");
                    parts.Add($"{shot} at a foe in sight within {Tiles(skill.Range)}.");
                    if (skill.LeavesTrap == TrapKind.Snare) parts.Add("Leaves a snare where the hero stood.");
                    if (skill.Knockback > 0) parts.Add($"Knocks the foe back {Tiles(skill.Knockback)}.");
                    if (skill.Bounces > 0)
                        parts.Add($"The arrow then bounces to up to {skill.Bounces} more {(skill.Bounces == 1 ? "foe" : "foes")}, each within " +
                                  $"{Tiles(skill.BounceRange)} of the last one hit, for {skill.BouncePercent}% of the hit before. It is one arrow, whatever the bow.");
                    break;
                case SkillEffect.Area:
                    parts.Add($"{hits} on every foe within {Tiles(skill.Radius)} of a foe in sight up to {Tiles(skill.Range)} away.");
                    break;
                case SkillEffect.SharedStrikes:
                    parts.Add($"{skill.Hits} strikes of {skill.Power}% ATK each, shared among the foes within {Tiles(skill.Radius)} of a foe next to the hero.");
                    break;
                case SkillEffect.Heal:
                    parts.Add(skill.HealTarget == HealTarget.Self ? $"Heals the hero for {skill.Power}% of its max HP."
                        : skill.HealTarget == HealTarget.SelfOrAdjacentAlly
                            ? $"Heals the most hurt of the hero and the allies next to it for {skill.Power}% of {theirs} max HP."
                            : $"Heals the hero and every ally within {Tiles(skill.Radius)} for {skill.Power}% of {theirs} max HP.");
                    break;
                case SkillEffect.Dash:
                    parts.Add($"The hero moves up to {Tiles(skill.Power)} in a straight line.");
                    break;
                case SkillEffect.Guard:
                    parts.Add(skill.Radius > 0
                        ? $"Until the hero's next turn, the hero and the allies within {Tiles(skill.Radius)} take {skill.Power}% less damage."
                        : $"Until its next turn, the hero takes {skill.Power}% less damage.");
                    break;
                case SkillEffect.Mark:
                    parts.Add($"Marks a foe in sight within {Tiles(skill.Range)}: for {Turns(skill.StatusTurns)} it takes {skill.Power}% more damage " +
                              "from the hero. When the marked foe falls, the mark moves to the nearest one.");
                    break;
                case SkillEffect.Aura:
                    parts.Add($"For {Turns(skill.StatusTurns)}, the hero and the allies next to it take {skill.StatusPower}% less damage, and at the " +
                              $"start of each of the hero's turns they are healed for {skill.Power}% of the hero's max HP.");
                    break;
                case SkillEffect.Counter:
                    parts.Add($"A stance until the hero's next turn: it takes {skill.StatusPower}% less damage ({skill.BossStatusPower}% less from a " +
                              $"boss), and the first foe that hits it from the next tile is struck back for {skill.Power}% ATK.");
                    break;
            }

            if (skill.DealsDamage)
            {
                if (skill.StunChance > 0)
                    parts.Add($"{skill.StunChance}% chance to stun: the foe's next turn comes {skill.StunPercent}% of a turn later.");
                if (skill.DelayPercent > 0) parts.Add($"Slows each foe hit: its next turn comes {skill.DelayPercent}% of a turn later.");
                if (skill.Status == StatusKind.Taunt)
                    parts.Add(skill.StatusAround
                        ? $"Taunts the foe and every other foe next to the hero: for {Turns(skill.StatusTurns)} they go for the hero."
                        : $"Taunts the foe: for {Turns(skill.StatusTurns)} it goes for the hero.");
                if (skill.Element != Element.None) parts.Add($"{skill.Element} damage.");
            }

            if (skill.IsUltimate) parts.Add("Ultimate: needs a full charge.");
            if (skill.IsQuick) parts.Add(skill.CostPercent == 50 ? "Quick: takes half a turn." : $"Quick: takes {skill.CostPercent}% of a turn.");
            else if (skill.CostPercent != 100) parts.Add($"Takes {skill.CostPercent}% of a turn.");
            if (!skill.IsUltimate && skill.Cooldown == 1) parts.Add("Not two turns in a row.");
            else if (!skill.IsUltimate && skill.Cooldown > 1) parts.Add($"Sits out the hero's next {skill.Cooldown} turns.");
            return string.Join(" ", parts);
        }

        static string Tiles(int count) => count == 1 ? "1 tile" : $"{count} tiles";

        static string Turns(int count) => count == 1 ? "1 turn" : $"{count} turns";
    }
}

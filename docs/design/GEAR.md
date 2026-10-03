# Gear, sets and weapons (design, agreed with Peter on 2026-10-02)

Status: **design only, not started.** Build when Peter says go. Levels, classes, Traces and professions are in
[PROGRESSION.md](PROGRESSION.md). Anything marked *proposed* still needs Peter's OK; all numbers are first drafts to
tune with `-balance` and the `-gear` report.

## Damage formula (do this first)
Today's `2 x ATK - DEF` stops working once stats grow 10x+ over 100 levels and % bonuses stack: DEF either does
nothing or turns hits into 1 damage. The set and weapon effects below assume a multiplicative formula, like Honkai
Star Rail's:

```
damage = (skill% x ATK + extra damage) x (1 + DMG bonus %) x crit x DEF mult x element RES mult
crit      = 1 + Crit DMG on a crit, else 1
DEF mult  = K / (K + target DEF), K = 10 x (attacker level + 20)   (K is data; tune it)
RES mult  = 1 - target's resistance to the element (negative resistance = weakness)
status hit chance = base chance x (1 + Affinity) x (1 - target Resist)
```
- Everything multiplies, so the order doesn't matter: crit always applies on top of every bonus.
- Physical and magic damage both use DEF; elements use resistances. No separate magic defense.
- Skills need tags: **physical/magic**, **melee/ranged/area**, and an optional **element**. Sets and weapons key off them.
- Basic attack = 200% ATK keeps today's damage at level 1 (Uzuki 60 ATK vs a slime's 10 DEF ~= 115 vs 110 now).
- Same commit: the **10x rescale** of HP/ATK/DEF (Uzuki 400 HP / 60 ATK / 30 DEF; enemies, level gains, berries and
  regen too), and base crit becomes **5% Crit Rate / 50% Crit DMG** (HSR). Re-tune with `-balance` afterwards.

## Stats
- **Primary:** HP, ATK, DEF, SPD, Crit Rate, Crit DMG. **Affinity** = chance to land status effects (poison,
  paralysis, sleep, bleed, slow, ...). **Resist** = chance to shrug them off. **Element damage** per element.
- Final stat = (hero base + level growth + weapon stats) x (1 + % bonuses) + flat gear bonuses (head HP, hands ATK).
  Weapon stats count as base, like HSR light cones, so % bonuses scale them; flat gear main stats are not scaled.
- Speed is flat integers only. Crit values are stored in 0.1% steps.
- Speed and crit have budgets (below). Every new source of either must fit them.

## Rarity and item level
| Rarity | Substats | Upgrades | Main stat | Icon |
|---|---|---|---|---|
| **5 stars** (max gear) | 4 | 5 (+0 to +5) | full values below | **red + gold** |
| **4 stars** | 3 | 3 (+0 to +3) | 80% of the 5-star value at the same upgrade | **purple + gold** |

- 4-star gear gets 3 upgrades, not 5. With only 3 substats, each upgrade lands on a given substat 1 time in 3 instead
  of 1 in 4. With 5 upgrades, a 4-star piece would roll *more* speed than a 5-star piece. With 3, it stays below:
  expected SPD substat 4.0 vs 4.5, max 8 vs 14.
- 4-star boots SPD main: 8 / 10 / 11 / 13 (+0 to +3).
- **Item level 1-100.** Flat stats (head HP, hands ATK, weapon stats) scale with item level; % stats don't. Final gear
  is item level 100. Drops take their item level from dungeon depth/difficulty, crafting from the recipe tier.
- Rarity names ("5 stars") are placeholders.

## Slots
| Who | Slots |
|---|---|
| Heroes (the main party of 4) | Head, Body, Hands, Feet (armor) + Ring 1, Ring 2 + 1 Weapon |
| Small monster (partner/summon outside the main 4) | 1 ring (either type) |
| Medium monster (includes Mech Engineer robots) | 2 rings |
| Large monster | any 2 of the 4 armor pieces + 1 of the 2 rings, in any combination |

Armor forms sets with 2-piece and 4-piece bonuses. The two rings form their own 2-piece sets. A large monster can use
one 2-piece armor bonus and can wear SPD boots.

## Main stats (one per piece)
| Slot | Possible main stats |
|---|---|
| Head | **flat HP** (fixed) |
| Hands | **flat ATK** (fixed) |
| Body | HP%, ATK%, DEF%, Crit Rate, Crit DMG, Affinity |
| Feet | HP%, ATK%, DEF%, **SPD (the only slot with a SPD main)** |
| Ring 1 (element ring) | one element's damage, HP%, ATK%, DEF% |
| Ring 2 (affinity ring) | one status's affinity (poison, paralysis, ...), Resist, HP%, ATK%, DEF% |

5-star values, +0 -> +5:
| Main stat | +0 | per upgrade | +5 |
|---|---|---|---|
| Flat HP (head), flat ATK (hands) | 40% of the +5 value | +12% of the +5 value | about 15-20% of a same-level hero's base HP / ATK, by item level |
| SPD (flat) | 10 | +2 | 20 |
| Crit Rate (HSR) | 5.2% | +5.44% | 32.4% |
| Crit DMG (HSR) | 10.4% | +10.88% | 64.8% |
| HP%, ATK% | 15% | +5% | 40% |
| DEF% | 20% | +6% | 50% |
| Affinity, Resist, element DMG, single-status affinity | 15% | +5% | 40% |

## Substats
- 4 distinct substats (3 on 4-star), drawn from the pool, **never the piece's main stat**.
- **Pool (8):** HP%, ATK%, DEF%, SPD, Crit Rate, Crit DMG, Affinity, Resist. Flat stats, specific elements and
  specific status affinities never appear as substats. A bigger pool dilutes SPD and breaks the speed targets.
- A flat-HP head can still roll HP%, and flat-ATK hands ATK%, because those are different stats.
- Odds start uniform; weights live in data.
- Each upgrade raises the main stat and moves **one random substat** (uniform) up one tier. Tiers 1-6 (5-star).

| Substat | T1 | T2 | T3 | T4 | T5 | T6 |
|---|---|---|---|---|---|---|
| SPD (flat) | 2 | 4 | 6 | 8 | 10 | 14 |
| Crit Rate (HSR average roll x n) | 2.9% | 5.8% | 8.7% | 11.7% | 14.6% | 17.5% |
| Crit DMG (HSR average roll x n) | 5.8% | 11.7% | 17.5% | 23.3% | 29.2% | 35.0% |
| HP%, ATK%, DEF%, Affinity, Resist (first draft) | 4% | 6% | 8% | 12% | 18% | 28% |

## Speed budget (protects the breakpoints)
| Source | Max |
|---|---|
| Pre-gear speed: hero/race + class modifier, **clamped to 85-100** (see PROGRESSION.md) | 100 |
| Boots main stat | +20 |
| Substats (5 pieces x tier 6) | +70 |
| Archer's Garb 2-piece (the **only** set that gives SPD) | +6 |
| Weapon (Hunter Bow is the max; any other weapon SPD must be at or below it) | +6 |
| Levels, class tiers, Traces, professions, ring sets | **never** |

- Gear ceiling: 100 + 20 + 70 + 6 + 6 = **202**. Combat buffs and turn-advance effects are budgeted with skills.
- Expected, 5-star +5 gear, SPD boots, SPD picked when crafting:
  - Base 100 + Archer 2-piece: about 148; 160 needs roughly 6-8 Tuning Stones spent on SPD.
  - Base 100 + Archer 2-piece + Hunter Bow: about 154; about 13% reach 160 before Tuning Stones. Archers get there
    first, but it's still a chase.
  - Base 85 + Archer 2-piece: reaches 134 about half the time. Base 90: about 87%.
- Breakpoints against a speed-100 enemy: 120, 134, 143, **160 (the chase)**, 172, 200.
- Build a `-gear` report in `Tools/CoreTests` (simulate thousands of gear sets, print breakpoint odds per base speed),
  plus a test that fails if these targets drift.

## Crit budget (a decent crit build reaches about 100%)
| Source | Crit Rate |
|---|---|
| Base | 5% |
| Body main stat (+5) | 32.4% |
| Crafted with Crit Rate picked on the other 5 pieces (expected) | about 33% |
| Hawk's Eye rings (2-piece) | 8% |
| Duelist's Leathers (armor 2-piece) | 8% |
| **Subtotal from gear** | **about 86%** |
| A crit class at tier 25 (PROGRESSION.md) | up to 6% |
| A crit hero's Traces | up to 8% |
| **Dedicated crit build** | **about 100%** |

Crit Rate caps at 100%. Weapons that add Crit Rate are a bonus on top.

**Crit ratio target 1 : 1.5 (100% Crit Rate / 150% Crit DMG).** Keep HSR roll values (one Crit DMG roll = two Crit
Rate rolls); the ratio comes out of which 2 substats a player picks when crafting. Estimates at base speed 100, crit
class and crit Traces included; average hit = 1 + Crit Rate x Crit DMG:

| Build | Crafting picks | Sets | Crit Rate | Crit DMG | SPD | Average hit |
|---|---|---|---|---|---|---|
| **Full crit** | Crit Rate + Crit DMG | Duelist's Leathers 4-piece, Hawk's Eye | ~100% | **~150%** | ~128 | x2.5 |
| **Speed + crit** | SPD + Crit Rate | Archer 2 + Duelist 2, Hawk's Eye | ~100% | ~85% | ~148 | x1.85 |

- Full crit does about 15% more damage over time. Speed + crit gets about 16% more turns, which also buy heals,
  buffs, movement and the 134 breakpoint. Both builds are viable, and that's the intended tension.
- Rough level-100 scale (depends on the level-100 stat growth): about 2,000 ATK after gear; a 200% skill against a
  same-level enemy does about 3,000 normally and about 8,000 on a full-crit build's crit.

## Armor sets (2-piece / 4-piece)
| Set | 2-piece | 4-piece |
|---|---|---|
| **Archer's Garb** | +6 SPD | Ranged skills deal +0.4% damage per SPD above 100 (+24% at 160, max +40%) |
| **Light Warrior** | +12% ATK | Physical skills deal extra damage equal to 25% of ATK |
| **Mage Robe** | +10% magic damage | Magic skills deal extra damage equal to 25% of ATK |
| **Heavy Armor** | +15% DEF | Each hit taken is reduced by 15% of final DEF (never below 30% of the hit) |
| **Duelist's Leathers** | +8% Crit Rate | Each crit grants +6% Crit DMG, stacking 3 times, until combat ends |
| **Pilgrim's Vestments** | +10% healing done | +5% ultimate charge at the start of each of the wearer's turns |
| **Windrider's Cloak** | +10% ultimate damage | After using the ultimate, this hero's next turn comes 25% sooner on the timeline |
| **Shadowstalker** | +10% Affinity | +12% damage against targets with a status effect, +20% with 2 or more |
| **Bloodrage Hide** | +12% HP | When hit, +8% Crit Rate for 2 turns, stacking twice |

Changes from Peter's first ideas, and why:
- **Archer 4-piece:** a damage bonus scaled by SPD above 100, instead of flat damage equal to a % of SPD. Speed
  stays around 85-200 while ATK and HP grow 10x+, so a flat bonus from SPD would fade to nothing by mid-game.
- **Mage Robe 2-piece:** +10% magic damage instead of +6 SPD. Four armor slots can hold two 2-piece sets, so
  Archer + Mage would stack +12 SPD and break the speed budget.
- **Light Warrior / Mage Robe 4-piece:** 25% of ATK as extra damage is roughly +12-25% on a typical 100-200% skill,
  in line with HSR 4-piece sets.
- **Heavy Armor 4-piece:** a flat cut per hit, so it's strong against many small hits and weaker against a boss's big
  slam. That's intended flavor.
- Element sets ("+10% Fire damage" style) come once the element list is final.

## Ring sets (2-piece)
| Set | Bonus |
|---|---|
| **Hawk's Eye** | +8% Crit Rate; +10% ultimate charge gained |
| **Executioner's Seal** | +16% Crit DMG; +10% ultimate charge gained |
| **Bloodbond** | Skill damage heals the user for 8% of the damage dealt |
| **Mender's Band** | +12% healing done |
| **Arcane Ward** | At the start of each fight, gain a barrier worth 15% of max HP that absorbs damage before HP; it recharges for the next fight |
| **Stoneguard** | -8% damage taken |
| **Heartwood Band** | +12% HP |
| **Venom Coil** | +10% Affinity; poison and bleed damage +15% |
| *Proposed:* **Tempo Signet** | +12% ATK; +10% damage while SPD is at least 134, +16% at 160 or more. Rewards breakpoints without adding SPD |

## Weapons
Flat HP + flat ATK + small flat DEF (scaled by % bonuses as part of base), plus a passive. No random substats. Leveling
a weapon raises its stats and strengthens the passive, and some weapons gain extra stats at high levels. Power budget:
a maxed passive is worth about +15-25% damage, or the same in utility. Sizes: ATK about 30-40% of a same-level hero's
base ATK, HP and DEF about 15-25%. Names are placeholders.

| Weapon | Passive (level 1 -> max) | Balance notes |
|---|---|---|
| **Arcane Sword** | Melee attacks and skills reach 2 tiles in a straight line. A target hit at distance 2 also takes magic damage equal to 30% of ATK | Corner rule still applies |
| **Dual Blades** | After a single-target skill, a left-hand follow-up hits for 60% -> 80% of that skill's damage. A class/Trace node can raise it to 100% | About 25% less weapon ATK than other weapons. The follow-up costs no turn, can crit, never triggers another follow-up, and "extra damage" set bonuses apply once per skill |
| **Mage Staff** | Magic skill range x1.5 (rounded down). Magic skills blast the 8 tiles around the target for 50% damage; the center target takes +20% | |
| **Great Shield** | Block chance = 10% + a quarter of your bonus DEF% (max 40%). A block halves the hit | Uses bonus DEF% from gear and sets, not raw DEF. Raw DEF grows 10x+ over 100 levels, so "% of DEF" would go from useless to 100% block |
| **Great Sword** | Physical skills deal +10% -> +20% damage. Crit applies on top, like everything | |
| **Piercer Blade** | Physical skills and ultimates apply Bleed for 2 turns: at the start of its turn the target loses 2% -> 4% of its max HP, capped at 50% of the wielder's ATK per tick | Uses Affinity vs Resist. The cap stops % max-HP damage from deleting bosses |
| **Long Sword** | *Passive later.* Haiden's weapon. Stats only for the first playtest | |
| **Gauntlets** | *Passive later.* Kristela's weapon, and the Monk/fist-fighter weapon type. Stats only for the first playtest | |
| **Hunter Bow** | **Multishot:** ranged physical skills fire 2 arrows at 60% -> 70% damage each. Each arrow picks its own target in range; both hit the same one if it's alone. +6 SPD | 120-140% total, each arrow rolling crit and on-hit effects separately. Full double damage would be far over budget. +6 is the weapon SPD max |

## Where gear comes from
- **Dungeon drops:** random main stat and substats. Found as **unappraised boxes** that are appraised at home after
  the run. **Boxes survive defeat.** Contents roll from the run seed so runs stay reproducible.
- **Crafting (farm):** choose the set, main stat and **2 substats**; the rest are random. Choosing a 3rd or 4th costs
  extra resources. Blacksmiths forge metal armor and weapons; Seamstresses make cloth and leather (PROGRESSION.md).
- **Tuning Stone (rare):** one upgrade goes to a substat the player picks instead of a random one.
- **Salvage:** single, **bulk**, and **auto-salvage** rules (e.g. "auto-salvage all 4-star") turn gear into dust and
  materials that feed upgrades and crafting.
- **Weapons:** gacha (later), drops, crafting.
- Resources are farmed and traded. The system store stays limited.

## Building it (notes for the coding session)
Suggested order:
1. Damage formula, 10x rescale and base crit, re-tuned with `-balance`.
2. Per-actor stat sheet (base + level + weapon) x % + flat.
3. Skill tags (physical/magic, melee/ranged/area, element).
4. Gear items, rarity, item level, upgrades, Tuning Stones.
5. Set bonuses.
6. Weapons and passives.
7. Unappraised boxes, appraisal and salvage.
8. Crafting.
9. Monster slot rules.
10. The `-gear` report.
11. UI: the first equipment screen is a simple between-runs screen until the farm exists.

All tables are data, not code. Tests: substat rules, upgrade tiers, crafted picks, Tuning Stones, set counting
(including monsters' 2 armor pieces), slot rules per monster size, seeded drops, the speed and crit budgets.

## Open questions
1. Final element and status lists (Peter is still deciding; statuses so far: poison, paralysis, sleep, bleed, slow).
   Decided so far: Darkness is the "everything else" magic element, covering time, cosmic and space magic.
2. Ultimate charge numbers (how fast the meter fills, and the set and ring bonuses to it). There is no mana; see
   PROGRESSION.md, "Skill resources".
3. Final set and weapon names.

# Hero progression: levels, classes, Traces, professions (design, 2026-10-03)

Status: **design only, not started.** Build when Peter says go, after the gear groundwork in [GEAR.md](GEAR.md)
(damage formula, rescale, stat sheet). Anything marked *proposed* still needs Peter's OK. Class names and numbers
are first drafts to get the code structure right; content gets tuned and expanded later.

Classes, Traces and professions are for **heroes only**. Monsters get their identity from breeding traits and jewelry.

## Layers and their jobs
| Layer | Job |
|---|---|
| **Traces** | Who this hero is: unique passives and modest stat nodes. Keeps every (gacha) hero special even though most classes are open to all |
| **Classes** | What the player builds: skills, ultimates, playstyle |
| **Professions** | Home-base jobs: passive play, running the farm, their own skills and ultimates, and unlocking some advanced classes |
| **Gear** | Stats, speed breakpoints and crit (GEAR.md) |

## Player character and starting party
- The player **creates their own character** and picks **one of the 5 kingdoms** as their origin, which opens that
  kingdom's inherited classes to them.
- The starting party of 4 is the player + three partners. Players then recruit or pull heroes to swap into the party
  and to fill **profession slots at the home base**.
  - **Uzuki** (boy, the first companion): 5th kingdom (Medieval Realm). Signature class **Crystal Ice Legion Hunter**: an
    Ice Hunter Bow user who summons ice avatars to fight alongside.
  - **Haiden** (boy): 3rd kingdom (Dynasty Nation). Starts as a **Paladin**, later trains fire **Rune Warrior**
    toward his signature class **Runegod Fire Blade**.
    Concept: [docs/concept/haiden.jpg](../concept/haiden.jpg): spiky orange-brown hair, blue headband with long
    tails, red eyes.
  - **Kristela** (girl): 5th kingdom (Medieval Realm), a princess. Base class **Monk**; signature class **Princess
    Timeless Monk**. Concept: [docs/concept/kristela.jpg](../concept/kristela.jpg): long wavy blonde hair, blue
    eyes, gold X-shaped hair clip on one side.
- GAME_PLAN.md still calls Uzuki the main character and needs updating.

**Starting builds (first playtest):**
| Hero | Class | Role | Profession | Base SPD (*proposed*) |
|---|---|---|---|---|
| Uzuki | Archer, speccing into Mage (Ice) toward Crystal Ice Legion Hunter | **Utility DPS** (ranged; traps, slows and control) | Alchemist (potions for the team) | 95 |
| Haiden | Paladin (later Rune Warrior, toward Runegod Fire Blade) | **Tank first, with some healing** (frontliner; fire runes that guard, mend and hit back) | Blacksmith (equipment) | 90 |
| Kristela | Monk | **Melee DPS** (speed build) | Chef (food buffs and heals) | 100 |

Together they cover tank (with some healing), melee DPS and utility DPS, so the player's own character can fill
whatever role they like.

**Weapons:** Uzuki uses a **Hunter Bow**, Haiden a **Long Sword**, Kristela **Gauntlets** (see GEAR.md).

**Starting kits (approved).** Skills are borrowed from D&D; numbers are first drafts to tune.

**Turn rules are ours, not D&D's (decided):**
- Each turn is one choice: **step one tile, or use one attack/skill**. The AV timeline decides who goes next.
- **Quick skills** cost half a turn, so the hero comes up again sooner. At most one per hero. The UI shows a small
  "Quick" tag, never D&D terms like "bonus action".
- **Reactions** (few) fire automatically on their trigger, at most once per cycle, and never prompt the player.
- Statuses stay few, short and shown as icons. The player controls one hero while partners use AI, and Auto is
  always available. Deep enough to be satisfying, light enough for manual phone play.

**Skill resources (decided: no mana):**
- **No mana.** Every skill has a **1-turn cooldown**: after using it, it's unavailable on the hero's next turn, so
  the same skill can't be used twice in a row.
- Every hero has an **always-ready weapon attack** with no cooldown, so there's always something useful to do
  (names are drafts: Uzuki's Quick Shot, Haiden's Sword Slash, Kristela's Jab).
- **Ultimates use a charge meter** that fills as the hero acts, deals damage and takes damage. When it's full, the
  ultimate is ready.
- Berries heal HP again (in 1d they restored mana).
- **Ultimate charge carries over between fights** within a run (decided), so walking into the boss with a full
  meter is a real strategy. Tuning (approved 2026-10-03): about two ultimates per hero in a boss fight and one every
  2-3 normal fights. One per normal fight would mean an ultimate every other action, since normal fights are short.

**Ranged vs melee (decided).** A ranged leader shouldn't solo every fight while the melee heroes watch.
- **Ranged reach: 5 tiles.** Ranged weapon attacks and shots deal **90%** of an equivalent melee hit (a multiplier
  on the skill's %). A light touch, so ranged builds aren't punished.
- **Point-blank rule (from D&D):** a ranged attack made while an enemy is adjacent to the shooter deals **30% less**.
- **Shots pass through allies.** No friendly fire, nothing to worry about.
- **Battle formation:** when a fight starts, melee partners move to the front and ranged ones hang back behind
  them.
- **Melee partners always engage:** in a fight they pick an enemy near the leader (within 6 steps' walk) and path
  their way to it, around allies when that's a short way (at most 4 steps longer than the way through them) or
  swapping past a ranged ally. When allies hold the only way in (a corridor, a doorway), they close up and wait right
  behind them, ready to take a place at the front.
- **Ranged heroes step out of melee:** with an enemy adjacent, a ranged hero steps back to a tile it can still shoot
  from (Uzuki's Rolling Shot does this and attacks in one turn). If there's no such tile, or the enemy keeps
  following, it shoots anyway at the point-blank penalty rather than retreating forever.
- **Swaps, without loops.** A swap is allowed when it either puts a melee hero next to an enemy (or strictly closer
  to one), or **moves a badly hurt hero away from enemies** ("run to safety"; this one works between two melee heroes
  too), or lets a partner get past the partner that follows it in line when no enemy is next to either ("regroup";
  only ever the earlier past the later, never the leader). A pair that just swapped can't swap back for a few turns. The soak test fails if it sees the same two heroes
  swapping back and forth.
- **Ranged targets anything within 5 tiles that's in sight** (decided), not only along the 8 grid lines. Walls block
  shots; allies don't. Damage stays at 90% of melee.
- **The party stays together** (Peter's playtest note, 2026-10-03): ranged partners take a tile to shoot from only
  near the leader and only by a short way; otherwise they stay behind the line. On Auto, the leader with partners in
  a fight goes for the enemies that are after the party (up to 12 steps) and waits behind a partner that holds the
  way.

**Targeting and input (decided).**
- **Two steps for skills and ultimates:** tap the skill, the tiles it can reach light up (Fire Emblem style) with
  valid targets marked, then tap the target. A lone valid target is preselected, so one more tap confirms. Area
  skills (e.g. Volley) show their area.
- **Basic Attack button, always free:** the hero's weapon attack (no cooldown) has its own button and works the same
  way: tap it, then tap the target.
- **Tapping an enemy in reach** uses the basic attack on it directly: one deliberate tap per hit.
- **Walking into an enemy no longer attacks.** The hero turns to face it and keeps the turn, like bumping a wall, so
  the D-pad never causes accidental hits.
- **Auto picks targets deliberately** through the same two steps: the marked enemy first, then the lowest HP.
- **Bigger fights:** more enemies per encounter, including some that close distance quickly, so no single hero can
  clear a room alone.
- **Ranged enemies** come later, under the same limits.
- Playtests can start with Haiden or Kristela leading.

| Hero | Skill | Effect |
|---|---|---|
| Uzuki | **Hunter's Mark** | Bonus action. The marked target takes +25% damage from Uzuki for 3 turns; the mark jumps to a new target on a kill |
| Uzuki | **Power Shot** | Heavy ranged shot (~300% ATK) that knocks the target back 1 tile, e.g. onto a trap |
| Uzuki | **Rolling Shot** | Roll 2 tiles, then shoot (~150% ATK). Leaves a **snare trap** on the tile Uzuki left, rooting the first enemy that steps on it |
| Uzuki | Ultimate: **Volley** | Arrows rain on a 3x3 area at range |
| Haiden | **Heal** | Heal an adjacent ally or himself for ~20% of **Haiden's** max HP (tanky builds heal more) |
| Haiden | **Divine Strike** | Smite (~250% ATK). Fire damage until a Light element is decided |
| Haiden | **Shoulder Bash** | Shove 1 tile; the target must attack Haiden on its next turn (like Compelled Duel). Against a wall: +50% damage instead of moving |
| Haiden | Ultimate: **Aura of Protection** | 3 turns: Haiden and the allies next to him take 30% less damage, and he heals them all (himself too) at the start of each of his turns. The aura covers its caster (decided 2026-10-03) |
| Kristela | **Piercing Punch** | Hits the target and the enemy behind it (~220% ATK) |
| Kristela | **Ki Heal** | Quick skill: heal herself ~25% |
| Kristela | **Stun Strike** | ~160% ATK with a chance (Affinity vs Resist) to **stun**: the target's next turn is pushed back 50% of a turn on the timeline (bosses 25%). No skipped turns, so no stun-lock |
| Kristela | Ultimate: **Flurry of Blows** | 5 rapid strikes, then her next turn comes 30% sooner |

## Levels 1-100
- **Each level gives 1 point** (100 at level 100), spent on class or profession tiers in any mix.
- **Free-for-all (decided):** no split and no separate profession points. Players can hyper-spec into advanced
  classes or professions, or do a profession-only run for fun.
- **EXP curve: linear.** EXP to the next level rises in a straight line, from 1x at level 1 to 3x at level 99 -> 100.
  Late levels cost more but never hit a wall. Split of the total EXP: levels 1-50 about 37%, 50-80 about 35%, 80-100
  about 28%.
- **Time target: about 1 week of daily play for a hero's first 1 -> 100**, mostly from EXP materials, as in HSR.
- **EXP sources:** EXP books from EXP-material dungeons and (*proposed*) from Academy Teachers at home. Dungeon monster
  and boss EXP is a bonus on top. EXP a hero can't use (at the cap, or blocked by an ascension gate) turns into
  **EXP shards**, which craft more EXP books.
- *Proposed* **catch-up:** heroes below your highest-level hero earn bonus EXP, so the 2nd, 3rd and 4th party
  members don't each take a full week.
- *Proposed* **ascension gates** every 10 levels (10, 20, ... 90): leveling past one needs materials (boss drops and
  farm goods). Gates pace progress without a harsh curve, tie leveling to the farm-dungeon loop, and unlock Trace
  nodes. Unlocking kingdoms could raise the reachable cap.
- EXP still carries in and out of dungeons, win or lose.

## Energy (the daily limit)
- **Energy** refills **fully every 24 hours** at the daily reset.
- **Costs energy:** gear-farming dungeons, special bosses, and **EXP-material dungeons**. EXP materials have to cost
  energy, or nothing holds the "1 week to 100" target.
- **Free, no energy:** story progression, regular dungeons and their bosses, and the farm. Players are never locked
  out of playing.
- **Reserve (approved):** unspent energy carries over, capped at 2 days' worth.
- **Weekly bosses:** a few clears per week, rewarding **summoning crystals** (the pull currency) and resources.
- **Refills:** refill items from the battle pass and events; premium refills limited per day. The battle pass and
  paid currency fall under GAME_PLAN.md's store and legal checks.
- This answers GAME_PLAN.md open question 4 (stamina and timers).

## Classes
- Every class has **tiers 1-25**; each tier costs 1 point. 100 points = **4 maxed classes** (or professions, in any
  mix) at level 100.
- **Milestones:** tier 5, 10, 15 = a skill pick or skill upgrade; **tier 20** = major ultimate upgrade or alternate
  ultimate; **tier 25** = major boost to the class's skills.
- **Three options at every milestone** (Peter, 2026-10-03), for classes and professions alike: a class reads as
  three paths side by side, and the player picks one option per milestone tier, **from any path: paths can be mixed
  freely** (Peter: "let them mix"). The screen is a tree of spheres like Peter's "Archer Mastery Path" reference
  (ART.md, "Later"). Agreed with it: a hero starts at tier 1 of their class; speeds stay as they are today; respec is
  free below level 20, and the tea and the class masters come later.
  *Proposed, not yet confirmed:* an option that upgrades a skill the hero doesn't know teaches the base skill
  instead; skills are tied to a weapon type (bow, sword, fists, or none). The drafts are under "Starting class
  content" and "Starting profession content".
- **Every tier** (milestones too) also grants a small class stat bump. **Never SPD.** Budget per class at tier 25, 1-2
  stats each: HP/ATK/DEF up to +15%, Crit Rate up to +6%, Crit DMG up to +12%, Affinity/Resist/healing up to +12%.
- Milestone skills can **upgrade** an existing skill (numbers or effects), **add** a skill to the hero's pool, or
  **replace** a skill or the ultimate with a variant. Replacements are A-or-B picks, so a loadout can't break.
- Every hero has **their own starting kit** (3 skills + 1 ultimate, per GAME_PLAN.md); classes and professions
  upgrade, add to or replace it.
- **Loadout:** 3 skills + 1 ultimate, chosen from everything the hero has unlocked. Edited **only at home base** in
  the hero menu. Builds on the 1d skill system: upgrade/add/replace are modifiers on skill data.
- **Advanced classes** require tiers in one or more base/advanced classes, and some require a profession tier. Same
  25-tier path and milestones. Example: **Spellblade = Warrior 15 + Mage 10**. Prerequisite points count toward the
  100, so players trade depth against breadth.
- **Kingdom locks (approved).** Base classes are open to every hero. Advanced classes come in two kinds:
  - **Inherited classes:** high-tier, kingdom-specific classes open only to heroes from that kingdom: heroes
    recruited or pulled from its banner, or a player character who picked it as their origin. Generalist heroes are
    locked out.
  - **Kingdom affinity** for all other advanced classes: anyone can take them, but heroes from the class's kingdom
    need fewer prerequisite tiers (e.g. 5 fewer).

  This keeps gacha heroes distinct without walling off most builds.
- Turn-manipulation effects (Strategist, Shogun, Windrider's Cloak, stuns) need their own budget like speed:
  - **Advances:** at most 30% of a turn per effect, and an actor can't be advanced more than once per enemy turn
    (*proposed*).
  - **Delays / stuns (decided):** a stun pushes the target's next turn back on the timeline instead of skipping it;
    at most 50% of a turn per effect (25% on bosses), and an actor can be delayed at most once per its own turn. The
    same rule applies when monsters and bosses stun heroes later.
- Transformation ultimates last a set number of the hero's own turns, so the timeline shows when they end.

## Starting class content (drafts, 2026-10-03)
Numbers are first drafts to tune with `-balance`. At today's levels (1-10) players reach tiers 5 and 10, so those
matter most for now. Each option either upgrades a skill of the class or teaches a new one.

### Archer (Uzuki's class): seen by Peter, who then asked for the third path and free mixing
Every tier: +0.4% ATK and +0.2% Crit Rate (+10% and +5% at tier 25).

| Tier | Marksman (single-target damage) | Hunter (traps and control) | Trickshot (several targets) |
|---|---|---|---|
| 5 | **Deadly Mark:** Hunter's Mark gives +40% (was +25%) | **Crippling Shot** (new skill): 180%, the target's next turn comes 30% later | **Bouncing Shot** (new skill): 160%, then it bounces to up to 2 more foes within 3 tiles, each bounce at 70% of the last hit |
| 10 | **Heavy Draw:** Power Shot hits for 360% (was 300%) and knocks back 2 tiles | **Barbed Snare:** a snare also deals 150% ATK when it springs; 5 per floor (was 3) | **Piercing Arrow:** Power Shot also hits every foe in a line behind the target for 60% |
| 15 | **Steady Aim:** a shot after a turn without moving deals +25% | **Shared Mark:** allies also deal +15% to the marked foe | **Splitting Arrows:** Quick Shot bounces once to a second foe for 50% |
| 20 | **Deadeye** (alternate ultimate): one arrow for 900% on a single foe | **Pinning Volley:** Volley also slows what it hits by 30% and leaves a snare at its center | **Storm of Arrows:** Volley hits 3 times (was 2) |
| 25 | **Master Marksman:** Archer shots deal +15%, with no point-blank penalty | **Master Hunter:** marks, slows and snares last one turn longer, and Hunter's Mark takes no time | **Master Trickshot:** one more bounce, and bounces and pierces deal full damage |

### Paladin (Haiden's class): approved by Peter as a starting point (2026-10-03)
Every tier: +0.4% HP and +0.4% DEF (+10% each at tier 25).

| Tier | Guardian (protection) | Devotion (healing) | Crusader (damage) |
|---|---|---|---|
| 5 | **Challenge:** Shoulder Bash also taunts every foe next to Haiden, for 2 turns | **Greater Heal:** Heal restores 30% (was 20%) | **Searing Smite:** Divine Strike hits for 320% (was 250%) |
| 10 | **Shield Wall** (new skill): Haiden and the allies next to him take 40% less damage until his next turn | **Healing Word** (new Quick skill): heals an ally within 3 tiles for 12% of Haiden's max HP | **Sweeping Slash** (new skill): hits up to three foes in front of him for 150% each |
| 15 | **Stand Firm:** Haiden takes 20% less damage from foes he has taunted | **Radiant Smite:** Divine Strike also heals the most hurt ally next to him for 10% of his max HP | **Judgment:** Divine Strike deals +50% to a taunted foe |
| 20 | **Bastion:** Aura of Protection blocks 40% (was 30%) and lasts 4 turns (was 3) | **Sanctuary:** the aura heals 15% a turn (was 10%) and reaches 2 tiles | **Holy Wrath** (alternate ultimate): fire on every foe next to Haiden, 350% each, and they are taunted |
| 25 | **Master Guardian:** his taunts last a turn longer, and allies next to him always take 10% less damage | **Master of Devotion:** his heals restore 25% more, and Heal reaches allies 2 tiles away | **Master Crusader:** Paladin strikes deal +15%, and a kill with one heals him for 10% |

### Monk (Kristela's class): approved by Peter as a starting point (2026-10-03)
Every tier: +0.4% ATK and +0.4% Crit DMG (+10% each at tier 25).

| Tier | Striker (damage) | Windwalker (speed) | Mystic (ki: control and sustain) |
|---|---|---|---|
| 5 | **Iron Fist:** Piercing Punch hits for 260% (was 220%) and reaches a third foe in the line | **Step of the Wind** (new Quick skill): moves up to 3 tiles in a straight line | **Stunning Fist:** Stun Strike hits for 200% (was 160%) with an 80% chance (was 60%) |
| 10 | **Double Jab:** Jab hits twice for 120% each (was once for 200%) | **Momentum:** a Jab right after a skill takes 25% less time | **Ki Surge:** Ki Heal restores 35% (was 25%) and her next strike deals +30% |
| 15 | **Finishing Blow** (new skill): 220%, doubled against a foe below 30% HP | **Deflect:** once per cycle, the first hit on Kristela deals half damage (a reaction) | **Ki Guard** (new Quick skill): she takes 40% less damage until her next turn |
| 20 | **Thousand Fists:** Flurry of Blows is 7 hits (was 5) | **Wind Dance** (alternate ultimate): for 3 of her turns everything she does takes 25% less time | **Quivering Palm** (alternate ultimate): one strike for 500% that always pushes the target's next turn back (50%, bosses 25%) |
| 25 | **Master Striker:** Monk strikes deal +15%, and her multi-hit skills gain +10% Crit Rate | **Master Windwalker:** her first turn in a fight comes 30% sooner, and she may carry two Quick skills (*proposed change*: the approved draft said "Quick skills no longer sit out a turn", which clashes with one Quick skill per hero) | **Master Mystic:** her stuns always land, and a foe she has stunned takes +20% damage from her until it acts (changed 2026-10-04: the first draft let Ki Heal heal her allies, and healing isn't handed to everyone) |

## Starting profession content (drafts)
Peter approved the first drafts as a starting point on 2026-10-04 ("We can fine tune them later after we get the
game going and farm tested") and then asked for more variety. **This is the revision after that; he hasn't
reviewed it yet.**

**Variety rule (Peter, 2026-10-04):** professions should differ in what they bring: buffs, debuffs, utility, item
support, companion support, traps, enhancing oneself, healing. **Healing is not handed to every class or
profession**; it has to make sense for the one that has it. How the starting set is split:

| | Brings | Healing |
|---|---|---|
| Alchemist | debuffs by flask, item support | none (it makes the party's berries better) |
| Blacksmith | armor-breaking debuffs, enhancing himself, later his allies | none |
| Chef | healing by food, buffs by food, later treats for monster companions | yes: the healing profession of the three |
| Paladin (class) | protection, taunts, smites | yes: allies and himself |
| Monk (class) | strikes, tempo, stuns | herself only (Ki Heal) |
| Archer (class) | marks, traps, shots at several targets | none |

Traps beyond the Archer's snares belong to the Hunter profession, and companion support to the Animal Trainer and
the Chef, when those are drafted.

Each profession has two paths that work in the dungeon and a farm path. **The farm path's spheres are shown but
stay locked until the farm exists** (decided 2026-10-04; the Blacksmith's and the Alchemist's also need gear), so
for now a profession offers two options per tier. Profession skills join the hero's pool for the 3 skills +
1 ultimate loadout.

Rules these drafts lean on (Peter has seen them and raised no objection):
- Four building blocks that later classes reuse: a foe **takes more damage**, a foe **deals less damage**, an ally
  **deals more damage**, an ally **takes less damage**, each for a number of the target's own turns. Two effects of
  the same kind don't stack: the stronger one applies.
- "At most one Quick skill per hero" is read as one in the loadout. Profession skills take a full turn.

### Alchemist (Uzuki's profession): debuffs and item support
Every tier: +0.4% Affinity and +0.4% Resist (+10% each at tier 25).

| Tier | Bomber (debuffs) | Brewer (item support) | Transmuter (farm, locked for now) |
|---|---|---|---|
| 5 | **Acid Flask** (new skill): thrown at a foe within 4 tiles: 120%, and it takes +20% damage from everyone for 2 turns | **Berry Tonic:** berries heal 50% more, and he can throw one to an ally within 4 tiles | Potions brew 20% faster |
| 10 | **Frost Flask** (new skill): every foe in a 3 x 3 area within 4 tiles takes 100%, and their next turn comes 30% later | **Forager:** the party finds an extra berry on every floor | A 15% chance of a second potion |
| 15 | **Smoke Bomb** (new skill): every foe in a 3 x 3 area within 4 tiles deals 25% less damage for 2 turns | **Berry Bomb** (new skill): turns a berry into a bomb: thrown up to 4 tiles, 250% to every foe in a 3 x 3 area | Reroll one substat of a gear piece for materials |
| 20 | **Philosopher's Fire** (alternate ultimate): a 5 x 5 blast within 4 tiles, 300% to every foe in it | **Transmute** (alternate ultimate): a foe within 4 tiles that is below 30% HP, and not a boss, turns into a berry | Craft Tuning Stones |
| 25 | **Master Bomber:** his flasks reach 5 tiles and their effects last a turn longer | **Master Brewer:** berries heal twice as much, and eating or throwing one takes half a turn | Once a day, a double batch |

The Brewer path grows once potions exist as items (brewed on the farm and taken into runs).

### Blacksmith (Haiden's profession): armor breaking and enhancement
Every tier: +0.4% ATK and +0.4% DEF (+10% each at tier 25).

| Tier | Hammer (debuffs) | Tempering (enhancing himself, then allies) | Forgemaster (farm, locked for now) |
|---|---|---|---|
| 5 | **Sundering Strike** (new skill): 180%, and the target takes +20% damage for 2 turns | **Temper Blade** (new skill): his own attacks deal +25% for 3 turns | Forging takes 20% less time |
| 10 | **Hammer Blow** (new skill): 220%, and the target's next turn comes 30% later | **Reinforce Armor** (new skill): he takes 30% less damage for 3 turns | One substat of a crafted piece starts a tier higher |
| 15 | **Shatter:** Sundering Strike's bonus is +35% and lasts 3 turns | **Field Forge:** Temper Blade and Reinforce Armor can be used on an ally next to him instead | Salvage returns 25% more |
| 20 | **Armor Shatter** (alternate ultimate): 500%, and the target takes +40% damage for 3 turns (a boss +20%) | **Battle Forge** (alternate ultimate): for 3 turns every ally deals +25% damage and takes 15% less | Once a day, a masterwork: a crafted piece with a third substat of your choice |
| 25 | **Master Hammer:** his strikes deal +15%, and sundered foes also deal 15% less damage | **Master Temperer:** his enhancements are a quarter stronger and last a turn longer | A third substat choice costs nothing extra |

### Chef (Kristela's profession): healing and buffs by food
Every tier: +0.4% HP and +0.4% healing done (+10% each at tier 25).

| Tier | Field Cook (healing) | Spice Rack (buffs) | Gourmet (farm, locked for now) |
|---|---|---|---|
| 5 | **Trail Snack** (new skill): an ally next to her, or herself, heals 20% of max HP | **Spicy Skewer** (new skill): an ally next to her, or herself, deals +20% damage for 3 turns | A meal at home gives the party +10% max HP for the next run |
| 10 | **Hearty Stew** (new skill): every ally within 2 tiles heals 6% of max HP at the start of each of their next 3 turns | **Strong Brew** (new skill): an ally next to her takes their next turn 30% sooner | One more food item can be taken into a run |
| 15 | **Second Helping:** her food reaches allies 3 tiles away, and Trail Snack heals 30% | **Family Recipe:** her buffs reach every ally within 2 tiles of the one she feeds | Treats that strengthen monster companions (when they join the party) |
| 20 | **Feast** (alternate ultimate): heals every ally for 40% of max HP | **Banquet** (alternate ultimate): for 3 turns every ally deals +25% damage with +20% Crit Rate | Once a day, a banquet with a large buff for a whole run |
| 25 | **Master Cook:** her food heals 25% more | **Master of Spice:** her buffs are a quarter stronger and last a turn longer | Meals cost half the ingredients |

## Class list (first draft)
Each kingdom's classes match its flavor, so players know what style they're building toward. Prerequisites marked
*TBD* are still to design.

### Medieval Realm (5th): the classic classes, faith
- **Base:** **Warrior** (offensive melee), **Knight** (shield and protection), **Mage** (elemental magic), **Archer**
  (ranged physical), **Cleric** (faith healing, support), **Paladin** (holy warrior: armored melee and smites, with
  light healing), **Rogue** (crit, bleed/poison, mobility), **Monk** (fast unarmed martial arts, multi-hit;
  Kristela's base class).
- **Advanced (affinity):** **Holy Knight** (Knight 15 + Cleric 10): tank-healer with faith shields. **Spellblade**
  (Warrior 15 + Mage 10): magic-infused melee. **Ranger** (Archer 15 + Hunter 10): traps, partner synergy.
  **Archmage** (Mage 25 + Academy Teacher 10): big area spells.
- **Inherited:** **Crystal Ice Legion Hunter** (Archer 15 + Mage 10 with an Ice skill picked): Uzuki's class. Ice
  Hunter Bow attacks, summons ice avatars. **Princess Timeless Monk** (*sketch*: Monk 15 + Mage 10 with a Darkness
  (time) skill picked): Kristela's class. Time magic for a speed build: turn advances and flurries.

### Aurelius Empire (1st): dragon and phoenix bloodlines
- **Base:** **Dragonblood Warrior** (fire/earth melee, bloodline passives), **Phoenix Acolyte** (fire healer,
  self-revive), **Wyvern Lancer** (reach-2 spear, leaps over tiles).
- **Advanced (affinity):** **Dragoon** (Wyvern Lancer 15 + Warrior 10): aerial lance strikes.
- **Inherited:** **True Dragon** (Dragonblood Warrior 25 + Wyvern Lancer 10): dragon-transformation ultimate.
  **Phoenix Sovereign** (Phoenix Acolyte 20 + Mage 10): rebirth-flame party revive.

### Steampunk Nation (2nd): magic scholars vs magic engineers
- **Base:** **Mech Engineer** (builds and customizes a robot companion; robots are medium companions with module
  parts), **Arcanist** (magic circles: area sigils and tile traps), **Gunslinger** (ranged physical, reload rhythm).
- **Advanced (affinity):** **Artificer** (Mech Engineer 15 + Blacksmith 10): turrets and in-combat gear buffs.
- **Inherited, magic side** (prerequisites are *sketches*):
  - **Summoner** (Arcanist 15 + Mage 10): summons sigil creatures.
  - **Cosmic Mage** (Mage 15 + Arcanist 10, with a Darkness (cosmic) skill picked): star and gravity magic.
  - **Broken Star Lance** (Arcanist 15 + Wyvern Lancer 10): falling-star lance strikes.
  - **Sun Warrior** (Warrior 15 + Arcanist 10, with a Fire skill picked): radiant melee.
  - **Moon Titan** (Knight 15 + Arcanist 10, with a Darkness skill picked): lunar tank.
- **Inherited, engineering side** (prerequisites are *sketches*):
  - **Mech Giant** (Mech Engineer 20 + Blacksmith 10): the robot grows into a large companion (2 armor pieces +
    1 ring).
  - **Mobile Suit Mechanist** (Mech Engineer 15 + Gunslinger 10): wears the mech as a transformation ultimate.
  - **Circuit Board Master** (Mech Engineer 15 + Arcanist 10): hacks enemies, drones and turrets.
  - **Railgun Sniper** (Gunslinger 20 + Mech Engineer 5): long piercing line shots.

### Dynasty Nation (3rd): Chinese dynasty, with samurai/ninja influence
- **Base:** **Rune Warrior** (elemental runes carved into weapon and armor, melee; Haiden trains it later), **Spirit Monk**
  (ancestor spirits empower allies), **Strategist** (timeline control: speed up allies' turns, delay enemies').
- **Advanced (affinity):** **Onmyoji** (Spirit Monk 15 + Strategist 10): summons spirit familiars.
- **Inherited** (prerequisites are *sketches*):
  - **Runegod Fire Blade** (Rune Warrior 15 + Paladin 10, with a Fire rune picked): Haiden's class. A burning rune
    long sword; a tank who deals real damage, with some healing from fire runes. Not a pure healer.
  - **Samurai** (Warrior 15 + Spirit Monk 10): iaido counters, single heavy strikes.
  - **Ninja** (Rogue 15 + Strategist 10): stealth, poison/bleed, shadow clones.
  - **Sword Singer** (Spirit Monk 15 + Warrior 10): sword dances whose songs empower allies.
  - **Song Fire-Lancer** (Rune Warrior 15 + Gunslinger 10): gunpowder fire lance, reach-2 fire bursts.
  - **Imperial Guard** (Knight 15 + Rune Warrior 10): bodyguard who intercepts hits for an ally.
  - **Shogun** (Samurai 15 + Strategist 10): commands the party, team turn advances.
  - **Kage** (Ninja 20 + Samurai 5): assassination and clones.

### Beastfolk (4th): beast forms of land, air and sea
- **Base:** **Beast Warrior** (land: claws, partial transformation), **Skyborn** (air: dives, mobility), **Tideborn**
  (sea: water magic, healing and control), **Beast Shaman** (beast-spirit magic, totems).
- **Advanced (affinity):** **Pack Alpha** (Beast Warrior 15 + Animal Trainer 10): boosts monster partners.
- **Inherited:** **Primal Beast** (Beast Warrior 25 + Beast Shaman 5): full transformation ultimate with buffed
  skills for a few turns. **Storm Roc** (Skyborn 20 + Beast Shaman 10): air transformation ultimate.

**Elements:** Darkness is the "everything else" magic element. Time, cosmic and space magic all fall under it.

## Skill presentation (later)
- When a hero uses a skill, a **manga/comic panel** of their chibi performing it flies in from the right side of the
  screen, then slides out.
- **Ultimates** get a short **cutscene**.
- Built later, class by class, as each skill and ultimate is designed. For now, skill events only need to carry the
  hero and skill id so a presentation layer can show a panel per skill later.

## Speed protection
- **Pre-gear speed = hero/race base + a class modifier, clamped to 85-100.** The modifier comes from the hero's
  highest-tier class: about -5 to +5 (Ninja, Archer, Skyborn faster; Knight, Holy Knight, heavy classes slower).
- Levels, class and profession tier bumps, and Traces **never** give SPD. Everything above 100 comes from gear
  (GEAR.md speed budget) or temporary combat buffs.

## Traces (per hero, unique)
- **3 major passives**, unlocked at ascension gates (*proposed*: levels 20, 40, 60).
- **About 10 minor stat nodes.** Budget about +10-15% of the hero's main stats in total; a crit hero up to +8% Crit
  Rate. **Never SPD.**
- Nodes cost materials (dungeon and farm), so Traces are a resource sink. Like HSR, Traces are mostly a completion
  path, not a build choice; classes are where choices live.

## Respec (approved)
- **Per class or profession:** a respec unlearns one (up to its 25 tiers).
- **Class masters.** Every class has a master in its kingdom: a guild hall (Medieval), a dojo (Dynasty), a bloodline
  shrine (Aurelius), a workshop academy (Steampunk), an elder's den (Beastfolk). To unlearn a class, the hero
  "returns the teachings" to its master. The cost is **Forgetting Tea**, brewed by a Chef or Alchemist.
- **Relearning is the penalty.** Refunded points don't come back at once: they return as the hero earns EXP again, at
  a **flat EXP cost per point**, so it costs the same at level 30 or level 100. Target: relearning a full 25-tier
  class takes about **1 day** of EXP income. EXP books and EXP shards count, so players can always farm through it.
- **Academy Teacher at home.** A hero at your base with the Academy Teacher profession who has **mastered that class
  (tier 25)** can run its respec at home and **halves the relearning EXP**. Mastering classes on several heroes and
  keeping teachers at home becomes a goal of its own.
- **No penalty while learning:** respecs are free (no relearning) below hero level 20, and the first respec after that
  is free too.
- **Heroes never lose levels.** Hero level stays; only the class's points go back through relearning.
- A class can't drop below an advanced class's prerequisite while that advanced class has points; unlearn the
  advanced class first.
- A premium respec is fine as a **shortcut** (e.g. skip relearning), never the only way. Pay-to-fix-mistakes reads
  badly in reviews.

## Professions (home-base jobs)
- **Professions and classes share the same 100 points.** Any mix is allowed: 4 maxed classes, 5 professions, or
  anything in between. It's the player's choice and the fun of min-maxing a build.
- Heroes not in the dungeon party work at home, filling the base's profession slots: the passive-play loop. **Any
  hero can do any basic home job at tier 0** for basic output; profession tiers raise output and unlock recipes and
  skills. Fighters with 4 classes can still help at home, and benched heroes become natural crafters, so every hero
  you own is useful.
- **Same shape as classes:** tiers 1-25, a small stat bump every tier, a skill pick or upgrade at tiers 5, 10 and 15,
  **an ultimate at tier 20** (a new one, an upgrade or an alternate, exactly like classes: e.g. a Chef's feast that
  heals the party, a Blacksmith's armor-shattering strike), and a major boost at tier 25.
- **Milestone picks can be combat or farm skills** (e.g. a Blacksmith chooses between a sundering strike and a better
  forging rate), so every profession asks "fight or farm?". Combat picks and profession ultimates join the hero's
  pool for the 3 skills + 1 ultimate loadout.
- Profession stat budget at tier 25: the same as a class's, since it costs the same points. **Never SPD.**
- **Advanced professions** work like advanced classes: they need tiers in basic professions. *Examples:* Runesmith
  (Blacksmith 15 + Alchemist 10: adds effects to gear), Master Rancher (Animal Breeder 15 + Animal Trainer 10),
  Royal Chef (Chef 15 + Farmer 10).
- **Inherited professions** exist too: Dynasty has **Silk Road Merchant** and **Imperial Censor**. They sit on the
  back burner with Merchant and Judge.
- Some advanced classes need a profession tier (class list above). Profession respecs work like class respecs.
- **Build the core professions first:** Chef, Blacksmith, Seamstress, Alchemist, Animal Breeder, Farmer, Hunter,
  Animal Trainer, Academy Teacher. **Back burner** until core classes, professions and the damage/gear math are
  settled: Prison Guard, Judge, Governor, Merchant, and the inherited professions.

| Profession | Job |
|---|---|
| **Chef** | Food buffs and heals for dungeon runs |
| **Blacksmith** | Forges metal armor and weapons |
| **Seamstress** | Makes cloth and leather armor |
| **Alchemist** | Potions; transmutes gear (*proposed*: reroll one substat for materials; the tier stays) and makes Tuning Stones |
| **Animal Breeder** | Better breeding odds and shorter egg timers |
| **Farmer** | Crop yield and growth speed |
| **Hunter** | Gathers animal materials on passive expeditions |
| **Animal Trainer** | Trains monster partners (EXP, affinity) |
| **Academy Teacher** | Respecs at home for classes they've mastered (half the relearning EXP); teaches skills; *proposed*: writes EXP books |
| **Prison Guard** | *Back burner.* Capture human-type dungeon enemies (bandits and the like) to work the farm as laborers, fight as minions, or sell for income. Another way to get followers |
| **Governor** | *Back burner.* Leads the farm: farm-wide buffs, more worker slots |
| **Judge** | *Back burner.* To define |
| **Merchant** | *Back burner.* Trades items. Visiting other players' trading posts needs an online backend (server-checked trades, no duplication), so start with NPC caravans offline |

## Open questions
1. Long Sword and Gauntlets passives: later. They're stats-only weapons for the first playtest.

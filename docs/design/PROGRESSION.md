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
  - **Uzuki** (the first companion): 5th kingdom (Medieval Realm). Signature class **Crystal Ice Legion Hunter**: an
    Ice Hunter Bow user who summons ice avatars to fight alongside.
  - **Haiden** (boy): 3rd kingdom (Dynasty Nation). Fire **Rune Warrior**; signature class **Runegod Fire Blade**.
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
| Haiden | Rune Warrior | **Tank DPS with some healing** (frontliner; fire runes that hit, guard and mend) | Blacksmith (equipment) | 90 |
| Kristela | Monk | **Melee DPS** (speed build) | Chef (food buffs and heals) | 100 |

Together they cover tank (with some healing), melee DPS and utility DPS, so the player's own character can fill
whatever role they like.

**Weapons:** Uzuki uses a **Hunter Bow**, Haiden a **Long Sword**, Kristela **Gauntlets** (see GEAR.md).

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
- Turn-manipulation effects (Strategist, Shogun, Windrider's Cloak) need their own budget like speed. *Proposed*: no
  single effect moves a turn by more than 30%, and an actor can't be advanced more than once per enemy turn.
- Transformation ultimates last a set number of the hero's own turns, so the timeline shows when they end.

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
- **Base:** **Rune Warrior** (Haiden's class: elemental runes carved into weapon and armor, melee), **Spirit Monk**
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

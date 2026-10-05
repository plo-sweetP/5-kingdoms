# 5 Kingdoms: Game Plan (v0.3)

## Decisions locked in
| Topic | Decision |
|---|---|
| Engine | Unity 6.3 LTS (6000.3.25f1), 2D with URP, C# |
| Platforms | iOS + Android first, one codebase. Developed on Windows; Android is the test platform until there's a Mac or build service. **PC on Steam (incl. Steam Deck) planned later** |
| Orientation | **Landscape only**, on phones and tablets (test devices: Samsung S22 Ultra, Galaxy Tab S8+) |
| Monetization | Phase 1: free-to-play where **breeding is the gacha** for monsters. Later: a paid gacha for **characters and weapons** |
| Art | **Pixel art from the Tiny Swords pack** by Pixel Frog (decided 2026-10-03, [docs/design/ART.md](docs/design/ART.md)): 64 px = 1 tile, chunky chibi units with frame animations. Heroes are built from layers so equipment shows on the sprite; missing pieces are made by code in the pack's style. It replaces the 32 px placeholders generated in code |
| Controls | 8-way grid movement. On-screen D-pad + buttons first, tap-to-move later. **Attacks are deliberate:** choose the attack, then its target, or tap an enemy in reach; walking into an enemy only turns to face it |
| Team | Peter (CS degree, C++/C#/Java/Python) directs, reviews and playtests. Claude writes most code |

## Pitch
Farm by day, delve by night. You run a farm in a world of five kingdoms, raising and breeding monsters. You send them into turn-based procedural dungeons for the materials, eggs, seeds and gear that make the farm and your party stronger.

## Core loop
```
FARM: plant / harvest / care for monsters / breed -> eggs / craft gear
  | party + food + gear                    ^ materials, rare seeds, eggs, gear
  v                                        |
DUNGEON: grid-based, turn-based roguelike floors
```
The two halves feed each other:
- **Farm to dungeon:** crops become food and buffs, monsters join the party, the farm crafts gear.
- **Dungeon to farm:** loot gives rare seeds, breeding ingredients, crafting materials and equipment.

## The 5 Kingdoms (five cultures)
Each kingdom has its own monster family, crops, dungeon biome, magic flavor and cross-breeding rules. Cross-kingdom breeding is the main collecting hook. Unlocking kingdoms is the progression spine. (Working names; final names TBD.)

| # | Kingdom | Culture and theme | Monster / magic flavor | Dungeon biome ideas |
|---|---|---|---|---|
| 1 | **Aurelius Empire** ("the golden empire") | Descendants of dragons and phoenixes. Their people carry diluted mythical blood and varied mythological abilities | Dragons, phoenixes, mythical creatures. Inherited bloodline abilities | Golden temples, volcanic aeries, ruined palaces |
| 2 | **Steampunk nation** | Magic meets technology. Magic scholars and magic engineers compete | Robots/constructs vs. large magic circles (golems, automatons, summoned sigil creatures) | Clockwork factories, sky docks, arcane labs |
| 3 | **Dynasty nation** (Three-Kingdoms-era China) | Walled off from the continent by a great wall. Isolation split its tech and magic from the rest of the world | Spirit and ancestor magic: spirits empower the user or monster | Mountain temples, spirit-haunted tombs, bamboo forests, wall fortresses |
| 4 | **Beastfolk** | Humans embraced the beast and took beast form | Transformations, beast-type magic, land / air / sea sub-types | Jungle, savanna, cliffs and sky, reefs and coast |
| 5 | **Medieval realm** | Knights, nobility, belief in higher deities, full magic system. Noble families to be introduced later | Faith magic. Hero / princess / king-style classes | Castles, cathedrals, catacombs, battlefields |

Design notes:
- **Distinct mechanical identity per kingdom** so breeding across them is interesting: Aurelius = bloodline traits; Steampunk = construct/module parts; Dynasty = spirit bonds (equip a spirit); Beastfolk = form-switching; Medieval = classes and faith blessings.
- The Dynasty nation's wall is a natural gate: it can be the last or a late-unlocked kingdom.
- Noble families (Medieval) are a later expansion hook, so leave a "family/lineage" field in monster data.

## Element system
- **Base elements (5):** Fire, Water, Wind, Earth, Darkness.
- **Advanced / variant elements** grow out of base ones: Lightning (from Fire), Ice (from Water), Metal (from Earth). Wind's advanced form is TBD.
- **Darkness is the "everything else" magic element** (decided 2026-10-03): it covers time, cosmic and space magic, so there's no separate Space/Time element.
- Implementation: a data-driven element chart (ScriptableObject): each element has a parent, strengths, weaknesses. An advanced element inherits its parent's matchups plus its own tweaks. New elements can be added later without code changes.
- Breeding hook: advanced elements are unlocked by specific parent pairs and ingredients, so they act as the "rare" tier.
- Open: Is there a Light element? Darkness is in the base set without an opposite. Decide before building the type chart.

## Heroes, skills and equipment (planned)
Full specs: [docs/design/PROGRESSION.md](docs/design/PROGRESSION.md) (levels, classes, professions, Traces, respec,
energy) and [docs/design/GEAR.md](docs/design/GEAR.md) (damage formula, stats, gear, sets, weapons).
- **Player character (later):** the player creates their own character and picks one of the 5 kingdoms as their
  origin, which opens that kingdom's inherited classes. Until the creator exists, Haiden leads the playtest party
  (a melee hero in front; the player can switch to anyone).
- **Uzuki is the first companion** (a boy, Medieval Realm; concept sketch: spiky blue hair, cyan eyes, sleeveless top with
  strap, one pauldron, baggy cuffed pants, boots). Placeholder sprite exists.
- **Starting party for the first playtest** (the player's own character joins later as the 4th):

  | Hero | Kingdom | Class | Role | Profession | Base SPD |
  |---|---|---|---|---|---|
  | Uzuki | Medieval Realm | Archer (later specs into Ice Mage) | Utility DPS: ranged, slows, control and support shots | Alchemist (team potions) | 95 |
  | Haiden (boy) | Dynasty Nation | Paladin (later Rune Warrior, toward Runegod Fire Blade) | Tank first, with some healing | Blacksmith (equipment) | 90 |
  | Kristela (girl, a princess) | Medieval Realm | Fencer (Peter, 2026-10-05; a Monk until then, and the Monk stays as a class) | Melee DPS, speed build | Chef (meals that buff the party between fights) | 100 |

  Weapons: Uzuki a **Hunter Bow**, Haiden a **Long Sword**, Kristela a **Piercer Blade** (Gauntlets while she was a Monk; stats only for the first playtest,
  except the Hunter Bow's Multishot).
  Concept sketches: [Haiden](docs/concept/haiden.jpg) (spiky orange-brown hair, blue headband with long tails, red
  eyes), [Kristela](docs/concept/kristela.jpg) (long wavy blonde hair, blue eyes, gold X-shaped hair clip on one side).
- **Persistent progression:** characters keep their level and gear between dungeon runs (unlike Mystery Dungeon's resets). They level up and gear up outside, at the farm, then enter dungeons to clear the stages and the boss. Defeating monsters and bosses inside a dungeon also gives EXP that carries back out.
- **Skills:** each character gets **3 skills + 1 ultimate** from their own starting kit, upgraded, extended or replaced
  by class and profession milestones. **No mana (decided):** a skill sits out the hero's next turn after use, every
  hero has an always-ready weapon attack, and ultimates fill a charge meter as the hero acts, hits and gets hit
  (PROGRESSION.md, "Skill resources"). Ultimates (buffs, debuffs, damage, summons, ...) grow with the classes.
- **Classes and professions** share one point per level (100 at level 100), spent freely on class or profession
  tiers 1-25 (PROGRESSION.md).
- **Equipment slots:** 4 armor pieces (head, body, hands, feet) + 2 rings + 1 weapon for heroes; monsters wear 1 ring,
  2 rings, or 2 armor pieces + 1 ring by size. Armor and rings form 2- and 4-piece sets (GEAR.md).
- **Equipment sources:** unappraised boxes from dungeons (they survive defeat), crafting on the farm, and weapons also
  from the gacha.
- Data rule: gear, sets, classes and drop tables live in data, like everything else.

## Gacha
- **Monsters come from breeding** (the free, earned "gacha").
- **Paid gacha pools: characters + weapons** (later). Needs server-side rolls, pity, and published odds (app store and regional rules) before it ships.

## Combat timeline (action value)
Turn order in fights works like Honkai: Star Rail, on Mystery Dungeon grids
(reference: https://game8.co/games/Honkai-Star-Rail/archives/438178).
- **Speed** (integer, never randomized; hero 100, species roughly 80-130). One turn lasts 10000 / Speed AV. Whoever's next
  turn comes soonest acts; ties go to the leader, then the lower actor id.
- **Cycles:** the first cycle is 150 AV, every later one 100 AV; a turn exactly on a boundary counts in the cycle ending
  there. Breakpoints are exact (Speed 134 gets 2 turns in the first cycle, 133 doesn't; Speed 120's third turn is exactly 250).
- **Exact math:** time is a reduced fraction (`AvTime`), never a float, so seeded runs replay identically.
- **Exploring vs. fighting:** exploring keeps the simple rhythm (everyone else takes one turn per hero action; the party moves
  at the leader's pace). When an enemy notices the hero, combat starts and the timeline resets (cycle 0 = 150 AV);
  it ends when no enemy is alerted. Every step still costs 10000 / Speed AV on the run's clock.
- **AV clock:** natural regeneration (every 600 AV) and reinforcements (every 4000 AV on a floor) run on AV time, so a
  faster hero gets more done before the floor reacts.
- **Action costs:** basic actions cost one full turn; each skill has its own cost (Spirit Strike 125%, Dash 50%), so a heavy
  skill pushes the user's next turn back and a quick one brings it closer.
- **Joining mid-fight:** summoned helpers first wait a full turn; reinforcements that walk in between rounds act in the
  coming round.
- **Speed changes** (buffs, later) keep the distance an actor has left and recompute when it acts; the gauge isn't reset.
- **HUD:** a turn-order strip (next 5 turns of the fight, cycle dividers, "+AV" until each, SLAM on a winding-up boss's
  next turn), shown only in combat. Walk animations are slightly faster or slower with Speed.
- **Content:** Spiders 100 (same as the hero), the Troll 85 (slow and heavy: about every 6th turn the hero gets two in a row). They were the slimes and the King Slime before art pass 1.
- **Regression anchor:** with all speeds equal, the timeline reproduces the original alternating turns exactly (golden
  replay test; balance report identical before and after).
- Every party member acts on the timeline at their own speed (1f). **Later:** Break/toughness with elements (1j),
  speed buffs and debuffs through skills (turn effects get their own budget, PROGRESSION.md).

## Party (planned)
- **Up to 4 characters on screen**: a leader plus 3 partners, as in Mystery Dungeon. The size is one constant (`MaxPartySize`), so trying 3 is a one-line change if 4 feels crowded on a phone.
- **First playtest (1f): a party of 3**, Haiden, Kristela and Uzuki (see Heroes above), with Haiden leading. The
  player's own character joins later as the 4th.
- The player controls the leader; partners act by AI with simple tactics (follow me / go after enemies / hold back), and the player can switch which character they control. Each character keeps their own 3 skills + ultimate.
- Turn order: exploring, the leader acts, then the partners, then enemies. In fights every party member acts on the AV
  timeline at their own speed. In corridors partners follow in a line and swap places with the leader when bumped.
- **Ranged vs melee (decided, PROGRESSION.md):** in a fight melee partners close in on a foe (swapping past a ranged
  ally when that's shorter, never straight back), ranged ones hang back at a tile they can shoot from (any foe in
  sight within 5 tiles), and a ranged hero with a foe next to it steps out of melee once, then shoots anyway. A badly
  hurt hero (under 30% HP) may swap back behind a healthier ally, two melee heroes included ("run to safety").
- **Staying together (Peter's playtest note, 2026-10-03):** partners fight within 6 steps' walk of the leader. With
  allies in their way (a corridor, a doorway) they walk around them only when that's at most 4 steps longer;
  otherwise they close up and wait right behind them, ready to take a place at the front, and Uzuki stays behind the
  line when it hides his target. A partner that got cut off swaps past the one that follows it in line ("regroup").
  On Auto, the leader doesn't walk off to the stairs while its partners fight.
- **Doorways and corridors (Peter's playtest note, built 2026-10-04):** the hero in front holds a doorway against
  more than two foes close beyond it (within their sight range: 4 tiles into the room) instead of stepping out among
  them: only the tile straight ahead reaches him there, and Uzuki shoots past him. Against one or two the party goes
  in, if he has at least 90% of his HP and a melee hero to bring along; in the corridor's mouth he then steps aside so
  she can come out and fight beside him. Where one hero holds the way, he trades places under half HP with the melee
  hero behind who has clearly more HP left: the fresh one fights, the hurt one heals behind. Partners do all of this
  for themselves and never move the hero the player controls; the autopilot's leader does it too.
- **Healing between fights (Peter's playtest note, built 2026-10-04):** outside a fight, partners and the
  autopilot's leader use their heals to top the party up: a heal is used whenever at least half of it goes to use
  (Haiden ends above 90% of his HP, Kristela near 88%). A hurt hero with no heal of its own (Uzuki) walks over to
  the one who can heal it. On Auto the leader waits for this before moving on, for at most 6 turns in a row without
  a heal landing; by hand the player decides when to move on, and the partners heal as they follow.
- **A line when a hero waits (Peter's playtest note, built 2026-10-04):** when a hero's AI holds a doorway, when
  the front rotates or a badly hurt hero ducks behind another, and when the Auto leader rests, a word shows over
  the hero ("Holding the door", "My turn!", "Resting") and the log says why, e.g. `Haiden holds the doorway: "One
  at a time, please!"`, so nobody thinks the hero is stuck.
- Defeat (decided 2026-10-03): the run ends when the whole party has fallen; when the leader falls, the next hero
  in line takes the lead. Resurrection may come later, as an ultimate, a weapon ability or a craftable item.
- Enemies and bosses can target any party member; area attacks hit everyone in range.
- Each character has their own saved progress (level, EXP, gear).
- Open: are partners only gacha characters, or can bred monsters fill party slots too? (Earlier plan: farm monsters join the party.)

## PC and Steam (planned)
- Same Unity project, built for Windows (Mac/Linux optional). The game already runs as a Windows build.
- **Controller support** (needed for Steam Deck): every input becomes a `HeroCommand`, so gamepads map onto the same commands as touch and keyboard. On PC the touch D-pad and buttons hide when a controller or keyboard is in use, with button prompts instead.
- Steam Deck's 1280x800 screen is 16:10, the same shape as the tablet layout; the pixel camera's whole-number zoom keeps it sharp.
- **Steamworks:** achievements, cloud saves, overlay (e.g. Steamworks.NET).
- **Purchases:** the Steam version's paid gacha must use Steam's payment system, separate from Google Play / App Store IAP. Same odds-disclosure and regional loot-box rules.
- **Cross-save** between phone and PC needs accounts and a server (also needed for the paid gacha).
- Business: Steamworks account, $100 Steam Direct fee per game, Valve's 30% share, store page with capsule art and trailer. Steam Playtest can double as a beta channel.
- Code rule from now on: platform services (payments, achievements, cloud saves) sit behind interfaces, so each store plugs in its own.

## Autopilot (planned)
- Auto-play for **smaller daily runs/missions**, and eventually for the **main quest**.
- Foundation exists: the Core `AutoPilot` already plays whole dungeon runs (used today for automated tests and balance reports). The in-game feature will need smarter tactics, player-set rules (e.g. "heal below 40%"), and rewards/limits so it doesn't replace playing.

## Energy (the daily limit, decided)
From PROGRESSION.md; answers the old open question on stamina and timers.
- **Energy** refills fully every 24 hours at the daily reset. Unspent energy carries over, capped at 2 days' worth.
- **Costs energy:** gear-farming dungeons, special bosses and EXP-material dungeons (so the "about a week of daily play
  to take a hero from 1 to 100" target holds).
- **Free:** story progression, regular dungeons and their bosses, and the farm. Players are never locked out of playing.
- **Weekly bosses:** a few clears a week, rewarding summoning crystals (the pull currency) and resources.
- **Refills:** items from the battle pass and events; premium refills are limited per day (store and legal checks apply).

## Part 1: Farm and breeding
- Tile-based farm (Unity Tilemap), crops with growth stages, seasons. The farm costs no energy.
- Real-time timers for crops and eggs, with offline progress calculated on return.
- Monsters on the farm: hunger, mood and affinity. They can do jobs (water, harvest).
- **Breeding:** 2 parents + ingredient -> egg (timer) -> hatch. Inputs: parents' kingdom, traits and rarity. Outputs: species, rarity tier, mutation chance, inherited traits, and a pity counter for rare results.
- **Crafting:** farm workshops turn dungeon materials into equipment.
- Design rule: all odds live in data tables, not code, so we can tune without rewriting.

## Part 2: Dungeon crawl (Mystery Dungeon style)
- Grid movement in 8 directions. Every action is a turn, then all enemies act. Diagonal moves and attacks can't cut wall corners.
- Shots reach any foe in sight within 5 tiles, at any angle: walls and wall corners block them, allies and other actors don't.
- Procedural floors: rooms and corridors, stairs down, items; later traps and monster houses.
- **Stages and a boss:** each dungeon is a set of floors (stages) ending in a boss fight.
- **Levels carry in and out:** the party enters at its farm-earned level and gear; EXP earned inside is kept.
- **Berries** heal the leader 300 HP (`BerryHealHp`); in 1d they restored mana, which no longer exists.
- **Bigger fights:** monsters come in packs of 2-4, and half the packs bring a fast bat, so no hero clears a room alone.
- A party of up to 4 heroes (3 in the first playtest); monster partners later. Skills, items, gear.
- Run ends by reaching the bottom or being defeated. Defeat has a soft penalty, with no permadeath of monsters in v1.
- The generator is seeded so runs are reproducible, which helps testing.
- **Built in Milestone 1:** see "Progress" below.

## Technical architecture
- **Separate rules from presentation:** game rules are plain C# in `Assets/Scripts/Core` (no Unity references) and emit events; Unity code in `Assets/Scripts` only draws and animates those events. Rules are unit tested in seconds.
- **Data-driven:** monsters, skills, items, gear, crops and dungeons move to data assets as they grow past a handful.
- **Save system:** versioned JSON, local first. Plan for a migration path from day 1.
- **Offline-first.** A backend (accounts, cloud save, server-side gacha rolls) is added only when paid pulls are introduced.
- **Testing:** NUnit tests for the rules (run in Unity, or in seconds with `dotnet`), an autopilot soak test over many seeds, a balance report, and an unattended autoplay build that takes screenshots.
- **Build constraint:** Android builds work on Windows. **iOS builds need a Mac** (or a cloud build service such as Unity Build Automation, or a rented Mac) plus an Apple Developer account ($99/yr).

## Roadmap (each milestone is playable)
| # | Milestone | Status |
|---|---|---|
| 0 | Project setup: Unity project, folders, git repo | **Done** |
| 1 | Dungeon prototype: hero on generated floors, enemies, combat, stairs, items | **Done** (first playtest: "looks great") |
| 1b | Dungeon follow-ups: hero level/EXP kept between runs (saved), a boss on the last floor, controller support, Auto button | **Done** |
| 1c | Combat timeline: Honkai Star Rail-style action value (AV) turn order on the grid, Speed stat, turn-order strip | **Done** |
| 1d | Mana (berries restore it), first skills with AV costs, healing (mana was removed again in 1f) | **Done** |
| 1e | Combat math (GEAR.md step 1-3): multiplicative damage formula, 10x HP/ATK/DEF rescale, Crit Rate/Crit DMG stats (5%/50%), per-actor stat sheet, skill tags; difficulty re-tuned to match 1d | **Done** |
| 1f | Party of 3 (**first playtest checkpoint**): Uzuki (Archer, Hunter Bow), Haiden (Paladin, Long Sword), Kristela (Monk, Gauntlets) with the approved kits and ultimates; no mana; AI partners with follow/attack/hold tactics and battle formation, switching control, corridor follow and swap, everyone on the AV timeline; traps, statuses, packs; shots at anything in sight, deliberate two-step attacks, stuns as timeline delays | **Done** (parts 1 and 2, and Peter's answers at the first playtest checkpoint) |
| A1 | Art pass 1 ([docs/design/ART.md](docs/design/ART.md)): the current game on the Tiny Swords art (heroes built from layers with their own faces and hair under the helmets, equipment looks for GEAR.md's weapons and armor sets, ring auras, skill animations, monsters from the Enemy Pack, the outdoor dungeon, the HUD) | **Done** (2026-10-04; new bosses and creatures come with row 7) |
| C1 | Corridor tactics for the party's AI (PROGRESSION.md, "Doorways and corridors"): hold the door, rotate the front, enter a room when it's safe or there's room; then a balance re-check | **Done** (2026-10-04; the Troll went from 16000 to 24000 HP) |
| 1g-1 | Classes core, part 1 (PROGRESSION.md, "Building 1g"): three small follow-ups from the playtest (heroes heal between fights, a note when a hero waits at a door, the test sandbox moved to an ordinary folder); points and tiers 1-25, stat bumps, milestones with three options, per-hero kits and loadout, save migration, free respec; the skill-tree screen of spheres; Kristela's Fencer kit (she was a Monk until 2026-10-05); tiers 5 and 10 of Archer, Paladin and Fencer | **In progress.** Done and on main (2026-10-05): the three follow-ups and the classes core (rules, the hero's own kit and loadout, save version 2, free respec), with no change in play. **Left:** Kristela's Fencer kit, the skill-tree screen, tiers 5 and 10, the balance with default builds ("Progress" below says where each stands) |
| 1g-2 | Classes core, part 2: tiers 15, 20 and 25 of the three classes (alternate ultimates, masteries), relearning after a respec from level 20, prerequisites and kingdom locks | |
| P1 | Playtest pass 1 ([docs/design/HUD.md](docs/design/HUD.md)), Peter's notes from the tablet: the top-right buttons in one row, a minimap with fog, the skills in a row along the bottom, a pause menu (with Reset level for testing), Auto hides the D-pad and greys the buttons out, holy light on Divine Strike, footing in the boss fight for the party's AI | After 1g-1: both change the HUD (listed here, a row down, so the 1g-1 row can be edited without a conflict) |
| 1g-3 | Professions: potions and meals as items, the Alchemist, Blacksmith and Chef paths (farm paths locked until the farm exists) | |
| 1h | Gear (GEAR.md steps 4-10): items, rarity, item level, upgrades, Tuning Stones, first sets, weapons, unappraised boxes, salvage, Blacksmith crafting, monster slots, `-gear` report with the speed and crit budget tests | |
| 1i | Hero screen between runs: equipment, class and profession tiers, loadout (until the farm exists) | |
| 1j | Elements and Break/toughness (needs the element chart) | Later |
| 2 | Farm prototype: plant, grow, harvest, inventory, day/season clock | |
| 3 | Monster and breeding core: stats, traits, egg, hatch, rarity and pity, with tests on the odds | |
| 4 | Connect the loops: farm monsters enter dungeons, loot flows back, saves work | |
| 5 | Vertical slice: one full kingdom with tutorial and basic UI | |
| 6 | Content expansion: kingdoms 2-5, monster roster, skills, equipment, balancing | |
| 7 | Art and audio pass: art beyond the pack (new bosses and creatures, more heroes, biomes), audio | |
| 8 | Monetization and live ops: shortcuts, gacha (characters + weapons), analytics, beta | |
| 9 | Release: TestFlight and Play closed testing, store listings, launch | |
| 10 | PC and Steam: controller polish, Steamworks, Steam payments, store page, Steam Deck check | Later |

## Progress
**Milestone 1 (dungeon prototype)** contains:
- Seeded rooms-and-corridors floors (56x32), 5 floors in the "Slime Cave", stairs to descend, clear on the last floor.
- Uzuki vs. slimes: bump-to-attack and an attack button, damage spread, critical hits, EXP and level-ups, slimes that notice, chase and bite, reinforcements over time.
- Berries to pick up and eat (heal), slow HP regeneration.
- Landscape touch HUD: 8-way D-pad, attack button, locked skill/ultimate slots, Wait and Berry buttons, Descend button on stairs, message log, floating damage numbers, floor banner, end-of-run panel. Keyboard controls in the editor.
- Juice: step hops, wind-up and lunge, slash swipe, white hit flash, knockback, hit-stop, screen shake on crits, death bursts, sparkles.

**Milestone 1b** adds:
- Persistent progression: Uzuki's level and EXP are saved (local JSON, versioned, crash-safe writes) and carried into every run; EXP earned inside is kept win or lose. EXP bar in the HUD; the end panel shows levels gained.
- Boss floor: B5F is an antechamber plus an arena with two pillars. The King Slime (110 HP) winds up a slam that hits every adjacent tile next turn (red warning tiles; step away to dodge), calls two slimes at half HP, and clearing the dungeon means defeating it. Boss HP bar in the HUD.
- Balance with progression: a fresh level-1 hero almost never beats the King Slime (3/200 autopilot runs), but with levels kept, every simulated player beats it, on average on the 2nd-3rd attempt at about Lv 9.
- Controller support (left stick/D-pad, A attack, Y wait, X berry, RB stairs); touch controls hide while a keyboard or controller is used and button hints appear instead.
- Debug launch flags for testing (`-fk-floors`, `-fk-level`, `-fk-save`), and a 5 Kingdoms > Debug menu to reset or show the save.
- Auto button (also T / View): the hero plays itself with the AutoPilot. It never switches itself off: only the player
  turns it off. While it plays, the player can't move or take normal actions (the D-pad and action buttons dim); only
  skills (and later the ultimate) stay usable by hand.

**Milestone 1c (combat timeline)**: see "Combat timeline (action value)" above. Balance with the slower King Slime:
7/200 fresh level-1 autopilot runs win (was 3); with levels kept, players beat the boss on attempt 2.5 at Lv 9.3 (was 2.6 at 9.4).

**Milestone 1d (mana and skills)**, kept as a record (1f removed mana):
- **Mana:** Uzuki has 30 MP (+2 per level), full at the start of each run. Berries restore 15 MP; a basic hit that
  connects gives +2 MP. No natural MP regeneration.
- **Uzuki's skills**, one of each kind:
  - *Spirit Strike* (builds mana): 120% damage on an adjacent enemy, +8 MP, takes 125% of a turn.
  - *Second Wind* (spends mana): heals 50% of max HP for 15 MP (one berry's worth).
  - *Dash* (no mana, unique effect): up to 3 tiles in a straight line in half a turn, then 4 turns to recharge.
- **Aiming:** strikes and dashes go the way Uzuki faces; hold a direction while pressing the skill to aim it.
- **HUD:** an MP bar; the three skill buttons show what they cost or build, count down while recharging, and dim when
  they can't be used (pressing one says why). With a keyboard or controller they line up in a row with their keys
  (1/2/3, LB/LT/RT). The ultimate stays locked.
- **Juice:** the skill's name pops up over Uzuki, Spirit Strike cuts with blue light, Dash leaves afterimages, and
  mana gains float up as "+8 MP".
- **Auto-pilot:** heals with Second Wind below 40% HP, eats a berry between fights if a heal isn't affordable, uses
  Spirit Strike when short of mana for a heal, and dashes down long straight stretches toward the stairs. The player
  can fire skills by hand while it plays.
- **Balance:** with levels kept, players beat the King Slime on attempt 2.5 at Lv 9.3, the same as 1c. More fresh runs
  now reach the boss floor (120/200, was 62); the boss is the wall (4/200 fresh wins, was 7).
- **Layouts checked** on phone (19.5:9) and tablet/Steam Deck (16:10) shapes, for touch, keyboard and controller
  (new `-fk-input keyboard|gamepad` launch flag); with keys or a controller the message log moves to the bottom-left.

**Milestone 1e (combat math)**, GEAR.md "Building it" steps 1-3:
- **Damage formula:** (skill% x ATK + extra damage) x (1 + DMG bonus) x crit x DEF mult x RES mult, where
  DEF mult = K / (K + DEF) and K = 10 x (attacker level + 20). A basic attack is 200% ATK. Everything is integer math,
  so a seeded run replays the same on every platform. The old 85-100% random spread stays for now (open question 9).
- **10x stats:** Uzuki 400 HP / 60 ATK / 30 DEF, +50 / +10 / +10 per level; regeneration 10 HP per tick.
- **Crit** is a per-actor stat: 5% Crit Rate for +50% Crit DMG, for everyone (was 8% for +50%). Crit Rate caps at 100%.
- **Stat sheet:** (base + level growth + weapon) x (1 + % bonuses) + flat bonuses. Only HP, ATK and DEF take %
  bonuses; SPD and the percentage stats (crit, Affinity, Resist) only add up. Weapons count as base (stats only so far).
- **Skill tags:** physical or magic, melee, ranged or area, and an element. Skill power is in percent of ATK
  (Spirit Strike 240%, the same as its old 120% of a basic attack).
- **Monster levels:** monsters spawn at the floor's level (a B3F slime is level 3); per-level growth replaces the old
  per-floor bonuses, and the King Slime has none.
- **Re-tune:** with DEF as a share instead of a subtraction, weak hits no longer bounce off a leveled hero, so slimes
  hit for ATK 40 (+6 per floor) and the King Slime has 1300 HP / 80 ATK. Balance: 8/200 fresh level-1 runs win (4%),
  123/200 reach the boss floor; with levels kept, players win on attempt 2.7 at Lv 9.5 (1d: 2.5 at Lv 9.3).
- The golden replay fingerprint was re-recorded, since every hit changed on purpose.

**Milestone 1f, part 1 (party of 3)**, kept as a record: part 2 below replaced its ranged numbers, the 8-line
aiming, attacking by walking into an enemy, the skipped-turn stun, the swap rule and the balance:
- **Party:** Uzuki (Archer, Hunter Bow), Haiden (Paladin, Long Sword) and Kristela (Monk, Gauntlets) with the approved
  starting kits (PROGRESSION.md). The player leads one hero (tap a party card, Tab or B to switch: free while
  exploring; mid-fight the old leader's turn is played by its AI); partners play by AI with Attack, Follow or Hold
  (tap the badge, or G / L3 for all). Every hero standing gets the EXP in full. The run ends when the whole party has
  fallen, and the next hero in line takes the lead when the leader falls (open question 7).
- **No mana (decided):** every skill sits out the hero's next turn; each hero has an always-ready weapon attack (Quick
  Shot, Sword Slash, Jab); Quick skills (Hunter's Mark, Ki Heal) take half a turn and show a small "Quick" tag.
- **Ultimates:** a gold charge meter on each party card fills by 20 per attack or skill, 10 per hit landed and 20 per
  hit taken (the ultimate's own hits don't count) and carries over between fights within a run. Volley: 2 hits of
  200% on every foe in a 3x3 area at range. Aura of Protection: for 3 of Haiden's turns, allies next to him take 30%
  less damage and heal 10% of his max HP at the start of each of his turns. Flurry of Blows: 5 hits of 80%, moving on
  if the target falls, and her next turn comes 30% sooner. With the autopilot that's about 2 ultimates per hero in the
  boss fight and one every 2-3 normal fights.
- **Ranged vs melee:** ranged reach 5 tiles; ranged hits deal 75% of a melee hit, 52% with a foe next to the shooter
  (point-blank). Formation, engaging and the swap rules are under "Party" above; the soak tests fail if two heroes swap
  back and forth.
- **Kit mechanics:** Rolling Shot leaves a snare that roots the first enemy stepping on it (at most 3 per floor);
  Power Shot knocks back; Hunter's Mark (+25% damage from Uzuki) jumps on a kill; Shoulder Bash shoves and taunts (+50%
  against a wall); Piercing Punch hits the enemy behind; Stun Strike stuns (60%, Affinity vs Resist). Bosses can't be
  rooted or stunned: they lose 30% of a turn instead. Statuses show as icons over heads; the aura glows over its 3x3.
- **Aiming:** choosing an attack, skill or ultimate that needs a target highlights its reach and marks every valid
  target along the 8 lines (Volley shows its area); tap a target, or press the action again for the marked one.
  Holding a direction while pressing fires that way at once, and walking into an adjacent enemy still attacks.
- **Monsters:** packs of 2-4; half bring a bat (Speed 130, frail). Slimes give 3 EXP (+1 a floor); the King Slime has
  10000 HP / 260 ATK. Berries heal 300 HP again.
- **Balance** (autopilot party of 3, 200 seeds): 11 fresh level-1 runs win (5.5%), 185 reach the boss floor; with
  levels kept, the first clear comes on attempt 3.1 at Lv 9.5. A Lv 9 party sent straight to the boss always wins.
- **Placeholder art:** Haiden, Kristela, the bat, the snare, status icons, target reticles, arrows and a punch impact
  for Kristela's gauntlets. Weapons on the hero sprites come with final art.
- **Playtest flags:** `-fk-leader haiden` or `-fk-leader kristela` puts that hero in the lead.

**Milestone 1f, part 2 (targeting and input; the first playtest checkpoint)**, Peter's decisions from PROGRESSION.md
("Ranged vs melee", "Targeting and input", "Delays / stuns"):
- **Shots at anything in sight:** ranged weapon attacks, Shot skills, Hunter's Mark and Volley's center can target any
  foe within 5 tiles (a diagonal counts as one) that's in sight, not only along the 8 lines. Walls and wall corners
  block a shot; allies and other actors don't; if A can shoot B, B can shoot A. Arrows fly at any angle, and a
  knockback goes straight away from the shooter (the nearest of the 8 ways). Ranged hits deal **90%** of a melee hit
  (63% at point-blank).
- **Deliberate attacks:** walking into an enemy only turns the hero to face it and keeps the turn, like bumping a
  wall. The weapon attack works like a skill: press it (the red button, Space, A), its reach lights up with every
  valid target marked, then tap the target, or press again for the marked one (a lone target is marked to begin
  with). Tapping an enemy in reach when not aiming attacks it directly. With nothing in reach the attack says so
  instead of swinging at the air. Holding a direction toward a target while pressing still fires at once; while
  aiming, pressing a direction again moves the mark to the next target out that way. Commands carry the target's
  tile (`HeroCommand.AttackAt / SkillAt / UltimateAt`).
- **Stun is a delay:** Stun Strike (60%, Affinity vs Resist) pushes the target's next turn back 50% of a turn (a
  boss's 25%) instead of skipping it. One budget covers every delay (a snare under a boss costs it 25% too): at most
  50% per effect, 25% on a boss, and **at most once per the target's own turn**, so nothing is stun-locked. A stun
  icon shows while a turn is pushed back. The Stunned status and skipped turns are gone. Fixed on the way: a delay a
  boss picked up during its own turn (walking onto a snare) used to be lost when the turn ended.
- **Run to safety:** a badly hurt hero (under 30% HP) with a foe next to it swaps back behind a healthier ally
  standing farther from the foes, two melee heroes included. Partners and the autopilot both do it when they can't
  heal. The pair cooldown and the soak check for back-and-forth swaps still apply.
- **AI targets:** partners and the autopilot attack with explicit commands naming the target's tile, never by walking
  into a foe, and pick the marked enemy first, then the lowest HP (then the nearest). Uzuki marks the foe he's about
  to shoot; Volley centers where it catches the most.
- **Melee leader:** the party starts as Haiden, Kristela, Uzuki (`ActorCatalog.StartingParty`), the tank in front.
  A tap on a party card (or Tab / B) switches hero; `-fk-leader kristela` or `uzuki` starts with that one.
- **Re-tune at the checkpoint:** the party hits harder now (Uzuki shoots nearly every turn, at 90%), so the King
  Slime went from 10000 to 14000 HP and an action's charge from 20 to 15: 7% of fresh level-1 runs won, the first
  clear came on attempt 3.1 at Lv 9.6, with 2.2 ultimates per hero in the boss fight. The numbers after Peter's
  answers are below.
- **Checks:** 188 Core tests (new: line of sight, targeted commands, the delay rule, run to safety, target choice,
  what aiming marks) and 13 PlayMode tests (new: the two-step aiming and tap-to-attack in the real scene). The soak
  tests also fail if the autopilot walks into an enemy or has a command refused, if a hero attacks nothing, or if
  anything is delayed twice before it acts. The golden replay was re-recorded (explicit targeted attacks, lowest HP
  first). The autoplay smoke test now aims each action once the way a player does and screenshots the highlight.
  The balance tool takes `-lead ID` and `seeds=N`, and reports who still stands on arrival at the boss floor and
  how often a badly hurt hero ran to safety (rarely: about once in 20-100 runs, since heroes heal first).

**First playtest checkpoint: Peter's answers (2026-10-03).**
- The run ends when the **whole party** has fallen (resurrection may come later: an ultimate, a weapon ability or a
  craftable item).
- **Aura of Protection covers Haiden too:** he and the allies next to him take 30% less damage, and at the start of
  each of his turns he heals them all, himself included, for 10% of his max HP. His AI raises it in a fight as soon as
  anyone it covers (him included) is in melee or hurt, instead of sitting on a full meter until an ally stands next
  to him.
- The **ultimate pace** is approved: about 2 per hero in the boss fight, one every 2-3 normal fights. With Haiden's
  aura now used as soon as it's ready, the old rates overshot it (one every 1.7 normal fights), so each source gives
  **10**: an action, a hit landed, a hit taken (was 15 / 10 / 20).
- The **85-100% damage spread** stays, and **slime EXP** (3, +1 per floor, to every hero standing) is fine.
- The weapon attack with **nothing in reach** stays refused (no swing at the air); Wait passes a turn.
- Also approved: Haiden leads; a boss rolls Stun Strike's chance like anyone else; a boss's snare delay is 25%; a
  stun outside a fight does nothing.
- **Balance now** (autopilot, Haiden leading, 200 seeds; the aura makes the party sturdier, so the King Slime has
  **16000 HP**): 13 fresh level-1 runs win (6.5%), 199 reach the boss floor; with levels kept, the first clear comes
  on attempt 2.8 at Lv 9.4. Ultimates: 2.1 per hero in the boss fight, one every 2.3 normal fights. On arrival at
  the boss floor Haiden still stands in 47% of fresh runs (35% before his aura covered him), Kristela in 55%, Uzuki
  always. A Lv 9 party sent straight to the boss wins 198 of 200, a Lv 12 one always.

**Party cohesion (after the checkpoint, 2026-10-03).** Peter's note from his runs: when a fight starts in a corner or
a corridor, the hero at the back of the line may wander off, most of all while one hero tanks everything at a room
entrance.
- **Why it happened:** partners treated their own allies as walls. Stuck behind them in a corridor, a melee partner
  looked for another way to the fight or to the leader (up to 12 steps for the fight; "following" could be a 60-step
  tour of the floor), Uzuki went looking for a tile to shoot from the same way, and two partners in a corridor in the
  wrong order could stand there for the rest of the floor. A foe just behind a wall also counted as near.
- **Now** (the rules are under "Party" above): partners fight within 6 steps' walk of the leader; they go around
  allies only when that's short, and otherwise close up and wait behind them; a partner swaps past the one that
  follows it in line when neither has a foe next to it, never the other way, so the line sorts itself without loops.
  The Auto leader, with partners in a fight, goes for the foes that are after the party (up to 12 steps off) and waits
  behind a partner that holds the way, instead of heading for the berries and the stairs.
- **Measured** (autopilot, Haiden leading, 200 seeds; `-- -spread`): in fights a partner was more than 4 steps' walk
  from the leader 21.1% of the time before and 3.8% now (2.1 steps on average, was 4.1), and the farthest anyone got
  was 42 steps before, 9 now. While exploring: 6.9% of the time before, 0.4% now. The soak tests fail if a partner
  ends an action more than 24 steps from the leader.
- **Balance** is where it was, so the King Slime keeps its 16000 HP. Over 600 seeds, 6.7% of fresh level-1 runs win
  (6.5% before) and the first clear comes on attempt 2.9 at Lv 9.3 (2.7 before); Kristela reaches the boss floor
  standing in 65% of fresh runs (54% before). The usual 200 seeds: 18 wins, 199 reach the boss floor, first clear on
  attempt 2.9 at Lv 9.4, 2.3 ultimates per hero in the boss fight and one every 2.4 normal fights.
- **Checks:** 195 Core tests (7 new: waiting behind the tank, no walk around the floor for a shot, a short way around
  still taken, the leash, the regroup swap and its limits, the Auto leader holding). The party trace (`-- -party`)
  prints where the foes after the party are and who swapped.

**Art pass 1 (2026-10-04, roadmap row A1).** The game runs on the Tiny Swords art; the spec is
[docs/design/ART.md](docs/design/ART.md), the sheets and screenshots are in
[docs/screenshots/2026-10-04-art-pass-1](docs/screenshots/2026-10-04-art-pass-1).
- **New names for the first dungeon**, to match the art (their own commit, rules untouched): the Slime is the
  **Spider**, the Bat the **Giant Bat**, the King Slime the **Troll**, and the "Slime Cave" is **Troll's Hollow**. The
  entries above keep the names of their time.
- **The art build:** `python Tools/pixelart/build_art.py` (Python 3.7, standard library only) reads the packs from
  outside the repo and writes everything under `Assets/Art/Resources`: 205 PNGs (405 KB) and `art_manifest.json`
  (frames, pivots, animations, 9-slice borders, where the head sits on each frame of a body). It takes the pack's
  units apart into layers, recolors and reshapes them, and draws what the pack doesn't have in its palette and
  outline. Only what the game uses is in the repo; the packs and their `.aseprite` files never are
  ([docs/THIRD_PARTY.md](docs/THIRD_PARTY.md)). Sprites are 64 px a tile, point-filtered and uncompressed; the
  camera zooms in whole steps only, and the HUD's art is drawn at a whole number of screen pixels per art pixel.
- **Heroes from layers:** a hero is stacked at run time (`HeroComposer`) from a body (the pack's Warrior, Archer,
  Monk or Pawn, picked by the weapon), the weapon, an armor set (its colors and head piece) and the hero's own head:
  face, eyes and hair drawn for each of the three, showing under the helmets. In the art: the 9 weapons of GEAR.md
  (Long Sword, Great Sword, Arcane Sword, Piercer Blade, Dual Blades, Great Shield, Hunter Bow, Mage Staff,
  Gauntlets), its 9 armor sets, its 9 ring sets (a twinkle on the hand, and a flash when the ring does something),
  3 cosmetic head pieces (hair bow, crown, headband), and an icon for every weapon, armor piece and ring. Every hero
  can show every weapon and armor set (a test builds them all). The looks for now: Haiden with the Long Sword in
  Heavy Armor, Kristela with Gauntlets as a Light Warrior, Uzuki with the Hunter Bow in the Archer's Garb.
  `-fk-look` tries others; milestone 1h sets a hero's look from the gear it has equipped (`HeroLooks.Set`).
- **Peter's choices at the two checkpoints:** Kristela wears a skirt and no ribbon; Haiden has no headband; the
  archer's helmet has no nose bar; the larger colored eyes and the head pieces stay; rings show as a twinkle on the
  hand, with a flash when they act, in nine colors; the bow, crown and headband are extra head pieces. **View size:**
  2x on a 1080p phone (sprites twice their art size, about 18 x 8.4 tiles in view). While aiming, the camera moves no
  further than it must to show every target clear of the HUD; when that would take the party out of the middle third
  of the screen, or the targets don't fit, it steps out to 1x until the aim ends. An always-1x view stays behind
  `-fk-view wide` for a player setting later; a camera that slides to the targets at 2x is out (the party ends up at
  the edge of the screen).
- **The dungeon** is outdoors: grass to walk on, raised ground with cliff faces for walls, bushes, rocks, trees,
  bones and skull spikes on the raised ground, and a cave entrance as the way down.
- **Animations** come from the pack's own strips: idle, walk, the sword's two swings, guard, cast, the bow's draw and
  release, the punches; the monsters' attacks; the Troll's wind-up (held while it charges its slam), slam, recovery
  and fall. An attack's impact frame lands when the turn's hit does, and nothing waits for an animation, so turns
  are as fast as before. Effects: the pack's dust, explosion and heal; drawn for it, the punch burst, the shock
  ring, the snare, the status icons and the aim reticle. The Troll turns see-through while someone stands behind it.
- **HUD** on the pack's UI kit: buttons, party cards and turn order with portraits cut from each hero's own look,
  bars, the banner's ribbon, the D-pad.
- **Rules untouched:** the balance report prints the same numbers as before the art pass (only the boss's name
  differs) and the golden replay's fingerprint is unchanged.
- **Checks:** 195 Core tests, 23 PlayMode tests (new: no missing sprite anywhere, every hero in every weapon and
  armor set, the camera's aiming rule on the phone, the tablet, Peter's two devices and small screens), the Windows
  build, and four autoplay runs (phone, straight to the boss, tablet with Uzuki leading, keyboard layout) without an
  error or a placeholder square.
- **Open:** Peter's OK on the monsters' and the dungeon's names; where menu frames with gold trim and a pixel font
  come from (ART.md's open questions); new bosses and creatures (roadmap row 7).

**Corridor tactics (2026-10-04, roadmap row C1).** Peter's playtest note: a melee hero who steps through a room's
entrance is surrounded and falls while the two behind him can't do anything useful. The party's AI (the partners and
the autopilot's leader) now uses the corridors. The rules are under "Party" above and in PROGRESSION.md, "Doorways
and corridors"; they are AI only, so nothing changes for what the player may do.
- **Measured first.** `-balance` now also reports the heroes that fall before the boss floor (and how many of them
  fell with a fresh melee ally idle within two tiles), what each hero does with its turns in fights, and how often a
  hero is hit by three or more enemies in one round. Before (autopilot, Haiden leading, 200 seeds): 162 heroes fell
  before the boss floor, all but 4 of them out of the corridor, with one to three spiders on them, and 70 with a
  fresh melee ally idle within two tiles. Haiden reached the boss floor standing in 53% of runs, Kristela in 66%.
  In the rounds of a fight where blows were exchanged, Kristela attacked or used a skill in 14% of her turns and
  waited in 54%. Three enemies hitting one hero in a round was rare (0.7% of rounds): two at a room's entrance were
  enough.
- **Hold the door.** A corridor tile or a doorway has at most two ways in or out (`DungeonMap.IsNarrow`), so the
  corner rule lets only the tile straight ahead reach a hero there. With more than two foes close beyond the doorway
  the hero in front waits for them there, for up to 5 turns if nobody comes (foes that are after someone they can't
  reach only mill about); then he goes in after all. Not against a boss: a slam is dodged in the open.
- **Rotate the front.** Under half HP, the hero that holds a corridor or a doorway trades places with the melee
  hero behind him if she has 20 points more of her HP left, in the fight or just before it reaches them; in a
  corridor's mouth only with one or two foes on him (a fresh hero isn't fed to a crowd). The one behind then heals
  whenever none of the heal is wasted. The pair can't trade back for 3 turns, as with every swap. A partner behind
  the leader never takes his place: the autopilot's leader steps back himself, and by hand that is the player's move.
- **Enter when it's safe.** Against one or two close foes the party goes in, so that the second melee hero gets to
  fight, but only if the hero in front has at least 90% of his HP and there is a melee hero to bring; hurt, or with
  nobody to bring, he goes in against one foe only. Fighting in the corridor's mouth with the other still behind
  him and two or more foes near, he gives up a turn to step aside to another tile he can fight from, and she comes
  out.
- **The numbers come from the run simulations**, as Peter asked. "Close" is the foes' sight range, 5 steps from the
  hero (4 tiles into the room): a foe that near has noticed him and is on its way, so the wait is never in vain. In
  an earlier state of the rules, 4, 5 and 6 steps left 167, 131 and 93 heroes fallen before the boss in 600 runs (519
  with no rules); at 6 he also waits for foes that haven't noticed him, and Kristela fights less. The 90% came last:
  without it 76 heroes fell in 400 runs, with it 37, and a lower bar does little (at 60% nine in ten of those still
  fell; from 85% up it makes no difference). Tried and dropped: waiting for a crowd out in the open as well, and the
  Auto leader walking around his partners to get to the front (no gain); holding the door whenever any foe stands
  right outside it (as safe as the 90% rule, but then Kristela fights no more than she did before).
- **After** (same 200 seeds): 15 heroes fall before the boss floor (was 162), 3 of them next to an idle fresh ally
  (was 70); Haiden reaches it standing in 97% of runs (53%) and Kristela in 95% (66%). On average the leader holds a
  doorway for 1.8 turns a run and the front rotates 1.2 times. Kristela attacks or uses a skill in 18% of her fight
  turns (14%) and waits in 46% (54%): in a corridor only one hero can fight, and this dungeon is mostly corridors.
  The partners still stay together: more than 4 steps from the leader 5.2% of the time in fights (3.8% before), at
  most 11.
- **Re-tune.** With all three usually alive at the boss, 30 of 200 fresh level-1 runs won (15%) and the first clear
  came on attempt 2.5 at Lv 8.8. The packs can't make up for that (bigger packs or harder spiders only mean more EXP),
  so, as before, the Troll has more HP: **24000** (was 16000). Now 9 of 200 fresh runs win (4.5%; 28 of 600), and
  with levels kept the first clear comes on attempt 3.1 at Lv 9.7 (3.0 at Lv 9.6 over 300 parties). A Lv 9 party
  sent straight to the boss wins 187 of 200 (198 before) in 54 of the leader's turns (41 before), a Lv 12 one always.
  Ultimates in pack fights are where they were (one every 2.4 fights); in the boss fight the report shows 2.95 per
  hero (2.26 before) because all three now get there: per hero that arrives it is about 3.0, as before. A winning
  boss fight is longer, though: a Lv 9 party uses 4.6 ultimates per hero in it (3.0 before).
- **Not built** (Peter: not now): pulling a pack to the door with a shot, walking in and backing out to drag enemies
  along, wider corridors.
- **Checks:** 219 Core tests (24 new in `DoorTacticsTests`: the corner rule at a doorway, each rule and its limits,
  a crowd coming to the door one at a time). The soak tests also fail if no doorway is ever held or the front never
  rotates. The golden replay was re-recorded: its lone hero now holds a doorway against two or more foes. The party
  trace takes `map=N` to draw the floor around the leader.
- **Open:** with Kristela leading (`-lead kristela`) she still falls before the boss in about one run in five: out
  in a room she walks up to the pack like a tank. And nothing on screen says why a hero waits at a door; a line in
  the log would be easy to add.

**Milestone 1g, part 1 (roadmap row 1g-1): the three follow-ups (2026-10-04).** The classes core, the skill tree
and tiers 5 and 10 follow in this same part.
- **The tooling lives in an ordinary folder.** The Unity sandbox, `checkpoint.ps1`, its results, the test APK and
  the Tiny Swords packs moved from `%LOCALAPPDATA%\5Kingdoms` to `C:\Users\peter\5Kingdoms`. The Claude desktop app
  keeps `%LOCALAPPDATA%` private to its own sessions, so Explorer, Peter's editor and the C# compiler server it
  starts never saw those files; that was the cause of the CS2011 errors in sandbox compiles. The art rebuilds
  byte-identical from the new place, and the sandbox compiles there while the editor is open. The old private
  folder (8.9 GB) is unused and left for Peter to delete.
- **Heroes heal between fights** (the rule is under "Party" above). Measured (autopilot, Haiden leading, 200
  seeds): 6.9 heals a run are now used outside a fight (0.4 before), and the Auto leader waits 2.4 turns a run for
  them. The HP brought into a fight went from 81% to 96% for Haiden and from 92% to 95% for Kristela; into the
  boss fight, from 75% to 95% and from 87% to 94%. **The balance did not move**, so nothing was re-tuned: 8 of 200
  fresh level-1 runs win (9 before; 17 of 600), and the first clear comes on attempt 3.1 at Lv 9.6 (3.1 at Lv 9.7
  before). A full HP bar at the Troll's door is worth little against 24000 HP; what decides that fight is the
  party's level. (The C1 session saw wins double when it healed "by accident". That change isn't in the repo; a
  guess, not checked: it also healed more freely inside fights.) A fit Haiden steps into rooms a little more often:
  19 heroes fall before the boss floor in 200 runs (15 before).
- **A line when a hero waits** (under "Party" above). The rules say what the AI is doing: a `HeroWaitedEvent` for
  holding a door or resting, and the reason on every swap (rotate, run to safety, engage, regroup, passing); the
  view only shows it. The balance report and the soak tests now count rotations from that reason instead of
  guessing them.
- **Checks:** 230 Core tests (11 new in `BetweenFightsTests`: the top-up rule and its limits, the hurt partner
  walking to the healer, the Auto leader waiting and giving up, the events), 230 in Unity's EditMode, 23 PlayMode,
  the Windows build, and four autoplay runs without an error. The soak tests also fail if the leader never rests,
  or rests longer than its patience. The golden replay is unchanged (its lone hero has no heal).

**Milestone 1g, part 1: the classes core (2026-10-05).** The rules of PROGRESSION.md, "Classes" and "Building 1g"
(part 1, items 2 to 5), built so that nothing changes in play yet: the balance report prints the same numbers line
for line, and the golden replay is unchanged.
- **Points and tiers.** A hero has a point per level. A point buys the next tier of any class; tiers 5, 10, 15, 20
  and 25 are milestones that also take one of three options, from any of the class's three paths. A milestone whose
  options aren't written yet can't be passed, so a class stops at tier 4 until its tier 5 exists. Every tier adds
  the class's stat bumps (never SPD). A hero starts at tier 1 of its own class.
- **The classes in the catalog:** Archer (Uzuki), Paladin (Haiden), Fencer (Kristela, since Peter's change of
  2026-10-05) and Monk (kept as a class, no starting hero), each with its three paths and its speed modifier. Their
  stat bumps and options are not in yet: they come with the content, so play is unchanged until then.
- **A kit of the hero's own.** The skills a hero fights with used to be read from the shared definition of the
  hero; now they come from the hero's saved progress. Its pool is its starting kit plus what its options teach; an
  upgrade changes the hero's own copy of a skill, and an upgrade of a skill the hero doesn't know teaches the skill
  instead. The loadout is three skills and an ultimate: at most one Quick skill, and a skill tied to a weapon
  family (bow, sword, fists) only while the hero holds such a weapon. It changes between runs only.
- **Speeds are as they were:** the heroes' own speeds are 90 / 95 / 95 (Uzuki / Haiden / Kristela) and their
  classes add +5 / -5 / +5, kept within 85-100 before gear.
- **Respec:** unlearning a class returns its points (free for now; relearning from level 20 is part 2). A hero can
  even unlearn its own class; its starting kit stays, since that is the hero's and not the class's.
- **Saves are version 2:** each hero's classes, picks and loadout. A hero from an older save gets a point per
  level with tier 1 of its own class spent and the rest free (Kristela as a Fencer). Loading learns a build again
  tier by tier, so anything today's rules don't allow is left out and its points stay free.
- **Kristela is a Fencer in name only so far.** Her class, her migration and her speed are the Fencer's; she still
  fights with the Monk's base kit and the Gauntlets until her Fencer kit is built (next).
- **Checks:** 259 Core tests (29 new in `ClassRulesTests`, on classes made up for the tests so the real ones can be
  tuned freely), 259 in Unity's EditMode, 28 PlayMode (5 new: the save migration, Kristela's, a mixed file, a round
  trip, a hero without a class), the Windows build and four autoplay runs without an error.
- **What is left of part 1, in the order to build it:**
  1. Kristela's Fencer kit (PROGRESSION.md, "Kristela's Fencer kit"): the Piercer Blade as her weapon and look,
     Thrust, Triple Thrust, Lunge, Riposte, Blade Dance, each with its rule, AI rule, animation and icon; she loses
     Ki Heal, so the balance moves.
  2. The class stat bumps (data only; they move the balance report a little, so they were left out of the core).
  3. The skill-tree screen. Its art is drafted but has never been run: the dark gold-trimmed panel, the spheres
     (tinted per path, with glow, ring and shine), a lock, badges and 24 skill icons, on the branch
     `wip/1g-1-skill-tree-art` (`Tools/pixelart/ui.py` and `icons.py`).
  4. Tier 5, then tier 10, of Archer, Paladin and Fencer; then default builds for the balance report and the
     re-tune to the targets in CLAUDE.md.

## Open questions (resolve as we go)
1. Final names of the five kingdoms. Light element or not, and Wind's advanced form.
2. ~~Pixel art spec.~~ Answered (2026-10-03): the Tiny Swords pack, 64 px tiles, 10 fps strips; see [docs/design/ART.md](docs/design/ART.md), which lists its own open questions.
3. ~~Exact armor pieces and how many weapon slots.~~ Answered in GEAR.md: head, body, hands, feet, 2 rings, 1 weapon.
4. ~~Stamina and timers.~~ Answered: see "Energy" above.
7. ~~Does a run end when the leader falls, or only when the whole party has fallen?~~ Answered: the whole party.
   Resurrection may come later.
10. ~~Should Aura of Protection cover Haiden himself?~~ Answered: yes, the caster too.
11. ~~Ultimate pace.~~ Answered: two per hero in the boss fight and one every 2-3 normal fights is good for now.
9. ~~Keep the 85-100% random damage spread?~~ Answered: keep it.
12. ~~Slime EXP of 3 (+1 per floor) to every hero standing.~~ Answered: fine.
13. ~~The weapon attack with nothing in reach.~~ Answered: it stays refused, no swing at the air.
14. ~~Should a skill that needs a target be usable at nothing, to pass the turn?~~ Answered (2026-10-03): no. It
    stays refused without a target (no turn used); Wait is the way to pass a turn.
8. ~~Crystal Ice Legion Hunter's prerequisites, the Space/Time element, monk weapons.~~ Answered in the specs: Archer 15 +
   Mage 10; Darkness covers time and space; Kristela uses Gauntlets (the Monk/fist weapon type, passive later).
5. Store policy and legal check before any paid gacha (odds disclosure is required in app stores and some regions).
6. A Mac for iOS, or a build service?

## Workflow
- Code lives in the private GitHub repo `plo-sweetP/5-kingdoms`; pushes happen at milestones.
- The project stays on the local drive, not in a Google Drive sync folder.
- Nightly update docs go to the shared Google Drive folder: summary, what was added, current progress, PR/commit updates with explanations, and anything else important.
- Sessions (2026-10-03): one planning hub plans with Peter, writes the specs (`docs/design/`, this file) and hands each major task to a fresh build session; build sessions build, test and push. Before a session that finished its task is closed, it adds a release notes and commit notes doc to the same Drive folder.
- Third-party art: the Tiny Swords packs may be used commercially but not passed on, so the repo stays private (ART.md, "The pack").

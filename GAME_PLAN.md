# 5 Kingdoms: Game Plan (v0.2)

## Decisions locked in
| Topic | Decision |
|---|---|
| Engine | Unity 6.3 LTS (6000.3.25f1), 2D with URP, C# |
| Platforms | iOS + Android first, one codebase. Developed on Windows; Android is the test platform until there's a Mac or build service. **PC on Steam (incl. Steam Deck) planned later** |
| Orientation | **Landscape only**, on phones and tablets (test devices: Samsung S22 Ultra, Galaxy Tab S8+) |
| Monetization | Phase 1: free-to-play where **breeding is the gacha** for monsters. Later: a paid gacha for **characters and weapons** |
| Art | **Pixel art** in the spirit of Final Fantasy Tactics Advance and Pokemon Mystery Dungeon: simple chibi sprites, flashier attack and defense animations. 32 px = 1 tile. Placeholders generated in code for now, AI-assisted art later |
| Controls | 8-way grid movement. On-screen D-pad + buttons first, tap-to-move later |
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
- **Advanced / variant elements** grow out of base ones: Lightning (from Fire), Ice (from Water), Metal (from Earth), Space/Time (from Darkness). Wind's advanced form is TBD.
- Implementation: a data-driven element chart (ScriptableObject): each element has a parent, strengths, weaknesses. An advanced element inherits its parent's matchups plus its own tweaks. New elements can be added later without code changes.
- Breeding hook: advanced elements are unlocked by specific parent pairs and ingredients, so they act as the "rare" tier.
- Open: Is there a Light element? Darkness is in the base set without an opposite. Decide before building the type chart.

## Heroes, skills and equipment (planned)
- **Main character:** Uzuki (concept sketch: spiky blue hair, cyan eyes, sleeveless top with strap, one pauldron, baggy cuffed pants, boots). Placeholder sprite exists.
- **Persistent progression:** characters keep their level and gear between dungeon runs (unlike Mystery Dungeon's resets). They level up and gear up outside, at the farm, then enter dungeons to clear the stages and the boss. Defeating monsters and bosses inside a dungeon also gives EXP that carries back out.
- **Skills:** each character gets **3 class/job/race skills + 1 ultimate**. Skills can cost mana, cost nothing (with more unique effects), or build mana. Ultimates (buffs, debuffs, damage, summons, ...) are designed later. Basic attacks only for now; the HUD already shows the four locked slots.
- **Equipment slots:** a **4-piece armor set + 2 rings + weapon slot(s)**. Set bonuses for wearing a full armor set are a natural fit.
- **Equipment sources:** looted in dungeons or crafted on the farm. **Weapons can also come from the gacha.**
- Data rule: gear, sets and drop tables live in data assets, like everything else.

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
- **Action costs:** every action costs one full turn for now; each action kind has a cost multiplier ready for skills.
- **Joining mid-fight:** summoned helpers first wait a full turn; reinforcements that walk in between rounds act in the
  coming round.
- **Speed changes** (buffs, later) keep the distance an actor has left and recompute when it acts; the gauge isn't reset.
- **HUD:** a turn-order strip (next 6 turns of the fight, cycle dividers, "+AV" until each, SLAM on a winding-up boss's
  next turn), shown only in combat. Walk animations are slightly faster or slower with Speed.
- **Content:** slimes 100 (same as the hero), King Slime 85 (slow and heavy: about every 6th turn the hero gets two in a row).
- **Regression anchor:** with all speeds equal, the timeline reproduces the original alternating turns exactly (golden
  replay test; balance report identical before and after).
- **Later:** skill AV costs and mana (1d), Break/toughness with elements (1e), the party of 4 (the scheduler already handles
  any number of actors per team).

## Party (planned)
- **Up to 4 characters on screen**: a leader plus 3 partners, as in Mystery Dungeon. The size is one constant (`MaxPartySize`), so trying 3 is a one-line change if 4 feels crowded on a phone.
- The player controls the leader; partners act by AI with simple tactics (follow me / go after enemies / hold back), and the player can switch which character they control. Each character keeps their own 3 skills + ultimate.
- Turn order: leader, then partners, then enemies. In corridors partners follow in a line and swap places with the leader when bumped.
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

## Part 1: Farm and breeding
- Tile-based farm (Unity Tilemap), crops with growth stages, seasons, energy or stamina.
- Real-time timers for crops and eggs, with offline progress calculated on return.
- Monsters on the farm: hunger, mood and affinity. They can do jobs (water, harvest).
- **Breeding:** 2 parents + ingredient -> egg (timer) -> hatch. Inputs: parents' kingdom, traits and rarity. Outputs: species, rarity tier, mutation chance, inherited traits, and a pity counter for rare results.
- **Crafting:** farm workshops turn dungeon materials into equipment.
- Design rule: all odds live in data tables, not code, so we can tune without rewriting.

## Part 2: Dungeon crawl (Mystery Dungeon style)
- Grid movement in 8 directions. Every action is a turn, then all enemies act. Diagonal moves and attacks can't cut wall corners.
- Procedural floors: rooms and corridors, stairs down, items; later traps and monster houses.
- **Stages and a boss:** each dungeon is a set of floors (stages) ending in a boss fight.
- **Levels carry in and out:** the party enters at its farm-earned level and gear; EXP earned inside is kept.
- **Berries** will restore mana once skills exist; healing comes from skills and other items later. (In the prototype, berries heal HP.)
- Party of 1-3 (hero + monsters later). Skills, items, gear.
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
| 1d | Mana (berries restore it), first skills with AV costs, healing | Next |
| 1e | Elements and Break/toughness (needs the element chart) | Later |
| 2 | Farm prototype: plant, grow, harvest, inventory, day/season clock | |
| 3 | Monster and breeding core: stats, traits, egg, hatch, rarity and pity, with tests on the odds | |
| 4 | Connect the loops: farm monsters enter dungeons, loot flows back, saves work | |
| 5 | Vertical slice: one full kingdom with tutorial and basic UI | |
| 6 | Content expansion: kingdoms 2-5, monster roster, skills, equipment, balancing | |
| 7 | Art and audio pass: AI-assisted art replaces placeholders | |
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
  skills and the ultimate will stay usable by hand once they exist (1d).

**Milestone 1c (combat timeline)**: see "Combat timeline (action value)" above. Balance with the slower King Slime:
7/200 fresh level-1 autopilot runs win (was 3); with levels kept, players beat the boss on attempt 2.5 at Lv 9.3 (was 2.6 at 9.4).

## Open questions (resolve as we go)
1. Final names of the five kingdoms. Light element or not, and Wind's advanced form.
2. Pixel art spec (to go in ART_BIBLE.md): placeholders use 32 px tiles and 32x32 chibi sprites; confirm, and set palette limits and animation frame counts.
3. Exact armor pieces (e.g. head/body/hands/feet) and how many weapon slots.
4. Stamina and timers: how aggressive? Must stay friendly for short phone sessions.
5. Store policy and legal check before any paid gacha (odds disclosure is required in app stores and some regions).
6. A Mac for iOS, or a build service?

## Workflow
- Code lives in the private GitHub repo `plo-sweetP/5-kingdoms`; pushes happen at milestones.
- The project stays on the local drive, not in a Google Drive sync folder.
- Nightly update docs go to the shared Google Drive folder: summary, what was added, current progress, PR/commit updates with explanations, and anything else important.

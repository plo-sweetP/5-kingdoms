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
  - **Kristela** (girl): 5th kingdom (Medieval Realm), a princess. Base class **Fencer** (Peter, 2026-10-05: "I'm
    not feeling the monk abilities for her as one of the starting characters"; she was a Monk until then, and the
    Monk stays in the game as a class). Signature class, *proposed:* **Princess Timeless Fencer** (was Princess
    Timeless Monk). Concept: [docs/concept/kristela.jpg](../concept/kristela.jpg): long wavy blonde hair, blue
    eyes, gold X-shaped hair clip on one side.
- GAME_PLAN.md still calls Uzuki the main character and needs updating.

**Starting builds (first playtest):**
| Hero | Class | Role | Profession | Base SPD (*proposed*) |
|---|---|---|---|---|
| Uzuki | Archer, speccing into Mage (Ice) toward Crystal Ice Legion Hunter | **Utility DPS** (ranged; traps, slows and control) | Alchemist (potions for the team) | 95 |
| Haiden | Paladin (later Rune Warrior, toward Runegod Fire Blade) | **Tank first, with some healing** (frontliner; fire runes that guard, mend and hit back) | Blacksmith (equipment) | 90 |
| Kristela | Fencer | **Melee DPS** (speed build: thrusts, lunges and counters) | Chef (meals that buff the party between fights) | 100 |

Together they cover tank (with some healing), melee DPS and utility DPS, so the player's own character can fill
whatever role they like.

**Weapons:** Uzuki uses a **Hunter Bow**, Haiden a **Long Sword**, Kristela a **Piercer Blade**, her rapier
(*proposed*; as a Monk she had Gauntlets) (see GEAR.md).

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
  (names are drafts: Uzuki's Quick Shot, Haiden's Sword Slash, Kristela's Thrust; the Monk's is Jab).
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
  only ever the earlier past the later, never the leader), or relieves a hurt hero who holds a doorway or corridor
  ("rotate the front", below). A pair that just swapped can't swap back for a few turns. The soak test fails if it sees the same two heroes
  swapping back and forth.
- **Ranged targets anything within 5 tiles that's in sight** (decided), not only along the 8 grid lines. Walls block
  shots; allies don't. Damage stays at 90% of melee.
- **The party stays together** (Peter's playtest note, 2026-10-03): ranged partners take a tile to shoot from only
  near the leader and only by a short way; otherwise they stay behind the line. On Auto, the leader with partners in
  a fight goes for the enemies that are after the party (up to 12 steps) and waits behind a partner that holds the
  way.
- **Doorways and corridors** (decided 2026-10-04, built as roadmap row C1). Peter's playtest note: a melee hero
  who steps through a room's entrance gets surrounded and falls, while the two behind him can't do anything useful.
  This dungeon keeps its narrow corridors (Peter: other biomes will open the floor up in other ways), so the party's
  AI, the autopilot's leader included, uses them:
  - **Hold the door.** With more than two enemies close beyond a doorway (within their sight range of the hero:
    5 steps, 4 tiles into the room), the hero at the front stays on the last corridor tile instead of stepping into
    the room. Only the tile straight ahead can reach him there, and the archer shoots past him. If nobody comes
    within 5 turns he goes in. Not against a boss (a slam has to be dodged).
  - **Rotate the front.** Where one hero holds the way (a corridor or a doorway; in a corridor's mouth only with one
    or two enemies on him), when he is under 50% HP and the melee hero behind him has 20 points more of her HP left,
    they swap, in the fight or just before it reaches them: the fresh one fights, the hurt one heals behind. The
    swap cooldown and the no-loops check still apply. A partner never takes the leader's place (on Auto the leader
    steps back himself; by hand it is the player's move); a ranged hero takes the front only through run to safety.
  - **Enter when it's safe.** With at most 2 enemies close, the party goes in, if the hero in front has at least 90%
    of his HP and a second melee hero to bring; hurt, or with nobody to bring, he goes in against one enemy only. In
    the corridor's mouth, with her still behind him and 2 or more enemies near, he steps aside to another tile he
    can fight from so she can come out. (Peter set "2 or fewer close" and left the rest to the run simulations; the
    90% rule was the biggest single gain.)
  - **What it did** (autopilot, Haiden leading, 200 seeds): heroes fallen before the boss floor 162 -> 15; standing
    on arrival Haiden 53% -> 97%, Kristela 66% -> 95%. Kristela still waits in 46% of her fight turns (54% before):
    in a corridor only one hero can fight, and this dungeon is mostly corridors. The Troll went from 16000 to 24000
    HP to keep the targets (fresh level-1 wins 4.5%, first clear on attempt 3.1 at Lv 9.7).
  - **Not built, as decided:** pulling a pack to the door with a shot ("almost seems too advance for now"), walking
    in and backing out to drag enemies along, and wider corridors. Reach weapons (a spear, the Arcane Sword) are the
    gear answer to hitting past an ally.
  - **A note when a hero waits** (Peter, 2026-10-04; to build in 1g part 1): when a hero holds a door or trades
    places, a short line on screen says so, "so the player doesn't think the hero is just standing there or stuck".
    His example: "<hero name> is strategizing for the next fight". Cute is welcome.
  - The boss fight got about a third longer with the Troll's 24000 HP; Peter keeps it ("this is really just a test
    boss"), and difficulty gets adjusted when real levels and tiers of bosses exist.
  - With Kristela leading she still falls often; that is accepted: "it actually makes sense for a dps to die quickly
    if they lead", and the party's arrangement is the player's to sort out.
- **Heroes heal between fights** (Peter, 2026-10-04; to build in 1g part 1): outside a fight, AI heroes (partners
  and the Auto leader) use their heals to top the party up before moving on. It needs a re-tune: when the C1 session
  tried it by accident, fresh level-1 wins went from 18 to 38 of 200, mostly through the boss fight.
- **Footing in a boss fight** (Peter's note from the tablet, 2026-10-04; built in playtest pass 1, part two, on
  2026-10-10; AI only): "when fighting the boss ... if the melle characters get stuck against the wall or the
  pillar. They are do not know to move or escape the bad situation. Can u help them navigate them out of those
  situations while doing damage?"
  - **A way out** (`HeroTactics.HasWayOut`): a free tile beside the hero that the boss's slam doesn't reach, and
    that no other hero needs as its only way out.
  - **Measured first**, as with the corridor tactics. `-balance` counts, per boss fight and per hero, the slams
    that hit a hero and how that hero stood when its last turn began (the rules say it with each
    `SlamCaughtEvent`: with a way out, with none, out of reach, or no turn since the wind-up). Before any rule
    changed (600 seeds): 1080 of 4957 slams hit a hero. The two melee heroes took 561 of those hits, and only 14
    with no way out. Every other one hit a hero that had stepped out of the wind-up, or had not been in it, and
    walked in on its next turn: the Troll is slower than they are, so they are often up again before the slam
    lands. Kristela fell to a slam 209 times in 600 fights. So the first rule is one this list did not have:
  - **Stay out of a wind-up.** No hero's AI walks, dashes or lunges into the reach of a slam that is winding up.
    It waits out of reach until the slam has landed, with "Keeping clear" over its head and a line in the log.
    Peter, 2026-10-10: "yes, keep the wind up rule."
  - **One more blow.** A hero in the wind-up that is up again before the slam lands strikes first and steps out
    on that next turn (the "while doing damage" of Peter's note).
  - **Stand where there is a way out.** Stepping out, a hero leaves an ally the tile that is its only way out,
    and a hero that stands on such a tile makes way for it. Partners come in, and a Lunge lands, where there is
    a way out. A melee hero next to the boss with none (its back to a wall, a pillar or a corner, or boxed in by
    allies) moves over to a tile next to the boss that has one, on a turn when the boss isn't winding up, giving
    up one attack. (Settled while building: a Lunge can't make that move. It runs along a line to the boss, and
    from a tile with no way out there is no step back onto another line. It picks its landing instead.)
  - **When the slam is coming and there is no way out**, the hero uses what it has: a dash or a roll that ends
    out of reach; a swap with an ally out of reach that can take the blow (only when the slam would fell the hero
    and the ally keeps at least 30% of its HP; never with the leader; "I've got this!" and a line in the log);
    else a guard, Riposte (25% less from a boss, and answered; only when the slam lands before her next turn) or
    the aura. Riposte against a slam is new: she used to fight on.
  - **What it did** (600 seeds): slams that hit a hero 1080 -> 523; a melee hero 561 -> 1. With the Troll at
    25000 HP fresh level-1 wins went from 28 to 46 of 600 (7.7%), so the Troll has 30000 HP now: 27 of 600 win
    (4.5%), and with levels kept the first clear comes on attempt 3.0 at Lv 9.6. Peter, 2026-10-10: "keep the
    troll hp for now. but we'll adjust later when we do more direct individual levels/stages".
  - **Not built: the archer** (decided on 2026-10-10: it waits for the Troll's leap). The 522 hits that are left all fall on
    Uzuki when he is the last hero standing: he backs into a wall or a corner and is slammed there. Tried in the
    simulator: letting him move to a tile with a way out changes nothing (in a true corner no tile next to him
    has one), and teaching him to back off toward open floor works too well: he beats the Troll alone, and fresh
    level-1 wins go to 476 of 600 even at 30000 HP. The Troll is slow and has no answer to an archer who keeps
    his distance. That is a gap in the boss, not in Uzuki's AI.
    **Decided by Peter on 2026-10-10** ("yes, add leap for the troll"): **every boss gets at least one move that
    reaches a hero who keeps his distance**, and the Troll's is a **leap**. *Proposed, to settle when it is
    built:* it aims at the farthest hero in sight, winds up for one of the Troll's turns with the tiles it will
    land on shown (so it can be dodged, like the slam), and comes more often once one hero is left. It is built
    with the individual stages, together with the Troll's HP (above), unless Peter asks for it sooner. Uzuki's
    footing (backing off toward open floor) comes with it: without the leap it wins the fight alone. Until then
    Uzuki's AI is as it was.
  - The hero the player controls is never moved: every rule is in the party's AI (`HeroTactics`).

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
| Kristela | **Triple Thrust** | Three quick hits on one adjacent enemy (~90% ATK each); each rolls its own crit |
| Kristela | **Lunge** | Dashes in a straight line to an enemy up to 3 tiles away and strikes it (~200% ATK): 2 tiles of dash, or 1 when the enemy is that near |
| Kristela | **Riposte** | A counter stance until her next turn: she takes 50% less damage (25% from a boss), and the first enemy that hits her from an adjacent tile is struck back (~250% ATK) |
| Kristela | Ultimate: **Blade Dance** | 5 strikes (~100% ATK each) shared among the enemies in a 3x3 area: each strike goes to the enemy hit least so far, the highest threat first |
| The Monk (no starting hero) | **Piercing Punch**, **Ki Heal**, **Stun Strike**, ultimate **Flurry of Blows** | Kristela's kit until 2026-10-05, kept as the Monk's base kit: ~220% on the target and the enemy behind it; a Quick self-heal of ~25%; ~160% with a chance to stun (a 50% delay, bosses 25%); 5 strikes, then the next turn comes 30% sooner |

**Kristela's Fencer kit (Peter, 2026-10-05).** His brief: "(1) multi 3 hit strike, (2) dash 2 positions with a
strike but can go 1 position if moving into a monster, and (3) a riposte to go into a counter stance then counter
strike if struck before the next turn. Instead of a full block. Do 50% of damage reduction and less if against a
boss", and an ultimate that is "a 5 strike that hits an area of 3x3". The numbers and the details below are the
hub's first drafts (*proposed*), to tune with `-balance`:
- **Thrust** is her always-ready weapon attack (200% ATK, as Jab was). Fencer skills need a sword in hand.
- **Triple Thrust:** 3 hits of 90% on one adjacent enemy. Each hit rolls its damage and its crit on its own; the
  charge meter counts one action and three hits landed.
- **Lunge:** the target is an enemy up to 3 tiles away in a straight line (the 8 directions; the corner rule
  applies) with nobody standing between. She dashes to the tile in front of it (2 tiles, 1, or none when it is
  already next to her) and strikes for 200%. It takes a full turn and, like every skill, needs a target.
- **Riposte:** takes a full turn. Until her next turn she takes 50% less damage (25% from a boss), and the first
  enemy that hits her from an adjacent tile is struck back at once for 250% (it can crit, and it charges the meter
  as a hit landed). One counter per stance; the damage cut lasts until her turn. The stance is paid for with her
  turn, so it isn't a reaction (those stay at one per cycle). An icon over her head shows the stance.
- **Blade Dance:** she targets an enemy next to her, and the area is the 3x3 around that enemy. Five strikes of
  100%, one after another: each goes to the living enemy in the area that this ultimate has hit the fewest times,
  and among those to the highest threat (a boss first, then the enemy she targeted, then the highest ATK, then the
  lowest actor id). Peter's examples follow from that rule: a boss alone is hit 5 times; a boss with two minions
  takes the 1st and 4th strikes and the minions the 2nd, 3rd and 5th; if the minions fell to their strikes, the
  boss takes the 4th and the 5th too. She doesn't move. As with every ultimate, its own hits don't charge the
  meter.
- **She has no heal of her own now** (the Monk had Ki Heal): Haiden's Heal, his aura, berries and the healing
  between fights carry her, and the balance is re-tuned for that.
- **AI rules** (the autopilot and the partners are the balance report's players): Triple Thrust whenever it is
  ready and a foe is next to her; Lunge to reach a foe instead of walking up to it, through the same checks as any
  step toward the foes (`HoldsTheDoor`, the leash); Riposte when a foe next to her acts before her next turn and
  isn't held by Haiden's taunt, never inside a boss's slam (she steps out, as today); Blade Dance when the area
  holds a boss or at least two foes.
- **As built** (2026-10-05, main f83b13a; Peter approved the drafts above the same day: "The fencer drafts looks
  good"). What the build session settled, by its report:
  - Riposte's AI rule is narrower: she takes the stance when a foe next to her acts before her next turn and is
    going for her (not held by Haiden's taunt, not busy with a hero it reaches first). With the wider rule most
    stances went unanswered.
  - Thrusts of Triple Thrust left over when its target falls are lost (the Monk's Flurry moves on to another foe).
  - Riposte's damage cut adds to the aura's (50 + 30 = 80% less; 25 + 30 from a boss). A slam from the next tile
    counts as a hit and is answered; the AI never stands in one. A counter can fell a monster on its own turn.
  - Lunge's AI doesn't dash out of a corridor from further back than its last tile: she walks to the doorway
    first, where "hold the door" is decided.
  - The Piercer Blade has the Gauntlets' stats (60 HP / 22 ATK / 4 DEF at item level 1).
  - Her attacks show a streak of light, not a slash arc. Blade Dance shows an image of her at each foe; she and
    the camera stay where they are.
  - The class stat bumps are in for Archer, Paladin, Fencer and Monk. The kit and the bumps raised the win rate
    just past the target (Blade Dance on a lone boss and Riposte outweigh the lost heal), so the Troll has
    **25000 HP** (was 24000): 28 of 600 fresh level-1 runs win (4.7%), first clear on attempt 3.1 at Lv 9.7.
    Without her heal more heroes fall on the way down (19 to 31 in 200 runs).

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

### Fencer (Kristela's class since 2026-10-05): the hub's draft from Peter's brief, not yet seen by him
Peter: "Make the skill tree to somewhat follow that idea" (her three abilities, under "Starting kits" above).
Every tier: +0.2% Crit Rate and +0.4% Crit DMG (+5% and +10% at tier 25). Class speed modifier +5, the same as the
Monk's, so her speed stays 100.

| Tier | Duelist (flurries and crits) | Footwork (lunges and tempo) | En Garde (counters) |
|---|---|---|---|
| 5 | **Precise Thrusts:** each hit of Triple Thrust has +15% Crit Rate, and the third hits for 130% (was 90%) | **Long Lunge:** Lunge reaches 4 tiles (was 3) and hits for 240% (was 200%) | **Sharp Riposte:** the counter hits for 320% (was 250%), and the stance cuts damage by 60% (was 50%; from a boss 30%) |
| 10 | **Feint** (new Quick skill): the target takes +30% damage from her next strike on it | **Fleche** (new skill): she runs through an enemy next to her to the free tile behind it and hits it for 220%; her next turn comes 25% sooner | **Parry** (new Quick skill): she takes 40% less damage until her next turn (20% from a boss) |
| 15 | **Remise:** when a hit of Triple Thrust crits, she adds a fourth hit for 90% | **Momentum:** after a skill that moved her, her next strike deals +30% | **Counter Stance:** Riposte strikes back at every enemy that hits her until her next turn (was the first only); the later counters hit for 150% |
| 20 | **Coup de Grace** (alternate ultimate): one thrust for 800% on a single enemy | **Storm of Steel:** Blade Dance is 7 strikes (was 5) | **Perfect Guard** (alternate ultimate): for 2 of her turns she takes 50% less damage (25% from a boss) and strikes back at every enemy that hits her for 200% |
| 25 | **Master Duelist:** Fencer strikes deal +15%, and her crits +20% Crit DMG | **Master of Footwork:** her first turn in a fight comes 30% sooner, and Lunge and Fleche take 25% less time | **Master of the Riposte:** her counters deal +25%, and a counter brings her next turn 20% closer (once per turn of hers) |

Feint and Parry are both Quick skills, and a loadout holds one. Every advance stays inside the turn budget
("Classes": at most 30% of a turn per effect).

### Monk (kept as a class; no starting hero has it since 2026-10-05): approved by Peter as a starting point (2026-10-03)
Its tiers are built when a hero or a weapon brings the class into play. "Kristela" and "her" below mean the monk.
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
game going and farm tested") and then asked for more variety and set the healing rules below. This is the revision
after that. Peter has seen it (2026-10-04: "Cool"); fine-tuning comes later.

**Variety rule (Peter, 2026-10-04):** professions should differ in what they bring: buffs, debuffs, utility, item
support, companion support, traps, enhancing oneself, healing. **Healing is not handed to every class or
profession**; it has to make sense for the one that has it. **Potions always heal better than food** (Peter:
"bc of magic stuff"): a meal may heal, but less than what a potion gives. How the starting set is split:

| | Brings | Healing |
|---|---|---|
| Alchemist | debuffs by flask, potions (item support) | yes: healing potions, the strongest healing a profession gives, usable in a fight |
| Blacksmith | armor-breaking debuffs, enhancing himself, later his allies | none |
| Chef | buffs by meals eaten between fights, item support (her stock of meals), later treats for monster companions | a little, when a meal is eaten between fights; always less than a potion |
| Paladin (class) | protection, taunts, smites | yes: allies and himself |
| Fencer (class) | thrusts, lunges, counters | none |
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

### Alchemist (Uzuki's profession): debuffs and potions
Every tier: +0.4% Affinity and +0.4% healing done (+10% each at tier 25).

**Potions are items** he makes and the party carries (Peter, 2026-10-04: "Allow alchemist to new potions to
carry"): 3 per run for now (later they're brewed on the farm). A potion can be drunk or thrown to an ally within
4 tiles **in a fight**, which takes a turn. Each time one is used he picks a recipe he knows. They don't take a
skill slot.

| Tier | Bomber (debuffs) | Brewer (potions) | Transmuter (farm, locked for now) |
|---|---|---|---|
| 5 | **Acid Flask** (new skill): thrown at a foe within 4 tiles: 120%, and it takes +20% damage from everyone for 2 turns | **Healing Potion** (recipe): heals 35% of the drinker's max HP | Potions brew 20% faster |
| 10 | **Frost Flask** (new skill): every foe in a 3 x 3 area within 4 tiles takes 100%, and their next turn comes 30% later | **Field Brewing:** outside a fight he can brew a berry into a new potion, and the party can carry 5 (was 3) | A 15% chance of a second potion |
| 15 | **Smoke Bomb** (new skill): every foe in a 3 x 3 area within 4 tiles deals 25% less damage for 2 turns | **Vigor Potion** (recipe): the drinker deals +25% damage for 3 turns | Reroll one substat of a gear piece for materials |
| 20 | **Philosopher's Fire** (alternate ultimate): a 5 x 5 blast within 4 tiles, 300% to every foe in it | **Elixir of Life** (alternate ultimate): heals every ally in sight for 40% of their max HP (later it can also revive a fallen ally) | Craft Tuning Stones |
| 25 | **Master Bomber:** his flasks reach 5 tiles and their effects last a turn longer | **Master Brewer:** his potions are a quarter stronger, and drinking or throwing one takes half a turn | Once a day, a double batch |

He knows a weak healing recipe from tier 1 (heals 20%, still more than a meal), so every option is useful on its own. An earlier draft had
berry upgrades here (stronger berries, a berry bomb, turning a weak foe into a berry); they can come back as options
if potions alone feel thin.

### Blacksmith (Haiden's profession): armor breaking and enhancement
Every tier: +0.4% ATK and +0.4% DEF (+10% each at tier 25).

| Tier | Hammer (debuffs) | Tempering (enhancing himself, then allies) | Forgemaster (farm, locked for now) |
|---|---|---|---|
| 5 | **Sundering Strike** (new skill): 180%, and the target takes +20% damage for 2 turns | **Temper Blade** (new skill): his own attacks deal +25% for 3 turns | Forging takes 20% less time |
| 10 | **Hammer Blow** (new skill): 220%, and the target's next turn comes 30% later | **Reinforce Armor** (new skill): he takes 30% less damage for 3 turns | One substat of a crafted piece starts a tier higher |
| 15 | **Shatter:** Sundering Strike's bonus is +35% and lasts 3 turns | **Field Forge:** Temper Blade and Reinforce Armor can be used on an ally next to him instead | Salvage returns 25% more |
| 20 | **Armor Shatter** (alternate ultimate): 500%, and the target takes +40% damage for 3 turns (a boss +20%) | **Battle Forge** (alternate ultimate): for 3 turns every ally deals +25% damage and takes 15% less | Once a day, a masterwork: a crafted piece with a third substat of your choice |
| 25 | **Master Hammer:** his strikes deal +15%, and sundered foes also deal 15% less damage | **Master Temperer:** his enhancements are a quarter stronger and last a turn longer | A third substat choice costs nothing extra |

### Chef (Kristela's profession): meals that buff the party, eaten between fights
Peter, 2026-10-04: "The field cook can bring meals as potion replacement. But not during mid combat. Meals can give
buffs but should not heal or replacement to full potions." So:
- **Meals are items** she brings into a run: 3 per run for now (later they're cooked on the farm from ingredients).
  They don't take a skill slot.
- A meal is **eaten outside a fight only**, by the whole party, and its buff lasts **through the next fight**. One
  meal buff at a time: a new meal replaces the old one.
- **A meal heals a little**: each hero recovers 15% of max HP when it's eaten. Peter, later the same day: "You can
  add healing to it. I really just meant that the healing should be less than what potions provide. Potions healing
  should always be better than food healing bc of magic stuff." A meal never heals as much as a potion of the same
  tier, and it can't be used in a fight; potions are the Alchemist's.
- She knows one plain dish from tier 1 (Trail Mix: +5% damage in the next fight), so every option below is useful on
  its own.

Every tier: +0.6% HP (+15% at tier 25).

| Tier | Field Cook (dishes: what a meal gives) | Provisions (item support: her stock of meals) | Gourmet (farm, locked for now) |
|---|---|---|---|
| 5 | **Grilled Skewers:** every hero deals +15% damage in the next fight | **Extra Portions:** she brings 5 meals a run (was 3) | A meal at home gives the party +10% max HP for the next run |
| 10 | **Hearty Stew:** every hero takes 15% less damage in the next fight | **Butcher:** each floor's first pack of monsters, and every boss, leaves an ingredient: one more meal | One more food item can be taken into a run |
| 15 | **Strong Tea:** every hero's first turn in the next fight comes 30% sooner | **Leftovers:** when a meal's fight ends, half of its buff stays for the fight after | Treats that strengthen monster companions (when they join the party) |
| 20 | **Banquet:** once per run, a meal that gives all her dishes at once | **Lunchboxes:** each hero carries one dish of their own, eaten outside a fight, on top of the party's meal | Once a day, a banquet with a large buff for a whole run |
| 25 | **Master Cook:** her dishes are a third stronger | **Master of Provisions:** meals last two fights | Meals cost half the ingredients |

## Building 1g (in parts)
Peter left the split to the planning hub (2026-10-04). Each part ends tested, pushed and playable. The code notes
come from the 1f, art pass and C1 build sessions.

**Part 1 (roadmap row 1g-1): the core, the skill tree, and tiers 5 and 10 of the three classes**
1. Three small follow-ups first: the test sandbox, its results and the art packs move out of the Claude app's
   private AppData to an ordinary folder (`C:\Users\peter\5Kingdoms`); heroes heal between fights; a note on
   screen when a hero waits at a door (both under "Ranged vs melee" above).
2. The rules: a hero has as many points as levels; a class has tiers 1-25 at one point each; every tier gives the
   class's stat bump (never SPD); tiers 5, 10, 15, 20 and 25 are milestones with three options, one picked per
   tier from any path. A hero starts at tier 1 of their class. Any hero can put points into any base class; an
   option that upgrades a skill the hero doesn't know teaches the base skill instead; a skill tied to a weapon type
   can only go in the loadout when the hero holds that weapon. Speeds stay as they are today (hero bases 90 / 95 /
   95 for Uzuki / Haiden / Kristela, plus the class modifier: Archer +5, Paladin -5, Fencer +5, Monk +5).
3. A hero's kit becomes the hero's own: today it is read from the shared `ActorDefinition` everywhere, so it moves
   onto the hero's saved progress first. The loadout is 3 skills + 1 ultimate from the hero's pool, with at most
   one Quick skill (two with the Monk's Windwalker mastery), changed between runs only.
4. Saves get a new version with a migration: an existing hero gets points equal to their level, with tier 1 of
   their class spent and the rest free.
5. Respec: free for now (every hero is below level 20): unlearning a class returns its points. The relearning rule
   from level 20 comes in part 2.
6. The skill-tree screen, reachable between runs: per hero, the classes with their tiers, the tree of spheres for
   one class (three paths side by side, rows for tiers 5 to 25, lines between them, an info panel, the points left),
   the loadout, and unlearn. Rows that have no content yet show as locked. It works with touch, keyboard and gamepad.
   Round icons build on the UI kit's round buttons; every skill needs an icon drawn (ART.md, "Later").
7. Content: Kristela's Fencer kit first (changed on 2026-10-05, after this part had started: "Starting kits"
   above; her weapon and look become the Piercer Blade, and the Monk's kit stays in the code as that class's base
   kit), then tier 5 of Archer, Paladin and Fencer, then tier 10 ("Starting class content"). The Monk's tiers wait
   until something brings the class into play. Each new skill needs its rule, its AI rule (the autopilot and the
   partners are the balance report's players), an animation and an icon.
8. Balance: the report plays sensible default builds, and the numbers go back to the targets in CLAUDE.md.

**Part 1, first half: built on 2026-10-04 and 05** (main 76904b9; GAME_PLAN.md, "Progress", has the details).
Done: step 1, and steps 2 to 5 as rules (points, tiers, milestones with three options, the hero's own pool and
loadout, save version 2 with its migration, free respec); Kristela's class is the Fencer in the catalog and in
saves. Nothing a player sees has changed yet, apart from the healing and the notes. What the build session settled
where this spec left room:
- **Healing between fights:** a heal is used when at least half of it goes to use; the Auto leader waits at most
  6 turns in a row without a heal landing; a hurt hero with no heal of its own walks to the healer. By the build
  session's report it didn't move the win rate (8 of 200 fresh runs, 9 before), so nothing was re-tuned.
- **Waiting notes:** also for "run to safety" and for the Auto leader resting; two or three lines take turns.
- **Weapon ties:** shots and Volley need a bow; Divine Strike and Shoulder Bash a sword; the Monk's strikes fists;
  Hunter's Mark, Heal, Ki Heal and the aura nothing. Swords are one family, so a hero who holds any sword and has
  learned Fencer skills can use them, Haiden included. Peter, 2026-10-05: yes, "Haiden can later multi class. The
  skills can somehow combine or replace when multi classing"; how they combine is decided with the advanced
  classes.
- **Picks:** a milestone pick is final until the class is unlearned. A hero can unlearn even its own class and
  keeps its starting kit. A newly learned skill goes into the loadout only when a slot is free.
- **Speed** with two classes at the same tier: the hero's own class counts, then the one learned first.
- The real classes give **no stat bump yet**: it comes with the content, so that this half changed nothing in play.

**Left of part 1** (2026-10-10): tiers 5 and 10 of Archer, Paladin and Fencer with default builds for `-balance`
and the re-tune (steps 7 and 8). Kristela's Fencer kit with the class stat bumps was built on 2026-10-05, the
skill-tree screen on 2026-10-10.

**Step 6 as built (2026-10-10): the skill-tree screen** (`SkillTreeModel`, `TreeText`, `SkillTreeScreen`). What the
build session settled where the spec left room; Peter had the mock-ups and has not answered on the look yet.
- **One screen.** On top a tab per hero (level, free points), the points left and Close. On the left the classes
  with their tiers (Archer, Paladin, Fencer, Monk: any hero can put points into any of them), the loadout (three
  skills and the ultimate) and Unlearn. In the middle the tree of the class the cursor chose: three paths side by
  side in blue, green and purple, rows for tiers 5, 10, 15, 20 and 25, and on its left a track with a mark per
  tier (gold: reached, white: next), so the tiers between milestones have a place. On the right an info panel
  for whatever the cursor is on, with one button.
- **A sphere's states:** picked (gold ring, a tick), can be picked now (it glows), further down (dimmed), not
  taken (grey: another option of its row was picked), not written yet (a lock; the row says so). A green arrow
  marks an option that upgrades a skill.
- **The cursor is the selection.** A tap puts it on a class, a sphere, a loadout slot or a skill in a list; the
  info panel's button presses. With keys or a controller the arrows, WASD, the D-pad or the stick move it, Enter,
  Space or A press, Q and E or L1 and R1 switch hero, Esc, B or Start step back and then close.
- **Raising.** On a class the button raises it a tier for a point, at once and without a question. When the next
  tier is a milestone the button leads to its row, and the pick is the press on one of its three spheres; a pick
  stays until the class is unlearned, and the panel says so before. Unlearning asks first (Cancel is marked).
- **What the info panel says:** for a class, what every tier gives and what that comes to, a note when its skills
  need a weapon the hero doesn't hold, and its speed modifier; when a tier would make another class the hero's
  highest, what that does to SPD (the highest class sets the modifier: Uzuki with more Paladin than Archer drops
  from 95 to 85). For an option, its sentence, then the skill as the hero has it now and as it would be, written
  from the hero's own copy (`SkillText.Describe`); for an upgrade of a skill the hero doesn't know, that it
  teaches the skill instead. For a loadout slot, the skill in it and the list of the hero's other skills, each
  with why it can't go there (another weapon, a second Quick skill) or what happens if it does (trades places,
  or the old one leaves the loadout).
- **Locked rows.** Every milestone row is unwritten today: three locks, "Not written yet", and the panel explains
  that the class stops at tier 4 for now and that points keep or can go into another class.
- **Where it opens.** The end panel has a Skills button (K, the controller's Y) and lists who has points to
  spend: there a build can change, and every change is saved at once. The pause menu's Hero stats page has a
  Skill tree button: during a run the screen only shows, and says that a build changes between runs. `-fk-tree
  [hero]` opens it before the first run, for screenshots and tests.
- **Balance.** Points can go into tiers 1 to 4 of several classes, each with its stat bumps; `-balance` still
  plays tier 1 of the hero's own class only, and nothing was re-tuned (default builds come with tiers 5 and 10).
- **Not built:** a layout for screens narrower than 16:10; scrolling a long list of skills by touch (it pages
  with "more" past four); the round, glowing look for the HUD's own skill buttons (ART.md, "Later").

**Part 2 (1g-2):** tiers 15, 20 and 25 of the three classes (alternate ultimates, masteries); relearning after a
respec from level 20; prerequisites and kingdom locks for advanced and inherited classes.

**Part 3 (1g-3):** the three professions: potions and meals as items the party carries, the Alchemist's flasks, the
Blacksmith's enhancements, the Chef's dishes and stock; farm paths shown but locked.

## Class list (first draft)
Each kingdom's classes match its flavor, so players know what style they're building toward. Prerequisites marked
*TBD* are still to design.

### Medieval Realm (5th): the classic classes, faith
- **Base:** **Warrior** (offensive melee), **Knight** (shield and protection), **Mage** (elemental magic), **Archer**
  (ranged physical), **Cleric** (faith healing, support), **Paladin** (holy warrior: armored melee and smites, with
  light healing), **Rogue** (crit, bleed/poison, mobility), **Fencer** (a light blade: flurries, lunges and
  counters; Kristela's base class), **Monk** (fast unarmed martial arts, multi-hit).
- **Advanced (affinity):** **Holy Knight** (Knight 15 + Cleric 10): tank-healer with faith shields. **Spellblade**
  (Warrior 15 + Mage 10): magic-infused melee. **Ranger** (Archer 15 + Hunter 10): traps, partner synergy.
  **Archmage** (Mage 25 + Academy Teacher 10): big area spells.
- **Inherited:** **Crystal Ice Legion Hunter** (Archer 15 + Mage 10 with an Ice skill picked): Uzuki's class. Ice
  Hunter Bow attacks, summons ice avatars. **Princess Timeless Fencer** (*proposed* name and base since Kristela
  became a Fencer; it was Princess Timeless Monk. *Sketch*: Fencer 15 + Mage 10 with a Darkness (time) skill
  picked): Kristela's class. Time magic for a speed build: turn advances and flurries.

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
  **an ultimate at tier 20** (a new one, an upgrade or an alternate, exactly like classes: e.g. a Chef's banquet
  that buffs the party, a Blacksmith's armor-shattering strike), and a major boost at tier 25.
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
| **Chef** | Meals that buff the party in dungeon runs, eaten between fights; they heal a little, always less than a potion |
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

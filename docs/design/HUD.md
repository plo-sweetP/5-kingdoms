# HUD and menus (design, 2026-10-05)

Status: **Peter's notes from his first runs on the tablet (2026-10-04), not built.** They are one build task,
"playtest pass 1" (see "Building it"). Items marked *proposed* are the planning hub's defaults for details he didn't
give; the build session shows him screenshots before they are final.

## The layout today (phone, touch)
Top left: the party cards, and under them the turn order. Top centre: the floor banner. Top right: Wait and Berry
side by side, with Auto under the Berry button. Bottom left: the D-pad. Bottom centre: the message log. Right: the
red attack button with the three skills and the ultimate in an arc above and beside it, reaching up to the middle of
the screen.

## Top-right buttons in one row
Peter: "make the top right icons into a row to look prettier."
- Wait, Berry, Auto and the new Pause button sit in **one row** along the top edge, right-aligned, with the same
  height and spacing, and Pause at the right end.
- *Proposed:* they become icon buttons (an hourglass, the berry with its count, AUTO with a lit state, a pause
  sign) so the row stays short. The Descend button keeps appearing only on the way down.

## Minimap
Peter: "a transparent mini-map that is on the top right hand side. The mini map should follow the 'map fog' rule
where it shows the player location and slowly reveal each room and corridor as the player explores. Small icons to
symbolize the hero character position and red for enemy position. Stairs down and yellow color for items on the
ground. Map should be big enough to see but small enough to not clutter the whole UI as a whole."
- **Where:** top right, under the button row. See-through: the dungeon shows through it. *Proposed:* the explored
  floor at about 60% opacity on a dark veil of about 35%, with no frame art.
- **Fog:** it shows only what the party has explored on this floor. A room appears as a whole when a hero steps
  into it or onto its doorway; a corridor appears a stretch at a time as the party walks it (*proposed:* the tiles
  within 2 steps of a hero). What was explored stays shown. A new floor starts empty.
- **Marks:** the hero the player controls (*proposed:* white, with a slow blink), the partners (*proposed:* blue),
  enemies **red**, items on the ground **yellow**, the way down as its own small icon. *Proposed:* enemies show only
  while the party can see them (in the same room as a hero, or within sight range in a corridor); items and the
  way down show once their tile is explored, and stay.
- **Size:** the whole floor (56 x 32 tiles) at a whole number of screen pixels per tile. *Proposed:* 6 px a tile
  on a 1080p phone (336 x 192, about a seventh of the screen's width); the build session shows Peter that and one
  size up.
- The explored tiles are **Core state** (kept with the floor in `DungeonRun`, no Unity types), so tests cover the
  fog rules, and a full-screen map or monsters that hide in the fog can use them later. The main view doesn't
  change: no fog is drawn over the dungeon itself.
- It takes no input for now (*proposed:* later a tap opens a larger map). It has the same place in the keyboard
  and gamepad layouts.

## Skills in a row along the bottom
Peter: "bc of the mini map, move the skills on the bottom in a row."
- The attack button, the three skills and the ultimate form **one row along the bottom edge**, right-aligned, so
  nothing on the right reaches up into the minimap. *Proposed order,* left to right: ultimate, skill 3, skill 2,
  skill 1, attack (the largest, in the corner under the right thumb).
- The message log moves up or left as far as needed so the row doesn't cover it. The keyboard and gamepad layouts
  already use a row and keep theirs.
- The build session checks the reach on Peter's two devices (S22 Ultra, Tab S8+) and shows him the phone and the
  tablet layout.

## Pause button and menu
Peter: "add a pause/menu button that opens the pause menu. There let the player see the resume, restart, settings,
hero stats shred option, and exit (exit will later be like 'return to farm?' mechanics later)", and "add a 'reset
level' for testing purposes. We can hide it or remove it later. The current build just keeping building up the
levels."
- The Pause button ends the top-right row; Esc and the gamepad's Start open the menu too. While it is open nothing
  acts, Auto included.
- **Resume.**
- **Restart:** the run starts again on the first floor. It asks first. *Proposed:* the EXP earned so far is kept,
  as after a defeat.
- **Settings.** *Proposed first set:* the camera while aiming (step out when needed, or always wide: ART.md's
  options B and C) and the minimap (on or off, small or large). Sound comes with audio.
- **Hero stats** (Peter, 2026-10-05: "a stats screen. It is per hero that list out the hero gear, stat attributes
  and list of skills. So they can read the skills description and ultimates"): a read-only page per hero with the
  gear (the weapon now, the other slots from 1h), level and EXP, HP, ATK, DEF, SPD, Crit Rate and Crit DMG, and
  the skills and the ultimate with their descriptions; from 1g also the class and its tiers. Changing a build
  stays between runs.
- **Exit:** leaves the run. It asks first; EXP is kept. Until the farm exists it ends the run with the usual end
  panel; later it becomes "Return to the farm?".
- **Reset level** (testing only and labelled so; to be hidden or removed later): every hero goes back to level 1
  with no EXP (from 1g also: no points spent beyond the starting tier), and the run restarts. It asks first.

## Auto
Peter: "if auto play is enabled. Please hide the dpad and grey out the skills option."
- While Auto is on, the **D-pad is hidden**, and the attack, skill, ultimate, Wait and Berry buttons are **greyed
  out and don't respond**. Auto, Pause, the party cards and the tactics badges still do. Switching Auto off brings
  everything back.
- This replaces the rule of 2026-10-02 that skills stay usable by hand while Auto plays (GAME_PLAN.md, "Progress",
  milestone 1b). Peter confirmed it on 2026-10-05: "Auto blocks the skills".

## Building it (playtest pass 1)
One build session, after 1g part 1: both change the HUD, so not at the same time.
1. What moves: the top-right row with the Pause button, the skill row, Auto's look.
2. The minimap, with the explored tiles in Core and tests for the fog rules.
3. The pause menu with its pages, and Reset level.
4. Two small art items: Divine Strike's holy light (ART.md, "Skill animations"), and the guard of Kristela's
   blade on its outer side only (ART.md, "Heroes built from layers").
5. Footing in a boss fight for the party's AI (PROGRESSION.md, "Ranged vs melee"): measured first, as with the
   corridor tactics.

Checks: every layout (touch, keyboard, gamepad) at phone and tablet sizes, on screenshots at 2340 x 1080 as well as
in the usual windows, and a new APK installed on the tablet over adb.

# HUD and menus (design, 2026-10-05)

Status: **Peter's notes from his first runs on the tablet (2026-10-04), built in part.** They are one build task,
"playtest pass 1" (see "Building it", which says what is built). Items marked *proposed* are the planning hub's
defaults for details he didn't give; the build session shows him screenshots before they are final.

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
- **Built 2026-10-05 (7772829)**, with Small (6 px a tile) as the default and Small, Large, Off in Settings; the
  way down is a small green "V". Peter on the screenshots: "Looks good so far", and "Let the dot for the hero and
  each party member be a yellow or orange to better see it". So the whole party is marked in **orange** (the hero
  the player controls keeps the blink), which also settles the white mark that was hard to see; items stay yellow.
  If orange and yellow read too alike at this size, the session shows Peter the choice instead of guessing.
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

**Reverted by Peter on 2026-10-05, after seeing the row as built (feeea98):** "For the skills and ultimate. Pls
revert and go back to the original design. I don't like the row anymore. I like the more reachable orientation u had
before." So the attack button, the three skills and the ultimate go back to the arc at the bottom right, as before
playtest pass 1. The top-right row, Auto's look and the pause menu stay. The session that reverts it checks that
the arc's top stays clear of the minimap (small by default) on the phone and the tablet and shows Peter both; the
log, the aiming prompt and the Descend button go back to their old places where the arc allows it. The bullets
below describe the row and no longer apply.
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
- **Settings.** The view (Near or Far, see "The view on the tablet") and the minimap (small, large or off).
  Sound comes with audio. (The first set had "Camera while aiming: steps out when needed / always wide" in the
  view's place; Far took it over on 2026-10-10: "always wide" was the same view one zoom step out.)
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

## The view on the tablet
Peter, 2026-10-10, on the tablet layout: "for the tablet, pls zoom out a bit more. the characters look a bit too
big". The camera only zooms in whole steps (ART.md, "Rules for all art"), so on a tablet-sized screen the view
goes **one whole zoom step further out** than the rule picks today, and the aiming camera's step-out works from
there.

The numbers (the build session's arithmetic, 2026-10-10): the rule picks the whole zoom nearest to 11 tiles high
(`BaseZoomFor`). On the Tab S8+ (2800 x 1752) that is zoom 2: 13.7 tiles high, a tile about 12 mm, heroes 42%
larger on the glass than on his phone. One step out is zoom 1: 27.4 tiles high, a tile about 6 mm, heroes 29%
smaller than on the phone, almost the whole floor in view. Nothing in between exists.

**Decided by Peter on 2026-10-10: both views, as a choice in Settings** ("build both zoom levels into the
settings"), so he compares them on the tablet itself.
- Settings gets **View: Near / Far**. Near is what the rule picks today. Far is one whole zoom step further out.
  *Proposed:* Near stays the default until Peter has compared them; then he picks the tablet's default.
- *Proposed:* the choice is offered on every screen where Far exists, the phone included (there Far is zoom 1,
  about 17 tiles high). Where the rule already picks zoom 1 there is no step further out: the choice is greyed out.
- It applies at once when it is changed in the pause menu, and it is kept with the other settings (PlayerPrefs,
  only with the real save).
- The camera while aiming works from the chosen view: from Near it steps out as today; from zoom 1 there is no
  further step, so it only slides as far as it must. Never a fraction.
- The HUD doesn't change with the view: buttons, the minimap and the party cards keep their size. What is drawn in
  the dungeon (damage numbers, the words over a hero, status icons, the aiming highlight) gets smaller with it, so
  the build session checks that it can still be read in Far and shows Peter screenshots of both views in the
  tablet's shape.
- A launch flag picks the view for screenshots and tests, and the log's "View: W x H at zoom N, T tiles high" line
  says which one is on.

**As built (2026-10-10, playtest pass 1, part two):**
- Settings has **View: Near / Far** (`PixelCamera.Far`). It took the place of "Camera while aiming: steps out when
  needed / always wide": that second choice was this same view one step out, so two settings would have said one
  thing. A device that had "always wide" on starts in Far.
- The choice is offered wherever the Near view plays above 1x (the phone and the tablet: 2x Near, 1x Far). Where
  the rule already picks 1x the button reads Near and is greyed out.
- Near is the default. The choice is kept in PlayerPrefs (`fk.view.far`), with the real save only.
- While aiming, the camera works from the chosen view: from 2x it steps out a whole step when the targets don't
  fit, from 1x it only slides, as far as it must.
- The HUD is the same size in both views. In the dungeon, the damage numbers and the words over a hero are drawn
  by the HUD, so they keep their size too; the sprites, the status icons over the heads and the aiming highlight
  are half as large in Far.
- `-fk-zoom near|far` picks the view at launch, and the log says "View: 2800 x 1752 at zoom 1, 27.4 tiles high
  (Far)." (or "(Near)", or "(Near, the only view on this screen)").

**Peter on the Near and Far screenshots, 2026-10-10 (built the same day, 8ab5327; "As built" follows the two points):**
- "default for tablet can be far". So **Far is the default on a tablet**; Near stays the default on a phone and on
  PC. *Proposed* rule for what counts as a tablet: a touch device (Android, iOS) on which Near would draw a tile
  larger than about 10 mm on the glass, going by the screen's reported dpi (the Tab S8+: about 12 mm; his phone:
  about 8.6 mm). When the dpi isn't reported, Near. The default only counts until the player has picked a view in
  Settings; a picked view is kept.
- "numbers should match the ratio so it should be smaller? please make it match enough". So the damage numbers and
  the words over a hero **scale with the view**: in Far they are drawn smaller, in about the same proportion to a
  hero as in Near. *Proposed:* half the Near size where Far is half the zoom, but never below a size that still
  reads easily on that device (the build session picks the floor and shows Peter Far in the tablet's shape and on
  the phone). The message log and the HUD keep their size.

**As built (2026-10-10, 8ab5327):**
- A tablet starts in Far, a phone and a PC in Near, until the player picks a view in Settings; a picked view is
  kept (the old "always wide" setting, switched on, counts as Far). What a tablet is: a touch screen at least
  600 dp (1/160 inch by the reported dpi, about 95 mm) on its shorter side, Android's own line between phones
  and tablets (`PixelCamera.ScreenKindFor`). The proposed tile size was not used: by Android's density setting,
  which Unity may report instead of the panel's dpi, the Tab S8+ comes to about 10.2 mm, right on the line; by
  the shorter side it is 820 to 1050 dp and the phone 350 to 460 either way. No dpi reported: a phone, so Near.
  Not checked on the tablet (none was connected); the log's "Screen: N dpi, Tablet; starts in Far (this screen's
  default)" line says what a device reported.
- The numbers and words over the actors scale with the view (`PixelCamera.WorldTextScale`): half the size where
  Far is half the zoom, two thirds where it is two thirds. The floor: on a phone never below three quarters of
  the Near size, which keeps its smallest words about 1.5 mm high; a tablet and a monitor draw them more than
  twice as large and need none. Their outline thins with them. The message log and the HUD keep their size.
- `-fk-device desktop|phone|tablet` shows a device's looks in a PC window. Screenshots of both views in the
  phone's size and the tablet's shape: `C:\Users\peter\5Kingdoms\results\v1\view`.

## The skill-tree screen (built 2026-10-10)
The spec and what was settled are in PROGRESSION.md ("Building 1g", "Step 6 as built"); the art in ART.md. For the
HUD it means:
- The end panel has two buttons, Try Again and **Skills** (K on the keyboard, Y on a controller), and a line that
  says who has points to spend. The pause menu's Hero stats page has a **Skill tree** button; from there the
  screen only shows.
- The screen covers everything and is laid out in the canvas's units from the width it gets: a block of at most
  2012 units in the middle (380 for the classes and the loadout, 480 to 600 for the info panel, the tree between
  them), inside the safe area. It fits the phone (2340 x 1080) and the tablet (2800 x 1752); a 4:3 screen is too
  narrow for it as it is.
- Its art sits on whole screen pixels like the rest of the HUD (`UiFactory`, `UiArtScaler`); the spheres are 44
  art pixels across, so 88 screen pixels on a 1080p phone and 132 on the tablet.
- Touch: a tap puts the cursor (a gold box) on something, the button in the info panel presses. Keys and a
  controller move the cursor and press on it; the left column says which keys.

## Building it (playtest pass 1)
One build session, after 1g part 1: both change the HUD, so not at the same time.
1. What moves: the top-right row with the Pause button, the skill row, Auto's look.
2. The minimap, with the explored tiles in Core and tests for the fog rules.
3. The pause menu with its pages, and Reset level.
4. Two small art items: Divine Strike's holy light (ART.md, "Skill animations"), and the guard of Kristela's
   blade on its outer side only (ART.md, "Heroes built from layers").
5. Footing in a boss fight for the party's AI (PROGRESSION.md, "Ranged vs melee"): measured first, as with the
   corridor tactics.
6. The view: Near and Far in Settings ("The view on the tablet").

Steps 1 to 4 are built and on main (2026-10-10, 1f8db05; GAME_PLAN.md, "Progress"). Steps 5 and 6 were built by a
second session the same day (PROGRESSION.md, "Footing in a boss fight"; "The view on the tablet" above).

Checks: every layout (touch, keyboard, gamepad) at phone and tablet sizes, on screenshots at 2340 x 1080 as well as
in the usual windows, and a new APK installed on the tablet over adb.

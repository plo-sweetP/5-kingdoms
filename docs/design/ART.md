# Art direction: the Tiny Swords pack (design, 2026-10-03)

Status: **decided by Peter on 2026-10-03; art pass 1 was built and pushed on 2026-10-04** (main 4d75ffa; what it
contains is in GAME_PLAN.md, "Progress", and in the Drive release notes). Items marked *proposed* are the
planning hub's defaults that Peter has seen and not objected to; a build session may change them where the pack forces
it, and says so in its report.

## Decision
- The game's art is the **Tiny Swords** pack by Pixel Frog (https://pixelfrog-assets.itch.io/tiny-swords). It replaces
  the code-generated placeholders from `Tools/pixelart/make_sprites.py`.
- Why now: Peter wants the game to look better before more systems are built on top of placeholder art. He first
  asked for a "modernized SNES JRPG" look (Sea of Stars screenshots), then found this pack and chose it.
- The same art serves the dungeon and the HUD now, and the main menu and the farm when they're built (the pack's
  houses, trees, sheep and tool-carrying pawns suit the farm).
- Sessions have no image generator: art is drawn or edited by code. Recolors, layer swaps, simple icons and tiles in
  the pack's palette are realistic. New animated characters at the pack's quality are not.

## The pack
- **Where it is** (outside the repo): `C:\Users\peter\5Kingdoms\ArtPacks\TinySwords\` (an ordinary folder since
  2026-10-04; the Claude app keeps `%LOCALAPPDATA%` private to its sessions), with the original zips and three
  unpacked folders: `Tiny Swords (Free Pack)`, `Tiny Swords (Enemy Pack)` (bought by Peter on 2026-10-03) and
  `Tiny Swords (Update 010)` (the old version, see below).
- **License** (store page, read 2026-10-03): free to use in personal and commercial projects, and the assets may be
  modified. The asset files themselves may not be redistributed, resold or repackaged, even modified. Credit is not
  required. What follows for us:
  - Shipping the art inside the game is fine, also when the game is sold.
  - The repo stays private. If it ever goes public, the pack's files (modified or not) must come out first.
  - We credit "Tiny Swords by Pixel Frog" anyway, in `docs/THIRD_PARTY.md` and later on a credits screen.
  - These terms cover the Free Pack and the Enemy Pack. The old version (`Tiny Swords (Update 010)`) is offered on
    the same page as CC0-licensed, so it has no such limits.
- **Format:** a 64 px tile grid; animations run at 10 fps; every unit faces right (the game already mirrors sprites
  for left, and up or down keeps the last facing).
- **Free pack contents:**
  - Units in five colors (blue, red, purple, yellow, black), as horizontal strips of 192 px frames: **Warrior**
    (idle 8, run 6, two attacks of 4, guard 6), **Archer** (idle 6, run 4, shoot 8, a 64 px arrow), **Monk**, a robed
    healer (idle 6, run 4, heal 11, heal effect 11), **Pawn** (idle 8, run 6, and versions holding wood, meat, gold,
    a hammer, an axe, a knife or a pickaxe, with "interact" strips for the tools), **Lancer** (320 px frames: idle 12,
    run 6, attack and defence in five directions).
  - Terrain: `Tilemap_color1-5.png` (9 x 6 tiles each: flat ground and raised ground with cliff faces, in five
    palettes), water with animated foam, a shadow tile. The author has a "Tilemap Guide" devlog on the store page;
    read it before building the tile mapping.
  - Decorations and resources: bushes and trees (animated), stumps, rocks, rocks in water, clouds, sheep (idle, move,
    graze), gold stones, wood, meat, tools.
  - Buildings in five colors: castle, three houses, tower, monastery, archery, barracks.
  - Effects: two dust puffs, two explosions, three fires, a water splash.
  - UI kit: big, small and tiny buttons (blue and red, regular and pressed), paper panels, ribbons, banners, a wood
    table, bars with fills, 12 icons, 25 avatars (unit portraits), cursors.
- **Layered sources.** The Blue units' `.aseprite` files keep separate layers: Warrior (shadow, sword, shield, body,
  head, effects), Archer (shadow, quiver, bow, body, head, hand, arrow), Monk (shadow, feet, body, hands, head,
  effects), Pawn (shadow, body, head, hands, held object, effect), Lancer (shadow, arm, body, spear, head, effect).
  This is what makes equipment that changes the sprite possible (see "Heroes built from layers"). The Enemy Pack's
  monsters have layered sources too.
- **Not in the free pack:** monsters, cave or dungeon-interior tiles, stairs, item icons, a font.
- **Enemy Pack** (paid, same store page; the author plans 30 enemies): 22 monsters, each with an idle, a move and an
  attack strip, a 256 px avatar and a layered `.aseprite` source: Bear, Bomb Fish, Bumblebee, Giant Bat, Gnoll, Gnome,
  Harpoon Shark, Hex Shaman, Imp, Lizard, Minotaur, Paddle Shark, Panda, Skull, Slingshot Gnome, Snake, Spear Goblin,
  Spider, Thief, Torch Goblin, Troll, Turtle. Most use 192 px frames; the Bear, Panda and Spear Goblin 256, the
  Minotaur and Turtle 320, the Troll 384. No slime.
  - The **Troll** is the boss: idle 12, walk 10, **wind-up 5, attack 6, recovery 10**, and a death animation with
    its club breaking (in `Extra/Troll Dead`).
  - `Extra/` also has a **cave entrance** (8 frames, eyes glowing inside), bones and skull spikes, a dead tree, a
    wooden fence (64 px tiles), guard and hit strips for some monsters, huts, a pig and a pig rider, boats and a
    cannon.
- **Old version** (`Tiny Swords (Update 010)`, CC0): the earlier drawing of the same world, in four colors: Warrior,
  Archer and Pawn on one sheet each (the Warrior's attacks are also drawn facing up and down), goblins (Torch, TNT,
  Barrel), buildings with construction and destroyed states, a gold mine, a bridge, 30 UI icons. Close to the new
  packs in style but not identical, so it's a spare parts box: use a piece only where it doesn't clash.

## Rules for all art
Peter wants the sprites "as crisp as possible" and the app as small as possible.
- **64 px = 1 tile = 1 world unit.** Point filter, no compression, no mipmaps, clamp. One importer enforces it
  (`Assets/Editor/PixelArtImporter.cs`, which did the same at 32 px for the placeholders), including slicing the
  animation strips, so dropping a file in is enough.
- **Whole-number zoom only** for the world, as `PixelCamera` does today. Sprites are never scaled by fractions, and UI
  pixel art is drawn at whole-number scales too, so no pixel is blurred or uneven. Text may be smooth.
- **Only what the game uses enters the project.** A script copies the needed files from the pack folder into
  `Assets/Art/` under our own names (and builds what we derive: recolors, 9-slice panels, cropped portraits), so it
  doubles as the list of what we use and can be re-run when the pack updates. Nothing else from the pack goes into
  `Assets/`; everything under a `Resources` folder ships in the app.
- Code-drawn extras (item icons, status icons, reticles, the snare, stairs if needed) are drawn at the pack's scale
  and from the pack's palette.
- Core rules don't change in an art pass: balance numbers, tests and the golden replay stay as they are.

## Art pass 1 (the current game on the new art, with hero sprites and equipment)
Scope: the dungeon and its HUD (there is no main menu or farm yet), and **the hero sprites with their equipment**.
Peter, 2026-10-03: "I want u to get through the hero sprites and equipment in this next session. Generate the
remaining weapons and armour as needed but follow the same art style as closely as possible." New bosses and
creatures beyond the pack come later.

### Heroes built from layers
- A hero is drawn from **layers** taken from the pack's `.aseprite` sources (shadow, back items, body, head, hands,
  weapon, shield, front items, effects), not from the flattened strips, so each layer can be swapped or recolored on
  its own. The sources exist for Blue only; the other four colors show how the pack recolors a unit.
- *Proposed:* **the weapon type picks the rig**, as in the pack, so the stance and the attack animation always fit
  the weapon: sword (with or without a shield) = Warrior rig, bow = Archer rig, spear = Lancer rig, staff = Monk
  rig, fists and tools = Pawn rig (its tool swings without the tool read as punches) or Monk rig, whichever looks
  better.
- **The heroes stay recognizable under their gear** (Peter, 2026-10-03): they keep the pack's helmets and hoods,
  which are their head armor, but each hero's **face and some hair show from under them**, so two heroes wearing
  the same equipment can still be told apart. Each hero has their own head (face, eyes, hair), drawn to follow the
  rig's head frame by frame; a head piece is drawn over it and leaves the face and part of the hair showing; with
  no head piece the whole hairstyle shows.
  - Uzuki: spiky blue hair, cyan eyes, one pauldron. Haiden: spiky orange-red hair, red eyes, and **no headband**
    (Peter, 2026-10-04, on the first previews: "U can just remove it and keep his orange red hair. The blue ribbon
    sticking out at the end is uneeded"; the concept sketch's headband can come back as a head piece). Kristela:
    long wavy blonde hair, blue eyes, a gold X-shaped clip on one side. Concept sketches are in `docs/concept/`; the
    Pawn shows the pack's way of drawing a face.
  - Eyes are drawn larger than the pack's plain dots, a dark dot over the hero's eye color, so the color reads
    (approved 2026-10-04).
  - Starting looks (approved 2026-10-04): Haiden on the Warrior rig with the Long Sword and shield, in Heavy Armor
    (the great helm); Kristela with Gauntlets, in Light Warrior (the crested cap); Uzuki on the Archer rig with the
    Hunter Bow, in Archer's Garb (the archer helmet).
- **Female heroes read as feminine, in any armor and with any weapon** (Peter, 2026-10-03, pointing at his chibi
  class sheet): for example long hair falling from under the helmet, lashes, a slimmer or skirted outline, softer
  trim, hair accessories. Kristela is the female hero today; Haiden and Uzuki are boys (Peter, 2026-10-03; older
  comments in the code say "she" for Uzuki and need correcting). For Kristela, Peter chose "B for now" from three
  previews (2026-10-04): long hair, lashes, blush and her clip, plus a pleated skirt on every body.
- Idle and run play from the rig; attacks, shots, guards and heals use the rig's strips where one fits, and the
  code-driven lunge and impact effects where none does. Hit flash, hit-stop, knockback, screen shake and the death
  animation keep working, and animations must not slow the turns down.

### Equipment that shows on the sprite
The gear rules (GEAR.md) aren't built yet (milestone 1h); this pass makes the **looks** and the way to set them.
- **Weapons:** a look for each of GEAR.md's nine: Long Sword, Great Sword, Arcane Sword, Piercer Blade, Dual Blades
  (a second blade where the shield is), Great Shield, Mage Staff, Hunter Bow, Gauntlets. The pack gives the sword,
  shield, bow and spear, and the Pawn's hammer, axe, knife and pickaxe; the rest are made from those by reshaping
  and recoloring, frame by frame, in the pack's palette, outline and shading.
- **Armor:** a look for each of GEAR.md's nine sets (Archer's Garb, Light Warrior, Mage Robe, Heavy Armor, Duelist's
  Leathers, Pilgrim's Vestments, Windrider's Cloak, Shadowstalker, Bloodrage Hide). The head piece and the body
  piece carry the look (a helmet, hood or hat over the hero's hair with the face still showing; the body's colors
  and trim); hands and feet are a few pixels at this size, so they show as color accents. Peter reviewed the nine
  head pieces on 2026-10-04 ("good so far"): the archer helmet loses the metal piece that hangs in front of the
  eyes. The preview sheets are in `docs/screenshots/2026-10-04-art-pass-1/`.
- **More head pieces, for variety** (Peter, 2026-10-04: "Bow, crown, head band in addition to what you currently
  have"): a hair bow, a crown and a headband, as cosmetic head pieces on top of the nine sets, generated in the
  pack's style. Built in art pass 1 (`hair_bow`, `crown`, `headband`): one is worn instead of the armor set's
  head piece.
- **Rings show as a sparkle and a flash, one color per ring set** (decided 2026-10-04): a tiny twinkle in the set's
  color near the hero's hand every few seconds, and a short pulse of that color on the hero when the set's bonus
  fires (the bonuses themselves come with 1h). The first preview used a glow disc on the ground under the hero;
  Peter found it "a bit much", and the ground is where the game shows what the player has to read (Aura of
  Protection, reach highlights, warning tiles), so nothing permanent goes there.
- A hero's look is **data**: which rig, head, weapon, armor pieces and aura. Until gear exists, each hero has a
  default look, a launch flag overrides it for trying things out, and 1h later sets it from the equipped items.
- Every weapon and armor piece also gets a 64 px **icon** for the gear and hero screens to come.
- **Preview sheets for Peter:** a tool renders each hero with every weapon and armor set into contact sheets. He
  reviews them on his phone (the hub forwards them) before the looks are final. Generated pieces will be simpler than
  the pack's hand-drawn art; the aim is that they don't stand out next to it.

### Skill animations
Peter, 2026-10-03: "Generate the animations as best as possible to match the skill effects." Every weapon attack,
skill and ultimate gets an animation and effect that shows what it does, from the rig's strips, the pack's effects
(dust, explosions, fire, the heal effect, slash arcs in the units' effect layers) and generated ones in the same
style. They stay short, so turns don't slow down. As a guide:
- **Uzuki:** Quick Shot looses an arrow (two with the Hunter Bow's Multishot). Hunter's Mark stamps a mark on the
  foe. Power Shot is a long draw, a heavier arrow with a trail and an impact that throws the target back. Rolling
  Shot is a tumble with dust, a shot, and the snare left where the roll began. Volley sends arrows up and rains them
  on the 3 x 3 area, once per hit.
- **Haiden:** Sword Slash swings with its arc. Heal plays the pack's heal effect on whoever is healed. Divine Strike
  is a heavy swing that bursts into fire on the target. Shoulder Bash leads with the shield and shoves the target.
  Aura of Protection spreads a golden ring over its 3 x 3 and stays as a glow, with a heal effect on each tick.
- **Kristela:** Jab is a quick punch. Piercing Punch drives a shockwave through the target into the foe behind.
  Ki Heal is a short glow on herself. Stun Strike lands with a burst and leaves stars over the target. Flurry of
  Blows is five fast punches with afterimages.
- **Monsters:** the Spider bites, the Giant Bat swoops, the Troll winds up, slams (dust and a shock on every tile it
  hits) and recovers, and roars when it calls its Spiders.
The manga panels and ultimate cutscenes in PROGRESSION.md ("Skill presentation") are still for later.

### The rest of the game
- **Monsters** (built this way; Peter's OK on the mapping and the dungeon's name is still open), from the Enemy Pack, with the stats and behavior they have today: the **Spider** replaces
  the Slime, the **Giant Bat** the Bat, and the **Troll** the King Slime. The Troll's wind-up strip is the slam's
  warning turn, its attack the slam, its recovery the turn after, and it calls two Spiders at half HP. Names change
  with the art everywhere the player sees them; the first dungeon needs a new name ("Troll's Hollow" as a working
  name, was "Slime Cave").
- **Dungeon** (built this way): it looks like the outdoors. Floors are flat ground, walls are raised ground with cliff
  faces toward the rooms, and bushes, rocks, trees, bones and skull spikes decorate the unwalkable parts without
  hiding a walkable tile. As built: the floor uses the pack's color 4 (dry ground) and the raised ground color 3
  (lush grass), so walkable ground reads at a glance. The **cave entrance** replaces the stairs (the way down to the next floor). The boss
  room's pillars need to read clearly as blocking. A cave recolor of the floors can follow once Peter has seen it.
- **Effects and items:** the pack's arrow, dust, explosions, fire and heal effect where they fit; the berry, the
  snare, reticles, reach highlights and status icons redrawn to match.
- **HUD** (built this way): the pack's buttons, panels, ribbons and bars; round buttons for the attack, skills and
  ultimate; portraits on the party cards and the turn-order strip (the heroes' own heads, the monsters' avatars).
  Layouts for touch, keyboard and gamepad on phone (19.5:9) and tablet or Steam Deck (16:10) all keep working.
- **View size (decided 2026-10-04 from screenshots):** at 64 px tiles and a crisp 2x zoom, a 1080p phone shows about
  8.5 tiles top to bottom (11 with the old art), so a foe 5 tiles straight up or down is off screen. Every valid
  target must stay visible while aiming. Peter chose **option B**: 2x normally; while aiming, if the targets don't
  all fit, the view steps out to 1x and steps back after the shot or a cancel. **Option C** (1x all the time) stays
  in the code for a player setting later ("keep C handy to allow some UI options for the player later"). Option A
  (staying at 2x and sliding the camera until the heroes sit at the screen's edge) is out: "that does look a bit
  weird".
  - As built: while aiming, the camera moves no further than it must to show every target clear of the HUD. If that
    move would take the party out of the middle third of the screen, or the targets don't fit at 2x, the view steps
    out to 1x until the aim ends. On a 1080p phone: targets up to 2 tiles up or down, or 5 to the side, need no move;
    3 up is a small nudge; 4-5 up, or 5 up and 5 down, steps out. A screen under 1056 px high already plays at 1x
    and can't step out. `-fk-view wide` is option C; `-fk-view lead` (A) is a debug flag only.
  - Not checked on the Android devices yet: the rule is tested by arithmetic for the S22 Ultra and Tab S8+ sizes and
    looked at in 1560x720, 1440x900 and 2340x1080 windows.

### Done when
The Core tests, Unity EditMode and PlayMode tests, the Windows build and the autoplay runs pass with the new art in
all layouts; no placeholder square shows; the three heroes are layered, look like themselves and can show every
weapon and armor look; and Peter has the preview sheets, screenshots and a build to try.

## Later (not in art pass 1)
- **New bosses and creatures** beyond the Enemy Pack, drawn or generated (Peter: "later").
- **More heads and rigs** for new heroes, classes and professions; Peter's chibi class sheet is the inspiration for
  how distinct they should look.
- **Skill tree screen** (with 1g): spheres like Peter's "Archer Mastery Path" reference: three paths side by side,
  rows for tiers 5, 10, 15, 20 and 25, round glowing icons in a color per path, connecting lines, an info panel and
  the points left. The same round, glowing look for the skill buttons. The reference's painted gold-vine frames need
  bought or generated art.
- **More places:** cave and other biome recolors per kingdom, the main menu, the farm.
- **Size pass:** remove Unity packages and modules the game doesn't use, strip managed code, check the build report.

## Open questions
1. Peter's OK on the monster mapping (Spider, Giant Bat, Troll) and a final name for the first dungeon.
2. The view size on 1080p phones (Peter chooses from screenshots).
3. Where the art for the gold-trimmed menu frames comes from, if Peter wants that exact look.
4. A pixel font that matches the pack (none is included).

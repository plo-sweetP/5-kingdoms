# Third-party assets

## Tiny Swords, by Pixel Frog
The game's art is built from the **Tiny Swords** packs by Pixel Frog: https://pixelfrog-assets.itch.io/tiny-swords

- **Tiny Swords (Free Pack)**: units, terrain, decorations, effects and the UI kit.
- **Tiny Swords (Enemy Pack)** (paid; bought by Peter on 2026-10-03): the Spider, the Giant Bat, the Troll, the cave
  entrance, bones and skull spikes.

**Terms** (the store page, read on 2026-10-03): the assets may be used in personal and commercial projects and may be
modified. The asset files themselves may not be redistributed, resold or repackaged, even modified. Credit is not
required; we give it anyway, here and later on a credits screen.

What that means for this repo:
- **The packs are not in the repo.** They live outside it (`%LOCALAPPDATA%\5Kingdoms\ArtPacks\TinySwords\`), and
  their raw files and `.aseprite` sources must never be committed.
- **`Assets/Art/Resources/` holds only what the game uses**, written by `Tools/pixelart/build_art.py`: files taken
  from the packs under our own names, and what is derived from them (units taken apart into layers, recolors,
  reshaped weapons, wall tiles put together from the terrain tileset, the cave entrance cut down to one tile, the UI
  kit's pieces joined into 9-slice textures). `Tools/pixelart/pack_files_used.txt` lists every pack file the build
  reads; it is rewritten on every run.
- **The repo stays private.** If it ever goes public, everything under `Assets/Art/Resources/Sprites/` and the
  screenshots that show the art come out first.
- Shipping the art inside the game is fine, also when the game is sold.

Not from the packs, drawn by the build script in the packs' palette and outline: the heroes' faces and hair, the
armor sets' head pieces (except the Archer's helmet and the Warrior's helm, which are the packs' own, changed), the
cosmetic head pieces, the Mage Staff, the gauntlets, the berry, the snare, status icons, the punch burst, the shock
ring, the ring twinkle, the HUD's slim frames, bars and rectangular buttons, the D-pad and the gear icons' bodies,
gloves, boots and rings.

The old version of the pack (`Tiny Swords (Update 010)`, offered as CC0 on the same page) is kept next to the others
as a spare parts box; nothing from it is used yet.

# 5 Kingdoms

A landscape mobile game for Android and iOS: farm by day, delve by night. Raise and breed monsters on your farm,
then take them into turn-based, Mystery Dungeon-style dungeons across five kingdoms.

**Status:** the dungeon prototype is playable with a party of three: Haiden (Paladin), Kristela (Monk) and Uzuki
(Archer) explore the generated floors of Troll's Hollow, fight Spiders and Giant Bats and face the Troll on B5F, with a
Honkai: Star Rail-style turn order in fights. Each hero has a weapon attack, three skills and an ultimate (no mana:
skills sit out a turn after use, ultimates charge up as the hero fights). The player leads one hero (Haiden to begin
with); the others fight on their own. Levels and EXP are saved between runs. See [GAME_PLAN.md](GAME_PLAN.md) for the
design and roadmap.

![Haiden aims Divine Strike at a Spider on B2F (phone layout)](docs/screenshots/2026-10-04-art-pass-1/game/01-fight.png)

More screenshots: [docs/screenshots](docs/screenshots).

The art is built from the **Tiny Swords** packs by Pixel Frog ([docs/THIRD_PARTY.md](docs/THIRD_PARTY.md)). Each hero
is stacked from layers (body, weapon, armor set, head, head piece), so what a hero carries and wears shows on the
sprite; see the [art pass sheets](docs/screenshots/2026-10-04-art-pass-1).

## Play it
1. Open the project in Unity Hub with **Unity 6.3 LTS (6000.3.25f1)**.
2. Open `Assets/Scenes/Dungeon.unity` and press Play.

| | Touch | Keyboard | Controller |
|---|---|---|---|
| Move (8 directions) | D-pad, slide for diagonals | WASD / arrows (+ two keys for diagonals), Q/E/Z/C, numpad | Left stick or D-pad |
| Weapon attack (Sword Slash, Jab, Quick Shot) | The big red button, then the target; or just tap an enemy in reach | Space, then the target (or click the enemy) | A, then the target |
| Skills | The three blue buttons, then the target | 1, 2, 3 | LB, LT, RT |
| Ultimate (when its meter is full) | The gold button, then the target | 4 | RB |
| Aim: pick the target | Tap a marked enemy, or tap the button again for the one that pulses | Arrows pick (again: the next one out that way), the same key or Space fires, Esc cancels | Stick picks, the same button or A fires, B cancels |
| Fire at once | Hold the D-pad toward an enemy, then tap the button | Hold a direction toward an enemy, press the key | Hold the stick toward an enemy, press the button |
| Switch hero | Tap a party card | Tab | B |
| Partner tactics (Attack, Follow, Hold) | Tap a partner's badge | G (all partners) | L3 (all partners) |
| Wait a turn | Wait | X | Y |
| Eat a berry (heals the leader) | Berry | B | X |
| Go down a floor | Descend (shown at the cave entrance) | Enter | Start |
| Restart after a run | Try Again | R | A or Start |
| Auto-pilot on/off | Auto | T | View |

Attacks are deliberate: walking into an enemy only turns your hero to face it, and never costs a turn. Choosing the
weapon attack, a skill or an ultimate that needs a target lights up where it reaches and marks every valid target; one
is marked to begin with (the enemy you face, else the nearest), so a second press confirms it. Melee heroes reach the
enemies next to them; Uzuki's shots reach any enemy in sight within 5 tiles, at any angle (walls and wall corners block
them, allies don't), for 90% of a melee hit, and less with an enemy right next to him. A stun pushes an enemy's next
turn back instead of skipping it, and the same enemy can't be pushed back again until it has acted (the stars over its
head). A badly hurt partner swaps back behind a healthier one. Partners stay with the leader: in a corridor or a
doorway they wait right behind whoever is fighting instead of looking for another way around, and a partner that got
cut off swaps past the one behind it. They use the doorways: a partner in front waits in a doorway for a pack to come
to it one at a time rather than step out among it, a partner in a corridor's mouth steps aside so the one behind can
come out and fight, and a fresh melee partner takes the place of a hurt one that holds a corridor (never yours: walk
into a partner to trade places yourself). The auto-pilot plays your hero the same way.
Skills can't be used two turns in a row (the button says "next turn"); Quick skills take half a turn. The gold bars on
the party cards are the ultimates' charge: it fills as each hero acts, hits and gets hit, and carries over between
fights. A dimmed button can't be used right now, and pressing it says why. While the auto-pilot plays you can't move
or act, but you can still fire skills and ultimates or switch heroes. The D-pad and attack button hide while you use a
keyboard or controller; tap or click to bring them back. Progress is saved automatically. To start over at Lv 1 use
**5 Kingdoms > Debug > Reset Save** in the editor.

While you aim, the camera keeps every target in view: it moves a little, or, when the targets are far apart, steps
out to a wider view until the aim ends. The Troll is much taller than a tile and turns see-through while someone stands
behind it.

## Art
Everything under `Assets/Art/Resources` is written by a script from the Tiny Swords packs, which are **not** in the
repo (their license allows using and changing them, not passing them on). To rebuild the art, put the packs in
`5Kingdoms\ArtPacks\TinySwords\` in your user folder (`C:\Users\<you>\5Kingdoms\ArtPacks\TinySwords\`), or pass
`--pack <folder>`, and run, with Python 3.7 or newer:
```
python Tools/pixelart/build_art.py
```
Add `--preview <folder>` for the review sheets (heroes, weapons, armor sets, head pieces, rings, animations, icons).

To try other looks in a build, start it with `-fk-look`, for example
`-fk-look "haiden=great_sword,mage_robe;kristela=hair_bow;uzuki=mage_staff,bare;all=hawks_eye"`: per hero (or `all`),
any of a weapon, an armor set, `bare` (no head piece), a cosmetic head piece (`hair_bow`, `crown`, `headband`) and a
ring set. The ids are in `Assets/Art/Resources/art_manifest.json`. Until gear exists (milestone 1h), this is the only
way to change a look.

## Tests
```
dotnet run --project Tools/CoreTests
```
Runs the game-rule tests in about two seconds (the project must have been opened in Unity once). The same tests
run in Unity's Test Runner (Window > General > Test Runner > EditMode).

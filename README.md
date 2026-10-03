# 5 Kingdoms

A landscape mobile game for Android and iOS: farm by day, delve by night. Raise and breed monsters on your farm,
then take them into turn-based, Mystery Dungeon-style dungeons across five kingdoms.

**Status:** the dungeon prototype is playable with a party of three: Uzuki (Archer), Haiden (Paladin) and Kristela
(Monk) explore the Slime Cave's generated floors and face the King Slime on B5F, with a Honkai: Star Rail-style turn
order in fights. Each hero has a weapon attack, three skills and an ultimate (no mana: skills sit out a turn after use,
ultimates charge up as the hero fights). The player leads one hero; the others fight on their own. Levels and EXP are
saved between runs. See [GAME_PLAN.md](GAME_PLAN.md) for the design and roadmap.

![Uzuki fighting a slime on B1F (phone layout, placeholder art)](docs/screenshots/2026-10-01/02-combat-critical-hit.png)

More screenshots: [docs/screenshots](docs/screenshots).

## Play it
1. Open the project in Unity Hub with **Unity 6.3 LTS (6000.3.25f1)**.
2. Open `Assets/Scenes/Dungeon.unity` and press Play.

| | Touch | Keyboard | Controller |
|---|---|---|---|
| Move (8 directions) | D-pad, slide for diagonals | WASD / arrows (+ two keys for diagonals), Q/E/Z/C, numpad | Left stick or D-pad |
| Weapon attack (Quick Shot, Sword Slash, Jab) | The big red button, or walk into an enemy | Space | A |
| Skills | The three blue buttons | 1, 2, 3 | LB, LT, RT |
| Ultimate (when its meter is full) | The gold button | 4 | RB |
| Aim: pick a target | Tap a marked target, or tap the button again for the marked one | Arrows pick, the same key or Space fires, Esc cancels | Stick picks, the same button or A fires, B cancels |
| Fire at once in a direction | Hold a D-pad direction, then tap the button | Hold a direction, press the key | Hold the stick, press the button |
| Switch hero | Tap a party card | Tab | B |
| Partner tactics (Attack, Follow, Hold) | Tap a partner's badge | G (all partners) | L3 (all partners) |
| Wait a turn | Wait | X | Y |
| Eat a berry (heals the leader) | Berry | B | X |
| Take the stairs | Descend (shown on the stairs) | Enter | Start |
| Restart after a run | Try Again | R | A or Start |
| Auto-pilot on/off | Auto | T | View |

Choosing an attack, skill or ultimate that needs a target lights up where it reaches and marks every valid target.
Skills can't be used two turns in a row (the button says "next turn"); Quick skills take half a turn. The gold bars on
the party cards are the ultimates' charge: it fills as each hero acts, hits and gets hit, and carries over between
fights. A dimmed button can't be used right now, and pressing it says why. While the auto-pilot plays you can't move
or act, but you can still fire skills and ultimates or switch heroes. The D-pad and attack button hide while you use a
keyboard or controller; tap or click to bring them back. Progress is saved automatically. To start over at Lv 1 use
**5 Kingdoms > Debug > Reset Save** in the editor.

## Tests
```
dotnet run --project Tools/CoreTests
```
Runs the game-rule tests in about two seconds (the project must have been opened in Unity once). The same tests
run in Unity's Test Runner (Window > General > Test Runner > EditMode).

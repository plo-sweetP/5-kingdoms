# 5 Kingdoms

A landscape mobile game for Android and iOS: farm by day, delve by night. Raise and breed monsters on your farm,
then take them into turn-based, Mystery Dungeon-style dungeons across five kingdoms.

**Status:** Milestone 1, the dungeon prototype, is playable: Uzuki explores 5 generated floors of the Slime Cave.
See [GAME_PLAN.md](GAME_PLAN.md) for the design and roadmap.

![Uzuki fighting a slime on B1F (phone layout, placeholder art)](docs/screenshots/2026-10-01/02-combat-critical-hit.png)

More screenshots: [docs/screenshots](docs/screenshots).

## Play it
1. Open the project in Unity Hub with **Unity 6.3 LTS (6000.3.25f1)**.
2. Open `Assets/Scenes/Dungeon.unity` and press Play.

| | Touch | Keyboard |
|---|---|---|
| Move (8 directions) | D-pad, slide for diagonals | WASD / arrows (+ two keys for diagonals), Q/E/Z/C, numpad |
| Attack | ATK, or walk into an enemy | Space |
| Wait a turn | Wait | X |
| Eat a berry (heal) | Berry | B |
| Take the stairs | Descend (shown on the stairs) | Enter |
| Restart after a run | Try Again | R |

## Tests
```
dotnet run --project Tools/CoreTests
```
Runs the game-rule tests in about two seconds (the project must have been opened in Unity once). The same tests
run in Unity's Test Runner (Window > General > Test Runner > EditMode).

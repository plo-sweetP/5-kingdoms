# 5 Kingdoms

Landscape mobile game (Android first, iOS later) in Unity 6.3 LTS (6000.3.25f1): farming + monster breeding +
Mystery Dungeon-style turn-based dungeons. Design and roadmap: GAME_PLAN.md.

## Layout
- `Assets/Scripts/Core/` — game rules in plain C# (asmdef `FiveKingdoms.Core`, `noEngineReferences`). No UnityEngine here.
  Rules change state and append `GameEvent`s; they never touch visuals.
- `Assets/Scripts/` (asmdef `FiveKingdoms.Game`) — Unity side: `Dungeon/` (controller, view, animations), `UI/` (HUD built
  in code), `Shared/` (sprites, pixel camera). Views only animate events and read state.
- `Assets/Tests/EditMode/` — NUnit tests for Core, including an autopilot soak test.
- `Assets/Art/Resources/Sprites/` — pixel art, 32 px = 1 tile, loaded by path via `SpriteLibrary`. Import settings are
  enforced by `Assets/Editor/PixelArtImporter.cs`; just drop PNGs in.
- `Tools/pixelart/make_sprites.py` — regenerates the placeholder art (Python 3, stdlib only). Replacing a PNG with
  final art (same name and size) needs no code change.
- `Tools/CoreTests/` — runs the Core tests outside Unity.

## Commands (from the repo root)
- Core tests, ~2 s: `dotnet run --project Tools/CoreTests` (add a name filter as an argument to run a subset)
- Balance report: `dotnet run --project Tools/CoreTests -- -balance`; print a floor: `-- -map <seed>`; trace the autopilot: `-- -trace <seed> <fromAction>`
- Regenerate art: `python Tools/pixelart/make_sprites.py --preview preview.png`
- Unity tests: `Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults results.xml`
- Windows build: `Unity.exe -batchmode -quit -projectPath . -executeMethod BuildTools.BuildWindowsDev`
- Autoplay smoke test: `Builds/Windows/5Kingdoms.exe -screen-fullscreen 0 -fk-autoplay <screenshot folder>`

`Tools/CoreTests` needs `Library/` (open the project in Unity once). Unity batchmode can't run while the editor has
the project open; copy Assets/Packages/ProjectSettings to a scratch folder and run there instead.

## Conventions
- Landscape only; Android builds use IL2CPP + ARM64 (`Assets/Editor/ProjectSettingsApplier.cs`).
- Unity's C# is 9.0: no file-scoped namespaces, global usings or records.
- Balance numbers live in `DungeonRunConfig`, `ActorCatalog` and `CombatRules`; check `-balance` after changing them.

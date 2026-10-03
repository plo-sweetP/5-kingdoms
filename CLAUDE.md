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
- Balance report: `dotnet run --project Tools/CoreTests -- -balance` (try numbers with `key=value` overrides, see
  `TuningFrom`; `seeds=600` for a steadier number; `-lead kristela` puts another hero in front); the party straight
  at the boss: `-- -boss <level>`; print a floor: `-- -map <seed>`; trace the autopilot: `-- -trace <seed> <fromAction>`
  (solo) or `-- -party <seed> <fromAction>`
- Regenerate art: `python Tools/pixelart/make_sprites.py --preview preview.png`
- Unity tests: `Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults results.xml`
- Windows build: `Unity.exe -batchmode -quit -projectPath . -executeMethod BuildTools.BuildWindowsDev`
- Autoplay smoke test: `Builds/Windows/5Kingdoms.exe -screen-fullscreen 0 -fk-autoplay <screenshot folder>`
  (add `-fk-floors 1 -fk-level 10` to go straight to the boss; autoplay always uses its own throwaway save, and
  aims each targeted action once the way a player does, saving `aim_*.png`)

Launch flags (`LaunchOptions`): `-fk-floors N`, `-fk-level N` (uses a throwaway save), `-fk-save PATH`,
`-fk-input keyboard|gamepad` (start with that HUD layout, e.g. to screenshot the skill row), `-fk-leader kristela|uzuki`
(someone other than Haiden leads). PlayMode tests set
`DungeonController.Overrides` instead. The real save is `save.json` in `Application.persistentDataPath`
(`SaveSystem`); never let tests or tools write to it.

`Tools/CoreTests` needs `Library/` (open the project in Unity once). Unity batchmode can't run while the editor has
the project open; copy Assets/Packages/ProjectSettings to a scratch folder and run there instead.

## Conventions
- Landscape only; Android builds use IL2CPP + ARM64 (`Assets/Editor/ProjectSettingsApplier.cs`).
- Unity's C# is 9.0: no file-scoped namespaces, global usings or records.
- Balance numbers live in `DungeonRunConfig`, `ActorCatalog`, `SkillCatalog`, `CombatRules` (damage, the ranged cuts,
  the ultimate's charge rates) and `EnemyBrain` (boss moves); check `-balance` after changing them (it reports fresh
  runs, ultimates per fight and a campaign with levels kept between runs). The autopilot and the partners' AI
  (`HeroTactics`) are the balance report's players, so a new skill or item needs AI rules too.
- There is no mana (PROGRESSION.md, "Skill resources"): skills sit out the hero's next turn, each hero has an
  always-ready weapon attack, and ultimates need a full charge meter (`Actor.Charge`).
- Attacks are deliberate (PROGRESSION.md, "Targeting and input"): moving into an enemy never attacks (it only turns
  the hero, no turn used). Attacks, skills and ultimates on a foe carry its tile (`HeroCommand.AttackAt / SkillAt /
  UltimateAt`); the AI must always use those (the soak tests fail on a refused command or an attack at nothing) and
  picks targets with `HeroTactics.PickTarget` (marked first, then lowest HP). The controller aims in two steps from
  `DungeonRun.AimFor` (`AimInfo`: reach, targets, the one marked first).
- Shots reach any foe within range that `DungeonMap.HasLineOfSight` sees (walls and wall corners block, actors
  don't; it's symmetric). Use `DungeonRun.InShotReach / FoesInSight / ShotTargetAt`, not line walks.
- Delays (stuns, slows, a snare under a boss) go through `DungeonRun.Delay`: capped at 50% of a turn (25% on a boss)
  and at most once per the target's own turn (`Actor.IsDelayed`). Never skip a turn.
- "Enter Play Mode" has domain reload off: statics survive between Play sessions, so reset them on scene load.
- Combat turn order lives in `Core/Run/Timeline.cs` (Honkai Star Rail-style action value). Game time is exact `AvTime`
  (BigInteger fractions): never use floats for time or turn decisions; ties go to the leader, then the lower actor id.
- Damage follows GEAR.md's multiplicative formula (`CombatRules.RollDamage`), in integer math, never floats. Final stats
  come from each actor's `StatSheet`: (base + level growth + weapon) x % + flat. Crit Rate, Crit DMG, Affinity and
  Resist are in tenths of a percent (50 = 5%). Design specs for gear and progression are in `docs/design/`.
- `GoldenReplayTests` pins equal-speed behavior to the pre-timeline engine. If a rules change is meant to alter
  replays, re-record its fingerprint and say why in the commit.
- Planned: party of up to 4 (`MaxPartySize`), PC/Steam with controller support. Write AI and combat so any
  hero-team member can be targeted, and keep platform services behind interfaces.

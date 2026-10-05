# 5 Kingdoms

Landscape mobile game (Android first, iOS later) in Unity 6.3 LTS (6000.3.25f1): farming + monster breeding +
Mystery Dungeon-style turn-based dungeons. Design and roadmap: GAME_PLAN.md.

## Layout
- `Assets/Scripts/Core/` — game rules in plain C# (asmdef `FiveKingdoms.Core`, `noEngineReferences`). No UnityEngine here.
  Rules change state and append `GameEvent`s; they never touch visuals.
- `Assets/Scripts/` (asmdef `FiveKingdoms.Game`) — Unity side: `Dungeon/` (controller, view, animations), `UI/` (HUD built
  in code), `Shared/` (sprites, the art manifest, the hero composer, pixel camera). Views only animate events and read
  state.
- `Assets/Tests/EditMode/` — NUnit tests for Core, including an autopilot soak test.
- `Assets/Art/Resources/` — the game's art, written by `Tools/pixelart/build_art.py` from the Tiny Swords packs
  (docs/design/ART.md): `Sprites/` (64 px = 1 tile: Tiles, Deco, Heroes, Heads, Monsters, Effects, Icons, UI) and
  `art_manifest.json` (frame sizes, pivots, animations, 9-slice borders, where the head sits on each frame of a hero's
  body). Don't edit these by hand: change the script and run it. Import settings are enforced by
  `Assets/Editor/PixelArtImporter.cs`.
- `Tools/pixelart/` — the art build (Python 3.7, stdlib only): `build_art.py` (run this), `ase.py` (reads .aseprite),
  `px.py` / `draw.py` (images, shapes, the pack's outline), `rigs.py` (the hero bodies and weapons), `heads.py` (the
  heroes' heads, armor sets, cosmetic head pieces), `looks.py` (stacks a look, like `HeroComposer`), `monsters.py`,
  `terrain.py`, `fx.py`, `ui.py`, `icons.py`, `sheets.py` (preview sheets). The packs stay outside the repo
  (`C:\Users\peter\5Kingdoms\ArtPacks\TinySwords\`, i.e. `5Kingdoms\ArtPacks\TinySwords` in the user's profile
  folder, or `--pack`): never commit their files (docs/THIRD_PARTY.md).
- `Tools/CoreTests/` — runs the Core tests outside Unity.

## Commands (from the repo root)
- Core tests, ~2 s: `dotnet run --project Tools/CoreTests` (add a name filter as an argument to run a subset)
- Balance report: `dotnet run --project Tools/CoreTests -- -balance` (try numbers with `key=value` overrides, see
  `TuningFrom`; `seeds=600` for a steadier number; `-lead kristela` puts another hero in front); the party straight
  at the boss: `-- -boss <level>`; print a floor: `-- -map <seed>`; trace the autopilot: `-- -trace <seed> <fromAction>`
  (solo) or `-- -party <seed> <fromAction>` (add `map=N` to draw the floor around the leader for N actions, and
  `key=value` overrides as for `-balance`); how far partners stray from the leader: `-- -spread`
- Rebuild the art, ~10 s: `python Tools/pixelart/build_art.py` (add `--preview <folder>` for the review sheets:
  heroes, weapons, armor sets, head pieces, rings, animation strips, icons; `--only preview` skips writing the art)
- Unity tests: `Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults results.xml`
- Windows build: `Unity.exe -batchmode -quit -projectPath . -executeMethod BuildTools.BuildWindowsDev`
- Android test APK, ~6 min: `Unity.exe -batchmode -quit -projectPath . -executeMethod BuildTools.BuildAndroidDev`
  (writes `Builds/Android/5Kingdoms-dev.apk`: a development build, IL2CPP, ARM64, Android 7.1 and up; the editor's
  Android module brings the SDK, NDK and JDK). It leaves the project on the Android target: switch back afterwards
  with `Unity.exe -batchmode -quit -projectPath . -buildTarget StandaloneWindows64`. The log's "Host type is not
  matching any asset type" lines come from the render pipeline package in every build and can be ignored.
- Autoplay smoke test: `Builds/Windows/5Kingdoms.exe -screen-fullscreen 0 -fk-autoplay <screenshot folder>`
  (add `-fk-floors 1 -fk-level 10` to go straight to the boss; autoplay always uses its own throwaway save, and
  aims each targeted action once the way a player does, saving `aim_*.png`; it saves `door_*.png` the first times
  the leader holds a doorway or the front rotates, and `rest*.png` when it waits for the party to heal up; with
  `-fk-demo view` it stages foes five tiles up and down a
  corridor, then one three tiles away, and captures how the camera shows them instead)

Launch flags (`LaunchOptions`): `-fk-floors N`, `-fk-level N` (uses a throwaway save), `-fk-save PATH`,
`-fk-input keyboard|gamepad` (start with that HUD layout, e.g. to screenshot the skill row), `-fk-leader kristela|uzuki`
(someone other than Haiden leads), `-fk-seed N` (the same floors every launch), `-fk-view zoomout|wide|lead` (the
camera while aiming: `zoomout` is the game's behavior, `wide` is always one zoom step out and is kept for a player
setting later, `lead` slides to the targets however far and is for debugging only), and `-fk-look` to try other looks, e.g.
`-fk-look "haiden=great_sword,mage_robe;uzuki=mage_staff,bare;kristela=crown;all=hawks_eye"`: per hero or `all`, any
of a weapon, an armor set, `bare` (no head piece), a cosmetic head piece (`hair_bow`, `crown`, `headband`) and a ring
set; the ids are in `art_manifest.json`. PlayMode tests set `DungeonController.Overrides` instead. The real save is
`save.json` in `Application.persistentDataPath` (`SaveSystem`); never let tests or tools write to it.

`Tools/CoreTests` needs `Library/` (open the project in Unity once). Unity batchmode can't run while the editor has
the project open, so the Unity checks run in a permanent sandbox copy of the project:
`& "C:\Users\peter\5Kingdoms\checkpoint.ps1" -Label <name> -Layouts` from the repo or worktree root mirrors `Assets`
into `C:\Users\peter\5Kingdoms\UnitySandbox`, runs EditMode, PlayMode, the Windows build and the autoplay runs, and
writes logs and screenshots to `C:\Users\peter\5Kingdoms\results\<name>` (about 10 minutes; `-CompileOnly` takes one).
That folder is an ordinary one on purpose: nothing the tools need may live under `%LOCALAPPDATA%`, which the Claude
desktop app keeps private to its own sessions (Explorer, Peter's editor and its C# compiler don't see files there).

## Conventions
- Landscape only; Android builds use IL2CPP + ARM64 (`Assets/Editor/ProjectSettingsApplier.cs`).
- Unity's C# is 9.0: no file-scoped namespaces, global usings or records.
- Balance numbers live in `DungeonRunConfig`, `ActorCatalog`, `SkillCatalog`, `CombatRules` (damage, the ranged cuts,
  the ultimate's charge rates) and `EnemyBrain` (boss moves); check `-balance` after changing them (it reports fresh
  runs, ultimates per fight, the heroes that fall before the boss, the HP they bring into a fight, what each hero
  does with its turns in fights, and a campaign with levels kept between runs). The targets: about 2-5% of fresh level-1 runs win, and with levels kept
  the first clear comes around the third attempt at Lv 9-10. The autopilot and the partners' AI (`HeroTactics`) are
  the balance report's players, so a new skill or item needs AI rules too.
- There is no mana (PROGRESSION.md, "Skill resources"): skills sit out the hero's next turn, each hero has an
  always-ready weapon attack, and ultimates need a full charge meter (`Actor.Charge`).
- Attacks are deliberate (PROGRESSION.md, "Targeting and input"): moving into an enemy never attacks (it only turns
  the hero, no turn used). Attacks, skills and ultimates on a foe carry its tile (`HeroCommand.AttackAt / SkillAt /
  UltimateAt`); the AI must always use those (the soak tests fail on a refused command or an attack at nothing) and
  picks targets with `HeroTactics.PickTarget` (marked first, then lowest HP). The controller aims in two steps from
  `DungeonRun.AimFor` (`AimInfo`: reach, targets, the one marked first).
- The party stays together (GAME_PLAN.md, "Party"): partners fight within `HeroTactics.LeashRange` steps' walk of
  the leader (`NearLeader`) and never search for a long way around their own allies. Measure the way as if allies
  weren't there, go around only when that's at most `DetourSteps` longer, otherwise close up and wait behind them
  (`TryEngage`, `PartnerBrain.Follow`; a partner gets past one that follows it with `DungeonRun.IsRegroupSwap`). Check
  `-spread` after changing partner movement; the soak tests fail if a partner strays more than 24 steps.
- Doorways and corridors (PROGRESSION.md, "Doorways and corridors"; AI only): a corridor tile or a doorway is
  `DungeonMap.IsNarrow` (at most two ways in or out, so the corner rule lets only the tile straight ahead reach a
  hero there). The hero in front holds a doorway instead of stepping out among more than `SafeCrowd` foes within
  sight range beyond it (`HeroTactics.HoldsTheDoor`; against that many or fewer it goes in only when fit, with a
  melee hero to bring). The wait is `HeroCommand.HoldTheDoor`, counted in `Actor.HeldTurns` and given up after
  `DoorPatience`. Where one hero holds the way, the hurt one and the fresh melee hero behind trade places
  (`DungeonRun.IsFrontRotation`), and the one behind heals; a hero fighting in a corridor's mouth makes way
  (`TryMakeWay`). A partner never moves the leader this way (`IsRotateSwap`). Any step of the party's AI toward the
  foes goes through `AutoPilot.Advance` or `HeroTactics.TryEngage`, which ask `HoldsTheDoor` first.
- Heroes heal between fights (PROGRESSION.md, "Heroes heal between fights"; AI only): outside a fight a hero's AI
  uses its heal whenever at least half of it goes to use (`HeroTactics.WorthToppingUp`, `TopUpPercent`), a hurt hero
  with no heal of its own goes and stands next to the one that can heal it (`TrySeekHealer`), and the autopilot's
  leader waits for all of that (`HeroCommand.Rest`, `HeroTactics.TryRest`), for at most `RestPatience` turns in a row
  without a heal landing on anyone (`Actor.RestedTurns`). The player's own leader is never made to wait.
- What the party's AI is up to is said by the rules, never guessed by the view: `HeroWaitedEvent` (it holds a door,
  or rests; with the turns in a row so far) and `SwappedEvent.Reason` (`Rotate`, `Safety`, `Engage`, `Regroup`,
  `Passing`). `DungeonView.ShowWait / ShowSwap` turn them into a word over the hero and a line in the log, so nobody
  thinks a hero is stuck. A new reason to stand still needs its event and its line.
- Shots reach any foe within range that `DungeonMap.HasLineOfSight` sees (walls and wall corners block, actors
  don't; it's symmetric). Use `DungeonRun.InShotReach / FoesInSight / ShotTargetAt`, not line walks.
- Delays (stuns, slows, a snare under a boss) go through `DungeonRun.Delay`: capped at 50% of a turn (25% on a boss)
  and at most once per the target's own turn (`Actor.IsDelayed`). Never skip a turn.
- "Enter Play Mode" has domain reload off: statics survive between Play sessions, so reset them on scene load
  (the sprite caches do it with `RuntimeInitializeOnLoadMethod(SubsystemRegistration)`).
- Art (docs/design/ART.md): 64 px = 1 tile = 1 unit, point filter, no compression. Whole-number zoom only
  (`PixelCamera`), no fractional sprite scaling, and HUD art at a whole number of screen pixels per art pixel
  (`UiArtScaler`; create HUD art through `UiFactory.CreatePanel / CreateIcon`, sized for 2 units per art pixel).
  Strips are cut into frames from the manifest (`SpriteLibrary.Strip / Monster`), with the pivot on the unit's tile
  centre. A hero is stacked from layers at run time (`HeroComposer`) from a `HeroLook`: hero, weapon (which picks the
  body: Warrior, Archer, Monk or Pawn rig), armor set (head piece and colours), head piece on or off, cosmetic head
  piece, ring set. Milestone 1h sets looks from equipped gear through `HeroLooks.Set`; `Tools/pixelart/looks.py`
  must stack in the same order as `HeroComposer`. Only what the game uses goes under `Assets/Art/Resources`.
- Animations never set the pace: `ActorView.Play(name, impactAfter)` times an attack so its impact frame lands when
  the turn's hit does, and nothing waits for an animation to finish. A sprite taller than about 1.6 tiles turns
  see-through while an actor stands behind it (`DungeonView.UpdateSeeThrough`).
- The camera while aiming (`PixelCamera.Frame`, Peter's choice on 2026-10-04): every target stays in view and out
  from under the HUD. The camera moves no further than it must; when that would slide the party out of the middle
  third of the screen, or the targets don't fit, it steps out one whole zoom level until the aim ends. Never slide
  the party toward the edge of the screen, and never zoom by a fraction.
- Combat turn order lives in `Core/Run/Timeline.cs` (Honkai Star Rail-style action value). Game time is exact `AvTime`
  (BigInteger fractions): never use floats for time or turn decisions; ties go to the leader, then the lower actor id.
- Damage follows GEAR.md's multiplicative formula (`CombatRules.RollDamage`), in integer math, never floats. Final stats
  come from each actor's `StatSheet`: (base + level growth + weapon) x % + flat. Crit Rate, Crit DMG, Affinity and
  Resist are in tenths of a percent (50 = 5%). Design specs for gear and progression are in `docs/design/`.
- `GoldenReplayTests` pins equal-speed behavior to the pre-timeline engine. If a rules change is meant to alter
  replays, re-record its fingerprint and say why in the commit.
- Planned: party of up to 4 (`MaxPartySize`), PC/Steam with controller support. Write AI and combat so any
  hero-team member can be targeted, and keep platform services behind interfaces.

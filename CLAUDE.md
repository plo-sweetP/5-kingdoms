# 5 Kingdoms

Landscape mobile game (Android first, iOS later) in Unity 6.3 LTS (6000.3.25f1): farming + monster breeding +
Mystery Dungeon-style turn-based dungeons. Design and roadmap: GAME_PLAN.md.

## Layout
- `Assets/Scripts/Core/` — game rules in plain C# (asmdef `FiveKingdoms.Core`, `noEngineReferences`). No UnityEngine here.
  Rules change state and append `GameEvent`s; they never touch visuals. `Actors/` (definitions, the skill catalog,
  `HeroProgress`), `Classes/` (class definitions and the kit a hero takes into a run), `Combat/`, `Dungeon/`, `Run/`.
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
  `terrain.py`, `fx.py`, `ui.py`, `tree.py` (the skill tree's pieces), `icons.py` (gear icons, skill icons),
  `sheets.py` (preview sheets), `tree_mock.py` (mock-ups of the skill-tree screen). The packs stay outside the repo
  (`C:\Users\peter\5Kingdoms\ArtPacks\TinySwords\`, i.e. `5Kingdoms\ArtPacks\TinySwords` in the user's profile
  folder, or `--pack`): never commit their files (docs/THIRD_PARTY.md).
- `Tools/CoreTests/` — runs the Core tests outside Unity.

## Commands (from the repo root)
- Core tests, ~2 s: `dotnet run --project Tools/CoreTests` (add a name filter as an argument to run a subset)
- Balance report: `dotnet run --project Tools/CoreTests -- -balance` (try numbers with `key=value` overrides, see
  `TuningFrom`; `seeds=600` for a steadier number; `-lead kristela` puts another hero in front; the heroes spend
  their points as `HeroBuilds` says: their own class as far as it is open, on the Guardian, Duelist and Marksman
  paths, and `build=uzuki:hunter` plays another path, `build=uzuki:marksman,hunter` one per milestone, `build=none`
  leaves the points unspent; `level=6` starts the "fresh" runs at that level, to measure a build); the party straight
  at the boss: `-- -boss <level>`; print a floor: `-- -map <seed>`; trace the autopilot: `-- -trace <seed> <fromAction>`
  (solo) or `-- -party <seed> <fromAction>` (add `map=N` to draw the floor around the leader for N actions, and
  `key=value` overrides as for `-balance`; on the boss floor each line lists who moved, struck, wound up and was
  caught by a slam); how far partners stray from the leader: `-- -spread`
- Rebuild the art, ~10 s: `python Tools/pixelart/build_art.py` (add `--preview <folder>` for the review sheets:
  heroes, weapons, armor sets, head pieces, rings, animation strips, icons, the Fencer kit's skill icons and
  effects, Divine Strike's holy light, the skill tree's pieces and mock-ups of its screen; `--only preview` skips
  writing the art)
- Unity tests: `Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults results.xml`
- Windows build: `Unity.exe -batchmode -quit -projectPath . -executeMethod BuildTools.BuildWindowsDev`
- Android test APK, ~6 min: `Unity.exe -batchmode -quit -projectPath . -executeMethod BuildTools.BuildAndroidDev`
  (writes `Builds/Android/5Kingdoms-dev.apk`: a development build, IL2CPP, ARM64, Android 7.1 and up; the editor's
  Android module brings the SDK, NDK and JDK). It leaves the project on the Android target: switch back afterwards
  with `Unity.exe -batchmode -quit -projectPath . -buildTarget StandaloneWindows64`. The log's "Host type is not
  matching any asset type" lines come from the render pipeline package in every build and can be ignored. The build
  removes Gradle's last package first (`BuildTools.GradlePackage`): patched in place, the same 41 MB of content had
  grown to 92 MB over three builds.
- Autoplay smoke test: `Builds/Windows/5Kingdoms.exe -screen-fullscreen 0 -fk-autoplay <screenshot folder>`
  (add `-fk-floors 1 -fk-level 10` to go straight to the boss; autoplay always uses its own throwaway save, and
  aims each targeted action once the way a player does, saving `aim_*.png`; it saves `mark*.png` when the leader's
  always-on mark is placed or grows, `door_*.png` the first times
  the leader holds a doorway or the front rotates, `rest*.png` when it waits for the party to heal up, and
  `keep_clear*.png` when a hero waits out of a boss's wind-up; with
  `-fk-demo view` it stages foes five tiles up and down a
  corridor, then one three tiles away, and captures how the camera shows them instead; every run also tours the skill tree from the pause menu,
  saving `01_tree_*.png`, and with `-fk-demo tree -fk-tree -fk-level 10` it only captures the tree for every hero, as
  between runs (each written milestone's three options, then the default path's picked), and quits)

Launch flags (`LaunchOptions`): `-fk-floors N`, `-fk-level N` (uses a throwaway save), `-fk-build default` with it
(the heroes' points spent as the balance report spends them, `HeroBuilds`; or builds by hero, e.g.
`-fk-build "uzuki:hunter;kristela:footwork"`, the others on their default path; without it the points stay free),
`-fk-save PATH`,
`-fk-input keyboard|gamepad` (start with that HUD layout, e.g. to screenshot the skill row), `-fk-minimap off|small|large`
(instead of the player's setting), `-fk-leader kristela|uzuki`
(someone other than Haiden leads), `-fk-seed N` (the same floors every launch), `-fk-view zoomout|wide|lead` (the
camera while aiming: `zoomout` is the game's behavior, `wide` is always one zoom step out and is kept for a player
setting later, `lead` slides to the targets however far and is for debugging only), `-fk-zoom near|far` (the view,
instead of the player's setting: Near is the zoom the rule picks, Far one whole step further out where the screen
has one; the log's "View: W x H at zoom N, T tiles high (Near)" line says which is on), `-fk-device desktop|phone|tablet`
(the kind of screen instead of what the device reports, to see the phone's or the tablet's looks in a PC window: the
view it starts in and how small the numbers over the actors get; the log's "Screen: N dpi, Tablet; starts in Far
(this screen's default)" line says what was used), `-fk-tree [hero]` (the skill tree first, as between runs: a build
can be changed there and the run starts when it closes; it edits the real save unless `-fk-level` or `-fk-save` is
given), and `-fk-look` to try other looks, e.g.
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
  runs, how fast the party fights (rounds per pack fight, the rounds of a won boss fight, the boss's HP lost per
  round: the yardstick for a damage option, on a `level=9` party), ultimates per fight, the heroes that fall before the boss, the HP they bring into a fight, what each hero
  does with its turns in fights, the boss's slams that hit a hero and how that hero stood, and a campaign with
  levels kept between runs, the points spent between runs on each hero's default build). The targets: about 2-5%
  of fresh level-1 runs win, and with levels kept the first clear comes around the third attempt at Lv 9-10. A
  level-1 hero has no point to spend, so class content moves only the campaign and `level=N` runs. The autopilot and the partners' AI (`HeroTactics`) are
  the balance report's players, so a new skill or item needs AI rules too.
- There is no mana (PROGRESSION.md, "Skill resources"): skills sit out the hero's next turn, each hero has an
  always-ready weapon attack, and ultimates need a full charge meter (`Actor.Charge`).
- Classes (PROGRESSION.md, "Classes" and "Building 1g"; `Core/Classes`, `HeroProgress`): a hero has a point per
  level. A point buys the next tier of any class (`HeroProgress.Raise`); a milestone tier (5, 10, 15, 20, 25) also
  takes one of its three `ClassOption`s, from any path, and a milestone whose options aren't written can't be passed
  (`ClassDefinition.IsOpen`, `HighestOpenTier`). The catalog has Archer, Paladin, Fencer (Kristela's since
  2026-10-05) and Monk, each with its "Every tier" bumps. Tier 5 is written for the Archer, the Paladin and the
  Fencer, which stop at tier 9; the Monk stops at tier 4. A new option needs its `ClassOption` in `ClassCatalog`,
  tests (`ArcherTiersTests`, `PaladinTiersTests`, `FencerTiersTests`), an AI rule where a sensible player would play
  differently, its sentence in `SkillText.Describe`, for a new skill an icon and an effect, and where it teaches
  a skill the one a default build gives up for it (`HeroBuilds`). Every tier adds the class's `StatBump`s (never SPD); the hero's
  highest class sets its speed modifier (`HeroKit.SpeedFor`, clamped to 85-100 before gear). An option teaches a
  skill, upgrades the hero's own copy of one (`SkillDefinition.Change`; an upgrade of a skill the hero doesn't know
  teaches the skill instead) or changes one weapon family's attack. Never change a catalog skill.
- An always-on skill (PROGRESSION.md, "Deadly Mark"; `SkillDefinition.AlwaysOn`) is a passive that keeps a loadout
  slot: it is never used (`SkillCheck.AlwaysOn`), is not Quick, has no cooldown and needs no aim. The Archer's
  Deadly Mark is the first: his aimed shots place and build the mark (`DungeonRun.ShootAtMark`, `MarkBuiltEvent`,
  `DungeonRun.MarkedBy`). Its look is shared by later passives: the HUD's dark button with an "Always on" tag that
  shows what it is doing (`DungeonHud.AlwaysOnLabel`), a press that only says what it is
  (`DungeonController.AlwaysOnMessage`), the lit and ringed loadout sphere (`SkillTreeModel.IsAlwaysOn`),
  `TreeText.AlwaysOn` in the tags.
- The Paladin's Challenge is a taunt with a power (`StatusKind.Taunt`, `StatusEffect.Power`): a foe under it does
  that much less damage to the one who taunted it (`DungeonRun.DamageTakenPercent`), and the AI keeps it up before
  it heals (`HeroTactics.TryChallenge`). The AI weighs a blow that is coming with `DungeonRun.BlowDamage`, and reads
  a boss's next turn with `EnemyBrain.NextTurnIsNoBlow` (a wind-up, the call for help), which must agree with
  `EnemyBrain.DecideTroll`: a new boss move needs its case there.
- A hero's kit is its own: read skills, the ultimate and the attack's name from the `Actor` (`Skills`, `Ultimate`,
  `AttackName`, `Kit`), never from `ActorDefinition`, whose `Skills` are only the kit a hero starts with. The run
  takes `HeroProgress.Kit` when it starts and keeps it: loadouts (three skills, at most one Quick, weapon-tied
  skills only with a weapon of that `WeaponFamily`, one ultimate) change between runs only. `HeroProgress.Unlearn`
  returns a class's points (free for now; relearning from level 20 comes with 1g-2).
- Kristela's Fencer kit (PROGRESSION.md, "Kristela's Fencer kit"; `FencerKitTests`). A strike that dashes up to
  its target first is `SkillDefinition.DashTiles` (Lunge: along the 8 lines, nobody between, corner rule kept;
  `DungeonRun.StrikeTargetAt(user, tile, dashTiles)`). A strike with several hits keeps to one foe unless `MovesOn`
  (the Monk's Flurry). A counter stance is `SkillEffect.Counter` and `StatusKind.Riposte`: the cut is in
  `DamageTakenPercent`, the answering blow in `DungeonRun.Counter` (said by `CounterEvent`), and a monster can fall
  to it on its own turn. Blade Dance is `SkillEffect.SharedStrikes`. The weapon attack hits as its kit says
  (`Kit.WeaponAttack.Power`, `Hits`). The AI's step toward the foes is `HeroTactics.StepToward`: a Lunge when that
  reaches a foe, through `HoldsTheDoor` and the leash; it takes the stance only when a foe next to the hero acts
  first and is going for it (`TryRiposte`, `EnemyBrain.TargetOf`), after her thrusts unless the blow is a heavy one
  (a boss's, or `HeroTactics.HeavyBlowPercent` of her HP). The Monk's kit is tested on `TestHeroes.Monk`.
- The view plays an action's events after the rules are through, so the run's state is already the end state: a
  status that begins and ends within the events (a counter stance) is shown from its events
  (`ActorView.SetStance`), not read from the actor. A skill's effects belong to its user's own strikes only
  (`DungeonView.activeSkillUser`).
- Saves are version 2 (`SaveSystem`): per hero the level, EXP, classes with their picks, and the loadout. A hero
  entry says which version wrote it (`HeroSave.savedWith`); one from before classes gets tier 1 of its own class
  and the rest of its points free. Loading relearns a build tier by tier (`HeroProgress.Restore`), so what the
  rules no longer allow is dropped and its points stay free. A change to what is saved needs a new version and a
  migration test in `SaveSystemTests`.
- The HUD (docs/design/HUD.md; `DungeonHud`, `PauseMenu`): Wait, Berry, Auto and Pause in a row top-right; for
  touch the attack bottom-right with the skills and the ultimate in an arc around it (Peter tried a row along the
  bottom on 2026-10-05 and went back to the arc: don't flatten it again). While Auto plays, the D-pad is
  hidden and the leader's buttons are greyed out: only `SwitchLeader` gets past `DungeonController.ChooseCommand`.
  The pause menu (the Pause button, Esc, the gamepad's Start; "Go down" is R3) stops everything, Auto included; its
  Exit ends the run as `RunState.Left` (`DungeonRun.Leave`), and its Reset level (testing) writes level 1 to the
  save: the player's own action, while tests and tools keep to their own save file. Its settings (View: Near / Far,
  and the minimap) are kept in PlayerPrefs, read and written only with the real save (`UsesRealSave`). The hero stats page writes each skill's
  description from the hero's own copy (`SkillText.Describe`): a new `SkillEffect` or skill field needs its
  sentence there.
- The skill-tree screen (PROGRESSION.md, "Building 1g", "Step 6 as built"; `SkillTreeModel`, `TreeText`,
  `SkillTreeScreen`; `SkillTreeModelTests`, `SkillTreeScreenTests`): everything the screen knows and does is the
  model's, plain C# in Core (where the cursor stands, what the info panel says, what pressing does), and goes
  through `HeroProgress`; the screen only draws the model and passes on touches, keys and controller buttons. The
  list on the left shows only the classes a hero has (`Learned`), with a "+" entry that lists the others (`Others`,
  `TreeZone.Adding`; Peter, 2026-10-10); professions join that list with 1g-3. A
  tap puts the cursor somewhere and the info panel's button presses; raising a tier asks nothing, unlearning asks
  first. A build changes between runs only: the end panel's Skills button (K, the controller's Y) opens the
  screen to change one, the pause menu's Hero stats page to look (`SkillTreeModel.ReadOnly`), `-fk-tree` before
  the first run; the controller saves on every change (`BuildChanged`). Its pieces are white or grey art the
  game tints (`Tools/pixelart/tree.py`; `SkillTreeScreen.PathColors` and `tree.PATH_COLORS` must agree), and a
  skill's icon is `Icons/skill_<skill id>` (a weapon attack by its weapon: `skill_attack_<weapon id>`;
  `skill_unknown` where none is drawn): a new skill needs its icon in `icons.py`. A new thing the screen can stand
  on or say needs its case in the model and a test there.
- The minimap (HUD.md, "Minimap"; `Minimap`, `MinimapFogTests`) only draws what the rules say: explored tiles are
  Core state (`DungeonRun.IsExplored`, updated by `Explore` after every `Execute` and on a new floor: a room as a
  whole once a hero stands in it or one step from it, a corridor two steps around a hero, never part of a room),
  and a foe is marked only while `DungeonRun.PartySees` its tile. No fog is drawn over the dungeon itself.
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
- Footing in a boss fight (PROGRESSION.md, "Footing in a boss fight"; AI only; `FootingTests`): a way out of a
  slam is `HeroTactics.HasWayOut` (a free tile beside the hero out of the slam's reach that no other hero needs as
  its only one). Both brains pass their choice through `HeroTactics.KeepClear`: no hero's AI walks, dashes or
  lunges into a slam that is winding up (`HeroCommand.KeepClear`), so a new way of moving needs its case in
  `HeroTactics.Destination`. `TryDodge` steps out on the last turn before the slam (a hero that is up again first
  strikes once more), leaves an ally its only way out, and with no way out uses what the hero has
  (`TryWeatherTheSlam`: a dash, a swap with an ally that can take the blow, `DungeonRun.IsShelterSwap`, never the
  leader; a guard, Riposte, the aura). A melee hero next to a boss on a tile with no way out moves over on a
  quiet turn (`TryFindFooting`). The slam says how each hero it catches stood (`SlamCaughtEvent`, from
  `Actor.Footing`), which is what `-balance` counts. The archer is left out on purpose: one that keeps off the
  walls beats the Troll alone (PROGRESSION.md).
- What the party's AI is up to is said by the rules, never guessed by the view: `HeroWaitedEvent` (it holds a door,
  rests, or keeps clear of a wind-up; with the turns in a row so far) and `SwappedEvent.Reason` (`Rotate`,
  `Safety`, `Shelter`, `Engage`, `Regroup`, `Passing`). `DungeonView.ShowWait / ShowSwap` turn them into a word over the hero and a line in the log, so nobody
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
- The view (HUD.md, "The view on the tablet"; Peter, 2026-10-10): Near is the whole zoom nearest to 11 tiles high
  (`PixelCamera.NearZoomFor`), Far (`PixelCamera.Far`, a choice in Settings) is one whole step further out, and a
  screen that already plays at 1x has no Far (`FarAvailable`: the choice is greyed out). The HUD keeps its size
  in both. Check anything drawn in the dungeon in both views, on the phone's and the tablet's shape. A tablet
  starts in Far, a phone and a PC in Near, until the player picks a view (`PixelCamera.ScreenKindFor`: a tablet is
  a touch screen at least 600 dp on its shorter side by the reported dpi; `FarByDefault`). The numbers and words
  over the actors scale with the view like the sprites (`PixelCamera.WorldTextScale`, `DungeonHud.ShowFloatingText`:
  half in a Far view at half the zoom, never below three quarters on a phone); the HUD and the message log don't.
- The camera while aiming (`PixelCamera.Frame`, Peter's choice on 2026-10-04): every target stays in view and out
  from under the HUD. It works from the chosen view: from 1x there is no step out left, so it only slides. The camera moves no further than it must; when that would slide the party out of the middle
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

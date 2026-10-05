using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FiveKingdoms.Core;
using FiveKingdoms.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace FiveKingdoms.Dungeon
{
    /// <summary>
    /// Entry point of the dungeon scene. Owns the rules (DungeonRun), the visuals (DungeonView), the HUD and the
    /// party's saved progress, and turns input into commands for the leader. Input is read only while nothing is
    /// animating, which keeps the game strictly turn-based; a button pressed during an animation is buffered and runs
    /// next. Attacks are deliberate (PROGRESSION.md, "Targeting and input"): walking into an enemy only turns the hero
    /// to face it. The weapon attack, a skill or an ultimate that needs a target starts aiming: its reach and valid
    /// targets light up, one is marked, and the player taps a target, or presses the action again to fire at the
    /// marked one; holding a direction toward a target while pressing fires at it at once. Tapping an enemy in reach
    /// when not aiming is the weapon attack on it. Every command names its target's tile. Touch: on-screen D-pad and
    /// buttons; tap a party card to lead that hero, tap a tactic badge to change it. Keyboard: WASD/arrows plus
    /// Q/E/Z/C or the numpad for 8 directions, Space attack, 1/2/3 skills, 4 ultimate, Tab switch hero, G partners'
    /// tactics, X wait, B berry, Enter stairs, T auto, R restart; while aiming, directions pick a target (again for
    /// the next one out that way), Space or the same key fires, Esc cancels. Controller: left stick or D-pad, A
    /// attack, LB/LT/RT skills, RB ultimate, B switch hero, L3 tactics, Y wait, X berry, Start stairs, View auto, A or
    /// Start to restart; while aiming, A fires and B cancels.
    /// </summary>
    public sealed class DungeonController : MonoBehaviour
    {
        const float ComboWindow = 0.06f; // Lets two direction keys pressed together count as one diagonal.
        const float StickDeadZone = 0.5f;

        /// <summary>Test hook: options for the next scene load (PlayMode tests). Null uses the command line.</summary>
        public static LaunchOptions Overrides;

        [Tooltip("Fixed seed for reproducible runs. 0 picks a new random seed every run.")]
        [SerializeField] int seed;
        [SerializeField] int floorCount = 5;

        LaunchOptions options;
        HeroProgress[] party;
        int[] levelsAtStart;

        /// <summary>Each hero's tactic, kept from run to run (party order).</summary>
        PartyTactic[] tactics;
        DungeonRun run;
        DungeonView view;
        DungeonHud hud;
        PixelCamera pixelCamera;
        bool busy;
        HeroCommand? buffered;
        float directionHeldFor;
        bool autoPilot;
        bool paused;
        Vector2Int menuStick;

        /// <summary>The settings page's choice for the camera while aiming, kept on this device (not in the save).</summary>
        const string WideViewKey = "fk.view.wide";
        const string MinimapKey = "fk.minimap";
        MinimapSize minimap = MinimapSize.Small;
        InputMode inputMode = InputMode.Touch;

        /// <summary>The action being aimed, or null.</summary>
        Aiming aiming;
        Direction8? aimHeld;
        bool confirmAim, cancelAim;
        GridPos? injectedTap;
        static readonly List<RaycastResult> HudHits = new List<RaycastResult>();

        /// <summary>An attack, skill or ultimate waiting for the player to pick what to aim it at.</summary>
        sealed class Aiming
        {
            public Aiming(HeroCommandKind kind, int slot, AimInfo aim, int selected)
            {
                Kind = kind;
                Slot = slot;
                Aim = aim;
                Selected = selected;
            }

            public HeroCommandKind Kind { get; }
            public int Slot { get; }
            public AimInfo Aim { get; }
            public int Selected { get; set; }

            /// <summary>The HUD button it came from: -1 the attack, 0-2 a skill, 3 the ultimate.</summary>
            public int Button => Kind == HeroCommandKind.Attack ? -1 : Kind == HeroCommandKind.Ultimate ? 3 : Slot;

            public bool Matches(HeroCommand command) => command.Kind == Kind && (Kind != HeroCommandKind.Skill || command.Slot == Slot);

            /// <summary>The command on the option at <paramref name="index"/>: its target's tile, or for a roll or dash that way.</summary>
            public HeroCommand CommandFor(int index) => Aim.Options[index].ToCommand(Kind, Slot);
        }

        public DungeonRun Run => run;
        public bool IsIdle => !busy && run != null && run.State == RunState.InProgress;

        /// <summary>True while an action's animations are playing.</summary>
        public bool IsAnimating => busy;

        /// <summary>True while an attack, skill or ultimate waits for the player to pick its target.</summary>
        public bool IsAiming => aiming != null;

        /// <summary>The pause menu is open: nothing acts, the auto-pilot included.</summary>
        public bool Paused
        {
            get => paused;
            set => SetPaused(value);
        }

        /// <summary>Tests, autoplay and debug launches use their own save file and never touch the player's settings either.</summary>
        bool UsesRealSave => options.SavePath == null;

        /// <summary>The Auto button: the hero plays itself until the player turns this off.</summary>
        public bool AutoPilotEnabled
        {
            get => autoPilot;
            set => SetAutoPilot(value);
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            options = Overrides ?? LaunchOptions.FromCommandLine();
            // Always set (null means the normal location): with domain reload off, a test's path would otherwise linger.
            SaveSystem.FilePath = options.SavePath;
            if (options.FreshSave) SaveSystem.Delete();
            var roster = ActorCatalog.StartingParty;
            party = options.StartLevel.HasValue
                ? roster.Select(definition => new HeroProgress(definition, options.StartLevel.Value)).ToArray()
                : SaveSystem.LoadParty(roster);
            // -fk-leader: someone else leads this session (saves don't depend on the order).
            int leader = Array.FindIndex(party, hero => hero.Definition.Id == options.Leader);
            if (leader > 0) party = party.Skip(leader).Take(1).Concat(party.Where((hero, i) => i != leader)).ToArray();
            tactics = new PartyTactic[party.Length];
            // Every hero starts in its default look; -fk-look tries others (gear will set them from milestone 1h on).
            HeroLooks.Clear();
            HeroLooks.Parse(options.Looks, roster.Select(definition => definition.Id));

            pixelCamera = SetUpCamera();
            if (options.View.HasValue) pixelCamera.Mode = options.View.Value;
            hud = DungeonHud.Create(pixelCamera.GetComponent<Camera>());
            hud.CommandRequested += command => buffered = command;
            hud.RestartRequested += StartNewRun;
            hud.AutoPilotToggled += () => SetAutoPilot(!autoPilot);
            hud.PauseRequested += () => SetPaused(!paused);
            hud.PauseMenu.ResumeRequested += () => SetPaused(false);
            hud.PauseMenu.RestartConfirmed += RestartRun;
            hud.PauseMenu.ExitConfirmed += LeaveRun;
            hud.PauseMenu.ResetLevelConfirmed += ResetLevels;
            hud.PauseMenu.WideViewChanged += SetWideView;
            hud.PauseMenu.MinimapChanged += SetMinimap;
            minimap = options.Minimap ?? (UsesRealSave ? (MinimapSize)Mathf.Clamp(PlayerPrefs.GetInt(MinimapKey, (int)MinimapSize.Small), 0, 2) : MinimapSize.Small);
            hud.SetMinimap(minimap);
            if (!options.View.HasValue && UsesRealSave && PlayerPrefs.GetInt(WideViewKey, 0) == 1) pixelCamera.Mode = ViewMode.Wide;
            hud.TacticCycleRequested += CycleTactic;
            if (options.StartInputMode.HasValue)
            {
                inputMode = options.StartInputMode.Value;
                hud.SetInputMode(inputMode);
            }
            view = new GameObject("Dungeon").AddComponent<DungeonView>();
            view.Init(hud, pixelCamera);
            AutoplayDriver.AttachIfRequested(this);
        }

        void Start() => StartNewRun();

        /// <summary>Queues a command as if a button had been pressed (used by the autoplay smoke test).</summary>
        public void Submit(HeroCommand command) => buffered = command;

        /// <summary>A tap on a map tile, as if the player had touched it there (used by tests and the autoplay smoke test).</summary>
        public void Tap(GridPos tile) => injectedTap = tile;

        /// <summary>Redraws everything from the run's state (the autoplay's staged demos move actors by hand).</summary>
        public void RefreshView()
        {
            EndAiming();
            view.Rebuild(run);
            hud.Refresh(run);
        }

        void StartNewRun()
        {
            StopAllCoroutines();
            view.StopAllCoroutines();
            int runSeed = options.Seed ?? (seed != 0 ? seed : Random.Range(1, int.MaxValue));
            var config = new DungeonRunConfig { FloorCount = options.FloorCount ?? floorCount };
            run = new DungeonRun(runSeed, config, party);
            levelsAtStart = party.Select(hero => hero.Level).ToArray();
            for (int i = 0; i < run.Party.Count; i++) run.SetTactic(run.Party[i], tactics[i]);
            Debug.Log($"Dungeon run started with seed {runSeed}, party: {string.Join(", ", party.Select(hero => hero.ToString()))}.");
            busy = false;
            buffered = null;
            injectedTap = null;
            EndAiming();
            SetPaused(false);
            hud.HideRunEnd();
            hud.ClearLog();
            StartCoroutine(hud.Fade(0f, 0.01f));
            view.Rebuild(run);
            hud.Refresh(run);
            hud.ShowBanner(run.Config.Name, DungeonView.FloorTitle(run));
            if (run.IsBossFloor) hud.AddMessage("A powerful presence fills the air...", DungeonHud.HintColor);
        }

        void Update()
        {
            if (run == null) return;
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            UpdateInputMode(keyboard, gamepad);

            if (run.State != RunState.InProgress)
            {
                EndAiming();
                bool restart = keyboard != null && (keyboard.rKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) ||
                               gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);
                if (!busy && restart) StartNewRun();
                return;
            }
            if (paused)
            {
                // Nothing acts, the auto-pilot included; what was pressed meanwhile is dropped, not kept for later.
                buffered = null;
                injectedTap = null;
                ReadMenuInput(keyboard, gamepad);
                return;
            }

            ReadButtons(keyboard, gamepad);
            var held = ReadHeldDirection(keyboard, gamepad);
            var tapped = ReadTappedTile() ?? injectedTap;
            injectedTap = null;
            // A tap on an enemy is the weapon attack on it. Buffered like a button press, so one made mid-animation counts.
            if (tapped.HasValue && aiming == null && TapAttack(tapped.Value) is HeroCommand tap) buffered = tap;
            if (busy) return;

            var command = buffered;
            buffered = null;
            if (aiming != null)
            {
                // While the player aims, the auto-pilot waits too.
                command = ContinueAiming(command, held, tapped);
                if (command.HasValue) StartCoroutine(Execute(command.Value));
                return;
            }
            confirmAim = cancelAim = false;

            if (command == null && held.HasValue) command = HeroCommand.Move(held.Value);
            else if (command.HasValue && IsAimable(command.Value) && (!autoPilot || AllowedDuringAuto(command.Value)))
            {
                command = Aim(command.Value, held);
                if (command == null) return; // Aiming now, or there was nothing to aim at.
            }
            if (autoPilot && command.HasValue && !AllowedDuringAuto(command.Value)) hud.ShowAutoPilotBlocked();
            command = ChooseCommand(command, autoPilot, run);
            if (command.HasValue) StartCoroutine(Execute(command.Value));
        }

        // ---- Aiming (PROGRESSION.md, "Targeting and input") ----

        /// <summary>An attack, skill or ultimate that hasn't been pointed anywhere yet.</summary>
        static bool IsAimable(HeroCommand command) =>
            !command.Aimed && (command.Kind == HeroCommandKind.Attack || command.Kind == HeroCommandKind.Skill || command.Kind == HeroCommandKind.Ultimate);

        /// <summary>
        /// The weapon attack on the enemy standing on a tapped tile, if it's in reach (one deliberate tap per hit). Null,
        /// with a hint, for an enemy out of reach; null for a tap on anything else.
        /// </summary>
        HeroCommand? TapAttack(GridPos tile)
        {
            var hero = run.Hero;
            if (!(run.ActorAt(tile) is Actor foe) || foe.Team == hero.Team) return null;
            if (run.AttackTargetAt(hero, tile) != null) return HeroCommand.AttackAt(tile);
            if (!autoPilot) hud.AddMessage(OutOfReachMessage(hero, foe), DungeonHud.HintColor);
            return null;
        }

        /// <summary>
        /// Points an attack, skill or ultimate that hasn't been aimed yet. Returns the command to carry out now: the action
        /// itself when it isn't aimed (heals, auras) or can't be used (the run then says why), or, with a direction held
        /// toward a target, the action on that target at once. Returns null when aiming started (its reach and targets
        /// light up, one of them marked) or when the weapon attack has nothing in reach (a hint says so: a swing at the
        /// air would only waste the turn).
        /// </summary>
        HeroCommand? Aim(HeroCommand command, Direction8? held)
        {
            var hero = run.Hero;
            SkillDefinition skill = null;
            if (command.Kind == HeroCommandKind.Skill)
            {
                if (run.CheckSkill(command.Slot) is SkillCheck.NoSkill or SkillCheck.OnCooldown) return command;
                skill = hero.Skills[command.Slot];
            }
            else if (command.Kind == HeroCommandKind.Ultimate)
            {
                if (run.CheckUltimate() is SkillCheck.NoSkill or SkillCheck.NotCharged) return command;
                skill = hero.Ultimate;
            }
            var aim = run.AimFor(hero, skill);
            if (!aim.NeedsAim) return command;
            if (aim.Options.Count == 0)
            {
                if (command.Kind != HeroCommandKind.Attack) return command;
                hud.AddMessage(NoAttackTargetMessage(hero), DungeonHud.HintColor);
                return null;
            }
            if (held.HasValue && aim.NearestToward(held.Value) is int toward && toward >= 0)
                return aim.Options[toward].ToCommand(command.Kind, command.Slot);

            aiming = new Aiming(command.Kind, command.Slot, aim, aim.Default);
            aimHeld = held; // A direction already held (and pointing at nothing) doesn't move the mark until it changes.
            confirmAim = cancelAim = false;
            view.ShowAim(run, aim, aiming.Selected);
            hud.SetAiming(aiming.Button, AimPrompt(aim, skill));
            return null;
        }

        /// <summary>
        /// One frame of aiming: fire (a tapped target, the same action pressed again, or Space/A), cancel (a tap elsewhere,
        /// Esc, B or a right click), pick another target by direction, or drop it for another command.
        /// </summary>
        HeroCommand? ContinueAiming(HeroCommand? command, Direction8? held, GridPos? tapped)
        {
            if (cancelAim)
            {
                EndAiming();
                return null;
            }
            if (confirmAim) return Fire(aiming.Selected);
            if (command.HasValue)
            {
                if (aiming.Matches(command.Value) && !command.Value.Aimed) return Fire(aiming.Selected);
                EndAiming();
                var next = command.Value;
                if (autoPilot && !AllowedDuringAuto(next))
                {
                    hud.ShowAutoPilotBlocked();
                    return null;
                }
                if (!IsAimable(next)) return next;
                var now = Aim(next, null); // Another action: aim that one instead (never at once: the stick was picking targets).
                aimHeld = held;
                return now;
            }
            if (tapped.HasValue)
            {
                int option = aiming.Aim.IndexAt(tapped.Value);
                if (option >= 0) return Fire(option);
                EndAiming(); // A tap anywhere else lets it go.
                return null;
            }
            if (held != aimHeld)
            {
                aimHeld = held;
                if (held.HasValue) SelectToward(held.Value);
            }
            return null;
        }

        HeroCommand Fire(int option)
        {
            var command = aiming.CommandFor(option);
            EndAiming();
            return command;
        }

        void EndAiming()
        {
            confirmAim = cancelAim = false;
            if (aiming == null) return;
            aiming = null;
            view.ClearAim();
            hud.SetAiming(null, null);
        }

        /// <summary>Marks the nearest target that way; pressed again, the next one out (<see cref="AimInfo.Toward"/>).</summary>
        void SelectToward(Direction8 direction)
        {
            aiming.Selected = aiming.Aim.Toward(direction, aiming.Selected);
            view.ShowAim(run, aiming.Aim, aiming.Selected);
        }

        string AimPrompt(AimInfo aim, SkillDefinition skill)
        {
            string name = skill?.Name ?? run.Hero.AttackName;
            string what = !aim.PicksTile ? "a target" : skill?.Effect == SkillEffect.Dash ? "where to dash" : "where to roll";
            switch (inputMode)
            {
                case InputMode.Keyboard: return $"{name}: pick {what} with the arrows, Space fires at the marked one, Esc cancels.";
                case InputMode.Gamepad: return $"{name}: pick {what} with the stick, A fires at the marked one, B cancels.";
                // Short, and naming the button by its label: on a tablet it has to fit between the D-pad and the skill buttons.
                default: return $"{name}: tap {what}, or tap {skill?.ShortName ?? name} again.";
            }
        }

        /// <summary>
        /// The map tile tapped or clicked this frame, if it wasn't on a HUD control. Any finger counts, not only the first
        /// one down: a thumb resting on the D-pad mustn't swallow a tap on an enemy.
        /// </summary>
        GridPos? ReadTappedTile()
        {
            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                    if (touch.press.wasPressedThisFrame && MapTileAt(touch.position.ReadValue()) is GridPos tile) return tile;
            }
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return MapTileAt(Mouse.current.position.ReadValue());
            return null;
        }

        /// <summary>The map tile under a screen point, or null when a HUD control is in the way.</summary>
        GridPos? MapTileAt(Vector2 screen)
        {
            var system = EventSystem.current;
            if (system != null)
            {
                HudHits.Clear();
                system.RaycastAll(new PointerEventData(system) { position = screen }, HudHits);
                if (HudHits.Count > 0) return null;
            }
            var world = pixelCamera.GetComponent<Camera>().ScreenToWorldPoint(screen);
            return new GridPos(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
        }

        /// <summary>
        /// Picks this turn's command. While the auto-pilot is on it plays every turn and the player's moves and actions
        /// are ignored, skills and ultimates included (HUD.md, "Auto": Peter, 2026-10-05, "Auto blocks the skills").
        /// Only switching which hero leads still goes through; the auto-pilot carries on with that hero.
        /// </summary>
        public static HeroCommand? ChooseCommand(HeroCommand? playerCommand, bool autoPilot, DungeonRun run) =>
            !autoPilot ? playerCommand
            : playerCommand.HasValue && AllowedDuringAuto(playerCommand.Value) ? playerCommand
            : AutoPilot.Decide(run);

        static bool AllowedDuringAuto(HeroCommand command) => command.Kind == HeroCommandKind.SwitchLeader;

        /// <summary>The hero plays itself (the same AutoPilot the tests use). Only the player turns it off: Auto, T or View.</summary>
        void SetAutoPilot(bool on)
        {
            if (autoPilot == on) return;
            autoPilot = on;
            if (on) EndAiming(); // An aim the player had open is let go: the hero plays itself from here.
            hud.SetAutoPilot(on);
            hud.AddMessage(on ? "Auto-pilot on. Press Auto again to take over." : "Auto-pilot off.", DungeonHud.HintColor);
        }

        /// <summary>
        /// The Pause button, Esc or Start: the pause menu opens, and while it is open no command is taken, from the
        /// player or the auto-pilot. Only a run that is still going can be paused.
        /// </summary>
        void SetPaused(bool on)
        {
            if (paused == on || on && (run == null || run.State != RunState.InProgress)) return;
            paused = on;
            if (on) EndAiming();
            hud.SetPaused(on);
            if (on) hud.PauseMenu.Open(run, party, pixelCamera.Mode == ViewMode.Wide, minimap);
            else hud.PauseMenu.Close();
        }

        /// <summary>The pause menu's Restart: the run starts again on the first floor. What the party earned is kept, as after a defeat.</summary>
        public void RestartRun()
        {
            SaveSystem.SaveParty(party);
            StartNewRun();
        }

        /// <summary>The pause menu's Exit: the run ends here with the usual end panel ("Return to the farm?" once there is one).</summary>
        public void LeaveRun()
        {
            SetPaused(false);
            if (run.State != RunState.InProgress) return;
            run.Leave();
            SaveSystem.SaveParty(party);
            hud.Refresh(run);
            hud.ShowRunEnd(run, levelsAtStart);
        }

        /// <summary>
        /// The pause menu's Reset level, for testing: every hero goes back to level 1 with no EXP and its starting build,
        /// the save is written, and the run starts again. This is the player's own action on their save; tests and tools
        /// run with a save file of their own.
        /// </summary>
        public void ResetLevels()
        {
            party = party.Select(hero => new HeroProgress(hero.Definition)).ToArray();
            SaveSystem.SaveParty(party);
            StartNewRun();
            hud.AddMessage("Testing: every hero is back at level 1.", DungeonHud.HintColor);
        }

        void SetMinimap(MinimapSize size)
        {
            minimap = size;
            hud.SetMinimap(size);
            if (!UsesRealSave) return;
            PlayerPrefs.SetInt(MinimapKey, (int)size);
            PlayerPrefs.Save();
        }

        void SetWideView(bool wide)
        {
            pixelCamera.Mode = wide ? ViewMode.Wide : ViewMode.ZoomOut;
            if (!UsesRealSave) return;
            PlayerPrefs.SetInt(WideViewKey, wide ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>The pause menu with keys or a controller: up and down mark a button, Enter, Space or A press it, Esc, B or Start go back.</summary>
        void ReadMenuInput(Keyboard keyboard, Gamepad gamepad)
        {
            var menu = hud.PauseMenu;
            int dx = 0, dy = 0;
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) dy--;
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) dy++;
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) dx--;
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) dx++;
            }
            if (gamepad != null)
            {
                if (gamepad.dpad.up.wasPressedThisFrame) dy--;
                if (gamepad.dpad.down.wasPressedThisFrame) dy++;
                if (gamepad.dpad.left.wasPressedThisFrame) dx--;
                if (gamepad.dpad.right.wasPressedThisFrame) dx++;
                // The stick counts once each time it is pushed over.
                var stick = gamepad.leftStick.ReadValue();
                var pushed = new Vector2Int(stick.x > 0.6f ? 1 : stick.x < -0.6f ? -1 : 0, stick.y > 0.6f ? -1 : stick.y < -0.6f ? 1 : 0);
                if (pushed.x != menuStick.x) dx += pushed.x;
                if (pushed.y != menuStick.y) dy += pushed.y;
                menuStick = pushed;
            }
            if (dy != 0) menu.Move(dy);
            if (dx != 0) menu.Side(dx);
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) ||
                gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
                menu.Activate();
            else if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame ||
                     gamepad != null && (gamepad.buttonEast.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame))
                menu.Back();
        }

        IEnumerator Execute(HeroCommand command)
        {
            busy = true;
            bool usedTurn = run.Execute(command);
            if (!usedTurn) ExplainRefusal(command);
            var events = run.Events.ToArray(); // The run reuses its list on the next action.
            if (events.Any(e => e is ExpGainedEvent || e is RunEndedEvent)) SaveSystem.SaveParty(party);
            yield return view.Play(run, events);
            hud.Refresh(run);
            if (run.State != RunState.InProgress) hud.ShowRunEnd(run, levelsAtStart);
            busy = false;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && party != null) SaveSystem.SaveParty(party); // Mobile apps can be killed while in the background.
        }

        void OnApplicationQuit()
        {
            if (party != null) SaveSystem.SaveParty(party);
        }

        void ExplainRefusal(HeroCommand command)
        {
            if (run.State != RunState.InProgress) return;
            switch (command.Kind)
            {
                case HeroCommandKind.Attack:
                    // A tapped enemy that moved away or fell before the tap's turn came.
                    hud.AddMessage(NoAttackTargetMessage(run.Hero), DungeonHud.HintColor);
                    break;
                case HeroCommandKind.UseBerry:
                    hud.AddMessage(run.Berries == 0 ? "You have no berries." : BerryFullMessage(run.Config), DungeonHud.HintColor);
                    break;
                case HeroCommandKind.Descend:
                    hud.AddMessage(run.IsBossFloor ? "No way down here. Defeat the boss!" : "The way down is the cave entrance.", DungeonHud.HintColor);
                    break;
                case HeroCommandKind.Skill:
                    hud.AddMessage(SkillRefusalMessage(run, command), DungeonHud.HintColor);
                    break;
                case HeroCommandKind.Ultimate:
                    hud.AddMessage(UltimateRefusalMessage(run, command), DungeonHud.HintColor);
                    break;
            }
        }

        /// <summary>Changes one partner's tactic: Attack, then Follow, then Hold. Takes no time.</summary>
        void CycleTactic(int partyIndex)
        {
            if (run == null || partyIndex < 0 || partyIndex >= run.Party.Count) return;
            var member = run.Party[partyIndex];
            if (member == run.Hero || !member.IsAlive) return;
            var next = NextTactic(member.Tactic);
            run.SetTactic(member, next);
            tactics[partyIndex] = next;
            hud.AddMessage($"{member.Name} will {TacticVerb(next)}.", DungeonHud.HintColor);
            hud.Refresh(run);
        }

        /// <summary>The keyboard and controller shortcut: every partner switches to the next tactic together.</summary>
        void CycleAllTactics()
        {
            Actor first = null;
            foreach (var member in run.Party)
                if (member != run.Hero && member.IsAlive) { first = member; break; }
            if (first == null) return;
            var next = NextTactic(first.Tactic);
            for (int i = 0; i < run.Party.Count; i++)
            {
                if (run.Party[i] == run.Hero) continue;
                run.SetTactic(run.Party[i], next);
                tactics[i] = next;
            }
            hud.AddMessage($"Partners will {TacticVerb(next)}.", DungeonHud.HintColor);
            hud.Refresh(run);
        }

        static PartyTactic NextTactic(PartyTactic tactic) => (PartyTactic)(((int)tactic + 1) % 3);

        static string TacticVerb(PartyTactic tactic) =>
            tactic == PartyTactic.Attack ? "go after enemies" : tactic == PartyTactic.Follow ? "stay close and follow" : "hold their ground";

        /// <summary>The next living party member after the leader, for the switch-hero key.</summary>
        int NextLeaderIndex()
        {
            int current = 0;
            for (int i = 0; i < run.Party.Count; i++)
                if (run.Party[i] == run.Hero) current = i;
            for (int step = 1; step < run.Party.Count; step++)
            {
                int index = (current + step) % run.Party.Count;
                if (run.Party[index].IsAlive) return index;
            }
            return current;
        }

        static string BerryFullMessage(DungeonRunConfig config) =>
            config.BerryHealHp > 0 ? "HP is already full." : "Berries do nothing in this dungeon.";

        /// <summary>Why a skill command can't be carried out right now, for the message log.</summary>
        public static string SkillRefusalMessage(DungeonRun run, HeroCommand command)
        {
            int slot = command.Slot;
            var skills = run.Hero.Skills;
            if (slot < 0 || slot >= skills.Count) return "No skill in that slot yet.";
            var skill = skills[slot];
            var check = command.Targeted ? run.CheckSkillAt(run.Hero, slot, command.Target)
                : command.Aimed ? run.CheckSkill(slot, command.Direction)
                : run.CheckSkill(slot);
            switch (check)
            {
                case SkillCheck.OnCooldown:
                    int turns = run.Hero.SkillCooldowns[slot];
                    return turns == 1 ? $"{skill.Name} was just used: it's ready again next turn." : $"{skill.Name} is recharging: {turns} more turns.";
                case SkillCheck.NoTarget: return NoTargetMessage(skill);
                case SkillCheck.NotNeeded:
                    return skill.HealTarget == HealTarget.SelfOrAdjacentAlly ? "Nobody next to you needs healing."
                        : skill.Radius > 0 ? "Everyone nearby is at full HP." : "HP is already full.";
                case SkillCheck.Blocked: return skill.RollTiles > 0 ? "No room to roll that way." : "No room to dash there.";
                default: return $"{skill.Name} can't be used right now.";
            }
        }

        /// <summary>Why the ultimate can't be used right now, for the message log.</summary>
        public static string UltimateRefusalMessage(DungeonRun run, HeroCommand command)
        {
            var hero = run.Hero;
            var ultimate = hero.Ultimate;
            if (ultimate == null) return $"{hero.Name} has no ultimate.";
            var check = command.Targeted ? run.CheckUltimateAt(hero, command.Target)
                : command.Aimed ? run.CheckUltimate(command.Direction)
                : run.CheckUltimate();
            switch (check)
            {
                case SkillCheck.NotCharged: return $"{ultimate.Name} is charging ({hero.Charge}%): it fills as {hero.Name} acts, hits and gets hit.";
                case SkillCheck.NoTarget: return NoTargetMessage(ultimate);
                default: return $"{ultimate.Name} can't be used right now.";
            }
        }

        static string NoTargetMessage(SkillDefinition skill) =>
            skill.Effect == SkillEffect.Strike && skill.DashTiles > 0 ? $"No enemy within {skill.StrikeReach} tiles in a straight line for {skill.Name}."
                : skill.Effect == SkillEffect.Strike || skill.Effect == SkillEffect.SharedStrikes ? $"No enemy next to you for {skill.Name}." : $"No enemy in sight within {skill.Range} tiles for {skill.Name}.";

        /// <summary>Why the weapon attack has nothing to aim at, for the message log.</summary>
        public static string NoAttackTargetMessage(Actor hero) =>
            hero.Definition.IsRanged ? $"No enemy in sight within {hero.Definition.AttackRange} tiles for {hero.AttackName}."
            : $"No enemy next to you for {hero.AttackName}.";

        static string OutOfReachMessage(Actor hero, Actor foe) =>
            hero.Definition.IsRanged ? $"The {foe.Name} is out of {hero.AttackName}'s reach or out of sight."
            : $"The {foe.Name} is too far for {hero.AttackName}: step next to it first.";

        // ---- Input ----

        /// <summary>Switches the HUD between touch controls and keyboard/controller hints based on what was used last.</summary>
        void UpdateInputMode(Keyboard keyboard, Gamepad gamepad)
        {
            var mode = inputMode;
            if (gamepad != null && GamepadActive(gamepad)) mode = InputMode.Gamepad;
            else if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) mode = InputMode.Keyboard;
            else if (PointerPressed()) mode = InputMode.Touch;
            if (mode == inputMode) return;
            inputMode = mode;
            hud.SetInputMode(mode);
        }

        static bool GamepadActive(Gamepad gamepad) =>
            gamepad.leftStick.ReadValue().magnitude > StickDeadZone || gamepad.dpad.ReadValue().sqrMagnitude > 0.25f ||
            gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonNorth.wasPressedThisFrame ||
            gamepad.buttonWest.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame ||
            gamepad.rightShoulder.wasPressedThisFrame || gamepad.leftShoulder.wasPressedThisFrame ||
            gamepad.leftTrigger.wasPressedThisFrame || gamepad.rightTrigger.wasPressedThisFrame ||
            gamepad.startButton.wasPressedThisFrame || gamepad.selectButton.wasPressedThisFrame ||
            gamepad.buttonEast.wasPressedThisFrame || gamepad.leftStickButton.wasPressedThisFrame || gamepad.rightStickButton.wasPressedThisFrame;

        static bool PointerPressed() =>
            Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame ||
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        void ReadButtons(Keyboard keyboard, Gamepad gamepad)
        {
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame || gamepad != null && gamepad.selectButton.wasPressedThisFrame)
                SetAutoPilot(!autoPilot);
            // Esc opens the pause menu unless it is letting go of an aim; Start always does.
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && aiming == null || gamepad != null && gamepad.startButton.wasPressedThisFrame)
            {
                SetPaused(true);
                return;
            }

            if (aiming != null)
            {
                // Aiming: Space, Enter or A fires at the marked target; Esc, B or a right click lets it go.
                if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) ||
                    gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
                    confirmAim = true;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame || gamepad != null && gamepad.buttonEast.wasPressedThisFrame ||
                    Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                    cancelAim = true;
                if (confirmAim || cancelAim) return;
            }
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) buffered = HeroCommand.Attack;
                else if (keyboard.digit1Key.wasPressedThisFrame) buffered = HeroCommand.Skill(0);
                else if (keyboard.digit2Key.wasPressedThisFrame) buffered = HeroCommand.Skill(1);
                else if (keyboard.digit3Key.wasPressedThisFrame) buffered = HeroCommand.Skill(2);
                else if (keyboard.digit4Key.wasPressedThisFrame) buffered = HeroCommand.UltimateFacing;
                else if (keyboard.tabKey.wasPressedThisFrame) buffered = HeroCommand.SwitchLeader(NextLeaderIndex());
                else if (keyboard.gKey.wasPressedThisFrame) CycleAllTactics();
                else if (keyboard.xKey.wasPressedThisFrame || keyboard.periodKey.wasPressedThisFrame) buffered = HeroCommand.Wait;
                else if (keyboard.bKey.wasPressedThisFrame) buffered = HeroCommand.UseBerry;
                else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) buffered = HeroCommand.Descend;
            }
            if (gamepad != null)
            {
                if (gamepad.buttonSouth.wasPressedThisFrame) buffered = HeroCommand.Attack;
                else if (gamepad.leftShoulder.wasPressedThisFrame) buffered = HeroCommand.Skill(0);
                else if (gamepad.leftTrigger.wasPressedThisFrame) buffered = HeroCommand.Skill(1);
                else if (gamepad.rightTrigger.wasPressedThisFrame) buffered = HeroCommand.Skill(2);
                else if (gamepad.rightShoulder.wasPressedThisFrame) buffered = HeroCommand.UltimateFacing;
                else if (gamepad.buttonNorth.wasPressedThisFrame) buffered = HeroCommand.Wait;
                else if (gamepad.buttonWest.wasPressedThisFrame) buffered = HeroCommand.UseBerry;
                else if (gamepad.rightStickButton.wasPressedThisFrame) buffered = HeroCommand.Descend;
                else if (gamepad.buttonEast.wasPressedThisFrame) buffered = HeroCommand.SwitchLeader(NextLeaderIndex());
                else if (gamepad.leftStickButton.wasPressedThisFrame) CycleAllTactics();
            }
        }

        /// <summary>Held direction from the touch D-pad, keyboard or controller. Holding keeps walking, one tile per step animation.</summary>
        Direction8? ReadHeldDirection(Keyboard keyboard, Gamepad gamepad)
        {
            if (hud.DPad.isActiveAndEnabled && hud.DPad.Direction.HasValue) return hud.DPad.Direction;

            if (gamepad != null)
            {
                var stick = gamepad.leftStick.ReadValue();
                if (stick.magnitude > StickDeadZone)
                {
                    float angle = Mathf.Atan2(stick.x, stick.y) * Mathf.Rad2Deg; // 0° = up, clockwise like Direction8.
                    return (Direction8)(Mathf.RoundToInt((angle + 360f) % 360f / 45f) % 8);
                }
            }

            int dx = 0, dy = 0;
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed || keyboard.numpad8Key.isPressed) dy++;
                if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed || keyboard.numpad2Key.isPressed) dy--;
                if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed || keyboard.numpad4Key.isPressed) dx--;
                if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed || keyboard.numpad6Key.isPressed) dx++;
                if (keyboard.qKey.isPressed || keyboard.numpad7Key.isPressed) { dx--; dy++; }
                if (keyboard.eKey.isPressed || keyboard.numpad9Key.isPressed) { dx++; dy++; }
                if (keyboard.zKey.isPressed || keyboard.numpad1Key.isPressed) { dx--; dy--; }
                if (keyboard.cKey.isPressed || keyboard.numpad3Key.isPressed) { dx++; dy--; }
            }
            if (gamepad != null)
            {
                var dpad = gamepad.dpad.ReadValue();
                dx += Mathf.RoundToInt(dpad.x);
                dy += Mathf.RoundToInt(dpad.y);
            }

            if (!Directions.TryFromDelta(dx, dy, out var direction))
            {
                directionHeldFor = 0f;
                return null;
            }
            directionHeldFor += Time.deltaTime;
            return directionHeldFor >= ComboWindow ? direction : (Direction8?)null;
        }

        static PixelCamera SetUpCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
            }
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(22, 28, 46, 255); // The pack's outline colour; the raised ground covers it.
            return camera.TryGetComponent<PixelCamera>(out var existing) ? existing : camera.gameObject.AddComponent<PixelCamera>();
        }
    }
}

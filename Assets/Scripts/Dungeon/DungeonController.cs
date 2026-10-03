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
    /// next. Choosing an attack, skill or ultimate that needs a target starts aiming (PROGRESSION.md, "Attack range
    /// highlight"): its reach and valid targets light up, and the player taps a target, or presses the action again to
    /// fire at the marked one; holding a direction while pressing fires that way at once, and bumping into an adjacent
    /// enemy still attacks right away. Touch: on-screen D-pad and buttons; tap a party card to lead that hero, tap a
    /// tactic badge to change it. Keyboard: WASD/arrows plus Q/E/Z/C or the numpad for 8 directions, Space attack,
    /// 1/2/3 skills, 4 ultimate, Tab switch hero, G partners' tactics, X wait, B berry, Enter stairs, T auto, R restart;
    /// while aiming, directions pick a target, Space or the same key fires, Esc cancels. Controller: left stick or
    /// D-pad, A attack, LB/LT/RT skills, RB ultimate, B switch hero, L3 tactics, Y wait, X berry, Start stairs, View
    /// auto, A or Start to restart; while aiming, A fires and B cancels.
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
        InputMode inputMode = InputMode.Touch;

        /// <summary>The action being aimed, or null.</summary>
        Aiming aiming;
        Direction8? aimHeld;
        bool confirmAim, cancelAim;
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

            public HeroCommand CommandFor(AimOption option) => AimedAt(Kind, Slot, option.Direction);
        }

        public DungeonRun Run => run;
        public bool IsIdle => !busy && run != null && run.State == RunState.InProgress;

        /// <summary>True while an action's animations are playing.</summary>
        public bool IsAnimating => busy;

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

            pixelCamera = SetUpCamera();
            hud = DungeonHud.Create(pixelCamera.GetComponent<Camera>());
            hud.CommandRequested += command => buffered = command;
            hud.RestartRequested += StartNewRun;
            hud.AutoPilotToggled += () => SetAutoPilot(!autoPilot);
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

        void StartNewRun()
        {
            StopAllCoroutines();
            view.StopAllCoroutines();
            int runSeed = seed != 0 ? seed : Random.Range(1, int.MaxValue);
            var config = new DungeonRunConfig { FloorCount = options.FloorCount ?? floorCount };
            run = new DungeonRun(runSeed, config, party);
            levelsAtStart = party.Select(hero => hero.Level).ToArray();
            for (int i = 0; i < run.Party.Count; i++) run.SetTactic(run.Party[i], tactics[i]);
            Debug.Log($"Dungeon run started with seed {runSeed}, party: {string.Join(", ", party.Select(hero => hero.ToString()))}.");
            busy = false;
            buffered = null;
            EndAiming();
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

            ReadButtons(keyboard, gamepad);
            var held = ReadHeldDirection(keyboard, gamepad);
            var tapped = ReadTappedTile();
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
                if (held.HasValue) command = AimedAt(command.Value.Kind, command.Value.Slot, held.Value); // Held direction: fire that way at once.
                else if (TryStartAiming(command.Value)) return;
            }
            if (autoPilot && command.HasValue && !AllowedDuringAuto(command.Value)) hud.ShowAutoPilotBlocked();
            command = ChooseCommand(command, autoPilot, run);
            if (command.HasValue) StartCoroutine(Execute(command.Value));
        }

        // ---- Aiming (PROGRESSION.md, "Attack range highlight") ----

        /// <summary>An attack, skill or ultimate that hasn't been pointed anywhere yet.</summary>
        static bool IsAimable(HeroCommand command) =>
            !command.Aimed && (command.Kind == HeroCommandKind.Attack || command.Kind == HeroCommandKind.Skill || command.Kind == HeroCommandKind.Ultimate);

        static HeroCommand AimedAt(HeroCommandKind kind, int slot, Direction8 direction) =>
            kind == HeroCommandKind.Attack ? HeroCommand.AttackToward(direction)
            : kind == HeroCommandKind.Ultimate ? HeroCommand.Ultimate(direction)
            : HeroCommand.Skill(slot, direction);

        /// <summary>
        /// Starts aiming when the action has targets to choose from. False when there's nothing to choose (no target in
        /// reach, an action that isn't aimed, or one that can't be used now): then it just goes ahead, and the log says why
        /// if the run refuses it.
        /// </summary>
        bool TryStartAiming(HeroCommand command)
        {
            var hero = run.Hero;
            SkillDefinition skill = null;
            if (command.Kind == HeroCommandKind.Skill)
            {
                if (run.CheckSkill(command.Slot) is SkillCheck.NoSkill or SkillCheck.OnCooldown) return false;
                skill = hero.Definition.Skills[command.Slot];
            }
            else if (command.Kind == HeroCommandKind.Ultimate)
            {
                if (run.CheckUltimate() is SkillCheck.NoSkill or SkillCheck.NotCharged) return false;
                skill = hero.Definition.Ultimate;
            }
            var aim = run.AimFor(hero, skill);
            if (!aim.NeedsAim || aim.Options.Count == 0) return false;

            aiming = new Aiming(command.Kind, command.Slot, aim, DefaultOption(aim, hero));
            aimHeld = null;
            confirmAim = cancelAim = false;
            view.ShowAim(run, aim, aiming.Selected);
            hud.SetAiming(aiming.Button, AimPrompt(aim, skill));
            return true;
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
                if (IsAimable(next) && (!autoPilot || AllowedDuringAuto(next)) && TryStartAiming(next)) return null;
                if (autoPilot && !AllowedDuringAuto(next))
                {
                    hud.ShowAutoPilotBlocked();
                    return null;
                }
                return next;
            }
            if (tapped.HasValue)
            {
                var options = aiming.Aim.Options;
                for (int i = 0; i < options.Count; i++)
                    if (options[i].Tile == tapped.Value) return Fire(i);
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
            var command = aiming.CommandFor(aiming.Aim.Options[option]);
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

        /// <summary>Marks the target in that direction, or the one closest to it.</summary>
        void SelectToward(Direction8 direction)
        {
            var options = aiming.Aim.Options;
            int best = aiming.Selected, bestTurn = int.MaxValue;
            for (int i = 0; i < options.Count; i++)
            {
                int turn = Math.Abs((int)options[i].Direction - (int)direction) % 8;
                turn = Math.Min(turn, 8 - turn);
                if (turn >= bestTurn) continue;
                bestTurn = turn;
                best = i;
            }
            aiming.Selected = best;
            view.ShowAim(run, aiming.Aim, best);
        }

        /// <summary>The target marked first: the one the hero faces; else the nearest; for a roll, the spot farthest from foes.</summary>
        int DefaultOption(AimInfo aim, Actor hero)
        {
            int best = 0, bestScore = int.MinValue;
            for (int i = 0; i < aim.Options.Count; i++)
            {
                var option = aim.Options[i];
                int score = aim.PicksTile ? run.DistanceToNearestFoe(option.Tile, hero.Team)
                    : option.Direction == hero.Facing ? int.MaxValue
                    : -GridPos.ChebyshevDistance(option.Tile, hero.Pos);
                if (score <= bestScore) continue;
                bestScore = score;
                best = i;
            }
            return best;
        }

        string AimPrompt(AimInfo aim, SkillDefinition skill)
        {
            string name = skill?.Name ?? run.Hero.Definition.AttackName;
            string what = !aim.PicksTile ? "a target" : skill?.Effect == SkillEffect.Dash ? "where to dash" : "where to roll";
            switch (inputMode)
            {
                case InputMode.Keyboard: return $"{name}: pick {what} with the arrows, press again or Space to fire, Esc to cancel.";
                case InputMode.Gamepad: return $"{name}: pick {what} with the stick, press again or A to fire, B to cancel.";
                default: return $"{name}: tap {what}, or tap {name} again for the marked one.";
            }
        }

        /// <summary>The map tile tapped or clicked this frame, if it wasn't on a HUD control.</summary>
        GridPos? ReadTappedTile()
        {
            Vector2 screen;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                screen = Touchscreen.current.primaryTouch.position.ReadValue();
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                screen = Mouse.current.position.ReadValue();
            else
                return null;
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
        /// are ignored, except skills and the ultimate, which the player can still fire by hand, and switching which hero
        /// leads (so another hero's skills can be fired). The auto-pilot carries on afterwards.
        /// </summary>
        public static HeroCommand? ChooseCommand(HeroCommand? playerCommand, bool autoPilot, DungeonRun run) =>
            !autoPilot ? playerCommand
            : playerCommand.HasValue && AllowedDuringAuto(playerCommand.Value) ? playerCommand
            : AutoPilot.Decide(run);

        static bool AllowedDuringAuto(HeroCommand command) =>
            command.Kind == HeroCommandKind.Skill || command.Kind == HeroCommandKind.Ultimate || command.Kind == HeroCommandKind.SwitchLeader;

        /// <summary>The hero plays itself (the same AutoPilot the tests use). Only the player turns it off: Auto, T or View.</summary>
        void SetAutoPilot(bool on)
        {
            if (autoPilot == on) return;
            autoPilot = on;
            hud.SetAutoPilot(on);
            hud.AddMessage(on ? "Auto-pilot on. Press Auto again to take over." : "Auto-pilot off.", DungeonHud.HintColor);
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
                case HeroCommandKind.UseBerry:
                    hud.AddMessage(run.Berries == 0 ? "You have no berries." : BerryFullMessage(run.Config), DungeonHud.HintColor);
                    break;
                case HeroCommandKind.Descend:
                    hud.AddMessage(run.IsBossFloor ? "No stairs here. Defeat the boss!" : "There are no stairs here.", DungeonHud.HintColor);
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
            var skills = run.Hero.Definition.Skills;
            if (slot < 0 || slot >= skills.Count) return "No skill in that slot yet.";
            var skill = skills[slot];
            switch (command.Aimed ? run.CheckSkill(slot, command.Direction) : run.CheckSkill(slot))
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
            var ultimate = hero.Definition.Ultimate;
            if (ultimate == null) return $"{hero.Name} has no ultimate.";
            switch (command.Aimed ? run.CheckUltimate(command.Direction) : run.CheckUltimate())
            {
                case SkillCheck.NotCharged: return $"{ultimate.Name} is charging ({hero.Charge}%): it fills as {hero.Name} acts, hits and gets hit.";
                case SkillCheck.NoTarget: return NoTargetMessage(ultimate);
                default: return $"{ultimate.Name} can't be used right now.";
            }
        }

        static string NoTargetMessage(SkillDefinition skill) =>
            skill.Effect == SkillEffect.Strike ? $"No enemy next to you for {skill.Name}." : $"No enemy in line within {skill.Range} tiles for {skill.Name}.";

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
            gamepad.buttonEast.wasPressedThisFrame || gamepad.leftStickButton.wasPressedThisFrame;

        static bool PointerPressed() =>
            Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame ||
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        void ReadButtons(Keyboard keyboard, Gamepad gamepad)
        {
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame || gamepad != null && gamepad.selectButton.wasPressedThisFrame)
                SetAutoPilot(!autoPilot);

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
                else if (gamepad.startButton.wasPressedThisFrame) buffered = HeroCommand.Descend;
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
            camera.backgroundColor = new Color32(12, 10, 18, 255);
            return camera.TryGetComponent<PixelCamera>(out var existing) ? existing : camera.gameObject.AddComponent<PixelCamera>();
        }
    }
}

using System.Collections;
using System.Linq;
using FiveKingdoms.Core;
using FiveKingdoms.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FiveKingdoms.Dungeon
{
    /// <summary>
    /// Entry point of the dungeon scene. Owns the rules (DungeonRun), the visuals (DungeonView), the HUD and the
    /// hero's saved progress, and turns input into hero commands. Input is read only while nothing is animating,
    /// which keeps the game strictly turn-based; a button pressed during an animation is buffered and runs next.
    /// Touch: on-screen D-pad and buttons. Keyboard: WASD/arrows plus Q/E/Z/C or the numpad for 8 directions,
    /// Space attack, X wait, B berry, Enter stairs, R restart. Controller: left stick or D-pad, A attack, Y wait,
    /// X berry, RB stairs, A or Start to restart.
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
        HeroProgress progress;
        DungeonRun run;
        DungeonView view;
        DungeonHud hud;
        PixelCamera pixelCamera;
        bool busy;
        HeroCommand? buffered;
        float directionHeldFor;
        int levelAtStart;
        bool autoPilot;
        InputMode inputMode = InputMode.Touch;

        public DungeonRun Run => run;
        public bool IsIdle => !busy && run != null && run.State == RunState.InProgress;

        /// <summary>True while an action's animations are playing.</summary>
        public bool IsAnimating => busy;

        /// <summary>The Auto button: the hero plays itself until this is turned off or the player acts.</summary>
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
            progress = options.StartLevel.HasValue
                ? new HeroProgress(ActorCatalog.Uzuki, options.StartLevel.Value)
                : SaveSystem.LoadHero(ActorCatalog.Uzuki);

            pixelCamera = SetUpCamera();
            hud = DungeonHud.Create(pixelCamera.GetComponent<Camera>());
            hud.CommandRequested += command => buffered = command;
            hud.RestartRequested += StartNewRun;
            hud.AutoPilotToggled += () => SetAutoPilot(!autoPilot);
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
            run = new DungeonRun(runSeed, config, progress);
            levelAtStart = progress.Level;
            Debug.Log($"Dungeon run started with seed {runSeed}, {progress}.");
            busy = false;
            buffered = null;
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
                bool restart = keyboard != null && (keyboard.rKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) ||
                               gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);
                if (!busy && restart) StartNewRun();
                return;
            }

            ReadButtons(keyboard, gamepad);
            var held = ReadHeldDirection(keyboard, gamepad);
            if (busy) return;

            var command = buffered;
            buffered = null;
            if (command == null && held.HasValue) command = HeroCommand.Move(held.Value);
            if (autoPilot)
            {
                if (command.HasValue) SetAutoPilot(false); // Any input from the player takes back control.
                else command = AutoPilot.Decide(run);
            }
            if (command.HasValue) StartCoroutine(Execute(command.Value));
        }

        /// <summary>The hero plays itself (the same AutoPilot the tests use) until toggled off or the player acts.</summary>
        void SetAutoPilot(bool on)
        {
            if (autoPilot == on) return;
            autoPilot = on;
            hud.SetAutoPilot(on);
            hud.AddMessage(on ? "Auto-pilot on. Press any action to take over." : "Auto-pilot off.", DungeonHud.HintColor);
        }

        IEnumerator Execute(HeroCommand command)
        {
            busy = true;
            bool usedTurn = run.Execute(command);
            if (!usedTurn) ExplainRefusal(command);
            var events = run.Events.ToArray(); // The run reuses its list on the next action.
            if (events.Any(e => e is ExpGainedEvent || e is RunEndedEvent)) SaveSystem.SaveHero(progress);
            yield return view.Play(run, events);
            hud.Refresh(run);
            if (run.State != RunState.InProgress) hud.ShowRunEnd(run, levelAtStart);
            busy = false;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && progress != null) SaveSystem.SaveHero(progress); // Mobile apps can be killed while in the background.
        }

        void OnApplicationQuit()
        {
            if (progress != null) SaveSystem.SaveHero(progress);
        }

        void ExplainRefusal(HeroCommand command)
        {
            if (run.State != RunState.InProgress) return;
            switch (command.Kind)
            {
                case HeroCommandKind.UseBerry:
                    hud.AddMessage(run.Berries == 0 ? "You have no berries." : "HP is already full.", DungeonHud.HintColor);
                    break;
                case HeroCommandKind.Descend:
                    hud.AddMessage(run.IsBossFloor ? "No stairs here. Defeat the boss!" : "There are no stairs here.", DungeonHud.HintColor);
                    break;
            }
        }

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
            gamepad.rightShoulder.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame ||
            gamepad.selectButton.wasPressedThisFrame;

        static bool PointerPressed() =>
            Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame ||
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        void ReadButtons(Keyboard keyboard, Gamepad gamepad)
        {
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame || gamepad != null && gamepad.selectButton.wasPressedThisFrame)
                SetAutoPilot(!autoPilot);

            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) buffered = HeroCommand.Attack;
                else if (keyboard.xKey.wasPressedThisFrame || keyboard.periodKey.wasPressedThisFrame) buffered = HeroCommand.Wait;
                else if (keyboard.bKey.wasPressedThisFrame) buffered = HeroCommand.UseBerry;
                else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) buffered = HeroCommand.Descend;
            }
            if (gamepad != null)
            {
                if (gamepad.buttonSouth.wasPressedThisFrame) buffered = HeroCommand.Attack;
                else if (gamepad.buttonNorth.wasPressedThisFrame) buffered = HeroCommand.Wait;
                else if (gamepad.buttonWest.wasPressedThisFrame) buffered = HeroCommand.UseBerry;
                else if (gamepad.rightShoulder.wasPressedThisFrame) buffered = HeroCommand.Descend;
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

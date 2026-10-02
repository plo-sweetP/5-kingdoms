using System.Collections;
using System.Linq;
using FiveKingdoms.Core;
using FiveKingdoms.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FiveKingdoms.Dungeon
{
    /// <summary>
    /// Entry point of the dungeon scene. Owns the rules (DungeonRun), the visuals (DungeonView) and the HUD,
    /// and turns input into hero commands. Input is read only while nothing is animating, which keeps the
    /// game strictly turn-based; a button tapped during an animation is buffered and runs next.
    /// Keyboard (editor and desktop): WASD/arrows plus Q/E/Z/C or the numpad for 8 directions,
    /// Space attack, X wait, B berry, Enter stairs, R restart after a run ends.
    /// </summary>
    public sealed class DungeonController : MonoBehaviour
    {
        const float ComboWindow = 0.06f; // Lets two arrow keys pressed together count as one diagonal.

        [Tooltip("Fixed seed for reproducible runs. 0 picks a new random seed every run.")]
        [SerializeField] int seed;
        [SerializeField] int floorCount = 5;

        DungeonRun run;
        DungeonView view;
        DungeonHud hud;
        PixelCamera pixelCamera;
        bool busy;
        HeroCommand? buffered;
        float directionHeldFor;

        public DungeonRun Run => run;
        public bool IsIdle => !busy && run != null && run.State == RunState.InProgress;

        /// <summary>True while an action's animations are playing.</summary>
        public bool IsAnimating => busy;

        void Awake()
        {
            Application.targetFrameRate = 60;
            pixelCamera = SetUpCamera();
            hud = DungeonHud.Create(pixelCamera.GetComponent<Camera>());
            hud.CommandRequested += command => buffered = command;
            hud.RestartRequested += StartNewRun;
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
            run = new DungeonRun(runSeed, new DungeonRunConfig { FloorCount = floorCount });
            Debug.Log($"Dungeon run started with seed {runSeed}.");
            busy = false;
            buffered = null;
            hud.HideRunEnd();
            hud.ClearLog();
            StartCoroutine(hud.Fade(0f, 0.01f));
            view.Rebuild(run);
            hud.Refresh(run);
            hud.ShowBanner(run.Config.Name, $"B{run.Floor}F");
        }

        void Update()
        {
            if (run == null) return;
            var keyboard = Keyboard.current;
            if (run.State != RunState.InProgress)
            {
                if (!busy && keyboard != null && keyboard.rKey.wasPressedThisFrame) StartNewRun();
                return;
            }

            ReadKeyboardButtons(keyboard);
            var held = ReadHeldDirection(keyboard);
            if (busy) return;

            var command = buffered;
            buffered = null;
            if (command == null && held.HasValue) command = HeroCommand.Move(held.Value);
            if (command.HasValue) StartCoroutine(Execute(command.Value));
        }

        IEnumerator Execute(HeroCommand command)
        {
            busy = true;
            bool usedTurn = run.Execute(command);
            if (!usedTurn) ExplainRefusal(command);
            var events = run.Events.ToArray(); // The run reuses its list on the next action.
            yield return view.Play(run, events);
            hud.Refresh(run);
            busy = false;
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
                    hud.AddMessage("There are no stairs here.", DungeonHud.HintColor);
                    break;
            }
        }

        void ReadKeyboardButtons(Keyboard keyboard)
        {
            if (keyboard == null) return;
            if (keyboard.spaceKey.wasPressedThisFrame) buffered = HeroCommand.Attack;
            else if (keyboard.xKey.wasPressedThisFrame || keyboard.periodKey.wasPressedThisFrame) buffered = HeroCommand.Wait;
            else if (keyboard.bKey.wasPressedThisFrame) buffered = HeroCommand.UseBerry;
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) buffered = HeroCommand.Descend;
        }

        /// <summary>Held direction from the touch D-pad or keyboard. Holding keeps walking, one tile per step animation.</summary>
        Direction8? ReadHeldDirection(Keyboard keyboard)
        {
            if (hud.DPad.Direction.HasValue) return hud.DPad.Direction;
            if (keyboard == null) return null;

            int dx = 0, dy = 0;
            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed || keyboard.numpad8Key.isPressed) dy++;
            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed || keyboard.numpad2Key.isPressed) dy--;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed || keyboard.numpad4Key.isPressed) dx--;
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed || keyboard.numpad6Key.isPressed) dx++;
            if (keyboard.qKey.isPressed || keyboard.numpad7Key.isPressed) { dx--; dy++; }
            if (keyboard.eKey.isPressed || keyboard.numpad9Key.isPressed) { dx++; dy++; }
            if (keyboard.zKey.isPressed || keyboard.numpad1Key.isPressed) { dx--; dy--; }
            if (keyboard.cKey.isPressed || keyboard.numpad3Key.isPressed) { dx++; dy--; }

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

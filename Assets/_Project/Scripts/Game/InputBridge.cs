using Metropolis.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Metropolis.Game
{
    /// <summary>Reads keyboard and mouse and forwards them. Holds no game logic.</summary>
    public sealed class InputBridge : MonoBehaviour
    {
        const float RayDistance = 5000f;

        [SerializeField] GameController game;
        [SerializeField] CameraRig rig;
        [SerializeField] Camera view;
        [SerializeField] LayerMask terrainMask;
        [SerializeField] GameUi ui;

        SimulationSpeed _lastSpeed = SimulationSpeed.X1;

        // A GameUi that failed to build disables itself; the game then runs on hotkeys alone.
        GameUi Interface => ui != null && ui.isActiveAndEnabled ? ui : null;

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            // Batch-mode test runs have no input devices.
            if (keyboard == null || mouse == null || game.Simulation == null)
                return;

            var live = Interface;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (live != null) live.HandleEscape();
                else game.Tools.Cancel();
            }

            if (live != null && live.MenuOpen)
            {
                // Menus own the screen: no hover preview, hotkeys or camera behind them.
                game.Tools.PointerMove(null);
                return;
            }

            var position = mouse.position.ReadValue();
            bool overUi = live != null && live.IsScreenPointOverUi(position);
            ReadCamera(keyboard, mouse, overUi);
            ReadTools(keyboard);
            ReadSpeed(keyboard, live);
            if (live != null && keyboard.f5Key.wasPressedThisFrame)
                live.Save();
            ReadPointer(mouse, position, overUi);
        }

        void OnApplicationFocus(bool focused)
        {
            // The mouse-up of a drag is lost when the window loses focus.
            if (!focused && game != null && game.Tools != null)
                game.Tools.CancelDrag();
        }

        void ReadCamera(Keyboard keyboard, Mouse mouse, bool overUi)
        {
            float dt = Time.unscaledDeltaTime;
            var pan = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            if (pan != Vector2.zero)
                rig.Pan(pan.normalized, dt);

            float turn = (keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f);
            if (turn != 0f)
                rig.Rotate(turn, dt);

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f && !overUi)
                rig.Zoom(scroll);
        }

        void ReadTools(Keyboard keyboard)
        {
            var tools = game.Tools;
            if (keyboard.digit1Key.wasPressedThisFrame) tools.SelectTool(BuildTool.Road);
            if (keyboard.digit2Key.wasPressedThisFrame) tools.SelectTool(BuildTool.Bulldoze);
            if (keyboard.digit3Key.wasPressedThisFrame) tools.SelectTool(BuildTool.ZoneResidential);
            if (keyboard.digit4Key.wasPressedThisFrame) tools.SelectTool(BuildTool.ZoneCommercial);
            if (keyboard.digit5Key.wasPressedThisFrame) tools.SelectTool(BuildTool.ZoneIndustrial);
            if (keyboard.digit6Key.wasPressedThisFrame) tools.SelectTool(BuildTool.PowerPlant);
            if (keyboard.digit7Key.wasPressedThisFrame) tools.SelectTool(BuildTool.WaterPump);
        }

        void ReadSpeed(Keyboard keyboard, GameUi live)
        {
            if (live != null)
            {
                // The interface owns speed, so keys and buttons share one remembered speed.
                if (keyboard.spaceKey.wasPressedThisFrame) live.TogglePause();
                if (keyboard.f1Key.wasPressedThisFrame) live.SetSpeed(SimulationSpeed.X1);
                if (keyboard.f2Key.wasPressedThisFrame) live.SetSpeed(SimulationSpeed.X2);
                if (keyboard.f3Key.wasPressedThisFrame) live.SetSpeed(SimulationSpeed.X4);
                return;
            }

            var clock = game.Clock;
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                if (clock.Speed == SimulationSpeed.Paused)
                {
                    clock.Speed = _lastSpeed;
                }
                else
                {
                    _lastSpeed = clock.Speed;
                    clock.Speed = SimulationSpeed.Paused;
                }
            }

            if (keyboard.f1Key.wasPressedThisFrame) clock.Speed = _lastSpeed = SimulationSpeed.X1;
            if (keyboard.f2Key.wasPressedThisFrame) clock.Speed = _lastSpeed = SimulationSpeed.X2;
            if (keyboard.f3Key.wasPressedThisFrame) clock.Speed = _lastSpeed = SimulationSpeed.X4;
        }

        void ReadPointer(Mouse mouse, Vector2 position, bool overUi)
        {
            var tools = game.Tools;

            // Over the interface the pointer counts as off the map, so a button click builds nothing.
            Cell? cell = null;
            if (!overUi && Physics.Raycast(view.ScreenPointToRay(position), out var hit, RayDistance, terrainMask)
                && game.Map.TryGetCell(hit.point, out var found))
                cell = found;

            tools.PointerMove(cell);
            if (mouse.rightButton.wasPressedThisFrame && !overUi)
                tools.Cancel();
            if (mouse.leftButton.wasPressedThisFrame)
                tools.PointerDown(cell);
            if (mouse.leftButton.wasReleasedThisFrame)
                tools.PointerUp(cell);
        }
    }
}

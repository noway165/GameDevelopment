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

        SimulationSpeed _lastSpeed = SimulationSpeed.X1;

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            // Batch-mode test runs have no input devices.
            if (keyboard == null || mouse == null || game.Simulation == null)
                return;

            ReadCamera(keyboard, mouse);
            ReadTools(keyboard);
            ReadSpeed(keyboard);
            ReadPointer(keyboard, mouse);
        }

        void ReadCamera(Keyboard keyboard, Mouse mouse)
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
            if (scroll != 0f)
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

        void ReadSpeed(Keyboard keyboard)
        {
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

        void ReadPointer(Keyboard keyboard, Mouse mouse)
        {
            var tools = game.Tools;
            Cell? cell = null;
            var ray = view.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out var hit, RayDistance, terrainMask) && game.Map.TryGetCell(hit.point, out var found))
                cell = found;

            tools.PointerMove(cell);
            if (keyboard.escapeKey.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
                tools.Cancel();
            if (mouse.leftButton.wasPressedThisFrame)
                tools.PointerDown(cell);
            if (mouse.leftButton.wasReleasedThisFrame)
                tools.PointerUp(cell);
        }
    }
}

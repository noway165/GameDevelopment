# Project METROPOLIS: Unity World and Construction Design

**Date:** 2026-11-10
**Status:** Awaiting review
**Source:** Project METROPOLIS Game Development Blueprint v1.0
**Builds on:** `docs/superpowers/specs/2026-11-09-simulation-core-design.md`

## 1. Intent

This is sub-project 2 of 3. It makes the finished simulation core visible and playable
in Unity 6.3 LTS (`6000.3.28f1`), with placeholder art.

**Success:** the player opens `GameWorld`, moves the camera, draws roads, paints zones,
places power and water, and watches buildings grow through the simulation.

The Editor script `WorldBuilder` generates the scene, prefabs, materials and building
assets, because the hand-built checklist proved too much for a first-time Unity user.
The results are ordinary Unity files that can be edited in the Editor afterwards, but
running the script again replaces them.

## 2. Scope

**In scope:** Terrain, camera, grid mapping, mouse and keyboard input, road, zone and
building tools with validity preview, views of roads, zones and buildings, pause and
speed control, and a temporary debug readout.

**Out of scope, for sub-project 3:** final HUD, menus, overlays, save and load keys,
player settings, audio, final art, and the Windows build.

**Out of scope entirely for the MVP:** undo and redo, drag-to-bulldoze, road junction
visuals, rebindable keys.

## 3. Division of work

| Owner | Work |
|---|---|
| Developer | Playtest, feel tuning in the Inspector, FPS measurement |
| Claude | All C#, `Packages/manifest.json`, tests, and `WorldBuilder`, which generates `GameWorld.unity`, Terrain, lighting, URP asset, placeholder prefabs (1 road, 3 zone, 7 building, 1 preview), 7 `BuildingAsset` files and Inspector references |

Scenes and prefabs use text serialization, so changes can be reviewed as diffs.

**Unity locks an open project.** Command-line test runs need the Editor closed. While
the Editor is open, tests run from its Test Runner window.

## 4. Project layout

```text
Assets/_Project/
├── Scripts/Game/                     Metropolis.Game.asmdef
├── Tests/Game/EditMode/              EditMode tests
├── Tests/Game/PlayMode/              PlayMode smoke test
├── Scenes/GameWorld.unity
├── Prefabs/{Roads,Zones,Buildings}/
└── ScriptableObjects/Buildings/
```

`Metropolis.Game` references `Metropolis.Simulation` and `Unity.InputSystem`. The core
never references `Metropolis.Game`.

Packages added to the manifest: Universal Render Pipeline, Input System, and the built-in
Terrain, Terrain Physics, Physics and IMGUI modules. Versions are taken from the
installed Editor. Active Input Handling is set to the Input System package.

## 5. Core change

The core validates commands only while executing them, so a preview cannot ask whether
an action is legal. Each command is split without changing its rules:

- `ICommand` gains `CommandResult Validate(SimContext ctx)`, which never changes state.
- `Execute` calls `Validate`, then applies the change.
- `Simulation` gains `CommandResult Validate(ICommand command)`.

## 6. Components

| Unit | Responsibility |
|---|---|
| `GameController` | Builds the catalog and `Simulation`, advances `SimulationClock` from unscaled time, validates its references at startup. |
| `BuildingAsset` | ScriptableObject with `Id`, `Kind`, `ConstructionCost`, `Maintenance`, `Capacity`, `PowerDemand`, `WaterDemand`, `Supply`, `Prefab`. Converts to `BuildingDefinition`. |
| `GridMapper` | Plain class. `TryGetCell(Vector3, out Cell)` and `CellCenter(Cell)`. |
| `ToolGeometry` | Plain static class. Axis-snaps a road drag and lists rectangle cells. |
| `ToolController` | Holds the current tool and drag, builds the preview, sends commands. Public `SelectTool`, `PointerDown`, `PointerMove`, `PointerUp`, `Cancel`. |
| `InputBridge` | Reads `Keyboard.current` and `Mouse.current`, raycasts to the Terrain, calls `ToolController`, the clock and `CameraRig`. No game logic. |
| `CameraRig` | Pan, zoom, rotate, clamped to the map. Speeds and zoom limits are serialized fields. |
| `RoadView`, `ZoneView`, `BuildingView` | Show city state and follow `SimulationEvents`. |
| `PreviewView` | Tints pooled preview quads. |
| `DebugReadout` | Temporary IMGUI panel, replaced in sub-project 3. |

`BuildingAsset` order in the `GameController` list must match `BuildingCatalog.Default()`,
because the order selects growth variants.

## 7. World rules

- Terrain is 800×800 m with its origin at `(0, 0, 0)`, matching 100×100 cells of 8 m.
- The buildable area is flat at height 0, and objects sit at `y = 0`. Hills may be
  sculpted only outside the grid.
- `Cell.X` maps to world X and `Cell.Y` maps to world Z. A cell's centre is
  `((x + 0.5) * 8, 0, (y + 0.5) * 8)`.
- The pointer raycast hits only the Terrain layer. A miss, or a hit outside the grid,
  yields no cell.

## 8. Controls

| Input | Action |
|---|---|
| WASD | Pan |
| Mouse wheel | Zoom |
| Q / E | Rotate |
| 1 | Road tool |
| 2 | Bulldoze tool |
| 3 / 4 / 5 | Residential / commercial / industrial zone tool |
| 6 / 7 | Power plant / water pump tool |
| Esc, right mouse | Cancel drag, or clear the tool when no drag is active |
| Space | Toggle pause |
| F1 / F2 / F3 | 1x / 2x / 4x |

Space resumes at the last selected speed.

## 9. Tool behaviour

- **Road:** press, drag, release. The line snaps to the axis with the larger drag
  distance. The whole line previews green or red from `Validate`, and release sends one
  `BuildRoadCommand`.
- **Zone:** drag a rectangle. Each cell is validated alone, and valid cells are tinted.
  Release sends one `CreateZoneCommand` with only the valid cells. No valid cell means
  no command.
- **Power and water:** click sends `ConstructBuildingCommand`.
- **Bulldoze:** click removes the building on the cell, otherwise the road, otherwise
  the zone.

Every refused command changes nothing and shows its `CommandFailure` on the readout.

## 10. Views

- `RoadView` and `ZoneView` scan the grid on `RoadsChanged` and `ZonesChanged`, adding or
  removing only cells that differ, because those events carry no position.
- `BuildingView` instantiates and destroys prefabs on `BuildingAdded` and
  `BuildingRemoved`. A building that is not operational is tinted red through a
  `MaterialPropertyBlock`.
- All views draw existing state at startup, so the entry road is visible on the first
  frame.
- Each road, zone and building is one GameObject. A full map is about 10,000 objects.
  Instanced drawing is added only if profiling requires it.

The readout shows day, population, budget, speed, demand, current tool, last refusal and
the status flags of the building under the pointer.

## 11. Error handling

- `GameController` reports every missing Inspector reference in one Console error, then
  disables itself.
- An invalid `BuildingAsset` set, a missing prefab, or a duplicate ID is reported by name
  at startup, and the game does not start.
- Terrain with no collider, or with the wrong size or origin, is reported at startup.
- A pointer outside the map selects nothing and sends nothing.

## 12. Testing

| Layer | Covers | Runner |
|---|---|---|
| Core | `Validate` matches `Execute` for each failure and success, and leaves saved JSON unchanged | `dotnet test` |
| EditMode | `GridMapper` at edges and outside the map, axis snapping, rectangle listing, `BuildingAsset` files match `BuildingCatalog.Default()` values and order | Unity |
| PlayMode | Loads `GameWorld`, confirms references, uses `ToolController` to build road, power, water and zones, runs 30 days, confirms view object counts match `CityState` | Unity |
| Playtest | Camera feel, input, preview and colours, from a written checklist | Developer |

Device reading in `InputBridge` is thin and covered by the playtest, not simulated input.

## 13. Definition of done

- All tests pass, including the 133 existing core tests.
- Blueprint Phase 1 and Phase 2 exit criteria are met.
- The playtest shows a city growing from roads, zones and utilities.
- FPS on a full map is measured with the Profiler on the target machine and recorded.
- `.meta` files, `ProjectSettings`, `Packages`, scenes and prefabs are committed.
  `Library` is not.

**Not verifiable by code alone:** camera feel and visual correctness, which need the
developer's playtest.

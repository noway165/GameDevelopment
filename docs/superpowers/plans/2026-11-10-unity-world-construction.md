# Unity World and Construction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the simulation core playable in Unity 6.3 LTS: camera, roads, zones, utilities, and visibly growing buildings, with placeholder art.

**Architecture:** A new `Metropolis.Game` assembly depends one way on the core. Plain C# classes hold mapping, tool logic and views. Thin MonoBehaviours connect them to Unity. The developer builds the scene and prefabs in the Editor from checklists in this plan.

**Tech Stack:** Unity `6000.3.28f1`, URP 17.3.0, Input System 1.20.1, Unity Test Framework 1.6.0, C# 9.

**Spec:** `docs/superpowers/specs/2026-11-10-unity-world-construction-design.md`

## Global Constraints

- Language is C# 9. The core stays free of `UnityEngine`.
- Namespaces: `Metropolis.Game`, tests in `Metropolis.Game.Tests` and `Metropolis.Game.PlayTests`.
- Cell size is 8 m, grid 100×100, Terrain 800×800 m at origin `(0, 0, 0)`, objects at `y = 0`.
- `Mouse.current.scroll.y` is ±1 per notch (Input System default).
- URP tint property is `_BaseColor`.
- Run commands from the repository root `E:\Game Development\Metropolis`.
- Command-line Unity runs need the Editor **closed**. If it is open, ask the developer to close it, or to press Run in the Test Runner and report the counts.
- Steps marked **[Developer]** are hands-on in the Editor. The executor stops, gives the developer that checklist in Vietnamese, waits, then verifies.
- Commit messages carry no AI attribution line.
- Test snippets show class members only. Put them in a public class named after the file, with the needed `using` lines, including `Metropolis.Core`, `NUnit.Framework`, `UnityEngine` and, for `[UnityTest]`, `UnityEngine.TestTools` and `System.Collections`. Do not add `using System;` where it makes `Object` ambiguous.

Refinements to the spec, decided here:
- `ToolController` and the views are plain classes built by `GameController`, so the developer wires far fewer Inspector references.
- `RoadLine` in the core becomes public, so previews reuse its line expansion.
- Startup checks live in a static `StartupCheck`, so they can be tested without a scene.

## Review Focus

1. The pointer leaves the map during a drag and the button is released outside: the drag is dropped and no command is sent. (Task 5)
2. A zone drag across the whole map: preview must recompute only when the end cell changes, or the frame rate collapses. (Task 5)
3. Bulldozing a road, zone or building while views exist: objects disappear and counts match `CityState`. (Task 6)
4. A half-wired scene: `GameController` reports the problem once and does nothing further, instead of throwing every frame. (Task 3)
5. A click exactly on the far map edge at `x = 800`: treated as outside, never an out-of-range cell. (Task 2)

---

### Task 1: Validate commands without changing state

**Files:**
- Modify: `Assets/_Project/Scripts/Simulation/Commands/ICommand.cs`, each of the seven command files in that folder, `Assets/_Project/Scripts/Simulation/Commands/CommandResult.cs`, `Assets/_Project/Scripts/Simulation/Simulation.cs`
- Test: `Assets/_Project/Tests/Simulation/CommandValidateTests.cs`

**Interfaces:**
- Produces: `CommandResult ICommand.Validate(SimContext ctx)`, never mutating and raising no events.
- Produces: `CommandResult Simulation.Validate(ICommand command)`.
- Produces: `public static class RoadLine` with existing `IsStraight` and `Cells`.

- [ ] **Step 1: Write the failing tests in `CommandValidateTests.cs`**

```csharp
static Cell C(int x, int y) => new Cell(x, y);

static TestWorld Built() {
    var w = TestWorld.Create().Road(0, 10, 5, 10);
    w.Place("power_plant_01", 1, 9);
    w.Place("residential_low_01", 2, 9);
    return w;
}

static ICommand[] Commands() => new ICommand[] {
    new BuildRoadCommand(C(6, 10), C(8, 10)),
    new BuildRoadCommand(C(1, 1), C(2, 2)),
    new BuildRoadCommand(C(1, 10), C(4, 10)),
    new RemoveRoadCommand(C(3, 10), C(3, 10)),
    new RemoveRoadCommand(C(0, 10), C(1, 10)),
    new CreateZoneCommand(new[] { C(3, 9) }, ZoneType.Commercial),
    new CreateZoneCommand(new[] { C(3, 1) }, ZoneType.Commercial),
    new RemoveZoneCommand(new[] { C(2, 9) }),
    new ConstructBuildingCommand("water_pump_01", C(3, 9)),
    new ConstructBuildingCommand("nope", C(3, 9)),
    new RemoveBuildingCommand(1),
    new RemoveBuildingCommand(99),
    new SetTaxRateCommand(20),
    new SetTaxRateCommand(99),
};

[Test] public void Validate_MatchesExecute_AndChangesNothing() {
    for (int i = 0; i < Commands().Length; i++) {
        var w = Built();
        var before = SaveSerializer.Serialize(w.City);
        var command = Commands()[i];
        var validated = command.Validate(w.Ctx);
        Assert.That(SaveSerializer.Serialize(w.City), Is.EqualTo(before), "changed state at " + i);
        Assert.That(command.Execute(w.Ctx).Failure, Is.EqualTo(validated.Failure), "mismatch at " + i);
    }
}
[Test] public void Validate_RaisesNoEvents() {
    var w = Built(); int raised = 0;
    w.Ctx.Events.RoadsChanged += () => raised++;
    w.Ctx.Events.BudgetChanged += _ => raised++;
    new BuildRoadCommand(C(6, 10), C(8, 10)).Validate(w.Ctx);
    Assert.That(raised, Is.EqualTo(0));
}
[Test] public void Simulation_Validate_PassesThrough() {
    var w = Built();
    var s = new Simulation(w.City, w.Ctx.Catalog, w.Ctx.Config);
    Assert.That(s.Validate(new SetTaxRateCommand(99)).Failure, Is.EqualTo(CommandFailure.InvalidTaxRate));
    Assert.That(w.City.TaxRate, Is.EqualTo(10));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests --filter FullyQualifiedName~CommandValidateTests`
Expected: build fails with `CS1061` for `Validate`.

- [ ] **Step 3: Split each command into `Validate` and `Execute`**

Move the existing checks, unchanged and in the same order, into `Validate`. `Execute` calls `Validate`, returns on failure, then applies. `BuildRoadCommand` keeps a private check method that also yields its new-cell list, so the list is not computed by a second algorithm.

- [ ] **Step 4: Run all core tests**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests`
Expected: `Passed!` with 136 tests.

- [ ] **Step 5: Commit**

```bash
git add Assets
git commit -m "feat: let commands validate without changing state"
```

### Task 2: Unity packages, game assembly, grid mapping

**Files:**
- Modify: `Packages/manifest.json`, `ProjectSettings/ProjectSettings.asset`
- Create: `tools/unity-tests.ps1`
- Create: `Assets/_Project/Scripts/Game/Metropolis.Game.asmdef`, `GridMapper.cs`, `ToolGeometry.cs`
- Create: `Assets/_Project/Tests/Game/EditMode/Metropolis.Game.Tests.asmdef`, `Assets/_Project/Tests/Game/PlayMode/Metropolis.Game.PlayTests.asmdef`
- Test: `Assets/_Project/Tests/Game/EditMode/GridMapperTests.cs`, `ToolGeometryTests.cs`

**Interfaces:**
- Produces: `GridMapper(float cellSize, int width, int height)`, `bool TryGetCell(Vector3 world, out Cell cell)`, `Vector3 CellCenter(Cell cell)`.
- Produces: `static Cell ToolGeometry.SnapToAxis(Cell start, Cell cursor)`, ties staying on X. `static List<Cell> ToolGeometry.Rectangle(Cell a, Cell b)`, ascending `y` then `x`.

- [ ] **Step 1: Configure the project** (Editor closed)

Add to `Packages/manifest.json` dependencies: `com.unity.render-pipelines.universal` 17.3.0, `com.unity.inputsystem` 1.20.1, and `com.unity.modules.terrain`, `com.unity.modules.terrainphysics`, `com.unity.modules.physics`, `com.unity.modules.imgui` at 1.0.0.

In `ProjectSettings.asset`, set `activeInputHandler: 1` and `m_ActiveColorSpace: 1`.

Write `tools/unity-tests.ps1` with parameter `-Platform EditMode|PlayMode`. It runs `E:\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe` through `Start-Process -Wait` with `-batchmode -nographics -runTests -testPlatform <Platform> -projectPath <repo root> -testResults <temp xml> -logFile <temp log>`. It prints `<Platform>: total=N passed=N failed=N`, the names of failed tests, and any `error CS` lines from the log, then exits non-zero on any failure or missing results file. Add the comment `# shortcut: editor path is fixed, read ProjectVersion.txt when a second machine appears`.

Game asmdef: `"name": "Metropolis.Game"`, `"rootNamespace": "Metropolis.Game"`, `"references": ["Metropolis.Simulation", "Unity.InputSystem"]`.

EditMode asmdef: `"name": "Metropolis.Game.Tests"`, references `Metropolis.Simulation`, `Metropolis.Game`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, `"includePlatforms": ["Editor"]`, `"overrideReferences": true`, `"precompiledReferences": ["nunit.framework.dll"]`, `"autoReferenced": false`, `"defineConstraints": ["UNITY_INCLUDE_TESTS"]`.

PlayMode asmdef: `"name": "Metropolis.Game.PlayTests"`, same references and settings with `"includePlatforms": []`.

- [ ] **Step 2: Write the failing tests**

```csharp
// GridMapperTests
static GridMapper Map() => new GridMapper(8f, 100, 100);

[TestCase(0f, 0f, 0, 0)] [TestCase(7.99f, 7.99f, 0, 0)]
[TestCase(8f, 0f, 1, 0)] [TestCase(799.9f, 799.9f, 99, 99)]
public void TryGetCell_Inside(float x, float z, int cx, int cy) {
    Assert.That(Map().TryGetCell(new Vector3(x, 0, z), out var cell), Is.True);
    Assert.That(cell, Is.EqualTo(new Cell(cx, cy)));
}
[TestCase(-0.01f, 5f)] [TestCase(800f, 5f)] [TestCase(5f, 800f)] [TestCase(5f, -1f)]
public void TryGetCell_Outside(float x, float z) =>
    Assert.That(Map().TryGetCell(new Vector3(x, 0, z), out _), Is.False);
[Test] public void CellCenter_IsMiddleOfCell() =>
    Assert.That(Map().CellCenter(new Cell(3, 7)), Is.EqualTo(new Vector3(28f, 0f, 60f)));

// ToolGeometryTests
static Cell C(int x, int y) => new Cell(x, y);

[Test] public void Snap_KeepsLongerAxisX() => Assert.That(ToolGeometry.SnapToAxis(C(5, 5), C(9, 7)), Is.EqualTo(C(9, 5)));
[Test] public void Snap_KeepsLongerAxisY() => Assert.That(ToolGeometry.SnapToAxis(C(5, 5), C(6, 9)), Is.EqualTo(C(5, 9)));
[Test] public void Snap_TieStaysOnX() => Assert.That(ToolGeometry.SnapToAxis(C(5, 5), C(8, 8)), Is.EqualTo(C(8, 5)));
[Test] public void Rectangle_ListsAscending_FromAnyCorner() => Assert.That(
    ToolGeometry.Rectangle(C(2, 2), C(1, 3)), Is.EqualTo(new[] { C(1, 2), C(2, 2), C(1, 3), C(2, 3) }));
[Test] public void Rectangle_SingleCell() => Assert.That(ToolGeometry.Rectangle(C(4, 4), C(4, 4)).Count, Is.EqualTo(1));
```

- [ ] **Step 3: Run to verify failure**

Run: `powershell -File tools/unity-tests.ps1 -Platform EditMode`
Expected: non-zero exit with `error CS0246` for `GridMapper`.

- [ ] **Step 4: Implement `GridMapper` and `ToolGeometry`**

Convert with `Mathf.FloorToInt(world / cellSize)` and bounds-check the result.

- [ ] **Step 5: Run to verify success**

Run: `powershell -File tools/unity-tests.ps1 -Platform EditMode`
Expected: failed=0, with the 136 core tests included in the total.

- [ ] **Step 6: Commit**

```bash
git add Assets Packages ProjectSettings tools
git commit -m "feat: add game assembly, packages and grid mapping"
```

### Task 3: Building assets, startup checks, game controller

**Files:**
- Create in `Assets/_Project/Scripts/Game/`: `BuildingAsset.cs`, `StartupCheck.cs`, `GameController.cs`
- Test: `Assets/_Project/Tests/Game/EditMode/StartupCheckTests.cs`, `Assets/_Project/Tests/Game/PlayMode/GameControllerTests.cs`

**Interfaces:**
- Produces: `[CreateAssetMenu(menuName = "Metropolis/Building")] class BuildingAsset : ScriptableObject` with public fields `string id; BuildingKind kind; int constructionCost, maintenance, capacity, powerDemand, waterDemand, supply; GameObject prefab;` and `BuildingDefinition ToDefinition()`.
- Produces: `static List<string> StartupCheck.Find(Terrain terrain, IReadOnlyList<BuildingAsset> buildings, GameObject road, GameObject residentialZone, GameObject commercialZone, GameObject industrialZone, GameObject preview)`.
- Produces: `GameController : MonoBehaviour` with `[SerializeField]` private fields `terrain`, `List<BuildingAsset> buildings`, `roadPrefab`, `residentialZonePrefab`, `commercialZonePrefab`, `industrialZonePrefab`, `previewPrefab`, and public getters `Terrain Terrain`, `IReadOnlyList<BuildingAsset> Buildings`, `GameObject RoadPrefab` and likewise for each prefab field, plus `Simulation Simulation`, `SimulationClock Clock`, `GridMapper Map`.

- [ ] **Step 1: Write the failing tests**

```csharp
// StartupCheckTests, destroying everything in _made in [TearDown] with Object.DestroyImmediate
readonly List<GameObject> _made = new List<GameObject>();
GameObject Go() { var g = new GameObject(); _made.Add(g); return g; }

Terrain MakeTerrain(float size, bool collider) {
    var g = Go();
    var t = g.AddComponent<Terrain>();
    t.terrainData = new TerrainData { size = new Vector3(size, 100, size) };
    if (collider) g.AddComponent<TerrainCollider>();
    return t;
}
BuildingAsset Asset(string id, GameObject prefab) {
    var a = ScriptableObject.CreateInstance<BuildingAsset>();
    a.id = id; a.prefab = prefab;
    return a;
}
List<string> Find(Terrain t, params BuildingAsset[] assets) =>
    StartupCheck.Find(t, assets, Go(), Go(), Go(), Go(), Go());

[Test] public void Valid_HasNoProblems() =>
    Assert.That(Find(MakeTerrain(800, true), Asset("a", Go())), Is.Empty);
[Test] public void AllMissing_ReportsEachReference() =>
    Assert.That(StartupCheck.Find(null, null, null, null, null, null, null).Count, Is.EqualTo(7));
[Test] public void MissingPrefab_NamesTheAsset() =>
    Assert.That(string.Join("|", Find(MakeTerrain(800, true), Asset("house", null))), Does.Contain("house"));
[Test] public void NullListEntry_IsReported() =>
    Assert.That(Find(MakeTerrain(800, true), new BuildingAsset[] { null }).Count, Is.EqualTo(1));
[Test] public void DuplicateId_IsReported() =>
    Assert.That(Find(MakeTerrain(800, true), Asset("a", Go()), Asset("a", Go())).Count, Is.EqualTo(1));
[Test] public void WrongTerrainSize_IsReported() =>
    Assert.That(Find(MakeTerrain(400, true), Asset("a", Go())).Count, Is.EqualTo(1));
[Test] public void MissingCollider_IsReported() =>
    Assert.That(Find(MakeTerrain(800, false), Asset("a", Go())).Count, Is.EqualTo(1));
[Test] public void MovedTerrain_IsReported() {
    var t = MakeTerrain(800, true); t.transform.position = new Vector3(10, 0, 0);
    Assert.That(Find(t, Asset("a", Go())).Count, Is.EqualTo(1));
}

// GameControllerTests
[UnityTest] public IEnumerator UnwiredController_LogsOnce_AndDisablesItself() {
    LogAssert.Expect(LogType.Error, new Regex("GameController"));
    var c = new GameObject().AddComponent<GameController>();
    yield return null;
    yield return null;
    Assert.That(c.enabled, Is.False);
    Assert.That(c.Simulation, Is.Null);
    Object.Destroy(c.gameObject);
}
```

- [ ] **Step 2: Run to verify failure**

Run: `powershell -File tools/unity-tests.ps1 -Platform EditMode`
Expected: non-zero exit with `error CS0246` for `BuildingAsset`.

- [ ] **Step 3: Implement the three classes**

`StartupCheck` reports a null or empty building list, each null entry and each entry without a prefab by its ID, and a Terrain with no `terrainData`. Only when every entry is present does it try `new BuildingCatalog(...)`, reporting the `ArgumentException` message. Serialized list fields start as empty lists. `GameController.Awake` calls `Find`. If it returns problems, it logs one `Debug.LogError` that starts with `GameController:` and joins all problems, sets `enabled = false`, and returns. Otherwise it builds the catalog with `new SimulationConfig()`, `CityState.CreateNew("New City", config)`, `Simulation`, `SimulationClock` and `GridMapper(8f, config.GridWidth, config.GridHeight)`. `Update` runs `Clock.Advance(Time.unscaledDeltaTime)` and calls `Simulation.AdvanceDay()` that many times.

- [ ] **Step 4: Run to verify success**

Run: `powershell -File tools/unity-tests.ps1 -Platform EditMode`, then `powershell -File tools/unity-tests.ps1 -Platform PlayMode`
Expected: both failed=0.

- [ ] **Step 5: Commit**

```bash
git add Assets
git commit -m "feat: add building assets, startup checks and game controller"
```

### Task 4: [Developer] Build the world in the Editor

**Files:**
- Created in the Editor: `Assets/_Project/Scenes/GameWorld.unity`, `Assets/_Project/Settings/*`, `Assets/_Project/Art/Materials/*`, `Assets/_Project/Prefabs/**`, `Assets/_Project/ScriptableObjects/Buildings/*`
- Test: `Assets/_Project/Tests/Game/EditMode/GameWorldSceneTests.cs`

**Interfaces:**
- Consumes: `GameController` getters and `StartupCheck.Find` from Task 3.

- [ ] **Step 1: Write the scene test (Claude)**

```csharp
[SetUp] public void OpenScene() =>
    EditorSceneManager.OpenScene("Assets/_Project/Scenes/GameWorld.unity");

static GameController Controller() => Object.FindFirstObjectByType<GameController>();

[Test] public void Scene_PassesStartupChecks() {
    var c = Controller();
    Assert.That(StartupCheck.Find(c.Terrain, c.Buildings, c.RoadPrefab, c.ResidentialZonePrefab,
        c.CommercialZonePrefab, c.IndustrialZonePrefab, c.PreviewPrefab), Is.Empty);
}
[Test] public void Assets_MatchDefaultCatalog_InOrder() {
    var expected = new[] { "residential_low_01", "commercial_small_01", "commercial_large_01",
        "industrial_small_01", "industrial_large_01", "power_plant_01", "water_pump_01" };
    var catalog = BuildingCatalog.Default();
    var c = Controller();
    Assert.That(c.Buildings.Count, Is.EqualTo(7));
    for (int i = 0; i < 7; i++) {
        var a = c.Buildings[i].ToDefinition();
        var d = catalog.Get(expected[i]);
        Assert.That(a.Id, Is.EqualTo(d.Id));
        Assert.That(a.Kind, Is.EqualTo(d.Kind), d.Id);
        Assert.That(a.ConstructionCost, Is.EqualTo(d.ConstructionCost), d.Id);
        Assert.That(a.Maintenance, Is.EqualTo(d.Maintenance), d.Id);
        Assert.That(a.Capacity, Is.EqualTo(d.Capacity), d.Id);
        Assert.That(a.PowerDemand, Is.EqualTo(d.PowerDemand), d.Id);
        Assert.That(a.WaterDemand, Is.EqualTo(d.WaterDemand), d.Id);
        Assert.That(a.Supply, Is.EqualTo(d.Supply), d.Id);
    }
}
[Test] public void Scene_IsInBuildList() {
    Assert.That(EditorBuildSettings.scenes.Any(s => s.path.EndsWith("GameWorld.unity")), Is.True);
}
```

- [ ] **Step 2: [Developer] Open and configure the project**

1. In Unity Hub choose **Projects → Add → Add project from disk** and pick `E:\Game Development\Metropolis`. Open it with `6000.3.28f1`.
2. **Assets → Create → Rendering → URP Asset (with Universal Renderer)**, saved in `Assets/_Project/Settings/`.
3. **Edit → Project Settings → Graphics**: set Default Render Pipeline to that asset. In **Quality**, set the Render Pipeline Asset for the active level as well.
4. **Edit → Project Settings → Tags and Layers**: add a layer named `Terrain`.

- [ ] **Step 3: [Developer] Build the scene**

1. **File → New Scene**, Basic template, saved as `Assets/_Project/Scenes/GameWorld.unity`.
2. **File → Build Profiles → Scene List**: add the open scene.
3. **GameObject → 3D Object → Terrain**. Position `(0, 0, 0)`. In Terrain Settings set Width 800, Length 800, Height 100. Set its Layer to `Terrain`. Do not sculpt inside the map.

- [ ] **Step 4: [Developer] Create materials and prefabs**

Create one URP Lit material per colour in `Assets/_Project/Art/Materials/`. Each prefab is an empty root at `(0, 0, 0)` with one primitive child. **Delete the collider on every child.**

| Prefab, in `Prefabs/` | Child | Child position | Child scale | Colour |
|---|---|---|---|---|
| `Roads/Road` | Quad, rotation X 90 | (0, 0.02, 0) | (8, 8, 1) | Dark grey |
| `Zones/ZoneResidential` | Quad, rotation X 90 | (0, 0.01, 0) | (8, 8, 1) | Light green |
| `Zones/ZoneCommercial` | Quad, rotation X 90 | (0, 0.01, 0) | (8, 8, 1) | Light blue |
| `Zones/ZoneIndustrial` | Quad, rotation X 90 | (0, 0.01, 0) | (8, 8, 1) | Light yellow |
| `Roads/Preview` | Quad, rotation X 90 | (0, 0.1, 0) | (8, 8, 1) | White |
| `Buildings/ResidentialLow` | Cube | (0, 3, 0) | (6, 6, 6) | Green |
| `Buildings/CommercialSmall` | Cube | (0, 4, 0) | (6, 8, 6) | Blue |
| `Buildings/CommercialLarge` | Cube | (0, 6, 0) | (7, 12, 7) | Dark blue |
| `Buildings/IndustrialSmall` | Cube | (0, 3, 0) | (7, 6, 7) | Yellow |
| `Buildings/IndustrialLarge` | Cube | (0, 4.5, 0) | (7.5, 9, 7.5) | Orange |
| `Buildings/PowerPlant` | Cube | (0, 5, 0) | (7, 10, 7) | Red-brown |
| `Buildings/WaterPump` | Cube | (0, 2.5, 0) | (6, 5, 6) | Cyan |

- [ ] **Step 5: [Developer] Create the seven building assets**

In `Assets/_Project/ScriptableObjects/Buildings/`, use **Create → Metropolis → Building**:

| Id | Kind | Cost | Maintenance | Capacity | Power | Water | Supply | Prefab |
|---|---|---|---|---|---|---|---|---|
| `residential_low_01` | Residential | 0 | 5 | 20 | 10 | 8 | 0 | ResidentialLow |
| `commercial_small_01` | Commercial | 0 | 8 | 10 | 12 | 5 | 0 | CommercialSmall |
| `commercial_large_01` | Commercial | 0 | 15 | 20 | 20 | 8 | 0 | CommercialLarge |
| `industrial_small_01` | Industrial | 0 | 10 | 15 | 20 | 10 | 0 | IndustrialSmall |
| `industrial_large_01` | Industrial | 0 | 18 | 30 | 35 | 18 | 0 | IndustrialLarge |
| `power_plant_01` | Power | 5000 | 100 | 0 | 0 | 0 | 500 | PowerPlant |
| `water_pump_01` | Water | 3000 | 60 | 0 | 0 | 0 | 400 | WaterPump |

- [ ] **Step 6: [Developer] Add and wire the controller**

Create an empty GameObject named `Game` and add `GameController`. Assign Terrain, the five prefab slots, and the seven assets **in the table's order**. Save the scene and close the Editor, or press Run in the Test Runner.

- [ ] **Step 7: Verify (Claude)**

Run: `powershell -File tools/unity-tests.ps1 -Platform EditMode`
Expected: failed=0. On a value mismatch, the test names the asset, and the developer fixes it in the Inspector.

- [ ] **Step 8: Commit**

```bash
git add Assets ProjectSettings
git commit -m "feat: add game world scene, prefabs and building assets"
```

### Task 5: Tool controller

**Files:**
- Create: `Assets/_Project/Scripts/Game/ToolController.cs`
- Test: `Assets/_Project/Tests/Game/EditMode/ToolControllerTests.cs`

**Interfaces:**
- Consumes: `Simulation.Validate`, `ToolGeometry`, `RoadLine.Cells`.
- Produces: `enum BuildTool { None, Road, Bulldoze, ZoneResidential, ZoneCommercial, ZoneIndustrial, PowerPlant, WaterPump }`.
- Produces: `ToolController(Simulation simulation)`, `BuildTool Tool`, `Cell? Hover`, `CommandFailure LastFailure`, `IReadOnlyList<Cell> PreviewValid`, `IReadOnlyList<Cell> PreviewInvalid`, `int PreviewVersion`, `void SelectTool(BuildTool tool)`, `void PointerMove(Cell? cell)`, `void PointerDown(Cell? cell)`, `void PointerUp(Cell? cell)`, `void Cancel()`.

- [ ] **Step 1: Write the failing tests in `ToolControllerTests.cs`**

```csharp
static Cell C(int x, int y) => new Cell(x, y);
Simulation _sim; ToolController _tools;

[SetUp] public void Create() {
    var config = new SimulationConfig();
    _sim = new Simulation(CityState.CreateNew("T", config), BuildingCatalog.Default(), config);
    _tools = new ToolController(_sim);
}
void Drag(Cell from, Cell to) { _tools.PointerDown(from); _tools.PointerMove(to); _tools.PointerUp(to); }
void Street() { _tools.SelectTool(BuildTool.Road); Drag(C(1, 50), C(10, 50)); }

[Test] public void Road_SnapsToAxis_AndChargesNewCells() {
    _tools.SelectTool(BuildTool.Road);
    Drag(C(1, 50), C(5, 52));
    Assert.That(_sim.City.HasRoad(C(5, 50)), Is.True);
    Assert.That(_sim.City.HasRoad(C(5, 52)), Is.False);
    Assert.That(_sim.City.Budget, Is.EqualTo(49950L));
}
[Test] public void Road_OverZone_PreviewsRed_AndIsRefused() {
    Street();
    _sim.City.SetZone(C(12, 50), ZoneType.Residential);
    var budget = _sim.City.Budget;
    _tools.PointerDown(C(11, 50)); _tools.PointerMove(C(14, 50));
    Assert.That(_tools.PreviewValid, Is.Empty);
    Assert.That(_tools.PreviewInvalid.Count, Is.EqualTo(4));
    _tools.PointerUp(C(14, 50));
    Assert.That(_tools.LastFailure, Is.EqualTo(CommandFailure.CellOccupied));
    Assert.That(_sim.City.Budget, Is.EqualTo(budget));
}
[Test] public void Zone_SendsOnlyValidCells() {
    Street();
    _tools.SelectTool(BuildTool.ZoneResidential);
    _tools.PointerDown(C(1, 51)); _tools.PointerMove(C(1, 56));
    Assert.That(_tools.PreviewValid.Count, Is.EqualTo(2));
    Assert.That(_tools.PreviewInvalid.Count, Is.EqualTo(4));
    _tools.PointerUp(C(1, 56));
    Assert.That(_sim.City.GetZone(C(1, 52)), Is.EqualTo(ZoneType.Residential));
    Assert.That(_sim.City.GetZone(C(1, 53)), Is.EqualTo(ZoneType.None));
}
[Test] public void Zone_NoValidCell_SendsNothing_AndReportsWhy() {
    _tools.SelectTool(BuildTool.ZoneCommercial);
    Drag(C(40, 5), C(42, 7));
    Assert.That(_sim.City.GetZone(C(40, 5)), Is.EqualTo(ZoneType.None));
    Assert.That(_tools.LastFailure, Is.EqualTo(CommandFailure.NoRoadAccess));
}
[Test] public void Power_Click_ChargesOnce() {
    Street();
    var budget = _sim.City.Budget;
    _tools.SelectTool(BuildTool.PowerPlant);
    _tools.PointerDown(C(1, 51)); _tools.PointerUp(C(1, 51));
    Assert.That(_sim.City.Buildings.Count, Is.EqualTo(1));
    Assert.That(_sim.City.Budget, Is.EqualTo(budget - 5000));
}
[Test] public void Bulldoze_PrefersBuilding_ThenRoad_ThenZone() {
    Street();
    _sim.City.SetZone(C(2, 51), ZoneType.Residential);
    _sim.City.AddBuilding("residential_low_01", C(2, 51));
    _tools.SelectTool(BuildTool.Bulldoze);
    _tools.PointerDown(C(2, 51));
    Assert.That(_sim.City.GetBuilding(C(2, 51)), Is.Null);
    Assert.That(_sim.City.GetZone(C(2, 51)), Is.EqualTo(ZoneType.Residential));
    _tools.PointerDown(C(2, 51));
    Assert.That(_sim.City.GetZone(C(2, 51)), Is.EqualTo(ZoneType.None));
    _tools.PointerDown(C(2, 50));
    Assert.That(_sim.City.HasRoad(C(2, 50)), Is.False);
}
[Test] public void Bulldoze_EmptyCell_IsNothingToDo() {
    _tools.SelectTool(BuildTool.Bulldoze);
    _tools.PointerDown(C(40, 5));
    Assert.That(_tools.LastFailure, Is.EqualTo(CommandFailure.NothingToDo));
}
[Test] public void Cancel_DropsDragFirst_ThenClearsTool() {
    _tools.SelectTool(BuildTool.Road);
    _tools.PointerDown(C(1, 50)); _tools.PointerMove(C(5, 50));
    _tools.Cancel();
    Assert.That(_tools.Tool, Is.EqualTo(BuildTool.Road));
    _tools.PointerUp(C(5, 50));
    Assert.That(_sim.City.HasRoad(C(5, 50)), Is.False);
    _tools.Cancel();
    Assert.That(_tools.Tool, Is.EqualTo(BuildTool.None));
}
[Test] public void SelectTool_DuringDrag_DropsDrag() {
    _tools.SelectTool(BuildTool.Road);
    _tools.PointerDown(C(1, 50));
    _tools.SelectTool(BuildTool.Bulldoze);
    _tools.PointerUp(C(5, 50));
    Assert.That(_sim.City.HasRoad(C(1, 50)), Is.False);
}
[Test] public void ReleaseOutsideMap_SendsNothing() {
    _tools.SelectTool(BuildTool.Road);
    _tools.PointerDown(C(1, 50)); _tools.PointerMove(C(5, 50)); _tools.PointerMove(null);
    _tools.PointerUp(null);
    Assert.That(_sim.City.HasRoad(C(1, 50)), Is.False);
    Assert.That(_sim.City.Budget, Is.EqualTo(50000L));
}
[Test] public void Preview_RecomputesOnlyWhenCellChanges() {
    _tools.SelectTool(BuildTool.ZoneResidential);
    _tools.PointerDown(C(0, 0)); _tools.PointerMove(C(99, 99));
    var version = _tools.PreviewVersion;
    _tools.PointerMove(C(99, 99));
    Assert.That(_tools.PreviewVersion, Is.EqualTo(version));
}
[Test] public void NoTool_ClickDoesNothing() {
    _tools.PointerDown(C(1, 50)); _tools.PointerUp(C(1, 50));
    Assert.That(_sim.City.HasRoad(C(1, 50)), Is.False);
}
```

- [ ] **Step 2: Run to verify failure**

Run: `powershell -File tools/unity-tests.ps1 -Platform EditMode`
Expected: non-zero exit with `error CS0246` for `ToolController`.

- [ ] **Step 3: Implement `ToolController` per spec section 9**

Road and zone tools act on `PointerUp` with an active drag and a non-null cell. Power, water and bulldoze act on `PointerDown`. `LastFailure` is set by every action, to `None` on success. With no valid zone cell, `LastFailure` is the first cell's `Validate` failure. A hover with no drag previews the single cell for the current tool. `PointerMove` returns early when the cell equals the previous one. A `null` move keeps the drag but clears the preview. `Cancel` and `SelectTool` recompute the preview from the current hover.

- [ ] **Step 4: Run to verify success**

Run: `powershell -File tools/unity-tests.ps1 -Platform EditMode`
Expected: failed=0.

- [ ] **Step 5: Commit**

```bash
git add Assets
git commit -m "feat: add tool controller with validity preview"
```

### Task 6: Views

**Files:**
- Create in `Assets/_Project/Scripts/Game/`: `RoadView.cs`, `ZoneView.cs`, `BuildingView.cs`, `PreviewView.cs`
- Test: `Assets/_Project/Tests/Game/PlayMode/ViewTests.cs`

**Interfaces:**
- Consumes: `GridMapper`, `ToolController`, `SimulationEvents`.
- Produces: `RoadView(Simulation simulation, GridMapper map, GameObject prefab, Transform parent)`, `int Count`.
- Produces: `ZoneView(Simulation simulation, GridMapper map, GameObject residential, GameObject commercial, GameObject industrial, Transform parent)`, `int Count`.
- Produces: `BuildingView(Simulation simulation, GridMapper map, IReadOnlyDictionary<string, GameObject> prefabs, Transform parent)`, `int Count`, `GameObject Find(int buildingId)`.
- Produces: `PreviewView(ToolController tools, GridMapper map, GameObject prefab, Transform parent)`, `void Refresh()`, `int ActiveCount`.
- All views draw existing state in their constructor.

- [ ] **Step 1: Write the failing tests in `ViewTests.cs`**

```csharp
static Cell C(int x, int y) => new Cell(x, y);
static readonly GridMapper Map = new GridMapper(8f, 100, 100);
Simulation _sim; Transform _parent; GameObject _prefab;

[SetUp] public void Create() {
    var config = new SimulationConfig();
    _sim = new Simulation(CityState.CreateNew("T", config), BuildingCatalog.Default(), config);
    _parent = new GameObject("views").transform;
    _prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
}
[TearDown] public void Clean() { Object.Destroy(_parent.gameObject); Object.Destroy(_prefab); }

[UnityTest] public IEnumerator RoadView_FollowsBuildAndRemove() {
    var view = new RoadView(_sim, Map, _prefab, _parent);
    Assert.That(view.Count, Is.EqualTo(1));
    _sim.Execute(new BuildRoadCommand(C(1, 50), C(3, 50)));
    Assert.That(view.Count, Is.EqualTo(4));
    Assert.That(_parent.GetChild(1).position, Is.EqualTo(Map.CellCenter(C(1, 50))));
    _sim.Execute(new RemoveRoadCommand(C(2, 50), C(2, 50)));
    yield return null;
    Assert.That(view.Count, Is.EqualTo(3));
    Assert.That(_parent.childCount, Is.EqualTo(3));
}
[UnityTest] public IEnumerator ZoneView_ReplacesObjectOnRepaint() {
    var other = GameObject.CreatePrimitive(PrimitiveType.Sphere); other.name = "sphere";
    var view = new ZoneView(_sim, Map, _prefab, other, _prefab, _parent);
    _sim.Execute(new CreateZoneCommand(new[] { C(1, 51) }, ZoneType.Residential));
    Assert.That(view.Count, Is.EqualTo(1));
    _sim.Execute(new CreateZoneCommand(new[] { C(1, 51) }, ZoneType.Commercial));
    yield return null;
    Assert.That(view.Count, Is.EqualTo(1));
    Assert.That(_parent.childCount, Is.EqualTo(1));
    Assert.That(_parent.GetChild(0).name, Does.Contain("sphere"));
    _sim.Execute(new RemoveZoneCommand(new[] { C(1, 51) }));
    yield return null;
    Assert.That(view.Count, Is.EqualTo(0));
    Object.Destroy(other);
}
[UnityTest] public IEnumerator BuildingView_FollowsAddRemove_AndTintsBrokenBuilding() {
    var prefabs = new Dictionary<string, GameObject> { ["power_plant_01"] = _prefab, ["residential_low_01"] = _prefab };
    var view = new BuildingView(_sim, Map, prefabs, _parent);
    _sim.Execute(new ConstructBuildingCommand("power_plant_01", C(1, 51)));
    Assert.That(view.Count, Is.EqualTo(1));

    _sim.City.SetZone(C(3, 51), ZoneType.Residential);
    var house = _sim.City.AddBuilding("residential_low_01", C(3, 51));
    var late = new BuildingView(_sim, Map, prefabs, _parent);
    Assert.That(late.Count, Is.EqualTo(2));

    _sim.AdvanceDay();
    var block = new MaterialPropertyBlock();
    late.Find(house.Id).GetComponentInChildren<Renderer>().GetPropertyBlock(block);
    Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(Color.red));

    _sim.Execute(new RemoveBuildingCommand(house.Id));
    yield return null;
    Assert.That(late.Count, Is.EqualTo(1));
}
[UnityTest] public IEnumerator PreviewView_ShowsAndReusesQuads() {
    var tools = new ToolController(_sim);
    var view = new PreviewView(tools, Map, _prefab, _parent);
    tools.SelectTool(BuildTool.Road);
    tools.PointerDown(C(1, 50)); tools.PointerMove(C(4, 50));
    view.Refresh();
    Assert.That(view.ActiveCount, Is.EqualTo(4));
    var created = _parent.childCount;
    tools.Cancel();
    view.Refresh();
    Assert.That(view.ActiveCount, Is.EqualTo(1));
    Assert.That(_parent.childCount, Is.EqualTo(created));
    yield return null;
}
```

Check of the tint case: `(3, 51)` is out of road range and has no water, so the house is not operational after the tick. After `Cancel`, the road tool still previews the hover cell, so one quad stays active.

- [ ] **Step 2: Run to verify failure**

Run: `powershell -File tools/unity-tests.ps1 -Platform PlayMode`
Expected: non-zero exit with `error CS0246` for `RoadView`.

- [ ] **Step 3: Implement the four views per spec section 10**

`RoadView` and `ZoneView` hold a per-cell array of objects and scan the grid on their event. `ZoneView` also stores the type shown per cell, so a repaint swaps the object. `BuildingView` handles `BuildingAdded`, `BuildingRemoved`, `BuildingStatusChanged`. It applies `Color.red` through a `MaterialPropertyBlock` on every child `Renderer` when not operational, and clears the block otherwise. `PreviewView.Refresh` returns early when `PreviewVersion` has not changed, grows a pool as needed, and tints green `(0.2, 0.9, 0.3, 1)` or red.

- [ ] **Step 4: Run to verify success**

Run: `powershell -File tools/unity-tests.ps1 -Platform PlayMode`
Expected: failed=0.

- [ ] **Step 5: Commit**

```bash
git add Assets
git commit -m "feat: add road, zone, building and preview views"
```

### Task 7: Camera, input, readout, wiring

**Files:**
- Create in `Assets/_Project/Scripts/Game/`: `CameraRig.cs`, `InputBridge.cs`, `DebugReadout.cs`
- Modify: `Assets/_Project/Scripts/Game/GameController.cs`
- Test: `Assets/_Project/Tests/Game/EditMode/CameraRigTests.cs`

**Interfaces:**
- Consumes: `ToolController`, views, `GridMapper`, `SimulationClock`.
- Produces: `CameraRig : MonoBehaviour` with serialized `panSpeed = 1f`, `rotateSpeed = 90f`, `zoomStep = 20f`, `minHeight = 30f`, `maxHeight = 300f`, methods `Pan(Vector2 input, float deltaTime)`, `Rotate(float direction, float deltaTime)`, `Zoom(float notches)`, and `static Vector3 Clamp(Vector3 position, float mapSize, float minHeight, float maxHeight)`.
- Produces: `GameController` getters `ToolController Tools`, `RoadView RoadView`, `ZoneView ZoneView`, `BuildingView BuildingView`.
- `InputBridge` has serialized `GameController game`, `CameraRig rig`, `Camera view`, `LayerMask terrainMask`.
- `DebugReadout` has serialized `GameController game`.

- [ ] **Step 1: Write the failing test in `CameraRigTests.cs`**

```csharp
[Test] public void Clamp_KeepsCameraOverMap_AndWithinZoom() =>
    Assert.That(CameraRig.Clamp(new Vector3(-5, 999, 900), 800, 30, 300), Is.EqualTo(new Vector3(0, 300, 800)));
[Test] public void Clamp_LeavesValidPositionAlone() =>
    Assert.That(CameraRig.Clamp(new Vector3(100, 50, 200), 800, 30, 300), Is.EqualTo(new Vector3(100, 50, 200)));
```

- [ ] **Step 2: Run to verify failure**

Run: `powershell -File tools/unity-tests.ps1 -Platform EditMode`
Expected: non-zero exit with `error CS0246` for `CameraRig`.

- [ ] **Step 3: Implement `CameraRig`, `InputBridge`, `DebugReadout`, and wire `GameController`**

`CameraRig.Pan` moves along the rig's flat forward and right, at `panSpeed * height`, so movement feels constant at any zoom. `Zoom` changes height by `notches * zoomStep`, negative scroll zooming out.

`InputBridge.Update` returns immediately when `Mouse.current` or `Keyboard.current` is null, because batch-mode test runs have no devices. Otherwise it follows spec section 8. It raycasts `view.ScreenPointToRay(Mouse.current.position.ReadValue())` against `terrainMask` with distance 5000, and uses `Map.TryGetCell`. It calls `Tools.PointerMove` each frame, `PointerDown` and `PointerUp` for the left button, and `Cancel` for right button or Esc. Space sets `Paused`, or restores a stored last non-paused speed.

`DebugReadout.OnGUI` shows the spec section 10 values in a `GUILayout` area. Status flags use `Tools.Hover` and `City.GetBuilding`.

`GameController.Awake` also creates `Tools`, the three views under a child `Views` transform, a `PreviewView`, and a prefab dictionary keyed by `BuildingAsset.id`. `Update` also calls `PreviewView.Refresh()`.

- [ ] **Step 4: Run to verify success**

Run: `powershell -File tools/unity-tests.ps1 -Platform EditMode`, then `powershell -File tools/unity-tests.ps1 -Platform PlayMode`
Expected: both failed=0.

- [ ] **Step 5: Commit**

```bash
git add Assets
git commit -m "feat: add camera, input bridge and debug readout"
```

### Task 8: Final wiring, smoke test, playtest

**Files:**
- Modify in the Editor: `Assets/_Project/Scenes/GameWorld.unity`
- Test: `Assets/_Project/Tests/Game/PlayMode/GameWorldSmokeTests.cs`
- Modify: `docs/superpowers/specs/2026-11-10-unity-world-construction-design.md` (status and FPS result)

**Interfaces:**
- Consumes: everything above.

- [ ] **Step 1: [Developer] Wire the camera and input**

1. Create an empty GameObject `CameraRig` at `(400, 150, 300)` and add the `CameraRig` component.
2. Drag **Main Camera** under it. Set local position `(0, 0, 0)` and local rotation X 55.
3. Add `InputBridge` to `Game`. Assign Game, Rig, View (Main Camera), and set Terrain Mask to `Terrain` only.
4. Add `DebugReadout` to `Game` and assign Game.
5. Save the scene and close the Editor, or press Run in the Test Runner.

- [ ] **Step 2: Write the smoke test in `GameWorldSmokeTests.cs`**

```csharp
static Cell C(int x, int y) => new Cell(x, y);

[UnityTest] public IEnumerator GameWorld_BuildsAndGrows() {
    SceneManager.LoadScene("GameWorld");
    yield return null;
    yield return null;
    var c = Object.FindFirstObjectByType<GameController>();
    Assert.That(c.enabled, Is.True);
    Assert.That(Object.FindFirstObjectByType<InputBridge>(), Is.Not.Null);

    c.Clock.Speed = SimulationSpeed.Paused;
    c.Tools.SelectTool(BuildTool.Road);
    c.Tools.PointerDown(C(1, 50)); c.Tools.PointerMove(C(20, 50)); c.Tools.PointerUp(C(20, 50));
    c.Tools.SelectTool(BuildTool.PowerPlant);
    c.Tools.PointerDown(C(1, 51)); c.Tools.PointerUp(C(1, 51));
    c.Tools.SelectTool(BuildTool.WaterPump);
    c.Tools.PointerDown(C(2, 51)); c.Tools.PointerUp(C(2, 51));
    c.Tools.SelectTool(BuildTool.ZoneResidential);
    c.Tools.PointerDown(C(5, 51)); c.Tools.PointerMove(C(15, 52)); c.Tools.PointerUp(C(15, 52));

    for (int i = 0; i < 30; i++) c.Simulation.AdvanceDay();
    yield return null;

    var city = c.Simulation.City;
    int roads = 0, zones = 0;
    for (int y = 0; y < city.Height; y++)
        for (int x = 0; x < city.Width; x++) {
            if (city.HasRoad(C(x, y))) roads++;
            if (city.GetZone(C(x, y)) != ZoneType.None) zones++;
        }
    Assert.That(c.RoadView.Count, Is.EqualTo(roads));
    Assert.That(c.ZoneView.Count, Is.EqualTo(zones));
    Assert.That(c.BuildingView.Count, Is.EqualTo(city.Buildings.Count));
    Assert.That(city.Buildings.Count, Is.GreaterThan(2));
    Assert.That(city.Population, Is.GreaterThan(0));
}
```

- [ ] **Step 3: Run the full automated suite**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests`
Expected: `Passed!` with 136 tests.

Run: `powershell -File tools/unity-tests.ps1 -Platform EditMode`, then `powershell -File tools/unity-tests.ps1 -Platform PlayMode`
Expected: both failed=0.

- [ ] **Step 4: [Developer] Playtest**

Open `GameWorld` and press Play. Report any line that fails:

1. The map edge shows one road cell at mid-height on the left.
2. WASD pans at a comfortable speed. The wheel zooms. Q and E rotate. The camera stays over the map.
3. Press 1, hover: one preview cell. Drag from the entry road: a straight green line follows the longer axis. Release builds it, and the budget falls.
4. Drag a road across a building: red preview, and the panel shows the refusal.
5. Press 3 and drag beside the road: only cells within 2 of the road turn green. Release paints them.
6. Press 6 and click near the road, then 7 and click near the road.
7. Space pauses and resumes. F1, F2, F3 change speed.
8. Within about a minute, buildings appear on the zones and population rises.
9. Press 2 and click the road feeding the houses: buildings turn red on the next day and population falls.
10. Esc cancels a drag, and a second Esc clears the tool.

Tune `CameraRig` serialized fields in the Inspector if the feel is wrong, then save the scene.

- [ ] **Step 5: [Developer] Measure a full map**

Zone as much of the map as practical, run at 4x until growth stops, then record average FPS and the Profiler's Scripts and Rendering times.

- [ ] **Step 6: Record and commit**

Replace `**Status:** Awaiting review` in the spec with `**Status:** Implemented`, and add a `## 14. Measured performance` section with the developer's numbers and building count.

```bash
git add Assets ProjectSettings docs
git commit -m "test: add game world smoke test and record playtest results"
```

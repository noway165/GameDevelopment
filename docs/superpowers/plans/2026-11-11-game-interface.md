# Game Interface and Windows Build Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give METROPOLIS a menu, HUD, mouse toolbar, save and load, and a Windows build that runs outside the Editor.

**Architecture:** The interface is one `GameUi` MonoBehaviour that builds a UI Toolkit tree in code and exposes each button's action as a public method. `GameController` owns sessions and rebuilds the simulation and views when the city changes. `WorldBuilder` generates all scene wiring, so nothing is done by hand in the Editor.

**Tech Stack:** Unity `6000.3.28f1`, UI Toolkit (runtime), URP 17.3.0, Input System 1.20.1, C# 9, PowerShell build scripts.

**Spec:** `docs/superpowers/specs/2026-11-11-game-interface-design.md`

## Global Constraints

- C# 9. The core stays free of `UnityEngine`. All UI text is English.
- Namespaces: `Metropolis.Core`, `Metropolis.Game`, `Metropolis.GameEditor`, tests in `Metropolis.Core.Tests`, `Metropolis.Game.Tests`, `Metropolis.Game.PlayTests`.
- Run commands from the repository root `E:\Game Development\Metropolis`.
- Unity command-line runs need the Editor closed. Check `Temp/UnityLockfile` first.
- Tests must never write to `Application.persistentDataPath`. Every test that saves sets `GameController.Saves` to a `SaveService` on a temporary path first.
- After changing `WorldBuilder`, regenerate the scene: `Unity.exe -batchmode -quit -projectPath . -executeMethod Metropolis.GameEditor.WorldBuilder.Build`.
- Test snippets show class members only. Put them in a public class named after the file with the needed `using` lines.
- Commit messages carry no AI attribution line.

Refinements to the spec, decided here:
- Views get no `Dispose()`. `GameController` destroys the `Views` parent object and builds new views, which removes every old object.
- `SaveService` lives in the core, because it needs no engine code and is then tested by `dotnet test`.
- The missing-`UIDocument` check lives in `GameUi`, which owns the document, not in `StartupCheck`.
- Esc also closes the pause menu.
- The built-player check uses a launch argument that makes the game capture two screenshots and quit, instead of a fixed 20-second run. It proves the UI renders.

## Review Focus

1. Quitting or pressing save at the main menu before a session starts: the existing save file is never replaced by an empty city. (Task 3)
2. Continue with a corrupt or truncated save: a dialog lists the problems, the file is untouched, and the menu stays open. (Tasks 3 and 4)
3. Loading a city while another is on screen: old objects disappear and view counts match the loaded city. (Task 3)
4. A click on a toolbar button that sits above buildable land: the tool is selected and nothing is built underneath. (Task 5)
5. Saving when the save folder does not exist yet, as on a first launch: the save succeeds. (Task 1)

---

### Task 1: Core statistics on refresh, and SaveService

**Files:**
- Modify: `Assets/_Project/Scripts/Simulation/Simulation.cs`, `Systems/PopulationSystem.cs`, `Systems/EconomySystem.cs`
- Create: `Assets/_Project/Scripts/Simulation/Save/SaveService.cs`
- Test: `Assets/_Project/Tests/Simulation/SaveServiceTests.cs`, and one test added to `SimulationTests.cs`

**Interfaces:**
- Produces: `static void PopulationSystem.RefreshStatistics(SimContext ctx)`, filling `Housing`, `UnemploymentPercent`, `NonRunningPercent`, `Attractiveness` without changing population.
- Produces: `static void EconomySystem.Compute(SimContext ctx)`, filling `Revenue` and `Expenses` without changing the budget. `Run` calls `Compute`, then applies.
- Produces: `SaveService(string path)`, `string Path`, `bool Exists`, `bool TrySave(CityState city, out string error)`, `bool TryLoad(BuildingCatalog catalog, SimulationConfig config, out CityState city, out IReadOnlyList<string> errors)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// SimulationTests.cs
[Test] public void RefreshDerived_FillsHudStatistics_WithoutChangingSave() {
    var w = TestWorld.Create().Road(0, 10, 20, 10);
    w.Place("power_plant_01", 1, 9); w.Place("water_pump_01", 2, 9);
    w.Place("residential_low_01", 3, 9); w.Place("commercial_small_01", 4, 9);
    w.City.Population = 20;
    var before = SaveSerializer.Serialize(w.City);
    new Simulation(w.City, w.Ctx.Catalog, w.Ctx.Config);
    Assert.That(w.Stats.Housing, Is.EqualTo(20));
    Assert.That(w.Stats.Attractiveness, Is.EqualTo(100));
    Assert.That(w.Stats.Revenue, Is.EqualTo(70L));
    Assert.That(w.Stats.Expenses, Is.EqualTo(173L));
    Assert.That(SaveSerializer.Serialize(w.City), Is.EqualTo(before));
}

// SaveServiceTests.cs
static string TempPath() => Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "city.json");
static TestWorld Built() {
    var w = TestWorld.Create().Road(0, 10, 20, 10);
    w.Place("power_plant_01", 1, 9);
    return w;
}
[Test] public void Save_CreatesMissingFolder_AndExistsTurnsTrue() {
    var s = new SaveService(TempPath());
    Assert.That(s.Exists, Is.False);
    Assert.That(s.TrySave(Built().City, out var error), Is.True, error);
    Assert.That(s.Exists, Is.True);
}
[Test] public void SaveThenLoad_RoundTrips() {
    var w = Built(); var s = new SaveService(TempPath());
    s.TrySave(w.City, out _);
    Assert.That(s.TryLoad(w.Ctx.Catalog, w.Ctx.Config, out var city, out _), Is.True);
    Assert.That(SaveSerializer.Serialize(city), Is.EqualTo(SaveSerializer.Serialize(w.City)));
}
[Test] public void Load_MissingFile_ReportsOneError() {
    var w = Built();
    Assert.That(new SaveService(TempPath()).TryLoad(w.Ctx.Catalog, w.Ctx.Config, out var city, out var errors), Is.False);
    Assert.That(city, Is.Null);
    Assert.That(errors.Count, Is.EqualTo(1));
}
[Test] public void Load_CorruptFile_ReportsErrors_AndLeavesFile() {
    var w = Built(); var path = TempPath();
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    File.WriteAllText(path, "{ broken");
    Assert.That(new SaveService(path).TryLoad(w.Ctx.Catalog, w.Ctx.Config, out _, out var errors), Is.False);
    Assert.That(errors.Count, Is.GreaterThan(0));
    Assert.That(File.ReadAllText(path), Is.EqualTo("{ broken"));
}
[Test] public void SecondSave_KeepsBackup() {
    var w = Built(); var s = new SaveService(TempPath());
    s.TrySave(w.City, out _); s.TrySave(w.City, out _);
    Assert.That(File.Exists(s.Path + ".bak"), Is.True);
}
```

Hand check: workers 10, jobs 10, employed 10, so revenue is `(20 * 20 + 10 * 30) * 10 / 100 = 70`. Expenses are `100 + 60 + 5 + 8 = 173`.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests`
Expected: build fails with `CS0246` for `SaveService`.

- [ ] **Step 3: Implement**

`RefreshDerived` runs infrastructure, operation, employment, `PopulationSystem.RefreshStatistics`, `FillHousing`, demand, then `EconomySystem.Compute`. `PopulationSystem.Run` reuses the shared calculation. `SaveService.TrySave` creates the folder inside its own `try` for `IOException` and `UnauthorizedAccessException`, then calls `SaveFile.TryWrite`. `TryLoad` returns the read error as its single error when the file cannot be read.

- [ ] **Step 4: Run to verify success**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests`
Expected: `Passed!` with 142 tests.

- [ ] **Step 5: Commit**

```bash
git add Assets
git commit -m "feat: fill statistics on refresh and add save service"
```

### Task 2: HUD text and drag cancel

**Files:**
- Create: `Assets/_Project/Scripts/Game/HudText.cs`
- Modify: `Assets/_Project/Scripts/Game/ToolController.cs`
- Test: `Assets/_Project/Tests/Game/EditMode/HudTextTests.cs`, and two tests added to `ToolControllerTests.cs`

**Interfaces:**
- Produces: `static string HudText.Money(long value)`, `Signed(long value)`, `Failure(CommandFailure failure)`, `Status(BuildingStatus status)`.
- Produces: `bool ToolController.IsDragging`, `void ToolController.CancelDrag()`.

Exact copy for `Failure`: `None` gives `""`. `OutOfBounds` "That is outside the map." `NotStraight` "Roads must be straight." `CellOccupied` "Something is already there." `NoRoadAccess` "Too far from a connected road." `InsufficientFunds` "Not enough money." `ProtectedRoad` "The entry road cannot be removed." `NothingToDo` "Nothing to do there." `UnknownDefinition` "Unknown building." `NotPlayerPlaceable` "That building grows on zones." `UnknownBuilding` "That building no longer exists." `InvalidTaxRate` "Tax rate is out of range."

- [ ] **Step 1: Write the failing tests**

```csharp
// HudTextTests.cs
[TestCase(50000L, "50,000")] [TestCase(-65870L, "-65,870")] [TestCase(0L, "0")]
public void Money(long value, string expected) => Assert.That(HudText.Money(value), Is.EqualTo(expected));
[TestCase(215L, "+215")] [TestCase(-90L, "-90")] [TestCase(0L, "0")]
public void Signed(long value, string expected) => Assert.That(HudText.Signed(value), Is.EqualTo(expected));
[Test] public void Failure_EveryReasonHasItsOwnSentence() {
    var seen = new HashSet<string>();
    foreach (CommandFailure f in Enum.GetValues(typeof(CommandFailure))) {
        if (f == CommandFailure.None) continue;
        Assert.That(HudText.Failure(f), Is.Not.Empty, f.ToString());
        Assert.That(seen.Add(HudText.Failure(f)), Is.True, f.ToString());
    }
    Assert.That(HudText.Failure(CommandFailure.None), Is.Empty);
    Assert.That(HudText.Failure(CommandFailure.NoRoadAccess), Is.EqualTo("Too far from a connected road."));
}
[TestCase(BuildingStatus.None, "Running")]
[TestCase(BuildingStatus.NoWater, "Not running: no water")]
[TestCase(BuildingStatus.NoRoad | BuildingStatus.NoPower | BuildingStatus.NoWater, "Not running: no road access, no power, no water")]
public void Status(BuildingStatus status, string expected) => Assert.That(HudText.Status(status), Is.EqualTo(expected));

// ToolControllerTests.cs
[Test] public void CancelDrag_DropsDrag_KeepsTool() {
    _tools.SelectTool(BuildTool.Road);
    _tools.PointerDown(C(1, 50));
    Assert.That(_tools.IsDragging, Is.True);
    _tools.CancelDrag();
    _tools.PointerUp(C(5, 50));
    Assert.That(_tools.Tool, Is.EqualTo(BuildTool.Road));
    Assert.That(_sim.City.HasRoad(C(5, 50)), Is.False);
}
[Test] public void CancelDrag_WithoutDrag_KeepsTool() {
    _tools.SelectTool(BuildTool.Road);
    _tools.CancelDrag();
    Assert.That(_tools.Tool, Is.EqualTo(BuildTool.Road));
}
```

- [ ] **Step 2: Run to verify failure**

Run: `powershell -File tools/unity-tests.ps1 -Platform EditMode`
Expected: non-zero exit with `error CS0103` for `HudText`.

- [ ] **Step 3: Implement**

`Money` formats with `CultureInfo.InvariantCulture` and `"N0"`.

- [ ] **Step 4: Run to verify success**

Run: `powershell -File tools/unity-tests.ps1 -Platform EditMode`
Expected: failed=0.

- [ ] **Step 5: Commit**

```bash
git add Assets
git commit -m "feat: add HUD text and drag cancel"
```

### Task 3: Sessions in GameController

**Files:**
- Modify: `Assets/_Project/Scripts/Game/GameController.cs`
- Test: `Assets/_Project/Tests/Game/PlayMode/SessionTests.cs`

**Interfaces:**
- Consumes: `SaveService` from Task 1.
- Produces: `SaveService Saves { get; set; }`, defaulting to `Path.Combine(Application.persistentDataPath, "city.json")`. `bool SessionActive`. `void StartNewCity()`. `bool TryContinue(out IReadOnlyList<string> errors)`. `bool TrySave(out string error)`.
- `Tools`, `Simulation` and the three views are replaced whenever the city changes. `Clock` stays the same object.

- [ ] **Step 1: Write the failing tests in `SessionTests.cs`**

```csharp
static Cell C(int x, int y) => new Cell(x, y);
GameController _c; string _path;

[UnitySetUp] public IEnumerator Load() {
    SceneManager.LoadScene("GameWorld");
    yield return null; yield return null;
    _c = Object.FindFirstObjectByType<GameController>();
    _path = Path.Combine(Application.temporaryCachePath, Path.GetRandomFileName(), "city.json");
    _c.Saves = new SaveService(_path);
}
void BuildStreet() {
    _c.Simulation.Execute(new BuildRoadCommand(C(1, 50), C(10, 50)));
    _c.Simulation.Execute(new ConstructBuildingCommand("power_plant_01", C(1, 51)));
}

[Test] public void Launch_HasNoSession_AndSaveIsRefused() {
    Assert.That(_c.SessionActive, Is.False);
    Assert.That(_c.TrySave(out var error), Is.False);
    Assert.That(error, Is.EqualTo("No city in progress."));
    Assert.That(File.Exists(_path), Is.False);
}
[UnityTest] public IEnumerator Save_NewCity_Continue_RestoresCityAndViews() {
    _c.StartNewCity();
    Assert.That(_c.SessionActive, Is.True);
    BuildStreet();
    for (int i = 0; i < 5; i++) _c.Simulation.AdvanceDay();
    var saved = SaveSerializer.Serialize(_c.Simulation.City);
    Assert.That(_c.TrySave(out var error), Is.True, error);

    _c.StartNewCity();
    yield return null;
    Assert.That(_c.Simulation.City.Budget, Is.EqualTo(50000L));
    Assert.That(_c.RoadView.Count, Is.EqualTo(1));
    Assert.That(_c.BuildingView.Count, Is.EqualTo(0));
    Assert.That(GameObject.Find("Views").transform.childCount, Is.LessThanOrEqualTo(2));

    Assert.That(_c.TryContinue(out _), Is.True);
    yield return null;
    Assert.That(SaveSerializer.Serialize(_c.Simulation.City), Is.EqualTo(saved));
    Assert.That(_c.RoadView.Count, Is.EqualTo(11));
    Assert.That(_c.BuildingView.Count, Is.EqualTo(1));
    Assert.That(GameObject.Find("Views").transform.childCount, Is.InRange(12, 13));
}
[Test] public void Continue_CorruptSave_KeepsCityAndFile() {
    Directory.CreateDirectory(Path.GetDirectoryName(_path));
    File.WriteAllText(_path, "{ broken");
    _c.StartNewCity(); BuildStreet();
    var before = SaveSerializer.Serialize(_c.Simulation.City);
    Assert.That(_c.TryContinue(out var errors), Is.False);
    Assert.That(errors.Count, Is.GreaterThan(0));
    Assert.That(SaveSerializer.Serialize(_c.Simulation.City), Is.EqualTo(before));
    Assert.That(File.ReadAllText(_path), Is.EqualTo("{ broken"));
}
[Test] public void Continue_NoFile_FailsWithoutSession() {
    Assert.That(_c.TryContinue(out var errors), Is.False);
    Assert.That(errors.Count, Is.EqualTo(1));
    Assert.That(_c.SessionActive, Is.False);
}
```

Check of the child counts: a new city holds 1 road object, and the saved city holds 11 roads and 1 building. The preview pool adds one quad only when a mouse is hovering, which is why both checks allow one extra object. The old city's 12 objects must be gone.

- [ ] **Step 2: Run to verify failure**

Run: `powershell -File tools/unity-tests.ps1 -Platform PlayMode`
Expected: non-zero exit with `error CS1061` for `Saves`.

- [ ] **Step 3: Implement**

One private `SetCity(CityState city)` destroys the current `Views` object, then creates the simulation, tools and views as `Awake` does now. `Awake` calls it with a fresh city. `TryContinue` changes nothing on failure. `TrySave` returns `"No city in progress."` without touching the file when no session is active.

- [ ] **Step 4: Run to verify success**

Run: `powershell -File tools/unity-tests.ps1 -Platform PlayMode`
Expected: failed=0.

- [ ] **Step 5: Commit**

```bash
git add Assets
git commit -m "feat: add new city, continue and save sessions"
```

### Task 4: The interface

**Files:**
- Create: `Assets/_Project/Scripts/Game/GameUi.cs`, `Assets/_Project/UI/Metropolis.uss`, `Assets/_Project/UI/MetropolisTheme.tss`
- Delete: `Assets/_Project/Scripts/Game/DebugReadout.cs` and its `.meta`
- Modify: `Assets/_Project/Scripts/GameEditor/WorldBuilder.cs`, `Packages/manifest.json`
- Test: `Assets/_Project/Tests/Game/PlayMode/GameUiTests.cs`, one test added to `GameWorldSceneTests.cs`

**Interfaces:**
- Consumes: `GameController` session API, `HudText`, `ToolController.IsDragging`.
- Produces: `GameUi : MonoBehaviour` with serialized `GameController game`, and:
  - `VisualElement Root`, `bool MenuOpen`, `bool DialogOpen`, `string DialogText`
  - `void NewCity()`, `void Continue()`, `void Save()`, `void Quit()`, `void OpenPauseMenu()`, `void Resume()`, `void ConfirmDialog()`, `void DismissDialog()`, `void HandleEscape()`, `void SetSpeed(SimulationSpeed speed)`, `void ChangeTax(int delta)`
  - `bool IsOverUi(Vector2 panelPosition)`, `bool IsScreenPointOverUi(Vector2 screenPosition)`

Element names, which tests and styles rely on:

| Name | Type | Content |
|---|---|---|
| `day-label`, `population-label`, `budget-label`, `income-label`, `power-label`, `water-label`, `demand-label`, `tax-label` | Label | Caption plus value, such as `Budget 50,000` |
| `debt-label` | Label | `City is in debt`, hidden unless the budget is negative |
| `speed-Paused`, `speed-X1`, `speed-X2`, `speed-X4`, `tax-down`, `tax-up`, `menu-button` | Button | Top bar |
| `info-label` | Label | Information line |
| `tool-Road`, `tool-Bulldoze`, `tool-ZoneResidential`, `tool-ZoneCommercial`, `tool-ZoneIndustrial`, `tool-PowerPlant`, `tool-WaterPump` | Button | Hotkey, name and cost. Class `active` on the current tool |
| `menu` | VisualElement | Full-screen overlay holding `continue-button`, `resume-button`, `save-button`, `new-city-button`, `quit-button` |
| `dialog` | VisualElement | Full-screen overlay holding `dialog-text`, `dialog-ok`, `dialog-cancel` |

- [ ] **Step 1: Write the failing tests**

```csharp
// GameUiTests.cs. Setup is Task 3's, plus: _ui = Object.FindFirstObjectByType<GameUi>(); yield return null;
Label L(string name) => _ui.Root.Q<Label>(name);
static bool Shown(VisualElement e) => e.style.display != DisplayStyle.None;

[Test] public void Launch_ShowsMainMenu_Paused_WithoutContinue() {
    Assert.That(_ui.MenuOpen, Is.True);
    Assert.That(_c.Clock.Speed, Is.EqualTo(SimulationSpeed.Paused));
    Assert.That(Shown(_ui.Root.Q<Button>("continue-button")), Is.False);
    Assert.That(Shown(_ui.Root.Q<Button>("resume-button")), Is.False);
}
[Test] public void NewCity_FromMainMenu_StartsPlaying() {
    _ui.NewCity();
    Assert.That(_ui.MenuOpen, Is.False);
    Assert.That(_c.SessionActive, Is.True);
    Assert.That(_c.Clock.Speed, Is.EqualTo(SimulationSpeed.X1));
}
[Test] public void PauseMenu_Pauses_AndResumesAtPreviousSpeed() {
    _ui.NewCity(); _ui.SetSpeed(SimulationSpeed.X4);
    _ui.OpenPauseMenu();
    Assert.That(_c.Clock.Speed, Is.EqualTo(SimulationSpeed.Paused));
    Assert.That(_ui.MenuOpen, Is.True);
    _ui.Resume();
    Assert.That(_c.Clock.Speed, Is.EqualTo(SimulationSpeed.X4));
    Assert.That(_ui.MenuOpen, Is.False);
}
[Test] public void NewCity_FromPauseMenu_AsksFirst() {
    _ui.NewCity();
    _c.Simulation.Execute(new BuildRoadCommand(C(1, 50), C(5, 50)));
    _ui.OpenPauseMenu(); _ui.NewCity();
    Assert.That(_ui.DialogOpen, Is.True);
    _ui.DismissDialog();
    Assert.That(_c.Simulation.City.HasRoad(C(5, 50)), Is.True);
    Assert.That(_ui.MenuOpen, Is.True);
    _ui.NewCity(); _ui.ConfirmDialog();
    Assert.That(_c.Simulation.City.HasRoad(C(5, 50)), Is.False);
    Assert.That(_ui.MenuOpen, Is.False);
}
[UnityTest] public IEnumerator Save_WritesFile_AndShowsNotice() {
    _ui.NewCity(); _ui.Save();
    yield return null;
    Assert.That(File.Exists(_path), Is.True);
    Assert.That(L("info-label").text, Is.EqualTo("City saved."));
}
[UnityTest] public IEnumerator MainMenu_OffersContinue_WhenSaveExists() {
    _ui.NewCity(); _ui.Save();
    SceneManager.LoadScene("GameWorld");
    yield return null; yield return null;
    var c = Object.FindFirstObjectByType<GameController>();
    c.Saves = new SaveService(_path);
    yield return null;
    var ui = Object.FindFirstObjectByType<GameUi>();
    Assert.That(Shown(ui.Root.Q<Button>("continue-button")), Is.True);
    ui.Continue();
    Assert.That(c.SessionActive, Is.True);
    Assert.That(ui.MenuOpen, Is.False);
}
[Test] public void Continue_CorruptSave_ShowsDialog_AndStaysOnMenu() {
    Directory.CreateDirectory(Path.GetDirectoryName(_path));
    File.WriteAllText(_path, "{ broken");
    _ui.Continue();
    Assert.That(_ui.DialogOpen, Is.True);
    Assert.That(_ui.DialogText, Is.Not.Empty);
    _ui.DismissDialog();
    Assert.That(_ui.MenuOpen, Is.True);
    Assert.That(_c.SessionActive, Is.False);
    Assert.That(File.ReadAllText(_path), Is.EqualTo("{ broken"));
}
[Test] public void Quit_FromPauseMenu_SavesFirst() {
    _ui.NewCity(); _ui.OpenPauseMenu(); _ui.Quit();
    Assert.That(File.Exists(_path), Is.True);
}
[Test] public void Quit_FromMainMenu_WritesNothing() {
    _ui.Quit();
    Assert.That(File.Exists(_path), Is.False);
}
[UnityTest] public IEnumerator Hud_ShowsBudget_AndDebtWarning() {
    _ui.NewCity();
    yield return null;
    Assert.That(L("budget-label").text, Does.Contain("50,000"));
    Assert.That(Shown(L("debt-label")), Is.False);
    _c.Simulation.City.Budget = -90;
    yield return null;
    Assert.That(L("budget-label").text, Does.Contain("-90"));
    Assert.That(Shown(L("debt-label")), Is.True);
    Assert.That(L("debt-label").text, Is.EqualTo("City is in debt"));
}
[UnityTest] public IEnumerator Hud_ShowsRefusal_InWords() {
    _ui.NewCity();
    _c.Tools.SelectTool(BuildTool.PowerPlant);
    _c.Tools.PointerDown(C(40, 5));
    yield return null;
    Assert.That(L("info-label").text, Is.EqualTo("Too far from a connected road."));
    Assert.That(_ui.Root.Q<Button>("tool-PowerPlant").ClassListContains("active"), Is.True);
}
[Test] public void HandleEscape_ClosesInOrder() {
    _ui.NewCity();
    _c.Tools.SelectTool(BuildTool.Road); _c.Tools.PointerDown(C(1, 50));
    _ui.HandleEscape();
    Assert.That(_c.Tools.IsDragging, Is.False);
    Assert.That(_c.Tools.Tool, Is.EqualTo(BuildTool.Road));
    _ui.HandleEscape();
    Assert.That(_c.Tools.Tool, Is.EqualTo(BuildTool.None));
    _ui.HandleEscape();
    Assert.That(_ui.MenuOpen, Is.True);
    _ui.HandleEscape();
    Assert.That(_ui.MenuOpen, Is.False);
}
[Test] public void HandleEscape_AtMainMenu_KeepsMenuOpen() {
    _ui.HandleEscape();
    Assert.That(_ui.MenuOpen, Is.True);
}
[Test] public void ChangeTax_StaysInRange() {
    _ui.NewCity(); _ui.ChangeTax(1);
    Assert.That(_c.Simulation.City.TaxRate, Is.EqualTo(11));
    for (int i = 0; i < 40; i++) _ui.ChangeTax(1);
    Assert.That(_c.Simulation.City.TaxRate, Is.EqualTo(30));
    for (int i = 0; i < 40; i++) _ui.ChangeTax(-1);
    Assert.That(_c.Simulation.City.TaxRate, Is.EqualTo(0));
}
[UnityTest] public IEnumerator UnwiredInterface_LogsOnce_AndDisablesItself() {
    LogAssert.Expect(LogType.Error, new Regex("GameUi"));
    var loose = new GameObject("loose").AddComponent<GameUi>();
    yield return null; yield return null;
    Assert.That(loose.enabled, Is.False);
    Assert.That(loose.MenuOpen, Is.False);
    Object.Destroy(loose.gameObject);
}

// GameWorldSceneTests.cs
[Test] public void Scene_HasWiredInterface() {
    var ui = Object.FindFirstObjectByType<GameUi>();
    Assert.That(ui, Is.Not.Null);
    var panel = ui.GetComponent<UIDocument>().panelSettings;
    Assert.That(panel, Is.Not.Null);
    Assert.That(panel.themeStyleSheet, Is.Not.Null);
}
```

- [ ] **Step 2: Run to verify failure**

Run: `powershell -File tools/unity-tests.ps1 -Platform PlayMode`
Expected: non-zero exit with `error CS0246` for `GameUi`.

- [ ] **Step 3: Implement**

`MetropolisTheme.tss` holds two lines: `@import url("unity-theme://default");` and `@import url("Metropolis.uss");`.

`Metropolis.uss`: dark translucent bars, 16px light text, buttons at least 36px tall, `.active` highlighted, `.debt` red, overlays covering the screen with a dimmed background and a centred column.

Add `"com.unity.modules.uielements": "1.0.0"` to `Packages/manifest.json`, so the game assembly may use UI Toolkit directly.

`GameUi` builds the tree in `Start`. If `game`, the `UIDocument` or its `panelSettings` is missing, it logs one error starting with `GameUi:` and disables itself. A `GameUi` that never built its tree reports `MenuOpen` false and is never over the pointer, so the game stays playable by hotkey. The three-second notice uses unscaled time. The root and pure layout containers use `PickingMode.Ignore`. Each button's `clicked` calls the matching public method.

State is a screen (`MainMenu`, `Playing`, `PauseMenu`) plus an optional dialog with a pending confirm action. `MenuOpen` is true unless the screen is `Playing` with no dialog. Opening a menu stores the clock speed and pauses. `Resume` restores it. A new session sets `X1`. `Resume` and `OpenPauseMenu` do nothing without a session.

`Update` refreshes labels from the simulation, setting text only when it changed, and shows `continue-button` only on the main menu while `game.Saves.Exists`. The information line shows a three-second notice first, then `HudText.Failure(Tools.LastFailure)`, then the hovered building's definition ID and `HudText.Status`.

`Quit` saves first when a session is active, shows the error in the dialog and returns if saving fails, and otherwise calls `Application.Quit()`. `Save` at the main menu does nothing.

`WorldBuilder` creates `Assets/_Project/UI/MetropolisPanel.asset` (`PanelSettings`, scale with screen size, reference 1600×900, theme assigned), a `UI` object with `UIDocument` and `GameUi`, and no longer adds `DebugReadout`. Regenerate the scene.

- [ ] **Step 4: Run to verify success**

Run: `powershell -File tools/unity-tests.ps1 -Platform EditMode`, then `-Platform PlayMode`
Expected: both failed=0.

- [ ] **Step 5: Commit**

```bash
git add -A Assets ProjectSettings
git commit -m "feat: add menu, HUD and toolbar interface"
```

### Task 5: Input rules

**Files:**
- Modify: `Assets/_Project/Scripts/Game/InputBridge.cs`, `Assets/_Project/Scripts/GameEditor/WorldBuilder.cs`
- Test: one test added to `GameUiTests.cs`, one to `GameWorldSceneTests.cs`

**Interfaces:**
- Consumes: `GameUi.MenuOpen`, `IsScreenPointOverUi`, `HandleEscape`, `Save`, `SetSpeed`, and `ToolController.CancelDrag`.
- `InputBridge` gains serialized `GameUi ui`, wired by `WorldBuilder`.

- [ ] **Step 1: Write the failing tests**

```csharp
// GameUiTests.cs
[UnityTest] public IEnumerator Pointer_OverToolbarOrMenu_IsOverUi() {
    _ui.NewCity();
    yield return null; yield return null;
    var button = _ui.Root.Q<Button>("tool-Road");
    Assert.That(float.IsNaN(button.worldBound.width), Is.False);
    Assert.That(_ui.IsOverUi(button.worldBound.center), Is.True);
    Assert.That(_ui.IsOverUi(_ui.Root.worldBound.center), Is.False);
    _ui.OpenPauseMenu();
    yield return null;
    Assert.That(_ui.IsOverUi(_ui.Root.worldBound.center), Is.True);
}

// GameWorldSceneTests.cs
[Test] public void Scene_InputKnowsTheInterface() {
    var input = new SerializedObject(Object.FindFirstObjectByType<InputBridge>());
    Assert.That(input.FindProperty("ui").objectReferenceValue, Is.Not.Null);
}
```

If `worldBound` is unresolved under `-nographics`, remove `-nographics` for PlayMode in `tools/unity-tests.ps1`.

- [ ] **Step 2: Run to verify failure**

Run: `powershell -File tools/unity-tests.ps1 -Platform EditMode`
Expected: `Scene_InputKnowsTheInterface` fails, because the property does not exist.

- [ ] **Step 3: Implement the rules of spec section 6**

- With `ui.MenuOpen`: only Esc is read, and it calls `ui.HandleEscape()`.
- Esc otherwise calls `ui.HandleEscape()`. The right mouse button still calls `Tools.Cancel()`.
- F5 calls `ui.Save()`. Speed keys call `ui.SetSpeed`.
- When `ui.IsScreenPointOverUi(mousePosition)` is true, the cell is `null` and `PointerDown` is not sent.
- `OnApplicationFocus(false)` calls `Tools.CancelDrag()`.
- With no `ui` assigned, behaviour stays as it is today.

`IsScreenPointOverUi` flips Y, converts with `RuntimePanelUtils.ScreenToPanel`, and returns whether `panel.Pick` finds an element. Regenerate the scene.

- [ ] **Step 4: Run to verify success**

Run: `powershell -File tools/unity-tests.ps1 -Platform EditMode`, then `-Platform PlayMode`
Expected: both failed=0.

- [ ] **Step 5: Commit**

```bash
git add -A Assets ProjectSettings tools
git commit -m "feat: block world input under the interface and menus"
```

### Task 6: Windows build and verification

**Files:**
- Create: `Assets/_Project/Scripts/GameEditor/BuildScript.cs`, `Assets/_Project/Scripts/Game/LaunchCapture.cs`, `tools/build-windows.ps1`, `tools/check-player.ps1`
- Modify: `Assets/_Project/Scripts/GameEditor/WorldBuilder.cs`, `Packages/manifest.json`, `docs/superpowers/specs/2026-11-11-game-interface-design.md`

**Interfaces:**
- Produces: `Metropolis.GameEditor.BuildScript.BuildWindows()`, writing `Builds/Windows/Metropolis.exe` for `StandaloneWindows64` with the Mono backend, and exiting the Editor with code 1 when the build fails.
- Produces: `LaunchCapture : MonoBehaviour` with serialized `GameUi ui`. It acts only when the command line holds `-capture <folder>`.

- [ ] **Step 1: Write the player check first**

`tools/check-player.ps1` starts `Builds/Windows/Metropolis.exe -capture <temp folder> -logFile <temp folder>\player.log`, waits up to 120 seconds for it to exit, and fails when: the exe is missing, the process had to be killed, `menu.png` or `hud.png` is missing, or the log contains `Exception` or `GameController:` or `GameUi:`. It prints the two image paths on success.

- [ ] **Step 2: Run to verify failure**

Run: `powershell -File tools/check-player.ps1`
Expected: non-zero exit, because the exe does not exist.

- [ ] **Step 3: Implement**

Add `"com.unity.modules.screencapture": "1.0.0"` to `Packages/manifest.json`.

`LaunchCapture` waits 60 frames, captures `menu.png` with `ScreenCapture.CaptureScreenshot`, calls `ui.NewCity()`, waits 60 frames, captures `hud.png`, waits 30 frames, then calls `Application.Quit()`. `WorldBuilder` adds it to the `Game` object and wires `ui`. Regenerate the scene.

`tools/build-windows.ps1` runs Unity with `-batchmode -quit -executeMethod Metropolis.GameEditor.BuildScript.BuildWindows`, prints any `error` lines of the build report from the log, and exits non-zero when Unity fails or the exe is missing.

- [ ] **Step 4: Build and check**

Run: `powershell -File tools/build-windows.ps1`, then `powershell -File tools/check-player.ps1`
Expected: both exit 0. Warn the developer first: the game window opens for a few seconds.

Open `menu.png` and `hud.png` and look at them. The menu must be centred and readable. The HUD must show the top bar, the toolbar and the entry road, with no overlapping or clipped text. Fix the USS and repeat until they do.

- [ ] **Step 5: Run every suite**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests`, then both Unity platforms.
Expected: all pass.

- [ ] **Step 6: [Developer] Playtest the built game**

Double-click `Builds\Windows\Metropolis.exe` and report any line that fails:

1. The menu shows `New City` and `Quit`. `Continue` is absent on the first launch.
2. `New City` shows the map, the top bar and the toolbar.
3. Toolbar buttons select tools, and the active one is highlighted. A click on a button builds nothing beneath it.
4. Build a road, one power plant, one water pump and a residential zone. Buildings grow.
5. Speed buttons and Space work. Tax `+` and `-` change the rate.
6. A refused action explains itself in words above the toolbar.
7. `Menu`, then `Save`, shows `City saved.` after `Resume`.
8. `Menu`, then `Quit`, closes the game.
9. Launch again. `Continue` restores the same city, day and budget.
10. `Menu`, then `New City`, asks for confirmation first.

- [ ] **Step 7: Record and commit**

Replace `**Status:** Awaiting review` in the spec with `**Status:** Implemented`.

```bash
git add -A Assets ProjectSettings tools docs
git commit -m "feat: add Windows build and built-player check"
```

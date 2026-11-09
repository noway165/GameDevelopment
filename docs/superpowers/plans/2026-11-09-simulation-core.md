# Simulation Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the engine-free METROPOLIS city simulation, with commands, daily tick, save and load, fully tested with `dotnet test`.

**Architecture:** Plain C# classes live under `Assets/_Project/Scripts/Simulation`, compiled by Unity through an asmdef and by .NET through csproj files that link the same sources. `CityState` holds data, static systems run in a fixed daily order, and commands are the only way a player changes the city.

**Tech Stack:** C# 9.0, .NET Standard 2.1, Newtonsoft.Json 13.0.3, NUnit 3.14.0, .NET 9 SDK.

**Spec:** `docs/superpowers/specs/2026-11-09-simulation-core-design.md`

## Global Constraints

- Core and tests compile with `LangVersion` 9.0. No init-only setters, no records.
- Core targets `netstandard2.1` and contains no reference to `UnityEngine`.
- Namespace is `Metropolis.Core`, with tests in `Metropolis.Core.Tests`. The assembly stays `Metropolis.Simulation`. A class named `Simulation` inside a namespace ending in `Simulation` would break lookups from other `Metropolis.*` namespaces.
- Money, population and formulas use integers, with `long` intermediates. Division floors unless the formula says `ceil`.
- Iterate buildings by ascending ID and cells by ascending `y` then `x`. No randomness.
- Tests use only `[Test]`, `[TestCase]`, `[SetUp]` and `Assert.That`, because Unity ships an older NUnit. No `Assert.Multiple`, no async tests.
- Tests read tunable numbers from `TestConfig.Pinned` and `TestCatalog.Spec`, never from defaults, so tuning in Task 9 cannot break them.
- Run all commands from the repository root `E:\Game Development\Metropolis`.
- Commit messages carry no AI attribution line.
- Test snippets show class members only. Put them in a public class named after the file, in namespace `Metropolis.Core.Tests`, with `using NUnit.Framework;`.

Refinements to the spec's layout and API, decided here:
- Each csproj gets its own folder under `dotnet/`, because two projects in one folder share `obj` and clash.
- `TryLoad` also takes `SimulationConfig`, which validation needs for `MaxTaxRate`.
- Formula constants from spec section 9 become `SimulationConfig` properties, so tuning needs no formula change.
- `MaxGridDimension` and `MaxDaysPerAdvance` are added to back Review Focus items 3 and 4.

## Review Focus

1. A player cuts the only road into a living district: buildings report `NoRoad`, population falls to remaining housing, nothing throws. (Task 7)
2. A very large road network: connectivity must not overflow the stack, so flood fill is iterative. (Task 2)
3. A tampered save declaring an enormous grid: load refuses it before allocating anything. (Task 8)
4. A long frame hitch, such as alt-tabbing at 4x: the clock must not queue thousands of days and freeze the game. (Task 7)
5. Saving when no save exists yet, or when the file is locked: the first save succeeds, a locked file returns an error and keeps old data. (Task 8)

---

### Task 1: Project skeleton, config and building catalog

**Files:**
- Create: `.gitignore`
- Create: `dotnet/Metropolis.Simulation/Metropolis.Simulation.csproj`
- Create: `dotnet/Metropolis.Simulation.Tests/Metropolis.Simulation.Tests.csproj`
- Create: `Assets/_Project/Scripts/Simulation/Metropolis.Simulation.asmdef`
- Create: `Assets/_Project/Tests/Simulation/Metropolis.Simulation.Tests.asmdef`
- Create: `Assets/_Project/Scripts/Simulation/SimulationConfig.cs`
- Create: `Assets/_Project/Scripts/Simulation/BuildingDefinition.cs`
- Create: `Assets/_Project/Scripts/Simulation/BuildingCatalog.cs`
- Test: `Assets/_Project/Tests/Simulation/CatalogTests.cs`

**Interfaces:**
- Produces: `enum BuildingKind { Residential, Commercial, Industrial, Power, Water }`
- Produces: `BuildingDefinition(string id, BuildingKind kind, int constructionCost, int maintenance, int capacity, int powerDemand, int waterDemand, int supply)` with matching get-only properties `Id, Kind, ConstructionCost, Maintenance, Capacity, PowerDemand, WaterDemand, Supply`. `Capacity` is residents or jobs.
- Produces: `BuildingCatalog(IEnumerable<BuildingDefinition>)`, `bool TryGet(string id, out BuildingDefinition d)`, `BuildingDefinition Get(string id)`, `IReadOnlyList<BuildingDefinition> Variants(BuildingKind kind)` in catalog order, `static BuildingCatalog Default()`.
- Produces: `SimulationConfig` with get/set properties and these defaults: `GridWidth=100, GridHeight=100, MaxGridDimension=1024, RoadAccessRange=2, StartingBudget=50000L, RoadCostPerCell=10, DefaultTaxRate=10, MaxTaxRate=30, NeutralTaxRate=10, TaxPenaltyPerPoint=5, WorkerPercent=50, ResidentTaxBase=20, JobTaxBase=30, LeaveThreshold=40, LeavePercent=2, SecondsPerDay=2, MaxDaysPerAdvance=8, GrowthDemandThreshold=20, GrowthDemandDivisor=25, MaxGrowthPerType=4, ResidentialDemandBase=40, ResidentialDemandPercent=110, FreeJobDemandWeight=2, CommercialJobPercent=20, IndustrialJobPercent=30`.

- [ ] **Step 1: Create the skeleton**

`.gitignore` ignores `/[Ll]ibrary/`, `/[Tt]emp/`, `/[Oo]bj/`, `/[Bb]uild/`, `/[Bb]uilds/`, `/[Ll]ogs/`, `/[Uu]ser[Ss]ettings/`, `/*.csproj`, `/*.sln`, `/.vs/`, `dotnet/**/bin/`, `dotnet/**/obj/`.

Library csproj: `Microsoft.NET.Sdk`, `TargetFramework` `netstandard2.1`, `LangVersion` `9.0`, `RootNamespace` `Metropolis.Core`, `EnableDefaultCompileItems` false, `<Compile Include="..\..\Assets\_Project\Scripts\Simulation\**\*.cs" />`, package `Newtonsoft.Json` 13.0.3.

Test csproj: `TargetFramework` `net9.0`, `LangVersion` `9.0`, `RootNamespace` `Metropolis.Core.Tests`, `IsPackable` false, `EnableDefaultCompileItems` false, `<Compile Include="..\..\Assets\_Project\Tests\Simulation\**\*.cs" />`, a project reference to the library, packages `NUnit` 3.14.0, `NUnit3TestAdapter` 4.5.0, `Microsoft.NET.Test.Sdk` 17.11.1.

Core asmdef JSON: `"name": "Metropolis.Simulation"`, `"rootNamespace": "Metropolis.Core"`, `"references": []`, `"overrideReferences": true`, `"precompiledReferences": ["Newtonsoft.Json.dll"]`, `"autoReferenced": true`, `"noEngineReferences": true`.

Tests asmdef JSON: `"name": "Metropolis.Simulation.Tests"`, `"references": ["Metropolis.Simulation", "UnityEngine.TestRunner", "UnityEditor.TestRunner"]`, `"includePlatforms": ["Editor"]`, `"overrideReferences": true`, `"precompiledReferences": ["nunit.framework.dll", "Newtonsoft.Json.dll"]`, `"autoReferenced": false`, `"defineConstraints": ["UNITY_INCLUDE_TESTS"]`.

- [ ] **Step 2: Write the failing tests in `CatalogTests.cs`**

```csharp
static BuildingDefinition Def(string id, int maintenance = 1) =>
    new BuildingDefinition(id, BuildingKind.Residential, 0, maintenance, 20, 10, 8, 0);

[Test] public void Default_HasSevenValidDefinitions() {
    var c = BuildingCatalog.Default();
    Assert.That(c.Variants(BuildingKind.Residential).Count, Is.EqualTo(1));
    Assert.That(c.Variants(BuildingKind.Commercial).Count, Is.EqualTo(2));
    Assert.That(c.Variants(BuildingKind.Industrial).Count, Is.EqualTo(2));
    Assert.That(c.Variants(BuildingKind.Power).Count, Is.EqualTo(1));
    Assert.That(c.Variants(BuildingKind.Water).Count, Is.EqualTo(1));
}
[Test] public void Duplicate_Id_Throws() => Assert.That(
    () => new BuildingCatalog(new[] { Def("a"), Def("a") }), Throws.ArgumentException);
[Test] public void Empty_Id_Throws() => Assert.That(
    () => new BuildingCatalog(new[] { Def("") }), Throws.ArgumentException);
[Test] public void Negative_Value_Throws() => Assert.That(
    () => new BuildingCatalog(new[] { Def("a", -1) }), Throws.ArgumentException);
[Test] public void TryGet_Unknown_ReturnsFalse() =>
    Assert.That(BuildingCatalog.Default().TryGet("nope", out _), Is.False);
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests`
Expected: build fails with `CS0246` for `BuildingDefinition`.

- [ ] **Step 4: Implement `SimulationConfig`, `BuildingDefinition`, `BuildingCatalog`**

`Default()` holds the seven definitions from spec section 8. Zoned definitions have `constructionCost` 0.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests`
Expected: `Passed!` with 5 tests.

- [ ] **Step 6: Commit**

```bash
git add .gitignore dotnet Assets
git commit -m "feat: add project skeleton and building catalog"
```

### Task 2: City state and road network

**Files:**
- Create in `Assets/_Project/Scripts/Simulation/`: `Cell.cs`, `Building.cs`, `CityState.cs`, `Statistics.cs`, `RoadNetwork.cs`, `SimulationEvents.cs`, `SimContext.cs`, `DayWork.cs`
- Create: `Assets/_Project/Tests/Simulation/TestWorld.cs`
- Test: `Assets/_Project/Tests/Simulation/RoadNetworkTests.cs`

**Interfaces:**
- Produces: `readonly struct Cell : IEquatable<Cell>` with `Cell(int x, int y)`, `X`, `Y`.
- Produces: `enum ZoneType { None, Residential, Commercial, Industrial }`, `[Flags] enum BuildingStatus { None = 0, NoRoad = 1, NoPower = 2, NoWater = 4 }`.
- Produces: `Building` with `int Id`, `string DefinitionId`, `Cell Cell` (get-only), `int Occupancy`, `BuildingStatus Status` (get/set), `bool IsOperational`.
- Produces: `CityState(string cityName, int width, int height)`, `static CityState CreateNew(string cityName, SimulationConfig config)`, get-only `CityName, Width, Height`, get/set `int Day, Population, TaxRate, NextBuildingId`, get/set `long Budget`, `Cell EntryRoad` = `(0, Height / 2)`, `List<Building> Buildings`, `Statistics Statistics`. Methods: `IsInBounds(Cell)`, `HasRoad(Cell)`, `SetRoad(Cell, bool)`, `GetZone(Cell)`, `SetZone(Cell, ZoneType)`, `Building GetBuilding(Cell)` (null if none), `Building FindBuilding(int id)` (null if none), `Building AddBuilding(string definitionId, Cell cell)` (uses and increments `NextBuildingId`, starting at 1), `RestoreBuilding(int id, string definitionId, Cell cell)`, `RemoveBuilding(Building)`.
- Produces: `RoadNetwork(CityState city, int accessRange)`, `void Rebuild()`, `bool IsConnected(Cell)`, `bool HasRoadAccess(Cell)`.
- Produces: `Statistics` public int fields `PowerSupply, PowerRemaining, WaterSupply, WaterRemaining, Workers, Jobs, Employed, Housing, Arrivals, Departures, UnemploymentPercent, NonRunningPercent, Attractiveness, ResidentialDemand, CommercialDemand, IndustrialDemand`, and long fields `Revenue, Expenses`.
- Produces: `SimulationEvents` with public events `Action<int> DayAdvanced`, `Action<int> PopulationChanged`, `Action<long> BudgetChanged`, `Action<Building> BuildingAdded`, `Action<Building> BuildingRemoved`, `Action<Building> BuildingStatusChanged`, `Action RoadsChanged`, `Action ZonesChanged`, and matching internal `Raise…` methods.
- Produces: `SimContext(CityState city, BuildingCatalog catalog, SimulationConfig config)` with get-only `City, Catalog, Config, Roads, Events`.
- Produces: `DayWork` with `HashSet<int> PowerServed, WaterServed` and `List<Building> StatusChanged, Added`.
- Produces for tests: `TestConfig.Pinned(int width, int height)` sets every Task 1 property to its listed value explicitly. `TestCatalog.Spec()` holds the seven spec definitions plus `power_small` (supply 25), `water_small` (supply 16), and `res_tiny` (Residential, capacity 5, power 5, water 0).
- Produces for tests: `TestWorld.Create(int size = 21, BuildingCatalog catalog = null)`, using `TestCatalog.Spec()` when `catalog` is null, with `Ctx`, `City`, `Stats`, `Work`, `TestWorld Road(int x1, int y1, int x2, int y2)` (sets cells directly and rebuilds the network), `Building Place(string definitionId, int x, int y)` (sets the matching zone for residential, commercial and industrial kinds), `void Zone(ZoneType type, int x, int y)`.

- [ ] **Step 1: Write the failing tests in `RoadNetworkTests.cs`**

```csharp
[Test] public void NewCity_HasEntryBudgetAndDefaultTax() {
    var w = TestWorld.Create(21);
    Assert.That(w.City.HasRoad(new Cell(0, 10)), Is.True);
    Assert.That(w.City.Budget, Is.EqualTo(50000L));
    Assert.That(w.City.TaxRate, Is.EqualTo(10));
    Assert.That(w.City.Day, Is.EqualTo(0));
}
[Test] public void Road_TouchingEntry_IsConnected() {
    var w = TestWorld.Create().Road(0, 10, 5, 10);
    Assert.That(w.Ctx.Roads.IsConnected(new Cell(5, 10)), Is.True);
}
[Test] public void Island_IsNotConnected_AndGivesNoAccess() {
    var w = TestWorld.Create().Road(10, 3, 12, 3);
    Assert.That(w.Ctx.Roads.IsConnected(new Cell(10, 3)), Is.False);
    Assert.That(w.Ctx.Roads.HasRoadAccess(new Cell(10, 4)), Is.False);
}
[TestCase(5, 12, true)] [TestCase(5, 13, false)]
[TestCase(6, 11, true)] [TestCase(7, 11, false)]
public void Access_UsesManhattanRange(int x, int y, bool expected) {
    var w = TestWorld.Create().Road(0, 10, 5, 10);
    Assert.That(w.Ctx.Roads.HasRoadAccess(new Cell(x, y)), Is.EqualTo(expected));
}
[Test] public void RemovingMiddleCell_DisconnectsFarSide() {
    var w = TestWorld.Create().Road(0, 10, 5, 10);
    w.City.SetRoad(new Cell(3, 10), false);
    w.Ctx.Roads.Rebuild();
    Assert.That(w.Ctx.Roads.IsConnected(new Cell(5, 10)), Is.False);
}
[Test] public void FullThousandSquareMap_DoesNotOverflow() {
    var w = TestWorld.Create(1000);
    for (int y = 0; y < 1000; y++) for (int x = 0; x < 1000; x++) w.City.SetRoad(new Cell(x, y), true);
    w.Ctx.Roads.Rebuild();
    Assert.That(w.Ctx.Roads.IsConnected(new Cell(999, 999)), Is.True);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests --filter FullyQualifiedName~RoadNetworkTests`
Expected: build fails with `CS0246` for `TestWorld`.

- [ ] **Step 3: Implement the listed types**

Use flat arrays indexed by `y * Width + x`. Flood fill uses an explicit `Queue<int>`, starting from `EntryRoad`. `HasRoadAccess` scans the Manhattan diamond of radius `accessRange`. `SimContext` constructor calls `Roads.Rebuild()`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests --filter FullyQualifiedName~RoadNetworkTests`
Expected: `Passed!` with 9 tests.

- [ ] **Step 5: Commit**

```bash
git add Assets
git commit -m "feat: add city state and road network"
```

### Task 3: Player commands

**Files:**
- Create in `Assets/_Project/Scripts/Simulation/Commands/`: `ICommand.cs`, `CommandResult.cs`, `BuildRoadCommand.cs`, `RemoveRoadCommand.cs`, `CreateZoneCommand.cs`, `RemoveZoneCommand.cs`, `ConstructBuildingCommand.cs`, `RemoveBuildingCommand.cs`, `SetTaxRateCommand.cs`
- Test: `Assets/_Project/Tests/Simulation/CommandTests.cs`

**Interfaces:**
- Consumes: `SimContext`, `CityState`, `RoadNetwork`, `SimulationEvents` from Task 2.
- Produces: `interface ICommand { CommandResult Execute(SimContext ctx); }`
- Produces: `enum CommandFailure { None, OutOfBounds, NotStraight, CellOccupied, NoRoadAccess, InsufficientFunds, ProtectedRoad, NothingToDo, UnknownDefinition, NotPlayerPlaceable, UnknownBuilding, InvalidTaxRate }`
- Produces: `readonly struct CommandResult` with `CommandFailure Failure`, `bool Succeeded`, `static Ok`, `static Fail(CommandFailure)`.
- Produces constructors: `BuildRoadCommand(Cell from, Cell to)`, `RemoveRoadCommand(Cell from, Cell to)`, `CreateZoneCommand(IReadOnlyList<Cell> cells, ZoneType type)`, `RemoveZoneCommand(IReadOnlyList<Cell> cells)`, `ConstructBuildingCommand(string definitionId, Cell cell)`, `RemoveBuildingCommand(int buildingId)`, `SetTaxRateCommand(int rate)`.

Failure precedence, first match wins:
- Roads: `NotStraight`, `OutOfBounds`, `ProtectedRoad` (remove only) or `CellOccupied` (build only), `NothingToDo`, `InsufficientFunds`.
- Zones: `NothingToDo` for an empty list, `OutOfBounds`, `CellOccupied`, then `NoRoadAccess` (create) or `NothingToDo` for an unzoned cell (remove).
- Buildings: `UnknownDefinition`, `NotPlayerPlaceable`, `OutOfBounds`, `CellOccupied`, `NoRoadAccess`, `InsufficientFunds`.

A cell counts as occupied for a road or a utility if it has a zone, a building or (for utilities) a road.

- [ ] **Step 1: Write the failing tests in `CommandTests.cs`**

```csharp
static Cell C(int x, int y) => new Cell(x, y);
static CommandFailure Run(TestWorld w, ICommand c) => c.Execute(w.Ctx).Failure;
static TestWorld Street() => TestWorld.Create().Road(0, 10, 5, 10);

[Test] public void BuildRoad_ChargesOnlyNewCells_AndConnects() {
    var w = Street();
    Assert.That(Run(w, new BuildRoadCommand(C(3, 10), C(8, 10))), Is.EqualTo(CommandFailure.None));
    Assert.That(w.City.Budget, Is.EqualTo(49970L));
    Assert.That(w.Ctx.Roads.IsConnected(C(8, 10)), Is.True);
}
[Test] public void BuildRoad_AllExisting_IsNothingToDo() =>
    Assert.That(Run(Street(), new BuildRoadCommand(C(1, 10), C(4, 10))), Is.EqualTo(CommandFailure.NothingToDo));
[Test] public void BuildRoad_Diagonal_NotStraight() =>
    Assert.That(Run(Street(), new BuildRoadCommand(C(1, 1), C(2, 2))), Is.EqualTo(CommandFailure.NotStraight));
[Test] public void BuildRoad_OutOfBounds() =>
    Assert.That(Run(Street(), new BuildRoadCommand(C(18, 1), C(25, 1))), Is.EqualTo(CommandFailure.OutOfBounds));
[Test] public void BuildRoad_OverZone_RefusesWholeLine() {
    var w = Street(); w.Zone(ZoneType.Residential, 8, 10);
    Assert.That(Run(w, new BuildRoadCommand(C(6, 10), C(9, 10))), Is.EqualTo(CommandFailure.CellOccupied));
    Assert.That(w.City.HasRoad(C(6, 10)), Is.False);
    Assert.That(w.City.Budget, Is.EqualTo(50000L));
}
[TestCase(30L, CommandFailure.None, 0L)]
[TestCase(29L, CommandFailure.InsufficientFunds, 29L)]
public void BuildRoad_ExactFunds(long budget, CommandFailure expected, long after) {
    var w = Street(); w.City.Budget = budget;
    Assert.That(Run(w, new BuildRoadCommand(C(6, 10), C(8, 10))), Is.EqualTo(expected));
    Assert.That(w.City.Budget, Is.EqualTo(after));
}
[Test] public void RemoveRoad_Entry_IsProtected() =>
    Assert.That(Run(Street(), new RemoveRoadCommand(C(0, 10), C(2, 10))), Is.EqualTo(CommandFailure.ProtectedRoad));
[Test] public void RemoveRoad_IsFree_AndDisconnects() {
    var w = Street();
    Assert.That(Run(w, new RemoveRoadCommand(C(3, 10), C(3, 10))), Is.EqualTo(CommandFailure.None));
    Assert.That(w.City.Budget, Is.EqualTo(50000L));
    Assert.That(w.Ctx.Roads.IsConnected(C(5, 10)), Is.False);
}
[Test] public void RemoveRoad_NoRoad_IsNothingToDo() =>
    Assert.That(Run(Street(), new RemoveRoadCommand(C(1, 1), C(3, 1))), Is.EqualTo(CommandFailure.NothingToDo));
[Test] public void CreateZone_NeedsAccess_AndRefusesWholeList() {
    var w = Street();
    var r = Run(w, new CreateZoneCommand(new[] { C(1, 11), C(1, 15) }, ZoneType.Residential));
    Assert.That(r, Is.EqualTo(CommandFailure.NoRoadAccess));
    Assert.That(w.City.GetZone(C(1, 11)), Is.EqualTo(ZoneType.None));
}
[Test] public void CreateZone_Repaint_IsAllowed_AndFree() {
    var w = Street(); w.Zone(ZoneType.Residential, 1, 11);
    Assert.That(Run(w, new CreateZoneCommand(new[] { C(1, 11) }, ZoneType.Industrial)), Is.EqualTo(CommandFailure.None));
    Assert.That(w.City.GetZone(C(1, 11)), Is.EqualTo(ZoneType.Industrial));
    Assert.That(w.City.Budget, Is.EqualTo(50000L));
}
[Test] public void CreateZone_OnRoad_Occupied() =>
    Assert.That(Run(Street(), new CreateZoneCommand(new[] { C(1, 10) }, ZoneType.Commercial)), Is.EqualTo(CommandFailure.CellOccupied));
[Test] public void RemoveZone_WithBuilding_Occupied() {
    var w = Street(); w.Place("residential_low_01", 1, 11);
    Assert.That(Run(w, new RemoveZoneCommand(new[] { C(1, 11) })), Is.EqualTo(CommandFailure.CellOccupied));
}
[Test] public void RemoveZone_Unzoned_NothingToDo() =>
    Assert.That(Run(Street(), new RemoveZoneCommand(new[] { C(1, 11) })), Is.EqualTo(CommandFailure.NothingToDo));
[Test] public void Construct_ChargesOnce() {
    var w = Street();
    Assert.That(Run(w, new ConstructBuildingCommand("power_plant_01", C(1, 11))), Is.EqualTo(CommandFailure.None));
    Assert.That(w.City.Budget, Is.EqualTo(45000L));
    Assert.That(w.City.Buildings.Count, Is.EqualTo(1));
}
[TestCase("nope", 1, 11, CommandFailure.UnknownDefinition)]
[TestCase("residential_low_01", 1, 11, CommandFailure.NotPlayerPlaceable)]
[TestCase("power_plant_01", 1, 10, CommandFailure.CellOccupied)]
[TestCase("power_plant_01", 1, 15, CommandFailure.NoRoadAccess)]
[TestCase("power_plant_01", 99, 1, CommandFailure.OutOfBounds)]
public void Construct_Refusals(string id, int x, int y, CommandFailure expected) =>
    Assert.That(Run(Street(), new ConstructBuildingCommand(id, C(x, y))), Is.EqualTo(expected));
[Test] public void Construct_NoFunds_ChangesNothing() {
    var w = Street(); w.City.Budget = 4999;
    Assert.That(Run(w, new ConstructBuildingCommand("power_plant_01", C(1, 11))), Is.EqualTo(CommandFailure.InsufficientFunds));
    Assert.That(w.City.Buildings.Count, Is.EqualTo(0));
}
[Test] public void RemoveBuilding_KeepsZone_ForRegrowth() {
    var w = Street(); var b = w.Place("residential_low_01", 1, 11);
    Assert.That(Run(w, new RemoveBuildingCommand(b.Id)), Is.EqualTo(CommandFailure.None));
    Assert.That(w.City.GetBuilding(C(1, 11)), Is.Null);
    Assert.That(w.City.GetZone(C(1, 11)), Is.EqualTo(ZoneType.Residential));
}
[Test] public void RemoveBuilding_Unknown() =>
    Assert.That(Run(Street(), new RemoveBuildingCommand(77)), Is.EqualTo(CommandFailure.UnknownBuilding));
[TestCase(0, CommandFailure.None)] [TestCase(30, CommandFailure.None)]
[TestCase(31, CommandFailure.InvalidTaxRate)] [TestCase(-1, CommandFailure.InvalidTaxRate)]
public void SetTaxRate_Range(int rate, CommandFailure expected) =>
    Assert.That(Run(Street(), new SetTaxRateCommand(rate)), Is.EqualTo(expected));
[Test] public void BuildRoad_RaisesEachEventOnce() {
    var w = Street(); int budget = 0, roads = 0;
    w.Ctx.Events.BudgetChanged += _ => budget++;
    w.Ctx.Events.RoadsChanged += () => roads++;
    new BuildRoadCommand(C(6, 10), C(8, 10)).Execute(w.Ctx);
    Assert.That(budget, Is.EqualTo(1));
    Assert.That(roads, Is.EqualTo(1));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests --filter FullyQualifiedName~CommandTests`
Expected: build fails with `CS0246` for `ICommand`.

- [ ] **Step 3: Implement the commands**

Each command validates everything first, then mutates, then raises its events. Road commands call `ctx.Roads.Rebuild()` after mutating. A command that charges nothing raises no `BudgetChanged`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests --filter FullyQualifiedName~CommandTests`
Expected: `Passed!` with no failures.

- [ ] **Step 5: Commit**

```bash
git add Assets
git commit -m "feat: add player commands"
```

### Task 4: Infrastructure, operation and employment systems

**Files:**
- Create in `Assets/_Project/Scripts/Simulation/Systems/`: `InfrastructureSystem.cs`, `OperationSystem.cs`, `EmploymentSystem.cs`
- Test: `Assets/_Project/Tests/Simulation/InfrastructureTests.cs`

**Interfaces:**
- Consumes: `SimContext`, `DayWork`, `Statistics`, `Building` from Task 2.
- Produces: `static void InfrastructureSystem.Run(SimContext ctx, DayWork work)`, `OperationSystem.Run(SimContext ctx, DayWork work)`, `EmploymentSystem.Run(SimContext ctx)`.
- `InfrastructureSystem` calls `ctx.Roads.Rebuild()` and fills `PowerSupply`, `PowerRemaining`, `WaterSupply`, `WaterRemaining`, `work.PowerServed`, `work.WaterServed`.
- `OperationSystem` sets every `Building.Status` and appends changed buildings to `work.StatusChanged`.
- `EmploymentSystem` fills `Workers`, `Jobs`, `Employed` and job-building `Occupancy`. Non-operational job buildings get 0.

- [ ] **Step 1: Write the failing tests in `InfrastructureTests.cs`**

```csharp
static void Infra(TestWorld w) { InfrastructureSystem.Run(w.Ctx, w.Work); OperationSystem.Run(w.Ctx, w.Work); }
static TestWorld Street() => TestWorld.Create().Road(0, 10, 20, 10);

[Test] public void Shortage_ServesAscendingId_ThenSkipsAndContinues() {
    var w = Street();
    w.Place("power_small", 1, 9); w.Place("water_pump_01", 2, 9);
    var a = w.Place("residential_low_01", 3, 9);
    var b = w.Place("residential_low_01", 4, 9);
    var c = w.Place("residential_low_01", 5, 9);
    var d = w.Place("res_tiny", 6, 9);
    Infra(w);
    Assert.That(a.Status, Is.EqualTo(BuildingStatus.None));
    Assert.That(b.Status, Is.EqualTo(BuildingStatus.None));
    Assert.That(c.Status, Is.EqualTo(BuildingStatus.NoPower));
    Assert.That(d.Status, Is.EqualTo(BuildingStatus.None));
    Assert.That(w.Stats.PowerRemaining, Is.EqualTo(0));
}
[Test] public void PowerAndWater_AreJudgedSeparately() {
    var w = Street();
    w.Place("power_plant_01", 1, 9);
    var a = w.Place("residential_low_01", 3, 9);
    Infra(w);
    Assert.That(a.Status, Is.EqualTo(BuildingStatus.NoWater));
    Assert.That(w.Stats.PowerRemaining, Is.EqualTo(490));
}
[Test] public void NoRoad_ConsumesNothing_AndUtilityWithoutRoadSuppliesNothing() {
    var w = Street();
    w.Place("power_plant_01", 1, 2);
    var a = w.Place("residential_low_01", 3, 2);
    Infra(w);
    Assert.That(a.Status, Is.EqualTo(BuildingStatus.NoRoad | BuildingStatus.NoPower | BuildingStatus.NoWater));
    Assert.That(w.Stats.PowerSupply, Is.EqualTo(0));
}
[Test] public void Utility_NeedsOnlyRoad() {
    var w = Street(); var p = w.Place("power_plant_01", 1, 9);
    Infra(w);
    Assert.That(p.IsOperational, Is.True);
}
[Test] public void StatusChanged_ListsOnlyChanges() {
    var w = Street(); w.Place("power_plant_01", 1, 9);
    var a = w.Place("residential_low_01", 3, 9);
    Infra(w);
    Assert.That(w.Work.StatusChanged, Is.EquivalentTo(new[] { a }));
    w.Work.StatusChanged.Clear();
    Infra(w);
    Assert.That(w.Work.StatusChanged.Count, Is.EqualTo(0));
}
[TestCase(9, 4)] [TestCase(10, 5)] [TestCase(0, 0)]
public void Workers_FloorHalfPopulation(int population, int workers) {
    var w = Street(); w.City.Population = population;
    EmploymentSystem.Run(w.Ctx);
    Assert.That(w.Stats.Workers, Is.EqualTo(workers));
}
[Test] public void Employed_FillsJobsAscending_SkipsBrokenBuildings() {
    var w = Street(); w.City.Population = 30;
    var a = w.Place("commercial_small_01", 1, 9);
    var broken = w.Place("commercial_large_01", 2, 9); broken.Status = BuildingStatus.NoPower;
    var c = w.Place("industrial_small_01", 3, 9);
    EmploymentSystem.Run(w.Ctx);
    Assert.That(w.Stats.Jobs, Is.EqualTo(25));
    Assert.That(w.Stats.Employed, Is.EqualTo(15));
    Assert.That(a.Occupancy, Is.EqualTo(10));
    Assert.That(broken.Occupancy, Is.EqualTo(0));
    Assert.That(c.Occupancy, Is.EqualTo(5));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests --filter FullyQualifiedName~InfrastructureTests`
Expected: build fails with `CS0103` for `InfrastructureSystem`.

- [ ] **Step 3: Implement the three systems per spec section 9, steps 1 to 3**

Utilities have zero demand and are served trivially. Clear `work` sets at the start of `InfrastructureSystem.Run`, but leave `StatusChanged` and `Added` to the caller.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests --filter FullyQualifiedName~InfrastructureTests`
Expected: `Passed!` with no failures.

- [ ] **Step 5: Commit**

```bash
git add Assets
git commit -m "feat: add infrastructure, operation and employment systems"
```

### Task 5: Population and demand systems

**Files:**
- Create in `Assets/_Project/Scripts/Simulation/Systems/`: `PopulationSystem.cs`, `DemandSystem.cs`
- Test: `Assets/_Project/Tests/Simulation/PopulationDemandTests.cs`

**Interfaces:**
- Consumes: `Statistics.Workers`, `Jobs`, `Employed` from Task 4.
- Produces: `static void PopulationSystem.Run(SimContext ctx)`, `static void PopulationSystem.FillHousing(SimContext ctx)`, `static int PopulationSystem.Attractiveness(int taxRate, int unemploymentPercent, int nonRunningPercent, SimulationConfig config)`, `static int PopulationSystem.Arrivals(int vacant, int attractiveness)`, `static int PopulationSystem.Departures(int population, int attractiveness, SimulationConfig config)`.
- Produces: `static void DemandSystem.Run(SimContext ctx)`, `static int DemandSystem.Demand(long target, long capacity)`.
- `PopulationSystem.Run` fills `Housing`, `Arrivals`, `Departures`, `UnemploymentPercent`, `NonRunningPercent`, `Attractiveness`.

- [ ] **Step 1: Write the failing tests in `PopulationDemandTests.cs`**

```csharp
static readonly SimulationConfig Cfg = TestConfig.Pinned(21, 21);
static TestWorld Street() => TestWorld.Create().Road(0, 10, 20, 10);

[TestCase(10, 0, 0, 100)] [TestCase(20, 40, 10, 20)]
[TestCase(30, 0, 0, 0)] [TestCase(5, 0, 0, 100)] [TestCase(30, 100, 100, 0)]
public void Attractiveness(int tax, int unemployment, int nonRunning, int expected) =>
    Assert.That(PopulationSystem.Attractiveness(tax, unemployment, nonRunning, Cfg), Is.EqualTo(expected));

[TestCase(15, 20, 1)] [TestCase(40, 100, 4)] [TestCase(15, 100, 2)]
[TestCase(100, 0, 0)] [TestCase(0, 100, 0)]
public void Arrivals_UseCeil(int vacant, int attractiveness, int expected) =>
    Assert.That(PopulationSystem.Arrivals(vacant, attractiveness), Is.EqualTo(expected));

[TestCase(100, 39, 2)] [TestCase(100, 40, 0)] [TestCase(49, 0, 0)]
public void Departures_OnlyBelowThreshold(int population, int attractiveness, int expected) =>
    Assert.That(PopulationSystem.Departures(population, attractiveness, Cfg), Is.EqualTo(expected));

[Test] public void Population_CappedToHousing_ThenNoArrivals() {
    var w = Street(); w.City.Population = 50;
    w.Place("residential_low_01", 1, 9); w.Place("residential_low_01", 2, 9);
    PopulationSystem.Run(w.Ctx);
    Assert.That(w.City.Population, Is.EqualTo(40));
    Assert.That(w.Stats.Arrivals, Is.EqualTo(0));
}
[Test] public void Residents_FillHousingAscending() {
    var w = Street(); w.City.Population = 25;
    var a = w.Place("residential_low_01", 1, 9);
    var b = w.Place("residential_low_01", 2, 9);
    PopulationSystem.Run(w.Ctx);
    Assert.That(w.City.Population, Is.EqualTo(27));
    Assert.That(a.Occupancy, Is.EqualTo(20));
    Assert.That(b.Occupancy, Is.EqualTo(7));
}
[Test] public void BrokenHousing_DoesNotCount_AndLowersAttractiveness() {
    var w = Street();
    w.Place("residential_low_01", 1, 9);
    w.Place("residential_low_01", 2, 9).Status = BuildingStatus.NoPower;
    PopulationSystem.Run(w.Ctx);
    Assert.That(w.Stats.Housing, Is.EqualTo(20));
    Assert.That(w.Stats.NonRunningPercent, Is.EqualTo(50));
    Assert.That(w.Stats.Attractiveness, Is.EqualTo(50));
}
[Test] public void EmptyCity_HasNoDivideByZero() {
    var w = Street();
    PopulationSystem.Run(w.Ctx);
    Assert.That(w.Stats.UnemploymentPercent, Is.EqualTo(0));
    Assert.That(w.Stats.NonRunningPercent, Is.EqualTo(0));
    Assert.That(w.City.Population, Is.EqualTo(0));
}
[TestCase(100L, 0L, 100)] [TestCase(100L, 80L, 20)] [TestCase(0L, 0L, 0)]
[TestCase(0L, 50L, 0)] [TestCase(232L, 100L, 56)]
public void Demand_Formula(long target, long capacity, int expected) =>
    Assert.That(DemandSystem.Demand(target, capacity), Is.EqualTo(expected));

[Test] public void Demand_UsesAllBuildings_AndFreeJobs() {
    var w = Street(); w.City.Population = 28;
    w.Stats.Workers = 9; w.Stats.Jobs = 90;
    for (int x = 1; x <= 5; x++) w.Place("residential_low_01", x, 9).Status = BuildingStatus.NoPower;
    DemandSystem.Run(w.Ctx);
    Assert.That(w.Stats.ResidentialDemand, Is.EqualTo(56));
    Assert.That(w.Stats.CommercialDemand, Is.EqualTo(100));
    Assert.That(w.Stats.IndustrialDemand, Is.EqualTo(100));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests --filter FullyQualifiedName~PopulationDemandTests`
Expected: build fails with `CS0103` for `PopulationSystem`.

- [ ] **Step 3: Implement both systems per spec section 9, steps 4 and 5**

Departures and arrivals both apply on the same day. `FillHousing` assigns `City.Population` to operational residential buildings by ascending ID and sets broken ones to 0.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests --filter FullyQualifiedName~PopulationDemandTests`
Expected: `Passed!` with no failures.

- [ ] **Step 5: Commit**

```bash
git add Assets
git commit -m "feat: add population and demand systems"
```

### Task 6: Growth and economy systems

**Files:**
- Create in `Assets/_Project/Scripts/Simulation/Systems/`: `GrowthSystem.cs`, `EconomySystem.cs`
- Test: `Assets/_Project/Tests/Simulation/GrowthEconomyTests.cs`

**Interfaces:**
- Consumes: `Statistics` demand and remaining supply from Tasks 4 and 5, `Statistics.Employed`.
- Produces: `static void GrowthSystem.Run(SimContext ctx, DayWork work)`, appending to `work.Added` and reducing `PowerRemaining` and `WaterRemaining`.
- Produces: `static void EconomySystem.Run(SimContext ctx)`, filling `Revenue`, `Expenses` and updating `City.Budget`.

- [ ] **Step 1: Write the failing tests in `GrowthEconomyTests.cs`**

```csharp
static TestWorld Zoned(int demand, int cells = 8) {
    var w = TestWorld.Create().Road(0, 10, 20, 10);
    for (int x = 1; x <= cells; x++) w.Zone(ZoneType.Residential, x, 9);
    w.Stats.ResidentialDemand = demand; w.Stats.PowerRemaining = 1000; w.Stats.WaterRemaining = 1000;
    return w;
}
[TestCase(19, 0)] [TestCase(20, 1)] [TestCase(25, 1)] [TestCase(50, 2)] [TestCase(100, 4)]
public void GrowthCount(int demand, int expected) {
    var w = Zoned(demand);
    GrowthSystem.Run(w.Ctx, w.Work);
    Assert.That(w.City.Buildings.Count, Is.EqualTo(expected));
}
[Test] public void Growth_TakesCellsInAscendingOrder_AndIsFree() {
    var w = Zoned(50);
    GrowthSystem.Run(w.Ctx, w.Work);
    Assert.That(w.City.GetBuilding(new Cell(1, 9)), Is.Not.Null);
    Assert.That(w.City.GetBuilding(new Cell(2, 9)), Is.Not.Null);
    Assert.That(w.City.Budget, Is.EqualTo(50000L));
    Assert.That(w.Work.Added.Count, Is.EqualTo(2));
}
[Test] public void Growth_StopsWhenSpareRunsOut_AndReserves() {
    var w = Zoned(100); w.Stats.PowerRemaining = 15;
    GrowthSystem.Run(w.Ctx, w.Work);
    Assert.That(w.City.Buildings.Count, Is.EqualTo(1));
    Assert.That(w.Stats.PowerRemaining, Is.EqualTo(5));
}
[Test] public void Growth_SkipsCellsWithoutRoadAccess() {
    var w = TestWorld.Create().Road(0, 10, 5, 10);
    w.Zone(ZoneType.Residential, 15, 3);
    w.Stats.ResidentialDemand = 100; w.Stats.PowerRemaining = 100; w.Stats.WaterRemaining = 100;
    GrowthSystem.Run(w.Ctx, w.Work);
    Assert.That(w.City.Buildings.Count, Is.EqualTo(0));
}
[TestCase(1, 9, "commercial_small_01")] [TestCase(2, 9, "commercial_large_01")]
public void Growth_PicksVariantFromCell(int x, int y, string expected) {
    var w = TestWorld.Create().Road(0, 10, 20, 10);
    w.Zone(ZoneType.Commercial, x, y);
    w.Stats.CommercialDemand = 100; w.Stats.PowerRemaining = 100; w.Stats.WaterRemaining = 100;
    GrowthSystem.Run(w.Ctx, w.Work);
    Assert.That(w.City.GetBuilding(new Cell(x, y)).DefinitionId, Is.EqualTo(expected));
}
[Test] public void Economy_RevenueAndMaintenance_IncludingBrokenBuildings() {
    var w = TestWorld.Create(); w.City.Population = 100; w.Stats.Employed = 40;
    w.Place("residential_low_01", 1, 9).Status = BuildingStatus.NoRoad;
    w.Place("power_plant_01", 2, 9);
    EconomySystem.Run(w.Ctx);
    Assert.That(w.Stats.Revenue, Is.EqualTo(320L));
    Assert.That(w.Stats.Expenses, Is.EqualTo(105L));
    Assert.That(w.City.Budget, Is.EqualTo(50215L));
}
[Test] public void Economy_MayGoNegative() {
    var w = TestWorld.Create(); w.City.Budget = 10;
    w.Place("power_plant_01", 2, 9);
    EconomySystem.Run(w.Ctx);
    Assert.That(w.City.Budget, Is.EqualTo(-90L));
}
```

Variant check: at `(1, 9)`, `(1 * 31 + 9) % 2 = 0` picks the first variant. At `(2, 9)`, `71 % 2 = 1` picks the second.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests --filter FullyQualifiedName~GrowthEconomyTests`
Expected: build fails with `CS0103` for `GrowthSystem`.

- [ ] **Step 3: Implement both systems per spec section 9, steps 6 and 7**

Growth handles types in the order residential, commercial, industrial. When the chosen variant does not fit the spare supply, stop growing that type for the day.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests --filter FullyQualifiedName~GrowthEconomyTests`
Expected: `Passed!` with no failures.

- [ ] **Step 5: Commit**

```bash
git add Assets
git commit -m "feat: add growth and economy systems"
```

### Task 7: Simulation loop and clock

**Files:**
- Create: `Assets/_Project/Scripts/Simulation/Simulation.cs`
- Create: `Assets/_Project/Scripts/Simulation/SimulationClock.cs`
- Test: `Assets/_Project/Tests/Simulation/SimulationTests.cs`

**Interfaces:**
- Consumes: all systems from Tasks 4 to 6, `ICommand` from Task 3.
- Produces: `Simulation(CityState city, BuildingCatalog catalog, SimulationConfig config)`, with get-only `City`, `Events`, `Context`, plus `CommandResult Execute(ICommand command)`, `void AdvanceDay()`, `void RefreshDerived()`. The constructor calls `RefreshDerived()`.
- `RefreshDerived()` runs infrastructure, operation, employment, `PopulationSystem.FillHousing` and demand, with no change to day, population or budget, and raises no events.
- Produces: `enum SimulationSpeed { Paused, X1, X2, X4 }`, `SimulationClock(SimulationConfig config)`, `SimulationSpeed Speed { get; set; }` defaulting to `X1`, `int Advance(double realSeconds)`.
- `Advance` returns 0 for non-positive input and at most `MaxDaysPerAdvance`. When capped, it discards the accumulated remainder.

- [ ] **Step 1: Write the failing tests in `SimulationTests.cs`**

```csharp
static Simulation Sim(TestWorld w) => new Simulation(w.City, w.Ctx.Catalog, w.Ctx.Config);
static Cell C(int x, int y) => new Cell(x, y);

[Test] public void AdvanceDay_IncrementsDay_AndGrowsFromEmptyZone() {
    var w = TestWorld.Create().Road(0, 10, 20, 10);
    w.Place("power_plant_01", 1, 9); w.Place("water_pump_01", 2, 9);
    w.Zone(ZoneType.Residential, 3, 9);
    var s = Sim(w);
    s.AdvanceDay();
    Assert.That(w.City.Day, Is.EqualTo(1));
    Assert.That(w.City.GetBuilding(C(3, 9)), Is.Not.Null);
    Assert.That(w.City.Population, Is.EqualTo(0));
    s.AdvanceDay();
    Assert.That(w.City.Population, Is.EqualTo(2));
}
[Test] public void DailyEvents_FireOnce_AfterTheDayIsFinished() {
    var w = TestWorld.Create().Road(0, 10, 20, 10);
    w.Place("power_plant_01", 1, 9);
    var s = Sim(w); int days = 0; long seen = 0;
    s.Events.DayAdvanced += d => { days++; seen = w.City.Budget; };
    s.AdvanceDay();
    Assert.That(days, Is.EqualTo(1));
    Assert.That(seen, Is.EqualTo(49900L));
}
[Test] public void GrownBuilding_RaisesAddedOnlyAfterTick() {
    var w = TestWorld.Create().Road(0, 10, 20, 10);
    w.Place("power_plant_01", 1, 9); w.Place("water_pump_01", 2, 9);
    w.Zone(ZoneType.Residential, 3, 9);
    var s = Sim(w); int added = 0;
    s.Events.BuildingAdded += _ => added++;
    s.AdvanceDay();
    Assert.That(added, Is.EqualTo(1));
}
[Test] public void Command_WorksWhilePaused_WithoutTick() {
    var w = TestWorld.Create();
    var s = Sim(w);
    Assert.That(s.Execute(new BuildRoadCommand(C(1, 10), C(3, 10))).Succeeded, Is.True);
    Assert.That(w.City.Day, Is.EqualTo(0));
}
[Test] public void CuttingOnlyRoad_DropsPopulation_WithoutThrowing() {
    var w = TestWorld.Create().Road(0, 10, 20, 10);
    w.Place("power_plant_01", 2, 9); w.Place("water_pump_01", 3, 9);
    var home = w.Place("residential_low_01", 15, 9);
    var s = Sim(w);
    for (int i = 0; i < 10; i++) s.AdvanceDay();
    Assert.That(w.City.Population, Is.GreaterThan(0));
    s.Execute(new RemoveRoadCommand(C(10, 10), C(10, 10)));
    s.AdvanceDay();
    Assert.That(w.City.Population, Is.EqualTo(0));
    Assert.That(home.Status.HasFlag(BuildingStatus.NoRoad), Is.True);
}
[Test] public void RefreshDerived_ChangesNoSourceData() {
    var w = TestWorld.Create().Road(0, 10, 20, 10);
    w.City.Population = 7;
    var s = Sim(w);
    s.RefreshDerived();
    Assert.That(w.City.Population, Is.EqualTo(7));
    Assert.That(w.City.Budget, Is.EqualTo(50000L));
    Assert.That(w.City.Day, Is.EqualTo(0));
}
static SimulationClock Clock(SimulationSpeed speed) =>
    new SimulationClock(TestConfig.Pinned(21, 21)) { Speed = speed };

[TestCase(SimulationSpeed.Paused, 100.0, 0)] [TestCase(SimulationSpeed.X1, 1.0, 0)]
[TestCase(SimulationSpeed.X1, 2.0, 1)] [TestCase(SimulationSpeed.X2, 1.0, 1)]
[TestCase(SimulationSpeed.X4, 2.0, 4)] [TestCase(SimulationSpeed.X1, -5.0, 0)]
public void Clock_Advance(SimulationSpeed speed, double seconds, int days) =>
    Assert.That(Clock(speed).Advance(seconds), Is.EqualTo(days));
[Test] public void Clock_Accumulates() {
    var c = Clock(SimulationSpeed.X1);
    Assert.That(c.Advance(1.5), Is.EqualTo(0));
    Assert.That(c.Advance(0.5), Is.EqualTo(1));
}
[Test] public void Clock_LongHitch_IsCapped_AndNotQueued() {
    var c = Clock(SimulationSpeed.X4);
    Assert.That(c.Advance(1000.0), Is.EqualTo(8));
    Assert.That(c.Advance(0.1), Is.EqualTo(0));
}
```

Check of the first test: on day 1, `residential_low_01` grows from residential demand 100, with no housing yet. On day 2, housing is 20, attractiveness 100, so arrivals are `ceil(20 * 100 / 1000) = 2`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests --filter FullyQualifiedName~SimulationTests`
Expected: build fails with `CS0246` for `Simulation`.

- [ ] **Step 3: Implement `Simulation` and `SimulationClock`**

`AdvanceDay` runs the spec section 9 order and keeps a fresh `DayWork` per call. Step 8 raises, in order: `BuildingStatusChanged` per item, `BuildingAdded` per item, `PopulationChanged` if changed, `BudgetChanged` if changed, `DayAdvanced`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests --filter FullyQualifiedName~SimulationTests`
Expected: `Passed!` with no failures.

- [ ] **Step 5: Commit**

```bash
git add Assets
git commit -m "feat: add simulation loop and clock"
```

### Task 8: Save and load

**Files:**
- Create in `Assets/_Project/Scripts/Simulation/Save/`: `CitySaveData.cs`, `SaveSerializer.cs`, `SaveFile.cs`
- Test: `Assets/_Project/Tests/Simulation/SaveTests.cs`

**Interfaces:**
- Consumes: `CityState`, `BuildingCatalog`, `SimulationConfig`.
- Produces: `static string SaveSerializer.Serialize(CityState city)`.
- Produces: `static bool SaveSerializer.TryLoad(string json, BuildingCatalog catalog, SimulationConfig config, out CityState state, out IReadOnlyList<string> errors)`. On failure, `state` is null and `errors` is non-empty.
- Produces: `static bool SaveFile.TryWrite(string path, string json, out string error)`, `static bool SaveFile.TryRead(string path, out string json, out string error)`.

- [ ] **Step 1: Write the failing tests in `SaveTests.cs`**

```csharp
static TestWorld Built() {
    var w = TestWorld.Create().Road(0, 10, 20, 10);
    w.Place("power_plant_01", 1, 9);
    w.Place("residential_low_01", 2, 9);
    return w;
}
static bool Load(string json, out CityState s, out IReadOnlyList<string> e) {
    var w = TestWorld.Create();
    return SaveSerializer.TryLoad(json, w.Ctx.Catalog, w.Ctx.Config, out s, out e);
}
static string Mutated(Action<JObject> edit) {
    var o = JObject.Parse(SaveSerializer.Serialize(Built().City));
    edit(o);
    return o.ToString();
}
static void AssertRefused(string json) {
    Assert.That(Load(json, out var s, out var e), Is.False);
    Assert.That(s, Is.Null);
    Assert.That(e.Count, Is.GreaterThan(0));
}
[Test] public void RoundTrip_ProducesIdenticalJson() {
    var json = SaveSerializer.Serialize(Built().City);
    Assert.That(Load(json, out var s, out _), Is.True);
    Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json));
}
[Test] public void NotJson_Refused() => AssertRefused("hello");
[Test] public void Truncated_Refused() => AssertRefused(SaveSerializer.Serialize(Built().City).Substring(0, 40));
[Test] public void MissingField_Refused() => AssertRefused(Mutated(o => o.Remove("budget")));
[Test] public void WrongType_Refused() => AssertRefused(Mutated(o => o["budget"] = "abc"));
[Test] public void FutureVersion_Refused() => AssertRefused(Mutated(o => o["saveVersion"] = 2));
[Test] public void ZeroGrid_Refused() => AssertRefused(Mutated(o => o["gridWidth"] = 0));
[Test] public void GiantGrid_Refused() => AssertRefused(Mutated(o => o["gridWidth"] = 2000000000));
[Test] public void RoadOutOfBounds_Refused() => AssertRefused(Mutated(o => ((JArray)o["roads"]).Add(new JArray(999, 999))));
[Test] public void MalformedRoad_Refused() => AssertRefused(Mutated(o => ((JArray)o["roads"]).Add(new JArray(1))));
[Test] public void MissingEntry_Refused() => AssertRefused(Mutated(o => o["roads"] = new JArray { new JArray(5, 10) }));
[Test] public void DuplicateBuildingId_Refused() => AssertRefused(Mutated(o => o["buildings"][1]["id"] = o["buildings"][0]["id"]));
[Test] public void LowNextBuildingId_Refused() => AssertRefused(Mutated(o => o["nextBuildingId"] = 1));
[Test] public void UnknownDefinition_Refused() => AssertRefused(Mutated(o => o["buildings"][0]["definitionId"] = "nope"));
[Test] public void NegativeDay_Refused() => AssertRefused(Mutated(o => o["day"] = -1));
[Test] public void NegativePopulation_Refused() => AssertRefused(Mutated(o => o["population"] = -1));
[Test] public void TaxTooHigh_Refused() => AssertRefused(Mutated(o => o["taxRate"] = 31));
[Test] public void GrownBuildingWithoutZone_Refused() => AssertRefused(Mutated(o => o["zones"] = new JArray()));
[Test] public void BuildingOnRoad_Refused() => AssertRefused(Mutated(o => ((JArray)o["roads"]).Add(new JArray(1, 9))));
[Test] public void ReportsEveryProblem() {
    var json = Mutated(o => { o["day"] = -1; o["taxRate"] = 31; });
    Load(json, out _, out var e);
    Assert.That(e.Count, Is.EqualTo(2));
}
[Test] public void RefusedCommands_LeaveSaveUnchanged() {
    var w = Built();
    var before = SaveSerializer.Serialize(w.City);
    var bad = new ICommand[] {
        new BuildRoadCommand(new Cell(1, 1), new Cell(2, 2)),
        new RemoveRoadCommand(new Cell(0, 10), new Cell(1, 10)),
        new CreateZoneCommand(new[] { new Cell(1, 1) }, ZoneType.Commercial),
        new RemoveZoneCommand(new[] { new Cell(2, 9) }),
        new ConstructBuildingCommand("nope", new Cell(3, 9)),
        new RemoveBuildingCommand(99),
        new SetTaxRateCommand(99)
    };
    foreach (var c in bad) Assert.That(c.Execute(w.Ctx).Succeeded, Is.False);
    Assert.That(SaveSerializer.Serialize(w.City), Is.EqualTo(before));
}
static string TempSave() => Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

[Test] public void FirstWrite_CreatesFile_WithNoBackup() {
    var p = TempSave();
    Assert.That(SaveFile.TryWrite(p, "one", out _), Is.True);
    Assert.That(File.ReadAllText(p), Is.EqualTo("one"));
    Assert.That(File.Exists(p + ".bak"), Is.False);
}
[Test] public void SecondWrite_KeepsPreviousAsBackup() {
    var p = TempSave();
    SaveFile.TryWrite(p, "one", out _);
    SaveFile.TryWrite(p, "two", out _);
    Assert.That(File.ReadAllText(p), Is.EqualTo("two"));
    Assert.That(File.ReadAllText(p + ".bak"), Is.EqualTo("one"));
}
[Test] public void LockedFile_ReturnsError_KeepsData_NoTempLeft() {
    var p = TempSave();
    SaveFile.TryWrite(p, "one", out _);
    using (new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.None))
        Assert.That(SaveFile.TryWrite(p, "two", out _), Is.False);
    Assert.That(File.ReadAllText(p), Is.EqualTo("one"));
    Assert.That(File.Exists(p + ".tmp"), Is.False);
}
[Test] public void ReadMissingFile_ReturnsError() {
    Assert.That(SaveFile.TryRead(TempSave(), out _, out var error), Is.False);
    Assert.That(error, Is.Not.Null);
}
```

File needs `using System; using System.Collections.Generic; using System.IO; using Newtonsoft.Json.Linq;`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests --filter FullyQualifiedName~SaveTests`
Expected: build fails with `CS0103` for `SaveSerializer`.

- [ ] **Step 3: Implement `CitySaveData`, `SaveSerializer`, `SaveFile`**

Use the JSON shape in spec section 10, with `[JsonProperty(Required = Required.Always)]` on every field and `StringEnumConverter { AllowIntegerValues = false }`. A zone type of `None` is refused. Catch `JsonException` into one error.

Validate grid size against `1..MaxGridDimension` first, and stop there if it fails, because later checks allocate from it. Collect all other problems. Cell combinations must match spec section 6. Sort loaded buildings by ID and restore them with `RestoreBuilding`.

`TryWrite` writes `path + ".tmp"`, then uses `File.Replace(tmp, path, path + ".bak")` if `path` exists, otherwise `File.Move`. Catch `IOException` and `UnauthorizedAccessException`, delete the temporary file, and return the message.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests --filter FullyQualifiedName~SaveTests`
Expected: `Passed!` with no failures.

- [ ] **Step 5: Commit**

```bash
git add Assets
git commit -m "feat: add validated save and load"
```

### Task 9: Whole-city scenarios and balance tuning

**Files:**
- Test: `Assets/_Project/Tests/Simulation/ScenarioTests.cs`
- Modify (tuning only): `Assets/_Project/Scripts/Simulation/SimulationConfig.cs`, `Assets/_Project/Scripts/Simulation/BuildingCatalog.cs`
- Modify: `docs/superpowers/specs/2026-11-09-simulation-core-design.md` section 8

**Interfaces:**
- Consumes: `Simulation`, commands, `SaveSerializer`, `BuildingCatalog.Default()`, default `SimulationConfig`.

- [ ] **Step 1: Write the deterministic scenario tests in `ScenarioTests.cs`**

```csharp
static TestWorld Scenario112() {
    var catalog = new BuildingCatalog(new[] {
        new BuildingDefinition("r100", BuildingKind.Residential, 0, 0, 100, 0, 0, 0),
        new BuildingDefinition("c50", BuildingKind.Commercial, 0, 0, 50, 0, 0, 0),
        new BuildingDefinition("i40", BuildingKind.Industrial, 0, 0, 40, 0, 0, 0) });
    var w = TestWorld.Create(21, catalog).Road(0, 10, 20, 10);
    w.Place("r100", 1, 9); w.Place("c50", 2, 9); w.Place("i40", 3, 9);
    return w;
}
[Test] public void Blueprint11_2_MatchesHandComputedDays() {
    var w = Scenario112();
    var s = new Simulation(w.City, w.Ctx.Catalog, w.Ctx.Config);
    s.AdvanceDay();
    Assert.That(w.City.Population, Is.EqualTo(10));
    Assert.That(w.City.Budget, Is.EqualTo(50020L));
    s.AdvanceDay();
    Assert.That(w.City.Population, Is.EqualTo(19));
    Assert.That(w.City.Budget, Is.EqualTo(50073L));
    s.AdvanceDay();
    Assert.That(w.City.Population, Is.EqualTo(28));
    Assert.That(w.City.Budget, Is.EqualTo(50156L));
    Assert.That(w.Stats.Workers, Is.EqualTo(9));
    Assert.That(w.Stats.Jobs, Is.EqualTo(90));
    Assert.That(w.Stats.ResidentialDemand, Is.EqualTo(56));
    Assert.That(w.Stats.CommercialDemand, Is.EqualTo(0));
    Assert.That(w.Stats.IndustrialDemand, Is.EqualTo(0));
}
```

Add `static Simulation Bot(int days, Action<int, Simulation> perDay = null)`, which plays this script on a default config and `BuildingCatalog.Default()`, on a 100×100 map:
- Day 0: road from `(0, 50)` to `(99, 50)`. Residential zones on `y = 48` and `y = 49`, commercial on `y = 51`, industrial on `y = 52`, all for `x` from 20 to 99. Utilities use free cells on `y = 49` and `y = 51` for `x` from 1 to 19, in that order.
- Before each day: when `PowerRemaining < 60`, construct `power_plant_01` on the next free utility cell. When `WaterRemaining < 60`, construct `water_pump_01` likewise. Ignore refusals.

Add these tests:
- `SameScript_ProducesIdenticalSave`: two `Bot(120)` runs have equal `Serialize` output.
- `RoundTrip_ThenThirtyDays_MatchesUninterruptedRun`: run `Bot(60)`, serialize, load into a new `Simulation`, advance both 30 days, assert equal `Serialize` output.
- `Balance_100Days_And_365Days`: during `Bot(365)`, write `day,population,budget,housing,jobs,residential,commercial,industrial` per day to `Path.Combine(Path.GetTempPath(), "metropolis-balance.csv")`. Assert budget `> 0` and population above its day 50 value at day 100, and population `>= 1000` at day 365.
- `FullMap_AdvanceDayTime_IsRecorded`: fill a 100×100 map with roads every third row and residential buildings elsewhere, time 100 `AdvanceDay` calls with `Stopwatch`, and write the mean using `TestContext.WriteLine`. Assert only that the run completes.

- [ ] **Step 2: Run the scenario tests**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests --filter FullyQualifiedName~ScenarioTests`
Expected: `Blueprint11_2`, determinism and round-trip pass. `Balance_100Days_And_365Days` is expected to fail at the 1,000-resident check. If `Blueprint11_2` fails, fix the code and never the expected values.

Reason for the expected failure: with `GrowthDemandThreshold` 20, residential demand `(40 + 0.1H) / (40 + 1.1H)` falls below 20% near housing of 260.

- [ ] **Step 3: Tune defaults until the balance test passes**

If the balance test already passes, skip this step and Step 4. Otherwise, first try `GrowthDemandThreshold = 10` in `SimulationConfig`. Then read `metropolis-balance.csv`, and adjust only `SimulationConfig` defaults and `BuildingCatalog.Default()` values.

Do not change a formula, a pinned test value or the balance assertions. If tuning cannot satisfy the test, stop and report the CSV findings.

- [ ] **Step 4: Update spec section 8 with the final values**

Each changed value in section 8 is replaced with the tuned value.

- [ ] **Step 5: Run the full verification**

Run: `dotnet test dotnet/Metropolis.Simulation.Tests`
Expected: `Passed!` with 0 failed.

Run: `git grep -n "UnityEngine" -- "Assets/_Project/Scripts/Simulation"`
Expected: no output.

Run: `dotnet test dotnet/Metropolis.Simulation.Tests --filter FullyQualifiedName~FullMap --logger "console;verbosity=detailed"`
Expected: output includes the mean `AdvanceDay` time. Record the number in the handoff report.

- [ ] **Step 6: Commit**

```bash
git add Assets docs
git commit -m "test: add whole-city scenarios and tune balance"
```

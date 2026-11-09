# Project METROPOLIS: Simulation Core Design

**Date:** 2026-11-09
**Status:** Awaiting review
**Source:** Project METROPOLIS Game Development Blueprint v1.0

## 1. Intent

Project METROPOLIS is an original 3D city builder for Windows and a solo portfolio
project. The blueprint is too large for one spec, so it is split into sub-projects:

1. **Simulation core (this spec):** all city rules, with no engine dependency.
2. **Unity world and construction:** terrain, camera, grid view, input, placement.
3. **Interface and presentation:** UI, overlays, player settings, art, audio, build.

The core comes first because it can be built and tested with .NET before Unity is
installed, and because modular simulation is the main engineering claim of the
portfolio.

**Success:** a new city can be created, built, advanced day by day, saved and loaded
entirely from tests, with deterministic results and no reference to `UnityEngine`.

## 2. Scope

**In scope:** grid, roads, zones, buildings, zoned growth, population, employment,
demand, economy, electricity, water, simulation clock, player commands, JSON save and
load, automated tests.

**Out of scope:** terrain, camera, input, rendering, UI, player settings, audio, art,
ScriptableObject assets, traffic, individual citizens, building levels, medium and high
density, districts, land value, undo and redo.

## 3. Project layout

```text
Metropolis/                                git repository, later the Unity project
├── Assets/_Project/
│   ├── Scripts/Simulation/                core source
│   │   └── Metropolis.Simulation.asmdef   noEngineReferences: true
│   └── Tests/Simulation/                  NUnit tests
│       └── Metropolis.Simulation.Tests.asmdef
├── dotnet/
│   ├── Metropolis.Simulation.csproj       netstandard2.1, LangVersion 9.0
│   └── Metropolis.Simulation.Tests.csproj net9.0, NUnit
└── docs/
```

- The csproj files link the `.cs` files under `Assets`, so there is one source tree.
  Unity ignores `dotnet/` because it is outside `Assets`.
- Tests live inside `Assets` because Unity compiles nothing outside `Assets` and
  `Packages`. The same tests later run in the Unity Test Framework in EditMode.
- Language limits come from the Unity 6000.6 manual: C# 9.0 and .NET Standard 2.1.
  Init-only setters and records are banned from the core.
- JSON uses Newtonsoft.Json. `JsonUtility` is engine code and is unavailable to the core.

## 4. Architecture

| Unit | Responsibility |
|---|---|
| `CityState` | All mutable city data, as plain classes. No GameObjects. |
| `BuildingCatalog` / `BuildingDefinition` | Immutable building data, validated at startup. Unity ScriptableObjects map onto it later. |
| `SimulationConfig` | Every tunable number in section 8. |
| Commands | Validate and apply one player action. Return `CommandResult`. |
| Systems | One daily job each: `InfrastructureSystem`, `OperationSystem`, `EmploymentSystem`, `PopulationSystem`, `DemandSystem`, `GrowthSystem`, `EconomySystem`. |
| `RoadNetwork` | Finds road cells connected to the entry road and answers road-access queries. |
| `Simulation` | Owns state, executes commands, runs `AdvanceDay()`, raises events. |
| `SimulationClock` | Converts real seconds and speed into a number of days to run. |
| `SaveSerializer` / `SaveFile` | Convert state to and from JSON, validate it, write files safely. |

Dependencies run one way: systems read `CityState`, the catalog and the config, and do
not call one another. `Simulation` is the only caller of systems.

**Determinism:** money, population and formulas use integers, with `long` intermediates.
Division floors unless a formula says `ceil`. Anything iterated uses a defined order:
buildings by ascending ID, cells by ascending `y` then `x`. No randomness in the MVP.

## 5. City state

```text
CityState
  CityName, Day, Population, Budget (long), TaxRate
  Grid: Width, Height, and per cell: road flag, zone type or none, building ID or none
  Zone types: Residential | Commercial | Industrial
  Buildings: list of Building, NextBuildingId
  Statistics: last computed values, never saved

Building
  Id, DefinitionId, Cell
  Occupancy (derived daily)
  Status flags: None | NoRoad | NoPower | NoWater (derived daily)
```

A building is operational when its flags are `None`. A building on a zoned cell keeps
the zone underneath it, so a removed building can regrow.

## 6. Map rules

- The default grid is 100×100. A cell is empty, a road, a zone, a utility building, or
  a zone with one grown building of the matching type. Nothing else can share a cell.
- Every new city starts with one protected entry road at `(0, Height / 2)`.
- Roads are straight, axis-aligned drags that fill cells. The network is a graph over
  road cells with 4-way adjacency. Flood fill from the entry finds connected roads.
- A cell has **road access** when a connected road cell is within Manhattan distance
  `RoadAccessRange`.

## 7. Commands

Each command applies completely or not at all. A refused command leaves state unchanged.
Player mistakes return a `CommandResult` and never throw. Commands apply immediately,
including while paused.

| Command | Rule |
|---|---|
| `BuildRoadCommand(from, to)` | Must be axis-aligned and in bounds. Each cell must be empty or already road. Charges `RoadCostPerCell` for new cells only. Returns `NothingToDo` if every cell is already road. |
| `RemoveRoadCommand(from, to)` | Same line rules. Refused if the line includes the entry road. Free, no refund. Non-road cells on the line are ignored. Returns `NothingToDo` if the line has no road. |
| `CreateZoneCommand(cells, type)` | Each cell must be in bounds, have road access and hold no road or building. Repainting a zone is allowed. Free. |
| `RemoveZoneCommand(cells)` | Refused if any listed cell has no zone or holds a building. Free. |
| `ConstructBuildingCommand(definitionId, cell)` | Only for power and water definitions. Cell must be empty and have road access. Charges `ConstructionCost` once. |
| `RemoveBuildingCommand(buildingId)` | Removes any building. Free, no refund. |
| `SetTaxRateCommand(rate)` | `rate` must be within `0..MaxTaxRate`. |

Failure reasons: `OutOfBounds`, `NotStraight`, `CellOccupied`, `NoRoadAccess`,
`InsufficientFunds`, `ProtectedRoad`, `NothingToDo`, `UnknownDefinition`,
`NotPlayerPlaceable`, `UnknownBuilding`, `InvalidTaxRate`.

The UI is expected to send only the cells it previews as valid, because `CreateZoneCommand`
refuses the whole list when one cell is invalid.

## 8. Starting values

These are parameters, to be tuned using the balance scenario in section 12.

| Parameter | Value |
|---|---|
| `GridWidth`, `GridHeight` | 100 |
| `RoadAccessRange` | 2 |
| `StartingBudget` | 50,000 |
| `RoadCostPerCell` | 10 |
| `DefaultTaxRate`, `MaxTaxRate` | 10, 30 |
| `WorkerPercent` | 50 |
| `ResidentTaxBase`, `JobTaxBase` | 20, 30 |
| `LeaveThreshold`, `LeavePercent` | 40, 2 |
| `SecondsPerDay` at 1x | 2 |
| `GrowthDemandThreshold`, `MaxGrowthPerType` | 10, 4 |

| Definition | Type | Cost | Maintenance | Residents or jobs | Power | Water | Supply |
|---|---|---|---|---|---|---|---|
| `residential_low_01` | Residential | none | 5 | 20 residents | 10 | 8 | |
| `commercial_small_01` | Commercial | none | 8 | 10 jobs | 12 | 5 | |
| `commercial_large_01` | Commercial | none | 15 | 20 jobs | 20 | 8 | |
| `industrial_small_01` | Industrial | none | 10 | 15 jobs | 20 | 10 | |
| `industrial_large_01` | Industrial | none | 18 | 30 jobs | 35 | 18 | |
| `power_plant_01` | Power | 5,000 | 100 | | 0 | 0 | 500 power |
| `water_pump_01` | Water | 3,000 | 60 | | 0 | 0 | 400 water |

## 9. Daily tick

`Simulation.AdvanceDay()` increments `Day`, then runs these steps in order.

**1. Infrastructure.** Recompute connected roads. Supply is the total from utility
buildings with road access. Walk consumers with road access by ascending ID, separately
for power and water. Serve a consumer when remaining supply covers its demand, otherwise
skip it and continue. Newest buildings therefore lose service first.

**2. Operation.** Set status flags. Utilities need only road access.

**3. Employment.**

```text
workers  = Population * WorkerPercent / 100
jobs     = sum of job capacity of operational commercial and industrial buildings
employed = min(workers, jobs)
```

Employed workers fill job buildings by ascending ID.

**4. Population.**

```text
housing = sum of residential capacity of operational buildings
Population = min(Population, housing)
vacant = housing - Population
unemployment% = workers == 0 ? 0 : 100 * (workers - employed) / workers
nonRunning%   = buildings == 0 ? 0 : 100 * nonOperational / buildings
attractiveness = clamp(100 - 5 * max(0, TaxRate - 10)
                           - unemployment% / 2 - nonRunning%, 0, 100)
arrivals   = ceil(vacant * attractiveness / 1000)
departures = attractiveness < LeaveThreshold ? Population * LeavePercent / 100 : 0
Population = Population + arrivals - departures
```

Residents then fill housing by ascending ID. Attractiveness fills the blueprint's
`CityHappiness`.

**5. Demand.** Capacity counts all existing buildings, operational or not, so a power
cut does not trigger a construction wave.

```text
demand(target, capacity) = clamp(100 * (target - capacity) / max(target, 1), 0, 100)

residential target = 40 + Population * 110 / 100 + 2 * max(0, jobs - workers)
commercial target  = Population * 20 / 100   (compared with commercial jobs)
industrial target  = Population * 30 / 100   (compared with industrial jobs)
```

**6. Growth.** Types run in the order residential, commercial, industrial. When demand
is at least `GrowthDemandThreshold`, grow up to `clamp(demand / 25, 1, MaxGrowthPerType)`
buildings. Eligible cells are zoned, empty and have road access, in ascending cell order.
Each building needs spare power and water after the day's allocation and reserves that
amount when placed. The definition is `variants[(x * 31 + y) % variants.Count]`, with
variants in catalog order. Zoned growth does not charge the city budget. New buildings
start with no occupancy and are evaluated the next day.

**7. Economy.**

```text
revenue  = (Population * ResidentTaxBase + employed * JobTaxBase) * TaxRate / 100
expenses = sum of maintenance of all buildings
Budget   = Budget + revenue - expenses
```

Daily expenses may push the budget negative. The MVP has no game over. Construction is
never charged here.

**8. Publish.** Update `Statistics`, then raise events for that day.

### Events

`DayAdvanced`, `PopulationChanged`, `BudgetChanged`, `BuildingAdded`, `BuildingRemoved`,
`BuildingStatusChanged`, `RoadsChanged`, `ZonesChanged`.

Commands raise their change events immediately. Daily events are raised only in step 8,
so listeners never see a half-finished day.

### Clock

`SimulationClock` supports paused, 1x, 2x and 4x. `Advance(realSeconds)` accumulates
time and returns how many whole days to run. Speed is presentation state and is not
saved. The caller invokes `AdvanceDay()` that many times.

## 10. Save and load

`CitySaveData` is separate from `CityState`.

```json
{
  "saveVersion": 1,
  "cityName": "New City",
  "day": 0,
  "population": 0,
  "budget": 50000,
  "taxRate": 10,
  "gridWidth": 100,
  "gridHeight": 100,
  "nextBuildingId": 1,
  "roads": [[0, 50]],
  "zones": [{ "x": 1, "y": 50, "type": "Residential" }],
  "buildings": [{ "id": 1, "definitionId": "power_plant_01", "x": 2, "y": 50 }]
}
```

- Only source data is saved, with lists in the deterministic order from section 4.
  Infrastructure, occupancy, flags, road connectivity and statistics are recalculated.
- After a load, steps 1 to 3 and 5 run once and residents fill housing, without
  advancing the day or changing population or budget, so the UI has current values.
- **Validation** runs before any state is built. It rejects an unsupported
  `saveVersion`, missing or mistyped fields, non-positive grid dimensions, cells out of
  bounds, a cell combination not allowed by section 6, a missing entry road, duplicate
  building IDs, `nextBuildingId` not above every ID, unknown definitions, negative `day`
  or `population`, and a `taxRate` outside `0..MaxTaxRate`.
- `TryLoad(json, catalog, out state, out errors)` returns every problem found. A failed
  load never touches the running city.
- `SaveFile.Write` writes a temporary file in the same folder, then replaces the save
  and keeps the previous version as `.bak`. A failed read or write returns an error.
  Restoring `.bak` is a player decision for the UI, not an automatic action.
- Versions above the supported one are rejected. Migration code is written when
  version 2 exists.

## 11. Changes from the blueprint

| Blueprint | This design | Reason |
|---|---|---|
| Root `Tests/` folder | Tests inside `Assets` | Unity does not compile the root folder. |
| Buildings copy cost, capacity and demand | Buildings store a definition ID | Copies go stale after rebalancing. |
| Commands applied at tick step 1 | Applied immediately | Allows building while paused, with instant feedback. |
| `CitizenGroup` income and education | One population count | No MVP rule reads them. |
| Road segments with start and end nodes | Road cells with implicit adjacency | Avoids segment splitting. Lane-level traffic would need a real node graph. |
| `ConstructionCost` on all buildings | Charged only for placed buildings | Automatic growth would drain the budget with no player action. |
| No growth step in section 7.2 | Growth is step 6 | Section 7.6 requires growth but the order omits it. |
| Example maintenance of 50 | 5 for low-density housing | A full 20-person house earns far less than 50 per day at 10% tax. |
| `Infrastructure` in saves | Recalculated on load | It derives entirely from saved buildings. |
| `CityId`, `Rotation`, `Level` | Not stored | One city per save. Facing is presentation. Levels are out of scope. |

## 12. Testing

Tests use NUnit, run with `dotnet test`, and later run unchanged in Unity EditMode.

- **Systems:** each formula, including boundaries such as zero workers, zero buildings,
  exact supply and clamping.
- **Commands:** each failure reason, cost charged exactly once, refused commands leave
  saved JSON unchanged, charges apply only to new road cells.
- **Roads:** connection, disconnection after removal, access range, entry protection.
- **Determinism:** the same command script run twice produces identical save JSON.
- **Round trip:** save at day N and load. Advancing the original and the loaded city by
  30 days produces identical save JSON.
- **Bad saves:** one case per validation rule, plus truncated and non-JSON input.
- **Blueprint scenario 11.2:** 100 residential capacity, 50 commercial jobs and 40
  industrial jobs, with hand-computed expected values.
- **Balance:** a scripted city must have a positive budget and a rising population at
  day 100, and reach 1,000 residents by day 365. The run exports a CSV for tuning.
- **Performance:** `AdvanceDay()` time on a full 100×100 map is measured and recorded.
  No threshold is asserted.

## 13. Definition of done

- `dotnet test` passes every test.
- The core contains no reference to `UnityEngine`.
- MVP acceptance criteria 1 to 10 pass without graphics.
- Criterion 11, a continuous playtest, is covered at simulation level by the 365-day
  run. Full playtesting needs sub-projects 2 and 3.

**Not verifiable in this sub-project:** compiling the core inside Unity, which needs
Unity installed. Sub-project 2 starts by confirming it.

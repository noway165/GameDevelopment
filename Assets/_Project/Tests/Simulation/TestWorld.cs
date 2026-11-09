namespace Metropolis.Core.Tests
{
    /// <summary>Spec values pinned for tests, so that tuning defaults cannot break them.</summary>
    public static class TestConfig
    {
        public static SimulationConfig Pinned(int width, int height) => new SimulationConfig
        {
            GridWidth = width,
            GridHeight = height,
            MaxGridDimension = 1024,
            RoadAccessRange = 2,
            StartingBudget = 50000L,
            RoadCostPerCell = 10,
            DefaultTaxRate = 10,
            MaxTaxRate = 30,
            NeutralTaxRate = 10,
            TaxPenaltyPerPoint = 5,
            WorkerPercent = 50,
            ResidentTaxBase = 20,
            JobTaxBase = 30,
            LeaveThreshold = 40,
            LeavePercent = 2,
            SecondsPerDay = 2,
            MaxDaysPerAdvance = 8,
            GrowthDemandThreshold = 20,
            GrowthDemandDivisor = 25,
            MaxGrowthPerType = 4,
            ResidentialDemandBase = 40,
            ResidentialDemandPercent = 110,
            FreeJobDemandWeight = 2,
            CommercialJobPercent = 20,
            IndustrialJobPercent = 30,
        };
    }

    public static class TestCatalog
    {
        public static BuildingCatalog Spec() => new BuildingCatalog(new[]
        {
            new BuildingDefinition("residential_low_01", BuildingKind.Residential, 0, 5, 20, 10, 8, 0),
            new BuildingDefinition("res_tiny", BuildingKind.Residential, 0, 0, 5, 5, 0, 0),
            new BuildingDefinition("commercial_small_01", BuildingKind.Commercial, 0, 8, 10, 12, 5, 0),
            new BuildingDefinition("commercial_large_01", BuildingKind.Commercial, 0, 15, 20, 20, 8, 0),
            new BuildingDefinition("industrial_small_01", BuildingKind.Industrial, 0, 10, 15, 20, 10, 0),
            new BuildingDefinition("industrial_large_01", BuildingKind.Industrial, 0, 18, 30, 35, 18, 0),
            new BuildingDefinition("power_plant_01", BuildingKind.Power, 5000, 100, 0, 0, 0, 500),
            new BuildingDefinition("power_small", BuildingKind.Power, 0, 0, 0, 0, 0, 25),
            new BuildingDefinition("water_pump_01", BuildingKind.Water, 3000, 60, 0, 0, 0, 400),
            new BuildingDefinition("water_small", BuildingKind.Water, 0, 0, 0, 0, 0, 16),
        });
    }

    public sealed class TestWorld
    {
        TestWorld(SimContext ctx) { Ctx = ctx; }

        public SimContext Ctx { get; }
        public CityState City => Ctx.City;
        public Statistics Stats => Ctx.City.Statistics;
        public DayWork Work { get; } = new DayWork();

        public static TestWorld Create(int size = 21, BuildingCatalog catalog = null)
        {
            var config = TestConfig.Pinned(size, size);
            var city = CityState.CreateNew("Test", config);
            return new TestWorld(new SimContext(city, catalog ?? TestCatalog.Spec(), config));
        }

        public TestWorld Road(int x1, int y1, int x2, int y2)
        {
            for (int y = System.Math.Min(y1, y2); y <= System.Math.Max(y1, y2); y++)
                for (int x = System.Math.Min(x1, x2); x <= System.Math.Max(x1, x2); x++)
                    City.SetRoad(new Cell(x, y), true);
            Ctx.Roads.Rebuild();
            return this;
        }

        public Building Place(string definitionId, int x, int y)
        {
            var cell = new Cell(x, y);
            switch (Ctx.Catalog.Get(definitionId).Kind)
            {
                case BuildingKind.Residential: City.SetZone(cell, ZoneType.Residential); break;
                case BuildingKind.Commercial: City.SetZone(cell, ZoneType.Commercial); break;
                case BuildingKind.Industrial: City.SetZone(cell, ZoneType.Industrial); break;
            }
            return City.AddBuilding(definitionId, cell);
        }

        public void Zone(ZoneType type, int x, int y) => City.SetZone(new Cell(x, y), type);
    }
}

namespace Metropolis.Core
{
    public static class PopulationSystem
    {
        public static void Run(SimContext ctx)
        {
            var city = ctx.City;
            var stats = city.Statistics;

            int housing = 0;
            int nonOperational = 0;
            foreach (var building in city.Buildings)
            {
                if (!building.IsOperational)
                {
                    nonOperational++;
                    continue;
                }
                var definition = ctx.Catalog.Get(building.DefinitionId);
                if (definition.Kind == BuildingKind.Residential)
                    housing += definition.Capacity;
            }

            city.Population = System.Math.Min(city.Population, housing);
            int vacant = housing - city.Population;

            int unemployment = stats.Workers == 0
                ? 0
                : (int)(100L * (stats.Workers - stats.Employed) / stats.Workers);
            int nonRunning = city.Buildings.Count == 0
                ? 0
                : (int)(100L * nonOperational / city.Buildings.Count);
            int attractiveness = Attractiveness(city.TaxRate, unemployment, nonRunning, ctx.Config);
            int arrivals = Arrivals(vacant, attractiveness);
            int departures = Departures(city.Population, attractiveness, ctx.Config);

            city.Population += arrivals - departures;

            stats.Housing = housing;
            stats.Arrivals = arrivals;
            stats.Departures = departures;
            stats.UnemploymentPercent = unemployment;
            stats.NonRunningPercent = nonRunning;
            stats.Attractiveness = attractiveness;

            FillHousing(ctx);
        }

        public static void FillHousing(SimContext ctx)
        {
            int unhoused = ctx.City.Population;
            foreach (var building in ctx.City.Buildings)
            {
                var definition = ctx.Catalog.Get(building.DefinitionId);
                if (definition.Kind != BuildingKind.Residential)
                    continue;

                building.Occupancy = building.IsOperational
                    ? System.Math.Min(definition.Capacity, unhoused)
                    : 0;
                unhoused -= building.Occupancy;
            }
        }

        public static int Attractiveness(int taxRate, int unemploymentPercent, int nonRunningPercent, SimulationConfig config)
        {
            int taxPenalty = config.TaxPenaltyPerPoint * System.Math.Max(0, taxRate - config.NeutralTaxRate);
            int score = 100 - taxPenalty - unemploymentPercent / 2 - nonRunningPercent;
            return System.Math.Max(0, System.Math.Min(100, score));
        }

        public static int Arrivals(int vacant, int attractiveness) =>
            (int)(((long)vacant * attractiveness + 999) / 1000);

        public static int Departures(int population, int attractiveness, SimulationConfig config) =>
            attractiveness < config.LeaveThreshold
                ? (int)((long)population * config.LeavePercent / 100)
                : 0;
    }
}

namespace Metropolis.Core
{
    public static class EmploymentSystem
    {
        public static void Run(SimContext ctx)
        {
            var city = ctx.City;
            int workers = (int)((long)city.Population * ctx.Config.WorkerPercent / 100);
            int unassigned = workers;
            int jobs = 0;

            foreach (var building in city.Buildings)
            {
                var definition = ctx.Catalog.Get(building.DefinitionId);
                if (definition.Kind != BuildingKind.Commercial && definition.Kind != BuildingKind.Industrial)
                    continue;

                if (!building.IsOperational)
                {
                    building.Occupancy = 0;
                    continue;
                }

                jobs += definition.Capacity;
                building.Occupancy = System.Math.Min(definition.Capacity, unassigned);
                unassigned -= building.Occupancy;
            }

            var stats = city.Statistics;
            stats.Workers = workers;
            stats.Jobs = jobs;
            stats.Employed = workers - unassigned;
        }
    }
}

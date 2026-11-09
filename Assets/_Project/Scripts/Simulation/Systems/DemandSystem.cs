namespace Metropolis.Core
{
    public static class DemandSystem
    {
        public static void Run(SimContext ctx)
        {
            var city = ctx.City;
            var stats = city.Statistics;
            var config = ctx.Config;

            // All buildings count, running or not, so a power cut does not trigger a construction wave.
            long housing = 0, commercial = 0, industrial = 0;
            foreach (var building in city.Buildings)
            {
                var definition = ctx.Catalog.Get(building.DefinitionId);
                switch (definition.Kind)
                {
                    case BuildingKind.Residential: housing += definition.Capacity; break;
                    case BuildingKind.Commercial: commercial += definition.Capacity; break;
                    case BuildingKind.Industrial: industrial += definition.Capacity; break;
                }
            }

            long population = city.Population;
            long residentialTarget = config.ResidentialDemandBase
                + population * config.ResidentialDemandPercent / 100
                + config.FreeJobDemandWeight * System.Math.Max(0, stats.Jobs - stats.Workers);

            stats.ResidentialDemand = Demand(residentialTarget, housing);
            stats.CommercialDemand = Demand(population * config.CommercialJobPercent / 100, commercial);
            stats.IndustrialDemand = Demand(population * config.IndustrialJobPercent / 100, industrial);
        }

        public static int Demand(long target, long capacity)
        {
            long demand = 100 * (target - capacity) / System.Math.Max(target, 1);
            return (int)System.Math.Max(0, System.Math.Min(100, demand));
        }
    }
}

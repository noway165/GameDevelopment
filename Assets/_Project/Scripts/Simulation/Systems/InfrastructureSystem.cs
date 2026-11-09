namespace Metropolis.Core
{
    public static class InfrastructureSystem
    {
        public static void Run(SimContext ctx, DayWork work)
        {
            ctx.Roads.Rebuild();
            work.PowerServed.Clear();
            work.WaterServed.Clear();

            int power = 0;
            int water = 0;
            foreach (var building in ctx.City.Buildings)
            {
                if (!ctx.Roads.HasRoadAccess(building.Cell))
                    continue;
                var definition = ctx.Catalog.Get(building.DefinitionId);
                if (definition.Kind == BuildingKind.Power) power += definition.Supply;
                else if (definition.Kind == BuildingKind.Water) water += definition.Supply;
            }

            var stats = ctx.City.Statistics;
            stats.PowerSupply = power;
            stats.WaterSupply = water;

            foreach (var building in ctx.City.Buildings)
            {
                if (!ctx.Roads.HasRoadAccess(building.Cell))
                    continue;
                var definition = ctx.Catalog.Get(building.DefinitionId);
                if (definition.PowerDemand <= power)
                {
                    power -= definition.PowerDemand;
                    work.PowerServed.Add(building.Id);
                }
                if (definition.WaterDemand <= water)
                {
                    water -= definition.WaterDemand;
                    work.WaterServed.Add(building.Id);
                }
            }

            stats.PowerRemaining = power;
            stats.WaterRemaining = water;
        }
    }
}

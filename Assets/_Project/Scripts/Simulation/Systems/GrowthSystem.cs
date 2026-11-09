namespace Metropolis.Core
{
    public static class GrowthSystem
    {
        public static void Run(SimContext ctx, DayWork work)
        {
            var stats = ctx.City.Statistics;
            Grow(ctx, work, ZoneType.Residential, BuildingKind.Residential, stats.ResidentialDemand);
            Grow(ctx, work, ZoneType.Commercial, BuildingKind.Commercial, stats.CommercialDemand);
            Grow(ctx, work, ZoneType.Industrial, BuildingKind.Industrial, stats.IndustrialDemand);
        }

        static void Grow(SimContext ctx, DayWork work, ZoneType zone, BuildingKind kind, int demand)
        {
            var config = ctx.Config;
            var variants = ctx.Catalog.Variants(kind);
            if (demand < config.GrowthDemandThreshold || variants.Count == 0)
                return;

            var city = ctx.City;
            var stats = city.Statistics;
            int remaining = System.Math.Max(1, System.Math.Min(config.MaxGrowthPerType, demand / config.GrowthDemandDivisor));

            for (int y = 0; y < city.Height; y++)
            {
                for (int x = 0; x < city.Width; x++)
                {
                    var cell = new Cell(x, y);
                    if (city.GetZone(cell) != zone || city.GetBuilding(cell) != null || !ctx.Roads.HasRoadAccess(cell))
                        continue;

                    var definition = variants[(x * 31 + y) % variants.Count];
                    if (definition.PowerDemand > stats.PowerRemaining || definition.WaterDemand > stats.WaterRemaining)
                        return;

                    stats.PowerRemaining -= definition.PowerDemand;
                    stats.WaterRemaining -= definition.WaterDemand;
                    work.Added.Add(city.AddBuilding(definition.Id, cell));

                    if (--remaining == 0)
                        return;
                }
            }
        }
    }
}

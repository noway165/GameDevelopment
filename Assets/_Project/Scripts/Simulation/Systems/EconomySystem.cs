namespace Metropolis.Core
{
    public static class EconomySystem
    {
        public static void Run(SimContext ctx)
        {
            var city = ctx.City;
            var stats = city.Statistics;
            var config = ctx.Config;

            long revenue = ((long)city.Population * config.ResidentTaxBase + (long)stats.Employed * config.JobTaxBase)
                * city.TaxRate / 100;

            long expenses = 0;
            foreach (var building in city.Buildings)
                expenses += ctx.Catalog.Get(building.DefinitionId).Maintenance;

            stats.Revenue = revenue;
            stats.Expenses = expenses;
            city.Budget += revenue - expenses;
        }
    }
}

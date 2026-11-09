namespace Metropolis.Core
{
    /// <summary>What commands and systems need to do their work.</summary>
    public sealed class SimContext
    {
        public SimContext(CityState city, BuildingCatalog catalog, SimulationConfig config)
        {
            City = city;
            Catalog = catalog;
            Config = config;
            Roads = new RoadNetwork(city, config.RoadAccessRange);
            Events = new SimulationEvents();
            Roads.Rebuild();
        }

        public CityState City { get; }
        public BuildingCatalog Catalog { get; }
        public SimulationConfig Config { get; }
        public RoadNetwork Roads { get; }
        public SimulationEvents Events { get; }
    }
}

namespace Metropolis.Core
{
    public enum BuildingKind { Residential, Commercial, Industrial, Power, Water }

    public sealed class BuildingDefinition
    {
        public BuildingDefinition(string id, BuildingKind kind, int constructionCost, int maintenance,
            int capacity, int powerDemand, int waterDemand, int supply)
        {
            Id = id;
            Kind = kind;
            ConstructionCost = constructionCost;
            Maintenance = maintenance;
            Capacity = capacity;
            PowerDemand = powerDemand;
            WaterDemand = waterDemand;
            Supply = supply;
        }

        public string Id { get; }
        public BuildingKind Kind { get; }
        public int ConstructionCost { get; }
        public int Maintenance { get; }

        /// <summary>Residents for residential buildings, jobs for commercial and industrial.</summary>
        public int Capacity { get; }
        public int PowerDemand { get; }
        public int WaterDemand { get; }
        public int Supply { get; }
    }
}

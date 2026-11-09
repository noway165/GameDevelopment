namespace Metropolis.Core
{
    /// <summary>Last computed daily values. Recalculated, never saved.</summary>
    public sealed class Statistics
    {
        public int PowerSupply;
        public int PowerRemaining;
        public int WaterSupply;
        public int WaterRemaining;
        public int Workers;
        public int Jobs;
        public int Employed;
        public int Housing;
        public int Arrivals;
        public int Departures;
        public int UnemploymentPercent;
        public int NonRunningPercent;
        public int Attractiveness;
        public int ResidentialDemand;
        public int CommercialDemand;
        public int IndustrialDemand;
        public long Revenue;
        public long Expenses;
    }
}

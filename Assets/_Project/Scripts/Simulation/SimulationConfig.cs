namespace Metropolis.Core
{
    /// <summary>Every tunable number in the simulation.</summary>
    public sealed class SimulationConfig
    {
        public int GridWidth { get; set; } = 100;
        public int GridHeight { get; set; } = 100;
        public int MaxGridDimension { get; set; } = 1024;
        public int RoadAccessRange { get; set; } = 2;
        public long StartingBudget { get; set; } = 50000L;
        public int RoadCostPerCell { get; set; } = 10;
        public int DefaultTaxRate { get; set; } = 10;
        public int MaxTaxRate { get; set; } = 30;
        public int NeutralTaxRate { get; set; } = 10;
        public int TaxPenaltyPerPoint { get; set; } = 5;
        public int WorkerPercent { get; set; } = 50;
        public int ResidentTaxBase { get; set; } = 20;
        public int JobTaxBase { get; set; } = 30;
        public int LeaveThreshold { get; set; } = 40;
        public int LeavePercent { get; set; } = 2;
        public int SecondsPerDay { get; set; } = 2;
        public int MaxDaysPerAdvance { get; set; } = 8;
        public int GrowthDemandThreshold { get; set; } = 20;
        public int GrowthDemandDivisor { get; set; } = 25;
        public int MaxGrowthPerType { get; set; } = 4;
        public int ResidentialDemandBase { get; set; } = 40;
        public int ResidentialDemandPercent { get; set; } = 110;
        public int FreeJobDemandWeight { get; set; } = 2;
        public int CommercialJobPercent { get; set; } = 20;
        public int IndustrialJobPercent { get; set; } = 30;
    }
}

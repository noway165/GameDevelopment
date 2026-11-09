namespace Metropolis.Core
{
    /// <summary>Owns a running city: executes commands and advances days in a fixed order.</summary>
    public sealed class Simulation
    {
        public Simulation(CityState city, BuildingCatalog catalog, SimulationConfig config)
        {
            Context = new SimContext(city, catalog, config);
            RefreshDerived();
        }

        public SimContext Context { get; }
        public CityState City => Context.City;
        public SimulationEvents Events => Context.Events;

        public CommandResult Execute(ICommand command) => command.Execute(Context);
        public CommandResult Validate(ICommand command) => command.Validate(Context);

        public void AdvanceDay()
        {
            var work = new DayWork();
            int populationBefore = City.Population;
            long budgetBefore = City.Budget;

            City.Day++;
            InfrastructureSystem.Run(Context, work);
            OperationSystem.Run(Context, work);
            EmploymentSystem.Run(Context);
            PopulationSystem.Run(Context);
            DemandSystem.Run(Context);
            GrowthSystem.Run(Context, work);
            EconomySystem.Run(Context);

            foreach (var building in work.StatusChanged)
                Events.RaiseBuildingStatusChanged(building);
            foreach (var building in work.Added)
                Events.RaiseBuildingAdded(building);
            if (City.Population != populationBefore)
                Events.RaisePopulationChanged(City.Population);
            if (City.Budget != budgetBefore)
                Events.RaiseBudgetChanged(City.Budget);
            Events.RaiseDayAdvanced(City.Day);
        }

        /// <summary>Recalculates derived values only. Used on creation and after a load.</summary>
        public void RefreshDerived()
        {
            var work = new DayWork();
            InfrastructureSystem.Run(Context, work);
            OperationSystem.Run(Context, work);
            EmploymentSystem.Run(Context);
            PopulationSystem.FillHousing(Context);
            DemandSystem.Run(Context);
        }
    }
}

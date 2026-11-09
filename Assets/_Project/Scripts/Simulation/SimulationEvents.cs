using System;

namespace Metropolis.Core
{
    public sealed class SimulationEvents
    {
        public event Action<int> DayAdvanced;
        public event Action<int> PopulationChanged;
        public event Action<long> BudgetChanged;
        public event Action<Building> BuildingAdded;
        public event Action<Building> BuildingRemoved;
        public event Action<Building> BuildingStatusChanged;
        public event Action RoadsChanged;
        public event Action ZonesChanged;

        internal void RaiseDayAdvanced(int day) => DayAdvanced?.Invoke(day);
        internal void RaisePopulationChanged(int population) => PopulationChanged?.Invoke(population);
        internal void RaiseBudgetChanged(long budget) => BudgetChanged?.Invoke(budget);
        internal void RaiseBuildingAdded(Building building) => BuildingAdded?.Invoke(building);
        internal void RaiseBuildingRemoved(Building building) => BuildingRemoved?.Invoke(building);
        internal void RaiseBuildingStatusChanged(Building building) => BuildingStatusChanged?.Invoke(building);
        internal void RaiseRoadsChanged() => RoadsChanged?.Invoke();
        internal void RaiseZonesChanged() => ZonesChanged?.Invoke();
    }
}

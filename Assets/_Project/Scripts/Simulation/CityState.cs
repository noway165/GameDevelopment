using System.Collections.Generic;

namespace Metropolis.Core
{
    public enum ZoneType { None, Residential, Commercial, Industrial }

    /// <summary>All mutable city data. Holds no rules.</summary>
    public sealed class CityState
    {
        readonly bool[] _roads;
        readonly ZoneType[] _zones;
        readonly Building[] _buildingAt;
        readonly Dictionary<int, Building> _buildingsById = new Dictionary<int, Building>();

        public CityState(string cityName, int width, int height)
        {
            CityName = cityName;
            Width = width;
            Height = height;
            NextBuildingId = 1;
            _roads = new bool[width * height];
            _zones = new ZoneType[width * height];
            _buildingAt = new Building[width * height];
        }

        public static CityState CreateNew(string cityName, SimulationConfig config)
        {
            var city = new CityState(cityName, config.GridWidth, config.GridHeight)
            {
                Budget = config.StartingBudget,
                TaxRate = config.DefaultTaxRate,
            };
            city.SetRoad(city.EntryRoad, true);
            return city;
        }

        public string CityName { get; }
        public int Width { get; }
        public int Height { get; }
        public int Day { get; set; }
        public int Population { get; set; }
        public int TaxRate { get; set; }
        public int NextBuildingId { get; set; }
        public long Budget { get; set; }
        public Cell EntryRoad => new Cell(0, Height / 2);

        /// <summary>Ordered by ascending ID.</summary>
        public List<Building> Buildings { get; } = new List<Building>();

        public Statistics Statistics { get; } = new Statistics();

        public bool IsInBounds(Cell cell) =>
            cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;

        public bool HasRoad(Cell cell) => _roads[Index(cell)];
        public void SetRoad(Cell cell, bool value) => _roads[Index(cell)] = value;
        public ZoneType GetZone(Cell cell) => _zones[Index(cell)];
        public void SetZone(Cell cell, ZoneType type) => _zones[Index(cell)] = type;
        public Building GetBuilding(Cell cell) => _buildingAt[Index(cell)];

        public Building FindBuilding(int id) =>
            _buildingsById.TryGetValue(id, out var building) ? building : null;

        public Building AddBuilding(string definitionId, Cell cell)
        {
            var building = new Building(NextBuildingId++, definitionId, cell);
            Register(building);
            return building;
        }

        /// <summary>Re-creates a saved building. Callers restore in ascending ID order.</summary>
        public void RestoreBuilding(int id, string definitionId, Cell cell) =>
            Register(new Building(id, definitionId, cell));

        public void RemoveBuilding(Building building)
        {
            Buildings.Remove(building);
            _buildingsById.Remove(building.Id);
            _buildingAt[Index(building.Cell)] = null;
        }

        void Register(Building building)
        {
            Buildings.Add(building);
            _buildingsById[building.Id] = building;
            _buildingAt[Index(building.Cell)] = building;
        }

        int Index(Cell cell) => cell.Y * Width + cell.X;
    }
}

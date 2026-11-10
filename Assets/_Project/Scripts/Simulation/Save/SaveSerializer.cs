using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Metropolis.Core
{
    public static class SaveSerializer
    {
        const int SupportedVersion = 1;

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Converters = { new StringEnumConverter { AllowIntegerValues = false } },
        };

        public static string Serialize(CityState city)
        {
            var data = new CitySaveData
            {
                SaveVersion = SupportedVersion,
                CityName = city.CityName,
                Day = city.Day,
                Population = city.Population,
                Budget = city.Budget,
                TaxRate = city.TaxRate,
                GridWidth = city.Width,
                GridHeight = city.Height,
                NextBuildingId = city.NextBuildingId,
                Roads = new List<int[]>(),
                Zones = new List<ZoneSaveData>(),
                Buildings = city.Buildings
                    .Select(b => new BuildingSaveData { Id = b.Id, DefinitionId = b.DefinitionId, X = b.Cell.X, Y = b.Cell.Y })
                    .ToList(),
            };

            for (int y = 0; y < city.Height; y++)
            {
                for (int x = 0; x < city.Width; x++)
                {
                    var cell = new Cell(x, y);
                    if (city.HasRoad(cell))
                        data.Roads.Add(new[] { x, y });
                    if (city.GetZone(cell) != ZoneType.None)
                        data.Zones.Add(new ZoneSaveData { X = x, Y = y, Type = city.GetZone(cell) });
                }
            }

            return JsonConvert.SerializeObject(data, Settings);
        }

        /// <summary>True when the text is well-formed save data, whether or not its contents validate.</summary>
        public static bool IsParseable(string json)
        {
            try
            {
                return json != null && JsonConvert.DeserializeObject<CitySaveData>(json, Settings) != null;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static bool TryLoad(string json, BuildingCatalog catalog, SimulationConfig config,
            out CityState state, out IReadOnlyList<string> errors)
        {
            state = null;
            var problems = new List<string>();
            errors = problems;

            CitySaveData data;
            try
            {
                data = json == null ? null : JsonConvert.DeserializeObject<CitySaveData>(json, Settings);
            }
            catch (JsonException e)
            {
                problems.Add("Save data is not valid: " + e.Message);
                return false;
            }

            if (data == null)
            {
                problems.Add("Save data is empty.");
                return false;
            }

            // Checked first and alone, because every later check allocates from these values.
            if (data.GridWidth < 1 || data.GridWidth > config.MaxGridDimension
                || data.GridHeight < 1 || data.GridHeight > config.MaxGridDimension)
            {
                problems.Add("Grid size is not valid: " + data.GridWidth + " by " + data.GridHeight + ".");
                return false;
            }

            Validate(data, catalog, config, problems);
            if (problems.Count > 0)
                return false;

            state = Build(data);
            return true;
        }

        static void Validate(CitySaveData data, BuildingCatalog catalog, SimulationConfig config, List<string> problems)
        {
            if (data.SaveVersion != SupportedVersion)
                problems.Add("Unsupported save version: " + data.SaveVersion + ".");
            if (data.Day < 0)
                problems.Add("Day is negative.");
            if (data.Population < 0)
                problems.Add("Population is negative.");
            if (data.TaxRate < 0 || data.TaxRate > config.MaxTaxRate)
                problems.Add("Tax rate is out of range: " + data.TaxRate + ".");

            int width = data.GridWidth;
            int height = data.GridHeight;
            bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;
            int Index(int x, int y) => y * width + x;

            var roads = new bool[width * height];
            var zones = new ZoneType[width * height];
            var occupied = new bool[width * height];

            foreach (var road in data.Roads)
            {
                if (road == null || road.Length != 2)
                    problems.Add("A road entry is malformed.");
                else if (!InBounds(road[0], road[1]))
                    problems.Add("A road is out of bounds.");
                else if (roads[Index(road[0], road[1])])
                    problems.Add("A road cell is duplicated.");
                else
                    roads[Index(road[0], road[1])] = true;
            }

            if (!roads[Index(0, height / 2)])
                problems.Add("The entry road is missing.");

            foreach (var zone in data.Zones)
            {
                if (zone == null)
                    problems.Add("A zone entry is malformed.");
                else if (zone.Type == ZoneType.None)
                    problems.Add("A zone has no type.");
                else if (!InBounds(zone.X, zone.Y))
                    problems.Add("A zone is out of bounds.");
                else if (roads[Index(zone.X, zone.Y)])
                    problems.Add("A zone overlaps a road.");
                else if (zones[Index(zone.X, zone.Y)] != ZoneType.None)
                    problems.Add("A zone cell is duplicated.");
                else
                    zones[Index(zone.X, zone.Y)] = zone.Type;
            }

            var ids = new HashSet<int>();
            foreach (var building in data.Buildings)
            {
                if (building == null)
                {
                    problems.Add("A building entry is malformed.");
                    continue;
                }

                if (building.Id < 1)
                    problems.Add("A building has an invalid ID.");
                if (!ids.Add(building.Id))
                    problems.Add("A building ID is duplicated: " + building.Id + ".");
                if (building.Id >= data.NextBuildingId)
                    problems.Add("nextBuildingId must be above every building ID.");

                if (!InBounds(building.X, building.Y))
                {
                    problems.Add("A building is out of bounds.");
                    continue;
                }

                int index = Index(building.X, building.Y);
                if (roads[index])
                    problems.Add("A building overlaps a road.");
                if (occupied[index])
                    problems.Add("Two buildings share a cell.");
                occupied[index] = true;

                if (!catalog.TryGet(building.DefinitionId, out var definition))
                    problems.Add("Unknown building definition: " + building.DefinitionId + ".");
                else if (zones[index] != RequiredZone(definition.Kind))
                    problems.Add("A building does not match the zone on its cell.");
            }

            if (data.NextBuildingId < 1)
                problems.Add("nextBuildingId must be positive.");
        }

        static ZoneType RequiredZone(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.Residential: return ZoneType.Residential;
                case BuildingKind.Commercial: return ZoneType.Commercial;
                case BuildingKind.Industrial: return ZoneType.Industrial;
                default: return ZoneType.None;
            }
        }

        static CityState Build(CitySaveData data)
        {
            var city = new CityState(data.CityName, data.GridWidth, data.GridHeight)
            {
                Day = data.Day,
                Population = data.Population,
                Budget = data.Budget,
                TaxRate = data.TaxRate,
                NextBuildingId = data.NextBuildingId,
            };

            foreach (var road in data.Roads)
                city.SetRoad(new Cell(road[0], road[1]), true);
            foreach (var zone in data.Zones)
                city.SetZone(new Cell(zone.X, zone.Y), zone.Type);
            foreach (var building in data.Buildings.OrderBy(b => b.Id))
                city.RestoreBuilding(building.Id, building.DefinitionId, new Cell(building.X, building.Y));

            return city;
        }
    }
}

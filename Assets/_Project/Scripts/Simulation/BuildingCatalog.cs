using System;
using System.Collections.Generic;

namespace Metropolis.Core
{
    public sealed class BuildingCatalog
    {
        readonly Dictionary<string, BuildingDefinition> _byId = new Dictionary<string, BuildingDefinition>();
        readonly Dictionary<BuildingKind, List<BuildingDefinition>> _byKind = new Dictionary<BuildingKind, List<BuildingDefinition>>();

        public BuildingCatalog(IEnumerable<BuildingDefinition> definitions)
        {
            foreach (BuildingKind kind in Enum.GetValues(typeof(BuildingKind)))
                _byKind[kind] = new List<BuildingDefinition>();

            foreach (var d in definitions)
            {
                Validate(d);
                if (_byId.ContainsKey(d.Id))
                    throw new ArgumentException("Duplicate building definition: " + d.Id);
                _byId[d.Id] = d;
                _byKind[d.Kind].Add(d);
            }
        }

        public bool TryGet(string id, out BuildingDefinition d) => _byId.TryGetValue(id, out d);

        public BuildingDefinition Get(string id) => _byId[id];

        public IReadOnlyList<BuildingDefinition> Variants(BuildingKind kind) => _byKind[kind];

        public static BuildingCatalog Default() => new BuildingCatalog(new[]
        {
            new BuildingDefinition("residential_low_01", BuildingKind.Residential, 0, 5, 20, 10, 8, 0),
            new BuildingDefinition("commercial_small_01", BuildingKind.Commercial, 0, 8, 10, 12, 5, 0),
            new BuildingDefinition("commercial_large_01", BuildingKind.Commercial, 0, 15, 20, 20, 8, 0),
            new BuildingDefinition("industrial_small_01", BuildingKind.Industrial, 0, 10, 15, 20, 10, 0),
            new BuildingDefinition("industrial_large_01", BuildingKind.Industrial, 0, 18, 30, 35, 18, 0),
            new BuildingDefinition("power_plant_01", BuildingKind.Power, 5000, 100, 0, 0, 0, 500),
            new BuildingDefinition("water_pump_01", BuildingKind.Water, 3000, 60, 0, 0, 0, 400),
        });

        static void Validate(BuildingDefinition d)
        {
            if (string.IsNullOrEmpty(d.Id))
                throw new ArgumentException("Building definition has no ID.");
            if (d.ConstructionCost < 0 || d.Maintenance < 0 || d.Capacity < 0
                || d.PowerDemand < 0 || d.WaterDemand < 0 || d.Supply < 0)
                throw new ArgumentException("Building definition has a negative value: " + d.Id);
        }
    }
}

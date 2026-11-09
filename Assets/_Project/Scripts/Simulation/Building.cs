using System;

namespace Metropolis.Core
{
    [Flags]
    public enum BuildingStatus { None = 0, NoRoad = 1, NoPower = 2, NoWater = 4 }

    public sealed class Building
    {
        public Building(int id, string definitionId, Cell cell)
        {
            Id = id;
            DefinitionId = definitionId;
            Cell = cell;
        }

        public int Id { get; }
        public string DefinitionId { get; }
        public Cell Cell { get; }

        /// <summary>Derived daily, never saved.</summary>
        public int Occupancy { get; set; }

        /// <summary>Derived daily, never saved.</summary>
        public BuildingStatus Status { get; set; }

        public bool IsOperational => Status == BuildingStatus.None;
    }
}

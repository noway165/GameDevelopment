using System.Collections.Generic;

namespace Metropolis.Core
{
    /// <summary>Scratch data shared by the systems during one day.</summary>
    public sealed class DayWork
    {
        public HashSet<int> PowerServed { get; } = new HashSet<int>();
        public HashSet<int> WaterServed { get; } = new HashSet<int>();
        public List<Building> StatusChanged { get; } = new List<Building>();
        public List<Building> Added { get; } = new List<Building>();
    }
}

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Metropolis.Core
{
    /// <summary>On-disk save format. Holds source data only; everything else is recalculated.</summary>
    public sealed class CitySaveData
    {
        [JsonProperty("saveVersion", Required = Required.Always)] public int SaveVersion;
        [JsonProperty("cityName", Required = Required.Always)] public string CityName;
        [JsonProperty("day", Required = Required.Always)] public int Day;
        [JsonProperty("population", Required = Required.Always)] public int Population;
        [JsonProperty("budget", Required = Required.Always)] public long Budget;
        [JsonProperty("taxRate", Required = Required.Always)] public int TaxRate;
        [JsonProperty("gridWidth", Required = Required.Always)] public int GridWidth;
        [JsonProperty("gridHeight", Required = Required.Always)] public int GridHeight;
        [JsonProperty("nextBuildingId", Required = Required.Always)] public int NextBuildingId;
        [JsonProperty("roads", Required = Required.Always)] public List<int[]> Roads;
        [JsonProperty("zones", Required = Required.Always)] public List<ZoneSaveData> Zones;
        [JsonProperty("buildings", Required = Required.Always)] public List<BuildingSaveData> Buildings;
    }

    public sealed class ZoneSaveData
    {
        [JsonProperty("x", Required = Required.Always)] public int X;
        [JsonProperty("y", Required = Required.Always)] public int Y;
        [JsonProperty("type", Required = Required.Always)] public ZoneType Type;
    }

    public sealed class BuildingSaveData
    {
        [JsonProperty("id", Required = Required.Always)] public int Id;
        [JsonProperty("definitionId", Required = Required.Always)] public string DefinitionId;
        [JsonProperty("x", Required = Required.Always)] public int X;
        [JsonProperty("y", Required = Required.Always)] public int Y;
    }
}

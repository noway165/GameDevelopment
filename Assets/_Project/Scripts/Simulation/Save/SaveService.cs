using System;
using System.Collections.Generic;
using System.IO;

namespace Metropolis.Core
{
    /// <summary>Saves and loads one city at one path.</summary>
    public sealed class SaveService
    {
        public SaveService(string path)
        {
            Path = path;
        }

        public string Path { get; }
        public bool Exists => File.Exists(Path);

        public bool TrySave(CityState city, out string error)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }

            return SaveFile.TryWrite(Path, SaveSerializer.Serialize(city), out error);
        }

        public bool TryLoad(BuildingCatalog catalog, SimulationConfig config, out CityState city,
            out IReadOnlyList<string> errors)
        {
            if (!SaveFile.TryRead(Path, out var json, out var error))
            {
                city = null;
                errors = new[] { error };
                return false;
            }

            return SaveSerializer.TryLoad(json, catalog, config, out city, out errors);
        }
    }
}

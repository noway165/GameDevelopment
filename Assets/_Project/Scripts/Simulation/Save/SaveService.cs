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
                MoveAsideIfCorrupt();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }

            return SaveFile.TryWrite(Path, SaveSerializer.Serialize(city), out error);
        }

        /// <summary>Stops a corrupt save from being rotated onto the last good backup.</summary>
        void MoveAsideIfCorrupt()
        {
            if (!SaveFile.TryRead(Path, out var current, out _) || SaveSerializer.IsParseable(current))
                return;

            var aside = Path + ".corrupt";
            File.Delete(aside);
            File.Move(Path, aside);
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

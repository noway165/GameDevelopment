using System;
using System.IO;

namespace Metropolis.Core
{
    public static class SaveFile
    {
        /// <summary>Writes through a temporary file so a crash cannot corrupt the existing save.</summary>
        public static bool TryWrite(string path, string json, out string error)
        {
            error = null;
            string temp = path + ".tmp";
            try
            {
                File.WriteAllText(temp, json);
                if (File.Exists(path))
                    File.Replace(temp, path, path + ".bak");
                else
                    File.Move(temp, path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = e.Message;
                TryDelete(temp);
                return false;
            }
        }

        public static bool TryRead(string path, out string json, out string error)
        {
            json = null;
            error = null;
            try
            {
                json = File.ReadAllText(path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }
        }

        static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // The original error is the one worth reporting.
            }
        }
    }
}

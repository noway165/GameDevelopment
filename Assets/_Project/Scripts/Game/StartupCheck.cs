using System;
using System.Collections.Generic;
using System.Linq;
using Metropolis.Core;
using UnityEngine;

namespace Metropolis.Game
{
    /// <summary>Finds wiring mistakes in a hand-built scene before the game starts.</summary>
    public static class StartupCheck
    {
        const float MapSize = 800f;

        public static List<string> Find(Terrain terrain, IReadOnlyList<BuildingAsset> buildings, GameObject road,
            GameObject residentialZone, GameObject commercialZone, GameObject industrialZone, GameObject preview)
        {
            var problems = new List<string>();
            CheckTerrain(terrain, problems);
            CheckBuildings(buildings, problems);
            Require(road, "Road prefab", problems);
            Require(residentialZone, "Residential zone prefab", problems);
            Require(commercialZone, "Commercial zone prefab", problems);
            Require(industrialZone, "Industrial zone prefab", problems);
            Require(preview, "Preview prefab", problems);
            return problems;
        }

        static void CheckTerrain(Terrain terrain, List<string> problems)
        {
            if (terrain == null)
            {
                problems.Add("Terrain is not assigned.");
                return;
            }

            if (terrain.terrainData == null)
            {
                problems.Add("Terrain has no Terrain Data.");
                return;
            }

            var size = terrain.terrainData.size;
            if (!Mathf.Approximately(size.x, MapSize) || !Mathf.Approximately(size.z, MapSize))
                problems.Add("Terrain must be " + MapSize + " by " + MapSize + " m, but is " + size.x + " by " + size.z + ".");
            if (terrain.transform.position != Vector3.zero)
                problems.Add("Terrain must be at position (0, 0, 0).");
            if (terrain.GetComponent<TerrainCollider>() == null)
                problems.Add("Terrain has no Terrain Collider.");
        }

        static void CheckBuildings(IReadOnlyList<BuildingAsset> buildings, List<string> problems)
        {
            if (buildings == null || buildings.Count == 0)
            {
                problems.Add("No building assets are assigned.");
                return;
            }

            bool complete = true;
            foreach (var asset in buildings)
            {
                if (asset == null)
                {
                    problems.Add("The building list has an empty slot.");
                    complete = false;
                }
                else if (asset.prefab == null)
                {
                    problems.Add("Building asset '" + asset.id + "' has no prefab.");
                }
            }

            if (!complete)
                return;

            try
            {
                new BuildingCatalog(buildings.Select(asset => asset.ToDefinition()));
            }
            catch (ArgumentException e)
            {
                problems.Add(e.Message);
            }
        }

        static void Require(GameObject prefab, string name, List<string> problems)
        {
            if (prefab == null)
                problems.Add(name + " is not assigned.");
        }
    }
}

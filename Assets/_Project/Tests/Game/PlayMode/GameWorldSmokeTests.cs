using System.Collections;
using Metropolis.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Metropolis.Game.PlayTests
{
    public class GameWorldSmokeTests
    {
        static Cell C(int x, int y) => new Cell(x, y);

        [UnityTest]
        public IEnumerator GameWorld_BuildsAndGrows()
        {
            SceneManager.LoadScene("GameWorld");
            yield return null;
            yield return null;
            var c = Object.FindFirstObjectByType<GameController>();
            Assert.That(c.enabled, Is.True);
            Assert.That(Object.FindFirstObjectByType<InputBridge>(), Is.Not.Null);

            c.Clock.Speed = SimulationSpeed.Paused;
            c.Tools.SelectTool(BuildTool.Road);
            c.Tools.PointerDown(C(1, 50));
            c.Tools.PointerMove(C(20, 50));
            c.Tools.PointerUp(C(20, 50));
            c.Tools.SelectTool(BuildTool.PowerPlant);
            c.Tools.PointerDown(C(1, 51));
            c.Tools.PointerUp(C(1, 51));
            c.Tools.SelectTool(BuildTool.WaterPump);
            c.Tools.PointerDown(C(2, 51));
            c.Tools.PointerUp(C(2, 51));
            c.Tools.SelectTool(BuildTool.ZoneResidential);
            c.Tools.PointerDown(C(5, 51));
            c.Tools.PointerMove(C(15, 52));
            c.Tools.PointerUp(C(15, 52));

            for (int i = 0; i < 30; i++)
                c.Simulation.AdvanceDay();
            yield return null;

            var city = c.Simulation.City;
            int roads = 0, zones = 0;
            for (int y = 0; y < city.Height; y++)
            {
                for (int x = 0; x < city.Width; x++)
                {
                    if (city.HasRoad(C(x, y))) roads++;
                    if (city.GetZone(C(x, y)) != ZoneType.None) zones++;
                }
            }

            Assert.That(c.RoadView.Count, Is.EqualTo(roads));
            Assert.That(c.ZoneView.Count, Is.EqualTo(zones));
            Assert.That(c.BuildingView.Count, Is.EqualTo(city.Buildings.Count));
            Assert.That(city.Buildings.Count, Is.GreaterThan(2));
            Assert.That(city.Population, Is.GreaterThan(0));
        }
    }
}

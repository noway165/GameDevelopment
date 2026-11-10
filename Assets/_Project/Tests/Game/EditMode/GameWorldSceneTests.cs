using System.Linq;
using Metropolis.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Metropolis.Game.Tests
{
    public class GameWorldSceneTests
    {
        [SetUp]
        public void OpenScene() =>
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/GameWorld.unity");

        static GameController Controller() => Object.FindFirstObjectByType<GameController>();

        [Test]
        public void Scene_PassesStartupChecks()
        {
            var c = Controller();
            Assert.That(StartupCheck.Find(c.Terrain, c.Buildings, c.RoadPrefab, c.ResidentialZonePrefab,
                c.CommercialZonePrefab, c.IndustrialZonePrefab, c.PreviewPrefab), Is.Empty);
        }

        [Test]
        public void Assets_MatchDefaultCatalog_InOrder()
        {
            var expected = new[]
            {
                "residential_low_01", "commercial_small_01", "commercial_large_01",
                "industrial_small_01", "industrial_large_01", "power_plant_01", "water_pump_01",
            };
            var catalog = BuildingCatalog.Default();
            var c = Controller();
            Assert.That(c.Buildings.Count, Is.EqualTo(7));
            for (int i = 0; i < 7; i++)
            {
                var a = c.Buildings[i].ToDefinition();
                var d = catalog.Get(expected[i]);
                Assert.That(a.Id, Is.EqualTo(d.Id));
                Assert.That(a.Kind, Is.EqualTo(d.Kind), d.Id);
                Assert.That(a.ConstructionCost, Is.EqualTo(d.ConstructionCost), d.Id);
                Assert.That(a.Maintenance, Is.EqualTo(d.Maintenance), d.Id);
                Assert.That(a.Capacity, Is.EqualTo(d.Capacity), d.Id);
                Assert.That(a.PowerDemand, Is.EqualTo(d.PowerDemand), d.Id);
                Assert.That(a.WaterDemand, Is.EqualTo(d.WaterDemand), d.Id);
                Assert.That(a.Supply, Is.EqualTo(d.Supply), d.Id);
            }
        }

        [Test]
        public void Scene_HasWiredInterface()
        {
            var ui = Object.FindFirstObjectByType<GameUi>();
            Assert.That(ui, Is.Not.Null);
            var panel = ui.GetComponent<UIDocument>().panelSettings;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.themeStyleSheet, Is.Not.Null);
        }

        [Test]
        public void Scene_InputKnowsTheInterface()
        {
            var input = new SerializedObject(Object.FindFirstObjectByType<InputBridge>());
            Assert.That(input.FindProperty("ui").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void Terrain_HasPaintedCellGrid()
        {
            // Without a layer the checkerboard exists only in the Editor, and a build shows blank white.
            var layers = Controller().Terrain.terrainData.terrainLayers;
            Assert.That(layers.Length, Is.EqualTo(1));
            Assert.That(layers[0].diffuseTexture, Is.Not.Null);
            Assert.That(layers[0].tileSize, Is.EqualTo(new Vector2(8, 8)));
        }

        [Test]
        public void Camera_StartsWithEntryRoadInView()
        {
            var view = Camera.main;
            view.aspect = 16f / 9f;
            var entry = view.WorldToViewportPoint(new Vector3(4, 0, 404));
            Assert.That(entry.z, Is.GreaterThan(0));
            Assert.That(entry.x, Is.InRange(0.05f, 0.95f));
            Assert.That(entry.y, Is.InRange(0.15f, 0.85f));
        }

        [Test]
        public void Scene_IsInBuildList() =>
            Assert.That(EditorBuildSettings.scenes.Any(s => s.path.EndsWith("GameWorld.unity")), Is.True);
    }
}

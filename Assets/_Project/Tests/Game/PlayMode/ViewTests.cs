using System.Collections;
using System.Collections.Generic;
using Metropolis.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Metropolis.Game.PlayTests
{
    public class ViewTests
    {
        static Cell C(int x, int y) => new Cell(x, y);
        static readonly GridMapper Map = new GridMapper(8f, 100, 100);

        Simulation _sim;
        Transform _parent;
        GameObject _prefab;

        [SetUp]
        public void Create()
        {
            var config = new SimulationConfig();
            _sim = new Simulation(CityState.CreateNew("T", config), BuildingCatalog.Default(), config);
            _parent = new GameObject("views").transform;
            _prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        }

        [TearDown]
        public void Clean()
        {
            Object.Destroy(_parent.gameObject);
            Object.Destroy(_prefab);
        }

        [UnityTest]
        public IEnumerator RoadView_FollowsBuildAndRemove()
        {
            var view = new RoadView(_sim, Map, _prefab, _parent);
            Assert.That(view.Count, Is.EqualTo(1));
            _sim.Execute(new BuildRoadCommand(C(1, 50), C(3, 50)));
            Assert.That(view.Count, Is.EqualTo(4));
            Assert.That(_parent.GetChild(1).position, Is.EqualTo(Map.CellCenter(C(1, 50))));
            _sim.Execute(new RemoveRoadCommand(C(2, 50), C(2, 50)));
            yield return null;
            Assert.That(view.Count, Is.EqualTo(3));
            Assert.That(_parent.childCount, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator ZoneView_ReplacesObjectOnRepaint()
        {
            var other = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            other.name = "sphere";
            var view = new ZoneView(_sim, Map, _prefab, other, _prefab, _parent);
            _sim.Execute(new CreateZoneCommand(new[] { C(1, 51) }, ZoneType.Residential));
            Assert.That(view.Count, Is.EqualTo(1));
            _sim.Execute(new CreateZoneCommand(new[] { C(1, 51) }, ZoneType.Commercial));
            yield return null;
            Assert.That(view.Count, Is.EqualTo(1));
            Assert.That(_parent.childCount, Is.EqualTo(1));
            Assert.That(_parent.GetChild(0).name, Does.Contain("sphere"));
            _sim.Execute(new RemoveZoneCommand(new[] { C(1, 51) }));
            yield return null;
            Assert.That(view.Count, Is.EqualTo(0));
            Object.Destroy(other);
        }

        Dictionary<string, GameObject> Prefabs() => new Dictionary<string, GameObject>
        {
            ["power_plant_01"] = _prefab,
            ["residential_low_01"] = _prefab,
        };

        [Test]
        public void BuildingView_AddsOnEvent()
        {
            var view = new BuildingView(_sim, Map, Prefabs(), _parent);
            _sim.Execute(new ConstructBuildingCommand("power_plant_01", C(1, 51)));
            Assert.That(view.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BuildingView_DrawsExisting_TintsBroken_AndRemoves()
        {
            var prefabs = Prefabs();
            _sim.Execute(new ConstructBuildingCommand("power_plant_01", C(1, 51)));
            _sim.City.SetZone(C(3, 51), ZoneType.Residential);
            var house = _sim.City.AddBuilding("residential_low_01", C(3, 51));
            var late = new BuildingView(_sim, Map, prefabs, _parent);
            Assert.That(late.Count, Is.EqualTo(2));

            _sim.AdvanceDay();
            var block = new MaterialPropertyBlock();
            late.Find(house.Id).GetComponentInChildren<Renderer>().GetPropertyBlock(block);
            Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(Color.red));

            _sim.Execute(new RemoveBuildingCommand(house.Id));
            yield return null;
            Assert.That(late.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PreviewView_ShowsAndReusesQuads()
        {
            var tools = new ToolController(_sim);
            var view = new PreviewView(tools, Map, _prefab, _parent);
            tools.SelectTool(BuildTool.Road);
            tools.PointerDown(C(1, 50));
            tools.PointerMove(C(4, 50));
            view.Refresh();
            Assert.That(view.ActiveCount, Is.EqualTo(4));
            var created = _parent.childCount;
            tools.Cancel();
            view.Refresh();
            Assert.That(view.ActiveCount, Is.EqualTo(1));
            Assert.That(_parent.childCount, Is.EqualTo(created));
            yield return null;
        }
    }
}

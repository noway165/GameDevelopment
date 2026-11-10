using System.Collections;
using System.IO;
using Metropolis.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Metropolis.Game.PlayTests
{
    public class SessionTests
    {
        static Cell C(int x, int y) => new Cell(x, y);

        GameController _c;
        string _path;

        [UnitySetUp]
        public IEnumerator Load()
        {
            SceneManager.LoadScene("GameWorld");
            yield return null;
            yield return null;
            _c = Object.FindFirstObjectByType<GameController>();
            _path = Path.Combine(Application.temporaryCachePath, Path.GetRandomFileName(), "city.json");
            _c.Saves = new SaveService(_path);
        }

        void BuildStreet()
        {
            _c.Simulation.Execute(new BuildRoadCommand(C(1, 50), C(10, 50)));
            _c.Simulation.Execute(new ConstructBuildingCommand("power_plant_01", C(1, 51)));
        }

        [Test]
        public void Launch_HasNoSession_AndSaveIsRefused()
        {
            Assert.That(_c.SessionActive, Is.False);
            Assert.That(_c.TrySave(out var error), Is.False);
            Assert.That(error, Is.EqualTo("No city in progress."));
            Assert.That(File.Exists(_path), Is.False);
        }

        [UnityTest]
        public IEnumerator Save_NewCity_Continue_RestoresCityAndViews()
        {
            _c.StartNewCity();
            Assert.That(_c.SessionActive, Is.True);
            BuildStreet();
            for (int i = 0; i < 5; i++)
                _c.Simulation.AdvanceDay();
            var saved = SaveSerializer.Serialize(_c.Simulation.City);
            Assert.That(_c.TrySave(out var error), Is.True, error);

            _c.StartNewCity();
            yield return null;
            Assert.That(_c.Simulation.City.Budget, Is.EqualTo(50000L));
            Assert.That(_c.RoadView.Count, Is.EqualTo(1));
            Assert.That(_c.BuildingView.Count, Is.EqualTo(0));
            Assert.That(GameObject.Find("Views").transform.childCount, Is.LessThanOrEqualTo(2));

            Assert.That(_c.TryContinue(out _), Is.True);
            yield return null;
            Assert.That(SaveSerializer.Serialize(_c.Simulation.City), Is.EqualTo(saved));
            Assert.That(_c.RoadView.Count, Is.EqualTo(11));
            Assert.That(_c.BuildingView.Count, Is.EqualTo(1));
            Assert.That(GameObject.Find("Views").transform.childCount, Is.InRange(12, 13));
        }

        [Test]
        public void Continue_CorruptSave_KeepsCityAndFile()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, "{ broken");
            _c.StartNewCity();
            BuildStreet();
            var before = SaveSerializer.Serialize(_c.Simulation.City);
            Assert.That(_c.TryContinue(out var errors), Is.False);
            Assert.That(errors.Count, Is.GreaterThan(0));
            Assert.That(SaveSerializer.Serialize(_c.Simulation.City), Is.EqualTo(before));
            Assert.That(File.ReadAllText(_path), Is.EqualTo("{ broken"));
        }

        [Test]
        public void Continue_NoFile_FailsWithoutSession()
        {
            Assert.That(_c.TryContinue(out var errors), Is.False);
            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.That(_c.SessionActive, Is.False);
        }
    }
}

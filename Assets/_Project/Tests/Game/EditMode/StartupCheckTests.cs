using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Metropolis.Game.Tests
{
    public class StartupCheckTests
    {
        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void Clean()
        {
            foreach (var g in _made)
                Object.DestroyImmediate(g);
            _made.Clear();
        }

        GameObject Go()
        {
            var g = new GameObject();
            _made.Add(g);
            return g;
        }

        Terrain MakeTerrain(float size, bool collider)
        {
            var g = Go();
            var t = g.AddComponent<Terrain>();
            t.terrainData = new TerrainData { size = new Vector3(size, 100, size) };
            if (collider)
                g.AddComponent<TerrainCollider>();
            return t;
        }

        BuildingAsset Asset(string id, GameObject prefab)
        {
            var a = ScriptableObject.CreateInstance<BuildingAsset>();
            a.id = id;
            a.prefab = prefab;
            return a;
        }

        List<string> Find(Terrain t, params BuildingAsset[] assets) =>
            StartupCheck.Find(t, assets, Go(), Go(), Go(), Go(), Go());

        [Test]
        public void Valid_HasNoProblems() =>
            Assert.That(Find(MakeTerrain(800, true), Asset("a", Go())), Is.Empty);

        [Test]
        public void AllMissing_ReportsEachReference() =>
            Assert.That(StartupCheck.Find(null, null, null, null, null, null, null).Count, Is.EqualTo(7));

        [Test]
        public void MissingPrefab_NamesTheAsset() =>
            Assert.That(string.Join("|", Find(MakeTerrain(800, true), Asset("house", null))), Does.Contain("house"));

        [Test]
        public void NullListEntry_IsReported() =>
            Assert.That(Find(MakeTerrain(800, true), new BuildingAsset[] { null }).Count, Is.EqualTo(1));

        [Test]
        public void DuplicateId_IsReported() =>
            Assert.That(Find(MakeTerrain(800, true), Asset("a", Go()), Asset("a", Go())).Count, Is.EqualTo(1));

        [Test]
        public void WrongTerrainSize_IsReported() =>
            Assert.That(Find(MakeTerrain(400, true), Asset("a", Go())).Count, Is.EqualTo(1));

        [Test]
        public void MissingCollider_IsReported() =>
            Assert.That(Find(MakeTerrain(800, false), Asset("a", Go())).Count, Is.EqualTo(1));

        [Test]
        public void MovedTerrain_IsReported()
        {
            var t = MakeTerrain(800, true);
            t.transform.position = new Vector3(10, 0, 0);
            Assert.That(Find(t, Asset("a", Go())).Count, Is.EqualTo(1));
        }
    }
}

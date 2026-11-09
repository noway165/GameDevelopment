using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Metropolis.Core.Tests
{
    public class SaveTests
    {
        static TestWorld Built()
        {
            var w = TestWorld.Create().Road(0, 10, 20, 10);
            w.Place("power_plant_01", 1, 9);
            w.Place("residential_low_01", 2, 9);
            return w;
        }

        static bool Load(string json, out CityState s, out IReadOnlyList<string> e)
        {
            var w = TestWorld.Create();
            return SaveSerializer.TryLoad(json, w.Ctx.Catalog, w.Ctx.Config, out s, out e);
        }

        static string Mutated(Action<JObject> edit)
        {
            var o = JObject.Parse(SaveSerializer.Serialize(Built().City));
            edit(o);
            return o.ToString();
        }

        static void AssertRefused(string json)
        {
            Assert.That(Load(json, out var s, out var e), Is.False);
            Assert.That(s, Is.Null);
            Assert.That(e.Count, Is.GreaterThan(0));
        }

        [Test]
        public void RoundTrip_ProducesIdenticalJson()
        {
            var json = SaveSerializer.Serialize(Built().City);
            Assert.That(Load(json, out var s, out _), Is.True);
            Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json));
        }

        [Test] public void NotJson_Refused() => AssertRefused("hello");
        [Test] public void Truncated_Refused() => AssertRefused(SaveSerializer.Serialize(Built().City).Substring(0, 40));
        [Test] public void MissingField_Refused() => AssertRefused(Mutated(o => o.Remove("budget")));
        [Test] public void WrongType_Refused() => AssertRefused(Mutated(o => o["budget"] = "abc"));
        [Test] public void FutureVersion_Refused() => AssertRefused(Mutated(o => o["saveVersion"] = 2));
        [Test] public void ZeroGrid_Refused() => AssertRefused(Mutated(o => o["gridWidth"] = 0));
        [Test] public void GiantGrid_Refused() => AssertRefused(Mutated(o => o["gridWidth"] = 2000000000));
        [Test] public void RoadOutOfBounds_Refused() => AssertRefused(Mutated(o => ((JArray)o["roads"]).Add(new JArray(999, 999))));
        [Test] public void MalformedRoad_Refused() => AssertRefused(Mutated(o => ((JArray)o["roads"]).Add(new JArray(1))));
        [Test] public void MissingEntry_Refused() => AssertRefused(Mutated(o => o["roads"] = new JArray { new JArray(5, 10) }));
        [Test] public void DuplicateBuildingId_Refused() => AssertRefused(Mutated(o => o["buildings"][1]["id"] = o["buildings"][0]["id"]));
        [Test] public void LowNextBuildingId_Refused() => AssertRefused(Mutated(o => o["nextBuildingId"] = 1));
        [Test] public void UnknownDefinition_Refused() => AssertRefused(Mutated(o => o["buildings"][0]["definitionId"] = "nope"));
        [Test] public void NegativeDay_Refused() => AssertRefused(Mutated(o => o["day"] = -1));
        [Test] public void NegativePopulation_Refused() => AssertRefused(Mutated(o => o["population"] = -1));
        [Test] public void TaxTooHigh_Refused() => AssertRefused(Mutated(o => o["taxRate"] = 31));
        [Test] public void GrownBuildingWithoutZone_Refused() => AssertRefused(Mutated(o => o["zones"] = new JArray()));
        [Test] public void BuildingOnRoad_Refused() => AssertRefused(Mutated(o => ((JArray)o["roads"]).Add(new JArray(1, 9))));

        [Test]
        public void ReportsEveryProblem()
        {
            var json = Mutated(o => { o["day"] = -1; o["taxRate"] = 31; });
            Load(json, out _, out var e);
            Assert.That(e.Count, Is.EqualTo(2));
        }

        [Test]
        public void RefusedCommands_LeaveSaveUnchanged()
        {
            var w = Built();
            var before = SaveSerializer.Serialize(w.City);
            var bad = new ICommand[]
            {
                new BuildRoadCommand(new Cell(1, 1), new Cell(2, 2)),
                new RemoveRoadCommand(new Cell(0, 10), new Cell(1, 10)),
                new CreateZoneCommand(new[] { new Cell(1, 1) }, ZoneType.Commercial),
                new RemoveZoneCommand(new[] { new Cell(2, 9) }),
                new ConstructBuildingCommand("nope", new Cell(3, 9)),
                new RemoveBuildingCommand(99),
                new SetTaxRateCommand(99),
            };
            foreach (var c in bad)
                Assert.That(c.Execute(w.Ctx).Succeeded, Is.False);
            Assert.That(SaveSerializer.Serialize(w.City), Is.EqualTo(before));
        }

        static string TempSave() => Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

        [Test]
        public void FirstWrite_CreatesFile_WithNoBackup()
        {
            var p = TempSave();
            Assert.That(SaveFile.TryWrite(p, "one", out _), Is.True);
            Assert.That(File.ReadAllText(p), Is.EqualTo("one"));
            Assert.That(File.Exists(p + ".bak"), Is.False);
        }

        [Test]
        public void SecondWrite_KeepsPreviousAsBackup()
        {
            var p = TempSave();
            SaveFile.TryWrite(p, "one", out _);
            SaveFile.TryWrite(p, "two", out _);
            Assert.That(File.ReadAllText(p), Is.EqualTo("two"));
            Assert.That(File.ReadAllText(p + ".bak"), Is.EqualTo("one"));
        }

        [Test]
        public void LockedFile_ReturnsError_KeepsData_NoTempLeft()
        {
            var p = TempSave();
            SaveFile.TryWrite(p, "one", out _);
            using (new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.That(SaveFile.TryWrite(p, "two", out _), Is.False);
            Assert.That(File.ReadAllText(p), Is.EqualTo("one"));
            Assert.That(File.Exists(p + ".tmp"), Is.False);
        }

        [Test]
        public void ReadMissingFile_ReturnsError()
        {
            Assert.That(SaveFile.TryRead(TempSave(), out _, out var error), Is.False);
            Assert.That(error, Is.Not.Null);
        }
    }
}

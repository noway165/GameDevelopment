using System.IO;
using NUnit.Framework;

namespace Metropolis.Core.Tests
{
    public class SaveServiceTests
    {
        static string TempPath() => Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "city.json");

        static TestWorld Built()
        {
            var w = TestWorld.Create().Road(0, 10, 20, 10);
            w.Place("power_plant_01", 1, 9);
            return w;
        }

        [Test]
        public void Save_CreatesMissingFolder_AndExistsTurnsTrue()
        {
            var s = new SaveService(TempPath());
            Assert.That(s.Exists, Is.False);
            Assert.That(s.TrySave(Built().City, out var error), Is.True, error);
            Assert.That(s.Exists, Is.True);
        }

        [Test]
        public void SaveThenLoad_RoundTrips()
        {
            var w = Built();
            var s = new SaveService(TempPath());
            s.TrySave(w.City, out _);
            Assert.That(s.TryLoad(w.Ctx.Catalog, w.Ctx.Config, out var city, out _), Is.True);
            Assert.That(SaveSerializer.Serialize(city), Is.EqualTo(SaveSerializer.Serialize(w.City)));
        }

        [Test]
        public void Load_MissingFile_ReportsOneError()
        {
            var w = Built();
            Assert.That(new SaveService(TempPath()).TryLoad(w.Ctx.Catalog, w.Ctx.Config, out var city, out var errors), Is.False);
            Assert.That(city, Is.Null);
            Assert.That(errors.Count, Is.EqualTo(1));
        }

        [Test]
        public void Load_CorruptFile_ReportsErrors_AndLeavesFile()
        {
            var w = Built();
            var path = TempPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "{ broken");
            Assert.That(new SaveService(path).TryLoad(w.Ctx.Catalog, w.Ctx.Config, out _, out var errors), Is.False);
            Assert.That(errors.Count, Is.GreaterThan(0));
            Assert.That(File.ReadAllText(path), Is.EqualTo("{ broken"));
        }

        [Test]
        public void Save_OverCorruptFile_KeepsGoodBackup()
        {
            var w = Built();
            var path = TempPath();
            var s = new SaveService(path);
            s.TrySave(w.City, out _);
            s.TrySave(w.City, out _);
            var good = File.ReadAllText(path + ".bak");
            File.WriteAllText(path, "{ broken");

            Assert.That(s.TrySave(w.City, out var error), Is.True, error);

            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(good));
            Assert.That(File.ReadAllText(path + ".corrupt"), Is.EqualTo("{ broken"));
        }

        [Test]
        public void SecondSave_KeepsBackup()
        {
            var w = Built();
            var s = new SaveService(TempPath());
            s.TrySave(w.City, out _);
            s.TrySave(w.City, out _);
            Assert.That(File.Exists(s.Path + ".bak"), Is.True);
        }
    }
}

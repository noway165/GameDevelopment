using NUnit.Framework;

namespace Metropolis.Core.Tests
{
    public class PopulationDemandTests
    {
        static readonly SimulationConfig Cfg = TestConfig.Pinned(21, 21);
        static TestWorld Street() => TestWorld.Create().Road(0, 10, 20, 10);

        [TestCase(10, 0, 0, 100)]
        [TestCase(20, 40, 10, 20)]
        [TestCase(30, 0, 0, 0)]
        [TestCase(5, 0, 0, 100)]
        [TestCase(30, 100, 100, 0)]
        public void Attractiveness(int tax, int unemployment, int nonRunning, int expected) =>
            Assert.That(PopulationSystem.Attractiveness(tax, unemployment, nonRunning, Cfg), Is.EqualTo(expected));

        [TestCase(15, 20, 1)]
        [TestCase(40, 100, 4)]
        [TestCase(15, 100, 2)]
        [TestCase(100, 0, 0)]
        [TestCase(0, 100, 0)]
        public void Arrivals_UseCeil(int vacant, int attractiveness, int expected) =>
            Assert.That(PopulationSystem.Arrivals(vacant, attractiveness), Is.EqualTo(expected));

        [TestCase(100, 39, 2)]
        [TestCase(100, 40, 0)]
        [TestCase(49, 0, 0)]
        public void Departures_OnlyBelowThreshold(int population, int attractiveness, int expected) =>
            Assert.That(PopulationSystem.Departures(population, attractiveness, Cfg), Is.EqualTo(expected));

        [Test]
        public void Population_CappedToHousing_ThenNoArrivals()
        {
            var w = Street();
            w.City.Population = 50;
            w.Place("residential_low_01", 1, 9);
            w.Place("residential_low_01", 2, 9);
            PopulationSystem.Run(w.Ctx);
            Assert.That(w.City.Population, Is.EqualTo(40));
            Assert.That(w.Stats.Arrivals, Is.EqualTo(0));
        }

        [Test]
        public void Residents_FillHousingAscending()
        {
            var w = Street();
            w.City.Population = 25;
            var a = w.Place("residential_low_01", 1, 9);
            var b = w.Place("residential_low_01", 2, 9);
            PopulationSystem.Run(w.Ctx);
            Assert.That(w.City.Population, Is.EqualTo(27));
            Assert.That(a.Occupancy, Is.EqualTo(20));
            Assert.That(b.Occupancy, Is.EqualTo(7));
        }

        [Test]
        public void BrokenHousing_DoesNotCount_AndLowersAttractiveness()
        {
            var w = Street();
            w.Place("residential_low_01", 1, 9);
            w.Place("residential_low_01", 2, 9).Status = BuildingStatus.NoPower;
            PopulationSystem.Run(w.Ctx);
            Assert.That(w.Stats.Housing, Is.EqualTo(20));
            Assert.That(w.Stats.NonRunningPercent, Is.EqualTo(50));
            Assert.That(w.Stats.Attractiveness, Is.EqualTo(50));
        }

        [Test]
        public void EmptyCity_HasNoDivideByZero()
        {
            var w = Street();
            PopulationSystem.Run(w.Ctx);
            Assert.That(w.Stats.UnemploymentPercent, Is.EqualTo(0));
            Assert.That(w.Stats.NonRunningPercent, Is.EqualTo(0));
            Assert.That(w.City.Population, Is.EqualTo(0));
        }

        [TestCase(100L, 0L, 100)]
        [TestCase(100L, 80L, 20)]
        [TestCase(0L, 0L, 0)]
        [TestCase(0L, 50L, 0)]
        [TestCase(232L, 100L, 56)]
        public void Demand_Formula(long target, long capacity, int expected) =>
            Assert.That(DemandSystem.Demand(target, capacity), Is.EqualTo(expected));

        [Test]
        public void Demand_UsesAllBuildings_AndFreeJobs()
        {
            var w = Street();
            w.City.Population = 28;
            w.Stats.Workers = 9;
            w.Stats.Jobs = 90;
            for (int x = 1; x <= 5; x++)
                w.Place("residential_low_01", x, 9).Status = BuildingStatus.NoPower;
            DemandSystem.Run(w.Ctx);
            Assert.That(w.Stats.ResidentialDemand, Is.EqualTo(56));
            Assert.That(w.Stats.CommercialDemand, Is.EqualTo(100));
            Assert.That(w.Stats.IndustrialDemand, Is.EqualTo(100));
        }
    }
}

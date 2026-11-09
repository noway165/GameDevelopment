using NUnit.Framework;

namespace Metropolis.Core.Tests
{
    public class InfrastructureTests
    {
        static void Infra(TestWorld w)
        {
            InfrastructureSystem.Run(w.Ctx, w.Work);
            OperationSystem.Run(w.Ctx, w.Work);
        }

        static TestWorld Street() => TestWorld.Create().Road(0, 10, 20, 10);

        [Test]
        public void Shortage_ServesAscendingId_ThenSkipsAndContinues()
        {
            var w = Street();
            w.Place("power_small", 1, 9);
            w.Place("water_pump_01", 2, 9);
            var a = w.Place("residential_low_01", 3, 9);
            var b = w.Place("residential_low_01", 4, 9);
            var c = w.Place("residential_low_01", 5, 9);
            var d = w.Place("res_tiny", 6, 9);
            Infra(w);
            Assert.That(a.Status, Is.EqualTo(BuildingStatus.None));
            Assert.That(b.Status, Is.EqualTo(BuildingStatus.None));
            Assert.That(c.Status, Is.EqualTo(BuildingStatus.NoPower));
            Assert.That(d.Status, Is.EqualTo(BuildingStatus.None));
            Assert.That(w.Stats.PowerRemaining, Is.EqualTo(0));
        }

        [Test]
        public void PowerAndWater_AreJudgedSeparately()
        {
            var w = Street();
            w.Place("power_plant_01", 1, 9);
            var a = w.Place("residential_low_01", 3, 9);
            Infra(w);
            Assert.That(a.Status, Is.EqualTo(BuildingStatus.NoWater));
            Assert.That(w.Stats.PowerRemaining, Is.EqualTo(490));
        }

        [Test]
        public void NoRoad_ConsumesNothing_AndUtilityWithoutRoadSuppliesNothing()
        {
            var w = Street();
            w.Place("power_plant_01", 1, 2);
            var a = w.Place("residential_low_01", 3, 2);
            Infra(w);
            Assert.That(a.Status, Is.EqualTo(BuildingStatus.NoRoad | BuildingStatus.NoPower | BuildingStatus.NoWater));
            Assert.That(w.Stats.PowerSupply, Is.EqualTo(0));
        }

        [Test]
        public void Utility_NeedsOnlyRoad()
        {
            var w = Street();
            var p = w.Place("power_plant_01", 1, 9);
            Infra(w);
            Assert.That(p.IsOperational, Is.True);
        }

        [Test]
        public void StatusChanged_ListsOnlyChanges()
        {
            var w = Street();
            w.Place("power_plant_01", 1, 9);
            var a = w.Place("residential_low_01", 3, 9);
            Infra(w);
            Assert.That(w.Work.StatusChanged, Is.EquivalentTo(new[] { a }));
            w.Work.StatusChanged.Clear();
            Infra(w);
            Assert.That(w.Work.StatusChanged.Count, Is.EqualTo(0));
        }

        [TestCase(9, 4)]
        [TestCase(10, 5)]
        [TestCase(0, 0)]
        public void Workers_FloorHalfPopulation(int population, int workers)
        {
            var w = Street();
            w.City.Population = population;
            EmploymentSystem.Run(w.Ctx);
            Assert.That(w.Stats.Workers, Is.EqualTo(workers));
        }

        [Test]
        public void Employed_FillsJobsAscending_SkipsBrokenBuildings()
        {
            var w = Street();
            w.City.Population = 30;
            var a = w.Place("commercial_small_01", 1, 9);
            var broken = w.Place("commercial_large_01", 2, 9);
            broken.Status = BuildingStatus.NoPower;
            var c = w.Place("industrial_small_01", 3, 9);
            EmploymentSystem.Run(w.Ctx);
            Assert.That(w.Stats.Jobs, Is.EqualTo(25));
            Assert.That(w.Stats.Employed, Is.EqualTo(15));
            Assert.That(a.Occupancy, Is.EqualTo(10));
            Assert.That(broken.Occupancy, Is.EqualTo(0));
            Assert.That(c.Occupancy, Is.EqualTo(5));
        }
    }
}

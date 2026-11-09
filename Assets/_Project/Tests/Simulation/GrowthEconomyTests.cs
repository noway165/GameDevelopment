using NUnit.Framework;

namespace Metropolis.Core.Tests
{
    public class GrowthEconomyTests
    {
        static TestWorld Zoned(int demand, int cells = 8)
        {
            var w = TestWorld.Create().Road(0, 10, 20, 10);
            for (int x = 1; x <= cells; x++)
                w.Zone(ZoneType.Residential, x, 9);
            w.Stats.ResidentialDemand = demand;
            w.Stats.PowerRemaining = 1000;
            w.Stats.WaterRemaining = 1000;
            return w;
        }

        [TestCase(19, 0)]
        [TestCase(20, 1)]
        [TestCase(25, 1)]
        [TestCase(50, 2)]
        [TestCase(100, 4)]
        public void GrowthCount(int demand, int expected)
        {
            var w = Zoned(demand);
            GrowthSystem.Run(w.Ctx, w.Work);
            Assert.That(w.City.Buildings.Count, Is.EqualTo(expected));
        }

        [Test]
        public void Growth_TakesCellsInAscendingOrder_AndIsFree()
        {
            var w = Zoned(50);
            GrowthSystem.Run(w.Ctx, w.Work);
            Assert.That(w.City.GetBuilding(new Cell(1, 9)), Is.Not.Null);
            Assert.That(w.City.GetBuilding(new Cell(2, 9)), Is.Not.Null);
            Assert.That(w.City.Budget, Is.EqualTo(50000L));
            Assert.That(w.Work.Added.Count, Is.EqualTo(2));
        }

        [Test]
        public void Growth_StopsWhenSpareRunsOut_AndReserves()
        {
            var w = Zoned(100);
            w.Stats.PowerRemaining = 12; // first cell takes 10; second cell's variant needs 5
            GrowthSystem.Run(w.Ctx, w.Work);
            Assert.That(w.City.Buildings.Count, Is.EqualTo(1));
            Assert.That(w.Stats.PowerRemaining, Is.EqualTo(2));
        }

        [Test]
        public void Growth_SkipsCellsWithoutRoadAccess()
        {
            var w = TestWorld.Create().Road(0, 10, 5, 10);
            w.Zone(ZoneType.Residential, 15, 3);
            w.Stats.ResidentialDemand = 100;
            w.Stats.PowerRemaining = 100;
            w.Stats.WaterRemaining = 100;
            GrowthSystem.Run(w.Ctx, w.Work);
            Assert.That(w.City.Buildings.Count, Is.EqualTo(0));
        }

        [TestCase(1, 9, "commercial_small_01")]
        [TestCase(2, 9, "commercial_large_01")]
        public void Growth_PicksVariantFromCell(int x, int y, string expected)
        {
            var w = TestWorld.Create().Road(0, 10, 20, 10);
            w.Zone(ZoneType.Commercial, x, y);
            w.Stats.CommercialDemand = 100;
            w.Stats.PowerRemaining = 100;
            w.Stats.WaterRemaining = 100;
            GrowthSystem.Run(w.Ctx, w.Work);
            Assert.That(w.City.GetBuilding(new Cell(x, y)).DefinitionId, Is.EqualTo(expected));
        }

        [Test]
        public void Economy_RevenueAndMaintenance_IncludingBrokenBuildings()
        {
            var w = TestWorld.Create();
            w.City.Population = 100;
            w.Stats.Employed = 40;
            w.Place("residential_low_01", 1, 9).Status = BuildingStatus.NoRoad;
            w.Place("power_plant_01", 2, 9);
            EconomySystem.Run(w.Ctx);
            Assert.That(w.Stats.Revenue, Is.EqualTo(320L));
            Assert.That(w.Stats.Expenses, Is.EqualTo(105L));
            Assert.That(w.City.Budget, Is.EqualTo(50215L));
        }

        [Test]
        public void Economy_MayGoNegative()
        {
            var w = TestWorld.Create();
            w.City.Budget = 10;
            w.Place("power_plant_01", 2, 9);
            EconomySystem.Run(w.Ctx);
            Assert.That(w.City.Budget, Is.EqualTo(-90L));
        }
    }
}

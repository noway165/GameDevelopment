using NUnit.Framework;

namespace Metropolis.Core.Tests
{
    public class RoadNetworkTests
    {
        [Test]
        public void NewCity_HasEntryBudgetAndDefaultTax()
        {
            var w = TestWorld.Create(21);
            Assert.That(w.City.HasRoad(new Cell(0, 10)), Is.True);
            Assert.That(w.City.Budget, Is.EqualTo(50000L));
            Assert.That(w.City.TaxRate, Is.EqualTo(10));
            Assert.That(w.City.Day, Is.EqualTo(0));
        }

        [Test]
        public void Road_TouchingEntry_IsConnected()
        {
            var w = TestWorld.Create().Road(0, 10, 5, 10);
            Assert.That(w.Ctx.Roads.IsConnected(new Cell(5, 10)), Is.True);
        }

        [Test]
        public void Island_IsNotConnected_AndGivesNoAccess()
        {
            var w = TestWorld.Create().Road(10, 3, 12, 3);
            Assert.That(w.Ctx.Roads.IsConnected(new Cell(10, 3)), Is.False);
            Assert.That(w.Ctx.Roads.HasRoadAccess(new Cell(10, 4)), Is.False);
        }

        [TestCase(5, 12, true)]
        [TestCase(5, 13, false)]
        [TestCase(6, 11, true)]
        [TestCase(7, 11, false)]
        public void Access_UsesManhattanRange(int x, int y, bool expected)
        {
            var w = TestWorld.Create().Road(0, 10, 5, 10);
            Assert.That(w.Ctx.Roads.HasRoadAccess(new Cell(x, y)), Is.EqualTo(expected));
        }

        [Test]
        public void RemovingMiddleCell_DisconnectsFarSide()
        {
            var w = TestWorld.Create().Road(0, 10, 5, 10);
            w.City.SetRoad(new Cell(3, 10), false);
            w.Ctx.Roads.Rebuild();
            Assert.That(w.Ctx.Roads.IsConnected(new Cell(5, 10)), Is.False);
        }

        [Test]
        public void FullThousandSquareMap_DoesNotOverflow()
        {
            var w = TestWorld.Create(1000);
            for (int y = 0; y < 1000; y++)
                for (int x = 0; x < 1000; x++)
                    w.City.SetRoad(new Cell(x, y), true);
            w.Ctx.Roads.Rebuild();
            Assert.That(w.Ctx.Roads.IsConnected(new Cell(999, 999)), Is.True);
        }
    }
}

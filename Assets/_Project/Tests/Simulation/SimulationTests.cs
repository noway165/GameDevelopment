using NUnit.Framework;

namespace Metropolis.Core.Tests
{
    public class SimulationTests
    {
        static Simulation Sim(TestWorld w) => new Simulation(w.City, w.Ctx.Catalog, w.Ctx.Config);
        static Cell C(int x, int y) => new Cell(x, y);

        [Test]
        public void AdvanceDay_IncrementsDay_AndGrowsFromEmptyZone()
        {
            var w = TestWorld.Create().Road(0, 10, 20, 10);
            w.Place("power_plant_01", 1, 9);
            w.Place("water_pump_01", 2, 9);
            w.Zone(ZoneType.Residential, 3, 9);
            var s = Sim(w);
            s.AdvanceDay();
            Assert.That(w.City.Day, Is.EqualTo(1));
            Assert.That(w.City.GetBuilding(C(3, 9)), Is.Not.Null);
            Assert.That(w.City.Population, Is.EqualTo(0));
            s.AdvanceDay();
            Assert.That(w.City.Population, Is.EqualTo(2));
        }

        [Test]
        public void DailyEvents_FireOnce_AfterTheDayIsFinished()
        {
            var w = TestWorld.Create().Road(0, 10, 20, 10);
            w.Place("power_plant_01", 1, 9);
            var s = Sim(w);
            int days = 0;
            long seen = 0;
            s.Events.DayAdvanced += d => { days++; seen = w.City.Budget; };
            s.AdvanceDay();
            Assert.That(days, Is.EqualTo(1));
            Assert.That(seen, Is.EqualTo(49900L));
        }

        [Test]
        public void GrownBuilding_RaisesAddedOnlyAfterTick()
        {
            var w = TestWorld.Create().Road(0, 10, 20, 10);
            w.Place("power_plant_01", 1, 9);
            w.Place("water_pump_01", 2, 9);
            w.Zone(ZoneType.Residential, 3, 9);
            var s = Sim(w);
            int added = 0;
            s.Events.BuildingAdded += _ => added++;
            s.AdvanceDay();
            Assert.That(added, Is.EqualTo(1));
        }

        [Test]
        public void Command_WorksWhilePaused_WithoutTick()
        {
            var w = TestWorld.Create();
            var s = Sim(w);
            Assert.That(s.Execute(new BuildRoadCommand(C(1, 10), C(3, 10))).Succeeded, Is.True);
            Assert.That(w.City.Day, Is.EqualTo(0));
        }

        [Test]
        public void CuttingOnlyRoad_DropsPopulation_WithoutThrowing()
        {
            var w = TestWorld.Create().Road(0, 10, 20, 10);
            w.Place("power_plant_01", 2, 9);
            w.Place("water_pump_01", 3, 9);
            var home = w.Place("residential_low_01", 15, 9);
            var s = Sim(w);
            for (int i = 0; i < 10; i++)
                s.AdvanceDay();
            Assert.That(w.City.Population, Is.GreaterThan(0));
            s.Execute(new RemoveRoadCommand(C(10, 10), C(10, 10)));
            s.AdvanceDay();
            Assert.That(w.City.Population, Is.EqualTo(0));
            Assert.That(home.Status.HasFlag(BuildingStatus.NoRoad), Is.True);
        }

        [Test]
        public void RefreshDerived_ChangesNoSourceData()
        {
            var w = TestWorld.Create().Road(0, 10, 20, 10);
            w.City.Population = 7;
            var s = Sim(w);
            s.RefreshDerived();
            Assert.That(w.City.Population, Is.EqualTo(7));
            Assert.That(w.City.Budget, Is.EqualTo(50000L));
            Assert.That(w.City.Day, Is.EqualTo(0));
        }

        static SimulationClock Clock(SimulationSpeed speed) =>
            new SimulationClock(TestConfig.Pinned(21, 21)) { Speed = speed };

        [TestCase(SimulationSpeed.Paused, 100.0, 0)]
        [TestCase(SimulationSpeed.X1, 1.0, 0)]
        [TestCase(SimulationSpeed.X1, 2.0, 1)]
        [TestCase(SimulationSpeed.X2, 1.0, 1)]
        [TestCase(SimulationSpeed.X4, 2.0, 4)]
        [TestCase(SimulationSpeed.X1, -5.0, 0)]
        public void Clock_Advance(SimulationSpeed speed, double seconds, int days) =>
            Assert.That(Clock(speed).Advance(seconds), Is.EqualTo(days));

        [Test]
        public void Clock_Accumulates()
        {
            var c = Clock(SimulationSpeed.X1);
            Assert.That(c.Advance(1.5), Is.EqualTo(0));
            Assert.That(c.Advance(0.5), Is.EqualTo(1));
        }

        [Test]
        public void Clock_LongHitch_IsCapped_AndNotQueued()
        {
            var c = Clock(SimulationSpeed.X4);
            Assert.That(c.Advance(1000.0), Is.EqualTo(8));
            Assert.That(c.Advance(0.1), Is.EqualTo(0));
        }
    }
}

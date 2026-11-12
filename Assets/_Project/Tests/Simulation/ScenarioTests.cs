using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace Metropolis.Core.Tests
{
    public class ScenarioTests
    {
        static TestWorld Scenario112()
        {
            var catalog = new BuildingCatalog(new[]
            {
                new BuildingDefinition("r100", BuildingKind.Residential, 0, 0, 100, 0, 0, 0),
                new BuildingDefinition("c50", BuildingKind.Commercial, 0, 0, 50, 0, 0, 0),
                new BuildingDefinition("i40", BuildingKind.Industrial, 0, 0, 40, 0, 0, 0),
            });
            var w = TestWorld.Create(21, catalog).Road(0, 10, 20, 10);
            w.Place("r100", 1, 9);
            w.Place("c50", 2, 9);
            w.Place("i40", 3, 9);
            return w;
        }

        [Test]
        public void Blueprint11_2_MatchesHandComputedDays()
        {
            var w = Scenario112();
            var s = new Simulation(w.City, w.Ctx.Catalog, w.Ctx.Config);
            s.AdvanceDay();
            Assert.That(w.City.Population, Is.EqualTo(10));
            Assert.That(w.City.Budget, Is.EqualTo(50020L));
            s.AdvanceDay();
            Assert.That(w.City.Population, Is.EqualTo(19));
            Assert.That(w.City.Budget, Is.EqualTo(50073L));
            s.AdvanceDay();
            Assert.That(w.City.Population, Is.EqualTo(28));
            Assert.That(w.City.Budget, Is.EqualTo(50156L));
            Assert.That(w.Stats.Workers, Is.EqualTo(9));
            Assert.That(w.Stats.Jobs, Is.EqualTo(90));
            Assert.That(w.Stats.ResidentialDemand, Is.EqualTo(56));
            Assert.That(w.Stats.CommercialDemand, Is.EqualTo(0));
            Assert.That(w.Stats.IndustrialDemand, Is.EqualTo(0));
        }

        static List<Cell> Row(int y)
        {
            var cells = new List<Cell>();
            for (int x = 20; x <= 99; x++)
                cells.Add(new Cell(x, y));
            return cells;
        }

        static Simulation Bot(int days, Action<int, Simulation> perDay = null)
        {
            var config = new SimulationConfig();
            var s = new Simulation(CityState.CreateNew("Bot", config), BuildingCatalog.Default(), config);

            s.Execute(new BuildRoadCommand(new Cell(0, 50), new Cell(99, 50)));
            s.Execute(new CreateZoneCommand(Row(48), ZoneType.Residential));
            s.Execute(new CreateZoneCommand(Row(49), ZoneType.Residential));
            s.Execute(new CreateZoneCommand(Row(51), ZoneType.Commercial));
            s.Execute(new CreateZoneCommand(Row(52), ZoneType.Industrial));

            var utilityCells = new Queue<Cell>();
            foreach (int y in new[] { 49, 51 })
                for (int x = 1; x <= 19; x++)
                    utilityCells.Enqueue(new Cell(x, y));

            void Build(string definitionId)
            {
                if (utilityCells.Count > 0 && s.Execute(new ConstructBuildingCommand(definitionId, utilityCells.Peek())).Succeeded)
                    utilityCells.Dequeue();
            }

            for (int day = 1; day <= days; day++)
            {
                if (s.City.Statistics.PowerRemaining < 60) Build("power_plant_01");
                if (s.City.Statistics.WaterRemaining < 60) Build("water_pump_01");
                s.AdvanceDay();
                perDay?.Invoke(day, s);
            }

            return s;
        }

        [Test]
        public void SameScript_ProducesIdenticalSave() =>
            Assert.That(SaveSerializer.Serialize(Bot(120).City), Is.EqualTo(SaveSerializer.Serialize(Bot(120).City)));

        [Test]
        public void RoundTrip_ThenThirtyDays_MatchesUninterruptedRun()
        {
            var original = Bot(60);
            var config = new SimulationConfig();
            var catalog = BuildingCatalog.Default();
            Assert.That(SaveSerializer.TryLoad(SaveSerializer.Serialize(original.City), catalog, config, out var state, out _), Is.True);
            var loaded = new Simulation(state, catalog, config);

            for (int i = 0; i < 30; i++)
            {
                original.AdvanceDay();
                loaded.AdvanceDay();
            }

            Assert.That(SaveSerializer.Serialize(loaded.City), Is.EqualTo(SaveSerializer.Serialize(original.City)));
        }

        [Test]
        public void Balance_100Days_And_365Days()
        {
            var csv = new StringBuilder("day,population,budget,housing,jobs,residential,commercial,industrial\n");
            int populationDay50 = 0, populationDay100 = 0;
            long budgetDay100 = 0, lowestBudget = long.MaxValue;

            var s = Bot(365, (day, sim) =>
            {
                var c = sim.City;
                var st = c.Statistics;
                csv.Append(day).Append(',').Append(c.Population).Append(',').Append(c.Budget).Append(',')
                    .Append(st.Housing).Append(',').Append(st.Jobs).Append(',').Append(st.ResidentialDemand).Append(',')
                    .Append(st.CommercialDemand).Append(',').Append(st.IndustrialDemand).Append('\n');
                lowestBudget = System.Math.Min(lowestBudget, c.Budget);
                if (day == 50) populationDay50 = c.Population;
                if (day == 100)
                {
                    populationDay100 = c.Population;
                    budgetDay100 = c.Budget;
                }
            });
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "metropolis-balance.csv"), csv.ToString());

            Assert.That(budgetDay100, Is.GreaterThan(0L));
            Assert.That(populationDay100, Is.GreaterThan(populationDay50));
            Assert.That(s.City.Population, Is.GreaterThanOrEqualTo(1000));
            Assert.That(lowestBudget, Is.GreaterThan(0L), "budget went negative");
            Assert.That(s.City.Budget, Is.InRange(50000L, 250000L), "budget at day 365");
        }

        [Test]
        public void FullMap_AdvanceDayTime_IsRecorded()
        {
            var w = TestWorld.Create(100);
            for (int y = 0; y < 100; y++)
                w.City.SetRoad(new Cell(0, y), true);
            for (int y = 0; y < 100; y++)
            {
                for (int x = 1; x < 100; x++)
                {
                    if (y % 3 == 0) w.City.SetRoad(new Cell(x, y), true);
                    else w.Place("residential_low_01", x, y);
                }
            }

            var s = new Simulation(w.City, w.Ctx.Catalog, w.Ctx.Config);
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
                s.AdvanceDay();
            timer.Stop();

            TestContext.WriteLine("Mean AdvanceDay ms: " + (timer.Elapsed.TotalMilliseconds / 100));
            Assert.That(w.City.Day, Is.EqualTo(100));
        }
    }
}

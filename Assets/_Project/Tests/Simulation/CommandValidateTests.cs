using NUnit.Framework;

namespace Metropolis.Core.Tests
{
    public class CommandValidateTests
    {
        static Cell C(int x, int y) => new Cell(x, y);

        static TestWorld Built()
        {
            var w = TestWorld.Create().Road(0, 10, 5, 10);
            w.Place("power_plant_01", 1, 9);
            w.Place("residential_low_01", 2, 9);
            return w;
        }

        static ICommand[] Commands() => new ICommand[]
        {
            new BuildRoadCommand(C(6, 10), C(8, 10)),
            new BuildRoadCommand(C(1, 1), C(2, 2)),
            new BuildRoadCommand(C(1, 10), C(4, 10)),
            new RemoveRoadCommand(C(3, 10), C(3, 10)),
            new RemoveRoadCommand(C(0, 10), C(1, 10)),
            new CreateZoneCommand(new[] { C(3, 9) }, ZoneType.Commercial),
            new CreateZoneCommand(new[] { C(3, 1) }, ZoneType.Commercial),
            new RemoveZoneCommand(new[] { C(2, 9) }),
            new ConstructBuildingCommand("water_pump_01", C(3, 9)),
            new ConstructBuildingCommand("nope", C(3, 9)),
            new RemoveBuildingCommand(1),
            new RemoveBuildingCommand(99),
            new SetTaxRateCommand(20),
            new SetTaxRateCommand(99),
        };

        [Test]
        public void Validate_MatchesExecute_AndChangesNothing()
        {
            for (int i = 0; i < Commands().Length; i++)
            {
                var w = Built();
                var before = SaveSerializer.Serialize(w.City);
                var command = Commands()[i];
                var validated = command.Validate(w.Ctx);
                Assert.That(SaveSerializer.Serialize(w.City), Is.EqualTo(before), "changed state at " + i);
                Assert.That(command.Execute(w.Ctx).Failure, Is.EqualTo(validated.Failure), "mismatch at " + i);
            }
        }

        [Test]
        public void Validate_RaisesNoEvents()
        {
            var w = Built();
            int raised = 0;
            w.Ctx.Events.RoadsChanged += () => raised++;
            w.Ctx.Events.BudgetChanged += _ => raised++;
            new BuildRoadCommand(C(6, 10), C(8, 10)).Validate(w.Ctx);
            Assert.That(raised, Is.EqualTo(0));
        }

        [Test]
        public void Simulation_Validate_PassesThrough()
        {
            var w = Built();
            var s = new Simulation(w.City, w.Ctx.Catalog, w.Ctx.Config);
            Assert.That(s.Validate(new SetTaxRateCommand(99)).Failure, Is.EqualTo(CommandFailure.InvalidTaxRate));
            Assert.That(w.City.TaxRate, Is.EqualTo(10));
        }
    }
}

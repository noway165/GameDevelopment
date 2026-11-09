using NUnit.Framework;

namespace Metropolis.Core.Tests
{
    public class CommandTests
    {
        static Cell C(int x, int y) => new Cell(x, y);
        static CommandFailure Run(TestWorld w, ICommand c) => c.Execute(w.Ctx).Failure;
        static TestWorld Street() => TestWorld.Create().Road(0, 10, 5, 10);

        [Test]
        public void BuildRoad_ChargesOnlyNewCells_AndConnects()
        {
            var w = Street();
            Assert.That(Run(w, new BuildRoadCommand(C(3, 10), C(8, 10))), Is.EqualTo(CommandFailure.None));
            Assert.That(w.City.Budget, Is.EqualTo(49970L));
            Assert.That(w.Ctx.Roads.IsConnected(C(8, 10)), Is.True);
        }

        [Test]
        public void BuildRoad_AllExisting_IsNothingToDo() =>
            Assert.That(Run(Street(), new BuildRoadCommand(C(1, 10), C(4, 10))), Is.EqualTo(CommandFailure.NothingToDo));

        [Test]
        public void BuildRoad_Diagonal_NotStraight() =>
            Assert.That(Run(Street(), new BuildRoadCommand(C(1, 1), C(2, 2))), Is.EqualTo(CommandFailure.NotStraight));

        [Test]
        public void BuildRoad_OutOfBounds() =>
            Assert.That(Run(Street(), new BuildRoadCommand(C(18, 1), C(25, 1))), Is.EqualTo(CommandFailure.OutOfBounds));

        [Test]
        public void BuildRoad_OverZone_RefusesWholeLine()
        {
            var w = Street();
            w.Zone(ZoneType.Residential, 8, 10);
            Assert.That(Run(w, new BuildRoadCommand(C(6, 10), C(9, 10))), Is.EqualTo(CommandFailure.CellOccupied));
            Assert.That(w.City.HasRoad(C(6, 10)), Is.False);
            Assert.That(w.City.Budget, Is.EqualTo(50000L));
        }

        [TestCase(30L, CommandFailure.None, 0L)]
        [TestCase(29L, CommandFailure.InsufficientFunds, 29L)]
        public void BuildRoad_ExactFunds(long budget, CommandFailure expected, long after)
        {
            var w = Street();
            w.City.Budget = budget;
            Assert.That(Run(w, new BuildRoadCommand(C(6, 10), C(8, 10))), Is.EqualTo(expected));
            Assert.That(w.City.Budget, Is.EqualTo(after));
        }

        [Test]
        public void RemoveRoad_Entry_IsProtected() =>
            Assert.That(Run(Street(), new RemoveRoadCommand(C(0, 10), C(2, 10))), Is.EqualTo(CommandFailure.ProtectedRoad));

        [Test]
        public void RemoveRoad_IsFree_AndDisconnects()
        {
            var w = Street();
            Assert.That(Run(w, new RemoveRoadCommand(C(3, 10), C(3, 10))), Is.EqualTo(CommandFailure.None));
            Assert.That(w.City.Budget, Is.EqualTo(50000L));
            Assert.That(w.Ctx.Roads.IsConnected(C(5, 10)), Is.False);
        }

        [Test]
        public void RemoveRoad_NoRoad_IsNothingToDo() =>
            Assert.That(Run(Street(), new RemoveRoadCommand(C(1, 1), C(3, 1))), Is.EqualTo(CommandFailure.NothingToDo));

        [Test]
        public void CreateZone_NeedsAccess_AndRefusesWholeList()
        {
            var w = Street();
            var r = Run(w, new CreateZoneCommand(new[] { C(1, 11), C(1, 15) }, ZoneType.Residential));
            Assert.That(r, Is.EqualTo(CommandFailure.NoRoadAccess));
            Assert.That(w.City.GetZone(C(1, 11)), Is.EqualTo(ZoneType.None));
        }

        [Test]
        public void CreateZone_Repaint_IsAllowed_AndFree()
        {
            var w = Street();
            w.Zone(ZoneType.Residential, 1, 11);
            Assert.That(Run(w, new CreateZoneCommand(new[] { C(1, 11) }, ZoneType.Industrial)), Is.EqualTo(CommandFailure.None));
            Assert.That(w.City.GetZone(C(1, 11)), Is.EqualTo(ZoneType.Industrial));
            Assert.That(w.City.Budget, Is.EqualTo(50000L));
        }

        [Test]
        public void CreateZone_OnRoad_Occupied() =>
            Assert.That(Run(Street(), new CreateZoneCommand(new[] { C(1, 10) }, ZoneType.Commercial)), Is.EqualTo(CommandFailure.CellOccupied));

        [Test]
        public void RemoveZone_WithBuilding_Occupied()
        {
            var w = Street();
            w.Place("residential_low_01", 1, 11);
            Assert.That(Run(w, new RemoveZoneCommand(new[] { C(1, 11) })), Is.EqualTo(CommandFailure.CellOccupied));
        }

        [Test]
        public void RemoveZone_Unzoned_NothingToDo() =>
            Assert.That(Run(Street(), new RemoveZoneCommand(new[] { C(1, 11) })), Is.EqualTo(CommandFailure.NothingToDo));

        [Test]
        public void Construct_ChargesOnce()
        {
            var w = Street();
            Assert.That(Run(w, new ConstructBuildingCommand("power_plant_01", C(1, 11))), Is.EqualTo(CommandFailure.None));
            Assert.That(w.City.Budget, Is.EqualTo(45000L));
            Assert.That(w.City.Buildings.Count, Is.EqualTo(1));
        }

        [TestCase("nope", 1, 11, CommandFailure.UnknownDefinition)]
        [TestCase("residential_low_01", 1, 11, CommandFailure.NotPlayerPlaceable)]
        [TestCase("power_plant_01", 1, 10, CommandFailure.CellOccupied)]
        [TestCase("power_plant_01", 1, 15, CommandFailure.NoRoadAccess)]
        [TestCase("power_plant_01", 99, 1, CommandFailure.OutOfBounds)]
        public void Construct_Refusals(string id, int x, int y, CommandFailure expected) =>
            Assert.That(Run(Street(), new ConstructBuildingCommand(id, C(x, y))), Is.EqualTo(expected));

        [Test]
        public void Construct_NoFunds_ChangesNothing()
        {
            var w = Street();
            w.City.Budget = 4999;
            Assert.That(Run(w, new ConstructBuildingCommand("power_plant_01", C(1, 11))), Is.EqualTo(CommandFailure.InsufficientFunds));
            Assert.That(w.City.Buildings.Count, Is.EqualTo(0));
        }

        [Test]
        public void RemoveBuilding_KeepsZone_ForRegrowth()
        {
            var w = Street();
            var b = w.Place("residential_low_01", 1, 11);
            Assert.That(Run(w, new RemoveBuildingCommand(b.Id)), Is.EqualTo(CommandFailure.None));
            Assert.That(w.City.GetBuilding(C(1, 11)), Is.Null);
            Assert.That(w.City.GetZone(C(1, 11)), Is.EqualTo(ZoneType.Residential));
        }

        [Test]
        public void RemoveBuilding_Unknown() =>
            Assert.That(Run(Street(), new RemoveBuildingCommand(77)), Is.EqualTo(CommandFailure.UnknownBuilding));

        [TestCase(0, CommandFailure.None)]
        [TestCase(30, CommandFailure.None)]
        [TestCase(31, CommandFailure.InvalidTaxRate)]
        [TestCase(-1, CommandFailure.InvalidTaxRate)]
        public void SetTaxRate_Range(int rate, CommandFailure expected) =>
            Assert.That(Run(Street(), new SetTaxRateCommand(rate)), Is.EqualTo(expected));

        [Test]
        public void BuildRoad_RaisesEachEventOnce()
        {
            var w = Street();
            int budget = 0, roads = 0;
            w.Ctx.Events.BudgetChanged += _ => budget++;
            w.Ctx.Events.RoadsChanged += () => roads++;
            new BuildRoadCommand(C(6, 10), C(8, 10)).Execute(w.Ctx);
            Assert.That(budget, Is.EqualTo(1));
            Assert.That(roads, Is.EqualTo(1));
        }
    }
}

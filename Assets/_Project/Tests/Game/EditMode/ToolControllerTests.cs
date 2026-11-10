using Metropolis.Core;
using NUnit.Framework;

namespace Metropolis.Game.Tests
{
    public class ToolControllerTests
    {
        static Cell C(int x, int y) => new Cell(x, y);

        Simulation _sim;
        ToolController _tools;

        [SetUp]
        public void Create()
        {
            var config = new SimulationConfig();
            _sim = new Simulation(CityState.CreateNew("T", config), BuildingCatalog.Default(), config);
            _tools = new ToolController(_sim);
        }

        void Drag(Cell from, Cell to)
        {
            _tools.PointerDown(from);
            _tools.PointerMove(to);
            _tools.PointerUp(to);
        }

        void Street()
        {
            _tools.SelectTool(BuildTool.Road);
            Drag(C(1, 50), C(10, 50));
        }

        [Test]
        public void Road_SnapsToAxis_AndChargesNewCells()
        {
            _tools.SelectTool(BuildTool.Road);
            Drag(C(1, 50), C(5, 52));
            Assert.That(_sim.City.HasRoad(C(5, 50)), Is.True);
            Assert.That(_sim.City.HasRoad(C(5, 52)), Is.False);
            Assert.That(_sim.City.Budget, Is.EqualTo(49950L));
        }

        [Test]
        public void Road_OverZone_PreviewsRed_AndIsRefused()
        {
            Street();
            _sim.City.SetZone(C(12, 50), ZoneType.Residential);
            var budget = _sim.City.Budget;
            _tools.PointerDown(C(11, 50));
            _tools.PointerMove(C(14, 50));
            Assert.That(_tools.PreviewValid, Is.Empty);
            Assert.That(_tools.PreviewInvalid.Count, Is.EqualTo(4));
            _tools.PointerUp(C(14, 50));
            Assert.That(_tools.LastFailure, Is.EqualTo(CommandFailure.CellOccupied));
            Assert.That(_sim.City.Budget, Is.EqualTo(budget));
        }

        [Test]
        public void Zone_SendsOnlyValidCells()
        {
            Street();
            _tools.SelectTool(BuildTool.ZoneResidential);
            _tools.PointerDown(C(1, 51));
            _tools.PointerMove(C(1, 56));
            Assert.That(_tools.PreviewValid.Count, Is.EqualTo(2));
            Assert.That(_tools.PreviewInvalid.Count, Is.EqualTo(4));
            _tools.PointerUp(C(1, 56));
            Assert.That(_sim.City.GetZone(C(1, 52)), Is.EqualTo(ZoneType.Residential));
            Assert.That(_sim.City.GetZone(C(1, 53)), Is.EqualTo(ZoneType.None));
        }

        [Test]
        public void Zone_NoValidCell_SendsNothing_AndReportsWhy()
        {
            _tools.SelectTool(BuildTool.ZoneCommercial);
            Drag(C(40, 5), C(42, 7));
            Assert.That(_sim.City.GetZone(C(40, 5)), Is.EqualTo(ZoneType.None));
            Assert.That(_tools.LastFailure, Is.EqualTo(CommandFailure.NoRoadAccess));
        }

        [Test]
        public void Power_Click_ChargesOnce()
        {
            Street();
            var budget = _sim.City.Budget;
            _tools.SelectTool(BuildTool.PowerPlant);
            _tools.PointerDown(C(1, 51));
            _tools.PointerUp(C(1, 51));
            Assert.That(_sim.City.Buildings.Count, Is.EqualTo(1));
            Assert.That(_sim.City.Budget, Is.EqualTo(budget - 5000));
        }

        [Test]
        public void Utility_ClearsToolAfterBuilding_SoOneClickBuildsOne()
        {
            Street();
            _tools.SelectTool(BuildTool.PowerPlant);
            _tools.PointerDown(C(1, 51));
            _tools.PointerUp(C(1, 51));
            _tools.PointerDown(C(2, 51));
            Assert.That(_tools.Tool, Is.EqualTo(BuildTool.None));
            Assert.That(_sim.City.Buildings.Count, Is.EqualTo(1));
        }

        [Test]
        public void Utility_KeepsToolWhenRefused()
        {
            _tools.SelectTool(BuildTool.PowerPlant);
            _tools.PointerDown(C(40, 5));
            Assert.That(_tools.LastFailure, Is.EqualTo(CommandFailure.NoRoadAccess));
            Assert.That(_tools.Tool, Is.EqualTo(BuildTool.PowerPlant));
        }

        [Test]
        public void Bulldoze_PrefersBuilding_ThenRoad_ThenZone()
        {
            Street();
            _sim.City.SetZone(C(2, 51), ZoneType.Residential);
            _sim.City.AddBuilding("residential_low_01", C(2, 51));
            _tools.SelectTool(BuildTool.Bulldoze);
            _tools.PointerDown(C(2, 51));
            Assert.That(_sim.City.GetBuilding(C(2, 51)), Is.Null);
            Assert.That(_sim.City.GetZone(C(2, 51)), Is.EqualTo(ZoneType.Residential));
            _tools.PointerDown(C(2, 51));
            Assert.That(_sim.City.GetZone(C(2, 51)), Is.EqualTo(ZoneType.None));
            _tools.PointerDown(C(2, 50));
            Assert.That(_sim.City.HasRoad(C(2, 50)), Is.False);
        }

        [Test]
        public void Bulldoze_EmptyCell_IsNothingToDo()
        {
            _tools.SelectTool(BuildTool.Bulldoze);
            _tools.PointerDown(C(40, 5));
            Assert.That(_tools.LastFailure, Is.EqualTo(CommandFailure.NothingToDo));
        }

        [Test]
        public void Cancel_DropsDragFirst_ThenClearsTool()
        {
            _tools.SelectTool(BuildTool.Road);
            _tools.PointerDown(C(1, 50));
            _tools.PointerMove(C(5, 50));
            _tools.Cancel();
            Assert.That(_tools.Tool, Is.EqualTo(BuildTool.Road));
            _tools.PointerUp(C(5, 50));
            Assert.That(_sim.City.HasRoad(C(5, 50)), Is.False);
            _tools.Cancel();
            Assert.That(_tools.Tool, Is.EqualTo(BuildTool.None));
        }

        [Test]
        public void SelectTool_DuringDrag_DropsDrag()
        {
            _tools.SelectTool(BuildTool.Road);
            _tools.PointerDown(C(1, 50));
            _tools.SelectTool(BuildTool.Bulldoze);
            _tools.PointerUp(C(5, 50));
            Assert.That(_sim.City.HasRoad(C(1, 50)), Is.False);
        }

        [Test]
        public void ReleaseOutsideMap_SendsNothing()
        {
            _tools.SelectTool(BuildTool.Road);
            _tools.PointerDown(C(1, 50));
            _tools.PointerMove(C(5, 50));
            _tools.PointerMove(null);
            _tools.PointerUp(null);
            Assert.That(_sim.City.HasRoad(C(1, 50)), Is.False);
            Assert.That(_sim.City.Budget, Is.EqualTo(50000L));
        }

        [Test]
        public void Preview_RecomputesOnlyWhenCellChanges()
        {
            _tools.SelectTool(BuildTool.ZoneResidential);
            _tools.PointerDown(C(0, 0));
            _tools.PointerMove(C(99, 99));
            var version = _tools.PreviewVersion;
            _tools.PointerMove(C(99, 99));
            Assert.That(_tools.PreviewVersion, Is.EqualTo(version));
        }

        [Test]
        public void CancelDrag_DropsDrag_KeepsTool()
        {
            _tools.SelectTool(BuildTool.Road);
            _tools.PointerDown(C(1, 50));
            Assert.That(_tools.IsDragging, Is.True);
            _tools.CancelDrag();
            _tools.PointerUp(C(5, 50));
            Assert.That(_tools.Tool, Is.EqualTo(BuildTool.Road));
            Assert.That(_sim.City.HasRoad(C(5, 50)), Is.False);
        }

        [Test]
        public void CancelDrag_WithoutDrag_KeepsTool()
        {
            _tools.SelectTool(BuildTool.Road);
            _tools.CancelDrag();
            Assert.That(_tools.Tool, Is.EqualTo(BuildTool.Road));
        }

        [Test]
        public void NoTool_ClickDoesNothing()
        {
            _tools.PointerDown(C(1, 50));
            _tools.PointerUp(C(1, 50));
            Assert.That(_sim.City.HasRoad(C(1, 50)), Is.False);
        }
    }
}

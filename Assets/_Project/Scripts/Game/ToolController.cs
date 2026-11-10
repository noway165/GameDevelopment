using System.Collections.Generic;
using Metropolis.Core;

namespace Metropolis.Game
{
    public enum BuildTool
    {
        None,
        Road,
        Bulldoze,
        ZoneResidential,
        ZoneCommercial,
        ZoneIndustrial,
        PowerPlant,
        WaterPump,
    }

    /// <summary>Turns pointer actions into commands and works out the validity preview.</summary>
    public sealed class ToolController
    {
        public const string PowerPlantId = "power_plant_01";
        public const string WaterPumpId = "water_pump_01";

        readonly Simulation _simulation;
        readonly List<Cell> _valid = new List<Cell>();
        readonly List<Cell> _invalid = new List<Cell>();
        Cell? _dragStart;

        public ToolController(Simulation simulation)
        {
            _simulation = simulation;
        }

        public BuildTool Tool { get; private set; }
        public Cell? Hover { get; private set; }
        public CommandFailure LastFailure { get; private set; }
        public IReadOnlyList<Cell> PreviewValid => _valid;
        public IReadOnlyList<Cell> PreviewInvalid => _invalid;
        public int PreviewVersion { get; private set; }
        public bool IsDragging => _dragStart != null;

        public void SelectTool(BuildTool tool)
        {
            Tool = tool;
            _dragStart = null;
            LastFailure = CommandFailure.None;
            RecomputePreview();
        }

        public void PointerMove(Cell? cell)
        {
            if (Hover.Equals(cell))
                return;
            Hover = cell;
            RecomputePreview();
        }

        public void PointerDown(Cell? cell)
        {
            if (cell == null || Tool == BuildTool.None)
                return;

            Hover = cell;
            if (IsDragTool)
                _dragStart = cell;
            else
                RunClick(cell.Value);
            RecomputePreview();
        }

        public void PointerUp(Cell? cell)
        {
            if (_dragStart == null)
                return;

            var start = _dragStart.Value;
            _dragStart = null;
            if (cell != null)
                Commit(start, cell.Value);
            RecomputePreview();
        }

        /// <summary>Drops an active drag and keeps the tool, for when the mouse-up will never arrive.</summary>
        public void CancelDrag()
        {
            if (_dragStart == null)
                return;
            _dragStart = null;
            RecomputePreview();
        }

        public void Cancel()
        {
            if (_dragStart != null)
                _dragStart = null;
            else
                Tool = BuildTool.None;
            LastFailure = CommandFailure.None;
            RecomputePreview();
        }

        bool IsDragTool => Tool == BuildTool.Road || ZoneTypeOf(Tool) != ZoneType.None;

        void Commit(Cell start, Cell end)
        {
            if (Tool == BuildTool.Road)
            {
                Run(new BuildRoadCommand(start, ToolGeometry.SnapToAxis(start, end)));
                return;
            }

            var type = ZoneTypeOf(Tool);
            var valid = new List<Cell>();
            var firstFailure = CommandFailure.None;
            foreach (var cell in ToolGeometry.Rectangle(start, end))
            {
                var failure = _simulation.Validate(new CreateZoneCommand(new[] { cell }, type)).Failure;
                if (failure == CommandFailure.None)
                    valid.Add(cell);
                else if (firstFailure == CommandFailure.None)
                    firstFailure = failure;
            }

            if (valid.Count == 0)
                LastFailure = firstFailure;
            else
                Run(new CreateZoneCommand(valid, type));
        }

        void RunClick(Cell cell)
        {
            Run(ClickCommand(cell));

            // Utilities are costly: repeated clicks once bankrupted a playtest city.
            if (LastFailure == CommandFailure.None && Tool != BuildTool.Bulldoze)
                Tool = BuildTool.None;
        }

        void Run(ICommand command)
        {
            LastFailure = command == null
                ? CommandFailure.NothingToDo
                : _simulation.Execute(command).Failure;
        }

        ICommand ClickCommand(Cell cell)
        {
            switch (Tool)
            {
                case BuildTool.PowerPlant: return new ConstructBuildingCommand(PowerPlantId, cell);
                case BuildTool.WaterPump: return new ConstructBuildingCommand(WaterPumpId, cell);
                default: return BulldozeCommand(cell);
            }
        }

        ICommand BulldozeCommand(Cell cell)
        {
            var city = _simulation.City;
            var building = city.GetBuilding(cell);
            if (building != null)
                return new RemoveBuildingCommand(building.Id);
            if (city.HasRoad(cell))
                return new RemoveRoadCommand(cell, cell);
            if (city.GetZone(cell) != ZoneType.None)
                return new RemoveZoneCommand(new[] { cell });
            return null;
        }

        void RecomputePreview()
        {
            _valid.Clear();
            _invalid.Clear();
            PreviewVersion++;

            if (Hover == null || Tool == BuildTool.None)
                return;

            var hover = Hover.Value;
            if (Tool == BuildTool.Road)
            {
                var start = _dragStart ?? hover;
                var end = ToolGeometry.SnapToAxis(start, hover);
                var target = IsValid(new BuildRoadCommand(start, end)) ? _valid : _invalid;
                target.AddRange(RoadLine.Cells(start, end));
            }
            else if (IsDragTool)
            {
                var type = ZoneTypeOf(Tool);
                foreach (var cell in ToolGeometry.Rectangle(_dragStart ?? hover, hover))
                    (IsValid(new CreateZoneCommand(new[] { cell }, type)) ? _valid : _invalid).Add(cell);
            }
            else
            {
                (IsValid(ClickCommand(hover)) ? _valid : _invalid).Add(hover);
            }
        }

        bool IsValid(ICommand command) => command != null && _simulation.Validate(command).Succeeded;

        static ZoneType ZoneTypeOf(BuildTool tool)
        {
            switch (tool)
            {
                case BuildTool.ZoneResidential: return ZoneType.Residential;
                case BuildTool.ZoneCommercial: return ZoneType.Commercial;
                case BuildTool.ZoneIndustrial: return ZoneType.Industrial;
                default: return ZoneType.None;
            }
        }
    }
}

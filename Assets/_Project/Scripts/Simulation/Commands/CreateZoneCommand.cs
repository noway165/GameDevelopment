using System.Collections.Generic;

namespace Metropolis.Core
{
    public sealed class CreateZoneCommand : ICommand
    {
        readonly IReadOnlyList<Cell> _cells;
        readonly ZoneType _type;

        public CreateZoneCommand(IReadOnlyList<Cell> cells, ZoneType type)
        {
            _cells = cells;
            _type = type;
        }

        public CommandResult Execute(SimContext ctx)
        {
            if (_cells.Count == 0 || _type == ZoneType.None)
                return CommandResult.Fail(CommandFailure.NothingToDo);

            var city = ctx.City;
            foreach (var cell in _cells)
                if (!city.IsInBounds(cell))
                    return CommandResult.Fail(CommandFailure.OutOfBounds);
            foreach (var cell in _cells)
                if (city.HasRoad(cell) || city.GetBuilding(cell) != null)
                    return CommandResult.Fail(CommandFailure.CellOccupied);
            foreach (var cell in _cells)
                if (!ctx.Roads.HasRoadAccess(cell))
                    return CommandResult.Fail(CommandFailure.NoRoadAccess);

            foreach (var cell in _cells)
                city.SetZone(cell, _type);

            ctx.Events.RaiseZonesChanged();
            return CommandResult.Ok;
        }
    }
}

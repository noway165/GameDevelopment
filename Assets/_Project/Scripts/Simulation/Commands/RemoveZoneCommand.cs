using System.Collections.Generic;

namespace Metropolis.Core
{
    public sealed class RemoveZoneCommand : ICommand
    {
        readonly IReadOnlyList<Cell> _cells;

        public RemoveZoneCommand(IReadOnlyList<Cell> cells)
        {
            _cells = cells;
        }

        public CommandResult Validate(SimContext ctx)
        {
            if (_cells.Count == 0)
                return CommandResult.Fail(CommandFailure.NothingToDo);

            var city = ctx.City;
            foreach (var cell in _cells)
                if (!city.IsInBounds(cell))
                    return CommandResult.Fail(CommandFailure.OutOfBounds);
            foreach (var cell in _cells)
                if (city.GetBuilding(cell) != null)
                    return CommandResult.Fail(CommandFailure.CellOccupied);
            foreach (var cell in _cells)
                if (city.GetZone(cell) == ZoneType.None)
                    return CommandResult.Fail(CommandFailure.NothingToDo);

            return CommandResult.Ok;
        }

        public CommandResult Execute(SimContext ctx)
        {
            var result = Validate(ctx);
            if (!result.Succeeded)
                return result;

            foreach (var cell in _cells)
                ctx.City.SetZone(cell, ZoneType.None);

            ctx.Events.RaiseZonesChanged();
            return CommandResult.Ok;
        }
    }
}

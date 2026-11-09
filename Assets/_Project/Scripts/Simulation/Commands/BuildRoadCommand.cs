using System.Collections.Generic;

namespace Metropolis.Core
{
    public sealed class BuildRoadCommand : ICommand
    {
        readonly Cell _from;
        readonly Cell _to;

        public BuildRoadCommand(Cell from, Cell to)
        {
            _from = from;
            _to = to;
        }

        public CommandResult Execute(SimContext ctx)
        {
            if (!RoadLine.IsStraight(_from, _to))
                return CommandResult.Fail(CommandFailure.NotStraight);

            var city = ctx.City;
            // Endpoints first: expanding an unchecked line could allocate billions of cells.
            if (!city.IsInBounds(_from) || !city.IsInBounds(_to))
                return CommandResult.Fail(CommandFailure.OutOfBounds);

            var line = RoadLine.Cells(_from, _to);
            var newCells = new List<Cell>();
            foreach (var cell in line)
            {
                if (city.HasRoad(cell))
                    continue;
                if (city.GetZone(cell) != ZoneType.None || city.GetBuilding(cell) != null)
                    return CommandResult.Fail(CommandFailure.CellOccupied);
                newCells.Add(cell);
            }

            if (newCells.Count == 0)
                return CommandResult.Fail(CommandFailure.NothingToDo);

            long cost = (long)newCells.Count * ctx.Config.RoadCostPerCell;
            if (cost > city.Budget)
                return CommandResult.Fail(CommandFailure.InsufficientFunds);

            foreach (var cell in newCells)
                city.SetRoad(cell, true);
            ctx.Roads.Rebuild();
            city.Budget -= cost;

            ctx.Events.RaiseRoadsChanged();
            if (cost > 0)
                ctx.Events.RaiseBudgetChanged(city.Budget);
            return CommandResult.Ok;
        }
    }
}

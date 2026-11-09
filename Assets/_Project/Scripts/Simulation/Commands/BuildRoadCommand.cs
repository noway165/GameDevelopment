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

        public CommandResult Validate(SimContext ctx) => Check(ctx, out _, out _);

        public CommandResult Execute(SimContext ctx)
        {
            var result = Check(ctx, out var newCells, out long cost);
            if (!result.Succeeded)
                return result;

            var city = ctx.City;
            foreach (var cell in newCells)
                city.SetRoad(cell, true);
            ctx.Roads.Rebuild();
            city.Budget -= cost;

            ctx.Events.RaiseRoadsChanged();
            if (cost > 0)
                ctx.Events.RaiseBudgetChanged(city.Budget);
            return CommandResult.Ok;
        }

        CommandResult Check(SimContext ctx, out List<Cell> newCells, out long cost)
        {
            newCells = new List<Cell>();
            cost = 0;

            if (!RoadLine.IsStraight(_from, _to))
                return CommandResult.Fail(CommandFailure.NotStraight);

            var city = ctx.City;
            // Endpoints first: expanding an unchecked line could allocate billions of cells.
            if (!city.IsInBounds(_from) || !city.IsInBounds(_to))
                return CommandResult.Fail(CommandFailure.OutOfBounds);

            foreach (var cell in RoadLine.Cells(_from, _to))
            {
                if (city.HasRoad(cell))
                    continue;
                if (city.GetZone(cell) != ZoneType.None || city.GetBuilding(cell) != null)
                    return CommandResult.Fail(CommandFailure.CellOccupied);
                newCells.Add(cell);
            }

            if (newCells.Count == 0)
                return CommandResult.Fail(CommandFailure.NothingToDo);

            cost = (long)newCells.Count * ctx.Config.RoadCostPerCell;
            if (cost > city.Budget)
                return CommandResult.Fail(CommandFailure.InsufficientFunds);

            return CommandResult.Ok;
        }
    }
}

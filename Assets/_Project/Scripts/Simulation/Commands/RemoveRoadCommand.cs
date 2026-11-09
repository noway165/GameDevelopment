namespace Metropolis.Core
{
    public sealed class RemoveRoadCommand : ICommand
    {
        readonly Cell _from;
        readonly Cell _to;

        public RemoveRoadCommand(Cell from, Cell to)
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
            bool anyRoad = false;
            foreach (var cell in line)
            {
                if (cell.Equals(city.EntryRoad))
                    return CommandResult.Fail(CommandFailure.ProtectedRoad);
                anyRoad |= city.HasRoad(cell);
            }

            if (!anyRoad)
                return CommandResult.Fail(CommandFailure.NothingToDo);

            foreach (var cell in line)
                city.SetRoad(cell, false);
            ctx.Roads.Rebuild();

            ctx.Events.RaiseRoadsChanged();
            return CommandResult.Ok;
        }
    }
}

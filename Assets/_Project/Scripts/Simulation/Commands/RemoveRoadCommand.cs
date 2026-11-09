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

        public CommandResult Validate(SimContext ctx)
        {
            if (!RoadLine.IsStraight(_from, _to))
                return CommandResult.Fail(CommandFailure.NotStraight);

            var city = ctx.City;
            // Endpoints first: expanding an unchecked line could allocate billions of cells.
            if (!city.IsInBounds(_from) || !city.IsInBounds(_to))
                return CommandResult.Fail(CommandFailure.OutOfBounds);

            bool anyRoad = false;
            foreach (var cell in RoadLine.Cells(_from, _to))
            {
                if (cell.Equals(city.EntryRoad))
                    return CommandResult.Fail(CommandFailure.ProtectedRoad);
                anyRoad |= city.HasRoad(cell);
            }

            return anyRoad ? CommandResult.Ok : CommandResult.Fail(CommandFailure.NothingToDo);
        }

        public CommandResult Execute(SimContext ctx)
        {
            var result = Validate(ctx);
            if (!result.Succeeded)
                return result;

            foreach (var cell in RoadLine.Cells(_from, _to))
                ctx.City.SetRoad(cell, false);
            ctx.Roads.Rebuild();

            ctx.Events.RaiseRoadsChanged();
            return CommandResult.Ok;
        }
    }
}

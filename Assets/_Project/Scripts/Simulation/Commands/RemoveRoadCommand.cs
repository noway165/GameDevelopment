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
            var line = RoadLine.Cells(_from, _to);
            foreach (var cell in line)
                if (!city.IsInBounds(cell))
                    return CommandResult.Fail(CommandFailure.OutOfBounds);

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

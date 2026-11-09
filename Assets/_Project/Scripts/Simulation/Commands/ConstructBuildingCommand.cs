namespace Metropolis.Core
{
    public sealed class ConstructBuildingCommand : ICommand
    {
        readonly string _definitionId;
        readonly Cell _cell;

        public ConstructBuildingCommand(string definitionId, Cell cell)
        {
            _definitionId = definitionId;
            _cell = cell;
        }

        public CommandResult Execute(SimContext ctx)
        {
            if (!ctx.Catalog.TryGet(_definitionId, out var definition))
                return CommandResult.Fail(CommandFailure.UnknownDefinition);
            if (definition.Kind != BuildingKind.Power && definition.Kind != BuildingKind.Water)
                return CommandResult.Fail(CommandFailure.NotPlayerPlaceable);

            var city = ctx.City;
            if (!city.IsInBounds(_cell))
                return CommandResult.Fail(CommandFailure.OutOfBounds);
            if (city.HasRoad(_cell) || city.GetZone(_cell) != ZoneType.None || city.GetBuilding(_cell) != null)
                return CommandResult.Fail(CommandFailure.CellOccupied);
            if (!ctx.Roads.HasRoadAccess(_cell))
                return CommandResult.Fail(CommandFailure.NoRoadAccess);
            if (definition.ConstructionCost > city.Budget)
                return CommandResult.Fail(CommandFailure.InsufficientFunds);

            var building = city.AddBuilding(_definitionId, _cell);
            city.Budget -= definition.ConstructionCost;

            ctx.Events.RaiseBuildingAdded(building);
            if (definition.ConstructionCost > 0)
                ctx.Events.RaiseBudgetChanged(city.Budget);
            return CommandResult.Ok;
        }
    }
}

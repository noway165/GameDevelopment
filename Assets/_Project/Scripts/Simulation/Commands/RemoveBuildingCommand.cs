namespace Metropolis.Core
{
    public sealed class RemoveBuildingCommand : ICommand
    {
        readonly int _buildingId;

        public RemoveBuildingCommand(int buildingId)
        {
            _buildingId = buildingId;
        }

        public CommandResult Validate(SimContext ctx) =>
            ctx.City.FindBuilding(_buildingId) != null
                ? CommandResult.Ok
                : CommandResult.Fail(CommandFailure.UnknownBuilding);

        public CommandResult Execute(SimContext ctx)
        {
            var result = Validate(ctx);
            if (!result.Succeeded)
                return result;

            var building = ctx.City.FindBuilding(_buildingId);
            ctx.City.RemoveBuilding(building);
            ctx.Events.RaiseBuildingRemoved(building);
            return CommandResult.Ok;
        }
    }
}

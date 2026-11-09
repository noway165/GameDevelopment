namespace Metropolis.Core
{
    public sealed class RemoveBuildingCommand : ICommand
    {
        readonly int _buildingId;

        public RemoveBuildingCommand(int buildingId)
        {
            _buildingId = buildingId;
        }

        public CommandResult Execute(SimContext ctx)
        {
            var building = ctx.City.FindBuilding(_buildingId);
            if (building == null)
                return CommandResult.Fail(CommandFailure.UnknownBuilding);

            ctx.City.RemoveBuilding(building);
            ctx.Events.RaiseBuildingRemoved(building);
            return CommandResult.Ok;
        }
    }
}

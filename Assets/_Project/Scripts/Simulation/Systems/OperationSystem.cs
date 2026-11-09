namespace Metropolis.Core
{
    public static class OperationSystem
    {
        public static void Run(SimContext ctx, DayWork work)
        {
            foreach (var building in ctx.City.Buildings)
            {
                var kind = ctx.Catalog.Get(building.DefinitionId).Kind;
                var status = BuildingStatus.None;
                if (!ctx.Roads.HasRoadAccess(building.Cell))
                    status |= BuildingStatus.NoRoad;
                if (kind != BuildingKind.Power && kind != BuildingKind.Water)
                {
                    if (!work.PowerServed.Contains(building.Id)) status |= BuildingStatus.NoPower;
                    if (!work.WaterServed.Contains(building.Id)) status |= BuildingStatus.NoWater;
                }

                if (status != building.Status)
                {
                    building.Status = status;
                    work.StatusChanged.Add(building);
                }
            }
        }
    }
}

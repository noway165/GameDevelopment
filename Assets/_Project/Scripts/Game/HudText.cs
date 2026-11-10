using System.Collections.Generic;
using System.Globalization;
using Metropolis.Core;

namespace Metropolis.Game
{
    /// <summary>Turns simulation values into the words and numbers the interface shows.</summary>
    public static class HudText
    {
        public static string Money(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

        public static string Signed(long value) => value > 0 ? "+" + Money(value) : Money(value);

        public static string Failure(CommandFailure failure)
        {
            switch (failure)
            {
                case CommandFailure.OutOfBounds: return "That is outside the map.";
                case CommandFailure.NotStraight: return "Roads must be straight.";
                case CommandFailure.CellOccupied: return "Something is already there.";
                case CommandFailure.NoRoadAccess: return "Too far from a connected road.";
                case CommandFailure.InsufficientFunds: return "Not enough money.";
                case CommandFailure.ProtectedRoad: return "The entry road cannot be removed.";
                case CommandFailure.NothingToDo: return "Nothing to do there.";
                case CommandFailure.UnknownDefinition: return "Unknown building.";
                case CommandFailure.NotPlayerPlaceable: return "That building grows on zones.";
                case CommandFailure.UnknownBuilding: return "That building no longer exists.";
                case CommandFailure.InvalidTaxRate: return "Tax rate is out of range.";
                default: return "";
            }
        }

        public static string Status(BuildingStatus status)
        {
            if (status == BuildingStatus.None)
                return "Running";

            var reasons = new List<string>();
            if ((status & BuildingStatus.NoRoad) != 0) reasons.Add("no road access");
            if ((status & BuildingStatus.NoPower) != 0) reasons.Add("no power");
            if ((status & BuildingStatus.NoWater) != 0) reasons.Add("no water");
            return "Not running: " + string.Join(", ", reasons);
        }
    }
}

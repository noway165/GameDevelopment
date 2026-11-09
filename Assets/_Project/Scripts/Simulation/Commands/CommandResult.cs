using System.Collections.Generic;

namespace Metropolis.Core
{
    public enum CommandFailure
    {
        None,
        OutOfBounds,
        NotStraight,
        CellOccupied,
        NoRoadAccess,
        InsufficientFunds,
        ProtectedRoad,
        NothingToDo,
        UnknownDefinition,
        NotPlayerPlaceable,
        UnknownBuilding,
        InvalidTaxRate,
    }

    public readonly struct CommandResult
    {
        CommandResult(CommandFailure failure) { Failure = failure; }

        public CommandFailure Failure { get; }
        public bool Succeeded => Failure == CommandFailure.None;

        public static CommandResult Ok => new CommandResult(CommandFailure.None);
        public static CommandResult Fail(CommandFailure failure) => new CommandResult(failure);
    }

    public static class RoadLine
    {
        public static bool IsStraight(Cell from, Cell to) => from.X == to.X || from.Y == to.Y;

        public static List<Cell> Cells(Cell from, Cell to)
        {
            var cells = new List<Cell>();
            int steps = System.Math.Max(System.Math.Abs(to.X - from.X), System.Math.Abs(to.Y - from.Y));
            int dx = System.Math.Sign(to.X - from.X);
            int dy = System.Math.Sign(to.Y - from.Y);
            for (int i = 0; i <= steps; i++)
                cells.Add(new Cell(from.X + i * dx, from.Y + i * dy));
            return cells;
        }
    }
}

using System;
using System.Collections.Generic;
using Metropolis.Core;

namespace Metropolis.Game
{
    public static class ToolGeometry
    {
        public static Cell SnapToAxis(Cell start, Cell cursor) =>
            Math.Abs(cursor.X - start.X) >= Math.Abs(cursor.Y - start.Y)
                ? new Cell(cursor.X, start.Y)
                : new Cell(start.X, cursor.Y);

        public static List<Cell> Rectangle(Cell a, Cell b)
        {
            var cells = new List<Cell>();
            for (int y = Math.Min(a.Y, b.Y); y <= Math.Max(a.Y, b.Y); y++)
                for (int x = Math.Min(a.X, b.X); x <= Math.Max(a.X, b.X); x++)
                    cells.Add(new Cell(x, y));
            return cells;
        }
    }
}

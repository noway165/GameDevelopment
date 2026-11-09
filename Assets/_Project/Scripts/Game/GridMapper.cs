using Metropolis.Core;
using UnityEngine;

namespace Metropolis.Game
{
    /// <summary>Converts between world positions and grid cells. Cell.Y maps to world Z.</summary>
    public sealed class GridMapper
    {
        readonly float _cellSize;
        readonly int _width;
        readonly int _height;

        public GridMapper(float cellSize, int width, int height)
        {
            _cellSize = cellSize;
            _width = width;
            _height = height;
        }

        public bool TryGetCell(Vector3 world, out Cell cell)
        {
            int x = Mathf.FloorToInt(world.x / _cellSize);
            int y = Mathf.FloorToInt(world.z / _cellSize);
            cell = new Cell(x, y);
            return x >= 0 && y >= 0 && x < _width && y < _height;
        }

        public Vector3 CellCenter(Cell cell) =>
            new Vector3((cell.X + 0.5f) * _cellSize, 0f, (cell.Y + 0.5f) * _cellSize);
    }
}

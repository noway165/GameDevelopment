using System.Collections.Generic;

namespace Metropolis.Core
{
    /// <summary>Graph over road cells, with 4-way adjacency, rooted at the entry road.</summary>
    public sealed class RoadNetwork
    {
        readonly CityState _city;
        readonly int _accessRange;
        readonly bool[] _connected;

        public RoadNetwork(CityState city, int accessRange)
        {
            _city = city;
            _accessRange = accessRange;
            _connected = new bool[city.Width * city.Height];
        }

        public void Rebuild()
        {
            System.Array.Clear(_connected, 0, _connected.Length);
            var entry = _city.EntryRoad;
            if (!_city.IsInBounds(entry) || !_city.HasRoad(entry))
                return;

            // Iterative on purpose: a recursive fill overflows the stack on large networks.
            var queue = new Queue<Cell>();
            Visit(entry, queue);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                Visit(new Cell(c.X + 1, c.Y), queue);
                Visit(new Cell(c.X - 1, c.Y), queue);
                Visit(new Cell(c.X, c.Y + 1), queue);
                Visit(new Cell(c.X, c.Y - 1), queue);
            }
        }

        public bool IsConnected(Cell cell) => _city.IsInBounds(cell) && _connected[Index(cell)];

        public bool HasRoadAccess(Cell cell)
        {
            for (int dy = -_accessRange; dy <= _accessRange; dy++)
            {
                int reach = _accessRange - System.Math.Abs(dy);
                for (int dx = -reach; dx <= reach; dx++)
                    if (IsConnected(new Cell(cell.X + dx, cell.Y + dy)))
                        return true;
            }
            return false;
        }

        void Visit(Cell cell, Queue<Cell> queue)
        {
            if (!_city.IsInBounds(cell) || _connected[Index(cell)] || !_city.HasRoad(cell))
                return;
            _connected[Index(cell)] = true;
            queue.Enqueue(cell);
        }

        int Index(Cell cell) => cell.Y * _city.Width + cell.X;
    }
}

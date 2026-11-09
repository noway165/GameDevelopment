using Metropolis.Core;
using UnityEngine;

namespace Metropolis.Game
{
    /// <summary>Keeps one object per road cell.</summary>
    public sealed class RoadView
    {
        readonly CityState _city;
        readonly GridMapper _map;
        readonly GameObject _prefab;
        readonly Transform _parent;
        readonly GameObject[] _objects;

        public RoadView(Simulation simulation, GridMapper map, GameObject prefab, Transform parent)
        {
            _city = simulation.City;
            _map = map;
            _prefab = prefab;
            _parent = parent;
            _objects = new GameObject[_city.Width * _city.Height];
            simulation.Events.RoadsChanged += Sync;
            Sync();
        }

        public int Count { get; private set; }

        void Sync()
        {
            for (int y = 0; y < _city.Height; y++)
            {
                for (int x = 0; x < _city.Width; x++)
                {
                    var cell = new Cell(x, y);
                    int index = y * _city.Width + x;
                    bool hasRoad = _city.HasRoad(cell);
                    if (hasRoad && _objects[index] == null)
                    {
                        _objects[index] = Object.Instantiate(_prefab, _map.CellCenter(cell), Quaternion.identity, _parent);
                        Count++;
                    }
                    else if (!hasRoad && _objects[index] != null)
                    {
                        Object.Destroy(_objects[index]);
                        _objects[index] = null;
                        Count--;
                    }
                }
            }
        }
    }
}

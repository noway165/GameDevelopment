using Metropolis.Core;
using UnityEngine;

namespace Metropolis.Game
{
    /// <summary>Keeps one object per zoned cell, matching the zone type.</summary>
    public sealed class ZoneView
    {
        readonly CityState _city;
        readonly GridMapper _map;
        readonly GameObject _residential;
        readonly GameObject _commercial;
        readonly GameObject _industrial;
        readonly Transform _parent;
        readonly GameObject[] _objects;
        readonly ZoneType[] _shown;

        public ZoneView(Simulation simulation, GridMapper map, GameObject residential, GameObject commercial,
            GameObject industrial, Transform parent)
        {
            _city = simulation.City;
            _map = map;
            _residential = residential;
            _commercial = commercial;
            _industrial = industrial;
            _parent = parent;
            _objects = new GameObject[_city.Width * _city.Height];
            _shown = new ZoneType[_city.Width * _city.Height];
            simulation.Events.ZonesChanged += Sync;
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
                    var type = _city.GetZone(cell);
                    if (type == _shown[index])
                        continue;

                    if (_objects[index] != null)
                    {
                        Object.Destroy(_objects[index]);
                        _objects[index] = null;
                        Count--;
                    }

                    if (type != ZoneType.None)
                    {
                        _objects[index] = Object.Instantiate(PrefabFor(type), _map.CellCenter(cell), Quaternion.identity, _parent);
                        Count++;
                    }

                    _shown[index] = type;
                }
            }
        }

        GameObject PrefabFor(ZoneType type)
        {
            switch (type)
            {
                case ZoneType.Residential: return _residential;
                case ZoneType.Commercial: return _commercial;
                default: return _industrial;
            }
        }
    }
}

using System.Collections.Generic;
using Metropolis.Core;
using UnityEngine;

namespace Metropolis.Game
{
    /// <summary>Keeps one prefab instance per building and tints buildings that are not running.</summary>
    public sealed class BuildingView
    {
        readonly GridMapper _map;
        readonly IReadOnlyDictionary<string, GameObject> _prefabs;
        readonly Transform _parent;
        readonly Dictionary<int, GameObject> _objects = new Dictionary<int, GameObject>();
        readonly MaterialPropertyBlock _broken = new MaterialPropertyBlock();

        public BuildingView(Simulation simulation, GridMapper map, IReadOnlyDictionary<string, GameObject> prefabs, Transform parent)
        {
            _map = map;
            _prefabs = prefabs;
            _parent = parent;
            _broken.SetColor("_BaseColor", Color.red);

            simulation.Events.BuildingAdded += Add;
            simulation.Events.BuildingRemoved += Remove;
            simulation.Events.BuildingStatusChanged += Tint;

            foreach (var building in simulation.City.Buildings)
                Add(building);
        }

        public int Count => _objects.Count;

        public GameObject Find(int buildingId) => _objects[buildingId];

        void Add(Building building)
        {
            _objects[building.Id] = Object.Instantiate(_prefabs[building.DefinitionId],
                _map.CellCenter(building.Cell), Quaternion.identity, _parent);
            Tint(building);
        }

        void Remove(Building building)
        {
            Object.Destroy(_objects[building.Id]);
            _objects.Remove(building.Id);
        }

        void Tint(Building building)
        {
            foreach (var renderer in _objects[building.Id].GetComponentsInChildren<Renderer>())
                renderer.SetPropertyBlock(building.IsOperational ? null : _broken);
        }
    }
}

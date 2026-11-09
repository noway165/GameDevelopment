using System.Collections.Generic;
using System.Linq;
using Metropolis.Core;
using UnityEngine;

namespace Metropolis.Game
{
    /// <summary>Owns the running simulation for the GameWorld scene.</summary>
    public sealed class GameController : MonoBehaviour
    {
        const float CellSize = 8f;

        [SerializeField] Terrain terrain;
        [SerializeField] List<BuildingAsset> buildings = new List<BuildingAsset>();
        [SerializeField] GameObject roadPrefab;
        [SerializeField] GameObject residentialZonePrefab;
        [SerializeField] GameObject commercialZonePrefab;
        [SerializeField] GameObject industrialZonePrefab;
        [SerializeField] GameObject previewPrefab;

        PreviewView _previewView;

        public Terrain Terrain => terrain;
        public IReadOnlyList<BuildingAsset> Buildings => buildings;
        public GameObject RoadPrefab => roadPrefab;
        public GameObject ResidentialZonePrefab => residentialZonePrefab;
        public GameObject CommercialZonePrefab => commercialZonePrefab;
        public GameObject IndustrialZonePrefab => industrialZonePrefab;
        public GameObject PreviewPrefab => previewPrefab;

        public Simulation Simulation { get; private set; }
        public SimulationClock Clock { get; private set; }
        public GridMapper Map { get; private set; }
        public ToolController Tools { get; private set; }
        public RoadView RoadView { get; private set; }
        public ZoneView ZoneView { get; private set; }
        public BuildingView BuildingView { get; private set; }

        void Awake()
        {
            var problems = StartupCheck.Find(terrain, buildings, roadPrefab, residentialZonePrefab,
                commercialZonePrefab, industrialZonePrefab, previewPrefab);
            if (problems.Count > 0)
            {
                Debug.LogError("GameController: " + string.Join(" ", problems), this);
                enabled = false;
                return;
            }

            var config = new SimulationConfig();
            var catalog = new BuildingCatalog(buildings.Select(asset => asset.ToDefinition()));
            Simulation = new Simulation(CityState.CreateNew("New City", config), catalog, config);
            Clock = new SimulationClock(config);
            Map = new GridMapper(CellSize, config.GridWidth, config.GridHeight);
            Tools = new ToolController(Simulation);

            var views = new GameObject("Views").transform;
            views.SetParent(transform, false);
            RoadView = new RoadView(Simulation, Map, roadPrefab, views);
            ZoneView = new ZoneView(Simulation, Map, residentialZonePrefab, commercialZonePrefab, industrialZonePrefab, views);
            BuildingView = new BuildingView(Simulation, Map, buildings.ToDictionary(asset => asset.id, asset => asset.prefab), views);
            _previewView = new PreviewView(Tools, Map, previewPrefab, views);
        }

        void Update()
        {
            int days = Clock.Advance(Time.unscaledDeltaTime);
            for (int i = 0; i < days; i++)
                Simulation.AdvanceDay();
            _previewView.Refresh();
        }
    }
}

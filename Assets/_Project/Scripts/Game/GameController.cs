using System.Collections.Generic;
using System.IO;
using System.Linq;
using Metropolis.Core;
using UnityEngine;

namespace Metropolis.Game
{
    /// <summary>Owns the running city for the GameWorld scene, and starts, loads and saves it.</summary>
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

        SimulationConfig _config;
        BuildingCatalog _catalog;
        PreviewView _previewView;
        GameObject _views;

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

        /// <summary>Where the city is saved. Tests point this at a temporary path.</summary>
        public SaveService Saves { get; set; }

        /// <summary>True once the player has chosen a new city or continued one.</summary>
        public bool SessionActive { get; private set; }

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

            _config = new SimulationConfig();
            _catalog = new BuildingCatalog(buildings.Select(asset => asset.ToDefinition()));
            Clock = new SimulationClock(_config);
            Map = new GridMapper(CellSize, _config.GridWidth, _config.GridHeight);
            Saves = new SaveService(Path.Combine(Application.persistentDataPath, "city.json"));
            SetCity(CityState.CreateNew("New City", _config));
        }

        void Update()
        {
            int days = Clock.Advance(Time.unscaledDeltaTime);
            for (int i = 0; i < days; i++)
                Simulation.AdvanceDay();
            _previewView.Refresh();
        }

        public void StartNewCity()
        {
            SetCity(CityState.CreateNew("New City", _config));
            SessionActive = true;
        }

        public bool TryContinue(out IReadOnlyList<string> errors)
        {
            if (!Saves.TryLoad(_catalog, _config, out var city, out errors))
                return false;

            SetCity(city);
            SessionActive = true;
            return true;
        }

        public bool TrySave(out string error)
        {
            if (!SessionActive)
            {
                // Without this, quitting from the main menu would replace a real save with an empty city.
                error = "No city in progress.";
                return false;
            }

            return Saves.TrySave(Simulation.City, out error);
        }

        void SetCity(CityState city)
        {
            // Destroying the parent removes every object of the previous city's views.
            if (_views != null)
                Destroy(_views);

            Simulation = new Simulation(city, _catalog, _config);
            Tools = new ToolController(Simulation);

            _views = new GameObject("Views");
            var parent = _views.transform;
            parent.SetParent(transform, false);
            RoadView = new RoadView(Simulation, Map, roadPrefab, parent);
            ZoneView = new ZoneView(Simulation, Map, residentialZonePrefab, commercialZonePrefab, industrialZonePrefab, parent);
            BuildingView = new BuildingView(Simulation, Map, buildings.ToDictionary(asset => asset.id, asset => asset.prefab), parent);
            _previewView = new PreviewView(Tools, Map, previewPrefab, parent);
        }
    }
}

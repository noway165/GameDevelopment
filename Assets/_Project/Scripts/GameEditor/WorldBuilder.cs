using System;
using System.Collections.Generic;
using System.IO;
using Metropolis.Core;
using Metropolis.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Metropolis.GameEditor
{
    /// <summary>
    /// Generates the GameWorld scene, placeholder prefabs, materials and building assets.
    /// Running it again replaces them, so hand edits to those generated files are lost.
    /// </summary>
    public static class WorldBuilder
    {
        const string Root = "Assets/_Project/";
        const string ScenePath = Root + "Scenes/GameWorld.unity";
        const string TerrainLayerName = "Terrain";
        const float CellSize = 8f;
        const float MapSize = 800f;

        static readonly Dictionary<string, string> BuildingPrefabNames = new Dictionary<string, string>
        {
            ["residential_low_01"] = "ResidentialLow",
            ["commercial_small_01"] = "CommercialSmall",
            ["commercial_large_01"] = "CommercialLarge",
            ["industrial_small_01"] = "IndustrialSmall",
            ["industrial_large_01"] = "IndustrialLarge",
            ["power_plant_01"] = "PowerPlant",
            ["water_pump_01"] = "WaterPump",
        };

        [MenuItem("Metropolis/Build World")]
        public static void Build() => BuildUnlessCancelled(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo);

        public static void BuildUnlessCancelled(Func<bool> saveOpenScenes)
        {
            // Building replaces the open scene, so unsaved work must be saved or the build abandoned.
            if (!saveOpenScenes())
                return;

            foreach (var folder in new[] { "Settings", "Scenes", "Art/Materials", "Art/Terrain",
                "Prefabs/Roads", "Prefabs/Zones", "Prefabs/Buildings", "ScriptableObjects/Buildings" })
                Directory.CreateDirectory(Root + folder);
            AssetDatabase.Refresh();

            // Must come first: opening a scene unloads assets that were created before it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetUpRenderPipeline();
            int terrainLayer = EnsureLayer(TerrainLayerName);

            var road = FlatPrefab("Roads/Road", Mat("Road", new Color(0.22f, 0.22f, 0.24f)), 0.02f);
            var residential = FlatPrefab("Zones/ZoneResidential", Mat("ZoneResidential", new Color(0.62f, 0.85f, 0.58f)), 0.01f);
            var commercial = FlatPrefab("Zones/ZoneCommercial", Mat("ZoneCommercial", new Color(0.58f, 0.74f, 0.92f)), 0.01f);
            var industrial = FlatPrefab("Zones/ZoneIndustrial", Mat("ZoneIndustrial", new Color(0.93f, 0.87f, 0.55f)), 0.01f);
            var preview = FlatPrefab("Roads/Preview", Mat("Preview", Color.white), 0.1f);

            var buildingPrefabs = new Dictionary<string, GameObject>
            {
                ["ResidentialLow"] = Block("ResidentialLow", new Vector3(6, 6, 6), new Color(0.25f, 0.62f, 0.30f)),
                ["CommercialSmall"] = Block("CommercialSmall", new Vector3(6, 8, 6), new Color(0.25f, 0.45f, 0.85f)),
                ["CommercialLarge"] = Block("CommercialLarge", new Vector3(7, 12, 7), new Color(0.12f, 0.22f, 0.55f)),
                ["IndustrialSmall"] = Block("IndustrialSmall", new Vector3(7, 6, 7), new Color(0.90f, 0.78f, 0.20f)),
                ["IndustrialLarge"] = Block("IndustrialLarge", new Vector3(7.5f, 9, 7.5f), new Color(0.90f, 0.50f, 0.15f)),
                ["PowerPlant"] = Block("PowerPlant", new Vector3(7, 10, 7), new Color(0.55f, 0.25f, 0.18f)),
                ["WaterPump"] = Block("WaterPump", new Vector3(6, 5, 6), new Color(0.20f, 0.75f, 0.80f)),
            };

            var assets = CreateBuildingAssets(buildingPrefabs);

            var terrain = CreateTerrain(terrainLayer);
            var rig = CreateCameraRig(out var view);
            CreateSun();
            var controller = WireController(terrain, assets, road, residential, commercial, industrial, preview);
            WireInput(controller, rig, view, terrainLayer, CreateInterface(controller));

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("WorldBuilder: finished.");
        }

        static void SetUpRenderPipeline()
        {
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, Root + "Settings/MetropolisRenderer.asset");
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, Root + "Settings/MetropolisURP.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
        }

        static int EnsureLayer(string name)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            int free = -1;
            for (int i = 8; i < layers.arraySize; i++)
            {
                var value = layers.GetArrayElementAtIndex(i).stringValue;
                if (value == name)
                    return i;
                if (free < 0 && string.IsNullOrEmpty(value))
                    free = i;
            }

            if (free < 0)
                throw new InvalidOperationException("No free layer slot for " + name + ".");
            layers.GetArrayElementAtIndex(free).stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            return free;
        }

        static Material Mat(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP Lit shader was not found.");
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, Root + "Art/Materials/" + name + ".mat");
            return material;
        }

        static GameObject FlatPrefab(string path, Material material, float height)
        {
            var child = Child(PrimitiveType.Quad, material, new Vector3(0, height, 0), new Vector3(CellSize, CellSize, 1));
            child.transform.localRotation = Quaternion.Euler(90, 0, 0);
            child.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return SavePrefab(path, child);
        }

        static GameObject Block(string name, Vector3 size, Color color)
        {
            var child = Child(PrimitiveType.Cube, Mat(name, color), new Vector3(0, size.y / 2, 0), size);
            return SavePrefab("Buildings/" + name, child);
        }

        static GameObject Child(PrimitiveType type, Material material, Vector3 position, Vector3 scale)
        {
            var child = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(child.GetComponent<Collider>());
            child.transform.localPosition = position;
            child.transform.localScale = scale;
            child.GetComponent<Renderer>().sharedMaterial = material;
            return child;
        }

        static GameObject SavePrefab(string path, GameObject child)
        {
            var root = new GameObject(Path.GetFileName(path));
            child.transform.SetParent(root.transform, false);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "Prefabs/" + path + ".prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static List<BuildingAsset> CreateBuildingAssets(Dictionary<string, GameObject> prefabs)
        {
            var catalog = BuildingCatalog.Default();
            var assets = new List<BuildingAsset>();

            // Kind order matches the catalog's definition order, which selects growth variants.
            foreach (BuildingKind kind in Enum.GetValues(typeof(BuildingKind)))
            {
                foreach (var definition in catalog.Variants(kind))
                {
                    var asset = ScriptableObject.CreateInstance<BuildingAsset>();
                    asset.id = definition.Id;
                    asset.kind = definition.Kind;
                    asset.constructionCost = definition.ConstructionCost;
                    asset.maintenance = definition.Maintenance;
                    asset.capacity = definition.Capacity;
                    asset.powerDemand = definition.PowerDemand;
                    asset.waterDemand = definition.WaterDemand;
                    asset.supply = definition.Supply;
                    asset.prefab = prefabs[BuildingPrefabNames[definition.Id]];
                    AssetDatabase.CreateAsset(asset, Root + "ScriptableObjects/Buildings/" + definition.Id + ".asset");
                    assets.Add(asset);
                }
            }

            return assets;
        }

        static Terrain CreateTerrain(int layer)
        {
            var data = new TerrainData { heightmapResolution = 513, size = new Vector3(MapSize, 100, MapSize) };
            AssetDatabase.CreateAsset(data, Root + "Art/Terrain/GameTerrain.asset");

            var material = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            AssetDatabase.CreateAsset(material, Root + "Art/Materials/Terrain.mat");

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.layer = layer;
            go.transform.position = Vector3.zero;
            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = material;
            return terrain;
        }

        static CameraRig CreateCameraRig(out Camera view)
        {
            var rig = new GameObject("CameraRig").AddComponent<CameraRig>();
            rig.transform.position = new Vector3(400, 150, 300);

            view = new GameObject("Main Camera").AddComponent<Camera>();
            view.gameObject.tag = "MainCamera";
            view.farClipPlane = 3000;
            view.transform.SetParent(rig.transform, false);
            view.transform.localRotation = Quaternion.Euler(55, 0, 0);
            return rig;
        }

        static void CreateSun()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        static void WireInput(GameController controller, CameraRig rig, Camera view, int terrainLayer, GameUi ui)
        {
            var input = new SerializedObject(controller.gameObject.AddComponent<InputBridge>());
            input.FindProperty("game").objectReferenceValue = controller;
            input.FindProperty("rig").objectReferenceValue = rig;
            input.FindProperty("view").objectReferenceValue = view;
            input.FindProperty("terrainMask").intValue = 1 << terrainLayer;
            input.FindProperty("ui").objectReferenceValue = ui;
            input.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameUi CreateInterface(GameController controller)
        {
            // The theme and its style sheet are hand-written source files, not generated ones.
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Root + "UI/MetropolisTheme.tss");
            if (theme == null)
                throw new InvalidOperationException("UI theme was not found at " + Root + "UI/MetropolisTheme.tss.");

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1600, 900);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            AssetDatabase.CreateAsset(panel, Root + "UI/MetropolisPanel.asset");

            var host = new GameObject("UI");
            host.AddComponent<UIDocument>().panelSettings = panel;
            var ui = host.AddComponent<GameUi>();
            var wiring = new SerializedObject(ui);
            wiring.FindProperty("game").objectReferenceValue = controller;
            wiring.ApplyModifiedPropertiesWithoutUndo();
            return ui;
        }

        static GameController WireController(Terrain terrain, List<BuildingAsset> assets, GameObject road,
            GameObject residential, GameObject commercial, GameObject industrial, GameObject preview)
        {
            var controller = new GameObject("Game").AddComponent<GameController>();
            var wiring = new SerializedObject(controller);
            wiring.FindProperty("terrain").objectReferenceValue = terrain;
            wiring.FindProperty("roadPrefab").objectReferenceValue = road;
            wiring.FindProperty("residentialZonePrefab").objectReferenceValue = residential;
            wiring.FindProperty("commercialZonePrefab").objectReferenceValue = commercial;
            wiring.FindProperty("industrialZonePrefab").objectReferenceValue = industrial;
            wiring.FindProperty("previewPrefab").objectReferenceValue = preview;

            var list = wiring.FindProperty("buildings");
            list.arraySize = assets.Count;
            for (int i = 0; i < assets.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = assets[i];

            wiring.ApplyModifiedPropertiesWithoutUndo();
            return controller;
        }
    }
}

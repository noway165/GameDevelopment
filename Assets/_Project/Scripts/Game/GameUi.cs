using System;
using System.Collections.Generic;
using Metropolis.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Metropolis.Game
{
    /// <summary>
    /// The whole interface: menus, top bar, toolbar and dialogs, built in code.
    /// Every button calls one public method here, and the class holds no game rules.
    /// </summary>
    public sealed class GameUi : MonoBehaviour
    {
        enum UiScreen { MainMenu, Playing, PauseMenu }

        const float NoticeSeconds = 3f;

        [SerializeField] GameController game;

        readonly Dictionary<BuildTool, Button> _toolButtons = new Dictionary<BuildTool, Button>();
        readonly Dictionary<SimulationSpeed, Button> _speedButtons = new Dictionary<SimulationSpeed, Button>();

        UiScreen _screen = UiScreen.MainMenu;
        SimulationSpeed _resumeSpeed = SimulationSpeed.X1;
        Action _dialogConfirm;
        bool _built;
        string _notice;
        float _noticeUntil;

        Label _day, _population, _budget, _income, _power, _water, _demand, _tax, _debt, _info;
        Label _menuTitle, _menuNote, _dialogText;
        VisualElement _menu, _dialog;
        Button _continue, _resume, _save, _dialogCancel;

        public VisualElement Root { get; private set; }
        public bool DialogOpen { get; private set; }
        public string DialogText => _built ? _dialogText.text : "";
        public bool MenuOpen => _built && (_screen != UiScreen.Playing || DialogOpen);

        void Start()
        {
            var document = GetComponent<UIDocument>();
            if (game == null || document == null || document.panelSettings == null)
            {
                Debug.LogError("GameUi: the interface is not wired. The game stays playable by hotkey.", this);
                enabled = false;
                return;
            }

            if (game.Simulation == null)
            {
                // GameController already reported why it could not start.
                enabled = false;
                return;
            }

            Root = document.rootVisualElement;
            Root.pickingMode = PickingMode.Ignore;
            Root.style.flexGrow = 1;
            BuildTree();
            _built = true;

            game.Clock.Speed = SimulationSpeed.Paused;
            ApplyScreen();
        }

        void Update()
        {
            if (!_built)
                return;

            var city = game.Simulation.City;
            var stats = city.Statistics;
            var tools = game.Tools;

            // shortcut: builds strings every frame, switch to change events if the profiler shows it.
            Set(_day, "Day " + city.Day);
            Set(_population, "Population " + HudText.Money(city.Population));
            Set(_budget, "Budget " + HudText.Money(city.Budget));
            Set(_income, "Income " + HudText.Signed(stats.Revenue - stats.Expenses) + "/day");
            Set(_power, "Power " + stats.PowerRemaining);
            Set(_water, "Water " + stats.WaterRemaining);
            Set(_demand, "Demand  R " + stats.ResidentialDemand + "  C " + stats.CommercialDemand + "  I " + stats.IndustrialDemand);
            Set(_tax, "Tax " + city.TaxRate + "%");

            bool inDebt = city.Budget < 0;
            _budget.EnableInClassList("debt", inDebt);
            Show(_debt, inDebt);

            foreach (var pair in _toolButtons)
                pair.Value.EnableInClassList("active", tools.Tool == pair.Key);
            foreach (var pair in _speedButtons)
                pair.Value.EnableInClassList("active", game.Clock.Speed == pair.Key);

            Show(_continue, _screen == UiScreen.MainMenu && game.Saves.Exists);

            var info = InfoText();
            Set(_info, info);
            Show(_info, info.Length > 0);
        }

        public void NewCity()
        {
            if (!_built)
                return;

            if (_screen == UiScreen.PauseMenu)
                ShowDialog("Start a new city? Unsaved progress will be lost.", StartNewCity);
            else if (game.Saves.Exists)
                ShowDialog("Start a new city? Your saved city will be replaced the next time you save.", StartNewCity);
            else
                StartNewCity();
        }

        public void Continue()
        {
            if (!_built)
                return;

            if (game.TryContinue(out var errors))
                EnterPlaying(SimulationSpeed.X1);
            else
                ShowDialog("The saved city could not be loaded.\n" + string.Join("\n", errors), null);
        }

        public void Save()
        {
            if (!_built || !game.SessionActive)
                return;

            if (game.TrySave(out var error))
            {
                _notice = "City saved.";
                _noticeUntil = Time.unscaledTime + NoticeSeconds;
                _menuNote.text = _notice;
            }
            else
            {
                ShowDialog("The city could not be saved.\n" + error, null);
            }
        }

        public void Quit()
        {
            if (!_built)
                return;

            if (game.SessionActive && !game.TrySave(out var error))
            {
                ShowDialog("The city could not be saved, so the game did not quit.\n" + error, null);
                return;
            }

            Application.Quit();
        }

        public void OpenPauseMenu()
        {
            if (!_built || !game.SessionActive || _screen != UiScreen.Playing)
                return;

            _resumeSpeed = game.Clock.Speed;
            game.Clock.Speed = SimulationSpeed.Paused;
            _screen = UiScreen.PauseMenu;
            _menuNote.text = "";
            ApplyScreen();
        }

        public void Resume()
        {
            if (_built && _screen == UiScreen.PauseMenu && !DialogOpen)
                EnterPlaying(_resumeSpeed);
        }

        public void ConfirmDialog()
        {
            var confirm = _dialogConfirm;
            DismissDialog();
            confirm?.Invoke();
        }

        public void DismissDialog()
        {
            if (!_built)
                return;

            _dialogConfirm = null;
            DialogOpen = false;
            Show(_dialog, false);
        }

        public void HandleEscape()
        {
            if (!_built)
                return;

            var tools = game.Tools;
            if (DialogOpen)
                DismissDialog();
            else if (_screen == UiScreen.PauseMenu)
                Resume();
            else if (_screen == UiScreen.MainMenu)
                return;
            else if (tools.IsDragging || tools.Tool != BuildTool.None)
                tools.Cancel();
            else
                OpenPauseMenu();
        }

        public void SetSpeed(SimulationSpeed speed)
        {
            if (_built && _screen == UiScreen.Playing && !DialogOpen)
                game.Clock.Speed = speed;
        }

        public void ChangeTax(int delta)
        {
            if (_built && _screen == UiScreen.Playing && !DialogOpen)
                game.Simulation.Execute(new SetTaxRateCommand(game.Simulation.City.TaxRate + delta));
        }

        public bool IsOverUi(Vector2 panelPosition) =>
            _built && Root.panel != null && Root.panel.Pick(panelPosition) != null;

        public bool IsScreenPointOverUi(Vector2 screenPosition)
        {
            if (!_built || Root.panel == null)
                return false;

            // Screen space starts at the bottom left, panel space at the top left.
            var flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return IsOverUi(RuntimePanelUtils.ScreenToPanel(Root.panel, flipped));
        }

        void StartNewCity()
        {
            game.StartNewCity();
            EnterPlaying(SimulationSpeed.X1);
        }

        void EnterPlaying(SimulationSpeed speed)
        {
            _screen = UiScreen.Playing;
            game.Clock.Speed = speed;
            ApplyScreen();
        }

        void ShowDialog(string text, Action confirm)
        {
            _dialogText.text = text;
            _dialogConfirm = confirm;
            Show(_dialogCancel, confirm != null);
            DialogOpen = true;
            Show(_dialog, true);
        }

        void ApplyScreen()
        {
            bool paused = _screen == UiScreen.PauseMenu;
            Show(_menu, _screen != UiScreen.Playing);
            _menuTitle.text = paused ? "Paused" : "METROPOLIS";
            Show(_resume, paused);
            Show(_save, paused);
            Show(_continue, _screen == UiScreen.MainMenu && game.Saves.Exists);
        }

        string InfoText()
        {
            if (_notice != null && Time.unscaledTime < _noticeUntil)
                return _notice;

            var tools = game.Tools;
            var failure = HudText.Failure(tools.LastFailure);
            if (failure.Length > 0)
                return failure;

            if (tools.Hover != null)
            {
                var building = game.Simulation.City.GetBuilding(tools.Hover.Value);
                if (building != null)
                {
                    var kind = game.Simulation.Context.Catalog.Get(building.DefinitionId).Kind;
                    return kind + ": " + HudText.Status(building.Status);
                }
            }

            return "";
        }

        void BuildTree()
        {
            Root.Add(BuildTopBar());

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.AddToClassList("spacer");
            Root.Add(spacer);

            _info = NewLabel("info-label", "info");
            Root.Add(_info);
            Root.Add(BuildToolbar());

            BuildMenu();
            BuildDialog();
        }

        VisualElement BuildTopBar()
        {
            var bar = new VisualElement { name = "top-bar" };
            bar.AddToClassList("bar");

            var left = Group(bar);
            _day = HudLabel(left, "day-label");
            _population = HudLabel(left, "population-label");
            _budget = HudLabel(left, "budget-label");
            _debt = HudLabel(left, "debt-label");
            _debt.text = "City is in debt";
            _debt.AddToClassList("debt");
            _income = HudLabel(left, "income-label");
            _power = HudLabel(left, "power-label");
            _water = HudLabel(left, "water-label");
            _demand = HudLabel(left, "demand-label");

            var right = Group(bar);
            SpeedButton(right, SimulationSpeed.Paused, "Pause");
            SpeedButton(right, SimulationSpeed.X1, "1x");
            SpeedButton(right, SimulationSpeed.X2, "2x");
            SpeedButton(right, SimulationSpeed.X4, "4x");
            HudButton(right, "tax-down", "-", () => ChangeTax(-1));
            _tax = HudLabel(right, "tax-label");
            HudButton(right, "tax-up", "+", () => ChangeTax(1));
            HudButton(right, "menu-button", "Menu", OpenPauseMenu);
            return bar;
        }

        VisualElement BuildToolbar()
        {
            var config = game.Simulation.Context.Config;
            var catalog = game.Simulation.Context.Catalog;

            var bar = new VisualElement { name = "toolbar" };
            bar.AddToClassList("toolbar");
            ToolButton(bar, BuildTool.Road, "1  Road\n" + config.RoadCostPerCell + " per cell");
            ToolButton(bar, BuildTool.Bulldoze, "2  Bulldoze\nfree");
            ToolButton(bar, BuildTool.ZoneResidential, "3  Residential\nfree");
            ToolButton(bar, BuildTool.ZoneCommercial, "4  Commercial\nfree");
            ToolButton(bar, BuildTool.ZoneIndustrial, "5  Industrial\nfree");
            ToolButton(bar, BuildTool.PowerPlant, "6  Power Plant\n" + Cost(catalog, ToolController.PowerPlantId));
            ToolButton(bar, BuildTool.WaterPump, "7  Water Pump\n" + Cost(catalog, ToolController.WaterPumpId));
            return bar;
        }

        void BuildMenu()
        {
            _menu = Overlay("menu");
            var panel = Panel(_menu);

            _menuTitle = NewLabel("menu-title", "title");
            panel.Add(_menuTitle);
            _continue = MenuButton(panel, "continue-button", "Continue", Continue);
            _resume = MenuButton(panel, "resume-button", "Resume", Resume);
            _save = MenuButton(panel, "save-button", "Save", Save);
            MenuButton(panel, "new-city-button", "New City", NewCity);
            MenuButton(panel, "quit-button", "Quit", Quit);
            _menuNote = NewLabel("menu-note", "note");
            panel.Add(_menuNote);
        }

        void BuildDialog()
        {
            _dialog = Overlay("dialog");
            var panel = Panel(_dialog);

            _dialogText = NewLabel("dialog-text", "dialog-text");
            panel.Add(_dialogText);

            var row = new VisualElement();
            row.AddToClassList("dialog-row");
            panel.Add(row);
            MenuButton(row, "dialog-ok", "OK", ConfirmDialog);
            _dialogCancel = MenuButton(row, "dialog-cancel", "Cancel", DismissDialog);
            Show(_dialog, false);
        }

        VisualElement Overlay(string name)
        {
            // Pickable on purpose: an open overlay blocks every click on the world behind it.
            var overlay = new VisualElement { name = name };
            overlay.AddToClassList("overlay");
            Root.Add(overlay);
            return overlay;
        }

        static VisualElement Panel(VisualElement overlay)
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");
            overlay.Add(panel);
            return panel;
        }

        static VisualElement Group(VisualElement bar)
        {
            var group = new VisualElement();
            group.AddToClassList("bar-group");
            bar.Add(group);
            return group;
        }

        static Label NewLabel(string name, string className)
        {
            var label = new Label { name = name };
            label.AddToClassList(className);
            return label;
        }

        static Label HudLabel(VisualElement parent, string name)
        {
            var label = NewLabel(name, "hud-label");
            parent.Add(label);
            return label;
        }

        static Button HudButton(VisualElement parent, string name, string text, Action onClick)
        {
            var button = new Button(onClick) { name = name, text = text };
            button.AddToClassList("hud-button");
            parent.Add(button);
            return button;
        }

        static Button MenuButton(VisualElement parent, string name, string text, Action onClick)
        {
            var button = HudButton(parent, name, text, onClick);
            button.AddToClassList("menu-button");
            return button;
        }

        void SpeedButton(VisualElement parent, SimulationSpeed speed, string text) =>
            _speedButtons[speed] = HudButton(parent, "speed-" + speed, text, () => SetSpeed(speed));

        void ToolButton(VisualElement parent, BuildTool tool, string text)
        {
            var button = HudButton(parent, "tool-" + tool, text, () => game.Tools.SelectTool(tool));
            button.AddToClassList("tool-button");
            _toolButtons[tool] = button;
        }

        static string Cost(BuildingCatalog catalog, string definitionId) =>
            catalog.TryGet(definitionId, out var definition) ? HudText.Money(definition.ConstructionCost) : "unavailable";

        static void Set(Label label, string text)
        {
            if (label.text != text)
                label.text = text;
        }

        static void Show(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}

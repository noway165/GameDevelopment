using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using Metropolis.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Metropolis.Game.PlayTests
{
    public class GameUiTests
    {
        static Cell C(int x, int y) => new Cell(x, y);
        static bool Shown(VisualElement e) => e.style.display != DisplayStyle.None;

        GameController _c;
        GameUi _ui;
        string _path;

        [UnitySetUp]
        public IEnumerator Load()
        {
            SceneManager.LoadScene("GameWorld");
            yield return null;
            yield return null;
            _c = Object.FindFirstObjectByType<GameController>();
            _path = Path.Combine(Application.temporaryCachePath, Path.GetRandomFileName(), "city.json");
            _c.Saves = new SaveService(_path);
            _ui = Object.FindFirstObjectByType<GameUi>();
            yield return null;
        }

        Label L(string name) => _ui.Root.Q<Label>(name);

        [Test]
        public void Launch_ShowsMainMenu_Paused_WithoutContinue()
        {
            Assert.That(_ui.MenuOpen, Is.True);
            Assert.That(_c.Clock.Speed, Is.EqualTo(SimulationSpeed.Paused));
            Assert.That(Shown(_ui.Root.Q<Button>("continue-button")), Is.False);
            Assert.That(Shown(_ui.Root.Q<Button>("resume-button")), Is.False);
        }

        [Test]
        public void NewCity_FromMainMenu_StartsPlaying()
        {
            _ui.NewCity();
            Assert.That(_ui.MenuOpen, Is.False);
            Assert.That(_c.SessionActive, Is.True);
            Assert.That(_c.Clock.Speed, Is.EqualTo(SimulationSpeed.X1));
        }

        [Test]
        public void PauseMenu_Pauses_AndResumesAtPreviousSpeed()
        {
            _ui.NewCity();
            _ui.SetSpeed(SimulationSpeed.X4);
            _ui.OpenPauseMenu();
            Assert.That(_c.Clock.Speed, Is.EqualTo(SimulationSpeed.Paused));
            Assert.That(_ui.MenuOpen, Is.True);
            _ui.Resume();
            Assert.That(_c.Clock.Speed, Is.EqualTo(SimulationSpeed.X4));
            Assert.That(_ui.MenuOpen, Is.False);
        }

        [Test]
        public void NewCity_FromPauseMenu_AsksFirst()
        {
            _ui.NewCity();
            _c.Simulation.Execute(new BuildRoadCommand(C(1, 50), C(5, 50)));
            _ui.OpenPauseMenu();
            _ui.NewCity();
            Assert.That(_ui.DialogOpen, Is.True);
            _ui.DismissDialog();
            Assert.That(_c.Simulation.City.HasRoad(C(5, 50)), Is.True);
            Assert.That(_ui.MenuOpen, Is.True);
            _ui.NewCity();
            _ui.ConfirmDialog();
            Assert.That(_c.Simulation.City.HasRoad(C(5, 50)), Is.False);
            Assert.That(_ui.MenuOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator Save_WritesFile_AndShowsNotice()
        {
            _ui.NewCity();
            _ui.Save();
            yield return null;
            Assert.That(File.Exists(_path), Is.True);
            Assert.That(L("info-label").text, Is.EqualTo("City saved."));
        }

        [UnityTest]
        public IEnumerator MainMenu_OffersContinue_WhenSaveExists()
        {
            _ui.NewCity();
            _ui.Save();
            SceneManager.LoadScene("GameWorld");
            yield return null;
            yield return null;
            var c = Object.FindFirstObjectByType<GameController>();
            c.Saves = new SaveService(_path);
            yield return null;
            var ui = Object.FindFirstObjectByType<GameUi>();
            Assert.That(Shown(ui.Root.Q<Button>("continue-button")), Is.True);
            ui.Continue();
            Assert.That(c.SessionActive, Is.True);
            Assert.That(ui.MenuOpen, Is.False);
        }

        [Test]
        public void Continue_CorruptSave_ShowsDialog_AndStaysOnMenu()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, "{ broken");
            _ui.Continue();
            Assert.That(_ui.DialogOpen, Is.True);
            Assert.That(_ui.DialogText, Is.Not.Empty);
            _ui.DismissDialog();
            Assert.That(_ui.MenuOpen, Is.True);
            Assert.That(_c.SessionActive, Is.False);
            Assert.That(File.ReadAllText(_path), Is.EqualTo("{ broken"));
        }

        [Test]
        public void Quit_FromPauseMenu_SavesFirst()
        {
            _ui.NewCity();
            _ui.OpenPauseMenu();
            _ui.Quit();
            Assert.That(File.Exists(_path), Is.True);
        }

        [Test]
        public void Quit_FromMainMenu_WritesNothing()
        {
            _ui.Quit();
            Assert.That(File.Exists(_path), Is.False);
        }

        [UnityTest]
        public IEnumerator Hud_ShowsBudget_AndDebtWarning()
        {
            _ui.NewCity();
            yield return null;
            Assert.That(L("budget-label").text, Does.Contain("50,000"));
            Assert.That(Shown(L("debt-label")), Is.False);
            _c.Simulation.City.Budget = -90;
            yield return null;
            Assert.That(L("budget-label").text, Does.Contain("-90"));
            Assert.That(Shown(L("debt-label")), Is.True);
            Assert.That(L("debt-label").text, Is.EqualTo("City is in debt"));
        }

        [UnityTest]
        public IEnumerator Hud_ShowsRefusal_InWords()
        {
            _ui.NewCity();
            _c.Tools.SelectTool(BuildTool.PowerPlant);
            _c.Tools.PointerDown(C(40, 5));
            yield return null;
            Assert.That(L("info-label").text, Is.EqualTo("Too far from a connected road."));
            Assert.That(_ui.Root.Q<Button>("tool-PowerPlant").ClassListContains("active"), Is.True);
        }

        [Test]
        public void HandleEscape_ClosesInOrder()
        {
            _ui.NewCity();
            _c.Tools.SelectTool(BuildTool.Road);
            _c.Tools.PointerDown(C(1, 50));
            _ui.HandleEscape();
            Assert.That(_c.Tools.IsDragging, Is.False);
            Assert.That(_c.Tools.Tool, Is.EqualTo(BuildTool.Road));
            _ui.HandleEscape();
            Assert.That(_c.Tools.Tool, Is.EqualTo(BuildTool.None));
            _ui.HandleEscape();
            Assert.That(_ui.MenuOpen, Is.True);
            _ui.HandleEscape();
            Assert.That(_ui.MenuOpen, Is.False);
        }

        [Test]
        public void HandleEscape_AtMainMenu_KeepsMenuOpen()
        {
            _ui.HandleEscape();
            Assert.That(_ui.MenuOpen, Is.True);
        }

        [Test]
        public void ChangeTax_StaysInRange()
        {
            _ui.NewCity();
            _ui.ChangeTax(1);
            Assert.That(_c.Simulation.City.TaxRate, Is.EqualTo(11));
            for (int i = 0; i < 40; i++)
                _ui.ChangeTax(1);
            Assert.That(_c.Simulation.City.TaxRate, Is.EqualTo(30));
            for (int i = 0; i < 40; i++)
                _ui.ChangeTax(-1);
            Assert.That(_c.Simulation.City.TaxRate, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Pointer_OverToolbarOrMenu_IsOverUi()
        {
            _ui.NewCity();
            yield return null;
            yield return null;
            var button = _ui.Root.Q<Button>("tool-Road");
            Assert.That(float.IsNaN(button.worldBound.width), Is.False);
            Assert.That(_ui.IsOverUi(button.worldBound.center), Is.True);
            Assert.That(_ui.IsOverUi(_ui.Root.worldBound.center), Is.False);
            _ui.OpenPauseMenu();
            yield return null;
            Assert.That(_ui.IsOverUi(_ui.Root.worldBound.center), Is.True);
        }

        [UnityTest]
        public IEnumerator UnwiredInterface_LogsOnce_AndDisablesItself()
        {
            LogAssert.Expect(LogType.Error, new Regex("GameUi"));
            var loose = new GameObject("loose").AddComponent<GameUi>();
            yield return null;
            yield return null;
            Assert.That(loose.enabled, Is.False);
            Assert.That(loose.MenuOpen, Is.False);
            Object.Destroy(loose.gameObject);
        }
    }
}

using UnityEngine;

namespace Metropolis.Game
{
    /// <summary>shortcut: temporary IMGUI panel, replace with the real HUD in sub-project 3.</summary>
    public sealed class DebugReadout : MonoBehaviour
    {
        [SerializeField] GameController game;

        void OnGUI()
        {
            if (game.Simulation == null)
                return;

            var city = game.Simulation.City;
            var stats = city.Statistics;
            var tools = game.Tools;

            GUILayout.BeginArea(new Rect(10, 10, 330, 200), GUI.skin.box);
            GUILayout.Label("Day " + city.Day + "   Speed " + game.Clock.Speed);
            GUILayout.Label("Population " + city.Population + "   Budget " + city.Budget);
            GUILayout.Label("Demand R " + stats.ResidentialDemand + "  C " + stats.CommercialDemand + "  I " + stats.IndustrialDemand);
            GUILayout.Label("Power left " + stats.PowerRemaining + "   Water left " + stats.WaterRemaining);
            GUILayout.Label("Tool " + tools.Tool);
            GUILayout.Label("Last result " + tools.LastFailure);

            if (tools.Hover != null)
            {
                var building = city.GetBuilding(tools.Hover.Value);
                if (building != null)
                    GUILayout.Label(building.DefinitionId + ": " + building.Status);
            }

            GUILayout.EndArea();
        }
    }
}

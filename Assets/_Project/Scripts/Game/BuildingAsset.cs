using Metropolis.Core;
using UnityEngine;

namespace Metropolis.Game
{
    [CreateAssetMenu(menuName = "Metropolis/Building")]
    public sealed class BuildingAsset : ScriptableObject
    {
        public string id;
        public BuildingKind kind;
        public int constructionCost;
        public int maintenance;
        public int capacity;
        public int powerDemand;
        public int waterDemand;
        public int supply;
        public GameObject prefab;

        public BuildingDefinition ToDefinition() =>
            new BuildingDefinition(id, kind, constructionCost, maintenance, capacity, powerDemand, waterDemand, supply);
    }
}

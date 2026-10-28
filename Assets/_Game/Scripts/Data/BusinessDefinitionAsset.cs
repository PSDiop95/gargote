using Gargote.Core;
using UnityEngine;

namespace Gargote.Data
{
    [CreateAssetMenu(menuName = "Gargote/Business Definition", fileName = "Business")]
    public sealed class BusinessDefinitionAsset : ScriptableObject
    {
        public string id;
        public string displayName;
        public double baseCost = 100;
        [Tooltip("Entre 1.07 et 1.15 (§4.1)")] public double growth = 1.07;
        public double baseRevenue = 1;
        public double baseCycleSeconds = 1;
        public double managerCost = 1000;
        public TimeAffinity affinity;

        public BusinessDefinition ToDefinition() => new BusinessDefinition
        {
            Id = id,
            BaseCost = baseCost,
            Growth = growth,
            BaseRevenue = baseRevenue,
            BaseCycleSeconds = baseCycleSeconds,
            ManagerCost = managerCost,
            Affinity = affinity,
        };
    }
}

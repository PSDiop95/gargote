using Gargote.Core;
using UnityEngine;

namespace Gargote.Data
{
    [CreateAssetMenu(menuName = "Gargote/Business Definition", fileName = "Business")]
    public sealed class BusinessDefinitionAsset : ScriptableObject
    {
        public string id;
        public string displayName;
        public double baseCost = 1250;
        [Tooltip("Environ 1.15 à 1.22 (§4.1)")] public double growth = 1.15;
        [Tooltip("Prix réel d'une vente (FCFA), jamais gonflé (§4.1)")] public double baseRevenue = 1000;
        [Tooltip("Temps de service d'un client par point de vente (s)")] public double baseCycleSeconds = 8;
        public double managerCost = 31000;
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

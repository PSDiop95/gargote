using System;
using Gargote.Core;
using UnityEngine;

namespace Gargote.Data
{
    [CreateAssetMenu(menuName = "Gargote/City Definition", fileName = "City")]
    public sealed class CityDefinitionAsset : ScriptableObject
    {
        [Serializable]
        public sealed class CashUpgradeEntry
        {
            public string id;
            [Tooltip("Nom affiché (à valider dans docs/CULTURE_REVIEW.md)")]
            public string displayName;
            [Tooltip("Commerce visé ; vide = tous les commerces de la ville")]
            public BusinessDefinitionAsset business;
            public double multiplier = 3;
            public double cost;
        }

        public string id;
        public string displayName;
        [Tooltip("Gains cumulés dans la ville pour débloquer la suivante")]
        public double goalLifetimeEarnings = 1e12;
        public double startingMoney;
        [Tooltip("Seuil d'étoiles propre à la ville (0 = seuil global du PrestigeConfig)")]
        public double starThreshold;
        public BusinessDefinitionAsset[] businesses = new BusinessDefinitionAsset[0];
        [Tooltip("Upgrades FCFA de la ville (§6), remis à zéro à chaque relance")]
        public CashUpgradeEntry[] upgrades = new CashUpgradeEntry[0];

        public CityDefinition ToDefinition()
        {
            var def = new CityDefinition
            {
                Id = id,
                GoalLifetimeEarnings = goalLifetimeEarnings,
                StartingMoney = startingMoney,
                StarThreshold = starThreshold,
                Businesses = new BusinessDefinition[businesses.Length],
                Upgrades = new CashUpgradeDefinition[upgrades.Length],
            };
            for (int i = 0; i < businesses.Length; i++) def.Businesses[i] = businesses[i].ToDefinition();
            for (int i = 0; i < upgrades.Length; i++)
            {
                var u = upgrades[i];
                def.Upgrades[i] = new CashUpgradeDefinition
                {
                    Id = u.id,
                    BusinessIndex = u.business == null ? -1 : Array.IndexOf(businesses, u.business),
                    RevenueMultiplier = u.multiplier,
                    Cost = u.cost,
                };
            }
            return def;
        }
    }
}

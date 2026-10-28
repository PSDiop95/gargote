using System;
using Gargote.Core;
using UnityEngine;

namespace Gargote.Data
{
    [CreateAssetMenu(menuName = "Gargote/Prestige Config", fileName = "PrestigeConfig")]
    public sealed class PrestigeConfigAsset : ScriptableObject
    {
        [Serializable]
        public sealed class StarUpgradeEntry
        {
            public string id;
            public string displayName;
            public StarUpgradeEffect effect;
            public long costStars;
        }

        [Tooltip("Étoiles = k × sqrt(gains_de_la_ville / threshold)")]
        public double k = 1;
        public double threshold = 1e9;
        public double bonusPerStar = 0.02;
        public long minStarsToPrestige = 1;

        [Header("Rendements décroissants")]
        [Tooltip("Au-delà de ce nombre d'étoiles effectives, elles comptent selon softCapExponent (0 = désactivé)")]
        public double softCapStars;
        public double softCapExponent = 0.5;
        [Tooltip("Part des étoiles des villes précédentes qui compte dans la ville en cours")]
        [Range(0, 1)] public double cityCarryOver = 1;
        [Tooltip("À l'arrivée dans une ville, le multiplicateur ne descend pas sous ce ratio de sa valeur avant le voyage")]
        [Range(0, 1)] public double travelMultiplierFloorRatio = 0.3;

        [Header("Upgrades d'étoiles (confort du prestige)")]
        public StarUpgradeEntry[] starUpgrades = new StarUpgradeEntry[0];
        [Tooltip("Avec « racheter comme avant » : fonds de départ = N secondes du revenu d'avant la relance")]
        public double rebuyStartingFundSeconds;

        public PrestigeConfig ToConfig()
        {
            var cfg = new PrestigeConfig
            {
                K = k,
                Threshold = threshold,
                BonusPerStar = bonusPerStar,
                MinStarsToPrestige = minStarsToPrestige,
                SoftCapStars = softCapStars,
                SoftCapExponent = softCapExponent,
                CityCarryOver = cityCarryOver,
                TravelMultiplierFloorRatio = travelMultiplierFloorRatio,
                RebuyStartingFundSeconds = rebuyStartingFundSeconds,
                StarUpgrades = new StarUpgradeDefinition[starUpgrades.Length],
            };
            for (int i = 0; i < starUpgrades.Length; i++)
                cfg.StarUpgrades[i] = new StarUpgradeDefinition
                {
                    Id = starUpgrades[i].id,
                    Effect = starUpgrades[i].effect,
                    CostStars = starUpgrades[i].costStars,
                };
            return cfg;
        }
    }
}

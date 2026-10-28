using Gargote.Core;
using UnityEngine;

namespace Gargote.Data
{
    [CreateAssetMenu(menuName = "Gargote/Prestige Config", fileName = "PrestigeConfig")]
    public sealed class PrestigeConfigAsset : ScriptableObject
    {
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

        public PrestigeConfig ToConfig() => new PrestigeConfig
        {
            K = k,
            Threshold = threshold,
            BonusPerStar = bonusPerStar,
            MinStarsToPrestige = minStarsToPrestige,
            SoftCapStars = softCapStars,
            SoftCapExponent = softCapExponent,
            CityCarryOver = cityCarryOver,
        };
    }
}

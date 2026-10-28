using Gargote.Core;
using UnityEngine;

namespace Gargote.Data
{
    [CreateAssetMenu(menuName = "Gargote/City Definition", fileName = "City")]
    public sealed class CityDefinitionAsset : ScriptableObject
    {
        public string id;
        public string displayName;
        [Tooltip("Gains cumulés dans la ville pour débloquer la suivante")]
        public double goalLifetimeEarnings = 1e12;
        public double startingMoney;
        [Tooltip("Seuil d'étoiles propre à la ville (0 = seuil global du PrestigeConfig)")]
        public double starThreshold;
        public BusinessDefinitionAsset[] businesses = new BusinessDefinitionAsset[0];

        public CityDefinition ToDefinition()
        {
            var def = new CityDefinition
            {
                Id = id,
                GoalLifetimeEarnings = goalLifetimeEarnings,
                StartingMoney = startingMoney,
                StarThreshold = starThreshold,
                Businesses = new BusinessDefinition[businesses.Length],
            };
            for (int i = 0; i < businesses.Length; i++) def.Businesses[i] = businesses[i].ToDefinition();
            return def;
        }
    }
}

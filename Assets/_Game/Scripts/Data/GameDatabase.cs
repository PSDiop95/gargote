using Gargote.Core;
using UnityEngine;

namespace Gargote.Data
{
    /// <summary>Racine des données d'équilibrage : villes dans l'ordre de déblocage + configs.</summary>
    [CreateAssetMenu(menuName = "Gargote/Game Database", fileName = "GameDatabase")]
    public sealed class GameDatabase : ScriptableObject
    {
        public CityDefinitionAsset[] cities = new CityDefinitionAsset[0];
        public EconomyConfigAsset economy;
        public PrestigeConfigAsset prestige;

        public GameDefinitions ToDefinitions()
        {
            var defs = new GameDefinitions
            {
                Cities = new CityDefinition[cities.Length],
                Economy = economy.ToConfig(),
                Prestige = prestige.ToConfig(),
            };
            for (int i = 0; i < cities.Length; i++) defs.Cities[i] = cities[i].ToDefinition();
            defs.Validate();
            return defs;
        }
    }
}

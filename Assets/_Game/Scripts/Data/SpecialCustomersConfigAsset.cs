using System;
using Gargote.Systems.Street;
using UnityEngine;

namespace Gargote.Data
{
    /// <summary>Équilibrage des clients spéciaux qui traversent la rue (bonus au tap). Surchargeable par Remote Config.</summary>
    [CreateAssetMenu(menuName = "Gargote/Special Customers Config", fileName = "SpecialCustomersConfig")]
    public sealed class SpecialCustomersConfigAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;
            [Tooltip("Clé de localisation du nom affiché.")] public string nameKey;
            public double weight = 1;
            [Tooltip("Bonus = revenu total/s × secondes.")] public double bonusIncomeSeconds = 60;
            public double minBonus = 100;
            public double crossingSeconds = 12;
            public Color color = Color.white;
        }

        public double minIntervalSeconds = 90;
        public double maxIntervalSeconds = 180;
        public int minUnlockedBusinesses = 2;
        public Entry[] customers = Array.Empty<Entry>();

        public SpecialCustomerRules ToRules()
        {
            var rules = new SpecialCustomerRules
            {
                MinIntervalSeconds = minIntervalSeconds,
                MaxIntervalSeconds = maxIntervalSeconds,
                MinUnlockedBusinesses = minUnlockedBusinesses,
                Customers = new SpecialCustomerDefinition[customers.Length],
            };
            for (int i = 0; i < customers.Length; i++)
            {
                var c = customers[i];
                rules.Customers[i] = new SpecialCustomerDefinition
                {
                    Id = c.id, Weight = c.weight, BonusIncomeSeconds = c.bonusIncomeSeconds,
                    MinBonus = c.minBonus, CrossingSeconds = c.crossingSeconds,
                };
            }
            rules.Validate();
            return rules;
        }
    }
}

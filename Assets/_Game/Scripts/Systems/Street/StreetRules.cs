using System;

namespace Gargote.Systems.Street
{
    /// <summary>Règles de foule (copie pure de CrowdConfig) : la foule reflète le revenu, sans représenter chaque cycle.</summary>
    public sealed class CrowdRules
    {
        /// <summary>Clients visibles maximum, toutes files confondues.</summary>
        public int MaxTotal = 20;
        /// <summary>Clients maximum dans la file d'un commerce.</summary>
        public int MaxPerBusiness = 6;
        /// <summary>Clients présents dès qu'un commerce est ouvert.</summary>
        public int MinPerUnlocked = 1;
        /// <summary>Clients ajoutés par décade de revenu/s au-dessus du revenu de base du commerce.</summary>
        public double CustomersPerDecade = 1.25;
        /// <summary>Durée minimale d'un service affiché : en dessous, le commerce passe en mode « rush ».</summary>
        public double MinVisualServiceSeconds = 0.6;
    }

    /// <summary>Règles de flux de billets (copie pure de JuiceConfig).</summary>
    public sealed class MoneyFlowRules
    {
        public int MinBills = 2;
        public int MaxBills = 5;
        /// <summary>Intervalle minimal entre deux envols de billets pour un même commerce ; au-delà, regroupement « xN ».</summary>
        public double MinBurstIntervalSeconds = 0.35;
        /// <summary>Intervalle minimal entre deux envols, tous commerces confondus.</summary>
        public double MinGlobalBurstIntervalSeconds = 0.08;
    }

    public sealed class SpecialCustomerDefinition
    {
        public string Id;
        /// <summary>Poids de tirage relatif.</summary>
        public double Weight = 1;
        /// <summary>Bonus = revenu total par seconde × ce nombre de secondes.</summary>
        public double BonusIncomeSeconds = 60;
        /// <summary>Bonus plancher en FCFA (début de partie).</summary>
        public double MinBonus = 5000;
        /// <summary>Temps de traversée de la rue (fenêtre de tap).</summary>
        public double CrossingSeconds = 12;
    }

    /// <summary>Règles des clients spéciaux (données d'équilibrage : SpecialCustomersConfig).</summary>
    public sealed class SpecialCustomerRules
    {
        public double MinIntervalSeconds = 90;
        public double MaxIntervalSeconds = 180;
        /// <summary>Aucun client spécial avant ce nombre de commerces ouverts.</summary>
        public int MinUnlockedBusinesses = 2;
        public SpecialCustomerDefinition[] Customers = Array.Empty<SpecialCustomerDefinition>();

        public void Validate()
        {
            if (MinIntervalSeconds <= 0 || MaxIntervalSeconds < MinIntervalSeconds)
                throw new ArgumentException("Special customers: invalid spawn interval.");
            foreach (var c in Customers)
            {
                if (string.IsNullOrEmpty(c.Id)) throw new ArgumentException("Special customer id is required.");
                if (c.Weight <= 0 || c.CrossingSeconds <= 0 || c.BonusIncomeSeconds < 0 || c.MinBonus < 0)
                    throw new ArgumentException(c.Id + ": invalid special customer values.");
            }
        }
    }
}

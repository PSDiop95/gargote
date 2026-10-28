using Gargote.Core;

namespace Gargote.Systems
{
    /// <summary>
    /// Compte les rewarded du jour (jour local fourni par l'appelant, ex. 20261028) et donne la valeur
    /// de la prochaine : pleine jusqu'au quota, décroissante ensuite.
    /// </summary>
    public sealed class RewardedTracker
    {
        private readonly RewardedConfig _config;

        public int DayKey { get; private set; }
        public int CountToday { get; private set; }

        public RewardedTracker(RewardedConfig config)
        {
            _config = config;
        }

        /// <summary>Valeur de la prochaine rewarded, sans la consommer (pour l'affichage du bouton).</summary>
        public double NextStrength(int dayKey) => RewardedRules.Strength(dayKey == DayKey ? CountToday : 0, _config);

        /// <summary>À appeler sur le callback de récompense uniquement. Renvoie la valeur accordée.</summary>
        public double Register(int dayKey)
        {
            if (dayKey != DayKey)
            {
                DayKey = dayKey;
                CountToday = 0;
            }
            double strength = RewardedRules.Strength(CountToday, _config);
            CountToday++;
            return strength;
        }

        internal void Restore(int dayKey, int countToday)
        {
            DayKey = dayKey;
            CountToday = countToday < 0 ? 0 : countToday;
        }
    }
}

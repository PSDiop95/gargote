using Gargote.Core;

namespace Gargote.Systems
{
    /// <summary>
    /// Étoiles Teranga : bonus permanent, réclamées en relançant la ville ou en changeant de ville.
    /// Les étoiles sont rattachées à la ville où elles ont été gagnées ; celles des villes précédentes
    /// ne comptent qu'en partie (<see cref="PrestigeConfig.CityCarryOver"/>).
    /// </summary>
    public sealed class PrestigeSystem
    {
        private readonly PrestigeConfig _config;

        public long TotalStars { get; private set; }
        /// <summary>Étoiles gagnées dans la ville en cours.</summary>
        public long CurrentCityStars { get; private set; }
        public long PreviousCitiesStars => TotalStars - CurrentCityStars;
        /// <summary>Étoiles déjà réclamées à partir des gains de la ville en cours.</summary>
        public long ClaimedForCurrentCity { get; private set; }
        public int PrestigeCount { get; private set; }
        /// <summary>Seuil d'étoiles de la ville en cours (fixé par <see cref="GameSession"/>).</summary>
        public double CityThreshold { get; internal set; }

        public PrestigeSystem(PrestigeConfig config)
        {
            _config = config;
            CityThreshold = config.Threshold;
        }

        public double EffectiveStars => PrestigeCalculator.EffectiveStars(CurrentCityStars, PreviousCitiesStars, _config);
        public double RevenueMultiplier => PrestigeCalculator.RevenueMultiplier(CurrentCityStars, PreviousCitiesStars, _config);

        public long StarsToGain(BigDouble cityLifetimeEarnings) =>
            PrestigeCalculator.StarsToGain(cityLifetimeEarnings, ClaimedForCurrentCity, _config, CityThreshold);

        public bool CanPrestige(BigDouble cityLifetimeEarnings) =>
            StarsToGain(cityLifetimeEarnings) >= System.Math.Max(1L, _config.MinStarsToPrestige);

        /// <summary>Multiplicateur après une relance de la ville, pour l'écran « +68 % sur tout ».</summary>
        public double MultiplierAfter(BigDouble cityLifetimeEarnings) =>
            PrestigeCalculator.RevenueMultiplier(CurrentCityStars + StarsToGain(cityLifetimeEarnings), PreviousCitiesStars, _config);

        internal long Claim(BigDouble cityLifetimeEarnings, bool movingToNewCity)
        {
            long gained = StarsToGain(cityLifetimeEarnings);
            TotalStars += gained;
            PrestigeCount++;
            if (movingToNewCity)
            {
                // Les étoiles de la ville quittée (dernière réclamation comprise) passent dans la reprise partielle.
                CurrentCityStars = 0;
                ClaimedForCurrentCity = 0;
            }
            else
            {
                CurrentCityStars += gained;
                ClaimedForCurrentCity += gained;
            }
            return gained;
        }

        internal void Restore(long totalStars, long currentCityStars, long claimedForCity, int prestigeCount)
        {
            TotalStars = System.Math.Max(0L, totalStars);
            CurrentCityStars = System.Math.Max(0L, System.Math.Min(currentCityStars, TotalStars));
            ClaimedForCurrentCity = System.Math.Max(0L, claimedForCity);
            PrestigeCount = System.Math.Max(0, prestigeCount);
        }
    }
}

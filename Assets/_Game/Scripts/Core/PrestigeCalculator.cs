using System;

namespace Gargote.Core
{
    /// <summary>Étoiles Teranga (§4.2) : K × sqrt(gains_totaux_de_la_ville / seuil).</summary>
    public static class PrestigeCalculator
    {
        public static long StarsFor(BigDouble cityLifetimeEarnings, PrestigeConfig config) =>
            StarsFor(cityLifetimeEarnings, config, config.Threshold);

        /// <param name="threshold">Seuil de la ville (voir <see cref="CityDefinition.StarThreshold"/>).</param>
        public static long StarsFor(BigDouble cityLifetimeEarnings, PrestigeConfig config, double threshold)
        {
            if (cityLifetimeEarnings <= BigDouble.Zero) return 0;
            if (threshold <= 0) threshold = config.Threshold;
            BigDouble stars = (cityLifetimeEarnings / threshold).Sqrt() * config.K;
            double d = stars.Floor().ToDouble();
            if (double.IsInfinity(d) || d > long.MaxValue / 2) return long.MaxValue / 2;
            return (long)d;
        }

        /// <summary>Étoiles encore à gagner dans cette ville (celles déjà réclamées sont déduites).</summary>
        public static long StarsToGain(BigDouble cityLifetimeEarnings, long alreadyClaimedForCity, PrestigeConfig config) =>
            StarsToGain(cityLifetimeEarnings, alreadyClaimedForCity, config, config.Threshold);

        public static long StarsToGain(BigDouble cityLifetimeEarnings, long alreadyClaimedForCity, PrestigeConfig config, double threshold) =>
            Math.Max(0L, StarsFor(cityLifetimeEarnings, config, threshold) - alreadyClaimedForCity);

        /// <summary>Étoiles effectives après rendements décroissants (plafond doux).</summary>
        public static double EffectiveStars(double stars, PrestigeConfig config)
        {
            if (stars <= 0) return 0d;
            double s0 = config.SoftCapStars;
            if (s0 <= 0d || stars <= s0) return stars;
            return s0 * Math.Pow(stars / s0, config.SoftCapExponent);
        }

        /// <summary>
        /// Étoiles qui comptent dans la ville en cours : celles de la ville + reprise partielle des villes précédentes,
        /// puis plafond doux.
        /// </summary>
        public static double EffectiveStars(long currentCityStars, long previousCitiesStars, PrestigeConfig config) =>
            EffectiveStars(currentCityStars + config.CityCarryOver * previousCitiesStars, config);

        public static double RevenueMultiplier(long totalStars, PrestigeConfig config) =>
            1d + EffectiveStars((double)totalStars, config) * config.BonusPerStar;

        public static double RevenueMultiplier(long currentCityStars, long previousCitiesStars, PrestigeConfig config) =>
            1d + EffectiveStars(currentCityStars, previousCitiesStars, config) * config.BonusPerStar;
    }

    /// <summary>Valeur d'une rewarded selon le nombre déjà vues aujourd'hui (1 = pleine valeur).</summary>
    public static class RewardedRules
    {
        public static double Strength(int alreadyWatchedToday, RewardedConfig config)
        {
            if (alreadyWatchedToday < config.FullValuePerDay) return 1d;
            return Math.Pow(config.DecayPerExtraAd, alreadyWatchedToday - config.FullValuePerDay + 1);
        }

        /// <summary>Multiplicateur hors ligne effectif : 1 + (x2 − 1) × valeur.</summary>
        public static double ScaledMultiplier(double fullMultiplier, double strength) =>
            1d + (fullMultiplier - 1d) * Math.Max(0d, strength);
    }
}

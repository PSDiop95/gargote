using System;

namespace Gargote.Core
{
    public readonly struct OfflineResult
    {
        public readonly double ElapsedSeconds;
        public readonly double CreditedSeconds;
        public readonly BigDouble Earnings;

        public OfflineResult(double elapsedSeconds, double creditedSeconds, BigDouble earnings)
        {
            ElapsedSeconds = elapsedSeconds;
            CreditedSeconds = creditedSeconds;
            Earnings = earnings;
        }

        public bool HasEarnings => !Earnings.IsZero;
    }

    /// <summary>Gains hors ligne (§4.3).</summary>
    public static class OfflineCalculator
    {
        /// <summary>
        /// Temps hors ligne fiable. Base : horloge UTC (insensible aux fuseaux horaires).
        /// Si l'horloge monotone (temps depuis le démarrage de l'appareil, sommeil compris) a continué
        /// depuis la sauvegarde, elle borne le temps UTC : avancer l'heure système ne rapporte rien.
        /// Une horloge UTC qui recule donne 0.
        /// </summary>
        /// <param name="lastMonotonicSeconds">Valeur monotone à la sauvegarde, négative si inconnue.</param>
        public static double ResolveElapsedSeconds(DateTime lastUtc, DateTime nowUtc,
            double lastMonotonicSeconds, double nowMonotonicSeconds)
        {
            double utcDelta = (nowUtc - lastUtc).TotalSeconds;
            if (utcDelta <= 0d) return 0d;

            bool sameBoot = lastMonotonicSeconds >= 0d && nowMonotonicSeconds >= lastMonotonicSeconds;
            if (!sameBoot) return utcDelta; // redémarrage : seule l'heure UTC est disponible

            double monotonicDelta = nowMonotonicSeconds - lastMonotonicSeconds;
            return Math.Min(utcDelta, monotonicDelta);
        }

        /// <summary>
        /// Gains pour un revenu géré par seconde (multiplicateurs permanents inclus, hors boost).
        /// Le boost actif s'applique à la portion de l'absence qu'il couvre.
        /// </summary>
        public static OfflineResult Compute(BigDouble managedIncomePerSecond, double elapsedSeconds, double capSeconds,
            double efficiency, double boostRemainingSeconds, double boostMultiplier) =>
            Compute(managedIncomePerSecond, elapsedSeconds, capSeconds, efficiency, boostRemainingSeconds, boostMultiplier, 0d, 1d);

        /// <summary>
        /// Variante à deux bonus temporisés (ex. boost rewarded + bonus de bienvenue), chacun couvrant le début de
        /// l'absence ; là où ils se chevauchent, leurs multiplicateurs se multiplient.
        /// </summary>
        public static OfflineResult Compute(BigDouble managedIncomePerSecond, double elapsedSeconds, double capSeconds,
            double efficiency, double boostARemaining, double boostAMultiplier, double boostBRemaining, double boostBMultiplier)
        {
            if (elapsedSeconds <= 0d || managedIncomePerSecond.IsZero || efficiency <= 0d)
                return new OfflineResult(Math.Max(0d, elapsedSeconds), 0d, BigDouble.Zero);

            double credited = Math.Min(elapsedSeconds, Math.Max(0d, capSeconds));
            double a = Math.Min(credited, Math.Max(0d, boostARemaining));
            double b = Math.Min(credited, Math.Max(0d, boostBRemaining));
            double both = Math.Min(a, b);
            double weightedSeconds = (credited - Math.Max(a, b))
                                     + both * boostAMultiplier * boostBMultiplier
                                     + (a - both) * boostAMultiplier
                                     + (b - both) * boostBMultiplier;
            var earnings = managedIncomePerSecond * (weightedSeconds * efficiency);
            return new OfflineResult(elapsedSeconds, credited, earnings);
        }
    }
}

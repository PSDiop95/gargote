using System;

namespace Gargote.Core
{
    /// <summary>Cycle jour/nuit du jeu (§3.3) : 24 h de jeu en <see cref="DayNightConfig.DayLengthSeconds"/> réelles.</summary>
    public static class DayNight
    {
        public static double HourOfDay(double gameSeconds, DayNightConfig config)
        {
            double len = config.DayLengthSeconds;
            double t = gameSeconds % len;
            if (t < 0) t += len;
            return t / len * 24d;
        }

        public static bool InWindow(double hour, double startHour, double endHour) =>
            startHour <= endHour ? hour >= startHour && hour < endHour : hour >= startHour || hour < endHour;

        public static double WindowLength(double startHour, double endHour) =>
            startHour <= endHour ? endHour - startHour : 24d - startHour + endHour;

        public static double Multiplier(TimeAffinity affinity, double hourOfDay, DayNightConfig config)
        {
            switch (affinity)
            {
                case TimeAffinity.Morning:
                    return InWindow(hourOfDay, config.MorningStartHour, config.MorningEndHour) ? config.AffinityMultiplier : 1d;
                case TimeAffinity.Night:
                    return InWindow(hourOfDay, config.NightStartHour, config.NightEndHour) ? config.AffinityMultiplier : 1d;
                default:
                    return 1d;
            }
        }

        /// <summary>Multiplicateur moyen sur une journée complète (utilisé pour les gains hors ligne).</summary>
        public static double AverageMultiplier(TimeAffinity affinity, DayNightConfig config)
        {
            double window;
            switch (affinity)
            {
                case TimeAffinity.Morning: window = WindowLength(config.MorningStartHour, config.MorningEndHour); break;
                case TimeAffinity.Night: window = WindowLength(config.NightStartHour, config.NightEndHour); break;
                default: return 1d;
            }
            return 1d + (config.AffinityMultiplier - 1d) * Math.Min(window, 24d) / 24d;
        }
    }
}

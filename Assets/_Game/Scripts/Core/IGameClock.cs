using System;

namespace Gargote.Core
{
    /// <summary>
    /// Source de temps injectée. L'implémentation mobile doit fournir une horloge monotone qui inclut
    /// le sommeil de l'appareil (Android : SystemClock.elapsedRealtime ; iOS : mach_continuous_time).
    /// </summary>
    public interface IGameClock
    {
        DateTime UtcNow { get; }
        /// <summary>Secondes depuis le démarrage de l'appareil, sommeil compris.</summary>
        double MonotonicSeconds { get; }
    }

    /// <summary>Horloge manipulable pour les tests et la simulation.</summary>
    public sealed class ManualClock : IGameClock
    {
        public DateTime UtcNow { get; set; }
        public double MonotonicSeconds { get; set; }

        public ManualClock(DateTime utcNow, double monotonicSeconds = 0d)
        {
            UtcNow = utcNow;
            MonotonicSeconds = monotonicSeconds;
        }

        public void Advance(double seconds)
        {
            UtcNow = UtcNow.AddSeconds(seconds);
            MonotonicSeconds += seconds;
        }
    }
}

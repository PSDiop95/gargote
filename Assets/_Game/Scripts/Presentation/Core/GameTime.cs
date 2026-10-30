using System;
using Gargote.Core;
using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>Horloge réelle : UTC + temps monotone (elapsedRealtime sur Android, survit à la mise en veille).</summary>
    public sealed class UnityGameClock : IGameClock
    {
        public DateTime UtcNow => DateTime.UtcNow;

        public double MonotonicSeconds
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using (var clock = new AndroidJavaClass("android.os.SystemClock"))
                        return clock.CallStatic<long>("elapsedRealtime") / 1000d;
                }
                catch (Exception)
                {
                    return -1d;
                }
#else
                // Repart de zéro à chaque lancement : traité comme un redémarrage (UTC seul, plafonné).
                return -1d;
#endif
            }
        }
    }

    /// <summary>
    /// Temps de jeu de la présentation. L'accélération (debug x10 / x100) multiplie le pas donné à l'économie :
    /// <see cref="Time.timeScale"/> n'est jamais modifié (pas de ralenti ni d'accéléré global, pas de hit-stop).
    /// </summary>
    public sealed class GameTime
    {
        private static readonly DateTime Epoch = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly IGameClock _clock;
        private readonly DayNightConfig _dayNight;
        private double _offsetSeconds;

        /// <summary>Facteur d'accélération (debug) ; 1 en jeu.</summary>
        public float Speed = 1f;
        /// <summary>Heure forcée (debug) ; négatif = suivre l'horloge.</summary>
        public double ForcedHour = -1d;
        /// <summary>Pas maximal donné à la simulation par sous-pas (précision des fins de cycle).</summary>
        public double MaxSubstep = 0.1d;
        /// <summary>Pas réel maximal par frame (une frame lente ne fait pas sauter l'économie).</summary>
        public double MaxFrameDelta = 0.25d;

        public GameTime(IGameClock clock, DayNightConfig dayNight)
        {
            _clock = clock;
            _dayNight = dayNight;
        }

        /// <summary>Temps de jeu écoulé depuis le lancement (accélération comprise).</summary>
        public double ElapsedGameSeconds { get; private set; }

        public double GameSeconds => (_clock.UtcNow - Epoch).TotalSeconds + _offsetSeconds;

        public double HourOfDay => ForcedHour >= 0d ? ForcedHour : DayNight.HourOfDay(GameSeconds, _dayNight);

        /// <summary>0..1 sur la journée (0 = minuit), pour les gradients visuels.</summary>
        public float DayFraction => (float)(HourOfDay / 24d);

        /// <summary>Convertit un pas réel en pas de jeu (accéléré) et fait avancer l'heure du jour en conséquence.</summary>
        public double Advance(double realDelta)
        {
            double dt = Math.Min(Math.Max(0d, realDelta), MaxFrameDelta) * Math.Max(0f, Speed);
            _offsetSeconds += dt - Math.Min(Math.Max(0d, realDelta), MaxFrameDelta);
            ElapsedGameSeconds += dt;
            return dt;
        }
    }
}

using System;

namespace Gargote.Core
{
    /// <summary>
    /// Source de temps unique du jeu (règles « par jour », minuteries, hors ligne) : horloge monotone + heure système.
    /// <para>Temps de confiance : une ligne de temps qui n'avance que de façon vérifiée — temps réel joué, plus les
    /// absences validées par <see cref="OfflineCalculator.ResolveElapsedSeconds"/> (bornées par l'horloge monotone,
    /// nulles si l'heure recule). Les minuteries (coffre gratuit, recharge des boosts) comptent sur cette ligne :
    /// avancer l'heure du téléphone ne les accélère pas.</para>
    /// <para>Jour local : la date de l'heure locale (fuseau courant). Un nouveau jour n'est accordé que si le temps de
    /// confiance a bien atteint le minuit prévu (moins une tolérance pour les changements de fuseau).</para>
    /// </summary>
    public sealed class GameClock
    {
        /// <summary>Recul toléré de l'horloge UTC (synchronisation réseau) avant de la considérer comme reculée.</summary>
        public const double RollbackToleranceSeconds = 300d;
        private static readonly DateTime Epoch = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        private readonly IGameClock _source;
        private readonly Func<TimeSpan> _utcOffset;

        /// <summary>Dernière heure UTC jugée fiable (sauvegardée).</summary>
        public DateTime LastTrustedUtc { get; private set; }
        /// <summary>Secondes de confiance cumulées depuis le début de la partie (sauvegardées).</summary>
        public double TrustedSeconds { get; private set; }

        public GameClock(IGameClock source, Func<TimeSpan> utcOffset = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _utcOffset = utcOffset ?? (() => TimeSpan.Zero);
            LastTrustedUtc = source.UtcNow;
        }

        public DateTime UtcNow => _source.UtcNow;
        public double MonotonicSeconds => _source.MonotonicSeconds;
        public TimeSpan UtcOffset => _utcOffset();
        public DateTime LocalNow => UtcNow + UtcOffset;
        public double LocalHour => LocalNow.TimeOfDay.TotalHours;

        /// <summary>Jour local (jours depuis le 01/01/2024).</summary>
        public int LocalDayKey => DayKey(LocalNow);
        /// <summary>Semaine locale (semaines depuis le lundi 01/01/2024).</summary>
        public int LocalWeekKey => LocalDayKey >= 0 ? LocalDayKey / 7 : (LocalDayKey - 6) / 7;

        public static int DayKey(DateTime local) => (int)Math.Floor((local.Date - Epoch).TotalDays);

        /// <summary>Secondes jusqu'au prochain minuit local.</summary>
        public double SecondsToLocalMidnight
        {
            get
            {
                var local = LocalNow;
                return (local.Date.AddDays(1) - local).TotalSeconds;
            }
        }

        /// <summary>L'heure UTC est-elle passée sous la dernière heure fiable (au-delà de la tolérance) ?</summary>
        public bool IsRolledBack => (LastTrustedUtc - UtcNow).TotalSeconds > RollbackToleranceSeconds;

        /// <summary>Temps réel de jeu (secondes non mises à l'échelle) : fait avancer la ligne de confiance.</summary>
        public void AdvancePlayed(double seconds)
        {
            if (seconds <= 0d || double.IsNaN(seconds)) return;
            TrustedSeconds += seconds;
            if (!IsRolledBack && UtcNow > LastTrustedUtc) LastTrustedUtc = UtcNow;
        }

        /// <summary>Absence validée (déjà bornée par l'horloge monotone ; 0 si l'heure a reculé).</summary>
        public void AdvanceOffline(double resolvedSeconds)
        {
            if (resolvedSeconds > 0d && !double.IsNaN(resolvedSeconds)) TrustedSeconds += resolvedSeconds;
            if (!IsRolledBack && UtcNow > LastTrustedUtc) LastTrustedUtc = UtcNow;
        }

        public void Restore(DateTime lastTrustedUtc, double trustedSeconds)
        {
            LastTrustedUtc = lastTrustedUtc;
            TrustedSeconds = Math.Max(0d, trustedSeconds);
        }
    }
}

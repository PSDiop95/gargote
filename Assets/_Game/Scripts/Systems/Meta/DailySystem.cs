using System;
using Gargote.Core;

namespace Gargote.Systems.Meta
{
    /// <summary>
    /// Connexion 7 jours, série (flamme), bouclier de série, roue Teranga. Tout est indexé par la clé de jour local
    /// (<see cref="GameClock.LocalDayKey"/>) et protégé par le temps de confiance :
    /// - horloge qui recule : rien n'est réclamable, aucun message, aucune pénalité (le jour revient tout seul) ;
    /// - fuseau changé vers l'est : un « nouveau jour » n'est accepté que si le minuit local suivant la dernière
    ///   réclamation est atteint en temps de confiance, à <see cref="LoginConfig.ResetToleranceHours"/> près.
    /// Le calendrier avance à chaque réclamation, même après une absence : on ne reprend jamais une récompense.
    /// </summary>
    public sealed class DailySystem
    {
        private readonly MetaDefinitions _defs;
        private readonly DeterministicRandom _random;
        private readonly double[] _wheelWeights;

        public int LastClaimDayKey { get; private set; } = int.MinValue;
        /// <summary>Temps de confiance auquel le prochain jour devient réclamable (minuit local suivant la réclamation).</summary>
        public double NextClaimTrustedSeconds { get; private set; }
        /// <summary>Jour du calendrier qui sera donné à la prochaine réclamation (0..Days-1).</summary>
        public int CalendarIndex { get; private set; }
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }
        public int Shields { get; private set; }
        public int ShieldWeekKey { get; private set; } = int.MinValue;
        public int TotalLoginDays { get; private set; }
        /// <summary>Dernière réclamation : un bouclier a sauvé la série.</summary>
        public bool LastClaimUsedShield { get; private set; }

        public int WheelDayKey { get; private set; } = int.MinValue;
        public int WheelFreeUsed { get; private set; }
        public int WheelRewardedUsed { get; private set; }
        public long WheelSpins { get; private set; }

        public event Action<int, RewardBundle> LoginClaimed;
        public event Action<int, WheelSegment> WheelSpun;

        public DailySystem(MetaDefinitions defs, DeterministicRandom random)
        {
            _defs = defs;
            _random = random;
            _wheelWeights = new double[defs.Wheel.Segments.Length];
            for (int i = 0; i < _wheelWeights.Length; i++) _wheelWeights[i] = defs.Wheel.Segments[i].Weight;
        }

        public LoginConfig Login => _defs.Login;
        public WheelConfig Wheel => _defs.Wheel;
        public DeterministicRandom Random => _random;
        public RewardBundle TodayReward => Login.Days[CalendarIndex % Login.Days.Length];

        // ------------------------------------------------------------------ Connexion
        public bool CanClaimLogin(int dayKey, double trustedSeconds) =>
            dayKey > LastClaimDayKey &&
            (LastClaimDayKey == int.MinValue || trustedSeconds >= NextClaimTrustedSeconds - Login.ResetToleranceHours * 3600d);

        /// <summary>La série serait-elle perdue si le joueur réclame aujourd'hui (et sans bouclier) ?</summary>
        public bool IsStreakInDanger(int dayKey) =>
            Streak > 0 && LastClaimDayKey != int.MinValue && dayKey - LastClaimDayKey >= 2 && Shields == 0;

        /// <summary>Réclame la connexion du jour. <paramref name="secondsToLocalMidnight"/> fixe le prochain jour.</summary>
        public RewardBundle ClaimLogin(int dayKey, double trustedSeconds, double secondsToLocalMidnight)
        {
            if (!CanClaimLogin(dayKey, trustedSeconds)) return null;
            int gap = LastClaimDayKey == int.MinValue ? 1 : dayKey - LastClaimDayKey;
            LastClaimUsedShield = false;
            if (gap == 1) Streak++;
            else if (gap == 2 && Shields > 0)
            {
                Shields--;
                Streak++;
                LastClaimUsedShield = true;
            }
            else Streak = 1; // une absence remet la flamme à 1, rien n'est retiré
            if (Streak > BestStreak) BestStreak = Streak;
            var reward = TodayReward;
            int day = CalendarIndex;
            CalendarIndex = (CalendarIndex + 1) % Login.Days.Length;
            LastClaimDayKey = dayKey;
            NextClaimTrustedSeconds = trustedSeconds + Math.Max(0d, secondsToLocalMidnight);
            TotalLoginDays++;
            LoginClaimed?.Invoke(day, reward);
            return reward;
        }

        /// <summary>Bouclier gagné via rewarded : 1 par semaine, plafonné.</summary>
        public bool CanEarnShield(int weekKey) => weekKey != ShieldWeekKey && Shields < Login.MaxShields && Login.ShieldsPerWeek > 0;

        public bool EarnShield(int weekKey)
        {
            if (!CanEarnShield(weekKey)) return false;
            ShieldWeekKey = weekKey;
            Shields++;
            return true;
        }

        // ------------------------------------------------------------------ Roue
        private void RollWheelDay(int dayKey)
        {
            if (dayKey <= WheelDayKey) return; // horloge qui recule : on garde le compteur du jour le plus récent
            WheelDayKey = dayKey;
            WheelFreeUsed = 0;
            WheelRewardedUsed = 0;
        }

        private int FreeLeft(int dayKey) => dayKey < WheelDayKey ? 0 : Wheel.FreePerDay - (dayKey == WheelDayKey ? WheelFreeUsed : 0);
        private int RewardedLeft(int dayKey) => dayKey < WheelDayKey ? 0 : Wheel.RewardedPerDay - (dayKey == WheelDayKey ? WheelRewardedUsed : 0);

        public bool HasFreeSpin(int dayKey) => FreeLeft(dayKey) > 0;
        public bool HasRewardedSpin(int dayKey) => RewardedLeft(dayKey) > 0;
        public int RewardedSpinsLeft(int dayKey) => Math.Max(0, RewardedLeft(dayKey));

        /// <summary>Tour de roue (gratuit, ou rewarded sur le callback de récompense). Index du segment, −1 si refusé.</summary>
        public int Spin(int dayKey, bool rewarded)
        {
            if (rewarded ? !HasRewardedSpin(dayKey) : !HasFreeSpin(dayKey)) return -1;
            RollWheelDay(dayKey);
            if (rewarded) WheelRewardedUsed++;
            else WheelFreeUsed++;
            int index = _random.Weighted(_wheelWeights);
            if (index < 0) return -1;
            WheelSpins++;
            WheelSpun?.Invoke(index, Wheel.Segments[index]);
            return index;
        }

        /// <summary>Probabilités affichées des segments.</summary>
        public double Probability(int segment)
        {
            double total = 0d;
            foreach (double w in _wheelWeights) total += w;
            return total > 0d ? _wheelWeights[segment] / total : 0d;
        }

        internal DailySaveData Save() => new DailySaveData
        {
            LastClaimDayKey = LastClaimDayKey, NextClaimTrustedSeconds = NextClaimTrustedSeconds, CalendarIndex = CalendarIndex,
            Streak = Streak, BestStreak = BestStreak, Shields = Shields, ShieldWeekKey = ShieldWeekKey, TotalLoginDays = TotalLoginDays,
            WheelDayKey = WheelDayKey, WheelFreeUsed = WheelFreeUsed, WheelRewardedUsed = WheelRewardedUsed, WheelSpins = WheelSpins,
            RandomState = _random.State.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        internal void Restore(DailySaveData d)
        {
            if (d == null) return;
            LastClaimDayKey = d.LastClaimDayKey;
            NextClaimTrustedSeconds = Math.Max(0d, d.NextClaimTrustedSeconds);
            CalendarIndex = Login.Days.Length == 0 ? 0 : Math.Abs(d.CalendarIndex) % Login.Days.Length;
            Streak = Math.Max(0, d.Streak);
            BestStreak = Math.Max(Streak, d.BestStreak);
            Shields = Math.Max(0, Math.Min(Login.MaxShields, d.Shields));
            ShieldWeekKey = d.ShieldWeekKey;
            TotalLoginDays = Math.Max(0, d.TotalLoginDays);
            WheelDayKey = d.WheelDayKey;
            WheelFreeUsed = Math.Max(0, d.WheelFreeUsed);
            WheelRewardedUsed = Math.Max(0, d.WheelRewardedUsed);
            WheelSpins = Math.Max(0L, d.WheelSpins);
            if (ulong.TryParse(d.RandomState, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out ulong state))
                _random.State = state;
        }
    }
}

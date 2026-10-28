using System;
using Gargote.Core;

namespace Gargote.Systems
{
    /// <summary>
    /// Façade d'une partie : regroupe économie, commerces et prestige de la ville en cours.
    /// Logique pure : aucune dépendance à UnityEngine ; la présentation écoute les événements.
    /// </summary>
    public sealed class GameSession
    {
        private readonly GameDefinitions _definitions;

        public EconomySystem Economy { get; }
        public PrestigeSystem Prestige { get; }
        public RewardedTracker Rewarded { get; }
        public BusinessSystem Businesses { get; private set; }
        public int CityIndex { get; private set; }
        public CityDefinition City => _definitions.Cities[CityIndex];
        public EconomyConfig EconomyConfig => _definitions.Economy;

        /// <summary>Levé après un prestige : (étoiles gagnées, changement de ville).</summary>
        public event Action<long, bool> Prestiged;
        /// <summary>Levé quand <see cref="Businesses"/> est remplacé (nouvelle ville).</summary>
        public event Action<BusinessSystem> CityChanged;

        public GameSession(GameDefinitions definitions)
        {
            definitions.Validate();
            _definitions = definitions;
            Economy = new EconomySystem(definitions.Economy);
            Prestige = new PrestigeSystem(definitions.Prestige);
            Rewarded = new RewardedTracker(definitions.Economy.Rewarded);
            EnterCity(0);
            Economy.ResetCity(City.StartingMoney);
        }

        public bool HasNextCity => CityIndex + 1 < _definitions.Cities.Length;
        public bool IsCityGoalReached => Economy.CityLifetimeEarnings >= BigDouble.FromDouble(City.GoalLifetimeEarnings);
        public double CityGoalProgress01 =>
            Math.Min(1d, (Economy.CityLifetimeEarnings / BigDouble.FromDouble(City.GoalLifetimeEarnings)).ToDouble());

        public long StarsToGain => Prestige.StarsToGain(Economy.CityLifetimeEarnings);
        public bool CanPrestige => Prestige.CanPrestige(Economy.CityLifetimeEarnings);

        public void Tick(double deltaSeconds, double hourOfDay)
        {
            Businesses.Tick(deltaSeconds, hourOfDay);
            Economy.ConsumeBoostTime(deltaSeconds);
        }

        /// <summary>
        /// Prestige. <paramref name="moveToNextCity"/> exige l'objectif de ville atteint et une ville suivante ;
        /// sinon la ville en cours est relancée (prestige libre).
        /// </summary>
        public bool PerformPrestige(bool moveToNextCity)
        {
            if (moveToNextCity && (!IsCityGoalReached || !HasNextCity)) return false;
            if (!moveToNextCity && !CanPrestige) return false;

            long gained = Prestige.Claim(Economy.CityLifetimeEarnings, moveToNextCity);
            Economy.PermanentMultiplier = Prestige.RevenueMultiplier;

            if (moveToNextCity)
            {
                EnterCity(CityIndex + 1);
                Economy.ResetCity(City.StartingMoney);
            }
            else
            {
                Businesses.ResetAll();
                Economy.ResetRun(City.StartingMoney);
            }
            Prestiged?.Invoke(gained, moveToNextCity);
            return true;
        }

        public OfflineResult ComputeOffline(double elapsedSeconds, double capSeconds)
        {
            var offline = _definitions.Economy.Offline;
            var boost = _definitions.Economy.AdBoost;
            return OfflineCalculator.Compute(Businesses.ManagedIncomePerSecond(), elapsedSeconds, capSeconds,
                offline.Efficiency, boost.AppliesOffline ? Economy.AdBoostRemainingSeconds : 0d, boost.Multiplier);
        }

        /// <summary>Crédite les gains hors ligne (multiplicateur x2 rewarded, x3 pass…) et consomme le boost écoulé.</summary>
        public void ClaimOffline(OfflineResult result, double multiplier)
        {
            Economy.Earn(result.Earnings * Math.Max(1d, multiplier));
            Economy.ConsumeBoostTime(result.ElapsedSeconds);
        }

        /// <summary>Rewarded « x2 gains hors ligne » regardée (callback de récompense) : renvoie le multiplicateur accordé.</summary>
        public double RewardOfflineAd(int dayKey) =>
            RewardedRules.ScaledMultiplier(_definitions.Economy.Offline.AdMultiplier, Rewarded.Register(dayKey));

        /// <summary>Rewarded « boost x2 » regardée (callback de récompense). Faux si le plafond de cumul est atteint.</summary>
        public bool RewardBoostAd(int dayKey)
        {
            if (!Economy.CanAddAdBoost) return false;
            return Economy.TryAddAdBoost(Rewarded.Register(dayKey));
        }

        public SaveData ToSaveData(DateTime nowUtc, double monotonicSeconds)
        {
            var data = new SaveData
            {
                SavedUtcTicks = nowUtc.Ticks,
                SavedMonotonicSeconds = monotonicSeconds,
                CityIndex = CityIndex,
                CityId = City.Id,
                Money = Economy.Money.ToSerializedString(),
                RunEarnings = Economy.RunEarnings.ToSerializedString(),
                CityLifetimeEarnings = Economy.CityLifetimeEarnings.ToSerializedString(),
                TotalLifetimeEarnings = Economy.TotalLifetimeEarnings.ToSerializedString(),
                AdBoostRemainingSeconds = Economy.AdBoostRemainingSeconds,
                TotalStars = Prestige.TotalStars,
                CurrentCityStars = Prestige.CurrentCityStars,
                ClaimedStarsForCity = Prestige.ClaimedForCurrentCity,
                PrestigeCount = Prestige.PrestigeCount,
                RewardedDayKey = Rewarded.DayKey,
                RewardedCountToday = Rewarded.CountToday,
                Businesses = new BusinessSaveData[Businesses.Count],
            };
            for (int i = 0; i < Businesses.Count; i++)
            {
                var s = Businesses[i];
                data.Businesses[i] = new BusinessSaveData
                {
                    Id = s.Definition.Id,
                    Level = s.Level,
                    HasManager = s.HasManager,
                    IsRunning = s.IsRunning,
                    Progress = s.Progress,
                };
            }
            return data;
        }

        /// <summary>Restaure une sauvegarde. Les commerces inconnus sont ignorés, les nouveaux restent à zéro.</summary>
        public void LoadFrom(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            int cityIndex = Array.FindIndex(_definitions.Cities, c => c.Id == data.CityId);
            if (cityIndex < 0) cityIndex = Math.Max(0, Math.Min(data.CityIndex, _definitions.Cities.Length - 1));
            EnterCity(cityIndex);

            Prestige.Restore(data.TotalStars, data.CurrentCityStars, data.ClaimedStarsForCity, data.PrestigeCount);
            Rewarded.Restore(data.RewardedDayKey, data.RewardedCountToday);
            Economy.PermanentMultiplier = Prestige.RevenueMultiplier;
            Economy.Restore(ParseOrZero(data.Money), ParseOrZero(data.RunEarnings),
                ParseOrZero(data.CityLifetimeEarnings), ParseOrZero(data.TotalLifetimeEarnings),
                data.AdBoostRemainingSeconds);

            if (data.Businesses == null) return;
            foreach (var b in data.Businesses)
            {
                if (b == null) continue;
                int index = Businesses.IndexOf(b.Id);
                if (index >= 0) Businesses.Restore(index, b.Level, b.HasManager, b.IsRunning, b.Progress);
            }
        }

        private void EnterCity(int index)
        {
            CityIndex = index;
            Prestige.CityThreshold = City.StarThreshold > 0 ? City.StarThreshold : _definitions.Prestige.Threshold;
            Businesses = new BusinessSystem(City, _definitions.Economy, Economy);
            CityChanged?.Invoke(Businesses);
        }

        private static BigDouble ParseOrZero(string text) =>
            BigDouble.TryParse(text, out var value) && value >= BigDouble.Zero ? value : BigDouble.Zero;
    }
}

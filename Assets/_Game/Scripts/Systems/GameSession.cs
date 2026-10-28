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
        private double _autoBuyTimer;
        private long[] _previousLevels;
        private bool[] _previousUpgrades;

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

        /// <summary>Niveaux juste avant la dernière relance (bouton « racheter comme avant »), ou null.</summary>
        public long[] PreviousLevels => _previousLevels;
        public bool CanRebuyPrevious => Prestige.Has(StarUpgradeEffect.RebuyPrevious) &&
                                         (Businesses.IsBelow(_previousLevels) || Businesses.IsBelowUpgrades(_previousUpgrades));

        public void Tick(double deltaSeconds, double hourOfDay)
        {
            Businesses.Tick(deltaSeconds, hourOfDay);
            Economy.ConsumeBoostTime(deltaSeconds);

            if (!Prestige.Has(StarUpgradeEffect.AutoBuyFirstMilestone)) return;
            _autoBuyTimer += deltaSeconds;
            if (_autoBuyTimer < _definitions.Economy.AutoBuyIntervalSeconds) return;
            _autoBuyTimer = 0d;
            var milestones = _definitions.Economy.Milestones;
            if (milestones.Length > 0) Businesses.AutoBuyToLevel(milestones[0]);
        }

        /// <summary>Achète un upgrade d'étoiles (les étoiles dépensées ne comptent plus dans le multiplicateur).</summary>
        public bool BuyStarUpgrade(int index)
        {
            if (!Prestige.TryBuyUpgrade(index)) return false;
            Economy.PermanentMultiplier = Prestige.RevenueMultiplier;
            return true;
        }

        private BigDouble StartingFundForReplay(BigDouble incomeBefore)
        {
            BigDouble start = BigDouble.FromDouble(City.StartingMoney);
            double seconds = _definitions.Prestige.RebuyStartingFundSeconds;
            if (seconds <= 0d || !Prestige.Has(StarUpgradeEffect.RebuyPrevious)) return start;
            return BigDouble.Max(start, incomeBefore * seconds);
        }

        /// <summary>Bouton « racheter comme avant » (upgrade d'étoiles requis).</summary>
        public bool RebuyPrevious()
        {
            if (!Prestige.Has(StarUpgradeEffect.RebuyPrevious)) return false;
            // Upgrades d'abord (peu chers, gros effet), puis niveaux, puis les upgrades devenus abordables.
            bool any = Businesses.RebuyUpgrades(_previousUpgrades);
            any |= Businesses.RebuyTowards(_previousLevels);
            any |= Businesses.RebuyUpgrades(_previousUpgrades);
            return any;
        }

        /// <summary>
        /// Prestige. <paramref name="moveToNextCity"/> exige l'objectif de ville atteint et une ville suivante ;
        /// sinon la ville en cours est relancée (prestige libre).
        /// </summary>
        public bool PerformPrestige(bool moveToNextCity)
        {
            if (moveToNextCity && (!IsCityGoalReached || !HasNextCity)) return false;
            if (!moveToNextCity && !CanPrestige) return false;

            // Revenu d'avant la relance, hors boosts temporaires (pub, bienvenue).
            BigDouble incomeBefore = Businesses.TotalIncomePerSecond() / (Economy.AdBoostMultiplier * Economy.WelcomeMultiplier);
            long gained = Prestige.Claim(Economy.CityLifetimeEarnings, moveToNextCity);
            Economy.PermanentMultiplier = Prestige.RevenueMultiplier;

            if (moveToNextCity)
            {
                _previousLevels = null;
                _previousUpgrades = null;
                EnterCity(CityIndex + 1);
                Economy.ResetCity(City.StartingMoney);
                Economy.StartWelcomeBonus();
            }
            else
            {
                _previousLevels = Businesses.LevelsSnapshot();
                _previousUpgrades = Businesses.UpgradesSnapshot();
                Businesses.ResetAll(Prestige.Has(StarUpgradeEffect.KeepManagers));
                Economy.ResetRun(StartingFundForReplay(incomeBefore));
            }
            Prestiged?.Invoke(gained, moveToNextCity);
            return true;
        }

        public OfflineResult ComputeOffline(double elapsedSeconds, double capSeconds)
        {
            var offline = _definitions.Economy.Offline;
            var boost = _definitions.Economy.AdBoost;
            // ManagedIncomePerSecond inclut le multiplicateur permanent ; boost et bienvenue s'appliquent à la part couverte.
            return OfflineCalculator.Compute(Businesses.ManagedIncomePerSecond(), elapsedSeconds, capSeconds,
                offline.Efficiency, boost.AppliesOffline ? Economy.AdBoostRemainingSeconds : 0d, boost.Multiplier,
                Economy.WelcomeRemainingSeconds, _definitions.Economy.Welcome.Multiplier);
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
                StarsSpent = Prestige.StarsSpent,
                OwnedStarUpgrades = Prestige.OwnedUpgradeIds(),
                MultiplierFloor = Prestige.MultiplierFloor,
                WelcomeRemainingSeconds = Economy.WelcomeRemainingSeconds,
                PreviousLevels = _previousLevels,
                OwnedCashUpgrades = Businesses.OwnedUpgradeIds(),
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

            Prestige.Restore(data.TotalStars, data.CurrentCityStars, data.ClaimedStarsForCity, data.PrestigeCount,
                data.StarsSpent, data.OwnedStarUpgrades, data.MultiplierFloor);
            _previousLevels = data.PreviousLevels;
            Rewarded.Restore(data.RewardedDayKey, data.RewardedCountToday);
            Economy.PermanentMultiplier = Prestige.RevenueMultiplier;
            Economy.Restore(ParseOrZero(data.Money), ParseOrZero(data.RunEarnings),
                ParseOrZero(data.CityLifetimeEarnings), ParseOrZero(data.TotalLifetimeEarnings),
                data.AdBoostRemainingSeconds, data.WelcomeRemainingSeconds);

            if (data.Businesses == null) return;
            foreach (var b in data.Businesses)
            {
                if (b == null) continue;
                int index = Businesses.IndexOf(b.Id);
                if (index >= 0) Businesses.Restore(index, b.Level, b.HasManager, b.IsRunning, b.Progress);
            }
            Businesses.RestoreUpgrades(data.OwnedCashUpgrades);
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

using System;

namespace Gargote.Systems
{
    /// <summary>
    /// DTO de sauvegarde (versionné). Les grands nombres sont stockés en texte invariant sans perte.
    /// Toute modification de forme = incrément de <see cref="CurrentVersion"/> + migration.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>v1 : M1/M2. v2 : M3 (méta, villes visitées, boosts gagnés, horloge de confiance).</summary>
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;
        public long SavedUtcTicks;
        public double SavedMonotonicSeconds = -1d;

        public int CityIndex;
        public string CityId;
        public string Money = "0";
        public string RunEarnings = "0";
        public string CityLifetimeEarnings = "0";
        public string TotalLifetimeEarnings = "0";
        public double AdBoostRemainingSeconds;

        public long TotalStars;
        public long CurrentCityStars;
        public long ClaimedStarsForCity;
        public int PrestigeCount;

        public int RewardedDayKey;
        public int RewardedCountToday;

        public long StarsSpent;
        public string[] OwnedStarUpgrades = Array.Empty<string>();
        public double MultiplierFloor = 1d;
        public double WelcomeRemainingSeconds;
        /// <summary>Niveaux d'avant la dernière relance (« racheter comme avant »), null si aucun.</summary>
        public long[] PreviousLevels;
        /// <summary>Upgrades FCFA achetés dans la ville en cours (run en cours).</summary>
        public string[] OwnedCashUpgrades = Array.Empty<string>();

        public BusinessSaveData[] Businesses = Array.Empty<BusinessSaveData>();

        // --- v2 (M3) ---
        public double TimedBoostRemainingSeconds;
        public double TimedBoostMultiplier = 1d;
        public double ExtraOfflineCapSeconds;
        public VisitedCitySaveData[] VisitedCities = Array.Empty<VisitedCitySaveData>();
        /// <summary>État de la méta (collection, coffres, missions, quotidien, succès, niveau…).</summary>
        public MetaSaveData Meta;
    }

    [Serializable]
    public sealed class VisitedCitySaveData
    {
        public string CityId;
        public string ManagedIncomePerSecond = "0";
        public string LifetimeEarnings = "0";
        public long[] Levels;
    }

    [Serializable]
    public sealed class BusinessSaveData
    {
        public string Id;
        public long Level;
        public bool HasManager;
        public bool IsRunning;
        public double Progress;
    }
}

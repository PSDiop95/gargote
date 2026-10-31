using System;
using Gargote.Core;

namespace Gargote.Simulation
{
    /// <summary>
    /// Forme JSON des données d'équilibrage initiales (Assets/_Game/Data/Seed/economy_seed.json).
    /// L'éditeur l'importe dans les ScriptableObjects (source de vérité du jeu) ;
    /// l'outil de simulation hors Unity (Tools/sim) la lit directement pour itérer vite.
    /// </summary>
    [Serializable]
    public sealed class GameDataSeed
    {
        public EconomySeed economy;
        public PrestigeSeed prestige;
        public CitySeed[] cities;
        public SimulationSeed simulation;

        public GameDefinitions ToDefinitions()
        {
            var defs = new GameDefinitions
            {
                Economy = economy.ToConfig(),
                Prestige = prestige.ToConfig(),
                Cities = new CityDefinition[cities.Length],
            };
            for (int i = 0; i < cities.Length; i++) defs.Cities[i] = cities[i].ToDefinition();
            defs.Validate();
            return defs;
        }
    }

    [Serializable]
    public sealed class EconomySeed
    {
        public long[] milestones;
        public double milestoneSpeedMultiplier;
        public double minCycleSeconds;
        public long[] globalMilestones;
        public double globalMilestoneMultiplier;
        public double dayLengthSeconds;
        public double morningStartHour, morningEndHour, nightStartHour, nightEndHour;
        public double affinityMultiplier;
        public double offlineBaseCapSeconds, offlineMaxCapSeconds, offlineEfficiency, offlineAdMultiplier;
        public double adBoostMultiplier, adBoostDurationSeconds, adBoostMaxStackSeconds;
        public bool adBoostAppliesOffline = true;
        public int rewardedFullValuePerDay;
        public double rewardedDecayPerExtraAd;
        public double welcomeMultiplier = 1, welcomeDurationSeconds;
        public double autoBuyIntervalSeconds = 0.5;

        public EconomyConfig ToConfig() => new EconomyConfig
        {
            Milestones = milestones,
            MilestoneSpeedMultiplier = milestoneSpeedMultiplier,
            MinCycleSeconds = minCycleSeconds,
            GlobalMilestones = globalMilestones ?? Array.Empty<long>(),
            GlobalMilestoneMultiplier = globalMilestoneMultiplier,
            DayNight = new DayNightConfig
            {
                DayLengthSeconds = dayLengthSeconds,
                MorningStartHour = morningStartHour, MorningEndHour = morningEndHour,
                NightStartHour = nightStartHour, NightEndHour = nightEndHour,
                AffinityMultiplier = affinityMultiplier,
            },
            Offline = new OfflineConfig
            {
                BaseCapSeconds = offlineBaseCapSeconds, MaxCapSeconds = offlineMaxCapSeconds,
                Efficiency = offlineEfficiency, AdMultiplier = offlineAdMultiplier,
            },
            AdBoost = new AdBoostConfig
            {
                Multiplier = adBoostMultiplier, DurationSeconds = adBoostDurationSeconds,
                MaxStackSeconds = adBoostMaxStackSeconds, AppliesOffline = adBoostAppliesOffline,
            },
            Rewarded = new RewardedConfig
            {
                FullValuePerDay = rewardedFullValuePerDay, DecayPerExtraAd = rewardedDecayPerExtraAd,
            },
            Welcome = new WelcomeBonusConfig { Multiplier = welcomeMultiplier, DurationSeconds = welcomeDurationSeconds },
            AutoBuyIntervalSeconds = autoBuyIntervalSeconds,
        };
    }

    [Serializable]
    public sealed class PrestigeSeed
    {
        public double k, threshold, bonusPerStar;
        public long minStarsToPrestige;
        public double softCapStars, softCapExponent;
        public double cityCarryOver = 1;
        public double travelMultiplierFloorRatio;
        public double rebuyStartingFundSeconds;
        /// <summary>Part du revenu géré d'une ville quittée qui continue de rentrer (villes visitées).</summary>
        public double visitedCityIncomeRatio;
        public StarUpgradeSeed[] starUpgrades;

        public PrestigeConfig ToConfig()
        {
            var cfg = new PrestigeConfig
            {
                K = k, Threshold = threshold, BonusPerStar = bonusPerStar, MinStarsToPrestige = minStarsToPrestige,
                SoftCapStars = softCapStars, SoftCapExponent = softCapExponent > 0 ? softCapExponent : 0.5,
                CityCarryOver = cityCarryOver, TravelMultiplierFloorRatio = travelMultiplierFloorRatio,
                RebuyStartingFundSeconds = rebuyStartingFundSeconds, VisitedCityIncomeRatio = visitedCityIncomeRatio,
                StarUpgrades = new StarUpgradeDefinition[starUpgrades?.Length ?? 0],
            };
            for (int i = 0; i < cfg.StarUpgrades.Length; i++) cfg.StarUpgrades[i] = starUpgrades[i].ToDefinition();
            return cfg;
        }
    }

    [Serializable]
    public sealed class StarUpgradeSeed
    {
        public string id;
        public string displayName;
        /// <summary>
        /// "keep_managers", "auto_buy_first_milestone", "rebuy_previous" (confort), ou "offline_earnings" (+value),
        /// "offline_cap" (+value secondes), "global_speed" (x value), "cost_reduction" (x value).
        /// </summary>
        public string effect;
        public long costStars;
        public double value;

        public StarUpgradeDefinition ToDefinition() => new StarUpgradeDefinition
        {
            Id = id, Effect = ParseEffect(effect), CostStars = costStars, Value = value,
        };

        public static StarUpgradeEffect ParseEffect(string value)
        {
            switch ((value ?? "").ToLowerInvariant())
            {
                case "keep_managers": return StarUpgradeEffect.KeepManagers;
                case "auto_buy_first_milestone": return StarUpgradeEffect.AutoBuyFirstMilestone;
                case "rebuy_previous": return StarUpgradeEffect.RebuyPrevious;
                case "offline_earnings": return StarUpgradeEffect.OfflineEarnings;
                case "offline_cap": return StarUpgradeEffect.OfflineCap;
                case "global_speed": return StarUpgradeEffect.GlobalSpeed;
                case "cost_reduction": return StarUpgradeEffect.CostReduction;
                default: throw new ArgumentException("Unknown star upgrade effect: " + value);
            }
        }
    }

    [Serializable]
    public sealed class CitySeed
    {
        public string id;
        public string displayName;
        public double goalLifetimeEarnings;
        public double startingMoney;
        public double starThreshold;
        public BusinessSeed[] businesses;
        public UpgradeSeed[] upgrades;

        public CityDefinition ToDefinition()
        {
            var def = new CityDefinition
            {
                Id = id, GoalLifetimeEarnings = goalLifetimeEarnings, StartingMoney = startingMoney,
                StarThreshold = starThreshold,
                Businesses = new BusinessDefinition[businesses.Length],
            };
            for (int i = 0; i < businesses.Length; i++) def.Businesses[i] = businesses[i].ToDefinition();
            def.Upgrades = new CashUpgradeDefinition[upgrades?.Length ?? 0];
            for (int i = 0; i < def.Upgrades.Length; i++)
            {
                var u = upgrades[i];
                int target = -1;
                if (!string.IsNullOrEmpty(u.business) && u.business != "all")
                {
                    target = Array.FindIndex(businesses, b => b.id == u.business);
                    if (target < 0) throw new ArgumentException(id + ": upgrade " + u.id + " targets unknown business " + u.business);
                }
                def.Upgrades[i] = new CashUpgradeDefinition { Id = u.id, BusinessIndex = target, RevenueMultiplier = u.multiplier, Cost = u.cost };
            }
            return def;
        }
    }

    [Serializable]
    public sealed class UpgradeSeed
    {
        public string id;
        /// <summary>Nom affiché (à valider dans docs/CULTURE_REVIEW.md).</summary>
        public string displayName;
        /// <summary>Id du commerce visé, ou "all".</summary>
        public string business;
        public double multiplier;
        public double cost;
    }

    [Serializable]
    public sealed class BusinessSeed
    {
        public string id;
        public string displayName;
        public double baseCost, growth, baseRevenue, baseCycleSeconds, managerCost;
        /// <summary>"none", "morning" ou "night".</summary>
        public string affinity;

        public BusinessDefinition ToDefinition() => new BusinessDefinition
        {
            Id = id, BaseCost = baseCost, Growth = growth, BaseRevenue = baseRevenue,
            BaseCycleSeconds = baseCycleSeconds, ManagerCost = managerCost, Affinity = ParseAffinity(affinity),
        };

        public static TimeAffinity ParseAffinity(string value)
        {
            switch ((value ?? "none").ToLowerInvariant())
            {
                case "morning": return TimeAffinity.Morning;
                case "night": return TimeAffinity.Night;
                default: return TimeAffinity.None;
            }
        }
    }

    [Serializable]
    public sealed class SimulationSeed
    {
        public int days;
        public double tickSeconds;
        public double prestigeWhenGainRatio;
        public double prestigeWhenStalledMinutes;
        public double stalledGainRatio;
        public double upgradeMaxShareOfStars = 0.25;
        public double adBoostRewatchBelowHours;
        public int maxBoostAdsPerDay;
        public double offlineAdMinAwayMinutes;
        public int proxyCities;
        public ProxyScalingSeed proxyCityScaling;
        public ProfileSeed[] profiles;
        public TargetsSeed targets;

        public ProxyCityScaling ToScaling() => proxyCityScaling == null
            ? new ProxyCityScaling()
            : new ProxyCityScaling
            {
                CostMultiplier = proxyCityScaling.costMultiplier, RevenueMultiplier = proxyCityScaling.revenueMultiplier,
                GoalMultiplier = proxyCityScaling.goalMultiplier, StarThresholdMultiplier = proxyCityScaling.starThresholdMultiplier,
                GoalMultipliers = proxyCityScaling.goalMultipliers ?? Array.Empty<double>(),
                UpgradeCostMultiplier = proxyCityScaling.upgradeCostMultiplier,
            };

        public SimulationSettings ToSettings()
        {
            var s = new SimulationSettings
            {
                Days = days, TickSeconds = tickSeconds, PrestigeWhenGainRatio = prestigeWhenGainRatio,
                PrestigeWhenStalledMinutes = prestigeWhenStalledMinutes, StalledGainRatio = stalledGainRatio,
                UpgradeMaxShareOfStars = upgradeMaxShareOfStars,
                AdBoostRewatchBelowHours = adBoostRewatchBelowHours,
                MaxBoostAdsPerDay = maxBoostAdsPerDay,
                OfflineAdMinAwayMinutes = offlineAdMinAwayMinutes,
                Profiles = new PlayerProfile[profiles.Length],
                Targets = new ProgressionTargets
                {
                    FirstManagerMaxMinutes = targets.firstManagerMaxMinutes,
                    FourthBusinessIndex = targets.fourthBusinessIndex,
                    FourthBusinessMaxMinutes = targets.fourthBusinessMaxMinutes,
                    FirstPrestigeMinDay = targets.firstPrestigeMinDay, FirstPrestigeMaxDay = targets.firstPrestigeMaxDay,
                    CityMinDays = targets.cityMinDays, CityMaxDays = targets.cityMaxDays,
                    MaxWallMinutes = targets.maxWallMinutes,
                    MinAdsPerDay = targets.minAdsPerDay, MaxAdsPerDay = targets.maxAdsPerDay,
                    MaxAdGapRatio = targets.maxAdGapRatio,
                    MaxStarMultiplier = targets.maxStarMultiplier, StarMultiplierDay = targets.starMultiplierDay,
                    SpacedUnlockIndex = targets.spacedUnlockIndex, MinUnlockSpacingMinutes = targets.minUnlockSpacingMinutes,
                    MinTravelFloorRatio = targets.minTravelFloorRatio, RecoveryShare = targets.recoveryShare,
                    MaxRecoveryMinutes = targets.maxRecoveryMinutes,
                    RecoveryMinMultiplierGain = targets.recoveryMinMultiplierGain,
                    MaxCaurisBalanceDays = targets.maxCaurisBalanceDays > 0 ? targets.maxCaurisBalanceDays : 3,
                    FirstEpicMaxDay = targets.firstEpicMaxDay > 0 ? targets.firstEpicMaxDay : 3,
                    FirstLegendaryMinDay = targets.firstLegendaryMinDay > 0 ? targets.firstLegendaryMinDay : 10,
                    FirstLegendaryMaxDay = targets.firstLegendaryMaxDay > 0 ? targets.firstLegendaryMaxDay : 20,
                    CollectionDay = targets.collectionDay > 0 ? targets.collectionDay : 30,
                    CollectionMin = targets.collectionMin > 0 ? targets.collectionMin : 0.6,
                    CollectionMax = targets.collectionMax > 0 ? targets.collectionMax : 0.75,
                },
            };
            for (int i = 0; i < profiles.Length; i++)
            {
                var p = profiles[i];
                s.Profiles[i] = new PlayerProfile
                {
                    Name = p.name, SessionsPerDay = p.sessionsPerDay, SessionMinutes = p.sessionMinutes,
                    FirstSessionMinutes = p.firstSessionMinutes, WatchesAds = p.watchesAds, IsReference = p.isReference,
                    Purchases = ParsePolicy(p.purchases), BoostSkipChance = p.boostSkipChance, Seed = p.seed == 0 ? i + 1 : p.seed,
                    WakeHour = p.wakeHour, SleepHour = p.sleepHour,
                    TapReactionSeconds = p.tapReactionSeconds, DecisionIntervalSeconds = p.decisionIntervalSeconds,
                };
            }
            return s;
        }

        public static PurchasePolicy ParsePolicy(string value)
        {
            switch ((value ?? "optimal").ToLowerInvariant())
            {
                case "cheapest": return PurchasePolicy.Cheapest;
                case "random": return PurchasePolicy.Random;
                default: return PurchasePolicy.Optimal;
            }
        }
    }

    [Serializable]
    public sealed class ProxyScalingSeed
    {
        public double costMultiplier, revenueMultiplier, goalMultiplier, starThresholdMultiplier;
        public double upgradeCostMultiplier = 1;
        public double[] goalMultipliers;
    }

    [Serializable]
    public sealed class ProfileSeed
    {
        public string name;
        public int sessionsPerDay;
        public double sessionMinutes, firstSessionMinutes;
        public bool watchesAds;
        public bool isReference;
        /// <summary>"optimal", "cheapest" ou "random".</summary>
        public string purchases;
        public double boostSkipChance;
        public int seed;
        public double wakeHour, sleepHour, tapReactionSeconds, decisionIntervalSeconds;
    }

    [Serializable]
    public sealed class TargetsSeed
    {
        public double firstManagerMaxMinutes;
        public int fourthBusinessIndex;
        public double fourthBusinessMaxMinutes;
        public double firstPrestigeMinDay, firstPrestigeMaxDay;
        public double[] cityMinDays, cityMaxDays;
        public double maxWallMinutes, minAdsPerDay, maxAdsPerDay;
        public double maxAdGapRatio, maxStarMultiplier;
        public int starMultiplierDay;
        public int spacedUnlockIndex;
        public double minUnlockSpacingMinutes;
        public double minTravelFloorRatio, recoveryShare, maxRecoveryMinutes, recoveryMinMultiplierGain;
        public double maxCaurisBalanceDays, firstEpicMaxDay, firstLegendaryMinDay, firstLegendaryMaxDay;
        public int collectionDay;
        public double collectionMin, collectionMax;
    }
}

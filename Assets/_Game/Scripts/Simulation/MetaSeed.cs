using System;
using System.Collections.Generic;
using Gargote.Core;

namespace Gargote.Simulation
{
    /// <summary>
    /// Forme JSON du contenu méta (Assets/_Game/Data/Seed/meta_seed.json) : gérants, coffres, missions, quotidien,
    /// niveaux, recettes, succès. L'éditeur l'importe dans les ScriptableObjects ; la simulation la lit directement.
    /// Les textes (noms, répliques, recettes) partent dans la table de localisation, pas dans les définitions.
    /// </summary>
    [Serializable]
    public sealed class MetaSeed
    {
        public ManagerSeed[] managers;
        public ManagerLevelsSeed managerLevels;
        public ChestSeed[] chests;
        public ChestRulesSeed chestRules;
        public MissionSeed[] missions;
        public MissionSeed[] challenges;
        public MissionRulesSeed missionRules;
        public LoginSeed login;
        public WheelSeed wheel;
        public PlayerLevelsSeed playerLevels;
        public RecipeSeed[] recipes;
        public AlbumSeed album;
        public AchievementSeed[] achievements;
        public NotificationsSeed notifications;
        public CaurisSeed cauris;
        public RedDotsSeed redDots;

        public MetaDefinitions ToDefinitions(GameDefinitions game = null)
        {
            var d = new MetaDefinitions
            {
                Managers = Map(managers, m => m.ToDefinition()),
                ManagerLevels = managerLevels.ToConfig(),
                Chests = Map(chests, c => c.ToDefinition()),
                ChestRules = chestRules.ToRules(),
                Missions = Map(missions, m => m.ToDefinition()),
                Challenges = Map(challenges, m => m.ToDefinition()),
                MissionRules = missionRules.ToRules(),
                Login = login.ToConfig(),
                Wheel = wheel.ToConfig(),
                PlayerLevels = playerLevels.ToConfig(),
                Recipes = Map(recipes, r => new RecipeDefinition { Id = r.id, BusinessId = r.business, UnlockLevel = Math.Max(1, r.unlockLevel) }),
                AlbumCityBonusMultiplier = album?.cityBonusMultiplier ?? 1.1,
                Achievements = Map(achievements, a => a.ToDefinition()),
                Notifications = notifications?.ToConfig() ?? new NotificationConfig(),
                Cauris = cauris?.ToConfig() ?? new CaurisConfig(),
                MinSecondsBetweenReminders = redDots?.minSecondsBetweenReminders ?? 60,
            };
            d.Validate(game);
            return d;
        }

        private static TOut[] Map<TIn, TOut>(TIn[] source, Func<TIn, TOut> f)
        {
            if (source == null) return Array.Empty<TOut>();
            var r = new TOut[source.Length];
            for (int i = 0; i < source.Length; i++) r[i] = f(source[i]);
            return r;
        }

        public static Rarity ParseRarity(string value)
        {
            switch ((value ?? "").ToLowerInvariant())
            {
                case "common": return Rarity.Common;
                case "rare": return Rarity.Rare;
                case "epic": return Rarity.Epic;
                case "legendary": return Rarity.Legendary;
                default: throw new ArgumentException("Unknown rarity: " + value);
            }
        }

        public static double[] ByRarity(Dictionary<string, double> values)
        {
            var r = new double[4];
            if (values == null) return r;
            foreach (var kv in values) r[(int)ParseRarity(kv.Key)] = kv.Value;
            return r;
        }

        /// <summary>snake_case → PascalCase d'énumération (« reach_level » → ReachLevel).</summary>
        public static T ParseEnum<T>(string value) where T : struct
        {
            var parts = (value ?? "").Split('_');
            var name = "";
            foreach (var p in parts) if (p.Length > 0) name += char.ToUpperInvariant(p[0]) + p.Substring(1);
            if (!Enum.TryParse(name, out T result)) throw new ArgumentException("Unknown " + typeof(T).Name + ": " + value);
            return result;
        }
    }

    [Serializable]
    public sealed class ManagerSeed
    {
        public string id, city, business, rarity, passive;
        public string nameFr, nameEn, lineFr, lineEn, lineWo;
        public double passivePerLevel, boostMultiplier, boostSeconds, boostCooldownSeconds;
        public bool boostGlobal;

        public ManagerDefinition ToDefinition() => new ManagerDefinition
        {
            Id = id, CityId = city, BusinessId = business, Rarity = MetaSeed.ParseRarity(rarity),
            Passive = passive == "global" ? PassiveScope.Global : PassiveScope.Business, PassivePerLevel = passivePerLevel,
            BoostMultiplier = boostMultiplier, BoostSeconds = boostSeconds, BoostCooldownSeconds = boostCooldownSeconds, BoostGlobal = boostGlobal,
        };
    }

    [Serializable]
    public sealed class ManagerLevelsSeed
    {
        public int maxLevel;
        public int[] cardsToNext;
        public double[] fcfaSecondsToNext;
        public Dictionary<string, double> duplicateCaurisAtMax;
        public long accelerateBoostCauris;

        public ManagerLevelConfig ToConfig()
        {
            var dup = MetaSeed.ByRarity(duplicateCaurisAtMax);
            return new ManagerLevelConfig
            {
                MaxLevel = maxLevel, CardsToNext = cardsToNext, FcfaSecondsToNext = fcfaSecondsToNext,
                DuplicateCaurisAtMax = new[] { (int)dup[0], (int)dup[1], (int)dup[2], (int)dup[3] },
                AccelerateBoostCauris = accelerateBoostCauris,
            };
        }
    }

    [Serializable]
    public sealed class ChestSeed
    {
        public string id, pityRarity;
        public int cards, pityEvery;
        public bool firstGuaranteed;
        public Dictionary<string, double> weights;
        public long caurisMin, caurisMax, costCauris;
        public double boostChance;

        public ChestDefinition ToDefinition() => new ChestDefinition
        {
            Id = id, Cards = cards, RarityWeights = MetaSeed.ByRarity(weights), CaurisMin = caurisMin, CaurisMax = caurisMax,
            BoostChance = boostChance, CostCauris = costCauris,
            PityRarity = string.IsNullOrEmpty(pityRarity) ? Rarity.Common : MetaSeed.ParseRarity(pityRarity), PityEvery = pityEvery,
            FirstGuaranteed = firstGuaranteed,
        };
    }

    [Serializable]
    public sealed class ChestRulesSeed
    {
        public string freeChest, rewardedChest;
        public double freeEverySeconds, boostMultiplier, boostSeconds, boostMaxStackSeconds;
        public int freeMaxStored, rewardedPerDay;

        public ChestRules ToRules() => new ChestRules
        {
            FreeChestId = freeChest, FreeEverySeconds = freeEverySeconds, FreeMaxStored = freeMaxStored,
            RewardedChestId = rewardedChest, RewardedPerDay = rewardedPerDay,
            BoostMultiplier = boostMultiplier, BoostSeconds = boostSeconds, BoostMaxStackSeconds = boostMaxStackSeconds,
        };
    }

    [Serializable]
    public sealed class MissionSeed
    {
        public string id, kind, chest;
        public double weight, amount;
        public long xp, cauris, tokens;

        public MissionDefinition ToDefinition() => new MissionDefinition
        {
            Id = id, Kind = MetaSeed.ParseEnum<MissionKind>(kind), Weight = weight, Amount = amount,
            Xp = xp, Cauris = cauris, Tokens = tokens, ChestId = chest ?? "",
        };
    }

    [Serializable]
    public sealed class MissionRulesSeed
    {
        public int activeSlots = 3, freeReplacesPerDay = 1, challengeSlots = 3, accelerateRewardedPerDay = 3;
        public double refillDelaySeconds = 1800;
        public long replaceCauris = 10;
        public string challengeBonusChest = "bronze";

        public MissionRules ToRules() => new MissionRules
        {
            ActiveSlots = activeSlots, FreeReplacesPerDay = freeReplacesPerDay, ReplaceCauris = replaceCauris,
            ChallengeSlots = challengeSlots, ChallengeBonusChestId = challengeBonusChest ?? "bronze",
            AccelerateRewardedPerDay = accelerateRewardedPerDay, RefillDelaySeconds = refillDelaySeconds,
        };
    }

    [Serializable]
    public sealed class RewardSeed
    {
        public string id, chest;
        public double weight = 1;
        public long cauris, tokens;
        public bool boost;
        public int cards;

        public RewardBundle ToReward() => new RewardBundle { Cauris = cauris, Tokens = tokens, ChestId = chest ?? "", Boost = boost, Cards = cards };
    }

    [Serializable]
    public sealed class LoginSeed
    {
        public RewardSeed[] days;
        public int shieldsPerWeek = 1, maxShields = 1;
        public double resetToleranceHours = 3;

        public LoginConfig ToConfig()
        {
            var r = new RewardBundle[days?.Length ?? 0];
            for (int i = 0; i < r.Length; i++) r[i] = days[i].ToReward();
            return new LoginConfig { Days = r, ShieldsPerWeek = shieldsPerWeek, MaxShields = maxShields, ResetToleranceHours = resetToleranceHours };
        }
    }

    [Serializable]
    public sealed class WheelSeed
    {
        public int freePerDay = 1, rewardedPerDay = 3;
        public RewardSeed[] segments;

        public WheelConfig ToConfig()
        {
            var s = new WheelSegment[segments?.Length ?? 0];
            for (int i = 0; i < s.Length; i++) s[i] = new WheelSegment { Id = segments[i].id, Weight = segments[i].weight, Reward = segments[i].ToReward() };
            return new WheelConfig { FreePerDay = freePerDay, RewardedPerDay = rewardedPerDay, Segments = s };
        }
    }

    [Serializable]
    public sealed class PlayerLevelsSeed
    {
        public long[] xpToReach;
        public long caurisPerLevel;
        public int chestEvery;
        public string chestOnLevel;
        public Dictionary<string, int> unlocks;
        public Dictionary<string, long> xp;

        public PlayerLevelConfig ToConfig()
        {
            var cfg = new PlayerLevelConfig
            {
                XpToReach = xpToReach, CaurisPerLevel = caurisPerLevel, ChestEvery = chestEvery, ChestOnLevel = chestOnLevel ?? "",
                FeatureLevels = new int[Enum.GetValues(typeof(Feature)).Length],
                Xp = new long[Enum.GetValues(typeof(XpSource)).Length],
            };
            if (unlocks != null) foreach (var kv in unlocks) cfg.FeatureLevels[(int)MetaSeed.ParseEnum<Feature>(kv.Key)] = kv.Value;
            if (xp != null) foreach (var kv in xp) cfg.Xp[(int)MetaSeed.ParseEnum<XpSource>(kv.Key)] = kv.Value;
            return cfg;
        }
    }

    [Serializable]
    public sealed class RecipeSeed
    {
        public string id, business, nameFr, nameEn, nameWo, anecdoteFr, anecdoteEn;
        public long unlockLevel = 1;
        public string[] stepsFr, stepsEn;
    }

    [Serializable]
    public sealed class AlbumSeed { public double cityBonusMultiplier = 1.1; }

    [Serializable]
    public sealed class AchievementSeed
    {
        public string id, nameFr, nameEn, stat, business;
        public double[] tiers;
        public long[] cauris;

        public AchievementDefinition ToDefinition() => new AchievementDefinition
        {
            Id = id, Stat = MetaSeed.ParseEnum<StatId>(stat), BusinessId = business ?? "", Tiers = tiers, CaurisRewards = cauris,
        };
    }

    [Serializable]
    public sealed class NotificationsSeed
    {
        public int maxPerDay = 2, quietStartHour = 22, quietEndHour = 8;
        public bool askPermissionAfterFirstFreeChest = true;

        public NotificationConfig ToConfig() => new NotificationConfig
        {
            MaxPerDay = maxPerDay, QuietStartHour = quietStartHour, QuietEndHour = quietEndHour,
            AskPermissionAfterFirstFreeChest = askPermissionAfterFirstFreeChest,
        };
    }

    [Serializable]
    public sealed class CaurisSeed
    {
        public long startingCauris, offlineCapExtensionCauris = 50;
        public double offlineCapExtensionSeconds = 14400, offlineCapExtensionDurationSeconds = 86400;

        public CaurisConfig ToConfig() => new CaurisConfig
        {
            StartingCauris = startingCauris, OfflineCapExtensionCauris = offlineCapExtensionCauris,
            OfflineCapExtensionSeconds = offlineCapExtensionSeconds, OfflineCapExtensionDurationSeconds = offlineCapExtensionDurationSeconds,
        };
    }

    [Serializable]
    public sealed class RedDotsSeed { public double minSecondsBetweenReminders = 60; }
}

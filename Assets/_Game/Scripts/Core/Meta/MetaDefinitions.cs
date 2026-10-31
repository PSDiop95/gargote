using System;
using System.Collections.Generic;

namespace Gargote.Core
{
    public enum Rarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3,
    }

    /// <summary>Portée du bonus passif d'un gérant de collection.</summary>
    public enum PassiveScope
    {
        /// <summary>Revenus de son commerce.</summary>
        Business = 0,
        /// <summary>Revenus de tous les commerces.</summary>
        Global = 1,
    }

    /// <summary>Gérant (§5) : personnage rattaché à un commerce, à collectionner et à faire monter de niveau.</summary>
    [Serializable]
    public sealed class ManagerDefinition
    {
        public string Id;
        public string CityId;
        public string BusinessId;
        public Rarity Rarity;
        /// <summary>Gérant de base : obtenu en embauchant le gérant du commerce avec des FCFA.</summary>
        public bool IsBase => Rarity == Rarity.Common;
        public PassiveScope Passive;
        /// <summary>Bonus passif par niveau (0,05 = +5 % par niveau).</summary>
        public double PassivePerLevel;
        public double BoostMultiplier = 3;
        public double BoostSeconds = 30;
        public double BoostCooldownSeconds = 600;
        /// <summary>Le boost actif s'applique à toute la rue (sinon : à son commerce).</summary>
        public bool BoostGlobal;
    }

    [Serializable]
    public sealed class ManagerLevelConfig
    {
        public int MaxLevel = 10;
        /// <summary>Cartes pour passer du niveau n au niveau n+1 (index n−1).</summary>
        public int[] CardsToNext = Array.Empty<int>();
        /// <summary>Coût FCFA du niveau suivant, en secondes du revenu actuel (index n−1).</summary>
        public double[] FcfaSecondsToNext = Array.Empty<double>();
        /// <summary>Cauris donnés par carte en double d'un gérant déjà au niveau max (par rareté).</summary>
        public int[] DuplicateCaurisAtMax = { 1, 3, 8, 20 };
        public long AccelerateBoostCauris = 20;
    }

    [Serializable]
    public sealed class ChestDefinition
    {
        public string Id;
        public int Cards;
        /// <summary>Poids par rareté (Common, Rare, Epic, Legendary) pour chaque carte.</summary>
        public double[] RarityWeights = new double[4];
        public long CaurisMin, CaurisMax;
        /// <summary>Probabilité qu'un boost x2 temporaire accompagne le coffre.</summary>
        public double BoostChance;
        /// <summary>Prix en cauris dans la boutique (0 = non vendu).</summary>
        public long CostCauris;
        /// <summary>Système de pitié : au moins une carte de cette rareté tous les <see cref="PityEvery"/> coffres.</summary>
        public Rarity PityRarity;
        public int PityEvery;
        /// <summary>Le tout premier coffre de ce type contient la rareté garantie (« ton premier coffre Or : un Épique ! »).</summary>
        public bool FirstGuaranteed;
    }

    [Serializable]
    public sealed class ChestRules
    {
        public string FreeChestId = "wood";
        public double FreeEverySeconds = 14400;
        public int FreeMaxStored = 2;
        public string RewardedChestId = "bronze";
        public int RewardedPerDay = 1;
        public double BoostMultiplier = 2;
        public double BoostSeconds = 900;
        public double BoostMaxStackSeconds = 7200;
    }

    public enum MissionKind
    {
        ReachLevel = 0,
        HireManager = 1,
        ServeCustomers = 2,
        ReachMilestone = 3,
        UseManagerBoost = 4,
        TapSpecials = 5,
        BuyUpgrades = 6,
        OpenChests = 7,
        EarnSecondsOfIncome = 8,
        CompleteMissions = 9,
    }

    /// <summary>Modèle de mission ou de défi du jour, tiré au hasard (pondéré) et adapté à l'état du joueur.</summary>
    [Serializable]
    public sealed class MissionDefinition
    {
        public string Id;
        public MissionKind Kind;
        public double Weight = 1;
        /// <summary>Paramètre du modèle : quantité, nombre de paliers à gagner, ou secondes de revenu.</summary>
        public double Amount = 1;
        public long Xp;
        public long Cauris;
        public long Tokens;
        /// <summary>Coffre offert (vide = aucun).</summary>
        public string ChestId = "";
    }

    [Serializable]
    public sealed class MissionRules
    {
        public int ActiveSlots = 3;
        public int FreeReplacesPerDay = 1;
        public long ReplaceCauris = 10;
        public int ChallengeSlots = 3;
        /// <summary>Après une réclamation, l'emplacement se recharge en N secondes de temps de confiance.</summary>
        public double RefillDelaySeconds = 1800;
        /// <summary>Coffre donné quand tous les défis du jour sont réclamés.</summary>
        public string ChallengeBonusChestId = "bronze";
        /// <summary>Recharge de boost de gérant accélérée via rewarded : nombre par jour.</summary>
        public int AccelerateRewardedPerDay = 3;
    }

    [Serializable]
    public sealed class RewardBundle
    {
        public long Cauris;
        public long Tokens;
        public string ChestId = "";
        /// <summary>Boost x2 temporaire (durée de <see cref="ChestRules.BoostSeconds"/>).</summary>
        public bool Boost;
        /// <summary>Cartes de gérant au hasard (rareté commune).</summary>
        public int Cards;
    }

    [Serializable]
    public sealed class LoginConfig
    {
        public RewardBundle[] Days = Array.Empty<RewardBundle>();
        public int ShieldsPerWeek = 1;
        public int MaxShields = 1;
        /// <summary>Tolérance (heures) sur le passage de minuit, pour les changements de fuseau.</summary>
        public double ResetToleranceHours = 3;
    }

    [Serializable]
    public sealed class WheelSegment
    {
        public string Id;
        public double Weight = 1;
        public RewardBundle Reward = new RewardBundle();
    }

    [Serializable]
    public sealed class WheelConfig
    {
        public int FreePerDay = 1;
        public int RewardedPerDay = 3;
        public WheelSegment[] Segments = Array.Empty<WheelSegment>();
    }

    /// <summary>Fonctionnalités dévoilées par le niveau joueur (§7.3, onboarding naturel).</summary>
    public enum Feature
    {
        Missions = 0,
        Chests = 1,
        Shop = 2,
        Login = 3,
        StarUpgrades = 4,
        Wheel = 5,
        Album = 6,
        Achievements = 7,
        DailyChallenges = 8,
    }

    /// <summary>Actions qui rapportent de l'XP joueur.</summary>
    public enum XpSource
    {
        BuyLevels = 0,
        UnlockBusiness = 1,
        HireManager = 2,
        Milestone = 3,
        CashUpgrade = 4,
        Chest = 5,
        Prestige = 6,
        NewCity = 7,
        Special = 8,
        ManagerBoost = 9,
        Login = 10,
        Wheel = 11,
        Recipe = 12,
        Achievement = 13,
        ManagerLevel = 14,
        Mission = 15,
        Challenge = 16,
    }

    [Serializable]
    public sealed class PlayerLevelConfig
    {
        /// <summary>XP cumulée pour atteindre le niveau n (index n−1 ; index 0 = 0).</summary>
        public long[] XpToReach = { 0 };
        public long CaurisPerLevel = 15;
        public int ChestEvery = 5;
        public string ChestOnLevel = "bronze";
        /// <summary>Niveau de déblocage de chaque <see cref="Feature"/>.</summary>
        public int[] FeatureLevels = new int[9];
        /// <summary>XP par <see cref="XpSource"/>.</summary>
        public long[] Xp = new long[17];

        public int UnlockLevel(Feature feature) => FeatureLevels[(int)feature];
    }

    [Serializable]
    public sealed class RecipeDefinition
    {
        public string Id;
        public string BusinessId;
        /// <summary>Le plat entre au menu (et dans l'album au premier service) à ce niveau du commerce.</summary>
        public long UnlockLevel = 1;
    }

    /// <summary>Statistiques suivies (missions, succès, album).</summary>
    public enum StatId
    {
        LifetimeEarnings = 0,
        Customers = 1,
        Taps = 2,
        LevelsBought = 3,
        BusinessesUnlocked = 4,
        ManagersHired = 5,
        Milestones = 6,
        CashUpgrades = 7,
        Prestiges = 8,
        Stars = 9,
        CitiesVisited = 10,
        ChestsOpened = 11,
        ManagersOwned = 12,
        RareOwned = 13,
        EpicOwned = 14,
        LegendaryOwned = 15,
        ManagerLevels = 16,
        MissionsCompleted = 17,
        ChallengesCompleted = 18,
        BestStreak = 19,
        LoginDays = 20,
        WheelSpins = 21,
        SpecialsTapped = 22,
        ManagerBoosts = 23,
        Recipes = 24,
        PlayerLevel = 25,
        CaurisEarned = 26,
        GlobalMilestones = 27,
        OfflineClaims = 28,
        RewardedWatched = 29,
        // Statistiques par commerce (avec BusinessId).
        BusinessLevel = 100,
        BusinessCustomers = 101,
        BusinessMilestones = 102,
        BusinessManagerLevel = 103,
    }

    [Serializable]
    public sealed class AchievementDefinition
    {
        public string Id;
        public StatId Stat;
        /// <summary>Commerce visé pour les statistiques par commerce, sinon vide.</summary>
        public string BusinessId = "";
        /// <summary>Seuils bronze / argent / or.</summary>
        public double[] Tiers = Array.Empty<double>();
        public long[] CaurisRewards = Array.Empty<long>();
        public bool IsPerBusiness => (int)Stat >= 100;
    }

    [Serializable]
    public sealed class NotificationConfig
    {
        public int MaxPerDay = 2;
        public int QuietStartHour = 22;
        public int QuietEndHour = 8;
        public bool AskPermissionAfterFirstFreeChest = true;
    }

    [Serializable]
    public sealed class CaurisConfig
    {
        public long StartingCauris;
        public long OfflineCapExtensionCauris = 50;
        public double OfflineCapExtensionSeconds = 14400;
        public double OfflineCapExtensionDurationSeconds = 86400;
    }

    /// <summary>Contenu méta complet (M3), produit par les ScriptableObjects puis surchargeable par Remote Config (M5).</summary>
    public sealed class MetaDefinitions
    {
        public ManagerDefinition[] Managers = Array.Empty<ManagerDefinition>();
        public ManagerLevelConfig ManagerLevels = new ManagerLevelConfig();
        public ChestDefinition[] Chests = Array.Empty<ChestDefinition>();
        public ChestRules ChestRules = new ChestRules();
        public MissionDefinition[] Missions = Array.Empty<MissionDefinition>();
        public MissionDefinition[] Challenges = Array.Empty<MissionDefinition>();
        public MissionRules MissionRules = new MissionRules();
        public LoginConfig Login = new LoginConfig();
        public WheelConfig Wheel = new WheelConfig();
        public PlayerLevelConfig PlayerLevels = new PlayerLevelConfig();
        public RecipeDefinition[] Recipes = Array.Empty<RecipeDefinition>();
        /// <summary>Album d'une ville complet : multiplicateur permanent de revenus.</summary>
        public double AlbumCityBonusMultiplier = 1.1;
        public AchievementDefinition[] Achievements = Array.Empty<AchievementDefinition>();
        public NotificationConfig Notifications = new NotificationConfig();
        public CaurisConfig Cauris = new CaurisConfig();
        /// <summary>Anti-spam des points rouges : un rappel au plus toutes les N secondes.</summary>
        public double MinSecondsBetweenReminders = 60;

        public ChestDefinition Chest(string id)
        {
            for (int i = 0; i < Chests.Length; i++)
                if (Chests[i].Id == id) return Chests[i];
            return null;
        }

        public int ManagerIndex(string id)
        {
            for (int i = 0; i < Managers.Length; i++)
                if (Managers[i].Id == id) return i;
            return -1;
        }

        public void Validate(GameDefinitions game)
        {
            var ids = new HashSet<string>();
            foreach (var m in Managers)
            {
                if (m == null || string.IsNullOrEmpty(m.Id) || !ids.Add("manager:" + m.Id)) throw new ArgumentException("Invalid or duplicate manager id.");
                if (m.BoostMultiplier < 1 || m.BoostSeconds <= 0 || m.BoostCooldownSeconds < m.BoostSeconds)
                    throw new ArgumentException(m.Id + ": boost must last > 0 and recharge after it ends.");
                if (m.PassivePerLevel < 0) throw new ArgumentException(m.Id + ": passive must be >= 0.");
            }
            if (ManagerLevels.MaxLevel < 1 || ManagerLevels.CardsToNext.Length < ManagerLevels.MaxLevel - 1 ||
                ManagerLevels.FcfaSecondsToNext.Length < ManagerLevels.MaxLevel - 1 || ManagerLevels.DuplicateCaurisAtMax.Length < 4)
                throw new ArgumentException("Manager level tables are incomplete.");
            foreach (var c in Chests)
            {
                if (c == null || string.IsNullOrEmpty(c.Id) || !ids.Add("chest:" + c.Id)) throw new ArgumentException("Invalid or duplicate chest id.");
                double total = 0;
                foreach (double w in c.RarityWeights) { if (w < 0) throw new ArgumentException(c.Id + ": negative weight."); total += w; }
                if (c.RarityWeights.Length != 4 || total <= 0 || c.Cards < 1) throw new ArgumentException(c.Id + ": weights or cards invalid.");
                if (c.CaurisMax < c.CaurisMin || c.CaurisMin < 0) throw new ArgumentException(c.Id + ": cauris range invalid.");
                if (c.PityEvery < 0) throw new ArgumentException(c.Id + ": pity invalid.");
            }
            if (Chest(ChestRules.FreeChestId) == null || Chest(ChestRules.RewardedChestId) == null) throw new ArgumentException("Free or rewarded chest missing.");
            foreach (var list in new[] { Missions, Challenges })
                foreach (var m in list)
                {
                    if (m == null || string.IsNullOrEmpty(m.Id) || !ids.Add("mission:" + m.Id)) throw new ArgumentException("Invalid or duplicate mission id.");
                    if (m.Weight <= 0 || m.Amount <= 0) throw new ArgumentException(m.Id + ": weight and amount must be > 0.");
                    if (!string.IsNullOrEmpty(m.ChestId) && Chest(m.ChestId) == null) throw new ArgumentException(m.Id + ": unknown chest.");
                }
            if (Login.Days.Length == 0) throw new ArgumentException("Login calendar is empty.");
            if (Wheel.Segments.Length == 0) throw new ArgumentException("Wheel has no segment.");
            foreach (var s in Wheel.Segments)
                if (s == null || string.IsNullOrEmpty(s.Id) || s.Weight <= 0 || !ids.Add("wheel:" + s.Id)) throw new ArgumentException("Invalid wheel segment.");
            var xp = PlayerLevels.XpToReach;
            if (xp.Length < 2 || xp[0] != 0) throw new ArgumentException("XP table must start at 0.");
            for (int i = 1; i < xp.Length; i++) if (xp[i] <= xp[i - 1]) throw new ArgumentException("XP table must be increasing.");
            foreach (var r in Recipes)
                if (r == null || string.IsNullOrEmpty(r.Id) || !ids.Add("recipe:" + r.Id)) throw new ArgumentException("Invalid or duplicate recipe id.");
            foreach (var a in Achievements)
            {
                if (a == null || string.IsNullOrEmpty(a.Id) || !ids.Add("achievement:" + a.Id)) throw new ArgumentException("Invalid or duplicate achievement id.");
                if (a.Tiers.Length == 0 || a.CaurisRewards.Length != a.Tiers.Length) throw new ArgumentException(a.Id + ": tiers and rewards mismatch.");
                for (int i = 1; i < a.Tiers.Length; i++) if (a.Tiers[i] <= a.Tiers[i - 1]) throw new ArgumentException(a.Id + ": tiers must increase.");
                if (a.IsPerBusiness && string.IsNullOrEmpty(a.BusinessId)) throw new ArgumentException(a.Id + ": business required.");
            }
            if (game == null) return;
            var businessIds = new HashSet<string>();
            foreach (var city in game.Cities) foreach (var b in city.Businesses) businessIds.Add(b.Id);
            foreach (var m in Managers)
                if (!businessIds.Contains(m.BusinessId)) throw new ArgumentException(m.Id + ": unknown business " + m.BusinessId);
            foreach (var r in Recipes)
                if (!businessIds.Contains(r.BusinessId)) throw new ArgumentException(r.Id + ": unknown business " + r.BusinessId);
        }
    }
}

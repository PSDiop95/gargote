using System;

namespace Gargote.Systems
{
    /// <summary>État sauvegardé de la méta (v2, M3). Champs simples uniquement (JsonUtility / Newtonsoft).</summary>
    [Serializable]
    public sealed class MetaSaveData
    {
        /// <summary>Horloge de confiance (<see cref="Gargote.Core.GameClock"/>).</summary>
        public long LastTrustedUtcTicks;
        public double TrustedSeconds;

        public LedgerSaveData Ledger;
        public StatsSaveData Stats;
        public ManagersSaveData Managers;
        public ChestsSaveData Chests;
        public DailySaveData Daily;

        public MissionSaveData[] Missions = Array.Empty<MissionSaveData>();
        public double[] MissionRefillAt = Array.Empty<double>();
        public string MissionRandomState = "";
        public int MissionReplaceDayKey = int.MinValue;
        public int MissionFreeReplacesUsed;

        public MissionSaveData[] Challenges = Array.Empty<MissionSaveData>();
        public string ChallengeRandomState = "";
        public int ChallengeDayKey = int.MinValue;
        public bool ChallengeBonusClaimed;

        public long Xp;
        public int RewardedLevel = 1;

        public string[] Recipes = Array.Empty<string>();
        public string[] RecipesSeen = Array.Empty<string>();

        public string[] AchievementIds = Array.Empty<string>();
        public int[] AchievementClaimed = Array.Empty<int>();

        /// <summary>Upgrades FCFA déjà vus (badge « nouveau »).</summary>
        public string[] SeenUpgrades = Array.Empty<string>();
        /// <summary>Fonctionnalités dont la bulle de présentation a été montrée.</summary>
        public int[] IntroducedFeatures = Array.Empty<int>();

        public double OfflineCapExtensionRemainingSeconds;
        public bool NotificationsEnabled = true;
        public bool NotificationPermissionAsked;
    }

    [Serializable]
    public sealed class LedgerSaveData
    {
        public long[] Balances = Array.Empty<long>();
        public long[] TotalEarned = Array.Empty<long>();
        public long[] TotalSpent = Array.Empty<long>();
        public string[] CardIds = Array.Empty<string>();
        public long[] CardCounts = Array.Empty<long>();
        public string[] EarnedKeys = Array.Empty<string>();
        public long[] EarnedValues = Array.Empty<long>();
        public string[] SpentKeys = Array.Empty<string>();
        public long[] SpentValues = Array.Empty<long>();
        public long MovementCount;
    }

    [Serializable]
    public sealed class StatsSaveData
    {
        public double[] Global = Array.Empty<double>();
        public string[] BusinessIds = Array.Empty<string>();
        public double[] Customers = Array.Empty<double>();
        public long[] MaxLevels = Array.Empty<long>();
        public int[] MaxMilestones = Array.Empty<int>();
        public bool[] FirstServed = Array.Empty<bool>();
    }

    [Serializable]
    public sealed class ManagersSaveData
    {
        public string[] Ids = Array.Empty<string>();
        public int[] Levels = Array.Empty<int>();
        public double[] BoostLeft = Array.Empty<double>();
        public double[] CooldownLeft = Array.Empty<double>();
        public string[] EquippedBusinesses = Array.Empty<string>();
        public string[] EquippedManagers = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ChestsSaveData
    {
        public int FreeStored;
        public double FreeProgress;
        public int RewardedDayKey = int.MinValue;
        public int RewardedToday;
        public long Opened;
        public bool FirstFreeClaimed;
        public string RandomState = "";
        public string[] PityIds = Array.Empty<string>();
        public int[] PityCounts = Array.Empty<int>();
    }

    [Serializable]
    public sealed class MissionSaveData
    {
        public string TemplateId;
        public int Kind;
        public string BusinessId = "";
        public double Target;
        public double Baseline;
        public long Xp, Cauris, Tokens;
        public string ChestId = "";
        public bool Claimed;
    }

    [Serializable]
    public sealed class DailySaveData
    {
        public int LastClaimDayKey = int.MinValue;
        public double NextClaimTrustedSeconds;
        public int CalendarIndex;
        public int Streak, BestStreak, Shields;
        public int ShieldWeekKey = int.MinValue;
        public int TotalLoginDays;
        public int WheelDayKey = int.MinValue;
        public int WheelFreeUsed, WheelRewardedUsed;
        public long WheelSpins;
        public string RandomState = "";
    }
}

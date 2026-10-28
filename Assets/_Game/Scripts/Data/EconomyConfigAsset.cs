using Gargote.Core;
using UnityEngine;

namespace Gargote.Data
{
    [CreateAssetMenu(menuName = "Gargote/Economy Config", fileName = "EconomyConfig")]
    public sealed class EconomyConfigAsset : ScriptableObject
    {
        [Header("Paliers")]
        public long[] milestones = { 25, 50, 100, 200, 300, 400, 500, 750, 1000 };
        public double milestoneSpeedMultiplier = 2;
        public double minCycleSeconds = 0.1;
        [Tooltip("Quand tous les commerces atteignent ce niveau : revenus x globalMilestoneMultiplier")]
        public long[] globalMilestones = { 25, 50, 100, 200, 300, 400, 500 };
        public double globalMilestoneMultiplier = 1.5;

        [Header("Jour / nuit")]
        public double dayLengthSeconds = 1440;
        public double morningStartHour = 6;
        public double morningEndHour = 11;
        public double nightStartHour = 19;
        public double nightEndHour = 2;
        public double affinityMultiplier = 2;

        [Header("Hors ligne")]
        public double offlineBaseCapSeconds = 7200;
        public double offlineMaxCapSeconds = 43200;
        public double offlineEfficiency = 1;
        public double offlineAdMultiplier = 2;

        [Header("Boost rewarded")]
        public double adBoostMultiplier = 2;
        public double adBoostDurationSeconds = 14400;
        public double adBoostMaxStackSeconds = 28800;
        [Tooltip("Le boost s'applique-t-il aussi aux gains hors ligne ?")]
        public bool adBoostAppliesOffline;

        [Header("Rewarded")]
        [Tooltip("Rewarded à pleine valeur par jour ; au-delà, chacune vaut rewardedDecayPerExtraAd fois la précédente")]
        public int rewardedFullValuePerDay = 8;
        public double rewardedDecayPerExtraAd = 0.5;

        public EconomyConfig ToConfig() => new EconomyConfig
        {
            Milestones = (long[])milestones.Clone(),
            MilestoneSpeedMultiplier = milestoneSpeedMultiplier,
            MinCycleSeconds = minCycleSeconds,
            GlobalMilestones = (long[])(globalMilestones ?? new long[0]).Clone(),
            GlobalMilestoneMultiplier = globalMilestoneMultiplier,
            DayNight = new DayNightConfig
            {
                DayLengthSeconds = dayLengthSeconds,
                MorningStartHour = morningStartHour,
                MorningEndHour = morningEndHour,
                NightStartHour = nightStartHour,
                NightEndHour = nightEndHour,
                AffinityMultiplier = affinityMultiplier,
            },
            Offline = new OfflineConfig
            {
                BaseCapSeconds = offlineBaseCapSeconds,
                MaxCapSeconds = offlineMaxCapSeconds,
                Efficiency = offlineEfficiency,
                AdMultiplier = offlineAdMultiplier,
            },
            AdBoost = new AdBoostConfig
            {
                Multiplier = adBoostMultiplier,
                DurationSeconds = adBoostDurationSeconds,
                MaxStackSeconds = adBoostMaxStackSeconds,
                AppliesOffline = adBoostAppliesOffline,
            },
            Rewarded = new RewardedConfig
            {
                FullValuePerDay = rewardedFullValuePerDay,
                DecayPerExtraAd = rewardedDecayPerExtraAd,
            },
        };
    }
}

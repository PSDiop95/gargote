using Gargote.Core;

namespace Gargote.Tests
{
    /// <summary>Définitions minimales et déterministes, indépendantes de l'équilibrage réel.</summary>
    internal static class TestData
    {
        public static BusinessDefinition Business(string id, double cost = 10, double growth = 1.1, double revenue = 1,
            double cycle = 1, double manager = 100, TimeAffinity affinity = TimeAffinity.None) => new BusinessDefinition
        {
            Id = id, BaseCost = cost, Growth = growth, BaseRevenue = revenue,
            BaseCycleSeconds = cycle, ManagerCost = manager, Affinity = affinity,
        };

        public static EconomyConfig Economy() => new EconomyConfig
        {
            Milestones = new long[] { 25, 50, 100 },
            MilestoneSpeedMultiplier = 2,
            MinCycleSeconds = 0.1,
            DayNight = new DayNightConfig
            {
                DayLengthSeconds = 1440, MorningStartHour = 6, MorningEndHour = 11,
                NightStartHour = 19, NightEndHour = 2, AffinityMultiplier = 2,
            },
            Offline = new OfflineConfig { BaseCapSeconds = 7200, MaxCapSeconds = 43200, Efficiency = 1, AdMultiplier = 2 },
            AdBoost = new AdBoostConfig { Multiplier = 2, DurationSeconds = 14400, MaxStackSeconds = 86400 },
        };

        public static GameDefinitions Definitions(double startingMoney = 0, int cities = 2)
        {
            var defs = new GameDefinitions
            {
                Economy = Economy(),
                Prestige = new PrestigeConfig { K = 1, Threshold = 100, BonusPerStar = 0.02, MinStarsToPrestige = 1 },
                Cities = new CityDefinition[cities],
            };
            for (int c = 0; c < cities; c++)
            {
                defs.Cities[c] = new CityDefinition
                {
                    Id = "city" + c,
                    GoalLifetimeEarnings = 10000,
                    StartingMoney = startingMoney,
                    Businesses = new[]
                    {
                        Business("a", cost: 10, growth: 1.1, revenue: 1, cycle: 1, manager: 100),
                        Business("b", cost: 100, growth: 1.15, revenue: 20, cycle: 4, manager: 1000, affinity: TimeAffinity.Night),
                    },
                };
            }
            return defs;
        }
    }
}

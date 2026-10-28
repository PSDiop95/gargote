using Gargote.Core;
using Gargote.Simulation;
using Gargote.Systems;
using NUnit.Framework;

namespace Gargote.Tests
{
    /// <summary>Mécaniques ajoutées par la passe d'équilibrage M1.</summary>
    public class BalancingMechanicsTests
    {
        [Test]
        public void Rewarded_Lose_Value_Beyond_Daily_Quota()
        {
            var cfg = new RewardedConfig { FullValuePerDay = 8, DecayPerExtraAd = 0.5 };
            Assert.That(RewardedRules.Strength(0, cfg), Is.EqualTo(1));
            Assert.That(RewardedRules.Strength(7, cfg), Is.EqualTo(1));
            Assert.That(RewardedRules.Strength(8, cfg), Is.EqualTo(0.5));
            Assert.That(RewardedRules.Strength(9, cfg), Is.EqualTo(0.25));
            Assert.That(RewardedRules.ScaledMultiplier(2, 0.5), Is.EqualTo(1.5));
        }

        [Test]
        public void Rewarded_Tracker_Resets_Each_Day()
        {
            var t = new RewardedTracker(new RewardedConfig { FullValuePerDay = 2, DecayPerExtraAd = 0.5 });
            Assert.That(t.Register(1), Is.EqualTo(1));
            Assert.That(t.Register(1), Is.EqualTo(1));
            Assert.That(t.NextStrength(1), Is.EqualTo(0.5));
            Assert.That(t.Register(1), Is.EqualTo(0.5));
            Assert.That(t.NextStrength(2), Is.EqualTo(1), "nouveau jour");
            Assert.That(t.Register(2), Is.EqualTo(1));
            Assert.That(t.CountToday, Is.EqualTo(1));
        }

        [Test]
        public void Offline_Ad_Multiplier_Decays_After_Quota()
        {
            var defs = TestData.Definitions();
            defs.Economy.Rewarded = new RewardedConfig { FullValuePerDay = 1, DecayPerExtraAd = 0.5 };
            var s = new GameSession(defs);
            Assert.That(s.RewardOfflineAd(10), Is.EqualTo(2));
            Assert.That(s.RewardOfflineAd(10), Is.EqualTo(1.5));
        }

        [Test]
        public void Boost_Stack_Is_Capped_And_Excess_Is_Lost()
        {
            var defs = TestData.Definitions();
            defs.Economy.AdBoost = new AdBoostConfig { Multiplier = 2, DurationSeconds = 14400, MaxStackSeconds = 28800 };
            var s = new GameSession(defs);
            Assert.That(s.RewardBoostAd(1));
            Assert.That(s.RewardBoostAd(1));
            Assert.That(s.Economy.AdBoostRemainingSeconds, Is.EqualTo(28800).Within(1e-6), "plafond 8 h");
            Assert.That(s.RewardBoostAd(1), Is.False);
            Assert.That(s.Rewarded.CountToday, Is.EqualTo(2), "une pub refusée n'est pas comptée");

            s.Economy.ConsumeBoostTime(3600);
            Assert.That(s.RewardBoostAd(1));
            Assert.That(s.Economy.AdBoostRemainingSeconds, Is.EqualTo(28800).Within(1e-6), "l'excédent est perdu");
        }

        [Test]
        public void Star_Soft_Cap_Gives_Diminishing_Returns()
        {
            var cfg = new PrestigeConfig { BonusPerStar = 0.02, SoftCapStars = 100, SoftCapExponent = 0.5 };
            Assert.That(PrestigeCalculator.EffectiveStars(50d, cfg), Is.EqualTo(50));
            Assert.That(PrestigeCalculator.EffectiveStars(400d, cfg), Is.EqualTo(200).Within(1e-9));
            Assert.That(PrestigeCalculator.RevenueMultiplier(400, cfg), Is.EqualTo(5).Within(1e-9));
        }

        [Test]
        public void Stars_From_Previous_Cities_Count_Partially()
        {
            var defs = TestData.Definitions(0, 3);
            defs.Prestige.CityCarryOver = 0.25;
            var s = new GameSession(defs);
            s.Economy.Earn(BigDouble.FromDouble(10000)); // seuil test 100 → 10 étoiles, objectif de ville atteint
            Assert.That(s.PerformPrestige(true));
            Assert.That(s.Prestige.TotalStars, Is.EqualTo(10));
            Assert.That(s.Prestige.CurrentCityStars, Is.EqualTo(0));
            Assert.That(s.Prestige.RevenueMultiplier, Is.EqualTo(1 + 10 * 0.25 * 0.02).Within(1e-12));

            s.Economy.Earn(BigDouble.FromDouble(400)); // 2 étoiles dans la nouvelle ville
            Assert.That(s.PerformPrestige(false));
            Assert.That(s.Prestige.CurrentCityStars, Is.EqualTo(2));
            Assert.That(s.Economy.PermanentMultiplier, Is.EqualTo(1 + (2 + 10 * 0.25) * 0.02).Within(1e-12));
        }

        [Test]
        public void City_Star_Threshold_Overrides_Global()
        {
            var defs = TestData.Definitions(0, 2);
            defs.Cities[1].StarThreshold = 10000;
            var s = new GameSession(defs);
            s.Economy.Earn(BigDouble.FromDouble(10000));
            s.PerformPrestige(true);
            s.Economy.Earn(BigDouble.FromDouble(40000));
            Assert.That(s.StarsToGain, Is.EqualTo(2), "sqrt(40000 / 10000)");
        }

        [Test]
        public void Global_Milestone_Multiplies_All_Revenue()
        {
            var defs = TestData.Definitions(1e9);
            defs.Economy.GlobalMilestones = new long[] { 10, 20 };
            defs.Economy.GlobalMilestoneMultiplier = 3;
            var s = new GameSession(defs);
            int raised = 0;
            s.Businesses.GlobalMilestoneReached += c => raised = c;

            s.Businesses.BuyLevels(0, 10);
            Assert.That(s.Businesses.GlobalMilestonesReached, Is.EqualTo(0), "le commerce b est encore au niveau 0");
            s.Businesses.BuyLevels(1, 10);
            Assert.That(raised, Is.EqualTo(1));
            Assert.That(s.Businesses.GlobalMilestoneMultiplier, Is.EqualTo(3));

            s.Businesses.HireManager(0);
            var before = s.Economy.Money;
            s.Tick(1, 12);
            Assert.That((s.Economy.Money - before).ToDouble(), Is.EqualTo(10 * 3).Within(1e-6));
        }

        [Test]
        public void Advisor_Can_Suggest_Global_Milestone()
        {
            var defs = TestData.Definitions(1e12);
            defs.Economy.GlobalMilestones = new long[] { 5 };
            defs.Economy.GlobalMilestoneMultiplier = 100;
            var s = new GameSession(defs);
            s.Businesses.BuyLevels(0, 4);
            s.Businesses.BuyLevels(1, 4);
            var best = PurchaseAdvisor.FindBestLevels(s.Businesses, s.Economy, s.EconomyConfig);
            Assert.That(best.IsGlobalMilestone, "x100 sur tout bat n'importe quel niveau isolé");
            Assert.That(PurchaseAdvisor.Apply(s.Businesses, s.Economy, best));
            Assert.That(s.Businesses.MinLevel(), Is.EqualTo(5));
        }

        [Test]
        public void New_Save_Fields_Round_Trip()
        {
            var s = new GameSession(TestData.Definitions(0, 2));
            s.RewardBoostAd(20261028);
            s.Economy.Earn(BigDouble.FromDouble(10000));
            s.PerformPrestige(false);
            var data = s.ToSaveData(System.DateTime.UtcNow, 0);

            var restored = new GameSession(TestData.Definitions(0, 2));
            restored.LoadFrom(data);
            Assert.That(restored.Rewarded.DayKey, Is.EqualTo(20261028));
            Assert.That(restored.Rewarded.CountToday, Is.EqualTo(1));
            Assert.That(restored.Prestige.CurrentCityStars, Is.EqualTo(s.Prestige.CurrentCityStars));
            Assert.That(restored.Economy.PermanentMultiplier, Is.EqualTo(s.Economy.PermanentMultiplier).Within(1e-12));
        }

        [Test]
        public void Proxy_Cities_Get_Harder_Each_Step()
        {
            var defs = EconomySimulator.WithProxyCities(TestData.Definitions(10, 1), 3,
                new ProxyCityScaling { CostMultiplier = 10, RevenueMultiplier = 10, GoalMultiplier = 100, StarThresholdMultiplier = 10 });
            Assert.That(defs.Cities.Length, Is.EqualTo(3));
            Assert.That(defs.Cities[2].Businesses[0].BaseCost, Is.EqualTo(10 * 100).Within(1e-9));
            Assert.That(defs.Cities[2].Businesses[0].BaseRevenue, Is.EqualTo(1 * 100).Within(1e-9));
            Assert.That(defs.Cities[2].GoalLifetimeEarnings, Is.EqualTo(10000 * 1e4).Within(1e-3));
            Assert.That(defs.Cities[1].StarThreshold, Is.EqualTo(100 * 10).Within(1e-9));
        }

        [Test]
        public void Suboptimal_Profiles_Run_Deterministically()
        {
            var defs = EconomySimulator.WithProxyCities(TestData.Definitions(10, 1), 2);
            foreach (var policy in new[] { PurchasePolicy.Cheapest, PurchasePolicy.Random })
            {
                var profile = new PlayerProfile
                {
                    Name = policy.ToString(), SessionsPerDay = 3, SessionMinutes = 5, FirstSessionMinutes = 10,
                    WatchesAds = true, Purchases = policy, BoostSkipChance = 0.5, Seed = 42,
                };
                var settings = new SimulationSettings { Days = 2, Profiles = new[] { profile } };
                var a = new EconomySimulator(defs, settings).Run().Profiles[0];
                var b = new EconomySimulator(defs, settings).Run().Profiles[0];
                Assert.That(a.Snapshots[a.Snapshots.Count - 1].TotalLifetimeEarnings,
                    Is.EqualTo(b.Snapshots[b.Snapshots.Count - 1].TotalLifetimeEarnings), policy.ToString());
                Assert.That(a.FirstEvent("unlock"), Is.Not.Null);
            }
        }
    }
}

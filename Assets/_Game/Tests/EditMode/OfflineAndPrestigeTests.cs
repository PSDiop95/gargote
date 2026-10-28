using System;
using Gargote.Core;
using Gargote.Systems;
using NUnit.Framework;

namespace Gargote.Tests
{
    public class OfflineAndPrestigeTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Offline_Time_Uses_Utc_And_Is_Bounded_By_Monotonic_Clock()
        {
            Assert.That(OfflineCalculator.ResolveElapsedSeconds(T0, T0.AddHours(1), 100, 3700), Is.EqualTo(3600).Within(1e-9));
            // Heure système avancée de 10 h sans redémarrage : seul le temps monotone compte.
            Assert.That(OfflineCalculator.ResolveElapsedSeconds(T0, T0.AddHours(10), 100, 700), Is.EqualTo(600).Within(1e-9));
        }

        [Test]
        public void Offline_Time_Ignores_Clock_Rollback()
        {
            Assert.That(OfflineCalculator.ResolveElapsedSeconds(T0, T0.AddHours(-3), 100, 5000), Is.EqualTo(0));
        }

        [Test]
        public void Offline_Time_After_Reboot_Falls_Back_To_Utc()
        {
            Assert.That(OfflineCalculator.ResolveElapsedSeconds(T0, T0.AddHours(2), 50000, 30), Is.EqualTo(7200).Within(1e-9));
            Assert.That(OfflineCalculator.ResolveElapsedSeconds(T0, T0.AddHours(2), -1, 30), Is.EqualTo(7200).Within(1e-9));
        }

        [Test]
        public void Timezone_Change_Does_Not_Penalize()
        {
            // Les deux instants sont en UTC : un changement de fuseau ne modifie pas le delta.
            var local = new DateTime(2026, 1, 1, 14, 0, 0, DateTimeKind.Local);
            var before = local.ToUniversalTime();
            Assert.That(OfflineCalculator.ResolveElapsedSeconds(before, before.AddMinutes(30), -1, 0), Is.EqualTo(1800).Within(1e-9));
        }

        [Test]
        public void Offline_Earnings_Are_Capped()
        {
            var r = OfflineCalculator.Compute(BigDouble.FromDouble(10), 10 * 3600, 7200, 1, 0, 2);
            Assert.That(r.CreditedSeconds, Is.EqualTo(7200));
            Assert.That(r.Earnings.ToDouble(), Is.EqualTo(72000).Within(1e-6));
        }

        [Test]
        public void Offline_Boost_Applies_To_Covered_Portion_Only()
        {
            var r = OfflineCalculator.Compute(BigDouble.FromDouble(10), 3600, 7200, 1, 600, 2);
            Assert.That(r.Earnings.ToDouble(), Is.EqualTo(10 * (3600 + 600)).Within(1e-6));
        }

        [Test]
        public void Session_Offline_Counts_Only_Managed_Businesses()
        {
            var s = new GameSession(TestData.Definitions(1e6));
            s.Businesses.Buy(0, BuyMode.One);
            s.Businesses.Buy(1, BuyMode.One); // non géré : ne rapporte rien hors ligne
            s.Businesses.HireManager(0);
            var money = s.Economy.Money;

            var offline = s.ComputeOffline(100, s.EconomyConfig.Offline.BaseCapSeconds);
            Assert.That(offline.Earnings.ToDouble(), Is.EqualTo(100).Within(1e-6));
            s.ClaimOffline(offline, s.EconomyConfig.Offline.AdMultiplier);
            Assert.That((s.Economy.Money - money).ToDouble(), Is.EqualTo(200).Within(1e-6));
        }

        [Test]
        public void Stars_Follow_Square_Root()
        {
            var cfg = new PrestigeConfig { K = 1, Threshold = 1e9, BonusPerStar = 0.02 };
            Assert.That(PrestigeCalculator.StarsFor(BigDouble.FromDouble(1e9), cfg), Is.EqualTo(1));
            Assert.That(PrestigeCalculator.StarsFor(BigDouble.FromDouble(4e11), cfg), Is.EqualTo(20));
            Assert.That(PrestigeCalculator.StarsFor(BigDouble.FromDouble(5e8), cfg), Is.EqualTo(0));
            Assert.That(PrestigeCalculator.StarsToGain(BigDouble.FromDouble(4e11), 15, cfg), Is.EqualTo(5));
            Assert.That(PrestigeCalculator.RevenueMultiplier(34, cfg), Is.EqualTo(1.68).Within(1e-12));
            Assert.That(PrestigeCalculator.StarsFor(BigDouble.FromLog10(1000), cfg), Is.GreaterThan(0));
        }

        [Test]
        public void Free_Prestige_Resets_City_And_Keeps_Stars()
        {
            var s = new GameSession(TestData.Definitions(1e6)); // seuil test : 100 → sqrt(1e6/100) = 100 étoiles max
            s.Businesses.BuyLevels(0, 30);
            s.Economy.Earn(BigDouble.FromDouble(1e6));
            Assert.That(s.CanPrestige);
            long gain = s.StarsToGain;
            Assert.That(s.PerformPrestige(false));

            Assert.That(s.Prestige.TotalStars, Is.EqualTo(gain));
            Assert.That(s.Businesses[0].Level, Is.EqualTo(0));
            Assert.That(s.Economy.Money.ToDouble(), Is.EqualTo(1e6).Within(1e-6), "argent de départ de la ville");
            Assert.That(s.Economy.PermanentMultiplier, Is.EqualTo(1 + gain * 0.02).Within(1e-12));
            Assert.That(s.StarsToGain, Is.EqualTo(0), "les étoiles déjà réclamées ne se regagnent pas");
            Assert.That(s.CityIndex, Is.EqualTo(0));
        }

        [Test]
        public void Moving_To_Next_City_Requires_Goal()
        {
            var s = new GameSession(TestData.Definitions(0));
            Assert.That(s.PerformPrestige(true), Is.False);
            s.Economy.Earn(BigDouble.FromDouble(20000));
            Assert.That(s.IsCityGoalReached);

            CityDefinition entered = null;
            s.CityChanged += b => entered = s.City;
            Assert.That(s.PerformPrestige(true));
            Assert.That(s.CityIndex, Is.EqualTo(1));
            Assert.That(entered.Id, Is.EqualTo("city1"));
            Assert.That(s.Economy.CityLifetimeEarnings.IsZero);
            Assert.That(s.Economy.TotalLifetimeEarnings.ToDouble(), Is.EqualTo(20000).Within(1e-6));
            Assert.That(s.HasNextCity, Is.False);
        }
    }
}

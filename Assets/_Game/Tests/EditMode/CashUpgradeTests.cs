using Gargote.Core;
using Gargote.Systems;
using NUnit.Framework;

namespace Gargote.Tests
{
    public class CashUpgradeTests
    {
        private const double Noon = 12;

        private static GameDefinitions Defs(double money)
        {
            var defs = TestData.Definitions(money, 1);
            defs.Cities[0].Upgrades = new[]
            {
                new CashUpgradeDefinition { Id = "a_x3", BusinessIndex = 0, RevenueMultiplier = 3, Cost = 50 },
                new CashUpgradeDefinition { Id = "all_x2", BusinessIndex = -1, RevenueMultiplier = 2, Cost = 500 },
            };
            return defs;
        }

        [Test]
        public void Business_Upgrade_Needs_Unlock_And_Multiplies_Revenue()
        {
            var s = new GameSession(Defs(1e6));
            var b = s.Businesses;
            Assert.That(b.IsUpgradeAvailable(0), Is.False, "commerce verrouillé");
            b.BuyLevels(0, 1);
            b.HireManager(0);
            Assert.That(b.BuyUpgrade(0));
            Assert.That(b.BuyUpgrade(0), Is.False, "déjà acheté");
            var before = s.Economy.Money;
            s.Tick(1, Noon);
            Assert.That((s.Economy.Money - before).ToDouble(), Is.EqualTo(3).Within(1e-9));
        }

        [Test]
        public void Global_Upgrade_Multiplies_Every_Business()
        {
            var s = new GameSession(Defs(1e6));
            var b = s.Businesses;
            b.BuyLevels(0, 1);
            b.HireManager(0);
            var incomeBefore = b.TotalIncomePerSecond();
            Assert.That(b.BuyUpgrade(1));
            Assert.That(b.AllBusinessesMultiplier, Is.EqualTo(2));
            Assert.That((b.TotalIncomePerSecond() / incomeBefore).ToDouble(), Is.EqualTo(2).Within(1e-9));
        }

        [Test]
        public void Upgrades_Reset_On_Replay_And_Survive_Save()
        {
            var s = new GameSession(Defs(1e6));
            s.Businesses.BuyLevels(0, 1);
            s.Businesses.BuyUpgrade(0);
            s.Businesses.BuyUpgrade(1);

            var restored = new GameSession(Defs(1e6));
            restored.LoadFrom(s.ToSaveData(System.DateTime.UtcNow, 0));
            Assert.That(restored.Businesses.OwnsUpgrade(0) && restored.Businesses.OwnsUpgrade(1));
            Assert.That(restored.Businesses[0].LocalRevenueMultiplier, Is.EqualTo(3));
            Assert.That(restored.Businesses.AllBusinessesMultiplier, Is.EqualTo(2));

            s.Economy.Earn(BigDouble.FromDouble(1e6));
            Assert.That(s.PerformPrestige(false));
            Assert.That(s.Businesses.OwnsUpgrade(0), Is.False);
            Assert.That(s.Businesses[0].LocalRevenueMultiplier, Is.EqualTo(1));
            Assert.That(s.Businesses.AllBusinessesMultiplier, Is.EqualTo(1));
        }

        [Test]
        public void Advisor_Suggests_A_Cheap_Strong_Upgrade()
        {
            var s = new GameSession(Defs(1e6));
            s.Businesses.BuyLevels(0, 20);
            var best = PurchaseAdvisor.FindBestLevels(s.Businesses, s.Economy, s.EconomyConfig);
            Assert.That(best.IsUpgrade, "x3 pour 50 FCFA bat n'importe quel niveau");
            Assert.That(PurchaseAdvisor.Apply(s.Businesses, s.Economy, best));
            Assert.That(s.Businesses.OwnsUpgrade(best.UpgradeIndex));
        }

        [Test]
        public void Invalid_Upgrade_Target_Is_Rejected()
        {
            var defs = Defs(0);
            defs.Cities[0].Upgrades[0].BusinessIndex = 9;
            Assert.Throws<System.ArgumentException>(() => new GameSession(defs));
        }
    }
}

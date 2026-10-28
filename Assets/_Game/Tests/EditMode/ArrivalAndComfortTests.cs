using Gargote.Core;
using Gargote.Systems;
using NUnit.Framework;

namespace Gargote.Tests
{
    /// <summary>Arrivée dans une nouvelle ville (plancher, bienvenue) et confort du prestige (upgrades d'étoiles).</summary>
    public class ArrivalAndComfortTests
    {
        private const double Noon = 12;

        private static GameDefinitions Defs(double startingMoney = 0)
        {
            var defs = TestData.Definitions(startingMoney, 3);
            defs.Prestige.CityCarryOver = 0;
            defs.Prestige.TravelMultiplierFloorRatio = 0.3;
            defs.Economy.Welcome = new WelcomeBonusConfig { Multiplier = 3, DurationSeconds = 3600 };
            defs.Prestige.StarUpgrades = new[]
            {
                new StarUpgradeDefinition { Id = "keep", Effect = StarUpgradeEffect.KeepManagers, CostStars = 5 },
                new StarUpgradeDefinition { Id = "auto", Effect = StarUpgradeEffect.AutoBuyFirstMilestone, CostStars = 5 },
                new StarUpgradeDefinition { Id = "rebuy", Effect = StarUpgradeEffect.RebuyPrevious, CostStars = 5 },
            };
            return defs;
        }

        [Test]
        public void Travel_Keeps_Thirty_Percent_Of_Multiplier()
        {
            var s = new GameSession(Defs());
            s.Economy.Earn(BigDouble.FromDouble(1e8)); // 1 000 étoiles : x21
            s.PerformPrestige(false);
            double before = s.Prestige.RevenueMultiplier;
            Assert.That(before, Is.EqualTo(21).Within(1e-9));

            s.Economy.Earn(BigDouble.FromDouble(1e8));
            Assert.That(s.PerformPrestige(true));
            Assert.That(s.Prestige.StarOnlyMultiplier, Is.EqualTo(1), "aucune reprise : sans plancher on retomberait à x1");
            Assert.That(s.Prestige.RevenueMultiplier, Is.EqualTo(0.3 * before).Within(1e-9));
            Assert.That(s.Economy.PermanentMultiplier, Is.EqualTo(0.3 * before).Within(1e-9));
        }

        [Test]
        public void Welcome_Bonus_Triples_Revenue_For_One_Hour()
        {
            var s = new GameSession(Defs());
            s.Economy.Earn(BigDouble.FromDouble(10000));
            s.PerformPrestige(true);
            Assert.That(s.Economy.WelcomeMultiplier, Is.EqualTo(3));
            Assert.That(s.Economy.WelcomeRemainingSeconds, Is.EqualTo(3600));

            s.Economy.Earn(BigDouble.FromDouble(1000));
            s.Businesses.BuyLevels(0, 1);
            s.Businesses.HireManager(0);
            var before = s.Economy.Money;
            s.Tick(1, Noon);
            double perCycle = (s.Economy.Money - before).ToDouble();
            Assert.That(perCycle, Is.EqualTo(1 * s.Economy.PermanentMultiplier * 3).Within(1e-9));

            s.Tick(3600, Noon);
            Assert.That(s.Economy.WelcomeMultiplier, Is.EqualTo(1), "terminé après 1 h");
        }

        [Test]
        public void Offline_Combines_Two_Timed_Bonuses()
        {
            // 1 h d'absence, boost x2 sur 30 min, bienvenue x3 sur 10 min.
            var r = OfflineCalculator.Compute(BigDouble.FromDouble(1), 3600, 7200, 1, 1800, 2, 600, 3);
            double expected = 600 * 6 + 1200 * 2 + 1800 * 1;
            Assert.That(r.Earnings.ToDouble(), Is.EqualTo(expected).Within(1e-6));
        }

        [Test]
        public void Star_Upgrades_Spend_Stars_From_Previous_Cities_First()
        {
            var defs = Defs();
            defs.Prestige.CityCarryOver = 0.5;
            var s = new GameSession(defs);
            s.Economy.Earn(BigDouble.FromDouble(10000)); // 10 étoiles, objectif atteint
            s.PerformPrestige(true);
            s.Economy.Earn(BigDouble.FromDouble(400));   // 2 étoiles dans la ville 2
            s.PerformPrestige(false);
            Assert.That(s.Prestige.EffectiveStars, Is.EqualTo(2 + 0.5 * 10).Within(1e-9));

            Assert.That(s.BuyStarUpgrade(0));
            Assert.That(s.Prestige.AvailableStars, Is.EqualTo(7));
            Assert.That(s.Prestige.EffectiveStars, Is.EqualTo(2 + 0.5 * 5).Within(1e-9), "pris sur les villes précédentes");
            Assert.That(s.BuyStarUpgrade(0), Is.False, "déjà acheté");
            Assert.That(s.Prestige.Has(StarUpgradeEffect.KeepManagers));
        }

        [Test]
        public void Keep_Managers_Survives_Replay()
        {
            var s = new GameSession(Defs(1e6));
            s.Businesses.BuyLevels(0, 10);
            s.Businesses.HireManager(0);
            s.Economy.Earn(BigDouble.FromDouble(1e6));
            s.PerformPrestige(false);
            Assert.That(s.Businesses[0].HasManager, Is.False, "sans upgrade, les gérants partent");

            s.Businesses.BuyLevels(0, 1);
            s.Businesses.HireManager(0);
            Assert.That(s.BuyStarUpgrade(0));
            s.Economy.Earn(BigDouble.FromDouble(1e8));
            s.PerformPrestige(false);
            Assert.That(s.Businesses[0].HasManager);
            Assert.That(s.Businesses[0].IsRunning, Is.False, "niveau 0 : rien ne tourne");
            s.Businesses.BuyLevels(0, 1);
            Assert.That(s.Businesses[0].IsRunning, "le gérant reprend dès le premier niveau");
        }

        [Test]
        public void Auto_Buy_Climbs_To_First_Milestone()
        {
            var s = new GameSession(Defs(1e9));
            s.Economy.Earn(BigDouble.FromDouble(1e6));
            s.PerformPrestige(false);
            s.BuyStarUpgrade(1);
            s.Economy.Earn(BigDouble.FromDouble(1e9));
            s.Tick(1, Noon);
            Assert.That(s.Businesses[0].Level, Is.EqualTo(25));
            Assert.That(s.Businesses[1].Level, Is.EqualTo(25));
            s.Tick(1, Noon);
            Assert.That(s.Businesses[0].Level, Is.EqualTo(25), "jamais au-delà du premier palier");
        }

        [Test]
        public void Rebuy_Restores_Previous_Levels_With_Starting_Fund()
        {
            var defs = Defs(1e6);
            defs.Prestige.RebuyStartingFundSeconds = 1e9;
            var s = new GameSession(defs);
            s.Economy.Earn(BigDouble.FromDouble(1e6));
            s.PerformPrestige(false);
            s.BuyStarUpgrade(2);

            s.Businesses.BuyLevels(0, 40);
            s.Businesses.BuyLevels(1, 12);
            s.Economy.Earn(BigDouble.FromDouble(1e8));
            var income = s.Businesses.TotalIncomePerSecond();
            Assert.That(s.PerformPrestige(false));

            Assert.That(s.PreviousLevels, Is.EqualTo(new long[] { 40, 12 }));
            Assert.That(s.Economy.Money.ApproximatelyEquals(income * 1e9, 1e-6), "fonds = revenu d'avant × secondes");
            Assert.That(s.CanRebuyPrevious);
            Assert.That(s.RebuyPrevious());
            Assert.That(s.Businesses[0].Level, Is.EqualTo(40));
            Assert.That(s.Businesses[1].Level, Is.EqualTo(12));
            Assert.That(s.CanRebuyPrevious, Is.False);
        }

        [Test]
        public void New_Fields_Survive_Save()
        {
            var s = new GameSession(Defs(1e6));
            s.Economy.Earn(BigDouble.FromDouble(1e6));
            s.PerformPrestige(false);
            s.BuyStarUpgrade(2);
            s.Businesses.BuyLevels(0, 3);
            s.Economy.Earn(BigDouble.FromDouble(1e8));
            s.PerformPrestige(false);
            s.Economy.Earn(BigDouble.FromDouble(1e12));
            s.PerformPrestige(true);
            var data = s.ToSaveData(System.DateTime.UtcNow, 0);

            var r = new GameSession(Defs(1e6));
            r.LoadFrom(data);
            Assert.That(r.Prestige.StarsSpent, Is.EqualTo(5));
            Assert.That(r.Prestige.Has(StarUpgradeEffect.RebuyPrevious));
            Assert.That(r.Prestige.MultiplierFloor, Is.EqualTo(s.Prestige.MultiplierFloor).Within(1e-12));
            Assert.That(r.Economy.WelcomeRemainingSeconds, Is.EqualTo(3600));
            Assert.That(r.Economy.PermanentMultiplier, Is.EqualTo(s.Economy.PermanentMultiplier).Within(1e-12));
        }
    }
}

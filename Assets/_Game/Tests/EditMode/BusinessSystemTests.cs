using Gargote.Core;
using Gargote.Systems;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Gargote.Tests
{
    public class BusinessSystemTests
    {
        private const double Noon = 12; // hors des fenêtres matin/nuit

        private static GameSession NewSession(double money) => new GameSession(TestData.Definitions(money));

        [Test]
        public void Tap_Runs_One_Cycle_Then_Stops_Without_Manager()
        {
            var s = NewSession(10);
            var b = s.Businesses;
            Assert.That(b.Tap(0), Is.False, "verrouillé");
            Assert.That(b.Buy(0, BuyMode.One));
            Assert.That(s.Economy.Money.IsZero);

            Assert.That(b.Tap(0));
            Assert.That(b.Tap(0), Is.False, "déjà en cours");
            s.Tick(0.5, Noon);
            Assert.That(s.Economy.Money.IsZero);
            s.Tick(0.6, Noon);
            Assert.That(s.Economy.Money.ToDouble(), Is.EqualTo(1).Within(1e-9));
            Assert.That(b[0].IsRunning, Is.False);
            s.Tick(10, Noon);
            Assert.That(s.Economy.Money.ToDouble(), Is.EqualTo(1).Within(1e-9), "pas de gain sans nouveau tap");
        }

        [Test]
        public void Manager_Automates_And_Handles_Large_Delta()
        {
            var s = NewSession(110);
            var b = s.Businesses;
            b.Buy(0, BuyMode.One);
            Assert.That(b.HireManager(0));
            Assert.That(b.HireManager(0), Is.False);
            s.Tick(10.5, Noon);
            Assert.That(s.Economy.Money.ToDouble(), Is.EqualTo(10).Within(1e-9));
            Assert.That(b[0].Progress, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(b.Tap(0), Is.False, "un commerce géré ne se tape pas");
        }

        [Test]
        public void Manager_Requires_Unlock_And_Money()
        {
            var s = NewSession(1000);
            Assert.That(s.Businesses.HireManager(0), Is.False);
            s.Businesses.Buy(0, BuyMode.One);
            Assert.That(s.Economy.Money.ToDouble(), Is.EqualTo(990).Within(1e-9));
            var poor = NewSession(10);
            poor.Businesses.Buy(0, BuyMode.One);
            Assert.That(poor.Businesses.HireManager(0), Is.False);
        }

        [Test]
        public void Buy_Modes_Quote_Exact_Geometric_Costs()
        {
            var s = NewSession(1e6);
            var b = s.Businesses;
            var ten = b.Quote(0, BuyMode.Ten);
            Assert.That(ten.Count, Is.EqualTo(10));
            Assert.That(ten.Cost.ApproximatelyEquals(EconomyMath.CostForLevels(10, 1.1, 0, 10)));
            Assert.That(b.Buy(0, BuyMode.Ten));
            Assert.That(b[0].Level, Is.EqualTo(10));

            var next = b.Quote(0, BuyMode.NextMilestone);
            Assert.That(next.Count, Is.EqualTo(15));

            var max = b.Quote(0, BuyMode.Max);
            Assert.That(max.Count, Is.EqualTo(EconomyMath.MaxAffordable(10, 1.1, 10, s.Economy.Money)));
            Assert.That(b.Buy(0, BuyMode.Max));
            Assert.That(b.Quote(0, BuyMode.One).Affordable, Is.False);
        }

        [Test]
        public void Max_Mode_Shows_Next_Level_When_Nothing_Affordable()
        {
            var s = NewSession(0);
            var q = s.Businesses.Quote(0, BuyMode.Max);
            Assert.That(q.Count, Is.EqualTo(1));
            Assert.That(q.Affordable, Is.False);
            Assert.That(s.Businesses.Buy(0, BuyMode.Max), Is.False);
        }

        [Test]
        public void Unaffordable_Purchase_Changes_Nothing()
        {
            var s = NewSession(50);
            Assert.That(s.Businesses.Buy(0, BuyMode.Ten), Is.False);
            Assert.That(s.Businesses[0].Level, Is.EqualTo(0));
            Assert.That(s.Economy.Money.ToDouble(), Is.EqualTo(50).Within(1e-9));
        }

        [Test]
        public void Milestone_Doubles_Speed_And_Raises_Event()
        {
            var s = NewSession(1e9);
            var b = s.Businesses;
            int raised = 0;
            b.MilestoneReached += (i, count) => raised = count;
            b.BuyLevels(0, 24);
            Assert.That(b[0].CycleSeconds, Is.EqualTo(1).Within(1e-12));
            b.BuyLevels(0, 1);
            Assert.That(raised, Is.EqualTo(1));
            Assert.That(b[0].CycleSeconds, Is.EqualTo(0.5).Within(1e-12));
            b.Buy(0, BuyMode.NextMilestone);
            Assert.That(b[0].Level, Is.EqualTo(50));
            Assert.That(raised, Is.EqualTo(2));
            Assert.That(b[0].CycleSeconds, Is.EqualTo(0.25).Within(1e-12));
        }

        [Test]
        public void Unlock_Event_Fires_Once()
        {
            var s = NewSession(1000);
            int unlocks = 0;
            s.Businesses.Unlocked += _ => unlocks++;
            s.Businesses.Buy(0, BuyMode.One);
            s.Businesses.Buy(0, BuyMode.One);
            Assert.That(unlocks, Is.EqualTo(1));
        }

        [Test]
        public void Night_Business_Earns_Double_At_Night()
        {
            var day = NewSession(100);
            day.Businesses.Buy(1, BuyMode.One);
            day.Businesses.Tap(1);
            day.Tick(4, Noon);

            var night = NewSession(100);
            night.Businesses.Buy(1, BuyMode.One);
            night.Businesses.Tap(1);
            night.Tick(4, 22);

            Assert.That(night.Economy.Money.ToDouble(), Is.EqualTo(2 * day.Economy.Money.ToDouble()).Within(1e-9));
        }

        [Test]
        public void Ad_Boost_Doubles_Revenue_And_Stacks_To_Cap()
        {
            var s = NewSession(110);
            s.Businesses.Buy(0, BuyMode.One);
            s.Businesses.HireManager(0);
            for (int i = 0; i < 6; i++) Assert.That(s.Economy.TryAddAdBoost(), $"boost {i}");
            Assert.That(s.Economy.TryAddAdBoost(), Is.False, "plafond 24 h");
            s.Tick(10, Noon);
            Assert.That(s.Economy.Money.ToDouble(), Is.EqualTo(20).Within(1e-9));
            Assert.That(s.Economy.AdBoostRemainingSeconds, Is.EqualTo(86400 - 10).Within(1e-6));
        }

        [Test]
        public void Tick_Does_Not_Allocate()
        {
            var s = NewSession(1e6);
            s.Businesses.BuyLevels(0, 30);
            s.Businesses.HireManager(0);
            s.Businesses.BuyLevels(1, 5);
            s.Businesses.Tap(1);
            for (int i = 0; i < 100; i++) s.Tick(0.016, Noon); // échauffement (JIT)

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++) s.Tick(0.016, Noon);
            }, Is.Not.AllocatingGCMemory(), "allocations dans la boucle de jeu");
        }

        [Test]
        public void Purchase_Advisor_Prefers_Best_Return()
        {
            var s = NewSession(1e4);
            s.Businesses.Buy(0, BuyMode.One);
            var best = PurchaseAdvisor.FindBestLevels(s.Businesses, s.Economy, s.EconomyConfig);
            Assert.That(best.IsValid);
            Assert.That(best.Score, Is.GreaterThan(0));
            Assert.That(s.Businesses.BuyLevels(best.BusinessIndex, best.Count));
        }
    }
}

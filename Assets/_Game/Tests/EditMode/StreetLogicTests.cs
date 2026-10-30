using System;
using Gargote.Core;
using Gargote.Systems;
using Gargote.Systems.Street;
using NUnit.Framework;
using static UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Gargote.Tests
{
    public class CrowdDensityTests
    {
        private static readonly CrowdRules Rules = new CrowdRules
        {
            MaxTotal = 20, MaxPerBusiness = 6, MinPerUnlocked = 1, CustomersPerDecade = 1.25, MinVisualServiceSeconds = 0.6,
        };

        [Test]
        public void Closed_Business_Has_No_Customer()
        {
            Assert.AreEqual(0, CrowdDensity.DesiredCustomers(BigDouble.Zero, 1, Rules));
        }

        [Test]
        public void Density_Is_Monotone_And_Capped()
        {
            int previous = 0;
            for (int e = -2; e <= 300; e++)
            {
                int n = CrowdDensity.DesiredCustomers(BigDouble.Parse("1e" + e), 1, Rules);
                Assert.GreaterOrEqual(n, previous, "e=" + e);
                Assert.LessOrEqual(n, Rules.MaxPerBusiness);
                Assert.GreaterOrEqual(n, Rules.MinPerUnlocked);
                previous = n;
            }
            Assert.AreEqual(Rules.MaxPerBusiness, previous);
        }

        [Test]
        public void Density_Handles_Huge_BigDouble()
        {
            var huge = BigDouble.Parse("5e900");
            Assert.AreEqual(Rules.MaxPerBusiness, CrowdDensity.DesiredCustomers(huge, 1, Rules));
        }

        [Test]
        public void Rush_Only_Below_Visual_Speed_And_While_Running()
        {
            Assert.IsFalse(CrowdDensity.IsRush(1.0, true, Rules));
            Assert.IsTrue(CrowdDensity.IsRush(0.2, true, Rules));
            Assert.IsFalse(CrowdDensity.IsRush(0.2, false, Rules));
            Assert.AreEqual(0.6, CrowdDensity.VisualServiceSeconds(0.1, Rules), 1e-9);
            Assert.AreEqual(3.0, CrowdDensity.VisualServiceSeconds(3.0, Rules), 1e-9);
        }

        [Test]
        public void Allocation_Respects_Global_Budget_And_Visibility()
        {
            var desired = new[] { 6, 6, 6, 6, 6, 6, 6, 6 };
            var visible = new[] { true, true, true, true, false, false, true, true };
            var alloc = new int[8];
            int used = CrowdDensity.Allocate(desired, visible, 20, alloc);
            Assert.AreEqual(20, used);
            Assert.AreEqual(0, alloc[4]);
            Assert.AreEqual(0, alloc[5]);
            int sum = 0;
            foreach (var a in alloc) { Assert.LessOrEqual(a, 6); sum += a; }
            Assert.AreEqual(20, sum);
        }

        [Test]
        public void Allocation_Is_Fair_Between_Small_And_Big_Businesses()
        {
            var desired = new[] { 6, 1, 6, 6, 6 };
            var visible = new[] { true, true, true, true, true };
            var alloc = new int[5];
            CrowdDensity.Allocate(desired, visible, 8, alloc);
            Assert.AreEqual(1, alloc[1], "small business keeps its customer");
            Assert.AreEqual(8, alloc[0] + alloc[1] + alloc[2] + alloc[3] + alloc[4]);
        }

        [Test]
        public void Allocation_Does_Not_Allocate()
        {
            var desired = new[] { 3, 6, 2, 6, 6, 1, 0, 6 };
            var visible = new[] { true, true, true, true, true, true, true, false };
            var alloc = new int[8];
            CrowdDensity.Allocate(desired, visible, 20, alloc);
            Assert.That((TestDelegate)(() => CrowdDensity.Allocate(desired, visible, 20, alloc)),
                UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }
    }

    public class DayNightBadgeTests
    {
        private static readonly DayNightConfig Config = TestData.Economy().DayNight;

        [TestCase(TimeAffinity.Morning, 8, AffinityBadge.MorningActive)]
        [TestCase(TimeAffinity.Morning, 14, AffinityBadge.None)]
        [TestCase(TimeAffinity.Night, 22, AffinityBadge.NightActive)]
        [TestCase(TimeAffinity.Night, 1, AffinityBadge.NightActive)]
        [TestCase(TimeAffinity.Night, 9, AffinityBadge.None)]
        [TestCase(TimeAffinity.None, 22, AffinityBadge.None)]
        public void Badge_Matches_Economic_Multiplier(TimeAffinity affinity, double hour, AffinityBadge expected)
        {
            Assert.AreEqual(expected, DayNightBadges.For(affinity, hour, Config));
            bool boosted = DayNight.Multiplier(affinity, hour, Config) > 1d;
            Assert.AreEqual(boosted, expected != AffinityBadge.None);
        }

        [Test]
        public void Visual_Stages_Follow_Thresholds()
        {
            var t = new long[] { 1, 25, 100, 300, 750 };
            Assert.AreEqual(-1, VisualStages.StageFor(0, t));
            Assert.AreEqual(0, VisualStages.StageFor(1, t));
            Assert.AreEqual(0, VisualStages.StageFor(24, t));
            Assert.AreEqual(1, VisualStages.StageFor(25, t));
            Assert.AreEqual(3, VisualStages.StageFor(749, t));
            Assert.AreEqual(4, VisualStages.StageFor(100000, t));
        }
    }

    public class MoneyFlowBatcherTests
    {
        private static readonly MoneyFlowRules Rules = new MoneyFlowRules
        {
            MinBills = 2, MaxBills = 5, MinBurstIntervalSeconds = 0.35, MinGlobalBurstIntervalSeconds = 0.08,
        };

        [Test]
        public void Bills_Between_2_And_5_And_Monotone()
        {
            var income = BigDouble.FromDouble(100);
            int previous = 0;
            foreach (double earned in new[] { 0.0, 1, 50, 100, 250, 800, 1600, 1e6, 1e300 })
            {
                int bills = MoneyFlowBatcher.BillsFor(BigDouble.FromDouble(earned), income, Rules);
                Assert.That(bills, Is.InRange(2, 5));
                Assert.GreaterOrEqual(bills, previous);
                previous = bills;
            }
            Assert.AreEqual(5, previous);
        }

        [Test]
        public void Single_Cycle_Gives_Several_Bills()
        {
            var b = new MoneyFlowBatcher(2, Rules);
            b.Add(0, BigDouble.FromDouble(400));
            Assert.IsTrue(b.TryEmit(0, 10, BigDouble.FromDouble(100), out var burst));
            Assert.IsFalse(burst.IsGrouped);
            Assert.AreEqual(4, burst.Bills);
            Assert.AreEqual(0, b.PendingCycles(0));
        }

        [Test]
        public void Fast_Cycles_Are_Grouped_Into_One_Big_Bill()
        {
            var b = new MoneyFlowBatcher(1, Rules);
            var income = BigDouble.FromDouble(10);
            double t = 0;
            int bursts = 0;
            long cycles = 0;
            BigDouble shown = BigDouble.Zero;
            // 10 cycles par seconde pendant 3 s.
            for (int i = 0; i < 30; i++, t += 0.1)
            {
                b.Add(0, BigDouble.FromDouble(1));
                if (!b.TryEmit(0, t, income, out var burst)) continue;
                bursts++;
                cycles += burst.Cycles;
                shown += burst.Amount;
                if (burst.IsGrouped) Assert.AreEqual(1, burst.Bills);
            }
            Assert.LessOrEqual(bursts, (int)Math.Ceiling(3 / Rules.MinBurstIntervalSeconds) + 1);
            Assert.AreEqual(30, cycles + b.PendingCycles(0), "no cycle lost");
            Assert.LessOrEqual(shown.ToDouble(), 30);
        }

        [Test]
        public void Global_Interval_Throttles_Across_Businesses()
        {
            var b = new MoneyFlowBatcher(3, Rules);
            for (int i = 0; i < 3; i++) b.Add(i, BigDouble.FromDouble(1));
            Assert.IsTrue(b.TryEmit(0, 1.0, BigDouble.FromDouble(1), out _));
            Assert.IsFalse(b.TryEmit(1, 1.01, BigDouble.FromDouble(1), out _));
            Assert.IsTrue(b.TryEmit(1, 1.09, BigDouble.FromDouble(1), out _));
        }

        [Test]
        public void Emit_Does_Not_Allocate()
        {
            var b = new MoneyFlowBatcher(1, Rules);
            var income = BigDouble.FromDouble(10);
            double t = 0;
            b.Add(0, income);
            b.TryEmit(0, t, income, out _);
            Assert.That(() =>
            {
                t += 1;
                b.Add(0, income);
                b.TryEmit(0, t, income, out _);
            }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }
    }

    public class SpecialCustomerSystemTests
    {
        private static SpecialCustomerRules Rules() => new SpecialCustomerRules
        {
            MinIntervalSeconds = 10, MaxIntervalSeconds = 20, MinUnlockedBusinesses = 2,
            Customers = new[]
            {
                new SpecialCustomerDefinition { Id = "fonctionnaire", Weight = 3, BonusIncomeSeconds = 30, MinBonus = 50, CrossingSeconds = 8 },
                new SpecialCustomerDefinition { Id = "delegation_mariage", Weight = 1, BonusIncomeSeconds = 300, MinBonus = 500, CrossingSeconds = 12 },
            },
        };

        private static (GameSession, SpecialCustomerSystem) Create(int unlocked)
        {
            var session = new GameSession(TestData.Definitions(startingMoney: 1e9));
            for (int i = 0; i < unlocked; i++) session.Businesses.Buy(i, BuyMode.One);
            var system = new SpecialCustomerSystem(Rules(), session.Economy, () => session.Businesses, 7);
            return (session, system);
        }

        [Test]
        public void No_Spawn_Before_Enough_Businesses()
        {
            var (_, system) = Create(1);
            for (int i = 0; i < 100; i++) system.Tick(1);
            Assert.AreEqual(-1, system.ActiveIndex);
        }

        [Test]
        public void Spawns_Within_Interval_One_At_A_Time()
        {
            var (_, system) = Create(2);
            int spawns = 0;
            system.Spawned += _ => spawns++;
            double t = 0;
            while (system.ActiveIndex < 0 && t < 21) { system.Tick(0.5); t += 0.5; }
            Assert.That(t, Is.InRange(10, 20.5));
            Assert.AreEqual(1, spawns);
            Assert.IsFalse(system.ForceSpawn(0), "only one special customer at a time");
        }

        [Test]
        public void Tap_Credits_Bonus_Once()
        {
            var (session, system) = Create(2);
            Assert.IsTrue(system.ForceSpawn(1));
            var before = session.Economy.Money;
            Assert.IsTrue(system.TryTap(out var bonus));
            Assert.IsTrue(bonus >= BigDouble.FromDouble(500));
            Assert.IsTrue((session.Economy.Money - before).ApproximatelyEquals(bonus));
            Assert.IsFalse(system.TryTap(out _), "no double reward");
            Assert.AreEqual(-1, system.ActiveIndex);
        }

        [Test]
        public void Bonus_Scales_With_Income()
        {
            var (session, system) = Create(2);
            session.Businesses.HireManager(0);
            session.Businesses.HireManager(1);
            var income = session.Businesses.TotalIncomePerSecond();
            var expected = BigDouble.Max(BigDouble.FromDouble(50), income * 30);
            Assert.IsTrue(system.BonusFor(0).ApproximatelyEquals(expected));
        }

        [Test]
        public void Untapped_Customer_Leaves_Without_Bonus()
        {
            var (session, system) = Create(2);
            int missed = 0;
            system.Missed += _ => missed++;
            system.ForceSpawn(0);
            var before = session.Economy.Money;
            for (int i = 0; i < 10; i++) system.Tick(1);
            Assert.AreEqual(1, missed);
            Assert.AreEqual(-1, system.ActiveIndex);
            Assert.IsTrue(session.Economy.Money == before);
        }

        [Test]
        public void Invalid_Rules_Are_Rejected()
        {
            var rules = Rules();
            rules.MaxIntervalSeconds = 1;
            Assert.Throws<ArgumentException>(rules.Validate);
        }
    }
}

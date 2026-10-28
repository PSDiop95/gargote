using Gargote.Core;
using NUnit.Framework;

namespace Gargote.Tests
{
    public class EconomyMathTests
    {
        private static BigDouble BruteForceCost(double baseCost, double growth, long owned, long count)
        {
            BigDouble sum = BigDouble.Zero;
            for (long i = 0; i < count; i++) sum += EconomyMath.LevelCost(baseCost, growth, owned + i);
            return sum;
        }

        [Test]
        public void Level_Cost_Is_Geometric()
        {
            Assert.That(EconomyMath.LevelCost(100, 1.07, 0).ToDouble(), Is.EqualTo(100).Within(1e-9));
            Assert.That(EconomyMath.LevelCost(100, 1.07, 10).ToDouble(), Is.EqualTo(100 * System.Math.Pow(1.07, 10)).Within(1e-6));
        }

        [TestCase(100, 1.07, 0, 1)]
        [TestCase(100, 1.07, 0, 10)]
        [TestCase(1500, 1.15, 37, 100)]
        [TestCase(4.5e9, 1.09, 512, 250)]
        public void Cost_For_Levels_Matches_Sum(double baseCost, double growth, long owned, long count)
        {
            var closed = EconomyMath.CostForLevels(baseCost, growth, owned, count);
            var brute = BruteForceCost(baseCost, growth, owned, count);
            Assert.That(closed.ApproximatelyEquals(brute, 1e-9), $"{closed} vs {brute}");
        }

        [Test]
        public void Cost_For_Zero_Levels_Is_Zero()
        {
            Assert.That(EconomyMath.CostForLevels(100, 1.1, 5, 0), Is.EqualTo(BigDouble.Zero));
        }

        [TestCase(100, 1.07, 0, 99.0, 0)]
        [TestCase(100, 1.07, 0, 100.0, 1)]
        [TestCase(100, 1.07, 0, 50000.0, -1)]
        [TestCase(1500, 1.15, 40, 1e12, -1)]
        [TestCase(10, 1.1, 1000, 1e60, -1)]
        public void Max_Affordable_Is_Exact(double baseCost, double growth, long owned, double money, long expected)
        {
            long n = EconomyMath.MaxAffordable(baseCost, growth, owned, money);
            if (expected >= 0) Assert.That(n, Is.EqualTo(expected));
            Assert.That(EconomyMath.CostForLevels(baseCost, growth, owned, n) <= BigDouble.FromDouble(money));
            Assert.That(EconomyMath.CostForLevels(baseCost, growth, owned, n + 1) > BigDouble.FromDouble(money));
        }

        [Test]
        public void Milestones()
        {
            long[] ms = { 25, 50, 100 };
            Assert.That(EconomyMath.MilestonesReached(0, ms), Is.EqualTo(0));
            Assert.That(EconomyMath.MilestonesReached(25, ms), Is.EqualTo(1));
            Assert.That(EconomyMath.MilestonesReached(99, ms), Is.EqualTo(2));
            Assert.That(EconomyMath.MilestonesReached(5000, ms), Is.EqualTo(3));
            Assert.That(EconomyMath.NextMilestone(0, ms), Is.EqualTo(25));
            Assert.That(EconomyMath.NextMilestone(25, ms), Is.EqualTo(50));
            Assert.That(EconomyMath.NextMilestone(100, ms), Is.EqualTo(-1));
        }

        [Test]
        public void Speed_Beyond_Minimum_Cycle_Becomes_Revenue()
        {
            double cycle = EconomyMath.EffectiveCycle(1, 4, 0.1, out double overflow);
            Assert.That(cycle, Is.EqualTo(0.25).Within(1e-12));
            Assert.That(overflow, Is.EqualTo(1));

            cycle = EconomyMath.EffectiveCycle(1, 40, 0.1, out overflow);
            Assert.That(cycle, Is.EqualTo(0.1).Within(1e-12));
            Assert.That(overflow, Is.EqualTo(4).Within(1e-9));
        }

        [Test]
        public void Revenue_Per_Second_Is_Preserved_Past_Speed_Cap()
        {
            var config = TestData.Economy();
            config.Milestones = new long[] { 1, 2, 3, 4, 5, 6, 7, 8 }; // 8 paliers : x256
            var def = TestData.Business("x", revenue: 1, cycle: 1);
            var rps = EconomyMath.RevenuePerSecond(def, 8, config);
            Assert.That(rps.ToDouble(), Is.EqualTo(8 * 256).Within(1e-6));
            EconomyMath.RevenuePerCycle(def, 8, config, 1, 1, out double c);
            Assert.That(c, Is.EqualTo(0.1).Within(1e-12));
        }

        [Test]
        public void Day_Night_Windows()
        {
            var c = TestData.Economy().DayNight;
            Assert.That(DayNight.Multiplier(TimeAffinity.Morning, 7, c), Is.EqualTo(2));
            Assert.That(DayNight.Multiplier(TimeAffinity.Morning, 12, c), Is.EqualTo(1));
            Assert.That(DayNight.Multiplier(TimeAffinity.Night, 23, c), Is.EqualTo(2));
            Assert.That(DayNight.Multiplier(TimeAffinity.Night, 1, c), Is.EqualTo(2));
            Assert.That(DayNight.Multiplier(TimeAffinity.Night, 3, c), Is.EqualTo(1));
            Assert.That(DayNight.Multiplier(TimeAffinity.None, 23, c), Is.EqualTo(1));
            Assert.That(DayNight.HourOfDay(1440 + 60, c), Is.EqualTo(1).Within(1e-9));
            Assert.That(DayNight.AverageMultiplier(TimeAffinity.Night, c), Is.EqualTo(1 + 7.0 / 24).Within(1e-12));
        }
    }
}

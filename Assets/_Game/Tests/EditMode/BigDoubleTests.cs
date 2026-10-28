using Gargote.Core;
using NUnit.Framework;

namespace Gargote.Tests
{
    public class BigDoubleTests
    {
        [Test]
        public void Normalizes_Mantissa_Into_One_To_Ten()
        {
            var v = new BigDouble(12345, 0);
            Assert.That(v.Mantissa, Is.EqualTo(1.2345).Within(1e-12));
            Assert.That(v.Exponent, Is.EqualTo(4));

            var small = BigDouble.FromDouble(0.00042);
            Assert.That(small.Mantissa, Is.EqualTo(4.2).Within(1e-12));
            Assert.That(small.Exponent, Is.EqualTo(-4));
        }

        [Test]
        public void Zero_Is_Canonical()
        {
            Assert.That(BigDouble.FromDouble(0).IsZero);
            Assert.That((BigDouble.FromDouble(5) - BigDouble.FromDouble(5)).IsZero);
            Assert.That(BigDouble.FromDouble(0), Is.EqualTo(BigDouble.Zero));
        }

        [Test]
        public void Arithmetic_Matches_Double_In_Range()
        {
            double[] values = { 1, 3.5, 1234.5678, 9.99e15, 0.125, 7e-5, -42, -1e10 };
            foreach (double a in values)
            foreach (double b in values)
            {
                Assert.That((BigDouble.FromDouble(a) + b).ToDouble(), Is.EqualTo(a + b).Within(1e-9 * (System.Math.Abs(a) + System.Math.Abs(b))));
                Assert.That((BigDouble.FromDouble(a) * b).ToDouble(), Is.EqualTo(a * b).Within(1e-9 * System.Math.Abs(a * b)));
                Assert.That((BigDouble.FromDouble(a) / b).ToDouble(), Is.EqualTo(a / b).Within(1e-9 * System.Math.Abs(a / b)));
                Assert.That(BigDouble.FromDouble(a).CompareTo(b), Is.EqualTo(a.CompareTo(b)), $"{a} vs {b}");
            }
        }

        [Test]
        public void Handles_Numbers_Far_Beyond_Double()
        {
            var huge = BigDouble.FromLog10(300) * BigDouble.FromLog10(300);
            Assert.That(huge.Exponent, Is.EqualTo(600));
            Assert.That(huge.Mantissa, Is.EqualTo(1).Within(1e-9));
            Assert.That(double.IsPositiveInfinity(huge.ToDouble()));
            Assert.That(huge > BigDouble.FromLog10(599.9));
            Assert.That((huge / BigDouble.FromLog10(590)).ToDouble(), Is.EqualTo(1e10).Within(1));
        }

        [Test]
        public void Adding_Negligible_Term_Returns_Larger()
        {
            var big = BigDouble.FromLog10(50);
            Assert.That(big + 1, Is.EqualTo(big));
            Assert.That(BigDouble.One + big, Is.EqualTo(big));
        }

        [Test]
        public void Pow_And_Sqrt()
        {
            Assert.That(BigDouble.Pow(1.07, 1000).Log10(), Is.EqualTo(1000 * System.Math.Log10(1.07)).Within(1e-9));
            Assert.That(BigDouble.FromDouble(1e12).Sqrt().ToDouble(), Is.EqualTo(1e6).Within(1e-3));
            Assert.That(BigDouble.FromLog10(1000).Sqrt().Log10(), Is.EqualTo(500).Within(1e-9));
            Assert.That(BigDouble.FromDouble(2).Pow(10).ToDouble(), Is.EqualTo(1024).Within(1e-9));
        }

        [Test]
        public void Negative_Comparisons()
        {
            Assert.That(BigDouble.FromDouble(-1e20) < BigDouble.FromDouble(-1e10));
            Assert.That(BigDouble.FromDouble(-1) < BigDouble.Zero);
            Assert.That(BigDouble.Zero < BigDouble.FromDouble(1e-20));
        }

        [Test]
        public void Floor()
        {
            Assert.That(BigDouble.FromDouble(12.9).Floor().ToDouble(), Is.EqualTo(12));
            Assert.That(BigDouble.FromDouble(0.5).Floor(), Is.EqualTo(BigDouble.Zero));
            var huge = BigDouble.FromLog10(40.5);
            Assert.That(huge.Floor(), Is.EqualTo(huge));
        }

        [TestCase(0d)]
        [TestCase(1d)]
        [TestCase(123456.789)]
        [TestCase(-9.87654321e-7)]
        [TestCase(1.7976931348623157e308)]
        public void Serialization_RoundTrips_Exactly(double value)
        {
            var v = BigDouble.FromDouble(value);
            Assert.That(BigDouble.Parse(v.ToSerializedString()), Is.EqualTo(v));
        }

        [Test]
        public void Serialization_RoundTrips_Huge_Values()
        {
            var v = BigDouble.FromLog10(12345.678);
            Assert.That(BigDouble.Parse(v.ToSerializedString()), Is.EqualTo(v));
            Assert.That(BigDouble.TryParse("abc", out _), Is.False);
            Assert.That(BigDouble.TryParse("NaN", out _), Is.False);
            Assert.That(BigDouble.TryParse("1.5e400", out var parsed));
            Assert.That(parsed.Exponent, Is.EqualTo(400));
        }

        [Test]
        public void Approximate_Equality()
        {
            var a = BigDouble.FromLog10(100);
            Assert.That(a.ApproximatelyEquals(a * 1.0000000001));
            Assert.That(a.ApproximatelyEquals(a * 1.01), Is.False);
        }
    }
}

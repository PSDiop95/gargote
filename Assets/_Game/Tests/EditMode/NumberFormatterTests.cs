using Gargote.Core;
using NUnit.Framework;
using static UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Gargote.Tests
{
    public class NumberFormatterTests
    {
        // Espace insécable ( ) entre milliers et avant le suffixe.
        [TestCase(0d, "0")]
        [TestCase(7.9d, "7")]
        [TestCase(999d, "999")]
        [TestCase(1000d, "1 000")]
        [TestCase(12345d, "12 345")]
        [TestCase(999999.9d, "999 999")]
        [TestCase(1.5e6, "1,5 M")]
        [TestCase(1.234e6, "1,23 M")]
        [TestCase(2.3e9, "2,3 Md")]
        [TestCase(45.6e9, "45,6 Md")]
        [TestCase(4.56e12, "4 560 Md")]
        [TestCase(227.4e12, "227 400 Md")]
        [TestCase(1e15, "1 aa")]
        [TestCase(1e18, "1 ab")]
        [TestCase(-1234d, "-1 234")]
        public void Formats_With_Suffixes(double value, string expected)
        {
            Assert.That(NumberFormatter.Format(BigDouble.FromDouble(value)), Is.EqualTo(expected));
        }

        [Test]
        public void Letter_Suffixes_Roll_Over()
        {
            // tier 5 = aa, tier 30 = az, tier 31 = ba
            Assert.That(NumberFormatter.Suffix(5), Is.EqualTo("aa"));
            Assert.That(NumberFormatter.Suffix(30), Is.EqualTo("az"));
            Assert.That(NumberFormatter.Suffix(31), Is.EqualTo("ba"));
            Assert.That(NumberFormatter.Format(BigDouble.FromLog10(300)), Is.EqualTo("1 " + NumberFormatter.Suffix(100)));
        }

        [Test]
        public void Falls_Back_To_Scientific_Beyond_Letters()
        {
            Assert.That(NumberFormatter.Format(BigDouble.FromLog10(5000)), Is.EqualTo("1e5000"));
        }

        [Test]
        public void Uses_Requested_Separator()
        {
            Assert.That(NumberFormatter.Format(BigDouble.FromDouble(1.234e6), '.'), Is.EqualTo("1.23 M"));
        }
        [Test]
        public void FormatTo_Matches_Format()
        {
            var buffer = new char[NumberFormatter.BufferSize];
            var rng = new System.Random(3);
            for (int i = 0; i < 5000; i++)
            {
                var value = BigDouble.Parse((1 + rng.NextDouble() * 9).ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                                            + "e" + rng.Next(-2, 2100));
                if (i % 7 == 0) value = -value;
                foreach (char sep in new[] { ',', '.' })
                {
                    int n = NumberFormatter.FormatTo(value, buffer, sep);
                    Assert.AreEqual(NumberFormatter.Format(value, sep), new string(buffer, 0, n), value.ToSerializedString());
                }
            }
            Assert.AreEqual("0", new string(buffer, 0, NumberFormatter.FormatTo(BigDouble.Zero, buffer)));
        }

        [Test]
        public void FormatTo_Does_Not_Allocate()
        {
            var buffer = new char[NumberFormatter.BufferSize];
            var value = BigDouble.Parse("4.567e41");
            NumberFormatter.FormatTo(value, buffer);
            Assert.That((TestDelegate)(() => NumberFormatter.FormatTo(value, buffer)),
                UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }
    }
}

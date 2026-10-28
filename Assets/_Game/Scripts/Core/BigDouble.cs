using System;
using System.Globalization;

namespace Gargote.Core
{
    /// <summary>
    /// Grand nombre immuable sans allocation : mantisse double normalisée dans [1, 10) (signée)
    /// et exposant base 10 sur un long. Couvre largement 1e300+ et au-delà.
    /// </summary>
    public readonly struct BigDouble : IComparable<BigDouble>, IEquatable<BigDouble>
    {
        // Au-delà de 17 chiffres d'écart, le plus petit terme est invisible dans une addition.
        private const int MaxSignificantDigits = 17;
        private const int Pow10Min = -300;
        private const int Pow10Max = 308;
        private static readonly double[] Pow10Table = BuildPow10Table();

        public static readonly BigDouble Zero = new BigDouble(0d, 0L, true);
        public static readonly BigDouble One = new BigDouble(1d, 0L, true);

        public readonly double Mantissa;
        public readonly long Exponent;

        private BigDouble(double mantissa, long exponent, bool _)
        {
            Mantissa = mantissa;
            Exponent = exponent;
        }

        public BigDouble(double mantissa, long exponent)
        {
            this = Normalize(mantissa, exponent);
        }

        public bool IsZero => Mantissa == 0d;
        public bool IsNegative => Mantissa < 0d;
        public int Sign => Math.Sign(Mantissa);

        public static BigDouble FromDouble(double value) => Normalize(value, 0L);

        /// <summary>Construit 10^log10.</summary>
        public static BigDouble FromLog10(double log10)
        {
            if (double.IsNegativeInfinity(log10)) return Zero;
            if (double.IsNaN(log10) || double.IsPositiveInfinity(log10))
                throw new ArgumentOutOfRangeException(nameof(log10), "log10 must be finite.");
            double e = Math.Floor(log10);
            return Normalize(Math.Pow(10d, log10 - e), (long)e);
        }

        private static BigDouble Normalize(double mantissa, long exponent)
        {
            if (mantissa == 0d) return Zero;
            if (double.IsNaN(mantissa) || double.IsInfinity(mantissa))
                throw new ArgumentOutOfRangeException(nameof(mantissa), "BigDouble requires a finite value.");

            double abs = Math.Abs(mantissa);
            if (abs >= 1d && abs < 10d) return new BigDouble(mantissa, exponent, true);

            // Sous-normaux : on remonte d'abord pour garder la précision.
            if (abs < 1e-290)
            {
                mantissa *= 1e300;
                exponent -= 300;
                abs = Math.Abs(mantissa);
            }

            int shift = (int)Math.Floor(Math.Log10(abs));
            mantissa /= Pow10(shift);
            exponent += shift;

            abs = Math.Abs(mantissa);
            if (abs >= 10d) { mantissa /= 10d; exponent++; }
            else if (abs < 1d) { mantissa *= 10d; exponent--; }
            return new BigDouble(mantissa, exponent, true);
        }

        private static double[] BuildPow10Table()
        {
            var table = new double[Pow10Max - Pow10Min + 1];
            for (int i = 0; i < table.Length; i++)
                table[i] = double.Parse("1e" + (i + Pow10Min).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            return table;
        }

        private static double Pow10(long power)
        {
            if (power < Pow10Min) return 0d;
            if (power > Pow10Max) return double.PositiveInfinity;
            return Pow10Table[power - Pow10Min];
        }

        public double ToDouble()
        {
            if (Mantissa == 0d) return 0d;
            if (Exponent > Pow10Max) return Mantissa > 0 ? double.PositiveInfinity : double.NegativeInfinity;
            if (Exponent < Pow10Min) return 0d;
            return Mantissa * Pow10(Exponent);
        }

        /// <summary>log10 de la valeur (doit être strictement positive).</summary>
        public double Log10()
        {
            if (Mantissa <= 0d) return double.NegativeInfinity;
            return Math.Log10(Mantissa) + Exponent;
        }

        public BigDouble Pow(double power)
        {
            if (power == 0d) return One;
            if (Mantissa == 0d) return Zero;
            if (Mantissa < 0d) throw new InvalidOperationException("Pow requires a positive BigDouble.");
            return FromLog10(Log10() * power);
        }

        public static BigDouble Pow(double value, double power)
        {
            if (power == 0d) return One;
            if (value <= 0d) throw new ArgumentOutOfRangeException(nameof(value), "Pow requires a positive base.");
            return FromLog10(Math.Log10(value) * power);
        }

        public BigDouble Sqrt() => Pow(0.5d);

        public BigDouble Floor()
        {
            if (Exponent >= MaxSignificantDigits) return this;
            if (Exponent < 0) return Mantissa < 0 ? -One : Zero;
            return FromDouble(Math.Floor(ToDouble()));
        }

        public static BigDouble Max(BigDouble a, BigDouble b) => a.CompareTo(b) >= 0 ? a : b;
        public static BigDouble Min(BigDouble a, BigDouble b) => a.CompareTo(b) <= 0 ? a : b;

        public static BigDouble operator -(BigDouble a) => new BigDouble(-a.Mantissa, a.Exponent, true);

        public static BigDouble operator +(BigDouble a, BigDouble b)
        {
            if (a.Mantissa == 0d) return b;
            if (b.Mantissa == 0d) return a;
            long diff = a.Exponent - b.Exponent;
            if (diff > MaxSignificantDigits) return a;
            if (diff < -MaxSignificantDigits) return b;
            if (diff >= 0) return Normalize(a.Mantissa + b.Mantissa / Pow10(diff), a.Exponent);
            return Normalize(b.Mantissa + a.Mantissa / Pow10(-diff), b.Exponent);
        }

        public static BigDouble operator -(BigDouble a, BigDouble b) => a + -b;

        public static BigDouble operator *(BigDouble a, BigDouble b)
        {
            if (a.Mantissa == 0d || b.Mantissa == 0d) return Zero;
            return Normalize(a.Mantissa * b.Mantissa, a.Exponent + b.Exponent);
        }

        public static BigDouble operator *(BigDouble a, double b)
        {
            if (a.Mantissa == 0d || b == 0d) return Zero;
            return a * FromDouble(b);
        }

        public static BigDouble operator /(BigDouble a, BigDouble b)
        {
            if (b.Mantissa == 0d) throw new DivideByZeroException();
            if (a.Mantissa == 0d) return Zero;
            return Normalize(a.Mantissa / b.Mantissa, a.Exponent - b.Exponent);
        }

        public static BigDouble operator /(BigDouble a, double b) => a / FromDouble(b);

        public static implicit operator BigDouble(double value) => FromDouble(value);

        public static bool operator <(BigDouble a, BigDouble b) => a.CompareTo(b) < 0;
        public static bool operator >(BigDouble a, BigDouble b) => a.CompareTo(b) > 0;
        public static bool operator <=(BigDouble a, BigDouble b) => a.CompareTo(b) <= 0;
        public static bool operator >=(BigDouble a, BigDouble b) => a.CompareTo(b) >= 0;
        public static bool operator ==(BigDouble a, BigDouble b) => a.Equals(b);
        public static bool operator !=(BigDouble a, BigDouble b) => !a.Equals(b);

        public int CompareTo(BigDouble other)
        {
            int sa = Math.Sign(Mantissa);
            int sb = Math.Sign(other.Mantissa);
            if (sa != sb) return sa.CompareTo(sb);
            if (sa == 0) return 0;
            if (Exponent != other.Exponent)
                return sa > 0 ? Exponent.CompareTo(other.Exponent) : other.Exponent.CompareTo(Exponent);
            return Mantissa.CompareTo(other.Mantissa);
        }

        public bool Equals(BigDouble other) => Mantissa.Equals(other.Mantissa) && Exponent == other.Exponent;
        public override bool Equals(object obj) => obj is BigDouble other && Equals(other);
        public override int GetHashCode() => Mantissa.GetHashCode() * 397 ^ Exponent.GetHashCode();

        /// <summary>Égalité relative, pour les tests et les comparaisons tolérantes.</summary>
        public bool ApproximatelyEquals(BigDouble other, double relativeTolerance = 1e-9)
        {
            if (Mantissa == 0d || other.Mantissa == 0d) return Mantissa == other.Mantissa;
            var diff = this - other;
            if (diff.IsZero) return true;
            double scale = Math.Max(Log10Abs(), other.Log10Abs());
            return Log10AbsOf(diff) - scale <= Math.Log10(relativeTolerance);
        }

        private double Log10Abs() => Math.Log10(Math.Abs(Mantissa)) + Exponent;
        private static double Log10AbsOf(BigDouble v) => v.Log10Abs();

        /// <summary>Format de sérialisation invariant et sans perte : "mantisse e exposant".</summary>
        public string ToSerializedString()
        {
            if (Mantissa == 0d) return "0";
            return Mantissa.ToString("R", CultureInfo.InvariantCulture) + "e" + Exponent.ToString(CultureInfo.InvariantCulture);
        }

        public static BigDouble Parse(string text)
        {
            if (!TryParse(text, out var value))
                throw new FormatException("Invalid BigDouble: " + text);
            return value;
        }

        public static bool TryParse(string text, out BigDouble value)
        {
            value = Zero;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            int e = text.IndexOfAny(new[] { 'e', 'E' });
            if (e < 0)
            {
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double plain) ||
                    double.IsNaN(plain) || double.IsInfinity(plain)) return false;
                value = FromDouble(plain);
                return true;
            }
            if (!double.TryParse(text.Substring(0, e), NumberStyles.Float, CultureInfo.InvariantCulture, out double m)) return false;
            if (!long.TryParse(text.Substring(e + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long exp)) return false;
            if (double.IsNaN(m) || double.IsInfinity(m)) return false;
            value = Normalize(m, exp);
            return true;
        }

        public override string ToString() => ToSerializedString();
    }
}

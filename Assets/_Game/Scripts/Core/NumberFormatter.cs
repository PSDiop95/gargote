using System;
using System.Globalization;

namespace Gargote.Core
{
    /// <summary>
    /// Affichage des grands nombres : 999 · 1,23K · 45,6M · 789B · 1,2T · puis aa, ab… az, ba…
    /// Les valeurs sont tronquées (jamais arrondies vers le haut) pour ne pas promettre d'argent absent.
    /// </summary>
    public static class NumberFormatter
    {
        private static readonly string[] NamedSuffixes = { "", "K", "M", "B", "T" };
        private const int LetterSuffixCount = 26 * 26;

        public static string Format(BigDouble value, char decimalSeparator = ',')
        {
            if (value.IsZero) return "0";
            if (value.IsNegative) return "-" + Format(-value, decimalSeparator);

            if (value.Exponent < 3)
                return Math.Floor(value.ToDouble() + 1e-9).ToString("0", CultureInfo.InvariantCulture);

            long tier = value.Exponent / 3;
            double scaled = value.Mantissa * Math.Pow(10d, value.Exponent % 3);

            string suffix = Suffix(tier);
            if (suffix == null)
            {
                double m = Math.Floor(value.Mantissa * 100d + 1e-9) / 100d;
                return WithSeparator(m.ToString("0.##", CultureInfo.InvariantCulture), decimalSeparator)
                       + "e" + value.Exponent.ToString(CultureInfo.InvariantCulture);
            }

            string text;
            if (scaled < 10d) text = (Math.Floor(scaled * 100d + 1e-9) / 100d).ToString("0.##", CultureInfo.InvariantCulture);
            else if (scaled < 100d) text = (Math.Floor(scaled * 10d + 1e-9) / 10d).ToString("0.#", CultureInfo.InvariantCulture);
            else text = Math.Floor(scaled + 1e-9).ToString("0", CultureInfo.InvariantCulture);

            return WithSeparator(text, decimalSeparator) + suffix;
        }

        /// <summary>Suffixe du palier (tier = exposant / 3) ; null au-delà des suffixes à deux lettres.</summary>
        public static string Suffix(long tier)
        {
            if (tier < NamedSuffixes.Length) return NamedSuffixes[tier];
            long index = tier - NamedSuffixes.Length;
            if (index >= LetterSuffixCount) return null;
            return new string(new[] { (char)('a' + index / 26), (char)('a' + index % 26) });
        }

        private static string WithSeparator(string invariant, char separator) =>
            separator == '.' ? invariant : invariant.Replace('.', separator);
    }
}

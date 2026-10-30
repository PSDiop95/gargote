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

        /// <summary>Taille de tampon suffisante pour <see cref="FormatTo"/>.</summary>
        public const int BufferSize = 32;

        /// <summary>
        /// Même texte que <see cref="Format"/>, écrit dans un tampon fourni : aucune allocation
        /// (compteurs qui roulent à chaque frame). Renvoie le nombre de caractères écrits.
        /// </summary>
        public static int FormatTo(BigDouble value, char[] buffer, char decimalSeparator = ',')
        {
            int pos = 0;
            if (value.IsZero) { buffer[pos++] = '0'; return pos; }
            if (value.IsNegative)
            {
                buffer[pos++] = '-';
                value = -value;
            }

            if (value.Exponent < 3)
                return WriteLong(buffer, pos, (long)Math.Floor(value.ToDouble() + 1e-9));

            long tier = value.Exponent / 3;
            long letterIndex = tier - NamedSuffixes.Length;
            if (tier >= NamedSuffixes.Length && letterIndex >= LetterSuffixCount)
            {
                pos = WriteDecimal(buffer, pos, value.Mantissa, 2, decimalSeparator);
                buffer[pos++] = 'e';
                return WriteLong(buffer, pos, value.Exponent);
            }

            double scaled = value.Mantissa * Math.Pow(10d, value.Exponent % 3);
            int decimals = scaled < 10d ? 2 : scaled < 100d ? 1 : 0;
            pos = WriteDecimal(buffer, pos, scaled, decimals, decimalSeparator);
            if (tier < NamedSuffixes.Length)
            {
                string named = NamedSuffixes[tier];
                for (int i = 0; i < named.Length; i++) buffer[pos++] = named[i];
            }
            else
            {
                buffer[pos++] = (char)('a' + letterIndex / 26);
                buffer[pos++] = (char)('a' + letterIndex % 26);
            }
            return pos;
        }

        /// <summary>Écrit <paramref name="number"/> tronqué à <paramref name="decimals"/> décimales, sans zéros finaux.</summary>
        private static int WriteDecimal(char[] buffer, int pos, double number, int decimals, char separator)
        {
            long scale = decimals == 2 ? 100 : decimals == 1 ? 10 : 1;
            long units = (long)Math.Floor(number * scale + 1e-9);
            pos = WriteLong(buffer, pos, units / scale);
            long fraction = units % scale;
            if (fraction == 0) return pos;
            buffer[pos++] = separator;
            if (decimals == 2)
            {
                buffer[pos++] = (char)('0' + fraction / 10);
                if (fraction % 10 != 0) buffer[pos++] = (char)('0' + fraction % 10);
            }
            else buffer[pos++] = (char)('0' + fraction);
            return pos;
        }

        private static int WriteLong(char[] buffer, int pos, long number)
        {
            if (number == 0) { buffer[pos++] = '0'; return pos; }
            int start = pos;
            while (number > 0)
            {
                buffer[pos++] = (char)('0' + number % 10);
                number /= 10;
            }
            Array.Reverse(buffer, start, pos - start);
            return pos;
        }
    }
}

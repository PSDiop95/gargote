using System;
using System.Globalization;

namespace Gargote.Core
{
    /// <summary>
    /// Affichage des montants en FCFA, à la française : 999 · 25 000 · 999 999 en entier, puis 1,23 M · 45,6 Md ·
    /// et les milliers de milliards en milliards groupés (3 600 Md · 227 000 Md, plus parlant que « billion ») ;
    /// au-delà aa, ab… az, ba… (filet de sécurité, l'économie réaliste n'y va pas).
    /// Espaces insécables entre milliers et avant le suffixe. Valeurs tronquées (jamais arrondies vers le haut)
    /// pour ne pas promettre d'argent absent.
    /// </summary>
    public static class NumberFormatter
    {
        /// <summary>Palier 0 et 1 (sous le million) : nombre entier complet, sans suffixe.</summary>
        private static readonly string[] NamedSuffixes = { "", "", "M", "Md", "Md" };
        private const int FullDigitsTiers = 2;
        /// <summary>Palier des milliers de milliards : écrit en milliards entiers groupés.</summary>
        private const int GroupedBillionsTier = 4;
        /// <summary>Espace insécable (séparateur de milliers et avant le suffixe).</summary>
        public const char Space = ' ';
        private const int LetterSuffixCount = 26 * 26;

        public static string Format(BigDouble value, char decimalSeparator = ',')
        {
            if (value.IsZero) return "0";
            if (value.IsNegative) return "-" + Format(-value, decimalSeparator);

            if (value.Exponent < 3 * FullDigitsTiers)
                return Grouped((long)Math.Floor(value.ToDouble() + 1e-9));

            long tier = value.Exponent / 3;
            if (tier == GroupedBillionsTier)
                return Grouped((long)Math.Floor(value.ToDouble() / 1e9 + 1e-9)) + Space + NamedSuffixes[tier];
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

            return WithSeparator(text, decimalSeparator) + Space + suffix;
        }

        private static string Grouped(long number)
        {
            string digits = number.ToString(CultureInfo.InvariantCulture);
            if (digits.Length <= 3) return digits;
            return digits.Substring(0, digits.Length - 3) + Space + digits.Substring(digits.Length - 3);
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

            if (value.Exponent < 3 * FullDigitsTiers)
                return WriteGrouped(buffer, pos, (long)Math.Floor(value.ToDouble() + 1e-9));

            long tier = value.Exponent / 3;
            if (tier == GroupedBillionsTier)
            {
                pos = WriteGrouped(buffer, pos, (long)Math.Floor(value.ToDouble() / 1e9 + 1e-9));
                buffer[pos++] = Space;
                buffer[pos++] = 'M';
                buffer[pos++] = 'd';
                return pos;
            }
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
            buffer[pos++] = Space;
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

        /// <summary>Entier inférieur au million, avec espace insécable entre milliers.</summary>
        private static int WriteGrouped(char[] buffer, int pos, long number)
        {
            if (number < 1000) return WriteLong(buffer, pos, number);
            pos = WriteLong(buffer, pos, number / 1000);
            buffer[pos++] = Space;
            long low = number % 1000;
            buffer[pos++] = (char)('0' + low / 100);
            buffer[pos++] = (char)('0' + low / 10 % 10);
            buffer[pos++] = (char)('0' + low % 10);
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

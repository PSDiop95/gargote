using System;

namespace Gargote.Core
{
    /// <summary>
    /// Générateur pseudo-aléatoire déterministe (SplitMix64), état sérialisable. Le tirage dépend uniquement de l'état
    /// sauvegardé : ni l'heure ni un redémarrage ne permettent de « relancer » un coffre ou la roue.
    /// </summary>
    public sealed class DeterministicRandom
    {
        public ulong State { get; set; }

        public DeterministicRandom(ulong seed) => State = seed;

        public ulong NextULong()
        {
            ulong z = State += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Réel uniforme dans [0, 1).</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);

        /// <summary>Entier uniforme dans [min, maxInclusive].</summary>
        public long Range(long min, long maxInclusive)
        {
            if (maxInclusive <= min) return min;
            ulong span = (ulong)(maxInclusive - min) + 1UL;
            return min + (long)(NextULong() % span);
        }

        public int Next(int count) => count <= 1 ? 0 : (int)(NextULong() % (ulong)count);

        /// <summary>Index tiré selon des poids positifs (−1 si aucun poids).</summary>
        public int Weighted(double[] weights)
        {
            double total = 0d;
            for (int i = 0; i < weights.Length; i++) if (weights[i] > 0d) total += weights[i];
            if (total <= 0d) return -1;
            double roll = NextDouble() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0d) continue;
                if (roll < weights[i]) return i;
                roll -= weights[i];
            }
            for (int i = weights.Length - 1; i >= 0; i--) if (weights[i] > 0d) return i;
            return -1;
        }

        /// <summary>Graine dérivée (flux indépendants par système à partir d'une graine de partie).</summary>
        public static ulong Derive(ulong seed, ulong stream)
        {
            var r = new DeterministicRandom(seed ^ (stream * 0xD1B54A32D192ED03UL));
            return r.NextULong();
        }
    }
}

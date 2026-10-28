using System;

namespace Gargote.Core
{
    /// <summary>
    /// Formules d'économie (§4.1) en forme fermée, sans boucle sur les niveaux.
    /// cost(n) = base × growth^n, où n = niveaux déjà possédés.
    /// </summary>
    public static class EconomyMath
    {
        /// <summary>Prix du prochain niveau quand on en possède déjà <paramref name="owned"/>.</summary>
        public static BigDouble LevelCost(double baseCost, double growth, long owned) =>
            BigDouble.FromDouble(baseCost) * BigDouble.Pow(growth, owned);

        /// <summary>Somme géométrique exacte : base × g^owned × (g^count − 1) / (g − 1).</summary>
        public static BigDouble CostForLevels(double baseCost, double growth, long owned, long count)
        {
            if (count <= 0) return BigDouble.Zero;
            BigDouble first = LevelCost(baseCost, growth, owned);
            BigDouble series = (BigDouble.Pow(growth, count) - BigDouble.One) / (growth - 1d);
            return first * series;
        }

        /// <summary>Nombre maximal de niveaux achetables avec <paramref name="money"/>.</summary>
        public static long MaxAffordable(double baseCost, double growth, long owned, BigDouble money)
        {
            BigDouble first = LevelCost(baseCost, growth, owned);
            if (money < first) return 0;

            // money ≥ first × (g^k − 1)/(g − 1)  ⇔  k ≤ log_g(money × (g − 1) / first + 1)
            BigDouble x = money * (growth - 1d) / first + BigDouble.One;
            double k = Math.Floor(x.Log10() / Math.Log10(growth));
            long n = k < 1 ? 1 : k > long.MaxValue / 2 ? long.MaxValue / 2 : (long)k;

            // Corrige l'imprécision flottante aux bornes (au plus quelques itérations).
            while (n > 0 && CostForLevels(baseCost, growth, owned, n) > money) n--;
            while (CostForLevels(baseCost, growth, owned, n + 1) <= money) n++;
            return n;
        }

        public static int MilestonesReached(long level, long[] milestones)
        {
            int count = 0;
            for (int i = 0; i < milestones.Length; i++)
            {
                if (level >= milestones[i]) count++;
                else break;
            }
            return count;
        }

        /// <summary>Prochain palier strictement supérieur au niveau, ou −1 s'il n'y en a plus.</summary>
        public static long NextMilestone(long level, long[] milestones)
        {
            for (int i = 0; i < milestones.Length; i++)
                if (milestones[i] > level) return milestones[i];
            return -1;
        }

        /// <summary>
        /// Durée de cycle effective. Sous le minimum, le cycle est bloqué et l'excès de vitesse
        /// devient un multiplicateur de revenu (le revenu par seconde est conservé).
        /// </summary>
        public static double EffectiveCycle(double baseCycleSeconds, double speedMultiplier, double minCycleSeconds,
            out double overflowRevenueMultiplier)
        {
            double raw = baseCycleSeconds / speedMultiplier;
            if (raw < minCycleSeconds)
            {
                overflowRevenueMultiplier = minCycleSeconds / raw;
                return minCycleSeconds;
            }
            overflowRevenueMultiplier = 1d;
            return raw;
        }

        public static double SpeedMultiplier(long level, EconomyConfig config, double localSpeedMultiplier = 1d) =>
            Math.Pow(config.MilestoneSpeedMultiplier, MilestonesReached(level, config.Milestones)) * localSpeedMultiplier;

        /// <summary>Revenu par cycle hors multiplicateurs globaux : base × niveau × multiplicateurs locaux.</summary>
        public static BigDouble RevenuePerCycle(BusinessDefinition def, long level, EconomyConfig config,
            double localRevenueMultiplier, double localSpeedMultiplier, out double cycleSeconds)
        {
            cycleSeconds = EffectiveCycle(def.BaseCycleSeconds, SpeedMultiplier(level, config, localSpeedMultiplier),
                config.MinCycleSeconds, out double overflow);
            if (level <= 0) return BigDouble.Zero;
            return BigDouble.FromDouble(def.BaseRevenue) * ((double)level * localRevenueMultiplier * overflow);
        }

        public static BigDouble RevenuePerSecond(BusinessDefinition def, long level, EconomyConfig config,
            double localRevenueMultiplier = 1d, double localSpeedMultiplier = 1d)
        {
            BigDouble perCycle = RevenuePerCycle(def, level, config, localRevenueMultiplier, localSpeedMultiplier, out double cycle);
            return perCycle / cycle;
        }
    }
}

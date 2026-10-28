using Gargote.Core;

namespace Gargote.Systems
{
    public readonly struct PurchaseSuggestion
    {
        /// <summary>Commerce visé, ou <see cref="AllBusinesses"/> pour « tous les commerces jusqu'au palier global ».</summary>
        public readonly int BusinessIndex;
        /// <summary>Niveaux à acheter, ou niveau cible pour un palier global.</summary>
        public readonly long Count;
        public readonly BigDouble Cost;
        /// <summary>Secondes (depuis maintenant) pour que l'achat soit rentabilisé, attente comprise.</summary>
        public readonly double Score;

        public const int AllBusinesses = -2;

        public PurchaseSuggestion(int businessIndex, long count, BigDouble cost, double score)
        {
            BusinessIndex = businessIndex;
            Count = count;
            Cost = cost;
            Score = score;
        }

        public bool IsValid => BusinessIndex >= 0 || BusinessIndex == AllBusinesses;
        public bool IsGlobalMilestone => BusinessIndex == AllBusinesses;
        public static readonly PurchaseSuggestion None = new PurchaseSuggestion(-1, 0, BigDouble.Zero, double.PositiveInfinity);
    }

    /// <summary>
    /// Bouton « Acheter le meilleur » (§10) et politique d'achat de la simulation.
    /// Candidats : +1 niveau et « jusqu'au prochain palier » pour chaque commerce, et « tous les commerces
    /// jusqu'au prochain palier global ». Score = attente pour l'acheter + temps de retour sur investissement.
    /// </summary>
    public static class PurchaseAdvisor
    {
        public static PurchaseSuggestion FindBestLevels(BusinessSystem businesses, EconomySystem economy, EconomyConfig config)
        {
            BigDouble income = businesses.TotalIncomePerSecond();
            double global = economy.GlobalRevenueMultiplier * businesses.GlobalMilestoneMultiplier;
            var best = PurchaseSuggestion.None;

            for (int i = 0; i < businesses.Count; i++)
            {
                var s = businesses[i];
                Consider(economy, config, s, 1, income, global, ref best);
                long next = EconomyMath.NextMilestone(s.Level, config.Milestones);
                if (next > 0 && next - s.Level > 1)
                    Consider(economy, config, s, next - s.Level, income, global, ref best);
            }
            ConsiderGlobalMilestone(businesses, economy, config, income, global, ref best);
            return best;
        }

        /// <summary>Exécute une suggestion. Faux si elle n'est plus abordable.</summary>
        public static bool Apply(BusinessSystem businesses, EconomySystem economy, PurchaseSuggestion suggestion)
        {
            if (!suggestion.IsValid || !economy.CanAfford(suggestion.Cost)) return false;
            if (!suggestion.IsGlobalMilestone) return businesses.BuyLevels(suggestion.BusinessIndex, suggestion.Count);

            bool any = false;
            for (int i = 0; i < businesses.Count; i++)
            {
                long missing = suggestion.Count - businesses[i].Level;
                if (missing > 0) any |= businesses.BuyLevels(i, missing);
            }
            return any;
        }

        private static double Score(EconomySystem economy, BigDouble cost, BigDouble gain, BigDouble income)
        {
            if (gain <= BigDouble.Zero) return double.PositiveInfinity;
            double payback = (cost / gain).ToDouble();
            double wait = 0d;
            if (economy.Money < cost)
            {
                if (income.IsZero) return double.PositiveInfinity;
                wait = ((cost - economy.Money) / income).ToDouble();
            }
            return payback + wait;
        }

        private static void Consider(EconomySystem economy, EconomyConfig config,
            BusinessState s, long count, BigDouble income, double global, ref PurchaseSuggestion best)
        {
            var def = s.Definition;
            BigDouble cost = EconomyMath.CostForLevels(def.BaseCost, def.Growth, s.Level, count);
            BigDouble before = s.RevenuePerSecond;
            BigDouble after = EconomyMath.RevenuePerSecond(def, s.Level + count, config, s.LocalRevenueMultiplier, s.LocalSpeedMultiplier);
            BigDouble gain = (after - before) * (global * DayNight.AverageMultiplier(def.Affinity, config.DayNight));
            double score = Score(economy, cost, gain, income);
            if (score < best.Score) best = new PurchaseSuggestion(s.Index, count, cost, score);
        }

        private static void ConsiderGlobalMilestone(BusinessSystem businesses, EconomySystem economy, EconomyConfig config,
            BigDouble income, double global, ref PurchaseSuggestion best)
        {
            long target = businesses.NextGlobalMilestone();
            if (target <= 0) return;

            BigDouble cost = BigDouble.Zero;
            BigDouble before = BigDouble.Zero;
            BigDouble after = BigDouble.Zero;
            for (int i = 0; i < businesses.Count; i++)
            {
                var s = businesses[i];
                var def = s.Definition;
                double day = DayNight.AverageMultiplier(def.Affinity, config.DayNight);
                long level = s.Level > target ? s.Level : target;
                cost += EconomyMath.CostForLevels(def.BaseCost, def.Growth, s.Level, level - s.Level);
                before += s.RevenuePerSecond * day;
                after += EconomyMath.RevenuePerSecond(def, level, config, s.LocalRevenueMultiplier, s.LocalSpeedMultiplier) * day;
            }
            BigDouble gain = after * (global * config.GlobalMilestoneMultiplier) - before * global;
            double score = Score(economy, cost, gain, income);
            if (score < best.Score) best = new PurchaseSuggestion(PurchaseSuggestion.AllBusinesses, target, cost, score);
        }
    }
}

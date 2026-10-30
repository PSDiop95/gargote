using System;
using Gargote.Core;

namespace Gargote.Systems.Street
{
    /// <summary>
    /// Traduit l'état économique en densité de foule. Pure, sans allocation : la présentation lit ces valeurs,
    /// elle ne calcule rien elle-même. La foule est une représentation plafonnée, jamais un compteur de cycles.
    /// </summary>
    public static class CrowdDensity
    {
        /// <summary>Clients souhaités pour un commerce : monotone en revenu/s, plafonné à MaxPerBusiness.</summary>
        /// <param name="revenuePerSecond">Revenu/s affiché du commerce (jour/nuit inclus).</param>
        /// <param name="baseRevenuePerSecond">Revenu/s du commerce au niveau 1 (référence d'échelle).</param>
        public static int DesiredCustomers(BigDouble revenuePerSecond, double baseRevenuePerSecond, CrowdRules rules)
        {
            if (revenuePerSecond <= BigDouble.Zero) return 0;
            double reference = Math.Max(1e-12, baseRevenuePerSecond);
            double decades = Math.Max(0d, revenuePerSecond.Log10() - Math.Log10(reference));
            double count = rules.MinPerUnlocked + decades * rules.CustomersPerDecade;
            int capped = (int)Math.Min(rules.MaxPerBusiness, Math.Floor(count));
            return Math.Max(Math.Min(rules.MinPerUnlocked, rules.MaxPerBusiness), capped);
        }

        /// <summary>Durée d'un service affiché : suit le cycle, jamais plus rapide que la vitesse visuelle max.</summary>
        public static double VisualServiceSeconds(double cycleSeconds, CrowdRules rules) =>
            Math.Max(cycleSeconds, rules.MinVisualServiceSeconds);

        /// <summary>Mode « rush » : le cycle réel est plus rapide que ce que la foule peut montrer.</summary>
        public static bool IsRush(double cycleSeconds, bool running, CrowdRules rules) =>
            running && cycleSeconds < rules.MinVisualServiceSeconds;

        /// <summary>
        /// Répartit le budget global entre commerces visibles (aucun client hors écran).
        /// Tour par tour : chaque commerce reçoit un client à la fois tant qu'il en veut et qu'il reste du budget,
        /// pour qu'un gros commerce n'affame pas les petits. Aucune allocation.
        /// </summary>
        public static int Allocate(int[] desired, bool[] visible, int totalBudget, int[] allocation)
        {
            int n = desired.Length;
            for (int i = 0; i < n; i++) allocation[i] = 0;
            int remaining = Math.Max(0, totalBudget);
            bool progressed = true;
            while (remaining > 0 && progressed)
            {
                progressed = false;
                for (int i = 0; i < n && remaining > 0; i++)
                {
                    if (!visible[i] || allocation[i] >= desired[i]) continue;
                    allocation[i]++;
                    remaining--;
                    progressed = true;
                }
            }
            return totalBudget - remaining;
        }
    }

    public enum AffinityBadge
    {
        None = 0,
        MorningActive = 1,
        NightActive = 2,
    }

    /// <summary>Badges « x2 MATIN » / « x2 NUIT » : affichés seulement quand le bonus s'applique réellement.</summary>
    public static class DayNightBadges
    {
        public static AffinityBadge For(TimeAffinity affinity, double hourOfDay, DayNightConfig config)
        {
            if (DayNight.Multiplier(affinity, hourOfDay, config) <= 1d) return AffinityBadge.None;
            return affinity == TimeAffinity.Morning ? AffinityBadge.MorningActive : AffinityBadge.NightActive;
        }
    }

    /// <summary>Stades visuels d'un diorama (brouette → étal → bâche → boutique → façade néon).</summary>
    public static class VisualStages
    {
        /// <summary>Index de stade 0..thresholds.Length-1 ; -1 si le commerce est fermé.</summary>
        /// <param name="thresholds">Niveaux d'entrée de chaque stade, croissants ; thresholds[0] est le niveau d'ouverture.</param>
        public static int StageFor(long level, long[] thresholds)
        {
            if (level <= 0 || thresholds == null || thresholds.Length == 0) return -1;
            int stage = 0;
            for (int i = 1; i < thresholds.Length; i++)
                if (level >= thresholds[i]) stage = i;
            return stage;
        }
    }
}

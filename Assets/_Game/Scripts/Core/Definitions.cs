using System;
using System.Collections.Generic;

namespace Gargote.Core
{
    /// <summary>Moment de la journée où un commerce vend le plus (§3.3).</summary>
    public enum TimeAffinity
    {
        None = 0,
        Morning = 1,
        Night = 2,
    }

    /// <summary>
    /// Définitions pures (sans UnityEngine) consommées par la logique et la simulation.
    /// Elles sont produites à partir des ScriptableObjects (et plus tard surchargées par Remote Config).
    /// </summary>
    public sealed class BusinessDefinition
    {
        public string Id;
        public double BaseCost;
        public double Growth;
        public double BaseRevenue;
        public double BaseCycleSeconds;
        public double ManagerCost;
        public TimeAffinity Affinity;

        public void Validate()
        {
            if (string.IsNullOrEmpty(Id)) throw new ArgumentException("Business id is required.");
            if (BaseCost <= 0) throw new ArgumentException(Id + ": BaseCost must be > 0.");
            if (Growth <= 1) throw new ArgumentException(Id + ": Growth must be > 1.");
            if (BaseRevenue <= 0) throw new ArgumentException(Id + ": BaseRevenue must be > 0.");
            if (BaseCycleSeconds <= 0) throw new ArgumentException(Id + ": BaseCycleSeconds must be > 0.");
            if (ManagerCost < 0) throw new ArgumentException(Id + ": ManagerCost must be >= 0.");
        }
    }

    public sealed class CityDefinition
    {
        public string Id;
        public BusinessDefinition[] Businesses = Array.Empty<BusinessDefinition>();
        /// <summary>Objectif de ville : gains cumulés à atteindre pour débloquer la ville suivante.</summary>
        public double GoalLifetimeEarnings;
        public double StartingMoney;
        /// <summary>Seuil d'étoiles propre à la ville (0 = <see cref="PrestigeConfig.Threshold"/>).</summary>
        public double StarThreshold;

        public void Validate()
        {
            if (string.IsNullOrEmpty(Id)) throw new ArgumentException("City id is required.");
            if (Businesses == null || Businesses.Length == 0) throw new ArgumentException(Id + ": no businesses.");
            var ids = new HashSet<string>();
            foreach (var b in Businesses)
            {
                b.Validate();
                if (!ids.Add(b.Id)) throw new ArgumentException(Id + ": duplicate business id " + b.Id);
            }
            if (GoalLifetimeEarnings <= 0) throw new ArgumentException(Id + ": goal must be > 0.");
            if (StartingMoney < 0) throw new ArgumentException(Id + ": starting money must be >= 0.");
        }
    }

    public sealed class DayNightConfig
    {
        public double DayLengthSeconds = 1440;
        public double MorningStartHour = 6;
        public double MorningEndHour = 11;
        public double NightStartHour = 19;
        public double NightEndHour = 2;
        public double AffinityMultiplier = 2;
    }

    public sealed class OfflineConfig
    {
        public double BaseCapSeconds = 7200;
        public double MaxCapSeconds = 43200;
        public double Efficiency = 1;
        public double AdMultiplier = 2;
    }

    public sealed class AdBoostConfig
    {
        public double Multiplier = 2;
        public double DurationSeconds = 14400;
        public double MaxStackSeconds = 28800;
        /// <summary>
        /// Le boost s'applique-t-il aussi aux gains hors ligne ? Faux : il ne sert qu'en jeu actif (et le temps
        /// d'absence le consomme quand même), ce qui évite le double avantage boost + x2 hors ligne.
        /// </summary>
        public bool AppliesOffline = true;
    }

    /// <summary>Valeur des rewarded : pleine jusqu'à N par jour, puis décroissante (§8.1, « sans forcer »).</summary>
    public sealed class RewardedConfig
    {
        public int FullValuePerDay = 8;
        /// <summary>Chaque rewarded au-delà du quota vaut ce facteur fois la précédente.</summary>
        public double DecayPerExtraAd = 0.5;
    }

    public sealed class EconomyConfig
    {
        public long[] Milestones = { 25, 50, 100, 200, 300, 400, 500, 750, 1000 };
        public double MilestoneSpeedMultiplier = 2;
        public double MinCycleSeconds = 0.1;
        /// <summary>Paliers globaux : quand TOUS les commerces atteignent ce niveau, revenus x <see cref="GlobalMilestoneMultiplier"/>.</summary>
        public long[] GlobalMilestones = Array.Empty<long>();
        public double GlobalMilestoneMultiplier = 2;
        public DayNightConfig DayNight = new DayNightConfig();
        public OfflineConfig Offline = new OfflineConfig();
        public AdBoostConfig AdBoost = new AdBoostConfig();
        public RewardedConfig Rewarded = new RewardedConfig();

        public void Validate()
        {
            if (Milestones == null) throw new ArgumentException("Milestones required.");
            for (int i = 1; i < Milestones.Length; i++)
                if (Milestones[i] <= Milestones[i - 1]) throw new ArgumentException("Milestones must be strictly increasing.");
            if (GlobalMilestones == null) GlobalMilestones = Array.Empty<long>();
            for (int i = 1; i < GlobalMilestones.Length; i++)
                if (GlobalMilestones[i] <= GlobalMilestones[i - 1]) throw new ArgumentException("Global milestones must be strictly increasing.");
            if (Rewarded.FullValuePerDay < 0 || Rewarded.DecayPerExtraAd < 0 || Rewarded.DecayPerExtraAd > 1)
                throw new ArgumentException("Rewarded config is inconsistent.");
            if (MilestoneSpeedMultiplier < 1) throw new ArgumentException("MilestoneSpeedMultiplier must be >= 1.");
            if (MinCycleSeconds <= 0) throw new ArgumentException("MinCycleSeconds must be > 0.");
            if (DayNight.DayLengthSeconds <= 0) throw new ArgumentException("DayLengthSeconds must be > 0.");
            if (Offline.BaseCapSeconds < 0 || Offline.MaxCapSeconds < Offline.BaseCapSeconds)
                throw new ArgumentException("Offline caps are inconsistent.");
        }
    }

    public sealed class PrestigeConfig
    {
        /// <summary>Étoiles = K × sqrt(gains_de_la_ville / Threshold).</summary>
        public double K = 1;
        public double Threshold = 1e9;
        public double BonusPerStar = 0.02;
        public long MinStarsToPrestige = 1;
        /// <summary>
        /// Rendements décroissants : au-delà de SoftCapStars, les étoiles comptent comme
        /// SoftCapStars × (étoiles / SoftCapStars)^SoftCapExponent. 0 = pas de plafond doux.
        /// </summary>
        public double SoftCapStars;
        public double SoftCapExponent = 0.5;
        /// <summary>
        /// Étoiles par ville avec reprise partielle : les étoiles des villes précédentes ne comptent qu'à ce ratio
        /// dans la ville en cours (1 = toutes les étoiles comptent pleinement partout).
        /// </summary>
        public double CityCarryOver = 1;

        public void Validate()
        {
            if (K <= 0 || Threshold <= 0) throw new ArgumentException("Prestige K and Threshold must be > 0.");
            if (BonusPerStar < 0) throw new ArgumentException("BonusPerStar must be >= 0.");
            if (SoftCapStars < 0 || SoftCapExponent <= 0 || SoftCapExponent > 1)
                throw new ArgumentException("Star soft cap is inconsistent.");
            if (CityCarryOver < 0 || CityCarryOver > 1) throw new ArgumentException("CityCarryOver must be in [0, 1].");
        }
    }

    /// <summary>Ensemble des définitions d'une partie (villes dans l'ordre de déblocage).</summary>
    public sealed class GameDefinitions
    {
        public CityDefinition[] Cities = Array.Empty<CityDefinition>();
        public EconomyConfig Economy = new EconomyConfig();
        public PrestigeConfig Prestige = new PrestigeConfig();

        public void Validate()
        {
            if (Cities == null || Cities.Length == 0) throw new ArgumentException("At least one city is required.");
            foreach (var c in Cities) c.Validate();
            Economy.Validate();
            Prestige.Validate();
        }
    }
}

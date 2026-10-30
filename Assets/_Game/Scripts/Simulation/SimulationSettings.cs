using System;

namespace Gargote.Simulation
{
    public enum PurchasePolicy
    {
        /// <summary>Meilleur rendement (bouton « Acheter le meilleur »).</summary>
        Optimal = 0,
        /// <summary>Toujours le niveau le moins cher.</summary>
        Cheapest = 1,
        /// <summary>Un commerce abordable au hasard.</summary>
        Random = 2,
    }

    /// <summary>Profil de joueur simulé (§4.5).</summary>
    public sealed class PlayerProfile
    {
        public string Name;
        public int SessionsPerDay;
        public double SessionMinutes;
        /// <summary>Minutes de la toute première session (découverte du jeu).</summary>
        public double FirstSessionMinutes;
        public bool WatchesAds;
        /// <summary>Profil de référence pour les cibles exprimées en jours.</summary>
        public bool IsReference;
        public PurchasePolicy Purchases = PurchasePolicy.Optimal;
        /// <summary>Probabilité d'ignorer une proposition de boost x2.</summary>
        public double BoostSkipChance;
        public int Seed = 1;
        public double WakeHour = 8;
        public double SleepHour = 23;
        /// <summary>Délai de réaction avant de retaper un commerce non géré.</summary>
        public double TapReactionSeconds = 0.3;
        public double DecisionIntervalSeconds = 1;
    }

    /// <summary>Cibles de progression (§4.5 + passe d'équilibrage M1), vérifiées par la simulation.</summary>
    public sealed class ProgressionTargets
    {
        public double FirstManagerMaxMinutes = 3;
        public int FourthBusinessIndex = 3;
        public double FourthBusinessMaxMinutes = 15;
        public double FirstPrestigeMinDay = 2;
        public double FirstPrestigeMaxDay = 3;
        /// <summary>Fenêtres d'arrivée (jours calendaires) dans les villes 2, 3, 4… pour le profil de référence.</summary>
        public double[] CityMinDays = { 5, 14, 30 };
        public double[] CityMaxDays = { 7, 18, 40 };
        public double MaxWallMinutes = 30;
        public double MinAdsPerDay = 4;
        public double MaxAdsPerDay = 8;
        /// <summary>Joueur sans pub au plus N fois plus lent (arrivée en ville 2, prestiges).</summary>
        public double MaxAdGapRatio = 1.6;
        /// <summary>Multiplicateur des étoiles maximal pour le joueur de référence au jour donné.</summary>
        public double MaxStarMultiplier = 600;
        public int StarMultiplierDay = 30;
        /// <summary>Écart minimal entre les déblocages des commerces SpacedUnlockIndex et SpacedUnlockIndex+1.</summary>
        public int SpacedUnlockIndex = 5;
        public double MinUnlockSpacingMinutes = 3;
        /// <summary>À l'arrivée dans une ville : multiplicateur après / avant le voyage, au minimum.</summary>
        public double MinTravelFloorRatio = 0.3;
        /// <summary>Après une relance (confort acheté), retrouver cette part du revenu d'avant…</summary>
        public double RecoveryShare = 0.5;
        /// <summary>…en au plus ce temps de jeu actif.</summary>
        public double MaxRecoveryMinutes = 10;
        /// <summary>Seules les relances qui augmentent le multiplicateur d'au moins ce ratio sont mesurées.</summary>
        public double RecoveryMinMultiplierGain = 0.1;
    }

    public sealed class ProxyCityScaling
    {
        /// <summary>Multiplicateurs appliqués à chaque ville successive (composés).</summary>
        public double CostMultiplier = 1;
        /// <summary>Revenu par seconde (taille du marché) : appliqué au cycle, le prix unitaire reste le prix réel.</summary>
        public double RevenueMultiplier = 1;
        public double GoalMultiplier = 1;
        /// <summary>Multiplicateur d'objectif propre à chaque nouvelle ville (remplace GoalMultiplier quand fourni).</summary>
        public double[] GoalMultipliers = Array.Empty<double>();
        public double StarThresholdMultiplier = 1;
        /// <summary>Surcoût des upgrades FCFA par ville successive (en plus du multiplicateur de coût).</summary>
        public double UpgradeCostMultiplier = 1;

        /// <summary>Multiplicateur d'objectif cumulé de la ville située <paramref name="step"/> crans après la dernière ville réelle.</summary>
        public double GoalFactor(int step)
        {
            double f = 1;
            for (int i = 0; i < step; i++)
                f *= GoalMultipliers != null && i < GoalMultipliers.Length ? GoalMultipliers[i] : GoalMultiplier;
            return f;
        }
    }

    public sealed class SimulationSettings
    {
        public int Days = 45;
        public double TickSeconds = 0.1;
        /// <summary>Le joueur relance la ville quand les étoiles gagnées ≥ ce ratio × étoiles possédées.</summary>
        public double PrestigeWhenGainRatio = 1;
        /// <summary>…ou quand il n'a atteint aucun objectif depuis ce temps actif et que le gain ≥ StalledGainRatio × étoiles.</summary>
        public double PrestigeWhenStalledMinutes = 15;
        public double StalledGainRatio = 0.25;
        /// <summary>Part maximale des étoiles disponibles que le joueur accepte de dépenser pour un upgrade.</summary>
        public double UpgradeMaxShareOfStars = 0.25;
        /// <summary>Le joueur reprend un boost x2 4 h en début de session si le boost restant est sous ce seuil (h).</summary>
        public double AdBoostRewatchBelowHours = 1;
        /// <summary>Nombre maximal de boosts x2 4 h regardés par jour (un joueur réel ne relance pas à chaque session).</summary>
        public int MaxBoostAdsPerDay = 2;
        /// <summary>Absence minimale pour que le joueur regarde la pub « x2 gains hors ligne ».</summary>
        public double OfflineAdMinAwayMinutes = 60;
        public PlayerProfile[] Profiles = Array.Empty<PlayerProfile>();
        public ProgressionTargets Targets = new ProgressionTargets();
    }
}

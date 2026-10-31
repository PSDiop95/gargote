using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Gargote.Core;

namespace Gargote.Simulation
{
    public sealed class SimulationEvent
    {
        public string Profile;
        public string Kind;
        public string Detail;
        /// <summary>Temps réel écoulé depuis la première session (heures).</summary>
        public double ElapsedHours;
        /// <summary>Temps de jeu actif cumulé (minutes).</summary>
        public double ActiveMinutes;
        public double Day => ElapsedHours / 24d + 1d;
    }

    public sealed class SimulationSnapshot
    {
        public string Profile;
        public int Day;
        public int Session;
        public double ElapsedHours;
        public double ActiveMinutes;
        public BigDouble Money;
        public BigDouble CityLifetimeEarnings;
        public BigDouble TotalLifetimeEarnings;
        public BigDouble IncomePerSecond;
        public long Stars;
        /// <summary>Multiplicateur de revenus des étoiles (rendements décroissants inclus).</summary>
        public double StarMultiplier;
        public double GlobalMilestoneMultiplier;
        /// <summary>Nombre d'upgrades FCFA possédés dans le run en cours.</summary>
        public int CashUpgrades;
        /// <summary>Bonus de bienvenue actif (1 = aucun).</summary>
        public double WelcomeMultiplier;
        public int CityIndex;
        public int Prestiges;
        public int AdsWatched;
        public int ManagersHired;
        public long TotalLevels;
        public long Cauris;
    }

    public sealed class TargetCheck
    {
        public string Profile;
        public string Metric;
        public string Value;
        public string Target;
        public bool Pass;
    }

    public sealed class ProfileResult
    {
        public PlayerProfile Profile;
        public readonly List<SimulationEvent> Events = new List<SimulationEvent>();
        public readonly List<SimulationSnapshot> Snapshots = new List<SimulationSnapshot>();
        public readonly List<TargetCheck> Checks = new List<TargetCheck>();
        public int[] AdsPerDay;
        public double LongestWallMinutes;
        public string LongestWallAfter;
        public double LongestWallAtActiveMinutes;
        /// <summary>Multiplicateur après / avant chaque voyage.</summary>
        public readonly List<double> TravelRatios = new List<double>();
        /// <summary>Voyages où le bonus de bienvenue s'est bien appliqué.</summary>
        public int WelcomeApplied;
        /// <summary>Temps de remontée (min actives) après chaque relance faite avec tous les upgrades de confort.</summary>
        public readonly List<double> ComfortRecoveries = new List<double>();
        /// <summary>Temps de remontée après les relances faites sans le confort complet (information).</summary>
        public readonly List<double> EarlyRecoveries = new List<double>();
        /// <summary>
        /// Temps de retour des achats optimaux : (minutes actives, coût ÷ gain de revenu/s mesuré, en secondes).
        /// Hors attente pour réunir la somme ; hors boosts temporaires non retirés.
        /// </summary>
        public readonly List<(double ActiveMinutes, double PaybackSeconds)> Paybacks = new List<(double, double)>();

        /// <summary>Relevés quotidiens de la méta (vide si la simulation tourne sans méta).</summary>
        public readonly List<MetaDaySample> MetaDays = new List<MetaDaySample>();
        public double? FirstEpicDay;
        public double? FirstLegendaryDay;
        /// <summary>Registre final : (monnaie, raison, gagné, dépensé) — toutes les sources et tous les puits.</summary>
        public readonly List<(string Currency, string Reason, long Earned, long Spent)> MetaLedger = new List<(string, string, long, long)>();

        public SimulationEvent FirstEvent(string kind, string detail = null)
        {
            foreach (var e in Events)
                if (e.Kind == kind && (detail == null || e.Detail == detail)) return e;
            return null;
        }

        /// <summary>Dernier relevé du jour calendaire donné (fin de journée), ou null.</summary>
        public SimulationSnapshot SnapshotAtDay(int day)
        {
            SimulationSnapshot last = null;
            foreach (var s in Snapshots)
            {
                if (s.Day > day) break;
                last = s;
            }
            return last;
        }
    }

    /// <summary>Rapport pub / sans pub d'une paire de profils (≥ 1 : le joueur sans pub est plus lent).</summary>
    public sealed class AdGap
    {
        public string Pair;
        public double? City2Ratio;
        public double? PrestigeRatio;
    }

    public sealed class SimulationReport
    {
        public readonly List<ProfileResult> Profiles = new List<ProfileResult>();
        public readonly List<AdGap> AdGaps = new List<AdGap>();

        public bool AllTargetsPass
        {
            get
            {
                foreach (var p in Profiles)
                    foreach (var c in p.Checks)
                        if (!c.Pass) return false;
                return true;
            }
        }

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        private static string L(BigDouble v) => v <= BigDouble.Zero ? "0" : F(v.Log10());

        public void WriteEventsCsv(TextWriter w)
        {
            w.WriteLine("profile,event,detail,day,elapsed_hours,active_minutes");
            foreach (var p in Profiles)
                foreach (var e in p.Events)
                    w.WriteLine(string.Join(",", p.Profile.Name, e.Kind, e.Detail, F(e.Day), F(e.ElapsedHours), F(e.ActiveMinutes)));
        }

        public void WriteTimelineCsv(TextWriter w)
        {
            w.WriteLine("profile,day,session,elapsed_hours,active_minutes,money,log10_money,city_lifetime,log10_city_lifetime," +
                        "log10_total_lifetime,income_per_s,log10_income_per_s,stars,star_multiplier,global_milestone_multiplier,welcome_multiplier," +
                        "city_index,prestiges,ads_watched,managers,total_levels,cash_upgrades,cauris");
            foreach (var p in Profiles)
                foreach (var s in p.Snapshots)
                    w.WriteLine(string.Join(",",
                        p.Profile.Name, s.Day.ToString(CultureInfo.InvariantCulture), s.Session.ToString(CultureInfo.InvariantCulture),
                        F(s.ElapsedHours), F(s.ActiveMinutes),
                        NumberFormatter.Format(s.Money, '.'), L(s.Money),
                        NumberFormatter.Format(s.CityLifetimeEarnings, '.'), L(s.CityLifetimeEarnings),
                        L(s.TotalLifetimeEarnings),
                        NumberFormatter.Format(s.IncomePerSecond, '.'), L(s.IncomePerSecond),
                        s.Stars.ToString(CultureInfo.InvariantCulture), F(s.StarMultiplier), F(s.GlobalMilestoneMultiplier), F(s.WelcomeMultiplier),
                        s.CityIndex.ToString(CultureInfo.InvariantCulture),
                        s.Prestiges.ToString(CultureInfo.InvariantCulture), s.AdsWatched.ToString(CultureInfo.InvariantCulture),
                        s.ManagersHired.ToString(CultureInfo.InvariantCulture), s.TotalLevels.ToString(CultureInfo.InvariantCulture),
                        s.CashUpgrades.ToString(CultureInfo.InvariantCulture),
                        s.Cauris.ToString(CultureInfo.InvariantCulture)));
        }

        public void WriteSummaryCsv(TextWriter w)
        {
            w.WriteLine("profile,metric,value,target,pass");
            foreach (var p in Profiles)
                foreach (var c in p.Checks)
                    w.WriteLine(string.Join(",", c.Profile, c.Metric, c.Value, c.Target, c.Pass ? "OK" : "FAIL"));
        }

        public void WriteAdGapsCsv(TextWriter w)
        {
            w.WriteLine("pair,city2_ratio,prestige_ratio");
            foreach (var g in AdGaps)
                w.WriteLine(string.Join(",", g.Pair,
                    g.City2Ratio.HasValue ? F(g.City2Ratio.Value) : "",
                    g.PrestigeRatio.HasValue ? F(g.PrestigeRatio.Value) : ""));
        }

        public void WriteAll(string directory)
        {
            Directory.CreateDirectory(directory);
            using (var w = new StreamWriter(Path.Combine(directory, "simulation_ad_gaps.csv"))) WriteAdGapsCsv(w);
            using (var w = new StreamWriter(Path.Combine(directory, "simulation_events.csv"))) WriteEventsCsv(w);
            using (var w = new StreamWriter(Path.Combine(directory, "simulation_timeline.csv"))) WriteTimelineCsv(w);
            using (var w = new StreamWriter(Path.Combine(directory, "simulation_summary.csv"))) WriteSummaryCsv(w);
            using (var w = new StreamWriter(Path.Combine(directory, "simulation_meta_daily.csv"))) WriteMetaDailyCsv(w);
            using (var w = new StreamWriter(Path.Combine(directory, "simulation_meta_ledger.csv"))) WriteMetaLedgerCsv(w);
        }

        public void WriteMetaDailyCsv(TextWriter w)
        {
            w.WriteLine("profile,day,cauris,cauris_earned,cauris_spent,managers_owned,managers_available,collection_pct,epic,legendary," +
                        "player_level,chests_opened,recipes,achievement_tiers,meta_ads");
            foreach (var p in Profiles)
                foreach (var d in p.MetaDays)
                    w.WriteLine(string.Join(",", p.Profile.Name, d.Day.ToString(CultureInfo.InvariantCulture),
                        d.Cauris.ToString(CultureInfo.InvariantCulture), d.CaurisEarnedToday.ToString(CultureInfo.InvariantCulture),
                        d.CaurisSpentToday.ToString(CultureInfo.InvariantCulture), d.ManagersOwned.ToString(CultureInfo.InvariantCulture),
                        d.ManagersAvailable.ToString(CultureInfo.InvariantCulture),
                        F(d.CollectibleAvailable > 0 ? 100d * d.CollectibleOwned / d.CollectibleAvailable : 0d),
                        d.Epic.ToString(CultureInfo.InvariantCulture), d.Legendary.ToString(CultureInfo.InvariantCulture),
                        d.PlayerLevel.ToString(CultureInfo.InvariantCulture), d.ChestsOpened.ToString(CultureInfo.InvariantCulture),
                        d.Recipes.ToString(CultureInfo.InvariantCulture), d.AchievementTiers.ToString(CultureInfo.InvariantCulture),
                        d.MetaAds.ToString(CultureInfo.InvariantCulture)));
        }

        public void WriteMetaLedgerCsv(TextWriter w)
        {
            w.WriteLine("profile,currency,reason,earned,spent");
            foreach (var p in Profiles)
                foreach (var e in p.MetaLedger)
                    w.WriteLine(string.Join(",", p.Profile.Name, e.Currency, e.Reason,
                        e.Earned.ToString(CultureInfo.InvariantCulture), e.Spent.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
